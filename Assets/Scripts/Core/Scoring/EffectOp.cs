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
        TransformCard
    }
}
