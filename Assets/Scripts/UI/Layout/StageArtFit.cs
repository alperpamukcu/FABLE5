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
        public enum Mode
        {
            /// <summary>Fill the parent, keeping the art's own shape — overflow is cropped.</summary>
            Cover,
            /// <summary>Span the parent's width, height following from it, hung from a line.</summary>
            WidthAligned,
        }

        public Mode Fit = Mode.Cover;

        /// <summary>The art's own pixel size — the size at which one texel is one unit.</summary>
        public Vector2 Native = new Vector2(640, 360);

        /// <summary>WidthAligned: where the pinned line must sit, in parent units from the bottom.</summary>
        public float RestLineY;

        /// <summary>WidthAligned: how far below the art's top that line is, at native size. For the
        /// bar it is the top surface — what a glass stands on.</summary>
        public float RestFromTop;

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

            // Cover takes the larger of the two ratios, so the art fills the parent in both
            // axes and whatever overflows is cropped rather than squashed. WidthAligned takes
            // the width alone and lets the height follow, which is what a bar running the whole
            // way across the frame needs.
            _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
            float scale = Fit == Mode.Cover
                ? Mathf.Max(p.x / Native.x, p.y / Native.y)
                : p.x / Native.x;
            var size = Native * scale;
            _rt.sizeDelta = size;

            // Cover sits centred; the bar hangs from its surface line, because that line is what
            // the rest of the stage is measured against — the glassware, the till and the seats
            // all stand on it.
            _rt.anchoredPosition = Fit == Mode.Cover
                ? Vector2.zero
                : new Vector2(0f, RestLineY - size.y * 0.5f + RestFromTop * scale - p.y * 0.5f);
        }
    }
}
