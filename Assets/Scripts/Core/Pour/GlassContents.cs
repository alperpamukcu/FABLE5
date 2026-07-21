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

        /// <summary>How much this glass holds. It never takes more (GDD 21 §3).</summary>
        public double Capacity { get; }

        public GlassContents(double capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        /// <summary>Pours in order, for the layered readout.</summary>
        public IReadOnlyList<Pour> Pours => _pours;

        public double TotalVolume { get; private set; }

        /// <summary>0…1 — how full the glass is. It cannot exceed 1: the glass stops taking.</summary>
        public double FillFraction => TotalVolume / Capacity;

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
        /// Adds volume and returns how much the glass actually took. The glass never
        /// overflows (GDD 21 §3, ruling 2026-07-20): pouring past the brim simply stops at
        /// it, so a heavy hand costs precision, not the whole drink. Consecutive pours of
        /// the same ingredient merge into one layer, so releasing and re-holding the same
        /// bottle does not stripe the glass.
        /// </summary>
        public double Add(string ingredientId, double volume)
        {
            if (string.IsNullOrEmpty(ingredientId))
                throw new ArgumentException("Ingredient id is required", nameof(ingredientId));

            double accepted = Math.Min(volume, Capacity - TotalVolume);
            if (accepted <= 0) return 0;

            if (_pours.Count > 0 && _pours[_pours.Count - 1].IngredientId == ingredientId)
                _pours[_pours.Count - 1] = new Pour(ingredientId, _pours[_pours.Count - 1].Volume + accepted);
            else
                _pours.Add(new Pour(ingredientId, accepted));

            _byIngredient.TryGetValue(ingredientId, out double existing);
            _byIngredient[ingredientId] = existing + accepted;
            TotalVolume += accepted;
            return accepted;
        }

        /// <summary>
        /// Removes up to <paramref name="volume"/> total, taken proportionally across every
        /// ingredient so the drink's ratios and its layer order survive — draining half a
        /// 70/30 glass leaves a 70/30 glass at half the volume. Returns what was removed.
        /// The pour from the shaker into the serving glass is built on this (GDD 24 §3).
        /// </summary>
        public double DrainProportional(double volume)
        {
            if (volume <= 0 || TotalVolume <= 0) return 0;

            double removed = Math.Min(volume, TotalVolume);
            double keep = (TotalVolume - removed) / TotalVolume;

            // Floating-point dregs below this read as empty: a "drained" shaker must report
            // IsEmpty so the serve stage and the layered readout both clear cleanly.
            if (keep <= 1e-9)
            {
                double all = TotalVolume;
                _pours.Clear();
                _byIngredient.Clear();
                TotalVolume = 0;
                return all;
            }

            for (int i = 0; i < _pours.Count; i++)
                _pours[i] = new Pour(_pours[i].IngredientId, _pours[i].Volume * keep);
            foreach (var id in new List<string>(_byIngredient.Keys))
                _byIngredient[id] *= keep;
            TotalVolume -= removed;
            return removed;
        }

        /// <summary>
        /// Pours from this vessel into <paramref name="target"/>, keeping this vessel's
        /// ingredient ratios (GDD 24 §3). <paramref name="accuracy"/> (0…1) is the share of
        /// what leaves that actually lands in the glass; the rest missed the rim and is
        /// spilled — a sloppy pour delivers a thinner, under-filled drink. The full
        /// <paramref name="volume"/> still drains from this vessel. Returns the volume that
        /// landed in the target.
        /// </summary>
        public double TransferInto(GlassContents target, double volume, double accuracy)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            accuracy = accuracy < 0 ? 0 : accuracy > 1 ? 1 : accuracy;

            double leaving = Math.Min(volume, TotalVolume);
            if (leaving <= 0) return 0;

            // Snapshot the shares before draining, then land each ingredient's portion into
            // the target (Add caps at the target's brim, so an over-pour spills there too).
            double landed = 0;
            foreach (var pair in new List<KeyValuePair<string, double>>(_byIngredient))
            {
                double share = pair.Value / TotalVolume;
                landed += target.Add(pair.Key, accuracy * leaving * share);
            }
            DrainProportional(leaving);
            return landed;
        }

        // ── preparations (GDD 22 §5, infrastructure only) ────────────────────────

        private readonly List<PreparationDefinition> _preparations = new List<PreparationDefinition>();

        /// <summary>Preparation steps applied to this glass, in order. No effect yet by design.</summary>
        public IReadOnlyList<PreparationDefinition> PreparationSteps => _preparations;

        /// <summary>Records a preparation. The same step never applies twice — a drink is shaken or it is not.</summary>
        public void AddPreparation(PreparationDefinition preparation)
        {
            if (preparation == null) return;
            foreach (var existing in _preparations)
                if (existing.Id == preparation.Id) return;

            // Shaken and stirred are the same slot: the second one replaces the first.
            if (preparation.Id == "shaken" || preparation.Id == "stirred")
                _preparations.RemoveAll(p => p.Id == "shaken" || p.Id == "stirred");
            _preparations.Add(preparation);
        }

        public bool HasPreparation(string id)
        {
            foreach (var preparation in _preparations)
                if (preparation.Id == id) return true;
            return false;
        }

        /// <summary>Empties the glass — served or binned.</summary>
        public void Clear()
        {
            _pours.Clear();
            _byIngredient.Clear();
            _preparations.Clear();
            TotalVolume = 0;
        }

        public override string ToString() =>
            IsEmpty ? "empty glass" : $"{FillFraction:P0} full: {string.Join(", ", _pours)}";
    }
}
