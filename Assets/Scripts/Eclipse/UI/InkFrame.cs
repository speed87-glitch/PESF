using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.UI
{
    // A brush-drawn frame around a card: four strokes that overshoot the corners a little,
    // drawn clockwise from the top-left as Fill goes 0..1 (a brush circling the card).
    // Procedural geometry only; it sits outside the parent rect by Outset.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InkFrame : MaskableGraphic
    {
        private const int SegmentsPerSide = 18;
        [SerializeField] private float fill = 1f;
        [SerializeField] private float thickness = 5f;
        [SerializeField] private int seed = 3;

        public float Fill
        {
            get { return fill; }
            set { value = Mathf.Clamp01(value); if (!Mathf.Approximately(value, fill)) { fill = value; SetVerticesDirty(); } }
        }

        public float Thickness { get { return thickness; } set { thickness = value; SetVerticesDirty(); } }
        public int Seed { get { return seed; } set { seed = value; SetVerticesDirty(); } }

        // Stretches a new frame over <paramref name="target"/>, <paramref name="outset"/> outside its edges.
        public static InkFrame Around(RectTransform target, Color color, float outset = 7f, float thickness = 5f)
        {
            var rect = new GameObject("Focus frame", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(target, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(-outset, -outset); rect.offsetMax = new Vector2(outset, outset);
            var frame = rect.gameObject.AddComponent<InkFrame>();
            frame.color = color;
            frame.thickness = thickness;
            frame.raycastTarget = false;
            frame.seed = target.name.GetHashCode() & 0xff;
            frame.fill = 0f;
            // Frames are pure decoration: never let a layout group place them.
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return frame;
        }

        private float Noise(float x, float salt)
        {
            float s = seed * 3.17f + salt * 11.3f;
            return Mathf.Sin(x * 9.1f + s) * .5f + Mathf.Sin(x * 23.3f + s * 1.9f) * .3f + Mathf.Sin(x * 51.7f + s * .7f) * .2f;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (fill <= 0f) return;
            Rect r = rectTransform.rect;
            float over = thickness * 1.6f;
            // Clockwise: top (left to right), right (down), bottom (right to left), left (up).
            var corners = new[] { new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMin) };
            for (int side = 0; side < 4; side++)
            {
                float local = Mathf.Clamp01(fill * 4f - side);
                if (local <= 0f) break;
                Vector2 a = corners[side], b = corners[(side + 1) % 4];
                Vector2 along = (b - a).normalized;
                Vector2 normal = new Vector2(-along.y, along.x);
                a -= along * over * .5f; b += along * over * .5f;
                int start = mesh.currentVertCount;
                int count = Mathf.Max(1, Mathf.CeilToInt(SegmentsPerSide * local));
                for (int i = 0; i <= count; i++)
                {
                    float u = Mathf.Min(local, i / (float)SegmentsPerSide);
                    // Pressed at the start of each stroke, lifting off toward its end.
                    float press = Mathf.Lerp(1.15f, .55f, u) * (Mathf.Sqrt(Mathf.Clamp01(u / .06f)) * .7f + .3f);
                    float w = thickness * press * (.85f + .15f * Noise(u + side, 1f)) * .5f;
                    Vector2 p = Vector2.Lerp(a, b, u) + normal * Noise(u + side, 2f) * 1.2f;
                    var c = color;
                    c.a *= Mathf.Lerp(1f, .7f, u);
                    mesh.AddVert(p + normal * w, c, Vector2.zero);
                    mesh.AddVert(p - normal * w, c, Vector2.zero);
                    if (i > 0)
                    {
                        int v = start + i * 2;
                        mesh.AddTriangle(v - 2, v - 1, v + 1);
                        mesh.AddTriangle(v - 2, v + 1, v);
                    }
                }
            }
        }
    }
}
