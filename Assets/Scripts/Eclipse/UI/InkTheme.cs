using UnityEngine;

namespace Eclipse.UI
{
    // The shared ink-and-paper palette, easing curves and the reduced-motion preference for
    // Eclipse-owned menus. Colour roles: red marks focus and the one primary action on a page;
    // ink marks state (the open tab, a chosen value); paper is the ground.
    public static class InkTheme
    {
        public static readonly Color Ink = new Color32(30, 25, 22, 255);
        public static readonly Color Paper = new Color32(223, 207, 177, 255);
        public static readonly Color PaperWarm = new Color32(240, 226, 196, 255);
        public static readonly Color PaperDim = new Color32(196, 178, 146, 255);
        public static readonly Color Red = new Color32(147, 39, 31, 255);
        public static readonly Color RedBright = new Color32(186, 52, 36, 255);
        public static readonly Color RedDeep = new Color32(120, 24, 18, 255);
        public static readonly Color Gold = new Color32(214, 170, 78, 255);

        public static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }

        // --- Reduced motion ------------------------------------------------------------------

        private const string ReducedMotionKey = "Eclipse.ReducedMotion";
        private static int reducedMotion = -1;

        // Entrances become short fades, focus marks appear without sweeping, and the title
        // scene stops breathing and swaying. Colour and focus feedback stay.
        public static bool ReducedMotion
        {
            get
            {
                if (reducedMotion < 0)
                {
                    try { reducedMotion = PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0 ? 1 : 0; }
                    catch (UnityException) { return false; } // PlayerPrefs is main-thread only.
                }
                return reducedMotion == 1;
            }
        }

        public static void ToggleReducedMotion()
        {
            reducedMotion = ReducedMotion ? 0 : 1;
            PlayerPrefs.SetInt(ReducedMotionKey, reducedMotion);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { reducedMotion = -1; }

        // --- Easing (t in 0..1) --------------------------------------------------------------

        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        public static float OutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
        public static float OutQuad(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t; }

        // Settles with a slight overshoot.
        public static float OutBack(float t, float overshoot = 1.25f)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f) return 0f;
            float u = t - 1f, c3 = overshoot + 1f;
            return 1f + c3 * u * u * u + overshoot * u * u;
        }
    }
}
