using System;
using System.Collections.Generic;
using System.Globalization;
using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Decor: the upgrade screen as a CATALOGUE (2026-09-13).
    //
    // The author: "Her bölümün ayrı kısmı olmalı markette. Örneğin duvar dendiğinde tüm
    // seçenekler desen png si ile gözükmeli. Bu her grup için olmalı, marketteki sunumları daha
    // detaylı ve iyi bir site tasarımına sahip olmalı. Örneğin Sims'in dekorasyon UI'ı güzeldi."
    //
    // So the department is laid out the way a build catalogue is: a RAIL of the room's parts
    // down the left — the bar's own fittings, then the walls, what hangs on them, the light, the
    // floor, the plants and the counter — and, for the part that is open, every LADDER in it as
    // a row of cards, EVERY rung on show with its own pattern: the ones bought (wear any of them;
    // the room keeps the comfort of the rung it climbed to), the one that can be bought next, and
    // the rest behind the rung or the star they wait for.
    //
    // This overturns two earlier rules on purpose, and both are the author's own: owned pieces
    // were taken off the shelf (2026-09-06, "satın alınan eşyalar gözükmemeli") and rungs past
    // the next one were hidden (2026-08-26, "3. seviye 2. seviyeyi açmadıysan gözükmemeli"). A
    // shelf you WEAR from has to show what you own, and "tüm seçenekler gözükmeli" is all of them.
    public sealed partial class TycoonHud
    {
        private static readonly string[] DecorShelves =
            { "bar", "walls", "wall_art", "light", "furniture", "greenery", "counter" };

        // SHORT, because the rail is narrow: a word or two a line, two lines at most.
        private static readonly string[] DecorShelfWordKeys =
            { "decor.shelf.bar", "decor.shelf.walls", "decor.shelf.wall_art", "decor.shelf.light",
              "decor.shelf.furniture", "decor.shelf.greenery", "decor.shelf.counter" };

        private static readonly string[] DecorShelfIcons =
            { "up_seats", "up_walls", "up_wall_art", "up_light", "up_furniture", "up_greenery", "up_counter" };

        /// <summary>The rail and the air between it and the aisle. 8 + 196 + 12 leaves the
        /// aisle 796 of its 1004: five cards of 152 with four gaps of 8 is 792.</summary>
        private const float RailW = 196f, RailGap = 12f, RailKeyH = 52f;

        /// <summary>One rung's card. The window takes a 64x48 swatch at 2x with room round it,
        /// and the foot under it is a name on two lines, one fact and one control.
        /// TEN TALLER FOR THE BUFF (2026-09-23): the 16-unit fact row could hold 8-unit type and no badge, so it
        /// became a 22-unit BUFF ROW (158..180) and the foot moved down under it (184..210, 4 of air). A rung with
        /// no buff in its data keeps the old fact line, centred in the same row (CardMetaTop).</summary>
        private const float CardW = 152f, CardH = 214f, CardWinH = 108f, CardGap = 8f;
        private const int CardCols = 5;
        private const float CardNameTop = CardWinH + 8f, CardBuffTop = CardWinH + 50f,
                            CardMetaTop = CardWinH + 52f, CardFootTop = CardWinH + 76f;

        /// <summary>
        /// Stands the rail beside the aisle, or takes it away. The aisle's own viewport gives up
        /// its left side while the upgrade screen is open and takes it back after, so the other
        /// departments keep the five-across shelf they were measured for.
        /// </summary>
        private void LayOutDecorRail(bool on)
        {
            var view = _shopScroll != null ? _shopScroll.viewport : null;
            if (view == null) return;
            view.offsetMin = new Vector2(on ? 8f + RailW + RailGap : 8f, view.offsetMin.y);
            if (!on)
            {
                if (_decorRail != null) _decorRail.gameObject.SetActive(false);
                return;
            }
            if (_decorRail == null)
            {
                _decorRail = NewRect("DecorRail", view.parent);
                _decorRail.gameObject.AddComponent<Image>().color = ShopAisle;
            }
            _decorRail.gameObject.SetActive(true);
            _decorRail.anchorMin = new Vector2(0, 0);
            _decorRail.anchorMax = new Vector2(0, 1);
            _decorRail.pivot = new Vector2(0, 1);
            _decorRail.offsetMin = new Vector2(8f, view.offsetMin.y);
            _decorRail.offsetMax = new Vector2(8f + RailW, view.offsetMax.y);
        }

        /// <summary>The department itself: the rail's keys, then the open shelf.</summary>
        private void BuildUpgradeAisle(TycoonRun run, TycoonConfig cfg)
        {
            if (Array.IndexOf(DecorShelves, _decorSection ?? "") < 0) _decorSection = "walls";
            BuildDecorRail(run);
            // WHAT THE ROOM GIVES OPENS EVERY SHELF (2026-09-23, the author: "oyuncu bu upgradelere biraz muhtaç
            // edilmeli"): the dependence is shown where the player spends - TycoonHud.ShopBuffs.cs.
            if (_decorSection == "bar") { RoomBuffBand(run); BuildFittingShelves(run, cfg); return; }

            // THE WALLS ARE WHERE A BARE BAR IS SENT (2026-09-06): until the second rung of the
            // back wall is up, the walls' sign says so, and so does their key on the rail.
            bool sendHere = _decorSection == "walls" && run.LadderLevel("walls") < 2;
            ComfortBand(run);                   // where the room stands, over what would raise it (eighth list)
            RoomBuffBand(run);                  // and what the pieces in it give, under it (2026-09-23)
            ShopSign(sendHere ? UIText.T("decor.sign.start_here")
                              : GroupTitle(_decorSection), sendHere);

            var slots = new List<string>();
            foreach (var f in run.FixtureCatalogue)
                if (f.Group == _decorSection && !slots.Contains(f.Slot)) slots.Add(f.Slot);
            foreach (var slot in slots) DecorLadder(run, slot);
            if (slots.Count == 0) _cardTarget = ShopSection(UIText.T("decor.shelf_empty"));
        }

        // ── the rail ─────────────────────────────────────────────────────────────

        private void BuildDecorRail(TycoonRun run)
        {
            if (_decorRail == null) return;
            foreach (Transform child in _decorRail) Destroy(child.gameObject);
            Bevel(_decorRail, 2f, raised: false);   // a tray cut into the page

            for (int i = 0; i < DecorShelves.Length; i++)
            {
                string shelf = DecorShelves[i];
                bool on = shelf == _decorSection;
                bool hot = shelf == "walls" && run.LadderLevel("walls") < 2;
                int open = OpenOnShelf(run, shelf);

                var key = NewRect("Shelf_" + shelf, _decorRail);
                key.anchorMin = new Vector2(0, 1); key.anchorMax = new Vector2(1, 1);
                key.pivot = new Vector2(0.5f, 1);
                key.sizeDelta = new Vector2(-8f, RailKeyH - 2f);
                key.anchoredPosition = new Vector2(0, -(3f + i * RailKeyH));
                var face = key.gameObject.AddComponent<Image>();
                face.sprite = ChromeArt.Win98Key();
                face.type = Image.Type.Sliced;
                face.color = on ? ShopViceDeep : hot ? UITheme.Amber[3] : ShopPage;

                // The open shelf wears the lit edge the open TAB wears, down its left side:
                // one language for "this is the page you are reading".
                if (on)
                {
                    var lit = NewRect("Lit", key);
                    lit.anchorMin = new Vector2(0, 0); lit.anchorMax = new Vector2(0, 1);
                    lit.pivot = new Vector2(0, 0.5f);
                    lit.sizeDelta = new Vector2(4f, -8f);
                    lit.anchoredPosition = new Vector2(3f, 0);
                    var li = lit.gameObject.AddComponent<Image>();
                    li.color = ShopTabLit; li.raycastTarget = false;
                }

                // THE GROUP'S OWN PICTOGRAM, at its own 48 — drawn to be shown whole.
                var icon = NewRect("I", key);
                Place(icon, new Vector2(0, 0.5f), new Vector2(48, 48), new Vector2(6, 0));
                var ii = icon.gameObject.AddComponent<Image>();
                ii.sprite = ItemArt.Load(DecorShelfIcons[i]);
                ii.preserveAspect = true; ii.raycastTarget = false;
                ii.enabled = ii.sprite != null;

                var word = NewText("L", key, _shop, 16, TextAnchor.MiddleLeft,
                    on ? Color.white : hot ? UITheme.Night[0] : ShopInk);
                Stretch(word.rectTransform, Vector2.zero, Vector2.one, new Vector2(58, 2), new Vector2(-30, -2));
                word.horizontalOverflow = HorizontalWrapMode.Wrap;
                word.verticalOverflow = VerticalWrapMode.Overflow;
                word.lineSpacing = 0.9f;
                word.text = UIText.T(DecorShelfWordKeys[i]);

                // How many rungs this shelf could sell TONIGHT — the number that says where
                // the next thing to do is, without opening every shelf to look.
                if (open > 0)
                {
                    var badge = NewRect("Open", key);
                    Place(badge, new Vector2(1, 0.5f), new Vector2(22, 18), new Vector2(-6, 0));
                    var bi = badge.gameObject.AddComponent<Image>();
                    bi.color = UITheme.Lime[3]; bi.raycastTarget = false;
                    var bt = NewText("N", badge, _shop, 8, TextAnchor.MiddleCenter, UITheme.Lime[0]);
                    Stretch(bt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    bt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    bt.text = open.ToString(CultureInfo.InvariantCulture);
                }

                var btn = key.gameObject.AddComponent<Button>();
                btn.targetGraphic = face;
                string target = shelf;
                btn.onClick.AddListener(() =>
                {
                    if (_decorSection == target) return;
                    _decorSection = target;
                    _justOrdered.Clear(); _shopScrollAt = 1f; _shopScrollPx = -1f;
                    Sfx.Play("key_press", 0.55f);
                    RebuildDayEnd();
                });
                MarkHoverable(key, face);

                string title = GroupTitle(shelf == "bar" ? null : shelf);
                if (shelf == "bar") title = UIText.T("decor.rail.bar_title");
                int count = open;
                var relay = key.gameObject.AddComponent<HoverRelay>();
                relay.Entered = () => ShowShopCard(new TileSpec
                {
                    Identity = title,
                    MetaLine = count == 0 ? UIText.T("decor.rail.open_none")
                             : UIText.N("decor.rail.open_count", count),
                    Body = shelf == "bar"
                        ? UIText.T("decor.rail.body_bar")
                        : UIText.T("decor.rail.body_room"),
                    Art = ii.sprite,
                });
                relay.Exited = () => ShowShopCard(null);
            }
        }

        /// <summary>Rungs on a shelf the market would sell tonight, whatever the till says.</summary>
        private static int OpenOnShelf(TycoonRun run, string shelf)
        {
            int n = 0;
            if (shelf == "bar")
            {
                if (run.Seats < run.Config.MaxSeats) n++;
                if (run.CounterTier < run.Config.MaxAmbienceTier) n++;
                foreach (var g in run.Glassware)
                    if (run.GlassTier(g.Id) < TycoonRun.MaxGlassTier) n++;
                return run.CanFitTonight ? n : 0;
            }
            foreach (var f in run.FixtureCatalogue)
            {
                if (f.Group != shelf || run.OwnsFixture(f.Id)) continue;
                if (f.Level > 0 && !run.CanBuyRung(f)) continue;
                if (run.Rating.Average < f.Stars) continue;
                n++;
            }
            return n;
        }

        // ── the bar's own fittings: the stool, the bar top, the glass lines ─────

        private void BuildFittingShelves(TycoonRun run, TycoonConfig cfg)
        {
            int raised = 0;
            _cardTarget = ShopSection(UIText.T("decor.fit.section_seats"), columns: 4);
            int seat = Math.Min(run.Seats + 1, cfg.MaxSeats);
            var stool = new TileSpec
            {
                Name = UIText.T("decor.fit.stool.name"),
                Meta = UIText.T("decor.fit.stool.meta", ("seat", seat), ("max", cfg.MaxSeats)),
                Art = UpgradeIcon("seats"),
                ArtH = IconH,
                Identity = UIText.T("decor.fit.stool.identity"),
                MetaLine = UIText.T("decor.fit.stool.meta_line", ("seat", seat), ("max", cfg.MaxSeats)),
                Body = UIText.T("decor.fit.stool.body"),
                BuffB = new Buff(BuffKind.Bad, UIText.T("decor.fit.uses_upgrade")),
            };
            // WHAT IT ALWAYS GIVES (2026-09-23): a seat, and the comfort a stool past the opening four adds -
            // said as comfort, where decor.fit.stool.gain promised "+0.25 stars" Core never paid.
            SetShopBuffs(stool, AlwaysShopBuffs("seats", "+1", VenueComfort.StoolComfort));
            if (run.Seats < cfg.MaxSeats)
            {
                DressBuyable(stool, cfg.SeatPrice(run.Seats), "seat", true, () => run.BuySeat());
                AddTile(stool); raised++;
            }

            // THE COUNTER. It has been a real, priced, guarded fitting in Core the whole
            // time — BuyCounter, CounterPrice, two steps at $40 and $80, worth up to 0.06
            // satisfaction on EVERY served visit — and it had no tile in any department
            // until it was found by counting what the data offers against what the shop can
            // show (2026-08-09).
            var bar = new TileSpec
            {
                Name = UIText.T("decor.fit.bar.name"),
                Meta = UIText.T("decor.fit.rung_of", ("rung", run.CounterTier), ("max", cfg.MaxAmbienceTier)),
                Art = UpgradeIcon("bar"),
                CardArt = ItemArt.Load("sh_p_bar"),
                ArtH = IconH,
                Identity = UIText.T("decor.fit.bar.identity"),
                MetaLine = UIText.T("decor.fit.bar.meta_line", ("rung", run.CounterTier), ("max", cfg.MaxAmbienceTier)),
                Body = UIText.T("decor.fit.bar.body"),
                BuffB = new Buff(BuffKind.Bad, UIText.T("decor.fit.uses_upgrade")),
            };
            // SERVICE +3%, ALWAYS (2026-09-23): the same points a picture on the wall gives while it hangs, and
            // the same heart, so the two read as one number from two places.
            SetShopBuffs(bar, AlwaysShopBuffs("service", Pct(BarTopServicePct), 0));
            if (run.CounterTier < cfg.MaxAmbienceTier)
            {
                DressBuyable(bar, cfg.CounterPrice(run.CounterTier), "counter", true,
                    () => run.BuyCounter());
                AddTile(bar); raised++;
            }

            _cardTarget = ShopSection(UIText.T("decor.fit.section_glass"), columns: 4);
            foreach (var g in run.Glassware)
            {
                var glass = g;
                int tier = run.GlassTier(glass.Id);
                if (tier >= TycoonRun.MaxGlassTier) continue;     // a finished line is a bought one
                int stepPrice = glass.TierPrices[tier - 1];
                string glassName = UIText.Data("glass", glass.Id, "name", glass.Name);
                string rungOf = UIText.T("decor.fit.rung_of", ("rung", tier), ("max", TycoonRun.MaxGlassTier));
                var spec = new TileSpec
                {
                    Name = glassName,
                    Meta = rungOf,
                    Art = UpgradeIcon("glass"),
                    CardArt = GlassArt.For(glass, Mathf.Min(tier + 1, TycoonRun.MaxGlassTier)).Sprite,
                    ArtH = IconH,
                    Identity = UIText.T("decor.fit.glass.identity", ("glass", UIText.Caps(glassName))),
                    MetaLine = rungOf + " · " + DrinksServedIn(glass.Id),
                    Body = UIText.T("decor.fit.glass.body"),
                    BuffB = new Buff(BuffKind.Bad, UIText.T("decor.fit.uses_upgrade")),
                };
                // A STEP OF GLASS IS SERVICE ON EVERY SERVE (2026-09-23): TycoonRun.Ambience counts the steps of
                // every line together, so a step lifts every drink the bar serves - not only the ones poured into
                // this glass, as decor.fit.glass.gain said. Its comfort is half a step cap Core keeps private
                // (TycoonRun.GlassStepCap), so it is not printed rather than printed from a copy of the table.
                SetShopBuffs(spec, AlwaysShopBuffs("service", GlassStepServiceFigure(), 0));
                DressBuyable(spec, stepPrice, "glass:" + glass.Id, true,
                    () => run.BuyGlassTier(glass.Id));
                AddTile(spec); raised++;
            }
            if (raised == 0) _cardTarget = ShopSection(UIText.T("decor.fit.all_fitted"));
        }

        // ── one ladder: its head, and every rung as a card ─────────────────────

        private LastCall.Game.StageSlot SlotNamed(string id)
        {
            var slots = _bootstrap != null ? _bootstrap.StageSlots : null;
            if (slots != null)
                foreach (var s in slots)
                    if (s.Id == id) return s;
            return null;
        }

        private void DecorLadder(TycoonRun run, string slot)
        {
            var rungs = new List<FixtureDefinition>();
            foreach (var f in run.FixtureCatalogue)
                if (f.Slot == slot) rungs.Add(f);
            rungs.Sort((a, b) => a.Level.CompareTo(b.Level));
            var where = SlotNamed(slot);
            int total = 0;
            foreach (var r in rungs) if (r.Level > 0) total++;
            int climbed = run.LadderLevel(slot);

            // THE HEAD: what this ladder is, and how far up it the bar is — in the room's own
            // numbers, the comfort it is worth now, or for a tool the speed it works at.
            var head = NewRect("Ladder_" + slot, _offerRow);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            var title = NewText("T", head, _shop, 16, TextAnchor.LowerLeft, ShopInk);
            Stretch(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 6), new Vector2(-300, 0));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = UIText.Caps(where != null && where.Title != null
                ? UIText.Data("slot", where.Id, "title", where.Title) : slot);

            var status = NewText("S", head, _shop, 8, TextAnchor.LowerRight, TileMetaInk);
            Stretch(status.rectTransform, Vector2.zero, Vector2.one, new Vector2(300, 8), new Vector2(-6, 0));
            status.horizontalOverflow = HorizontalWrapMode.Overflow;
            var top = climbed > 0 ? TopRung(rungs, climbed) : null;
            // THE COMFORT IS THE CLIMBED RUNG'S, THE BUFF THE WORN RUNG'S (2026-09-23, the fittings spec §11.4:
            // "MARK 3 OF 6 · CROWD +4%"). A ladder whose rungs name a kind says what the worn one does in the
            // buff's own words - a tool's speed included, which follows the worn rung now (Option A), so the old
            // tool line read off the TOP rung would tell a bar wearing the steel tin that it shakes gold's speed.
            var wornNow = climbed > 0 ? run.WornRung(slot) : null;
            bool typed = wornNow != null && wornNow.Buff != null;
            string does = typed ? LadderBuffWords(run, wornNow) : null;
            string worth = top == null ? "" : (typed ? null : ToolWords(run, top, brief: true)) ?? (top.Comfort > 0
                ? UIText.T("decor.comfort", ("comfort", top.Comfort.ToString("0.0#", CultureInfo.InvariantCulture))) : "");
            if (does != null) worth = worth.Length > 0 ? worth + "  ·  " + does : does;
            status.text = total > 0
                ? (climbed == 0 ? UIText.T("decor.ladder.nothing_fitted")
                                : UIText.T("decor.ladder.mark_of", ("mark", climbed), ("total", total)))
                  + (worth.Length > 0 ? "  ·  " + UIText.Caps(worth) : "")
                : "";

            var rule = NewRect("Rule", head);
            rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0);
            rule.pivot = new Vector2(0.5f, 0);
            rule.sizeDelta = new Vector2(0, 2f);
            rule.anchoredPosition = new Vector2(0, 2f);
            var ri = rule.gameObject.AddComponent<Image>();
            ri.color = new Color(ShopViceDeep.r, ShopViceDeep.g, ShopViceDeep.b, 0.35f);
            ri.raycastTarget = false;

            var row = NewRect("Rungs_" + slot, _offerRow);
            var grid = row.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CardW, CardH);
            grid.spacing = new Vector2(CardGap, CardGap);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CardCols;
            _cardTarget = row;
            foreach (var f in rungs) DecorCard(run, f, where, climbed, total);
            if (slot == TycoonRun.CounterPaintFixture && run.CanRepaintCounter) FinishRow(run);
        }

        /// <summary>THE FINISHES, once the kit is owned (2026-09-16): five swatches cut from the counter's own drawing
        /// in each finish, the one on the bar lit; pressing one repaints it — the room and the benches follow at
        /// once (TycoonHud.StepCounterFinish), and the market redraws.</summary>
        private void FinishRow(TycoonRun run)
        {
            var head = NewRect("FinishHead", _offerRow);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            var t = NewText("T", head, _shop, 8, TextAnchor.LowerLeft, TileMetaInk);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 4), new Vector2(-2, 0));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = UIText.T("decor.finish.head");

            var row = NewRect("Finishes", _offerRow);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = CardGap; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false; h.childControlHeight = false;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var counter = stage != null ? stage.CounterArt : null;
            foreach (var id in TycoonRun.CounterFinishes)
            {
                string finish = id;
                bool on = run.CounterFinish == id;
                var cell = NewRect("Finish_" + id, row);
                cell.sizeDelta = new Vector2(CardW, 80f);
                var plate = cell.gameObject.AddComponent<Image>();
                plate.sprite = ChromeArt.Win98Key();
                plate.type = Image.Type.Sliced;
                plate.color = on ? PlateOrdered : ShopPage;
                if (!on)
                {
                    var button = cell.gameObject.AddComponent<Button>();
                    button.targetGraphic = plate;
                    button.onClick.AddListener(() =>
                    {
                        run.RepaintCounter(finish);
                        RememberScroll();
                        Sfx.Play("click", 0.7f);
                        RebuildDayEnd();
                    });
                    MarkHoverable(cell, plate);
                }
                var swatch = NewRect("Swatch", cell);
                Place(swatch, new Vector2(0.5f, 1f), new Vector2(CardW - 12f, 46f), new Vector2(0, -6f));
                var si = swatch.gameObject.AddComponent<Image>();
                si.sprite = CounterFinish.Swatch(counter, id);
                si.preserveAspect = true; si.raycastTarget = false;
                if (si.sprite == null) si.color = CounterFinish.Of(id).Accent;
                var name = NewText("N", cell, _shop, 8, TextAnchor.LowerCenter, on ? ShopInk : TileMetaInk);
                Stretch(name.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(2, 6), new Vector2(-2, 22));
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                name.text = UIText.T("decor.finish." + id) + (on ? " · " + UIText.T("decor.finish.on") : "");
            }
        }

        private static FixtureDefinition TopRung(List<FixtureDefinition> rungs, int level)
        {
            foreach (var r in rungs) if (r.Level == level) return r;
            return null;
        }

        /// <summary>What a TOOL's rung does for the work, in a few words — null for a piece
        /// that is only worth comfort. <paramref name="brief"/> is the card's one line, which
        /// has room for the speed and nothing after it; the reading card gets the whole of it.</summary>
        private static string ToolWords(TycoonRun run, FixtureDefinition f, bool brief = false)
        {
            string percent = f.WorkSpeed > 1.0001
                ? ((int)Math.Round((f.WorkSpeed - 1.0) * 100)).ToString(CultureInfo.InvariantCulture)
                : null;
            if (f.IsTap)
            {
                string kegs = UIText.N("decor.tool.kegs_on_tap", f.TapLevel);
                string pours = percent != null ? UIText.T("decor.tool.pours_faster", ("percent", percent)) : null;
                return brief ? (pours ?? kegs)
                             : (pours != null ? pours + " · " : "") + kegs;
            }
            if (f.IsDrain)
            {
                string seconds = (f.WashSeconds > 0 ? f.WashSeconds : Housekeeping.WashSeconds)
                    .ToString("0.#", CultureInfo.InvariantCulture);
                return UIText.T(brief ? "decor.tool.washes_brief" : "decor.tool.washes", ("seconds", seconds))
                       + (f.DrainsFree && !brief ? " · " + UIText.T("decor.tool.drains_free") : "");
            }
            if (f.Slot == "shaker")
                return percent != null ? UIText.T("decor.tool.shakes_faster", ("percent", percent))
                    : brief ? UIText.T("decor.tool.house_tin") : UIText.T("decor.tool.tin_you_shake");
            return null;
        }

        /// <summary>
        /// One rung. Four ways to be, and each says so in the card's plate, its corner stamp and
        /// its one control — never in colour alone (the market's state language, 16 §5):
        ///   WORN      in the room now                  green plate, WORN stamp, no control
        ///   OWNED     bought, not on show              WEAR key
        ///   NEXT      the rung the bar may buy         price and the basket's own ADD
        ///   LOCKED    waiting on a rung or a star      dimmed, the gate it waits on
        /// </summary>
        private void DecorCard(TycoonRun run, FixtureDefinition f, LastCall.Game.StageSlot where,
            int climbed, int total)
        {
            bool owned = run.OwnsFixture(f.Id);
            var wornRung = f.Level > 0 ? run.WornRung(f.Slot) : null;
            bool worn = owned && (f.Level == 0 || (wornRung != null && wornRung.Id == f.Id));
            bool rungLock = !owned && f.Level > 0 && !run.CanBuyRung(f);
            bool starLock = !owned && !rungLock && run.Rating.Average < f.Stars;
            bool locked = rungLock || starLock;
            var art = FixtureArt(f.Swatch ?? f.Sprite);

            string place = where != null && where.Place != null ? UIText.Data("slot", where.Id, "place", where.Place)
                         : where != null && where.OnCounter ? UIText.T("decor.card.place_counter")
                         : UIText.T("decor.card.place_room");
            string tool = ToolWords(run, f);
            string comfort = f.Comfort > 0
                ? UIText.T("decor.comfort", ("comfort", f.Comfort.ToString("0.0#", CultureInfo.InvariantCulture))) : null;
            string fixtureName = UIText.Data("fixture", f.Id, "name", f.Name);
            var spec = new TileSpec
            {
                Name = fixtureName,
                Art = art,
                CardArt = art,
                Identity = UIText.Caps(fixtureName),
                MetaLine = place + (f.Level > 0
                    ? " · " + UIText.T("decor.card.mark_of", ("mark", f.Level), ("total", total)) : ""),
                Body = UIText.Data("fixture", f.Id, "blurb", f.Flavor),
                BuffA = tool != null
                    ? new Buff(BuffKind.Gain, tool + (comfort != null
                        ? " · " + UIText.T("decor.card.to_room", ("comfort", comfort)) : ""))
                    : comfort != null ? new Buff(BuffKind.Gain, UIText.T("decor.card.to_room", ("comfort", comfort))) : null,
            };

            if (worn)
            {
                spec.State = TileState.Ordered;
                spec.BuffB = new Buff(BuffKind.Gain, f.Level > 0 && climbed > f.Level
                    ? UIText.T("decor.card.worn_stays", ("mark", climbed))
                    : UIText.T("decor.card.worn"));
            }
            else if (owned)
            {
                spec.State = TileState.Held;
                spec.BuffB = new Buff(BuffKind.Use, UIText.T("decor.card.owned", ("mark", climbed)));
                string id = f.Id;
                spec.OnClick = () =>
                {
                    if (!run.WearFixture(id)) { Sfx.Play("deny", 0.7f); return; }
                    RememberScroll();
                    Sfx.Play("click", 0.7f);
                    RebuildDayEnd();
                };
            }
            else if (rungLock)
            {
                spec.State = TileState.Sealed;
                spec.BuffB = new Buff(BuffKind.Bad, UIText.T("decor.card.rung_lock",
                    ("next", climbed + 1), ("mark", climbed)));
            }
            else if (starLock)
            {
                spec.State = TileState.Sealed;
                spec.BuffB = new Buff(BuffKind.Bad, UIText.T("decor.card.star_lock",
                    ("stars", f.Stars.ToString("0.0", CultureInfo.InvariantCulture)),
                    ("have", run.Rating.Average.ToString("0.0", CultureInfo.InvariantCulture))));
            }
            else
            {
                DressBuyable(spec, run.FixturePrice(f), "fx:" + f.Id, false, () => run.BuyFixture(f.Id));
                spec.BuffB = new Buff(BuffKind.Cost, UIText.T("decor.card.no_upgrade_spent"));
            }

            // WHAT IT DOES, AND WHETHER IT IS DOING IT (2026-09-23, the author: "Hangi geliştirme takılıysa o buff
            // aktif olacak, konfor gibi değil"): the rung's kind and figure, in the state Core says it is in, as the
            // card's badge row and the reading card's first rows; its comfort as the medal pip on the picture. The
            // old effect line (a tool's speed, the comfort to the room) is what those rows say now, so it goes -
            // except a tower's kegs, which are a fact about the tap and not a buff.
            var buffs = FittingShopBuffs(run, f, owned, locked);
            SetShopBuffs(spec, buffs);
            var leadBuff = LeadOf(buffs);
            if (buffs.Count > 0)
                spec.BuffA = f.IsTap ? new Buff(BuffKind.Use, UIText.N("decor.tool.kegs_on_tap", f.TapLevel)) : null;

            // ── the plate ──
            var rt = NewRect("Rung_" + f.Id, _cardTarget);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.Win98Key();
            img.type = Image.Type.Sliced;
            img.color = worn ? PlateOrdered : owned ? ShopPage : locked ? PlateSealed : PlateOf(spec.State);
            if (spec.OnClick != null)
            {
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                var act = spec.OnClick;
                button.onClick.AddListener(() => act());
                MarkHoverable(rt, img);
            }
            var shown = spec;
            var hover = rt.gameObject.AddComponent<HoverRelay>();
            hover.Entered = () => ShowShopCard(shown);
            hover.Exited = () => ShowShopCard(null);

            // ── the window, and the pattern in it ──
            var win = NewRect("Window", rt);
            Place(win, new Vector2(0.5f, 1), new Vector2(CardW - 8f, CardWinH), new Vector2(0, -4f));
            var wi = win.gameObject.AddComponent<Image>();
            wi.color = locked ? UITheme.Night[0] : UITheme.Night[2];
            wi.raycastTarget = false;
            win.gameObject.AddComponent<RectMask2D>();
            if (art != null)
            {
                var pic = NewRect("Art", win);
                FitCardArt(pic, art);
                var pi = pic.gameObject.AddComponent<Image>();
                pi.sprite = art;
                pi.raycastTarget = false;
                pi.color = locked ? new Color(1f, 1f, 1f, 0.42f) : Color.white;
            }

            // THE MARK, in the corner the ladder is read from: a rung is a number on a step.
            if (f.Level > 0)
            {
                var mark = NewRect("Mark", win);
                Place(mark, new Vector2(0, 1), new Vector2(24, 22), new Vector2(4, -4));
                var mi = mark.gameObject.AddComponent<Image>();
                mi.color = new Color(ShopViceDeep.r, ShopViceDeep.g, ShopViceDeep.b, 0.9f);
                mi.raycastTarget = false;
                var mt = NewText("N", mark, _display, 16, TextAnchor.MiddleCenter, Color.white);
                Stretch(mt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                mt.horizontalOverflow = HorizontalWrapMode.Overflow;
                mt.text = f.Level.ToString(CultureInfo.InvariantCulture);
            }

            if (worn) CardStamp(win, UIText.T("decor.stamp.worn"), ItemArt.Load("sh_g_tick"), UITheme.Lime[3]);
            else if (spec.State == TileState.Picked) CardStamp(win, UIText.T("decor.stamp.in_basket"), ItemArt.Load("sh_g_tick"), UITheme.Amber[3]);
            else if (locked) CardStamp(win, UIText.T("decor.stamp.locked"), ItemArt.Load("sh_b_lock"), new Color(0.86f, 0.87f, 0.92f, 1f));

            // ── the name, and the one thing it does ──
            var name = NewText("Name", rt, _shop, 16, TextAnchor.UpperLeft, locked ? ShopInkSoft : ShopInk);
            Place(name.rectTransform, new Vector2(0, 1), new Vector2(CardW - 16f, 40f), new Vector2(8f, -CardNameTop));
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.lineSpacing = 1.0f;
            name.text = UIText.Caps(fixtureName);

            // THE BUFF ROW (2026-09-23): the lead buff's badge under the name, and the comfort as the medal pip in
            // the picture's bottom-left, drawn after the stamps so nothing lies over it. A rung whose data names
            // no kind keeps the old fact line exactly as it was, so the comfort is never said twice.
            if (leadBuff != null)
            {
                ShopBadge(rt, leadBuff, new Vector2(0, 1), new Vector2(8f, -CardBuffTop));
                var pip = ComfortOf(buffs);
                if (pip != null) ComfortPip(win, Vector2.zero, new Vector2(4f, 4f), pip.Comfort, pip.Alpha);
            }
            else
            {
                string fact = ToolWords(run, f, brief: true)
                    ?? (comfort != null ? comfort
                        : f.Level > 0 ? UIText.T("decor.card.mark", ("mark", f.Level)) : UIText.T("decor.card.given"));
                float factX = 8f;
                if (tool == null && comfort != null)
                {
                    var medal = NewRect("Medal", rt);
                    Place(medal, new Vector2(0, 1), new Vector2(16, 16), new Vector2(8f, -CardMetaTop));
                    var md = medal.gameObject.AddComponent<Image>();
                    md.sprite = ItemArt.Medal(true, 16f);
                    md.preserveAspect = true; md.raycastTarget = false;
                    md.enabled = md.sprite != null;
                    factX = 28f;
                }
                var meta = NewText("Fact", rt, _shop, 8, TextAnchor.MiddleLeft, TileMetaInk);
                Place(meta.rectTransform, new Vector2(0, 1), new Vector2(CardW - factX - 8f, 16f), new Vector2(factX, -CardMetaTop));
                meta.horizontalOverflow = HorizontalWrapMode.Wrap;
                meta.verticalOverflow = VerticalWrapMode.Truncate;
                meta.text = UIText.Caps(fact);
            }

            // ── the foot: the one control ──
            if (worn)
                CardFootWord(rt, f.Level > 0 ? UIText.T("decor.foot.on_show") : UIText.T("decor.foot.in_room"),
                    ItemArt.Load("sh_g_tick"), StripStock);
            else if (owned)
            {
                var key = NewRect("Wear", rt);
                Place(key, new Vector2(0.5f, 1), new Vector2(CardW - 16f, 26f), new Vector2(0, -CardFootTop));
                var ki = key.gameObject.AddComponent<Image>();
                ki.sprite = ChromeArt.Win98Key();
                ki.type = Image.Type.Sliced;
                ki.color = StripReturn;
                ki.raycastTarget = false;
                var kt = NewText("L", key, _shop, 16, TextAnchor.MiddleCenter, Color.white);
                Stretch(kt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                kt.horizontalOverflow = HorizontalWrapMode.Overflow;
                kt.text = UIText.T("decor.foot.wear");
                var press = rt.gameObject.AddComponent<Win98Press>();
                press.Face = ki;
                press.Caption = kt.rectTransform;
            }
            else if (rungLock)
                CardFootWord(rt, UIText.T("decor.foot.mark_first", ("mark", climbed + 1)), ItemArt.Load("sh_b_lock"), StripSealed);
            else if (starLock)
            {
                StarRow(rt, new Vector2(0, 1), new Vector2(8f, -(CardFootTop + 7f)), 12f,
                    f.Stars, UITheme.Amber[3], new Color(0.3f, 0.28f, 0.34f, 0.25f));
            }
            else
                CardPriceFoot(rt, spec);

            if (spec.State == TileState.Picked) Frame(rt, 2f, StripPicked);
            if (worn) Frame(rt, 2f, StripStock);
        }

        /// <summary>A card's picture in its window: a swatch or a small drawing at the biggest
        /// whole multiple that fits (up to 3x), and a drawing wider than the window scaled
        /// down just enough to fit rather than cut.</summary>
        private static void FitCardArt(RectTransform rt, Sprite s)
        {
            float w = s.rect.width, h = s.rect.height;
            float k = Mathf.Min((CardW - 16f) / w, (CardWinH - 8f) / h);
            if (k >= 1f) k = Mathf.Min(3f, Mathf.Floor(k));
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(w * k, h * k), Vector2.zero);
        }

        private void CardStamp(RectTransform win, string word, Sprite mark, Color ink)
        {
            var stamp = NewRect("Stamp", win);
            var text = NewText("State", stamp, _shop, 8, TextAnchor.MiddleLeft, ink);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = word;
            float textW = text.preferredWidth;
            float markW = mark != null ? TileStateH + 4f : 0f;
            Place(stamp, new Vector2(1, 1), new Vector2(markW + textW + 12f, 20f), new Vector2(-4f, -4f));
            var bg = stamp.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.82f);
            bg.raycastTarget = false;
            if (mark != null)
            {
                var m = NewRect("StateMark", stamp);
                Place(m, new Vector2(0, 0.5f), new Vector2(TileStateH, TileStateH), new Vector2(6f, 0));
                var mi = m.gameObject.AddComponent<Image>();
                mi.sprite = mark; mi.preserveAspect = true; mi.raycastTarget = false; mi.color = ink;
            }
            Place(text.rectTransform, new Vector2(0, 0.5f), new Vector2(textW + 2f, TileStateH), new Vector2(6f + markW, 0));
        }

        private void CardFootWord(RectTransform card, string word, Sprite mark, Color ink)
        {
            if (mark != null)
            {
                var m = NewRect("FootMark", card);
                Place(m, new Vector2(0, 1), new Vector2(16, 16), new Vector2(8f, -(CardFootTop + 5f)));
                var mi = m.gameObject.AddComponent<Image>();
                mi.sprite = mark; mi.preserveAspect = true; mi.raycastTarget = false; mi.color = ink;
            }
            var t = NewText("Foot", card, _shop, 8, TextAnchor.MiddleLeft, ink);
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(CardW - 40f, 26f),
                new Vector2(mark != null ? 28f : 8f, -CardFootTop));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = word;
        }

        /// <summary>The price and the basket's key, set the way the aisle's own tiles set them.</summary>
        private void CardPriceFoot(RectTransform card, TileSpec spec)
        {
            bool dim = spec.State == TileState.Unaffordable;
            if (!string.IsNullOrEmpty(spec.Money))
            {
                // The price mark, drawn (2026-09-25), centred on the foot's 26-unit line.
                var sign = NewRect("Sign", card);
                Place(sign, new Vector2(0, 1), new Vector2(16f, 16f), new Vector2(8f, -(CardFootTop + 5f)));
                var signImg = sign.gameObject.AddComponent<Image>();
                signImg.sprite = ItemArt.Price(16f);
                signImg.preserveAspect = true;
                signImg.raycastTarget = false;
                signImg.color = dim ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
                var money = NewText("Money", card, _figures, 16, TextAnchor.MiddleLeft, dim ? ShopInkSoft : ShopInk);
                Place(money.rectTransform, new Vector2(0, 1), new Vector2(60f, 26f), new Vector2(24f, -CardFootTop));
                money.horizontalOverflow = HorizontalWrapMode.Overflow;
                money.text = spec.Money.Replace("$", "");
            }
            if (string.IsNullOrEmpty(spec.PillVerb)) return;
            var pill = NewRect("Pill", card);
            Place(pill, new Vector2(1, 1), new Vector2(64f, 24f), new Vector2(-8f, -(CardFootTop + 1f)));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.sprite = ChromeArt.Win98Key();
            pillImg.type = Image.Type.Sliced;
            pillImg.color = PillOf(spec.State);
            pillImg.raycastTarget = false;
            var label = NewText("L", pill, _shop, 8, TextAnchor.MiddleCenter, PillInk(spec.State));
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.text = spec.PillVerb;
            if (spec.OnClick != null)
            {
                var press = card.gameObject.AddComponent<Win98Press>();
                press.Face = pillImg;
                press.Caption = label.rectTransform;
            }
        }
    }
}
