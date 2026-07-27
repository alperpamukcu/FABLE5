using UnityEngine;
using UnityEngine.EventSystems;

namespace LastCall.DebugUI
{
    /// <summary>
    /// Gives a UI button a real press: while it is held, its face slides down into the plate
    /// and shrinks a hair, and it springs back on release. Driven per frame rather than snapped,
    /// so the button reads as being pushed in rather than just changing colour (2026-07-24).
    /// Put it on the clickable object and hand it the child that carries the label and icons.
    /// </summary>
    public sealed class PressSink : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public RectTransform Face;
        public float Depth = 6f;      // px the face travels down
        public float Squash = 0.03f;  // how much it shrinks while held
        public float Speed = 22f;     // approach rate

        private bool _held;
        private float _t;             // 0 = out, 1 = fully pressed
        private Vector2 _home;
        private bool _homeSet;

        public void OnPointerDown(PointerEventData _) => _held = true;
        public void OnPointerUp(PointerEventData _) => _held = false;
        public void OnPointerExit(PointerEventData _) => _held = false;

        private void LateUpdate()
        {
            if (Face == null) return;
            if (!_homeSet) { _home = Face.anchoredPosition; _homeSet = true; }

            float target = _held ? 1f : 0f;
            if (Mathf.Approximately(_t, target) && _t <= 0f) return;

            _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime * Speed);
            float e = _t * _t * (3f - 2f * _t);                 // smoothstep
            Face.anchoredPosition = _home + new Vector2(0f, -Depth * e);
            float k = 1f - Squash * e;
            Face.localScale = new Vector3(k, k, 1f);
        }
    }
}
