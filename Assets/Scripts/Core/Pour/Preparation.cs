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

        /// <summary><see cref="Name"/> as a string-table line (localization L1):
        /// <c>prep.&lt;id&gt;.name</c>. Only the built-in set in <see cref="Preparations"/> has one.</summary>
        public Line NameLine => Line.Of("prep." + Id + ".name");

        /// <summary><see cref="Description"/> as a string-table line: <c>prep.&lt;id&gt;.description</c>.</summary>
        public Line DescriptionLine => Line.Of("prep." + Id + ".description");

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
        /// <summary>THE JARS ARE EXTRAS (2026-09-21, the author: "kokteyl tariflerinde direkt olarak mint veya
        /// zeytin olmamalı, ekstra olarak istenmeli"): olives and mint are dropped on the glass like ice, asked
        /// for by the customer, never poured into the mix and never a recipe's band.</summary>
        public static readonly PreparationDefinition Olive =
            new PreparationDefinition("olive", "Olives", "A spear of olives in the glass.");
        public static readonly PreparationDefinition Mint =
            new PreparationDefinition("mint", "Mint Sprig", "A sprig of mint over the top.");
        /// <summary>Pulled from a keg (GDD 21 §10). Not a step the player chooses — it is how
        /// the drink got into the glass, and it is what tells the judge to grade the head.</summary>
        public static readonly PreparationDefinition Draught =
            new PreparationDefinition("draught", "Draught", "Pulled from the tap.");

        public static readonly IReadOnlyList<PreparationDefinition> All = new[]
        {
            Shaken, Stirred, Ice, LemonTwist, SaltRim, SugarRim, Draught, Olive, Mint
        };

        public static PreparationDefinition Find(string id)
        {
            foreach (var preparation in All)
                if (preparation.Id == id) return preparation;
            return null;
        }
    }
}
