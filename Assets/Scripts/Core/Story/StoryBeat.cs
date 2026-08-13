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

        /// <summary>What they order. Resolved against the real catalogue when the arc is
        /// built, so a beat can never name a drink that does not exist.</summary>
        public RecipeDefinition Drink { get; }

        /// <summary>The earliest night they can walk in on.</summary>
        public int Day { get; }

        /// <summary>How long they will sit for it. From the file, never from the dice — a
        /// scripted night has to play the same way twice.</summary>
        public double PatienceSeconds { get; }

        /// <summary>The style the market must sell before this can be poured ("bourbon"), or
        /// null when the ask needs nothing the bar has not got. The ask ALWAYS names what is
        /// missing (GDD 26 §4); this is the field the words are checked against.</summary>
        public string NeedStyle { get; }

        /// <summary>The tier that style has to reach. 0 means any bottle of it will do.</summary>
        public int NeedTier { get; }

        /// <summary>A recipe id the ASK itself hands over — the drink the player cannot make
        /// yet, given as a page so the ask is a job and not a wall (GDD 26 §4).</summary>
        public string GrantsRecipeOnAsk { get; }

        /// <summary>Nights before they try again after a miss. Never zero: a beat that
        /// re-arms tonight would seat the same person twice in one closing.</summary>
        public int ReturnsAfterDays { get; }

        /// <summary>The beat that follows this one, or null when the arc ends here.</summary>
        public string NextId { get; }

        public StoryLines Lines { get; }
        public StoryReward Reward { get; }

        public StoryBeat(string id, StoryCharacter who, RecipeDefinition drink, int day,
            double patienceSeconds, StoryLines lines = null, StoryReward reward = null,
            string needStyle = null, int needTier = 0, string grantsRecipeOnAsk = null,
            int returnsAfterDays = 1, string nextId = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A beat needs an id.", nameof(id));
            if (who == null)
                throw new ArgumentNullException(nameof(who), $"beat '{id}' has nobody in it");
            if (drink == null)
                throw new ArgumentNullException(nameof(drink), $"beat '{id}' asks for nothing");
            if (day < 1)
                throw new ArgumentOutOfRangeException(nameof(day), $"beat '{id}' happens on day {day}");
            if (patienceSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(patienceSeconds),
                    $"beat '{id}' would leave before it was read");
            if (needTier < 0)
                throw new ArgumentOutOfRangeException(nameof(needTier));
            if (returnsAfterDays < 1)
                throw new ArgumentOutOfRangeException(nameof(returnsAfterDays),
                    $"beat '{id}' would come back the same night it left");

            Id = id;
            Who = who;
            Drink = drink;
            Day = day;
            PatienceSeconds = patienceSeconds;
            Lines = lines ?? StoryLines.Silent;
            Reward = reward ?? StoryReward.None;
            NeedStyle = string.IsNullOrWhiteSpace(needStyle) ? null : needStyle;
            NeedTier = needTier;
            GrantsRecipeOnAsk = string.IsNullOrWhiteSpace(grantsRecipeOnAsk) ? null : grantsRecipeOnAsk;
            ReturnsAfterDays = returnsAfterDays;
            NextId = string.IsNullOrWhiteSpace(nextId) ? null : nextId;
        }

        public override string ToString() => $"{Id}: {Who.Name} wants {Drink.Name} (day {Day})";
    }
}
