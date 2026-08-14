using System;

namespace LastCall.Core
{
    /// <summary>
    /// The bar's open nights. SUNDAY IS THE DAY OFF (2026-08-14, the author) and is not in
    /// the week at all — every night the game has is one of these six, and the calendar draws
    /// the seventh as the shutter it is.
    /// </summary>
    public enum BarNight
    {
        Monday, Tuesday, Wednesday, Thursday, Friday, Saturday,
    }

    /// <summary>
    /// The week (the author's calendar): seven days, six of them open — Monday through
    /// Saturday — and SUNDAY OFF (2026-08-14).
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
        /// <summary>Nights the bar opens in a week. Sunday is not one of them.</summary>
        public const int OpenNights = 6;

        /// <summary>Which night day N is. Day 1 is the first Monday — the week the bar
        /// opened in starts on one.</summary>
        public static BarNight NightOf(int day) => (BarNight)(Index(day) % OpenNights);

        /// <summary>Which week day N is in, counting from 1.</summary>
        public static int WeekOf(int day) => Index(day) / OpenNights + 1;

        /// <summary>The day number of a night in a week — the inverse of the two above.</summary>
        public static int DayOf(int week, BarNight night)
        {
            if (week < 1) throw new ArgumentOutOfRangeException(nameof(week), "The bar opened in week 1.");
            return (week - 1) * OpenNights + (int)night + 1;
        }

        /// <summary>
        /// THE NIGHT A NAME COMES — and now the ONLY one (2026-08-14, the author: "her
        /// cumartesi bir hikaye müşterisi gelecek"). Saturday is the week's own promise: the
        /// calendar says so before any beat is scheduled, so the days between are days the
        /// player is counting towards something rather than days that merely pass.
        ///
        /// It began as presentation — a star on the marquee — while the RULE still allowed
        /// Friday or Saturday. A wall that advertises one night while the arc may use another
        /// is a wall that lies, and this game has a standing rule against exactly that: the
        /// strip cannot say Friday while the story thinks Saturday, because neither of them
        /// does its own arithmetic. So the marker became the rule (see <see cref="StoryBeat"/>);
        /// Friday keeps its place in <see cref="IsWeekend"/>, which is the ECONOMY's busy
        /// weekend and a different question entirely.
        /// </summary>
        public const BarNight VipNight = BarNight.Saturday;

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
                case BarNight.Monday: return "MONDAY";
                case BarNight.Tuesday: return "TUESDAY";
                case BarNight.Wednesday: return "WEDNESDAY";
                case BarNight.Thursday: return "THURSDAY";
                case BarNight.Friday: return "FRIDAY";
                default: return "SATURDAY";
            }
        }

        /// <summary>Reads a night by name, for the data files. Null for anything else — the
        /// caller decides how loudly to complain, since it knows which file it came from.</summary>
        public static BarNight? Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            switch (name.Trim().ToLowerInvariant())
            {
                case "monday": return BarNight.Monday;
                case "tuesday": return BarNight.Tuesday;
                case "wednesday": return BarNight.Wednesday;
                case "thursday": return BarNight.Thursday;
                case "friday": return BarNight.Friday;
                case "saturday": return BarNight.Saturday;
                default: return null;   // sunday included: the bar is shut
            }
        }

        /// <summary>The day the shutter stays down. It has no night of its own — it is drawn
        /// on the calendar and skipped by the clock (2026-08-14).</summary>
        public const string DayOffName = "SUNDAY";

        /// <summary>The seven columns a calendar draws, in order, with the closed one last.
        /// Presentation asks for this; the rules only ever count the six.</summary>
        public static readonly string[] WeekColumns =
            { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };

        private static int Index(int day) => Math.Max(1, day) - 1;
    }
}
