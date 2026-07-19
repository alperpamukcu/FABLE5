using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    public class VipRulesTests
    {
        // The shipped recipe table has no ratio bands until Phase 5 authors them, so this
        // suite brings its own. VIP rules are about voiding and debuffing a *matched* drink;
        // they need something matchable, and waiting on the content pass would leave the
        // rules untested in the meantime.
        private static readonly RecipeDefinition NeatPour = new RecipeDefinition(
            "neat_pour", "Neat Pour", rank: 1, baseFlavor: 5, baseMult: 1,
            flavorPerLevel: 10, multPerLevel: 1, requirements: Array.Empty<PatternRequirement>(),
            ratioRequirements: new[] { new RatioRequirement(IngredientType.Spirit, 0.90, 1.00) },
            minFill: 0.30);

        private static readonly RecipeDefinition Spritz = new RecipeDefinition(
            "spritz", "Spritz", rank: 2, baseFlavor: 10, baseMult: 2,
            flavorPerLevel: 15, multPerLevel: 1, requirements: Array.Empty<PatternRequirement>(),
            ratioRequirements: new[]
            {
                new RatioRequirement(IngredientType.Spirit, 0.40, 0.60),
                new RatioRequirement(IngredientType.Bubbly, 0.40, 0.60),
            },
            minFill: 0.30);

        private static readonly IReadOnlyList<RecipeDefinition> Recipes = new[] { NeatPour, Spritz };

        private static IngredientCard Card(string id, IngredientType type, int flavor) =>
            new IngredientCard(id, id, type, flavor);

        private static List<IngredientCard> SpiritCards(int count = 48) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        /// <summary>A bar with the two ingredients the local recipes are made of.</summary>
        private static Shelf BarShelf() => new Shelf(new[]
        {
            new ShelfBottle(Card("gin", IngredientType.Spirit, 6), capacity: 20),
            new ShelfBottle(Card("soda", IngredientType.Bubbly, 2), capacity: 20),
        });

        private static RoundController NewRound(VipRuleSet rules,
            Shelf shelf = null, IReadOnlyDictionary<string, int> levels = null,
            IReadOnlyList<PatronInstance> patrons = null) =>
            new RoundController(shelf ?? BarShelf(), Recipes,
                new CustomerOrder("VIP", 100000), null, levels, patrons, rules);

        /// <summary>Pours a full glass of gin — a Neat Pour worth (5 + 6) x 1 = 11.</summary>
        private static ScoreBreakdown ServeNeat(RoundController round)
        {
            round.PourMeasure("gin", round.Config.GlassCapacity);
            return round.Serve();
        }

        private static readonly VipDefinition Teetotaler = new VipDefinition(
            "teetotaler", "The Teetotaler", "Spirits score 0 Flavor this order.", false, false,
            new[] { new VipRule(VipRuleKind.DebuffType, IngredientType.Spirit) });

        private static readonly VipDefinition Critic = new VipDefinition(
            "critic", "The Critic", "Target x1.5, one random type debuffed.", false, true,
            new[]
            {
                new VipRule(VipRuleKind.TargetScale, doubleValue: 1.5),
                new VipRule(VipRuleKind.DebuffRandomType)
            });

        private static readonly VipDefinition Gentle = new VipDefinition(
            "allergic", "The Allergic", "All Garnish cards are debuffed.", true, false,
            new[] { new VipRule(VipRuleKind.DebuffType, IngredientType.Garnish) });

        private static readonly VipDefinition Harsh = new VipDefinition(
            "purist", "The Purist", "Only your first Mix counts.", false, false,
            new[] { new VipRule(VipRuleKind.OnlyFirstMixScores) });

        // ── rule mechanics on the round ──────────────────────────────────────────

        [Test]
        public void ADebuffedIngredient_AddsNoFlavor()
        {
            var debuffed = new VipRuleSet(new[] { IngredientType.Spirit }, false, 0, false);
            var round = NewRound(debuffed);

            var breakdown = ServeNeat(round);

            Assert.AreEqual("neat_pour", breakdown.Recipe.Id, "the drink still forms");
            Assert.AreEqual(5, breakdown.TotalFlavor, 1e-9, "recipe base only; the gin adds nothing");
            Assert.IsTrue(breakdown.Steps.Any(s => s.Source.Contains("debuffed")));
        }

        [Test]
        public void OnlyFirstDrinkScores_VoidsLaterOnes_ButConsumesThem()
        {
            var round = NewRound(new VipRuleSet(null, true, 0, false));

            var first = ServeNeat(round);
            var second = ServeNeat(round);

            Assert.AreEqual(11, first.FinalScore, 1e-9);
            Assert.IsTrue(second.IsVoided);
            Assert.AreEqual(0, second.FinalScore);
            Assert.AreEqual(2, round.DrinksRemaining);
            Assert.AreEqual(11, round.AccumulatedScore, 1e-9);
        }

        [Test]
        public void MinRecipeLevel_VoidsUnleveledRecipes()
        {
            var rules = new VipRuleSet(null, false, 2, false);
            var levels = new Dictionary<string, int> { ["neat_pour"] = 2 };

            var leveled = NewRound(rules, levels: levels);
            var scored = ServeNeat(leveled);
            Assert.IsFalse(scored.IsVoided);
            Assert.AreEqual((15 + 6) * 2, scored.FinalScore, 1e-9, "Neat Pour at level 2");

            // A Spritz is still level 1, so the same VIP voids it.
            var unleveled = NewRound(rules, levels: levels);
            unleveled.PourMeasure("gin", 0.5);
            unleveled.PourMeasure("soda", 0.5);
            Assert.IsTrue(unleveled.Serve().IsVoided);
        }

        [Test]
        public void EachDrinkMustBeADifferentRecipe()
        {
            var round = NewRound(new VipRuleSet(null, false, 0, true));

            var first = ServeNeat(round);
            var repeat = ServeNeat(round);

            Assert.IsFalse(first.IsVoided);
            Assert.IsTrue(repeat.IsVoided);
        }

        [Test]
        public void PreviewScore_ShowsTheVoid_BeforeCommitting()
        {
            var round = NewRound(new VipRuleSet(null, true, 0, false));
            ServeNeat(round);

            round.PourMeasure("gin", round.Config.GlassCapacity);
            var preview = round.PreviewScore();

            Assert.IsTrue(preview.IsVoided);
            Assert.AreEqual(3, round.DrinksRemaining, "preview must not consume");
        }

        // ── VIP selection on the run ─────────────────────────────────────────────

        private static RunController NewRun(IReadOnlyList<VipDefinition> pool,
            int nights = 8, string seed = "VIP-TEST") =>
            new RunController(SpiritCards(), Recipes, new RunRng(seed), null,
                new RunConfig(nights: nights, targetProvider: (n, s) => 10),
                vipPool: pool);

        /// <summary>Wins customers (Neat Pour spam) until sitting at a fresh VIP round.</summary>
        private static void AdvanceToNextVip(RunController run)
        {
            do
            {
                PourTestKit.ServeSomething(run);
                if (run.Phase == RunPhase.BackRoom) run.ContinueToNextCustomer();
            }
            while (!(run.Slot == CustomerSlot.Vip && run.Phase == RunPhase.CustomerRound));
        }

        [Test]
        public void VipSlot_GetsAVip_RegularsDoNot()
        {
            var run = NewRun(new[] { Gentle, Harsh, Critic });
            Assert.IsNull(run.CurrentVip, "Customer A is a regular");

            AdvanceToNextVip(run);
            Assert.IsNotNull(run.CurrentVip);
            Assert.IsNotEmpty(run.CurrentRound.Customer.RuleText);
        }

        [Test]
        public void Nights1And2_DrawOnlyGentleVips()
        {
            var run = NewRun(new[] { Gentle, Harsh, Critic }, nights: 2);
            AdvanceToNextVip(run);
            Assert.AreEqual("allergic", run.CurrentVip.Id, "night 1 must use the gentle pool");
        }

        [Test]
        public void FinalNight_AlwaysBringsTheFinaleVip_WithScaledTarget()
        {
            var run = NewRun(new[] { Gentle, Harsh, Critic }, nights: 2);
            AdvanceToNextVip(run);                        // night 1 VIP
            AdvanceToNextVip(run);                        // night 2 VIP = finale

            Assert.AreEqual(2, run.Night);
            Assert.AreEqual("critic", run.CurrentVip.Id);
            Assert.AreEqual(15, run.CurrentRound.Customer.TargetScore, "10 x 1.5");
            Assert.AreEqual(1, run.CurrentRound.VipRules.DebuffedTypes.Count, "one random type debuffed");
        }

        [Test]
        public void VipChoice_IsSeedDeterministic()
        {
            VipDefinition VipOf(string seed)
            {
                var run = NewRun(new[] { Gentle, Harsh, Critic }, seed: seed);
                for (int night = 1; night <= 3; night++) AdvanceToNextVip(run);
                return run.CurrentVip;
            }

            Assert.AreEqual(VipOf("PAIR").Id, VipOf("PAIR").Id);
        }

        [Test]
        public void RailSizeDelta_ShrinksTheGlass()
        {
            var inspector = new VipDefinition("health_inspector", "The Health Inspector",
                "Less room to work in this order.", true, false,
                new[] { new VipRule(VipRuleKind.RailSizeDelta, intValue: -3) });
            var run = NewRun(new[] { inspector });
            AdvanceToNextVip(run);

            // The rail is gone; the equivalent squeeze is a 30% smaller glass, which is less
            // room for the precision the pour system asks for.
            Assert.AreEqual(0.7, run.CurrentRound.Config.GlassCapacity, 1e-9);
        }
    }
}
