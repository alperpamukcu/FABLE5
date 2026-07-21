using System;

namespace LastCall.Core
{
    public enum VisitState
    {
        /// <summary>Sitting at the bar, order open, patience ticking.</summary>
        Waiting,
        /// <summary>Served and settled up; leaves on the next floor tick.</summary>
        Served,
        /// <summary>Patience ran out. No payment, satisfaction zero, stool frees up.</summary>
        StormedOff,
    }

    /// <summary>
    /// One customer's time on a stool (GDD 23 §1–§2): an order, a patience clock that only
    /// ticks while they wait, and the money they end up leaving. The emotion read rides
    /// along for tips and extra orders — the licence ID still matters, it just pays in
    /// dollars now instead of points.
    /// </summary>
    public sealed class CustomerVisit
    {
        /// <summary>Extra orders a perfect streak can add (GDD 23 §5).</summary>
        public const int MaxExtraOrders = 2;

        /// <summary>An extra order refills patience to this share of the original roll.</summary>
        public const double ExtraOrderPatienceRefill = 0.8;

        public RegularState Regular { get; }
        public CustomerRead Read { get; }
        public DrinkOrder Order { get; private set; }
        public double PatienceMax { get; }
        public double PatienceLeft { get; private set; }
        public VisitState State { get; private set; } = VisitState.Waiting;
        public int Paid { get; private set; }
        public int ExtraOrdersTaken { get; private set; }

        /// <summary>Final satisfaction (0–1) once resolved; storm-offs stay at 0.</summary>
        public double Satisfaction { get; private set; }

        /// <summary>0 = just sat down, 1 = patience gone. Locked in by serving.</summary>
        public double WaitFraction =>
            PatienceMax <= 0 ? 1.0 : 1.0 - PatienceLeft / PatienceMax;

        public CustomerVisit(DrinkOrder order, double patienceSeconds,
            RegularState regular = null, CustomerRead read = null)
        {
            Order = order ?? throw new ArgumentNullException(nameof(order));
            if (patienceSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(patienceSeconds));
            PatienceMax = patienceSeconds;
            PatienceLeft = patienceSeconds;
            Regular = regular;
            Read = read;
        }

        /// <summary>Advances the clock while they wait. Zero patience is the storm-off.</summary>
        public void Tick(double seconds)
        {
            if (State != VisitState.Waiting || seconds <= 0) return;
            PatienceLeft -= seconds;
            if (PatienceLeft > 0) return;

            PatienceLeft = 0;
            State = VisitState.StormedOff;
            Satisfaction = 0;
        }

        /// <summary>
        /// Settles a served drink. When the verdict earned an extra order and a fresh one
        /// is offered, the visit continues with refreshed patience; otherwise they pay up
        /// and are done.
        /// </summary>
        public void Resolve(ServiceVerdict verdict, DrinkOrder nextOrder = null)
        {
            if (verdict == null) throw new ArgumentNullException(nameof(verdict));
            if (State != VisitState.Waiting)
                throw new InvalidOperationException("This customer is no longer waiting.");

            Paid += verdict.Total;
            Satisfaction = verdict.Satisfaction;

            if (verdict.OrdersAgain && nextOrder != null && ExtraOrdersTaken < MaxExtraOrders)
            {
                ExtraOrdersTaken++;
                Order = nextOrder;
                PatienceLeft = PatienceMax * ExtraOrderPatienceRefill;
                return;
            }

            State = VisitState.Served;
        }
    }
}
