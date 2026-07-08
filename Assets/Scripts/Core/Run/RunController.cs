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
        private readonly List<ToolDefinition> _tools = new List<ToolDefinition>();
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

        /// <summary>The current Back Room inventory; non-null only during the BackRoom phase.</summary>
        public ShopState Shop { get; private set; }

        public RunController(IEnumerable<IngredientCard> cards, IReadOnlyList<RecipeDefinition> recipes,
            RunRng rng, IEnumerable<PatronInstance> patrons = null, RunConfig config = null,
            IReadOnlyList<PatronDefinition> patronPool = null, IReadOnlyList<ToolDefinition> toolPool = null)
        {
            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _patrons = patrons != null ? new List<PatronInstance>(patrons) : new List<PatronInstance>();
            _patronPool = patronPool ?? Array.Empty<PatronDefinition>();
            _toolPool = toolPool ?? Array.Empty<ToolDefinition>();
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

        /// <summary>Uses a held Tool on rail cards during the current customer round.</summary>
        public void UseTool(ToolDefinition tool, IReadOnlyList<IngredientCard> targets)
        {
            EnsurePhase(RunPhase.CustomerRound);
            if (!_tools.Contains(tool))
                throw new InvalidOperationException($"'{tool?.Name}' is not in the tool inventory.");

            CurrentRound.ApplyTool(tool, targets);
            _tools.Remove(tool); // single-use
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
            // Rail leftovers go back to the cabinet before the payout is counted.
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

            LastTips = new TipsBreakdown(baseTip, unusedMixBonus, interest, vipBonus, (int)patronMoney);
            Money += LastTips.Total;

            bool runComplete = Night == Config.Nights && Slot == CustomerSlot.Vip;
            Phase = runComplete ? RunPhase.RunWon : RunPhase.BackRoom;
            if (Phase == RunPhase.BackRoom) OpenShop();
        }

        private void OpenShop()
        {
            Shop = new ShopState(_rng.GetStream("shop"), PatronCandidates, _toolPool, _recipes,
                Config.ShopSlots, Config.BookPrice, Config.RerollBaseCost,
                firstShopOfRun: !_firstShopOpened);
            _firstShopOpened = true;
        }

        private IReadOnlyList<PatronDefinition> PatronCandidates() =>
            _patronPool.Where(p => !_patrons.Exists(owned => owned.Definition.Id == p.Id)).ToList();

        private void StartCustomer()
        {
            _deck.ResetForNewCustomer(_rng.GetStream("deck"));
            double target = Config.TargetProvider(Night, Slot);
            string name = $"Night {Night} — {SlotName(Slot)}";
            CurrentRound = new RoundController(_deck, _recipes,
                new CustomerOrder(name, target), Config.RoundConfig, _recipeLevels, _patrons);
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
