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

        private bool _over, _held;

        public void OnPointerDown(PointerEventData _) { _held = true; Apply(); }
        public void OnPointerUp(PointerEventData _) { _held = false; Apply(); }
        public void OnPointerEnter(PointerEventData _) { _over = true; Apply(); }
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
        }
    }
}
