using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Turns a mix into emotional movement (GDD 19 §5). Pure: same cards and recipe in,
    /// same delta out. The craft layer is the amplifier here — a well-made drink carries
    /// what you put in it further, and a mix that isn't a recipe still says something,
    /// just at half volume.
    /// </summary>
    public static class EmotionResolver
    {
        /// <summary>A mix that matches no recipe still pours, at half strength (GDD 19 §5, D5).</summary>
        public const double NoRecipeMultiplier = 0.5;

        /// <summary>Ceiling on how far the craft layer can amplify charges.</summary>
        public const double MaxChargeMultiplier = 3.0;

        /// <summary>Raw, unamplified sum of every charge printed on the selection.</summary>
        public static EmotionDelta RawCharges(IReadOnlyList<IngredientCard> selection)
        {
            var delta = new EmotionDelta();
            if (selection == null) return delta;
            foreach (var card in selection)
            {
                if (card?.Charges == null) continue;
                foreach (var charge in card.Charges) delta.Add(charge.Emotion, charge.Amount);
            }
            return delta;
        }

        /// <summary>
        /// The multiplier the craft layer contributes. Derived from the recipe's base Mult so
        /// authoring a recipe never means authoring two numbers that can drift apart.
        /// </summary>
        public static double MultiplierFor(RecipeMatch match)
        {
            if (match?.Recipe == null) return NoRecipeMultiplier;
            return Math.Min(MaxChargeMultiplier, match.Recipe.ChargeMultiplier);
        }

        /// <summary>
        /// Full resolution: sum the charges, amplify by the craft layer, round once at the
        /// end. Rounding once (rather than per card) is what keeps exact landings on 0 and
        /// 100 reachable — see GDD 19 §5, D6.
        /// </summary>
        /// <remarks>
        /// Voided mixes never reach this method — the round layer skips resolution entirely,
        /// because a drink cancelled by a VIP rule was never set down in front of anyone.
        /// </remarks>
        public static EmotionDelta Resolve(IReadOnlyList<IngredientCard> selection, RecipeMatch match)
        {
            if (selection == null || selection.Count == 0) return EmotionDelta.Empty;
            return RawCharges(selection).Scaled(MultiplierFor(match));
        }
    }
}
