using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// Gives a UI button a real press: while it is held, its face slides down into the plate
    /// and shrinks a hair, and it springs back on release. Driven per frame rather than snapped,
    /// so the button reads as being pushed in rather than just changing colour (2026-07-24).
    /// Put it on the clickable object and hand it the child that carries the label and icons.
    ///
    /// It also answers the pointer BEFORE the click (2026-07-31). Darkening on press told the
    /// player what they had already done; nothing told them what they could do, so a screen full
    /// of props read as a picture until they gambled a click on it. On hover the face lifts a
    /// little, grows a hair and warms — the same three things the press undoes, which is why they
    /// read as a pair: hovering brings the object toward you, pressing pushes it away.
    /// </summary>
    public sealed class PressSink : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
                                    IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform Face;
        public float Depth = 6f;      // px the face travels down while held
        public float Squash = 0.03f;  // how much it shrinks while held
        public float Speed = 22f;     // approach rate for the press

        /// <summary>px the face rises under the pointer. 0 turns the hover lift off.</summary>
        public float Lift = 3f;

        /// <summary>How much it grows under the pointer.</summary>
        public float Bloom = 0.035f;

        /// <summary>Approach rate for the hover. Slower than the press, which has to feel instant.</summary>
        public float HoverSpeed = 12f;

        /// <summary>Optional: warmed toward white under the pointer.</summary>
        public Graphic Tint;

        /// <summary>How far <see cref="Tint"/> travels toward white, 0–1.</summary>
        public float Warm = 0.22f;

        private bool _held, _over;
        private float _t;             // 0 = out, 1 = fully pressed
        private float _h;             // 0 = cold, 1 = fully hovered
        private Vector2 _home;
        private bool _homeSet;
        private Color _cold;
        private bool _coldSet;

        public void OnPointerDown(PointerEventData _) => _held = true;
        public void OnPointerUp(PointerEventData _) => _held = false;

        public void OnPointerEnter(PointerEventData _) => _over = true;

        public void OnPointerExit(PointerEventData _)
        {
            _over = false;
            _held = false;             // a drag that wanders off the button is not a press on it
        }

        private void LateUpdate()
        {
            if (Face == null) return;
            if (!_homeSet) { _home = Face.anchoredPosition; _homeSet = true; }

            float pressed = _held ? 1f : 0f;
            float hovered = _over ? 1f : 0f;
            // Resting and asked for nothing: leave the transform alone, so a button the layout
            // moves under our feet is not fought over every frame — and TAKE THE NEW HOME with
            // us while we are down there (2026-08-25). The empties left on the counter ride the
            // cellar's drawer now (TycoonHud.RefreshDirtyGlasses), and a home cached the first
            // frame one was drawn is a glass that jumps back down the bar the moment a pointer
            // touches it. Free: this is the branch that was already doing nothing.
            if (Mathf.Approximately(_t, pressed) && Mathf.Approximately(_h, hovered)
                && _t <= 0f && _h <= 0f) { _home = Face.anchoredPosition; return; }

            _t = Mathf.MoveTowards(_t, pressed, Time.unscaledDeltaTime * Speed);
            _h = Mathf.MoveTowards(_h, hovered, Time.unscaledDeltaTime * HoverSpeed);

            float e = _t * _t * (3f - 2f * _t);               // smoothstep
            float g = _h * _h * (3f - 2f * _h);

            Face.anchoredPosition = _home + new Vector2(0f, Lift * g - Depth * e);
            float k = (1f + Bloom * g) * (1f - Squash * e);
            Face.localScale = new Vector3(k, k, 1f);

            if (Tint == null) return;
            if (!_coldSet) { _cold = Tint.color; _coldSet = true; }
            // Only the hover warms. A press is already saying something with its travel, and
            // warming that too made the press read as a second, brighter hover.
            Tint.color = Color.Lerp(_cold, new Color(1f, 1f, 1f, _cold.a), Warm * g * (1f - e));
        }
    }
}
