using System;
using UnityEngine;
using UnityEngine.UI;
using LastCall.Game;

namespace LastCall.UI
{
    /// <summary>
    /// THE FRONT DOOR (2026-09-26; repainted 2026-09-27 on the author's second look: "Ana
    /// menüyü sevmedim kalitesi düşük düz arkaplan ve hareketli iconlar olabilir ... oyunda
    /// kullandığımız logo giriş sayfasında olmalı"). A cold boot opens on a FLAT plum field —
    /// no painted scene, no scrim over the room — with the store's own neon lockup
    /// (menu_logo, the Malibu Club wordmark over its teal subtitle) standing where a marquee
    /// used to, and the game's empty glassware drifting slowly behind the keys: quiet motion,
    /// the way a title screen breathes, and perfectly still under REDUCED MOTION.
    ///
    /// The keys: CONTINUE (always on the board — greyed while no save stands, its note naming
    /// the night and the till when one does), NEW RUN (a fresh SeedPolicy seed), SETTINGS,
    /// QUIT — and a small row under them straight to the AUDIO and LANGUAGE pages plus the
    /// STEAM page (which stands only once StoreLink carries an address; the app does not
    /// exist yet). The menu appears ONCE per boot, never after a language reload
    /// (GameBootstrap.ResumedAcrossReload), never after START OVER, and only through the
    /// pause menu's MAIN MENU key after that. While it is up the night is held exactly the
    /// way the pause holds it, and Escape does nothing — the menu's own keys are its doors.
    /// It plays its own record (the "menu" mood, music_menu_1) and fades up from black.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private RectTransform _menuPanel;
        private RectTransform _menuColumn;
        private CanvasGroup _menuFade;
        private float _menuFadeT = 1f;
        private float _menuShownAt;         // the drift's own zero: every showing starts from the same scatter
        private bool _menuOffered;          // one offer per HUD life: the cold boot's
        private bool _settingsFromMenu;     // the window came from the menu, so BACK returns there

        private const float MenuKeyH = 50f, MenuKeyMinW = 352f, MenuColW = 440f;

        /// <summary>The flat field the title stands on — the palette's deep plum, the same
        /// family the menus' pictures were snapped to.</summary>
        private static Color MenuField => UITheme.Night[1];

        /// <summary>The menu is up (the settings opened from it count — the night stays held).</summary>
        private bool MenuUp => _menuPanel != null && _menuPanel.gameObject.activeSelf;

        // ── the drift: the game's empty glasses, floating (2026-09-27) ──────────────────
        // Cosmetic and CLOCKLESS in the rules' sense: positions are arithmetic over the
        // unscaled time from per-sprite constants — no RNG stream is touched, so the menu
        // can never move a seed. Reduced motion parks them where they stand.

        private readonly System.Collections.Generic.List<RectTransform> _menuDrifters =
            new System.Collections.Generic.List<RectTransform>();
        private static readonly string[] DriftArt =
            { "glass3d_martini", "glass3d_rocks", "glass3d_highball", "glass3d_coupe", "glass3d_pint" };
        private const int DriftCount = 10;
        private const float DriftAlpha = 0.13f;

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

            // THE LOGO IS THE TITLE (2026-09-27): the store's own lockup — the neon script
            // and its teal subtitle — shipped into Resources/Menu at exactly the size it is
            // drawn, so the point filter never resamples it.
            var logo = MenuPack.Art("menu_logo");
            if (logo != null)
            {
                var rt = NewRect("Logo", _menuPanel);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = logo.rect.size;
                rt.anchoredPosition = new Vector2(0f, 182f);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = logo;
                img.preserveAspect = true;
                img.raycastTarget = false;
                UiAuditExempt.Mark(rt, "the store's own lockup, drawn 1:1 at the size it was shipped");
            }
            else
            {
                var title = NewText("Title", _menuPanel, _display, 24, TextAnchor.MiddleCenter, UITheme.Amber[4]);
                Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(MenuColW, 32), new Vector2(0, 182f));
                title.text = "MALIBU CLUB";
                title.gameObject.AddComponent<NeonFlicker>();
            }

            // The build's number, small and out of the way.
            var version = NewText("Version", _menuPanel, _body, 8, TextAnchor.LowerRight, UITheme.Cream[2]);
            Place(version.rectTransform, new Vector2(1, 0), new Vector2(200, 12), new Vector2(-12f, 8f));
            version.rectTransform.pivot = new Vector2(1, 0);
            version.text = "v" + Application.version;

            // The title comes up out of the dark rather than snapping on (0.6 s, unscaled) —
            // and instantly under reduced motion, which is that setting's whole promise.
            _menuFade = _menuPanel.gameObject.AddComponent<CanvasGroup>();

            _menuPanel.gameObject.SetActive(false);
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
            _menuColumn = NewRect("Column", _menuPanel);
            _menuColumn.anchorMin = _menuColumn.anchorMax = _menuColumn.pivot = new Vector2(0.5f, 0.5f);
            _menuColumn.sizeDelta = new Vector2(MenuColW, 360f);
            _menuColumn.anchoredPosition = new Vector2(0f, -110f);

            // The keys hang from the column's TOP edge (panel +70, just under the logo's
            // foot at +77) and read downward — the first cut started them at +180 and stood
            // CONTINUE across the wordmark (r247's screenshot).
            float y = -6f;

            // CONTINUE is always on the board (2026-09-27, the author: "New Run'un yanına
            // load"): lit over the save's own note when one stands, greyed when the bar has
            // nothing to come back to — a door the player can see exists either way.
            bool saved = SaveStore.TryPeek(out var summary);
            var loadKey = MenuKey("CONTINUE", UIText.T("chrome.pause.continue"), "lock",
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
                y -= 14f;
            }
            else DimMenuKey(loadKey);

            MenuKey("NEW RUN", UIText.T("chrome.pause.new_run"), "restart",
                saved ? MenuPack.Tone.Grey : MenuPack.Tone.Orange, ref y,
                () =>
                {
                    HideMainMenu();
                    _bootstrap?.StartFreshRun(SeedPolicy.Next());
                });
            MenuKey("SETTINGS", UIText.T("chrome.pause.settings"), "cog", MenuPack.Tone.Grey, ref y,
                () => OpenSettingsFromMenu(null));
            MenuKey("QUIT", UIText.T("chrome.pause.quit"), "exit", MenuPack.Tone.Grey, ref y,
                Application.Quit);

            // The small row (2026-09-27): straight to the two pages the author named, and to
            // the store once it has an address to send anyone to.
            y -= 6f;
            float rowH = 36f;
            var small = new System.Collections.Generic.List<RectTransform>
            {
                SmallMenuKey("AUDIO", UIText.T("chrome.settings.audio"), "sound_on",
                    () => OpenSettingsFromMenu("AUDIO"), rowH),
                SmallMenuKey("LANGUAGE", UIText.T("chrome.settings.language"), "mail",
                    () => OpenSettingsFromMenu("LANGUAGE"), rowH),
            };
            if (StoreLink.HasPage)
                small.Add(SmallMenuKey("STEAM", "STEAM", "heart_line", StoreLink.Open, rowH));
            float total = -8f;
            foreach (var key in small) total += key.sizeDelta.x + 8f;
            float x = -total * 0.5f;
            foreach (var key in small)
            {
                key.anchorMin = key.anchorMax = new Vector2(0.5f, 1f);
                key.pivot = new Vector2(0, 1f);
                key.anchoredPosition = new Vector2(x, y);
                x += key.sizeDelta.x + 8f;
            }
        }

        /// <summary>A key of the menu's column: the pack's worded key with its brass icon,
        /// fitted to its word, wearing the ESC family's surface.</summary>
        private RectTransform MenuKey(string id, string label, string glyph, MenuPack.Tone tone, ref float y, Action onClick)
        {
            var key = PackWordKey(_menuColumn, id, label, glyph, tone, new Vector2(0.5f, 1),
                new Vector2(MenuKeyMinW, MenuKeyH), new Vector2(0, y), onClick, MenuKeyMinW, 48f + 24f);
            SurfaceKey(key, tone);
            y -= MenuKeyH + 10f;
            return key;
        }

        /// <summary>One of the small row's keys, fitted to its word and NOT yet placed —
        /// the row centres itself once it knows all its widths.</summary>
        private RectTransform SmallMenuKey(string id, string label, string glyph, Action onClick, float h)
        {
            var key = PackWordKey(_menuColumn, id, label, glyph, MenuPack.Tone.Grey, new Vector2(0.5f, 1),
                new Vector2(120f, h), new Vector2(0, 0), onClick, 96f, 44f + 16f);
            SurfaceKey(key, MenuPack.Tone.Grey);
            return key;
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
                glyphImg.color = MenuPack.IsIcon(glyphImg.sprite) ? new Color(0.55f, 0.55f, 0.55f, 1f) : UITheme.Cream[2];
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
            _menuPanel.gameObject.SetActive(true);
            SetPaused(true);
            _menuFadeT = Motion.Reduced ? 1f : 0f;
            if (_menuFade != null) _menuFade.alpha = Motion.Reduced ? 1f : 0f;
            // The drift starts from the SAME arranged scatter at every showing (and stays
            // parked there under reduced motion — deterministically, so the look test can
            // photograph it; wherever-the-clock-was parking never matched twice).
            _menuShownAt = Time.unscaledTime;
            for (int i = 0; i < _menuDrifters.Count; i++)
                if (_menuDrifters[i] != null) _menuDrifters[i].anchoredPosition = DriftAt(i, 0f);
        }

        /// <summary>The title's fade and its drift, on the unscaled clock — the menu holds
        /// the game's own. Reduced motion parks both.</summary>
        private void StepMenuFade()
        {
            if (!MenuUp) return;
            if (_menuFade != null && _menuFadeT < 1f)
            {
                _menuFadeT = Motion.Reduced ? 1f : Mathf.Min(1f, _menuFadeT + Time.unscaledDeltaTime / 0.6f);
                _menuFade.alpha = _menuFadeT;
            }
            if (Motion.Reduced) return;             // parked on the showing's own scatter
            float t = Time.unscaledTime - _menuShownAt;
            for (int i = 0; i < _menuDrifters.Count; i++)
                if (_menuDrifters[i] != null) _menuDrifters[i].anchoredPosition = DriftAt(i, t);
        }

        private void HideMainMenu()
        {
            if (_menuPanel == null) return;
            _menuPanel.gameObject.SetActive(false);
            SetPaused(false);
        }
    }
}
