using System;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>Upgrade popup for a hideout object (Docs/GDD/UI_UX.md §4.2).</summary>
    public sealed class UpgradePanel
    {
        private readonly GameSession _session;
        private readonly OverlayStack _overlays;
        private readonly Action<CommandResult> _onResult;
        private readonly GameObject _root;
        private readonly TMP_Text _text;
        private readonly UiButton _buy;
        private UpgradeId? _upgrade;

        public UpgradePanel(GameSession session, RectTransform parent, OverlayStack overlays, Action<CommandResult> onResult)
        {
            _session = session;
            _overlays = overlays;
            _onResult = onResult;
            var dim = Ui.Panel(parent, "UpgradePanel", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _overlays.Close(_root));
            _root = dim.gameObject;
            var card = Ui.Card(dim.transform, "Card", CardStyle.Paper, raycast: true);
            card.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(880, 700));
            _text = Ui.Label(card.transform, "", 36, TextAnchor.UpperCenter, Palette.Ink);
            _text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(40, 180), new Vector2(-40, -40));
            _buy = Ui.Button(card.transform, "Upgrade_Buy", "", Palette.Primary, Buy, 38);
            _buy.Rect.Place(new Vector2(0.15f, 0), new Vector2(0.85f, 0), new Vector2(0, 40), new Vector2(0, 150));
            _root.SetActive(false);
        }

        public void Open(UpgradeId upgrade)
        {
            _upgrade = upgrade;
            Refresh();
            _overlays.Push(_root, () => _upgrade = null);
        }

        public void Refresh()
        {
            if (_upgrade == null) return;
            var config = _session.Config;
            var definition = config.GetUpgrade(_upgrade.Value);
            var level = _session.World.GetUpgradeLevel(definition.Id);
            var next = level < definition.MaxLevel ? definition.Costs[level] : (int?)null;
            _text.text = Loc.T("upgrade_panel.01", Texts.UpgradeName(definition.Id), level, definition.MaxLevel, Effect(config, definition, level))
                         + (next != null ? $"\n→ {Effect(config, definition, level + 1)}" : Loc.T("upgrade_panel.02"));
            _buy.Interactable = next != null && _session.World.Junk >= next.Value;
            _buy.Text.text = next == null ? Loc.T("upgrade_panel.03") : _session.World.Junk >= next.Value ? Loc.T("upgrade_panel.04", next) : Loc.T("upgrade_panel.05", next, _session.World.Junk);
        }

        private void Buy()
        {
            if (_upgrade == null) return;
            var result = _session.BuyUpgrade(_upgrade.Value);
            _onResult(result);
            Refresh();
        }

        private static int Base(GameConfig config, ActivityKind kind) =>
            System.Linq.Enumerable.First(config.Activities, a => a.Kind == kind).BaseOutput;

        private static string Effect(GameConfig config, UpgradeDefinition definition, int level) => definition.Effect switch
        {
            SleepEnergyBonusEffect e => Loc.T("upgrade_panel.06", Base(config, ActivityKind.Sleep) + e.BonusPerLevel[level]),
            ResourceOutputBonusEffect e => Loc.T("upgrade_panel.07", Base(config, ActivityKind.Scavenge) + e.FoodPerLevel[level], Base(config, ActivityKind.Rummage) + e.JunkPerLevel[level]),
            TrainingXpBonusEffect e => Loc.T("upgrade_panel.08", Texts.AttributeGenitive(e.Attribute).ToLowerInvariant(), e.PctPerLevel[level]),
            _ => "",
        };
    }
}
