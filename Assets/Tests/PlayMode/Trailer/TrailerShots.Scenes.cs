using System.Collections;
using System.Collections.Generic;
using LastCall.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LastCall.PlayTests.Trailer
{
    /// <summary>
    /// THE SHOTS (2026-10-02, the author: "tüm sahneler farklı uyumlu şekilde tasarlansın, ilk gelen karakter ve müşteri
    /// roxy olsun, dekorlar hep aynı olmasın; dolu ve pozitif bir fatura; mutlu müşteriler; çeşitli şişeler ve bar
    /// tasarımları"). One story, a different room in every shot:
    ///
    ///   S00_roxy        night one, the shabby bar as it is handed over - Roxy walks in and talks (her house tour)
    ///   S01_roxy_serve  a tropical dressing - Roxy on a stool, the first drink served is hers (the story's guest)
    ///   S02_serve_N     the 2-star club (chevron, stripes) - drinkers served one by one, their faces
    ///   S03_kick        the 2.5-star wave room - the card that lies, KICK
    ///   S04_close       the 3-star deco room - a good night worked off camera, the full slip, the market, the next night
    ///   S05_rush        the 1.5-star stucco room - every stool taken
    ///   S06_bottles     the 4-star shelf - bottle after bottle on the bench
    ///   S07_rooms       one fixed frame, the room dressed up rung by rung - the makeover
    ///
    /// Each shot sets its bar OFF camera, rolls, plays, marks its beats and cuts. The edit asks for shots by name and
    /// beats by mark, so a shot may run long.
    /// </summary>
    public sealed partial class TrailerShots
    {
        /// <summary>The bar to the doors, then (when <paramref name="stars"/> > 0) the preset for that standing and a
        /// reload around the same run, so every panel is built against it. Off camera.</summary>
        private IEnumerator SetTheBar(double stars, bool onAMonday = false)
        {
            yield return LoadToTheDoor();
            yield return WalkInAndOpen();
            if (stars <= 0) { Hush(); KeepTheLampsLow(); yield break; }
            _boot.Tycoon.DevPresetStars(stars);
            // the story's guest comes on Mondays (days 1, 7, 13, ...): the calendar wound on to the next one
            if (onAMonday)
            {
                int day = _boot.Tycoon.Day + 1;
                while (day % 6 != 1) day++;
                _boot.Tycoon.DevJumpToNight(day);
            }
            var old = _boot;
            _boot.ReloadKeepingRun();
            bool back = false;
            yield return Until(() =>
            {
                var b = Object.FindFirstObjectByType<LastCall.Game.GameBootstrap>();
                if (b == null || b == old || b.Tycoon == null) return false;
                _boot = b;
                return b.Tycoon.Phase == TycoonPhase.DayOpen && b.Tycoon.Floor.Elapsed > 0;
            }, 40f, ok => back = ok);
            if (!back) yield return HearTheHostOut();
            yield return Until(() => _boot != old && _boot.Tycoon.Floor.Elapsed > 0, 20f, ok => back = ok);
            Assert.That(back, Is.True, "the bar never reopened after the preset");
            yield return HearTheHostOut();
            Hush();
            KeepTheLampsLow();
            yield return Hold(1f);
        }

        /// <summary>
        /// Roxy's first-time lessons, already heard (2026-10-02: the first filmed take had "Click the stool, not the
        /// drink" standing over the licence). The trailer shows the bar, not the tutorial: every cue is marked taught
        /// and anything already queued is let go. S00 keeps hers - there she IS the shot.
        /// </summary>
        private void Hush()
        {
            var run = _boot.Tycoon;
            if (run.Story != null)
                foreach (StoryCue cue in System.Enum.GetValues(typeof(StoryCue))) run.Story.Learn(cue);
            for (int i = 0; i < 20 && run.LessonDue != null; i++) run.HeardLesson();
        }

        /// <summary>Puts these fittings in the room (each must outrank what the slot shows to change it).</summary>
        private void Dress(IEnumerable<string> fittings)
        {
            foreach (var id in fittings)
            {
                try { _boot.Tycoon.DevFit(id); }
                catch (System.Exception e) { Debug.LogWarning("[trailer] cannot fit " + id + ": " + e.Message); }
            }
        }

        // the dressings (fixtures.json ids), each valid on a fresh night-one bar, every piece above the starting rung
        private static readonly string[] Tropical =
        {
            "back7_wave", "rwall6_tile", "ceil_palm", "floor_terra", "rug6_palm", "picS_trioA", "post_surf_R",
            "picR6_sunset", "wall_lamps_three", "neon_flamingo", "table_v1_L", "table_v1_R", "plant5_yucca_L", "plant_agave",
        };

        /// <summary>The room's rungs as the shop would sell them, standing by standing: each step outranks the last, so
        /// fitting them in order dresses an empty room up one visible change at a time.</summary>
        private static readonly string[][] Makeover =
        {
            new[] { "walls_2", "walls_right_2", "ceil8_shack", "floor_carpet", "rug_leopard", "art_city", "post_malibu_R", "pic_pelican", "plant_palm", "plant_agave" },
            new[] { "walls_3", "walls_right_3", "ceil_palm", "floor_marble", "rug_wave", "picS_trio", "post_surf_R", "picR6_sunset", "neon_martini", "wall_lamps_one", "table_v1_L", "table_v1_R" },
            new[] { "walls_right_4", "ceil_stucco", "floor_check", "rug6_memphis", "picS_trioA", "post_pier_R", "counter_lamps_bell", "wall_lamps_two", "neon_flamingo", "table_v2_L", "table_v2_R", "plant5_yucca_L", "sink_brass" },
            new[] { "back6_chevron", "rwall6_stripe", "ceil6_grid", "floor6_grid", "rug6_sunset", "post_coast_R", "picR6_conv", "neon6_sun" },
            new[] { "back7_wave", "rwall6_tile", "ceil6_ray", "floor6_chip", "rug6_palm", "picS_trioB", "post5_neon", "neon6_glass", "lamp6_neonbar" },
            new[] { "walls_4", "rwall7_deco", "ceil7_dusk", "floor7_marble", "flamingo_triptych", "post5_car", "picR6_shapes", "lamp6_shell", "neon6_bird", "table_left_3", "table_right_3", "plant_pothos", "counter_paint" },
        };

        // ── the plate: Roxy's lines, heard one key at a time ───────────────────────────────────────────────────

        private static RectTransform PlateKey()
        {
            var plate = Find("LastCallPlate");
            if (!Shown(plate)) return null;
            var key = Find("Listen", plate);
            return Shown(key) ? key : null;
        }

        /// <summary>Presses GO ON for every line the plate shows, leaving each up long enough to read on film, until the
        /// plate has been quiet for <paramref name="quiet"/> seconds.</summary>
        private IEnumerator ReadThePlate(float perLine = 1.6f, float quiet = 1.2f, float atMost = 60f)
        {
            float until = Time.unscaledTime + atMost, lastSeen = Time.unscaledTime;
            while (Time.unscaledTime < until && Time.unscaledTime - lastSeen < quiet)
            {
                var key = PlateKey();
                if (key == null) { yield return null; continue; }
                yield return Hold(perLine);
                key = PlateKey();
                if (key != null) yield return ClickFace(key);
                lastSeen = Time.unscaledTime;
            }
        }

        // ── S00: Roxy walks into the bar on night one ─────────────────────────────────────────────────────────

        [UnityTest, Timeout(600000)]
        public IEnumerator S00_roxy()
        {
            yield return LoadToTheDoor();
            LastCall.Game.GameBootstrap.TourForNewRuns = true;             // her house tour opens night one
            TrailerCamera.Roll("S00_roxy");
            TrailerCamera.EachFrame = TrackRoxy();
            TrailerCamera.Mark("menu");
            var newRun = (RectTransform)GameObject.Find("MainMenu/Column/NEW RUN").transform;
            yield return Hold(2.0f);
            for (int attempt = 0; attempt < 6 && newRun != null && newRun.gameObject.activeInHierarchy; attempt++)
            {
                yield return ClickFace(newRun);
                yield return Hold(0.4f);
            }
            LastCall.Game.GameBootstrap.TourForNewRuns = false;
            yield return Until(() =>
            {
                var b = Object.FindFirstObjectByType<LastCall.Game.GameBootstrap>();
                if (b != null && b.Tycoon != null) _boot = b;
                return GameObject.Find("Hostess") != null;
            }, 30f);
            TrailerCamera.Mark("roxy walks in");
            yield return Until(() => PlateKey() != null, 15f);
            TrailerCamera.Mark("roxy talks");
            // her opening lines - the welcome, the clock, the till, the bill, the stars, the house - then the night opens
            var run = _boot.Tycoon;
            int lines = 0;
            float until = Time.unscaledTime + 60f;
            while (run.TourRunning && Time.unscaledTime < until)
            {
                if (run.TourStep.Wait != TourWait.Heard || lines >= 9) { run.SkipTour(); break; }
                var key = PlateKey();
                if (key != null)
                {
                    yield return Hold(1.7f);
                    yield return ClickFace(key);
                    lines++;
                    continue;
                }
                yield return null;
            }
            // she stays for the first job: her offer, heard out, then she walks off
            TrailerCamera.Mark("first job");
            yield return ReadThePlate(1.4f);
            yield return Until(() => run.HostessVisit == null, 8f);
            TrailerCamera.Mark("roxy leaves");
            yield return Hold(2.5f);
            TrailerCamera.Cut();
        }

        // ── S01: Roxy on a stool - the first drink is hers ────────────────────────────────────────────────────

        [UnityTest, Timeout(1200000)]
        public IEnumerator S01_roxy_serve()
        {
            yield return SetTheBar(0);
            var run = _boot.Tycoon;
            Dress(Tropical);
            run.DevForceLastCall = true;                                   // the story's guest of the house, night one
            // the night worked out off camera, so the floor is empty at closing when she comes in
            bool done = false;
            float until = Time.unscaledTime + 240f;
            while (!done && Time.unscaledTime < until)
            {
                if (run.HostessVisit != null) run.HearHostess();         // she takes the stool, not the floor
                if (run.Talking) yield return HearTheHostOut();
                if (run.Phase != TycoonPhase.DayOpen) break;
                if (run.Floor.IsClosingTime && run.Floor.Seated.Count == 0) { done = true; break; }
                run.Tick(1.0);
                yield return null;
                WatchStools();
            }
            Assert.That(done, Is.True, "the night never emptied at closing for her");
            TrailerCamera.Roll("S01_roxy_serve");
            CustomerVisit roxy = null;
            yield return Until(() =>
            {
                WatchStools();
                roxy = run.LastCustomer;
                return roxy != null && _stoolOf.ContainsKey(roxy);
            }, 30f);
            Assert.That(roxy != null && _stoolOf.ContainsKey(roxy), Is.True, "the guest of the house never sat down");
            var seat = _stoolOf[roxy];
            TrailerCamera.EachFrame = FoldBalloons();
            yield return WaitUntilSettled(seat);
            TrailerCamera.Mark("seated");
            MarkFocus("roxy", seat);
            yield return ReadThePlate(1.6f);                               // her ask; the last key starts the trial
            TrailerCamera.Mark("card open");                               // her order is in what she says (GDD 26 §3)
            yield return MakeTheOrder(roxy, seat);
            yield return ReadThePlate(1.6f);                               // what she says about it
            yield return Hold(2f);
            TrailerCamera.Cut();
        }

        // ── S02: the crowd, served - one take per drinker ─────────────────────────────────────────────────────

        /// <summary>
        /// Drinkers are served one after another, each in a take of their own (S02_serve_1, _2, ...), until the reel
        /// holds a shaken drink, a built one and a pint and enough happy faces for the montage, or eight takes. The
        /// order is on the card and nowhere else, so which drink a take holds is only known once it is read.
        /// </summary>
        [UnityTest, Timeout(2400000)]
        public IEnumerator S02_serve()
        {
            yield return SetTheBar(2.0);
            bool shaken = false, built = false, pint = false;
            for (int take = 1; take <= 8 && !(shaken && built && pint && take > 5); take++)
            {
                CustomerVisit visit = null;
                yield return Admit(v => true, 900f, v => visit = v);
                if (visit == null) break;
                var seat = _stoolOf[visit];
                TrailerCamera.Roll("S02_serve_" + take);
                yield return ReadTheCard(visit, seat, lingerOnTheOrder: take == 1);
                var recipe = visit.Order.Wanted;
                TrailerCamera.Mark("order " + recipe.Id + " " + recipe.Prep);
                yield return MakeTheOrder(visit, seat);
                yield return Hold(1f);
                TrailerCamera.Cut();
                if (recipe.Id == "draught" || recipe.GlassId == "pint") pint = true;
                else if (recipe.Prep == PrepMethod.Shaken) shaken = true;
                else built = true;
                yield return CleanUpAfter();
            }
        }

        /// <summary>Between takes, off camera: whatever is still open, put away.</summary>
        private IEnumerator CleanUpAfter()
        {
            yield return PutTheCardDown();
            if (Shown(Find("ShakerPanel")) || Shown(Find("ServePanel")) || Shown(Find("TapPanel")))
                yield return Tap(_keys.escapeKey);
            yield return CloseTheCellar();
            yield return Hold(0.3f);
        }

        // ── S03: the door - a card that lies, and KICK ───────────────────────────────────────────────────────

        /// <summary>
        /// The card that lies. Waiting for a young face and hoping was the first version, and its only take was an
        /// honest 27-year-old. The camera is a director, not a player: each drinker's card is read OFF camera through
        /// Core (the same verb the sim bot uses), and only a minor or a forged card is filmed - the click on the stool
        /// opens the card again for the camera, and KICK.
        /// </summary>
        [UnityTest, Timeout(1800000)]
        public IEnumerator S03_kick()
        {
            yield return SetTheBar(2.5);
            var run = _boot.Tycoon;
            Assert.That(run.Has(Feature.Door), Is.True, "the preset did not open the door rung");
            for (int attempt = 0; attempt < 60; attempt++)
            {
                CustomerVisit visit = null;
                yield return Admit(v => true, 900f, v => visit = v);
                if (visit == null) break;
                yield return Until(() => visit.HasOrdered, 25f);
                if (!visit.HasOrdered || visit.State != VisitState.Waiting) continue;
                visit.InspectId();
                if (!visit.Papers.ShouldBeKicked) continue;
                var seat = _stoolOf[visit];
                TrailerCamera.Roll("S03_kick");
                yield return Hold(1.2f);
                yield return ReadTheCard(visit, seat, lingerOnTheOrder: false);
                TrailerCamera.Mark("liar");
                TrailerCamera.Mark("papers " + visit.Papers.Forgery + (visit.Papers.IsMinor ? " minor" : ""));
                yield return Hold(1.4f);                               // long enough to see what is wrong with it
                var kick = Find("Kick", Find("IdCard"));
                Assert.That(Shown(kick), Is.True, "the card has no KICK key");
                yield return ClickFace(kick);
                TrailerCamera.Mark("kicked");
                yield return Hold(3f);
                TrailerCamera.Cut();
                yield break;
            }
            Assert.Inconclusive("no minor or forged card came in tonight - film it again with another seed");
        }

        // ── S04: the close - a good night's slip, the market, the next night ────────────────────────────────

        [UnityTest, Timeout(1200000)]
        public IEnumerator S04_close()
        {
            yield return SetTheBar(3.0);
            var run = _boot.Tycoon;
            // a busy, good night, worked off camera so the slip has a full tape and a profit on it - up to closing time
            yield return WorkTheNightOffCamera(100000, untilClosing: true);
            // ON CAMERA FROM THE CLOSE (the author: "gün sonu kapanış ekranı gösterilsin"): the doors shut, the curtain,
            // then the slip
            TrailerCamera.Roll("S04_close");
            TrailerCamera.Mark("closing");
            yield return Hold(0.8f);
            if (run.Phase == TycoonPhase.DayOpen) run.DevSkipToDayEnd();
            bool slip = false;
            yield return Until(() => Shown(Find("BillNext")), 60f, ok => slip = ok);
            Assert.That(slip, Is.True, "the night's slip never came up");
            TrailerCamera.Mark("slip");
            yield return Hold(6f);                                     // the tape, the stars, the stamp
            TrailerCamera.Mark("slip done");
            for (int i = 0; i < 5 && !Shown(Find("Basket")); i++)
            {
                if (Shown(Find("BillNext"))) yield return ClickFace(Find("BillNext"));
                yield return Hold(0.6f);
            }
            TrailerCamera.Mark("market");
            yield return Hold(0.8f);
            yield return HearTheHostOut();
            yield return ReadThePlate(1.2f, 0.8f, 20f);
            for (int tab = 0; tab < 4; tab++)
            {
                var key = Find("Tab" + tab);
                if (!Shown(key)) continue;
                yield return ClickFace(key);
                yield return Hold(0.9f);
            }
            var open = Find("OpenTomorrow");
            var caption = open != null ? open.GetComponentInChildren<UnityEngine.UI.Text>() : null;
            for (int i = 0; i < 6 && caption != null && !caption.text.Contains("ORDER"); i++)
            {
                var tile = NthTile(i);
                if (tile == null) break;
                yield return ClickFace(tile);
                yield return Hold(0.3f);
            }
            if (caption != null && caption.text.Contains("ORDER"))
            {
                yield return ClickFace(open);
                TrailerCamera.Mark("bought");
                yield return Hold(1.2f);
            }
            yield return ClickFace(Find("OpenTomorrow"));
            yield return Hold(0.5f);
            var ask = Find("ClosingAsk");
            if (Shown(ask)) yield return ClickFace(ask.Find("Card/Anyway") as RectTransform);
            int leaving = run.Day;
            bool next = false;
            yield return Until(() =>
            {
                if (run.Talking || PlateKey() != null) return false;
                return run.Day != leaving && run.Phase == TycoonPhase.DayOpen && run.Floor.Elapsed > 0;
            }, 30f, ok => next = ok);
            if (!next) { yield return HearTheHostOut(); yield return ReadThePlate(1.2f, 0.8f, 20f); }
            TrailerCamera.Mark("next night");
            yield return Hold(3f);
            TrailerCamera.Cut();
        }

        private static RectTransform NthTile(int index)
        {
            var tiles = new List<RectTransform>();
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (rt.name == "Tile" && rt.gameObject.activeInHierarchy) tiles.Add(rt);
            tiles.Sort((a, b) =>
            {
                int byRow = b.position.y.CompareTo(a.position.y);
                return byRow != 0 ? byRow : a.position.x.CompareTo(b.position.x);
            });
            return index < tiles.Count ? tiles[index] : null;
        }

        // ── S05: the rush - every stool taken, the hand going card to card ──────────────────────────────────

        [UnityTest, Timeout(900000)]
        public IEnumerator S05_rush()
        {
            yield return SetTheBar(1.5);
            var run = _boot.Tycoon;
            for (int i = 0; i < 1200 && run.Floor.Seated.Count < run.Seats; i++)
            {
                if (run.Talking || run.HostessVisit != null) yield return HearTheHostOut();
                run.Tick(0.5);
                yield return null;
                WatchStools();
            }
            yield return Hold(2f);
            TrailerCamera.Roll("S05_rush");
            TrailerCamera.Mark("full room");
            yield return Hold(2.5f);
            int read = 0;
            foreach (var kv in new List<KeyValuePair<CustomerVisit, RectTransform>>(_stoolOf))
            {
                if (read >= 3) break;
                var visit = kv.Key;
                if (visit.State != VisitState.Waiting || !visit.HasOrdered || visit.IdInspected) continue;
                yield return Click(kv.Value, BodyOf(kv.Value) - ScreenPointOf(kv.Value));
                yield return Hold(1.6f);
                yield return PutTheCardDown();
                read++;
            }
            yield return Hold(4f);
            TrailerCamera.Cut();
        }

        // ── S06: the bottles - one after another on the bench ────────────────────────────────────────────────

        [UnityTest, Timeout(900000)]
        public IEnumerator S06_bottles()
        {
            yield return SetTheBar(4.0);
            var run = _boot.Tycoon;
            // spirits first, then the rest of the shelf (never the kegs); at most ten
            var picks = new List<ShelfBottle>();
            foreach (var b in run.Shelf.Bottles)
                if (!b.IsEmpty && b.Ingredient.Type == IngredientType.Spirit) picks.Add(b);
            foreach (var b in run.Shelf.Bottles)
                if (!b.IsEmpty && b.Ingredient.Type != IngredientType.Spirit && b.Ingredient.Type != IngredientType.Beer) picks.Add(b);
            if (picks.Count > 10) picks.RemoveRange(10, picks.Count - 10);
            Assert.That(picks.Count, Is.GreaterThan(0), "the shelf is bare");

            TrailerCamera.Roll("S06_bottles");
            yield return OpenTheCellar("CellarDoor_" + picks[0].Id);
            TrailerCamera.Mark("cellar");
            foreach (var b in run.Shelf.Bottles)
            {
                var door = Find("CellarDoor_" + b.Id);
                if (!Shown(door)) continue;
                yield return Glide(FaceOf(door), 0.22f);
            }
            for (int k = 0; k < picks.Count; k++)
            {
                if (k > 0) yield return StepBack();
                string name = "CellarDoor_" + picks[k].Id;
                if (!DoorAnswers(name)) yield return OpenTheCellar(name);
                yield return Click(Find(name));
                yield return Until(() => Shown(Find("ShakerPanel")), 3f);
                TrailerCamera.Mark("bottle " + (k + 1));
                // a short pour from every bottle, building like the real one (the author: "farklı dökülen alkol
                // şişeleri belli periyotlarla hızlı hızlı gözükmeli"); the tin is binned before it can brim
                yield return PourBurst(picks[k].Id, 0.9f);
                if (run.Glass.FillFraction > 0.75)
                {
                    var bin = Find("Bin", Find("ShakerPanel"));
                    if (Shown(bin)) { yield return ClickFace(bin); yield return Hold(0.4f); }
                }
            }
            yield return StepBack();
            yield return Hold(1f);
            TrailerCamera.Cut();
        }

        // ── S07: the makeover - one frame, the room dressed up rung by rung ──────────────────────────────────

        [UnityTest, Timeout(900000)]
        public IEnumerator S07_rooms()
        {
            yield return SetTheBar(0);
            var run = _boot.Tycoon;
            // a few drinkers in, so the room is alive while it changes
            for (int i = 0; i < 600 && run.Floor.Seated.Count < Mathf.Min(3, run.Seats); i++)
            {
                if (run.Talking || run.HostessVisit != null) yield return HearTheHostOut();
                run.Tick(0.5);
                yield return null;
            }
            Set(_mouse.position, new Vector2(Screen.width * 0.97f, Screen.height * 0.05f));   // the hand out of the frame's way
            yield return Hold(1.5f);
            TrailerCamera.Roll("S07_rooms");
            TrailerCamera.Mark("room 0");
            yield return Hold(1.6f);
            for (int k = 0; k < Makeover.Length; k++)
            {
                Dress(Makeover[k]);
                TrailerCamera.Mark("room " + (k + 1));
                yield return Hold(1.6f);
            }
            yield return Hold(1.5f);
            TrailerCamera.Cut();
        }

        // ── S08: the end card's picture - the last customer, Roxy, alone under the lamp ─────────────────────

        /// <summary>
        /// THE END CARD IS A FRAME OF THE GAME (2026-10-02, the author: "Wishlist görseli pixel art gibi değil, kutu
        /// kutu ve kalitesiz; neden kimlik var; roxynin de dahil olduğu bir bütün pixel art"). The 4-star room, the
        /// night over, the floor empty - and the story's guest of the house walks in and takes a stool under the
        /// last-call lamp. The top bar and her plate are folded away while it rolls, the hand is off the frame; the
        /// edit sets the sign and the call to action over this picture.
        /// </summary>
        [UnityTest, Timeout(1200000)]
        public IEnumerator S08_endcard()
        {
            yield return SetTheBar(4.0, onAMonday: true);
            var run = _boot.Tycoon;
            run.DevForceLastCall = true;
            bool empty = false;
            float until = Time.unscaledTime + 300f;
            while (!empty && Time.unscaledTime < until)
            {
                if (run.HostessVisit != null) run.HearHostess();
                if (run.Talking) yield return HearTheHostOut();
                if (run.Phase != TycoonPhase.DayOpen) break;
                if (run.Floor.IsClosingTime && run.Floor.Seated.Count == 0) { empty = true; break; }
                run.Tick(1.0);
                yield return null;
                WatchStools();
            }
            Assert.That(empty, Is.True, "the night never emptied at closing for her");
            Set(_mouse.position, new Vector2(-200f, -200f));          // no hand in this picture
            TrailerCamera.Roll("S08_endcard");
            var fold = FoldBalloons();
            TrailerCamera.EachFrame = () => { fold(); FoldTheChrome(); };
            CustomerVisit roxy = null;
            float wait = Time.unscaledTime + 30f;
            while (Time.unscaledTime < wait)
            {
                FoldTheChrome();
                WatchStools();
                roxy = run.LastCustomer;
                if (roxy != null && _stoolOf.ContainsKey(roxy)) break;
                yield return null;
            }
            Assert.That(roxy != null && _stoolOf.ContainsKey(roxy), Is.True, "the guest of the house never sat down");
            TrailerCamera.Mark("roxy walks in");
            var seat = _stoolOf[roxy];
            var last = seat.anchoredPosition;
            int still = 0;
            while (still < 8 && Time.unscaledTime < wait + 20f)
            {
                FoldTheChrome();
                yield return null;
                if ((seat.anchoredPosition - last).sqrMagnitude < 0.01f) still++;
                else { still = 0; last = seat.anchoredPosition; }
            }
            TrailerCamera.Mark("roxy sits");
            MarkFocus("roxy", seat);
            float hold = Time.unscaledTime + 5f;
            while (Time.unscaledTime < hold) { FoldTheChrome(); yield return null; }
            TrailerCamera.Cut();
        }

        /// <summary>The HUD's own chrome, scaled to nothing for a clean picture (re-applied every frame, because the
        /// HUD lays its rects out every frame).</summary>
        private static void FoldTheChrome()
        {
            foreach (var name in new[] { "TopBar", "LastCallPlate", "QuestBubble", "HostNote" })
            {
                var rt = Find(name);
                if (rt != null) rt.localScale = Vector3.zero;
            }
        }
    }
}
