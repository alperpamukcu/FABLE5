using System;
using System.Collections.Generic;
using System.Text;
using LastCall.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The drink menu: a wooden clipboard holding a sheet of paper, the stocked
    /// bottles listed on it by section (GDD 21 §10 puts beer at the front). Changing
    /// pages slides the page across a fixed clip while the board holds still.
    /// </summary>
    public sealed partial class TycoonServiceFlow
    {

        // ── the menu ─────────────────────────────────────────────────────────────
        // The aisle pages and their page-turn slide left in the 2026-08-07 sweep: the wall
        // has been ONE page since 2026-07-31, so the machinery only ever idled.

        /// <summary>Puts the page back on its mark whenever the menu opens.</summary>
        private void ResetPageSlide()
        {
            if (_bottleList != null) _bottleList.anchoredPosition = _listHome;
        }

        /// <summary>
        /// Whether a bottle belongs on the BACK BAR at all. Since the 2026-08-13 rebuild the
        /// wall shows the WHOLE bar, fizz included — the fridge that used to be the fizz's
        /// only address is retired, and clicking a carbonated bottle here routes it to the
        /// glass stage already in hand (OpenBottle), the same door it has always poured
        /// through. Only the garnishes stay off the wall: they are a pinch, not a bottle,
        /// and their jars stand on the benches. Beer stays: its kegs are drawn on the floor
        /// below the wall.
        /// </summary>
        private static bool OnTheBackBar(IngredientCard card)
        {
            return card.Type != IngredientType.Garnish;
        }

        /// <summary>The word a bottle wears on the shelf: its STYLE, which is what a recipe
        /// asks for, falling back to what kind of thing it is when a bottle names no style.
        /// Short by nature — the plates stand as close together as the bottles do.</summary>
        private static string ShelfWord(IngredientCard card)
        {
            string style = card.Info?.Style;
            if (!string.IsNullOrEmpty(style)) return style.Replace('_', ' ').ToUpperInvariant();
            switch (card.Type)
            {
                case IngredientType.Beer: return "BEER";
                case IngredientType.Spirit: return "SPIRIT";
                case IngredientType.Sour: return "SOUR";
                case IngredientType.Sweet: return "SWEET";
                case IngredientType.Bitter: return "BITTER";
                case IngredientType.Bubbly: return "SODA";
                case IngredientType.Garnish: return "GARNISH";
                default: return card.Name.ToUpperInvariant();
            }
        }

        // (Handwritten() retired, audit 2026-08-11: its whole body set fontStyle to
        // Normal — the default — a decision record wearing a function's clothes. The
        // decision stands: the pixel faces have no true italic, and Unity's sheared
        // fake reads as broken, so the sheet is set upright.)


        /// <summary>Gives a corner control the same press as the section keys: it swaps to its
        /// pressed art and dips as it goes down.</summary>
        private static void GiveKeyPress(RectTransform rt, Button btn, Image img, string pressedName)
        {
            var down = ItemArt.Load(pressedName);
            if (down != null && img.sprite != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var st = btn.spriteState;
                st.pressedSprite = down; st.selectedSprite = img.sprite;
                btn.spriteState = st;
            }
            var sink = rt.gameObject.AddComponent<PressSink>();
            sink.Face = rt; sink.Depth = 6f; sink.Squash = 0.02f;
        }

        /// <summary>Rings a label in black so it stays legible on any coloured key. The ring is one
        /// font-pixel wide and closes on all eight sides — see <see cref="PixelOutline"/>.</summary>
        private static Text Outlined(Text t, float thickness = 2f)
        {
            var o = t.gameObject.AddComponent<PixelOutline>();
            o.EffectColor = new Color(0f, 0f, 0f, 1f);
            o.Distance = thickness;
            return t;
        }

        /// <summary>The wall, rebuilt: one page, every bottle (no aisles since 2026-07-31).</summary>
        private void RefreshMenu()
        {
            var run = Run;
            foreach (Transform child in _bottleList) Destroy(child.gameObject);
            BuildShelfPage(run);

            bool loaded = !run.Glass.IsEmpty || !run.ServingGlass.IsEmpty;
            bool owing = BenchUnfinished(run);
            if (_serveButton != null)
            {
                // Still pressable while the bench is owed something — pressing it is how you
                // are TAKEN there. A dead key would leave a player with a half-built drink
                // and no key that does anything, which is the state the bug report describes.
                _serveButton.interactable = loaded;
                if (_serveLabel != null)
                {
                    _serveLabel.text = !loaded ? "POUR FIRST" : owing ? "FINISH THE TIN" : "SERVE";
                    _serveLabel.color = loaded ? Color.black : new Color(0.1f, 0.08f, 0.06f, 0.45f);
                }
            }
        }

        /// <summary>
        /// An aisle's page: the bottles STANDING ON A SHELF rather than listed as keys (v5 P13,
        /// the notes' shelf view). Hovering one raises an info panel with what is left in it and
        /// what it costs; clicking takes it to the prep stage as the keys used to.
        /// </summary>
        private void BuildShelfPage(TycoonRun run)
        {
            // THE WHOLE WALL (2026-07-31): every bottle the bar owns on one back-bar,
            // no aisles. The hover panel still answers what each one is.
            _menuTitle.text = "LAST CALL";

            // Beer leaves the shelves (the author, 2026-08-01): it lives in KEGS on the
            // floor, drawn at keg scale with only their crowns in frame. Everything else
            // stands on the wall in rows that widen as the cellar grows, so the endgame
            // bar still fits on three shelves.
            var items = new List<ShelfBottle>();
            var kegs = new List<ShelfBottle>();
            foreach (var b in run.Shelf.Bottles)
            {
                if (!OnTheBackBar(b.Ingredient)) continue;
                if (b.Ingredient.Type == IngredientType.Beer) kegs.Add(b);
                else items.Add(b);
            }
            float areaW = _bottleList.rect.width, areaH = _bottleList.rect.height;

            // BIGGER, AND STOOD CLOSER TOGETHER (2026-08-11, the author). The wall used to
            // divide a plank into `perRow` equal slots and drop one bottle in the middle of
            // each, so a shelf of five short bottles was five bottles marooned in a lot of
            // wood — and every bottle was capped by a slot width nobody had measured against
            // its art. A row is packed by its bottles' OWN widths now: the height is the
            // shelf's (which is what a slim bottle can grow on), the width follows from each
            // silhouette's aspect, and they stand at a fixed gap, centred. A row that would
            // outgrow the plank is scaled down as a whole — so bottles never overlap and
            // never leave the wall, by construction rather than by a chosen column count.
            int perRow = Mathf.Max(5, Mathf.CeilToInt(items.Count / 3f));
            int shelves = Mathf.Max(3, Mathf.CeilToInt(items.Count / (float)perRow));
            // The top padding is headroom for the first shelf's overhanging bottles and
            // must come out of the bands' budget, or the bottom band spills off the page.
            // So must EVERY gap between them: the budget used to give back a single GridGap
            // however many shelves there were, which left the bottom plank a few pixels
            // under the mask — and raising the headroom to the overhang it actually needs
            // would have pushed the bottom clean off. The page is now spent exactly:
            // headroom + the bands + the gaps between them.
            float shelfH = (areaH - ListTopPad - GridGap * Mathf.Max(0, shelves - 1)) / shelves;

            for (int row = 0; row < shelves; row++)
            {
                int from = row * perRow, count = Mathf.Max(0, Mathf.Min(perRow, items.Count - from));
                // An empty plank still hangs on the wall (2026-08-01): a young bar faces a
                // sparsely stocked back-bar, not a wall with one shelf on it.

                // The sheet lays its children out vertically, so the shelves are STACKED by the
                // layout rather than anchored by hand -- hand-anchored bands were simply
                // overridden and the page came up blank.
                var band = NewRect($"Shelf{row}", _bottleList);
                var fill = band.gameObject.AddComponent<LayoutElement>();
                fill.preferredHeight = shelfH; fill.preferredWidth = areaW; fill.flexibleWidth = 1f;

                // The plank: a board with a lit front edge, drawn at the sheet's own grain.
                // The niche (2026-08-01, drawn in code): the shelf above throws a shadow into
                // the top of the cell, the floor is a perspective trapezoid lighter at its
                // front edge, and a lit lip hangs under it — depth from geometry, not a picture.
                var nicheShadow = NewRect("NicheShadow", band);
                nicheShadow.anchorMin = new Vector2(0.02f, 1); nicheShadow.anchorMax = new Vector2(0.98f, 1);
                nicheShadow.pivot = new Vector2(0.5f, 1);
                nicheShadow.sizeDelta = new Vector2(0, 34);
                nicheShadow.anchoredPosition = Vector2.zero;
                var ns = nicheShadow.gameObject.AddComponent<Image>();
                ns.sprite = BackBarArt.NicheTop(); ns.raycastTarget = false;

                // The plank grew a FACE (the author): a thick front board with a brass
                // edge, tall enough to carry the bottle names — the label is furniture now.
                var face = NewRect("Face", band);
                face.anchorMin = new Vector2(0.02f, 0); face.anchorMax = new Vector2(0.98f, 0);
                face.pivot = new Vector2(0.5f, 0);
                face.offsetMin = Vector2.zero; face.offsetMax = new Vector2(0, ShelfFaceH);
                var faceImg = face.gameObject.AddComponent<Image>();
                faceImg.sprite = BackBarArt.ShelfFace();
                faceImg.type = Image.Type.Tiled;
                faceImg.raycastTarget = false;

                var plank = NewRect("Plank", band);
                plank.anchorMin = new Vector2(0.02f, 0); plank.anchorMax = new Vector2(0.98f, 0);
                plank.pivot = new Vector2(0.5f, 0);
                plank.offsetMin = new Vector2(0, ShelfFaceH - 2f);
                plank.offsetMax = new Vector2(0, ShelfFaceH + 26f);
                var plankImg = plank.gameObject.AddComponent<Image>();
                plankImg.sprite = BackBarArt.ShelfFloor();
                plankImg.raycastTarget = false;


                // Centred on the plank rather than packed to the left: a shelf with two bottles
                // on it is a shelf with two bottles on it, not a row that ran out.
                //
                // A row is packed by what each bottle IS DRAWN as, not by the sheet its art
                // arrived on (2026-08-11): the juice cartons carry a wide margin of nothing
                // around them, and a row packed by sheets left those four standing in gaps of
                // their own air, each at a different size. VesselArt measures the drawing.
                float artH = shelfH - ShelfFaceH + BottleRise;
                var wide = new float[Mathf.Max(count, 1)];
                float run0 = 0f;
                for (int i = 0; i < count; i++)
                {
                    var sprite = BottleArt.For(items[from + i].Ingredient);
                    var drawn = VesselArt.StandSize(sprite, artH);
                    wide[i] = drawn.x > 0f ? drawn.x : artH * 0.5f;
                    run0 += wide[i];
                }
                float span = run0 + BottleGap * Mathf.Max(0, count - 1);
                float roomW = areaW * 0.96f;
                float k = span > roomW && span > 0f ? roomW / span : 1f;   // the row shrinks whole
                artH *= k;
                float x = -span * k * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    float w = wide[i] * k;
                    AddShelfBottle(band, items[from + i], run, x + w * 0.5f,
                        w + BottleGap * k, shelfH, w, artH);
                    x += w + BottleGap * k;
                }
            }

            BuildKegRow(run, kegs);

            if (items.Count == 0 && kegs.Count == 0)
            {
                var none = (NewText("Empty", _bottleList, _body, 8,
                    TextAnchor.MiddleCenter, InkSoft));
                Stretch(none.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                none.text = "nothing on this shelf";
            }
        }

        /// <summary>
        /// The kegs, at keg scale (the author): real barrels standing on the cellar floor,
        /// only about a fifth of each in frame at the bottom edge — the new top-perspective
        /// keg art, since the straight-on keg could not sit on a floor the eye looks down
        /// at. Clicking a crown opens the tap exactly as the old shelf bottle did.
        /// </summary>
        private void BuildKegRow(TycoonRun run, List<ShelfBottle> kegs)
        {
            if (_kegRow == null) return;
            foreach (Transform child in _kegRow) Destroy(child.gameObject);
            var art = ItemArt.Load("keg_persp");
            if (art == null) art = BackBarArt.KegCrown();   // no generated keg yet: the drawn one
            const float KegW = 160f, KegH = 250f, Visible = 0.5f;
            float xL = -_menuPanel.rect.width * 0.5f + 150f, xR = -190f;
            float step = kegs.Count > 1 ? Mathf.Min(240f, (xR - xL) / (kegs.Count - 1)) : 0f;
            for (int i = 0; i < kegs.Count; i++)
            {
                var keg = kegs[i];
                var card = keg.Ingredient;
                bool empty = keg.IsEmpty;
                string blocked = empty ? "OUT"
                    : (run.CanPull(card.Id) ? null : run.ServingGlass.IsFull ? "FULL" : "BUSY");
                bool shut = blocked != null;

                var slot = NewRect($"Keg_{card.Id}", _kegRow);
                Place(slot, new Vector2(0.5f, 0), new Vector2(KegW, KegH * Visible + 26f),
                    new Vector2(xL + step * i, 0f));
                slot.pivot = new Vector2(0.5f, 0);
                var hit = slot.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0.001f);

                var kegArt = NewRect("Art", slot);
                Place(kegArt, new Vector2(0.5f, 0), new Vector2(KegW, KegH),
                    new Vector2(0, -KegH * (1f - Visible)));
                kegArt.pivot = new Vector2(0.5f, 0);
                var img = kegArt.gameObject.AddComponent<Image>();
                if (art != null) { img.sprite = art; img.preserveAspect = true; }
                else img.color = new Color(0.55f, 0.57f, 0.60f);
                img.raycastTarget = false;
                if (shut) img.color = new Color(1f, 1f, 1f, 0.45f);

                // the brand, lettered across the crown
                var name = Outlined(NewText("N", slot, _body, 8, TextAnchor.MiddleCenter,
                    shut ? UITheme.Cream[2] : UITheme.Cream[4]), 1f);
                Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(KegW - 20f, 14f),
                    new Vector2(0, KegH * Visible + 8f));
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                name.text = shut ? $"{card.Name} · {blocked}" : card.Name;

                Pressable(slot, kegArt, img, lift: shut ? 2f : 5f, depth: shut ? 0f : 5f);
                var trigger = slot.gameObject.AddComponent<EventTrigger>();
                var kb = keg;
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => ShowBottleInfo(kb, run, slot));
                trigger.triggers.Add(enter);
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => HideBottleInfo());
                trigger.triggers.Add(exit);
                if (shut) continue;
                var c = card;
                var press = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                press.callback.AddListener(_ => { HideBottleInfo(); OpenBottle(c); });
                trigger.triggers.Add(press);
            }
        }

        /// <summary>
        /// One bottle standing on the shelf. The whole slot is the hit target -- a bottle is a
        /// narrow silhouette and asking the player to hit the glass itself would be a precision
        /// test nobody signed up for. Hover raises the info panel; a bottle that cannot be used
        /// says why on itself instead of opening a stage that would refuse it.
        /// </summary>
        private void AddShelfBottle(RectTransform band, ShelfBottle bottle, TycoonRun run,
            float centreX, float slotW, float shelfH, float artW, float artH)
        {
            var card = bottle.Ingredient;
            bool empty = bottle.IsEmpty;
            // Each bottle is judged against the vessel IT pours into. Beer fills the
            // serving glass off the tap; fizz fills it at the counter (its only door,
            // GDD 21 §12) — and since 2026-08-13 the fizz stands on this wall, where it
            // was being greyed out by a FULL TIN it can never reach. Everything else
            // builds in the tin.
            bool glassSide = card.Info != null && card.Info.Carbonated;
            string blocked =
                empty ? "OUT"
                : card.Type == IngredientType.Beer
                    ? (run.CanPull(card.Id) ? null : run.ServingGlass.IsFull ? "FULL" : "BUSY")
                    : glassSide
                        ? (run.ServingGlass.IsFull ? "FULL" : null)
                        : (run.Glass.IsFull ? "FULL" : null);
            bool shut = blocked != null;

            var sprite = ItemArt.Bottle(card);
            // How big this bottle actually comes out at the height this shelf offers. The
            // wide ones (the cartons) stand a little shorter for it — see VesselArt.MaxAspect.
            var drawn = VesselArt.StandSize(sprite, artH);
            if (drawn.y <= 0f) drawn = new Vector2(artW, artH);

            // The hit plate is the BOTTLE's own size now, not a share of the plank: with the
            // row packed tight, a slot wider than its art would reach across its neighbour
            // and the two would trade hovers.
            var slot = NewRect($"Slot_{card.Id}", band);
            Place(slot, new Vector2(0.5f, 0), new Vector2(slotW - 4f, drawn.y),
                new Vector2(centreX, ShelfFaceH + BottleFoot));
            var hit = slot.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);          // invisible, but catches the pointer

            // The ellipse that pins the bottle to the shelf's floor plane (2026-08-01). Cut to
            // the drawing's width, so a bottle standing in a wide sheet does not throw a
            // shadow out past its own glass.
            var shadow = NewRect("Shadow", slot);
            shadow.anchorMin = shadow.anchorMax = new Vector2(0.5f, 0);
            shadow.pivot = new Vector2(0.5f, 0.5f);
            shadow.sizeDelta = new Vector2(drawn.x * 0.92f, 12);
            shadow.anchoredPosition = new Vector2(0, 14);
            var shImg = shadow.gameObject.AddComponent<Image>();
            shImg.sprite = BackBarArt.BottleShadow(); shImg.raycastTarget = false;

            // Feet ON the plank (the author: bottles must centre on the shelf's depth):
            // preserveAspect centres vertically inside its rect, which floated short
            // bottles above the wood — so the rect is cut to the art's own aspect and
            // pinned by its base to the plank's mid-depth. The size is the SHELF's
            // decision (see BuildShelfPage), and VesselArt turns that height into a rect
            // by measuring the DRAWING: whatever air the sheet was saved with is taken up
            // by the rect, never by the bottle, so the feet land on the wood either way.
            var body = NewRect("Bottle", slot);
            VesselArt.StandOn(body, new Vector2(0.5f, 0f), sprite, artH,
                new Vector2(0, BottleStand));

            // What is left, drawn behind the glass and cut out by it — see BottleFill for
            // why it is a stencil and not a rect (2026-08-11, the author: "şişeler boş
            // görünüyor"). Built BEFORE the art so the art draws over it.
            BottleFill.Under(body).Show(
                sprite, UITheme.LiquidColor(card.Info?.Style, card.Type),
                bottle.Capacity > 0 ? bottle.Remaining / bottle.Capacity : 0.0,
                shut ? 0.38f : 1f);

            var art = NewRect("Art", body);
            Stretch(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = art.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true; img.raycastTarget = false;
            img.color = img.sprite == null
                ? UITheme.StyleColor(card.Info?.Style, card.Type)
                : (shut ? new Color(1f, 1f, 1f, 0.38f) : Color.white);

            // A little SIGN under each bottle (the author): a brass-framed plate sized to
            // its own name, pinned to the shelf face — a tabela, not floating text. Sized to
            // the name but CAPPED at the slot, because a plate that outgrows its slot lies
            // across its neighbour's and the two names print through each other (the author,
            // 2026-08-03: "yazılar birbiriyle giriyor"). A name that will not fit is cut with
            // a visible ".." — the hover card carries it whole, so nothing is lost, and two
            // plates can no longer meet by construction.
            //
            // THE PLATE SAYS WHAT IT IS, NOT WHOSE IT IS (2026-08-11). Packing the row by the
            // bottles' own widths left a plate about 45 units wide under a slim bottle, and
            // "SMIRKOFF VODKA" set at 8 needs three times that — so the names printed
            // straight through each other again, the very failure the cap was written for.
            // The brand is on the hover card; the plate carries the STYLE, which is both
            // short enough to fit and the word the recipes are written in ("GIN 45–65%"), so
            // the shelf and the book finally speak the same language. A plate that still
            // cannot fit four characters is not drawn at all.
            string label = shut ? blocked : ShelfWord(card);
            const float Em = 5.4f;                 // Silkscreen at 8, measured off the shelf
            float maxPlateW = slotW - 2f;
            int fits = Mathf.FloorToInt((maxPlateW - 12f) / Em);
            if (fits >= 4)                         // under 4 characters a plate says nothing
            {
                if (label.Length > fits) label = label.Substring(0, fits - 2).TrimEnd() + "..";
                float plateW = Mathf.Min(label.Length * Em + 12f, maxPlateW);
                var plate = NewRect("Plate", band);
                Place(plate, new Vector2(0.5f, 0), new Vector2(plateW, 20f),
                    new Vector2(centreX, ShelfFaceH * 0.5f - 2f));
                var plateImg = plate.gameObject.AddComponent<Image>();
                plateImg.sprite = BackBarArt.NamePlate();
                plateImg.type = Image.Type.Sliced;
                plateImg.raycastTarget = false;
                if (shut) plateImg.color = new Color(1f, 1f, 1f, 0.7f);
                var name = Outlined(NewText("N", plate, _body, 8, TextAnchor.MiddleCenter,
                    shut ? UITheme.Cream[2] : UITheme.Cream[4]), 1f);
                Stretch(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                name.verticalOverflow = VerticalWrapMode.Overflow;
                name.raycastTarget = false;
                name.text = label;
            }

            // The bottle answers the pointer whether or not it can be taken: a bottle that is OUT
            // still lifts, because "you found the thing" and "the thing will do something" are two
            // different answers and the player needs the first one to trust the shelf at all.
            Pressable(slot, body, img, lift: shut ? 2f : HoverLift, depth: shut ? 0f : 5f);

            var trigger = slot.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowBottleInfo(bottle, run, slot));
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideBottleInfo());
            trigger.triggers.Add(exit);

            if (shut) return;

            var press = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            press.callback.AddListener(_ => { HideBottleInfo(); OpenBottle(card); });
            trigger.triggers.Add(press);

            var sink = slot.gameObject.AddComponent<PressSink>();
            sink.Face = body; sink.Depth = 3f; sink.Squash = 0.01f;
        }

        /// <summary>What is left in the bottle and what it costs, raised beside it on hover
        /// (v5 P13). Built once and moved, so hovering along a shelf does not churn objects.</summary>
        private void ShowBottleInfo(ShelfBottle bottle, TycoonRun run, RectTransform near)
        {
            if (_bottleInfo == null) BuildBottleInfo();
            var card = bottle.Ingredient;

            _bottleInfoName.text = card.Name.ToUpperInvariant();
            double left = bottle.Capacity > 0 ? bottle.Remaining / bottle.Capacity : 0;
            _bottleInfoStock.text = bottle.IsEmpty
                ? "EMPTY"
                : $"{left:P0} left  ·  {bottle.Remaining:0.#} of {bottle.Capacity:0.#}";
            int price = card.Info?.Price ?? 0;
            _bottleInfoPrice.text = price > 0 ? $"restock ${price}" : "house pour";
            _bottleInfoFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)left), 1f);
            _bottleInfoFill.color = bottle.IsEmpty ? UITheme.ViceRed[3]
                : left < 0.25 ? UITheme.Amber[3] : UITheme.Lime[3];

            // Hung UNDER the bottle (the author, 2026-08-02): above it, the card covered the
            // top of the very bottle it was describing. Positioned in the PANEL's space, not
            // the sheet's: the sheet lays its children out vertically, so a panel parented
            // there is treated as another row -- it lost its size, its backing plate and its
            // place by the bottle all at once.
            var panel = _bottleInfo;
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            float half = near.rect.height * 0.5f;
            var below = (Vector2)_menuPanel.InverseTransformPoint(
                near.TransformPoint(new Vector3(0, -half - 10f, 0)));
            var above = (Vector2)_menuPanel.InverseTransformPoint(
                near.TransformPoint(new Vector3(0, half + 10f, 0)));

            float halfBoard = _menuPanel.rect.width * 0.5f;
            float x = Mathf.Clamp(below.x, -halfBoard + panel.rect.width * 0.5f + 8f,
                halfBoard - panel.rect.width * 0.5f - 8f);

            // The bottom shelf has no room underneath, so the card goes back over the bottle
            // there and its spout turns to follow.
            bool room = below.y - panel.rect.height > -_menuPanel.rect.height * 0.5f + 8f;
            panel.anchoredPosition = new Vector2(x, room ? below.y : above.y + panel.rect.height);
            if (_bottleInfoTail != null)
            {
                _bottleInfoTail.anchorMin = _bottleInfoTail.anchorMax =
                    new Vector2(0.5f, room ? 1f : 0f);
                _bottleInfoTail.anchoredPosition = new Vector2(0, room ? -2f : 2f);
                _bottleInfoTail.localScale = new Vector3(1f, room ? 1f : -1f, 1f);
            }
        }

        private void HideBottleInfo()
        {
            if (_bottleInfo != null) _bottleInfo.gameObject.SetActive(false);
        }

        private void BuildBottleInfo()
        {
            _bottleInfo = NewRect("BottleInfo", _menuPanel);
            _bottleInfo.anchorMin = _bottleInfo.anchorMax = new Vector2(0.5f, 0.5f);
            // Hangs DOWNWARD from its anchor, because the anchor is now the foot of the
            // bottle rather than its shoulder.
            _bottleInfo.pivot = new Vector2(0.5f, 1f);
            _bottleInfo.sizeDelta = new Vector2(184, 68);
            var bg = _bottleInfo.gameObject.AddComponent<Image>();
            bg.sprite = BackBarArt.InfoPlate();
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;

            // The spout, pointing back up at the bottle the card is talking about.
            var tail = NewRect("Tail", _bottleInfo);
            tail.anchorMin = tail.anchorMax = new Vector2(0.5f, 1f);
            tail.pivot = new Vector2(0.5f, 0f);
            tail.sizeDelta = new Vector2(13, 9);
            tail.anchoredPosition = new Vector2(0, -2);
            var tailImg = tail.gameObject.AddComponent<Image>();
            tailImg.sprite = BackBarArt.InfoTail();
            tailImg.raycastTarget = false;
            _bottleInfoTail = tail;

            // Two lines' worth: a name like REDLINE BOURBON WHISKEY wraps, and the second
            // line used to land on the stock figure.
            _bottleInfoName = NewText("N", _bottleInfo, _display, 8, TextAnchor.UpperCenter,
                UITheme.Cream[4]);
            Place(_bottleInfoName.rectTransform, new Vector2(0.5f, 1), new Vector2(176, 22),
                new Vector2(0, -7));
            _bottleInfoStock = NewText("S", _bottleInfo, _body, 8, TextAnchor.UpperCenter,
                UITheme.Cream[3]);
            Place(_bottleInfoStock.rectTransform, new Vector2(0.5f, 1), new Vector2(176, 12),
                new Vector2(0, -31));
            _bottleInfoPrice = NewText("P", _bottleInfo, _body, 8, TextAnchor.UpperCenter,
                UITheme.Money);
            Place(_bottleInfoPrice.rectTransform, new Vector2(0.5f, 1), new Vector2(176, 12),
                new Vector2(0, -54));

            var track = NewRect("Track", _bottleInfo);
            Place(track, new Vector2(0.5f, 1), new Vector2(160, 4), new Vector2(0, -46));
            track.gameObject.AddComponent<Image>().color = UITheme.Night[3];
            var fill = NewRect("Fill", track);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(1, 1);
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            _bottleInfoFill = fill.gameObject.AddComponent<Image>();
            _bottleInfoFill.raycastTarget = false;

            _bottleInfo.gameObject.SetActive(false);
        }

        private const float ShelfFaceH = 34f;

        /// <summary>How far a bottle stands ABOVE its own band, in front of the niche
        /// overhead. The mask carries the same amount as top padding (see ListTopPad), so
        /// the top shelf keeps its heads.</summary>
        private const float BottleRise = 10f;

        /// <summary>The air between two bottles standing on the same plank. Small on
        /// purpose: a back bar is crowded, and the shoulders nearly touching is what makes
        /// it read as stock rather than as a display.</summary>
        private const float BottleGap = 10f;

        /// <summary>How high a bottle's foot stands above the shelf face — the plank has
        /// depth, and a bottle stands on the middle of it rather than on its front edge.</summary>
        private const float BottleFoot = 14f;

        /// <summary>The last two pixels of stand-off between the slot and the art itself.</summary>
        private const float BottleStand = 2f;

        /// <summary>How far the pointer raises a bottle out of the row.</summary>
        private const float HoverLift = 5f;

        /// <summary>Mask headroom above the first shelf band, so the top row's bottles keep
        /// their heads.
        ///
        /// DERIVED, NOT CHOSEN (2026-08-11, the author: "backbarın en üstündeki alkollerin
        /// en üstü kesiliyor"). This was 16, written when a bottle overhung its band by
        /// about ten pixels. It does not any more: the art grew to <c>shelfH − ShelfFaceH +
        /// BottleRise</c> while its foot stayed at <c>ShelfFaceH + BottleFoot</c>, so the
        /// real overhang is <c>BottleFoot + BottleStand + BottleRise</c> — twenty-six — and
        /// the pointer adds another five on top of that. Ten pixels of every top-row bottle
        /// were being clipped, which on a tall bottle is exactly its cap.
        ///
        /// So the number is no longer a guess: it is the sum of the four things that lift a
        /// bottle above its band, plus two pixels of air — measured in play at 11px of
        /// overhang against 10 predicted, which is close enough to want the rounding to have
        /// somewhere to go.</summary>
        private const float ListTopPad = BottleFoot + BottleStand + BottleRise + HoverLift + 2f;
        private RectTransform _kegRow;
        private RectTransform _bottleInfo;
        private Button _serveButton;
        private Text _serveLabel;
        private Text _bottleInfoName, _bottleInfoStock, _bottleInfoPrice;
        private RectTransform _bottleInfoTail;
        private Image _bottleInfoFill;

        private static readonly Color InkSoft = new Color(0.34f, 0.30f, 0.26f, 1f);

        private void BuildMenuPanel()
        {
            // THE BACK BAR (the author's direction, 2026-07-31): the clipboard retires. The
            // player turns around to face the wall of bottles — a full-screen back-bar, every
            // bottle on one wall, no aisles. The wall is the generated kit at native grain:
            // tiled wood behind, the lit art-deco cornice across the top.
            _menuPanel = NewRect("MenuPanel", _field);
            Stretch(_menuPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var boardImg = _menuPanel.gameObject.AddComponent<Image>();
            // Drawn in code (2026-08-01): the generated tile carried a baked frame that
            // repeated as a white grid across the wall. BackBarArt's boards seam at their
            // own edges, so the tiling is invisible by construction.
            boardImg.sprite = BackBarArt.LuxeWall();
            boardImg.type = Image.Type.Tiled;
            boardImg.pixelsPerUnitMultiplier = 0.5f;   // one art pixel = 2 screen px, the scene's grain
            Swallow(_menuPanel);

            // NEON, not timber (the author, 2026-08-02: the board sign, the ivy and the
            // framed paintings all read as the wrong decade for a vice bar). The name is
            // a magenta neon word with a soft halo — the stage sign's own voice.
            var glow = (NewText("SignGlow", _menuPanel, _display, 24, TextAnchor.MiddleCenter,
                new Color(UITheme.Magenta[3].r, UITheme.Magenta[3].g, UITheme.Magenta[3].b, 0.35f)));
            Place(glow.rectTransform, new Vector2(0.5f, 1), new Vector2(420, 44), new Vector2(0, -46f));
            glow.rectTransform.localScale = new Vector3(1.06f, 1.2f, 1f);
            glow.text = "LAST CALL";
            glow.raycastTarget = false;

            // A red X at the cornice's right end closes the whole flow.
            var close = NewRect("Close", _menuPanel);
            Place(close, new Vector2(1, 1), new Vector2(52, 52), new Vector2(-16, -30));
            var closeImg = close.gameObject.AddComponent<Image>();
            var closeSprite = ItemArt.Load("btn_close");
            if (closeSprite != null) { closeImg.sprite = closeSprite; closeImg.preserveAspect = true; closeImg.color = Color.white; }
            else closeImg.color = new Color(0.62f, 0.15f, 0.17f);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(CloseFlow);
            GiveKeyPress(close, closeBtn, closeImg, "btn_close_down");
            if (closeSprite == null)
            {
                var closeX = NewText("X", close, _display, 18, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.90f));
                Stretch(closeX.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                closeX.text = "X";
            }

            // The title sits ON the cornice, part of the architecture rather than floating
            // over the bottles.
            var title = _menuTitle = (NewText("Title", _menuPanel, _display, 24, TextAnchor.MiddleCenter, UITheme.Magenta[4]));
            var outline = title.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.16f, 0.09f, 0.04f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(420, 40), new Vector2(0, -46f));
            title.text = "LAST CALL";

            // Left: a SCROLLABLE back-shelf of grouped item boxes — it grows as you buy more
            // stock without overflowing the panel (2026-07-23 fix).
            // One grid on the paper, never a scrollbar: the cell size is recomputed from the
            // stock count in RefreshMenu, so a growing bar packs tighter instead of scrolling.
            // The page slides off the paper on a change, so it runs inside a frame that stays put
            // and clips it. The mask has to be on the frame, not on the page: put it on the thing
            // that moves and it travels with the keys and clips nothing at all.
            // Short enough to leave the bottom strip to SERVE and the bin: with four columns the
            // grid reaches the paper's right edge, and a full-height page put its last key under
            // the bin (2026-07-27).
            // The counter ledge along the bottom: the same floor plane as the shelves,
            // taller — SERVE and the bin STAND on it instead of floating on the wall.
            var ledge = NewRect("Ledge", _menuPanel);
            ledge.anchorMin = new Vector2(0, 0); ledge.anchorMax = new Vector2(1, 0);
            ledge.pivot = new Vector2(0.5f, 0);
            ledge.sizeDelta = new Vector2(0, 88);
            ledge.anchoredPosition = Vector2.zero;
            var ledgeImg = ledge.gameObject.AddComponent<Image>();
            ledgeImg.sprite = BackBarArt.Ledge();
            ledgeImg.raycastTarget = false;

            // The keg strip lives between the ledge and the buttons: kegs poke up from the
            // bottom edge, SERVE and the bin keep the top of the pile.
            _kegRow = NewRect("Kegs", _menuPanel);
            _kegRow.anchorMin = new Vector2(0, 0); _kegRow.anchorMax = new Vector2(1, 0);
            _kegRow.pivot = new Vector2(0.5f, 0);
            _kegRow.sizeDelta = new Vector2(0, 130);
            _kegRow.anchoredPosition = Vector2.zero;

            // The wall's working area: full width under the cornice, above the ledge.
            // The mask's top edge reaches HIGHER than the first shelf band does, because a
            // bottle rises ABOVE its band by construction — the slot stands at 48 and the art
            // is shelfH−40 tall, so the top of the glass overhangs the band by ~10px. On the
            // middle shelves that overhang leans in front of the niche above, which reads as
            // depth; on the TOP shelf it used to hit the mask and the author saw beheaded
            // bottles (2026-08-05). The list gets the same amount as top padding, so the
            // bands themselves stand exactly where they always did.
            var pageClip = NewRect("PageClip", _menuPanel);
            Stretch(pageClip, Vector2.zero, Vector2.one, new Vector2(40, 92), new Vector2(-40, -102));
            pageClip.gameObject.AddComponent<RectMask2D>();

            // THE KEGS STAND IN FRONT OF THE WALL (2026-08-11, the author: "bira fıçıları
            // en alttaki rafın altında kalıyor"). They are built before the shelves so the
            // ledge can be built before them, and UGUI draws siblings in order — so the
            // bottom plank was printing over the crowns of barrels standing on the floor in
            // front of it. A keg is nearer the camera than the wall is; it draws later.
            _kegRow.SetAsLastSibling();

            _bottleList = NewRect("Bottles", pageClip);
            Stretch(_bottleList, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _listHome = _bottleList.anchoredPosition;
            var listLayout = _bottleList.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(0, 0, (int)ListTopPad, 0);
            listLayout.spacing = GridGap; listLayout.childControlHeight = true;
            listLayout.childControlWidth = true; listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childAlignment = TextAnchor.UpperLeft;

            // SERVE stands on the ledge, centred.
            var actions = NewRect("Actions", _menuPanel);
            Place(actions, new Vector2(0.5f, 0), new Vector2(212, 40), new Vector2(0, 28));
            var actLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actLayout.childControlWidth = true; actLayout.childForceExpandWidth = true;
            actLayout.childControlHeight = true; actLayout.childForceExpandHeight = true;
            // Gated again, on the RIGHT condition this time (the author, 2026-08-01): the
            // old guard asked for something in the shaker and starved the six built drinks;
            // this one asks for something poured ANYWHERE — tin or glass — because the pass
            // stage with two empty vessels is a dead end dressed as a button.
            // The SERVE key is the wall's other door onto the counter, and it carried the
            // same hole the fizz did (2026-08-14): it lit on "something poured anywhere",
            // which includes a tin that was never capped. It goes to the bench instead
            // while the bench is owed something, and says so on arrival.
            _serveButton = AddFlexButton(actions, "SERVE", UITheme.PrimaryAction, () =>
            {
                if (BenchUnfinished(Run)) { DemandBench(BenchOwed(Run).ToUpperInvariant()); return; }
                GoTo(Stage.Serve);
            });
            _serveLabel = _serveButton.GetComponentInChildren<Text>();

            AddBinButton(_menuPanel);

            // The BOOK beside the bin (the author): the recipes are needed most mid-build.
            var bookRt = NewRect("BookBtn", _menuPanel);
            Place(bookRt, new Vector2(1, 0), new Vector2(56, 56), new Vector2(-214f, 26f));
            var bookImg = bookRt.gameObject.AddComponent<Image>();
            var bookSprite = ItemArt.Load("menu_board");
            if (bookSprite != null) { bookImg.sprite = bookSprite; bookImg.preserveAspect = true; }
            else bookImg.color = UITheme.Night[3];
            var bookBtn = bookRt.gameObject.AddComponent<Button>();
            bookBtn.targetGraphic = bookImg;
            bookBtn.onClick.AddListener(() => GetComponent<TycoonHud>()?.ToggleRecipeBook());
            var bookSink = bookRt.gameObject.AddComponent<PressSink>();
            bookSink.Face = bookRt; bookSink.Depth = 4f; bookSink.Lift = 2f;
        }

    }
}
