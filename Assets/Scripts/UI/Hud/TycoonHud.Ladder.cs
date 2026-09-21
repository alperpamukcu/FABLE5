using System;
using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE CERTIFICATE (PLAN_rank_ladder L2/L4/L5, 2026-09-21). The author asked for a level-up screen "sertifika
    /// gibi", then for more: "daha çok hareketli ve daha görsel ... kokteyl şeklinde mühür sağ altında, kurdele
    /// uzanabilir, yıldızlar hareket edebilir ... hepsinde bir kutlama efekti ... açılan özellikler kutu kutu
    /// görselleriyle beraber sertifikanın altında ... hangi sınıftan neler açıldıysa ... konfor ve servis puanları
    /// da sertifikada yazsın".
    ///
    /// One page on the author's blue plate: a sheet of cream paper ruled twice in gold — the heading, the title the
    /// bar is hereby known as, the stars climbing from the old standing to the new with a pop and a burst of sparks
    /// for every star that lands, the title it rose from, the comfort and the service the night was worth in hearts
    /// and medals, the night it was conferred, and a wax seal pressed askew at the foot's right with a cocktail
    /// glass embossed in it and two ribbon tails hanging out past the sheet's edge. Under the sheet, what the rung
    /// opened as TILES grouped by where they live — the counter, the door, the bench, the taps, the market's
    /// bottles, the book's pages — each with its own picture, popping in one after another. Confetti falls over
    /// it all while the climb plays. The plate is as tall as the tiles need.
    ///
    /// Two doors: at the night's end, the beat after the bill's climb lands on a new rung, the page opens by itself
    /// with the climb; from the top bar's star row it opens for the rank the bar is on, at rest. There is no
    /// certificate for the foot of the ladder (the author: "sadece 0.5-1-2-3-4-5 yıldızın sertifikası olmalı"):
    /// the star row says when the first one comes. Everything here reads BarRank and the run's own gates, so the
    /// page cannot name a threshold Core does not enforce.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private RectTransform _ladderPanel, _ladderPlate, _ladderPaper, _ladderBand, _ladderFx, _ladderNewFlag;
        private RectTransform _ladderSeal, _ladderRibbonL, _ladderRibbonR;
        private Text _ladderTitle, _ladderFrom, _ladderTo, _ladderNext, _ladderFoot, _ladderComfortValue, _ladderServiceValue;
        private Image[] _ladderToStars, _ladderHearts, _ladderMedals;
        private readonly List<RectTransform> _ladderTiles = new List<RectTransform>();
        private float _ladderT = -1f, _ladderFxT = -1f;
        private double _ladderFromStanding, _ladderToStanding;
        private readonly float[] _ladderStarPop = new float[BarRating.MaxStars];
        private readonly bool[] _ladderStarLanded = new bool[BarRating.MaxStars];
        private readonly List<LadderParticle> _ladderParticles = new List<LadderParticle>();
        private uint _ladderScatter = 0x9E3779B9u;
        private int _ladderWave;
        /// <summary>The highest rung this run has had its window opened for, and the run it belongs to.</summary>
        private int _ladderSeen = -1;
        private TycoonRun _ladderSeenRun;

        private const float LadderW = 920f, LadderClimb = 1.4f, LadderStar = 32f;
        /// <summary>The sheet and its writing: 856 wide under the plate's rings, 236 tall (measured: the seal's
        /// ribbons hang out of it on purpose).</summary>
        private const float PaperW = 856f, PaperH = 236f, PaperTop = -78f, PaperMargin = 32f;
        /// <summary>The tiles under the sheet: 80 square on an 88 pitch, a caption of two lines under each, a
        /// class caption over the first of each group, and the whole band at most two rows tall.</summary>
        private const float TileSize = 80f, TilePitch = 88f, TileGroupGap = 20f, TileRowH = 118f, BandHeadH = 22f;
        private const int TilesPerRow = 9, TileRowsMax = 2;
        private const float PlateHeadH = 70f, PlateFootH = 108f;

        private sealed class LadderParticle
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Vel;
            public float Life, Age, Spin, Sway, Size;
            public bool Fade;
        }

        // ── build ─────────────────────────────────────────────────────────────────────────────────────

        private void BuildLadderWindow(RectTransform root)
        {
            _ladderPanel = NewRect("Ladder", root);
            var canvas = _ladderPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 28;      // over the bill and the market (22..27), under the pause menu (29)
            _ladderPanel.gameObject.AddComponent<ForgivingRaycaster>();
            Stretch(_ladderPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var dim = NewRect("Dim", _ladderPanel);
            Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = MenuScrim;
            dimImg.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(CloseLadder);

            _ladderPlate = BluePlate(_ladderPanel, "Plate", new Vector2(LadderW, 600f));
            _ladderTitle = NewText("Title", _ladderPlate, _display, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Place(_ladderTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(LadderW - 80f, 24f), new Vector2(0f, -30f));
            _ladderTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            SunsetRules(_ladderPlate, -56f, LadderW - 80f);

            BuildSheet();

            _ladderBand = NewRect("Band", _ladderPlate);
            Place(_ladderBand, new Vector2(0.5f, 1f), new Vector2(PaperW, TileRowH), new Vector2(0f, PaperTop - PaperH - 14f));
            _ladderBand.pivot = new Vector2(0.5f, 1f);

            _ladderNext = NewText("Next", _ladderPlate, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[2]);
            Place(_ladderNext.rectTransform, new Vector2(0.5f, 0f), new Vector2(LadderW - 80f, 14f), new Vector2(0f, 82f));

            PackWordKey(_ladderPlate, "CONTINUE", UIText.T("rank.window.continue"), "next", MenuPack.Tone.Orange,
                new Vector2(0.5f, 0f), new Vector2(240f, PauseKeyH), new Vector2(0f, 22f), CloseLadder, 240f, 48f + 24f);

            // The celebration's layer: confetti and sparks, over the plate, never in the pointer's way.
            _ladderFx = NewRect("Fx", _ladderPanel);
            Stretch(_ladderFx, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _ladderPanel.gameObject.SetActive(false);
        }

        /// <summary>THE SHEET. Cream paper, a gold rule two deep and a finer one inside it, a diamond at each corner
        /// where the inner rule turns, and every line of the certificate's writing in its place.</summary>
        private void BuildSheet()
        {
            _ladderPaper = NewRect("Paper", _ladderPlate);
            Place(_ladderPaper, new Vector2(0.5f, 1f), new Vector2(PaperW, PaperH), new Vector2(0f, PaperTop));
            var paperImg = _ladderPaper.gameObject.AddComponent<Image>();
            paperImg.color = UITheme.Cream[4];
            paperImg.raycastTarget = false;
            var outer = NewRect("RuleOuter", _ladderPaper);
            Stretch(outer, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            Frame(outer, 2f, UITheme.Malt[3]);
            var inner = NewRect("RuleInner", _ladderPaper);
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(11f, 11f), new Vector2(-11f, -11f));
            Frame(inner, 1f, UITheme.Malt[2]);
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                var d = NewRect("Corner", inner);
                d.anchorMin = d.anchorMax = corner;
                d.pivot = new Vector2(0.5f, 0.5f);
                d.sizeDelta = new Vector2(6f, 6f);
                d.anchoredPosition = Vector2.zero;
                d.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var di = d.gameObject.AddComponent<Image>();
                di.color = UITheme.Malt[3]; di.raycastTarget = false;
            }

            var heading = PaperLine("Heading", -18f, UITheme.Malt[2]);
            heading.text = UIText.T("rank.cert.heading");
            var headRule = NewRect("HeadRule", _ladderPaper);
            Place(headRule, new Vector2(0.5f, 1f), new Vector2(160f, 1f), new Vector2(0f, -30f));
            var hri = headRule.gameObject.AddComponent<Image>();
            hri.color = UITheme.Malt[2]; hri.raycastTarget = false;
            var knownAs = PaperLine("KnownAs", -42f, UITheme.Cream[1]);
            knownAs.text = UIText.T("rank.cert.known_as");

            // The title the bar is hereby known as, in the display face's large size: "NOBODY'S HEARD OF IT" is
            // twenty characters and stays on one line across the sheet's writing width.
            _ladderTo = NewText("To", _ladderPaper, _display, 24, TextAnchor.UpperCenter, UITheme.Night[0]);
            Place(_ladderTo.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 96f, 32f), new Vector2(0f, -52f));
            _ladderTo.rectTransform.pivot = new Vector2(0.5f, 1f);
            _ladderTo.horizontalOverflow = HorizontalWrapMode.Overflow;

            // The stars that climb. Place hands a row its anchor as its pivot, so at the sheet's middle it stands
            // centred with nothing to slide.
            _ladderToStars = LiveStarRow(_ladderPaper, new Vector2(0.5f, 1f), new Vector2(0f, -92f), LadderStar, 4f,
                UITheme.Amber[3], UITheme.Night[3]);
            _ladderFrom = PaperLine("From", -134f, UITheme.Cream[1]);

            // COMFORT and SERVICE, in the house's own hearts and medals, each with its number — what the night the
            // rank was conferred was worth (the author: "konfor ve servis puanları da sertifikada yazsın").
            _ladderHearts = ScoreGroup("Comfort", -150f, "rank.cert.comfort", ItemArt.Heart, out _ladderComfortValue);
            _ladderMedals = ScoreGroup("Service", 150f, "rank.cert.service", ItemArt.Medal, out _ladderServiceValue);

            _ladderFoot = NewText("Foot", _ladderPaper, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[1]);
            Place(_ladderFoot.rectTransform, new Vector2(0f, 1f), new Vector2(PaperW - 200f, 14f), new Vector2(40f, -196f));
            _ladderFoot.rectTransform.pivot = new Vector2(0f, 0.5f);
            _ladderFoot.horizontalOverflow = HorizontalWrapMode.Overflow;

            // THE SEAL, at the foot's right: two ribbon tails first (they hang from behind it, out past the
            // sheet's bottom edge), then the wax pressed a little askew, a cocktail glass embossed in gold on it.
            _ladderRibbonL = Ribbon("RibbonL", PaperW - 82f, -20f, UITheme.ViceRed[2]);
            _ladderRibbonR = Ribbon("RibbonR", PaperW - 60f, 18f, UITheme.ViceRed[3]);
            _ladderSeal = NewRect("Seal", _ladderPaper);
            _ladderSeal.anchorMin = _ladderSeal.anchorMax = new Vector2(0f, 1f);
            _ladderSeal.pivot = new Vector2(0.5f, 0.5f);
            _ladderSeal.sizeDelta = new Vector2(64f, 64f);
            _ladderSeal.anchoredPosition = new Vector2(PaperW - 72f, -206f);
            _ladderSeal.localRotation = Quaternion.Euler(0f, 0f, -9f);
            var wax = _ladderSeal.gameObject.AddComponent<Image>();
            wax.sprite = SealWax(64); wax.color = Color.white; wax.raycastTarget = false;
            var glassRt = NewRect("Glass", _ladderSeal);
            Place(glassRt, new Vector2(0.5f, 0.5f), new Vector2(30f, 36f), new Vector2(0f, 1f));
            var glass = glassRt.gameObject.AddComponent<Image>();
            glass.sprite = ItemArt.Load("glass3d_martini_t2_Front");
            glass.color = UITheme.Malt[4]; glass.preserveAspect = true; glass.raycastTarget = false;
            glass.enabled = glass.sprite != null;
        }

        /// <summary>One line of the sheet's small writing, centred, at <paramref name="y"/> below its top.</summary>
        private Text PaperLine(string id, float y, Color ink)
        {
            var t = NewText(id, _ladderPaper, _body, 8, TextAnchor.MiddleCenter, ink);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 96f, 14f), new Vector2(0f, y));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// <summary>A score on the sheet: its label, a row of five of the house's icons filled to the value, the
        /// number. Centred on <paramref name="x"/> from the sheet's middle.</summary>
        private Image[] ScoreGroup(string id, float x, string labelKey, Func<bool, float, Sprite> art, out Text value)
        {
            const float px = 16f, gap = 2f, labelW = 72f, valueW = 40f, y = -156f;
            float rowW = BarRating.MaxStars * (px + gap) - gap;
            float total = labelW + 8f + rowW + 8f + valueW;
            float left = x - total * 0.5f;
            var label = NewText(id + "Label", _ladderPaper, _body, 8, TextAnchor.MiddleRight, UITheme.Malt[2]);
            Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(labelW, 14f), new Vector2(left + labelW * 0.5f, y));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = UIText.T(labelKey);
            var row = NewRect(id + "Row", _ladderPaper);
            Place(row, new Vector2(0.5f, 1f), new Vector2(rowW, px), new Vector2(left + labelW + 8f + rowW * 0.5f, y));
            var fills = new Image[BarRating.MaxStars];
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var cell = NewRect("C" + i, row);
                Place(cell, new Vector2(0f, 0.5f), new Vector2(px, px), new Vector2(i * (px + gap), 0f));
                cell.pivot = new Vector2(0f, 0.5f);
                var back = cell.gameObject.AddComponent<Image>();
                back.sprite = art(false, px); back.preserveAspect = true; back.raycastTarget = false;
                var over = NewRect("F", cell);
                Stretch(over, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var oi = over.gameObject.AddComponent<Image>();
                oi.sprite = art(true, px); oi.preserveAspect = true; oi.raycastTarget = false;
                oi.type = Image.Type.Filled; oi.fillMethod = Image.FillMethod.Horizontal; oi.fillAmount = 0f;
                fills[i] = oi;
            }
            value = NewText(id + "Value", _ladderPaper, _body, 8, TextAnchor.MiddleLeft, UITheme.Night[0]);
            Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(valueW, 14f), new Vector2(left + total - valueW * 0.5f, y));
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            return fills;
        }

        private static void SetRowFill(Image[] fills, double value)
        {
            if (fills == null) return;
            for (int i = 0; i < fills.Length; i++)
                if (fills[i] != null) fills[i].fillAmount = Mathf.Clamp01((float)(value - i));
        }

        /// <summary>A ribbon tail: hangs from the seal's centre, pivot at its top, tilted by <paramref name="tilt"/>.</summary>
        private RectTransform Ribbon(string id, float x, float tilt, Color ink)
        {
            var rt = NewRect(id, _ladderPaper);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(14f, 62f);
            rt.anchoredPosition = new Vector2(x, -206f);
            rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = ink; img.raycastTarget = false;
            // the notch at the tail's end: the sheet's own cream, a diamond on the tail's tip
            var notch = NewRect("Notch", rt);
            notch.anchorMin = notch.anchorMax = new Vector2(0.5f, 0f);
            notch.pivot = new Vector2(0.5f, 0.5f);
            notch.sizeDelta = new Vector2(9f, 9f);
            notch.anchoredPosition = Vector2.zero;
            notch.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var ni = notch.gameObject.AddComponent<Image>();
            ni.color = UITheme.Cyan[0]; ni.raycastTarget = false;
            return rt;
        }

        private static Sprite s_sealWax;

        /// <summary>THE WAX: a scalloped disc in the vice red with a thin gold ring inside its edge, drawn once.</summary>
        private static Sprite SealWax(int px)
        {
            if (s_sealWax != null) return s_sealWax;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "seal_wax" };
            var clear = new Color(0f, 0f, 0f, 0f);
            var wax = UITheme.ViceRed[2];
            var rim = UITheme.ViceRed[1];
            var gold = UITheme.Malt[4];
            float c = px * 0.5f;
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);
                    float edge = c - 1.5f - 1.6f * (0.5f + 0.5f * Mathf.Cos(a * 14f));   // fourteen scallops
                    Color p = clear;
                    if (r <= edge - 1.2f) p = wax;
                    else if (r <= edge) p = rim;
                    if (r > edge - 8f && r <= edge - 6.6f) p = gold;
                    tex.SetPixel(x, y, p);
                }
            tex.Apply(false, false);
            s_sealWax = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            s_sealWax.name = "seal_wax";
            return s_sealWax;
        }

        /// <summary>
        /// THE AUTHOR'S BLUE PLATE (2026-09-21: "UI ayarlarken bu arkaplanları kullan ... panellerde kullanılacaksa
        /// orta kısmın şeffaflığı olmamalı"): ui_blue 9-sliced at 2x, its rings kept at their drawn width whatever
        /// the size, and a solid of the plate's own ink laid under the middle so nothing behind shows through. A
        /// project without the drawing falls back to the night plate.
        /// </summary>
        private RectTransform BluePlate(RectTransform parent, string name, Vector2 size)
        {
            var skinSprite = ChromeArt.BluePlate();
            if (skinSprite == null) return NightPlate(parent, name, size, 0f);
            var plate = NewRect(name, parent);
            Place(plate, new Vector2(0.5f, 0.5f), size, Vector2.zero);
            var catcher = plate.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0.004f);
            catcher.raycastTarget = true;
            plate.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            var fill = NewRect("Fill", plate);
            Stretch(fill, Vector2.zero, Vector2.one, new Vector2(BluePlateInset, BluePlateInset),
                new Vector2(-BluePlateInset, -BluePlateInset));
            var fi = fill.gameObject.AddComponent<Image>();
            fi.color = UITheme.Cyan[0];
            fi.raycastTarget = false;

            var skin = NewRect("Skin", plate);
            Stretch(skin, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var si = skin.gameObject.AddComponent<Image>();
            si.sprite = skinSprite;
            si.type = Image.Type.Sliced;
            si.pixelsPerUnitMultiplier = 0.5f;   // the drawing at exactly 2x, like the pack's keys
            si.color = Color.white;
            si.raycastTarget = false;
            UiAuditExempt.Mark(skin, "the author's ui_blue plate, 9-sliced at 2x");
            return plate;
        }

        /// <summary>How far in from the plate's edge the solid fill starts: where the drawing's fill begins (two
        /// rings of two pixels, at 2x), under the rings' own corner pixels, so nothing pokes past the arc.</summary>
        private const float BluePlateInset = 8f;

        /// <summary>THE AUTHOR'S DOOR (2026-09-21, "oyun editöründen yeni eklenen ekranları göremiyorum"): parks
        /// the standing on the next rung and opens the certificate for the climb, the way the bill would. Dev bench.</summary>
        private void DevClimbLadder()
        {
            var run = Run;
            if (run == null || _ladderPanel == null) return;
            var next = BarRank.Above(run.Rank);
            if (next == null) { Toast(UIText.T("rank.window.top")); return; }
            double from = run.Rating.Average;
            run.Rating.DevSet(next.Stars);
            ShowLadder(run, from, next.Stars, climb: true);
        }

        // ── show ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Opens the certificate for a climb from <paramref name="from"/> to <paramref name="to"/> stars;
        /// with <paramref name="climb"/> off it stands at rest at the rank the bar is on (the star row's door).</summary>
        private void ShowLadder(TycoonRun run, double from, double to, bool climb)
        {
            if (_ladderPanel == null || run == null) return;
            var was = BarRank.Of(from);
            var now = BarRank.Of(to);
            bool moving = climb && !Motion.Reduced;
            _ladderTitle.text = UIText.T(climb ? "rank.window.title" : "rank.window.title_again");
            _ladderTo.text = UIText.T(now.Title);
            _ladderFrom.text = climb ? UIText.T("rank.cert.risen_from", ("title", UIText.T(was.Title))) : "";
            _ladderFoot.text = UIText.T(climb ? "rank.cert.foot" : "rank.cert.foot_again", ("night", run.Day.ToString()));
            _ladderFromStanding = from;
            _ladderToStanding = to;
            SetStars(_ladderToStars, moving ? from : to);
            for (int i = 0; i < BarRating.MaxStars; i++) { _ladderStarPop[i] = -1f; _ladderStarLanded[i] = !moving || from >= i + 1 - 1e-6; }
            double comfort = run.Phase == TycoonPhase.DayEnd ? run.ComfortTonight : run.ComfortNow;
            double service = run.ServiceTonight;
            SetRowFill(_ladderHearts, comfort);
            SetRowFill(_ladderMedals, service);
            _ladderComfortValue.text = comfort.ToString("0.0");
            _ladderServiceValue.text = service.ToString("0.0");

            int rows = LayTiles(run, was, now, climb, moving);
            var next = BarRank.Above(now);
            _ladderNext.text = next == null ? UIText.T("rank.window.top")
                : UIText.T("rank.window.next", ("stars", next.Stars.ToString("0.0")));

            // The plate is as tall as the sheet and the tiles need, and stands centred.
            float h = PlateHeadH + (-PaperTop - PlateHeadH) + PaperH + 14f + BandHeadH + rows * TileRowH + PlateFootH;
            _ladderPlate.sizeDelta = new Vector2(LadderW, h);
            _ladderPlate.anchoredPosition = Vector2.zero;
            _ladderPlate.localScale = moving ? new Vector3(0.94f, 0.94f, 1f) : Vector3.one;

            // The seal: pressed when the climb lands, or already there.
            float sealScale = moving ? 0f : 1f;
            _ladderSeal.localScale = new Vector3(sealScale, sealScale, 1f);
            _ladderRibbonL.localScale = new Vector3(1f, sealScale, 1f);
            _ladderRibbonR.localScale = new Vector3(1f, sealScale, 1f);

            ClearParticles();
            _ladderT = moving ? 0f : -1f;
            _ladderFxT = moving ? 0f : -1f;
            _ladderWave = 0;

            _ladderSeenRun = run;
            if (now.Index > _ladderSeen) _ladderSeen = now.Index;
            RefreshLadderFlag(run);
            CloseId();
            _ladderPanel.gameObject.SetActive(true);
            Sfx.Play(climb ? "stamp" : "menu_open", 0.7f);
        }

        /// <summary>
        /// WHAT THE RUNG OPENED, AS TILES, grouped by where it lives: the counter's dishes, the door, the bench's
        /// spoon, the taps' lines, the market's bottles and the book's pages that the rung's stars unlocked. A
        /// climb lists every rung from the one after WAS up to NOW (a night that climbs two rungs lists both); the
        /// standing window lists the rung it is on. Returns how many rows the band took (at least one).
        /// </summary>
        private int LayTiles(TycoonRun run, Rung was, Rung now, bool climb, bool moving)
        {
            foreach (var t in _ladderTiles) if (t != null) Destroy(t.gameObject);
            _ladderTiles.Clear();
            foreach (Transform old in _ladderBand) Destroy(old.gameObject);

            var head = NewText("Head", _ladderBand, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
            Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW, 14f), new Vector2(0f, -4f));
            head.text = UIText.T("rank.window.new");

            // Collect: (class caption key, tile caption, picture) in the order the room reads them.
            var tiles = new List<(string cls, string name, Sprite art)>();
            for (int i = climb ? was.Index + 1 : 1; i <= now.Index; i++)
            {
                if (!climb && i < now.Index) continue;
                var rung = BarRank.Rungs[i];
                foreach (var f in rung.Opens)
                    foreach (var tile in TilesFor(f)) tiles.Add(tile);
                foreach (var card in run.BottlesOpeningAt(rung.Stars))
                    tiles.Add(("rank.cert.class.market", UIText.Caps(UIText.Data("bottle", card.Id, "name", card.Name)),
                        ItemArt.Bottle(card) ?? ItemArt.StyleBottle(run.CatalogueBottles, card.Info?.Style)));
                foreach (var r in run.RecipesOpeningAt(rung.Stars))
                    tiles.Add(("rank.cert.class.book", UIText.Caps(UIText.Data("recipe", r.Id, "name", r.Name)),
                        _bootstrap != null ? DrinkIcon.For(r, _bootstrap.Glassware) : null));
            }
            if (tiles.Count == 0)
            {
                var none = NewText("None", _ladderBand, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[2]);
                Place(none.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW, 14f), new Vector2(0f, -BandHeadH - 30f));
                none.horizontalOverflow = HorizontalWrapMode.Overflow;
                none.text = UIText.T("rank.window.nothing_new");
                return 1;
            }
            // Each class together, in the order the room reads them (the counter, the door, the bench, the taps,
            // the market, the book), whatever order the rungs handed them over in.
            var byClass = new List<(string cls, string name, Sprite art)>(tiles.Count);
            foreach (var cls in TileClasses)
                foreach (var t in tiles) if (t.cls == cls) byClass.Add(t);
            foreach (var t in tiles) if (Array.IndexOf(TileClasses, t.cls) < 0) byClass.Add(t);
            tiles = byClass;

            // The slots first, dry: where each tile would stand under the wrap rule. Then the cut: the band is at
            // most two rows, and when the tiles need more, the last slot of the last row stands for the rest.
            float rowW = TilesPerRow * TilePitch - (TilePitch - TileSize);
            float left = (PaperW - rowW) * 0.5f;
            var slots = new List<(float x, float y, int row, bool head)>(tiles.Count);
            {
                float x = 0f, y = -BandHeadH;
                int row = 0;
                string lastClass = null;
                foreach (var (cls, _, _) in tiles)
                {
                    bool groupHead = cls != lastClass;
                    if (groupHead && x > 0f) x += TileGroupGap;
                    if (x + TileSize > rowW + 0.5f) { x = 0f; row++; y -= TileRowH; groupHead = true; }
                    slots.Add((left + x, y, row, groupHead));
                    lastClass = cls;
                    x += TilePitch;
                }
            }
            int cut = slots.FindIndex(s => s.row >= TileRowsMax);
            int shown = cut < 0 ? tiles.Count : Math.Max(0, cut - 1);
            int rest = tiles.Count - shown;
            int rows = 0;
            for (int i = 0; i < shown; i++)
            {
                var (cls, name, art) = tiles[i];
                var (x, y, row, isHead) = slots[i];
                Tile(x, y, name, art, i, moving);
                if (isHead)
                {
                    var cap = NewText("Class", _ladderBand, _body, 8, TextAnchor.MiddleLeft, UITheme.Cyan[4]);
                    Place(cap.rectTransform, new Vector2(0f, 1f), new Vector2(200f, 12f), new Vector2(x, y));
                    cap.rectTransform.pivot = new Vector2(0f, 1f);
                    cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                    cap.text = UIText.T(cls);
                }
                rows = Math.Max(rows, row + 1);
            }
            if (rest > 0 && shown < slots.Count)
            {
                var (x, y, row, _) = slots[shown];
                Tile(x, y, "+" + rest, null, shown, moving);
                rows = Math.Max(rows, row + 1);
            }
            return Math.Max(1, rows);
        }

        /// <summary>The classes in the order the band lays them, which is the order the room reads them.</summary>
        private static readonly string[] TileClasses =
        {
            "rank.cert.class.counter", "rank.cert.class.door", "rank.cert.class.bench",
            "rank.cert.class.draught", "rank.cert.class.market", "rank.cert.class.book",
        };

        /// <summary>One tile: a cream card with a gold rule, the picture centred in it, the name under it. Popped in
        /// on its turn when the page is moving.</summary>
        private RectTransform Tile(float x, float y, string name, Sprite art, int order, bool moving)
        {
            var rt = NewRect("Tile", _ladderBand);
            Place(rt, new Vector2(0f, 1f), new Vector2(TileSize, TileSize), new Vector2(x + TileSize * 0.5f, y - 14f - TileSize * 0.5f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = UITheme.Cream[4]; bg.raycastTarget = false;
            Frame(rt, 1f, UITheme.Malt[2]);
            var picRt = NewRect("Pic", rt);
            Place(picRt, new Vector2(0.5f, 0.5f), new Vector2(TileSize - 20f, TileSize - 20f), Vector2.zero);
            var pic = picRt.gameObject.AddComponent<Image>();
            pic.sprite = art; pic.preserveAspect = true; pic.raycastTarget = false;
            pic.enabled = art != null;
            if (art == null)
            {
                var plus = NewText("Plus", rt, _display, 16, TextAnchor.MiddleCenter, UITheme.Night[0]);
                Stretch(plus.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                plus.text = name;
            }
            // The name rides under the tile, on the tile, so it pops in with it.
            var cap = NewText("Name", rt, _body, 8, TextAnchor.UpperCenter, UITheme.Cream[3]);
            Place(cap.rectTransform, new Vector2(0.5f, 0f), new Vector2(TilePitch, 22f), new Vector2(0f, -3f));
            cap.rectTransform.pivot = new Vector2(0.5f, 1f);
            cap.horizontalOverflow = HorizontalWrapMode.Wrap;
            cap.text = art == null ? "" : name;
            if (moving) rt.localScale = Vector3.zero;
            _ladderTiles.Add(rt);
            return rt;
        }

        /// <summary>A feature's tiles: the pictures the room itself draws these things with.</summary>
        private static IEnumerable<(string cls, string name, Sprite art)> TilesFor(Feature f)
        {
            switch (f)
            {
                case Feature.IceAndLemon:
                    yield return ("rank.cert.class.counter", UIText.T("rank.tile.ice"), ItemArt.Load("counter_ice"));
                    yield return ("rank.cert.class.counter", UIText.T("rank.tile.lemon"), ItemArt.Load("counter_lemon"));
                    break;
                case Feature.Door:
                    yield return ("rank.cert.class.door", UIText.T("rank.tile.door"), ItemArt.Load("card_body"));
                    break;
                case Feature.Rims:
                    yield return ("rank.cert.class.counter", UIText.T("rank.tile.salt"), ItemArt.Load("counter_salt"));
                    yield return ("rank.cert.class.counter", UIText.T("rank.tile.sugar"), ItemArt.Load("counter_sugar"));
                    break;
                case Feature.Spoon:
                    yield return ("rank.cert.class.bench", UIText.T("rank.tile.spoon"), ItemArt.Load("bench_spoon"));
                    break;
                case Feature.Jars:
                    yield return ("rank.cert.class.market", UIText.T("rank.tile.olives"), ItemArt.Load("counter_olive"));
                    yield return ("rank.cert.class.market", UIText.T("rank.tile.mint"), ItemArt.Load("counter_mint"));
                    break;
                case Feature.SecondLine:
                    yield return ("rank.cert.class.draught", UIText.T("rank.tile.line2"), Resources.Load<Sprite>("Fixtures/fx_tap_beer"));
                    break;
                case Feature.ThirdLine:
                    yield return ("rank.cert.class.draught", UIText.T("rank.tile.line3"), Resources.Load<Sprite>("Fixtures/fx_tap_beer"));
                    break;
            }
        }

        private void CloseLadder()
        {
            if (_ladderPanel == null || !_ladderPanel.gameObject.activeSelf) return;
            _ladderT = -1f; _ladderFxT = -1f;
            ClearParticles();
            _ladderPanel.gameObject.SetActive(false);
            Sfx.Play("menu_close", 0.6f);
        }

        // ── the celebration, frame by frame ───────────────────────────────────────────────────────────

        /// <summary>
        /// THE CLIMB AND THE PARTY, on the unscaled clock at the ceremony's pace: the plate settles, the stars run
        /// from the old standing to the new and each one pops with a burst of sparks as it lands, the seal comes
        /// down with its ribbons, the tiles pop in one after another, and confetti falls through it all.
        /// </summary>
        private void StepLadder()
        {
            if (_ladderPanel == null || !_ladderPanel.gameObject.activeSelf) return;
            float dt = Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace;
            if (_ladderFxT >= 0f)
            {
                float t0 = _ladderFxT;
                _ladderFxT += dt;
                float t = _ladderFxT;

                // the plate settles in its first quarter second
                float settle = Mathf.Clamp01(t / 0.25f);
                float s = 0.94f + 0.06f * (1f - (1f - settle) * (1f - settle));
                _ladderPlate.localScale = new Vector3(s, s, 1f);

                // the stars climb from 0.4 s, and pop as they land
                float k = Mathf.Clamp01((t - 0.4f) / LadderClimb);
                float e = k * k * (3f - 2f * k);
                double at = Mathf.Lerp((float)_ladderFromStanding, (float)_ladderToStanding, e);
                SetStars(_ladderToStars, at);
                for (int i = 0; i < BarRating.MaxStars; i++)
                {
                    if (_ladderStarLanded[i]) continue;
                    bool full = at >= i + 1 - 1e-6;
                    bool lastPartial = k >= 1f && at > i + 1e-6;
                    if (full || lastPartial)
                    {
                        _ladderStarLanded[i] = true;
                        _ladderStarPop[i] = 0f;
                        Sparks(_ladderToStars[i].rectTransform);
                        Sfx.Play("key_press", 0.35f);
                    }
                }
                for (int i = 0; i < BarRating.MaxStars; i++)
                {
                    if (_ladderStarPop[i] < 0f) continue;
                    _ladderStarPop[i] += dt;
                    float p = Mathf.Clamp01(_ladderStarPop[i] / 0.28f);
                    float sc = 1f + 0.5f * Mathf.Sin(p * Mathf.PI);
                    var cell = _ladderToStars[i].rectTransform.parent as RectTransform;
                    if (cell != null) cell.localScale = new Vector3(sc, sc, 1f);
                    if (p >= 1f) _ladderStarPop[i] = -1f;
                }

                // the seal is pressed the moment the climb lands, and the ribbons unroll after it
                float sealT = t - (0.4f + LadderClimb + 0.1f);
                if (sealT >= 0f)
                {
                    if (t0 - (0.4f + LadderClimb + 0.1f) < 0f) Sfx.Play("stamp", 0.8f);
                    float q = Mathf.Clamp01(sealT / 0.22f);
                    float sc = Mathf.Lerp(2.2f, 1f, 1f - (1f - q) * (1f - q) * (1f - q));
                    _ladderSeal.localScale = new Vector3(sc, sc, 1f);
                    float rq = Mathf.Clamp01((sealT - 0.15f) / 0.3f);
                    float ry = 1f - (1f - rq) * (1f - rq);
                    _ladderRibbonL.localScale = new Vector3(1f, ry, 1f);
                    _ladderRibbonR.localScale = new Vector3(1f, ry, 1f);
                }

                // the tiles pop in, one every twelfth of a second, from the moment the seal is down
                float tileT = t - (0.4f + LadderClimb + 0.4f);
                for (int i = 0; i < _ladderTiles.Count; i++)
                {
                    if (_ladderTiles[i] == null) continue;
                    float q = Mathf.Clamp01((tileT - i * 0.08f) / 0.25f);
                    float sc = q < 1f ? 1.15f * Mathf.Sin(q * Mathf.PI * 0.5f) * (1f + 0.15f * Mathf.Sin(q * Mathf.PI)) / 1.15f : 1f;
                    if (q >= 1f) sc = 1f;
                    _ladderTiles[i].localScale = new Vector3(sc, sc, 1f);
                }

                // confetti: two waves, at the start and as the climb lands
                if (_ladderWave == 0 && t >= 0.15f) { Confetti(36); _ladderWave = 1; }
                if (_ladderWave == 1 && t >= 0.4f + LadderClimb) { Confetti(30); _ladderWave = 2; }
                if (t > 12f) _ladderFxT = -1f;   // long over; the particles below finish on their own
            }
            StepParticles(dt);
        }

        // ── particles: sparks off a landed star, confetti over the page ───────────────────────────────

        private uint Scatter()
        {
            _ladderScatter ^= _ladderScatter << 13;
            _ladderScatter ^= _ladderScatter >> 17;
            _ladderScatter ^= _ladderScatter << 5;
            return _ladderScatter;
        }

        private float Scatter01() => (Scatter() & 0xFFFFFF) / (float)0x1000000;

        private void Sparks(RectTransform star)
        {
            if (_ladderFx == null || star == null) return;
            var world = star.TransformPoint(star.rect.center);
            var local = _ladderFx.InverseTransformPoint(world);
            var art = ItemArt.Star(true, 16f);
            for (int i = 0; i < 7; i++)
            {
                float ang = (i * 51.4f + 20f + Scatter01() * 20f) * Mathf.Deg2Rad;
                float speed = 110f + Scatter01() * 70f;
                var p = Spawn(local, 9f, art, Color.white);
                p.Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
                p.Life = 0.55f; p.Spin = 240f; p.Fade = true;
            }
        }

        private void Confetti(int count)
        {
            if (_ladderFx == null) return;
            var inks = new[] { UITheme.Amber[3], UITheme.Magenta[3], UITheme.Cyan[3], UITheme.Cream[4], UITheme.ViceRed[3], UITheme.Lime[3] };
            var r = _ladderFx.rect;
            for (int i = 0; i < count; i++)
            {
                var at = new Vector2(r.xMin + Scatter01() * r.width, r.yMax + 20f + Scatter01() * 220f);
                var p = Spawn(at, 0f, null, inks[(int)(Scatter() % (uint)inks.Length)]);
                p.Rt.sizeDelta = new Vector2(7f, 4f);
                p.Vel = new Vector2(0f, -(120f + Scatter01() * 130f));
                p.Sway = 20f + Scatter01() * 40f;
                p.Spin = (Scatter01() - 0.5f) * 520f;
                p.Life = 6f; p.Fade = false;
            }
        }

        private LadderParticle Spawn(Vector2 at, float size, Sprite art, Color ink)
        {
            var rt = NewRect("P", _ladderFx);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = at;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = art; img.color = ink; img.preserveAspect = true; img.raycastTarget = false;
            var p = new LadderParticle { Rt = rt, Img = img, Size = size };
            _ladderParticles.Add(p);
            return p;
        }

        private void StepParticles(float dt)
        {
            if (_ladderParticles.Count == 0) return;
            float floor = _ladderFx != null ? _ladderFx.rect.yMin - 20f : -400f;
            for (int i = _ladderParticles.Count - 1; i >= 0; i--)
            {
                var p = _ladderParticles[i];
                p.Age += dt;
                var pos = p.Rt.anchoredPosition + p.Vel * dt;
                if (p.Sway > 0f) pos.x += Mathf.Sin(p.Age * 3.1f + i) * p.Sway * dt;
                if (p.Fade) p.Vel *= Mathf.Max(0f, 1f - 4f * dt);
                p.Rt.anchoredPosition = pos;
                p.Rt.localRotation = Quaternion.Euler(0f, 0f, p.Age * p.Spin);
                bool dead = p.Age >= p.Life || pos.y < floor;
                if (p.Fade)
                {
                    float f = 1f - Mathf.Clamp01(p.Age / p.Life);
                    p.Img.color = new Color(p.Img.color.r, p.Img.color.g, p.Img.color.b, f);
                    p.Rt.sizeDelta = new Vector2(p.Size * (0.4f + 0.6f * f), p.Size * (0.4f + 0.6f * f));
                }
                if (dead)
                {
                    Destroy(p.Rt.gameObject);
                    _ladderParticles.RemoveAt(i);
                }
            }
        }

        private void ClearParticles()
        {
            foreach (var p in _ladderParticles) if (p.Rt != null) Destroy(p.Rt.gameObject);
            _ladderParticles.Clear();
        }

        // ── doors ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>THE BILL'S DOOR: called the beat the stand board's climb lands. Opens only when the night's
        /// close will lift the bar onto a rung it has not stood on, and only once per rung.</summary>
        private void OfferLadderForTonight(TycoonRun run)
        {
            if (run == null) return;
            if (!ReferenceEquals(run, _ladderSeenRun)) { _ladderSeen = -1; _ladderSeenRun = run; }
            var before = run.Rank;
            var after = run.RankAfterTonight;
            if (after.Index <= before.Index || after.Index <= _ladderSeen) return;
            ShowLadder(run, run.Rating.Average, run.StandingAfterTonight, climb: true);
        }

        /// <summary>THE STAR ROW'S DOOR: the certificate the bar holds, at rest — and at the foot of the ladder,
        /// where there is none yet, a word about when the first one comes.</summary>
        private void OpenLadderFromTheBeam()
        {
            var run = Run;
            if (run == null || _ladderPanel == null) return;
            if (_ladderPanel.gameObject.activeSelf) { CloseLadder(); return; }
            if (!ReferenceEquals(run, _ladderSeenRun)) { _ladderSeen = -1; _ladderSeenRun = run; }
            if (run.Rank.Index == 0)
            {
                Toast(UIText.T("rank.window.no_certificate", ("stars", BarRank.Rungs[1].Stars.ToString("0.0"))));
                return;
            }
            double stars = run.Rating.BestStanding;
            ShowLadder(run, stars, stars, climb: false);
        }

        /// <summary>A rung reached and not yet looked at wears NEW on the star row.</summary>
        private void RefreshLadderFlag(TycoonRun run)
        {
            if (_ladderNewFlag == null || run == null) return;
            if (!ReferenceEquals(run, _ladderSeenRun)) { _ladderSeen = -1; _ladderSeenRun = run; }
            bool fresh = run.Rank.Index > Math.Max(0, _ladderSeen);
            if (_ladderNewFlag.gameObject.activeSelf != fresh) _ladderNewFlag.gameObject.SetActive(fresh);
        }
    }
}
