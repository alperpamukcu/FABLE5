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

        public ServiceVerdict(OrderMatch match, int basePaid, int tip,
            bool craftLanded, bool ordersAgain, double satisfaction,
            double specScore = 1, double fillScore = 1)
        {
            Match = match;
            BasePaid = basePaid;
            Tip = tip;
            CraftLanded = craftLanded;
            OrdersAgain = ordersAgain;
            Satisfaction = satisfaction;
            SpecScore = specScore;
            FillScore = fillScore;
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
        /// doubles the take — which is the point: base pay is low, service is the earner.</summary>
        public const double TipCeiling = 1.0;

        /// <summary>What the tip is made of. Speed leads, because it is the pressure the whole
        /// floor runs on; the spec is the craft read; the fill is the pour itself.</summary>
        public const double SpeedWeight = 0.45, SpecWeight = 0.35, FillWeight = 0.20;

        /// <summary>What is left of the tip when the drink came out wrong (2026-08-14). Half:
        /// enough that a night of missed pours is visibly a worse night at the till, not so
        /// much that a busy bartender is better off binning the glass and starting again.
        /// It is the one number the new middle grade invents, and it is the one to turn if
        /// the pour is to bite harder.</summary>
        public const double CloseTipShare = 0.5;

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
        /// Prices one serve.
        ///
        /// <para><paramref name="served"/> is what the glass actually turned out to be — needed
        /// because a wrong drink now pays the delivered drink's own base price (v5 P11 / C1). An
        /// unidentifiable glass is worth nothing, which is the honest reading of "whatever this
        /// is, it is not a drink".</para>
        /// </summary>
        public static ServiceVerdict Judge(CustomerVisit visit, OrderMatch match,
            GlassContents delivered, WealthTier crowd = WealthTier.Regular, double ambienceBonus = 0,
            RecipeMatch served = null)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));

            var spec = visit.OrderTruth.Spec;
            double fill = delivered?.FillFraction ?? 0;

            // A glass with barely anything in it is refused before anything else is weighed.
            if (delivered != null && !delivered.IsEmpty && fill < RefusalFill)
                return new ServiceVerdict(OrderMatch.Refused, 0, 0, false, false,
                    0.02, 0, 0);

            // What the till takes. The right drink (exact or right family) pays its menu price;
            // the wrong one pays what was actually handed over, which may be nothing at all.
            int basePaid;
            if (match == OrderMatch.Wrong)
                basePaid = served?.Recipe != null ? DrinkOrder.MenuPrice(served.Recipe) : 0;
            else
                basePaid = visit.OrderTruth.Price;

            // How much of the job was done. Each part is a continuous 0–1: nothing here is a
            // cliff, which is the point of the rewrite — patience used to stop mattering at
            // half-time and the spec used to be worth nothing at the till.
            double specScore = spec.Delivered(delivered);
            double fillScore = FillScore(fill, spec.ExpectedFill);
            double speedScore = Math.Max(0.0, 1.0 - visit.WaitFraction);

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

            double quality = SpeedWeight * speedScore
                           + SpecWeight * craftScore
                           + FillWeight * fillScore;

            // A broke crowd never tips; a wrong drink is not tipped for either; and a drink
            // that came out wrong is not tipped like one that came out right. Without that
            // last clause the new Close grade would pay a missed pour EXACTLY what a perfect
            // one pays — same menu price, same tip — and the only cost of sloppy hands would
            // be a quarter of a satisfaction point. The haircut is the money half of the same
            // sentence: they drink it, they pay for it, they do not thank you for it.
            int tip = 0;
            if (crowd != WealthTier.Broke && match != OrderMatch.Wrong && basePaid > 0)
                tip = (int)Math.Round(
                    basePaid * TipCeiling * quality * (match == OrderMatch.Close ? CloseTipShare : 1.0),
                    MidpointRounding.AwayFromZero);

            double satisfaction =
                (match == OrderMatch.Exact ? 0.75 : match == OrderMatch.Close ? 0.5 : 0.05)
                + (match != OrderMatch.Wrong ? 0.20 * (craftScore - 0.5) : 0.0)
                + (match != OrderMatch.Wrong ? 0.12 * (fillScore - 0.5) : 0.0)
                - 0.30 * visit.WaitFraction
                + ambienceBonus;
            satisfaction = Math.Max(0.0, Math.Min(1.0, satisfaction));

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
                satisfaction, specScore, fillScore);
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
