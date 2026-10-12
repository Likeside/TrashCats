using UnityEngine;

namespace TrashCats.Presentation.Animation
{
    /// <summary>
    /// Keeps a separately animated part (e.g. the tail) glued to its attachment point on the current frame of the body.
    /// Must be a child of the body so the body's scale (breathing) applies to it as well.
    /// </summary>
    public sealed class FlipbookAttachment : MonoBehaviour
    {
        [SerializeField] private SpriteFlipbook body;

        private SpriteRenderer _renderer;

        public void Bind(SpriteFlipbook bodyFlipbook) => body = bodyFlipbook;

        private void LateUpdate()
        {
            if (body == null) return;
            var clip = body.Current;
            var point = Vector2.zero;
            var attached = clip != null && clip.TryGetAttachPoint(body.FrameIndex, out point);
            // body clips without attach points have the part drawn in (e.g. a tail): hide the separate part
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null) _renderer.enabled = attached;
            if (attached)
                transform.localPosition = new Vector3(point.x, point.y, transform.localPosition.z);
        }
    }
}
