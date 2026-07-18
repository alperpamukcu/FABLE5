namespace LastCall.Core
{
    /// <summary>
    /// How well the bartender knows someone (GDD 19 §10). Earned by satisfying them across
    /// repeat visits; each rank tightens the Range bands you can read on them. Rank order
    /// is used arithmetically (<c>(int)relationship</c>) — do not reorder.
    /// </summary>
    public enum Relationship
    {
        Stranger = 0,
        Familiar = 1,
        Regular = 2,
        Confidant = 3
    }

    public static class Relationships
    {
        /// <summary>Satisfied visits needed to reach each rank.</summary>
        public static Relationship ForSatisfiedVisits(int satisfiedCount) =>
            satisfiedCount >= 6 ? Relationship.Confidant
            : satisfiedCount >= 3 ? Relationship.Regular
            : satisfiedCount >= 1 ? Relationship.Familiar
            : Relationship.Stranger;
    }
}
