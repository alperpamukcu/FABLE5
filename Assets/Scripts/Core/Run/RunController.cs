using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    public enum RunPhase
    {
        CustomerRound,
        BackRoom,
        RunWon,
        RunLost
    }

    /// <summary>
    /// One full run: 8 Nights × (Customer A, Customer B, VIP) per GDD 5.1. Owns the wallet,
    /// the shelf, the patron roster and the recipe levels; spins up a
    /// <see cref="RoundController"/> per customer and pays out tips (GDD 7.5) on wins.
    /// Deterministic: all randomness comes from the injected <see cref="RunRng"/>.
    /// VIP special rules and the Back Room inventory arrive in later slices — the BackRoom
    /// phase currently just gates advancement.
    /// </summary>
    public sealed class RunController
    {
        private readonly Shelf _shelf;
        private readonly IReadOnlyList<RecipeDefinition> _recipes;
        private readonly RunRng _rng;
        private readonly List<PatronInstance> _patrons;
        private readonly Dictionary<string, int> _recipeLevels = new Dictionary<string, int>();
        private readonly IReadOnlyList<PatronDefinition> _patronPool;
        private readonly IReadOnlyList<ToolDefinition> _toolPool;
        private readonly IReadOnlyList<VipDefinition> _vipPool;
        private readonly IReadOnlyList<VoucherDefinition> _voucherPool;
        private readonly List<ToolDefinition> _tools = new List<ToolDefinition>();
        private readonly List<VoucherDefinition> _vouchers = new List<VoucherDefinition>();
        private readonly HashSet<string> _usedVipIds = new HashSet<string>();
        private ToolDefinition _lastToolUsed;
        private bool _firstShopOpened;
        private readonly List<FavorTag> _favorTags = new List<FavorTag>();
        private int _bouncerUsedNight;
        private readonly RegularsRegistry _regulars;
        private readonly IReadOnlyList<IngredientCard> _brandCatalogue;

        private const int MaxFavorTags = 4;    // GDD 5.4: tags queue, max 4 held
        private const int InvestorPayout = 15; // GDD 5.4: Investor pays after the next VIP

        public RunConfig Config { get; }
        public int Night { get; private set; } = 1;
        public CustomerSlot Slot { get; private set; } = CustomerSlot.CustomerA;
        public int Money { get; private set; }
        public RunPhase Phase { get; private set; } = RunPhase.CustomerRound;
        public RoundController CurrentRound { get; private set; }

        /// <summary>Payout of the most recently satisfied customer; null before the first win.</summary>
        public TipsBreakdown LastTips { get; private set; }

        /// <summary>Slot order = scoring order. The shop mutates this list in the BackRoom slice.</summary>
        public IReadOnlyList<PatronInstance> Patrons => _patrons;

        public IReadOnlyDictionary<string, int> RecipeLevels => _recipeLevels;

        /// <summary>The run's recipe table (for the Recipe Book UI and previews).</summary>
        public IReadOnlyList<RecipeDefinition> Recipes => _recipes;

        /// <summary>Single-use consumables held (GDD 7.3, max <see cref="RunConfig.MaxToolSlots"/>).</summary>
        public IReadOnlyList<ToolDefinition> ToolInventory => _tools;

        /// <summary>Permanent run upgrades owned (GDD 7.4); each voucher at most once.</summary>
        public IReadOnlyList<VoucherDefinition> Vouchers => _vouchers;

        /// <summary>The current Back Room inventory; non-null only during the BackRoom phase.</summary>
        public ShopState Shop { get; private set; }

        /// <summary>A bought pack waiting for its pick/skip; non-null only in the BackRoom.</summary>
        public OpenPackState OpenPack { get; private set; }

        /// <summary>The tag granted by the most recent skip; null when nothing was grantable.</summary>
        public FavorTag? LastFavor { get; private set; }

        /// <summary>Human-readable line for the most recent favor (HUD log).</summary>
        public string LastFavorText { get; private set; }

        /// <summary>Held favor tags (GDD 5.4: queue of max 4, consumed automatically).</summary>
        public IReadOnlyList<FavorTag> FavorTags => _favorTags;

        /// <summary>Tonight's VIP, revealed when the Night begins (GDD 5.5); null without a pool.</summary>
        public VipDefinition TonightsVip { get; private set; }

        /// <summary>True while Customer A can still be skipped: their round is untouched.</summary>
        public bool CanSkipCustomerA =>
            Phase == RunPhase.CustomerRound && Slot == CustomerSlot.CustomerA &&
            CurrentRound.DrinksServed == 0 && CurrentRound.Glass.IsEmpty;

        /// <summary>Bouncer voucher (GDD 7.4 v1.1): once per Night, before the VIP is faced.</summary>
        public bool CanRerollTonightsVip =>
            TonightsVip != null && Slot != CustomerSlot.Vip &&
            (Phase == RunPhase.CustomerRound || Phase == RunPhase.BackRoom) &&
            _bouncerUsedNight != Night &&
            _vouchers.Exists(v => v.Op == VoucherOp.RerollVip);

        /// <summary>The VIP whose rules govern the current order; null for regular customers.</summary>
        public VipDefinition CurrentVip { get; private set; }

        /// <summary>
        /// Everyone this run has served, with their persistent emotional state (GDD 19 §10).
        /// Null when the run was built without archetypes — the emotion layer is opt-in until
        /// the content pass lands, and without it the run behaves exactly as it did before.
        /// </summary>
        public RegularsRegistry Regulars => _regulars;

        /// <summary>True when this run is playing the read-the-customer layer.</summary>
        public bool HasEmotionLayer => _regulars != null;

        /// <summary>
        /// This week's satisfaction quota — the run's only loss condition (fork B). Missing it
        /// ends the run; a single customer you couldn't read never does.
        /// </summary>
        public WeekQuota Quota { get; private set; }

        /// <summary>
        /// The week that just closed, with its final tally — the gate has already moved on
        /// by the time anyone wants to show or measure it. Null until the first week ends.
        /// </summary>
        public WeekQuota LastClosedWeek { get; private set; }

        public RunController(IEnumerable<IngredientCard> cards, IReadOnlyList<RecipeDefinition> recipes,
            RunRng rng, IEnumerable<PatronInstance> patrons = null, RunConfig config = null,
            IReadOnlyList<PatronDefinition> patronPool = null, IReadOnlyList<ToolDefinition> toolPool = null,
            IReadOnlyList<VipDefinition> vipPool = null, IReadOnlyList<VoucherDefinition> voucherPool = null,
            BarDefinition bar = null, IReadOnlyList<ArchetypeDefinition> archetypes = null,
            IReadOnlyList<IngredientCard> brandCatalogue = null)
        {
            _brandCatalogue = brandCatalogue ?? Array.Empty<IngredientCard>();
            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _patrons = patrons != null ? new List<PatronInstance>(patrons) : new List<PatronInstance>();
            _patronPool = patronPool ?? Array.Empty<PatronDefinition>();
            _toolPool = toolPool ?? Array.Empty<ToolDefinition>();
            _vipPool = vipPool ?? Array.Empty<VipDefinition>();
            _voucherPool = voucherPool ?? Array.Empty<VoucherDefinition>();
            Config = config ?? RunConfig.Default;
            Money = Config.StartingMoney;
            if (archetypes != null && archetypes.Count > 0)
                _regulars = new RegularsRegistry(archetypes);
            Quota = new WeekQuota(1, Config.QuotaProvider(1));
            var startingCards = new List<IngredientCard>(cards);
            if (bar != null) ApplyBar(bar, startingCards);
            _shelf = BuildShelf(startingCards);
            StartCustomer();


        }

        /// <summary>Applies the Bar's starting configuration (GDD 9) before the first deal.</summary>
        private void ApplyBar(BarDefinition bar, List<IngredientCard> startingCards)
        {
            Money = Math.Max(0, Money + bar.MoneyDelta);

            foreach (var spec in bar.ExtraCards)
                startingCards.Add(new IngredientCard(
                    $"bar_{spec.Type}_{spec.Flavor}".ToLowerInvariant(),
                    $"{spec.Type} {spec.Flavor}", spec.Type, spec.Flavor));

            foreach (var pair in bar.RecipeLevels)
                _recipeLevels[pair.Key] = pair.Value;

            var barRng = _rng.GetStream("bar");
            for (int i = 0; i < bar.RandomRarePatrons; i++)
            {
                var rares = _patronPool
                    .Where(p => p.Rarity == PatronRarity.Rare)
                    .Where(p => !_patrons.Exists(owned => owned.Definition.Id == p.Id))
                    .ToList();
                if (rares.Count == 0 || _patrons.Count >= Config.MaxPatronSlots) break;
                _patrons.Add(new PatronInstance(rares[barRng.NextInt(rares.Count)]));
            }
        }

        /// <summary>
        /// Delegates to the current round and settles the run state when the visit ends.
        /// Falling short of an order no longer ends the run (fork B): the customer simply
        /// leaves, having got whatever they got, and the week's quota does the judging.
        /// </summary>
        public ScoreBreakdown Serve()
        {
            EnsurePhase(RunPhase.CustomerRound);
            var breakdown = CurrentRound.Serve();

            if (CurrentRound.Phase == RoundPhase.Won) OnCustomerSatisfied();
            else if (CurrentRound.Phase == RoundPhase.Closed) OnCustomerLeft();

            return breakdown;
        }

        /// <summary>Starts pouring a bottle into the glass (GDD 21 §3).</summary>
        public void BeginPour(string ingredientId)
        {
            EnsurePhase(RunPhase.CustomerRound);
            CurrentRound.BeginPour(ingredientId);
        }

        /// <summary>Advances the held pour; returns the volume that actually went in.</summary>
        public double PourTick(double seconds)
        {
            EnsurePhase(RunPhase.CustomerRound);
            return CurrentRound.PourTick(seconds);
        }

        public void EndPour() => CurrentRound?.EndPour();

        /// <summary>Pours an exact measure — the tap-to-measure input mode (GDD 21 §10).</summary>
        public double PourMeasure(string ingredientId, double volume)
        {
            EnsurePhase(RunPhase.CustomerRound);
            return CurrentRound.PourMeasure(ingredientId, volume);
        }

        /// <summary>Bins the glass — the volume is gone.</summary>
        public void DiscardGlass()
        {
            EnsurePhase(RunPhase.CustomerRound);
            CurrentRound.Discard();
        }

        /// <summary>
        /// Uses a held Tool during the current customer round. Rail Tools act on the
        /// selected cards; run Tools (Tab Ledger, Bottle Opener) ignore the selection.
        /// </summary>
        public void UseTool(ToolDefinition tool, IReadOnlyList<IngredientCard> targets)
        {
            EnsurePhase(RunPhase.CustomerRound);
            if (!_tools.Contains(tool))
                throw new InvalidOperationException($"'{tool?.Name}' is not in the tool inventory.");

            switch (tool.Op)
            {
                case ToolOp.DoubleMoney:
                    Money += Math.Min(Money, Config.MoneyDoubleCap);
                    break;
                case ToolOp.CreateLastTool:
                    if (_lastToolUsed == null)
                        throw new InvalidOperationException("No Tool has been used yet this run.");
                    _tools.Remove(tool);
                    _tools.Add(_lastToolUsed); // the freed slot takes the recreated tool
                    return;                    // the opener itself never counts as last used
                case ToolOp.RevealReading:
                    if (!CurrentRound.Customer.HasEmotion)
                        throw new InvalidOperationException("This customer has no reading to overhear.");
                    if (!CurrentRound.NarrowDarkestReading())
                        throw new InvalidOperationException("Nothing left to learn about them.");
                    break;
                default:
                    ApplyBottleTool(tool, targets);
                    break;
            }

            _tools.Remove(tool); // single-use
            _lastToolUsed = tool;
        }

        /// <summary>Buys the shop offer at <paramref name="index"/> (Back Room only).</summary>
        public void BuyOffer(int index)
        {
            EnsurePhase(RunPhase.BackRoom);
            if (index < 0 || index >= Shop.Offers.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            var offer = Shop.Offers[index];
            if (offer.Sold) throw new InvalidOperationException("Offer already sold.");
            if (Money < offer.Price)
                throw new InvalidOperationException($"Not enough money (${Money} < ${offer.Price}).");

            switch (offer.Kind)
            {
                case ShopOfferKind.Patron:
                    if (_patrons.Count >= Config.MaxPatronSlots)
                        throw new InvalidOperationException("All patron slots are taken.");
                    if (_patrons.Exists(p => p.Definition.Id == offer.Patron.Id))
                        throw new InvalidOperationException($"{offer.Patron.Name} already sits at the bar.");
                    _patrons.Add(new PatronInstance(offer.Patron));
                    break;
                case ShopOfferKind.Tool:
                    if (_tools.Count >= Config.MaxToolSlots)
                        throw new InvalidOperationException("Tool inventory is full.");
                    _tools.Add(offer.Tool);
                    break;
                case ShopOfferKind.Book:
                    _recipeLevels[offer.Recipe.Id] = RecipeLevelOf(offer.Recipe.Id) + 1;
                    break;
            }

            Money -= offer.Price;
            offer.MarkSold();
        }

        /// <summary>Buys the shop's Voucher slot: a permanent upgrade for the rest of the run.</summary>
        public void BuyVoucher()
        {
            EnsurePhase(RunPhase.BackRoom);
            var offer = Shop.VoucherOffer;
            if (offer == null) throw new InvalidOperationException("No voucher on offer.");
            if (offer.Sold) throw new InvalidOperationException("Voucher already sold.");
            if (Money < offer.Price)
                throw new InvalidOperationException($"Not enough money (${Money} < ${offer.Price}).");

            _vouchers.Add(offer.Voucher);
            Money -= offer.Price;
            offer.MarkSold();
        }

        /// <summary>
        /// Skips Customer A for a Regular's Favor (GDD 5.2): no tips, no Back Room,
        /// straight to Customer B with a random seeded reward. Only allowed while the
        /// round is untouched.
        /// </summary>
        public FavorTag? SkipCustomerA()
        {
            if (!CanSkipCustomerA)
                throw new InvalidOperationException(
                    "Only an untouched Customer A round can be skipped.");

            GrantFavor(_rng.GetStream("favor"));

            Slot = CustomerSlot.CustomerB;
            StartCustomer();
            return LastFavor;
        }

        /// <summary>One random tag, no duplicates held, queue capped at 4 (GDD 5.4).
        /// Word of Mouth resolves immediately and only rolls when it can apply.</summary>
        private void GrantFavor(SeededRng rng)
        {
            var eligible = new List<FavorTag>();
            if (_favorTags.Count < MaxFavorTags)
            {
                foreach (FavorTag tag in Enum.GetValues(typeof(FavorTag)))
                    if (tag != FavorTag.WordOfMouth && !_favorTags.Contains(tag))
                        eligible.Add(tag);
            }
            if (_patrons.Count < Config.MaxPatronSlots && CommonCandidates().Count > 0)
                eligible.Add(FavorTag.WordOfMouth);

            if (eligible.Count == 0)
            {
                LastFavor = null;
                LastFavorText = "Regular's Favor: the regulars had nothing left to offer.";
                return;
            }

            var granted = eligible[rng.NextInt(eligible.Count)];
            if (granted == FavorTag.WordOfMouth)
            {
                var commons = CommonCandidates();
                var patron = commons[rng.NextInt(commons.Count)];
                _patrons.Add(new PatronInstance(patron));
                SetFavor(granted, $"Regular's Favor — Word of Mouth: {patron.Name} joins the bar!");
                return;
            }

            _favorTags.Add(granted);
            SetFavor(granted, $"Regular's Favor — {TagText(granted)}");
        }

        private List<PatronDefinition> CommonCandidates() =>
            _patronPool.Where(p => p.Rarity == PatronRarity.Common)
                .Where(p => !_patrons.Exists(owned => owned.Definition.Id == p.Id)).ToList();

        private static string TagText(FavorTag tag)
        {
            switch (tag)
            {
                case FavorTag.LoyalTab: return "Loyal Tab: next shop, one Patron is free!";
                case FavorTag.OnTheHouse: return "On the House: next shop, packs cost $0!";
                case FavorTag.DoubleTip: return "Double Tip: the next tip is doubled!";
                case FavorTag.Investor: return "Investor: +$15 after beating the next VIP!";
                case FavorTag.TopShelfCellar: return "Top Shelf Cellar: next Cellar Pack is all Top Shelf!";
                case FavorTag.SpeakeasyKey: return "Speakeasy Key: next shop stocks a Speakeasy Pack!";
                default: return "Quick Hands: +1 Mix for the next customer!";
            }
        }

        private bool ConsumeTag(FavorTag tag) => _favorTags.Remove(tag);

        private void SetFavor(FavorTag tag, string text)
        {
            LastFavor = tag;
            LastFavorText = text;
        }

        /// <summary>Bouncer voucher: reroll tonight's VIP to a random valid one (no repeats).</summary>
        public void RerollTonightsVip()
        {
            if (!CanRerollTonightsVip)
                throw new InvalidOperationException("The Bouncer can't act right now.");
            TonightsVip = PickVip();
            _bouncerUsedNight = Night;
        }

        /// <summary>Buys one of the shop's two Booster Pack slots and opens it.</summary>
        public void BuyPack(int index)
        {
            EnsurePhase(RunPhase.BackRoom);
            if (OpenPack != null) throw new InvalidOperationException("Resolve the open pack first.");
            if (index < 0 || index >= Shop.PackOffers.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            var offer = Shop.PackOffers[index];
            if (offer.Sold) throw new InvalidOperationException("Pack already sold.");
            if (Money < offer.Price)
                throw new InvalidOperationException($"Not enough money (${Money} < ${offer.Price}).");

            Money -= offer.Price;
            offer.MarkSold();
            OpenPack = GeneratePack(offer.Pack);
        }

        /// <summary>Takes one reward from the open pack. Throws (pack stays open) when the
        /// reward can't be applied — e.g. patron slots or the tool inventory are full.</summary>
        public void PickFromPack(int optionIndex)
        {
            if (OpenPack == null) throw new InvalidOperationException("No pack is open.");
            if (optionIndex < 0 || optionIndex >= OpenPack.Options.Count)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));

            var option = OpenPack.Options[optionIndex];
            switch (option.Kind)
            {
                case PackOptionKind.IngredientCard:
                    // A pack ingredient now joins the shelf as a new bottle. Drawing a
                    // duplicate of something already stocked tops that bottle up instead,
                    // since shelf bottles are unique by id.
                    var existing = _shelf.Find(option.Card.Id);
                    if (existing != null) existing.Refill();
                    else _shelf.Add(new ShelfBottle(option.Card));
                    break;
                case PackOptionKind.Patron:
                    if (_patrons.Count >= Config.MaxPatronSlots)
                        throw new InvalidOperationException("All patron slots are taken.");
                    if (_patrons.Exists(p => p.Definition.Id == option.Patron.Id))
                        throw new InvalidOperationException($"{option.Patron.Name} already sits at the bar.");
                    _patrons.Add(new PatronInstance(option.Patron));
                    break;
                case PackOptionKind.Tool:
                    if (_tools.Count >= Config.MaxToolSlots)
                        throw new InvalidOperationException("Tool inventory is full.");
                    _tools.Add(option.Tool);
                    break;
                case PackOptionKind.Book:
                    _recipeLevels[option.Recipe.Id] = RecipeLevelOf(option.Recipe.Id) + 1;
                    break;
            }
            OpenPack = null;
        }

        /// <summary>Closes the open pack without taking anything (money stays spent).</summary>
        public void SkipPack()
        {
            if (OpenPack == null) throw new InvalidOperationException("No pack is open.");
            OpenPack = null;
        }

        private OpenPackState GeneratePack(PackKind kind)
        {
            var rng = _rng.GetStream("pack_contents");
            var options = new List<PackOption>();

            switch (kind)
            {
                case PackKind.Cellar:
                    int count = 3 + VoucherValue(VoucherOp.PackExtraCard); // Deep Cellar
                    bool topShelf = ConsumeTag(FavorTag.TopShelfCellar);
                    for (int i = 0; i < count; i++)
                    {
                        var card = RollPackCard(rng);
                        if (topShelf) card.Refine(QualityTier.TopShelf);
                        options.Add(PackOption.ForCard(card));
                    }
                    break;
                case PackKind.Distiller:
                    foreach (var recipe in RollDistinct(rng, _recipes, 2))
                        options.Add(PackOption.ForBook(recipe));
                    break;
                case PackKind.BarKit:
                    for (int i = 0; i < 2 && _toolPool.Count > 0; i++)
                        options.Add(PackOption.ForTool(_toolPool[rng.NextInt(_toolPool.Count)]));
                    break;
                case PackKind.Regulars:
                    foreach (var patron in RollDistinct(rng, PatronCandidates(), 2))
                        options.Add(PackOption.ForPatron(patron));
                    break;
                case PackKind.Speakeasy:
                    // GDD 7.1: the "special means" — the one place Legendaries can roll.
                    var rarePlus = PatronCandidates()
                        .Where(p => p.Rarity == PatronRarity.Rare || p.Rarity == PatronRarity.Legendary)
                        .ToList();
                    if (rarePlus.Count > 0)
                    {
                        var legendaries = rarePlus.Where(p => p.Rarity == PatronRarity.Legendary).ToList();
                        var pool = legendaries.Count > 0 && rng.NextInt(4) == 0 ? legendaries : rarePlus;
                        options.Add(PackOption.ForPatron(pool[rng.NextInt(pool.Count)]));
                    }
                    if (_toolPool.Count > 0)
                        options.Add(PackOption.ForTool(_toolPool[rng.NextInt(_toolPool.Count)]));
                    options.Add(PackOption.ForBook(_recipes[rng.NextInt(_recipes.Count)]));
                    break;
            }
            return new OpenPackState(kind, options);
        }

        /// <summary>A fresh random ingredient card: any type, flavor 2–10, 1-in-4 quality upgrade.</summary>
        private static IngredientCard RollPackCard(SeededRng rng)
        {
            var types = (IngredientType[])Enum.GetValues(typeof(IngredientType));
            var type = types[rng.NextInt(types.Length)];
            int flavor = 2 + rng.NextInt(9);
            var quality = QualityTier.HousePour;
            if (rng.NextInt(4) == 0)
            {
                var upgrades = new[] { QualityTier.TopShelf, QualityTier.BarrelAged, QualityTier.Signature };
                quality = upgrades[rng.NextInt(upgrades.Length)];
            }
            return new IngredientCard($"pack_{type}_{flavor}".ToLowerInvariant(),
                $"{type} {flavor}", type, flavor, quality);
        }

        private static List<T> RollDistinct<T>(SeededRng rng, IReadOnlyList<T> pool, int count)
        {
            var indices = Enumerable.Range(0, pool.Count).ToList();
            var picks = new List<T>();
            for (int i = 0; i < count && indices.Count > 0; i++)
            {
                int at = rng.NextInt(indices.Count);
                picks.Add(pool[indices[at]]);
                indices.RemoveAt(at);
            }
            return picks;
        }

        /// <summary>Rerolls the shop for its escalating fee (GDD 7: $5, +$1 per reroll).</summary>
        public void RerollShop()
        {
            EnsurePhase(RunPhase.BackRoom);
            if (Money < Shop.RerollCost)
                throw new InvalidOperationException($"Not enough money (${Money} < ${Shop.RerollCost}).");

            Money -= Shop.RerollCost;
            Shop.Reroll();
        }

        /// <summary>Sells a held Tool for half price, rounded up (GDD 7.0 v1.1).</summary>
        public void SellTool(ToolDefinition tool)
        {
            if (Phase != RunPhase.CustomerRound && Phase != RunPhase.BackRoom)
                throw new InvalidOperationException($"Run is over ({Phase}).");
            if (!_tools.Remove(tool))
                throw new InvalidOperationException("That tool is not in the inventory.");

            Money += (tool.Cost + 1) / 2;
        }

        /// <summary>Sells a patron for half price, rounded up (GDD 8).</summary>
        public void SellPatron(PatronInstance patron)
        {
            if (Phase != RunPhase.CustomerRound && Phase != RunPhase.BackRoom)
                throw new InvalidOperationException($"Run is over ({Phase}).");
            if (!_patrons.Remove(patron))
                throw new InvalidOperationException("That patron is not at the bar.");

            Money += (patron.Definition.Cost + 1) / 2;
        }

        public int RecipeLevelOf(string recipeId) =>
            _recipeLevels.TryGetValue(recipeId, out int level) ? level : 1;

        /// <summary>Leaves the Back Room and deals the next customer.</summary>
        public void ContinueToNextCustomer()
        {
            EnsurePhase(RunPhase.BackRoom);
            if (OpenPack != null) throw new InvalidOperationException("Resolve the open pack first.");
            Shop = null;

            if (Slot == CustomerSlot.Vip)
            {
                Night++;
                Slot = CustomerSlot.CustomerA;
            }
            else
            {
                Slot = Slot + 1;
            }

            Phase = RunPhase.CustomerRound;
            StartCustomer();
        }

        private void OnCustomerSatisfied()
        {
            // Enhancement.Golden paid per Golden card left on the rail at customer end.
            // There is no rail under the pour system, so it is a casualty of the pivot —
            // see the audit in Docs/PLAN_pour_pivot.md.
            const int goldenBonus = 0;

            // Interest is computed on money held BEFORE this customer's payout (GDD 7.5:
            // banking to $25 is the intended play, so the payout itself must not compound).
            int interest = Math.Min(Money / Config.InterestPerDollars, Config.InterestCap);
            int baseTip = BaseTipFor(Slot);
            if (ConsumeTag(FavorTag.DoubleTip)) baseTip *= 2;
            int favorBonus = Slot == CustomerSlot.Vip && ConsumeTag(FavorTag.Investor)
                ? InvestorPayout : 0;
            int unusedMixBonus = CurrentRound.DrinksRemaining;
            int vipBonus = Slot == CustomerSlot.Vip ? Config.VipDefeatBonus : 0;

            double patronMoney = CurrentRound.PatronPayout;
            if (Slot == CustomerSlot.Vip)
            {
                patronMoney += PatronTriggers.ResolveMoney(EffectTrigger.OnNightEnd, _patrons,
                    new EffectContext(null, null, CurrentRound.DrinksServed, 0,
                        noSpills: CurrentRound.Spills == 0));
            }

            LastTips = new TipsBreakdown(baseTip, unusedMixBonus, interest, vipBonus,
                (int)patronMoney, goldenBonus, favorBonus);
            Money += LastTips.Total;

            EndCustomer();
        }

        /// <summary>
        /// The customer ran out of patience before the order was filled. They still drank
        /// what you made them, so whatever the serves earned still counts toward the week —
        /// there are just no tips in it.
        /// </summary>
        private void OnCustomerLeft()
        {
            LastTips = TipsBreakdown.None;
            EndCustomer();
        }

        /// <summary>
        /// Closes out a visit: banks its satisfaction, and — when the week's last VIP has
        /// been served — runs the quota gate, drifts everyone, and opens the next week.
        /// </summary>
        private void EndCustomer()
        {
            Quota.Add(CurrentRound.SatisfactionEarned);
            CurrentRound.Customer.Regular?.RecordVisit(CurrentRound.SatisfactionEarned);

            bool runComplete = Night == Config.Nights && Slot == CustomerSlot.Vip;
            bool weekCloses = Slot == CustomerSlot.Vip && QuotaTable.IsWeekEnd(Night);

            // The quota only judges runs that are actually playing the emotion layer; a run
            // built without archetypes has no satisfaction to earn and must not be failed.
            if (weekCloses && HasEmotionLayer)
            {
                LastClosedWeek = Quota.Snapshot();
                if (!Quota.Met)
                {
                    Phase = RunPhase.RunLost;
                    return;
                }

                _regulars.DriftAll(_rng.GetStream("drift"));
                if (!runComplete)
                    Quota = new WeekQuota(Quota.Week + 1, Config.QuotaProvider(Quota.Week + 1));
            }

            Phase = runComplete ? RunPhase.RunWon : RunPhase.BackRoom;
            if (Phase == RunPhase.BackRoom) OpenShop();
        }

        private void OpenShop()
        {
            // Deliveries come at closing time: the market only stocks after the VIP.
            _marketOffers = Slot == CustomerSlot.Vip
                ? Market.OffersFor(_shelf, _brandCatalogue)
                : new List<MarketOffer>();

            // Walking into the Back Room is itself a patron trigger (Coat Check Girl et al).
            Money += (int)PatronTriggers.ResolveMoney(EffectTrigger.OnShopEnter, _patrons, EffectContext.Empty);
            var voucherCandidates = _voucherPool
                .Where(v => !_vouchers.Exists(owned => owned.Id == v.Id)).ToList();
            Shop = new ShopState(_rng.GetStream("shop"), PatronCandidates, _toolPool, _recipes,
                Config.ShopSlots, Config.BookPrice, Config.RerollBaseCost,
                firstShopOfRun: !_firstShopOpened,
                voucherCandidates: voucherCandidates, voucherRng: _rng.GetStream("voucher"),
                patronDiscount: VoucherValue(VoucherOp.PatronDiscount),
                packRng: _rng.GetStream("packs"),
                rarePatronBoost: 1 + VoucherValue(VoucherOp.RarePatronBoost),
                firstPatronFree: ConsumeTag(FavorTag.LoyalTab),
                packsFree: ConsumeTag(FavorTag.OnTheHouse),
                forceSpeakeasyPack: ConsumeTag(FavorTag.SpeakeasyKey));
            _firstShopOpened = true;
        }

        /// <summary>
        /// Turns the starting cards into the shelf (GDD 21 §2). Duplicates in the old starter
        /// deck (two Rye, two Single Malt) become one bottle with more in it — the deck used
        /// copies to weight the draw, and the shelf has no draw to weight.
        /// </summary>
        private static Shelf BuildShelf(IEnumerable<IngredientCard> cards)
        {
            var bottles = new List<ShelfBottle>();
            var byId = new Dictionary<string, ShelfBottle>();
            foreach (var card in cards)
            {
                if (byId.TryGetValue(card.Id, out var bottle))
                {
                    bottle.Upgrade(capacityDelta: DuplicateBottleBonus, pourRateDelta: 0);
                    continue;
                }
                bottle = new ShelfBottle(card);
                byId[card.Id] = bottle;
                bottles.Add(bottle);
            }
            return new Shelf(bottles);
        }

        /// <summary>Extra capacity a duplicate starter card contributes to its bottle.</summary>
        private const double DuplicateBottleBonus = 2.0;

        /// <summary>
        /// Tools that used to rework rail cards now rework shelf bottles (GDD 21 §7.1), which
        /// makes them permanent upgrades rather than one-shot card edits. Ice Pick and Bar
        /// Spoon have no bottle equivalent and are casualties — see the audit.
        /// </summary>
        private void ApplyBottleTool(ToolDefinition tool, IReadOnlyList<IngredientCard> targets)
        {
            if (targets == null || targets.Count == 0)
                throw new ArgumentException("Pick a bottle.", nameof(targets));
            if (targets.Count > tool.MaxTargets)
                throw new ArgumentException($"{tool.Name} works on at most {tool.MaxTargets} bottle(s).",
                    nameof(targets));

            foreach (var target in targets)
            {
                var bottle = _shelf.Find(target.Id);
                if (bottle == null)
                    throw new ArgumentException($"'{target.Id}' is not on the shelf.", nameof(targets));

                switch (tool.Op)
                {
                    case ToolOp.Enhance: bottle.Ingredient.Enhance(tool.Enhancement); break;
                    case ToolOp.ConvertType: bottle.Ingredient.ConvertType(tool.ConvertTo); break;
                    case ToolOp.SetQuality: bottle.Ingredient.Refine(tool.Quality); break;
                    case ToolOp.ShiftValue: bottle.Ingredient.ShiftFlavor(tool.ShiftAmount); break;
                    default:
                        throw new InvalidOperationException(
                            $"{tool.Name} has no meaning under the pour system.");
                }
            }
        }

        /// <summary>Refills every bottle for a price, and wakes the refill patrons.</summary>
        public int RefillShelf()
        {
            EnsurePhase(RunPhase.BackRoom);
            int cost = _shelf.RefillCost(Config.RefillPricePerCapacity);
            if (cost == 0) return 0;
            if (Money < cost)
                throw new InvalidOperationException($"Not enough money (${Money} < ${cost}).");

            Money -= cost;
            _shelf.RefillAll();
            PatronTriggers.ResolveAccumulation(EffectTrigger.OnRefill, _patrons, EffectContext.Empty);
            Money += (int)PatronTriggers.ResolveMoney(EffectTrigger.OnRefill, _patrons, EffectContext.Empty);
            return cost;
        }

        /// <summary>The shelf, for the UI and the Back Room.</summary>
        public Shelf Shelf => _shelf;

        /// <summary>
        /// Brand upgrades on offer (GDD 22 §4). Non-empty only in the Back Room after a VIP —
        /// the market takes deliveries when the night closes, not between customers.
        /// </summary>
        public IReadOnlyList<MarketOffer> MarketOffers => _marketOffers;
        private List<MarketOffer> _marketOffers = new List<MarketOffer>();

        /// <summary>Buys a brand upgrade: the shelf bottle of that style is replaced, full.</summary>
        public void BuyBrand(int offerIndex)
        {
            EnsurePhase(RunPhase.BackRoom);
            if (offerIndex < 0 || offerIndex >= _marketOffers.Count)
                throw new ArgumentOutOfRangeException(nameof(offerIndex));
            var offer = _marketOffers[offerIndex];
            if (offer.Sold) throw new InvalidOperationException("That brand is already yours.");
            if (Money < offer.Price)
                throw new InvalidOperationException($"Not enough money (${Money} < ${offer.Price}).");

            var current = Market.FindByStyle(_shelf, offer.Style);
            if (current == null)
                throw new InvalidOperationException($"Nothing on the shelf pours {offer.Style}.");

            Money -= offer.Price;
            _shelf.Replace(current, new ShelfBottle(offer.Bottle));
            offer.MarkSold();
        }

        /// <summary>Sum of IntValues across owned vouchers of one op (0 when none owned).</summary>
        private int VoucherValue(VoucherOp op)
        {
            int total = 0;
            foreach (var voucher in _vouchers)
                if (voucher.Op == op) total += voucher.IntValue;
            return total;
        }

        private IReadOnlyList<PatronDefinition> PatronCandidates() =>
            _patronPool.Where(p => !_patrons.Exists(owned => owned.Definition.Id == p.Id)).ToList();

        private void StartCustomer()
        {
            double target = Config.TargetProvider(Night, Slot);
            string name = $"Night {Night} — {SlotName(Slot)}";
            var roundConfig = Config.RoundConfig;
            if (_vouchers.Count > 0)
            {
                // Vouchers upgrade the base rules; VIP rules then modify on top. ExtraRail
                // and ExtraRestock died with the deck (see the audit); ExtraMix survives as
                // one more drink the customer will accept.
                roundConfig = roundConfig.With(
                    drinksPerCustomer: roundConfig.DrinksPerCustomer + VoucherValue(VoucherOp.ExtraMix));
            }
            if (ConsumeTag(FavorTag.QuickHands))
                roundConfig = roundConfig.With(drinksPerCustomer: roundConfig.DrinksPerCustomer + 1);

            // GDD 5.5: tonight's VIP is revealed when the Night begins, before Customer A,
            // so skip and economy decisions can react to it.
            if (Slot == CustomerSlot.CustomerA && _vipPool.Count > 0)
                TonightsVip = PickVip();

            var ruleSet = VipRuleSet.Empty;
            string ruleText = null;
            CurrentVip = null;

            if (Slot == CustomerSlot.Vip && TonightsVip != null)
            {
                CurrentVip = TonightsVip;
                name = $"Night {Night} — VIP: {CurrentVip.Name}";
                ruleText = CurrentVip.Description;
                (ruleSet, roundConfig, target) = ResolveVipRules(CurrentVip, roundConfig, target);
            }

            var order = BuildOrder(name, target, ruleText, ruleSet);
            CurrentRound = new RoundController(_shelf, _recipes,
                order, roundConfig, _recipeLevels, _patrons, ruleSet, Night);

            ResolveInformationPatrons(order);
        }

        /// <summary>
        /// Decides who is at the bar and how much of them the bartender can see tonight
        /// (GDD 19 §3/§10). Without archetypes this is the pre-pivot anonymous order.
        /// </summary>
        private CustomerOrder BuildOrder(string name, double target, string ruleText, VipRuleSet rules)
        {
            if (!HasEmotionLayer) return new CustomerOrder(name, target, ruleText);

            var regular = _regulars.RollNext(_rng.GetStream("customer"));
            var readRng = _rng.GetStream("read");

            // A face you have seen before is read through what you remember of them, decayed;
            // a stranger gets a fresh roll of tiers.
            bool returning = regular.Visits > 0;
            var read = returning
                ? CustomerReadFactory.FromTiers(regular.Stats, regular.KnownTiers, Night, readRng,
                    regular.Relationship, regular.BaseDemand)
                : CustomerReadFactory.Build(regular.Stats, Night, readRng,
                    regular.Relationship, regular.BaseDemand);

            // A VIP can blank the licence, print it in full, or plant a lie on it (GDD 19 §8).
            read = CustomerReadFactory.ApplyVipRules(read, regular.Stats, rules, Night, readRng,
                regular.Relationship);

            // Every visit teaches you something, so what you know is refreshed each time —
            // not only on the first meeting. Drift still stales it between weeks.
            regular.RememberTiers(TiersOf(read));

            string label = ruleText != null ? $"{regular.Name} — {name}" : regular.Name;
            return new CustomerOrder(label, target, regular, read, ruleText);
        }

        /// <summary>
        /// The information patrons take their turn as the customer sits down (GDD 19 §8).
        /// They buy clarity, never score — a Gossip at the end of the bar tells you something
        /// about the person who just walked in, and that is the whole effect.
        /// </summary>
        private void ResolveInformationPatrons(CustomerOrder order)
        {
            if (!order.HasEmotion || _patrons.Count == 0) return;

            var context = new EffectContext(null, null, 0, 0,
                returningCustomer: order.Regular.Visits > 0);

            foreach (var patron in _patrons)
            {
                foreach (var effect in patron.Definition.Effects)
                {
                    if (effect.Trigger != EffectTrigger.OnCustomerStart) continue;
                    if (!effect.Condition.Evaluate(context)) continue;

                    switch (effect.Op)
                    {
                        case EffectOp.NarrowReading:
                            int times = Math.Max(1, (int)effect.Value);
                            for (int i = 0; i < times; i++)
                                if (!CurrentRound.NarrowDarkestReading()) break;
                            break;
                        case EffectOp.NarrowIntentReading:
                            CurrentRound.NarrowReading(order.Read.Intent);
                            break;
                    }
                }
            }
        }

        private static VisibilityTier[] TiersOf(CustomerRead read)
        {
            var tiers = new VisibilityTier[Emotions.Count];
            for (int i = 0; i < tiers.Length; i++) tiers[i] = read[Emotions.All[i]].Tier;
            return tiers;
        }

        /// <summary>Draws from the "vip" stream: finale VIP on the last Night, the gentle
        /// subset on Nights 1–2 (GDD 11), no repeats until the pool is exhausted.</summary>
        private VipDefinition PickVip()
        {
            var rng = _rng.GetStream("vip");

            if (Night == Config.Nights)
            {
                var finale = _vipPool.Where(v => v.FinaleOnly).ToList();
                if (finale.Count > 0) return finale[rng.NextInt(finale.Count)];
            }

            var candidates = _vipPool
                .Where(v => !v.FinaleOnly && !_usedVipIds.Contains(v.Id))
                .Where(v => Night > 2 || v.Gentle)
                .ToList();
            if (candidates.Count == 0)
            {
                _usedVipIds.Clear();
                candidates = _vipPool.Where(v => !v.FinaleOnly)
                    .Where(v => Night > 2 || v.Gentle).ToList();
            }
            if (candidates.Count == 0)
                candidates = _vipPool.Where(v => !v.FinaleOnly).ToList();
            if (candidates.Count == 0)
                candidates = _vipPool.ToList();

            var vip = candidates[rng.NextInt(candidates.Count)];
            _usedVipIds.Add(vip.Id);
            return vip;
        }

        private (VipRuleSet rules, RoundConfig config, double target) ResolveVipRules(
            VipDefinition vip, RoundConfig roundConfig, double target)
        {
            var debuffed = new HashSet<IngredientType>();
            bool onlyFirstMix = false;
            int minRecipeLevel = 0;
            bool eachMixDifferent = false;
            var readOverride = ReadOverride.None;
            bool oneReadingFalse = false;

            foreach (var rule in vip.Rules)
            {
                switch (rule.Kind)
                {
                    case VipRuleKind.DebuffType:
                        debuffed.Add(rule.Type);
                        break;
                    case VipRuleKind.DebuffRandomType:
                        var types = (IngredientType[])Enum.GetValues(typeof(IngredientType));
                        debuffed.Add(types[_rng.GetStream("vip").NextInt(types.Length)]);
                        break;
                    case VipRuleKind.OnlyFirstMixScores:
                        onlyFirstMix = true;
                        break;
                    case VipRuleKind.MinRecipeLevel:
                        minRecipeLevel = rule.IntValue;
                        break;
                    case VipRuleKind.EachMixDifferentRecipe:
                        eachMixDifferent = true;
                        break;
                    case VipRuleKind.RailSizeDelta:
                        // The rail is gone; the equivalent squeeze is a smaller glass, so
                        // The Health Inspector's -3 becomes -30% capacity.
                        roundConfig = roundConfig.With(glassCapacity:
                            Math.Max(0.3, roundConfig.GlassCapacity * (1 + rule.IntValue * 0.1)));
                        break;
                    case VipRuleKind.TargetScale:
                        target *= rule.DoubleValue;
                        break;
                    case VipRuleKind.AllReadingsUnknown:
                        readOverride = ReadOverride.AllUnknown;
                        break;
                    case VipRuleKind.AllReadingsExact:
                        readOverride = ReadOverride.AllExact;
                        break;
                    case VipRuleKind.OneReadingFalse:
                        oneReadingFalse = true;
                        break;
                }
            }

            return (new VipRuleSet(debuffed, onlyFirstMix, minRecipeLevel, eachMixDifferent,
                    readOverride, oneReadingFalse),
                roundConfig, target);
        }

        private int BaseTipFor(CustomerSlot slot)
        {
            switch (slot)
            {
                case CustomerSlot.CustomerA: return Config.TipCustomerA;
                case CustomerSlot.CustomerB: return Config.TipCustomerB;
                default: return Config.TipVip;
            }
        }

        private static string SlotName(CustomerSlot slot)
        {
            switch (slot)
            {
                case CustomerSlot.CustomerA: return "Customer A";
                case CustomerSlot.CustomerB: return "Customer B";
                default: return "VIP";
            }
        }

        private void EnsurePhase(RunPhase expected)
        {
            if (Phase != expected)
                throw new InvalidOperationException($"Expected phase {expected}, but run is in {Phase}.");
        }
    }
}
