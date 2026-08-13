using System;

namespace LastCall.Core
{
    /// <summary>
    /// One written night (GDD 26 §2): who comes in after the door is shut, what they ask for,
    /// what the bar is missing if it cannot pour it, and what keeping the beat is worth.
    ///
    /// A beat is CONTENT — it is built once from the story file and shared by every run that
    /// plays it. Where a run has got to lives in <see cref="StoryProgress"/>, which is why
    /// nothing here changes after it is constructed; two hundred simulated runs read these
    /// same objects at the same time.
    /// </summary>
    public sealed class StoryBeat
    {
        /// <summary>The arc's name for this night ("collector_1"). Never shown.</summary>
        public string Id { get; }

        public StoryCharacter Who { get; }

        /// <summary>What they want, which is a RUN of drinks against one clock (GDD 26 §4).
        /// Resolved against the real catalogue when the arc is built, so a beat can never ask
        /// for a drink that does not exist.</summary>
        public StoryTrial Trial { get; }

        /// <summary>The drink the night is remembered by — the first thing they ask for.</summary>
        public RecipeDefinition Drink => Trial.Headline;

        /// <summary>Which week they are written for, counting from the week the bar opened.</summary>
        public int Week { get; }

        /// <summary>
        /// The night of that week they come in on — and for a guest that is FRIDAY or SATURDAY,
        /// refused otherwise (GDD 26 §2b). Nobody important turns up on a Wednesday, and the
        /// rule is worth more than the exception: it makes the days between a beat into the
        /// days you go and buy what the last one asked for. The host is not a guest — Ece
        /// works the shift, so her nights are the arc's to choose.
        /// </summary>
        public BarNight Night { get; }

        /// <summary>The earliest night they can walk in on, off the calendar.</summary>
        public int Day { get; }

        /// <summary>How long the whole trial gets, off the trial. From the file, never from
        /// the dice — a scripted night has to play the same way twice.</summary>
        public double PatienceSeconds => Trial.Seconds;

        /// <summary>
        /// The style the bar has to have in before this night ("bourbon"), or null when it
        /// needs nothing it has not got. Since the asks are revealed ONE AT A TIME (§4), this
        /// is what the shopping week is built on: it is what the HOST warns about days early
        /// (§1b), not what the post-it says — the post-it only ever shows the drink in hand.
        /// </summary>
        public string NeedStyle { get; }

        /// <summary>The tier that style has to reach. 0 means any bottle of it will do.</summary>
        public int NeedTier { get; }

        /// <summary>A recipe id the ASK itself hands over — the drink the player cannot make
        /// yet, given as a page so the ask is a job and not a wall (GDD 26 §4).</summary>
        public string GrantsRecipeOnAsk { get; }

        /// <summary>Weeks before they try again after a miss — on their own night, so a beat
        /// missed on a Friday comes back on a Friday. Never zero: a beat that re-armed the same
        /// night would seat the same person twice in one closing.</summary>
        public int ReturnsAfterWeeks { get; }

        /// <summary>The beat that follows this one, or null when the arc ends here.</summary>
        public string NextId { get; }

        public StoryLines Lines { get; }
        public StoryReward Reward { get; }

        public StoryBeat(string id, StoryCharacter who, StoryTrial trial, int week,
            BarNight night, StoryLines lines = null,
            StoryReward reward = null, string needStyle = null, int needTier = 0,
            string grantsRecipeOnAsk = null, int returnsAfterWeeks = 1, string nextId = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A beat needs an id.", nameof(id));
            if (who == null)
                throw new ArgumentNullException(nameof(who), $"beat '{id}' has nobody in it");
            if (trial == null)
                throw new ArgumentNullException(nameof(trial), $"beat '{id}' asks for nothing");
            if (week < 1)
                throw new ArgumentOutOfRangeException(nameof(week), $"beat '{id}' happens in week {week}");
            if (!who.IsHost && !BarCalendar.IsWeekend(night))
                throw new ArgumentException(
                    $"beat '{id}' brings {who.Name} in on a {BarCalendar.Name(night)} — a guest " +
                    "comes at the weekend (GDD 26 §2b); only the house works the quiet nights.",
                    nameof(night));
            if (needTier < 0)
                throw new ArgumentOutOfRangeException(nameof(needTier));
            if (returnsAfterWeeks < 1)
                throw new ArgumentOutOfRangeException(nameof(returnsAfterWeeks),
                    $"beat '{id}' would come back the same night it left");

            Id = id;
            Who = who;
            Trial = trial;
            Week = week;
            Night = night;
            Day = BarCalendar.DayOf(week, night);
            Lines = lines ?? StoryLines.Silent;
            Reward = reward ?? StoryReward.None;
            NeedStyle = string.IsNullOrWhiteSpace(needStyle) ? null : needStyle;
            NeedTier = needTier;
            GrantsRecipeOnAsk = string.IsNullOrWhiteSpace(grantsRecipeOnAsk) ? null : grantsRecipeOnAsk;
            ReturnsAfterWeeks = returnsAfterWeeks;
            NextId = string.IsNullOrWhiteSpace(nextId) ? null : nextId;
        }

        public override string ToString() =>
            $"{Id}: {Who.Name} wants {Trial} ({BarCalendar.Label(Day)})";
    }
}
