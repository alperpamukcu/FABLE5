using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Turns a poured glass into emotional movement (GDD 21 §4). The pour system's
    /// replacement for the selection-based path retired with the card loop.
    ///
    /// Charges are printed **per full glass**, and what actually went in scales them:
    ///
    ///     applied(e) = round( Σ charge_i(e) × (v_i / capacity) × chargeMultiplier )
    ///
    /// So a full glass of vodka delivers vodka's printed charges; half a glass delivers half;
    /// a full glass of 70% vodka / 30% lemon delivers 0.7 × vodka + 0.3 × lemon. That last
    /// case is the whole game — you read who is in front of you and pour the proportions that
    /// move their stats where they asked.
    /// </summary>
    public static class PourResolver
    {
        /// <summary>A drink that matches no recipe still says something, at half volume (GDD 19 §5).</summary>
        public const double NoRecipeMultiplier = 0.5;

        /// <summary>Ceiling on how far the craft layer can amplify charges (GDD 19 §5).</summary>
        public const double MaxChargeMultiplier = 3.0;

        /// <summary>One garnish tap = this share of the glass (GDD 21 §3, 2026-07-20).</summary>
        public const double GarnishClickFraction = 0.05;

        /// <summary>
        /// Raw, unamplified movement from what is in the glass. Ingredients are looked up by
        /// id through <paramref name="lookup"/>, so this stays pure and testable without a shelf.
        /// </summary>
        public static EmotionDelta RawCharges(GlassContents glass, Func<string, IngredientCard> lookup)
        {
            var delta = new EmotionDelta();
            if (glass == null || lookup == null || glass.IsEmpty) return delta;

            // Accumulate in floating point and round once at the very end: rounding per
            // ingredient would make exact landings on 0 and 100 unreachable, which is the
            // same rule the card system needed (GDD 19 D6).
            var totals = new double[Emotions.Count];
            foreach (var pour in glass.Pours)
            {
                var card = lookup(pour.IngredientId);
                if (card?.Charges == null) continue;

                double share = pour.Volume / glass.Capacity;
                foreach (var charge in card.Charges)
                    totals[(int)charge.Emotion] += charge.Amount * share;
            }

            for (int i = 0; i < totals.Length; i++)
                delta.Add(Emotions.All[i], (int)Math.Round(totals[i], MidpointRounding.AwayFromZero));
            return delta;
        }

        /// <summary>
        /// Full resolution: scale by volume, amplify by the craft layer, round once.
        /// The glass itself cannot overflow (GDD 21 §3), so what is in it is what serves.
        /// </summary>
        public static EmotionDelta Resolve(GlassContents glass, RecipeMatch match,
            Func<string, IngredientCard> lookup)
        {
            if (glass == null || glass.IsEmpty) return EmotionDelta.Empty;

            double multiplier = match?.Recipe == null
                ? NoRecipeMultiplier
                : Math.Min(MaxChargeMultiplier, match.Recipe.ChargeMultiplier);

            var totals = new double[Emotions.Count];
            foreach (var pour in glass.Pours)
            {
                var card = lookup(pour.IngredientId);
                if (card?.Charges == null) continue;

                double share = pour.Volume / glass.Capacity;
                foreach (var charge in card.Charges)
                    totals[(int)charge.Emotion] += charge.Amount * share * multiplier;
            }

            var delta = new EmotionDelta();
            for (int i = 0; i < totals.Length; i++)
                delta.Add(Emotions.All[i], (int)Math.Round(totals[i], MidpointRounding.AwayFromZero));
            return delta;
        }

        /// <summary>
        /// The bonus for handing someone the kind of drink they wanted to be holding
        /// (GDD 21 §5). Applied on top of <see cref="Resolve"/>; a miss adds nothing rather
        /// than taking anything away.
        /// </summary>
        public static EmotionDelta FillBonus(GlassContents glass, FillPreference preference,
            IntentDirection directionOfServedStat)
        {
            var delta = new EmotionDelta();
            if (glass == null || glass.IsEmpty) return delta;
            if (!preference.IsSatisfiedBy(glass.FillFraction)) return delta;

            int amount = directionOfServedStat == IntentDirection.Extinguish
                ? -preference.Reward
                : preference.Reward;
            delta.Add(preference.Serves, amount);
            return delta;
        }
    }
}
