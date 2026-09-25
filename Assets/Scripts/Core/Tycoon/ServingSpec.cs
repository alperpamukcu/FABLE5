using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// How a customer wants their drink served (v5 P11, GDD 23 §3.1). The order names a drink;
    /// this names everything else about it — ice, a rim, a twist. It is **stated**,
    /// printed on the licence for the player to read; the inferred-preference layer it once
    /// stood beside was demolished with the emotion machinery.
    ///
    /// Every request is gradeable and every miss costs tip rather than payment: a Gin Fizz
    /// served without the ice is still a Gin Fizz, and the customer still pays for it.
    ///
    /// THE SHAKE LEFT THE SPEC (2026-08-11, the author: "shaken olması müşterinin talebi
    /// değil tarifin talebi olmalı"). "Extra shaken" was a customer whim rolled at 25% —
    /// and it was the only place the mixing method was ever graded, while the RECIPE's own
    /// shaken/stirred went unread. That was backwards: how a drink is worked belongs to the
    /// book, and the judge grades it against <see cref="RecipeDefinition.Prep"/> now.
    /// </summary>
    public sealed class ServingSpec
    {
        public static readonly ServingSpec Plain = new ServingSpec(null);

        /// <summary>Ice, a lemon twist, a salted or sugared rim.</summary>
        public IReadOnlyList<PreparationDefinition> Garnishes { get; }

        /// <summary>How many separate things they asked for.</summary>
        public int RequestCount => Garnishes.Count;

        public bool IsPlain => RequestCount == 0;

        public ServingSpec(IReadOnlyList<PreparationDefinition> garnishes)
        {
            Garnishes = garnishes ?? Array.Empty<PreparationDefinition>();
        }

        /// <summary>
        /// How often a customer wants the drink THE WAY IT COMES — its own dressing, from
        /// <see cref="RecipeDefinition.Likes"/> (2026-09-23, the author: "gin fizz söyleyen biri
        /// yüksek ihtimalle şeker gerdanlıklı söylemeli").
        ///
        /// Three in four, and not four in four on purpose: a habit that never failed would be a
        /// second signature, and the difference between the two is the point of having both. It is
        /// also the reason the number lives here rather than per page — a habit is how the DRINK is
        /// taken, and how strongly a habit holds is how this bar's crowd behaves.
        /// </summary>
        public const int UsualChance = 75;

        /// <summary>The fill every order expects ("filled to the top" retired 2026-08-02,
        /// its machinery removed in the 2026-08-07 sweep). Under-filling costs;
        /// over-filling cannot happen — the glass stops at the brim.</summary>
        public const double NormalFill = 0.80;

        public double ExpectedFill => NormalFill;

        /// <summary>
        /// The share of this spec the delivered glass actually satisfies, 0–1. A plain order is
        /// satisfied by definition — asking for nothing cannot be got wrong.
        /// </summary>
        public double Delivered(GlassContents glass)
        {
            int total = RequestCount;
            if (total == 0) return 1.0;
            if (glass == null) return 0.0;

            int met = 0;
            foreach (var garnish in Garnishes)
                if (glass.HasPreparation(garnish.Id)) met++;
            return (double)met / total;
        }

        /// <summary>
        /// Rolls what this customer wants doing to this drink, from the options the recipe can
        /// actually honour (stream "orders"). A pint takes no garnish and cannot be shaken; a
        /// built drink never sees a shaker. Asking for something the recipe forbids would be an
        /// order nobody could fill — the one thing an order must never be.
        /// </summary>
        /// <param name="allowed">The preparations the bar can actually put on a glass — the ladder's answer
        /// (BarRank.PreparationsOpen, 2026-09-21). Null means all four, which is every bench setup and old test.
        /// An empty list is a bar with nothing on its rail yet: the order is plain, whatever was rolled.</param>
        public static ServingSpec Roll(RecipeDefinition recipe, SeededRng rng,
            IReadOnlyList<PreparationDefinition> allowed = null)
        {
            if (recipe == null) return Plain;

            bool draught = IsDraught(recipe);

            // THE SIGNATURE EXTRA (2026-09-21): a page that names its garnish is always asked for with it.
            var signature = recipe.Garnish != null ? Preparations.Find(recipe.Garnish) : null;
            var garnishes = new List<PreparationDefinition>(3);
            if (signature != null) garnishes.Add(signature);

            // ── the two rolls, taken for EVERY order (2026-09-23) ────────────────────────────
            //
            // Roughly half of all orders want something extra, as before the spec existed. The
            // second roll is new: whether this one wants the drink THE WAY IT COMES.
            //
            // Both are taken whatever page it is, and so is everything below — which is the whole
            // reason this is safe. A stream whose draw COUNT depends on the recipe means re-tuning
            // content silently reseeds every later customer's night, and a page's habit is content.
            // (The old shape already had that fault in miniature: it removed the signature from the
            // bag, so a signed page drew its index against a smaller number than a plain one. It
            // does not any more.)
            bool wantsSomething = rng.NextInt(100) >= 50;
            bool wantsTheUsual = rng.NextInt(100) < UsualChance;

            // ── how the drink is usually taken ───────────────────────────────────────────────
            //
            // The author (2026-09-23): "bazı kokteyllerde bazı garnishler şarttır, örneğin gin
            // fizz'de şeker gerdanlık". So a page's own dressing is asked for far more often than
            // the dice would ever ask for it — and when it IS asked for, it is the whole ask: the
            // random extras stand down rather than piling a twist and a salt rim on top of a drink
            // that already came with its sugared rim. A pint takes none of this (GDD 21 §10).
            bool tookTheUsual = false;
            if (wantsTheUsual && !draught)
                foreach (var id in recipe.Likes)
                {
                    var wanted = Preparations.Find(id);
                    if (wanted == null || garnishes.Contains(wanted)) continue;
                    if (!OnTheRail(allowed, wanted)) continue;   // the rail cannot give it tonight
                    garnishes.Add(wanted);
                    tookTheUsual = true;
                }

            // ── and the extras, rolled either way and kept only when they are wanted ─────────
            if (!draught)
            {
                var bag = new List<PreparationDefinition>(allowed ?? GarnishPool);
                if (bag.Count > 0)
                {
                    int count = rng.NextInt(100) < 65 ? 1 : 2;
                    for (int i = 0; i < count && bag.Count > 0; i++)
                    {
                        int k = rng.NextInt(bag.Count);
                        var picked = bag[k];
                        bag.RemoveAt(k);
                        if (wantsSomething && !tookTheUsual && !garnishes.Contains(picked))
                            garnishes.Add(picked);
                    }
                }
            }

            // (The extra-shaken roll stood here until 2026-08-11. Its NextInt stays OUT —
            // the stream's numbers shift either way, and the sim re-baseline covers it.)

            if (garnishes.Count == 0) return Plain;
            return new ServingSpec(garnishes);
        }

        /// <summary>The garnishes a customer can ask for: the six droppable preparations, in the rail's order.
        /// Olives and mint joined on 2026-09-21 (the author: extras, never a recipe's band); a run hands Roll
        /// the ones its ladder and its shelf allow.</summary>
        public static readonly IReadOnlyList<PreparationDefinition> GarnishPool = new[]
        {
            Preparations.Ice, Preparations.LemonTwist, Preparations.SaltRim, Preparations.SugarRim,
            Preparations.Olive, Preparations.Mint,
        };

        /// <summary>Whether the bar can put this on a glass tonight. A null rail is every bench
        /// setup and every older test, where the whole pool is open.</summary>
        private static bool OnTheRail(IReadOnlyList<PreparationDefinition> allowed,
            PreparationDefinition wanted)
        {
            if (allowed == null) return true;
            for (int i = 0; i < allowed.Count; i++)
                if (ReferenceEquals(allowed[i], wanted) || allowed[i].Id == wanted.Id) return true;
            return false;
        }

        private static bool IsDraught(RecipeDefinition recipe)
        {
            foreach (var band in recipe.RatioRequirements)
                if (!band.IsStyleBand && band.Type == IngredientType.Beer) return true;
            return false;
        }

        public override string ToString()
        {
            if (IsPlain) return "as it comes";
            var parts = new List<string>();
            foreach (var g in Garnishes) parts.Add(g.Name);
            return string.Join(", ", parts);
        }
    }
}
