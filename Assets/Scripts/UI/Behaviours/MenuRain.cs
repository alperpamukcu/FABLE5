using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// RAIN FALLING OVER THE ESC MENU'S DOOR (2026-09-25, the author: "Gerekli arkaplan görsellerini icon görsellerini
    /// veya efektleri pixellabden üretebilirsin" - the effects are the code's). A RawImage over the picture, inside its
    /// window's mask, showing NightArt.RainTile at the picture's own 2x and moving it down one texel at a time on the
    /// unscaled clock - the menu is up while the night's clock is held, so scaled time would freeze it. Whole texels
    /// only, so the rain never stands between pixels; still under reduced motion.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class MenuRain : MonoBehaviour
    {
        /// <summary>Seconds a texel: 0.05 is 40 units a second at 2x, a steady rain rather than a storm.</summary>
        public float Step = 0.05f;

        private RawImage _img;
        private float _next;
        private int _at;

        private void OnEnable()
        {
            _img = GetComponent<RawImage>();
            Fit();
        }

        /// <summary>The tile at 2x across the rect: one texel is two units.</summary>
        private void Fit()
        {
            if (_img == null || _img.texture == null) return;
            var r = ((RectTransform)transform).rect;
            var uv = _img.uvRect;
            uv.width = r.width * 0.5f / _img.texture.width;
            uv.height = r.height * 0.5f / _img.texture.height;
            _img.uvRect = uv;
        }

        private void Update()
        {
            if (_img == null || _img.texture == null || Motion.Reduced) return;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Step;
            _at = (_at + 1) % _img.texture.height;
            var uv = _img.uvRect;
            uv.y = (float)_at / _img.texture.height;               // the window reads higher up the tile: the rain falls
            uv.width = ((RectTransform)transform).rect.width * 0.5f / _img.texture.width;
            uv.height = ((RectTransform)transform).rect.height * 0.5f / _img.texture.height;
            _img.uvRect = uv;
        }
    }
}
