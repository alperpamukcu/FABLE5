using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// Sizes a piece of stage art so its pixels stay SQUARE (2026-07-29).
    ///
    /// The stage art used to be stretched to a rect that had nothing to do with its own
    /// proportions: the backdrop was a 592×336 image pulled across a 736×456 area — 1.24×
    /// across and 1.36× up, so every pixel came out a different width from its height and the
    /// whole room was 9% too tall. Pixel art cannot survive that; it is the one thing that has
    /// to be scaled by a single number in both axes.
    ///
    /// It scales by ONE factor. At the stage's own 16:9 reference that factor is exactly 1, so
    /// on the aspect the game is drawn for every art pixel lands on a whole screen pixel — ×4 at
    /// 1440p, measured; other aspects stay undistorted rather than pixel-exact.
    /// </summary>
    public sealed class StageArtFit : MonoBehaviour
    {
        /// <summary>The art's own pixel size — the size at which one texel is one unit.</summary>
        public Vector2 Native = new Vector2(640, 360);

        private RectTransform _rt;
        private RectTransform _parent;
        private Vector2 _lastParentSize = new Vector2(-1, -1);

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _parent = transform.parent as RectTransform;
            Apply();
        }

        private void OnEnable() => Apply();

        // A resized window has to re-fit, and the parent's rect is only trustworthy after a
        // layout pass — so this watches for the change rather than assuming one moment is right.
        // Two Vector2 compares a frame against getting the whole stage's proportions wrong.
        private void Update()
        {
            if (_parent != null && _parent.rect.size != _lastParentSize) Apply();
        }

        public void Apply()
        {
            if (_rt == null) _rt = (RectTransform)transform;
            if (_parent == null) _parent = transform.parent as RectTransform;
            if (_parent == null || Native.x <= 0f || Native.y <= 0f) return;

            var p = _parent.rect.size;
            _lastParentSize = p;

            // Cover: the larger of the two ratios, so the art fills the parent in both axes and
            // whatever overflows is cropped rather than squashed.
            _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
            float scale = Mathf.Max(p.x / Native.x, p.y / Native.y);
            _rt.sizeDelta = Native * scale;
            _rt.anchoredPosition = Vector2.zero;
        }
    }
}
