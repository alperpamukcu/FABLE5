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
    /// It stands over the menu on the menu's own flat field (the room never shows out of game), on the
    /// quiet plate the settings wear, scrolls with the wheel, and BACK returns to the menu.
    /// </summary>
    public sealed partial class TycoonHud
    {
        [Serializable] private sealed class CreditsFile { public string game; public List<CreditsSection> sections; }
        [Serializable] private sealed class CreditsSection { public string head; public List<string> lines; }

        private RectTransform _creditsPanel;
        private ScrollRect _creditsScroll;

        private const float CreditsW = 760f, CreditsH = 600f, CreditsPad = 44f;

        /// <summary>Built on first open: most sessions never read it.</summary>
        private void OpenCredits()
        {
            if (_menuPanel == null) return;
            if (_creditsPanel == null) BuildCredits();
            _creditsPanel.gameObject.SetActive(true);
            _creditsPanel.SetAsLastSibling();
            if (_creditsScroll != null) _creditsScroll.verticalNormalizedPosition = 1f;
            Sfx.Play("menu_open", 0.7f);
        }

        private void CloseCredits()
        {
            if (_creditsPanel == null || !_creditsPanel.gameObject.activeSelf) return;
            _creditsPanel.gameObject.SetActive(false);
            Sfx.Play("menu_close", 0.6f);
        }

        private void BuildCredits()
        {
            _creditsPanel = NewRect("Credits", _menuPanel);
            Stretch(_creditsPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var field = _creditsPanel.gameObject.AddComponent<Image>();
            field.color = MenuField;                     // out of game: the menu's own field, wall to wall
            field.raycastTarget = true;

            var plate = NewRect("Plate", _creditsPanel);
            Place(plate, new Vector2(0.5f, 0.5f), new Vector2(CreditsW, CreditsH), new Vector2(0f, -8f));
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.color = UITheme.Night[2];
            plateImg.raycastTarget = true;
            NeonEdge(plate);

            NightTitle(plate, UIText.T("chrome.menu.credits"), -26f);
            SunsetRules(plate, -66f, CreditsW - 120f);

            // the scrolling column
            var view = NewRect("View", plate);
            view.anchorMin = new Vector2(0, 0); view.anchorMax = new Vector2(1, 1);
            view.offsetMin = new Vector2(CreditsPad, 86f);
            view.offsetMax = new Vector2(-CreditsPad, -82f);
            view.gameObject.AddComponent<RectMask2D>();
            var viewImg = view.gameObject.AddComponent<Image>();
            viewImg.color = new Color(0, 0, 0, 0);           // a catcher for the wheel, drawn as nothing
            viewImg.raycastTarget = true;
            var content = NewRect("Content", view);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            float width = CreditsW - CreditsPad * 2f;
            float y = 0f;

            var data = LoadCredits();
            if (data != null)
            {
                y = CreditsLine(content, data.game ?? "", _display, 16, UITheme.Amber[4], width, y, TextAnchor.UpperCenter) + 18f;
                foreach (var section in data.sections ?? new List<CreditsSection>())
                {
                    y = CreditsLine(content, UIText.T(section.head), _body, 16, UITheme.Cyan[4], width, y, TextAnchor.UpperCenter) + 6f;
                    bool studio = section.head == "chrome.credits.by";
                    foreach (var raw in section.lines ?? new List<string>())
                    {
                        string line = raw != null && raw.StartsWith("@") ? UIText.T(raw.Substring(1)) : raw ?? "";
                        y = CreditsLine(content, line, studio ? _display : _body, studio ? 16 : 8,
                            studio ? UITheme.Magenta[4] : UITheme.Cream[4], width, y, TextAnchor.UpperCenter) + 3f;
                    }
                    y += 16f;
                }
            }

            // THE LICENCE TRAVELS WITH THE FONTS: every notice, then the SIL OFL 1.1 once, verbatim,
            // in paragraphs (one Text per paragraph keeps each under UGUI's per-Text vertex ceiling).
            var lic = Resources.Load<TextAsset>("Data/licenses");
            if (lic != null)
            {
                y = CreditsLine(content, UIText.T("chrome.credits.licences"), _body, 16, UITheme.Cyan[4], width, y, TextAnchor.UpperCenter) + 8f;
                foreach (var para in lic.text.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                    y = CreditsLine(content, para.Trim(), _body, 8, UITheme.Cream[3], width, y, TextAnchor.UpperLeft) + 8f;
                y += 12f;
            }
            y = CreditsLine(content, UIText.T("chrome.credits.thanks"), _display, 16, UITheme.Magenta[4], width, y, TextAnchor.UpperCenter) + 8f;
            content.sizeDelta = new Vector2(0f, y);

            _creditsScroll = view.gameObject.AddComponent<ScrollRect>();
            _creditsScroll.content = content;
            _creditsScroll.viewport = view;
            _creditsScroll.horizontal = false;
            _creditsScroll.vertical = true;
            _creditsScroll.movementType = ScrollRect.MovementType.Clamped;
            _creditsScroll.inertia = false;
            _creditsScroll.scrollSensitivity = 32f;

            PackWordKey(plate, "BACK", UIText.T("chrome.settings.back"), "back", MenuPack.Tone.Orange, new Vector2(1, 0),
                new Vector2(180, 46), new Vector2(-CreditsPad, 22), () => { Sfx.Play("click"); CloseCredits(); }, 140f, 48f + 24f);

            _creditsPanel.gameObject.SetActive(false);
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
