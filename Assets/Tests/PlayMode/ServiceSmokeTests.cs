using System.Collections;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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

            yield return ClickOn(seat);

            Assert.That(visit.IdInspected, Is.True,
                "clicking the stool did not read the licence — the pointer never reached the seat");
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

        private IEnumerator ClickOn(RectTransform target)
        {
            Assert.That(target, Is.Not.Null, "there is nothing there to click");
            var at = ScreenPointOf(target);
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
            if (target != null) Set(_mouse.position, ScreenPointOf(target));
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
                var key = Find("CellarKey");
                Assert.That(key, Is.Not.Null, "the cellar's key is not on the screen to press");
                yield return ClickOn(key);
                yield return new WaitForSecondsRealtime(0.6f);      // the roller's own travel
            }
            Assert.Fail("six presses of the cellar's key never opened it onto " + doorName);
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
