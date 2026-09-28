using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Week: the week's bill hook (2026-09-28, the day-end rebuilt; spec §2.6, Q6/Q7/Q9).
    //
    // The author, 2026-08-25: "mevcut haftalık takvimin ilerlemesini daha profesyonelce göster". The week was a 7-row
    // table on a board; it is the till's own record now - a bill hook, each night's counterfoil pushed up its point and
    // ridden up the shaft to sit under the one before, so it reads top-down, Monday first, the way the curtain's week
    // glass and the top bar read. Every filed night's ticket carries its stars, its take and its net (the book's own
    // figures); tonight's counterfoil carries none of them - they are on the tape beside it, and the book files them at
    // dawn. The nights to come are bare shaft; Saturday keeps the marquee's promise; Sunday's shutter is on the card.
    public sealed partial class TycoonHud
    {
        /// <summary>Where the slots hang: the first 20 under the bracket's top, one every 72.</summary>
        private const float SlotTop = 20f, SlotPitch = 72f, HookPointY = 454f, WeekCardTop = 466f;

        /// <summary>Each slot hangs a little crooked about its hole, the polaroids' precedent - never twice the same.</summary>
        private static readonly float[] SlotTilts = { -2f, 1.5f, -1f, 2f, -1.5f, 1f };

        /// <summary>The hook's top line, in the night sheet's space (T), and tonight's slot.</summary>
        private float _weekTop;
        private RectTransform _tonightSlot;
        private int _tonightIndex;

        /// <summary>A slot's centre (the ticket's hole) in the night sheet's space.</summary>
        private Vector2 SlotCentre(int i) =>
            new Vector2(-438f, _weekTop - SlotTop - SlotPitch * i - TapeStubH * 0.5f);

        /// <summary>The hook's point, where a ticket is pushed on before it rides up the shaft.</summary>
        private Vector2 HookPoint() => new Vector2(-438f, _weekTop - HookPointY);

        /// <summary>
        /// The six open nights of tonight's week, Monday first: the book's row for each night already filed, tonight,
        /// and the nights to come (no book). The Sunday edition's source (next pass) - every figure it needs is filed.
        /// </summary>
        private List<(BarNight night, DayResult book, bool tonight)> WeekNights(TycoonRun run)
        {
            var nights = new List<(BarNight, DayResult, bool)>(BarCalendar.OpenNights);
            int week = BarCalendar.WeekOf(run.Day);
            for (int i = 0; i < BarCalendar.OpenNights; i++)
            {
                var night = (BarNight)i;
                int day = BarCalendar.DayOf(week, night);
                bool tonight = day == run.Day;
                nights.Add((night, day < run.Day ? BookFor(run, day) : null, tonight));
            }
            return nights;
        }

        /// <summary>The left column: the bracket, the shaft and its six slots, the point, and the week's card. Returns
        /// its height (the column's G).</summary>
        private float BuildWeek(TycoonRun run, RectTransform parent)
        {
            _weekRoot = NewRect("Week", parent);
            _weekRoot.anchorMin = _weekRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _weekRoot.pivot = new Vector2(0.5f, 1f);
            _leftGroup = _weekRoot.gameObject.AddComponent<CanvasGroup>();
            _leftGroup.blocksRaycasts = false;
            _leftGroup.interactable = false;
            int week = BarCalendar.WeekOf(run.Day);
            _tonightIndex = (int)BarCalendar.NightOf(run.Day);
            _tonightSlot = null;

            var bracket = TopRect("Bracket", _weekRoot, 32f, 12f, 0f);
            var bi = bracket.gameObject.AddComponent<Image>();
            bi.sprite = ChromeArt.HookBracket();
            bi.raycastTarget = false;
            if (bi.sprite == null) bi.color = UITheme.Graphite[3];

            // THE SHAFT: two strips of the Graphite ramp, lit and shaded - the point's own two columns.
            var shaft = TopRect("Shaft", _weekRoot, 4f, HookPointY - 4f - 12f, 12f);
            foreach (var (name, x, ink) in new[] { ("Lit", 0f, UITheme.Graphite[4]), ("Shade", 2f, UITheme.Graphite[2]) })
            {
                var strip = NewRect(name, shaft);
                strip.anchorMin = new Vector2(0f, 0f); strip.anchorMax = new Vector2(0f, 1f);
                strip.pivot = new Vector2(0f, 0.5f);
                strip.sizeDelta = new Vector2(2f, 0f);
                strip.anchoredPosition = new Vector2(x, 0f);
                var si = strip.gameObject.AddComponent<Image>();
                si.color = ink; si.raycastTarget = false;
            }

            var nights = WeekNights(run);
            for (int i = 0; i < nights.Count; i++)
            {
                var slot = NewRect("Slot" + i, _weekRoot);
                slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 1f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = new Vector2(TicketW, TapeStubH);
                slot.anchoredPosition = new Vector2(0f, -(SlotTop + SlotPitch * i + TapeStubH * 0.5f));
                slot.localRotation = Quaternion.Euler(0f, 0f, SlotTilts[i]);
                var (night, book, tonight) = nights[i];
                if (tonight) _tonightSlot = slot;
                else if (book != null) FiledTicket(slot, night, book);
            }

            // The point, hanging point down under the last slot.
            var tip = NewRect("Tip", _weekRoot);
            tip.anchorMin = tip.anchorMax = new Vector2(0.5f, 1f);
            tip.pivot = new Vector2(0.5f, 0.5f);
            tip.sizeDelta = new Vector2(4f, 8f);
            tip.anchoredPosition = new Vector2(0f, -HookPointY);
            tip.localScale = new Vector3(1f, -1f, 1f);
            var ti = tip.gameObject.AddComponent<Image>();
            ti.sprite = ChromeArt.SpikeTip();
            ti.raycastTarget = false;
            if (ti.sprite == null) ti.color = UITheme.Graphite[4];

            // SATURDAY IS PROMISED BEFORE IT ARRIVES (BarCalendar.VipNight): the marquee's star, on the bare shaft.
            int vipDay = BarCalendar.DayOf(week, BarCalendar.VipNight);
            if (vipDay > run.Day)
            {
                var vip = NewRect("Vip", _weekRoot);
                vip.anchorMin = vip.anchorMax = new Vector2(0.5f, 1f);
                vip.pivot = new Vector2(0.5f, 0.5f);
                vip.sizeDelta = new Vector2(14f, 12f);
                vip.anchoredPosition = new Vector2(0f, -(SlotTop + SlotPitch * (int)BarCalendar.VipNight + TapeStubH * 0.5f));
                var vi = vip.gameObject.AddComponent<Image>();
                vi.sprite = ItemArt.Star(true, 14f);
                vi.preserveAspect = true; vi.raycastTarget = false;
                vi.color = new Color(1f, 1f, 1f, 0.55f);
            }

            float cardH = WeekCard(run, week, nights);
            float h = WeekCardTop + cardH;
            _weekRoot.sizeDelta = new Vector2(372f, h);
            return h;
        }

        /// <summary>
        /// A NIGHT ON THE HOOK: its folded counterfoil with the hole the shaft goes through - the night and its stars on
        /// the left, what it took over what it kept on the right (2026-09-04, "haftadaki günlerdeki ciro gözükmeli"),
        /// both off the book. A stacked pair carries no coin (2026-09-07): the signs tell them apart.
        /// </summary>
        private void FiledTicket(RectTransform slot, BarNight night, DayResult book)
        {
            var img = slot.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var stub = ChromeArt.TapeStub();
            if (stub != null) img.sprite = stub; else img.color = UITheme.Cream[4];
            TicketHole(slot);

            var name = NewText("Day", slot, _display, 16, TextAnchor.MiddleLeft, TapeInk);
            TicketZone(name.rectTransform, -106f, 10f, false);
            name.text = UIText.T(BarCalendar.WeekColumnLines[(int)night]);
            StarRow(slot, new Vector2(0.5f, 0.5f), new Vector2(-106f + 40f, -11f), 14f, book.NightStars,
                Color.white, new Color(1f, 1f, 1f, 0.85f));

            var take = NewText("Take", slot, BillDigits(_body), 16, TextAnchor.MiddleRight, TapeInk);
            TicketZone(take.rectTransform, 106f, 10f, true);
            take.text = "$" + Mathf.Abs(book.Income);
            int net = book.Net;
            var kept = NewText("Net", slot, BillDigits(_body), 16, TextAnchor.MiddleRight, net >= 0 ? BillGain : BillLoss);
            TicketZone(kept.rectTransform, 106f, -11f, true);
            kept.text = (net >= 0 ? "+" : "-") + "$" + Mathf.Abs(net);
        }

        /// <summary>A ticket's print zone: 98 wide either side of the hole, eight clear of it.</summary>
        private static void TicketZone(RectTransform rt, float x, float y, bool right)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(right ? 1f : 0f, 0.5f);
            rt.sizeDelta = new Vector2(98f, 20f);
            rt.anchoredPosition = new Vector2(x, y);
            var t = rt.GetComponent<Text>();
            if (t != null) { t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; }
        }

        /// <summary>The hole the shaft passes behind, at a ticket's centre.</summary>
        private static void TicketHole(RectTransform ticket)
        {
            var hole = NewRect("Hole", ticket);
            Place(hole, new Vector2(0.5f, 0.5f), new Vector2(4f, 4f), Vector2.zero);
            var hi = hole.gameObject.AddComponent<Image>();
            hi.color = UITheme.Night[0];
            hi.raycastTarget = false;
        }

        /// <summary>
        /// THE WEEK'S CARD, under the hook: its number in the clock's cyan, Sunday's shutter (the shutter says closed, no
        /// word), what the week has taken and kept so far - tonight included, and hidden while tonight is the week's only
        /// night, when they would reprint the tape (Q9) - and the red nights' strike band closing the card.
        /// </summary>
        private float WeekCard(TycoonRun run, int week, List<(BarNight night, DayResult book, bool tonight)> nights)
        {
            var card = HousePlate(_weekRoot, "WeekCard", new Vector2(0.5f, 1f), 360f, UIText.T("dayend.week.head"),
                week.ToString("00"), UITheme.Cyan[3]);
            card.anchoredPosition = new Vector2(0f, -WeekCardTop);
            const float X = BoardPad + BoardTextInset, Row = 24f;
            float y = BoardBodyTop;

            // SUNDAY: its name and the marquee's two shutter slats.
            var sun = NewText("Sun", card, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(sun.rectTransform, new Vector2(0f, 1f), new Vector2(120f, Row), new Vector2(X, -y));
            sun.horizontalOverflow = HorizontalWrapMode.Overflow;
            sun.text = UIText.T(BarCalendar.WeekColumnLines[BarCalendar.OpenNights]);
            float slatX = X + Mathf.Ceil(sun.preferredWidth) + 12f;
            for (int s = 0; s < 2; s++)
            {
                var slat = NewRect("Shut" + s, card);
                Place(slat, new Vector2(0f, 1f), new Vector2(44f, 3f), new Vector2(slatX, -(y + 8f + 6f * s)));
                var si = slat.gameObject.AddComponent<Image>();
                si.color = UITheme.Night[3]; si.raycastTarget = false;
            }
            y += Row;

            int take = run.DayIncome, net = run.DayIncome - run.DayExpenses, scored = 1;
            foreach (var (_, book, tonight) in nights)
                if (!tonight && book != null) { take += book.Income; net += book.Net; scored++; }
            if (scored > 1)
            {
                WeekSum(card, "Taken", y, UIText.T("dayend.week.taken"), take, "", UITheme.Amber[4]);
                y += Row;
                WeekSum(card, "Net", y, UIText.T("dayend.week.net"), net, net >= 0 ? "+" : "-",
                    net >= 0 ? UITheme.Lime[4] : UITheme.ViceRed[4]);
                y += Row;
            }

            // THE RED NIGHTS (Q8: the count as today - Core advances it at dawn).
            int strikes = run.Ledger.DebtStrikes;
            if (strikes <= 0) y += 12f;
            else
            {
                y += 4f;
                string line = UIText.T("dayend.bill.strike", ("strikes", strikes), ("limit", DayLedger.StrikesToClose));
                if (strikes == DayLedger.StrikesToClose - 1) line += "\n" + UIText.T("dayend.bill.last_strike");
                var band = NewRect("Strike", card);
                band.anchorMin = band.anchorMax = new Vector2(0.5f, 1f);
                band.pivot = new Vector2(0.5f, 1f);
                var bi = band.gameObject.AddComponent<Image>();
                bi.color = UITheme.ViceRed[0]; bi.raycastTarget = false;
                var words = NewText("L", band, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[4]);
                words.rectTransform.anchorMin = words.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                words.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                words.rectTransform.sizeDelta = new Vector2(360f - 2f * X, 20f);
                words.horizontalOverflow = HorizontalWrapMode.Wrap;
                words.verticalOverflow = VerticalWrapMode.Overflow;
                words.text = line;
                float textH = Mathf.Max(20f, Mathf.Ceil(words.preferredHeight));
                words.rectTransform.sizeDelta = new Vector2(360f - 2f * X, textH);
                float bandH = Mathf.Min(68f, textH + 12f);
                band.sizeDelta = new Vector2(360f - 24f, bandH);
                band.anchoredPosition = new Vector2(0f, -y);
                y += bandH + 12f;
            }

            card.sizeDelta = new Vector2(360f, y);
            return y;
        }

        /// <summary>One of the week's sums: what it is on the left, its figure (the drawn price mark) on the right.</summary>
        private void WeekSum(RectTransform card, string name, float y, string caption, int amount, string sign, Color ink)
        {
            const float X = BoardPad + BoardTextInset;
            var label = NewText(name + "Label", card, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(200f, 24f), new Vector2(X, -y));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = caption;
            var total = NewText(name + "Total", card, _display, 16, TextAnchor.MiddleRight, ink);
            Place(total.rectTransform, new Vector2(1f, 1f), new Vector2(140f, 24f), new Vector2(-X, -y));
            total.horizontalOverflow = HorizontalWrapMode.Overflow;
            total.verticalOverflow = VerticalWrapMode.Overflow;
            CoinFigure(total, amount, sign);
        }
    }
}
