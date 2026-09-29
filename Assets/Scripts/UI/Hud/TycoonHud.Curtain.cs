using System;
using System.Collections.Generic;
using System.Globalization;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Curtain: the black between two nights, and the club's night sign that lights in it.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    //
    // (The curtain was rebuilt whole on 2026-09-28 and keeps its own state here, the way the game over does: a stage
    //  read whole in one file.)
    //
    // ── THE NIGHT SIGN (2026-09-28) ──────────────────────────────────────────────────────────────────────────────────
    //
    // The author picked it off the curtain mocks: direction A, "the marquee". The pink card that stood in the dark -
    // the house panel with a sky, a clock, two names crossfading in one seat and the week instrument scaled 1.4x under
    // them - is gone, and what lights in the black between two nights is the CLUB'S OWN SIGN: one neon tube bent round
    // the sky window, the hour wound on the beam's segment clock at 6x, tonight's name turning over on a split-flap
    // board, and the week as seven tube fittings. The critic's three fixes over the mock are in:
    //
    //   * THE SKY IS SMOOTH. The mock dithered it, and the author took exactly that dither out of the room's window on
    //     2026-09-22 ("pixel pixel gün batımı"). One flat colour per row off the window's own model (SkyClock), its
    //     02:00 and 18:00 the window's own keys, so the lift never cuts from one sky to another.
    //   * THE BOARD IS ONE HOUSING. A row of equal boxes is the style guide's loudest tell ("kutu kutu"): the flaps sit
    //     in one case behind one lip with hairline seams, and the week number is the sign's only foot.
    //   * THE WEEK READS. At 1x in the 16 face (the old 1.4x put Silkscreen 16 at 22.4 and the well off the grid),
    //     spent nights in Cream[2] (6:1 on the black, not Night[4]'s 1.8:1), and the sign's tube burns HALF through
    //     the day and strikes full at 18:00, instead of standing dark for four of its seven seconds.
    //
    // It also fixes a tint that never worked: the old city was pushed ABOVE white to lighten it by day, and uGUI writes
    // vertex colour as Color32, which clamps at 1 - the skyline was always its dark art. It is drawn untinted now; the
    // daylight is the sky's job.
    public sealed partial class TycoonHud
    {
        // ── the sign's geometry (sign-local units: y down from its top edge, x from its centre) ─────────────────────

        /// <summary>The sign's outer rim, 688×536, centred on the field - the mock's 296,92 → 984,628.</summary>
        private const float SignW = 688f, SignH = 536f;
        /// <summary>The tube's corner, in texels of its half-size drawing (12 units on screen).</summary>
        private const int SignChamfer = 6;
        /// <summary>The sky window: 320×112 texels shown at exactly 2×, 24 in from the rim.</summary>
        private const float SignSkyTop = 24f;
        /// <summary>The top of the hour's ink: SegmentClock's 110×28 glass at localScale 3 (the art at 6×).</summary>
        private const float SignClockTop = 272f;
        /// <summary>The split-flap board: flaps 32×44, the hinge across at 22, faces to 42 and the lip under them.</summary>
        private const float SignBoardTop = 388f;
        private const int FlapW = 32, FlapH = 44, FlapHinge = 22, FlapFaces = 42, FlapInkTop = 12, MaxFlaps = 16;
        /// <summary>The week: seven columns at 72, the word's ink top, the rim top of the tube under it.</summary>
        private const float NightPitch = 72f, NightInkTop = 460f, NightTubeTop = 480f;
        /// <summary>The bottom tube's own centre line, where the week number sits in its break.</summary>
        private const float SignPlateY = 533f;
        /// <summary>Air between the week plate and the ends of the tube it breaks, either side.</summary>
        private const float SignPlateAir = 20f;
        /// <summary>The Sunday shutter: 520 wide over the week row, rolling down 48 in steps of 4 out of a 6-unit box.</summary>
        private const float SignShutterTop = 452f, SignShutterW = 520f, SignShutterDrop = 48f;

        // ── the timeline (seconds at Ceremony.Pace 1) ────────────────────────────────────────────────────────────────
        //
        // SEVEN SECONDS, NOT MORE (2026-08-15, the author: "gün geçişinde takvim gözüktüğü sahne daha yavaş aksın, şu an 3
        // saniye ise 6 saniye olsun" - a scene waits for you; past seven a rest turns into a wait). 6.80 an ordinary
        // night, 8.60 when Saturday hands to Monday and the Sunday is played through, 1.52 under reduced motion. All of
        // them stay under the 9.6 the suite's 1.2 s wait at pace 8 allows.

        private const float CurtainFadeIn = 0.40f;       // the sign comes in over four alpha steps
        private const float CurtainDay = 3.60f;          // 02:00 → 18:00
        private const float CurtainDaySunday = 5.60f;    // 02:00 → Sunday → 18:00 Monday (hours 2 → 42)
        private const float CurtainHold = 1.20f;         // the hour stands where it landed
        private const float CurtainHoldSunday = 1.00f;
        private const float CurtainLift = 1.60f;         // the sign goes out, then the room comes up
        private const float SignOff = 0.32f;             // ...the sign in four steps, the black still whole
        private const float ReducedHold = 1.20f;         // reduced motion: the finished frame, held, then out
        private const float FlapLead = 0.20f, FlapStagger = 0.06f, FlapStep = 0.08f;
        private const float LeavingOut = 1.75f, ArrivingStrike = 1.95f, TubeOut = 0.15f;
        private const float SaturdayOut = 1.00f, ShutterDown = 1.40f, ShutterRoll = 0.40f;

        // ── the sign's parts ─────────────────────────────────────────────────────────────────────────────────────────

        private RectTransform _curtain;
        private Image _curtainImg;
        private RectTransform _sign;
        private CanvasGroup _signGroup;
        private SignNeon _signTube;
        private CurtainSky _signSky;
        private RectTransform _signSun, _signMoon;
        private Image _signSunImg, _signMoonImg;
        private SegmentClock _curtainClock;
        private RectTransform _signBoard, _boardCase, _boardFaces, _boardHinge, _boardLip;
        private readonly Image[] _boardSeams = new Image[MaxFlaps - 1];
        private readonly SignFlap[] _flaps = new SignFlap[MaxFlaps];
        private readonly SignFlap[] _weekFlaps = new SignFlap[2];
        private readonly Text[] _nightWords = new Text[7];
        private readonly SignNeon[] _nightTubes = new SignNeon[BarCalendar.OpenNights];
        private Image _nightStar;
        private RectTransform _shutterBlind;
        private Image _shutterBlindImg, _shutterBox;

        // ── one opening, read once on the way in ────────────────────────────────────────────────────────────────────

        private int _curtainFrom = 1, _curtainTo = 1;
        private int _curtainStoryNight = -1;   // which night of the ARRIVING week the arc is due on, or -1
        /// <summary>Seconds into this opening, and how long it runs. Both 0 until the first opening: the HUD used to boot
        /// with a phantom curtain - its clock at 0 of 7 with the curtain switched off - which held the night's clock and
        /// played the door's sound under the front door, and again seven seconds into a night after a language reload.</summary>
        private float _curtainT, _curtainTotal;
        private float _curtainDay, _curtainLand, _curtainLiftAt, _curtainHourTo, _curtainMidnight, _curtainMonStrike;
        private bool _curtainSunday, _curtainReduced, _curtainMagenta;
        private int _flapCount, _weekFrom, _weekTo;
        private string[] _flapA, _flapB, _flapC;
        private string[][] _seqAB, _seqBC;

        // ── building ─────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the sign once; OpenTheDoors writes the night into it and StepCurtain drives it. Nothing here allocates
        /// while the sign is lit - a blackout that allocates is a blackout that hitches on the one frame the player is
        /// only looking at it - so every flap the longest name could need is built now and switched on per opening.
        /// </summary>
        private void BuildCurtainSign(RectTransform curtain)
        {
            _sign = NewRect("CurtainSign", curtain);
            Place(_sign, new Vector2(0.5f, 0.5f), new Vector2(SignW, SignH), Vector2.zero);
            _signGroup = _sign.gameObject.AddComponent<CanvasGroup>();
            _signGroup.alpha = 0f;
            _signGroup.blocksRaycasts = false;
            _signGroup.interactable = false;

            // The week plate is measured first: the tube is bent with a break exactly as wide as the plate that sits in
            // it, in the language being spoken, and it is drawn behind everything else on the sign.
            var plate = BuildWeekPlate(out float plateW);
            int gap = Mathf.CeilToInt((plateW + SignPlateAir * 2f) / 4f) * 4;   // whole texels either side of the centre
            _signTube = new SignNeon(_sign, "SignTube",
                layer => ChromeArt.NeonPath((int)(SignW / 2f), (int)(SignH / 2f), SignChamfer, gap / 2, layer),
                new Vector2(SignW + ChromeArt.NeonPad * 4f, SignH + ChromeArt.NeonPad * 4f),
                new Vector2(0.5f, 0.5f), Vector2.zero);

            BuildSignSky();

            // THE HOUR, WOUND - the beam's own readout at 6× the art (a whole multiple, so it stays on the grid). The
            // host's top IS the ink's top: SegmentClock hangs its cells off the glass's left-middle and the numeral fills
            // the glass's 28 rows.
            var host = NewRect("SignHour", _sign);
            Place(host, new Vector2(0.5f, 1f), new Vector2(110f, 28f), new Vector2(0f, -SignClockTop));
            host.localScale = new Vector3(3f, 3f, 1f);
            _curtainClock = new SegmentClock(host, UITheme.Cyan[4]);

            BuildFlapBoard();
            BuildSignNights();

            // THE SUNDAY SHUTTER: a blind that rolls down out of its box over the whole week while the bar is shut, and
            // back up on the new one. It slides inside a mask, so its slats move with its bar the way a real one does.
            var shutter = NewRect("SignShutter", _sign);
            Place(shutter, new Vector2(0.5f, 1f), new Vector2(SignShutterW, SignShutterDrop + 2f), new Vector2(0f, -SignShutterTop));
            shutter.gameObject.AddComponent<RectMask2D>();
            _shutterBlind = NewRect("Blind", shutter);
            Place(_shutterBlind, new Vector2(0.5f, 1f), new Vector2(SignShutterW, SignShutterDrop + 2f), new Vector2(0f, SignShutterDrop + 2f));
            _shutterBlindImg = _shutterBlind.gameObject.AddComponent<Image>();
            _shutterBlindImg.sprite = ChromeArt.RollerShutter((int)(SignShutterW / 2f), 0, (int)((SignShutterDrop - 6f) / 2f), 3, 12, 0);
            _shutterBlindImg.raycastTarget = false;
            _shutterBlindImg.enabled = false;
            var box = NewRect("ShutterBox", _sign);
            Place(box, new Vector2(0.5f, 1f), new Vector2(SignShutterW + 8f, 6f), new Vector2(0f, -(SignShutterTop - 6f)));
            _shutterBox = box.gameObject.AddComponent<Image>();
            _shutterBox.color = UITheme.Night[4];
            _shutterBox.raycastTarget = false;
            _shutterBox.enabled = false;

            plate.SetAsLastSibling();   // the week number in the tube's break, over the tube's own light
        }

        /// <summary>
        /// THE SKY WINDOW: the window's own model drawn smooth into a 320×112 texture shown at 2×, the moon and the sun
        /// over it, and the city in front of both - they rise from behind the towers and set back behind them. The
        /// window's mask cuts the sun off below the bay.
        /// </summary>
        private void BuildSignSky()
        {
            var sky = NewRect("SignSky", _sign);
            Place(sky, new Vector2(0.5f, 1f), new Vector2(CurtainSky.W * 2f, CurtainSky.H * 2f), new Vector2(0f, -SignSkyTop));
            sky.gameObject.AddComponent<RectMask2D>();
            _signSky = new CurtainSky(SkyClock.Load());
            var skyImg = sky.gameObject.AddComponent<RawImage>();
            skyImg.texture = _signSky.Texture;
            skyImg.raycastTarget = false;

            // The moon first, because the sun rises through where it has been. Scene/curtain_moon also hangs in the
            // room's window (WindowSky.HangMoon): it stays.
            _signMoon = NewRect("SignMoon", sky);
            Place(_signMoon, new Vector2(0f, 1f), new Vector2(48f, 48f), Vector2.zero);
            _signMoon.pivot = new Vector2(0.5f, 0.5f);
            _signMoonImg = _signMoon.gameObject.AddComponent<Image>();
            _signMoonImg.sprite = Resources.Load<Sprite>("Scene/curtain_moon");
            _signMoonImg.raycastTarget = false;

            _signSun = NewRect("SignSun", sky);
            Place(_signSun, new Vector2(0f, 1f), new Vector2(64f, 64f), Vector2.zero);
            _signSun.pivot = new Vector2(0.5f, 0.5f);
            _signSunImg = _signSun.gameObject.AddComponent<Image>();
            _signSunImg.sprite = Resources.Load<Sprite>("Scene/curtain_sun");
            _signSunImg.raycastTarget = false;

            // THE CITY - generated art (2026-08-25, the author: "kullanılan mevcut görsel profesyonelce durmuyor,
            // gerekirse görsel ve animasyonu üret"; Tools/day_sky_gen.py), 320×96 at a whole 2×, UNTINTED.
            var city = NewRect("SignCity", sky);
            Place(city, new Vector2(0.5f, 0f), new Vector2(CurtainSky.W * 2f, 192f), Vector2.zero);
            var cityImg = city.gameObject.AddComponent<Image>();
            cityImg.sprite = Resources.Load<Sprite>("Scene/curtain_city");
            cityImg.raycastTarget = false;
        }

        /// <summary>
        /// THE BOARD: one housing - a case, one run of faces, one hinge across them and one lip under them - with a
        /// hairline seam where one flap meets the next. Sixteen flaps are built; an opening switches on as many as the
        /// longest name it will show needs, plus one blank either side.
        /// </summary>
        private void BuildFlapBoard()
        {
            _signBoard = NewRect("SignBoard", _sign);
            Place(_signBoard, new Vector2(0.5f, 1f), new Vector2(FlapW * 9f, FlapH), new Vector2(0f, -SignBoardTop));
            _boardCase = BoardPart("Case", UITheme.Night[1]);
            _boardFaces = BoardPart("Faces", UITheme.Night[2]);
            // The seams are the case showing between two flaps (a hairline, Night[1]); the hinge runs over them unbroken.
            for (int i = 0; i < _boardSeams.Length; i++)
            {
                var seam = BoardPart("Seam" + i, UITheme.Night[1]);
                SetTopLeft(seam, new Vector2(FlapW * (i + 1), 0f), new Vector2(1f, FlapFaces));
                _boardSeams[i] = seam.GetComponent<Image>();
            }
            _boardHinge = BoardPart("Hinge", UITheme.Night[0]);
            _boardLip = BoardPart("Lip", UITheme.Night[3]);
            for (int i = 0; i < _flaps.Length; i++)
            {
                _flaps[i] = new SignFlap(this, _signBoard, "Flap" + i, FlapW, FlapFaces, FlapHinge, FlapInkTop,
                    _display, 24, UITheme.Cream[4], UITheme.Cream[2], 8, 2);
                SetTopLeft(_flaps[i].Rt, new Vector2(FlapW * i, 0f), new Vector2(FlapW, FlapFaces));
            }
        }

        private RectTransform BoardPart(string name, Color colour)
        {
            var rt = NewRect(name, _signBoard);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>Anchors a rect by its top-left corner to its parent's top-left, at whole units.</summary>
        private static void SetTopLeft(RectTransform rt, Vector2 at, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(at.x, -at.y);
        }

        /// <summary>
        /// THE WEEK, AS SEVEN FITTINGS. The word says the state, the fitting under it says what the night is: a tube
        /// under each open night that burns only under the night being played, Saturday's star beside its word (the
        /// author: "cumartesi günleri vip hikaye müşterisi geleceği belirtilsin"; its own gold, dimmed by alpha only),
        /// and Sunday's roller shutter where a tube would be ("pazar gününün tatil olduğu anlaşılsın"). All at 1×.
        /// </summary>
        private void BuildSignNights()
        {
            var row = NewRect("SignNights", _sign);
            Stretch(row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var names = BarCalendar.WeekColumnLines;
            for (int i = 0; i < names.Length && i < _nightWords.Length; i++)
            {
                float cx = (i - 3) * NightPitch;
                var word = NewText("NightWord" + i, row, _body, 16, TextAnchor.MiddleCenter, UITheme.Cream[3]);
                // The ink's top sits at NightInkTop: Silkscreen's caps are 10 tall and ride 1.24 under the middle of
                // its 20.48 line, so a 16-tall rect centred 4 below the ink top puts the caps on it.
                Place(word.rectTransform, new Vector2(0.5f, 1f), new Vector2(NightPitch, 16f), new Vector2(cx, -(NightInkTop - 4f)));
                word.horizontalOverflow = HorizontalWrapMode.Overflow;
                word.text = UIText.T(names[i]);
                _nightWords[i] = word;

                if (i >= BarCalendar.OpenNights)
                {
                    var shut = NewRect("NightShutter", row);
                    Place(shut, new Vector2(0.5f, 1f), new Vector2(48f, 14f), new Vector2(cx, -(NightTubeTop - 2f)));
                    var shutImg = shut.gameObject.AddComponent<Image>();
                    shutImg.sprite = ChromeArt.RollerShutter(24, 1, 4, 1, 4, 1);
                    shutImg.raycastTarget = false;
                    continue;
                }
                _nightTubes[i] = new SignNeon(row, "NightTube" + i, layer => ChromeArt.NeonBar(24, layer),
                    new Vector2(48f + ChromeArt.NeonPad * 4f, 2f + ChromeArt.NeonPad * 4f),
                    new Vector2(0.5f, 1f), new Vector2(cx, -(NightTubeTop - 4f)));

                if ((BarNight)i == BarCalendar.VipNight)
                {
                    // Six right of the word's ink: the face's advances are even at 16 and carry one trailing texel.
                    float ink = Mathf.Max(0f, Mathf.Round(word.preferredWidth) - 2f);
                    var star = NewRect("NightStar", row);
                    Place(star, new Vector2(0.5f, 1f), new Vector2(16f, 16f),
                        new Vector2(Mathf.Round(cx + ink * 0.5f + 6f + 8f), -(NightInkTop - 3f)));
                    _nightStar = star.gameObject.AddComponent<Image>();
                    _nightStar.sprite = ItemArt.Star(true, 16f);
                    _nightStar.raycastTarget = false;
                }
            }
        }

        /// <summary>
        /// THE SIGN'S ONE FOOT: the week's caption and its number on a two-flap housing, sitting in the bottom tube's
        /// break on the tube's own centre line. Its width, measured in the language being spoken, comes back so the
        /// tube can be bent round it.
        /// </summary>
        private RectTransform BuildWeekPlate(out float width)
        {
            var plate = NewRect("SignWeekNo", _sign);
            var cap = NewText("WeekWord", plate, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            cap.text = UIText.T("hud.week_well.caption");
            float capW = Mathf.Ceil(cap.preferredWidth);
            const float CaseW = 44f, CaseH = 28f, Between = 8f;
            width = capW + Between + CaseW;
            if (((int)width & 1) == 1) width += 1f;   // an even plate centres on a whole unit
            Place(plate, new Vector2(0.5f, 1f), new Vector2(width, CaseH), new Vector2(0f, -(SignPlateY - CaseH * 0.5f)));
            SetTopLeft(cap.rectTransform, new Vector2(0f, 4f), new Vector2(capW + 2f, 20f));

            float hx = width - CaseW;
            PlatePart(plate, "WeekCase", new Vector2(hx, 0f), new Vector2(CaseW, CaseH), UITheme.Night[1]);
            PlatePart(plate, "WeekFaces", new Vector2(hx + 2f, 2f), new Vector2(40f, 24f), UITheme.Night[2]);
            PlatePart(plate, "WeekHinge", new Vector2(hx + 2f, 13f), new Vector2(40f, 2f), UITheme.Night[0]);
            PlatePart(plate, "WeekSeam", new Vector2(hx + 22f, 2f), new Vector2(1f, 24f), UITheme.Night[1]);
            for (int k = 0; k < 2; k++)
            {
                _weekFlaps[k] = new SignFlap(this, plate, "WeekFlap" + k, 20, 24, 11, 5, _display, 16,
                    UITheme.Cyan[4], UITheme.Cyan[2], 4, 0);
                SetTopLeft(_weekFlaps[k].Rt, new Vector2(hx + 2f + 20f * k, 2f), new Vector2(20f, 24f));
            }
            return plate;
        }

        private static void PlatePart(RectTransform parent, string name, Vector2 at, Vector2 size, Color colour)
        {
            var rt = NewRect(name, parent);
            SetTopLeft(rt, at, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
        }

        // ── opening ──────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>True while the room is still coming up: the clock must not run.</summary>
        private bool DoorsClosed => _curtainT < _curtainTotal;

        private void OpenTheDoors(int leaving, int arriving)
        {
            if (_curtain == null) return;
            _curtainFrom = leaving;
            _curtainTo = arriving;
            _curtain.gameObject.SetActive(true);
            _curtain.SetAsLastSibling();
            _curtainT = 0f;
            var black = UITheme.Night[0];
            _curtainImg.color = new Color(black.r, black.g, black.b, 1f);   // whole from the first frame: the rent flies under it
            Sfx.Play("curtain", 0.8f);

            // Read ONCE, on the way in: the sign is a scene, and asking the arc which night it is due on every frame of
            // it would be a question whose answer cannot change while the room is dark.
            _weekFrom = BarCalendar.WeekOf(leaving);
            _weekTo = BarCalendar.WeekOf(arriving);
            _curtainStoryNight = Run != null ? StoryNightOf(Run, _weekTo) : -1;
            var from = BarCalendar.NightOf(leaving);
            var to = BarCalendar.NightOf(arriving);
            _curtainMagenta = _curtainStoryNight >= 0 && _curtainStoryNight == (int)to;

            // SATURDAY TO MONDAY PLAYS THE SUNDAY (the mock's own beat): the clock winds forty hours, the board turns
            // to the day off and the shutter comes down over the week; at Sunday's midnight on the clock the week turns
            // behind it, the shutter goes up on the new one, and the board turns to Monday.
            _curtainSunday = from == BarNight.Saturday && to == BarNight.Monday && _weekTo > _weekFrom;
            _curtainReduced = Motion.Reduced;
            _curtainDay = _curtainSunday ? CurtainDaySunday : CurtainDay;
            _curtainHourTo = _curtainSunday ? 42f : 18f;
            _curtainLand = CurtainFadeIn + _curtainDay;
            _curtainLiftAt = _curtainLand + (_curtainSunday ? CurtainHoldSunday : CurtainHold);
            _curtainTotal = _curtainReduced ? ReducedHold + SignOff : _curtainLiftAt + CurtainLift;
            _curtainMidnight = _curtainSunday ? TimeOfHour(24f) : 0f;

            // The names, cut into what a flap can carry - a text element, so an accent stays on its letter - and
            // padded to one blank either side of the longest.
            var a = Elements(UIText.T(BarCalendar.NameLine(from)));
            var b = Elements(UIText.T(BarCalendar.NameLine(to)));
            var c = _curtainSunday ? Elements(UIText.T(BarCalendar.DayOffLine)) : null;
            int longest = Mathf.Max(a.Length, Mathf.Max(b.Length, c != null ? c.Length : 0));
            _flapCount = Mathf.Clamp(longest + 2, 3, MaxFlaps);
            _flapA = Padded(a, _flapCount);
            _flapB = Padded(c ?? b, _flapCount);        // on a Sunday the first turn is to the day off...
            _flapC = c != null ? Padded(b, _flapCount) : null;   // ...and the second to Monday
            _seqAB = FlapRuns(_flapA, _flapB, 0);
            _seqBC = _flapC != null ? FlapRuns(_flapB, _flapC, 2) : null;
            _curtainMonStrike = _curtainMidnight + FlapStagger * _flapCount + 0.45f;
            LayTheBoard(_flapCount);

            _signSky.Reset();
            StepCurtain(0f);   // place everything before the first frame is drawn
        }

        /// <summary>When the eased clock reads this hour (hours past 02:00 of the night that closed).</summary>
        private float TimeOfHour(float hour)
        {
            float e = Mathf.Clamp01((hour - 2f) / (_curtainHourTo - 2f));
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (mid * mid * (3f - 2f * mid) < e) lo = mid; else hi = mid;
            }
            return CurtainFadeIn + _curtainDay * lo;
        }

        private static string[] Elements(string s)
        {
            var list = new List<string>();
            var e = StringInfo.GetTextElementEnumerator(s ?? "");
            while (e.MoveNext()) list.Add(e.GetTextElement());
            return list.ToArray();
        }

        private static string[] Padded(string[] word, int n)
        {
            var row = new string[n];
            int left = (n - word.Length) / 2;
            for (int i = 0; i < n; i++)
            {
                int k = i - left;
                row[i] = k >= 0 && k < word.Length ? word[k] : " ";
            }
            return row;
        }

        /// <summary>
        /// What each flap falls through on its way from one name to the next: two to four letters taken from the two
        /// names themselves (so any script works), in a fixed order - nothing on this screen rolls a die.
        /// </summary>
        private static string[][] FlapRuns(string[] from, string[] to, int salt)
        {
            var pool = new List<string>();
            foreach (var s in from) if (s != " " && !pool.Contains(s)) pool.Add(s);
            foreach (var s in to) if (s != " " && !pool.Contains(s)) pool.Add(s);
            pool.Sort(string.CompareOrdinal);
            var runs = new string[from.Length][];
            for (int i = 0; i < from.Length; i++)
            {
                if (from[i] == to[i]) continue;
                int k = pool.Count == 0 ? 0 : 2 + (i * 7 + salt) % 3;
                var run = new string[k + 2];
                run[0] = from[i];
                for (int j = 0; j < k; j++) run[j + 1] = pool[(i * 5 + j * 3 + salt) % pool.Count];
                run[k + 1] = to[i];
                runs[i] = run;
            }
            return runs;
        }

        /// <summary>Sizes the housing to this opening's flaps, centred on the sign.</summary>
        private void LayTheBoard(int n)
        {
            float w = n * FlapW;
            _signBoard.sizeDelta = new Vector2(w, FlapH);
            SetTopLeft(_boardCase, new Vector2(-2f, -2f), new Vector2(w + 4f, FlapH + 2f));
            SetTopLeft(_boardFaces, Vector2.zero, new Vector2(w, FlapFaces));
            SetTopLeft(_boardHinge, new Vector2(0f, FlapHinge), new Vector2(w, 2f));
            SetTopLeft(_boardLip, new Vector2(-2f, FlapFaces), new Vector2(w + 4f, FlapH - FlapFaces));
            for (int i = 0; i < _boardSeams.Length; i++) _boardSeams[i].enabled = i < n - 1;
            for (int i = 0; i < _flaps.Length; i++) _flaps[i].Rt.gameObject.SetActive(i < n);
        }

        // ── running ──────────────────────────────────────────────────────────────────────────────────────────────────

        private void StepCurtain() => StepCurtain(Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace);   // 1 in the game

        private void StepCurtain(float dt)
        {
            if (_curtain == null || _curtainT >= _curtainTotal) return;
            _curtainT += dt;
            DrawSignAt(Mathf.Min(_curtainT, _curtainTotal));
            if (_curtainT >= _curtainTotal)
            {
                _curtainT = _curtainTotal;
                Sfx.Play("day_open", 0.75f);   // the black is gone; the bar trades
                // All the way to clear before it goes. The step that crosses the finish returns early on the NEXT frame,
                // so whatever alpha the last computed frame happened to land on - six percent, measured - was the last
                // thing drawn.
                var black = UITheme.Night[0];
                _curtainImg.color = new Color(black.r, black.g, black.b, 0f);
                _curtain.gameObject.SetActive(false);
            }
        }

        private static float Stepped(float k) => Mathf.Floor(Mathf.Clamp01(k) * 4f + 1e-4f) / 4f;

        private static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        /// <summary>
        /// The whole sign at one moment of the opening. A pure function of <paramref name="t"/>: every part reads the
        /// same clock, so a sun that crossed on its own timer and a board that turned on another can never drift into
        /// two animations playing at once, which is exactly what a time-skip must not be.
        /// </summary>
        private void DrawSignAt(float t)
        {
            bool calm = Motion.NoFlashes;   // asked every frame, so a switch thrown mid-curtain lands at once

            // THE BLACK AND THE SIGN'S OWN LIGHT. The sign goes out FIRST, in four steps with the black still whole, and
            // only then does the room come up: the sign is never drawn over the room.
            float signA, black;
            if (_curtainReduced)
            {
                float k = t <= ReducedHold ? 0f : Stepped((t - ReducedHold) / SignOff);
                signA = 1f - k;
                black = 1f - k;
            }
            else
            {
                signA = Stepped(t / CurtainFadeIn);
                black = 1f;
                if (t > _curtainLiftAt)
                {
                    signA *= 1f - Stepped((t - _curtainLiftAt) / SignOff);
                    float k = Mathf.Clamp01((t - _curtainLiftAt - SignOff) / (CurtainLift - SignOff));
                    black = (1f - k) * (1f - k);
                }
            }
            var night0 = UITheme.Night[0];
            _curtainImg.color = new Color(night0.r, night0.g, night0.b, black);
            _signGroup.alpha = signA;

            // Under reduced motion the sign shows its finished frame from the first: the state is read at the landing.
            float s = _curtainReduced ? Mathf.Max(t, _curtainLand) : t;

            // THE DAY ITSELF: one eased run from two in the morning to six in the evening (forty hours on a Sunday),
            // read in the five-minute steps the beam's clock reads in, and the sky, the bodies and the readout all
            // drawn from that one reading.
            float e = Smooth((s - CurtainFadeIn) / _curtainDay);
            float hour = 2f + (_curtainHourTo - 2f) * e;
            int five = Mathf.RoundToInt(hour * 12f);
            float read = five / 12f;
            bool landed = s >= _curtainLand;
            _curtainClock.Show((five / 12) % 24, (five % 12) * 5, calm || landed || ((int)(s * 2f) & 1) == 0);
            _signSky.Draw(read, five, s, !calm);
            PlaceSignBodies(read);

            // THE SIGN'S TUBE burns half through the day and strikes when the hour lands: dark, half, lit.
            int frame = !landed ? SignNeon.Half
                : calm ? SignNeon.Lit
                : s < _curtainLand + 0.05f ? SignNeon.Dark
                : s < _curtainLand + 0.09f ? SignNeon.Half
                : SignNeon.Lit;
            _signTube.Show(frame, _curtainMagenta);

            int tonightIx = (int)BarCalendar.NightOf(_curtainTo), leavingIx = (int)BarCalendar.NightOf(_curtainFrom);
            float flapsFrom = CurtainFadeIn + FlapLead;
            if (!_curtainSunday)
            {
                ShowFlaps(_flapA, _flapB, _seqAB, s, flapsFrom);
                ShowWeekNumber(_weekTo, _weekTo, false);
                PlaceShutter(0f);
                float burnOut = 1f - Mathf.Clamp01((s - LeavingOut) / TubeOut);
                LightSignNights(tonightIx, StrikeBurn(s, ArrivingStrike, calm), leavingIx, burnOut,
                    _curtainStoryNight, StrikeState(s, ArrivingStrike, calm));
                return;
            }

            // THE SUNDAY. Saturday closes on the old week - every night of it spent - and goes out; the shutter comes
            // down; at midnight on the clock the week turns behind it (a tenth before, so the new number is up when the
            // shutter rises), the shutter goes up on the new week and the board turns to Monday, which strikes after the
            // last flap lands.
            float midnight = _curtainMidnight;
            if (s < midnight) ShowFlaps(_flapA, _flapB, _seqAB, s, flapsFrom);
            else ShowFlaps(_flapB, _flapC, _seqBC, s, midnight);
            float drop = s < ShutterDown ? 0f
                : s < ShutterDown + ShutterRoll ? Mathf.Floor((s - ShutterDown) / ShutterRoll * SignShutterDrop / 4f) * 4f
                : s < midnight ? SignShutterDrop
                : s < midnight + ShutterRoll ? SignShutterDrop - Mathf.Floor((s - midnight) / ShutterRoll * SignShutterDrop / 4f) * 4f
                : 0f;
            PlaceShutter(drop);
            bool newWeek = s >= midnight - 0.10f;
            ShowWeekNumber(newWeek ? _weekTo : _weekFrom, _weekFrom, newWeek && s < midnight - 0.06f);
            if (!newWeek)
                LightSignNights(BarCalendar.OpenNights, 0f, leavingIx,
                    1f - Mathf.Clamp01((s - SaturdayOut) / TubeOut), -1, -1);
            else
                LightSignNights(tonightIx, StrikeBurn(s, _curtainMonStrike, calm), -1, 0f,
                    _curtainStoryNight, StrikeState(s, _curtainMonStrike, calm));
        }

        /// <summary>Tonight's tube: out until it strikes, then lit - a tenth later than the strike when it stutters.</summary>
        private static float StrikeBurn(float s, float strike, bool calm) => s >= strike + (calm ? 0f : 0.10f) ? 1f : 0f;

        /// <summary>The strike's stutter: half for .05, dark for .05, then the burn takes over. None under NO FLASHES.</summary>
        private static int StrikeState(float s, float strike, bool calm) =>
            calm || s < strike || s >= strike + 0.10f ? -1
            : s < strike + 0.05f ? SignNeon.Half : SignNeon.Dark;

        private void ShowFlaps(string[] from, string[] to, string[][] runs, float s, float t0)
        {
            for (int i = 0; i < _flapCount; i++)
            {
                var run = runs[i];
                if (run == null) { _flaps[i].Show(to[i], to[i], false); continue; }
                float start = t0 + FlapStagger * i;
                if (s < start) { _flaps[i].Show(from[i], from[i], false); continue; }
                float step = (s - start) / FlapStep;
                int j = Mathf.FloorToInt(step);
                if (j >= run.Length - 1) { _flaps[i].Show(to[i], to[i], false); continue; }
                // Half a step falling (the new top over the old bottom, the leaf across it), half a step settled.
                if (step - j < 0.5f) _flaps[i].Show(run[j + 1], run[j], true);
                else _flaps[i].Show(run[j + 1], run[j + 1], false);
            }
        }

        /// <summary>The week's two flaps pick their digit from this table rather than formatting the number: this runs
        /// every frame the sign is lit, and a formatted "07" cut into two letters was six new strings a frame
        /// (2026-09-28, the review's catch against the promise above BuildCurtainSign).</summary>
        private static readonly string[] FlapDigits = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        private void ShowWeekNumber(int week, int was, bool falling)
        {
            for (int k = 0; k < 2; k++)
            {
                // k = 0 is the tens, k = 1 the units - of the week's last two digits, as the board has two flaps.
                int place = k == 0 ? 10 : 1;
                string top = FlapDigits[week % 100 / place % 10];
                string bottom = falling ? FlapDigits[was % 100 / place % 10] : top;
                _weekFlaps[k].Show(top, bottom, falling && !ReferenceEquals(top, bottom));
            }
        }

        /// <summary>The blind's bar stands <paramref name="drop"/> below the box; rolled all the way up, the blind, its
        /// pull and its box are gone.</summary>
        private void PlaceShutter(float drop)
        {
            _shutterBlind.anchoredPosition = new Vector2(0f, SignShutterDrop - drop);
            bool down = drop > 0f;
            if (_shutterBox.enabled != down) _shutterBox.enabled = down;
            if (_shutterBlindImg.enabled != down) _shutterBlindImg.enabled = down;
        }

        /// <summary>
        /// Lights the week. Spent nights are Cream[2] (6:1 on the black - the old Night[4] was 1.8:1, the author's "ufak
        /// ve sönük"), nights ahead Cream[3], the closed day Cream[1] under its shutter, the story's night Magenta[4], and
        /// the night being played Amber[4] with its tube lit (magenta on a story night: the tube burns the word's hue).
        /// </summary>
        private void LightSignNights(int tonight, float burnTonight, int leaving, float burnLeaving, int story, int strike)
        {
            for (int i = 0; i < _nightWords.Length; i++)
            {
                if (_nightWords[i] == null) continue;
                bool closed = i >= BarCalendar.OpenNights;
                bool isStory = !closed && i == story;
                bool worked = !closed && i < tonight;
                float burn = closed ? 0f : i == tonight ? burnTonight : i == leaving ? burnLeaving : 0f;
                _nightWords[i].color = closed ? UITheme.Cream[1]
                    : burn >= 0.5f ? (isStory ? UITheme.Magenta[4] : UITheme.Amber[4])
                    : isStory ? UITheme.Magenta[4]
                    : worked || i == leaving ? UITheme.Cream[2]
                    : UITheme.Cream[3];
                if (closed || _nightTubes[i] == null) continue;
                int state = i == tonight && strike >= 0 ? strike
                    : burn >= 0.66f ? SignNeon.Lit : burn >= 0.33f ? SignNeon.Half : SignNeon.Dark;
                _nightTubes[i].Show(state, isStory);
                if ((BarNight)i == BarCalendar.VipNight && _nightStar != null)
                    _nightStar.color = new Color(1f, 1f, 1f, i == tonight && burn >= 0.5f ? 1f : 0.62f);
            }
        }

        /// <summary>The sun and the moon on their arcs, from the same reading the sky was drawn at, on whole texels.</summary>
        private void PlaceSignBodies(float hour)
        {
            float h = Mathf.Repeat(hour, 24f);
            float sunK = (h - CurtainSky.SunUp) / (CurtainSky.SunDown - CurtainSky.SunUp);
            bool sunUp = sunK >= -0.04f && sunK <= 1.04f;
            if (_signSunImg.enabled != sunUp) _signSunImg.enabled = sunUp;
            if (sunUp)
            {
                var at = CurtainSky.ArcAt(sunK);
                _signSun.anchoredPosition = new Vector2(at.x * 2f, -at.y * 2f);
            }
            float moonK = CurtainSky.MoonK(h);
            float moonA = Mathf.Clamp01((1.02f - moonK) / 0.12f) * CurtainSky.NightOf(h);
            bool moonUp = moonA > 0.01f;
            if (_signMoonImg.enabled != moonUp) _signMoonImg.enabled = moonUp;
            if (moonUp)
            {
                var at = CurtainSky.ArcAt(moonK);
                _signMoon.anchoredPosition = new Vector2(at.x * 2f, -at.y * 2f);
                _signMoonImg.color = new Color(1f, 1f, 1f, moonA);   // in, not on: a moon that pops is a bug in the sky
            }
        }

        // ── the sign's pieces ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// One tube on the sign, as its four rings (ChromeArt.NeonPath / NeonBar), run between three states by tint:
        /// DARK glass (Night[3] rim, Night[2] core, no light), HALF (the hue's [2] rim round its [3] core, no light) and
        /// LIT (the hue's [3] rim round a Cream[4] core, its light in two flat bands of [2] at .30 and .12). Amber, or
        /// magenta on the story's night.
        /// </summary>
        private sealed class SignNeon
        {
            public const int Dark = 0, Half = 1, Lit = 2;
            private readonly Image _glow2, _glow1, _rim, _core;
            private int _state = -1;
            private bool _magenta;

            public SignNeon(RectTransform parent, string name, Func<ChromeArt.NeonLayer, Sprite> art,
                Vector2 size, Vector2 anchor, Vector2 pos)
            {
                var holder = NewRect(name, parent);
                Place(holder, anchor, size, pos);
                _glow2 = Layer(holder, "Glow2", art(ChromeArt.NeonLayer.Glow2));
                _glow1 = Layer(holder, "Glow1", art(ChromeArt.NeonLayer.Glow1));
                _rim = Layer(holder, "Rim", art(ChromeArt.NeonLayer.Rim));
                _core = Layer(holder, "Core", art(ChromeArt.NeonLayer.Core));
                Show(Dark, false);
            }

            private static Image Layer(RectTransform holder, string name, Sprite sprite)
            {
                var rt = NewRect(name, holder);
                Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                return img;
            }

            public void Show(int state, bool magenta)
            {
                if (state == _state && magenta == _magenta) return;
                _state = state;
                _magenta = magenta;
                var ramp = magenta ? UITheme.Magenta : UITheme.Amber;
                bool lit = state == Lit;
                _glow1.enabled = lit;
                _glow2.enabled = lit;
                if (lit)
                {
                    _glow1.color = new Color(ramp[2].r, ramp[2].g, ramp[2].b, 0.30f);
                    _glow2.color = new Color(ramp[2].r, ramp[2].g, ramp[2].b, 0.12f);
                    _rim.color = ramp[3];
                    _core.color = UITheme.Cream[4];
                }
                else if (state == Half)
                {
                    _rim.color = ramp[2];
                    _core.color = ramp[3];
                }
                else
                {
                    _rim.color = UITheme.Night[3];
                    _core.color = UITheme.Night[2];
                }
            }
        }

        /// <summary>
        /// One flap of a split-flap housing: the letter in three masked bands - above the hinge, across it, below it -
        /// and the leaf that falls across the lower half while it turns. THE HINGE DARKENS THE LETTER, IT DOES NOT CUT
        /// IT: a W cut by a two-unit gap read as a won sign on the mock, so the band across the hinge carries the same
        /// glyph one step down its own ramp. The flap has no face of its own; the housing behind it is one surface.
        /// </summary>
        private sealed class SignFlap
        {
            public readonly RectTransform Rt;
            private readonly Text _top, _seam, _bottom;
            private readonly GameObject _leaf;
            private string _shownTop, _shownBottom;
            private bool _shownFalling = true;

            public SignFlap(TycoonHud hud, RectTransform parent, string name, int w, int faces, int hinge, int inkTop,
                Font font, int size, Color ink, Color seamInk, int leafRows, int leafLit)
            {
                Rt = NewRect(name, parent);
                _top = Band(hud, "Upper", 0, hinge, w, inkTop, font, size, ink);
                _seam = Band(hud, "Across", hinge, hinge + 2, w, inkTop, font, size, seamInk);
                _bottom = Band(hud, "Lower", hinge + 2, faces, w, inkTop, font, size, ink);
                var leaf = NewRect("Leaf", Rt);
                SetTopLeft(leaf, new Vector2(0f, hinge + 2), new Vector2(w, leafRows));
                var back = leaf.gameObject.AddComponent<Image>();
                back.color = UITheme.Night[3];
                back.raycastTarget = false;
                if (leafLit > 0)
                {
                    var edge = NewRect("LeafEdge", leaf);
                    SetTopLeft(edge, Vector2.zero, new Vector2(w, leafLit));
                    var edgeImg = edge.gameObject.AddComponent<Image>();
                    edgeImg.color = UITheme.Night[4];
                    edgeImg.raycastTarget = false;
                }
                _leaf = leaf.gameObject;
                Show(" ", " ", false);
            }

            private Text Band(TycoonHud hud, string name, int y0, int y1, int w, int inkTop, Font font, int size, Color ink)
            {
                var band = NewRect(name, Rt);
                SetTopLeft(band, new Vector2(0f, y0), new Vector2(w, y1 - y0));
                band.gameObject.AddComponent<RectMask2D>();
                // One line of the face, its top on the ink's top: the display face's ascent is its whole em and its caps
                // stand on the ascent line, so UpperCenter puts the letter's first row exactly there in every band.
                var text = hud.NewText("Ink", band, font, size, TextAnchor.UpperCenter, ink);
                SetTopLeft(text.rectTransform, new Vector2(0f, inkTop - y0), new Vector2(w, size + 8));
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                return text;
            }

            public void Show(string top, string bottom, bool falling)
            {
                if (top != _shownTop) { _top.text = top; _shownTop = top; }
                if (bottom != _shownBottom) { _seam.text = bottom; _bottom.text = bottom; _shownBottom = bottom; }
                if (falling != _shownFalling) { _leaf.SetActive(falling); _shownFalling = falling; }
            }
        }

        /// <summary>
        /// THE SKY WINDOW'S PICTURE: the room window's own model (SkyClock, sky_cycle.json) drawn at 320×112 - one flat
        /// colour per row, no dither (the author took the dither out of the window on 2026-09-22: "güneş batması daha
        /// smooth olmalı şu an pixel pixel gün batımı"), the sun's and the moon's light warmed into it, and the small
        /// hours' stars as single texels that twinkle in steps. Redrawn only when the five-minute reading moves, or while
        /// stars are out and their step turns.
        /// </summary>
        private sealed class CurtainSky
        {
            public const int W = 320, H = 112;
            /// <summary>The horizon row: the city's own bay band, 10 texels at its foot (20 units at 2×).</summary>
            public const int Horizon = H - 10;
            /// <summary>A Miami summer: first light before six, the sun down at eight - so 18:00 is late in its fall,
            /// which is why the room opens in gold. The moon is up from six in the evening to six in the morning.</summary>
            public const float SunUp = 6f, SunDown = 20f, MoonUp = 18f, MoonDown = 6f;
            private const int Arc = 71, StarCount = 34, StarRows = 58;

            public Texture Texture => _tex;
            private readonly SkyClock _clock;
            private readonly Texture2D _tex;
            private readonly Color32[] _px = new Color32[W * H];
            private readonly Color[] _stops;
            private readonly Color[] _row = new Color[H];
            private readonly float[] _rowLuma = new float[H];
            private int _lastFive = int.MinValue, _lastTwinkle = int.MinValue;

            public CurtainSky(SkyClock clock)
            {
                _clock = clock;
                _stops = new Color[clock != null ? clock.Spec.stopsV.Length : 1];
                _tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "CurtainSky",
                };
            }

            /// <summary>Forgets the last picture, so the next opening draws its first frame whatever it reads.</summary>
            public void Reset() { _lastFive = int.MinValue; _lastTwinkle = int.MinValue; }

            /// <summary>Lets the texture go with the HUD that drew it.</summary>
            public void Release() { if (_tex != null) UnityEngine.Object.Destroy(_tex); }

            /// <summary>East at 0, west at 1: a body's centre in texels of the window, highest at the middle.</summary>
            public static Vector2Int ArcAt(float k)
            {
                int x = Mathf.RoundToInt(23f + (W - 46f) * k);
                int y = Mathf.RoundToInt(Horizon - Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * Arc);
                return new Vector2Int(x, y);
            }

            /// <summary>Where the moon is on its arc: 0 rising at six in the evening, 1 setting at six in the morning.</summary>
            public static float MoonK(float h) => Mathf.Repeat(h - MoonUp, 24f) / (24f - MoonUp + MoonDown);

            /// <summary>1 in the small hours and after dark, 0 in daylight: the stars and the moon.</summary>
            public static float NightOf(float h) =>
                h < 12f ? 1f - SkyClock.SmoothStep(4.4f, 6.2f, h) : SkyClock.SmoothStep(20.4f, 22.8f, h);

            public void Draw(float hour, int five, float clock, bool twinkle)
            {
                float h = Mathf.Repeat(hour, 24f);
                float night = NightOf(h);
                int step = twinkle && night > 0.14f ? Mathf.FloorToInt(clock / 0.11f) : 0;
                if (five == _lastFive && step == _lastTwinkle) return;
                _lastFive = five;
                _lastTwinkle = step;

                // One flat colour per row, top to the horizon, off the model's five stops for this hour.
                if (_clock != null) _clock.DayBands(hour, _stops);
                else _stops[0] = UITheme.Night[1];
                var stopsV = _clock != null ? _clock.Spec.stopsV : null;
                for (int y = 0; y < H; y++)
                {
                    Color c = _stops[0];
                    if (stopsV != null && stopsV.Length > 1)
                    {
                        float v = Mathf.Clamp01(y / (float)Horizon);
                        int k = 0;
                        while (k < stopsV.Length - 2 && v > stopsV[k + 1]) k++;
                        c = Color.Lerp(_stops[k], _stops[k + 1], Mathf.InverseLerp(stopsV[k], stopsV[k + 1], v));
                    }
                    _row[y] = c;
                    _rowLuma[y] = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                    Color32 c32 = c;
                    int at = (H - 1 - y) * W;   // textures count up
                    for (int x = 0; x < W; x++) _px[at + x] = c32;
                }

                // The bodies' light, warmed into the sky - the window's own method, not a sprite glow.
                float moonK = MoonK(h);
                float moonA = Mathf.Clamp01((1.02f - moonK) / 0.12f) * night;
                if (moonA > 0.01f) Warm(ArcAt(moonK), 22, 0.40f * moonA, UITheme.ClubBlue[3]);
                float sunK = (h - SunUp) / (SunDown - SunUp);
                if (sunK >= -0.04f && sunK <= 1.04f)
                {
                    float high = Mathf.Sin(Mathf.Clamp01(sunK) * Mathf.PI);
                    Warm(ArcAt(sunK), 44, 0.55f + 0.25f * (1f - high), UITheme.Amber[4]);
                }

                // THE STARS, one texel each, out where the sky has gone dark enough, twinkling in three steps (never a
                // smooth alpha) - or holding still under NO FLASHES. WindowSky's own hash, so they are the window's kind.
                if (night > 0.14f)
                    for (int i = 0; i < StarCount; i++)
                    {
                        int sx = (int)(WindowSky.Hash01(i, 1, 11) * W), sy = (int)(WindowSky.Hash01(i, 2, 11) * StarRows);
                        if (night < 0.15f + 0.8f * WindowSky.Hash01(i, 3, 11) || _rowLuma[sy] > 0.22f) continue;
                        float tw = twinkle
                            ? 0.5f + 0.5f * Mathf.Sin(clock * 6.2832f / (2.2f + 2f * WindowSky.Hash01(i, 5, 11)) + WindowSky.Hash01(i, 4, 11) * 6.28f)
                            : 1f;
                        if (tw <= 0.22f) continue;
                        bool bright = WindowSky.Hash01(i, 6, 11) > 0.6f;
                        Color ink = tw > 0.55f ? (bright ? UITheme.Cream[4] : UITheme.Cream[3])
                            : (bright ? UITheme.Cream[3] : UITheme.Cream[2]);
                        _px[(H - 1 - sy) * W + sx] = ink;
                    }

                _tex.SetPixels32(_px);
                _tex.Apply(false, false);
            }

            private void Warm(Vector2Int at, int radius, float strength, Color toward)
            {
                for (int y = Mathf.Max(0, at.y - radius); y < Mathf.Min(H, at.y + radius + 1); y++)
                    for (int x = Mathf.Max(0, at.x - radius); x < Mathf.Min(W, at.x + radius + 1); x++)
                    {
                        float dx = x - at.x, dy = y - at.y;
                        float fall = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / radius);
                        if (fall <= 0f) continue;
                        fall = fall * fall * strength;
                        int i = (H - 1 - y) * W + x;
                        _px[i] = Color.Lerp(_px[i], toward, fall);
                    }
            }
        }
    }
}
