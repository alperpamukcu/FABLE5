using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// THE ONE DOOR TO THE STORE (2026-09-27, the author: "steam sayfasına gönderen bir
    /// buton"). The page's address lives here and nowhere else — the app has not been
    /// created on Steam yet (LAUNCH_READINESS, 26–28 Sep), so the address is EMPTY and
    /// every key that would open it hides itself until the author fills this one line in.
    /// </summary>
    public static class StoreLink
    {
        /// <summary>The game's Steam page, e.g. "https://store.steampowered.com/app/XXXXXXX/Malibu_Club/".
        /// Empty until the app exists; the menu's STEAM key stands only when this does.</summary>
        public const string Page = "";

        public static bool HasPage => !string.IsNullOrEmpty(Page);

        /// <summary>
        /// THE STEAM APP ID (2026-09-28). Zero until the app exists on Steamworks; while it is zero the Steam
        /// assembly never starts Steam, so the editor does not show the author as playing Spacewar and a build
        /// keeps its achievements on disk alone. The day the app is created: this number, the same number in
        /// steam_appid.txt at the project root (the editor's test runs), and Docs/steam/STEAMWORKS_SETUP.md.
        /// </summary>
        public const uint SteamAppId = 0;

        public static void Open()
        {
            if (HasPage) Application.OpenURL(Page);
        }
    }
}
