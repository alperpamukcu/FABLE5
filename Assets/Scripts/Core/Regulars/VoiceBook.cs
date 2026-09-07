using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// The moments a customer opens their mouth. Each is a pool of lines in
    /// <see cref="VoiceDefinition"/>; the UI asks for one when the moment comes.
    /// </summary>
    public enum VoiceCue
    {
        /// <summary>The order, said once the card has been read — carries <c>{drink}</c>.</summary>
        Order,
        /// <summary>The serve, exact: a short shout.</summary>
        Perfect,
        /// <summary>The serve that earns another round.</summary>
        Another,
        /// <summary>The serve that was near enough.</summary>
        Close,
        /// <summary>The serve that was the wrong drink.</summary>
        Wrong,
        /// <summary>A flawless pour, at the first sip — the balloon's own praise.</summary>
        Praise,
        /// <summary>A sip with something to fix — wraps the coaching sentence in
        /// <c>{advice}</c> (as written) or <c>{advice_l}</c> (its first letter lowered).</summary>
        Sip,
        /// <summary>Patience ran out; said on the way off the stool.</summary>
        Leaving,
        /// <summary>Shown the door off the licence.</summary>
        Kicked,
    }

    /// <summary>
    /// ONE WAY OF TALKING (2026-09-06, the author: "müşterilerin tepki sesleri, belli başlı
    /// aksanlarda konuşma şekilleri, konuşmalarına karakter katalım … her müşteri aynı
    /// cümleleri kurmaz"). A voice is a name, the people and the flag codes it answers for,
    /// and a pool of lines per cue. It is CONTENT: the pools are read from
    /// <c>Resources/Data/voices.json</c>, and adding a voice is adding rows there.
    /// </summary>
    public sealed class VoiceDefinition
    {
        public string Id { get; }
        public string Name { get; }
        /// <summary>Two-letter lower-case flag codes this voice speaks for, when no person
        /// claims the look outright.</summary>
        public IReadOnlyList<string> Isos { get; }
        /// <summary>Look slugs (the papers' <c>slug</c>) that speak in this voice.</summary>
        public IReadOnlyList<string> People { get; }

        private readonly Dictionary<VoiceCue, IReadOnlyList<string>> _lines;

        public IReadOnlyList<string> Lines(VoiceCue cue) =>
            _lines.TryGetValue(cue, out var pool) ? pool : Array.Empty<string>();

        public VoiceDefinition(string id, string name, IReadOnlyList<string> isos,
            IReadOnlyList<string> people, IReadOnlyDictionary<VoiceCue, IReadOnlyList<string>> lines)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A voice needs an id.", nameof(id));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Isos = isos ?? Array.Empty<string>();
            People = people ?? Array.Empty<string>();
            foreach (var iso in Isos)
                if (iso == null || iso.Length != 2 || iso != iso.ToLowerInvariant())
                    throw new ArgumentException($"Voice '{id}' answers for the flag code '{iso}'; codes are two lower-case letters.");
            _lines = new Dictionary<VoiceCue, IReadOnlyList<string>>();
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            foreach (VoiceCue cue in Enum.GetValues(typeof(VoiceCue)))
            {
                if (!lines.TryGetValue(cue, out var pool) || pool == null || pool.Count == 0)
                    throw new ArgumentException($"Voice '{id}' has nothing to say for {cue}.");
                var kept = new List<string>(pool.Count);
                foreach (var line in pool)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        throw new ArgumentException($"Voice '{id}' has an empty {cue} line.");
                    if (cue == VoiceCue.Sip && !line.Contains("{advice}") && !line.Contains("{advice_l}"))
                        throw new ArgumentException($"Voice '{id}': the {cue} line \"{line}\" never says what to fix ({{advice}}).");
                    if (cue != VoiceCue.Sip && (line.Contains("{advice}") || line.Contains("{advice_l}")))
                        throw new ArgumentException($"Voice '{id}': only a Sip line may carry {{advice}} (\"{line}\").");
                    string stripped = line.Replace("{drink}", "").Replace("{advice}", "").Replace("{advice_l}", "");
                    if (stripped.IndexOf('{') >= 0 || stripped.IndexOf('}') >= 0)
                        throw new ArgumentException($"Voice '{id}': unknown placeholder in \"{line}\".");
                    kept.Add(line);
                }
                _lines[cue] = kept;
            }
        }
    }

    /// <summary>
    /// EVERY VOICE THE BAR HAS, and who speaks in which. Pure and rng-fed: a line is picked
    /// on a <see cref="SeededRng"/> stream the run owns ("voice"), so a seeded night says the
    /// same things twice, and the last line said per voice and cue is remembered so nobody
    /// repeats themselves back to back. The order is the one line that knows the drink, and it
    /// is only ever asked for with an order the card has already given up — the book never
    /// reaches past <c>InspectId</c> itself (the house rule on hidden information).
    /// </summary>
    public sealed class VoiceBook
    {
        public const string DefaultId = "default";

        public IReadOnlyList<VoiceDefinition> Voices { get; }
        public VoiceDefinition Default { get; }

        private readonly Dictionary<string, VoiceDefinition> _byId = new Dictionary<string, VoiceDefinition>();
        private readonly Dictionary<string, VoiceDefinition> _byIso = new Dictionary<string, VoiceDefinition>();
        private readonly Dictionary<string, VoiceDefinition> _byPerson = new Dictionary<string, VoiceDefinition>();
        private readonly Dictionary<string, string> _last = new Dictionary<string, string>();

        public VoiceBook(IReadOnlyList<VoiceDefinition> voices)
        {
            if (voices == null || voices.Count == 0) throw new ArgumentException("A voice book needs at least the default voice.");
            Voices = voices;
            foreach (var v in voices)
            {
                if (_byId.ContainsKey(v.Id)) throw new ArgumentException($"Two voices are called '{v.Id}'.");
                _byId[v.Id] = v;
                foreach (var iso in v.Isos)
                {
                    if (_byIso.ContainsKey(iso)) throw new ArgumentException($"Two voices answer for the flag code '{iso}'.");
                    _byIso[iso] = v;
                }
                foreach (var slug in v.People)
                {
                    if (slug == null) continue;
                    if (_byPerson.ContainsKey(slug)) throw new ArgumentException($"Two voices claim the look '{slug}'.");
                    _byPerson[slug] = v;
                }
            }
            if (!_byId.TryGetValue(DefaultId, out var fallback))
                throw new ArgumentException($"The voice book has no '{DefaultId}' voice to fall back on.");
            Default = fallback;
        }

        public VoiceDefinition ById(string id) =>
            id != null && _byId.TryGetValue(id, out var v) ? v : Default;

        /// <summary>The voice of a look: the person's own if one is written for them, else the
        /// one for their flag, else the default. A story face passes through here like any
        /// other, but the story's own lines are scripted and never ask this book.</summary>
        public VoiceDefinition For(string personSlug, string iso)
        {
            if (personSlug != null && _byPerson.TryGetValue(personSlug, out var own)) return own;
            if (iso != null && _byIso.TryGetValue(iso.ToLowerInvariant(), out var flag)) return flag;
            return Default;
        }

        /// <summary>
        /// One line for the moment. Picks on <paramref name="rng"/>, skips the line this voice
        /// said last for this cue when it has more than one, and fills the placeholders.
        /// Returns an empty string only for a cue no voice has lines for, which the
        /// constructor already refuses.
        /// </summary>
        public string Say(VoiceDefinition voice, VoiceCue cue, SeededRng rng, string drink = null, string advice = null)
        {
            voice = voice ?? Default;
            var pool = voice.Lines(cue);
            if (pool.Count == 0) pool = Default.Lines(cue);
            if (pool.Count == 0) return string.Empty;
            string key = voice.Id + ":" + cue;
            int i = rng != null ? rng.NextInt(0, pool.Count) : 0;
            if (pool.Count > 1 && _last.TryGetValue(key, out var last) && pool[i] == last)
                i = (i + 1) % pool.Count;
            _last[key] = pool[i];
            return Fill(pool[i], drink, advice);
        }

        /// <summary>The placeholders: <c>{drink}</c>, <c>{advice}</c> and <c>{advice_l}</c> — the
        /// advice with its first letter lowered, unless it starts with "I".</summary>
        public static string Fill(string line, string drink, string advice)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;
            string a = advice ?? string.Empty;
            string al = a;
            if (a.Length > 0 && char.IsUpper(a[0]) && !(a.Length > 1 && a[0] == 'I' && (a[1] == ' ' || a[1] == '\'')))
                al = char.ToLowerInvariant(a[0]) + a.Substring(1);
            return line.Replace("{drink}", drink ?? "that")
                       .Replace("{advice_l}", al)
                       .Replace("{advice}", a);
        }
    }
}
