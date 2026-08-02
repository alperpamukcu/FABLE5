using System;

namespace LastCall.Core
{
    /// <summary>
    /// One named person, remembered for the whole run (GDD 19 §10, trimmed 2026-08-02:
    /// the emotion layer is gone — what persists is who they are and how the bar has
    /// treated them; what they give back each visit is their reaction to the cocktail).
    /// </summary>
    public sealed class RegularState
    {
        public string Id { get; }
        public string Name { get; }
        public string ArchetypeId { get; }

        /// <summary>Their archetype's disposition, carried so the run layer need not re-look it up.</summary>
        public DemandLevel BaseDemand { get; }

        /// <summary>Printed on the licence (GDD 22 §3); dialogue will use both later.</summary>
        public int Age { get; }
        public string Hometown { get; }

        public int Visits { get; private set; }
        public int SatisfiedCount { get; private set; }
        public Relationship Relationship { get; private set; } = Relationship.Stranger;

        /// <summary>Total satisfaction this person has contributed to the week's quota.</summary>
        public int SatisfactionEarned { get; private set; }

        public RegularState(string id, string name, string archetypeId,
            DemandLevel baseDemand = DemandLevel.Easygoing,
            int age = 30, string hometown = null)
        {
            Age = age;
            Hometown = hometown ?? "this side of town";
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Regular id is required", nameof(id));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            ArchetypeId = archetypeId ?? string.Empty;
            BaseDemand = baseDemand;
        }

        /// <summary>Closes out a visit. Satisfaction is what the serve earned (GDD 19 §10).</summary>
        public void RecordVisit(int satisfaction)
        {
            Visits++;
            SatisfactionEarned += Math.Max(0, satisfaction);
            if (satisfaction > 0) SatisfiedCount++;
            Relationship = Relationships.ForSatisfiedVisits(SatisfiedCount);
        }

        public override string ToString() => $"{Name} ({Relationship}, {Visits} visits)";
    }
}
