using System;
using LastCall.Core;
using LastCall.Game;

namespace LastCall.UI
{
    /// <summary>
    /// The UI's words, in the player's language (2026-09-13, localization L0/L1). Every label the HUD
    /// builds goes through here; the rules for keys and text are in Docs/PLAN_localization_L1.md.
    ///
    ///   UIText.T("market.tab.restock")                                  a fixed line
    ///   UIText.T("market.more_at", ("noun", noun), ("stars", next))     a sentence with named slots
    ///   UIText.N("dayend.nights", nights)                               a counted line: key#one / key#other, {n}
    ///   UIText.Caps(name)                                               capitals in this language, never ToUpperInvariant
    ///   UIText.Data("recipe", recipe.Id, "name", recipe.Name)           words whose English lives in a data file
    ///   UIText.Refusal(e)                                               a rule's refusal, for the toast
    /// </summary>
    public static class UIText
    {
        private static Localizer L => Localization.Current;

        public static string T(string key) => L.Get(key);

        public static string T(Line line) => L.Render(line);

        public static string T(string key, params (string name, object value)[] args) => L.Render(Build(key, args));

        /// <summary>A counted line: the count is also the <c>{n}</c> slot.</summary>
        public static string N(string key, long count, params (string name, object value)[] args) =>
            L.Render(Build(key, args).Counting("n", count));

        public static string Caps(string s) => L.Upper(s);

        /// <summary>Sentence case in this language: "SATIŞ" → "Satış" (<see cref="TextCase.Sentence"/>).</summary>
        public static string Sentence(string s) => L.Sentence(s);

        /// <summary>A line, or <paramref name="fallback"/> where no table in the chain has it (a nationality word for a
        /// country the tables have not met yet reads as the country's name).</summary>
        public static string TOr(string key, string fallback) => L.GetOr(key, fallback);

        public static string CapsT(string key) => Caps(T(key));

        public static string CapsT(string key, params (string name, object value)[] args) => Caps(T(key, args));

        /// <summary><c>data.{kind}.{id}.{field}</c>, falling back to the English the data file carries.</summary>
        public static string Data(string kind, string id, string field, string english) =>
            L.GetOr("data." + kind + "." + id + "." + field, english);

        /// <summary>A list of lines from a data file (a lesson's <c>say</c>, a story beat's <c>ask</c>), each
        /// as <c>data.{kind}.{id}.{field}.{index}</c>, falling back line by line to the English.</summary>
        public static System.Collections.Generic.List<string> DataLines(string kind, string id, string field,
            System.Collections.Generic.IReadOnlyList<string> english)
        {
            var said = new System.Collections.Generic.List<string>(english?.Count ?? 0);
            if (english == null) return said;
            for (int i = 0; i < english.Count; i++)
                said.Add(Data(kind, id, field + "." + i, english[i]));
            return said;
        }

        /// <summary>A refusal in the player's language when the rule attached its sentence
        /// (<see cref="Said"/> or <see cref="IHasLine"/>), else its English message in capitals as the
        /// toast has always shown it.</summary>
        public static string Refusal(Exception e)
        {
            if (Said.TryGet(e, out var line)) return Caps(L.Render(line));
            if (e is IHasLine has) return Caps(L.Render(has.Line));
            return e.Message.ToUpperInvariant();
        }

        private static Line Build(string key, (string name, object value)[] args)
        {
            var line = Line.Of(key);
            if (args != null)
                foreach (var (name, value) in args)
                    line = line.With(name, value);
            return line;
        }
    }
}
