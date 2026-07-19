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
        OnRestock,
        OnShopEnter,

        /// <summary>A customer sits down and the ID is built — the information patrons' moment.</summary>
        OnCustomerStart,

        OnCustomerEnd,
        OnNightEnd
    }
}
