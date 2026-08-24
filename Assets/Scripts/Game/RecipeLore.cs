using System;
using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// The cookbook page's bottom matter (2026-08-24, the author: "yemek kitabı gibi
    /// altında detaylar bilgiler tarihi vs"): where a drink came from, and one line of
    /// the house's voice about it. DISPLAY CONTENT ONLY — no rule anywhere reads this,
    /// which is why it lives beside the menu art in Resources rather than in Core's
    /// recipe table: it can never fork the game by drifting.
    ///
    /// It still loads LOUDLY, the way all content does: a malformed file, a duplicate
    /// id or an id naming no catalogue recipe throws on first touch, and
    /// RecipeLoreTests pins the file to RecipeCatalog in both directions so a new
    /// recipe cannot ship without its page's bottom half.
    /// </summary>
    public static class RecipeLore
    {
        public sealed class Entry
        {
            public readonly string Origin;
            public readonly string Note;

            public Entry(string origin, string note)
            {
                Origin = origin;
                Note = note;
            }
        }

        [Serializable]
        private class FileDto
        {
            public EntryDto[] entries;
        }

        [Serializable]
        private class EntryDto
        {
            public string id;
            public string origin;
            public string note;
        }

        private static Dictionary<string, Entry> _all;

        /// <summary>Every entry by recipe id, parsed once. Throws on a bad file.</summary>
        public static IReadOnlyDictionary<string, Entry> All
        {
            get
            {
                if (_all == null)
                {
                    var text = Resources.Load<TextAsset>("Data/recipes_lore");
                    _all = Parse(text != null ? text.text : null);
                }
                return _all;
            }
        }

        /// <summary>This recipe's bottom matter, or null for a page that has none.</summary>
        public static Entry For(string recipeId)
        {
            Entry e;
            return recipeId != null && All.TryGetValue(recipeId, out e) ? e : null;
        }

        /// <summary>Parses and validates the lore file. Public so the tests can feed it
        /// hand-made JSON and pin the refusals.</summary>
        public static Dictionary<string, Entry> Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new FormatException("recipes_lore.json is missing or empty.");
            var dto = JsonUtility.FromJson<FileDto>(json);
            if (dto?.entries == null || dto.entries.Length == 0)
                throw new FormatException("recipes_lore.json carries no entries.");

            var known = new HashSet<string>();
            foreach (var r in RecipeCatalog.CreateDefault()) known.Add(r.Id);

            var all = new Dictionary<string, Entry>();
            foreach (var e in dto.entries)
            {
                if (string.IsNullOrWhiteSpace(e.id))
                    throw new FormatException("A lore entry has no id.");
                if (all.ContainsKey(e.id))
                    throw new FormatException($"Lore entry '{e.id}' appears twice.");
                if (!known.Contains(e.id))
                    throw new FormatException(
                        $"Lore entry '{e.id}' names no recipe in the catalogue.");
                if (string.IsNullOrWhiteSpace(e.note))
                    throw new FormatException($"Lore entry '{e.id}' has an empty note.");
                all.Add(e.id, new Entry(e.origin ?? string.Empty, e.note.Trim()));
            }
            return all;
        }
    }
}
