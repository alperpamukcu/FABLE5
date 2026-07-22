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
        private readonly IReadOnlyList<RecipeDefinition> _recipes;
        private readonly IReadOnlyList<IngredientCard> _brandCatalogue;
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
        public int GlasswareTier { get; private set; } = 1;
        public int CounterTier { get; private set; } = 1;
        public int WallTier { get; private set; } = 1;
        public bool HasMusician { get; private set; }

        /// <summary>The satisfaction the bar's look adds to every served visit (GDD 23 §8).</summary>
        public double Ambience => _config.AmbienceBonus(GlasswareTier, CounterTier, WallTier, HasMusician);

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

        /// <summary>True once the shaker has been shaken this build (GDD 24 §2.5).</summary>
        public bool IsShaken { get; private set; }

        /// <summary>How hard the last shake was, 0–1 (GDD 24 §2.5). A craft hook for later;
        /// recorded now so the shake motion means something the moment it earns an effect.</summary>
        public double ShakeEnergy { get; private set; }

        public string PouringId { get; private set; }

        private readonly List<MarketOffer> _marketOffers = new List<MarketOffer>();
        public IReadOnlyList<MarketOffer> MarketOffers => _marketOffers;

        public TycoonRun(Shelf shelf, IReadOnlyList<RecipeDefinition> recipes, RunRng rng,
            TycoonConfig config = null, RegularsRegistry regulars = null,
            IReadOnlyList<IngredientCard> brandCatalogue = null)
        {
            _shelf = shelf ?? throw new ArgumentNullException(nameof(shelf));
            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _config = config ?? TycoonConfig.Default;
            _regulars = regulars;
            _brandCatalogue = brandCatalogue ?? Array.Empty<IngredientCard>();

            Money = _config.StartingMoney;
            Seats = _config.StartingSeats;
            Glass = new GlassContents(_config.GlassCapacity);
            ServingGlass = new GlassContents(_config.GlassCapacity);
            Floor = new BarDay(Day, Seats, _config, _rng.GetStream("arrivals"));
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

        private CustomerVisit NextArrival()
        {
            var order = RollOrder();
            double patience = _config.RollPatience(Day, _rng.GetStream("patience"));
            if (_regulars == null) return new CustomerVisit(order, patience);

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

            return new CustomerVisit(order, patience, regular, read);
        }

        private DrinkOrder RollOrder()
        {
            var order = DrinkOrder.Roll(_recipes, Day, _config, _rng.GetStream("orders"));
            int price = Math.Max(1, (int)Math.Round(
                order.Price * _config.PriceMultiplier(CrowdToday), MidpointRounding.AwayFromZero));
            return price == order.Price ? order : new DrinkOrder(order.Wanted, price);
        }

        // ── building the drink (pour verbs, GDD 21 §3 unchanged) ────────────────

        public void BeginPour(string ingredientId)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (_shelf.Find(ingredientId) == null)
                throw new ArgumentException($"No '{ingredientId}' on the shelf.", nameof(ingredientId));
            PouringId = ingredientId;
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
            if (_shelf.Find(ingredientId) == null)
                throw new ArgumentException($"No '{ingredientId}' on the shelf.", nameof(ingredientId));
            if (volume <= 0) return 0;
            return _shelf.PourInto(Glass, ingredientId, volume);
        }

        /// <summary>One garnish tap = a fixed pinch (GDD 21 §3).</summary>
        public double PourGarnish(string ingredientId) =>
            PourMeasure(ingredientId, PourResolver.GarnishClickFraction * Glass.Capacity);

        /// <summary>Drops a preparation (ice, a twist, a rim) into the shaker (GDD 24 §2.4).</summary>
        public void AddPreparation(PreparationDefinition preparation)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            Glass.AddPreparation(preparation);
        }

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
            return Glass.TransferInto(ServingGlass, volume, accuracy);
        }

        public void DiscardGlass()
        {
            EnsurePhase(TycoonPhase.DayOpen);
            ResetVessels();
        }

        // ── serving a seat (GDD 23 §4–§5) ───────────────────────────────────────

        /// <summary>
        /// Hands the drink to one seated customer. If the serve pour was never made, the
        /// whole shaker is delivered perfectly (the quick path used by the sim, the tests
        /// and the interim UI); the aim/spill minigame pours into the serving glass first,
        /// so what it built is what goes out. Identifies the drink, applies its charges to
        /// who they really are, prices it for today's crowd, and settles or reopens them.
        /// </summary>
        public ServiceVerdict ServeTo(CustomerVisit visit)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            if (visit.State != VisitState.Waiting || !Floor.Seated.Contains(visit))
                throw new InvalidOperationException("That customer is not waiting at the bar.");
            if (ServingGlass.IsEmpty && Glass.IsEmpty)
                throw new InvalidOperationException("Nothing to serve.");

            PouringId = null;
            if (ServingGlass.IsEmpty)
                Glass.TransferInto(ServingGlass, Glass.TotalVolume, 1.0);

            var delivered = ServingGlass;
            var match = RatioRecipeMatcher.Match(delivered, _recipes, IngredientOf);
            var applied = PourResolver.Resolve(delivered, match, IngredientOf);
            var matchKind = ServiceJudge.Compare(visit.Order, match, delivered, IngredientOf);
            var verdict = ServiceJudge.Judge(visit, matchKind, applied, CrowdToday, Ambience);

            visit.Regular?.Stats.Apply(applied);
            visit.Resolve(verdict, verdict.OrdersAgain ? RollOrder() : null);
            if (visit.State != VisitState.Waiting)
                visit.Regular?.RecordVisit((int)Math.Round(verdict.Satisfaction * 3));

            Money += verdict.Total;
            DaySales += verdict.BasePaid;
            DayTips += verdict.Tip;
            ResetVessels();
            return verdict;
        }

        private void ResetVessels()
        {
            PouringId = null;
            IsShaken = false;
            ShakeEnergy = 0;
            Glass = new GlassContents(_config.GlassCapacity);
            ServingGlass = new GlassContents(_config.GlassCapacity);
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
            if (offer.Sold) throw new InvalidOperationException("That brand is already yours.");

            var current = Market.FindByStyle(_shelf, offer.Style);
            if (current == null)
                throw new InvalidOperationException($"Nothing on the shelf pours {offer.Style}.");

            Spend(offer.Price);
            _shelf.Replace(current, new ShelfBottle(offer.Bottle));
            offer.MarkSold();
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
            return price;
        }

        // ── ambience upgrades (GDD 23 §8): every one changes the scene (GDD 24 §6) ─

        public int BuyGlassware()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            if (GlasswareTier >= _config.MaxAmbienceTier)
                throw new InvalidOperationException("The finest glassware is already yours.");
            int price = _config.GlasswarePrice(GlasswareTier);
            Spend(price);
            GlasswareTier++;
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
            var result = Ledger.CloseDay(Day, DayIncome, DayExpenses, Floor.AverageSatisfaction,
                tillAfter: Money);

            if (Ledger.IsBankrupt)
            {
                Phase = TycoonPhase.Closed;
                return result;
            }

            Day++;
            CrowdToday = Ledger.TomorrowsCrowd;
            DaySales = DayTips = DayRent = DayStock = DayUpgrades = 0;
            ResetVessels();
            Floor = new BarDay(Day, Seats, _config, _rng.GetStream("arrivals"));
            Phase = TycoonPhase.DayOpen;
            return result;
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private void RollMarket()
        {
            // v0: deterministic "everything strictly better than what you stock" (GDD 22
            // §4). The rotating random market of GDD 23 §8 replaces this in P5.
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
