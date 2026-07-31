using System;

namespace LastCall.Core
{
    public enum VisitState
    {
        /// <summary>Sitting at the bar. First they mull the menu (<see cref="CustomerVisit.HasOrdered"/>
        /// false), then the order is open and patience ticks.</summary>
        Waiting,
        /// <summary>Served and paid, now nursing the drink on the stool (GDD 23 §2, 2026-07-23).
        /// The seat stays taken until the savour timer runs out, then they get up to leave.</summary>
        Drinking,
        /// <summary>Finished the drink (or a served leftover) and gone; leaves on the next tick.</summary>
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

        private DrinkOrder _order;

        /// <summary>
        /// True once the player has looked at this customer's ID card. Until then the order is
        /// theirs, not yours — see <see cref="Order"/>.
        /// </summary>
        public bool IdInspected { get; private set; }

        /// <summary>Reads the ID card. There is no undo: what has been seen stays seen, and an
        /// extra order does not re-hide it — it is spoken across the bar by someone whose card
        /// you already read.</summary>
        public void InspectId() => IdInspected = true;

        /// <summary>
        /// What they asked for — IF you have read the card (v5 C3). The bubble naming the order
        /// made the ID card decorative: everything it told you was already floating over the
        /// seat, price included. Core refuses here rather than trusting the HUD to look away,
        /// the same bargain the emotion reads struck: serving blind stays legal (the judge
        /// compares against the truth internally), but *knowing* costs the inspection.
        /// </summary>
        public DrinkOrder Order => IdInspected
            ? _order
            : throw new InvalidOperationException(
                "The order is on the ID card — inspect it before reading it (C3).");

        /// <summary>The truth, for the judge and the run. Core-only by assembly.</summary>
        internal DrinkOrder OrderTruth => _order;

        /// <summary>
        /// True once the tab has gone into the till. Money changes hands on the way OUT
        /// (2026-07-31): a customer pays and rates when they finish the drink and get up, not
        /// the moment the glass lands — so the till cannot spoil a verdict the reaction has
        /// not delivered yet, and every round of an extra-order visit settles as one tab.
        /// </summary>
        public bool TabSettled { get; private set; }

        internal void SettleTab() => TabSettled = true;
        public double PatienceMax { get; }
        public double PatienceLeft { get; private set; }
        public VisitState State { get; private set; } = VisitState.Waiting;
        public int Paid { get; private set; }

        /// <summary>
        /// The drink half of <see cref="Paid"/>, without the tip. Kept apart so the night's
        /// receipt can itemise what was sold and still add up: a wrong drink is paid at the
        /// delivered drink's price, not the ordered one, so listing menu prices against the
        /// day's sales total would leave the bill off by however often the player misread
        /// somebody.
        /// </summary>
        public int PaidBase { get; private set; }

        /// <summary>What they were poured, once served — the receipt's line item. Null while
        /// waiting, and after a storm-off, because nothing was sold.</summary>
        public RecipeDefinition Served { get; private set; }

        public int ExtraOrdersTaken { get; private set; }

        /// <summary>Final satisfaction (0–1) once resolved; storm-offs stay at 0.</summary>
        public double Satisfaction { get; private set; }

        /// <summary>
        /// Seconds of "thinking" left before they place the order (GDD 23 §2, 2026-07-23). A
        /// freshly seated customer mulls the menu for a beat: while this is positive they have
        /// not ordered yet, nothing can be served to them, and their patience does not tick —
        /// being kept waiting only counts once they have actually asked for something.
        /// </summary>
        public double DecideLeft { get; private set; }

        /// <summary>True once they have made up their mind and the order is on the bar.</summary>
        public bool HasOrdered => DecideLeft <= 0;

        /// <summary>Seconds left of nursing a served drink before they get up to leave
        /// (2026-07-23). Only meaningful in <see cref="VisitState.Drinking"/>.</summary>
        public double SavorLeft { get; private set; }

        /// <summary>0 = just ordered, 1 = patience gone. Locked in by serving.</summary>
        public double WaitFraction =>
            PatienceMax <= 0 ? 1.0 : 1.0 - PatienceLeft / PatienceMax;

        public CustomerVisit(DrinkOrder order, double patienceSeconds,
            RegularState regular = null, CustomerRead read = null, double decideSeconds = 0)
        {
            _order = order ?? throw new ArgumentNullException(nameof(order));
            if (patienceSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(patienceSeconds));
            if (decideSeconds < 0) throw new ArgumentOutOfRangeException(nameof(decideSeconds));
            PatienceMax = patienceSeconds;
            PatienceLeft = patienceSeconds;
            DecideLeft = decideSeconds;
            Regular = regular;
            Read = read;
        }

        /// <summary>
        /// Advances the clock. A served customer nurses the drink down (then leaves); a waiting
        /// one first thinks the order over — only after that does patience tick, and zero
        /// patience is the storm-off.
        /// </summary>
        public void Tick(double seconds)
        {
            if (seconds <= 0) return;

            if (State == VisitState.Drinking)
            {
                SavorLeft -= seconds;
                if (SavorLeft > 0) return;
                SavorLeft = 0;
                State = VisitState.Served;   // finished the drink — up and out on the next tick
                return;
            }

            if (State != VisitState.Waiting) return;

            // Still making up their mind: think first, and only the leftover ticks patience.
            if (DecideLeft > 0)
            {
                DecideLeft -= seconds;
                if (DecideLeft >= 0) return;
                seconds = -DecideLeft;
                DecideLeft = 0;
            }

            PatienceLeft -= seconds;
            if (PatienceLeft > 0) return;

            PatienceLeft = 0;
            State = VisitState.StormedOff;
            Satisfaction = 0;
        }

        /// <summary>
        /// Settles a served drink. When the verdict earned an extra order and a fresh one
        /// is offered, the visit continues with refreshed patience; otherwise they take the
        /// drink and, given a <paramref name="savorSeconds"/>, nurse it on the stool before
        /// leaving. A zero savour keeps the old behaviour (gone on the next tick) for the sim
        /// and the direct-construction tests.
        /// </summary>
        public void Resolve(ServiceVerdict verdict, DrinkOrder nextOrder = null,
            double savorSeconds = 0, RecipeDefinition served = null)
        {
            if (verdict == null) throw new ArgumentNullException(nameof(verdict));
            if (State != VisitState.Waiting)
                throw new InvalidOperationException("This customer is no longer waiting.");

            Paid += verdict.Total;
            PaidBase += verdict.BasePaid;
            if (served != null) Served = served;
            Satisfaction = verdict.Satisfaction;

            if (verdict.OrdersAgain && nextOrder != null && ExtraOrdersTaken < MaxExtraOrders)
            {
                ExtraOrdersTaken++;
                _order = nextOrder;
                PatienceLeft = PatienceMax * ExtraOrderPatienceRefill;
                return;
            }

            if (savorSeconds > 0)
            {
                SavorLeft = savorSeconds;
                State = VisitState.Drinking;
            }
            else
            {
                State = VisitState.Served;
            }
        }
    }
}
