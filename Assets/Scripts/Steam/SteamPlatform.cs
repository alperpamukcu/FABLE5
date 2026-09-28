#if !DISABLESTEAMWORKS
using System;
using LastCall.Game;
using Steamworks;
using UnityEngine;

namespace LastCall.Steam
{
    /// <summary>
    /// STEAM, BEHIND THE GAME'S ONE STORE DOOR (2026-09-28; <see cref="StorePlatform"/>). This assembly is
    /// compiled only when the Steamworks.NET package is installed (its asmdef's version define), and even then
    /// Steam is started only once <see cref="StoreLink.SteamAppId"/> is a real app: until the app exists on
    /// Steamworks the game runs exactly as it did, its achievements kept on disk.
    ///
    /// Up, it registers itself before the first scene loads — early enough for the language the player set in
    /// the Steam library to choose the game's — pumps Steam's callbacks once a frame, tells the game when the
    /// overlay comes up (the HUD pauses an open night), and shuts Steam down when play ends.
    /// </summary>
    public sealed class SteamPlatform : IStorePlatform
    {
        private static SteamPlatform s_live;
        private static Callback<GameOverlayActivated_t> s_overlay;

        public string Name => "Steam";

        public string Language
        {
            get
            {
                try { return SteamApps.GetCurrentGameLanguage(); }
                catch (Exception) { return null; }
            }
        }

        // Enter Play Mode runs without a domain reload: anything the last play left up is taken down first.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSession() => Shutdown();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (StoreLink.SteamAppId == 0) return;   // no app on Steamworks yet: Steam is never started
            try
            {
#if !UNITY_EDITOR
                // Started outside Steam: Steam starts the game itself and this copy steps aside.
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(StoreLink.SteamAppId)))
                {
                    Application.Quit();
                    return;
                }
#endif
                if (!Packsize.Test() || !DllCheck.Test())
                {
                    Debug.LogError("[store] Steamworks.NET does not match this platform; Steam stays off.");
                    return;
                }
                if (!SteamAPI.Init())
                {
                    Debug.Log("[store] Steam is not running for this app; achievements stay on disk.");
                    return;
                }
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError("[store] steam_api could not be loaded; Steam stays off. " + e.Message);
                return;
            }

            s_live = new SteamPlatform();
            s_overlay = Callback<GameOverlayActivated_t>.Create(t => StorePlatform.RaiseOverlay(t.m_bActive != 0));
            var pump = new GameObject("SteamPump") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(pump);
            pump.AddComponent<SteamPump>();
            StorePlatform.Register(s_live);
        }

        // A script reload in the middle of a play (the editor's "Recompile And Continue Playing") clears these
        // statics and Steamworks.NET's dispatcher count with them, but leaves the pump standing: it asks here
        // before every pump, and stands down instead of throwing once a frame.
        internal static bool IsUp => s_live != null && CallbackDispatcher.IsInitialized;

        internal static void Shutdown()
        {
            var live = s_live;
            if (live == null) return;
            s_live = null;
            s_overlay?.Dispose();
            s_overlay = null;
            foreach (var find in live._finds) find.Dispose();
            live._finds.Clear();
            SteamAPI.Shutdown();
        }

        public void Unlock(string achievementId) => SteamUserStats.SetAchievement(achievementId);

        public bool IsUnlocked(string achievementId) =>
            SteamUserStats.GetAchievement(achievementId, out bool achieved) && achieved;

        public void SetStat(string stat, long value) =>
            SteamUserStats.SetStat(stat, (int)Math.Min(value, int.MaxValue));

        public bool TryGetStat(string stat, out long value)
        {
            // False for a stat Steamworks does not define yet — the merge then leaves it to this machine.
            bool known = SteamUserStats.GetStat(stat, out int v);
            value = known ? v : 0;
            return known;
        }

        public void ShowProgress(string achievementId, long have, long need) =>
            SteamUserStats.IndicateAchievementProgress(achievementId,
                (uint)Math.Max(0, Math.Min(have, uint.MaxValue)), (uint)Math.Max(1, Math.Min(need, uint.MaxValue)));

        public void Commit() => SteamUserStats.StoreStats();

        public void SetPresence(string key, string value) => SteamFriends.SetRichPresence(key, value);

        public void ClearPresence() => SteamFriends.ClearRichPresence();

        // ── leaderboards ──────────────────────────────────────────────────────────────────────────────
        // A board is found (or made, the first time) asynchronously; scores sent before it answers wait, and
        // only the best of them goes up. Steam keeps a player's best on each board (KeepBest).

        private readonly System.Collections.Generic.Dictionary<string, SteamLeaderboard_t> _boards =
            new System.Collections.Generic.Dictionary<string, SteamLeaderboard_t>();
        private readonly System.Collections.Generic.Dictionary<string, long> _waiting =
            new System.Collections.Generic.Dictionary<string, long>();
        private readonly System.Collections.Generic.HashSet<string> _asking = new System.Collections.Generic.HashSet<string>();
        private readonly System.Collections.Generic.List<CallResult<LeaderboardFindResult_t>> _finds =
            new System.Collections.Generic.List<CallResult<LeaderboardFindResult_t>>();

        public void SubmitScore(string board, long score)
        {
            if (string.IsNullOrEmpty(board) || score <= 0) return;
            if (_boards.TryGetValue(board, out var handle))
            {
                Upload(handle, score);
                return;
            }
            _waiting[board] = _waiting.TryGetValue(board, out long was) ? Math.Max(was, score) : score;
            if (!_asking.Add(board)) return;
            var call = SteamUserStats.FindOrCreateLeaderboard(board, ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);
            var result = CallResult<LeaderboardFindResult_t>.Create((found, failed) =>
            {
                _asking.Remove(board);
                if (failed || found.m_bLeaderboardFound == 0) return;   // asked again with the next score
                _boards[board] = found.m_hSteamLeaderboard;
                if (_waiting.TryGetValue(board, out long best))
                {
                    _waiting.Remove(board);
                    Upload(found.m_hSteamLeaderboard, best);
                }
            });
            result.Set(call);
            _finds.Add(result);
        }

        private static void Upload(SteamLeaderboard_t board, long score) =>
            SteamUserStats.UploadLeaderboardScore(board, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
                (int)Math.Min(score, int.MaxValue), null, 0);

        // ── the timeline ──────────────────────────────────────────────────────────────────────────────

        public void SetTimelineMode(StoreTimeline.Mode mode) =>
            SteamTimeline.SetTimelineGameMode(mode == StoreTimeline.Mode.Playing ? ETimelineGameMode.k_ETimelineGameMode_Playing
                : mode == StoreTimeline.Mode.Staging ? ETimelineGameMode.k_ETimelineGameMode_Staging
                : ETimelineGameMode.k_ETimelineGameMode_Menus);

        public void SetTimelineTooltip(string text)
        {
            if (string.IsNullOrEmpty(text)) SteamTimeline.ClearTimelineTooltip(0f);
            else SteamTimeline.SetTimelineTooltip(text, 0f);
        }

        public void MarkMoment(string title, string description, string icon, int priority, bool featured) =>
            SteamTimeline.AddInstantaneousTimelineEvent(title ?? string.Empty, description ?? string.Empty,
                string.IsNullOrEmpty(icon) ? "steam_marker" : icon, (uint)Math.Max(0, priority), 0f,
                featured ? ETimelineEventClipPriority.k_ETimelineEventClipPriority_Featured
                         : ETimelineEventClipPriority.k_ETimelineEventClipPriority_Standard);

        public void BeginPhase(string id)
        {
            SteamTimeline.StartGamePhase();
            if (!string.IsNullOrEmpty(id)) SteamTimeline.SetGamePhaseID(id);
        }

        public void EndPhase() => SteamTimeline.EndGamePhase();
    }

    /// <summary>Pumps Steam's callbacks once a frame and takes Steam down with the game.</summary>
    internal sealed class SteamPump : MonoBehaviour
    {
        private void Update()
        {
            if (SteamPlatform.IsUp) SteamAPI.RunCallbacks();
            else Destroy(gameObject);   // Steam went down under it (a reload mid-play): the rest of the play is offline
        }

        private void OnDestroy() => SteamPlatform.Shutdown();
    }
}
#endif
