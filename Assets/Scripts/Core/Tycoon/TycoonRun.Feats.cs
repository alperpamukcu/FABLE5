using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// WHAT THE RUN HAS DONE, REPORTED AS IT DOES IT (2026-09-28, the achievements). Each verb that earns
    /// something pushes a <see cref="StatBump"/> — a serve where the judge rules on it, a kick at the
    /// door, a burst tin, a paid job — and the night's own numbers (stars, till, the book, the room, the
    /// chores) are pushed once, where its books close. The Game layer drains the queue into the lifetime
    /// ledger; nothing here knows what an achievement is.
    ///
    /// Two guards, both in the rules layer because the UI is not trusted with them:
    /// - nothing is queued unless somebody is listening (<see cref="RunFeats.Recording"/>) — the sim, the
    ///   tests and a run nobody watches allocate nothing;
    /// - a run touched by a DEV verb reports nothing, ever again (<see cref="DevTouched"/>, saved with it):
    ///   a preset, a skipped night or a fitting handed over for free are not things anybody did.
    /// </summary>
    public sealed partial class TycoonRun
    {
        /// <summary>The queue of stat bumps the run has earned since it was last drained.</summary>
        public RunFeats Feats { get; } = new RunFeats();

        private bool _devTouched;

        /// <summary>A dev verb has been used on this run (or on its rating): it earns nothing.</summary>
        public bool DevTouched => _devTouched || Rating.DevTouched;

        /// <summary>Every Dev* verb calls this first.</summary>
        private void MarkDevTouched() => _devTouched = true;

        // Run-lifetime counts of what the market sold this bar, filed at each dawn from the night's own
        // purchase slip — after the refund window has closed, so a buy taken back is never counted.
        private int _brandsBought;
        private int _fittingsBought;

        private void Feat(string stat, long value)
        {
            if (value <= 0 || DevTouched) return;
            Feats.Push(stat, value);
        }

        /// <summary>The serve's numbers, read before the vessels are reset (ServeTo).</summary>
        private void FeatTheServe(CustomerVisit visit, OrderMatch match, GlassContents delivered,
            ServiceVerdict verdict)
        {
            if (!Feats.Recording) return;
            if (match == OrderMatch.Exact)
            {
                Feat(Stats.ServesExact, 1);
                var wanted = visit.OrderTruth.Wanted;
                if (wanted.Prep == PrepMethod.Shaken && delivered.HasPreparation(Preparations.Shaken.Id))
                    Feat(Stats.ShakenExact, 1);
                if (wanted.Prep == PrepMethod.Stirred && delivered.HasPreparation(Preparations.Stirred.Id))
                    Feat(Stats.StirredExact, 1);
                if (delivered.HasPreparation(Preparations.Draught.Id) && verdict.CraftLanded)
                    Feat(Stats.PintsGoodHead, 1);
                if (verdict.PerfectMake) Feat(Stats.PerfectPours, 1);
            }
            Feat(Stats.Tips, verdict.Tip);
            if (verdict.OrdersAgain) Feat(Stats.ExtraRounds, 1);
            if (visit.Regular != null) Feat(Stats.BestRegular, (int)visit.Regular.Relationship);
        }

        /// <summary>The door's numbers (Kick).</summary>
        private void FeatTheKick(bool rightly, IdPapers papers)
        {
            if (!rightly)
            {
                Feat(Stats.WrongKicks, 1);
                return;
            }
            Feat(Stats.RightKicks, 1);
            switch (papers?.Forgery ?? Forgery.None)
            {
                case Forgery.Borrowed: Feat(Stats.CaughtBorrowed, 1); break;
                case Forgery.Altered: Feat(Stats.CaughtAltered, 1); break;
                case Forgery.Copied: Feat(Stats.CaughtCopied, 1); break;
                case Forgery.Drawn: Feat(Stats.CaughtDrawn, 1); break;
            }
        }

        /// <summary>
        /// THE NIGHT'S NUMBERS, at the moment its books close (ContinueToNextDay) — the rating filed, the
        /// market's buys final, the floor and its chores not yet swept away for tomorrow.
        /// </summary>
        private void FeatTheNight(int served, int walked)
        {
            // The slip's buys are counted whether or not anybody listens: they are the bar's own history.
            foreach (var p in _todayPurchases)
            {
                if (p.What == DayPurchase.Kind.Brand) _brandsBought++;
                else if (p.What == DayPurchase.Kind.Fixture) _fittingsBought++;
            }
            if (!Feats.Recording) return;

            Feat(Stats.Nights, 1);
            Feat(Stats.LongestRun, Day);
            Feat(Stats.BestNightStars, (long)Math.Round(Rating.LastNight * 10.0));
            Feat(Stats.BestNightTips, DayTips);
            Feat(Stats.BestTill, Money);
            if (served >= CleanSheetGuests && walked == 0 && !NightHadAMistake) Feat(Stats.CleanSheets, 1);

            Feat(Stats.BestRank, Rank.Index);
            Feat(Stats.PagesBought, _boughtRecipes.Count);
            Feat(Stats.RecipesKnown, MenuRecipes.Count);
            bool bookWhole = true;
            foreach (var _ in LockedRecipes) { bookWhole = false; break; }
            if (bookWhole) Feat(Stats.WholeBook, 1);
            Feat(Stats.RecipesPerfected, PerfectedCount);
            Feat(Stats.BrandsOwned, _brandsBought);
            Feat(Stats.FittingsOwned, _fittingsBought);
            int bestGlass = 0;
            foreach (var g in _glassware) bestGlass = Math.Max(bestGlass, GlassTier(g.Id));
            Feat(Stats.BestGlassTier, bestGlass);
            Feat(Stats.Stools, Seats);
            Feat(Stats.RoomComfort, (long)Math.Round(ComfortBase * 100.0));

            Feat(Stats.Wipes, Floor.House.Wipes);
            Feat(Stats.GlassesWashed, Floor.House.GlassesWashed);
            if (Ledger.IsBankrupt) Feat(Stats.BarsLost, 1);
        }

        /// <summary>A clean sheet is a night of at least this many guests.</summary>
        public const int CleanSheetGuests = 10;
    }

    /// <summary>The run's queue of stat bumps. Off until the Game layer (or the pacing sim) listens.</summary>
    public sealed class RunFeats
    {
        private readonly List<StatBump> _pending = new List<StatBump>();

        /// <summary>Somebody drains this queue. Off by default, so nothing piles up where nobody reads it.</summary>
        public bool Recording { get; set; }

        internal void Push(string stat, long value)
        {
            if (Recording) _pending.Add(new StatBump(stat, value));
        }

        public bool Pending => _pending.Count > 0;

        /// <summary>Everything earned since the last call, oldest first; the queue is emptied.</summary>
        public IReadOnlyList<StatBump> Take()
        {
            if (_pending.Count == 0) return Array.Empty<StatBump>();
            var taken = _pending.ToArray();
            _pending.Clear();
            return taken;
        }
    }
}
