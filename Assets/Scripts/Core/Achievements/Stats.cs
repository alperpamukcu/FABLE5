using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>How a lifetime number moves: SUMMED over every bar the player has run, or the BEST
    /// any one bar (or night) has ever reached.</summary>
    public enum StatKind
    {
        Sum,
        Best,
    }

    /// <summary>
    /// THE LIFETIME NUMBERS (2026-09-28, the author: "Oyuna steam etkileşimleri koyalım ... sık başarım
    /// kazanılsın oyuncuya ilerleme hissi verilsin"). Every achievement is a threshold on one or more of
    /// these, and the set is CLOSED: the data names stats from this list and DataLoader refuses any other
    /// name, so a typo in achievements.json is a load error rather than an achievement nobody can earn.
    ///
    /// The run reports them as it plays (<see cref="TycoonRun.Feats"/>), from the verbs the player, the
    /// sim bot and the tests all use — a serve is counted where the judge rules on it, a night where its
    /// books close — never from the screen. Each name is also the Steamworks stat's API name, so a Sum
    /// stat is what draws an achievement's progress bar on Steam.
    /// </summary>
    public static class Stats
    {
        // ── summed over every bar ────────────────────────────────────────────────────────────────
        /// <summary>Nights whose books were closed.</summary>
        public const string Nights = "nights";
        /// <summary>Drinks served exactly as ordered.</summary>
        public const string ServesExact = "serves_exact";
        /// <summary>Exact serves of a shaken page that went out shaken.</summary>
        public const string ShakenExact = "shaken_exact";
        /// <summary>Exact serves of a stirred page that went out stirred.</summary>
        public const string StirredExact = "stirred_exact";
        /// <summary>Exact pints whose head landed in the good band.</summary>
        public const string PintsGoodHead = "pints_good_head";
        /// <summary>PERFECT makes: an authored page with every band inside the perfect window.</summary>
        public const string PerfectPours = "perfect_pours";
        /// <summary>Guests who ordered another round.</summary>
        public const string ExtraRounds = "extra_rounds";
        /// <summary>Dollars of tip, as the judge prices them at the serve.</summary>
        public const string Tips = "tips";
        /// <summary>People rightly shown the door (a minor, or a card that was not theirs).</summary>
        public const string RightKicks = "right_kicks";
        /// <summary>An honest adult shown the door.</summary>
        public const string WrongKicks = "wrong_kicks";
        /// <summary>Each kind of forged card caught at the door.</summary>
        public const string CaughtBorrowed = "caught_borrowed";
        public const string CaughtAltered = "caught_altered";
        public const string CaughtCopied = "caught_copied";
        public const string CaughtDrawn = "caught_drawn";
        /// <summary>Marks wiped off the counter.</summary>
        public const string Wipes = "wipes";
        /// <summary>Glasses run through the sink.</summary>
        public const string GlassesWashed = "glasses_washed";
        /// <summary>Nights of ten or more guests with nobody walking out and nothing wrong across the bar.</summary>
        public const string CleanSheets = "clean_sheets";
        /// <summary>Hostess jobs finished and paid.</summary>
        public const string JobsDone = "jobs_done";
        /// <summary>Tins of fizz shaken until they burst.</summary>
        public const string Blowouts = "blowouts";
        /// <summary>Bars that closed for good.</summary>
        public const string BarsLost = "bars_lost";

        // ── the best ever reached ────────────────────────────────────────────────────────────────
        /// <summary>The highest rung of the rank ladder a bar has stood on (0–6).</summary>
        public const string BestRank = "best_rank";
        /// <summary>A single night's filed stars, in tenths (0–50).</summary>
        public const string BestNightStars = "best_night_stars";
        /// <summary>A single night's tips, in dollars.</summary>
        public const string BestNightTips = "best_night_tips";
        /// <summary>The till at the close of a night, in dollars.</summary>
        public const string BestTill = "best_till";
        /// <summary>Nights one bar has stayed open.</summary>
        public const string LongestRun = "longest_run";
        /// <summary>Pages one bar has bought for its book.</summary>
        public const string PagesBought = "pages_bought";
        /// <summary>Pages in one bar's book, the opening ones included.</summary>
        public const string RecipesKnown = "recipes_known";
        /// <summary>1 once a bar owns every page there is.</summary>
        public const string WholeBook = "whole_book";
        /// <summary>Pages one bar has perfected.</summary>
        public const string RecipesPerfected = "recipes_perfected";
        /// <summary>Brands one bar has bought onto its shelf.</summary>
        public const string BrandsOwned = "brands_owned";
        /// <summary>Fittings one bar has bought for its room (the pieces it opened with not counted).</summary>
        public const string FittingsOwned = "fittings_owned";
        /// <summary>The highest tier any line of one bar's glassware has reached (1–6).</summary>
        public const string BestGlassTier = "best_glass_tier";
        /// <summary>Stools at one bar.</summary>
        public const string Stools = "stools";
        /// <summary>The room's comfort from its fittings and glass, in hundredths of a star (0–500).</summary>
        public const string RoomComfort = "room_comfort";
        /// <summary>The closest any guest has come to the bar, as a <see cref="Relationship"/> ordinal.</summary>
        public const string BestRegular = "best_regular";

        private static readonly string[] Summed =
        {
            Nights, ServesExact, ShakenExact, StirredExact, PintsGoodHead, PerfectPours, ExtraRounds, Tips,
            RightKicks, WrongKicks, CaughtBorrowed, CaughtAltered, CaughtCopied, CaughtDrawn,
            Wipes, GlassesWashed, CleanSheets, JobsDone, Blowouts, BarsLost,
        };

        private static readonly string[] Bests =
        {
            BestRank, BestNightStars, BestNightTips, BestTill, LongestRun, PagesBought, RecipesKnown, WholeBook,
            RecipesPerfected, BrandsOwned, FittingsOwned, BestGlassTier, Stools, RoomComfort, BestRegular,
        };

        private static readonly Dictionary<string, StatKind> Kinds = BuildKinds();

        /// <summary>Every stat name, in a fixed order (the Steamworks setup sheet lists them in it).</summary>
        public static readonly IReadOnlyList<string> All = BuildAll();

        private static Dictionary<string, StatKind> BuildKinds()
        {
            var kinds = new Dictionary<string, StatKind>(StringComparer.Ordinal);
            foreach (var s in Summed) kinds.Add(s, StatKind.Sum);
            foreach (var s in Bests) kinds.Add(s, StatKind.Best);
            return kinds;
        }

        private static IReadOnlyList<string> BuildAll()
        {
            var all = new List<string>(Summed);
            all.AddRange(Bests);
            return all;
        }

        public static bool IsKnown(string stat) => stat != null && Kinds.ContainsKey(stat);

        public static StatKind KindOf(string stat)
        {
            if (stat == null || !Kinds.TryGetValue(stat, out var kind))
                throw new ArgumentException($"'{stat}' is not a stat the run reports", nameof(stat));
            return kind;
        }
    }

    /// <summary>One movement of one lifetime number: add <see cref="Value"/> to a Sum stat, or raise a
    /// Best stat to it.</summary>
    public readonly struct StatBump
    {
        public readonly string Stat;
        public readonly long Value;

        public StatBump(string stat, long value)
        {
            Stat = stat;
            Value = value;
        }

        public override string ToString() => Stat + (Stats.KindOf(Stat) == StatKind.Sum ? " +" : " ≥ ") + Value;
    }
}
