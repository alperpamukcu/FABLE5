using System.Collections.Generic;
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
        //   a tab       the aisle is PUSHED: the old shelf slides away as a ghost, the new one arrives from the other
        //               side, the key rises into the open file and its lit edge grows out of the middle (2026-09-27)
        //   a shelf     the upgrade rail's shelves turn the same way, down the rail from the right; the rail itself
        //               fades in with its department and out with it
        //   the card    the hover card opens like the drinkers' balloons (PopIn, on the card itself)
        //
        // The tablet's own way in and out is the day end's slide: the slip leaving left while the van arrives from the
        // right (a second slot since 2026-09-27 - until then only the tablet moved and the slip simply vanished), and
        // the tablet pulling away into the curtain.

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

        // ── the aisle turns (2026-09-27; the fields and the why are in TycoonHud.cs) ──────────────────────────────────
        //
        // It replaced FadeInAisle, which faded the VIEWPORT up ten units. Moving the viewport was the wrong rect: the
        // upgrade rail re-writes that rect's left edge when its department opens or shuts, and read off the code, two
        // clicks across the upgrade tab inside one fade would have left it holding a home 104 units off - the mask run
        // over the scroll track or over the rail, with nothing to ever put it back. The push moves only the content's X
        // (a vertical ScrollRect never writes it) and the viewport's group alpha, and leaves the viewport to the rail.

        /// <summary>The market's own clock: real seconds, shortened by the ceremony pace the suite runs at. Only a
        /// DURATION is ever divided by it - where a movement ends is the same at any pace, and nothing that decides
        /// anything reads it.</summary>
        private static float MarketSpan(float seconds) =>
            seconds / Mathf.Max(1f, LastCall.Game.Ceremony.Pace);

        /// <summary>Takes every control under a LEAVING thing (the ghost shelf, a rail on its way out) off the keyboard's
        /// arrows, without the disabled tint a non-interactable group would paint over it. The pointer is kept off by
        /// the thing's own blocksRaycasts; nothing leaving is ever used again, so nothing puts this back.</summary>
        private static void OutOfNavigation(Transform root)
        {
            if (root == null) return;
            foreach (var s in root.GetComponentsInChildren<Selectable>(true))
            {
                var nav = s.navigation;
                if (nav.mode == Navigation.Mode.None) continue;
                nav.mode = Navigation.Mode.None;
                s.navigation = nav;
            }
        }

        private static void SetX(RectTransform rt, float x)
        {
            if (rt == null) return;
            var p = rt.anchoredPosition;
            if (p.x != x) rt.anchoredPosition = new Vector2(x, p.y);
        }

        /// <summary>One frame of every market movement that is not a flight: the keys, the aisle, the rail and the two
        /// message boxes' scrims. Each reads its own target, so a rebuild in the middle of any of them changes what it
        /// is going TO, never where it is.</summary>
        private void StepMarketMotion()
        {
            StepShopTabKeys();
            StepAisleSwap();
            StepDecorRail();
            StepDialogScrims();
        }

        /// <summary>Everything the market is moving, put where it was going, now: the tablet is leaving, the next night
        /// is being dealt, or a new night's books are coming up over a market left mid-turn.</summary>
        private void SettleMarketMotion()
        {
            SettleAisleSwap();
            SettleDecorRail();
            if (_tabKeyT >= 0f) SettleShopTabKeys();
            SettleBillOut();
        }

        /// <summary>Asks the next rebuild to turn the aisle: +1 brings the new shelf in from the right. Reduced motion
        /// asks for nothing, and the rebuild destroys the old shelf as it always did.</summary>
        private void QueueAisleSwap(int dir)
        {
            _aisleSwapDir = Motion.Reduced ? 0 : dir;
        }

        /// <summary>
        /// Called where the rebuild used to destroy the old shelf. When a turn is queued the shelf is LIFTED OUT of the
        /// scroll content instead, into a ghost standing exactly where it stood: a masked rect over the OLD viewport
        /// (the upgrade rail may be about to move the real one), holding a copy of the content rect at its old scroll,
        /// so not a pixel of it moves on the frame of the press. Returns false when there is no turn.
        ///
        /// The ghost takes no raycasts and answers to no name the suite looks for: every "Tile" and "Name" in it is
        /// renamed, because NthTile and TileNamed find listings by exactly those names, and a ghost tile found first is
        /// a press on a shelf that is sliding away. It sits right over the aisle and under the scroll track, the foot
        /// and the rail, so it can only ever paint over the page it is leaving.
        /// </summary>
        private bool LiftAisleIntoGhost()
        {
            int dir = _aisleSwapDir;
            _aisleSwapDir = 0;
            if (dir == 0 || Motion.Reduced || _offerRow == null || _shopScroll == null || _dayEndStep != 1) return false;
            var view = _shopScroll.viewport;
            if (view == null || view.parent == null) return false;
            // A TURN ON A TURN (2026-09-27, review): the shelf still arriving is lifted from where it is DRAWN - its X
            // and its brightness - instead of being snapped home first, which was a one-frame jump in both. The real
            // rects still start from rest (content at 0, the viewport whole, then sent to the side), and only the
            // ghost of the turn before goes at once: by the time a hand clicks again it is most of the way faded.
            bool midTurn = _aisleSwapT >= 0f;
            float fromX = midTurn ? _offerRow.anchoredPosition.x : 0f;
            float fromA = midTurn && _aisleGroup != null ? _aisleGroup.alpha : 1f;
            SettleAisleSwap();   // the last ghost goes, the shelf is home

            var ghost = NewRect("AisleGhost", view.parent);
            ghost.SetSiblingIndex(view.GetSiblingIndex() + 1);
            ghost.anchorMin = view.anchorMin;
            ghost.anchorMax = view.anchorMax;
            ghost.pivot = view.pivot;
            ghost.offsetMin = view.offsetMin;
            ghost.offsetMax = view.offsetMax;
            ghost.gameObject.AddComponent<RectMask2D>();
            var group = ghost.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = fromA;
            // NOT interactable = false (review, 2026-09-27): a group that turns it off sends every Button under it to
            // the default disabled tint over 0.1 s, so the plates went half-transparent grey before their captions had
            // begun to fade. blocksRaycasts keeps the pointer off, and each Selectable's navigation is set to None below
            // so the keyboard's arrows cannot walk onto a leaving tile either - a change that repaints nothing.
            var content = NewRect("GhostContent", ghost);
            content.anchorMin = _offerRow.anchorMin;
            content.anchorMax = _offerRow.anchorMax;
            content.pivot = _offerRow.pivot;
            content.sizeDelta = _offerRow.sizeDelta;
            content.anchoredPosition = new Vector2(fromX, _offerRow.anchoredPosition.y);
            // Collected first: a Transform's own enumerator breaks when its children are taken away under it.
            var old = new List<Transform>(_offerRow.childCount);
            foreach (Transform child in _offerRow) old.Add(child);
            foreach (var child in old) child.SetParent(content, false);
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
                if (t.name == "Tile" || t.name == "Name") t.name = "Ghost" + t.name;
            OutOfNavigation(content);

            _aisleGhost = ghost;
            _aisleGhostContent = content;
            _aisleGhostGroup = group;
            _aisleGhostX0 = fromX;
            _aisleGhostA0 = fromA;
            _aisleGroup = view.GetComponent<CanvasGroup>();
            if (_aisleGroup == null) _aisleGroup = view.gameObject.AddComponent<CanvasGroup>();   // not ??: the editor's fake null
            _aisleSwapSide = dir;
            _aisleSwapT = 0f;
            // The new shelf starts off to its side and unseen - but LIVE: its tiles take the pointer from the first
            // frame, exactly where they are drawn, so a quick hand never presses into a wall.
            _aisleGroup.alpha = 0f;
            SetX(_offerRow, dir * AislePush);
            return true;
        }

        private void StepAisleSwap()
        {
            if (_aisleSwapT < 0f) return;
            _aisleSwapT += Time.unscaledDeltaTime;
            float k = Motion.Reduced ? 1f : Mathf.Clamp01(_aisleSwapT / MarketSpan(AisleSwapDur));
            if (k >= 1f) { SettleAisleSwap(); return; }
            float e = Tweening.OutCubic(k);
            // Content X only. The ScrollRect owns Y (it clamps it back every LateUpdate) and never writes X on a
            // vertical-only scroll, so the push and the player's wheel can share the rect without meeting.
            // On whole units (review, 2026-09-27): the tiles' names are the pixel face.
            SetX(_offerRow, Mathf.Round(_aisleSwapSide * AislePush * (1f - e)));
            if (_aisleGroup != null) _aisleGroup.alpha = e;
            SetX(_aisleGhostContent, _aisleGhostX0 - Mathf.Round(_aisleSwapSide * AislePush * e));
            if (_aisleGhostGroup != null) _aisleGhostGroup.alpha = _aisleGhostA0 * (1f - e);
        }

        /// <summary>The shelf home at exactly 0 and fully drawn, the ghost gone. Safe to call at rest.</summary>
        private void SettleAisleSwap()
        {
            _aisleSwapT = -1f;
            SetX(_offerRow, 0f);
            if (_aisleGroup != null) _aisleGroup.alpha = 1f;
            if (_aisleGhost != null) Destroy(_aisleGhost.gameObject);
            _aisleGhost = null;
            _aisleGhostContent = null;
            _aisleGhostGroup = null;
        }

        // ── the keys rise (2026-09-27) ───────────────────────────────────────────────────────────────────────────────
        //
        // The rebuild used to write every key's height, colour and lit edge outright, on EVERY rebuild - every pick,
        // wear and order - so a key could not ease without being snapped by the next click. It only AIMS now; this
        // step draws. The face colour goes through HoverWarm.Repaint: the key just clicked is always under the pointer,
        // its HoverWarm had captured the paper colour on enter, and behind its back the old direct write was undone -
        // read off the code, the open tab went back to paper under its white title the moment the pointer left it.

        /// <summary>Points the keys at the open tab; starts their ease if that changed since they were last aimed. The
        /// first dressing, a hidden market and reduced motion snap.</summary>
        private void AimShopTabKeys()
        {
            if (_tabKeysFor == _shopTab || _shopTabKeys[0] == null) return;
            bool first = _tabKeysFor < 0;
            _tabKeysFor = _shopTab;
            for (int i = 0; i < _tabKeyV.Length; i++) _tabKeyFrom[i] = _tabKeyV[i];
            _tabKeyT = 0f;
            if (first || Motion.Reduced || !MarketIsUp) SettleShopTabKeys();
        }

        private void SettleShopTabKeys()
        {
            _tabKeyT = -1f;
            for (int i = 0; i < _tabKeyV.Length; i++)
            {
                _tabKeyV[i] = i == _shopTab ? 1f : 0f;
                PaintTabKey(i, _tabKeyV[i]);
            }
        }

        private void StepShopTabKeys()
        {
            AimShopTabKeys();
            if (_tabKeyT < 0f) return;
            _tabKeyT += Time.unscaledDeltaTime;
            float k = Motion.Reduced ? 1f : Mathf.Clamp01(_tabKeyT / MarketSpan(AisleSwapDur));
            if (k >= 1f) { SettleShopTabKeys(); return; }
            float e = Tweening.OutCubic(k);
            for (int i = 0; i < _tabKeyV.Length; i++)
            {
                _tabKeyV[i] = Mathf.Lerp(_tabKeyFrom[i], i == _shopTab ? 1f : 0f, e);
                PaintTabKey(i, _tabKeyV[i]);
            }
        }

        /// <summary>
        /// One key at a given openness. It grows UPWARD only: the key's pivot is its bottom-left corner, which is where
        /// the suite presses it (ScreenPointOf is the pivot) and where it stands on the page's border, so the corner never
        /// moves and nothing moves in X. The lit edge hangs from the top and opens out of its middle.
        /// </summary>
        private void PaintTabKey(int i, float v)
        {
            var face = _shopTabKeys[i];
            if (face == null) return;
            // On EVEN units while it moves (review, 2026-09-27): the caption and the icon are centred on the key, so a
            // height like 33.4 put the pixel face between two rows for the quarter second of the rise. An even height
            // keeps their centre on a whole unit, and an even lit width keeps both its ends on one. Every end value
            // (30 and 38, -160 and -6) is even, so nothing at rest changes.
            float h = 2f * Mathf.Round(Mathf.Lerp(TabRestH, TabLiveH, v) * 0.5f);
            ((RectTransform)face.transform).sizeDelta = new Vector2(TabKeyW, h);
            // The two ends are the constants themselves, not a lerp's float arithmetic landing a hair off them.
            var colour = v >= 1f ? ShopViceDeep : v <= 0f ? ShopPaper : Color.Lerp(ShopPaper, ShopViceDeep, v);
            var warm = face.GetComponent<HoverWarm>();
            if (warm != null) warm.Repaint(colour);
            else face.color = colour;
            var lit = _shopTabLits[i];
            if (lit == null) return;
            lit.enabled = v > 0.001f;
            lit.rectTransform.sizeDelta = new Vector2(2f * Mathf.Round(Mathf.Lerp(-TabKeyW, -6f, v) * 0.5f), 3f);
        }

        // ── the upgrade rail comes and goes (2026-09-27) ────────────────────────────────────────────────────────────

        private void StepDecorRail()
        {
            if (_railT < 0f || _decorRail == null) return;
            _railT += Time.unscaledDeltaTime;
            float k = Motion.Reduced ? 1f : Mathf.Clamp01(_railT / MarketSpan(AisleSwapDur));
            if (k >= 1f) { SettleDecorRail(); return; }
            _railV = Mathf.Lerp(_railFrom, _railUp ? 1f : 0f, Tweening.OutCubic(k));
            PlaceDecorRail();
        }

        /// <summary>The rail where its department says it belongs: up and whole, or away and switched off. Only
        /// switched off HERE, at the end of its fade - never in the frame the department changes.</summary>
        private void SettleDecorRail()
        {
            if (_railT < 0f) return;
            _railT = -1f;
            _railV = _railUp ? 1f : 0f;
            if (_decorRail == null) return;
            PlaceDecorRail();
            if (!_railUp) _decorRail.gameObject.SetActive(false);
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
            // At the medallion's own 32 (2026-09-27): the 32 drawn into 24 was a 0.75x shrink. Its drawing is
            // 22 wide in the canvas, so it still clears the caption at 42.
            Place(medal, new Vector2(0, 0.5f), new Vector2(32, 32), new Vector2(8, 0));
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
            // TWO DECIMALS (2026-09-26): the house sums to five and a piece adds +0.02 to +0.19, so a one-decimal
            // reading would not move when a +0.03 gold tin or a +0.04 refinish kit went in.
            fig.text = run.ComfortNow.ToString("0.00") + " / " + BarRating.MaxStars;
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
