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
    /// the persistent deck, the patron roster and the recipe levels; spins up a
    /// <see cref="RoundController"/> per customer and pays out tips (GDD 7.5) on wins.
    /// Deterministic: all randomness comes from the injected <see cref="RunRng"/>.
    /// VIP special rules and the Back Room inventory arrive in later slices — the BackRoom
    /// phase currently just gates advancement.
    /// </summary>
    public sealed class RunController
    {
        private readonly Deck _deck;
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

        /// <summary>The VIP whose rules govern the current order; null for regular customers.</summary>
        public VipDefinition CurrentVip { get; private set; }

        public RunController(IEnumerable<IngredientCard> cards, IReadOnlyList<RecipeDefinition> recipes,
            RunRng rng, IEnumerable<PatronInstance> patrons = null, RunConfig config = null,
            IReadOnlyList<PatronDefinition> patronPool = null, IReadOnlyList<ToolDefinition> toolPool = null,
            IReadOnlyList<VipDefinition> vipPool = null, IReadOnlyList<VoucherDefinition> voucherPool = null)
        {
            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _patrons = patrons != null ? new List<PatronInstance>(patrons) : new List<PatronInstance>();
            _patronPool = patronPool ?? Array.Empty<PatronDefinition>();
            _toolPool = toolPool ?? Array.Empty<ToolDefinition>();
            _vipPool = vipPool ?? Array.Empty<VipDefinition>();
            _voucherPool = voucherPool ?? Array.Empty<VoucherDefinition>();
            Config = config ?? RunConfig.Default;
            Money = Config.StartingMoney;
            _deck = new Deck(cards);
            StartCustomer();
        }

        /// <summary>Delegates to the current round and settles the run state on a terminal result.</summary>
        public ScoreBreakdown Mix(IReadOnlyList<IngredientCard> selection)
        {
            EnsurePhase(RunPhase.CustomerRound);
            var breakdown = CurrentRound.Mix(selection);

            if (CurrentRound.Phase == RoundPhase.Won) OnCustomerSatisfied();
            else if (CurrentRound.Phase == RoundPhase.Lost) Phase = RunPhase.RunLost;

            return breakdown;
        }

        public void Restock(IReadOnlyList<IngredientCard> selection)
        {
            EnsurePhase(RunPhase.CustomerRound);
            CurrentRound.Restock(selection);
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
                default:
                    CurrentRound.ApplyTool(tool, targets);
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

        /// <summary>Rerolls the shop for its escalating fee (GDD 7: $5, +$1 per reroll).</summary>
        public void RerollShop()
        {
            EnsurePhase(RunPhase.BackRoom);
            if (Money < Shop.RerollCost)
                throw new InvalidOperationException($"Not enough money (${Money} < ${Shop.RerollCost}).");

            Money -= Shop.RerollCost;
            Shop.Reroll();
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
            // Golden cards pay for sitting on the rail when the customer is satisfied
            // (GDD 3.3) — count them before the leftovers go back to the cabinet.
            int goldenBonus = Config.GoldenCardBonus *
                CurrentRound.Rail.Count(c => c.Enhancement == Enhancement.Golden);
            _deck.Discard(CurrentRound.Rail);

            // Interest is computed on money held BEFORE this customer's payout (GDD 7.5:
            // banking to $25 is the intended play, so the payout itself must not compound).
            int interest = Math.Min(Money / Config.InterestPerDollars, Config.InterestCap);
            int baseTip = BaseTipFor(Slot);
            int unusedMixBonus = CurrentRound.MixesRemaining;
            int vipBonus = Slot == CustomerSlot.Vip ? Config.VipDefeatBonus : 0;

            double patronMoney = CurrentRound.PatronPayout;
            if (Slot == CustomerSlot.Vip)
            {
                patronMoney += PatronTriggers.ResolveMoney(EffectTrigger.OnNightEnd, _patrons,
                    new EffectContext(null, null, CurrentRound.MixesUsed, CurrentRound.RestocksUsed));
            }

            LastTips = new TipsBreakdown(baseTip, unusedMixBonus, interest, vipBonus,
                (int)patronMoney, goldenBonus);
            Money += LastTips.Total;

            bool runComplete = Night == Config.Nights && Slot == CustomerSlot.Vip;
            Phase = runComplete ? RunPhase.RunWon : RunPhase.BackRoom;
            if (Phase == RunPhase.BackRoom) OpenShop();
        }

        private void OpenShop()
        {
            // Walking into the Back Room is itself a patron trigger (Coat Check Girl et al).
            Money += (int)PatronTriggers.ResolveMoney(EffectTrigger.OnShopEnter, _patrons, EffectContext.Empty);
            var voucherCandidates = _voucherPool
                .Where(v => !_vouchers.Exists(owned => owned.Id == v.Id)).ToList();
            Shop = new ShopState(_rng.GetStream("shop"), PatronCandidates, _toolPool, _recipes,
                Config.ShopSlots, Config.BookPrice, Config.RerollBaseCost,
                firstShopOfRun: !_firstShopOpened,
                voucherCandidates: voucherCandidates, voucherRng: _rng.GetStream("voucher"),
                patronDiscount: VoucherValue(VoucherOp.PatronDiscount));
            _firstShopOpened = true;
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
            _deck.ResetForNewCustomer(_rng.GetStream("deck"));
            double target = Config.TargetProvider(Night, Slot);
            string name = $"Night {Night} — {SlotName(Slot)}";
            var roundConfig = Config.RoundConfig;
            if (_vouchers.Count > 0)
            {
                // Vouchers upgrade the base rules; VIP rules then modify on top.
                roundConfig = new RoundConfig(
                    roundConfig.RailSize + VoucherValue(VoucherOp.ExtraRail),
                    roundConfig.MaxMixSelection,
                    roundConfig.MixesPerCustomer + VoucherValue(VoucherOp.ExtraMix),
                    roundConfig.RestocksPerCustomer + VoucherValue(VoucherOp.ExtraRestock));
            }
            var ruleSet = VipRuleSet.Empty;
            string ruleText = null;
            CurrentVip = null;

            if (Slot == CustomerSlot.Vip && _vipPool.Count > 0)
            {
                CurrentVip = PickVip();
                name = $"Night {Night} — VIP: {CurrentVip.Name}";
                ruleText = CurrentVip.Description;
                (ruleSet, roundConfig, target) = ResolveVipRules(CurrentVip, roundConfig, target);
            }

            CurrentRound = new RoundController(_deck, _recipes,
                new CustomerOrder(name, target, ruleText), roundConfig, _recipeLevels, _patrons,
                ruleSet, _rng.GetStream("shatter"));
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
                        roundConfig = new RoundConfig(
                            Math.Max(1, roundConfig.RailSize + rule.IntValue),
                            roundConfig.MaxMixSelection,
                            roundConfig.MixesPerCustomer,
                            roundConfig.RestocksPerCustomer);
                        break;
                    case VipRuleKind.TargetScale:
                        target *= rule.DoubleValue;
                        break;
                }
            }

            return (new VipRuleSet(debuffed, onlyFirstMix, minRecipeLevel, eachMixDifferent),
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
