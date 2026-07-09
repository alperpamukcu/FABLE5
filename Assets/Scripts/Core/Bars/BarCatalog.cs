using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    /// <summary>A card the bar adds to the starting cabinet.</summary>
    public sealed class CardSpec
    {
        public IngredientType Type { get; }
        public int Flavor { get; }

        public CardSpec(IngredientType type, int flavor)
        {
            Type = type;
            Flavor = flavor;
        }
    }

    /// <summary>
    /// A starting configuration (GDD 9, Balatro deck equivalent): tweaks to money,
    /// cabinet contents, starting roster and recipe levels applied at run start.
    /// </summary>
    public sealed class BarDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int MoneyDelta { get; }
        public int RandomRarePatrons { get; }
        public IReadOnlyList<CardSpec> ExtraCards { get; }
        public IReadOnlyDictionary<string, int> RecipeLevels { get; }

        public BarDefinition(string id, string name, string description,
            int moneyDelta = 0, int randomRarePatrons = 0,
            IReadOnlyList<CardSpec> extraCards = null,
            IReadOnlyDictionary<string, int> recipeLevels = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Bar id is required", nameof(id));
            Id = id;
            Name = name;
            Description = description ?? string.Empty;
            MoneyDelta = moneyDelta;
            RandomRarePatrons = randomRarePatrons;
            ExtraCards = extraCards ?? Array.Empty<CardSpec>();
            RecipeLevels = recipeLevels ?? new Dictionary<string, int>();
        }
    }

    /// <summary>The three launch Bars (GDD 9 / M3). The Dive and Hotel Lobby come later.</summary>
    public static class BarCatalog
    {
        public static IReadOnlyList<BarDefinition> CreateDefault()
        {
            return new[]
            {
                new BarDefinition("classic", "The Classic",
                    "Standard 48-card cabinet."),

                new BarDefinition("speakeasy", "The Speakeasy",
                    "Start with 1 random Rare Patron; -$2 starting money.",
                    moneyDelta: -2, randomRarePatrons: 1),

                new BarDefinition("tiki_hut", "Tiki Hut",
                    "+4 Garnish cards; Tiki starts at level 2.",
                    extraCards: new[]
                    {
                        new CardSpec(IngredientType.Garnish, 4),
                        new CardSpec(IngredientType.Garnish, 6),
                        new CardSpec(IngredientType.Garnish, 8),
                        new CardSpec(IngredientType.Garnish, 10)
                    },
                    recipeLevels: new Dictionary<string, int> { ["tiki"] = 2 })
            };
        }

        /// <summary>Case-insensitive lookup; unknown or empty ids fall back to The Classic.</summary>
        public static BarDefinition Find(IReadOnlyList<BarDefinition> bars, string id)
        {
            if (bars == null || bars.Count == 0) throw new ArgumentException("No bars", nameof(bars));
            return bars.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase))
                   ?? bars[0];
        }
    }
}
