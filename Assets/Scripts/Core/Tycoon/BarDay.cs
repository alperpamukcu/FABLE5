using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One day on the floor (GDD 23 §1, §6): customers arrive over time into a limited
    /// row of stools, wait with ticking patience, and leave served or fuming. The day is
    /// over when the last planned customer has come and gone.
    ///
    /// Deliberately decoupled: BarDay owns seats and timing only. Who arrives — their
    /// order, patience roll, face and read — comes from the factory the caller passes to
    /// <see cref="Tick"/>, so the floor is testable without regulars or menus.
    /// </summary>
    public sealed class BarDay
    {
        public int Day { get; }
        public int Seats { get; }
        public int CustomersPlanned { get; }
        public int Arrived { get; private set; }

        private readonly List<CustomerVisit> _seated = new List<CustomerVisit>();
        public IReadOnlyList<CustomerVisit> Seated => _seated;

        private readonly List<CustomerVisit> _finished = new List<CustomerVisit>();
        /// <summary>Everyone who has left, served or stormed off — the satisfaction record.</summary>
        public IReadOnlyList<CustomerVisit> Finished => _finished;

        private readonly TycoonConfig _config;
        private readonly SeededRng _arrivals;
        private double _untilNextArrival;

        public BarDay(int day, int seats, TycoonConfig config, SeededRng arrivalStream)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            if (seats < 1) throw new ArgumentOutOfRangeException(nameof(seats));
            Day = day;
            Seats = seats;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _arrivals = arrivalStream ?? throw new ArgumentNullException(nameof(arrivalStream));
            CustomersPlanned = config.CustomersOnDay(day);
            _untilNextArrival = NextGap();
        }

        public bool IsComplete => Arrived >= CustomersPlanned && _seated.Count == 0;

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
                if (visit.State == VisitState.Waiting) return false;
                _finished.Add(visit);
                return true;
            });

            var newlySeated = new List<CustomerVisit>();
            if (Arrived < CustomersPlanned)
            {
                _untilNextArrival -= seconds;
                while (_untilNextArrival <= 0 && Arrived < CustomersPlanned && _seated.Count < Seats)
                {
                    var visit = arrivalFactory();
                    _seated.Add(visit);
                    newlySeated.Add(visit);
                    Arrived++;
                    _untilNextArrival += NextGap();
                }
                // A full row does not queue a backlog: the next arrival waits at the door.
                if (_untilNextArrival <= 0) _untilNextArrival = 0;
            }
            return newlySeated;
        }

        private double NextGap() =>
            _config.ArrivalGap(Day) *
            (1.0 + (_arrivals.NextDouble() * 2.0 - 1.0) * TycoonConfig.ArrivalJitter);
    }
}
