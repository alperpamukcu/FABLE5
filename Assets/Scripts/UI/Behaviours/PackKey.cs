using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A key drawn from the author's pack (2026-09-15, MenuPack / KeyCaps): the pointer swaps its plate between the
    /// three drawings the pack ships — resting, lit under the pointer, pressed — and tints its glyph between the
    /// pack's two inks. The pressed drawing carries its own travel (the rim a pixel lower, the shadow a row thinner),
    /// so a PressSink beside this one keeps its Depth small or at zero and lends only the hover lift.
    /// <see cref="Held"/> lets the owner press the key from outside — the CONTROLS page holds a cap down while the
    /// real key under the player's finger is.
    /// </summary>
    public sealed class PackKey : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
                                  IPointerEnterHandler, IPointerExitHandler
    {
        public Image Plate;
        public Sprite Rest, Lit, Pressed;

        /// <summary>Optional: a glyph tinted between the two inks.</summary>
        public Image Glyph;
        public Color GlyphRest = Color.white, GlyphLit = Color.white;

        /// <summary>Pressed from outside, whatever the pointer does.</summary>
        public bool Held;

        /// <summary>
        /// Optional: the key's SURFACE (2026-09-25, the ESC keys - ChromeArt.KeySurfaceTile): tinted with the plate's
        /// state - its rest ink, or the lit ink while the plate is the hover blue - and dropped by <see cref="SurfaceDrop"/>
        /// while the plate shows its pressed drawing, whose face is drawn that much lower. Multiplied by the plate's own
        /// colour, so a dimmed key dims its surface with it. Keys without one are untouched.
        /// </summary>
        public Image Surface;
        public Color SurfaceRest = Color.white, SurfaceLit = Color.white;
        public float SurfaceDrop = 2f;
        private Vector2 _surfaceHome;
        private bool _surfaceHomeSet;

        private bool _over, _held;

        public void OnPointerDown(PointerEventData _) { _held = true; Apply(); }
        public void OnPointerUp(PointerEventData _) { _held = false; Apply(); }
        public void OnPointerEnter(PointerEventData _)
        {
            _over = true;
            Apply();
            // The pack's keys tick under the pointer like the room's props (HoverGlow), on the same short gap so a
            // sweep down a column is one run of ticks and not a buzz (2026-09-25).
            if (Time.unscaledTime - _lastTick < TickGap) return;
            _lastTick = Time.unscaledTime;
            Sfx.Play("hover", 0.14f);
        }

        private static float _lastTick = -1f;
        private const float TickGap = 0.09f;
        public void OnPointerExit(PointerEventData _) { _over = false; _held = false; Apply(); }

        private void OnEnable() { _over = false; _held = false; Apply(); }   // a key shown again is at rest
        private void OnDisable() { _over = false; _held = false; Apply(); }

        /// <summary>New drawings for a key whose tone changes (a tab lit, a flag chosen).</summary>
        public void Refit(Sprite rest, Sprite lit, Sprite pressed, Color glyphRest, Color glyphLit)
        {
            Rest = rest; Lit = lit; Pressed = pressed;
            GlyphRest = glyphRest; GlyphLit = glyphLit;
            Apply();
        }

        public void Apply()
        {
            bool down = _held || Held;
            bool warm = _over || down;
            if (Plate != null)
            {
                var s = down ? Pressed : _over ? Lit : Rest;
                if (s == null) s = Rest;
                if (s != null && Plate.sprite != s) Plate.sprite = s;
            }
            if (Glyph != null) Glyph.color = warm ? GlyphLit : GlyphRest;
            if (Surface != null)
            {
                var srt = Surface.rectTransform;
                if (!_surfaceHomeSet) { _surfaceHome = srt.anchoredPosition; _surfaceHomeSet = true; }
                srt.anchoredPosition = _surfaceHome + new Vector2(0f, down ? -SurfaceDrop : 0f);
                var ink = !down && _over ? SurfaceLit : SurfaceRest;
                var k = Plate != null ? Plate.color : Color.white;
                Surface.color = new Color(ink.r * k.r, ink.g * k.g, ink.b * k.b, ink.a * k.a);
            }
        }
    }
}
