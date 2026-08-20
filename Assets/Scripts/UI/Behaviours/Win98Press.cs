using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The 98 press: a button of that era does not travel — it turns INSIDE OUT. On pointer
    /// down the face swaps to the inverted-bevel drawing and the caption steps one unit
    /// down-and-right; on release both come back. That one-unit step is the whole tell, and
    /// it is why this is not <see cref="PressSink"/>: PressSink moves a key through space,
    /// which is the bar's dialect, and the market's site speaks the desktop's.
    ///
    /// Used only by the market's 98 keys (GDD 16 §1 grew a named object for them,
    /// 2026-08-19). Hover stays HoverWarm's job — the market brightens, never lifts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Win98Press : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>The Image wearing ChromeArt.Win98Key(); swapped, never moved.</summary>
        public Image Face;
        /// <summary>What steps by one unit while held — the caption, usually. Optional.</summary>
        public RectTransform Caption;

        private Vector2 _home;
        private bool _held;

        public void OnPointerDown(PointerEventData e)
        {
            if (_held) return;
            _held = true;
            if (Face != null) Face.sprite = ChromeArt.Win98Key(down: true);
            if (Caption != null)
            {
                _home = Caption.anchoredPosition;
                Caption.anchoredPosition = _home + new Vector2(1f, -1f);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_held) return;
            _held = false;
            if (Face != null) Face.sprite = ChromeArt.Win98Key(down: false);
            if (Caption != null) Caption.anchoredPosition = _home;
        }

        private void OnDisable()
        {
            // A key disabled mid-press (the dialog closes under the pointer) must not come
            // back stuck inside out.
            if (!_held) return;
            _held = false;
            if (Face != null) Face.sprite = ChromeArt.Win98Key(down: false);
            if (Caption != null) Caption.anchoredPosition = _home;
        }
    }
}
