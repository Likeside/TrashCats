using DG.Tweening;
using UnityEngine;

namespace TrashCats.Presentation.UI
{
    /// <summary>
    /// How sheets and modals come and go (DOTween). A bottom sheet (full width, anchored to the bottom: the activity
    /// sheet, the cat details) slides up from below the screen; a modal in the middle pops in from a little smaller. The
    /// dimmed backdrop fades. Closing plays it back quickly and only then deactivates the root; it never takes taps.
    /// </summary>
    public static class OverlayAnimator
    {
        private const float InTime = 0.32f;
        private const float OutTime = 0.18f;

        /// <summary>True for a full-width panel anchored to the bottom edge (a bottom sheet).</summary>
        public static bool IsBottomSheet(GameObject root) =>
            Panel(root) is RectTransform panel && panel.anchorMin.y <= 0.001f && panel.anchorMin.x <= 0.001f && panel.anchorMax.x >= 0.999f
            && panel.anchorMax.y < 0.999f;

        public static void Show(GameObject root)
        {
            DOTween.Kill(root); // a close still playing must not deactivate the root it reopens
            var group = Group(root);
            group.DOKill();
            group.blocksRaycasts = true;
            group.alpha = 0f;
            group.DOFade(1f, InTime * 0.6f).SetUpdate(true).SetLink(root);

            if (!(Panel(root) is RectTransform panel))
                return;
            panel.DOKill();
            if (IsBottomSheet(root))
            {
                var rest = Rest(panel);
                var height = panel.rect.height > 1f ? panel.rect.height : 1000f;
                panel.anchoredPosition = new Vector2(rest.x, rest.y - height);
                panel.DOAnchorPosY(rest.y, InTime).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(root);
            }
            else
            {
                panel.localScale = Vector3.one * 0.86f;
                panel.DOScale(1f, InTime).SetEase(Ease.OutBack).SetUpdate(true).SetLink(root);
            }
        }

        public static void Hide(GameObject root)
        {
            var group = Group(root);
            group.DOKill();
            group.blocksRaycasts = false;
            var panel = Panel(root);
            panel?.DOKill();
            DOTween.Kill(root);
            var sequence = DOTween.Sequence().SetId(root).SetUpdate(true).SetLink(root);
            sequence.Join(group.DOFade(0f, OutTime));
            if (panel != null)
            {
                if (IsBottomSheet(root))
                    sequence.Join(panel.DOAnchorPosY(Rest(panel).y - panel.rect.height, OutTime).SetEase(Ease.InCubic));
                else
                    sequence.Join(panel.DOScale(0.9f, OutTime).SetEase(Ease.InQuad));
            }
            sequence.OnComplete(() =>
            {
                root.SetActive(false);
                if (panel != null)
                {
                    panel.localScale = Vector3.one;
                    panel.anchoredPosition = Rest(panel);
                }
                group.alpha = 1f;
                group.blocksRaycasts = true;
            });
        }

        /// <summary>Where the panel sits when open (remembered the first time, before any tween moves it).</summary>
        private static Vector2 Rest(RectTransform panel)
        {
            var mark = panel.GetComponent<RestPosition>();
            if (mark == null)
            {
                mark = panel.gameObject.AddComponent<RestPosition>();
                mark.Position = panel.anchoredPosition;
            }
            return mark.Position;
        }

        private sealed class RestPosition : MonoBehaviour
        {
            public Vector2 Position;
        }

        // Unity objects: "??" does not see a missing component
        private static CanvasGroup Group(GameObject root) =>
            root.TryGetComponent<CanvasGroup>(out var group) ? group : root.AddComponent<CanvasGroup>();

        /// <summary>The window itself: the root's "Sheet" or "Card" child.</summary>
        private static RectTransform Panel(GameObject root)
        {
            foreach (Transform child in root.transform)
                if (child.name == "Sheet" || child.name == "Card")
                    return (RectTransform)child;
            return null;
        }
    }
}
