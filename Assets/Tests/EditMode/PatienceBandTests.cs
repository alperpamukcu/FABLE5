using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE CLOCK RUNS IN THREE BANDS (2026-09-04, the author: "sabır barını 3'e böleceğiz —
    /// kırmızı, sarı, yeşil; böylece hızlı servis etmenin de önemi artacak, bahşişi
    /// arttıracak"). The gauge over a drinker's head and the money in the till read the same
    /// thirds, so what these pin is the SHARED rule: where the bands fall, what speed is
    /// worth inside each, and that the difference reaches the tip.
    /// </summary>
    public class PatienceBandTests
    {
        private static RecipeDefinition Shakeable() =>
            new RecipeDefinition("sour", "Sour", 6, 30, 3, 25, 2,
                new[]
                {
                    new PatternRequirement(1, IngredientType.Spirit),
                    new PatternRequirement(1, IngredientType.Sour),
                },
                prep: PrepMethod.Shaken);

        private static GlassContents Full()
        {
            var glass = new GlassContents(10);
            glass.Add("gin", 5);
            glass.Add("lemon", 5);
            glass.AddPreparation(Preparations.Shaken);
            return glass;
        }

        /// <summary>A visit whose drink clock has already run <paramref name="spent"/> of it.</summary>
        private static CustomerVisit Waited(double spent, double patience = 60)
        {
            var visit = new CustomerVisit(new DrinkOrder(Shakeable(), 10), patience);
            visit.InspectId();                 // the drink clock starts when the order is taken
            visit.Tick(patience * spent);
            return visit;
        }

        [Test]
        public void TheBands_AreEvenThirds_OfTheWait()
        {
            Assert.AreEqual(ServiceBand.Green, ServiceJudge.BandOf(0.0));
            Assert.AreEqual(ServiceBand.Green, ServiceJudge.BandOf(0.32));
            Assert.AreEqual(ServiceBand.Amber, ServiceJudge.BandOf(0.34));
            Assert.AreEqual(ServiceBand.Amber, ServiceJudge.BandOf(0.66));
            Assert.AreEqual(ServiceBand.Red, ServiceJudge.BandOf(0.67));
            Assert.AreEqual(ServiceBand.Red, ServiceJudge.BandOf(1.0));
        }

        [Test]
        public void TheGaugeAndTheTill_ReadTheSameThirds()
        {
            // The visit answers with the band of whichever clock is live; the judge is asked
            // about the wait it will actually price. One rule, two callers.
            Assert.AreEqual(ServiceBand.Green, Waited(0.10).Band);
            Assert.AreEqual(ServiceBand.Amber, Waited(0.50).Band);
            Assert.AreEqual(ServiceBand.Red, Waited(0.80).Band);
        }

        [Test]
        public void SpeedScore_RunsFromOneToZero_AndBendsAtTheBandEdges()
        {
            Assert.AreEqual(1.0, ServiceJudge.SpeedScore(0.0), 1e-9, "handed over on the instant");
            Assert.AreEqual(ServiceJudge.GreenFloor, ServiceJudge.SpeedScore(ServiceJudge.GreenBand), 1e-9);
            Assert.AreEqual(ServiceJudge.AmberFloor, ServiceJudge.SpeedScore(ServiceJudge.AmberBand), 1e-9);
            Assert.AreEqual(0.0, ServiceJudge.SpeedScore(1.0), 1e-9, "it arrives as they get up");

            // Monotone, and never a step: no single tick of the clock may cost a whole band.
            double last = 1.0;
            for (int i = 0; i <= 200; i++)
            {
                double s = ServiceJudge.SpeedScore(i / 200.0);
                Assert.LessOrEqual(s, last + 1e-12, "the clock never pays more for waiting");
                Assert.LessOrEqual(last - s, 0.05, "and never loses a chunk in one step");
                last = s;
            }
        }

        [Test]
        public void TheAmberThird_IsWhereTheMoneyDrains()
        {
            double green = ServiceJudge.SpeedScore(0.0) - ServiceJudge.SpeedScore(ServiceJudge.GreenBand);
            double amber = ServiceJudge.SpeedScore(ServiceJudge.GreenBand) - ServiceJudge.SpeedScore(ServiceJudge.AmberBand);
            Assert.Greater(amber, green, "being quick is barely punished; being late is");
        }

        [Test]
        public void ServedGreen_TipsMoreThanAmber_WhichTipsMoreThanRed()
        {
            int Tip(double spent) =>
                ServiceJudge.Judge(Waited(spent), OrderMatch.Exact, Full()).Tip;

            int fast = Tip(0.05), middling = Tip(0.5), late = Tip(0.95);
            Assert.Greater(fast, middling, "the same drink, sooner, is worth more");
            Assert.Greater(middling, late, "and later is worth less again");
            Assert.Greater(Tip(1.0), 0, "a late drink is still a drink, and somebody made it");
            Assert.Less(Tip(1.0), fast / 2, "but the clock has taken most of the thanks");
        }

        [Test]
        public void TheCraftEarnsTheTip_AndTheClockDecidesHowMuchOfItYouKeep()
        {
            Assert.AreEqual(1.0, ServiceJudge.SpecWeight + ServiceJudge.AccuracyWeight
                + ServiceJudge.FillWeight, 1e-9,
                "a flawless serve on the instant tips exactly its base price");

            // The same perfect glass, at each band edge: the clock is a multiplier, so what
            // it leaves is the band's own floor. This is the author's "hızlı servis etmenin
            // de önemi artacak" in dollars.
            int Tip(double spent) => ServiceJudge.Judge(Waited(spent), OrderMatch.Exact, Full()).Tip;
            int Clock(double speed) => Round(10 * ServiceJudge.TipCeiling
                * (ServiceJudge.ClockFloor + (1 - ServiceJudge.ClockFloor) * speed));

            Assert.AreEqual(Clock(1.0), Tip(0.0), "handed straight over: the whole ceiling");
            Assert.AreEqual(Clock(ServiceJudge.GreenFloor), Tip(ServiceJudge.GreenBand), 1,
                "at the green line, most of it survives");
            Assert.AreEqual(Clock(ServiceJudge.AmberFloor), Tip(ServiceJudge.AmberBand), 1,
                "at the amber line, about half");
            Assert.AreEqual(Clock(0.0), Tip(1.0), 1,
                "and out of patience, only the part that was never on the clock");
            Assert.Greater(Tip(0.0), 10, "a fast serve tips MORE than it used to (the author's ask)");
        }

        private static int Round(double v) => (int)System.Math.Round(v, System.MidpointRounding.AwayFromZero);
    }
}
