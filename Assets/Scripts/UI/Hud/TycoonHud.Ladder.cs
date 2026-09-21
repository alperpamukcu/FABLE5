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
    /// One window, two doors. At the night's end, the beat after the bill's climb lands on a new rung, it opens
    /// by itself: the old title over a row of stars at the old standing, the stars climbing to the new one, the new
    /// title, and the list of what the rung opened. From the top bar's star row it opens for the rank the bar is
    /// on: the same plate without the climb. It is the pause menu's family (NightPlate, the pack's keys) and reads
    /// BarRank and nothing else, so it cannot disagree with a threshold Core enforces.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private RectTransform _ladderPanel, _ladderPlate, _ladderList, _ladderNewFlag;
        private Text _ladderTitle, _ladderFrom, _ladderTo, _ladderNext, _ladderFromCap, _ladderToCap;
        private Image[] _ladderFromStars, _ladderToStars;
        private float _ladderT = -1f;
        private double _ladderFromStanding, _ladderToStanding;
        /// <summary>The highest rung this run has had its window opened for, and the run it belongs to.</summary>
        private int _ladderSeen = -1;
        private TycoonRun _ladderSeenRun;

        private const float LadderW = 640f, LadderH = 440f, LadderClimb = 1.4f, LadderStar = 32f;

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

            _ladderPlate = NightPlate(_ladderPanel, "Plate", new Vector2(LadderW, LadderH), 0f);
            _ladderTitle = NewText("Title", _ladderPlate, _display, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Place(_ladderTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(LadderW - 80f, 24f), new Vector2(0f, -36f));
            _ladderTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            SunsetRules(_ladderPlate, -66f, LadderW - 80f);

            // WAS, on the left; NOW, on the right — each a caption, a rank title and five stars.
            _ladderFromCap = Caption("FromCap", -0.5f, -90f);
            _ladderToCap = Caption("ToCap", 0.5f, -90f);
            // Two lines of room for a title: "NOBODY'S HEARD OF IT" is twenty characters, and at 16 px that is
            // wider than half the plate - it wraps, and the box is tall enough that it wraps INSIDE its half.
            _ladderFrom = RankTitle("From", -0.5f, -104f, UITheme.Cream[3]);
            _ladderTo = RankTitle("To", 0.5f, -104f, UITheme.Magenta[4]);
            // Place hands a row the anchor as its pivot, so a row at 0.25 hangs a quarter of its width right of the
            // plate's quarter line: the rows are slid by a quarter of their own width (44 of 176) to stand centred
            // in their halves. Slid, not re-pivoted - the lit stars inside keep their own placement (measured twice).
            const float rowQuarter = (BarRating.MaxStars * (LadderStar + 4f) - 4f) * 0.25f;
            _ladderFromStars = LiveStarRow(_ladderPlate, new Vector2(0.25f, 1f), new Vector2(-rowQuarter, -166f), LadderStar, 4f,
                UITheme.Cream[3], UITheme.Night[3]);
            _ladderToStars = LiveStarRow(_ladderPlate, new Vector2(0.75f, 1f), new Vector2(rowQuarter, -166f), LadderStar, 4f,
                UITheme.Amber[3], UITheme.Night[3]);
            var arrow = NewRect("Arrow", _ladderPlate);
            Place(arrow, new Vector2(0.5f, 1f), new Vector2(16f, 16f), new Vector2(0f, -182f));
            var ai = arrow.gameObject.AddComponent<Image>();
            ai.sprite = ChromeArt.Mark("rise"); ai.color = UITheme.Cream[3]; ai.raycastTarget = false;

            var newHead = NewText("NewHead", _ladderPlate, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
            Place(newHead.rectTransform, new Vector2(0.5f, 1f), new Vector2(LadderW - 80f, 14f), new Vector2(0f, -222f));
            newHead.text = UIText.T("rank.window.new");
            _ladderList = NewRect("List", _ladderPlate);
            Place(_ladderList, new Vector2(0.5f, 1f), new Vector2(LadderW - 120f, 130f), new Vector2(0f, -236f));
            _ladderList.pivot = new Vector2(0.5f, 1f);

            _ladderNext = NewText("Next", _ladderPlate, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[2]);
            Place(_ladderNext.rectTransform, new Vector2(0.5f, 0f), new Vector2(LadderW - 80f, 14f), new Vector2(0f, 84f));

            PackWordKey(_ladderPlate, "CONTINUE", UIText.T("rank.window.continue"), "next", MenuPack.Tone.Orange,
                new Vector2(0.5f, 0f), new Vector2(240f, PauseKeyH), new Vector2(0f, 24f), CloseLadder, 240f, 48f + 24f);
            _ladderPanel.gameObject.SetActive(false);
        }

        private Text Caption(string id, float side, float y)
        {
            var t = NewText(id, _ladderPlate, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[2]);
            Place(t.rectTransform, new Vector2(0.5f + side * 0.5f, 1f), new Vector2(260f, 12f), new Vector2(0f, y));
            return t;
        }

        private Text RankTitle(string id, float side, float y, Color ink)
        {
            var t = NewText(id, _ladderPlate, _display, 16, TextAnchor.MiddleCenter, ink);
            Place(t.rectTransform, new Vector2(0.5f + side * 0.5f, 1f), new Vector2(296f, 44f), new Vector2(0f, y));
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.lineSpacing = 1.3f;   // the display face's two lines sat on each other at the default
            return t;
        }

        /// <summary>Opens the window for a climb from <paramref name="from"/> to <paramref name="to"/> stars; with
        /// <paramref name="climb"/> off it stands still at the rank the bar is on (the star row's door).</summary>
        private void ShowLadder(TycoonRun run, double from, double to, bool climb)
        {
            if (_ladderPanel == null || run == null) return;
            var was = BarRank.Of(from);
            var now = BarRank.Of(to);
            _ladderTitle.text = UIText.T(climb ? "rank.window.title" : "rank.window.title_again");
            _ladderFromCap.text = UIText.T("rank.window.from");
            _ladderToCap.text = UIText.T("rank.window.to");
            _ladderFrom.text = UIText.T(was.Title);
            _ladderTo.text = UIText.T(now.Title);
            _ladderFromStanding = from;
            _ladderToStanding = to;
            SetStars(_ladderFromStars, from);
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
                var none = NewText("None", _ladderList, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
                Place(none.rectTransform, new Vector2(0.5f, 1f), new Vector2(LadderW - 120f, 14f), new Vector2(0f, -4f));
                none.text = UIText.T("rank.window.nothing_new");
            }
            foreach (var (key, mark) in lines)
            {
                var row = NewRect("Row", _ladderList);
                Place(row, new Vector2(0f, 1f), new Vector2(LadderW - 120f, 22f), new Vector2(0f, -y));
                row.pivot = new Vector2(0f, 1f);
                var m = NewRect("Mark", row);
                Place(m, new Vector2(0f, 0.5f), new Vector2(16f, 16f), new Vector2(8f, 0f));
                m.pivot = new Vector2(0f, 0.5f);
                var mi = m.gameObject.AddComponent<Image>();
                mi.sprite = mark; mi.color = mark != null ? Color.white : UITheme.Amber[3];
                mi.preserveAspect = true; mi.raycastTarget = false;
                var t = NewText("L", row, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[4]);
                Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(LadderW - 160f, 14f), new Vector2(32f, 0f));
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.text = UIText.T(key);
                y += 24f;
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

        /// <summary>The climb: the NOW stars run from the old standing to the new one on the unscaled clock.</summary>
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
                default: return ChromeArt.Mark("garnish");
            }
        }
    }
}
