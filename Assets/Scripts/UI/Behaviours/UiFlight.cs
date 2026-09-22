using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// A THING THAT FLIES ACROSS THE MARKET (2026-09-22, the author's eighth list: "market sahnesinde geçişler sepete
    /// eklemeler çıkarmalar satın almalar, ekran geçişleri, pop-up açılmaları vs. bunlar için animasyonlar olmalı").
    /// The market is rebuilt whole on every change, so what moves between two builds cannot be a piece of either: it is
    /// a copy on the effects layer above them, carried from where the old picture was to where the new one is, on a
    /// small arc, shrinking and fading as asked, and gone when it lands - with <see cref="Done"/> called then, so what
    /// it flew to can answer (a chip popping in as its bottle arrives).
    /// </summary>
    public sealed class UiFlight : MonoBehaviour
    {
        public Vector2 From, To;
        public float Seconds = 0.42f, Delay;
        public float ScaleFrom = 1f, ScaleTo = 1f, AlphaFrom = 1f, AlphaTo = 1f;
        /// <summary>How high the path bows over the straight line, in units.</summary>
        public float Arc = 36f;
        public System.Action Done;

        private float _t;
        private CanvasGroup _group;
        private RectTransform _rt;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();   // not ??: the editor hands back a fake null
            _group.blocksRaycasts = false;
            _group.interactable = false;
            Apply(0f);
        }

        private void LateUpdate()
        {
            _t += Time.unscaledDeltaTime;
            if (Motion.Reduced) { Land(); return; }
            float k = Mathf.Clamp01((_t - Delay) / Mathf.Max(0.01f, Seconds));
            Apply(k);
            if (k >= 1f) Land();
        }

        private void Apply(float k)
        {
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            _rt.anchoredPosition = Vector2.LerpUnclamped(From, To, ease) + Vector2.up * (Arc * Mathf.Sin(Mathf.PI * k));
            float s = Mathf.Lerp(ScaleFrom, ScaleTo, ease);
            _rt.localScale = new Vector3(s, s, 1f);
            _group.alpha = Mathf.Lerp(AlphaFrom, AlphaTo, k);
        }

        private void Land()
        {
            var done = Done;
            Done = null;
            Destroy(gameObject);
            done?.Invoke();
        }
    }

    /// <summary>A page coming up: faded in from nothing and slid the last few units into place (a tab opening).</summary>
    public sealed class UiFadeIn : MonoBehaviour
    {
        public float Seconds = 0.22f;
        public Vector2 Offset = new Vector2(0f, -10f);

        private float _t;
        private Vector2 _home;
        private CanvasGroup _group;
        private RectTransform _rt;

        public void Play()
        {
            _rt = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();   // not ??: the editor hands back a fake null
            if (_t < 0f || _t == 0f) _home = _rt.anchoredPosition;
            _t = 0.0001f;
            if (Motion.Reduced) { _t = -1f; _group.alpha = 1f; return; }
            _group.alpha = 0f;
            _rt.anchoredPosition = _home + Offset;
            enabled = true;
        }

        private void LateUpdate()
        {
            if (_t <= 0f) { enabled = false; return; }
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / Mathf.Max(0.01f, Seconds));
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            _group.alpha = ease;
            _rt.anchoredPosition = _home + Offset * (1f - ease);
            if (k >= 1f) { _t = -1f; _rt.anchoredPosition = _home; enabled = false; }
        }
    }

    /// <summary>A thing answering: a quick swell past its size and back (a balance paid from, a key pressed).</summary>
    public sealed class UiPunch : MonoBehaviour
    {
        public float Seconds = 0.28f, Amount = 0.10f;
        private float _t = -1f;

        public void Play()
        {
            if (Motion.Reduced) return;
            _t = 0f;
            enabled = true;
        }

        private void LateUpdate()
        {
            if (_t < 0f) { enabled = false; return; }
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / Mathf.Max(0.01f, Seconds));
            float s = 1f + Amount * Mathf.Sin(Mathf.PI * k) * (1f - k * 0.3f);
            transform.localScale = new Vector3(s, s, 1f);
            if (k >= 1f) { _t = -1f; transform.localScale = Vector3.one; enabled = false; }
        }

        private void OnDisable()
        {
            if (_t >= 0f) { _t = -1f; transform.localScale = Vector3.one; }
        }
    }
}
