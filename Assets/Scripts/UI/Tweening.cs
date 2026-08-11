using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// Global motion settings. Reduced-motion (accessibility, GDD 12 juice) collapses
    /// every stage animation to an instant snap. PlayerPrefs-backed so a settings
    /// screen (module 16, not built yet) can flip it later.
    /// </summary>
    public static class Motion
    {
        private const string Key = "lastcall.reducedMotion";
        private static bool _loaded;
        private static bool _reduced;

        public static bool Reduced
        {
            get
            {
                if (!_loaded) { _reduced = PlayerPrefs.GetInt(Key, 0) == 1; _loaded = true; }
                return _reduced;
            }
            set
            {
                _reduced = value; _loaded = true;
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
            }
        }
    }

    /// <summary>
    /// The easing shelf. The coroutine tweens that lived here died unused (audit
    /// 2026-08-11) — the house pattern is the Update-timer, precisely because an
    /// interrupted coroutine parks its target at the start offset — and the stage
    /// slide keeps only the curve it actually rides.
    /// </summary>
    public static class Tweening
    {
        // ── easings ──────────────────────────────────────────────────────────────

        public static float OutCubic(float x)
        {
            float p = 1f - x;
            return 1f - p * p * p;
        }
    }
}
