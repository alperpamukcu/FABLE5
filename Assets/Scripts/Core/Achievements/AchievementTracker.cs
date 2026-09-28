using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// THE LIFETIME LEDGER (2026-09-28). Holds every stat the player has built up across all their bars
    /// and which achievements are earned, and rules on each <see cref="StatBump"/> the run reports: a Sum
    /// stat grows by it, a Best stat rises to it, and any achievement whose stats have all reached its
    /// target is earned. Pure — the Game layer keeps it on disk and tells Steam; nothing here knows either.
    ///
    /// THE PROGRESS MARKS ("oyuncuya ilerleme hissi verilsin"): between unlocks, a long count says how far
    /// along it is at the quarters (targets of a hundred or more) or the half (ten or more), and a set says
    /// so each time one more of its stats is in. Once per mark, as the count crosses it.
    /// </summary>
    public sealed class AchievementTracker
    {
        private readonly IReadOnlyList<AchievementDefinition> _book;
        private readonly Dictionary<string, long> _stats = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly HashSet<string> _unlocked = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<AchievementDefinition>> _byStat =
            new Dictionary<string, List<AchievementDefinition>>(StringComparer.Ordinal);

        public AchievementTracker(IReadOnlyList<AchievementDefinition> book,
            IEnumerable<KeyValuePair<string, long>> stats = null, IEnumerable<string> unlocked = null)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in book)
            {
                if (!ids.Add(a.Id)) throw new ArgumentException($"Achievement '{a.Id}' is listed twice", nameof(book));
                foreach (var s in a.Stats)
                {
                    if (!_byStat.TryGetValue(s, out var list)) _byStat[s] = list = new List<AchievementDefinition>();
                    list.Add(a);
                }
            }
            // What the save remembers. A stat the game no longer reports is dropped; an achievement that has
            // left the book stays earned in the record (Steam never takes one back either).
            if (stats != null)
                foreach (var kv in stats)
                    if (Stats.IsKnown(kv.Key) && kv.Value > 0) _stats[kv.Key] = kv.Value;
            if (unlocked != null)
                foreach (var id in unlocked)
                    if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);
        }

        public IReadOnlyList<AchievementDefinition> Book => _book;

        public long Stat(string stat) => stat != null && _stats.TryGetValue(stat, out var v) ? v : 0;

        /// <summary>Every stat with a value, for the save.</summary>
        public IEnumerable<KeyValuePair<string, long>> AllStats => _stats;

        /// <summary>Every earned id the record holds, for the save.</summary>
        public IReadOnlyCollection<string> UnlockedIds => _unlocked;

        public bool IsUnlocked(string id) => id != null && _unlocked.Contains(id);

        /// <summary>How many of the book's achievements are earned.</summary>
        public int UnlockedCount
        {
            get
            {
                int n = 0;
                foreach (var a in _book) if (_unlocked.Contains(a.Id)) n++;
                return n;
            }
        }

        public AchievementDefinition Find(string id)
        {
            foreach (var a in _book) if (a.Id == id) return a;
            return null;
        }

        /// <summary>How far along an achievement is: the count against its target for one stat, the stats
        /// already in against how many there are for a set.</summary>
        public AchievementProgress ProgressOf(AchievementDefinition a) => ProgressWith(a, null, 0);

        private AchievementProgress ProgressWith(AchievementDefinition a, string stat, long value)
        {
            long Read(string s) => s == stat ? value : Stat(s);
            if (a.Stats.Count == 1)
                return new AchievementProgress(a, Math.Min(Read(a.Stats[0]), a.Target), a.Target);
            long have = 0;
            foreach (var s in a.Stats) if (Read(s) >= a.Target) have++;
            return new AchievementProgress(a, have, a.Stats.Count);
        }

        /// <summary>What the store remembers and this machine does not (another computer, a lost file): raises
        /// <paramref name="stat"/> to at least <paramref name="value"/>, whatever its kind, and announces nothing.
        /// Follow with <see cref="Reconcile"/> to earn what it now covers.</summary>
        public void Absorb(string stat, long value)
        {
            if (!Stats.IsKnown(stat) || value <= Stat(stat)) return;
            _stats[stat] = value;
        }

        /// <summary>An achievement the store already holds as earned: recorded here too, quietly.</summary>
        public bool Adopt(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return _unlocked.Add(id);
        }

        /// <summary>Earns anything the stats already cover but the record does not hold — an achievement
        /// added to the book after its stats were built up. Called once when the ledger is loaded.</summary>
        public AchievementNews Reconcile()
        {
            List<AchievementDefinition> earned = null;
            foreach (var a in _book)
            {
                if (_unlocked.Contains(a.Id) || ProgressOf(a).Have < ProgressOf(a).Need) continue;
                _unlocked.Add(a.Id);
                (earned ?? (earned = new List<AchievementDefinition>())).Add(a);
            }
            return earned == null ? AchievementNews.None
                : new AchievementNews(earned, Array.Empty<AchievementProgress>());
        }

        public AchievementNews Apply(IEnumerable<StatBump> bumps)
        {
            List<AchievementDefinition> earned = null;
            List<AchievementProgress> marks = null;
            foreach (var b in bumps)
            {
                var news = Apply(b);
                if (news.IsEmpty) continue;
                if (news.Unlocked.Count > 0) (earned ?? (earned = new List<AchievementDefinition>())).AddRange(news.Unlocked);
                if (news.Marks.Count > 0) (marks ?? (marks = new List<AchievementProgress>())).AddRange(news.Marks);
            }
            if (earned == null && marks == null) return AchievementNews.None;
            // A mark on something earned in the same breath says nothing the unlock does not.
            if (marks != null && earned != null) marks.RemoveAll(m => earned.Contains(m.Achievement));
            return new AchievementNews((IReadOnlyList<AchievementDefinition>)earned ?? Array.Empty<AchievementDefinition>(),
                (IReadOnlyList<AchievementProgress>)marks ?? Array.Empty<AchievementProgress>());
        }

        public AchievementNews Apply(StatBump bump)
        {
            if (!Stats.IsKnown(bump.Stat) || bump.Value <= 0) return AchievementNews.None;
            long was = Stat(bump.Stat);
            long now = Stats.KindOf(bump.Stat) == StatKind.Sum ? SafeAdd(was, bump.Value) : Math.Max(was, bump.Value);
            if (now == was) return AchievementNews.None;
            if (!_byStat.TryGetValue(bump.Stat, out var watching))
            {
                _stats[bump.Stat] = now;
                return AchievementNews.None;
            }

            List<AchievementDefinition> earned = null;
            List<AchievementProgress> marks = null;
            foreach (var a in watching)
            {
                if (_unlocked.Contains(a.Id)) continue;
                var before = ProgressWith(a, bump.Stat, was);
                var after = ProgressWith(a, bump.Stat, now);
                if (after.Have >= after.Need)
                {
                    (earned ?? (earned = new List<AchievementDefinition>())).Add(a);
                    continue;
                }
                if (a.ShowsProgress && CrossesMark(a, before, after))
                    (marks ?? (marks = new List<AchievementProgress>())).Add(after);
            }
            _stats[bump.Stat] = now;
            if (earned != null) foreach (var a in earned) _unlocked.Add(a.Id);
            if (earned == null && marks == null) return AchievementNews.None;
            return new AchievementNews((IReadOnlyList<AchievementDefinition>)earned ?? Array.Empty<AchievementDefinition>(),
                (IReadOnlyList<AchievementProgress>)marks ?? Array.Empty<AchievementProgress>());
        }

        /// <summary>The marks a count is announced at: every stat of a set; the quarters of a hundred or
        /// more; the half of ten or more; nothing smaller.</summary>
        private static bool CrossesMark(AchievementDefinition a, AchievementProgress before, AchievementProgress after)
        {
            if (a.Stats.Count > 1) return after.Have > before.Have;
            double[] marks = a.Target >= 100 ? Quarters : a.Target >= 10 ? Half : NoMarks;
            foreach (var m in marks)
                if (before.Fraction < m && after.Fraction >= m) return true;
            return false;
        }

        private static readonly double[] Quarters = { 0.25, 0.5, 0.75 };
        private static readonly double[] Half = { 0.5 };
        private static readonly double[] NoMarks = Array.Empty<double>();

        private static long SafeAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
    }
}
