using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    /// <summary>
    /// WHAT A NIGHT IS WORTH, WORKED OUT RATHER THAN GUESSED (2026-09-23, the author's economy
    /// brief: *"her gün ekonomiye göre nasıl siparişler gelecek oyuncu hangi gün ne kadar
    /// kazanabilecek bunların hesaplamalarını yap"*).
    ///
    /// The 200-run sim plays the loop with a bot and reports what happened. This does the other
    /// half: it computes, from the same constants the game reads, what a night SHOULD pay before
    /// anyone plays it — covers from the door's own arithmetic, the night's covers from
    /// <see cref="DayPlan"/>, the price from <see cref="DrinkPricing"/>, the tip from
    /// <see cref="ServiceJudge"/>'s ceiling, the rent from <see cref="TycoonConfig.Rent"/>. So a
    /// balance change can be READ before it is played, and the two can be held against each other.
    ///
    /// It is deliberately a projection and not a simulation: nobody walks out, nobody is refused,
    /// nothing is spilled. What it answers is *"a player of this standard, on this night, banks
    /// about this"* — which is the question the brief asks, and the one the sim cannot answer
    /// because its bot never shops.
    /// </summary>
    public static class EconomyProjection
    {
        /// <summary>One night, projected.</summary>
        public struct Night
        {
            public int Day;
            public double Stars;
            public int Covers;
            /// <summary>What the drinks ring up before tips — menu price, the shelf's premium and
            /// the crowd's mood.</summary>
            public int Gross;
            public int Tips;
            /// <summary>Stock burned: every cover fills a glass, and a glass of stock costs what
            /// the shelf's tier costs.</summary>
            public int Stock;
            public int Rent;
            public int Net;
            /// <summary>Running till, started at <see cref="TycoonConfig.StartingMoney"/>.</summary>
            public int Till;
            public int CheapestCover, DearestCover;
            public double AverageCover;
            /// <summary>What share of the night's covers were the hard column.</summary>
            public double HardShare;
        }

        /// <summary>
        /// HOW GOOD THE PLAYER IS, as the one number the judge actually multiplies by. A serve's
        /// quality is craft × the clock; 0.45 is somebody still learning the bench, 0.70 is
        /// competent, 0.88 is a player who knows the book. The tip is the whole of the difference —
        /// the bill is paid either way — which is why the ladder is worth printing.
        /// </summary>
        public const double Learning = 0.45, Competent = 0.70, Sharp = 0.88;

        /// <summary>
        /// AND WHAT THE ROOM THINKS OF THAT, which is a different number and decides a different
        /// thing. A serve's quality sets the TIP; the customers' satisfaction sets TOMORROW'S CROWD
        /// (<see cref="BarRating.CrowdFor"/> of <see cref="BarRating.ExactStarsFor"/>), and the
        /// crowd is a quarter either way on every bill.
        ///
        /// Read off ServiceJudge's own line for an exact match: 0.75, plus a tenth of the accuracy
        /// over half, plus a fifth of the craft over half, plus a little of the fill, less the wait.
        /// A player still learning lands near 0.68, a competent one near 0.78 and a sharp one near
        /// 0.87 — so nobody at these three standards ever draws a BROKE crowd, and only the sharp
        /// player draws the high rollers. Worth writing down, because the broke line reads a
        /// SATISFACTION of 0.125: it is the punishment for a dreadful night, not the state a new
        /// bar opens in. A first measurement of this projection said otherwise — it asked the
        /// crowd about the bar's STANDING, which starts at zero — and made the opening week look
        /// like a trap it is not.
        /// </summary>
        public static double SatisfactionFor(double quality) =>
            Math.Max(0.0, Math.Min(1.0, 0.49 + 0.43 * quality));

        /// <summary>
        /// THE STANDING A DECENT PLAYER HOLDS ON A GIVEN NIGHT. The rating moves by at most
        /// <see cref="BarRating.GainStep"/> a night, so five stars is thirty flawless nights at the
        /// very fastest; a real climb is slower because a night's stars are the LOWER of service and
        /// comfort, and comfort has to be bought. This is the curve the projection walks by default.
        /// </summary>
        public static double StarsOn(int day) => Math.Min(BarRating.MaxStars, (day - 1) * 0.135);

        /// <summary>The menu a bar at this standing can own: every unlocked page its rung reaches.
        /// An optimistic reading — the player has bought the recipes — and the honest ceiling for
        /// "what could this night pay".</summary>
        public static IReadOnlyList<RecipeDefinition> MenuAt(
            IReadOnlyList<RecipeDefinition> book, double stars) =>
            book.Where(r => r.RatioRequirements.Count > 0 && RankGate(r.Rank) <= stars + 1e-9).ToList();

        /// <summary>The same split TycoonRun.RecipeStarGate makes, without a run to ask.</summary>
        public static double RankGate(int rank) =>
            rank <= 8 ? 0.0 : rank <= 11 ? 1.0 : rank <= 14 ? 2.0 : rank <= 21 ? 3.0 : rank <= 29 ? 4.0 : 5.0;

        /// <summary>The shelf a bar at this standing pours from — one tier a star, held to four,
        /// which is what the market's rungs open.</summary>
        public static int TierAt(double stars) => Math.Max(1, Math.Min(4, 1 + (int)Math.Floor(stars)));

        /// <summary>
        /// Walks the nights. <paramref name="quality"/> is the player's standard (above);
        /// <paramref name="starsOn"/> is the climb, defaulting to <see cref="StarsOn"/>.
        /// <paramref name="houseOn"/> is the room the bar wears on a night at a standing
        /// (2026-09-23, the fitting buffs) — typically <see cref="HouseBuffs.FullRoomAt"/>. It
        /// reaches the price, the tip and the door's upper bound; the clock is the sim's to
        /// measure. Null is the bare room, and reproduces the tables before the buffs bit for bit.
        /// </summary>
        public static List<Night> Walk(IReadOnlyList<RecipeDefinition> book, TycoonConfig config,
            int days, double quality, Func<int, double> starsOn = null, string seed = "projection",
            Func<int, double, HouseBuffs> houseOn = null)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (config == null) throw new ArgumentNullException(nameof(config));
            starsOn = starsOn ?? StarsOn;

            var nights = new List<Night>(days);
            int till = config.StartingMoney;
            for (int day = 1; day <= days; day++)
            {
                double stars = Math.Max(0, Math.Min(BarRating.MaxStars, starsOn(day)));
                var menu = MenuAt(book, stars);
                if (menu.Count == 0) continue;

                var h = houseOn?.Invoke(day, stars) ?? HouseBuffs.None;
                // The door's CROWD as the plan's upper bound: the projection has no stools and no
                // waiting line, so it cannot ask whether the bar is keeping up.
                var plan = DayPlan.Roll(menu, day, stars, config, new RunRng(seed + day).GetStream("plan"),
                    h.ArrivalGapScale);
                int tier = TierAt(stars);
                double crowd = config.PriceMultiplier(
                    BarRating.CrowdFor(BarRating.ExactStarsFor(SatisfactionFor(quality))));

                int gross = 0, tips = 0, cheapest = int.MaxValue, dearest = 0, hard = 0;
                foreach (var page in plan.Queue)
                {
                    // The same line TycoonRun.PriceOf walks: the menu price, the premium the shelf's
                    // better bottles earn, the crowd's mood, the room's PRICE, and the till's ceiling.
                    int premium = config.StockPremiumPerTier * (tier - 1) * AlcoholicStyles(page);
                    int paid = Math.Min(DrinkPricing.CeilingPerDrink, Math.Max(1, (int)Math.Round(
                        (DrinkOrder.MenuPrice(page, stars) + premium) * crowd * h.PriceScale,
                        MidpointRounding.AwayFromZero)));
                    int tip = (int)Math.Round(paid * ServiceJudge.TipCeiling * h.TipScale * quality,
                        MidpointRounding.AwayFromZero);
                    if (paid + tip > DrinkPricing.CeilingPerDrink)
                        tip = Math.Max(0, DrinkPricing.CeilingPerDrink - paid);

                    gross += paid;
                    tips += tip;
                    cheapest = Math.Min(cheapest, paid);
                    dearest = Math.Max(dearest, paid);
                    if (RecipeDifficulty.Of(page) == DrinkDifficulty.Hard) hard++;
                }

                // A cover fills a glass, and a glass of stock costs what the shelf's tier costs.
                int stock = plan.Covers * config.RefillPricePerCapacity(tier);
                int rent = StarEconomy.PriceAt(config.Rent(day), stars);
                int net = gross + tips - stock - rent;
                till += net;

                nights.Add(new Night
                {
                    Day = day, Stars = stars, Covers = plan.Covers,
                    Gross = gross, Tips = tips, Stock = stock, Rent = rent, Net = net, Till = till,
                    CheapestCover = cheapest == int.MaxValue ? 0 : cheapest, DearestCover = dearest,
                    AverageCover = plan.Covers == 0 ? 0 : gross / (double)plan.Covers,
                    HardShare = plan.Covers == 0 ? 0 : hard / (double)plan.Covers,
                });
            }
            return nights;
        }

        /// <summary>How many distinct alcoholic styles a page names — what
        /// TycoonRun.PremiumFor counts, without a shelf to ask.</summary>
        private static int AlcoholicStyles(RecipeDefinition recipe) =>
            recipe.RatioRequirements == null ? 0
                : Math.Max(1, recipe.RatioRequirements
                    .Count(b => b.Type == IngredientType.Spirit || b.Type == IngredientType.Bitter));
    }
}
