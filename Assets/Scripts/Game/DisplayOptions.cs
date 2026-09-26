using System.Collections.Generic;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// THE SCREEN (2026-09-26, the author: "display kısmında çözünürlük ..."): the window, its size and the frame
    /// pacing, as the settings window's DISPLAY page offers them.
    ///
    /// The picture is the 640x360 stage scaled by max(h/360, w/640) screen pixels a unit (DesignFrame), and the HUD
    /// by half that. A window at a WHOLE multiple of 640x360 keeps every stage pixel square, so those are the only
    /// window sizes offered; FULLSCREEN is the borderless window at the desktop's own size (ProjectSettings'
    /// FullScreenWindow), never an exclusive mode the monitor's scaler would blur. The window and its size are Unity's
    /// own player keys, which Alt+Enter writes too, so the page reads the live screen and the two never disagree.
    ///
    /// Nothing here touches the screen in the EDITOR: the PlayMode suite pins the Game view to 1280x720 and counts
    /// on the editor's own pacing (CLAUDE.md, "Verifying changes"). In the editor the page's choices are remembered
    /// for the session and applied nowhere.
    /// </summary>
    public static class DisplayOptions
    {
        public const int StageW = 640, StageH = 360;

        /// <summary>What a window leaves of the desktop's height for its title bar and the taskbar.</summary>
        public const int WindowChrome = 80;

        /// <summary>The frame caps offered: 0 matches the screen (vsync). No uncapped choice - a steady frame, not
        /// the most frames (GameBootstrap, 2026-09-11) - and none under 60.</summary>
        public static readonly int[] FrameCaps = { 0, 60, 120, 144 };

        // ── the arithmetic (pure, tested) ───────────────────────────────────────────────────────────────────────

        /// <summary>Screen pixels a stage unit at a <paramref name="w"/> x <paramref name="h"/> screen: DesignFrame's
        /// px, max(h/360, w/640).</summary>
        public static float Scale(int w, int h) => Mathf.Max(h / (float)StageH, w / (float)StageW);

        /// <summary>Screen pixels a HUD unit: the HUD's 1280x720 field is the stage at 2x.</summary>
        public static float HudFactor(int w, int h) => Scale(w, h) * 0.5f;

        /// <summary>
        /// The window sizes a desktop can hold: the whole multiples 640k x 360k from k = 2 (720 tall - below it the
        /// HUD's 8-size print would go under 8 screen pixels) that fit the desktop's width and its height less
        /// <see cref="WindowChrome"/>. 1920x1080 holds 1280x720; 2560x1440 adds 1920x1080; 1366x768 holds none.
        /// </summary>
        public static List<Vector2Int> WholeSizes(int desktopW, int desktopH)
        {
            var sizes = new List<Vector2Int>();
            for (int k = 2; k < 64; k++)
            {
                int w = StageW * k, h = StageH * k;
                if (w > desktopW || h > desktopH - WindowChrome) break;
                sizes.Add(new Vector2Int(w, h));
            }
            return sizes;
        }

        /// <summary>The window WINDOWED opens at: the largest whole size the desktop holds; on a desktop too small
        /// for any, the design's 1280x720 if the screen can show it at all, else the desktop itself.</summary>
        public static Vector2Int WindowFor(int desktopW, int desktopH)
        {
            var sizes = WholeSizes(desktopW, desktopH);
            if (sizes.Count > 0) return sizes[sizes.Count - 1];
            if (desktopW >= StageW * 2 && desktopH >= StageH * 2) return new Vector2Int(StageW * 2, StageH * 2);
            return new Vector2Int(desktopW, desktopH);
        }

        /// <summary>
        /// The pacing a frame cap asks for: 0 (MATCH SCREEN) is vsync at the display's own rate with no target -
        /// exactly what GameBootstrap set before there was a choice; a cap turns vsync off, because Unity ignores
        /// targetFrameRate while vsync is on.
        /// </summary>
        public static (int vSyncCount, int targetFrameRate) Pacing(int cap) => cap <= 0 ? (1, -1) : (0, cap);

        // ── the live screen ─────────────────────────────────────────────────────────────────────────────────────

        private static bool s_editorWindowed;
        private static Vector2Int s_editorSize;
        private static bool s_hasAsk;
        private static bool s_askWindowed;
        private static Vector2Int s_askSize;
        private static int s_askFrame;

        /// <summary>A change asked this frame lands at the frame's end, and the screen reports the old one until
        /// then; the page shows what was asked for a few frames rather than flicking back.</summary>
        private static bool Asked => s_hasAsk && Time.frameCount - s_askFrame < 10;

        /// <summary>The desktop's own size (the monitor's native resolution).</summary>
        public static Vector2Int Desktop
        {
            get
            {
                var d = Display.main;
                int w = d != null ? d.systemWidth : Screen.currentResolution.width;
                int h = d != null ? d.systemHeight : Screen.currentResolution.height;
                return new Vector2Int(Mathf.Max(w, 1), Mathf.Max(h, 1));
            }
        }

        public static bool Windowed
        {
            get
            {
                if (Application.isEditor) return s_editorWindowed;
                if (Asked) return s_askWindowed;
                return Screen.fullScreenMode == FullScreenMode.Windowed;
            }
        }

        /// <summary>The window's size while WINDOWED (the desktop's in fullscreen).</summary>
        public static Vector2Int WindowSize
        {
            get
            {
                if (Application.isEditor)
                {
                    if (s_editorSize.x <= 0) s_editorSize = WindowFor(Desktop.x, Desktop.y);
                    return s_editorWindowed ? s_editorSize : Desktop;
                }
                if (Asked) return s_askSize;
                return new Vector2Int(Screen.width, Screen.height);
            }
        }

        /// <summary>FULLSCREEN at the desktop's size, or WINDOWED at the largest whole size it holds.</summary>
        public static void SetWindowed(bool windowed)
        {
            var desk = Desktop;
            Apply(windowed ? WindowFor(desk.x, desk.y) : desk, windowed);
        }

        /// <summary>A new size for the window (only while WINDOWED; fullscreen is always the desktop's).</summary>
        public static void SetWindowSize(Vector2Int size)
        {
            if (!Windowed) return;
            Apply(size, true);
        }

        private static void Apply(Vector2Int size, bool windowed)
        {
            s_hasAsk = true;
            s_askWindowed = windowed;
            s_askSize = size;
            s_askFrame = Time.frameCount;
            if (Application.isEditor)
            {
                s_editorWindowed = windowed;
                if (windowed) s_editorSize = size;
            }
#if !UNITY_EDITOR
            Screen.SetResolution(size.x, size.y, windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
#endif
        }

        /// <summary>
        /// The frame pacing <see cref="PlayerOptions.FrameCap"/> asks for, set on the engine. Player builds only, as
        /// GameBootstrap's own lines were (2026-09-11): the editor and the PlayMode suite keep their pacing.
        /// </summary>
        public static void ApplyPacing()
        {
#if !UNITY_EDITOR
            var (vSync, target) = Pacing(PlayerOptions.FrameCap);
            QualitySettings.vSyncCount = vSync;
            Application.targetFrameRate = target;
#endif
        }
    }
}
