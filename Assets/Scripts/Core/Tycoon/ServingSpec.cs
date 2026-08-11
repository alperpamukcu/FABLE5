using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// How a customer wants their drink served (v5 P11, GDD 23 §3.1). The order names a drink;
    /// this names everything else about it — ice, a rim, a twist. It is **stated**,
    /// printed on the licence for the player to read; the inferred-preference layer it once
    /// stood beside was demolished with the emotion machinery.
    ///
    /// Every request is gradeable and every miss costs tip rather than payment: a Gin Fizz
    /// served without the ice is still a Gin Fizz, and the customer still pays for it.
    ///
    /// THE SHAKE LEFT THE SPEC (2026-08-11, the author: "shaken olması müşterinin talebi
    /// değil tarifin talebi olmalı"). "Extra shaken" was a customer whim rolled at 25% —
    /// and it was the only place the mixing method was ever graded, while the RECIPE's own
    /// shaken/stirred went unread. That was backwards: how a drink is worked belongs to the
    /// book, and the judge grades it against <see cref="RecipeDefinition.Prep"/> now.
    /// </summary>
    public sealed class ServingSpec
    {
        public static readonly ServingSpec Plain = new ServingSpec(null);

        /// <summary>Ice, a lemon twist, a salted or sugared rim.</summary>
        public IReadOnlyList<PreparationDefinition> Garnishes { get; }

        /// <summary>How many separate things they asked for.</summary>
        public int RequestCount => Garnishes.Count;

        public bool IsPlain => RequestCount == 0;

        public ServingSpec(IReadOnlyList<PreparationDefinition> garnishes)
        {
            Garnishes = garnishes ?? Array.Empty<PreparationDefinition>();
        }

        /// <summary>The fill every order expects ("filled to the top" retired 2026-08-02,
        /// its machinery removed in the 2026-08-07 sweep). Under-filling costs;
        /// over-filling cannot happen — the glass stops at the brim.</summary>
        public const double NormalFill = 0.80;

        public double ExpectedFill => NormalFill;

        /// <summary>
        /// The share of this spec the delivered glass actually satisfies, 0–1. A plain order is
        /// satisfied by definition — asking for nothing cannot be got wrong.
        /// </summary>
        public double Delivered(GlassContents glass)
        {
            int total = RequestCount;
            if (total == 0) return 1.0;
            if (glass == null) return 0.0;

            int met = 0;
            foreach (var garnish in Garnishes)
                if (glass.HasPreparation(garnish.Id)) met++;
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

            // (The extra-shaken roll stood here until 2026-08-11. Its NextInt stays OUT —
            // the stream's numbers shift either way, and the sim re-baseline covers it.)

            if (garnishes.Count == 0) return Plain;
            return new ServingSpec(garnishes);
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
            return string.Join(", ", parts);
        }
    }
}
