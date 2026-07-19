namespace LastCall.Core
{
    /// <summary>
    /// Atomic scoring operations (GDD 13). M1 uses the first three (recipe base, card
    /// flavor, quality tiers); the rest are reserved for Patron/Tool effects in M2+.
    /// </summary>
    public enum EffectOp
    {
        AddFlavor,
        AddMult,
        MultMult,
        AddMoney,
        Retrigger,
        CreateCard,
        DestroyCard,
        TransformCard,
        /// <summary>Engine-internal: bump a patron's run-scoped counter (scaling patrons).</summary>
        Accumulate,

        // Information effects (GDD 19 §8). Resolved by the run layer when a customer sits
        // down; they change what the ID says, never what the mix scores.

        /// <summary>Tighten Value readings, darkest first (Gossip, Confidant).</summary>
        NarrowReading,

        /// <summary>Tighten the reading for the stat they actually came in about (Empath).</summary>
        NarrowIntentReading
    }
}
