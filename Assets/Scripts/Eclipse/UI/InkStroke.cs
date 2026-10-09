using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.UI
{
    // A procedural sumi brush stroke: ragged top and bottom edges, a blunt loaded start
    // and a dry, tapering tail. Fill (0..1) draws it left to right for a "painted" reveal.
    // Code-owned geometry; it never touches a recovered sprite mesh.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InkStroke : MaskableGraphic
    {
        private const int Segments = 36;
        [SerializeField] private float fill = 1f;
        [SerializeField] private int seed = 7;
        [SerializeField] private float taper = 1f;

        public float Fill
        {
            get { return fill; }
            set { value = Mathf.Clamp01(value); if (!Mathf.Approximately(value, fill)) { fill = value; SetVerticesDirty(); } }
        }

        public int Seed { get { return seed; } set { if (seed != value) { seed = value; SetVerticesDirty(); } } }

        // How much the tail thins and dries out: 1 is the classic stroke, 0 keeps a full bar
        // (for wide rows, where a long dry tail reads as the row being cut off).
        public float Taper
        {
            get { return taper; }
            set { value = Mathf.Clamp01(value); if (!Mathf.Approximately(value, taper)) { taper = value; SetVerticesDirty(); } }
        }

        private float Noise(float x, float salt)
        {
            float s = seed * 12.9898f + salt * 78.233f;
            return Mathf.Sin(x * 17.1f + s) * .45f + Mathf.Sin(x * 43.7f + s * 1.7f) * .3f + Mathf.Sin(x * 91.3f + s * .3f) * .25f;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (fill <= 0f) return;
            Rect r = rectTransform.rect;
            float half = r.height * .5f, mid = r.center.y;
            // A short-tailed stroke still lifts off the paper: its last stretch (about the
            // stroke's own height) rounds off instead of ending in a square cut. That stretch is
            // sampled finely so the curve reads on wide rows; the classic tail (Taper 1) keeps
            // its dry, thin end as before.
            float tipU = r.width > 1f ? Mathf.Clamp01(Mathf.Min(r.height * 1.1f, r.width * .12f) / r.width) : 0f;
            const int TipSteps = 10;
            int verts = 0;
            for (int i = 0; i <= Segments + TipSteps; i++)
            {
                float u = i <= Segments ? i / (float)Segments : 1f - tipU + tipU * (i - Segments) / TipSteps;
                if (i <= Segments && u > 1f - tipU) continue; // replaced by the finer tip samples
                bool last = u >= fill;
                u = Mathf.Min(u, fill);
                float x = r.xMin + r.width * u;
                // Loaded, rounded head; long tail that thins and dries out.
                float head = Mathf.Sqrt(Mathf.Clamp01(u / .05f));
                float tail = 1f - Mathf.Pow(Mathf.Clamp01((u - .72f) / .28f), 1.6f) * .78f * taper;
                float k = tipU > 0f ? Mathf.Clamp01((1f - u) / tipU) : 1f;
                float lift = Mathf.Sqrt(1f - (1f - k) * (1f - k)); // quarter circle: 1 inside, 0 at the very end
                float end = Mathf.Lerp(.06f + .94f * lift, 1f, taper);
                float thickness = half * head * tail * end;
                float top = mid + thickness * (.9f + .1f * Noise(u, 1f)) + Noise(u, 3f) * 1.6f * end;
                float bottom = mid - thickness * (.9f + .1f * Noise(u, 2f)) + Noise(u, 4f) * 1.6f * end;
                var c = color;
                c.a *= Mathf.Lerp(1f, .55f + .25f * Noise(u, 5f), Mathf.Clamp01((u - .6f) / .4f) * taper);
                mesh.AddVert(new Vector3(x, top), c, Vector2.zero);
                mesh.AddVert(new Vector3(x, bottom), c, Vector2.zero);
                if (verts > 0)
                {
                    int v = verts * 2;
                    mesh.AddTriangle(v - 2, v - 1, v + 1);
                    mesh.AddTriangle(v - 2, v + 1, v);
                }
                verts++;
                if (last) break;
            }
        }
    }
}
