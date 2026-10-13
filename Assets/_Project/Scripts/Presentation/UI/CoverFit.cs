using UnityEngine;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// Sizes a rect to cover its parent while keeping <see cref="aspect"/> (width / height), like CSS "cover", so children
    /// anchored in its 0..1 space stay glued to the same spots of a background picture on any screen. The overflow is
    /// cropped according to <see cref="focus"/> (0.5 = centre; a low y keeps the bottom and crops the sky).
    /// </summary>
    [ExecuteAlways]
    public sealed class CoverFit : MonoBehaviour
    {
        public float aspect = 2f / 3f;
        public Vector2 focus = new Vector2(0.5f, 0.5f);

        private void OnEnable() => Apply();

        private void OnRectTransformDimensionsChange() => Apply();

        private void Update() => Apply();

        public void Apply()
        {
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null || aspect <= 0f)
                return;
            var area = parent.rect.size;
            if (area.x <= 0f || area.y <= 0f)
                return;
            var size = area.x / area.y > aspect ? new Vector2(area.x, area.x / aspect) : new Vector2(area.y * aspect, area.y);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (rt.sizeDelta != size)
                rt.sizeDelta = size;
            var offset = new Vector2((size.x - area.x) * (0.5f - focus.x), (size.y - area.y) * (0.5f - focus.y));
            if (rt.anchoredPosition != offset)
                rt.anchoredPosition = offset;
        }
    }
}
