using System;

namespace LastCall.Core
{
    /// <summary>
    /// THE HOUSE (GDD 27, 2026-09-05): what the room is worth, and the counter's own night.
    /// The run's half of the two-ratings rule — SERVICE is what the customers thought of the
    /// drink, COMFORT is what the room is worth less the mess, and the night files the LOWER
    /// (<see cref="StarCeiling"/> reads it). The pure rules live in <see cref="VenueComfort"/>
    /// and <see cref="Housekeeping"/>; this file is where the run hands them what it owns and
    /// forwards the four verbs with the phase guard every verb carries.
    /// </summary>
    public sealed partial class TycoonRun
    {
        // ── what the room is worth ───────────────────────────────────────────────

        /// <summary>
        /// Σ <c>FixtureDefinition.Comfort</c> over what the room STANDS: the tallest owned rung
        /// of every ladder slot, plus every owned single piece. A fitted-over rung counts
        /// nothing — rungs carry absolute values, not increments (GDD 27 §3) — which is the
        /// same filter the room uses to decide what to draw. A run built without a fixture
        /// catalogue is worth nothing here, and the free base is the whole of its room.
        /// </summary>
        public double FixtureComfort
        {
            get
            {
                double sum = 0;
                foreach (var f in _fixtureCatalogue)
                {
                    if (!_fixtures.Contains(f.Id)) continue;
                    if (f.Level > 0 && f.Level < LadderLevel(f.Slot)) continue;   // fitted over
                    sum += f.Comfort;
                }
                return sum;
            }
        }

        /// <summary>The glassware ladder's cap, as the old fittings ceiling summed it
        /// (front-loaded per step, measured 2026-08-02 — see <see cref="GlassStepCap"/>).
        /// Comfort counts half of it (<see cref="VenueComfort.GlassComfortShare"/>).</summary>
        public double GlassStepsCap
        {
            get
            {
                double cap = 0;
                foreach (var g in _glassware)
                {
                    int steps = GlassTier(g.Id) - 1;
                    for (int s = 0; s < steps && s < GlassStepCap.Length; s++)
                        cap += GlassStepCap[s];
                }
                return cap;
            }
        }

        /// <summary>What the room is worth with nobody in it (GDD 27 §3): the free base, the
        /// fittings, half the glass ladder, the extra stools. Changes at the market, never
        /// during a night. A fresh bar is worth exactly two — the number the retired
        /// UpgradeStarCap opened with, so every pin that says so stays true.</summary>
        public double ComfortBase =>
            VenueComfort.Base(FixtureComfort, GlassStepsCap, Math.Max(0, Seats - _config.StartingSeats));

        /// <summary>The room as it stands THIS SECOND: the base less the messes past their
        /// grace (GDD 27 §2.2). The shift's gauge, and what the sim reads per tick.</summary>
        public double ComfortNow =>
            VenueComfort.Now(ComfortBase, Floor.House.DirtySpots, Seats);

        /// <summary>Tonight's filed comfort: the base less what the mess cost over the whole
        /// night so far, time-weighted per seat against the night the floor actually ran.
        /// Exactly the number <see cref="ContinueToNextDay"/> files as ComfortStars, asked
        /// before it is filed.</summary>
        public double ComfortTonight =>
            VenueComfort.Tonight(ComfortBase, Floor.House.Cleanliness(Seats, Floor.Elapsed));

        /// <summary>The share of the night the counter was clean so far, 0..1.</summary>
        public double CleanlinessTonight => Floor.House.Cleanliness(Seats, Floor.Elapsed);

        /// <summary>What the customers thought of the drinks, in stars, held under the menu
        /// ceiling (GDD 27 §2.1). The other of the two ratings; tomorrow's crowd reads THIS
        /// side (§2.3), so a filthy counter can hold the standing down but never by itself
        /// turn the crowd broke.</summary>
        public double ServiceTonight =>
            Math.Min(BarRating.ExactStarsFor(Floor.AverageSatisfaction), MenuStarCap);

        // ── the counter's night, forwarded ───────────────────────────────────────

        /// <summary>Empty glasses collected and not yet washed.</summary>
        public int GlassesInHand => Floor.House.GlassesInHand;

        /// <summary>The tap is running; a second wash waits for it.</summary>
        public bool SinkBusy => Floor.House.SinkBusy;

        /// <summary>Seconds the sink still has to run (zero when idle).</summary>
        public double WashLeft => Floor.House.WashLeft;

        /// <summary>The glass leaves the counter for the hand; the stool is free this instant.
        /// The click on the empty glass (GDD 27 §4.2).</summary>
        public void CollectGlass(CounterMess mess)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            Floor.House.CollectGlass(mess);
        }

        /// <summary>The cloth over the mark. Refuses under a glass — collect first.</summary>
        public void Wipe(CounterMess mess)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            Floor.House.Wipe(mess);
        }

        /// <summary>Carry the hand's glasses to the sink and run the tap. Returns how long
        /// the water runs; refuses an empty hand and a sink that is already running.</summary>
        public double WashGlasses()
        {
            EnsurePhase(TycoonPhase.DayOpen);
            return Floor.House.WashGlasses();
        }
    }
}
