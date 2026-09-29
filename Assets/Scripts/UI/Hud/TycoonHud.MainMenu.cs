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
    /// A cold boot opens on a FLAT plum field under a vignette of four flat bands, the bar's own bottles
    /// drifting full, upright and a whole texel at a time behind everything readable, and the store's lockup
    /// as the title - since 2026-09-29 a lit NEON SIGN drawn at the size it is shown (TitleSign: "ışık hareketi
    /// olmalı ... upscale yapma"), whose letters strike on and whose beads of light then run along its tubes.
    /// The keys are SIGN KEYS (2026-09-29, TycoonHud.MenuKit - the menus rebuilt in the week's language) wearing
    /// NEON MARKS drawn in code (NeonIcons - the 1-bit ib_m_* marks were "kalitesiz"): CONTINUE (always on the
    /// board - dark glass while no save stands, the one amber key and a note naming the night and the till when
    /// one does), NEW RUN (a fresh SeedPolicy seed; the amber key when there is no save), SETTINGS, QUIT; under
    /// them a Graphite ledge with the cellar's deco fan on it, and the small row - AUDIO and LANGUAGE straight to
    /// their pages, ACHIEVEMENTS, CREDITS, and STEAM once StoreLink carries an address - its marks in the second
    /// line's cyan. The keys rise into place one after another as the title fades up; each key's mark rests
    /// half-lit and strikes lit under the pointer inside a ClubBlue tube, and a neon arrow in the same blue stands
    /// beside whichever key the mouse is over. Everything that moves is arithmetic on the unscaled clock (no RNG
    /// stream is ever touched) and holds still under REDUCED MOTION; the sign's idle light also stops with FLASHES off.
    ///
    /// IT FITS A WIDE WINDOW (2026-09-29, the critic): a 21:9 window crops the field to its rows 90..630
    /// (DesignFrame), so the sign's centre is +168 and the column's pitch 54, and with or without a save every door
    /// stands inside those rows (the small row's foot at 612, or 628 under a save's note).
    ///
    /// The menu appears ONCE per boot, never after a language reload (GameBootstrap.ResumedAcrossReload) unless the
    /// reload was asked for FROM the door (its settings' LANGUAGE → APPLY, which used to drop the player into a bar
    /// they never chose - read_menus finding 2), never after START OVER, and only through the pause menu's MAIN MENU
    /// key after that. While it is up - its settings, credits and achievements included - the night is held exactly
    /// the way the pause holds it. Escape does nothing on the door itself (its own keys are its doors) and goes back
    /// from anything opened over it. It plays its own record (the "menu" mood, music_menu_1).
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

        private const float MenuColW = 440f;

        // THE DOOR'S RECTS, in field units (2026-09-29; BUILD_SPEC §3 "Door", re-laid for 21:9 by the critic): the
        // column's keys 352 wide centred on 640, the first one's top at 304 - eight under the sign's glow - at a pitch
        // of 54 (a save's note under CONTINUE adds 16); the ledge 28 under the last key, 432 wide; the small row 22
        // under the ledge's top, its keys 12 apart.
        private const float DoorKeyMinW = 352f, DoorColTop = 304f, DoorPitch = 54f, DoorNoteH = 16f;
        private const float DoorLedgeGap = 28f, DoorLedgeW = 432f, DoorSmallGap = 22f, DoorSmallSpace = 12f, DoorSmallMinW = 120f;

        /// <summary>
        /// The title sign's centre over the field's (2026-09-29): +168, not the old lockup's +186. A 21:9 window
        /// crops the field to its middle rows (DesignFrame: y 90..630 of 720 at 2560x1080), and at +186 the tops of
        /// the M and the C were cut there; at +168 the sign's whole glow starts on row 90.
        /// </summary>
        private const float TitleSignY = 168f;

        private TitleSign _menuSign;

        /// <summary>The flat field the title stands on — the palette's deep plum.</summary>
        private static Color MenuField => UITheme.Night[1];

        /// <summary>
        /// The menu is up - and so are the settings opened from it (2026-09-29, read_menus finding 4). They hide the
        /// door's panel (its full-field catcher is on canvas 31 and would cover the settings' 29 and swallow their
        /// clicks - the critic), so the panel alone said "no menu" while they stood over it: Escape ran the bar's order,
        /// the record went back to the bar's mood, the presence said "Night" and the hostess's gates opened.
        /// </summary>
        private bool MenuUp => _menuPanel != null && (_menuPanel.gameObject.activeSelf || _settingsFromMenu);

        // ── the drift: the bar's own bottles, floating (2026-09-27; bottles 2026-09-28) ──────

        private RectTransform _menuDrift;
        private readonly List<RectTransform> _menuDrifters = new List<RectTransform>();
        private readonly List<Image> _menuDriftImages = new List<Image>();
        private readonly List<int> _menuDriftLap = new List<int>();
        private readonly List<int> _menuDriftSlot = new List<int>();
        private readonly List<Sprite> _menuDriftArt = new List<Sprite>();
        private readonly List<float> _menuDriftSlots = new List<float>();   // the side strips' columns, |x|
        private int _menuDriftNext;                                           // the dealer's place on the shelf
        private const int DriftCount = 10;
        /// <summary>The UI blends in linear space, so an alpha reads far stronger than its number: 0.45 shows a
        /// bottle about two-thirds on over the field's plum (measured 2026-09-28).</summary>
        private const float DriftAlpha = 0.45f;
        /// <summary>How far past the top and the bottom a lane runs, so every wrap is made off the field.</summary>
        private const float DriftMargin = 110f;

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

            // The drifting bottles, between the field and everything readable (filled at the first showing,
            // when the run's catalogue is in hand: FillMenuDrift).
            _menuDrift = NewRect("Drift", _menuPanel);
            Stretch(_menuDrift, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // THE VIGNETTE (2026-09-27; banded 2026-09-29): the field darkens toward its edges, so the drifting bottles
            // fall away into the dark and the title and the keys stand forward. It was one soft bilinear texture - "the
            // one smooth thing on the screen", and the one thing breaking the house's rule that light is laid in flat
            // bands (LampGlow, ClosedNeon). Now four frames of the palette's own night at .15, 20, 48, 88 and 136 units in
            // from the field's edge, each over the ones outside it: four flat steps down to the edge, on the even grid.
            var vig = NewRect("Vignette", _menuPanel);
            Stretch(vig, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            foreach (float reach in VignetteReach) VignetteRing(vig, reach);

            // THE LOGO IS THE TITLE, AND IT IS A SIGN (2026-09-29, the author: "Giriş ekranındaki malibu club logosu
            // ışık hareketi olmalı ve görselin kullanıldığı yere uygun boyda üretilmesi gerekiyor upscale yapma"). The
            // store's lockup was one 560x210 picture LANCZOS-shrunk from the 2000-px capsule - 16k colours, a smooth
            // glow, and a texel on a pixel only at 1280x720. The sign is drawn once per screen scale from the same
            // masters and lit in code (TitleSign); menu_logo.png stays in Resources/Menu as the store's art only.
            // Without the maps the old worded title stands.
            if (TitleSign.HasArt)
                _menuSign = TitleSign.Build(_menuPanel, "Logo", new Vector2(0f, TitleSignY));
            else
            {
                var title = NewText("Title", _menuPanel, _display, 24, TextAnchor.MiddleCenter, UITheme.Amber[4]);
                Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(MenuColW, 32), new Vector2(0, TitleSignY));
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

        /// <summary>The vignette's four frames, in units in from the field's edge (BUILD_SPEC §3 "Door").</summary>
        private static readonly float[] VignetteReach = { 20f, 48f, 88f, 136f };

        /// <summary>One frame of the vignette: the field's night at .15 on the four strips within
        /// <paramref name="reach"/> of the edge (the top and bottom across, the sides between them - never twice over a
        /// corner).</summary>
        private static void VignetteRing(RectTransform vig, float reach)
        {
            var ring = NewRect("R" + reach, vig);
            Stretch(ring, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var night = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.15f);
            const float W = 1280f, H = 720f;
            FieldFill(ring, "T", 0f, 0f, W, reach, night);
            FieldFill(ring, "B", 0f, H - reach, W, reach, night);
            FieldFill(ring, "L", 0f, reach, reach, H - 2f * reach, night);
            FieldFill(ring, "R", W - reach, reach, reach, H - 2f * reach, night);
        }

        /// <summary>
        /// THE BAR'S OWN BOTTLES (2026-09-28, the author: "Başlangıç menüsünde artık arkada uçan bardaklar değil
        /// yeni alkol şişelerimiz gezsin"): every alcoholic bottle the catalogue carries — beer and the zero-proof
        /// mixers, syrups and juices left out — full, as the recipe book draws it (ItemArt.BottleFull: the cellar's
        /// plates, so a redrawn bottle drifts here the day it ships).
        ///
        /// They RISE up the two side strips the logo and the keys leave open, five on each side — the reading
        /// block is the middle of the screen from the lockup down to the small row, and a bottle behind the keys
        /// only made them harder to read. Every time a bottle leaves over the top, it comes back in under the
        /// bottom as another bottle, in another column and at another lean: the dealer hands out the next bottle
        /// on the shelf that is not already on screen, and the column is one the bottle above it is not using.
        /// A bottle enters every five seconds or so, and the whole shelf passes in about two and a half minutes.
        /// </summary>
        private void FillMenuDrift()
        {
            if (_menuDrift == null || _menuDriftArt.Count > 0) return;
            var run = Run;
            if (run == null) return;
            // The well the bar opens with stands on the shelf, every other brand in the market's catalogue.
            var seen = new HashSet<string>();
            void Take(LastCall.Core.IngredientCard c)
            {
                if (c?.Info == null || c.Type == LastCall.Core.IngredientType.Beer || c.Info.Abv <= 0) return;
                if (!seen.Add(c.Id)) return;
                var art = ItemArt.BottleFull(c);
                if (art != null) _menuDriftArt.Add(art);
            }
            if (run.Shelf != null) foreach (var b in run.Shelf.Bottles) Take(b.Ingredient);
            if (run.CatalogueBottles != null) foreach (var c in run.CatalogueBottles) Take(c);
            if (_menuDriftArt.Count == 0) return;
            for (int i = 0; i < DriftCount; i++)
            {
                var rt = NewRect("D" + i, _menuDrift);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                var img = rt.gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, DriftAlpha);
                _menuDrifters.Add(rt);
                _menuDriftImages.Add(img);
                _menuDriftLap.Add(int.MinValue);
                _menuDriftSlot.Add(0);
            }
        }

        /// <summary>Every showing starts the drift from the same arranged frame: the reading block measured
        /// (its width is the language's and the store key's to say), the strips' columns laid out beside it,
        /// the dealer back at the top of the shelf.</summary>
        private void ResetMenuDrift()
        {
            if (_menuDrifters.Count == 0) return;
            // The reading block: the lockup and everything in the column, in the drift's own space.
            float half = 0f;
            var cs = new Vector3[4];
            void Take(RectTransform r)
            {
                if (r == null || !r.gameObject.activeSelf) return;
                r.GetWorldCorners(cs);
                foreach (var c in cs) half = Mathf.Max(half, Mathf.Abs(_menuDrift.InverseTransformPoint(c).x));
            }
            Take(_menuPanel.Find("Logo") as RectTransform);
            if (_menuColumn != null)
                foreach (RectTransform r in _menuColumn)
                    if (r != _menuPointer) Take(r);
            if (half <= 0f) half = 290f;
            // Up to three columns a side, from a bottle's width off the block to a bottle's width off the edge.
            float from = half + 60f, to = DesignFrame.StageWidth * StageToHud * 0.5f - 40f;
            int n = Mathf.Clamp(1 + Mathf.FloorToInt((to - from) / 90f), 1, 3);
            _menuDriftSlots.Clear();
            for (int s = 0; s < n; s++)
                _menuDriftSlots.Add(to <= from ? to : Mathf.Lerp(from, to, (s + 0.5f) / n));
            _menuDriftNext = 0;
            for (int i = 0; i < _menuDrifters.Count; i++)
            {
                _menuDriftLap[i] = int.MinValue;
                _menuDriftImages[i].sprite = null;
            }
        }

        /// <summary>Stands drifter <paramref name="i"/> where its lane has it at unscaled second
        /// <paramref name="t"/>: rising up its side strip with a small sway — constants from the index,
        /// never a die — and entering as a new bottle each time the lane has carried it off the top.</summary>
        private void PlaceDrifter(int i, float t)
        {
            var rt = _menuDrifters[i];
            if (rt == null || _menuDriftSlots.Count == 0) return;
            float laneH = DesignFrame.StageHeight * StageToHud + 2f * DriftMargin;
            // One pace a side, so a strip's five keep the even spacing the showing opens with (a fifth of the
            // lane) and never bunch or pass; the two strips move at different paces, and the sway and the lean
            // are each bottle's own.
            float speed = (i & 1) == 0 ? 15f : 16.5f;
            float ys = (i * 0.1f + 0.05f) * laneH + t * speed;   // the showing opens with the ten spread up the height
            int lap = Mathf.FloorToInt(ys / laneH);
            float y = Mathf.Repeat(ys, laneH) - laneH * 0.5f;
            if (lap != _menuDriftLap[i]) EnterDrifter(i, lap, y);
            float side = (i & 1) == 0 ? -1f : 1f;
            float sway = Mathf.Sin(t * (0.45f + (i % 3) * 0.1f) + i * 1.7f) * (8f + (i % 4) * 2f);
            // A WHOLE TEXEL AT A TIME (2026-09-29, BUILD_SPEC §3): the bottle's corner stands on the even grid, so its 2x
            // texels never fall between the field's - a bottle sliding by fractions shimmered at its label's edges.
            var half = rt.sizeDelta * 0.5f;
            rt.anchoredPosition = new Vector2(Mathf.Round((side * _menuDriftSlots[_menuDriftSlot[i]] + sway - half.x) * 0.5f) * 2f + half.x,
                                              Mathf.Round((y - half.y) * 0.5f) * 2f + half.y);
        }

        /// <summary>A drifter coming in under the bottom: its column (the first of its strip, from a place the
        /// index and the lap pick, that the bottle a fifth of the lane above it is not using), its lean, and the
        /// next bottle off the shelf that is not already on screen.</summary>
        private void EnterDrifter(int i, int lap, float y)
        {
            _menuDriftLap[i] = lap;
            int h;
            unchecked
            {
                uint u = (uint)(i * 73856093) ^ (uint)(lap * 19349663);
                u ^= u >> 13; u *= 0x5bd1e995; u ^= u >> 15;
                h = (int)(u & 0x7fffffff);
            }
            int slots = _menuDriftSlots.Count, pick = h % slots;
            for (int k = 0; k < slots; k++)
            {
                int s = (pick + k) % slots;
                bool crowded = false;
                for (int j = (i & 1); j < _menuDrifters.Count && !crowded; j += 2)
                    crowded = j != i && _menuDriftLap[j] != int.MinValue && _menuDriftSlot[j] == s
                              && Mathf.Abs(_menuDrifters[j].anchoredPosition.y - y) < 240f;
                if (!crowded) { pick = s; break; }
            }
            _menuDriftSlot[i] = pick;
            // UPRIGHT (2026-09-29): a lean of up to ten degrees rotated each bottle's pixels off the grid - the one
            // drawing on the door that was not whole texels. The sway still says it is floating.
            _menuDrifters[i].localRotation = Quaternion.identity;
            // the dealer: the next bottle on the shelf that no other drifter is showing
            var img = _menuDriftImages[i];
            img.sprite = null;
            int n = _menuDriftArt.Count;
            Sprite art = _menuDriftArt[_menuDriftNext % n];
            for (int k = 0; k < n; k++)
            {
                var cand = _menuDriftArt[(_menuDriftNext + k) % n];
                bool shown = false;
                foreach (var other in _menuDriftImages) shown |= other.sprite == cand;
                if (!shown) { art = cand; _menuDriftNext += k; break; }
            }
            _menuDriftNext++;
            img.sprite = art;
            _menuDrifters[i].sizeDelta = art.rect.size * 2f;   // the house scale
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
            // The column stands over the whole field and everything in it is laid by field rows (the door's rects
            // above); its keys stay its direct children, so the suite's MainMenu/Column/NEW RUN still finds the door.
            _menuColumn = NewRect("Column", _menuPanel);
            Stretch(_menuColumn, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            float y = DoorColTop;

            bool saved = SaveStore.TryPeek(out var summary);
            // THE ONE AMBER KEY is the thing to do next (BUILD_SPEC §1): CONTINUE under a save, NEW RUN without one.
            var cont = DoorKey("CONTINUE", UIText.T("chrome.pause.continue"), "continue", saved, ref y, () =>
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
                // the note two under CONTINUE's foot, its caps' middle eight under it (the mock's)
                var note = NewText("ContinueNote", _menuColumn, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
                FieldRect(note.rectTransform, 640f - DoorKeyMinW * 0.5f, y - DoorPitch + SignKeyH + 2f, DoorKeyMinW, 12f);
                note.horizontalOverflow = HorizontalWrapMode.Overflow;
                note.text = UIText.T("chrome.menu.continue_note",
                    ("n", summary.day.ToString()), ("money", summary.money.ToString()));
                Enter(note.rectTransform, true);
                y += DoorNoteH;
            }
            else
            {
                // NO SAVE: dark glass - the door is there, there is just nothing behind it yet. The Button's own flag is
                // the switch (the smoke test reads it; SignKey draws its dead state off it).
                cont.Button.interactable = false;
                cont.Apply();
            }

            DoorKey("NEW RUN", UIText.T("chrome.pause.new_run"), "new_run", !saved, ref y, () =>
            {
                HideMainMenu();
                _bootstrap?.StartFreshRun(SeedPolicy.Next());
            });
            DoorKey("SETTINGS", UIText.T("chrome.pause.settings"), "settings", false, ref y, () => OpenSettingsFromMenu(null));
            DoorKey("QUIT", UIText.T("chrome.pause.quit"), "quit", false, ref y, Application.Quit);

            // THE LEDGE (2026-09-29, in place of the three sunset rules): a Graphite sill - its lit row, its lip, its shade -
            // with the cellar's deco fan standing on its middle, between the doors and the small row.
            float ledgeY = y - (DoorPitch - SignKeyH) + DoorLedgeGap;
            var ledge = NewRect("Ledge", _menuColumn);
            FieldRect(ledge, 640f - DoorLedgeW * 0.5f, ledgeY, DoorLedgeW, 6f);
            FieldFill(ledge, "Lit", 0f, 0f, DoorLedgeW, 2f, UITheme.Graphite[4]);
            FieldFill(ledge, "Lip", 0f, 2f, DoorLedgeW, 2f, UITheme.Graphite[2]);
            FieldFill(ledge, "Shade", 0f, 4f, DoorLedgeW, 2f, UITheme.Night[0]);
            var fan = NewRect("Fan", ledge);
            FieldRect(fan, DoorLedgeW * 0.5f - 26f, -24f, 52f, 28f);   // its flat foot over the ledge's lit row
            var fanImg = fan.gameObject.AddComponent<Image>();
            fanImg.sprite = MenuArt.Fan();
            fanImg.raycastTarget = false;
            Enter(ledge, true);

            // The small row: straight to the two pages the author named, the achievements, the credits, and the store
            // once it has an address to send anyone to. Its marks are the second line's cyan.
            float rowY = ledgeY + DoorSmallGap;
            var small = new List<SignKey>
            {
                DoorSmallKey("AUDIO", UIText.T("chrome.settings.audio"), "audio", () => OpenSettingsFromMenu("AUDIO")),
                DoorSmallKey("LANGUAGE", UIText.T("chrome.settings.language"), "language", () => OpenSettingsFromMenu("LANGUAGE")),
                // the achievements (2026-09-28): the list of what a bar earns, lit as it is earned
                DoorSmallKey("ACHIEVEMENTS", UIText.T("chrome.menu.achievements"), "achievements", OpenAchievements),
                DoorSmallKey("CREDITS", UIText.T("chrome.menu.credits"), "credits", OpenCredits),
            };
            if (StoreLink.HasPage)
                small.Add(DoorSmallKey("STEAM", "STEAM", "store", StoreLink.Open));
            float total = -DoorSmallSpace;
            foreach (var key in small) total += ((RectTransform)key.transform).sizeDelta.x + DoorSmallSpace;
            float x = 640f - total * 0.5f;
            foreach (var key in small)
            {
                float w = ((RectTransform)key.transform).sizeDelta.x;
                PlaceSignKey(key, x + w * 0.5f, rowY);
                x += w + DoorSmallSpace;
                Enter((RectTransform)key.transform, true);
            }

            // THE POINTER: a neon arrow beside the key under the mouse - the same tube as the keys' marks, lit, with
            // both bands of its light (it stands on the field, not inside a key), in the HOVER'S BLUE (2026-09-29: the
            // one colour the game answers a pointer with - the key under it wears a ClubBlue tube too; it was the
            // 1-bit arrow tinted magenta).
            var arrow = new NeonIcons.View(_menuColumn, "Pointer", "pointer", new Vector2(0f, 1f), Vector2.zero);
            arrow.Show(NeonIcons.State.Lit, UITheme.ClubBlue, true);
            _menuPointer = arrow.Rt;
            _menuPointer.pivot = new Vector2(1f, 0.5f);
            _menuPointer.gameObject.SetActive(false);
        }

        /// <summary>A door of the column: a sign key 50 tall and at least 352 wide, centred on the field at
        /// <paramref name="y"/>, its mark in the house's magenta; the next one a pitch lower.</summary>
        private SignKey DoorKey(string id, string label, string icon, bool primary, ref float y, Action onClick)
        {
            var key = MenuSignKey(_menuColumn, id, label, icon, UITheme.Magenta, primary, 1, SignKeyH, DoorKeyMinW, onClick);
            PlaceSignKey(key, 640f, y);
            Enter((RectTransform)key.transform, false);
            y += DoorPitch;
            return key;
        }

        /// <summary>One of the small row's keys, fitted to its word and NOT yet placed — the row centres itself once it
        /// knows all its widths. Its mark is the second line's cyan.</summary>
        private SignKey DoorSmallKey(string id, string label, string icon, Action onClick) =>
            MenuSignKey(_menuColumn, id, label, icon, UITheme.Cyan, false, 1, SignKeySmallH, DoorSmallMinW, onClick);

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

        /// <summary>The settings over the menu, landing on <paramref name="page"/> when one is named (the small row's
        /// AUDIO and LANGUAGE); BACK returns to the menu. The door's panel is HIDDEN, not stepped aside (the critic,
        /// 2026-09-29): its full-field catcher is on canvas 31, over the settings' 29, and would swallow their clicks -
        /// <see cref="MenuUp"/> reads <c>_settingsFromMenu</c> instead, so the night stays held and the door's record
        /// plays on.</summary>
        private void OpenSettingsFromMenu(string page)
        {
            _settingsFromMenu = true;
            _menuPanel.gameObject.SetActive(false);   // the hold stays: SetPaused is the menu's until a key lets go
            ToggleSettings();
            if (page != null) ShowSettingsPage(page);
        }

        /// <summary>The door's own face - the sign (or its worded stand-in), the column, the maker's line and the build
        /// - steps aside for a page opened over it (the credits) and comes back with it. The field, the drift and the
        /// vignette stay behind the page, so the panel stays up, and with it the hold and <see cref="MenuUp"/>.</summary>
        private void ShowDoorFace(bool on)
        {
            if (_menuPanel == null) return;
            foreach (var part in DoorFace)
            {
                var t = _menuPanel.Find(part);
                if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            }
            if (_menuColumn != null && _menuColumn.gameObject.activeSelf != on) _menuColumn.gameObject.SetActive(on);
            _menuHover = null;                        // the pointer answers the next key the mouse finds
        }

        private static readonly string[] DoorFace = { "Logo", "Title", "Studio", "Version" };

        /// <summary>The one offer, at the cold boot's first run — never after a language reload (unless the reload was
        /// asked for from the door itself: its LANGUAGE page's APPLY goes back to the door, GameBootstrap.DoorAcrossReload),
        /// never after START OVER, never on the runs the menu itself starts.</summary>
        private void OfferMainMenuAtBoot()
        {
            if (_menuOffered) return;
            _menuOffered = true;
            if (_bootstrap == null || (_bootstrap.ResumedAcrossReload && !_bootstrap.DoorAcrossReload)) return;
            ShowMainMenu();
        }

        private void ShowMainMenu()
        {
            if (_menuPanel == null) return;
            CloseId();
            RebuildMenuColumn();
            if (_creditsPanel != null) _creditsPanel.gameObject.SetActive(false);
            if (_achievementsPanel != null) _achievementsPanel.gameObject.SetActive(false);
            ShowDoorFace(true);                       // a door that went away under its credits comes back whole
            _menuPanel.gameObject.SetActive(true);
            // The sign strikes on from dark at every showing from the boot or a run - inside the panel's fade, so
            // over the game over's papers it comes up with the door rather than popping dark tubes over them.
            if (_menuSign != null) _menuSign.Ignite();
            SetPaused(true);
            _menuFadeT = Motion.Reduced ? 1f : 0f;
            if (_menuFade != null) _menuFade.alpha = Motion.Reduced ? 1f : 0f;
            // The drift and the entrance start from the SAME arranged frame at every showing (and stay
            // parked on the finished frame under reduced motion — deterministically, so the look test
            // can photograph it).
            _menuShownAt = Time.unscaledTime;
            FillMenuDrift();
            ResetMenuDrift();
            for (int i = 0; i < _menuDrifters.Count; i++) PlaceDrifter(i, 0f);
            StepMenuEntrance(Motion.Reduced ? float.MaxValue : 0f);
        }

        /// <summary>The title's fade, its drift, the keys' rise and the pointer, on the unscaled clock —
        /// the menu holds the game's own. Reduced motion parks them all on the finished frame.</summary>
        private void StepMenuFade()
        {
            if (_menuPanel == null || !_menuPanel.gameObject.activeSelf) return;   // (MenuUp also counts its settings)
            if (_menuFade != null && _menuFadeT < 1f)
            {
                _menuFadeT = Motion.Reduced ? 1f : Mathf.Min(1f, _menuFadeT + Time.unscaledDeltaTime / 0.6f);
                _menuFade.alpha = _menuFadeT;
            }
            float t = Motion.Reduced ? float.MaxValue : Time.unscaledTime - _menuShownAt;
            StepMenuEntrance(t);
            StepMenuPointer();
            if (Motion.Reduced) return;             // parked on the showing's own scatter
            for (int i = 0; i < _menuDrifters.Count; i++) PlaceDrifter(i, t);
        }

        // (The sign's own light runs in TitleSign.LateUpdate on the same unscaled clock, and parks the same way.)

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

        /// <summary>The neon arrow stands at the left of the hovered key, level with the key's own mark, and leans
        /// toward it a whole texel at a time (two units: the 2x arrow never lands between its own pixels).</summary>
        private void StepMenuPointer()
        {
            if (_menuPointer == null) return;
            bool show = _menuHover != null && _menuHover.gameObject.activeInHierarchy;
            if (_menuPointer.gameObject.activeSelf != show) _menuPointer.gameObject.SetActive(show);
            if (!show) return;
            float lean = Motion.Reduced ? 0f : Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f)) * 2f;
            // the key's left edge, in the column's own frame (keys and arrow both hang from its top-left corner); the
            // arrow is level with the key's middle, where a sign key's mark is centred
            var home = _menuHover.anchoredPosition;
            float left = home.x - _menuHover.pivot.x * _menuHover.sizeDelta.x;
            float midY = home.y + (0.5f - _menuHover.pivot.y) * _menuHover.sizeDelta.y;
            _menuPointer.anchoredPosition = new Vector2(left - 4f + lean, midY);
        }

        private void HideMainMenu()
        {
            if (_menuPanel == null) return;
            if (_creditsPanel != null) _creditsPanel.gameObject.SetActive(false);
            if (_achievementsPanel != null) _achievementsPanel.gameObject.SetActive(false);
            _menuPanel.gameObject.SetActive(false);
            // the door is gone until the next showing: the sign's texture goes with it (a step aside for the settings
            // keeps it, so coming back costs no decode)
            if (_menuSign != null) _menuSign.Release();
            SetPaused(false);
        }
    }
}
