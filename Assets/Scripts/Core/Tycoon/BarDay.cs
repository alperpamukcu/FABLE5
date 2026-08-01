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
    /// Deliberately decoupled: BarDay owns seats and timing only. Who arrives — their
    /// order, patience roll, face and read — comes from the factory the caller passes to
    /// <see cref="Tick"/>, so the floor is testable without regulars or menus.
    /// </summary>
    /// <summary>
    /// An empty glass left on a stool (D2, v5 P14). It blocks the stool until it is bussed:
    /// the player's click clears it now, and the bar's own slow clock clears it eventually —
    /// so ignoring the bussing costs seats-seconds, and clearing it is worth the click.
    /// </summary>
    public sealed class DirtyGlass
    {
        public double Left { get; private set; }
        public bool Cleared { get; private set; }

        /// <summary>Which glass line the drink was served in, so the empty on the counter
        /// is drawn as THE glass, not a stand-in (the author, 2026-08-02).</summary>
        public string GlasswareId { get; }

        internal DirtyGlass(double seconds, string glasswareId = null)
        { Left = seconds; GlasswareId = glasswareId; }

        internal void Tick(double seconds)
        {
            if (Cleared) return;
            Left -= seconds;
            if (Left <= 0) Cleared = true;
        }

        /// <summary>The player clears it by hand — the stool frees this instant.</summary>
        public void Bus() => Cleared = true;
    }

    public sealed class BarDay
    {
        public int Day { get; }
        public int Seats { get; }

        /// <summary>Seconds an unbussed glass blocks its stool before the bar gets to it.</summary>
        public const double BusSeconds = 7.0;

        private readonly List<DirtyGlass> _dirty = new List<DirtyGlass>();

        /// <summary>The empty glasses standing on stools right now.</summary>
        public IReadOnlyList<DirtyGlass> Dirty => _dirty;
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

        /// <summary>Mean of every finished visit's satisfaction, storm-offs counting as 0.</summary>
        public double AverageSatisfaction
        {
            get
            {
                if (_finished.Count == 0) return 0;
                double total = 0;
                foreach (var visit in _finished) total += visit.Satisfaction;
                return total / _finished.Count;
            }
        }

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
                // The bussing beat (D2, v5 P14): someone who drank leaves their empty glass
                // on the stool, and the stool is not sat on again until it is cleared — by
                // the player's click, or by the bar getting to it in its own time. A
                // storm-off leaves nothing: there was no glass.
                if (visit.Served != null)
                    _dirty.Add(new DirtyGlass(BusSeconds, visit.Served.GlassId));
                return true;
            });

            // The dirty glasses age out on the bar's own slow clock.
            for (int i = _dirty.Count - 1; i >= 0; i--)
            {
                _dirty[i].Tick(seconds);
                if (_dirty[i].Cleared) _dirty.RemoveAt(i);
            }

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
                while (_untilNextArrival <= 0 && _seated.Count + _dirty.Count < Seats && !IsTooBusyToSit)
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
                    if (IsTooBusyToSit && _seated.Count + _dirty.Count < Seats)
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
