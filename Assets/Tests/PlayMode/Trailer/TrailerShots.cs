using System.Collections;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LastCall.PlayTests.Trailer
{
    /// <summary>
    /// THE TRAILER SHOOTS ITSELF (2026-10-02, the author: "videoyu sen kayıt et sen oluştur"). Each test here is one
    /// shot of the trailer: the real scene, played by a scripted hand, filmed by TrailerCamera into
    /// Recordings/Trailer/{shot}.mp4 with its beats marked beside it. Tools/trailer/edit.py cuts them on the music.
    ///
    /// NOT PART OF THE SUITE. Every shot is ignored unless TrailerSwitch is on, which only the menu item
    /// (LastCall -> Trailer -> Record All Shots) sets, so the PlayMode suite stays what it was and a shot never runs
    /// by accident on a push. Nothing here asserts on the game - a shot that goes wrong is a take to throw away, not
    /// a red test - but each one does fail loudly when it cannot find what it came to film, because a silent shot
    /// is a 20-second film of nothing.
    ///
    /// THE HAND IS A HUMAN'S, NOT THE SUITE'S. The suite jumps the pointer and waits on the wall clock; on film both
    /// look like a machine. Here every move is an eased glide over game time (TrailerCamera steps the clock 1/60 per
    /// frame, so a wall-clock wait would be too short on a slow capture), every press rests a beat before and after,
    /// and the clock is never fast-forwarded on camera.
    /// </summary>
    public sealed partial class TrailerShots : InputTestFixture
    {
        private const string SceneName = "Main";
        private const int FilmW = 1920, FilmH = 1080;

        private Mouse _mouse;
        private GameBootstrap _boot;
#if UNITY_EDITOR
        private uint _windowW, _windowH;
#endif

        [OneTimeSetUp]
        public void Stage()
        {
            if (!TrailerSwitch.On)
                Assert.Ignore("trailer shots are filmed from LastCall -> Trailer -> Record All Shots, not by the suite");
            Ceremony.Pace = 1f;
            Localization.UseForSession(TrailerSwitch.Language);
            PlayerOptions.UseDefaultsForSession();
            SeedPolicy.UseForSession(TrailerSwitch.Seed);
            SaveStore.DisableForSession();
            // a filmed run is a dev run (the presets): nothing it does may reach the player's achievements or Steam
            Achievements.DisableForSession();
            GameBootstrap.TourForNewRuns = false;
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.GetRenderingResolution(out _windowW, out _windowH);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(FilmW, FilmH, "LastCall Trailer");
#endif
        }

        [OneTimeTearDown]
        public void Strike()
        {
            TrailerCamera.Cut();
            Ceremony.Pace = 1f;
#if UNITY_EDITOR
            if (_windowW > 0 && _windowH > 0)
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution(_windowW, _windowH, "LastCall");
#endif
        }

        public override void Setup()
        {
            base.Setup();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _mouse = InputSystem.AddDevice<Mouse>();
            _keys = InputSystem.AddDevice<Keyboard>();
            // the hand starts where a player's rests: low right, off the work
            Set(_mouse.position, new Vector2(FilmW * 0.78f, FilmH * 0.22f));
        }

        public override void TearDown()
        {
            TrailerCamera.Cut();
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if (_keys != null && _keys.added) InputSystem.RemoveDevice(_keys);
            _mouse = null;
            _keys = null;
            _stoolOf.Clear();
            _litLastLook.Clear();
            base.TearDown();
        }

        // ── the clock: game time, which is film time ─────────────────────────────────────────────────────────

        /// <summary>Waits this many seconds of the game's own (unscaled) clock - film seconds while the camera rolls.</summary>
        private static IEnumerator Hold(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }

        /// <summary>Waits until <paramref name="done"/> is true, at most <paramref name="seconds"/> of game time; false if it never came.</summary>
        private static IEnumerator Until(System.Func<bool> done, float seconds, System.Action<bool> result = null)
        {
            float until = Time.unscaledTime + seconds;
            while (!done() && Time.unscaledTime < until) yield return null;
            result?.Invoke(done());
        }

        // ── the hand ─────────────────────────────────────────────────────────────────────────────────────────

        private Vector2 HandAt => _mouse.position.ReadValue();

        /// <summary>Moves the pointer to <paramref name="to"/> over <paramref name="seconds"/>, eased in and out, on a slight
        /// arc - the path a wrist takes, not a ruler's.</summary>
        private IEnumerator Glide(Vector2 to, float seconds = -1f)
        {
            var from = HandAt;
            float dist = (to - from).magnitude;
            if (seconds < 0f) seconds = Mathf.Clamp(0.18f + dist / 1900f, 0.18f, 0.75f);
            var bow = new Vector2(-(to - from).y, (to - from).x).normalized * Mathf.Min(40f, dist * 0.08f);
            float t0 = Time.unscaledTime;
            while (true)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - t0) / seconds);
                float e = u * u * (3f - 2f * u);
                Set(_mouse.position, Vector2.Lerp(from, to, e) + bow * Mathf.Sin(Mathf.PI * e));
                if (u >= 1f) break;
                yield return null;
            }
            Set(_mouse.position, to);
            yield return null;
        }

        /// <summary>Follows a moving target: glides toward wherever <paramref name="where"/> says it is now.</summary>
        private IEnumerator GlideTo(System.Func<Vector2> where, float seconds = -1f)
        {
            var from = HandAt;
            var first = where();
            float dist = (first - from).magnitude;
            if (seconds < 0f) seconds = Mathf.Clamp(0.18f + dist / 1900f, 0.18f, 0.75f);
            float t0 = Time.unscaledTime;
            while (true)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - t0) / seconds);
                float e = u * u * (3f - 2f * u);
                Set(_mouse.position, Vector2.Lerp(from, where(), e));
                if (u >= 1f) break;
                yield return null;
            }
            yield return null;
        }

        /// <summary>A click as a player makes one: travel, settle, press, release, let the press land.</summary>
        private IEnumerator Click(RectTransform target, Vector2 nudge = default)
        {
            Assert.That(target, Is.Not.Null, "the shot came to click something that is not on screen");
            yield return GlideTo(() => ScreenPointOf(target) + nudge);
            yield return Hold(0.08f);
            Set(_mouse.position, ScreenPointOf(target) + nudge);
            Press(_mouse.leftButton);
            yield return Hold(0.07f);
            Release(_mouse.leftButton);
            yield return Hold(0.30f);
        }

        /// <summary>Clicks the centre of the rect's face (keys are hung by an edge, not their middle).</summary>
        private IEnumerator ClickFace(RectTransform target)
        {
            Assert.That(target, Is.Not.Null, "the shot came to press a key that is not on screen");
            var centre = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            yield return Click(target, centre - ScreenPointOf(target));
        }

        /// <summary>Press here, carry along <paramref name="path"/> (screen points), let go: a drag a hand makes.</summary>
        private IEnumerator Drag(Vector2 from, Vector2[] path, float secondsPerLeg = 0.35f, float holdAtEnd = 0.1f)
        {
            yield return Glide(from);
            yield return Hold(0.08f);
            Press(_mouse.leftButton);
            yield return Hold(0.08f);
            foreach (var p in path) yield return Glide(p, secondsPerLeg);
            yield return Hold(holdAtEnd);
            Release(_mouse.leftButton);
            yield return Hold(0.2f);
        }

        // ── the bar, opened ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>The scene, to the front door. Back once the main menu is up (the menu is a shot of its own).</summary>
        private IEnumerator LoadToTheDoor()
        {
            for (int i = 0; i < 240 && (Screen.width != FilmW || Screen.height != FilmH); i++) yield return null;
            Assert.That(Screen.width, Is.EqualTo(FilmW),
                "the Game view never became 1920x1080 - dock it where it can be that size and film again");
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            bool booted = false;
            yield return Until(() =>
            {
                _boot = Object.FindFirstObjectByType<GameBootstrap>();
                return _boot != null && _boot.Tycoon != null;
            }, 30f, ok => booted = ok);
            Assert.That(booted, Is.True, "the run never started");
            bool door = false;
            yield return Until(() => GameObject.Find("MainMenu/Column/NEW RUN") != null, 15f, ok => door = ok);
            Assert.That(door, Is.True, "the front door never offered NEW RUN");
            yield return Hold(0.5f);
        }

        /// <summary>NEW RUN under the hand, then the doors: back when the night's clock runs and DayOpen holds.</summary>
        private IEnumerator WalkInAndOpen()
        {
            var newRun = GameObject.Find("MainMenu/Column/NEW RUN");
            for (int attempt = 0; attempt < 6 && newRun != null && newRun.activeInHierarchy; attempt++)
            {
                yield return ClickFace((RectTransform)newRun.transform);
                yield return Hold(0.4f);
            }
            TrailerCamera.Mark("new run");
            bool open = false;
            yield return Until(() =>
            {
                var run = _boot.Tycoon;
                if (run.Talking || HostKey() != null) return false;
                return run.Floor.Elapsed > 0 && run.Phase == TycoonPhase.DayOpen;
            }, 30f, ok => open = ok);
            if (!open) yield return HearTheHostOut();
            yield return Until(() => _boot.Tycoon.Floor.Elapsed > 0 && _boot.Tycoon.Phase == TycoonPhase.DayOpen, 20f,
                ok => open = ok);
            Assert.That(open, Is.True, "the bar never opened");
            TrailerCamera.Mark("open");
        }

        /// <summary>Whatever the host has to say, heard out key by key, as a player would (and quick about it).</summary>
        private IEnumerator HearTheHostOut()
        {
            float until = Time.unscaledTime + 30f;
            int presses = 0;
            while (Time.unscaledTime < until && presses < 40)
            {
                var key = HostKey();
                if (key != null)
                {
                    presses++;
                    yield return Hold(0.6f);           // long enough to read a line on film
                    yield return ClickFace(key);
                    continue;
                }
                var run = _boot != null ? _boot.Tycoon : null;
                if (run == null || (run.HostessVisit == null && !run.Talking)) yield break;
                yield return Hold(0.1f);
            }
        }

        private static RectTransform HostKey()
        {
            // a SHOWN key only: Find falls back to a hidden one, and the plate hides its key on the lines that wait
            var plate = Find("LastCallPlate");
            if (Shown(plate)) { var k = Find("Listen", plate); if (Shown(k)) return k; }
            var note = Find("HostNote");
            if (Shown(note)) { var k = Find("Key", note); if (Shown(k)) return k; }
            return null;
        }

        // ── finding things by name, as the suite does ────────────────────────────────────────────────────────

        private static Vector2 ScreenPointOf(RectTransform rt) => RectTransformUtility.WorldToScreenPoint(null, rt.position);

        private static Vector2 ScreenPointIn(RectTransform panel, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(local));

        private static Vector2 FaceOf(RectTransform rt) =>
            RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));

        private static RectTransform Find(string name, RectTransform under = null)
        {
            RectTransform hidden = null;
            var all = under != null
                ? under.GetComponentsInChildren<RectTransform>(true)
                : Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var rt in all)
            {
                if (rt.name != name) continue;
                if (rt.gameObject.activeInHierarchy) return rt;
                hidden = hidden != null ? hidden : rt;
            }
            return hidden;
        }

        private static bool Shown(RectTransform rt) => rt != null && rt.gameObject.activeInHierarchy;

        private static string WhatIsUnder(Vector2 screen)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return "";
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = screen };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < hits.Count && i < 4; i++) sb.Append('[').Append(hits[i].gameObject.name).Append(']');
            return sb.ToString();
        }

        private static string TextOf(RectTransform rt)
        {
            var t = rt != null ? rt.GetComponent<Text>() : null;
            return t != null ? t.text : null;
        }
    }
}
