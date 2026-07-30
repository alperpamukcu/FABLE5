using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// The menu's aisles (v5 P10). Categories are how the player browses — "the vodka shelf",
    /// "the mixers" — where <see cref="IngredientType"/> is how recipes mix. The two are
    /// deliberately separate axes: bourbon and tequila are both Spirit to a recipe, but nobody
    /// looks for tequila on the whiskey shelf.
    ///
    /// The set is closed and validated loudly at load: a typo in a data file must fail there,
    /// not render an empty shelf tab.
    /// </summary>
    public static class IngredientCategories
    {
        public const string Vodka = "vodka";
        public const string Gin = "gin";
        public const string Rum = "rum";
        public const string Whiskey = "whiskey";
        public const string Tequila = "tequila";
        public const string Liqueur = "liqueur";
        public const string Juice = "juice";
        public const string Mixer = "mixer";
        public const string Garnish = "garnish";
        public const string Beer = "beer";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Vodka, Gin, Rum, Whiskey, Tequila, Liqueur, Juice, Mixer, Garnish, Beer,
        };

        private static readonly HashSet<string> Known = new HashSet<string>(All, StringComparer.Ordinal);

        public static bool IsKnown(string category) =>
            !string.IsNullOrEmpty(category) && Known.Contains(category);
    }
}
