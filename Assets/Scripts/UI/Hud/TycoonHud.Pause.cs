using System;
using UnityEngine;
using UnityEngine.UI;
using LastCall.Core;

namespace LastCall.UI
{
    /// <summary>
    /// THE PAUSE MENU (2026-09-15, the author: "esc ekranı ve ayaralar key bind ses ve şimdilik daha eklenmeyen kayıt
    /// et devam et butonları ... Palmiye Duvarı kullanılsın"). Escape with nothing open STOPS the night — the engine's
    /// own clock (SetPaused: Time.timeScale 0, "ESC'de oyunda her şey durmalı") — and hangs a frame over the room:
    /// the drawn night inside it (NightArt.Night, at 2x, under a tint and scanlines), the bar's keys down it, the hour
    /// and the till at its foot. THE ROOM STAYS AROUND THE FRAME (the author, later the same day: "arkaplan ana ekran
    /// kalsın sadece butonların üstünde olduğu çerçevenin arkaplanı olsun"): no wall, a faint dim that swallows the
    /// clicks. The menu opens and closes on its own two cues (menu_open / menu_close).
    ///
    /// THE KEYS ARE THE AUTHOR'S PACK (the same message: "Butonlar içinde bu dosya yolundaki butonları kullan" —
    /// MenuPack): a worded key stands on the pack's blank cell 9-sliced at 2x and carries one of its glyphs; RESUME is
    /// the one orange key; SAVE and CONTINUE stand greyed with a SOON tag until a save layer exists (there is none —
    /// TycoonRun.cs:310); SETTINGS opens the window over this; NEW RUN and QUIT do what they say.
    ///
    /// THE KEYS FIT THEIR WORDS (the author: "butonlar hoverlar dillere göre cümle uzun veya kısa olduğunda flexible
    /// olmalı kesinlikle"): every key here is sized from its label's measured width (FitKey), never from a number.
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

            // THE ROOM STAYS, DARKENED (2026-09-16, the author: "ESC menüsü açıldığında arka plan karartılmalı"): the
            // house scrim over it, which also catches every click — the room under it is on hold, and a click into
            // it must not reach a stool.
            var dim = NewRect("Dim", _pausePanel);
            Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = UITheme.Scrim;
            dimImg.raycastTarget = true;

            var plate = NightPlate(_pausePanel, "Plate", new Vector2(PausePlateW, PausePlateH), 0.10f);

            // the title, a shadow copy under it, and the three sunset rules under that
            NightTitle(plate, UIText.T("chrome.pause.title"), -36f);
            SunsetRules(plate, -66f, PausePlateW - 80f);

            // the keys, top down; each fitted to its word
            float y = -96f;
            PauseKey(plate, "RESUME", UIText.T("chrome.pause.resume"), "play", MenuPack.Tone.Orange, ref y, TogglePause);
            PauseKey(plate, "SAVE", UIText.T("chrome.pause.save"), "save", MenuPack.Tone.Grey, ref y, null);
            PauseKey(plate, "CONTINUE", UIText.T("chrome.pause.continue"), "lock", MenuPack.Tone.Grey, ref y, null);
            PauseKey(plate, "SETTINGS", UIText.T("chrome.pause.settings"), "cog", MenuPack.Tone.Grey, ref y, () =>
            {
                Sfx.Play("click");
                _settingsFromPause = true;
                _pausePanel.gameObject.SetActive(false);
                if (_settingsPanel != null && !_settingsPanel.gameObject.activeSelf) ToggleSettings();
            });
            PauseKey(plate, "NEW RUN", UIText.T("chrome.pause.new_run"), "restart", MenuPack.Tone.Grey, ref y, () =>
            {
                TogglePause();
                _bootstrap.StartNewRun(null);
            });
            PauseKey(plate, "QUIT", UIText.T("chrome.pause.quit"), "exit", MenuPack.Tone.Grey, ref y, () =>
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

        /// <summary>A key on the pause plate: the pack's worded key with a glyph at its left, fitted to its word, and
        /// — with no <paramref name="onClick"/> — greyed with a SOON tag, because the thing it names is not built yet.</summary>
        private RectTransform PauseKey(RectTransform plate, string id, string label, string glyph, MenuPack.Tone tone, ref float y, Action onClick)
        {
            bool soon = onClick == null;
            var key = PackWordKey(plate, id, label, glyph, tone, new Vector2(0.5f, 1), new Vector2(PauseKeyMinW, PauseKeyH),
                new Vector2(0, y), onClick ?? (() => { }), PauseKeyMinW, 48f + 24f + (soon ? 72f : 0f));
            if (soon)
            {
                // The pack has no disabled drawing: the plate, the glyph and the word go to half light, and the key
                // stops answering the pointer.
                key.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f, 1f);   // a uniform dim, so the audit reads it as one
                key.GetComponent<Button>().interactable = false;
                key.GetComponent<PressSink>().enabled = false;
                key.GetComponent<PackKey>().enabled = false;
                var face = (RectTransform)key.Find("Face");
                face.Find("Label").GetComponent<Text>().color = UITheme.Cream[2];
                var glyphImg = face.Find("Glyph");
                if (glyphImg != null) glyphImg.GetComponent<Image>().color = UITheme.Cream[2];
                var tag = NewRect("Soon", face);
                Place(tag, new Vector2(1, 0.5f), new Vector2(60, 20), new Vector2(-12f, 2f));
                tag.pivot = new Vector2(1, 0.5f);
                var tagImg = tag.gameObject.AddComponent<Image>();
                tagImg.color = UITheme.Magenta[1]; tagImg.raycastTarget = false;
                var tagText = NewText("L", tag, _body, 8, TextAnchor.MiddleCenter, UITheme.Magenta[4]);
                Stretch(tagText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                tagText.text = UIText.T("chrome.pause.soon");
                FitRect(tag, tagText, 16f, 40f);
            }
            y -= PauseKeyH + 10f;
            return key;
        }

        // ── the framed night and the pack's keys (shared with the settings window) ────────────────────────────────

        /// <summary>A FRAMED NIGHT (2026-09-15): the drawn night stands INSIDE the frame at exactly 2x — drawn at the
        /// frame's own half size, cropped to its inside, under a tint of <paramref name="tint"/> and the scanlines —
        /// and the frame (NightArt.MenuFrame, its inner line the picture's mat) lies over it. The plate catches every
        /// click that lands on it, so nothing under it is pressed through it.</summary>
        private RectTransform NightPlate(RectTransform parent, string name, Vector2 size, float tint)
        {
            var plate = NewRect(name, parent);
            Place(plate, new Vector2(0.5f, 0.5f), size, Vector2.zero);
            var catcher = plate.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0.004f);
            catcher.raycastTarget = true;
            plate.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            var pic = NewRect("Picture", plate);
            Stretch(pic, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            pic.gameObject.AddComponent<RectMask2D>();
            int w = Mathf.CeilToInt((size.x - 6f) / 2f), h = Mathf.CeilToInt((size.y - 6f) / 2f);
            var art = NewRect("Night", pic);
            Place(art, new Vector2(0.5f, 0.5f), new Vector2(w * 2, h * 2), Vector2.zero);
            var ai = art.gameObject.AddComponent<Image>();
            ai.sprite = NightArt.Picture(w, h);
            ai.type = NightArt.UseFlatBackdrop ? Image.Type.Tiled : Image.Type.Simple;
            ai.pixelsPerUnitMultiplier = 0.5f;
            ai.color = Color.white;
            ai.raycastTarget = false;
            UiAuditExempt.Mark(art, "the menu's drawn night, " + w + "x" + h + " shown at exactly 2x inside its frame");
            var glass = NewRect("Tint", pic);
            Stretch(glass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gi = glass.gameObject.AddComponent<Image>();
            gi.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, tint);
            gi.raycastTarget = false;
            Scanlines(pic, 0.12f);

            var frame = NewRect("Frame", plate);
            Stretch(frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fi = frame.gameObject.AddComponent<Image>();
            fi.sprite = NightArt.MenuFrame();
            fi.type = Image.Type.Sliced;
            fi.color = Color.white;
            fi.raycastTarget = false;
            return plate;
        }

        /// <summary>A WORDED KEY from the author's pack (2026-09-15, MenuPack): the pack's blank cell 9-sliced at 2x
        /// for the plate, one of its glyphs at the left in its own inks, the word in the house face. Under the pointer
        /// the glyph lights; pressed, the plate swaps to the pack's own pressed drawing (the rim two units lower) and
        /// the face travels with it. Widened to its word (FitKey) from <paramref name="minW"/>.</summary>
        private RectTransform PackWordKey(RectTransform parent, string id, string label, string glyph, MenuPack.Tone tone,
            Vector2 anchor, Vector2 size, Vector2 pos, Action onClick, float minW, float pad)
        {
            var rt = NewRect(id, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var plate = rt.gameObject.AddComponent<Image>();
            plate.sprite = MenuPack.Blank(tone, false);
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 0.5f;          // the pack at exactly 2x
            plate.color = Color.white;
            plate.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = plate;
            button.onClick.AddListener(() => onClick());
            var face = NewRect("Face", rt);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var sink = rt.gameObject.AddComponent<PressSink>();
            sink.Face = face; sink.Depth = 2f; sink.Lift = 2f; sink.Squash = 0f; sink.Bloom = 0f;
            Image glyphImg = null;
            if (glyph != null)
            {
                var g = NewRect("Glyph", face);
                Place(g, new Vector2(0, 0.5f), new Vector2(32, 32), new Vector2(10f, 1f));
                g.pivot = new Vector2(0, 0.5f);
                glyphImg = g.gameObject.AddComponent<Image>();
                glyphImg.sprite = MenuPack.Glyph(glyph);
                glyphImg.color = MenuPack.Ink(tone, false);
                glyphImg.raycastTarget = false;
            }
            var pk = rt.gameObject.AddComponent<PackKey>();
            pk.Plate = plate;
            pk.Rest = plate.sprite; pk.Lit = plate.sprite; pk.Pressed = MenuPack.Blank(tone, true);
            pk.Glyph = glyphImg; pk.GlyphRest = MenuPack.Ink(tone, false); pk.GlyphLit = MenuPack.Ink(tone, true);
            // THE WORD SITS DEAD CENTRE ON THE FACE (2026-09-16, the author: "butonların üstündeki yazılar butonların
            // tam ortasında olsun"; measured off a capture): centred across the WHOLE key, not the part right of the
            // glyph — which put it twenty units off — and two units up, because the pack's face runs from the rim
            // under the outline to the shadow, whose middle is two units above the rect's. The pad below keeps a
            // key wide enough that a centred word never reaches the glyph.
            var text = NewText("Label", face, _body, size.y >= 32f ? 16 : 8, TextAnchor.MiddleCenter, MenuPack.Word(tone));
            // (9 and -7: the face's glyphs land one unit left of the box's middle — their bearing — measured too.)
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(9f, 4f), new Vector2(-7f, 0f));
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = label;
            FitKey(rt, minW, glyph != null ? Mathf.Max(pad, 100f) : pad);
            return rt;
        }

        /// <summary>A worded key changing tone (a tab lit, a flag chosen, a switch thrown): new drawings and inks.</summary>
        private static void RetoneWordKey(RectTransform key, MenuPack.Tone tone)
        {
            var pk = key.GetComponent<PackKey>();
            if (pk == null) return;
            pk.Refit(MenuPack.Blank(tone, false), MenuPack.Blank(tone, false), MenuPack.Blank(tone, true),
                MenuPack.Ink(tone, false), MenuPack.Ink(tone, true));
            var label = key.Find("Face/Label");
            if (label != null) label.GetComponent<Text>().color = MenuPack.Word(tone);
        }

        /// <summary>An ICON KEY from the pack: one cell at 2x (32x32) — the blank cell for the plate and the glyph
        /// over it in the pack's inks, which is how the pack's own cells are built and lets the two glyphs the pack
        /// lacks (prev, next) stand on the same plate. Lit under the pointer, the pack's pressed drawing while held.
        /// The meters' - and +, the player's three keys, the sound switch.</summary>
        private RectTransform PackIconKey(RectTransform parent, string id, string icon, MenuPack.Tone tone, Vector2 anchor, Vector2 pos, Action onClick)
        {
            var rt = NewRect(id, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = new Vector2(32f, 32f);
            rt.anchoredPosition = pos;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = MenuPack.Blank(tone, false);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            var face = NewRect("Face", rt);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var sink = rt.gameObject.AddComponent<PressSink>();
            sink.Face = face; sink.Depth = 2f; sink.Lift = 2f; sink.Squash = 0f; sink.Bloom = 0f;
            var g = NewRect("Glyph", face);
            Stretch(g, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gi = g.gameObject.AddComponent<Image>();
            gi.sprite = MenuPack.Glyph(icon);
            gi.color = MenuPack.Ink(tone, false);
            gi.raycastTarget = false;
            var pk = rt.gameObject.AddComponent<PackKey>();
            pk.Plate = img;
            pk.Rest = img.sprite; pk.Lit = img.sprite; pk.Pressed = MenuPack.Blank(tone, true);
            pk.Glyph = gi; pk.GlyphRest = MenuPack.Ink(tone, false); pk.GlyphLit = MenuPack.Ink(tone, true);
            return rt;
        }

        /// <summary>An icon key changing its drawing (the hold key between pause and play, the sound switch).</summary>
        private static void ReiconKey(RectTransform key, MenuPack.Tone tone, string icon)
        {
            var pk = key.GetComponent<PackKey>();
            if (pk == null) return;
            if (pk.Glyph != null) pk.Glyph.sprite = MenuPack.Glyph(icon);
            pk.Refit(MenuPack.Blank(tone, false), MenuPack.Blank(tone, false), MenuPack.Blank(tone, true),
                MenuPack.Ink(tone, false), MenuPack.Ink(tone, true));
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

        /// <summary>Three two-unit rules in the sunset's colours, under a title, <paramref name="width"/> wide.</summary>
        private void SunsetRules(RectTransform plate, float y, float width)
        {
            var cols = new[] { UITheme.Amber[3], UITheme.Magenta[3], UITheme.ClubBlue[3] };
            for (int i = 0; i < cols.Length; i++)
            {
                var rt = NewRect("Rule" + i, plate);
                Place(rt, new Vector2(0.5f, 1), new Vector2(width, 2), new Vector2(0, y - i * 4f));
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
                Sfx.Play("menu_open", 0.7f);
            }
            else Sfx.Play("menu_close", 0.6f);
            _pausePanel.gameObject.SetActive(show);
            SetPaused(show);
            _settingsFromPause = false;
        }

        /// <summary>EVERYTHING STOPS (2026-09-15, the author: "ESC'de oyunda her şey durmalı"): the engine's clock goes
        /// to zero, so the sim, the room, the weather, a pour in the glass and a drinker mid-step all hold where they
        /// are; the HUD's own motion (PressSink, the hover plates, the music's crossfade) runs on unscaled time and
        /// keeps answering the pointer. Let go here, by CloseEverySheet, and by OnDestroy — the editor keeps the
        /// scale across plays.</summary>
        private void SetPaused(bool on)
        {
            _paused = on;
            Time.timeScale = on ? 0f : 1f;
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
