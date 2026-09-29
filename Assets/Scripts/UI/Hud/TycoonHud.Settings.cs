using System;
using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE SETTINGS WINDOW. Four pages under tabs - AUDIO, CONTROLS, DISPLAY, LANGUAGE - and a foot with RESET DEFAULTS
    /// and the way back. Every option writes the store it always wrote (Sound, Keys, PlayerOptions, DisplayOptions,
    /// Localization) with the same values; the 2026-09-29 rebuild changed how the window looks, not what it sets.
    ///
    /// A CABINET SINCE 2026-09-29 (the author: "Ayarlar menüsünü/esc/dil vs. arkaplanıyla her şeyiyle tekrardan diğer
    /// sahneleri ürettiğine uygun bir tasarımda sıfırdan tekrar oluştur"). The window was a blue skin under a
    /// generated marquee picture (menu_header, the P2 board) with the brass icons on its tabs; it is the menus' own
    /// fitting now, drawn in code like the pause and the credits (TycoonHud.MenuKit, BUILD_SPEC §3 "Settings"):
    ///   THE CABINET  x 112-1168, rows 90-630, SETTINGS lit in its crown as a sign (NeonWord, clipped to the crown's
    ///                face), the house's magenta tube round its body; both strike on when the window opens (NeonStrike)
    ///   THE TABS     a column down the cabinet's left, each cell the curtain's week row stood on end: the page's neon
    ///                mark and word over a straight tube - the page open lit cyan, the one under the pointer a half-lit
    ///                ClubBlue - one 180x54 cell each; tabs never strike
    ///   THE PAGE     a recess let into the plate right of the tabs; one row per option - its mark, its name, its
    ///                control against the right; the row under the pointer lies on ClubBlue with a tab at its left
    ///   THE NOTE     a well under the tabs with the pointed row's note in it, else the page's own hint, wrapped -
    ///                the notes moved out of the rows, so every row is one line in all 29 languages
    ///   THE FOOT     RESET DEFAULTS at the left; BACK, the one amber key, at the right - or, while a language pick
    ///                waits, APPLY is the amber key and BACK stands beside it
    ///
    /// THE CONTROLS, one grammar for every page (BUILD_SPEC §2): a CHOICE is a well cut into one segment per way, the
    /// chosen one cyan enamel; a METER is a tube struck cyan up to the level, dark glass after it, held by a Graphite
    /// clamp at the level; a CYCLE is the value on a well between two small keys; small keys carry 9-texel neon marks
    /// half-lit in cyan that strike under the pointer (the critic: magenta on every small control crowded the house's
    /// own colour); a key cap is the author's Classic drawing at 2x. Nothing here is a brass icon or a pack plate.
    ///
    /// WHERE IT STANDS. In a night, over the room under the house scrim - the player is in the bar, and the held night
    /// behind the window is the truth. From the front door, on the door's own field and vignette with no scrim (the
    /// author, 2026-09-27: "oyunda değilken ana sahne gözükmemeli"; read_menus finding 8: the scrim over the field
    /// made the page near-black, not the door's plum).
    /// </summary>
    public sealed partial class TycoonHud
    {
        // THE CABINET, in field units (BUILD_SPEC §3 "Settings", re-laid for 21:9 on 2026-09-29): a 21:9 window crops
        // the field to its rows 90..630 (DesignFrame), and the first cabinet - crown at 56, foot keys 632..678 - lost its
        // title and its APPLY there, the only way to switch the language (the review). Stacked, the tabs, the page, the
        // strip and the foot need ~576 rows of the 540 and the page cannot shrink (DISPLAY's ten rows of 38, LANGUAGE's
        // ten 36-unit flag cells), so the TABS stood up into a column at the left and the NOTE went under them: the
        // crown 90..130, the body 130..630 (its tube twelve in, at 142 and 618); the tabs 136..316 x 150 + 60n; the page
        // 328..1144 x 150..560 (410, the pitches unchanged: 62 / 46 / 38); the note 136..316 x 392..612; the foot
        // 566..612. 1056 wide, it stands inside a 16:10 window's columns 64..1216 (the Deck); a 4:3 window crops it.
        private const float SetCabX = 112f, SetCabW = 1056f, SetTop = 90f, SetCrownH = 40f, SetBodyH = 500f;
        private const float SetTabX = 136f, SetTabY = 150f, SetTabH = 54f, SetTabPitch = 60f, SetTabCell = 180f;
        private const float SetPageX = 328f, SetPageY = 150f, SetPageW = 816f, SetPageH = 410f;
        private const float SetNoteX = SetTabX, SetNoteY = 392f, SetNoteW = SetTabCell, SetNoteH = 220f, SetFootY = 566f;
        /// <summary>Where a row's control ends: 24 in from the page's right.</summary>
        private const float SetCtrlRight = SetPageW - 24f;
        /// <summary>A row's name starts here; its mark at 20.</summary>
        private const float SetNameX = 48f;
        /// <summary>The height of every control on a row (a choice, a small key, a cycle's well, a row key).</summary>
        private const float SetCtrlH = 34f;
        /// <summary>A key cap's height: its drawing's 16 at 2x.</summary>
        private const float CapH = 32f;
        /// <summary>The meters' tube, in texels (272 units), and the seek bar's (340).</summary>
        private const int MeterLen = 136, SeekLen = 170;
        private const float SongWellW = 268f, SongRowH = 20f;

        private readonly Dictionary<string, RectTransform> _settingsPages = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, SetTab> _settingsTabs = new Dictionary<string, SetTab>();
        private string _settingsPage = "AUDIO";
        private NeonStrike _settingsStrike;
        private RectTransform _settingsDim;
        private Text _settingsNote, _settingsProbe;

        private readonly List<SetRow> _setRows = new List<SetRow>();
        private readonly List<SetChoice> _setChoices = new List<SetChoice>();
        private readonly List<SetCycle> _setCycles = new List<SetCycle>();
        private readonly List<SetMeter> _setMeters = new List<SetMeter>();
        private readonly List<LangCell> _langCells = new List<LangCell>();
        private SetRow _noteRow;
        private LangCell _noteCell;

        private SetMeter _seek;
        private Text _seekClock, _settingsNowTitle;
        private RectTransform _settingsSongKey, _songList;
        private NeonTube _songHover, _songDrop;
        private SignKey _settingsHoldKey;
        private NeonTube _holdMark, _playMark;
        private readonly Dictionary<string, Text> _songRows = new Dictionary<string, Text>();
        private bool _songListOpen, _seekDragging;
        private string _shownSong;

        private readonly Dictionary<KeyAction, Image> _bindCaps = new Dictionary<KeyAction, Image>();
        private readonly Dictionary<KeyAction, Text> _bindWords = new Dictionary<KeyAction, Text>();
        private readonly Dictionary<KeyAction, Text> _bindHints = new Dictionary<KeyAction, Text>();
        private readonly Dictionary<KeyAction, SetRow> _bindRows = new Dictionary<KeyAction, SetRow>();
        private readonly Dictionary<KeyAction, bool> _capDown = new Dictionary<KeyAction, bool>();
        private KeyAction? _bindListening;

        private SignKey _settingsReset, _settingsBack, _settingsApply, _settingsDev, _settingsBookKey, _settingsStartOver;

        /// <summary>The window mode the DISPLAY page last drew, so Alt+Enter redraws it.</summary>
        private bool _shownWindowed;

        /// <summary>The settings window took the clock when it opened (straight off the room - the beam's cog, while it had
        /// one - not from the pause menu).</summary>
        private bool _settingsHeldClock;

        /// <summary>The door's field behind the window, shown only when the window came from the front door - out of
        /// game, the room is nobody's backdrop.</summary>
        private RectTransform _settingsBackdrop;

        // ── the window's parts ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A row: its pointed look (a ClubBlue[0] fill, a ClubBlue[4] tab at its left, its mark ClubBlue[4]),
        /// shown while the pointer is on it or - a listening CONTROLS row - while it waits for its key.</summary>
        private sealed class SetRow
        {
            public RectTransform Rt;
            public Image Fill, Tab, Mark;
            public Text Name;
            public Func<string> Note;
            public bool Over, Held;

            public void Paint()
            {
                bool on = Over || Held;
                if (Fill.enabled != on) Fill.enabled = on;
                if (Tab.enabled != on) Tab.enabled = on;
                Mark.color = on ? UITheme.ClubBlue[4] : UITheme.Cream[2];
            }
        }

        /// <summary>A CHOICE: one well, one segment a way; the chosen one Cyan[3] enamel with a Cyan[4] lit row and its
        /// word in Night[1]; the one under the pointer ringed by a ClubBlue tube.</summary>
        private sealed class SetChoice
        {
            public Func<int> Selected;
            public Image[] Fill, Lit;
            public Text[] Words;

            public void Paint()
            {
                int s = Selected();
                for (int i = 0; i < Words.Length; i++)
                {
                    bool on = i == s;
                    Fill[i].enabled = on;
                    Lit[i].enabled = on;
                    Words[i].color = on ? UITheme.Night[1] : UITheme.Cream[3];
                }
            }
        }

        /// <summary>A CYCLE: the value in cyan on a well between two small keys; with nothing to choose (the window's size
        /// in fullscreen) the keys are dark glass and the value Cream[2].</summary>
        private sealed class SetCycle
        {
            public SignKey Prev, Next;
            public Text Value;
            public Func<string> Word;
            public Func<bool> Live;
        }

        /// <summary>A METER: the tube dark glass end to end, the lit copy of it clipped to the level (whole texels), the
        /// clamp at the level.</summary>
        private sealed class SetMeter
        {
            public RectTransform Clip, Clamp;
            public float X, GlassY;
            public int Len;
            public Text Value;
        }

        /// <summary>A TAB: its mark, its word, its tube.</summary>
        private sealed class SetTab
        {
            public NeonIcons.View Icon;
            public Text Word;
            public NeonTube Bar;
            public bool Over;

            public void Paint(bool chosen)
            {
                Icon.Show(chosen || Over ? NeonIcons.State.Lit : NeonIcons.State.Half, UITheme.Cyan, true);
                Word.color = chosen ? UITheme.Cyan[4] : Over ? UITheme.Cream[4] : UITheme.Cream[3];
                if (chosen) Bar.Show(NeonIcons.State.Lit, UITheme.Cyan, true);
                else if (Over) Bar.Show(NeonIcons.State.Half, UITheme.ClubBlue, false);
                else Bar.Show(NeonIcons.State.Dark, UITheme.Cyan, false);
            }
        }

        /// <summary>A LANGUAGE CELL: the flag, the language's own name, a tube round it (cyan: the pick; ClubBlue: under the
        /// pointer) and a cyan bead at its right end for the language being spoken now.</summary>
        private sealed class LangCell
        {
            public string Code, Name;
            public NeonTube Tube, Bead;
            public Text Word;
            public bool Over;
        }

        /// <summary>One flag a language (Tools/flags.py draws them, LANGUAGE_ISOS): English flies half the Union flag
        /// and half the Stars and Stripes (fl_en, 2026-09-25 - the author: "yarısı ingiltere yarısı amerika bayrağı
        /// olsun"; fl_gb stays the British licences' flag), the two Chinese tables theirs, the two Spanish and the two
        /// Portuguese each their own; the rest fly their own letters.</summary>
        private static string LanguageFlag(string code)
        {
            switch (code)
            {
                case "en": return "en";
                case "zh-CN": return "cn";
                case "zh-TW": return "tw";
                case "pt-BR": return "br";
                case "es-419": return "mx";
                case "ko": return "kr";
                case "ja": return "jp";
                case "uk": return "ua";
                case "cs": return "cz";
                case "sv": return "se";
                case "da": return "dk";
                case "el": return "gr";
                case "ms": return "my";
                case "vi": return "vn";
                default: return code;
            }
        }

        private void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            bool show = !_settingsPanel.gameObject.activeSelf;
            if (show)
            {
                CloseId();
                // FROM THE DOOR, THE DOOR'S FIELD; IN A NIGHT, THE ROOM UNDER THE SCRIM (read_menus finding 8, 2026-09-29).
                if (_settingsBackdrop != null) _settingsBackdrop.gameObject.SetActive(_settingsFromMenu);
                if (_settingsDim != null) _settingsDim.gameObject.SetActive(!_settingsFromMenu);
                ForgetSettingsPointer();
                RefreshSettings();
                if (!_settingsFromPause && !_settingsFromMenu) Sfx.Play("menu_open", 0.7f);   // from a menu the wall is already down
                // THE NIGHT STOPS FOR THE SETTINGS TOO (2026-09-25, the author: "Settings açıldığında oyun durmalı").
                // From the pause menu it is already held; from the top bar's cog it ran on at full speed behind the
                // window, patience and all. It holds the clock the way the ladder's window does, and lets go of only
                // the hold it took. (The cog left the beam on 2026-09-28's second pass - the way in is Escape's pause
                // menu, or the front door - and the hold stays for any door that ever opens the window straight off
                // the room again; from the pause menu it takes nothing, the night is already held.)
                if (!_settingsFromPause && !Paused) { SetPaused(true); _settingsHeldClock = true; }
            }
            else
            {
                _bindListening = null;
                ToggleSongList(false);
                _seekDragging = false;
                if (_settingsFromPause && _pausePanel != null)
                {
                    // BACK goes back to where the window came from; the night stays held meanwhile.
                    _pausePanel.gameObject.SetActive(true);
                    RefreshPauseFoot();
                }
                else if (_settingsFromMenu && _menuPanel != null)
                {
                    RebuildMenuColumn();
                    _menuPanel.gameObject.SetActive(true);
                }
                else Sfx.Play("menu_close", 0.6f);
                _settingsFromPause = false;
                _settingsFromMenu = false;
                if (_settingsBackdrop != null) _settingsBackdrop.gameObject.SetActive(false);
                if (_settingsHeldClock) { _settingsHeldClock = false; SetPaused(false); }
            }
            _settingsPanel.gameObject.SetActive(show);
            // the sign strikes on every time the window opens (NeonStrike): the title, then the tube a tenth after it
            if (show && _settingsStrike != null) _settingsStrike.Restart();
        }

        /// <summary>The family's neon frame, drawn from the palette's own tubes: a 6-unit band
        /// on each edge — magenta, the cream core, magenta — the same three lines the pause
        /// picture carried, here procedural so the plate can be any size. (The settings wore it until 2026-09-29; the
        /// achievements' shell still does.)</summary>
        private void NeonEdge(RectTransform plate)
        {
            var tubes = new[] { UITheme.Magenta[3], UITheme.Cream[4], UITheme.Magenta[3] };
            for (int line = 0; line < tubes.Length; line++)
            {
                float inset = line * 2f;
                foreach (var (name, min, max, offMin, offMax) in new (string, Vector2, Vector2, Vector2, Vector2)[]
                {
                    ("Top" + line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(inset, -inset - 2f), new Vector2(-inset, -inset)),
                    ("Bottom" + line, new Vector2(0, 0), new Vector2(1, 0), new Vector2(inset, inset), new Vector2(-inset, inset + 2f)),
                    ("Left" + line, new Vector2(0, 0), new Vector2(0, 1), new Vector2(inset, inset), new Vector2(inset + 2f, -inset)),
                    ("Right" + line, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-inset - 2f, inset), new Vector2(-inset, -inset)),
                })
                {
                    var strip = NewRect("Neon" + name, plate);
                    strip.anchorMin = min; strip.anchorMax = max;
                    strip.offsetMin = offMin; strip.offsetMax = offMax;
                    var img = strip.gameObject.AddComponent<Image>();
                    img.color = tubes[line];
                    img.raycastTarget = false;
                }
            }
        }

        private void BuildSettings(RectTransform root)
        {
            _settingsPanel = NewRect("Settings", root);
            var canvas = _settingsPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 29;                 // with the pause menu, over the book; the market (22) never shows it
            _settingsPanel.gameObject.AddComponent<ForgivingRaycaster>();
            Stretch(_settingsPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // NOT IN GAME, NOT THE ROOM (2026-09-27, the author: "Ana menüde settings vs. basıldığında arkada ana sahne
            // gözüküyor, oyunda değilken gözükmemesi gerekiyor"): opened from the front door, the window stands on the
            // door's own field - its night and its four-band vignette - wall to wall, and nothing darkens it (read_menus
            // finding 8: the house scrim lay over the field too and the page read near-black). The door's panel itself
            // stays hidden under the window - its field is on canvas 31 and would cover this one's 29 (the critic).
            _settingsBackdrop = NewRect("MenuField", _settingsPanel);
            Stretch(_settingsBackdrop, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backdropImg = _settingsBackdrop.gameObject.AddComponent<Image>();
            backdropImg.color = MenuField;
            backdropImg.raycastTarget = true;
            var backdropBtn = _settingsBackdrop.gameObject.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(ToggleSettings);        // a click beside the cabinet goes back, as the scrim's does
            var vig = NewRect("Vignette", _settingsBackdrop);
            Stretch(vig, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            foreach (float reach in VignetteReach) VignetteRing(vig, reach);
            _settingsBackdrop.gameObject.SetActive(false);

            // THE ROOM STAYS, DARKENED (2026-09-16, with the pause menu): in a night the window stands over it under the
            // house scrim; a click on the scrim closes the window.
            _settingsDim = NewRect("Dim", _settingsPanel);
            Stretch(_settingsDim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dimImg = _settingsDim.gameObject.AddComponent<Image>();
            dimImg.color = MenuScrim;
            dimImg.raycastTarget = true;
            var dimBtn = _settingsDim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(ToggleSettings);

            // one measuring line for the build (every control is sized to its widest word in the language spoken)
            _settingsProbe = NewText("Probe", _settingsPanel, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
            _settingsProbe.horizontalOverflow = HorizontalWrapMode.Overflow;

            // THE CABINET (2026-09-29): SETTINGS lit in the crown - in place of the generated marquee (menu_header, the
            // P2 board) - its light clipped to the crown's face above the tabs; the tube round the body.
            var cab = BuildMenuCabinet(_settingsPanel, "Plate", SetCabX, SetTop, SetCabW, SetCrownH, SetBodyH,
                UIText.T("chrome.settings.title"), SetPageY - 6f);
            _settingsStrike = cab.Strike;

            BuildSettingsTabs();
            MenuRecess(_settingsPanel, "Page", SetPageX, SetPageY, SetPageW, SetPageH, UITheme.Night[1]);
            _settingsPages["AUDIO"] = BuildAudioPage();
            _settingsPages["CONTROLS"] = BuildControlsPage();
            _settingsPages["DISPLAY"] = BuildDisplayPage();
            _settingsPages["LANGUAGE"] = BuildLanguagePage();

            // THE NOTE: the pointed row's note, else the page's hint - the small face, wrapped and centred in a well
            // under the tabs (it was a one-line strip under the page until the 21:9 re-lay took that row).
            var strip = MenuRecess(_settingsPanel, "Note", SetNoteX, SetNoteY, SetNoteW, SetNoteH, UITheme.Night[0]).rectTransform;
            _settingsNote = NewText("Text", strip, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
            FieldRect(_settingsNote.rectTransform, 12f, 12f, SetNoteW - 24f, SetNoteH - 24f);
            _settingsNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            _settingsNote.verticalOverflow = VerticalWrapMode.Truncate;

            BuildSettingsFoot();

            Destroy(_settingsProbe.gameObject);
            _settingsProbe = null;
            ShowSettingsPage("AUDIO");
            _settingsPanel.gameObject.SetActive(false);
        }

        /// <summary>The foot, on the cabinet's own face (Night[2]): RESET DEFAULTS at the page's left edge; BACK at its
        /// right - the amber key - and APPLY, which takes the amber while a language pick waits (RefreshSettings lays
        /// the two). The dev bench beside RESET in the editor and development builds.</summary>
        private void BuildSettingsFoot()
        {
            _settingsReset = MenuSignKey(_settingsPanel, "RESET", UIText.T("chrome.settings.reset"), "reset", UITheme.Cyan,
                false, 2, SignKeySmallH, 200f, () =>
                {
                    Sound.Volume = 0.8f; Sound.MusicVolume = 1f; Sound.EffectsVolume = 1f; Sound.Muted = false;
                    // Every option of the DISPLAY page and INVERT POUR back to its default, motion with them
                    // (PlayerOptions) - but NOT the window or its size (2026-09-26): a reset must never throw a
                    // windowed player into fullscreen.
                    PlayerOptions.ResetDefaults();
                    DisplayOptions.ApplyPacing();
                    Keys.ResetAll();
                    Sfx.Play("click");
                    RefreshSettings();
                });
            PlaceSignKey(_settingsReset, SetPageX + ((RectTransform)_settingsReset.transform).sizeDelta.x * 0.5f, SetFootY);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _settingsDev = MenuSignKey(_settingsPanel, "DEV", "DEV TOOLS", null, UITheme.Cyan, false, 2, SetCtrlH, 120f,
                () => { ToggleSettings(); ToggleDevBench(); }, 16f);
            float devW = ((RectTransform)_settingsDev.transform).sizeDelta.x;
            PlaceSignKey(_settingsDev, SetPageX + ((RectTransform)_settingsReset.transform).sizeDelta.x + 12f + devW * 0.5f,
                SetFootY + (SignKeySmallH - SetCtrlH) * 0.5f);
#endif
            _settingsBack = MenuSignKey(_settingsPanel, "BACK", UIText.T("chrome.settings.back"), "back", UITheme.Cyan,
                true, 2, SignKeySmallH, 180f, () => { Sfx.Play("click"); ToggleSettings(); });
            // APPLY (2026-09-16, the author: "dil seçildikten sonra uygula dendiğinde oyunun dili direkt değişmeli"):
            // up once a different language is picked; the scene rebuilds around the same run in the new words.
            _settingsApply = MenuSignKey(_settingsPanel, "APPLY", UIText.T("chrome.settings.apply_language"), "apply",
                UITheme.Cyan, true, 2, SignKeySmallH, 180f, () =>
                {
                    string pick = Localization.PreferredCode();
                    if (pick == Localization.Current.Code || _bootstrap == null) return;
                    Sfx.Play("click");
                    Localization.UseForSession(pick);
                    // from the front door, back to the front door (2026-09-29, read_menus finding 2): it used to land the
                    // player in the cold boot's bar, dealt on the inspector's seed, with the clock running
                    _bootstrap.ReloadKeepingRun(_settingsFromMenu);
                });
            _settingsApply.gameObject.SetActive(false);
            LaySettingsFoot(false);
        }

        /// <summary>BACK alone at the page's right edge and amber; or APPLY there, amber, and BACK beside it, dark.</summary>
        private void LaySettingsFoot(bool applyUp)
        {
            if (_settingsBack == null) return;
            float right = SetPageX + SetPageW;
            float bw = ((RectTransform)_settingsBack.transform).sizeDelta.x;
            if (_settingsApply != null && _settingsApply.gameObject.activeSelf != applyUp) _settingsApply.gameObject.SetActive(applyUp);
            if (applyUp && _settingsApply != null)
            {
                float aw = ((RectTransform)_settingsApply.transform).sizeDelta.x;
                PlaceSignKey(_settingsApply, right - aw * 0.5f, SetFootY);
                PlaceSignKey(_settingsBack, right - aw - 12f - bw * 0.5f, SetFootY);
            }
            else PlaceSignKey(_settingsBack, right - bw * 0.5f, SetFootY);
            if (_settingsBack.Primary == applyUp)
            {
                _settingsBack.Primary = !applyUp;
                _settingsBack.Apply();
            }
            // the dev bench steps aside while APPLY needs the foot (a long APPLY and BACK reach it in a few languages)
            if (_settingsDev != null && _settingsDev.gameObject.activeSelf == applyUp) _settingsDev.gameObject.SetActive(!applyUp);
        }

        // ── the tabs ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE TABS AS THE CURTAIN'S WEEK ROW (BUILD_SPEC §2 "Tabs"): four cells of 180x54 down the cabinet's left (a row
        /// across the page until the 21:9 re-lay, 2026-09-29), each the page's neon mark and its word over a straight tube
        /// 156 long. The open page's mark and tube lit cyan, its word Cyan[4]; the one under the pointer its mark lit and
        /// its tube half ClubBlue; the rest half-lit, the word Cream[3]. A word too long for its cell beside the mark steps
        /// to the 8 size - measured, it should not (Russian's УПРАВЛЕНИЕ left 32 units of a 204 cell, fit_check
        /// 2026-09-29, so 8 of the 180). The old row of pack keys went with the brass icons.
        /// </summary>
        private void BuildSettingsTabs()
        {
            int i = 0;
            foreach (var (id, key, icon) in new[] {
                ("AUDIO", "chrome.settings.audio", "audio"), ("CONTROLS", "chrome.settings.controls", "controls"),
                ("DISPLAY", "chrome.settings.display", "display"), ("LANGUAGE", "chrome.settings.language", "language") })
            {
                string page = id;
                var rt = NewRect("Tab_" + id, _settingsPanel);
                FieldRect(rt, SetTabX, SetTabY + i * SetTabPitch, SetTabCell, SetTabH);
                var hit = rt.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                var btn = rt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = hit;
                btn.onClick.AddListener(() =>
                {
                    if (_settingsPage == page) return;
                    Sfx.Play("click");
                    ShowSettingsPage(page);
                });

                var word = NewText("Word", rt, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[3]);
                word.horizontalOverflow = HorizontalWrapMode.Overflow;
                word.text = UIText.T(key);
                if (NeonIcons.Size * 2 + 4f + word.preferredWidth > SetTabCell - 8f)
                    word.fontSize = LanguageFonts.Size(word.font, 8);
                float ww = Mathf.Ceil(word.preferredWidth);
                float x0 = Mathf.Floor((SetTabCell - (NeonIcons.Size * 2 + 4f + ww)) * 0.25f) * 2f;   // even
                var tab = new SetTab
                {
                    Icon = new NeonIcons.View(rt, "Icon", icon, new Vector2(0f, 1f), new Vector2(x0 + NeonIcons.Size, -NeonIcons.Size)),
                    Word = word,
                };
                FieldRect(word.rectTransform, x0 + NeonIcons.Size * 2 + 4f, -1f, ww + 8f, NeonIcons.Size * 2);
                // the tube: its glass along row 52 of the cell, from 12 in to 12 from its end (NeonBar is NeonPad bigger)
                int len = (int)(SetTabCell / 2f) - 12;                     // 78 texels, 156 units
                var size = new Vector2((len + 2 * ChromeArt.NeonPad) * 2f, (1 + 2 * ChromeArt.NeonPad) * 2f);
                tab.Bar = new NeonTube(rt, "Tube", l => ChromeArt.NeonBar(len, l), size, new Vector2(0f, 1f),
                    new Vector2(12f - 2f * ChromeArt.NeonPad + size.x * 0.5f, -(52f - 2f * ChromeArt.NeonPad + size.y * 0.5f)));
                word.transform.SetAsLastSibling();
                var relay = rt.gameObject.AddComponent<HoverRelay>();
                relay.Entered = () => { tab.Over = true; SettingsTick(); tab.Paint(_settingsPage == page); };
                relay.Exited = () => { tab.Over = false; tab.Paint(_settingsPage == page); };
                _settingsTabs[id] = tab;
                i++;
            }
        }

        private void ShowSettingsPage(string id)
        {
            _settingsPage = id;
            _bindListening = null;
            ToggleSongList(false);
            _noteRow = null;
            _noteCell = null;
            foreach (var pair in _settingsPages) pair.Value.gameObject.SetActive(pair.Key == id);
            foreach (var pair in _settingsTabs) pair.Value.Paint(pair.Key == id);
            RefreshSettings();
        }

        /// <summary>The pointer's marks are forgotten when the window opens (a row or cell the pointer was on when it
        /// closed never heard it leave).</summary>
        private void ForgetSettingsPointer()
        {
            foreach (var row in _setRows) { row.Over = false; row.Paint(); }
            foreach (var cell in _langCells) cell.Over = false;
            foreach (var tab in _settingsTabs.Values) tab.Over = false;
            foreach (var tube in _setSegmentTubes) tube.Visible = false;
            if (_songHover != null) _songHover.Visible = false;
            _noteRow = null;
            _noteCell = null;
            foreach (var pair in _settingsTabs) pair.Value.Paint(pair.Key == _settingsPage);
        }

        private static float s_settingsTick = -1f;

        /// <summary>The pack's hover tick for the window's own plates (the tabs, the segments, the language cells) - on
        /// the sign keys' gap, so a sweep is one run of ticks.</summary>
        private static void SettingsTick()
        {
            if (Time.unscaledTime - s_settingsTick < 0.09f) return;
            s_settingsTick = Time.unscaledTime;
            Sfx.Play("hover", 0.14f);
        }

        // ── the row grammar ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// ROW PITCH (BUILD_SPEC §2 "Row"): the largest that fits <paramref name="n"/> rows in the page, at most
        /// <paramref name="max"/>, and 2 (mod 4) so a 34-tall control stands an even number of units under the row's top
        /// (AUDIO 62, CONTROLS 46, DISPLAY 38); the rows centred in the page, on the even grid.
        /// </summary>
        private static (float pitch, float top) RowPitch(int n, float max)
        {
            int pitch = Mathf.Min((int)max, ((int)SetPageH - 16) / n);
            while (pitch % 4 != 2) pitch--;
            int top = ((int)SetPageH - pitch * n) / 2;
            top -= top % 2;
            return (pitch, top);
        }

        private RectTransform NewSettingsPage(string id)
        {
            var page = NewRect("Page_" + id, _settingsPanel);
            FieldRect(page, SetPageX, SetPageY, SetPageW, SetPageH);
            return page;
        }

        /// <summary>A row of <paramref name="page"/> at <paramref name="y"/>, <paramref name="pitch"/> tall: its mark at 20,
        /// its name at 48 (body 16, Cream[4]), a Night[2] rule under it unless it is the <paramref name="last"/>; the
        /// caller stands the control against the row's right and then fits the name to what it leaves
        /// (<see cref="FitRowName"/>). <paramref name="note"/> is what the strip says while the pointer is on it.</summary>
        private SetRow SettingsRow(RectTransform page, string id, string name, string mark, float y, float pitch, bool last,
            Func<string> note)
        {
            var rt = NewRect("R_" + id, page);
            FieldRect(rt, 0f, y, SetPageW, pitch);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);       // the row answers the pointer over its whole width
            hit.raycastTarget = true;
            var row = new SetRow { Rt = rt, Note = note };
            row.Fill = FieldFill(rt, "Pointed", 4f, 2f, SetPageW - 8f, pitch - 4f, UITheme.ClubBlue[0]);
            row.Tab = FieldFill(rt, "Tab", 4f, 2f, 4f, pitch - 4f, UITheme.ClubBlue[4]);
            float my = Mathf.Floor((pitch - 16f) / 4f) * 2f;               // the 16 mark on the even grid
            row.Mark = FieldFill(rt, "Mark", 20f, my, 16f, 16f, UITheme.Cream[2]);
            row.Mark.sprite = NightArt.Mark(mark) ?? ChromeArt.Mark(mark);
            row.Mark.enabled = row.Mark.sprite != null;                     // never a bare square for a missing mark
            row.Name = NewText("Name", rt, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
            FieldRect(row.Name.rectTransform, SetNameX, -1f, 400f, pitch);
            row.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            row.Name.text = name;
            if (!last) FieldFill(rt, "Rule", 16f, pitch - 2f, SetPageW - 32f, 2f, UITheme.Night[2]);
            var relay = rt.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () => { row.Over = true; row.Paint(); _noteRow = row; PaintSettingsNote(); };
            relay.Exited = () =>
            {
                row.Over = false; row.Paint();
                if (_noteRow == row) { _noteRow = null; PaintSettingsNote(); }
            };
            row.Paint();
            _setRows.Add(row);
            return row;
        }

        /// <summary>A row's name keeps clear of its control: 16 air before the control's left edge
        /// (<paramref name="controlLeft"/>, row units), else the 8 size - documented, never an overlap. Measured in all 29
        /// languages it never steps (fit_check, 2026-09-29: the tightest, Bulgarian's master volume, leaves 76).</summary>
        private static void FitRowName(SetRow row, float controlLeft)
        {
            float room = controlLeft - 16f - SetNameX;
            var rt = row.Name.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(8f, room), rt.sizeDelta.y);
            if (row.Name.preferredWidth > room) row.Name.fontSize = LanguageFonts.Size(row.Name.font, 8);
        }

        /// <summary>The widest of <paramref name="words"/> in the body face at 16 (the language spoken).</summary>
        private float WidestWord(IEnumerable<string> words)
        {
            float w = 0f;
            if (_settingsProbe == null) return w;
            _settingsProbe.fontSize = LanguageFonts.Size(_settingsProbe.font, 16);
            foreach (var s in words)
            {
                _settingsProbe.text = s;
                w = Mathf.Max(w, _settingsProbe.preferredWidth);
            }
            return Mathf.Ceil(w);
        }

        /// <summary>One segment width for a set of choices: the widest way + 32, at least 80, snapped to 4.</summary>
        private float ChoiceSeg(IEnumerable<string> words) => Mathf.Max(80f, SnapUp(WidestWord(words) + 32f, 4f));

        private readonly List<NeonTube> _setSegmentTubes = new List<NeonTube>();

        /// <summary>
        /// A CHOICE against <paramref name="right"/> at <paramref name="top"/> (row units): a Night[0] well with a Graphite
        /// rim, cut into one <paramref name="seg"/>-wide segment per word by Graphite seams. A click on a segment picks
        /// it (<paramref name="pick"/>); <paramref name="selected"/> says which is chosen. Returns its left edge.
        /// </summary>
        private float ChoiceControl(RectTransform row, string id, string[] words, float seg, float right, float top,
            Func<int> selected, Action<int> pick)
        {
            float w = seg * words.Length + 4f;
            var rt = NewRect(id, row);
            FieldRect(rt, right - w, top, w, SetCtrlH);
            var well = rt.gameObject.AddComponent<Image>();
            well.sprite = MenuArt.ChoiceWell();
            well.type = Image.Type.Sliced;
            well.pixelsPerUnitMultiplier = 0.5f;
            well.raycastTarget = false;
            var choice = new SetChoice
            {
                Selected = selected,
                Fill = new Image[words.Length],
                Lit = new Image[words.Length],
                Words = new Text[words.Length],
            };
            for (int i = 0; i < words.Length; i++)
            {
                int at = i;
                float sx = 2f + i * seg;
                if (i > 0) FieldFill(rt, "Seam" + i, sx, 2f, 2f, SetCtrlH - 4f, UITheme.Graphite[2]);
                float fx = i > 0 ? sx + 2f : sx, fw = i > 0 ? seg - 2f : seg;
                choice.Fill[i] = FieldFill(rt, "On" + i, fx, 2f, fw, SetCtrlH - 4f, UITheme.Cyan[3]);
                choice.Lit[i] = FieldFill(rt, "Lit" + i, fx, 2f, fw, 2f, UITheme.Cyan[4]);
                var word = NewText("Word" + i, rt, _body, 16, TextAnchor.MiddleCenter, UITheme.Cream[3]);
                FieldRect(word.rectTransform, fx + 1f, -1f, fw, SetCtrlH);
                word.horizontalOverflow = HorizontalWrapMode.Overflow;
                word.text = words[i];
                choice.Words[i] = word;

                var s = NewRect("S" + i, rt);
                FieldRect(s, sx, 0f, seg, SetCtrlH);
                var hit = s.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                var btn = s.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = hit;
                btn.onClick.AddListener(() => { pick(at); Sfx.Play("click"); RefreshSettings(); });
                int tw = Mathf.RoundToInt((seg + 2f) / 2f), th = Mathf.RoundToInt(SetCtrlH / 2f);
                var tube = new NeonTube(rt, "Pointed" + i, l => ChromeArt.NeonPath(tw, th, 1, 0, l),
                    new Vector2((tw + 2 * ChromeArt.NeonPad) * 2f, (th + 2 * ChromeArt.NeonPad) * 2f), new Vector2(0f, 1f),
                    new Vector2(sx + (seg + 2f) * 0.5f, -SetCtrlH * 0.5f));
                tube.Show(NeonIcons.State.Lit, UITheme.ClubBlue, true);
                tube.Visible = false;
                _setSegmentTubes.Add(tube);
                var relay = s.gameObject.AddComponent<HoverRelay>();
                relay.Entered = () => { tube.Visible = true; SettingsTick(); };
                relay.Exited = () => tube.Visible = false;
            }
            _setChoices.Add(choice);
            return right - w;
        }

        /// <summary>A SMALL KEY at (<paramref name="x"/>, <paramref name="top"/>) of <paramref name="parent"/>: a 34-unit
        /// sign key with no word - its plate, its 9-texel mark half-lit cyan at rest and struck lit under the pointer,
        /// the ClubBlue tube round it (SignKey runs it; a Button beside it carries the click and its dead state).</summary>
        private SignKey SmallMarkKey(RectTransform parent, string id, string mark, float x, float top, Action onClick)
        {
            var rt = NewRect(id, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(SetCtrlH, SetCtrlH);
            rt.anchoredPosition = new Vector2(x + SetCtrlH * 0.5f, -(top + SetCtrlH * 0.5f));
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            var key = rt.gameObject.AddComponent<SignKey>();
            button.onClick.AddListener(key.Click);
            key.Button = button;
            key.Pressed = onClick;
            key.Hue = UITheme.Cyan;
            key.Ground = 1;
            var body = NewRect("Body", rt);
            Stretch(body, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var plate = NewRect("Plate", body);
            Stretch(plate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.type = Image.Type.Sliced;
            plateImg.pixelsPerUnitMultiplier = 0.5f;
            plateImg.raycastTarget = false;
            key.Icon = new NeonIcons.SmallView(body, "Mark", mark, new Vector2(0.5f, 0.5f), Vector2.zero);
            int k = Mathf.RoundToInt(SetCtrlH / 2f);
            key.Hover = new NeonTube(body, "Hover", l => ChromeArt.NeonPath(k, k, 1, 0, l),
                new Vector2((k + 2 * ChromeArt.NeonPad) * 2f, (k + 2 * ChromeArt.NeonPad) * 2f), new Vector2(0.5f, 0.5f), Vector2.zero);
            key.Hover.Visible = false;
            key.Body = body;
            key.Plate = plateImg;
            key.Apply();
            return key;
        }

        /// <summary>A WORDED ROW KEY (TONIGHT'S BOOK's OPEN, START OVER's NEW RUN): a 34-tall sign key on the page's
        /// Night[1], its small chevron at the left in cyan, its word dead centre, fitted to the widest of
        /// <paramref name="fitWords"/> (so a key that asks first never grows when it asks), against
        /// <paramref name="right"/>.</summary>
        private SignKey RowWordKey(RectTransform row, string id, string word, IEnumerable<string> fitWords, float right,
            float top, Action onClick)
        {
            const float Pad = 52f;                                          // 6 + the 30 mark + 16 of air
            float w = Mathf.Max(160f, SnapUp(WidestWord(fitWords) + 2f * Pad, 4f));
            var key = MenuSignKey(row, id, word, null, UITheme.Cyan, false, 1, SetCtrlH, w, onClick, Pad);
            var body = key.Body;
            key.Icon = new NeonIcons.SmallView(body, "Mark", "right", new Vector2(0f, 0.5f),
                new Vector2(6f + NeonIcons.SmallSize, 0f));                 // six in from the key's left, as a sign key's mark
            key.Label.transform.SetAsLastSibling();
            key.Apply();
            var rt = (RectTransform)key.transform;
            PlaceSignKey(key, right - rt.sizeDelta.x * 0.5f, top);
            return key;
        }

        /// <summary>
        /// A CYCLE against <paramref name="right"/>: the left small key, the value on a Night[0] well as wide as the widest
        /// of <paramref name="words"/> + 32 (never under 144), the right small key, 8 apart. A click steps
        /// <paramref name="step"/> by -1 or +1. Returns its left edge.
        /// </summary>
        private float CycleControl(RectTransform row, string id, IEnumerable<string> words, float right, float top,
            Func<string> word, Func<bool> live, Action<int> step)
        {
            float vw = Mathf.Max(144f, SnapUp(WidestWord(words) + 32f, 4f));
            float xNext = right - SetCtrlH, xWell = xNext - 8f - vw, xPrev = xWell - 8f - SetCtrlH;
            var prev = SmallMarkKey(row, "PREV", "left", xPrev, top, () => { step(-1); Sfx.Play("click"); RefreshSettings(); });
            var well = MenuRecess(row, id, xWell, top, vw, SetCtrlH, UITheme.Night[0]);
            var value = NewText("Value", well.rectTransform, _body, 16, TextAnchor.MiddleCenter, UITheme.Cyan[4]);
            FieldRect(value.rectTransform, 1f, -1f, vw, SetCtrlH);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            var next = SmallMarkKey(row, "NEXT", "right", xNext, top, () => { step(+1); Sfx.Play("click"); RefreshSettings(); });
            _setCycles.Add(new SetCycle { Prev = prev, Next = next, Value = value, Word = word, Live = live });
            return xPrev;
        }

        /// <summary>
        /// A METER'S TUBE (BUILD_SPEC §2 "Meter"): a straight tube <paramref name="len"/> texels long whose rim starts at
        /// row unit <paramref name="x"/> and whose glass runs along row unit <paramref name="glassY"/> - dark glass end to
        /// end, its lit copy clipped to the level in whole texels, a Graphite clamp holding it at the level and, with
        /// <paramref name="ticks"/>, a Graphite[3] tick under every tenth.
        /// </summary>
        private SetMeter MeterTube(RectTransform row, string id, float x, float glassY, int len, bool ticks)
        {
            int P = ChromeArt.NeonPad;
            var size = new Vector2((len + 2 * P) * 2f, (1 + 2 * P) * 2f);
            var dark = new NeonTube(row, id + "Dark", l => ChromeArt.NeonBar(len, l), size, new Vector2(0f, 1f),
                new Vector2(x - 2f * P + size.x * 0.5f, -(glassY - 2f * P + size.y * 0.5f)));
            dark.Show(NeonIcons.State.Dark, UITheme.Cyan, false);
            var clip = NewRect(id + "Lit", row);
            FieldRect(clip, x - 2f * P, glassY - 2f * P, 0f, size.y);
            clip.gameObject.AddComponent<RectMask2D>();
            var lit = new NeonTube(clip, "Tube", l => ChromeArt.NeonBar(len, l), size, new Vector2(0f, 1f),
                new Vector2(size.x * 0.5f, -size.y * 0.5f));
            lit.Show(NeonIcons.State.Lit, UITheme.Cyan, true);
            if (ticks)
                for (int k = 1; k < 10; k++)
                    FieldFill(row, id + "Tick" + k, x + Mathf.Round(len * k / 10f) * 2f, glassY + 10f, 2f, 4f, UITheme.Graphite[3]);
            var clamp = NewRect(id + "Clamp", row);
            FieldRect(clamp, x - 4f, glassY - 8f, 8f, 20f);
            var ci = clamp.gameObject.AddComponent<Image>();
            ci.sprite = MenuArt.Clamp();
            ci.raycastTarget = false;
            var m = new SetMeter { Clip = clip, Clamp = clamp, X = x, GlassY = glassY, Len = len };
            return m;
        }

        /// <summary>The level on a meter: the lit tube up to it, the clamp at it (both on whole texels); muted, the whole
        /// tube is dark glass and the clamp still marks where the level waits.</summary>
        private static void PaintMeterTube(SetMeter m, float level, bool dark)
        {
            if (m == null) return;
            int lit = Mathf.Clamp(Mathf.RoundToInt(m.Len * Mathf.Clamp01(level)), 0, m.Len);
            bool show = !dark && lit >= 2;
            if (m.Clip.gameObject.activeSelf != show) m.Clip.gameObject.SetActive(show);
            if (show) m.Clip.sizeDelta = new Vector2(2f * ChromeArt.NeonPad + lit * 2f, m.Clip.sizeDelta.y);
            m.Clamp.anchoredPosition = new Vector2(m.X + lit * 2f - 4f, -(m.GlassY - 8f));
        }

        // ── AUDIO ────────────────────────────────────────────────────────────────────────────────────────────────

        private RectTransform BuildAudioPage()
        {
            var page = NewSettingsPage("AUDIO");
            var (pitch, top) = RowPitch(6, 64f);
            float ctop = (pitch - SetCtrlH) * 0.5f;
            float y = top;

            // THE THREE LEVELS: - [ the tube ] + and the level in cyan, steps of a tenth (as the pack's keys stepped).
            MeterRow(page, "MASTER", "chrome.settings.master", "speaker", y, pitch, ctop, () => Sound.Volume, v => Sound.Volume = v);
            y += pitch;
            MeterRow(page, "MUSIC", "chrome.settings.music", "note", y, pitch, ctop, () => Sound.MusicVolume, v => Sound.MusicVolume = v);
            y += pitch;
            MeterRow(page, "EFFECTS", "chrome.settings.effects", "glass", y, pitch, ctop, () => Sound.EffectsVolume, v => Sound.EffectsVolume = v);
            y += pitch;

            // SOUND, as a choice: OFF / ON (the switch it was stepped Muted; a segment sets it).
            var snd = SettingsRow(page, "SOUND", UIText.T("chrome.settings.sound"), "speaker", y, pitch, false, null);
            string off = UIText.T("chrome.settings.off"), on = UIText.T("chrome.settings.on");
            float left = ChoiceControl(snd.Rt, "MUTE", new[] { off, on }, ChoiceSeg(new[] { off, on }), SetCtrlRight, ctop,
                () => Sound.Muted ? 0 : 1, i => Sound.Muted = i == 0);   // the click after it is audible iff it came back on
            FitRowName(snd, left);
            y += pitch;

            // THE SONG IS A KEY, AND THE KEY OPENS THE LIST (2026-09-16, the author: "now playing kısmında tüm
            // şarkıları açılan bir combobox ile görüntüleyip istenilen seçilebilmeli"): the title on a Night[0] well with a
            // chevron at its end; pressed, every song the bar owns unrolls ABOVE it, in the order the moods play them,
            // the one playing lit. Which list it is on and its place in it is the page's hint in the strip. The transport
            // keys stand before it.
            var now = SettingsRow(page, "NOW PLAYING", UIText.T("chrome.settings.now_playing"), "note", y, pitch, false, null);
            float sx = SetCtrlRight - SongWellW;
            _settingsSongKey = MenuRecess(now.Rt, "SONG", sx, ctop, SongWellW, SetCtrlH, UITheme.Night[0]).rectTransform;
            var songImg = _settingsSongKey.GetComponent<Image>();
            songImg.raycastTarget = true;
            var songBtn = _settingsSongKey.gameObject.AddComponent<Button>();
            songBtn.transition = Selectable.Transition.None;
            songBtn.targetGraphic = songImg;
            songBtn.onClick.AddListener(() => { Sfx.Play("click"); ToggleSongList(!_songListOpen); });
            _settingsNowTitle = NewText("Title", _settingsSongKey, _body, 16, TextAnchor.MiddleLeft, UITheme.Cyan[4]);
            FieldRect(_settingsNowTitle.rectTransform, 14f, -1f, SongWellW - 14f - 40f, SetCtrlH);
            _settingsNowTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            _songDrop = new NeonIcons.SmallView(_settingsSongKey, "Drop", "down", new Vector2(0f, 1f),
                new Vector2(SongWellW - 34f + NeonIcons.SmallSize, -SetCtrlH * 0.5f));
            _songDrop.Show(NeonIcons.State.Half, UITheme.Cyan, false);
            int hw = Mathf.RoundToInt(SongWellW / 2f), hh = Mathf.RoundToInt(SetCtrlH / 2f);
            _songHover = new NeonTube(_settingsSongKey, "Pointed", l => ChromeArt.NeonPath(hw, hh, 1, 0, l),
                new Vector2((hw + 2 * ChromeArt.NeonPad) * 2f, (hh + 2 * ChromeArt.NeonPad) * 2f), new Vector2(0.5f, 0.5f), Vector2.zero);
            _songHover.Show(NeonIcons.State.Lit, UITheme.ClubBlue, true);
            _songHover.Visible = false;
            var songRelay = _settingsSongKey.gameObject.AddComponent<HoverRelay>();
            songRelay.Entered = () => { _songHover.Visible = true; _songDrop.Show(NeonIcons.State.Lit, UITheme.Cyan, false); SettingsTick(); };
            songRelay.Exited = () => { _songHover.Visible = false; _songDrop.Show(NeonIcons.State.Half, UITheme.Cyan, false); };
            float kx = sx - 12f - SetCtrlH;
            SmallMarkKey(now.Rt, "NEXT", "next", kx, ctop, () => { Sfx.SkipTrack(+1); Sfx.Play("click"); RefreshSettings(); });
            kx -= SetCtrlH + 8f;
            _settingsHoldKey = SmallMarkKey(now.Rt, "HOLD", "hold", kx, ctop, () => { Sfx.MusicPaused = !Sfx.MusicPaused; Sfx.Play("click"); RefreshSettings(); });
            _holdMark = _settingsHoldKey.Icon;
            _playMark = new NeonIcons.SmallView(_settingsHoldKey.Body, "Play", "play", new Vector2(0.5f, 0.5f), Vector2.zero);
            _playMark.Visible = false;
            kx -= SetCtrlH + 8f;
            SmallMarkKey(now.Rt, "PREV", "prev", kx, ctop, () => { Sfx.SkipTrack(-1); Sfx.Play("click"); RefreshSettings(); });
            FitRowName(now, kx);
            BuildSongList(now.Rt, sx, ctop);
            y += pitch;

            // THE TRACK, SEEKABLE (the author: "şarkıyı ileri saran bir player olmalı"): a tube the pointer presses or
            // drags through the song, the clamp riding it, the clock after it - read off the player every frame the
            // window is up (StepSeek), except while the hand is on it.
            var track = SettingsRow(page, "TRACK", UIText.T("chrome.settings.track"), "note", y, pitch, true, null);
            _seekClock = NewText("Clock", track.Rt, _body, 16, TextAnchor.MiddleRight, UITheme.Cream[3]);
            _seekClock.horizontalOverflow = HorizontalWrapMode.Overflow;
            float clockW = SnapUp(WidestWord(new[] { UIText.T("chrome.settings.clock", ("at", "00:00"), ("of", "00:00")) }), 2f);
            FieldRect(_seekClock.rectTransform, SetCtrlRight - clockW - 8f, -1f, clockW + 8f, pitch);
            float bx = SetCtrlRight - clockW - 24f - SeekLen * 2f;
            float glass = Mathf.Floor(pitch / 4f) * 2f;                     // the glass row, even (30 of 62)
            _seek = MeterTube(track.Rt, "Seek", bx, glass, SeekLen, false);
            var seek = NewRect("SeekHit", track.Rt);
            FieldRect(seek, bx, glass - 11f, SeekLen * 2f, 24f);
            var seekHit = seek.gameObject.AddComponent<Image>();
            seekHit.color = new Color(0f, 0f, 0f, 0f);    // the whole 24 answers the hand, not the tube's 2 of glass
            seekHit.raycastTarget = true;
            var trig = seek.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(e => { _seekDragging = true; SeekTo(seek, (PointerEventData)e); });
            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(e => SeekTo(seek, (PointerEventData)e));
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(e => { SeekTo(seek, (PointerEventData)e); _seekDragging = false; });
            trig.triggers.Add(down); trig.triggers.Add(drag); trig.triggers.Add(up);
            FitRowName(track, bx - 8f);
            PaintSeek(0f);
            return page;
        }

        /// <summary>A level's row: [-] the tube [+] and the level in cyan against the right; steps of a tenth.</summary>
        private void MeterRow(RectTransform page, string id, string nameKey, string mark, float y, float pitch, float ctop,
            Func<float> get, Action<float> set)
        {
            var row = SettingsRow(page, id, UIText.T(nameKey), mark, y, pitch, false, null);
            const float ValueW = 64f;
            var value = NewText("Pct", row.Rt, _body, 16, TextAnchor.MiddleRight, UITheme.Cyan[4]);
            FieldRect(value.rectTransform, SetCtrlRight - ValueW, -1f, ValueW, pitch);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            float plusX = SetCtrlRight - ValueW - 12f - SetCtrlH;
            SmallMarkKey(row.Rt, "PLUS", "plus", plusX, ctop, () =>
            {
                set(Mathf.Clamp01(Mathf.Round((get() + 0.1f) * 10f) / 10f)); Sfx.Play("click"); RefreshSettings();
            });
            float tx = plusX - 16f - MeterLen * 2f;
            var meter = MeterTube(row.Rt, "Meter", tx, ctop + SetCtrlH * 0.5f - 1f, MeterLen, true);   // glass on row 30 of 62
            meter.Value = value;
            float minusX = tx - 16f - SetCtrlH;
            SmallMarkKey(row.Rt, "MINUS", "minus", minusX, ctop, () =>
            {
                set(Mathf.Clamp01(Mathf.Round((get() - 0.1f) * 10f) / 10f)); Sfx.Play("click"); RefreshSettings();
            });
            FitRowName(row, minusX);
            _setMeters.Add(meter);
            _meterLevels.Add(get);
        }

        private readonly List<Func<float>> _meterLevels = new List<Func<float>>();

        /// <summary>The list of every song, closed until the song key opens it: a Night[0] well standing on the key's top,
        /// one row a song - its title, and small at the right which list it is on.</summary>
        private void BuildSongList(RectTransform row, float x, float keyTop)
        {
            var songs = Sfx.AllSongs;
            float h = songs.Count * SongRowH + 8f;
            var well = MenuRecess(row, "SongList", x, keyTop - 4f - h, SongWellW, h, UITheme.Night[0]);
            well.raycastTarget = true;
            _songList = well.rectTransform;
            _songRows.Clear();
            for (int i = 0; i < songs.Count; i++)
            {
                string song = songs[i];
                var r = NewRect("S_" + song, _songList);
                FieldRect(r, 4f, 4f + i * SongRowH, SongWellW - 8f, SongRowH);
                var bg = r.gameObject.AddComponent<Image>();
                bg.color = UITheme.Night[2];
                bg.enabled = false;
                var hit = NewRect("Hit", r);
                Stretch(hit, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var hitImg = hit.gameObject.AddComponent<Image>();
                hitImg.color = new Color(0f, 0f, 0f, 0f);
                hitImg.raycastTarget = true;
                var relay = hit.gameObject.AddComponent<HoverRelay>();
                relay.Entered = () => bg.enabled = true;
                relay.Exited = () => bg.enabled = false;
                var btn = hit.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = hitImg;
                btn.onClick.AddListener(() =>
                {
                    Sfx.PlaySong(song); Sfx.Play("click");
                    ToggleSongList(false);
                    RefreshSettings();
                });
                var title = NewText("Title", r, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
                FieldRect(title.rectTransform, 8f, -1f, SongWellW - 96f, SongRowH);
                title.horizontalOverflow = HorizontalWrapMode.Overflow;
                title.text = SongTitle(song);
                if (title.preferredWidth > SongWellW - 104f) title.fontSize = LanguageFonts.Size(title.font, 8);
                _songRows[song] = title;
                int cut = song.LastIndexOf('_');
                var mood = NewText("Mood", r, _body, 8, TextAnchor.MiddleRight, UITheme.Cream[3]);
                FieldRect(mood.rectTransform, SongWellW - 96f, 0f, 84f, SongRowH);
                mood.horizontalOverflow = HorizontalWrapMode.Overflow;
                mood.text = MoodWord(cut < 0 ? song : song.Substring(0, cut));
                hit.SetAsLastSibling();
            }
            _songList.gameObject.SetActive(false);
        }

        private void ToggleSongList(bool open)
        {
            _songListOpen = open && _songList != null;
            if (_songList != null) _songList.gameObject.SetActive(_songListOpen);
        }

        private void SeekTo(RectTransform bar, PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bar, e.position, e.pressEventCamera, out var local)) return;
            float p = Mathf.Clamp01(local.x / (SeekLen * 2f));
            Sfx.MusicProgress = p;
            PaintSeek(p);
        }

        private void PaintSeek(float p)
        {
            if (_seek == null) return;
            PaintMeterTube(_seek, p, false);
            var (at, length) = Sfx.MusicClock;
            _seekClock.text = length <= 0f ? "" : UIText.T("chrome.settings.clock", ("at", Clock(at)), ("of", Clock(length)));
        }

        private static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>The seek bar follows the song while the window is up and the hand is off it; a new song redraws the
        /// title and the strip's place.</summary>
        private void StepSeek()
        {
            if (_seek == null || _settingsPage != "AUDIO" || _seekDragging) return;
            if (_shownSong != Sfx.NowPlaying) RefreshSettings();
            PaintSeek(Sfx.MusicProgress);
        }

        // ── CONTROLS ─────────────────────────────────────────────────────────────────────────────────────────────

        private RectTransform BuildControlsPage()
        {
            var page = NewSettingsPage("CONTROLS");
            var binds = new[] {
                (KeyAction.Pause, "chrome.bind.pause", "cog"), (KeyAction.Book, "chrome.bind.book", "book"),
                (KeyAction.Cellar, "chrome.bind.cellar", "bottle"), (KeyAction.PageBack, "chrome.bind.pages", "book"),
                (KeyAction.PageForward, "chrome.bind.pages", "book"), (KeyAction.NextTrack, "chrome.bind.next_track", "note"),
                (KeyAction.MusicToggle, "chrome.bind.music_toggle", "speaker") };
            var (pitch, top) = RowPitch(binds.Length + 1, 48f);
            float ctop = (pitch - SetCtrlH) * 0.5f;
            float capTop = Mathf.Floor((pitch - CapH) / 4f) * 2f;          // 6 of 46: the cap on the even grid
            float y = top;
            foreach (var (action, key, mark) in binds)
            {
                var row = SettingsRow(page, action.ToString(), UIText.T(key), mark, y, pitch, false, null);
                y += pitch;
                FitRowName(row, SetCtrlRight - 78f - 16f - 160f);          // the widest cap (EMPTY2, 78) and the hint's room
                // the book's pages carry their direction as the house's chevron mark (the pixel face has no arrows)
                if (action == KeyAction.PageBack || action == KeyAction.PageForward)
                {
                    float nameW = Mathf.Ceil(row.Name.preferredWidth);
                    var chev = FieldFill(row.Rt, "Way", SetNameX + nameW + 10f, Mathf.Floor((pitch - 16f) / 4f) * 2f, 16f, 16f, UITheme.Cream[3]);
                    chev.sprite = ChromeArt.Mark("chevron_left");
                    if (action == KeyAction.PageForward) chev.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                    chev.rectTransform.pivot = new Vector2(0.5f, 1f);
                    chev.rectTransform.anchoredPosition += new Vector2(8f, 0f);
                }
                var a = action;
                // THE CAP (2026-09-15, KeyCaps): the author's drawing of the key the action is on, at 2x, against the
                // row's right; a key the pack has no cap for gets its word printed on the blank cap. Click it and the
                // row listens for the next key - the row lit as pointed, the cap in its pressed frame, PRESS A KEY in cyan.
                var cap = NewRect("Cap", row.Rt);
                cap.anchorMin = cap.anchorMax = new Vector2(0f, 1f);
                cap.pivot = new Vector2(1f, 1f);
                cap.sizeDelta = new Vector2(34f, CapH);
                cap.anchoredPosition = new Vector2(SetCtrlRight, -capTop);
                var ci = cap.gameObject.AddComponent<Image>();
                ci.color = Color.white; ci.raycastTarget = true;
                var btn = cap.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = ci;
                btn.onClick.AddListener(() =>
                {
                    _bindListening = _bindListening == a ? null : a;
                    Sfx.Play("click");
                    RefreshSettings();
                });
                var sink = cap.gameObject.AddComponent<PressSink>();
                sink.Face = cap; sink.Depth = 0f; sink.Lift = 2f; sink.Squash = 0f; sink.Bloom = 0f;
                UiAuditExempt.Mark(cap, "a key cap of the author's Classic set, 17x16 (or wider) shown at exactly 2x");
                var word = NewText("Word", cap, _body, 16, TextAnchor.MiddleCenter, KeyCaps.Ink);
                Stretch(word.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, 0f));
                word.horizontalOverflow = HorizontalWrapMode.Overflow;
                word.text = "";
                var hint = NewText("Hint", row.Rt, _body, 8, TextAnchor.MiddleRight, UITheme.Cyan[4]);
                FieldRect(hint.rectTransform, SetCtrlRight - 34f - 16f - 300f, -1f, 300f, pitch);
                hint.horizontalOverflow = HorizontalWrapMode.Overflow;
                hint.text = "";
                _bindCaps[action] = ci;
                _bindWords[action] = word;
                _bindHints[action] = hint;
                _bindRows[action] = row;
            }

            // INVERT POUR (2026-09-26, the author's "ters mouse"): the one axis the mouse has in this game is the
            // pour's lean, read off how far the hand has risen (PourHand) - every other verb follows the pointer
            // where it is, and an inverted pointer would put the bottle on one side of the screen and the hand on
            // the other. Inverted, the bottle is lifted over the glass upright and LOWERED to tip. A choice now, under
            // the seven keys; its note is the strip's while the pointer is on it.
            var inv = SettingsRow(page, "INVERT", UIText.T("chrome.settings.invert_pour"), "bottle", y, pitch, true,
                () => UIText.T("chrome.settings.invert_pour_note"));
            string off = UIText.T("chrome.settings.off"), on = UIText.T("chrome.settings.on");
            float left = ChoiceControl(inv.Rt, "INVERT", new[] { off, on }, ChoiceSeg(new[] { off, on }), SetCtrlRight, ctop,
                () => PlayerOptions.InvertPour ? 1 : 0, i => PlayerOptions.InvertPour = i == 1);
            FitRowName(inv, left);
            return page;
        }

        /// <summary>One cap's drawing: the frame for the key the action is on, up or pressed, the cap widened to the
        /// drawing (a wide key is a wide cap), and the word on a blank cap — the small face when the word is long.</summary>
        private void PaintCap(KeyAction action, bool pressed)
        {
            if (!_bindCaps.TryGetValue(action, out var img)) return;
            var key = Keys.Get(action);
            var word = _bindWords[action];
            var sprite = KeyCaps.Cap(key, pressed, out bool blank);
            if (sprite == null)
            {
                img.enabled = false;           // no caps at all: the word alone
                word.text = Keys.Label(key);
                return;
            }
            img.enabled = true;
            img.sprite = sprite;
            float w = sprite.rect.width * 2f;
            var rt = img.rectTransform;
            if (!Mathf.Approximately(rt.sizeDelta.x, w)) rt.sizeDelta = new Vector2(w, CapH);
            word.text = blank ? Keys.Label(key) : "";
            if (blank)
            {
                word.fontSize = LanguageFonts.Size(word.font, 16);
                if (word.preferredWidth > w - 12f) word.fontSize = LanguageFonts.Size(word.font, 8);
            }
            // the pressed frame stands two units lower; the word goes with it
            Stretch(word.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, pressed ? 0f : 2f), new Vector2(-4f, pressed ? -2f : 0f));
            if (_bindHints.TryGetValue(action, out var hint))
            {
                var hr = hint.rectTransform;
                hr.anchoredPosition = new Vector2(SetCtrlRight - w - 16f - hr.sizeDelta.x, hr.anchoredPosition.y);
            }
        }

        /// <summary>Every frame the window is up: the caps follow the keyboard (the real key held → the cap pressed; a
        /// listening row holds its cap pressed - still, not blinking, since 2026-09-29: FLASHES off promises nothing
        /// blinks), and a row that listens takes the next key (Escape cancels).</summary>
        private void StepSettings()
        {
            if (_settingsPanel == null || !_settingsPanel.gameObject.activeSelf) return;
            StepSeek();
            // Alt+Enter with the window up (2026-09-26): the WINDOW and RESOLUTION rows follow the screen.
            if (_settingsPage == "DISPLAY" && _shownWindowed != DisplayOptions.Windowed) RefreshSettings();
            var kb = Keyboard.current;
            foreach (var pair in _bindCaps)
            {
                bool listening = _bindListening == pair.Key;
                bool down = listening || (kb != null && KeyHeld(kb, Keys.Get(pair.Key)));
                if (_capDown.TryGetValue(pair.Key, out var was) && was == down) continue;
                _capDown[pair.Key] = down;
                PaintCap(pair.Key, down);
            }
            if (_bindListening == null) return;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { _bindListening = null; RefreshSettings(); return; }
            var key = Keys.AnyPressed();
            if (key == Key.None) return;
            Keys.Set(_bindListening.Value, key);
            _bindListening = null;
            Sfx.Play("key_press", 0.6f);
            RefreshSettings();
        }

        private static bool KeyHeld(Keyboard kb, Key key)
        {
            if (key == Key.None) return false;
            try { return kb[key].isPressed; }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        // ── DISPLAY ──────────────────────────────────────────────────────────────────────────────────────────────

        private RectTransform BuildDisplayPage()
        {
            var page = NewSettingsPage("DISPLAY");
            var (pitch, top) = RowPitch(10, 40f);
            float ctop = (pitch - SetCtrlH) * 0.5f;
            float y = top;
            string T(string k) => UIText.T("chrome.settings." + k);

            // ONE SEGMENT WIDTH FOR THE PAGE (BUILD_SPEC §3): every choice on it is cut to the widest way any of them
            // offers, so the six wells stand as one column of equal cells.
            var ways = new[]
            {
                new[] { T("window_full"), T("window_windowed") }, new[] { T("full"), T("reduced") },
                new[] { T("off"), T("on") }, new[] { T("normal"), T("large") },
                new[] { T("colours_standard"), T("colours_clear") }, new[] { T("off"), T("on") },
            };
            var all = new List<string>();
            foreach (var w in ways) all.AddRange(w);
            float seg = ChoiceSeg(all);

            SetRow Row(string id, string nameKey, string mark, string noteKey, Func<string> dynamicNote = null)
            {
                var r = SettingsRow(page, id, T(nameKey), mark, y, pitch, id == "START OVER",
                    dynamicNote ?? (noteKey != null ? (Func<string>)(() => T(noteKey)) : null));
                y += pitch;
                return r;
            }

            // THE SCREEN (2026-09-26, the author: "display kısmında çözünürlük"). FULLSCREEN is the borderless
            // window at the desktop's size; WINDOWED opens at the largest whole multiple of 640x360 the desktop holds,
            // and those are the only sizes offered - at a whole multiple every stage pixel stays square
            // (DisplayOptions). Applied in a player's build only; the editor remembers the choice and leaves the
            // Game view the suites pin at 1280x720 alone.
            var win = Row("WINDOW", "window", "win_max", "window_note");
            FitRowName(win, ChoiceControl(win.Rt, "WINDOW", ways[0], seg, SetCtrlRight, ctop,
                () => DisplayOptions.Windowed ? 1 : 0, i => DisplayOptions.SetWindowed(i == 1)));
            var desk = DisplayOptions.Desktop;
            var sizeWords = new List<string> { SizeWord(desk) };
            foreach (var s in WindowChoices()) sizeWords.Add(SizeWord(s));
            var res = Row("RESOLUTION", "resolution", "room", null,
                () => T(DisplayOptions.Windowed ? "resolution_note" : "resolution_desktop"));
            FitRowName(res, CycleControl(res.Rt, "Size", sizeWords, SetCtrlRight, ctop,
                () => SizeWord(DisplayOptions.Windowed ? DisplayOptions.WindowSize : DisplayOptions.Desktop),
                () => DisplayOptions.Windowed && WindowChoices().Count > 1, StepWindowSize));
            var rateWords = new List<string>();
            foreach (int cap in DisplayOptions.FrameCaps) rateWords.Add(FrameWord(cap));
            var rate = Row("FRAME RATE", "frame_rate", "clock", "frame_note");
            FitRowName(rate, CycleControl(rate.Rt, "Rate", rateWords, SetCtrlRight, ctop,
                () => FrameWord(PlayerOptions.FrameCap), () => true, StepFrameCap));

            // WHAT MOVES AND WHAT FLASHES.
            var mot = Row("MOTION", "motion", "redo", "motion_note");
            FitRowName(mot, ChoiceControl(mot.Rt, "MOTION", ways[1], seg, SetCtrlRight, ctop,
                () => Motion.Reduced ? 1 : 0, i => Motion.Reduced = i == 1));
            var fl = Row("FLASHES", "flashes", "flash", "flashes_note");
            FitRowName(fl, ChoiceControl(fl.Rt, "FLASHES", ways[2], seg, SetCtrlRight, ctop,
                () => PlayerOptions.Flashes ? 1 : 0, i => PlayerOptions.Flashes = i == 1));

            // THE HAND, THE COLOURS, THE FOCUS.
            var ptr = Row("POINTER", "pointer", "rise", "pointer_note");
            FitRowName(ptr, ChoiceControl(ptr.Rt, "POINTER", ways[3], seg, SetCtrlRight, ctop,
                () => PlayerOptions.Pointer == PlayerOptions.PointerSize.Large ? 1 : 0,
                i => PlayerOptions.Pointer = i == 1 ? PlayerOptions.PointerSize.Large : PlayerOptions.PointerSize.Normal));
            var col = Row("COLOURS", "colours", "mix", "colours_note");
            FitRowName(col, ChoiceControl(col.Rt, "COLOURS", ways[4], seg, SetCtrlRight, ctop,
                () => PlayerOptions.Cues == PlayerOptions.ColourCues.Clear ? 1 : 0,
                i => PlayerOptions.Cues = i == 1 ? PlayerOptions.ColourCues.Clear : PlayerOptions.ColourCues.Standard));
            var away = Row("PAUSE AWAY", "pause_away", "pause", "pause_away_note");
            FitRowName(away, ChoiceControl(away.Rt, "PAUSE AWAY", ways[5], seg, SetCtrlRight, ctop,
                () => PlayerOptions.PauseWhenAway ? 1 : 0, i => PlayerOptions.PauseWhenAway = i == 1));

            // THE RUN'S OWN VERBS. TONIGHT'S BOOK opens the ledger (dark glass from the front door: there is no night
            // behind the door to read, and the book would open under it).
            var book = Row("BOOK", "book", "cash", "book_note");
            _settingsBookKey = RowWordKey(book.Rt, "OPEN", T("open"), new[] { T("open") }, SetCtrlRight, ctop,
                () => { ToggleSettings(); if (Showing(_pausePanel)) TogglePause(); ToggleLedger(); });
            FitRowName(book, SetCtrlRight - ((RectTransform)_settingsBookKey.transform).sizeDelta.x);
            // START OVER ASKS FIRST (2026-09-29, the critic: it lost its inline warning to the strip, which only shows on
            // hover, and still threw the night away on one press) - the pause's NEW RUN's two presses: the first says
            // SURE? PRESS AGAIN round a ViceRed tube, the second deals a fresh bar on a fresh seed and clears the save
            // (2026-09-26: every player used to restart into the inspector's one run). The key is fitted to the longer
            // of its two words, so it never grows when it asks. From the front door it is the door's own NEW RUN.
            var fresh = Row("START OVER", "start_over", "moon", "start_over_note");
            string ask = UIText.T("chrome.pause.new_run_sure");
            _settingsStartOver = RowWordKey(fresh.Rt, "NEW RUN", T("new_run"), new[] { T("new_run"), ask }, SetCtrlRight, ctop, () =>
            {
                bool fromDoor = _settingsFromMenu;
                ToggleSettings(); if (Showing(_pausePanel)) TogglePause();
                if (fromDoor) HideMainMenu();
                _bootstrap?.StartFreshRun(SeedPolicy.Next());
            });
            _settingsStartOver.AskWord = ask;
            FitRowName(fresh, SetCtrlRight - ((RectTransform)_settingsStartOver.transform).sizeDelta.x);
            return page;
        }

        /// <summary>The window sizes the RESOLUTION row steps through: the whole multiples the desktop holds, or - on
        /// a desktop too small for any - the one size WINDOWED opens at.</summary>
        private static List<Vector2Int> WindowChoices()
        {
            var desk = DisplayOptions.Desktop;
            var sizes = DisplayOptions.WholeSizes(desk.x, desk.y);
            if (sizes.Count == 0) sizes.Add(DisplayOptions.WindowFor(desk.x, desk.y));
            return sizes;
        }

        private static string SizeWord(Vector2Int size) =>
            UIText.T("chrome.settings.resolution_value", ("w", size.x), ("h", size.y));

        private static string FrameWord(int cap) =>
            cap <= 0 ? UIText.T("chrome.settings.frame_match") : UIText.T("chrome.settings.frame_cap", ("fps", cap));

        /// <summary>The next or the previous whole size, round the ends; a window at a size of its own (a player's
        /// build started with -screen-width) steps to the nearest one first.</summary>
        private static void StepWindowSize(int step)
        {
            if (!DisplayOptions.Windowed) return;
            var sizes = WindowChoices();
            if (sizes.Count < 2) return;
            int at = sizes.IndexOf(DisplayOptions.WindowSize);
            if (at < 0)
            {
                at = 0;
                for (int i = 1; i < sizes.Count; i++)
                    if (Mathf.Abs(sizes[i].y - DisplayOptions.WindowSize.y) < Mathf.Abs(sizes[at].y - DisplayOptions.WindowSize.y)) at = i;
            }
            else at = (at + step + sizes.Count) % sizes.Count;
            DisplayOptions.SetWindowSize(sizes[at]);
        }

        /// <summary>MATCH SCREEN, 60, 120, 144, round the ends; applied at once (in a player's build).</summary>
        private static void StepFrameCap(int step)
        {
            var caps = DisplayOptions.FrameCaps;
            int at = Array.IndexOf(caps, PlayerOptions.FrameCap);
            if (at < 0) at = 0;
            PlayerOptions.FrameCap = caps[(at + step + caps.Length) % caps.Length];
            DisplayOptions.ApplyPacing();
        }

        /// <summary>
        /// PAUSE WHEN AWAY (2026-09-26): another window comes up over the game and the night is held behind the pause
        /// menu, the way Escape holds it - the bar went on serving while the player was alt-tabbed (runInBackground
        /// keeps the room alive, and it should: the music, the rain). Only an open night with nothing already holding
        /// the clock; the menu stays up when the game comes back, and RESUME lets it go. Never in the editor
        /// (PlayerOptions.PausesWhenAway): the author drives it from the IDE, and the suite ignores focus.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus || !PlayerOptions.PausesWhenAway) return;
            if (_pausePanel == null || Showing(_pausePanel) || Paused || _bindListening != null) return;
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            TogglePause();
        }

        // ── LANGUAGE ─────────────────────────────────────────────────────────────────────────────────────────────

        private const float LangCellW = 256f, LangCellH = 36f, LangGapX = 8f, LangGapY = 4f;
        private const int LangRows = 10;

        /// <summary>
        /// THE LANGUAGE LIST (2026-09-29, BUILD_SPEC §3 "LANGUAGE"): every language this build has a table for, in
        /// Languages order, down three columns of ten - each a cell with its flag (the author's, 2026-09-15: "bayrakla
        /// dil seçimi"; shown 1:1) and its own name in a face that draws it (LanguageFonts.ListFace), so the list is
        /// readable without a hover tip (the flags' tips drew under the window: read_menus finding 7). The pick wears a
        /// cyan tube, the cell under the pointer a ClubBlue one, the language spoken now a cyan bead at its right end.
        /// A name's bracket - BRASIL, ESPAÑA, LATINOAMÉRICA - is the flag's to say (the critic: stacked under the name
        /// in a 36 cell it touched the rim); the whole name is the strip's while the pointer is on the cell. The tube
        /// lies UNDER the flag, so its light never washes over the flag (the critic's other catch).
        /// </summary>
        private RectTransform BuildLanguagePage()
        {
            var page = NewSettingsPage("LANGUAGE");
            var all = Localization.Available;
            float y0 = Mathf.Floor((SetPageH - (LangRows * LangCellH + (LangRows - 1) * LangGapY)) / 4f) * 2f;
            var house = bodyFont != null ? bodyFont : _body;
            _langCells.Clear();
            for (int k = 0; k < all.Count; k++)
            {
                var info = all[k];
                string code = info.Code;
                int c = k / LangRows, r = k % LangRows;
                var rt = NewRect("Lang_" + code, page);
                FieldRect(rt, 16f + c * (LangCellW + LangGapX), y0 + r * (LangCellH + LangGapY), LangCellW, LangCellH);
                var plate = rt.gameObject.AddComponent<Image>();
                plate.sprite = MenuArt.KeyPlate(UITheme.Night[2], UITheme.Graphite[3], UITheme.Graphite[4]);
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = 0.5f;
                plate.raycastTarget = true;
                var btn = rt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = plate;
                btn.onClick.AddListener(() => { Localization.Choose(code); Sfx.Play("click"); RefreshSettings(); });

                var cell = new LangCell { Code = code, Name = (info.Name ?? code).ToUpperInvariant() };
                int tw = Mathf.RoundToInt(LangCellW / 2f), th = Mathf.RoundToInt(LangCellH / 2f);
                cell.Tube = new NeonTube(rt, "Tube", l => ChromeArt.NeonPath(tw, th, 1, 0, l),
                    new Vector2((tw + 2 * ChromeArt.NeonPad) * 2f, (th + 2 * ChromeArt.NeonPad) * 2f), new Vector2(0.5f, 0.5f), Vector2.zero);
                cell.Tube.Visible = false;
                var flag = FieldFill(rt, "Flag", 4f, 2f, 48f, 33f, Color.white);
                flag.sprite = ItemArt.Load("fl_" + LanguageFlag(code));
                UiAuditExempt.Mark(flag, "a language's flag, the author's 48x33 drawing shown 1:1");

                string main = cell.Name;
                int bracket = main.IndexOf('(');
                if (bracket > 0) main = main.Substring(0, bracket).Trim();
                var (face, size) = LanguageFonts.ListFace(code, cell.Name, house, 16);
                cell.Word = NewText("Name", rt, face, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
                cell.Word.font = face;
                cell.Word.fontSize = size;
                // the name from 58 (six past the flag) to 18 short of the cell's end, where the bead's glass stands:
                // BAHASA INDONESIA, the longest, is 176 of its 180 (settings_fit, 2026-09-29)
                const float NameX = 58f, NameW = LangCellW - NameX - 18f;
                FieldRect(cell.Word.rectTransform, NameX, -1f, NameW, LangCellH);
                cell.Word.horizontalOverflow = HorizontalWrapMode.Overflow;
                cell.Word.text = main;
                if (cell.Word.preferredWidth > NameW) cell.Word.fontSize = LanguageFonts.Size(face, 8);

                cell.Bead = new NeonTube(rt, "Now", MenuArt.Bead, new Vector2((1 + 2 * ChromeArt.NeonPad) * 2f, (1 + 2 * ChromeArt.NeonPad) * 2f),
                    new Vector2(0f, 1f), new Vector2(LangCellW - 12f, -(LangCellH * 0.5f - 1f)));   // its glass 243..245
                cell.Bead.Show(NeonIcons.State.Lit, UITheme.Cyan, true);
                cell.Bead.Visible = false;

                var relay = rt.gameObject.AddComponent<HoverRelay>();
                relay.Entered = () => { cell.Over = true; _noteCell = cell; SettingsTick(); PaintLangCell(cell); PaintSettingsNote(); };
                relay.Exited = () =>
                {
                    cell.Over = false; PaintLangCell(cell);
                    if (_noteCell == cell) { _noteCell = null; PaintSettingsNote(); }
                };
                _langCells.Add(cell);
            }
            return page;
        }

        private void PaintLangCell(LangCell cell)
        {
            string pick = Localization.PreferredCode();
            bool picked = cell.Code == pick;
            if (picked) { cell.Tube.Visible = true; cell.Tube.Show(NeonIcons.State.Lit, UITheme.Cyan, true); }
            else if (cell.Over) { cell.Tube.Visible = true; cell.Tube.Show(NeonIcons.State.Lit, UITheme.ClubBlue, true); }
            else cell.Tube.Visible = false;
            cell.Word.color = picked ? UITheme.Cyan[4] : UITheme.Cream[4];
            cell.Bead.Visible = cell.Code == Localization.Current.Code;
        }

        // ── the strip ────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE STRIP (BUILD_SPEC §2 "Note strip"): the pointed row's note, the pointed language's whole name in its own
        /// face, else the page's hint - AUDIO: which list the song is on and its place in it; CONTROLS: how a key is
        /// bound; LANGUAGE: the hint, or - once a different language is picked - in THAT language and its face, that
        /// APPLY switches the bar at once (the proof it took).
        /// </summary>
        private void PaintSettingsNote()
        {
            if (_settingsNote == null) return;
            var house = bodyFont != null ? bodyFont : _body;
            Font font = _body;
            string text = "";
            if (_settingsPage == "LANGUAGE" && _noteCell != null)
            {
                var (face, _) = LanguageFonts.ListFace(_noteCell.Code, _noteCell.Name, house, 8);
                font = face;
                text = _noteCell.Name;
            }
            else if (_noteRow != null && _noteRow.Note != null && _noteRow.Rt.gameObject.activeInHierarchy)
                text = _noteRow.Note() ?? "";
            if (string.IsNullOrEmpty(text) && _noteCell == null)
            {
                switch (_settingsPage)
                {
                    case "AUDIO":
                    {
                        string now = Sfx.NowPlaying;
                        var (at, of) = Sfx.NowPlayingPlace;
                        int cut = now == null ? -1 : now.LastIndexOf('_');
                        string mood = now == null ? "" : cut < 0 ? now : now.Substring(0, cut);
                        text = of == 0 ? "" : UIText.T("build.player.place", ("mood", MoodWord(mood)), ("at", at), ("of", of));
                        break;
                    }
                    case "CONTROLS":
                        text = UIText.T("chrome.settings.controls_hint");
                        break;
                    case "LANGUAGE":
                    {
                        string pick = Localization.PreferredCode();
                        if (pick != Localization.Current.Code)
                        {
                            if (_languageNoteCode != pick)
                            {
                                _languageNoteCode = pick;
                                _languageNoteText = Localization.Load(pick).Get("chrome.settings.language_note_now");
                            }
                            text = _languageNoteText;
                            font = LanguageFonts.BodyFor(pick, house);
                        }
                        else text = UIText.T("chrome.settings.language_hint");
                        break;
                    }
                }
            }
            if (_settingsNote.font != font) _settingsNote.font = font;
            _settingsNote.fontSize = LanguageFonts.Size(font, 8);
            _settingsNote.text = text;
        }

        // ── refresh ──────────────────────────────────────────────────────────────────────────────────────────────

        private void RefreshSettings()
        {
            if (_settingsNote == null) return;
            for (int i = 0; i < _setMeters.Count; i++)
            {
                float level = _meterLevels[i]();
                PaintMeterTube(_setMeters[i], level, Sound.Muted);
                var value = _setMeters[i].Value;
                if (value == null) continue;
                // muted, the tubes go dark and the levels - kept for when the sound comes back - read in Cream[2]. Not the
                // word OFF the spec drew: it is 68-102 units in eight languages (DESLIGADO, WYŁĄCZONY, ИЗКЛЮЧЕН) and the
                // level's box is 64 (settings_fit, 2026-09-29); SOUND's own choice already says OFF.
                value.text = UIText.T("chrome.settings.volume_value", ("pct", Mathf.RoundToInt(level * 100)));
                value.color = Sound.Muted ? UITheme.Cream[2] : UITheme.Cyan[4];
            }
            _shownWindowed = DisplayOptions.Windowed;
            foreach (var choice in _setChoices) choice.Paint();
            foreach (var cycle in _setCycles)
            {
                bool live = cycle.Live();
                cycle.Value.text = cycle.Word();
                cycle.Value.color = live ? UITheme.Cyan[4] : UITheme.Cream[2];
                foreach (var key in new[] { cycle.Prev, cycle.Next })
                    if (key.Button.interactable != live) { key.Button.interactable = live; key.Apply(); }
            }
            if (_settingsNowTitle != null)
            {
                string now = Sfx.NowPlaying;
                _shownSong = now;
                _settingsNowTitle.text = SongTitle(now);
                _settingsNowTitle.fontSize = LanguageFonts.Size(_settingsNowTitle.font, 16);
                if (_settingsNowTitle.preferredWidth > SongWellW - 54f)
                    _settingsNowTitle.fontSize = LanguageFonts.Size(_settingsNowTitle.font, 8);
                if (_settingsHoldKey != null)
                {
                    // the hold key says what it will do: hold the record while it plays, play it while it is held
                    var want = Sfx.MusicPaused ? _playMark : _holdMark;
                    if (_settingsHoldKey.Icon != want)
                    {
                        _settingsHoldKey.Icon.Visible = false;
                        _settingsHoldKey.Icon = want;
                        want.Visible = true;
                        _settingsHoldKey.Apply();
                    }
                }
                foreach (var pair in _songRows) pair.Value.color = pair.Key == now ? UITheme.Cyan[4] : UITheme.Cream[4];
                PaintSeek(Sfx.MusicProgress);
            }
            _capDown.Clear();
            foreach (var pair in _bindHints)
            {
                bool listening = _bindListening == pair.Key;
                PaintCap(pair.Key, listening);
                pair.Value.text = listening ? UIText.T("chrome.settings.press_key") : "";
                if (_bindRows.TryGetValue(pair.Key, out var row) && row.Held != listening) { row.Held = listening; row.Paint(); }
            }
            if (_settingsBookKey != null && _settingsBookKey.Button.interactable == _settingsFromMenu)
            {
                _settingsBookKey.Button.interactable = !_settingsFromMenu;
                _settingsBookKey.Apply();
            }
            string pick = Localization.PreferredCode();
            foreach (var cell in _langCells) PaintLangCell(cell);
            LaySettingsFoot(pick != Localization.Current.Code);
            PaintSettingsNote();
        }
    }
}
