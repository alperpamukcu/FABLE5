namespace LastCall.Core
{
    /// <summary>
    /// The reward for skipping Customer A (GDD 5.2): tempo instead of money.
    /// Rolled from the run's "favor" stream; rewards that can't apply (full bar,
    /// full tool belt, empty pools) fall through to cash.
    /// </summary>
    public enum RegularsFavorKind
    {
        FreePatron, // a random (non-legendary) patron joins the bar for free
        FreeTool,   // a random tool from the pool joins the inventory
        DoubledTip, // the next satisfied customer's base tip is doubled
        Cash        // the regular covers part of the till
    }
}
