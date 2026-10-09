using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Eclipse.UI
{
    // A text field written on the page: a faint wash over a brush-stroke rule. While it has
    // focus the wash warms and a red stroke paints along the rule. Works on paper cards and on
    // dark glass. The InputField's own colour tint is disabled; this owns the look.
    [DisallowMultipleComponent]
    public sealed class InkField : MonoBehaviour
    {
        private InputField field;
        private Image wash;
        private InkStroke wipe;
        private Color washNormal, washFocused;
        private float focus;

        public static InputField Build(RectTransform box, Font font, int fontSize, bool onDark, string placeholder = null)
        {
            var ink = InkTheme.Ink;
            var paper = InkTheme.Paper;
            var wash = box.gameObject.AddComponent<Image>();
            wash.raycastTarget = true;
            var rule = Stroke(box, "Rule", onDark ? InkTheme.Alpha(paper, .4f) : InkTheme.Alpha(ink, .55f), box.name.GetHashCode() & 0xffff);
            var wipe = Stroke(box, "Focus rule", InkTheme.RedBright, box.name.GetHashCode() & 0xffff);
            wipe.Fill = 0f;
            var text = Label(box, "", font, fontSize, onDark ? paper : ink);
            var hint = Label(box, placeholder ?? "", font, fontSize - 2, InkTheme.Alpha(onDark ? paper : ink, .42f));
            hint.fontStyle = FontStyle.Italic;
            var field = box.gameObject.AddComponent<InputField>();
            field.textComponent = text; field.placeholder = hint; field.targetGraphic = wash;
            field.transition = Selectable.Transition.None;
            field.customCaretColor = true; field.caretColor = InkTheme.RedBright; field.caretWidth = 2;
            field.selectionColor = InkTheme.Alpha(InkTheme.Red, .3f);
            field.lineType = InputField.LineType.SingleLine;
            var look = box.gameObject.AddComponent<InkField>();
            look.field = field; look.wash = wash; look.wipe = wipe;
            look.washNormal = onDark ? InkTheme.Alpha(paper, .08f) : InkTheme.Alpha(ink, .06f);
            look.washFocused = onDark ? InkTheme.Alpha(paper, .16f) : InkTheme.Alpha(InkTheme.PaperWarm, .95f);
            wash.color = look.washNormal;
            return field;
        }

        private static InkStroke Stroke(RectTransform box, string name, Color color, int seed)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(box, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, -3); rect.sizeDelta = new Vector2(0, 6);
            var stroke = rect.gameObject.AddComponent<InkStroke>();
            stroke.color = color; stroke.Seed = seed; stroke.raycastTarget = false; stroke.Taper = .3f;
            return stroke;
        }

        private static Text Label(RectTransform box, string text, Font font, int size, Color color)
        {
            var rect = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(box, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12, 2); rect.offsetMax = new Vector2(-12, 0);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false; label.supportRichText = false;
            return label;
        }

        private void Update()
        {
            if (field == null) return;
            var events = EventSystem.current;
            bool live = field.isFocused || (events != null && events.currentSelectedGameObject == gameObject);
            float target = live ? 1f : 0f;
            if (focus == target) return;
            focus = InkTheme.ReducedMotion ? target : Mathf.MoveTowards(focus, target, Time.unscaledDeltaTime / .18f);
            float t = InkTheme.Smooth(focus);
            wash.color = Color.Lerp(washNormal, washFocused, t);
            wipe.Fill = t;
        }
    }
}
