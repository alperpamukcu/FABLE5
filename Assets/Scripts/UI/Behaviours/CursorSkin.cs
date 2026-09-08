using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// THE HAND (2026-09-08, the author: "Hand3.png yeni cursorumuz ... basılı frame ve tutma
    /// taşıma frame'i de olmalı"). The system pointer is replaced by the author's drawn hand in
    /// three frames — idle, pressed, grabbing — which is the whole of what a pointer in this
    /// game has to say: where you are, that you are pressing, and that you are carrying
    /// something (a glass, the tin, the cloth, a bowl, a bottle on the bench).
    ///
    /// The frames are cut from the one drawing by Tools/ui_art_2026_09_08.py, so they are the
    /// same hand at the same size (32x32, the 16x15 drawing at 2x — a Windows hardware cursor's size). The hotspot is the
    /// fingertip. One static so every caller agrees on the state, and one <see cref="Step"/>
    /// a frame from the HUD, which is the only place that knows what the hand is holding.
    /// </summary>
    public static class CursorSkin
    {
        public enum State { Idle, Pressed, Grab }

        private static Texture2D _idle, _pressed, _grab;
        private static State _shown = (State)(-1);
        private static bool _tried;

        /// <summary>The fingertip, in texture pixels from the top-left. The author's own
        /// drawings (2026-09-08, Tools/cursor_src) point UP-LEFT, the tip in the top-left
        /// corner: cells (1..2, 0), so (3, 1) at 2x. The raycasters forgive a few pixels
        /// around it (<see cref="ForgivingRaycaster"/>), which is the "4px circle at the
        /// png's top-left corner" the author asked for.</summary>
        private static readonly Vector2 Hotspot = new Vector2(3f, 1f);

        private static void LoadOnce()
        {
            if (_tried) return;
            _tried = true;
            _idle = ItemArt.Load("cursor_hand")?.texture;
            _pressed = ItemArt.Load("cursor_hand_pressed")?.texture ?? _idle;
            _grab = ItemArt.Load("cursor_hand_grab")?.texture ?? _idle;
        }

        /// <summary>Shows the frame for <paramref name="state"/>; a no-op when it is already
        /// up, because Cursor.SetCursor every frame is a driver call every frame.</summary>
        public static void Step(State state)
        {
            LoadOnce();
            if (_idle == null) return;            // no art on disk: the system pointer stays
            if (state == _shown) return;
            _shown = state;
            var tex = state == State.Grab ? _grab : state == State.Pressed ? _pressed : _idle;
            // HARDWARE, at the hardware's size (2026-09-08, the author: "imlecin hedefi
            // parmağın ucunda olmalı, şu an ortayı hedefleyen bir hitbox'ı var"). A Windows
            // hardware cursor is 32x32; handed the 48x45 drawing, Auto scaled it and the
            // hotspot with it, so the click landed near the middle of the hand. A software
            // cursor kept the size but was drawn INTO every screenshot — the look tests
            // went red on a 48x45 hand — and the frames are 32x32 now (the 16x15 drawing
            // at 2x), so the hardware takes them as they are and the tip is the tip.
            Cursor.SetCursor(tex, Hotspot, CursorMode.Auto);
        }

        /// <summary>Hands the pointer back to the system (leaving play mode).</summary>
        public static void Reset()
        {
            _shown = (State)(-1);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
