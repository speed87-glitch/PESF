using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.UI
{
    // Paints an InkStroke (or InkFrame) in from nothing after a delay, then removes itself.
    // Unscaled time, so it also plays while a fight is paused.
    [DisallowMultipleComponent]
    public sealed class InkPaint : MonoBehaviour
    {
        private InkStroke stroke;
        private InkFrame frame;
        private float delay, duration, elapsed;

        public static void Play(Graphic target, float delay = .1f, float duration = .32f)
        {
            if (target == null) return;
            var paint = target.GetComponent<InkPaint>();
            if (paint == null) paint = target.gameObject.AddComponent<InkPaint>();
            paint.stroke = target as InkStroke;
            paint.frame = target as InkFrame;
            paint.delay = delay;
            paint.duration = Mathf.Max(.01f, duration);
            paint.elapsed = 0f;
            if (InkTheme.ReducedMotion) { paint.Set(1f); Destroy(paint); return; }
            paint.Set(0f);
        }

        private void Set(float value)
        {
            if (stroke != null) stroke.Fill = value;
            if (frame != null) frame.Fill = value;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((elapsed - delay) / duration);
            Set(InkTheme.OutQuad(t));
            if (t >= 1f) Destroy(this);
        }
    }

    // A short scale "punch" about the element's own scale: it jumps to Amount and springs
    // back. Used when a value changes so the eye catches it.
    [DisallowMultipleComponent]
    public sealed class UiPunch : MonoBehaviour
    {
        private const float Duration = .28f;
        private Vector3 home;
        private float amount, elapsed;

        public static void Play(Transform target, float amount = 1.14f)
        {
            if (target == null || InkTheme.ReducedMotion) return;
            var punch = target.GetComponent<UiPunch>();
            if (punch == null)
            {
                punch = target.gameObject.AddComponent<UiPunch>();
                punch.home = target.localScale;
            }
            punch.amount = amount;
            punch.elapsed = 0f;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            // Decaying spring: full kick at the start, a small undershoot, then rest.
            float spring = Mathf.Cos(t * Mathf.PI * 1.5f) * (1f - t) * (1f - t);
            float scale = 1f + (amount - 1f) * spring;
            transform.localScale = new Vector3(home.x * scale, home.y * scale, home.z);
            if (t >= 1f) { transform.localScale = home; Destroy(this); }
        }

        private void OnDisable() { transform.localScale = home; }
    }

    // Fades parts of a row in or out while any of its controls has focus: a save's Rename and
    // Delete appear on the focused row only, and its play statistics step aside for them.
    // Hidden parts stay clickable, so hovering where they are reveals them.
    public sealed class FocusGroup : MonoBehaviour
    {
        private Selectable[] members;
        private CanvasGroup shown, hidden;
        private float focus = -1f;

        public static FocusGroup Attach(GameObject host, CanvasGroup shownOnFocus, CanvasGroup hiddenOnFocus, params Selectable[] members)
        {
            var group = host.AddComponent<FocusGroup>();
            group.members = members;
            group.shown = shownOnFocus;
            group.hidden = hiddenOnFocus;
            group.Step(0f, true);
            return group;
        }

        private bool Focused()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            var current = events != null ? events.currentSelectedGameObject : null;
            if (current == null) return false;
            foreach (var member in members)
                if (member != null && member.gameObject == current) return true;
            return false;
        }

        private void Update()
        {
            float target = Focused() ? 1f : 0f;
            if (focus == target) return;
            Step(InkTheme.ReducedMotion ? target : Mathf.MoveTowards(focus, target, Time.unscaledDeltaTime / .16f), false);
        }

        private void Step(float value, bool force)
        {
            if (!force && value == focus) return;
            focus = value;
            float t = InkTheme.Smooth(value);
            if (shown != null) shown.alpha = t;
            if (hidden != null) hidden.alpha = 1f - t;
        }
    }
}
