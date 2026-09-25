using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Note: the drink a customer asked for, as the player needs it at the bench - the WORK CARD the
    // order's own tip prints over a head, and the same card pinned to the right of the screen as a note (2026-09-25,
    // the author: "Müşterilerin kimlikte kullandığı hover sağ üstteki bir sabitleme butonuna basarak ekranın sağına
    // küçük postit gibi her şeyin üzerinde olacak şekilde sadece oyuncunun içeceği yaparken ihtiyacı olacak bilgileri
    // barındıran bilgi kutusu gelecek, o kutuyu x basarak kapatabilmeliyiz aynı zamanda").
    public sealed partial class TycoonHud
    {
        // ── the work card ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// WHAT THE BENCH NEEDS, IN THE BOOK'S OWN BLOCKS: whose drink it is (on the note), its name in the title
        /// face, how it is worked and what it goes in (the book's chips - the work's difficulty only where there is
        /// room to spare, <paramref name="withWork"/>), how THIS customer wants it (<paramref name="asks"/>, the
        /// garnish run the page's habit row draws), the pours at the book's measure, and the fill line where the page
        /// has one. Nothing a player does not act on at the bench: no price, no character, no legend, no story.
        ///
        /// Every block is the page's own method (RecipeChips, AsksRow, BookPourRows), so the order's tip, the pinned
        /// note and the menu cannot drift apart. Returns the height used.
        /// </summary>
        private float DrawWorkCard(RectTransform host, RecipeDefinition r, IReadOnlyList<PreparationDefinition> asks,
            float width, string whose = null, bool withWork = true)
        {
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);
            if (r == null) return 0f;
            var run = Run;
            Color quiet = new Color(0.52f, 0.44f, 0.36f);
            Color figure = new Color(0.10f, 0.06f, 0.02f);
            float y = 0f;

            Text Centred(string name, Font face, int size, Color colour, float h)
            {
                var t = NewText(name, host, face, size, TextAnchor.MiddleCenter, colour);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                t.rectTransform.pivot = new Vector2(0.5f, 1f);
                t.rectTransform.sizeDelta = new Vector2(width, h);
                t.rectTransform.anchoredPosition = new Vector2(0f, -y);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = false;
                return t;
            }

            if (!string.IsNullOrEmpty(whose))
            {
                var who = Centred("Whose", _body, 8, quiet, 12f);
                who.text = UIText.Caps(whose);
                y += 13f;
            }

            // The name in the title face; a name wider than the card steps down a face at a time rather than
            // running over the frame (LONG ISLAND ICED TEA is wider than a note in the title face).
            var (titleFace, titlePx) = TitleFace();
            var head = Centred("Head", titleFace, titlePx, new Color(0.30f, 0.16f, 0.05f), 30f);
            head.text = UIText.Caps(RecipeTitle(r));
            if (head.preferredWidth > width) { head.font = _display; head.fontSize = LanguageFonts.Size(_display, 16); }
            if (head.preferredWidth > width) { head.font = _body; head.fontSize = LanguageFonts.Size(_body, 16); }
            if (head.preferredWidth > width) head.fontSize = LanguageFonts.Size(_body, 8);
            y += 32f;

            y += RecipeChips(host, r, width, y, withWork) + 4f;

            if (asks != null && asks.Count > 0)
            {
                var ids = new List<string>(asks.Count);
                foreach (var a in asks) if (a != null) ids.Add(a.Id);
                float row = AsksRow(host, ids, UIText.T("id.tip.how_they_want_it"), width, y, dark: false);
                if (row > 0f) y += row + 4f;
            }

            if (run == null) return y;
            int pours = 0;
            var peek = RecipeSpecRows(r, poursOnly: true, locked: false);
            for (int k = 0; k < peek.Count; k++)
                if (!(k == 0 && r.Id != "draught") && !peek[k].Hint) pours++;
            y += BookPourRows(host, r, run, width, y, pours <= 3 ? 40f : 34f, locked: false);

            if (r.MinFill > 0)
            {
                y += 2f;
                var fillLine = Centred("Fill", _body, 16, figure, 20f);
                fillLine.text = UIText.T("book.page.fill", ("pct", (r.MinFill * 100).ToString("0")));
                y += 22f;
            }
            return y;
        }

        // ── the pinned note ─────────────────────────────────────────────────────────────────────────────────────
        //
        // A post-it on the right of the screen, over everything the player works in - the room, the benches, the
        // book - and under the pause menu and the curtain. It says what one drink needs and nothing else, stays
        // until its X is pressed, and lets itself go when the night it was written for closes (a note from last
        // night over tomorrow's market is litter).

        private const float NoteW = 280f, NotePad = 12f, NoteTop = 104f, NoteRight = 12f;
        private RectTransform _note, _noteBody;
        private int _noteDay = -1;

        /// <summary>The note's canvas order: over the bench (25), the licence (26) and the book (27); under the
        /// pause menu and the settings (29) and the curtain and the toast (30).</summary>
        private const int NoteSortingOrder = 28;

        private void BuildNote(RectTransform root)
        {
            _note = NewRect("PinnedNote", root);
            _note.anchorMin = _note.anchorMax = _note.pivot = new Vector2(1f, 1f);
            _note.anchoredPosition = new Vector2(-NoteRight, -NoteTop);
            _note.sizeDelta = new Vector2(NoteW, 120f);
            var canvas = _note.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = NoteSortingOrder;
            _note.gameObject.AddComponent<ForgivingRaycaster>();

            // Its shadow on whatever it is stuck over, then the paper - a post-it's pale amber, framed a step darker
            // - and a strip of tape across its head.
            var shadow = NewRect("Shadow", _note);
            Stretch(shadow, Vector2.zero, Vector2.one, new Vector2(4f, -4f), new Vector2(4f, -4f));
            var shadowImg = shadow.gameObject.AddComponent<Image>();
            shadowImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.35f);
            shadowImg.raycastTarget = false;

            var paper = NewRect("Paper", _note);
            Stretch(paper, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var paperImg = paper.gameObject.AddComponent<Image>();
            paperImg.color = UITheme.Amber[4];
            paperImg.raycastTarget = true;          // a press on the note stops on the note, never in the room behind
            Frame(paper, 2f, UITheme.Amber[2]);
            var grain = NewRect("Grain", paper);
            Stretch(grain, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var grainImg = grain.gameObject.AddComponent<Image>();
            grainImg.sprite = ChromeArt.PaperGrain();
            grainImg.type = Image.Type.Tiled;
            grainImg.color = new Color(1f, 1f, 1f, 0.6f);
            grainImg.raycastTarget = false;

            var tape = NewRect("Tape", _note);
            tape.anchorMin = tape.anchorMax = tape.pivot = new Vector2(0.5f, 1f);
            tape.sizeDelta = new Vector2(56f, 12f);
            tape.anchoredPosition = new Vector2(0f, 6f);
            var tapeImg = tape.gameObject.AddComponent<Image>();
            tapeImg.color = new Color(UITheme.Cream[4].r, UITheme.Cream[4].g, UITheme.Cream[4].b, 0.72f);
            tapeImg.raycastTarget = false;

            _noteBody = NewRect("Body", _note);
            _noteBody.anchorMin = _noteBody.anchorMax = _noteBody.pivot = new Vector2(0.5f, 1f);
            _noteBody.sizeDelta = new Vector2(NoteW - NotePad * 2f, 10f);
            _noteBody.anchoredPosition = new Vector2(0f, -NotePad);

            // THE X: a 20-unit key in the top-right corner, the house's close mark on it.
            var close = NewRect("Close", _note);
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
            close.sizeDelta = new Vector2(20f, 20f);
            close.anchoredPosition = new Vector2(-4f, -4f);
            var hit = close.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            var key = close.gameObject.AddComponent<Button>();
            key.transition = Selectable.Transition.None;
            key.onClick.AddListener(CloseNote);
            var x = NewRect("X", close);
            x.anchorMin = x.anchorMax = x.pivot = new Vector2(0.5f, 0.5f);
            x.sizeDelta = new Vector2(16f, 16f);
            x.anchoredPosition = Vector2.zero;
            var xi = x.gameObject.AddComponent<Image>();
            xi.sprite = ChromeArt.Mark("win_close");
            xi.color = new Color(0.30f, 0.16f, 0.05f);
            xi.raycastTarget = false;

            _note.gameObject.SetActive(false);
        }

        /// <summary>Pins the drink to the note, replacing whatever was pinned: one note, the drink being made.</summary>
        private void PinNote(RecipeDefinition r, IReadOnlyList<PreparationDefinition> asks, string whose)
        {
            if (_note == null || r == null) return;
            float inner = NoteW - NotePad * 2f;
            // The work's three lamps stay off the note: how hard a drink is does not change how it is made, and the
            // note's column is narrower than the page's.
            float h = DrawWorkCard(_noteBody, r, asks, inner, whose, withWork: false);
            _noteBody.sizeDelta = new Vector2(inner, h);
            _note.sizeDelta = new Vector2(NoteW, h + NotePad * 2f);
            foreach (var g in _noteBody.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            _noteDay = Run != null ? Run.Day : -1;
            _note.gameObject.SetActive(true);
            _note.SetAsLastSibling();
        }

        private void CloseNote()
        {
            if (_note != null) _note.gameObject.SetActive(false);
        }

        /// <summary>Once a frame (from <see cref="UpdateOrderTip"/>): the note lets itself go when the night it was
        /// pinned on is over - the doors closed, or a new night begun.</summary>
        private void StepNote()
        {
            if (_note == null || !_note.gameObject.activeSelf) return;
            var run = Run;
            if (run == null || run.Day != _noteDay || run.Phase != TycoonPhase.DayOpen) CloseNote();
        }
    }
}
