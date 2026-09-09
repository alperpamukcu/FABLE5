using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Chrome: the top bar, the week, the notice line and the dev sheets.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    public sealed partial class TycoonHud
    {
        /// <summary>
        /// Escape shuts whatever sheet is open, topmost first (2026-08-11, the author).
        ///
        /// Order matters and it is the drawing order backwards: the thing lying over
        /// everything else is the thing the key belongs to. Without that, Escape over the
        /// book would close the licence underneath it and leave the board sitting there.
        /// </summary>
        private void UpdateEscape()
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys == null || !keys.escapeKey.wasPressedThisFrame) return;
            if (_bookOpen) { ToggleRecipeBook(); return; }
            if (Showing(_settingsPanel)) { ToggleSettings(); return; }
            // The bench is above the guide, so Escape must reach it first — a panel that
            // covers another and cannot be closed over it is a trap.
            if (Showing(_devPanel)) { _devPanel.gameObject.SetActive(false); return; }
            if (Showing(_guidePanel)) { _guidePanel.gameObject.SetActive(false); return; }
            if (Showing(_ledgerPanel)) { _ledgerPanel.gameObject.SetActive(false); return; }
            if (Showing(_idRoot)) { _idRoot.gameObject.SetActive(false); _idVisit = null; return; }
            // The market (2026-08-19): now that the title bar's close box is gone the foot
            // key is the one exit, and a fullscreen panel with one small exit and no Escape
            // is a trap. Escape walks the SAME door — the ask first if it is up (Escape on
            // a question is "go back", never "do it"), else the guarded advance, so the
            // basket warning can never be skipped past with a key.
            if (Showing(_dayEndPanel) && _dayEndStep == 1)
            {
                if (Showing(_hostNote)) { OnHostNoteKey(); return; }
                if (Showing(_closingAsk)) { _closingAsk.gameObject.SetActive(false); return; }
                OnDayEndAdvance();
                return;
            }
            if (_flow != null && _flow.IsOpen) _flow.CloseFlow();
        }

        /// <summary>Is the counter's cellar open? Asked by everything that must get out of its
        /// way — the drinkers' tickets, their clocks, and the stool that must not be served
        /// through while the room is lifted.</summary>
        private bool CellarOpen => stage != null && stage.DrawerPhase > 0.01f;

        /// <summary>A short notice under the top bar — refusals, mostly (GDD 24 §7).</summary>
        /// <summary>The bar's one notice line. Public since the bench got its own bin
        /// (2026-08-22): a discard says the same sentence wherever it is done from, and a
        /// second message channel on the bench would be a second thing to keep in step.</summary>
        public void Toast(string message)
        {
            Toast(message, null);
        }

        /// <summary>The same notice line, in a colour and for a length of its own. The
        /// channel was built vice-red and refusal-shaped; a first perfect pour is the
        /// opposite kind of news and cannot arrive wearing the same coat (2026-08-25).
        /// A null tint restores the refusal ink, so no caller has to put it back.</summary>
        public void Toast(string message, Color? tint, float seconds = 1.6f)
        {
            Toast(message, tint, seconds, null);
        }

        /// <summary>
        /// ...AND WITH THE THING IT IS ABOUT BESIDE IT (2026-09-06, the author: "bildirimlerde
        /// alkollerin nesnelerin paranın yıldızın ve benzeri nesnelerin kullanım durumunda
        /// iconlarından faydalan"). A notice that carries the coin, the star or the bottle it
        /// concerns is read before it is read: the picture lands while the eye is still on the
        /// counter. Null draws no icon and the line sits where it always has.
        /// </summary>
        public void Toast(string message, Color? tint, float seconds, Sprite icon)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toast.color = tint ?? _toastInk;
            _toastUntil = Time.unscaledTime + seconds;
            _toast.gameObject.SetActive(true);
            if (_toastIcon == null) return;
            _toastIcon.sprite = icon;
            _toastIcon.enabled = icon != null;
            _toastIcon.color = tint ?? Color.white;
            // The line shifts right to make room for it, and comes back when there is none.
            var rt = _toast.rectTransform;
            var off = rt.offsetMin;
            rt.offsetMin = new Vector2(icon != null ? ToastIconRoom : 0f, off.y);
        }

        /// <summary>How much room the notice gives up when it carries a picture.</summary>
        private const float ToastIconRoom = 22f;

        /// <summary>
        /// The first perfect pour of a recipe, told three ways (2026-08-25, the author:
        /// "bildirim gelmeli ... menüden bakabileceğine yönlendirmeli ... menünün
        /// girişindeki sayfada bildirimi olmalı"): a gold notice in the moment, a mark on
        /// the BOOK key for after it fades, and a pressable line on the book's title page
        /// that opens straight at the page it is about. The book's own page has been the
        /// reward since the cookbook (platinum, exact shares); this is what tells the
        /// player to go and look at it.
        /// </summary>
        private void NotePerfect(RecipeDefinition recipe)
        {
            if (recipe == null) return;
            if (!_perfectNews.Contains(recipe.Id)) _perfectNews.Add(recipe.Id);
            Toast("PERFECT POUR · " + recipe.Name.ToUpperInvariant() + " — IN THE BOOK NOW",
                BkPlatinum, 3.4f);
            Sfx.Play("cheer_sfx", 0.5f);
            RefreshBookBadge();
        }

        // ── refresh ─────────────────────────────────────────────────────────────

        /// <summary>Pushes the bought glassware onto the bar — the under-counter rack is the
        /// one fitting the picture still shows (the stage's own tier tint retired with the
        /// pour-glass HUD in the 2026-08-07 sweep).</summary>
        private void ApplyBarLook()
        {
            var run = Run;
            if (run == null) return;
            RefreshGlassRack(run);
        }

        // ── what a prop does, said before it is pressed (2026-08-26) ─────────────
        //
        // The author: "bu tarz etkileşimlerde etkileşime girilen nesnenin üzerinde ne olduğu
        // yazmalı, örneğin menünün üstüne gelindiğinde menüyü aç demek olduğunu biliyorsun."
        // The recipe book has said OPEN THE MENU on hover since 2026-08-25 and it was the
        // only prop in the room that did. Everything else — the sink, the beer font, the six
        // things on the garnish rail — was a drawing you had to press to find out about.
        //
        // ONE PLATE, not one per prop. The book's own label is a child of the book and
        // follows it; six more of those would be six more rects riding the counter's lift.
        // This one lives on the HUD root, is told which rect to stand over, and converts
        // through the screen — so it works for a prop on the stage's own canvas (the sink,
        // the font) exactly as it does for one on the HUD's.
        private RectTransform _propTip;
        private Text _propTipText, _propTipDetail;
        private Image _propTipIcon;
        private System.Func<string> _propTipDetailFn;   // a line re-read each frame (the beam's readings)
        private CanvasGroup _propTipGroup;
        private RectTransform _propTipOver;
        private const float PropTipFade = 0.12f;

        private void BuildPropTip(RectTransform root)
        {
            _propTip = NewRect("PropTip", root);
            _propTip.anchorMin = _propTip.anchorMax = new Vector2(0.5f, 0.5f);
            _propTip.pivot = new Vector2(0.5f, 0f);
            _propTip.sizeDelta = new Vector2(180f, 22f);
            var plate = _propTip.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.Card();
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.Night[1];
            plate.raycastTarget = false;
            _propTipText = NewText("Line", _propTip, _display, 8, TextAnchor.MiddleCenter,
                                   UITheme.Amber[4]);
            Stretch(_propTipText.rectTransform, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero);
            _propTipText.raycastTarget = false;
            _propTipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            // A READING'S TIP CARRIES ITS MARK AND A LINE (2026-09-06): the icon at the left,
            // the name beside it, one line under. Both stay off until a caller hands them
            // something, so a prop's plain word is the one-row tip it always was.
            var iconRt = NewRect("Icon", _propTip);
            Place(iconRt, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(8f, 0));
            iconRt.pivot = new Vector2(0, 0.5f);
            _propTipIcon = iconRt.gameObject.AddComponent<Image>();
            _propTipIcon.preserveAspect = true;
            _propTipIcon.raycastTarget = false;
            _propTipIcon.enabled = false;
            _propTipDetail = NewText("Detail", _propTip, _body, 8, TextAnchor.LowerLeft, UITheme.Cream[3]);
            Stretch(_propTipDetail.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(30f, 5f), new Vector2(-8f, -20f));
            _propTipDetail.raycastTarget = false;
            _propTipDetail.horizontalOverflow = HorizontalWrapMode.Overflow;
            _propTipDetail.enabled = false;
            _propTipGroup = _propTip.gameObject.AddComponent<CanvasGroup>();
            _propTipGroup.alpha = 0f;
            _propTipGroup.blocksRaycasts = false;
            _propTipGroup.interactable = false;
            UiAuditExempt.Mark(_propTip, "the hover caption stands over whatever prop the "
                + "pointer is on, in that prop's own place rather than in a fixed one");
        }

        // ── THE CELLAR'S CARD (2026-09-07, the author: "mahzendeki alkollerin üstüne
        // gelindiğinde gözüken bilgi barını sevmedim … büyük boy bir kart çıkar, içerisinde
        // kullanıldığı tarifleri, ismini, fiyatını gibi detaylı, küçük kompakt bir tasarımı
        // olsun"). One card, built once, filled for whichever bottle the pointer is on and
        // stood over it the way the prop caption is — through the screen, because the doors
        // live on the stage's own canvas. Compact: a name row, a facts row, the stock, the
        // house drinks it goes into, and the no-shake mark only on a bottle that fizzes.
        private RectTransform _cellarCard, _cellarCardOver, _cellarCardSlot, _cellarCardBody,
            _cellarCardVessel, _cellarCardStars, _cellarCardUsesRoot, _cellarCardMarkRow,
            _cellarCardStockBar, _cellarCardStockFill;
        private CanvasGroup _cellarCardGroup;
        private Text _cellarCardName, _cellarCardMeta, _cellarCardPrice, _cellarCardStock,
            _cellarCardUsesHead, _cellarCardMark;
        private Image _cellarCardIcon, _cellarCardDish, _cellarCardHalo;
        private Text _cellarCardSign;   // the yellow $ before the price
        private BottleArt _cellarCardBottle;
        private bool _cellarCardFree;      // a garnish card stands on the counter, cellar shut or not
        // The author's card at 2x: a 3px rule is 6 units, the slot 84 wide, and its
        // well (36 art px) 72 — a cellar plate (32x64) at 2x stands in it with 4 to spare.
        private const float CardScale = 2f, CardRule = 3f * CardScale;
        // TWENTY-FOUR, not twelve (2026-09-09, measured in play twice): the box is drawn from
        // the author's art with a six art px slice, which lands as TWELVE units of rule at 2x,
        // so a twelve-unit pad left nothing at all under the last line and the no-shake mark
        // printed on the frame itself. Twelve for the rule, twelve for the air, on every side.
        private const float CardPad = 24f, CardBodyMinW = 220f, CardRowGap = 6f;
        private const float CardDrinkIcon = 32f;   // DrinkIcon.Size, as drawn

        // THE SLOT IS A BOX OF ITS OWN, SIZED TO THE BOTTLE (2026-09-09, the author:
        // "gereksiz yere card'ı uzatma, alkolün kapladığı alan kadar boyutu büyüsün aynı
        // açıklamaların olduğu kısımdaki gibi"). The frame numbers are measured off the
        // author's card_slot.png: the rule sits five art px in from the closed side and
        // eight from the top and foot. Air is what the hover's rise and growth need.
        private const float CardSlotFrameX = 5f * CardScale, CardSlotFrameY = 8f * CardScale;
        private const float CardSlotAir = 8f;
        private const float CardSlotMinW = 56f, CardSlotMinH = 76f;

        private int _cellarCardShown = -1;  // the shelf bottle switched off under the card's copy, or -1
        private Vector2 _cellarCardSlotSize = new Vector2(84f, 140f);   // the bottle's box, this card
        private Color _cellarCardTone = Color.white;   // the drink in the copy, re-laid every frame
        private double _cellarCardFrac;
        private Vector2 _cellarCardBodySize = new Vector2(CardBodyMinW, 140f);   // the text's box
        private float _cellarCardStockFrac;    // laid on the bar AFTER the bar has its width

        private void BuildCellarCard(RectTransform root)
        {
            // ON TOP OF EVERYTHING (2026-09-08, the author: "açılan bilgi kartları her zaman
            // hiyerarşide en üstte olmalı, sadece üstüne gelinen asset gözükecek"). The card
            // is on the HUD's own canvas, sorted at 26 — over the shelf captions, the book
            // prop (8), the counter, every sprite. What shows over it is the one thing the
            // pointer is on: the bottle, drawn again in the slot as a UI sandwich — which is
            // unlit, the "ışıklandırılmadan arındırılmış aydınlık" form the author asked for
            // — at the shelf bottle's own place and size, following its sway, while the
            // shelf's renderers are switched off so there is one bottle and it is in front.
            // (A camera-space canvas under a lifted bottle was tried the same day; it lost
            // every bottle's glass on the author's machine.)
            _cellarCard = NewRect("CellarCard", root);
            _cellarCard.anchorMin = _cellarCard.anchorMax = new Vector2(0.5f, 0.5f);
            _cellarCard.pivot = new Vector2(0.5f, 0f);
            _cellarCard.sizeDelta = _cellarCardSlotSize + new Vector2(CardBodyMinW, 0f);
            var cardCanvas = _cellarCard.gameObject.AddComponent<Canvas>();
            cardCanvas.overrideSorting = true;
            cardCanvas.sortingOrder = 26;

            // the slot: its own box beside the text's, as tall and as wide as the bottle in it
            _cellarCardSlot = NewRect("Slot", _cellarCard);
            _cellarCardSlot.anchorMin = _cellarCardSlot.anchorMax = new Vector2(0f, 0.5f);
            _cellarCardSlot.pivot = new Vector2(0f, 0.5f);
            _cellarCardSlot.sizeDelta = _cellarCardSlotSize;
            _cellarCardSlot.anchoredPosition = Vector2.zero;
            var slotImg = _cellarCardSlot.gameObject.AddComponent<Image>();
            slotImg.sprite = ChromeArt.CardSlot();
            slotImg.type = Image.Type.Sliced;
            slotImg.pixelsPerUnitMultiplier = 1f / CardScale;
            slotImg.raycastTarget = false;
            if (slotImg.sprite == null) slotImg.color = UITheme.Night[1];

            // THE HALO COMES WITH THE COPY (2026-09-08, the author: "şişenin hareketiyle
            // arkadaki ışıklandırmanın hareketi bir değil"): the shelf's halo stays off while
            // the card stands, and the same art, tint and rise are drawn here, behind the
            // copy, on the copy's own rect — so light and bottle move as one.
            var haloRt = NewRect("Halo", _cellarCardSlot);
            haloRt.anchorMin = haloRt.anchorMax = new Vector2(0.5f, 0.5f);
            haloRt.pivot = new Vector2(0.5f, 0.5f);
            _cellarCardHalo = haloRt.gameObject.AddComponent<Image>();
            _cellarCardHalo.raycastTarget = false;
            _cellarCardHalo.enabled = false;
            _cellarCardVessel = NewRect("Vessel", _cellarCardSlot);
            _cellarCardVessel.anchorMin = _cellarCardVessel.anchorMax = new Vector2(0.5f, 0.5f);
            _cellarCardVessel.pivot = new Vector2(0.5f, 0.5f);
            _cellarCardVessel.sizeDelta = new Vector2(32f * CardScale, 64f * CardScale);
            _cellarCardVessel.anchoredPosition = Vector2.zero;   // re-laid every frame on the shelf bottle's rect
            _cellarCardBottle = BottleArt.Under(_cellarCardVessel);

            var dishRt = NewRect("Dish", _cellarCardSlot);
            dishRt.anchorMin = dishRt.anchorMax = new Vector2(0.5f, 0.5f);
            dishRt.pivot = new Vector2(0.5f, 0.5f);
            dishRt.sizeDelta = new Vector2(64f, 64f);
            dishRt.anchoredPosition = Vector2.zero;
            _cellarCardDish = dishRt.gameObject.AddComponent<Image>();
            _cellarCardDish.preserveAspect = true;
            _cellarCardDish.raycastTarget = false;
            _cellarCardDish.enabled = false;

            // the body: its own box, as wide and as tall as its text, beside the slot's
            _cellarCardBody = NewRect("Body", _cellarCard);
            _cellarCardBody.anchorMin = _cellarCardBody.anchorMax = new Vector2(1f, 0.5f);
            _cellarCardBody.pivot = new Vector2(1f, 0.5f);
            _cellarCardBody.anchoredPosition = Vector2.zero;
            _cellarCardBody.sizeDelta = _cellarCardBodySize;
            var bodyImg = _cellarCardBody.gameObject.AddComponent<Image>();
            bodyImg.sprite = ChromeArt.CardBody();
            bodyImg.type = Image.Type.Sliced;
            bodyImg.pixelsPerUnitMultiplier = 1f / CardScale;
            bodyImg.raycastTarget = false;
            if (bodyImg.sprite == null) bodyImg.color = UITheme.Night[1];

            Text Line(string name, Font face, int size, Color ink)
            {
                var t = NewText(name, _cellarCardBody, face, size, TextAnchor.UpperLeft, ink);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f);
                t.rectTransform.pivot = new Vector2(0f, 1f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                return t;
            }
            // THE TITLE (the author: "alkolün ismi hem başlık gibi büyük ve kalın olmalı"):
            // Silkscreen Bold at 24, the house's own heavy face at 3x, in the light ink.
            _cellarCardName = Line("Name", _shop, 24, UITheme.Cream[4]);
            _cellarCardMeta = Line("Meta", _body, 16, UITheme.Cream[2]);

            // A YELLOW $ AND A YELLOW FIGURE (2026-09-09, the author: "dolar iconu yerine
            // fiyatlarda sarı $ ve sarı para miktarı kullanılsın"): the drawn coin came off
            // the card, and the sign is typed in the same amber as the money beside it.
            _cellarCardSign = Line("Sign", _display, 16, UITheme.Amber[4]);
            _cellarCardSign.text = "$";
            _cellarCardPrice = Line("Price", _display, 16, UITheme.Amber[4]);

            _cellarCardStock = Line("Stock", _body, 16, UITheme.Cream[3]);
            _cellarCardStockBar = NewRect("StockBar", _cellarCardBody);
            _cellarCardStockBar.anchorMin = _cellarCardStockBar.anchorMax = new Vector2(0f, 1f);
            _cellarCardStockBar.pivot = new Vector2(0f, 1f);
            _cellarCardStockBar.sizeDelta = new Vector2(120f, 8f);
            var barBg = _cellarCardStockBar.gameObject.AddComponent<Image>();
            barBg.color = UITheme.Night[0];
            barBg.raycastTarget = false;
            _cellarCardStockFill = NewRect("Fill", _cellarCardStockBar);
            _cellarCardStockFill.pivot = new Vector2(0f, 0.5f);
            var fillImg = _cellarCardStockFill.gameObject.AddComponent<Image>();
            fillImg.color = UITheme.Cyan[3];
            fillImg.raycastTarget = false;

            _cellarCardUsesHead = Line("UsesHead", _body, 16, UITheme.Cream[3]);
            _cellarCardUsesRoot = NewRect("Uses", _cellarCardBody);
            _cellarCardUsesRoot.anchorMin = _cellarCardUsesRoot.anchorMax = new Vector2(0f, 1f);
            _cellarCardUsesRoot.pivot = new Vector2(0f, 1f);

            _cellarCardMarkRow = NewRect("MarkRow", _cellarCardBody);
            _cellarCardMarkRow.anchorMin = _cellarCardMarkRow.anchorMax = new Vector2(0f, 1f);
            _cellarCardMarkRow.pivot = new Vector2(0f, 1f);
            _cellarCardMarkRow.sizeDelta = new Vector2(200f, 16f);
            var iconRt = NewRect("Icon", _cellarCardMarkRow);
            Place(iconRt, new Vector2(0, 0.5f), new Vector2(16, 16), Vector2.zero);
            iconRt.pivot = new Vector2(0f, 0.5f);
            _cellarCardIcon = iconRt.gameObject.AddComponent<Image>();
            _cellarCardIcon.sprite = ChromeArt.NoShake();
            _cellarCardIcon.color = UITheme.ViceRed[3];
            _cellarCardIcon.preserveAspect = true;
            _cellarCardIcon.raycastTarget = false;
            _cellarCardMark = NewText("Mark", _cellarCardMarkRow, _body, 16, TextAnchor.MiddleLeft, UITheme.ViceRed[3]);
            Stretch(_cellarCardMark.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 0f), Vector2.zero);
            _cellarCardMark.horizontalOverflow = HorizontalWrapMode.Overflow;
            _cellarCardMark.raycastTarget = false;
            _cellarCardMark.text = "NEVER SHAKEN · BUILT AT THE GLASS";

            _cellarCardGroup = _cellarCard.gameObject.AddComponent<CanvasGroup>();
            _cellarCardGroup.alpha = 0f;
            _cellarCardGroup.blocksRaycasts = false;
            _cellarCardGroup.interactable = false;
            UiAuditExempt.Mark(_cellarCard, "the cellar's card stands over whichever bottle the "
                + "pointer is on, in that bottle's own place rather than in a fixed one");
        }

        /// <summary>One line of the "in the book" list: the menu's own drink icon and the
        /// drink's name beside it. Returns the line's width.</summary>
        private float CardUseLine(RecipeDefinition r, float top)
        {
            var row = NewRect("Use_" + r.Id, _cellarCardUsesRoot);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(0f, -top);
            row.sizeDelta = new Vector2(200f, CardDrinkIcon);
            var iconRt = NewRect("Icon", row);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.sizeDelta = new Vector2(CardDrinkIcon, CardDrinkIcon);
            var ii = iconRt.gameObject.AddComponent<Image>();
            ii.sprite = _bootstrap != null ? DrinkIcon.For(r, _bootstrap.Glassware) : null;
            ii.preserveAspect = true;
            ii.raycastTarget = false;
            if (ii.sprite == null) ii.enabled = false;
            var name = NewText("Name", row, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(0f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 0.5f);
            name.rectTransform.anchoredPosition = new Vector2(CardDrinkIcon + 8f, 0f);
            name.rectTransform.sizeDelta = new Vector2(300f, 0f);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.raycastTarget = false;
            name.text = r.Name.ToUpperInvariant();
            name.rectTransform.sizeDelta = new Vector2(name.preferredWidth, 0f);   // its ink, not a guess
            float w = CardDrinkIcon + 8f + name.preferredWidth;
            row.sizeDelta = new Vector2(w, CardDrinkIcon);
            return w;
        }

        /// <summary>Lays the body's rows out top to bottom and sizes the card to them: the
        /// body is as wide as its widest row plus the padding, never under CardBodyMinW,
        /// and the slot takes the card's height (the author: "kartlar metinlerin boyutuna
        /// veya şişenin boyutuna göre genişleyip büyüyebilmeli").</summary>
        private void LayOutCellarCard(int usesRows, float usesW, bool fizzy, bool showStock)
        {
            float x = CardPad, y = CardPad;
            float widest = Mathf.Max(_cellarCardName.preferredWidth, _cellarCardMeta.preferredWidth,
                                     _cellarCardUsesHead.preferredWidth, usesW,
                                     fizzy ? 22f + _cellarCardMark.preferredWidth : 0f);   // the no-shake line, measured
            _cellarCardName.rectTransform.anchoredPosition = new Vector2(x, -y);
            y += _cellarCardName.preferredHeight + 2f;
            _cellarCardMeta.rectTransform.anchoredPosition = new Vector2(x, -y);
            y += _cellarCardMeta.preferredHeight + CardRowGap;
            if (_cellarCardStars != null)
            {
                _cellarCardStars.anchoredPosition = new Vector2(x, -y);
                y += 16f + CardRowGap;
            }
            _cellarCardSign.rectTransform.anchoredPosition = new Vector2(x, -y);
            float signW = Mathf.Max(14f, _cellarCardSign.preferredWidth + 4f);
            _cellarCardPrice.rectTransform.anchoredPosition = new Vector2(x + signW, -y);
            widest = Mathf.Max(widest, signW + _cellarCardPrice.preferredWidth);
            y += Mathf.Max(20f, _cellarCardPrice.preferredHeight) + CardRowGap;
            _cellarCardStock.gameObject.SetActive(showStock);
            _cellarCardStockBar.gameObject.SetActive(showStock);
            if (showStock)
            {
                _cellarCardStock.rectTransform.anchoredPosition = new Vector2(x, -y);
                widest = Mathf.Max(widest, _cellarCardStock.preferredWidth);
                y += _cellarCardStock.preferredHeight + 2f;
                _cellarCardStockBar.anchoredPosition = new Vector2(x, -y);
                y += 8f + CardRowGap;
            }
            _cellarCardUsesHead.rectTransform.anchoredPosition = new Vector2(x, -y);
            y += _cellarCardUsesHead.preferredHeight + 2f;
            _cellarCardUsesRoot.anchoredPosition = new Vector2(x, -y);
            _cellarCardUsesRoot.sizeDelta = new Vector2(usesW, usesRows * (CardDrinkIcon + 2f));
            y += usesRows * (CardDrinkIcon + 2f);
            _cellarCardMarkRow.gameObject.SetActive(fizzy);
            if (fizzy)
            {
                y += CardRowGap;
                _cellarCardMarkRow.anchoredPosition = new Vector2(x, -y);
                _cellarCardMarkRow.sizeDelta = new Vector2(22f + _cellarCardMark.preferredWidth, 16f);
                y += 16f;
            }
            y += CardPad;
            // TWO BOXES, EACH THE SIZE OF WHAT IT HOLDS (2026-09-09): the text's box takes its
            // widest row and its last row, the bottle's box takes the bottle, and the card is
            // as tall as the taller of them. Nothing is stretched to match the other.
            _cellarCardBodySize = new Vector2(Mathf.Max(CardBodyMinW, widest + CardPad * 2f), y);
            _cellarCard.sizeDelta = new Vector2(_cellarCardSlotSize.x + _cellarCardBodySize.x,
                                                Mathf.Max(_cellarCardBodySize.y, _cellarCardSlotSize.y));
            _cellarCardStockBar.sizeDelta = new Vector2(_cellarCardBodySize.x - CardPad * 2f, 8f);
            ApplyCardHand();
            ApplyCardStockFill();
        }

        /// <summary>The bottle's box, sized to the bottle standing in it plus the rule and a
        /// little air — what the hover's rise and growth need to stay inside the well.</summary>
        private void SizeCardSlot(Vector2 art)
        {
            _cellarCardSlotSize = new Vector2(
                Mathf.Max(CardSlotMinW, art.x + (CardSlotFrameX + CardSlotAir) * 2f),
                Mathf.Max(CardSlotMinH, art.y + (CardSlotFrameY + CardSlotAir) * 2f));
        }

        private void ClearCardUses()
        {
            if (_cellarCardUsesRoot == null) return;
            for (int i = _cellarCardUsesRoot.childCount - 1; i >= 0; i--)
                Destroy(_cellarCardUsesRoot.GetChild(i).gameObject);
            if (_cellarCardStars != null) { Destroy(_cellarCardStars.gameObject); _cellarCardStars = null; }
        }

        /// <summary>The same card over a dish on the counter's rail (the author: "şişeler ve
        /// mahzen için kullanacağımız kart UI'ın aynısını garnishler için de kullan"): the
        /// dish in the slot, its name as the title, what it is for, and what it costs and
        /// how much is left when it is a thing the van brings.</summary>
        private void ShowGarnishCard(RectTransform over, PrepProp prop, string word, Sprite icon, string why)
        {
            if (_cellarCard == null || prop == null) return;
            var run = Run;
            var card = GarnishOnTheShelf(run, prop.Style);
            var bottle = card != null ? run?.Shelf.Find(card.Id) : null;
            ClearCardUses();
            _cellarCardBottle.Show(null);
            _cellarCardHalo.enabled = false;
            _cellarCardDish.enabled = false;   // the dish itself rises over the card (its own canvas, 27)
            _cellarCardFrac = 0.0;
            SizeCardSlot(new Vector2(Mathf.Clamp(over.rect.width, 32f, 120f),
                                     Mathf.Clamp(over.rect.height, 32f, 120f)));
            LiftProp(over, true);
            string title = card != null ? card.Name : (prop.Prep != null ? prop.Prep.Name : prop.Id.Replace('_', ' '));
            _cellarCardName.text = title.ToUpperInvariant();
            _cellarCardMeta.text = prop.IsRim ? "RIM  ·  " + word : (prop.Id == "ice" ? "ICE  ·  " + word : "GARNISH  ·  " + word);
            int price = card != null ? Market.StockPrice(card) : 0;
            _cellarCardPrice.text = card != null ? price + " A BOX" : "ON THE HOUSE";
            bool showStock = bottle != null;
            if (showStock)
            {
                float frac = bottle.Capacity > 0 ? (float)(bottle.Remaining / bottle.Capacity) : 0f;
                bool low = bottle.Remaining <= bottle.Capacity * 0.15;
                _cellarCardStock.text = (low ? "ALMOST OUT  ·  " : "") + $"{bottle.Remaining:0.0} OF {bottle.Capacity:0} LEFT";
                _cellarCardStock.color = low ? UITheme.ViceRed[3] : UITheme.Cream[3];
                SetCardStockFill(frac, low ? UITheme.ViceRed[3] : UITheme.Lime[3]);
            }
            _cellarCardUsesHead.text = why ?? "";
            // AND IT SHOWS WHAT IT DOES (2026-09-09, the author: "salted rim garnishinin ana
            // sahnedeki hover tasarımı güzel ama içerisindeki yazılar eksik, daha fazla
            // görsellerden yararlanılmalı"). A line of type saying "the rim run through salt"
            // is the caption for a picture that was never drawn: the card carries the glass
            // wearing that rim now — the author's own crust art on the author's own glass —
            // or, for a garnish that goes IN the drink, the dish it is taken from.
            float usesW = GarnishPicture(prop, card);
            LayOutCellarCard(usesW > 0f ? 2 : 0, usesW, false, showStock);
            _cellarCardFree = true;
            _cellarCardOver = over;
        }

        /// <summary>The picture under a garnish card's heading: a glass wearing the crust for
        /// a rim, the counter's own dish for anything else. Returns the width it took, or 0
        /// when there is nothing to draw.</summary>
        private float GarnishPicture(PrepProp prop, IngredientCard card)
        {
            if (prop == null || _cellarCardUsesRoot == null) return 0f;
            const float Box = 64f;
            var row = NewRect("Shows", _cellarCardUsesRoot);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(220f, Box + 4f);

            var picRt = NewRect("Pic", row);
            picRt.anchorMin = picRt.anchorMax = new Vector2(0f, 0.5f);
            picRt.pivot = new Vector2(0f, 0.5f);
            picRt.sizeDelta = new Vector2(Box, Box);
            picRt.anchoredPosition = Vector2.zero;

            string tell;
            if (prop.IsRim)
            {
                // The glass the crust is turned in — the tumbler for salt, the coupe for
                // sugar, which is how a bar actually rims them.
                string glassId = prop.Id == "sugar_rim" ? "coupe" : "rocks";
                GlasswareDefinition def = null;
                if (_bootstrap != null && _bootstrap.Glassware != null)
                    foreach (var g in _bootstrap.Glassware)
                        if (g.Id == glassId) { def = g; break; }
                var piece = def != null ? GlassArt.For(def, 2) : default;
                var img = picRt.gameObject.AddComponent<Image>();
                img.sprite = piece.Sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.enabled = piece.Sprite != null;
                var crust = prop.Id == "sugar_rim" ? piece.RimSugar : piece.RimSalt;
                if (crust != null && piece.RimPlacement(picRt.sizeDelta, crust,
                                                        out var cSize, out var cTop))
                {
                    var cRt = NewRect("Crust", picRt);
                    cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 1f);
                    cRt.pivot = new Vector2(0.5f, 1f);
                    cRt.sizeDelta = cSize;
                    cRt.anchoredPosition = cTop;
                    var cImg = cRt.gameObject.AddComponent<Image>();
                    cImg.sprite = crust;
                    cImg.preserveAspect = true;
                    cImg.raycastTarget = false;
                }
                tell = prop.Id == "sugar_rim"
                    ? "TURN THE GLASS IN THE DISH" : "TURN THE GLASS IN THE DISH";
            }
            else
            {
                var img = picRt.gameObject.AddComponent<Image>();
                img.sprite = GarnishCounterArt(prop.Id) ?? (card != null ? ItemArt.Bottle(card) : null);
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.enabled = img.sprite != null;
                tell = prop.Id == "ice" ? "DROP IT IN BEFORE THE POUR" : "ON THE RIM OR IN THE DRINK";
            }

            var word = NewText("Tell", row, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            word.rectTransform.anchorMin = new Vector2(0f, 0f);
            word.rectTransform.anchorMax = new Vector2(0f, 1f);
            word.rectTransform.pivot = new Vector2(0f, 0.5f);
            word.rectTransform.anchoredPosition = new Vector2(Box + 10f, 0f);
            word.rectTransform.sizeDelta = new Vector2(200f, 0f);
            word.horizontalOverflow = HorizontalWrapMode.Wrap;
            word.verticalOverflow = VerticalWrapMode.Overflow;
            word.raycastTarget = false;
            word.text = tell;
            float w = Box + 10f + Mathf.Min(200f, word.preferredWidth);
            word.rectTransform.sizeDelta = new Vector2(Mathf.Min(200f, word.preferredWidth), 0f);
            row.sizeDelta = new Vector2(w, Box + 4f);
            return w;
        }

        private void HideGarnishCard(RectTransform over)
        {
            LiftProp(over, false);
            if (_cellarCardOver == over) { _cellarCardOver = null; _cellarCardFree = false; }
        }

        /// <summary>Sorts a HUD prop over the card (27) or lets it inherit again: the prop
        /// carries a dormant Canvas for this (see the rail's dishes in Seats).</summary>
        private static void LiftProp(RectTransform prop, bool up)
        {
            if (prop == null) return;
            var cv = prop.GetComponent<Canvas>();
            if (cv == null) return;
            cv.overrideSorting = up;
            if (up) cv.sortingOrder = 27;
        }

        /// <summary>Switches the shelf's bottle back on when its card goes.</summary>
        private void LowerCellarBottle()
        {
            if (_cellarCardShown < 0) return;
            stage?.ShowCellarBottle(_cellarCardShown, true);
            _cellarCardShown = -1;
        }

        /// <summary>What is left in the bottle, as a share of the bar. REMEMBERED, not
        /// measured (2026-09-09, the author: "mahzendeki bilgi kutusunda açılan bar ekrandan
        /// taşıyor"): this is called while the card is being filled in, BEFORE the layout
        /// gives the bar its width, so a fill sized here in pixels was sized against the
        /// last card's bar — and a wide card followed by a narrow one ran the fill out
        /// past the box. The share is laid on in <see cref="ApplyCardStockFill"/>, on
        /// anchors, so the fill cannot be wider than the bar whatever happens.</summary>
        private void SetCardStockFill(float frac, Color tone)
        {
            _cellarCardStockFrac = Mathf.Clamp01(frac);
            var img = _cellarCardStockFill.GetComponent<Image>();
            if (img != null) img.color = tone;
        }

        private void ApplyCardStockFill()
        {
            var img = _cellarCardStockFill.GetComponent<Image>();
            if (img != null) img.enabled = _cellarCardStockFrac > 0.004f;
            _cellarCardStockFill.anchorMin = new Vector2(0f, 0f);
            _cellarCardStockFill.anchorMax = new Vector2(_cellarCardStockFrac, 1f);
            _cellarCardStockFill.offsetMin = new Vector2(1f, 1f);
            _cellarCardStockFill.offsetMax = new Vector2(-1f, -1f);
        }

        private void StepCellarCard()
        {
            if (_cellarCard == null) return;
            var over = _cellarCardOver;
            bool up = over != null && over.gameObject.activeInHierarchy && (CellarOpen || _cellarCardFree);
            float want = up ? 1f : 0f;
            _cellarCardGroup.alpha = Motion.Reduced ? want : Mathf.MoveTowards(
                _cellarCardGroup.alpha, want, Time.unscaledDeltaTime / PropTipFade);
            if (!up && _cellarCardGroup.alpha <= 0f) LowerCellarBottle();
            if (!up || _cellarCardGroup.alpha <= 0f) return;
            // BEHIND THE BOTTLE (2026-09-08, the author: "mevcut sahnedeki gin görselinin
            // arkasında bir bilgi paneli oluşacak"). The card does not stand over the shelf
            // with a second bottle on it: its SLOT is laid on the bottle where it stands —
            // the copy in the slot at the shelf bottle's own size and place, so nothing
            // seems to move — and the box runs out beside it. Past the screen's right
            // third the box would run off, so the card turns round (the mirrored cut of
            // the same art) and the box runs left.
            var parent = (RectTransform)_cellarCard.parent;
            var corners = new Vector3[4];
            over.GetWorldCorners(corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                RectTransformUtility.WorldToScreenPoint(null, corners[0]), null, out Vector2 lo);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                RectTransformUtility.WorldToScreenPoint(null, corners[2]), null, out Vector2 hi);
            var centre = (lo + hi) * 0.5f;
            var size = new Vector2(Mathf.Abs(hi.x - lo.x), Mathf.Abs(hi.y - lo.y));

            // THE BOTTLE IN THE SLOT IS THE SHELF'S BOTTLE, THIS FRAME — its pose, not its
            // bounding box: the plate's centre, its size WITHOUT the rock, and the angle it
            // is rocked by. The card is hung on that centre, so the bottle sits in the middle
            // of the well however the hit plate over it is cut.
            Vector2 bCentre = Vector2.zero, bSize = Vector2.zero;
            float bAngle = 0f;
            bool haveBottle = !_cellarCardFree && _cellarCardShown >= 0
                && CardBottleRect(_cellarCardShown, parent, out bCentre, out bSize, out bAngle);
            var anchor = haveBottle ? bCentre : centre;
            bool left = anchor.x > parent.rect.width * (0.5f - 0.34f);
            SetCardHand(left);
            if (_cellarCardFree)
                _cellarCardDish.rectTransform.sizeDelta = new Vector2(
                    Mathf.Clamp(size.x, 32f, _cellarCardSlotSize.x - CardSlotFrameX * 2f),
                    Mathf.Clamp(size.y, 32f, _cellarCardSlotSize.y - CardSlotFrameY * 2f));
            float w = _cellarCard.sizeDelta.x, h = _cellarCard.sizeDelta.y;
            float slotCx = left ? w - _cellarCardSlotSize.x * 0.5f : _cellarCardSlotSize.x * 0.5f;
            _cellarCard.pivot = new Vector2(0f, 0f);
            // THE BOTTLE'S BOX NEVER LEAVES THE BOTTLE (2026-09-09, measured). The card used
            // to be clamped to the screen as one plate, so a tall card on the lower shelf was
            // pushed up and took the slot with it while the copy stayed on the bottle —
            // seventy-five units of daylight between a bottle and the box drawn round it.
            // They are two boxes: the card hangs on the bottle, and the TEXT box slides
            // inside it to stay on screen.
            var pos = anchor - new Vector2(slotCx, h * 0.5f);
            _cellarCard.anchoredPosition = pos;
            float lim = parent.rect.width * 0.5f - 8f, vlim = parent.rect.height * 0.5f - 8f;
            var bodySize = _cellarCardBodySize;
            var bodyMid = pos + new Vector2(left ? bodySize.x * 0.5f : w - bodySize.x * 0.5f, h * 0.5f);
            Vector2 slide = Vector2.zero;
            if (bodyMid.y + bodySize.y * 0.5f > vlim) slide.y = vlim - (bodyMid.y + bodySize.y * 0.5f);
            else if (bodyMid.y - bodySize.y * 0.5f < -vlim) slide.y = -vlim - (bodyMid.y - bodySize.y * 0.5f);
            if (bodyMid.x + bodySize.x * 0.5f > lim) slide.x = lim - (bodyMid.x + bodySize.x * 0.5f);
            else if (bodyMid.x - bodySize.x * 0.5f < -lim) slide.x = -lim - (bodyMid.x - bodySize.x * 0.5f);
            _cellarCardBody.anchoredPosition = slide;

            if (haveBottle)
            {
                // the vessel is anchored at the slot's centre; the offset is from there —
                // zero until the card is pushed off the bottle by the screen's edge
                var slotCentre = pos + new Vector2(slotCx, h * 0.5f);
                var rock = Quaternion.Euler(0f, 0f, bAngle);
                _cellarCardVessel.sizeDelta = bSize;
                _cellarCardVessel.anchoredPosition = bCentre - slotCentre;
                _cellarCardVessel.localRotation = rock;
                // THE DRINK IS RE-LAID EVERY FRAME (2026-09-09, the author: "içerisindeki sıvı
                // sabit kaldığından bir bütün gibi durmuyorlar"). BottleArt measures the level
                // against the rect the stencil HAS when it is told, in canvas units, so a
                // level set once when the card opened stayed the size the bottle was then —
                // and the glass rose, grew and rocked away from it. The tilt is zero because
                // the whole vessel is rocked by the line above, drink and glass together.
                if (_cellarCardFrac > 0.0)
                    _cellarCardBottle.SetLevel(_cellarCardTone, _cellarCardFrac, 0f);
                var glow = stage != null ? stage.CellarGlow(_cellarCardShown) : null;
                bool lit = glow != null && glow.HaloArtNow != null && glow.GlowNow > 0.01f;
                _cellarCardHalo.enabled = lit;
                if (lit)
                {
                    _cellarCardHalo.sprite = glow.HaloArtNow;
                    var tint = glow.HaloTint;
                    _cellarCardHalo.color = new Color(tint.r, tint.g, tint.b, tint.a * glow.GlowNow);
                    _cellarCardHalo.rectTransform.sizeDelta = glow.HaloBoxFor(bSize);
                    _cellarCardHalo.rectTransform.anchoredPosition = bCentre - slotCentre;
                    _cellarCardHalo.rectTransform.localRotation = rock;
                }
                if (_cellarCardDish.enabled)   // the flat copy, same rect
                {
                    _cellarCardDish.rectTransform.sizeDelta = bSize;
                    _cellarCardDish.rectTransform.anchoredPosition = bCentre - slotCentre;
                    _cellarCardDish.rectTransform.localRotation = rock;
                }
            }
        }

        /// <summary>One cellar bottle's pose in the card's own units: where its front plate
        /// stands, how big it is with the rock taken out, and the angle of that rock. The
        /// stage speaks world units and they go through the CAMERA —
        /// RectTransformUtility.WorldToScreenPoint(null, ...) is for canvas objects and hands
        /// a world point back untouched, which laid the copy a shelf too high (2026-09-08).</summary>
        private bool CardBottleRect(int index, RectTransform parent,
                                    out Vector2 centre, out Vector2 size, out float angleDeg)
        {
            centre = Vector2.zero; size = Vector2.zero; angleDeg = 0f;
            if (stage == null || !stage.CellarBottlePose(index, out var wc, out var ws, out angleDeg))
                return false;
            var cam = Camera.main;
            var wr = wc + new Vector3(ws.x, ws.y, 0f) * 0.5f;
            Vector2 s0 = cam != null ? (Vector2)cam.WorldToScreenPoint(wc) : (Vector2)wc;
            Vector2 s1 = cam != null ? (Vector2)cam.WorldToScreenPoint(wr) : (Vector2)wr;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, s0, null, out centre);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, s1, null, out Vector2 corner);
            size = new Vector2(Mathf.Abs(corner.x - centre.x) * 2f, Mathf.Abs(corner.y - centre.y) * 2f);
            return size.x > 1f && size.y > 1f;
        }

        private bool _cellarCardLeft;

        /// <summary>Turns the card round: the slot on the right and the box running left,
        /// on the mirrored cut of the art — or back. The box's rows are anchored to the
        /// box's own top-left either way, so nothing inside it moves.</summary>
        private void SetCardHand(bool left)
        {
            if (_cellarCardLeft == left && _cellarCardSlot.GetComponent<Image>().sprite != null) return;
            _cellarCardLeft = left;
            var slotImg = _cellarCardSlot.GetComponent<Image>();
            var bodyImg = _cellarCardBody.GetComponent<Image>();
            var slotSprite = left ? ChromeArt.CardSlotL() : ChromeArt.CardSlot();
            var bodySprite = left ? ChromeArt.CardBodyL() : ChromeArt.CardBody();
            if (slotSprite != null) slotImg.sprite = slotSprite;
            if (bodySprite != null) bodyImg.sprite = bodySprite;
            ApplyCardHand();
        }

        /// <summary>Puts the two boxes side by side at the sizes they were given: the bottle's
        /// box against one edge, the text's box against the other, both centred on the card's
        /// middle line. Neither is stretched to the other's height (2026-09-09).</summary>
        private void ApplyCardHand()
        {
            bool left = _cellarCardLeft;
            _cellarCardSlot.anchorMin = _cellarCardSlot.anchorMax = new Vector2(left ? 1f : 0f, 0.5f);
            _cellarCardSlot.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            _cellarCardSlot.anchoredPosition = Vector2.zero;
            _cellarCardSlot.sizeDelta = _cellarCardSlotSize;
            _cellarCardBody.anchorMin = _cellarCardBody.anchorMax = new Vector2(left ? 0f : 1f, 0.5f);
            _cellarCardBody.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            _cellarCardBody.anchoredPosition = Vector2.zero;
            _cellarCardBody.sizeDelta = _cellarCardBodySize;
        }

        /// <summary>The pointer arrived on a prop: say what pressing it does.</summary>
        /// <param name="picture">Draws the mark as a PICTURE rather than a bullet
        /// (2026-09-09, the author: "kimlikte hangi garnish istenildiği iconun üstüne
        /// gelindiğinde görseliyle gözükmeli"). A 16-unit pictogram beside a word says which
        /// of four things it is; the thing itself, at 48, says what it looks like on a
        /// counter — which is what a player about to go and find it needs.</param>
        internal void ShowPropTip(RectTransform over, string word, Sprite icon = null, string detail = null,
            System.Func<string> detailFn = null, bool picture = false)
        {
            if (_propTip == null || over == null || string.IsNullOrEmpty(word)) return;
            _propTipOver = over;
            _propTipDetailFn = detailFn;
            _propTipText.text = word;
            bool rich = icon != null || !string.IsNullOrEmpty(detail);
            bool big = picture && icon != null;
            float box = big ? 48f : 16f;
            // Two rows and as wide as its longer line when it carries a mark or a detail;
            // the one-row word it always was otherwise. A picture takes its own column.
            float chars = Mathf.Max(word.Length, (detail ?? "").Length);
            _propTip.sizeDelta = big
                ? new Vector2(Mathf.Max(220f, box + 24f + chars * 7.2f), 64f)
                : rich ? new Vector2(Mathf.Max(200f, 30f + chars * 7.2f + 12f), 40f)
                : new Vector2(180f, 22f);
            var ir = _propTipIcon.rectTransform;
            ir.sizeDelta = new Vector2(box, box);
            ir.anchoredPosition = new Vector2(big ? 12f : 8f, 0f);
            _propTipIcon.sprite = icon;
            _propTipIcon.enabled = icon != null;
            _propTipDetail.text = detail ?? "";
            _propTipDetail.enabled = rich;
            _propTipText.alignment = rich ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter;
            var tr = _propTipText.rectTransform;
            float inset = big ? box + 20f : icon != null ? 30f : 10f;
            tr.offsetMin = rich ? new Vector2(inset, big ? 26f : 20f) : Vector2.zero;
            tr.offsetMax = rich ? new Vector2(-8f, big ? -10f : -7f) : Vector2.zero;
            var dr = _propTipDetail.rectTransform;
            dr.offsetMin = new Vector2(inset, 5f);
            dr.offsetMax = new Vector2(-8f, big ? -30f : -20f);
        }

        /// <summary>...and left it. Only the prop that RAISED the tip may lower it: two props
        /// whose rects touch would otherwise trade it, and the second one's Exit would take
        /// down the first one's Enter.</summary>
        internal void HidePropTip(RectTransform over)
        {
            if (_propTipOver == over) _propTipOver = null;
        }

        private void StepPropTip()
        {
            if (_propTip == null) return;
            var over = _propTipOver;
            bool up = over != null && over.gameObject.activeInHierarchy;
            float want = up ? 1f : 0f;
            _propTipGroup.alpha = Motion.Reduced ? want : Mathf.MoveTowards(
                _propTipGroup.alpha, want, Time.unscaledDeltaTime / PropTipFade);
            if (!up || _propTipGroup.alpha <= 0f) return;
            if (_propTipDetailFn != null && _propTipDetail != null)
            {
                string now = _propTipDetailFn();
                if (now != _propTipDetail.text) _propTipDetail.text = now;
            }

            // THROUGH THE SCREEN, because the prop may not be on this canvas. The sink and
            // the beer font are hit plates on the stage's own overlay; a straight read of
            // their anchoredPosition would place the caption in the HUD's coordinates as if
            // it were the stage's, which is only the same thing by accident.
            var corners = new Vector3[4];
            over.GetWorldCorners(corners);
            var top = (corners[1] + corners[2]) * 0.5f;
            var screen = RectTransformUtility.WorldToScreenPoint(null, top);
            // UNDER a prop that lives at the top of the screen (2026-09-06: the beam's
            // readings), where a caption raised above it would be off the picture.
            bool hang = screen.y > Screen.height * 0.88f;
            if (hang)
            {
                var bottom = (corners[0] + corners[3]) * 0.5f;
                screen = RectTransformUtility.WorldToScreenPoint(null, bottom);
            }
            _propTip.pivot = new Vector2(0.5f, hang ? 1f : 0f);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_propTip.parent, screen, null, out Vector2 local))
                _propTip.anchoredPosition = local + new Vector2(0f, hang ? -8f : 8f);
        }

        /// <summary>One figure, falling out from under the money it changed.</summary>
        private void DropMoney(int amount, int slot)
        {
            if (_tillFloats == null || amount == 0 || Motion.Reduced) return;
            var rt = NewRect("Drop", _tillFloats);
            Place(rt, new Vector2(1, 0), new Vector2(126, 16), new Vector2(-10, -14f));
            rt.pivot = new Vector2(1, 1);
            var t = NewText("L", rt, _display, 8, TextAnchor.MiddleRight,
                amount >= 0 ? ShopViceLit : ShopCost);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = (amount >= 0 ? "+$" : "-$") + Mathf.Abs(amount);
            _moneyDrops.Add((rt, t, Time.unscaledTime + slot * DropStagger));
            _coinDue.Add(Time.unscaledTime + slot * DropStagger);
        }

        /// <summary>
        /// A COIN PER RECEIPT LINE (2026-08-27). Checkout played one `cash` for a basket
        /// of six while the receipt already staggered a -$N line per item beneath the
        /// till — the picture counted and the sound did not. Each line's coin waits for
        /// that line's own delay, so a six-item order sounds like six things being paid
        /// for. Held in a plain due-list rather than a coroutine per line: they are
        /// timestamps on the unscaled clock, and the drops beside them work the same way.
        /// </summary>
        private readonly List<float> _coinDue = new List<float>();

        private void StepCoinDue()
        {
            for (int i = _coinDue.Count - 1; i >= 0; i--)
                if (Time.unscaledTime >= _coinDue[i])
                {
                    _coinDue.RemoveAt(i);
                    Sfx.Play("coin", 0.55f);
                }
        }

        private void StepMoneyDrops()
        {
            StepCoinDue();
            for (int i = _moneyDrops.Count - 1; i >= 0; i--)
            {
                var (rt, label, born) = _moneyDrops[i];
                if (rt == null) { _moneyDrops.RemoveAt(i); continue; }
                float t = Time.unscaledTime - born;
                if (t < 0f) { label.color = Clear(label.color); continue; }
                float k = t / DropLife;
                if (k >= 1f) { Destroy(rt.gameObject); _moneyDrops.RemoveAt(i); continue; }
                // Out fast, then drifting down and away — a figure that is read and gone.
                rt.anchoredPosition = new Vector2(-10f, -14f - DropRise * (1f - (1f - k) * (1f - k)));
                label.color = Opaque(label.color);
                var c = label.color;
                label.color = new Color(c.r, c.g, c.b,
                    k < 0.15f ? k / 0.15f : 1f - Mathf.Clamp01((k - 0.55f) / 0.45f));
            }
        }

        private void RunTheTill(TycoonRun run)
        {
            float want = run.Money;
            if (float.IsNaN(_tillShown) || Motion.Reduced) { _tillShown = want; }
            else if (!Mathf.Approximately(_tillShown, want))
            {
                // Proportional, with a floor: a $2 refill still moves, a $200 fitting does
                // not take ten times as long as a $20 one.
                float speed = Mathf.Max(28f, Mathf.Abs(want - _tillShown) * 4.5f);
                _tillShown = Mathf.MoveTowards(_tillShown, want, speed * Time.unscaledDeltaTime);
            }
            // (The change used to float off the register's drawer as it moved; the register
            //  went out of the room on 2026-08-26 and took the float with it. What a
            //  CUSTOMER pays still rises off their own stool — see TabFloat — which is the
            //  only money the shift is asked to watch.)

            int shown = Mathf.RoundToInt(_tillShown);
            // The till wears the coin rather than a typed $ (2026-09-07). It is also the one
            // figure in the game that COUNTS, so the coin is re-placed as the digits change
            // width — $99 to $100 moves the mark a whole glyph.
            CoinFigure(_tabletTill, shown, shown < 0 ? "-" : "");
            if (_beamTillCard != null)
            {
                // AND IT GOES BEHIND A SHEET (2026-09-09): the books and the market draw
                // their own till, and a second one hanging over the tablet is both a repeat
                // and something in the way.
                bool room = _dayEndPanel == null || !_dayEndPanel.gameObject.activeSelf;
                if (_beamTillCard.gameObject.activeSelf != room)
                    _beamTillCard.gameObject.SetActive(room);
            }
            if (_beamTillText != null)
            {
                string money = (shown < 0 ? "-" : "") + Mathf.Abs(shown);
                if (_beamTillText.text != money) _beamTillText.text = money;
                _beamTillText.color = shown < 0 ? UITheme.ViceRed[3] : UITheme.Amber[4];
            }
        }

        /// <summary>Stands the house coin in front of a figure and writes the digits
        /// (2026-09-07, the author: "oyunda para gosteren her yere $ yerine o iconu koy").
        ///
        /// The coin is a CHILD of the figure's own Text and is placed by MEASURING the digits
        /// rather than by reserving a column — the same thing the slip's own marks have done
        /// since 2026-09-04, and for the same reason: a fixed slot is either too wide for a
        /// two-digit night or too narrow for a four-digit one. The sign stays in the type,
        /// because it belongs to the arithmetic and not to the coin.
        ///
        /// The figure must be RIGHT-aligned; every money readout in the chrome already is.</summary>
        private void CoinFigure(Text figure, int amount, string sign = "")
        {
            if (figure == null) return;
            figure.text = sign + Mathf.Abs(amount);
            var rt = figure.rectTransform;
            // A TYPED $, NOT THE DRAWN COIN (2026-09-09, the author: "dolar iconu yerine
            // fiyatlarda sarı $ ve sarı para miktarı kullanılsın"). The coin was a 16px
            // drawing that had to be sized against three different scales to stay a disc
            // rather than a smudge (the four notes this replaces); a $ set in the figure's
            // own face and ink is the same word in the same voice, at any size, and reads
            // as part of the number rather than as a badge beside it. One per figure, kept
            // and re-placed rather than rebuilt — these are stepped readouts.
            var sign_ = rt.Find("Sign") as RectTransform;
            Text signText;
            if (sign_ == null)
            {
                var t = NewText("Sign", rt, figure.font, figure.fontSize, TextAnchor.MiddleRight, figure.color);
                sign_ = t.rectTransform;
                sign_.pivot = new Vector2(1f, 0.5f);
                sign_.anchorMin = sign_.anchorMax = new Vector2(1f, 0.5f);
                sign_.sizeDelta = new Vector2(24f, figure.fontSize + 8f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.text = "$";
                signText = t;
            }
            else signText = sign_.GetComponent<Text>();
            if (!sign_.gameObject.activeSelf) sign_.gameObject.SetActive(true);
            if (signText != null)
            {
                signText.font = figure.font;
                signText.fontSize = figure.fontSize;
                signText.color = figure.color;
            }
            // Clear of the digits by a couple of units, measured off the type rather than
            // guessed: preferredWidth is the ink, and the gap is added to it.
            sign_.anchoredPosition = new Vector2(-(figure.preferredWidth + CoinGap), 0f);
        }

        /// <summary>Empties a money figure AND takes its coin down with it. A figure blanked
        /// by writing "" keeps the coin this helper hung on it, which is a mark floating over
        /// nothing (the shop's basket, cleared).</summary>
        private void ClearCoin(Text figure)
        {
            if (figure == null) return;
            figure.text = "";
            var sign = figure.rectTransform.Find("Sign");
            if (sign != null) sign.gameObject.SetActive(false);
        }

        /// <summary>The coin's drawn size in the chrome, and its gap off the digits. 24
        /// (2026-09-08, the author: "daha 1.5 kat daha büyük bir tasarım olabilir") — half
        /// again the 16 it shipped at this morning, and a size the coin is DRAWN at rather
        /// than scaled to (Tools/coin_icon.py draws 16, 24 and 32). The gap is in the figure's
        /// own layout units and is scaled with it.</summary>
        private const float CoinGap = 6f;   // the $ sits this far off the digits (2026-09-09)

        private void WatchFixtures()
        {
            var run = Run;
            if (run == null) return;
            var stage = _stage != null ? _stage : FindFirstObjectByType<DiegeticStage>();
            _stage = stage;
            if (stage == null) return;
            if (run.OwnedFixtureCount == _lastFixtureCount) return;
            bool firstSync = _lastFixtureCount < 0;
            int gained = run.OwnedFixtureCount - _lastFixtureCount;
            _lastFixtureCount = run.OwnedFixtureCount;
            if (!firstSync && gained > 0) Sfx.Play("level_up", 0.9f);
            // ONE RUNG STANDING, NOT THE WHOLE LADDER (2026-08-19; generic since the wall
            // lamps, 2026-08-24). A bar that upgraded still OWNS the lower rungs — fitted
            // over, not sold back — and every rung stands in the same slot, so handing the
            // room all of them draws the ladder one inside the other. Only the slot's
            // tallest owned rung goes in; everything unranked goes in as it always did.
            var owned = new List<FixtureDefinition>();
            foreach (var f in run.FixtureCatalogue)
            {
                if (!run.OwnsFixture(f.Id)) continue;
                if (f.Level > 0 && f.Level < run.LadderLevel(f.Slot)) continue;
                owned.Add(f);
            }
            // The room is handed its hooks before anything is stood in them. Cheap enough
            // to repeat: seven entries into a dictionary, only on the frames the dressing
            // actually changed.
            stage.SetSlots(_bootstrap != null ? _bootstrap.StageSlots : null);
            stage.SyncFixtures(owned);
        }

        /// <summary>
        /// The cellar's OWN change signal (2026-08-25, the author: "açılan yeni alkol
        /// rafımıza satın alınan alkoller ve meşrubatlar eklenmiyor").
        ///
        /// They were not. Restocking the shelves hung off WatchFixtures, which returns early
        /// unless the FIXTURE count moved — so a night spent buying bottles and nothing else
        /// changed nothing in the room, and a bar could buy the whole catalogue without one
        /// more bottle appearing behind it. Buying a lamp put them all up at once, which is
        /// exactly the shape of a bug nobody can describe.
        ///
        /// The shelf's own count is the cheapest honest signal — every bottle JOINS the shelf
        /// (TycoonRun.BuyBrand), so a purchase always moves it — and the ids are folded in
        /// after it, because an upgrade that swaps a brand in place (Shelf.Replace) changes
        /// the picture without changing the count.
        /// </summary>
        private void WatchCellar()
        {
            var run = Run;
            if (run == null) return;
            var stage = _stage != null ? _stage : FindFirstObjectByType<DiegeticStage>();
            _stage = stage;
            if (stage == null) return;
            int mark = 17;
            foreach (var b in run.Shelf.Bottles)
                mark = mark * 31 + (b.Id != null ? b.Id.GetHashCode() : 0);
            if (mark == _lastShelfMark) return;
            _lastShelfMark = mark;
            RefreshCellar(run);
        }

        /// <summary>
        /// Stands the bar's own stock in the counter's cellar (2026-08-22). The SAME rule the
        /// back-bar wall keeps: garnish is not stock you pour from and beer comes off the
        /// font on the counter, so neither stands here. The stage is TOLD the pictures and
        /// never reads the run, which is why this lives on the HUD side of the line.
        /// </summary>
        private void RefreshCellar(TycoonRun run)
        {
            if (stage == null) return;
            var art = new List<Sprite>(DiegeticStage.CellarSlots);
            _cellarCards.Clear();
            // GROUPED (2026-09-07, the author: "mahzen sahnesinde en azından içeceklerin hangi
            // grupta olduğu anlaşılsın, mouse ile üzerine gelmeden"): the stock stands by
            // family — gins together, then vodkas, whiskies… the mixers last — and the
            // family's name is written on the shelf under each run (StepCellarLabels).
            var stock = new List<IngredientCard>();
            if (run != null)
                foreach (var b in run.Shelf.Bottles)
                {
                    var card = b.Ingredient;
                    if (card == null) continue;
                    if (card.Type == IngredientType.Garnish || card.Type == IngredientType.Beer)
                        continue;
                    stock.Add(card);
                }
            stock.Sort((a, b) =>
            {
                int ga = CellarGroupOrder(a), gb = CellarGroupOrder(b);
                if (ga != gb) return ga.CompareTo(gb);
                return string.CompareOrdinal(a.Name, b.Name);
            });
            foreach (var card in stock)
            {
                var sprite = ItemArt.Bottle(card);
                if (sprite == null) continue;
                art.Add(sprite);
                _cellarCards.Add(card);          // the SAME order the plates are indexed by
                if (art.Count >= DiegeticStage.CellarSlots) break;
            }
            var ids = new List<string>(_cellarCards.Count);
            foreach (var c in _cellarCards) ids.Add(c.Id);
            stage.SetCellar(art, ids);
            // The v4 sandwich: plates, drink tones and levels in the same order (PLAN §4c).
            var plates = new List<ItemArt.BottlePlates>(_cellarCards.Count);
            var tones = new List<Color>(_cellarCards.Count);
            foreach (var c in _cellarCards)
            {
                plates.Add(ItemArt.Plates(c, cellar: true));
                tones.Add(UITheme.LiquidColor(c.Info?.Style, c.Type));
            }
            stage.SetCellarTones(tones);
            stage.SetCellarPlates(plates, CellarFills(run));
            _lastCellarFills.Clear();
        }

        /// <summary>What is left in each cellar bottle, in the cellar's own order.</summary>
        /// <summary>
        /// THE BOTTLE'S CARD (2026-09-06, the author: "backbarda alkollerin isimleri gözükmüyor,
        /// gözükürse de isimleri üst üste binebilir ... alkollerin üstüne gelindiğinde ... bir
        /// kart içerisinde olmalı, kartta alkolün adı, hangi kokteyllerde kullanılabileceği
        /// (oyuncunun sahip olduğu tarifler içerisinde), çalkalanmaması gereken bir içecekse
        /// çalkalamama iconu"). Thirteen names under thirteen bottles print over each other,
        /// so the name comes up WITH the bottle: the hover caption on its card, over the
        /// bottle, with the house drinks that call for it and a NO-SHAKE mark on anything
        /// that fizzes (GDD 21 §12: a carbonated pour is never put through the tin).
        /// </summary>
        private void OnCellarHover(int index, RectTransform plate)
        {
            if (index < 0 || index >= _cellarCards.Count || _cellarCard == null)
            {
                if (_cellarCardOver == plate) { _cellarCardOver = null; LowerCellarBottle(); }
                return;
            }
            if (_cellarCardShown != index)
            {
                LowerCellarBottle();
                stage?.ShowCellarBottle(index, false);
                _cellarCardShown = index;
            }
            var card = _cellarCards[index];
            var run = Run;
            var bottle = run?.Shelf.Find(card.Id);
            string style = (card.Info?.Style ?? card.Type.ToString()).Replace('_', ' ').ToUpperInvariant();
            int tier = bottle != null ? bottle.Tier : (card.Info?.Tier ?? 1);
            int price = Market.StockPrice(card);
            ClearCardUses();

            // The bottle itself in the slot — the UI sandwich, unlit and bright — filled to
            // what is left of it; StepCellarCard lays it on the shelf bottle's own rect.
            _cellarCardDish.enabled = false;
            var plates = ItemArt.Plates(card, cellar: true);
            _cellarCardBottle.Show(plates);
            _cellarCardTone = UITheme.LiquidColor(card.Info?.Style, card.Type);
            _cellarCardFrac = plates != null
                ? (bottle != null && bottle.Capacity > 0 ? bottle.Remaining / bottle.Capacity : 1.0) : 0.0;
            if (_cellarCardFrac > 0.0) _cellarCardBottle.SetLevel(_cellarCardTone, _cellarCardFrac, 0f);
            // THE BOX IS THE BOTTLE'S SIZE (2026-09-09): measured off the shelf where it
            // stands, before the hover's rise and growth have started.
            if (CardBottleRect(index, (RectTransform)_cellarCard.parent, out _, out var art, out _))
                SizeCardSlot(art);
            _cellarCardHalo.enabled = false;
            if (plates == null)
            {
                // A card without the v4 plates (lemon_fresh, the cartons) is drawn on the
                // shelf from its flat cellar sprite — the copy is that same sprite, on the
                // same rect. Without this the shelf's one went dark and nothing stood in
                // for it (the author, 2026-09-08: "üstüne gelindiğinde şişeler yok oluyor").
                _cellarCardDish.sprite = ItemArt.Bottle(card);
                _cellarCardDish.enabled = _cellarCardDish.sprite != null;
            }

            _cellarCardName.text = card.Name.ToUpperInvariant();
            _cellarCardMeta.text = $"{style}  ·  TIER {tier}";
            // THE RUNG'S STARS IN THE BOX (the author: "alkolün yıldız seviyesi de kutunun
            // içerisinde yer almalı"): the same star gate the market shows for it.
            double rung = RungOf(card);
            if (!double.IsNaN(rung))
                _cellarCardStars = StarRow(_cellarCardBody, new Vector2(0f, 1f), Vector2.zero, 16f,
                    rung, UITheme.Amber[3], new Color(1f, 1f, 1f, 0.22f));
            if (_cellarCardStars != null) _cellarCardStars.pivot = new Vector2(0f, 1f);
            _cellarCardPrice.text = price + " A BOTTLE";
            bool showStock = bottle != null;
            if (showStock)
            {
                float frac = bottle.Capacity > 0 ? (float)(bottle.Remaining / bottle.Capacity) : 0f;
                bool low = bottle.Remaining <= bottle.Capacity * 0.15;
                _cellarCardStock.text = (low ? "ALMOST OUT  ·  " : "") + $"{bottle.Remaining:0.0} OF {bottle.Capacity:0} LEFT";
                _cellarCardStock.color = low ? UITheme.ViceRed[3] : UITheme.Cream[3];
                SetCardStockFill(frac, low ? UITheme.ViceRed[3] : UITheme.LiquidColor(card.Info?.Style, card.Type));
            }

            // THE DRINKS IT GOES INTO, AS THE MENU DRAWS THEM (the author: "mevcut
            // alkollerimizden hangilerinin tariflerine dahillerse o kokteyllerin menüde
            // kullanılan iconları, altında ya da yanında hangi kokteyl olduğu"): one line a
            // drink, the icon and the name, six at most and a count for the rest.
            int rows = 0, more = 0;
            float usesW = 0f;
            if (run != null)
                foreach (var r in run.MenuDrinksUsingStyle(card.Info?.Style))
                {
                    if (rows >= 6) { more++; continue; }
                    usesW = Mathf.Max(usesW, CardUseLine(r, rows * (CardDrinkIcon + 2f)));
                    rows++;
                }
            _cellarCardUsesHead.text = rows == 0 ? "IN NO HOUSE DRINK YET"
                : more > 0 ? $"IN THE BOOK  ·  AND {more} MORE" : "IN THE BOOK";

            bool fizzy = card.Type == IngredientType.Bubbly;
            LayOutCellarCard(rows, usesW, fizzy, showStock);
            _cellarCardFree = false;
            _cellarCardOver = plate;
        }

        // ── the shelf captions (2026-09-07) ───────────────────────────────────────
        private static readonly string[] CellarGroupRank =
            { "gin", "vodka", "rum", "whiskey", "tequila", "liqueur", "bitters", "syrup", "juice", "soda", "mixer" };

        /// <summary>The family a bottle stands with: the category for the spirits, the
        /// type's plain word for the rest.</summary>
        private static string CellarGroup(IngredientCard card)
        {
            string cat = card?.Info?.Category;
            if (!string.IsNullOrEmpty(cat) && cat != IngredientCategories.Mixer && cat != IngredientCategories.Juice)
                return cat;
            switch (card?.Type ?? IngredientType.Spirit)
            {
                case IngredientType.Bubbly: return "soda";
                case IngredientType.Sour: return cat == IngredientCategories.Juice ? "juice" : "sour";
                case IngredientType.Sweet: return cat == IngredientCategories.Juice ? "juice" : "syrup";
                case IngredientType.Bitter: return "bitters";
                default: return cat ?? "mixer";
            }
        }

        private static int CellarGroupOrder(IngredientCard card)
        {
            string g = CellarGroup(card);
            int i = System.Array.IndexOf(CellarGroupRank, g);
            return i < 0 ? CellarGroupRank.Length : i;
        }

        private static string CellarGroupWord(string group)
        {
            switch (group)
            {
                case "whiskey": return "WHISKY";
                case "soda": return "SODA & TONIC";
                case "syrup": return "SYRUPS";
                case "juice": return "JUICES";
                case "liqueur": return "LIQUEURS";
                case "mixer": return "MIXERS";
                default: return group.ToUpperInvariant();
            }
        }

        private readonly List<Text> _cellarLabels = new List<Text>();

        /// <summary>The family names under each run of bottles, in the HUD over the room and
        /// riding the drawer with it. Rebuilt whenever the cellar is; placed every frame.</summary>
        private void StepCellarLabels()
        {
            if (stage == null || _hudRoot == null) return;
            float phase = stage.DrawerPhase;
            int need = 0;
            // The runs: one caption per family per shelf row.
            var runs = new List<(string group, int first, int last)>();
            for (int i = 0; i < _cellarCards.Count && i < stage.CellarSlotCount; i++)
            {
                string g = CellarGroup(_cellarCards[i]);
                stage.CellarSlotStage(i, out var here);
                if (runs.Count > 0 && runs[runs.Count - 1].group == g)
                {
                    stage.CellarSlotStage(runs[runs.Count - 1].last, out var prev);
                    if (Mathf.Abs(prev.y - here.y) < 1f)
                    {
                        runs[runs.Count - 1] = (g, runs[runs.Count - 1].first, i);
                        continue;
                    }
                }
                runs.Add((g, i, i));
            }
            while (_cellarLabels.Count < runs.Count)
            {
                // A PLATE, NOT LOOSE TYPE (2026-09-07, the author: "mahzende alkol isimleri
                // hic gorunur olmuyor arka plandan dolayi"). Cream[3] on the plum boards
                // under pink neon measures under 2:1 against its own ground, which is why
                // the names were invisible; type wants 4.5. The bar's own answer to this is
                // the NAME PLATE its back-bar rails wear (16 §6, and the market borrowed it
                // for the same reason on 2026-08-19): a dark field with a lit lower lip, the
                // word in cream on top. It carries its contrast with it, so it reads over
                // whatever the room's lights are doing behind it.
                var plate = NewRect("CellarTag" + _cellarLabels.Count, _hudRoot);
                plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.5f);
                plate.pivot = new Vector2(0.5f, 1f);
                plate.sizeDelta = new Vector2(88f, 18f);
                var bg = plate.gameObject.AddComponent<Image>();
                bg.sprite = ChromeArt.Card();
                bg.type = Image.Type.Sliced;
                bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.92f);
                bg.raycastTarget = false;
                // The shelf-edge lip: one bright rule along the bottom, which is what makes a
                // plate read as screwed on rather than as a rectangle drawn over the wood.
                var lip = NewRect("Lip", plate);
                lip.anchorMin = new Vector2(0, 0); lip.anchorMax = new Vector2(1, 0);
                lip.pivot = new Vector2(0.5f, 0f);
                lip.sizeDelta = new Vector2(0, 1f);
                lip.anchoredPosition = Vector2.zero;
                var lipImg = lip.gameObject.AddComponent<Image>();
                lipImg.color = new Color(UITheme.Magenta[3].r, UITheme.Magenta[3].g,
                                         UITheme.Magenta[3].b, 0.75f);
                lipImg.raycastTarget = false;

                var t = NewText("L", plate, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[4]);
                Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = false;
                // The group is built HERE, once, rather than hunted for every frame while the
                // cellar is open.
                var pg = plate.gameObject.AddComponent<CanvasGroup>();
                pg.blocksRaycasts = false;
                pg.interactable = false;
                plate.transform.SetAsFirstSibling();
                _cellarLabels.Add(t);
            }
            // NEIGHBOURS ON ONE SHELF DO NOT SHARE A SPOT (2026-09-08, the author:
            // "mahzende isim ayırması net değil, tüm içecekler mevcut olduğunda problem
            // oluyor"). A plate is as wide as its word and centred on its run; with every
            // shelf full a run can be one slot wide, and "SODA & TONIC" is wider than a
            // slot, so two plates on one shelf overlapped and read as one smear. The
            // plates are laid left to right, and one that would overlap the plate before
            // it on the same shelf drops a plate's height — the balloons' own staircase,
            // at plate scale — so every family keeps its own name.
            float prevRight = float.MinValue, prevY = float.NaN, prevDrop = 0f;
            for (int i = 0; i < _cellarLabels.Count; i++)
            {
                var t = _cellarLabels[i];
                var plate = t.rectTransform.parent as RectTransform;
                bool on = i < runs.Count && phase > 0.02f;
                if (plate.gameObject.activeSelf != on) plate.gameObject.SetActive(on);
                if (!on) continue;
                var r = runs[i];
                stage.CellarSlotStage(r.first, out var a);
                stage.CellarSlotStage(r.last, out var b);
                float cx = (a.x + b.x) * 0.5f * StageToHud;
                float y = a.y * StageToHud - 6f + CounterLift;
                t.text = CellarGroupWord(r.group);
                // The plate is the width of what is written on it, never narrower than the
                // run it labels looks like it wants — measured, because "SODA & TONIC" and
                // "GIN" cannot share a box.
                float w = Mathf.Max(48f, t.preferredWidth + 16f);
                const float PlateH = 18f, PlateGap = 4f;
                float drop = 0f;
                bool sameShelf = !float.IsNaN(prevY) && Mathf.Abs(prevY - y) < 1f;
                if (sameShelf && cx - w * 0.5f < prevRight + PlateGap)
                    drop = prevDrop > 0f ? 0f : PlateH + 2f;   // alternate: down, level, down
                plate.sizeDelta = new Vector2(w, PlateH);
                plate.anchoredPosition = ToCentre(new Vector2(cx, y - drop));
                prevRight = cx + w * 0.5f; prevY = y; prevDrop = drop;
                // The plate's group is built with the plate (see above), so this is a read,
                // not a hunt. It was `GetComponent<CanvasGroup>() ?? Add...` for one afternoon
                // and threw MissingComponentException every frame the cellar was open: a
                // missing UnityEngine.Object is FAKE null — a live C# reference with an
                // overloaded ==, which `??` hands straight back.
                if (plate.TryGetComponent<CanvasGroup>(out var group)) group.alpha = phase;
            }
        }

        private List<float> CellarFills(TycoonRun run)
        {
            var fills = new List<float>(_cellarCards.Count);
            foreach (var c in _cellarCards)
            {
                var b = run?.Shelf?.Find(c.Id);
                fills.Add(b != null && b.Capacity > 0 ? (float)(b.Remaining / b.Capacity) : 0f);
            }
            return fills;
        }

        private readonly List<float> _lastCellarFills = new List<float>();

        /// <summary>Levels move as the night pours; the plates do not. Called from the frame —
        /// and the stage is only told on the frames a level actually moved (2026-09-04 audit:
        /// it rewrote thirty-six transforms a frame for nothing).</summary>
        private void PushCellarFills(TycoonRun run)
        {
            if (stage == null || _cellarCards.Count == 0) return;
            var fills = CellarFills(run);
            bool same = fills.Count == _lastCellarFills.Count;
            for (int i = 0; same && i < fills.Count; i++) same = fills[i] == _lastCellarFills[i];
            if (same) return;
            _lastCellarFills.Clear(); _lastCellarFills.AddRange(fills);
            stage.SetCellarFills(fills);
        }

        /// <summary>A bottle taken out of the cellar. The index is the stage's, into the list
        /// it was handed — kept in step by being filled in the one loop above.</summary>
        private void OnCellarPick(int index)
        {
            if (index < 0 || index >= _cellarCards.Count) return;
            GetComponent<TycoonServiceFlow>()?.PickFromCellar(_cellarCards[index]);
        }

        /// <summary>A fixture's sprite, from its own Resources shelf (PPU 1 — world art) —
        /// or, for a CARRIED piece whose drawing lives with the tools rather than with the
        /// room's dressing (2026-09-06, the shaker), off the Items shelf beside them.</summary>
        private static Sprite FixtureArt(string name) =>
            string.IsNullOrEmpty(name) ? null
            : Resources.Load<Sprite>("Fixtures/" + name) ?? ItemArt.Load(name);

        /// <summary>
        /// Where a ladder's rung stands, on the market card that sells it. READ OFF THE
        /// SLOT, not assumed: the wall lamps were the first ladder that was not a draught
        /// tower (2026-08-24) and their line went in as a constant — "the back wall · both
        /// lamps, one fitting" — which the brass sink then inherited and told the player
        /// about a basin on the counter (2026-08-25). The slot already carries both facts
        /// this needs, so a fourth ladder somewhere else needs no code here either.
        /// </summary>
        private string RungPlace(FixtureDefinition f)
        {
            LastCall.Game.StageSlot slot = null;
            var slots = _bootstrap != null ? _bootstrap.StageSlots : null;
            if (slots != null)
                foreach (var s in slots)
                    if (s.Id == f.Slot) { slot = s; break; }
            string where = slot != null && slot.OnCounter ? "The counter" : "The back wall";
            return where + (slot != null && slot.PairSpreadPx > 0f
                ? " · both of them, one fitting"
                : " · fitted over the mark below");
        }

        private void WatchGlassRack()
        {
            if (!GlassRackShown) { if (_glassRack != null) _glassRack.gameObject.SetActive(false); return; }
            var run = Run;
            if (run == null || _glassRack == null) return;
            var stage = _stage != null ? _stage : FindFirstObjectByType<DiegeticStage>();
            _stage = stage;
            if (stage == null) return;
            if (!stage.ShelfCell(0, out float cx, out float fy, out _)) return;
            if (Mathf.Approximately(cx, _rackCellX) && Mathf.Approximately(fy, _rackCellY)) return;
            _rackCellX = cx; _rackCellY = fy;
            RefreshGlassRack(run);
        }

        private void RefreshGlassRack(TycoonRun run)
        {
            if (_hudRoot == null || run.Glassware.Count == 0) return;
            if (_glassRack == null)
            {
                _glassRack = NewRect("GlassRack", _hudRoot);
                _glassRack.anchorMin = _glassRack.anchorMax = new Vector2(0.5f, 0);
                _glassRack.pivot = new Vector2(0.5f, 0);
                // The whole lower half: the compartments sit at HUD y 76..154 at the
                // reference aspect, well above the 110-unit strip this used to be.
                _glassRack.sizeDelta = new Vector2(1280, 360);
                _glassRack.anchoredPosition = Vector2.zero;
                UiAuditExempt.Mark(_glassRack,
                    "glasses on a shelf, sized and dimmed by which row they stand in — " +
                    "perspective, not chrome; rounding them to whole units moves the shelf");
                // BEHIND EVERYTHING. The rack is built lazily, on the first ApplyBarLook,
                // which is long after the HUD's own children exist — so it arrived as the
                // last sibling and drew over the menu keys, the bill and the whole market
                // (the author, 2026-08-09). It is scenery: it belongs at the back.
                _glassRack.SetAsFirstSibling();
            }
            _glassRack.SetAsFirstSibling();
            foreach (Transform c in _glassRack) Destroy(c.gameObject);

            // The compartments, asked of the bar itself. A missing stage (a bench, a test
            // scene) falls back to the old fixed spacing rather than stacking every glass
            // on top of the next.
            var stage = _stage != null ? _stage : FindFirstObjectByType<DiegeticStage>();
            _stage = stage;

            int i = 0;
            foreach (var g in run.Glassware)
            {
                int tier = run.GlassTier(g.Id);
                var piece = GlassArt.For(g, tier);
                int cell = GlassRackCells[Mathf.Min(i, GlassRackCells.Length - 1)];

                // Stage units, then doubled: the stage draws at 640x360 and the HUD at
                // 1280x720, so one is exactly two of the other.
                float x, floorY, cellH;
                if (stage != null && stage.ShelfCell(cell, out float sx, out float sy, out float sh))
                {
                    x = sx * StageToHud;
                    floorY = sy * StageToHud;
                    cellH = sh * StageToHud;
                }
                else
                {
                    x = -600f + i * 80f;
                    floorY = 8f;
                    cellH = RackGlassH;
                }
                // A SET OF FIVE, filling the bay with a little air at each end. Derived
                // rather than drawn — the line's OWN sprite at its own tier, five times —
                // so a bought rung changes all five at once and there is no second asset
                // to keep in step. Perspective is carried by DEPTH, not by scale alone:
                // the outermost stand furthest back, so they sit higher, smaller and
                // dimmer, which is what a shelf drawn from slightly above looks like.
                // The RUN is sized from the BAY, not the bay from the glass. Five glasses
                // as tall as the opening allows would each be 47 units wide in a 150-unit
                // interior, so they would land on top of one another; asking instead what
                // width lets five stand across the bay with a clean overlap, and taking the
                // height from THAT, is the only ordering that fills the shelf.
                // TWO ROWS IN DEPTH, NOT FIVE ACROSS (the author: the glasses should follow
                // the table's perspective, and cover more of it). Five in one line is a
                // frieze; a shelf holds a row at the back and a row in front of it, and
                // that is where a real front-to-back height difference comes from.
                //
                // THREE BACK, TWO FRONT. The back row stands on the far edge of the
                // surface, the front row on the near edge, and the depth between them is
                // MEASURED: the turquoise band is 13 art px of the cell's 53, so the two
                // rows are (13/53) of the opening apart on screen.
                const int BackRow = 3, FrontRow = 2;
                const float Overlap = 0.60f;                    // step within a row
                // The surface is 93..105 in the art and the cell opening is 53 tall, so the
                // far edge is nine art pixels behind the near one — NOT thirteen. Thirteen
                // was the whole band including its own front lip, and it stood the back row
                // clean off the shelf.
                const float SurfaceDepth = DiegeticStage.ShelfDepthPx / 53f;
                float bay = cellH * (75f / 53f);                // the interior, in HUD units
                // PROPORTION ACROSS THE WHOLE RACK, not within one bay. Sizing each line to
                // fill its own bay made a rocks tumbler and a highball the same height,
                // because the wide one had to shrink to fit five across — so the shelf said
                // they were the same glass. The run is measured from the WIDEST vessel in
                // the set instead, and every other line is drawn at the same units-per-
                // sprite-pixel, which is what makes a pint taller than a tumbler on screen
                // exactly as it is on the page.
                // ONE k FOR THE WHOLE SET — HUD units per sprite pixel — taken from the
                // widest and tallest vessels the bar owns. Dividing by each sprite's own
                // height instead gave every line the same drawn height, which is the very
                // thing that was wrong: a rocks tumbler is not as tall as a pint.
                float widestPx = 1f, tallestPx = 1f;
                foreach (var other in run.Glassware)
                {
                    var op = GlassArt.For(other, run.GlassTier(other.Id));
                    if (op.Sprite == null) continue;
                    widestPx = Mathf.Max(widestPx, op.Sprite.rect.width);
                    tallestPx = Mathf.Max(tallestPx, op.Sprite.rect.height);
                }
                // The back row is the wider one, so it sets the size; the front row then
                // has room to sit between its gaps.
                float wForBay = (bay - 4f) / (1f + Overlap * (BackRow - 1));
                // A shade smaller (the author): five vessels and two rows want a little air
                // between them and the shelf above.
                float unitsPerPixel = Mathf.Min(wForBay / widestPx, (cellH - 10f) / tallestPx) * 0.88f;
                float h = piece.Sprite.rect.height * unitsPerPixel;
                float gw = h * piece.Aspect;
                float step = gw * Overlap;
                float rise = cellH * SurfaceDepth;              // the far edge, in HUD units
                for (int k = 0; k < BackRow + FrontRow; k++)
                {
                    // 0..2 are the back row, 3..4 the front row standing in its gaps.
                    bool back = k < BackRow;
                    int inRow = back ? k : k - BackRow;
                    int rowCount = back ? BackRow : FrontRow;
                    int depth = back ? 1 : 0;
                    float dx = (inRow - (rowCount - 1) * 0.5f) * step * (back ? 1f : 1.6f);
                    var rt = NewRect($"G_{g.Id}_{k}", _glassRack);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                    rt.pivot = new Vector2(0.5f, 0);
                    // The far row is smaller AND higher by the surface's own depth — the
                    // two together are what perspective is. 3 units of rise, which is what
                    // this was, is a nudge; the drawn floor is thirteen art pixels deep.
                    float kh = h * (back ? 0.84f : 1f);
                    rt.sizeDelta = new Vector2(kh * piece.Aspect, kh);
                    rt.anchoredPosition = new Vector2(x + dx, floorY + (back ? rise : 0f));
                    // A CONTACT SHADOW UNDER EACH ONE. They are standing IN a shelf now,
                    // not on a lit counter, and nothing sells that like the dark pooling
                    // where the glass meets the wood. Laid before the glass so it reads as
                    // underneath it, and narrower than the foot so it stays a contact
                    // rather than a halo.
                    var foot = NewRect($"S_{g.Id}_{k}", _glassRack);
                    foot.anchorMin = foot.anchorMax = new Vector2(0.5f, 0);
                    foot.pivot = new Vector2(0.5f, 0.5f);
                    foot.sizeDelta = new Vector2(kh * piece.Aspect * 0.86f, 7f);
                    foot.anchoredPosition = new Vector2(x + dx,
                        floorY + (back ? rise : 0f) + 2f);
                    var footImg = foot.gameObject.AddComponent<Image>();
                    footImg.sprite = BackBarArt.BottleShadow();
                    footImg.raycastTarget = false;
                    footImg.color = new Color(0f, 0f, 0f, back ? 0.42f : 0.62f);
                    foot.SetSiblingIndex(rt.GetSiblingIndex());

                    var img = rt.gameObject.AddComponent<Image>();
                    img.sprite = piece.Sprite;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    // AND THE GLASS ITSELF IS IN SHADOW. A bay is a hole in the bar front:
                    // the light that reaches it comes from in front and above and falls off
                    // fast, so even the near row sits well under full brightness and the far
                    // row further still. Drawing them at 1.0 lit them as if they were on the
                    // counter, which is the one place they are not.
                    float lit = (back ? 0.58f : 0.78f);
                    img.color = new Color(lit * 0.96f, lit * 1.0f, lit * 1.08f, 1f);
                    for (int d = 0; d < depth; d++) rt.SetAsFirstSibling();
                }
                // No tier stars under the rack (the author, 2026-08-02): the glass's own
                // dress already says which rung it is, and a row of stars under every one
                // read as a scoreboard bolted to the counter.
                i++;
            }
        }

        private void RefreshTopBar()
        {
            var run = Run;
            // The clock, not a quota (v5 P12 / C5): a shift from 18:00 to 02:00. The day
            // number survives underneath — rent, the ledger and the strike count all still
            // count days — it simply stops being what the player reads the night by.
            double hour = run.Floor.ClockHour;
            int hh = (int)hour % 24, mm = (int)((hour - (int)hour) * 60);
            // The sky outside runs on this same clock (2026-08-19): the window holds an
            // evening's worth of frames — a low sun at 18:00 through the pink band to a lit
            // city by 02:00 — and the shift's fraction is simply which frame is up. Driven
            // from here, beside the hour it belongs to, so the plaque and the glass can never
            // disagree about what time it is.
            if (stage != null) stage.SetSkyFraction((float)run.Floor.NightFraction);
            // The plaque's rule is the state light: cyan through the shift, magenta once the
            // room is being called — visible from across the screen without reading a word.
            bool last = run.Floor.IsClosingTime;
            // The colon keeps the second, which is the one thing on this board that moves on
            // its own. A display whose colon is painted on is a picture of a clock.
            if (_clock != null)
            {
                if (last != _clockWasLast)
                {
                    _clockWasLast = last;
                    if (last) Sfx.Play("last_call_bell", 0.7f);   // on the way in only
                    _clock.SetHue(last ? UITheme.Magenta[4] : UITheme.Cyan[4]);
                }
                _clock.Show(hh, mm / 5 * 5, ((int)(Time.unscaledTime * 2f) & 1) == 0);
            }
            // The night names itself on the marquee — tonight's bulb is lit and its letters
            // are amber — so nothing up here prints the day in words as well. Printing it
            // twice across one board is what made the old one read as assembled.
            RefreshWeekStrip(run);
            // The night's well (2026-09-07): the count and the name, in place of the week.
            if (_dayLabel != null) _dayLabel.text = $"{run.Day:00}";
            if (_nightLabel != null)
                _nightLabel.text = BarCalendar.Name(BarCalendar.NightOf(run.Day)).ToUpperInvariant();

            // THE BEAM IS THE STATE LIGHT (2026-08-14; it now answers to two states, not
            // one). A 2px rule under one plaque was never going to be seen, and the board
            // itself changing colour is read before anything is read: amber through the
            // shift, magenta once the room is being called.
            //
            // DEBT JOINED IT ON 2026-08-26. Under water used to redden the REGISTER's own
            // window, and the register left the room with the rest of the money (the author:
            // "kasa ve parayı ana sahneden kaldır"), so the beam took the reading. It beats
            // last call, because a bar in the red is the more urgent of the two facts — and
            // it is a colour, not a figure: how DEEP under is the book's business (behind the
            // cog) and the slip's. Both are driven from HERE, off one cached state, because
            // the tube used to be painted inside the clock's own change-check and a second
            // writer keyed on a different change would have left it wearing whichever of them
            // moved last.
            bool underWater = run.Money < 0;
            int beam = underWater ? 2 : last ? 1 : 0;
            if (beam != _beamState)
            {
                _beamState = beam;
                var core = underWater ? UITheme.ViceRed[3]
                    : last ? UITheme.Magenta[4] : UITheme.Amber[4];
                var halo = underWater ? UITheme.ViceRed[2]
                    : last ? UITheme.Magenta[2] : UITheme.Amber[2];
                if (_neonTube != null) _neonTube.color = core;
                if (_neonBloom != null)
                    _neonBloom.color = new Color(halo.r, halo.g, halo.b, beam == 0 ? 0.30f : 0.42f);
            }

            // The caption line over the standing carries the crowd — and gives way to LAST
            // CALL when the room is being called, because at that point what is in front of
            // the bar matters more than who it is.
            _crowdText.text = last ? "LAST CALL"
                : run.CrowdToday == WealthTier.HighRoller ? "HIGH ROLLERS"
                : run.CrowdToday == WealthTier.Broke ? "BROKE CROWD" : "REGULARS";
            _crowdText.color = last ? UITheme.Magenta[4]
                : run.CrowdToday == WealthTier.HighRoller ? UITheme.Magenta[4]
                : run.CrowdToday == WealthTier.Broke ? UITheme.ViceRed[3] : UITheme.Cream[3];

            // The standing, as a row of stars and NOTHING ELSE (2026-08-19, the author:
            // "0.0 neden gösteriliyor, daha çok görsel bir şerit olmalı"). The number that
            // read beside them is gone: the fill IS the reading. A half-lit star is a real
            // half — the average is continuous, and the mask's width carries it exactly,
            // so nothing legible was lost; the decimal lives on in the ledger and the shop,
            // where a number is being compared to another number.
            double stars = run.Rating.Average;
            _starsFill.sizeDelta = new Vector2((float)(stars / 5.0) * _ratingStars.Length * StarGap, 0);
            // The house's two strips (H5): the drinks so far tonight, and the room right now
            // — the one reading that moves while a glass stands on the counter.
            if (_serviceFill != null)
                _serviceFill.sizeDelta = new Vector2((float)(run.ServiceTonight / BarRating.MaxStars) * HouseStripW, 0);
            if (_comfortFill != null)
                _comfortFill.sizeDelta = new Vector2((float)(run.ComfortNow / BarRating.MaxStars) * HouseStripW, 0);

            RefreshJobStrip(run);
            StepJobStrip();

            // ECE SETTLES UP THE MOMENT IT LANDS (2026-09-06). The run raises the flag on the
            // serve — or on the night, for a clean week — and the room says so once, with the
            // coin beside it and the money already in the till.
            var done = run.TakeJobJustDone();
            if (done != null)
            {
                Toast($"{done.Who} PAYS UP · +${done.Reward}", UITheme.Lime[3], 3.2f,
                      ChromeArt.Mark("cash"));
                Sfx.Play("cash", 0.9f);
                LogService($"<color=#6FCC4B>{done.Who}'S JOB</color> done · +${done.Reward}");
            }
        }

        /// <summary>
        /// The week's job in one line: how many are left, and of what. It counts DOWN rather
        /// than up — "3 MORE NEGRONIS" is an instruction and "2/5 NEGRONI" is a scoreboard,
        /// and this sits beside a LOG key at 8px where only one of those is worth the room.
        /// Done, it says so in the lime the rest of the game says "landed" in, and stays
        /// said for the rest of the week: a job finished on Tuesday should still be visible
        /// on Friday, or the player cannot tell it from one never given.
        /// </summary>
        private void RefreshJobStrip(TycoonRun run)
        {
            if (_jobStrip == null) return;
            var job = run.Job;
            bool show = job != null && job.RunsOn(run.Day);
            if (_jobIcon != null && _jobIcon.gameObject.activeSelf != show)
                _jobIcon.gameObject.SetActive(show);
            // The plate is a notice; an empty plate is a hole in the screen (photographed
            // the week it went up). The whole row goes with the job.
            if (_jobStripRow != null && _jobStripRow.gameObject.activeSelf != show)
                _jobStripRow.gameObject.SetActive(show);
            if (!show) { _jobStrip.text = ""; return; }
            // THE ICON SAYS WHAT KIND OF WEEK IT IS (2026-09-06, the author: "bildirimlerde
            // alkollerin nesnelerin paranın yıldızın ... ikonlarından faydalan"): the drink
            // itself for a count of one drink, a star for perfect pours, the cloth for a run
            // of clean nights. Read before the words are.
            if (_jobIcon != null)
            {
                Sprite art = null;
                switch (job.Kind)
                {
                    case JobKind.Perfect:
                        art = ItemArt.Star(true, 16);
                        break;
                    case JobKind.Clean:
                        art = ItemArt.Load("bar_cloth") ?? ItemArt.Load("cloth");
                        break;
                    default:
                        foreach (var r in run.AllRecipes)
                            if (r.Id == job.RecipeId) { art = DrinkIcon.For(r, _bootstrap.Glassware); break; }
                        break;
                }
                _jobIcon.sprite = art;
                _jobIcon.enabled = art != null;
                _jobIcon.color = job.IsDone ? UITheme.Lime[3] : Color.white;
            }
            // THE COUNT, ON A PLATE (2026-09-06, the author: "Ecenin görev bildirimleri daha
            // dikkat çekici olmalı arkaplanda yok oluyor bir plaka olmalı onun üstünde yazmalı
            // ve kaçta kaç olduğu yazmalı"). What is asked, as a fraction of the week —
            // "2/5" — beside the giver's name, and the plate behind the row is cut to the
            // line so the notice is a THING on the screen rather than words over the room.
            string what;
            switch (job.Kind)
            {
                case JobKind.Perfect: what = "PERFECT POURS"; break;
                case JobKind.Clean: what = "CLEAN NIGHTS"; break;
                default: what = (job.RecipeName ?? "").ToUpperInvariant(); break;
            }
            _jobStrip.text = job.IsDone
                ? $"<color=#6FCC4B>{job.Who} · {job.Target}/{job.Target} · DONE · +${job.Reward}</color>"
                : $"<color=#E84DA6>{job.Who}</color> · <color=#F2E8D5>{job.Served}/{job.Target}</color> · {what}";
            if (_jobPlate != null)
            {
                _jobStripRow.sizeDelta = new Vector2(28f + _jobStrip.preferredWidth + 14f, 26f);
                _jobPlate.color = job.IsDone
                    ? new Color(UITheme.Lime[0].r, UITheme.Lime[0].g, UITheme.Lime[0].b, 0.96f)
                    : new Color(UITheme.Night[1].r, UITheme.Night[1].g, UITheme.Night[1].b, 0.96f);
            }
        }

        /// <summary>
        /// THE STRIP GETS OUT OF THE WAY WITHOUT LEAVING (2026-09-06, the author: "çok uzun
        /// üstüne bir nesne veya asset geldiğinde şeffaflaşmalı (yok olmamalı sadece biraz
        /// şeffaflaşmalı) mouse ile üstüne gelindiğinde netleşmeli"). It sits over the room,
        /// so anything the room raises into that corner — a drinker walking in, the cellar
        /// coming up — would be read through it. It fades to a third rather than going, so
        /// the job is never a thing the player has to remember was there, and the pointer
        /// brings it back whole.
        /// </summary>
        private void StepJobStrip()
        {
            if (_jobStrip == null || _jobStripGroup == null) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool under = false;
            if (mouse != null && _jobStrip.gameObject.activeInHierarchy)
                under = RectTransformUtility.RectangleContainsScreenPoint(
                    _jobStripRow, mouse.position.ReadValue(), null);
            // Busy behind it: the counter is up (the cellar is open) or a bench is over the
            // room. Both are the moments the corner has something else to say.
            bool busy = CellarOpen || (_flow != null && _flow.IsOpen);
            float want = under ? 1f : busy ? 0.35f : 1f;
            _jobStripGroup.alpha = Motion.Reduced ? want
                : Mathf.MoveTowards(_jobStripGroup.alpha, want, Time.unscaledDeltaTime * 3.5f);
        }

        private void BuildServiceLog(RectTransform root)
        {
            // The key stays put under the fascia; only the sheet below it comes and goes.
            NewButton(root, "LOG", new Vector2(0, 1), new Vector2(44, 20),
                new Vector2(10, -66), UITheme.Night[2], ToggleServiceLog);

            // THE WEEK'S JOB, BESIDE THE LOG KEY (2026-09-04, the author: "bu görev oyun
            // içerisinde LOG'un olduğu yerde çok yer kaplamamalı"). One line, 8px, on the
            // LOG key's own row and running off to its right: a count, then the drink. It
            // is not a panel and it does not open — a job you have to press for is a job
            // nobody reads, and a job with a box around it is a second window on a screen
            // whose whole rule is that its instruments are objects in the room.
            //
            // It draws NOTHING at all before the first hand-over, so week one is exactly
            // the screen it was.
            // One row, so the icon and the line fade together and can be asked whether the
            // pointer is on THEM rather than on the text's own overflowing rect.
            _jobStripRow = NewRect("WeekJobRow", root);
            Place(_jobStripRow, new Vector2(0, 1), new Vector2(320, 26), new Vector2(60, -66));
            _jobStripRow.pivot = new Vector2(0, 0.5f);
            _jobStripGroup = _jobStripRow.gameObject.AddComponent<CanvasGroup>();
            _jobStripGroup.blocksRaycasts = false;
            // The plate (2026-09-06): the house card, cut to the line by RefreshJobStrip, with
            // the aisle sign's pip at its head so it reads as a notice and not as a caption.
            _jobPlate = _jobStripRow.gameObject.AddComponent<Image>();
            _jobPlate.sprite = ChromeArt.Card();
            _jobPlate.type = Image.Type.Sliced;
            _jobPlate.color = new Color(UITheme.Night[1].r, UITheme.Night[1].g, UITheme.Night[1].b, 0.96f);
            _jobPlate.raycastTarget = false;
            var jobPip = NewRect("Pip", _jobStripRow);
            Place(jobPip, new Vector2(0, 0.5f), new Vector2(3, 16), new Vector2(0, 0));
            jobPip.pivot = new Vector2(0, 0.5f);
            var pipImg = jobPip.gameObject.AddComponent<Image>();
            pipImg.color = UITheme.Magenta[3];
            pipImg.raycastTarget = false;
            var iconRt = NewRect("Icon", _jobStripRow);
            Place(iconRt, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(9, 0));
            _jobIcon = iconRt.gameObject.AddComponent<Image>();
            _jobIcon.preserveAspect = true;
            _jobIcon.raycastTarget = false;
            iconRt.gameObject.SetActive(false);

            _jobStrip = NewText("WeekJob", _jobStripRow, _display, 8, TextAnchor.MiddleLeft,
                UITheme.Cream[3]);
            Place(_jobStrip.rectTransform, new Vector2(0, 0.5f), new Vector2(292, 20),
                new Vector2(30, 0));
            _jobStrip.rectTransform.pivot = new Vector2(0, 0.5f);
            _jobStrip.horizontalOverflow = HorizontalWrapMode.Overflow;
            _jobStrip.verticalOverflow = VerticalWrapMode.Truncate;
            _jobStrip.supportRichText = true;
            _jobStrip.raycastTarget = false;
            _jobStrip.text = "";

            var panel = _serviceLogPanel = NewRect("ServiceLog", root);
            Place(panel, new Vector2(0, 1), new Vector2(430, 150), new Vector2(10, -90));
            panel.pivot = new Vector2(0, 1);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.55f);
            bg.raycastTarget = false;
            _serviceLog = NewText("Lines", panel, _body, 8, TextAnchor.UpperLeft, UITheme.TextSecondary);
            _serviceLog.supportRichText = true;
            Stretch(_serviceLog.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 4), new Vector2(-6, -4));
            panel.gameObject.SetActive(_serviceLogOpen);
        }

        private void ToggleServiceLog()
        {
            _serviceLogOpen = !_serviceLogOpen;
            if (_serviceLogPanel != null) _serviceLogPanel.gameObject.SetActive(_serviceLogOpen);
        }

        private void LogService(string line)
        {
            if (_serviceLog == null) return;
            _serviceLogLines.Insert(0, line);
            while (_serviceLogLines.Count > ServiceLogMax)
                _serviceLogLines.RemoveAt(_serviceLogLines.Count - 1);
            _serviceLog.text = string.Join("\n", _serviceLogLines);
        }

        /// <summary>The visit's score for the log, in marks a pixel font can actually draw.
        /// It used to be U+2605 — the same glyph that was printing five empty boxes over the
        /// payment float, and the same one this project already replaces with an asterisk
        /// wherever a licence name carries it (2026-08-11).</summary>
        private static string LogStars(double satisfaction) =>
            new string('*', Mathf.Clamp(Mathf.RoundToInt((float)satisfaction * 5f), 0, 5));

        /// <summary>The judge's verdict, said as one log line with its reasons.</summary>
        private void LogVerdict(CustomerVisit visit, ServiceVerdict verdict)
        {
            string ordered = visit.IdInspected ? visit.Order.Wanted.Name.ToUpperInvariant() : "?";
            string made = visit.Served != null ? visit.Served.Name.ToUpperInvariant() : "NOTHING NAMED";
            string col = verdict.Match == OrderMatch.Exact ? "8CE28C"
                : verdict.Match == OrderMatch.Close ? "F5C97B" : "F27D8A";
            var why = new List<string>();
            if (verdict.Match == OrderMatch.Wrong) why.Add($"made {made}");
            // A Close line names its own reason or it is a mystery (2026-08-14). The grade is
            // "their drink, out of tolerance", and the glass usually matches no recipe at all,
            // so `made` would read NOTHING NAMED and the reasons list would come back empty —
            // a serve that paid less than the last one with nothing on the line to say why.
            if (verdict.Match == OrderMatch.Close) why.Add("measures off");
            if (verdict.SpecScore < 0.999) why.Add($"spec {verdict.SpecScore:P0}");
            if (verdict.FillScore < 0.999) why.Add($"fill {verdict.FillScore:P0}");
            string reasons = why.Count > 0 ? "  <color=#9C8F80>(" + string.Join(", ", why) + ")</color>" : "";
            LogService($"<color=#{col}>{verdict.Match.ToString().ToUpperInvariant()}</color> {ordered}" +
                       $" · ${verdict.BasePaid}+${verdict.Tip} · {LogStars(verdict.Satisfaction)}{reasons}");
        }

        private void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            bool show = !_settingsPanel.gameObject.activeSelf;
            if (show) CloseId();
            _settingsPanel.gameObject.SetActive(show);
            if (show) RefreshSettings();
        }

        // The menu's plate and its margins, in one place.
        private const float SetPlateW = 520f, SetPlateH = 404f, SetInset = 24f, SetRowH = 40f;

        /// <summary>
        /// A MENU, NOT A LIST (2026-09-06, the author: "ayarlar menüsü tekrardan tasarlansın
        /// şu an öylesine koyulmuş bir menü mevcut, bunu profesyonel bir oyun menüsü haline
        /// getir, dev tools için kenara şimdilik bir buton koyabilirsin"). Six keys stacked
        /// in a corner was a dev panel with three settings in it. This is a WINDOW over the
        /// room, the way the licence and the market are: its own scrim, a titled plate in
        /// the centre, and the settings as rows — the name on the left, the control on the
        /// right — grouped under what they are about. The run's own verbs (the book, a new
        /// run) are the last group, so the one thing that throws the night away sits
        /// furthest from the thumb; the developer's bench is one small key at the plate's
        /// foot, where it is found and not pressed by accident.
        /// </summary>
        private void BuildSettings(RectTransform root)
        {
            _settingsPanel = NewRect("Settings", root);
            var canvas = _settingsPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 23;                 // over the market (22), under the guide (24)
            _settingsPanel.gameObject.AddComponent<ForgivingRaycaster>();
            Stretch(_settingsPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _settingsPanel.gameObject.AddComponent<Image>();
            scrim.color = UITheme.Scrim;
            var scrimBtn = _settingsPanel.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(ToggleSettings);

            var plate = NewRect("Plate", _settingsPanel);
            Place(plate, new Vector2(0.5f, 0.5f), new Vector2(SetPlateW, SetPlateH), new Vector2(0, 10));
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.sprite = ChromeArt.Panel();          // the house's panel (2026-09-08)
            plateImg.type = Image.Type.Sliced;
            plateImg.pixelsPerUnitMultiplier = 0.5f;
            plate.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // swallow clicks

            // The title band: the cog it opened from, the word, and the neon under it.
            var band = NewRect("Band", plate);
            // inside the panel's 6-unit rule, not over it (2026-09-08)
            Place(band, new Vector2(0.5f, 1), new Vector2(SetPlateW - 12f, 44f), new Vector2(0, -6f));
            band.pivot = new Vector2(0.5f, 1);
            var bandImg = band.gameObject.AddComponent<Image>();
            bandImg.color = UITheme.Night[0];
            bandImg.raycastTarget = false;
            var cog = NewRect("Cog", band);
            Place(cog, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(SetInset, 0));
            cog.pivot = new Vector2(0, 0.5f);
            var cogImg = cog.gameObject.AddComponent<Image>();
            cogImg.sprite = ChromeArt.Mark("cog");
            cogImg.color = UITheme.Amber[4];
            cogImg.raycastTarget = false;
            var title = NewText("T", band, _display, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
            Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(300, 20), new Vector2(SetInset + 28f, 0));
            title.rectTransform.pivot = new Vector2(0, 0.5f);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = "SETTINGS";
            Hairline(band, new Vector2(0, 0), new Vector2(1, 0), UITheme.Amber[3]);

            float y = -62f;

            // ── AUDIO ────────────────────────────────────────────────────────────
            SettingsCaption(plate, "AUDIO", ref y);
            var vol = SettingsLine(plate, "VOLUME", null, ref y);
            // The volume is a meter with a key at each end: five blocks, a fifth apiece.
            // (It used to be one key that CYCLED 20% at a press — six presses to turn it
            // down a notch, and nothing on it said which way it was going.)
            const float VolKeyW = 36f, VolCell = 16f;
            SettingsKey(vol, VolKeyW, 0f, "+", UITheme.Night[3], () =>
            {
                Sound.Volume = Mathf.Clamp01(Mathf.Round((Sound.Volume + 0.2f) * 5f) / 5f);
                Sfx.Play("click");
                RefreshSettings();
            });
            _settingsMeter = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var cell = NewRect("M" + i, vol);
                Place(cell, new Vector2(1, 0.5f), new Vector2(12f, 14f),
                    new Vector2(-(VolKeyW + 8f + (4 - i) * VolCell + 4f), 0));
                cell.pivot = new Vector2(1, 0.5f);
                _settingsMeter[i] = cell.gameObject.AddComponent<Image>();
                _settingsMeter[i].raycastTarget = false;
            }
            SettingsKey(vol, VolKeyW, VolKeyW + 8f + 5f * VolCell + 8f, "-", UITheme.Night[3], () =>
            {
                Sound.Volume = Mathf.Clamp01(Mathf.Round((Sound.Volume - 0.2f) * 5f) / 5f);
                Sfx.Play("click");
                RefreshSettings();
            });
            _settingsVolume = NewText("V", vol, _body, 8, TextAnchor.MiddleRight, UITheme.Cream[3]);
            Place(_settingsVolume.rectTransform, new Vector2(1, 0.5f), new Vector2(60, 12),
                new Vector2(-(VolKeyW * 2f + 16f + 5f * VolCell + 10f), 0));
            _settingsVolume.rectTransform.pivot = new Vector2(1, 0.5f);
            _settingsVolume.horizontalOverflow = HorizontalWrapMode.Overflow;

            var snd = SettingsLine(plate, "SOUND", null, ref y);
            _settingsMute = SettingsKey(snd, 96f, 0f, "ON", UITheme.Night[3], () =>
            {
                Sound.Muted = !Sound.Muted;
                Sfx.Play("click");              // audible iff it just came back on — itself the test
                RefreshSettings();
            });

            // ── DISPLAY ──────────────────────────────────────────────────────────
            y -= 10f;
            SettingsCaption(plate, "DISPLAY", ref y);
            var mot = SettingsLine(plate, "MOTION", "REDUCED: NO SLIDES, NO FLOATS, NO FADES", ref y);
            _settingsMotion = SettingsKey(mot, 96f, 0f, "FULL", UITheme.Night[3], () =>
            {
                Motion.Reduced = !Motion.Reduced;
                Sfx.Play("click");
                RefreshSettings();
            });

            // ── THE RUN ──────────────────────────────────────────────────────────
            // THE BOOK LOST ITS DOOR WITH THE TILL (2026-08-26, the author: "kasa ve parayı
            // ana sahneden kaldır"): nothing counts money at you while you are serving, so
            // the night's ledger lives here, one press away for anybody who wants it.
            // NEW RUN LIVES HERE TOO (2026-08-14, the author: "new run yazısını ayarların
            // içine taşı"): a thing that throws the night away belongs behind a door.
            y -= 10f;
            SettingsCaption(plate, "THE RUN", ref y);
            var book = SettingsLine(plate, "TONIGHT'S BOOK", "EVERY LINE THE TILL HAS TAKEN", ref y);
            SettingsKey(book, 96f, 0f, "OPEN", UITheme.Night[3], () => { ToggleSettings(); ToggleLedger(); });
            var fresh = SettingsLine(plate, "START OVER", "DAY 1, AN EMPTY BAR — THIS NIGHT IS LOST", ref y);
            SettingsKey(fresh, 96f, 0f, "NEW RUN", UITheme.Brick[2], () =>
            { _bootstrap.StartNewRun(null); ToggleSettings(); });

            // The foot: the developer's door at one corner, the way out at the other.
            // THE WORKBENCH IS NOT A SETTING (2026-08-14, the author: "ayarlarla dev toolu
            // ayır"); it keeps one small key here, for now, because the author asked for one.
            NewButton(plate, "DEV TOOLS", new Vector2(0, 0), new Vector2(110, 26),
                new Vector2(SetInset, 16), UITheme.Night[2], () => { ToggleSettings(); ToggleDevBench(); });
            NewButton(plate, "CLOSE", new Vector2(1, 0), new Vector2(120, 32),
                new Vector2(-SetInset, 14), UITheme.PrimaryAction, ToggleSettings);

            _settingsPanel.gameObject.SetActive(false);
        }

        /// <summary>A group's caption: small amber caps over its rows.</summary>
        private void SettingsCaption(RectTransform plate, string text, ref float y)
        {
            var t = NewText("G_" + text, plate, _body, 8, TextAnchor.LowerLeft, UITheme.Amber[3]);
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(300, 16), new Vector2(SetInset, y));
            t.rectTransform.pivot = new Vector2(0, 1);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = text;
            y -= 18f;
        }

        /// <summary>One setting's row: its name (and a note under it, if it needs one) on
        /// the left, a hairline under the row; the control is added by the caller at the
        /// right edge.</summary>
        private RectTransform SettingsLine(RectTransform plate, string name, string note, ref float y)
        {
            var row = NewRect("R_" + name, plate);
            Place(row, new Vector2(0, 1), new Vector2(SetPlateW - SetInset * 2f, SetRowH), new Vector2(SetInset, y));
            row.pivot = new Vector2(0, 1);
            Hairline(row, new Vector2(0, 0), new Vector2(1, 0), new Color(1f, 1f, 1f, 0.07f));
            var t = NewText("N", row, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
            Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(260, 20), new Vector2(0, note != null ? 6f : 0f));
            t.rectTransform.pivot = new Vector2(0, 0.5f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = name;
            if (note != null)
            {
                var n = NewText("Note", row, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
                Place(n.rectTransform, new Vector2(0, 0.5f), new Vector2(320, 12), new Vector2(0, -10f));
                n.rectTransform.pivot = new Vector2(0, 0.5f);
                n.horizontalOverflow = HorizontalWrapMode.Overflow;
                n.raycastTarget = false;
                n.text = note;
            }
            y -= SetRowH;
            return row;
        }

        /// <summary>A row's control: the house key, right-aligned, its word returned so the
        /// refresh can rewrite it (ON / OFF, FULL / REDUCED).</summary>
        private Text SettingsKey(RectTransform row, float w, float rightInset, string label, Color fill, Action onClick)
        {
            // The helper names the rect after its label and WRITES the label, so the word
            // goes in bare (a "K_" prefix here printed itself on every key, photographed).
            var key = NewButton(row, label, new Vector2(1, 0.5f), new Vector2(w, 28f),
                new Vector2(-rightInset, 0), fill, onClick);
            return key.GetComponentInChildren<Text>();
        }

        private void ToggleDevBench()
        {
            if (_devPanel == null) return;
            bool show = !_devPanel.gameObject.activeSelf;
            if (show) { CloseId(); RefreshDevBench(); }
            _devPanel.gameObject.SetActive(show);
        }

        /// <summary>The bench's face: Unity's built-in Arial. The bench is the author's
        /// own tool and not part of the game (2026-09-06: "dev tools sadece benim için oyunda
        /// olmayacağından tasarımı açık ve net olsun yeter pixel art olmasına gerek yok"), so
        /// it is set in a plain proportional face that has every Turkish glyph and reads at
        /// any size — never in the pixel faces, which have neither.</summary>
        private Font _plain;

        private Font Plain =>
            _plain != null ? _plain : _plain = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void BuildDevBench(RectTransform root)
        {
            _devPanel = NewRect("DevBench", root);
            Place(_devPanel, new Vector2(0.5f, 0.5f), new Vector2(1180, 640), new Vector2(0, 6));
            var canvas = _devPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 25;                 // above the guide (24) and the market (22)
            _devPanel.gameObject.AddComponent<ForgivingRaycaster>();
            var bg = _devPanel.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.985f);
            bg.raycastTarget = true;
            Frame(_devPanel, 2f, UITheme.Cyan[3]);    // cyan, not amber: this is not the game

            // IN TURKISH, IN A PLAIN FACE (2026-09-06, the author: "dev tools güncellensin ve
            // metinleri türkçe ve okunaklı normal bir fontta üretilsin"). Nothing here is
            // the game's voice; it is the author's own bench, and it speaks their language.
            var title = NewText("T", _devPanel, Plain, 22, TextAnchor.MiddleLeft, UITheme.Cyan[3]);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(600, 28), new Vector2(20, -20));
            title.fontStyle = FontStyle.Bold;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = "GELİŞTİRİCİ TEZGÂHI";

            _devStanding = NewText("S", _devPanel, Plain, 14, TextAnchor.MiddleRight, UITheme.Cream[2]);
            Place(_devStanding.rectTransform, new Vector2(1, 1), new Vector2(640, 18), new Vector2(-20, -24));
            _devStanding.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── the left rail: the verbs ────────────────────────────────────────
            int slot = 0;
            DevHeading(ref slot, "KOŞU");
            DevKey(ref slot, "YENİ KOŞU", "1. gün, boş bar",
                () => { _bootstrap.StartNewRun(null); ToggleDevBench(); });
            DevKey(ref slot, "ORTA OYUN", "12. gün, stoklu",
                () => { _bootstrap.StartNewRun(null); Run.DevPreset(1); ApplyBarLook(); ToggleDevBench(); });
            DevKey(ref slot, "SON OYUN", "geç koşu, dolu raf",
                () => { _bootstrap.StartNewRun(null); Run.DevPreset(2); ApplyBarLook(); ToggleDevBench(); });

            DevHeading(ref slot, "SAAT");
            DevKey(ref slot, "GÜN SONUNA ATLA", "şimdi kapat, marketi aç", () =>
            {
                if (Run == null || Run.Phase != TycoonPhase.DayOpen) { Toast("NOT MID-DAY"); return; }
                _flow?.CloseFlow();
                CloseId();
                Run.DevSkipToDayEnd();
                ToggleDevBench();
            });
            DevKey(ref slot, "SON SİPARİŞE ATLA", "geceye atla, sonra sonuna kadar oynat",
                DevJumpToLastCall);

            DevHeading(ref slot, "İNSANLAR");
            DevKey(ref slot, "ODA", "her müşteri, kâğıtları ve yıldızı",
                () => { ToggleDevBench(); ToggleGuide(); });

            // ── the right pane: the lineup ──────────────────────────────────────
            // FOUR COLUMNS, NOT ONE PADDED STRING (2026-09-06): the bench is set in a
            // proportional face now, so the columns are rects rather than runs of spaces.
            var head = NewRect("H", _devPanel);
            Place(head, new Vector2(0, 1), new Vector2(820, 20), new Vector2(340, -48));
            head.pivot = new Vector2(0, 1);
            DevColumns(head, "FİYAT", "AD", "NASIL YAPILIR", "NE İSTER", UITheme.Cream[2], true);

            var view = NewRect("LineupView", _devPanel);
            Place(view, new Vector2(0, 1), new Vector2(820, 556), new Vector2(340, -58));
            view.pivot = new Vector2(0, 1);
            view.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.02f);
            view.gameObject.AddComponent<RectMask2D>();
            _devRows = NewRect("Rows", view);
            _devRows.anchorMin = new Vector2(0, 1); _devRows.anchorMax = Vector2.one;
            _devRows.pivot = new Vector2(0.5f, 1);
            _devRows.offsetMin = Vector2.zero; _devRows.offsetMax = Vector2.zero;
            var layout = _devRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            // TRUE, unlike the guide's: its rows carry a photo and size themselves, these are
            // single lines of 8px type that must be told their height. With it false the
            // LayoutElement is ignored and every row took a Text's default rect — measured at
            // a hundred pixels a line, four rows to a screen for a table meant to be read in
            // one pass.
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var fit = _devRows.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = _devRows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.inertia = false;

            NewButton(_devPanel, "KAPAT", new Vector2(0, 0), new Vector2(300, 32),
                new Vector2(20, 12), UITheme.Cyan[3], () => ToggleDevBench());
            _devPanel.gameObject.SetActive(false);
        }

        /// <summary>The rail's pitch: one slot per heading, two per key (the key and its
        /// note). Roomier than the pixel bench's 26, because the plain face is taller.</summary>
        private const float DevSlotH = 30f;

        private void DevHeading(ref int slot, string text)
        {
            var t = NewText("DH", _devPanel, Plain, 13, TextAnchor.LowerLeft, UITheme.Cyan[3]);
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(DevRailW, 20),
                new Vector2(DevRailX, -56f - slot * DevSlotH));
            t.rectTransform.pivot = new Vector2(0, 1);
            t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = text;
            slot++;
        }

        /// <summary>One line of the lineup, four columns wide: price, name, how it is
        /// made, what it asks for. Each column is its own rect, so the table lines up
        /// whatever the words are and whatever face they are set in.</summary>
        private void DevColumns(RectTransform row, string price, string name, string how, string asks,
            Color ink, bool header = false)
        {
            float[] w = { 70f, 250f, 190f, 310f };   // the asks are the longest column
            string[] cols = { price, name, how, asks };
            float x = 8f;
            for (int i = 0; i < cols.Length; i++)
            {
                var t = NewText("C" + i, row, Plain, header ? 12 : 13, TextAnchor.MiddleLeft, ink);
                Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(w[i], 18f), new Vector2(x, 0));
                t.rectTransform.pivot = new Vector2(0, 0.5f);
                if (header) t.fontStyle = FontStyle.Bold;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.text = cols[i];
                x += w[i];
            }
        }

        /// <summary>How a recipe is made, in the bench's own language.</summary>
        private static string DevPrep(PrepMethod prep)
        {
            switch (prep)
            {
                case PrepMethod.Shaken: return "ÇALKALANIR";
                case PrepMethod.Stirred: return "KARIŞTIRILIR";
                case PrepMethod.Built: return "BARDAKTA";
                default: return prep.ToString().ToUpperInvariant();
            }
        }

        /// <summary>One verb: its NAME on the key, and what it does underneath it rather than
        /// crammed inside it. That is the whole reason this panel exists.</summary>
        private void DevKey(ref int slot, string name, string what, Action onClick)
        {
            var row = NewRect("DK_" + name, _devPanel);
            Place(row, new Vector2(0, 1), new Vector2(DevRailW, 28),
                new Vector2(DevRailX, -56f - slot * DevSlotH));
            row.pivot = new Vector2(0, 1);
            var btn = row.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var face = NewRect("Face", row);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(row, UITheme.Night[3], btn, face);
            var label = NewText("L", face, Plain, 14, TextAnchor.MiddleLeft, UITheme.TextPrimary);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10, KeyPlate.Throw), new Vector2(-8, 0));
            label.fontStyle = FontStyle.Bold;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = name;
            slot++;

            var note = NewText("N_" + name, _devPanel, Plain, 12, TextAnchor.UpperLeft, UITheme.Cream[2]);
            Place(note.rectTransform, new Vector2(0, 1), new Vector2(DevRailW - 8f, 18),
                new Vector2(DevRailX + 10f, -56f - slot * DevSlotH + 10f));
            note.rectTransform.pivot = new Vector2(0, 1);
            note.horizontalOverflow = HorizontalWrapMode.Overflow;
            note.text = what;
            slot++;
        }

        /// <summary>
        /// THE LINEUP, RUNG BY RUNG, read off the live run.
        ///
        /// `LastCall → Write Balance Guide` already writes the numbers a spreadsheet wants;
        /// this is the half a document cannot hold — what THIS bar owns tonight, what its
        /// standing has already opened, and what the next rung is still holding. Both halves
        /// exist because they answer different questions: the file says how the game is
        /// priced, this says where this run has got to.
        /// </summary>
        private void RefreshDevBench()
        {
            if (_devRows == null) return;
            for (int i = _devRows.childCount - 1; i >= 0; i--)
                Destroy(_devRows.GetChild(i).gameObject);
            var run = Run;
            if (run == null) { _devStanding.text = "no run"; return; }

            double stars = run.Rating.Average;
            _devStanding.text = $"puan {stars:0.00}★ · gün {run.Day} · ${run.Money} · "
                              + $"menüde {run.MenuRecipes.Count} sayfa · "
                              + $"duvarda {run.Shelf.Bottles.Count} şişe";

            // Every page in the book and every bottle in the catalogue, filed under the rung
            // that opens it. The bottle's rung is its own lock's answer, so the table cannot
            // disagree with the shop — both ask the same object.
            var rungs = new SortedDictionary<double, List<(string price, string name, string how, string asks, Color ink)>>();
            void File(double rung, string price, string name, string how, string asks, Color ink)
            {
                if (!rungs.TryGetValue(rung, out var list))
                    rungs[rung] = list = new List<(string, string, string, string, Color)>();
                list.Add((price, name, how, asks, ink));
            }

            foreach (var r in run.AllRecipes)
            {
                double gate = run.RecipeStarGate(r);
                bool owned = false;
                foreach (var m in run.MenuRecipes) if (m.Id == r.Id) { owned = true; break; }
                var bands = new StringBuilder();
                foreach (var b in r.RatioRequirements)
                {
                    if (bands.Length > 0) bands.Append(", ");
                    bands.Append(b.IsStyleBand ? b.Style : b.Type.ToString());
                    bands.Append($" {b.MinRatio:P0}-{b.MaxRatio:P0}");
                    if (b.MinTier > 1) bands.Append($" T{b.MinTier}+");
                }
                string how = DevPrep(r.Prep)
                           + (string.IsNullOrEmpty(r.GlassId) ? "" : " · " + r.GlassId);
                File(gate, "$" + run.RecipePrice(r), r.Name, how, bands.ToString(),
                    owned ? UITheme.Lime[3] : run.Money >= run.RecipePrice(r) && stars + 1e-9 >= gate
                        ? UITheme.TextPrimary : UITheme.Cream[2]);
            }

            foreach (var card in run.CatalogueBottles)
            {
                if (card.Info == null) continue;
                double rung = card.Info.Unlock != null
                    ? card.Info.Unlock.StarsWanted
                    : Market.RequiredStars(card.Info.Tier, card.Info.Price);
                if (double.IsNaN(rung)) rung = 0.0;   // a bottle earned from a person: file it at the top
                bool owned = run.Shelf.Find(card.Id) != null;
                File(rung, "$" + card.Info.Price, card.Name,
                    card.Info.Category + " · seviye " + card.Info.Tier,
                    owned ? "DUVARDA" : "stokta",
                    owned ? UITheme.Lime[3] : UITheme.Cream[2]);
            }

            foreach (var rung in rungs)
            {
                bool reached = stars + 1e-9 >= rung.Key;
                // A SEALED RUNG IS THE MOST INTERESTING ROW ON THE TABLE and it was drawn in
                // the beam's own shade on a black panel — invisible, so the reader saw a gap
                // between two blocks and no reason for it. Dimmer than an open rung, never
                // dimmer than the rows under it.
                var header = NewText("Rung", _devRows, Plain, 14, TextAnchor.MiddleLeft,
                    reached ? UITheme.PrimaryAction : UITheme.Cyan[3]);
                var hr = header.rectTransform;
                hr.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
                header.fontStyle = FontStyle.Bold;
                header.horizontalOverflow = HorizontalWrapMode.Overflow;
                header.text = $"  ★ {rung.Key:0.0}   {rung.Value.Count} SATIR"
                            + (reached ? "   — AÇIK" : "   — KİLİTLİ");

                foreach (var (price, name, how, asks, ink) in rung.Value)
                {
                    var row = NewRect("L", _devRows);
                    row.gameObject.AddComponent<LayoutElement>().preferredHeight = 19f;
                    DevColumns(row, price, name, how, asks, ink);
                }
            }
        }

        /// <summary>The last-call jump, lifted out of the settings stack unchanged.</summary>
        private void DevJumpToLastCall()
        {
            if (Run == null || Run.Phase != TycoonPhase.DayOpen) { Toast("NOT MID-DAY"); return; }
            if (Run.Story == null) { Toast("THIS RUN HAS NO STORY"); return; }
            if (Run.LastCustomer != null) { Toast("THEY ARE ALREADY AT THE BAR"); return; }
            Run.DevForceLastCall = true;   // the scene does not schedule it any more (2026-09-06)
            _flow?.CloseFlow();
            CloseId();

            // THE DAY JUMPS TOO (2026-08-14): looking at a beat two weeks out used to be two
            // weeks of pressing things. `DevJumpToNight` winds the calendar; what that skips,
            // and why it skips it rather than playing the nights for real, is written there.
            int skipped = Run.DevJumpToNight(Run.Story.DueDay);
            if (skipped > 0) ApplyBarLook();
            if (!Run.Story.IsDueOn(Run.Day))
            {
                ToggleDevBench();
                Toast("NOTHING WRITTEN AHEAD — LAST WAS "
                      + BarCalendar.Label(Run.Story.DueDay).ToUpperInvariant());
                return;
            }
            // The REAL clock and the REAL verb, the same bargain DevSkipToDayEnd strikes:
            // everyone still seated storms off exactly as they would have, the rent and the
            // rating land where they always do. What is skipped is the waiting, never the
            // rules — a shortcut that lied would measure a game nobody plays.
            for (int guard = 0; guard < 20000 && Run.LastCustomer == null
                 && Run.Phase == TycoonPhase.DayOpen; guard++)
                Run.Tick(0.25);
            ToggleDevBench();
            Toast(Run.LastCustomer != null
                ? "LAST CALL — " + Run.LastCallBeat.Who.Name.ToUpperInvariant() + " IS AT THE BAR"
                : "THE NIGHT ENDED WITHOUT THEM");
        }

        private void ToggleGuide()
        {
            if (_guidePanel == null) return;
            bool show = !_guidePanel.gameObject.activeSelf;
            if (show) { CloseId(); RefreshGuide(); }
            _guidePanel.gameObject.SetActive(show);
        }

        private void BuildGuide(RectTransform root)
        {
            _guidePanel = NewRect("Guide", root);
            Place(_guidePanel, new Vector2(0.5f, 0.5f), new Vector2(940, 620), new Vector2(0, 10));
            var canvas = _guidePanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 24;                 // above the market, which is 22
            _guidePanel.gameObject.AddComponent<ForgivingRaycaster>();
            var bg = _guidePanel.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.985f);
            bg.raycastTarget = true;
            Frame(_guidePanel, 2f, UITheme.PrimaryAction);

            var title = NewText("T", _guidePanel, _display, 16, TextAnchor.MiddleLeft,
                UITheme.PrimaryAction);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(600, 22), new Vector2(20, -18));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = "THE ROOM — WHO DRINKS HERE";

            var note = NewText("N", _guidePanel, _body, 8, TextAnchor.MiddleRight, UITheme.Cream[2]);
            Place(note.rectTransform, new Vector2(1, 1), new Vector2(420, 12), new Vector2(-20, -22));
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.verticalOverflow = VerticalWrapMode.Truncate;
            note.text = "Stars = how many you need before they walk in";

            // Column heads, so the rows underneath do not need repeating labels.
            var head = NewText("H", _guidePanel, _shop, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(head.rectTransform, new Vector2(0, 1), new Vector2(880, 12), new Vector2(96, -46));
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.text = "NAME                          AGE   CITIZEN OF              ARCHETYPE            FROM";

            var view = NewRect("GuideView", _guidePanel);
            Stretch(view, Vector2.zero, Vector2.one, new Vector2(14, 52), new Vector2(-14, -58));
            view.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.02f);
            view.gameObject.AddComponent<RectMask2D>();
            _guideRows = NewRect("Rows", view);
            _guideRows.anchorMin = new Vector2(0, 1); _guideRows.anchorMax = Vector2.one;
            _guideRows.pivot = new Vector2(0.5f, 1);
            _guideRows.offsetMin = Vector2.zero; _guideRows.offsetMax = Vector2.zero;
            var layout = _guideRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childControlHeight = false; layout.childForceExpandHeight = false;
            var fit = _guideRows.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = _guideRows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.inertia = false;

            NewButton(_guidePanel, "CLOSE", new Vector2(0.5f, 0), new Vector2(180, 32),
                new Vector2(0, 12), UITheme.PrimaryAction, () => ToggleGuide());
            _guidePanel.gameObject.SetActive(false);
        }

        private void RefreshGuide()
        {
            if (_guideRows == null) return;
            for (int i = _guideRows.childCount - 1; i >= 0; i--)
                Destroy(_guideRows.GetChild(i).gameObject);

            float standing = Run != null ? (float)Run.Rating.Average : 0f;
            // IN THE ORDER THEY ARRIVE (the author, 2026-08-10). The roster's job is
            // "who drinks here and when", and read down the star gate it answers that in
            // one pass: everyone already in the room at the top, then the next person the
            // bar has to earn, and so on. In generation order it answered nothing — the
            // gates ran 0, 0, 1.5, 0, 2.5, 1.5 and the reader had to sort it by eye.
            // List.Sort is not stable, so the generation index is the tiebreaker rather
            // than a hope: a batch of characters gated alike still reads as the batch it
            // was drawn in.
            var roster = new List<PatronLook>(_looks);
            var drawnOrder = new Dictionary<PatronLook, int>();
            for (int i = 0; i < _looks.Count; i++) drawnOrder[_looks[i]] = i;
            roster.Sort((a, b) =>
            {
                int byGate = a.Stars.CompareTo(b.Stars);
                return byGate != 0 ? byGate : drawnOrder[a].CompareTo(drawnOrder[b]);
            });
            foreach (var look in roster)
            {
                var papers = PapersFor(look);
                var row = NewRect("R", _guideRows);
                row.sizeDelta = new Vector2(0, 62);
                var rowBg = row.gameObject.AddComponent<Image>();
                bool here = look.Stars <= standing + 0.001f;
                rowBg.color = here ? new Color(1f, 1f, 1f, 0.045f) : new Color(1f, 1f, 1f, 0.015f);

                if (look.Face != null)
                {
                    var photo = NewRect("P", row);
                    Place(photo, new Vector2(0, 0.5f), new Vector2(54, 54), new Vector2(8, 0));
                    var pi = photo.gameObject.AddComponent<Image>();
                    pi.sprite = look.Face; pi.preserveAspect = true; pi.raycastTarget = false;
                    // Somebody who will not come in yet is shown, but dimmed — the guide is
                    // a roster, not a spoiler, and the point is seeing WHO is still to come.
                    pi.color = here ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
                }

                var line = NewText("L", row, _body, 8, TextAnchor.MiddleLeft,
                    here ? UITheme.Cream[4] : UITheme.Cream[2]);
                Place(line.rectTransform, new Vector2(0, 1), new Vector2(700, 14), new Vector2(72, -10));
                line.horizontalOverflow = HorizontalWrapMode.Overflow;
                line.text = papers != null
                    ? papers.Name.PadRight(30) + (papers.Age + "").PadRight(6) + papers.Country
                    : (look.Slug ?? "patron");

                var sub = NewText("S", row, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
                Place(sub.rectTransform, new Vector2(0, 1), new Vector2(700, 12), new Vector2(72, -28));
                sub.horizontalOverflow = HorizontalWrapMode.Overflow;
                sub.text = (look.Slug ?? "patron") + "   ·   6 clips   ·   head row " + (int)look.HeadY;

                var gate = NewText("G", row, _shop, 8, TextAnchor.MiddleRight,
                    here ? new Color(0.42f, 0.84f, 0.51f, 1f) : UITheme.PrimaryAction);
                Place(gate.rectTransform, new Vector2(1, 1), new Vector2(220, 14), new Vector2(-10, -10));
                gate.horizontalOverflow = HorizontalWrapMode.Overflow;
                gate.text = look.Stars <= 0f ? "OPENS THE DOORS"
                    : (here ? "IN THE ROOM · " : "WAITS FOR ") + look.Stars.ToString("0.0") + "*";

                if (papers != null)
                {
                    var flag = NewRect("F", row);
                    Place(flag, new Vector2(1, 1), new Vector2(16, 11), new Vector2(-10, -30));
                    var fi = flag.gameObject.AddComponent<Image>();
                    fi.sprite = ItemArt.Load("fl_" + papers.Iso);
                    fi.raycastTarget = false;
                    // A citizenship with no flag drawn shows nothing rather than a white box.
                    fi.enabled = fi.sprite != null;
                }
            }
        }

        private void RefreshSettings()
        {
            if (_settingsVolume == null) return;
            _settingsVolume.text = Mathf.RoundToInt(Sound.Volume * 100) + "%";
            if (_settingsMeter != null)
                for (int i = 0; i < _settingsMeter.Length; i++)
                    if (_settingsMeter[i] != null)
                        _settingsMeter[i].color = !Sound.Muted && Sound.Volume + 1e-3f >= (i + 1) / 5f
                            ? UITheme.Amber[4] : UITheme.Night[3];
            _settingsMute.text = Sound.Muted ? "OFF" : "ON";
            _settingsMotion.text = Motion.Reduced ? "REDUCED" : "FULL";
        }

        /// <summary>Leader dots so the bill columns line up in the monospace pixel font.</summary>
        private static string Dots(int n) => "<color=#9C8F80>" + new string('.', n) + "</color>";

        /// <summary>One receipt line, right-aligned to the slip's width.</summary>
        private static string Line(string label, string amount, string hex)
        {
            int gap = Math.Max(1, Columns - label.Length - amount.Length);
            string body = label + Dots(gap) + amount;
            return hex == null ? body : $"<color=#{hex}>{body}</color>";
        }

        // The week (the author's calendar): six open days, Monday through Saturday —
        // SUNDAY the bar is dark (BarCalendar.OpenNights; this comment said the opposite
        // week for a while, which is exactly the drift the next line exists to prevent).
        // It lives in Core now
        // (2026-08-13): the story schedules its guests by the weekend, so the week became a
        // RULE, and a calendar the HUD kept to itself would be a second one, free to disagree
        // with the game about what day it is. The words are unchanged.
        private static string CalendarFor(int day) => BarCalendar.Label(day);

        /// <summary>
        /// Lights the marquee from the run: which week it is, which night is being played,
        /// which nights the arc is due on, and the one the bar does not open at all.
        /// </summary>
        private void RefreshWeekStrip(TycoonRun run)
        {
            if (_weekCells.Count == 0) return;
            int week = BarCalendar.WeekOf(run.Day);
            if (week != _weekShown)
            {
                _weekShown = week;
                // Just the count: the word WEEK is the instrument's own printed caption now.
                _weekLabel.text = $"{week:00}";
            }
            // NOTHING IS HANDING OVER UP HERE. The beam shows one night, fully lit, so it
            // passes no `leaving` and a full `over` - the crossfade is the DAY CARD's, and
            // the instrument is the same instrument either way (see LightWeekCells).
            LightWeekCells(_weekCells, _vipCell, (int)BarCalendar.NightOf(run.Day), -1, 1f,
                StoryNightOf(run, week));
        }

        /// <summary>Which slot in THIS week the arc is due on, or -1. A beat due in a later
        /// week leaves the calendar clean: it shows the week it is showing.</summary>
        private static int StoryNightOf(TycoonRun run, int week)
        {
            var due = run.Story?.Current;
            int dueDay = run.Story != null ? run.Story.DueDay : 0;
            if (due == null || BarCalendar.WeekOf(dueDay) != week) return -1;
            int i = (int)BarCalendar.NightOf(dueDay);
            return i < BarCalendar.OpenNights ? i : -1;
        }

        /// <summary>
        /// Lights one week instrument, wherever it is mounted.
        ///
        /// THE WORD SAYS THE STATE, THE SIGN UNDER IT SAYS WHAT THE NIGHT IS. A tube burns
        /// only under the night being played; the star fitting is always Saturday's and only
        /// how hard it burns changes; the shutter is Sunday's and never changes at all.
        ///
        /// ONE NUMBER CARRIES BOTH MOUNTS. The beam only ever has one night lit and passes
        /// <paramref name="over"/> = 1 with no <paramref name="leaving"/>; the day card is a
        /// HAND-OVER, so the night that closed goes out on exactly the curve the night
        /// arriving comes up on. Everything else - which nights are spent, which is the
        /// story's, which is dark - is the same arithmetic on both, which is the point of
        /// there being one of these rather than two.
        /// </summary>
        private static void LightWeekCells(List<(Image sign, Image bloom, Text name)> cells,
            int vipCell, int tonight, int leaving, float over, int storyNight)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                var (sign, bloom, name) = cells[i];
                bool closed = i >= BarCalendar.OpenNights;          // the seventh night
                float burn = closed ? 0f
                    : i == tonight ? over
                    : i == leaving ? 1f - over
                    : 0f;
                bool story = !closed && i == storyNight;
                bool worked = !closed && i < tonight;

                if (sign != null)
                {
                    if (i == vipCell)
                    {
                        // Legible from across the week even four days out, and BRIGHTEST on
                        // the night itself. The star keeps its own gold (2026-09-04, the
                        // author's one-icon rule): magenta over it came out a muddy red, and
                        // the marquee already says which night is the story's in the word
                        // above it. Only how hard it burns is ours to set.
                        sign.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.62f, 1f, burn));
                    }
                    else
                    {
                        // The tube burns the WORD's own hue: a story night that is also
                        // tonight reads magenta up top, and an amber tube under magenta
                        // letters would be the strip disagreeing with itself.
                        sign.enabled = burn > 0.01f;
                        var t = story ? UITheme.Magenta[4] : UITheme.Amber[4];
                        sign.color = new Color(t.r, t.g, t.b, t.a * burn);
                    }
                }
                if (bloom != null)
                {
                    bloom.enabled = burn > 0.01f;
                    var b = story ? UITheme.Magenta[2] : UITheme.Amber[2];
                    bloom.color = new Color(b.r, b.g, b.b, b.a * 0.5f * burn);
                }

                // BRIGHT ENOUGH TO BE A CALENDAR (the author: "ufak ve sonuk kaliyor",
                // then "yazilar okumuyor"): nights ahead are cream, one step up from the
                // first cut; the nights already worked are the dim ones, because a night
                // that is spent is the only one on the glass with nothing left to say.
                var rest = closed ? UITheme.Night[4]
                    : story ? UITheme.Magenta[4]
                    : worked ? UITheme.Night[4]
                    : UITheme.Cream[3];
                var awake = story ? UITheme.Magenta[4] : UITheme.Amber[4];
                name.color = burn > 0.001f ? Color.Lerp(rest, awake, burn) : rest;
            }
        }

        /// <summary>
        /// Drives the two surfaces off the run's own state (GDD 26 §4). It owns no timing and
        /// no rules: Core decides whether a trial is talking, pouring or over, and this reads
        /// that once a frame. The one thing it DOES own is the script — which line of the
        /// beat is being said, and by whom.
        /// </summary>
        private void SyncLastCall(TycoonRun run)
        {
            var beat = run.LastCallBeat;
            var trial = run.Trial;
            // Nothing of this survives the night's end: the slip is the next thing the player
            // reads, and a plate at layer 7 would sit on top of it.
            if (run.Phase != TycoonPhase.DayOpen) { beat = null; trial = null; }

            // THE ROOM SAYS IT TOO (GDD 26 §7, S4). The ceiling comes down, the neon over the
            // door burns harder and one lamp finds whoever is at the bar — driven off the
            // guest's own stool, so the light lands on the person and not on a guessed spot.
            if (stage != null)
            {
                var lit = run.LastCustomer;
                float x = 0f;
                if (lit != null)
                    foreach (var s in _seats) if (s.Visit == lit) { x = s.Root.anchoredPosition.x; break; }
                stage.SetClosingBeat(lit != null, x);
            }
            // A WITHHELD NIGHT HAS NO TRIAL AND IS STILL A SCENE (GDD 26 §12). The guest came,
            // the bar has not reached their rung, and they are on the stool saying so — which
            // is the one thing this panel exists for. Keying off `trial` alone hid the plate
            // and left somebody sitting in a dimmed room in silence.
            bool withheld = beat != null && run.LastCallWithheld && run.LastCustomer != null;
            if (beat == null || (trial == null && !withheld))
            {
                // THE HOST'S LESSONS RIDE THE SAME PLATE (GDD 26 §1b, PLAN_last_call S5):
                // when no beat is being played, whatever she has to say about the night
                // goes here — her face, her name, one line a key. Only on an open night;
                // the closing's lessons are said on the market (SyncHostNote).
                // Never over the open cellar: the plate sits exactly where the bottles are
                // picked up, so while the drawer is out the lesson waits, queued.
                var lesson = run.Phase == TycoonPhase.DayOpen && !CellarOpen ? run.LessonDue : null;
                if (lesson != null && _plate != null) { SyncLesson(run, lesson); return; }
                if (_plate != null && _plate.gameObject.activeSelf) _plate.gameObject.SetActive(false);
                if (_postIt != null && _postIt.gameObject.activeSelf) _postIt.gameObject.SetActive(false);
                if (_gateRow != null && _gateRow.gameObject.activeSelf) _gateRow.gameObject.SetActive(false);
                _plateStage = "";
                return;
            }

            // The script is rebuilt when the night moves to a new part of itself, and only
            // then: a plate that re-cued every frame would never get past its first line.
            string part = withheld ? "short"
                : trial.State == TrialState.Talking ? "ask"
                : trial.State == TrialState.Pouring ? "pour"
                : trial.State == TrialState.Passed ? "kept" : "missed";
            if (part != _plateStage)
            {
                _plateStage = part;
                _plateAt = 0;
                _plateScript.Clear();
                var host = _bootstrap?.Story?.Cast?.FirstOrDefault(c => c.IsHost);
                if (part == "ask")
                {
                    foreach (var line in beat.Lines.HostBefore) Add(host, line);
                    foreach (var line in beat.Lines.Ask) Add(beat.Who, line);
                }
                else if (part == "short")
                {
                    // The guest explains, and the house has the last word — which is where the
                    // system gets taught, because the host is the one who can say what a star
                    // is and how you get another one.
                    foreach (var line in beat.Lines.HostBefore) Add(host, line);
                    foreach (var line in beat.Lines.ShortOfGate) Add(beat.Who, line);
                    foreach (var line in beat.Lines.HostAfter) Add(host, line);
                }
                else if (part == "kept" || part == "missed")
                {
                    // THREE WAYS TO MISS, THREE THINGS TO SAY (GDD 26 §5): a wrong drink is
                    // answered by the wrong-drink line, an honest no by the declined line,
                    // and a clock that simply ran out by the nudge — because the beat never
                    // wrote a line for being ignored, and the nudge is what it has.
                    var said = part == "kept" ? beat.Lines.ServedRight
                        : trial.ToldNo ? beat.Lines.Declined
                        : trial.Mistakes > 0 ? beat.Lines.ServedWrong
                        : beat.Lines.Nudge;
                    foreach (var line in said) Add(beat.Who, line);
                    foreach (var line in beat.Lines.HostAfter) Add(host, line);
                }
            }

            bool talking = _plateScript.Count > 0 && _plateAt < _plateScript.Count;
            if (talking != _plate.gameObject.activeSelf) _plate.gameObject.SetActive(talking);
            if (talking)
            {
                var (who, look, line) = _plateScript[_plateAt];
                _plateName.text = who.ToUpperInvariant();
                _plateLine.text = line;
                var face = LookNamed(look);
                _plateFace.sprite = face?.Face;
                _plateFace.enabled = _plateFace.sprite != null;
                bool last = _plateAt == _plateScript.Count - 1;
                _plateKeyLabel.text = part == "ask" && last ? "POUR IT"
                    : part == "short" && last ? "GOOD NIGHT" : "GO ON";
                // Nothing to decline on a night nothing was asked for.
                _plateNoKey.gameObject.SetActive(part == "ask");
            }

            // THE RUNG STAYS UP FOR THE WHOLE SCENE, under whoever is speaking: the guest
            // saying they will be back and the host explaining why are both about one number,
            // and it is drawn once rather than repeated in two lines of prose.
            bool showGate = withheld && talking;
            if (showGate != _gateRow.gameObject.activeSelf) _gateRow.gameObject.SetActive(showGate);
            if (showGate)
            {
                double need = beat.RequiresStars, now = run.Rating.Average;
                _gateFill.sizeDelta = new Vector2((float)(need / BarRating.MaxStars)
                                                  * BarRating.MaxStars * 18f, 0);
                _gateText.text = $"COMES BACK AT {need:0.0} STARS  ·  YOU HAVE {now:0.0}";
            }

            bool working = trial != null && trial.State == TrialState.Pouring;
            if (working != _postIt.gameObject.activeSelf) _postIt.gameObject.SetActive(working);
            if (working)
            {
                var ask = trial.Current;
                _postWho.text = beat.Who.Name.ToUpperInvariant();
                _postAsk.text = ask != null ? ask.Name.ToUpperInvariant() : "";
                _postCount.text = $"{trial.Done + 1} OF {trial.Total}"
                                  + (trial.Trial.AllowedMistakes > 0
                                      ? $"  ·  {Math.Max(0, trial.Trial.AllowedMistakes - trial.Mistakes)} SPARE"
                                      : "  ·  NO MISTAKES");
                var guest = run.LastCustomer;
                _postClock.fillAmount = guest == null || guest.PatienceMax <= 0
                    ? 0f : Mathf.Clamp01((float)(guest.PatienceLeft / guest.PatienceMax));
                _postClock.color = _postClock.fillAmount < 0.25f ? UITheme.ViceRed[3] : UITheme.Magenta[4];
                var lacking = ask != null ? MissingStyles(ask) : null;
                _postMissing.text = lacking != null && lacking.Count > 0
                    ? "NO " + string.Join(", ", lacking).ToUpperInvariant() + " ON THE SHELF" : "";
            }

            void Add(StoryCharacter speaker, string line)
            {
                if (speaker == null || string.IsNullOrEmpty(line)) return;
                _plateScript.Add((speaker.Name, LookForStory(speaker)?.Slug, line));
            }
        }

        /// <summary>
        /// The host's lesson on the plate (GDD 26 §1b "the teacher"): the tutorial this
        /// project deleted comes back as a person saying the thing once. Her lines are
        /// worked through with the same key the beat uses; the last one reads GOT IT and
        /// tells Core it was heard, so the next lesson (if a second moment came on the same
        /// tick) follows on. No SAY NO: there is nothing to decline.
        /// </summary>
        private void SyncLesson(TycoonRun run, StoryLesson lesson)
        {
            string part = "lesson:" + lesson.Id;
            if (part != _plateStage)
            {
                _plateStage = part;
                _plateAt = 0;
                _plateScript.Clear();
                var host = _bootstrap?.Story?.Cast?.FirstOrDefault(c => c.IsHost);
                if (host != null)
                    foreach (var line in lesson.Say)
                        if (!string.IsNullOrEmpty(line))
                            _plateScript.Add((host.Name, LookForStory(host)?.Slug, line));
                // A story with no host has nobody to say it: the moment is spent silently
                // rather than leaving a plate up with no one on it.
                if (_plateScript.Count == 0) { run.HeardLesson(); _plateStage = ""; return; }
            }
            bool talking = _plateAt < _plateScript.Count;
            if (talking != _plate.gameObject.activeSelf) _plate.gameObject.SetActive(talking);
            if (talking)
            {
                var (who, look, line) = _plateScript[_plateAt];
                _plateName.text = who.ToUpperInvariant();
                _plateLine.text = line;
                var face = LookNamed(look);
                _plateFace.sprite = face?.Face;
                _plateFace.enabled = _plateFace.sprite != null;
                _plateKeyLabel.text = _plateAt == _plateScript.Count - 1 ? "GOT IT" : "GO ON";
                if (_plateNoKey.gameObject.activeSelf) _plateNoKey.gameObject.SetActive(false);
            }
            if (_gateRow.gameObject.activeSelf) _gateRow.gameObject.SetActive(false);
            if (_postIt.gameObject.activeSelf) _postIt.gameObject.SetActive(false);
        }

        /// <summary>The listen key: one line at a time, and the last one starts the clock.</summary>
        private void OnPlateKey()
        {
            var run = Run;
            // A lesson is worked through with the same key; its last line is GOT IT, and
            // Core is told so the next one can follow (GDD 26 §1b).
            if (run != null && _plateStage.StartsWith("lesson:", StringComparison.Ordinal))
            {
                if (_plateAt < _plateScript.Count - 1) { _plateAt++; return; }
                _plateAt = _plateScript.Count;
                _plateStage = "";
                run.HeardLesson();
                return;
            }
            // A WITHHELD NIGHT HAS NO TRIAL and the key must still turn the page (GDD 26 §12);
            // guarding on `Trial` alone left the guest's own scene unadvanceable, with the
            // only way out being the clock.
            if (run == null || (run.Trial == null && !run.LastCallWithheld)) return;
            if (_plateAt < _plateScript.Count - 1) { _plateAt++; return; }
            _plateAt = _plateScript.Count;      // the script is spoken
            if (run.Trial != null && run.Trial.State == TrialState.Talking) run.BeginLastCallTrial();
        }

        /// <summary>The honest no. It costs the night and never the arc (GDD 26 §5).</summary>
        private void OnSayNoTonight()
        {
            var run = Run;
            if (run == null || run.LastCustomer == null) return;
            run.DeclineLastCall();
            Toast("YOU TOLD THEM NO — THEY WILL BE BACK");
        }

        /// <summary>The register's book of past days (GDD 24 §7, 2026-07-22): a scrollable
        /// list of every closed day — income, expenses, net, and the room's mood.</summary>
        private void BuildLedgerPanel(RectTransform root)
        {
            _ledgerPanel = NewRect("Ledger", root);
            Place(_ledgerPanel, new Vector2(0.5f, 0.5f), new Vector2(560, 560), new Vector2(0, 10));
            var panelImg = _ledgerPanel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(UITheme.Night[1].r, UITheme.Night[1].g, UITheme.Night[1].b, 0.98f);
            // Catch clicks so the world behind the book stays untouched.
            panelImg.raycastTarget = true;

            var title = NewText("Title", _ledgerPanel, _display, 16, TextAnchor.MiddleCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -44), new Vector2(0, -10));
            title.text = "THE REGISTER — DAYS SO FAR";

            // Column header, then the rows on cream stock beneath it. The header names the
            // TOP line of each entry; every entry now carries a second and third line under
            // it, which no fixed column head could describe.
            var header = NewText("Header", _ledgerPanel, _body, 8, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(header.rectTransform, new Vector2(0, 1), new Vector2(504, 20), new Vector2(28, -52));
            header.text = "DAY        TOOK        PAID OUT         NET        TILL";

            var sheet = NewRect("Sheet", _ledgerPanel);
            Place(sheet, new Vector2(0.5f, 1), new Vector2(508, 424), new Vector2(0, -76));
            sheet.gameObject.AddComponent<Image>().color = UITheme.Cream[4];

            _ledgerRows = NewRect("Rows", sheet);
            Stretch(_ledgerRows, Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12));
            var layout = _ledgerRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperLeft;

            NewButton(_ledgerPanel, "CLOSE", new Vector2(0.5f, 0),
                new Vector2(200, 38), new Vector2(0, 18), UITheme.PrimaryAction, () => ToggleLedger());

            _ledgerPanel.gameObject.SetActive(false);
        }

        /// <summary>Opens or closes the register's ledger; refreshes the rows on open.
        /// The book and the licence never share the screen — opening one closes the other.</summary>
        private void ToggleLedger()
        {
            if (_ledgerPanel == null || Run == null) return;
            bool show = !_ledgerPanel.gameObject.activeSelf;
            if (show) { CloseId(); RefreshLedger(); }
            _ledgerPanel.gameObject.SetActive(show);
        }

        private void RefreshLedger()
        {
            for (int i = _ledgerRows.childCount - 1; i >= 0; i--)
                Destroy(_ledgerRows.GetChild(i).gameObject);

            var history = Run.Ledger.History;
            if (history.Count == 0)
            {
                var empty = NewText("Empty", _ledgerRows, _body, 14, TextAnchor.UpperLeft, UITheme.Night[1]);
                empty.rectTransform.sizeDelta = new Vector2(0, 28);
                empty.text = "No days yet. Close a night first.";
                return;
            }

            // Newest day on top: the last thing you did is the first thing you read.
            //
            // Three lines a night, not one. The top line is the money as it was — what came in,
            // what went out, the net and the till it left behind. Under it the income is split
            // into what was CHARGED and what was TIPPED, and the outgoings into rent, stock and
            // fittings, because "you lost $180" and "the rent was fine, you spent $210 stocking
            // the shelf" are different nights and only the second one can be played differently.
            // The last line is the room: who drank, who left without a drink, and what the night
            // itself was worth in stars before the standing averaged it away.
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var d = history[i];
                bool red = d.Net < 0;
                var head = NewText($"Day{d.Day}", _ledgerRows, _body, 12, TextAnchor.UpperLeft,
                    red ? UITheme.ViceRed[3] : UITheme.Night[1]);
                head.rectTransform.sizeDelta = new Vector2(0, 20);
                head.supportRichText = true;
                string net = red ? $"-${-d.Net}" : $"+${d.Net}";
                string till = d.HasDetail
                    ? (d.TillAfter < 0 ? $"-${-d.TillAfter}" : $"${d.TillAfter}") : "";
                head.text = $"DAY {d.Day,-3}   ${d.Income,-7} ${d.Expenses,-8} {net,-8} {till}";

                if (!d.HasDetail)
                {
                    // A day booked before the book kept its detail. Say so rather than
                    // printing zeroes that would read as a night where nothing happened.
                    var bare = NewText($"Day{d.Day}Bare", _ledgerRows, _body, 8, TextAnchor.UpperLeft,
                        UITheme.Cream[1]);
                    bare.rectTransform.sizeDelta = new Vector2(0, 16);
                    bare.text = $"        {MoodLabel(d.AverageSatisfaction)} night · no breakdown kept";
                    Spacer(12);
                    continue;
                }

                var money = NewText($"Day{d.Day}Money", _ledgerRows, _body, 8, TextAnchor.UpperLeft,
                    UITheme.Cream[1]);
                money.rectTransform.sizeDelta = new Vector2(0, 16);
                money.supportRichText = true;
                var outgoings = new List<string>();
                if (d.Rent > 0) outgoings.Add($"rent ${d.Rent}");
                if (d.Stock > 0) outgoings.Add($"stock ${d.Stock}");
                if (d.Upgrades > 0) outgoings.Add($"fittings ${d.Upgrades}");
                if (d.Fines > 0) outgoings.Add($"fines ${d.Fines}");           // the law (GDD 28 §7)
                money.text = $"        drinks ${d.Sales} · tips ${d.Tips}"
                           + (d.Bonus > 0 ? $" · thanks ${d.Bonus}" : "")
                           + (outgoings.Count > 0 ? "   —   " + string.Join(" · ", outgoings) : "");

                var room = NewText($"Day{d.Day}Room", _ledgerRows, _body, 8, TextAnchor.UpperLeft,
                    d.WalkedOut > 0 ? UITheme.ViceRed[2] : UITheme.Cream[1]);
                room.rectTransform.sizeDelta = new Vector2(0, 16);
                room.supportRichText = true;
                string walked = d.WalkedOut > 0
                    ? $" · {d.WalkedOut} left without one" : " · nobody left thirsty";
                room.text = $"        {d.Served} served{walked}"
                          + (d.RightKicks > 0 ? $" · {d.RightKicks} shown the door" : "")
                          + $" · {d.NightStars:0.0} stars on the night"
                          + $" · {MoodLabel(d.AverageSatisfaction)}";

                Spacer(12);
            }
        }

        /// <summary>A blank row between ledger entries — three lines a night need the air, or
        /// the book reads as one wall of numbers.</summary>
        private void Spacer(float height)
        {
            var gap = NewRect("Gap", _ledgerRows);
            gap.sizeDelta = new Vector2(0, height);
        }

        private static string MoodLabel(double satisfaction) =>
            satisfaction >= DayLedger.HighRollerBar ? "GREAT"
            : satisfaction >= DayLedger.BrokeBar ? "OK"
            : "SOUR";

        // ── the state language, in one place ─────────────────────────────────────
        // Seven answers, seven rows. Keeping them as switches beside each other is what
        // makes "no two states may look alike" checkable rather than hopeful.

        private static Color PlateOf(TileState s) =>
            s == TileState.Unaffordable ? PlateDeny
            : s == TileState.Picked ? PlatePicked
            : s == TileState.Ordered ? PlateOrdered
            : s == TileState.Sealed ? PlateSealed
            : s == TileState.Refundable ? PlateReturn
            : s == TileState.Held ? ShopAisle
            : s == TileState.NoFitting ? PlateDeny
            : ShopPage;

        private static Color MoneyInk(TileState s) =>
            s == TileState.Unaffordable ? StripDeny
            : s == TileState.Picked ? PickedInk
            : s == TileState.Ordered ? ShopVice
            : s == TileState.Refundable ? StripReturn
            : s == TileState.Held || s == TileState.Sealed ? ShopInkSoft
            : ShopViceDeep;

        /// <summary>
        /// Which face a money token is set in. The display face is the one a shopper reads
        /// first, but it is exactly `fontSize` wide per character, so it only fits while the
        /// string is short: the price slot is 66 units, which is four display characters and
        /// no more. A refunded legendary glass is "+$105" — five — and would have walked 14
        /// units onto the pill. Anything that long drops to the body face, where the same
        /// string measures 58.
        /// </summary>
        private Font MoneyFace(string token) => token.Length <= 4 ? _display : _body;
    }
}
