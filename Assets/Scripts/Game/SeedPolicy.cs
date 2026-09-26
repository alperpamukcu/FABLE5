using System;

namespace LastCall.Game
{
    /// <summary>
    /// WHERE A NEW RUN'S SEED COMES FROM (2026-09-26, the main menu). Every player used to
    /// get the inspector's one seed, so every bar in the world played the same nights; the
    /// menu's NEW RUN and the settings' START OVER draw a fresh one here instead.
    ///
    /// This is the ONE nondeterministic door in the Game layer, and it opens only when the
    /// player asks for a new run — Core never calls it, and a seed once drawn reproduces its
    /// whole run like any other (CLAUDE.md, determinism). The PlayMode fixtures pin it with
    /// <see cref="UseForSession"/> beside the other session pins, so the suite plays the
    /// seed its baselines were blessed on.
    ///
    /// The shape is readable on purpose ("MC-K4T7NZ"): a seed the author can copy off a
    /// screenshot into the simulator is a bug report that reproduces.
    /// </summary>
    public static class SeedPolicy
    {
        private static string s_pinned;

        /// <summary>With domain reload off a static survives play sessions; the suite's pin
        /// must never seed the author's own NEW RUN (the SaveStore learned this the measured
        /// way, r242).</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSession() => s_pinned = null;

        /// <summary>The fixtures' pin: every seed this session draws is this one.</summary>
        public static void UseForSession(string seed) => s_pinned = seed;

        /// <summary>A fresh seed — or the session's pinned one. No 0/O or 1/I/L: the seed
        /// is meant to be read off a screen and typed back.</summary>
        public static string Next()
        {
            if (!string.IsNullOrEmpty(s_pinned)) return s_pinned;
            const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            var bytes = Guid.NewGuid().ToByteArray();
            var chars = new char[6];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            return "MC-" + new string(chars);
        }
    }
}
