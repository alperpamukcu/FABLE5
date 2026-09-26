using System;
using System.Collections.Generic;

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

        // ── what the room DOES: the installed pieces' buffs (2026-09-23) ─────────────────────
        // The author: "Hangi geliştirme takılıysa o buff aktif olacak, konfor gibi değil." Comfort
        // reads the rung a ladder CLIMBED to (above); a buff reads the rung it WEARS — the one the
        // player chose to show, else the tallest owned — and a single piece while it is owned. The
        // tools follow the same rule (Option A, TycoonRun.WorkSpeed), so there is one answer to
        // "which rung counts" for everything but comfort.

        /// <summary>
        /// A fitting's buff is live while this piece is the one INSTALLED: the rung its slot
        /// wears (the one the player chose, else the tallest owned), or, for a single piece, while
        /// it is owned. Comfort does not read this; it reads the rung the ladder CLIMBED to
        /// (<see cref="FixtureComfort"/>).
        /// </summary>
        public bool IsActive(FixtureDefinition f) =>
            f != null && _fixtures.Contains(f.Id) && (f.Level == 0 || WornRung(f.Slot)?.Id == f.Id);

        /// <summary>Every installed piece, in catalogue order.</summary>
        public IEnumerable<FixtureDefinition> ActiveFittings
        {
            get
            {
                foreach (var f in _fixtureCatalogue)
                    if (IsActive(f)) yield return f;
            }
        }

        /// <summary>
        /// The installed room's buffs. Cached on the room's revision and the A/B gate: the top
        /// bar reads <see cref="ComfortNow"/> every frame, and a fresh aggregate per frame would
        /// allocate per frame. The room cannot change while the doors are open
        /// (<see cref="WearFixture"/>), so a night reads one room from its first arrival to its last.
        /// </summary>
        public HouseBuffs Buffs
        {
            get
            {
                if (_buffs == null || _buffsRevision != _roomRevision || _buffsEnabled != HouseBuffs.Enabled)
                {
                    _buffs = HouseBuffs.From(ActiveFittings);
                    _buffsRevision = _roomRevision;
                    _buffsEnabled = HouseBuffs.Enabled;
                }
                return _buffs;
            }
        }

        private HouseBuffs _buffs;
        private int _buffsRevision = -1;
        private bool _buffsEnabled;
        private int _roomRevision;

        /// <summary>Called at every site that changes what the bar owns or what a slot wears.</summary>
        private void RoomChanged() => _roomRevision++;

        /// <summary>One piece's effects, lead first, with the tools' figures derived against the
        /// slot's own foot (<see cref="FittingBuffs.EffectsOf"/>). What the market tile, the upgrade
        /// card and the hover print.</summary>
        public IReadOnlyList<FittingEffect> FittingEffects(FixtureDefinition f) =>
            FittingBuffs.EffectsOf(f, _fixtureCatalogue);

        /// <summary>The floor's house is told what the installed room says: the basin's wash and
        /// the counter's grace. One body for every site that builds a floor or changes the room.</summary>
        private void PushHouse(BarDay floor, HouseBuffs b)
        {
            floor.House.SinkSeconds = SinkSeconds;
            floor.House.Grace = Housekeeping.DirtGrace * b.GraceScale;
        }

        /// <summary>
        /// ONE FLOOR FACTORY (2026-09-23). Five sites built a <see cref="BarDay"/> and then pushed
        /// the basin into it; they all come here now, so the door's CROWD and the counter's grace
        /// reach every night the same way. The streams are fetched in the order they always were
        /// ("arrivals", then "mess"), and a bare room's gap scale is exactly 1.
        /// </summary>
        private BarDay NewFloor(double stars)
        {
            var b = Buffs;
            var floor = new BarDay(Day, Seats, _config, _rng.GetStream("arrivals"), stars,
                                   _rng.GetStream("mess"), b.ArrivalGapScale);
            PushHouse(floor, b);
            return floor;
        }

        /// <summary>What the glass steps bought are worth to the room: every step each line has
        /// climbed, at the line's own <see cref="GlasswareDefinition.TierComfort"/> (2026-09-26).
        /// It was half of a table of step caps Core kept private (0.20…0.05 a line), which the
        /// glass card could not print and which made five lines worth 1.50 of a house that has to
        /// sum to five; the five shipped lines are worth 0.50 together now, a fiftieth a step.</summary>
        public double GlassComfort
        {
            get
            {
                double sum = 0;
                foreach (var g in _glassware)
                {
                    int steps = GlassTier(g.Id) - 1;
                    for (int s = 0; s < steps && s < g.TierComfort.Count; s++)
                        sum += g.TierComfort[s];
                }
                return sum;
            }
        }

        /// <summary>What the room is worth with nobody in it (GDD 27 §3): the free base, the
        /// fittings, the glass steps, the extra stools and the bar-top steps (2026-09-26 — the
        /// counter joined the room when the house was cut to sum to exactly five). Changes at
        /// the market, never during a night. A fresh bar is worth NOTHING (2026-09-06): the room
        /// is what has been put into it, and the walls are the first and biggest thing to put in.</summary>
        public double ComfortBase =>
            VenueComfort.Base(FixtureComfort, GlassComfort, Math.Max(0, Seats - _config.StartingSeats),
                Math.Max(0, CounterTier - 1));

        /// <summary>
        /// WHAT A PIECE ADDS (2026-09-26, the author: "Şu an sanki tüm gelişmeler +5 oluyormuş gibi
        /// gözüküyor"): a rung's comfort less the comfort of the rung under it on the same ladder — 0
        /// under the first rung — or a single piece's own comfort. Rungs carry ABSOLUTE values
        /// (<see cref="FixtureComfort"/> reads the climbed rung), and the market printed them as if each
        /// were added: the back wall's cards read +1.25, +2.25, +2.75, +3.00, +3.25 for a wall worth
        /// 3.25. The market's pips and card lines print this, and the sim values a piece by it.
        /// </summary>
        public double ComfortGain(FixtureDefinition f) => ComfortGainIn(f, _fixtureCatalogue);

        /// <summary><see cref="ComfortGain"/> against any catalogue — the economy projection's furnished
        /// walk values a piece by the same increment the market prints.</summary>
        public static double ComfortGainIn(FixtureDefinition f, IReadOnlyList<FixtureDefinition> catalogue)
        {
            if (f == null) return 0;
            if (f.Level <= 1 || catalogue == null) return f.Comfort;
            foreach (var below in catalogue)
                if (below.Slot == f.Slot && below.Level == f.Level - 1)
                    return f.Comfort - below.Comfort;
            return f.Comfort;
        }

        /// <summary>The comfort of a ladder's top rung: what the whole ladder is worth once it is climbed
        /// (the ladder head prints the climbed rung's comfort against it). A single piece is its own top.</summary>
        public double LadderComfort(string slot)
        {
            double top = 0; int level = -1;
            foreach (var f in _fixtureCatalogue)
                if (f.Slot == slot && f.Level > level) { level = f.Level; top = f.Comfort; }
            return top;
        }

        /// <summary>The room as it stands THIS SECOND: the base less the messes past their
        /// grace (GDD 27 §2.2), cushioned by the installed COMFORT buff (2026-09-23). The shift's
        /// gauge, and what the sim reads per tick.</summary>
        public double ComfortNow =>
            VenueComfort.Now(ComfortBase, Floor.House.DirtySpots, Seats, Buffs.ComfortScale);

        /// <summary>Tonight's filed comfort: the base less what the mess cost over the whole
        /// night so far, time-weighted per seat against the night the floor actually ran, and
        /// then cushioned by the installed COMFORT buff — after the mess, never under it
        /// (2026-09-23). Exactly the number <see cref="ContinueToNextDay"/> files as ComfortStars,
        /// asked before it is filed.</summary>
        public double ComfortTonight =>
            VenueComfort.Tonight(ComfortBase, Floor.House.Cleanliness(Seats, Floor.Elapsed), Buffs.ComfortScale);

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

        /// <summary>
        /// The basin the bar has fitted, in seconds — read off the FIXTURE, like the drain's
        /// waiver beside it (2026-09-06). Pushed into the counter whenever the room changes,
        /// so Core never has to ask the shop mid-night.
        ///
        /// THE INSTALLED BASIN (2026-09-23). This used to return the first OWNED drain in
        /// catalogue order, and the old steel sink the room opens with is listed first — so the
        /// Steel Sink's 3.0 s and the Brass Sink's 2.5 s never applied, while the upgrade card
        /// promised them. It reads the basin the slot wears now, like every other tool.
        /// </summary>
        public double SinkSeconds
        {
            get
            {
                foreach (var f in _fixtureCatalogue)
                    if (f.IsDrain && IsActive(f))
                        return f.WashSeconds > 0 ? f.WashSeconds : Housekeeping.WashSeconds;
                return Housekeeping.WashSeconds;
            }
        }

        /// <summary>The tap is running; a second wash waits for it.</summary>
        public bool SinkBusy => Floor.House.SinkBusy;

        /// <summary>Glasses under the tap this second — the room prints them beside what
        /// is in the hand, because both are stools the bar cannot lay (GDD 27 §4.2).</summary>
        public int GlassesWashing => Floor.House.GlassesWashing;

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
