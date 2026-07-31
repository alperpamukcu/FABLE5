using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The hidden information has to stay hidden. Everything the player sees is derived from
    /// <see cref="CustomerRead"/>; the moment a preview or a label reaches past it into
    /// <see cref="RegularState.Stats"/>, blind reads stop being reads. These tests pin the
    /// boundary at its source — the readings themselves and the factory that builds them.
    /// (The Chat/preview/resonance probes retired with the card loop; the read integrity
    /// they guarded now lives here and in <c>RegularsAndReadTests</c>.)
    /// </summary>
    public class ReadIntegrityTests
    {
        [Test]
        public void AnUnknownReading_ExposesNoBound()
        {
            // StatReading.Unknown must not carry the truth in its Low/High: it is rendered,
            // logged and previewed, and anything stored on it is one ToString() from leaking.
            var unknown = StatReading.Unknown;

            Assert.AreEqual(EmotionStats.Min, unknown.Low);
            Assert.AreEqual(EmotionStats.Max, unknown.High);
            Assert.AreEqual("??", unknown.ToString());
        }

        [Test]
        public void ARangeReading_NeverPrintsTheExactValue()
        {
            var reading = StatReading.Range(50, 8);

            Assert.AreEqual("42-58", reading.ToString());
            Assert.AreNotEqual(reading.Low, reading.High, "a zero-width range would be an Exact in disguise");
        }

        [Test]
        public void AnUninspectedVisit_RefusesToNameItsOrder()
        {
            // v5 C3: the order lives on the ID card. Until the card is read, Core does not hand
            // it over at all — a bubble, a preview or a debug label that names the drink early
            // is not a style bug, it is this exception not being thrown.
            var visit = new CustomerVisit(Spritz(6), patienceSeconds: 60);

            Assert.IsFalse(visit.IdInspected);
            Assert.Throws<System.InvalidOperationException>(() => _ = visit.Order);
        }

        [Test]
        public void ReadingTheId_OpensTheOrder_AndAnExtraOrderStaysOpen()
        {
            var visit = new CustomerVisit(Spritz(6), patienceSeconds: 60);
            visit.InspectId();
            Assert.AreEqual(6, visit.Order.Price, "read the card, know the order");

            // An extra order is spoken across the bar by someone whose card was already read:
            // it does not re-hide.
            var verdict = ServiceJudge.Judge(visit, OrderMatch.Exact, ExactGlass());
            visit.Resolve(verdict, Spritz(8));
            Assert.IsTrue(visit.IdInspected);
            Assert.AreEqual(8, visit.Order.Price);
        }

        private static DrinkOrder Spritz(int price) => new DrinkOrder(
            new RecipeDefinition("spritz", "Spritz", 2, baseFlavor: 10, baseMult: 2,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: System.Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                    new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                },
                minFill: 0.5),
            price,
            new ServingSpec(new[] { Preparations.Ice }));   // craft the judge rewards with a next round

        private static GlassContents ExactGlass()
        {
            var glass = new GlassContents(1.0);
            glass.Add("gin", 0.4);
            glass.Add("soda", 0.4);
            glass.AddPreparation(Preparations.Ice);   // the spec met, so OrdersAgain fires
            return glass;
        }

        [Test]
        public void TheCard_ShowsAtMostOneExactValue()
        {
            var truth = new EmotionStats(new[] { 11, 22, 33, 44, 55, 66 });

            for (int i = 0; i < 25; i++)
            {
                var read = CustomerReadFactory.Build(truth, 3, new RunRng($"s{i}").GetStream("read"));
                int exact = Emotions.All.Count(e => read[e].Tier == VisibilityTier.Exact);
                Assert.AreEqual(1, exact, "exactly one stat is ever printed outright");
            }
        }
    }
}
