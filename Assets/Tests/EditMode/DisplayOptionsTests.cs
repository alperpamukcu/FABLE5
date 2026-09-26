using System.Collections.Generic;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// THE SCREEN (2026-09-26, the settings' DISPLAY page): the stage's pixels a screen pixel, the window sizes that
    /// keep them square, and the frame pacing a cap asks for - the arithmetic DisplayOptions does, none of which
    /// touches the screen, so it runs in the editor exactly as in a build.
    /// </summary>
    public sealed class DisplayOptionsTests
    {
        [TestCase(1280, 720, 2f)]
        [TestCase(1920, 1080, 3f)]
        [TestCase(2560, 1440, 4f)]
        [TestCase(1600, 900, 2.5f)]
        [TestCase(2560, 1080, 4f)]              // the ultrawide: the width decides
        [TestCase(3440, 1440, 5.375f)]
        [TestCase(1366, 768, 2.134375f)]        // 1366/640: the width decides by a hair
        public void Scale_IsDesignFramesPixelsAUnit(int w, int h, float px) =>
            Assert.AreEqual(px, DisplayOptions.Scale(w, h), 1e-4f);

        [Test]
        public void TheHud_IsHalfTheStagesScale()
        {
            Assert.AreEqual(1f, DisplayOptions.HudFactor(1280, 720), 1e-6f);
            Assert.AreEqual(1.5f, DisplayOptions.HudFactor(1920, 1080), 1e-6f, "why the 8-size print is soft at 1080p");
        }

        private static string Sizes(int w, int h)
        {
            var parts = new List<string>();
            foreach (var s in DisplayOptions.WholeSizes(w, h)) parts.Add(s.x + "x" + s.y);
            return string.Join(" ", parts);
        }

        [Test]
        public void WholeSizes_AreTheMultiplesTheDesktopHolds()
        {
            Assert.AreEqual("1280x720", Sizes(1920, 1080), "1080 tall leaves no room for 1920x1080 and a title bar");
            Assert.AreEqual("1280x720 1920x1080", Sizes(2560, 1440));
            Assert.AreEqual("1280x720 1920x1080 2560x1440", Sizes(2560, 1600));
            Assert.AreEqual("1280x720 1920x1080 2560x1440 3200x1800", Sizes(3840, 2160));
            Assert.AreEqual("", Sizes(1366, 768), "768 tall holds no whole size with a taskbar");
        }

        [Test]
        public void EveryWindowOffered_KeepsThePixelsSquare()
        {
            foreach (var (w, h) in new[] { (1920, 1080), (2560, 1440), (2560, 1600), (3840, 2160), (3440, 1440), (5120, 2880) })
                foreach (var s in DisplayOptions.WholeSizes(w, h))
                {
                    float px = DisplayOptions.Scale(s.x, s.y);
                    Assert.AreEqual(Mathf.Round(px), px, 1e-6f, s + " is a whole scale");
                    Assert.GreaterOrEqual(s.y, 720, s + ": under 720 the HUD's 8-size print drops under 8 screen pixels");
                    Assert.LessOrEqual(s.x, w, s + " fits " + w + "x" + h);
                    Assert.LessOrEqual(s.y, h - DisplayOptions.WindowChrome, s + " leaves room for the title bar and taskbar");
                }
        }

        [Test]
        public void Windowed_OpensAtTheLargestWholeSize()
        {
            Assert.AreEqual(new Vector2Int(1280, 720), DisplayOptions.WindowFor(1920, 1080));
            Assert.AreEqual(new Vector2Int(3200, 1800), DisplayOptions.WindowFor(3840, 2160));
            Assert.AreEqual(new Vector2Int(1280, 720), DisplayOptions.WindowFor(1366, 768), "none fits: the design's size");
            Assert.AreEqual(new Vector2Int(1024, 768), DisplayOptions.WindowFor(1024, 768), "too small even for that: the desktop");
        }

        [Test]
        public void MatchScreen_IsTodaysPacing()
        {
            // GameBootstrap set exactly this before there was a choice (2026-09-11), and it is the default.
            var (vSync, target) = DisplayOptions.Pacing(0);
            Assert.AreEqual(1, vSync);
            Assert.AreEqual(-1, target);
        }

        [Test]
        public void ACap_TurnsVsyncOff_AndTargetsIt()
        {
            foreach (int cap in DisplayOptions.FrameCaps)
            {
                if (cap == 0) continue;
                var (vSync, target) = DisplayOptions.Pacing(cap);
                Assert.AreEqual(0, vSync, cap + ": Unity ignores the target while vsync is on");
                Assert.AreEqual(cap, target);
            }
        }

        [Test]
        public void TheCaps_AreMatchScreenThenSixtyAndUp_AndNeverUncapped()
        {
            var caps = DisplayOptions.FrameCaps;
            Assert.AreEqual(0, caps[0], "MATCH SCREEN first, the default");
            for (int i = 1; i < caps.Length; i++)
            {
                Assert.GreaterOrEqual(caps[i], 60, "nothing under 60 is offered");
                Assert.Greater(caps[i], caps[i - 1], "in order, each once");
            }
            CollectionAssert.DoesNotContain(caps, -1, "no uncapped choice: a steady frame, not the most frames");
        }
    }

    /// <summary>
    /// THE HAND'S SPRING, STEPPED SMALL (2026-09-26): PourHand's follow spring (omega 30, zeta 0.85) diverged in one
    /// semi-implicit step at 30 fps; cut into steps of a sixtieth it settles at 30 and at 20, and at 60 fps and
    /// faster it is the old single step, bit for bit, so nothing about the feel moved.
    /// </summary>
    public sealed class SpringStepTests
    {
        private const float Omega = 30f, Zeta = 0.85f;
        private const float Gap = 100f;

        [TestCase(1f / 144f, 1)]
        [TestCase(1f / 120f, 1)]
        [TestCase(1f / 60f, 1)]
        [TestCase(0.0169f, 1)]                   // a 60 Hz frame's jitter is still one step
        [TestCase(1f / 30f, 2)]
        [TestCase(1f / 20f, 3)]
        [TestCase(0f, 0)]
        public void AFrame_IsCutIntoSixtieths(float dt, int steps) =>
            Assert.AreEqual(steps, SpringStep.Substeps(dt));

        [TestCase(30)]
        [TestCase(20)]
        public void AtLowFrameRates_TheHandSettles(int fps)
        {
            float dt = 1f / fps, x = 0f, v = 0f;
            for (int frame = 0; frame < 3 * fps; frame++)
            {
                SpringStep.Advance(ref x, ref v, Gap, Omega, Zeta, dt);
                Assert.LessOrEqual(Mathf.Abs(Gap - x), Gap, "frame " + frame + ": the error never grows past the move");
            }
            Assert.Less(Mathf.Abs(Gap - x), Gap * 0.01f, "settled within 1% in three seconds");
        }

        [Test]
        public void TheOldSingleStep_FlewOffAtThirty()
        {
            // What the hand did before (one step a frame, dt clamped to 1/30): the spectral radius is 1.26 there.
            float x = 0f, v = 0f;
            for (int frame = 0; frame < 90; frame++) SpringStep.Damped(ref x, ref v, Gap, Omega, Zeta, 1f / 30f);
            Assert.Greater(Mathf.Abs(Gap - x), Gap, "diverged");
        }

        [TestCase(60)]
        [TestCase(120)]
        [TestCase(144)]
        public void AtSixtyAndFaster_ItIsTheOldStepBitForBit(int fps)
        {
            float dt = 1f / fps;
            float x1 = 0f, v1 = 0f, x2 = 0f, v2 = 0f;
            for (int frame = 0; frame < fps; frame++)
            {
                SpringStep.Advance(ref x1, ref v1, Gap, Omega, Zeta, dt);
                SpringStep.Damped(ref x2, ref v2, Gap, Omega, Zeta, dt);
                Assert.AreEqual(x2, x1, 0f, "frame " + frame);
                Assert.AreEqual(v2, v1, 0f, "frame " + frame);
            }
        }
    }
}
