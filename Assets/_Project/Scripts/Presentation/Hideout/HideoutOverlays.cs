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
            var food = Ui.Skin?.Icon("food");
            var junk = Ui.Skin?.Icon("junk");
            _withIcons = food != null && junk != null;
            if (!_withIcons)
            {
                Ui.Card(root, "HudBack", CardStyle.Tin).rectTransform.Fill();
                _resources = Ui.Label(root, "", 40, TextAnchor.MiddleLeft, Palette.Paper);
                _resources.rectTransform.Fill(30);
            }
            else
            {
                // the concept's top bar: a wide dark rounded bar; each resource is one centred block (icon + number on a plate)
                var pill = Ui.Card(root, "HudPill", CardStyle.Tin);
                pill.rectTransform.Place(new Vector2(0.02f, 0.06f), new Vector2(0.75f, 0.94f));
                pill.pixelsPerUnitMultiplier = 2.6f;
                _resources = Group(pill.rectTransform, 0.04f, "food");
                _junk = Group(pill.rectTransform, 0.52f, "junk");
            }

            if (onSettings != null)
            {
                var settings = Ui.Button(root, "Hud_Settings", "", Palette.Secondary, onSettings);
                settings.Rect.Place(new Vector2(0.75f, 0.12f), new Vector2(0.86f, 0.88f));
                var gear = Ui.Icon(settings.Rect, "settings");
                gear?.rectTransform.Place(new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
            }

            if (onDebug != null)
            {
                var debug = Ui.Button(root, "Debug", "DBG", Palette.Disabled, onDebug, 26);
                debug.Rect.Place(new Vector2(0.87f, 0.12f), new Vector2(0.98f, 0.88f));
            }

            Refresh();
        }

        /// <summary>One resource block inside the bar: the icon, then the number on a dark plate.</summary>
        private static TMP_Text Group(RectTransform pill, float x0, string icon)
        {
            var block = Ui.Rect(pill, "Block_" + icon).Place(new Vector2(x0, 0.12f), new Vector2(x0 + 0.44f, 0.88f));
            var plate = Ui.Panel(block, "Plate", new Color(0f, 0f, 0f, 0.45f));
            plate.sprite = Ui.Skin?.barFrame;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 3f;
            plate.rectTransform.Place(new Vector2(0.3f, 0.08f), new Vector2(1f, 0.92f));
            var picture = Ui.Icon(block, icon);
            // set by hand in the editor: a bit smaller and nudged right, so it stays on the bar
            picture.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0.36f, 1f), new Vector2(41.5f, 2f), new Vector2(41.5f, 2f));
            picture.rectTransform.localScale = new Vector3(0.85f, 0.85f, 1f);
            var label = Ui.Label(plate.transform, "", 46, TextAnchor.MiddleCenter, Palette.Paper, heading: true);
            label.rectTransform.Fill(4);
            return label;
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
            _text.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -210), new Vector2(-40, -140));
            _text.Outlined();
            _text.color = new Color(1, 1, 1, 0);
            _icon = Ui.Panel(_text.transform, "Icon", Color.white);
            _icon.preserveAspect = true;
            _icon.rectTransform.anchorMin = _icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _icon.rectTransform.sizeDelta = new Vector2(80, 80);
            _icon.gameObject.SetActive(false);
        }

        private readonly Image _icon;

        /// <param name="icon">Optional skin icon shown left of the text (attribute of a level-up...).</param>
        public void Show(string message, string icon = null)
        {
            _text.DOKill();
            _icon.DOKill();
            _text.text = message;
            _text.color = Palette.Paper;
            _text.DOFade(0f, 0.6f).SetDelay(1.6f).SetLink(_text.gameObject);
            var sprite = icon != null ? Ui.Skin?.Icon(icon) : null;
            _icon.gameObject.SetActive(sprite != null);
            if (sprite == null) return;
            _icon.sprite = sprite;
            _icon.color = Color.white;
            _icon.rectTransform.anchoredPosition = new Vector2(-_text.preferredWidth * 0.5f - 50f, 0);
            _icon.DOFade(0f, 0.6f).SetDelay(1.6f).SetLink(_icon.gameObject);
        }
    }

    /// <summary>
    /// "Пока тебя не было": what happened while the player was away, as icons — resources gained, who grew which
    /// attribute, new skills, and which cats stopped and why (they wait for the player).
    /// </summary>
    public sealed class OfflineSummaryModal
    {
        private const float RowHeight = 96f;
        private const int MaxRows = 3;

        private readonly OverlayStack _overlays;
        private readonly Func<TrashCats.Domain.CatId, Sprite> _portrait;
        private readonly GameObject _root;
        private readonly TMP_Text _time;
        private readonly RectTransform _content;

        public OfflineSummaryModal(RectTransform parent, OverlayStack overlays, Func<TrashCats.Domain.CatId, Sprite> portrait = null)
        {
            _overlays = overlays;
            _portrait = portrait;
            var dim = Ui.Panel(parent, "OfflineSummary", Palette.Dim, raycast: true);
            dim.rectTransform.Fill();
            _root = dim.gameObject;

            var panel = Ui.Card(dim.transform, "Card", CardStyle.Paper, raycast: true);
            panel.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(940, 1240));

            Ui.SetBadge(Ui.Badge(panel.transform, 190f), "clock");
            var title = Ui.Label(panel.transform, Loc.T("hideout_overlays.02"), 46, TextAnchor.UpperCenter, Palette.Ink);
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(40, -170), new Vector2(-40, -100));
            _time = Ui.Label(panel.transform, "", 30, TextAnchor.UpperCenter, Palette.Ink);
            _time.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(40, -220), new Vector2(-40, -172));

            _content = Ui.Rect(panel.transform, "Content").Place(Vector2.zero, Vector2.one, new Vector2(60, 170), new Vector2(-60, -235));
            var column = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 10;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var ok = Ui.Button(panel.transform, "Ok", Loc.T("hideout_overlays.03"), Palette.Primary, () => _overlays.Close(_root));
            ok.Rect.Place(new Vector2(0.25f, 0), new Vector2(0.75f, 0), new Vector2(0, 40), new Vector2(0, 140));

            _root.SetActive(false);
        }

        public void Show(OfflineSummary summary)
        {
            _time.text = Loc.T("hideout_overlays.04", Texts.Duration(summary.RealElapsedMs))
                         + (summary.WasCapped ? Loc.T("hideout_overlays.05", Texts.Duration(summary.SimulatedMs)) : "");
            for (var i = _content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);

            // resources
            if (summary.FoodGained > 0 || summary.JunkGained > 0)
            {
                var row = Row(TextAnchor.MiddleCenter, 40);
                if (summary.FoodGained > 0) Chip(row, "food", "+" + summary.FoodGained);
                if (summary.JunkGained > 0) Chip(row, "junk", "+" + summary.JunkGained);
            }

            // grown attributes: portrait, attribute icon, new level (highest per cat and attribute)
            var levels = summary.LevelUps.GroupBy(e => (e.CatId, e.Attribute)).Select(g => (g.Key.CatId, g.Key.Attribute, Level: g.Max(e => e.NewLevel))).ToList();
            if (levels.Count > 0)
            {
                Header(Loc.T("offline.levels"));
                foreach (var cat in levels.GroupBy(l => l.CatId).Take(MaxRows))
                {
                    var row = Row(TextAnchor.MiddleLeft, 18);
                    Portrait(row, cat.Key);
                    foreach (var level in cat)
                        Chip(row, Texts.AttributeIcon(level.Attribute), level.Level.ToString(), 70f);
                }
            }

            if (summary.SkillsUnlocked.Count > 0)
            {
                Header(Loc.T("offline.skills"));
                foreach (var unlocked in summary.SkillsUnlocked.Take(MaxRows))
                {
                    var row = Row(TextAnchor.MiddleLeft, 18);
                    Portrait(row, unlocked.CatId);
                    Chip(row, "skill", Texts.SkillName(unlocked.SkillId), 70f);
                }
            }

            // cats that stopped and wait for the player, with the reason
            var waiting = summary.Stopped.Where(s => Texts.IdleBubble(s.Reason).Length > 0).GroupBy(s => s.CatId).Select(g => g.Last()).ToList();
            if (waiting.Count > 0)
            {
                Header(Loc.T("offline.waiting"));
                // two cats with their bubbles per row: three did not fit the card's width
                RectTransform row = null;
                var shown = waiting.Take(MaxRows * 2).ToList();
                for (var i = 0; i < shown.Count; i++)
                {
                    if (i % 2 == 0) row = Row(TextAnchor.MiddleLeft, 16, RowHeight + 24);
                    CatWithBubble(row, shown[i].CatId, shown[i].Reason);
                    if (i % 2 == 0 && i + 1 < shown.Count) Spacer(row, 24);
                }
            }

            if (_content.childCount == 0)
                Header(Loc.T("offline.nothing"));

            _overlays.Push(_root);
        }

        private RectTransform Row(TextAnchor align, float spacing, float height = RowHeight)
        {
            var row = Ui.Rect(_content, "Row");
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = align;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        private void Header(string text)
        {
            var label = Ui.Label(_content, text, 32, TextAnchor.LowerLeft, Palette.Ink, heading: true);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
        }

        private static void Fixed(Component c, float width, float height)
        {
            var element = c.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = width;
            element.preferredHeight = height;
        }

        /// <summary>Icon followed by a short text (number, level, skill name).</summary>
        private static void Chip(RectTransform row, string icon, string text, float size = 86f)
        {
            var picture = Ui.Icon(row, icon);
            if (picture != null)
                Fixed(picture, size * picture.sprite.rect.width / picture.sprite.rect.height, size);
            var label = Ui.Label(row, text, 40, TextAnchor.MiddleLeft, Palette.Paper, heading: true);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
        }

        private void Portrait(RectTransform row, TrashCats.Domain.CatId cat)
        {
            var cell = Ui.Rect(row, "Portrait");
            Fixed(cell, RowHeight, RowHeight);
            var picture = new RoundPicture(cell, "Round", CatColors.Backdrop(cat), 0.8f);
            picture.Root.Fill();
            picture.SetPicture(_portrait?.Invoke(cat));
        }

        private static void Spacer(RectTransform row, float width) => Fixed(Ui.Rect(row, "Gap"), width, RowHeight);

        /// <summary>
        /// The cat's round portrait with the idle reason (and its icon) in a speech bubble whose tail comes out of the
        /// portrait's right side, so it is clear who says it.
        /// </summary>
        private void CatWithBubble(RectTransform row, TrashCats.Domain.CatId cat, TrashCats.Domain.IdleReason reason)
        {
            const float bubbleWidth = 210f;
            var cell = Ui.Rect(row, "CatBubble");
            Fixed(cell, RowHeight + bubbleWidth - 20f, RowHeight + 24);
            var portrait = Ui.Rect(cell, "Portrait").Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(RowHeight, RowHeight));
            var picture = new RoundPicture(portrait, "Round", CatColors.Backdrop(cat), 0.8f);
            picture.Root.Fill();
            picture.SetPicture(_portrait?.Invoke(cat));
            var bubble = new SpeechBubble(cell, "Bubble", new Vector2(bubbleWidth, 80), 26);
            bubble.Root.anchorMin = bubble.Root.anchorMax = Vector2.zero;
            bubble.Root.anchoredPosition = new Vector2(RowHeight * 0.92f, RowHeight * 0.5f);
            bubble.Set(Texts.IdleBubble(reason), Texts.IdleIcon(reason));
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
