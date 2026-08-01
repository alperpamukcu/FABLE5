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
    /// The emotion layer is opt-in: built without a regulars registry, visits carry no read
    /// and the mood tip simply never lands.
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
        // ladder — glassware.json prices each line's steps 2 and 3, everything starts at 1.
        public const int MaxGlassTier = 3;
        private readonly Dictionary<string, int> _glassTiers = new Dictionary<string, int>();

        /// <summary>The upgrade tier of one glass line (1–3).</summary>
        public int GlassTier(string glassId) =>
            glassId != null && _glassTiers.TryGetValue(glassId, out var t) ? t : 1;

        /// <summary>Paid steps across every line — what the star cap and ambience read.</summary>
        public int GlassUpgradeSteps
        {
            get { int s = 0; foreach (var g in _glassware) s += GlassTier(g.Id) - 1; return s; }
        }
        public int CounterTier { get; private set; } = 1;
        public int WallTier { get; private set; } = 1;
        public bool HasMusician { get; private set; }

        /// <summary>The satisfaction the bar's look adds to every served visit (GDD 23 §8).
        /// Musician, counter and wall retired (the author, 2026-08-02); the glassware LINES
        /// carry it now — same ceiling the old single ladder had (0.03×2 = 0.006×10).</summary>
        public double Ambience => Math.Min(0.15, 0.006 * GlassUpgradeSteps);

        /// <summary>The most stars the bar's fittings allow a night to bank (the author's
        /// loop, 2026-08-02): happy customers alone cannot carry a dive past two stars —
        /// the glassware line and the extra stools raise the ceiling toward five.</summary>
        /// <remarks>0.30/step, measured (2026-08-02): at 0.25 the per-line ladder asked
        /// four times the old money for less ceiling per dollar, and the sim's floor went
        /// 0% → 85% bankruptcies on that arithmetic alone. Ten steps now buy the whole
        /// +3.0, and the json's step prices were halved in the same pass.</remarks>
        public double UpgradeStarCap =>
            2.0 + 0.30 * GlassUpgradeSteps + 0.25 * Math.Max(0, Seats - 3);

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
            IReadOnlyList<IngredientCard> lockedStock = null)
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
            RestockSnacks();

            Money = _config.StartingMoney;
            Seats = _config.StartingSeats;
            Glass = new GlassContents(_config.GlassCapacity);
            ServingGlass = NewServingGlass(DefaultGlassware);
            Floor = new BarDay(Day, Seats, _config, _rng.GetStream("arrivals"));
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
        public double RecipeStarGate(RecipeDefinition recipe) =>
            recipe.Rank <= 8 ? 0.0
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

        // ── today's purchases: same-day refunds (the author, 2026-08-02) ─────────
        // A slip of everything bought at THIS close. Refunding reverses the purchase in
        // place; at the next dawn the slip is torn up — yesterday's buys are final.

        public sealed class DayPurchase
        {
            public enum Kind { Brand, Recipe, Seat, Glassware }
            public Kind What { get; }
            public string Id { get; }
            public string Name { get; }
            public int Price { get; }
            internal ShelfBottle Added;      // Brand: the bottle that came in
            internal ShelfBottle Replaced;   // Brand upgrade: the one it displaced
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
                    if (p.Added != null)
                    {
                        if (p.Replaced != null) _shelf.Replace(p.Added, p.Replaced);
                        else _shelf.Remove(p.Added);
                    }
                    _newStockIds.Remove(p.Id);
                    p.Offer?.MarkUnsold();
                    break;
                case DayPurchase.Kind.Recipe:
                    // Relocks the drink. The bottles it released stay in the catalogue —
                    // they are only offer candidates, and unwinding a market re-roll would
                    // cost more honesty than it buys.
                    _boughtRecipes.Remove(p.Id);
                    for (int i = _recipes.Count - 1; i >= 0; i--)
                        if (_recipes[i].Id == p.Id) { _recipes.RemoveAt(i); break; }
                    break;
                case DayPurchase.Kind.Seat: Seats--; break;
                case DayPurchase.Kind.Glassware:
                    if (_glassTiers.TryGetValue(p.Id, out var gt) && gt > 1) _glassTiers[p.Id] = gt - 1;
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
                    : (g.Id == "rocks" || g.Id == "highball" ? 2 : 1);
            while (Seats < (late ? _config.MaxSeats : 4)) Seats++;

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
            // The unlocked menu must be POURABLE for the playtest: any catalogue style the
            // shelf lacks walks straight onto it.
            foreach (var card in _brandCatalogue)
            {
                if (card.Info?.Style == null) continue;
                if (Market.FindByStyle(_shelf, card.Info.Style) == null)
                    _shelf.Add(new ShelfBottle(card.Clone()));
            }
            RollMarket();
            Day = late ? 30 : 12;
            Floor = new BarDay(Day, Seats, _config, _rng.GetStream("arrivals"), Rating.Average);
            Phase = TycoonPhase.DayOpen;
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

        private CustomerVisit NextArrival()
        {
            var order = RollOrder();
            double patience = _config.RollPatience(Day, _rng.GetStream("patience"));
            double decide = _config.RollDecideDelay(_rng.GetStream("decide"));
            if (_regulars == null) return new CustomerVisit(order, patience, decideSeconds: decide);

            // The same face-and-memory pipeline the old loop used (GDD 19 §3, 20 §3):
            // returning regulars are read through decayed memory, strangers roll fresh.
            var regular = _regulars.RollNext(_rng.GetStream("customer"));
            var readRng = _rng.GetStream("read");
            var read = regular.Visits > 0
                ? CustomerReadFactory.FromTiers(regular.Stats, regular.KnownTiers, Day, readRng,
                    regular.Relationship, regular.BaseDemand)
                : CustomerReadFactory.Build(regular.Stats, Day, readRng,
                    regular.Relationship, regular.BaseDemand);
            regular.RememberTiers(TiersOf(read));

            return new CustomerVisit(order, patience, regular, read, decide);
        }

        private DrinkOrder RollOrder()
        {
            var order = DrinkOrder.Roll(_recipes, Day, _config, _rng.GetStream("orders"));
            // Premium spirits on the shelf raise the price; the crowd tier then scales it.
            int price = Math.Max(1, (int)Math.Round(
                (order.Price + PremiumFor(order.Wanted)) * _config.PriceMultiplier(CrowdToday),
                MidpointRounding.AwayFromZero));
            return price == order.Price
                ? order
                : new DrinkOrder(order.Wanted, price, order.Spec);   // keep how they want it served
        }

        /// <summary>The premium a drink earns from the shelf's stock (GDD 23 §3, 2026-07-23):
        /// for each spirit/bitter the recipe needs, the best bottle above the base tier adds
        /// <see cref="TycoonConfig.StockPremiumPerTier"/> to the price. A basic bar adds nothing.</summary>
        private int PremiumFor(RecipeDefinition recipe)
        {
            if (recipe.RatioRequirements == null || recipe.RatioRequirements.Count == 0) return 0;
            int premium = 0;
            var counted = new HashSet<IngredientType>();
            foreach (var band in recipe.RatioRequirements)
            {
                if (band.Type != IngredientType.Spirit && band.Type != IngredientType.Bitter) continue;
                if (!counted.Add(band.Type)) continue;   // one premium per alcohol type
                int bestTier = 1;
                foreach (var bottle in _shelf.Bottles)
                    if (bottle.Ingredient.Type == band.Type)
                        bestTier = Math.Max(bestTier, bottle.Ingredient.Info?.Tier ?? 1);
                premium += (bestTier - 1) * _config.StockPremiumPerTier;
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
            // Carbonated is built, not shaken (v5 P10, GDD 21 §12): fizz goes straight into
            // the serving glass. Same reasoning as the keg rule above — the refusal lives
            // here because the sim and the tests use these verbs too.
            if (bottle.Ingredient.Info != null && bottle.Ingredient.Info.Carbonated)
                throw new ArgumentException(
                    $"'{ingredientId}' is carbonated — it goes straight into the serving glass, never the shaker.",
                    argName);
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
            volume = Math.Min(volume, ServingGlass.Headroom);
            return _shelf.PourInto(ServingGlass, ingredientId, volume);
        }

        public double PourTick(double seconds)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (PouringId == null || seconds <= 0) return 0;

            var bottle = _shelf.Find(PouringId);
            double poured = _shelf.PourInto(Glass, PouringId, bottle.PourRate * seconds);
            if (poured <= 0 || bottle.IsEmpty) PouringId = null;
            return poured;
        }

        public void EndPour() => PouringId = null;

        public double PourMeasure(string ingredientId, double volume)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            ShakerIngredient(ingredientId, nameof(ingredientId));
            if (volume <= 0) return 0;
            double poured = _shelf.PourInto(Glass, ingredientId, volume);
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
            PourMeasure(ingredientId, PourResolver.GarnishClickFraction * Glass.Capacity);

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
            if (Glass.IsFull)
                throw new InvalidOperationException(
                    "The shaker is full to the brim — there is no room for anything else.");
            Glass.AddPreparation(preparation);
        }

        /// <summary>Whether that preparation would be taken, so the UI can say "full" instead of
        /// offering a drop that will be refused.</summary>
        public bool CanAddPreparation => Phase == TycoonPhase.DayOpen && !Glass.IsFull;

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
            if (ServingGlass.IsFull)
                throw new InvalidOperationException(
                    "The glass is full to the brim — there is no room for anything else.");
            ServingGlass.AddPreparation(preparation);
        }

        /// <summary>Whether the serving glass would take a finishing touch right now.</summary>
        public bool CanFinishAtGlass => Phase == TycoonPhase.DayOpen && !ServingGlass.IsFull;

        /// <summary>Shakes the built drink (GDD 24 §2.5). Recorded on the shaker; the craft
        /// effect of a good shake is a later balance pass, the plumbing is here now.</summary>
        public void Shake(double energy = 1.0)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (Glass.IsEmpty) throw new InvalidOperationException("Nothing in the shaker to shake.");
            Glass.AddPreparation(Preparations.Shaken);
            IsShaken = true;
            ShakeEnergy = energy < 0 ? 0 : energy > 1 ? 1 : energy;
        }

        /// <summary>
        /// The serve pour (GDD 24 §3): moves <paramref name="volume"/> from the shaker into
        /// the serving glass. <paramref name="accuracy"/> (0…1) is the aim — a share lands,
        /// the rest spills and is lost. Returns the volume that landed. The UI drives this
        /// per frame from where the pour is aimed; the sim and the quick path pour perfectly.
        /// </summary>
        public double PourIntoServingGlass(double volume, double accuracy)
        {
            EnsurePhase(TycoonPhase.DayOpen);
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
            var match = RatioRecipeMatcher.Match(delivered, _recipes, IngredientOf);
            var applied = PourResolver.Resolve(delivered, match, IngredientOf);
            var matchKind = ServiceJudge.Compare(visit.OrderTruth, match, delivered, IngredientOf);
            // Emotion→recipe pivot (2026-07-22): the verdict is priced off the drink and the
            // garnishes they asked for, not a mood read. The emotion charge is still applied to
            // the regular's dormant stats (harmless) so the customer model stays intact.
            var verdict = ServiceJudge.Judge(visit, matchKind, delivered, CrowdToday, Ambience,
                served: match, shakeEnergy: ShakeEnergy);

            // The night remembers its best EXACT serve (2026-08-02): the menu cap reads it.
            if (matchKind == OrderMatch.Exact && match?.Recipe != null
                && match.Recipe.Rank > _bestRankServedTonight)
                _bestRankServedTonight = match.Recipe.Rank;

            visit.Regular?.Stats.Apply(applied);
            // What actually went across the bar, not what was asked for — the receipt lists the
            // drink that was poured, and a wrong one is paid at its own price.
            visit.Resolve(verdict, verdict.OrdersAgain ? RollOrder() : null, _config.SavorSeconds,
                served: match?.Recipe);
            if (visit.State != VisitState.Waiting)
                visit.Regular?.RecordVisit((int)Math.Round(verdict.Satisfaction * 3));

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
                    bool hit = band.IsStyleBand
                        ? bottle.Ingredient.Info?.Style == band.Style
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
            ShelfBottle displaced = null;
            if (offer.IsNewStock)
            {
                // A style you did not carry — it joins the shelf so its drinks become makeable.
                _shelf.Add(incoming);
            }
            else
            {
                var current = Market.FindByStyle(_shelf, offer.Style);
                if (current == null)
                    throw new InvalidOperationException($"Nothing on the shelf pours {offer.Style}.");
                displaced = current;
                _shelf.Replace(current, incoming);
            }
            _newStockIds.Add(offer.Bottle.Id);   // flashes NEW on the menu tomorrow
            offer.MarkSold();
            _todayPurchases.Add(new DayPurchase(DayPurchase.Kind.Brand, offer.Bottle.Id,
                offer.Bottle.Name, offer.Price)
            { Added = incoming, Replaced = displaced, Offer = offer });
        }

        /// <summary>One more stool, up to the room's limit (GDD 23 §8).</summary>
        public int BuySeat()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            if (Seats >= _config.MaxSeats)
                throw new InvalidOperationException("The bar has no room for another stool.");
            int price = _config.SeatPrice(Seats);
            Spend(price);
            Seats++;
            _todayPurchases.Add(new DayPurchase(DayPurchase.Kind.Seat, "seat",
                $"Stool #{Seats}", price));
            return price;
        }

        // ── ambience upgrades (GDD 23 §8): every one changes the scene (GDD 24 §6) ─

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
            int price = def.TierPrices[tier - 1];
            Spend(price);
            _glassTiers[glassId] = tier + 1;
            _todayPurchases.Add(new DayPurchase(DayPurchase.Kind.Glassware, glassId,
                $"{def.Name} ★{tier + 1}", price));
            return price;
        }

        public int BuyCounter()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            if (CounterTier >= _config.MaxAmbienceTier)
                throw new InvalidOperationException("The counter cannot be finer.");
            int price = _config.CounterPrice(CounterTier);
            Spend(price);
            CounterTier++;
            return price;
        }

        public int BuyWall()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            if (WallTier >= _config.MaxAmbienceTier)
                throw new InvalidOperationException("The back bar cannot be finer.");
            int price = _config.WallPrice(WallTier);
            Spend(price);
            WallTier++;
            return price;
        }

        public int BuyMusician()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            if (HasMusician) throw new InvalidOperationException("The stage is already taken.");
            Spend(_config.MusicianPrice);
            HasMusician = true;
            return _config.MusicianPrice;
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
            foreach (var visit in Floor.Finished) Rating.Record(visit.Satisfaction);
            // The night's stars are CAPPED before they touch the standing (the author's
            // loop): the room can only say what the fittings and the menu let it say.
            Rating.CloseNight(Floor.AverageSatisfaction, Math.Min(UpgradeStarCap, MenuStarCap));

            // Tomorrow's crowd reacts to TONIGHT (2026-08-02) — a dreadful night drives
            // the paying crowd off — while fame alone brings the rollers: once the
            // STANDING clears the high-roller line, it overrides the night. Keying broke
            // off the zero-start standing either starved the opening week or never fired.
            double crowdStars = Rating.Average >= BarRating.HighRollerStars
                ? BarRating.HighRollerStars
                : Rating.LastNight;
            double standing = (crowdStars - 1.0) / 4.0;
            var result = Ledger.CloseDay(Day, DayIncome, DayExpenses, standing,
                tillAfter: Money);

            if (Ledger.IsBankrupt)
            {
                Phase = TycoonPhase.Closed;
                return result;
            }

            Day++;
            CrowdToday = Ledger.TomorrowsCrowd;
            DaySales = DayTips = DayRent = DayStock = DayUpgrades = 0;
            _bestRankServedTonight = 0;
            _todayPurchases.Clear();   // yesterday's buys are kept; refunds are same-day only
            BuyBackTheBowls();
            ResetVessels();
            Floor = new BarDay(Day, Seats, _config, _rng.GetStream("arrivals"), Rating.Average);
            Phase = TycoonPhase.DayOpen;
            return result;
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private void RollMarket()
        {
            // Deterministic: new stock (styles you lack) + upgrades (better brands you have).
            _newStockIds.Clear();   // yesterday's "NEW" flashes have worn off
            _marketOffers.Clear();
            _marketOffers.AddRange(Market.OffersFor(_shelf, _brandCatalogue));
        }

        private IngredientCard IngredientOf(string id) => _shelf.Find(id)?.Ingredient;

        private static VisibilityTier[] TiersOf(CustomerRead read)
        {
            var tiers = new VisibilityTier[Emotions.Count];
            for (int i = 0; i < tiers.Length; i++) tiers[i] = read[Emotions.All[i]].Tier;
            return tiers;
        }

        private void EnsurePhase(TycoonPhase expected)
        {
            if (Phase != expected)
                throw new InvalidOperationException($"Expected {expected}, but the bar is in {Phase}.");
        }
    }
}
