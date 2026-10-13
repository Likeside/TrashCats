using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// A picture cropped to a circle on a coloured backdrop, framed by a ring (map vignettes, the fighter queue in
    /// battle; Docs/ArtDirection/Concepts/round2/map_S4.png, combat_S4.png). Without a skin it is a square.
    /// </summary>
    public sealed class RoundPicture
    {
        public RectTransform Root { get; }
        public Image Backdrop { get; }
        public Image Picture { get; }
        public Image Ring { get; }

        private readonly AspectRatioFitter _fit;

        public RoundPicture(Transform parent, string name, Color backdrop, float focusY = 0.7f)
        {
            Root = Ui.Rect(parent, name);
            var skin = Ui.Skin;

            Backdrop = Ui.Panel(Root, "Backdrop", backdrop);
            Backdrop.rectTransform.anchorMin = new Vector2(0.06f, 0.06f);
            Backdrop.rectTransform.anchorMax = new Vector2(0.94f, 0.94f);
            Backdrop.rectTransform.offsetMin = Backdrop.rectTransform.offsetMax = Vector2.zero;
            if (skin?.circle != null)
            {
                Backdrop.sprite = skin.circle;
                Backdrop.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            }
            else
                Backdrop.gameObject.AddComponent<RectMask2D>();

            Picture = Ui.Panel(Backdrop.transform, "Picture", Color.white);
            _fit = Picture.gameObject.AddComponent<AspectRatioFitter>();
            _fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            Picture.rectTransform.pivot = new Vector2(0.5f, focusY);
            Picture.enabled = false;

            Ring = Ui.Panel(Root, "Ring", skin?.ring != null ? Color.white : new Color(0, 0, 0, 0));
            Ring.sprite = skin?.ring;
            Ring.preserveAspect = true;
            Ring.rectTransform.Fill();
        }

        public void SetPicture(Sprite sprite)
        {
            Picture.enabled = sprite != null;
            Picture.sprite = sprite;
            if (sprite != null)
                _fit.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        /// <summary>Golden ring for the highlighted item (available territory, fighter in the ring).</summary>
        public void SetGold(bool gold)
        {
            var skin = Ui.Skin;
            if (skin?.ring == null)
                return;
            Ring.sprite = gold && skin.ringGold != null ? skin.ringGold : skin.ring;
        }
    }
}
