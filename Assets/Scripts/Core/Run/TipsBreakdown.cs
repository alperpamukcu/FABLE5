namespace LastCall.Core
{
    /// <summary>
    /// Itemized payout for one satisfied customer (GDD 7.5 + 6). The UI shows these
    /// lines on the cash-out screen; Total is what lands in the wallet.
    /// </summary>
    public sealed class TipsBreakdown
    {
        public int Base { get; }
        public int UnusedMixBonus { get; }
        public int Interest { get; }
        public int VipBonus { get; }
        public int PatronBonus { get; }

        /// <summary>$ per Golden card still on the rail when the customer is satisfied (GDD 3.3).</summary>
        public int GoldenBonus { get; }

        /// <summary>Favor tag payouts consumed on this win (e.g. Investor, GDD 5.4).</summary>
        public int FavorBonus { get; }

        public int Total => Base + UnusedMixBonus + Interest + VipBonus + PatronBonus + GoldenBonus + FavorBonus;

        public TipsBreakdown(int baseTip, int unusedMixBonus, int interest, int vipBonus,
            int patronBonus, int goldenBonus = 0, int favorBonus = 0)
        {
            Base = baseTip;
            UnusedMixBonus = unusedMixBonus;
            Interest = interest;
            VipBonus = vipBonus;
            PatronBonus = patronBonus;
            GoldenBonus = goldenBonus;
            FavorBonus = favorBonus;
        }

        /// <summary>Nobody paid: the customer left before the order was filled (fork B).</summary>
        public static TipsBreakdown None { get; } = new TipsBreakdown(0, 0, 0, 0, 0);
    }
}
