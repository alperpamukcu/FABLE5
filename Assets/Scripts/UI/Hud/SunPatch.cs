using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastCall.UI
{
    /// <summary>
    /// A PATCH OF SUN ON ONE SURFACE (2026-09-22, the author's eighth list: "Işık hüzmesi zeminin üstündeyse zeminin
    /// üstünde olduğu gibi tepki vermeli anlık, tavandaysa anlık tavandayken nasılsa öyle tepki vermeli").
    ///
    /// The window's light is one walk across three surfaces - the boards, the back wall, the ceiling - and each
    /// surface has its own drawing of the four panes (foreshortened on the boards, tall on the plaster, leaning the
    /// other way overhead). They used to be CROSS-FADED where the walk crossed a seam, so for a hand's width either
    /// side the room showed two half-strength patches at once, neither of them the shape of the surface it lay on.
    /// Now each surface's patch is CUT at its own seam and shown at full strength: whatever part of the light is on
    /// the floor is the floor's shape the moment it is there, and the part that has climbed onto the wall is the
    /// wall's - which is what a patch crossing a crease does.
    ///
    /// The cut is rows of the cookie: the patch is never rotated, only leaned, so a horizontal seam in the room is a
    /// row of the drawing. The cookie is this patch's own texture, repainted only when the seam moves to another row.
    /// </summary>
    internal sealed class SunPatch
    {
        public readonly Light2D Light;
        private readonly Texture2D _tex;
        private readonly Color32[] _full;
        private readonly Color32[] _work;
        private readonly int _w, _h;
        private int _lo = -1, _hi = -1;

        public SunPatch(string name, Transform parent, Color32[] full, int w, int h)
        {
            _w = w; _h = h;
            _full = full;
            _work = new Color32[full.Length];
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name + "Cookie",
            };
            _tex.SetPixels32(full);
            _tex.Apply(false, false);
            Light = new GameObject(name).AddComponent<Light2D>();
            Light.transform.SetParent(parent, false);
            Light.lightType = Light2D.LightType.Sprite;
            Light.lightCookieSprite = Sprite.Create(_tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            Light.intensity = 0f;
        }

        /// <summary>The patch's height in cookie rows.</summary>
        public int Rows => _h;

        /// <summary>
        /// The first cookie row whose centre lies at or above <paramref name="artY"/>, for a patch centred at
        /// <paramref name="centreY"/> and drawn <paramref name="rowPx"/> art px to a row (rows count up).
        /// </summary>
        public int RowAt(float artY, float centreY, float rowPx)
        {
            float r = (artY - centreY) / Mathf.Max(0.01f, rowPx) + _h * 0.5f - 0.5f;
            return Mathf.Clamp(Mathf.CeilToInt(r), 0, _h);
        }

        /// <summary>Shows rows [lo, hi) and blanks the rest; says whether anything shows.</summary>
        public bool Show(int lo, int hi)
        {
            lo = Mathf.Clamp(lo, 0, _h);
            hi = Mathf.Clamp(hi, lo, _h);
            if (lo == _lo && hi == _hi) return hi > lo;
            _lo = lo; _hi = hi;
            var clear = new Color32(255, 255, 255, 0);
            for (int y = 0; y < _h; y++)
            {
                bool on = y >= lo && y < hi;
                int row = y * _w;
                for (int x = 0; x < _w; x++) _work[row + x] = on ? _full[row + x] : clear;
            }
            _tex.SetPixels32(_work);
            _tex.Apply(false, false);
            return hi > lo;
        }
    }
}
