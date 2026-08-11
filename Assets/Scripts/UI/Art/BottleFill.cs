using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// What is left in a bottle, drawn BEHIND its art and clipped to it.
    ///
    /// This layer has been built and torn out twice, and both times for the same reason
    /// stated two ways (2026-08-11: "sıvılar sağdan soldan taşıyor", then "şişeler boş
    /// görünüyor"). It was a coloured rectangle in a rectangular rect: the art inside that
    /// rect is letterboxed by <c>preserveAspect</c> and is a bottle-shaped hole in the
    /// middle of it, so the drink appeared as bars down either side of the glass and
    /// nowhere useful. Sizing the rect by hand only moves that problem to the next bottle,
    /// because thirty sprites arrive from an art pipeline and not one of them is a
    /// rectangle.
    ///
    /// So the fill is not sized against the art — it is CUT OUT BY IT. The bottle's own
    /// sprite is the stencil, which means the drink can only appear where there is glass
    /// for it to appear through: it narrows into the neck because the glass does, it stops
    /// dead at the outline, and it cannot be wrong for a silhouette nobody has measured.
    /// The opaque parts of the drawing — the label, the cap — cover it, because the art is
    /// drawn afterwards.
    ///
    /// It is drawn at full alpha on purpose. The transparency belongs to the GLASS and is
    /// already baked into the sprite in front of it; a translucent drink behind a
    /// translucent bottle is a drink seen through two panes, which is how it went grey the
    /// first time.
    /// </summary>
    public sealed class BottleFill
    {
        private readonly GameObject _root;
        private readonly Image _stencil;      // the bottle sprite, drawn only to cut the hole
        private readonly RectTransform _drink;
        private readonly Image _drinkImage;

        private BottleFill(GameObject root, Image stencil, RectTransform drink, Image image)
        {
            _root = root; _stencil = stencil; _drink = drink; _drinkImage = image;
        }

        /// <summary>
        /// Builds the layer filling <paramref name="under"/>, as its FIRST child — so
        /// anything the caller adds afterwards draws in front of the drink.
        ///
        /// The caller must not put the bottle's art on <paramref name="under"/> itself: a
        /// parent Graphic draws before its children, so art on the parent would end up
        /// BEHIND the drink however the children are ordered. Give the parent the hit plate
        /// (or nothing) and the art its own child rect.
        /// </summary>
        public static BottleFill Under(RectTransform under)
        {
            var clip = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer),
                                      typeof(Image), typeof(Mask));
            var rt = (RectTransform)clip.transform;
            rt.SetParent(under, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            var stencil = clip.GetComponent<Image>();
            stencil.preserveAspect = true;      // exactly how the art in front is fitted
            stencil.raycastTarget = false;
            clip.GetComponent<Mask>().showMaskGraphic = false;

            var drinkGo = new GameObject("Drink", typeof(RectTransform), typeof(CanvasRenderer),
                                         typeof(Image));
            var drt = (RectTransform)drinkGo.transform;
            drt.SetParent(rt, false);
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            drt.localScale = Vector3.one;
            var image = drinkGo.GetComponent<Image>();
            image.raycastTarget = false;

            rt.SetAsFirstSibling();
            return new BottleFill(clip, stencil, drt, image);
        }

        /// <summary>Fill this bottle to <paramref name="fraction"/> of what it holds. An
        /// empty bottle, or one with no art to be cut out by, shows nothing at all.</summary>
        public void Show(Sprite sprite, Color tone, double fraction, float alpha = 1f)
        {
            if (sprite == null || fraction <= 0.0) { Hide(); return; }
            if (!_root.activeSelf) _root.SetActive(true);
            _stencil.sprite = sprite;
            _drinkImage.color = new Color(tone.r, tone.g, tone.b, alpha);
            // The surface rides the TOP edge; the foot stays on the floor of the bottle — of
            // the BOTTLE, which is not the same as the floor of the sheet it was saved on. The
            // level is measured across the drawing (VesselArt), so a carton drawn small and
            // centred on its sheet reads half full at its own half and not at the sheet's,
            // where the stencil would have cut the drink off somewhere around its label.
            var m = VesselArt.Of(sprite);
            float lo = m.Sheet.y > 0f ? m.Drawing.yMin / m.Sheet.y : 0f;
            float hi = m.Sheet.y > 0f ? m.Drawing.yMax / m.Sheet.y : 1f;
            _drink.anchorMin = new Vector2(0f, lo);
            _drink.anchorMax = new Vector2(1f, Mathf.Lerp(lo, hi, Mathf.Clamp01((float)fraction)));
        }

        public void Hide()
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }
    }
}
