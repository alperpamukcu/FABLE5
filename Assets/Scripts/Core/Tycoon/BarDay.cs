using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One night on the floor (GDD 23 §1, §6): customers arrive over time into a limited row
    /// of stools, wait with ticking patience, and leave served or fuming.
    ///
    /// **The night is open (v5 P12, C4).** There is no quota of customers: the shift runs on a
    /// clock, people keep coming until closing, and how many get through the door is decided by
    /// how fast the stools empty — which is to say by how fast the player works. That machinery
    /// was always here (a full row makes the next arrival wait at the door rather than queueing
    /// a backlog); the quota was what hid it.
    ///
    /// **The counter has a night of its own (GDD 27 §4, 2026-09-05).** What a served leaver
    /// puts down — their empty glass and a mark on the wood — is the floor's
    /// <see cref="House"/>, and it clears NOTHING by itself any more: the seven-second bussing
    /// clock of 2026-08-11 was a stand-in for verbs that did not exist (collect, wipe, carry
    /// to the sink, wash), and it is retired with them.
    ///
    /// Deliberately decoupled: BarDay owns seats and timing only. Who arrives — their
    /// order, patience roll, face and read — comes from the factory the caller passes to
    /// <see cref="Tick"/>, so the floor is testable without regulars or menus.
    /// </summary>
    public sealed class BarDay
    {
        public int Day { get; }
        public int Seats { get; }

        /// <summary>The counter's mess, the hand and the sink — the night's housekeeping
        /// (GDD 27 §4). One per night; nothing on it carries to tomorrow.</summary>
        public Housekeeping House { get; } = new Housekeeping();

        /// <summary>Everything a leaver left on the counter that still wants a hand. The
        /// room claims one per stool it drew the leaver on, exactly as it claimed the old
        /// glass; a mess with its glass still standing holds that stool.</summary>
        public IReadOnlyList<CounterMess> Messes => House.Messes;

        public int Arrived { get; private set; }

        /// <summary>Seconds of the shift gone by.</summary>
        public double Elapsed { get; private set; }

        /// <summary>How long the shift runs.</summary>
        public double NightSeconds { get; }

        /// <summary>0 at opening, 1 at closing time.</summary>
        public double NightFraction =>
            NightSeconds <= 0 ? 1.0 : Math.Min(1.0, Elapsed / NightSeconds);

        /// <summary>Past closing: the door is shut and nobody else comes in.</summary>
        public bool IsClosingTime => Elapsed >= NightSeconds;

        /// <summary>The wall clock the shift is shown on (GDD 23 §6), e.g. 21.5 = 21:30.
        /// Presentation only — the floor runs on <see cref="Elapsed"/>.</summary>
        public double ClockHour =>
            TycoonConfig.OpeningHour
            + (TycoonConfig.ClosingHour - TycoonConfig.OpeningHour) * NightFraction;

        private readonly List<CustomerVisit> _seated = new List<CustomerVisit>();
        public IReadOnlyList<CustomerVisit> Seated => _seated;

        private readonly List<CustomerVisit> _finished = new List<CustomerVisit>();
        /// <summary>Everyone who has left, served or stormed off — the satisfaction record.</summary>
        public IReadOnlyList<CustomerVisit> Finished => _finished;

        private readonly TycoonConfig _config;
        private readonly SeededRng _arrivals;
        private readonly SeededRng _mess;
        private readonly double _stars;
        private readonly double _gapScale;
        private double _untilNextArrival;

        /// <param name="gapScale">The installed room's CROWD (2026-09-23): × the gap between
        /// arrivals, read only while the bar is <see cref="KeepingUp"/>. 1 is the door as it was.</param>
        public BarDay(int day, int seats, TycoonConfig config, SeededRng arrivalStream,
            double stars = BarRating.NeutralStars, SeededRng messStream = null, double gapScale = 1.0)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            if (seats < 1) throw new ArgumentOutOfRangeException(nameof(seats));
            if (!(gapScale > 0)) throw new ArgumentOutOfRangeException(nameof(gapScale), "A door opens at a positive pace.");
            Day = day;
            Seats = seats;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _arrivals = arrivalStream ?? throw new ArgumentNullException(nameof(arrivalStream));
            // Its own stream, so adding it moves nothing that was already rolled; a floor
            // built without one leaves the single mark the counter left before today.
            _mess = messStream;
            _stars = stars;
            // Stored BEFORE the first gap is rolled: that gap is rolled here, in the constructor.
            _gapScale = gapScale;
            NightSeconds = config.NightSeconds;
            _untilNextArrival = NextGap();
        }

        /// <summary>
        /// How much mess this drinker made: none a quarter of the time, one most of the time,
        /// and now and then two or three. Weighted rather than uniform because the counter
        /// should usually look worked-in and only sometimes look neglected — an even roll
        /// makes every fourth customer a disaster, which reads as noise rather than as people.
        /// </summary>
        /// <remarks>
        /// SOME DRINKS ARE WORSE TO MAKE THAN OTHERS (2026-09-22, DrinkTraits): the three
        /// thresholds are the page's, off the DELIVERED drink — a Mojito is mint torn on the
        /// board and fruit crushed in the glass, a Ranch Water is a pour and a wipe of the hand.
        /// The same single draw is taken whatever the page is, so the "mess" stream's shape is
        /// exactly what it was and no seed moves because a character was re-tuned.
        /// </remarks>
        private int MarksLeftBy(CustomerVisit visit)
        {
            if (!_config.CounterSmudges) return 0;
            if (_mess == null) return 1;               // an older floor: what it always left
            double r = _mess.NextDouble();
            var t = DrinkTraits.Of(visit?.Served);
            return r < t.Mess0 ? 0 : r < t.Mess1 ? 1 : r < t.Mess2 ? 2 : 3;
        }

        /// <summary>The shift is over when the door has shut AND the last stool is empty:
        /// closing time stops new arrivals, it does not throw anyone out mid-drink.</summary>
        /// <summary>The floor is done with its people: closing time, nobody on a stool.
        /// The NIGHT may still be open — the run waits for the counter too (2026-09-08,
        /// TycoonRun.Tick), but that is the run's rule: this class is a floor, and the
        /// verbs that clear a counter are the run's. Kept as the floor's own word so the
        /// floor's tests keep meaning what they say.</summary>
        public bool FloorEmpty => IsClosingTime && _seated.Count == 0;

        public bool IsComplete => FloorEmpty;

        /// <summary>
        /// Sits somebody the night did not roll (GDD 26 §2) — the last customer, who comes in
        /// after the door is shut and answers to none of the arrival machinery: not the gap
        /// clock, not the seat count, not the crowd. Everything AFTER the door is the same as
        /// for anyone else. The floor ticks their patience and refuses to be complete while
        /// they are on the stool, which is why this needs no new end condition:
        /// <see cref="IsComplete"/> already says the shift is over when the last stool is
        /// empty, and now one of them is not.
        /// </summary>
        public void SeatGuest(CustomerVisit visit)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            if (_seated.Contains(visit))
                throw Said.With(new InvalidOperationException("They are already at the bar."),
                    Line.Of("rule.already_at_bar"));
            _seated.Add(visit);
            Arrived++;   // they walked in; the night's count would lie without them
        }

        /// <summary>People at the bar who still have not been served.</summary>
        public int Waiting
        {
            get
            {
                int n = 0;
                foreach (var visit in _seated) if (visit.State == VisitState.Waiting) n++;
                return n;
            }
        }

        /// <summary>
        /// THE BARTENDER IS KEEPING UP (2026-09-23): nobody is waiting but, at most, the one
        /// who just sat down or the one being served. The room's greed kinds — CROWD at the
        /// door, the tables' SECOND ROUND at the serve — act only then: measured unconditional,
        /// they sat or kept more people than a busy bar could serve and cost their buyer stars.
        /// A deterministic reading of the room; it draws nothing.
        /// </summary>
        public bool KeepingUp => Waiting <= 1;

        /// <summary>Whether the room looks too far behind to be worth sitting down in
        /// (v5 P12). Someone who walks in, counts the people still waiting on a drink and
        /// thinks better of it never becomes a storm-off — they were never a customer.</summary>
        public bool IsTooBusyToSit => Waiting >= _config.BalkAtWaiting;

        /// <summary>How many turned round at the door tonight. Not a failure in itself — a
        /// busy bar turns people away — but a bar that turns away everyone is losing money.</summary>
        public int Balked { get; private set; }

        /// <summary>
        /// Everyone who left tonight AND COUNTS. The guest of the house does not (GDD 26 §3):
        /// the story's customer pays nothing and rates nothing, so a trial cannot move the
        /// bar's standing by walking in — good or bad. Every ledger that reads the night reads
        /// this list, so "does this person count" is answered once, here, instead of three
        /// times in three files that can drift apart.
        /// </summary>
        public List<CustomerVisit> FinishedCounted()
        {
            var counted = new List<CustomerVisit>(_finished.Count);
            // A FACE RIGHTLY SHOWN THE DOOR NOW FILES ONE (2026-09-22, the author: "sahte kimlikle
            // kovulması gereken müşteriler ... hem + puan sağlamalı hem de + para getirmeli"). GDD
            // 28 D10 kept them out of this list entirely — no review, no seat in the mean — which
            // made the door worth exactly nothing to the stars. They are counted now, at
            // CustomerVisit.RightKickSatisfaction; what D10 was really protecting is that they are
            // neither SERVED nor WALKED on the slip, and that is done where the slip is written
            // (TycoonRun.ContinueToNextDay) off OffTheBooks, which they still carry.
            foreach (var visit in _finished)
                if (!visit.OnTheHouse) counted.Add(visit);
            return counted;
        }

        /// <summary>
        /// Mean of every finished visit's review — WEIGHTED, since 2026-09-22. A seat is one seat,
        /// except a customer who was refused a drink they were entitled to: a wrong kick is already
        /// at the floor of what one review can say, so the only way for the door to cost more
        /// standing than a slow drink is for it to weigh more of the room
        /// (<see cref="CustomerVisit.RatingWeight"/>).
        /// </summary>
        public double AverageSatisfaction
        {
            get
            {
                double total = 0, weight = 0;
                foreach (var visit in _finished)
                {
                    if (visit.OnTheHouse) continue;
                    double w = visit.RatingWeight;
                    total += visit.Satisfaction * w;
                    weight += w;
                }
                return weight <= 0 ? 0 : total / weight;
            }
        }

        /// <summary>Stools with nobody on them and no glass standing at them: what the door
        /// can seat this instant.</summary>
        /// <summary>
        /// Stools that can take somebody right now: the ones nobody is sitting on and no
        /// dirty glass is holding. A glass holds its stool from the moment it is left until
        /// the moment the sink hands it back — on the counter, in the hand, under the tap
        /// (GDD 27 §4.2, 2026-09-06). Collecting it used to free the stool on the spot,
        /// which made the wash a thing you did for comfort and nothing else; now the wash
        /// is the bottleneck, and a bar that lets the glasses pile up turns people away.
        /// </summary>
        public int FreeStools => Math.Max(0, Seats - _seated.Count - House.GlassesOut);

        /// <summary>
        /// Advances the floor: patience ticks, the settled and the stormed-off leave, and
        /// when a stool is free and the moment comes, the factory seats the next arrival.
        /// Returns whoever just sat down, for the presentation layer to walk in.
        /// </summary>
        public IReadOnlyList<CustomerVisit> Tick(double seconds, Func<CustomerVisit> arrivalFactory)
        {
            if (arrivalFactory == null) throw new ArgumentNullException(nameof(arrivalFactory));

            foreach (var visit in _seated) visit.Tick(seconds);
            _seated.RemoveAll(visit =>
            {
                // The still-waiting and the still-drinking keep their stools; only the
                // served-and-done and the stormed-off free up and land in the record.
                if (visit.State == VisitState.Waiting || visit.State == VisitState.Drinking)
                    return false;
                _finished.Add(visit);
                // WHAT THEY LEAVE BEHIND (GDD 27 §4.1). Somebody who was handed a drink
                // leaves the empty glass and a mark on the counter; the glass holds the
                // stool until it is collected, the mark stays until it is wiped, and both
                // cost the room's comfort past their grace. The signal is the SERVE, not the
                // state: a storm-off poured nothing, a declined order poured nothing (it
                // used to leave an invisible glass that held the stool seven seconds — the
                // bug GDD 27 C6 closes), and the guest of the house is outside the books
                // and outside the mess. EVERY served drink leaves one (audit 2026-08-11) —
                // the unmatched glass used to vanish, a bussing discount for the worst
                // pour — and it is the VESSEL that was actually handed over.
                if (visit.DrinkServed && !visit.OnTheHouse)
                {
                    // THE GLASS ALWAYS, THE MARKS BY THE DICE (2026-09-06). Some drinkers
                    // are tidy and some are not: a served customer leaves their empty and
                    // between none and three marks around it, rolled on the counter's own
                    // stream so the same seed still plays the same night.
                    House.LeaveMess(visit.ServedGlassId ?? visit.Served?.GlassId, smudge: false);
                    for (int m = 0; m < MarksLeftBy(visit); m++) House.LeaveMark();
                }
                return true;
            });

            // The counter's own clock: every mess past its grace costs a seat-second per
            // second, and the sink counts down. Ticked with the floor, never with the screen.
            House.Tick(seconds);

            // How much of this tick falls before closing. Taken BEFORE the clock advances, and
            // clamped: a single tick big enough to cover the whole shift must still let the
            // night's arrivals happen inside it. Adding the time first and then asking whether
            // the door was shut meant one 10,000-second step opened and closed the bar without
            // a soul walking in — invisible at a 60th of a second, plain in the sim.
            double open = Math.Max(0.0, Math.Min(seconds, NightSeconds - Elapsed));
            Elapsed += seconds;

            var newlySeated = new List<CustomerVisit>();
            if (open > 0)
            {
                _untilNextArrival -= open;
                while (_untilNextArrival <= 0 && FreeStools > 0 && !IsTooBusyToSit)
                {
                    var visit = arrivalFactory();
                    _seated.Add(visit);
                    newlySeated.Add(visit);
                    Arrived++;
                    _untilNextArrival += NextGap();
                }
                // A full row does not queue a backlog: the next arrival waits at the door, and
                // walks in the moment a stool frees. This is what makes speed pay -- it is also
                // why nobody storms off for being kept OUTSIDE, only for being kept waiting once
                // they are sitting down.
                if (_untilNextArrival <= 0)
                {
                    // Held at the door by a room that is too far behind rather than by a full
                    // one: that is somebody deciding against the place, and the gap restarts.
                    if (IsTooBusyToSit && FreeStools > 0)
                    {
                        Balked++;
                        _untilNextArrival = NextGap();
                    }
                    else _untilNextArrival = 0;
                }
            }
            return newlySeated;
        }

        // Still ONE draw per gap. The room's CROWD shortens it only while the bar keeps up; a
        // bare room's scale is exactly 1, and (a × 1.0) × b is a × b to the last bit.
        private double NextGap() =>
            _config.ArrivalGap(Day, _stars) * (KeepingUp ? _gapScale : 1.0) *
            (1.0 + (_arrivals.NextDouble() * 2.0 - 1.0) * TycoonConfig.ArrivalJitter);
    }
}
