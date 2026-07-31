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

        private static int CountStockedGroups(TycoonRun run)
        {
            int n = 0;
            foreach (var type in MenuOrder)
            {
                if (type == IngredientType.Garnish) continue;
                foreach (var b in run.Shelf.Bottles)
                    if (b.Ingredient.Type == type) { n++; break; }
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
            if (_menuTab == null) BuildGroupPage(run); else BuildShelfPage(run, _menuTab);

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
                    if (b.Ingredient.Info?.Category == category)
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
                var name = Handwritten(NewText("N", content, _display, 16, TextAnchor.MiddleCenter, InkDark));
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
                    if (b.Ingredient.Info?.Category != cat) continue;
                    var slot = NewRect($"I_{b.Ingredient.Id}", icons);
                    var si = slot.gameObject.AddComponent<Image>();
                    si.sprite = ItemArt.Bottle(b.Ingredient.Info?.Style);
                    si.preserveAspect = true; si.raycastTarget = false;
                    si.color = si.sprite == null
                        ? UITheme.StyleColor(b.Ingredient.Info?.Style, b.Ingredient.Type)
                        : (b.IsEmpty ? new Color(1f, 1f, 1f, 0.30f) : Color.white);
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
            _menuTitle.text = AisleName(category).ToUpperInvariant();

            var items = new List<ShelfBottle>();
            foreach (var b in run.Shelf.Bottles)
                if (b.Ingredient.Info?.Category == category) items.Add(b);
            float areaW = _bottleList.rect.width, areaH = _bottleList.rect.height;

            // The aisle is a SHELF, not a list of keys (v5 P13). Two planks, bottles standing on
            // them at their own proportions, and nothing under a bottle but the wood -- the
            // price and the stock come up on hover instead of being printed on every key, so
            // the eye reads bottles first and numbers only when it asks for them.
            const int shelves = 2;
            float shelfH = (areaH - GridGap) / shelves;

            for (int row = 0; row < shelves; row++)
            {
                int from = row * ShelfColumns, count = Mathf.Min(ShelfColumns, items.Count - from);
                if (count <= 0) break;

                // The sheet lays its children out vertically, so the shelves are STACKED by the
                // layout rather than anchored by hand -- hand-anchored bands were simply
                // overridden and the page came up blank.
                var band = NewRect($"Shelf{row}", _bottleList);
                var fill = band.gameObject.AddComponent<LayoutElement>();
                fill.preferredHeight = shelfH; fill.preferredWidth = areaW; fill.flexibleWidth = 1f;

                // The plank: a board with a lit front edge, drawn at the sheet's own grain.
                var plank = NewRect("Plank", band);
                plank.anchorMin = new Vector2(0.02f, 0); plank.anchorMax = new Vector2(0.98f, 0);
                plank.pivot = new Vector2(0.5f, 0);
                plank.offsetMin = new Vector2(0, 6); plank.offsetMax = new Vector2(0, 16);
                plank.gameObject.AddComponent<Image>().color = ShelfWood;
                var lip = NewRect("Lip", plank);
                lip.anchorMin = new Vector2(0, 1); lip.anchorMax = new Vector2(1, 1);
                lip.pivot = new Vector2(0.5f, 1);
                lip.sizeDelta = new Vector2(0, 2);
                lip.anchoredPosition = Vector2.zero;
                lip.gameObject.AddComponent<Image>().color = ShelfLip;

                // Centred on the plank rather than packed to the left: a shelf with two bottles
                // on it is a shelf with two bottles on it, not a row that ran out.
                float slotW = (areaW * 0.96f) / ShelfColumns;
                float startX = -slotW * count * 0.5f;
                for (int i = 0; i < count; i++)
                    AddShelfBottle(band, items[from + i], run,
                        startX + slotW * (i + 0.5f), slotW, shelfH);
            }

            if (items.Count == 0)
            {
                var none = Handwritten(NewText("Empty", _bottleList, _body, 8,
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
            Place(slot, new Vector2(0.5f, 0), new Vector2(slotW - 6f, shelfH - 18f),
                new Vector2(centreX, 16f));
            var hit = slot.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);          // invisible, but catches the pointer

            var art = NewRect("Art", slot);
            Stretch(art, Vector2.zero, Vector2.one, new Vector2(6, 4), new Vector2(-6, -18));
            var img = art.gameObject.AddComponent<Image>();
            img.sprite = ItemArt.Bottle(card.Info?.Style);
            img.preserveAspect = true; img.raycastTarget = false;
            img.color = img.sprite == null
                ? UITheme.StyleColor(card.Info?.Style, card.Type)
                : (shut ? new Color(1f, 1f, 1f, 0.38f) : Color.white);

            // The brand, lettered in engine under the bottle -- the art carries a blank label
            // because the generator cannot spell (the keg precedent).
            var name = Handwritten(NewText("N", slot, _body, 8, TextAnchor.UpperCenter,
                shut ? InkSoft : InkDark));
            Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(slotW - 8f, 12f), Vector2.zero);
            name.text = shut ? $"{card.Name}  ·  {blocked}" : card.Name;

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

            // Pinned above the bottle, and kept inside the board at either end. Positioned in
            // the PANEL's space, not the sheet's: the sheet lays its children out vertically,
            // so a panel parented there is treated as another row -- it lost its size, its
            // backing plate and its place above the bottle all at once.
            var panel = _bottleInfo;
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            var world = near.TransformPoint(new Vector3(0, near.rect.height * 0.5f + 14f, 0));
            var local = (Vector2)_menuPanel.InverseTransformPoint(world);
            float halfBoard = _menuPanel.rect.width * 0.5f;
            float x = Mathf.Clamp(local.x, -halfBoard + panel.rect.width * 0.5f + 8f,
                halfBoard - panel.rect.width * 0.5f - 8f);
            panel.anchoredPosition = new Vector2(x, local.y + 8f);
        }

        private void HideBottleInfo()
        {
            if (_bottleInfo != null) _bottleInfo.gameObject.SetActive(false);
        }

        private void BuildBottleInfo()
        {
            _bottleInfo = NewRect("BottleInfo", _menuPanel);
            _bottleInfo.anchorMin = _bottleInfo.anchorMax = new Vector2(0.5f, 0.5f);
            _bottleInfo.pivot = new Vector2(0.5f, 0f);
            _bottleInfo.sizeDelta = new Vector2(184, 58);
            var bg = _bottleInfo.gameObject.AddComponent<Image>();
            bg.color = InkDark;
            bg.raycastTarget = false;

            _bottleInfoName = NewText("N", _bottleInfo, _display, 8, TextAnchor.UpperCenter,
                UITheme.Cream[4]);
            Place(_bottleInfoName.rectTransform, new Vector2(0.5f, 1), new Vector2(176, 12),
                new Vector2(0, -6));
            _bottleInfoStock = NewText("S", _bottleInfo, _body, 8, TextAnchor.UpperCenter,
                UITheme.Cream[3]);
            Place(_bottleInfoStock.rectTransform, new Vector2(0.5f, 1), new Vector2(176, 12),
                new Vector2(0, -21));
            _bottleInfoPrice = NewText("P", _bottleInfo, _body, 8, TextAnchor.UpperCenter,
                UITheme.Money);
            Place(_bottleInfoPrice.rectTransform, new Vector2(0.5f, 1), new Vector2(176, 12),
                new Vector2(0, -44));

            var track = NewRect("Track", _bottleInfo);
            Place(track, new Vector2(0.5f, 1), new Vector2(160, 4), new Vector2(0, -36));
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
        private static readonly Color ShelfWood = new Color(0.30f, 0.19f, 0.12f, 1f);
        private static readonly Color ShelfLip = new Color(0.46f, 0.31f, 0.19f, 1f);
        private RectTransform _bottleInfo;
        private Text _bottleInfoName, _bottleInfoStock, _bottleInfoPrice;
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
            iconImg.sprite = ItemArt.Bottle(card.Info?.Style);
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
            // The menu is a wooden clipboard with the drink list written on its paper.
            _menuPanel = NewRect("MenuPanel", _root);
            Place(_menuPanel, new Vector2(0.5f, 0.5f), new Vector2(BoardW, BoardH), new Vector2(BoardX, 0));
            _menuHome = _menuPanel.anchoredPosition;
            var boardImg = _menuPanel.gameObject.AddComponent<Image>();
            var board = ItemArt.Load("menu_board");
            if (board != null) { boardImg.sprite = board; boardImg.preserveAspect = true; boardImg.color = Color.white; }
            else boardImg.color = UITheme.Night[1];
            Swallow(_menuPanel);

            // A red X in the board's top-right corner closes the whole flow.
            var close = NewRect("Close", _menuPanel);
            Place(close, new Vector2(0.5f, 0.5f), new Vector2(CornerSize, CornerSize),
                PaperCorner(1, 1) + new Vector2(-22f, -12f));
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

            var title = _menuTitle = Handwritten(NewText("Title", _menuPanel, _display, 19, TextAnchor.MiddleCenter, Color.white));
            var outline = title.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.16f, 0.09f, 0.04f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);
            // Kept inside the clip: it wraps and shrinks to fit rather than running past the metal.
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(232, 44),
                new Vector2(BoardW * PaperCX, BoardH * (PaperCY + PaperH * 0.5f) + 2f));
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 9; title.resizeTextMaxSize = 19;
            title.text = "MAKE A DRINK";

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
            var pageClip = NewRect("PageClip", _menuPanel);
            Place(pageClip, new Vector2(0.5f, 0.5f),
                new Vector2(BoardW * PaperW - 44f, BoardH * PaperH - 156f),
                new Vector2(BoardW * PaperCX, BoardH * PaperCY + 2f));
            pageClip.gameObject.AddComponent<RectMask2D>();

            _bottleList = NewRect("Bottles", pageClip);
            Stretch(_bottleList, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _listHome = _bottleList.anchoredPosition;
            var listLayout = _bottleList.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = GridGap; listLayout.childControlHeight = true;
            listLayout.childControlWidth = true; listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childAlignment = TextAnchor.UpperLeft;

            // Right: a side column beside the menu — what's in the shaker, then the actions.
            // The mix/serve buttons live here, out of the item grid, per the redesign.
            // Nothing but the drink list belongs on the paper — the readouts and the buttons
            // sit off the board, under it.
            var side = _menuSide = NewRect("Side", _root);
            Place(side, new Vector2(0.5f, 0.5f), new Vector2(BoardW * PaperW, 54),
                new Vector2(BoardW * PaperCX, BoardH * (PaperCY - PaperH * 0.5f) + 34f));

            // On the sheet itself and centred, so the page animation carries it too.
            var actions = NewRect("Actions", _menuPanel);
            Place(actions, new Vector2(0.5f, 0.5f), new Vector2(212, 40),
                new Vector2(BoardW * PaperCX, BoardH * (PaperCY - PaperH * 0.5f) + 30f));
            var actLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actLayout.childControlWidth = true; actLayout.childForceExpandWidth = true;
            actLayout.childControlHeight = true; actLayout.childForceExpandHeight = true;
            AddFlexButton(actions, "SERVE  →", UITheme.PrimaryAction, () =>
            {
                if (!Run.Glass.IsEmpty) GoTo(Stage.Serve);
            });

            AddBinButton(_menuPanel);
        }
    }
}
