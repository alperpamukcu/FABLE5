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
    /// THE SETTINGS WINDOW, in the palm wall direction (2026-09-15, the author: "ayarlar key bind ses ... Menü ayarlar
    /// panellerinde daha çok görsellerden yararlanabilir daha yaratıcı olabilir"). Four pages under tabs on one framed
    /// night (NightPlate — the drawn night inside the frame, the room around it), every key the author's pack
    /// (MenuPack, "Butonlar içinde bu dosya yolundaki butonları kullan"):
    ///   AUDIO     the master, the music and the effects each on a ten-cell meter between the pack's - and + keys, the
    ///             sound switch (the pack's speaker, muted or not), and the player itself on a NOW PLAYING row
    ///   CONTROLS  every bound action with THE CAP OF THE KEY IT IS ON (KeyCaps, the author's Classic set at 2x, its
    ///             pressed frame while the real key is held); click the cap and the row listens for the next key,
    ///             the cap blinking meanwhile
    ///   DISPLAY   motion, and the run's own verbs (tonight's book, a new run)
    ///   LANGUAGE  the flags of every shipped language in a grid (the author: "Dil seçiminde çerçeveye olan dillerin
    ///             bayrakları gözüksün bayrağa tıklanarak dil seçilsin"); the chosen one stands on the green plate,
    ///             its name under the grid and, in that language, the note that it speaks at the next start
    /// The foot carries RESET DEFAULTS and the one orange key, BACK — to the pause menu when the window came from it.
    /// Every worded key and tab is fitted to its word (FitKey), so a long translation gets a long key.
    ///
    /// This replaced the one-page window of 2026-09-06 (BuildSettings in TycoonHud.Chrome.cs); its fields
    /// (_settingsPanel, _settingsMeter, _settingsVolume, _settingsMute, _settingsMotion, _settingsLanguage...) are the
    /// same ones, declared in TycoonHud.cs.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private const float SetW = 800f, SetH = 540f, SetPad = 44f, SetRow = 56f, CapH = 32f;
        // The audio page's rows are 50, not 56: six of them (three meters, the switch, the player, the track) have to
        // stand between the tabs and the foot, and a 32 key on a 50 row is still a key with room around it.
        private const float AudioRow = 50f, SeekW = 300f, SongRowH = 22f;
        private RectTransform _settingsSongKey, _songList, _seekFill, _seekKnob, _settingsApplyLanguage;
        private readonly Dictionary<string, Text> _songRows = new Dictionary<string, Text>();
        private Text _seekClock;
        private bool _songListOpen, _seekDragging;
        private readonly Dictionary<string, RectTransform> _settingsPages = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, RectTransform> _settingsTabs = new Dictionary<string, RectTransform>();
        private string _settingsPage = "AUDIO";
        private Image[] _settingsMusicMeter, _settingsEffectsMeter;
        private Text _settingsMusicPct, _settingsEffectsPct, _settingsNowTitle, _settingsNowPlace;
        private RectTransform _settingsHoldKey, _settingsMuteKey, _settingsMotionKey;
        private readonly Dictionary<KeyAction, Image> _bindCaps = new Dictionary<KeyAction, Image>();
        private readonly Dictionary<KeyAction, Text> _bindWords = new Dictionary<KeyAction, Text>();
        private readonly Dictionary<KeyAction, Text> _bindHints = new Dictionary<KeyAction, Text>();
        private readonly Dictionary<KeyAction, bool> _capDown = new Dictionary<KeyAction, bool>();
        private KeyAction? _bindListening;
        private readonly Dictionary<string, RectTransform> _flagKeys = new Dictionary<string, RectTransform>();

        /// <summary>One flag a language (Tools/flags.py draws them, LANGUAGE_ISOS): English flies the Union flag,
        /// the two Chinese tables theirs, the two Spanish and the two Portuguese each their own; the rest fly their
        /// own letters.</summary>
        private static string LanguageFlag(string code)
        {
            switch (code)
            {
                case "en": return "gb";
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
                RefreshSettings();
                if (!_settingsFromPause) Sfx.Play("menu_open", 0.7f);   // from the pause menu the wall is already down
                // THE NIGHT STOPS FOR THE SETTINGS TOO (2026-09-25, the author: "Settings açıldığında oyun durmalı").
                // From the pause menu it is already held; from the top bar's cog it ran on at full speed behind the
                // window, patience and all. It holds the clock the way the ladder's window does, and lets go of only
                // the hold it took.
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
                else Sfx.Play("menu_close", 0.6f);
                _settingsFromPause = false;
                if (_settingsHeldClock) { _settingsHeldClock = false; SetPaused(false); }
            }
            _settingsPanel.gameObject.SetActive(show);
        }

        /// <summary>The settings window took the clock when it opened (from the cog, not from the pause menu).</summary>
        private bool _settingsHeldClock;

        private void BuildSettings(RectTransform root)
        {
            _settingsPanel = NewRect("Settings", root);
            var canvas = _settingsPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 29;                 // with the pause menu, over the book; the market (22) never shows it
            _settingsPanel.gameObject.AddComponent<ForgivingRaycaster>();
            Stretch(_settingsPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // THE ROOM STAYS, DARKENED (2026-09-16, with the pause menu): the window stands over it under the house
            // scrim; a click on the scrim closes the window.
            var dim = NewRect("Dim", _settingsPanel);
            Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = MenuScrim;
            dimImg.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(ToggleSettings);

            var plate = BluePlate(_settingsPanel, "Plate", new Vector2(SetW, SetH));   // the ESC family's plate (2026-09-21)
            NightTitle(plate, UIText.T("chrome.settings.title"), -36f);
            SunsetRules(plate, -66f, SetW - 80f);

            // the tabs, each with a glyph of the pack, fitted to its word, laid left to right
            float tx = SetPad;
            foreach (var (id, key, glyph) in new[] {
                ("AUDIO", "chrome.settings.audio", "sound_on"), ("CONTROLS", "chrome.settings.controls", "gamepad"),
                ("DISPLAY", "chrome.settings.display", "expand"), ("LANGUAGE", "chrome.settings.language", "mail") })
            {
                string page = id;
                var tab = PackWordKey(plate, "Tab_" + id, UIText.T(key), glyph, MenuPack.Tone.Grey, new Vector2(0, 1), new Vector2(140, 38),
                    new Vector2(tx, -90f), () => { Sfx.Play("click"); ShowSettingsPage(page); }, 140f, 48f + 16f);
                _settingsTabs[id] = tab;
                tx += tab.sizeDelta.x + 8f;
            }

            _settingsPages["AUDIO"] = BuildAudioPage(plate);
            _settingsPages["CONTROLS"] = BuildControlsPage(plate);
            _settingsPages["DISPLAY"] = BuildDisplayPage(plate);
            _settingsPages["LANGUAGE"] = BuildLanguagePage(plate);

            // the foot: reset at one corner, the way back at the other; the dev bench beside reset in the editor
            var reset = PackWordKey(plate, "RESET", UIText.T("chrome.settings.reset"), "restart", MenuPack.Tone.Grey, new Vector2(0, 0), new Vector2(250, 46), new Vector2(SetPad, 26), () =>
            {
                Sound.Volume = 0.8f; Sound.MusicVolume = 1f; Sound.EffectsVolume = 1f; Sound.Muted = false;
                Motion.Reduced = false;
                Keys.ResetAll();
                Sfx.Play("click");
                RefreshSettings();
            }, 200f, 48f + 24f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PackWordKey(plate, "DEV", "DEV TOOLS", null, MenuPack.Tone.Grey, new Vector2(0, 0), new Vector2(110, 26),
                new Vector2(SetPad + reset.sizeDelta.x + 12f, 36), () => { ToggleSettings(); ToggleDevBench(); }, 100f, 16f);
#endif
            PackWordKey(plate, "BACK", UIText.T("chrome.settings.back"), "back", MenuPack.Tone.Orange, new Vector2(1, 0), new Vector2(180, 46),
                new Vector2(-SetPad, 26), () => { Sfx.Play("click"); ToggleSettings(); }, 140f, 48f + 24f);

            ShowSettingsPage("AUDIO");
            _settingsPanel.gameObject.SetActive(false);
        }

        private void ShowSettingsPage(string id)
        {
            _settingsPage = id;
            _bindListening = null;
            ToggleSongList(false);
            foreach (var pair in _settingsPages) pair.Value.gameObject.SetActive(pair.Key == id);
            foreach (var pair in _settingsTabs) RetoneWordKey(pair.Value, pair.Key == id ? MenuPack.Tone.Green : MenuPack.Tone.Grey);
            RefreshSettings();
        }

        // ── AUDIO ────────────────────────────────────────────────────────────────────────────────────────────────

        private RectTransform BuildAudioPage(RectTransform plate)
        {
            var page = NewRect("Page_AUDIO", plate);
            Stretch(page, Vector2.zero, Vector2.one, new Vector2(SetPad, 90f), new Vector2(-SetPad, -140f));
            float y = 0f;
            _settingsMeter = MeterRow(page, "MASTER", UIText.T("chrome.settings.master"), "speaker", ref y, () => Sound.Volume, v => Sound.Volume = v, out _settingsVolume, AudioRow);
            _settingsMusicMeter = MeterRow(page, "MUSIC", UIText.T("chrome.settings.music"), "note", ref y, () => Sound.MusicVolume, v => Sound.MusicVolume = v, out _settingsMusicPct, AudioRow);
            _settingsEffectsMeter = MeterRow(page, "EFFECTS", UIText.T("chrome.settings.effects"), "glass", ref y, () => Sound.EffectsVolume, v => Sound.EffectsVolume = v, out _settingsEffectsPct, AudioRow);

            var snd = SettingsRow(page, "SOUND", UIText.T("chrome.settings.sound"), "speaker", ref y, AudioRow);
            _settingsMuteKey = PackIconKey(snd, "MUTE", "sound_on", MenuPack.Tone.Green, new Vector2(0, 0.5f), new Vector2(300f, 0), () =>
            {
                Sound.Muted = !Sound.Muted;
                Sfx.Play("click");              // audible iff it just came back on — itself the test
                RefreshSettings();
            });
            _settingsMute = NewText("State", snd, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
            Place(_settingsMute.rectTransform, new Vector2(0, 0.5f), new Vector2(200, 20), new Vector2(344f, 0));
            _settingsMute.rectTransform.pivot = new Vector2(0, 0.5f);
            _settingsMute.horizontalOverflow = HorizontalWrapMode.Overflow;

            var now = SettingsRow(page, "NOW PLAYING", UIText.T("chrome.settings.now_playing"), "note", ref y, AudioRow);
            float kx = 300f;
            PackIconKey(now, "PREV", "prev", MenuPack.Tone.Grey, new Vector2(0, 0.5f), new Vector2(kx, 0), () => { Sfx.SkipTrack(-1); Sfx.Play("click"); });
            kx += 40f;
            _settingsHoldKey = PackIconKey(now, "HOLD", "pause", MenuPack.Tone.Grey, new Vector2(0, 0.5f), new Vector2(kx, 0), () => { Sfx.MusicPaused = !Sfx.MusicPaused; Sfx.Play("click"); RefreshSettings(); });
            kx += 40f;
            PackIconKey(now, "NEXT", "next", MenuPack.Tone.Grey, new Vector2(0, 0.5f), new Vector2(kx, 0), () => { Sfx.SkipTrack(+1); Sfx.Play("click"); });
            kx += 32f + 12f;
            // THE SONG IS A KEY, AND THE KEY OPENS THE LIST (2026-09-16, the author: "now playing kısmında tüm
            // şarkıları açılan bir combobox ile görüntüleyip istenilen seçilebilmeli"): the title sits on a pack key
            // with a chevron at its end; pressed, every song the bar owns unrolls ABOVE it (there is room above and
            // none below), in the order the moods play them, the one playing lit. Under the key, small, which list
            // the song is on and its place in it.
            float songW = SetW - SetPad * 2f - kx;
            _settingsSongKey = PackWordKey(now, "SONG", "", null, MenuPack.Tone.Grey, new Vector2(0, 0.5f), new Vector2(songW, 32f),
                new Vector2(kx, 0), () => { Sfx.Play("click"); ToggleSongList(!_songListOpen); }, songW, 0f);
            _settingsNowTitle = _settingsSongKey.Find("Face/Label").GetComponent<Text>();
            var drop = NewRect("Drop", _settingsSongKey.Find("Face") as RectTransform);
            Place(drop, new Vector2(1, 0.5f), new Vector2(16, 16), new Vector2(-10f, 1f));
            drop.pivot = new Vector2(1, 0.5f);
            drop.localRotation = Quaternion.Euler(0, 0, 90f);   // the bench's left chevron, turned to point down
            var di = drop.gameObject.AddComponent<Image>();
            di.sprite = ChromeArt.Mark("chevron_left"); di.color = MenuPack.PalettedInk(MenuPack.Tone.Grey, false); di.raycastTarget = false;
            _settingsNowPlace = NewText("Place", now, _body, 8, TextAnchor.MiddleRight, UITheme.Magenta[3]);
            Place(_settingsNowPlace.rectTransform, new Vector2(1, 0.5f), new Vector2(300, 12), new Vector2(-4f, -21f));
            _settingsNowPlace.rectTransform.pivot = new Vector2(1, 0.5f);
            _settingsNowPlace.horizontalOverflow = HorizontalWrapMode.Overflow;
            BuildSongList(now, kx, songW);

            // THE TRACK, SEEKABLE (the author: "şarkıyı ileri saran bir player olmalı"): a rail the pointer presses or
            // drags through the song, the knob on it, and the clock beside it — read off the player every frame the
            // window is up (StepSeek), except while the hand is on it.
            var track = SettingsRow(page, "TRACK", UIText.T("chrome.settings.track"), "note", ref y, 44f);
            var seek = NewRect("Seek", track);
            Place(seek, new Vector2(0, 0.5f), new Vector2(SeekW, 24f), new Vector2(300f, 0));
            seek.pivot = new Vector2(0, 0.5f);
            var seekHit = seek.gameObject.AddComponent<Image>();
            seekHit.color = new Color(0, 0, 0, 0.001f);   // the whole 24 answers the hand, not the 6 of the rail
            var rail = NewRect("Rail", seek);
            Place(rail, new Vector2(0, 0.5f), new Vector2(SeekW, 6f), Vector2.zero);
            rail.pivot = new Vector2(0, 0.5f);
            var railImg = rail.gameObject.AddComponent<Image>(); railImg.color = UITheme.Night[3]; railImg.raycastTarget = false;
            _seekFill = NewRect("Fill", seek);
            Place(_seekFill, new Vector2(0, 0.5f), new Vector2(0f, 6f), Vector2.zero);
            _seekFill.pivot = new Vector2(0, 0.5f);
            var fillImg = _seekFill.gameObject.AddComponent<Image>(); fillImg.color = UITheme.Cyan[3]; fillImg.raycastTarget = false;
            _seekKnob = NewRect("Knob", seek);
            Place(_seekKnob, new Vector2(0, 0.5f), new Vector2(6f, 16f), Vector2.zero);
            _seekKnob.pivot = new Vector2(0.5f, 0.5f);
            var knobImg = _seekKnob.gameObject.AddComponent<Image>(); knobImg.color = UITheme.Cream[4]; knobImg.raycastTarget = false;
            var trig = seek.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(e => { _seekDragging = true; SeekTo(seek, (PointerEventData)e); });
            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(e => SeekTo(seek, (PointerEventData)e));
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(e => { SeekTo(seek, (PointerEventData)e); _seekDragging = false; });
            trig.triggers.Add(down); trig.triggers.Add(drag); trig.triggers.Add(up);
            _seekClock = NewText("Clock", track, _body, 16, TextAnchor.MiddleLeft, UITheme.Cyan[4]);
            Place(_seekClock.rectTransform, new Vector2(0, 0.5f), new Vector2(140, 20), new Vector2(300f + SeekW + 14f, 0));
            _seekClock.rectTransform.pivot = new Vector2(0, 0.5f);
            _seekClock.horizontalOverflow = HorizontalWrapMode.Overflow;
            PaintSeek(0f);
            return page;
        }

        /// <summary>The list of every song, closed until the song key opens it: a well of the beam's make standing
        /// above the key, one row a song — its title, and small at the right which list it is on.</summary>
        private void BuildSongList(RectTransform row, float x, float w)
        {
            var songs = Sfx.AllSongs;
            float h = songs.Count * SongRowH + 8f;
            _songList = NewRect("SongList", row);
            Place(_songList, new Vector2(0, 0.5f), new Vector2(w, h), new Vector2(x + w * 0.5f, 16f + 4f + h * 0.5f));
            _songList.pivot = new Vector2(0.5f, 0.5f);
            var plate = _songList.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.Well(); plate.type = Image.Type.Sliced; plate.color = Color.white; plate.raycastTarget = true;
            _songRows.Clear();
            for (int i = 0; i < songs.Count; i++)
            {
                string song = songs[i];
                var r = NewRect("S_" + song, _songList);
                Place(r, new Vector2(0, 1), new Vector2(w - 8f, SongRowH), new Vector2(4f, -4f - i * SongRowH));
                r.pivot = new Vector2(0, 1);
                var bg = r.gameObject.AddComponent<Image>();
                bg.color = new Color(1f, 1f, 1f, 0f); bg.raycastTarget = true;
                var relay = r.gameObject.AddComponent<HoverRelay>();
                relay.Entered = () => bg.color = UITheme.Night[3];
                relay.Exited = () => bg.color = new Color(1f, 1f, 1f, 0f);
                var btn = r.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    Sfx.PlaySong(song); Sfx.Play("click");
                    ToggleSongList(false);
                    RefreshSettings(); RefreshMusicPlayer(true);
                });
                var title = NewText("Title", r, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
                Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(w - 90f, 20), new Vector2(8f, 0));
                title.rectTransform.pivot = new Vector2(0, 0.5f);
                title.horizontalOverflow = HorizontalWrapMode.Overflow; title.raycastTarget = false;
                title.text = SongTitle(song);
                _songRows[song] = title;
                int cut = song.LastIndexOf('_');
                var mood = NewText("Mood", r, _body, 8, TextAnchor.MiddleRight, UITheme.Magenta[3]);
                Place(mood.rectTransform, new Vector2(1, 0.5f), new Vector2(80, 12), new Vector2(-8f, 0));
                mood.rectTransform.pivot = new Vector2(1, 0.5f);
                mood.horizontalOverflow = HorizontalWrapMode.Overflow; mood.raycastTarget = false;
                mood.text = MoodWord(cut < 0 ? song : song.Substring(0, cut));
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
            float p = Mathf.Clamp01(local.x / SeekW);
            Sfx.MusicProgress = p;
            PaintSeek(p);
        }

        private void PaintSeek(float p)
        {
            if (_seekFill == null) return;
            _seekFill.sizeDelta = new Vector2(p * SeekW, 6f);
            _seekKnob.anchoredPosition = new Vector2(p * SeekW, 0f);
            var (at, length) = Sfx.MusicClock;
            _seekClock.text = length <= 0f ? "" : UIText.T("chrome.settings.clock", ("at", Clock(at)), ("of", Clock(length)));
        }

        private static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>The seek bar follows the song while the window is up and the hand is off it.</summary>
        private void StepSeek()
        {
            if (_seekFill == null || _settingsPage != "AUDIO" || _seekDragging) return;
            PaintSeek(Sfx.MusicProgress);
        }

        /// <summary>A row: a mark, the name beside it, a hairline under; the control is placed by the caller from
        /// x 300 on. The name's box takes its measured width, so a long word never runs into the control.</summary>
        private RectTransform SettingsRow(RectTransform page, string id, string name, string mark, ref float y, float rowH = SetRow)
        {
            var row = NewRect("R_" + id, page);
            Place(row, new Vector2(0, 1), new Vector2(SetW - SetPad * 2f, rowH), new Vector2(0, y));
            row.pivot = new Vector2(0, 1);
            row.anchorMax = new Vector2(1, 1);
            row.sizeDelta = new Vector2(0, rowH);
            Hairline(row, new Vector2(0, 0), new Vector2(1, 0), new Color(1f, 1f, 1f, 0.07f));
            var mk = NewRect("Mark", row);
            Place(mk, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(0, 0));
            mk.pivot = new Vector2(0, 0.5f);
            var mi = mk.gameObject.AddComponent<Image>();
            mi.sprite = NightArt.Mark(mark) ?? ChromeArt.Mark(mark); mi.color = UITheme.Amber[4]; mi.raycastTarget = false;
            var t = NewText("N", row, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
            Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(260, 20), new Vector2(28f, 0));
            t.rectTransform.pivot = new Vector2(0, 0.5f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = name;
            y -= rowH;
            return row;
        }

        /// <summary>A level on a ten-cell meter between the pack's - and + keys, its percentage after it. The cells
        /// are returned for the refresh, the percentage text through <paramref name="pct"/>.</summary>
        private Image[] MeterRow(RectTransform page, string id, string name, string mark, ref float y, Func<float> get, Action<float> set, out Text pct, float rowH = SetRow)
        {
            var row = SettingsRow(page, id, name, mark, ref y, rowH);
            const float KeyW = 32f, Cell = 26f;
            float x = 300f;
            PackIconKey(row, "MINUS", "minus", MenuPack.Tone.Grey, new Vector2(0, 0.5f), new Vector2(x, 0), () =>
            {
                set(Mathf.Clamp01(Mathf.Round((get() - 0.1f) * 10f) / 10f)); Sfx.Play("click"); RefreshSettings();
            });
            x += KeyW + 12f;
            var cells = new Image[10];
            for (int i = 0; i < 10; i++)
            {
                var cell = NewRect("M" + i, row);
                Place(cell, new Vector2(0, 0.5f), new Vector2(22f, 22f), new Vector2(x + i * Cell, 0));
                cell.pivot = new Vector2(0, 0.5f);
                cells[i] = cell.gameObject.AddComponent<Image>();
                cells[i].raycastTarget = false;
                Scanlines(cell, 0.35f);
            }
            x += 10 * Cell + 4f;
            PackIconKey(row, "PLUS", "plus", MenuPack.Tone.Grey, new Vector2(0, 0.5f), new Vector2(x, 0), () =>
            {
                set(Mathf.Clamp01(Mathf.Round((get() + 0.1f) * 10f) / 10f)); Sfx.Play("click"); RefreshSettings();
            });
            x += KeyW + 14f;
            pct = NewText("Pct", row, _body, 16, TextAnchor.MiddleLeft, UITheme.Cyan[4]);
            Place(pct.rectTransform, new Vector2(0, 0.5f), new Vector2(80, 20), new Vector2(x, 0));
            pct.rectTransform.pivot = new Vector2(0, 0.5f);
            pct.horizontalOverflow = HorizontalWrapMode.Overflow;
            return cells;
        }

        // ── CONTROLS ─────────────────────────────────────────────────────────────────────────────────────────────

        private RectTransform BuildControlsPage(RectTransform plate)
        {
            var page = NewRect("Page_CONTROLS", plate);
            Stretch(page, Vector2.zero, Vector2.one, new Vector2(SetPad, 90f), new Vector2(-SetPad, -140f));
            float y = 0f;
            foreach (var (action, key, mark) in new[] {
                (KeyAction.Pause, "chrome.bind.pause", "cog"), (KeyAction.Book, "chrome.bind.book", "book"),
                (KeyAction.Cellar, "chrome.bind.cellar", "bottle"), (KeyAction.PageBack, "chrome.bind.pages", "book"),
                (KeyAction.PageForward, "chrome.bind.pages", "book"), (KeyAction.NextTrack, "chrome.bind.next_track", "note"),
                (KeyAction.MusicToggle, "chrome.bind.music_toggle", "speaker") })
            {
                string name = UIText.T(key) + (action == KeyAction.PageBack ? " ←" : action == KeyAction.PageForward ? " →" : "");
                // 40 a row, not 56: seven actions and the hint under them have to stand between the tabs and the foot.
                var row = SettingsRow(page, action.ToString(), name, mark, ref y, 40f);
                var a = action;
                // THE CAP (2026-09-15, KeyCaps): the author's drawing of the key the action is on, at 2x, against the
                // row's right; a key the pack has no cap for gets its word printed on the blank cap.
                var cap = NewRect("Cap", row);
                cap.anchorMin = cap.anchorMax = cap.pivot = new Vector2(1, 0.5f);
                cap.sizeDelta = new Vector2(34f, CapH);
                cap.anchoredPosition = Vector2.zero;
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
                var hint = NewText("Hint", row, _body, 8, TextAnchor.MiddleRight, UITheme.Cyan[4]);
                Place(hint.rectTransform, new Vector2(1, 0.5f), new Vector2(300, 12), new Vector2(-46f, 0));
                hint.rectTransform.pivot = new Vector2(1, 0.5f);
                hint.horizontalOverflow = HorizontalWrapMode.Overflow;
                hint.text = "";
                _bindCaps[action] = ci;
                _bindWords[action] = word;
                _bindHints[action] = hint;
            }
            var foot = NewText("Hint", page, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(foot.rectTransform, new Vector2(0, 0), new Vector2(640, 12), new Vector2(0, 8f));
            foot.rectTransform.pivot = new Vector2(0, 0);
            foot.horizontalOverflow = HorizontalWrapMode.Overflow;
            foot.text = UIText.T("chrome.settings.controls_hint");
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
                hint.rectTransform.anchoredPosition = new Vector2(-(w + 12f), 0);
        }

        /// <summary>Every frame the window is up: the caps follow the keyboard (the real key held → the cap pressed;
        /// a listening row blinks), and a row that listens takes the next key (Escape cancels).</summary>
        private void StepSettings()
        {
            if (_settingsPanel == null || !_settingsPanel.gameObject.activeSelf) return;
            StepSeek();
            var kb = Keyboard.current;
            bool blink = ((int)(Time.unscaledTime * 4f) & 1) == 0;
            foreach (var pair in _bindCaps)
            {
                bool listening = _bindListening == pair.Key;
                bool down = listening ? blink : kb != null && KeyHeld(kb, Keys.Get(pair.Key));
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

        private RectTransform BuildDisplayPage(RectTransform plate)
        {
            var page = NewRect("Page_DISPLAY", plate);
            Stretch(page, Vector2.zero, Vector2.one, new Vector2(SetPad, 90f), new Vector2(-SetPad, -140f));
            float y = 0f;
            var mot = SettingsRow(page, "MOTION", UIText.T("chrome.settings.motion"), "redo", ref y);
            _settingsMotionKey = PackWordKey(mot, "MOTION", UIText.T("chrome.settings.full"), null, MenuPack.Tone.Green, new Vector2(0, 0.5f), new Vector2(140, 40), new Vector2(300f, 0), () =>
            {
                Motion.Reduced = !Motion.Reduced; Sfx.Play("click"); RefreshSettings();
            }, 100f, 32f);
            _settingsMotion = _settingsMotionKey.Find("Face/Label").GetComponent<Text>();
            var motNote = NewText("Note", mot, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(motNote.rectTransform, new Vector2(0, 0.5f), new Vector2(360, 12), new Vector2(300f + _settingsMotionKey.sizeDelta.x + 12f, 0));
            motNote.rectTransform.pivot = new Vector2(0, 0.5f);
            motNote.horizontalOverflow = HorizontalWrapMode.Overflow;
            motNote.text = UIText.T("chrome.settings.motion_note");

            var book = SettingsRow(page, "BOOK", UIText.T("chrome.settings.book"), "cash", ref y);
            PackWordKey(book, "OPEN", UIText.T("chrome.settings.open"), "menu", MenuPack.Tone.Grey, new Vector2(0, 0.5f), new Vector2(140, 40), new Vector2(300f, 0),
                () => { ToggleSettings(); if (Showing(_pausePanel)) TogglePause(); ToggleLedger(); }, 100f, 48f + 16f);
            var fresh = SettingsRow(page, "START OVER", UIText.T("chrome.settings.start_over"), "moon", ref y);
            var newRun = PackWordKey(fresh, "NEW RUN", UIText.T("chrome.settings.new_run"), "restart", MenuPack.Tone.Grey, new Vector2(0, 0.5f), new Vector2(140, 40), new Vector2(300f, 0), () =>
            {
                ToggleSettings(); if (Showing(_pausePanel)) TogglePause(); _bootstrap.StartNewRun(null);
            }, 100f, 48f + 16f);
            var freshNote = NewText("Note", fresh, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(freshNote.rectTransform, new Vector2(0, 0.5f), new Vector2(360, 12), new Vector2(300f + newRun.sizeDelta.x + 12f, 0));
            freshNote.rectTransform.pivot = new Vector2(0, 0.5f);
            freshNote.horizontalOverflow = HorizontalWrapMode.Overflow;
            freshNote.text = UIText.T("chrome.settings.start_over_note");
            return page;
        }

        // ── LANGUAGE ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>THE FLAGS (2026-09-15): every language this build has a table for, as its flag on one of the pack's
        /// plates, eight to a row; the chosen one on the green plate. Under the pointer a flag says its language's
        /// name; under the grid the chosen language's name and, in that language, that it speaks at the next start.</summary>
        private RectTransform BuildLanguagePage(RectTransform plate)
        {
            var page = NewRect("Page_LANGUAGE", plate);
            Stretch(page, Vector2.zero, Vector2.one, new Vector2(SetPad, 90f), new Vector2(-SetPad, -140f));
            var hint = NewText("Hint", page, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            Place(hint.rectTransform, new Vector2(0, 1), new Vector2(640, 12), new Vector2(0, -2f));
            hint.rectTransform.pivot = new Vector2(0, 1);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.text = UIText.T("chrome.settings.language_hint");

            const int Cols = 8;
            const float CellW = 64f, CellH = 49f, GapX = 24f, GapY = 8f;
            float left = (SetW - SetPad * 2f - (Cols * CellW + (Cols - 1) * GapX)) * 0.5f;
            var all = Localization.Available;
            int rows = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var info = all[i];
                string code = info.Code;
                int r = i / Cols, c = i % Cols;
                rows = r + 1;
                var key = NewRect("Flag_" + code, page);
                key.anchorMin = key.anchorMax = key.pivot = new Vector2(0, 1);
                key.sizeDelta = new Vector2(CellW, CellH);
                key.anchoredPosition = new Vector2(left + c * (CellW + GapX), -24f - r * (CellH + GapY));
                var plateImg = key.gameObject.AddComponent<Image>();
                plateImg.sprite = MenuPack.Paletted(MenuPack.Tone.Grey, false);
                plateImg.type = Image.Type.Sliced;
                plateImg.pixelsPerUnitMultiplier = 0.5f;
                plateImg.color = Color.white;
                plateImg.raycastTarget = true;
                var btn = key.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = plateImg;
                btn.onClick.AddListener(() => { Localization.Choose(code); Sfx.Play("click"); RefreshSettings(); });
                var face = NewRect("Face", key);
                Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var sink = key.gameObject.AddComponent<PressSink>();
                sink.Face = face; sink.Depth = 2f; sink.Lift = 2f; sink.Squash = 0f; sink.Bloom = 0.03f;
                var flag = NewRect("Flag", face);
                Place(flag, new Vector2(0.5f, 0.5f), new Vector2(48, 33), new Vector2(0, 2f));   // on the face's middle, two up (PackWordKey)
                var fi = flag.gameObject.AddComponent<Image>();
                fi.sprite = ItemArt.Load("fl_" + LanguageFlag(code));
                fi.color = Color.white; fi.raycastTarget = false;
                var pk = key.gameObject.AddComponent<PackKey>();
                pk.Plate = plateImg;
                pk.Rest = plateImg.sprite; pk.Lit = MenuPack.Hovered(); pk.Pressed = MenuPack.Paletted(MenuPack.Tone.Grey, true);
                HoverTip(key, fi.sprite, info.Name, code.ToUpperInvariant());
                _flagKeys[code] = key;
            }
            float captionY = -24f - rows * (CellH + GapY) - 6f;
            _settingsLanguage = NewText("Name", page, _body, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Place(_settingsLanguage.rectTransform, new Vector2(0.5f, 1), new Vector2(640, 20), new Vector2(0, captionY));
            _settingsLanguage.rectTransform.pivot = new Vector2(0.5f, 1);
            _settingsLanguage.horizontalOverflow = HorizontalWrapMode.Overflow;
            _settingsLanguageNote = NewText("LangNote", page, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[2]);
            Place(_settingsLanguageNote.rectTransform, new Vector2(0.5f, 1), new Vector2(640, 12), new Vector2(0, captionY - 24f));
            _settingsLanguageNote.rectTransform.pivot = new Vector2(0.5f, 1);
            _settingsLanguageNote.horizontalOverflow = HorizontalWrapMode.Overflow;
            // APPLY (2026-09-16, the author: "dil seçildikten sonra uygula dendiğinde oyunun dili direkt değişmeli"):
            // shown once a different language is picked; the scene rebuilds around the same run in the new words.
            _settingsApplyLanguage = PackWordKey(page, "APPLY", UIText.T("chrome.settings.apply_language"), "restart", MenuPack.Tone.Green,
                new Vector2(0.5f, 1), new Vector2(180, 40), new Vector2(0, captionY - 44f), () =>
                {
                    string pick = Localization.PreferredCode();
                    if (pick == Localization.Current.Code || _bootstrap == null) return;
                    Sfx.Play("click");
                    Localization.UseForSession(pick);
                    _bootstrap.ReloadKeepingRun();
                }, 140f, 48f + 24f);
            _settingsApplyLanguage.gameObject.SetActive(false);
            return page;
        }

        // ── refresh ──────────────────────────────────────────────────────────────────────────────────────────────

        private void RefreshSettings()
        {
            if (_settingsVolume == null) return;
            PaintMeter(_settingsMeter, Sound.Volume, _settingsVolume);
            PaintMeter(_settingsMusicMeter, Sound.MusicVolume, _settingsMusicPct);
            PaintMeter(_settingsEffectsMeter, Sound.EffectsVolume, _settingsEffectsPct);
            _settingsMute.text = Sound.Muted ? UIText.T("chrome.settings.off") : UIText.T("chrome.settings.on");
            if (_settingsMuteKey != null)
                ReiconKey(_settingsMuteKey, Sound.Muted ? MenuPack.Tone.Grey : MenuPack.Tone.Green, Sound.Muted ? "sound_off" : "sound_on");
            _settingsMotion.text = Motion.Reduced ? UIText.T("chrome.settings.reduced") : UIText.T("chrome.settings.full");
            if (_settingsMotionKey != null)
            {
                RetoneWordKey(_settingsMotionKey, Motion.Reduced ? MenuPack.Tone.Grey : MenuPack.Tone.Green);
                FitKey(_settingsMotionKey, 100f, 32f);
            }
            if (_settingsNowTitle != null)
            {
                string now = Sfx.NowPlaying;
                _settingsNowTitle.text = SongTitle(now);
                var (at, of) = Sfx.NowPlayingPlace;
                string mood = now == null ? "" : now.Substring(0, now.LastIndexOf('_') < 0 ? now.Length : now.LastIndexOf('_'));
                _settingsNowPlace.text = of == 0 ? "" : UIText.T("build.player.place", ("mood", MoodWord(mood)), ("at", at), ("of", of));
                if (_settingsHoldKey != null) ReiconKey(_settingsHoldKey, MenuPack.Tone.Grey, Sfx.MusicPaused ? "play" : "pause");
                foreach (var pair in _songRows) pair.Value.color = pair.Key == now ? UITheme.Cyan[4] : UITheme.Cream[4];
                PaintSeek(Sfx.MusicProgress);
            }
            _capDown.Clear();
            foreach (var pair in _bindHints)
            {
                PaintCap(pair.Key, false);
                pair.Value.text = _bindListening == pair.Key ? UIText.T("chrome.settings.press_key") : "";
            }
            if (_settingsLanguage != null)
            {
                string pick = Localization.PreferredCode();
                var info = Languages.Find(pick);
                if (_settingsApplyLanguage != null) _settingsApplyLanguage.gameObject.SetActive(pick != Localization.Current.Code);
                _settingsLanguage.text = info != null ? info.Name : pick;
                foreach (var pair in _flagKeys)
                    RetoneWordKey(pair.Value, pair.Key == pick ? MenuPack.Tone.Green : MenuPack.Tone.Grey);
                // The note is said in the language just PICKED, not the one on screen: a player who
                // chose Deutsch reads, in German, that it comes at the next start — the proof it took.
                if (_settingsLanguageNote != null)
                {
                    if (pick == Localization.Current.Code) _settingsLanguageNote.text = "";
                    else
                    {
                        if (_languageNoteCode != pick)
                        {
                            _languageNoteCode = pick;
                            _languageNoteText = Localization.Load(pick).Get("chrome.settings.language_note_now");
                        }
                        _settingsLanguageNote.text = _languageNoteText;
                    }
                }
            }
        }

        private static void PaintMeter(Image[] cells, float level, Text pct)
        {
            if (cells != null)
                for (int i = 0; i < cells.Length; i++)
                    if (cells[i] != null)
                        cells[i].color = !Sound.Muted && level + 1e-3f >= (i + 1) / 10f ? UITheme.Cyan[3] : UITheme.Night[3];
            if (pct != null) pct.text = UIText.T("chrome.settings.volume_value", ("pct", Mathf.RoundToInt(level * 100)));
        }
    }
}
