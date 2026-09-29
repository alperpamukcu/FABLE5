using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE CREDITS (2026-09-27, the author: "Evet ekle" - a CREDITS screen on the front door). Two
    /// debts made it necessary rather than nice: the 1-bit icon pack's author asks for attribution
    /// (CC0, not required, given), and the SIL Open Font License asks that its text travel WITH the
    /// fonts - which it now does, in full, at the foot of this screen.
    ///
    /// CONTENT IS DATA: the sections come from Resources/Data/credits.json and the licence text from
    /// Resources/Data/licenses.txt, both written by Tools/credits_build.py out of their single sources
    /// (the fonts' own OFL files, the sound ledger, Docs/KREDILER.md's picture rows). The headings are
    /// string-table lines; the names under them are proper nouns and read the same in every language.
    ///
    /// A CABINET ON THE DOOR'S OWN FIELD SINCE 2026-09-29 (the menus rebuilt in the week's language, TycoonHud.MenuKit):
    /// the door's title and keys step aside and its field, its drifting bottles and its vignette stay behind - the room
    /// never shows out of game. CREDITS is lit in the crown; the game's name is a neon line over the recess's own Night[1]
    /// (NeonWord, baked over the ground it stands on); every section head stands on the cellar's cream enamel plate; a
    /// cyan tube down the recess's right side is the scroll, the span in view struck. It scrolls with the wheel or a drag;
    /// BACK - the one amber key - and Escape return to the door (Escape did nothing here: read_menus finding 5).
    /// </summary>
    public sealed partial class TycoonHud
    {
        [Serializable] private sealed class CreditsFile { public string game; public List<CreditsSection> sections; }
        [Serializable] private sealed class CreditsSection { public string head; public List<string> lines; }

        private RectTransform _creditsPanel;
        private ScrollRect _creditsScroll;
        private NeonStrike _creditsStrike;
        private NeonTube _creditsThumb;
        private int _creditsThumbLen, _creditsTrackLen;

        // THE CABINET, in field units (BUILD_SPEC §3 "Credits", re-laid for 21:9 on 2026-09-29 like the door): a 21:9
        // window crops the field to its rows 90..630 (DesignFrame), and the first cabinet - crown at 52, BACK 600..646 -
        // lost its lit title and half its BACK there (the review). The crown 90..130, the body 130..630 (its tube twelve
        // in, at 142 and 618), the recess 280..1000 x 154..554, BACK on the plinth under it, 566..612. The reading column
        // is centred on 632, the scroll tube's glass down x 980..982.
        private const float CreditsCabX = 256f, CreditsCabW = 768f, CreditsTop = 90f, CreditsCrownH = 40f, CreditsBodyH = 500f;
        private const float CreditsRecessX = 280f, CreditsRecessY = 154f, CreditsRecessW = 720f, CreditsRecessH = 400f;
        private const float CreditsViewL = 16f, CreditsViewR = 32f, CreditsViewV = 12f, CreditsBackY = 566f;

        /// <summary>Built on first open: most sessions never read it.</summary>
        private void OpenCredits()
        {
            if (_menuPanel == null) return;
            if (_creditsPanel == null) BuildCredits();
            ShowDoorFace(false);
            _creditsPanel.gameObject.SetActive(true);
            _creditsPanel.SetAsLastSibling();
            if (_creditsScroll != null) _creditsScroll.verticalNormalizedPosition = 1f;
            PlaceCreditsThumb();
            if (_creditsStrike != null) _creditsStrike.Restart();
            Sfx.Play("menu_open", 0.7f);
        }

        private void CloseCredits()
        {
            if (_creditsPanel == null || !_creditsPanel.gameObject.activeSelf) return;
            _creditsPanel.gameObject.SetActive(false);
            ShowDoorFace(true);
            Sfx.Play("menu_close", 0.6f);
        }

        private void BuildCredits()
        {
            _creditsPanel = NewRect("Credits", _menuPanel);
            Stretch(_creditsPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var field = _creditsPanel.gameObject.AddComponent<Image>();
            field.color = new Color(0f, 0f, 0f, 0f);     // the door's own field shows through; nothing behind takes a click
            field.raycastTarget = true;

            var cab = BuildMenuCabinet(_creditsPanel, "Plate", CreditsCabX, CreditsTop, CreditsCabW, CreditsCrownH,
                CreditsBodyH, UIText.T("chrome.menu.credits"), CreditsRecessY);
            _creditsStrike = cab.Strike;
            MenuRecess(_creditsPanel, "Recess", CreditsRecessX, CreditsRecessY, CreditsRecessW, CreditsRecessH, UITheme.Night[1]);

            // the scrolling column
            var view = NewRect("View", _creditsPanel);
            float viewW = CreditsRecessW - CreditsViewL - CreditsViewR, viewH = CreditsRecessH - 2f * CreditsViewV;
            FieldRect(view, CreditsRecessX + CreditsViewL, CreditsRecessY + CreditsViewV, viewW, viewH);
            view.gameObject.AddComponent<RectMask2D>();
            var viewImg = view.gameObject.AddComponent<Image>();
            viewImg.color = new Color(0, 0, 0, 0);           // a catcher for the wheel, drawn as nothing
            viewImg.raycastTarget = true;
            var content = NewRect("Content", view);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            float width = viewW - 16f;
            float y = 0f;

            var data = LoadCredits();
            if (data != null)
            {
                y = CreditsNeonLine(content, data.game ?? "", width, y) + 8f;
                foreach (var section in data.sections ?? new List<CreditsSection>())
                {
                    y = CreditsHead(content, UIText.T(section.head), y) + 8f;
                    bool studio = section.head == "chrome.credits.by";
                    foreach (var raw in section.lines ?? new List<string>())
                    {
                        string line = raw != null && raw.StartsWith("@") ? UIText.T(raw.Substring(1)) : raw ?? "";
                        y = CreditsLine(content, line, _body, studio ? 16 : 8, UITheme.Cream[4], width, y,
                            TextAnchor.UpperCenter) + (studio ? 4f : 3f);
                    }
                    y += 16f;
                }
            }

            // THE LICENCE TRAVELS WITH THE FONTS: every notice, then the SIL OFL 1.1 once, verbatim,
            // in paragraphs (one Text per paragraph keeps each under UGUI's per-Text vertex ceiling).
            var lic = Resources.Load<TextAsset>("Data/licenses");
            if (lic != null)
            {
                y = CreditsHead(content, UIText.T("chrome.credits.licences"), y) + 10f;
                foreach (var para in lic.text.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                    y = CreditsLine(content, para.Trim(), _body, 8, UITheme.Cream[3], width, y, TextAnchor.UpperLeft) + 8f;
                y += 8f;
            }
            y = CreditsNeonLine(content, UIText.T("chrome.credits.thanks"), width, y);
            content.sizeDelta = new Vector2(0f, y);

            _creditsScroll = view.gameObject.AddComponent<ScrollRect>();
            _creditsScroll.content = content;
            _creditsScroll.viewport = view;
            _creditsScroll.horizontal = false;
            _creditsScroll.vertical = true;
            _creditsScroll.movementType = ScrollRect.MovementType.Clamped;
            _creditsScroll.inertia = false;
            _creditsScroll.scrollSensitivity = 32f;
            _creditsScroll.onValueChanged.AddListener(_ => PlaceCreditsThumb());

            // THE SCROLL IS A TUBE (BUILD_SPEC §3): unlit glass down the recess's right side, the span in view struck
            // cyan - the current thing, in the second line's colour - moving in whole texels.
            _creditsTrackLen = Mathf.RoundToInt(CreditsRecessH / 2f) - 16;
            var track = new NeonTube(_creditsPanel, "ScrollTrack", l => MenuArt.NeonLine(_creditsTrackLen, true, l),
                new Vector2((1 + 2 * ChromeArt.NeonPad) * 2f, (_creditsTrackLen + 2 * ChromeArt.NeonPad) * 2f),
                new Vector2(0f, 1f), Vector2.zero);
            SetTubeTopLeft(track, CreditsRecessX + CreditsRecessW - 26f, CreditsRecessY + 10f);
            track.Show(NeonIcons.State.Dark, UITheme.Cyan, false);
            float shown = Mathf.Clamp01(viewH / Mathf.Max(1f, y));
            _creditsThumbLen = Mathf.Clamp(Mathf.RoundToInt(_creditsTrackLen * shown), 12, _creditsTrackLen);
            _creditsThumb = new NeonTube(_creditsPanel, "ScrollThumb", l => MenuArt.NeonLine(_creditsThumbLen, true, l),
                new Vector2((1 + 2 * ChromeArt.NeonPad) * 2f, (_creditsThumbLen + 2 * ChromeArt.NeonPad) * 2f),
                new Vector2(0f, 1f), Vector2.zero);
            _creditsThumb.Show(NeonIcons.State.Lit, UITheme.Cyan, true);
            track.Visible = _creditsThumb.Visible = shown < 1f;
            PlaceCreditsThumb();

            // BACK, the one amber key, on the plinth under the recess's right end
            var back = MenuSignKey(_creditsPanel, "BACK", UIText.T("chrome.settings.back"), "back", UITheme.Cyan, true, 2,
                SignKeySmallH, 180f, () => { Sfx.Play("click"); CloseCredits(); });
            var backRt = (RectTransform)back.transform;
            PlaceSignKey(back, CreditsRecessX + CreditsRecessW - backRt.sizeDelta.x * 0.5f, CreditsBackY);

            _creditsPanel.gameObject.SetActive(false);
        }

        /// <summary>A tube's rect by its sprite's top-left corner in field units.</summary>
        private static void SetTubeTopLeft(NeonTube tube, float x, float y)
        {
            var s = tube.Rt.sizeDelta;
            tube.Rt.anchoredPosition = new Vector2(x + s.x * 0.5f, -(y + s.y * 0.5f));
        }

        /// <summary>The struck span follows the scroll, a whole texel at a time.</summary>
        private void PlaceCreditsThumb()
        {
            if (_creditsThumb == null || _creditsScroll == null) return;
            float down = 1f - Mathf.Clamp01(_creditsScroll.verticalNormalizedPosition);
            int step = Mathf.RoundToInt((_creditsTrackLen - _creditsThumbLen) * down);
            SetTubeTopLeft(_creditsThumb, CreditsRecessX + CreditsRecessW - 26f, CreditsRecessY + 10f + step * 2f);
        }

        /// <summary>A section's head on the cellar's cream enamel plate (MenuArt.Enamel, as wide as its word and 16 of
        /// air), its word Night[1] in the body face; returns the y under it.</summary>
        private float CreditsHead(RectTransform content, string word, float y)
        {
            var plate = NewRect("Head", content);
            var img = plate.gameObject.AddComponent<Image>();
            img.sprite = MenuArt.Enamel();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.5f;
            img.raycastTarget = false;
            var text = NewText("Word", plate, _body, 16, TextAnchor.MiddleCenter, UITheme.Night[1]);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 2f), new Vector2(1f, 0f));
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = word;
            float w = Mathf.Max(64f, SnapUp(text.preferredWidth + 32f, 4f));
            plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0.5f, 1f);
            plate.sizeDelta = new Vector2(w, 24f);
            plate.anchoredPosition = new Vector2(0f, -y);
            return y + 24f;
        }

        /// <summary>A line lit as a sign (the game's name, the thanks): display 16 in NeonWord's light, baked over the
        /// recess's own Night[1] - its reach of room above and below, so the view's edge never cuts it at rest.</summary>
        private float CreditsNeonLine(RectTransform content, string text, float width, float y)
        {
            float reach = NeonWord.Reach * 2f;                  // display 16: a face pixel is two units
            float under = CreditsLine(content, text, _display, 16, UITheme.Cream[4], width, y + reach, TextAnchor.UpperCenter);
            var line = content.GetChild(content.childCount - 1).GetComponent<Text>();
            var word = line.gameObject.AddComponent<NeonWord>();
            word.Ramp = UITheme.Magenta;
            word.Ground = UITheme.Night[1];
            return under + reach;
        }

        /// <summary>One wrapped line of the credits at <paramref name="y"/> (measured down from the
        /// content's top); returns the y under it.</summary>
        private float CreditsLine(RectTransform content, string text, Font font, int size, Color ink, float width,
            float y, TextAnchor align)
        {
            var t = NewText("Line", content, font, size, align, ink);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.fontSize = LanguageFonts.Size(font, size);
            t.text = text;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(width, 10f);
            float h = Mathf.Ceil(t.cachedTextGeneratorForLayout.GetPreferredHeight(text,
                t.GetGenerationSettings(new Vector2(width, 0f))) / t.pixelsPerUnit);
            rt.sizeDelta = new Vector2(width, Mathf.Max(h, size));
            rt.anchoredPosition = new Vector2(0f, -y);
            return y + rt.sizeDelta.y;
        }

        private static CreditsFile LoadCredits()
        {
            var asset = Resources.Load<TextAsset>("Data/credits");
            if (asset == null) return null;
            try { return JsonUtility.FromJson<CreditsFile>(asset.text); }
            catch (Exception e) { Debug.LogWarning("[LastCall] credits.json would not read: " + e.Message); return null; }
        }
    }
}
