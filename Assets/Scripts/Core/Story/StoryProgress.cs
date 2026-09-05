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

        /// <summary>The beats still to come, the armed one first. What the host looks along
        /// when she warns about the shelf (GDD 26 §4): the next guest who needs a style may
        /// be two nights behind the one that is armed.</summary>
        public IEnumerable<StoryBeat> Ahead
        {
            get { for (int i = _at; i < Arc.Beats.Count; i++) yield return Arc.Beats[i]; }
        }

        /// <summary>The night <see cref="Current"/> is expected on. Starts at the beat's own
        /// day and only ever moves later.</summary>
        public int DueDay { get; private set; }

        /// <summary>Beats served the way they were asked for.</summary>
        public int Kept { get; private set; }

        /// <summary>Nights the ask was fumbled, refused, or left waiting. Not failures — the
        /// same beat comes back — but the arc remembers, and the ending will read them.</summary>
        public int Missed { get; private set; }

        /// <summary>
        /// Nights the guest came, sat, and did not order because the bar had not reached their
        /// rung (GDD 26 §12). Counted APART from <see cref="Missed"/> on purpose: one of those
        /// is the player getting a drink wrong and the other is the player not being there
        /// yet, and an ending that cannot tell them apart would tell somebody who never
        /// fumbled a serve that they kept letting people down.
        /// </summary>
        public int TurnedAway { get; private set; }

        /// <summary>
        /// Has the beat that is armed already been SAT — asked for and missed, or come and
        /// turned away — so that its ask is a thing the player has heard? Between visits the
        /// ask stands, and the book shows it as an open tab (GDD 26 §5); before the first
        /// visit there is nothing to show but the host's warning. Cleared when the arc moves on.
        /// </summary>
        public bool CurrentAsked { get; private set; }

        public StoryProgress(StoryArc arc)
        {
            Arc = arc ?? throw new ArgumentNullException(nameof(arc));
            DueDay = arc.Opener.Day;
        }

        // ── the host's lessons (GDD 26 §1b/§10, PLAN_last_call S5) ────────────────
        // What the host has already said this run. A lesson fires ONCE per run, the moment
        // its condition is first true, and never again — which is bookkeeping about the RUN
        // and so lives here, beside the arc's own clock, not in the shared content.

        private readonly HashSet<StoryCue> _taught = new HashSet<StoryCue>();

        /// <summary>Has the moment for this cue already come and gone this run?</summary>
        public bool HasTaught(StoryCue cue) => _taught.Contains(cue);

        /// <summary>
        /// The condition behind <paramref name="cue"/> just became true. Returns the lesson
        /// the host has for it the FIRST time, and null every time after — or null at once
        /// when the arc has nothing written for that cue, in which case the moment is still
        /// spent: a cue the writer left silent is a decision, not a retry.
        /// </summary>
        public StoryLesson Learn(StoryCue cue)
        {
            if (!_taught.Add(cue)) return null;
            foreach (var lesson in Arc.Lessons)
                if (lesson.Cue == cue) return lesson;
            return null;
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

        /// <summary>
        /// Has the bar earned tonight's guest? The gate is asked SEPARATELY from the clock
        /// (GDD 26 §12) and never folded into <see cref="IsDueOn"/>, because a guest whose
        /// rung has not been reached still comes: they sit, they say what the bar is short of,
        /// and they leave without ordering. Folding the two would make a locked night into a
        /// night where nobody turns up, which teaches nothing and looks like a bug.
        /// </summary>
        public bool GateOpenFor(double stars) =>
            Current == null || stars + 1e-9 >= Current.RequiresStars;

        /// <summary>
        /// They came, they saw the bar was not there yet, and they went home. The beat STANDS
        /// — same as a miss, and on the same return clock — but it is written in its own
        /// column: nothing was fumbled here.
        /// </summary>
        public void RecordTurnedAway(int day)
        {
            if (Current == null) return;
            TurnedAway++;
            CurrentAsked = true;
            PushBack(day);
        }

        /// <summary>They got what they asked for. The arc moves on, and the next beat cannot
        /// land tonight — one last call a night, always.</summary>
        /// <summary>
        /// Which written nights were served the way they were asked for, by id. `Kept` counts
        /// them; this remembers WHICH — because the author's third kind of lock is a bottle
        /// you earn from a person rather than from a number (GDD 26 §12.2 step 4), and a
        /// counter cannot answer "did they keep Ece's second night".
        /// </summary>
        private readonly HashSet<string> _keptIds = new HashSet<string>(StringComparer.Ordinal);

        public bool WasKept(string beatId) =>
            !string.IsNullOrEmpty(beatId) && _keptIds.Contains(beatId);

        public void RecordServed(int day)
        {
            if (Current == null) return;
            Kept++;
            _keptIds.Add(Current.Id);
            _at++;
            CurrentAsked = false;
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
            CurrentAsked = true;
            PushBack(day);
        }

        /// <summary>The return clock, shared by a miss and a turn-away — one piece of
        /// arithmetic, so the two cannot drift apart and strand a beat on a Wednesday.</summary>
        private void PushBack(int day)
        {
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
