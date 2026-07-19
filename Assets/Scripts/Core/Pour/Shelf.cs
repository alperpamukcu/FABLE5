using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// A bottle standing on the shelf (GDD 21 §6). Unlike the card it replaces, it is not
    /// drawn or discarded — it is always there, and it drains.
    /// </summary>
    public sealed class ShelfBottle
    {
        public IngredientCard Ingredient { get; }

        /// <summary>Full volume, in glass-capacities. Rises with the bottle's tier.</summary>
        public double Capacity { get; private set; }

        /// <summary>How much is left right now.</summary>
        public double Remaining { get; private set; }

        /// <summary>Volume per second while held. Faster is not strictly better — it is harder to stop.</summary>
        public double PourRate { get; private set; }

        /// <summary>Upgrade level (GDD 21 §7.1); 1 is the house pour.</summary>
        public int Tier { get; private set; } = 1;

        public string Id => Ingredient.Id;
        public bool IsEmpty => Remaining <= 0;

        public ShelfBottle(IngredientCard ingredient, double capacity = 6.0, double pourRate = 0.55)
        {
            Ingredient = ingredient ?? throw new ArgumentNullException(nameof(ingredient));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (pourRate <= 0) throw new ArgumentOutOfRangeException(nameof(pourRate));
            Capacity = capacity;
            Remaining = capacity;
            PourRate = pourRate;
        }

        /// <summary>
        /// Takes up to <paramref name="requested"/> and returns what was actually available.
        /// Running dry mid-pour is not a failure — you get what was left (PLAN P7).
        /// </summary>
        public double Draw(double requested)
        {
            if (requested <= 0) return 0;
            double drawn = Math.Min(requested, Remaining);
            Remaining -= drawn;
            return drawn;
        }

        public void Refill() => Remaining = Capacity;

        /// <summary>Upgrades the bottle: more in it, and it pours a little faster.</summary>
        public void Upgrade(double capacityDelta, double pourRateDelta)
        {
            Tier++;
            Capacity += capacityDelta;
            PourRate += pourRateDelta;
            Remaining = Math.Min(Remaining + capacityDelta, Capacity);
        }

        public override string ToString() => $"{Ingredient.Name} T{Tier} {Remaining:0.#}/{Capacity:0.#}";
    }

    /// <summary>
    /// Everything behind the bar (GDD 21 §2). Replaces <c>Deck</c>: there is no draw, no
    /// discard and no shuffle — every bottle is always in reach, and scarcity comes from
    /// bottles running dry instead of from what you happened to be dealt.
    /// </summary>
    public sealed class Shelf
    {
        private readonly List<ShelfBottle> _bottles = new List<ShelfBottle>();
        private readonly Dictionary<string, ShelfBottle> _byId = new Dictionary<string, ShelfBottle>();

        public Shelf(IEnumerable<ShelfBottle> bottles)
        {
            if (bottles == null) throw new ArgumentNullException(nameof(bottles));
            foreach (var bottle in bottles) Add(bottle);
            if (_bottles.Count == 0)
                throw new ArgumentException("A shelf needs at least one bottle.", nameof(bottles));
        }

        /// <summary>Shelf order — stable, because muscle memory for where a bottle lives is the point.</summary>
        public IReadOnlyList<ShelfBottle> Bottles => _bottles;

        public int Count => _bottles.Count;

        public ShelfBottle Find(string ingredientId) =>
            ingredientId != null && _byId.TryGetValue(ingredientId, out var bottle) ? bottle : null;

        /// <summary>
        /// Swaps one bottle for another in place, keeping its shelf position — muscle memory
        /// for where the vodka lives must survive upgrading the vodka (GDD 22 §4).
        /// </summary>
        public void Replace(ShelfBottle oldBottle, ShelfBottle newBottle)
        {
            if (oldBottle == null) throw new ArgumentNullException(nameof(oldBottle));
            if (newBottle == null) throw new ArgumentNullException(nameof(newBottle));
            int index = _bottles.IndexOf(oldBottle);
            if (index < 0) throw new ArgumentException("Bottle is not on the shelf.", nameof(oldBottle));
            if (newBottle.Id != oldBottle.Id && _byId.ContainsKey(newBottle.Id))
                throw new ArgumentException($"'{newBottle.Id}' is already on the shelf.", nameof(newBottle));

            _bottles[index] = newBottle;
            _byId.Remove(oldBottle.Id);
            _byId[newBottle.Id] = newBottle;
        }

        public void Add(ShelfBottle bottle)
        {
            if (bottle == null) throw new ArgumentNullException(nameof(bottle));
            if (_byId.ContainsKey(bottle.Id))
                throw new ArgumentException($"'{bottle.Id}' is already on the shelf.", nameof(bottle));
            _bottles.Add(bottle);
            _byId[bottle.Id] = bottle;
        }

        /// <summary>
        /// Draws from one bottle into a glass, capped so a pour can never exceed the glass —
        /// the round layer decides whether hitting the brim is a spill.
        /// Returns the volume actually poured.
        /// </summary>
        public double PourInto(GlassContents glass, string ingredientId, double requested)
        {
            if (glass == null) throw new ArgumentNullException(nameof(glass));
            var bottle = Find(ingredientId);
            if (bottle == null) throw new ArgumentException($"No '{ingredientId}' on the shelf.", nameof(ingredientId));

            double poured = bottle.Draw(requested);
            if (poured > 0) glass.Add(ingredientId, poured);
            return poured;
        }

        /// <summary>Total refill cost for everything below full, at the given price per capacity.</summary>
        public int RefillCost(int pricePerCapacity)
        {
            double missing = 0;
            foreach (var bottle in _bottles) missing += bottle.Capacity - bottle.Remaining;
            return (int)Math.Ceiling(missing * pricePerCapacity);
        }

        public void RefillAll()
        {
            foreach (var bottle in _bottles) bottle.Refill();
        }
    }
}
