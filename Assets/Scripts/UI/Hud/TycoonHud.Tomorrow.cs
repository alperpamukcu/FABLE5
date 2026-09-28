using System;
using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Tomorrow: the night's right-hand column (2026-09-28, the day-end rebuilt; spec §2.7, Q5/Q10/Q12).
    //
    // Two plates in the house's own panel. The CRITICS: the night's best and worst, faces at their real size (the scaling
    // law), in the columns the author approved on 2026-08-11 - picture, drink, words, score. And TOMORROW: where the bar
    // stands (the climb), the next rung and what it opens, tomorrow's crowd, and her job - what it owes, how far along,
    // and when it pays. Every figure is Core's (StandingAfterTonight, RankAfterTonight, CrowdTomorrow, QuestPaysAtDawn);
    // the board draws them and works nothing out.
    public sealed partial class TycoonHud
    {
        /// <summary>The critics' scores, which count while their column comes in - before any tape figure moves.</summary>
        private readonly List<Action<float>> _criticCounts = new List<Action<float>>();

        /// <summary>
        /// THE HOUSE'S PLATE (the old boards' drawing, 2026-09-08 "bottle_card sanatına yakın"): the card's plum field
        /// in its magenta rule, sliced at 2x, with a caption and a reading on its head when it has one.
        /// </summary>
        private RectTransform HousePlate(RectTransform parent, string name, Vector2 anchor, float width, string caption,
            string reading, Color readingInk)
        {
            var root = NewRect(name, parent);
            root.anchorMin = root.anchorMax = anchor;
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(width, 100f);
            var plate = root.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.Panel();
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 0.5f;
            plate.raycastTarget = false;
            // DOWN A REGISTER (2026-09-22, seventh list: "kullanılan arkaplana uygun bir renk seçimi"): the card's plum
            // field at full strength was the brightest thing on a screen whose room is scrimmed to night, so the plates
            // shouted over the paper they stand beside. Multiplied down, the field is the night the room is in and the
            // magenta rule around it stays lit. (The one tint on this screen that is not a ramp step - GDD 16 §7's gate
            // allows it with this written reason.)
            plate.color = new Color(0.56f, 0.5f, 0.64f, 1f);
            if (caption == null) return root;

            var cap = NewText("Cap", root, _body, 16, TextAnchor.MiddleLeft, UITheme.Magenta[3]);
            Place(cap.rectTransform, new Vector2(0f, 1f), new Vector2(width - BoardPad * 2f, 20f), new Vector2(BoardPad, -10f));
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            cap.text = caption;
            var read = NewText("Reading", root, _display, 16, TextAnchor.MiddleRight, readingInk);
            Place(read.rectTransform, new Vector2(1f, 1f), new Vector2(180f, 20f), new Vector2(-BoardPad, -10f));
            read.horizontalOverflow = HorizontalWrapMode.Overflow;
            read.verticalOverflow = VerticalWrapMode.Overflow;
            read.text = reading ?? "";
            return root;
        }

        // ── the board's grid, its divider and its dark cards; the critics' reason ───────────────────────────

        /// <summary>One short honest line, from what a finished visit still carries. The
        /// judge's full verdict is transient — said in the service log, never stored — so
        /// this reads the STATE: how they left, what they were made, how it landed.</summary>
        private string CriticReason(CustomerVisit v)
        {
            // SHORT, because the row is one line now (2026-08-11). The drink is drawn beside
            // the name, so the reason no longer has to name it — it only has to say what
            // went right or wrong, in the fewest words that still sound like a person.
            if (v.State == VisitState.Kicked) return UIText.T("dayend.critic.shown_door");
            if (v.State == VisitState.StormedOff) return UIText.T("dayend.critic.walked_out");
            if (v.IdInspected && v.Served != null && v.Order.Wanted.Id != v.Served.Id)
                return UIText.T("dayend.critic.wrong_drink");
            if (v.Satisfaction >= 0.85) return UIText.T("dayend.critic.exactly_right");
            if (v.Satisfaction >= 0.55) return UIText.T("dayend.critic.fair_pour");
            return UIText.T("dayend.critic.rough_pour");
        }

        /// <summary>
        /// A PARAGRAPH BREAK IN THE FRAME'S OWN MAGENTA (seventh list: "paragrafı bölmek için kullanılan düz
        /// çizgilerin yerini pembe UI'ın çerçevesinden yapabilirsin"): a five-unit bar - highlight, body, shade,
        /// the steps the plate's rule is drawn in - run out to the frame on both sides, so the board reads as one
        /// frame divided into rooms rather than a panel with hairlines laid on it.
        /// </summary>
        /// <summary>
        /// ONE GRID FOR BOTH BOARDS (2026-09-22, the eighth list): the body runs <see cref="BoardPad"/> in from the
        /// plate's edge, a card is exactly the body's width, and every word and figure on the board - on a card or off it
        /// - stands <see cref="BoardTextInset"/> inside the body: the nights' names and the foot's captions on one left
        /// edge, every figure on one right edge. They were 4, 12 and 2 in from three different lines, and the cards hung
        /// 4 past the body towards the frame.
        /// </summary>
        private const float BoardTextInset = 12f;
        /// <summary>Where the body starts under the plate's top: the title's foot (30) and a hand.</summary>
        private const float BoardBodyTop = 44f;

        private void BoardDivider(RectTransform body, ref float y)
        {
            y += 3f;
            var bar = NewRect("Divider", body);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.offsetMin = new Vector2(-(BoardPad - 6f), 0f); bar.offsetMax = new Vector2(BoardPad - 6f, 0f);
            bar.sizeDelta = new Vector2(bar.sizeDelta.x, 5f);
            bar.anchoredPosition = new Vector2(0, -y);
            foreach (var (row, h, c) in new[] { (0f, 1f, UITheme.Magenta[4]), (1f, 3f, UITheme.Magenta[3]), (4f, 1f, UITheme.Magenta[1]) })
            {
                var strip = NewRect("S", bar);
                strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1);
                strip.pivot = new Vector2(0.5f, 1);
                strip.sizeDelta = new Vector2(0, h);
                strip.anchoredPosition = new Vector2(0, -row);
                var si = strip.gameObject.AddComponent<Image>();
                si.color = c; si.raycastTarget = false;
            }
            y += 5f + 7f;   // 15 in all, the hairline's own budget: the boards' height is fixed
        }

        /// <summary>A DARK CARD behind what the board wants read first (seventh list: "öne çıkmasını istediğin
        /// şeylerin arkasını karartacak kart"). Made at the block's top and sized by <see cref="CloseCard"/> once
        /// the block knows how tall it came out; it goes to the back of the body so the block draws over it.</summary>
        private RectTransform OpenCard(RectTransform body, float y)
        {
            var card = NewRect("Card", body);
            card.anchorMin = new Vector2(0, 1); card.anchorMax = new Vector2(1, 1);
            card.pivot = new Vector2(0.5f, 1);
            card.anchoredPosition = new Vector2(0, -(y - 4f));   // its width is the body's: CloseCard gives no overhang
            var ci = card.gameObject.AddComponent<Image>();
            ci.sprite = ChromeArt.Card();
            ci.type = Image.Type.Sliced;
            ci.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.72f);
            ci.raycastTarget = false;
            card.SetAsFirstSibling();
            return card;
        }

        private static void CloseCard(RectTransform card, float yTop, float yBottom)
        {
            if (card == null) return;
            card.sizeDelta = new Vector2(0f, Mathf.Max(8f, yBottom - yTop + 8f));
        }

        // ── the critics ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// WHO DECIDED THE NIGHT: the best served and the lowest of the room (one row each; one visit is never both), on a
        /// plate with no head. Nothing when the room was empty. Returns its height, or 0.
        /// </summary>
        private float BuildCritics(TycoonRun run, RectTransform parent)
        {
            _criticsRoot = null;
            _rightGroup = null;
            CustomerVisit high = null, low = null;
            foreach (var v in run.Floor.Finished)
            {
                if (v.State == VisitState.Served && (high == null || v.Satisfaction > high.Satisfaction)) high = v;
                if (low == null || v.Satisfaction < low.Satisfaction) low = v;
            }
            if (low == high) low = null;
            if (high == null && low == null) return 0f;

            const float Pad = 14f, RowH = 76f, Between = 8f;
            _criticsRoot = HousePlate(parent, "Critics", new Vector2(0.5f, 0.5f), 372f, null, null, Color.white);
            _rightGroup = _criticsRoot.gameObject.AddComponent<CanvasGroup>();
            _rightGroup.blocksRaycasts = false;
            _rightGroup.interactable = false;
            float y = Pad;
            if (high != null) { CriticRow(_criticsRoot, y, high, true); y += RowH + Between; }
            if (low != null) { CriticRow(_criticsRoot, y, low, false); y += RowH + Between; }
            float h = y - Between + Pad;
            _criticsRoot.sizeDelta = new Vector2(372f, h);
            return h;
        }

        /// <summary>
        /// One critic (Q5 A): the licence photo at its real 64 in a polaroid (frame 3, chin 9, dropped at a slight angle),
        /// the words as one run - first name in capitals, then why, quieter - wrapping only where a language runs long;
        /// under them the drink they were poured, drawn at 32, and the score hard right beside the unit star at 36 (Q12,
        /// the 18 px star at a whole 2x).
        /// </summary>
        private void CriticRow(RectTransform card, float y, CustomerVisit v, bool best)
        {
            const float X = BoardPad + BoardTextInset, W = 312f, H = 76f, WordsX = 78f;
            var row = NewRect("Critic", card);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(W, H);
            row.anchoredPosition = new Vector2(X, -y);

            var look = LookFor(v);
            if (look != null && look.Face != null)
            {
                float fw = look.Face.rect.width, fh = look.Face.rect.height;
                var pol = NewRect("Polaroid", row);
                pol.anchorMin = pol.anchorMax = new Vector2(0f, 1f);
                pol.pivot = new Vector2(0.5f, 0.5f);
                pol.sizeDelta = new Vector2(fw + 6f, fh + 12f);
                pol.anchoredPosition = new Vector2((fw + 6f) * 0.5f, -(fh + 12f) * 0.5f);
                pol.localRotation = Quaternion.Euler(0f, 0f, best ? -2.5f : 2.5f);
                var pi = pol.gameObject.AddComponent<Image>();
                pi.color = UITheme.Cream[4]; pi.raycastTarget = false;
                var lift = pol.gameObject.AddComponent<Shadow>();
                lift.effectColor = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.32f);
                lift.effectDistance = new Vector2(2f, -2f);
                var photo = NewRect("P", pol);
                photo.anchorMin = photo.anchorMax = photo.pivot = new Vector2(0f, 1f);
                photo.sizeDelta = new Vector2(fw, fh);
                photo.anchoredPosition = new Vector2(3f, -3f);
                var ph = photo.gameObject.AddComponent<Image>();
                ph.sprite = look.Face; ph.raycastTarget = false;
            }

            var papers = PapersFor(look);
            string full = papers != null ? papers.Name
                : v.Regular != null ? v.Regular.Name : UIText.T("dayend.critic.someone");
            int space = full.IndexOf(' ');
            string first = space > 0 ? full.Substring(0, space) : full;
            string name = BillHandedOn ? UIText.Sentence(first) : UIText.Caps(first);
            var words = NewText("L", row, _body, 16, TextAnchor.UpperLeft, UITheme.Cream[4]);
            words.rectTransform.anchorMin = words.rectTransform.anchorMax = words.rectTransform.pivot = new Vector2(0f, 1f);
            words.rectTransform.sizeDelta = new Vector2(W - WordsX, 40f);
            words.rectTransform.anchoredPosition = new Vector2(WordsX, 0f);
            words.horizontalOverflow = HorizontalWrapMode.Wrap;
            words.verticalOverflow = VerticalWrapMode.Overflow;
            words.supportRichText = true;
            words.text = name + "  <color=#" + ColorUtility.ToHtmlStringRGB(UITheme.Cream[2]) + ">" + CriticReason(v)
                         + "</color>";

            // What they were poured, drawn rather than named - the icon the ticket and the book use.
            var served = v.Served ?? (v.IdInspected ? v.Order.Wanted : null);
            if (served != null)
            {
                var glyph = NewRect("D", row);
                glyph.anchorMin = glyph.anchorMax = new Vector2(0f, 1f);
                glyph.pivot = new Vector2(0f, 0.5f);
                glyph.sizeDelta = new Vector2(32f, 32f);
                glyph.anchoredPosition = new Vector2(WordsX, -58f);
                var gi = glyph.gameObject.AddComponent<Image>();
                gi.sprite = DrinkIcon.For(served, _bootstrap != null ? _bootstrap.Glassware : null);
                gi.preserveAspect = true; gi.raycastTarget = false;
                gi.enabled = gi.sprite != null;
            }

            var ink = best ? UITheme.Amber[4] : UITheme.ViceRed[4];
            var score = NewText("N", row, BillDigits(_body), 24, TextAnchor.MiddleRight, ink);
            score.rectTransform.anchorMin = score.rectTransform.anchorMax = new Vector2(0f, 1f);
            score.rectTransform.pivot = new Vector2(1f, 0.5f);
            score.rectTransform.sizeDelta = new Vector2(64f, 36f);
            score.rectTransform.anchoredPosition = new Vector2(W, -58f);
            score.horizontalOverflow = HorizontalWrapMode.Overflow;
            score.verticalOverflow = VerticalWrapMode.Overflow;
            double critic = BarRating.ExactStarsFor(v.Satisfaction);
            score.text = critic.ToString("0.0");
            float scoreW = Mathf.Ceil(score.preferredWidth);

            // A STAR BESIDE THE FIGURE (2026-08-11), so the number with a point in it reads as a star rating.
            var unit = NewRect("U", row);
            unit.anchorMin = unit.anchorMax = new Vector2(0f, 1f);
            unit.pivot = new Vector2(1f, 0.5f);
            unit.sizeDelta = new Vector2(36f, 36f);
            unit.anchoredPosition = new Vector2(W - scoreW - 6f, -58f);
            var ui = unit.gameObject.AddComponent<Image>();
            ui.sprite = ItemArt.Star(true, 36f);
            ui.preserveAspect = true; ui.raycastTarget = false;
            _criticCounts.Add(k => score.text = (critic * k).ToString("0.0"));
        }

        // ── tomorrow ─────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The TOMORROW board. Laid, measured, and laid again tighter while the right column would not fit the field:
        /// the gaps 12 to 8, then the crowd's TONIGHT row goes (spec §2.7's guard). Returns its height.
        /// </summary>
        private float BuildTomorrow(TycoonRun run, RectTransform parent, float budget)
        {
            for (int level = 0; ; level++)
            {
                float h = LayTomorrow(run, parent, level);
                if (h <= budget || level >= 2) return h;
                _tomorrowRoot.gameObject.SetActive(false);
                Destroy(_tomorrowRoot.gameObject);
                _swapFrom.Clear(); _swapTo.Clear();
            }
        }

        private float LayTomorrow(TycoonRun run, RectTransform parent, int level)
        {
            var next = BarCalendar.NightOf(run.Day + 1);
            _tomorrowRoot = HousePlate(parent, "Tomorrow", new Vector2(0.5f, 0.5f), 372f, UIText.T("dayend.stand.tomorrow"),
                UIText.T(BarCalendar.NameLine(next)), UITheme.Cyan[3]);
            _tomorrowGroup = _tomorrowRoot.gameObject.AddComponent<CanvasGroup>();
            _tomorrowGroup.blocksRaycasts = false;
            _tomorrowGroup.interactable = false;
            var body = NewRect("Body", _tomorrowRoot);
            body.anchorMin = new Vector2(0f, 1f); body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.sizeDelta = new Vector2(-BoardPad * 2f, 0f);
            body.anchoredPosition = new Vector2(0f, -BoardBodyTop);

            float gap = level >= 1 ? 8f : 12f;
            bool crossed = run.RankAfterTonight.Index > run.Rank.Index;
            float y = TomorrowStanding(run, body, crossed);
            y += gap;
            y = TomorrowRung(run, body, y, crossed);
            y += gap;
            y = TomorrowCrowd(run, body, y, level < 2);
            if (run.Quests != null && run.Quest != null && !run.QuestChainOver)
            {
                y += gap;
                y = TomorrowJob(run, body, y, crossed);
            }
            y += 18f;
            float h = BoardBodyTop + y;
            _tomorrowRoot.sizeDelta = new Vector2(372f, h);
            return h;
        }

        /// <summary>One line on the board, hung from the body's top-left at <paramref name="x"/>.</summary>
        private Text BoardLine(RectTransform parent, string name, Font font, Color ink, float x, float y, float w,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = NewText(name, parent, font, 16, align, ink);
            bool right = align == TextAnchor.MiddleRight || align == TextAnchor.UpperRight;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(right ? 1f : 0f, 1f);
            t.rectTransform.pivot = new Vector2(right ? 1f : 0f, 1f);
            t.rectTransform.sizeDelta = new Vector2(w, 20f);
            t.rectTransform.anchoredPosition = new Vector2(x, -y);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>A group the rung's swap fades: the old rung's out, the new one's in.</summary>
        private RectTransform SwapGroup(RectTransform parent, string name, bool incoming)
        {
            var rt = NewRect(name, parent);
            Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.interactable = false;
            if (incoming) { g.alpha = 0f; _swapTo.Add(g); } else _swapFrom.Add(g);
            return rt;
        }

        /// <summary>
        /// WHERE THE BAR STANDS, on its own dark card (seventh list): the rung it stands on, the standing as five live
        /// stars and its figure (NOW), the gauge - the fill, the step as a ghost, the old mark as a tick, the next rung as
        /// a notch - and WAS with the step's chip. The climb moves the stars, the figure and the fill (2026-08-25, "bugün
        /// görselle ne kadar ilerlediğini göster").
        /// </summary>
        private float TomorrowStanding(TycoonRun run, RectTransform body, bool crossed)
        {
            const float In = BoardTextInset;
            double was = run.Rating.Average, now = run.StandingAfterTonight;
            _standFrom = was; _standTo = now;
            var card = OpenCard(body, 4f);

            var titleOld = crossed ? SwapGroup(body, "RungWas", false) : body;
            BoardLine(titleOld, "Rung", _body, UITheme.Magenta[4], In, 8f, 312f).text = UIText.T(run.Rank.Title);
            if (crossed)
                BoardLine(SwapGroup(body, "RungNow", true), "Rung", _body, UITheme.Magenta[4], In, 8f, 312f).text =
                    UIText.T(run.RankAfterTonight.Title);

            _standStars = LiveStarRow(body, new Vector2(0f, 1f), new Vector2(In, -32f), 36f, 4f,
                UITheme.Amber[4], new Color(1f, 1f, 1f, 0.13f));
            _standNumber = BoardLine(body, "Now", _display, UITheme.Amber[4], -In, 40f, 64f, TextAnchor.MiddleRight);
            _standNumber.text = was.ToString("0.00");

            // THE STEP, DRAWN: the gauge fills to where the bar stands, a ghost shows the step, a tick keeps the old mark.
            const float TrackW = 296f, TrackH = 18f;
            var track = NewRect("Track", body);
            Place(track, new Vector2(0.5f, 1f), new Vector2(TrackW, TrackH), new Vector2(0f, -74f));
            var tube = track.gameObject.AddComponent<Image>();
            tube.sprite = ChromeArt.GaugeTube((int)TrackW, (int)TrackH);
            tube.color = UITheme.Night[2];
            tube.raycastTarget = false;
            var inner = NewRect("Inner", track);
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            bool rising = now >= was - 1e-9;
            var ghost = rising ? UITheme.Amber[2] : UITheme.ViceRed[3];
            _standFillGhost = FillBar(inner, new Color(ghost.r, ghost.g, ghost.b, 0.55f));
            _standFillGhost.fillAmount = (float)(Math.Max(was, now) / BarRating.MaxStars);
            _standFill = FillBar(inner, UITheme.Amber[4]);
            _standFill.fillAmount = (float)(was / BarRating.MaxStars);
            var glass = NewRect("Glass", track);
            Stretch(glass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gi = glass.gameObject.AddComponent<Image>();
            gi.sprite = ChromeArt.GaugeGlass((int)TrackW, (int)TrackH, BarRating.MaxStars);
            gi.raycastTarget = false;
            _standWasTick = NewRect("Was", track);
            Place(_standWasTick, new Vector2(0f, 1f), new Vector2(2f, TrackH + 8f),
                new Vector2(Mathf.Round((float)(was / BarRating.MaxStars) * (TrackW - 4f) + 2f), 4f));
            _standWasTick.pivot = new Vector2(0.5f, 1f);
            var wi = _standWasTick.gameObject.AddComponent<Image>();
            wi.color = UITheme.Cream[4]; wi.raycastTarget = false;
            RungNotch(crossed ? SwapGroup(track, "NotchWas", false) : track, BarRank.Above(run.Rank), TrackW, TrackH);
            if (crossed) RungNotch(SwapGroup(track, "NotchNow", true), BarRank.Above(run.RankAfterTonight), TrackW, TrackH);

            var wasLine = BoardLine(body, "WasLine", _body, UITheme.Cream[2], In, 100f, 160f);
            wasLine.text = UIText.T("dayend.stand.was", ("stars", was.ToString("0.00")));

            double step = now - was;
            bool held = Math.Abs(step) < 0.005;
            var chipInk = held ? UITheme.Cream[2] : step > 0 ? UITheme.Lime[4] : UITheme.ViceRed[4];
            var chipPlate = held ? UITheme.Night[2] : step > 0 ? UITheme.Lime[0] : UITheme.ViceRed[0];
            _standDeltaChip = NewRect("Step", body);
            // the chip's figure lands on the board's one right edge: its text stands 8 inside the chip
            Place(_standDeltaChip, new Vector2(1f, 1f), new Vector2(132f, 26f), new Vector2(-(In - 8f), -97f));
            var chip = _standDeltaChip.gameObject.AddComponent<Image>();
            chip.sprite = ChromeArt.Card();
            chip.type = Image.Type.Sliced;
            chip.color = new Color(chipPlate.r, chipPlate.g, chipPlate.b, 0.9f);
            chip.raycastTarget = false;
            if (!held)
            {
                var arrow = NewRect("Arrow", _standDeltaChip);
                Place(arrow, new Vector2(0f, 0.5f), new Vector2(16f, 16f), new Vector2(8f, 0f));
                arrow.pivot = new Vector2(0.5f, 0.5f);
                arrow.anchoredPosition = new Vector2(16f, 0f);
                arrow.localRotation = Quaternion.Euler(0f, 0f, step > 0 ? 0f : 180f);
                _standDeltaArrow = arrow.gameObject.AddComponent<Image>();
                _standDeltaArrow.sprite = ChromeArt.Mark("rise");
                _standDeltaArrow.raycastTarget = false;
                _standDeltaArrow.color = chipInk;
            }
            _standDelta = NewText("StepText", _standDeltaChip, _display, 16, TextAnchor.MiddleRight, chipInk);
            Place(_standDelta.rectTransform, new Vector2(1f, 0.5f), new Vector2(104f, 20f), new Vector2(-8f, 0f));
            _standDelta.horizontalOverflow = HorizontalWrapMode.Overflow;
            _standDelta.verticalOverflow = VerticalWrapMode.Overflow;
            _standDelta.text = held ? UIText.T("dayend.stand.held") : (step > 0 ? "+" : "-") + Math.Abs(step).ToString("0.00");
            _standDeltaChip.gameObject.SetActive(false);   // it lands when the climb does

            CloseCard(card, 4f, 122f);
            return 126f;
        }

        private static void RungNotch(RectTransform track, Rung rung, float trackW, float trackH)
        {
            if (rung == null) return;
            var notch = NewRect("Notch", track);
            notch.anchorMin = notch.anchorMax = new Vector2(0f, 1f);
            notch.pivot = new Vector2(0.5f, 1f);
            notch.sizeDelta = new Vector2(2f, trackH + 6f);
            notch.anchoredPosition = new Vector2(Mathf.Round((float)(rung.Stars / BarRating.MaxStars) * (trackW - 4f) + 2f), 3f);
            var ni = notch.gameObject.AddComponent<Image>();
            ni.color = UITheme.Cyan[3]; ni.raycastTarget = false;
        }

        /// <summary>The next rung, both before and after the swap when tonight crosses one; its height is the taller.</summary>
        private float TomorrowRung(TycoonRun run, RectTransform body, float y, bool crossed)
        {
            if (!crossed) return y + RungBlock(run, body, y, run.Rank);
            float was = RungBlock(run, SwapGroup(body, "NextWas", false), y, run.Rank);
            float now = RungBlock(run, SwapGroup(body, "NextNow", true), y, run.RankAfterTonight);
            return y + Mathf.Max(was, now);
        }

        /// <summary>
        /// THE NEXT RUNG: its gate drawn as stars beside its figure (2026-08-25, "yıldız gereksinimleri her zaman görsel
        /// olarak belirtilsin"), its title, and how many things the certificate will list when it opens - or, on the top
        /// rung, that every rung is open. When tonight was held down, one red line says which ceiling bit.
        /// </summary>
        private float RungBlock(TycoonRun run, RectTransform parent, float y, Rung standing)
        {
            const float In = BoardTextInset, W = 312f;
            var next = BarRank.Above(standing);
            float h;
            if (next == null)
            {
                var all = BoardLine(parent, "AllOpen", _body, UITheme.Cream[2], In, y, W);
                all.horizontalOverflow = HorizontalWrapMode.Wrap;
                all.text = UIText.T("dayend.stand.all_open");
                h = Mathf.Max(20f, Mathf.Ceil(all.preferredHeight));
            }
            else
            {
                var cap = BoardLine(parent, "NextRung", _body, UITheme.Cream[2], In, y, 200f);
                cap.text = UIText.T("dayend.tomorrow.next_rung");
                var gate = BoardLine(parent, "Gate", _display, UITheme.Amber[4], -In, y, 64f, TextAnchor.MiddleRight);
                gate.text = next.Stars.ToString("0.0");
                // the gate's stars sit between the caption and the figure: 14 at 1x
                var row = StarRow(parent, new Vector2(0f, 1f), new Vector2(0f, -(y + 3f)), 14f, next.Stars,
                    Color.white, new Color(1f, 1f, 1f, 0.35f));
                row.anchoredPosition = new Vector2(In + Mathf.Ceil(cap.preferredWidth) + 8f, -(y + 3f));
                var title = BoardLine(parent, "Title", _display, UITheme.Amber[4], In, y + 22f, W);
                title.horizontalOverflow = HorizontalWrapMode.Wrap;
                title.text = UIText.T(next.Title);
                float titleH = Mathf.Max(20f, Mathf.Ceil(title.preferredHeight));
                int opens = TilesFor(run, next, next, false).Count;
                var open = BoardLine(parent, "Opens", _body, UITheme.Cream[2], In, y + 22f + titleH, W);
                open.horizontalOverflow = HorizontalWrapMode.Wrap;
                open.text = UIText.N("dayend.tomorrow.opens", opens);
                h = 22f + titleH + Mathf.Max(20f, Mathf.Ceil(open.preferredHeight));
            }
            // HELD DOWN TONIGHT: the room's customers went higher than the bar was allowed to be worth.
            double raw = BarRating.ExactStarsFor(run.Floor.AverageSatisfaction);
            if (raw > run.StarCeiling + 1e-9)
            {
                bool room = run.ComfortTonight < run.MenuStarCap;
                var line = BoardLine(parent, "Held", _body, UITheme.ViceRed[4], In, y + h + 2f, W);
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                line.text = UIText.T(room ? "dayend.tomorrow.room_held" : "dayend.tomorrow.menu_held",
                    ("stars", run.StarCeiling.ToString("0.00")));
                h += 2f + Mathf.Max(20f, Mathf.Ceil(line.preferredHeight));
            }
            return h;
        }

        /// <summary>
        /// TOMORROW'S CROWD (Q10: the crowd and its arrow, no covers count - Core plans the covers when a night opens):
        /// the name in cream, the arrow lime up or red down, none when it held. When it moved, tonight's crowd stays on
        /// the screen under it.
        /// </summary>
        private float TomorrowCrowd(TycoonRun run, RectTransform body, float y, bool tonightRow)
        {
            const float In = BoardTextInset, W = 312f;
            var tomorrow = run.CrowdTomorrow;
            var today = run.CrowdToday;
            int dir = ((int)tomorrow).CompareTo((int)today);
            var cap = BoardLine(body, "Crowd", _body, UITheme.Cream[2], In, y, 160f);
            cap.text = UIText.T("dayend.tomorrow.crowd");
            float arrowW = dir != 0 ? 20f : 0f;
            var name = BoardLine(body, "CrowdName", _display, UITheme.Cream[4], -(In + arrowW), y, 260f, TextAnchor.MiddleRight);
            name.text = CrowdName(tomorrow);
            float h = 22f;
            float lineY = y;
            if (Mathf.Ceil(cap.preferredWidth) + 12f + Mathf.Ceil(name.preferredWidth) + arrowW > W)
            {
                // a name that does not fit beside its caption takes its own row
                lineY = y + 22f;
                name.rectTransform.anchoredPosition = new Vector2(-(In + arrowW), -lineY);
                h += 22f;
            }
            if (dir != 0)
            {
                var arrow = NewRect("CrowdArrow", body);
                arrow.anchorMin = arrow.anchorMax = new Vector2(1f, 1f);
                arrow.pivot = new Vector2(0.5f, 0.5f);
                arrow.sizeDelta = new Vector2(16f, 16f);
                arrow.anchoredPosition = new Vector2(-(In + 8f), -(lineY + 10f));
                arrow.localRotation = Quaternion.Euler(0f, 0f, dir > 0 ? 0f : 180f);
                var ai = arrow.gameObject.AddComponent<Image>();
                ai.sprite = ChromeArt.Mark("rise");
                ai.color = dir > 0 ? UITheme.Lime[4] : UITheme.ViceRed[4];
                ai.raycastTarget = false;
                if (tonightRow)
                {
                    float ty = y + h;
                    BoardLine(body, "Tonight", _body, UITheme.Cream[2], In, ty, 160f).text = UIText.T("dayend.stand.tonight");
                    BoardLine(body, "TonightName", _body, UITheme.Cream[2], -In, ty, 200f, TextAnchor.MiddleRight).text =
                        CrowdName(today);
                    h += 22f;
                }
            }
            return y + h;
        }

        /// <summary>
        /// HER JOB, on the board that says what tomorrow holds: her face (the face is the caption), what is owed with the
        /// kind's own picture, how far along in the kind's own shape, and when it pays - on the spot, at dawn (a goal the
        /// dawn will pay, asked of Core), or, done, when she comes by to say so.
        /// </summary>
        private float TomorrowJob(TycoonRun run, RectTransform body, float y, bool crossed)
        {
            const float In = BoardTextInset, RowX = In + 76f, RowW = 236f;
            var quest = run.Quest;
            var def = quest.Definition;
            var host = Hostess;
            var face = LookForStory(host)?.Face;
            bool done = quest.IsDone;
            var nextUp = run.QuestNextUp;
            bool waiting = done && !run.QuestDoneUnsaid && nextUp != null && run.Rank.Index < nextUp.Rung;

            if (face != null)
            {
                var frame = NewRect("Face", body);
                Place(frame, new Vector2(0f, 1f), new Vector2(face.rect.width + 4f, face.rect.height + 4f), new Vector2(In, -y));
                var fi = frame.gameObject.AddComponent<Image>();
                fi.color = UITheme.Cream[1]; fi.raycastTarget = false;
                var photo = NewRect("Photo", frame);
                Stretch(photo, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
                var pi = photo.gameObject.AddComponent<Image>();
                pi.sprite = face; pi.raycastTarget = false;
            }

            // a: what is owed, with the kind's picture at the largest whole multiple that fits its box
            string owed;
            Color owedInk = UITheme.Cream[4];
            Sprite icon = null;
            if (waiting) owed = UIText.T("chrome.quest.waiting", ("title", UIText.T(BarRank.Rungs[nextUp.Rung].Title)));
            else if (done) { owed = UIText.T("dayend.tomorrow.job_done"); owedInk = UITheme.Lime[4]; icon = JobIcon(run, quest); }
            else { owed = UIText.Caps(UIText.T(quest.OwedLine())); icon = JobIcon(run, quest); }
            float textX = RowX;
            if (icon != null)
            {
                var box = NewRect("Kind", body);
                Place(box, new Vector2(0f, 1f), new Vector2(36f, 36f), new Vector2(RowX, -y));
                var art = NewRect("Art", box);
                float aw = icon.rect.width, ah = icon.rect.height;
                float k = Mathf.Floor(36f / Mathf.Max(aw, ah));
                art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 0.5f);
                art.anchoredPosition = Vector2.zero;
                // a whole multiple where the art has one; the three with none (the cloth, the card, a fitting) are the
                // bubble's own scale findings, fitted as the bubble fits them
                art.sizeDelta = k >= 1f ? new Vector2(aw * k, ah * k) : new Vector2(36f, 36f);
                var ai = art.gameObject.AddComponent<Image>();
                ai.sprite = icon; ai.preserveAspect = true; ai.raycastTarget = false;
                textX = RowX + 44f;
            }
            var owedText = BoardLine(body, "Owed", _body, owedInk, textX, y + 8f, RowX + RowW - textX);
            owedText.horizontalOverflow = HorizontalWrapMode.Wrap;
            owedText.text = owed;
            float aH = Mathf.Max(36f, 8f + Mathf.Ceil(owedText.preferredHeight));

            // b: how far along, in the kind's shape
            float by = y + aH + 4f, bH = 0f;
            if (!waiting)
            {
                if (!def.IsStateGoal)
                {
                    var pips = NewRect("Pips", body);
                    Place(pips, new Vector2(0f, 1f), new Vector2(RowW, QuestPipH), new Vector2(RowX, -(by + 5f)));
                    for (int i = 0; i < quest.Target; i++)
                    {
                        var pip = NewRect("P" + i, pips);
                        Place(pip, new Vector2(0f, 0.5f), new Vector2(QuestPipW, QuestPipH),
                            new Vector2(i * (QuestPipW + QuestPipGap), 0f));
                        var pimg = pip.gameObject.AddComponent<Image>();
                        bool lit = i < quest.Progress;
                        pimg.color = !lit ? new Color(UITheme.Cream[1].r, UITheme.Cream[1].g, UITheme.Cream[1].b, 0.35f)
                            : done ? UITheme.Lime[3] : UITheme.Magenta[3];
                        pimg.raycastTarget = false;
                    }
                    bH = 16f;
                }
                else if (def.Kind == QuestKind.Rank)
                {
                    var from = crossed ? SwapGroup(body, "ProgressWas", false) : body;
                    BoardLine(from, "Progress", _body, UITheme.Cream[3], RowX, by, RowW).text =
                        UIText.T("chrome.quest.progress_rank", ("title", UIText.T(run.Rank.Title)));
                    if (crossed)
                        BoardLine(SwapGroup(body, "ProgressNow", true), "Progress", _body, UITheme.Cream[3], RowX, by, RowW).text =
                            UIText.T("chrome.quest.progress_rank", ("title", UIText.T(run.RankAfterTonight.Title)));
                    bH = 20f;
                }
                else
                {
                    string line = def.Kind == QuestKind.Comfort
                        ? UIText.T("chrome.quest.progress_comfort",
                            ("now", run.ComfortBase.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                            ("goal", def.GoalComfort.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)))
                        : UIText.T("chrome.quest.progress_fit", ("now", run.LadderLevel(def.Slot)), ("goal", def.Level));
                    BoardLine(body, "Progress", _body, UITheme.Cream[3], RowX, by, RowW).text = line;
                    bH = 20f;
                }
            }

            // c: when it pays, or when she comes to say so
            float cy = by + (bH > 0f ? bH + 4f : 0f);
            string status = null;
            if (!done)
                status = run.QuestPaysAtDawn
                    ? UIText.T("dayend.tomorrow.pays_dawn", ("reward", quest.Reward))
                    : UIText.T("chrome.quest.pays", ("reward", quest.Reward));
            else if (!waiting)
            {
                bool comesTonight = run.HostessComesOn > 0 && run.Day >= run.HostessComesOn;
                status = UIText.T(comesTonight ? "chrome.quest.comes_tonight" : "chrome.quest.comes_tomorrow",
                    ("who", HostessWho()));
            }
            float cH = 0f;
            if (status != null)
            {
                var st = BoardLine(body, "Status", _body, UITheme.Lime[3], RowX, cy, RowW);
                st.horizontalOverflow = HorizontalWrapMode.Wrap;
                st.text = status;
                cH = Mathf.Max(20f, Mathf.Ceil(st.preferredHeight));
            }
            float h = Mathf.Max(face != null ? face.rect.height + 4f : 0f, cy + cH - y) + 4f;
            return y + h;
        }

        /// <summary>The kind's picture for the board's 36 box: the star at 2x, the medal and the perfect mark at their
        /// 32, and otherwise the message's own picture (TycoonHud.Quest).</summary>
        private Sprite JobIcon(TycoonRun run, ActiveQuest quest)
        {
            switch (quest.Kind)
            {
                case QuestKind.Rank: return ItemArt.Star(true, 36f);
                case QuestKind.Comfort: return ItemArt.Medal(true, 32f);
                case QuestKind.Perfect: return ItemArt.Perfect(32f);
                default: return QuestIcon(run, quest);
            }
        }
    }
}
