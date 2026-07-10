namespace LastCall.Core
{
    /// <summary>
    /// Regular's Favor tags (GDD 5.4 v1.1): skipping Customer A grants ONE random tag.
    /// Tags stack in a queue (max 4 held, no duplicates) and are consumed automatically
    /// when their condition occurs. Word of Mouth resolves immediately and is never held.
    /// </summary>
    public enum FavorTag
    {
        LoyalTab,       // next shop: one Patron slot is free
        OnTheHouse,     // next shop: booster packs cost $0
        DoubleTip,      // next satisfied customer pays double base tip
        Investor,       // gain $15 after beating the next VIP
        TopShelfCellar, // next Cellar pack: all cards Top Shelf
        SpeakeasyKey,   // next shop guaranteed to stock a Speakeasy Pack
        WordOfMouth,    // immediately gain a random Common Patron
        QuickHands      // +1 Mix for the next customer only
    }
}
