using System;
using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE CERTIFICATE (PLAN_rank_ladder L2/L4/L5, PLAN_second_look §1, 2026-09-21; fourth draft the same day,
    /// the author: "sertifika düz bir kağıt gibi, biraz daha desenler ... kağıt hissiyatı; her sertifikada sıradaki
    /// açılacaklar gözükmeli, tarifler dahil; daha büyük yazılar, başlık kalınlığı ve stili değişsin; şişeler dolu
    /// gözüksün, arkaplanın üstünde öne çıksın; arkaplanda mavi UI olmasın, sadece sertifika ve devam et").
    ///
    /// One sheet of paper over the dimmed room and one key under it, nothing else. The sheet is 1024 wide always,
    /// 16:9 at the least and taller only when its tiles need it. On it, in the night ink with the one amber
    /// accent: the heading, the rank's title (the display face at 24, with its shadow and an accent underline),
    /// the stars, one line of meta, COMFORT in medals and SERVICE in hearts, then two bands of picture tiles on
    /// dark plates — WHAT THIS RUNG OPENED and WHAT THE NEXT RUNG WILL (dimmed) — each grouped by where it lives,
    /// bottles drawn full from their plates. The paper has a grain, a double rule with corner brackets, a faint
    /// star watermark, and the seal at its foot's right. One motion: the sheet unrolls, the stars climb and pop,
    /// a cheer and confetti as the climb lands, the seal is pressed, the tiles come up.
    ///
    /// Two doors: at the night's end, the beat after the bill's climb lands on a new rung, the page opens by
    /// itself with the climb; from the top bar's star row it opens for the rank the bar is on, at rest. No
    /// certificate for the foot of the ladder. Everything here reads BarRank and the run's own gates.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private RectTransform _ladderPanel, _ladderGroup, _ladderReveal, _ladderPaper, _ladderBand, _ladderNextBand, _ladderFx, _ladderNewFlag;
        private RectTransform _ladderSeal, _ladderRibbonL, _ladderRibbonR;
        private Text _ladderHeading, _ladderTo, _ladderToShadow, _ladderMeta, _ladderBandHead, _ladderNextHead, _ladderComfortValue, _ladderServiceValue;
        private Image[] _ladderToStars, _ladderHearts, _ladderMedals;
        private readonly List<CanvasGroup> _ladderTiles = new List<CanvasGroup>();
        private float _ladderFxT = -1f, _ladderPaperH;
        private double _ladderFromStanding, _ladderToStanding;
        private readonly float[] _ladderStarPop = new float[BarRating.MaxStars];
        private readonly bool[] _ladderStarLanded = new bool[BarRating.MaxStars];
        private readonly List<LadderParticle> _ladderParticles = new List<LadderParticle>();
        private uint _ladderScatter = 0x9E3779B9u;
        private int _ladderWave;
        private bool _ladderCheered;
        /// <summary>The highest rung this run has had its window opened for, and the run it belongs to.</summary>
        private int _ladderSeen = -1;
        private TycoonRun _ladderSeenRun;

        private const float LadderClimb = 1.2f, LadderStar = 32f, ClimbStart = 0.6f;
        /// <summary>The sheet: 1024 wide, always; 16:9 (576) at the least, taller only when the tiles need it —
        /// the author's rule: never wider, only taller.</summary>
        private const float PaperW = 1024f, PaperMinH = 576f, PaperSide = 48f;
        /// <summary>The tiles: 80 square on an 88 pitch, ten to a row, a name of two lines under each, a class
        /// caption over the first of each group. This rung's band takes two rows at most, the next rung's one.</summary>
        private const float TileSize = 80f, TilePitch = 88f, TileGroupGap = 16f, TileRowH = 122f;
        private const int TilesPerRow = 10;
        /// <summary>Where this rung's tiles begin under the sheet's top, the gap before the next rung's band (its
        /// head included), and what the sheet keeps under its last row.</summary>
        private const float TilesTop = 218f, NextBandGap = 36f, PaperFoot = 28f;
        /// <summary>The key under the sheet: the gap above it.</summary>
        private const float KeyGap = 16f;

        private sealed class LadderParticle
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Vel;
            public float Life, Age, Spin, Sway;
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

            // The sheet and its key stand together, centred; the group is sized to them when the page opens.
            _ladderGroup = NewRect("Group", _ladderPanel);
            Place(_ladderGroup, new Vector2(0.5f, 0.5f), new Vector2(PaperW, PaperMinH + KeyGap + PauseKeyH), Vector2.zero);

            // The sheet stands inside a reveal: a masked rect that opens downward when the page is moving.
            _ladderReveal = NewRect("Reveal", _ladderGroup);
            Place(_ladderReveal, new Vector2(0.5f, 1f), new Vector2(PaperW, PaperMinH), Vector2.zero);
            _ladderReveal.pivot = new Vector2(0.5f, 1f);
            _ladderReveal.gameObject.AddComponent<RectMask2D>();
            BuildSheet();

            PackWordKey(_ladderGroup, "CONTINUE", UIText.T("rank.window.continue"), "next", MenuPack.Tone.Orange,
                new Vector2(0.5f, 0f), new Vector2(240f, PauseKeyH), Vector2.zero, CloseLadder, 240f, 48f + 24f);

            // The celebration's layer: confetti over the sheet, never in the pointer's way.
            _ladderFx = NewRect("Fx", _ladderPanel);
            Stretch(_ladderFx, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _ladderPanel.gameObject.SetActive(false);
        }

        /// <summary>THE SHEET: cream paper with a grain, a double rule with brackets at its corners, a faint star
        /// behind the writing, and the certificate's lines in the night ink.</summary>
        private void BuildSheet()
        {
            _ladderPaper = NewRect("Paper", _ladderReveal);
            Place(_ladderPaper, new Vector2(0.5f, 1f), new Vector2(PaperW, PaperMinH), Vector2.zero);
            _ladderPaper.pivot = new Vector2(0.5f, 1f);
            var paperImg = _ladderPaper.gameObject.AddComponent<Image>();
            paperImg.color = UITheme.Cream[4];
            paperImg.raycastTarget = false;
            // the grain: a speckle tile laid over the cream, faint
            var grain = NewRect("Grain", _ladderPaper);
            Stretch(grain, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gi = grain.gameObject.AddComponent<Image>();
            gi.sprite = PaperGrain(64); gi.type = Image.Type.Tiled; gi.pixelsPerUnitMultiplier = 0.5f;
            gi.color = new Color(1f, 1f, 1f, 0.10f); gi.raycastTarget = false;
            UiAuditExempt.Mark(grain, "paper grain, a speckle tile at 2x under a tenth of alpha");
            // the watermark: the house's star, large and almost not there, behind the writing
            var mark = NewRect("Watermark", _ladderPaper);
            Place(mark, new Vector2(0.5f, 0.5f), new Vector2(360f, 360f), new Vector2(0f, 0f));
            var mi = mark.gameObject.AddComponent<Image>();
            mi.sprite = ItemArt.Star(false, 32f); mi.preserveAspect = true;
            mi.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.05f); mi.raycastTarget = false;
            // the rules: two deep outside, one inside, and a bracket at each corner between them
            var outer = NewRect("RuleOuter", _ladderPaper);
            Stretch(outer, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            Frame(outer, 2f, UITheme.Night[3]);
            var inner = NewRect("RuleInner", _ladderPaper);
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -18f));
            Frame(inner, 1f, new Color(UITheme.Night[3].r, UITheme.Night[3].g, UITheme.Night[3].b, 0.55f));
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
                Bracket(_ladderPaper, corner);

            _ladderHeading = NewText("Heading", _ladderPaper, _body, 16, TextAnchor.MiddleCenter, UITheme.Night[2]);
            Place(_ladderHeading.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 20f), new Vector2(0f, -22f));
            _ladderHeading.horizontalOverflow = HorizontalWrapMode.Overflow;

            // The title the bar is hereby known as: the display face's large size, its shadow two units under
            // and right so it stands off the paper, and the accent's rule under it.
            _ladderToShadow = NewText("ToShadow", _ladderPaper, _display, 24, TextAnchor.UpperCenter, UITheme.Night[3]);
            Place(_ladderToShadow.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 32f), new Vector2(2f, -46f));
            _ladderToShadow.rectTransform.pivot = new Vector2(0.5f, 1f);
            _ladderToShadow.horizontalOverflow = HorizontalWrapMode.Overflow;
            _ladderTo = NewText("To", _ladderPaper, _display, 24, TextAnchor.UpperCenter, UITheme.Night[0]);
            Place(_ladderTo.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 32f), new Vector2(0f, -44f));
            _ladderTo.rectTransform.pivot = new Vector2(0.5f, 1f);
            _ladderTo.horizontalOverflow = HorizontalWrapMode.Overflow;
            var under = NewRect("Underline", _ladderPaper);
            Place(under, new Vector2(0.5f, 1f), new Vector2(240f, 2f), new Vector2(0f, -80f));
            var ui = under.gameObject.AddComponent<Image>();
            ui.color = UITheme.Amber[3]; ui.raycastTarget = false;

            _ladderToStars = LiveStarRow(_ladderPaper, new Vector2(0.5f, 1f), new Vector2(0f, -92f), LadderStar, 4f,
                UITheme.Amber[3], UITheme.Night[3]);
            _ladderMeta = NewText("Meta", _ladderPaper, _body, 16, TextAnchor.MiddleCenter, UITheme.Night[1]);
            Place(_ladderMeta.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 20f), new Vector2(0f, -136f));
            _ladderMeta.horizontalOverflow = HorizontalWrapMode.Overflow;

            // COMFORT in the house's medals and SERVICE in its hearts, the way the top bar wears them.
            _ladderMedals = ScoreGroup("Comfort", -240f, -160f, "rank.cert.comfort", ItemArt.Medal, out _ladderComfortValue);
            _ladderHearts = ScoreGroup("Service", 240f, -160f, "rank.cert.service", ItemArt.Heart, out _ladderServiceValue);

            var cut = NewRect("Cut", _ladderPaper);
            Place(cut, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 1f), new Vector2(0f, -184f));
            var ci = cut.gameObject.AddComponent<Image>();
            ci.color = UITheme.Night[3]; ci.raycastTarget = false;

            _ladderBandHead = BandHead("BandHead", -196f);
            _ladderBand = NewRect("Tiles", _ladderPaper);
            Place(_ladderBand, new Vector2(0.5f, 1f), new Vector2(PaperW, TileRowH), new Vector2(0f, -TilesTop));
            _ladderBand.pivot = new Vector2(0.5f, 1f);
            _ladderNextHead = BandHead("NextHead", -400f);
            _ladderNextBand = NewRect("NextTiles", _ladderPaper);
            Place(_ladderNextBand, new Vector2(0.5f, 1f), new Vector2(PaperW, TileRowH), new Vector2(0f, -420f));
            _ladderNextBand.pivot = new Vector2(0.5f, 1f);

            // THE SEAL, in the sheet's bottom-right corner: two ribbon tails first (they hang from behind it,
            // out past the sheet's edge), then the disc with the cocktail glass in it. All in the one accent.
            _ladderRibbonL = Ribbon("RibbonL", -7f, UITheme.Amber[1]);
            _ladderRibbonR = Ribbon("RibbonR", 7f, UITheme.Amber[2]);
            _ladderSeal = NewRect("Seal", _ladderPaper);
            _ladderSeal.anchorMin = _ladderSeal.anchorMax = new Vector2(1f, 0f);
            _ladderSeal.pivot = new Vector2(0.5f, 0.5f);
            _ladderSeal.sizeDelta = new Vector2(56f, 56f);
            _ladderSeal.anchoredPosition = new Vector2(-PaperSide - 20f, 52f);
            var disc = _ladderSeal.gameObject.AddComponent<Image>();
            disc.sprite = SealDisc(56); disc.color = Color.white; disc.raycastTarget = false;
            var glassRt = NewRect("Glass", _ladderSeal);
            Place(glassRt, new Vector2(0.5f, 0.5f), new Vector2(26f, 32f), new Vector2(0f, 1f));
            var glass = glassRt.gameObject.AddComponent<Image>();
            glass.sprite = ItemArt.Load("glass3d_martini_t2_Front");
            glass.color = UITheme.Cream[4]; glass.preserveAspect = true; glass.raycastTarget = false;
            glass.enabled = glass.sprite != null;
        }

        /// <summary>A band's head: sixteen-pixel writing, left-aligned at the sheet's side.</summary>
        private Text BandHead(string id, float y)
        {
            var t = NewText(id, _ladderPaper, _body, 16, TextAnchor.MiddleLeft, UITheme.Night[1]);
            Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(PaperW - 2f * PaperSide, 20f), new Vector2(PaperSide, y));
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// <summary>A corner bracket: two short strokes meeting at the corner, inside the rules.</summary>
        private void Bracket(RectTransform paper, Vector2 corner)
        {
            const float inset = 24f, arm = 26f, stroke = 2f;
            float sx = corner.x < 0.5f ? 1f : -1f, sy = corner.y < 0.5f ? 1f : -1f;
            var h = NewRect("BracketH", paper);
            h.anchorMin = h.anchorMax = corner; h.pivot = corner;
            h.sizeDelta = new Vector2(arm, stroke);
            h.anchoredPosition = new Vector2(sx * inset, sy * inset);
            var hi = h.gameObject.AddComponent<Image>(); hi.color = UITheme.Night[2]; hi.raycastTarget = false;
            var v = NewRect("BracketV", paper);
            v.anchorMin = v.anchorMax = corner; v.pivot = corner;
            v.sizeDelta = new Vector2(stroke, arm);
            v.anchoredPosition = new Vector2(sx * inset, sy * inset);
            var vi = v.gameObject.AddComponent<Image>(); vi.color = UITheme.Night[2]; vi.raycastTarget = false;
        }

        /// <summary>A score on the sheet: its label, five of the house's icons filled to the value, the number.</summary>
        private Image[] ScoreGroup(string id, float x, float y, string labelKey, Func<bool, float, Sprite> art, out Text value)
        {
            const float px = 16f, gap = 2f, labelW = 110f, valueW = 48f;
            float rowW = BarRating.MaxStars * (px + gap) - gap;
            float total = labelW + 10f + rowW + 10f + valueW;
            float left = x - total * 0.5f;
            var label = NewText(id + "Label", _ladderPaper, _body, 16, TextAnchor.MiddleRight, UITheme.Night[1]);
            Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(labelW, 20f), new Vector2(left + labelW * 0.5f, y));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = UIText.T(labelKey);
            var row = NewRect(id + "Row", _ladderPaper);
            Place(row, new Vector2(0.5f, 1f), new Vector2(rowW, px), new Vector2(left + labelW + 10f + rowW * 0.5f, y));
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
            value = NewText(id + "Value", _ladderPaper, _body, 16, TextAnchor.MiddleLeft, UITheme.Night[0]);
            Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(valueW, 20f), new Vector2(left + total - valueW * 0.5f, y));
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            return fills;
        }

        private static void SetRowFill(Image[] fills, double value)
        {
            if (fills == null) return;
            for (int i = 0; i < fills.Length; i++)
                if (fills[i] != null) fills[i].fillAmount = Mathf.Clamp01((float)(value - i));
        }

        /// <summary>A ribbon tail: hangs from the seal's centre, pivot at its top, offset <paramref name="dx"/>.</summary>
        private RectTransform Ribbon(string id, float dx, Color ink)
        {
            var rt = NewRect(id, _ladderPaper);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(12f, 62f);
            rt.anchoredPosition = new Vector2(-PaperSide - 20f + dx, 52f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = ink; img.raycastTarget = false;
            return rt;
        }

        private static Sprite s_sealDisc, s_paperGrain;

        /// <summary>The seal's disc: a flat amber circle with a thin lighter ring inside its edge, drawn once.</summary>
        private static Sprite SealDisc(int px)
        {
            if (s_sealDisc != null) return s_sealDisc;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "seal_disc" };
            var clear = new Color(0f, 0f, 0f, 0f);
            var fill = UITheme.Amber[2];
            var ring = UITheme.Amber[3];
            float c = px * 0.5f, R = c - 0.5f;
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color p = clear;
                    if (r <= R) p = fill;
                    if (r > R - 5f && r <= R - 3f) p = ring;
                    tex.SetPixel(x, y, p);
                }
            tex.Apply(false, false);
            s_sealDisc = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            s_sealDisc.name = "seal_disc";
            return s_sealDisc;
        }

        /// <summary>The paper's grain: a tile of light and dark specks, most of it clear, drawn once. Laid at 2x
        /// under a tenth of alpha it reads as fibre, not noise.</summary>
        private static Sprite PaperGrain(int px)
        {
            if (s_paperGrain != null) return s_paperGrain;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "paper_grain", wrapMode = TextureWrapMode.Repeat };
            uint seed = 0x2545F491u;
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                    uint r = seed & 0xFF;
                    Color p = new Color(0f, 0f, 0f, 0f);
                    if (r < 22) p = new Color(1f, 1f, 1f, 1f);            // a light fleck
                    else if (r < 40) p = new Color(0.25f, 0.2f, 0.16f, 1f); // a dark one
                    tex.SetPixel(x, y, p);
                }
            tex.Apply(false, false);
            s_paperGrain = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            s_paperGrain.name = "paper_grain";
            return s_paperGrain;
        }

        /// <summary>
        /// THE AUTHOR'S BLUE PLATE (2026-09-21: "UI ayarlarken bu arkaplanları kullan ... panellerde kullanılacaksa
        /// orta kısmın şeffaflığı olmamalı"): ui_blue 9-sliced at 2x, its rings kept at their drawn width whatever
        /// the size, and a solid of the plate's own ink laid under the middle so nothing behind shows through. A
        /// project without the drawing falls back to the night plate. The pause menu and the settings stand on it.
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
            _ladderHeading.text = UIText.T(climb ? "rank.window.title" : "rank.window.title_again");
            _ladderTo.text = _ladderToShadow.text = UIText.T(now.Title);
            _ladderMeta.text = climb
                ? UIText.T("rank.cert.meta_climb", ("title", UIText.T(was.Title)), ("night", run.Day.ToString()))
                : UIText.T("rank.cert.foot_again", ("night", run.Day.ToString()));
            _ladderFromStanding = from;
            _ladderToStanding = to;
            SetStars(_ladderToStars, moving ? from : to);
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                _ladderStarPop[i] = -1f;
                _ladderStarLanded[i] = !moving || from >= i + 1 - 1e-6;
                var cell = _ladderToStars[i].rectTransform.parent as RectTransform;
                if (cell != null) cell.localScale = Vector3.one;
            }
            double comfort = run.Phase == TycoonPhase.DayEnd ? run.ComfortTonight : run.ComfortNow;
            double service = run.ServiceTonight;
            SetRowFill(_ladderMedals, comfort);
            SetRowFill(_ladderHearts, service);
            _ladderComfortValue.text = comfort.ToString("0.0");
            _ladderServiceValue.text = service.ToString("0.0");

            // This rung's band, then the next rung's, dimmed, so every certificate says what comes next — the
            // author: "her sertifikada sıradaki açılacaklar gözükmeli, tarifler dahil".
            _ladderTiles.Clear();
            _ladderBandHead.text = UIText.T("rank.window.new");
            int rows = LayTiles(_ladderBand, run, TilesFor(run, was, now, climb), moving, rowsMax: 2, dim: false);
            float nextTop = TilesTop + rows * TileRowH + NextBandGap;
            var next = BarRank.Above(now);
            int nextRows;
            if (next != null)
            {
                _ladderNextHead.text = UIText.T("rank.window.next", ("stars", next.Stars.ToString("0.0")));
                nextRows = LayTiles(_ladderNextBand, run, TilesFor(run, now, next, true), moving, rowsMax: 1, dim: true);
            }
            else
            {
                _ladderNextHead.text = UIText.T("rank.window.top");
                foreach (Transform old in _ladderNextBand) Destroy(old.gameObject);
                nextRows = 0;
            }
            _ladderNextHead.rectTransform.anchoredPosition = new Vector2(PaperSide, -(nextTop - 22f));
            _ladderNextBand.anchoredPosition = new Vector2(0f, -nextTop);

            // The sheet is as tall as its bands, and never shorter than 16:9; the group holds it and the key.
            _ladderPaperH = Mathf.Max(PaperMinH, nextTop + nextRows * TileRowH + (nextRows == 0 ? 8f : 0f) + PaperFoot);
            _ladderPaper.sizeDelta = new Vector2(PaperW, _ladderPaperH);
            _ladderReveal.sizeDelta = new Vector2(PaperW, moving ? 0f : _ladderPaperH);
            _ladderGroup.sizeDelta = new Vector2(PaperW, _ladderPaperH + KeyGap + PauseKeyH);
            _ladderGroup.anchoredPosition = Vector2.zero;
            _ladderGroup.localScale = moving ? new Vector3(0.94f, 0.94f, 1f) : Vector3.one;

            float sealScale = moving ? 0f : 1f;
            _ladderSeal.localScale = new Vector3(sealScale, sealScale, 1f);
            _ladderRibbonL.localScale = new Vector3(1f, sealScale, 1f);
            _ladderRibbonR.localScale = new Vector3(1f, sealScale, 1f);

            ClearParticles();
            _ladderFxT = moving ? 0f : -1f;
            _ladderWave = 0;
            _ladderCheered = false;

            _ladderSeenRun = run;
            if (now.Index > _ladderSeen) _ladderSeen = now.Index;
            RefreshLadderFlag(run);
            CloseId();
            _ladderPanel.gameObject.SetActive(true);
            Sfx.Play(climb ? "level_up" : "menu_open", climb ? 0.9f : 0.7f);
        }

        private sealed class CertTile
        {
            public string Cls, Name;
            public Sprite Art;
            public IngredientCard Bottle;   // drawn full from its plates when set
        }

        /// <summary>
        /// WHAT A RUNG BRINGS, AS TILES: the counter's dishes, the door, the bench's spoon, the taps' lines, the
        /// market's bottles and the book's pages the rung's stars unlock, grouped by where they live. Every rung
        /// from the one after <paramref name="was"/> up to <paramref name="now"/> when <paramref name="all"/>
        /// (a night that climbs two rungs lists both); the one rung otherwise.
        /// </summary>
        private List<CertTile> TilesFor(TycoonRun run, Rung was, Rung now, bool all)
        {
            var tiles = new List<CertTile>();
            for (int i = all ? was.Index + 1 : now.Index; i <= now.Index; i++)
            {
                var rung = BarRank.Rungs[i];
                foreach (var f in rung.Opens)
                    foreach (var (cls, name, art) in FeatureTiles(f)) tiles.Add(new CertTile { Cls = cls, Name = name, Art = art });
                foreach (var card in run.BottlesOpeningAt(rung.Stars))
                    tiles.Add(new CertTile { Cls = "rank.cert.class.market", Name = UIText.Caps(UIText.Data("bottle", card.Id, "name", card.Name)),
                        Art = ItemArt.Bottle(card) ?? ItemArt.StyleBottle(run.CatalogueBottles, card.Info?.Style), Bottle = card });
                foreach (var r in run.RecipesOpeningAt(rung.Stars))
                    tiles.Add(new CertTile { Cls = "rank.cert.class.book", Name = UIText.Caps(UIText.Data("recipe", r.Id, "name", r.Name)),
                        Art = _bootstrap != null ? DrinkIcon.For(r, _bootstrap.Glassware) : null });
            }
            // Each class together, in the order the room reads them.
            var byClass = new List<CertTile>(tiles.Count);
            foreach (var cls in TileClasses)
                foreach (var t in tiles) if (t.Cls == cls) byClass.Add(t);
            foreach (var t in tiles) if (Array.IndexOf(TileClasses, t.Cls) < 0) byClass.Add(t);
            return byClass;
        }

        /// <summary>Lays <paramref name="tiles"/> in <paramref name="band"/>: the slots first, dry, under the wrap
        /// rule; then the cut at <paramref name="rowsMax"/> rows, the last slot standing for the rest. Returns
        /// the rows taken (at least one, which a band with nothing in it uses for its one line).</summary>
        private int LayTiles(RectTransform band, TycoonRun run, List<CertTile> tiles, bool moving, int rowsMax, bool dim)
        {
            foreach (Transform old in band) Destroy(old.gameObject);
            if (tiles.Count == 0)
            {
                var none = NewText("None", band, _body, 16, TextAnchor.MiddleLeft, UITheme.Night[2]);
                Place(none.rectTransform, new Vector2(0f, 1f), new Vector2(PaperW - 2f * PaperSide, 20f), new Vector2(PaperSide, -34f));
                none.rectTransform.pivot = new Vector2(0f, 0.5f);
                none.horizontalOverflow = HorizontalWrapMode.Overflow;
                none.text = UIText.T("rank.window.nothing_new");
                return 1;
            }
            float rowW = TilesPerRow * TilePitch - (TilePitch - TileSize);
            float left = PaperSide;
            var slots = new List<(float x, float y, int row, bool head)>(tiles.Count);
            {
                float x = 0f, y = 0f;
                int row = 0;
                string lastClass = null;
                foreach (var t in tiles)
                {
                    bool groupHead = t.Cls != lastClass;
                    if (groupHead && x > 0f) x += TileGroupGap;
                    if (x + TileSize > rowW + 0.5f) { x = 0f; row++; y -= TileRowH; groupHead = true; }
                    slots.Add((left + x, y, row, groupHead));
                    lastClass = t.Cls;
                    x += TilePitch;
                }
            }
            int cut = slots.FindIndex(s => s.row >= rowsMax);
            int shown = cut < 0 ? tiles.Count : Math.Max(0, cut - 1);
            int rest = tiles.Count - shown;
            int rows = 0;
            for (int i = 0; i < shown; i++)
            {
                var (x, y, row, isHead) = slots[i];
                var tile = Tile(band, x, y, tiles[i], moving, dim);
                if (isHead)
                {
                    var cap = NewText("Class", tile, _body, 8, TextAnchor.MiddleLeft, UITheme.Night[2]);
                    Place(cap.rectTransform, new Vector2(0f, 1f), new Vector2(200f, 12f), new Vector2(0f, 12f));
                    cap.rectTransform.pivot = new Vector2(0f, 1f);
                    cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                    cap.text = UIText.T(tiles[i].Cls);
                }
                rows = Math.Max(rows, row + 1);
            }
            if (rest > 0 && shown < slots.Count)
            {
                var (x, y, row, _) = slots[shown];
                Tile(band, x, y, new CertTile { Cls = "", Name = "+" + rest }, moving, dim);
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

        /// <summary>One tile: a dark plate on the paper so the picture stands off it, the picture centred — a
        /// bottle drawn FULL from its plates — and the name under it in two lines at most. Faded in on its turn
        /// when the page is moving; held at half when it belongs to the next rung.</summary>
        private RectTransform Tile(RectTransform band, float x, float y, CertTile spec, bool moving, bool dim)
        {
            var rt = NewRect("Tile", band);
            Place(rt, new Vector2(0f, 1f), new Vector2(TileSize, TileSize), new Vector2(x, y - 12f));
            rt.pivot = new Vector2(0f, 1f);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = UITheme.Night[1]; bg.raycastTarget = false;
            Frame(rt, 1f, UITheme.Cream[2]);
            if (spec.Bottle != null && ItemArt.Plates(spec.Bottle, cellar: true) is ItemArt.BottlePlates plates)
            {
                // the bottle whole: its cellar plates at their own size, filled to the brim in its own liquid
                var vessel = NewRect("Vessel", rt);
                Place(vessel, new Vector2(0.5f, 0.5f), new Vector2(32f, 64f), new Vector2(0f, 0f));
                var art = BottleArt.Under(vessel);
                art.Show(plates);
                if (!plates.Sealed) art.SetLevel(UITheme.LiquidColor(spec.Bottle.Info?.Style, spec.Bottle.Type), 1.0, 0f);
            }
            else
            {
                var picRt = NewRect("Pic", rt);
                Place(picRt, new Vector2(0.5f, 0.5f), new Vector2(TileSize - 18f, TileSize - 18f), Vector2.zero);
                var pic = picRt.gameObject.AddComponent<Image>();
                pic.sprite = spec.Art; pic.preserveAspect = true; pic.raycastTarget = false;
                pic.enabled = spec.Art != null;
                if (spec.Art == null)
                {
                    var plus = NewText("Plus", rt, _display, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
                    Stretch(plus.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    plus.text = spec.Name;
                }
            }
            var cap = NewText("Name", rt, _body, 8, TextAnchor.UpperCenter, UITheme.Night[1]);
            Place(cap.rectTransform, new Vector2(0.5f, 0f), new Vector2(TilePitch, 22f), new Vector2(0f, -3f));
            cap.rectTransform.pivot = new Vector2(0.5f, 1f);
            cap.horizontalOverflow = HorizontalWrapMode.Wrap;
            cap.text = spec.Art == null && spec.Bottle == null ? "" : spec.Name;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = moving ? 0f : dim ? 0.55f : 1f;
            group.blocksRaycasts = false; group.interactable = false;
            _ladderTiles.Add(group);
            return rt;
        }

        /// <summary>A feature's tiles: the pictures the room itself draws these things with.</summary>
        private static IEnumerable<(string cls, string name, Sprite art)> FeatureTiles(Feature f)
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
            _ladderFxT = -1f;
            ClearParticles();
            _ladderPanel.gameObject.SetActive(false);
            Sfx.Play("menu_close", 0.6f);
        }

        // ── the ceremony, frame by frame ──────────────────────────────────────────────────────────────

        /// <summary>
        /// ON THE UNSCALED CLOCK AT THE CEREMONY'S PACE: the sheet settles and unrolls, the stars climb from the
        /// old standing to the new and pop as they land, the seal is pressed with its ribbons, the tiles come up
        /// one after another, and confetti falls with a cheer as the climb lands.
        /// </summary>
        private void StepLadder()
        {
            if (_ladderPanel == null || !_ladderPanel.gameObject.activeSelf) return;
            float dt = Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace;
            if (_ladderFxT >= 0f)
            {
                _ladderFxT += dt;
                float t = _ladderFxT;

                float settle = Mathf.Clamp01(t / 0.25f);
                float s = 0.94f + 0.06f * (1f - (1f - settle) * (1f - settle));
                _ladderGroup.localScale = new Vector3(s, s, 1f);
                float open = Mathf.Clamp01(t / 0.5f);
                _ladderReveal.sizeDelta = new Vector2(PaperW, _ladderPaperH * (1f - (1f - open) * (1f - open)));

                float k = Mathf.Clamp01((t - ClimbStart) / LadderClimb);
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
                        Sfx.Play("star_earn", 0.5f);
                    }
                }
                for (int i = 0; i < BarRating.MaxStars; i++)
                {
                    if (_ladderStarPop[i] < 0f) continue;
                    _ladderStarPop[i] += dt;
                    float p = Mathf.Clamp01(_ladderStarPop[i] / 0.28f);
                    float sc = 1f + 0.4f * Mathf.Sin(p * Mathf.PI);
                    var cell = _ladderToStars[i].rectTransform.parent as RectTransform;
                    if (cell != null) cell.localScale = new Vector3(sc, sc, 1f);
                    if (p >= 1f) _ladderStarPop[i] = -1f;
                }

                float landed = t - (ClimbStart + LadderClimb);
                if (landed >= 0f && !_ladderCheered)
                {
                    _ladderCheered = true;
                    Sfx.Play("cheer_sfx", 0.6f);
                    Confetti(30);
                }
                float sealT = landed - 0.1f;
                if (sealT >= 0f)
                {
                    if (sealT - dt < 0f) Sfx.Play("stamp", 0.8f);
                    float q = Mathf.Clamp01(sealT / 0.22f);
                    float sc = Mathf.Lerp(1.8f, 1f, 1f - (1f - q) * (1f - q) * (1f - q));
                    _ladderSeal.localScale = new Vector3(sc, sc, 1f);
                    float rq = Mathf.Clamp01((sealT - 0.15f) / 0.3f);
                    float ry = 1f - (1f - rq) * (1f - rq);
                    _ladderRibbonL.localScale = new Vector3(1f, ry, 1f);
                    _ladderRibbonR.localScale = new Vector3(1f, ry, 1f);
                }

                // the tiles come up one every twentieth of a second once the seal is down; the next rung's stay dim
                float tileT = landed - 0.3f;
                for (int i = 0; i < _ladderTiles.Count; i++)
                {
                    var g = _ladderTiles[i];
                    if (g == null) continue;
                    bool dimmed = g.transform.parent == _ladderNextBand;
                    g.alpha = Mathf.Clamp01((tileT - i * 0.05f) / 0.2f) * (dimmed ? 0.55f : 1f);
                }

                if (_ladderWave == 0 && t >= 0.15f) { Confetti(36); _ladderWave = 1; }
                if (t > 12f) _ladderFxT = -1f;
            }
            StepParticles(dt);
        }

        // ── confetti ──────────────────────────────────────────────────────────────────────────────────

        private uint Scatter()
        {
            _ladderScatter ^= _ladderScatter << 13;
            _ladderScatter ^= _ladderScatter >> 17;
            _ladderScatter ^= _ladderScatter << 5;
            return _ladderScatter;
        }

        private float Scatter01() => (Scatter() & 0xFFFFFF) / (float)0x1000000;

        /// <summary>Confetti in the page's own roles: the accent, the paper, the plate's bright ring.</summary>
        private void Confetti(int count)
        {
            if (_ladderFx == null) return;
            var inks = new[] { UITheme.Amber[3], UITheme.Cream[4], UITheme.Cyan[4], UITheme.Amber[4] };
            var r = _ladderFx.rect;
            for (int i = 0; i < count; i++)
            {
                var rt = NewRect("P", _ladderFx);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(7f, 4f);
                rt.anchoredPosition = new Vector2(r.xMin + Scatter01() * r.width, r.yMax + 20f + Scatter01() * 220f);
                var img = rt.gameObject.AddComponent<Image>();
                img.color = inks[(int)(Scatter() % (uint)inks.Length)]; img.raycastTarget = false;
                _ladderParticles.Add(new LadderParticle
                {
                    Rt = rt, Img = img,
                    Vel = new Vector2(0f, -(120f + Scatter01() * 130f)),
                    Sway = 20f + Scatter01() * 40f,
                    Spin = (Scatter01() - 0.5f) * 520f,
                    Life = 6f,
                });
            }
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
                pos.x += Mathf.Sin(p.Age * 3.1f + i) * p.Sway * dt;
                p.Rt.anchoredPosition = pos;
                p.Rt.localRotation = Quaternion.Euler(0f, 0f, p.Age * p.Spin);
                if (p.Age >= p.Life || pos.y < floor)
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
