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
