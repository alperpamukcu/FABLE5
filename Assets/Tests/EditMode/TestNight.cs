using LastCall.Core;

namespace LastCall.Tests
{
    /// <summary>
    /// THE TEST NIGHTS CLEAN AS THEY GO (PLAN_house_and_law 0b, 2026-09-05). A glass left on
    /// the counter no longer clears itself (GDD 27 §4.1), so every helper that plays a night
    /// and hands drinks over calls this after each tick — the way the sim bot does — and
    /// every pinned star number stays the number of a CLEAN bar. The one night that wants to
    /// be dirty says so by not calling it.
    /// </summary>
    public static class TestNight
    {
        /// <summary>Collect every glass, wipe every mark, and run the sink when the hand is
        /// full and the tap is idle. Safe to call every tick; a night with nothing on the
        /// counter is a no-op.</summary>
        public static void Clean(TycoonRun run)
        {
            if (run.Phase != TycoonPhase.DayOpen) return;
            var messes = run.Floor.Messes;
            for (int i = messes.Count - 1; i >= 0; i--)
            {
                var mess = messes[i];
                if (mess.HasGlass) run.CollectGlass(mess);
                if (mess.Smudged) run.Wipe(mess);
            }
            if (run.GlassesInHand > 0 && !run.SinkBusy) run.WashGlasses();
        }
    }
}
