using System.Globalization;
using System.Text;

namespace LastCall.Core
{
    /// <summary>
    /// Capitals the way each language writes them (2026-09-13, localization L0). The UI sets most
    /// of its labels in capitals, and until now did it with <c>ToUpperInvariant</c> — 81 call
    /// sites — which is right for English and wrong for three shipped languages:
    /// Turkish "istanbul" is "İSTANBUL" (dotted capital İ, and dotless ı rises to plain I);
    /// Greek drops the accent in capitals ("Έλεγξε" is "ΕΛΕΓΞΕ", never "ΈΛΕΓΞΕ");
    /// German ß has no everyday capital and becomes "SS".
    /// Rich-text tags (<c>&lt;color=#ff7dc6&gt;</c>) are left as written.
    /// Everything else is invariant, so a result never depends on the player's desktop culture.
    /// </summary>
    public static class TextCase
    {
        public static string Upper(string code, string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length + 4);
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '<')
                {
                    int close = s.IndexOf('>', i);
                    if (close > i)
                    {
                        sb.Append(s, i, close - i + 1);
                        i = close + 1;
                        continue;
                    }
                }
                int next = s.IndexOf('<', i + 1);
                if (next < 0) next = s.Length;
                sb.Append(UpperPlain(code, s.Substring(i, next - i)));
                i = next;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Small letters the way each language writes them (2026-09-22): Turkish "KIRA" is "kıra" and "İ" is "i" -
        /// the mirror of <see cref="Upper"/>'s rule - so a label kept in capitals in the tables can be set in
        /// sentence case without the invariant table turning DONANIM into "donanim".
        /// </summary>
        public static string Lower(string code, string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (code == "tr") s = s.Replace('I', 'ı').Replace('İ', 'i');
            return s.ToLowerInvariant();
        }

        /// <summary>
        /// SENTENCE CASE (2026-09-22, the author's eighth list: "küçük harflerde kullan (ALPER değil Alper)"): the
        /// first letter a capital and the rest small, in this language - "SATIŞ" is "Satış", "NİNA" is "Nina".
        /// Rich-text tags are left as written, and the first LETTER is the one raised, whatever comes before it.
        /// </summary>
        public static string Sentence(string code, string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            bool raised = false;
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '<')
                {
                    int close = s.IndexOf('>', i);
                    if (close > i)
                    {
                        sb.Append(s, i, close - i + 1);
                        i = close + 1;
                        continue;
                    }
                }
                string one = s[i].ToString();
                if (!raised && char.IsLetter(s[i]))
                {
                    sb.Append(UpperPlain(code, Lower(code, one)));
                    raised = true;
                }
                else sb.Append(Lower(code, one));
                i++;
            }
            return sb.ToString();
        }

        private static string UpperPlain(string code, string s)
        {
            if (code == "tr")
                s = s.Replace('i', 'İ').Replace('ı', 'I');
            else if (code == "el")
                s = WithoutTonos(s);
            // the invariant tables of some runtimes leave final sigma and ß as they are
            string upper = s.ToUpperInvariant().Replace('ς', 'Σ');
            return upper.IndexOf('ß') >= 0 ? upper.Replace("ß", "SS") : upper;
        }

        /// <summary>Greek capitals carry no tonos; the diaeresis stays (ΐ → Ϊ).</summary>
        private static string WithoutTonos(string s)
        {
            string d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char c in d)
                if (c != '́' && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.EnclosingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
