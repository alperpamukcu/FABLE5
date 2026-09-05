using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A control that answers the pointer by WARMING, not by moving (2026-08-14, the author:
    /// "markette mouse bir butonun üstüne gelince belli olsun o buton").
    ///
    /// <see cref="PressSink"/> is the house answer everywhere a control stands still: it
    /// lifts, blooms and warms together. The market tablet cannot use it. Its tiles live
    /// inside a ScrollRect and its tabs are re-laid on every rebuild — their height and
    /// colour are written per frame — so a component that moves a rect would spend its life
    /// arguing with the code that places it, and the argument would show as a jitter. Warmth
    /// has no position to fight over.
    ///
    /// It captures the face's colour ON ENTER rather than at construction, so a key that was
    /// repainted since (ORDERED grey, NO CASH, a tab that just became the live one) cools
    /// back to what it is NOW and not to what it was when it was dressed. Exit restores that
    /// colour exactly, so nothing this component does can survive it leaving.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoverWarm : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>The graphic that warms. Usually the plate the caption sits on.</summary>
        public Graphic Face;

        /// <summary>How far toward white, 0–1. The default is the smallest step that reads
        /// on the shop's green fascia, measured against it.</summary>
        public float Warm = 0.28f;

        /// <summary>How fast it gets there. Fast enough to feel like a response and slow
        /// enough not to strobe when the pointer crosses a row of tiles.</summary>
        public float Speed = 14f;

        private bool _over;
        private Color _cold;
        private bool _held;

        public void OnPointerEnter(PointerEventData e)
        {
            if (Face == null) return;
            if (!_held) { _cold = Face.color; _held = true; }
            _over = true;
        }

        public void OnPointerExit(PointerEventData e)
        {
            _over = false;
            if (Face != null && _held) Face.color = _cold;
            _held = false;
        }

        private void OnDisable()
        {
            // A tile can be torn down mid-hover on a rebuild; put the colour back first so
            // a pooled or re-shown graphic never comes up wearing the highlight.
            if (Face != null && _held) Face.color = _cold;
            _over = false; _held = false;
        }

        /// <summary>
        /// REPAINT THE RESTING COLOUR, hover or no hover (2026-09-04). Capturing on enter is
        /// only half the rule: a key that is repainted WHILE the pointer is on it would cool
        /// back to the colour it wore when the pointer arrived — which since the market's two
        /// foot keys became one is a key showing the shop's blue over a caption reading OPEN
        /// TOMORROW. Anything that repaints a warmed face has to say so here instead of
        /// writing <c>Face.color</c> behind this component's back.
        /// </summary>
        public void Repaint(Color cold)
        {
            if (Face == null) return;
            _cold = cold;
            if (!_over) Face.color = cold;   // hovered, LateUpdate warms it from the new cold
        }

        private void LateUpdate()
        {
            if (!_over || Face == null) return;
            var want = Color.Lerp(_cold, Color.white, Mathf.Clamp01(Warm));
            Face.color = Color.Lerp(Face.color, want,
                1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
        }
    }
}
