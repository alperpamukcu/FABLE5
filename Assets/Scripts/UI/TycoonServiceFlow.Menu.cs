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

        /// <summary>Starts a page turn to <paramref name="tab"/> (null = back to the index).</summary>
        private void OpenTab(string tab)
        {
            if (_flipT < 1f) return;                 // already turning
            _flipTo = tab;
            _flipDir = tab != null ? 1 : -1;         // in = folded away, out = lifted back
            _flipT = 0f;
        }

        /// <summary>Drives the change. The board and its clip hold still; the page of keys slides
        /// off one edge and the next slides in from the other, clipped to the paper. Opening a
        /// section runs it leftward and coming back runs it right, so the motion says which way you
        /// went. The page is swapped at the midpoint, while none of it is on screen.</summary>
        private void AdvancePageTurn()
        {
            if (_flipT >= 1f || _bottleList == null) return;
            bool wasFirstHalf = _flipT < 0.5f;
            _flipT = Mathf.MoveTowards(_flipT, 1f, Mathf.Max(Time.deltaTime, 1e-4f) / FlipTime);

            if (wasFirstHalf && _flipT >= 0.5f)
            {
                _menuTab = _flipTo;
                RefreshMenu();
            }

            // Far enough that the outermost key is clear of the paper before the swap.
            float travel = _bottleList.rect.width + 60f;
            float x;
            if (_flipT < 0.5f)
            {
                float u = _flipT / 0.5f;
                x = -travel * (u * u) * _flipDir;              // accelerates away
            }
            else
            {
                float u = (_flipT - 0.5f) / 0.5f;
                float e = 1f - (1f - u) * (1f - u);            // and eases into place
                x = travel * (1f - e) * _flipDir;
            }
            _bottleList.anchoredPosition = _listHome + new Vector2(x, 0f);

            if (_flipT >= 1f) ResetPageSlide();
        }

        /// <summary>Puts the page back on its mark — on landing, and whenever the menu opens, so a
        /// flow closed mid-slide never comes back with its page off the paper.</summary>
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

        private static int CountStockedGroups(TycoonRun run)
        {
            int n = 0;
            foreach (var type in MenuOrder)
            {
                if (type == IngredientType.Garnish) continue;
                foreach (var b in run.Shelf.Bottles)
                    if (b.Ingredient.Type == type && OnTheBackBar(b.Ingredient)) { n++; break; }
            }
            return n;
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

        /// <summary>A point inset from one of the paper's corners — every corner control uses
        /// this, so they are geometrically symmetric rather than eyeballed.</summary>
        private static Vector2 PaperCorner(int sx, int sy) => new Vector2(
            BoardW * (PaperCX + sx * PaperW * 0.5f) - sx * CornerInset,
            BoardH * (PaperCY + sy * PaperH * 0.5f) - sy * CornerInset);

        /// <summary>
        /// What is in the tin, as a single bar across the top of the sheet: one segment per
        /// poured ingredient, in its own colour, carrying its share. No vessel, just the mix.
        /// </summary>
        private void BuildMixBar()
        {
            _mixBar = NewRect("MixBar", _menuPanel);
            var barHost = NewRect("MixBarFrame", _menuPanel);
            Place(barHost, new Vector2(0.5f, 0.5f), new Vector2(BoardW * PaperW - 30f, 74f),
                new Vector2(BoardW * PaperCX, BoardH * (PaperCY + PaperH * 0.5f) - 96f));
            var frame = barHost.gameObject.AddComponent<Image>();
            var barSprite = ItemArt.Load("plate");
            if (barSprite != null)
            {
                // The same plate, tinted dark and sunk — a track the mix sits in.
                frame.sprite = barSprite; frame.type = Image.Type.Sliced;
                frame.color = new Color(0.26f, 0.22f, 0.19f);
            }
            else frame.color = new Color(0.30f, 0.24f, 0.16f, 0.16f);
            frame.raycastTarget = false;

            // Say what the gauge is, so a row of coloured chips is not a mystery.
            var caption = Handwritten(NewText("Caption", barHost, _body, 11, TextAnchor.MiddleLeft,
                new Color(0.30f, 0.21f, 0.12f)));
            Place(caption.rectTransform, new Vector2(0, 0.5f), new Vector2(180, 18), new Vector2(6, 46));
            caption.text = "WHAT'S IN THE TIN";

            // The segments live in the frame's recessed channel.
            _mixBar = NewRect("MixBar", barHost);
            Stretch(_mixBar, Vector2.zero, Vector2.one, new Vector2(14, 10), new Vector2(-14, -10));


        }

        private void RefreshMixBar()
        {
            if (_mixBar == null) return;   // the gauge is shelved for now
            foreach (Transform child in _mixBar) Destroy(child.gameObject);

            var run = Run;
            var glass = run.Glass;
            float w = _mixBar.rect.width;
            float x = 0f;

            // Each poured ingredient in ITS GROUP'S colour, then what is still empty — the bar
            // is the whole tin, so you read how much room is left, not just the ratios.
            foreach (var id in glass.Ingredients)
            {
                var card = run.Shelf.Find(id)?.Ingredient;
                float share = (float)glass.RatioOf(id) * (float)glass.FillFraction;
                var col = UITheme.TypeRamp[card?.Type ?? IngredientType.Spirit][3];
                x += AddMixSegment(id, share * w, new Color(col.r, col.g, col.b, 0.92f),
                    $"{share:P0}", new Color(0.12f, 0.10f, 0.08f), x);
            }

            float free = Mathf.Max(0f, 1f - (float)glass.FillFraction);
            if (free > 0.001f)
                AddMixSegment("empty", free * w, new Color(0.42f, 0.35f, 0.26f, 0.16f),
                    glass.IsEmpty ? "EMPTY" : $"{free:P0} EMPTY",
                    new Color(0.44f, 0.37f, 0.28f), x);
        }

        private float AddMixSegment(string id, float width, Color fill, string label, Color ink, float x)
        {
            var seg = NewRect($"Seg_{id}", _mixBar);
            seg.anchorMin = new Vector2(0, 0); seg.anchorMax = new Vector2(0, 1);
            seg.pivot = new Vector2(0, 0.5f);
            seg.offsetMin = new Vector2(0, 2); seg.offsetMax = new Vector2(0, -2);
            seg.sizeDelta = new Vector2(width, -4);
            seg.anchoredPosition = new Vector2(x, 0);
            var img = seg.gameObject.AddComponent<Image>();
            var chip = ItemArt.Load("plate");
            if (chip != null) { img.sprite = chip; img.type = Image.Type.Sliced; }
            img.color = fill; img.raycastTarget = false;

            var text = NewText("Pct", seg, _body, 11, TextAnchor.MiddleCenter, ink);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = width > 26f ? label : "";
            return width;
        }

        /// <summary>
        /// The menu is paged (2026-07-24). The first page is the shelf's sections — SPIRITS,
        /// BITTERS and so on — and choosing one opens that section's page, where the bottles
        /// are listed with what they cost. That keeps the sheet readable however big the bar
        /// gets, and gives prices somewhere to live.
        /// </summary>
        private void RefreshMenu()
        {
            var run = Run;
            foreach (Transform child in _bottleList) Destroy(child.gameObject);
            if (_menuBack != null) _menuBack.gameObject.SetActive(_menuTab != null);
            BuildShelfPage(run, null);   // the whole wall, every bottle — no aisles (2026-07-31)

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

        /// <summary>Page one: one card per stocked section.</summary>
        /// <summary>
        /// The index: the bar's AISLES (v5 P10 categories, not ingredient types) as flat keys
        /// on the sheet. The notes asked for cream paper rather than the coloured plastic
        /// plates this used to carry -- a bar menu is a printed list, and the colour was
        /// spending the eye's attention on navigation instead of on the drink.
        /// </summary>
        private void BuildGroupPage(TycoonRun run)
        {
            _menuTitle.text = "DRINKS";
            var row = NewRect("Groups", _bottleList);
            var grid = row.gameObject.AddComponent<GridLayoutGroup>();
            float areaW = _bottleList.rect.width, areaH = _bottleList.rect.height;
            grid.spacing = new Vector2(12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            const int cols = 3, gRows = 3;
            grid.constraintCount = cols;
            grid.cellSize = new Vector2((areaW - (cols - 1) * 12f) / cols,
                Mathf.Min(132f, (areaH - (gRows - 1) * 12f) / gRows));
            grid.childAlignment = TextAnchor.UpperCenter;

            foreach (var category in IngredientCategories.All)
            {
                int have = 0, empty = 0;
                foreach (var b in run.Shelf.Bottles)
                    if (b.Ingredient.Info?.Category == category && OnTheBackBar(b.Ingredient))
                    { have++; if (b.IsEmpty) empty++; }
                if (have == 0) continue;

                var key = NewRect($"Aisle_{category}", row);
                var bg = key.gameObject.AddComponent<Image>();
                bg.color = PaperKey;
                var btn = key.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.ColorTint;
                var cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.03f, 1.02f, 0.98f, 1f);
                cb.pressedColor = new Color(0.90f, 0.88f, 0.83f, 1f);
                cb.fadeDuration = 0.05f;
                btn.colors = cb;
                var cat = category;
                btn.onClick.AddListener(() => OpenTab(cat));

                var content = NewRect("Content", key);
                Stretch(content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var sink = key.gameObject.AddComponent<PressSink>();
                sink.Face = content; sink.Depth = 3f; sink.Squash = 0.012f;

                // A hairline rule under the heading, the way a printed list is set. Ink, not
                // plastic: the only colour on the key is the drink it is pointing at.
                var name = Handwritten(NewText("N", content, _display, 16,
                    TextAnchor.MiddleCenter, InkDark));
                Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(grid.cellSize.x - 20, 22),
                    new Vector2(0, -9));
                name.text = AisleName(category);

                var rule = NewRect("Rule", content);
                Place(rule, new Vector2(0.5f, 1), new Vector2(grid.cellSize.x - 34, 1), new Vector2(0, -25));
                rule.gameObject.AddComponent<Image>().color = new Color(InkDark.r, InkDark.g, InkDark.b, 0.35f);

                var count = Handwritten(NewText("C", content, _body, 8, TextAnchor.UpperCenter, InkSoft));
                Place(count.rectTransform, new Vector2(0.5f, 1), new Vector2(grid.cellSize.x - 20, 12),
                    new Vector2(0, -30));
                string unit = have == 1 ? "bottle" : "bottles";
                count.text = empty > 0 ? $"{have} {unit} · {empty} out" : $"{have} {unit}";

                var icons = NewRect("Icons", content);
                Place(icons, new Vector2(0.5f, 0), new Vector2(grid.cellSize.x - 16, grid.cellSize.y - 58),
                    new Vector2(0, 12));
                var ig = icons.gameObject.AddComponent<GridLayoutGroup>();
                int iconCols = Mathf.Clamp(have, 1, 4);
                float cell = Mathf.Min(grid.cellSize.y - 60f, (grid.cellSize.x - 24f) / iconCols);
                ig.cellSize = new Vector2(cell, cell);
                ig.spacing = new Vector2(3, 3);
                ig.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                ig.constraintCount = iconCols;
                ig.childAlignment = TextAnchor.LowerCenter;
                foreach (var b in run.Shelf.Bottles)
                {
                    if (b.Ingredient.Info?.Category != cat || !OnTheBackBar(b.Ingredient)) continue;
                    var slot = NewRect($"I_{b.Ingredient.Id}", icons);
                    var si = slot.gameObject.AddComponent<Image>();
                    si.sprite = ItemArt.Bottle(b.Ingredient);
                    si.preserveAspect = true; si.raycastTarget = false;
                    si.color = si.sprite == null
                        ? UITheme.StyleColor(b.Ingredient.Info?.Style, b.Ingredient.Type)
                        : (b.IsEmpty ? new Color(1f, 1f, 1f, 0.30f) : Color.white);
                    var sl = BottleArt.AddLiquid(slot, b.Ingredient);
                    if (sl != null)
                        BottleArt.SetLevel(sl, b.Ingredient, open: false,
                            b.Capacity > 0 ? (float)(b.Remaining / b.Capacity) : 0f);
                }
            }
        }

        /// <summary>
        /// An aisle's page: the bottles STANDING ON A SHELF rather than listed as keys (v5 P13,
        /// the notes' shelf view). Hovering one raises an info panel with what is left in it and
        /// what it costs; clicking takes it to the prep stage as the keys used to.
        /// </summary>
        private void BuildShelfPage(TycoonRun run, string category)
        {
            // null category = THE WHOLE WALL (2026-07-31): every bottle the bar owns on one
            // back-bar, no aisles. The hover panel still answers what each one is.
            _menuTitle.text = category == null ? "LAST CALL" : AisleName(category).ToUpperInvariant();

            // Beer leaves the shelves (the author, 2026-08-01): it lives in KEGS on the
            // floor, drawn at keg scale with only their crowns in frame. Everything else
            // stands on the wall in rows that widen as the cellar grows, so the endgame
            // bar still fits on three shelves.
            var items = new List<ShelfBottle>();
            var kegs = new List<ShelfBottle>();
            foreach (var b in run.Shelf.Bottles)
            {
                if (category != null && b.Ingredient.Info?.Category != category) continue;
                if (!OnTheBackBar(b.Ingredient)) continue;
                if (b.Ingredient.Type == IngredientType.Beer) kegs.Add(b);
                else items.Add(b);
            }
            float areaW = _bottleList.rect.width, areaH = _bottleList.rect.height;

            int perRow = Mathf.Max(7, Mathf.CeilToInt(items.Count / 3f));
            int shelves = Mathf.Max(3, Mathf.CeilToInt(items.Count / (float)perRow));
            float shelfH = (areaH - GridGap) / shelves;

            for (int row = 0; row < shelves; row++)
            {
                int from = row * perRow, count = Mathf.Max(0, Mathf.Min(perRow, items.Count - from));
                // An empty plank still hangs on the wall (2026-08-01): a young bar faces a
                // sparsely stocked back-bar, not a wall with one shelf on it.
                if (count <= 0 && category != null) break;

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
            var art = NewRect("Art", slot);
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0);
            art.pivot = new Vector2(0.5f, 0);
            art.sizeDelta = new Vector2(artW, artH);
            art.anchoredPosition = new Vector2(0, 2f);
            var img = art.gameObject.AddComponent<Image>();
            img.sprite = ItemArt.Bottle(card);
            img.preserveAspect = true; img.raycastTarget = false;
            img.color = img.sprite == null
                ? UITheme.StyleColor(card.Info?.Style, card.Type)
                : (shut ? new Color(1f, 1f, 1f, 0.38f) : Color.white);

            // What is left in it, drawn rather than printed. The bottles are shot empty, so this is
            // the only thing that says a bottle is running down -- and it says it where the player
            // is already looking instead of in a hover panel.
            var liquid = BottleArt.AddLiquid(art, card);
            if (liquid != null)
            {
                var piece = BottleArt.For(card);
                float level = bottle.Capacity > 0 ? (float)(bottle.Remaining / bottle.Capacity) : 0f;
                liquid.fillAmount = piece.FillAmount(level);
                if (shut)
                {
                    liquid.color = new Color(liquid.color.r, liquid.color.g, liquid.color.b, 0.38f);
                    var frontRt = liquid.transform.Find("GlassFront");   // dims with its bottle
                    if (frontRt != null) frontRt.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.19f);
                }
            }

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
            Pressable(slot, art, img, lift: shut ? 2f : 5f, depth: shut ? 0f : 5f);

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
            sink.Face = art; sink.Depth = 3f; sink.Squash = 0.01f;
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

        // The menu is printed, not moulded (v5 P13): cream stock, two weights of ink, and no
        // colour of its own -- the only colour on the page is the drink.
        private const int ShelfColumns = 4;
        private const float ShelfFaceH = 34f;
        private RectTransform _kegRow;
        private static readonly Color ShelfWood = new Color(0.30f, 0.19f, 0.12f, 1f);
        private static readonly Color ShelfLip = new Color(0.46f, 0.31f, 0.19f, 1f);
        private RectTransform _bottleInfo;
        private Button _serveButton;
        private Text _serveLabel;
        private Text _bottleInfoName, _bottleInfoStock, _bottleInfoPrice;
        private RectTransform _bottleInfoTail;
        private Image _bottleInfoFill;

        private static readonly Color PaperKey = new Color(0.96f, 0.94f, 0.86f, 1f);
        private static readonly Color InkDark = new Color(0.12f, 0.10f, 0.09f, 1f);
        private static readonly Color InkSoft = new Color(0.34f, 0.30f, 0.26f, 1f);

        /// <summary>An aisle's name as it is printed on the menu (v5 P10 categories).</summary>
        private static string AisleName(string category)
        {
            switch (category)
            {
                case IngredientCategories.Vodka: return "VODKA";
                case IngredientCategories.Gin: return "GIN";
                case IngredientCategories.Rum: return "RUM";
                case IngredientCategories.Whiskey: return "WHISKEY";
                case IngredientCategories.Tequila: return "TEQUILA";
                case IngredientCategories.Liqueur: return "LIQUEURS";
                case IngredientCategories.Juice: return "JUICES";
                case IngredientCategories.Mixer: return "MIXERS";
                case IngredientCategories.Garnish: return "GARNISHES";
                case IngredientCategories.Beer: return "ON TAP";
                default: return category.ToUpperInvariant();
            }
        }

        private static readonly IngredientType[] MenuOrder =
        {
            // Beer leads: it is the order you answer without thinking (GDD 21 §10), so it sits
            // where the hand already is rather than at the end of the cocktail sections.
            IngredientType.Beer,
            IngredientType.Spirit, IngredientType.Bitter, IngredientType.Sweet,
            IngredientType.Sour, IngredientType.Bubbly, IngredientType.Garnish,
        };

        private static string GroupName(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.Spirit: return "SPIRITS";
                case IngredientType.Bitter: return "BITTERS";
                case IngredientType.Sweet: return "SWEET";
                case IngredientType.Sour: return "SOUR / CITRUS";
                case IngredientType.Bubbly: return "MIXERS";
                case IngredientType.Beer: return "ON TAP";
                default: return "GARNISHES";
            }
        }

        /// <summary>The name as it fits on a key — one word, so the heading never wraps onto the
        /// bottle count underneath it. The section page still uses the full name.</summary>
        private static string GroupKeyName(IngredientType type)
            => type == IngredientType.Sour ? "CITRUS"
             : type == IngredientType.Beer ? "BEER"
             : GroupName(type);

        private void AddGroupHeader(RectTransform parent, string title, Color colour)
        {
            var rt = NewRect("Header", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = HeadingH;
            var text = NewText("L", rt, _body, 12, TextAnchor.LowerLeft,
                Color.Lerp(colour, new Color(0.24f, 0.16f, 0.09f), 0.62f));
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 0), new Vector2(-2, 0));
            text.text = $"— {title} —";
            var line = NewRect("Rule", rt);
            line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0);
            line.offsetMin = new Vector2(0, 0); line.offsetMax = new Vector2(0, 2);
            var img = line.gameObject.AddComponent<Image>();
            img.color = new Color(colour.r, colour.g, colour.b, 0.4f);
            img.raycastTarget = false;
        }

        /// <summary>
        /// One bottle on a section page — the same key as the section tabs, tinted by its group
        /// and only as big as its name, how full it is and what it costs.
        /// </summary>
        private void AddItemBox(RectTransform parent, ShelfBottle bottle, TycoonRun run)
        {
            var card = bottle.Ingredient;
            bool empty = bottle.IsEmpty;
            // Why this key cannot be pressed, if it cannot — printed where the fill level goes,
            // because "there is no room for it" is the same kind of fact as "there is none left"
            // (2026-07-28). A key that opens a stage which would refuse you is a key that lies.
            string blocked =
                empty ? "OUT"
                : card.Type == IngredientType.Beer
                    ? (run.CanPull(card.Id) ? null : run.ServingGlass.IsFull ? "FULL" : "BUSY")
                    : (run.Glass.IsFull ? "FULL" : null);
            bool shut = blocked != null;
            var col = UITheme.TypeRamp[card.Type][3];

            var box = NewRect($"Box_{card.Id}", parent);
            var bg = box.gameObject.AddComponent<Image>();
            var plate = ItemArt.Load("plate");
            var plateDown = ItemArt.Load("plate_down");
            if (plate != null)
            {
                bg.sprite = plate; bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = PlatePixelScale;
                bg.color = shut ? Color.Lerp(col, new Color(0.45f, 0.43f, 0.42f), 0.7f) : col;
            }
            else bg.color = new Color(col.r, col.g, col.b, shut ? 0.25f : 0.5f);

            var content = NewRect("Content", box);
            Stretch(content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            if (!shut)
            {
                var btn = box.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                if (plateDown != null)
                {
                    btn.transition = Selectable.Transition.SpriteSwap;
                    var st = btn.spriteState;
                    st.pressedSprite = plateDown; st.selectedSprite = plate;
                    btn.spriteState = st;
                }
                var sink = box.gameObject.AddComponent<PressSink>();
                sink.Face = content; sink.Depth = 4f; sink.Squash = 0.015f;
                var c = card;
                btn.onClick.AddListener(() => OpenBottle(c));
            }

            // The key's contents follow the grid cell, so changing the column count moves the
            // bottle, the name and the price together instead of leaving them at an old width.
            var g = parent.GetComponent<GridLayoutGroup>();
            float cw = g != null ? g.cellSize.x : 172f;
            float chh = g != null ? g.cellSize.y : 226f;

            // The bottle is the thing you are choosing, so it gets most of the key.
            var icon = NewRect("Icon", content);
            Place(icon, new Vector2(0.5f, 1), new Vector2(cw - 30f, chh - 68f), new Vector2(0, -8));
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.raycastTarget = false; iconImg.preserveAspect = true;
            iconImg.sprite = ItemArt.Bottle(card);
            iconImg.color = iconImg.sprite == null ? UITheme.StyleColor(card.Info?.Style, card.Type)
                : (shut ? new Color(1f, 1f, 1f, 0.4f) : Color.white);

            // Name, then how full it is and what it costs — the three things the key is sized for.
            // Pixel faces only rasterise cleanly at whole multiples of their 8px design size, so
            // the labels are pinned to 16 and best-fit is off — it used to pick sizes like 11,
            // which lands the stems on half pixels and makes the letters look chewed (2026-07-27).
            var name = Outlined(Handwritten(NewText("Name", content, _body, 16, TextAnchor.LowerCenter, Color.white)));
            Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(cw + 6f, 34), new Vector2(0, 26));
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.text = card.Name.ToUpperInvariant();

            // How full it is, and what it costs — each in its own colour, both ringed in black.
            double fill = bottle.Capacity > 0 ? bottle.Remaining / bottle.Capacity : 0;
            // Small: on a 172-wide key the fill and the price cannot both be 16 without running
            // into each other, and the price is the number you are deciding on.
            var pct = Outlined(Handwritten(NewText("Fill", content, _body, 8, TextAnchor.UpperLeft,
                shut ? new Color(1f, 0.42f, 0.42f) : new Color(1f, 0.80f, 0.32f))), 1f);
            Place(pct.rectTransform, new Vector2(0, 1), new Vector2(cw * 0.5f, 14), new Vector2(12, -12));
            pct.text = blocked ?? $"{(int)System.Math.Round(fill * 100)}%";

            var price = Outlined(Handwritten(NewText("Price", content, _body, 16, TextAnchor.UpperRight,
                new Color(0.45f, 0.95f, 0.45f))));
            Place(price.rectTransform, new Vector2(1, 1), new Vector2(cw * 0.6f, 20), new Vector2(-12, -10));
            price.text = $"${Market.StockPrice(card)}";

            if (!shut && run.IsNewStock(card.Id))
            {
                var badge = Handwritten(NewText("New", content, _body, 8, TextAnchor.UpperRight,
                    new Color(0.62f, 0.36f, 0.04f)));
                Place(badge.rectTransform, new Vector2(1, 1), new Vector2(46, 14), new Vector2(-8, -6));
                badge.text = "NEW";
            }
        }

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

            // Its mirror on the paper's top-left: step back out of a section.
            _menuBack = NewRect("Back", _menuPanel);
            Place(_menuBack, new Vector2(0.5f, 0.5f), new Vector2(CornerSize, CornerSize),
                PaperCorner(-1, 1) + new Vector2(22f, -12f));
            var backImg = _menuBack.gameObject.AddComponent<Image>();
            var backSprite = ItemArt.Load("btn_back");
            if (backSprite != null) { backImg.sprite = backSprite; backImg.preserveAspect = true; backImg.color = Color.white; }
            else backImg.color = new Color(0.62f, 0.15f, 0.17f);
            var backBtn = _menuBack.gameObject.AddComponent<Button>();
            backBtn.targetGraphic = backImg;
            backBtn.onClick.AddListener(() => OpenTab(null));
            GiveKeyPress(_menuBack, backBtn, backImg, "btn_back_down");
            if (backSprite == null)
            {
                var backArrow = NewText("A", _menuBack, _display, 20, TextAnchor.MiddleCenter, new Color(0.97f, 0.93f, 0.86f));
                Stretch(backArrow.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                backArrow.text = "←";
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
            var pageClip = NewRect("PageClip", _menuPanel);
            Stretch(pageClip, Vector2.zero, Vector2.one, new Vector2(40, 92), new Vector2(-40, -118));
            pageClip.gameObject.AddComponent<RectMask2D>();

            _bottleList = NewRect("Bottles", pageClip);
            Stretch(_bottleList, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _listHome = _bottleList.anchoredPosition;
            var listLayout = _bottleList.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = GridGap; listLayout.childControlHeight = true;
            listLayout.childControlWidth = true; listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childAlignment = TextAnchor.UpperLeft;

            var side = _menuSide = NewRect("Side", _root);
            side.gameObject.SetActive(false);   // the clipboard's side column retired with it

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
