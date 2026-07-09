using System;

namespace LastCall.Core
{
    /// <summary>
    /// Stakes 1–4 (GDD 5.3): unlockable difficulty modifiers that stack cumulatively —
    /// playing Stake N applies every modifier from Stake 2 up to N. Stakes 5–8 are on
    /// the GDD cut-first list and land after M3.
    /// </summary>
    public static class StakeTable
    {
        public const int Min = 1;
        public const int Max = 4;

        public static string NameOf(int stake)
        {
            switch (Clamp(stake))
            {
                case 1: return "Green";
                case 2: return "Amber";
                case 3: return "Copper";
                default: return "Silver";
            }
        }

        /// <summary>Returns a config with every modifier up to <paramref name="stake"/> applied.</summary>
        public static RunConfig Apply(RunConfig config, int stake)
        {
            stake = Clamp(stake);
            if (stake <= 1) return config;

            // Copper: targets +25% on Nights 1–2.
            var target = config.TargetProvider;
            if (stake >= 3)
            {
                var baseTarget = target;
                target = (night, slot) => baseTarget(night, slot) * (night <= 2 ? 1.25 : 1.0);
            }

            // Silver: −1 Restock.
            var round = config.RoundConfig;
            if (stake >= 4)
            {
                round = new RoundConfig(round.RailSize, round.MaxMixSelection,
                    round.MixesPerCustomer, Math.Max(0, round.RestocksPerCustomer - 1));
            }

            return new RunConfig(config.Nights, config.StartingMoney,
                config.TipCustomerA, config.TipCustomerB, config.TipVip,
                stake >= 2 ? 0 : config.VipDefeatBonus, // Amber: no VIP tip bonus
                config.InterestPerDollars, config.InterestCap,
                config.ShopSlots, config.MaxPatronSlots, config.MaxToolSlots,
                config.RerollBaseCost, config.BookPrice, config.GoldenCardBonus,
                config.MoneyDoubleCap, target, round);
        }

        private static int Clamp(int stake) => stake < Min ? Min : (stake > Max ? Max : stake);
    }
}
