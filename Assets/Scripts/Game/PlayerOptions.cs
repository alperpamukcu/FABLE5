using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// THE PLAYER'S OPTIONS, IN ONE STORE (2026-09-26, the author: "Ayarlara daha fazla seçenek ekleyelim hem
    /// erişebilirlik hem display kısmında çözünürlük ters mouse vs vs"). Every option the settings window adds is a
    /// PREFERENCE, never a rule: nothing here reaches Core, RunRng or the data, so a run plays the same whatever the
    /// player picked - the sim and the projection never read it.
    ///
    /// One store, in Game rather than UI, for two reasons: the PlayMode suite can reach Game and not UI, so it can pin
    /// every option to its default for its own run (<see cref="UseDefaultsForSession"/>, beside
    /// <c>Localization.UseForSession</c>); and the EditMode tests can hold the defaults to today's game. Each option is
    /// an int under a <c>lastcall.*</c> PlayerPrefs key, read once and kept; <c>Motion.Reduced</c> reads through here
    /// and keeps the key it always had.
    ///
    /// EVERY DEFAULT IS TODAY'S GAME, with one written exception: a player's build holds the night when the window
    /// loses focus (<see cref="PauseWhenAway"/>). The window and its size are not here: those are Unity's own player
    /// keys, which Alt+Enter writes too (<see cref="DisplayOptions"/>).
    /// </summary>
    public static class PlayerOptions
    {
        public const string ReducedMotionKey = "lastcall.reducedMotion";
        public const string NoFlashesKey = "lastcall.noFlashes";
        public const string PointerKey = "lastcall.pointer";
        public const string ColourCuesKey = "lastcall.colourCues";
        public const string FrameCapKey = "lastcall.frameCap";
        public const string PauseAwayKey = "lastcall.pauseAway";
        public const string InvertPourKey = "lastcall.invertPour";

        /// <summary>The hand the pointer is drawn as: the 32x32 hardware cursor, or the same drawing at twice that
        /// (a software cursor, which Windows cannot shrink - and which runs a frame behind the mouse).</summary>
        public enum PointerSize { Normal = 0, Large = 1 }

        /// <summary>The five measures' colours and the patience bar's: the author's red-to-green ladder, or a ladder
        /// every eye can tell apart (built from the palette's own ramps, TycoonHud.BandBoxColors).</summary>
        public enum ColourCues { Standard = 0, Clear = 1 }

        /// <summary>The one default that is not today's game: a PLAYER'S BUILD holds the night when another window
        /// comes up. The editor never does - the author drives it from the IDE with the game unfocused, and the
        /// PlayMode suite ignores focus on purpose (CLAUDE.md, "Verifying changes").</summary>
        public const bool PauseWhenAwayInPlayer = true;

        /// <summary>Where the options are kept: PlayerPrefs in the game, a dictionary in a test.</summary>
        public interface IStore
        {
            bool TryGet(string key, out int value);
            void Set(string key, int value);
        }

        private sealed class PrefsStore : IStore
        {
            public bool TryGet(string key, out int value)
            {
                if (PlayerPrefs.HasKey(key)) { value = PlayerPrefs.GetInt(key, 0); return true; }
                value = 0;
                return false;
            }

            public void Set(string key, int value)
            {
                PlayerPrefs.SetInt(key, value);
                PlayerPrefs.Save();          // a settings click is rare; a crash must not lose it
            }
        }

        /// <summary>A store that keeps its values in memory and counts its writes - what the tests hand in.</summary>
        public sealed class MemoryStore : IStore
        {
            public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
            public int Writes { get; private set; }

            public bool TryGet(string key, out int value) => Values.TryGetValue(key, out value);

            public void Set(string key, int value)
            {
                Values[key] = value;
                Writes++;
            }
        }

        private enum Id { ReducedMotion, NoFlashes, Pointer, ColourCues, FrameCap, PauseAway, InvertPour, Count }

        private static readonly string[] Keys =
        {
            ReducedMotionKey, NoFlashesKey, PointerKey, ColourCuesKey, FrameCapKey, PauseAwayKey, InvertPourKey,
        };

        private static readonly int[] s_value = new int[(int)Id.Count];
        private static readonly bool[] s_loaded = new bool[(int)Id.Count];
        private static bool s_session;
        private static IStore s_store;

        private static IStore Store => s_store ?? (s_store = new PrefsStore());

        /// <summary>Is this run on the defaults for its own session (the PlayMode suite)?</summary>
        public static bool InSession => s_session;

        // ── the options ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>MOTION: every stage animation collapsed to a snap (GDD 12). It implies no flashes.</summary>
        public static bool ReducedMotion
        {
            get => Get(Id.ReducedMotion) == 1;
            set => Set(Id.ReducedMotion, value ? 1 : 0);
        }

        /// <summary>FLASHES: the neon's stutter, the room's mains flicker, the television switching itself off and
        /// on, the patience bar breathing red. On by default; reduced motion turns them off as well
        /// (<c>Motion.NoFlashes</c>). Stored the other way round, as the key has always been "no flashes".</summary>
        public static bool Flashes
        {
            get => Get(Id.NoFlashes) == 0;
            set => Set(Id.NoFlashes, value ? 0 : 1);
        }

        public static PointerSize Pointer
        {
            get => Get(Id.Pointer) == 1 ? PointerSize.Large : PointerSize.Normal;
            set => Set(Id.Pointer, value == PointerSize.Large ? 1 : 0);
        }

        public static ColourCues Cues
        {
            get => Get(Id.ColourCues) == 1 ? ColourCues.Clear : ColourCues.Standard;
            set => Set(Id.ColourCues, value == ColourCues.Clear ? 1 : 0);
        }

        /// <summary>FRAME RATE: 0 matches the screen (vsync, today's pacing); otherwise one of
        /// <see cref="DisplayOptions.FrameCaps"/>. Anything else read back from the store is the screen's.</summary>
        public static int FrameCap
        {
            get
            {
                int v = Get(Id.FrameCap);
                return Array.IndexOf(DisplayOptions.FrameCaps, v) >= 0 ? v : 0;
            }
            set => Set(Id.FrameCap, Array.IndexOf(DisplayOptions.FrameCaps, value) >= 0 ? value : 0);
        }

        /// <summary>PAUSE WHEN AWAY, as the player set it (what the settings row shows). Whether it acts is
        /// <see cref="PausesWhenAway"/>.</summary>
        public static bool PauseWhenAway
        {
            get => Get(Id.PauseAway) == 1;
            set => Set(Id.PauseAway, value ? 1 : 0);
        }

        /// <summary>Whether losing focus holds the night: the player's choice, and never in the editor.</summary>
        public static bool PausesWhenAway => PauseWhenAway && !Application.isEditor;

        /// <summary>INVERT POUR: the bottle and the tin tip as the hand comes DOWN instead of up (PourHand).</summary>
        public static bool InvertPour
        {
            get => Get(Id.InvertPour) == 1;
            set => Set(Id.InvertPour, value ? 1 : 0);
        }

        // ── the store ───────────────────────────────────────────────────────────────────────────────────────────

        private static int Default(Id id)
        {
            switch (id)
            {
                case Id.PauseAway: return PauseWhenAwayInPlayer && !Application.isEditor ? 1 : 0;
                default: return 0;
            }
        }

        private static int Get(Id id)
        {
            int i = (int)id;
            if (!s_loaded[i])
            {
                s_value[i] = !s_session && Store.TryGet(Keys[i], out int v) ? v : Default(id);
                s_loaded[i] = true;
            }
            return s_value[i];
        }

        private static void Set(Id id, int value)
        {
            int i = (int)id;
            s_value[i] = value;
            s_loaded[i] = true;
            if (!s_session) Store.Set(Keys[i], value);   // a session's options are its own and are never saved
        }

        /// <summary>RESET DEFAULTS: every option here back to its default, saved. (The window and its size are not
        /// options here, so a reset never throws a window into fullscreen.)</summary>
        public static void ResetDefaults()
        {
            for (int i = 0; i < (int)Id.Count; i++) Set((Id)i, Default((Id)i));
        }

        /// <summary>Every option at its default for this play session only, and nothing written: the PlayMode
        /// suite's baselines and presses must not depend on what the author last picked in the settings.</summary>
        public static void UseDefaultsForSession()
        {
            s_session = true;
            for (int i = 0; i < (int)Id.Count; i++)
            {
                s_value[i] = Default((Id)i);
                s_loaded[i] = true;
            }
        }

        /// <summary>Drops what was read (and a session's defaults), so the next read goes back to the store.</summary>
        public static void Forget()
        {
            s_session = false;
            Array.Clear(s_loaded, 0, s_loaded.Length);
        }

        /// <summary>Keeps the options in <paramref name="store"/> from now on (null: PlayerPrefs again) - the tests'
        /// way in, so they never touch the author's own settings.</summary>
        public static void UseStore(IStore store)
        {
            s_store = store;
            Forget();
        }

        /// <summary>Forget what was read when play starts: the project enters play mode WITHOUT a domain reload, so
        /// statics outlive the session (the same as Localization.ForgetOnPlay). A suite's session defaults end with
        /// its play, and an option changed in a build started from the editor is read fresh.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetOnPlay() => Forget();
    }
}
