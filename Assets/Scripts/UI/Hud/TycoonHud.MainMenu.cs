using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastCall.Game;

namespace LastCall.UI
{
    /// <summary>
    /// THE FRONT DOOR (2026-09-26; repainted flat 2026-09-27 on the author's second look: "Ana menüyü
    /// sevmedim kalitesi düşük düz arkaplan ve hareketli iconlar olabilir ... oyunda kullandığımız logo
    /// giriş sayfasında olmalı"; and dressed the same evening: "ana menüyü geliştir tasarımını ve
    /// butonların üstündeki iconları güncelle", with a CREDITS door).
    ///
    /// A cold boot opens on a FLAT plum field under a soft vignette, the game's empty glassware drifting
    /// slowly behind everything readable, and the store's own neon lockup (menu_logo) as the title,
    /// breathing like a tube (NeonPulse). The keys wear the 1-bit pack's marks (Nikoichu, CC0 - credited)
    /// at exactly 2x: CONTINUE (always on the board - greyed while no save stands, its note naming the
    /// night and the till when one does), NEW RUN (a fresh SeedPolicy seed), SETTINGS, QUIT; under them a
    /// neon rule and the small row - AUDIO and LANGUAGE straight to their pages, CREDITS, and STEAM once
    /// StoreLink carries an address. The keys rise into place one after another as the title fades up,
    /// and a neon pointer stands beside whichever key the mouse is over. Everything that moves is
    /// arithmetic on the unscaled clock (no RNG stream is ever touched) and holds still under REDUCED
    /// MOTION; the logo's breath also stops with FLASHES off.
    ///
    /// The menu appears ONCE per boot, never after a language reload (GameBootstrap.ResumedAcrossReload),
    /// never after START OVER, and only through the pause menu's MAIN MENU key after that. While it is up
    /// the night is held exactly the way the pause holds it, and Escape does nothing - the menu's own
    /// keys are its doors. It plays its own record (the "menu" mood, music_menu_1).
    /// </summary>
    public sealed partial class TycoonHud
    {
        private RectTransform _menuPanel;
        private RectTransform _menuColumn;
        private CanvasGroup _menuFade;
        private float _menuFadeT = 1f;
        private float _menuShownAt;         // the drift's and the entrance's own zero
        private bool _menuOffered;          // one offer per HUD life: the cold boot's
        private bool _settingsFromMenu;     // the window came from the menu, so BACK returns there

        private const float MenuKeyH = 50f, MenuKeyMinW = 352f, MenuColW = 440f;

        /// <summary>The flat field the title stands on — the palette's deep plum.</summary>
        private static Color MenuField => UITheme.Night[1];

        /// <summary>The menu is up (the settings opened from it count — the night stays held).</summary>
        private bool MenuUp => _menuPanel != null && _menuPanel.gameObject.activeSelf;

        // ── the drift: the game's empty glasses, floating (2026-09-27) ──────────────────

        private readonly List<RectTransform> _menuDrifters = new List<RectTransform>();
        private static readonly string[] DriftArt =
            { "glass3d_martini", "glass3d_rocks", "glass3d_highball", "glass3d_coupe", "glass3d_pint" };
        private const int DriftCount = 10;
        private const float DriftAlpha = 0.13f;

        // ── the keys' entrance and the pointer (2026-09-27) ─────────────────────────────

        private sealed class MenuKeyEntry { public RectTransform Key; public Vector2 Home; public CanvasGroup Fade; public bool Live; }
        private readonly List<MenuKeyEntry> _menuKeys = new List<MenuKeyEntry>();
        private RectTransform _menuPointer;
        private RectTransform _menuHover;
        private const float EnterDelay = 0.22f, EnterStagger = 0.06f, EnterTime = 0.24f, EnterRise = 14f;

        private void BuildMainMenu(RectTransform root)
        {
            _menuPanel = NewRect("MainMenu", root);
            var canvas = _menuPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 31;      // over the curtain (30): the front door covers the whole bar
            _menuPanel.gameObject.AddComponent<ForgivingRaycaster>();
            Stretch(_menuPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // The flat field, catching every click that lands on it.
            var field = NewRect("Field", _menuPanel);
            Stretch(field, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fieldImg = field.gameObject.AddComponent<Image>();
            fieldImg.color = MenuField;
            fieldImg.raycastTarget = true;

            // The drifting glassware, between the field and everything readable.
            var drift = NewRect("Drift", _menuPanel);
            Stretch(drift, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _menuDrifters.Clear();
            for (int i = 0; i < DriftCount; i++)
            {
                var art = ItemArt.Load(DriftArt[i % DriftArt.Length]);
                if (art == null) continue;
                var rt = NewRect("D" + i, drift);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = art.rect.size * 2f;             // the house scale
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = art;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, DriftAlpha);
                rt.anchoredPosition = DriftAt(i, 0f);
                rt.localRotation = Quaternion.Euler(0, 0, ((i * 53) % 24) - 12);
                _menuDrifters.Add(rt);
            }

            // THE VIGNETTE (2026-09-27): the field darkens toward its edges, so the drifting glasses
            // fall away into the dark and the title and the keys stand forward. One small soft texture,
            // the palette's own night at an alpha - a soft gradient on purpose, the one smooth thing on
            // the screen, so it reads as light rather than as a drawn shape.
            var vig = NewRect("Vignette", _menuPanel);
            Stretch(vig, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var vigImg = vig.gameObject.AddComponent<RawImage>();
            vigImg.texture = MenuVignette();
            vigImg.raycastTarget = false;
            UiAuditExempt.Mark(vig, "the front door's vignette: one soft gradient, the palette's night at an alpha");

            // THE LOGO IS THE TITLE: the store's own lockup, 1:1 at the size it was shipped, breathing.
            var logo = MenuPack.Art("menu_logo");
            if (logo != null)
            {
                var rt = NewRect("Logo", _menuPanel);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = logo.rect.size;
                rt.anchoredPosition = new Vector2(0f, 186f);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = logo;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.gameObject.AddComponent<NeonPulse>();
                UiAuditExempt.Mark(rt, "the store's own lockup, drawn 1:1 at the size it was shipped");
            }
            else
            {
                var title = NewText("Title", _menuPanel, _display, 24, TextAnchor.MiddleCenter, UITheme.Amber[4]);
                Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(MenuColW, 32), new Vector2(0, 186f));
                title.text = "MALIBU CLUB";
                title.gameObject.AddComponent<NeonFlicker>();
            }

            // The studio at the foot, left; the build's number, right.
            var studio = NewText("Studio", _menuPanel, _body, 8, TextAnchor.LowerLeft, UITheme.Cream[2]);
            Place(studio.rectTransform, new Vector2(0, 0), new Vector2(300, 12), new Vector2(12f, 8f));
            studio.rectTransform.pivot = new Vector2(0, 0);
            studio.text = "LASGEN INTERACTIVE";
            var version = NewText("Version", _menuPanel, _body, 8, TextAnchor.LowerRight, UITheme.Cream[2]);
            Place(version.rectTransform, new Vector2(1, 0), new Vector2(200, 12), new Vector2(-12f, 8f));
            version.rectTransform.pivot = new Vector2(1, 0);
            version.text = "v" + Application.version;

            // The title comes up out of the dark rather than snapping on (0.6 s, unscaled) —
            // and instantly under reduced motion, which is that setting's whole promise.
            _menuFade = _menuPanel.gameObject.AddComponent<CanvasGroup>();

            _menuPanel.gameObject.SetActive(false);
        }

        private static Texture2D s_menuVignette;

        /// <summary>A 64x36 soft vignette: clear inside an ellipse, deepening to the palette's night at the
        /// corners. Built once, bilinear (it is light, not a drawing).</summary>
        private static Texture2D MenuVignette()
        {
            if (s_menuVignette != null) return s_menuVignette;
            const int W = 64, H = 36;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var night = UITheme.Night[0];
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f) / W * 2f - 1f, dy = (y + 0.5f) / H * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 1f, d)) * 0.72f;
                    px[y * W + x] = new Color(night.r, night.g, night.b, a);
                }
            t.SetPixels(px);
            t.Apply(false, true);
            return s_menuVignette = t;
        }

        /// <summary>Where drifter <paramref name="i"/> stands at unscaled second
        /// <paramref name="t"/>: a slow diagonal lane wrapped over the field, plus a small
        /// bob — constants from the index, never a die.</summary>
        private static Vector2 DriftAt(int i, float t)
        {
            float laneW = DesignFrame.StageWidth * StageToHud + 160f;
            float laneH = DesignFrame.StageHeight * StageToHud + 160f;
            float speed = 9f + (i * 37 % 11);                    // units a second, per lane
            float x = Mathf.Repeat((i * 331f) % laneW + t * speed, laneW) - laneW * 0.5f;
            float y = Mathf.Repeat((i * 173f) % laneH + t * speed * 0.62f, laneH) - laneH * 0.5f;
            float bob = Mathf.Sin(t * 0.8f + i * 1.7f) * 6f;
            return new Vector2(x, y + bob);
        }

        /// <summary>
        /// The column is rebuilt at every showing: whether CONTINUE stands lit, and what its
        /// note reads, are the save's to say — and the save changes between showings.
        /// </summary>
        private void RebuildMenuColumn()
        {
            if (_menuColumn != null) Destroy(_menuColumn.gameObject);
            _menuKeys.Clear();
            _menuHover = null;
            _menuColumn = NewRect("Column", _menuPanel);
            _menuColumn.anchorMin = _menuColumn.anchorMax = _menuColumn.pivot = new Vector2(0.5f, 0.5f);
            _menuColumn.sizeDelta = new Vector2(MenuColW, 360f);
            _menuColumn.anchoredPosition = new Vector2(0f, -110f);

            // The keys hang from the column's TOP edge (panel +70, just under the logo's foot) and read
            // downward.
            float y = -6f;

            bool saved = SaveStore.TryPeek(out var summary);
            var loadKey = MenuKey("CONTINUE", UIText.T("chrome.pause.continue"), "lock", "continue",
                saved ? MenuPack.Tone.Orange : MenuPack.Tone.Grey, ref y, () =>
                {
                    if (SaveStore.TryLoad(out var snap) && _bootstrap != null && _bootstrap.TryStartSavedRun(snap))
                        HideMainMenu();
                    else
                    {
                        Toast(UIText.T("chrome.menu.stale_save"));
                        RebuildMenuColumn();
                    }
                });
            if (saved)
            {
                var note = NewText("ContinueNote", _menuColumn, _body, 8, TextAnchor.UpperCenter, UITheme.Cream[3]);
                Place(note.rectTransform, new Vector2(0.5f, 1), new Vector2(MenuKeyMinW, 12), new Vector2(0, y + 6f));
                note.rectTransform.pivot = new Vector2(0.5f, 1);
                note.text = UIText.T("chrome.menu.continue_note",
                    ("n", summary.day.ToString()), ("money", summary.money.ToString()));
                Enter(note.rectTransform, true);
                y -= 14f;
            }
            else DimMenuKey(loadKey);

            MenuKey("NEW RUN", UIText.T("chrome.pause.new_run"), "restart", "new_run",
                saved ? MenuPack.Tone.Grey : MenuPack.Tone.Orange, ref y,
                () =>
                {
                    HideMainMenu();
                    _bootstrap?.StartFreshRun(SeedPolicy.Next());
                });
            MenuKey("SETTINGS", UIText.T("chrome.pause.settings"), "cog", "settings", MenuPack.Tone.Grey, ref y,
                () => OpenSettingsFromMenu(null));
            MenuKey("QUIT", UIText.T("chrome.pause.quit"), "exit", "quit", MenuPack.Tone.Grey, ref y,
                Application.Quit);

            // a neon rule between the doors and the small row
            y -= 2f;
            var ruleHost = NewRect("Rule", _menuColumn);
            Place(ruleHost, new Vector2(0.5f, 1), new Vector2(MenuKeyMinW - 40f, 10f), new Vector2(0f, y));
            ruleHost.pivot = new Vector2(0.5f, 1);
            SunsetRules(ruleHost, 0f, MenuKeyMinW - 40f);
            Enter(ruleHost, true);
            y -= 18f;

            // The small row: straight to the two pages the author named, the credits, and the store
            // once it has an address to send anyone to.
            float rowH = 36f;
            var small = new List<RectTransform>
            {
                SmallMenuKey("AUDIO", UIText.T("chrome.settings.audio"), "sound_on", "audio",
                    () => OpenSettingsFromMenu("AUDIO"), rowH),
                SmallMenuKey("LANGUAGE", UIText.T("chrome.settings.language"), "mail", "language",
                    () => OpenSettingsFromMenu("LANGUAGE"), rowH),
                SmallMenuKey("CREDITS", UIText.T("chrome.menu.credits"), "info", "credits", OpenCredits, rowH),
            };
            if (StoreLink.HasPage)
                small.Add(SmallMenuKey("STEAM", "STEAM", "heart_line", "steam", StoreLink.Open, rowH));
            float total = -8f;
            foreach (var key in small) total += key.sizeDelta.x + 8f;
            float x = -total * 0.5f;
            foreach (var key in small)
            {
                key.anchorMin = key.anchorMax = new Vector2(0.5f, 1f);
                key.pivot = new Vector2(0, 1f);
                key.anchoredPosition = new Vector2(x, y);
                x += key.sizeDelta.x + 8f;
                Enter(key, true);
            }

            // the pointer: a neon arrow beside the key under the mouse
            var art = ItemArt.Load("ib_m_pointer");
            if (art != null)
            {
                _menuPointer = NewRect("Pointer", _menuColumn);
                _menuPointer.anchorMin = _menuPointer.anchorMax = new Vector2(0.5f, 1f);
                _menuPointer.pivot = new Vector2(1f, 0.5f);
                _menuPointer.sizeDelta = new Vector2(32f, 32f);
                var pi = _menuPointer.gameObject.AddComponent<Image>();
                pi.sprite = art;
                pi.raycastTarget = false;
                pi.color = UITheme.Magenta[4];
                _menuPointer.gameObject.SetActive(false);
            }
        }

        /// <summary>A key of the menu's column: the pack's worded key, fitted to its word, wearing the ESC
        /// family's surface and the 1-bit mark <c>ib_m_&lt;icon&gt;</c> at exactly 2x.</summary>
        private RectTransform MenuKey(string id, string label, string glyph, string icon, MenuPack.Tone tone,
            ref float y, Action onClick)
        {
            var key = PackWordKey(_menuColumn, id, label, glyph, tone, new Vector2(0.5f, 1),
                new Vector2(MenuKeyMinW, MenuKeyH), new Vector2(0, y), onClick, MenuKeyMinW, 48f + 24f);
            SurfaceKey(key, tone);
            OneBitGlyph(key, icon);
            Enter(key, false);
            y -= MenuKeyH + 10f;
            return key;
        }

        /// <summary>One of the small row's keys, fitted to its word and NOT yet placed —
        /// the row centres itself once it knows all its widths.</summary>
        private RectTransform SmallMenuKey(string id, string label, string glyph, string icon, Action onClick, float h)
        {
            var key = PackWordKey(_menuColumn, id, label, glyph, MenuPack.Tone.Grey, new Vector2(0.5f, 1),
                new Vector2(120f, h), new Vector2(0, 0), onClick, 96f, 44f + 16f);
            SurfaceKey(key, MenuPack.Tone.Grey);
            OneBitGlyph(key, icon);
            return key;
        }

        /// <summary>The key's glyph slot takes the 1-bit mark (ink baked in: Cream[4] fill, Night[0] ring),
        /// and the key's hover no longer re-inks it. A missing file leaves the pack's own glyph.</summary>
        private static void OneBitGlyph(RectTransform key, string icon)
        {
            var art = ItemArt.Load("ib_m_" + icon);
            var glyph = key.Find("Face/Glyph")?.GetComponent<Image>();
            if (art == null || glyph == null) return;
            glyph.sprite = art;
            glyph.preserveAspect = true;
            glyph.color = Color.white;
            var pk = key.GetComponent<PackKey>();
            if (pk != null) { pk.GlyphRest = Color.white; pk.GlyphLit = Color.white; }
        }

        /// <summary>Registers a piece of the column for the staggered rise, and — for a key that takes
        /// clicks — for the pointer.</summary>
        private void Enter(RectTransform rt, bool quiet)
        {
            var fade = rt.GetComponent<CanvasGroup>();
            if (fade == null) fade = rt.gameObject.AddComponent<CanvasGroup>();   // never ?? on a Unity object
            var entry = new MenuKeyEntry { Key = rt, Home = rt.anchoredPosition, Fade = fade, Live = !quiet };
            _menuKeys.Add(entry);
            if (quiet) return;
            var relay = rt.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () => { var b = rt.GetComponent<Button>(); if (b != null && b.interactable) _menuHover = rt; };
            relay.Exited = () => { if (_menuHover == rt) _menuHover = null; };
        }

        /// <summary>The pause menu's SOON dress without the tag: the door is there, the bar
        /// just has nothing behind it yet.</summary>
        private void DimMenuKey(RectTransform key)
        {
            key.GetComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f, 1f);
            key.GetComponent<Button>().interactable = false;
            var sink = key.GetComponent<PressSink>();
            if (sink != null) sink.enabled = false;
            var pk = key.GetComponent<PackKey>();
            if (pk != null) pk.enabled = false;
            var face = (RectTransform)key.Find("Face");
            var label = face != null ? face.Find("Label")?.GetComponent<Text>() : null;
            if (label != null) label.color = UITheme.Cream[2];
            var glyphImg = face != null ? face.Find("Glyph")?.GetComponent<Image>() : null;
            if (glyphImg != null)
            {
                bool baked = glyphImg.sprite != null && glyphImg.sprite.name.StartsWith("ib_");
                glyphImg.color = baked || MenuPack.IsIcon(glyphImg.sprite) ? new Color(0.55f, 0.55f, 0.55f, 1f) : UITheme.Cream[2];
            }
        }

        /// <summary>The settings over the menu, landing on <paramref name="page"/> when one
        /// is named (the small row's AUDIO and LANGUAGE); BACK returns to the menu.</summary>
        private void OpenSettingsFromMenu(string page)
        {
            _settingsFromMenu = true;
            _menuPanel.gameObject.SetActive(false);   // the hold stays: SetPaused is the menu's until a key lets go
            ToggleSettings();
            if (page != null) ShowSettingsPage(page);
        }

        /// <summary>The one offer, at the cold boot's first run — never after a language
        /// reload, never after START OVER, never on the runs the menu itself starts.</summary>
        private void OfferMainMenuAtBoot()
        {
            if (_menuOffered) return;
            _menuOffered = true;
            if (_bootstrap == null || _bootstrap.ResumedAcrossReload) return;
            ShowMainMenu();
        }

        private void ShowMainMenu()
        {
            if (_menuPanel == null) return;
            CloseId();
            RebuildMenuColumn();
            if (_creditsPanel != null) _creditsPanel.gameObject.SetActive(false);
            _menuPanel.gameObject.SetActive(true);
            SetPaused(true);
            _menuFadeT = Motion.Reduced ? 1f : 0f;
            if (_menuFade != null) _menuFade.alpha = Motion.Reduced ? 1f : 0f;
            // The drift and the entrance start from the SAME arranged frame at every showing (and stay
            // parked on the finished frame under reduced motion — deterministically, so the look test
            // can photograph it).
            _menuShownAt = Time.unscaledTime;
            for (int i = 0; i < _menuDrifters.Count; i++)
                if (_menuDrifters[i] != null) _menuDrifters[i].anchoredPosition = DriftAt(i, 0f);
            StepMenuEntrance(Motion.Reduced ? float.MaxValue : 0f);
        }

        /// <summary>The title's fade, its drift, the keys' rise and the pointer, on the unscaled clock —
        /// the menu holds the game's own. Reduced motion parks them all on the finished frame.</summary>
        private void StepMenuFade()
        {
            if (!MenuUp) return;
            if (_menuFade != null && _menuFadeT < 1f)
            {
                _menuFadeT = Motion.Reduced ? 1f : Mathf.Min(1f, _menuFadeT + Time.unscaledDeltaTime / 0.6f);
                _menuFade.alpha = _menuFadeT;
            }
            float t = Motion.Reduced ? float.MaxValue : Time.unscaledTime - _menuShownAt;
            StepMenuEntrance(t);
            StepMenuPointer();
            if (Motion.Reduced) return;             // parked on the showing's own scatter
            for (int i = 0; i < _menuDrifters.Count; i++)
                if (_menuDrifters[i] != null) _menuDrifters[i].anchoredPosition = DriftAt(i, t);
        }

        /// <summary>Each piece of the column rises <see cref="EnterRise"/> into its home and fades up, one
        /// after the other.</summary>
        private void StepMenuEntrance(float t)
        {
            for (int i = 0; i < _menuKeys.Count; i++)
            {
                var e = _menuKeys[i];
                if (e.Key == null) continue;
                float k = Mathf.Clamp01((t - EnterDelay - i * EnterStagger) / EnterTime);
                float ease = 1f - (1f - k) * (1f - k);
                e.Key.anchoredPosition = e.Home + new Vector2(0f, -(1f - ease) * EnterRise);
                if (e.Fade != null) e.Fade.alpha = ease;
            }
        }

        /// <summary>The neon arrow stands at the left of the hovered key and leans toward it.</summary>
        private void StepMenuPointer()
        {
            if (_menuPointer == null) return;
            bool show = _menuHover != null && _menuHover.gameObject.activeInHierarchy;
            if (_menuPointer.gameObject.activeSelf != show) _menuPointer.gameObject.SetActive(show);
            if (!show) return;
            float lean = Motion.Reduced ? 0f : Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f) * 2f);
            // the key's left edge, in the column's own frame (keys hang from its top edge)
            var home = _menuHover.anchoredPosition;
            float left = home.x - _menuHover.pivot.x * _menuHover.sizeDelta.x;
            float midY = home.y + (0.5f - _menuHover.pivot.y) * _menuHover.sizeDelta.y;
            _menuPointer.anchoredPosition = new Vector2(left - 6f + lean, midY);
        }

        private void HideMainMenu()
        {
            if (_menuPanel == null) return;
            if (_creditsPanel != null) _creditsPanel.gameObject.SetActive(false);
            _menuPanel.gameObject.SetActive(false);
            SetPaused(false);
        }
    }
}
