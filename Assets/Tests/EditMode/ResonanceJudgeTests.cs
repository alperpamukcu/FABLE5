using System;
using System.Collections.Generic;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The verdict rules (GDD 19 §6). These are the boundaries the whole mechanic balances
    /// on — an exact landing must pay, one unit past it must not.
    /// </summary>
    public class ResonanceJudgeTests
    {
        private static EmotionStats Stats(Emotion emotion, int value)
        {
            var stats = new EmotionStats();
            stats.Set(emotion, value);
            return stats;
        }

        private static EmotionDelta Delta(Emotion emotion, int amount)
        {
            var delta = new EmotionDelta();
            delta.Add(emotion, amount);
            return delta;
        }

        private static CustomerRead Read(Emotion intent, IntentDirection direction,
            VisibilityTier intentTier = VisibilityTier.Exact, int trueValue = 50)
        {
            var readings = new List<StatReading>();
            foreach (var emotion in Emotions.All)
            {
                readings.Add(emotion == intent
                    ? (intentTier == VisibilityTier.Exact ? StatReading.Exact(trueValue)
                        : intentTier == VisibilityTier.Range ? StatReading.Range(trueValue, 8)
                        : StatReading.Unknown)
                    : StatReading.Exact(0));
            }
            return new CustomerRead(readings, intent, direction);
        }

        // ---- landings ----------------------------------------------------------------

        [Test]
        public void Extinguish_LandingExactlyOnZero_IsACleanServe()
        {
            var result = ResonanceJudge.Judge(
                Stats(Emotion.Sadness, 30), Delta(Emotion.Sadness, -30),
                Read(Emotion.Sadness, IntentDirection.Extinguish, trueValue: 30));

            Assert.IsFalse(result.IsBust);
            Assert.IsTrue(result.CleanServe);
            Assert.AreEqual(ResonanceJudge.CleanServeBurst, result.ServeBurst);
            Assert.AreEqual(3.0, result.ResonanceMult, 1e-9, "30 progress / 10");
            Assert.AreEqual(3, result.Satisfaction);
        }

        [Test]
        public void Fuel_LandingExactlyOnHundred_IsACleanServe()
        {
            var result = ResonanceJudge.Judge(
                Stats(Emotion.Excitement, 80), Delta(Emotion.Excitement, 20),
                Read(Emotion.Excitement, IntentDirection.Fuel, trueValue: 80));

            Assert.IsTrue(result.CleanServe);
            Assert.IsFalse(result.IsBust);
            Assert.AreEqual(2.0, result.ResonanceMult, 1e-9);
        }

        [Test]
        public void Extinguish_OneUnitPastZero_Busts()
        {
            var result = ResonanceJudge.Judge(
                Stats(Emotion.Sadness, 30), Delta(Emotion.Sadness, -31),
                Read(Emotion.Sadness, IntentDirection.Extinguish, trueValue: 30));

            Assert.IsTrue(result.IsBust);
            Assert.AreEqual(BustKind.Overshoot, result.Bust);
            Assert.AreEqual(0, result.Satisfaction);
        }

        [Test]
        public void Fuel_OneUnitPastHundred_Busts()
        {
            var result = ResonanceJudge.Judge(
                Stats(Emotion.Excitement, 80), Delta(Emotion.Excitement, 21),
                Read(Emotion.Excitement, IntentDirection.Fuel, trueValue: 80));

            Assert.AreEqual(BustKind.Overshoot, result.Bust);
        }

        // ---- wrong direction ---------------------------------------------------------

        [Test]
        public void WrongWay_UpToTen_IsASlipNotABust()
        {
            var result = ResonanceJudge.Judge(
                Stats(Emotion.Anger, 40), Delta(Emotion.Anger, 10),
                Read(Emotion.Anger, IntentDirection.Extinguish, trueValue: 40));

            Assert.IsFalse(result.IsBust);
            Assert.AreEqual(0, result.ResonanceMult);
            Assert.AreEqual(0, result.Satisfaction);
        }

        [Test]
        public void WrongWay_ElevenUnits_Busts()
        {
            var result = ResonanceJudge.Judge(
                Stats(Emotion.Anger, 40), Delta(Emotion.Anger, 11),
                Read(Emotion.Anger, IntentDirection.Extinguish, trueValue: 40));

            Assert.AreEqual(BustKind.WrongWay, result.Bust);
        }

        [Test]
        public void ASlip_StillReachesTheCustomer_ButABustDoesNot()
        {
            var slip = ResonanceJudge.Judge(
                Stats(Emotion.Anger, 40), Delta(Emotion.Anger, 5),
                Read(Emotion.Anger, IntentDirection.Extinguish, trueValue: 40));
            var bust = ResonanceJudge.Judge(
                Stats(Emotion.Anger, 40), Delta(Emotion.Anger, 30),
                Read(Emotion.Anger, IntentDirection.Extinguish, trueValue: 40));

            Assert.AreEqual(5, slip.CommittedDelta[Emotion.Anger], "a small slip still lands");
            Assert.IsTrue(bust.CommittedDelta.IsEmpty, "a bust is never written through");
        }

        // ---- blind reads -------------------------------------------------------------

        [Test]
        public void BlindProgress_PaysTheLuckyReadBonus()
        {
            var result = ResonanceJudge.Judge(
                Stats(Emotion.Anxiety, 50), Delta(Emotion.Anxiety, -20),
                Read(Emotion.Anxiety, IntentDirection.Extinguish, VisibilityTier.Unknown));

            Assert.IsTrue(result.BlindRead);
            Assert.AreEqual(ResonanceJudge.LuckyReadBonus, result.LuckyReadMult);
        }

        [Test]
        public void KnownProgress_PaysNoLuckyReadBonus()
        {
            var result = ResonanceJudge.Judge(
                Stats(Emotion.Anxiety, 50), Delta(Emotion.Anxiety, -20),
                Read(Emotion.Anxiety, IntentDirection.Extinguish, VisibilityTier.Exact, 50));

            Assert.IsFalse(result.BlindRead);
            Assert.AreEqual(0, result.LuckyReadMult);
        }

        [Test]
        public void BlindCleanServe_BurstsHarder()
        {
            var blind = ResonanceJudge.Judge(
                Stats(Emotion.Sadness, 25), Delta(Emotion.Sadness, -25),
                Read(Emotion.Sadness, IntentDirection.Extinguish, VisibilityTier.Unknown));
            var known = ResonanceJudge.Judge(
                Stats(Emotion.Sadness, 25), Delta(Emotion.Sadness, -25),
                Read(Emotion.Sadness, IntentDirection.Extinguish, VisibilityTier.Exact, 25));

            Assert.AreEqual(ResonanceJudge.BlindCleanServeBurst, blind.ServeBurst);
            Assert.AreEqual(ResonanceJudge.CleanServeBurst, known.ServeBurst);
        }

        [Test]
        public void ABlindBust_PaysNothing()
        {
            var result = ResonanceJudge.Judge(
                Stats(Emotion.Sadness, 25), Delta(Emotion.Sadness, -40),
                Read(Emotion.Sadness, IntentDirection.Extinguish, VisibilityTier.Unknown));

            Assert.IsTrue(result.IsBust);
            Assert.AreEqual(0, result.LuckyReadMult, "no bonus for a blind guess that overshot");
        }

        // ---- satisfaction ------------------------------------------------------------

        [Test]
        public void Satisfaction_IsCoarseByDesign()
        {
            Assert.AreEqual(3, ResonanceJudge.SatisfactionFor(5, cleanServe: true));
            Assert.AreEqual(2, ResonanceJudge.SatisfactionFor(
                Demands.StrongProgress(DemandLevel.Easygoing), false));
            Assert.AreEqual(1, ResonanceJudge.SatisfactionFor(1, false));
            Assert.AreEqual(0, ResonanceJudge.SatisfactionFor(0, false));
        }

        // ---- how hard they are to please (GDD 20 §2.1) --------------------------------

        [Test]
        public void ADemandingCustomer_NeedsMoreMovementForTheSameCredit()
        {
            const int progress = 28;

            Assert.AreEqual(2, ResonanceJudge.SatisfactionFor(progress, false, DemandLevel.Easygoing));
            Assert.AreEqual(2, ResonanceJudge.SatisfactionFor(progress, false, DemandLevel.Particular));
            Assert.AreEqual(1, ResonanceJudge.SatisfactionFor(progress, false, DemandLevel.Demanding));
        }

        [Test]
        public void OnlyTheDemanding_IgnoreATokenGesture()
        {
            Assert.AreEqual(1, ResonanceJudge.SatisfactionFor(4, false, DemandLevel.Easygoing));
            Assert.AreEqual(1, ResonanceJudge.SatisfactionFor(4, false, DemandLevel.Particular));
            Assert.AreEqual(0, ResonanceJudge.SatisfactionFor(4, false, DemandLevel.Demanding),
                "a nudge is worth nothing to someone who is hard to please");
        }

        [Test]
        public void ACleanServe_IsWorthThreeToAnyone()
        {
            // Landing someone exactly where they asked cannot be improved on, so demand
            // moves the goalposts, never the ceiling.
            foreach (DemandLevel demand in System.Enum.GetValues(typeof(DemandLevel)))
                Assert.AreEqual(3, ResonanceJudge.SatisfactionFor(0, true, demand), demand.ToString());
        }

        [Test]
        public void DemandRises_AsTheRunGoesOn()
        {
            Assert.AreEqual(DemandLevel.Easygoing, Demands.For(1, DemandLevel.Easygoing));
            Assert.AreEqual(DemandLevel.Particular, Demands.For(4, DemandLevel.Easygoing));
            Assert.AreEqual(DemandLevel.Demanding, Demands.For(8, DemandLevel.Easygoing));
        }

        [Test]
        public void ADemandingArchetype_StartsHarderAndClampsAtTheTop()
        {
            Assert.AreEqual(DemandLevel.Particular, Demands.For(1, DemandLevel.Particular));
            Assert.AreEqual(DemandLevel.Demanding, Demands.For(4, DemandLevel.Particular));
            Assert.AreEqual(DemandLevel.Demanding, Demands.For(8, DemandLevel.Demanding),
                "never falls off the end of the scale");
        }

        [Test]
        public void TheJudge_ReadsDemandOffTheCard()
        {
            // 20 progress: worth 2 to an easygoing customer, only 1 to a demanding one.
            var easy = ResonanceJudge.Judge(
                Stats(Emotion.Sadness, 60), Delta(Emotion.Sadness, -20),
                ReadWithDemand(DemandLevel.Easygoing));
            var hard = ResonanceJudge.Judge(
                Stats(Emotion.Sadness, 60), Delta(Emotion.Sadness, -20),
                ReadWithDemand(DemandLevel.Demanding));

            Assert.AreEqual(2, easy.Satisfaction);
            Assert.AreEqual(1, hard.Satisfaction);
            Assert.AreEqual(easy.ResonanceMult, hard.ResonanceMult, 1e-9,
                "demand changes what satisfies them, not what the drink scores");
        }

        private static CustomerRead ReadWithDemand(DemandLevel demand)
        {
            var readings = new List<StatReading>();
            foreach (var emotion in Emotions.All)
                readings.Add(emotion == Emotion.Sadness
                    ? StatReading.Exact(60)
                    : StatReading.Exact(0));
            return new CustomerRead(readings, Emotion.Sadness, IntentDirection.Extinguish, demand);
        }

        [Test]
        public void AnEmptyDelta_IsNotAServe()
        {
            var result = ResonanceJudge.Judge(
                Stats(Emotion.Sadness, 30), new EmotionDelta(),
                Read(Emotion.Sadness, IntentDirection.Extinguish));

            Assert.AreSame(ResonanceResult.None, result);
        }

        // ---- rounding boundary, end to end -------------------------------------------

        [Test]
        public void RoundingBoundary_DecidesBetweenALandingAndABust()
        {
            // Sadness 32, extinguish. A raw -32 lands exactly on 0; a raw -33 goes past it.
            var read = Read(Emotion.Sadness, IntentDirection.Extinguish, trueValue: 32);

            var lands = ResonanceJudge.Judge(Stats(Emotion.Sadness, 32), Delta(Emotion.Sadness, -32), read);
            var busts = ResonanceJudge.Judge(Stats(Emotion.Sadness, 32), Delta(Emotion.Sadness, -33), read);

            Assert.IsTrue(lands.CleanServe);
            Assert.AreEqual(BustKind.Overshoot, busts.Bust);
        }
    }

    /// <summary>Scoring's resonance block (GDD 19 §6) sits after patrons and floors Mult at 1.</summary>
    public class ResonanceScoringTests
    {
        private static RecipeMatch Match(int baseFlavor, int baseMult)
        {
            var card = new IngredientCard("c", "Card", IngredientType.Spirit, 0);
            var recipe = new RecipeDefinition("r", "Recipe", 1, baseFlavor, baseMult, 0, 0,
                Array.Empty<PatternRequirement>());
            return new RecipeMatch(recipe, new[] { card });
        }

        private static ResonanceResult Verdict(double resonance, double lucky, double burst,
            double penalty, BustKind bust = BustKind.None) =>
            new ResonanceResult(resonance, lucky, burst, penalty, bust,
                burst != 1, false, 0, 0, EmotionDelta.Empty);

        [Test]
        public void Resonance_AddsToMult()
        {
            var breakdown = ScoringEngine.Score(Match(100, 2), 1, null, EffectContext.Empty,
                Verdict(3, 0, 1, 0));

            Assert.AreEqual(5, breakdown.TotalMult, 1e-9);
            Assert.AreEqual(500, breakdown.FinalScore, 1e-9);
        }

        [Test]
        public void CleanServe_MultipliesLast()
        {
            var breakdown = ScoringEngine.Score(Match(100, 2), 1, null, EffectContext.Empty,
                Verdict(3, 0, 2, 0));

            Assert.AreEqual(10, breakdown.TotalMult, 1e-9, "(2 + 3) × 2");
        }

        [Test]
        public void BustPenalty_NeverDrivesMultBelowOne()
        {
            var breakdown = ScoringEngine.Score(Match(100, 2), 1, null, EffectContext.Empty,
                Verdict(0, 0, 1, 10, BustKind.Overshoot));

            Assert.AreEqual(ScoringEngine.MinMult, breakdown.TotalMult, 1e-9);
        }

        [Test]
        public void NoResonance_ScoresExactlyAsBefore()
        {
            var withNull = ScoringEngine.Score(Match(100, 4), 1, null, EffectContext.Empty, null);
            var legacy = ScoringEngine.Score(Match(100, 4), 1);

            Assert.AreEqual(legacy.FinalScore, withNull.FinalScore, 1e-9);
            Assert.AreEqual(legacy.Steps.Count, withNull.Steps.Count);
        }
    }
}
