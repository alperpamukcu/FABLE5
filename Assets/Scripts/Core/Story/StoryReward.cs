using System;

namespace LastCall.Core
{
    /// <summary>
    /// What a kept beat pays out (GDD 26 §6). Every kind of it lands in a system the player
    /// already reads — the till, the standing, the book, the shelf — because a reward the
    /// player cannot find is a flag, and this module does not add flags.
    ///
    /// Core carries the ids, not the things: paying out is <see cref="TycoonRun"/>'s job, and
    /// the ids are checked against the real catalogues when the arc is built.
    /// </summary>
    public sealed class StoryReward
    {
        /// <summary>A beat that pays in nothing but what the drink was worth.</summary>
        public static readonly StoryReward None = new StoryReward();

        /// <summary>Straight into the till, on top of the price of the drink.</summary>
        public int Money { get; }

        /// <summary>Added to the night's standing — a quarter star is a lot here.</summary>
        public double Stars { get; }

        /// <summary>A page for the book: the recipe id this beat hands over.</summary>
        public string RecipeId { get; }

        /// <summary>A bottle left on the counter: an ingredient id.</summary>
        public string BottleId { get; }

        public bool IsNothing => Money == 0 && Stars <= 0
            && string.IsNullOrEmpty(RecipeId) && string.IsNullOrEmpty(BottleId);

        public StoryReward(int money = 0, double stars = 0, string recipeId = null,
            string bottleId = null)
        {
            if (money < 0) throw new ArgumentOutOfRangeException(nameof(money),
                "A beat pays out; it does not take a cut.");
            if (stars < 0) throw new ArgumentOutOfRangeException(nameof(stars),
                "A kept beat cannot cost standing — that is what missing it is for.");

            Money = money;
            Stars = stars;
            RecipeId = string.IsNullOrWhiteSpace(recipeId) ? null : recipeId;
            BottleId = string.IsNullOrWhiteSpace(bottleId) ? null : bottleId;
        }
    }
}
