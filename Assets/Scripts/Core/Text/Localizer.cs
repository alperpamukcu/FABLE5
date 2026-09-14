using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LastCall.Core
{
    /// <summary>
    /// Says a <see cref="Line"/> in one language (2026-09-13, localization L0). A key is looked
    /// for down <see cref="Languages.Chain"/> — the language, its nearest neighbour, English — so
    /// a line not yet translated reads in the closest language a player understands instead of
    /// going blank. A counted line picks its plural form by the rules of the table it was found
    /// in: a Latin American line missing and read from the Spain table counts by Spanish rules,
    /// a Malay line read from the Indonesian table by Indonesian ones.
    ///
    /// A key missing from every table renders as <c>[key]</c>: loud on screen, easy to grep, and
    /// the table tests catch it before a player does. Arguments print invariant (RunCulture);
    /// a <see cref="Line"/> argument is said in the same language.
    /// </summary>
    public sealed class Localizer
    {
        private readonly List<StringTable> _chain = new List<StringTable>();

        public string Code { get; }

        /// <param name="code">The language to speak.</param>
        /// <param name="tables">Every table in hand; the ones on the language's chain are used.</param>
        public Localizer(string code, IEnumerable<StringTable> tables)
        {
            var byCode = new Dictionary<string, StringTable>(StringComparer.Ordinal);
            if (tables != null)
                foreach (var t in tables)
                    if (t != null) byCode[t.Code] = t;
            Code = Languages.Find(code) != null ? code : Languages.Source;
            foreach (string c in Languages.Chain(Code))
                if (byCode.TryGetValue(c, out var table)) _chain.Add(table);
        }

        public string Get(string key) => Render(Line.Of(key));

        /// <summary>The line for <paramref name="key"/>, or <paramref name="fallback"/> when no table on
        /// the chain has it: for words whose English lives in a data file (a recipe's name), where an
        /// untranslated entry should read as that English rather than as a bracketed key.</summary>
        public string GetOr(string key, string fallback) => Lookup(key, null) ?? fallback;

        public bool Has(string key)
        {
            foreach (var t in _chain)
                if (t.TryGet(key, out _) || t.TryGet(key + StringTable.PluralMark + "other", out _))
                    return true;
            return false;
        }

        public string Render(Line line)
        {
            string template = Lookup(line.Key, line.Count);
            if (template == null) return "[" + line.Key + "]";
            return Fill(template, line.Args);
        }

        /// <summary>Capitals in this language (<see cref="TextCase"/>).</summary>
        public string Upper(string s) => TextCase.Upper(Code, s);

        private string Lookup(string key, long? count)
        {
            foreach (var t in _chain)
            {
                if (count.HasValue)
                {
                    string form = PluralRules.Suffix(PluralRules.For(t.Code, count.Value));
                    if (t.TryGet(key + StringTable.PluralMark + form, out string plural)) return plural;
                    if (t.TryGet(key + StringTable.PluralMark + "other", out string other)) return other;
                }
                if (t.TryGet(key, out string text)) return text;
            }
            return null;
        }

        private string Fill(string template, IReadOnlyList<KeyValuePair<string, object>> args)
        {
            if (args.Count == 0 || template.IndexOf('{') < 0) return template;
            var sb = new StringBuilder(template.Length + 16);
            int i = 0;
            while (i < template.Length)
            {
                int open = template.IndexOf('{', i);
                if (open < 0)
                {
                    sb.Append(template, i, template.Length - i);
                    break;
                }
                int close = template.IndexOf('}', open + 1);
                if (close < 0)
                {
                    sb.Append(template, i, template.Length - i);
                    break;
                }
                sb.Append(template, i, open - i);
                string name = template.Substring(open + 1, close - open - 1);
                bool found = false;
                foreach (var arg in args)
                    if (arg.Key == name)
                    {
                        sb.Append(Say(arg.Value));
                        found = true;
                        break;
                    }
                if (!found) sb.Append('{').Append(name).Append('}');   // left visible for the tests
                i = close + 1;
            }
            return sb.ToString();
        }

        private string Say(object value)
        {
            switch (value)
            {
                case null: return "";
                case Line line: return Render(line);
                case string s: return s;
                case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }
    }
}
