using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Result of identifying a poured glass: the recipe it reads as (or null), plus the
    /// ingredients that count and their share of the drink. The tycoon loop reads only
    /// <see cref="Recipe"/> — the weights survive from the pour pivot so a future scoring
    /// pass could weight Flavor by share again without a rework.
    /// </summary>
    public sealed class RecipeMatch
    {
        public RecipeDefinition Recipe { get; }
        public IReadOnlyList<IngredientCard> ScoredCards { get; }
        public IReadOnlyList<double> ScoredWeights { get; }

        public RecipeMatch(RecipeDefinition recipe, IReadOnlyList<IngredientCard> scoredCards,
            IReadOnlyList<double> scoredWeights = null)
        {
            Recipe = recipe;
            ScoredCards = scoredCards;
            ScoredWeights = scoredWeights;
        }

        /// <summary>The weight of the card at <paramref name="index"/>; 1 when unweighted.</summary>
        public double WeightAt(int index) =>
            ScoredWeights != null && index >= 0 && index < ScoredWeights.Count
                ? ScoredWeights[index]
                : 1.0;
    }

    /// <summary>
    /// One ingredient's allowed share of a recipe (GDD 21 §9). Bounds are inclusive: a
    /// Martini specified at 55–75% gin accepts exactly 55% and exactly 75%, because a band
    /// the player can see should not have invisible slivers cut off its ends.
    /// </summary>
    public readonly struct RatioRequirement
    {
        /// <summary>
        /// The ingredient type this band constrains. Bands are by *type*, not by a specific
        /// bottle: "a Spritz is 40–60% Spirit and 40–60% Bubbly" generalises across the shelf
        /// exactly the way the card-era pattern did, and it means adding a new gin does not
        /// mean editing every recipe. A named-bottle band (a true Martini insisting on gin) is
        /// a later refinement, not something the table needs yet.
        /// </summary>
        public IngredientType Type { get; }
        public double MinRatio { get; }
        public double MaxRatio { get; }

        public RatioRequirement(IngredientType type, double minRatio, double maxRatio)
        {
            if (minRatio < 0 || maxRatio > 1 || maxRatio < minRatio)
                throw new ArgumentException($"Bad ratio band {minRatio}–{maxRatio} for {type}.");
            Type = type;
            MinRatio = minRatio;
            MaxRatio = maxRatio;
        }

        public bool Accepts(double ratio) => ratio >= MinRatio && ratio <= MaxRatio;

        public override string ToString() => $"{Type} {MinRatio:P0}–{MaxRatio:P0}";
    }

    /// <summary>
    /// Matches a poured glass against the recipe table by proportion instead of card count
    /// (GDD 21 §9).
    ///
    /// Recipes are a **bonus, not a gate**. Serving the emotionally correct drink satisfies
    /// the customer whether or not it happens to be a Martini; matching one pays Flavor, Mult
    /// and the charge multiplier on top. Inverting that would put the craft layer back in
    /// charge of a game that is now about reading people.
    /// </summary>
    public static class RatioRecipeMatcher
    {
        /// <summary>
        /// The best (highest-rank) recipe whose bands the glass satisfies, or null.
        ///
        /// The match carries the glass's ingredients as scored cards, weighted by their share
        /// of the drink. That is what keeps card Flavor values, quality tiers and enhancements
        /// meaningful under pouring: a drink that is 70% vodka scores 70% of vodka's Flavor.
        /// </summary>
        public static RecipeMatch Match(GlassContents glass, IReadOnlyList<RecipeDefinition> recipes,
            Func<string, IngredientCard> lookup = null)
        {
            if (glass == null || glass.IsEmpty || recipes == null) return null;

            var byType = RatiosByType(glass, lookup);
            RecipeDefinition best = null;
            foreach (var recipe in recipes)
            {
                if (recipe.RatioRequirements == null || recipe.RatioRequirements.Count == 0) continue;
                if (!Satisfies(glass, byType, recipe)) continue;
                if (best == null || recipe.Rank > best.Rank) best = recipe;
            }
            if (best == null) return null;

            var (cards, weights) = ScoredContents(glass, lookup);
            return new RecipeMatch(best, cards, weights);
        }

        /// <summary>
        /// The glass's distinct ingredients in pour order, with each one's share of the drink.
        /// Merged per ingredient, so a bottle returned to twice scores once at its total share
        /// rather than twice at half-weight — which would double its Mult effects.
        /// </summary>
        public static (IReadOnlyList<IngredientCard> cards, IReadOnlyList<double> weights)
            ScoredContents(GlassContents glass, Func<string, IngredientCard> lookup)
        {
            var cards = new List<IngredientCard>();
            var weights = new List<double>();
            if (glass == null || lookup == null || glass.IsEmpty) return (cards, weights);

            var seen = new HashSet<string>();
            foreach (var pour in glass.Pours)
            {
                if (!seen.Add(pour.IngredientId)) continue;
                var card = lookup(pour.IngredientId);
                if (card == null) continue;
                cards.Add(card);
                weights.Add(glass.RatioOf(pour.IngredientId));
            }
            return (cards, weights);
        }

        /// <summary>Share of the drink contributed by each ingredient type.</summary>
        public static Dictionary<IngredientType, double> RatiosByType(
            GlassContents glass, Func<string, IngredientCard> lookup)
        {
            var byType = new Dictionary<IngredientType, double>();
            if (glass == null || lookup == null || glass.TotalVolume <= 0) return byType;

            foreach (var pour in glass.Pours)
            {
                var card = lookup(pour.IngredientId);
                if (card == null) continue;
                byType.TryGetValue(card.Type, out double share);
                byType[card.Type] = share + pour.Volume / glass.TotalVolume;
            }
            return byType;
        }

        private static bool Satisfies(GlassContents glass,
            IReadOnlyDictionary<IngredientType, double> byType, RecipeDefinition recipe)
        {
            if (glass.FillFraction < recipe.MinFill) return false;

            double accounted = 0;
            foreach (var requirement in recipe.RatioRequirements)
            {
                byType.TryGetValue(requirement.Type, out double ratio);
                if (!requirement.Accepts(ratio)) return false;
                accounted += ratio;
            }

            // Anything the recipe does not name is a stray. A splash is fine; a third of the
            // glass means this is a different drink wearing the recipe's proportions.
            return 1.0 - accounted <= MaxUnnamedShare + 1e-9;
        }

        /// <summary>How much of the glass may be ingredients the recipe never mentions.
        /// Loosened 0.10 → 0.15 (2026-07-20): a garnish pinch plus a splash should not
        /// knock a drink out of its recipe.</summary>
        public const double MaxUnnamedShare = 0.15;

        /// <summary>
        /// Derives ratio bands from a card-era recipe's type pattern (GDD 21 §9).
        ///
        /// "2 Spirit + 1 Bubbly" becomes "Spirit around 2/3, Bubbly around 1/3", with a
        /// tolerance either side so pouring by eye is possible. Deriving rather than
        /// hand-authoring is deliberate: fourteen recipes hand-written as bands is fourteen
        /// chances to author something unmatchable — ratios must sum to 1, and my first
        /// attempt at a Martini did not (see PourSystemTests.BandsMustAdmitAValidDrink).
        ///
        /// Recipes with no type pattern (the value-axis and mono-type ones) get no bands and
        /// simply cannot be poured yet; they need their own design pass.
        /// </summary>
        // Tolerance loosened 0.15 → 0.20 (2026-07-20): free-hand pouring on a held button
        // lands within ±10% at best, so ±15 made recipes a precision test instead of a
        // judgement call. Wider bands mean neighbouring recipes overlap more; the matcher's
        // rank order decides those, which is what rank is for.
        public static IReadOnlyList<RatioRequirement> DeriveBands(RecipeDefinition recipe,
            double tolerance = 0.20)
        {
            if (recipe?.Requirements == null || recipe.Requirements.Count == 0)
                return Array.Empty<RatioRequirement>();

            // Only derive when the type pattern *is* the whole recipe. Perfect Serve and
            // Double Perfect list a single Spirit slot but are really "five distinct types"
            // and "five distinct types at one Flavor value" — deriving from their partial
            // pattern produced "Spirit 85-100%", which made a glass of neat whisky score as
            // the top recipe in the table. Constraints that have no proportional meaning
            // (distinct types, equal Flavor, ascending Flavor, mono-type group size) leave a
            // recipe unpourable until it gets hand-authored bands.
            if (recipe.AllDistinctTypes || recipe.AllEqualFlavor ||
                recipe.EqualFlavorGroupSize > 0 || recipe.AscendingFlavorGroupSize > 0 ||
                recipe.SameTypeGroupMin > 0)
                return Array.Empty<RatioRequirement>();

            // A slot listing several acceptable types (Martini's "Bitter or Garnish") cannot
            // become a single type band, so those recipes stay unpourable for now.
            foreach (var slot in recipe.Requirements)
                if (slot.Types.Count != 1) return Array.Empty<RatioRequirement>();

            var totals = new Dictionary<IngredientType, int>();
            int slots = 0;
            foreach (var slot in recipe.Requirements)
            {
                totals.TryGetValue(slot.Types[0], out int count);
                totals[slot.Types[0]] = count + slot.Count;
                slots += slot.Count;
            }
            if (slots == 0) return Array.Empty<RatioRequirement>();

            var bands = new List<RatioRequirement>(totals.Count);
            foreach (var pair in totals)
            {
                double share = (double)pair.Value / slots;
                bands.Add(new RatioRequirement(pair.Key,
                    Math.Max(0, share - tolerance),
                    Math.Min(1, share + tolerance)));
            }
            return bands;
        }
    }
}
