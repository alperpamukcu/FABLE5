using System.Reflection;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// THE PLAYER'S OPTIONS (2026-09-26, the settings' DISPLAY page and INVERT POUR): one store in Game, every
    /// default today's game but one, a key an option, and a session the PlayMode suite can pin without saving a thing.
    /// Every test runs on a store of its own, so the author's own settings are never read or written.
    /// </summary>
    public sealed class PlayerOptionsTests
    {
        private PlayerOptions.MemoryStore _store;

        [SetUp]
        public void AStoreOfItsOwn()
        {
            _store = new PlayerOptions.MemoryStore();
            PlayerOptions.UseStore(_store);
        }

        [TearDown]
        public void ThePrefsBack() => PlayerOptions.UseStore(null);

        [Test]
        public void EveryDefault_IsTodaysGame()
        {
            Assert.IsFalse(PlayerOptions.ReducedMotion, "motion is full");
            Assert.IsTrue(PlayerOptions.Flashes, "the neon, the room and the television still flicker");
            Assert.AreEqual(PlayerOptions.PointerSize.Normal, PlayerOptions.Pointer, "the 32x32 hardware hand");
            Assert.AreEqual(PlayerOptions.ColourCues.Standard, PlayerOptions.Cues, "the author's red-to-green ladder");
            Assert.AreEqual(0, PlayerOptions.FrameCap, "MATCH SCREEN: vsync, as GameBootstrap set it");
            Assert.IsFalse(PlayerOptions.InvertPour, "higher tips further (GDD 24 §2.2)");
            Assert.AreEqual(0, _store.Writes, "reading a default writes nothing");
        }

        [Test]
        public void PauseWhenAway_IsTheOneDefaultThatIsNotTodays()
        {
            // Today the night runs on behind another window. A player's build now holds it by default...
            Assert.IsTrue(PlayerOptions.PauseWhenAwayInPlayer);
            // ...and the editor, where these tests run, never does: its default is off, and a player who turns it on
            // in the editor still never pauses on focus (the author drives the editor from the IDE).
            Assert.IsFalse(PlayerOptions.PauseWhenAway, "the editor's default is off");
            PlayerOptions.PauseWhenAway = true;
            Assert.IsTrue(PlayerOptions.PauseWhenAway, "the row shows what was set");
            Assert.IsFalse(PlayerOptions.PausesWhenAway, "the editor never pauses on focus");
        }

        [Test]
        public void EveryOption_RoundTripsThroughItsKey()
        {
            PlayerOptions.ReducedMotion = true;
            PlayerOptions.Flashes = false;
            PlayerOptions.Pointer = PlayerOptions.PointerSize.Large;
            PlayerOptions.Cues = PlayerOptions.ColourCues.Clear;
            PlayerOptions.FrameCap = 144;
            PlayerOptions.PauseWhenAway = true;
            PlayerOptions.InvertPour = true;

            Assert.AreEqual(1, _store.Values["lastcall.reducedMotion"], "Motion's key is the one it always had");
            Assert.AreEqual(1, _store.Values[PlayerOptions.NoFlashesKey], "stored as NO flashes");
            Assert.AreEqual(1, _store.Values[PlayerOptions.PointerKey]);
            Assert.AreEqual(1, _store.Values[PlayerOptions.ColourCuesKey]);
            Assert.AreEqual(144, _store.Values[PlayerOptions.FrameCapKey]);
            Assert.AreEqual(1, _store.Values[PlayerOptions.PauseAwayKey]);
            Assert.AreEqual(1, _store.Values[PlayerOptions.InvertPourKey]);

            PlayerOptions.Forget();   // what the next play does: read them back
            Assert.IsTrue(PlayerOptions.ReducedMotion);
            Assert.IsFalse(PlayerOptions.Flashes);
            Assert.AreEqual(PlayerOptions.PointerSize.Large, PlayerOptions.Pointer);
            Assert.AreEqual(PlayerOptions.ColourCues.Clear, PlayerOptions.Cues);
            Assert.AreEqual(144, PlayerOptions.FrameCap);
            Assert.IsTrue(PlayerOptions.PauseWhenAway);
            Assert.IsTrue(PlayerOptions.InvertPour);
        }

        [Test]
        public void AFrameCapNotOffered_ReadsAsMatchScreen()
        {
            _store.Values[PlayerOptions.FrameCapKey] = 30;          // hand-edited, or an older build's choice
            PlayerOptions.Forget();
            Assert.AreEqual(0, PlayerOptions.FrameCap, "30 is not offered: the spring steps are, but the choice is not");
            PlayerOptions.FrameCap = 75;
            Assert.AreEqual(0, _store.Values[PlayerOptions.FrameCapKey], "a cap not on the list is stored as MATCH SCREEN");
        }

        [Test]
        public void UseDefaultsForSession_WritesNothing_AndHidesTheSavedChoices()
        {
            _store.Values[PlayerOptions.ReducedMotionKey] = 1;
            _store.Values[PlayerOptions.PointerKey] = 1;
            _store.Values[PlayerOptions.ColourCuesKey] = 1;

            PlayerOptions.UseDefaultsForSession();
            Assert.IsTrue(PlayerOptions.InSession);
            Assert.IsFalse(PlayerOptions.ReducedMotion, "the suite plays with full motion whatever the author picked");
            Assert.AreEqual(PlayerOptions.PointerSize.Normal, PlayerOptions.Pointer);
            Assert.AreEqual(PlayerOptions.ColourCues.Standard, PlayerOptions.Cues, "the bench baseline wears STANDARD");
            PlayerOptions.InvertPour = true;                        // a choice made inside the session...
            Assert.IsTrue(PlayerOptions.InvertPour);
            Assert.AreEqual(0, _store.Writes, "...is the session's own and is never saved");

            PlayerOptions.Forget();                                 // the next play
            Assert.IsFalse(PlayerOptions.InSession);
            Assert.IsTrue(PlayerOptions.ReducedMotion, "the saved choices come back");
            Assert.IsFalse(PlayerOptions.InvertPour);
        }

        [Test]
        public void TheCachesAreForgottenWhenPlayStarts()
        {
            // The project enters play mode without a domain reload: the forgetting must hang on the same hook
            // Localization's does, or a session's defaults would outlive the suite into the author's next play.
            var method = typeof(PlayerOptions).GetMethod("ForgetOnPlay", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "PlayerOptions.ForgetOnPlay");
            var hook = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(hook, "ForgetOnPlay runs when play starts");
            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, hook.loadType);

            PlayerOptions.UseDefaultsForSession();
            method.Invoke(null, null);
            Assert.IsFalse(PlayerOptions.InSession);
        }

        [Test]
        public void ResetDefaults_PutsEveryOptionBack()
        {
            PlayerOptions.ReducedMotion = true;
            PlayerOptions.Flashes = false;
            PlayerOptions.Pointer = PlayerOptions.PointerSize.Large;
            PlayerOptions.Cues = PlayerOptions.ColourCues.Clear;
            PlayerOptions.FrameCap = 60;
            PlayerOptions.PauseWhenAway = true;
            PlayerOptions.InvertPour = true;

            PlayerOptions.ResetDefaults();
            Assert.IsFalse(PlayerOptions.ReducedMotion);
            Assert.IsTrue(PlayerOptions.Flashes);
            Assert.AreEqual(PlayerOptions.PointerSize.Normal, PlayerOptions.Pointer);
            Assert.AreEqual(PlayerOptions.ColourCues.Standard, PlayerOptions.Cues);
            Assert.AreEqual(0, PlayerOptions.FrameCap);
            Assert.IsFalse(PlayerOptions.PauseWhenAway, "the editor's default");
            Assert.IsFalse(PlayerOptions.InvertPour);
            PlayerOptions.Forget();
            Assert.IsFalse(PlayerOptions.ReducedMotion, "and the defaults were saved");
            Assert.AreEqual(0, _store.Values[PlayerOptions.InvertPourKey]);
        }
    }
}
