using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LastCall.Core;

namespace LastCall.EditorTools
{
    /// <summary>
    /// THE ACHIEVEMENTS' PACE, MEASURED (2026-09-28, the author: "her saniye başarım da kazanılmasın çok zor
    /// başarımlarda olsun ama sık başarım kazanılsın"). The floor bot plays seeded bars with the run's feats
    /// recording; each bar keeps a fresh lifetime ledger — a new player's first bar — and this notes the night
    /// every achievement landed on. The report says three things the brief asks about: how many land on each
    /// night of the opening weeks, the longest run of nights with nothing, and which achievements a bar does
    /// not reach at all. The bot is a floor (it never learns, it dies young), so a late achievement it never
    /// earns is expected; an early one it never earns is a design bug.
    /// </summary>
    internal sealed class AchievementPacing
    {
        private readonly IReadOnlyList<AchievementDefinition> _book;
        private readonly Dictionary<string, List<int>> _nightOf = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly List<int> _longestDry = new List<int>();
        private readonly List<int> _nightsPlayed = new List<int>();
        private readonly Dictionary<int, int> _unlocksOnNight = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _barsOnNight = new Dictionary<int, int>();
        private AchievementTracker _ledger;
        private List<int> _thisRunNights;
        private int _runs;

        public AchievementPacing(IReadOnlyList<AchievementDefinition> book)
        {
            _book = book;
            foreach (var a in book) _nightOf[a.Id] = new List<int>();
        }

        public void BeginRun(TycoonRun run)
        {
            run.Feats.Recording = true;
            _ledger = new AchievementTracker(_book);
            _thisRunNights = new List<int>();
        }

        /// <summary>After each night's books close: everything the night and its serves reported.</summary>
        public void Drain(TycoonRun run)
        {
            int night = run.Ledger.History.Count;
            _barsOnNight.TryGetValue(night, out int bars);
            _barsOnNight[night] = bars + 1;
            var news = _ledger.Apply(run.Feats.Take());
            foreach (var a in news.Unlocked)
            {
                _nightOf[a.Id].Add(night);
                _thisRunNights.Add(night);
                _unlocksOnNight.TryGetValue(night, out int n);
                _unlocksOnNight[night] = n + 1;
            }
        }

        public void EndRun(TycoonRun run)
        {
            _runs++;
            int played = run.Ledger.History.Count;
            _nightsPlayed.Add(played);
            int last = 0, dry = 0;
            foreach (int n in _thisRunNights.Distinct().OrderBy(n => n))
            {
                dry = Math.Max(dry, n - last - 1);
                last = n;
            }
            dry = Math.Max(dry, played - last);
            _longestDry.Add(dry);
        }

        public string Report(int dayCap)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Achievement pacing — the floor bot");
            sb.AppendLine();
            sb.AppendLine($"Written by `LastCall → Achievement Pacing`. **{_runs}** seeded bars, horizon {dayCap} nights, each");
            sb.AppendLine("with a fresh ledger (a new player's first bar). The bot is a FLOOR: it plays at one standard,");
            sb.AppendLine("never learns, and goes bankrupt young — so a late achievement it does not reach is expected, an");
            sb.AppendLine("early one it does not reach is a design bug. Nights are counted from 1; a bar's night is the");
            sb.AppendLine("one whose books just closed (the market comes before, so purchases count on their own night).");
            sb.AppendLine();
            sb.AppendLine($"Nights played p25/median/p75: {Pct(_nightsPlayed, 25)} / {Pct(_nightsPlayed, 50)} / {Pct(_nightsPlayed, 75)}.");
            sb.AppendLine($"Longest run of nights with nothing earned, p25/median/p75: {Pct(_longestDry, 25)} / {Pct(_longestDry, 50)} / {Pct(_longestDry, 75)}.");
            sb.AppendLine();
            sb.AppendLine("## How many land on each night");
            sb.AppendLine();
            sb.AppendLine("| night | bars still open | unlocks per open bar | earned by then (per bar) |");
            sb.AppendLine("|--:|--:|--:|--:|");
            double cumulative = 0;
            for (int night = 1; night <= Math.Min(dayCap, 40); night++)
            {
                _barsOnNight.TryGetValue(night, out int bars);
                _unlocksOnNight.TryGetValue(night, out int unlocks);
                if (bars == 0) break;
                cumulative += (double)unlocks / _runs;
                sb.AppendLine($"| {night} | {bars} | {F((double)unlocks / bars)} | {F(cumulative)} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Each achievement");
            sb.AppendLine();
            sb.AppendLine("| id | tier | name | bars that earned it | night: p25 / median / p75 |");
            sb.AppendLine("|---|---|---|--:|--:|");
            foreach (var a in _book)
            {
                var nights = _nightOf[a.Id];
                string when = nights.Count == 0 ? "—" : $"{Pct(nights, 25)} / {Pct(nights, 50)} / {Pct(nights, 75)}";
                sb.AppendLine($"| {a.Id} | {a.Tier} | {a.Name} | {nights.Count} of {_runs} ({F(100.0 * nights.Count / Math.Max(1, _runs))}%) | {when} |");
            }
            return sb.ToString();
        }

        private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static int Pct(List<int> xs, int p)
        {
            if (xs.Count == 0) return 0;
            var s = xs.OrderBy(x => x).ToList();
            int i = (int)Math.Round((s.Count - 1) * p / 100.0);
            return s[Math.Max(0, Math.Min(s.Count - 1, i))];
        }
    }
}
