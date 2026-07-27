namespace LastCall.Core
{
    /// <summary>
    /// The ingredient "suits" (GDD 01 §3) — the original six, plus Beer (GDD 21 §10).
    /// Beer is appended rather than slotted in with the others: it is not a cocktail
    /// ingredient. Nothing mixes with it, it comes from a keg instead of a bottle, and it
    /// reaches the glass without passing through the shaker.
    /// </summary>
    public enum IngredientType
    {
        Spirit,
        Sour,
        Sweet,
        Bitter,
        Bubbly,
        Garnish,
        Beer
    }
}
