using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.UI
{
    // Footer key hints shared by every Eclipse menu: each hint is a small paper keycap with
    // the key on it, followed by what it does. Hints with a click action are also buttons
    // (mouse only; they never take keyboard or pad focus).
    public static class InkKeyHints
    {
        public struct Hint
        {
            public string Key, Action;
            public Action Click;
            public Hint(string key, string action, Action click = null) { Key = key; Action = action; Click = click; }
        }

        private const float CapHeight = 25f, CapGap = 8f, HintGap = 24f;

        // Lays the hints in a row inside <paramref name="host"/>, vertically centred, from its
        // left edge (or ending at its right edge). Returns the row's width.
        public static float Build(RectTransform host, Font font, IList<Hint> hints, bool alignRight)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                host.GetChild(i).gameObject.SetActive(false);
                UnityEngine.Object.Destroy(host.GetChild(i).gameObject);
            }
            var paper = InkTheme.Paper;
            var widths = new List<(Text key, Text action, float cap, float act)>();
            float total = 0f;
            foreach (var hint in hints)
            {
                var key = MakeLabel(host, hint.Key, font, 12, paper, TextAnchor.MiddleCenter);
                var action = MakeLabel(host, hint.Action, font, 15, InkTheme.Alpha(paper, .85f), TextAnchor.MiddleLeft);
                float cap = Mathf.Max(26f, key.preferredWidth + 14f), act = action.preferredWidth + 2f;
                widths.Add((key, action, cap, act));
                total += cap + CapGap + act;
            }
            if (hints.Count > 1) total += HintGap * (hints.Count - 1);
            float x = alignRight ? -total : 0f;
            for (int i = 0; i < hints.Count; i++)
            {
                var (key, action, cap, act) = widths[i];
                var box = new GameObject("Key", typeof(RectTransform)).GetComponent<RectTransform>();
                box.SetParent(host, false);
                box.SetSiblingIndex(key.transform.GetSiblingIndex());
                var boxImage = box.gameObject.AddComponent<Image>(); boxImage.color = InkTheme.Alpha(paper, .13f); boxImage.raycastTarget = false;
                Place(box, x, cap, CapHeight, alignRight);
                var edge = new GameObject("Edge", typeof(RectTransform)).GetComponent<RectTransform>();
                edge.SetParent(box, false);
                edge.anchorMin = Vector2.zero; edge.anchorMax = new Vector2(1, 0); edge.pivot = new Vector2(.5f, 0);
                edge.anchoredPosition = Vector2.zero; edge.sizeDelta = new Vector2(0, 2);
                var edgeImage = edge.gameObject.AddComponent<Image>(); edgeImage.color = InkTheme.Alpha(paper, .35f); edgeImage.raycastTarget = false;
                Place(key.rectTransform, x, cap, CapHeight, alignRight);
                Place(action.rectTransform, x + cap + CapGap, act, 24f, alignRight);
                if (hints[i].Click != null)
                {
                    var hit = new GameObject(hints[i].Key + " button", typeof(RectTransform)).GetComponent<RectTransform>();
                    hit.SetParent(host, false);
                    Place(hit, x - 4f, cap + CapGap + act + 8f, 34f, alignRight);
                    var hitImage = hit.gameObject.AddComponent<Image>(); hitImage.color = Color.clear;
                    var button = hit.gameObject.AddComponent<Button>();
                    button.targetGraphic = hitImage;
                    button.transition = Selectable.Transition.None;
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
                    var click = hints[i].Click;
                    button.onClick.AddListener(() => click());
                    // A hover brightens the hint, so it reads as clickable.
                    var keyLabel = key; var actionLabel = action;
                    Eclipse.Multiplayer.UiHover.Attach(hit.gameObject,
                        () => { if (actionLabel != null) actionLabel.color = paper; if (boxImage != null) boxImage.color = InkTheme.Alpha(InkTheme.Red, .85f); },
                        () => { if (actionLabel != null) actionLabel.color = InkTheme.Alpha(paper, .85f); if (boxImage != null) boxImage.color = InkTheme.Alpha(paper, .13f); });
                }
                x += cap + CapGap + act + HintGap;
            }
            return total;
        }

        private static Text MakeLabel(RectTransform host, string text, Font font, int size, Color color, TextAnchor alignment)
        {
            var rect = new GameObject(text, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(host, false);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.alignment = alignment; label.raycastTarget = false; label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static void Place(RectTransform rect, float x, float width, float height, bool alignRight)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(alignRight ? 1f : 0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
