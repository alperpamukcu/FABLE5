using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// The written nights, in the order they happen (GDD 26 §11). The arc is CONTENT: it is
    /// built once from the story file, it never changes, and every run — including the two
    /// hundred the simulator plays at once — reads the same one. A run's place in it is
    /// <see cref="StoryProgress"/>, held by the run and nowhere else.
    ///
    /// The chain is checked at construction, loudly, like every other content file in this
    /// project: a beat that names a night that does not exist, a beat nothing leads to, a
    /// loop, two beats on one night. All of those are edits somebody made in a text file, and
    /// all of them would otherwise show up as a customer who never walks in.
    /// </summary>
    public sealed class StoryArc
    {
        private readonly List<StoryBeat> _order;
        private readonly Dictionary<string, StoryBeat> _byId;

        /// <summary>The beats in playing order, starting with the opener.</summary>
        public IReadOnlyList<StoryBeat> Beats => _order;

        /// <summary>What the host says the first time the game does something (GDD 26 §10).
        /// Part of the arc because they are the same voice and the same file.</summary>
        public IReadOnlyList<StoryLesson> Lessons { get; }

        /// <summary>The first night that is written — Ece's, by design (GDD 26 §1b).</summary>
        public StoryBeat Opener => _order[0];

        public StoryArc(IReadOnlyList<StoryBeat> beats, IReadOnlyList<StoryLesson> lessons = null)
        {
            Lessons = lessons != null
                ? new List<StoryLesson>(lessons)
                : (IReadOnlyList<StoryLesson>)Array.Empty<StoryLesson>();
            if (beats == null) throw new ArgumentNullException(nameof(beats));
            if (beats.Count == 0)
                throw new ArgumentException("An arc with no beats is not a story.", nameof(beats));

            _byId = new Dictionary<string, StoryBeat>(beats.Count, StringComparer.Ordinal);
            foreach (var beat in beats)
            {
                if (beat == null) throw new ArgumentException("A beat is missing.", nameof(beats));
                if (_byId.ContainsKey(beat.Id))
                    throw new ArgumentException($"Two beats are called '{beat.Id}'.", nameof(beats));
                _byId.Add(beat.Id, beat);
            }

            // Walk the chain from the first written beat. The FILE's order is a courtesy; the
            // 'next' field is the truth, because that is the field a writer edits when they
            // insert a night in the middle.
            _order = new List<StoryBeat>(beats.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var walk = beats[0];
            while (walk != null)
            {
                if (!seen.Add(walk.Id))
                    throw new ArgumentException(
                        $"The arc goes in a circle: '{walk.Id}' happens twice.", nameof(beats));
                if (_order.Count > 0)
                {
                    var before = _order[_order.Count - 1];
                    if (walk.Day <= before.Day)
                        throw new ArgumentException(
                            $"'{walk.Id}' is written for day {walk.Day} but follows '{before.Id}' " +
                            $"on day {before.Day} — the bar has one last call a night.", nameof(beats));
                }
                _order.Add(walk);

                if (walk.NextId == null) break;
                if (!_byId.TryGetValue(walk.NextId, out var next))
                    throw new ArgumentException(
                        $"'{walk.Id}' leads to '{walk.NextId}', which is not written.", nameof(beats));
                walk = next;
            }

            if (_order.Count != beats.Count)
                foreach (var beat in beats)
                    if (!seen.Contains(beat.Id))
                        throw new ArgumentException(
                            $"'{beat.Id}' is written but nothing leads to it.", nameof(beats));
        }

        public StoryBeat BeatNamed(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _byId.TryGetValue(id, out var beat) ? beat : null;
        }

        /// <summary>Everyone the arc is about, in the order they first appear.</summary>
        public IReadOnlyList<StoryCharacter> Cast
        {
            get
            {
                var cast = new List<StoryCharacter>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var beat in _order)
                    if (seen.Add(beat.Who.Id)) cast.Add(beat.Who);
                return cast;
            }
        }
    }
}
