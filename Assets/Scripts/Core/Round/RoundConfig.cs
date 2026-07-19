using System;

namespace LastCall.Core
{
    /// <summary>
    /// Per-customer rules (GDD 00 core loop, GDD 21 pour system). All values are "modifiable"
    /// by design — Patrons, Tools and glassware upgrades hand out altered configs.
    /// </summary>
    public sealed class RoundConfig
    {
        /// <summary>How many drinks this customer will accept before leaving.</summary>
        public int DrinksPerCustomer { get; }

        /// <summary>How much the glass holds (GDD 21 §7.2); upgradeable glassware raises it.</summary>
        public double GlassCapacity { get; }

        /// <summary>How many times you can ask instead of pour (GDD 19 §8).</summary>
        public int ChatsPerCustomer { get; }

        public RoundConfig(int drinksPerCustomer = 4, double glassCapacity = 1.0,
            int chatsPerCustomer = 2)
        {
            if (drinksPerCustomer <= 0) throw new ArgumentOutOfRangeException(nameof(drinksPerCustomer));
            if (glassCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(glassCapacity));
            if (chatsPerCustomer < 0) throw new ArgumentOutOfRangeException(nameof(chatsPerCustomer));
            DrinksPerCustomer = drinksPerCustomer;
            GlassCapacity = glassCapacity;
            ChatsPerCustomer = chatsPerCustomer;
        }

        public static RoundConfig Default { get; } = new RoundConfig();

        /// <summary>A copy with one field changed — the shape upgrades and Stakes need.</summary>
        public RoundConfig With(int? drinksPerCustomer = null, double? glassCapacity = null,
            int? chatsPerCustomer = null) =>
            new RoundConfig(
                drinksPerCustomer ?? DrinksPerCustomer,
                glassCapacity ?? GlassCapacity,
                chatsPerCustomer ?? ChatsPerCustomer);
    }
}
