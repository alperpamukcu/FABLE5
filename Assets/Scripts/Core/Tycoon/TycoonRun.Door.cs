using System;
using System.Linq;

namespace LastCall.Core
{
    /// <summary>
    /// THE DOOR (GDD 28, PLAN_house_and_law H2b, 2026-09-05). The card's second job: some of
    /// the people at the bar should not be served, and the card is how you know. This half of
    /// the run holds the night's door counters and its one verb, the kick; the papers are
    /// rolled in <c>NextArrival</c>, the fine is set in <c>ServeTo</c> and taken in
    /// <c>SettleDepartures</c>, the thanks is paid in the close block — all in TycoonRun.cs,
    /// beside the lines they belong to.
    /// </summary>
    public sealed partial class TycoonRun
    {
        /// <summary>What the law took tonight from drinks served to minors (GDD 28 §5).</summary>
        public int DayFines { get; private set; }

        /// <summary>The state's thanks for tonight's right kicks, paid at close (§6).</summary>
        public int DayBonus { get; private set; }

        /// <summary>Faces rightly shown the door tonight — minors and borrowed cards.</summary>
        public int RightKicks { get; private set; }

        /// <summary>Adults wrongly shown the door tonight — walk-outs at zero.</summary>
        public int WrongKicks { get; private set; }

        /// <summary>Minors and borrowed cards that were served a drink tonight.</summary>
        public int MinorsServed { get; private set; }

        /// <summary>People who sat down tonight whose card should have shown them the door,
        /// whichever way it went. The sim's denominator.</summary>
        public int MinorsMet { get; private set; }

        /// <summary>
        /// Shows a customer the door (GDD 28 §4). Five guards, in this order: the day is open;
        /// the guest of the house is never yours to kick (module 26 owns them); they are seated
        /// and waiting; the card has been READ — hidden information, you decide on what you saw;
        /// and nothing has been served to them — the card was your moment.
        ///
        /// Right (the truth behind the card is a minor or a forgery): the visit is off the
        /// books, the person is barred from coming back, the state's thanks is owed at close.
        /// Wrong (an honest adult): a walk-out at a zero review, on the books, and the regular
        /// remembers. No fine either way — the author priced only the other mistake.
        /// </summary>
        public void Kick(CustomerVisit visit)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            if (ReferenceEquals(visit, LastCustomer))
                throw new InvalidOperationException("The guest of the house is not yours to show the door.");
            if (visit.State != VisitState.Waiting || !Floor.Seated.Contains(visit))
                throw new InvalidOperationException("They are not waiting at the bar.");
            if (!visit.IdInspected)
                throw new InvalidOperationException("Read the card first.");
            if (visit.Paid > 0)
                throw new InvalidOperationException(
                    "You cannot show the door to someone you have already served; the card was your moment.");

            var papers = visit.Regular?.Papers;
            bool rightly = papers != null && papers.ShouldBeKicked;
            visit.Kick(offTheBooks: rightly);
            if (rightly)
            {
                visit.Regular.Bar();
                RightKicks++;
            }
            else
            {
                WrongKicks++;
                visit.Regular?.RecordVisit(0);   // refused a drink they were entitled to
            }
        }
    }
}
