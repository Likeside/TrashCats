using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// Small building blocks for information windows: rows laid out left to right, "icon + value" chips, attribute
    /// rows with a level bar, and the two-sided win/lose bar.
    /// </summary>
    public static class Info
    {
        public static readonly Color Gain = Palette.Hex("#3E8E2F");

        /// <summary>A row that lays its children out left to right at their preferred sizes.</summary>
        public static RectTransform Row(Transform parent, string name, float spacing = 12f, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var row = Ui.Rect(parent, name);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = align;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        /// <summary>A column that stacks its children top to bottom at their preferred heights, full width.</summary>
        public static RectTransform Column(Transform parent, string name, float spacing = 8f)
        {
            var column = Ui.Rect(parent, name);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return column;
        }

        public static void Size(Component c, float width, float height)
        {
            var element = c.TryGetComponent<LayoutElement>(out var existing) ? existing : c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) element.preferredWidth = element.minWidth = width;
            if (height >= 0) element.preferredHeight = element.minHeight = height;
        }

        /// <summary>An icon of the given height inside a layout row (keeps the sprite's aspect); null without art.</summary>
        public static Image Icon(Transform row, string key, float height)
        {
            var icon = Ui.Icon(row, key);
            if (icon != null)
                Size(icon, height * icon.sprite.rect.width / icon.sprite.rect.height, height);
            return icon;
        }

        /// <summary>A text inside a layout row, as wide as it needs.</summary>
        public static TMP_Text Text(Transform row, string text, int fontSize, Color color, bool heading = true, float height = -1f)
        {
            var label = Ui.Label(row, text, fontSize, TextAnchor.MiddleLeft, color, heading);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (height > 0) Size(label, -1, height);
            return label;
        }

        /// <summary>"[icon] value" as one unit inside a row.</summary>
        public static RectTransform Chip(Transform row, string icon, string text, float height, int fontSize, Color color, bool heading = true)
        {
            var chip = Row(row, "Chip_" + icon, 6f);
            Size(chip, -1, height);
            Icon(chip, icon, height);
            Text(chip, text, fontSize, color, heading, height);
            return chip;
        }

        /// <summary>Attribute line: icon, name, big level number and an XP bar with "xp/next" on it.</summary>
        public static UiBar AttributeRow(Transform parent, string icon, string name, int level, float progress01, string barText, bool aptitude, float height = 76f)
        {
            var row = Ui.Rect(parent, "Attr_" + icon);
            Size(row, -1, height);
            var picture = Ui.Icon(row, icon);
            picture?.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 4), new Vector2(height - 8, -4));
            if (aptitude && Ui.Icon(row, "crown") is Image crown)
            {
                // the cat's aptitude: a small crown on the icon's corner
                crown.rectTransform.Place(new Vector2(0, 0.55f), new Vector2(0, 1.1f), new Vector2(height * 0.55f, 0), new Vector2(height + 4, 0));
                crown.transform.SetAsLastSibling();
            }
            var label = Ui.Label(row, name, 32, TextAnchor.MiddleLeft, Palette.Ink, heading: true);
            label.rectTransform.Place(Vector2.zero, new Vector2(0.42f, 1), new Vector2(height + 4, 0), Vector2.zero);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var value = Ui.Label(row, level.ToString(), 44, TextAnchor.MiddleCenter, Palette.Ink, heading: true);
            value.rectTransform.Place(new Vector2(0.42f, 0), new Vector2(0.52f, 1));
            var bar = Ui.Bar(row, "Xp", Palette.Progress);
            bar.Rect.Place(new Vector2(0.53f, 0.18f), new Vector2(1, 0.82f));
            bar.Set(progress01, barText);
            return bar;
        }

        /// <summary>Two bars that meet: the win share from the left in green, the loss share from the right in red.</summary>
        public sealed class SplitBar
        {
            private readonly RectTransform _win;
            private readonly RectTransform _loss;
            private readonly TMP_Text _winText;
            private readonly TMP_Text _lossText;

            public RectTransform Rect { get; }

            public SplitBar(Transform parent, string name)
            {
                var frame = Ui.Panel(parent, name, Palette.Ink);
                Rect = frame.rectTransform;
                var inner = Ui.Rect(frame.transform, "Inner").Fill(5);
                _win = Ui.Panel(inner, "Win", Palette.Hex("#4CAF3F")).rectTransform;
                _loss = Ui.Panel(inner, "Loss", Palette.Warning).rectTransform;
                _winText = Ui.Label(frame.transform, "", 30, TextAnchor.MiddleLeft, Palette.Paper, heading: true);
                _winText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(30, 4), new Vector2(-30, -4));
                _winText.Outlined();
                _lossText = Ui.Label(frame.transform, "", 30, TextAnchor.MiddleRight, Palette.Paper, heading: true);
                _lossText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(30, 4), new Vector2(-30, -4));
                _lossText.Outlined();
            }

            private float _shown = -1f;
            private Tween _slide;

            /// <summary>The split slides to a new chance (it jumps the first time).</summary>
            public void Set(float win01, string winText, string lossText)
            {
                win01 = Mathf.Clamp01(win01);
                _winText.text = winText;
                _lossText.text = lossText;
                _slide?.Kill();
                if (_shown < 0f)
                    Show(win01);
                else if (!Mathf.Approximately(_shown, win01))
                    _slide = DOTween.To(() => _shown, Show, win01, 0.35f).SetEase(Ease.OutCubic).SetLink(Rect.gameObject);
            }

            private void Show(float win01)
            {
                _shown = win01;
                _win.Place(Vector2.zero, new Vector2(win01, 1));
                _loss.Place(new Vector2(win01, 0), Vector2.one);
            }
        }
    }
}
