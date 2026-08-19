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
        /// and their jars stand on the benches.
        ///
        /// **AND BEER LEFT ENTIRELY** (2026-08-15, the author: "backbardan biralari
        /// kaldiralim ... bira musluguna tiklanmasi gereksin"). It stood on the floor below
        /// the wall as a row of keg crowns whose only job was to open the tap station, and
        /// the room now has the real thing: a beer font on the counter that the bar owns
        /// from its opening night. A draught is poured by walking to the tap, which is what
        /// a bartender does — and it means the wall has exactly one kind of answer again:
        /// everything on it goes in the tin.
        /// </summary>
        private static bool OnTheBackBar(IngredientCard card)
        {
            return card.Type != IngredientType.Garnish && card.Type != IngredientType.Beer;
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
        // ── the plate's own geometry (2026-08-19) ──
        // "Backbar sahnesindeki arkaplan tamamen bu olacak, şişeleri raflara tam oturt."
        // Measured off backbar_pixellab.png after its (24,0,664,360) crop, in PANEL units
        // (art px ×2; y from the panel's bottom). Three niche columns, three shelf
        // boards: the stand line is each board's own top edge, and the bottles are laid
        // INTO these cells instead of onto code-drawn planks — the plate is the
        // furniture now, which retires the drawn plank/face/niche kit below.
        private static readonly float[] NicheStandY = { 484f, 296f, 116f };
        private static readonly float[] NicheHeight = { 150f, 158f, 152f };
        private static readonly Vector2[] NicheSpanX =
            { new Vector2(68f, 432f), new Vector2(476f, 850f), new Vector2(894f, 1258f) };

        private void BuildShelfPage(TycoonRun run)
        {
            // THE WHOLE WALL (2026-07-31): every bottle the bar owns on one back-bar,
            // no aisles. The hover panel still answers what each one is.
            _menuTitle.text = "LAST CALL";

            // Beer is not here at all (2026-08-15): it is poured at the font on the
            // counter, in the room. Everything else stands in the plate's nine niches,
            // top shelf first, left to right, spread as evenly as the count divides.
            var items = new List<ShelfBottle>();
            foreach (var b in run.Shelf.Bottles)
                if (OnTheBackBar(b.Ingredient)) items.Add(b);

            int cells = NicheStandY.Length * NicheSpanX.Length;
            int taken = 0;
            for (int cell = 0; cell < cells; cell++)
            {
                int row = cell / NicheSpanX.Length, col = cell % NicheSpanX.Length;
                int count = items.Count / cells + (cell < items.Count % cells ? 1 : 0);
                if (count == 0) continue;
                int from = taken; taken += count;

                float nicheL = NicheSpanX[col].x, nicheR = NicheSpanX[col].y;
                float nicheW = nicheR - nicheL;
                var band = NewRect($"Shelf{row}_{col}", _bottleList);
                band.anchorMin = band.anchorMax = new Vector2(0, 0);
                band.pivot = new Vector2(0.5f, 0);
                band.sizeDelta = new Vector2(nicheW, NicheHeight[row]);
                // The band's bottom IS the stand line: AddShelfBottle stands feet at it.
                band.anchoredPosition = new Vector2((nicheL + nicheR) * 0.5f, NicheStandY[row]);

                // A cell is packed by what each bottle IS DRAWN as (2026-08-11, unchanged):
                // VesselArt measures the drawing, the cell scales the whole group down when
                // the niche cannot take it, and nothing ever crosses a pilaster.
                float artH = NicheHeight[row] - BottleRise - HoverLift;
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
                float roomW = nicheW - 16f;
                float k = span > roomW && span > 0f ? roomW / span : 1f;   // the cell shrinks whole
                artH *= k;
                float x = -span * k * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    float w = wide[i] * k;
                    AddShelfBottle(band, items[from + i], run, x + w * 0.5f,
                        w + BottleGap * k, NicheHeight[row], w, artH);
                    x += w + BottleGap * k;
                }
            }

            if (items.Count == 0)
            {
                var none = (NewText("Empty", _bottleList, _body, 8,
                    TextAnchor.MiddleCenter, InkSoft));
                Stretch(none.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                none.text = "nothing on this shelf";
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
            // Each bottle is judged against the vessel IT pours into — and since GDD 21 §12
            // was overturned (2026-08-14) there is only one such vessel left: the TIN. Fizz
            // used to be judged against the serving glass, its old and only door, which now
            // reads a bottle as pourable while the tin it is about to go into is full, and
            // greys it out because a glass it never touches is. Beer keeps its own line: it
            // is pulled at the tap, into the serving glass.
            string blocked =
                empty ? "OUT"
                : card.Type == IngredientType.Beer
                    ? (run.CanPull(card.Id) ? null : run.ServingGlass.IsFull ? "FULL" : "BUSY")
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
            // The band's bottom IS the plate's shelf-board top (2026-08-19), so the feet
            // stand at zero - the old ShelfFaceH+BottleFoot lift belonged to the drawn kit.
            Place(slot, new Vector2(0.5f, 0), new Vector2(slotW - 4f, drawn.y),
                new Vector2(centreX, 0f));
            var hit = slot.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);          // invisible, but catches the pointer

            // The ellipse that pins the bottle to the shelf's floor plane (2026-08-01). Cut to
            // the drawing's width, so a bottle standing in a wide sheet does not throw a
            // shadow out past its own glass.
            var shadow = NewRect("Shadow", slot);
            shadow.anchorMin = shadow.anchorMax = new Vector2(0.5f, 0);
            shadow.pivot = new Vector2(0.5f, 0.5f);
            shadow.sizeDelta = new Vector2(drawn.x * 0.92f, 12);
            shadow.anchoredPosition = new Vector2(0, 3);
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
                // Hung on the shelf board's own face, just under the stand line.
                Place(plate, new Vector2(0.5f, 0), new Vector2(plateW, 20f),
                    new Vector2(centreX, -22f));
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
            // THE AUTHOR'S OWN PLATE (2026-08-19, "backbar sahnesindeki arkaplan tamamen
            // bu olacak"): one 640x360 picture stretched across the 1280x720 panel - an
            // exact integer 2x, the scene's grain - carrying the wall, the niches, the
            // shelf boards and the marble ledge that BackBarArt used to draw. The drawn
            // kit remains only as the fallback for a missing file, which should look
            // wrong rather than invisible.
            var wallPlate = Resources.Load<Sprite>("Scene/backbar");
            if (wallPlate != null)
            {
                boardImg.sprite = wallPlate;
                boardImg.type = Image.Type.Simple;
            }
            else
            {
                boardImg.sprite = BackBarArt.LuxeWall();
                boardImg.type = Image.Type.Tiled;
                boardImg.pixelsPerUnitMultiplier = 0.5f;
            }
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
            if (wallPlate == null)
            {
                var ledge = NewRect("Ledge", _menuPanel);
                ledge.anchorMin = new Vector2(0, 0); ledge.anchorMax = new Vector2(1, 0);
                ledge.pivot = new Vector2(0.5f, 0);
                ledge.sizeDelta = new Vector2(0, 88);
                ledge.anchoredPosition = Vector2.zero;
                var ledgeImg = ledge.gameObject.AddComponent<Image>();
                ledgeImg.sprite = BackBarArt.Ledge();
                ledgeImg.raycastTarget = false;
            }

            // The wall's working area: full width under the cornice, above the ledge.
            // The mask's top edge reaches HIGHER than the first shelf band does, because a
            // bottle rises ABOVE its band by construction — the slot stands at 48 and the art
            // is shelfH−40 tall, so the top of the glass overhangs the band by ~10px. On the
            // middle shelves that overhang leans in front of the niche above, which reads as
            // depth; on the TOP shelf it used to hit the mask and the author saw beheaded
            // bottles (2026-08-05). The list gets the same amount as top padding, so the
            // bands themselves stand exactly where they always did.
            // Full-panel now: the niches are placed by hand against the plate's own
            // measured geometry (NicheStandY/NicheSpanX), so there is no layout group and
            // no margin to fight - the mask only guards the page slide at the panel edge.
            var pageClip = NewRect("PageClip", _menuPanel);
            Stretch(pageClip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pageClip.gameObject.AddComponent<RectMask2D>();

            _bottleList = NewRect("Bottles", pageClip);
            Stretch(_bottleList, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _listHome = _bottleList.anchoredPosition;

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
