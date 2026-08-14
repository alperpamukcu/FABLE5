using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Where one run has got to in the arc (GDD 26 §5). The beats are content and are shared;
    /// this is the part that moves, and every run owns its own — which is the whole reason the
    /// two are separate classes. A hundred simulated bars can be on a hundred different nights
    /// of the same story.
    ///
    /// The clock is the only rule here: a beat is ARMED for a night, and a night that misses
    /// it — served wrong, declined, or walked out on — pushes it back by its own return time
    /// instead of failing it. No dead ends (PLAN_last_call, standing rule 2).
    /// </summary>
    public sealed class StoryProgress
    {
        private readonly Dictionary<string, RegularState> _people =
            new Dictionary<string, RegularState>(StringComparer.Ordinal);
        private int _at;

        public StoryArc Arc { get; }

        /// <summary>The beat waiting to be played, or null once the arc has been walked.</summary>
        public StoryBeat Current => _at < Arc.Beats.Count ? Arc.Beats[_at] : null;

        public bool IsFinished => Current == null;

        /// <summary>The night <see cref="Current"/> is expected on. Starts at the beat's own
        /// day and only ever moves later.</summary>
        public int DueDay { get; private set; }

        /// <summary>Beats served the way they were asked for.</summary>
        public int Kept { get; private set; }

        /// <summary>Nights the ask was fumbled, refused, or left waiting. Not failures — the
        /// same beat comes back — but the arc remembers, and the ending will read them.</summary>
        public int Missed { get; private set; }

        public StoryProgress(StoryArc arc)
        {
            Arc = arc ?? throw new ArgumentNullException(nameof(arc));
            DueDay = arc.Opener.Day;
        }

        /// <summary>
        /// Is tonight the night? Two things have to be true: the clock has reached the beat,
        /// and this is the night of the week that beat comes in on (GDD 26 §2b). A run past
        /// the due day still gets it — the story must never be lost to arithmetic — but it
        /// waits for the right night to do it, which is what makes a name on the plaque
        /// something the player can count towards.
        /// </summary>
        public bool IsDueOn(int day) =>
            Current != null && day >= DueDay && BarCalendar.NightOf(day) == Current.Night;

        /// <summary>They got what they asked for. The arc moves on, and the next beat cannot
        /// land tonight — one last call a night, always.</summary>
        public void RecordServed(int day)
        {
            if (Current == null) return;
            Kept++;
            _at++;
            DueDay = Current != null
                ? Math.Max(Current.Day, BarCalendar.NextNightOnOrAfter(day + 1, Current.Night))
                : day + 1;
        }

        /// <summary>
        /// Wrong drink, honest no, or nobody came to the stool in time. The beat stands and
        /// comes back on its own night, a week or more later — NOT on "today plus two", which
        /// would push it onto a Wednesday it can never happen on and freeze the arc for the
        /// rest of the run. This is the one line the weekend rule can be got wrong in.
        /// </summary>
        public void RecordMissed(int day)
        {
            if (Current == null) return;
            Missed++;
            int back = BarCalendar.DayOf(
                BarCalendar.WeekOf(day) + Current.ReturnsAfterWeeks, Current.Night);
            if (back <= day) back += BarCalendar.OpenNights;   // missed late in its own week
            DueDay = Math.Max(Current.Day, back);
        }

        /// <summary>
        /// The run's memory of a story person. One <see cref="RegularState"/> per character,
        /// made the first night they come in and kept afterwards — so a collector on his
        /// second visit is somebody the bar has met, with the visits to prove it, exactly
        /// like anyone else who comes back.
        /// </summary>
        public RegularState PersonFor(StoryCharacter who)
        {
            if (who == null) throw new ArgumentNullException(nameof(who));
            if (_people.TryGetValue(who.Id, out var person)) return person;
            // The archetype slot says how they came in, and they came in as the last call
            // (2026-08-10: the archetype keeps only what it is actually about). Their face
            // and their papers come off the look, like everybody else's.
            person = new RegularState("story_" + who.Id, who.Name, "story", who.Age, who.Hometown);
            _people.Add(who.Id, person);
            return person;
        }
    }
}
