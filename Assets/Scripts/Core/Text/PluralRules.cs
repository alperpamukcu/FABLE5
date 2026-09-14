using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>The CLDR plural categories. A string table spells a counted line once per
    /// category its language uses, as <c>key#one</c>, <c>key#few</c>, <c>key#other</c>.</summary>
    public enum PluralCategory
    {
        Zero,
        One,
        Two,
        Few,
        Many,
        Other,
    }

    /// <summary>
    /// Which plural form a whole number takes in each shipped language (2026-09-13, localization
    /// L0). The rules are CLDR's cardinal rules restricted to integers, because every count the
    /// game prints — nights, glasses, stars, dollars — is one. English "1 night / 2 nights" is the
    /// easy case; Russian and Ukrainian want three forms (1, 21 / 2–4, 22–24 / 5–20, 25), Polish
    /// the same with 1 alone as "one", Czech three, Romanian a "few" that runs to 19, and French
    /// and Brazilian Portuguese treat zero as singular. Chinese, Japanese, Korean, Vietnamese,
    /// Indonesian and Malay do not inflect for number at all.
    /// </summary>
    public static class PluralRules
    {
        public static PluralCategory For(string code, long n)
        {
            if (n < 0) n = -n;
            long mod10 = n % 10, mod100 = n % 100;
            switch (code)
            {
                case "zh-CN":
                case "zh-TW":
                case "ja":
                case "ko":
                case "vi":
                case "id":
                case "ms":
                    return PluralCategory.Other;

                case "fr":
                case "pt-BR":
                    if (n == 0 || n == 1) return PluralCategory.One;
                    return Millions(n) ? PluralCategory.Many : PluralCategory.Other;

                case "es":
                case "es-419":
                case "it":
                case "pt":
                    if (n == 1) return PluralCategory.One;
                    return Millions(n) ? PluralCategory.Many : PluralCategory.Other;

                case "ru":
                case "uk":
                    if (mod10 == 1 && mod100 != 11) return PluralCategory.One;
                    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return PluralCategory.Few;
                    return PluralCategory.Many;

                case "pl":
                    if (n == 1) return PluralCategory.One;
                    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return PluralCategory.Few;
                    return PluralCategory.Many;

                case "cs":
                    if (n == 1) return PluralCategory.One;
                    if (n >= 2 && n <= 4) return PluralCategory.Few;
                    return PluralCategory.Other;

                case "ro":
                    if (n == 1) return PluralCategory.One;
                    if (n == 0 || (mod100 >= 2 && mod100 <= 19)) return PluralCategory.Few;
                    return PluralCategory.Other;

                default:   // en, de, nl, sv, da, no, fi, el, bg, hu, tr
                    return n == 1 ? PluralCategory.One : PluralCategory.Other;
            }
        }

        /// <summary>The forms a counted line must spell out in this language. The Spanish,
        /// Italian, Portuguese and French "many" (whole millions) is optional: a table without it
        /// reads <c>#other</c>, which is what those languages write for millions in running text.</summary>
        public static IReadOnlyList<PluralCategory> Required(string code)
        {
            switch (code)
            {
                case "zh-CN":
                case "zh-TW":
                case "ja":
                case "ko":
                case "vi":
                case "id":
                case "ms":
                    return new[] { PluralCategory.Other };
                case "ru":
                case "uk":
                case "pl":
                    return new[] { PluralCategory.One, PluralCategory.Few, PluralCategory.Many };
                case "cs":
                case "ro":
                    return new[] { PluralCategory.One, PluralCategory.Few, PluralCategory.Other };
                default:
                    return new[] { PluralCategory.One, PluralCategory.Other };
            }
        }

        /// <summary>The suffix a table writes after <c>#</c>: "one", "few", "other".</summary>
        public static string Suffix(PluralCategory category)
        {
            switch (category)
            {
                case PluralCategory.Zero: return "zero";
                case PluralCategory.One: return "one";
                case PluralCategory.Two: return "two";
                case PluralCategory.Few: return "few";
                case PluralCategory.Many: return "many";
                default: return "other";
            }
        }

        public static bool TryParseSuffix(string suffix, out PluralCategory category)
        {
            foreach (PluralCategory c in System.Enum.GetValues(typeof(PluralCategory)))
                if (Suffix(c) == suffix)
                {
                    category = c;
                    return true;
                }
            category = PluralCategory.Other;
            return false;
        }

        private static bool Millions(long n) => n != 0 && n % 1000000 == 0;
    }
}
