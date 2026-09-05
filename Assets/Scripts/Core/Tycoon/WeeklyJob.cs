using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// ONE THING TO GET DONE THIS WEEK (2026-09-04, the author: "haftanın sonunda hikaye
    /// karakteri dediğimiz karakter gelip bize bu hafta içerisinde yapmamız gereken bir görev
    /// vermeli. örneğin şu siparişten 5 adet servis et gibi").
    ///
    /// It replaces the scripted last call for now. That beat asked the player to read a
    /// conversation and pass a trial inside one night; this asks for something across SIX,
    /// which is the unit the calendar was already built in (<see cref="BarCalendar"/>: six
    /// open nights, Sunday shut) and which nothing in the loop was using. A week with a
    /// number on it is a week the player is counting towards.
    ///
    /// WHAT IT IS NOT: a script. There is no dialogue tree, no trial clock, no pass mark —
    /// a job is a recipe, a count, and the week it has to happen in. The written arc is
    /// switched off rather than deleted (GDD 26 is still in the tree); when it comes back the
    /// two are not rivals, because a guest who talks and a guest who leaves a job on the bar
    /// are different beats.
    /// </summary>
    public sealed class WeeklyJob
    {
        /// <summary>The drink they want to see going out.</summary>
        public string RecipeId { get; }

        /// <summary>Its name, kept so the strip on the HUD needs no catalogue to draw.</summary>
        public string RecipeName { get; }

        /// <summary>Who left it. Presentation only — nothing here is graded by whom.</summary>
        public string Who { get; }

        /// <summary>How many have to go out.</summary>
        public int Target { get; }

        /// <summary>How many have so far. Only EXACT serves count — a job asking for five
        /// Negronis is not satisfied by five near misses, and the note over the drinker's
        /// head already says what a near miss was.</summary>
        public int Served { get; private set; }

        /// <summary>The week it has to happen in — the one AFTER the night it was handed
        /// over, because it is handed over as the week closes.</summary>
        public int Week { get; }

        public bool IsDone => Served >= Target;

        /// <summary>What is still owed, never below zero.</summary>
        public int Left => Math.Max(0, Target - Served);

        public WeeklyJob(string recipeId, string recipeName, int target, int week, string who = null)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
                throw new ArgumentException("A job has to name a drink.", nameof(recipeId));
            if (target < 1) throw new ArgumentOutOfRangeException(nameof(target));
            if (week < 1) throw new ArgumentOutOfRangeException(nameof(week));
            RecipeId = recipeId;
            RecipeName = string.IsNullOrWhiteSpace(recipeName) ? recipeId : recipeName;
            Target = target;
            Week = week;
            Who = string.IsNullOrWhiteSpace(who) ? DefaultGiver : who;
        }

        /// <summary>Whose job it is when nobody was named — the host, who works the shift.</summary>
        public const string DefaultGiver = "ECE";

        /// <summary>Counts one serve towards this job, if it is the drink and there is room
        /// left. Returns true when this serve is the one that finished it.</summary>
        public bool Count(string recipeId)
        {
            if (IsDone || recipeId != RecipeId) return false;
            Served++;
            return IsDone;
        }

        /// <summary>Whether this job is the one live in <paramref name="day"/>'s week.</summary>
        public bool RunsOn(int day) => BarCalendar.WeekOf(day) == Week;

        public override string ToString() => $"{RecipeName} {Served}/{Target} (week {Week})";
    }

    /// <summary>
    /// Picks the week's job. Split from the model so the RULE for what gets asked has one
    /// home and the tests can pin it without a run.
    /// </summary>
    public static class WeeklyJobs
    {
        /// <summary>The night the job is handed over: the week's last, which the calendar
        /// already calls the one a name comes on.</summary>
        public const BarNight HandOverNight = BarCalendar.VipNight;

        /// <summary>How many of it. It grows with the bar, because a five-stool room
        /// serving fifteen a night is not asked for the same as a four-stool one serving
        /// eight — but it stays a number a bad week can still reach.</summary>
        public const int MinTarget = 3, MaxTarget = 7;

        public static int TargetFor(int week) =>
            Math.Min(MaxTarget, MinTarget + Math.Max(0, week - 1) / 2);

        /// <summary>
        /// The job for <paramref name="week"/>, or null when there is nothing sensible to
        /// ask for.
        ///
        /// It only ever asks for a drink the bar can ACTUALLY POUR TONIGHT — on the menu,
        /// and every band of it answerable off the shelf. A job naming a drink the player
        /// has no bottle for is not a challenge, it is a bug they cannot tell from one; and
        /// since the market runs between the hand-over and the week, a bar that wants a
        /// harder job can go and buy its way into one.
        /// </summary>
        public static WeeklyJob Roll(IReadOnlyList<RecipeDefinition> menu,
            Func<RecipeDefinition, bool> pourable, int week, RunRng rng, string who = null)
        {
            if (menu == null || menu.Count == 0 || week < 1) return null;
            var pool = new List<RecipeDefinition>();
            foreach (var r in menu)
            {
                if (r == null) continue;
                // Draught and the neat pour are the two drinks nobody has to learn, so
                // asking for five of either is asking for nothing.
                if (!r.HasAuthoredRatios) continue;
                if (pourable != null && !pourable(r)) continue;
                pool.Add(r);
            }
            if (pool.Count == 0) return null;
            var pick = pool[rng == null ? 0 : rng.GetStream("job").NextInt(0, pool.Count)];
            return new WeeklyJob(pick.Id, pick.Name, TargetFor(week), week, who);
        }
    }
}
