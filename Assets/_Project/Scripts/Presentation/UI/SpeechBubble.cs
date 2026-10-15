using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// Comic speech bubble with an optional icon before the text. Its pivot is the tip of the art's tail, so placing the
    /// bubble at a point makes the tail come out of it (a cat's head); flipped, the bubble opens to the left.
    /// </summary>
    public sealed class SpeechBubble
    {
        /// <summary>Tip of the tail in bubble.png (bottom left), 0..1 of the sprite.</summary>
        private static readonly Vector2 TailTip = new Vector2(0.09f, 0.02f);

        private readonly RectTransform _content;
        private readonly Image _icon;
        private readonly TMP_Text _label;

        public RectTransform Root { get; }
        public bool Flipped { get; private set; }

        public SpeechBubble(Transform parent, string name, Vector2 size, int fontSize)
        {
            var back = Ui.Panel(parent, name, Ui.Skin?.bubble != null ? Color.white : new Color(0, 0, 0, 0.55f));
            back.sprite = Ui.Skin?.bubble;
            Root = back.rectTransform;
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
            Root.pivot = TailTip;
            Root.sizeDelta = size;

            // the body of the bubble is above the tail: text and icon keep clear of the outline
            _content = Info.Row(back.transform, "Content", 6f, TextAnchor.MiddleCenter);
            _content.Place(Vector2.zero, Vector2.one, new Vector2(18, size.y * 0.26f), new Vector2(-18, -size.y * 0.1f));
            _icon = Ui.Panel(_content, "Icon", Color.white);
            _icon.preserveAspect = true;
            Info.Size(_icon, fontSize * 1.6f, fontSize * 1.6f);
            _label = Info.Text(_content, "", fontSize, Ui.Skin?.bubble != null ? Palette.Ink : Palette.Paper);
        }

        public void Set(string text, string icon)
        {
            _label.text = text;
            var sprite = icon != null ? Ui.Skin?.Icon(icon) : null;
            _icon.sprite = sprite;
            _icon.gameObject.SetActive(sprite != null);
        }

        /// <summary>Mirrors the bubble so it opens to the left of its tail (the text stays readable).</summary>
        public void SetFlipped(bool flipped)
        {
            if (Flipped == flipped)
                return;
            Flipped = flipped;
            Root.pivot = flipped ? new Vector2(1f - TailTip.x, TailTip.y) : TailTip;
            Root.localScale = new Vector3(flipped ? -Mathf.Abs(Root.localScale.x) : Mathf.Abs(Root.localScale.x), Root.localScale.y, 1f);
            _content.localScale = new Vector3(flipped ? -1f : 1f, 1f, 1f);
        }
    }
}
