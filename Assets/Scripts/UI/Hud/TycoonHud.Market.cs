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
    // TycoonHud, part Market: the market: the tablet, its listings, the basket and the checkout.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    public sealed partial class TycoonHud
    {
        /// <summary>Whether the bar can actually pour this band right now — the style, at the
        /// rung the recipe asks for, with something left in the bottle.</summary>
        private bool InStock(string style, int minTier)
        {
            var run = Run;
            if (run == null || string.IsNullOrEmpty(style)) return false;
            foreach (var b in run.Shelf.Bottles)
                if (!b.IsEmpty && b.Ingredient.Info != null && b.Ingredient.Info.Style == style
                    && b.Ingredient.Info.Tier >= minTier) return true;
            return false;
        }

        /// <summary>
        /// The open tab's title comes UP to white rather than being swapped to it (the
        /// author, 2026-08-10). One frame of green and the next of white reads as a redraw;
        /// a fifth of a second of travel reads as the tab lighting up. The icons ride the
        /// same curve, so the whole key brightens as one object.
        /// </summary>
        private void FadeShopTabs()
        {
            if (_shopTabLabels == null) return;
            float step = Time.unscaledDeltaTime / TabFade;
            for (int i = 0; i < _shopTabLabels.Length; i++)
            {
                if (_shopTabLabels[i] == null) continue;
                bool on = i == _shopTab;
                var want = on ? Color.white : ShopViceDeep;
                _shopTabLabels[i].color = Color.Lerp(_shopTabLabels[i].color, want, step);
                if (_shopTabIcons[i] != null)
                    _shopTabIcons[i].color = Color.Lerp(_shopTabIcons[i].color,
                        on ? Color.white : new Color(0.494f, 0.529f, 0.635f, 1f), step);
            }
        }

        // ── what a listing IS, in words (the author, 2026-08-07) ─────────────────

        /// <summary>
        /// A bottle, said in the inspector's five rows instead of one shouted blob: what it
        /// is, how good it is, what the bar pours it into, and — for a bottle already behind
        /// the bar — how much is left. Sentence case throughout; the whole market used to be
        /// upper-cased, which is a third of the author's complaint about the descriptions.
        /// </summary>
        private void DescribeBottle(TileSpec spec, IngredientCard card, ShelfBottle onShelf)
        {
            if (card == null) return;
            string style = card.Info?.Style;
            string styleWord = string.IsNullOrEmpty(style) ? "" : style.Replace('_', ' ');
            int tier = card.Info?.Tier ?? 1;

            // THE NAME, ALONE, IN BOLD BESIDE ITS PICTURE (the author). The style used to
            // ride the same row — "SMIRKOFF VODKA · VODKA" — which made the heading a
            // sentence and left the mark next to a fact rather than next to a title. The
            // style is a property of the bottle, so it goes on the property row.
            spec.Identity = card.Name.ToUpperInvariant();
            // The ABV lives HERE, not on the identity line: "GRAND MARINER TRIPLE SEC ·
            // TRIPLE SEC" is already 37 of the 46 characters that fit.
            var meta = new StringBuilder();
            if (styleWord.Length > 0)
                meta.Append(char.ToUpperInvariant(styleWord[0])).Append(styleWord.Substring(1))
                    .Append(" · ");
            meta.Append("Rung ").Append(tier).Append(" of 4");
            if (card.Info != null && card.Info.Abv > 0)
                meta.Append(" · ").Append(card.Info.Abv).Append("% ABV");
            if (onShelf != null && onShelf.Capacity > 0)
                meta.Append(" · ")
                    .Append((int)Math.Round(onShelf.Remaining / onShelf.Capacity * 100))
                    .Append("% left behind the bar");
            spec.MetaLine = meta.ToString();
            // The tile shows the STOCK BAR, so the tile's own meta stays empty here and the
            // two never say the same thing twice.
            if (styleWord.Length > 0 && spec.StockFrac < 0f)
                spec.Meta = char.ToUpperInvariant(styleWord[0]) + styleWord.Substring(1)
                    + (card.Info != null && card.Info.Abv > 0 ? " · " + card.Info.Abv + "%" : "");

            // What it is FOR: the drinks whose bands name this style — and only the drinks
            // the bar can actually pour (Core filters it; see MenuDrinksUsingStyle).
            var uses = new List<string>();
            if (Run != null)
                foreach (var r in Run.MenuDrinksUsingStyle(style))
                {
                    if (!uses.Contains(r.Name)) uses.Add(r.Name);
                    if (uses.Count >= 5) break;
                }
            spec.Body = uses.Count > 0 ? "Poured into: " + string.Join(", ", uses) + "."
                : card.Type == IngredientType.Beer ? "Pulled at the tap."
                : "No drink on the book calls for it yet.";
            spec.BuffA = uses.Count > 0
                ? new Buff(BuffKind.Use, uses.Count + (uses.Count == 1 ? " drink" : " drinks")
                           + " on the menu call for it")
                : new Buff(BuffKind.Bad, "Nothing on tonight's menu calls for it");
            if (tier > 1)
                spec.BuffB = new Buff(BuffKind.Gain,
                    "Joins the shelf; the well bottle stays");
        }

        /// <summary>
        /// Which state a buyable listing is in, and the control that goes with it. One
        /// if/else chain, so the predicates cannot both fire: they genuinely overlap (a
        /// picked fitting is also a fitting the night has no room for), and without an
        /// order the same tile could render two ways.
        /// </summary>
        private void DressBuyable(TileSpec spec, int price, string cartKey, bool isFitting,
            Action buy)
        {
            bool sold = cartKey != null && _justOrdered.Contains(cartKey);
            bool picked = cartKey != null && InCart(cartKey);
            // What the till has left AFTER the order already in the basket — checkout charges
            // the whole basket at once, so this is the only honest reading of "afford".
            bool afford = Run.Money - CartTotal() >= price;
            bool noRoom = isFitting && (!Run.CanFitTonight || CartHasFitting());

            spec.Money = "$" + price;
            if (sold)
            {
                spec.State = TileState.Ordered;
                spec.Money = null; spec.Word = "SOLD";
                return;
            }
            if (picked) { spec.State = TileState.Picked; spec.PillVerb = "TAKE OUT"; }
            else if (noRoom) { spec.State = TileState.NoFitting; spec.PillVerb = "NO SLOT"; }
            else if (!afford) { spec.State = TileState.Unaffordable; spec.PillVerb = "NO CASH"; }
            else { spec.State = TileState.Orderable; spec.PillVerb = "ADD"; }
            string label = spec.Name;
            var art = spec.Art;                 // the basket draws what the tile drew
            spec.OnClick = () => ToggleCart(cartKey, label, price, isFitting, buy, art);
        }

        private bool CartHasFitting()
        {
            foreach (var e in _cart) if (e.IsFitting) return true;
            return false;
        }

        /// <summary>The glass a recipe is served in, by name.</summary>
        private string GlassNameFor(RecipeDefinition r)
        {
            if (Run != null)
                foreach (var g in Run.Glassware)
                    if (g.Id == r.GlassId) return g.Name;
            return "glass";
        }

        /// <summary>The picture for a refund row, resolved from what was bought. The purchase
        /// record carries a kind and an id, which is enough — no Core change needed.</summary>
        private Sprite RefundArt(TycoonRun.DayPurchase pch)
        {
            if (Run == null) return null;
            switch (pch.What)
            {
                case TycoonRun.DayPurchase.Kind.Brand:
                    var bottle = Run.Shelf.Find(pch.Id);
                    return bottle != null ? ItemArt.Bottle(bottle.Ingredient) : null;
                case TycoonRun.DayPurchase.Kind.Recipe:
                    foreach (var r in Run.AllRecipes)
                        if (r.Id == pch.Id) return DrinkIcon.For(r, _bootstrap.Glassware);
                    return null;
                case TycoonRun.DayPurchase.Kind.Glassware:
                    foreach (var g in Run.Glassware)
                        if (g.Id == pch.Id) return GlassArt.For(g, Run.GlassTier(g.Id)).Sprite;
                    return null;
                case TycoonRun.DayPurchase.Kind.Fixture:
                    foreach (var f in Run.FixtureCatalogue)
                        if (f.Id == pch.Id) return FixtureArt(f.Sprite);
                    return null;
                default:
                    return ItemArt.Load("sh_i_upgrades");
            }
        }

        /// <summary>The drinks a glass line actually serves, for its upgrade card. On the
        /// MENU only — a glassware card that listed sealed drinks leaked them exactly as the
        /// bottle card did (2026-08-09).</summary>
        private string DrinksServedIn(string glassId)
        {
            var names = new List<string>();
            if (Run != null)
                foreach (var r in Run.MenuDrinksInGlass(glassId))
                    if (!names.Contains(r.Name))
                    {
                        names.Add(r.Name);
                        if (names.Count >= 4) break;
                    }
            return names.Count == 0 ? "NOTHING ON THE BOOK YET."
                : string.Join(", ", names).ToUpperInvariant() + ".";
        }

        // ── the basket ───────────────────────────────────────────────────────────

        private bool InCart(string key)
        {
            foreach (var e in _cart) if (e.Key == key) return true;
            return false;
        }

        private int CartTotal()
        {
            int n = 0;
            foreach (var e in _cart) n += e.Price;
            return n;
        }

        /// <summary>
        /// Draws the basket: one chip per picked line, each with the product's own art and
        /// its price, each a button that takes it back out (2026-08-11, the author).
        /// </summary>
        private void RebuildBasket()
        {
            if (_cartChips == null) return;
            foreach (Transform child in _cartChips) Destroy(child.gameObject);
            if (_cartEmpty != null) _cartEmpty.gameObject.SetActive(_cart.Count == 0);
            if (_cart.Count == 0) return;

            float rowW = _cartChips.rect.width, rowH = _cartChips.rect.height;
            int fits = Mathf.Max(1, Mathf.FloorToInt((rowW + ChipGap) / (ChipMin + ChipGap)));
            bool over = _cart.Count > fits;
            int shown = over ? fits - 1 : _cart.Count;
            int slots = over ? fits : _cart.Count;
            float box = Mathf.Clamp((rowW + ChipGap) / slots - ChipGap, ChipMin, ChipMax);
            float x = 0f;
            for (int i = 0; i < shown; i++)
            {
                AddCartChip(_cart[i], x, box, rowH);
                x += box + ChipGap;
            }
            if (over) AddMoreChip(_cart.Count - shown, x, box, rowH);
        }

        private void AddCartChip(CartEntry entry, float x, float box, float rowH)
        {
            var chip = ChipPlate("Chip_" + entry.Key, x, box, rowH, PlateOf(TileState.Picked));

            // The product, standing on the chip's own line — measured off the drawing, so a
            // carton saved with air around it is the same size as the bottle beside it. It
            // takes the whole chip above the price now (2026-08-11, the author: the X went,
            // the picture grew into the corner it was using).
            var art = NewRect("Art", chip);
            VesselArt.StandOn(art, new Vector2(0.5f, 0f), entry.Art, rowH - 28f, new Vector2(0, 22f));
            var ai = art.gameObject.AddComponent<Image>();
            ai.sprite = entry.Art;
            ai.preserveAspect = true;
            ai.raycastTarget = false;
            ai.enabled = entry.Art != null;

            // The price in the AISLE's own money face and size (MoneyFace at 16), so a thing
            // in the basket and the same thing on the shelf are priced in one voice.
            string token = "$" + entry.Price;
            var price = NewText("P", chip, MoneyFace(token), 16, TextAnchor.MiddleCenter,
                MoneyInk(TileState.Picked));
            Place(price.rectTransform, new Vector2(0.5f, 0), new Vector2(box - 4f, 20), new Vector2(0, 3));
            price.horizontalOverflow = HorizontalWrapMode.Overflow;
            price.text = token;

            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip.GetComponent<Image>();
            var e = entry;
            button.onClick.AddListener(() => ToggleCart(e.Key, e.Label, e.Price, e.IsFitting, e.Buy, e.Art));

            var sink = chip.gameObject.AddComponent<PressSink>();
            sink.Face = art; sink.Depth = 3f; sink.Lift = 3f; sink.Squash = 0.02f;

            // A chip is a picture and a price; WHICH bottle it is goes on the pointer, in the
            // same card the aisle uses, so the basket needs no small print at all.
            var hover = chip.gameObject.AddComponent<HoverRelay>();
            hover.Entered = () => ShowShopCard(new TileSpec
            {
                Identity = (e.Label ?? "").ToUpperInvariant(),
                MetaLine = "$" + e.Price + "  ·  in the basket",
                Body = "Click to take it back out. You only pay when you place the order.",
                Art = e.Art,
            });
            hover.Exited = () => ShowShopCard(null);
        }

        /// <summary>What the row could not fit, counted rather than dropped.</summary>
        private void AddMoreChip(int more, float x, float box, float rowH)
        {
            var chip = ChipPlate("Chip_more", x, box, rowH, ShopAisle);
            var label = NewText("L", chip, _display, 16, TextAnchor.MiddleCenter, ShopInk);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = "+" + more;
            var hover = chip.gameObject.AddComponent<HoverRelay>();
            int n = more;
            hover.Entered = () => ShowShopCard(new TileSpec
            {
                Identity = "AND " + n + " MORE",
                MetaLine = "the basket holds them all",
                Body = "The row is full, the basket is not. Everything you picked "
                     + "is in the total.",
            });
            hover.Exited = () => ShowShopCard(null);
        }

        private RectTransform ChipPlate(string name, float x, float box, float rowH, Color paper)
        {
            var chip = NewRect(name, _cartChips);
            chip.anchorMin = chip.anchorMax = new Vector2(0, 1);
            chip.pivot = new Vector2(0, 1);
            chip.sizeDelta = new Vector2(box, rowH);
            chip.anchoredPosition = new Vector2(x, 0);
            var plate = chip.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.Card();
            plate.type = Image.Type.Sliced;
            plate.color = paper;
            return chip;
        }

        /// <summary>Notes where the aisle is standing, so a rebuild can put it back.</summary>
        private void RememberScroll()
        {
            if (_shopScroll != null) _shopScrollAt = _shopScroll.verticalNormalizedPosition;
        }

        /// <summary>Picks a listing up, or puts it back. Refuses what the night cannot
        /// carry: a second fitting, or more money than the till holds.</summary>
        private void ToggleCart(string key, string label, int price, bool isFitting, Action buy,
            Sprite art = null)
        {
            RememberScroll();
            for (int i = 0; i < _cart.Count; i++)
                if (_cart[i].Key == key)
                {
                    _cart.RemoveAt(i);
                    Sfx.Play("click", 0.5f);
                    RebuildDayEnd();
                    return;
                }

            if (isFitting)
            {
                if (!Run.CanFitTonight) { Toast("ONE UPGRADE A NIGHT"); return; }
                foreach (var e in _cart)
                    if (e.IsFitting) { Toast("ONE UPGRADE A NIGHT"); return; }
            }
            if (CartTotal() + price > Run.Money) { Toast("NOT ENOUGH MONEY"); return; }

            _cart.Add(new CartEntry { Key = key, Label = label, Price = price,
                                      IsFitting = isFitting, Buy = buy, Art = art });
            Sfx.Play("click", 0.7f);
            RebuildDayEnd();
        }

        /// <summary>Places the order: every picked line bought in the order it was picked.
        /// A refusal stops the rest — the till is the shop's word, not the basket's.</summary>
        private void Checkout()
        {
            if (_cart.Count == 0) { Toast("BASKET IS EMPTY"); return; }
            RememberScroll();
            _justOrdered.Clear();
            int bought = 0;
            var paid = new List<int>();
            foreach (var e in _cart)
            {
                try { e.Buy(); _justOrdered.Add(e.Key); paid.Add(e.Price); bought++; }
                catch (InvalidOperationException) { Toast("ORDER STOPPED — " + e.Label); break; }
            }
            _cart.Clear();
            Sfx.Play("cash", 0.9f);
            ApplyBarLook();
            RebuildDayEnd();

            // Each line says what it cost, one after the other, under the account.
            for (int i = 0; i < paid.Count; i++) DropMoney(-paid[i], i);

            // THE KEY ANSWERS (2026-08-11, the author: it should grey out and say it was
            // bought, for about three seconds, then come back). A toast said the same thing
            // in a corner of the room; the key is the thing that was pressed, so the key is
            // where the answer belongs — and a control that cannot be pressed again while
            // the order is landing cannot be pressed twice by accident either.
            _checkoutUntil = Time.unscaledTime + CheckoutHold;
            RefreshCheckoutKey();
        }

        private void RefreshCheckoutKey()
        {
            if (_checkout == null || _checkoutLabel == null) return;
            bool spent = Time.unscaledTime < _checkoutUntil;
            var img = _checkout.GetComponent<Image>();
            var btn = _checkout.GetComponent<Button>();
            if (btn != null) btn.interactable = !spent;
            if (img != null)
                img.color = spent ? new Color(0.612f, 0.635f, 0.706f, 1f) : Color.white;
            _checkoutLabel.color = spent ? new Color(0.898f, 0.910f, 0.949f, 0.85f) : Color.white;
            // Spent, the key says so; free, the BASKET says what it says — an empty one
            // reads NOTHING PICKED, and hard-coding PLACE ORDER here would have handed the
            // player a key inviting them to order nothing the moment the hold released.
            _checkoutLabel.text = spent ? "ORDERED"
                : _cart.Count == 0 ? "NOTHING PICKED" : "PLACE ORDER";
        }

        private void StepCheckoutKey()
        {
            if (_checkoutUntil < 0f) return;
            if (Time.unscaledTime < _checkoutUntil) return;
            _checkoutUntil = -1f;
            RefreshCheckoutKey();
        }

        /// <summary>
        /// The pour, on the pointer. Nothing in it may take a raycast: the panel sits under
        /// the cursor, and a graphic that answers the pointer would read as leaving the tile
        /// underneath — which hides the panel, which hands the pointer back, many times a
        /// second. The licence tip learned this the hard way (2026-08-10).
        /// </summary>
        private void ShowShopSpec(RecipeDefinition r)
        {
            if (_shopSpec == null || _shopSpecBody == null) return;
            if (r == null) { _shopSpec.gameObject.SetActive(false); return; }
            // A CRATE IN THE MARKET IS A PAGE YOU DO NOT OWN, so its gauges read empty too
            // (2026-08-20). This is the surface the old market rule meant when it said
            // "buyable recipes show their pour on hover" — it shows what goes IN the drink,
            // which is what the purchase decision needs, and keeps the proportions for the
            // page you have actually bought. A recipe already on the menu never reaches here.
            bool unowned = Run != null && r.Locked && !Run.MenuRecipes.Contains(r);
            float h = DrawRecipeSpec(_shopSpecBody, r, dark: true, width: ShopSpecW - 20f,
                locked: unowned);
            _shopSpec.sizeDelta = new Vector2(ShopSpecW, h + 16f);
            _shopSpec.gameObject.SetActive(true);
            _shopSpec.SetAsLastSibling();
            foreach (var g in _shopSpec.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            FollowPointerWithShopSpec();
        }

        /// <summary>Hangs the spec off the cursor, turning back at the edges of the market's
        /// own panel rather than running off it.</summary>
        private void FollowPointerWithShopSpec()
        {
            // The card's gate, for the same reason — see FollowPointerWithShopCard.
            if (_shopSpec == null) return;
            if (!MarketIsUp)
            {
                if (_shopSpec.gameObject.activeSelf) _shopSpec.gameObject.SetActive(false);
                return;
            }
            if (!_shopSpec.gameObject.activeSelf) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || _dayEndPanel == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dayEndPanel, mouse.position.ReadValue(), null, out local)) return;
            const float Gap = 20f;
            Vector2 size = _shopSpec.sizeDelta;
            float halfW = _dayEndPanel.rect.width * 0.5f, halfH = _dayEndPanel.rect.height * 0.5f;
            // Under the reading card when both are up: they describe the same tile, so they
            // stack into one column rather than fighting for the same corner of the cursor.
            float drop = _shopCard != null && _shopCard.gameObject.activeSelf
                ? _shopCard.sizeDelta.y + 6f : 0f;
            float x = local.x + Gap;
            if (x + size.x > halfW) x = local.x - Gap - size.x;
            float y = local.y - Gap - drop;
            // The drop belongs to the TURNED-BACK case too. Near the foot both panels flip
            // above the cursor, and without it here the table landed back on top of the card
            // it was supposed to be stacked under (measured, 2026-08-11).
            if (y - size.y < -halfH) y = local.y + Gap + drop + size.y;
            _shopSpec.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>
        /// What the pointer is on, said on the pointer. Hiding is the whole answer for "on
        /// nothing": the card is a thing the cursor carries, so an empty one would be a box
        /// following the mouse around the shop saying nothing.
        ///
        /// The card GROWS to its text (2026-08-11). The slab it replaced was a fixed 128
        /// units with every line set to Truncate, which is how a description longer than
        /// three lines simply stopped mid-sentence.
        /// </summary>
        private void ShowShopCard(TileSpec spec)
        {
            if (_cardIdentity == null) return;
            if (spec == null)
            {
                if (_shopCard != null) _shopCard.gameObject.SetActive(false);
                return;
            }
            if (_cardMarkImg != null)
            {
                _cardMarkImg.enabled = spec.Art != null;
                _cardMarkImg.sprite = spec.Art;
            }
            // The mark alone said "a bottle"; the NAME beside it says which. The identity
            // row already carried it, but a hundred units to the right of the picture it
            // belongs to — so the two read as separate facts about the same tile.
            // WHITE AND HEAVY (2026-08-10, the author: the bottle had a mark and a grey
            // line of specifications, and nowhere did it say WHAT IT WAS). The identity row
            // was already here and already carried the name; it was set in the body face at
            // the body's weight, so it read as one more line of small print.
            // NEVER BLANK. A tile that forgot to set Identity showed the bottle's mark, a
            // grey line of specifications and no name at all — which is exactly the one
            // thing the panel exists to say. The tile's own name is always there.
            _cardIdentity.text = !string.IsNullOrEmpty(spec.Identity)
                ? spec.Identity : (spec.Name ?? "").ToUpperInvariant();
            _cardMeta.text = spec.MetaLine ?? "";
            _cardBody.text = spec.Body ?? "";
            WriteBuff(_cardBuffA, _cardBuffAIcon, spec.BuffA);
            WriteBuff(_cardBuffB, _cardBuffBIcon, spec.BuffB);

            // Every row is stacked on the one above it, and the card is cut to the last of
            // them — measured off the text itself, so a long name pushes the description
            // down instead of printing through it.
            float y = 8f;
            y += Mathf.Max(20f, _cardIdentity.preferredHeight);
            if (_cardMeta.text.Length > 0) y += RowAt(_cardMeta, y, 4f);
            else y += 2f;
            _shopCardRule.anchoredPosition = new Vector2(10f, -(y + 4f));
            y += 10f;
            if (_cardBody.text.Length > 0) y += RowAt(_cardBody, y, 0f);
            if (_cardBuffA.text.Length > 0) y += BuffAt(_cardBuffA, _cardBuffAIcon, y);
            if (_cardBuffB.text.Length > 0) y += BuffAt(_cardBuffB, _cardBuffBIcon, y);
            _shopCard.sizeDelta = new Vector2(ShopCardW, y + 10f);
            _shopCard.gameObject.SetActive(true);
            _shopCard.SetAsLastSibling();
            foreach (var g in _shopCard.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            FollowPointerWithShopCard();
        }

        private static float BuffAt(Text row, Image icon, float y)
        {
            float h = RowAt(row, y, 2f);
            var irt = (RectTransform)icon.transform;
            irt.anchoredPosition = new Vector2(irt.anchoredPosition.x, -(y + 3f));
            return h;
        }

        /// <summary>
        /// Whether the pointer's two reading panels are allowed up at all. They describe
        /// MARKET TILES and nothing else, so the market being on screen is the whole
        /// condition — the night's slip is the same panel one step earlier, and a bottle's
        /// specifications hanging off the cursor over the takings describe nothing that is
        /// on that screen.
        /// </summary>
        private bool MarketIsUp => Showing(_dayEndPanel) && _dayEndStep == 1;

        /// <summary>Hangs the reading card off the cursor, turning back at the edges of the
        /// market's own panel rather than running off it.</summary>
        private void FollowPointerWithShopCard()
        {
            // PUT AWAY WITH THE SCREEN IT BELONGS TO (2026-08-19, the author: the card was
            // still following the mouse around the invoice). Every hover puts it away on
            // exit, but an exit is not always REPORTED: leaving the market by Escape or by
            // the foot key takes the panel down UNDER the pointer, which moves nothing and
            // destroys nothing, so OnPointerExit never fires — and the card outlives the
            // aisle, then comes back up with the panel on the next night's slip. The gate
            // is here, in the one thing that runs every frame, rather than at each of the
            // several ways out.
            if (_shopCard == null) return;
            if (!MarketIsUp)
            {
                if (_shopCard.gameObject.activeSelf) _shopCard.gameObject.SetActive(false);
                return;
            }
            if (!_shopCard.gameObject.activeSelf) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || _dayEndPanel == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dayEndPanel, mouse.position.ReadValue(), null, out Vector2 local)) return;
            const float Gap = 18f;
            Vector2 size = _shopCard.sizeDelta;
            float halfW = _dayEndPanel.rect.width * 0.5f, halfH = _dayEndPanel.rect.height * 0.5f;
            float x = local.x + Gap;
            if (x + size.x > halfW) x = local.x - Gap - size.x;
            float y = local.y - Gap;
            if (y - size.y < -halfH) y = local.y + Gap + size.y;
            _shopCard.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>One effect line: a colour AND its own icon. Colour alone would leave the
        /// three kinds indistinguishable for anyone who cannot separate them, and the icons
        /// are one-to-one with the kinds for exactly that reason.</summary>
        private void WriteBuff(Text line, Image icon, Buff buff)
        {
            if (buff == null || string.IsNullOrEmpty(buff.Text))
            {
                line.text = "";
                icon.enabled = false;
                return;
            }
            line.text = buff.Text;
            line.color = buff.Kind == BuffKind.Gain ? BuffGood
                : buff.Kind == BuffKind.Cost ? BuffCost
                : buff.Kind == BuffKind.Bad ? BuffBad : InspectorInk;
            icon.enabled = true;
            icon.color = line.color;
            icon.sprite = ItemArt.Load(
                buff.Kind == BuffKind.Gain ? "sh_b_star"
                : buff.Kind == BuffKind.Cost ? "sh_b_coin"
                : buff.Kind == BuffKind.Bad ? "sh_b_lock" : "sh_b_pour");
        }

        /// <summary>
        /// The header a crate stands under, made on demand: the one the offers already
        /// built, or a fresh one when this aisle has nothing for sale tonight but something
        /// waiting behind a star. Null when the aisle is genuinely finished — then no sign
        /// is drawn and none is wanted.
        /// </summary>
        private RectTransform AisleSign(TycoonRun run, RectTransform existing,
            System.Func<IngredientCard, bool> belongs, bool booze, string title)
        {
            if (existing != null) return existing;
            foreach (var g in run.GatedStock())
            {
                if (IngredientCategories.IsAlcoholic(g.Card.Info?.Category, g.Card.Type) != booze) continue;
                if (!belongs(g.Card)) continue;
                return ShopSection(title);
            }
            return null;
        }

        /// <summary>
        /// The sealed crate for ONE aisle: how many of its lines are still behind a star,
        /// and the nearest of those stars. Draws nothing when the aisle is finished, which
        /// is the whole signal — a shelf with no crate at its foot has nothing left to give.
        /// </summary>
        private void SectionGate(TycoonRun run, System.Func<IngredientCard, bool> belongs,
            bool booze, string noun, RectTransform grid)
        {
            if (grid == null) return;
            int locked = 0;
            double next = double.MaxValue;
            // What the starless half of the aisle is waiting for, in ITS OWN words. The
            // locks already write these — "SERVE ECE WHAT THEY ASK FOR", "NEEDS THE 2-LINE
            // TOWER" — and the crate has been throwing them away and printing a star
            // instead, which is the one thing they are guaranteed not to be waiting for.
            var asked = new List<string>();
            foreach (var g in run.GatedStock())
            {
                if (IngredientCategories.IsAlcoholic(g.Card.Info?.Category, g.Card.Type) != booze) continue;
                if (!belongs(g.Card)) continue;
                locked++;
                // NaN is a line waiting on a person or on the room, not on a rung — it counts
                // as held back but has no number to pull the aisle's hint towards.
                if (!double.IsNaN(g.Stars)) { if (g.Stars < next) next = g.Stars; }
                else if (!string.IsNullOrEmpty(g.Sentence) && !asked.Contains(g.Sentence))
                    asked.Add(g.Sentence);
            }
            if (locked == 0) return;
            // NOTHING HERE IS WAITING FOR A STAR (2026-08-19). This used to fall back to the
            // bar's CURRENT standing, so an aisle held entirely behind the draught tower
            // promised "get 5.0 stars and more of these show up here" to a bar that already
            // had five — a sealed crate telling the player to go and do the one thing that
            // would change nothing. It says what the locks say now.
            bool starless = next == double.MaxValue;
            string wanted = asked.Count > 0 ? string.Join(" · ", asked) : "";
            var was = _cardTarget;
            _cardTarget = grid;
            AddTile(new TileSpec
            {
                Name = locked + " more waiting",
                Money = starless ? locked.ToString() : next.ToString("0.0"),
                GateStars = starless ? double.NaN : next,
                GateNote = starless ? "STILL LOCKED" : null,
                State = TileState.Sealed,
                Identity = starless
                    ? "MORE " + noun.ToUpperInvariant() + "S TO EARN"
                    : "MORE " + noun.ToUpperInvariant() + "S AT " + next.ToString("0.0") + " STARS",
                MetaLine = locked + " " + (locked == 1 ? noun : noun + "s")
                           + " the van will not bring you yet",
                Body = starless
                    ? (wanted.Length > 0 ? wanted : "These are earned, not bought.")
                    : "Get " + next.ToString("0.0") + " stars and more of these show up "
                      + "here.",
                BuffA = new Buff(BuffKind.Bad, starless
                    ? (wanted.Length > 0 ? wanted : "Earned, not bought")
                    : "Needs " + next.ToString("0.0")
                      + " stars · you have " + run.Rating.Average.ToString("0.0")),
            });
            _cardTarget = was;
        }

        /// <summary>A titled section of the market: its header row, then its own grid.</summary>
        private RectTransform ShopSection(string title)
        {
            // An aisle sign: a coloured tick, the name in the signage colour, and a rule
            // running out to the edge — how a storefront heads a shelf.
            // An aisle sign that actually signs the aisle (the author: "başlıkların daha ön
            // plana çıkması lazım") — a solid green band with the name in white, not a
            // grey line of small caps lost between two rows of cards.
            var h = NewRect("SH", _offerRow);
            h.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            var band = NewRect("Band", h);
            Stretch(band, Vector2.zero, Vector2.one, new Vector2(0, 2), new Vector2(0, -2));
            band.gameObject.AddComponent<Image>().color = ShopViceDeep;
            var pip = NewRect("Pip", band);
            Place(pip, new Vector2(0, 0.5f), new Vector2(6, 18), new Vector2(10, 0));
            pip.gameObject.AddComponent<Image>().color = ShopViceLit;
            var t = NewText("T", band, _shop, 16, TextAnchor.MiddleLeft, Color.white);
            Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(700, 18), new Vector2(26, 0));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = title;

            var sec = NewRect("Sec", _offerRow);
            var g = sec.gameObject.AddComponent<GridLayoutGroup>();
            // SIX across, and the arithmetic is the point: 6*160 + 5*8 = 1000 in a 1004
            // viewport. The grid runs from screen x 8 to 1008, the mask cuts at 1012 and the
            // scroll track begins at 1022 — 4 units inside the mask, 14 units of air before
            // the bar. The old line claimed "leaving 790" against a viewport that has never
            // been 790: it was 730, so a third of every fourth card — its whole pill and
            // pick-mark — was masked away, which is what the author saw run under the bar.
            // No padding and no centring: the 4 units of slack must stay on the right.
            g.cellSize = new Vector2(TileW, TileH);
            g.spacing = new Vector2(8, 8);
            g.padding = new RectOffset(0, 0, 0, 0);
            g.childAlignment = TextAnchor.UpperLeft;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 6;
            return sec;
        }

        /// <summary>
        /// One endorsement, drawn the way a licence draws its categories: a bordered cell
        /// with the pictogram and the word side by side, not a picture with a caption
        /// floating under it. Returns 1 so the caller can count.
        /// </summary>
        private int PrefChip(Sprite icon, string label, RectTransform host = null)
        {
            const float CellH = 38f, IconBox = 26f;
            var chip = NewRect("Pref", host ?? _idPrefRow);
            var plate = chip.gameObject.AddComponent<Image>();
            plate.color = new Color(0.98f, 0.97f, 0.93f, 1f);
            plate.raycastTarget = false;
            Frame(chip, 2f, new Color(0.42f, 0.39f, 0.34f, 1f));

            var iconRt = NewRect("I", chip);
            Place(iconRt, new Vector2(0, 0.5f), new Vector2(IconBox, IconBox), new Vector2(7, 0));
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
            img.enabled = icon != null;

            var t = NewText("L", chip, _body, 8, TextAnchor.MiddleLeft, UITheme.Night[1]);
            Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(200, 12),
                new Vector2(IconBox + 12f, 0));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = label;

            var le = chip.gameObject.AddComponent<LayoutElement>();
            // The measured cell: icon gutter, the word at 6.7 points a character, and the
            // border's own margin. The old chip guessed at 7 per character with no gutter
            // and the widest word ran out of its box.
            le.preferredWidth = IconBox + 12f + label.Length * 6.7f + 12f;
            le.preferredHeight = CellH;
            return 1;
        }

        /// <summary>
        /// The lamp behind PLACE ORDER, which only burns when there is an order to place.
        /// A slow breath rather than a blink: the key is asking to be pressed, not warning
        /// about anything, and the chrome language keeps the fast pulses for refusals.
        /// It also goes dark for the three seconds after a checkout, while the key itself
        /// reads ORDERED — the one moment pressing it again would do nothing.
        /// </summary>
        private void StepCheckoutLamp()
        {
            if (_checkoutLampImg == null) return;
            bool wants = _cart.Count > 0 && Time.unscaledTime >= _checkoutUntil;
            float breath = wants
                ? 0.35f + 0.30f * Mathf.Sin(Time.unscaledTime * 3.4f)
                : 0f;
            var c = UITheme.Lime[3];
            var now = _checkoutLampImg.color;
            float a = Mathf.Lerp(now.a, breath, 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime));
            _checkoutLampImg.color = new Color(c.r, c.g, c.b, a);
        }

        /// <summary>
        /// ONE LISTING, 160x208 portrait. The old card was 190x104 landscape carrying up to
        /// five texts, a rotated stamp and a tick — as much as 800 units of type on a 190
        /// plate, saying the same thing four different ways and contradicting itself twice
        /// (a just-ordered stool printed "SOLD", a SOLD stamp AND "+ ADD").
        ///
        /// A tile now carries at most five objects: a state strip, a corner chip, the
        /// product, two short texts and one control. Everything a listing has to EXPLAIN
        /// moved to the inspector, which is where the author asked for it.
        /// </summary>
        private RectTransform AddTile(TileSpec spec)
        {
            var state = spec.State;
            bool hasPill = state == TileState.Orderable || state == TileState.Unaffordable
                        || state == TileState.Picked || state == TileState.Refundable
                        || state == TileState.NoFitting;

            var rt = NewRect("Tile", _cardTarget != null ? _cardTarget : _offerRow);
            var img = rt.gameObject.AddComponent<Image>();
            // THE 98 PANEL (2026-08-19, the author: "ürün kartlarını kötü buluyorum" — the
            // chamfered Card read as a washed grey lozenge on a grey page). A listing is a
            // small raised panel of the site now, square-cornered with the era's two-step
            // bevel, tinted with the state's paper — so its edges are shades of the
            // listing's own colour, same rule the Card obeyed, sharper object wearing it.
            img.sprite = ChromeArt.Win98Key();
            img.type = Image.Type.Sliced;
            img.color = PlateOf(state);

            // The click. A tile that cannot be acted on gets no Button at all, so the
            // pointer itself says whether there is anything here to do.
            if (spec.OnClick != null)
            {
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                var act = spec.OnClick;
                button.onClick.AddListener(() => act());
                // …and only THEN does it warm under the pointer. A sealed crate or a bottle
                // with no cash behind it lights up for nobody: hover says "you are pointing
                // at this", and a listing that cannot be acted on must not answer as though
                // it can (2026-08-14).
                MarkHoverable(rt, img);
            }

            // Hovering fills the inspector — the one place long text is allowed to live.
            // NOT an EventTrigger: it implements IScrollHandler too, so it ate the mouse
            // wheel and froze the aisle over every tile that had something to read.
            var shown = spec;
            var hover = rt.gameObject.AddComponent<HoverRelay>();
            hover.Entered = () => { ShowShopCard(shown); ShowShopSpec(shown.Recipe); };
            hover.Exited = () => { ShowShopCard(null); ShowShopSpec(null); };

            // 0 — THE BRAND CAP: four units of the vice fade across the panel's head,
            // inside the bevel. Every listing carries the storefront's one signature, the
            // way every window of a 98 site carried its bar — and at four units it is a
            // COLOPHON, not a title bar: it names the shop, it says nothing about the
            // listing, so the fade's chrome-only law holds.
            var cap = NewRect("Cap", rt);
            Place(cap, new Vector2(0, 1), new Vector2(TileW - 4f, TileCapH), new Vector2(2f, -2f));
            var capImg = cap.gameObject.AddComponent<Image>();
            capImg.raycastTarget = false;
            if (state == TileState.Sealed)
            {
                // A SEALED CRATE LOSES THE STOREFRONT'S COLOURS (2026-08-19, the author:
                // "kilitli olanların tepesindeki fade şerit yerini gri bir şerit alsın").
                // The fade is the shop saying "this is ours to sell"; on a crate the shop
                // will not open, it was the one cheerful thing on an otherwise chained card.
                //
                // Cream[1] and not a Graphite step: Graphite and Brick are ARCHITECTURE ONLY
                // (14 v3 §3) and may never carry a signal, and "you cannot buy this" is a
                // signal. Cream is under no such rule and its second step is a true grey.
                capImg.color = UITheme.Cream[1];
            }
            else capImg.sprite = ChromeArt.FadeStrip();

            // 1 — (THE STRIP IS GONE, 2026-08-19, the author: "kartin solunda yesil,
            // kirmizi, kahverengi dik serit tasarimi cok AI duruyor" — and the small box on
            // its top-left corner with it.)
            //
            // They were the same mistake twice. A coloured bar welded to an edge and a
            // coloured square welded to a corner are not things in a bar's world; they are
            // the house style of a dashboard, and 16 §6.8 has the name for it — a dot
            // standing in for an object. Worse, they were the ONLY two channels that said
            // the state, so between them they took a fact that deserved a sentence and gave
            // it two pieces of decoration.
            //
            // What the state says is a ROW now, on the grid with the name and the meta:
            // a mark and a word (row 4a below). Shape and language carry it, colour comes
            // along, and the plate's own tint is still underneath — three channels, none of
            // them bolted to an edge.

            // 2 — THE PRODUCT, on the shelf line every class shares.
            // Glassware is mostly transparent, and transparent on a white page is nothing
            // at all (the author: the glasses disappear). A vessel gets a recess to stand
            // in — a shaded back with a lit lip at the foot line — the way the bar's own
            // back shelf gives them something to be seen against. Bottles are opaque and
            // need none of it.
            if (spec.Art != null && spec.ArtH == VesselH)
            {
                var niche = NewRect("Niche", rt);
                Place(niche, new Vector2(0.5f, 0), new Vector2(116, 112),
                    new Vector2(0, ProductFootY - 4f));
                var ni = niche.gameObject.AddComponent<Image>();
                // sh_niche2 is DRAWN AT 116x112 — the exact rect — so it goes in 1:1: no
                // slicing, no stretch, no smear. A back-bar alcove is a thing in the bar
                // rather than a piece of chrome, which is why this one is generated.
                // sh_niche3: a cool DISPLAY CASE in the page's own family. The generated warm
                // wooden alcove was a lovely picture of somewhere else — a brown lamp-lit
                // cupboard pasted into a white-and-green catalogue.
                var nicheArt = ItemArt.Load("sh_niche3") ?? ItemArt.Load("sh_niche2");
                if (nicheArt != null) ni.sprite = nicheArt;
                else ni.color = ShopAisle;
                ni.raycastTarget = false;
                // A dead listing dims the recess with the glass in it rather than going
                // pale — a pale recess is the white page again, and the vessel vanishes
                // exactly where it was supposed to become visible.
                if (state == TileState.Unaffordable || state == TileState.Held)
                    ni.color = new Color(0.714f, 0.729f, 0.784f, 1f);
            }
            if (spec.Art != null)
            {
                var thumb = NewRect("Art", rt);
                PlaceProduct(thumb, spec.Art, spec.ArtH);
                var ti = thumb.gameObject.AddComponent<Image>();
                ti.sprite = spec.Art;
                ti.raycastTarget = false;
                ti.color = state == TileState.Unaffordable ? new Color(0.78f, 0.80f, 0.80f, 0.55f)
                    : state == TileState.Held ? new Color(0.855f, 0.871f, 0.918f, 0.85f)
                    : Color.white;
            }
            else if (state == TileState.Sealed)
            {
                // A crate the house will not open, and it is the WHOLE tile that is shut:
                // the chains run corner to corner (the author — a 78px X in the middle read
                // as an ornament, not as something chained), drawn at the tile's own
                // 160x208 so no link is stretched into an oval, with the padlock where they
                // cross. No product and no name: the empty well is the tell.
                // AT ITS OWN SIZE, centred (2026-08-19). sh_chain_x is drawn at 160x208 and
                // the card is 230 now, so stretching it to fill would pull every link into
                // an oval — the exact fault the art was cut at the tile's size to avoid.
                var chain = NewRect("Chain", rt);
                Place(chain, new Vector2(0.5f, 0.5f), new Vector2(160f, 208f), Vector2.zero);
                var chainImg = chain.gameObject.AddComponent<Image>();
                chainImg.sprite = ItemArt.Load("sh_chain_x") ?? ItemArt.Load("sh_chain");
                chainImg.raycastTarget = false;
                chainImg.color = new Color(1f, 1f, 1f, 0.95f);
                var padlock = NewRect("Lock", rt);
                Place(padlock, new Vector2(0.5f, 0.5f), new Vector2(42, 63), new Vector2(0, 6));
                var lockImg = padlock.gameObject.AddComponent<Image>();
                lockImg.sprite = ItemArt.Load("sh_lock");
                lockImg.preserveAspect = true;
                lockImg.raycastTarget = false;
            }

            // 3 — THE NAME, title case straight from the JSON. Two lines of 26 characters;
            // the longest string in the game is 24 and lands on one.
            //
            // A SEALED crate is laid out differently, and it has to be: the chains cross
            // the whole tile now, so a star gate sitting in the bottom-left action row
            // printed straight through them. The gate belongs directly under the padlock,
            // centred, where the eye already is — and with the chains and the lock saying
            // "sealed" three ways over, the crate needs no left-aligned name beside them.
            if (state == TileState.Sealed)
            {
                // A TAG HUNG ON THE LOCK. The chains cross the whole tile, so a star gate
                // set straight onto the plate lands on the links whatever row it sits in —
                // it needs its own ground, not a better y. A dark tag under the padlock is
                // that ground, and it is the thing a chained crate would actually carry.
                bool drawnGate = !double.IsNaN(spec.GateStars);
                var tag = NewRect("Tag", rt);
                Place(tag, new Vector2(0.5f, 1), new Vector2(104, drawnGate ? 58 : 44),
                    new Vector2(0, -132));
                var tagImg = tag.gameObject.AddComponent<Image>();
                tagImg.color = new Color(ShopInk.r, ShopInk.g, ShopInk.b, 0.92f);
                tagImg.raycastTarget = false;
                var gate = NewText("Gate", tag, _display, 16, TextAnchor.MiddleCenter, Color.white);
                Place(gate.rectTransform, new Vector2(0.5f, 1), new Vector2(96, 20),
                    new Vector2(0, -5));
                gate.horizontalOverflow = HorizontalWrapMode.Wrap;
                gate.verticalOverflow = VerticalWrapMode.Truncate;
                gate.text = spec.Money;
                var what = NewText("Sealed", tag, _body, 8, TextAnchor.MiddleCenter,
                    new Color(0.624f, 0.647f, 0.729f, 1f));
                Place(what.rectTransform, new Vector2(0.5f, 1), new Vector2(96, 12),
                    new Vector2(0, -27));
                what.horizontalOverflow = HorizontalWrapMode.Wrap;
                what.verticalOverflow = VerticalWrapMode.Truncate;
                what.text = string.IsNullOrEmpty(spec.GateNote) ? "STARS TO OPEN" : spec.GateNote;
                if (drawnGate)
                {
                    // The gate itself, drawn between its figure and its word: the row is
                    // lit to what the crate WANTS, not to what the bar has — a padlock
                    // states its price, and the standing is read off the top bar.
                    StarRow(tag, new Vector2(0.5f, 1), new Vector2(0, -30f), 12f,
                        spec.GateStars, UITheme.Amber[3], new Color(1f, 1f, 1f, 0.16f));
                    what.rectTransform.anchoredPosition = new Vector2(0, -44);
                }
            }
            else
            {
                // NOT THE BOLD FACE (2026-08-11, the author: change whatever font the
                // product names are set in). It is the same complaint the receipt's figures
                // had: Silkscreen Bold is drawn on an 8px grid with NO side bearing, so its
                // letters touch at every size and a name reads as one long shape. The two
                // ways out are a lighter weight or a face that carries its own gap;
                // PressStart2P carries its gap inside the cell, which is why it is the one
                // of the three that can be set solid — and at 8 it is exactly 1x its design
                // size, so it lands on the pixel grid perfectly.
                //
                // It is wider — a flat 8 units a character against the bold's 6.25 mixed —
                // so the column holds 17 characters a line instead of 22. The box is two
                // lines, and the longest name on the shelf is 24 characters, which is what
                // the measurement below had to settle before this shipped.
                // THE NAME PLATE (2026-08-19, the author: "isimlerini kutu içerisine al
                // veya ön plana çıkması için bir şey yap ... daha okunaklı ve kalın bir
                // font"). The name was 8px PressStart2P lying loose on the card, one grey
                // among four other greys, and on a shelf of twelve listings the thing you
                // are actually shopping BY was the quietest reading on the plate.
                //
                // It is a NAME PLATE now, and that is not a box invented to hold it: the
                // back bar of this game has name plates on its rails, so the aisle borrows
                // the object the room already owns (16 §6, the positive form). Dark field,
                // light type — the one inversion on the card, which is why the eye lands on
                // it first.
                var namePlate = NewRect("NamePlate", rt);
                Place(namePlate, new Vector2(0, 1), new Vector2(TileW - 8f, TileNameH),
                    new Vector2(4f, -TileNameTop));
                var nameBg = namePlate.gameObject.AddComponent<Image>();
                // ONE PLATE FOR EVERY STATE. The first cut dimmed it for the listings you
                // cannot buy — Cream[0] under Cream[2] type, which measures 2.6:1 and shipped
                // a name you had to lean in to read. A name is not a state: what a bottle is
                // called does not change because the till is empty. The card's own tint and
                // the red NO CASH key carry that, and the plate stays legible.
                nameBg.color = ShopViceDeep;
                nameBg.raycastTarget = false;

                // SILKSCREEN BOLD AT 16, not PressStart2P at 8. Both halves of that matter:
                // twice the size, and the bold face. The standing objection to this face for
                // product names (2026-08-11) was that its letters touch — true, and it is a
                // complaint about 8px, where the gap it does not have is a whole pixel of a
                // six-pixel glyph. At 16 the same face is 2x its design size, every letter
                // lands on the grid, and light-on-dark parts them by contrast anyway.
                var name = NewText("Name", namePlate, _shop, 16, TextAnchor.UpperLeft,
                    Color.white);
                Stretch(name.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(6, 3), new Vector2(-6, -3));
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                name.text = spec.Name;
            }

            // 4 — ONE contextual token, or the stock meter where stock IS the fact.
            if (spec.StockFrac >= 0f)
            {
                // A METER YOU CAN ACTUALLY READ (2026-08-10, the author). It was a 6-unit
                // hairline with an 8pt percentage floating to its right: at a glance you
                // could tell "some" from "none" and nothing else. Now it is 12 deep with a
                // dark surround, so the bar has an edge to be read against, and the number
                // rides ON it in the shop's bold face — one object, one reading.
                // A GAUGE DOWN THE SIDE (2026-08-11, the author: stand the meter up beside
                // the bottles, and the bottle stays centred in its box).
                //
                // It was a 136-wide bar lying at -170, and the ADD pill sits 6..30 up from
                // the tile's foot — the bar's own bottom edge is 24 up from it, so the two
                // shared six units and the percentage printed into the key. Standing it up
                // does not just move the collision, it removes the row they were fighting
                // over: the strip runs the height of the ART, where there is nothing else,
                // and reads like the level in the bottle it is standing next to. The art is
                // an overlay on the left, so nothing about the bottle's placement changes.
                // ONE GAUGE, ONE SIZE, ON THE RIGHT EDGE (2026-08-11, the author's second
                // ruling, and the better one). Pinning it to each bottle's own drawing put
                // it where the product was — but that means it MOVES: a page of tiles then
                // has six gauges at six different x's and six different heights, and an
                // instrument you have to find on every card is not an instrument. Fixed and
                // flush right, it is the same stripe in the same place on every plate, and
                // the eye can run down a column of them and compare.
                //
                // It still clears the ADD key by construction: the strip's foot is at the
                // product's own foot line, 68 up from the plate, and the key lives in the
                // bottom 30.
                float frac = Mathf.Clamp01(spec.StockFrac);
                const float StripW = 12f;
                const float StripH = TileArtH - 8f;
                const float StripX = TileW - TilePad - StripW;
                const float StripTop = -(TileArtTop + 4f);
                var surround = NewRect("Track", rt);
                Place(surround, new Vector2(0, 1), new Vector2(StripW, StripH),
                    new Vector2(StripX, StripTop));
                var surroundImg = surround.gameObject.AddComponent<Image>();
                surroundImg.color = UITheme.ClubBlue[0];
                surroundImg.raycastTarget = false;

                var well = NewRect("Well", rt);
                Place(well, new Vector2(0, 1), new Vector2(StripW - 4f, StripH - 4f),
                    new Vector2(StripX + 2f, StripTop - 2f));
                var wellImg = well.gameObject.AddComponent<Image>();
                wellImg.color = new Color(0.792f, 0.812f, 0.871f, 1f);
                wellImg.raycastTarget = false;

                // THE LEVEL WEARS THE FADE (2026-08-19, the author: "restock doluluk barı da
                // yeşil yerine bu renk olsun"). The fill is the vertical FadeStrip CROPPED
                // by a Filled image, never squeezed: the rect spans the whole well and
                // fillAmount reveals the bottom `frac` of it, so a half bottle shows the
                // fade's blue half and a full one the whole blue-into-pink run — the level
                // climbs INTO the pink the way the evening climbs into the neon. Geometry
                // still carries the reading (height is the fraction); the fade only dresses
                // it, which is what keeps the chrome-only law honest here.
                //
                // The one signal stays a signal: under a quarter the level drops the fade
                // for flat ShopCost red, because "nearly out" is a warning and a warning is
                // never worn as decoration.
                var fill = NewRect("Fill", well);
                Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var fillImg = fill.gameObject.AddComponent<Image>();
                if (frac < 0.25f)
                {
                    fill.anchorMax = new Vector2(1, 0);
                    fill.pivot = new Vector2(0.5f, 0);
                    fill.sizeDelta = new Vector2(0, (StripH - 4f) * frac);
                    fill.anchoredPosition = Vector2.zero;
                    fillImg.color = ShopCost;
                }
                else
                {
                    fillImg.sprite = ChromeArt.FadeStrip(horizontal: false);
                    fillImg.type = Image.Type.Filled;
                    fillImg.fillMethod = Image.FillMethod.Vertical;
                    fillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
                    fillImg.fillAmount = frac;
                }
                fillImg.raycastTarget = false;

                // The reading moved onto the STATE ROW (2026-08-19), right-aligned. It used
                // to float at the card's top-right corner, which put a number in the one
                // band that carries no words — and left the state row half empty. Words
                // about the listing all live on one line now: what it is on the left, how
                // much of it there is on the right.
                var pct = NewText("Pct", rt, _shop, 8, TextAnchor.MiddleRight, ShopInkSoft);
                Place(pct.rectTransform, new Vector2(1, 1), new Vector2(60, TileStateH),
                    new Vector2(-TilePad, -TileStateTop));
                pct.raycastTarget = false;
                pct.text = Mathf.RoundToInt(frac * 100f) + "%";
            }
            else if (!string.IsNullOrEmpty(spec.Meta) && state != TileState.Sealed)
            {
                var meta = NewText("Meta", rt, _body, 8, TextAnchor.MiddleLeft, TileMetaInk);
                Place(meta.rectTransform, new Vector2(0, 1), new Vector2(ContentW, TileMetaH),
                    new Vector2(TilePad, -TileMetaTop));
                meta.horizontalOverflow = HorizontalWrapMode.Wrap;
                meta.verticalOverflow = VerticalWrapMode.Truncate;
                meta.text = spec.Meta;
            }

            // 5 — THE ACTION ROW: one money token, and at most one control. Both texts
            // TRUNCATE rather than overflow — Overflow is exactly how the old badge walked
            // 165 units onto its neighbour.
            if (!string.IsNullOrEmpty(spec.Money) && state != TileState.Sealed)
            {
                // Sealed puts its price — the star gate — under the padlock instead.
                //
                // ON A TAG (2026-08-19, the author: "fiyatını gösteren yazıyı bir fiyat
                // etiketi içerisine al"). A number set loose in the bottom-left corner was
                // the last thing on this card still being a caption rather than an object;
                // ChromeArt.PriceTag is the card of stock a shop hangs on a bottle's neck,
                // 9-sliced so "$8" and "+$105" are one drawing at two widths.
                //
                // AMBER, which is not decoration: money is Amber and only money is (16 §5),
                // so the tag is the sacred colour doing exactly its job. Out of reach, it
                // drops to the ramp's dark step — still plainly a price tag, plainly one you
                // cannot pay, and the key beside it says NO CASH in red.
                var tag = NewRect("PriceTag", rt);
                Place(tag, new Vector2(0, 1), new Vector2(62, 16f),
                    new Vector2(TilePad, -(TileFootTop + 2f)));
                var tagImg = tag.gameObject.AddComponent<Image>();
                tagImg.sprite = ChromeArt.PriceTag();
                tagImg.type = Image.Type.Sliced;
                tagImg.color = state == TileState.Unaffordable || state == TileState.Held
                    ? UITheme.Amber[0] : UITheme.Money;
                tagImg.raycastTarget = false;

                // The type sits in the tag's BODY, clear of the nib and its punch hole —
                // the 11 units the 9-slice keeps for the point are 11 units of no man's land.
                var money = NewText("Money", tag, MoneyFace(spec.Money), 16, TextAnchor.MiddleLeft,
                    state == TileState.Unaffordable || state == TileState.Held
                        ? UITheme.Amber[4] : UITheme.TextOnAmber);
                Stretch(money.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(13, 0), new Vector2(-4, 0));
                money.horizontalOverflow = HorizontalWrapMode.Overflow;
                money.verticalOverflow = VerticalWrapMode.Truncate;
                money.text = spec.Money;
            }
            else if (!string.IsNullOrEmpty(spec.Word) && state != TileState.Held)
            {
                // Held says it on the sash across the product, so the action row stays
                // empty — printing FULL twice on one tile is the habit this rewrite was
                // supposed to break.
                var word = NewText("Word", rt, _shop, 16, TextAnchor.MiddleLeft, MoneyInk(state));
                Place(word.rectTransform, new Vector2(0, 1), new Vector2(62, TileFootH),
                    new Vector2(TilePad, -TileFootTop));
                word.horizontalOverflow = HorizontalWrapMode.Wrap;
                word.verticalOverflow = VerticalWrapMode.Truncate;
                word.text = spec.Word;
            }

            if (hasPill && !string.IsNullOrEmpty(spec.PillVerb))
            {
                // A KEY YOU COULD PRESS (2026-08-11, the author: "ADD butonu çok yapay
                // duruyor"). The generated pill was a flat lozenge with a word on it — a
                // picture of a button. This one is drawn with an edge and a throw: two dark
                // rows under the face, so it stands above the card instead of being printed
                // on it. The label rides one pixel up, off the throw.
                var pill = NewRect("Pill", rt);
                Place(pill, new Vector2(1, 1), new Vector2(70, TileFootH),
                    new Vector2(-TilePad, -TileFootTop));
                var pillImg = pill.gameObject.AddComponent<Image>();
                // The 98 key face (2026-08-19), same drawing as every button on this site.
                pillImg.sprite = ChromeArt.Win98Key();
                pillImg.type = Image.Type.Sliced;
                pillImg.color = PillOf(state);
                pillImg.raycastTarget = false;
                var label = NewText("L", pill, _shop, 8, TextAnchor.MiddleCenter, PillInk(state));
                Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(6, 0), new Vector2(-6, 0));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.text = spec.PillVerb;
                // The TILE is the click, so the tile's press turns ITS key inside out —
                // the pointer lands anywhere on the listing and the ADD key answers.
                if (spec.OnClick != null)
                {
                    var press = rt.gameObject.AddComponent<Win98Press>();
                    press.Face = pillImg;
                    press.Caption = label.rectTransform;
                }
            }

            // 6 — the picked tile is the only one wearing a frame on all four sides.
            if (state == TileState.Picked) Frame(rt, 2f, StripPicked);

            // (THE SASH IS GONE, 2026-08-19. A dark band printed FULL across the bottle
            // while the gauge beside it read 100% and the corner chip wore an "=" — the
            // same fact three times on one card, which is §6.5 exactly. The state row says
            // it once, in the place every card says everything.)

            // 7 — THE STATE, IN A ROW (2026-08-19). This is what replaced the edge strip
            // and the corner chip, and like the chip before it, it is built LAST so nothing
            // on the card can draw over the one line that says what the listing is doing.
            //
            // The card had four channels for saying what a listing was doing — a strip hue,
            // a chip glyph, a plate tint, and whether a control existed — and two of them
            // were decoration welded to the plate's edges. They are one LINE now, sitting on
            // the grid between the meta and the foot, built the way every other line on this
            // page is built: a drawn mark, then a word, then the reading right-aligned.
            //
            // It costs nothing in what the player can tell apart. Shape says it (each state
            // has its own mark), language says it (each has its own word), the plate's tint
            // still says it underneath, and the ADD key's presence still says it — so the
            // colour-blind path reads on three channels instead of on a hue down an edge.
            // And it gains the thing the strip could never give: a listing you can READ.
            string stateWord = StateWordOf(state);
            if (!string.IsNullOrEmpty(stateWord))
            {
                float markX = TilePad;
                var ink = StateInk(state);
                var markArt = StateMark(state);
                RectTransform markRt = null;
                if (markArt != null)
                {
                    markRt = NewRect("StateMark", rt);
                    Place(markRt, new Vector2(0, 1), new Vector2(TileStateH, TileStateH),
                        new Vector2(markX, -TileStateTop));
                    var mi = markRt.gameObject.AddComponent<Image>();
                    mi.sprite = markArt;
                    mi.preserveAspect = true;
                    mi.raycastTarget = false;
                    mi.color = ink;
                    markX += TileStateH + 4f;
                }
                var stateText = NewText("State", rt, _shop, 8, TextAnchor.MiddleLeft, ink);
                // IT STOPS SHORT OF THE READING. The stock percentage shares this row and
                // is right-aligned to the same margin, so a word allowed to overflow would
                // print straight through it on the restock tab — where a full shelf and an
                // empty wallet are exactly the two states that have most to say. 44 units
                // is what "100%" takes in the shop face plus a space to breathe.
                const float ReadingCol = 44f;
                Place(stateText.rectTransform, new Vector2(0, 1),
                    new Vector2(TileW - markX - TilePad - ReadingCol, TileStateH),
                    new Vector2(markX, -TileStateTop));
                stateText.horizontalOverflow = HorizontalWrapMode.Wrap;
                stateText.verticalOverflow = VerticalWrapMode.Truncate;
                stateText.text = stateWord;
                // The van still lands. It lands on the ROW rather than on a corner square:
                // the stamp belongs to the thing that says "ordered", and that is this line.
                if (state == TileState.Ordered && !Motion.Reduced)
                    StartCoroutine(StampDrop(markRt != null ? markRt : stateText.rectTransform));
            }
            return rt;
        }
    }
}
