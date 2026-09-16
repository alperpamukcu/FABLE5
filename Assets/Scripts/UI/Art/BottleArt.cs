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
        private readonly Image _back, _stencil, _drink, _surface, _floor, _front;
        // THE LIQUID'S BODY (2026-09-16): a bottle-aligned shade over the flat drink, and bubbles that rise to the
        // face and pop while the bottle is tipped to pour (the author: "sıvı hissiyatı için doku belki baloncuklar
        // baloncuk patlama animasyonları şişeden dökülürken").
        private readonly Image _grain;
        private readonly RectTransform _grainRt;
        private readonly Image[] _bubbles;
        private struct BubbleState { public float x, y, vy, life; public bool popping; }
        private readonly BubbleState[] _bubbleState;
        private uint _seed = 2463534242u;
        private float Rand() { _seed = _seed * 1664525u + 1013904223u; return (_seed >> 8) / 16777216f; }
        private readonly RectTransform _level, _drinkRt, _surfaceRt, _floorRt;
        // THE CHORD TABLE (2026-09-14): per tilt bucket, per one-texel slab along world-up from the cavity's lowest texel,
        // how many cavity texels the slab holds (its width) and the sum of their positions along world-right (its middle).
        private float[] _chordN, _chordC, _chordMin;
        private const int ChordBins = 256;
        private ItemArt.BottlePlates _plates;
        private float[] _lut;                 // [bucket][row] surface height, lazily built
        private float _shoulderFrac = 1f;     // the cavity's share below the shoulder: "full"
        private int _lutW, _lutH;
        private Color32[] _maskPx;

        private const int Buckets = 72;       // 5° each over the full circle: the pour leans to 118°
        private const int Rows = 64;          // fraction resolution of the table

        private BottleArt(RectTransform root, Image back, Image stencil, RectTransform level,
                          Image drink, Image floor, Image surface, Image front, Image grain, Image[] bubbles)
        {
            _root = root; _back = back; _stencil = stencil; _level = level;
            _drink = drink; _drinkRt = drink.rectTransform;
            _floor = floor; _floorRt = floor.rectTransform;
            _surface = surface; _surfaceRt = surface.rectTransform; _front = front;
            _grain = grain; _grainRt = grain != null ? grain.rectTransform : null;
            _bubbles = bubbles ?? new Image[0];
            _bubbleState = new BubbleState[_bubbles.Length];
            for (int i = 0; i < _bubbleState.Length; i++) _bubbleState[i].life = 0.2f + Rand() * 0.6f;
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
            // THE SANDWICH GETS ITS OWN ROOT (2026-09-06). It used to hang its plates
            // straight on the vessel and switch THE VESSEL off when a card had no plates —
            // which also switched off the flat body image beside them, so every card that is
            // one sprite by design (a carton, a can: brief.SEALED) came to the bench invisible.
            // The plates live under a rect of their own now; the vessel stays up and the two
            // ways of drawing a bottle can no longer turn each other off.
            var rootGo = new GameObject("Sandwich", typeof(RectTransform));
            var root = (RectTransform)rootGo.transform;
            root.SetParent(vessel, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;

            var back = Plate("Back", root, true);
            var stencil = Plate("Clip", root, true);
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
            // The shade rides INSIDE the drink's quad (clipped to it by the quad's own mask, and to the cavity by
            // the stencil above), centred on the vessel and turned back to the bottle's frame in SetLevel.
            drink.gameObject.AddComponent<RectMask2D>();
            var grain = Plate("Grain", drink.transform, false);
            grain.rectTransform.anchorMin = grain.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            grain.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            grain.rectTransform.sizeDelta = Vector2.zero;
            grain.enabled = false;
            var floor = Plate("Floor", level, false);       // the drink's round foot (2026-09-14)
            var surface = Plate("Surface", level, false);   // the drink's oval face (2026-09-14)
            floor.sprite = GlassArt.SurfaceDisc();
            surface.sprite = GlassArt.SurfaceDisc();
            var bubbles = new Image[6];
            for (int i = 0; i < bubbles.Length; i++)
            {
                var b = Plate("Bubble" + i, level, false);
                b.rectTransform.anchorMin = b.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                b.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                b.rectTransform.sizeDelta = new Vector2(6f, 6f);
                b.sprite = ChromeArt.Bubble();
                b.enabled = false;
                bubbles[i] = b;
            }
            var front = Plate("Front", root, true);
            return new BottleArt(root, back, stencil, level, drink, floor, surface, front, grain, bubbles);
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
            if (_grain != null) _grain.sprite = plates.Mask != null
                ? ChromeArt.LiquidShade((int)plates.Mask.rect.width, (int)plates.Mask.rect.height) : null;
            _lut = null; _maskPx = null;
        }

        /// <summary>
        /// The drink: <paramref name="fraction"/> of the cavity (0 = dry, 1 = the shoulder),
        /// in <paramref name="tone"/>, with the surface level for a bottle tilted by
        /// <paramref name="tiltDeg"/> (the parent's z rotation, counter-clockwise positive).
        /// </summary>
        /// <summary>Nothing to show: the drink, its face, foot, shade and bubbles all off.</summary>
        private void Dry()
        {
            _drink.enabled = _surface.enabled = _floor.enabled = false;
            if (_grain != null) _grain.enabled = false;
            foreach (var b in _bubbles) if (b != null) b.enabled = false;
        }

        public void SetLevel(Color tone, double fraction, float tiltDeg)
        {
            if (_plates == null || _plates.Mask == null) { Dry(); return; }
            float f = Mathf.Clamp01((float)fraction);
            if (f <= 0f) { Dry(); return; }
            _drink.enabled = _surface.enabled = true;
            // A LITTLE GLASS IN THE DRINK (2026-09-16, the author: "sıvılarda sıvı dokusu olsa daha gerçekçi olur"):
            // the drink is 90% over the back plate, so the interior's own gradient — light down the middle, dark
            // at the walls — shows through it as the body of a liquid in a round bottle, and the front's streak
            // lies over that. A flat swatch at 100% read as paint.
            _drink.color = new Color(tone.r, tone.g, tone.b, tone.a * 0.9f);
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

            // THE FOOT IS AN ARC WHILE IT STANDS (2026-09-14, the author: "bardağın içerisindeki sıvı ve şişelerin içerisindeki sıvı da 3 boyutlu olmalı altı ve üstü bardağın yüzeylerine göre dairesel hissini vermeli"): the drink's lowest edge is the
            // near half of the base's oval — its middle on the cavity's lowest row, its sides a squashed half-width
            // higher. Only near upright: lying over, the low side of the drink is a wall, not a floor.
            // ...AND THE DRINK STILL REACHES THE FOOT (2026-09-16, the author: "şişelerin büyük hallerinin altındaki
            // kısımlar dolmuyor"). The arc used to be the drink's LOWER BOUNDARY: the quad started `rise` above the
            // cavity's lowest row and only the disc reached down, so at 2x the base's two corners stood as empty
            // glass under a full bottle. In a round bottle the liquid runs straight down the walls to the foot; the
            // near edge of the base's oval is a shading line across it, not where it ends. So the quad goes to the
            // foot, and the disc lies OVER it a shade darker — the base seen through the drink.
            float upright = Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(0f, tiltDeg)) / 20f);
            ChordAt(bucket, 2, out float footChord, out float footMid);
            float rise = footChord * unit * GlassArt.SurfaceSquash * 0.5f * upright;
            if (rise > 0.5f * unit && y > lowest * unit + rise * 2f)
            {
                _floorRt.anchorMin = _floorRt.anchorMax = _floorRt.pivot = new Vector2(0.5f, 0.5f);
                _floorRt.sizeDelta = new Vector2(footChord * unit, rise * 2f);
                _floorRt.anchoredPosition = new Vector2(footMid * unit, lowest * unit + rise);
                _floor.color = new Color(tone.r * 0.80f, tone.g * 0.80f, tone.b * 0.80f, tone.a);
                _floor.enabled = true;
            }
            else _floor.enabled = false;

            // Drink: from its foot up to the surface, full width of the level rect.
            _drinkRt.anchorMin = Vector2.zero; _drinkRt.anchorMax = Vector2.one;
            _drinkRt.offsetMin = new Vector2(0f, Mathf.Clamp(bottom + half, 0f, diag));
            _drinkRt.offsetMax = new Vector2(0f, -(diag - Mathf.Clamp(y + half, 0f, diag)));

            // THE FACE IS AN OVAL (2026-09-14): the level line's chord through the cavity — as wide as the drink is there,
            // at any tilt — drawn as the squashed disc the glasses wear, a tone lighter: its near half over the drink,
            // its far half above the line, and the stencil cutting both to the walls. It replaces the two-row band.
            int faceBin = Mathf.Clamp(Mathf.FloorToInt(surf - _chordMin[bucket]) - 1, 0, ChordBins - 1);
            ChordAt(bucket, faceBin, out float chord, out float chordMid);
            _surfaceRt.anchorMin = _surfaceRt.anchorMax = _surfaceRt.pivot = new Vector2(0.5f, 0.5f);
            _surfaceRt.sizeDelta = new Vector2(chord * unit, Mathf.Max(unit, chord * unit * GlassArt.SurfaceSquash));
            _surfaceRt.anchoredPosition = new Vector2(chordMid * unit, y);
            _surface.enabled = chord > 0f;

            // THE SHADE, in the bottle's frame over the drink's quad: sized to the plate, centred on the vessel
            // (the quad's centre sits (A+B)/2 up the level; the vessel's centre sits `half` up), turned by +tilt to
            // undo the level's counter-rotation.
            if (_grainRt != null && _grain.sprite != null)
            {
                float a = Mathf.Clamp(bottom + half, 0f, diag), b = Mathf.Clamp(y + half, 0f, diag);
                _grain.enabled = true;
                _grainRt.sizeDelta = new Vector2(artW, artH);
                _grainRt.anchoredPosition = new Vector2(0f, half - (a + b) * 0.5f);
                _grainRt.localRotation = Quaternion.Euler(0f, 0f, tiltDeg);
            }
            StepBubbles(tiltDeg, bottom, y, footChord * unit, footMid * unit, unit);
        }

        /// <summary>Bubbles while the bottle pours: born low in the drink, they rise to the face along world-up (the
        /// level's frame), pop as a ring that grows and fades, and come again after a beat. Still when the bottle
        /// stands. The clock is clamped so a slow frame does not throw them across the bottle.</summary>
        private void StepBubbles(float tiltDeg, float floorY, float surfY, float chord, float mid, float unit)
        {
            if (_bubbles.Length == 0) return;
            bool active = Mathf.Abs(Mathf.DeltaAngle(0f, tiltDeg)) > 25f && surfY - floorY > 6f * unit;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int i = 0; i < _bubbles.Length; i++)
            {
                var img = _bubbles[i];
                if (img == null) continue;
                var rt = img.rectTransform;
                ref var s = ref _bubbleState[i];
                if (!img.enabled)
                {
                    if (!active) continue;
                    s.life -= dt;
                    if (s.life > 0f) continue;
                    s.x = mid + (Rand() - 0.5f) * chord * 0.8f;
                    s.y = floorY + Rand() * (surfY - floorY) * 0.5f;
                    s.vy = (10f + Rand() * 14f) * unit;
                    s.popping = false; s.life = 0f;
                    rt.sizeDelta = new Vector2(6f, 6f) * (0.6f + Rand() * 0.6f);
                    rt.localScale = Vector3.one; img.color = Color.white;
                    img.enabled = true;
                }
                else if (!s.popping)
                {
                    s.y += s.vy * dt;
                    s.x += Mathf.Sin(s.y * 0.15f) * 6f * dt;
                    if (s.y >= surfY - 2f * unit) { s.popping = true; s.life = 0.14f; }
                }
                else
                {
                    s.life -= dt;
                    float k = 1f + (0.14f - s.life) * 4f;
                    rt.localScale = new Vector3(k, k, 1f);
                    img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(s.life / 0.14f));
                    if (s.life <= 0f) { img.enabled = false; rt.localScale = Vector3.one; img.color = Color.white; s.life = 0.15f + Rand() * 0.5f; }
                }
                if (!active && img.enabled && !s.popping) { s.popping = true; s.life = 0.14f; }   // the bottle stood up: they burst
                rt.anchoredPosition = new Vector2(s.x, s.y);
            }
        }

        /// <summary>How wide the cavity is in slab <paramref name="bin"/> of a tilt bucket (texels) and where its middle
        /// is along world-right (texels from the art's centre). Zero width for an empty slab.</summary>
        private void ChordAt(int bucket, int bin, out float width, out float middle)
        {
            width = 0f; middle = 0f;
            if (_chordN == null) return;
            int i = bucket * ChordBins + Mathf.Clamp(bin, 0, ChordBins - 1);
            width = _chordN[i];
            middle = width > 0f ? _chordC[i] / width : 0f;
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
            _chordN = new float[Buckets * ChordBins];
            _chordC = new float[Buckets * ChordBins];
            _chordMin = new float[Buckets];
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
                Vector2 right = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));   // world-right in the art's frame
                float pmin = float.PositiveInfinity;
                for (int i = 0; i < pts.Count; i++) { proj[i] = Vector2.Dot(pts[i], up); if (proj[i] < pmin) pmin = proj[i]; }
                _chordMin[b] = pmin;
                for (int i = 0; i < pts.Count; i++)
                {
                    int bin = Mathf.Clamp(Mathf.FloorToInt(proj[i] - pmin), 0, ChordBins - 1);
                    _chordN[b * ChordBins + bin] += 1f;
                    _chordC[b * ChordBins + bin] += Vector2.Dot(pts[i], right);
                }
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
