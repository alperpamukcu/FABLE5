using System;
using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part GameOver: the bar that goes under (2026-09-28).
    //
    // Until today a lost run was one red line of text over the live room, the market still had to be walked (a till
    // three nights under cannot buy anything), a question about the empty van still had to be answered, and after all
    // of it Escape did nothing: the only road out was the cog, the settings and START OVER. The author asked for the end
    // of a bar to lead back to the front door and to show what the bar did with its life. So, when the dawn shuts it:
    //
    //   the sign on the beam dies - the tube sputters and goes out (the NEON beat; _beamOut is the tube's OUT state);
    //   the register prints its last Z REPORT, the bar's whole life on one tape - how long, how high, how rich, how
    //     much work - every figure a fold over the ledger's own rows (Core's history, read here, never re-judged);
    //   the landlord's NOTICE OF CLOSURE slides in with the red nights that shut it, and takes his stamp;
    //   and one orange key walks to the front door (MAIN MENU), where CONTINUE stands greyed - the save went with the
    //     bar - and NEW RUN is one press away.
    //
    // No "GAME OVER" caption anywhere: the dead sign says it. It never calls Feat or Achievements (Core pushed the lost
    // bar at the close) and it writes nothing. The night's show (TycoonHud.NightShow) routes its last night here through
    // LOCK UP; OnOpenTomorrow raises it in the frame the dawn closes the bar, whichever door led there. The clocks run
    // on Ceremony.Pace like every ceremony's, a click lands the print and the count, Escape lays it all out, and reduced
    // motion lays it out on the first frame - the key still waiting and arming by its own rules.
    public sealed partial class TycoonHud
    {
        /// <summary>The game over's beats, in the order they play.</summary>
        private enum GoBeat { Off, Dark, Neon, Print, Count, Notice, Stamp, Key, Done }

        private RectTransform _gameOverPanel;
        private CanvasGroup _goGroup;
        /// <summary>The dark over the room (under the beam) and the lid over the beam itself: the bar is inert once the
        /// tape prints - no buried tips or cards, no ladder side trip out of the end of a bar.</summary>
        private Image _goScrim, _goLid;
        private RectTransform _goReport, _goRoll, _goRows, _goFoot, _goNotice, _goStamp, _goKey, _goNote;
        private CanvasGroup _goNoticeFade, _goKeyFade, _goNoteFade;
        private Text _goStampInk;
        private Button _goKeyButton;
        private Vector2 _goNoticeHome, _goKeyHome, _goNoteHome;
        /// <summary>The Z report's length (its paper, on the 12-unit grid; the torn foot hangs under it).</summary>
        private float _goTapeL;
        /// <summary>The report's figures, top to bottom, as closures drawing each at a share of its value - the night
        /// tape's own counting (TycoonHud.DayEnd._billCounts), kept apart so neither screen ever counts the other's.</summary>
        private readonly List<Action<float>> _goCounts = new List<Action<float>>();
        private int _goCountTicked;

        private GoBeat _goBeat = GoBeat.Off;
        /// <summary>Seconds into the beat in progress, paced.</summary>
        private float _goT;
        /// <summary>When the screen went up, in real seconds: the key never arms inside the first half second, so the
        /// press that locked up cannot also leave the bar.</summary>
        private float _goEnteredAt;
        /// <summary>When the key was first asked for (its wait for an achievement's card is bounded from here), and
        /// how far into its rise it is (&lt; 0 while it waits).</summary>
        private float _goKeyAskedAt, _goKeyT = -1f;
        private float _goShake;
        private Color _goTubeFrom, _goBloomFrom;
        private bool _goLeaving, _goArmed;

        /// <summary>The game over is up (it stays up under the front door until a new run starts).</summary>
        private bool GameOverUp => _gameOverPanel != null && _gameOverPanel.gameObject.activeSelf;

        /// <summary>An achievement's card is on the screen or queued for it (TycoonHud.Achievements, read only): the lost
        /// bar's LAST ORDERS lands a frame after the close, at order 30, and the front door (31) would bury it.</summary>
        private bool AchievementCardBusy => _achCardAt >= 0f || _achievementNews.Count > 0;

        // ── the clocks (Pace 1) ──────────────────────────────────────────────────────────────────────────────────────
        /// <summary>The dark lifts a little off the night show's .988, so the empty room comes up faintly.</summary>
        private const float GoDark = 0.50f, GoScrimA = 0.90f;
        /// <summary>The sign's death: 0.8 s, cut on and off ten times, then out.</summary>
        private const float GoNeon = 0.80f, NeonFade = 0.30f;
        /// <summary>The cuts, in seconds into the NEON beat. The beat opens on the power dropping; the tube is lit when an
        /// odd number of cuts has passed - four short catches and a longer one - and it is dark from the last until
        /// the sign goes OUT at the beat's end.</summary>
        private static readonly float[] NeonCuts = { 0.04f, 0.07f, 0.10f, 0.12f, 0.20f, 0.26f, 0.29f, 0.31f, 0.45f, 0.55f };
        /// <summary>The lid over the beam: all but clear while the sign dies (it still takes the pointer), closed to
        /// the room's own dark as the tape starts.</summary>
        private const float GoLid = 0.30f, GoLidA = 0.90f, GoLidOff = 0.004f;
        /// <summary>The Z report feeds at 420 units a second - an English tape in about a second.</summary>
        private const float GoFeed = 420f;
        private const float GoNoticeIn = 0.40f, GoSlide = 54f, GoShakeFor = 0.25f;
        private const float GoKeyRise = 0.24f, GoKeyLift = 14f, GoArm = 0.5f, GoCardWait = 8f;

        // ── the layout (1280x720, y down from the top) ───────────────────────────────────────────────────────────────
        /// <summary>The tape's centre, x 128-584 (so the lost bar's card, x 586 and up, lands clear of it).</summary>
        private const float GoTapeX = -284f;
        /// <summary>The notice: x 680-1120, from y 150 (under the card's y 92-144); its words stand 30 in from its edges.</summary>
        private const float GoNoticeX = 260f, GoNoticeTop = 150f, GoNoticeW = 440f, GoNoticeInset = 30f;
        /// <summary>The stamp's own ground under the red nights (a 256x54 stamp at -9 degrees stands ~94 tall), and its
        /// centre left of the paper's middle so its dropped right end clears the signature.</summary>
        private const float GoStampGround = 88f, GoStampX = -40f;
        private const float GoRowH = 24f;

        // ── construction ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The panel, once: its own canvas at 23 (over the books' 22, under the curtain and the achievement card's 30 and
        /// the front door's 31 - no other layer is 23), the dark over the room and the lid over the beam. The report,
        /// the notice and the key are laid for each bar that goes under (ShowGameOver).
        /// </summary>
        private void BuildGameOver(RectTransform root)
        {
            _gameOverPanel = NewRect("GameOver", root);
            var canvas = _gameOverPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 23;
            _gameOverPanel.gameObject.AddComponent<ForgivingRaycaster>();
            _goGroup = _gameOverPanel.gameObject.AddComponent<CanvasGroup>();
            Stretch(_gameOverPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var night = UITheme.Night[0];
            var scrim = NewRect("Scrim", _gameOverPanel);
            Stretch(scrim, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -TopBarH));
            BleedWidth.Apply(scrim);
            _goScrim = scrim.gameObject.AddComponent<Image>();
            _goScrim.color = new Color(night.r, night.g, night.b, DayEndScrimA);
            _goScrim.raycastTarget = true;     // the room under it is over: a click must not reach a stool

            var lid = NewRect("Lid", _gameOverPanel);
            Stretch(lid, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -TopBarH), Vector2.zero);
            BleedWidth.Apply(lid);
            _goLid = lid.gameObject.AddComponent<Image>();
            _goLid.color = new Color(night.r, night.g, night.b, GoLidOff);
            _goLid.raycastTarget = true;

            _gameOverPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// THE BAR WENT UNDER: the report and the notice laid for this bar, parked for the beats (or placed whole), and
        /// the panel up. From OnOpenTomorrow in the frame the dawn closes the bar - under reduced motion placed whole -
        /// and from the phase check when a scene is rebuilt around a bar already closed (<paramref name="quiet"/>: the
        /// stamp is not heard a second time).
        /// </summary>
        private void ShowGameOver(TycoonRun run, bool whole, bool quiet = false)
        {
            if (_gameOverPanel == null || run == null) return;
            CloseNote();
            CloseId();
            // Up before it is laid, so every word is measured against a live rect.
            _gameOverPanel.gameObject.SetActive(true);
            if (_goGroup != null) { _goGroup.alpha = 1f; _goGroup.interactable = true; _goGroup.blocksRaycasts = true; }
            ClearGoPaper();
            BuildGoTape(run);
            BuildGoKey(BuildGoNotice(run));

            _goEnteredAt = Time.unscaledTime;
            _goLeaving = _goArmed = false;
            _goBeat = GoBeat.Dark;
            _goT = 0f; _goKeyT = -1f; _goShake = 0f; _goCountTicked = 0;

            // Parked: the dark as the night show left it, the lid open, the tape rolled up, every figure blank, the
            // notice off its edge, the stamp not yet struck, the key not yet offered.
            SetGoScrim(DayEndScrimA);
            SetGoLid(GoLidOff);
            SetGoTape(0f);
            foreach (var count in _goCounts) count(0f);
            SetGoNotice(0f);
            if (_goStamp != null) _goStamp.gameObject.SetActive(false);
            if (whole) PlaceWholeGameOver(quiet);
        }

        /// <summary>A new bar: nothing of the last one's end survives it - the panel down, the beats off, the paper
        /// gone, the sign on the beam lit again (its state repainted from scratch) and the printer quiet.</summary>
        private void ResetGameOver()
        {
            if (_gameOverPanel != null) _gameOverPanel.gameObject.SetActive(false);
            _goBeat = GoBeat.Off;
            _goLeaving = _goArmed = _beamOut = _fatalNight = false;
            _beamState = -1;
            if (_neonTube != null) _neonTube.enabled = true;
            if (_neonBloom != null) _neonBloom.enabled = true;
            ClearGoPaper();
            Sfx.HoldLoop(null);
        }

        /// <summary>Everything laid for a bar - the report, the notice, the key and its note - goes; the dark and the lid
        /// are the panel's own and stay. Switched off before it is destroyed, so nothing finds it in the frame between.</summary>
        private void ClearGoPaper()
        {
            _goCounts.Clear();
            if (_gameOverPanel != null)
                for (int i = _gameOverPanel.childCount - 1; i >= 0; i--)
                {
                    var child = _gameOverPanel.GetChild(i);
                    if ((_goScrim != null && child == _goScrim.transform) || (_goLid != null && child == _goLid.transform))
                        continue;
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            _goReport = _goRoll = _goRows = _goFoot = _goNotice = _goStamp = _goKey = _goNote = null;
            _goNoticeFade = _goKeyFade = _goNoteFade = null;
            _goStampInk = null;
            _goKeyButton = null;
        }

        // ── the Z report ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE FINAL Z REPORT: the author's paper (the night tape's stock, ChromeArt.TapeBody at a whole 3x) printed with
        /// the bar's whole life, from the ledger's rows - a sum or a single best, read once, here. Old saves' rows without
        /// their detail add zeros, as the register's book has always read them. NIGHTS OPEN is the one big figure: the
        /// game is "how long and how rich" (GDD 23 §9), and how long comes first.
        /// </summary>
        private void BuildGoTape(TycoonRun run)
        {
            _goReport = NewRect("ZReport", _gameOverPanel);
            _goReport.anchorMin = _goReport.anchorMax = new Vector2(0.5f, 1f);
            _goReport.pivot = new Vector2(0.5f, 1f);
            _goReport.anchoredPosition = new Vector2(GoTapeX, -TopBarH);   // fed out from under the beam
            _goRoll = TopRect("Roll", _goReport, TapeW, 0f, 0f);
            _goRoll.gameObject.AddComponent<RectMask2D>();
            var paper = TopRect("Paper", _goRoll, TapeW, 0f, 0f);
            var paperImg = paper.gameObject.AddComponent<Image>();
            paperImg.raycastTarget = false;
            var body = ChromeArt.TapeBody();
            if (body != null)
            {
                paperImg.sprite = body;
                paperImg.type = Image.Type.Tiled;
                paperImg.pixelsPerUnitMultiplier = 1f / 3f;
            }
            else { paperImg.color = UITheme.Cream[4]; Frame(paper, 2f, UITheme.Cream[2]); }
            _goRows = TopRect("Rows", _goRoll, TapeW, 0f, 0f);

            // the sums, folded once over the ledger's rows
            var h = run.Ledger.History;
            int took = 0, tips = 0, paid = 0, rent = 0, served = 0, walked = 0, best = -1;
            for (int i = 0; i < h.Count; i++)
            {
                var r = h[i];
                took += r.Income; tips += r.Tips; paid += r.Expenses; rent += r.Rent;
                served += r.Served; walked += r.WalkedOut;
                if (best < 0 || r.Income > h[best].Income) best = i;   // the first of equals
            }
            int bestDay = best >= 0 ? h[best].Day : run.Day, bestTook = best >= 0 ? h[best].Income : 0;

            float y = 12f + 4f;                      // the stock's torn top, and a margin
            var title = GoLine("Title", y, 28f);
            var tt = NewText("T", title, _display, 16, TextAnchor.MiddleCenter, TapeInk);
            Stretch(tt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            tt.horizontalOverflow = HorizontalWrapMode.Overflow;
            tt.text = UIText.T("gameover.z.title");
            y += 28f;
            var sub = GoLine("Sub", y, 16f);
            var st = NewText("T", sub, _body, 8, TextAnchor.MiddleCenter, TapeQuiet);
            Stretch(st.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            st.horizontalOverflow = HorizontalWrapMode.Overflow;
            string seed = _bootstrap != null ? _bootstrap.CurrentSeed : "";
            st.text = BillWords(UIText.T("gameover.z.sub", ("when", UIText.T(BarCalendar.LabelLine(run.Day))),
                ("seed", seed ?? "")));
            y += 16f;
            y = GoRule(y);

            // HOW LONG: the one big figure, the display face at 24
            y = GoCountRow(y, "Row.nights", UIText.T("gameover.z.nights"), run.Day, TapeInk, 32f, 24);
            y = GoRule(y);

            // THE BAR: how high it climbed, and its best night in money
            y = GoHead(y, "Head.bar", UIText.T("gameover.z.head.bar"));
            y = GoRungRow(y, run);
            y = GoMoneyRow(y, "Row.best", UIText.T("gameover.z.best_night", ("n", bestDay)), bestTook, "", BillGain,
                TapeInk, 0f);
            y += 4f;

            // THE TILL: what came in, what went out, and the two parts of each a player asks after
            y = GoHead(y, "Head.till", UIText.T("gameover.z.head.till"));
            y = GoMoneyRow(y, "Row.in", UIText.T("dayend.bill.took_in"), took, "", BillGain, TapeInk, 0f);
            y = GoMoneyRow(y, "Row.tips", UIText.T("dayend.bill.tips"), tips, "", TapeQuiet, TapeQuiet, 16f);
            y = GoMoneyRow(y, "Row.out", UIText.T("dayend.bill.paid_out"), paid, "-", BillLoss, TapeInk, 0f);
            y = GoMoneyRow(y, "Row.rent", UIText.T("dayend.bill.rent"), rent, "-", TapeQuiet, TapeQuiet, 16f);
            y += 4f;

            // THE WORK: the people, the book, and her jobs - the story's own ink - where there is a book of them
            y = GoHead(y, "Head.work", UIText.T("gameover.z.head.work"));
            y = GoCountRow(y, "Row.served", UIText.T("gameover.z.served"), served, TapeInk, GoRowH, 16);
            y = GoCountRow(y, "Row.walked", UIText.T("gameover.z.walked"), walked, BillLoss, GoRowH, 16);
            y = GoOfRow(y, "Row.pages", UIText.T("gameover.z.pages"), run.PerfectedCount, run.MenuRecipes.Count, TapeInk);
            int jobs = run.Quests != null ? run.Quests.Count : 0;
            if (jobs > 0)
            {
                string who = HostessWho();
                if (string.IsNullOrEmpty(who)) who = UIText.T("dayend.host.fallback");
                y = GoOfRow(y, "Row.jobs", UIText.T("gameover.z.jobs", ("who", who)), run.QuestsDone, jobs,
                    UITheme.Magenta[1]);
            }

            y += 12f;                                // blank paper, then the torn foot
            _goTapeL = Mathf.Ceil(y / 12f) * 12f;    // the tile ends on a whole art row, the edge on the grid
            paper.sizeDelta = new Vector2(TapeW, _goTapeL);
            _goRows.sizeDelta = new Vector2(TapeW, _goTapeL);
            _goReport.sizeDelta = new Vector2(TapeW, _goTapeL + 12f);

            // the stock's own torn foot, riding the roll's edge as it feeds and hanging under it once it is out
            _goFoot = TopRect("Foot", _goReport, TapeW, 12f, _goTapeL);
            var foot = _goFoot.gameObject.AddComponent<Image>();
            foot.sprite = ChromeArt.TapeFoot();
            foot.raycastTarget = false;
            if (foot.sprite == null) foot.color = UITheme.Cream[4];
            SetGoTape(_goTapeL);
        }

        /// <summary>One printed line of the report: the print column, hung at <paramref name="y"/> under the paper's top.</summary>
        private RectTransform GoLine(string name, float y, float h) => TopRect(name, _goRows, TapePrint, h, y);

        /// <summary>A printed rule, dashed, in the paper's ink; eight units with the rule across their middle.</summary>
        private float GoRule(float y)
        {
            var rule = GoLine("Rule", y + 4f, 1f);
            var img = rule.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.DashRule();
            img.type = Image.Type.Tiled;
            img.color = TapeInk;
            img.raycastTarget = false;
            return y + 8f;
        }

        /// <summary>A block's head, the night tape's look rebuilt here (never TapeHead, which prints on the night's own
        /// tape): the heavy face, wide-tracked capitals, a two-unit rule of the ink under the word.</summary>
        private float GoHead(float y, string name, string text)
        {
            var row = GoLine(name, y, GoRowH);
            var head = NewText("H", row, _shop != null ? _shop : _display, 16, TextAnchor.MiddleLeft, TapeInk);
            Stretch(head.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.verticalOverflow = VerticalWrapMode.Overflow;
            var sb = new System.Text.StringBuilder();
            foreach (var ch in text ?? "") { sb.Append(ch); if (ch != ' ' && ch <= 'ɏ') sb.Append(' '); }
            head.text = sb.ToString().TrimEnd(' ');
            var rule = NewRect("U", row);
            rule.anchorMin = rule.anchorMax = new Vector2(0f, 0.5f);
            rule.pivot = new Vector2(0f, 1f);
            rule.sizeDelta = new Vector2(Mathf.Min(Mathf.Round(head.preferredWidth), 200f), 2f);
            rule.anchoredPosition = new Vector2(0f, -9f);
            var ri = rule.gameObject.AddComponent<Image>();
            ri.color = new Color(TapeInk.r, TapeInk.g, TapeInk.b, 0.8f);
            ri.raycastTarget = false;
            return y + GoRowH;
        }

        /// <summary>A row's word, at the left of the column - dropped to the small line where it would run into its
        /// figure (the night tape's rule, 2026-09-22: no word runs into its figure).</summary>
        private void GoLabel(RectTransform row, string text, float indent, float figW, Color ink)
        {
            var l = NewText("L", row, _body, 16, TextAnchor.MiddleLeft, ink);
            Stretch(l.rectTransform, Vector2.zero, Vector2.one, new Vector2(indent, 0f), new Vector2(-(figW + 14f), 0f));
            l.horizontalOverflow = HorizontalWrapMode.Overflow;
            l.verticalOverflow = VerticalWrapMode.Overflow;
            l.text = BillWords(text);
            if (l.preferredWidth > TapePrint - indent - figW - 14f)
            {
                l.font = BillSmallFont;
                l.fontSize = BillSmallSize;
            }
        }

        /// <summary>
        /// A figure, hard right: its digits alone in a Text named V (the suite reads them), in the house figure face in
        /// every language (_figures - the till's own L2 rule), the drawn cash mark left of them when it is money and the
        /// sign left of that - placed by measuring the digits, and re-placed as they count. A figure that has not
        /// started counting shows nothing. Returns its width at the final value.
        /// </summary>
        private float GoFigure(RectTransform row, int amount, string sign, Color ink, bool money, int size, float h)
        {
            const float Mark = 16f, Gap = 3f;
            var digits = NewText("V", row, _figures, size, TextAnchor.MiddleRight, ink);
            Place(digits.rectTransform, new Vector2(1f, 0.5f), new Vector2(160f, h), Vector2.zero);
            digits.horizontalOverflow = HorizontalWrapMode.Overflow;
            digits.verticalOverflow = VerticalWrapMode.Overflow;
            int target = Mathf.Abs(amount);
            digits.text = target.ToString();
            float digitsW = digits.preferredWidth;
            float markRoom = money ? Gap + Mark : 0f;

            RectTransform cash = null;
            if (money)
            {
                cash = NewRect("C", row);
                Place(cash, new Vector2(1f, 0.5f), new Vector2(Mark, Mark), new Vector2(-Mathf.Round(digitsW + Gap), 0f));
                var ci = cash.gameObject.AddComponent<Image>();
                ci.sprite = ChromeArt.Mark("cash");
                ci.color = ink; ci.raycastTarget = false;
            }
            Text s = null;
            if (!string.IsNullOrEmpty(sign))
            {
                s = NewText("S", row, _figures, size, TextAnchor.MiddleRight, ink);
                Place(s.rectTransform, new Vector2(1f, 0.5f), new Vector2(40f, h),
                    new Vector2(-Mathf.Round(digitsW + markRoom + Gap), 0f));
                s.horizontalOverflow = HorizontalWrapMode.Overflow;
                s.verticalOverflow = VerticalWrapMode.Overflow;
                s.text = sign;
            }
            float width = digitsW + markRoom + (s != null ? Gap + s.preferredWidth : 0f);
            _goCounts.Add(k =>
            {
                bool on = k > 0f;
                digits.text = on ? Mathf.RoundToInt(target * k).ToString() : "";
                if (cash != null && cash.gameObject.activeSelf != on) cash.gameObject.SetActive(on);
                if (s != null && s.gameObject.activeSelf != on) s.gameObject.SetActive(on);
                if (!on) return;
                float w = digits.preferredWidth;
                if (cash != null) cash.anchoredPosition = new Vector2(-Mathf.Round(w + Gap), 0f);
                if (s != null) s.rectTransform.anchoredPosition = new Vector2(-Mathf.Round(w + markRoom + Gap), 0f);
            });
            return width;
        }

        private float GoMoneyRow(float y, string name, string label, int amount, string sign, Color figInk, Color labelInk,
            float indent)
        {
            var row = GoLine(name, y, GoRowH);
            float figW = GoFigure(row, amount, sign, figInk, true, 16, GoRowH);
            GoLabel(row, label, indent, figW, labelInk);
            return y + GoRowH;
        }

        private float GoCountRow(float y, string name, string label, int count, Color figInk, float h, int size)
        {
            var row = GoLine(name, y, h);
            float figW = GoFigure(row, count, "", figInk, false, size, h);
            GoLabel(row, label, 0f, figW, TapeInk);
            return y + h;
        }

        /// <summary>A row that reads n OF total - the pages perfected, her jobs done - in the body face, counting its n.</summary>
        private float GoOfRow(float y, string name, string label, int n, int total, Color figInk)
        {
            var row = GoLine(name, y, GoRowH);
            var v = NewText("V", row, _body, 16, TextAnchor.MiddleRight, figInk);
            Place(v.rectTransform, new Vector2(1f, 0.5f), new Vector2(200f, GoRowH), Vector2.zero);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            v.verticalOverflow = VerticalWrapMode.Overflow;
            v.text = UIText.T("gameover.z.of", ("n", n), ("total", total));
            float figW = Mathf.Ceil(v.preferredWidth);
            _goCounts.Add(k => v.text = k <= 0f ? ""
                : UIText.T("gameover.z.of", ("n", Mathf.RoundToInt(n * k)), ("total", total)));
            GoLabel(row, label, 0f, figW, TapeInk);
            return y + GoRowH;
        }

        /// <summary>
        /// HOW HIGH IT CLIMBED: the rung's title as the row's word, and at the right the beam's own five small stars at
        /// exactly 2x on its pitch of 30, filled under a mask to the best standing the bar ever reached (the rung is read
        /// from it) - on whole units, so the mask's edge is a pixel's.
        /// </summary>
        private float GoRungRow(float y, TycoonRun run)
        {
            var row = GoLine("Row.rung", y, GoRowH);
            float w = BarRating.MaxStars * TopStarPitch;
            var host = NewRect("Stars", row);
            Place(host, new Vector2(1f, 0.5f), new Vector2(w, TopStarH), Vector2.zero);
            var socket = ItemArt.Star(false, 16f);
            var lit = ItemArt.Star(true, 16f);
            for (int i = 0; i < BarRating.MaxStars; i++) GoStar(host, "S" + i, i, socket);
            var fill = NewRect("Fill", host);
            fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fill.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < BarRating.MaxStars; i++) GoStar(fill, "F" + i, i, lit);
            float reach = Mathf.Round((float)(run.Rating.BestStanding / BarRating.MaxStars) * w);
            fill.sizeDelta = new Vector2(reach, 0f);
            _goCounts.Add(k => fill.sizeDelta = new Vector2(Mathf.Round(reach * k), 0f));
            GoLabel(row, UIText.T(run.Rank.Title), 0f, w, TapeInk);
            return y + GoRowH;
        }

        private static void GoStar(RectTransform parent, string name, int i, Sprite art)
        {
            var star = NewRect(name, parent);
            star.anchorMin = star.anchorMax = new Vector2(0f, 0.5f);   // fixed x: the mask slides over it
            star.pivot = new Vector2(0.5f, 0.5f);
            star.sizeDelta = new Vector2(TopStarW, TopStarH);
            star.anchoredPosition = new Vector2(i * TopStarPitch + TopStarPitch * 0.5f, 0f);
            var img = star.gameObject.AddComponent<Image>();
            img.sprite = art;
            img.raycastTarget = false;
        }

        // ── the notice ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE NOTICE OF CLOSURE: the landlord's paper - the house card in the tape's cream, a red rule inside its edge -
        /// saying in the game's words how many nights in a row shut the bar (the count is DayLedger's, never written as
        /// "three"), the red nights themselves from the ledger (the till each closed at, what each took and paid), his
        /// signature, and his stamp on its own ground under the red nights, struck by the night stamp's own recipe (a copy:
        /// the night's stamp is never touched). The plate is as tall as its words, so a language whose sentence wraps
        /// longer grows the paper and the stamp moves down with it. Returns the notice's foot, in units down from the top.
        /// </summary>
        private float BuildGoNotice(TycoonRun run)
        {
            const float In = GoNoticeInset;
            var h = run.Ledger.History;
            int from = Mathf.Max(0, h.Count - DayLedger.StrikesToClose);
            int rows = h.Count - from;

            _goNotice = NewRect("Notice", _gameOverPanel);
            _goNotice.anchorMin = _goNotice.anchorMax = new Vector2(0.5f, 1f);
            _goNotice.pivot = new Vector2(0.5f, 1f);
            _goNoticeHome = new Vector2(GoNoticeX, -GoNoticeTop);
            _goNotice.anchoredPosition = _goNoticeHome;
            _goNotice.sizeDelta = new Vector2(GoNoticeW, 300f);
            var plate = _goNotice.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.Card();
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.Cream[4];
            plate.raycastTarget = false;
            _goNoticeFade = _goNotice.gameObject.AddComponent<CanvasGroup>();
            _goNoticeFade.blocksRaycasts = false;
            _goNoticeFade.interactable = false;

            // the words first: their height is the paper's
            var body = NewText("Body", _goNotice, _body, 16, TextAnchor.UpperCenter, TapeInk);
            var brt = body.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(GoNoticeW - 2f * In, 20f);     // 380
            brt.anchoredPosition = new Vector2(0f, -62f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.text = UIText.N("gameover.notice.body", DayLedger.StrikesToClose);
            float bodyH = Mathf.Max(20f, Mathf.Ceil(body.preferredHeight));
            brt.sizeDelta = new Vector2(brt.sizeDelta.x, bodyH);
            float rowsTop = 62f + bodyH + 14f;
            // THE STAMP HAS ITS OWN GROUND (2026-09-28, measured in play): struck on the words' middle it buried the one
            // sentence the paper exists to say. It lands under the red nights now, left of the paper's middle, where its
            // -9 degree tilt clears the last row's small print and the signature under its right end.
            float stampTop = rowsTop + rows * 40f + 4f;
            float signTop = stampTop + GoStampGround + 2f;
            float height = Mathf.Ceil((signTop + 12f + 22f) / 4f) * 4f;
            _goNotice.sizeDelta = new Vector2(GoNoticeW, height);

            var inner = NewRect("Inner", _goNotice);
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            Frame(inner, 2f, UITheme.ViceRed[1]);
            inner.SetSiblingIndex(0);   // under the words

            var title = NewText("Title", _goNotice, _display, 16, TextAnchor.MiddleCenter, UITheme.ViceRed[1]);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(400f, 20f), new Vector2(0f, -22f));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = UIText.T("gameover.notice.title");
            var rule = NewRect("Rule", _goNotice);
            Place(rule, new Vector2(0.5f, 1f), new Vector2(360f, 1f), new Vector2(0f, -50f));
            var ruleImg = rule.gameObject.AddComponent<Image>();
            ruleImg.color = UITheme.ViceRed[1];
            ruleImg.raycastTarget = false;

            // THE RED NIGHTS, oldest first: the night, the till it closed at, and what it took and paid
            for (int i = 0; i < rows; i++)
            {
                var r = h[from + i];
                var row = NewRect("Red" + i, _goNotice);
                Place(row, new Vector2(0.5f, 1f), new Vector2(GoNoticeW - 2f * In, 34f), new Vector2(0f, -(rowsTop + 40f * i)));
                var l = NewText("L", row, _body, 16, TextAnchor.MiddleLeft, TapeInk);
                Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(200f, 20f), Vector2.zero);
                l.horizontalOverflow = HorizontalWrapMode.Overflow;
                l.text = UIText.T("gameover.notice.night", ("n", r.Day));
                var v = NewText("V", row, _figures, 16, TextAnchor.MiddleRight, UITheme.ViceRed[1]);
                Place(v.rectTransform, new Vector2(1f, 1f), new Vector2(160f, 20f), Vector2.zero);
                v.horizontalOverflow = HorizontalWrapMode.Overflow;
                v.verticalOverflow = VerticalWrapMode.Overflow;
                CoinFigure(v, r.TillAfter, r.TillAfter < 0 ? "-" : "", account: true);
                var d = NewText("D", row, _body, 8, TextAnchor.MiddleLeft, TapeQuiet);
                Place(d.rectTransform, new Vector2(0f, 1f), new Vector2(GoNoticeW - 2f * In, 12f), new Vector2(0f, -22f));
                d.horizontalOverflow = HorizontalWrapMode.Overflow;
                d.text = UIText.T("gameover.notice.detail", ("in", r.Income), ("out", r.Expenses));
            }

            var sign = NewText("Sign", _goNotice, _body, 8, TextAnchor.MiddleRight, TapeQuiet);
            Place(sign.rectTransform, new Vector2(1f, 1f), new Vector2(GoNoticeW - 2f * In, 12f), new Vector2(-In, -signTop));
            sign.horizontalOverflow = HorizontalWrapMode.Overflow;
            sign.text = UIText.T("gameover.notice.sign");

            // THE STAMP, last, so it lands over the paper: the night stamp's construction (TycoonHud.Tape) - a plate at
            // the ink's 16 %, a five-unit rubber edge, a two-unit ring inside it, the heaviest face at 24 - resting at
            // -9 degrees on its own ground under the red nights. Parked hidden: it is not on the paper until it is struck.
            _goStamp = NewRect("Stamp", _goNotice);
            _goStamp.anchorMin = _goStamp.anchorMax = new Vector2(0.5f, 1f);
            _goStamp.pivot = new Vector2(0.5f, 0.5f);
            _goStamp.sizeDelta = new Vector2(256f, 54f);
            _goStamp.anchoredPosition = new Vector2(GoStampX, -(stampTop + GoStampGround * 0.5f));
            var red = UITheme.ViceRed[2];
            var stampPlate = _goStamp.gameObject.AddComponent<Image>();
            stampPlate.color = new Color(red.r, red.g, red.b, 0.16f);
            stampPlate.raycastTarget = false;
            Frame(_goStamp, 5f, new Color(red.r, red.g, red.b, 0.95f));
            var ring = NewRect("Inner", _goStamp);
            Stretch(ring, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            Frame(ring, 2f, new Color(red.r, red.g, red.b, 0.95f));
            _goStampInk = NewText("W", _goStamp, _shop != null ? _shop : _display, 24, TextAnchor.MiddleCenter,
                new Color(red.r, red.g, red.b, 1f));
            Stretch(_goStampInk.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
            _goStampInk.horizontalOverflow = HorizontalWrapMode.Overflow;
            _goStampInk.verticalOverflow = VerticalWrapMode.Overflow;
            _goStampInk.text = UIText.T("gameover.notice.stamp");
            _goStamp.gameObject.SetActive(false);

            return GoNoticeTop + height;
        }

        // ── the way out ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// ONE KEY, TO THE FRONT DOOR: MAIN MENU in the pack's orange - the screen's one primary - under the notice, and
        /// a small line under it saying the save went with the bar and where NEW RUN is. There is no NEW RUN here: one
        /// road, to the menu. Parked hidden and unarmed; the KEY beat raises it, DONE arms it.
        /// </summary>
        private void BuildGoKey(float noticeFoot)
        {
            float top = noticeFoot + 32f;
            _goKey = PackWordKey(_gameOverPanel, "MAIN MENU", UIText.T("chrome.pause.main_menu"), "home",
                MenuPack.Tone.Orange, new Vector2(0.5f, 1f), new Vector2(300f, 50f), new Vector2(GoNoticeX, -top),
                OnGameOverMainMenu, 300f, 72f);
            SurfaceKey(_goKey, MenuPack.Tone.Orange);
            _goKeyHome = _goKey.anchoredPosition;
            _goKeyButton = _goKey.GetComponent<Button>();
            _goKeyFade = _goKey.gameObject.AddComponent<CanvasGroup>();

            var note = NewText("Note", _gameOverPanel, _body, 8, TextAnchor.UpperCenter, UITheme.Cream[2]);
            Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(GoNoticeW, 12f),
                new Vector2(GoNoticeX, -(top + 50f + 12f)));
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.verticalOverflow = VerticalWrapMode.Overflow;
            note.text = UIText.T("gameover.key.note");
            note.rectTransform.sizeDelta = new Vector2(GoNoticeW, Mathf.Max(12f, Mathf.Ceil(note.preferredHeight)));
            _goNote = note.rectTransform;
            _goNoteHome = _goNote.anchoredPosition;
            _goNoteFade = note.gameObject.AddComponent<CanvasGroup>();
            _goNoteFade.blocksRaycasts = false;

            SetGoKeyArmed(false);
            _goKey.gameObject.SetActive(false);
            _goNote.gameObject.SetActive(false);
        }

        /// <summary>The key takes the pointer only once it is armed (its Button says so too, for the suite).</summary>
        private void SetGoKeyArmed(bool armed)
        {
            _goArmed = armed;
            if (_goKeyFade != null) { _goKeyFade.blocksRaycasts = armed; _goKeyFade.interactable = armed; }
            if (_goKeyButton != null) _goKeyButton.interactable = armed;
        }

        /// <summary>
        /// MAIN MENU: the front door comes up OVER the game over (31 over 23) and fades in from nothing, so the bar's end
        /// stays behind it until NEW RUN takes the room back (OnRunStarted puts it away). The panel is never hidden
        /// here, and nothing shuts the sheets after: that would let go of the hold the menu has just taken.
        /// </summary>
        private void OnGameOverMainMenu()
        {
            if (_goLeaving || !_goArmed) return;
            _goLeaving = true;
            Sfx.Play("menu_close", 0.6f);
            ShowMainMenu();
        }

        // ── the beats ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// One frame of the game over, after the night's show in Update. A click lands the PRINT and the COUNT; the dark,
        /// the sign, the notice and the stamp are objects and nothing skips them. Escape (UpdateEscape) lays the whole
        /// screen out.
        /// </summary>
        private void StepGameOver()
        {
            if (_goBeat == GoBeat.Off || !GameOverUp) return;
            float dt = Time.unscaledDeltaTime * Ceremony.Pace;
            StepGoShake();
            switch (_goBeat)
            {
                case GoBeat.Dark:
                {
                    // the dark lifts off the night show's, and the empty room comes up faintly; the beam still burns red
                    _goT += dt;
                    float k = Mathf.Clamp01(_goT / GoDark);
                    SetGoScrim(Mathf.Lerp(DayEndScrimA, GoScrimA, k * k * (3f - 2f * k)));
                    if (k < 1f) return;
                    _goBeat = GoBeat.Neon; _goT = 0f;
                    if (_neonTube != null) _goTubeFrom = _neonTube.color;
                    if (_neonBloom != null) _goBloomFrom = _neonBloom.color;
                    return;
                }
                case GoBeat.Neon:
                {
                    _goT += dt;
                    if (Motion.NoFlashes)
                    {
                        // FLASHES OFF: the sign fades out once, over 0.3 s, where it would sputter
                        float f = 1f - Mathf.Clamp01(_goT / NeonFade);
                        if (_neonTube != null)
                            _neonTube.color = new Color(_goTubeFrom.r, _goTubeFrom.g, _goTubeFrom.b, _goTubeFrom.a * f);
                        if (_neonBloom != null)
                            _neonBloom.color = new Color(_goBloomFrom.r, _goBloomFrom.g, _goBloomFrom.b, _goBloomFrom.a * f);
                    }
                    else
                    {
                        int cuts = 0;
                        foreach (float at in NeonCuts) if (_goT >= at) cuts++;
                        bool lit = (cuts & 1) == 1;
                        if (_neonTube != null && _neonTube.enabled != lit) _neonTube.enabled = lit;
                        if (_neonBloom != null && _neonBloom.enabled != lit) _neonBloom.enabled = lit;
                    }
                    if (_goT < GoNeon) return;
                    BeamOut();
                    Sfx.Play("screen_off", 0.6f);
                    // THE LID AND THE PRINT: the beam goes under the dark as the register feeds its last tape
                    _goBeat = GoBeat.Print; _goT = 0f;
                    Sfx.Play("bill_slip", 0.85f);
                    Sfx.HoldLoop("printer_feed", 0.5f);
                    return;
                }
                case GoBeat.Print:
                {
                    _goT += dt;
                    float k = Mathf.Clamp01(_goT / GoLid);
                    SetGoLid(Mathf.Lerp(GoLidOff, GoLidA, k * k * (3f - 2f * k)));
                    if (ClickedToSkip()) { LandGoPaper(); return; }
                    float h = Mathf.Min(_goTapeL, Mathf.Round(_goT * GoFeed));
                    SetGoTape(h);
                    if (h < _goTapeL) return;
                    Sfx.HoldLoop(null);
                    SetGoLid(GoLidA);
                    _goBeat = GoBeat.Count; _goT = 0f; _goCountTicked = 0;
                    return;
                }
                case GoBeat.Count:
                {
                    // the figures count top to bottom, one after another, as the night's do
                    _goT += dt;
                    if (ClickedToSkip()) { LandGoPaper(); return; }
                    float total = _goCounts.Count == 0 ? 0f : (_goCounts.Count - 1) * CountStep + CountEach;
                    for (int i = 0; i < _goCounts.Count; i++)
                    {
                        float k = Mathf.Clamp01((_goT - i * CountStep) / CountEach);
                        if (k > 0f && i >= _goCountTicked)
                        {
                            _goCountTicked = i + 1;
                            if (_goT < total) Sfx.Play("hover", 0.18f);
                        }
                        _goCounts[i](k <= 0f ? 0f : 1f - (1f - k) * (1f - k));
                    }
                    if (_goT < total) return;
                    Sfx.Play("cash", 0.6f);
                    BeginGoNotice();
                    return;
                }
                case GoBeat.Notice:
                {
                    _goT += dt;
                    float k = Mathf.Clamp01(_goT / GoNoticeIn);
                    SetGoNotice(Tweening.OutCubic(k));
                    if (k < 1f) return;
                    SetGoNotice(1f);
                    _goBeat = GoBeat.Stamp; _goT = 0f;
                    // shown huge, crooked and unprinted on its first frame: it can only fall (TycoonHud.Tape's ArmStamp)
                    if (_goStamp != null)
                    {
                        _goStamp.localScale = new Vector3(3.4f, 3.4f, 1f);
                        _goStamp.localRotation = Quaternion.Euler(0f, 0f, -26f);
                        SetGoStampInk(0f);
                        _goStamp.gameObject.SetActive(true);
                    }
                    return;
                }
                case GoBeat.Stamp:
                {
                    // the night stamp's fall: gathering pace all the way down, rocking, and stopping dead
                    _goT += dt;
                    float k = Mathf.Clamp01(_goT / StampFall);
                    if (_goStamp != null)
                    {
                        float e = k * k * k;
                        float s = Mathf.Lerp(3.4f, 1f, e);
                        _goStamp.localScale = new Vector3(s, s, 1f);
                        _goStamp.localRotation = Quaternion.Euler(0f, 0f,
                            Mathf.Lerp(-26f, -9f, e) + Mathf.Sin(k * Mathf.PI * 4f) * 3f * (1f - k));
                        SetGoStampInk(Mathf.Clamp01(k * 2.2f));
                    }
                    if (k < 1f) return;
                    RestGoStamp();
                    Sfx.Play("stamp", 0.95f);
                    _goShake = 1f;                          // the paper takes it
                    BeginGoKey();
                    return;
                }
                case GoBeat.Key:
                {
                    if (_goKeyT < 0f)
                    {
                        // THE KEY WAITS FOR THE LOST BAR'S CARD (LAST ORDERS, order 30 - the front door at 31 would bury
                        // it), for eight real seconds at most, and never comes inside the first half second: the card is
                        // queued a frame after the close, which a screen laid whole on its first frame would beat.
                        bool early = Time.unscaledTime - _goEnteredAt < GoArm;
                        bool card = AchievementCardBusy && Time.unscaledTime - _goKeyAskedAt < GoCardWait;
                        if (early || card) return;
                        _goKeyT = 0f;
                        if (_goKey != null) _goKey.gameObject.SetActive(true);
                        if (_goNote != null) _goNote.gameObject.SetActive(true);
                    }
                    _goKeyT += dt;
                    float k = Motion.Reduced ? 1f : Mathf.Clamp01(_goKeyT / GoKeyRise);
                    SetGoKey(1f - (1f - k) * (1f - k));
                    if (k < 1f) return;
                    _goBeat = GoBeat.Done;
                    return;
                }
                case GoBeat.Done:
                    // ARMED once it has landed and half a real second has passed since the screen went up
                    if (!_goArmed && Time.unscaledTime - _goEnteredAt >= GoArm) SetGoKeyArmed(true);
                    return;
            }
        }

        /// <summary>
        /// THE WHOLE SCREEN AT ONCE - reduced motion's first frame, and Escape before the key: the dark and the lid at
        /// rest, the sign OUT (no sputter, no switch heard), the tape out and every figure final, the notice home and the
        /// stamp on it - heard once, unless it already was or the scene is only being rebuilt round a closed bar
        /// (<paramref name="quiet"/>). The key then keeps its own rules: it waits for an achievement's card and arms
        /// half a second after the screen went up.
        /// </summary>
        private void PlaceWholeGameOver(bool quiet = false)
        {
            if (!GameOverUp) return;
            bool struck = _goBeat >= GoBeat.Key;
            SetGoScrim(GoScrimA);
            SetGoLid(GoLidA);
            if (!_beamOut) BeamOut();
            Sfx.HoldLoop(null);
            SetGoTape(_goTapeL);
            foreach (var count in _goCounts) count(1f);
            _goShake = 0f;
            SetGoNotice(1f);
            RestGoStamp();
            if (struck)
            {
                if (_goBeat == GoBeat.Key && _goKeyT >= 0f) _goKeyT = GoKeyRise;   // a rise under way lands
                return;
            }
            if (!quiet) Sfx.Play("stamp", 0.95f);
            BeginGoKey();
        }

        /// <summary>A click in the print or the count: the tape out, every figure final, the till's ring (the last row's
        /// cash), and on to the notice.</summary>
        private void LandGoPaper()
        {
            Sfx.HoldLoop(null);
            SetGoLid(GoLidA);
            SetGoTape(_goTapeL);
            foreach (var count in _goCounts) count(1f);
            Sfx.Play("cash", 0.6f);
            BeginGoNotice();
        }

        private void BeginGoNotice()
        {
            _goBeat = GoBeat.Notice; _goT = 0f;
            Sfx.Play("bill_slip", 0.6f);
            SetGoNotice(0f);
        }

        private void BeginGoKey()
        {
            _goBeat = GoBeat.Key;
            _goKeyAskedAt = Time.unscaledTime;
            _goKeyT = -1f;
        }

        /// <summary>THE SIGN IS OUT: the tube a dead Night glass, no light off it (RefreshTopBar paints the same OUT state
        /// from the next frame; this lands it in this one, so the lit tube is never seen again).</summary>
        private void BeamOut()
        {
            _beamOut = true;
            if (_neonTube != null) { _neonTube.enabled = true; _neonTube.color = UITheme.Night[2]; }
            if (_neonBloom != null) _neonBloom.enabled = false;
        }

        // ── the pieces the beats move ────────────────────────────────────────────────────────────────────────────────

        private void SetGoScrim(float a)
        {
            if (_goScrim == null) return;
            var c = _goScrim.color;
            _goScrim.color = new Color(c.r, c.g, c.b, a);
        }

        private void SetGoLid(float a)
        {
            if (_goLid == null) return;
            var c = _goLid.color;
            _goLid.color = new Color(c.r, c.g, c.b, a);
        }

        /// <summary>How much of the report is out, and its torn foot riding the edge.</summary>
        private void SetGoTape(float h)
        {
            if (_goRoll == null) return;
            _goRoll.sizeDelta = new Vector2(TapeW, h);
            if (_goFoot == null) return;
            bool on = h > 0f;
            if (_goFoot.gameObject.activeSelf != on) _goFoot.gameObject.SetActive(on);
            _goFoot.anchoredPosition = new Vector2(0f, -h);
        }

        /// <summary>The notice in from its right, alpha with it, on whole units.</summary>
        private void SetGoNotice(float e)
        {
            if (_goNotice == null) return;
            _goNotice.anchoredPosition = _goNoticeHome + new Vector2(Mathf.Round(GoSlide * (1f - e)), 0f);
            if (_goNoticeFade != null) _goNoticeFade.alpha = e;
        }

        private void SetGoStampInk(float a)
        {
            if (_goStampInk == null) return;
            var c = _goStampInk.color;
            _goStampInk.color = new Color(c.r, c.g, c.b, a);
        }

        private void RestGoStamp()
        {
            if (_goStamp == null) return;
            _goStamp.localScale = Vector3.one;
            _goStamp.localRotation = Quaternion.Euler(0f, 0f, -9f);
            SetGoStampInk(1f);
            _goStamp.gameObject.SetActive(true);
        }

        /// <summary>The key and its note rising into place, fading up with the rise, on whole units.</summary>
        private void SetGoKey(float e)
        {
            float drop = Mathf.Round((1f - e) * GoKeyLift);
            if (_goKey != null) _goKey.anchoredPosition = _goKeyHome - new Vector2(0f, drop);
            if (_goKeyFade != null) _goKeyFade.alpha = e;
            if (_goNote != null) _goNote.anchoredPosition = _goNoteHome - new Vector2(0f, drop);
            if (_goNoteFade != null) _goNoteFade.alpha = e;
        }

        /// <summary>The notice takes the stamp's blow: it shakes, on whole units, and settles over a quarter second.</summary>
        private void StepGoShake()
        {
            if (_goShake <= 0f || _goNotice == null) return;
            _goShake = Mathf.Max(0f, _goShake - Time.unscaledDeltaTime * Ceremony.Pace / GoShakeFor);
            float amp = _goShake * _goShake * 7f;
            _goNotice.anchoredPosition = _goNoticeHome + new Vector2(
                Mathf.Round(Mathf.Sin(Time.unscaledTime * 62f) * amp * 0.5f),
                Mathf.Round(Mathf.Sin(Time.unscaledTime * 47f) * amp));
            if (_goShake <= 0f) _goNotice.anchoredPosition = _goNoticeHome;
        }
    }
}
