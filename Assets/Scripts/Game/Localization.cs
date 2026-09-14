using System;
using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// Which language the game speaks, and its tables (2026-09-13, localization L0).
    ///
    /// The choice, first that answers: the player's own pick (saved), then the OS language.
    /// When Steamworks lands, <c>SteamApps.GetCurrentGameLanguage()</c> goes between the two
    /// (<see cref="Languages.FromSteam"/>) — it is the language the player set for this game in
    /// the Steam library. Tables are <c>Resources/Data/loc/&lt;code&gt;.json</c>, read the way the
    /// voices are; only the tables on the language's fallback chain are loaded.
    /// </summary>
    public static class Localization
    {
        public const string PrefKey = "lastcall.language";
        private const string Folder = "Data/loc/";

        private static Localizer _current;

        /// <summary>Raised after <see cref="Use"/> switches the language; the UI rebuilds its text.</summary>
        public static event Action Changed;

        public static Localizer Current => _current ?? (_current = Load(PreferredCode()));

        /// <summary>Forget the loaded language when play starts. The project enters play mode WITHOUT a
        /// domain reload, so statics outlive the play session: a pick saved in the settings kept the
        /// old language through every later play until a script reload (measured 2026-09-14). A
        /// player's build starts a fresh process, so there this changes nothing.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetOnPlay()
        {
            _current = null;
            _available = null;
            Changed = null;
        }

        public static string PreferredCode()
        {
            string saved = PlayerPrefs.GetString(PrefKey, "");
            if (Languages.Find(saved) != null) return saved;
#if UNITY_EDITOR
            // THE EDITOR SPEAKS ENGLISH UNTIL TOLD OTHERWISE (2026-09-14): the author's Windows is
            // Turkish, and once tr.json was whole the editor opened the game in Turkish — the key-art
            // captures and store screenshots are taken here and must be English. The settings picker
            // still previews any language; a player's build follows the OS as below.
            return Languages.Source;
#else
            return Languages.FromSystemLanguage(Application.systemLanguage.ToString()) ?? Languages.Source;
#endif
        }

        /// <summary>Speak <paramref name="code"/> from now on, and remember it.</summary>
        public static void Use(string code)
        {
            if (Languages.Find(code) == null)
                throw new ArgumentException($"'{code}' is not a shipped language.");
            PlayerPrefs.SetString(PrefKey, code);
            PlayerPrefs.Save();
            _current = Load(code);
            Changed?.Invoke();
        }

        /// <summary>Remember <paramref name="code"/> as the player's pick WITHOUT switching now (2026-09-14,
        /// localization L4). The HUD is built once, in one language, so a pick made in the settings is
        /// spoken from the next start; switching mid-game would leave every built label in the old
        /// language beside new lines in the new one. <see cref="PreferredCode"/> answers the pick at once.</summary>
        public static void Choose(string code)
        {
            if (Languages.Find(code) == null)
                throw new ArgumentException($"'{code}' is not a shipped language.");
            PlayerPrefs.SetString(PrefKey, code);
            PlayerPrefs.Save();
        }

        private static List<Languages.Info> _available;

        /// <summary>The shipped languages this build has a table for, in <see cref="Languages.All"/>
        /// order; English always. What the settings picker cycles through, so a language whose
        /// translation has not landed yet is never offered.</summary>
        public static IReadOnlyList<Languages.Info> Available
        {
            get
            {
                if (_available != null) return _available;
                _available = new List<Languages.Info>();
                foreach (var info in Languages.All)
                {
                    if (info.Code == Languages.Source) { _available.Add(info); continue; }
                    var asset = Resources.Load<TextAsset>(Folder + info.Code);
                    if (asset == null) continue;
                    _available.Add(info);
                    Resources.UnloadAsset(asset);
                }
                return _available;
            }
        }

        /// <summary>Speak <paramref name="code"/> for this session only, without saving it. The PlayMode
        /// suite pins English this way, so its captions and pixel baselines hold on any desktop language.</summary>
        public static void UseForSession(string code)
        {
            if (Languages.Find(code) == null)
                throw new ArgumentException($"'{code}' is not a shipped language.");
            _current = Load(code);
            Changed?.Invoke();
        }

        public static Localizer Load(string code)
        {
            var tables = new List<StringTable>();
            foreach (string c in Languages.Chain(code))
            {
                var asset = Resources.Load<TextAsset>(Folder + c);
                if (asset != null) tables.Add(DataLoader.ParseStringTable(asset.text));
            }
            return new Localizer(code, tables);
        }
    }
}
