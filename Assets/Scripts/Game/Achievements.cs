using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LastCall.Core;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// THE ACHIEVEMENTS, KEPT (2026-09-28, the author: "Steam başarımı vs. bu entegrasyonu oyuna iyi bir
    /// şekilde yap ... çok zor başarımlar da olsun ama sık başarım kazanılsın"). The lifetime ledger
    /// (<see cref="AchievementTracker"/>) lives here, on disk beside the save, across every bar the player
    /// ever runs; the run it is attached to reports what it does (<see cref="TycoonRun.Feats"/>) and
    /// <see cref="Step"/> rules on it once a frame.
    ///
    /// The game's own record is the truth, and the store is told: every unlock and stat goes to
    /// <see cref="StorePlatform.Current"/> when there is one, and the first time a store comes up in a
    /// session the two are merged both ways — what Steam remembers from another computer is taken in
    /// quietly, what was earned off Steam is handed over. So nothing earned is lost for being earned
    /// offline, in the editor, or before the game had an App ID.
    ///
    /// A session that keeps no save keeps no achievements (<see cref="SaveStore.Enabled"/>): the suites pin
    /// that once, and their runs then record nothing and show nothing.
    /// </summary>
    public static class Achievements
    {
        private static IReadOnlyList<AchievementDefinition> s_book;
        private static AchievementTracker s_tracker;
        private static readonly Dictionary<string, string> s_earnedAt = new Dictionary<string, string>(StringComparer.Ordinal);
        private static TycoonRun s_run;
        private static bool s_disabled, s_dirty, s_storeSynced;
        private static float s_lastWrite;

        /// <summary>How often a ledger with news on it is written and sent, when nothing was unlocked.</summary>
        private const float WriteEvery = 30f;

        /// <summary>
        /// THE LEADERBOARDS (2026-09-28, the Steamworks feature list's "Sıralama Listeleri"): the personal bests worth
        /// holding up against friends', each kept at its best on Steam. Board API name → the lifetime stat it carries;
        /// Steam creates a board the first time a score is sent to it (its community name is set on Steamworks).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Leaderboards = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "LONGEST_RUN", Stats.LongestRun },         // nights one bar stayed open
            { "BIGGEST_TILL", Stats.BestTill },          // dollars in the till at a night's close
            { "BEST_NIGHT_TIPS", Stats.BestNightTips },  // dollars of tips in one night
        };

        /// <summary>An achievement was earned just now.</summary>
        public static event Action<AchievementDefinition> Unlocked;
        /// <summary>A count crossed one of its progress marks (a quarter, a half, one more of a set).</summary>
        public static event Action<AchievementProgress> Progressed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSession()
        {
            s_book = null;
            s_tracker = null;
            s_earnedAt.Clear();
            s_run = null;
            s_disabled = s_dirty = s_storeSynced = false;
            s_lastWrite = 0f;
            Unlocked = null;
            Progressed = null;
        }

        /// <summary>This session neither reads, writes nor tells the store anything; its runs record nothing.</summary>
        public static void DisableForSession()
        {
            s_disabled = true;
            s_tracker = null;
            s_earnedAt.Clear();
        }

        /// <summary>This session keeps the player's achievements.</summary>
        public static bool Keeps => !s_disabled && SaveStore.Enabled;

        public static IReadOnlyList<AchievementDefinition> Book => Tracker.Book;

        public static AchievementTracker Tracker
        {
            get
            {
                if (s_tracker == null) Load();
                return s_tracker;
            }
        }

        public static AchievementProgress ProgressOf(AchievementDefinition a) => Tracker.ProgressOf(a);

        public static bool IsUnlocked(string id) => Tracker.IsUnlocked(id);

        /// <summary>When an achievement was earned (UTC, ISO 8601), or null — also null for one the store
        /// handed over without a date.</summary>
        public static string EarnedAt(string id) =>
            id != null && s_earnedAt.TryGetValue(id, out var at) && !string.IsNullOrEmpty(at) ? at : null;

        /// <summary>Listens to <paramref name="run"/> from now on (the bootstrap, at every run start).</summary>
        public static void Attach(TycoonRun run)
        {
            s_run = run;
            if (run != null) run.Feats.Recording = Keeps;
        }

        /// <summary>Once a frame: merge with a store that has just come up, rule on whatever the run has
        /// reported, and write the ledger when it has news on it.</summary>
        public static void Step()
        {
            if (!Keeps) return;
            var ledger = Tracker;
            SyncWithStore();
            if (s_run != null && s_run.Feats.Pending)
            {
                var bumps = s_run.Feats.Take();
                var news = ledger.Apply(bumps);
                s_dirty = true;
                var store = StorePlatform.Current;
                if (store != null)
                    foreach (var b in bumps)
                    {
                        store.SetStat(b.Stat, ledger.Stat(b.Stat));
                        foreach (var board in Leaderboards)
                            if (board.Value == b.Stat) store.SubmitScore(board.Key, ledger.Stat(b.Stat));
                    }
                if (news.Unlocked.Count > 0)
                {
                    string now = Now();
                    foreach (var a in news.Unlocked)
                    {
                        s_earnedAt[a.Id] = now;
                        store?.Unlock(a.Id);
                    }
                    Write();
                    store?.Commit();
                }
                if (store != null)
                    foreach (var m in news.Marks) store.ShowProgress(m.Achievement.Id, m.Have, m.Need);
                foreach (var a in news.Unlocked) Unlocked?.Invoke(a);
                foreach (var m in news.Marks) Progressed?.Invoke(m);
            }
            if (s_dirty && Time.unscaledTime - s_lastWrite > WriteEvery)
            {
                Write();
                StorePlatform.Current?.Commit();
            }
        }

        /// <summary>Writes the ledger and sends the store what it has not heard (the bootstrap, on quit).</summary>
        public static void Flush()
        {
            if (!Keeps || s_tracker == null) return;
            if (s_dirty) Write();
            StorePlatform.Current?.Commit();
        }

        // ── the store ────────────────────────────────────────────────────────────────────────────────

        /// <summary>The first time a store is up this session: take in what it remembers (the larger of each
        /// stat, every achievement it holds), earn what that covers, and hand it everything this machine
        /// has. Quiet — nothing merged here is announced as new.</summary>
        private static void SyncWithStore()
        {
            var store = StorePlatform.Current;
            if (s_storeSynced || store == null) return;
            s_storeSynced = true;
            var ledger = Tracker;
            foreach (var stat in Stats.All)
                if (store.TryGetStat(stat, out long theirs)) ledger.Absorb(stat, theirs);
            foreach (var a in ledger.Book)
                if (store.IsUnlocked(a.Id) && ledger.Adopt(a.Id) && !s_earnedAt.ContainsKey(a.Id))
                    s_earnedAt[a.Id] = string.Empty;
            string now = Now();
            foreach (var a in ledger.Reconcile().Unlocked) s_earnedAt[a.Id] = now;
            foreach (var kv in ledger.AllStats) store.SetStat(kv.Key, kv.Value);
            foreach (var board in Leaderboards)
                if (ledger.Stat(board.Value) > 0) store.SubmitScore(board.Key, ledger.Stat(board.Value));
            foreach (var a in ledger.Book)
                if (ledger.IsUnlocked(a.Id)) store.Unlock(a.Id);
            store.Commit();
            s_dirty = true;
        }

        // ── the file ─────────────────────────────────────────────────────────────────────────────────

        private static string FilePath => Path.Combine(Application.persistentDataPath, "achievements.json");
        private static string FreshPath => Path.Combine(Application.persistentDataPath, "achievements.new.json");
        private static string PrevPath => Path.Combine(Application.persistentDataPath, "achievements.prev.json");
        private static string SpoiltPath => Path.Combine(Application.persistentDataPath, "achievements.unreadable.json");

        private static void Load()
        {
            if (s_book == null)
            {
                var asset = Resources.Load<TextAsset>("Data/achievements");
                s_book = asset != null ? DataLoader.ParseAchievements(asset.text) : Array.Empty<AchievementDefinition>();
            }
            s_earnedAt.Clear();
            List<KeyValuePair<string, long>> stats = null;
            List<string> unlocked = null;
            if (Keeps && File.Exists(FilePath))
            {
                try
                {
                    var file = JsonUtility.FromJson<LedgerFile>(File.ReadAllText(FilePath));
                    if (file == null || file.format != LedgerFile.Format) throw new FormatException("not a ledger this game reads");
                    stats = new List<KeyValuePair<string, long>>();
                    if (file.stats != null)
                        foreach (var s in file.stats) stats.Add(new KeyValuePair<string, long>(s.stat, s.value));
                    unlocked = new List<string>();
                    if (file.unlocked != null)
                        foreach (var u in file.unlocked)
                        {
                            unlocked.Add(u.id);
                            if (!string.IsNullOrEmpty(u.id)) s_earnedAt[u.id] = u.at ?? string.Empty;
                        }
                }
                catch (Exception e)
                {
                    // Kept aside rather than written over: a ledger that could not be read this time is still
                    // somebody's history, and the store (when there is one) will hand most of it back.
                    Debug.LogWarning($"[LastCall] Achievements could not be read ({e.Message}); set aside as {SpoiltPath}");
                    try { File.Copy(FilePath, SpoiltPath, true); } catch (Exception) { }
                    stats = null;
                    unlocked = null;
                    s_earnedAt.Clear();
                }
            }
            s_tracker = new AchievementTracker(s_book, stats, unlocked);
            // An achievement added after its stats were built up is earned at the first load that sees it.
            var caughtUp = s_tracker.Reconcile();
            if (caughtUp.Unlocked.Count > 0)
            {
                string now = Now();
                foreach (var a in caughtUp.Unlocked) s_earnedAt[a.Id] = now;
                s_dirty = true;
            }
        }

        private static void Write()
        {
            s_lastWrite = Time.unscaledTime;
            s_dirty = false;
            if (!Keeps || s_tracker == null) return;
            try
            {
                var file = new LedgerFile { stats = new List<StatEntry>(), unlocked = new List<UnlockEntry>() };
                foreach (var stat in Stats.All)
                {
                    long v = s_tracker.Stat(stat);
                    if (v > 0) file.stats.Add(new StatEntry { stat = stat, value = v });
                }
                foreach (var id in s_tracker.UnlockedIds)
                    file.unlocked.Add(new UnlockEntry { id = id, at = s_earnedAt.TryGetValue(id, out var at) ? at : string.Empty });
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(FreshPath, JsonUtility.ToJson(file, true));
                if (File.Exists(FilePath)) File.Replace(FreshPath, FilePath, PrevPath);
                else File.Move(FreshPath, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LastCall] Achievements could not be written: {e.Message}");
            }
        }

        private static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        [Serializable]
        private sealed class LedgerFile
        {
            public const int Format = 1;
            public int format = Format;
            public List<StatEntry> stats;
            public List<UnlockEntry> unlocked;
        }

        [Serializable]
        private sealed class StatEntry
        {
            public string stat;
            public long value;
        }

        [Serializable]
        private sealed class UnlockEntry
        {
            public string id;
            public string at;
        }
    }
}
