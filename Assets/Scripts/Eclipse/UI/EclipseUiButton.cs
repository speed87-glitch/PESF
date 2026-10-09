using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Eclipse.UI
{
    // Animated focus for Eclipse menu controls. Hovering focuses the control, so mouse,
    // keyboard and gamepad share one highlight; leaving with the pointer releases a focus
    // the pointer gave. Colours ease instead of snapping, the label
    // slides slightly and a press gives a short squash. Runs on unscaled time.
    // A brush-stroke body gets a focus wipe: a stroke of the highlight colour paints over
    // it left to right on focus and lifts off again when focus leaves. Accents (extra strokes
    // or frames) paint in with focus the same way, and tints ease other graphics with it.
    [DisallowMultipleComponent]
    public sealed class EclipseUiButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float FocusSeconds = .14f;
        private Selectable target;
        private Graphic body;
        private Text label;
        private Color normal, highlight, labelNormal, labelHighlight;
        private Vector2 labelHome;
        private float slide, grow, squash;
        private float focus, press;
        private bool selected, pressed, hovered;
        private InkStroke bodyStroke, wipe;
        private InkFrame frame;
        private readonly System.Collections.Generic.List<Graphic> accents = new System.Collections.Generic.List<Graphic>();
        private readonly System.Collections.Generic.List<(Graphic graphic, Color normal, Color focused)> tints =
            new System.Collections.Generic.List<(Graphic, Color, Color)>();

        public float Focus => focus;

        public static EclipseUiButton Attach(Selectable target, Graphic body, Text label, Color normal, Color highlight,
            Color labelNormal, Color labelHighlight, float slide = 8f, float grow = .03f, float squash = .05f)
        {
            var fx = Eclipse.UI.ComponentUtility.Ensure<EclipseUiButton>(target.gameObject);
            fx.target = target; fx.body = body; fx.label = label;
            fx.slide = slide; fx.grow = grow; fx.squash = squash;
            target.transition = Selectable.Transition.None;
            // A Button tints its target the moment it is enabled; that tint would otherwise
            // multiply every colour set here (it made red plates render near-black).
            if (target.targetGraphic != null) target.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
            // Grow and squash about the centre; keep the control where it was authored.
            var rect = (RectTransform)target.transform;
            var shift = new Vector2(.5f, .5f) - rect.pivot;
            if (shift != Vector2.zero)
            {
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition += Vector2.Scale(shift, rect.rect.size);
            }
            if (label != null) fx.labelHome = label.rectTransform.anchoredPosition;
            fx.bodyStroke = body as InkStroke;
            if (fx.bodyStroke != null && fx.wipe == null) fx.wipe = CreateWipe(fx.bodyStroke, target.gameObject);
            // A paper card is marked by a red brush drawn around it while it has focus.
            if (body is PaperPanel && fx.frame == null) fx.AddAccent(fx.frame = InkFrame.Around(body.rectTransform, InkTheme.RedBright, 7f, 5f));
            fx.SetColors(normal, highlight, labelNormal, labelHighlight);
            return fx;
        }

        // The focus wipe sits directly above the body stroke (below the label) with the same shape.
        private static InkStroke CreateWipe(InkStroke body, GameObject owner)
        {
            var rect = new GameObject("Focus wipe", typeof(RectTransform)).GetComponent<RectTransform>();
            var source = body.rectTransform;
            if (body.gameObject == owner)
            {
                rect.SetParent(source, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.SetSiblingIndex(0);
            }
            else
            {
                rect.SetParent(source.parent, false);
                rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax; rect.pivot = source.pivot;
                rect.anchoredPosition = source.anchoredPosition; rect.sizeDelta = source.sizeDelta;
                rect.SetSiblingIndex(source.GetSiblingIndex() + 1);
            }
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var wipe = rect.gameObject.AddComponent<InkStroke>();
            wipe.raycastTarget = false;
            wipe.Seed = body.Seed;
            wipe.Taper = body.Taper;
            wipe.Fill = 0f;
            return wipe;
        }

        // A stroke or frame that paints in while the control has focus.
        public EclipseUiButton AddAccent(Graphic accent)
        {
            if (accent != null && !accents.Contains(accent)) { accents.Add(accent); Apply(); }
            return this;
        }

        // Another graphic (a row label, say) that eases between two colours with focus.
        public EclipseUiButton AddTint(Graphic graphic, Color normal, Color focused)
        {
            if (graphic != null) { tints.Add((graphic, normal, focused)); Apply(); }
            return this;
        }

        public void SetColors(Color normal, Color highlight, Color labelNormal, Color labelHighlight)
        {
            this.normal = normal; this.highlight = highlight;
            this.labelNormal = labelNormal; this.labelHighlight = labelHighlight;
            Apply();
        }

        public void OnSelect(BaseEventData data) { selected = true; EclipseUiAudio.Play(UiSound.Focus); }
        public void OnDeselect(BaseEventData data) { selected = false; pressed = false; hovered = false; }

        public void OnPointerEnter(PointerEventData data)
        {
            if (target == null || !target.IsInteractable() || EventSystem.current == null) return;
            var current = EventSystem.current.currentSelectedGameObject;
            var field = current == null ? null : current.GetComponent<InputField>();
            if (field != null && field.isFocused) return;
            if (EventSystem.current.currentSelectedGameObject != gameObject) { target.Select(); hovered = true; }
        }

        // Only a hover focus is dropped; keyboard or gamepad focus stays where it was put.
        public void OnPointerExit(PointerEventData data)
        {
            if (!hovered || EventSystem.current == null) return;
            hovered = false;
            if (EventSystem.current.currentSelectedGameObject == gameObject) EventSystem.current.SetSelectedGameObject(null);
        }

        public void OnPointerDown(PointerEventData data) { if (target != null && target.IsInteractable()) pressed = true; }
        public void OnPointerUp(PointerEventData data) { pressed = false; }

        // Call after moving the label yourself, so the focus slide starts from its new place.
        public void Rehome() { if (label != null) labelHome = label.rectTransform.anchoredPosition; }

        // Code-triggered presses (keyboard Enter) get the same squash as a click.
        public void Punch() { press = 1f; }

        private void OnDisable() { selected = pressed = false; focus = press = 0f; Apply(); }

        private void Update()
        {
            if (target == null) return;
            bool live = target.IsInteractable() && (selected ||
                (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject));
            focus = Mathf.MoveTowards(focus, live ? 1f : 0f, Time.unscaledDeltaTime / FocusSeconds);
            press = pressed ? Mathf.MoveTowards(press, 1f, Time.unscaledDeltaTime / .06f)
                : Mathf.MoveTowards(press, 0f, Time.unscaledDeltaTime / .22f);
            Apply();
        }

        private void Apply()
        {
            if (target == null) return;
            float t = focus * focus * (3f - 2f * focus);
            bool reduced = InkTheme.ReducedMotion;
            // The wipe sweeps with an ease-out so the brush lands quickly and settles.
            float sweep = reduced ? (focus > 0f ? 1f : 0f) : 1f - (1f - focus) * (1f - focus);
            bool enabled = target.IsInteractable();
            if (body != null)
            {
                var color = wipe != null ? normal : Color.Lerp(normal, highlight, t);
                if (!enabled) color.a *= .42f;
                body.color = color;
            }
            if (wipe != null)
            {
                wipe.Seed = bodyStroke.Seed;
                wipe.Taper = bodyStroke.Taper;
                wipe.color = highlight;
                wipe.Fill = enabled ? sweep : 0f;
            }
            for (int i = 0; i < accents.Count; i++)
            {
                var accent = accents[i];
                if (accent is InkStroke stroke) stroke.Fill = sweep;
                else if (accent is InkFrame frame) frame.Fill = sweep;
                else if (accent != null) { var c = accent.color; c.a = t; accent.color = c; }
            }
            for (int i = 0; i < tints.Count; i++)
                if (tints[i].graphic != null) tints[i].graphic.color = Color.Lerp(tints[i].normal, tints[i].focused, t);
            if (label != null)
            {
                label.color = Color.Lerp(labelNormal, labelHighlight, t);
                label.rectTransform.anchoredPosition = labelHome + new Vector2(reduced ? 0f : slide * t, 0f);
            }
            float scale = 1f + grow * t - (reduced ? 0f : squash * press);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
