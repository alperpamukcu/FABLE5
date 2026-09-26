using System;
using UnityEngine;
using UnityEngine.UI;
using LastCall.Game;

namespace LastCall.UI
{
    /// <summary>
    /// THE FRONT DOOR (2026-09-26, the author: "oyuna bir ana menü yapılsın"). The game used
    /// to open MID-NIGHT: GameBootstrap dealt a run and the first stool was clickable before
    /// the player had said a word. Now a cold boot opens on the menu — the live room behind
    /// the pause family's scrim, the marquee wearing the game's name (NAME_CLEARANCE.md,
    /// 2026-09-11: "Malibu Club: Cocktail Bar Simulator"), and the pack's keys under it:
    /// CONTINUE (only when SaveStore holds a dawn worth continuing — its note says which
    /// night and how much is in the till), NEW RUN (a fresh seed from SeedPolicy — every
    /// player used to get the inspector's one run), SETTINGS over the menu, QUIT.
    ///
    /// It appears ONCE per boot and never after a language reload (ReloadKeepingRun hands
    /// the run across; GameBootstrap.ResumedAcrossReload says so), never after START OVER,
    /// and never over a running night except through the pause menu's own MAIN MENU key.
    /// While it is up the night is held exactly the way the pause holds it, and Escape does
    /// nothing — the menu's own keys are its only doors.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private RectTransform _menuPanel;
        private RectTransform _menuColumn;
        private bool _menuOffered;          // one offer per HUD life: the cold boot's
        private bool _settingsFromMenu;     // the window came from the menu, so BACK returns there

        private const float MenuKeyH = 50f, MenuKeyMinW = 352f, MenuColW = 440f;

        /// <summary>The menu is up (the settings opened from it count — the night stays held).</summary>
        private bool MenuUp => _menuPanel != null && _menuPanel.gameObject.activeSelf;

        private void BuildMainMenu(RectTransform root)
        {
            _menuPanel = NewRect("MainMenu", root);
            var canvas = _menuPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 31;      // over the curtain (30): the front door covers the whole bar
            _menuPanel.gameObject.AddComponent<ForgivingRaycaster>();
            Stretch(_menuPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // The room stays, darkened — the pause family's scrim, catching every click.
            var dim = NewRect("Dim", _menuPanel);
            Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = MenuScrim;
            dimImg.raycastTarget = true;

            // The build's number, small and out of the way — the one place the game says
            // which build it is without a dev panel.
            var version = NewText("Version", _menuPanel, _body, 8, TextAnchor.LowerRight, UITheme.Cream[2]);
            Place(version.rectTransform, new Vector2(1, 0), new Vector2(200, 12), new Vector2(-12f, 8f));
            version.rectTransform.pivot = new Vector2(1, 0);
            version.text = "v" + Application.version;

            _menuPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// The column is rebuilt at every showing: whether CONTINUE stands, and what its
        /// note reads, are the save's to say — and the save changes between showings.
        /// </summary>
        private void RebuildMenuColumn()
        {
            if (_menuColumn != null) Destroy(_menuColumn.gameObject);
            _menuColumn = NewRect("Column", _menuPanel);
            _menuColumn.anchorMin = _menuColumn.anchorMax = _menuColumn.pivot = new Vector2(0.5f, 0.5f);
            _menuColumn.sizeDelta = new Vector2(MenuColW, 480f);
            _menuColumn.anchoredPosition = new Vector2(0f, 10f);

            // The marquee, wearing the game's name. A proper noun, the same in every
            // language — like the bill head — so it is a hard string, not a table line.
            float y = -8f;
            var header = MenuPack.Art("menu_header");
            if (header != null)
            {
                var win = new Vector2(header.rect.width * 2f + 6f, header.rect.height * 2f + 6f);
                var window = NightPlate(_menuColumn, "Header", win, 0f, header);
                window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 1f);
                window.anchoredPosition = new Vector2(0f, y);
                NightTitle(_menuColumn, "MALIBU CLUB", y - win.y * 0.5f + 16f);
                var title = _menuColumn.Find("Title")?.GetComponent<Text>();
                var shadow = _menuColumn.Find("TitleShadow")?.GetComponent<Text>();
                if (title != null && title.preferredWidth > 220f)   // the board's calm middle
                {
                    title.fontSize = LanguageFonts.Size(title.font, 16);
                    if (shadow != null) shadow.fontSize = title.fontSize;
                }
                if (title != null) title.gameObject.AddComponent<NeonFlicker>();
                SunsetRules(_menuColumn, y - win.y - 8f, MenuColW - 60f);
                y -= win.y + 34f;
            }
            else
            {
                NightTitle(_menuColumn, "MALIBU CLUB", y - 8f);
                var title = _menuColumn.Find("Title")?.GetComponent<Text>();
                if (title != null) title.gameObject.AddComponent<NeonFlicker>();
                SunsetRules(_menuColumn, y - 48f, MenuColW - 60f);
                y -= 74f;
            }

            bool saved = SaveStore.TryPeek(out var summary);
            if (saved)
            {
                MenuKey("CONTINUE", UIText.T("chrome.pause.continue"), "lock", MenuPack.Tone.Orange, ref y, () =>
                {
                    if (SaveStore.TryLoad(out var snap) && _bootstrap != null && _bootstrap.TryStartSavedRun(snap))
                        HideMainMenu();
                    else
                    {
                        // A save the data no longer honours: said once, and the key goes
                        // grey rather than vanishing — the player is owed the why.
                        Toast(UIText.T("chrome.menu.stale_save"));
                        RebuildMenuColumn();
                    }
                });
                var note = NewText("ContinueNote", _menuColumn, _body, 8, TextAnchor.UpperCenter, UITheme.Cream[3]);
                Place(note.rectTransform, new Vector2(0.5f, 1), new Vector2(MenuKeyMinW, 12), new Vector2(0, y + 6f));
                note.rectTransform.pivot = new Vector2(0.5f, 1);
                note.text = UIText.T("chrome.menu.continue_note",
                    ("n", summary.day.ToString()), ("money", summary.money.ToString()));
                y -= 14f;
            }

            MenuKey("NEW RUN", UIText.T("chrome.pause.new_run"), "restart",
                saved ? MenuPack.Tone.Grey : MenuPack.Tone.Orange, ref y,
                () =>
                {
                    HideMainMenu();
                    _bootstrap?.StartFreshRun(SeedPolicy.Next());
                });
            MenuKey("SETTINGS", UIText.T("chrome.pause.settings"), "cog", MenuPack.Tone.Grey, ref y, () =>
            {
                _settingsFromMenu = true;
                _menuPanel.gameObject.SetActive(false);   // the hold stays: SetPaused is the menu's until a key lets go
                ToggleSettings();
            });
            MenuKey("QUIT", UIText.T("chrome.pause.quit"), "exit", MenuPack.Tone.Grey, ref y,
                Application.Quit);
        }

        /// <summary>A key of the menu's column: the pack's worded key with its brass icon,
        /// fitted to its word, wearing the ESC family's surface.</summary>
        private void MenuKey(string id, string label, string glyph, MenuPack.Tone tone, ref float y, Action onClick)
        {
            var key = PackWordKey(_menuColumn, id, label, glyph, tone, new Vector2(0.5f, 1),
                new Vector2(MenuKeyMinW, MenuKeyH), new Vector2(0, y), onClick, MenuKeyMinW, 48f + 24f);
            SurfaceKey(key, tone);
            y -= MenuKeyH + 10f;
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
        }

        private void HideMainMenu()
        {
            if (_menuPanel == null) return;
            _menuPanel.gameObject.SetActive(false);
            SetPaused(false);
        }
    }
}
