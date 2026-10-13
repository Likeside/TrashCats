using System;
using System.Linq;
using System.Text;
using DG.Tweening;
using TrashCats.App;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>Top bar with the gang's resources and (in development builds) the debug button.</summary>
    public sealed class HudView
    {
        private readonly GameSession _session;
        private readonly TMP_Text _resources;
        private readonly TMP_Text _junk;
        private readonly bool _withIcons;

        public HudView(GameSession session, RectTransform root, Action onDebug, Action onSettings = null)
        {
            _session = session;
            Ui.Card(root, "HudBack", CardStyle.Tin).rectTransform.Fill();
            _resources = Ui.Label(root, "", 40, TextAnchor.MiddleLeft, Palette.Paper);
            _resources.rectTransform.Fill(30);
            // with the skin: fish-bone and bolt icons in front of the numbers
            var food = Ui.Icon(root, "food");
            var junk = Ui.Icon(root, "junk");
            if (food != null && junk != null)
            {
                _withIcons = true;
                food.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(28, 14), new Vector2(110, -14));
                junk.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(300, 14), new Vector2(370, -14));
                _resources.rectTransform.Fill();
                _resources.rectTransform.offsetMin = new Vector2(120, 0);
                _junk = Ui.Label(root, "", 44, TextAnchor.MiddleLeft, Palette.Paper);
                _junk.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(382, 0), Vector2.zero);
                _resources.fontSize = 44;
            }

            if (onSettings != null)
            {
                // gear on a small square button, left of the debug button
                var settings = Ui.Button(root, "Hud_Settings", "", Palette.Secondary, onSettings);
                settings.Rect.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-260, 12), new Vector2(-170, -12));
                var gear = Ui.Icon(settings.Rect, "settings");
                gear?.rectTransform.Place(new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f));
            }

            if (onDebug != null)
            {
                var debug = Ui.Button(root, "Debug", "DBG", Palette.Disabled, onDebug, 28);
                debug.Rect.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-150, 15), new Vector2(-20, -15));
            }

            Refresh();
        }

        public void Refresh()
        {
            if (_withIcons)
            {
                _resources.text = _session.World.Food.ToString();
                _junk.text = _session.World.Junk.ToString();
            }
            else
                _resources.text = Loc.T("hideout_overlays.01", _session.World.Food, _session.World.Junk);
        }
    }

    /// <summary>Short message line for rejected commands.</summary>
    public sealed class ToastView
    {
        private readonly TMP_Text _text;

        public ToastView(RectTransform parent)
        {
            _text = Ui.Label(parent, "", 34, TextAnchor.MiddleCenter, Palette.Paper);
            _text.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -90), new Vector2(-40, -20));
            _text.Outlined();
            _text.color = new Color(1, 1, 1, 0);
        }

        public void Show(string message)
        {
            _text.DOKill();
            _text.text = message;
            _text.color = Palette.Paper;
            _text.DOFade(0f, 0.6f).SetDelay(1.6f).SetLink(_text.gameObject);
        }
    }

    /// <summary>"Пока тебя не было" modal built from an <see cref="OfflineSummary"/>.</summary>
    public sealed class OfflineSummaryModal
    {
        private readonly OverlayStack _overlays;
        private readonly GameObject _root;
        private readonly TMP_Text _body;

        public OfflineSummaryModal(RectTransform parent, OverlayStack overlays)
        {
            _overlays = overlays;
            var dim = Ui.Panel(parent, "OfflineSummary", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            _root = dim.gameObject;

            var panel = Ui.Card(dim.transform, "Card", CardStyle.Paper, raycast: true);
            panel.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(900, 900));

            var title = Ui.Label(panel.transform, Loc.T("hideout_overlays.02"), 46, TextAnchor.UpperCenter, Palette.Ink);
            title.rectTransform.Fill(40);
            _body = Ui.Label(panel.transform, "", 32, TextAnchor.UpperLeft, Palette.Ink);
            _body.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(50, 160), new Vector2(-50, -120));

            var ok = Ui.Button(panel.transform, "Ok", Loc.T("hideout_overlays.03"), Palette.Primary, () => _overlays.Close(_root));
            ok.Rect.Place(new Vector2(0.25f, 0), new Vector2(0.75f, 0), new Vector2(0, 40), new Vector2(0, 140));

            _root.SetActive(false);
        }

        public void Show(OfflineSummary summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Loc.T("hideout_overlays.04", Texts.Duration(summary.RealElapsedMs)) + (summary.WasCapped ? Loc.T("hideout_overlays.05", Texts.Duration(summary.SimulatedMs)) : ""));
            if (summary.FoodGained > 0) sb.AppendLine(Loc.T("hideout_overlays.06", summary.FoodGained));
            if (summary.JunkGained > 0) sb.AppendLine(Loc.T("hideout_overlays.07", summary.JunkGained));
            foreach (var group in summary.LevelUps.GroupBy(e => (e.CatId, e.Attribute)))
                sb.AppendLine($"{Texts.CatName(group.Key.CatId)}: {Texts.Attribute(group.Key.Attribute)} {group.Max(e => e.NewLevel)}");
            foreach (var unlocked in summary.SkillsUnlocked)
                sb.AppendLine(Loc.T("hideout_overlays.08", Texts.CatName(unlocked.CatId), Texts.SkillName(unlocked.SkillId)));
            foreach (var stopped in summary.Stopped)
            {
                var reason = Texts.IdleBubble(stopped.Reason);
                if (reason.Length > 0)
                    sb.AppendLine($"{Texts.CatName(stopped.CatId)}: {reason.ToLowerInvariant()}");
            }

            _body.text = sb.ToString();
            _overlays.Push(_root);
        }
    }

    /// <summary>Development-only cheats: time scale, resources, skipping time and resetting progress.</summary>
    public sealed class DebugPanel
    {
        private readonly OverlayStack _overlays;
        private readonly GameObject _root;
        private readonly TMP_Text _scale;
        private readonly GameSession _session;

        public DebugPanel(RectTransform parent, OverlayStack overlays, GameSession session, DebugCommands debug, Action resetProgress)
        {
            _overlays = overlays;
            _session = session;
            var dim = Ui.Panel(parent, "DebugPanel", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            _root = dim.gameObject;

            var panel = Ui.Card(dim.transform, "Card", CardStyle.Tin, raycast: true);
            panel.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(900, 1260));
            _scale = Ui.Label(panel.transform, "", 40, TextAnchor.UpperCenter, Palette.Paper);
            _scale.rectTransform.Fill(30);

            void Add(int row, int col, string label, Action action)
            {
                var b = Ui.Button(panel.transform, label, label, Palette.Secondary, () => { action(); Refresh(); }, 32);
                b.Rect.Place(new Vector2(col * 0.5f, 1), new Vector2((col + 1) * 0.5f, 1),
                    new Vector2(30, -120 - (row + 1) * 140), new Vector2(-30, -120 - row * 140 - 20));
            }

            Add(0, 0, Loc.T("hideout_overlays.09"), () => session.DebugTimeScale = 1);
            Add(0, 1, Loc.T("hideout_overlays.10"), () => session.DebugTimeScale = 5);
            Add(1, 0, Loc.T("hideout_overlays.11"), () => session.DebugTimeScale = 20);
            Add(1, 1, Loc.T("hideout_overlays.12"), () => debug.SkipGameTime(10 * 60_000));
            Add(2, 0, Loc.T("hideout_overlays.13"), () => debug.AddResources(100, 0));
            Add(2, 1, Loc.T("hideout_overlays.14"), () => debug.AddResources(0, 100));
            Add(3, 0, Loc.T("hideout_overlays.15"), debug.UnlockAllTerritories);
            Add(3, 1, Loc.T("hideout_overlays.16"), debug.RecruitAllAndRest);
            Add(4, 0, Loc.T("hideout_overlays.17"), resetProgress);
            Add(4, 1, Loc.T("hideout_overlays.18"), () => _overlays.Close(_root));

            _root.SetActive(false);
        }

        public void Open()
        {
            Refresh();
            _overlays.Push(_root);
        }

        private void Refresh() => _scale.text = Loc.T("hideout_overlays.19", _session.DebugTimeScale);
    }
}
