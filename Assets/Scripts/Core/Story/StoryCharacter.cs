using System;

namespace LastCall.Core
{
    /// <summary>
    /// Somebody the arc is written about (GDD 26 §8). A story character is a FACE plus the
    /// papers that face already carries: the cast file is the one place a person's name, age
    /// and citizenship live, so the story names a look and the layer that loads it hands the
    /// papers down. That is the same law the licence learned the hard way on 2026-08-10 — the
    /// papers belong to the face, not to whatever sent them through the door.
    ///
    /// Core needs exactly enough of a person to sit them on a stool and print their card.
    /// Everything else about them — the portrait, the plate, the voice — is the UI's.
    /// </summary>
    public sealed class StoryCharacter
    {
        /// <summary>How the arc refers to them ("collector"), not what the card says.</summary>
        public string Id { get; }

        /// <summary>The face they wear: a slug in the cast file, which is also an art look.</summary>
        public string Look { get; }

        public string Name { get; }
        public int Age { get; }

        /// <summary>Where the licence says they are from.</summary>
        public string Hometown { get; }

        /// <summary>The host works the shift and frames the beats; guests come in through the
        /// door and are the ones being served (GDD 26 §1b). Ece is the only host there is.</summary>
        public bool IsHost { get; }

        /// <summary>One line about who they are, for the book and for whoever writes the next
        /// beat. Never printed on the licence.</summary>
        public string Blurb { get; }

        public StoryCharacter(string id, string look, string name, int age = 30,
            string hometown = null, bool isHost = false, string blurb = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A story character needs an id.", nameof(id));
            if (string.IsNullOrWhiteSpace(look))
                throw new ArgumentException($"'{id}' has no face — a story character IS a look.", nameof(look));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"'{id}' has no papers; the plate would say nothing.", nameof(name));
            if (age < 18)
                throw new ArgumentOutOfRangeException(nameof(age), $"'{id}' cannot be served at {age}.");

            Id = id;
            Look = look;
            Name = name;
            Age = age;
            Hometown = string.IsNullOrWhiteSpace(hometown) ? "this side of town" : hometown;
            IsHost = isHost;
            Blurb = blurb ?? string.Empty;
        }

        public override string ToString() => $"{Name} ({Look})";
    }
}
