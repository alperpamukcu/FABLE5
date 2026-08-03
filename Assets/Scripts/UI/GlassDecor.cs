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
        private RectTransform _ice1, _ice2, _mint, _olive;
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
            _ice1 = _ice2 = _mint = _olive = null;
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
                img.sprite = ItemArt.Prep("lemon_twist");
                img.preserveAspect = true; img.raycastTarget = false;
                if (img.sprite == null) img.color = UITheme.Amber[3];
                wedge.localRotation = Quaternion.Euler(0, 0, -18f);
            }

            if (glass.HasPreparation("ice"))
            {
                _ice1 = IceCube("Ice1", 20f, -10f);
                _ice2 = IceCube("Ice2", 16f, 12f);
            }

            // The garnish floats. The sprig stands proud of the surface; the spear leans,
            // olives half under. Both ride the fill in PlaceFloats, exactly as the ice does.
            if (mint) _mint = Float("Mint", "garnish_mint", new Vector2(26f, 27f), -14f, -8f);
            if (olive) _olive = Float("Olive", "garnish_olive", new Vector2(30f, 31f), 10f, 34f);
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

        private RectTransform IceCube(string name, float size, float x)
        {
            var cube = NewChild(name, new Vector2(size, size), Vector2.zero);
            var img = cube.gameObject.AddComponent<Image>();
            // The licence's own cube (the author picked it): the same PrefArt sprite, so
            // the ask on the card and the cube in the glass wear one face.
            img.sprite = PrefArt.Ice();
            if (img.sprite == null) img.sprite = ItemArt.Prep("ice");
            img.preserveAspect = true; img.raycastTarget = false;
            if (img.sprite == null) img.color = new Color(0.75f, 0.9f, 1f, 0.9f);
            img.color = new Color(img.color.r, img.color.g, img.color.b, 0.92f);
            cube.localRotation = Quaternion.Euler(0, 0, x * 2.4f);   // a fixed, deterministic tilt
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
            if (_ice1 != null)
            {
                _ice1.anchoredPosition = new Vector2(_ice1.anchoredPosition.x, y - 6f);
                _ice2.anchoredPosition = new Vector2(_ice2.anchoredPosition.x, y - 11f);
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
