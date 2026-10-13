using System;
using System.Linq;
using System.Text;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Battle
{
    /// <summary>Battle results (Docs/GDD/UI_UX.md §9), shown before the pending battle is applied.</summary>
    public sealed class ResultsModal
    {
        private readonly GameSession _session;
        private readonly OverlayStack _overlays;
        private readonly GameObject _root;
        private readonly TMP_Text _title;
        private readonly TMP_Text _body;
        private Action<TerritoryId, bool> _onDone;
        private TerritoryId _territory;

        public ResultsModal(GameSession session, RectTransform parent, OverlayStack overlays)
        {
            _session = session;
            _overlays = overlays;
            var dim = Ui.Panel(parent, "Results", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            _root = dim.gameObject;

            var card = Ui.Card(dim.transform, "Card", CardStyle.Paper, raycast: true);
            card.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(940, 1100));
            _title = Ui.Label(card.transform, "", 64, TextAnchor.UpperCenter, Palette.Ink);
            _title.rectTransform.Fill(50);
            _body = Ui.Label(card.transform, "", 34, TextAnchor.UpperLeft, Palette.Ink);
            _body.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(60, 200), new Vector2(-60, -150));

            var cont = Ui.Button(card.transform, "Results_Continue", Loc.T("results_modal.01"), Palette.Primary, () => Close(false), 40);
            cont.Rect.Place(new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(40, 40), new Vector2(-15, 160));
            var retry = Ui.Button(card.transform, "Results_Retry", Loc.T("results_modal.02"), Palette.Secondary, () => Close(true), 40);
            retry.Rect.Place(new Vector2(0.5f, 0), new Vector2(1f, 0), new Vector2(15, 40), new Vector2(-40, 160));
            _root.SetActive(false);
        }

        public void SetHandler(Action<TerritoryId, bool> onDone) => _onDone = onDone;

        public void Show(PendingBattle battle)
        {
            var world = _session.World;
            var config = _session.Config;
            var territory = config.GetTerritory(battle.Territory);
            _territory = battle.Territory;
            var victory = battle.Result.Outcome == BattleOutcome.Victory;
            var first = victory && world.Territories.Get(territory.Id).Status != TerritoryStatus.Captured;

            _title.text = victory ? Loc.T("results_modal.03") : Loc.T("results_modal.04");
            var sb = new StringBuilder();
            sb.AppendLine(Texts.TerritoryName(territory.Id) + (first ? Loc.T("results_modal.05") : ""));
            sb.AppendLine();
            if (victory)
            {
                var (food, junk) = TerritoryProgress.Reward(territory, first);
                sb.AppendLine(Loc.T("results_modal.06", food, junk));
                if (first && territory.Bonus != TerritoryBonusKind.None) sb.AppendLine(Loc.T("results_modal.07", Texts.Bonus(territory)));
                if (first && territory.RecruitCat is CatId recruit && !world.TryGetCat(recruit, out _)) sb.AppendLine(Loc.T("results_modal.08", Texts.CatName(recruit)));
            }

            var fought = battle.Result.FoughtPlayerIds.Select(id => Texts.CatName(new CatId(id)));
            sb.AppendLine();
            sb.AppendLine(victory
                ? Loc.T("results_modal.09", string.Join(", ", fought), config.Progression.BattleXpWin, config.Needs.BattleWinEnergyCost)
                : Loc.T("results_modal.10", string.Join(", ", fought), config.Progression.BattleXpLoss));
            if (!victory)
            {
                sb.AppendLine();
                foreach (var hint in DefeatHints.For(battle, config))
                    sb.AppendLine("• " + Loc.T(hint));
            }

            _body.text = sb.ToString();
            _overlays.Push(_root);
        }

        private void Close(bool retry)
        {
            _overlays.Close(_root);
            _onDone?.Invoke(_territory, retry);
        }
    }
}
