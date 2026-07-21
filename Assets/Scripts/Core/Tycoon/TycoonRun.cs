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
    /// Lives beside the old <see cref="RunController"/> until P7 demolition. The emotion
    /// layer is opt-in exactly as before: built without a regulars registry, visits carry
    /// no read and the mood tip simply never lands.
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

        /// <summary>Today's crowd, decided by yesterday's satisfaction bar (GDD 23 §7).</summary>
        public WealthTier CrowdToday { get; private set; } = WealthTier.Regular;

        public GlassContents Glass { get; private set; }
        public string PouringId { get; private set; }

        private int _dayIncome;
        private int _dayExpenses;

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
                _dayExpenses += rent;
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

        /// <summary>One garnish tap = a fixed pinch, same rule as the old loop (GDD 21 §3).</summary>
        public double PourGarnish(string ingredientId) =>
            PourMeasure(ingredientId, RoundController.GarnishClickFraction * Glass.Capacity);

        public void DiscardGlass()
        {
            EnsurePhase(TycoonPhase.DayOpen);
            PouringId = null;
            Glass = new GlassContents(_config.GlassCapacity);
        }

        // ── serving a seat (GDD 23 §4–§5) ───────────────────────────────────────

        /// <summary>
        /// Hands the glass to one seated customer. Identifies the drink (recipe match),
        /// applies its charges to who they really are, prices the serve for today's crowd,
        /// and either settles them up or opens their next round.
        /// </summary>
        public ServiceVerdict ServeTo(CustomerVisit visit)
        {
            EnsurePhase(TycoonPhase.DayOpen);
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            if (visit.State != VisitState.Waiting || !Floor.Seated.Contains(visit))
                throw new InvalidOperationException("That customer is not waiting at the bar.");
            if (Glass.IsEmpty) throw new InvalidOperationException("Nothing in the glass.");

            PouringId = null;
            var match = RatioRecipeMatcher.Match(Glass, _recipes, IngredientOf);
            var applied = PourResolver.Resolve(Glass, match, IngredientOf);
            var matchKind = ServiceJudge.Compare(visit.Order, match, Glass, IngredientOf);
            var verdict = ServiceJudge.Judge(visit, matchKind, applied, CrowdToday);

            visit.Regular?.Stats.Apply(applied);
            visit.Resolve(verdict, verdict.OrdersAgain ? RollOrder() : null);
            if (visit.State != VisitState.Waiting)
                visit.Regular?.RecordVisit((int)Math.Round(verdict.Satisfaction * 3));

            Money += verdict.Total;
            _dayIncome += verdict.Total;
            Glass = new GlassContents(_config.GlassCapacity);
            return verdict;
        }

        // ── day end: invoice, stock, market (GDD 23 §6–§8) ──────────────────────

        /// <summary>Everything below full, refilled at once. Books the expense.</summary>
        public int RefillShelf()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            int cost = _shelf.RefillCost(_config.RefillPricePerCapacity);
            if (cost == 0) return 0;
            Money -= cost;
            _dayExpenses += cost;
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

            Money -= offer.Price;
            _dayExpenses += offer.Price;
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
            Money -= price;
            _dayExpenses += price;
            Seats++;
            return price;
        }

        /// <summary>
        /// Closes the books on today and opens tomorrow — or the doors for good: three
        /// consecutive red days end the run (GDD 23 §6).
        /// </summary>
        public DayResult ContinueToNextDay()
        {
            EnsurePhase(TycoonPhase.DayEnd);
            var result = Ledger.CloseDay(Day, _dayIncome, _dayExpenses, Floor.AverageSatisfaction);

            if (Ledger.IsBankrupt)
            {
                Phase = TycoonPhase.Closed;
                return result;
            }

            Day++;
            CrowdToday = Ledger.TomorrowsCrowd;
            _dayIncome = 0;
            _dayExpenses = 0;
            Glass = new GlassContents(_config.GlassCapacity);
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
