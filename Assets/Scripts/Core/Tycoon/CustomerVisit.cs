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

        /// <summary>What TAKING THE ORDER gives back, as a share of the roll: exactly one of
        /// the three boxes the gauge is cut into (the author, 2026-09-04: "sipariş almak barı
        /// 0lamaz +1 kutu daha ekler"). One clock runs the whole visit and going over to ask
        /// is a move inside it, not a reset of it — see <see cref="InspectId"/>.</summary>
        public const double OrderTakenPatienceBonus = 1.0 / 3.0;

        public RegularState Regular { get; }

        private DrinkOrder _order;

        /// <summary>
        /// True once the player has looked at this customer's ID card. Until then the order is
        /// theirs, not yours — see <see cref="Order"/>.
        /// </summary>
        public bool IdInspected { get; private set; }

        /// <summary>Reads the ID card — which IS taking the order. There is no undo: what has
        /// been seen stays seen, and an extra order does not re-hide it, being spoken across
        /// the bar by someone whose card you already read.
        ///
        /// Taking the order PAYS ONE BOX BACK (the author, 2026-09-04: "sipariş almak barı
        /// 0lamaz +1 kutu daha ekler"). It used to end one clock and start another from full,
        /// which meant a bar that got to a stool quickly bought itself the entire build for
        /// free: the gauge over the head refilled to the brim and the tip's speed term began
        /// counting from zero however long they had already sat there. One clock runs the
        /// whole visit now — sitting down starts it, and being ASKED is worth a third of it,
        /// added to what is left rather than replacing it. It cannot push the gauge past
        /// full, so the reward is real exactly where it should be (a stool you got to late)
        /// and invisible where it should be (one you got to at once).</summary>
        public void InspectId()
        {
            if (IdInspected) return;
            IdInspected = true;
            // An extra round already set its own clock (the refill in Resolve). Serving blind
            // and reading the card afterwards must not top that up as well.
            if (_orderTaken) return;
            _orderTaken = true;
            PatienceLeft = Math.Min(PatienceMax,
                PatienceLeft + PatienceMax * OrderTakenPatienceBonus);
        }

        /// <summary>True once the order is on the bar — set by reading the card, and by an
        /// extra round, which is asked for across the bar and needs no reading.</summary>
        private bool _orderTaken;

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

        /// <summary>Bowls taken this visit (v5 P16). The money rides the same tab the drinks
        /// do — a snack is a line on the bill, not its own transaction.</summary>
        public int SnacksTaken { get; private set; }

        internal void AddSnack(int price)
        {
            SnacksTaken++;
            Paid += price;
            PaidBase += price;   // no tip on a bowl of nuts
        }
        /// <summary>The whole wait, one clock: it starts the moment they have made up their
        /// mind and runs until the drink lands. Being kept waiting to be ASKED and waiting on
        /// the drink are the same bar — taking the order pays a box back into it
        /// (<see cref="OrderTakenPatienceBonus"/>) and never resets it.</summary>
        public double PatienceMax { get; }
        public double PatienceLeft { get; private set; }

        /// <summary>True while they are sitting with an order nobody has come to take. The
        /// clock is the same one either way; this only says which sentence the bubble
        /// should be showing.</summary>
        public bool AwaitingOrderTaking => State == VisitState.Waiting && HasOrdered && !IdInspected;

        /// <summary>Which third of the clock they are in — the SAME thirds the tip is paid on
        /// (<see cref="ServiceJudge.BandOf"/>), so the gauge over their head and the money in
        /// the till cannot tell two different stories.</summary>
        public ServiceBand Band => ServiceJudge.BandOf(1.0 - PatienceFraction);

        /// <summary>The clock as a 0–1 fullness — the three boxes the gauge draws.</summary>
        public double PatienceFraction =>
            PatienceMax <= 0 ? 1.0 : PatienceLeft / PatienceMax;

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

        /// <summary>The VESSEL the drink actually went out in (audit 2026-08-11) — what the
        /// dirty glass on the stool is, which the recipe's nominal glass id cannot say.</summary>
        public string ServedGlassId { get; internal set; }

        /// <summary>A drink actually crossed the bar to them — set by the serve and by
        /// nothing else (GDD 27 §4.1). The floor's signal for what they leave behind: a
        /// declined order, a storm-off, the guest of the house and (module 28) the kicked
        /// leave nothing, because nothing was poured or nothing counts.</summary>
        public bool DrinkServed { get; internal set; }

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

        /// <summary>0 = the bar is full, 1 = patience gone. Locked in by serving, and it is
        /// the gauge's own reading inverted, so what the tip pays by is exactly what was
        /// drawn over the head — including the box that taking the order paid back.</summary>
        public double WaitFraction =>
            PatienceMax <= 0 ? 1.0 : 1.0 - PatienceLeft / PatienceMax;

        /// <summary>
        /// A guest of the house (GDD 26 §3): the story's customer, who is NOT a customer in
        /// any of the ways the books care about. They pay nothing, they file no rating, they
        /// are not on the night's slip, and nobody asks them for their licence — they said
        /// who they were on the way in. Everything else about them is an ordinary visit,
        /// which is what lets the same stool, the same serve verb and the same judge do the
        /// work; only the LEDGERS look away, and each of them looks away in one place.
        /// </summary>
        public bool OnTheHouse { get; }

        /// <summary>
        /// A clock that has not started. A guest of the house arrives into a conversation
        /// (GDD 26 §3.1) and nothing should be ticking while they are talking — the trial's
        /// time begins when the talking ends, which is what <see cref="ReleaseClock"/> says.
        /// </summary>
        public bool ClockHeld { get; private set; }

        /// <summary>The conversation is over. From here the clock runs like anyone else's.</summary>
        public void ReleaseClock() => ClockHeld = false;

        public CustomerVisit(DrinkOrder order, double patienceSeconds,
            RegularState regular = null, double decideSeconds = 0,
            bool onTheHouse = false)
        {
            _order = order ?? throw new ArgumentNullException(nameof(order));
            if (patienceSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(patienceSeconds));
            if (decideSeconds < 0) throw new ArgumentOutOfRangeException(nameof(decideSeconds));
            OnTheHouse = onTheHouse;
            ClockHeld = onTheHouse;   // they are talking; the trial starts it
            PatienceMax = patienceSeconds;
            PatienceLeft = patienceSeconds;
            DecideLeft = decideSeconds;
            Regular = regular;
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

            // Nothing runs while they are still talking (GDD 26 §3.1).
            if (ClockHeld) return;

            // Still making up their mind: think first, and only the leftover ticks patience.
            if (DecideLeft > 0)
            {
                DecideLeft -= seconds;
                if (DecideLeft >= 0) return;
                seconds = -DecideLeft;
                DecideLeft = 0;
            }

            // One clock, whether they are waiting to be asked or waiting on the drink. A
            // customer nobody ever comes to storms off on the same bar as one whose drink
            // never arrived — being ignored counts, even with nothing poured.
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
                throw new InvalidOperationException("They are not waiting any more.");

            Paid += verdict.Total;
            PaidBase += verdict.BasePaid;
            if (served != null) Served = served;
            Satisfaction = verdict.Satisfaction;

            if (verdict.OrdersAgain && nextOrder != null && ExtraOrdersTaken < MaxExtraOrders)
            {
                ExtraOrdersTaken++;
                _order = nextOrder;
                // A round they asked for across the bar: nobody has to come and take it, so
                // reading the card later must not pay the asking box a second time.
                _orderTaken = true;
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

        /// <summary>
        /// The next thing they ask for, WITHOUT touching the clock (GDD 26 §3.2). A trial is
        /// several drinks and one clock — which is exactly what the extra-round path is not:
        /// that one refreshes patience by design, because it is a reward. This is a demand.
        /// </summary>
        public void AskFor(DrinkOrder order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (State != VisitState.Waiting)
                throw new InvalidOperationException("They are not waiting any more.");
            _order = order;
        }

        /// <summary>
        /// They are done and get up — no drink to nurse, no tab to settle. The story's guest
        /// leaves this way whichever way the trial went (GDD 26 §5); the satisfaction is for
        /// whoever draws the reaction, and it reaches no ledger, because a guest of the house
        /// is not on the books.
        ///
        /// THEY TAKE A MOMENT FIRST when given one. The trial's last words are said after the
        /// last drink lands, and a guest who vanished on the same tick took the whole ending
        /// with them: the stool emptied, the night completed, and the day-end slip came up
        /// over a line nobody had read (2026-08-13, seen in play). The seconds are the
        /// caller's — Core does not know how long a sentence takes to read.
        /// </summary>
        public void GetUp(double satisfaction = 0, double lingerSeconds = 0)
        {
            if (State != VisitState.Waiting)
                throw new InvalidOperationException("They are not waiting any more.");
            Satisfaction = Math.Max(0.0, Math.Min(1.0, satisfaction));
            if (lingerSeconds > 0)
            {
                SavorLeft = lingerSeconds;
                State = VisitState.Drinking;
            }
            else State = VisitState.Served;
        }
    }
}
