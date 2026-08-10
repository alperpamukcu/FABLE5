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
        /// Whether a bottle belongs on the BACK BAR at all (the author, 2026-08-02: there is
        /// no sense keeping bottles behind the bar that can never go in the shaker). The wall
        /// holds what a drink is BUILT from at the tin; the fizz that Core refuses in the
        /// shaker, and the garnishes that go on at the glass, live in the serve stage where
        /// they are actually used. Beer stays: its kegs are drawn on the floor below the wall.
        /// </summary>
        private static bool OnTheBackBar(IngredientCard card)
        {
            if (card.Type == IngredientType.Beer) return true;
            if (card.Type == IngredientType.Garnish) return false;
            return card.Info == null || !card.Info.Carbonated;
        }

        /// <summary>Everything written on the sheet is slanted — the closest a pixel face gets
        /// to a hand that scrawled the list out behind the bar.</summary>
        private static Text Handwritten(Text t)
        {
            // The pixel faces have no true italic, so Unity fakes it by shearing the glyphs —
            // which read as broken rather than hand-written. Upright it is.
            t.fontStyle = FontStyle.Normal;
            return t;
        }

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
            if (_serveButton != null)
            {
                _serveButton.interactable = loaded;
                if (_serveLabel != null)
                {
                    _serveLabel.text = loaded ? "SERVE  →" : "POUR FIRST";
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

            int perRow = Mathf.Max(7, Mathf.CeilToInt(items.Count / 3f));
            int shelves = Mathf.Max(3, Mathf.CeilToInt(items.Count / (float)perRow));
            // The top padding is headroom for the first shelf's overhanging bottles and
            // must come out of the bands' budget, or the bottom band spills off the page.
            float shelfH = (areaH - GridGap - ListTopPad) / shelves;

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
                float slotW = (areaW * 0.96f) / perRow;
                float startX = -slotW * count * 0.5f;
                for (int i = 0; i < count; i++)
                    AddShelfBottle(band, items[from + i], run,
                        startX + slotW * (i + 0.5f), slotW, shelfH);
            }

            BuildKegRow(run, kegs);

            if (items.Count == 0 && kegs.Count == 0)
            {
                var none = Handwritten(NewText("Empty", _bottleList, _body, 8,
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
            float centreX, float slotW, float shelfH)
        {
            var card = bottle.Ingredient;
            bool empty = bottle.IsEmpty;
            string blocked =
                empty ? "OUT"
                : card.Type == IngredientType.Beer
                    ? (run.CanPull(card.Id) ? null : run.ServingGlass.IsFull ? "FULL" : "BUSY")
                    : (run.Glass.IsFull ? "FULL" : null);
            bool shut = blocked != null;

            var slot = NewRect($"Slot_{card.Id}", band);
            Place(slot, new Vector2(0.5f, 0), new Vector2(slotW - 6f, shelfH - ShelfFaceH - 20f),
                new Vector2(centreX, ShelfFaceH + 14f));
            var hit = slot.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);          // invisible, but catches the pointer

            // The ellipse that pins the bottle to the shelf's floor plane (2026-08-01).
            var shadow = NewRect("Shadow", slot);
            shadow.anchorMin = shadow.anchorMax = new Vector2(0.5f, 0);
            shadow.pivot = new Vector2(0.5f, 0.5f);
            shadow.sizeDelta = new Vector2(slotW * 0.62f, 12);
            shadow.anchoredPosition = new Vector2(0, 14);
            var shImg = shadow.gameObject.AddComponent<Image>();
            shImg.sprite = BackBarArt.BottleShadow(); shImg.raycastTarget = false;

            // Feet ON the plank (the author: bottles must centre on the shelf's depth):
            // preserveAspect centres vertically inside its rect, which floated short
            // bottles above the wood — so the rect is cut to the art's own aspect and
            // pinned by its base to the plank's mid-depth.
            var piece0 = BottleArt.For(card);
            // 24 → 6 of headroom (the author, 2026-08-05: "oyun içi şişenin boyutunu
            // büyüt") — the v3 masters are slim, and the height is the only axis a
            // slim bottle can grow on.
            float artH = shelfH - ShelfFaceH - 6f, artW = slotW - 12f;
            if (piece0.Exists && piece0.Aspect > 0f)
            {
                if (artH * piece0.Aspect <= artW) artW = artH * piece0.Aspect;
                else artH = artW / piece0.Aspect;
            }
            // WHAT IS LEFT IN THE BOTTLE, BACK IN THE BOTTLE (2026-08-10). This is the rebuild
            // BottleArt's sweep left room for ("when that art lands, the liquid layer gets
            // rebuilt against it"), not a resurrection of the plate machinery it removed: the
            // pilot glass is genuinely see-through, so the drink is drawn BEHIND the sprite —
            // an EARLIER SIBLING in the same rect, so it renders first — and the glass covers
            // it for free. No back plate, no film, no label to protect.
            // Drink and glass live in ONE rect, and it is that rect the pointer lifts.
            // They used to be siblings under the slot with only the art handed to
            // Pressable, so hovering raised the bottle and left its contents standing
            // where they were (the author, 2026-08-10: "sıvı sabit kalıyor, şişe ön
            // plana çıkıyor"). Anything that moves a bottle has to move what is in it.
            var body = NewRect("Bottle", slot);
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0);
            body.pivot = new Vector2(0.5f, 0);
            body.sizeDelta = new Vector2(artW, artH);
            body.anchoredPosition = new Vector2(0, 2f);

            var wet = NewRect("Fluid", body);
            Stretch(wet, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fluid = wet.gameObject.AddComponent<BottleFluid>();
            fluid.raycastTarget = false;
            var drink = UITheme.StyleColor(card.Info?.Style, card.Type);
            fluid.color = new Color(drink.r, drink.g, drink.b, shut ? 0.42f : 0.92f);
            fluid.Bind(ItemArt.Bottle(card), card.Id);
            fluid.SetLevel(bottle.Capacity > 0.0
                ? (float)(bottle.Remaining / bottle.Capacity)
                : 0f);

            var art = NewRect("Art", body);
            Stretch(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = art.gameObject.AddComponent<Image>();
            img.sprite = ItemArt.Bottle(card);
            img.preserveAspect = true; img.raycastTarget = false;
            img.color = img.sprite == null
                ? UITheme.StyleColor(card.Info?.Style, card.Type)
                : (shut ? new Color(1f, 1f, 1f, 0.38f) : Color.white);

            // The hover card still SAYS what is left; the bottle now shows it.

            // A little SIGN under each bottle (the author): a brass-framed plate sized to
            // its own name, pinned to the shelf face — a tabela, not floating text. Sized to
            // the name but CAPPED at the slot, because a plate that outgrows its slot lies
            // across its neighbour's and the two names print through each other (the author,
            // 2026-08-03: "yazılar birbiriyle giriyor"). A name that will not fit is cut with
            // a visible ".." — the hover card carries it whole, so nothing is lost, and two
            // plates can no longer meet by construction.
            string label = shut ? $"{card.Name} · {blocked}" : card.Name;
            float maxPlateW = slotW - 4f;
            int fits = Mathf.FloorToInt((maxPlateW - 18f) / 7f);
            if (label.Length > fits && fits > 4)
                label = label.Substring(0, fits - 2).TrimEnd() + "..";
            float plateW = Mathf.Min(label.Length * 7f + 18f, maxPlateW);
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
            name.raycastTarget = false;
            name.text = label;

            // The bottle answers the pointer whether or not it can be taken: a bottle that is OUT
            // still lifts, because "you found the thing" and "the thing will do something" are two
            // different answers and the player needs the first one to trust the shelf at all.
            Pressable(slot, body, img, lift: shut ? 2f : 5f, depth: shut ? 0f : 5f);

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

        /// <summary>Mask headroom above the first shelf band, so the top row's bottles —
        /// which overhang their band by ~10px like every row's do — keep their heads.</summary>
        private const float ListTopPad = 16f;
        private RectTransform _kegRow;
        private static readonly Color ShelfWood = new Color(0.30f, 0.19f, 0.12f, 1f);
        private static readonly Color ShelfLip = new Color(0.46f, 0.31f, 0.19f, 1f);
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
            _menuPanel = NewRect("MenuPanel", _root);
            Stretch(_menuPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _menuHome = _menuPanel.anchoredPosition;
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
            var glow = Handwritten(NewText("SignGlow", _menuPanel, _display, 24, TextAnchor.MiddleCenter,
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
            var title = _menuTitle = Handwritten(NewText("Title", _menuPanel, _display, 24, TextAnchor.MiddleCenter, UITheme.Magenta[4]));
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
            _serveButton = AddFlexButton(actions, "SERVE  →", UITheme.PrimaryAction, () => GoTo(Stage.Serve));
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
