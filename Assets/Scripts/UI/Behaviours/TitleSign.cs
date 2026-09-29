using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE TITLE SIGN (2026-09-29, the author: "Giriş ekranındaki malibu club logosu ışık hareketi olmalı ve görselin
    /// kullanıldığı yere uygun boyda üretilmesi gerekiyor upscale yapma"). The Malibu Club lockup as a lit neon sign,
    /// DRAWN AT THE SIZE IT IS SHOWN: the art ships as seven MAPS, one per screen scale (Resources/Logo/
    /// logo_{set}_map.bytes - the PNG's own bytes under a .bytes name, so no texture importer can resize, compress or
    /// filter them; the 2048 cap alone would have shrunk the x4 set), each drawn on its own grid from the store logo's
    /// full-size masters, so the capsules and the game still show one mark. The sign takes the largest set not bigger
    /// than the canvas's live factor and shows it 1:1 - Point, its corner on a whole screen pixel - a few percent
    /// under the footprint between the steps and NEVER scaled up. The one exception is a factor under 1 (a 640x360
    /// window), where the x1 set is halved: it only ever goes down.
    ///
    /// The light is <see cref="TitleSignLight"/>'s, painted into this one texture: the letters strike in reading
    /// order when the door comes up from the boot or a run (<see cref="Ignite"/>), then a bead runs along each line,
    /// the second B stutters, the coupe's liquid glints and the outer glow breathes. REDUCED MOTION: lit and still
    /// from the first frame (the "menu" look test photographs that). FLASHES off: the strikes are single dark-to-lit
    /// steps and the idle holds still - no bead, no glint, no stutter, no breath.
    ///
    /// It fades WITH the door's panel (the critic: a sign that ignored the fade popped dark tubes over the game
    /// over's papers for 0.6 s), and it stays invisible until its first paint - a RawImage with no texture is a
    /// white box. The texture is kept while the door only steps aside for its settings, and let go when the door
    /// closes (<see cref="Release"/>, from HideMainMenu), so coming back from a page costs no decode.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class TitleSign : MonoBehaviour
    {
        /// <summary>The footprint in canvas units: the x1 set's own size, which is 1:1 at 1280x720.</summary>
        public static readonly Vector2 Footprint = new Vector2(560f, 212f);

        /// <summary>Largest first. A set's name is its file's (no dots: Resources paths read one as an extension).</summary>
        private static readonly (string Name, float Scale)[] Sets =
        {
            ("x4", 4f), ("x3", 3f), ("x2_5", 2.5f), ("x2", 2f), ("x1_5", 1.5f), ("x1_25", 1.25f), ("x1", 1f),
        };

        private static string MapPath(string set) => "Logo/logo_" + set + "_map";

        /// <summary>Whether the maps shipped. A build without them keeps the old worded title.</summary>
        public static bool HasArt
        {
            get
            {
                var probe = Resources.Load<TextAsset>(MapPath("x1"));
                if (probe == null) return false;
                Resources.UnloadAsset(probe);
                return true;
            }
        }

        private RawImage _img;
        private RectTransform _rt;
        private Canvas _root;
        private Texture2D _tex;
        private TitleSignLight _light;
        private string _set;
        private float _factor = -1f;
        private Vector2 _home;
        private float _litAt;
        private int _lastN = -1;
        private readonly Vector3[] _corners = new Vector3[4];

        /// <summary>Build the sign under <paramref name="parent"/>, centred on <paramref name="centre"/> (canvas
        /// units, the parent's centre anchor). It never animates its position: the pixel snap is measured off the
        /// place it was built at.</summary>
        public static TitleSign Build(RectTransform parent, string name, Vector2 centre)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Footprint;
            rt.anchoredPosition = centre;
            var img = go.GetComponent<RawImage>();
            img.raycastTarget = false;
            img.enabled = false;                         // nothing painted yet: an empty RawImage is a white box
            var sign = go.AddComponent<TitleSign>();
            sign._home = centre;
            UiAuditExempt.Mark(rt, "the title sign: a map drawn per screen scale, one texel per screen pixel (TitleSign)");
            return sign;
        }

        /// <summary>Light the sign from dark: the door came up from the boot or from a run. Coming back from one of
        /// the door's own pages does not call this - the sign is already lit.</summary>
        public void Ignite()
        {
            _litAt = Time.unscaledTime;
            _lastN = -1;
            if (_light != null) _light.Restart();
        }

        /// <summary>Let the texture go (the door closed). The next showing decodes the set again.</summary>
        public void Release()
        {
            if (_tex != null) Destroy(_tex);
            _tex = null;
            _light = null;
            _set = null;
            _factor = -1f;
            if (_img != null) { _img.texture = null; _img.enabled = false; }
        }

        private void Awake()
        {
            _img = GetComponent<RawImage>();
            _rt = (RectTransform)transform;
            _litAt = Time.unscaledTime;
        }

        private void OnEnable()
        {
            var c = GetComponentInParent<Canvas>();
            _root = c != null ? c.rootCanvas : null;
            _factor = -1f;          // re-fit: the window may have changed while the door was away
        }

        private void OnDestroy() => Release();

        private void LateUpdate()
        {
            float f = _root != null ? _root.scaleFactor : 1f;
            if (f <= 0f) return;
            if (!Mathf.Approximately(f, _factor)) Fit(f);
            else if (_tex != null) Snap();              // the field may have moved half a pixel under the sign
            if (_light == null) return;
            int n = Mathf.FloorToInt((Time.unscaledTime - _litAt) * TitleSignLight.Fps);
            if (n < 0) n = 0;
            if (n == _lastN) return;
            _lastN = n;
            if (_light.Step(n, Motion.Reduced, Motion.NoFlashes))
            {
                _tex.SetPixelData(_light.Pixels, 0);
                _tex.Apply(false, false);
                if (!_img.enabled) _img.enabled = true;   // the first paint is up
            }
        }

        /// <summary>Pick the set for this factor, size the rect so one texel is one screen pixel, snap it to the
        /// pixel grid.</summary>
        private void Fit(float factor)
        {
            _factor = factor;
            string want = "x1";
            foreach (var s in Sets)
                if (s.Scale <= factor + 0.001f) { want = s.Name; break; }
            if (want != _set) Load(want);
            if (_tex == null) return;
            // Under 1 (a 640x360 window) the x1 set stands at the footprint and the canvas halves it: the one case
            // that filters, and only ever down.
            bool whole = factor + 0.001f >= 1f;
            _tex.filterMode = whole ? FilterMode.Point : FilterMode.Bilinear;
            _rt.sizeDelta = new Vector2(_tex.width, _tex.height) / (whole ? factor : 1f);
            _rt.anchoredPosition = _home;
            Snap();
        }

        /// <summary>Stand the rect's lower-left corner on a whole screen pixel, measured from its home so repeated
        /// snaps never walk it away. An overlay canvas's world units are screen pixels.</summary>
        private void Snap()
        {
            if (_factor < 1f - 0.001f) return;
            _rt.GetWorldCorners(_corners);
            var cam = _root != null && _root.renderMode != RenderMode.ScreenSpaceOverlay ? _root.worldCamera : null;
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
            Vector2 moved = (_rt.anchoredPosition - _home) * _factor;   // screen pixels the last snap moved it
            Vector2 home = p - moved;
            var want = new Vector2(Mathf.Round(home.x) - home.x, Mathf.Round(home.y) - home.y);
            if ((want - moved).sqrMagnitude > 1e-4f) _rt.anchoredPosition = _home + want / _factor;
        }

        private void Load(string set)
        {
            var asset = Resources.Load<TextAsset>(MapPath(set));
            if (asset == null) { Debug.LogError("TitleSign: no map for " + set); return; }
            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool ok = tmp.LoadImage(asset.bytes, false);
            Resources.UnloadAsset(asset);
            if (!ok) { Destroy(tmp); Debug.LogError("TitleSign: the " + set + " map did not decode"); return; }
            int w = tmp.width, h = tmp.height;
            var px = tmp.GetPixels32();         // bottom row first
            Destroy(tmp);
            var cls = new byte[w * h];
            var grp = new byte[w * h];
            var ph = new byte[w * h];
            for (int i = 0; i < px.Length; i++) { cls[i] = px[i].r; grp[i] = px[i].g; ph[i] = px[i].b; }
            _light = new TitleSignLight(w, h, cls, grp, ph, true, Token);
            if (_tex != null) Destroy(_tex);
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "TitleSign " + set, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            _img.enabled = false;                // until this texture's first paint
            _img.texture = _tex;
            _set = set;
            _lastN = -1;                         // paint the current frame whole on the new texture
        }

        private static uint Token(TitleSignLight.Ramp ramp, int step)
        {
            Color32 c = (ramp == TitleSignLight.Ramp.Magenta ? UITheme.Magenta
                       : ramp == TitleSignLight.Ramp.Cyan ? UITheme.Cyan : UITheme.Cream)[step];
            return c.r | ((uint)c.g << 8) | ((uint)c.b << 16);
        }
    }
}
