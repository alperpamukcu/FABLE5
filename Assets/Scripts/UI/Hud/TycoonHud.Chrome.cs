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
            if (_toast == null) return;
            _toast.text = message;
            _toast.color = tint ?? _toastInk;
            _toastUntil = Time.unscaledTime + seconds;
            _toast.gameObject.SetActive(true);
        }

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
        private Text _propTipText;
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
            _propTipGroup = _propTip.gameObject.AddComponent<CanvasGroup>();
            _propTipGroup.alpha = 0f;
            _propTipGroup.blocksRaycasts = false;
            _propTipGroup.interactable = false;
            UiAuditExempt.Mark(_propTip, "the hover caption stands over whatever prop the "
                + "pointer is on, in that prop's own place rather than in a fixed one");
        }

        /// <summary>The pointer arrived on a prop: say what pressing it does.</summary>
        internal void ShowPropTip(RectTransform over, string word)
        {
            if (_propTip == null || over == null || string.IsNullOrEmpty(word)) return;
            _propTipOver = over;
            _propTipText.text = word;
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

            // THROUGH THE SCREEN, because the prop may not be on this canvas. The sink and
            // the beer font are hit plates on the stage's own overlay; a straight read of
            // their anchoredPosition would place the caption in the HUD's coordinates as if
            // it were the stage's, which is only the same thing by accident.
            var corners = new Vector3[4];
            over.GetWorldCorners(corners);
            var top = (corners[1] + corners[2]) * 0.5f;
            var screen = RectTransformUtility.WorldToScreenPoint(null, top);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_propTip.parent, screen, null, out Vector2 local))
                _propTip.anchoredPosition = local + new Vector2(0f, 8f);
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
            if (_tabletTill != null) _tabletTill.text = "$" + shown;
        }

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
            if (run != null)
                foreach (var b in run.Shelf.Bottles)
                {
                    var card = b.Ingredient;
                    if (card == null) continue;
                    if (card.Type == IngredientType.Garnish || card.Type == IngredientType.Beer)
                        continue;
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

        /// <summary>A fixture's sprite, from its own Resources shelf (PPU 1 — world art).</summary>
        private static Sprite FixtureArt(string name) =>
            string.IsNullOrEmpty(name) ? null : Resources.Load<Sprite>("Fixtures/" + name);

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
                : run.CrowdToday == WealthTier.HighRoller ? "TONIGHT · HIGH ROLLERS"
                : run.CrowdToday == WealthTier.Broke ? "TONIGHT · BROKE CROWD" : "TONIGHT · REGULARS";
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
            if (job == null || !job.RunsOn(run.Day)) { _jobStrip.text = ""; return; }
            string drink = job.RecipeName.ToUpperInvariant();
            _jobStrip.text = job.IsDone
                ? $"<color=#6FCC4B>{job.Who} · {drink} DONE</color>"
                : $"<color=#E84DA6>{job.Who}</color> · {job.Left} MORE {drink}";
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
            _jobStrip = NewText("WeekJob", root, _display, 8, TextAnchor.MiddleLeft,
                UITheme.Cream[3]);
            Place(_jobStrip.rectTransform, new Vector2(0, 1), new Vector2(300, 20),
                new Vector2(60, -66));
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
            _settingsPanel.gameObject.SetActive(!_settingsPanel.gameObject.activeSelf);
            RefreshSettings();
        }

        private void BuildSettings(RectTransform root)
        {
            _settingsPanel = NewRect("Settings", root);
            // 420, not 300: the dev rows say what they DO ("close now, open the market"),
            // and a button whose caption does not fit is a button with no caption.
            // 360, not 320: the last-call skip is a ninth row and a row that does not fit the
            // panel is a button nobody can press.
            Place(_settingsPanel, new Vector2(1, 1), new Vector2(420, 360), new Vector2(-16, -58));
            _settingsPanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];

            var title = NewText("T", _settingsPanel, _body, 10, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -18), Vector2.zero);
            title.text = "— SETTINGS —";

            _settingsVolume = SettingsRow(0, "VOLUME", () =>
            {
                Sound.Volume = Sound.Volume <= 0.05f ? 0.2f : Sound.Volume >= 0.95f ? 0.2f
                    : Sound.Volume + 0.2f;      // cycles 0.2 → 1.0 and wraps
                Sfx.Play("click");
                RefreshSettings();
            });
            _settingsMute = SettingsRow(1, "SOUND", () =>
            {
                Sound.Muted = !Sound.Muted;
                Sfx.Play("click");              // audible iff it just came back on — itself the test
                RefreshSettings();
            });
            // NEW RUN LIVES HERE NOW (2026-08-14, the author: "new run yazısını ayarların
            // içine taşı"). It was a key on the board, one thumb from the things pressed all
            // night, and it throws the night away — this is where a thing like that belongs.
            // It is the same verb the fresh-start dev row already called, so it takes that
            // row's place rather than becoming a tenth button that does the same thing.
            SettingsRow(3, "NEW RUN — day 1, empty bar", () =>
            { _bootstrap.StartNewRun(null); ToggleSettings(); });
            // THE WORKBENCH IS NOT A SETTING (2026-08-14, the author: "dev tool'u daha
            // verimli bir panele dönüştür bu şekilde seçmek zor oluyor. Ayarlarla dev toolu
            // ayır"). Five dev rows and three settings shared one 420-wide stack, so the
            // volume lived a thumb away from "throw this run away and jump two weeks", and
            // every dev row had to spell its whole job into a caption because there was
            // nowhere else to say it. Settings keeps what a player changes; everything a
            // DEVELOPER does moved to its own bench, which has room to group and to explain.
            SettingsRow(4, "DEV TOOLS — the bench, and the lineup table", () =>
            {
                ToggleSettings();
                ToggleDevBench();
            });

            _settingsMotion = SettingsRow(2, "MOTION", () =>
            {
                Motion.Reduced = !Motion.Reduced;
                Sfx.Play("click");
                RefreshSettings();
            });

            // THE BOOK LOST ITS DOOR WITH THE TILL (2026-08-26, the author: "kasa ve parayı
            // ana sahneden kaldır"). The register was the way into the night's ledger, and
            // taking the machine out of the room took the handle with it. It is not going
            // back onto the bar in another shape: the whole point of the removal is that
            // nothing counts money at you while you are serving. So it lives behind the cog,
            // with the other things you go and LOOK for rather than reach for — one row, and
            // the night's figures are still one press away for anybody who wants them.
            SettingsRow(5, "TONIGHT'S BOOK — every line the till has taken", () =>
            { ToggleSettings(); ToggleLedger(); });

            _settingsPanel.gameObject.SetActive(false);
        }

        private void ToggleDevBench()
        {
            if (_devPanel == null) return;
            bool show = !_devPanel.gameObject.activeSelf;
            if (show) { CloseId(); RefreshDevBench(); }
            _devPanel.gameObject.SetActive(show);
        }

        private void BuildDevBench(RectTransform root)
        {
            _devPanel = NewRect("DevBench", root);
            Place(_devPanel, new Vector2(0.5f, 0.5f), new Vector2(1180, 640), new Vector2(0, 6));
            var canvas = _devPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 25;                 // above the guide (24) and the market (22)
            _devPanel.gameObject.AddComponent<GraphicRaycaster>();
            var bg = _devPanel.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.985f);
            bg.raycastTarget = true;
            Frame(_devPanel, 2f, UITheme.Cyan[3]);    // cyan, not amber: this is not the game

            var title = NewText("T", _devPanel, _display, 16, TextAnchor.MiddleLeft, UITheme.Cyan[3]);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(600, 22), new Vector2(20, -18));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = "DEV BENCH";

            _devStanding = NewText("S", _devPanel, _body, 8, TextAnchor.MiddleRight, UITheme.Cream[2]);
            Place(_devStanding.rectTransform, new Vector2(1, 1), new Vector2(560, 12), new Vector2(-20, -22));
            _devStanding.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── the left rail: the verbs ────────────────────────────────────────
            int slot = 0;
            DevHeading(ref slot, "THE RUN");
            DevKey(ref slot, "NEW RUN", "day 1, empty bar",
                () => { _bootstrap.StartNewRun(null); ToggleDevBench(); });
            DevKey(ref slot, "MIDGAME", "day 12, stocked",
                () => { _bootstrap.StartNewRun(null); Run.DevPreset(1); ApplyBarLook(); ToggleDevBench(); });
            DevKey(ref slot, "ENDGAME", "late run, full shelf",
                () => { _bootstrap.StartNewRun(null); Run.DevPreset(2); ApplyBarLook(); ToggleDevBench(); });

            DevHeading(ref slot, "THE CLOCK");
            DevKey(ref slot, "SKIP TO DAY END", "close now, open the market", () =>
            {
                if (Run == null || Run.Phase != TycoonPhase.DayOpen) { Toast("NOT MID-DAY"); return; }
                _flow?.CloseFlow();
                CloseId();
                Run.DevSkipToDayEnd();
                ToggleDevBench();
            });
            DevKey(ref slot, "SKIP TO THE LAST CALL", "jump to the night, then run it out",
                DevJumpToLastCall);

            DevHeading(ref slot, "THE PEOPLE");
            DevKey(ref slot, "THE ROOM", "every drinker, papers and star",
                () => { ToggleDevBench(); ToggleGuide(); });

            // ── the right pane: the lineup ──────────────────────────────────────
            var head = NewText("H", _devPanel, _shop, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(head.rectTransform, new Vector2(0, 1), new Vector2(820, 12), new Vector2(348, -46));
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.text = "PRICE  NAME                          HOW IT IS MADE        WHAT IT ASKS FOR";

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

            NewButton(_devPanel, "CLOSE", new Vector2(0, 0), new Vector2(300, 32),
                new Vector2(20, 12), UITheme.Cyan[3], () => ToggleDevBench());
            _devPanel.gameObject.SetActive(false);
        }

        private void DevHeading(ref int slot, string text)
        {
            var t = NewText("DH", _devPanel, _shop, 8, TextAnchor.LowerLeft, UITheme.Cyan[3]);
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(DevRailW, 16),
                new Vector2(DevRailX, -52f - slot * 26f));
            t.rectTransform.pivot = new Vector2(0, 1);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = text;
            slot++;
        }

        /// <summary>One verb: its NAME on the key, and what it does underneath it rather than
        /// crammed inside it. That is the whole reason this panel exists.</summary>
        private void DevKey(ref int slot, string name, string what, Action onClick)
        {
            var row = NewRect("DK_" + name, _devPanel);
            Place(row, new Vector2(0, 1), new Vector2(DevRailW, 22),
                new Vector2(DevRailX, -52f - slot * 26f));
            row.pivot = new Vector2(0, 1);
            var btn = row.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var face = NewRect("Face", row);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(row, UITheme.Night[3], btn, face);
            var label = NewText("L", face, _body, 8, TextAnchor.MiddleLeft, UITheme.TextPrimary);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(8, KeyPlate.Throw), new Vector2(-8, 0));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = name;
            slot++;

            var note = NewText("N_" + name, _devPanel, _body, 8, TextAnchor.UpperLeft, UITheme.Cream[2]);
            Place(note.rectTransform, new Vector2(0, 1), new Vector2(DevRailW - 8f, 14),
                new Vector2(DevRailX + 8f, -52f - slot * 26f + 8f));
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
            _devStanding.text = $"standing {stars:0.00}★ · day {run.Day} · ${run.Money} · "
                              + $"{run.MenuRecipes.Count} pages on the menu · "
                              + $"{run.Shelf.Bottles.Count} bottles on the wall";

            // Every page in the book and every bottle in the catalogue, filed under the rung
            // that opens it. The bottle's rung is its own lock's answer, so the table cannot
            // disagree with the shop — both ask the same object.
            var rungs = new SortedDictionary<double, List<(string line, Color ink)>>();
            void File(double rung, string line, Color ink)
            {
                if (!rungs.TryGetValue(rung, out var list))
                    rungs[rung] = list = new List<(string, Color)>();
                list.Add((line, ink));
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
                string how = r.Prep.ToString().ToUpperInvariant()
                           + (string.IsNullOrEmpty(r.GlassId) ? "" : " · " + r.GlassId);
                File(gate, $"${run.RecipePrice(r),-4} {r.Name,-28} {how,-20} {bands}",
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
                File(rung, $"${card.Info.Price,-4} {card.Name,-28} "
                         + $"{card.Info.Category + " · tier " + card.Info.Tier,-20} "
                         + (owned ? "ON THE WALL" : "stock"),
                    owned ? UITheme.Lime[3] : UITheme.Cream[2]);
            }

            foreach (var rung in rungs)
            {
                bool reached = stars + 1e-9 >= rung.Key;
                // A SEALED RUNG IS THE MOST INTERESTING ROW ON THE TABLE and it was drawn in
                // the beam's own shade on a black panel — invisible, so the reader saw a gap
                // between two blocks and no reason for it. Dimmer than an open rung, never
                // dimmer than the rows under it.
                var header = NewText("Rung", _devRows, _shop, 8, TextAnchor.MiddleLeft,
                    reached ? UITheme.PrimaryAction : UITheme.Cyan[3]);
                var hr = header.rectTransform;
                hr.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
                header.horizontalOverflow = HorizontalWrapMode.Overflow;
                header.text = $"  ★ {rung.Key:0.0}   {rung.Value.Count} LINES"
                            + (reached ? "   — OPEN" : "   — SEALED");

                foreach (var (line, ink) in rung.Value)
                {
                    var row = NewText("L", _devRows, _shop, 8, TextAnchor.MiddleLeft, ink);
                    row.rectTransform.gameObject.AddComponent<LayoutElement>().preferredHeight = 13f;
                    row.horizontalOverflow = HorizontalWrapMode.Overflow;
                    row.text = "    " + line;
                }
            }
        }

        /// <summary>The last-call jump, lifted out of the settings stack unchanged.</summary>
        private void DevJumpToLastCall()
        {
            if (Run == null || Run.Phase != TycoonPhase.DayOpen) { Toast("NOT MID-DAY"); return; }
            if (Run.Story == null) { Toast("THIS RUN HAS NO STORY"); return; }
            if (Run.LastCustomer != null) { Toast("THEY ARE ALREADY AT THE BAR"); return; }
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
            _guidePanel.gameObject.AddComponent<GraphicRaycaster>();
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

        private Text SettingsRow(int index, string label, Action onClick)
        {
            var row = NewRect($"Row{index}", _settingsPanel);
            Place(row, new Vector2(0.5f, 1), new Vector2(396, 30), new Vector2(0, -24f - index * 34f));
            var btn = row.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            // THE ONE KEY (GDD 16 §2). These were bare rects that did not even press — the
            // fourth dialect, and the one the author named first: a menu of things you click
            // where nothing answers the click.
            var face = NewRect("Face", row);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(row, UITheme.Night[3], btn, face);
            // THE LABEL WAS TAKEN AND NEVER WRITTEN (2026-08-10). Only the three settings
            // rows had text, because RefreshSettings assigns theirs afterwards — every dev
            // button was a blank slab you had to have written to know what it did.
            var text = NewText("L", face, _body, 8, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(8, KeyPlate.Throw), new Vector2(-8, 0));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = label;
            return text;
        }

        private void RefreshSettings()
        {
            if (_settingsVolume == null) return;
            _settingsVolume.text = $"VOLUME  {Mathf.RoundToInt(Sound.Volume * 100)}%";
            _settingsMute.text = Sound.Muted ? "SOUND  OFF" : "SOUND  ON";
            _settingsMotion.text = Motion.Reduced ? "MOTION  REDUCED" : "MOTION  FULL";
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

        /// <summary>The listen key: one line at a time, and the last one starts the clock.</summary>
        private void OnPlateKey()
        {
            var run = Run;
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
