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
        private Text _ladderHeading, _ladderTo, _ladderToShadow, _ladderToBold, _ladderMeta, _ladderBandHead, _ladderNextHead;
        private RectTransform _ladderRuleOuter, _ladderRuleInner, _ladderGilt, _ladderUnderline;
        private RectTransform _ladderBandBox, _ladderNextBox, _ladderSealHost;
        private Image[] _ladderBandBoxEdges, _ladderNextBoxEdges;
        private Image[] _ladderRuleOuterEdges, _ladderRuleInnerEdges, _ladderGiltEdges;
        private List<Image> _ladderBrackets;
        private Image _ladderPaperImg, _ladderPatternImg, _ladderUnderlineImg;
        private Text _ladderComfortLabel, _ladderServiceLabel;
        /// <summary>Whether this sheet stopped the clock when it opened (2026-09-21, the author: "bu ekran açıkken menüde
        /// olduğu gibi zaman durmalı"); let go when it closes. Only during the night - at the day's end nothing runs.</summary>
        private bool _ladderHeldClock;
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
        /// <summary>The tallest the sheet may stand: the 720 field less a hand of room at each end, so the title
        /// and the seal are both in shot however many tiles a rung brings (2026-09-22).</summary>
        private const float PaperMaxH = 612f;   // 720 less the key under the sheet and its gap, and a hand at each end
        /// <summary>The tiles: 80 square on an 88 pitch, ten to a row, a name of two lines under each, a class
        /// caption over the first of each group. This rung's band takes two rows at most, the next rung's one.</summary>
        // 64 on a 72 pitch, twelve to a row (2026-09-21, the author: "kutular daha kalın çerçeveli ve biraz daha küçük
        // olsun böylece daha çok yer açılabilir"): a row of new things takes 104 instead of 122.
        // 112 and 28 (2026-09-22): a group's head is set at 16 now and needs its own line over the tiles, and a
        // group is fenced off from the next by a rule standing in a wider gap.
        private const float TileSize = 64f, TilePitch = 72f, TileGroupGap = 28f, TileRowH = 112f;
        private const int TilesPerRow = 12;
        /// <summary>Where this rung's tiles begin under the sheet's top, the gap before the next rung's band (its
        /// head included), and what the sheet keeps under its last row.</summary>
        // 272 down to the first row, and 64 between the bands: the band's box carries a head above it and a
        // hand of air under its tiles, and the two boxes may not touch (measured, r112).
        private const float TilesTop = 272f, NextBandGap = 64f, PaperFoot = 28f;
        /// <summary>The seal's size on the sheet: PixelLab's drawing at its own 128, or the disc drawn at it.</summary>
        private const float SealPx = 128f;
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

        /// <summary>
        /// HOW GOOD THE PAPER IS (2026-09-22, the author: "sertifika kalitesi yıldız seviyesi yükseldikçe artmalı;
        /// şu anki temel sertifika olmalı, ilerleyen sertifikalar renkleri ve tasarımları bakımından giderek
        /// gelişmeli"). One sheet, six grades, and the grade is the rung - a bar that has climbed is handed better
        /// stationery, which is the oldest way a certificate says what it is worth.
        ///
        /// The design rule holds at every grade (GDD 16 and the memory rule): three colour roles, ONE accent, no
        /// ornament for its own sake. What climbs is the PRINTING - a second rule, cut corners, a gilt edge, a
        /// deeper stock - never a second accent.
        /// </summary>
        private readonly struct CertLook
        {
            public readonly Color Paper;        // the stock
            public readonly Color Rule;         // the outer rule
            public readonly float RuleW;
            public readonly bool InnerRule;     // a second, finer rule inside it
            public readonly bool Brackets;      // cut corners
            public readonly Color Trim;         // brackets, the title's underline, the band heads: the accent
            public readonly float Pattern;      // how far up the glasses printed on the stock come
            public readonly bool Gilt;          // a gold band just inside the edge
            public readonly bool Ribbons;       // the seal's tails
            public readonly float Seal;         // the seal's scale
            public readonly float Wear;         // how used the stock is: foxing, a ring, the folds (1 = a docket in a drawer)
            public readonly int Ornament;       // 0 plain, 1 a chain of diamonds along the gilt, 2 the chain with rosettes

            public CertLook(Color paper, Color rule, float ruleW, bool innerRule, bool brackets, Color trim,
                            float pattern, bool gilt, bool ribbons, float seal, float wear = 0f, int ornament = 0)
            {
                Paper = paper; Rule = rule; RuleW = ruleW; InnerRule = innerRule; Brackets = brackets;
                Trim = trim; Pattern = pattern; Gilt = gilt; Ribbons = ribbons; Seal = seal;
                Wear = wear; Ornament = ornament;
            }
        }

        /// <summary>The six grades, by rung. Nothing here is a new colour: Cream is the stock, Night is the ink,
        /// Amber is the one accent, and the climb spends them a step at a time.</summary>
        private static CertLook LookFor(int rung)
        {
            // Seven rungs (BarRank.Rungs: 0, 0.5, 1, 2, 3, 4, 5 stars), so seven grades - the last two share the
            // best stock, because there is nothing left to add to a sheet that is already gilt.
            // THE RECEIPT'S STOCK, AND IT IMPROVES (2026-09-22, the author's seventh list: "sertifikaların arkaplanını
            // aynı fatura görselinde olduğu gibi üret, 0.5 yıldız sertifikasının kağıdı biraz yıpranmış olmalı giderek
            // düzelecek ve daha premium bir sertifikaya dönüşecek"). The grey Cream ramp is gone from the sheet: it is the
            // slip's own warm white now (TycoonHud.BillPaper), yellowed and handled at the bottom of the ladder and
            // cleaner, then ivory, then ornamented as the bar climbs.
            var slip = BillPaper;
            var yellowed = new Color(slip.r * 0.93f, slip.g * 0.90f, slip.b * 0.80f, 1f);
            var ivory = new Color(0.985f, 0.965f, 0.9f, 1f);
            switch (Mathf.Clamp(rung, 0, 6))
            {
                // 0 stars: a docket out of a drawer. Yellowed, handled, one thin rule, no trim, no seal tails.
                case 0: return new CertLook(yellowed, UITheme.Night[3], 1f, false, false, UITheme.Night[2], 0.10f, false, false, 0.75f, 1f, 0);
                // 0.5: still worn, but the corners are cut and the title gets its line.
                case 1: return new CertLook(Color.Lerp(yellowed, slip, 0.35f), UITheme.Night[3], 2f, false, true, UITheme.Amber[1], 0.14f, false, false, 0.85f, 0.75f, 0);
                // 1: a second rule inside the first, and the stock is nearly clean.
                case 2: return new CertLook(Color.Lerp(yellowed, slip, 0.75f), UITheme.Night[3], 2f, true, true, UITheme.Amber[2], 0.18f, false, true, 1f, 0.35f, 0);
                // 2: the slip's own clean white; the trim brightens to the accent proper.
                case 3: return new CertLook(slip, UITheme.Night[3], 2f, true, true, UITheme.Amber[3], 0.22f, false, true, 1f, 0.1f, 1);
                // 3 stars: a gilt band just inside the edge, a chain of diamonds along it.
                case 4: return new CertLook(Color.Lerp(slip, ivory, 0.5f), UITheme.Night[2], 3f, true, true, UITheme.Amber[3], 0.26f, true, true, 1.1f, 0f, 1);
                // 4 and 5 stars: ivory, gilt, a heavy rule, the chain with rosettes at its corners.
                default: return new CertLook(ivory, UITheme.Amber[1], 3f, true, true, UITheme.Amber[4], 0.30f, true, true, 1.2f, 0f, 2);
            }
        }

        /// <summary>The sheet's face: the heaviest the game ships, which is the shop's.</summary>
        private Font CertFace => _shop != null ? _shop : _display;

        /// <summary>
        /// WIDE-TRACKED CAPS (2026-09-22). A certificate's name is set with air between the letters; uGUI's Text
        /// has no tracking, so the air is put in as thin spaces. Latin only - a Chinese or Japanese line is already
        /// spaced by its own square grid, and pulling it apart would read as broken rather than as engraved.
        /// </summary>
        private static string Tracked(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            foreach (var ch in line) if (ch > '\u024F') return line;
            var sb = new System.Text.StringBuilder(line.Length * 2);
            for (int i = 0; i < line.Length; i++)
            {
                sb.Append(line[i]);
                if (i < line.Length - 1 && line[i] != ' ') sb.Append('\u2009');
            }
            return sb.ToString();
        }

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
            _ladderPaperImg = _ladderPaper.gameObject.AddComponent<Image>();
            _ladderPaperImg.color = UITheme.Cream[4];
            _ladderPaperImg.raycastTarget = false;
            // the grain: a speckle tile laid over the cream, faint
            var grain = NewRect("Grain", _ladderPaper);
            Place(grain, new Vector2(0.5f, 1f), new Vector2(PaperW - 40f, 640f - 40f), new Vector2(0f, -20f));
            grain.pivot = new Vector2(0.5f, 1f);
            var gi = grain.gameObject.AddComponent<Image>();
            // ONE SHEET, NOT A TILE (2026-09-22). Tiled over the paper this painted BLOTCHES - measured on three
            // captures: with the grain on, more than half the sheet came back a shade darker in hard-edged blocks
            // a couple of hundred units wide; with it off the paper was flat. A code-made sprite with no border,
            // tiled across a thousand units, is not a road this file needs to walk: the flecks are scattered over
            // one sheet-sized drawing instead, exactly as the lattice of glasses is, and shown at the house's 2x.
            gi.sprite = PaperGrain(PatternW, PatternH); gi.type = Image.Type.Simple;
            gi.color = new Color(1f, 1f, 1f, 0.10f); gi.raycastTarget = false;
            UiAuditExempt.Mark(grain, "paper grain, a speckle tile at 2x under a tenth of alpha");
            // the watermark: the house's star, large and almost not there, behind the writing
            // THE PATTERN (2026-09-21, the author: "kağıdın arkasında bir desen olsun, kağıdın rengine yakın kokteyl bardağı
            // iconu desenleri"): a lattice of small martini glasses drawn in code, tiled at 2x inside the rules, a step
            // darker than the paper and a third opaque - a watermark you read as texture, not as a picture.
            // One sheet-sized drawing shown at 2x rather than a tiled one: a Tiled Image of a code-made sprite drew
            // a single tile here (measured, r83: 56 lit pixels where 900 were due), so the lattice is laid over the
            // whole sheet once and stretched to it.
            var pattern = NewRect("Pattern", _ladderPaper);
            Place(pattern, new Vector2(0.5f, 1f), new Vector2(PaperW - 40f, 640f - 40f), new Vector2(0f, -20f));
            pattern.pivot = new Vector2(0.5f, 1f);
            var pi = pattern.gameObject.AddComponent<Image>();
            pi.sprite = PaperPattern(PatternW, PatternH); pi.type = Image.Type.Simple;
            pi.color = new Color(UITheme.Cream[2].r, UITheme.Cream[2].g, UITheme.Cream[2].b, 0.22f); pi.raycastTarget = false;
            _ladderPatternImg = pi;
            UiAuditExempt.Mark(pattern, "the paper's pattern: cocktail glasses at 2x, near the paper's own colour");
            // THE WEAR (seventh list): foxing, a glass ring, two folds and a browned edge, one sheet-sized drawing at
            // the house's 2x, shown as strong as the grade says the stock is handled (CertLook.Wear).
            var wear = NewRect("Wear", _ladderPaper);
            Stretch(wear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _ladderWearImg = wear.gameObject.AddComponent<Image>();
            _ladderWearImg.sprite = CertWear(PatternW, PatternH);
            _ladderWearImg.type = Image.Type.Simple;
            _ladderWearImg.raycastTarget = false;
            UiAuditExempt.Mark(wear, "the certificate's wear: foxing and folds drawn once at 2x, faded by the grade");
            _ladderOrnament = NewRect("Ornament", _ladderPaper);
            Stretch(_ladderOrnament, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // the rules: two deep outside, one inside, and a bracket at each corner between them
            // The rules, the gilt band and the corner brackets are all built once and RESTYLED by the rung (Regrade).
            _ladderRuleOuter = NewRect("RuleOuter", _ladderPaper);
            Stretch(_ladderRuleOuter, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            _ladderRuleOuterEdges = FrameEdges(_ladderRuleOuter, 2f, UITheme.Night[3]);
            _ladderGilt = NewRect("Gilt", _ladderPaper);
            Stretch(_ladderGilt, Vector2.zero, Vector2.one, new Vector2(15f, 15f), new Vector2(-15f, -15f));
            _ladderGiltEdges = FrameEdges(_ladderGilt, 1f, UITheme.Amber[3]);
            _ladderRuleInner = NewRect("RuleInner", _ladderPaper);
            Stretch(_ladderRuleInner, Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -18f));
            _ladderRuleInnerEdges = FrameEdges(_ladderRuleInner, 1f, new Color(UITheme.Night[3].r, UITheme.Night[3].g, UITheme.Night[3].b, 0.55f));
            _ladderBrackets = new List<Image>();
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
                Bracket(_ladderPaper, corner);

            // THE HIERARCHY (2026-09-21, the author: "hangisi başlık hangisi alt başlık hangisi metin anlaşılmıyor"): an
            // eyebrow in the small face, muted; the TITLE at 32 in the display face, doubled a step to the right for
            // weight, over its shadow, on an amber rule; the meta line quiet; the scores' words in the accent and their
            // figures at 24; the bands' heads in the accent too. Four sizes, two faces, three inks - nothing else.
            // THE TYPE OF A DOCUMENT, NOT OF AN ARCADE (2026-09-22, the author: "sertifikada kullanılan font
            // seçimini iyi araştır, oyunlarda bu tarz belge gibi gözüken sahnelerde nasıl fontlar kullanılıyor").
            // What a certificate is set in, everywhere it is set: a HEAVY roman for the name, wide-tracked CAPS for
            // the line above it, a light roman for the small print, and rules that the type sits on. A pixel game
            // has no blackletter to reach for, so the three levers that survive the grid are used instead - the
            // heaviest face the game ships (Silkscreen Bold, the shop's), CAPITALS, and TRACKING, which is the one
            // thing that makes a pixel line read as engraved rather than as a label. The arcade face (MalibuArcade)
            // stays on the arcade: it is a game's voice, and this sheet is the bar's.
            _ladderHeading = NewText("Heading", _ladderPaper, _body, 16, TextAnchor.MiddleCenter, UITheme.Night[3]);
            Place(_ladderHeading.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 20f), new Vector2(0f, -24f));
            _ladderHeading.horizontalOverflow = HorizontalWrapMode.Overflow;

            // The title the bar is hereby known as: the display face's large size, its shadow two units under
            // and right so it stands off the paper, and the accent's rule under it.
            _ladderToShadow = NewText("ToShadow", _ladderPaper, CertFace, 32, TextAnchor.UpperCenter, UITheme.Night[3]);
            Place(_ladderToShadow.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 40f), new Vector2(3f, -47f));
            _ladderToShadow.rectTransform.pivot = new Vector2(0.5f, 1f);
            _ladderToShadow.horizontalOverflow = HorizontalWrapMode.Overflow;
            _ladderToBold = NewText("ToBold", _ladderPaper, CertFace, 32, TextAnchor.UpperCenter, UITheme.Night[0]);
            Place(_ladderToBold.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 40f), new Vector2(2f, -44f));
            _ladderToBold.rectTransform.pivot = new Vector2(0.5f, 1f);
            _ladderToBold.horizontalOverflow = HorizontalWrapMode.Overflow;
            _ladderTo = NewText("To", _ladderPaper, CertFace, 32, TextAnchor.UpperCenter, UITheme.Night[0]);
            Place(_ladderTo.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 40f), new Vector2(0f, -44f));
            _ladderTo.rectTransform.pivot = new Vector2(0.5f, 1f);
            _ladderTo.horizontalOverflow = HorizontalWrapMode.Overflow;
            _ladderUnderline = NewRect("Underline", _ladderPaper);
            Place(_ladderUnderline, new Vector2(0.5f, 1f), new Vector2(320f, 3f), new Vector2(0f, -92f));
            _ladderUnderlineImg = _ladderUnderline.gameObject.AddComponent<Image>();
            _ladderUnderlineImg.color = UITheme.Amber[3]; _ladderUnderlineImg.raycastTarget = false;

            _ladderToStars = LiveStarRow(_ladderPaper, new Vector2(0.5f, 1f), new Vector2(0f, -112f), LadderStar, 4f,
                UITheme.Amber[3], UITheme.Night[3]);
            // THE HEADLINE ON A DARK CARD (seventh list: "comfort service ve yıldızımızın arkasında karartılmış kart
            // olmalı onun üstünde yazanlar daha çok dikkat çekecektir"): the stars, the line under them and the two
            // scores stand on one card of the night ink, so the gold and the marks come off the paper at the eye.
            _ladderHeadCard = NewRect("HeadCard", _ladderPaper);
            Place(_ladderHeadCard, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide - 40f, 120f), new Vector2(0f, -100f));
            _ladderHeadCard.pivot = new Vector2(0.5f, 1f);
            var hcImg = _ladderHeadCard.gameObject.AddComponent<Image>();
            hcImg.sprite = ChromeArt.Card();
            hcImg.type = Image.Type.Sliced;
            hcImg.color = UITheme.Night[0];   // the night ink itself: grey-plum at 94% read as mud over the cream
            hcImg.raycastTarget = false;
            var starsRow = _ladderPaper.Find("LiveStars");   // built before it: the card goes UNDER the stars
            if (starsRow != null) _ladderHeadCard.SetSiblingIndex(starsRow.GetSiblingIndex());
            _ladderMeta = NewText("Meta", _ladderPaper, _body, 16, TextAnchor.MiddleCenter, UITheme.Cream[3]);
            Place(_ladderMeta.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 20f), new Vector2(0f, -148f));
            _ladderMeta.horizontalOverflow = HorizontalWrapMode.Overflow;

            // COMFORT in the house's medals and SERVICE in its hearts, the way the top bar wears them.
            _ladderMedals = ScoreGroup("Comfort", -240f, -170f, "rank.cert.comfort", ItemArt.Medal);
            _ladderHearts = ScoreGroup("Service", 240f, -170f, "rank.cert.service", ItemArt.Heart);

            var cut = NewRect("Cut", _ladderPaper);
            Place(cut, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 1f), new Vector2(0f, -240f));
            var ci = cut.gameObject.AddComponent<Image>();
            ci.color = UITheme.Night[3]; ci.raycastTarget = false;

            _ladderBandHead = BandHead("BandHead", -232f);
            // TWO BANDS, TWO FRAMES (2026-09-22, the author: "sıradaki ürünlerin bulunduğu bölümün etrafında
            // çizgili çerçeve olsun, açılanların ise etrafında çizgisiz çerçeve olsun"): what the bar HAS is ruled
            // in a solid line, what is still to come is ruled in a dashed one - the oldest way a document says
            // "issued" and "pending" without a word.
            _ladderBandBox = NewRect("BandBox", _ladderPaper);
            _ladderBandBox.pivot = new Vector2(0.5f, 1f);
            _ladderBandBoxEdges = FrameEdges(_ladderBandBox, 1f, UITheme.Night[3]);
            _ladderBand = NewRect("Tiles", _ladderPaper);
            Place(_ladderBand, new Vector2(0.5f, 1f), new Vector2(PaperW, TileRowH), new Vector2(0f, -TilesTop));
            _ladderBand.pivot = new Vector2(0.5f, 1f);
            _ladderNextHead = BandHead("NextHead", -400f);
            _ladderNextBox = NewRect("NextBox", _ladderPaper);
            _ladderNextBox.pivot = new Vector2(0.5f, 1f);
            _ladderNextBoxEdges = DashedFrame(_ladderNextBox, UITheme.Night[3]);
            _ladderNextBand = NewRect("NextTiles", _ladderPaper);
            Place(_ladderNextBand, new Vector2(0.5f, 1f), new Vector2(PaperW, TileRowH), new Vector2(0f, -420f));
            _ladderNextBand.pivot = new Vector2(0.5f, 1f);

            // THE SEAL, in the sheet's bottom-right corner: two ribbon tails first (they hang from behind it,
            // out past the sheet's edge), then the disc with the cocktail glass in it. All in the one accent.
            // OUTSIDE THE ROLL (2026-09-22, the author: "kurdeleler ... sertifikanın dışına taşabilir, şu an
            // uçları kesilmiş gibi duruyor"): the seal and its tails hang off a host under the GROUP, not under the
            // paper - the paper lives inside the reveal's mask, which was cutting the tails off square at the
            // sheet's bottom edge. ShowLadder hangs this host at the paper's foot.
            _ladderSealHost = NewRect("SealHost", _ladderGroup);
            _ladderSealHost.anchorMin = _ladderSealHost.anchorMax = new Vector2(0.5f, 1f);
            _ladderSealHost.pivot = new Vector2(0.5f, 1f);
            _ladderSealHost.sizeDelta = new Vector2(PaperW, 1f);
            _ladderRibbonL = Ribbon("RibbonL", -14f, UITheme.Amber[1], -9f, RibbonLen);
            _ladderRibbonR = Ribbon("RibbonR", 12f, UITheme.Amber[2], 7f, RibbonLen * 1.18f);
            // THE SEAL, DRAWN (2026-09-21, the author: "Mühürü daha büyük ve pixellabden üret"): Items/cert_seal is
            // PixelLab's amber wax with the martini pressed into it, at 128; without it the disc and the glass stand in.
            _ladderSeal = NewRect("Seal", _ladderSealHost);
            _ladderSeal.anchorMin = _ladderSeal.anchorMax = new Vector2(1f, 0f);
            _ladderSeal.pivot = new Vector2(0.5f, 0.5f);
            _ladderSeal.sizeDelta = new Vector2(SealPx, SealPx);
            _ladderSeal.anchoredPosition = new Vector2(-PaperSide - 52f, 84f);
            var disc = _ladderSeal.gameObject.AddComponent<Image>();
            var drawnSeal = ItemArt.Load("cert_seal");
            disc.sprite = drawnSeal ?? SealDisc((int)SealPx); disc.color = Color.white; disc.raycastTarget = false;
            disc.preserveAspect = true;
            var glassRt = NewRect("Glass", _ladderSeal);
            Place(glassRt, new Vector2(0.5f, 0.5f), new Vector2(56f, 68f), new Vector2(0f, 2f));
            var glass = glassRt.gameObject.AddComponent<Image>();
            glass.sprite = ItemArt.Load("glass3d_martini_t2_Front");
            glass.color = UITheme.Cream[4]; glass.preserveAspect = true; glass.raycastTarget = false;
            glass.enabled = drawnSeal == null && glass.sprite != null;
        }

        /// <summary>A band's head: sixteen-pixel writing, left-aligned at the sheet's side.</summary>
        private Text BandHead(string id, float y)
        {
            var t = NewText(id, _ladderPaper, CertFace, 16, TextAnchor.MiddleLeft, UITheme.Amber[2]);
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
            _ladderBrackets.Add(hi);
            var v = NewRect("BracketV", paper);
            v.anchorMin = v.anchorMax = corner; v.pivot = corner;
            v.sizeDelta = new Vector2(stroke, arm);
            v.anchoredPosition = new Vector2(sx * inset, sy * inset);
            var vi = v.gameObject.AddComponent<Image>(); vi.color = UITheme.Night[2]; vi.raycastTarget = false;
            _ladderBrackets.Add(vi);
        }

        /// <summary>A score on the sheet: its label, five of the house's icons filled to the value, the number.</summary>
        private Image[] ScoreGroup(string id, float x, float y, string labelKey, Func<bool, float, Sprite> art)
        {
            // THE ROW SITS UNDER ITS WORD, AND THERE IS NO FIGURE (2026-09-22, the author: "comfort yazısı ve service
            // yazısının altında puanlarını gösteren barı oraya taşı ... puan ibresi 5.0 veya 0.0 olarak gösterilmesin
            // hiçbir zaman, kaç yıldız ise o kadar yıldız görseli ile"). A certificate does not print a score out of
            // five; it shows five marks and fills as many as were earned. The word, and the marks under it.
            const float px = 24f, gap = 4f, labelW = 220f;
            float rowW = BarRating.MaxStars * (px + gap) - gap;
            var label = NewText(id + "Label", _ladderPaper, CertFace, 16, TextAnchor.MiddleCenter, UITheme.Amber[2]);
            if (id == "Comfort") _ladderComfortLabel = label; else _ladderServiceLabel = label;
            Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(labelW, 20f), new Vector2(x, y));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = Tracked(UIText.T(labelKey));
            var row = NewRect(id + "Row", _ladderPaper);
            Place(row, new Vector2(0.5f, 1f), new Vector2(rowW, px), new Vector2(x, y - 26f));
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
            return fills;
        }

        private static void SetRowFill(Image[] fills, double value)
        {
            if (fills == null) return;
            for (int i = 0; i < fills.Length; i++)
                if (fills[i] != null) fills[i].fillAmount = Mathf.Clamp01((float)(value - i));
        }

        /// <summary>A ribbon tail: hangs from the seal's centre, pivot at its top, offset <paramref name="dx"/>.</summary>
        private RectTransform Ribbon(string id, float dx, Color ink, float lean, float len)
        {
            var rt = NewRect(id, _ladderSealHost);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            // LONGER, AND CUT TO A POINT (2026-09-22, the author: "kurdelenin ipleri uzamalı ve > şeklinde olmalı"):
            // the two tails fall a hundred and eighty units off the seal and each ends in the swallowtail a ribbon
            // is actually cut to - a notch taken out of the end, which is the ">" read the right way up.
            rt.sizeDelta = new Vector2(24f, len);
            rt.anchoredPosition = new Vector2(-PaperSide - 52f + dx, 84f);
            // A RIBBON DOES NOT HANG PLUMB (2026-09-22): each tail leans its own way off the wax, and the two are
            // different lengths, which is what stops them reading as two drawn rectangles.
            rt.localRotation = Quaternion.Euler(0f, 0f, lean);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RibbonArt(12, 90);
            img.type = Image.Type.Simple;
            img.color = ink; img.raycastTarget = false;
            return rt;
        }

        /// <summary>How far the seal's tails fall.</summary>
        private const float RibbonLen = 128f;

        private static Sprite s_sealDisc, s_paperGrain, s_paperPattern, s_ribbon, s_dash, s_dashV;

        /// <summary>The dash a pending band is ruled in: four on, three off, drawn at half and tiled at the house's
        /// 2x. Two cuts, one lying down and one standing - a rotated Image keeps its RECT, so the upright edges of a
        /// frame have to be drawn upright rather than turned (measured: a turned edge threw its dashes clear across
        /// the screen, r102).</summary>
        private static Sprite DashArt(bool upright)
        {
            if (upright && s_dashV != null) return s_dashV;
            if (!upright && s_dash != null) return s_dash;
            int w = upright ? 1 : 7, h = upright ? 7 : 1;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "cert_dash", wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int along = upright ? y : x;
                    tex.SetPixel(x, y, along < 4 ? Color.white : new Color(0f, 0f, 0f, 0f));
                }
            tex.Apply(false, false);
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            sp.name = upright ? "cert_dash_v" : "cert_dash";
            if (upright) s_dashV = sp; else s_dash = sp;
            return sp;
        }

        /// <summary>Four dashed edges round a rect, handed back so the grade can retint them.</summary>
        private Image[] DashedFrame(RectTransform parent, Color c)
        {
            var edges = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var rt = NewRect("Dash", parent);
                bool horizontal = i < 2;
                rt.anchorMin = horizontal ? new Vector2(0, i) : new Vector2(i - 2, 0);
                rt.anchorMax = horizontal ? new Vector2(1, i) : new Vector2(i - 2, 1);
                rt.pivot = new Vector2(horizontal ? 0.5f : i - 2, horizontal ? i : 0.5f);
                rt.sizeDelta = horizontal ? new Vector2(0, 2f) : new Vector2(2f, 0);
                rt.anchoredPosition = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = DashArt(!horizontal);
                img.type = Image.Type.Tiled;
                img.pixelsPerUnitMultiplier = 0.5f;
                img.color = c;
                img.raycastTarget = false;
                edges[i] = img;
            }
            return edges;
        }

        /// <summary>A ribbon tail: a straight band with a V notched out of its end, drawn white and worn in the
        /// colour the caller gives it. Drawn at half and shown at the house's 2x, like every other code sprite here.</summary>
        private static Sprite RibbonArt(int w, int h)
        {
            if (s_ribbon != null) return s_ribbon;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "cert_ribbon" };
            int notch = Mathf.Max(3, w / 2);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // the notch is cut out of the BOTTOM rows, deepest in the middle
                    // texture y grows up, so row 0 is the tail's end: the notch is deepest in the middle and
                    // nothing at the two corners, which is the V a ribbon is cut to.
                    float half = Mathf.Max(0.001f, (w - 1) * 0.5f);
                    int cut = Mathf.RoundToInt(notch * (1f - Mathf.Abs(x - half) / half));
                    bool inside = y >= cut;
                    tex.SetPixel(x, y, inside ? Color.white : new Color(0f, 0f, 0f, 0f));
                }
            tex.Apply(false, false);
            s_ribbon = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 1f, 0, SpriteMeshType.FullRect);
            s_ribbon.name = "cert_ribbon";
            return s_ribbon;
        }

        /// <summary>A lattice of martini glasses, two to a tile on the diagonal, drawn in one-pixel lines; white,
        /// coloured by the Image that wears it. Tiled at 2x it reads as the paper's own printing.</summary>
        private static Sprite PaperPattern(int w, int h)
        {
            if (s_paperPattern != null) return s_paperPattern;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "paper_pattern", wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) tex.SetPixel(x, y, clear);
            void Dot(int x, int y) { if (x >= 0 && y >= 0 && x < w && y < h) tex.SetPixel(x, y, Color.white); }
            void Glass(int cx, int cy)
            {
                for (int i = 0; i <= 6; i++) { Dot(cx - 6 + i, cy + 6 - i); Dot(cx + 6 - i, cy + 6 - i); }   // the bowl's two sides
                for (int i = -6; i <= 6; i++) Dot(cx + i, cy + 7);                                             // the rim
                for (int i = 1; i <= 5; i++) Dot(cx, cy - i);                                                  // the stem
                for (int i = -3; i <= 3; i++) Dot(cx + i, cy - 6);                                             // the foot
            }
            const int pitch = 48;   // the lattice: a glass every 48, the next row half a step over
            for (int row = 0, cy = 12; cy < h; row++, cy += pitch / 2)
                for (int cx = (row % 2 == 0) ? 12 : 12 + pitch / 2; cx < w; cx += pitch)
                    Glass(cx, cy);
            tex.Apply(false, false);
            s_paperPattern = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            s_paperPattern.name = "paper_pattern";
            return s_paperPattern;
        }

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
        /// <summary>The paper's own drawings are one sheet at half the sheet's size, shown at 2x.</summary>
        private const int PatternW = 492, PatternH = 300;

        private Image _ladderWearImg;
        private RectTransform _ladderOrnament;
        private RectTransform _ladderHeadCard;
        private Color _ladderTrim = UITheme.Amber[3], _ladderGiltTrim = UITheme.Amber[3];
        private int _ladderOrnamentKind = -1;
        private float _ladderOrnamentH = -1f;

        /// <summary>
        /// A HANDLED SHEET (2026-09-22): what a certificate that has lived in a drawer carries - brown foxing
        /// specks gathered toward the edges, the dry ring a glass left on it, two soft fold lines, and the edge
        /// itself browned a few texels in. Transparent everywhere else; one drawing, never tiled (a tiled code
        /// sprite painted blotches on this very sheet once).
        /// </summary>
        private static Sprite CertWear(int w, int h)
        {
            const string Key = "cert:wear";
            if (s_certWear != null) return s_certWear;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = Key };
            var px = new Color32[w * h];
            var brown = new Color32(120, 82, 38, 0);
            void Put(int x, int y, byte a)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                var c = px[y * w + x];
                if (c.a >= a) return;
                px[y * w + x] = new Color32(brown.r, brown.g, brown.b, a);
            }
            // A HASHED SEQUENCE, not a random stream: the sheet is one fixed drawing (and System.Random stays out of
            // this codebase by rule, even where nothing is decided by it).
            int seq = 0;
            float R() { seq++; float v = Mathf.Sin(seq * 12.9898f + 78.233f) * 43758.5453f; return v - Mathf.Floor(v); }
            // the browned edge: strongest at the very edge, gone four texels in, broken so it is not a frame
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    if (d > 4) continue;
                    if (R() < 0.35) continue;
                    Put(x, y, (byte)(70 - d * 14));
                }
            // foxing: specks, thicker toward the edges
            for (int i = 0; i < 420; i++)
            {
                float fx = (float)R(), fy = (float)R();
                float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
                if (R() < edge * 3.2f) continue;
                int x = (int)(fx * w), y = (int)(fy * h);
                byte a = (byte)(40 + (int)(R() * 50f));
                Put(x, y, a);
                if (R() < 0.4) Put(x + 1, y, (byte)(a * 0.6f));
                if (R() < 0.3) Put(x, y + 1, (byte)(a * 0.5f));
            }
            // a dry glass ring, low on the right, broken
            float rcx = w * 0.78f, rcy = h * 0.22f, rr = w * 0.07f;
            for (float t = 0f; t < 6.2832f; t += 0.01f)
            {
                if (R() < 0.25) continue;
                int x = Mathf.RoundToInt(rcx + Mathf.Cos(t) * rr), y = Mathf.RoundToInt(rcy + Mathf.Sin(t) * rr * 0.92f);
                Put(x, y, (byte)(45 + (int)(R() * 25f)));
                if (Mathf.Sin(t) < -0.3f) Put(x, y - 1, 30);
            }
            // two folds: a soft darker line with a lit one beside it, across the middle each way
            for (int x = 0; x < w; x++) { if (R() < 0.1) continue; Put(x, h / 2, 34); }
            for (int y = 0; y < h; y++) { if (R() < 0.1) continue; Put(w / 3, y, 26); }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            s_certWear = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            return s_certWear;
        }
        private static Sprite s_certWear;

        /// <summary>
        /// THE WORKED BORDER (seventh list: "kenarlıklar biraz daha işlemeli olsun düz köşe işlemesi olmasın"): a chain
        /// of diamonds with a dot between each pair laid along the gilt line, and at the top grades a rosette at each
        /// corner - small drawn pieces placed along the sheet's real edge, so the chain keeps its pitch at any height.
        /// Rebuilt only when the grade or the sheet's height changes.
        /// </summary>
        private void LayOrnament(int kind, Color trim)
        {
            if (_ladderOrnament == null) return;
            if (kind == _ladderOrnamentKind && Mathf.Approximately(_ladderOrnamentH, _ladderPaperH))
            {
                foreach (var img in _ladderOrnament.GetComponentsInChildren<Image>(true)) img.color = new Color(trim.r, trim.g, trim.b, img.color.a);
                return;
            }
            _ladderOrnamentKind = kind; _ladderOrnamentH = _ladderPaperH;
            foreach (Transform old in _ladderOrnament) Destroy(old.gameObject);
            if (kind <= 0) return;
            const float Inset = 22f, Pitch = 18f;
            float w = PaperW - Inset * 2f, h = _ladderPaperH - Inset * 2f;
            void Piece(float x, float y, float size, float rot, float alpha)
            {
                var p = NewRect("O", _ladderOrnament);
                p.anchorMin = p.anchorMax = new Vector2(0f, 0f);
                p.pivot = new Vector2(0.5f, 0.5f);
                p.sizeDelta = new Vector2(size, size);
                p.anchoredPosition = new Vector2(Inset + x, Inset + y);
                p.localRotation = Quaternion.Euler(0f, 0f, rot);
                var im = p.gameObject.AddComponent<Image>();
                im.sprite = ChromeArt.Solid();
                im.color = new Color(trim.r, trim.g, trim.b, alpha);
                im.raycastTarget = false;
            }
            void Chain(Vector2 a, Vector2 b)
            {
                float len = Vector2.Distance(a, b);
                int n = Mathf.Max(1, Mathf.FloorToInt(len / Pitch));
                for (int i = 1; i < n; i++)
                {
                    var at = Vector2.Lerp(a, b, i / (float)n);
                    if ((i & 1) == 0) Piece(at.x, at.y, 5f, 45f, 0.9f);   // a diamond
                    else Piece(at.x, at.y, 2f, 0f, 0.75f);                 // the dot between
                }
            }
            Chain(new Vector2(0f, 0f), new Vector2(w, 0f));
            Chain(new Vector2(0f, h), new Vector2(w, h));
            Chain(new Vector2(0f, 0f), new Vector2(0f, h));
            Chain(new Vector2(w, 0f), new Vector2(w, h));
            foreach (var c in new[] { new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(0f, h), new Vector2(w, h) })
            {
                Piece(c.x, c.y, 9f, 45f, 1f);
                if (kind >= 2)
                {
                    // the rosette: a ring of four small diamonds round a larger one, and a dot at its heart
                    for (int k = 0; k < 4; k++)
                    {
                        float ang = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                        Piece(c.x + Mathf.Cos(ang) * 10f, c.y + Mathf.Sin(ang) * 10f, 5f, 45f, 0.95f);
                    }
                    Piece(c.x, c.y, 13f, 45f, 0.35f);
                    Piece(c.x, c.y, 3f, 0f, 1f);
                }
            }
        }

        /// <summary>A sprite with its transparent margin cut away, so a picture centred in a tile is centred by
        /// what is DRAWN, not by the box the artist left round it (seventh list: "görselleri kutuların tam ortasına
        /// denk getirilsin"). Only readable textures can be measured; anything else is returned as it came.</summary>
        private static Sprite Trimmed(Sprite s)
        {
            if (s == null) return null;
            if (s_trimmed.TryGetValue(s, out var got)) return got;
            var tex = s.texture;
            Sprite result = s;
            if (tex != null && tex.isReadable)
            {
                var r = s.rect;
                int x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height;
                var px = tex.GetPixels32();
                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[(y0 + y) * tex.width + x0 + x].a > 8)
                        { if (x < minX) minX = x; if (y < minY) minY = y; if (x > maxX) maxX = x; if (y > maxY) maxY = y; }
                if (maxX >= minX && (minX > 0 || minY > 0 || maxX < w - 1 || maxY < h - 1))
                    result = Sprite.Create(tex, new Rect(x0 + minX, y0 + minY, maxX - minX + 1, maxY - minY + 1),
                                           new Vector2(0.5f, 0.5f), s.pixelsPerUnit);
            }
            s_trimmed[s] = result;
            return result;
        }
        private static readonly Dictionary<Sprite, Sprite> s_trimmed = new Dictionary<Sprite, Sprite>();

        private static Sprite PaperGrain(int w, int h)
        {
            if (s_paperGrain != null) return s_paperGrain;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "paper_grain", wrapMode = TextureWrapMode.Clamp };
            uint seed = 0x2545F491u;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                    uint r = seed & 0xFF;
                    Color p = new Color(0f, 0f, 0f, 0f);
                    if (r < 22) p = new Color(1f, 1f, 1f, 1f);            // a light fleck
                    else if (r < 40) p = new Color(0.25f, 0.2f, 0.16f, 1f); // a dark one
                    tex.SetPixel(x, y, p);
                }
            tex.Apply(false, false);
            s_paperGrain = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
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
            _ladderHeading.text = Tracked(UIText.T(climb ? "rank.window.title" : "rank.window.title_again"));
            _ladderTo.text = _ladderToShadow.text = _ladderToBold.text = Tracked(UIText.T(now.Title));
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


            // This rung's band, then the next rung's, dimmed, so every certificate says what comes next — the
            // author: "her sertifikada sıradaki açılacaklar gözükmeli, tarifler dahil".
            Regrade(now.Index);
            _ladderTiles.Clear();
            _ladderBandHead.text = UIText.T("rank.window.new");
            var next = BarRank.Above(now);
            // The earned band takes the rows the sheet can spare after keeping one for what comes next - a cut slot
            // counts the rest - so the sheet, the key under it and the screen all agree (2026-09-22).
            int openMax = Mathf.Clamp(Mathf.FloorToInt(
                (PaperMaxH - TilesTop - PaperFoot - (next != null ? NextBandGap + TileRowH : 0f)) / TileRowH), 1, 2);
            int rows = LayTiles(_ladderBand, run, TilesFor(run, was, now, climb), moving, rowsMax: openMax, dim: false);
            float nextTop = TilesTop + rows * TileRowH + NextBandGap;
            int nextRows;
            if (next != null)
            {
                _ladderNextHead.text = UIText.T("rank.window.next", ("stars", next.Stars.ToString("0.0")));
                // THE SHEET MAY GROW DOWN, BUT NOT OFF THE SCREEN (2026-09-22). The author's rule is that the
                // certificate keeps its width and takes its height from what is on it - and once the room's
                // fittings joined the tiles, both bands ran to two rows and the paper reached 780 on a 720
                // screen: the title was above the top edge and the seal below the bottom one. The open band
                // keeps its two rows, because what was just earned is the point of the page; the NEXT band takes
                // whatever rows are left under PaperMaxH, and its own cut slot says how many it stood for.
                int nextMax = Mathf.Clamp(
                    Mathf.FloorToInt((PaperMaxH - nextTop - PaperFoot) / TileRowH), 1, 2);
                nextRows = LayTiles(_ladderNextBand, run, TilesFor(run, now, next, true), moving, rowsMax: nextMax, dim: true);
            }
            else
            {
                _ladderNextHead.text = UIText.T("rank.window.top");
                foreach (Transform old in _ladderNextBand) Destroy(old.gameObject);
                nextRows = 0;
            }
            _ladderNextHead.rectTransform.anchoredPosition = new Vector2(PaperSide, -(nextTop - 40f));
            _ladderNextBand.anchoredPosition = new Vector2(0f, -nextTop);
            // the two boxes: each one drawn round its band's tiles, a hand's width out
            BoxBand(_ladderBandBox, TilesTop, rows);
            BoxBand(_ladderNextBox, nextTop, nextRows);
            _ladderNextBox.gameObject.SetActive(next != null && nextRows > 0);

            // The sheet is as tall as its bands, and never shorter than 16:9; the group holds it and the key.
            _ladderPaperH = Mathf.Max(PaperMinH, nextTop + nextRows * TileRowH + (nextRows == 0 ? 8f : 0f) + PaperFoot);
            _ladderPaper.sizeDelta = new Vector2(PaperW, _ladderPaperH);
            LayOrnament(_ladderOrnamentGrade, _ladderGiltTrim);
            if (_ladderSealHost != null) _ladderSealHost.anchoredPosition = new Vector2(0f, -_ladderPaperH);
            _ladderReveal.sizeDelta = new Vector2(PaperW, moving ? 0f : _ladderPaperH);
            _ladderGroup.sizeDelta = new Vector2(PaperW, _ladderPaperH + KeyGap + PauseKeyH);
            _ladderGroup.anchoredPosition = Vector2.zero;
            _ladderGroup.localScale = moving ? new Vector3(0.94f, 0.94f, 1f) : Vector3.one;

            float sealScale = moving ? 0f : _ladderSealGrade;
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
            // THE CLOCK STOPS (2026-09-21, the author: "bu ekran açıkken menüde olduğu gibi zaman durmalı"): the
            // engine's clock goes to zero as it does for the menu, while the night is running and nothing else holds
            // it; the sheet's own motion runs on unscaled time. Let go in CloseLadder.
            if (run.Phase == TycoonPhase.DayOpen && !Paused) { SetPaused(true); _ladderHeldClock = true; }
            _ladderPanel.gameObject.SetActive(true);
            Sfx.Play(climb ? "level_up" : "menu_open", climb ? 0.9f : 0.7f);
        }

        /// <summary>Dresses the sheet to the rung's own grade (see <see cref="CertLook"/>).</summary>
        private void Regrade(int rung)
        {
            var look = LookFor(rung);
            if (_ladderPaperImg != null) _ladderPaperImg.color = look.Paper;
            if (_ladderPatternImg != null)
                _ladderPatternImg.color = new Color(UITheme.Cream[2].r, UITheme.Cream[2].g, UITheme.Cream[2].b, look.Pattern);
            foreach (var e in _ladderRuleOuterEdges)
                if (e != null)
                {
                    e.color = look.Rule;
                    // A Frame edge stretches along one axis (sizeDelta 0 there) and carries the rule's weight on
                    // the other; the weight is the one to write.
                    var r = (RectTransform)e.transform;
                    var size = r.sizeDelta;
                    if (Mathf.Approximately(size.x, 0f)) r.sizeDelta = new Vector2(0f, look.RuleW);
                    else if (Mathf.Approximately(size.y, 0f)) r.sizeDelta = new Vector2(look.RuleW, 0f);
                }
            if (_ladderRuleInner != null) _ladderRuleInner.gameObject.SetActive(look.InnerRule);
            if (_ladderGilt != null) _ladderGilt.gameObject.SetActive(look.Gilt);
            if (_ladderBrackets != null)
                foreach (var b in _ladderBrackets)
                    if (b != null) { b.gameObject.SetActive(look.Brackets); b.color = look.Trim; }
            if (_ladderUnderlineImg != null) _ladderUnderlineImg.color = look.Trim;
            // TYPE IN THE TRIM, BUT NEVER PALER THAN THE PAPER CAN CARRY (2026-09-22): the top grades' accent is the
            // lightest amber, which is right for gilt and a line, and all but invisible as words on ivory.
            var inkTrim = look.Trim == UITheme.Amber[4] || look.Trim == UITheme.Amber[3] ? UITheme.Amber[1] : look.Trim;
            if (_ladderBandHead != null) _ladderBandHead.color = inkTrim;
            if (_ladderBandBoxEdges != null)
                foreach (var e2 in _ladderBandBoxEdges) if (e2 != null) e2.color = look.Rule;
            if (_ladderNextBoxEdges != null)
                foreach (var e2 in _ladderNextBoxEdges) if (e2 != null) e2.color = new Color(look.Rule.r, look.Rule.g, look.Rule.b, 0.75f);
            if (_ladderNextHead != null) _ladderNextHead.color = inkTrim;
            // On the dark card, so in the LIGHT accent at every grade: the grade's trim is a dark amber low down.
            if (_ladderComfortLabel != null) _ladderComfortLabel.color = UITheme.Amber[4];
            if (_ladderServiceLabel != null) _ladderServiceLabel.color = UITheme.Amber[4];
            if (_ladderSeal != null) _ladderSealGrade = look.Seal;
            if (_ladderWearImg != null) _ladderWearImg.color = new Color(1f, 1f, 1f, look.Wear);
            _ladderTrim = inkTrim;   // the group heads are words too
            _ladderGiltTrim = look.Trim;   // ...and the worked border is gilt, so it keeps the light accent
            _ladderOrnamentGrade = look.Ornament;
            if (_ladderRibbonL != null) _ladderRibbonL.gameObject.SetActive(look.Ribbons);
            if (_ladderRibbonR != null) _ladderRibbonR.gameObject.SetActive(look.Ribbons);
        }

        private int _ladderOrnamentGrade;

        /// <summary>The seal's size at this rung, the scale the climb's pop multiplies.</summary>
        private float _ladderSealGrade = 1f;

        /// <summary>Draws a band's frame round the rows it holds: the tiles start at <paramref name="top"/> and
        /// each row is TileRowH deep, with the class captions standing above the first one.</summary>
        private void BoxBand(RectTransform box, float top, int rows)
        {
            if (box == null) return;
            const float padX = 12f, padTop = 26f, padBottom = 8f;
            float h = rows * TileRowH + padTop + padBottom;
            box.sizeDelta = new Vector2(PaperW - 2f * PaperSide + padX * 2f, h);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
            box.anchoredPosition = new Vector2(0f, -(top - padTop));
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
                foreach (var f in run.FixturesOpeningAt(rung.Stars))
                    tiles.Add(new CertTile { Cls = "rank.cert.class.room",
                        Name = UIText.Caps(UIText.Data("fixture", f.Id, "name", f.Name)),
                        // THE SWATCH, when the fitting has one (2026-09-22): a wall's own drawing is the whole
                        // 640x360 room, and shrunk into a 64 px tile it reads as a tiny photograph of a bar.
                        // The market already solved this - it shows the swatch - so the sheet shows the same.
                        Art = FixtureArt(f.Swatch ?? f.Sprite) });
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
                float groupEnds = 0f;   // where the current group's HEAD ends: the next group may not start inside it
                foreach (var t in tiles)
                {
                    bool groupHead = t.Cls != lastClass;
                    // A GROUP IS AS WIDE AS ITS HEAD (2026-09-22): a one-tile group under a 16 px tracked head ran its
                    // words into the next group's (THE COUNTER / THE DOOR / THE ROOM, seen in play). The next group
                    // starts after whichever is wider, the tiles or the words over them.
                    if (groupHead && x > 0f) x = Mathf.Max(x, groupEnds) + TileGroupGap;
                    if (x + TileSize > rowW + 0.5f) { x = 0f; row++; y -= TileRowH; groupHead = true; }
                    if (groupHead) groupEnds = x + ClassHeadWidth(t.Cls) - (TilePitch - TileSize);
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
                    // A GROUP'S HEAD IS NOT A TILE'S NAME (2026-09-22, seventh list: "The Room, The Taps, The Market
                    // yazısının boyutu ve stili ürünlerin ismiyle aynı, böyle olmamalı, her grubun içerisinde net
                    // ayrılması gerekiyor"): the heavy face at twice the size, tracked, in the grade's accent, with its
                    // own rule under it - and a rule stood in the gap before it, so each group is fenced off.
                    var cap = NewText("Class", tile, CertFace, 16, TextAnchor.LowerLeft, _ladderTrim);
                    Place(cap.rectTransform, new Vector2(0f, 1f), new Vector2(260f, 18f), new Vector2(0f, 20f));
                    cap.rectTransform.pivot = new Vector2(0f, 1f);
                    cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                    cap.text = Tracked(UIText.T(tiles[i].Cls));
                    var capRule = NewRect("ClassRule", tile);
                    Place(capRule, new Vector2(0f, 1f), new Vector2(Mathf.Min(cap.preferredWidth, 240f), 2f), new Vector2(0f, 3f));
                    capRule.pivot = new Vector2(0f, 1f);
                    var cri = capRule.gameObject.AddComponent<Image>();
                    cri.color = new Color(_ladderTrim.r, _ladderTrim.g, _ladderTrim.b, 0.7f);
                    cri.raycastTarget = false;
                    if (x > left + 0.5f)
                    {
                        var fence = NewRect("GroupFence", band);
                        Place(fence, new Vector2(0f, 1f), new Vector2(2f, TileSize + 26f),
                              new Vector2(x - (TileGroupGap + (TilePitch - TileSize)) * 0.5f - 1f, y - 2f));
                        fence.pivot = new Vector2(0f, 1f);
                        var fi = fence.gameObject.AddComponent<Image>();
                        fi.color = new Color(_ladderTrim.r, _ladderTrim.g, _ladderTrim.b, 0.45f);
                        fi.raycastTarget = false;
                    }
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

        /// <summary>How wide a group's head prints, measured once per class on a hidden line.</summary>
        private float ClassHeadWidth(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return 0f;
            if (_classHeadW.TryGetValue(cls, out var w)) return w;
            if (_classMeasure == null)
            {
                _classMeasure = NewText("Measure", _ladderPaper, CertFace, 16, TextAnchor.MiddleLeft, Color.clear);
                _classMeasure.horizontalOverflow = HorizontalWrapMode.Overflow;
                _classMeasure.gameObject.SetActive(false);
            }
            _classMeasure.text = Tracked(UIText.T(cls));
            w = _classMeasure.preferredWidth + 8f;
            _classHeadW[cls] = w;
            return w;
        }
        private Text _classMeasure;
        private readonly Dictionary<string, float> _classHeadW = new Dictionary<string, float>();

        /// <summary>The classes in the order the band lays them, which is the order the room reads them.</summary>
        private static readonly string[] TileClasses =
        {
            "rank.cert.class.counter", "rank.cert.class.door", "rank.cert.class.bench",
            "rank.cert.class.draught", "rank.cert.class.room", "rank.cert.class.market",
            "rank.cert.class.book",
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
            Frame(rt, 3f, UITheme.Cream[3]);   // three, not one (2026-09-21): a thick frame on a smaller tile
            if (spec.Bottle != null && ItemArt.Plates(spec.Bottle, cellar: true) is ItemArt.BottlePlates plates)
            {
                // the bottle whole: its cellar plates at their own size, filled to the brim in its own liquid
                var vessel = NewRect("Vessel", rt);
                Place(vessel, new Vector2(0.5f, 0.5f), new Vector2(28f, 56f), new Vector2(0f, 0f));
                var art = BottleArt.Under(vessel);
                art.Show(plates);
                if (!plates.Sealed) art.SetLevel(UITheme.LiquidColor(spec.Bottle.Info?.Style, spec.Bottle.Type), 1.0, 0f);
            }
            else
            {
                var picRt = NewRect("Pic", rt);
                Place(picRt, new Vector2(0.5f, 0.5f), new Vector2(TileSize - 16f, TileSize - 16f), Vector2.zero);
                var pic = picRt.gameObject.AddComponent<Image>();
                pic.sprite = Trimmed(spec.Art); pic.preserveAspect = true; pic.raycastTarget = false;
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
            if (_ladderHeldClock) { _ladderHeldClock = false; SetPaused(false); }
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
                    Confetti(90);
                }
                float sealT = landed - 0.1f;
                if (sealT >= 0f)
                {
                    if (sealT - dt < 0f) Sfx.Play("stamp", 0.8f);
                    float q = Mathf.Clamp01(sealT / 0.22f);
                    // ...and it lands at the RUNG'S OWN size (2026-09-22): the grade sets how big the seal is,
                    // the stamp only overshoots it.
                    float sc = _ladderSealGrade * Mathf.Lerp(1.8f, 1f, 1f - (1f - q) * (1f - q) * (1f - q));
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
            // BIGGER, AND IN MORE SHAPES (2026-09-22, the author: "konfetilerin boyutunu ve çeşitini arttıralım,
            // daha coşkulu olmalı"): six inks off the palette instead of four, and four cuts - the long streamer,
            // the square, the little bar and the round dot - at twice the size they were, which is what turns a
            // sprinkle into a shower.
            var inks = new[] { UITheme.Amber[3], UITheme.Amber[4], UITheme.Cream[4], UITheme.Cyan[4],
                               UITheme.Magenta[4], UITheme.Lime[3] };
            var cuts = new[] { new Vector2(16f, 6f), new Vector2(10f, 10f), new Vector2(7f, 14f), new Vector2(8f, 8f) };
            var r = _ladderFx.rect;
            for (int i = 0; i < count; i++)
            {
                var rt = NewRect("P", _ladderFx);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                int cut = (int)(Scatter() % (uint)cuts.Length);
                rt.sizeDelta = cuts[cut] * (0.8f + Scatter01() * 0.7f);
                rt.anchoredPosition = new Vector2(r.xMin + Scatter01() * r.width, r.yMax + 20f + Scatter01() * 260f);
                var img = rt.gameObject.AddComponent<Image>();
                if (cut == 3) img.sprite = ChromeArt.Bulb(4);     // the round one: the pack's own dot
                img.color = inks[(int)(Scatter() % (uint)inks.Length)]; img.raycastTarget = false;
                _ladderParticles.Add(new LadderParticle
                {
                    Rt = rt, Img = img,
                    Vel = new Vector2((Scatter01() - 0.5f) * 90f, -(110f + Scatter01() * 170f)),
                    Sway = 26f + Scatter01() * 60f,
                    Spin = (Scatter01() - 0.5f) * 640f,
                    Life = 7f,
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
            // IT OPENS AT ZERO TOO (2026-09-22, the author: "0 yıldızda da sertifika görünmeli, böylece 0.5 yıldız
            // olduğunda ne alınacağı görünür"): a bar that has climbed nothing is handed the plain docket, whose
            // NEXT band is the whole point - it is the only place the first rung's unlocks can be read.
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
