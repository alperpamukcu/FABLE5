using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    public enum ShopOfferKind
    {
        Patron,
        Tool,
        Book,
        Voucher,
        Pack
    }

    /// <summary>One purchasable slot in the Back Room.</summary>
    public sealed class ShopOffer
    {
        public ShopOfferKind Kind { get; }
        public PatronDefinition Patron { get; }
        public ToolDefinition Tool { get; }
        public RecipeDefinition Recipe { get; }
        public VoucherDefinition Voucher { get; }
        public PackKind Pack { get; }
        public int Price { get; }
        public bool Sold { get; private set; }

        public string DisplayName
        {
            get
            {
                switch (Kind)
                {
                    case ShopOfferKind.Patron: return Patron.Name;
                    case ShopOfferKind.Tool: return Tool.Name;
                    case ShopOfferKind.Voucher: return $"Voucher: {Voucher.Name}";
                    case ShopOfferKind.Pack: return PackCatalog.NameOf(Pack);
                    default: return $"Recipe Book: {Recipe.Name}";
                }
            }
        }

        private ShopOffer(ShopOfferKind kind, PatronDefinition patron, ToolDefinition tool,
            RecipeDefinition recipe, VoucherDefinition voucher, PackKind pack, int price)
        {
            Kind = kind;
            Patron = patron;
            Tool = tool;
            Recipe = recipe;
            Voucher = voucher;
            Pack = pack;
            Price = price;
        }

        internal static ShopOffer ForPatron(PatronDefinition patron, int discount, bool free = false) =>
            new ShopOffer(ShopOfferKind.Patron, patron, null, null, null, default,
                free ? 0 : Math.Max(1, patron.Cost - discount));

        internal static ShopOffer ForTool(ToolDefinition tool) =>
            new ShopOffer(ShopOfferKind.Tool, null, tool, null, null, default, tool.Cost);

        internal static ShopOffer ForBook(RecipeDefinition recipe, int price) =>
            new ShopOffer(ShopOfferKind.Book, null, null, recipe, null, default, price);

        internal static ShopOffer ForVoucher(VoucherDefinition voucher) =>
            new ShopOffer(ShopOfferKind.Voucher, null, null, null, voucher, default, voucher.Cost);

        internal static ShopOffer ForPack(PackKind pack, bool free = false) =>
            new ShopOffer(ShopOfferKind.Pack, null, null, null, null, pack,
                free ? 0 : PackCatalog.PriceOf(pack));

        internal void MarkSold() => Sold = true;
    }

    /// <summary>
    /// One Back Room visit (GDD 7): card slots offering Patrons/Tools/Books, and an
    /// escalating reroll. Voucher and Booster Pack slots arrive in M3. All randomness
    /// comes from the run's "shop" stream, so shops are seed-stable.
    /// </summary>
    public sealed class ShopState
    {
        private readonly SeededRng _rng;
        private readonly Func<IReadOnlyList<PatronDefinition>> _patronCandidates;
        private readonly IReadOnlyList<ToolDefinition> _toolPool;
        private readonly IReadOnlyList<RecipeDefinition> _recipes;
        private readonly int _slots;
        private readonly int _bookPrice;
        private readonly int _patronDiscount;
        private readonly int _rarePatronBoost;
        private readonly bool _packsFree;
        private bool _firstPatronFree;
        private readonly List<ShopOffer> _offers = new List<ShopOffer>();
        private readonly List<ShopOffer> _packOffers = new List<ShopOffer>();

        public IReadOnlyList<ShopOffer> Offers => _offers;
        public int RerollCost { get; private set; }

        /// <summary>The dedicated Voucher slot; null when every voucher is owned. Never rerolls.</summary>
        public ShopOffer VoucherOffer { get; }

        /// <summary>The two Booster Pack slots (GDD 7 layout). Never reroll.</summary>
        public IReadOnlyList<ShopOffer> PackOffers => _packOffers;

        internal ShopState(SeededRng rng, Func<IReadOnlyList<PatronDefinition>> patronCandidates,
            IReadOnlyList<ToolDefinition> toolPool, IReadOnlyList<RecipeDefinition> recipes,
            int slots, int bookPrice, int rerollBaseCost, bool firstShopOfRun,
            IReadOnlyList<VoucherDefinition> voucherCandidates = null, SeededRng voucherRng = null,
            int patronDiscount = 0, SeededRng packRng = null, int rarePatronBoost = 1,
            bool firstPatronFree = false, bool packsFree = false, bool forceSpeakeasyPack = false)
        {
            _rng = rng;
            _patronCandidates = patronCandidates;
            _toolPool = toolPool;
            _recipes = recipes;
            _slots = slots;
            _bookPrice = bookPrice;
            _patronDiscount = patronDiscount;
            _rarePatronBoost = rarePatronBoost;
            _firstPatronFree = firstPatronFree; // Loyal Tab (GDD 5.4)
            _packsFree = packsFree;             // On the House (GDD 5.4)
            RerollCost = rerollBaseCost;

            if (firstShopOfRun) GenerateFirstShop();
            else Generate();

            if (voucherCandidates != null && voucherCandidates.Count > 0 && voucherRng != null)
                VoucherOffer = ShopOffer.ForVoucher(
                    voucherCandidates[voucherRng.NextInt(voucherCandidates.Count)]);

            if (packRng != null) RollPackOffers(packRng, forceSpeakeasyPack);
        }

        /// <summary>Two distinct pack kinds among those the run's pools can actually fill.
        /// The Speakeasy Key tag forces one slot to be a Speakeasy Pack.</summary>
        private void RollPackOffers(SeededRng packRng, bool forceSpeakeasy)
        {
            var available = new List<PackKind> { PackKind.Cellar, PackKind.Distiller };
            if (_toolPool.Count > 0) available.Add(PackKind.BarKit);
            var candidates = _patronCandidates();
            if (candidates.Count > 0) available.Add(PackKind.Regulars);
            if (_toolPool.Count > 0 || candidates.Any(
                    p => p.Rarity == PatronRarity.Rare || p.Rarity == PatronRarity.Legendary))
                available.Add(PackKind.Speakeasy);

            if (forceSpeakeasy)
            {
                _packOffers.Add(ShopOffer.ForPack(PackKind.Speakeasy, _packsFree));
                available.Remove(PackKind.Speakeasy);
            }

            while (_packOffers.Count < 2 && available.Count > 0)
            {
                int pick = packRng.NextInt(available.Count);
                _packOffers.Add(ShopOffer.ForPack(available[pick], _packsFree));
                available.RemoveAt(pick);
            }
        }

        /// <summary>Regenerates the offers; the escalating fee is charged by the run layer.</summary>
        internal void Reroll()
        {
            Generate();
            RerollCost++;
        }

        /// <summary>GDD 11 pity rule: the first shop always teaches — 1 Common Patron + 1 Book.</summary>
        private void GenerateFirstShop()
        {
            _offers.Clear();
            var candidates = _patronCandidates();
            var commons = candidates.Where(p => p.Rarity == PatronRarity.Common).ToList();
            var pool = commons.Count > 0 ? commons : candidates.ToList();

            if (pool.Count > 0) _offers.Add(NewPatronOffer(pool[_rng.NextInt(pool.Count)]));
            _offers.Add(ShopOffer.ForBook(_recipes[_rng.NextInt(_recipes.Count)], _bookPrice));
            while (_offers.Count < _slots) _offers.Add(RollOffer(excludePatronIds: OfferedPatronIds()));
        }

        private void Generate()
        {
            _offers.Clear();
            for (int i = 0; i < _slots; i++)
                _offers.Add(RollOffer(excludePatronIds: OfferedPatronIds()));
        }

        /// <summary>The Loyal Tab tag makes the visit's first patron offer free (GDD 5.4).</summary>
        private ShopOffer NewPatronOffer(PatronDefinition patron)
        {
            var offer = ShopOffer.ForPatron(patron, _patronDiscount, _firstPatronFree);
            _firstPatronFree = false;
            return offer;
        }

        private HashSet<string> OfferedPatronIds() =>
            new HashSet<string>(_offers.Where(o => o.Kind == ShopOfferKind.Patron).Select(o => o.Patron.Id));

        private ShopOffer RollOffer(HashSet<string> excludePatronIds)
        {
            // Slot mix: patrons dominate (2/4), tools and books fill the rest (GDD 7 layout,
            // simplified until Booster Packs arrive in M3). Exhausted pools fall through
            // to the next kind; books never run out.
            int roll = _rng.NextInt(4);

            if (roll <= 1)
            {
                var candidates = _patronCandidates().Where(p => !excludePatronIds.Contains(p.Id)).ToList();
                var patron = PatronRoll.Weighted(_rng, candidates, _rarePatronBoost);
                if (patron != null)
                    return NewPatronOffer(patron);
            }

            if (roll <= 2 && _toolPool.Count > 0)
                return ShopOffer.ForTool(_toolPool[_rng.NextInt(_toolPool.Count)]);

            return ShopOffer.ForBook(_recipes[_rng.NextInt(_recipes.Count)], _bookPrice);
        }
    }
}
