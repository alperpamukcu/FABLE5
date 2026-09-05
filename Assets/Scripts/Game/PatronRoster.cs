using System;
using System.Collections.Generic;

namespace LastCall.Game
{
    /// <summary>
    /// What a face's licence says: the name, the age, the country and the flag beside it.
    ///
    /// This lives in the GAME layer and not in Core for the same reason <see cref="StageSlot"/>
    /// does — it is presentation. No rule in this game asks how old a drinker is or where they
    /// are from; the licence PRINTS it, the receipt shortens it, and the guide lists it. Core
    /// knows a customer as a <c>RegularState</c> and nothing here reaches it.
    /// </summary>
    public sealed class Papers
    {
        /// <summary>The LOOK these papers belong to — the sprite folder's name, which is the
        /// key the licence, the receipt and the guide all agree on. Empty is the fallback
        /// for a face with no papers of its own.</summary>
        public string Slug { get; }

        public string Name { get; }
        public int Age { get; }
        public string Country { get; }

        /// <summary>Two letters, lower case: the flag is drawn from <c>fl_{iso}</c>.</summary>
        public string Iso { get; }

        /// <summary>Could this face pass for nineteen (GDD 28 §3.1)? A visit the room may
        /// read as young draws from these — every minor, and the adults who look it.</summary>
        public bool Young { get; }

        public Papers(string slug, string name, int age, string country, string iso, bool young = false)
        {
            Young = young;
            Slug = slug ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"Papers for '{Slug}' have no name.", nameof(name));
            if (age <= 0)
                throw new ArgumentOutOfRangeException(nameof(age), $"'{name}' has no age on their licence.");
            Name = name;
            Age = age;
            Country = country ?? string.Empty;
            Iso = iso ?? string.Empty;
        }

        public override string ToString() => $"{Name} ({Age}, {Country})";
    }

    /// <summary>
    /// Everyone the bar can draw, by face. One lookup, one fallback, one place that knows a
    /// slug with no papers is answered with the house's own (the empty-slug entry) rather
    /// than with nothing.
    ///
    /// It was a Dictionary in the middle of TycoonHud until 2026-08-12: thirty people written
    /// in C#, which meant a writer could not add one and the story's characters could not
    /// share the cast's papers (PLAN_last_call S0).
    /// </summary>
    public sealed class PatronRoster
    {
        private readonly Dictionary<string, Papers> _bySlug;

        public IReadOnlyList<Papers> All { get; }

        public PatronRoster(IReadOnlyList<Papers> papers)
        {
            if (papers == null || papers.Count == 0)
                throw new ArgumentException("The bar needs at least one set of papers.", nameof(papers));
            _bySlug = new Dictionary<string, Papers>(papers.Count);
            foreach (var person in papers)
            {
                if (_bySlug.ContainsKey(person.Slug))
                    throw new ArgumentException(
                        $"Two people claim the look '{person.Slug}' ({_bySlug[person.Slug].Name} and {person.Name}).",
                        nameof(papers));
                _bySlug[person.Slug] = person;
            }
            All = papers;
        }

        /// <summary>The papers for a look, or null when nobody has claimed it — the caller
        /// decides what an unknown face is called, because the licence and the receipt answer
        /// that differently.</summary>
        public Papers For(string slug) =>
            slug != null && _bySlug.TryGetValue(slug, out var papers) ? papers : null;
    }
}
