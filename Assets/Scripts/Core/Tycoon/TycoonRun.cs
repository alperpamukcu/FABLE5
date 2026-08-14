using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    public enum TycoonPhase
    {
        /// <summary>The floor is live: customers arrive, patience ticks, drinks go out.</summary>
        DayOpen,
        /// <summary>The last customer left. Invoice, refills, the market — then tomorrow.</summary>
        DayEnd,
        /// <summary>Three red days. The bar is gone.</summary>
        Closed,
    }

    /// <summary>
    /// The v4 run controller (GDD 23, PLAN P2): days on the floor, money in the till, the
    /// ledger underneath. One glass is built at a time with the pour verbs and served to a
    /// chosen seat; everything else — arrivals, patience, prices, tips, rent, bankruptcy —
    /// flows through the Tycoon core classes.
    ///
    /// Regulars are opt-in: built without a regulars registry, the crowd stays anonymous
    /// and nothing else changes.
    /// </summary>
    public sealed class TycoonRun
    {
        private readonly RunRng _rng;
        private readonly Shelf _shelf;
        private readonly List<RecipeDefinition> _recipes;   // unlocked only; grows as recipes are bought

        /// <summary>The whole catalogue, locked recipes included — what the shop can sell.</summary>
        public IReadOnlyList<RecipeDefinition> AllRecipes { get; private set; }

        /// <summary>What the bar can offer tonight: the unlocked recipes — the list orders
        /// roll from and pours are matched against. The menu renders exactly this.</summary>
        public IReadOnlyList<RecipeDefinition> MenuRecipes => _recipes;
        private readonly List<IngredientCard> _brandCatalogue;
        private readonly List<IngredientCard> _lockedStock;   // bottles waiting on their recipes (v5 P16)
        private readonly RegularsRegistry _regulars;
        private readonly TycoonConfig _config;

        public TycoonPhase Phase { get; private set; } = TycoonPhase.DayOpen;
        public int Day { get; private set; } = 1;

        /// <summary>The till. Allowed to go negative — debt is the whole drama (GDD 23 §6).</summary>
        public int Money { get; private set; }

        public int Seats { get; private set; }
        public DayLedger Ledger { get; } = new DayLedger();
        public BarDay Floor { get; private set; }
        public Shelf Shelf => _shelf;
        public TycoonConfig Config => _config;

        // ── ambience upgrades (GDD 23 §8) ───────────────────────────────────────
        // Per-glass upgrade lines (the author, 2026-08-02): every glass TYPE is its own
        // SIX-step ladder — the 0★ base set, then 1★ through 4★, then the 5★ legendary
        // set. Tier here is 1-based: tier 1 = 0★, tier 6 = legendary.
        public const int MaxGlassTier = 6;
        private readonly Dictionary<string, int> _glassTiers = new Dictionary<string, int>();

        /// <summary>The upgrade tier of one glass line (1–6; the shown stars are tier−1).</summary>
        public int GlassTier(string glassId) =>
            glassId != null && _glassTiers.TryGetValue(glassId, out var t) ? t : 1;

        /// <summary>Paid steps across every line — what the star cap and ambience read.</summary>
        public int GlassUpgradeSteps
        {
            get { int s = 0; foreach (var g in _glassware) s += GlassTier(g.Id) - 1; return s; }
        }
        /// <summary>The counter's tier (1–3). It is the ONE fitting left: the back bar and the
        /// musician were deleted outright on 2026-08-04, and the counter was kept on the
        /// condition that it stops touching the scene and pays in a number instead (the
        /// author). Nothing reads it but <see cref="Ambience"/>.</summary>
        public int CounterTier { get; private set; } = 1;

        /// <summary>The satisfaction the bar adds to every served visit (GDD 23 §4's "plus
        /// ambience"). Two ladders with their own ceilings, so a finished glass cabinet cannot
        /// swallow the counter: the glassware LINES carry 0.15 (a RAISED ceiling — the old single
        /// ladder capped at 0.06 = 0.006×10; full at 25 steps now), and the counter
        /// carries 0.03 a step to 0.06.</summary>
        /// <remarks>
        /// The counter used to be worth nothing at all. It was cut out of this sum on
        /// 2026-08-02 along with the wall and the musician, and what it kept instead was a tint
        /// on the scene — so the fitting a player paid for changed the picture and not the
        /// night. That is now exactly inverted, on the author's instruction: it is a stat and
        /// only a stat. Pooling it into the 0.15 would have been the same as deleting it, since
        /// five lines of five steps reach 25 on their own.
        /// </remarks>
        public double Ambience => Math.Min(0.15, 0.006 * GlassUpgradeSteps)    // full at 25 steps
                                + Math.Min(0.06, 0.03 * (CounterTier - 1));

        /// <summary>The most stars the bar's fittings allow a night to bank (the author's
        /// loop, 2026-08-02): happy customers alone cannot carry a dive past two stars —
        /// the glassware line and the extra stools raise the ceiling toward five.</summary>
        /// <remarks>FRONT-LOADED per step (measured, 2026-08-02): a flat 0.12/step made a
        /// cap-star cost ~$383 against the ~$100 the healthy three-step economy paid, and
        /// the sim went 13% → 83% bankruptcies on that arithmetic. The early steps of a
        /// line now carry most of its ceiling (0.20/0.15/0.12/0.08/0.05 — 0.60 a line,
        /// +3.0 across five); the late, expensive steps are the endgame's prestige, not
        /// its survival math.</remarks>
        public double UpgradeStarCap
        {
            get
            {
                double glassCap = 0;
                foreach (var g in _glassware)
                {
                    int steps = GlassTier(g.Id) - 1;
                    for (int s = 0; s < steps && s < GlassStepCap.Length; s++)
                        glassCap += GlassStepCap[s];
                }
                // Counted from the CONFIG's opening seats, not a literal (audit 2026-08-11):
                // the hard-coded 3 against StartingSeats = 4 gave every new bar a free
                // quarter-star, against this property's own "a dive caps at two" promise.
                return 2.0 + glassCap + 0.25 * Math.Max(0, Seats - _config.StartingSeats);
            }
        }

        private static readonly double[] GlassStepCap = { 0.20, 0.15, 0.12, 0.08, 0.05 };

        /// <summary>The most stars tonight's MENU allows: a night that never served past
        /// the starter list caps low; only the stirred precision drinks open five.</summary>
        public double MenuStarCap =>
            _bestRankServedTonight <= 0 ? 2.0
            : _bestRankServedTonight <= 8 ? 2.5
            : _bestRankServedTonight <= 14 ? 3.5
            : _bestRankServedTonight <= 21 ? 4.5 : 5.0;

        private int _bestRankServedTonight;

        /// <summary>Today's crowd, decided by yesterday's satisfaction bar (GDD 23 §7).</summary>
        public WealthTier CrowdToday { get; private set; } = WealthTier.Regular;

        // ── the day's book, itemised for the invoice (GDD 24 §7) ────────────────
        public int DaySales { get; private set; }
        public int DayTips { get; private set; }
        public int DayRent { get; private set; }
        public int DayStock { get; private set; }
        public int DayUpgrades { get; private set; }
        public int DayIncome => DaySales + DayTips;
        public int DayExpenses => DayRent + DayStock + DayUpgrades;

        /// <summary>The shaker: the vessel you build the drink in (GDD 24 §2).</summary>
        public GlassContents Glass { get; private set; }

        /// <summary>The serving glass: what the shaker is poured into and handed over
        /// (GDD 24 §3). Empty until the serve pour, or filled perfectly by <see cref="ServeTo"/>.</summary>
        public GlassContents ServingGlass { get; private set; }

        /// <summary>Is there a drink that can actually be carried to a seat? Only what has been
        /// poured into the glass counts — the shaker is a step, not a drink.</summary>
        public bool DrinkReady => !ServingGlass.IsEmpty;

        /// <summary>A built drink that has not been poured out yet, so the UI can say so instead
        /// of quietly showing nothing when the player backs out of the flow.</summary>
        public bool DrinkWaitingInShaker => ServingGlass.IsEmpty && !Glass.IsEmpty;

        /// <summary>True once the shaker has been shaken this build (GDD 24 §2.5).</summary>
        public bool IsShaken { get; private set; }

        /// <summary>How hard the last shake was, 0–1 (GDD 24 §2.5). A craft hook for later;
        /// recorded now so the shake motion means something the moment it earns an effect.</summary>
        public double ShakeEnergy { get; private set; }

        /// <summary>True once the open tin has been stirred this build (GDD 21 §14). The
        /// verb <see cref="Preparations.Stirred"/> waited for arrived 2026-08-11, when the
        /// mandatory-mix rule made it load-bearing: every stirred recipe in the book names
        /// two spirits, so a bar without a spoon could only ever shake its Martinis.</summary>
        public bool IsStirred { get; private set; }

        /// <summary>How thorough the last stir was, 0–1 — <see cref="ShakeEnergy"/>'s twin,
        /// recorded on the same promise: plumbing now, craft effect at a later balance pass.</summary>
        public double StirEnergy { get; private set; }

        /// <summary>Has the tin been mixed at all — by tin or by spoon. The mandatory-mix
        /// rule (GDD 21 §14) demands MIXED, not mixed CORRECTLY: the judge does not read the
        /// recipe's prep method yet, so shaking a Martini is legal and merely wrong.</summary>
        public bool IsMixed => IsShaken || IsStirred;

        public string PouringId { get; private set; }

        private readonly List<MarketOffer> _marketOffers = new List<MarketOffer>();
        public IReadOnlyList<MarketOffer> MarketOffers => _marketOffers;

        // Bottles bought at the last close still wear a "NEW" flash on the menu today
        // (2026-07-23 inventory economy). Cleared when the next night's market opens.
        private readonly HashSet<string> _newStockIds = new HashSet<string>();
        public bool IsNewStock(string ingredientId) => _newStockIds.Contains(ingredientId);

        public TycoonRun(Shelf shelf, IReadOnlyList<RecipeDefinition> recipes, RunRng rng,
            TycoonConfig config = null, RegularsRegistry regulars = null,
            IReadOnlyList<IngredientCard> brandCatalogue = null,
            IReadOnlyList<GlasswareDefinition> glassware = null,
            IReadOnlyList<SnackDefinition> snacks = null,
            IReadOnlyList<IngredientCard> lockedStock = null,
            IReadOnlyList<FixtureDefinition> fixtures = null,
            StoryArc story = null)
        {
            _shelf = shelf ?? throw new ArgumentNullException(nameof(shelf));
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            // Locked recipes (v5 P10) exist in the catalogue but not in the bar: they are
            // neither rolled as orders nor matched against a pour until something unlocks
            // them. Filtered once here so every consumer — the order roll, the matcher, the
            // sim, the menu — sees the same world.
            var active = new List<RecipeDefinition>(recipes.Count);
            foreach (var recipe in recipes)
                if (!recipe.Locked) active.Add(recipe);
            _recipes = active;
            AllRecipes = recipes;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _config = config ?? TycoonConfig.Default;
            _regulars = regulars;
            _brandCatalogue = brandCatalogue != null
                ? new List<IngredientCard>(brandCatalogue) : new List<IngredientCard>();
            _lockedStock = lockedStock != null
                ? new List<IngredientCard>(lockedStock) : new List<IngredientCard>();

            _glassware = glassware ?? Array.Empty<GlasswareDefinition>();
            _snacks = snacks ?? Array.Empty<SnackDefinition>();
            _fixtureCatalogue = fixtures ?? Array.Empty<FixtureDefinition>();
            RestockSnacks();

            Money = _config.StartingMoney;
            Seats = _config.StartingSeats;
            Glass = new GlassContents(_config.GlassCapacity);
            ServingGlass = NewServingGlass(DefaultGlassware);
            Floor = new BarDay(Day, Seats, _config, _rng.GetStream("arrivals"));
            // The story is opt-in exactly like the regulars: a run built without an arc has
            // no last customer and behaves in every way like a run from before there was one.
            Story = story != null ? new StoryProgress(story) : null;
        }

        // ── the recipe book (v5 P16): buying the menu ───────────────────────────
        // P10 shipped twelve cocktails LOCKED and nothing ever unlocked them — dead content
        // behind a comment that said "until something unlocks them". This is the something:
        // recipes are bought at day end like stock, and the better ones are gated on the
        // bar's standing (C6/D3 — the rating drives unlocks; the P18 note staged it here
        // because variety without a door is not variety).

        private readonly HashSet<string> _boughtRecipes = new HashSet<string>();

        /// <summary>What the book still holds: locked recipes not yet bought.</summary>
        public IEnumerable<RecipeDefinition> LockedRecipes
        {
            get
            {
                foreach (var r in AllRecipes)
                    if (r.Locked && !_boughtRecipes.Contains(r.Id)) yield return r;
            }
        }

        /// <summary>
        /// The drinks ON THE MENU that call for this bottle style — what a shop listing is
        /// allowed to say a bottle is for.
        ///
        /// This lives in Core, not in the shop's text builder, because the shop got it wrong
        /// (2026-08-09): it walked <see cref="AllRecipes"/>, which carries the sealed book,
        /// and printed the names of the very drinks the SEALED crate hides behind its star
        /// gate. Hidden information stays hidden, and a rule the UI has to remember is a rule
        /// the UI will forget — so the only list the UI can reach is already filtered.
        /// </summary>
        public IEnumerable<RecipeDefinition> MenuDrinksUsingStyle(string style)
        {
            if (string.IsNullOrEmpty(style)) yield break;
            foreach (var r in _recipes)
                foreach (var band in r.RatioRequirements)
                    if (band.IsStyleBand && band.Style == style) { yield return r; break; }
        }

        /// <summary>The drinks ON THE MENU poured into this glass line — what a glassware
        /// listing is allowed to say the set serves. Sealed for the same reason.</summary>
        public IEnumerable<RecipeDefinition> MenuDrinksInGlass(string glassId)
        {
            if (string.IsNullOrEmpty(glassId)) yield break;
            foreach (var r in _recipes)
                if (r.GlassId == glassId) yield return r;
        }

        /// <summary>Every bottle style the MENU actually calls for — the only styles a
        /// filter may offer, since a chip for a style no visible drink uses is both a dead
        /// filter and a hint that something is waiting behind it.</summary>
        public IEnumerable<string> MenuStyles()
        {
            var seen = new HashSet<string>();
            foreach (var r in _recipes)
                foreach (var band in r.RatioRequirements)
                    if (band.IsStyleBand && seen.Add(band.Style)) yield return band.Style;
        }

        /// <summary>The stock the board is holding back tonight, and the standing each one
        /// waits for. The shop draws one tile from this so an early aisle does not read as
        /// a finished one.</summary>
        public IEnumerable<(IngredientCard Card, double Stars)> GatedStock() =>
            Market.GatedFor(_shelf, _brandCatalogue, Rating.Average);

        /// <summary>What a recipe costs to put on the menu, priced off its tier's rank —
        /// kept cheap enough that the menu can GROW at the pace the rent climbs, because the
        /// ladder of bought recipes is the income curve now (P16).</summary>
        public int RecipePrice(RecipeDefinition recipe) =>
            Math.Max(9, 5 + (5 * (recipe.Rank - 2)) / 2);

        /// <summary>Stars the room must say about this bar before the recipe sells (C6).
        /// Lowered under the caps they unlock (2026-08-02): with the menu cap in play, a
        /// starter-only bar tops out at 2.5 stars — the old 3.0 gate on the mid tier was
        /// therefore UNREACHABLE and the sim deadlocked every run at 2.5. Each gate now
        /// sits inside the band the previous tier can actually earn.</summary>
        /// <summary>
        /// The book's rungs. SPLIT AT ONE STAR (2026-08-10, the same note as the stock
        /// ladder): ranks 9–14 all opened at two, so the first star bought nothing here
        /// either and the second handed over six drinks at once. The band divides — the
        /// three easiest of them come at one star — and no recipe was added or moved out
        /// of the book to do it.
        /// </summary>
        public double RecipeStarGate(RecipeDefinition recipe) =>
            recipe.Rank <= 8 ? 0.0
            : recipe.Rank <= 11 ? 1.0
            : recipe.Rank <= 14 ? 2.0
            : recipe.Rank <= 21 ? 3.0
            : 4.0;

        /// <summary>
        /// Buys a locked recipe onto the menu (v5 P16). A day-end act, like every purchase:
        /// deliveries come when the doors are shut. Core refuses shortfalls of money and of
        /// reputation — from tomorrow the drink can be ordered, rolled and matched.
        /// </summary>
        public RecipeDefinition UnlockRecipe(string recipeId)
        {
            EnsurePhase(TycoonPhase.DayEnd);
            RecipeDefinition recipe = null;
            foreach (var r in AllRecipes)
                if (r.Id == recipeId) { recipe = r; break; }
            if (recipe == null || !recipe.Locked || _boughtRecipes.Contains(recipeId))
                throw new InvalidOperationException($"'{recipeId}' is not in the book to buy.");
            double gate = RecipeStarGate(recipe);
            if (Rating.Average < gate)
                throw new InvalidOperationException(
                    $"The room is not talking about this bar enough yet — {recipe.Name} wants {gate:0.0} stars.");
            int price = RecipePrice(recipe);
            if (Money < price)
                throw new InvalidOperationException($"Not enough money — {recipe.Name} costs ${price}.");

            Money -= price;
            DayUpgrades += price;
            _boughtRecipes.Add(recipeId);
            _recipes.Add(recipe);

            // The recipe brings its bottles with it (v5 P16). P10 quarantined the stock its
            // locked drinks needed, and nothing ever consumed the quarantine — a bought
            // margarita would have rolled orders no shelf could answer and no market could
            // fix. Buying the recipe releases every waiting bottle of its styles into the
            // catalogue, and the market re-rolls (deterministic, rng-free) so they are on
            // TONIGHT'S shop, not tomorrow's.
            var styles = new HashSet<string>();
            foreach (var band in recipe.RatioRequirements)
                if (!string.IsNullOrEmpty(band.Style)) styles.Add(band.Style);
            for (int i = _lockedStock.Count - 1; i >= 0; i--)
            {
                var card = _lockedStock[i];
                if (card.Info?.Style != null && styles.Contains(card.Info.Style))
                {
                    _brandCatalogue.Add(card);
                    _lockedStock.RemoveAt(i);
                }
            }
            RollMarket();
            _todayPurchases.Add(new DayPurchase(DayPurchase.Kind.Recipe, recipeId,
                recipe.Name, price));
            return recipe;
        }

        /// <summary>
        /// A page handed over, not bought (GDD 26 §4): the story's guest writes the recipe on
        /// the back of a napkin — "here is how it is built" — and the star gate and the price
        /// are waived, because the ask IS the teaching. It happens at the seat, mid-night, so
        /// it deliberately skips the day-end rule every other purchase obeys; nothing else in
        /// the game may use this door, which is why it takes no money and appears on no slip.
        ///
        /// Silent when the bar already knows the drink, or when nobody wrote it down.
        /// </summary>
        private void GrantRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return;
            RecipeDefinition recipe = null;
            foreach (var r in AllRecipes)
                if (r.Id == recipeId) { recipe = r; break; }
            if (recipe == null || !recipe.Locked || _boughtRecipes.Contains(recipeId)) return;

            _boughtRecipes.Add(recipeId);
            _recipes.Add(recipe);
            // The page brings its bottles with it, exactly as a bought one does — a recipe
            // whose stock is still quarantined is a recipe nobody can pour.
            var styles = new HashSet<string>();
            foreach (var band in recipe.RatioRequirements)
                if (!string.IsNullOrEmpty(band.Style)) styles.Add(band.Style);
            for (int i = _lockedStock.Count - 1; i >= 0; i--)
            {
                var card = _lockedStock[i];
                if (card.Info?.Style != null && styles.Contains(card.Info.Style))
                {
                    _brandCatalogue.Add(card);
                    _lockedStock.RemoveAt(i);
                }
            }
        }

        // ── today's purchases: same-day refunds (the author, 2026-08-02) ─────────
        // A slip of everything bought at THIS close. Refunding reverses the purchase in
        // place; at the next dawn the slip is torn up — yesterday's buys are final.

        public sealed class DayPurchase
        {
            public enum Kind { Brand, Recipe, Seat, Glassware, Counter, Fixture }
            public Kind What { get; }
            public string Id { get; }
            public string Name { get; }
            public int Price { get; }
            internal ShelfBottle Added;      // Brand: the bottle that came in
            internal MarketOffer Offer;
            internal DayPurchase(Kind what, string id, string name, int price)
            { What = what; Id = id; Name = name; Price = price; }
        }

        private readonly List<DayPurchase> _todayPurchases = new List<DayPurchase>();
        public IReadOnlyList<DayPurchase> TodaysPurchases => _todayPurchases;

        public void RefundToday(int index)
        {
            EnsurePhase(TycoonPhase.DayEnd);
            if (index < 0 || index >= _todayPurchases.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            var p = _todayPurchases[index];
            switch (p.What)
            {
                case DayPurchase.Kind.Brand:
                    // Brands only ever JOIN the shelf now (2026-08-02), so a refund is
                    // simply the bottle going back on the truck — and the board re-rolls
                    // (audit 2026-08-11): a recipe bought after this brand re-rolled the
                    // market underneath it, so the refunded bottle was off the shelf AND
                    // off the board, unbuyable for the rest of the night.
                    if (p.Added != null) _shelf.Remove(p.Added);
                    _newStockIds.Remove(p.Id);
                    p.Offer?.MarkUnsold();
                    RollMarket();
                    break;
                case DayPurchase.Kind.Recipe:
                    // Relocks the drink. The bottles it released stay in the catalogue —
                    // they are only offer candidates, and unwinding a market re-roll would
                    // cost more honesty than it buys.
                    _boughtRecipes.Remove(p.Id);
                    for (int i = _recipes.Count - 1; i >= 0; i--)
                        if (_recipes[i].Id == p.Id) { _recipes.RemoveAt(i); break; }
                    break;
                // Taking a fitting back gives tonight its fitting back too — otherwise a
                // misclick would cost the whole night's one decision (2026-08-07).
                case DayPurchase.Kind.Seat:
                    Seats--;
                    if (UpgradesToday > 0) UpgradesToday--;
                    break;
                case DayPurchase.Kind.Glassware:
                    if (_glassTiers.TryGetValue(p.Id, out var gt) && gt > 1) _glassTiers[p.Id] = gt - 1;
                    if (UpgradesToday > 0) UpgradesToday--;
                    break;
                case DayPurchase.Kind.Counter:
                    if (CounterTier > 1) CounterTier--;
                    if (UpgradesToday > 0) UpgradesToday--;
                    break;
                case DayPurchase.Kind.Fixture:
                    // Dressing is not a fitting, so there is no fitting to give back —
                    // the piece just goes back on the truck.
                    _fixtures.Remove(p.Id);
                    break;
            }
            Money += p.Price;
            DayUpgrades -= p.Price;
            _todayPurchases.RemoveAt(index);
        }

        /// <summary>One bottle refilled alone (the author: stock renews per-bottle now).</summary>
        public int RefillBottle(string ingredientId)
        {
            EnsurePhase(TycoonPhase.DayEnd);
            var bottle = _shelf.Find(ingredientId);
            if (bottle == null)
                throw new InvalidOperationException($"No '{ingredientId}' on the shelf.");
            int cost = (int)Math.Ceiling((bottle.Capacity - bottle.Remaining) * _config.RefillPricePerCapacity);
            if (cost == 0) return 0;
            EnsureAffordable(cost);
            Money -= cost;
            DayStock += cost;
            bottle.Refill();
            return cost;
        }

        /// <summary>
        /// Developer presets (the author's three game modes, 2026-08-02): jump a fresh run
        /// to the start (0, a no-op), the middle (1) or the endgame (2). Dev tooling only —
        /// it moves state directly and asks no one for money.
        /// </summary>
        public void DevPreset(int stage)
        {
            if (stage <= 0) return;
            bool late = stage >= 2;
            Money = late ? 600 : 160;
            Rating.DevSet(late ? 5.0 : 2.6);
            foreach (var g in _glassware)
                _glassTiers[g.Id] = late ? MaxGlassTier
                    : (g.Id == "rocks" || g.Id == "highball" ? 3 : 2);   // mid: 1–2★ lines
            while (Seats < (late ? _config.MaxSeats : 4)) Seats++;
            // The counter, which the preset used to leave at 1 (2026-08-04) — the last of the
            // fittings, and the only one it still has to buy.
            CounterTier = late ? _config.MaxAmbienceTier : 2;

            int rankCap = late ? int.MaxValue : 14;
            var toUnlock = new List<RecipeDefinition>();
            foreach (var r in AllRecipes)
                if (r.Locked && !_boughtRecipes.Contains(r.Id) && r.Rank <= rankCap)
                    toUnlock.Add(r);
            foreach (var r in toUnlock)
            {
                _boughtRecipes.Add(r.Id);
                _recipes.Add(r);
                var styles = new HashSet<string>();
                foreach (var band in r.RatioRequirements)
                    if (!string.IsNullOrEmpty(band.Style)) styles.Add(band.Style);
                for (int i = _lockedStock.Count - 1; i >= 0; i--)
                {
                    var card = _lockedStock[i];
                    if (card.Info?.Style != null && styles.Contains(card.Info.Style))
                    { _brandCatalogue.Add(card); _lockedStock.RemoveAt(i); }
                }
            }
            // The unlocked menu must be POURABLE for the playtest. The late preset is
            // "everything is bought" (the author, 2026-08-02), and since a better brand now
            // JOINS the shelf rather than replacing the well bottle, that has to mean every
            // rung of every line — otherwise the top-shelf cocktails the preset just
            // unlocked cannot be poured at all, because their bands name a brand tier. The
            // mid preset still carries one bottle a style, which is what a mid bar looks like.
            foreach (var card in _brandCatalogue)
            {
                if (card.Info?.Style == null) continue;
                bool missing = late
                    ? _shelf.Find(card.Id) == null
                    : Market.FindByStyle(_shelf, card.Info.Style) == null;
                if (missing) _shelf.Add(new ShelfBottle(card.Clone()));
            }
            // Same for the stock still waiting on its recipe: at the endgame there is no
            // recipe left locked, so nothing should still be in the back room.
            if (late)
                for (int i = _lockedStock.Count - 1; i >= 0; i--)
                {
                    var card = _lockedStock[i];
                    if (_shelf.Find(card.Id) == null) _shelf.Add(new ShelfBottle(card.Clone()));
                    _brandCatalogue.Add(card);
                    _lockedStock.RemoveAt(i);
                }
            RollMarket();
            Day = late ? 30 : 12;
            Floor = new BarDay(Day, Seats, _config, _rng.GetStream("arrivals"), Rating.Average);
            LastCustomer = null;       // a jumped night starts its last call from scratch
            LastCallBeat = null;
            Trial = null;
            _lastCallSpent = _lastCallAnswered = LastCallWithheld = false;
            Phase = TycoonPhase.DayOpen;
        }

        /// <summary>
        /// Dev tooling (the author, 2026-08-07): closes the night on the spot so the books
        /// and the shop can be looked at without playing a whole day. It runs the REAL
        /// clock — Tick, the same verb the floor uses — so everyone still seated drinks up
        /// or storms off exactly as they would have, the rent still lands, the market still
        /// rolls. The only thing skipped is the waiting. No-op outside an open day.
        /// </summary>
        public void DevSkipToDayEnd()
        {
            if (Phase != TycoonPhase.DayOpen) return;
            // A generous cap: a night is 95 seconds and the longest patience is under a
            // minute, so this lands long before it. It exists so a bug can never spin here.
            for (int guard = 0; guard < 20000 && Phase == TycoonPhase.DayOpen; guard++)
                Tick(0.25);
        }

        /// <summary>
        /// Dev tooling (2026-08-14, the author: "gün olarak da o zamana ışınlanılmasını
        /// istiyorum"): moves the calendar to <paramref name="day"/> so a written night can be
        /// looked at without playing the fortnight standing in front of it. Returns how many
        /// nights were skipped.
        ///
        /// IT STRIKES THE OPPOSITE BARGAIN TO <see cref="DevSkipToDayEnd"/>, ON PURPOSE.
        /// That one plays the night for real and skips only the waiting. This one does not
        /// play the nights at all — no rent, no takings, no entry in the books — because the
        /// honest version was measured and it does not work: closing and reopening real nights
        /// on a bar nobody is serving puts a fresh run under on day 4, which makes every beat
        /// past Wednesday unreachable by the tool built to reach them.
        ///
        /// So this is a VIEWING tool and says so. It is not a shortcut through the economy —
        /// nothing here earns anything — it is the calendar being wound forward with the till,
        /// the standing and the shelf left exactly where they were. The sim bot is what
        /// measures this game; a dev key that skipped rent would only lie to a person.
        /// </summary>
        public int DevJumpToNight(int day)
        {
            if (Phase != TycoonPhase.DayOpen || day <= Day) return 0;
            int from = Day;
            Day = day;
            // Everything ContinueToNextDay clears for a new night, minus the books.
            DaySales = DayTips = DayRent = DayStock = DayUpgrades = 0;
            UpgradesToday = 0;
            _bestRankServedTonight = 0;
            _todayPurchases.Clear();
            LastCustomer = null;
            LastCallBeat = null;
            Trial = null;
            _lastCallSpent = _lastCallAnswered = LastCallWithheld = false;
            BuyBackTheBowls();
            ResetVessels();
            Floor = new BarDay(Day, Seats, _config, _rng.GetStream("arrivals"), Rating.Average);
            return Day - from;
        }

        // ── snacks (v5 P16) ─────────────────────────────────────────────────────

        private readonly IReadOnlyList<SnackDefinition> _snacks;
        private readonly Dictionary<string, int> _snackLeft = new Dictionary<string, int>();

        /// <summary>The bowls this bar puts out. Empty for a run built without them, which
        /// keeps the bench setups and the older tests snack-free.</summary>
        public IReadOnlyList<SnackDefinition> Snacks => _snacks;

        /// <summary>What is left in a bowl today; 0 for an unknown id.</summary>
        public int SnackLeft(string snackId) =>
            snackId != null && _snackLeft.TryGetValue(snackId, out var n) ? n : 0;

        /// <summary>Opening day's bowls come with the bar. Only the constructor fills free.</summary>
        private void RestockSnacks()
        {
            foreach (var snack in _snacks) _snackLeft[snack.Id] = snack.Stock;
        }

        /// <summary>
        /// The morning snack delivery (v5 P16): every unit eaten yesterday is bought back at
        /// one dollar under its menu price, so a bowl nets exactly $1 — "small income" made
        /// literal. The first pass filled the bowls FREE, and the sim caught what that did:
        /// ~$11 a night of costless money wiped the rent-v2 squeeze whole (bankruptcies
        /// 19.5% → 0%). Purchases require cash (GDD 23): the delivery fills unit by unit as
        /// far as the till reaches, so a bar under water opens with thin bowls — which is a
        /// small extra tooth on exactly the bar the squeeze is for.
        /// </summary>
        private void BuyBackTheBowls()
        {
            foreach (var snack in _snacks)
            {
                int unitCost = Math.Max(1, snack.Price - 1);
                while (_snackLeft[snack.Id] < snack.Stock && Money >= unitCost)
                {
                    Money -= unitCost;
                    DayStock += unitCost;
                    _snackLeft[snack.Id]++;
                }
            }
        }

        /// <summary>
        /// Puts a bowl in front of a seated customer (v5 P16). Core refuses everything the
        /// design forbids, so no menu wiring can create a solo snack: they must have a DRINK
        /// order open (never alone — the pairing rule), the bowl must have something in it,
        /// and they must still be at the bar. The price rides their tab and settles on the
        /// way out with everything else.
        /// </summary>
        public SnackDefinition ServeSnack(string snackId, CustomerVisit visit)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (visit == null) throw new ArgumentNullException(nameof(visit));

            SnackDefinition snack = null;
            foreach (var s in _snacks)
                if (s.Id == snackId) { snack = s; break; }
            if (snack == null)
                throw new InvalidOperationException($"No bowl of '{snackId}' at this bar.");
            if (SnackLeft(snackId) <= 0)
                throw new InvalidOperationException($"The {snack.Name} bowl is empty today.");

            bool atTheBar = (visit.State == VisitState.Waiting || visit.State == VisitState.Drinking)
                            && Floor.Seated.Contains(visit);
            if (!atTheBar)
                throw new InvalidOperationException("That customer is not at the bar.");
            // Never alone (GDD 23, the pairing rule): a snack rides an alcoholic order. A
            // customer still reading the menu has not ordered one, so the bowl waits.
            if (!visit.HasOrdered)
                throw new InvalidOperationException(
                    "Snacks ride a drink order — they have not ordered one yet.");

            _snackLeft[snackId]--;
            visit.AddSnack(snack.Price);
            return snack;
        }

        // ── glassware (v5 P14 / C9) ─────────────────────────────────────────────

        private readonly IReadOnlyList<GlasswareDefinition> _glassware;

        /// <summary>The glass set this bar owns. Empty for a run built without one, which is
        /// what keeps the bench setups and the older tests on the single 1.0 glass.</summary>
        public IReadOnlyList<GlasswareDefinition> Glassware => _glassware;

        /// <summary>Which glass is on the counter right now; null when the bar has no set.</summary>
        public GlasswareDefinition ServingGlassware { get; private set; }

        private GlasswareDefinition GlassNamed(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var g in _glassware)
                if (g.Id == id) return g;
            return null;
        }

        /// <summary>What stands on the counter with nothing in it.</summary>
        private GlasswareDefinition DefaultGlassware => GlassNamed(_config.DefaultGlassId);

        /// <summary>Beer's recipe, so the tap can ask for its glass by name rather than by
        /// hardcoding "pint" in two places.</summary>
        private RecipeDefinition DraughtRecipe
        {
            get
            {
                foreach (var recipe in _recipes)
                    foreach (var req in recipe.Requirements)
                        foreach (var type in req.Types)
                            if (type == IngredientType.Beer) return recipe;
                return null;
            }
        }

        private GlassContents NewServingGlass(GlasswareDefinition glass)
        {
            ServingGlassware = glass;
            return new GlassContents(glass?.Capacity ?? _config.GlassCapacity);
        }

        /// <summary>
        /// Puts the drink's own glass on the counter (C9, the notes' auto-selection). Called
        /// the moment a drink commits to a glass — the pour out of the shaker, the first pull
        /// on the tap — and refused once there is anything in the glass, because swapping a
        /// vessel under liquid is either a spill or a free top-up depending on which way the
        /// capacity moved. A bar with no glass set keeps the single 1.0 glass and this is a
        /// no-op, which is what keeps the emotion-free bench runs valid.
        /// </summary>
        private void SelectGlassFor(RecipeDefinition recipe)
        {
            if (_glassware.Count == 0 || !ServingGlass.IsEmpty) return;
            var glass = GlassNamed(recipe?.GlassId) ?? GlassNamed(_config.DefaultGlassId);
            if (glass == null || ReferenceEquals(glass, ServingGlassware)) return;
            ServingGlass = NewServingGlass(glass);
        }

        // ── the floor clock ─────────────────────────────────────────────────────

        /// <summary>
        /// Advances the day: patience, departures, arrivals. Returns whoever just sat
        /// down. When the last planned customer has left, the day closes into
        /// <see cref="TycoonPhase.DayEnd"/> and the rent lands on the books.
        /// </summary>
        public IReadOnlyList<CustomerVisit> Tick(double seconds)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            var seated = Floor.Tick(seconds, NextArrival);
            SettleDepartures();
            Trial?.Waited(seconds);   // the talking backstop's clock, nothing else's
            seated = SettleLastCall(seated);

            if (Floor.IsComplete)
            {
                int rent = _config.Rent(Day);
                Money -= rent;
                DayRent += rent;
                RollMarket();
                Phase = TycoonPhase.DayEnd;
            }
            return seated;
        }

        /// <summary>
        /// Collects the tab of everyone who has left since the last tick. One pass over the
        /// night's leavers, idempotent by <see cref="CustomerVisit.TabSettled"/>; runs before
        /// the day can close, so the last leaver's money is in the till the rent lands on.
        /// </summary>
        private void SettleDepartures()
        {
            foreach (var visit in Floor.Finished)
            {
                if (visit.TabSettled) continue;
                visit.SettleTab();
                if (visit.Paid <= 0) continue;
                Money += visit.Paid;
                DaySales += visit.PaidBase;
                DayTips += visit.Paid - visit.PaidBase;
            }
        }

        // ── the last customer (GDD 26) ──────────────────────────────────────────

        /// <summary>Where this run has got to in the written nights, or null for a run with
        /// no story — the same opt-in the regulars have.</summary>
        public StoryProgress Story { get; }

        /// <summary>Which night of the week this is. The plaque has printed it since long
        /// before it meant anything; the story is what made it a rule (GDD 26 §2b).</summary>
        public BarNight Tonight => BarCalendar.NightOf(Day);

        /// <summary>Friday or Saturday — the nights a guest can walk in on.</summary>
        public bool IsWeekend => BarCalendar.IsWeekend(Day);

        /// <summary>The scripted guest while they are on their stool, and null every other
        /// moment. The one visit the arc is watching.</summary>
        public CustomerVisit LastCustomer { get; private set; }

        /// <summary>The beat being played tonight. Set when they sit down and kept until the
        /// night is closed, so the last word can still be said after they have gone.</summary>
        public StoryBeat LastCallBeat { get; private set; }

        /// <summary>Tonight's attempt at the beat's trial — what the post-it is drawn from
        /// (GDD 26 §4). Null on a night with no last customer, and null on a night the guest
        /// came but did not order (see <see cref="LastCallWithheld"/>).</summary>
        public StoryTrialRun Trial { get; private set; }

        /// <summary>
        /// Tonight's guest came in, but the bar has not reached the standing they came for
        /// (GDD 26 §12) — so they are on the stool with no order and no clock, and what they
        /// say is <see cref="StoryLines.ShortOfGate"/> instead of the ask. The beat stands and
        /// comes back; nothing about tonight was wrong, it was early.
        /// </summary>
        public bool LastCallWithheld { get; private set; }

        /// <summary>How many stars tonight's guest came for, or 0 when nobody is due. What
        /// the plate prints when it has to say what the bar is short of.</summary>
        public double LastCallNeedsStars => LastCallBeat?.RequiresStars ?? 0;

        private bool _lastCallSpent;      // one last customer a night, served or not
        private bool _lastCallAnswered;   // the arc has been told how tonight went

        /// <summary>How long the guest stays on the stool after the trial ends, so the beat
        /// can be finished out loud (GDD 26 §7). Long enough for two lines and a look at
        /// them; the night cannot close while they are sitting there, which is the point.</summary>
        public const double LastWordSeconds = 12.0;

        /// <summary>
        /// The last customer, in the one place the night is already settled (GDD 26 §2). Two
        /// things happen here and nowhere else: the scripted guest is let in once the room has
        /// emptied at closing time, and the arc is told when they get up without an answer.
        ///
        /// It runs INSIDE <see cref="Tick"/>, after the floor has settled and before the day's
        /// end condition is read — because that condition is one line in one place
        /// (<see cref="BarDay.IsComplete"/>: the door is shut and the last stool is empty) and
        /// a guest on a stool already satisfies it. The beat needs no second way to end a day.
        /// </summary>
        private IReadOnlyList<CustomerVisit> SettleLastCall(IReadOnlyList<CustomerVisit> seated)
        {
            if (Story == null) return seated;

            // The talking cannot hold the night hostage: a plate nobody dismisses — a UI bug,
            // a headless run that forgot the verb — would hold a clock that never starts and
            // a day that never ends. Long past any real dialogue, the trial starts itself.
            if (Trial != null && Trial.State == TrialState.Talking && LastCustomer != null
                && Trial.TalkedFor >= StoryTrial.TalkingGrace)
                BeginLastCallTrial();

            // Up and gone: the trial finished, or its clock ran out under them. Nobody having
            // answered them is itself an answer, and the beat comes back for it.
            if (LastCustomer != null && !Floor.Seated.Contains(LastCustomer))
            {
                if (!_lastCallAnswered)
                {
                    Trial?.Fail();          // the clock beat the bar
                    Story.RecordMissed(Day);
                    _lastCallAnswered = true;
                }
                LastCustomer = null;
            }

            if (_lastCallSpent || LastCustomer != null) return seated;
            if (!Floor.IsClosingTime || Floor.Seated.Count > 0) return seated;
            if (!Story.IsDueOn(Day)) return seated;

            // Scripted through and through, and ON THE HOUSE (GDD 26 §3): the drinks are the
            // beat's, the clock is written down, no die is rolled — and nothing they do
            // reaches the till or the standing. They said who they were on the way in, so the
            // licence is already read: the ask is behind a CONVERSATION tonight, not a card.
            var beat = Story.Current;
            // THE GATE IS ASKED AT THE STOOL, NOT AT THE DOOR (GDD 26 §12). They come either
            // way; what changes is whether there is a job tonight. A guest whose rung the bar
            // has not reached sits down, says what it is short of, and goes home — so the
            // lock is a scene the player watches rather than a night that quietly does not
            // happen. Nothing is fumbled, so the arc books it in its own column.
            LastCallWithheld = !Story.GateOpenFor(Rating.Average);
            var visit = new CustomerVisit(
                new DrinkOrder(beat.Trial.Headline, PriceOf(beat.Trial.Headline), ServingSpec.Plain),
                LastCallWithheld ? LastWordSeconds : beat.Trial.Seconds,
                Story.PersonFor(beat.Who), onTheHouse: true);
            visit.InspectId();
            Floor.SeatGuest(visit);
            LastCustomer = visit;
            LastCallBeat = beat;
            // NO TRIAL, SO NO POST-IT. The note on the screen is drawn from `Trial`, and a
            // night with nothing being asked for must not put a job on the wall.
            Trial = LastCallWithheld ? null : new StoryTrialRun(beat.Trial);
            if (LastCallWithheld)
            {
                // The clock is theirs and it is short: they stay the length of the last word,
                // which is the same stay a finished beat gets. The arc is told now rather than
                // when they stand up, so nothing depends on a stool emptying on time.
                visit.ReleaseClock();
                Story.RecordTurnedAway(Day);
                _lastCallAnswered = true;
            }
            else
            {
                // The ask can hand its own page over (GDD 26 §4): a drink the book has not got
                // and the star gate will not open for weeks is written down for you at the bar,
                // so the trial is a job and not a wall.
                GrantRecipe(beat.GrantsRecipeOnAsk);
                _lastCallAnswered = false;
            }
            _lastCallSpent = true;

            // Whoever just sat down is what the caller draws. Leaving the guest out of this
            // list would put somebody on a stool that nobody can see.
            var all = new List<CustomerVisit>(seated.Count + 1);
            all.AddRange(seated);
            all.Add(visit);
            return all;
        }

        /// <summary>
        /// The talking is over and the clock starts (GDD 26 §3.1). One verb, called by whoever
        /// finished the dialogue — the UI when the plate is done, the bot the moment it sits
        /// down — so that a headless run and a played one measure the same seconds.
        /// </summary>
        public void BeginLastCallTrial()
        {
            // A withheld night has no trial to start (GDD 26 §12): the guest is sitting there
            // saying why there is no order, and the plate ends the same way it always does.
            // Whoever dismisses the dialogue must not have to know which kind of night it is.
            if (LastCallWithheld) return;
            if (LastCustomer == null || Trial == null)
                throw new InvalidOperationException("Nobody is waiting on the last call.");
            Trial.Begin();
            LastCustomer.ReleaseClock();
        }

        /// <summary>
        /// One drink, held against the standard nothing else in this game is held against
        /// (GDD 26 §4): exactly what was asked for, made the way the book says, garnished the
        /// way they asked. The fill is the one forgiving part — nine tenths of the glass is a
        /// poured glass.
        ///
        /// It is the ORDINARY judge doing the measuring; only the pass mark is the trial's.
        /// Nothing is priced, nothing is tipped and nothing is rated: the verdict that comes
        /// back is a reaction, not a receipt.
        /// </summary>
        private ServiceVerdict ServeTheTrial(CustomerVisit visit, GlassContents delivered)
        {
            var match = RatioRecipeMatcher.Match(delivered, _recipes, IngredientOf);
            var matchKind = ServiceJudge.Compare(visit.OrderTruth, match, delivered, IngredientOf);

            var spec = visit.OrderTruth.Spec;
            double specScore = spec.Delivered(delivered);
            double methodScore = ServiceJudge.MethodScore(visit.OrderTruth.Wanted, delivered);
            double fill = delivered?.FillFraction ?? 0;
            bool perfect = matchKind == OrderMatch.Exact
                           && specScore >= 1.0 && methodScore >= 1.0
                           && fill >= Trial.Trial.MinFill;

            visit.ServedGlassId = ServingGlassware?.Id;
            if (perfect) Trial.Landed(); else Trial.Fumbled();

            if (Trial.State == TrialState.Pouring)
            {
                // Still going: the next drink, or the same one again if that was wrong. One
                // clock — AskFor does not touch it, which is the whole difference between a
                // demand and the extra round a good serve earns.
                visit.AskFor(new DrinkOrder(Trial.Current, PriceOf(Trial.Current), ServingSpec.Plain));
            }
            else
            {
                // Passed or out of mistakes: they get up either way, owing nothing — but not
                // on this tick. The beat's last words are said now, and a stool that emptied
                // instantly ended the night on top of them (GDD 26 §7).
                visit.GetUp(Trial.State == TrialState.Passed ? 1.0 : 0.0, LastWordSeconds);
                AnswerLastCall(Trial.State == TrialState.Passed);
            }

            ResetVessels();
            return new ServiceVerdict(matchKind, 0, 0, perfect, false,
                perfect ? 1.0 : 0.0, specScore, fill);
        }

        /// <summary>
        /// Tells the arc how tonight's beat went. Called at the SERVE and at the decline —
        /// not when the guest gets up — because the words that answer a beat have to land on
        /// the drink that earned them. A right serve moves the story on; anything else leaves
        /// it standing and sets the return clock.
        /// </summary>
        private void AnswerLastCall(bool servedRight)
        {
            if (Story == null || _lastCallAnswered) return;
            _lastCallAnswered = true;
            // The reward is data already (GDD 26 §6); paying it out lands in PLAN S5, with
            // the writing, so that the money and the pages arrive with the words that explain
            // them rather than out of a silent night.
            if (servedRight) Story.RecordServed(Day);
            else Story.RecordMissed(Day);
        }

        /// <summary>
        /// The honest no, for the one customer who cannot simply be ignored (GDD 26 §5). It is
        /// the ordinary <see cref="DeclineOrder"/> — the same mark on the night, the same
        /// stool freed now instead of in thirty seconds — with the arc's bookkeeping attached,
        /// so the plate's SAY NO key and the bar's own rule are never two different things.
        /// </summary>
        public ServiceVerdict DeclineLastCall()
        {
            if (LastCustomer == null)
                throw new InvalidOperationException("Nobody is waiting on the last call.");
            return DeclineOrder(LastCustomer);
        }

        private CustomerVisit NextArrival()
        {
            var order = RollOrder();
            var patienceRng = _rng.GetStream("patience");
            double patience = _config.RollPatience(Day, patienceRng);
            // The asking wait rides the same stream, drawn right after the drink wait, so a
            // seed still reproduces a night exactly (2026-08-02).
            double askPatience = _config.RollOrderPatience(Day, patienceRng);
            double decide = _config.RollDecideDelay(_rng.GetStream("decide"));
            if (_regulars == null)
                return new CustomerVisit(order, patience, decideSeconds: decide,
                    orderPatienceSeconds: askPatience);

            // A returning face or a fresh roll — the person persists; what is left of them
            // after the emotion demolition (2026-08-02) is who they ARE: name, visits,
            // relationship. The "read" stream still burns a draw so old seeds stay close.
            var regular = _regulars.RollNext(_rng.GetStream("customer"));
            _rng.GetStream("read").NextInt(100);

            return new CustomerVisit(order, patience, regular, decide, askPatience);
        }

        private DrinkOrder RollOrder()
        {
            var order = DrinkOrder.Roll(_recipes, Day, _config, _rng.GetStream("orders"));
            int price = PriceOf(order.Wanted);
            return price == order.Price
                ? order
                : new DrinkOrder(order.Wanted, price, order.Spec);   // keep how they want it served
        }

        /// <summary>What the bar charges for a drink tonight: the menu price, plus what the
        /// premium spirits on the shelf earn it, scaled by the crowd. No dice anywhere in it,
        /// which is why the scripted last call is priced through the same line as the crowd.</summary>
        private int PriceOf(RecipeDefinition recipe) => Math.Max(1, (int)Math.Round(
            (DrinkOrder.MenuPrice(recipe) + PremiumFor(recipe)) * _config.PriceMultiplier(CrowdToday),
            MidpointRounding.AwayFromZero));

        /// <summary>The premium a drink earns from the shelf's stock (GDD 23 §3, 2026-07-23):
        /// for each spirit/bitter the recipe needs, the best bottle above the base tier adds
        /// <see cref="TycoonConfig.StockPremiumPerTier"/> to the price. A basic bar adds nothing.</summary>
        private int PremiumFor(RecipeDefinition recipe)
        {
            // STYLE BANDS ARE THE RULE, NOT THE EXCEPTION (audit 2026-08-11). The old loop
            // read band.Type — which the style-band constructor leaves at its default, and
            // the default of IngredientType is Spirit. So all 51 style-banded recipes
            // collapsed to "one premium, from the best Spirit anywhere on the shelf":
            // a Vodka Soda priced up by your tier-4 bourbon, a Negroni earning one premium
            // for three bottles. The premium now comes from THE bottle the band names —
            // per distinct alcoholic style, the bottle of that style actually on the shelf.
            if (recipe.RatioRequirements == null || recipe.RatioRequirements.Count == 0) return 0;
            int premium = 0;
            var countedStyles = new HashSet<string>();
            var countedTypes = new HashSet<IngredientType>();
            foreach (var band in recipe.RatioRequirements)
            {
                if (band.IsStyleBand)
                {
                    if (!countedStyles.Add(band.Style)) continue;
                    // The best bottle OF THAT STYLE on the shelf — a T1 and a T3 gin can
                    // stand together (brands only ever join), and the drink pours the best.
                    int tier = 0; string category = null; var type = default(IngredientType);
                    foreach (var b in _shelf.Bottles)
                    {
                        var inf = b.Ingredient.Info;
                        if (inf == null || inf.Style != band.Style || inf.Tier <= tier) continue;
                        tier = inf.Tier; category = inf.Category; type = b.Ingredient.Type;
                    }
                    if (tier <= 1) continue;
                    if (!IngredientCategories.IsAlcoholic(category, type)) continue;
                    premium += (tier - 1) * _config.StockPremiumPerTier;
                }
                else
                {
                    if (band.Type != IngredientType.Spirit && band.Type != IngredientType.Bitter) continue;
                    if (!countedTypes.Add(band.Type)) continue;   // one premium per alcohol type
                    int bestTier = 1;
                    foreach (var bottle in _shelf.Bottles)
                        if (bottle.Ingredient.Type == band.Type)
                            bestTier = Math.Max(bestTier, bottle.Ingredient.Info?.Tier ?? 1);
                    premium += (bestTier - 1) * _config.StockPremiumPerTier;
                }
            }
            return premium;
        }

        // ── building the drink (pour verbs, GDD 21 §3 unchanged) ────────────────

        public void BeginPour(string ingredientId)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            ShakerIngredient(ingredientId, nameof(ingredientId));
            PouringId = ingredientId;
        }

        /// <summary>
        /// The shelf item behind an id, refusing anything the shaker must never hold. Beer is the
        /// one such thing (GDD 21 §10): it belongs to the tap, and a glass of it built in the
        /// shaker would still read as a draught to the matcher while carrying no head at all —
        /// a perfect pint with the whole mechanic skipped. The rule lives here rather than in the
        /// menu because the sim and the tests use these verbs too (2026-07-27).
        /// </summary>
        private ShelfBottle ShakerIngredient(string ingredientId, string argName)
        {
            var bottle = _shelf.Find(ingredientId);
            if (bottle == null)
                throw new ArgumentException($"No '{ingredientId}' on the shelf.", argName);
            if (bottle.Ingredient.Type == IngredientType.Beer)
                throw new ArgumentException(
                    $"'{ingredientId}' is a keg — beer is pulled into the glass, never built in the shaker.",
                    argName);
            // FIZZ COMES INTO THE TIN NOW (2026-08-14, the author: "tüm içecekler shakera
            // koyulacak, soda gibi gazlı içecekler karıştırılacak tarife göre"). The old
            // refusal sent carbonated straight to the serving glass, which gave the bar two
            // building places and left the tin holding half a drink; the method a recipe
            // asks for is what decides whether it is worked, and Built is a method. The
            // serving glass keeps its own door (PourAtGlass) — the tap and the sim use it —
            // but nothing is barred from the tin except the keg.
            return bottle;
        }

        /// <summary>
        /// Pours straight from the shelf into the serving glass (v5 P10) — how a drink is
        /// BUILT rather than shaken, and the only door carbonated ingredients have. Draws real
        /// stock, lands brim-capped like every pour. Beer stays the tap's business.
        /// </summary>
        public double PourAtGlass(string ingredientId, double volume)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            var bottle = _shelf.Find(ingredientId);
            if (bottle == null)
                throw new ArgumentException($"No '{ingredientId}' on the shelf.", nameof(ingredientId));
            if (bottle.Ingredient.Type == IngredientType.Beer)
                throw new ArgumentException(
                    $"'{ingredientId}' is a keg — beer is pulled at the tap, not poured by hand.",
                    nameof(ingredientId));
            if (volume <= 0) return 0;
            // Capped by true headroom BEFORE the shelf draws: the shelf's own cap ignores the
            // head on a pint, and stock drawn for room the foam already owns would evaporate.
            double asked = volume;
            volume = Math.Min(volume, ServingGlass.Headroom);
            double poured = _shelf.PourInto(ServingGlass, ingredientId, volume);
            // THE DRINK DECLARES ITSELF AT THE GLASS (audit 2026-08-11). Built drinks only
            // become themselves here — the tin can never hold the fizz — so the vessel
            // chosen at the pour-out was matched against HALF a drink: pure gin read as a
            // neat pour and every Gin & Tonic in the game was served in a rocks glass,
            // its own highball dead data. When the glass-side pour names a recipe whose
            // vessel differs, the barman moves the drink to the proper glass — an upgrade
            // re-pour, only ever into a glass with room for all of it, so the no-spill
            // law holds and the "never swap under a drink" rule gains its one honest
            // exception: the moment the drink stops being what the tin thought it was.
            if (poured > 0)
            {
                RevesselFor(RatioRecipeMatcher.Match(ServingGlass, _recipes, IngredientOf)?.Recipe);
                // The old glass's brim capped the pour BEFORE the drink declared itself:
                // whatever the new, roomier vessel can still take of what was asked, it
                // takes now — otherwise the upgrade moment short-pours by exactly the
                // difference between the two glasses.
                double rest = Math.Min(asked - poured, ServingGlass.Headroom);
                if (rest > 1e-9)
                    poured += _shelf.PourInto(ServingGlass, ingredientId, rest);
            }
            return poured;
        }

        /// <summary>Moves the serving glass's whole content into <paramref name="recipe"/>'s
        /// own vessel when it differs and fits. See <see cref="PourAtGlass"/>.</summary>
        private void RevesselFor(RecipeDefinition recipe)
        {
            if (_glassware.Count == 0 || recipe == null) return;
            var glass = GlassNamed(recipe.GlassId);
            if (glass == null || ReferenceEquals(glass, ServingGlassware)) return;
            if (glass.Capacity < ServingGlass.TotalVolume) return;   // never spill by re-pouring
            var next = NewServingGlass(glass);
            ServingGlass.TransferInto(next, ServingGlass.TotalVolume, accuracy: 1.0);
            ServingGlass = next;
        }

        public double PourTick(double seconds)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (PouringId == null || seconds <= 0) return 0;

            var bottle = _shelf.Find(PouringId);
            double poured = _shelf.PourInto(Glass, PouringId, bottle.PourRate * seconds);
            if (poured > 0) UnmixTin();
            if (poured <= 0 || bottle.IsEmpty) PouringId = null;
            return poured;
        }

        /// <summary>
        /// A pour into the tin UN-mixes it (audit 2026-08-11). Without this, a splash of
        /// gin shaken early stamped the whole build "mixed": pour vermouth after and the
        /// two-spirit tin walked straight past the mandatory-mix gate — and the stale
        /// `shaken` step rode into the serving glass to collect full method credit from
        /// the judge for a drink that was hand-built. New liquid means mix again.
        /// </summary>
        private void UnmixTin()
        {
            if (!IsShaken && !IsStirred) return;
            IsShaken = false; ShakeEnergy = 0;
            IsStirred = false; StirEnergy = 0;
            Glass.RemovePreparation(Preparations.Shaken.Id);
            Glass.RemovePreparation(Preparations.Stirred.Id);
        }

        public void EndPour() => PouringId = null;

        public double PourMeasure(string ingredientId, double volume)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            ShakerIngredient(ingredientId, nameof(ingredientId));
            if (volume <= 0) return 0;
            double poured = _shelf.PourInto(Glass, ingredientId, volume);
            if (poured > 0) UnmixTin();
            // The right glass stands ready WHILE the drink is built (2026-07-31): the counter
            // retargets as the tin's contents start naming a recipe, so the serve stage opens
            // with the vessel already correct instead of swapping it in front of the player.
            // SelectGlassFor refuses once the serving glass holds liquid, so nothing is ever
            // swapped under a drink — the pinned rule stands.
            if (poured > 0 && ServingGlass.IsEmpty)
                SelectGlassFor(RatioRecipeMatcher.Match(Glass, _recipes, IngredientOf)?.Recipe);
            return poured;
        }

        /// <summary>One garnish tap = a fixed pinch (GDD 21 §3).</summary>
        public double PourGarnish(string ingredientId) =>
            PourMeasure(ingredientId, GarnishClickFraction * Glass.Capacity);

        // ── the tap (GDD 21 §10) ────────────────────────────────────────────────

        /// <summary>The keg being pulled, or null. Beer never enters the shaker.</summary>
        public string PullingId { get; private set; }

        /// <summary>
        /// Opens a keg's tap. Beer skips the shaker entirely — it is pulled straight into the
        /// glass the customer drinks from, which is what makes it the four-second order.
        /// </summary>
        public void BeginPull(string kegId)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            var keg = _shelf.Find(kegId);
            if (keg == null)
                throw new ArgumentException($"No '{kegId}' on the shelf.", nameof(kegId));
            if (keg.Ingredient.Type != IngredientType.Beer)
                throw new ArgumentException($"'{kegId}' is not a keg — it cannot be pulled.", nameof(kegId));
            if (!Glass.IsEmpty)
                throw new InvalidOperationException("There is a cocktail on the go — bin it before pulling a pint.");
            // A pint goes into a clean glass. Topping up the same pint is fine — that is what a
            // second pull IS — but anything else already standing in it is a different drink, and
            // beer poured on top of it would go out as one (2026-07-28). This became reachable
            // the moment the serve pour stopped being optional: the finished cocktail now waits
            // in the serving glass rather than in the shaker, where !Glass.IsEmpty caught it.
            if (!ServingGlass.IsEmpty && ServingGlass.VolumeOf(kegId) <= 0)
                throw new InvalidOperationException(
                    "There is already a drink in that glass — serve it or bin it before pulling a pint.");
            // The matched pair holds (audit 2026-08-11): CanPull greys the key on a full
            // glass, so the verb refuses the same fact instead of being quietly looser.
            if (ServingGlass.IsFull)
                throw new InvalidOperationException("The glass is full — serve it or bin it.");
            // Beer goes in a pint (v5 P14 / C9) — the one glass the bar reaches for without
            // being told, and the reason draught is the drink you can put down in four seconds.
            SelectGlassFor(DraughtRecipe);
            PullingId = kegId;
        }

        /// <summary>Whether that keg could be opened right now, so the menu can grey the key
        /// instead of offering a tap that would refuse the glass in front of it.</summary>
        public bool CanPull(string kegId) =>
            Phase == TycoonPhase.DayOpen && Glass.IsEmpty && !ServingGlass.IsFull &&
            (ServingGlass.IsEmpty || ServingGlass.VolumeOf(kegId) > 0);

        /// <summary>How much beer has run past the rim this build. Waste, nothing more — it
        /// came out of the keg and reached nobody (GDD 21 §10.2).</summary>
        public double SpilledBeer { get; private set; }

        /// <summary>
        /// One moment under the open tap with the glass held at <paramref name="tiltDegrees"/>
        /// from upright. Returns the beer that landed; the foam that came with it is on the
        /// glass's head, what missed the rim is spilled, and all three came out of the keg.
        /// Running the keg dry closes the tap.
        /// </summary>
        public double PourTilted(double seconds, double tiltDegrees)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (PullingId == null || seconds <= 0) return 0;

            // A full glass ends the pour (2026-07-30). It used to take nothing while the tap went
            // on running, which is a strange thing to watch and, held past the spill angle, a
            // strange thing to pay for: the keg kept giving up beer for the floor. The head still
            // settles, so a pint that was full a moment ago makes room and can be topped up —
            // that is what a second pull is for.
            if (ServingGlass.IsFull) return 0;

            var keg = _shelf.Find(PullingId);
            var flow = TapPour.Flow(tiltDegrees, seconds);
            if (flow.Total <= 0) return 0;

            // The keg gives up beer, foam and spill alike; the glass only limits what it catches.
            double wanted = Math.Min(flow.Caught, ServingGlass.Headroom) + flow.Spill;
            double available = keg.Draw(wanted);
            if (available <= 0)
            {
                if (keg.IsEmpty) PullingId = null;
                return 0;
            }

            double share = wanted > 0 ? available / wanted : 0;
            double landed = ServingGlass.Add(PullingId, flow.Beer * share);
            ServingGlass.AddHead(flow.Head * share);
            SpilledBeer += flow.Spill * share;
            if (landed > 0 || flow.Head * share > 0) ServingGlass.AddPreparation(Preparations.Draught);
            if (keg.IsEmpty) PullingId = null;
            return landed;
        }

        public void EndPull() => PullingId = null;

        /// <summary>
        /// Lets the head stand. Foam collapses, most of it into air, so the glass drops and
        /// leaves room to top up (GDD 21 §10.2). Driven off the same clock as the floor, so
        /// waiting for a pint to settle costs exactly what it should: the customer's patience.
        /// </summary>
        public void SettleHead(double seconds)
        {
            if (seconds <= 0 || ServingGlass.Head <= 0) return;
            // A head the pint should have keeps standing; only what is over that is froth.
            double keep = TapPour.GoodHeadMin * ServingGlass.Capacity;
            ServingGlass.CollapseHead(TapPour.Settled(ServingGlass.Head, seconds, keep),
                TapPour.FoamLiquidShare);
        }

        /// <summary>
        /// Drops a preparation (ice, a twist, a rim) into the shaker (GDD 24 §2.4). A full tin
        /// takes none of them: a cube of ice needs somewhere to go, and pouring to the brim and
        /// then icing it anyway was the one way to get a garnished drink for free (2026-07-28).
        /// Refusing here rather than in the menu is the point — the sim and the tests drop ice
        /// through this same verb.
        /// </summary>
        public void AddPreparation(PreparationDefinition preparation)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            // No fullness test, for the same reason as at the glass: a preparation is a
            // recorded step with no volume, so a brimful tin still takes a twist.
            Glass.AddPreparation(preparation);
        }

        /// <summary>Whether that preparation would be taken, so the UI can say "full" instead of
        /// offering a drop that will be refused.</summary>
        /// <summary>
        /// A PREPARATION TAKES NO ROOM (2026-08-10, the author: "buz limon tuz şeker gibi
        /// eklentiler doluluğu değiştirmemeli"). AddPreparation records a step; it has never
        /// touched TotalVolume. So a tin filled to the brim refusing a twist of lemon was a
        /// rule about volume applied to something that has none — and it read as the bench
        /// seizing up the moment you got the pour right.
        /// </summary>
        public bool CanAddPreparation => Phase == TycoonPhase.DayOpen;

        /// <summary>
        /// Finishes the drink in the SERVING GLASS rather than in the shaker (v5 P14) — ice,
        /// a rim, a twist, added where a built drink is actually made. The shaker verb above
        /// cannot serve them: a built drink never sees the shaker, so before this the six
        /// built cocktails could be poured but never finished, and every serving spec asking
        /// for ice on one was unmeetable.
        ///
        /// Same refusal as its shaker twin, against the glass it is actually going in: a cube
        /// of ice needs somewhere to go, and a brimful glass that takes ice anyway is a
        /// garnished drink for free.
        /// </summary>
        public void AddPreparationAtGlass(PreparationDefinition preparation)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            // No fullness test: a rim of salt and a twist of lemon displace nothing, so a
            // glass poured to the brim can still be finished (2026-08-10).
            ServingGlass.AddPreparation(preparation);
        }

        /// <summary>Whether the serving glass would take a finishing touch right now.</summary>
        /// <summary>Finishing takes no room either — see <see cref="CanAddPreparation"/>.</summary>
        public bool CanFinishAtGlass => Phase == TycoonPhase.DayOpen;

        /// <summary>
        /// WOULD A SHAKE BLOW THE TIN OPEN? (2026-08-14, the author: "gazlı içecekler
        /// çalkalandığında patlayabilir, shaker boşalsın ve patlama animasyonu olsun.")
        /// Fizz under pressure is not a technique, it is an accident, and the accident is
        /// the answer the bar should give — not a refusal, which would teach nothing.
        ///
        /// THE RECIPE IS THE AUTHORITY, as everywhere else since GDD 21 §12 was overturned.
        /// A Gin Fizz and a Long Island are shaken WITH their fizz in this book, so the rule
        /// asks the tin's own method first and only blows a drink that was never meant to be
        /// worked that way. A mix the book cannot name and that holds fizz blows too — there
        /// is nothing saying it should not.
        ///
        /// The UI reads this BEFORE it shakes, the <see cref="CanPull"/> idiom: after the
        /// shake the tin is empty and there is nothing left to ask.
        /// </summary>
        public bool ShakeBlowsTheTin
        {
            get
            {
                if (Glass.IsEmpty || TinMethod == PrepMethod.Shaken) return false;
                foreach (var id in Glass.Ingredients)
                {
                    if (Glass.RatioOf(id) < MixEpsilon) continue;
                    var card = IngredientOf(id);
                    if (card?.Info != null && card.Info.Carbonated) return true;
                }
                return false;
            }
        }

        /// <summary>How many tins have burst this run. The goods are written off exactly as
        /// a binned drink's are, so blowing one is never the cheap way to start again.</summary>
        public int Blowouts { get; private set; }

        /// <summary>Shakes the built drink (GDD 24 §2.5). Recorded on the shaker; the craft
        /// effect of a good shake is a later balance pass, the plumbing is here now.
        ///
        /// A tin of fizz shaken anyway BURSTS (<see cref="ShakeBlowsTheTin"/>): the drink is
        /// gone and the bar is back where it started. The rule lives here rather than in the
        /// bench because the sim and the tests shake through this same verb.</summary>
        public void Shake(double energy = 1.0)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (Glass.IsEmpty) throw new InvalidOperationException("Nothing in the shaker to shake.");
            if (ShakeBlowsTheTin)
            {
                Blowouts++;
                WriteOffVessels();
                return;
            }
            Glass.AddPreparation(Preparations.Shaken);
            IsShaken = true;
            ShakeEnergy = energy < 0 ? 0 : energy > 1 ? 1 : energy;
            // The glass keeps ONE of shaken/stirred, last one wins (GlassContents) — the
            // run-level flags must agree with the vessel they describe.
            IsStirred = false;
            StirEnergy = 0;
        }

        /// <summary>
        /// Stirs the open tin (GDD 21 §14) — <see cref="Shake"/>'s mirror, and the second
        /// way to satisfy the mandatory mix. One preparation slot: a stir after a shake
        /// REPLACES it, on the glass and on these flags alike.
        /// </summary>
        public void Stir(double energy = 1.0)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (Glass.IsEmpty) throw new InvalidOperationException("Nothing in the shaker to stir.");
            Glass.AddPreparation(Preparations.Stirred);
            IsStirred = true;
            StirEnergy = energy < 0 ? 0 : energy > 1 ? 1 : energy;
            IsShaken = false;
            ShakeEnergy = 0;
        }

        /// <summary>Below this share of the tin, an alcoholic ingredient is seasoning — a
        /// dash of vermouth over gin does not conscript the spoon (GDD 21 §14).</summary>
        public const double MixEpsilon = 0.03;

        /// <summary>
        /// THE METHOD THE TIN'S OWN CONTENTS ASK FOR (2026-08-14, the author: "tariflerin
        /// hangilerinin çalkalanması gerektiği hangisinin karıştırması gerektiği belirtilsin
        /// önemli"). The tin is matched against the book the same way the pour-out picks its
        /// glassware, and the matched recipe's <see cref="RecipeDefinition.Prep"/> is the
        /// answer — Built included, because "do not work this one" is an instruction too.
        /// Null while nothing in the book fits, which is most of a half-built drink's life.
        ///
        /// This reads the tin, never the order: a mix declares its own method, so nothing
        /// here can leak what the customer asked for (the hidden-information rule).
        /// </summary>
        public PrepMethod? TinMethod
        {
            get
            {
                // Cached against the tin's own shape, because the bench asks for this several
                // times a frame (the step card, the lid's nudge, MixRequired inside
                // CanPourOut) and a match walks every recipe in the book building two ratio
                // maps as it goes. Nothing can change the answer without changing how much is
                // in the tin or how many things are in it — pours only ever add, and the one
                // verb that takes away empties it.
                int count = Glass.Ingredients.Count;
                double volume = Glass.TotalVolume;
                if (count != _tinSigCount || volume != _tinSigVolume)
                {
                    _tinSigCount = count;
                    _tinSigVolume = volume;
                    _tinMethod = RatioRecipeMatcher.Match(Glass, _recipes, IngredientOf)?.Recipe?.Prep;
                }
                return _tinMethod;
            }
        }

        private int _tinSigCount = -1;
        private double _tinSigVolume = double.NaN;
        private PrepMethod? _tinMethod;

        /// <summary>
        /// Does the tin hold a drink that MUST be worked before it leaves?
        ///
        /// The recipe answers when the book knows the drink (2026-08-14): a Shaken or a
        /// Stirred one has to be worked, a Built one never does — which is what lets the
        /// highballs come through the tin now that the fizz is allowed in it. Only when
        /// nothing matches does the old structural rule stand in: two or more distinct
        /// alcoholic ingredients at a real share each. Alcoholic is the category test
        /// (<see cref="IngredientCategories.IsAlcoholic"/>): liqueurs count, because a
        /// Martini is gin and vermouth and it is exactly the drink that rule is about. ABV
        /// never feeds a rule — that law is written where the categories live.
        /// </summary>
        public bool MixRequired
        {
            get
            {
                var method = TinMethod;
                if (method.HasValue)
                    return method.Value == PrepMethod.Shaken || method.Value == PrepMethod.Stirred;

                int booze = 0;
                foreach (var id in Glass.Ingredients)
                {
                    if (Glass.RatioOf(id) < MixEpsilon) continue;
                    var card = IngredientOf(id);
                    if (card != null
                        && IngredientCategories.IsAlcoholic(card.Info?.Category, card.Type)
                        && ++booze >= 2)
                        return true;
                }
                return false;
            }
        }

        /// <summary>Whether the tin may be poured out right now — the UI's grey-the-key
        /// predicate for the refusal <see cref="PourIntoServingGlass"/> enforces
        /// (the <see cref="CanPull"/>/<see cref="BeginPull"/> pattern).</summary>
        public bool CanPourOut => Phase == TycoonPhase.DayOpen && (!MixRequired || IsMixed);

        /// <summary>
        /// The serve pour (GDD 24 §3): moves <paramref name="volume"/> from the shaker into
        /// the serving glass. <paramref name="accuracy"/> (0…1) is the aim — a share lands,
        /// the rest spills and is lost. Returns the volume that landed. The UI drives this
        /// per frame from where the pour is aimed; the sim and the quick path pour perfectly.
        /// </summary>
        public double PourIntoServingGlass(double volume, double accuracy)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            // THE MANDATORY MIX (GDD 21 §14, 2026-08-11): two spirits may not leave the tin
            // unmixed. The refusal lives HERE — the verb by which a drink leaves the shaker —
            // exactly like the keg rule and the carbonated rule before it: routing around it
            // in a menu is not enough, because the sim and the tests pour through this same
            // verb. Drinks built directly at the glass are exempt by design: the rule is
            // about the tin. The UI reads CanPourOut and stops the stream instead.
            if (!CanPourOut)
                throw new InvalidOperationException(
                    "Two spirits in the tin — shake it or stir it before it goes to the glass.");
            // The glass is chosen here, on the first pour out (v5 P14 / C9): the shaker is what
            // knows the drink, so this is the last moment the bar can reach for the right vessel
            // and the first moment it has anything to reach for it WITH. Whatever the shaker
            // identifies as, that glass comes down — including nothing, which lands in the
            // default the way an unrecognisable mix always has.
            if (ServingGlass.IsEmpty && !Glass.IsEmpty)
                SelectGlassFor(RatioRecipeMatcher.Match(Glass, _recipes, IngredientOf)?.Recipe);
            return Glass.TransferInto(ServingGlass, volume, accuracy);
        }

        /// <summary>Dollars per glass-unit of drink deliberately binned (2026-07-31, the
        /// author: a mistake must cost). Small on purpose — a full shaker is ~$2. First cut
        /// was 3.0 and the sim put the floor from 7.5% to 42.5% bankruptcies on it: on
        /// knife-edge margins even the small fine wants measuring, not guessing.</summary>
        public const double BinFeePerVolume = 2.0;

        /// <summary>
        /// Tips whatever is built down the drain and pays for the waste: the fee scales with
        /// what was actually in the vessels, so binning a splash stings less than binning a
        /// finished drink. Clamped to the till — only rent may take a bar below zero (GDD 23).
        /// Post-serve leftovers stay free: the fee is for the DECISION, not for residue.
        /// Returns what it cost, so the UI can say so.
        /// </summary>
        public int DiscardGlass()
        {
            EnsurePhase(TycoonPhase.DayOpen);
            return WriteOffVessels();
        }

        /// <summary>The bin's accounting, shared with the burst tin: the goods are written
        /// off at the same rate and both vessels go back to empty. One body, so a blowout
        /// can never end up cheaper than owning up to the drink and binning it.</summary>
        private int WriteOffVessels()
        {
            double binned = Glass.TotalVolume + ServingGlass.TotalVolume;
            int fee = 0;
            if (binned > 0.01)
            {
                fee = (int)Math.Ceiling(binned * BinFeePerVolume);
                fee = Math.Min(fee, Math.Max(0, Money));
                Money -= fee;
                DayStock += fee;   // written off with the goods
            }
            ResetVessels();
            return fee;
        }

        // ── serving a seat (GDD 23 §4–§5) ───────────────────────────────────────

        /// <summary>
        /// Hands the drink to one seated customer. What goes out is the SERVING GLASS and
        /// nothing else: a drink still sitting in the shaker has not been poured yet, and no
        /// amount of backing out of the menu turns it into a served drink (ruling 2026-07-28).
        /// The quick path that used to tip the shaker in here made the aim-and-spill pour
        /// optional — closing the flow mid-build served a perfect drink the player never
        /// poured — so the sim and the tests make the serve pour like everyone else.
        /// Identifies the drink, applies its charges to who they really are, prices it for
        /// today's crowd, and settles or reopens them.
        /// </summary>
        public ServiceVerdict ServeTo(CustomerVisit visit)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            if (visit.State != VisitState.Waiting || !Floor.Seated.Contains(visit))
                throw new InvalidOperationException("That customer is not waiting at the bar.");
            if (!visit.HasOrdered)
                throw new InvalidOperationException("That customer is still deciding — no order to serve yet.");
            if (ServingGlass.IsEmpty)
                throw new InvalidOperationException(Glass.IsEmpty
                    ? "Nothing to serve."
                    : "That drink is still in the shaker — pour it into a glass first.");

            PouringId = null;
            var delivered = ServingGlass;

            // THE STORY'S GUEST IS SERVED WITH THE SAME VERB and judged by the same judge, but
            // they are not a customer in any way the books notice (GDD 26 §3): no price, no
            // tip, no rating, and the pass mark is the trial's. One verb, because the bot, the
            // tests and the player all have to be able to hand somebody a drink the same way.
            if (Trial != null && !Trial.IsOver && ReferenceEquals(visit, LastCustomer))
            {
                if (Trial.State == TrialState.Talking)
                    throw new InvalidOperationException("They are still talking — nothing has been asked for.");
                return ServeTheTrial(visit, delivered);
            }

            var match = RatioRecipeMatcher.Match(delivered, _recipes, IngredientOf);
            var matchKind = ServiceJudge.Compare(visit.OrderTruth, match, delivered, IngredientOf);
            // The verdict is priced off the DRINK — the recipe matched, the garnishes asked
            // for, the fill (the 2026-07-22 pivot, made total 2026-08-02: the emotion layer
            // is gone; what a customer gives you back is their reaction to the cocktail).
            var verdict = ServiceJudge.Judge(visit, matchKind, delivered, CrowdToday, Ambience,
                served: match);

            // The night remembers its best EXACT serve (2026-08-02): the menu cap reads it.
            if (matchKind == OrderMatch.Exact && match?.Recipe != null
                && match.Recipe.Rank > _bestRankServedTonight)
                _bestRankServedTonight = match.Recipe.Rank;

            // The VESSEL that actually went out, remembered on the visit (audit 2026-08-11):
            // the dirty glass on the stool used to be conjured from the RECIPE's glass id,
            // which lies whenever the served vessel differs — and lied by omission for a
            // drink that matched no recipe at all, which left no glass and freed the stool
            // instantly, a bussing discount for the worst pour in the house.
            visit.ServedGlassId = ServingGlassware?.Id;

            // What actually went across the bar, not what was asked for — the receipt lists the
            // drink that was poured, and a wrong one is paid at its own price.
            visit.Resolve(verdict, verdict.OrdersAgain ? RollOrder() : null, _config.SavorSeconds,
                served: match?.Recipe);
            if (visit.State != VisitState.Waiting)
                visit.Regular?.RecordVisit((int)Math.Round(verdict.Satisfaction * 3));

            // The last customer is the one visit the arc is watching (GDD 26 §5). Exactly what
            // they asked for moves the story on; near enough does not, and comes back.
            if (ReferenceEquals(visit, LastCustomer)) AnswerLastCall(matchKind == OrderMatch.Exact);

            // No money moves here. The verdict is settled onto the VISIT (its Paid/PaidBase),
            // and the till collects when they get up — see SettleDepartures. The author's note
            // (2026-07-31): the money and the stars show when the drink is finished, not when
            // it is served — and the till ticking up at the serve was itself a spoiler.
            ResetVessels();
            return verdict;
        }

        /// <summary>
        /// Tells a waiting customer the bar cannot make what they asked for (v5 P11, C2). They
        /// leave without paying and the night takes a mark for it — but being told is not the
        /// same as being ignored, so it scores above a storm-off and the stool frees up now
        /// instead of in thirty seconds. The honest answer to a dry bottle.
        /// </summary>
        public ServiceVerdict DeclineOrder(CustomerVisit visit)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            if (visit.State != VisitState.Waiting || !Floor.Seated.Contains(visit))
                throw new InvalidOperationException("That customer is not waiting at the bar.");
            if (!visit.HasOrdered)
                throw new InvalidOperationException("That customer has not asked for anything yet.");

            var verdict = ServiceJudge.Declined();

            // Told no, the last customer keeps their beat and their return clock: declining
            // costs a night, never the arc (PLAN_last_call, standing rule 2). It costs the
            // BAR nothing else either — a guest of the house is not on the night's slip, so
            // the honest no that ends a trial is not also a mark against the room.
            if (ReferenceEquals(visit, LastCustomer))
            {
                Trial?.Fail(toldNo: true);
                visit.GetUp(0.0, LastWordSeconds);   // they have a line to leave on
                AnswerLastCall(false);
                return verdict;
            }

            visit.Resolve(verdict);   // no savour: there is no drink to nurse
            visit.Regular?.RecordVisit((int)Math.Round(verdict.Satisfaction * 3));
            DeclinedOrders++;
            return verdict;
        }

        /// <summary>Orders turned away this run because the bar could not make them.</summary>
        public int DeclinedOrders { get; private set; }

        /// <summary>What the bar is worth to the people who drink in it (v5 P12, GDD 23 §7).
        /// Every finished visit leaves stars; the standing sets the crowd and the arrival
        /// rate.</summary>
        public BarRating Rating { get; } = new BarRating();

        /// <summary>
        /// Whether the shelf can currently answer an order — every band it names has a bottle
        /// with something in it. What the UI greys out, and what tells the player when saying
        /// "we're out" is the only honest move.
        /// </summary>
        public bool CanMake(DrinkOrder order)
        {
            if (order == null) return false;
            foreach (var band in order.Wanted.RatioRequirements)
            {
                bool found = false;
                foreach (var bottle in _shelf.Bottles)
                {
                    if (bottle.IsEmpty) continue;
                    // MinTier counts (audit 2026-08-11): the matcher refuses a Vesper built
                    // on well gin, so CanMake saying "yes" off that same bottle pushed the
                    // bot and the honest DeclineOrder both into a build that cannot match.
                    bool hit = band.IsStyleBand
                        ? bottle.Ingredient.Info?.Style == band.Style
                          && (bottle.Ingredient.Info?.Tier ?? 1) >= band.MinTier
                        : bottle.Ingredient.Type == band.Type;
                    if (hit) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        private void ResetVessels()
        {
            PouringId = null;
            PullingId = null;
            SpilledBeer = 0;
            IsShaken = false;
            ShakeEnergy = 0;
            IsStirred = false;
            StirEnergy = 0;
            Glass = new GlassContents(_config.GlassCapacity);
            ServingGlass = NewServingGlass(DefaultGlassware);   // back to the default until a drink asks
        }

        // ── day end: invoice, stock, market (GDD 23 §6–§8) ──────────────────────

        /// <summary>Everything below full, refilled at once. Books the expense.</summary>
        public int RefillShelf()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            int cost = _shelf.RefillCost(_config.RefillPricePerCapacity);
            if (cost == 0) return 0;
            EnsureAffordable(cost);
            Money -= cost;
            DayStock += cost;
            _shelf.RefillAll();
            return cost;
        }

        public void BuyBrand(int offerIndex)
        {
            EnsurePhase(TycoonPhase.DayEnd);
            if (offerIndex < 0 || offerIndex >= _marketOffers.Count)
                throw new ArgumentOutOfRangeException(nameof(offerIndex));
            var offer = _marketOffers[offerIndex];
            if (offer.Sold) throw new InvalidOperationException("That bottle is already yours.");

            Spend(offer.Price);
            var incoming = new ShelfBottle(offer.Bottle.Clone());
            // Every bottle JOINS the shelf (the author, 2026-08-02). A better brand used to
            // take the old one's place; now the well bottle stays and pours the cheap
            // drinks while the reserve stands beside it for the cocktails that name a rung
            // (RatioRequirement.MinTier) — the choice of which vodka to reach for only
            // exists if both are actually on the bar.
            _shelf.Add(incoming);
            _newStockIds.Add(offer.Bottle.Id);   // flashes NEW on the menu tomorrow
            offer.MarkSold();
            _todayPurchases.Add(new DayPurchase(DayPurchase.Kind.Brand, offer.Bottle.Id,
                offer.Bottle.Name, offer.Price)
            { Added = incoming, Offer = offer });
        }

        /// <summary>One more stool, up to the room's limit (GDD 23 §8).</summary>
        public int BuySeat()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            if (Seats >= _config.MaxSeats)
                throw new InvalidOperationException("The bar has no room for another stool.");
            EnsureUpgradeRoom();
            int price = _config.SeatPrice(Seats);
            Spend(price);
            UpgradesToday++;
            Seats++;
            _todayPurchases.Add(new DayPurchase(DayPurchase.Kind.Seat, "seat",
                $"Stool #{Seats}", price));
            return price;
        }

        // ── the fittings (GDD 23 §8) ────────────────────────────────────────────
        // GDD 23 §8 wanted every buyable to change the main scene. Two of the three that did
        // are gone (2026-08-04) and the third deliberately does not any more, so that line of
        // the design is historical: the glassware carries the visible progress now, because a
        // finer glass is a thing the drink is actually served in rather than a tint.

        /// <summary>One step up ONE glass line (the author: per-type upgrades).</summary>
        public int BuyGlassTier(string glassId)
        {
            EnsurePhase(TycoonPhase.DayEnd);
            var def = GlassNamed(glassId);
            if (def == null)
                throw new InvalidOperationException($"No glass line called '{glassId}'.");
            int tier = GlassTier(glassId);
            if (tier >= MaxGlassTier)
                throw new InvalidOperationException($"The {def.Name} line is already the finest.");
            EnsureUpgradeRoom();
            int price = def.TierPrices[tier - 1];
            Spend(price);
            UpgradesToday++;
            _glassTiers[glassId] = tier + 1;
            _todayPurchases.Add(new DayPurchase(DayPurchase.Kind.Glassware, glassId,
                // Said in words: this label is printed in the pixel faces, and none of them
                // carries a star (2026-08-11).
                tier + 1 >= MaxGlassTier ? $"{def.Name} — Legendary" : $"{def.Name} {tier}-star",
                price));
            return price;
        }

        /// <summary>One step up the counter. It buys satisfaction and nothing you can see —
        /// see <see cref="Ambience"/>.</summary>
        public int BuyCounter()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            if (CounterTier >= _config.MaxAmbienceTier)
                throw new InvalidOperationException("The counter cannot be finer.");
            EnsureUpgradeRoom();
            int price = _config.CounterPrice(CounterTier);
            Spend(price);
            UpgradesToday++;
            CounterTier++;
            // It books like the other two fittings. It did not, which meant the ONE
            // fitting a night that could not be taken back was the one nothing on screen
            // had told the player about — the counter had no tile at all until 2026-08-09,
            // so the omission was invisible in both directions at once.
            _todayPurchases.Add(new DayPurchase(
                DayPurchase.Kind.Counter, "counter", $"Bar Top {CounterTier}", price));
            return price;
        }

        // ── bar dressing (2026-08-10): the modular fixtures ─────────────────────
        // Plants, lamps, wall pieces — bought at day end like everything else, gated on
        // the bar's standing like the better recipes, refundable the same night. They are
        // COSMETIC: no ambience number, and no fitting slot spent, because the fitting
        // cap is for things that change what the bar IS (stools, glassware, the counter),
        // and a fern changes what it looks like.

        private readonly IReadOnlyList<FixtureDefinition> _fixtureCatalogue;
        private readonly HashSet<string> _fixtures = new HashSet<string>();

        /// <summary>Everything the market could ever sell for the room, gated or not.</summary>
        public IReadOnlyList<FixtureDefinition> FixtureCatalogue => _fixtureCatalogue;

        public bool OwnsFixture(string fixtureId) => _fixtures.Contains(fixtureId);

        /// <summary>How many pieces of dressing the bar owns — the cheap change-detection
        /// handle the stage watches, so it only rebuilds the room when the room changed.</summary>
        public int OwnedFixtureCount => _fixtures.Count;

        public int BuyFixture(string fixtureId)
        {
            EnsurePhase(TycoonPhase.DayEnd);
            FixtureDefinition def = null;
            foreach (var f in _fixtureCatalogue)
                if (f.Id == fixtureId) { def = f; break; }
            if (def == null)
                throw new InvalidOperationException($"No fixture '{fixtureId}' in the catalogue.");
            if (_fixtures.Contains(fixtureId))
                throw new InvalidOperationException($"The bar already has {def.Name}.");
            if (Rating.Average < def.Stars)
                throw new InvalidOperationException(
                    $"{def.Name} needs a {def.Stars:0.0}-star room; this bar rates {Rating.Average:0.0}.");
            Spend(def.Price);
            _fixtures.Add(fixtureId);
            _todayPurchases.Add(new DayPurchase(
                DayPurchase.Kind.Fixture, fixtureId, def.Name, def.Price));
            return def.Price;
        }

        /// <summary>
        /// ONE fitting a night (the author, 2026-08-07: "1 gecede maksimum 1 upgrade
        /// yapılabilir"). A bar is rebuilt over weeks, not in an evening: a glass line can
        /// climb a single step and then it waits for tomorrow. Stock is not a fitting —
        /// bottles, recipes and restocking are as free as the till allows; this counts
        /// only the things that change what the bar IS: stools, glassware, the counter.
        /// </summary>
        public int UpgradesToday { get; private set; }

        /// <summary>Whether tonight still has its fitting to spend (the shop greys out on it).</summary>
        public bool CanFitTonight => UpgradesToday < _config.MaxUpgradesPerNight;

        private void EnsureUpgradeRoom()
        {
            if (!CanFitTonight)
                throw new InvalidOperationException(
                    "One fitting a night — the rest keeps until tomorrow.");
        }

        private void Spend(int price)
        {
            EnsureAffordable(price);
            Money -= price;
            DayUpgrades += price;
        }

        /// <summary>
        /// Purchases require cash (GDD 23 §6, 2026-07-22): nothing here is bought on
        /// credit. Only rent can push the till below zero — debt is something that happens
        /// *to* you, never a button you pressed.
        /// </summary>
        private void EnsureAffordable(int price)
        {
            if (Money < price)
                throw new InvalidOperationException($"Not enough money (${Money} < ${price}).");
        }

        /// <summary>
        /// Closes the books on today and opens tomorrow — or the doors for good: three
        /// consecutive red days end the run (GDD 23 §6).
        /// </summary>
        public DayResult ContinueToNextDay()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            // Every one of tonight's leavers files a rating on the way out -- storm-offs
            // included, because a storm-off is a review too.
            foreach (var visit in Floor.FinishedCounted()) Rating.Record(visit.Satisfaction);
            // The night's stars are CAPPED before they touch the standing (the author's
            // loop): the room can only say what the fittings and the menu let it say.
            // FAME IS READ BEFORE THE NIGHT IS CLOSED (2026-08-11, the author's ruling).
            // The line below has always said "fame alone brings the rollers", and the code
            // then asked for that fame AFTER the night had already dragged it down — so a
            // made bar could lose its room to one bad evening, which is the opposite of
            // what a reputation is. Letting a night be worth ZERO stars is what exposed it:
            // a zero night moves a standing to 0.8 of itself, and since the scale ends at
            // five, NO standing could clear the roller line afterwards. The rule was always
            // meant to be read against the standing the bar walked in with.
            double fame = Rating.Average;
            Rating.CloseNight(Floor.AverageSatisfaction, Math.Min(UpgradeStarCap, MenuStarCap));

            // Tomorrow's crowd reacts to TONIGHT (2026-08-02) — a dreadful night drives
            // the paying crowd off — while fame alone brings the rollers: once the
            // STANDING clears the high-roller line, it overrides the night. Keying broke
            // off the zero-start standing either starved the opening week or never fired.
            double crowdStars = fame >= BarRating.HighRollerStars
                ? BarRating.HighRollerStars
                : Rating.LastNight;
            // The star scale's INVERSE, and it has to move with it (2026-08-11): this read
            // (stars - 1) / 4 because stars used to be 1 + 4x. They are 5x now, so the
            // undo is a division. A test caught it — a made bar stopped drawing rollers,
            // because its 4.9 standing was being read back as a 0.98 room instead of 0.78.
            double standing = crowdStars / BarRating.MaxStars;
            // Everything the night was made of, written down with it. The run has always known
            // all of this at this exact moment and the book kept none of it, so a closed day
            // could only say that it made or lost money — never which half of the bar did it.
            int served = 0, walked = 0;
            foreach (var visit in Floor.FinishedCounted())
                if (visit.State == VisitState.StormedOff) walked++; else served++;
            var result = Ledger.CloseDay(Day, DayIncome, DayExpenses, standing,
                tillAfter: Money,
                detail: new DayDetail
                {
                    Sales = DaySales, Tips = DayTips,
                    Rent = DayRent, Stock = DayStock, Upgrades = DayUpgrades,
                    Served = served, WalkedOut = walked,
                    NightStars = Rating.LastNight,
                });

            if (Ledger.IsBankrupt)
            {
                Phase = TycoonPhase.Closed;
                return result;
            }

            Day++;
            CrowdToday = Ledger.TomorrowsCrowd;
            DaySales = DayTips = DayRent = DayStock = DayUpgrades = 0;
            UpgradesToday = 0;         // tonight's fitting is spent; tomorrow gets its own
            _bestRankServedTonight = 0;
            _todayPurchases.Clear();   // yesterday's buys are kept; refunds are same-day only
            LastCustomer = null;       // last night's beat is over, whichever way it went
            LastCallBeat = null;
            Trial = null;
            _lastCallSpent = _lastCallAnswered = LastCallWithheld = false;
            BuyBackTheBowls();
            ResetVessels();
            Floor = new BarDay(Day, Seats, _config, _rng.GetStream("arrivals"), Rating.Average);
            Phase = TycoonPhase.DayOpen;
            return result;
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private void RollMarket()
        {
            // Deterministic: new stock (styles you lack) + better bottles (brands you do
            // not own yet), the higher rungs gated by the bar's standing.
            _newStockIds.Clear();   // yesterday's "NEW" flashes have worn off
            _marketOffers.Clear();
            _marketOffers.AddRange(Market.OffersFor(_shelf, _brandCatalogue, Rating.Average));
        }

        /// <summary>A garnish press, as a share of the shaker (survives PourResolver).</summary>
        private const double GarnishClickFraction = 0.05;

        private IngredientCard IngredientOf(string id) => _shelf.Find(id)?.Ingredient;

        private void EnsurePhase(TycoonPhase expected)
        {
            if (Phase != expected)
                throw new InvalidOperationException($"Expected {expected}, but the bar is in {Phase}.");
        }
    }
}
