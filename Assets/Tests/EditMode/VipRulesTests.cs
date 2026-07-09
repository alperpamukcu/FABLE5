using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    public class VipRulesTests
    {
        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static IngredientCard Card(IngredientType type, int flavor) =>
            new IngredientCard($"{type}_{flavor}", $"{type} {flavor}", type, flavor);

        private static List<IngredientCard> SpiritCards(int count = 48) =>
            Enumerable.Range(0, count)
                .Select(i => new IngredientCard($"spirit_{i}", $"Spirit {i}", IngredientType.Spirit, 6))
                .ToList();

        private static RoundController NewRound(VipRuleSet rules,
            Deck deck = null, IReadOnlyDictionary<string, int> levels = null,
            IReadOnlyList<PatronInstance> patrons = null) =>
            new RoundController(deck ?? new Deck(SpiritCards()), Recipes,
                new CustomerOrder("VIP", 100000), null, levels, patrons, rules);

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
        public void DebuffedType_ScoresNothing_ButRecipeStillMatches()
        {
            var rules = new VipRuleSet(new[] { IngredientType.Spirit }, false, 0, false);
            var deck = new Deck(new[]
            {
                Card(IngredientType.Spirit, 6), Card(IngredientType.Sweet, 4),
                Card(IngredientType.Bitter, 3), Card(IngredientType.Spirit, 2),
                Card(IngredientType.Spirit, 2), Card(IngredientType.Spirit, 2),
                Card(IngredientType.Spirit, 2), Card(IngredientType.Spirit, 2)
            });
            var round = NewRound(rules, deck);

            var mix = round.Rail.Where(c => c.Type != IngredientType.Spirit)
                .Concat(round.Rail.Where(c => c.Type == IngredientType.Spirit && c.Flavor == 6))
                .ToList();
            var breakdown = round.Mix(mix);

            // Old Fashioned still forms; debuffed Spirit adds nothing: (20+4+3) x 2 = 54.
            Assert.AreEqual("old_fashioned", breakdown.Recipe.Id);
            Assert.AreEqual(54, breakdown.FinalScore);
            Assert.IsTrue(breakdown.Steps.Any(s => s.Source.Contains("debuffed")));
        }

        [Test]
        public void DebuffedCard_TriggersNoPatronOrQualityEffects()
        {
            var chemist = new PatronInstance(new PatronDefinition("chemist", "The Chemist",
                PatronRarity.Uncommon, 6, "",
                new[] { new PatronEffect(EffectTrigger.OnCardScored, EffectOp.AddMult, 2,
                    EffectCondition.CardTypeIs(IngredientType.Sour)) }));
            var rules = new VipRuleSet(new[] { IngredientType.Sour }, false, 0, false);
            var sour = Card(IngredientType.Sour, 4);
            sour.Enhance(Enhancement.Overproof);
            var deck = new Deck(new[]
            {
                Card(IngredientType.Spirit, 6), sour, Card(IngredientType.Sweet, 2),
                Card(IngredientType.Spirit, 2), Card(IngredientType.Spirit, 2),
                Card(IngredientType.Spirit, 2), Card(IngredientType.Spirit, 2),
                Card(IngredientType.Spirit, 2)
            });
            var round = NewRound(rules, deck, patrons: new[] { chemist });

            var mix = new[] { round.Rail.First(c => c.Flavor == 6 && c.Type == IngredientType.Spirit),
                round.Rail.First(c => c.Type == IngredientType.Sour),
                round.Rail.First(c => c.Type == IngredientType.Sweet) };
            var breakdown = round.Mix(mix);

            // Sour recipe: (30 + 6 + 0 + 2) x 3 — no +2 Mult from Chemist, no Overproof.
            Assert.AreEqual("sour", breakdown.Recipe.Id);
            Assert.AreEqual(38 * 3, breakdown.FinalScore);
        }

        [Test]
        public void OnlyFirstMixScores_VoidsLaterMixes_ButConsumesThem()
        {
            var rules = new VipRuleSet(null, true, 0, false);
            var round = NewRound(rules);

            var first = round.Mix(new[] { round.Rail[0] });
            var second = round.Mix(new[] { round.Rail[0] });

            Assert.AreEqual(11, first.FinalScore);
            Assert.IsTrue(second.IsVoided);
            Assert.AreEqual(0, second.FinalScore);
            Assert.AreEqual(2, round.MixesRemaining);
            Assert.AreEqual(11, round.AccumulatedScore);
        }

        [Test]
        public void MinRecipeLevel_VoidsUnleveledRecipes()
        {
            var rules = new VipRuleSet(null, false, 2, false);
            var levels = new Dictionary<string, int> { ["neat_pour"] = 2 };
            var round = NewRound(rules, levels: levels);

            var leveled = round.Mix(new[] { round.Rail[0] }); // Neat Pour Lv2 scores
            Assert.IsFalse(leveled.IsVoided);
            Assert.AreEqual((15 + 6) * 2, leveled.FinalScore);

            var spritzDeck = new Deck(new[]
            {
                Card(IngredientType.Spirit, 6), Card(IngredientType.Bubbly, 2),
                Card(IngredientType.Spirit, 2), Card(IngredientType.Spirit, 2),
                Card(IngredientType.Spirit, 2), Card(IngredientType.Spirit, 2),
                Card(IngredientType.Spirit, 2), Card(IngredientType.Spirit, 2)
            });
            var round2 = NewRound(rules, spritzDeck, levels);
            var voided = round2.Mix(new[]
            {
                round2.Rail.First(c => c.Flavor == 6),
                round2.Rail.First(c => c.Type == IngredientType.Bubbly)
            });
            Assert.IsTrue(voided.IsVoided, "Spritz is still level 1");
        }

        [Test]
        public void EachMixDifferentRecipe_VoidsRepeats()
        {
            var rules = new VipRuleSet(null, false, 0, true);
            var round = NewRound(rules);

            var first = round.Mix(new[] { round.Rail[0] });
            var repeat = round.Mix(new[] { round.Rail[0] });

            Assert.IsFalse(first.IsVoided);
            Assert.IsTrue(repeat.IsVoided);
        }

        [Test]
        public void PreviewScore_ShowsTheVoid_BeforeCommitting()
        {
            var rules = new VipRuleSet(null, true, 0, false);
            var round = NewRound(rules);
            round.Mix(new[] { round.Rail[0] });

            var preview = round.PreviewScore(new[] { round.Rail[0] });
            Assert.IsTrue(preview.IsVoided);
            Assert.AreEqual(3, round.MixesRemaining, "preview must not consume");
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
                run.Mix(new[] { run.CurrentRound.Rail[0] });
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
        public void RailSizeDelta_ShrinksTheRail()
        {
            var inspector = new VipDefinition("health_inspector", "The Health Inspector",
                "Rail size -3 this order.", true, false,
                new[] { new VipRule(VipRuleKind.RailSizeDelta, intValue: -3) });
            var run = NewRun(new[] { inspector });
            AdvanceToNextVip(run);

            Assert.AreEqual(5, run.CurrentRound.Rail.Count);
            Assert.AreEqual(5, run.CurrentRound.Config.RailSize);
        }
    }
}
