using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.UI
{
    // An on/off mark drawn as a hanko seal: on is a red seal stamped over an ink ring, with
    // the title's small eclipsed sun in its centre; off is the empty ink ring. Turning it on
    // stamps the seal down (it lands from slightly larger with a small overshoot); turning it
    // off lifts it away. One procedural mesh, no sprites.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InkSeal : MaskableGraphic
    {
        private const int Steps = 40;
        private bool on;
        private float stamp, landing = 1f;

        public bool On => on;

        public static InkSeal Create(Transform parent, string name, float size, bool on)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(size, size);
            var seal = rect.gameObject.AddComponent<InkSeal>();
            seal.raycastTarget = false;
            seal.color = Color.white;
            seal.Set(on, false);
            return seal;
        }

        public void Set(bool value, bool animate)
        {
            on = value;
            if (!animate || InkTheme.ReducedMotion) { stamp = on ? 1f : 0f; landing = 1f; }
            else if (on) landing = 0f;
            SetVerticesDirty();
        }

        private void Update()
        {
            float target = on ? 1f : 0f;
            bool dirty = false;
            if (stamp != target) { stamp = Mathf.MoveTowards(stamp, target, Time.unscaledDeltaTime / (on ? .1f : .16f)); dirty = true; }
            if (landing < 1f) { landing = Mathf.MoveTowards(landing, 1f, Time.unscaledDeltaTime / .32f); dirty = true; }
            if (dirty) SetVerticesDirty();
        }

        private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < Steps; i++)
            {
                float a = i * Mathf.PI * 2f / Steps;
                // A slightly uneven edge, like a stone seal pressed into paper.
                float wobble = 1f + .025f * Mathf.Sin(a * 5f + 1.3f) + .015f * Mathf.Sin(a * 11f);
                mesh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * wobble, color, Vector2.zero);
            }
            for (int i = 0; i < Steps; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % Steps);
        }

        private static void Ring(VertexHelper mesh, Vector2 center, float outer, float width, Color color)
        {
            int start = mesh.currentVertCount;
            float inner = Mathf.Max(0f, outer - width);
            for (int i = 0; i < Steps; i++)
            {
                float a = i * Mathf.PI * 2f / Steps;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                // Brush pressure varies around the ring; it is thickest at the top left.
                float press = .8f + .2f * Mathf.Cos(a - 2.3f);
                mesh.AddVert(center + d * (outer - (outer - inner) * (1f - press)), color, Vector2.zero);
                mesh.AddVert(center + d * inner, color, Vector2.zero);
            }
            for (int i = 0; i < Steps; i++)
            {
                int a = start + i * 2, b = start + ((i + 1) % Steps) * 2;
                mesh.AddTriangle(a, a + 1, b + 1);
                mesh.AddTriangle(a, b + 1, b);
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            float radius = Mathf.Min(r.width, r.height) * .5f;
            Vector2 c = r.center;
            float alpha = color.a;
            // The empty ring stays faintly under the seal.
            Ring(mesh, c, radius * .92f, Mathf.Max(2f, radius * .16f), new Color(InkTheme.Ink.r, InkTheme.Ink.g, InkTheme.Ink.b, alpha * Mathf.Lerp(.7f, .35f, stamp)));
            if (stamp <= 0f) return;
            float land = InkTheme.OutBack(landing, 2.2f);
            float scale = Mathf.Lerp(1.45f, 1f, land);
            float sealAlpha = alpha * stamp * Mathf.Lerp(.25f, 1f, Mathf.Clamp01(landing * 3f));
            float sealRadius = radius * scale;
            Disc(mesh, c, sealRadius, InkTheme.Alpha(InkTheme.Red, sealAlpha));
            Ring(mesh, c, sealRadius, Mathf.Max(1.5f, radius * .1f), InkTheme.Alpha(InkTheme.RedDeep, sealAlpha));
            // The eclipsed sun: a paper disc with the red moon over most of it.
            float sun = sealRadius * .42f;
            Disc(mesh, c, sun, InkTheme.Alpha(InkTheme.Paper, sealAlpha));
            Disc(mesh, c + new Vector2(-sun * .42f, sun * .14f), sun * 1.02f, InkTheme.Alpha(InkTheme.Red, sealAlpha));
        }
    }
}
