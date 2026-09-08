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
    /// same hand at the same size (48x45, a 16x15 drawing at 3x). The hotspot is the
    /// fingertip. One static so every caller agrees on the state, and one <see cref="Step"/>
    /// a frame from the HUD, which is the only place that knows what the hand is holding.
    /// </summary>
    public static class CursorSkin
    {
        public enum State { Idle, Pressed, Grab }

        private static Texture2D _idle, _pressed, _grab;
        private static State _shown = (State)(-1);
        private static bool _tried;

        /// <summary>The fingertip, in texture pixels from the top-left: the finger points up
        /// from x 24..32 at rows 0..2, so the tip is the middle of that run.</summary>
        private static readonly Vector2 Hotspot = new Vector2(28f, 1f);

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
