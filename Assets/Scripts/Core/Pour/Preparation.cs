using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// A preparation step applied to the glass besides pouring (GDD 22 §5): shaking,
    /// stirring, ice, a salted or sugared rim, a squeeze of lemon. **Infrastructure only for
    /// now** — preparations are recorded on the glass and rendered, but have no scoring or
    /// emotional effect until their design pass. Building the plumbing first means that pass
    /// is a data-and-balance change, not a systems change.
    /// </summary>
    public sealed class PreparationDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        public PreparationDefinition(string id, string name, string description = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Preparation id is required", nameof(id));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Description = description ?? string.Empty;
        }

        public override string ToString() => Name;
    }

    /// <summary>The built-in preparation set. Data-driven definitions can extend this later.</summary>
    public static class Preparations
    {
        public static readonly PreparationDefinition Shaken =
            new PreparationDefinition("shaken", "Shaken", "Mixed hard in the shaker.");
        public static readonly PreparationDefinition Stirred =
            new PreparationDefinition("stirred", "Stirred", "Turned gently over a bar spoon.");
        public static readonly PreparationDefinition Ice =
            new PreparationDefinition("ice", "On Ice", "Served over cubes.");
        public static readonly PreparationDefinition LemonTwist =
            new PreparationDefinition("lemon_twist", "Lemon Twist", "A curl of peel over the top.");
        public static readonly PreparationDefinition SaltRim =
            new PreparationDefinition("salt_rim", "Salt Rim", "The rim run through salt.");
        public static readonly PreparationDefinition SugarRim =
            new PreparationDefinition("sugar_rim", "Sugar Rim", "The rim run through sugar.");

        public static readonly IReadOnlyList<PreparationDefinition> All = new[]
        {
            Shaken, Stirred, Ice, LemonTwist, SaltRim, SugarRim
        };

        public static PreparationDefinition Find(string id)
        {
            foreach (var preparation in All)
                if (preparation.Id == id) return preparation;
            return null;
        }
    }
}
