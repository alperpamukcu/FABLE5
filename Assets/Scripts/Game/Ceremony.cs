using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// HOW FAST THE CEREMONIES PLAY (2026-09-14, the author: "playmode testlerini daha optimize hızlı
    /// yapamaz mısın? Tüm hızlandırmaları yap"). The curtain between two nights (7 s) and the night's
    /// slip, stars, stamp and standing (about 10 s) are presentation the player watches once a night;
    /// the PlayMode suite walked through them 13 and 4 times a run — 80 s and 40 s of its 272, measured
    /// (Temp/PlayTestTimes.txt). The suite sets a pace for its own run, and the HUD multiplies those
    /// ceremonies' clocks by it. Nothing that decides anything reads it: rules run on Core's clock,
    /// and the end state of every ceremony is the same at any pace, so the look baselines hold.
    /// </summary>
    public static class Ceremony
    {
        /// <summary>1 in the game. Only the PlayMode suite raises it, and only for its own run.</summary>
        public static float Pace = 1f;

        /// <summary>Back to 1 whenever play starts: the project enters play without a domain reload,
        /// so a suite killed mid-run would otherwise leave the next play's curtain racing.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Pace = 1f;
    }
}
