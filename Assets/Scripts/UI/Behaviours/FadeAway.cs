using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// A thing that says its piece and goes (2026-09-28, the refusal plate by the pointer): shown by
    /// <see cref="Hold"/>, it pops in, holds for its seconds on the unscaled clock (a paused night still reads it),
    /// fades, and switches itself off. Reduced motion skips the pop and the fade; the hold stays.
    /// </summary>
    public sealed class FadeAway : MonoBehaviour
    {
        private CanvasGroup _group;
        private float _shownAt, _hold;
        private const float Pop = 0.10f, Fade = 0.25f;

        public void Hold(float seconds)
        {
            if (_group == null) _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _shownAt = Time.unscaledTime;
            _hold = Mathf.Max(0.5f, seconds);
            gameObject.SetActive(true);
            Step();
        }

        private void Update() => Step();

        private void Step()
        {
            if (_group == null) return;
            float t = Time.unscaledTime - _shownAt;
            if (Motion.Reduced)
            {
                _group.alpha = 1f;
                transform.localScale = Vector3.one;
                if (t >= _hold) gameObject.SetActive(false);
                return;
            }
            float end = _hold + Fade;
            if (t >= end)
            {
                gameObject.SetActive(false);
                return;
            }
            float pop = Mathf.Clamp01(t / Pop);
            transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, 1f - (1f - pop) * (1f - pop));
            _group.alpha = t <= _hold ? 1f : 1f - (t - _hold) / Fade;
        }
    }
}
