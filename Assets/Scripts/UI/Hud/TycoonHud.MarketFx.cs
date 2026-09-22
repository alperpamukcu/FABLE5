using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    public sealed partial class TycoonHud
    {
        // ── THE MARKET MOVES (2026-09-22) ──────────────────────────────────────────────────────────────────────────
        //
        // The author's eighth list: "market sahnesinde geçişler sepete eklemeler çıkarmalar satın almalar, ekran
        // geçişleri, pop-up açılmaları vs. bunlar için animasyonlar olmalı". The market is rebuilt whole on every
        // change, so its movement lives on a layer above the build that no rebuild touches:
        //
        //   picked      the product flies from the pointer into its chip, and the chip bursts in as it lands
        //   put back    the chip's picture drops and fades where the chip stood
        //   bought      every chip flies up into the account, one after another, and the account swells as it takes them
        //   a tab       the aisle fades up from a few units under its place
        //   the card    the hover card opens like the drinkers' balloons (PopIn, on the card itself)
        //
        // The tablet's own way in and out - the slip leaving left, the van arriving from the right, the tablet pulling
        // away into the curtain - is the day end's slide and is left as it was.

        private RectTransform _shopFx;

        private RectTransform ShopFx()
        {
            if (_shopFx == null && _dayEndPanel != null)
            {
                _shopFx = NewRect("ShopFx", _dayEndPanel);
                Stretch(_shopFx, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            if (_shopFx != null) _shopFx.SetAsLastSibling();
            return _shopFx;
        }

        private bool FxPoint(Vector2 screen, out Vector2 local)
        {
            local = Vector2.zero;
            var fx = ShopFx();
            return fx != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(fx, screen, null, out local);
        }

        private bool FxCentre(RectTransform rt, out Vector2 local)
        {
            local = Vector2.zero;
            if (rt == null) return false;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return FxPoint((c[0] + c[2]) * 0.5f, out local);
        }

        private UiFlight FxFlyer(Sprite art, Vector2 from, Vector2 to, float size)
        {
            var fx = ShopFx();
            if (fx == null || art == null) return null;
            var rt = NewRect("Flyer", fx);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = art;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var fly = rt.gameObject.AddComponent<UiFlight>();
            fly.From = from; fly.To = to;
            return fly;
        }

        private RectTransform CartChip(string key) =>
            _cartChips != null ? _cartChips.Find("Chip_" + key) as RectTransform : null;

        /// <summary>Picked: the product from the pointer into its chip, which bursts in as it lands.</summary>
        private void FlyToBasket(string key, Sprite art, Vector2 fromScreen)
        {
            var chip = CartChip(key);
            if (chip == null) return;
            var pop = chip.GetComponent<PopIn>();
            if (art == null || Motion.Reduced || !FxPoint(fromScreen, out var from) || !FxCentre(chip, out var to))
            {
                if (pop != null) pop.Play();
                return;
            }
            chip.localScale = new Vector3(0.01f, 0.01f, 1f);          // waiting for its bottle
            var fly = FxFlyer(art, from, to, 64f);
            if (fly == null) { chip.localScale = Vector3.one; if (pop != null) pop.Play(); return; }
            fly.Seconds = 0.36f; fly.ScaleFrom = 1.1f; fly.ScaleTo = 0.7f; fly.Arc = 60f;
            fly.Done = () => { if (chip != null) { chip.localScale = Vector3.one; if (pop != null) pop.Play(); } };
        }

        /// <summary>Put back: the chip's picture drops and fades where it stood (called before the rebuild).</summary>
        private void GhostChip(string key, Sprite art)
        {
            var chip = CartChip(key);
            if (chip == null || art == null || !FxCentre(chip, out var at)) return;
            var fly = FxFlyer(art, at, at + new Vector2(0f, -26f), 44f);
            if (fly == null) return;
            fly.Seconds = 0.26f; fly.ScaleTo = 0.35f; fly.AlphaTo = 0f; fly.Arc = 0f;
        }

        /// <summary>Bought: every chip up into the account, one after another; the account swells as each arrives.</summary>
        private void FlyCartToTill()
        {
            if (_cartChips == null || _tillFloats == null || !FxCentre(_tillFloats, out var till)) return;
            var punch = _tillFloats.GetComponent<UiPunch>();
            if (punch == null) punch = _tillFloats.gameObject.AddComponent<UiPunch>();
            int n = 0;
            foreach (Transform child in _cartChips)
            {
                var chip = child as RectTransform;
                if (chip == null || !chip.name.StartsWith("Chip_") || chip.name == "Chip_more") continue;
                var art = chip.Find("Art")?.GetComponent<Image>()?.sprite;
                if (art == null || !FxCentre(chip, out var from)) continue;
                var fly = FxFlyer(art, from, till, 52f);
                if (fly == null) continue;
                fly.Seconds = 0.42f; fly.Delay = n * 0.07f; fly.ScaleTo = 0.35f; fly.AlphaTo = 0.25f; fly.Arc = 70f;
                fly.Done = () => { if (punch != null) punch.Play(); };
                n++;
            }
        }

        /// <summary>A tab opened: the aisle fades up into its place.</summary>
        private void FadeInAisle()
        {
            if (_shopScroll == null) return;
            var fade = _shopScroll.GetComponent<UiFadeIn>();
            if (fade == null) fade = _shopScroll.gameObject.AddComponent<UiFadeIn>();
            fade.Offset = new Vector2(0f, -10f);
            fade.Play();
        }

        // ── the room's comfort, on the upgrade screen ───────────────────────────────────────────────────────────────

        /// <summary>
        /// WHAT THE ROOM IS WORTH NOW (2026-09-22, the eighth list: "Markette yükseltme ekranında mevcut konforumuzu
        /// görüntüleyebilmeliyiz"): a band over the fittings with the medallion strip, the reading and what it is, so every
        /// rung's "+0.5 comfort" can be read against where the room stands.
        /// </summary>
        private void ComfortBand(TycoonRun run)
        {
            if (_offerRow == null || run == null) return;
            var h = NewRect("Comfort", _offerRow);
            h.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            var band = NewRect("Band", h);
            Stretch(band, Vector2.zero, Vector2.one, new Vector2(0, 3), new Vector2(0, -3));
            var bi = band.gameObject.AddComponent<Image>();
            bi.sprite = ChromeArt.Card();
            bi.type = Image.Type.Sliced;
            bi.color = UITheme.Night[1];
            bi.raycastTarget = false;
            var medal = NewRect("Medal", band);
            Place(medal, new Vector2(0, 0.5f), new Vector2(24, 24), new Vector2(10, 0));
            medal.pivot = new Vector2(0, 0.5f);
            var mi = medal.gameObject.AddComponent<Image>();
            mi.sprite = ItemArt.Medal(true, 32f);
            mi.preserveAspect = true;
            mi.raycastTarget = false;
            var cap = NewText("Cap", band, _shop, 16, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            Place(cap.rectTransform, new Vector2(0, 0.5f), new Vector2(300, 20), new Vector2(42, 0));
            cap.rectTransform.pivot = new Vector2(0, 0.5f);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            cap.text = UIText.T("decor.comfort.now");
            float x = 42f + cap.preferredWidth + 12f;
            var fig = NewText("Fig", band, _figures, 16, TextAnchor.MiddleLeft, UITheme.Amber[4]);
            Place(fig.rectTransform, new Vector2(0, 0.5f), new Vector2(80, 20), new Vector2(x, 0));
            fig.rectTransform.pivot = new Vector2(0, 0.5f);
            fig.horizontalOverflow = HorizontalWrapMode.Overflow;
            fig.text = run.ComfortNow.ToString("0.0") + " / " + BarRating.MaxStars;
            x += fig.preferredWidth + 14f;
            // IconStrip hands back the strip's FILL; its row is the fill's parent
            var fill = IconStrip(band, "Strip", ItemArt.Medal(false, 16f), ItemArt.Medal(true, 16f), 0f);
            if (fill != null)
            {
                if (fill.parent is RectTransform row) row.anchoredPosition = new Vector2(x, 0f);
                fill.sizeDelta = new Vector2((float)(run.ComfortNow / BarRating.MaxStars) * HouseStripW, 0);
            }
            var note = NewText("Note", band, _body, 8, TextAnchor.MiddleRight, UITheme.Cream[2]);
            note.rectTransform.anchorMin = note.rectTransform.anchorMax = new Vector2(1, 0.5f);
            note.rectTransform.pivot = new Vector2(1, 0.5f);
            note.rectTransform.sizeDelta = new Vector2(320, 14);
            note.rectTransform.anchoredPosition = new Vector2(-12f, 0f);
            note.horizontalOverflow = HorizontalWrapMode.Overflow;
            note.text = UIText.T("decor.comfort.note");
        }
    }
}
