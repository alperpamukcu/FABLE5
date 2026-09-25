using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// HOW A DRINK IS USUALLY TAKEN (2026-09-23, the author: "bazı kokteyllerde bazı garnishler
    /// şarttır, örneğin gin fizz'de şeker gerdanlık — yani gin fizz söyleyen biri yüksek ihtimalle
    /// şeker gerdanlıklı söylemeli. Bunun aynısı diğer garnishler için de geçerli").
    ///
    /// Two different things are pinned here. The FICTION — a page's own dressing is asked for far
    /// more often than the dice would ever ask for it, but not always, or it would be a second
    /// signature. And the DETERMINISM, which is the one that would break quietly: the order stream
    /// must draw the same number of times whatever page is rolled, or re-tuning which drink likes
    /// ice reseeds every later customer's night and a string seed stops reproducing a run.
    /// </summary>
    public sealed class ServingHabitTests
    {
        private static RecipeDefinition Page(string id, string garnish = null, params string[] likes) =>
            new RecipeDefinition(id, id, 12, 10, 2, 0, 0,
                Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                    new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                },
                prep: PrepMethod.Built, garnish: garnish, likes: likes);

        private static RecipeDefinition Pint() =>
            new RecipeDefinition("draught", "Draught", 1, 5, 1, 10, 1,
                new[] { new PatternRequirement(1, IngredientType.Beer) },
                exactMixSize: 1, minFill: 0.75, glassId: "pint", prep: PrepMethod.Built,
                likes: new[] { "lemon_twist" });

        /// <summary>The whole rail, the way a bar at three stars has it.</summary>
        private static IReadOnlyList<PreparationDefinition> Rail() => ServingSpec.GarnishPool;

        private static List<ServingSpec> RollMany(RecipeDefinition r, string seed, int n,
            IReadOnlyList<PreparationDefinition> rail)
        {
            var rng = new RunRng(seed).GetStream("orders");
            var specs = new List<ServingSpec>(n);
            for (int i = 0; i < n; i++) specs.Add(ServingSpec.Roll(r, rng, rail));
            return specs;
        }

        private static int Asked(IEnumerable<ServingSpec> specs, string id) =>
            specs.Count(s => s.Garnishes.Any(g => g.Id == id));

        [Test]
        public void ThePageIsAskedForItsOwnDressing_MostOfTheTime_AndNotAlways()
        {
            // The author's own example, in the shape the book carries it: a Gin Fizz over ice with
            // a sugared rim.
            var fizz = Page("gin_fizz", likes: new[] { "ice", "sugar_rim" });
            var specs = RollMany(fizz, "habit-fizz", 600, Rail());

            int rim = Asked(specs, "sugar_rim"), ice = Asked(specs, "ice");
            Assert.Greater(rim, 600 * 0.6, "a Gin Fizz is nearly always asked for with its rim");
            Assert.Less(rim, 600, "but not every single time — a habit is not a signature");
            Assert.Greater(ice, 600 * 0.6, "and over ice");

            // ...where the same drink WITHOUT the habit is asked for its rim only by the dice.
            var plain = Page("plain_fizz");
            Assert.Less(Asked(RollMany(plain, "habit-fizz", 600, Rail()), "sugar_rim"), 600 * 0.3,
                "a page with no habit is dressed by the dice and nothing else");
        }

        [Test]
        public void TheOrderStream_DrawsTheSameWhicheverPageItIs()
        {
            // THE ONE THAT WOULD BREAK QUIETLY. If a page's habit changed how many numbers an order
            // consumes, then moving "ice" from one drink to another would change every LATER
            // customer of the night — a content edit reseeding a run, with nothing to show for it.
            var withHabit = Page("with", likes: new[] { "ice", "lemon_twist" });
            var without = Page("without");
            var signed = Page("signed", garnish: "olive");

            foreach (var rail in new[] { Rail(), (IReadOnlyList<PreparationDefinition>)new List<PreparationDefinition>() })
                foreach (var seed in new[] { "a", "b", "c", "d" })
                {
                    var probes = new List<int>();
                    foreach (var page in new[] { withHabit, without, signed })
                    {
                        var rng = new RunRng(seed).GetStream("orders");
                        for (int i = 0; i < 12; i++) ServingSpec.Roll(page, rng, rail);
                        probes.Add(rng.NextInt(1000000));      // where the stream stands afterwards
                    }
                    Assert.AreEqual(probes[0], probes[1],
                        "a habit must not move the stream (seed " + seed + ")");
                    Assert.AreEqual(probes[0], probes[2],
                        "and neither must a signature (seed " + seed + ")");
                }
        }

        [Test]
        public void AHabitTheBarCannotGive_IsNotAskedFor()
        {
            // The rail has the last word, exactly as it does for the dice's own extras: an opening
            // night has nothing on it, so nobody asks for anything.
            var fizz = Page("gin_fizz", likes: new[] { "ice", "sugar_rim" });
            var bare = new List<PreparationDefinition>();
            Assert.IsTrue(RollMany(fizz, "habit-bare", 200, bare).All(s => s.IsPlain),
                "a bar with nothing on its rail is asked for nothing");

            // And a rail with only the first rung open gives the ice and withholds the rim.
            var firstRung = new List<PreparationDefinition> { Preparations.Ice, Preparations.LemonTwist };
            var specs = RollMany(fizz, "habit-rung", 400, firstRung);
            Assert.Greater(Asked(specs, "ice"), 400 * 0.6);
            Assert.AreEqual(0, Asked(specs, "sugar_rim"), "the rims are not open yet");
        }

        [Test]
        public void APint_TakesNoHabit()
        {
            // GDD 21 §10: a pint is pulled, not dressed. Even a page that names a habit gets none.
            Assert.IsTrue(RollMany(Pint(), "habit-pint", 200, Rail()).All(s => s.IsPlain));
        }

        [Test]
        public void TheUsualIsTheWholeAsk_SoNobodyOrdersFourThings()
        {
            // When the drink comes as it comes, the dice stand down: the point of a habit is that
            // the ask is the DRINK's, and piling a random twist on top of a rim and an ice would
            // make the fussiest pages the ones with the most character.
            var fizz = Page("gin_fizz", garnish: "mint", likes: new[] { "ice", "sugar_rim" });
            foreach (var spec in RollMany(fizz, "habit-cap", 600, Rail()))
                Assert.LessOrEqual(spec.RequestCount, 3,
                    "a signature and the drink's own dressing, and never more than that");
        }

        [Test]
        public void TheShippedBook_NamesOnlyRealPreparations_AndNeverItsOwnSignature()
        {
            var legal = ServingSpec.GarnishPool.Select(p => p.Id).ToList();
            foreach (var r in RecipeCatalog.CreateDefault())
            {
                foreach (var id in r.Likes)
                {
                    CollectionAssert.Contains(legal, id, r.Id + " is usually taken with '" + id + "'");
                    Assert.AreNotEqual(r.Garnish, id,
                        r.Id + " lists its own signature as a habit, which can only ever be a no-op");
                }
                Assert.AreEqual(r.Likes.Distinct().Count(), r.Likes.Count, r.Id + ": a habit twice");
            }
            // The pint is pulled, and the neat pour is neat: neither is dressed by the house.
            Assert.IsEmpty(RecipeCatalog.CreateDefault().First(r => r.Id == "draught").Likes);
            Assert.IsEmpty(RecipeCatalog.CreateDefault().First(r => r.Id == "neat_pour").Likes);
        }
    }
}
