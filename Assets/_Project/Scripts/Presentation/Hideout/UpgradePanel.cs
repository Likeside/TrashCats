using System;
using System.Linq;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Upgrade popup for a hideout object (Docs/GDD/UI_UX.md §4.2): the object's picture, its name and level pips, what
    /// it gives now and after the upgrade as icons with values (the gain in green), and the buy button with the junk price.
    /// </summary>
    public sealed class UpgradePanel
    {
        private readonly GameSession _session;
        private readonly OverlayStack _overlays;
        private readonly Action<CommandResult> _onResult;
        private readonly Func<UpgradeId, Sprite> _picture;
        private readonly GameObject _root;
        private readonly Image _prop;
        private readonly TMP_Text _title;
        private readonly RectTransform _pips;
        private readonly TMP_Text _effectName;
        private readonly RectTransform _now;
        private readonly RectTransform _next;
        private readonly TMP_Text _nextCaption;
        private readonly UiButton _buy;
        private readonly RectTransform _price;
        private UpgradeId? _upgrade;

        public UpgradePanel(GameSession session, RectTransform parent, OverlayStack overlays, Action<CommandResult> onResult, Func<UpgradeId, Sprite> picture = null)
        {
            _session = session;
            _overlays = overlays;
            _onResult = onResult;
            _picture = picture;
            var dim = Ui.Panel(parent, "UpgradePanel", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _overlays.Close(_root));
            _root = dim.gameObject;
            var card = Ui.Card(dim.transform, "Card", CardStyle.Paper, raycast: true);
            card.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(900, 1080));
            Ui.SetBadge(Ui.Badge(card.transform, 190f), "tools");

            _title = Ui.Label(card.transform, "", 46, TextAnchor.MiddleCenter, Palette.Ink, heading: true);
            _title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(40, -170), new Vector2(-40, -105));
            _pips = Info.Row(card.transform, "Pips", 10f, TextAnchor.MiddleCenter);
            _pips.Place(new Vector2(0, 1), Vector2.one, new Vector2(40, -220), new Vector2(-40, -172));

            // the object itself, so the player sees what is being upgraded
            _prop = Ui.Panel(card.transform, "Prop", Color.white);
            _prop.preserveAspect = true;
            _prop.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-170, -470), new Vector2(170, -235));

            _effectName = Ui.Label(card.transform, "", 30, TextAnchor.MiddleCenter, Palette.Ink, heading: true);
            _effectName.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(40, -530), new Vector2(-40, -480));

            Caption(card.transform, Loc.T("upgrade_panel.09"), -545);
            _now = EffectRow(card.transform, "Now", -590);
            _nextCaption = Caption(card.transform, Loc.T("upgrade_panel.10"), -705);
            _next = EffectRow(card.transform, "Next", -750);

            _buy = Ui.Button(card.transform, "Upgrade_Buy", "", Palette.Primary, Buy, 38);
            _buy.Rect.Place(new Vector2(0.12f, 0), new Vector2(0.88f, 0), new Vector2(0, 40), new Vector2(0, 160));
            // "Улучшить [junk] 20": the price with the junk icon, centred on the button
            _buy.Text.gameObject.SetActive(false);
            _price = Info.Row(_buy.Rect, "Price", 14f, TextAnchor.MiddleCenter);
            _price.Fill(20);
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
            var maxed = level >= definition.MaxLevel;
            var cost = maxed ? 0 : definition.Costs[level];
            var junk = _session.World.Junk;

            _title.text = Texts.UpgradeName(definition.Id);
            Clear(_pips);
            Info.Text(_pips, Loc.T("upgrade_panel.11"), 28, Palette.Ink, height: 44);
            for (var i = 0; i < definition.MaxLevel; i++)
            {
                var pip = Ui.Panel(_pips, "Pip", i < level ? Palette.Primary : new Color(0, 0, 0, 0.15f));
                pip.sprite = Ui.Skin?.circle;
                Info.Size(pip, 34, 34);
                pip.gameObject.AddComponent<Outline>().effectColor = Palette.Ink;
            }
            Info.Text(_pips, $"{level}/{definition.MaxLevel}", 28, Palette.Ink, height: 44);

            var sprite = _picture?.Invoke(definition.Id);
            _prop.sprite = sprite;
            _prop.gameObject.SetActive(sprite != null);

            _effectName.text = EffectName(definition);
            Fill(_now, Values(config, definition, level), null);
            _nextCaption.gameObject.SetActive(!maxed);
            _next.gameObject.SetActive(!maxed);
            if (!maxed)
                Fill(_next, Values(config, definition, level + 1), Values(config, definition, level));

            _buy.Interactable = !maxed && junk >= cost;
            Clear(_price);
            if (maxed)
                Info.Text(_price, Loc.T("upgrade_panel.12"), 38, Palette.Paper);
            else
            {
                Info.Text(_price, junk >= cost ? Loc.T("upgrade_panel.13") : Loc.T("upgrade_panel.14"), 38, Palette.Paper);
                Info.Chip(_price, "junk", cost.ToString(), 70f, 44, Palette.Paper);
                if (junk < cost)
                    Info.Text(_price, Loc.T("upgrade_panel.15", junk), 30, Palette.Paper);
            }
            foreach (var label in _price.GetComponentsInChildren<TMP_Text>())
                label.Outlined();
        }

        private void Buy()
        {
            if (_upgrade == null) return;
            var result = _session.BuyUpgrade(_upgrade.Value);
            _onResult(result);
            Refresh();
        }

        private static TMP_Text Caption(Transform card, string text, float top)
        {
            var label = Ui.Label(card, text, 28, TextAnchor.MiddleLeft, Palette.Ink, heading: true);
            label.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(70, top - 40), new Vector2(-70, top));
            return label;
        }

        private static RectTransform EffectRow(Transform card, string name, float top)
        {
            var back = Ui.Panel(card, name, new Color(0, 0, 0, 0.08f));
            back.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(60, top - 100), new Vector2(-60, top));
            var row = Info.Row(back.transform, "Values", 40f, TextAnchor.MiddleCenter);
            row.Fill(8);
            return row;
        }

        /// <summary>Icon chips with values; with <paramref name="before"/> the values that grow are green with "+delta".</summary>
        private static void Fill(RectTransform row, (string Icon, int Value, string Suffix)[] values, (string Icon, int Value, string Suffix)[] before)
        {
            Clear(row);
            for (var i = 0; i < values.Length; i++)
            {
                var (icon, value, suffix) = values[i];
                var delta = before != null ? value - before[i].Value : 0;
                var text = $"+{value}{suffix}" + (delta > 0 ? $" <size=30>(+{delta}{(suffix.StartsWith("%") ? "%" : "")})</size>" : "");
                Info.Chip(row, icon, text, 76f, 44, delta > 0 ? Info.Gain : Palette.Ink);
            }
        }

        private static void Clear(RectTransform rect)
        {
            for (var i = rect.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(rect.GetChild(i).gameObject);
        }

        private static int Base(GameConfig config, ActivityKind kind) => config.Activities.First(a => a.Kind == kind).BaseOutput;

        private static string EffectName(UpgradeDefinition definition) => definition.Effect switch
        {
            SleepEnergyBonusEffect => Loc.T("upgrade_panel.16"),
            ResourceOutputBonusEffect => Loc.T("upgrade_panel.17"),
            TrainingXpBonusEffect e => Loc.T("upgrade_panel.18", Texts.AttributeGenitive(e.Attribute).ToLowerInvariant()),
            _ => "",
        };

        /// <summary>What the object gives at a level: (icon, value, suffix) per resource or bonus.</summary>
        private static (string Icon, int Value, string Suffix)[] Values(GameConfig config, UpgradeDefinition definition, int level) => definition.Effect switch
        {
            SleepEnergyBonusEffect e => new[] { ("energy", Base(config, ActivityKind.Sleep) + e.BonusPerLevel[level], "") },
            ResourceOutputBonusEffect e => new[]
            {
                ("food", Base(config, ActivityKind.Scavenge) + e.FoodPerLevel[level], ""),
                ("junk", Base(config, ActivityKind.Rummage) + e.JunkPerLevel[level], ""),
            },
            TrainingXpBonusEffect e => new[] { (Texts.AttributeIcon(e.Attribute), e.PctPerLevel[level], Loc.T("upgrade_panel.19")) },
            _ => Array.Empty<(string, int, string)>(),
        };
    }
}
