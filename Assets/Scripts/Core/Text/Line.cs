using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// A sentence the rules want said, before it is in any language (2026-09-13, localization L0):
    /// a string-table key plus its named arguments. Core returns these instead of building English
    /// out of fragments, so a translator can put the drink's name wherever their grammar wants it.
    /// An argument may itself be a Line (a drink's name is a key too); a counted line names the
    /// argument that picks its plural form.
    /// </summary>
    public readonly struct Line
    {
        private static readonly KeyValuePair<string, object>[] NoArgs = new KeyValuePair<string, object>[0];
        private readonly KeyValuePair<string, object>[] _args;

        public string Key { get; }

        /// <summary>The number that picks the plural form, when the line is counted.</summary>
        public long? Count { get; }

        public IReadOnlyList<KeyValuePair<string, object>> Args => _args ?? NoArgs;

        private Line(string key, KeyValuePair<string, object>[] args, long? count)
        {
            Key = key;
            _args = args;
            Count = count;
        }

        public static Line Of(string key) => new Line(key, NoArgs, null);

        /// <summary>This line with one more named argument; <c>{name}</c> in the text.</summary>
        public Line With(string name, object value)
        {
            var args = Args;
            var next = new KeyValuePair<string, object>[args.Count + 1];
            for (int i = 0; i < args.Count; i++) next[i] = args[i];
            next[args.Count] = new KeyValuePair<string, object>(name, value);
            return new Line(Key, next, Count);
        }

        /// <summary>This line counted by <paramref name="n"/>, which is also argument
        /// <paramref name="name"/>: the table's <c>key#one</c> / <c>key#few</c> / <c>key#other</c>.</summary>
        public Line Counting(string name, long n) => new Line(Key, With(name, n)._args, n);

        public override string ToString() => Key;
    }
}
