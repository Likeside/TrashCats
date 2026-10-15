using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Animation
{
    /// <summary>
    /// A skipping rope drawn as a UI mesh between two handle points (0..1 of the rect, y up): an inked brown cubic curve
    /// whose middle swings round the cat with the phase - over the head at 0, in front going down to 0.5 (under the
    /// feet), behind going up to 1. It is smooth at any frame rate; the cat's frames only bring the hands.
    /// </summary>
    public sealed class RopeGraphic : MaskableGraphic
    {
        private const int Segments = 28;

        public Vector2 Left;
        public Vector2 Right;
        /// <summary>Phase of the turn, 0..1.</summary>
        public float Phase;
        /// <summary>Highest and lowest point of the rope's middle (0..1 of the rect, y up).</summary>
        public float Top = 1.02f;
        public float Bottom = 0.02f;
        public float Width = 5f;
        public Color Ink = new Color(0.11f, 0.08f, 0.06f);
        public Color Rope = new Color(0.62f, 0.4f, 0.2f);

        /// <summary>True while the rope passes behind the cat (the caller draws it under the body).</summary>
        public bool Behind => IsBehind(Phase);

        public void Set(Vector2 left, Vector2 right, float phase)
        {
            Left = left;
            Right = right;
            Phase = phase;
            SetVerticesDirty();
        }

        /// <summary>Points of the rope from the left handle to the right one, in the same 0..1 space as the handles.</summary>
        public static Vector2[] Curve(Vector2 a, Vector2 b, float phase, float top, float bottom, int segments = Segments)
        {
            var theta = Mathf.Repeat(phase, 1f) * 2f * Mathf.PI;
            var middleY = Mathf.Lerp(bottom, top, 0.5f + 0.5f * Mathf.Cos(theta));
            // bows outwards most when it is far above or below the hands (the loop seen edge-on is narrow at waist height)
            var bulge = (b.x - a.x) * 0.22f * Mathf.Abs(Mathf.Cos(theta));
            var p1 = new Vector2(a.x - bulge, a.y + (middleY - a.y) * 1.333f);
            var p2 = new Vector2(b.x + bulge, b.y + (middleY - b.y) * 1.333f);
            var points = new Vector2[segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var u = 1f - t;
                points[i] = u * u * u * a + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * b;
            }
            return points;
        }

        public static bool IsBehind(float phase) => Mathf.Repeat(phase, 1f) > 0.5f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            var points = Curve(Left, Right, Phase, Top, Bottom);
            for (var i = 0; i < points.Length; i++)
                points[i] = new Vector2(r.xMin + points[i].x * r.width, r.yMin + points[i].y * r.height);
            Strip(vh, points, Width * 1.9f, Ink);
            Strip(vh, points, Width, Rope);
        }

        private static void Strip(VertexHelper vh, Vector2[] points, float width, Color color)
        {
            var start = vh.currentVertCount;
            for (var i = 0; i < points.Length; i++)
            {
                var dir = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                var normal = new Vector2(-dir.y, dir.x) * (width * 0.5f);
                vh.AddVert(points[i] + normal, color, Vector2.zero);
                vh.AddVert(points[i] - normal, color, Vector2.zero);
            }
            for (var i = 0; i < points.Length - 1; i++)
            {
                var k = start + i * 2;
                vh.AddTriangle(k, k + 1, k + 3);
                vh.AddTriangle(k, k + 3, k + 2);
            }
        }
    }
}
