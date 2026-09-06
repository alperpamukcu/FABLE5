using System;

namespace LastCall.Core
{
    /// <summary>
    /// THE STAR ECONOMY (2026-09-06, the author: "Ekonomik gelişim sürekli katlanarak
    /// artmalı. Örneğin 0 yıldızda her şeyin fiyatı (geliştirmeler, tarifler, müşterilerin
    /// kokteyllere ödediği ücretler) x iken, 1 yıldız aşamasındaki nesnelerde bu 2x, 2 yıldız
    /// aşamasında 3x diye gitmeli, oyuncu sürekli gelişiyor hissi verilmeli").
    ///
    /// One multiplier, read two ways:
    ///   · a THING is priced at the stage it belongs to — a recipe at its star gate, a fitting
    ///     at its star requirement, a bottle at the rung the market sells it on — so the
    ///     one-star shelf costs twice the sheet's figure and the two-star shelf three times;
    ///   · a NIGHT is paid at the stage the bar has reached — what a drinker pays for a drink
    ///     and what the landlord asks for the room both follow the standing — so the numbers
    ///     on both sides of the book climb together, and climbing is what the player feels.
    ///
    /// The stage is the whole star: 0.9 is still the first stage, 1.0 the second. The sheet's
    /// figures (fixtures.json, RecipePrice's rank curve, Market.StockPrice) are the "x" —
    /// stage-zero prices — and are never edited to fit this; the multiplier is applied where
    /// money moves, never stored.
    /// </summary>
    public static class StarEconomy
    {
        /// <summary>How many times the sheet's figure a stage costs and pays: 1 + the whole
        /// stars, held to the five the standing can reach.</summary>
        public static double TierMultiplier(double stars)
        {
            if (double.IsNaN(stars) || stars < 0) stars = 0;
            if (stars > BarRating.MaxStars) stars = BarRating.MaxStars;
            return 1.0 + Math.Floor(stars);
        }

        /// <summary>A sheet figure at a stage, rounded to the dollar (half up).</summary>
        public static int PriceAt(int basePrice, double stars) =>
            (int)Math.Round(basePrice * TierMultiplier(stars), MidpointRounding.AwayFromZero);
    }
}
