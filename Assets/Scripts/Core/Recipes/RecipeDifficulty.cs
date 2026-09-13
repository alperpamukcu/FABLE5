namespace LastCall.Core
{
    /// <summary>How hard a drink is to make, in the three steps the book and the market show.</summary>
    public enum DrinkDifficulty
    {
        Easy = 1,     // green
        Medium = 2,   // orange
        Hard = 3,     // red
    }

    /// <summary>
    /// HOW HARD A DRINK IS (2026-09-13, the author: "kokteyllere zorluk seviyesi ekleyelim,
    /// örneğin çok malzeme isteyen zaman alan kokteyller 3. seviye kırmızı zorluk, daha azı
    /// turuncu, daha azı yeşil").
    ///
    /// Read off what the drink ASKS of the bartender, not written beside it: every pour is a
    /// bottle picked up, tipped and landed in its box, and a shaken or stirred drink is a whole
    /// verb more on top of the pours. So the work is the pours plus one for a worked method, and
    /// the steps fall where the book's 54 drinks fall — the built two- and three-pour drinks are
    /// easy (20 of them), a worked three-pour is medium (22), and four worked pours or five of
    /// anything is hard (12). Derived rather than authored so it can never disagree with the
    /// recipe it describes; a drink that gains a pour becomes harder by itself.
    /// </summary>
    public static class RecipeDifficulty
    {
        /// <summary>The work at which a drink stops being easy, and the work at which it is hard.</summary>
        public const int MediumFrom = 4, HardFrom = 5;

        /// <summary>The pours, and one more for a drink that has to be shaken or stirred.</summary>
        public static int Work(RecipeDefinition recipe)
        {
            if (recipe == null) return 0;
            int pours = recipe.RatioRequirements != null ? recipe.RatioRequirements.Count : 0;
            return pours + (recipe.Prep == PrepMethod.Built ? 0 : 1);
        }

        public static DrinkDifficulty Of(RecipeDefinition recipe)
        {
            int work = Work(recipe);
            return work >= HardFrom ? DrinkDifficulty.Hard
                 : work >= MediumFrom ? DrinkDifficulty.Medium
                 : DrinkDifficulty.Easy;
        }
    }
}
