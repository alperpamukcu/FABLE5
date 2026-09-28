using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part NightShow: the night's end as ONE show (2026-09-28, the author: "Gün sonu ekranı en baştan
    // tekrardan tasarla kendin araştır ve bu tarz oyunlardaki gün sonu sanat tasarımını oyuna ekle"; the reviewed spec's
    // defaults, "Yeni önerilenleri uygula").
    //
    // The bar's light goes down, the night is called on the dark, and the till's own tape unrolls from the top of the
    // screen - the author's PixelLab stock, re-cut to the night's length - while the week's bill hook and the TOMORROW
    // board come in from their sides. The tape counts itself, top to bottom; the pen rings the lower of SERVICE and
    // COMFORT and runs down to the stars the night is filed at; the stars drop; the stamp strikes; the counterfoil is
    // torn off, folded and hung on the week's hook; the standing climbs; the way on waits for all of it. Every clock is
    // the one the author already asked for (the slow feed, the stars one by one, the struck stamp) and runs on
    // Ceremony.Pace; Motion.Reduced lays the whole night out at once.
    //
    // The three columns are TycoonHud.Tape (the tape), TycoonHud.Week (the hook) and TycoonHud.Tomorrow (the critics and
    // the board). This file owns the order of the beats and nothing else: the builders lay things out, the beats move
    // them, and there is ONE way to the end of the show (FinishShow), so the Sunday edition has one door to come in by.
    public sealed partial class TycoonHud
    {
        /// <summary>The show's beats, in the order they play. Edition is reserved for the Sunday edition (next pass):
        /// it is never entered today.</summary>
        private enum NightBeat { Off, Lights, Call, Print, Count, Grade, Stars, Stamp, Tear, Climb, Edition, Done }

        private NightBeat _show = NightBeat.Off;
        /// <summary>Seconds into the beat in progress, paced.</summary>
        private float _showT;
        /// <summary>Seconds into the unroll; the side columns ride the same clock.</summary>
        private float _tapeT;

        /// <summary>The night's sheet: everything the show lays out, one rect over the panel, so the way out moves it
        /// whole. It never takes the pointer (its CanvasGroup), and CONTINUE is built after it, on top.</summary>
        private RectTransform _nightSheet;
        private CanvasGroup _nightGroup;
        /// <summary>The room's light going down: the day-end panel's own Image.</summary>
        private Image _dayEndScrim;
        private CanvasGroup _leftGroup, _rightGroup, _tomorrowGroup;
        private RectTransform _weekRoot, _criticsRoot, _tomorrowRoot;
        private Vector2 _weekHome, _criticsHome, _tomorrowHome;

        // ── the clocks (Pace 1; every one of them today's or the spec's §1.2) ────────────────────────────────────────
        /// <summary>The author's darkness (2026-09-08, asked twice: 0.88, 0.965, 0.988) - arriving now rather than
        /// simply being there, so the bar's light is seen going down.</summary>
        private const float DayEndScrimA = 0.988f;
        private const float LightsDur = 0.60f;
        /// <summary>The bounce at the foot of the unroll: the rig dips twice, 8 then about 2, in whole units.</summary>
        private const float TapeDip = 8f;
        /// <summary>The critics' scores count over the columns' last stretch in, before any tape figure moves.</summary>
        private const float CriticCount = 0.30f;
        private const float GradeFill = 0.25f, PenLead = 0.10f;
        private const float TearDur = 0.20f, FlyDur = 0.40f, HangDur = 0.30f, FoldDur = 0.15f;
        private const float ChipPop = 0.20f, RungSwap = 0.20f;
        /// <summary>How far the side columns come in from.</summary>
        private const float ColumnSlide = 54f;
        /// <summary>The field the columns are laid in: 704 of the 720 rows, eight clear above and below.</summary>
        private const float NightField = 704f;

        /// <summary>The climb's own steps inside its beat: the standing, then the chip, then the rung swap.</summary>
        private int _climbStage;
        private float _swapT = -1f;
        private readonly List<CanvasGroup> _swapFrom = new List<CanvasGroup>();
        private readonly List<CanvasGroup> _swapTo = new List<CanvasGroup>();

        /// <summary>The ladder's window is up over the night (the certificate): the way on waits under it.</summary>
        private bool LadderUp => _ladderPanel != null && _ladderPanel.gameObject.activeSelf;

        /// <summary>The Sunday edition's condition (next pass, spec §3): tonight closes the week.</summary>
        private static bool WeekClosesTonight(TycoonRun run) =>
            run != null && BarCalendar.NightOf(run.Day) == BarNight.Saturday;

        // ── the show's door in ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Lays the night's sheet out and starts the show. Everything the sheet shows is built here, once, at its rest
        /// pose - and then parked (the columns off their edges, the tape rolled up, the figures blank, the stars above
        /// their places) for the beats to bring in.
        /// </summary>
        private void BuildNightSheet(TycoonRun run)
        {
            if (_nightSheet == null || run == null) return;
            // Last night's sheet goes at once: switched off first, so nothing finds its objects in the frame before
            // the Destroy lands.
            foreach (Transform old in _nightSheet) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            _billCounts.Clear();
            _criticCounts.Clear();
            _billStars.Clear();
            _billStamp = null; _billStampInk = null;
            _swapFrom.Clear(); _swapTo.Clear();
            _swapT = -1f; _climbStage = 0;
            _standStars = null; _standFill = _standFillGhost = null;
            _standNumber = _standDelta = null;
            _standDeltaChip = _standWasTick = null; _standDeltaArrow = null;
            _nightSheet.anchoredPosition = Vector2.zero;
            if (_nightGroup != null) { _nightGroup.alpha = 1f; _nightGroup.blocksRaycasts = false; }

            float gTape = BuildTape(run, _nightSheet);
            float gLeft = BuildWeek(run, _nightSheet);
            float gCritics = BuildCritics(run, _nightSheet);
            float over = gCritics > 0f ? gCritics + 12f : 0f;
            float gTomorrow = BuildTomorrow(run, _nightSheet, NightField - over);
            float gRight = over + gTomorrow;

            // ONE TOP LINE (spec §2.2): the three columns top-align at T, and T centres the tallest in the field.
            float tallest = Mathf.Max(gTape, Mathf.Max(gLeft, gRight));
            float t = Mathf.Floor((NightField * 0.5f - (NightField - tallest) * 0.5f) / 4f) * 4f;

            _zRigHome = new Vector2(0f, t);
            _zRig.anchoredPosition = _zRigHome;
            _weekHome = new Vector2(-438f, t);
            _weekRoot.anchoredPosition = _weekHome;
            _weekTop = t;
            if (_criticsRoot != null)
            {
                _criticsHome = new Vector2(438f, t);
                _criticsRoot.anchoredPosition = _criticsHome;
            }
            _tomorrowHome = new Vector2(438f, t - over);
            _tomorrowRoot.anchoredPosition = _tomorrowHome;

            // THE WAY ON STANDS WHERE THE COUNTERFOIL HUNG (Build.cs, "ON THE SLIP'S FOOT"): eight under the body.
            if (_billNext != null)
                _billNext.anchoredPosition = new Vector2(0f, t - _lBody - 8f - BillKeyH * 0.5f);
            if (_billNextLabel != null) _billNextLabel.text = UIText.T("dayend.bill.next");

            // Parked for the beats: nothing of the night is on the screen while it is called.
            if (!Motion.Reduced)
            {
                SetColumnsIn(0f);
                SetTapeLength(0f);
                foreach (var count in _billCounts) count(0f);
                foreach (var count in _criticCounts) count(0f);
                SetGrade(0f, 0f);
            }
        }

        // ── the beats ─────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// One frame of the show (spec §1.3). A click is read for the beat in progress only and never stored: COUNT,
        /// GRADE, STARS and CLIMB land at once; the dark, the call, the paper, the stamp's blow and the counterfoil are
        /// objects, and nothing skips them (2026-08-11: "nothing skips the paper coming in").
        /// </summary>
        private void StepNightShow()
        {
            if (_show == NightBeat.Off) return;
            var run = Run;
            if (run == null) return;
            float dt = Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace;
            StepTapeShake();

            switch (_show)
            {
                case NightBeat.Lights:
                {
                    _showT += dt;
                    float k = Mathf.Clamp01(_showT / LightsDur);
                    SetNightScrim(DayEndScrimA * k * k * (3f - 2f * k));
                    if (k < 1f) return;
                    _show = NightBeat.Call; _showT = 0f;
                    if (_lastCallRt != null)
                    {
                        _lastCallRt.gameObject.SetActive(true);
                        _lastCallGroup.alpha = 0f;
                    }
                    return;
                }
                case NightBeat.Call:
                {
                    _showT += dt;
                    float a = _showT < CallIn ? _showT / CallIn
                            : _showT < CallIn + CallHold ? 1f
                            : 1f - Mathf.Clamp01((_showT - CallIn - CallHold) / CallOut);
                    if (_lastCallGroup != null) _lastCallGroup.alpha = a;
                    if (_lastCallCard != null)
                    {
                        // It settles as it arrives - a line that lands rather than appears.
                        float k = Mathf.Clamp01(_showT / CallIn);
                        _lastCallCard.rectTransform.anchoredPosition = new Vector2(0, 10f + Mathf.Round((1f - k) * 14f));
                    }
                    if (_showT < CallIn + CallHold + CallOut) return;
                    if (_lastCallRt != null) _lastCallRt.gameObject.SetActive(false);
                    _show = NightBeat.Print; _showT = 0f; _tapeT = 0f;
                    Sfx.Play("bill_slip", 0.85f);
                    Sfx.HoldLoop("printer_feed", 0.5f);
                    Sfx.Play("whoosh", 0.25f);
                    return;
                }
                case NightBeat.Print:
                    if (!StepUnroll(dt)) return;
                    Sfx.HoldLoop(null);
                    _show = NightBeat.Count; _showT = 0f; _billCountTicked = 0;
                    return;
                case NightBeat.Count:
                {
                    _showT += dt;
                    float total = _billCounts.Count == 0 ? 0f : (_billCounts.Count - 1) * CountStep + CountEach;
                    if (ClickedToSkip()) _showT = total;
                    for (int i = 0; i < _billCounts.Count; i++)
                    {
                        float k = Mathf.Clamp01((_showT - i * CountStep) / CountEach);
                        if (k > 0f && i >= _billCountTicked)
                        {
                            _billCountTicked = i + 1;
                            if (_showT < total) Sfx.Play("hover", 0.18f);
                            // the till in the red is heard as its own figure starts, not after the counting
                            if (i == _tillCount && run.Money < 0) Sfx.Play("debt_alarm", 0.8f);
                        }
                        _billCounts[i](k <= 0f ? 0f : 1f - (1f - k) * (1f - k));
                    }
                    if (_showT < total) return;
                    _show = NightBeat.Grade; _showT = 0f;
                    Sfx.Play("hover", 0.25f);
                    return;
                }
                case NightBeat.Grade:
                {
                    _showT += dt;
                    if (ClickedToSkip()) _showT = GradeFill + PenLead;
                    SetGrade(Mathf.Clamp01(_showT / GradeFill), Mathf.Clamp01((_showT - GradeFill) / PenLead));
                    if (_showT < GradeFill + PenLead) return;
                    _show = NightBeat.Stars; _showT = 0f;
                    StartStarDrop((float)(run.TonightStars / BarRating.MaxStars));
                    return;
                }
                case NightBeat.Stars:
                {
                    _showT += dt;
                    // A click lands every star still in the air (seventh list) - one sound, one shake.
                    if (_starT >= 0f && ClickedToSkip()) _starT = 99f;
                    float run5 = Mathf.Max(0, _starCount - 1) * StarStagger + StarDrop;
                    SetStarFigure(_starT < 0f ? 1f : Mathf.Clamp01(_showT / run5));
                    if (_starT >= 0f) return;
                    SetStarFigure(1f);
                    if (_stampT >= 0f) { _show = NightBeat.Stamp; return; }
                    if (_billShake > 0f) return;
                    BeginTear(run);
                    return;
                }
                case NightBeat.Stamp:
                    // The stamp's blow is an object too: it lands, the paper takes it, and only then does anything tear.
                    if (_stampT >= 0f || _billShake > 0f) return;
                    BeginTear(run);
                    return;
                case NightBeat.Tear:
                    if (!StepTear(dt)) return;
                    BeginClimb(run);
                    return;
                case NightBeat.Climb:
                    StepClimb(run, dt);
                    return;
                case NightBeat.Done:
                    ShowWayOn();
                    return;
            }
        }

        /// <summary>The climb's three steps: the standing (zero long when it held), the chip's pop, the rung's swap -
        /// then the certificate is offered and the show ends. A click lands all three.</summary>
        private void BeginClimb(TycoonRun run)
        {
            _show = NightBeat.Climb; _showT = 0f;
            _climbStage = 0;
            StartStandingClimb();
        }

        private void StepClimb(TycoonRun run, float dt)
        {
            if (ClickedToSkip())
            {
                if (_standT >= 0f) _standT = StandClimb;
                _chipPop = 0f;
                if (_standDeltaChip != null) _standDeltaChip.localScale = Vector3.one;
                if (_swapT >= 0f) _swapT = RungSwap;
            }
            if (_climbStage == 0)
            {
                StepStandingClimb();
                if (_standT >= 0f) return;
                _climbStage = 1;
            }
            if (_climbStage == 1)
            {
                if (_chipPop > 0f) return;
                _climbStage = 2;
                bool crossed = _swapTo.Count > 0;
                _swapT = crossed ? 0f : -1f;
            }
            if (_climbStage == 2)
            {
                if (_swapT >= 0f)
                {
                    _swapT += dt;
                    SetRungSwap(Mathf.Clamp01(_swapT / RungSwap));
                    if (_swapT < RungSwap) return;
                    SetRungSwap(1f);
                    _swapT = -1f;
                }
                _climbStage = 3;
                // The beat the climb lands: if it crossed a rung, the window (2026-09-21) - and the way on waits under it.
                OfferLadderForTonight(run);
                FinishShow(run);
            }
        }

        /// <summary>
        /// THE ONE EXIT (spec §3): every path to the end of the show - timed, clicked or reduced - comes through here.
        /// The Sunday edition (next pass) inserts itself at this door, and CONTINUE then follows the edition, so the
        /// suite's door does not move.
        /// </summary>
        private void FinishShow(TycoonRun run)
        {
            // (next pass: if (WeekClosesTonight(run) && EditionShips) { _show = NightBeat.Edition; ... return; })
            _show = NightBeat.Done;
            _showT = 0f;
            ShowWayOn();
        }

        /// <summary>
        /// CONTINUE is up only once the show is over, only on the night's own step, and only while the certificate is
        /// down - the two used to come up in one frame, and the first click on the certificate's dim was lost to it.
        /// Asked every frame after the show, and by every rebuild.
        /// </summary>
        private void ShowWayOn()
        {
            if (_billNext == null) return;
            bool on = _dayEndStep == 0 && _show == NightBeat.Done && !LadderUp;
            if (_billNext.gameObject.activeSelf != on) _billNext.gameObject.SetActive(on);
        }

        /// <summary>
        /// REDUCED MOTION: the whole night at its rest pose on the show's first frame (the 3 s beat before it still
        /// waits) - the dark down, the columns in, every figure final, the grade drawn, the stars placed, the stamp at
        /// rest, the counterfoil already hung and the foot torn, the standing at NOW - and the certificate still offered
        /// (the old screen never offered it under reduced motion, §9.5).
        /// </summary>
        private void PlaceWholeNight(TycoonRun run)
        {
            SetNightScrim(DayEndScrimA);
            if (_lastCallRt != null) _lastCallRt.gameObject.SetActive(false);
            SetColumnsIn(1f);
            foreach (var count in _criticCounts) count(1f);
            LandTape();
            foreach (var count in _billCounts) count(1f);
            SetGrade(1f, 1f);
            StartStarDrop((float)(run.TonightStars / BarRating.MaxStars));
            if (_stampKind != StampKind.None) Sfx.Play("stamp", 0.95f);
            SetStarFigure(1f);
            TearAtOnce();
            StartStandingClimb();
            SetRungSwap(1f);
            _climbStage = 3;
            OfferLadderForTonight(run);
            FinishShow(run);
        }

        // ── the pieces the beats move ────────────────────────────────────────────────────────────────────────────────

        private void SetNightScrim(float a)
        {
            if (_dayEndScrim == null) return;
            var c = _dayEndScrim.color;
            _dayEndScrim.color = new Color(c.r, c.g, c.b, a);
        }

        /// <summary>Both side columns from their own edges, alpha with them, on an out-cubic - whole units, since
        /// they are all pixel face.</summary>
        private void SetColumnsIn(float k)
        {
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            float off = Mathf.Round(ColumnSlide * (1f - e));
            if (_weekRoot != null) _weekRoot.anchoredPosition = _weekHome - new Vector2(off, 0f);
            if (_criticsRoot != null) _criticsRoot.anchoredPosition = _criticsHome + new Vector2(off, 0f);
            if (_tomorrowRoot != null) _tomorrowRoot.anchoredPosition = _tomorrowHome + new Vector2(off, 0f);
            if (_leftGroup != null) _leftGroup.alpha = e;
            if (_rightGroup != null) _rightGroup.alpha = e;
            if (_tomorrowGroup != null) _tomorrowGroup.alpha = e;
        }

        /// <summary>The rung's swap: the rung the bar stood on fades out and the one tonight leaves it on fades in -
        /// the title, the NEXT RUNG block, the gauge's notch and a rank job's progress.</summary>
        private void SetRungSwap(float k)
        {
            if (_swapTo.Count == 0) return;
            foreach (var g in _swapFrom) if (g != null) g.alpha = 1f - k;
            foreach (var g in _swapTo) if (g != null) g.alpha = k;
        }

        /// <summary>The shake lives here, on the rig, so the tape, its foot and the stamp are only ever moved by one
        /// thing. Stepped every frame of the show (idempotent once it has died away).</summary>
        private void StepTapeShake()
        {
            if (_billShake <= 0f || _zRig == null) return;
            _billShake = Mathf.Max(0f, _billShake - Time.unscaledDeltaTime * 4.5f * LastCall.Game.Ceremony.Pace);
            float amp = _billShake * _billShake * 7f;   // dies away fast, like a strike
            _zRig.anchoredPosition = _zRigHome + new Vector2(
                Mathf.Round(Mathf.Sin(Time.unscaledTime * 62f) * amp * 0.5f),
                Mathf.Round(Mathf.Sin(Time.unscaledTime * 47f) * amp));
            if (_billShake <= 0f) _zRig.anchoredPosition = _zRigHome;
        }
    }
}
