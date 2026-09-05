using System.Collections.Generic;

namespace LastCall.Core
{
    // TycoonRun, part Lessons: the host speaks up the first time each thing happens
    // (GDD 26 §1b "the teacher", §10 `lessons`; PLAN_last_call S5).
    //
    // The tutorial this project deleted in the 2026-08-07 sweep comes back as a PERSON:
    // Ece says the thing once, the moment it is first true, in her own words from the
    // story file. Core owns the CONDITIONS — a small fixed table of things the run can
    // actually observe (StoryCue) — and the data picks one by name and supplies the words.
    // Nothing here renders, waits, or blocks: a lesson that is due sits in LessonDue until
    // whoever is drawing the bar says it has been heard, and a run with no story (the
    // bench, the sim, the older tests) never has one.
    public sealed partial class TycoonRun
    {
        private readonly Queue<StoryLesson> _lessons = new Queue<StoryLesson>();

        /// <summary>Whether anybody's card has been read yet this run — the fact the
        /// FirstLicence cue is about. Observed off the visits, since reading a card is the
        /// visit's own verb and the run is never told directly.</summary>
        private bool _anyLicenceRead;

        /// <summary>
        /// The lesson the host is waiting to say, or null. Lessons queue in the order their
        /// moments came, one at a time — two conditions that come true on the same tick are
        /// two things to say, not one plate with both on it.
        /// </summary>
        public StoryLesson LessonDue => _lessons.Count > 0 ? _lessons.Peek() : null;

        /// <summary>The player has read it: the next one, if any, is due. Harmless when
        /// nothing is waiting, so a UI can call it on any key without checking first.</summary>
        public void HeardLesson()
        {
            if (_lessons.Count > 0) _lessons.Dequeue();
        }

        /// <summary>The condition behind <paramref name="cue"/> just came true. Once per run.</summary>
        private void Teach(StoryCue cue)
        {
            if (Story == null) return;
            var lesson = Story.Learn(cue);
            if (lesson != null) _lessons.Enqueue(lesson);
        }

        /// <summary>
        /// The moments a fresh door can be the first of: the run's first night, and a written
        /// guest coming THIS WEEK for a style the shelf has not got (GDD 26 §4 — the warning
        /// the asks-one-at-a-time rule makes necessary). Asked when a day opens; the first
        /// night's is asked once, from the constructor, so day one says it before the first
        /// customer is through the door.
        /// </summary>
        private void TeachAtOpen()
        {
            if (Story == null) return;
            if (Day == 1) Teach(StoryCue.FirstNight);
            if (ShelfShortOfTheAsk()) Teach(StoryCue.CannotPourTheAsk);
        }

        /// <summary>
        /// Is a written guest coming THIS calendar week for a style the shelf cannot pour?
        /// Looks along the arc from the armed beat, because the one that is armed may be the
        /// host's own quiet night and the guest who needs bourbon is Saturday's. The tier
        /// counts: a beat that wants a tier-2 bourbon is not answered by the well bottle
        /// (GDD 26 §4, `needTier`).
        /// </summary>
        private bool ShelfShortOfTheAsk()
        {
            if (Story == null) return false;
            int week = BarCalendar.WeekOf(Day);
            foreach (var beat in Story.Ahead)
            {
                int night = ReferenceEquals(beat, Story.Current) ? Story.DueDay : beat.Day;
                if (BarCalendar.WeekOf(night) > week) return false;    // next week's worry, not tonight's
                if (beat.NeedStyle == null) continue;
                var bottle = Market.FindByStyle(_shelf, beat.NeedStyle);
                return bottle == null || (bottle.Ingredient.Info?.Tier ?? 1) < beat.NeedTier;
            }
            return false;
        }

        /// <summary>
        /// The conditions that are read off the floor rather than off a verb, checked once a
        /// tick: somebody waiting to be asked while no card has ever been read, and two
        /// spirits standing in the tin with no verb on them (Core's own MixRequired).
        /// </summary>
        private void WatchForLessons()
        {
            if (Story == null) return;
            if (!Story.HasTaught(StoryCue.FirstLicence))
            {
                bool waitingUnread = false;
                foreach (var visit in Floor.Seated)
                {
                    if (visit.IdInspected) _anyLicenceRead = true;
                    else if (visit.State == VisitState.Waiting && visit.HasOrdered) waitingUnread = true;
                }
                // A card read before the first lesson could land means the player already
                // knows the thing it teaches; the moment is spent without a word.
                if (_anyLicenceRead) Story.Learn(StoryCue.FirstLicence);
                else if (waitingUnread) Teach(StoryCue.FirstLicence);
            }
            if (!Story.HasTaught(StoryCue.TwoSpiritsInTheTin) && MixRequired && !IsMixed)
                Teach(StoryCue.TwoSpiritsInTheTin);
        }
    }
}
