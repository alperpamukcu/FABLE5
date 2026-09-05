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
        private readonly double _stars;
        private double _untilNextArrival;

        public BarDay(int day, int seats, TycoonConfig config, SeededRng arrivalStream,
            double stars = BarRating.NeutralStars)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            if (seats < 1) throw new ArgumentOutOfRangeException(nameof(seats));
            Day = day;
            Seats = seats;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _arrivals = arrivalStream ?? throw new ArgumentNullException(nameof(arrivalStream));
            _stars = stars;
            NightSeconds = config.NightSeconds;
            _untilNextArrival = NextGap();
        }

        /// <summary>The shift is over when the door has shut AND the last stool is empty:
        /// closing time stops new arrivals, it does not throw anyone out mid-drink.</summary>
        public bool IsComplete => IsClosingTime && _seated.Count == 0;

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
                throw new InvalidOperationException("They are already at the bar.");
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
            foreach (var visit in _finished) if (!visit.OnTheHouse) counted.Add(visit);
            return counted;
        }

        /// <summary>Mean of every finished visit's satisfaction, storm-offs counting as 0.</summary>
        public double AverageSatisfaction
        {
            get
            {
                double total = 0;
                int counted = 0;
                foreach (var visit in _finished)
                {
                    if (visit.OnTheHouse) continue;
                    total += visit.Satisfaction;
                    counted++;
                }
                return counted == 0 ? 0 : total / counted;
            }
        }

        /// <summary>Stools with nobody on them and no glass standing at them: what the door
        /// can seat this instant.</summary>
        public int FreeStools => Math.Max(0, Seats - _seated.Count - House.GlassesOnCounter);

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
                    House.LeaveMess(visit.ServedGlassId ?? visit.Served?.GlassId,
                        smudge: _config.CounterSmudges);
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

        private double NextGap() =>
            _config.ArrivalGap(Day, _stars) *
            (1.0 + (_arrivals.NextDouble() * 2.0 - 1.0) * TycoonConfig.ArrivalJitter);
    }
}
