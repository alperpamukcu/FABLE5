using System.Collections;
using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LastCall.PlayTests
{
    /// <summary>
    /// THE FIRST TEST THAT PLAYS THE GAME (2026-08-12).
    ///
    /// The rules have had 203 EditMode tests for a year; the 17k lines of UI that carry them
    /// have had none, because the tests assembly is Editor-only and a click is not a function
    /// call. Every UI regression in this project's history was caught by a human entering play
    /// mode and looking — which is why they were caught late, one at a time, and only where
    /// someone happened to look.
    ///
    /// So this suite plays. A virtual mouse is created (InputTestFixture), the real scene is
    /// loaded, and the game is driven through the same path a player's hand takes: a device
    /// event, the InputSystemUIInputModule, a GraphicRaycaster, a Button. Nothing here reaches
    /// into the UI's private fields — the tests find what they press BY NAME and then assert
    /// on CORE state, which is public and is the only thing that actually matters. That keeps
    /// the suite honest in both directions: it cannot pass by inspecting the thing it clicked,
    /// and it does not break when a panel is rearranged, only when the game stops working.
    ///
    /// The bar's own clock is fast-forwarded with TycoonRun.Tick rather than waited on: the
    /// floor seats drinkers on a schedule measured in bar-minutes, and a test that waits for
    /// one is a test nobody runs.
    /// </summary>
    public sealed class ServiceSmokeTests : InputTestFixture
    {
        private const string SceneName = "Main";

        /// <summary>How long the boot may take before the test calls it dead.</summary>
        private const float BootTimeout = 20f;

        /// <summary>Real SECONDS to give a press before the next one: long enough for a stage
        /// slide (0.16s, unscaled) to finish and hand the raycasts back.
        ///
        /// Seconds, not frames, and that distinction is the whole reason this suite took four
        /// runs to go green: a PlayMode test drives frames as fast as the editor can produce
        /// them — about a millisecond each — while everything the game animates runs on the
        /// clock. Twenty frames of waiting was twenty milliseconds of waiting, so every second
        /// click landed on a stage still sliding, with its raycasts off, and the raycast at
        /// the failure said exactly that: nothing under the pointer.</summary>
        private const float SettleSeconds = 0.35f;

        /// <summary>The field the bar is drawn in (DesignFrame). The tests run at exactly
        /// this, because at anything smaller the game crops itself on purpose.</summary>
        private const int DesignW = 1280, DesignH = 720;

        private Mouse _mouse;
        private GameBootstrap _boot;
#if UNITY_EDITOR
        private uint _windowW, _windowH;
#endif

        /// <summary>
        /// THE TESTS RUN AT THE SIZE THE GAME IS DRAWN AT. The bar is laid out in a fixed
        /// 1280x720 field which CROPS rather than shrinks when the window is smaller
        /// (DesignFrame, by the author's own call) — so in a half-size Game view the top
        /// shelf of the back bar is genuinely off-screen, and a test clicking it is clicking
        /// past the edge of the world. The first run of this suite failed exactly there, on a
        /// 903x508 window, and the raycast said so: zero hits at a live bottle's position.
        /// </summary>
        [OneTimeSetUp]
        public void PinTheWindow()
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.GetRenderingResolution(out _windowW, out _windowH);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(DesignW, DesignH, "LastCall PlayTests");
#endif
        }

        [OneTimeTearDown]
        public void GiveTheWindowBack()
        {
#if UNITY_EDITOR
            if (_windowW > 0 && _windowH > 0)
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution(_windowW, _windowH, "LastCall");
#endif
        }

        public override void Setup()
        {
            base.Setup();
            // The fixture wipes the input system, so the hand is made here, after the wipe and
            // before the scene builds its EventSystem against it.
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            // THE HAND GOES BACK IN THE BOX. InputTestFixture restores the whole input system
            // here, but only if this runs at all — and a run that is cancelled or killed
            // leaves its virtual mouse as the editor's ONLY pointer, at which point the game
            // appears to play itself and ignore the player (2026-08-13). Removing the device
            // explicitly costs nothing and shortens the window in which that can happen.
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
            base.TearDown();
        }

        // ── the tests ────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator The_bar_opens_and_deals_a_night()
        {
            yield return OpenTheBar();

            Assert.That(_boot.Tycoon, Is.Not.Null, "the run was never dealt");
            Assert.That(_boot.Tycoon.Phase, Is.EqualTo(TycoonPhase.DayOpen), "the bar did not open");
            Assert.That(_boot.Tycoon.Shelf.Bottles.Count, Is.GreaterThan(0), "the well is empty");
            Assert.That(Find("Seat0"), Is.Not.Null, "no stools were built");
            // Any Debug.LogError or exception during boot fails this test on its own — that is
            // the half of this test which has no assert of its own and still earns its place.
        }

        [UnityTest]
        public IEnumerator A_stool_answers_the_pointer_and_the_licence_takes_the_order()
        {
            yield return OpenTheBar();
            yield return SeatSomebody();

            var visit = FirstSeated();
            Assert.That(visit, Is.Not.Null, "nobody ever sat down");

            // THE ORDER IS BEHIND THE CARD, and this is the one place that rule can be tested
            // the way a player meets it: Core refuses to hand the order over until the licence
            // has been read, so asking for it now must throw.
            Assert.That(() => visit.Order, Throws.Exception,
                "the order was readable before anyone read the licence");

            // A drinker DECIDES before they can be asked — the seat refuses the click until
            // they have an order to give (OnSeatClicked: "still deciding"). The suite learned
            // this the hard way: clicking on arrival passed or failed depending on how long
            // that night's roll made them think.
            yield return WaitUntilTheyDecide(visit);

            // WHOEVER IS ON IT. Which visit sits on which stool is the HUD's own bookkeeping,
            // and asking it would mean reaching into the HUD — so the test clicks the stool
            // that has somebody on it.
            var seat = FirstOccupiedStool();
            Assert.That(seat, Is.Not.Null, "the drinker who sat down was given no stool");
            yield return WaitUntilTheyReachTheStool(seat);

            // AT THE BODY, NOT THE RECT'S CENTRE (2026-08-25). The stool's rect runs from
            // the counter band up past the drinker's head, so its centre lands on the
            // COUNTER — where the recipe book now stands, and the book, built later, wins
            // that raycast (measured: under=[BookProp][Seat1], four runs, two editors). A
            // player clicks the person they can see; a quarter-height up is their torso.
            yield return ClickOn(seat, new Vector2(0f, seat.rect.height * 0.27f));

            var clicked = ScreenPointOf(seat) + new Vector2(0f, seat.rect.height * 0.27f);
            bool anyInspected = false;
            foreach (var v in _boot.Tycoon.Floor.Seated) if (v.IdInspected) anyInspected = true;
            Assert.That(visit.IdInspected, Is.True,
                "clicking the stool did not read the licence — the pointer never reached the seat"
                + $" [DIAG seat={seat.name} clicked={clicked} under={WhatIsUnder(clicked)}"
                + $" anyInspected={anyInspected} idOpen={Find("IdCard") != null}"
                + $" mousePos={_mouse.position.ReadValue()}"
                + $" state={visit.State} ordered={visit.HasOrdered}]");
            Assert.That(() => visit.Order, Throws.Nothing,
                "the licence was read and the order stayed hidden");
            Assert.That(visit.Order.Wanted, Is.Not.Null, "the card came out with no drink named on it");
        }

        [UnityTest]
        public IEnumerator The_menu_opens_and_a_bottle_takes_the_bench()
        {
            yield return OpenTheBar();

            // THE BACK BAR IS THE COUNTER'S OWN CELLAR NOW (2026-08-22). The verb is the same
            // and the page it used to open is gone: pressing it lifts the room and rolls the
            // shutter down, and the stock is standing in the bar's own body.
            yield return OpenTheCellar("CellarDoor_vodka_astra");

            // The house pour that every run opens with, standing in the cellar. The doors
            // carry their bottle's id, so this asks for the vodka and not for "slot 0".
            var slot = Find("CellarDoor_vodka_astra");
            string underPointer = WhatIsUnder(ScreenPointOf(slot));
            yield return ClickOn(slot);

            var bench = Find("ShakerPanel");
            Assert.That(bench, Is.Not.Null, "the bench never built");
            Assert.That(bench.gameObject.activeInHierarchy, Is.True,
                "clicking a bottle did not take it to the bench; under the pointer: " + underPointer);
        }

        [UnityTest]
        public IEnumerator Tipping_the_bottle_pours_into_the_tin()
        {
            yield return OpenTheBar();
            var run = _boot.Tycoon;

            yield return OpenTheCellar("CellarDoor_vodka_astra");
            yield return ClickOn(Find("CellarDoor_vodka_astra"));

            var panel = Find("ShakerPanel");
            var bottle = Find("Bottle", panel);
            Assert.That(bottle, Is.Not.Null, "there is no bottle on the bench");
            Assert.That(run.Glass.IsEmpty, Is.True, "the tin was not empty to begin with");

            // A HAND, NOT A FORMULA. The pour is a physical aim — lift the bottle, swing its
            // mouth over the tin — and the geometry behind it (the tilt curve, where the mouth
            // ends up, how wide the tin's mouth counts as) is the bench's business and changes
            // when the bench changes. So the test does what a player does: it holds the bottle
            // and moves it across the bench until liquid lands. It passes if ANY sane arc
            // pours, and fails only if the mechanic itself has stopped answering the mouse.
            Press(_mouse.leftButton, ScreenPointOf(bottle));
            yield return new WaitForSecondsRealtime(0.1f);

            bool poured = false;
            for (int y = 0; y < 5 && !poured; y++)
            {
                for (int x = 0; x < 7 && !poured; x++)
                {
                    var aim = new Vector2(Mathf.Lerp(-260f, 260f, x / 6f),
                                          Mathf.Lerp(-40f, 200f, y / 4f));
                    Set(_mouse.position, ScreenPointIn(panel, aim));
                    // Held for a real moment: the pour is measured per frame against the
                    // clock, so a millisecond over the tin pours a millisecond's worth.
                    yield return new WaitForSecondsRealtime(0.08f);
                    poured = !run.Glass.IsEmpty;
                }
            }
            Release(_mouse.leftButton);
            yield return new WaitForSecondsRealtime(0.1f);

            Assert.That(poured, Is.True,
                "the bottle was carried over every part of the bench and the tin stayed empty");
            Assert.That(run.Glass.TotalVolume, Is.GreaterThan(0.0));
            Assert.That(run.Glass.Ingredients, Contains.Item("vodka_astra"),
                "something poured, but not the bottle that was picked up");
        }

        /// <summary>
        /// ONE KEY IN THE MARKET'S FOOT, AND IT DOES BOTH ERRANDS (2026-09-04, the author:
        /// "2 butonu 1 buton yapıyoruz"). The market used to carry PLACE ORDER in the
        /// basket's head band and OPEN TOMORROW in the foot; they are the same key now, and
        /// which errand it is on is read off the basket.
        ///
        /// Worth a floor test because the failure mode is silent and total: a key that keeps
        /// the wrong face buys nothing and ends the night instead, or ends nothing and offers
        /// to buy an empty basket. Nothing about that shows up in a compile, and the picture
        /// suite only ever sees the empty-basket face.
        ///
        /// It shops the way a player does — the aisle, a tile, the key — and never asks the
        /// UI what it thinks: the caption on the key and the run's own book of purchases are
        /// the whole of the evidence.
        /// </summary>
        [UnityTest]
        public IEnumerator The_markets_one_key_buys_first_and_opens_tomorrow_after()
        {
            yield return OpenTheBar();
            var run = _boot.Tycoon;

            // An empty night, called on the spot — the same door the look suite walks.
            run.DevSkipToDayEnd();
            yield return OpenTheMarket();

            // THERE IS NO SECOND KEY. The head band's PLACE ORDER was named "Checkout", and
            // its absence is the merge itself: if that rect comes back, so has the pair.
            Assert.That(Find("Checkout"), Is.Null,
                "the basket's head band has a key again — the market is back to two");

            var key = Find("OpenTomorrow");
            Assert.That(key, Is.Not.Null, "the market has no key in its foot at all");
            var caption = key.GetComponentInChildren<Text>();
            Assert.That(caption, Is.Not.Null, "the key has no caption");
            Assert.That(caption.text, Does.Not.Contain("ORDER"),
                "nothing is picked and the key already offers to buy: " + caption.text);

            // THE MIXERS AISLE, which is the cheap one — every bottle in it is 2–4, so a bar
            // that has just paid its first night's rent can still afford the first thing it
            // points at. That is the aisle this key was merged alongside (the soft drinks got
            // small and cheap the same day), so it is the right one to shop from.
            yield return ClickOn(Find("Tab2"));
            yield return new WaitForSecondsRealtime(0.4f);

            // Pick the first thing that will actually go in: a listing the till cannot cover
            // still answers the pointer, it just refuses, so the basket is the only honest
            // signal that something landed. Only the top of the aisle is tried — below the
            // fold the press would be a press into the scroll view's clip.
            bool picked = false;
            for (int i = 0; i < 6 && !picked; i++)
            {
                var tile = NthTile(i);
                if (tile == null) break;
                yield return ClickOn(tile);
                picked = caption.text.Contains("ORDER");
            }
            Assert.That(picked, Is.True,
                "six listings were pressed in the mixer aisle and the basket stayed empty "
                + "— the key still reads: " + caption.text);
            Assert.That(caption.text, Is.EqualTo("PLACE\nORDER"),
                "something is in the basket and the key does not say what it would do");

            // AND THE KEY SPENDS. The run's own book is what says so — the UI could paint
            // anything it liked and Core is what actually took the money.
            int before = run.TodaysPurchases.Count;
            yield return ClickOn(key);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(run.TodaysPurchases.Count, Is.EqualTo(before + 1),
                "the key was pressed on a full basket and nothing was bought");
            Assert.That(caption.text, Is.EqualTo("ORDERED"),
                "the order landed and the key did not answer");
            Assert.That(run.Phase, Is.EqualTo(TycoonPhase.DayEnd),
                "the key bought AND ended the night — it did both errands at one press");
        }

        /// <summary>
        /// THE RESTOCK AISLE ADDS UP ONCE (2026-09-04, the author: "hem ayrı olarak
        /// alkolleri restocklayıp hem de ayrıyeten tam fiyatına restock satın alınıyor").
        ///
        /// The whole-well crate quoted the shelf's entire shortfall no matter what the basket
        /// was already covering, so a bottle picked by hand and the crate beside it billed
        /// the same measures twice. The crate is a remainder now, and this is the arithmetic
        /// said out loud: put EVERY short bottle in the basket by hand, and the crate has
        /// nothing left to sell — the author's second sentence, "eğer restock edilebilecek
        /// ürün yoksa restock alınamamalı".
        ///
        /// It is proved against the TILL and the SHELF, not against a label: what the night
        /// costs is the one number a wrong answer here would move.
        /// </summary>
        [UnityTest]
        public IEnumerator The_restock_aisle_never_bills_the_same_measure_twice()
        {
            yield return OpenTheBar();
            var run = _boot.Tycoon;

            // A mid bar, so the till can cover an order; then two bottles pulled down by a
            // known amount, so the shortfall is a number this test knows rather than one it
            // has to read back off the screen.
            run.DevPreset(1);
            var drained = new List<ShelfBottle>();
            foreach (var b in run.Shelf.Bottles)
            {
                if (b.Ingredient.Type == IngredientType.Beer || b.Capacity < 2.5) continue;
                b.Draw(2.0);
                drained.Add(b);
                if (drained.Count == 2) break;
            }
            Assert.That(drained.Count, Is.EqualTo(2), "the well had no two bottles to pull down");

            int shortfall = run.Shelf.RefillCost(run.Config.RefillPricePerCapacity);
            Assert.That(shortfall, Is.GreaterThan(0), "nothing was short after draining two bottles");

            run.DevSkipToDayEnd();
            yield return OpenTheMarket();
            yield return ClickOn(Find("Tab0"));
            yield return new WaitForSecondsRealtime(0.4f);

            // The aisle sorts what is emptiest first, so behind the crate at slot 0 stand
            // exactly the two bottles that were drained.
            var head = Find("BasketHL").GetComponent<Text>();
            yield return ClickOn(NthTile(1));
            yield return ClickOn(NthTile(2));
            Assert.That(head.text, Is.EqualTo("BASKET (2)"),
                "the two short bottles did not both go in: " + head.text);

            // AND NOW THE CRATE HAS NOTHING TO ADD. Pressing it must not put a third line in
            // the basket — that line is the double bill.
            yield return ClickOn(NthTile(0));
            Assert.That(head.text, Is.EqualTo("BASKET (2)"),
                "the whole-well crate went in on top of the bottles it would have covered: "
                + head.text);

            int before = run.Money;
            yield return ClickOn(Find("OpenTomorrow"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(before - run.Money, Is.EqualTo(shortfall),
                "the order charged something other than the shelf's shortfall");
            foreach (var b in drained)
                Assert.That(b.Remaining, Is.EqualTo(b.Capacity).Within(1e-9),
                    b.Id + " was paid for and not filled");
        }

        /// <summary>
        /// AN EMPTY-HANDED NIGHT IS ASKED ABOUT (2026-09-04, the author: "hiçbir şey almadan
        /// devam etmek istediğinde emin misin diye tekrar sorsun"). A night nobody shopped on
        /// is a night of rent for nothing, and the one key in the foot now ends the night on
        /// a single press — so the question in front of it is the whole of the guard.
        /// </summary>
        [UnityTest]
        public IEnumerator Leaving_the_market_having_bought_nothing_asks_first()
        {
            yield return OpenTheBar();
            var run = _boot.Tycoon;
            run.DevSkipToDayEnd();
            yield return OpenTheMarket();

            Assert.That(run.TodaysPurchases.Count, Is.EqualTo(0), "the night bought something on its own");
            yield return ClickOn(Find("OpenTomorrow"));
            yield return new WaitForSecondsRealtime(0.4f);

            var ask = Find("ClosingAsk");
            Assert.That(ask != null && ask.gameObject.activeInHierarchy, Is.True,
                "the market let an empty-handed night close without asking");
            Assert.That(run.Phase, Is.EqualTo(TycoonPhase.DayEnd),
                "the night ended anyway, behind the question");
        }

        /// <summary>Walks the night's slip and stops when the market is on screen and settled.
        /// Three tests take this same door; the look suite keeps its own copy because it has
        /// to photograph what it finds at the end of it.</summary>
        private IEnumerator OpenTheMarket()
        {
            RectTransform next = null;
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                next = Find("BillNext");
                if (next != null && next.gameObject.activeInHierarchy) break;
                next = null;
                yield return null;
            }
            Assert.That(next, Is.Not.Null, "the night's slip never offered a way on");
            yield return ClickOn(next);

            RectTransform basket = null;
            float shop = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < shop)
            {
                basket = Find("Basket");
                if (basket != null && basket.gameObject.activeInHierarchy) break;
                basket = null;
                yield return null;
            }
            Assert.That(basket, Is.Not.Null, "the market never opened after the slip");
            yield return new WaitForSecondsRealtime(0.5f);   // it slides in from the right
        }

        /// <summary>The nth listing in the open aisle, in the order they are laid out — or
        /// null once the aisle runs out. Named "Tile" by the market that builds them.</summary>
        private static RectTransform NthTile(int index)
        {
            var tiles = new List<RectTransform>();
            foreach (var rt in Object.FindObjectsByType<RectTransform>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (rt.name == "Tile" && rt.gameObject.activeInHierarchy) tiles.Add(rt);
            // Top-down, then left-to-right: the order a reader meets them, which is not the
            // order FindObjectsByType hands them back in.
            tiles.Sort((a, b) =>
            {
                var pa = a.position; var pb = b.position;
                int byRow = pb.y.CompareTo(pa.y);
                return byRow != 0 ? byRow : pa.x.CompareTo(pb.x);
            });
            return index < tiles.Count ? tiles[index] : null;
        }

        // ── the bar, opened ──────────────────────────────────────────────────────

        /// <summary>Loads the real scene and waits until the night is actually dealt.</summary>
        private IEnumerator OpenTheBar()
        {
            // THE WINDOW FIRST, THE BAR SECOND. The HUD measures the window once, while it
            // builds itself, and never re-measures — so a scene loaded before the Game view
            // has finished becoming 1280x720 lays its stools out against the OLD width and
            // puts them off the field. That is what the first pinned run caught: a stool at
            // x=1427 on a 1280 window.
            for (int i = 0; i < 180 && (Screen.width != DesignW || Screen.height != DesignH); i++)
                yield return null;
            Assert.That(Screen.width, Is.EqualTo(DesignW),
                "the Game view never became the design width — the layout cannot be trusted");

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            float waited = 0f;
            while (waited < BootTimeout)
            {
                _boot = Object.FindFirstObjectByType<GameBootstrap>();
                if (_boot != null && _boot.Tycoon != null) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(_boot, Is.Not.Null, $"'{SceneName}' has no GameBootstrap in it");
            Assert.That(_boot.Tycoon, Is.Not.Null,
                $"the run never started within {BootTimeout}s — the boot is half-loaded");

            // THE BAR IS OPEN WHEN ITS CLOCK IS RUNNING (2026-08-13). A quarter of a second
            // was the wait, and the suite's first test kept failing on a press that landed
            // on nothing: the room opens behind a CURTAIN on its own canvas above everything,
            // and the HUD deliberately holds the night's clock until it lifts. So the honest
            // signal that the doors are open is the one the game itself uses — Elapsed only
            // moves once the curtain is gone, and a press before that is a press into a
            // black screen. Six retries could not fix what was never a timing budget.
            // …AND THE PHASE THE DOORS ACTUALLY ANSWER TO (2026-08-22). The clock lifting is
            // necessary and was not sufficient: every door in the flow opens with the same
            // guard — `if (Run.Phase != TycoonPhase.DayOpen) return;` — so a press that lands
            // one phase early does NOTHING AT ALL, silently, and the six retries above it
            // report "the button never opened the panel" for what is really "the bar was not
            // open yet". That is the intermittent red this suite has been throwing; it waits
            // for the state the press requires now, rather than pressing and hoping.
            float open = Time.realtimeSinceStartup + 15f;
            while ((_boot.Tycoon.Floor.Elapsed <= 0
                    || _boot.Tycoon.Phase != TycoonPhase.DayOpen)
                   && Time.realtimeSinceStartup < open)
                yield return null;
            Assert.That(_boot.Tycoon.Floor.Elapsed, Is.GreaterThan(0),
                "the curtain never lifted — the night's clock never started");
            Assert.That(_boot.Tycoon.Phase, Is.EqualTo(TycoonPhase.DayOpen),
                "the run never reached DayOpen — every door in the flow refuses a press "
                + "before that, and refuses it without a sound");
            yield return WaitFrames(2);
        }

        /// <summary>Runs the bar's clock forward until somebody is on a stool. The floor seats
        /// arrivals on bar-time, so waiting in real seconds is waiting for nothing.</summary>
        private IEnumerator SeatSomebody()
        {
            var run = _boot.Tycoon;
            for (int i = 0; i < 600 && FirstSeated() == null; i++)
            {
                run.Tick(0.5);
                if (i % 20 == 0) yield return null;   // let the HUD see what the floor did
            }
            yield return WaitFrames(2);
            Assert.That(FirstSeated(), Is.Not.Null, "nobody came in all night");
        }

        private CustomerVisit FirstSeated()
        {
            var seated = _boot.Tycoon.Floor.Seated;
            for (int i = 0; i < seated.Count; i++)
                if (seated[i].State == VisitState.Waiting) return seated[i];
            return seated.Count > 0 ? seated[0] : null;
        }

        /// <summary>A stool with somebody on it. The HUD names them Seat0..SeatN and only
        /// shows the ones it has given to a drinker, so "visible" is the whole test.</summary>
        private RectTransform FirstOccupiedStool()
        {
            for (int i = 0; i < 8; i++)
            {
                var seat = Find("Seat" + i);
                if (seat != null && seat.gameObject.activeInHierarchy) return seat;
            }
            return null;
        }

        /// <summary>
        /// A drinker WALKS IN. The stool's rect starts off the right edge of the field
        /// (`_hudRoot.rect.width + margin`) and slides to its place over real frames, so a
        /// test that clicks the moment somebody is seated is clicking at x=1428 on a 1280
        /// window — which is exactly what this suite did on its third run, and the number in
        /// the failure was the entry mark to the pixel. Waiting for the stool to stop moving
        /// is also the player's own rule: you cannot serve someone still crossing the room.
        /// </summary>
        private IEnumerator WaitUntilTheyReachTheStool(RectTransform seat)
        {
            // EIGHT still frames, not three (2026-08-13). A walk-in eases into its stool, so
            // its last few frames each move less than a tenth of a pixel — which reads as
            // "arrived" to a three-frame test while the drinker is still moving. Eight frames
            // of nothing is a stop; three is a slow moment inside a move.
            var last = seat.anchoredPosition;
            int still = 0;
            float deadline = Time.realtimeSinceStartup + 25f;
            while (still < 8 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if ((seat.anchoredPosition - last).sqrMagnitude < 0.01f) still++;
                else { still = 0; last = seat.anchoredPosition; }
            }
            Assert.That(still, Is.GreaterThanOrEqualTo(8),
                $"the drinker never finished walking in — stool stopped at x={last.x:0}");
        }

        /// <summary>Waits out the thinking. The decide delay is rolled per customer, so this
        /// is the difference between a suite that passes and one that passes on Tuesdays.</summary>
        private IEnumerator WaitUntilTheyDecide(CustomerVisit visit)
        {
            float deadline = Time.realtimeSinceStartup + 25f;
            while (!visit.HasOrdered && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(visit.HasOrdered, Is.True,
                $"the drinker never decided what to order (state {visit.State})");
            Assert.That(visit.State, Is.EqualTo(VisitState.Waiting),
                "the drinker left before they could be asked");
        }

        // ── the hand ─────────────────────────────────────────────────────────────

        private IEnumerator ClickOn(RectTransform target) => ClickOn(target, Vector2.zero);

        /// <summary>Clicks <paramref name="nudge"/> away from the rect's centre — for a
        /// target whose centre is not where a player's eye aims. A seated drinker's rect
        /// reaches all the way down the counter band, and the counter now carries real,
        /// clickable furniture (the recipe book, 2026-08-25): the player clicks the BODY
        /// they can see, so the suite does too.</summary>
        private IEnumerator ClickOn(RectTransform target, Vector2 nudge)
        {
            Assert.That(target, Is.Not.Null, "there is nothing there to click");
            var at = ScreenPointOf(target) + nudge;
            // On screen, or the click is a click into the letterbox and the failure it causes
            // three asserts later says nothing about why.
            Assert.That(at.x, Is.InRange(0f, (float)Screen.width),
                $"'{target.name}' sits off the side of a {Screen.width}x{Screen.height} window");
            Assert.That(at.y, Is.InRange(0f, (float)Screen.height),
                $"'{target.name}' sits above or below a {Screen.width}x{Screen.height} window");
            // A move BEFORE the press: the input module raycasts on pointer motion, and a
            // click delivered to a pointer that has never moved lands on whatever the module
            // last thought was under it.
            Set(_mouse.position, at);
            yield return WaitFrames(2);
            // AND THE POINT IS TAKEN AGAIN, HERE (2026-08-13). Two frames passed between
            // measuring the thing and pressing on it, and things in this game move: a stool
            // is still drifting the last pixel of a walk-in, a panel is still sliding. The
            // press then lands beside what the test aimed at, and the failure it causes reads
            // as "the licence did not open", which is a lie about what happened. A hand that
            // has already moved to something looks at it once more before it presses.
            if (target != null) Set(_mouse.position, ScreenPointOf(target) + nudge);
            Press(_mouse.leftButton);
            yield return WaitFrames(2);
            Release(_mouse.leftButton);
            // AND THEN IT WAITS. A press can start a stage slide, and a sliding stage takes
            // no input on purpose (the flow drops raycasts for the length of the move), so a
            // test that clicks again straight away is clicking into a closed door.
            yield return new WaitForSecondsRealtime(SettleSeconds);
        }

        private void Press(ButtonControl button, Vector2 at)
        {
            Set(_mouse.position, at);
            Press(button);
        }

        /// <summary>What the UI itself says is under a screen point — the evidence a failed
        /// click needs, since "nothing happened" is the one message that explains nothing.</summary>
        /// <summary>
        /// Presses the making verb until the counter's cellar is open onto a named door. THE
        /// SUITE MAY NOT LOOK INSIDE THE UI (CLAUDE.md), so it cannot ask the stage whether
        /// its drawer is open; it asks the POINTER, which is the better question anyway — the
        /// doors only take a ray once the roller is clear, so a door answering IS the cellar
        /// being open. Retried, because ONE press is not reliably enough: the room is still
        /// settling when the first one lands, and a press that arrives early does nothing at
        /// all. The look suite learned this first and this is its helper, kept in step.
        /// </summary>
        private IEnumerator OpenTheCellar(string doorName)
        {
            // THE KEY IS A TOGGLE NOW, so this LOOKS BEFORE IT PRESSES: a retry that
            // presses blind cannot drive one, because every even press undoes the odd one.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var door = Find(doorName);
                if (door != null && WhatIsUnder(ScreenPointOf(door)).Contains(doorName))
                    yield break;
                // THE KEY IS GONE (2026-08-25). SHUT IT and its shut-state caption were one
                // plate floating over the roller; the roller was always the door underneath
                // it, and now it is the only one. The suite aims at the WORD written on it,
                // because the roller's own rect is hung by its top and its centre is off the
                // bottom of the screen - a click there lands nowhere.
                // The ARROW since 2026-08-26: the word came off the roller and the chevron
                // is the whole sign now, drawn at 3× where the word used to sit.
                var key = Find("OpenSignArrow");
                Assert.That(key, Is.Not.Null, "the roller carries no OPEN to press");
                yield return ClickOn(key);
                yield return new WaitForSecondsRealtime(0.6f);      // the roller's own travel
            }
            Assert.Fail("six presses of the roller never opened it onto " + doorName);
        }

        private static string WhatIsUnder(Vector2 screen)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return "no EventSystem";
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = screen };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            if (hits.Count == 0) return $"nothing at {screen} (screen is {Screen.width}x{Screen.height})";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < hits.Count && i < 4; i++)
                sb.Append('[').Append(hits[i].gameObject.name).Append(']');
            return sb.ToString();
        }

        private static Vector2 ScreenPointOf(RectTransform rt) =>
            RectTransformUtility.WorldToScreenPoint(null, rt.position);

        /// <summary>A point given in a panel's own coordinates, in screen terms.</summary>
        private static Vector2 ScreenPointIn(RectTransform panel, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(local));

        // ── finding things the way a player does: by looking ─────────────────────

        /// <summary>
        /// The thing on screen with this name, LIVE one preferred. The menu tears its whole
        /// wall down and rebuilds it, and a Destroy()ed object lives one more frame — so a
        /// search that takes the first match can hand back a bottle that is already dead and
        /// answers no clicks. Active wins; a hidden one is only returned when there is no
        /// live one, because "built but closed" is a thing several tests assert about.
        /// </summary>
        private static RectTransform Find(string name, RectTransform under = null)
        {
            RectTransform hidden = null;
            if (under != null)
            {
                foreach (var rt in under.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt.name != name) continue;
                    if (rt.gameObject.activeInHierarchy) return rt;
                    hidden = hidden != null ? hidden : rt;
                }
                return hidden;
            }
            foreach (var rt in Object.FindObjectsByType<RectTransform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rt.name != name) continue;
                if (rt.gameObject.activeInHierarchy) return rt;
                hidden = hidden != null ? hidden : rt;
            }
            return hidden;
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }
    }
}
