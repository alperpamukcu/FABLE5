using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// The languages the game ships in (2026-09-13, localization L0; the author: "çıkarabildiğimiz
    /// kadar çok dile çıkaracağız"). Steam's full-platform set less Arabic and Thai, which need text
    /// shaping that the pixel faces and uGUI text do not do.
    ///
    /// <see cref="Info.Code"/> is Steam's Web API code and names the string table file
    /// (<c>Resources/Data/loc/&lt;code&gt;.json</c>); <see cref="Info.SteamApi"/> is what
    /// <c>SteamApps.GetCurrentGameLanguage()</c> answers. The store tooling keeps the same list in
    /// <c>Tools/steam_kit/i18n.py</c> — change both.
    /// </summary>
    public static class Languages
    {
        /// <summary>The language every line is written in first, and the last fallback.</summary>
        public const string Source = "en";

        public sealed class Info
        {
            public string Code { get; }
            public string SteamApi { get; }

            /// <summary>The language's own name, as the picker shows it.</summary>
            public string Name { get; }

            /// <summary>The shipped language a reader of this one reads most comfortably, tried
            /// before English when a line is missing; null when English is the nearest.</summary>
            public string Fallback { get; }

            internal Info(string code, string steamApi, string name, string fallback)
            {
                Code = code;
                SteamApi = steamApi;
                Name = name;
                Fallback = fallback;
            }
        }

        /// <summary>In priority order (Docs/marketing/LOCALIZATION_PLAN.md §1). Ukrainian never
        /// falls back to Russian, on purpose.</summary>
        public static readonly IReadOnlyList<Info> All = new[]
        {
            new Info("en", "english", "English", null),
            new Info("zh-CN", "schinese", "简体中文", null),
            new Info("ru", "russian", "Русский", null),
            new Info("de", "german", "Deutsch", null),
            new Info("pt-BR", "brazilian", "Português (Brasil)", "pt"),
            new Info("es", "spanish", "Español (España)", "es-419"),
            new Info("fr", "french", "Français", null),
            new Info("tr", "turkish", "Türkçe", null),
            new Info("pl", "polish", "Polski", null),
            new Info("ko", "koreana", "한국어", null),
            new Info("ja", "japanese", "日本語", null),
            new Info("uk", "ukrainian", "Українська", null),
            new Info("zh-TW", "tchinese", "繁體中文", "zh-CN"),
            new Info("it", "italian", "Italiano", null),
            new Info("es-419", "latam", "Español (Latinoamérica)", "es"),
            new Info("pt", "portuguese", "Português (Portugal)", "pt-BR"),
            new Info("cs", "czech", "Čeština", null),
            new Info("hu", "hungarian", "Magyar", null),
            new Info("ro", "romanian", "Română", null),
            new Info("nl", "dutch", "Nederlands", null),
            new Info("sv", "swedish", "Svenska", null),
            new Info("da", "danish", "Dansk", null),
            new Info("no", "norwegian", "Norsk", null),
            new Info("fi", "finnish", "Suomi", null),
            new Info("el", "greek", "Ελληνικά", null),
            new Info("bg", "bulgarian", "Български", null),
            new Info("id", "indonesian", "Bahasa Indonesia", null),
            new Info("ms", "malay", "Bahasa Melayu", "id"),
            new Info("vi", "vietnamese", "Tiếng Việt", null),
        };

        /// <summary>The shipped language with this code (ordinal), or null.</summary>
        public static Info Find(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (var info in All)
                if (info.Code == code) return info;
            return null;
        }

        /// <summary>The code for a Steam API language name ("schinese", "latam"), or null.</summary>
        public static string FromSteam(string steamApi)
        {
            if (string.IsNullOrEmpty(steamApi)) return null;
            foreach (var info in All)
                if (info.SteamApi == steamApi) return info.Code;
            return null;
        }

        /// <summary>The code for the name of a <c>UnityEngine.SystemLanguage</c> value, or null.
        /// Core cannot see the enum, so the Game layer passes its name. Plain "Portuguese" maps to
        /// Brazil and plain "Spanish" to Spain: the OS does not say which, and those are the larger
        /// Steam audiences (the Steam language, when present, wins over this anyway).</summary>
        public static string FromSystemLanguage(string name)
        {
            switch (name)
            {
                case "English": return "en";
                case "Chinese":
                case "ChineseSimplified": return "zh-CN";
                case "ChineseTraditional": return "zh-TW";
                case "Russian": return "ru";
                case "German": return "de";
                case "Portuguese": return "pt-BR";
                case "Spanish": return "es";
                case "French": return "fr";
                case "Turkish": return "tr";
                case "Polish": return "pl";
                case "Korean": return "ko";
                case "Japanese": return "ja";
                case "Ukrainian": return "uk";
                case "Italian": return "it";
                case "Czech": return "cs";
                case "Hungarian": return "hu";
                case "Romanian": return "ro";
                case "Dutch": return "nl";
                case "Swedish": return "sv";
                case "Danish": return "da";
                case "Norwegian": return "no";
                case "Finnish": return "fi";
                case "Greek": return "el";
                case "Bulgarian": return "bg";
                case "Indonesian": return "id";
                case "Vietnamese": return "vi";
                default: return null;
            }
        }

        /// <summary>Where a line is looked for, in order: the language, its one nearest
        /// neighbour, then English. An unknown code reads English.</summary>
        public static IReadOnlyList<string> Chain(string code)
        {
            var chain = new List<string>(3);
            var info = Find(code);
            if (info != null)
            {
                chain.Add(info.Code);
                if (info.Fallback != null && Find(info.Fallback) != null && !chain.Contains(info.Fallback))
                    chain.Add(info.Fallback);
            }
            if (!chain.Contains(Source)) chain.Add(Source);
            return chain;
        }
    }
}
