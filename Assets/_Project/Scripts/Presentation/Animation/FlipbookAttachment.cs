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

        private void LateUpdate()
        {
            var clip = body.Current;
            if (clip != null && clip.TryGetAttachPoint(body.FrameIndex, out var point))
                transform.localPosition = new Vector3(point.x, point.y, transform.localPosition.z);
        }
    }
}
