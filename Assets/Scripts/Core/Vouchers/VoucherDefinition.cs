using System;

namespace LastCall.Core
{
    /// <summary>What a Voucher permanently upgrades for the rest of the run (GDD 7.4).</summary>
    public enum VoucherOp
    {
        ExtraRestock,    // +N Restocks every customer (Happy Hour)
        ExtraMix,        // +N Mixes every customer (Double Shift)
        ExtraRail,       // +N Rail size (Wider Rail)
        PatronDiscount,  // Patrons cost $N less, min $1 (Loyal Clientele)
        RarePatronBoost, // Uncommon/Rare shop weights xN+1 (Neon Sign)
        PackExtraCard    // Cellar Packs show +N options (Deep Cellar)
    }

    /// <summary>
    /// A one-time permanent run upgrade sold in its own Back Room slot. Each voucher
    /// can be owned once; the slot never rerolls (GDD 7.4 rulings in 05_shop_economy).
    /// </summary>
    public sealed class VoucherDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Cost { get; }
        public VoucherOp Op { get; }
        public int IntValue { get; }
        public string Description { get; }

        public VoucherDefinition(string id, string name, int cost, VoucherOp op, int intValue,
            string description = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Voucher id is required", nameof(id));
            if (intValue <= 0) throw new ArgumentOutOfRangeException(nameof(intValue));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Cost = cost;
            Op = op;
            IntValue = intValue;
            Description = description ?? string.Empty;
        }
    }
}
