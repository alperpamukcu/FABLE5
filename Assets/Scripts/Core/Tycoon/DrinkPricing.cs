using System;

namespace LastCall.Core
{
    /// <summary>
    /// WHAT A DRINK IS WORTH (2026-09-23, the author's economy brief).
    ///
    /// > *"Kokteyl fiyatları yıldızlara göre değişmeli, ekonomiyi en baştan tekrar oluşturmalıyız.
    /// > Her yıldız seviyesinin kolay-orta-zor kokteylleri olacak ... KOLAY/ORTA/ZOR üçlüsünde aynı
    /// > yıldız grubunda hepsinin fiyatı aynı olmayacak ama gruplar arası fiyat farkı olacak."*
    ///
    /// The price was <c>3 + (rank + 1) / 2</c> — one straight line through all fifty-four pages —
    /// and then everything the bar did was multiplied by <c>1 + floor(stars)</c>, so a night's take
    /// climbed because the STANDING climbed rather than because the menu did. Two things were wrong
    /// with that. The book printed the sheet while the drinker paid three times it, so the one
    /// number the page is about was a lie from two stars up; and a bar could stand still on its
    /// opening menu and still earn more every rung.
    ///
    /// Now the price belongs to the DRINK: the rung the page opens on, crossed with how hard it is
    /// to make. A Vodka &amp; Soda is a four-dollar drink at five stars exactly as it is at none —
    /// the way to earn more is to serve something better, which is the loop the brief asks for. The
    /// crowd's mood and the premium spirits on the shelf still ride on top (TycoonRun.PriceOf), and
    /// the stage multiplier does NOT: the band already carries the stage, and two of them on one
    /// number is how a four-dollar drink becomes a twenty-four-dollar drink by accident.
    /// </summary>
    public static class DrinkPricing
    {
        /// <summary>
        /// The six rungs a page can open on, and what the three kinds of work are worth on each.
        ///
        /// The zero-star row is the author's own figures, kept to the dollar (*"0 yıldız: Kolay
        /// 3-6$ / Orta 7-10$ / Zor 11-12$"*). Above it each band is about half again the one under
        /// it, decelerating — so the two sides of the book keep pace without the top running away.
        ///
        /// Inside a band the exact figure comes from the page's RANK, so the drinks of one rung are
        /// not all the same price (the brief asks for exactly this) and the order inside the rung
        /// is the book's own order — a harder page in the band is the dearer page in the band.
        ///
        /// RE-CUT AT ×1.5 A RUNG, NOT ×2 (2026-09-23, the author: "bir kokteyl 400 dolar
        /// olmamalı"). The first cut doubled a rung at a time and the top page reached $470 on the
        /// sheet — and once the crowd, the shelf premium and a flawless tip were on it, one
        /// customer handed over a thousand dollars. A gentler climb keeps the whole ladder legible:
        /// the dearest ticket the shipped book can print is now about $355, the easy-to-hard spread
        /// inside a rung is a constant ×1.78 instead of collapsing from ×2.6 to ×1.7, and a rung's
        /// hard top never runs past the next rung's medium top — so buying up is always worth more
        /// than grinding the hard pages of the rung you are on.
        /// </summary>
        private static readonly (int Lo, int Hi)[,] Bands =
        {
            //  easy          medium        hard
            { (3,   6),    (7,   10),    (11,  12) },    // 0★  ranks 1–8   (the brief's own row)
            { (9,  12),    (13,  16),    (17,  20) },    // 1★  ranks 9–11
            { (14, 18),    (19,  25),    (26,  32) },    // 2★  ranks 12–14
            { (21, 27),    (29,  37),    (39,  48) },    // 3★  ranks 15–21
            { (32, 41),    (44,  55),    (58,  72) },    // 4★  ranks 22–29
            { (48, 60),    (64,  80),    (86, 105) },    // 5★  rank 30
        };

        /// <summary>The rank each rung starts and ends at — the same split
        /// <c>TycoonRun.RecipeStarGate</c> makes, kept here so the band can place a page INSIDE
        /// its rung. One table, read twice, rather than two that can drift.</summary>
        private static readonly (int Lo, int Hi)[] Ranks =
        {
            (1, 8), (9, 11), (12, 14), (15, 21), (22, 29), (30, 30),
        };

        /// <summary>Which rung a page opens on, as an index into the tables above. Pure, so the
        /// price can be asked without a run — the book, the shop and the tests all do.</summary>
        public static int RungOf(RecipeDefinition recipe)
        {
            int rank = recipe.Rank;
            for (int i = 0; i < Ranks.Length; i++)
                if (rank <= Ranks[i].Hi) return i;
            return Ranks.Length - 1;
        }

        /// <summary>The stars that rung asks for — 0, 1, 2, 3, 4, 5.</summary>
        public static double StarsOfRung(int rung) => Math.Max(0, Math.Min(Ranks.Length - 1, rung));

        /// <summary>
        /// What the page is worth before its character moves it: the rung's band for its kind of
        /// work, and the page's own place inside that band by rank.
        /// </summary>
        public static int SheetPrice(RecipeDefinition recipe)
        {
            if (recipe == null) return 0;
            int rung = RungOf(recipe);
            int work = (int)RecipeDifficulty.Of(recipe) - 1;          // Easy 0, Medium 1, Hard 2
            if (work < 0) work = 0;
            if (work > 2) work = 2;
            var band = Bands[rung, work];
            var ranks = Ranks[rung];

            int span = Math.Max(1, ranks.Hi - ranks.Lo);
            int step = Math.Max(0, Math.Min(span, recipe.Rank - ranks.Lo));
            return band.Lo + (int)Math.Round(
                (band.Hi - band.Lo) * (step / (double)span), MidpointRounding.AwayFromZero);
        }

        /// <summary>The cheapest and dearest a rung can sell for, for anything that wants to say
        /// what a rung is worth without naming a drink (the certificate, the market's aisles).</summary>
        public static (int Lo, int Hi) BandOfRung(int rung, DrinkDifficulty work)
        {
            int w = Math.Max(0, Math.Min(2, (int)work - 1));
            var band = Bands[Math.Max(0, Math.Min(Bands.GetLength(0) - 1, rung)), w];
            return (band.Lo, band.Hi);
        }

        /// <summary>How many rungs the table carries. The tests walk it.</summary>
        public static int Rungs => Bands.GetLength(0);

        /// <summary>
        /// THE MOST ONE CUSTOMER MAY EVER HAND OVER (2026-09-23, the author: "bir kokteyl 400 dolar
        /// olmamalı"). The bands above are cut so the shipped book cannot come near it — the worst
        /// real ticket is a Long Island Iced Tea at about $355, menu plus the shelf's premium,
        /// times a high-rolling crowd, with a flawless tip on top. This is the backstop for what is
        /// NOT shipped: a future five-star hard page at the band's ceiling, carrying a buff notch
        /// and a tip character, with four top-shelf spirits in it, computes to $493. The rule is
        /// written down so no page, trait or premium can walk past it by accident.
        /// </summary>
        public const int CeilingPerDrink = 399;

        /// <summary>
        /// THE TWO PAGES WITH NOTHING TO GRADE. A pint and a neat pour have no authored bands — a
        /// pint's craft is its head and a neat pour has no proportions — so the rank table would
        /// price both at the bottom of rung 0 for ever. Their price rides the BAR's rung instead of
        /// the page's, which is the only way the keg stays in the economy as the house climbs.
        /// </summary>
        public static int HousePourPrice(RecipeDefinition recipe, double barStars)
        {
            int rung = (int)Math.Max(0, Math.Min(5, Math.Floor(barStars)));
            return recipe != null && recipe.Id == "draught" ? 3 + 2 * rung : 4 + 3 * rung;
        }

        /// <summary>Whether this page is one of the two the house pours by hand rather than by
        /// ratio — the pint and the neat pour.</summary>
        public static bool IsHousePour(RecipeDefinition recipe) =>
            recipe != null && !recipe.HasAuthoredRatios;
    }
}
