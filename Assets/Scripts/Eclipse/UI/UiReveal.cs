using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.UI
{
    // One-shot entrance: fades an element in while it eases from an offset to its
    // authored position, then removes itself. Unscaled time, so it also plays while paused.
    [DisallowMultipleComponent]
    public sealed class UiReveal : MonoBehaviour
    {
        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 home, from;
        private float delay, duration, elapsed;
        private float fromScale;
        private bool ownsGroup;
        private bool layoutDriven;
        private bool finished;
        private Vector3 homeScale;

        public static void Play(RectTransform target, float delay, float duration, Vector2 offset, float fromScale = 1f)
        {
            if (target == null) return;
            // Reduced motion: a short fade in place, no travel or scale.
            if (InkTheme.ReducedMotion) { offset = Vector2.zero; fromScale = 1f; delay *= .5f; duration = Mathf.Min(duration, .16f); }
            // Restarting reuses the running reveal: Destroy is deferred to the end of the frame,
            // so adding a second one now would hit DisallowMultipleComponent and return null.
            var reveal = target.GetComponent<UiReveal>();
            if (reveal != null && reveal.finished) { DestroyImmediate(reveal); reveal = null; }
            if (reveal != null) reveal.Restore();
            else reveal = target.gameObject.AddComponent<UiReveal>();
            reveal.elapsed = 0f;
            reveal.rect = target;
            reveal.home = target.anchoredPosition;
            reveal.homeScale = target.localScale;
            var layout = target.parent != null ? target.parent.GetComponent<LayoutGroup>() : null;
            var element = target.GetComponent<LayoutElement>();
            reveal.layoutDriven = layout != null && layout.isActiveAndEnabled && (element == null || !element.ignoreLayout);
            reveal.from = offset;
            reveal.delay = delay;
            reveal.duration = Mathf.Max(.01f, duration);
            reveal.fromScale = fromScale;
            if (reveal.group == null)
            {
                reveal.group = target.GetComponent<CanvasGroup>();
                reveal.ownsGroup = reveal.group == null;
                if (reveal.ownsGroup) reveal.group = target.gameObject.AddComponent<CanvasGroup>();
            }
            reveal.Step(0f);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((elapsed - delay) / duration);
            Step(t);
            if (t >= 1f) Finish();
        }

        private void Step(float t)
        {
            // Ease-out back: settles with a slight overshoot, like paper landing.
            const float c1 = 1.25f, c3 = c1 + 1f;
            float u = t - 1f;
            float eased = t <= 0f ? 0f : 1f + c3 * u * u * u + c1 * u * u;
            // Layout groups place their children after Update. Never restore a position
            // captured before that first layout pass (often the prefab's top-left origin).
            if (!layoutDriven) rect.anchoredPosition = home + from * (1f - eased);
            float scale = Mathf.LerpUnclamped(fromScale, 1f, eased);
            if (!Mathf.Approximately(fromScale, 1f)) rect.localScale = Vector3.Scale(homeScale, new Vector3(scale, scale, 1f));
            group.alpha = Mathf.Clamp01(t * 1.6f);
        }

        // Puts the element back where and how it was authored, keeping this reveal alive.
        private void Restore()
        {
            if (rect != null)
            {
                if (!layoutDriven) rect.anchoredPosition = home;
                if (!Mathf.Approximately(fromScale, 1f)) rect.localScale = homeScale;
            }
            if (group != null) group.alpha = 1f;
        }

        private void Finish()
        {
            Restore();
            if (group != null && ownsGroup) DestroyImmediate(group);
            group = null;
            finished = true;
            Destroy(this);
        }
    }
}
