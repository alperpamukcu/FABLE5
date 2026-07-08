namespace LastCall.Core
{
    /// <summary>Edition system (GDD 01, section 3.1). Scoring effects live in ScoringEngine.</summary>
    public enum QualityTier
    {
        HousePour,   // default: no bonus
        TopShelf,    // +30 Flavor when scored
        BarrelAged,  // +8 Mult when scored
        Signature,   // x1.5 Mult when scored
        Bootleg      // +1 Patron slot (M2), card itself scores 0 Flavor
    }
}
