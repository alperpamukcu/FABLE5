using System.Text;
using LastCall.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The finishing touches, ON the glass they were put on (v5 P14). Until now
    /// <c>AddPreparationAtGlass</c> was acknowledged by a ripple and nothing else: the player
    /// salted a rim they could not see, and the delivered drink looked identical to an
    /// unfinished one. This draws the four grade-bearing preparations where they physically
    /// live — salt and sugar as a speckled crust along the rim, the lemon wedge perched on
    /// the rim's edge, ice floating at the liquid line — anchored off <see cref="GlassArt.Piece"/>,
    /// the same measured interior everything else about the glass already uses.
    ///
    /// One component per glass rect. <see cref="Sync"/> is cheap to call every refresh: it
    /// rebuilds only when the preparation set changes, and otherwise just keeps the ice on
    /// the (moving) surface.
    ///
    /// Mint and olive float too (2026-08-03): they finally have their own pieces —
    /// <c>garnish_mint</c>, a sprig, and <c>garnish_olive</c>, a spear — so a drink with a
    /// garnish poured into it stops looking identical to one without. They are INGREDIENTS,
    /// not preparations, so the caller hands over the run and the styles are read off the
    /// shelf cards behind the glass's ingredient ids.
    /// </summary>
    public sealed class GlassDecor : MonoBehaviour
    {
        private GlassArt.Piece _piece;
        private string _signature = "";
        private readonly System.Collections.Generic.List<RectTransform> _ice =
            new System.Collections.Generic.List<RectTransform>();
        private RectTransform _mint, _olive;

        /// <summary>The most cubes the drawing will stack (2026-08-25). The COUNT is the
        /// glass's (GlassContents.IceCubes, unbounded); past this many the picture is a
        /// full glass of ice whatever the number says, and more sprites would only climb
        /// out of the mouth.</summary>
        private const int MaxDrawnCubes = 7;

        /// <summary>Where the n-th cube sits and how it leans — a fixed table, because ice
        /// that reshuffled itself between refreshes would read as boiling. Hand-laid so the
        /// pile reads as a pile: alternating sides, climbing as it grows.</summary>
        private static readonly (float x, float dy, float size, float lean)[] CubeLay =
        {
            (-10f,  -6f, 20f,  -24f),
            ( 12f, -11f, 16f,   29f),
            (  1f,  -2f, 18f,    8f),
            (-16f, -14f, 15f,   52f),
            ( 18f,  -4f, 15f,  -38f),
            ( -5f, -16f, 14f,   17f),
            (  8f, -18f, 14f,  -11f),
        };
        private static Sprite _saltBand, _sugarBand;

        /// <summary>How the ice rides. Slow and shallow: a cube in a drink is buoyed by it,
        /// not thrown about, and anything faster reads as a boiling glass — the exact word
        /// the lay table was written to avoid.</summary>
        private const float BobRate = 1.6f, BobRise = 2.6f, BobRoll = 5f;

        /// <summary>Finds or adds the decor layer on <paramref name="glassRect"/> and brings it
        /// up to date with what is actually on <paramref name="glass"/>.</summary>
        public static void Sync(RectTransform glassRect, GlassArt.Piece piece, GlassContents glass,
                                TycoonRun run = null, float buildSalt = 0f, float buildSugar = 0f)
        {
            var t = glassRect.Find("Decor");
            GlassDecor decor;
            if (t == null)
            {
                var go = new GameObject("Decor", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(glassRect, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                decor = go.AddComponent<GlassDecor>();
            }
            else decor = t.GetComponent<GlassDecor>();
            decor.transform.SetAsLastSibling();   // crust and wedge draw over the glass walls
            decor.BuildSalt = buildSalt;
            decor.BuildSugar = buildSugar;
            decor.Refresh(piece, glass, run);
        }

        private void Refresh(GlassArt.Piece piece, GlassContents glass, TycoonRun run)
        {
            _piece = piece;
            bool mint = false, olive = false;
            if (glass != null && run != null)
            {
                foreach (var id in glass.Ingredients)
                {
                    string style = run.Shelf.Find(id)?.Ingredient?.Info?.Style;
                    mint |= style == "mint";
                    olive |= style == "olive";
                }
            }
            var sig = new StringBuilder();
            if (glass != null)
                foreach (var prep in glass.PreparationSteps) sig.Append(prep.Id).Append(';');
            // The cube COUNT is part of the signature: a third cube dropped into an iced
            // drink must redraw, and "ice;" alone would say nothing changed.
            if (glass != null && glass.IceCubes > 0) sig.Append('i').Append(glass.IceCubes).Append(';');
            if (mint) sig.Append("m;");
            if (olive) sig.Append("o;");
            if (BuildSalt > 0.004f) sig.Append("bs;");
            if (BuildSugar > 0.004f) sig.Append("bg;");
            string signature = sig.ToString();
            if (signature != _signature)
            {
                _signature = signature;
                Rebuild(glass, mint, olive);
            }
            PlaceFloats(glass);
            if (_crust != null)
            {
                bool done = glass != null && (glass.HasPreparation("salt_rim")
                                              || glass.HasPreparation("sugar_rim"));
                float want = done ? 1f : Mathf.Clamp01(Mathf.Max(BuildSalt, BuildSugar));
                if (!Mathf.Approximately(_crust.fillAmount, want)) _crust.fillAmount = want;
            }
        }

        /// <summary>How far round the lap has got, 0..1, for the crust that is being laid
        /// right now (2026-09-09). Set by whoever is running the turn; zero the rest of the
        /// time. The finished preparation on the glass still wins over both.</summary>
        public float BuildSalt, BuildSugar;
        private Image _crust;

        private void Rebuild(GlassContents glass, bool mint, bool olive)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            _ice.Clear();
            _mint = _olive = null;
            if (glass == null) return;

            var rect = ((RectTransform)transform).rect;
            float w = rect.width, h = rect.height;
            float interiorW = _piece.InteriorHalf * w;           // InteriorHalf is of the half-width
            float rimYLocal = (_piece.RimY - 0.5f) * h;          // glass rects pivot centre

            // THE CRUST IS ON THE MOUTH AND YOU CAN SEE IT (2026-08-26, the author:
            // "bardagin etrafina surdugumuz seker ve tuz daha belirgin olmali ve bardagin
            // agzinda olmali, su an kaymalar var ve sadece kutulardan olusuyor fark
            // edilmiyor bile").
            //
            // Three faults in one strip. It was drawn at the glass's INTERIOR width, which
            // is narrower than the rim it is supposed to be crusted onto, so it floated
            // inside the mouth instead of sitting on it. It was seven units tall against a
            // ninety-unit glass. And its speckle was six rows of hard white dots on nothing
            // — at that size, boxes.
            //
            // It is the RIM's own width now (the drawn width at the mouth, not the
            // interior), twice as deep, and it is drawn as three pieces rather than one:
            // a dark seat where the crust meets the glass so it reads as ON something, the
            // speckle itself, and a bright lip along the very top edge where a real crust
            // catches the light. Nothing about it is random — see Speckles.
            // A CRUST IS ON THE GLASS EITHER WHEN IT IS FINISHED OR WHILE IT IS BEING LAID.
            bool applied = glass.HasPreparation("salt_rim") || glass.HasPreparation("sugar_rim");
            if (applied || BuildSalt > 0.004f || BuildSugar > 0.004f)
            {
                // A RING OF GRAINS ROUND THE MOUTH (2026-09-06, the author: "çok küçük tuz
                // ve şeker taneleri bardağın ağzını saracak şekilde"). The new glasses are
                // seen a little from above, so the mouth is an ellipse; the crust is drawn
                // ALONG it — one-pixel grains scattered on that ellipse, denser where the
                // near arc catches the light — at the exact size the glass is shown at, so
                // no grain is ever scaled. A flat band across the rim was a stripe.
                bool salt = glass.HasPreparation("salt_rim") || (!applied && BuildSalt > BuildSugar);
                // THE AUTHOR DREW THEM (2026-09-09: "artık şeker ve tuz rim için görseller
                // geliştirdim"). Eight crusts, one a glass a kind, each cut to that glass's
                // own mouth with the grains breaking over the near arc — so the ring is a
                // drawing now rather than a scatter of pixels on an ellipse, and it is
                // seated by measurement: centred, its top on the glass's first opaque row.
                var drawn = salt ? _piece.RimSalt : _piece.RimSugar;
                if (drawn != null && _piece.RimPlacement(new Vector2(w, h), drawn,
                                                        out var crustSize, out var crustTop))
                {
                    // UP ON THE MOUTH, NOT DOWN AT THE FOOT (2026-09-09, the author: "tuz ve
                    // limon bardağın yukarısında olmalı, şu an aşağısına sabitlenmiş").
                    // RimPlacement answers in the LIP's frame — an offset from the rect's TOP
                    // for a top-pivoted child — and NewChild builds a CENTRE-pivoted one, so
                    // the offset was half a glass adrift. Half the height puts it back.
                    var crust = NewChild("Crust", crustSize,
                        new Vector2(crustTop.x, h * 0.5f + crustTop.y - crustSize.y * 0.5f));
                    _crust = crust.gameObject.AddComponent<Image>();
                    _crust.sprite = drawn;
                    _crust.preserveAspect = true;
                    _crust.raycastTarget = false;
                    // AND IT BUILDS AS THE GLASS TURNS (2026-09-09, the author: "bardağın
                    // etrafında tuzu döndürürken döndürdüğümüz kadarı oluşmalı"): the crust
                    // is filled left to right by the lap's own sweep, so the picture on the
                    // glass and the ring under the hand say the same thing at the same time.
                    _crust.type = Image.Type.Filled;
                    _crust.fillMethod = Image.FillMethod.Horizontal;
                    _crust.fillOrigin = (int)Image.OriginHorizontal.Left;
                    _crust.fillAmount = applied ? 1f : Mathf.Clamp01(salt ? BuildSalt : BuildSugar);
                }
                else
                {
                    float mouthW = _piece.InteriorWidthAt(_piece.RimY) * w;
                    if (mouthW < 4f) mouthW = interiorW * 2f;
                    var tone = salt ? new Color(0.95f, 0.96f, 0.97f) : new Color(0.94f, 0.89f, 0.77f);
                    int ringW = Mathf.Max(12, Mathf.RoundToInt(mouthW + 10f));
                    int ringH = Mathf.Max(6, Mathf.RoundToInt(h * 0.085f));
                    var ring = NewChild("Crust", new Vector2(ringW, ringH),
                        new Vector2(0, rimYLocal + ringH * 0.5f - 1f));
                    var img = ring.gameObject.AddComponent<Image>();
                    img.sprite = RimRing(ringW, ringH, salt);
                    img.color = tone;
                    img.raycastTarget = false;
                }
            }

            // THE WEDGE STRADDLES THE GLASS (2026-08-26, the author: "bardagin camina
            // sokulan version bir limon uretmelisin, bir kismi bardagin icerisinde
            // hissettirmeli"). glass_lemon_rim is cut with a slit so it sits ON the edge
            // with its lower half inside the drink; it hangs at the RIM's own edge rather
            // than half an interior in, and it is drawn as a CHILD of this decor — which
            // is the whole of the author's other note about it, because this decor rides
            // the glass rect and everything parented to it moves and leans with the drink.
            if (glass.HasPreparation("lemon_twist"))
            {
                // A HALF SLICE ON THE RIM (2026-09-06, the author: "yarım limon dilimi"): a
                // half wheel, cut side down, straddling the rim at its right-hand end —
                // rind, pith, five segments — drawn at the size the glass is shown at.
                float mouthHalf = _piece.InteriorWidthAt(_piece.RimY) * w * 0.5f;
                if (mouthHalf < 2f) mouthHalf = interiorW * 0.5f;
                int d = Mathf.Max(10, Mathf.RoundToInt(Mathf.Clamp(mouthW_(w), 20f, 200f) * 0.34f));
                var wedge = NewChild("Wedge", new Vector2(d, d * 0.5f + 2f),
                    new Vector2(mouthHalf - d * 0.30f, rimYLocal + d * 0.22f));
                var img = wedge.gameObject.AddComponent<Image>();
                img.sprite = HalfSlice(d);
                img.raycastTarget = false;
                wedge.localRotation = Quaternion.Euler(0, 0, -4f);
            }

            if (glass.HasPreparation("ice"))
            {
                // AS MANY CUBES AS WENT IN (2026-08-25, the author: "buz istediği sayıda
                // atılabilecek ve bardağın içerisinde gözükecek"). The glass counts them
                // (GlassContents.IceCubes); the lay table keeps each one where it landed.
                int cubes = Mathf.Clamp(glass.IceCubes, 1, MaxDrawnCubes);
                for (int n = 0; n < cubes; n++)
                {
                    var lay = CubeLay[n];
                    _ice.Add(IceCube("Ice" + n, lay.size, lay.x, lay.lean));
                }
            }

            // The garnish floats. The sprig stands proud of the surface; the spear leans,
            // olives half under. Both ride the fill in PlaceFloats, exactly as the ice does.
            // Both re-cut on 2026-08-26 with the ice and the wedge: the old sprig and the
            // old spear were drawn for a tray, at a tray's size, with a keyline a garnish
            // floating in a drink has no business wearing.
            if (mint) _mint = Float("Mint", "glass_mint", new Vector2(26f, 27f), -14f, -8f);
            if (olive) _olive = Float("Olive", "glass_olive", new Vector2(22f, 40f), 10f, 20f);
        }

        private RectTransform Float(string name, string art, Vector2 size, float x, float lean)
        {
            var piece = NewChild(name, size, new Vector2(x, 0));
            var img = piece.gameObject.AddComponent<Image>();
            img.sprite = ItemArt.Load(art);
            img.preserveAspect = true; img.raycastTarget = false;
            if (img.sprite == null) img.color = UITheme.Lime[3];
            piece.localRotation = Quaternion.Euler(0, 0, lean);
            return piece;
        }

        private RectTransform IceCube(string name, float size, float x, float lean)
        {
            var cube = NewChild(name, new Vector2(size, size), Vector2.zero);
            var img = cube.gameObject.AddComponent<Image>();
            // ITS OWN CUBE (2026-08-26, the author: "bardagin icerisindeki buz gorselini
            // tekrardan olustur"). It wore the LICENCE's pictogram, which is a mark drawn to
            // read at 12 px on a card - a flat white lozenge - and seven of them stacked in a
            // glass read as a snowdrift. glass_ice is a drawn cube with facets and a
            // highlight, struck at the size it floats at. The pictogram stays the fallback:
            // the card and the glass agreeing was the old reason for it, and a missing
            // drawing should still put something in the drink.
            img.sprite = ItemArt.Load("glass_ice") ?? PrefArt.Ice();
            img.preserveAspect = true; img.raycastTarget = false;
            if (img.sprite == null) img.color = new Color(0.75f, 0.9f, 1f, 0.9f);
            img.color = new Color(img.color.r, img.color.g, img.color.b, 0.92f);
            cube.localRotation = Quaternion.Euler(0, 0, lean);   // the lay table's own tilt
            cube.anchoredPosition = new Vector2(x, 0);
            return cube;
        }

        /// <summary>Everything that floats sits AT the liquid line and rides the fill as the
        /// drink pours — the ice cubes, the sprig, the spear.</summary>
        private void PlaceFloats(GlassContents glass)
        {
            if (glass == null) return;
            var rect = ((RectTransform)transform).rect;
            float surface = _piece.FillAmount((float)glass.FillFraction);
            float y = (surface - 0.5f) * rect.height;
            // ICE FLOATS (2026-08-26, the author: "icine koyulan buz yuzuyor hissi
            // vermeli"). The cubes sat at fixed offsets off the liquid line, which is a
            // pile of ice RESTING on a surface — the one thing ice in a drink never does.
            // Each cube now rides its own slow bob and rolls a few degrees with it, out of
            // phase with the others: a sine on the unscaled clock, with the phase taken
            // from the cube's own index so it is the same drink every time it is drawn and
            // no randomness is involved (the house rule). Amplitude is small on purpose —
            // ice in a full glass is buoyed, not tossed — and it SETTLES as the glass
            // empties, because a cube with nothing under it is aground.
            float afloat = Mathf.Clamp01((float)glass.FillFraction / 0.35f);
            float clock = Time.unscaledTime;
            for (int n = 0; n < _ice.Count; n++)
            {
                if (_ice[n] == null) continue;
                var lay = CubeLay[Mathf.Min(n, CubeLay.Length - 1)];
                float phase = n * 1.7f;
                float bob = Mathf.Sin(clock * BobRate + phase) * BobRise * afloat;
                float roll = Mathf.Sin(clock * BobRate * 0.8f + phase) * BobRoll * afloat;
                _ice[n].anchoredPosition = new Vector2(lay.x, y + lay.dy + bob);
                _ice[n].localRotation = Quaternion.Euler(0, 0, lay.lean + roll);
            }
            // The sprig and the spear ride the same swell, half as hard: they are lighter
            // and they are anchored on the rim, so they nod rather than bob.
            if (_mint != null)
                _mint.anchoredPosition = new Vector2(_mint.anchoredPosition.x,
                    y + 4f + Mathf.Sin(clock * BobRate + 0.6f) * BobRise * 0.5f * afloat);
            if (_olive != null)
                _olive.anchoredPosition = new Vector2(_olive.anchoredPosition.x,
                    y - 8f + Mathf.Sin(clock * BobRate + 2.4f) * BobRise * 0.5f * afloat);
        }

        private RectTransform NewChild(string name, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        // ── the crusts: tiny procedural speckle strips, built once and kept ─────────
        // Deterministic speckles from a hash, not a die: same crust every run, and the rule
        // about randomness never even comes up.

        private static Sprite SaltBand() => _saltBand != null ? _saltBand
            : _saltBand = Speckles(new Color32(0xF2, 0xF4, 0xF6, 0xFF));

        private static Sprite SugarBand() => _sugarBand != null ? _sugarBand
            : _sugarBand = Speckles(new Color32(0xF0, 0xE2, 0xC4, 0xFF));

        private float mouthW_(float w)
        {
            float m = _piece.InteriorWidthAt(_piece.RimY) * w;
            return m < 4f ? _piece.InteriorHalf * w * 2f : m;
        }

        private static readonly Dictionary<string, Sprite> _rings = new Dictionary<string, Sprite>();
        private static readonly Dictionary<int, Sprite> _slices = new Dictionary<int, Sprite>();

        /// <summary>Grains on the mouth's ellipse, drawn at the glass's own size: the ring
        /// is the ellipse inscribed in the sprite, two pixels thick, seeded by a hash so it
        /// is the same crust every time (the house rule on randomness). White, tinted by
        /// the caller; sugar is a touch sparser and its grains clump.</summary>
        private static Sprite RimRing(int w, int h, bool salt)
        {
            string key = w + "x" + h + (salt ? "s" : "g");
            if (_rings.TryGetValue(key, out var got) && got != null) return got;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var clear = new Color32(0, 0, 0, 0);
            var grain = new Color32(255, 255, 255, 255);
            var dim = new Color32(255, 255, 255, 150);
            // A SEAT under the grains: the ellipse a pixel lower in a dark wash, so white
            // grains still read on a pale frosted rim (photographed without it: invisible).
            var seat = new Color32(20, 16, 28, 96);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            float a = (w - 1) * 0.5f, b = (h - 1) * 0.5f;
            float ringOut = 1f, ringIn = 1f - 2.4f / Mathf.Max(1f, Mathf.Min(a, b) * 1.6f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - a) / Mathf.Max(1f, a), dy = (y - 1 - b) / Mathf.Max(1f, b);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r <= ringOut && r >= ringIn) px[y * w + x] = seat;
                }
            uint hash = 2166136261;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - a) / Mathf.Max(1f, a), dy = (y - b) / Mathf.Max(1f, b);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // The band: from the ellipse's own line to two pixels inside it.
                    if (r > ringOut || r < ringIn) continue;
                    hash = (hash ^ (uint)(x * 31 + y * 7 + w * 3)) * 16777619;
                    // Denser on the near (lower) arc, which is the one the light and the
                    // eye land on; the far arc thins so the ring reads as going round.
                    int chance = y < b ? (salt ? 3 : 4) : (salt ? 2 : 3);
                    if ((hash >> 8) % (uint)chance != 0) continue;
                    px[y * w + x] = grain;
                    if (!salt && (hash >> 20) % 3 == 0 && x + 1 < w) px[y * w + x + 1] = dim;
                }
            // INKED (2026-09-07, the author: "salted rim ve sugar rim için de siyah kontürlü
            // bir tasarım"). Every grain gets a dark pixel ring where there was nothing, so
            // the crust reads as crystals on the glass rather than white dust on white.
            var ink = new Color32(0x0D, 0x08, 0x13, 200);
            var inked = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a >= 150) continue;
                    bool touches = false;
                    for (int dy = -1; dy <= 1 && !touches; dy++)
                        for (int dx = -1; dx <= 1 && !touches; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            touches = px[ny * w + nx].a >= 150;
                        }
                    if (touches) inked[y * w + x] = ink;
                }
            tex.SetPixels32(inked);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            sp.hideFlags = HideFlags.DontSave;
            return _rings[key] = sp;
        }

        /// <summary>A half wheel of lemon, cut side down, at diameter <paramref name="d"/>:
        /// a two-pixel rind, a pale pith, the flesh in five segments.</summary>
        private static Sprite HalfSlice(int d)
        {
            if (_slices.TryGetValue(d, out var got) && got != null) return got;
            int h = d / 2 + 2;
            var tex = new Texture2D(d, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[d * h];
            var clear = new Color32(0, 0, 0, 0);
            var rind = new Color32(0xE8, 0xA3, 0x3D, 255);      // Amber[3]
            var rindDark = new Color32(0xC9, 0x82, 0x2B, 255);  // Amber[2]
            var pith = new Color32(0xF5, 0xEE, 0xC8, 255);
            var flesh = new Color32(0xF5, 0xE0, 0x6A, 255);
            var seam = new Color32(0xFA, 0xF3, 0xB0, 255);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            float cx = (d - 1) * 0.5f, R = (d - 1) * 0.5f;
            float rindW = Mathf.Max(1.5f, d * 0.08f), pithW = Mathf.Max(1f, d * 0.05f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < d; x++)
                {
                    float dx = x - cx, dy = y - 1.5f;      // the flat cut is the top rows
                    if (dy < 0f) continue;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r > R) continue;
                    Color32 c;
                    if (r > R - rindW) c = y > h * 0.7f ? rindDark : rind;
                    else if (r > R - rindW - pithW) c = pith;
                    else
                    {
                        // five segments: seams every 36 degrees, from the centre out
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;   // 0..180
                        float m = ang % 36f;
                        c = (m < 2.5f || m > 33.5f) && r > pithW + 1f ? seam : flesh;
                    }
                    px[(h - 1 - y) * d + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, d, h), new Vector2(0.5f, 0.5f), 1f);
            sp.hideFlags = HideFlags.DontSave;
            return _slices[d] = sp;
        }

        /// <summary>
        /// The crust along a rim, as GRAINS (2026-09-06, the author: "aynı şekilde tuz ve
        /// şeker gerdanlığı için çok küçük partiküller üretilebilir"). It was 48x6 stretched
        /// across a 190-unit glass, so every grain of salt landed as a four-unit block and the
        /// rim read as a dotted line drawn on the glass. Three times the pixels, a sparser
        /// scatter and a soft edge: from a chair it is a crust, up close it is crystals.
        /// </summary>
        private static Sprite Speckles(Color32 tone)
        {
            const int W = 144, H = 18;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            uint hash = 2166136261;
            for (int x = 0; x < W; x++)
            {
                for (int y = 0; y < H; y++)
                {
                    hash = (hash ^ (uint)(x * 31 + y * 7)) * 16777619;
                    // Thickest through the middle of the band and thinning to nothing at
                    // both edges, so the crust clings to the rim instead of ending on a line.
                    float t = Mathf.Abs(y - (H - 1) * 0.5f) / ((H - 1) * 0.5f);
                    int chance = 3 + Mathf.RoundToInt(t * t * 22f);
                    if ((hash >> 8) % (uint)chance == 0)
                    {
                        // A grain is one pixel; now and then two of them clump.
                        px[y * W + x] = tone;
                        if ((hash >> 20) % 5 == 0 && x + 1 < W) px[y * W + x + 1] = tone;
                    }
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }
    }
}
