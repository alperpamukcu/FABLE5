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
        public int Total => Base + UnusedMixBonus + Interest + VipBonus + PatronBonus;

        public TipsBreakdown(int baseTip, int unusedMixBonus, int interest, int vipBonus, int patronBonus)
        {
            Base = baseTip;
            UnusedMixBonus = unusedMixBonus;
            Interest = interest;
            VipBonus = vipBonus;
            PatronBonus = patronBonus;
        }
    }
}
