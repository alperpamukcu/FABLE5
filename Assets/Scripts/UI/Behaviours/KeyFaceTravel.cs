using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// Moves a key's FACE — its mark and its word — with the cap under it.
    ///
    /// A <see cref="ChromeArt.KeyCap"/> is two drawings of one cap at two heights in a
    /// socket, swapped by the Button on press. That works for the plastic and not at all
    /// for what is written on it: a label parented to the key stays exactly where it was
    /// while the cap drops six pixels out from under it, so the writing appears to float
    /// off the surface it is supposed to be moulded into. It reads worse than no press at
    /// all, because the eye catches the two halves disagreeing.
    ///
    /// So the face is its own rect and this carries it the same distance the cap travels.
    /// It takes the pointer itself rather than asking the Button, because a Selectable
    /// keeps its own state protected — and it treats POINTER EXIT as a release, which is
    /// the case worth being careful about: a pointer that goes down on the key and drags
    /// off never sends an up, and a face left sunk under a raised cap is precisely the
    /// disagreement this exists to prevent.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class KeyFaceTravel : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Button Button;

        /// <summary>The face's bottom offset while the key stands, and while it is
        /// pressed. Both negative: the face hangs from the key's top edge.</summary>
        public float Up = -4f, Down = -10f;

        /// <summary>How fast the face catches the cap. Quick enough to look welded to it,
        /// not instant — an instant jump on a key that itself eases reads as a stutter.</summary>
        public float Speed = 34f;

        private RectTransform _rt;
        private float _at;
        private bool _down;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _at = Up;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Button == null || Button.IsInteractable()) _down = true;
        }

        public void OnPointerUp(PointerEventData e) => _down = false;
        public void OnPointerExit(PointerEventData e) => _down = false;

        private void Update()
        {
            float want = _down ? Down : Up;
            _at = Mathf.Lerp(_at, want, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
            var min = _rt.offsetMin;
            var max = _rt.offsetMax;
            _rt.offsetMin = new Vector2(min.x, 0f);
            _rt.offsetMax = new Vector2(max.x, _at);
        }
    }
}
