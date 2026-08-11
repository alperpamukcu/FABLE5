using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The safe frame: a field of FIXED size, centred, drawn at the same whole-pixel scale
    /// as the room behind it, holding everything the player reads or touches.
    ///
    /// This is how a pixel-art game handles a window it did not choose (the author,
    /// 2026-08-11: "gerçekteki 2d oyunlar nasıl bir yöntem izliyorsa öyle yapalım"), and it
    /// is three rules that only work together:
    ///
    ///   1. ONE SCALE, AND IT IS A WHOLE NUMBER. The PixelPerfectCamera snaps the world to
    ///      an integer zoom — at 1052px of window height it draws 2 screen pixels per stage
    ///      unit and shows 526 units of room. The canvases are pinned to that same number
    ///      instead of scaling smoothly off the height, which is where this went wrong: the
    ///      world was drawing at 2x while the UI drew at 2.92x, so the glasses, the till and
    ///      the bin could not help sliding against the counter they stand on. A pixel is a
    ///      pixel everywhere now, and it is square.
    ///   2. SPARE ROOM IS ROOM. Whatever the window has left over shows more bar — the
    ///      backdrop, the room picture and the counter all bleed out to fill it. Black bars
    ///      are what you get for boxing the camera in, and the author rejected them on
    ///      sight: a wide monitor should buy a wider bar.
    ///   3. THE FIELD DOES NOT MOVE. Everything laid out in here is centred and never
    ///      rescales or repacks, so nothing drifts against the room at any window size.
    ///
    /// Narrower than the design and the field's edges crop, which is the trade the author
    /// asked for by name — a very narrow window loses a little of the game rather than
    /// shrinking all of it. Anything that must survive that belongs nearer the middle;
    /// that is what a safe frame is for.
    ///
    /// Nothing inside may anchor to a screen edge, because there are no screen edges in
    /// here — only field edges, which is the whole point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesignFrame : MonoBehaviour
    {
        /// <summary>The size the ROOM is authored at, in stage units. Every scale in the game
        /// is expressed against it, which is what keeps them all in step.</summary>
        public const float StageWidth = 640f, StageHeight = 360f;

        /// <summary>
        /// How much the whole scene is magnified to cover this window — ONE number, shared by
        /// the room and by everything standing in it.
        ///
        /// This is the answer to the last and worst version of the drift (2026-08-11, the
        /// author: "ekran boyutu değiştikçe arkaplandaki konumları hareket ediyor... bir tek
        /// bardaklar doğru kalıyor"). The room was cover-fitted to the window while the props
        /// were pinned to a fixed pixel size, so the picture grew and the things standing on
        /// it did not — and the glasses were the only things that held, because they alone
        /// were computed from the counter's live fit rather than from a constant.
        ///
        /// So there is no fixed pixel size any more. The scene is one picture: it is scaled
        /// until it covers the window, everything in it is scaled by the same amount, and
        /// what does not fit is CROPPED — a wide window loses a little off the top and
        /// bottom, a narrow one a little off the sides, which is the trade the author asked
        /// for. Nothing can drift against anything else, because nothing has its own scale.
        /// </summary>
        public static float SceneScale
        {
            get
            {
                var cam = Camera.main;
                if (cam == null || !cam.orthographic || cam.orthographicSize <= 0f) return 1f;
                float visibleW = cam.orthographicSize * 2f * cam.aspect;
                return Mathf.Max(visibleW / StageWidth, 1f);
            }
        }

        [SerializeField] private Vector2 _design = new Vector2(1280f, 720f);

        private RectTransform _self;
        private CanvasScaler _scaler;
        private float _lastFactor = -1f;

        /// <summary>Puts a fixed <paramref name="design"/>-sized field inside
        /// <paramref name="host"/> and hands it back to build in.</summary>
        public static RectTransform Wrap(RectTransform host, Vector2 design)
        {
            var go = new GameObject("SafeFrame", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(host, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.sizeDelta = design;
            var frame = go.AddComponent<DesignFrame>();
            frame._design = design;
            frame.Fit();
            return rt;
        }

        private void Awake()
        {
            _self = (RectTransform)transform;
            _scaler = GetComponentInParent<CanvasScaler>();
        }

        private void OnEnable() { _lastFactor = -1f; Fit(); }

        private void LateUpdate() => Fit();

        /// <summary>Screen pixels per stage unit: the camera's own mapping, magnified by the
        /// scene scale, so a canvas unit and a world unit are the same length on screen.</summary>
        private static float PixelsPerStageUnit()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic || cam.orthographicSize <= 0f) return 0f;
            return Screen.height / (cam.orthographicSize * 2f) * SceneScale;
        }

        private void Fit()
        {
            if (_self == null) _self = (RectTransform)transform;
            if (_scaler == null) _scaler = GetComponentInParent<CanvasScaler>();

            // The size is a constant; holding it costs nothing and stops a stray layout
            // group or stretch from quietly turning the safe frame back into a
            // window-shaped one.
            if (_self.sizeDelta != _design) _self.sizeDelta = _design;
            if (_self.localScale != Vector3.one) _self.localScale = Vector3.one;

            if (_scaler == null) return;
            float px = PixelsPerStageUnit();
            if (px <= 0f) return;
            // A canvas authored at 720 for a 360-unit room draws half a stage unit per
            // canvas unit, so it wants half the pixels. Constant pixel size, not
            // scale-with-height: the number has to be the camera's, not the window's.
            float factor = px * StageHeight / _design.y;
            if (Mathf.Approximately(factor, _lastFactor)) return;
            _lastFactor = factor;
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _scaler.scaleFactor = factor;
        }
    }
}
