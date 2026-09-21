using System;
using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE CERTIFICATE (PLAN_rank_ladder L2/L4/L5, PLAN_second_look §1, 2026-09-21). The author asked for a
    /// level-up screen "sertifika gibi", then for motion and a seal, then — seeing the second draft — for less:
    /// "Senin yaptığın tasarımlar AI slop oluyor ... renk teorisi ... kilit teoriler". So this is the third draft,
    /// written to three colour roles and one accent: the blue plate, cream paper with night ink, amber for the
    /// stars, the seal and the key. One headline (the rank's title), one hero (the stars), one list (what the rung
    /// opened, INSIDE the sheet as picture tiles grouped by where they live), one key. One motion: the sheet
    /// unrolls, the stars climb and pop as they land, the seal is pressed, the tiles come up; confetti and a cheer
    /// over it while the climb plays.
    ///
    /// Two doors: at the night's end, the beat after the bill's climb lands on a new rung, the page opens by itself
    /// with the climb; from the top bar's star row it opens for the rank the bar is on, at rest. There is no
    /// certificate for the foot of the ladder: the star row says when the first one comes. Everything here reads
    /// BarRank and the run's own gates, so the page cannot name a threshold Core does not enforce.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private RectTransform _ladderPanel, _ladderPlate, _ladderReveal, _ladderPaper, _ladderTileBand, _ladderFx, _ladderNewFlag;
        private RectTransform _ladderSeal, _ladderRibbonL, _ladderRibbonR;
        private Text _ladderTitle, _ladderTo, _ladderMeta, _ladderNext, _ladderComfortValue, _ladderServiceValue;
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

        private const float LadderW = 920f, LadderClimb = 1.2f, LadderStar = 32f;
        /// <summary>The sheet: 856 wide under the plate's rings, always; 16:9 at the least, taller only when the
        /// tiles need it (the author's rule, 2026-09-21).</summary>
        private const float PaperW = 856f, PaperTop = -74f, PaperSide = 40f;
        /// <summary>The tiles inside the sheet: 72 square on an 80 pitch, eight to a row so the seal has the
        /// sheet's right-hand corner to itself, a name of two lines under each, a class caption over the first
        /// of each group; two rows at most, the last slot standing for the rest.</summary>
        private const float TileSize = 72f, TilePitch = 80f, TileGroupGap = 16f, TileRowH = 116f;
        private const int TilesPerRow = 8, TileRowsMax = 2;
        /// <summary>Where the tiles begin under the sheet's top, and what the sheet keeps under its last row.</summary>
        private const float TilesTop = 182f, PaperFoot = 16f;
        /// <summary>The plate around the sheet: the header above it, the next-rung line and the key below.</summary>
        private const float PlateAbovePaper = 74f, PlateBelowPaper = 110f;
        private const float ClimbStart = 0.6f;

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

            _ladderPlate = BluePlate(_ladderPanel, "Plate", new Vector2(LadderW, 500f));
            _ladderTitle = NewText("Title", _ladderPlate, _display, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Place(_ladderTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(LadderW - 80f, 24f), new Vector2(0f, -30f));
            _ladderTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            SunsetRules(_ladderPlate, -54f, LadderW - 80f);

            // The sheet stands inside a reveal: a masked rect that opens downward when the page is moving.
            _ladderReveal = NewRect("Reveal", _ladderPlate);
            Place(_ladderReveal, new Vector2(0.5f, 1f), new Vector2(PaperW, 300f), new Vector2(0f, PaperTop));
            _ladderReveal.pivot = new Vector2(0.5f, 1f);
            _ladderReveal.gameObject.AddComponent<RectMask2D>();
            BuildSheet();

            _ladderNext = NewText("Next", _ladderPlate, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[2]);
            Place(_ladderNext.rectTransform, new Vector2(0.5f, 0f), new Vector2(LadderW - 80f, 14f), new Vector2(0f, 82f));

            PackWordKey(_ladderPlate, "CONTINUE", UIText.T("rank.window.continue"), "next", MenuPack.Tone.Orange,
                new Vector2(0.5f, 0f), new Vector2(240f, PauseKeyH), new Vector2(0f, 22f), CloseLadder, 240f, 48f + 24f);

            // The celebration's layer: confetti over the plate, never in the pointer's way.
            _ladderFx = NewRect("Fx", _ladderPanel);
            Stretch(_ladderFx, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _ladderPanel.gameObject.SetActive(false);
        }

        /// <summary>THE SHEET: cream paper with one rule inside its edge, and the certificate's lines in the night
        /// ink — the title, the stars, one line of meta, the two scores, then the tiles.</summary>
        private void BuildSheet()
        {
            _ladderPaper = NewRect("Paper", _ladderReveal);
            Place(_ladderPaper, new Vector2(0.5f, 1f), new Vector2(PaperW, 300f), Vector2.zero);
            _ladderPaper.pivot = new Vector2(0.5f, 1f);
            var paperImg = _ladderPaper.gameObject.AddComponent<Image>();
            paperImg.color = UITheme.Cream[4];
            paperImg.raycastTarget = false;
            var rule = NewRect("Rule", _ladderPaper);
            Stretch(rule, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            Frame(rule, 1f, UITheme.Night[3]);

            // The title the bar is hereby known as — the headline, in the display face's large size; twenty
            // characters at 24 px stay on one line across the sheet.
            _ladderTo = NewText("To", _ladderPaper, _display, 24, TextAnchor.UpperCenter, UITheme.Night[0]);
            Place(_ladderTo.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 32f), new Vector2(0f, -24f));
            _ladderTo.rectTransform.pivot = new Vector2(0.5f, 1f);
            _ladderTo.horizontalOverflow = HorizontalWrapMode.Overflow;

            _ladderToStars = LiveStarRow(_ladderPaper, new Vector2(0.5f, 1f), new Vector2(0f, -64f), LadderStar, 4f,
                UITheme.Amber[3], UITheme.Night[3]);
            _ladderMeta = PaperLine("Meta", -108f, UITheme.Night[1]);

            // COMFORT in the house's medals and SERVICE in its hearts, the way the top bar wears them, each with
            // its number: what the night the rank was conferred was worth.
            _ladderMedals = ScoreGroup("Comfort", -150f, -130f, "rank.cert.comfort", ItemArt.Medal, out _ladderComfortValue);
            _ladderHearts = ScoreGroup("Service", 150f, -130f, "rank.cert.service", ItemArt.Heart, out _ladderServiceValue);

            var cut = NewRect("Cut", _ladderPaper);
            Place(cut, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 1f), new Vector2(0f, -154f));
            var ci = cut.gameObject.AddComponent<Image>();
            ci.color = UITheme.Night[3]; ci.raycastTarget = false;
            var newHead = PaperLine("NewHead", -166f, UITheme.Night[1]);
            newHead.text = UIText.T("rank.window.new");
            _ladderTileBand = NewRect("Tiles", _ladderPaper);
            Place(_ladderTileBand, new Vector2(0.5f, 1f), new Vector2(PaperW, TileRowH), new Vector2(0f, -TilesTop));
            _ladderTileBand.pivot = new Vector2(0.5f, 1f);

            // THE SEAL, in the sheet's bottom-right corner: two ribbon tails first (they hang from behind it, out
            // past the sheet's edge), then the disc with the cocktail glass in it. All in the one accent.
            _ladderRibbonL = Ribbon("RibbonL", -7f, UITheme.Amber[1]);
            _ladderRibbonR = Ribbon("RibbonR", 7f, UITheme.Amber[2]);
            _ladderSeal = NewRect("Seal", _ladderPaper);
            _ladderSeal.anchorMin = _ladderSeal.anchorMax = new Vector2(1f, 0f);
            _ladderSeal.pivot = new Vector2(0.5f, 0.5f);
            _ladderSeal.sizeDelta = new Vector2(56f, 56f);
            _ladderSeal.anchoredPosition = new Vector2(-PaperSide - 28f, 44f);
            var disc = _ladderSeal.gameObject.AddComponent<Image>();
            disc.sprite = SealDisc(56); disc.color = Color.white; disc.raycastTarget = false;
            var glassRt = NewRect("Glass", _ladderSeal);
            Place(glassRt, new Vector2(0.5f, 0.5f), new Vector2(26f, 32f), new Vector2(0f, 1f));
            var glass = glassRt.gameObject.AddComponent<Image>();
            glass.sprite = ItemArt.Load("glass3d_martini_t2_Front");
            glass.color = UITheme.Cream[4]; glass.preserveAspect = true; glass.raycastTarget = false;
            glass.enabled = glass.sprite != null;
        }

        /// <summary>One line of the sheet's small writing, centred, at <paramref name="y"/> below its top.</summary>
        private Text PaperLine(string id, float y, Color ink)
        {
            var t = NewText(id, _ladderPaper, _body, 8, TextAnchor.MiddleCenter, ink);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 14f), new Vector2(0f, y));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// <summary>A score on the sheet: its label, five of the house's icons filled to the value, the number.</summary>
        private Image[] ScoreGroup(string id, float x, float y, string labelKey, Func<bool, float, Sprite> art, out Text value)
        {
            const float px = 16f, gap = 2f, labelW = 72f, valueW = 40f;
            float rowW = BarRating.MaxStars * (px + gap) - gap;
            float total = labelW + 8f + rowW + 8f + valueW;
            float left = x - total * 0.5f;
            var label = NewText(id + "Label", _ladderPaper, _body, 8, TextAnchor.MiddleRight, UITheme.Night[1]);
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

        /// <summary>A ribbon tail: hangs from the seal's centre, pivot at its top, offset <paramref name="dx"/>.</summary>
        private RectTransform Ribbon(string id, float dx, Color ink)
        {
            var rt = NewRect(id, _ladderPaper);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(12f, 62f);
            rt.anchoredPosition = new Vector2(-PaperSide - 28f + dx, 44f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = ink; img.raycastTarget = false;
            return rt;
        }

        private static Sprite s_sealDisc;

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

            int rows = LayTiles(run, was, now, climb, moving);
            var next = BarRank.Above(now);
            _ladderNext.text = next == null ? UIText.T("rank.window.top")
                : UIText.T("rank.window.next", ("stars", next.Stars.ToString("0.0")));

            // The sheet is as tall as its tiles, the plate as tall as the sheet; both stand centred.
            // NEVER WIDER, ONLY TALLER (2026-09-21, the author: "16:9 olacaksa yatay olarak genişleyemez ama dikey
            // olarak genişleyebilir, 16:11, 16:16 olabilir ama yatay genişlik hep aynı"): the sheet is 16:9 at the
            // least, and grows down the same width when the tiles need the room.
            _ladderPaperH = Mathf.Max(Mathf.Ceil(PaperW * 9f / 16f), TilesTop + rows * TileRowH + PaperFoot);
            _ladderPaper.sizeDelta = new Vector2(PaperW, _ladderPaperH);
            _ladderReveal.sizeDelta = new Vector2(PaperW, moving ? 0f : _ladderPaperH);
            _ladderPlate.sizeDelta = new Vector2(LadderW, PlateAbovePaper + _ladderPaperH + PlateBelowPaper);
            _ladderPlate.anchoredPosition = Vector2.zero;
            _ladderPlate.localScale = moving ? new Vector3(0.92f, 0.92f, 1f) : Vector3.one;

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

        /// <summary>
        /// WHAT THE RUNG OPENED, AS TILES INSIDE THE SHEET, grouped by where it lives: the counter's dishes, the
        /// door, the bench's spoon, the taps' lines, the market's bottles and the book's pages the rung's stars
        /// unlocked. A climb lists every rung from the one after WAS up to NOW; the standing window lists the rung
        /// it is on. Returns how many rows the band took (at least one).
        /// </summary>
        private int LayTiles(TycoonRun run, Rung was, Rung now, bool climb, bool moving)
        {
            _ladderTiles.Clear();
            foreach (Transform old in _ladderTileBand) Destroy(old.gameObject);

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
                var none = NewText("None", _ladderTileBand, _body, 8, TextAnchor.MiddleCenter, UITheme.Night[1]);
                Place(none.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperW - 2f * PaperSide, 14f), new Vector2(0f, -40f));
                none.horizontalOverflow = HorizontalWrapMode.Overflow;
                none.text = UIText.T("rank.window.nothing_new");
                return 1;
            }
            // Each class together, in the order the room reads them.
            var byClass = new List<(string cls, string name, Sprite art)>(tiles.Count);
            foreach (var cls in TileClasses)
                foreach (var t in tiles) if (t.cls == cls) byClass.Add(t);
            foreach (var t in tiles) if (Array.IndexOf(TileClasses, t.cls) < 0) byClass.Add(t);
            tiles = byClass;

            // The slots first, dry: where each tile would stand under the wrap rule. Then the cut: two rows at
            // most, and when the tiles need more, the last slot of the last row stands for the rest.
            float rowW = TilesPerRow * TilePitch - (TilePitch - TileSize);
            float left = (PaperW - rowW) * 0.5f;
            var slots = new List<(float x, float y, int row, bool head)>(tiles.Count);
            {
                float x = 0f, y = 0f;
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
                var tile = Tile(x, y, name, art, moving);
                if (isHead)
                {
                    // The class caption rides its first tile, so it comes up with it and not before.
                    var cap = NewText("Class", tile, _body, 8, TextAnchor.MiddleLeft, UITheme.Night[1]);
                    Place(cap.rectTransform, new Vector2(0f, 1f), new Vector2(200f, 12f), new Vector2(0f, 12f));
                    cap.rectTransform.pivot = new Vector2(0f, 1f);
                    cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                    cap.text = UIText.T(cls);
                }
                rows = Math.Max(rows, row + 1);
            }
            if (rest > 0 && shown < slots.Count)
            {
                var (x, y, row, _) = slots[shown];
                Tile(x, y, "+" + rest, null, moving);
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

        /// <summary>One tile on the sheet: a thin frame on the paper, the picture centred, the name under it in
        /// two lines at most. Faded in on its turn when the page is moving.</summary>
        private RectTransform Tile(float x, float y, string name, Sprite art, bool moving)
        {
            var rt = NewRect("Tile", _ladderTileBand);
            Place(rt, new Vector2(0f, 1f), new Vector2(TileSize, TileSize), new Vector2(x, y - 12f));
            rt.pivot = new Vector2(0f, 1f);
            Frame(rt, 1f, UITheme.Night[3]);
            var picRt = NewRect("Pic", rt);
            Place(picRt, new Vector2(0.5f, 0.5f), new Vector2(TileSize - 18f, TileSize - 18f), Vector2.zero);
            var pic = picRt.gameObject.AddComponent<Image>();
            pic.sprite = art; pic.preserveAspect = true; pic.raycastTarget = false;
            pic.enabled = art != null;
            if (art == null)
            {
                var plus = NewText("Plus", rt, _display, 16, TextAnchor.MiddleCenter, UITheme.Night[0]);
                Stretch(plus.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                plus.text = name;
            }
            var cap = NewText("Name", rt, _body, 8, TextAnchor.UpperCenter, UITheme.Night[1]);
            Place(cap.rectTransform, new Vector2(0.5f, 0f), new Vector2(TilePitch, 22f), new Vector2(0f, -3f));
            cap.rectTransform.pivot = new Vector2(0.5f, 1f);
            cap.horizontalOverflow = HorizontalWrapMode.Wrap;
            cap.text = art == null ? "" : name;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = moving ? 0f : 1f;
            group.blocksRaycasts = false; group.interactable = false;
            _ladderTiles.Add(group);
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
            _ladderFxT = -1f;
            ClearParticles();
            _ladderPanel.gameObject.SetActive(false);
            Sfx.Play("menu_close", 0.6f);
        }

        // ── the ceremony, frame by frame ──────────────────────────────────────────────────────────────

        /// <summary>
        /// ON THE UNSCALED CLOCK AT THE CEREMONY'S PACE: the plate settles and the sheet unrolls, the stars climb
        /// from the old standing to the new and pop as they land, the seal is pressed with its ribbons, the tiles
        /// come up one after another, and confetti falls with a cheer as the climb lands.
        /// </summary>
        private void StepLadder()
        {
            if (_ladderPanel == null || !_ladderPanel.gameObject.activeSelf) return;
            float dt = Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace;
            if (_ladderFxT >= 0f)
            {
                _ladderFxT += dt;
                float t = _ladderFxT;

                // the plate settles and the sheet unrolls, in the first half second
                float settle = Mathf.Clamp01(t / 0.25f);
                float s = 0.92f + 0.08f * (1f - (1f - settle) * (1f - settle));
                _ladderPlate.localScale = new Vector3(s, s, 1f);
                float open = Mathf.Clamp01(t / 0.5f);
                _ladderReveal.sizeDelta = new Vector2(PaperW, _ladderPaperH * (1f - (1f - open) * (1f - open)));

                // the stars climb and pop as they land
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

                // the climb lands: the cheer, the second wave, the seal pressed and the ribbons let down
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

                // the tiles come up, one every twentieth of a second, once the seal is down
                float tileT = landed - 0.3f;
                for (int i = 0; i < _ladderTiles.Count; i++)
                {
                    if (_ladderTiles[i] == null) continue;
                    _ladderTiles[i].alpha = Mathf.Clamp01((tileT - i * 0.05f) / 0.2f);
                }

                if (_ladderWave == 0 && t >= 0.15f) { Confetti(36); _ladderWave = 1; }
                if (t > 12f) _ladderFxT = -1f;   // long over; the particles below finish on their own
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

        /// <summary>Confetti in the plate's own three roles: the accent, the paper and the plate's bright ring.</summary>
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
