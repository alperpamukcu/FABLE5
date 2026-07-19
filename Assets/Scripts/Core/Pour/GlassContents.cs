using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>One ingredient's contribution to the glass, in pour order.</summary>
    public readonly struct Pour
    {
        public string IngredientId { get; }
        public double Volume { get; }

        public Pour(string ingredientId, double volume)
        {
            IngredientId = ingredientId;
            Volume = volume;
        }

        public override string ToString() => $"{IngredientId} {Volume:0.##}";
    }

    /// <summary>
    /// What is currently in the glass (GDD 21 §3). Holds pours in the order they were made,
    /// because that order is what the layered readout draws — the glass is the primary
    /// feedback channel and it has to show the drink being built, not a summary of it.
    ///
    /// Volume is unitless: capacity is 1 full glass and everything is measured against it,
    /// so charges scale cleanly (GDD 21 §4) and glassware upgrades are just a bigger capacity.
    /// </summary>
    public sealed class GlassContents
    {
        private readonly List<Pour> _pours = new List<Pour>();
        private readonly Dictionary<string, double> _byIngredient = new Dictionary<string, double>();

        /// <summary>How much this glass holds before it spills.</summary>
        public double Capacity { get; }

        public GlassContents(double capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        /// <summary>Pours in order, for the layered readout.</summary>
        public IReadOnlyList<Pour> Pours => _pours;

        public double TotalVolume { get; private set; }

        /// <summary>0…1+ — how full the glass is. Above 1 means it has spilled.</summary>
        public double FillFraction => TotalVolume / Capacity;

        /// <summary>
        /// Poured past the brim (GDD 21 §3). Exactly at capacity is a perfectly full glass,
        /// not a spill — the boundary belongs to the player.
        /// </summary>
        public bool IsOverflowing => TotalVolume > Capacity;

        public bool IsEmpty => _pours.Count == 0;

        /// <summary>Distinct ingredients in the glass.</summary>
        public IReadOnlyCollection<string> Ingredients => _byIngredient.Keys;

        /// <summary>Total volume of one ingredient, however many separate pours it took.</summary>
        public double VolumeOf(string ingredientId) =>
            ingredientId != null && _byIngredient.TryGetValue(ingredientId, out double v) ? v : 0;

        /// <summary>
        /// This ingredient's share of the drink, 0…1. Ratios are what the player is really
        /// choosing (GDD 21 §4) — 70% vodka is 70% vodka whether the glass is full or half.
        /// </summary>
        public double RatioOf(string ingredientId) =>
            TotalVolume <= 0 ? 0 : VolumeOf(ingredientId) / TotalVolume;

        /// <summary>
        /// Adds volume. Consecutive pours of the same ingredient merge into one layer, so
        /// releasing and re-holding the same bottle does not stripe the glass.
        /// </summary>
        public void Add(string ingredientId, double volume)
        {
            if (string.IsNullOrEmpty(ingredientId))
                throw new ArgumentException("Ingredient id is required", nameof(ingredientId));
            if (volume <= 0) return;

            if (_pours.Count > 0 && _pours[_pours.Count - 1].IngredientId == ingredientId)
                _pours[_pours.Count - 1] = new Pour(ingredientId, _pours[_pours.Count - 1].Volume + volume);
            else
                _pours.Add(new Pour(ingredientId, volume));

            _byIngredient.TryGetValue(ingredientId, out double existing);
            _byIngredient[ingredientId] = existing + volume;
            TotalVolume += volume;
        }

        /// <summary>Empties the glass — served, spilled or binned.</summary>
        public void Clear()
        {
            _pours.Clear();
            _byIngredient.Clear();
            TotalVolume = 0;
        }

        public override string ToString() =>
            IsEmpty ? "empty glass" : $"{FillFraction:P0} full: {string.Join(", ", _pours)}";
    }
}
