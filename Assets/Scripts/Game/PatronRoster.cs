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
        /// <summary>
        /// THE LICENCE'S LIMITS (2026-09-22, the author's eighth list: "Kimlikte yazan isimler yazılar çok uzun
        /// olmamalı sayfa düzeni için bir karakter sınırı koymalıyız ve o karakter sınırına göre ülke isimleri veya
        /// müşteri isimleri seçmeliyiz"). The card prints the first name and the surname in two boxes side by side
        /// and the nationality in a half-width one, each at the heading face's sixteen units a letter; these are
        /// the letters those boxes hold. A name over them is refused at load rather than clipped on the card, and
        /// the nationality words are held to theirs by the string-table test (LicenceTextTests).
        /// </summary>
        public const int FirstNameMax = 10, SurnameMax = 11, NationalityMax = 12;

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

        /// <summary>The first word of the name: the card's NAME box.</summary>
        public string First
        {
            get { int at = Name.IndexOf(' '); return at < 0 ? Name : Name.Substring(0, at); }
        }

        /// <summary>Everything after the first word: the card's SURNAME box (empty for a one-word name).</summary>
        public string Surname
        {
            get { int at = Name.IndexOf(' '); return at < 0 ? string.Empty : Name.Substring(at + 1); }
        }

        public Papers(string slug, string name, int age, string country, string iso, bool young = false)
        {
            Young = young;
            Slug = slug ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"Papers for '{Slug}' have no name.", nameof(name));
            int space = name.IndexOf(' ');
            string first = space < 0 ? name : name.Substring(0, space);
            string surname = space < 0 ? string.Empty : name.Substring(space + 1);
            if (first.Length > FirstNameMax)
                throw new ArgumentException(
                    $"'{name}': the first name is {first.Length} letters and the licence prints {FirstNameMax}.", nameof(name));
            if (surname.Length > SurnameMax)
                throw new ArgumentException(
                    $"'{name}': the surname is {surname.Length} letters and the licence prints {SurnameMax}.", nameof(name));
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
