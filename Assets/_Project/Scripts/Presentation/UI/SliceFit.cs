using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// Scales a 9-sliced image's borders with its height, so a button drawn 250 px tall keeps its proportions
    /// (outline, tape) at 90 px; <see cref="heightShare"/> is how much of the rect the sprite's full height covers.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class SliceFit : MonoBehaviour
    {
        public float heightShare = 1f;
        public float maxBorderShare = 0.45f;

        private Image _image;

        private void OnEnable() => Apply();

        private void OnRectTransformDimensionsChange() => Apply();

        private void Apply()
        {
            if (_image == null)
                _image = GetComponent<Image>();
            var sprite = _image.sprite;
            var size = ((RectTransform)transform).rect.size;
            if (sprite == null || size.y <= 1f)
                return;
            var multiplier = sprite.rect.height / (size.y * heightShare);
            // keep the side borders from eating a narrow rect
            var sides = (sprite.border.x + sprite.border.z) / multiplier;
            if (sides > size.x * maxBorderShare * 2f)
                multiplier = (sprite.border.x + sprite.border.z) / (size.x * maxBorderShare * 2f);
            _image.pixelsPerUnitMultiplier = Mathf.Max(0.3f, multiplier);
        }
    }
}
