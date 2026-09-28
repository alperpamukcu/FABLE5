using System;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// The storefront the game is running under (2026-09-28, the author: "Oyuna steam etkileşimleri koyalım").
    /// Everything the game asks of a store goes through this one door: achievements and their stats, the
    /// status friends see (rich presence), the store's language, and the overlay coming up. The Steam
    /// assembly (Assets/Scripts/Steam, compiled only when the Steamworks.NET package is installed) registers
    /// an implementation at start-up; without one — the editor with no App ID, a build off Steam — the game
    /// keeps its achievements on disk alone and nothing else changes.
    /// </summary>
    public interface IStorePlatform
    {
        /// <summary>"Steam". For the log and the credits.</summary>
        string Name { get; }
        /// <summary>The store's own name for the language the player runs it in ("english", "turkish",
        /// "schinese"), or null when it has none to give.</summary>
        string Language { get; }
        void Unlock(string achievementId);
        bool IsUnlocked(string achievementId);
        void SetStat(string stat, long value);
        bool TryGetStat(string stat, out long value);
        /// <summary>The store's own "3 / 10" notice for an achievement still to come.</summary>
        void ShowProgress(string achievementId, long have, long need);
        /// <summary>Sends everything set since the last commit.</summary>
        void Commit();
        void SetPresence(string key, string value);
        void ClearPresence();

        /// <summary>A score on a leaderboard, kept only if it beats the player's own best there.</summary>
        void SubmitScore(string board, long score);

        /// <summary>The Timeline's mode: what the recording bar shows the player was doing.</summary>
        void SetTimelineMode(StoreTimeline.Mode mode);
        /// <summary>The line under the recording bar, until the next one.</summary>
        void SetTimelineTooltip(string text);
        /// <summary>A marked moment on the recording. <paramref name="featured"/> offers it as a clip.</summary>
        void MarkMoment(string title, string description, string icon, int priority, bool featured);
        /// <summary>A stretch of play the recording groups under one heading (a night).</summary>
        void BeginPhase(string id);
        void EndPhase();
    }

    public static class StorePlatform
    {
        /// <summary>The store the game runs under, or null.</summary>
        public static IStorePlatform Current { get; private set; }

        /// <summary>The overlay came up (true) or went away (false). The HUD pauses an open night on it.</summary>
        public static event Action<bool> OverlayChanged;

        // Enter Play Mode runs without a domain reload here, so every static starts each play clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSession()
        {
            Current = null;
            OverlayChanged = null;
        }

        /// <summary>Called by a store's own assembly once it is up.</summary>
        public static void Register(IStorePlatform platform)
        {
            Current = platform;
            if (platform != null) Debug.Log("[store] " + platform.Name + " is up");
        }

        /// <summary>Called by a store's own assembly when its overlay opens or closes.</summary>
        public static void RaiseOverlay(bool shown) => OverlayChanged?.Invoke(shown);
    }

    /// <summary>
    /// THE STEAM TIMELINE (2026-09-28, the Steamworks feature list's "Zaman Çizelgesi"). Steam records play for the
    /// player's own clips; the game marks what happened on that recording. Each night is a game phase (opened when
    /// the doors do, closed at dawn), the recording bar says what the player is doing (a night / the books / the
    /// front door), and the moments worth a clip are marked: an achievement earned, five stars in a night, a new
    /// rank, a perfect pour, a bar closing for good. Every string arrives already in the player's language (the HUD
    /// reads it from its tables); the icons are Steam's own (steam_*). Change-detected, so the HUD may call it every
    /// frame; with no store up, every call is nothing.
    /// </summary>
    public static class StoreTimeline
    {
        public enum Mode { Playing, Menus, Staging }

        private static Mode s_mode;
        private static bool s_modeSet, s_inPhase;
        private static string s_tooltip, s_phase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSession()
        {
            s_modeSet = s_inPhase = false;
            s_tooltip = s_phase = null;
        }

        public static void SetMode(Mode mode)
        {
            var store = StorePlatform.Current;
            if (store == null || (s_modeSet && s_mode == mode)) return;
            s_mode = mode;
            s_modeSet = true;
            store.SetTimelineMode(mode);
        }

        public static void Tooltip(string text)
        {
            var store = StorePlatform.Current;
            if (store == null || text == s_tooltip) return;
            s_tooltip = text;
            store.SetTimelineTooltip(text ?? string.Empty);
        }

        /// <summary>Opens the phase <paramref name="id"/> (one night of one bar), closing any other first.</summary>
        public static void BeginPhase(string id)
        {
            var store = StorePlatform.Current;
            if (store == null || (s_inPhase && id == s_phase)) return;
            if (s_inPhase) store.EndPhase();
            store.BeginPhase(id);
            s_inPhase = true;
            s_phase = id;
        }

        public static void EndPhase()
        {
            var store = StorePlatform.Current;
            if (store == null || !s_inPhase) return;
            store.EndPhase();
            s_inPhase = false;
            s_phase = null;
        }

        /// <summary>A moment worth finding again; <paramref name="featured"/> offers it as a clip.</summary>
        public static void Moment(string title, string description, string icon, int priority, bool featured) =>
            StorePlatform.Current?.MarkMoment(title, description ?? string.Empty, icon, priority, featured);
    }

    /// <summary>
    /// WHAT FRIENDS SEE (Steam rich presence). One status at a time — the front door, a night being worked,
    /// the books being closed — set only when it changes. The display strings live in the store's own
    /// localisation file (Docs/steam/STEAMWORKS_SETUP.md): the game sends a token and its numbers.
    /// </summary>
    public static class StorePresence
    {
        public enum State { None, Menu, Night, Books }

        private static State s_state;
        private static int s_night, s_stars;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSession()
        {
            s_state = State.None;
            s_night = s_stars = -1;
        }

        /// <summary>Shows <paramref name="state"/>; <paramref name="stars"/> is the bar's standing in whole
        /// stars. Cheap to call every frame: the store hears only a change.</summary>
        public static void Show(State state, int night, int stars)
        {
            var store = StorePlatform.Current;
            if (store == null) return;
            if (state == s_state && night == s_night && stars == s_stars) return;
            s_state = state;
            s_night = night;
            s_stars = stars;
            switch (state)
            {
                case State.Menu:
                    store.SetPresence("steam_display", "#Status_Menu");
                    break;
                case State.Night:
                    store.SetPresence("night", night.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    store.SetPresence("stars", stars.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    store.SetPresence("steam_display", "#Status_Night");
                    break;
                case State.Books:
                    store.SetPresence("night", night.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    store.SetPresence("steam_display", "#Status_Books");
                    break;
                default:
                    store.ClearPresence();
                    break;
            }
        }
    }
}
