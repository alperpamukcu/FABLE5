using System;

namespace LastCall.Core
{
    /// <summary>
    /// Everything a bottle can tell you about itself (GDD 22): the brand, where it is from,
    /// how strong it is, what kind of drink it is. Pure flavour text today; the bottle-info
    /// popup and the dialogue system will read from here later, which is why it is a proper
    /// model instead of a description string.
    ///
    /// <see cref="Style"/> and <see cref="Tier"/> are the two fields that are already
    /// load-bearing: the market sells brand upgrades *by style* ("a better vodka replaces
    /// your vodka"), so every bottle must say what it is a brand *of*.
    /// </summary>
    public sealed class IngredientInfo
    {
        /// <summary>What this is a brand of: "vodka", "gin", "soda", "mint"… (market key).</summary>
        public string Style { get; }

        /// <summary>Brand quality rung. Tier 1 is the starting well; higher tiers are market goods.</summary>
        public int Tier { get; }

        /// <summary>Market price. Meaningless for tier-1 bottles, which you start with.</summary>
        public int Price { get; }

        /// <summary>Where the bottle says it comes from.</summary>
        public string Origin { get; }

        /// <summary>Alcohol by volume, 0 for mixers and garnishes. Display only — the tone
        /// guardrail means strength must never feed scoring.</summary>
        public double Abv { get; }

        /// <summary>One line of character for the info popup and, later, dialogue.</summary>
        public string Blurb { get; }

        public IngredientInfo(string style, int tier = 1, int price = 0,
            string origin = null, double abv = 0, string blurb = null)
        {
            if (string.IsNullOrWhiteSpace(style))
                throw new ArgumentException("A bottle must say what it is a brand of.", nameof(style));
            if (tier < 1) throw new ArgumentOutOfRangeException(nameof(tier));
            if (abv < 0 || abv > 100) throw new ArgumentOutOfRangeException(nameof(abv));
            Style = style;
            Tier = tier;
            Price = price;
            Origin = origin ?? string.Empty;
            Abv = abv;
            Blurb = blurb ?? string.Empty;
        }

        public override string ToString() => $"{Style} T{Tier} ({Origin}, {Abv:0.#}%)";
    }
}
