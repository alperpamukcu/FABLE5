using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A v4 bottle on a canvas: back plate, the drink, the glass front — and the drink's
    /// surface stays LEVEL while the bottle tilts (Docs/PLAN_bottle_art_v4.md §12).
    ///
    /// This is the industry's standard shape, sourced in the plan: the liquid is a cutoff on
    /// a height measured in WORLD space, not in the bottle's own frame — every liquid shader
    /// in Unity's own course and the Minions Art lineage does exactly this, because a fill
    /// measured in object space rotates with the container, which is what the previous
    /// <c>BottleFill</c> did under the 118° pour. Here there is no shader: a "Level" rect
    /// between the stencil and the drink is counter-rotated by −tilt every frame, so its axes
    /// stay world-aligned and the drink's top edge is a level line whatever the bottle does.
    ///
    /// The fill is VOLUME-TRUE, which no 3D liquid shader manages (their fill is a height
    /// offset from the pivot, so a tilted half-full flask visibly changes how much it holds).
    /// In 2D we have the cavity mask, so an exact table is cheap: for each tilt bucket the
    /// mask's texels are sorted by their projection onto world-up, and the fraction picks
    /// the texel whose projection is the surface. A bottle that is 40% full is 40% full
    /// upright, on its side, and everywhere between.
    ///
    /// Layer order, back to front: Back (the interior, opaque) → Clip (a Mask carrying the
    /// drink's own cavity sprite) → Level → Drink + Surface band → Front (the glass film, the
    /// label and the outline at full alpha). All four plates share one canvas, so the same
    /// stretched-and-preserved rect puts them 1:1 on each other.
    /// </summary>
    public sealed class BottleArt
    {
        private readonly RectTransform _root;
        private readonly Image _back, _stencil, _drink, _surface, _front;
        private readonly RectTransform _level, _drinkRt, _surfaceRt;
        private ItemArt.BottlePlates _plates;
        private float[] _lut;                 // [bucket][row] surface height, lazily built
        private float _shoulderFrac = 1f;     // the cavity's share below the shoulder: "full"
        private int _lutW, _lutH;
        private Color32[] _maskPx;

        private const int Buckets = 72;       // 5° each over the full circle: the pour leans to 118°
        private const int Rows = 64;          // fraction resolution of the table

        private BottleArt(RectTransform root, Image back, Image stencil, RectTransform level,
                          Image drink, Image surface, Image front)
        {
            _root = root; _back = back; _stencil = stencil; _level = level;
            _drink = drink; _drinkRt = drink.rectTransform;
            _surface = surface; _surfaceRt = surface.rectTransform; _front = front;
        }

        /// <summary>
        /// Builds the sandwich as the children of <paramref name="vessel"/>. The caller keeps
        /// the vessel's rect sized to the art (VesselArt.StandOn) and rotates its PARENT for
        /// the tilt; this only ever reads that rotation.
        /// </summary>
        public static BottleArt Under(RectTransform vessel)
        {
            Image Plate(string name, Transform parent, bool preserve)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                var img = go.GetComponent<Image>();
                img.preserveAspect = preserve;
                img.raycastTarget = false;
                return img;
            }
            var back = Plate("Back", vessel, true);
            var stencil = Plate("Clip", vessel, true);
            stencil.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            // The level rect: centred, world-aligned by counter-rotation, big enough that its
            // drink child covers the stencil at any angle (the diagonal, with room).
            var levelGo = new GameObject("Level", typeof(RectTransform));
            var level = (RectTransform)levelGo.transform;
            level.SetParent(stencil.rectTransform, false);
            level.anchorMin = level.anchorMax = new Vector2(0.5f, 0.5f);
            level.pivot = new Vector2(0.5f, 0.5f);
            level.anchoredPosition = Vector2.zero;
            var drink = Plate("Drink", level, false);
            var surface = Plate("Surface", level, false);
            var front = Plate("Front", vessel, true);
            return new BottleArt(vessel, back, stencil, level, drink, surface, front);
        }

        public void Show(ItemArt.BottlePlates plates)
        {
            _plates = plates;
            bool on = plates != null;
            if (_root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
            if (!on) return;
            _back.sprite = plates.Back; _back.enabled = plates.Back != null;
            _stencil.sprite = plates.Mask; _stencil.enabled = plates.Mask != null;
            _front.sprite = plates.Front; _front.enabled = plates.Front != null;
            _lut = null; _maskPx = null;
        }

        /// <summary>
        /// The drink: <paramref name="fraction"/> of the cavity (0 = dry, 1 = the shoulder),
        /// in <paramref name="tone"/>, with the surface level for a bottle tilted by
        /// <paramref name="tiltDeg"/> (the parent's z rotation, counter-clockwise positive).
        /// </summary>
        public void SetLevel(Color tone, double fraction, float tiltDeg)
        {
            if (_plates == null || _plates.Mask == null) { _drink.enabled = _surface.enabled = false; return; }
            float f = Mathf.Clamp01((float)fraction);
            if (f <= 0f) { _drink.enabled = _surface.enabled = false; return; }
            _drink.enabled = _surface.enabled = true;
            _drink.color = tone;
            _surface.color = new Color(Mathf.Min(1f, tone.r * 1.18f + 0.07f),
                                       Mathf.Min(1f, tone.g * 1.18f + 0.07f),
                                       Mathf.Min(1f, tone.b * 1.18f + 0.07f), tone.a);

            // The stencil's rect is the vessel's; the mask sprite is letterboxed inside it by
            // preserveAspect, so one art texel is `unit` canvas units and the art's origin
            // sits at `origin` inside the rect.
            var rect = _stencil.rectTransform.rect;
            var sp = _plates.Mask;
            float unit = Mathf.Min(rect.width / sp.rect.width, rect.height / sp.rect.height);
            float artW = sp.rect.width * unit, artH = sp.rect.height * unit;

            // The level rect is world-aligned: counter-rotate, and size it to cover.
            _level.localRotation = Quaternion.Euler(0f, 0f, -tiltDeg);
            float diag = Mathf.Sqrt(artW * artW + artH * artH) * 1.2f;
            _level.sizeDelta = new Vector2(diag, diag);

            // Surface height along world-up, from the table (texel units above the cavity's
            // lowest texel along that same direction), converted to canvas units in the
            // level rect's frame — whose origin is the vessel centre.
            // The table covers the whole circle. It stopped at ±90° until 2026-09-04 while the
            // pour leans to 118° (MaxTilt): past horizontal the horizontal bucket was read and
            // the cavity's mouth-side corner — where the drink pools while pouring — drew dry.
            int bucket = Mathf.RoundToInt(Mathf.Repeat(tiltDeg + 180f, 360f) / 360f * Buckets) % Buckets;
            EnsureLut();
            // FULL MEANS THE SHOULDER, AS A VOLUME (the author, 2026-09-04): 1.0 is the volume
            // below the shoulder when the bottle stands upright, so upright the surface sits
            // on the shoulder line — and tilted, that same volume runs into the neck, which
            // the mask now includes. The remap is the shoulder's share of the cavity's texels.
            f *= _shoulderFrac;
            int row = Mathf.Clamp(Mathf.RoundToInt(f * (Rows - 1)), 0, Rows - 1);
            float surf = _lut[bucket * Rows + row];          // projection, in texels
            float lowest = _lut[bucket * Rows + 0];          // the cavity's bottom along up
            // Projection is measured from the ART's centre, so the level rect (also centred
            // on the art, because preserveAspect centres the plate) maps it directly.
            float y = surf * unit;
            float bottom = (lowest - 2f) * unit;
            float half = diag * 0.5f;
            // Drink: from below the cavity up to the surface, full width of the level rect.
            _drinkRt.anchorMin = Vector2.zero; _drinkRt.anchorMax = Vector2.one;
            _drinkRt.offsetMin = new Vector2(0f, Mathf.Clamp(bottom + half, 0f, diag));
            _drinkRt.offsetMax = new Vector2(0f, -(diag - Mathf.Clamp(y + half, 0f, diag)));
            // Surface band: two art rows just under the line — a lighter tone, clipped by the
            // stencil so its ends land on the cavity walls by themselves.
            float band = 2f * unit;
            _surfaceRt.anchorMin = Vector2.zero; _surfaceRt.anchorMax = Vector2.one;
            _surfaceRt.offsetMin = new Vector2(0f, Mathf.Clamp(y + half - band, 0f, diag));
            _surfaceRt.offsetMax = new Vector2(0f, -(diag - Mathf.Clamp(y + half, 0f, diag)));
        }

        /// <summary>
        /// The volume table: for each tilt bucket, the mask's opaque texels projected onto
        /// world-up and sorted; entry r is the projection of the (r/Rows)-th texel. Built once
        /// per mask, ~96×192 texels × 36 buckets, well under a millisecond in practice.
        /// </summary>
        private void EnsureLut()
        {
            if (_lut != null) return;
            var sp = _plates.Mask;
            var tex = sp.texture;
            // textureRect, not rect: for an atlas-packed sprite the texture is the atlas and
            // the texels live in the sub-rect (2026-09-04 audit).
            var tr = sp.textureRect;
            int x0 = Mathf.RoundToInt(tr.x), y0 = Mathf.RoundToInt(tr.y);
            _lutW = Mathf.RoundToInt(tr.width); _lutH = Mathf.RoundToInt(tr.height);
            _maskPx = tex.isReadable ? tex.GetPixels32() : null;
            if (_maskPx == null)
                Debug.LogWarning("BottleArt: mask '" + sp.name + "' is not readable, so no drink can be drawn "
                                 + "(Resources/Items must import Read/Write; see PatronArtPostprocessor).");
            var pts = new List<Vector2>(_lutW * _lutH / 2);
            if (_maskPx != null)
            {
                int tw = tex.width;
                for (int y = 0; y < _lutH; y++)
                    for (int x = 0; x < _lutW; x++)
                        if (_maskPx[(y0 + y) * tw + x0 + x].a > 127)
                            pts.Add(new Vector2(x + 0.5f - _lutW * 0.5f, y + 0.5f - _lutH * 0.5f));
            }
            _lut = new float[Buckets * Rows];
            _shoulderFrac = 1f;
            if (pts.Count == 0) return;
            // FULL IS THE SHOULDER: the remap is the shoulder's share of the cavity's texels,
            // from the one table the cellar reads too (Upright), so the two cannot drift.
            _shoulderFrac = Upright(sp).ShoulderFrac;
            var proj = new float[pts.Count];
            for (int b = 0; b < Buckets; b++)
            {
                float tilt = -180f + 360f * b / Buckets;
                // World-up expressed in the art's frame for a bottle rotated by `tilt`
                // counter-clockwise: a texel p lands at R(tilt)·p, whose height is
                // p·(sin tilt, cos tilt). The sign was flipped (−tilt) until 2026-09-04, which
                // mirrored every bucket; unseen only because the cavities are near-symmetric.
                float rad = tilt * Mathf.Deg2Rad;
                Vector2 up = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                for (int i = 0; i < pts.Count; i++) proj[i] = Vector2.Dot(pts[i], up);
                System.Array.Sort(proj);
                for (int r = 0; r < Rows; r++)
                {
                    int k = Mathf.Clamp(Mathf.RoundToInt((float)r / (Rows - 1) * (proj.Length - 1)), 0, proj.Length - 1);
                    _lut[b * Rows + r] = proj[k];
                }
            }
        }

        public void Hide() { if (_root != null && _root.gameObject.activeSelf) _root.gameObject.SetActive(false); }

        /// <summary>The run-restart path (TycoonHud) drops the upright tables with the sprites.</summary>
        public static void ClearCache() { _tables.Clear(); }

        // ── the upright table: what "full" means, shared by the hand and the cellar ──────

        /// <summary>
        /// A cavity mask measured upright: opaque texels per row (rows from the sprite's
        /// bottom edge), the shoulder row by the pipeline's rule — the first row from the top
        /// at least 88% as wide as the median width of the lower body (rows 55%..90% down) —
        /// and the texel counts that turn a fraction into a volume. FULL IS THE SHOULDER
        /// (the author, 2026-09-04): 1.0 is the volume below it. The hand's tilt table takes
        /// its remap from here and the cellar takes its level rows from here, so one bottle
        /// shows one level wherever it stands (the cellar drew a plain height fraction over
        /// the whole mask until the 2026-09-04 audit: a neck full of drink, 5–11 rows above
        /// the bench's level).
        /// </summary>
        public sealed class UprightTable
        {
            public int MinY, MaxY, Shoulder;
            public int[] Width;
            public int Total, BelowShoulder;
            public float ShoulderFrac => Total > 0 ? Mathf.Clamp01(BelowShoulder / (float)Total) : 1f;

            /// <summary>Whole rows of cavity, counted up from its foot, that
            /// <paramref name="fraction"/> of the shoulder volume fills standing upright.</summary>
            public int RowsFor(float fraction)
            {
                if (fraction <= 0f || BelowShoulder <= 0 || Width == null) return 0;
                int want = Mathf.RoundToInt(Mathf.Clamp01(fraction) * BelowShoulder);
                if (want <= 0) return 0;
                int cum = 0;
                for (int ry = MinY; ry <= Shoulder; ry++)
                {
                    cum += Width[ry];
                    if (cum >= want) return ry - MinY + 1;
                }
                return Shoulder - MinY + 1;
            }
        }

        private static readonly Dictionary<Sprite, UprightTable> _tables = new Dictionary<Sprite, UprightTable>();

        /// <summary>The upright table of a mask sprite, built once per sprite. An unreadable
        /// texture gives an empty table (ShoulderFrac 1, no rows) and a warning.</summary>
        public static UprightTable Upright(Sprite mask)
        {
            if (mask == null) return new UprightTable();
            if (_tables.TryGetValue(mask, out var cached)) return cached;
            var t = new UprightTable();
            var tex = mask.texture;
            var tr = mask.textureRect;
            int x0 = Mathf.RoundToInt(tr.x), y0 = Mathf.RoundToInt(tr.y);
            int w = Mathf.RoundToInt(tr.width), h = Mathf.RoundToInt(tr.height);
            if (tex == null || !tex.isReadable || w <= 0 || h <= 0)
            {
                if (tex != null && !tex.isReadable)
                    Debug.LogWarning("BottleArt: mask '" + mask.name + "' is not readable; no upright table.");
                _tables[mask] = t;
                return t;
            }
            var px = tex.GetPixels32();
            int tw = tex.width;
            t.Width = new int[h];
            t.MinY = h; t.MaxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[(y0 + y) * tw + x0 + x].a > 127)
                    {
                        t.Width[y]++; t.Total++;
                        if (y < t.MinY) t.MinY = y; if (y > t.MaxY) t.MaxY = y;
                    }
            if (t.Total == 0) { _tables[mask] = t; return t; }
            // texel rows count from the BOTTOM (Unity's texture origin): the top of the
            // bottle is MaxY, the foot is MinY.
            int hgt = t.MaxY - t.MinY + 1;
            var lower = new List<int>();
            for (int ry = t.MinY; ry <= t.MaxY; ry++)
            {
                float down = (t.MaxY - ry) / (float)Mathf.Max(1, hgt);   // 0 at the top
                if (down >= 0.55f && down <= 0.90f && t.Width[ry] > 0) lower.Add(t.Width[ry]);
            }
            lower.Sort();
            int bodyW = lower.Count > 0 ? lower[lower.Count / 2] : 1;
            t.Shoulder = t.MaxY;
            for (int ry = t.MaxY; ry >= t.MinY; ry--)
                if (t.Width[ry] >= 0.88f * bodyW) { t.Shoulder = ry; break; }
            for (int ry = t.MinY; ry <= t.Shoulder; ry++) t.BelowShoulder += t.Width[ry];
            _tables[mask] = t;
            return t;
        }
    }
}
