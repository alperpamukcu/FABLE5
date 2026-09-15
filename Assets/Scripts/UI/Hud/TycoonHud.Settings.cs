using System;
using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE SETTINGS WINDOW, in the palm wall direction (2026-09-15, the author: "ayarlar key bind ses ... Menü ayarlar
    /// panellerinde daha çok görsellerden yararlanabilir daha yaratıcı olabilir"). Three pages under tabs that carry
    /// marks — AUDIO, CONTROLS, DISPLAY — on one glass plate:
    ///   AUDIO     the master, the music and the effects each on a ten-cell meter between - and +, the sound switch,
    ///             and the player itself on a NOW PLAYING row
    ///   CONTROLS  every bound action with the key it is on; press the cap and the row listens for the next key
    ///   DISPLAY   motion, the language, and the run's own verbs (tonight's book, a new run)
    /// The foot carries RESET DEFAULTS and the one amber key, BACK — to the pause menu when the window came from it.
    /// Every key and tab is fitted to its word (FitKey), so a long translation gets a long key.
    ///
    /// This replaced the one-page window of 2026-09-06 (BuildSettings in TycoonHud.Chrome.cs); its fields
    /// (_settingsPanel, _settingsMeter, _settingsVolume, _settingsMute, _settingsMotion, _settingsLanguage...) are the
    /// same ones, declared in TycoonHud.cs.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private const float SetW = 800f, SetH = 540f, SetPad = 44f, SetRow = 56f;
        private readonly Dictionary<string, RectTransform> _settingsPages = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, RectTransform> _settingsTabs = new Dictionary<string, RectTransform>();
        private string _settingsPage = "AUDIO";
        private Image[] _settingsMusicMeter, _settingsEffectsMeter;
        private Text _settingsMusicPct, _settingsEffectsPct, _settingsNowTitle, _settingsNowPlace;
        private Image _settingsHoldMark;
        private readonly Dictionary<KeyAction, Text> _bindCaps = new Dictionary<KeyAction, Text>();
        private readonly Dictionary<KeyAction, RectTransform> _bindRows = new Dictionary<KeyAction, RectTransform>();
        private KeyAction? _bindListening;
        private Text _bindListenText;

        private void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            bool show = !_settingsPanel.gameObject.activeSelf;
            if (show) { CloseId(); RefreshSettings(); }
            else
            {
                _bindListening = null;
                if (_settingsFromPause && _pausePanel != null)
                {
                    // BACK goes back to where the window came from; the night stays held meanwhile.
                    _pausePanel.gameObject.SetActive(true);
                    RefreshPauseFoot();
                }
                _settingsFromPause = false;
            }
            _settingsPanel.gameObject.SetActive(show);
        }

        private void BuildSettings(RectTransform root)
        {
            _settingsPanel = NewRect("Settings", root);
            var canvas = _settingsPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 29;                 // with the pause menu, over the book; the market (22) never shows it
            _settingsPanel.gameObject.AddComponent<ForgivingRaycaster>();
            Stretch(_settingsPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // The wall on a child of its own, so the audit's exemption covers the drawing and not the window over it.
            var wallRt = NewRect("Wall", _settingsPanel);
            Stretch(wallRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var wall = wallRt.gameObject.AddComponent<Image>();
            wall.sprite = NightArt.Backdrop();
            wall.type = NightArt.UseFlatBackdrop ? Image.Type.Tiled : Image.Type.Simple;
            wall.pixelsPerUnitMultiplier = 0.5f;
            wall.color = Color.white;
            wall.raycastTarget = true;
            UiAuditExempt.Mark(wallRt, "the settings' drawn night is 640x360 shown at exactly 2x");
            var dim = NewRect("Dim", _settingsPanel);
            Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.42f);   // Night[0] at 42%
            dimImg.raycastTarget = false;
            Scanlines(_settingsPanel, 0.16f);
            var scrimBtn = wallRt.gameObject.AddComponent<Button>();    // a click on the night closes the window
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(ToggleSettings);

            var plate = NewRect("Plate", _settingsPanel);
            Place(plate, new Vector2(0.5f, 0.5f), new Vector2(SetW, SetH), new Vector2(0, 0));
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.sprite = NightArt.MenuPlate();
            plateImg.type = Image.Type.Sliced;
            plateImg.raycastTarget = true;
            plate.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // swallow clicks
            Scanlines(plate, 0.10f);
            NightTitle(plate, UIText.T("chrome.settings.title"), -36f);
            SunsetRules(plate, -66f);

            // the tabs, each with its mark, fitted to its word, laid left to right
            float tx = SetPad;
            foreach (var (id, key, mark) in new[] { ("AUDIO", "chrome.settings.audio", "speaker"), ("CONTROLS", "chrome.settings.controls", "key"), ("DISPLAY", "chrome.settings.display", "moon") })
            {
                string page = id;
                var tab = NewButton(plate, UIText.T(key), new Vector2(0, 1), new Vector2(160, 38), new Vector2(tx, -90f), UITheme.ClubBlue[1], () => ShowSettingsPage(page));
                tab.name = "Tab_" + id;
                var face = (RectTransform)tab.Find("Face");
                KeyMark(face, mark, UITheme.Cream[4]);
                face.Find("Label").GetComponent<Text>().rectTransform.offsetMin = new Vector2(40f, KeyPlate.Throw);
                FitKey(tab, 160f, 40f + 24f);
                _settingsTabs[id] = tab;
                tx += tab.sizeDelta.x + 10f;
            }

            _settingsPages["AUDIO"] = BuildAudioPage(plate);
            _settingsPages["CONTROLS"] = BuildControlsPage(plate);
            _settingsPages["DISPLAY"] = BuildDisplayPage(plate);

            // the foot: reset at one corner, the way back at the other; the dev bench beside reset in the editor
            var reset = NewButton(plate, UIText.T("chrome.settings.reset"), new Vector2(0, 0), new Vector2(250, 46), new Vector2(SetPad, 26), UITheme.ClubBlue[2], () =>
            {
                Sound.Volume = 0.8f; Sound.MusicVolume = 1f; Sound.EffectsVolume = 1f; Sound.Muted = false;
                Motion.Reduced = false;
                Keys.ResetAll();
                Sfx.Play("click");
                RefreshSettings();
            });
            reset.name = "RESET";
            KeyMark((RectTransform)reset.Find("Face"), "redo", UITheme.Cream[4]);
            reset.Find("Face/Label").GetComponent<Text>().rectTransform.offsetMin = new Vector2(44f, KeyPlate.Throw);
            FitKey(reset, 200f, 44f + 24f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var dev = NewButton(plate, "DEV TOOLS", new Vector2(0, 0), new Vector2(110, 26), new Vector2(SetPad + reset.sizeDelta.x + 12f, 36), UITheme.Night[2], () => { ToggleSettings(); ToggleDevBench(); });
            dev.name = "DEV";
#endif
            var back = NewButton(plate, UIText.T("chrome.settings.back"), new Vector2(1, 0), new Vector2(180, 46), new Vector2(-SetPad, 26), UITheme.PrimaryAction, ToggleSettings);
            back.name = "BACK";
            KeyMark((RectTransform)back.Find("Face"), "back", UITheme.TextOnAmber);
            back.Find("Face/Label").GetComponent<Text>().rectTransform.offsetMin = new Vector2(44f, KeyPlate.Throw);
            FitKey(back, 140f, 44f + 24f);

            ShowSettingsPage("AUDIO");
            _settingsPanel.gameObject.SetActive(false);
        }

        private void ShowSettingsPage(string id)
        {
            _settingsPage = id;
            _bindListening = null;
            foreach (var pair in _settingsPages) pair.Value.gameObject.SetActive(pair.Key == id);
            foreach (var pair in _settingsTabs)
            {
                bool on = pair.Key == id;
                var img = pair.Value.GetComponent<Image>();
                img.color = on ? UITheme.Cyan[2] : UITheme.ClubBlue[1];
                var sink = pair.Value.GetComponent<PressSink>();
                if (sink != null) sink.Repaint(img.color);
            }
            RefreshSettings();
        }

        // ── AUDIO ────────────────────────────────────────────────────────────────────────────────────────────────

        private RectTransform BuildAudioPage(RectTransform plate)
        {
            var page = NewRect("Page_AUDIO", plate);
            Stretch(page, Vector2.zero, Vector2.one, new Vector2(SetPad, 90f), new Vector2(-SetPad, -140f));
            float y = 0f;
            _settingsMeter = MeterRow(page, "MASTER", UIText.T("chrome.settings.master"), "speaker", ref y, () => Sound.Volume, v => Sound.Volume = v, out _settingsVolume);
            _settingsMusicMeter = MeterRow(page, "MUSIC", UIText.T("chrome.settings.music"), "note", ref y, () => Sound.MusicVolume, v => Sound.MusicVolume = v, out _settingsMusicPct);
            _settingsEffectsMeter = MeterRow(page, "EFFECTS", UIText.T("chrome.settings.effects"), "glass", ref y, () => Sound.EffectsVolume, v => Sound.EffectsVolume = v, out _settingsEffectsPct);

            var snd = SettingsRow(page, "SOUND", UIText.T("chrome.settings.sound"), "speaker", ref y);
            var mute = NewButton(snd, UIText.T("chrome.settings.on"), new Vector2(0, 0.5f), new Vector2(140, 40), new Vector2(300f, 0), UITheme.ClubBlue[2], () =>
            {
                Sound.Muted = !Sound.Muted;
                Sfx.Play("click");              // audible iff it just came back on — itself the test
                RefreshSettings();
            });
            mute.name = "MUTE";
            _settingsMute = mute.Find("Face/Label").GetComponent<Text>();
            FitKey(mute, 100f, 32f);

            var now = SettingsRow(page, "NOW PLAYING", UIText.T("chrome.settings.now_playing"), "note", ref y);
            float kx = 300f;
            foreach (var (id, mark, act) in new (string, string, Action)[] {
                ("PREV", "prev", () => { Sfx.SkipTrack(-1); Sfx.Play("click"); }),
                ("HOLD", "pause", () => { Sfx.MusicPaused = !Sfx.MusicPaused; Sfx.Play("click"); RefreshSettings(); }),
                ("NEXT", "next", () => { Sfx.SkipTrack(+1); Sfx.Play("click"); }) })
            {
                var k = NewButton(now, id, new Vector2(0, 0.5f), new Vector2(26, 26), new Vector2(kx, 0), UITheme.Night[2], act, NightArt.Mark(mark));
                k.name = id;
                var mi = k.Find("Face/Mark").GetComponent<Image>();
                mi.color = UITheme.Cyan[4];
                if (id == "HOLD") _settingsHoldMark = mi;
                kx += 34f;
            }
            _settingsNowTitle = NewText("Title", now, _body, 8, TextAnchor.MiddleLeft, UITheme.Cyan[4]);
            Place(_settingsNowTitle.rectTransform, new Vector2(0, 0.5f), new Vector2(300, 12), new Vector2(kx + 6f, 8f));
            _settingsNowTitle.rectTransform.pivot = new Vector2(0, 0.5f);
            _settingsNowTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            _settingsNowPlace = NewText("Place", now, _body, 8, TextAnchor.MiddleLeft, UITheme.Magenta[3]);
            Place(_settingsNowPlace.rectTransform, new Vector2(0, 0.5f), new Vector2(300, 12), new Vector2(kx + 6f, -8f));
            _settingsNowPlace.rectTransform.pivot = new Vector2(0, 0.5f);
            _settingsNowPlace.horizontalOverflow = HorizontalWrapMode.Overflow;
            return page;
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

        /// <summary>A level on a ten-cell meter between a - and a + key, its percentage after it. The cells are
        /// returned for the refresh, the percentage text through <paramref name="pct"/>.</summary>
        private Image[] MeterRow(RectTransform page, string id, string name, string mark, ref float y, Func<float> get, Action<float> set, out Text pct)
        {
            var row = SettingsRow(page, id, name, mark, ref y);
            const float KeyW = 40f, Cell = 26f;
            float x = 300f;
            var minus = NewButton(row, "-", new Vector2(0, 0.5f), new Vector2(KeyW, 40f), new Vector2(x, 0), UITheme.ClubBlue[2], () =>
            {
                set(Mathf.Clamp01(Mathf.Round((get() - 0.1f) * 10f) / 10f)); Sfx.Play("click"); RefreshSettings();
            });
            minus.name = "MINUS";
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
            var plus = NewButton(row, "+", new Vector2(0, 0.5f), new Vector2(KeyW, 40f), new Vector2(x, 0), UITheme.ClubBlue[2], () =>
            {
                set(Mathf.Clamp01(Mathf.Round((get() + 0.1f) * 10f) / 10f)); Sfx.Play("click"); RefreshSettings();
            });
            plus.name = "PLUS";
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
                // 44 a row, not 56: seven actions have to stand between the tabs and the foot.
                var row = SettingsRow(page, action.ToString(), name, mark, ref y, 44f);
                var a = action;
                var cap = NewButton(row, Keys.Label(Keys.Get(action)), new Vector2(1, 0.5f), new Vector2(72, 36), new Vector2(0, 0), UITheme.ClubBlue[2], () =>
                {
                    _bindListening = _bindListening == a ? null : a;
                    Sfx.Play("click");
                    RefreshSettings();
                });
                cap.name = "Cap";
                _bindCaps[action] = cap.Find("Face/Label").GetComponent<Text>();
                _bindRows[action] = row;
            }
            _bindListenText = NewText("Listen", page, _body, 8, TextAnchor.MiddleRight, UITheme.Cyan[4]);
            Place(_bindListenText.rectTransform, new Vector2(1, 1), new Vector2(300, 16), new Vector2(0, 0));
            _bindListenText.rectTransform.pivot = new Vector2(1, 1);
            _bindListenText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _bindListenText.text = "";
            return page;
        }

        /// <summary>Every frame the window is up: a row that listens takes the next key (Escape cancels).</summary>
        private void StepSettings()
        {
            if (_settingsPanel == null || !_settingsPanel.gameObject.activeSelf || _bindListening == null) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { _bindListening = null; RefreshSettings(); return; }
            var key = Keys.AnyPressed();
            if (key == Key.None) return;
            Keys.Set(_bindListening.Value, key);
            _bindListening = null;
            Sfx.Play("key_press", 0.6f);
            RefreshSettings();
        }

        // ── DISPLAY ──────────────────────────────────────────────────────────────────────────────────────────────

        private RectTransform BuildDisplayPage(RectTransform plate)
        {
            var page = NewRect("Page_DISPLAY", plate);
            Stretch(page, Vector2.zero, Vector2.one, new Vector2(SetPad, 90f), new Vector2(-SetPad, -140f));
            float y = 0f;
            var mot = SettingsRow(page, "MOTION", UIText.T("chrome.settings.motion"), "redo", ref y);
            var motion = NewButton(mot, UIText.T("chrome.settings.full"), new Vector2(0, 0.5f), new Vector2(140, 40), new Vector2(300f, 0), UITheme.ClubBlue[2], () =>
            {
                Motion.Reduced = !Motion.Reduced; Sfx.Play("click"); RefreshSettings();
            });
            motion.name = "MOTION";
            _settingsMotion = motion.Find("Face/Label").GetComponent<Text>();
            FitKey(motion, 100f, 32f);
            var motNote = NewText("Note", mot, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(motNote.rectTransform, new Vector2(0, 0.5f), new Vector2(360, 12), new Vector2(300f + motion.sizeDelta.x + 12f, 0));
            motNote.rectTransform.pivot = new Vector2(0, 0.5f);
            motNote.horizontalOverflow = HorizontalWrapMode.Overflow;
            motNote.text = UIText.T("chrome.settings.motion_note");

            var lang = SettingsRow(page, "LANGUAGE", UIText.T("chrome.settings.language"), "book", ref y);
            var prev = NewButton(lang, "PREV", new Vector2(0, 0.5f), new Vector2(40, 40), new Vector2(300f, 0), UITheme.ClubBlue[2], () => StepLanguage(-1), NightArt.Mark("back"));
            prev.name = "PREV";
            _settingsLanguage = NewText("Name", lang, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Place(_settingsLanguage.rectTransform, new Vector2(0, 0.5f), new Vector2(160, 20), new Vector2(348f, 0));
            _settingsLanguage.rectTransform.pivot = new Vector2(0, 0.5f);
            _settingsLanguage.horizontalOverflow = HorizontalWrapMode.Overflow;
            var next = NewButton(lang, "NEXT", new Vector2(0, 0.5f), new Vector2(40, 40), new Vector2(516f, 0), UITheme.ClubBlue[2], () => StepLanguage(+1), NightArt.Mark("next"));
            next.name = "NEXT";
            _settingsLanguageNote = NewText("LangNote", lang, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(_settingsLanguageNote.rectTransform, new Vector2(0, 0.5f), new Vector2(240, 12), new Vector2(568f, 0));
            _settingsLanguageNote.rectTransform.pivot = new Vector2(0, 0.5f);
            _settingsLanguageNote.horizontalOverflow = HorizontalWrapMode.Overflow;

            var book = SettingsRow(page, "BOOK", UIText.T("chrome.settings.book"), "cash", ref y);
            var open = NewButton(book, UIText.T("chrome.settings.open"), new Vector2(0, 0.5f), new Vector2(140, 40), new Vector2(300f, 0), UITheme.ClubBlue[2], () => { ToggleSettings(); if (Showing(_pausePanel)) TogglePause(); ToggleLedger(); });
            open.name = "OPEN";
            FitKey(open, 100f, 32f);
            var fresh = SettingsRow(page, "START OVER", UIText.T("chrome.settings.start_over"), "moon", ref y);
            var newRun = NewButton(fresh, UIText.T("chrome.settings.new_run"), new Vector2(0, 0.5f), new Vector2(140, 40), new Vector2(300f, 0), UITheme.Brick[2], () =>
            {
                ToggleSettings(); if (Showing(_pausePanel)) TogglePause(); _bootstrap.StartNewRun(null);
            });
            newRun.name = "NEW RUN";
            FitKey(newRun, 100f, 32f);
            var freshNote = NewText("Note", fresh, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(freshNote.rectTransform, new Vector2(0, 0.5f), new Vector2(360, 12), new Vector2(300f + newRun.sizeDelta.x + 12f, 0));
            freshNote.rectTransform.pivot = new Vector2(0, 0.5f);
            freshNote.horizontalOverflow = HorizontalWrapMode.Overflow;
            freshNote.text = UIText.T("chrome.settings.start_over_note");
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
            FitKey((RectTransform)_settingsMute.transform.parent.parent, 100f, 32f);
            _settingsMotion.text = Motion.Reduced ? UIText.T("chrome.settings.reduced") : UIText.T("chrome.settings.full");
            FitKey((RectTransform)_settingsMotion.transform.parent.parent, 100f, 32f);
            if (_settingsNowTitle != null)
            {
                string now = Sfx.NowPlaying;
                _settingsNowTitle.text = SongTitle(now);
                var (at, of) = Sfx.NowPlayingPlace;
                string mood = now == null ? "" : now.Substring(0, now.LastIndexOf('_') < 0 ? now.Length : now.LastIndexOf('_'));
                _settingsNowPlace.text = of == 0 ? "" : UIText.T("build.player.place", ("mood", MoodWord(mood)), ("at", at), ("of", of));
                if (_settingsHoldMark != null) _settingsHoldMark.sprite = NightArt.Mark(Sfx.MusicPaused ? "play" : "pause");
            }
            foreach (var pair in _bindCaps)
            {
                bool listening = _bindListening == pair.Key;
                pair.Value.text = listening ? UIText.T("chrome.settings.press_key") : Keys.Label(Keys.Get(pair.Key));
                var cap = (RectTransform)pair.Value.transform.parent.parent;
                cap.sizeDelta = new Vector2(72f, 36f);
                FitKey(cap, 72f, 32f);
                var img = cap.GetComponent<Image>();
                img.color = listening ? UITheme.Cyan[2] : UITheme.ClubBlue[2];
                var sink = cap.GetComponent<PressSink>();
                if (sink != null) sink.Repaint(img.color);
            }
            if (_settingsLanguage != null)
            {
                string pick = Localization.PreferredCode();
                var info = Languages.Find(pick);
                _settingsLanguage.text = info != null ? info.Name : pick;
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
                            _languageNoteText = Localization.Load(pick).Get("chrome.settings.language_note");
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

        /// <summary>One press of the LANGUAGE row's keys: the next language this build has a table for, remembered
        /// for the next start (<see cref="Localization.Choose"/>).</summary>
        private void StepLanguage(int step)
        {
            var all = Localization.Available;
            if (all.Count == 0) return;
            string pick = Localization.PreferredCode();
            int at = 0;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Code == pick) { at = i; break; }
            at = ((at + step) % all.Count + all.Count) % all.Count;
            Localization.Choose(all[at].Code);
            Sfx.Play("click");
            RefreshSettings();
        }
    }
}
