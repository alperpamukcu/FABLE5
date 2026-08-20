using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The 2026-08-20 perfect-pour respec (GDD 21 §9, 23 §4): every recipe carries one hidden
    /// perfect pour; the menu shows only the 20-point box it sits in until the drink has been
    /// made perfectly once; the box is the acceptance and the closeness to the perfect is the
    /// pay. These suites pin the three legs — the derivation, the grading, the reveal gate —
    /// and the catalogue-wide checks are the real guard: a perfect that sits on a grid edge
    /// or reads as a higher-ranked drink is a broken order, whichever recipe it happens to.
    /// </summary>
    public class PerfectPourTests
    {
        // ── the boxes ───────────────────────────────────────────────────────────

        [Test]
        public void Boxes_AreLowerInclusive_TheAuthorsOwnExample()
        {
            // "40 vodka koyması gerektiği yerde sarı" — an exact 40 lights the 40–60 box,
            // a 39 lights the 20–40 one. And 0.60/0.20 is 2.999…96 in doubles: the epsilon
            // is what keeps an exact 60 out of the yellow box.
            Assert.AreEqual(2, RatioBox.IndexOf(0.40));
            Assert.AreEqual(1, RatioBox.IndexOf(0.39));
            Assert.AreEqual(3, RatioBox.IndexOf(0.60));
            Assert.AreEqual(0, RatioBox.IndexOf(0.0));
            Assert.AreEqual(4, RatioBox.IndexOf(1.0));
            Assert.AreEqual(4, RatioBox.IndexOf(0.80));
            Assert.AreEqual(1, RatioBox.IndexOf(0.399999));
        }

        // ── the derivation, over the whole shipped catalogue ────────────────────

        private static IEnumerable<RecipeDefinition> AuthoredCatalogue() =>
            RecipeCatalog.CreateDefault().Where(r => r.HasAuthoredRatios);

        [Test]
        public void EveryPerfect_FillsTheGlass_AndSitsClearOfTheGrid()
        {
            foreach (var recipe in AuthoredCatalogue())
            {
                var perfect = RatioRecipeMatcher.PerfectPour(recipe);
                Assert.AreEqual(recipe.RatioRequirements.Count, perfect.Length, recipe.Id);
                Assert.AreEqual(1.0, perfect.Sum(), 1e-6,
                    $"{recipe.Id}: a perfect pour is a full glass");
                for (int i = 0; i < perfect.Length; i++)
                {
                    double inBox = perfect[i] % RatioBox.Width;
                    double toEdge = Math.Min(inBox, RatioBox.Width - inBox);
                    Assert.GreaterOrEqual(toEdge, RatioRecipeMatcher.BoxEdgeGuard - 1e-6,
                        $"{recipe.Id}[{i}]: a perfect on a grid edge is an impossible order");
                    Assert.GreaterOrEqual(perfect[i], ServiceJudge.TraceShare + 0.01,
                        $"{recipe.Id}[{i}]: a perfect below the dash floor cannot be poured legally");
                }
            }
        }

        [Test]
        public void ThePerfect_IsStable_AndCachedOnTheRecipe()
        {
            foreach (var recipe in AuthoredCatalogue())
            {
                var once = RatioRecipeMatcher.PerfectPour(recipe);
                var twice = RatioRecipeMatcher.PerfectPour(recipe);
                CollectionAssert.AreEqual(once, twice, recipe.Id);
                for (int i = 0; i < once.Length; i++)
                    Assert.AreEqual(RatioBox.IndexOf(once[i]), recipe.PerfectBoxes[i],
                        $"{recipe.Id}[{i}]: the public boxes must be the perfect's own boxes");
            }
        }

        [Test]
        public void TheSevenIdenticalHighballs_LearnDifferentPerfects()
        {
            // Seven starter builds share the same 40/60 ideal; the id-hashed nudge is what
            // makes each its own drink to learn. If they all settled to one number the
            // reveal would teach nothing.
            var perfects = new[] { "vodka_soda", "gin_tonic", "whiskey_cola", "screwdriver" }
                .Select(id => RecipeCatalog.CreateDefault().First(r => r.Id == id))
                .Select(r => Math.Round(RatioRecipeMatcher.PerfectPour(r)[0], 4))
                .ToList();
            Assert.Greater(perfects.Distinct().Count(), 1,
                "the starter highballs must not share one perfect");
        }

        // ── makeability: the perfect pour must read as its own drink ────────────

        private static readonly Dictionary<string, IngredientCard> Cellar =
            new Dictionary<string, IngredientCard>();

        /// <summary>A top-shelf bottle of any style, minted on demand — tier 4 so the
        /// MinTier bands read the full share.</summary>
        private static IngredientCard CardOf(string style)
        {
            if (!Cellar.TryGetValue(style, out var card))
                Cellar[style] = card = new IngredientCard(style, style,
                    IngredientType.Spirit, 5, new IngredientInfo(style, tier: 4));
            return card;
        }

        [Test]
        public void EveryPerfectPour_ServedExactly_ReadsAsItsOwnRecipe()
        {
            // Matched against the WHOLE catalogue, locked pages included — harsher than the
            // live game, which only matches unlocked recipes. If some drink's perfect pour
            // reads as a higher-ranked drink, its order can never be served Exact and the
            // recipe is dead content.
            var all = RecipeCatalog.CreateDefault();
            foreach (var recipe in all.Where(r => r.HasAuthoredRatios))
            {
                var perfect = RatioRecipeMatcher.PerfectPour(recipe);
                var glass = new GlassContents(1.0);
                for (int i = 0; i < perfect.Length; i++)
                    glass.Add(recipe.RatioRequirements[i].Style, perfect[i]);

                var match = RatioRecipeMatcher.Match(glass, all, CardOf);
                Assert.IsNotNull(match, $"{recipe.Id}: its own perfect matches nothing");
                Assert.AreEqual(recipe.Id, match.Recipe.Id,
                    $"{recipe.Id}: its perfect pour reads as {match.Recipe.Id}");

                var order = new DrinkOrder(recipe, 6);
                Assert.AreEqual(OrderMatch.Exact,
                    ServiceJudge.Compare(order, match, glass, CardOf), recipe.Id);
            }
        }

        [Test]
        public void TheWrongBox_IsNotTheDrink_EvenInsideTheOldBand()
        {
            // vodka_soda's authored band accepts 30–50% vodka; its perfect sits in ONE
            // 20-point box. A pour inside the old band but in the neighbouring box must not
            // match — the box on the menu is the whole contract now.
            var recipe = RecipeCatalog.CreateDefault().First(r => r.Id == "vodka_soda");
            var perfect = RatioRecipeMatcher.PerfectPour(recipe);
            int vodkaBox = RatioBox.IndexOf(perfect[0]);

            // Build the drink with the vodka nudged into the box next door, soda taking the
            // difference (staying legal-sum), and check the matcher refuses it.
            double wrongVodka = vodkaBox == 1 ? 0.45 : 0.35;   // the other side of 40%
            var glass = new GlassContents(1.0);
            glass.Add("vodka", wrongVodka);
            glass.Add("soda", 1.0 - wrongVodka);

            var match = RatioRecipeMatcher.Match(glass, new[] { recipe }, CardOf);
            Assert.IsNull(match, "the wrong box is not the drink");
            Assert.AreEqual(OrderMatch.Close,
                ServiceJudge.Compare(new DrinkOrder(recipe, 6), match, glass, CardOf),
                "still recognisably their drink, made wrong");
        }

        // ── the pay: closeness scales it, the floor holds it ────────────────────

        private static CustomerVisit VisitFor(RecipeDefinition recipe, int price = 10) =>
            new CustomerVisit(new DrinkOrder(recipe, price), patienceSeconds: 60);

        private static GlassContents GlassAt(RecipeDefinition recipe, params double[] shares)
        {
            var glass = new GlassContents(1.0);
            for (int i = 0; i < shares.Length; i++)
                glass.Add(recipe.RatioRequirements[i].Style, shares[i]);
            return glass;
        }

        [Test]
        public void ClosenessScalesTheBase_AndTheFarEdgeStillEarnsSomething()
        {
            var recipe = RecipeCatalog.CreateDefault().First(r => r.Id == "vodka_soda");
            var perfect = RatioRecipeMatcher.PerfectPour(recipe);

            var dead = ServiceJudge.Judge(VisitFor(recipe), OrderMatch.Exact,
                GlassAt(recipe, perfect[0], perfect[1]), served: null, lookup: CardOf);
            Assert.AreEqual(1.0, dead.Accuracy, 1e-9);
            Assert.IsTrue(dead.PerfectMake, "dead on the number is the perfect make");
            Assert.AreEqual(10, dead.BasePaid, "a perfect pour earns the whole menu price");

            // Same box, four points adrift: paid, but visibly less.
            var off = ServiceJudge.Judge(VisitFor(recipe), OrderMatch.Exact,
                GlassAt(recipe, perfect[0] + 0.04, perfect[1] - 0.04), served: null, lookup: CardOf);
            Assert.Less(off.Accuracy, 1.0);
            Assert.IsFalse(off.PerfectMake, "four points off is not perfect");
            Assert.Less(off.BasePaid, dead.BasePaid, "closeness is money now");
            Assert.GreaterOrEqual(off.BasePaid, 1,
                "the author's floor: the right box always earns SOMETHING");
            Assert.Less(off.Tip, dead.Tip, "and the thanks cool with the pour");
        }

        [Test]
        public void JudgedWithoutAShelf_TheOldTestsStillPriceAtFullAccuracy()
        {
            // The economy suites price drinks with no lookup — nothing to measure, so
            // accuracy reads 1 and nothing they pin moved. This is that contract, pinned.
            var recipe = RecipeCatalog.CreateDefault().First(r => r.Id == "vodka_soda");
            var verdict = ServiceJudge.Judge(VisitFor(recipe), OrderMatch.Exact,
                GlassAt(recipe, 0.5, 0.45));
            Assert.AreEqual(1.0, verdict.Accuracy, 1e-9);
            Assert.IsFalse(verdict.PerfectMake, "but nothing unmeasured is ever PERFECT");
            Assert.AreEqual(10, verdict.BasePaid);
        }

        // ── the reveal gate ─────────────────────────────────────────────────────

        private static RecipeDefinition TestSpritz() => new RecipeDefinition(
            "spritz", "Spritz", rank: 2, baseFlavor: 10, baseMult: 2,
            flavorPerLevel: 0, multPerLevel: 0,
            requirements: Array.Empty<PatternRequirement>(),
            ratioRequirements: new[]
            {
                new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
            },
            minFill: 0.5, prep: PrepMethod.Built);

        private static TycoonRun RevealRun(string seed = "reveal") =>
            new TycoonRun(
                new Shelf(new[]
                {
                    new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), 20),
                    new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), 20),
                }),
                new[] { TestSpritz() }, new RunRng(seed),
                config: new TycoonConfig(20, orderDecisionSeconds: 0, savorSeconds: 0));

        private static CustomerVisit FirstDrinker(TycoonRun run)
        {
            int guard = 0;
            while (run.Floor.Seated.Count == 0)
            {
                Assert.Less(guard++, 200);
                run.Tick(5);
            }
            return run.Floor.Seated[0];
        }

        [Test]
        public void TheExactPour_IsThrownUntilPerfected_AndRevealedAfter()
        {
            var run = RevealRun();
            var recipe = run.MenuRecipes.First(r => r.Id == "spritz");
            var visit = FirstDrinker(run);

            Assert.IsFalse(run.IsPerfected("spritz"));
            Assert.Throws<InvalidOperationException>(() => run.ExactPourFor(recipe),
                "the menu knows only the boxes until the drink has been made perfectly");

            // The test spritz's perfect settles to 50/50 — pour it dead on.
            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
            var verdict = run.ServeTo(visit);

            Assert.AreEqual(OrderMatch.Exact, verdict.Match);
            Assert.IsTrue(verdict.PerfectMake, "a dead-centre pour is the perfect make");
            Assert.IsTrue(run.IsPerfected("spritz"), "and the run remembers it");
            var revealed = run.ExactPourFor(recipe);
            Assert.AreEqual(recipe.RatioRequirements.Count, revealed.Count);
            Assert.AreEqual(1.0, revealed.Sum(), 1e-6);

            var best = run.BestMakeFor("spritz");
            Assert.IsNotNull(best, "the best make is on the book");
            Assert.AreEqual(1.0, best.Accuracy, 1e-6);
        }

        [Test]
        public void TheUiNeverComputesThePerfect()
        {
            // The reveal gate is TycoonRun.ExactPourFor and nothing else. IdealPour and
            // PerfectPour are pure functions of public band data, so Core cannot REFUSE the
            // math — but it can be caught reaching for it: the UI assembly must not name
            // either. This is the fence CLAUDE.md's hidden-information rule asks for, in the
            // only place a fence around arithmetic can stand. (The Editor assembly — the sim,
            // the balance guide — is design tooling and stays free to compute.)
            var uiRoot = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scripts", "UI");
            var offenders = new List<string>();
            foreach (var file in System.IO.Directory.GetFiles(uiRoot, "*.cs",
                System.IO.SearchOption.AllDirectories))
            {
                var text = System.IO.File.ReadAllText(file);
                if (text.Contains("IdealPour") || text.Contains("PerfectPour"))
                    offenders.Add(System.IO.Path.GetFileName(file));
            }
            Assert.IsEmpty(offenders,
                "the menu asks TycoonRun what it may show; it never computes the secret: "
                + string.Join(", ", offenders));
        }

        [Test]
        public void AnImperfectExactServe_RecordsTheBestMake_ButRevealsNothing()
        {
            var run = RevealRun("imperfect");
            var visit = FirstDrinker(run);

            run.PourMeasure("gin", 0.40);   // 57/43 — in box, off the 50/50 perfect
            run.PourMeasure("soda", 0.30);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
            var verdict = run.ServeTo(visit);

            Assert.AreEqual(OrderMatch.Exact, verdict.Match);
            Assert.IsFalse(verdict.PerfectMake);
            Assert.IsFalse(run.IsPerfected("spritz"), "close is not perfect");
            var best = run.BestMakeFor("spritz");
            Assert.IsNotNull(best, "but the attempt is on the book");
            Assert.Less(best.Accuracy, 1.0);
            Assert.Greater(best.Accuracy, 0.0);
        }
    }
}
