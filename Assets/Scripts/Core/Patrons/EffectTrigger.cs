namespace LastCall.Core
{
    /// <summary>
    /// When a patron effect fires (GDD 13 trigger list + customer-end for payout effects).
    /// OnShopEnter / OnNightEnd are consumed by the run layer (M2 run loop).
    /// </summary>
    public enum EffectTrigger
    {
        OnCardScored,
        OnHandScored,
        /// <summary>A bottle was refilled in the Back Room (GDD 21 §6).</summary>
        OnRefill,

        OnShopEnter,

        /// <summary>A customer sits down and the ID is built — the information patrons' moment.</summary>
        OnCustomerStart,

        OnCustomerEnd,
        OnNightEnd
    }
}
