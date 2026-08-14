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

        /// <summary>Which menu aisle this belongs to (v5 P10) — one of
        /// <see cref="IngredientCategories.All"/>, or empty on legacy unbranded cards.</summary>
        public string Category { get; }

        /// <summary>Fizzy in the bottle (v5 P10). Carbonated things are built at the serving
        /// glass and never shaken — shaking one is a mess, not a technique — and the rule is
        /// enforced by the run's shaker verbs, the same way beer is kept out of the tin.</summary>
        public bool Carbonated { get; }

        /// <summary>
        /// WHAT THIS BOTTLE IS WAITING FOR, when it is waiting for something a tier and a
        /// price cannot say (GDD 26 §12.2 step 4, 2026-08-14, the author: "bazı alkoller bazı
        /// yükseltmeler ... müşteriye doğru siparişi vererek kilit açılacak").
        ///
        /// Null for every bottle the market gates on the standing alone, which is all of them
        /// today. It exists so the alengirli spirits a last call asks for can be earned from
        /// the guest who asked rather than bought off a number — and because the REWARD for
        /// keeping a night is precisely this lock coming off (the author, 2026-08-14), which
        /// is why the arc pushes nothing and the bottle names the night instead.
        /// </summary>
        public UnlockCondition Unlock { get; }

        /// <summary>The written night this bottle is earned from, or null — kept beside the
        /// condition so the loader can check the id against the arc.</summary>
        public string UnlockBeatId { get; }

        public IngredientInfo(string style, int tier = 1, int price = 0,
            string origin = null, double abv = 0, string blurb = null,
            string category = null, bool carbonated = false,
            UnlockCondition unlock = null, string unlockBeatId = null)
        {
            if (string.IsNullOrWhiteSpace(style))
                throw new ArgumentException("A bottle must say what it is a brand of.", nameof(style));
            if (tier < 1) throw new ArgumentOutOfRangeException(nameof(tier));
            if (abv < 0 || abv > 100) throw new ArgumentOutOfRangeException(nameof(abv));
            if (!string.IsNullOrEmpty(category) && !IngredientCategories.IsKnown(category))
                throw new ArgumentException($"Unknown category '{category}' for style '{style}'.", nameof(category));
            Style = style;
            Tier = tier;
            Price = price;
            Origin = origin ?? string.Empty;
            Abv = abv;
            Blurb = blurb ?? string.Empty;
            Category = category ?? string.Empty;
            Carbonated = carbonated;
            Unlock = unlock is null || ReferenceEquals(unlock, UnlockCondition.Open) ? null : unlock;
            UnlockBeatId = string.IsNullOrWhiteSpace(unlockBeatId) ? null : unlockBeatId;
        }

        public override string ToString() => $"{Style} T{Tier} ({Origin}, {Abv:0.#}%)";
    }
}
