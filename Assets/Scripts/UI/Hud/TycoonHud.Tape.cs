using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Tape: the night's Z-tape (2026-09-28, the day-end rebuilt; spec §2.4-§2.5).
    //
    // A till's end-of-day report, torn off the roll: the author's own paper (Items/bill_sheet.png, cut by ChromeArt at a
    // whole 3x and as long as the night), one ink on one paper, printed top to bottom in the order it counts - the
    // counts line, what came in and its subtotal, what went out and its subtotal, NET (the one biggest figure on the
    // screen), the TILL, the two ratings with the lower ringed in pen and the pen run on to the stars the night is filed
    // at, the stars, and the stamp's own zone. Its last piece is the counterfoil, which the show tears off along the
    // perforation and hangs on the week's hook (TycoonHud.Week).
    //
    // Names are addresses (the suite reads them): ZRig holds everything that shakes; ZTape is the unroll's mask; ZPaper
    // the paper; the rows live under ZRows as Row.in.*, Row.out.*, Row.net, Row.till, Rate.*, Stars; every money figure's
    // digits are a Text named V.
    public sealed partial class TycoonHud
    {
        /// <summary>The stock at a whole 3x, its print column, the counterfoil and a folded ticket.</summary>
        private const float TapeW = 456f, TapePrint = 392f, TapeStubH = 66f, TicketW = 228f;

        private RectTransform _zRig, _zTape, _zPaper, _zRows, _zFoot, _zStub, _zStubFold, _zPerf;
        private Image _zPaperImg, _zStubImg;
        /// <summary>The tape's body (everything above the counterfoil) and the whole of it, both on the 12-unit grid.</summary>
        private float _lBody, _lTotal;
        private Vector2 _zRigHome;
        /// <summary>Which of the night's counts is the TILL - the debt alarm sounds as it starts.</summary>
        private int _tillCount = -1;

        // the grade: the icons that fill, the figures that count with them, the ring and the pen's run
        private readonly List<(Image img, float value, int slot)> _gradeFills = new List<(Image, float, int)>();
        private readonly List<(Text text, double value)> _rateFigures = new List<(Text, double)>();
        private RectTransform _ringMask, _penMask;
        private float _ringW, _penH, _ringBottom;
        private Text _starFigure;
        private double _starFigureValue;

        // the tear: where the counterfoil starts from, in the night sheet's own space
        private Vector2 _stubFrom;

        private static Color TapeInk => UITheme.Night[1];
        private static Color TapeQuiet => UITheme.Cream[1];

        /// <summary>
        /// The guard's steps (spec §2.4): a tape longer than the field is laid again a step tighter - never scaled.
        /// (1) both dashed rules 16 to 8; (2) the gap between the blocks 8 to 4; (3) item rows 24 to 20, their words and
        /// figures at 16; (4) heads and subtotals 26 to 22. Measured once, at build.
        /// </summary>
        private struct TapeMetrics
        {
            public float Rule, Gap, Item, Head, Sub;
            public int ItemSize;

            public static TapeMetrics At(int level) => new TapeMetrics
            {
                Rule = level >= 1 ? 8f : 16f,
                Gap = level >= 2 ? 4f : 8f,
                Item = level >= 3 ? 20f : 24f,
                ItemSize = level >= 3 ? 16 : 24,
                Head = level >= 4 ? 22f : 26f,
                Sub = level >= 4 ? 22f : 26f,
            };
        }

        /// <summary>Lays the tape; returns its whole length (body and counterfoil).</summary>
        private float BuildTape(TycoonRun run, RectTransform parent)
        {
            for (int level = 0; ; level++)
            {
                float total = LayTape(run, parent, TapeMetrics.At(level));
                if (total <= NightField || level >= 4) return total;
                _zRig.gameObject.SetActive(false);
                Destroy(_zRig.gameObject);
                _billCounts.Clear();
                _billStars.Clear();
                _billStamp = null; _billStampInk = null;
            }
        }

        private float LayTape(TycoonRun run, RectTransform parent, TapeMetrics m)
        {
            _tillCount = -1;
            _gradeFills.Clear();
            _rateFigures.Clear();
            _ringMask = _penMask = null;

            _zRig = NewRect("ZRig", parent);
            _zRig.anchorMin = _zRig.anchorMax = new Vector2(0.5f, 0.5f);
            _zRig.pivot = new Vector2(0.5f, 1f);
            _zTape = TopRect("ZTape", _zRig, TapeW, 0f, 0f);
            _zTape.gameObject.AddComponent<RectMask2D>();
            _zPaper = TopRect("ZPaper", _zTape, TapeW, 0f, 0f);
            _zPaperImg = _zPaper.gameObject.AddComponent<Image>();
            _zPaperImg.raycastTarget = false;
            var body = ChromeArt.TapeBody();
            if (body != null)
            {
                // The band tiles at a whole 3x, the torn top prints once (its border), as long as the night is.
                _zPaperImg.sprite = body;
                _zPaperImg.type = Image.Type.Tiled;
                _zPaperImg.pixelsPerUnitMultiplier = 1f / 3f;
            }
            else { _zPaperImg.color = UITheme.Cream[4]; Frame(_zPaper, 2f, UITheme.Cream[2]); }
            _zRows = TopRect("ZRows", _zTape, TapeW, 0f, 0f);

            // ── the print, top to bottom (the counts register in this order: it is the order they count in) ──
            float y = 12f + 4f;                      // the stock's torn top, and a margin
            y = TapeTitle(y);
            y += 4f;
            y = TapeCountsLine(run, y);
            y = TapeRule(y, m.Rule);

            y = TapeHead(y, "Row.in", UIText.T("dayend.bill.took_in"), BillGain, m.Head);
            y = TapeRow(y, "Row.in.sales", UIText.T("dayend.bill.sales"), run.DaySales, "", TapeInk, "sales", m);
            y = TapeRow(y, "Row.in.tips", UIText.T("dayend.bill.tips"), run.DayTips, "", TapeInk, "tips", m);
            // THE BONUS IS TWO PEOPLE'S MONEY (2026-09-28): her pay for a job, whenever it landed, and the state's thanks
            // for the door - each on its own row, and the rows add up to the subtotal under them.
            if (run.DayQuestPaid > 0)
            {
                string who = HostessWho();
                if (string.IsNullOrEmpty(who)) who = UIText.T("dayend.host.fallback");
                y = TapeRow(y, "Row.in.job", UIText.T("dayend.bill.quest", ("who", who)), run.DayQuestPaid, "",
                    TapeInk, "job", m);
            }
            int thanks = run.DayBonus - run.DayQuestPaid;
            if (thanks > 0)
                y = TapeRow(y, "Row.in.thanks", UIText.N("dayend.bill.thanks", run.RightKicks), thanks, "",
                    TapeInk, "thanks", m);
            y = TapeSub(y, "Row.in.sub", run.DayIncome, "", BillGain, m);
            y += m.Gap;

            var loss = UITheme.ViceRed[1];
            y = TapeHead(y, "Row.out", UIText.T("dayend.bill.paid_out"), BillLoss, m.Head);
            // What the night bought, then what it was charged, then the rent.
            if (run.DayStock > 0)
                y = TapeRow(y, "Row.out.stock", UIText.T("dayend.bill.stock"), run.DayStock, "-", loss, "stock", m);
            if (run.DayUpgrades > 0)
                y = TapeRow(y, "Row.out.shop", UIText.T("dayend.bill.shop"), run.DayUpgrades, "-", loss, "shop", m);
            if (run.DayFines > 0)
                y = TapeRow(y, "Row.out.fines", UIText.T("dayend.bill.fines", ("reason", FineReason(run))), run.DayFines,
                    "-", loss, "fine", m);
            // WALK-OUTS (2026-09-23 billed, 2026-09-28 printed): paid for drinks that never came. The old slip carried
            // the fee in its subtotal and printed no row for it, so its rows did not add up.
            if (run.DayWalkOutFees > 0)
                y = TapeRow(y, "Row.out.walkouts", UIText.N("dayend.tape.walkouts", run.DayWalkOuts), run.DayWalkOutFees,
                    "-", loss, "walkout", m);
            y = TapeRow(y, "Row.out.rent", UIText.T("dayend.bill.rent"), run.DayRent, "-", loss, "rent", m);
            y = TapeSub(y, "Row.out.sub", run.DayExpenses, "-", BillLoss, m);

            y = TapeDoubleRule(y);
            int net = run.DayIncome - run.DayExpenses;
            y = TapeNet(y, net);
            y = TapeTill(y, run.Money);
            y = TapeRule(y, m.Rule);

            y = TapeRatings(run, y);
            y += 8f;                                 // the ring's strips reach three below their row
            y = TapeStars(run, y, out float figY);
            TapePenLead(_ringBottom, figY);
            y += 12f;                                // the foot: blank paper, torn at the tear
            _lBody = Mathf.Ceil(y / 12f) * 12f;      // the tile ends on a whole art row, the edge on the grid
            _lTotal = _lBody + TapeStubH;

            _zPaper.sizeDelta = new Vector2(TapeW, _lBody);
            _zRows.sizeDelta = new Vector2(TapeW, _lBody);
            _zRig.sizeDelta = new Vector2(TapeW, _lTotal);
            TapeCounterfoil(run);

            _zFoot = TopRect("ZFoot", _zRig, TapeW, 12f, 0f);
            var foot = _zFoot.gameObject.AddComponent<Image>();
            foot.sprite = ChromeArt.TapeFoot();
            foot.raycastTarget = false;
            if (foot.sprite == null) foot.color = UITheme.Cream[4];
            _zFoot.gameObject.SetActive(false);

            LandTape();
            return _lTotal;
        }

        /// <summary>A rect hung by its top edge from its parent's top, centred across it.</summary>
        private static RectTransform TopRect(string name, RectTransform parent, float w, float h, float y)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(0f, -y);
            return rt;
        }

        /// <summary>One printed line of the tape: the print column's width, hung at <paramref name="y"/> under the top.</summary>
        private RectTransform TapeLine(string name, float y, float h) => TopRect(name, _zRows, TapePrint, h, y);

        private float TapeTitle(float y)
        {
            // PRINTED, not banded: a receipt is one ink on one paper. At 16 now, so NET is the one biggest figure.
            var row = TapeLine("Head", y, 28f);
            var t = NewText("T", row, _display, 16, TextAnchor.MiddleCenter, TapeInk);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = UIText.T("build.bill.head");
            return y + 28f;
        }

        /// <summary>The counts line, first to count: the two numbers the books file (TycoonRun.NightTally).</summary>
        private float TapeCountsLine(TycoonRun run, float y)
        {
            var row = TapeLine("Counts", y, 20f);
            var t = NewText("T", row, BillSmallFont, BillSmallSize, TextAnchor.MiddleCenter, TapeQuiet);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var (served, walked) = run.NightTally();
            System.Action<float> count = k => t.text = k <= 0f ? ""
                : BillWords(UIText.T("dayend.score.counts", ("served", Mathf.RoundToInt(served * k)),
                    ("walked", Mathf.RoundToInt(walked * k))));
            count(1f);
            _billCounts.Add(count);
            return y + 20f;
        }

        /// <summary>A printed rule: dashed, two on and four off, in the paper's own quiet ink, across the column.</summary>
        private float TapeRule(float y, float h)
        {
            var rule = TapeLine("Rule", y + Mathf.Floor(h * 0.5f), 1f);
            var img = rule.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.DashRule();
            img.type = Image.Type.Tiled;
            img.color = UITheme.Cream[2];
            img.raycastTarget = false;
            return y + h;
        }

        /// <summary>The rule over the total: two hairlines of the ink.</summary>
        private float TapeDoubleRule(float y)
        {
            foreach (float at in new[] { 4f, 7f })
            {
                var line = TapeLine("Rule2", y + at, 1f);
                var img = line.gameObject.AddComponent<Image>();
                img.color = TapeInk;
                img.raycastTarget = false;
            }
            return y + 12f;
        }

        /// <summary>A block's head (seventh list: "başlıkları kalın ... farklı renkte"): the heavy face, wide-tracked
        /// caps, in the colour of the money under it, with a short rule of the same ink under the word.</summary>
        private float TapeHead(float y, string name, string text, Color ink, float h)
        {
            var row = TapeLine(name, y, h);
            var head = NewText("H", row, _shop != null ? _shop : _display, 16, TextAnchor.MiddleLeft, ink);
            Stretch(head.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.verticalOverflow = VerticalWrapMode.Overflow;
            var sb = new System.Text.StringBuilder();
            foreach (var ch in text ?? "") { sb.Append(ch); if (ch != ' ' && ch <= 'ɏ') sb.Append(' '); }
            head.text = sb.ToString().TrimEnd(' ');
            var rule = NewRect("U", row);
            rule.anchorMin = rule.anchorMax = new Vector2(0f, 0.5f);
            rule.pivot = new Vector2(0f, 1f);
            rule.sizeDelta = new Vector2(Mathf.Min(Mathf.Round(head.preferredWidth), 200f), 2f);
            rule.anchoredPosition = new Vector2(0f, -9f);
            var ri = rule.gameObject.AddComponent<Image>();
            ri.color = new Color(ink.r, ink.g, ink.b, 0.8f);
            ri.raycastTarget = false;
            return y + h;
        }

        /// <summary>
        /// One item: its mark, its word and its figure. The word is the ink's, the mark the row's (red on what went
        /// out) and the money its own colour. NO WORD RUNS INTO ITS FIGURE (2026-09-22, seventh list): a word too long
        /// for its room drops to the small face, and one too long even then wraps and the row grows.
        /// </summary>
        private float TapeRow(float y, string name, string label, int amount, string sign, Color markInk, string mark,
            TapeMetrics m)
        {
            float h = m.Item;
            var row = TapeLine(name, y, h);
            var art = ChromeArt.Mark(mark);
            if (art != null)
            {
                var icon = NewRect("M", row);
                Place(icon, new Vector2(0f, 0.5f), new Vector2(16f, 16f), Vector2.zero);
                var ii = icon.gameObject.AddComponent<Image>();
                ii.sprite = art; ii.color = markInk; ii.raycastTarget = false;
            }
            var l = NewText("L", row, _body, BillWordSize(m.ItemSize), TextAnchor.MiddleLeft, TapeInk);
            Stretch(l.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 0f), Vector2.zero);
            l.horizontalOverflow = HorizontalWrapMode.Overflow;
            l.verticalOverflow = VerticalWrapMode.Overflow;
            l.text = BillWords(label);

            float figW = TapeFigure(row, amount, sign, sign == "-" ? BillLoss : BillGain, _body, m.ItemSize, h);
            float room = TapePrint - 24f - figW - 14f;
            l.rectTransform.offsetMax = new Vector2(-(figW + 14f), 0f);
            if (l.preferredWidth > room && l.fontSize > BillSmallSize)
            {
                l.font = BillSmallFont;
                l.fontSize = BillSmallSize;
            }
            if (l.preferredWidth > room)
            {
                l.horizontalOverflow = HorizontalWrapMode.Wrap;
                int lines = Mathf.CeilToInt(l.preferredWidth / Mathf.Max(1f, room));
                h = Mathf.Max(h, lines * 18f + 6f);
                row.sizeDelta = new Vector2(TapePrint, h);
            }
            return y + h;
        }

        /// <summary>A block's subtotal: a short rule over the figures it adds up, and the figure alone on the right. No
        /// label - the block above it is the label (2026-08-26, cut and restored the same day).</summary>
        private float TapeSub(float y, string name, int amount, string sign, Color ink, TapeMetrics m)
        {
            var row = TapeLine(name, y, m.Sub);
            var rule = NewRect("R", row);
            rule.anchorMin = new Vector2(0.62f, 1f); rule.anchorMax = new Vector2(1f, 1f);
            rule.pivot = new Vector2(0.5f, 1f);
            rule.sizeDelta = new Vector2(0f, 1f);
            rule.anchoredPosition = Vector2.zero;
            var ri = rule.gameObject.AddComponent<Image>();
            ri.color = new Color(ink.r, ink.g, ink.b, 0.45f);
            ri.raycastTarget = false;
            TapeFigure(row, amount, sign, ink, _body, m.ItemSize, m.Sub);
            return y + m.Sub;
        }

        /// <summary>NET: the one biggest figure on the screen - the heavy face at 24, its sign always, on the ink's 7 %
        /// wash (2026-09-04, "daha dikkat çekici").</summary>
        private float TapeNet(float y, int net)
        {
            const float H = 32f;
            var row = TapeLine("Row.net", y, H);
            var wash = NewRect("Wash", row);
            Stretch(wash, Vector2.zero, Vector2.one, new Vector2(-6f, 1f), new Vector2(6f, -1f));
            var wi = wash.gameObject.AddComponent<Image>();
            wi.color = new Color(TapeInk.r, TapeInk.g, TapeInk.b, 0.07f);
            wi.raycastTarget = false;
            TapeHeavyLabel(row, "net", UIText.T("dayend.bill.net"));
            TapeFigure(row, net, net >= 0 ? "+" : "-", net >= 0 ? BillGain : BillLoss, _display, 24, H);
            return y + H;
        }

        /// <summary>TILL: what is in it now, in the ink - red, and signed, only below nothing.</summary>
        private float TapeTill(float y, int money)
        {
            const float H = 26f;
            var row = TapeLine("Row.till", y, H);
            TapeHeavyLabel(row, "till", UIText.T("dayend.bill.till"));
            _tillCount = _billCounts.Count;
            TapeFigure(row, money, money < 0 ? "-" : "", money < 0 ? BillLoss : TapeInk, _body, 24, H);
            return y + H;
        }

        /// <summary>The two summary words: bold only here (2026-08-11, "çok fazla kalın yazı kullanma").</summary>
        private void TapeHeavyLabel(RectTransform row, string mark, string word)
        {
            var art = ChromeArt.Mark(mark);
            if (art != null)
            {
                var icon = NewRect("M", row);
                Place(icon, new Vector2(0f, 0.5f), new Vector2(16f, 16f), Vector2.zero);
                var ii = icon.gameObject.AddComponent<Image>();
                ii.sprite = art; ii.color = TapeInk; ii.raycastTarget = false;
            }
            var l = NewText("L", row, _display, 16, TextAnchor.MiddleLeft, TapeInk);
            Stretch(l.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 0f), Vector2.zero);
            l.horizontalOverflow = HorizontalWrapMode.Overflow;
            l.verticalOverflow = VerticalWrapMode.Overflow;
            l.text = word;
        }

        /// <summary>
        /// A FIGURE WITH THE DOLLAR DRAWN (2026-09-04): the digits alone in a Text named V, hard right in the column, the
        /// drawn cash mark (C) left of them and the sign (S) left of that - laid out by MEASURING the digits at their
        /// final value, and re-placed as they count. A figure that has not started counting shows nothing, not "$0".
        /// Returns its width at the final value.
        /// </summary>
        private float TapeFigure(RectTransform row, int amount, string sign, Color ink, Font font, int size, float h)
        {
            const float Mark = 16f, Gap = 3f;
            font = BillDigits(font);   // the house face's digits, in every language (eighth list)
            var digits = NewText("V", row, font, size, TextAnchor.MiddleRight, ink);
            Place(digits.rectTransform, new Vector2(1f, 0.5f), new Vector2(160f, h), Vector2.zero);
            digits.horizontalOverflow = HorizontalWrapMode.Overflow;
            digits.verticalOverflow = VerticalWrapMode.Overflow;
            int target = Mathf.Abs(amount);
            digits.text = target.ToString();
            float digitsW = digits.preferredWidth;

            var cash = NewRect("C", row);
            Place(cash, new Vector2(1f, 0.5f), new Vector2(Mark, Mark), new Vector2(-Mathf.Round(digitsW + Gap), 0f));
            var ci = cash.gameObject.AddComponent<Image>();
            ci.sprite = ChromeArt.Mark("cash");
            ci.color = ink; ci.raycastTarget = false;

            Text s = null;
            if (!string.IsNullOrEmpty(sign))
            {
                s = NewText("S", row, font, size, TextAnchor.MiddleRight, ink);
                Place(s.rectTransform, new Vector2(1f, 0.5f), new Vector2(40f, h),
                    new Vector2(-Mathf.Round(digitsW + Gap + Mark + Gap), 0f));
                s.horizontalOverflow = HorizontalWrapMode.Overflow;
                s.verticalOverflow = VerticalWrapMode.Overflow;
                s.text = sign;
            }
            float width = digitsW + Gap + Mark + (s != null ? Gap + s.preferredWidth : 0f);
            _billCounts.Add(k =>
            {
                bool on = k > 0f;
                digits.text = on ? Mathf.RoundToInt(target * k).ToString() : "";
                if (cash.gameObject.activeSelf != on) cash.gameObject.SetActive(on);
                if (s != null && s.gameObject.activeSelf != on) s.gameObject.SetActive(on);
                if (!on) return;
                float w = digits.preferredWidth;
                cash.anchoredPosition = new Vector2(-Mathf.Round(w + Gap), 0f);
                if (s != null) s.rectTransform.anchoredPosition = new Vector2(-Mathf.Round(w + Gap + Mark + Gap), 0f);
            });
            return width;
        }

        // ── the grade ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE TWO THE NIGHT WAS MADE OF (GDD 27 §6): SERVICE in hearts, COMFORT in medals, two decimals each in one
        /// figure column - and the lower of them, the one the night is filed at, ringed in pen (a tie rings SERVICE,
        /// today's rule). The ring and the pen's run to the stars say which one decided the night without a word to
        /// translate.
        /// </summary>
        private float TapeRatings(TycoonRun run, float y)
        {
            double service = run.ServiceTonight, comfort = run.ComfortTonight;
            bool roomFiled = comfort < service - 1e-9;
            var svc = TapeLine("Rate.service", y, 32f);
            var cmf = TapeLine("Rate.comfort", y + 32f, 32f);
            var svcLabel = RateLabel(svc, "dayend.stand.service");
            var cmfLabel = RateLabel(cmf, "dayend.stand.comfort");
            float labelCol = Mathf.Ceil(Mathf.Max(svcLabel.preferredWidth, cmfLabel.preferredWidth)) + 12f;
            RateSlots(svc, labelCol, true, service);
            RateSlots(cmf, labelCol, false, comfort);
            RateFigure(svc, service);
            RateFigure(cmf, comfort);
            TapeRing(roomFiled ? y + 32f : y, 32f);
            return y + 64f;
        }

        private Text RateLabel(RectTransform row, string key)
        {
            var l = NewText("L", row, BillSmallFont, BillSmallSize, TextAnchor.MiddleLeft, TapeInk);
            Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(220f, 32f), Vector2.zero);
            l.horizontalOverflow = HorizontalWrapMode.Overflow;
            l.verticalOverflow = VerticalWrapMode.Overflow;
            l.text = BillWords(UIText.T(key));
            return l;
        }

        /// <summary>Five slots at a pitch of 30: the heart's 12 at a whole 2x, the medal's 32 at 1x, each a socket with
        /// its lit icon filled over it to the reading.</summary>
        private void RateSlots(RectTransform row, float labelCol, bool heart, double value)
        {
            float px = heart ? 24f : 32f;
            var socket = heart ? ItemArt.Heart(false, px) : ItemArt.Medal(false, px);
            var lit = heart ? ItemArt.Heart(true, px) : ItemArt.Medal(true, px);
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var cell = NewRect("S" + i, row);
                cell.anchorMin = cell.anchorMax = new Vector2(0f, 0.5f);
                cell.pivot = new Vector2(0.5f, 0.5f);
                cell.sizeDelta = new Vector2(px, px);
                cell.anchoredPosition = new Vector2(labelCol + 15f + 30f * i, 0f);
                var back = cell.gameObject.AddComponent<Image>();
                back.sprite = socket; back.preserveAspect = true; back.raycastTarget = false;
                var fill = NewRect("F", cell);
                Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var fi = fill.gameObject.AddComponent<Image>();
                fi.sprite = lit; fi.preserveAspect = true; fi.raycastTarget = false;
                fi.type = Image.Type.Filled;
                fi.fillMethod = Image.FillMethod.Horizontal;
                fi.fillOrigin = (int)Image.OriginHorizontal.Left;
                fi.fillAmount = Mathf.Clamp01((float)value - i);
                _gradeFills.Add((fi, (float)value, i));
            }
        }

        private void RateFigure(RectTransform row, double value)
        {
            // 64 wide, right-aligned: "0.00" in the display face at 16 measures 64 (the old board's 56 box let COMFORT
            // run under its medal, §9.3).
            var v = NewText("V", row, _display, 16, TextAnchor.MiddleRight, TapeInk);
            Place(v.rectTransform, new Vector2(1f, 0.5f), new Vector2(64f, 20f), Vector2.zero);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            v.verticalOverflow = VerticalWrapMode.Overflow;
            v.text = value.ToString("0.00");
            _rateFigures.Add((v, value));
        }

        /// <summary>The pen's ring round the filed row: 404 wide, three over and under the row, revealed left to right.</summary>
        private void TapeRing(float rowTop, float rowH)
        {
            float top = rowTop - 3f, h = rowH + 6f;
            _ringMask = NewRect("Ring", _zRows);
            _ringMask.anchorMin = _ringMask.anchorMax = new Vector2(0.5f, 1f);
            _ringMask.pivot = new Vector2(0f, 1f);
            _ringMask.anchoredPosition = new Vector2(-202f, -top);
            _ringMask.sizeDelta = new Vector2(404f, h);
            _ringMask.gameObject.AddComponent<RectMask2D>();
            _ringW = 404f;
            _ringBottom = top + h;
            var pen = NewRect("Pen", _ringMask);
            pen.anchorMin = pen.anchorMax = pen.pivot = new Vector2(0f, 1f);
            pen.sizeDelta = new Vector2(404f, h);
            pen.anchoredPosition = Vector2.zero;
            var img = pen.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.PenRing();
            img.type = Image.Type.Sliced;
            img.color = UITheme.ViceRed[2];
            img.raycastTarget = false;
            if (img.sprite == null) img.enabled = false;
        }

        /// <summary>
        /// THE PEN RUNS ON: a short jog out of the ring's lower-right corner, a line down the tape's right margin (outside
        /// the print column, so it never crosses a figure) and a hooked head pointing at the stars' figure - the link
        /// from the filed row to the stars, drawn rather than inferred.
        /// </summary>
        private void TapePenLead(float ringBottom, float figY)
        {
            float top = ringBottom - 6f, bottom = figY + 4f;
            _penH = Mathf.Max(8f, bottom - top);
            _penMask = NewRect("Lead", _zRows);
            _penMask.anchorMin = _penMask.anchorMax = new Vector2(0.5f, 1f);
            _penMask.pivot = new Vector2(0f, 1f);
            _penMask.anchoredPosition = new Vector2(198f, -top);
            _penMask.sizeDelta = new Vector2(12f, _penH);
            _penMask.gameObject.AddComponent<RectMask2D>();
            var ink = UITheme.ViceRed[2];
            PenPiece("Jog", 2f, 1f, 8f, 2f, ink, null);
            PenPiece("Down", 8f, 1f, 2f, figY - top, ink, null);
            PenPiece("Head", 2f, figY - 4f - top, 8f, 8f, ink, ChromeArt.PenArrowHead());
        }

        private void PenPiece(string name, float x, float y, float w, float h, Color ink, Sprite art)
        {
            var rt = NewRect(name, _penMask);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            var img = rt.gameObject.AddComponent<Image>();
            if (art != null) img.sprite = art;
            img.color = ink;
            img.raycastTarget = false;
        }

        /// <summary>The grade at <paramref name="fill"/> of its fill (the icons and the ring, together) and
        /// <paramref name="lead"/> of the pen's run down the margin.</summary>
        private void SetGrade(float fill, float lead)
        {
            foreach (var (img, value, slot) in _gradeFills)
                if (img != null) img.fillAmount = Mathf.Clamp01(value * fill - slot);
            foreach (var (text, value) in _rateFigures)
                if (text != null) text.text = fill <= 0f ? "" : (value * fill).ToString("0.00");
            if (_ringMask != null) _ringMask.sizeDelta = new Vector2(Mathf.Round(_ringW * fill), _ringMask.sizeDelta.y);
            if (_penMask != null) _penMask.sizeDelta = new Vector2(_penMask.sizeDelta.x, Mathf.Round(_penH * lead));
        }

        // ── the stars and the stamp ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE STARS THE NIGHT IS FILED AT (Q1: the lower of SERVICE and COMFORT, the number the book files): five cells of
        /// 36 at a pitch of 40, the socket row, the lit row under a mask cut to the reading (a half star is the mask
        /// cutting the last one down the middle), and the figure hard right. The zone is as tall as the stamp's rotated
        /// footprint when a stamp will be struck across it, so the stamp never reaches a figure (§9.4).
        /// </summary>
        private float TapeStars(TycoonRun run, float y, out float figY)
        {
            float zoneH = _stampKind == StampKind.Record ? 100f : _stampKind == StampKind.Disgrace ? 96f : 52f;
            var host = TapeLine("Stars", y, zoneH);
            const float Cell = 36f, Pitch = 40f, Left = -154f;
            double tonight = run.TonightStars;
            float frac = Mathf.Clamp01((float)(tonight / BarRating.MaxStars));
            var socket = ItemArt.Star(false, Cell);
            var litArt = ItemArt.Star(true, Cell);
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var dim = NewRect("D" + i, host);
                Place(dim, new Vector2(0.5f, 0.5f), new Vector2(Cell, Cell), new Vector2(Left + Cell * 0.5f + Pitch * i, 0f));
                var di = dim.gameObject.AddComponent<Image>();
                di.sprite = socket; di.preserveAspect = true; di.raycastTarget = false;
                di.color = new Color(1f, 1f, 1f, 0.85f);
            }
            var lit = NewRect("Lit", host);
            lit.anchorMin = lit.anchorMax = new Vector2(0.5f, 0.5f);
            lit.pivot = new Vector2(0f, 0.5f);
            lit.anchoredPosition = new Vector2(Left, 0f);
            // TALLER THAN THE ROW (2026-08-11): the mask cuts a half star in half, and the stars FALL into place.
            lit.sizeDelta = new Vector2(Mathf.Round((5f * Pitch - (Pitch - Cell)) * frac), StarFallH * 2f);
            lit.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var on = NewRect("L" + i, lit);
                on.anchorMin = on.anchorMax = new Vector2(0f, 0.5f);
                on.pivot = new Vector2(0.5f, 0.5f);
                on.sizeDelta = new Vector2(Cell, Cell);
                var oi = on.gameObject.AddComponent<Image>();
                oi.sprite = litArt; oi.preserveAspect = true; oi.raycastTarget = false;
                // THE STARS ARE NOUGHT FIRST (the author: "yıldızlar ilk 0'dır"): above their places, clear, until they drop.
                bool placed = Motion.Reduced;
                on.anchoredPosition = new Vector2(Cell * 0.5f + Pitch * i, placed ? 0f : StarFallH);
                oi.color = new Color(1f, 1f, 1f, placed ? 1f : 0f);
                _billStars.Add(on);
            }
            var v = NewText("V", host, _display, 16, TextAnchor.MiddleRight, TapeInk);
            Place(v.rectTransform, new Vector2(1f, 0.5f), new Vector2(64f, 20f), Vector2.zero);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            v.verticalOverflow = VerticalWrapMode.Overflow;
            _starFigure = v;
            _starFigureValue = tonight;
            SetStarFigure(1f);

            figY = y + zoneH * 0.5f;
            TapeStamp(figY);
            return y + zoneH;
        }

        private void SetStarFigure(float k)
        {
            if (_starFigure == null) return;
            _starFigure.text = k <= 0f ? "" : (_starFigureValue * k).ToString("0.00");
        }

        /// <summary>
        /// THE STAMP, today's construction moved unchanged (2026-08-11; seventh list "daha kalın ve daha canlı"): a plate
        /// at the ink's 16 %, a five-unit rubber edge, a two-unit inner ring, the heaviest face at 24 - centred on the
        /// five cells and on their row, on the rig (it arrives at 3.4x, so it lives outside the tape's mask). Parked
        /// hidden: it is not on the paper until it is struck (2026-08-19).
        /// </summary>
        private void TapeStamp(float centreY)
        {
            _billStamp = NewRect("Stamp", _zRig);
            _billStamp.anchorMin = _billStamp.anchorMax = new Vector2(0.5f, 1f);
            _billStamp.pivot = new Vector2(0.5f, 0.5f);
            _billStamp.sizeDelta = new Vector2(256f, 54f);
            _billStamp.anchoredPosition = new Vector2(-56f, -centreY);
            var red = UITheme.ViceRed[2];
            var plate = _billStamp.gameObject.AddComponent<Image>();
            plate.color = new Color(red.r, red.g, red.b, 0.16f);
            plate.raycastTarget = false;
            Frame(_billStamp, 5f, new Color(red.r, red.g, red.b, 0.95f));
            var inner = NewRect("Inner", _billStamp);
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            Frame(inner, 2f, new Color(red.r, red.g, red.b, 0.95f));
            _billStampInk = NewText("W", _billStamp, _shop != null ? _shop : _display, 24, TextAnchor.MiddleCenter,
                new Color(red.r, red.g, red.b, 0.92f));
            Stretch(_billStampInk.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
            _billStampInk.horizontalOverflow = HorizontalWrapMode.Overflow;
            _billStampInk.verticalOverflow = VerticalWrapMode.Overflow;
            _billStampInk.text = UIText.T("dayend.stamp.disgrace");
            _billStamp.gameObject.SetActive(false);
        }

        // ── the counterfoil ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE COUNTERFOIL, the tape's last piece: the perforation over its top, and two lines in its left half - the
        /// night and the Z number - printed eight in from the edge, the filed tickets' own margin, so tonight's lines up
        /// with last night's once it hangs. It carries no money and no stars: those are on the tape beside it, and the
        /// book files them at dawn (GDD 16 §6.5: every fact once).
        /// </summary>
        private void TapeCounterfoil(TycoonRun run)
        {
            _zStub = TopRect("ZStub", _zTape, TapeW, TapeStubH, _lBody);
            _zStubImg = _zStub.gameObject.AddComponent<Image>();
            _zStubImg.raycastTarget = false;
            var art = ChromeArt.TapeCounterfoil(false);
            if (art != null) _zStubImg.sprite = art; else _zStubImg.color = UITheme.Cream[4];

            _zPerf = TopRect("Perf", _zStub, 444f, 2f, 5f);
            var perf = _zPerf.gameObject.AddComponent<Image>();
            perf.sprite = ChromeArt.Perforation();
            perf.type = Image.Type.Tiled;
            perf.color = UITheme.Night[0];
            perf.raycastTarget = false;

            // the right half, which folds behind the left on the way to the hook
            _zStubFold = NewRect("Fold", _zStub);
            _zStubFold.anchorMin = _zStubFold.anchorMax = new Vector2(1f, 0.5f);
            _zStubFold.pivot = new Vector2(0f, 0.5f);
            _zStubFold.sizeDelta = new Vector2(TicketW, TapeStubH);
            _zStubFold.anchoredPosition = Vector2.zero;
            var fold = _zStubFold.gameObject.AddComponent<Image>();
            fold.raycastTarget = false;
            var right = ChromeArt.TapeCounterfoilHalf(true);
            if (right != null) fold.sprite = right; else fold.color = UITheme.Cream[4];
            _zStubFold.gameObject.SetActive(false);

            var night = NewText("Day", _zStub, _display, 16, TextAnchor.MiddleLeft, TapeInk);
            TicketLine(night.rectTransform, 8f, -24f);
            night.text = UIText.T(BarCalendar.WeekColumnLines[(int)BarCalendar.NightOf(run.Day)]);
            var z = NewText("Z", _zStub, _body, 16, TextAnchor.MiddleLeft, TapeQuiet);
            TicketLine(z.rectTransform, 8f, -44f);
            z.text = UIText.T("dayend.tape.z", ("n", run.Day.ToString("000")));
        }

        /// <summary>A line of print on a ticket, hung from its top-left.</summary>
        private static void TicketLine(RectTransform rt, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(196f, 20f);
            rt.anchoredPosition = new Vector2(x, y);
            var t = rt.GetComponent<Text>();
            if (t != null) { t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; }
        }

        // ── the unroll and the tear ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>How much of the tape shows, and its torn foot riding the edge as it comes.</summary>
        private void SetTapeLength(float h)
        {
            if (_zTape == null) return;
            _zTape.sizeDelta = new Vector2(TapeW, h);
            if (_zFoot == null) return;
            bool riding = h > 0f && h < _lTotal;
            if (_zFoot.gameObject.activeSelf != riding) _zFoot.gameObject.SetActive(riding);
            _zFoot.anchoredPosition = new Vector2(0f, -h);
        }

        /// <summary>The whole tape down, its own foot (the counterfoil's) the edge, the rig at home.</summary>
        private void LandTape()
        {
            if (_zTape == null) return;
            _zTape.sizeDelta = new Vector2(TapeW, _lTotal);
            if (_zFoot != null) _zFoot.gameObject.SetActive(false);
            if (_zRig != null) _zRig.anchoredPosition = _zRigHome;
        }

        /// <summary>
        /// One frame of the paper coming in (2026-08-11: "much slower still", and "at the very bottom it should bounce a
        /// little"): fed down from the top at the till's near-even rate over <see cref="PaperLand"/>'s feed, its torn
        /// foot riding the edge until the counterfoil's own foot arrives, then the rig dips twice on the landing. The
        /// side columns come in with it, and the critics count over their last stretch. True once all of it is in.
        /// </summary>
        private bool StepUnroll(float dt)
        {
            _tapeT += dt;
            float k = Mathf.Clamp01(_tapeT / SlipFeed);
            const float Feed = 0.86f;
            SetTapeLength(Mathf.Round((_lTotal - 12f) * (k < Feed ? PaperLand(k) : 1f)));
            float dip = 0f;
            if (k >= Feed)
            {
                float u = (k - Feed) / (1f - Feed);
                dip = Mathf.Round(TapeDip * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * u)) * (1f - u));
            }
            if (_zRig != null) _zRig.anchoredPosition = _zRigHome - new Vector2(0f, dip);
            SetColumnsIn(Mathf.Clamp01(_tapeT / BoardsIn));
            float c = Mathf.Clamp01((_tapeT - (BoardsIn - CriticCount)) / CriticCount);
            foreach (var count in _criticCounts) count(c);
            if (k < 1f || _tapeT < BoardsIn) return false;
            LandTape();
            SetColumnsIn(1f);
            return true;
        }

        private void BeginTear(TycoonRun run)
        {
            _show = NightBeat.Tear; _showT = 0f;
            Sfx.Play("page_turn", 0.5f);
            TearOff();
        }

        /// <summary>
        /// THE TEAR: the perforation goes, the tape's foot is the stock's own torn foot, and the counterfoil leaves the
        /// tape for the room - its left half the ticket, its right half about to fold behind it.
        /// </summary>
        private void TearOff()
        {
            var torn = ChromeArt.TapeBody(true);
            if (torn != null && _zPaperImg != null) _zPaperImg.sprite = torn;
            if (_zPerf != null) _zPerf.gameObject.SetActive(false);
            if (_zFoot != null) _zFoot.gameObject.SetActive(false);
            if (_zStub == null || _nightSheet == null) return;
            _zStub.SetParent(_nightSheet, false);
            _zStub.anchorMin = _zStub.anchorMax = new Vector2(0.5f, 0.5f);
            _zStub.pivot = new Vector2(0.5f, 0.5f);
            _zStub.sizeDelta = new Vector2(TicketW, TapeStubH);
            _zStub.localRotation = Quaternion.identity;
            _stubFrom = new Vector2(-TapeW * 0.25f, _zRigHome.y - _lBody - TapeStubH * 0.5f);
            _zStub.anchoredPosition = _stubFrom;
            var left = ChromeArt.TapeCounterfoilHalf(false);
            if (left != null && _zStubImg != null) _zStubImg.sprite = left;
            if (_zStubFold != null) { _zStubFold.gameObject.SetActive(true); _zStubFold.localScale = Vector3.one; }
            _zStub.SetAsLastSibling();
        }

        /// <summary>One frame of the counterfoil's way to the hook: it tears and drops, flies to the hook's point folding
        /// in half on the way, and rides up the shaft to tonight's slot. True when it hangs there.</summary>
        private bool StepTear(float dt)
        {
            _showT += dt;
            if (_zStub == null) return true;
            float t = _showT;
            var dropped = _stubFrom + new Vector2(0f, -8f);
            var point = HookPoint();
            if (t < TearDur)
            {
                float k = t / TearDur;
                _zStub.anchoredPosition = RoundV(Vector2.Lerp(_stubFrom, dropped, k * k));
                return false;
            }
            t -= TearDur;
            if (t < FlyDur)
            {
                _zStub.anchoredPosition = RoundV(Vector2.Lerp(dropped, point, Tweening.OutCubic(t / FlyDur)));
                if (_zStubFold != null)
                {
                    float f = Mathf.Clamp01(t / FoldDur);
                    _zStubFold.localScale = new Vector3(1f - f, 1f, 1f);
                    if (f >= 1f && _zStubFold.gameObject.activeSelf) _zStubFold.gameObject.SetActive(false);
                }
                return false;
            }
            t -= FlyDur;
            if (_zStubFold != null && _zStubFold.gameObject.activeSelf) _zStubFold.gameObject.SetActive(false);
            if (t < HangDur)
            {
                float e = OutBack(t / HangDur);
                _zStub.anchoredPosition = RoundV(Vector2.LerpUnclamped(point, SlotCentre(_tonightIndex), e));
                _zStub.localRotation = Quaternion.Euler(0f, 0f, SlotTilts[_tonightIndex] * Mathf.Clamp01(e));
                return false;
            }
            HangStub();
            Sfx.Play("click", 0.4f);
            return true;
        }

        /// <summary>Reduced motion: torn and hung in one step.</summary>
        private void TearAtOnce()
        {
            TearOff();
            HangStub();
        }

        /// <summary>The counterfoil on its slot: the ticket's own cut (the same pixels), its hole, and the cyan rim that
        /// marks tonight (GDD 16 §5: the current night is clock and information).</summary>
        private void HangStub()
        {
            if (_zStub == null || _tonightSlot == null) return;
            _zStub.SetParent(_tonightSlot, false);
            _zStub.anchorMin = _zStub.anchorMax = _zStub.pivot = new Vector2(0.5f, 0.5f);
            _zStub.anchoredPosition = Vector2.zero;
            _zStub.localRotation = Quaternion.identity;
            _zStub.localScale = Vector3.one;
            _zStub.sizeDelta = new Vector2(TicketW, TapeStubH);
            var stub = ChromeArt.TapeStub();
            if (stub != null && _zStubImg != null) _zStubImg.sprite = stub;
            if (_zStubFold != null) _zStubFold.gameObject.SetActive(false);
            TicketHole(_zStub);
            Frame(_zStub, 2f, UITheme.Cyan[3]);
        }

        // ── the stamp's blow, the tape's words where the house face is handed on, the fines' reason ──────────

        /// <summary>Dresses the stamp for what it is about to say.</summary>
        private void SetStampFace(StampKind kind)
        {
            _stampKind = kind;
            if (_billStamp == null || kind == StampKind.None) return;
            bool good = kind == StampKind.Record;
            // The truer inks (seventh list): the ramps' own saturated steps, not a faded brown-green.
            var ink = good ? UITheme.Lime[2] : UITheme.ViceRed[2];
            _billStamp.GetComponent<Image>().color = new Color(ink.r, ink.g, ink.b, 0.16f);
            foreach (var edge in _billStamp.GetComponentsInChildren<Image>(true))
                if (edge.transform != _billStamp && edge.GetComponent<Text>() == null)
                    edge.color = new Color(ink.r, ink.g, ink.b, 0.95f);
            _billStampInk.color = new Color(ink.r, ink.g, ink.b, 1f);
            if (_shop != null) _billStampInk.font = _shop;   // the heaviest face the game ships
            _billStampInk.text = UIText.T(good ? "dayend.stamp.record" : "dayend.stamp.disgrace");
            _billStamp.sizeDelta = new Vector2(good ? 288f : 256f, 54f);
        }

        /// <summary>
        /// A rubber stamp is a thing DRIVEN at the paper: it arrives huge, out of focus and
        /// crooked, and it stops dead. So it scales down hard rather than easing, and the
        /// only softness in it is after the strike — it rocks a few degrees and settles,
        /// and the paper takes the blow on the same frame the ink lands.
        /// </summary>
        private void ArmStamp()
        {
            if (_stampArmed || _billStamp == null || _stampKind == StampKind.None) return;
            _stampArmed = true;
            if (Motion.Reduced)
            {
                _billStamp.localScale = Vector3.one;
                _billStamp.localRotation = Quaternion.Euler(0, 0, -9f);
                _billStamp.gameObject.SetActive(true);
                return;
            }
            _stampT = 0f;
            // THE FIRST FRAME OF THE STRIKE IS SET HERE, not left to the step that runs
            // next frame. Arming can happen after StepStamp has already run for this frame
            // (the zero-star night arms from the beats, which are stepped last), and a stamp
            // shown at whatever pose it was left in flashes at rest for one frame before it
            // starts falling. Shown huge, crooked and unprinted, it can only fall.
            _billStamp.localScale = new Vector3(3.4f, 3.4f, 1f);
            _billStamp.localRotation = Quaternion.Euler(0, 0, -26f);
            var ink0 = _billStampInk.color;
            _billStampInk.color = new Color(ink0.r, ink0.g, ink0.b, 0f);
            _billStamp.gameObject.SetActive(true);
        }

        private void StepStamp()
        {
            if (_stampT < 0f || _billStamp == null) return;
            _stampT += Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace;
            float k = Mathf.Clamp01(_stampT / StampFall);
            float e = k * k * k;                            // gathers pace all the way down
            float scale = Mathf.Lerp(3.4f, 1f, e);
            _billStamp.localScale = new Vector3(scale, scale, 1f);
            _billStamp.localRotation = Quaternion.Euler(0, 0,
                Mathf.Lerp(-26f, -9f, e) + Mathf.Sin(k * Mathf.PI * 4f) * 3f * (1f - k));
            var c = _billStampInk.color;
            _billStampInk.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(k * 2.2f));
            if (k < 1f) return;
            _billStamp.localScale = Vector3.one;
            _billStamp.localRotation = Quaternion.Euler(0, 0, -9f);
            _stampT = -1f;
            _billShake = 1f;                                 // the paper takes it
            Sfx.Play("stamp", 0.95f);
        }

        /// <summary>
        /// THE SLIP IN A LANGUAGE THE HOUSE FACE CANNOT DRAW (2026-09-22, the author's eighth list: "fatura üstündeki
        /// türkçe fontu beğenmedim çok büyük duruyor ve üst satırdaki metinlerle çakışıyor biraz küçült ve küçük harflerde
        /// kullan (ALPER değil Alper)"). Silkscreen is a capitals face with a short cap; the face that draws Turkish, and
        /// the other languages <see cref="LanguageFonts"/> hands on, is a full-height face with a lower case, so at the
        /// slip's 24 its capitals stood half as tall again and İ's dot and Ş's cedilla reached the row above. Where the
        /// body face has been handed on, the slip's WORDS go a step down (24 to 16, 16 to 8) and into sentence case -
        /// "Satış", "Nina" - and its FIGURES stay in the house face at the size the English slip prints them: digits
        /// and the dollar are the same in every language, and so is the column they line up in.
        /// </summary>
        private bool BillHandedOn => bodyFont != null && _body != bodyFont;
        /// <summary>The slip's big words, a step down where the face is handed on (24 to 16).</summary>
        private int BillWordSize(int size) => BillHandedOn && size >= 24 ? 16 : size;
        /// <summary>The slip's small lines: the house face at 16, or the handed-on face's small line
        /// (<see cref="LanguageFonts.SmallLine"/>) - Galmuri7's 8 read as specks, its 16 too tall (r153).</summary>
        private Font BillSmallFont => BillHandedOn ? LanguageFonts.SmallLine(_body).font : _body;
        private int BillSmallSize => BillHandedOn ? LanguageFonts.SmallLine(_body).size : 16;
        /// <summary>Sentence case where the face is handed on, one clause at a time: a slip line runs clauses
        /// together with a middle dot, and each of them starts like a sentence ("Hafta 2 · Cumartesi").</summary>
        private string BillWords(string s)
        {
            if (!BillHandedOn || string.IsNullOrEmpty(s)) return s;
            var parts = s.Split('·');
            for (int i = 0; i < parts.Length; i++) parts[i] = UIText.Sentence(parts[i]);
            return string.Join("·", parts);
        }
        private Font BillDigits(Font font) => BillHandedOn && font == _body ? bodyFont : font;

        /// <summary>What the fines were for (GDD 28 §7): the label carries the reason, read
        /// off the truth behind each fined card — or UNREAD CARD for a minor served blind,
        /// which is the honest word for it.</summary>
        private static string FineReason(TycoonRun run)
        {
            int under = 0, borrowed = 0, altered = 0, copied = 0, drawn = 0, unread = 0;
            foreach (var v in run.Floor.Finished)
            {
                if (!v.Fined) continue;
                if (!v.IdInspected) { unread++; continue; }
                var truth = v.Papers;
                var kind = truth != null ? truth.Forgery : Forgery.None;
                if (kind == Forgery.Altered) altered++;
                else if (kind == Forgery.Copied) copied++;       // the cheap reprint (the eighth list)
                else if (kind == Forgery.Drawn) drawn++;         // the card drawn by hand
                else if (kind == Forgery.Borrowed) borrowed++;
                else under++;
            }
            var parts = new List<string>();
            if (under > 0) parts.Add(UIText.T("dayend.fine.under_age"));
            if (borrowed > 0) parts.Add(UIText.T("dayend.fine.borrowed"));
            if (altered > 0) parts.Add(UIText.T("dayend.fine.altered"));
            if (copied > 0) parts.Add(UIText.T("dayend.fine.copied"));
            if (drawn > 0) parts.Add(UIText.T("dayend.fine.drawn"));
            if (unread > 0) parts.Add(UIText.T("dayend.fine.unread"));
            return parts.Count == 0 ? UIText.T("dayend.fine.the_law") : string.Join(", ", parts);
        }

        private static Vector2 RoundV(Vector2 v) => new Vector2(Mathf.Round(v.x), Mathf.Round(v.y));

        /// <summary>Out-back: past its place and back into it - a ticket pushed up a spike settles, it does not stop dead.</summary>
        private static float OutBack(float k)
        {
            const float C1 = 1.70158f, C3 = C1 + 1f;
            float u = k - 1f;
            return 1f + C3 * u * u * u + C1 * u * u;
        }
    }
}
