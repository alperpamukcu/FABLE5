using System.Linq;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Quest: the job the hostess left, as a message at the top left (2026-09-28, spec §D).
    //
    // The author, 2026-09-27: the active job sits at the top left "like a message bubble" - collapsed, ONE line with
    // an icon and the two or three facts that matter (what is owed, what it pays); under the pointer it opens like
    // "read more" onto the details. It replaces the week's job tab that hung under the beam: the weekly job is gone
    // (TycoonRun.Quests), and a job that comes from a person reads as something she said, not as a gauge.
    //
    // Core owns every fact on it (TycoonRun.Quest, HostessComesOn, QuestNextUp); this file only draws them, and draws
    // nothing at all before her first job, after her last, or anywhere but an open night.
    public sealed partial class TycoonHud
    {
        private const float QuestRowH = 30f, QuestOpenH = 156f, QuestOpenW = 380f, QuestMinW = 140f;
        private const float QuestOwedMaxW = 230f, QuestIconX = 10f, QuestTextX = 32f;
        private const float QuestPipW = 12f, QuestPipH = 6f, QuestPipGap = 3f;

        /// <summary>How long the message takes to open, and how long the pointer may be off it before it shuts - so a
        /// hand crossing the edge on its way to the rim does not snap it closed under itself.</summary>
        private const float QuestOpenSeconds = 0.18f, QuestShutGrace = 0.15f;

        private RectTransform _questBubble, _questDetail, _questPips, _questBar, _questBarFill, _questDotRt;
        private CanvasGroup _questGroup;
        private Image _questPlate, _questTail, _questIcon, _questDot, _questFace, _questCoin;
        private Text _questOwed, _questReward, _questName, _questNumber, _questLine, _questProgress, _questStatus;

        private bool _questOpenWant;           // the pointer is on it (or was, inside the grace)
        private float _questOpenT;             // 0 shut, 1 open
        private float _questShutAt = -1f;      // unscaled time the grace runs out; -1 = none running
        private float _questShutW = QuestMinW; // the one-line width, measured off its words
        private bool _questUnread;             // a job just handed over and not yet looked at
        private float _questDotT = -1f;        // the dot's two pulses, 0..1; -1 = steady
        private string _questSig = "";
        private int _questPipsFor = -1;

        // ── construction ─────────────────────────────────────────────────────────────────────────────────────────────

        private void BuildQuestBubble(RectTransform root)
        {
            // UNDER THE BEAM, ON ITS INK (the tenth list): the left edge sits at TopEdge like the hour's case, whose
            // well draws its ink from the rect's own edge - so the two are the same edge - and six units under the
            // beam's foot rather than hung into its tube, because a message is a thing that ARRIVED, not a tab of the bar.
            _questBubble = NewRect("QuestBubble", root);
            _questBubble.anchorMin = _questBubble.anchorMax = new Vector2(0, 1);
            _questBubble.pivot = new Vector2(0, 1);
            _questBubble.sizeDelta = new Vector2(QuestMinW, QuestRowH);
            _questBubble.anchoredPosition = new Vector2(TopEdge, -(TopBarH + 6f));
            _questGroup = _questBubble.gameObject.AddComponent<CanvasGroup>();
            _questGroup.blocksRaycasts = true;              // the pointer opens it
            _questPlate = _questBubble.gameObject.AddComponent<Image>();
            _questPlate.sprite = ChromeArt.MessageBubble(false);
            _questPlate.type = Image.Type.Sliced;
            _questPlate.raycastTarget = true;

            // The tail hangs two rows INTO the plate's foot and opens it (ChromeArt.MessageTail), just past the
            // rounded corner so the rim runs straight into it.
            var tail = NewRect("Tail", _questBubble);
            tail.anchorMin = tail.anchorMax = new Vector2(0, 0);
            tail.pivot = new Vector2(0, 1);
            tail.sizeDelta = new Vector2(9f, 7f);
            tail.anchoredPosition = new Vector2(6f, 2f);
            _questTail = tail.gameObject.AddComponent<Image>();
            _questTail.sprite = ChromeArt.MessageTail(false);
            _questTail.raycastTarget = false;

            // ── the one line ──────────────────────────────────────────────────────────────────────────────────────
            var iconRt = NewRect("Icon", _questBubble);
            Place(iconRt, new Vector2(0, 1), new Vector2(16, 16), new Vector2(QuestIconX, -QuestRowH * 0.5f));
            iconRt.pivot = new Vector2(0, 0.5f);
            _questIcon = iconRt.gameObject.AddComponent<Image>();
            _questIcon.preserveAspect = true;
            _questIcon.raycastTarget = false;

            _questOwed = QuestText("Owed", _questBubble, 16, UITheme.Cream[4], new Vector2(QuestTextX, -QuestRowH * 0.5f));
            _questReward = QuestText("Reward", _questBubble, 16, UITheme.Lime[3], new Vector2(QuestTextX, -QuestRowH * 0.5f));

            // THE UNREAD DOT: a job handed over and not looked at yet. It rides the top-right corner of whatever
            // width the message is, and goes the moment the pointer is on it.
            _questDotRt = NewRect("Unread", _questBubble);
            _questDotRt.anchorMin = _questDotRt.anchorMax = new Vector2(1, 1);
            _questDotRt.pivot = new Vector2(0.5f, 0.5f);
            _questDotRt.sizeDelta = new Vector2(6f, 6f);
            _questDotRt.anchoredPosition = new Vector2(-7f, -5f);   // its box's right edge 4 in, top edge 2 down
            _questDot = _questDotRt.gameObject.AddComponent<Image>();
            _questDot.sprite = ChromeArt.Bulb(6);
            _questDot.color = UITheme.Magenta[4];
            _questDot.raycastTarget = false;
            _questDotRt.gameObject.SetActive(false);

            // ── the details, revealed rather than scaled ──────────────────────────────────────────────────────────
            // Everything under the line lives in a mask as tall as the message is: opening it is the plate growing
            // over rows that were always laid, which is "read more" and not a second window popping up.
            _questDetail = NewRect("Detail", _questBubble);
            Stretch(_questDetail, Vector2.zero, Vector2.one, new Vector2(0, 4f), new Vector2(0, -QuestRowH));
            _questDetail.gameObject.AddComponent<RectMask2D>();
            float y0 = QuestRowH;   // the rows are laid in the message's own coordinates, less the line above them

            var rule = NewRect("Rule", _questDetail);
            rule.anchorMin = new Vector2(0, 1); rule.anchorMax = new Vector2(1, 1);
            rule.pivot = new Vector2(0.5f, 1);
            rule.sizeDelta = new Vector2(-16f, 1f);
            rule.anchoredPosition = new Vector2(0, -(32f - y0));
            var ruleImg = rule.gameObject.AddComponent<Image>();
            ruleImg.color = UITheme.Magenta[1];
            ruleImg.raycastTarget = false;

            // her face, in the same two-unit cream frame the tab gave it
            var frame = NewRect("Face", _questDetail);
            Place(frame, new Vector2(0, 1), new Vector2(36, 36), new Vector2(QuestIconX, -(40f - y0)));
            var frameImg = frame.gameObject.AddComponent<Image>();
            frameImg.color = UITheme.Cream[1];
            frameImg.raycastTarget = false;
            var photo = NewRect("Photo", frame);
            Stretch(photo, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            _questFace = photo.gameObject.AddComponent<Image>();
            _questFace.preserveAspect = true;
            _questFace.raycastTarget = false;

            _questName = NewText("Who", _questDetail, _display, 16, TextAnchor.UpperLeft, UITheme.Cream[4]);
            Place(_questName.rectTransform, new Vector2(0, 1), new Vector2(310, 20), new Vector2(50f, -(40f - y0)));
            _questName.horizontalOverflow = HorizontalWrapMode.Overflow;
            _questNumber = NewText("Number", _questDetail, _body, 8, TextAnchor.UpperLeft, UITheme.Magenta[4]);
            Place(_questNumber.rectTransform, new Vector2(0, 1), new Vector2(310, 12), new Vector2(50f, -(60f - y0)));
            _questNumber.horizontalOverflow = HorizontalWrapMode.Overflow;

            // her line: the last thing she said when she handed it over, three lines at most
            _questLine = NewText("Said", _questDetail, _body, 8, TextAnchor.UpperLeft, UITheme.Cream[3]);
            Place(_questLine.rectTransform, new Vector2(0, 1), new Vector2(360, 32), new Vector2(QuestIconX, -(80f - y0)));
            _questLine.verticalOverflow = VerticalWrapMode.Truncate;

            // how far along: pips for a count, a bar for the room, a line for the rest
            _questPips = NewRect("Pips", _questDetail);
            Place(_questPips, new Vector2(0, 1), new Vector2(200, QuestPipH), new Vector2(QuestIconX, -(112f - y0)));
            _questBar = NewRect("Bar", _questDetail);
            Place(_questBar, new Vector2(0, 1), new Vector2(180, QuestPipH), new Vector2(QuestIconX, -(112f - y0)));
            var barBack = _questBar.gameObject.AddComponent<Image>();
            barBack.color = new Color(UITheme.Cream[1].r, UITheme.Cream[1].g, UITheme.Cream[1].b, 0.35f);
            barBack.raycastTarget = false;
            _questBarFill = NewRect("Fill", _questBar);
            _questBarFill.anchorMin = new Vector2(0, 0); _questBarFill.anchorMax = new Vector2(0, 1);
            _questBarFill.pivot = new Vector2(0, 0.5f);
            _questBarFill.sizeDelta = Vector2.zero;
            _questBarFill.anchoredPosition = Vector2.zero;
            var fillImg = _questBarFill.gameObject.AddComponent<Image>();
            fillImg.color = UITheme.Magenta[3];
            fillImg.raycastTarget = false;
            _questProgress = NewText("Progress", _questDetail, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            Place(_questProgress.rectTransform, new Vector2(0, 1), new Vector2(340, 12), new Vector2(QuestIconX, -(109f - y0)));
            _questProgress.horizontalOverflow = HorizontalWrapMode.Overflow;

            // what it pays, or when she comes for the next one
            var coin = NewRect("Coin", _questDetail);
            Place(coin, new Vector2(0, 1), new Vector2(16, 16), new Vector2(QuestIconX, -(130f - y0)));
            _questCoin = coin.gameObject.AddComponent<Image>();
            _questCoin.sprite = ItemArt.Coin(16f);
            _questCoin.preserveAspect = true;
            _questCoin.raycastTarget = false;
            _questStatus = NewText("Status", _questDetail, _body, 16, TextAnchor.MiddleLeft, UITheme.Lime[3]);
            Place(_questStatus.rectTransform, new Vector2(0, 1), new Vector2(340, 20), new Vector2(30f, -(128f - y0)));
            _questStatus.horizontalOverflow = HorizontalWrapMode.Overflow;

            // THE POINTER OPENS IT. A relay rather than an EventTrigger (HoverRelay: the trigger swallows the wheel),
            // and a grace on the way out.
            var relay = _questBubble.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () =>
            {
                _questOpenWant = true;
                _questShutAt = -1f;
                _questUnread = false;           // looked at
            };
            relay.Exited = () => _questShutAt = Time.unscaledTime + QuestShutGrace;
            MarkHoverable(_questBubble, _questPlate);

            _questBubble.gameObject.SetActive(false);
        }

        private Text QuestText(string name, RectTransform parent, int size, Color ink, Vector2 at)
        {
            var t = NewText(name, parent, _body, size, TextAnchor.MiddleLeft, ink);
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(QuestOwedMaxW, 20), at);
            t.rectTransform.pivot = new Vector2(0, 0.5f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ── the reading ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The story's hostess, off the bootstrap's own parsed cast (her name lives in papers.json).</summary>
        private StoryCharacter Hostess => _bootstrap?.Story?.Cast?.FirstOrDefault(c => c.IsHost);

        /// <summary>What the toast, the bill and the message call her: the name up to its first space, in capitals.</summary>
        private string HostessWho()
        {
            var host = Hostess;
            return host != null ? UIText.Caps(host.ShortName) : "";
        }

        /// <summary>
        /// Draws the message off the run. Asked every frame from RefreshTopBar; it re-lays its words only when
        /// something it prints has moved.
        /// </summary>
        private void RefreshQuestBubble(TycoonRun run)
        {
            if (_questBubble == null) return;
            var quest = run.Quest;
            // NOT BEFORE HER FIRST JOB, NOT AFTER HER LAST, AND ONLY ON AN OPEN NIGHT: the day's end and the market
            // own the screen then, and a first night has nothing to say here at all.
            bool show = run.Quests != null && quest != null && !run.QuestChainOver && !MenuUp
                        && run.Phase == TycoonPhase.DayOpen;
            if (_questBubble.gameObject.activeSelf != show)
            {
                _questBubble.gameObject.SetActive(show);
                // A message hidden under the pointer never hears it leave; it comes back shut.
                _questOpenWant = false;
                _questShutAt = -1f;
                _questOpenT = 0f;
            }
            if (!show) return;

            bool done = quest.IsDone;
            var next = run.QuestNextUp;
            bool waiting = done && !run.QuestDoneUnsaid && next != null && run.Rank.Index < next.Rung;
            bool comesTonight = run.HostessComesOn > 0 && run.Day >= run.HostessComesOn;
            string sig = quest.Id + "|" + quest.Progress + "/" + quest.Target + "|" + done + "|" + waiting + "|"
                         + comesTonight + "|" + run.Rank.Index + "|" + run.ComfortBase.ToString("0.00") + "|"
                         + (quest.Kind == QuestKind.Fit ? run.LadderLevel(quest.Definition.Slot) : 0) + "|"
                         + Localization.Current.Code + "|" + (Hostess?.Name ?? "");
            if (sig == _questSig) return;
            _questSig = sig;
            LayQuestBubble(run, quest, done, waiting, next, comesTonight);
        }

        private void LayQuestBubble(TycoonRun run, ActiveQuest quest, bool done, bool waiting,
            QuestDefinition next, bool comesTonight)
        {
            var def = quest.Definition;
            var host = Hostess;
            var face = LookForStory(host)?.Face;

            // THE PLATE SAYS WHICH KIND OF NEWS: the night's ink while it is owed, lime once it is paid.
            _questPlate.sprite = ChromeArt.MessageBubble(done);
            _questTail.sprite = ChromeArt.MessageTail(done);

            // ── the one line ──
            // The icon says what kind of job before the words do. It is never tinted: the star, the rosette and the
            // medal carry their own colours (CLAUDE.md: one star). Waiting on a rung, it is her face instead.
            var icon = waiting && face != null ? face : QuestIcon(run, quest);
            _questIcon.sprite = icon;
            _questIcon.enabled = icon != null;
            _questIcon.color = Color.white;

            string owed, reward;
            if (waiting)
            {
                owed = UIText.T("chrome.quest.waiting", ("title", UIText.T(BarRank.Rungs[next.Rung].Title)));
                reward = "";
            }
            else if (done)
            {
                owed = UIText.T("chrome.quest.done_line", ("reward", quest.Reward));
                reward = "";
            }
            else
            {
                // An instruction, not a scoreboard: "3 MORE NEGRONIS", counting down (Core's OwedLine).
                owed = UIText.Caps(UIText.T(quest.OwedLine()));
                reward = UIText.T("chrome.quest.reward", ("reward", quest.Reward));
            }
            _questOwed.color = done ? UITheme.Lime[4] : UITheme.Cream[4];
            _questOwed.text = FitToWidth(_questOwed, owed, QuestOwedMaxW);
            float owedW = Mathf.Min(QuestOwedMaxW, _questOwed.preferredWidth);
            _questReward.text = reward;
            _questReward.gameObject.SetActive(reward.Length > 0);
            _questReward.rectTransform.anchoredPosition = new Vector2(QuestTextX + owedW + 10f, -QuestRowH * 0.5f);
            float contentW = QuestTextX + owedW + (reward.Length > 0 ? 10f + _questReward.preferredWidth : 0f);
            _questShutW = Mathf.Clamp(contentW + 12f, QuestMinW, QuestOpenW);

            // ── the details ──
            _questFace.sprite = face;
            _questFace.enabled = face != null;
            _questFace.transform.parent.gameObject.SetActive(face != null);
            _questName.text = host != null ? UIText.Caps(host.Name) : "";
            int total = run.Quests != null ? run.Quests.Count : 0;
            _questNumber.text = UIText.T("chrome.quest.number", ("at", def.Index + 1), ("total", total),
                ("title", UIText.Caps(UIText.Data("quest", def.Id, "title", def.Title))));
            _questLine.text = "\"" + HostessQuotedLine(quest, host) + "\"";
            LayQuestProgress(run, quest, done);

            bool pays = !done;
            _questCoin.gameObject.SetActive(pays);
            _questStatus.rectTransform.anchoredPosition = new Vector2(pays ? 30f : QuestIconX, -(128f - QuestRowH));
            if (pays)
            {
                _questStatus.fontSize = LanguageFonts.Size(_body, 16);
                _questStatus.color = UITheme.Lime[3];
                _questStatus.text = UIText.T("chrome.quest.pays", ("reward", quest.Reward));
            }
            else
            {
                _questStatus.fontSize = LanguageFonts.Size(_body, 8);
                _questStatus.color = UITheme.Cream[3];
                _questStatus.text = UIText.T(comesTonight ? "chrome.quest.comes_tonight" : "chrome.quest.comes_tomorrow",
                    ("who", HostessWho()));
            }
        }

        /// <summary>The kind's own picture (spec D.4): the drink, the rosette, a pint, the dish, the cloth, the card,
        /// the star, the medal, the piece.</summary>
        private Sprite QuestIcon(TycoonRun run, ActiveQuest quest)
        {
            switch (quest.Kind)
            {
                case QuestKind.Serve:
                    return DrinkIconOf(run, quest.RecipeId);
                case QuestKind.Perfect:
                    return ItemArt.Perfect(16);
                case QuestKind.Pints:
                    return DrinkIconOf(run, "draught");
                case QuestKind.Garnish:
                    return quest.Definition.Preps.Count > 0 ? PrefArt.ForPreparation(quest.Definition.Preps[0].Id) : null;
                case QuestKind.Clean:
                    return ItemArt.Load("towel");            // the counter cloth (bar_cloth and cloth were never drawn)
                case QuestKind.Door:
                    return ItemArt.Load("card_body");
                case QuestKind.Rank:
                    return ItemArt.Star(true, 16);
                case QuestKind.Comfort:
                    return ItemArt.Medal(true, 16);
                case QuestKind.Fit:
                    FixtureDefinition piece = null;
                    foreach (var f in run.FixtureCatalogue)
                        if (f.Id == quest.Definition.FixtureId) { piece = f; break; }
                    var art = piece != null && !string.IsNullOrEmpty(piece.Sprite)
                        ? Resources.Load<Sprite>("Fixtures/" + piece.Sprite) : null;
                    return art != null ? art : ItemArt.Medal(true, 16);
                default:
                    return null;
            }
        }

        private Sprite DrinkIconOf(TycoonRun run, string recipeId)
        {
            foreach (var r in run.AllRecipes)
                if (r.Id == recipeId) return DrinkIcon.For(r, _bootstrap != null ? _bootstrap.Glassware : null);
            return null;
        }

        /// <summary>How far along, in the shape the kind is counted in: a pip each for a count, the room against its
        /// goal as a bar, the rung or the ladder's step as words.</summary>
        private void LayQuestProgress(TycoonRun run, ActiveQuest quest, bool done)
        {
            var def = quest.Definition;
            bool pips = !def.IsStateGoal;
            bool bar = def.Kind == QuestKind.Comfort;
            _questPips.gameObject.SetActive(pips);
            _questBar.gameObject.SetActive(bar);
            if (pips) LayQuestPips(quest);

            if (bar)
            {
                double now = run.ComfortBase;
                float k = def.GoalComfort > 0 ? Mathf.Clamp01((float)(now / def.GoalComfort)) : 1f;
                if (done) k = 1f;
                _questBarFill.sizeDelta = new Vector2(180f * k, 0f);
                var fill = _questBarFill.GetComponent<Image>();
                if (fill != null) fill.color = done ? UITheme.Lime[3] : UITheme.Magenta[3];
                _questProgress.rectTransform.anchoredPosition = new Vector2(QuestIconX + 190f, -(109f - QuestRowH));
                _questProgress.text = UIText.T("chrome.quest.progress_comfort",
                    ("now", now.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                    ("goal", def.GoalComfort.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
            }
            else
            {
                _questProgress.rectTransform.anchoredPosition = new Vector2(QuestIconX, -(109f - QuestRowH));
                _questProgress.text = def.Kind == QuestKind.Rank
                    ? UIText.T("chrome.quest.progress_rank", ("title", UIText.T(run.Rank.Title)))
                    : def.Kind == QuestKind.Fit
                        ? UIText.T("chrome.quest.progress_fit", ("now", run.LadderLevel(def.Slot)), ("goal", def.Level))
                        : "";
            }
        }

        /// <summary>A pip for each of what she asked, lit for each done - lime once it is paid, the magenta of her rim
        /// until then - redrawn only when the count moves.</summary>
        private void LayQuestPips(ActiveQuest quest)
        {
            int sig = quest.Target * 1000 + quest.Progress * 10 + (quest.IsDone ? 1 : 0);
            if (sig == _questPipsFor) return;
            _questPipsFor = sig;
            foreach (Transform old in _questPips) Destroy(old.gameObject);
            for (int i = 0; i < quest.Target; i++)
            {
                var pip = NewRect("P" + i, _questPips);
                Place(pip, new Vector2(0, 0.5f), new Vector2(QuestPipW, QuestPipH), new Vector2(i * (QuestPipW + QuestPipGap), 0f));
                pip.pivot = new Vector2(0, 0.5f);
                var pi = pip.gameObject.AddComponent<Image>();
                bool lit = i < quest.Progress;
                pi.color = !lit ? new Color(UITheme.Cream[1].r, UITheme.Cream[1].g, UITheme.Cream[1].b, 0.35f)
                         : quest.IsDone ? UITheme.Lime[3] : UITheme.Magenta[3];
                pi.raycastTarget = false;
            }
        }

        /// <summary>A line cut at a character boundary to fit <paramref name="maxW"/>, with an ellipsis where it was
        /// cut (the ladder's tiles do the same) - one line never wraps into the room under it.</summary>
        private static string FitToWidth(Text t, string s, float maxW)
        {
            t.text = s;
            if (string.IsNullOrEmpty(s) || t.preferredWidth <= maxW) return s;
            for (int cut = s.Length - 1; cut > 1; cut--)
            {
                t.text = s.Substring(0, cut).TrimEnd() + "…";
                if (t.preferredWidth <= maxW) return t.text;
            }
            return t.text;
        }

        // ── the motion ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The message's own clocks, all unscaled: opening and shutting under the pointer, the unread dot's two
        /// pulses, and the busy fade (from the job tab: over the open cellar or a bench it drops to a third rather
        /// than going, and the pointer brings it back whole).
        /// </summary>
        private void StepQuestBubble()
        {
            if (_questBubble == null || !_questBubble.gameObject.activeInHierarchy) return;
            float dt = Time.unscaledDeltaTime;

            if (_questShutAt > 0f && Time.unscaledTime >= _questShutAt)
            {
                _questOpenWant = false;
                _questShutAt = -1f;
            }
            float want = _questOpenWant ? 1f : 0f;
            _questOpenT = Motion.Reduced ? want : Mathf.MoveTowards(_questOpenT, want, dt / QuestOpenSeconds);
            float e = Mathf.SmoothStep(0f, 1f, _questOpenT);
            _questBubble.sizeDelta = new Vector2(Mathf.Lerp(_questShutW, QuestOpenW, e), Mathf.Lerp(QuestRowH, QuestOpenH, e));
            if (_questDetail.gameObject.activeSelf != _questOpenT > 0f) _questDetail.gameObject.SetActive(_questOpenT > 0f);

            // THE DOT: two pulses when the job lands, then it simply stays lit until the message is looked at.
            bool dot = _questUnread;
            if (_questDotRt.gameObject.activeSelf != dot) _questDotRt.gameObject.SetActive(dot);
            if (dot)
            {
                float s = 1f;
                if (_questDotT >= 0f && !Motion.Reduced)
                {
                    _questDotT += dt;
                    if (_questDotT >= 1f) _questDotT = -1f;
                    else s = 1f + 0.4f * Mathf.Sin(Mathf.PI * Mathf.Repeat(_questDotT, 0.5f) / 0.5f);
                }
                _questDotRt.localScale = new Vector3(s, s, 1f);
            }

            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool under = _questOpenWant;
            if (!under && mouse != null)
                under = RectTransformUtility.RectangleContainsScreenPoint(_questBubble, mouse.position.ReadValue(), null);
            bool busy = CellarOpen || (_flow != null && _flow.IsOpen);
            float alpha = under ? 1f : busy ? 0.35f : 1f;
            _questGroup.alpha = Motion.Reduced ? alpha : Mathf.MoveTowards(_questGroup.alpha, alpha, dt * 3.5f);
        }

        /// <summary>
        /// The message's news, read once each (spec D.5): a job paid is a toast with the coin and the till's own
        /// sound, the moment it lands; a job handed over is the dot, and no toast - she has just said it to your face.
        /// Not while the room is still coming up behind the curtain, so a dawn's pay lands on the night's first frame.
        /// </summary>
        private void ReadQuestNews(TycoonRun run)
        {
            if (DoorsClosed || MenuUp) return;
            var done = run.TakeQuestJustDone();
            if (done != null)
            {
                string who = HostessWho();
                Toast(UIText.T("chrome.toast.quest_paid", ("who", who), ("reward", done.Reward)),
                      UITheme.Lime[3], 3.2f, ChromeArt.Mark("cash"));
                Sfx.Play("cash", 0.9f);
                LogService(UIText.T("chrome.log.quest_done", ("who", who), ("reward", done.Reward)));
            }
            var given = run.TakeQuestJustGiven();
            if (given != null)
            {
                _questUnread = true;
                _questDotT = 0f;
                LogService(UIText.T("chrome.log.quest_given", ("who", HostessWho()),
                    ("title", UIText.Caps(UIText.Data("quest", given.Id, "title", given.Definition.Title)))));
            }
        }
    }
}
