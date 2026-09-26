using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using UnityEditor;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// Writes <c>Docs/ECONOMY_&lt;today&gt;.md</c>: what a night pays, night by night, at three
    /// standards of play (2026-09-23, the author: *"her gün ekonomiye göre nasıl siparişler gelecek
    /// oyuncu hangi gün ne kadar kazanabilecek bunların hesaplamalarını yap"*).
    ///
    /// The 200-run sim plays the loop and reports what happened; this computes what SHOULD happen
    /// before anyone plays it, from the same constants the game reads
    /// (<see cref="EconomyProjection"/>). The two are meant to be read together — where they
    /// disagree, one of them is wrong and it is worth knowing which.
    ///
    /// The prose half of the document is hand-written and lives in the file already; this rewrites
    /// only the tables between the markers, so re-running it after a balance change never costs the
    /// commentary.
    /// </summary>
    public static class EconomyReport
    {
        private const string Begin = "<!-- ECONOMY TABLES BEGIN -->";
        private const string End = "<!-- ECONOMY TABLES END -->";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [MenuItem("LastCall/Economy Projection")]
        public static void Write()
        {
            var recipes = DataLoader.ParseRecipes(
                File.ReadAllText(Path.Combine(Application.dataPath, "Data/recipes/recipes.json")));
            var fixtures = DataLoader.ParseFixtures(
                File.ReadAllText(Path.Combine(Application.dataPath, "Data/fixtures/fixtures.json"))).Fixtures;
            var glassware = DataLoader.ParseGlassware(
                File.ReadAllText(Path.Combine(Application.dataPath, "Data/glassware/glassware.json")));
            var deck = DataLoader.ParseDeck(
                File.ReadAllText(Path.Combine(Application.dataPath, "Data/bottles/base_bar.json")));
            var config = TycoonConfig.Default;

            var sb = new StringBuilder();
            sb.AppendLine(Begin);
            Week(sb, recipes, config);
            foreach (var (name, q) in new[]
                     {
                         ("Still learning", EconomyProjection.Learning),
                         ("Competent", EconomyProjection.Competent),
                         ("Knows the book", EconomyProjection.Sharp),
                     })
            {
                Standard(sb, recipes, config, name, q);
                Fitted(sb, recipes, config, name, q, fixtures);
            }
            Room(sb, recipes, config, fixtures, glassware, deck);
            Bands(sb, recipes);
            Mix(sb);
            sb.Append(End);

            var path = Directory.GetFiles(Path.Combine(Application.dataPath, "../Docs"), "ECONOMY_*.md")
                .OrderBy(p => p).LastOrDefault();
            if (path == null)
            {
                Debug.LogError("No Docs/ECONOMY_*.md to write the tables into.");
                return;
            }
            string doc = File.ReadAllText(path);
            int a = doc.IndexOf(Begin, System.StringComparison.Ordinal);
            int b = doc.IndexOf(End, System.StringComparison.Ordinal);
            doc = a >= 0 && b > a
                ? doc.Substring(0, a) + sb + doc.Substring(b + End.Length)
                : doc.TrimEnd() + "\n\n" + sb + "\n";
            File.WriteAllText(path, doc);
            Debug.Log("Economy projection written to " + path);
            EditorUtility.RevealInFinder(path);
        }

        private static void Week(StringBuilder sb, System.Collections.Generic.IReadOnlyList<RecipeDefinition> book,
            TycoonConfig config)
        {
            var opening = book.Where(r => !r.Locked && r.RatioRequirements.Count > 0).ToList();
            sb.AppendLine().AppendLine("### The written week, night by night").AppendLine();
            sb.AppendLine("| night | covers | sheet | gross | tips | stock | rent | net | till |");
            sb.AppendLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (var n in EconomyProjection.Walk(book, config, FirstWeek.Nights,
                         EconomyProjection.Competent))
            {
                int sheet = FirstWeek.For(n.Day, 0.0, opening, config)
                    .Sum(r => DrinkOrder.MenuPrice(r, 0.0));
                sb.AppendLine(string.Format(Inv,
                    "| {0} | {1} | ${2} | ${3} | ${4} | ${5} | ${6} | **${7}** | ${8} |",
                    n.Day, n.Covers, sheet, n.Gross, n.Tips, n.Stock, n.Rent, n.Net, n.Till));
            }
        }

        private static void Standard(StringBuilder sb,
            System.Collections.Generic.IReadOnlyList<RecipeDefinition> book, TycoonConfig config,
            string name, double quality)
        {
            var crowd = BarRating.CrowdFor(BarRating.ExactStarsFor(
                EconomyProjection.SatisfactionFor(quality)));
            sb.AppendLine().AppendLine(string.Format(Inv, "### {0} (serve quality {1:0.00}, crowd {2})",
                name, quality, crowd)).AppendLine();
            sb.AppendLine("| day | stars | covers | avg cover | gross | tips | stock | rent | net | till | hard |");
            sb.AppendLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (var n in EconomyProjection.Walk(book, config, 42, quality))
            {
                if (n.Day > 8 && n.Day % 3 != 0) continue;
                sb.AppendLine(string.Format(Inv,
                    "| {0} | {1:0.0} | {2} | ${3:0.0} | ${4} | ${5} | ${6} | ${7} | **${8}** | ${9} | {10:0}% |",
                    n.Day, n.Stars, n.Covers, n.AverageCover, n.Gross, n.Tips, n.Stock, n.Rent,
                    n.Net, n.Till, n.HardShare * 100));
            }
        }

        /// <summary>
        /// THE SAME NIGHTS IN A FITTED ROOM (2026-09-23, the fitting buffs): every piece the standing
        /// opens bought and its top rung worn (<see cref="HouseBuffs.FullRoomAt"/>). The projection
        /// reaches the room's PRICE, TIP and the door's CROWD as an upper bound — it has no stools and
        /// no waiting line — so the clock's kinds (patience, lateness, the round) are the sim's to
        /// measure, and the heading says so.
        /// </summary>
        private static void Fitted(StringBuilder sb,
            System.Collections.Generic.IReadOnlyList<RecipeDefinition> book, TycoonConfig config,
            string name, double quality, System.Collections.Generic.IReadOnlyList<FixtureDefinition> fixtures)
        {
            sb.AppendLine().AppendLine(string.Format(Inv,
                "#### {0}, in a fitted room (price, tip and the door's upper bound; the clock is the sim's)",
                name)).AppendLine();
            sb.AppendLine("| day | stars | the room | covers | gross | tips | net | till | net vs bare |");
            sb.AppendLine("|--:|--:|---|--:|--:|--:|--:|--:|--:|");
            var bare = EconomyProjection.Walk(book, config, 42, quality);
            var fitted = EconomyProjection.Walk(book, config, 42, quality,
                houseOn: (d, s) => HouseBuffs.FullRoomAt(fixtures, s));
            for (int i = 0; i < fitted.Count && i < bare.Count; i++)
            {
                var n = fitted[i];
                if (n.Day > 8 && n.Day % 3 != 0) continue;
                var room = HouseBuffs.FullRoomAt(fixtures, n.Stars);
                string lift = bare[i].Net == 0 ? "—"
                    : string.Format(Inv, "{0:+0;-0;0}%", 100.0 * (n.Net - bare[i].Net) / System.Math.Abs(bare[i].Net));
                sb.AppendLine(string.Format(Inv,
                    "| {0} | {1:0.0} | {2} | {3} | ${4} | ${5} | **${6}** | ${7} | {8} |",
                    n.Day, n.Stars, RoomLine(room), n.Covers, n.Gross, n.Tips, n.Net, n.Till, lift));
            }
        }

        /// <summary>How far the furnished walk runs: long enough for a competent bar to finish the house.</summary>
        private const int RoomNights = 120;

        /// <summary>
        /// THE ROOM, NIGHT BY NIGHT (2026-09-26, the author: "Tüm geliştirmeleri ekonomi dengesine dahil et,
        /// Oyuncu oyunda maksimum 5 konfora ulaşmalı ve bu oyun sonlarına yakın gerçekleşmeli"): the furnished
        /// walk (<see cref="EconomyProjection.WalkFurnishing"/>) at the three standards — a bar that buys its
        /// pages, its bottles and its room out of its own till, from the opening shelf and the bare room — and
        /// the night the house reaches five. The caption says what the walk assumes, because the finish it
        /// prints is only as good as that assumption.
        /// </summary>
        private static void Room(StringBuilder sb,
            System.Collections.Generic.IReadOnlyList<RecipeDefinition> book, TycoonConfig config,
            System.Collections.Generic.IReadOnlyList<FixtureDefinition> fixtures,
            System.Collections.Generic.IReadOnlyList<GlasswareDefinition> glassware, LoadedDeck deck)
        {
            var shelf = deck.StartingCards;
            var catalogue = deck.Cards.Where(c => !deck.IsStarting(c)).Concat(deck.LockedCards).ToList();
            var standards = new[]
            {
                ("Still learning", EconomyProjection.Learning),
                ("Competent", EconomyProjection.Competent),
                ("Knows the book", EconomyProjection.Sharp),
            };
            var walks = standards.Select(s => EconomyProjection.WalkFurnishing(book, config, fixtures, glassware,
                RoomNights, s.Item2, shelf, catalogue)).ToList();

            sb.AppendLine().AppendLine("### The room, night by night").AppendLine();
            sb.AppendLine("A bar that buys its own way: the rung's better bottles, every page its gate opens, the bottles");
            sb.AppendLine("those pages need, then the room — the most comfort per dollar while the room is under the");
            sb.AppendLine("standing plus half a star, the night's one fitting, then the rest cheapest first — keeping");
            sb.AppendLine("tomorrow's rent less what tonight cleared. Each night is priced from the pages and bottles it");
            sb.AppendLine("owns and files the lower of the climb, the menu's ceiling and the room (a clean counter).");
            sb.AppendLine();
            sb.AppendLine("**The climb is the projection's assumption, not a result:** the service side of every night is the");
            sb.AppendLine(string.Format(Inv,
                "standing plus {0:0.000} (`StarsOn`'s slope). A competent night's drinks are worth about {1:0.0} stars",
                EconomyProjection.Climb,
                BarRating.ExactStarsFor(EconomyProjection.SatisfactionFor(EconomyProjection.Competent))));
            sb.AppendLine("of service, so in play the drinks bind before the room does; what these tables answer is when the");
            sb.AppendLine("MONEY buys the house for a bar that climbs at the projection's pace.");
            sb.AppendLine();
            sb.AppendLine("| standard | comfort 5.00 | 5★ standing | 1★ / 2★ / 3★ / 4★ | nights the room held |");
            sb.AppendLine("|---|--:|--:|--:|--:|");
            for (int i = 0; i < standards.Length; i++)
            {
                var w = walks[i];
                int five = EconomyProjection.ComfortFiveNight(w), top = EconomyProjection.FiveStarNight(w);
                string Reach(double s)
                {
                    foreach (var n in w) if (n.Best + BarRank.Epsilon >= s) return n.Night.Day.ToString(Inv);
                    return "—";
                }
                sb.AppendLine(string.Format(Inv, "| {0} | {1} | {2} | {3} / {4} / {5} / {6} | {7} of {8} |",
                    standards[i].Item1,
                    five > 0 ? "night " + five : "not in " + RoomNights,
                    top > 0 ? "night " + top : "not in " + RoomNights,
                    Reach(1), Reach(2), Reach(3), Reach(4), w.Count(n => n.RoomBound), w.Count));
            }

            for (int i = 0; i < standards.Length; i++)
            {
                var w = walks[i];
                int top = EconomyProjection.FiveStarNight(w);
                sb.AppendLine().AppendLine(string.Format(Inv, "#### {0}, furnishing as it goes", standards[i].Item1)).AppendLine();
                sb.AppendLine("| night | standing | comfort | net | spent: room · pages · bottles | till | held by the room |");
                sb.AppendLine("|--:|--:|--:|--:|--:|--:|:-:|");
                foreach (var n in w)
                {
                    int d = n.Night.Day;
                    if (d > 8 && d % 6 != 0) continue;
                    if (top > 0 && d > top + 6) break;
                    sb.AppendLine(string.Format(Inv,
                        "| {0} | {1:0.00} | {2:0.00} | ${3} | ${4} · ${5} · ${6} | ${7} | {8} |",
                        d, n.Standing, n.ComfortAfter, n.Night.Net, n.Room, n.Pages, n.Bottles, n.Till,
                        n.RoomBound ? "yes" : ""));
                }
            }
        }

        /// <summary>The kinds the projection reads, as a short line: "PRICE +8 · TIP +4 · CROWD +6".</summary>
        private static string RoomLine(HouseBuffs room)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var (kind, word) in new[]
                     {
                         (FittingBuffs.Price, "PRICE"), (FittingBuffs.Tip, "TIP"), (FittingBuffs.Arrivals, "CROWD"),
                     })
            {
                int p = room.Percent(kind);
                if (p != 0) parts.Add(string.Format(Inv, "{0} +{1}", word, p));
            }
            return parts.Count == 0 ? "bare" : string.Join(" · ", parts);
        }

        private static void Bands(StringBuilder sb,
            System.Collections.Generic.IReadOnlyList<RecipeDefinition> book)
        {
            sb.AppendLine().AppendLine("### What a drink is worth, by rung and by work").AppendLine();
            sb.AppendLine("| rung | easy | medium | hard | the pages on it |");
            sb.AppendLine("|--:|--:|--:|--:|---|");
            for (int rung = 0; rung < DrinkPricing.Rungs; rung++)
            {
                var e = DrinkPricing.BandOfRung(rung, DrinkDifficulty.Easy);
                var m = DrinkPricing.BandOfRung(rung, DrinkDifficulty.Medium);
                var h = DrinkPricing.BandOfRung(rung, DrinkDifficulty.Hard);
                int n = book.Count(r => r.RatioRequirements.Count > 0 && DrinkPricing.RungOf(r) == rung);
                sb.AppendLine(string.Format(Inv, "| {0}★ | ${1}–{2} | ${3}–{4} | ${5}–{6} | {7} |",
                    rung, e.Lo, e.Hi, m.Lo, m.Hi, h.Lo, h.Hi, n));
            }
        }

        private static void Mix(StringBuilder sb)
        {
            sb.AppendLine().AppendLine("### What the night asks for, by rung").AppendLine();
            sb.AppendLine("| bar rung | its own pages | one below | two | three |");
            sb.AppendLine("|--:|--:|--:|--:|--:|");
            var w = DayPlan.RungWeights;
            for (int rung = 0; rung < DrinkPricing.Rungs; rung++)
            {
                int total = 0;
                for (int d = 0; d <= rung && d < w.Length; d++) total += w[d];
                string Share(int d) => d > rung ? "—" : (w[d] * 100 / total) + "%";
                sb.AppendLine(string.Format(Inv, "| {0}★ | {1} | {2} | {3} | {4} |",
                    rung, Share(0), Share(1), Share(2), Share(3)));
            }
            sb.AppendLine();
        }
    }
}
