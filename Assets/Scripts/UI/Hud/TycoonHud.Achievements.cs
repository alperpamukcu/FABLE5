using System;
using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE ACHIEVEMENTS, SHOWN (2026-09-28, the author: "Oyuna steam etkileşimleri koyalım ... oyuncuya
    /// ilerleme hissi verilsin"). Three things the game draws itself, whether or not Steam is there:
    ///
    /// - THE CARD: an achievement earned slides in at the top right, under the notice line — a dark plate
    ///   with a gold edge, the trophy and the name — and a long count passing a quarter or its half gets
    ///   the same card in cyan with its bar along the foot. One at a time, queued, each held long enough to
    ///   read; never on the notice line itself, which has its own news to carry (a refusal must not wait
    ///   behind a trophy, nor a trophy be written over by a refusal).
    /// - THE LIST: ACHIEVEMENTS on the front door's small row opens every achievement in the order a bar
    ///   earns them, lit when earned, with a bar for the counts still going; a secret stays a question
    ///   mark until it is found.
    /// - THE STORE'S HALF: the status friends see (rich presence), and the overlay coming up pausing an
    ///   open night the way a lost focus does.
    ///
    /// The rules live in Core (AchievementTracker, TycoonRun.Feats) and the keeping in Game
    /// (Achievements); this file only draws.
    /// </summary>
    public sealed partial class TycoonHud
    {
        private readonly Queue<AchievementNotice> _achievementNews = new Queue<AchievementNotice>();

        private struct AchievementNotice
        {
            public string Text;
            public Color Ink;
            public float Seconds;
            public bool Unlock;
            public float Fraction;   // the progress card's bar; unused on an unlock
            public Sprite Icon;      // the achievement's own, lit on an unlock, black and white on the way
        }

        private RectTransform _achievementsPanel;
        private ScrollRect _achievementsScroll;

        private const float AchievementsW = 760f, AchievementsH = 600f, AchievementsPad = 44f;
        private const float AchievementRowH = 74f, AchievementIcon = 64f;

        // ── the card ──
        private RectTransform _achCard, _achCardBar, _achCardFill;
        private Image _achCardIcon;
        private Image[] _achCardEdge;
        private Text _achCardText;
        private float _achCardAt = -1f, _achCardHold;
        private const float AchCardH = 76f, AchCardMaxW = 720f, AchCardSlide = 0.28f, AchCardRight = 14f, AchCardTop = 92f;

        /// <summary>
        /// THE ACHIEVEMENT'S OWN ICON (2026-09-28, the author approving the set: "İkonlar güzel beğendim kullanılsın.
        /// Açık olmayan başarım siyah beyaz gözüksün. Gizli olan başarım kilit iconuyla gözüksün."): the same picture
        /// Steam shows — the plate with the achievement's mark, lit when earned, in black and white until then, and a
        /// secret the padlock until it is found. Items/ach_&lt;id&gt;[_off].png and ach_secret.png, 32x32, drawn at 2x;
        /// the trophy stands in should one be missing.
        /// </summary>
        private static Sprite AchievementPicture(AchievementDefinition a, bool earned)
        {
            string name = earned ? "ach_" + a.Id.ToLowerInvariant()
                : a.Hidden ? "ach_secret" : "ach_" + a.Id.ToLowerInvariant() + "_off";
            return ItemArt.Load(name) ?? ItemArt.Load("ib_m_achievements");
        }

        /// <summary>The name and description in the language being spoken; the data's English where a
        /// table has not caught up (<c>achievement.&lt;id&gt;.name/description</c>, Tools/loc/achievement_keys.py).</summary>
        private static string AchievementText(AchievementDefinition a, string field) =>
            UIText.TOr("achievement." + a.Id.ToLowerInvariant() + "." + field,
                field == "name" ? a.Name : a.Description);

        private void WireAchievements()
        {
            Achievements.Unlocked += OnAchievementUnlocked;
            Achievements.Progressed += OnAchievementProgress;
            StorePlatform.OverlayChanged += OnStoreOverlay;
        }

        private void UnwireAchievements()
        {
            Achievements.Unlocked -= OnAchievementUnlocked;
            Achievements.Progressed -= OnAchievementProgress;
            StorePlatform.OverlayChanged -= OnStoreOverlay;
        }

        private void OnAchievementUnlocked(AchievementDefinition a)
        {
            _achievementNews.Enqueue(new AchievementNotice
            {
                Text = UIText.T("chrome.achievement.unlocked", ("name", UIText.Caps(AchievementText(a, "name")))),
                Ink = UITheme.Amber[4],
                Seconds = 3.8f,
                Unlock = true,
                Icon = AchievementPicture(a, true),
            });
            StoreTimeline.Moment(UIText.Caps(AchievementText(a, "name")), AchievementText(a, "description"),
                "steam_achievement", 100, true);
            if (_achievementsPanel != null && _achievementsPanel.gameObject.activeSelf) RebuildAchievements();
        }

        private void OnAchievementProgress(AchievementProgress p)
        {
            _achievementNews.Enqueue(new AchievementNotice
            {
                Text = UIText.T("chrome.achievement.progress",
                    ("name", UIText.Caps(AchievementText(p.Achievement, "name"))), ("have", p.Have), ("need", p.Need)),
                Ink = UITheme.Cyan[4],
                Seconds = 2.8f,
                Unlock = false,
                Fraction = (float)p.Fraction,
                Icon = AchievementPicture(p.Achievement, false),
            });
            // A list left open keeps its bars honest.
            if (_achievementsPanel != null && _achievementsPanel.gameObject.activeSelf) RebuildAchievements();
        }

        /// <summary>Once a frame: the card in and out, the next one when it has gone; and the status friends see.</summary>
        private void StepAchievements()
        {
            StepAchievementCard();

            var run = Run;
            if (MenuUp || run == null) StorePresence.Show(StorePresence.State.Menu, 0, 0);
            else if (run.Phase == TycoonPhase.DayOpen)
                StorePresence.Show(StorePresence.State.Night, run.Day, (int)Math.Floor(run.Rating.Average));
            else StorePresence.Show(StorePresence.State.Books, run.Day, 0);
            StepTimeline(run);
        }

        // ── the Steam Timeline ──
        private TycoonPhase _timelinePhase = (TycoonPhase)(-1);
        private int _timelineDay = -1;

        /// <summary>
        /// What Steam's recording shows (StoreTimeline): the mode and the line under the bar every frame (change-
        /// detected there), a game phase per night — opened when the doors open, closed at dawn — and, at the moment
        /// a night's books close, the two things worth a clip: a five-star night and a new rank. A closed bar is the
        /// last moment of its run. Every word is the tables' own, already in the player's language.
        /// </summary>
        private void StepTimeline(TycoonRun run)
        {
            if (StorePlatform.Current == null) return;
            if (MenuUp || run == null)
            {
                StoreTimeline.SetMode(StoreTimeline.Mode.Menus);
                StoreTimeline.Tooltip(UIText.T("steam.presence.menu"));
                return;
            }
            int stars = (int)Math.Floor(run.Rating.Average);
            if (run.Phase == TycoonPhase.DayOpen)
            {
                StoreTimeline.SetMode(StoreTimeline.Mode.Playing);
                StoreTimeline.Tooltip(UIText.T("steam.presence.night", ("night", run.Day), ("stars", stars)));
                if (_timelinePhase != TycoonPhase.DayOpen || _timelineDay != run.Day)
                    StoreTimeline.BeginPhase("bar-" + (_bootstrap != null ? _bootstrap.CurrentSeed : "") + "-night-" + run.Day);
            }
            else
            {
                StoreTimeline.SetMode(StoreTimeline.Mode.Menus);
                StoreTimeline.Tooltip(UIText.T("steam.presence.books", ("night", run.Day)));
                if (run.Phase == TycoonPhase.DayEnd && (_timelinePhase != TycoonPhase.DayEnd || _timelineDay != run.Day))
                {
                    string night = UIText.T("steam.presence.night", ("night", run.Day), ("stars", stars));
                    if (run.TonightStars >= BarRating.MaxStars - 1e-6)
                        StoreTimeline.Moment(UIText.TOr("achievement.five_stars_tonight.name", "Five Stars Tonight"), night,
                            "steam_star", 90, true);
                    if (run.RankAfterTonight.Index > run.Rank.Index)
                        StoreTimeline.Moment(UIText.T("rank.window.title"),
                            UIText.T("rank.title.r" + run.RankAfterTonight.Index), "steam_crown", 95, true);
                }
                if (run.Phase == TycoonPhase.Closed && _timelinePhase != TycoonPhase.Closed)
                {
                    StoreTimeline.Moment(UIText.T("hud.closed.banner").Split('\n')[0], null, "steam_death", 100, true);
                    StoreTimeline.EndPhase();
                }
            }
            _timelinePhase = run.Phase;
            _timelineDay = run.Day;
        }

        private void StepAchievementCard()
        {
            if (_achCard != null && _achCardAt >= 0f)
            {
                float t = Time.unscaledTime - _achCardAt;
                float end = _achCardHold + (Motion.Reduced ? 0f : 2f * AchCardSlide);
                if (t >= end)
                {
                    _achCardAt = -1f;
                    _achCard.gameObject.SetActive(false);
                }
                else
                {
                    // In from the right edge, held, and back out the way it came (arithmetic on the unscaled
                    // clock, like the menu's motion; reduced motion shows it where it stands).
                    float gone = 0f;
                    if (!Motion.Reduced)
                    {
                        float inT = Mathf.Clamp01(t / AchCardSlide), outT = Mathf.Clamp01((t - end + AchCardSlide) / AchCardSlide);
                        gone = 1f - Ease(inT) + Ease(outT);
                    }
                    _achCard.anchoredPosition = new Vector2(-AchCardRight + gone * (_achCard.sizeDelta.x + AchCardRight + 8f), -AchCardTop);
                }
            }
            if (_achCardAt >= 0f || _achievementNews.Count == 0 || _toast == null) return;
            ShowAchievementCard(_achievementNews.Dequeue());
        }

        private static float Ease(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

        private void ShowAchievementCard(AchievementNotice n)
        {
            if (_achCard == null) BuildAchievementCard(_toast.transform.parent as RectTransform);
            _achCardText.text = n.Text;
            _achCardText.color = n.Ink;
            // The line at 16 where it fits the card, else at 8: the faces rasterise only at whole multiples.
            _achCardText.fontSize = LanguageFonts.Size(_display, 16);
            const float Pad = 6f, IconRoom = 64f + 12f;
            float room = AchCardMaxW - Pad * 2f - IconRoom;
            if (_achCardText.preferredWidth > room) _achCardText.fontSize = LanguageFonts.Size(_display, 8);
            float w = Mathf.Min(AchCardMaxW, Mathf.Ceil(_achCardText.preferredWidth) + Pad * 2f + IconRoom + 4f);
            _achCard.sizeDelta = new Vector2(w, AchCardH);
            _achCardIcon.sprite = n.Icon;
            _achCardIcon.color = Color.white;   // the icon carries its own colours (or none, on the way)
            foreach (var e in _achCardEdge) e.color = n.Unlock ? UITheme.Amber[3] : UITheme.Cyan[3];
            _achCardBar.gameObject.SetActive(!n.Unlock);
            _achCardFill.anchorMax = new Vector2(Mathf.Clamp01(n.Fraction), 1f);
            _achCardHold = n.Seconds;
            _achCardAt = Time.unscaledTime;
            _achCard.gameObject.SetActive(true);
            _achCard.SetAsLastSibling();
            StepAchievementCard();
            if (n.Unlock) Sfx.Play("star_earn", 0.8f);
        }

        private void BuildAchievementCard(RectTransform root)
        {
            _achCard = NewRect("AchievementCard", root);
            _achCard.anchorMin = _achCard.anchorMax = new Vector2(1f, 1f);
            _achCard.pivot = new Vector2(1f, 1f);
            _achCard.sizeDelta = new Vector2(AchCardMaxW, AchCardH);
            var canvas = _achCard.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30;   // with the notice line: over the market (22), the guide (24), the bench (25)
            var plate = _achCard.gameObject.AddComponent<Image>();
            plate.color = UITheme.Night[1];
            plate.raycastTarget = false;   // it says a thing and takes nothing

            // a 2px edge, in the card's own ink
            _achCardEdge = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var e = NewRect("Edge" + i, _achCard);
                bool across = i < 2;
                e.anchorMin = across ? new Vector2(0f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0f);
                e.anchorMax = across ? new Vector2(1f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 1f);
                e.pivot = across ? new Vector2(0.5f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0.5f);
                e.sizeDelta = across ? new Vector2(0f, 2f) : new Vector2(2f, 0f);
                e.anchoredPosition = Vector2.zero;
                _achCardEdge[i] = e.gameObject.AddComponent<Image>();
                _achCardEdge[i].raycastTarget = false;
            }

            var icon = NewRect("Icon", _achCard);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.sizeDelta = new Vector2(64f, 64f);
            icon.anchoredPosition = new Vector2(6f, 0f);
            _achCardIcon = icon.gameObject.AddComponent<Image>();
            _achCardIcon.preserveAspect = true;
            _achCardIcon.raycastTarget = false;

            _achCardText = NewText("Line", _achCard, _display, 16, TextAnchor.MiddleLeft, UITheme.Amber[4]);
            _achCardText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _achCardText.raycastTarget = false;
            var tr = _achCardText.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(6f + 64f + 12f, 4f);
            tr.offsetMax = new Vector2(-12f, 0f);

            // the progress card's bar, along the foot inside the edge
            _achCardBar = NewRect("Bar", _achCard);
            _achCardBar.anchorMin = new Vector2(0f, 0f);
            _achCardBar.anchorMax = new Vector2(1f, 0f);
            _achCardBar.pivot = new Vector2(0.5f, 0f);
            _achCardBar.offsetMin = new Vector2(6f + 64f + 12f, 8f);
            _achCardBar.offsetMax = new Vector2(-12f, 10f);
            var track = _achCardBar.gameObject.AddComponent<Image>();
            track.color = UITheme.Night[0];
            track.raycastTarget = false;
            _achCardFill = NewRect("Fill", _achCardBar);
            _achCardFill.anchorMin = Vector2.zero;
            _achCardFill.anchorMax = new Vector2(0.5f, 1f);
            _achCardFill.offsetMin = _achCardFill.offsetMax = Vector2.zero;
            var fill = _achCardFill.gameObject.AddComponent<Image>();
            fill.color = UITheme.Cyan[3];
            fill.raycastTarget = false;

            _achCard.gameObject.SetActive(false);
        }

        /// <summary>The store's overlay over an open night pauses it, as a lost focus does.</summary>
        private void OnStoreOverlay(bool shown)
        {
            if (!shown || Paused || MenuUp) return;
            if (Run != null && Run.Phase == TycoonPhase.DayOpen) TogglePause();
        }

        // ── the list ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>Built fresh at every open: the counts move between showings.</summary>
        private void OpenAchievements()
        {
            if (_menuPanel == null) return;
            RebuildAchievements();
            _achievementsPanel.gameObject.SetActive(true);
            _achievementsPanel.SetAsLastSibling();
            if (_achievementsScroll != null) _achievementsScroll.verticalNormalizedPosition = 1f;
            Sfx.Play("menu_open", 0.7f);
        }

        private void CloseAchievements()
        {
            if (_achievementsPanel == null || !_achievementsPanel.gameObject.activeSelf) return;
            _achievementsPanel.gameObject.SetActive(false);
            Sfx.Play("menu_close", 0.6f);
        }

        private void RebuildAchievements()
        {
            bool open = _achievementsPanel != null && _achievementsPanel.gameObject.activeSelf;
            float scroll = _achievementsScroll != null ? _achievementsScroll.verticalNormalizedPosition : 1f;
            if (_achievementsPanel != null) Destroy(_achievementsPanel.gameObject);

            _achievementsPanel = NewRect("Achievements", _menuPanel);
            Stretch(_achievementsPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var field = _achievementsPanel.gameObject.AddComponent<Image>();
            field.color = MenuField;                     // out of game: the menu's own field, wall to wall
            field.raycastTarget = true;

            var plate = NewRect("Plate", _achievementsPanel);
            Place(plate, new Vector2(0.5f, 0.5f), new Vector2(AchievementsW, AchievementsH), new Vector2(0f, -8f));
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.color = UITheme.Night[2];
            plateImg.raycastTarget = true;
            NeonEdge(plate);

            NightTitle(plate, UIText.T("chrome.menu.achievements"), -26f);
            SunsetRules(plate, -66f, AchievementsW - 120f);

            var book = Achievements.Book;
            int earned = 0;
            foreach (var a in book) if (Achievements.IsUnlocked(a.Id)) earned++;
            var count = NewText("Count", plate, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
            Place(count.rectTransform, new Vector2(0.5f, 1f), new Vector2(AchievementsW - 120f, 12f), new Vector2(0f, -78f));
            count.text = UIText.T("chrome.achievements.count", ("have", earned), ("total", book.Count));
            count.raycastTarget = false;

            // the scrolling column
            var view = NewRect("View", plate);
            view.anchorMin = new Vector2(0, 0); view.anchorMax = new Vector2(1, 1);
            view.offsetMin = new Vector2(AchievementsPad, 86f);
            view.offsetMax = new Vector2(-AchievementsPad, -94f);
            view.gameObject.AddComponent<RectMask2D>();
            var viewImg = view.gameObject.AddComponent<Image>();
            viewImg.color = new Color(0, 0, 0, 0);           // a catcher for the wheel, drawn as nothing
            viewImg.raycastTarget = true;
            var content = NewRect("Content", view);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            float width = AchievementsW - AchievementsPad * 2f;
            float y = 0f;
            foreach (var a in book) y = AchievementRow(content, a, width, y) + 6f;
            content.sizeDelta = new Vector2(0f, y);

            _achievementsScroll = view.gameObject.AddComponent<ScrollRect>();
            _achievementsScroll.content = content;
            _achievementsScroll.viewport = view;
            _achievementsScroll.horizontal = false;
            _achievementsScroll.vertical = true;
            _achievementsScroll.movementType = ScrollRect.MovementType.Clamped;
            _achievementsScroll.inertia = false;
            _achievementsScroll.scrollSensitivity = 32f;
            _achievementsScroll.verticalNormalizedPosition = scroll;

            PackWordKey(plate, "BACK", UIText.T("chrome.settings.back"), "back", MenuPack.Tone.Orange, new Vector2(1, 0),
                new Vector2(180, 46), new Vector2(-AchievementsPad, 22), () => { Sfx.Play("click"); CloseAchievements(); }, 140f, 48f + 24f);

            _achievementsPanel.gameObject.SetActive(open);
        }

        /// <summary>One achievement's row at <paramref name="y"/> (measured down from the content's top);
        /// returns the y under it.</summary>
        private float AchievementRow(RectTransform content, AchievementDefinition a, float width, float y)
        {
            bool earned = Achievements.IsUnlocked(a.Id);
            bool dark = a.Hidden && !earned;
            var progress = Achievements.ProgressOf(a);

            var row = NewRect("Row_" + a.Id, content);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(width, AchievementRowH);
            row.anchoredPosition = new Vector2(0f, -y);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = earned ? UITheme.Night[3] : UITheme.Night[1];
            bg.raycastTarget = false;

            // the achievement's own icon: lit when earned, black and white on the way, the padlock for a secret
            var icon = NewRect("Icon", row);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.sizeDelta = new Vector2(AchievementIcon, AchievementIcon);
            icon.anchoredPosition = new Vector2(5f, 0f);
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = AchievementPicture(a, earned);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // the right-hand column: EARNED, or the count and its bar
            const float RightW = 150f;
            float textX = 5f + AchievementIcon + 12f;
            float textW = width - textX - RightW - 12f;
            if (earned)
            {
                var mark = NewText("Earned", row, _body, 8, TextAnchor.MiddleRight, UITheme.Amber[4]);
                PlaceInRow(mark.rectTransform, new Vector2(-10f, 0f), new Vector2(RightW, 12f));
                mark.text = UIText.T("chrome.achievements.earned");
                mark.raycastTarget = false;
            }
            else if (a.ShowsProgress && !dark)
            {
                var figure = NewText("Figure", row, _body, 8, TextAnchor.MiddleRight, UITheme.Cream[3]);
                PlaceInRow(figure.rectTransform, new Vector2(-10f, 8f), new Vector2(RightW, 12f));
                figure.text = progress.Have.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " / " +
                              progress.Need.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                figure.raycastTarget = false;
                var track = NewRect("Track", row);
                PlaceInRow(track, new Vector2(-10f, -8f), new Vector2(RightW, 6f));
                var trackImg = track.gameObject.AddComponent<Image>();
                trackImg.color = UITheme.Night[0];
                trackImg.raycastTarget = false;
                var fill = NewRect("Fill", track);
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2((float)progress.Fraction, 1f);
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                var fillImg = fill.gameObject.AddComponent<Image>();
                fillImg.color = UITheme.Cyan[3];
                fillImg.raycastTarget = false;
            }

            string name = dark ? UIText.T("chrome.achievements.secret") : UIText.Caps(AchievementText(a, "name"));
            string what = dark ? UIText.T("chrome.achievements.secret_hint") : AchievementText(a, "description");

            // The name at 16 where it fits the column, else at 8: the faces rasterise only at whole multiples.
            var title = NewText("Name", row, _display, 16, TextAnchor.UpperLeft,
                earned ? UITheme.Amber[4] : UITheme.Cream[4]);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.raycastTarget = false;
            title.text = name;
            if (title.preferredWidth > textW) title.fontSize = LanguageFonts.Size(_display, 8);
            PlaceTextInRow(title.rectTransform, textX, -18f, textW, 18f);

            var body = NewText("What", row, _body, 8, TextAnchor.UpperLeft,
                earned ? UITheme.Cream[4] : UITheme.Cream[2]);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.raycastTarget = false;
            body.text = what;
            PlaceTextInRow(body.rectTransform, textX, -38f, textW, 16f);

            return y + AchievementRowH;
        }

        private static void PlaceInRow(RectTransform rt, Vector2 fromRightMiddle, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = fromRightMiddle;
        }

        private static void PlaceTextInRow(RectTransform rt, float x, float fromTop, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, fromTop);
        }
    }
}
