using LastCall.Game;
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
    ///
    /// THE LARGE HAND (2026-09-26, the settings' POINTER row): the same three frames at twice the
    /// size, 64x64 - each texel of the 32x32 frame doubled, the fingertip with it. Windows shrinks a
    /// hardware cursor that size (the 48x45 lesson below), so the large hand is a SOFTWARE cursor:
    /// Unity draws it, it runs about a frame behind the mouse, and it lands in screenshots - which is
    /// why it is a choice and NORMAL stays the default (the PlayMode suite pins the default).
    /// </summary>
    public static class CursorSkin
    {
        public enum State { Idle, Pressed, Grab }

        private static Texture2D _idle, _pressed, _grab;
        private static Texture2D _idleLarge, _pressedLarge, _grabLarge;
        private static State _shown = (State)(-1);
        private static bool _shownLarge;
        private static bool _tried, _triedLarge;

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

        /// <summary>The large frames, made the first time LARGE is asked for (never, for most players).</summary>
        private static void LoadLargeOnce()
        {
            if (_triedLarge) return;
            _triedLarge = true;
            _idleLarge = Doubled(_idle);
            _pressedLarge = _pressed == _idle ? _idleLarge : Doubled(_pressed) ?? _idleLarge;
            _grabLarge = _grab == _idle ? _idleLarge : Doubled(_grab) ?? _idleLarge;
        }

        /// <summary>A frame at twice its size, every texel doubled (point-filtered, so the hand stays pixel art).
        /// Null when the drawing cannot be read back (an import without Read/Write): the hand then stays normal.</summary>
        private static Texture2D Doubled(Texture2D src)
        {
            if (src == null) return null;
            try
            {
                var px = src.GetPixels32();
                int w = src.width, h = src.height, w2 = w * 2;
                var big = new Color32[w2 * h * 2];
                for (int y = 0; y < h * 2; y++)
                    for (int x = 0; x < w2; x++)
                        big[y * w2 + x] = px[(y / 2) * w + x / 2];
                var tex = new Texture2D(w2, h * 2, TextureFormat.RGBA32, false)
                {
                    name = src.name + "_large",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                tex.SetPixels32(big);
                tex.Apply(false, false);
                return tex;
            }
            catch (UnityException)
            {
                return null;
            }
        }

        /// <summary>Shows the frame for <paramref name="state"/>; a no-op when it is already
        /// up, because Cursor.SetCursor every frame is a driver call every frame.</summary>
        public static void Step(State state)
        {
            LoadOnce();
            if (_idle == null) return;            // no art on disk: the system pointer stays
            bool large = PlayerOptions.Pointer == PlayerOptions.PointerSize.Large;
            if (large) { LoadLargeOnce(); large = _idleLarge != null; }
            if (state == _shown && large == _shownLarge) return;
            _shown = state;
            _shownLarge = large;
            if (large)
            {
                var big = state == State.Grab ? _grabLarge : state == State.Pressed ? _pressedLarge : _idleLarge;
                Cursor.SetCursor(big, Hotspot * 2f, CursorMode.ForceSoftware);
                return;
            }
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
            _shownLarge = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
