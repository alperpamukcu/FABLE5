using System;

namespace LastCall.Core
{
    /// <summary>How close the served drink came to what was asked for (GDD 23 §4).</summary>
    public enum OrderMatch
    {
        /// <summary>The drink they named.</summary>
        Exact,
        /// <summary>THE DRINK THEY ASKED FOR, MADE WRONG: everything the recipe names is in
        /// the glass and nothing much else is, but the proportions missed a band — so it is
        /// recognisably their drink and it is not their drink.</summary>
        Close,
        /// <summary>Something else entirely. Pays only what the thing in the glass is worth.</summary>
        Wrong,
        /// <summary>Barely anything in the glass. They refuse to pay for it (v5 P11).</summary>
        Refused,
        /// <summary>The bar could not make it and said so (v5 P11, C2).</summary>
        Declined,
    }

    /// <summary>
    /// WHICH THIRD OF THEIR PATIENCE A CUSTOMER IS IN (2026-09-04, the author: "sabır barını
    /// 3'e böleceğiz — kırmızı, sarı, yeşil; böylece hızlı servis etmenin de önemi artacak,
    /// bahşişi arttıracak").
    ///
    /// The clock over a drinker's head has always been one continuous slide, and a slide
    /// says "you are somewhere in this" — it never says WHEN to hurry. Three bands do: the
    /// bar can see at a glance who is still fresh, who is turning and who is about to walk,
    /// and the tip pays for the difference. The band is the rules layer's word, not the
    /// gauge's, so what is drawn over the head and what the till pays are one reading.
    /// </summary>
    public enum ServiceBand
    {
        /// <summary>The first third of the wait: they are happy, and it is worth racing for.</summary>
        Green,
        /// <summary>The middle third: they have noticed how long this is taking.</summary>
        Amber,
        /// <summary>The last third: the drink is late and the tip is nearly gone.</summary>
        Red,
    }

    /// <summary>The money and outcome of one serve (GDD 23 §4–§5).</summary>
    public sealed class ServiceVerdict
    {
        public OrderMatch Match { get; }
        public int BasePaid { get; }
        public int Tip { get; }
        public int Total => BasePaid + Tip;
        /// <summary>They asked for things doing to the drink and got every one of them.</summary>
        public bool CraftLanded { get; }
        public bool OrdersAgain { get; }
        public double Satisfaction { get; }

        /// <summary>Share of the serving spec that was delivered, 0–1 (1 for a plain order).</summary>
        public double SpecScore { get; }

        /// <summary>How close the glass came to the fill they expected, 0–1.</summary>
        public double FillScore { get; }

        /// <summary>How close the pour came to the recipe's perfect ratios, 0–1
        /// (2026-08-20). 1 for drinks with nothing to measure — a pint, a neat pour,
        /// a judge called without a shelf to look ingredients up on.</summary>
        public double Accuracy { get; }

        /// <summary>Every ingredient landed within the perfect window — the make that
        /// reveals the recipe's exact numbers on the menu (2026-08-20).</summary>
        public bool PerfectMake { get; }

        public ServiceVerdict(OrderMatch match, int basePaid, int tip,
            bool craftLanded, bool ordersAgain, double satisfaction,
            double specScore = 1, double fillScore = 1,
            double accuracy = 1, bool perfectMake = false)
        {
            Match = match;
            BasePaid = basePaid;
            Tip = tip;
            CraftLanded = craftLanded;
            OrdersAgain = ordersAgain;
            Satisfaction = satisfaction;
            SpecScore = specScore;
            FillScore = fillScore;
            Accuracy = accuracy;
            PerfectMake = perfectMake;
        }
    }

    /// <summary>
    /// Turns "what was served to whom, how fast" into money and satisfaction (GDD 23 §4).
    /// Pure and stateless: the visit carries the wait and the read, the caller carries the
    /// glass identification (recipe match + applied charges), the judge only prices it.
    ///
    /// **v5 P11 rewrite.** The payment matrix the revision notes asked for: the base price is
    /// low and is what a *correct* drink earns, while the tip is the whole reward for doing
    /// the job well — speed, the serving spec, and the fill, each scaling continuously. Two
    /// rules changed outright and their old pins were rewritten with them: a wrong drink now
    /// pays what the thing in the glass is actually worth instead of nothing, and a glass with
    /// barely anything in it is refused outright.
    /// </summary>
    public static class ServiceJudge
    {
        // ── the tip (v5 P11) ────────────────────────────────────────────────────
        /// <summary>The best tip, as a share of the drink's base price. At 1.0 a perfect serve
        /// doubles the take — which is the point: base pay is low, service is the earner.
        ///
        /// 1.0 → 1.15 (2026-09-04, the author: "bahşişi arttıracak"). A drink handed straight
        /// over is worth MORE than it was, which is the reward half of splitting the clock in
        /// three; the punishment half is <see cref="ClockFloor"/>, and the two were set
        /// together against the 200-run sim rather than by eye.</summary>
        public const double TipCeiling = 1.15;

        /// <summary>
        /// What survives of a tip when the clock has run out — the share of the earned tip
        /// that is NOT on the clock at all.
        ///
        /// MEASURED, NOT CHOSEN (2026-09-04). With the clock as a bare multiplier and no
        /// floor, a bar that serves at a middling pace loses so much of its tip that it
        /// cannot restock: 200 runs went from 2% bankrupt to 100%, dying about day 21, with
        /// the tip per serve down from $4.65 to $2.60 and the take following it into the
        /// ground. A late drink is still a drink, and somebody still paid for it. So a third
        /// of the tip is the craft's, whatever the clock says, and the other two thirds are
        /// what speed is playing for.
        /// </summary>
        public const double ClockFloor = 0.35;

        /// <summary>What the tip is made of. Speed leads, because it is the pressure the whole
        /// floor runs on; the spec is the craft read; the accuracy is the pour's closeness to
        /// the recipe's perfect (2026-08-20); the fill is the glass itself. Fill kept its
        /// 0.20 deliberately — at 0.15 a visibly thin pour rounded away to the same dollar
        /// as a full one on a $10 drink, and a term rounding can erase is not a term.
        ///
        /// THE CLOCK LEFT THE SUM (2026-09-04, the author: "sabır barını 3'e böleceğiz …
        /// böylece hızlı servis etmenin de önemi artacak, bahşişi arttıracak"). Speed used to
        /// be one term of four, weighted 0.35, and the arithmetic made a nonsense of the ask:
        /// with the other three near full, a drink handed over as the customer stood up still
        /// tipped 65% of one handed over instantly — measured, 6 against 10 on a $10 drink.
        /// A weighted term cannot say "too late"; only a multiplier can. So the three craft
        /// terms make the tip you EARNED — they are what weights are for, and they still sum
        /// to one — and the clock decides how much of it you keep. A flawless serve on the
        /// instant still tips exactly its base price; the same drink at the amber line tips
        /// under a third of it; one that arrives as they get up tips nothing.</summary>
        public const double SpecWeight = 0.40, AccuracyWeight = 0.30, FillWeight = 0.30;

        // ── the three bands (2026-09-04) ────────────────────────────────────────

        /// <summary>Where the green band ends and where the amber does, as shares of the
        /// wait SPENT. Even thirds: the gauge is divided by eye and the till agrees with it.</summary>
        public const double GreenBand = 1.0 / 3.0, AmberBand = 2.0 / 3.0;

        /// <summary>What the speed term is still worth at the bottom of each band — the two
        /// numbers that give the curve its shape. Green gives up a quarter across its whole
        /// third (being quick is barely punished), amber gives up more than half of what is
        /// left (the drink is visibly late), and red runs the rest down to nothing.</summary>
        public const double GreenFloor = 0.75, AmberFloor = 0.30;

        /// <summary>Which band a wait of <paramref name="waitFraction"/> stands in.</summary>
        public static ServiceBand BandOf(double waitFraction) =>
            waitFraction < GreenBand ? ServiceBand.Green
            : waitFraction < AmberBand ? ServiceBand.Amber
            : ServiceBand.Red;

        /// <summary>
        /// The speed term of the tip: 1 for a drink handed over on the instant, 0 for one
        /// that arrives as they get up.
        ///
        /// STILL CONTINUOUS, BUT NO LONGER FLAT (2026-09-04). It was <c>1 − wait</c>, a
        /// straight line with no shape, so a bar could not tell whether it was in trouble
        /// until the customer left. Now it bends at the band edges — full marks are worth
        /// racing for, the amber third is where the money actually drains, and red is a
        /// formality — and because it bends rather than steps, nothing is a cliff and no
        /// single frame of lateness costs a dollar.
        /// </summary>
        public static double SpeedScore(double waitFraction)
        {
            double w = Math.Max(0.0, Math.Min(1.0, waitFraction));
            if (w <= GreenBand)
                return 1.0 - w * (1.0 - GreenFloor) / GreenBand;
            if (w <= AmberBand)
                return GreenFloor - (w - GreenBand) * (GreenFloor - AmberFloor) / (AmberBand - GreenBand);
            return AmberFloor * (1.0 - (w - AmberBand) / (1.0 - AmberBand));
        }

        // ── the perfect pour (2026-08-20 respec) ────────────────────────────────

        /// <summary>What an in-box pour earns at its WORST, as a share of the menu price. The
        /// author's own sentence sets both ends of this dial: land the right box far from the
        /// perfect and "en azından çok düşük de olsa ücret alacaksın" — very low, never zero.
        /// Zero is reserved for the wrong box, which is not the drink at all.</summary>
        public const double AccuracyPayFloor = 0.10;

        /// <summary>How close every ingredient must land to the recipe's perfect value for the
        /// make to count as PERFECT — the make that reveals the exact numbers on the menu.
        /// 2.5 points of the glass: tight enough that it is an achievement, wide enough that a
        /// steady hand on a held button can actually reach it (free-hand pouring lands within
        /// ~10 points at best; a deliberate, watched pour does far better). Tuned by sim.</summary>
        public const double PerfectWindow = 0.025;

        /// <summary>How craft splits between the customer's asks and the BOOK's method
        /// (2026-08-11): the garnish spec carries most of it, the recipe's own shaken/stirred
        /// the rest. The method is the recipe's demand, never the customer's — a Martini
        /// wants stirring whoever ordered it.</summary>
        public const double GarnishShare = 0.6, MethodShare = 0.4;

        /// <summary>
        /// Was the drink worked the way its RECIPE says (GDD 23 §4, 2026-08-11)? Shaken
        /// recipes want the tin shaken; stirred recipes want the spoon — and a shaken
        /// Martini is bruised, so the wrong mix scores the same zero as no mix. Built
        /// recipes don't care: they are the "either, or neither" class, which is also what
        /// keeps a tin-built Black Russian's mandatory stir from costing anything.
        /// </summary>
        public static double MethodScore(RecipeDefinition recipe, GlassContents delivered)
        {
            if (recipe == null || delivered == null) return 1.0;
            switch (recipe.Prep)
            {
                case PrepMethod.Shaken:
                    return delivered.HasPreparation(Preparations.Shaken.Id) ? 1.0 : 0.0;
                case PrepMethod.Stirred:
                    return delivered.HasPreparation(Preparations.Stirred.Id) ? 1.0 : 0.0;
                default:
                    return 1.0;
            }
        }

        /// <summary>Below this fill the customer refuses to pay at all (v5 P11). A glass with a
        /// third of a drink in it is not a drink, whatever the ratios say.</summary>
        public const double RefusalFill = 0.35;

        // Widened 0.75 → 0.90 (2026-07-22): the extra order should reward *reading* someone
        // and serving their drink right, not also racing the clock.
        public const double ExtraOrderWindow = 0.90;

        /// <summary>How little of an ingredient still counts as having poured it. Below a
        /// twentieth of the glass it is a dash, and a dash of soda does not make a gin a
        /// Spritz — which is the hole this floor closes.</summary>
        public const double TraceShare = 0.05;

        /// <summary>
        /// Compares the served glass to the order. Exact needs the named recipe; Close is the
        /// SAME drink poured out of tolerance.
        ///
        /// **Rewritten 2026-08-14, because the middle grade did not exist.** Close used to be
        /// "wrong drink from the right family — dominant *type* matches", and
        /// <c>DominantBandType</c> skipped style bands. Since the v5 P10 style era every one
        /// of the 52 banded recipes in <c>recipes.json</c> is style-banded (a recipe may not
        /// mix the two kinds — <see cref="RecipeDefinition"/> refuses it), so that method
        /// returned null every time and this enum value could never be produced by the
        /// shipped game. The UI's amber "THANKS." line and the simulator's Close column were
        /// dead branches; measured across 400 simulated runs the count was zero.
        ///
        /// So the grade is redefined as the one the pour actually needs. A drink whose ratios
        /// drift out of their bands matches NO recipe at all, and the old rule dropped it
        /// straight to Wrong — where it pays the menu price of whatever the glass happens to
        /// be (usually nothing) and sours the customer to 0.05. That is a cliff at the edge
        /// of a band the player cannot see, with no step in between. Close is that step: they
        /// drink it and they pay for it, they do not tip like it was right, and the room
        /// remembers — which lands the cost in STANDING, where the star track can see it.
        ///
        /// A different drink of the same family stays Wrong. A Gin &amp; Tonic is not a Gin
        /// Sour, and paying the sour's menu price for one was the old rule's mistake rather
        /// than its virtue; a wrong drink is already paid for at what it is worth (P11/C1).
        /// </summary>
        public static OrderMatch Compare(DrinkOrder order, RecipeMatch served,
            GlassContents glass, Func<string, IngredientCard> lookup)
        {
            if (order == null || glass == null || glass.IsEmpty) return OrderMatch.Wrong;
            if (served?.Recipe != null && served.Recipe.Id == order.Wanted.Id) return OrderMatch.Exact;

            return NearlyTheDrink(order.Wanted, glass, lookup)
                ? OrderMatch.Close
                : OrderMatch.Wrong;
        }

        /// <summary>
        /// Is this the ordered drink, made wrong? Every band the recipe names has to be IN
        /// the glass, and the glass must not be mostly something the recipe never mentions —
        /// the same stray allowance the matcher itself uses, so "close" and "matched" are
        /// judged against one idea of what the drink is made of. What is forgiven is the one
        /// thing the matcher refuses: the shares themselves.
        ///
        /// The TIER is forgiven with them. A Vesper poured from the well gin fails its
        /// <c>MinTier</c> band and lands here, which is the honest reading of a bottle that
        /// is right in kind and lesser in grade — the drink came out, it came out cheaper.
        ///
        /// An ask with no bands at all — the pint, the neat pour — is exact or it is nothing:
        /// there are no proportions to miss, so there is no middle to stand in.
        /// </summary>
        private static bool NearlyTheDrink(RecipeDefinition wanted, GlassContents glass,
            Func<string, IngredientCard> lookup)
        {
            var bands = wanted?.RatioRequirements;
            if (bands == null || bands.Count == 0) return false;

            var byType = RatioRecipeMatcher.RatiosByType(glass, lookup);
            var byStyle = RatioRecipeMatcher.RatiosByStyle(glass, lookup);

            double named = 0;
            foreach (var band in bands)
            {
                double share;
                if (band.IsStyleBand) byStyle.TryGetValue(band.Style, out share);
                else byType.TryGetValue(band.Type, out share);
                if (share < TraceShare) return false;
                named += share;
            }

            return 1.0 - named <= RatioRecipeMatcher.MaxUnnamedShare + 1e-9;
        }

        /// <summary>The verdict for an order the bar could not fill and said so (v5 P11, C2).
        /// Nothing is paid and the night takes a mark — but telling someone straight is not
        /// the same as leaving them sitting there, and a storm-off scores worse.</summary>
        public static ServiceVerdict Declined() =>
            new ServiceVerdict(OrderMatch.Declined, 0, 0, false, false, 0.15);

        /// <summary>
        /// How close the glass came to the recipe's perfect pour, 0–1, and how far the WORST
        /// ingredient missed it (as a share of the glass). Weighted by each band's perfect
        /// share, because a spirit two points adrift moves the drink more than a garnish two
        /// points adrift — the mouth weights by volume and so does the judge.
        ///
        /// Full marks, by design, for: recipes whose bands were derived rather than authored
        /// (a pint's craft is its head, a neat pour has nothing to learn), and calls with no
        /// lookup (nothing to measure — the older tests price drinks without a shelf).
        /// </summary>
        public static double AccuracyOf(RecipeDefinition recipe, GlassContents glass,
            Func<string, IngredientCard> lookup, out double worstMiss)
        {
            worstMiss = 0;
            if (recipe == null || glass == null || lookup == null
                || !recipe.HasAuthoredRatios)
                return 1.0;

            var perfect = recipe.Perfect;
            var shares = RatioRecipeMatcher.SharesFor(recipe, glass, lookup);
            if (perfect.Length == 0 || shares.Length != perfect.Length) return 1.0;

            double weighted = 0, weight = 0;
            for (int i = 0; i < perfect.Length; i++)
            {
                double miss = Math.Abs(shares[i] - perfect[i]);
                if (miss > worstMiss) worstMiss = miss;
                double a = 1.0 - Math.Min(1.0, miss / RatioBox.Width);
                weighted += perfect[i] * a;
                weight += perfect[i];
            }
            return weight <= 0 ? 1.0 : weighted / weight;
        }

        /// <summary>
        /// Prices one serve.
        ///
        /// <para><paramref name="served"/> is what the glass actually turned out to be — needed
        /// because a wrong drink now pays the delivered drink's own base price (v5 P11 / C1). An
        /// unidentifiable glass is worth nothing, which is the honest reading of "whatever this
        /// is, it is not a drink".</para>
        /// </summary>
        public static ServiceVerdict Judge(CustomerVisit visit, OrderMatch match,
            GlassContents delivered, WealthTier crowd = WealthTier.Regular, double ambienceBonus = 0,
            RecipeMatch served = null, Func<string, IngredientCard> lookup = null)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));

            var spec = visit.OrderTruth.Spec;
            double fill = delivered?.FillFraction ?? 0;

            // A glass with barely anything in it is refused before anything else is weighed.
            if (delivered != null && !delivered.IsEmpty && fill < RefusalFill)
                return new ServiceVerdict(OrderMatch.Refused, 0, 0, false, false,
                    0.02, 0, 0);

            // How close the pour came to the recipe's perfect (2026-08-20). Measured against
            // the ORDERED recipe on an Exact serve — that is the drink whose perfect the
            // player is chasing — and against the DELIVERED drink on a Wrong one, because a
            // wrong drink is paid as what it is, at the quality it is.
            double accuracy; double worstMiss;
            if (match == OrderMatch.Exact)
                accuracy = AccuracyOf(visit.OrderTruth.Wanted, delivered, lookup, out worstMiss);
            else if (match == OrderMatch.Wrong && served?.Recipe != null)
                accuracy = AccuracyOf(served.Recipe, delivered, lookup, out worstMiss);
            else { accuracy = 0; worstMiss = double.MaxValue; }

            // What the till takes (the 2026-08-20 matrix). The right drink pays its menu
            // price SCALED BY CLOSENESS — the box got you paid, the perfect decides how much,
            // and the floor keeps the author's promise that the right box always earns
            // something. The ordered drink in the WRONG box pays nothing at all ("tamamen
            // yanlış"): the box is on the menu for everyone to read, and missing it is
            // missing the drink. A different drink still pays what it actually is.
            int basePaid;
            if (match == OrderMatch.Exact)
                basePaid = Math.Max(1, (int)Math.Round(visit.OrderTruth.Price
                    * (AccuracyPayFloor + (1 - AccuracyPayFloor) * accuracy),
                    MidpointRounding.AwayFromZero));
            else if (match == OrderMatch.Wrong)
                basePaid = served?.Recipe != null
                    ? Math.Max(1, (int)Math.Round(DrinkOrder.MenuPrice(served.Recipe)
                        * (AccuracyPayFloor + (1 - AccuracyPayFloor) * accuracy),
                        MidpointRounding.AwayFromZero))
                    : 0;
            else
                basePaid = 0;   // Close: their drink, out of its box — refused at the till

            // How much of the job was done. Each part is a continuous 0–1: nothing here is a
            // cliff, which is the point of the rewrite — patience used to stop mattering at
            // half-time and the spec used to be worth nothing at the till.
            double specScore = spec.Delivered(delivered);
            double fillScore = FillScore(fill, spec.ExpectedFill);
            double speedScore = SpeedScore(visit.WaitFraction);

            // A pint's craft is its head (GDD 21 §10.3) — it stands in for the spec, because a
            // pint is not garnished and the head is the part you had to get right by hand.
            // A cocktail's craft is the customer's asks AND the book's method (2026-08-11):
            // the METHOD is graded against the ORDERED recipe — you are judged for how the
            // drink they asked for wanted working, even when you built something else.
            bool draught = delivered != null && delivered.HasPreparation(Preparations.Draught.Id);
            double headScore = draught ? TapPour.HeadScore(delivered.Head / delivered.Capacity) : 1.0;
            double methodScore = MethodScore(visit.OrderTruth.Wanted, delivered);
            double craftScore = draught ? headScore
                : GarnishShare * specScore + MethodShare * methodScore;

            // What the job was worth, and then what the clock left of it.
            double craft = SpecWeight * craftScore
                         + AccuracyWeight * accuracy
                         + FillWeight * fillScore;
            double quality = craft * (ClockFloor + (1.0 - ClockFloor) * speedScore);

            // Only the RIGHT drink is tipped now (2026-08-20): a broke crowd never tips, a
            // wrong drink is paid for and nothing more, and the ordered drink out of its box
            // pays nothing at all — so there is nothing to tip on. Accuracy reaches the tip
            // twice on purpose, once inside the base it multiplies and once in the quality:
            // the bill is smaller AND the thanks are cooler, which is how a bar actually
            // treats a drink that is nearly right.
            int tip = 0;
            if (crowd != WealthTier.Broke && match == OrderMatch.Exact && basePaid > 0)
                tip = (int)Math.Round(basePaid * TipCeiling * quality,
                    MidpointRounding.AwayFromZero);

            // Close drops from its P11 half-way house (0.5) to 0.30: they can tell it is
            // their drink and they can tell it is ruined. Still well above Wrong — being
            // recognisably wrong about THEIR drink sours less than handing them a stranger's.
            double satisfaction =
                (match == OrderMatch.Exact ? 0.75 : match == OrderMatch.Close ? 0.30 : 0.05)
                + (match == OrderMatch.Exact ? 0.10 * (accuracy - 0.5) : 0.0)
                + (match != OrderMatch.Wrong ? 0.20 * (craftScore - 0.5) : 0.0)
                + (match != OrderMatch.Wrong ? 0.12 * (fillScore - 0.5) : 0.0)
                - 0.30 * visit.WaitFraction
                + ambienceBonus;
            satisfaction = Math.Max(0.0, Math.Min(1.0, satisfaction));

            // THE PERFECT MAKE (2026-08-20): every ingredient within the window of the
            // recipe's perfect value. This is the make that reveals the exact numbers on the
            // menu — the run keeps the set, the judge only says whether this serve was one.
            bool perfectMake = match == OrderMatch.Exact && lookup != null
                && visit.OrderTruth.Wanted.HasAuthoredRatios
                && worstMiss <= PerfectWindow;

            // Another round is the reward for the exact drink made the way they asked,
            // comfortably inside patience — and only from someone who has been in before.
            // A first-timer orders once (v5 P11, the notes' own rule); an anonymous crowd
            // (no emotion layer) keeps the old behaviour, since "returning" has no meaning
            // when nobody is remembered.
            //
            // The craft gate still needs them to have ASKED for something and got all of it —
            // a plain drink poured plainly is not a feat. Scoring it off the raw spec score
            // instead would hand every plain order a free extra round, which is a different
            // (and much richer) game: the sim's refill bill went up half again.
            // ...and, since 2026-08-11, made the way the BOOK says: a shaken Daiquiri with
            // its asked-for twist earns the round; a stirred one does not, however pretty.
            bool craftForExtra = draught ? headScore >= 1.0
                : (!spec.IsPlain && specScore >= 1.0 && methodScore >= 1.0);
            bool returning = visit.Regular == null || visit.Regular.Visits >= 1;
            bool ordersAgain = match == OrderMatch.Exact && craftForExtra && returning
                && visit.WaitFraction < ExtraOrderWindow
                && visit.ExtraOrdersTaken < CustomerVisit.MaxExtraOrders;

            return new ServiceVerdict(match, basePaid, tip, craftForExtra, ordersAgain,
                satisfaction, specScore, fillScore, accuracy, perfectMake);
        }

        /// <summary>
        /// How close a fill came to what was expected, 0–1. Only shortfalls count: the glass
        /// cannot be overfilled (GDD 21 §3), so a brim-full glass never scores worse than the
        /// one that was asked for.
        /// </summary>
        public static double FillScore(double fill, double expected)
        {
            if (expected <= 0) return 1.0;
            double shortfall = expected - fill;
            if (shortfall <= 0) return 1.0;
            return Math.Max(0.0, 1.0 - shortfall / expected);
        }

        // DominantGlassType / DominantBandType were deleted with the family rule they served
        // (2026-08-14). Worth its own line: the glass one picked its winner by walking a
        // Dictionary, so two ingredients tied on volume were separated by hash order — a
        // grade that could differ between two identical runs, in a project whose first rule
        // is that a seed reproduces. It never showed, because it could never be reached.
    }
}
