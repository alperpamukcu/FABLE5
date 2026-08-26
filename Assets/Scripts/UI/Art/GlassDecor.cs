using System.Text;
using LastCall.Core;
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

        /// <summary>Finds or adds the decor layer on <paramref name="glassRect"/> and brings it
        /// up to date with what is actually on <paramref name="glass"/>.</summary>
        public static void Sync(RectTransform glassRect, GlassArt.Piece piece, GlassContents glass,
                                TycoonRun run = null)
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
            string signature = sig.ToString();
            if (signature != _signature)
            {
                _signature = signature;
                Rebuild(glass, mint, olive);
            }
            PlaceFloats(glass);
        }

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

            if (glass.HasPreparation("salt_rim") || glass.HasPreparation("sugar_rim"))
            {
                bool salt = glass.HasPreparation("salt_rim");
                var band = NewChild("Crust", new Vector2(interiorW + 8f, 7f),
                    new Vector2(0, rimYLocal + 2f));
                var img = band.gameObject.AddComponent<Image>();
                img.sprite = salt ? SaltBand() : SugarBand();
                img.raycastTarget = false;
            }

            if (glass.HasPreparation("lemon_twist"))
            {
                var wedge = NewChild("Wedge", new Vector2(30f, 30f),
                    new Vector2(interiorW * 0.5f + 2f, rimYLocal + 4f));
                var img = wedge.gameObject.AddComponent<Image>();
                // A WEDGE, not a wheel (2026-08-26). prep_lemon is a slice seen face on -
                // a cross-section lying on a plate - and hooking one over a rim drew a coin
                // balanced on the glass. glass_lemon is cut from a lemon.
                img.sprite = ItemArt.Load("glass_lemon") ?? ItemArt.Prep("lemon_twist");
                img.preserveAspect = true; img.raycastTarget = false;
                if (img.sprite == null) img.color = UITheme.Amber[3];
                wedge.localRotation = Quaternion.Euler(0, 0, -18f);
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
            if (mint) _mint = Float("Mint", "glass_mint", "garnish_mint",
                                    new Vector2(26f, 27f), -14f, -8f);
            if (olive) _olive = Float("Olive", "glass_olive", "garnish_olive",
                                      new Vector2(22f, 40f), 10f, 20f);
        }

        private RectTransform Float(string name, string art, string fallback,
                                   Vector2 size, float x, float lean)
        {
            var piece = NewChild(name, size, new Vector2(x, 0));
            var img = piece.gameObject.AddComponent<Image>();
            img.sprite = ItemArt.Load(art) ?? ItemArt.Load(fallback);
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
            img.sprite = ItemArt.Load("glass_ice") ?? PrefArt.Ice() ?? ItemArt.Prep("ice");
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
            for (int n = 0; n < _ice.Count; n++)
            {
                if (_ice[n] == null) continue;
                var lay = CubeLay[Mathf.Min(n, CubeLay.Length - 1)];
                _ice[n].anchoredPosition = new Vector2(lay.x, y + lay.dy);
            }
            if (_mint != null) _mint.anchoredPosition = new Vector2(_mint.anchoredPosition.x, y + 4f);
            if (_olive != null) _olive.anchoredPosition = new Vector2(_olive.anchoredPosition.x, y - 8f);
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

        private static Sprite Speckles(Color32 tone)
        {
            const int W = 48, H = 6;
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
                    // Denser along the middle rows, so the crust reads as clinging to the rim.
                    int chance = y == 0 || y == H - 1 ? 6 : 3;
                    if ((hash >> 8) % (uint)chance == 0) px[y * W + x] = tone;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }
    }
}
