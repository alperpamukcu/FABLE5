using System;
using System.Collections;
using UnityEngine;

namespace LastCall.DebugUI
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
    /// Lightweight coroutine tween utility — a swappable stand-in for DOTween (which is
    /// not vendored). Only the easings the diegetic stage needs. Durations honour
    /// <see cref="Motion.Reduced"/> (snap to the end instantly).
    /// </summary>
    public static class Tweening
    {
        /// <summary>Anchored-position move with an easing; invokes <paramref name="onDone"/> at the end.</summary>
        public static IEnumerator MoveAnchored(RectTransform rt, Vector2 to, float duration,
            Func<float, float> ease, Action onDone = null)
        {
            if (rt == null) yield break;
            if (Motion.Reduced || duration <= 0f)
            {
                rt.anchoredPosition = to;
                onDone?.Invoke();
                yield break;
            }

            Vector2 from = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = ease(Mathf.Clamp01(t / duration));
                if (rt == null) yield break;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = to;
            onDone?.Invoke();
        }

        /// <summary>Fade a CanvasGroup, honouring reduced motion.</summary>
        public static IEnumerator Fade(CanvasGroup group, float to, float duration, Action onDone = null)
        {
            if (group == null) yield break;
            if (Motion.Reduced || duration <= 0f)
            {
                group.alpha = to;
                onDone?.Invoke();
                yield break;
            }

            float from = group.alpha, t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (group == null) yield break;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (group != null) group.alpha = to;
            onDone?.Invoke();
        }

        // ── easings ──────────────────────────────────────────────────────────────

        /// <summary>Overshoot-and-settle (DOTween's OutBack). The signature bottle-slide feel.</summary>
        public static float OutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        public static float OutCubic(float x)
        {
            float p = 1f - x;
            return 1f - p * p * p;
        }
    }
}
