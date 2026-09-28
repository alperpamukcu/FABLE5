using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// ONE ACHIEVEMENT, AS DATA (2026-09-28; Resources/Data/achievements.json). It is earned when every
    /// stat it names has reached <see cref="Target"/> — one stat for almost all of them, several for a
    /// set to complete (catch every kind of forged card). <see cref="Id"/> is the Steamworks API name.
    /// </summary>
    public sealed class AchievementDefinition
    {
        /// <summary>The Steamworks API name, UPPER_SNAKE.</summary>
        public string Id { get; }
        /// <summary>The lifetime stats that must each reach <see cref="Target"/>.</summary>
        public IReadOnlyList<string> Stats { get; }
        public long Target { get; }
        /// <summary>A secret: its name and description stay dark until it is earned.</summary>
        public bool Hidden { get; }
        /// <summary>Where it sits on the pacing curve — opening, week, early, mid, late, secret. Read by the
        /// pacing report and the Steamworks sheet; nothing in play decides on it.</summary>
        public string Tier { get; }
        /// <summary>English name and description; the string tables carry the rest
        /// (<c>data.achievement.&lt;id&gt;.name/description</c>).</summary>
        public string Name { get; }
        public string Description { get; }

        public AchievementDefinition(string id, IReadOnlyList<string> stats, long target, bool hidden,
            string tier, string name, string description)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An achievement needs an id", nameof(id));
            if (stats == null || stats.Count == 0)
                throw new ArgumentException($"Achievement '{id}' names no stat", nameof(stats));
            foreach (var s in stats)
                if (!LastCall.Core.Stats.IsKnown(s))
                    throw new ArgumentException($"Achievement '{id}' names '{s}', which is not a stat the run reports", nameof(stats));
            if (target <= 0) throw new ArgumentException($"Achievement '{id}' needs a target above zero", nameof(target));
            Id = id;
            Stats = stats;
            Target = target;
            Hidden = hidden;
            Tier = tier ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Description = description ?? string.Empty;
        }

        /// <summary>A single summed stat with a target above one draws a progress bar (in the game's list
        /// and, linked in Steamworks, on Steam).</summary>
        public bool ShowsProgress => Stats.Count > 1 || (Target > 1 && LastCall.Core.Stats.KindOf(Stats[0]) == StatKind.Sum);

        public override string ToString() => Id;
    }

    /// <summary>What one bump earned: the achievements it unlocked, and the progress marks it crossed on
    /// the ones still to come.</summary>
    public sealed class AchievementNews
    {
        public static readonly AchievementNews None = new AchievementNews(Array.Empty<AchievementDefinition>(),
            Array.Empty<AchievementProgress>());

        public IReadOnlyList<AchievementDefinition> Unlocked { get; }
        public IReadOnlyList<AchievementProgress> Marks { get; }
        public bool IsEmpty => Unlocked.Count == 0 && Marks.Count == 0;

        public AchievementNews(IReadOnlyList<AchievementDefinition> unlocked, IReadOnlyList<AchievementProgress> marks)
        {
            Unlocked = unlocked;
            Marks = marks;
        }
    }

    /// <summary>How far along one achievement is.</summary>
    public readonly struct AchievementProgress
    {
        public readonly AchievementDefinition Achievement;
        public readonly long Have;
        public readonly long Need;

        public AchievementProgress(AchievementDefinition achievement, long have, long need)
        {
            Achievement = achievement;
            Have = have;
            Need = need;
        }

        public double Fraction => Need <= 0 ? 1.0 : Math.Min(1.0, (double)Have / Need);
    }
}
