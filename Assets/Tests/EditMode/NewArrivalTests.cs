using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>The market's NEW! band (2026-09-08): a gate the rating crossed last night.</summary>
    public class NewArrivalTests
    {
        [Test]
        public void A_gate_crossed_by_last_nights_close_is_new_and_one_already_open_is_not()
        {
            var rating = new BarRating();
            double start = rating.Average;
            // a run of good nights lifts the standing past the next half-star
            double before = rating.Average;
            while (rating.Average < start + 0.6)
            {
                before = rating.Average;
                rating.CloseNight(1.0);
            }
            Assert.AreEqual(before, rating.PreviousStanding, 1e-9, "PreviousStanding is the standing before the last close");
            double crossed = (before + rating.Average) * 0.5;
            Assert.IsTrue(crossed > rating.PreviousStanding && crossed <= rating.Average);
            Assert.IsTrue(rating.Average > start, "the standing rose");

            var run = new TycoonRun(PourTestKit.NewShelf(), new System.Collections.Generic.List<RecipeDefinition>(),
                new RunRng("new-arrivals"), config: new TycoonConfig(200));
            // the same arithmetic the run applies, on the run's own rating
            var r = run.Rating;
            double b0 = r.Average;
            r.CloseNight(1.0);
            double gateInside = (r.PreviousStanding + r.Average) * 0.5;
            Assert.IsTrue(run.OpenedLastNight(gateInside), "a gate between the two standings opened last night");
            Assert.IsFalse(run.OpenedLastNight(b0 - 0.5), "a gate long open is not new");
            Assert.IsFalse(run.OpenedLastNight(r.Average + 1.0), "a gate still shut is not new");
            Assert.IsFalse(run.OpenedLastNight(double.NaN), "no gate, never new");
        }
    }
}
