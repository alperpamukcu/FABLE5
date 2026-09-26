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
        public static double StarsOn(int day) => Math.Min(BarRating.MaxStars, (day - 1) * Climb);

        /// <summary>The slope of <see cref="StarsOn"/>: the stars a night adds to the standing the projection assumes.
        /// The furnished walk's service side climbs by it too (<see cref="WalkFurnishing"/>).</summary>
        public const double Climb = 0.135;

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
                int tier = TierAt(stars);
                // ONE NIGHT'S ARITHMETIC, SHARED (2026-09-26): the furnished walk below prices its nights
                // through the same body, handing it the shelf it owns where this hands it the rung's tier.
                // Integer for integer the same sums in the same order, so this walk's tables do not move.
                var n = NightOf(day, stars, quality, config, menu, seed, h,
                    page => config.StockPremiumPerTier * (tier - 1) * AlcoholicStyles(page),
                    page => config.RefillPricePerCapacity(tier), out _);
                till += n.Net;
                n.Till = till;
                nights.Add(n);
            }
            return nights;
        }

        /// <summary>
        /// One night at <paramref name="stars"/>, cut from <paramref name="menu"/>: covers from the plan,
        /// each priced the way TycoonRun.PriceOf prices it, tipped at the standard's quality, less the
        /// stock it burned and the rent. <see cref="Night.Till"/> is left for the caller, which owns the
        /// till. <paramref name="bestRank"/> is the dearest page the night served — the menu's ceiling.
        /// </summary>
        private static Night NightOf(int day, double stars, double quality, TycoonConfig config,
            IReadOnlyList<RecipeDefinition> menu, string seed, HouseBuffs h,
            Func<RecipeDefinition, int> premiumOf, Func<RecipeDefinition, int> refillOf, out int bestRank)
        {
            // The door's CROWD as the plan's upper bound: the projection has no stools and no
            // waiting line, so it cannot ask whether the bar is keeping up.
            var plan = DayPlan.Roll(menu, day, stars, config, new RunRng(seed + day).GetStream("plan"),
                h.ArrivalGapScale);
            double crowd = config.PriceMultiplier(
                BarRating.CrowdFor(BarRating.ExactStarsFor(SatisfactionFor(quality))));

            int gross = 0, tips = 0, cheapest = int.MaxValue, dearest = 0, hard = 0, stock = 0;
            bestRank = 0;
            foreach (var page in plan.Queue)
            {
                // The same line TycoonRun.PriceOf walks: the menu price, the premium the shelf's
                // better bottles earn, the crowd's mood, the room's PRICE, and the till's ceiling.
                int premium = premiumOf(page);
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
                // A cover fills a glass, and a glass of stock costs what the shelf's tier costs.
                stock += refillOf(page);
                bestRank = Math.Max(bestRank, page.Rank);
            }

            int rent = StarEconomy.PriceAt(config.Rent(day), stars);
            int net = gross + tips - stock - rent;
            return new Night
            {
                Day = day, Stars = stars, Covers = plan.Covers,
                Gross = gross, Tips = tips, Stock = stock, Rent = rent, Net = net,
                CheapestCover = cheapest == int.MaxValue ? 0 : cheapest, DearestCover = dearest,
                AverageCover = plan.Covers == 0 ? 0 : gross / (double)plan.Covers,
                HardShare = plan.Covers == 0 ? 0 : hard / (double)plan.Covers,
            };
        }

        /// <summary>How many distinct alcoholic styles a page names — what
        /// TycoonRun.PremiumFor counts, without a shelf to ask.</summary>
        private static int AlcoholicStyles(RecipeDefinition recipe) =>
            recipe.RatioRequirements == null ? 0
                : Math.Max(1, recipe.RatioRequirements
                    .Count(b => b.Type == IngredientType.Spirit || b.Type == IngredientType.Bitter));

        // ── THE ROOM, BOUGHT NIGHT BY NIGHT (2026-09-26) ─────────────────────────────────────────
        // The author: "Tüm geliştirmeleri ekonomi dengesine dahil et, Oyuncu oyunda maksimum 5 konfora
        // ulaşmalı ve bu oyun sonlarına yakın gerçekleşmeli." Walk above spends nothing: the room, the
        // pages and the bottles never touched its till, and comfort was never computed. This walk buys
        // them — so the night the house reaches five is a number the tables can print, and a balance
        // change to a price or a comfort figure moves it where anyone can read it.

        /// <summary>One furnished night: what it took, where it left the bar, and what the bar bought after it.</summary>
        public struct RoomNight
        {
            /// <summary>The night's takings, priced from the pages and bottles the bar owned that night.
            /// Its <see cref="Night.Till"/> is the till at the close, BEFORE the day-end shopping.</summary>
            public Night Night;
            /// <summary>The standing after the night was filed, and its high-water mark (what the shop reads).</summary>
            public double Standing, Best;
            /// <summary>The stars the night filed: the lower of the climb, the menu's ceiling and the room.</summary>
            public double Filed;
            /// <summary>What the room was worth that night (a clean counter: exactly its base).</summary>
            public double Comfort;
            /// <summary>What the room is worth after the day's shopping.</summary>
            public double ComfortAfter;
            /// <summary>True when the room held the night under what the drinks would have filed.</summary>
            public bool RoomBound;
            /// <summary>Spent after the close: fittings, glass, stools and the bar top; pages; bottles.</summary>
            public int Room, Pages, Bottles;
            /// <summary>The till after the day's shopping — what the next night opens with.</summary>
            public int Till;
        }

        /// <summary>
        /// THE FURNISHED WALK: a bar of this standard that buys its room, its pages and its bottles out of
        /// its own till, night by night, from the bare room and the opening shelf.
        ///
        /// Each night is priced at the bar's high-water mark from the pages it OWNS that its shelf can
        /// pour (<see cref="TycoonRun.CanServeFrom"/> — the night's plan asks nothing else, 2026-09-26), with
        /// the premium and the refill of the bottles it has actually bought and the buffs of the room it
        /// actually has installed. The night files the LOWER of three things — the climb, the menu's ceiling
        /// (<see cref="TycoonRun.MenuStarCapFor"/>) and the room (a clean counter: the base) — through
        /// <see cref="BarRating"/>'s own step. Then, at the close, the shop:
        /// <list type="number">
        /// <item>the rung's better bottles — the best tier the market sells at this standing for every
        ///   alcoholic style a page owned pours, which is the premium <see cref="Walk"/> assumes
        ///   (<see cref="TierAt"/>) and which pays for itself within a night or two;</item>
        /// <item>every page whose gate is open, cheapest first;</item>
        /// <item>for every page owned the cheapest card that answers a band the shelf cannot (and the jar a mint
        ///   or olive page needs once the rail carries it);</item>
        /// <item>while the room is worth less than the standing plus <paramref name="lead"/>, the open piece with
        ///   the most comfort per dollar (<see cref="TycoonRun.ComfortGainIn"/> for a rung; a glass step, a stool
        ///   or a bar-top step is one of them and spends the night's one fitting);</item>
        /// <item>the night's one fitting, if it is still unspent: the cheapest stool, bar-top step or glass step;</item>
        /// <item>the rest of the room, cheapest first.</item>
        /// </list>
        /// Keeping tomorrow's rent and twenty dollars less what tonight's drinks cleared (never under twenty);
        /// ties go to catalogue order.
        ///
        /// THE BOOK BEFORE THE ROOM (measured, 2026-09-26). The shopper the comfort brief first sketched bought
        /// the room first — while it held the standing — and then the pages, keeping the whole of tomorrow's
        /// rent back. That order was drawn against a projection whose nights were priced from every page at
        /// the rung whether bought or not. Priced honestly, from what the bar owns, it starves the book: the
        /// room eats the money the next rung's pages need, the rung's rent doubles or triples under a menu of
        /// cheaper pages, and a competent bar never left two stars. Pages first, the room with what is left.
        ///
        /// <b>THE CLIMB IS AN ASSUMPTION, NOT A RESULT.</b> The service side of every night is the standing
        /// plus <paramref name="climb"/> — <see cref="StarsOn"/>'s own slope — and not what this standard's
        /// drinks would file: <see cref="SatisfactionFor"/> puts a competent night at about four stars of
        /// service, so a real competent bar is held under five by its drinks before its room. What this walk
        /// answers is WHEN THE MONEY BUYS THE HOUSE, for a bar that climbs at the projection's pace — which is
        /// the question the author asked ("maksimum 5 konfor ... oyun sonlarına yakın").
        ///
        /// <paramref name="shelf"/> and <paramref name="catalogue"/> are the opening shelf and everything the
        /// market could ever sell (the deck's starting cards and the rest, locked stock included — Core cannot
        /// read the deck). A null shelf answers every band: the pages alone are bought and priced at the rung's
        /// tier, as <see cref="Walk"/> prices them. Pure and deterministic: the plan's own seed, no other dice.
        /// </summary>
        public static List<RoomNight> WalkFurnishing(IReadOnlyList<RecipeDefinition> book, TycoonConfig config,
            IReadOnlyList<FixtureDefinition> fixtures, IReadOnlyList<GlasswareDefinition> glassware,
            int days, double quality, IReadOnlyList<IngredientCard> shelf = null,
            IReadOnlyList<IngredientCard> catalogue = null, double climb = Climb, double lead = 0.5,
            string seed = "projection")
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (config == null) throw new ArgumentNullException(nameof(config));
            var bar = new FurnishedBar(book, config, fixtures, glassware, shelf, catalogue);
            var rating = new BarRating();
            var nights = new List<RoomNight>(days);
            int till = config.StartingMoney;

            for (int day = 1; day <= days; day++)
            {
                // ── the night, priced at the mark the shop reads ──
                double stars = rating.BestStanding;
                var menu = bar.MenuTonight(stars);
                var h = HouseBuffs.From(bar.Installed());
                int tier = TierAt(stars);
                var n = NightOf(day, stars, quality, config, menu, seed, h,
                    page => bar.Shelf == null
                        ? config.StockPremiumPerTier * (tier - 1) * AlcoholicStyles(page)
                        : TycoonRun.PremiumFrom(page, bar.Shelf, config.StockPremiumPerTier),
                    page => config.RefillPricePerCapacity(bar.Shelf == null ? tier : bar.RefillTier(page)),
                    out int bestRank);
                till += n.Net;
                n.Till = till;

                // ── filed: the lower of the climb, the menu and the room ──
                double comfort = VenueComfort.Tonight(bar.ComfortBase, 1.0, h.ComfortScale);
                double service = Math.Min(BarRating.MaxStars, rating.Average + climb);
                double menuCap = TycoonRun.MenuStarCapFor(bestRank);
                rating.CloseNight(service / BarRating.MaxStars, Math.Min(comfort, menuCap));
                var night = new RoomNight
                {
                    Night = n,
                    Standing = rating.Average,
                    Best = rating.BestStanding,
                    Filed = rating.LastNight,
                    Comfort = comfort,
                    RoomBound = comfort < Math.Min(service, menuCap) - 1e-9,
                };

                // ── the day's end: the shop ──
                // What is kept back is tomorrow's rent and twenty dollars LESS what tonight's drinks cleared:
                // the rent is charged at tomorrow's close, after tomorrow's takings, so a bar that kept the
                // whole of it in the drawer would never buy a page it could have paid for twice over.
                double best = rating.BestStanding;
                int keep = Math.Max(20, StarEconomy.PriceAt(config.Rent(day + 1), best) + 20
                                        - (n.Gross + n.Tips - n.Stock));
                bar.NewNight();
                int better = bar.BuyBetter(best, till - keep);
                till -= better; night.Bottles += better;
                int pages = bar.BuyPages(best, till - keep);
                till -= pages; night.Pages += pages;
                int bottles = bar.BuyBottles(best, till - keep);
                till -= bottles; night.Bottles += bottles;
                while (bar.ComfortBase < best + lead - 1e-9)
                {
                    int spent = bar.BuyPiece(best, till - keep, byValue: true);
                    if (spent <= 0) break;
                    till -= spent; night.Room += spent;
                }
                int fitting = bar.BuyFitting(till - keep);
                till -= fitting; night.Room += fitting;
                while (true)
                {
                    int spent = bar.BuyPiece(best, till - keep, byValue: false);
                    if (spent <= 0) break;
                    till -= spent; night.Room += spent;
                }
                night.ComfortAfter = bar.ComfortBase;
                night.Till = till;
                nights.Add(night);
            }
            return nights;
        }

        /// <summary>The first night whose shopping left the room worth five, or 0 if none in the walk.</summary>
        public static int ComfortFiveNight(IReadOnlyList<RoomNight> nights)
        {
            foreach (var n in nights)
                if (n.ComfortAfter >= VenueComfort.MaxComfort - 1e-9) return n.Night.Day;
            return 0;
        }

        /// <summary>The first night the standing reached five, or 0 if none in the walk.</summary>
        public static int FiveStarNight(IReadOnlyList<RoomNight> nights)
        {
            foreach (var n in nights)
                if (n.Standing + BarRank.Epsilon >= BarRating.MaxStars) return n.Night.Day;
            return 0;
        }

        /// <summary>
        /// The furnished walk's bar: what it owns and how it buys. Every purchase is priced the way the run
        /// prices it (<see cref="StarEconomy"/> for a fitting, the glass line's step, the config's stool and bar
        /// top, <see cref="TycoonRun.RecipePriceAt"/> for a page, <see cref="Market.RungPrice"/> for a bottle).
        /// </summary>
        private sealed class FurnishedBar
        {
            private readonly IReadOnlyList<RecipeDefinition> _book;
            private readonly TycoonConfig _config;
            private readonly IReadOnlyList<FixtureDefinition> _fixtures;
            private readonly IReadOnlyList<GlasswareDefinition> _glassware;
            private readonly IReadOnlyList<IngredientCard> _catalogue;
            private readonly Dictionary<string, int> _level = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly HashSet<string> _singles = new HashSet<string>(StringComparer.Ordinal);
            private readonly int[] _glassTier;
            private readonly List<RecipeDefinition> _pages = new List<RecipeDefinition>();
            private readonly HashSet<string> _owned = new HashSet<string>(StringComparer.Ordinal);
            private int _seats, _counter;
            private bool _fitted;

            /// <summary>The bottles bought, null for a walk that does not count bottles.</summary>
            public readonly List<IngredientCard> Shelf;

            public FurnishedBar(IReadOnlyList<RecipeDefinition> book, TycoonConfig config,
                IReadOnlyList<FixtureDefinition> fixtures, IReadOnlyList<GlasswareDefinition> glassware,
                IReadOnlyList<IngredientCard> shelf, IReadOnlyList<IngredientCard> catalogue)
            {
                _book = book;
                _config = config;
                _fixtures = fixtures ?? Array.Empty<FixtureDefinition>();
                _glassware = glassware ?? Array.Empty<GlasswareDefinition>();
                _catalogue = catalogue ?? Array.Empty<IngredientCard>();
                Shelf = shelf == null ? null : new List<IngredientCard>(shelf);
                _glassTier = new int[_glassware.Count];
                for (int i = 0; i < _glassTier.Length; i++) _glassTier[i] = 1;
                _seats = config.StartingSeats;
                _counter = 1;
                foreach (var f in _fixtures)
                {
                    if (!f.StartsInTheRoom) continue;
                    if (f.Level > 0) _level[f.Slot] = Math.Max(LevelOf(f.Slot), f.Level);
                    else _singles.Add(f.Id);
                }
                foreach (var r in book)
                    if (!r.Locked && r.RatioRequirements.Count > 0 && _owned.Add(r.Id)) _pages.Add(r);
            }

            private int LevelOf(string slot) => _level.TryGetValue(slot, out int l) ? l : 0;

            private bool Owns(FixtureDefinition f) =>
                f.Level > 0 ? LevelOf(f.Slot) >= f.Level : _singles.Contains(f.Id);

            /// <summary>The pieces the room wears: the top rung of every ladder and every owned single.</summary>
            public List<FixtureDefinition> Installed()
            {
                var room = new List<FixtureDefinition>();
                foreach (var f in _fixtures)
                    if (f.Level > 0 ? LevelOf(f.Slot) == f.Level : _singles.Contains(f.Id)) room.Add(f);
                return room;
            }

            public double ComfortBase
            {
                get
                {
                    double fx = 0, glass = 0;
                    foreach (var f in Installed()) fx += f.Comfort;
                    for (int i = 0; i < _glassware.Count; i++)
                        for (int s = 0; s < _glassTier[i] - 1 && s < _glassware[i].TierComfort.Count; s++)
                            glass += _glassware[i].TierComfort[s];
                    return VenueComfort.Base(fx, glass, _seats - _config.StartingSeats, _counter - 1);
                }
            }

            /// <summary>Tonight's orders: the pages owned that the shelf can pour at this standing — the rule
            /// the run's plan keeps. A page-less night is the run's own fallback: every page owned.</summary>
            public List<RecipeDefinition> MenuTonight(double stars)
            {
                var menu = Shelf == null
                    ? _pages.Where(r => RankGate(r.Rank) <= stars + 1e-9).ToList()
                    : _pages.Where(r => TycoonRun.CanServeFrom(r, Shelf, stars)).ToList();
                return menu.Count > 0 ? menu : new List<RecipeDefinition>(_pages);
            }

            /// <summary>The tier a cover of this page refills at: its best alcoholic bottle's, 1 at the well.</summary>
            public int RefillTier(RecipeDefinition page)
            {
                int tier = 1;
                foreach (var band in page.RatioRequirements)
                    foreach (var card in Shelf)
                        if (TycoonRun.CardAnswers(card, band) && card.Info != null
                            && IngredientCategories.IsAlcoholic(card.Info.Category, card.Type))
                            tier = Math.Max(tier, card.Info.Tier);
                return tier;
            }

            public void NewNight() => _fitted = false;

            // ── the room ──

            /// <summary>
            /// Buys ONE open piece within <paramref name="budget"/>: by comfort per dollar, or the cheapest.
            /// Returns what it cost, 0 when nothing open is both worth something and affordable.
            /// </summary>
            public int BuyPiece(double stars, int budget, bool byValue)
            {
                FixtureDefinition pick = null; int pickGlass = -1, pickKind = 0, pickPrice = 0;
                double pickScore = double.NegativeInfinity;
                void Consider(double gain, int price, FixtureDefinition f, int glass, int kind)
                {
                    if (gain <= 0 || price <= 0 || price > budget) return;
                    double score = byValue ? gain / price : -price;
                    if (score <= pickScore) return;
                    pickScore = score; pick = f; pickGlass = glass; pickKind = kind; pickPrice = price;
                }
                foreach (var f in _fixtures)
                {
                    if (f.IsTap || f.StartsInTheRoom || Owns(f) || f.Stars > stars + 1e-9) continue;
                    if (f.Level > 0 && f.Level != LevelOf(f.Slot) + 1) continue;
                    Consider(TycoonRun.ComfortGainIn(f, _fixtures), StarEconomy.PriceAt(f.Price, f.Stars), f, -1, 1);
                }
                if (!_fitted) FittingCandidates(Consider);
                if (pickKind == 0) return 0;
                Take(pick, pickGlass, pickKind);
                return pickPrice;
            }

            /// <summary>The night's one fitting, if it is still unspent: the cheapest stool, bar-top step or glass step.</summary>
            public int BuyFitting(int budget)
            {
                if (_fitted) return 0;
                int pickGlass = -1, pickKind = 0, pickPrice = int.MaxValue;
                FittingCandidates((gain, price, f, glass, kind) =>
                {
                    if (price <= 0 || price > budget || price >= pickPrice) return;
                    pickPrice = price; pickGlass = glass; pickKind = kind;
                });
                if (pickKind == 0) return 0;
                Take(null, pickGlass, pickKind);
                return pickPrice;
            }

            /// <summary>The three fittings that share the night's one slot, in the shop's order: the next
            /// stool, the next bar-top step, the next step of each glass line. Kinds: 2 stool, 3 bar top, 4 glass.</summary>
            private void FittingCandidates(Action<double, int, FixtureDefinition, int, int> consider)
            {
                if (_seats < _config.MaxSeats)
                    consider(VenueComfort.StoolComfort, _config.SeatPrice(_seats), null, -1, 2);
                if (_counter < _config.MaxAmbienceTier)
                    consider(VenueComfort.CounterComfort, _config.CounterPrice(_counter), null, -1, 3);
                for (int i = 0; i < _glassware.Count; i++)
                {
                    int t = _glassTier[i];
                    if (t >= TycoonRun.MaxGlassTier) continue;
                    var g = _glassware[i];
                    double gain = t - 1 < g.TierComfort.Count ? g.TierComfort[t - 1] : 0;
                    consider(gain, g.TierPrices[t - 1], null, i, 4);
                }
            }

            private void Take(FixtureDefinition f, int glass, int kind)
            {
                switch (kind)
                {
                    case 1:
                        if (f.Level > 0) _level[f.Slot] = f.Level; else _singles.Add(f.Id);
                        break;
                    case 2: _seats++; _fitted = true; break;
                    case 3: _counter++; _fitted = true; break;
                    case 4: _glassTier[glass]++; _fitted = true; break;
                }
            }

            // ── the book and the shelf ──

            /// <summary>Every page whose gate is open and the till can pay for, cheapest first.</summary>
            public int BuyPages(double stars, int budget)
            {
                int spent = 0;
                var open = _book.Where(r => r.Locked && r.RatioRequirements.Count > 0 && !_owned.Contains(r.Id)
                                            && RankGate(r.Rank) <= stars + 1e-9)
                                .OrderBy(r => TycoonRun.RecipePriceAt(r.Rank, RankGate(r.Rank)))
                                .ThenBy(r => r.Rank).ToList();
                foreach (var r in open)
                {
                    int price = TycoonRun.RecipePriceAt(r.Rank, RankGate(r.Rank));
                    if (price > budget - spent) continue;
                    spent += price;
                    _owned.Add(r.Id);
                    _pages.Add(r);
                }
                return spent;
            }

            /// <summary>For every page owned, in the book's order, the cheapest card that answers a band the shelf
            /// cannot — and the jar a mint or olive page needs once the rail carries it — whose gate is open.</summary>
            public int BuyBottles(double stars, int budget)
            {
                if (Shelf == null) return 0;
                int spent = 0;
                foreach (var page in _pages.OrderBy(r => r.Rank))
                {
                    foreach (var band in page.RatioRequirements)
                    {
                        if (Shelf.Any(c => TycoonRun.CardAnswers(c, band))) continue;
                        spent += BuyCheapest(c => TycoonRun.CardAnswers(c, band), stars, budget - spent);
                    }
                    string jar = page.Garnish == Preparations.Olive.Id ? "olive"
                               : page.Garnish == Preparations.Mint.Id ? "mint" : null;
                    if (jar != null && BarRank.Has(stars, Feature.Jars) && !Shelf.Any(c => c.Info?.Style == jar))
                        spent += BuyCheapest(c => c.Info?.Style == jar, stars, budget - spent);
                }
                return spent;
            }

            /// <summary>
            /// THE RUNG'S BETTER BOTTLES: for every alcoholic style a page owned pours, the best tier the market
            /// sells at this standing, when the shelf's best of that style is under it — the premium a better
            /// bottle earns on every drink (<see cref="TycoonRun.PremiumFrom"/>) is what <see cref="Walk"/>
            /// assumes a bar at the rung has (<see cref="TierAt"/>); this walk buys it before it counts it.
            /// </summary>
            public int BuyBetter(double stars, int budget)
            {
                if (Shelf == null) return 0;
                int spent = 0;
                foreach (var page in _pages.OrderBy(r => r.Rank))
                    foreach (var band in page.RatioRequirements)
                    {
                        if (!band.IsStyleBand) continue;
                        var own = Shelf.Where(c => c.Info?.Style == band.Style).ToList();
                        if (own.Count == 0 || !own.Any(c => IngredientCategories.IsAlcoholic(c.Info.Category, c.Type))) continue;
                        int have = own.Max(c => c.Info.Tier);
                        int top = 0;
                        foreach (var card in _catalogue)
                        {
                            if (card?.Info == null || card.Info.Style != band.Style) continue;
                            double gate = Market.GateOf(card);
                            if (double.IsNaN(gate) || gate > stars + 1e-9) continue;
                            top = Math.Max(top, card.Info.Tier);
                        }
                        if (top <= have) continue;
                        spent += BuyCheapest(c => c.Info?.Style == band.Style && c.Info.Tier == top, stars, budget - spent);
                    }
                return spent;
            }

            private int BuyCheapest(Func<IngredientCard, bool> answers, double stars, int budget)
            {
                IngredientCard pick = null; int pickPrice = int.MaxValue;
                foreach (var card in _catalogue)
                {
                    if (card?.Info == null || !answers(card) || Shelf.Any(c => c.Id == card.Id)) continue;
                    double gate = Market.GateOf(card);
                    if (double.IsNaN(gate) || gate > stars + 1e-9) continue;
                    int price = Market.RungPrice(card);
                    if (price < pickPrice) { pick = card; pickPrice = price; }
                }
                if (pick == null || pickPrice > budget) return 0;
                Shelf.Add(pick);
                return pickPrice;
            }
        }
    }
}
