using System;
using UnityEngine;
using UnityEngine.UI;
using LastCall.Core;

namespace LastCall.UI
{
    /// <summary>
    /// THE PAUSE MENU (2026-09-15, the author: "esc ekranı ve ayaralar key bind ses ve şimdilik daha eklenmeyen kayıt
    /// et devam et butonları ... Palmiye Duvarı kullanılsın"). Escape with nothing open puts the night on hold and
    /// hangs the palm wall over the room: a drawn night (NightArt.Backdrop) under scanlines, a glass plate with the
    /// bar's own keys down it, the hour and the till at its foot. RESUME is the one amber key; SAVE and CONTINUE stand
    /// greyed with a SOON tag until a save layer exists (there is none — TycoonRun.cs:310); SETTINGS opens the window
    /// over this; NEW RUN and QUIT do what they say.
    ///
    /// THE KEYS FIT THEIR WORDS (the author: "butonlar hoverlar dillere göre cümle uzun veya kısa olduğunda flexible
    /// olmalı kesinlikle"): every key here is sized from its label's measured width (FitKey), never from a number.
    ///
    /// Pausing is one flag the clock reads (TycoonHud.Update): the night's scale goes to 0, the room and its weather
    /// with it, and nothing that decides anything is touched.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private RectTransform _pausePanel;
        private Text _pauseFoot, _pauseTill;
        private bool _paused;
        private bool _settingsFromPause;   // the window came from this menu, so BACK returns here

        /// <summary>The night is held: the menu is up, or the settings opened from it are.</summary>
        private bool Paused => _paused;

        private const float PauseKeyH = 50f, PauseKeyMinW = 352f, PausePlateW = 440f, PausePlateH = 500f;

        private void BuildPauseMenu(RectTransform root)
        {
            _pausePanel = NewRect("Pause", root);
            var canvas = _pausePanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 29;                 // over the book (27), under the curtain and the toast (30)
            _pausePanel.gameObject.AddComponent<ForgivingRaycaster>();
            Stretch(_pausePanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // THE NIGHT BEHIND THE MENU: the drawn wall at exactly 2x (640x360 shown over 1280x720), or the flat tile
            // at 2x when the author prefers it, and the scanlines over either. It catches every click — the room
            // under it is on hold, and a click into it must not reach a stool.
            var wall = NewRect("Wall", _pausePanel);
            Stretch(wall, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var wallImg = wall.gameObject.AddComponent<Image>();
            wallImg.sprite = NightArt.Backdrop();
            wallImg.type = NightArt.UseFlatBackdrop ? Image.Type.Tiled : Image.Type.Simple;
            wallImg.pixelsPerUnitMultiplier = 0.5f;   // tiles at 2x
            wallImg.color = Color.white;
            wallImg.raycastTarget = true;
            UiAuditExempt.Mark(wall, "the pause menu's drawn night is 640x360 shown at exactly 2x; the audit reads the stretch as a scale");
            var scrim = NewRect("Dim", _pausePanel);
            Stretch(scrim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrimImg = scrim.gameObject.AddComponent<Image>();
            scrimImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.42f);   // Night[0] at 42%: the wall stays legible
            scrimImg.raycastTarget = false;
            Scanlines(_pausePanel, 0.16f);

            var plate = NewRect("Plate", _pausePanel);
            Place(plate, new Vector2(0.5f, 0.5f), new Vector2(PausePlateW, PausePlateH), new Vector2(0, 0));
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.sprite = NightArt.MenuPlate();
            plateImg.type = Image.Type.Sliced;
            plateImg.raycastTarget = true;
            Scanlines(plate, 0.10f);

            // the title, a shadow copy under it, and the three sunset rules under that
            NightTitle(plate, UIText.T("chrome.pause.title"), -36f);
            SunsetRules(plate, -66f);

            // the keys, top down; each fitted to its word
            float y = -96f;
            var resume = PauseKey(plate, "RESUME", UIText.T("chrome.pause.resume"), "play", UITheme.PrimaryAction, ref y, TogglePause);
            PauseKey(plate, "SAVE", UIText.T("chrome.pause.save"), "save", UITheme.Night[3], ref y, null);
            PauseKey(plate, "CONTINUE", UIText.T("chrome.pause.continue"), "redo", UITheme.Night[3], ref y, null);
            PauseKey(plate, "SETTINGS", UIText.T("chrome.pause.settings"), "cog", UITheme.ClubBlue[2], ref y, () =>
            {
                _settingsFromPause = true;
                _pausePanel.gameObject.SetActive(false);
                if (_settingsPanel != null && !_settingsPanel.gameObject.activeSelf) ToggleSettings();
            });
            PauseKey(plate, "NEW RUN", UIText.T("chrome.pause.new_run"), "moon", UITheme.ClubBlue[2], ref y, () =>
            {
                TogglePause();
                _bootstrap.StartNewRun(null);
            });
            PauseKey(plate, "QUIT", UIText.T("chrome.pause.quit"), "door", UITheme.ClubBlue[2], ref y, () =>
            {
                Sfx.Play("bar_closed", 0.6f);
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });

            // the foot: the hour and the till, so the player knows where they left the night
            var footIcon = NewRect("ClockMark", plate);
            Place(footIcon, new Vector2(0, 0), new Vector2(16, 16), new Vector2(40f, 22f));
            footIcon.pivot = new Vector2(0, 0);
            var fi = footIcon.gameObject.AddComponent<Image>();
            fi.sprite = NightArt.Mark("clock"); fi.color = UITheme.Cream[3]; fi.raycastTarget = false;
            _pauseFoot = NewText("Foot", plate, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            Place(_pauseFoot.rectTransform, new Vector2(0, 0), new Vector2(240, 16), new Vector2(64f, 22f));
            _pauseFoot.rectTransform.pivot = new Vector2(0, 0);
            _pauseFoot.horizontalOverflow = HorizontalWrapMode.Overflow;
            var tillIcon = NewRect("TillMark", plate);
            Place(tillIcon, new Vector2(1, 0), new Vector2(16, 16), new Vector2(-110f, 22f));
            tillIcon.pivot = new Vector2(1, 0);
            var ti = tillIcon.gameObject.AddComponent<Image>();
            ti.sprite = NightArt.Mark("cash"); ti.color = UITheme.Cream[3]; ti.raycastTarget = false;
            _pauseTill = NewText("Till", plate, _figures, 16, TextAnchor.MiddleRight, UITheme.Money);
            Place(_pauseTill.rectTransform, new Vector2(1, 0), new Vector2(100, 20), new Vector2(-40f, 20f));
            _pauseTill.rectTransform.pivot = new Vector2(1, 0);
            _pauseTill.horizontalOverflow = HorizontalWrapMode.Overflow;

            _pausePanel.gameObject.SetActive(false);
        }

        /// <summary>A key on the pause plate: the house key (KeyPlate through NewButton), a mark at its left, the
        /// word fitted, and — with no <paramref name="onClick"/> — greyed with a SOON tag, because the thing it names is
        /// not built yet.</summary>
        private RectTransform PauseKey(RectTransform plate, string id, string label, string mark, Color fill, ref float y, Action onClick)
        {
            bool soon = onClick == null;
            var key = NewButton(plate, label, new Vector2(0.5f, 1), new Vector2(PauseKeyMinW, PauseKeyH),
                new Vector2(0, y), fill, onClick ?? (() => { }));
            key.name = id;
            var face = (RectTransform)key.Find("Face");
            KeyMark(face, mark, soon ? UITheme.Cream[2] : fill == UITheme.PrimaryAction ? UITheme.TextOnAmber : UITheme.Cream[4]);
            var labelText = face.Find("Label").GetComponent<Text>();
            labelText.rectTransform.offsetMin = new Vector2(44f, KeyPlate.Throw);
            if (soon)
            {
                labelText.color = UITheme.Cream[2];
                key.GetComponent<Button>().interactable = false;
                key.GetComponent<PressSink>().enabled = false;
                var tag = NewRect("Soon", face);
                Place(tag, new Vector2(1, 0.5f), new Vector2(60, 20), new Vector2(-12f, KeyPlate.Throw * 0.5f));
                tag.pivot = new Vector2(1, 0.5f);
                var tagImg = tag.gameObject.AddComponent<Image>();
                tagImg.color = UITheme.Magenta[1]; tagImg.raycastTarget = false;
                var tagText = NewText("L", tag, _body, 8, TextAnchor.MiddleCenter, UITheme.Magenta[4]);
                Stretch(tagText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                tagText.text = UIText.T("chrome.pause.soon");
                FitRect(tag, tagText, 16f, 40f);
            }
            else Scanlines(face, 0.22f);
            FitKey(key, PauseKeyMinW, 44f + 24f + (soon ? 72f : 0f));
            y -= PauseKeyH + 10f;
            return key;
        }

        /// <summary>A 16-unit mark inlaid at a key face's left, lifted off the throw like NewButton's own.</summary>
        private void KeyMark(RectTransform face, string mark, Color ink)
        {
            var rt = NewRect("Mark", face);
            Place(rt, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(16f, KeyPlate.Throw * 0.5f));
            rt.pivot = new Vector2(0, 0.5f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = NightArt.Mark(mark) ?? ChromeArt.Mark(mark);
            img.color = ink; img.preserveAspect = true; img.raycastTarget = false;
        }

        /// <summary>The direction's scanlines over a surface: a 1x4 tile at 2x, tinted dark at <paramref name="alpha"/>.</summary>
        private void Scanlines(RectTransform over, float alpha)
        {
            var rt = NewRect("Scan", over);
            Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = NightArt.Scanlines();
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = 0.5f;
            img.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, alpha);   // Night[0], dimmed
            img.raycastTarget = false;
            UiAuditExempt.Mark(rt, "a 1x4 scanline tile shown at 2x over the plate");
        }

        /// <summary>A title in the display face with a one-unit night shadow under it (the sunset text of the mockup,
        /// in the two colours a pixel face can carry).</summary>
        private void NightTitle(RectTransform plate, string word, float y)
        {
            var shadow = NewText("TitleShadow", plate, _display, 24, TextAnchor.MiddleCenter, UITheme.Night[0]);
            Place(shadow.rectTransform, new Vector2(0.5f, 1), new Vector2(PausePlateW - 40f, 32), new Vector2(2f, y - 2f));
            shadow.rectTransform.pivot = new Vector2(0.5f, 1);
            shadow.horizontalOverflow = HorizontalWrapMode.Overflow;
            shadow.text = word;
            var title = NewText("Title", plate, _display, 24, TextAnchor.MiddleCenter, UITheme.Amber[4]);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(PausePlateW - 40f, 32), new Vector2(0, y));
            title.rectTransform.pivot = new Vector2(0.5f, 1);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = word;
        }

        /// <summary>Three two-unit rules in the sunset's colours, under a title.</summary>
        private void SunsetRules(RectTransform plate, float y)
        {
            var cols = new[] { UITheme.Amber[3], UITheme.Magenta[3], UITheme.ClubBlue[3] };
            for (int i = 0; i < cols.Length; i++)
            {
                var rt = NewRect("Rule" + i, plate);
                Place(rt, new Vector2(0.5f, 1), new Vector2(PausePlateW - 80f, 2), new Vector2(0, y - i * 4f));
                rt.pivot = new Vector2(0.5f, 1);
                var img = rt.gameObject.AddComponent<Image>();
                img.color = cols[i]; img.raycastTarget = false;
            }
        }

        /// <summary>Widens a key to its word (2026-09-15, the author: "dillere göre cümle uzun veya kısa olduğunda flexible
        /// olmalı"): the label's measured width plus <paramref name="pad"/>, never under <paramref name="minW"/>, snapped
        /// to the grid. Measured off the Text itself, so a longer translation gets a longer key.</summary>
        private static void FitKey(RectTransform key, float minW, float pad)
        {
            var label = key.GetComponentInChildren<Text>();
            if (label == null) return;
            float want = Mathf.Max(minW, Mathf.Ceil((label.preferredWidth + pad) / 4f) * 4f);
            if (want > key.sizeDelta.x) key.sizeDelta = new Vector2(want, key.sizeDelta.y);
        }

        /// <summary>The same for a plain rect with a text in it (a tag, a caption's box).</summary>
        private static void FitRect(RectTransform rt, Text label, float pad, float minW)
        {
            float want = Mathf.Max(minW, Mathf.Ceil((label.preferredWidth + pad) / 4f) * 4f);
            if (want > rt.sizeDelta.x) rt.sizeDelta = new Vector2(want, rt.sizeDelta.y);
        }

        /// <summary>Escape with nothing open, and the RESUME key: the menu up or down, the night held or let go.</summary>
        private void TogglePause()
        {
            if (_pausePanel == null) return;
            bool show = !_pausePanel.gameObject.activeSelf;
            if (show)
            {
                CloseId();
                RefreshPauseFoot();
                Sfx.Play("screen_on", 0.5f);
            }
            else Sfx.Play("screen_off", 0.4f);
            _pausePanel.gameObject.SetActive(show);
            _paused = show;
            _settingsFromPause = false;
        }

        private void RefreshPauseFoot()
        {
            var run = Run;
            if (run == null || _pauseFoot == null) return;
            double hour = run.Floor != null ? run.Floor.ClockHour : 0;
            int hh = (int)Math.Floor(hour), mm = (int)Math.Floor((hour - hh) * 60.0);
            _pauseFoot.text = UIText.T("chrome.pause.foot", ("day", run.Day), ("night", NightWord(run)),
                ("clock", (hh % 24).ToString("00") + ":" + mm.ToString("00")));
            _pauseTill.text = "$" + run.Money;
        }

        /// <summary>The night's name as the top bar's well prints it, in capitals (BarCalendar).</summary>
        private static string NightWord(TycoonRun run) =>
            UIText.Caps(UIText.T(BarCalendar.NameLine(BarCalendar.NightOf(run.Day))));

        /// <summary>The bar's keys with nothing open (2026-09-15): the book, the cellar, and the music player's next
        /// and pause, on whatever keys the player put them. Read after Escape, which has its own order of things to
        /// close; nothing here runs while the settings window listens for a binding.</summary>
        private void UpdateHotkeys()
        {
            if (_bindListening != null || AnySheetOpen() || Showing(_pausePanel)) return;
            var run = Run;
            if (run == null) return;
            if (Keys.Pressed(KeyAction.NextTrack)) { Sfx.SkipTrack(+1); Sfx.Play("click"); }
            if (Keys.Pressed(KeyAction.MusicToggle)) { Sfx.MusicPaused = !Sfx.MusicPaused; Sfx.Play("click"); }
            if (run.Phase != TycoonPhase.DayOpen) return;
            if (Keys.Pressed(KeyAction.Book)) ToggleRecipeBook();
            else if (Keys.Pressed(KeyAction.Cellar) && stage != null && (_flow == null || !_flow.IsOpen))
                stage.SetDrawerOpen(!(stage.DrawerPhase > 0.5f));
        }
    }
}
