using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// How a customer wants their drink served (v5 P11, GDD 23 §3.1). The order names a drink;
    /// this names everything else about it — ice, a rim, a twist, shaken hard, filled up.
    ///
    /// It replaces the bare garnish list the order used to carry. The distinction that matters:
    /// this is **stated**, printed on the licence for the player to read, where the emotion
    /// layer's <see cref="FillPreference"/> is **inferred** from a hidden read. Two different
    /// questions, deliberately not merged.
    ///
    /// Every request is gradeable and every miss costs tip rather than payment: a Gin Fizz
    /// served without the ice is still a Gin Fizz, and the customer still pays for it.
    /// </summary>
    public sealed class ServingSpec
    {
        public static readonly ServingSpec Plain = new ServingSpec(null, false, false);

        /// <summary>Ice, a lemon twist, a salted or sugared rim.</summary>
        public IReadOnlyList<PreparationDefinition> Garnishes { get; }

        /// <summary>They want it worked hard — only ever asked of a shaken recipe.</summary>
        public bool ExtraShaken { get; }

        /// <summary>They want the glass full, not a measure.</summary>
        public bool FilledToTheTop { get; }

        /// <summary>How many separate things they asked for.</summary>
        public int RequestCount => Garnishes.Count + (ExtraShaken ? 1 : 0) + (FilledToTheTop ? 1 : 0);

        public bool IsPlain => RequestCount == 0;

        public ServingSpec(IReadOnlyList<PreparationDefinition> garnishes,
            bool extraShaken = false, bool filledToTheTop = false)
        {
            Garnishes = garnishes ?? Array.Empty<PreparationDefinition>();
            ExtraShaken = extraShaken;
            FilledToTheTop = filledToTheTop;
        }

        /// <summary>Shake energy at or above this counts as "worked hard".</summary>
        public const double ExtraShakenEnergy = 0.6;

        /// <summary>The fill a plain order expects, and the fill a "to the top" order expects.
        /// Under-filling costs; over-filling cannot happen (the glass stops at the brim).</summary>
        public const double NormalFill = 0.80;
        public const double BrimFill = 0.95;

        public double ExpectedFill => FilledToTheTop ? BrimFill : NormalFill;

        /// <summary>
        /// The share of this spec the delivered glass actually satisfies, 0–1. A plain order is
        /// satisfied by definition — asking for nothing cannot be got wrong.
        /// </summary>
        public double Delivered(GlassContents glass, double shakeEnergy)
        {
            int total = RequestCount;
            if (total == 0) return 1.0;
            if (glass == null) return 0.0;

            int met = 0;
            foreach (var garnish in Garnishes)
                if (glass.HasPreparation(garnish.Id)) met++;
            if (ExtraShaken && glass.HasPreparation(Preparations.Shaken.Id)
                && shakeEnergy >= ExtraShakenEnergy) met++;
            if (FilledToTheTop && glass.FillFraction >= BrimFill) met++;
            return (double)met / total;
        }

        /// <summary>
        /// Rolls what this customer wants doing to this drink, from the options the recipe can
        /// actually honour (stream "orders"). A pint takes no garnish and cannot be shaken; a
        /// built drink never sees a shaker. Asking for something the recipe forbids would be an
        /// order nobody could fill — the one thing an order must never be.
        /// </summary>
        public static ServingSpec Roll(RecipeDefinition recipe, SeededRng rng)
        {
            if (recipe == null) return Plain;

            bool draught = IsDraught(recipe);

            // Roughly half of all orders are plain, as before the spec existed.
            bool wantsSomething = rng.NextInt(100) >= 50;
            if (!wantsSomething) return Plain;

            var garnishes = new List<PreparationDefinition>(2);
            if (!draught)
            {
                var bag = new List<PreparationDefinition>(GarnishPool);
                int count = rng.NextInt(100) < 65 ? 1 : 2;
                for (int i = 0; i < count && bag.Count > 0; i++)
                {
                    int k = rng.NextInt(bag.Count);
                    garnishes.Add(bag[k]);
                    bag.RemoveAt(k);
                }
            }

            bool extraShaken = !draught && recipe.Prep == PrepMethod.Shaken && rng.NextInt(100) < 25;

            // "Filled to the top" retired (the author, 2026-08-02): nobody DEMANDS a fill
            // any more. The only fill rule left is the house one — ExpectedFill's normal
            // pour — and it only ever punishes shorting the glass, never rewards topping.
            if (garnishes.Count == 0 && !extraShaken) return Plain;
            return new ServingSpec(garnishes, extraShaken);
        }

        /// <summary>The garnishes a customer can ask for (the four droppable preparations).</summary>
        public static readonly IReadOnlyList<PreparationDefinition> GarnishPool = new[]
        {
            Preparations.Ice, Preparations.LemonTwist, Preparations.SaltRim, Preparations.SugarRim,
        };

        private static bool IsDraught(RecipeDefinition recipe)
        {
            foreach (var band in recipe.RatioRequirements)
                if (!band.IsStyleBand && band.Type == IngredientType.Beer) return true;
            return false;
        }

        public override string ToString()
        {
            if (IsPlain) return "as it comes";
            var parts = new List<string>();
            foreach (var g in Garnishes) parts.Add(g.Name);
            if (ExtraShaken) parts.Add("extra shaken");
            if (FilledToTheTop) parts.Add("filled to the top");
            return string.Join(", ", parts);
        }
    }
}
