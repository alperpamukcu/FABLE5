namespace LastCall.Core
{
    /// <summary>
    /// Ingredient enhancements applied by Tools (GDD 3.3, rulings in 01_ingredients).
    /// Premium resolves in the matcher, Infused/Overproof/Frozen in the scoring engine,
    /// the Frozen shatter roll and Doubled copy in the round, Golden in the run payout.
    /// </summary>
    public enum Enhancement
    {
        None,
        Infused,    // +40 Flavor when scored
        Overproof,  // +4 Mult when scored
        Premium,    // counts as any one Type (wild)
        Frozen,     // x2 Mult but 1-in-4 chance to shatter after scoring
        Doubled,    // permanent copy stays in deck
        Golden      // $3 if held at end of round
    }
}
