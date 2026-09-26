using LastCall.Game;

namespace LastCall.UI
{
    /// <summary>
    /// Global motion settings. Reduced-motion (accessibility, GDD 12 juice) collapses
    /// every stage animation to an instant snap.
    ///
    /// READ THROUGH THE ONE STORE (2026-09-26, the settings' second page of options): the value lives in
    /// <see cref="PlayerOptions"/> under the key it always had ("lastcall.reducedMotion"), so the PlayMode suite can
    /// pin it from Game and a play started without a domain reload reads it fresh. The seventy callers keep asking
    /// <c>Motion.Reduced</c>.
    /// </summary>
    public static class Motion
    {
        public static bool Reduced
        {
            get => PlayerOptions.ReducedMotion;
            set => PlayerOptions.ReducedMotion = value;
        }

        /// <summary>
        /// NOTHING FLICKERS (2026-09-26, the FLASHES switch): the neon's stutter, the room's mains flicker, the
        /// television switching itself off and on, the patience bar breathing red. Every one was already under
        /// three flashes a second - this is comfort (migraine, photosensitivity), not compliance. Reduced motion
        /// implies it, as it always silenced them. Asked every frame by each of them, so a switch thrown mid-night
        /// reaches the room at once.
        /// </summary>
        public static bool NoFlashes => Reduced || !PlayerOptions.Flashes;
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
