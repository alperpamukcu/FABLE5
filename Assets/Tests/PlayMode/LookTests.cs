using System.Collections;
using System.Collections.Generic;
using System.IO;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LastCall.PlayTests
{
    /// <summary>
    /// THE SCREEN, COMPARED TO THE LAST TIME ANYONE LOOKED AT IT (2026-08-12).
    ///
    /// A pixel-art game rendered point-filtered at a fixed field is one of the few kinds of
    /// software whose output is an exact value: the same state draws the same 921,600 pixels
    /// every time. That was measured before this file was written — two captures of the back
    /// bar a second apart came back byte-identical — so "it still looks right" can stop being
    /// a thing a human remembers and become a thing a test knows.
    ///
    /// What it does NOT cover is as deliberate as what it does. The ROOM moves: its 2D lights
    /// breathe and its neon glows, so the bar floor differs from itself by 22,000 pixels a
    /// second apart and has no business in an exact test. The screens here are the full-frame
    /// UI surfaces that hold still — and the bench, which holds still everywhere except the
    /// strip of room around its panel, so it is compared inside that strip.
    ///
    /// THE BASELINES ARE THIS MACHINE'S. They live in Baselines~ (Unity ignores directories
    /// ending in ~, so they are never imported, compressed or given a .meta) and they are
    /// committed, because the point is to notice a change between two sessions on the same
    /// machine. A different GPU may draw one pixel differently and the whole suite goes red;
    /// that is a known trade, and the reason these do not belong in CI.
    ///
    /// When a screen is meant to change, re-bless it: LastCall → Re-bless UI Baselines, then
    /// run this suite twice (the first run writes the new pictures and fails on purpose, so
    /// nobody blesses a screen without looking at it).
    /// </summary>
    public sealed class LookTests : InputTestFixture
    {
        private const int DesignW = 1280, DesignH = 720;
        private const float SettleSeconds = 0.4f;

        private Mouse _mouse;
        private GameBootstrap _boot;
#if UNITY_EDITOR
        private uint _windowW, _windowH;
#endif

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

        // ── the screens ──────────────────────────────────────────────────────────

        // THE BACK-BAR PICTURE IS RETIRED (2026-08-22), and not replaced. It photographed a
        // full-screen UI panel, which held still because no light in the world touched it.
        // The back bar is the counter's own cellar now: LIT WORLD ART, tinted every frame by
        // the evening the room is having. A picture of it would be a photograph of a clock —
        // the exact failure the comment on this test used to warn about, made structural.
        // Freezing the shift to hold it still would be testing a bar that does not exist.
        //
        // What is lost is real and worth saying out loud: the cellar's LOOK is now only
        // checked by entering play and looking at it. What still guards it is the smoke
        // suite, which drives the same door with the same virtual mouse and asserts the
        // cellar opens and a named bottle in it reaches the bench — behaviour, not pixels.
        // Its baseline picture went with it; Baselines~/bench.png and basket.png stay,
        // because both are still panels.

        [UnityTest]
        public IEnumerator The_bench_looks_the_way_it_did()
        {
            yield return OpenTheBar();
            // Through the cellar now: the verb opens the counter's own drawer, and the door
            // carrying the opening vodka takes it to the bench. The bench itself is still a
            // panel, which is why this picture survives the redesign and the back bar's did not.
            yield return OpenTheCellar("CellarDoor_vodka_astra");
            yield return OpenUntil("CellarDoor_vodka_astra", "ShakerPanel");
            // THE BAR TOP ONLY, since the bench became a scrim over the room (2026-08-22).
            // The old crop took most of the screen, which worked while the panel was opaque.
            // It is not any more: above the counter band the real room shows through, and the
            // room is an evening — the suite's own settle guard caught it at once ("something
            // on it is always moving"). The counter band IS opaque and everything standing on
            // it holds still between pours, so the picture is cut to the surface and the props
            // on it. Same rule as always: compare the instrument, not the room around it.
            // fromY 0.60 of the panel puts the band's top edge at screen row 288.
            yield return LooksTheSame("bench", new RectInt(40, 300, 1200, 400));
        }

        [UnityTest]
        public IEnumerator The_closing_beat_takes_the_room_down_and_lights_the_guest()
        {
            // THE LIGHT, MEASURED (GDD 26 §7, S4) — not photographed. A picture was the first
            // attempt and it was the wrong tool: two runs of the same beat differ by a few
            // units on the plate's cream (a residual fade over bright pixels rounds where it
            // does not over dark ones) and by whether the settings key drew its icon or its
            // word. Neither has anything to do with the closing beat. What the beat IS, is
            // four numbers — so those are what this asserts, and they cannot drift quietly.
            yield return OpenTheBar();

            var run = _boot.Tycoon;
            // THE ROOM OPENS BARE (2026-09-06): the wall lamps are bought now, so the house
            // lights this beat takes down have to be fitted first — the dev way, since the
            // market has not opened yet on night one. The HUD re-dresses the room on the
            // next frame it sees the owned count move.
            run.DevFit("wall_lamps_one");
            yield return new WaitForSecondsRealtime(0.5f);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (run.LastCustomer == null && run.Phase == TycoonPhase.DayOpen
                   && Time.realtimeSinceStartup < deadline)
            {
                run.Tick(1.0);            // the night on the run's own clock, not the wall's
                yield return null;
            }
            Assert.That(run.LastCustomer, Is.Not.Null,
                "night one is written (GDD 26 §11) and nobody came to the last call");

            yield return new WaitForSecondsRealtime(2.5f);   // the rig ramps over about a second

            // THE HOUSE LIGHTS ARE THE WALL LAMPS NOW (2026-08-23; fixture-driven since
            // 2026-08-24). The room has no ceiling in frame - what comes down at the last
            // call is the pair of wall lamps standing in the houseLight slot, whichever
            // level the bar has fitted. The stage names their glows HouseLight0, 1, ... so
            // this test can find them without seeing the UI assembly.
            float house = 0f;
            int lamps = 0;
            for (int i = 0; i < 8; i++)
            {
                float lit = Intensity("HouseLight" + i);
                if (lit >= 0f) { house += lit; lamps++; }
            }
            float guestLamp = Intensity("LastCallLamp");
            float wash = Intensity("GlobalLight");
            Assert.That(lamps, Is.GreaterThan(0), "the room has no house lights to take down");
            float perLamp = house / lamps;

            Assert.That(guestLamp, Is.GreaterThan(perLamp * 3f),
                $"nothing is picking the guest out: their lamp {guestLamp:0.00} against a "
                + $"house light of {perLamp:0.00}");
            Assert.That(perLamp, Is.LessThan(0.3f), "the house lights did not come down");
            Assert.That(wash, Is.LessThan(0.7f), "the room's wash did not thin");
            // The sign's own line went with the sign (2026-08-19): the beat used to burn the
            // LAST CALL neon harder as the room came down, and there is no neon in the room
            // any more. What the beat still is — a ceiling that falls, a wash that thins and
            // one lamp that finds the guest — is asserted above and is the whole of it now.

            // And it gives the room back. The market opens on the ordinary bar, not on a
            // night that never ended.
            var run2 = _boot.Tycoon;
            run2.DeclineLastCall();
            float deadline2 = Time.realtimeSinceStartup + 20f;
            while (run2.Phase == TycoonPhase.DayOpen && Time.realtimeSinceStartup < deadline2)
            { run2.Tick(1.0); yield return null; }
            yield return new WaitForSecondsRealtime(2f);

            Assert.That(Intensity("LastCallLamp"), Is.LessThan(0.05f),
                "the guest's lamp is still burning over an empty stool");
        }

        /// <summary>
        /// One 2D light's intensity, by the name the stage gave it, WITHOUT naming its type.
        /// `Light2D` lives in the URP 2D runtime assembly, and this test assembly does not
        /// reference it — adding that reference is a change to what the whole suite links
        /// against, for one number. Reflection is the smaller footprint, and the name is the
        /// contract either way. Returns -1 when there is no such light.
        /// </summary>
        private static float Intensity(string lightName)
        {
            var go = GameObject.Find(lightName);
            if (go == null) return -1f;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null || c.GetType().Name != "Light2D") continue;
                var prop = c.GetType().GetProperty("intensity");
                if (prop != null) return (float)prop.GetValue(c);
            }
            return -1f;
        }

        [UnityTest]
        public IEnumerator The_baskets_foot_looks_the_way_it_did()
        {
            yield return OpenTheBar();

            // An EMPTY night, called on the spot: nobody was served, so the slip and the
            // shelves are the same every time this runs. A night played out in real seconds
            // would put a different receipt on the screen at every capture.
            _boot.Tycoon.DevSkipToDayEnd();

            // The night is announced, the slip feeds, the stars fall — and only then does the
            // way on appear. Waiting for the key IS waiting for that sequence to finish.
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
            var clickedAt = RectTransformUtility.WorldToScreenPoint(null, next.position);

            // PRESSED UNTIL IT OPENS, like every other door this suite walks (2026-09-05).
            // One press was the rule here and it is the only door in the file that got one —
            // and it is the FIRST press of the whole suite, which the helper's own comment
            // already calls the slow one. Measured: this test failed twice in a full run with
            // "key active True" and the market never opening, and passed on its own both
            // times in between. A swallowed first press is exactly what OpenUntil exists for.
            //
            // Safe to press twice even though BillNext is not idempotent — a second press on
            // the market would try to CLOSE it. RebuildDayEnd activates the tablet
            // synchronously inside the first press, so the basket is activeInHierarchy before
            // OpenUntil looks; it can only press again when the first genuinely did nothing.
            yield return OpenUntil("BillNext", "Basket");

            // WAIT FOR THE MARKET, DO NOT COUNT TO ONE (2026-08-13). This was a fixed 0.6s and
            // it captured the night's SLIP instead — 161,693 differing pixels that say nothing
            // about the basket. The slip's own feed got longer the day the written beat arrived
            // (the guest holds the night open while they talk), and any wait measured in
            // seconds is a wait that a slower machine or a longer animation walks straight
            // past. The basket itself is the thing to wait for, and it is the thing being
            // photographed: the suite's own rule, applied where it had not been.
            float shop = Time.realtimeSinceStartup + 15f;
            RectTransform basket = null;
            while (Time.realtimeSinceStartup < shop)
            {
                basket = Find("Basket");
                if (basket != null && basket.gameObject.activeInHierarchy) break;
                basket = null;
                yield return null;
            }
            // A PERMANENT DIAGNOSTIC, the same one the smoke suite's stool click carries: a
            // press that lands on the wrong rect looks exactly like a press that did nothing,
            // and the only cheap way to tell them apart is to ask the raycaster what was
            // under the pointer at the moment it fired.
            Assert.That(basket, Is.Not.Null, "the order never opened after the slip"
                + " · clicked " + clickedAt + " in " + Screen.width + "x" + Screen.height
                + " · under: " + WhatIsUnder(clickedAt)
                + " · key active " + (next != null && next.gameObject.activeInHierarchy));
            yield return new WaitForSecondsRealtime(0.4f);   // it slides in from the right

            // THE FOOT, NOT THE WHOLE MARKET. The aisle above it scrolls, and its scroll
            // settles on one of two pixel offsets depending on how the frames fell — measured:
            // two stable variants of the same page, differing by a few pixels of content
            // offset and a flat +5 of brightness over the sheet, everywhere above y 510 and
            // nowhere below it. The basket is what this pins: it is the surface that was
            // rebuilt on 2026-08-11, it does not scroll, and it was identical in both variants.
            //
            // AND NOT THE ROOM BESIDE IT (2026-08-19). The tablet is 1096 wide on a 1280
            // field, so it ends at screen x 1188 — and this region ran to 1230, catching 42
            // columns of the ROOM past its right edge. That was harmless while the room's
            // light was a constant; it is a photograph of a clock now that the light runs off
            // the window's own frames and moves continuously through the evening. Measured:
            // 2672 pixels of drift at 3/255, all of it in those columns and none of it in the
            // basket. 1078 stops just inside the bezel. Same rule the bench and the back bar
            // keep — compare the instrument, not the room around it.
            //
            // ONE MORE PIXEL OFF EACH FAR EDGE (2026-08-26). 1078×150 stopped just inside the
            // bezel and its very LAST pixel — the bottom-right corner, where the bezel is
            // rounded — was still room: the day the room's furniture was re-cut it went from
            // 55,59,63 to 26,22,27 and failed this test on ONE pixel, with the basket itself
            // identical. Re-blessing would have baked the new corner in and bought exactly one
            // room change of peace. 1077×149 is the same rule applied one pixel further.
            //
            // THE ORIGIN STAYS AT 550. This region is given the way a person reads a screen —
            // y DOWN from the top (see Crop) — so shrinking the height drops the LAST row,
            // which is the one that was room. Moving the origin instead was tried and shifted
            // the whole picture by a row against its baseline: 9190 pixels "differed", which
            // is what an image looks like held one row off itself.
            //
            // The blessed picture was CROPPED to match rather than re-blessed. Every pixel
            // left in it is a pixel that was already blessed, and Re-bless UI Baselines would
            // have taken the BENCH with it — which is the one thing the gate exists to stop.
            // Her word on the market is read first: its scrim would darken the foot.
            yield return LetTheHostFinish();
            yield return LooksTheSame("basket", new RectInt(110, 550, 1077, 149));
        }

        /// <summary>
        /// Presses a thing until the screen it opens is actually open — and says so plainly
        /// if it never does.
        ///
        /// A picture test that does not check which screen it is looking at will happily
        /// bless the wrong one: this suite once reported 756,000 differing pixels on the
        /// bench, and the answer was that the bench had never opened — the capture was of the
        /// back bar. The retry is the other half: the wall tears itself down and rebuilds when
        /// it opens, so a rect found on one frame can be destroyed on the next and a click
        /// lands on a dead object. A player would simply click again.
        ///
        /// THE FIRST PRESS OF A SESSION IS THE SLOW ONE (2026-08-13, measured): the suite's
        /// very first test failed here twice in a row — the button on the screen, the panel
        /// built, and three presses inside one second not opening it — and the same build then
        /// passed twice. Everything that makes the first play-mode enter expensive lands on
        /// that press. So this waits longer than a player would need to, which costs nothing
        /// on the ordinary path (it returns the instant the screen is open) and buys a suite
        /// that fails only for real reasons. It is still a hard assert: a screen that never
        /// opens still fails, loudly, with which of the two things went wrong.
        /// </summary>
        /// <summary>
        /// Presses the making verb until the counter's cellar is open. The same shape as
        /// OpenUntil and for the same reason — the roller takes a moment and a press that
        /// lands on a moving room is a press into nothing — but it waits on the STAGE's own
        /// answer rather than on a panel, because the cellar is not one.
        /// </summary>
        private IEnumerator OpenTheCellar(string doorName)
        {
            // THE KEY IS A TOGGLE NOW, so this LOOKS BEFORE IT PRESSES. A retry that
            // presses blind cannot drive a toggle — every even press undoes the odd one —
            // which this suite learned once already and is not learning twice.
            //
            // "Is the cellar open" is asked of the POINTER, because the suite may not look
            // inside the UI (CLAUDE.md): the doors only take a ray once the roller is clear,
            // so a door answering IS the cellar being open, and it is what the player relies on.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                yield return LetTheHostFinish();
                var door = Find(doorName);
                if (door != null && Reaches(door)) yield break;
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
                yield return new WaitForSecondsRealtime(0.6f);   // the roller's own travel
            }
            Assert.Fail("six presses of the roller never opened it onto " + doorName);
        }

        /// <summary>
        /// Is this reachable by a click at its own centre? IT ASKS WHETHER THE DOOR IS IN THE
        /// RAY AT ALL, not whether it is first. The strict version — first hit or nothing —
        /// held this test red while the smoke suite drove the very same door to the very same
        /// bench and passed, which is the tell that something harmless is sitting in the ray
        /// above it and the click still arrives. A test that is stricter than the thing it is
        /// standing in for reports failures the game does not have.
        /// </summary>
        private static bool Reaches(RectTransform target)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var at = RectTransformUtility.WorldToScreenPoint(null, target.position);
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = at };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var h in hits) if (h.gameObject == target.gameObject) return true;
            return false;
        }

        private IEnumerator OpenUntil(string press, string expected)
        {
            const int Attempts = 6;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var target = Find(press);
                Assert.That(target, Is.Not.Null, $"'{press}' is not on the screen to press");
                yield return ClickOn(target);

                var panel = Find(expected);
                if (panel != null && panel.gameObject.activeInHierarchy) yield break;
                yield return new WaitForSecondsRealtime(0.4f);
            }
            var last = Find(expected);
            Assert.Fail($"pressing '{press}' {Attempts} times never opened '{expected}' " +
                        (last == null ? "(it was never even built)" : "(it stayed closed)"));
        }

        // ── the comparison ───────────────────────────────────────────────────────

        private static string BaselineDir =>
            Path.Combine(Application.dataPath, "Tests", "PlayMode", "Baselines~");

        private static string FailureDir =>
            Path.Combine(Application.dataPath, "..", "Temp", "UiLooks");

        /// <summary>
        /// Captures the screen and holds it against the blessed picture, pixel for pixel.
        /// A missing baseline is written and then FAILS the run: a picture nobody has looked
        /// at is not a baseline, it is just the last thing that happened.
        /// </summary>
        private IEnumerator LooksTheSame(string name, RectInt? region = null)
        {
            // WAIT FOR IT TO STOP MOVING, rather than for a number of seconds. Panels slide
            // and fade in, and a screen caught mid-fade is 4% brighter than the same screen a
            // moment later — which is how the market failed its own baseline by 190,000
            // pixels that were all the right pixels. Two identical captures in a row is the
            // only honest definition of "settled", and it costs a few frames.
            Color32[] now = null, previous = null;
            RectInt roi = default;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSecondsRealtime(0.15f);
                yield return new WaitForEndOfFrame();

                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(shot.width, Is.EqualTo(DesignW),
                    $"the screen is {shot.width}x{shot.height}; these pictures are only " +
                    $"comparable at {DesignW}x{DesignH}");
                roi = region ?? new RectInt(0, 0, shot.width, shot.height);
                var pixels = Crop(shot, roi);
                Object.Destroy(shot);

                if (previous != null && Identical(previous, pixels)) { now = pixels; break; }
                previous = pixels;
            }
            Assert.That(now, Is.Not.Null,
                $"'{name}' never held still long enough to be compared — something on it is " +
                "always moving, so it cannot be tested this way (see the class comment).");

            Directory.CreateDirectory(BaselineDir);
            string blessed = Path.Combine(BaselineDir, name + ".png");
            if (!File.Exists(blessed))
            {
                File.WriteAllBytes(blessed, Encode(now, roi.width, roi.height));
                Assert.Fail($"no blessed picture for '{name}' — one was written to {blessed}. " +
                            "Look at it; if it is what the game should look like, run again.");
            }

            var before = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(before.LoadImage(File.ReadAllBytes(blessed)), Is.True,
                    $"'{blessed}' is not a readable picture");
                Assert.That(new Vector2Int(before.width, before.height),
                    Is.EqualTo(new Vector2Int(roi.width, roi.height)),
                    $"the blessed '{name}' is {before.width}x{before.height} and the region " +
                    $"being compared is {roi.width}x{roi.height} — re-bless it");

                var was = before.GetPixels32();
                int differing = 0, worst = 0;
                int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                for (int i = 0; i < now.Length; i++)
                {
                    int d = Mathf.Max(Mathf.Abs(now[i].r - was[i].r),
                            Mathf.Max(Mathf.Abs(now[i].g - was[i].g), Mathf.Abs(now[i].b - was[i].b)));
                    if (d == 0) continue;
                    differing++;
                    worst = Mathf.Max(worst, d);
                    int x = i % roi.width, y = i / roi.width;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }

                if (differing > 0)
                {
                    Directory.CreateDirectory(FailureDir);
                    File.WriteAllBytes(Path.Combine(FailureDir, name + ".now.png"),
                                       Encode(now, roi.width, roi.height));
                    File.WriteAllBytes(Path.Combine(FailureDir, name + ".diff.png"),
                                       Encode(Difference(was, now), roi.width, roi.height));
                    Assert.Fail(
                        $"'{name}' does not look the way it did: {differing} pixels differ " +
                        $"(worst channel {worst}), inside x {minX}..{maxX}, y " +
                        $"{roi.height - 1 - maxY}..{roi.height - 1 - minY} from the top of " +
                        $"the compared region. What it looks like now, and what changed, " +
                        $"are written to {Path.GetFullPath(FailureDir)}.");
                }
            }
            finally { Object.Destroy(before); }
        }

        private static bool Identical(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b) return false;
            return true;
        }

        /// <summary>
        /// The region, in the order Unity keeps pixels: BOTTOM-UP. Everything downstream —
        /// the PNG writer, the blessed picture read back with LoadImage — is bottom-up too,
        /// and the first run of this file compared a top-down capture against a bottom-up
        /// baseline: 860,198 of 921,600 pixels "differed", which is what an image looks like
        /// held against its own mirror.
        ///
        /// The region itself is given the way a person reads a screen (y down from the top),
        /// so that flip happens here, once.
        /// </summary>
        private static Color32[] Crop(Texture2D shot, RectInt roi)
        {
            var pixels = shot.GetPixels32();
            int bottom = shot.height - roi.y - roi.height;
            var cut = new Color32[roi.width * roi.height];
            for (int y = 0; y < roi.height; y++)
                System.Array.Copy(pixels, (bottom + y) * shot.width + roi.x,
                                  cut, y * roi.width, roi.width);
            return cut;
        }

        /// <summary>What changed, drawn: every differing pixel lit, everything else black.
        /// Amplified, because a one-value difference is invisible and still a difference.</summary>
        private static Color32[] Difference(Color32[] was, Color32[] now)
        {
            var diff = new Color32[now.Length];
            for (int i = 0; i < now.Length; i++)
            {
                byte d = (byte)Mathf.Min(255, Mathf.Max(Mathf.Abs(now[i].r - was[i].r),
                        Mathf.Max(Mathf.Abs(now[i].g - was[i].g), Mathf.Abs(now[i].b - was[i].b))) * 8);
                diff[i] = new Color32(d, d, d, 255);
            }
            return diff;
        }

        private static byte[] Encode(Color32[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);      // already bottom-up, like everything else here
            tex.Apply(false);
            var png = tex.EncodeToPNG();
            Object.Destroy(tex);
            return png;
        }

        // ── the same hand the smoke suite uses ───────────────────────────────────

        private IEnumerator OpenTheBar()
        {
            for (int i = 0; i < 180 && (Screen.width != DesignW || Screen.height != DesignH); i++)
                yield return null;
            Assert.That(Screen.width, Is.EqualTo(DesignW),
                "the Game view never became the design width — the pictures would not match anything");

            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            float waited = 0f;
            while (waited < 20f)
            {
                _boot = Object.FindFirstObjectByType<GameBootstrap>();
                if (_boot != null && _boot.Tycoon != null) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(_boot, Is.Not.Null, "the scene has no GameBootstrap in it");
            Assert.That(_boot.Tycoon, Is.Not.Null, "the run never started");

            // THE BAR IS OPEN WHEN ITS CLOCK IS RUNNING (2026-08-13, and the same fix is in
            // the smoke suite). The room opens behind a CURTAIN on a canvas above everything,
            // and the HUD holds the night's clock until it lifts — so a press before that is
            // a press into a black screen, which is what kept failing the first test of every
            // session. Elapsed moving is the game's own signal that the doors are open.
            // …AND THE PHASE THE DOORS ANSWER TO (2026-08-22). The clock was necessary and
            // not sufficient: every door in the flow guards on `Phase != DayOpen` and refuses
            // WITHOUT A SOUND, so a press one phase early is swallowed and OpenUntil reports
            // "six presses never opened the panel" for what is really "the bar was not open".
            // That is the intermittent red this suite kept throwing at whichever screen ran
            // first, which is why the failing test name moved around and the cause did not.
            float open = Time.realtimeSinceStartup + 15f;
            while ((_boot.Tycoon.Floor.Elapsed <= 0
                    || _boot.Tycoon.Phase != TycoonPhase.DayOpen)
                   && Time.realtimeSinceStartup < open)
                yield return null;
            Assert.That(_boot.Tycoon.Floor.Elapsed, Is.GreaterThan(0),
                "the curtain never lifted — the night's clock never started");
            Assert.That(_boot.Tycoon.Phase, Is.EqualTo(TycoonPhase.DayOpen),
                "the run never reached DayOpen — a press before that is swallowed in silence");
            yield return null;
            yield return null;
        }

        // ── the host, heard out ──────────────────────────────────────────────────

        /// <summary>
        /// The host speaks up the first time each thing happens (GDD 26 §1b, 2026-09-05) —
        /// on the plate during the night, in a 98 box over the market — and the player
        /// answers with its one key before reaching past her. So does this mouse; and a
        /// picture taken with her box still up is a picture of her box, not of the screen
        /// under it. Bounded: a plate that never goes away is a failure worth seeing.
        /// </summary>
        private IEnumerator LetTheHostFinish()
        {
            for (int i = 0; i < 12; i++)
            {
                var key = HostKey();
                if (key == null) yield break;
                yield return ClickCentre(key);
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        private static RectTransform HostKey()
        {
            var plate = Find("LastCallPlate");
            if (plate != null && plate.gameObject.activeInHierarchy) return Under(plate, "Listen");
            var note = Find("HostNote");
            if (note != null && note.gameObject.activeInHierarchy) return Under(note, "Key");
            return null;
        }

        private static RectTransform Under(RectTransform root, string name)
        {
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == name) return rt;
            return null;
        }

        /// <summary>ClickOn, at the centre of the face: keys are placed by their anchor, so
        /// the rect's own position is an edge and a press there can land beside it.</summary>
        private IEnumerator ClickCentre(RectTransform target)
        {
            Assert.That(target, Is.Not.Null, "there is nothing there to click");
            var at = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            Set(_mouse.position, at);
            yield return null;
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            yield return null;
            Release(_mouse.leftButton);
            yield return new WaitForSecondsRealtime(SettleSeconds);
        }

        private IEnumerator ClickOn(RectTransform target)
        {
            Assert.That(target, Is.Not.Null, "there is nothing there to click");
            var at = RectTransformUtility.WorldToScreenPoint(null, target.position);
            Set(_mouse.position, at);
            yield return null;
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            yield return null;
            Release(_mouse.leftButton);
            yield return new WaitForSecondsRealtime(SettleSeconds);
        }

        /// <summary>Every graphic the UI raycaster finds at a screen point, nearest first.</summary>
        private static string WhatIsUnder(Vector2 at)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return "(no EventSystem)";
            var ped = new UnityEngine.EventSystems.PointerEventData(es) { position = at };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(ped, hits);
            if (hits.Count == 0) return "(nothing)";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < hits.Count && i < 8; i++)
                sb.Append('[').Append(hits[i].gameObject.name)
                  .Append('@').Append(hits[i].sortingOrder).Append(']');
            return sb.ToString();
        }

        private static RectTransform Find(string name)
        {
            RectTransform hidden = null;
            foreach (var rt in Object.FindObjectsByType<RectTransform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rt.name != name) continue;
                if (rt.gameObject.activeInHierarchy) return rt;
                hidden = hidden != null ? hidden : rt;
            }
            return hidden;
        }
    }
}
