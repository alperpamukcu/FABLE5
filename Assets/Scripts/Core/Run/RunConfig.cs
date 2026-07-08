using System;

namespace LastCall.Core
{
    /// <summary>
    /// Run-wide rules (GDD 5.1 + 7.5). The target provider is injectable so tests and,
    /// later, Stakes/Endless can rescale the curve without touching the controller.
    /// </summary>
    public sealed class RunConfig
    {
        public int Nights { get; }
        public int StartingMoney { get; }
        public int TipCustomerA { get; }
        public int TipCustomerB { get; }
        public int TipVip { get; }
        public int VipDefeatBonus { get; }
        public int InterestPerDollars { get; }
        public int InterestCap { get; }
        public int ShopSlots { get; }
        public int MaxPatronSlots { get; }
        public int MaxToolSlots { get; }
        public int RerollBaseCost { get; }
        public int BookPrice { get; }
        public Func<int, CustomerSlot, double> TargetProvider { get; }
        public RoundConfig RoundConfig { get; }

        public RunConfig(int nights = 8, int startingMoney = 4,
            int tipCustomerA = 3, int tipCustomerB = 4, int tipVip = 5, int vipDefeatBonus = 5,
            int interestPerDollars = 5, int interestCap = 5,
            int shopSlots = 2, int maxPatronSlots = 5, int maxToolSlots = 2,
            int rerollBaseCost = 5, int bookPrice = 4,
            Func<int, CustomerSlot, double> targetProvider = null,
            RoundConfig roundConfig = null)
        {
            if (nights <= 0) throw new ArgumentOutOfRangeException(nameof(nights));
            Nights = nights;
            StartingMoney = startingMoney;
            TipCustomerA = tipCustomerA;
            TipCustomerB = tipCustomerB;
            TipVip = tipVip;
            VipDefeatBonus = vipDefeatBonus;
            InterestPerDollars = interestPerDollars;
            InterestCap = interestCap;
            ShopSlots = shopSlots;
            MaxPatronSlots = maxPatronSlots;
            MaxToolSlots = maxToolSlots;
            RerollBaseCost = rerollBaseCost;
            BookPrice = bookPrice;
            TargetProvider = targetProvider ?? TargetTable.GreenStake;
            RoundConfig = roundConfig ?? RoundConfig.Default;
        }

        public static RunConfig Default { get; } = new RunConfig();
    }
}
