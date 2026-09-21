using System;
using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE LADDER'S WINDOW (PLAN_rank_ladder L2, 2026-09-21, the author: "Her yıldız seviyesine ulaşıldığında
    /// oyuncuyu kutlayan ve yeni nelerin açıldığını gösteren, eski yıldız seviyesi ile şimdiki arasındaki geçişi
    /// gösteren bir ekran açılmalı, bu ekran üst bardaki yıldız barına tıklayarak tekrar açılabilmeli").
    ///
    /// A CERTIFICATE (2026-09-21, the author: "level up screen ... bu sahnede sertifika gibi olabilir"): a sheet of
    /// cream paper on the author's blue plate, ruled twice in gold with a seal in its corner — the heading, the
    /// title the bar is hereby known as, the stars climbing from the old standing to the new, the title it rose from,
    /// what is new behind the bar, and the night it was conferred. One window, two doors: at the night's end, the
    /// beat after the bill's climb lands on a new rung, it opens by itself and the stars climb; from the top bar's
    /// star row it opens for the rank the bar is on, still. It reads BarRank and nothing else, so it cannot disagree
    /// with a threshold Core enforces.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private RectTransform _ladderPanel, _ladderPlate, _ladderPaper, _ladderList, _ladderNewFlag;
        private Text _ladderTitle, _ladderFrom, _ladderTo, _ladderNext, _ladderFoot;
        private Image[] _ladderToStars;
        private float _ladderT = -1f;
        private double _ladderFromStanding, _ladderToStanding;
        /// <summary>The highest rung this run has had its window opened for, and the run it belongs to.</summary>
        private int _ladderSeen = -1;
        private TycoonRun _ladderSeenRun;

        private const float LadderW = 640f, LadderH = 480f, LadderClimb = 1.4f, LadderStar = 32f;
        /// <summary>The sheet: its size, and how wide the writing on it runs. Measured (2026-09-21): three rows of
        /// unlocks end 242 below the sheet's top, the foot line and the seal sit under them, and the sheet's own
        /// rules need eleven of margin — 292 tall clears the seal by seven.</summary>
        private const float PaperW = 560f, PaperH = 292f, PaperTop = -82f, PaperLineW = 460f;

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

            _ladderPlate = BluePlate(_ladderPanel, "Plate", new Vector2(LadderW, LadderH));
            _ladderTitle = NewText("Title", _ladderPlate, _display, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Place(_ladderTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(LadderW - 80f, 24f), new Vector2(0f, -34f));
            _ladderTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            SunsetRules(_ladderPlate, -60f, LadderW - 80f);

            // THE SHEET. Cream paper, a gold rule two deep and a finer one inside it, a diamond at each corner
            // where the inner rule turns — the ornaments a certificate is drawn with, in the palette's own malt.
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

            var heading = PaperLine("Heading", -16f, UITheme.Malt[2]);
            heading.text = UIText.T("rank.cert.heading");
            var headRule = NewRect("HeadRule", _ladderPaper);
            Place(headRule, new Vector2(0.5f, 1f), new Vector2(120f, 1f), new Vector2(0f, -28f));
            var hri = headRule.gameObject.AddComponent<Image>();
            hri.color = UITheme.Malt[2]; hri.raycastTarget = false;
            var knownAs = PaperLine("KnownAs", -40f, UITheme.Cream[1]);
            knownAs.text = UIText.T("rank.cert.known_as");

            // The title the bar is hereby known as: two lines of room, because "NOBODY'S HEARD OF IT" at 16 px is
            // wider than the writing runs — it wraps, and the box is tall enough that it wraps on the sheet.
            _ladderTo = NewText("To", _ladderPaper, _display, 16, TextAnchor.UpperCenter, UITheme.Night[0]);
            Place(_ladderTo.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperLineW, 44f), new Vector2(0f, -56f));
            _ladderTo.rectTransform.pivot = new Vector2(0.5f, 1f);
            _ladderTo.horizontalOverflow = HorizontalWrapMode.Wrap;
            _ladderTo.lineSpacing = 1.3f;   // the display face's two lines sat on each other at the default

            // The stars: the row that climbs. Place hands a row its anchor as its pivot, so at the sheet's middle
            // it stands centred with no sliding (the two-column plate before this one had to be slid a quarter).
            _ladderToStars = LiveStarRow(_ladderPaper, new Vector2(0.5f, 1f), new Vector2(0f, -104f), LadderStar, 4f,
                UITheme.Amber[3], UITheme.Night[3]);
            _ladderFrom = PaperLine("From", -146f, UITheme.Cream[1]);

            var newHead = PaperLine("NewHead", -162f, UITheme.Malt[2]);
            newHead.text = UIText.T("rank.window.new");
            _ladderList = NewRect("List", _ladderPaper);
            Place(_ladderList, new Vector2(0.5f, 1f), new Vector2(PaperLineW, 70f), new Vector2(0f, -176f));
            _ladderList.pivot = new Vector2(0.5f, 1f);

            // The seal, at the foot's left, set a little askew the way a seal is pressed; the foot line beside it.
            var seal = NewRect("Seal", _ladderPaper);
            Place(seal, new Vector2(0f, 1f), new Vector2(LadderStar, LadderStar), new Vector2(38f, -258f));
            seal.pivot = new Vector2(0.5f, 0.5f);
            seal.anchoredPosition = new Vector2(38f, -258f);
            seal.localRotation = Quaternion.Euler(0f, 0f, -12f);
            var si = seal.gameObject.AddComponent<Image>();
            si.sprite = ItemArt.Star(true, LadderStar); si.color = Color.white;
            si.preserveAspect = true; si.raycastTarget = false;
            _ladderFoot = PaperLine("Foot", -262f, UITheme.Cream[1]);

            _ladderNext = NewText("Next", _ladderPlate, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[2]);
            Place(_ladderNext.rectTransform, new Vector2(0.5f, 0f), new Vector2(LadderW - 80f, 14f), new Vector2(0f, 76f));

            PackWordKey(_ladderPlate, "CONTINUE", UIText.T("rank.window.continue"), "next", MenuPack.Tone.Orange,
                new Vector2(0.5f, 0f), new Vector2(240f, PauseKeyH), new Vector2(0f, 22f), CloseLadder, 240f, 48f + 24f);
            _ladderPanel.gameObject.SetActive(false);
        }

        /// <summary>One line of the sheet's small writing, centred, at <paramref name="y"/> below its top.</summary>
        private Text PaperLine(string id, float y, Color ink)
        {
            var t = NewText(id, _ladderPaper, _body, 8, TextAnchor.MiddleCenter, ink);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperLineW, 14f), new Vector2(0f, y));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
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
        /// the standing on the next rung and opens the window for the climb, the way the bill would. Dev bench.</summary>
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

        /// <summary>Opens the window for a climb from <paramref name="from"/> to <paramref name="to"/> stars; with
        /// <paramref name="climb"/> off it stands still at the rank the bar is on (the star row's door).</summary>
        private void ShowLadder(TycoonRun run, double from, double to, bool climb)
        {
            if (_ladderPanel == null || run == null) return;
            var was = BarRank.Of(from);
            var now = BarRank.Of(to);
            _ladderTitle.text = UIText.T(climb ? "rank.window.title" : "rank.window.title_again");
            _ladderTo.text = UIText.T(now.Title);
            _ladderFrom.text = climb ? UIText.T("rank.cert.risen_from", ("title", UIText.T(was.Title))) : "";
            _ladderFoot.text = UIText.T(climb ? "rank.cert.foot" : "rank.cert.foot_again", ("night", run.Day.ToString()));
            _ladderFromStanding = from;
            _ladderToStanding = to;
            SetStars(_ladderToStars, climb && !Motion.Reduced ? from : to);
            _ladderT = climb && !Motion.Reduced ? 0f : -1f;

            // What the rung opened — every feature from the rung after WAS up to NOW, so a night that climbs two
            // rungs at once (a preset, a dev jump) lists both. Unlocks are lines of the string table, each with the
            // mark that stands for it around the game.
            foreach (Transform old in _ladderList) Destroy(old.gameObject);
            var lines = new List<(string key, Sprite mark)>();
            for (int i = climb ? was.Index + 1 : 1; i <= now.Index; i++)
            {
                if (!climb && i < now.Index) continue;   // the standing window lists the rung it is on only
                foreach (var f in BarRank.Rungs[i].Opens) lines.Add((UnlockKey(f), UnlockMark(f)));
            }
            float y = 0f;
            if (lines.Count == 0)
            {
                var none = NewText("None", _ladderList, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[1]);
                Place(none.rectTransform, new Vector2(0.5f, 1f), new Vector2(PaperLineW, 14f), new Vector2(0f, -4f));
                none.horizontalOverflow = HorizontalWrapMode.Overflow;
                none.text = UIText.T("rank.window.nothing_new");
            }
            foreach (var (key, mark) in lines)
            {
                var row = NewRect("Row", _ladderList);
                Place(row, new Vector2(0f, 1f), new Vector2(PaperLineW, 22f), new Vector2(0f, -y));
                row.pivot = new Vector2(0f, 1f);
                var m = NewRect("Mark", row);
                Place(m, new Vector2(0f, 0.5f), new Vector2(16f, 16f), new Vector2(8f, 0f));
                m.pivot = new Vector2(0f, 0.5f);
                var mi = m.gameObject.AddComponent<Image>();
                mi.sprite = mark; mi.color = mark != null ? Color.white : UITheme.Amber[3];
                mi.preserveAspect = true; mi.raycastTarget = false;
                var t = NewText("L", row, _body, 8, TextAnchor.MiddleLeft, UITheme.Night[1]);
                Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(PaperLineW - 40f, 14f), new Vector2(32f, 0f));
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.text = UIText.T(key);
                y += 22f;
            }
            var next = BarRank.Above(now);
            _ladderNext.text = next == null ? UIText.T("rank.window.top")
                : UIText.T("rank.window.next", ("stars", next.Stars.ToString("0.0")));

            _ladderSeenRun = run;
            if (now.Index > _ladderSeen) _ladderSeen = now.Index;
            RefreshLadderFlag(run);
            CloseId();
            _ladderPanel.gameObject.SetActive(true);
            Sfx.Play(climb ? "stamp" : "menu_open", 0.7f);
        }

        private void CloseLadder()
        {
            if (_ladderPanel == null || !_ladderPanel.gameObject.activeSelf) return;
            _ladderT = -1f;
            _ladderPanel.gameObject.SetActive(false);
            Sfx.Play("menu_close", 0.6f);
        }

        /// <summary>The climb: the stars run from the old standing to the new one on the unscaled clock.</summary>
        private void StepLadder()
        {
            if (_ladderT < 0f || _ladderPanel == null || !_ladderPanel.gameObject.activeSelf) return;
            _ladderT += Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace;
            float k = Mathf.Clamp01(_ladderT / LadderClimb);
            float e = k * k * (3f - 2f * k);
            SetStars(_ladderToStars, Mathf.Lerp((float)_ladderFromStanding, (float)_ladderToStanding, e));
            if (k >= 1f) { _ladderT = -1f; Sfx.Play("key_press", 0.5f); }
        }

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

        /// <summary>THE STAR ROW'S DOOR: the rank the bar is on, no climb.</summary>
        private void OpenLadderFromTheBeam()
        {
            var run = Run;
            if (run == null || _ladderPanel == null) return;
            if (_ladderPanel.gameObject.activeSelf) { CloseLadder(); return; }
            if (!ReferenceEquals(run, _ladderSeenRun)) { _ladderSeen = -1; _ladderSeenRun = run; }
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

        private static string UnlockKey(Feature f)
        {
            switch (f)
            {
                case Feature.IceAndLemon: return "rank.unlock.ice_lemon";
                case Feature.Door: return "rank.unlock.door";
                case Feature.Rims: return "rank.unlock.rims";
                case Feature.Spoon: return "rank.unlock.spoon";
                case Feature.SecondLine: return "rank.unlock.line2";
                case Feature.ThirdLine: return "rank.unlock.line3";
                default: return "rank.unlock.jars";
            }
        }

        private static Sprite UnlockMark(Feature f)
        {
            switch (f)
            {
                case Feature.IceAndLemon: return PrefArt.Ice();
                case Feature.Door: return ChromeArt.Mark("thanks");
                case Feature.Rims: return PrefArt.SaltRim();
                case Feature.Spoon: return ChromeArt.Mark("step_stir");
                case Feature.SecondLine:
                case Feature.ThirdLine: return Resources.Load<Sprite>("Fixtures/fx_tap_beer");   // the room's own tower
                default: return ChromeArt.Mark("garnish");
            }
        }
    }
}
