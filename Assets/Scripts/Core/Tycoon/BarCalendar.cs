using System;

namespace LastCall.Core
{
    /// <summary>The bar's open nights. Monday is dark, so it is not in the week at all —
    /// the calendar simply skips it, and every night the game has is one of these six.</summary>
    public enum BarNight
    {
        Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday,
    }

    /// <summary>
    /// The week (the author's calendar): six open nights, Tuesday through Sunday, Mondays dark.
    ///
    /// It was on the screen for weeks before it was a rule — the plaque and the night's slip
    /// have always read "WEEK 2 · FRIDAY" — and it meant nothing: any night was like any other
    /// night. The story is what gives it teeth (GDD 26 §2b): a guest comes at the WEEKEND, so
    /// the days between are the ones you go shopping in, and a name on the plaque is a date the
    /// player can count towards. Presentation reads the same class the rules do, because a
    /// calendar the HUD keeps to itself is a calendar that can disagree with the game.
    /// </summary>
    public static class BarCalendar
    {
        /// <summary>Nights the bar opens in a week. Monday is not one of them.</summary>
        public const int OpenNights = 6;

        /// <summary>Which night day N is. Day 1 is the first Tuesday.</summary>
        public static BarNight NightOf(int day) => (BarNight)(Index(day) % OpenNights);

        /// <summary>Which week day N is in, counting from 1.</summary>
        public static int WeekOf(int day) => Index(day) / OpenNights + 1;

        /// <summary>The day number of a night in a week — the inverse of the two above.</summary>
        public static int DayOf(int week, BarNight night)
        {
            if (week < 1) throw new ArgumentOutOfRangeException(nameof(week), "The bar opened in week 1.");
            return (week - 1) * OpenNights + (int)night + 1;
        }

        /// <summary>Friday and Saturday: the two nights the room is worth being seen in.</summary>
        public static bool IsWeekend(BarNight night) =>
            night == BarNight.Friday || night == BarNight.Saturday;

        public static bool IsWeekend(int day) => IsWeekend(NightOf(day));

        /// <summary>
        /// The first day on or after <paramref name="from"/> that falls on this night. This is
        /// the one piece of arithmetic the story cannot do without: a beat that comes back
        /// "next Friday" has to land ON a Friday, and a beat pushed onto a Wednesday would
        /// simply never happen again.
        /// </summary>
        public static int NextNightOnOrAfter(int from, BarNight night)
        {
            if (from < 1) from = 1;
            int day = DayOf(WeekOf(from), night);
            return day >= from ? day : day + OpenNights;
        }

        /// <summary>What the plaque and the slip print. Kept here so the words on the screen
        /// and the rule behind them cannot drift apart.</summary>
        public static string Label(int day) => $"WEEK {WeekOf(day)} · {Name(NightOf(day))}";

        public static string Name(BarNight night)
        {
            switch (night)
            {
                case BarNight.Tuesday: return "TUESDAY";
                case BarNight.Wednesday: return "WEDNESDAY";
                case BarNight.Thursday: return "THURSDAY";
                case BarNight.Friday: return "FRIDAY";
                case BarNight.Saturday: return "SATURDAY";
                default: return "SUNDAY";
            }
        }

        /// <summary>Reads a night by name, for the data files. Null for anything else — the
        /// caller decides how loudly to complain, since it knows which file it came from.</summary>
        public static BarNight? Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            switch (name.Trim().ToLowerInvariant())
            {
                case "tuesday": return BarNight.Tuesday;
                case "wednesday": return BarNight.Wednesday;
                case "thursday": return BarNight.Thursday;
                case "friday": return BarNight.Friday;
                case "saturday": return BarNight.Saturday;
                case "sunday": return BarNight.Sunday;
                default: return null;
            }
        }

        private static int Index(int day) => Math.Max(1, day) - 1;
    }
}
