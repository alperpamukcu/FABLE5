using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// One language's lines by key (2026-09-13, localization L0). Built from a
    /// <c>Resources/Data/loc/&lt;code&gt;.json</c> file by the Game layer; this refuses what would
    /// otherwise show up as a wrong word on screen weeks later: an unknown language, an empty or
    /// repeated key, a key with spaces, a plural suffix that is not a CLDR category, a brace that
    /// never closes, a placeholder that is not <c>{lower_snake}</c>.
    /// </summary>
    public sealed class StringTable
    {
        public const char PluralMark = '#';

        private readonly Dictionary<string, string> _texts;

        public string Code { get; }
        public int Count => _texts.Count;
        public IEnumerable<string> Keys => _texts.Keys;

        public StringTable(string code, IEnumerable<KeyValuePair<string, string>> entries)
        {
            if (Languages.Find(code) == null)
                throw new ArgumentException($"'{code}' is not a shipped language (Core/Text/Languages.cs).");
            Code = code;
            _texts = new Dictionary<string, string>(StringComparer.Ordinal);
            if (entries == null) return;
            foreach (var entry in entries)
            {
                string key = entry.Key;
                if (string.IsNullOrWhiteSpace(key))
                    throw new ArgumentException("a line has an empty key.");
                foreach (char c in key)
                    if (char.IsWhiteSpace(c))
                        throw new ArgumentException($"key '{key}' has a space in it.");
                int mark = key.IndexOf(PluralMark);
                if (mark >= 0 && (mark == 0 || !PluralRules.TryParseSuffix(key.Substring(mark + 1), out _)))
                    throw new ArgumentException($"key '{key}': after '#' comes one of zero, one, two, few, many, other.");
                if (entry.Value == null)
                    throw new ArgumentException($"key '{key}' has no text.");
                if (_texts.ContainsKey(key))
                    throw new ArgumentException($"key '{key}' appears twice.");
                string problem = PlaceholderProblem(entry.Value);
                if (problem != null)
                    throw new ArgumentException($"key '{key}': {problem}");
                _texts.Add(key, entry.Value);
            }
        }

        public bool TryGet(string key, out string text) => _texts.TryGetValue(key, out text);

        /// <summary>The key without its plural suffix: "night.count#few" → "night.count".</summary>
        public static string BaseKey(string key)
        {
            int mark = key.IndexOf(PluralMark);
            return mark < 0 ? key : key.Substring(0, mark);
        }

        /// <summary>The placeholder names in a text, in order of first appearance.</summary>
        public static IReadOnlyList<string> Placeholders(string text)
        {
            var names = new List<string>();
            int i = 0;
            while ((i = text.IndexOf('{', i)) >= 0)
            {
                int close = text.IndexOf('}', i + 1);
                if (close < 0) break;
                string name = text.Substring(i + 1, close - i - 1);
                if (!names.Contains(name)) names.Add(name);
                i = close + 1;
            }
            return names;
        }

        private static string PlaceholderProblem(string text)
        {
            int depth = 0, start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    if (depth > 0) return "a '{' opens inside another placeholder.";
                    depth = 1;
                    start = i + 1;
                }
                else if (c == '}')
                {
                    if (depth == 0) return "a '}' closes nothing.";
                    depth = 0;
                    string name = text.Substring(start, i - start);
                    if (name.Length == 0) return "an empty placeholder '{}'.";
                    foreach (char n in name)
                        if (!((n >= 'a' && n <= 'z') || (n >= '0' && n <= '9') || n == '_'))
                            return $"placeholder '{{{name}}}' is not lower_snake_case.";
                }
            }
            return depth > 0 ? "a '{' never closes." : null;
        }
    }
}
