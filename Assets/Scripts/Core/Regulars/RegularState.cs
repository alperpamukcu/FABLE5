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

        /// <summary>Printed on the licence (GDD 22 §3); dialogue will use both later.</summary>
        public int Age { get; }
        public string Hometown { get; }

        public int Visits { get; private set; }
        public int SatisfiedCount { get; private set; }
        public Relationship Relationship { get; private set; } = Relationship.Stranger;

        /// <summary>Total satisfaction this person has contributed to the week's quota.</summary>
        public int SatisfactionEarned { get; private set; }

        /// <summary>
        /// The truth behind their card (GDD 28, 2026-09-04). CORE-ONLY, like the order's
        /// <c>OrderTruth</c>: the one public door is <c>CustomerVisit.Papers</c>, which throws
        /// until the card has been read. Null means the run has not asked yet — it asks ONCE,
        /// at first arrival, on the <c>"papers"</c> stream, and an honest adult is a real
        /// answer, not a null one. A minor is a person, not a visit: whoever comes back comes
        /// back with the same card.
        /// </summary>
        internal IdPapers Papers { get; private set; }

        /// <summary>Whether the run has asked the question yet.</summary>
        internal bool PapersRolled => Papers != null;

        /// <summary>
        /// The one fact about the papers the room may read before the card is: could they
        /// pass for nineteen? Every minor could, and so could a share of honest adults, so the
        /// face is a reason to look and never the verdict. False until the papers are rolled.
        /// </summary>
        public bool LooksYoung => Papers != null && Papers.LooksYoung;

        /// <summary>Shown the door for cause (GDD 28 §4). A bounced minor does not try the same
        /// bar again — the registry's return roll passes over them — so the state's thanks
        /// is paid once per face and the dice cannot farm it.</summary>
        public bool Barred { get; private set; }

        internal void SetPapers(IdPapers papers)
        {
            if (papers == null) throw new ArgumentNullException(nameof(papers));
            if (PapersRolled) throw new InvalidOperationException($"{Name} already has their papers.");
            Papers = papers;
        }

        internal void Bar() => Barred = true;

        public RegularState(string id, string name, string archetypeId,
            int age = 30, string hometown = null)
        {
            Age = age;
            Hometown = hometown ?? "this side of town";
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Regular id is required", nameof(id));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            ArchetypeId = archetypeId ?? string.Empty;
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
