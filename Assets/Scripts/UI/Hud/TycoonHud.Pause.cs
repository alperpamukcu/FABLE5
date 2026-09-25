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
        private bool _paused;
        private bool _settingsFromPause;   // the window came from this menu, so BACK returns here

        /// <summary>The night is held: the menu is up, or the settings opened from it are.</summary>
        private bool Paused => _paused;

        private const float PauseKeyH = 50f, PauseKeyMinW = 352f, PausePlateW = 440f, PausePlateH = 500f;

        /// <summary>What the room goes under while a menu is up: the house scrim's night at 85% (2026-09-16, the
        /// author, after the 70% scrim: "Arkaplan biraz daha karartılsın") — an alpha of a token.</summary>
        private static Color MenuScrim => new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.85f);

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
            dimImg.color = MenuScrim;
            dimImg.raycastTarget = true;

            // THE PLATE IS A PICTURE (2026-09-26, the author: "ESC için arkaplanda kullanılan mavi UI yerine arkaplan
            // görseli üret"): the generated panel (MenuPack.Art "menu_esc_bg") at exactly 2x - its own neon edge, a
            // sunburst behind the title and a calm middle the keys stand on. Without it, the blue plate the certificate
            // and the room's tips stand on (2026-09-21, "ESC menüsü backbar tasarımı ile uyumlu olmalı").
            var bg = MenuPack.Art("menu_esc_bg");
            var plate = bg != null ? PicturePlate(_pausePanel, "Plate", bg)
                                   : BluePlate(_pausePanel, "Plate", new Vector2(PausePlateW, PausePlateH));

            // the title, a shadow copy under it; the three sunset rules under it on the blue plate, while the picture's
            // own sunburst stands behind it and the word catches like a tube (NeonFlicker)
            NightTitle(plate, UIText.T("chrome.pause.title"), -36f);
            if (bg == null) SunsetRules(plate, -66f, PausePlateW - 80f);
            else plate.Find("Title")?.gameObject.AddComponent<NeonFlicker>();

            // the keys, top down; each fitted to its word
            float y = -96f;
            var keys = new System.Collections.Generic.List<RectTransform>();
            keys.Add(PauseKey(plate, "RESUME", UIText.T("chrome.pause.resume"), "play", MenuPack.Tone.Orange, ref y, TogglePause));
            keys.Add(PauseKey(plate, "SAVE", UIText.T("chrome.pause.save"), "save", MenuPack.Tone.Grey, ref y, null));
            keys.Add(PauseKey(plate, "CONTINUE", UIText.T("chrome.pause.continue"), "lock", MenuPack.Tone.Grey, ref y, null));
            keys.Add(PauseKey(plate, "SETTINGS", UIText.T("chrome.pause.settings"), "cog", MenuPack.Tone.Grey, ref y, () =>
            {
                Sfx.Play("click");
                _settingsFromPause = true;
                _pausePanel.gameObject.SetActive(false);
                if (_settingsPanel != null && !_settingsPanel.gameObject.activeSelf) ToggleSettings();
            }));
            keys.Add(PauseKey(plate, "NEW RUN", UIText.T("chrome.pause.new_run"), "restart", MenuPack.Tone.Grey, ref y, () =>
            {
                TogglePause();
                _bootstrap.StartNewRun(null);
            }));
            keys.Add(PauseKey(plate, "QUIT", UIText.T("chrome.pause.quit"), "exit", MenuPack.Tone.Grey, ref y, () =>
            {
                Sfx.Play("bar_closed", 0.6f);
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }));
            BuildPauseFoot(plate);
            _pausePanel.gameObject.SetActive(false);
        }

        private SegmentClock _pauseClock;
        private SegmentFigure _pauseTillFigure;
        private Text _pauseNightNo, _pauseNightName;
        private const float PauseFootH = 38f, PauseFootEdge = 12f, PauseFootY = 10f, PauseFootGap = 6f, PauseFootPad = 8f;

        /// <summary>
        /// THE FOOT, AS THE BEAM'S OWN INSTRUMENTS (2026-09-26, the author, of the 8px line and the orange till under the
        /// keys: "Görseldeki yazıların tarzını değiş"). Two wells on the beam's glass (ChromeArt.Well): the HOUR - the
        /// top bar's segment clock in cyan, the night's number over its name - and the TILL - the register's green figure,
        /// red in the red. The same readouts the beam carries, so the menu says where the night was left in the words the
        /// bar already uses. The night's name steps down to the 8 size when a language's weekday outgrows its box.
        /// </summary>
        private void BuildPauseFoot(RectTransform plate)
        {
            float plateW = plate.sizeDelta.x;
            float tillW = SegmentFigure.Width + TopWellPad * 2f;
            float hourW = plateW - PauseFootEdge * 2f - PauseFootGap - tillW;   // 98 units left for the night's name

            var hour = NewRect("FootHour", plate);
            hour.anchorMin = hour.anchorMax = hour.pivot = Vector2.zero;
            hour.sizeDelta = new Vector2(hourW, PauseFootH);
            hour.anchoredPosition = new Vector2(PauseFootEdge, PauseFootY);
            var hi = hour.gameObject.AddComponent<Image>();
            hi.sprite = ChromeArt.Well(); hi.type = Image.Type.Sliced; hi.raycastTarget = false;
            var digits = NewRect("Digits", hour);
            Place(digits, new Vector2(0, 0.5f), new Vector2(TopHourDigitsW, 28), new Vector2(PauseFootPad, 0));
            _pauseClock = new SegmentClock(digits, UITheme.Cyan[4]);
            float dayX = PauseFootPad + TopHourDigitsW + 6f, dayW = hourW - dayX - PauseFootPad;
            _pauseNightNo = NewText("NightNo", hour, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            Place(_pauseNightNo.rectTransform, new Vector2(0, 0.5f), new Vector2(dayW, 12), new Vector2(dayX, 8f));
            _pauseNightNo.rectTransform.pivot = new Vector2(0, 0.5f);
            _pauseNightNo.horizontalOverflow = HorizontalWrapMode.Overflow;
            _pauseNightName = NewText("NightName", hour, _body, 16, TextAnchor.MiddleLeft, UITheme.Amber[4]);
            Place(_pauseNightName.rectTransform, new Vector2(0, 0.5f), new Vector2(dayW, 18), new Vector2(dayX, -6f));
            _pauseNightName.rectTransform.pivot = new Vector2(0, 0.5f);
            _pauseNightName.horizontalOverflow = HorizontalWrapMode.Overflow;

            var till = NewRect("FootTill", plate);
            till.anchorMin = till.anchorMax = till.pivot = new Vector2(1, 0);
            till.sizeDelta = new Vector2(tillW, PauseFootH);
            till.anchoredPosition = new Vector2(-PauseFootEdge, PauseFootY);
            var ti = till.gameObject.AddComponent<Image>();
            ti.sprite = ChromeArt.Well(); ti.type = Image.Type.Sliced; ti.raycastTarget = false;
            var host = NewRect("Figure", till);
            Place(host, new Vector2(0, 0.5f), new Vector2(SegmentFigure.Width, 32), new Vector2(TopWellPad, 0));
            host.pivot = new Vector2(0, 0.5f);
            _pauseTillFigure = new SegmentFigure(host, UITheme.Lime[4]);
        }

        /// <summary>
        /// A GENERATED PLATE (MenuPack.Art): the picture at exactly twice its size, catching every click that lands on it
        /// so nothing under it is pressed through it.
        /// </summary>
        private RectTransform PicturePlate(RectTransform parent, string name, Sprite art)
        {
            var plate = NewRect(name, parent);
            Place(plate, new Vector2(0.5f, 0.5f), new Vector2(art.rect.width * 2f, art.rect.height * 2f), Vector2.zero);
            var img = plate.gameObject.AddComponent<Image>();
            img.sprite = art;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = true;
            plate.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            UiAuditExempt.Mark(plate, "the menu's generated picture, " + art.rect.width + "x" + art.rect.height + " shown at exactly 2x");
            return plate;
        }

        /// <summary>A key on the pause plate: the pack's worded key with a glyph at its left, fitted to its word, and
        /// — with no <paramref name="onClick"/> — greyed with a SOON tag, because the thing it names is not built yet.</summary>
        private RectTransform PauseKey(RectTransform plate, string id, string label, string glyph, MenuPack.Tone tone, ref float y, Action onClick)
        {
            bool soon = onClick == null;
            var key = PackWordKey(plate, id, label, glyph, tone, new Vector2(0.5f, 1), new Vector2(PauseKeyMinW, PauseKeyH),
                new Vector2(0, y), onClick ?? (() => { }), PauseKeyMinW, 48f + 24f + (soon ? 72f : 0f));
            SurfaceKey(key, tone);                     // before the SOON dim below, which the surface then takes too
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
                if (glyphImg != null)
                {
                    var gi = glyphImg.GetComponent<Image>();
                    // a brass icon takes the plate's own uniform dim; a pack mask goes to the half-light cream
                    gi.color = MenuPack.IsIcon(gi.sprite) ? new Color(0.55f, 0.55f, 0.55f, 1f) : UITheme.Cream[2];
                }
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
        private RectTransform NightPlate(RectTransform parent, string name, Vector2 size, float tint, Sprite art = null)
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
            // A GENERATED PICTURE (2026-09-25, MenuPack.Art) is drawn the same way - at its own size times two, in the
            // same window - so a window sized from it (art x 2 + the frame's 6) shows it whole at exactly 2x.
            if (art != null) { w = Mathf.RoundToInt(art.rect.width); h = Mathf.RoundToInt(art.rect.height); }
            var night = NewRect("Night", pic);
            Place(night, new Vector2(0.5f, 0.5f), new Vector2(w * 2, h * 2), Vector2.zero);
            var ai = night.gameObject.AddComponent<Image>();
            ai.sprite = art != null ? art : NightArt.Picture(w, h);
            ai.type = Image.Type.Simple;
            ai.color = Color.white;
            ai.raycastTarget = false;
            UiAuditExempt.Mark(night, "the menu's glass (or drawn night), " + w + "x" + h + " shown at exactly 2x inside its frame");
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
            plate.sprite = MenuPack.Paletted(tone, false);
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
            // A KEY GROWS UNDER THE POINTER (2026-09-22, the author: "tüm hoverlara açılma ve kapanma animasyonu
            // ekle, mousun olduğu yerden büyüsünler ve küçülsünler"): three hundredths on the pack's own hover
            // clock, so the plate breathes rather than jumps.
            sink.Face = face; sink.Depth = 2f; sink.Lift = 2f; sink.Squash = 0f; sink.Bloom = 0.03f;
            Image glyphImg = null;
            // A GREY key shows the shipped brass icon in the glyph's slot when there is one (MenuPack.IconFor, 32x32 at
            // 1x, the top bar's star3d / cog3d family), never tinted; the amber and lime keys keep the pack's mask in
            // their own inks, where brass would sit on brass.
            var icon = glyph != null && tone == MenuPack.Tone.Grey ? MenuPack.IconFor(glyph) : null;
            if (glyph != null)
            {
                var g = NewRect("Glyph", face);
                Place(g, new Vector2(0, 0.5f), new Vector2(32, 32), new Vector2(10f, 1f));
                g.pivot = new Vector2(0, 0.5f);
                glyphImg = g.gameObject.AddComponent<Image>();
                glyphImg.sprite = icon != null ? icon : MenuPack.Glyph(glyph);
                glyphImg.color = icon != null ? Color.white : MenuPack.PalettedInk(tone, false);
                glyphImg.raycastTarget = false;
            }
            var pk = rt.gameObject.AddComponent<PackKey>();
            pk.Plate = plate;
            // THE HOVER IS THE BLUE ONE (2026-09-22, the author: "menünün hover UI'ı eski kalmış, mavi olanla
            // değiştir"): the pack's own lit sheet was its factory blue-grey; a key goes to the game's club blue
            // under the pointer, which is the answer the back bar already gives.
            pk.Rest = plate.sprite; pk.Lit = MenuPack.Hovered(); pk.Pressed = MenuPack.Paletted(tone, true);
            pk.Glyph = glyphImg;
            pk.GlyphRest = icon != null ? Color.white : MenuPack.PalettedInk(tone, false);
            pk.GlyphLit = icon != null ? Color.white : MenuPack.PalettedInk(tone, true);
            // THE WORD SITS DEAD CENTRE ON THE FACE (2026-09-16, the author: "butonların üstündeki yazılar butonların
            // tam ortasında olsun"; measured off a capture): centred across the WHOLE key, not the part right of the
            // glyph — which put it twenty units off — and two units up, because the pack's face runs from the rim
            // under the outline to the shadow, whose middle is two units above the rect's. The pad below keeps a
            // key wide enough that a centred word never reaches the glyph.
            var text = NewText("Label", face, _body, size.y >= 32f ? 16 : 8, TextAnchor.MiddleCenter, MenuPack.PalettedInk(tone, false));
            // (9 and -7: the face's glyphs land one unit left of the box's middle — their bearing — measured too.)
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(9f, 4f), new Vector2(-7f, 0f));
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = label;
            FitKey(rt, minW, glyph != null ? Mathf.Max(pad, 100f) : pad);
            return rt;
        }

        /// <summary>Which surface the ESC keys wear (ChromeArt.KeySurfaceTile). A field, so a probe can put the three
        /// side by side.</summary>
        internal static ChromeArt.KeySurface PauseKeySurface = ChromeArt.KeySurface.Terrazzo;

        /// <summary>
        /// THE KEY'S SURFACE (2026-09-25, the author: "ESC menüsündeki butonlara yüzey deseni ekle"). A tiled layer over
        /// the plate and under the face (glyph, word, SOON tag), inset to the face the pack's drawing leaves inside its
        /// outline, corner glints and shadow row: (4, 8) to (-4, -4) at rest, measured off pack_grey_blank at 2x, and
        /// concentric with the word. It is the key root's own child, NOT the face's: the face grows 3% under the
        /// pointer (PressSink.Bloom) and would carry the surface over the rim. PackKey inks it with the plate's state
        /// and drops it the pressed drawing's 2 units.
        /// </summary>
        private static void SurfaceKey(RectTransform key, MenuPack.Tone tone)
        {
            var rt = NewRect("Surface", key);
            Stretch(rt, Vector2.zero, Vector2.one, new Vector2(4f, 8f), new Vector2(-4f, -4f));
            rt.SetAsFirstSibling();
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.KeySurfaceTile(PauseKeySurface);
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = 0.5f;        // one texel is two units, the pack's own 2x
            img.raycastTarget = false;
            var pk = key.GetComponent<PackKey>();
            pk.Surface = img;
            pk.SurfaceRest = MenuPack.SurfaceInk(tone);
            pk.SurfaceLit = MenuPack.SurfaceHover;
            pk.Apply();
        }

        /// <summary>A worded key changing tone (a tab lit, a flag chosen, a switch thrown): new drawings and inks.</summary>
        private static void RetoneWordKey(RectTransform key, MenuPack.Tone tone)
        {
            var pk = key.GetComponent<PackKey>();
            if (pk == null) return;
            bool icon = pk.Glyph != null && MenuPack.IsIcon(pk.Glyph.sprite);     // a brass icon keeps its own colours
            pk.Refit(MenuPack.Paletted(tone, false), MenuPack.Hovered(), MenuPack.Paletted(tone, true),
                icon ? Color.white : MenuPack.PalettedInk(tone, false), icon ? Color.white : MenuPack.PalettedInk(tone, true));
            var label = key.Find("Face/Label");
            if (label != null) label.GetComponent<Text>().color = MenuPack.PalettedInk(tone, false);
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
            img.sprite = MenuPack.Paletted(tone, false);
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
            sink.Face = face; sink.Depth = 2f; sink.Lift = 2f; sink.Squash = 0f; sink.Bloom = 0.05f;
            var g = NewRect("Glyph", face);
            Stretch(g, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gi = g.gameObject.AddComponent<Image>();
            gi.sprite = MenuPack.Glyph(icon);
            gi.color = MenuPack.PalettedInk(tone, false);
            gi.raycastTarget = false;
            var pk = rt.gameObject.AddComponent<PackKey>();
            pk.Plate = img;
            pk.Rest = img.sprite; pk.Lit = MenuPack.Hovered(); pk.Pressed = MenuPack.Paletted(tone, true);
            pk.Glyph = gi; pk.GlyphRest = MenuPack.PalettedInk(tone, false); pk.GlyphLit = MenuPack.PalettedInk(tone, true);
            return rt;
        }

        /// <summary>An icon key changing its drawing (the hold key between pause and play, the sound switch).</summary>
        private static void ReiconKey(RectTransform key, MenuPack.Tone tone, string icon)
        {
            var pk = key.GetComponent<PackKey>();
            if (pk == null) return;
            if (pk.Glyph != null) pk.Glyph.sprite = MenuPack.Glyph(icon);
            pk.Refit(MenuPack.Paletted(tone, false), MenuPack.Hovered(), MenuPack.Paletted(tone, true),
                MenuPack.PalettedInk(tone, false), MenuPack.PalettedInk(tone, true));
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
            if (run == null || _pauseClock == null) return;
            double hour = run.Floor != null ? run.Floor.ClockHour : 0;
            int hh = (int)Math.Floor(hour), mm = (int)Math.Floor((hour - hh) * 60.0);
            _pauseClock.Show(hh % 24, mm, true);                  // held: the colon stands lit
            _pauseNightNo.text = UIText.T("hud.day_well.caption") + " " + run.Day;
            _pauseNightName.text = NightWord(run);
            _pauseNightName.fontSize = LanguageFonts.Size(_pauseNightName.font, 16);
            if (_pauseNightName.preferredWidth > _pauseNightName.rectTransform.sizeDelta.x)
                _pauseNightName.fontSize = LanguageFonts.Size(_pauseNightName.font, 8);
            _pauseTillFigure.SetHue(run.Money < 0 ? UITheme.ViceRed[3] : UITheme.Lime[4]);
            _pauseTillFigure.Show(run.Money);
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
