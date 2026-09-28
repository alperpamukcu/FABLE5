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

        internal static void Shutdown()
        {
            if (s_live == null) return;
            s_overlay?.Dispose();
            s_overlay = null;
            s_live = null;
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
    }

    /// <summary>Pumps Steam's callbacks once a frame and takes Steam down with the game.</summary>
    internal sealed class SteamPump : MonoBehaviour
    {
        private void Update() => SteamAPI.RunCallbacks();

        private void OnDestroy() => SteamPlatform.Shutdown();
    }
}
#endif
