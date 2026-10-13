using UnityEngine;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Keeps the wheel's spin ellipse matched to the wheel picture: a square of the rim's width, squashed vertically
    /// to the rim's height, so content rotating inside it follows the rim's ellipse.
    /// </summary>
    public sealed class SpinFit : MonoBehaviour
    {
        private RectTransform _picture;
        private Sprite _sprite;
        private float _width;
        private float _height;

        public void Init(RectTransform picture, Sprite sprite, float width, float height)
        {
            _picture = picture;
            _sprite = sprite;
            _width = width;
            _height = height;
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (_picture == null) return;
            var r = _picture.rect;
            var scale = Mathf.Min(r.width / _sprite.rect.width, r.height / _sprite.rect.height);
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(_width * scale, _width * scale);
            rt.localScale = new Vector3(1f, _height / _width, 1f);
        }
    }
}
