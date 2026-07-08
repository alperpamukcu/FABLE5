namespace LastCall.Core
{
    /// <summary>
    /// Ingredient enhancements applied by Tools (GDD 3.3). M2 implements Infused and
    /// Overproof; Premium (wild type), Frozen (rng shatter), Doubled and Golden land
    /// with the tools that create them in M3.
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
