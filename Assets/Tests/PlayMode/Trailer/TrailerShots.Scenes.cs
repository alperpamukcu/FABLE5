using System.Collections;
using LastCall.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LastCall.PlayTests.Trailer
{
    /// <summary>
    /// THE SHOTS. Each sets its bar up OFF camera (the door, a standing preset, the clock run on until the right
    /// drinker walks in), rolls, plays, marks its beats and cuts. The edit (Tools/trailer/cuts/*.json) asks for them
    /// by name and by mark, so a shot may run long - only the marked moments are used.
    /// </summary>
    public sealed partial class TrailerShots
    {
        /// <summary>The standing most shots are filmed at: the spoon, the garnish rail and the door are open, the room
        /// has its first fittings, and the book is past its first page.</summary>
        private const double Standing = 2.0;

        /// <summary>The bar to the doors, then (when <paramref name="stars"/> > 0) the preset for that standing and a
        /// reload around the same run, so every panel is built against it. Off camera.</summary>
        private IEnumerator SetTheBar(double stars)
        {
            yield return LoadToTheDoor();
            yield return WalkInAndOpen();
            if (stars <= 0) yield break;
            _boot.Tycoon.DevPresetStars(stars);
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
            yield return Hold(1f);
        }

        // ── S01: the front door and the opening ───────────────────────────────────────────────────────────────

        [UnityTest, Timeout(600000)]
        public IEnumerator S01_open()
        {
            yield return LoadToTheDoor();
            TrailerCamera.Roll("S01_open");
            TrailerCamera.Mark("menu");
            var newRun = (RectTransform)GameObject.Find("MainMenu/Column/NEW RUN").transform;
            yield return Hold(2.5f);                                   // the sign, lit
            yield return Glide(FaceOf(newRun) + new Vector2(0f, -140f * U), 0.9f);
            yield return Hold(0.6f);
            yield return WalkInAndOpen();
            yield return Hold(2.5f);                                   // the room, before anyone is in
            var first = _boot.Tycoon.Shelf.Bottles[0].Id;
            yield return OpenTheCellar("CellarDoor_" + first);
            yield return Hold(1.2f);
            // the hand runs along the doors, the way an eye reads a shelf
            foreach (var b in _boot.Tycoon.Shelf.Bottles)
            {
                var door = Find("CellarDoor_" + b.Id);
                if (!Shown(door)) continue;
                yield return Glide(FaceOf(door), 0.28f);
                yield return Hold(0.15f);
            }
            yield return Hold(1f);
            TrailerCamera.Cut();
        }

        // ── S02: the card, then the drink, whole - one take per drinker ──────────────────────────────────────

        /// <summary>
        /// Drinkers are served one after another, each in a take of their own (S02_serve_1, _2, ...), until the reel
        /// holds a shaken drink, a built one and a pint, or six takes have been filmed. The order is on the card and
        /// nowhere else, so which drink a take holds is only known once it is read - on camera, as a player learns it.
        /// </summary>
        [UnityTest, Timeout(1800000)]
        public IEnumerator S02_serve()
        {
            yield return SetTheBar(Standing);
            bool shaken = false, built = false, pint = false;
            for (int take = 1; take <= 6 && !(shaken && built && pint); take++)
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

        /// <summary>Between takes, off camera: the counter cleared through Core so the next take starts tidy.</summary>
        private IEnumerator CleanUpAfter()
        {
            var run = _boot.Tycoon;
            yield return PutTheCardDown();
            if (Shown(Find("ShakerPanel")) || Shown(Find("ServePanel")) || Shown(Find("TapPanel")))
                yield return Tap(_keys.escapeKey);
            yield return CloseTheCellar();
            yield return Hold(0.3f);
        }

        // ── S03: the door - a card that lies, and KICK ───────────────────────────────────────────────────────

        [UnityTest, Timeout(1800000)]
        public IEnumerator S03_kick()
        {
            yield return SetTheBar(Standing);
            Assert.That(_boot.Tycoon.Has(Feature.Door), Is.True, "the preset did not open the door rung");
            for (int attempt = 0; attempt < 12; attempt++)
            {
                CustomerVisit visit = null;
                yield return Admit(v => v.Regular != null && v.Regular.LooksYoung, 1800f, v => visit = v);
                if (visit == null) break;
                var seat = _stoolOf[visit];
                TrailerCamera.Roll("S03_kick");
                yield return ReadTheCard(visit, seat, lingerOnTheOrder: false);
                if (!visit.Papers.ShouldBeKicked)
                {
                    // an honest young face: not this take. Serve nobody, put the card down, try the next one.
                    TrailerCamera.Cut();
                    yield return PutTheCardDown();
                    continue;
                }
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
            Assert.Inconclusive("no forged card or minor walked in within the takes allowed - film it again with another seed");
        }

        // ── S04: the close - the slip, the market, a fitting bought, the next night ─────────────────────────

        [UnityTest, Timeout(900000)]
        public IEnumerator S04_close()
        {
            yield return SetTheBar(3.0);
            var run = _boot.Tycoon;
            // straight to the close, the way the suite proves it works: a night with glasses still on the counter
            // will not close until they are collected, and that is not a beat this shot is about
            run.DevSkipToDayEnd();
            bool slip = false;
            yield return Until(() => Shown(Find("BillNext")), 40f, ok => slip = ok);
            Assert.That(slip, Is.True, "the night's slip never came up");
            TrailerCamera.Roll("S04_close");
            TrailerCamera.Mark("slip");
            yield return Hold(5f);                                     // the tape, the stars, the stamp
            for (int i = 0; i < 5 && !Shown(Find("Basket")); i++)
            {
                if (Shown(Find("BillNext"))) yield return ClickFace(Find("BillNext"));
                yield return Hold(0.6f);
            }
            TrailerCamera.Mark("market");
            yield return Hold(0.8f);
            yield return HearTheHostOut();
            // browse the aisles, then buy what the last one offers first
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
                if (run.Talking || HostKey() != null) return false;
                return run.Day != leaving && run.Phase == TycoonPhase.DayOpen && run.Floor.Elapsed > 0;
            }, 30f, ok => next = ok);
            if (!next) yield return HearTheHostOut();
            TrailerCamera.Mark("next night");
            yield return Hold(3f);
            TrailerCamera.Cut();
        }

        private static RectTransform NthTile(int index)
        {
            var tiles = new System.Collections.Generic.List<RectTransform>();
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (rt.name == "Tile" && rt.gameObject.activeInHierarchy) tiles.Add(rt);
            tiles.Sort((a, b) =>
            {
                int byRow = b.position.y.CompareTo(a.position.y);
                return byRow != 0 ? byRow : a.position.x.CompareTo(b.position.x);
            });
            return index < tiles.Count ? tiles[index] : null;
        }

        // ── S05: the rush - a full room late in the run, the hand going card to card ────────────────────────

        [UnityTest, Timeout(900000)]
        public IEnumerator S05_rush()
        {
            yield return SetTheBar(4.5);
            var run = _boot.Tycoon;
            // fill the room off camera
            for (int i = 0; i < 1200 && run.Floor.Seated.Count < Mathf.Min(run.Seats, 6); i++)
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
            foreach (var kv in new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<CustomerVisit, RectTransform>>(_stoolOf))
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
    }
}
