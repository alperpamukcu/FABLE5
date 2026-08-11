using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// A field of FIXED size inside a canvas, so what is drawn stops depending on the
    /// shape of the window.
    ///
    /// The author, 2026-08-11: "ekran boyutu oynayınca nesnelerin yerleri kayıyor, ölçünün
    /// hep sabit olması lazım." Every canvas here scales off the HEIGHT (match 1), which
    /// pins the vertical at 720 units and lets the WIDTH be 720×aspect — so a wider window
    /// is a wider bar. That was on purpose and it is also the fault: the back bar packs its
    /// rows by the width it is given, so dragging the window edge repacked the shelves,
    /// resized every bottle, and moved everything that was measured off an edge.
    ///
    /// A fixed field is the usual answer. The layout gets one size forever — the design
    /// size — and the frame is scaled to fit whatever the window happens to be. Wider than
    /// 16:9 and the scale stays 1 with the room simply ending; narrower and the whole field
    /// shrinks together, which keeps the composition and loses only sharpness, in the one
    /// case where something has to give.
    ///
    /// It is deliberately NOT a stretch: nothing inside may be anchored to a screen edge,
    /// because the point is that there are no screen edges any more, only field edges.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesignFrame : MonoBehaviour
    {
        [SerializeField] private Vector2 _design = new Vector2(1280f, 720f);

        private RectTransform _self, _host;
        private Vector2 _lastHost = new Vector2(-1f, -1f);
        private Vector2 _lastDesign = new Vector2(-1f, -1f);
        private Vector2 _lastView = new Vector2(-1f, -1f);

        /// <summary>The cropped viewport in screen pixels, or zero when there is no camera
        /// to match — a menu scene with no room behind it falls back to a plain fit.</summary>
        private static Vector2 Viewport()
        {
            var cam = Camera.main;
            return cam != null ? new Vector2(cam.pixelRect.width, cam.pixelRect.height)
                               : Vector2.zero;
        }

        /// <summary>Puts a fixed <paramref name="design"/>-sized field inside
        /// <paramref name="host"/> and hands it back to build in.</summary>
        public static RectTransform Wrap(RectTransform host, Vector2 design)
        {
            var go = new GameObject("DesignFrame", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(host, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = design;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            var frame = go.AddComponent<DesignFrame>();
            frame._design = design;
            frame.Fit();
            return rt;
        }

        private void Awake()
        {
            _self = (RectTransform)transform;
            _host = transform.parent as RectTransform;
        }

        // The frame's OWN rect never changes, so it cannot be told by
        // OnRectTransformDimensionsChange that anything happened — it is the host that
        // resizes. Watching the host for a change is both the cheapest way to hear about
        // it and the only one that survives a window drag, which fires continuously.
        private void LateUpdate() => Fit();

        // A disabled frame gets no LateUpdate, and the service flow spends most of its life
        // disabled: measured at build time it had cached the canvas rect in SCREEN pixels —
        // the CanvasScaler had not run yet — and sat at 1.33 until something woke it. So the
        // cache is dropped whenever the frame comes back, and the first frame it is seen on
        // is already the right size rather than one frame late.
        private void OnEnable()
        {
            _lastHost = new Vector2(-1f, -1f);
            Fit();
        }

        private void Fit()
        {
            if (_self == null) _self = (RectTransform)transform;
            if (_host == null) _host = transform.parent as RectTransform;
            if (_host == null) return;

            // The design is part of the cache key, not just the host size. AddComponent runs
            // OnEnable *inside* the call, before Wrap can hand over the design it was asked
            // for — so the first Fit always runs on the default 1280x720, and a cache keyed
            // on the host alone would then refuse to hear that the caller wanted 640x360.
            // Every stage canvas came out at twice its reference that way.
            var size = _host.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            var view = Viewport();
            if (size == _lastHost && _design == _lastDesign && view == _lastView) return;
            _lastHost = size;
            _lastDesign = _design;
            _lastView = view;

            // Fit to the CAMERA's viewport, not to the canvas. The room is windowboxed by a
            // PixelPerfectCamera, which snaps to a whole pixel scale — a 1292px window came
            // back as a 1280px viewport — while an overlay canvas always covers the window
            // entire. Min-fitting the canvas would leave the UI 0.9% larger than the room
            // standing behind it: six pixels of drift at each edge, which is exactly the
            // drift this class exists to remove. Matched to the viewport, the till sits on
            // the end of the counter at every window size.
            float pxPerUnit = Screen.width / size.x;
            float k = view.x > 0f && pxPerUnit > 0f
                ? (view.x / pxPerUnit) / _design.x
                : Mathf.Min(size.x / _design.x, size.y / _design.y);
            if (k <= 0f || float.IsNaN(k) || float.IsInfinity(k)) k = 1f;

            _self.sizeDelta = _design;
            _self.localScale = new Vector3(k, k, 1f);
        }
    }
}
