using System.Linq;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    public sealed partial class TycoonHud
    {
        // ── THE BEAM, LAID OUT BY WHAT IT SAYS (2026-09-22) ─────────────────────────────────────────────────────────
        //
        // The author's eighth list: "Üst barın düzenini tekrardan tasarlayalım, saat ve tarih beraber olabilir, müzik
        // çalar daha da kompakt hale getirilebilir, yıldız ve puanlar daha kompakt hale gelebilir gereksiz yere
        // büyütülmüş duruyor, dillere göre bu paneller genişletilip sıkıştırılabilmeli, para göstergesinin yerini üst
        // panele alalım ve tarzını değiştirelim. Ayarlar butonunun üstündeki görseli değiştir."
        //
        // Left to right: THE HOUR - the clock, the night's number and the night's name with its crowd, one well - and
        // the PLAYER. Right to left: the SETTINGS key, the STANDING (the house's two strips and the five stars, one well)
        // and the TILL, which came up off the room onto the beam as the clock's own seven-bar machine in the money's
        // green (SegmentFigure, built for it and never hung). Every well is as wide as its words in the language being
        // spoken - measured once they are set, not reserved for English - and LayTopBar stands them edge to edge a gap
        // apart; the player takes the middle that is left and shortens its words to fit it.

        private const float TopEdge = 16f, TopGap = 8f, TopWellH = 42f;
        /// <summary>The standing's star: the small drawing (14x12) at exactly 2x. The big one at 36 was a headline.</summary>
        private const float TopStarW = 28f, TopStarH = 24f, TopStarPitch = 30f;
        private const float TopHourDigitsW = 110f, TopHourDayW = 44f, TopWellPad = 12f;

        private RectTransform _hourWell, _hourRuleA, _hourDayBlock, _hourRuleB, _tillWell, _rateWell, _cogKeyRt;
        private RectTransform _rateHouse, _rateStars;
        private Text _hourDayCap, _rateCapService, _rateCapComfort;
        private SegmentFigure _tillFigure;
        private int _tillFigureShown = int.MinValue;
        private string _topBarSig = "";

        private void BuildTopBarWells(RectTransform top)
        {
            BuildHourWell(top);
            BuildTillWell(top);
            BuildRateWell(top);

            // A NEW MARK ON THE SETTINGS KEY (the eighth list: "Ayarlar butonunun üstündeki görseli değiştir"): the
            // house's own flat cog, drawn on the same 16 px grid as every mark on the beam and shown at exactly 2x in
            // the beam's gold, where the gold 3D gear stood out as the one rendered object on a drawn bar.
            var cog = ChromeArt.Mark("cog");
            _cogKeyRt = NewButton(top, "SETTINGS", new Vector2(1, 0.5f), new Vector2(TopWellH, TopWellH),
                new Vector2(-TopEdge, 0), UITheme.Night[2], ToggleSettings, cog);
            Hairline(_cogKeyRt, new Vector2(0, 1), new Vector2(1, 1), UITheme.Night[3]);
            Hairline(_cogKeyRt, new Vector2(0, 0), new Vector2(1, 0), new Color(0f, 0f, 0f, 0.55f));
            HairlineV(_cogKeyRt, 0f, UITheme.Night[3]);
            HairlineV(_cogKeyRt, 1f, new Color(0f, 0f, 0f, 0.55f));
            var cogMark = _cogKeyRt.Find("Face/Mark") as RectTransform;
            if (cogMark != null)
            {
                // exactly 32 - the 16 px mark at 2x - centred, lifted off the key's throw like every other mark
                cogMark.anchorMin = cogMark.anchorMax = new Vector2(0.5f, 0.5f);
                cogMark.sizeDelta = new Vector2(32f, 32f);
                cogMark.anchoredPosition = new Vector2(0f, KeyPlate.Throw * 0.5f);
                var mi = cogMark.GetComponent<Image>();
                if (mi != null) mi.color = UITheme.Amber[4];
                // ...AND NOW THE MENUS' BRASS COG (2026-09-26, the author: "Ana sahnede üst bardaki ayarlar butonunun
                // iconunu da aynı icon sanat diliyle üret"): the icon family the ESC and settings keys wear, 32x32 at
                // 1x and never tinted; the flat mark stays for a build without it.
                var brass = MenuPack.Art("mi_settings");
                if (mi != null && brass != null) { mi.sprite = brass; mi.color = Color.white; }
            }
            HoverTip(_cogKeyRt, cog, UIText.T("build.settings.tip_title"), UIText.T("build.settings.tip"));

            BuildMusicPlayer(top);
            LayTopBar();
        }

        // ── the hour ──────────────────────────────────────────────────────────────────────────────────────────────

        private void BuildHourWell(RectTransform top)
        {
            _hourWell = NewRect("Clock", top);
            Place(_hourWell, new Vector2(0, 0.5f), new Vector2(360f, TopWellH), new Vector2(TopEdge, 0));
            var img = _hourWell.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.Well();
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;

            var digits = NewRect("Digits", _hourWell);
            Place(digits, new Vector2(0, 0.5f), new Vector2(TopHourDigitsW, 28), new Vector2(TopWellPad, 0));
            _clock = new SegmentClock(digits, UITheme.Cyan[4]);

            _hourRuleA = HourRule("RuleA");
            _hourDayBlock = NewRect("Night", _hourWell);
            Place(_hourDayBlock, new Vector2(0, 0.5f), new Vector2(TopHourDayW, TopWellH), Vector2.zero);
            _hourDayBlock.pivot = new Vector2(0, 0.5f);
            _hourDayCap = NewText("Cap", _hourDayBlock, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
            Place(_hourDayCap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(TopHourDayW + 12f, 12), new Vector2(0, 7f));
            _hourDayCap.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hourDayCap.text = UIText.T("hud.day_well.caption");
            _dayLabel = NewText("Day", _hourDayBlock, _figures, 16, TextAnchor.MiddleCenter, UITheme.Cyan[3]);
            Place(_dayLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(TopHourDayW + 12f, 18), new Vector2(0, -7f));
            _dayLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hourRuleB = HourRule("RuleB");

            _nightLabel = NewText("NightName", _hourWell, _body, 16, TextAnchor.MiddleLeft, UITheme.Amber[4]);
            Place(_nightLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(160, 18), new Vector2(0, 6f));
            _nightLabel.rectTransform.pivot = new Vector2(0, 0.5f);
            _nightLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            // Tonight's crowd rides under the night's name: it is a fact about the night.
            _crowdText = NewText("Crowd", _hourWell, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            Place(_crowdText.rectTransform, new Vector2(0, 0.5f), new Vector2(160, 12), new Vector2(0, -8f));
            _crowdText.rectTransform.pivot = new Vector2(0, 0.5f);
            _crowdText.horizontalOverflow = HorizontalWrapMode.Overflow;

            HoverTip(_hourWell, ItemArt.Star(true, 16f), UIText.T("hud.day_well.tip_title"), () =>
            {
                var r = Run;
                if (r == null) return "";
                return UIText.T("hud.day_well.tip", ("day", r.Day), ("week", BarCalendar.WeekOf(r.Day)),
                    ("night", UIText.Caps(UIText.T(BarCalendar.NameLine(BarCalendar.NightOf(r.Day))))));
            });
        }

        private RectTransform HourRule(string name)
        {
            var rule = NewRect(name, _hourWell);
            Place(rule, new Vector2(0, 0.5f), new Vector2(1, 22), Vector2.zero);
            rule.pivot = new Vector2(0, 0.5f);
            var ri = rule.gameObject.AddComponent<Image>();
            ri.color = new Color(UITheme.Cyan[4].r, UITheme.Cyan[4].g, UITheme.Cyan[4].b, 0.22f);
            ri.raycastTarget = false;
            return rule;
        }

        // ── the till ──────────────────────────────────────────────────────────────────────────────────────────────

        private void BuildTillWell(RectTransform top)
        {
            // THE TILL ON THE BEAM, AS A TILL (the eighth list: "para göstergesinin yerini üst panele alalım ve tarzını
            // değiştirelim"): a register's own readout - the clock's seven bars, dollar and all, in the green of money -
            // standing in the beam beside the standing, where the plum card with a coin used to hang over the room.
            _tillWell = NewRect("TillCard", top);
            Place(_tillWell, new Vector2(1, 0.5f), new Vector2(SegmentFigure.Width + TopWellPad * 2f, TopWellH), Vector2.zero);
            _tillWell.pivot = new Vector2(1, 0.5f);
            var img = _tillWell.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.Well();
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            var host = NewRect("Figure", _tillWell);
            Place(host, new Vector2(0, 0.5f), new Vector2(SegmentFigure.Width, 32), new Vector2(TopWellPad, 0));
            host.pivot = new Vector2(0, 0.5f);
            _tillFigure = new SegmentFigure(host, UITheme.Lime[4]);
            _tillFigure.Show(0);
            _beamTillCard = _tillWell;
            _beamTillText = null;
            HoverTip(_tillWell, ItemArt.Coin(16f), UIText.T("build.till.tip_title"), () =>
            {
                var r = Run;
                return r == null ? "" : UIText.T("build.till.tip", ("money", "$" + r.Money));
            });
        }

        /// <summary>The till's readout, once a frame from RunTheTill: repainted only when the figure moves.</summary>
        private void ShowTillFigure(int shown)
        {
            if (_tillFigure == null || shown == _tillFigureShown) return;
            _tillFigureShown = shown;
            _tillFigure.SetHue(shown < 0 ? UITheme.ViceRed[3] : UITheme.Lime[4]);
            _tillFigure.Show(shown);
        }

        // ── the standing ──────────────────────────────────────────────────────────────────────────────────────────

        private void BuildRateWell(RectTransform top)
        {
            _rateWell = NewRect("RatingWell", top);
            Place(_rateWell, new Vector2(1, 0.5f), new Vector2(300f, TopWellH), Vector2.zero);
            _rateWell.pivot = new Vector2(1, 0.5f);
            var wellImg = _rateWell.gameObject.AddComponent<Image>();
            wellImg.sprite = ChromeArt.Well();
            wellImg.type = Image.Type.Sliced;
            wellImg.color = Color.white;
            wellImg.raycastTarget = true;

            // THE FIVE STARS, A SIZE DOWN: the small star at exactly 2x, a pitch of 30 - where the big one stood 36 high
            // at 38 apart and the standing was the loudest thing on the beam.
            float starsW = _ratingStars.Length * TopStarPitch;
            _rateStars = NewRect("Standing", _rateWell);
            Place(_rateStars, new Vector2(1, 0.5f), new Vector2(starsW, TopWellH), new Vector2(-TopWellPad, 0));
            _rateStars.pivot = new Vector2(1, 0.5f);
            var starsRow = NewRect("Stars", _rateStars);
            Place(starsRow, new Vector2(0, 0.5f), new Vector2(starsW, TopStarH), Vector2.zero);
            starsRow.pivot = new Vector2(0, 0.5f);
            var starsCatch = starsRow.gameObject.AddComponent<Image>();
            starsCatch.color = new Color(0f, 0f, 0f, 0.004f);
            starsCatch.raycastTarget = true;
            var starsBtn = starsRow.gameObject.AddComponent<Button>();
            starsBtn.transition = Selectable.Transition.None;
            starsBtn.onClick.AddListener(OpenLadderFromTheBeam);
            // A RUNG NOBODY HAS LOOKED AT (2026-09-22, the eighth... ninth list): the word NEW was a word in one
            // language on a beam that speaks twenty-nine; it is the house's notification mark now, in the beam's own
            // magenta, hung off the star row's corner and pointing at it.
            _ladderNewFlag = NewRect("New", starsRow);
            Place(_ladderNewFlag, new Vector2(1f, 1f), new Vector2(16f, 16f), new Vector2(8f, 12f));
            var flagImg = _ladderNewFlag.gameObject.AddComponent<Image>();
            flagImg.sprite = ChromeArt.Notice();
            flagImg.color = UITheme.Magenta[3]; flagImg.raycastTarget = false;
            _ladderNewFlag.gameObject.SetActive(false);
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                var star = NewRect($"B{i}", starsRow);
                star.anchorMin = star.anchorMax = new Vector2(0, 0.5f);
                star.pivot = new Vector2(0.5f, 0.5f);
                star.sizeDelta = new Vector2(TopStarW, TopStarH);
                star.anchoredPosition = new Vector2(i * TopStarPitch + TopStarPitch * 0.5f, 0);
                var bi = star.gameObject.AddComponent<Image>();
                bi.sprite = ItemArt.Star(false, 16f);
                bi.raycastTarget = false;
            }
            _starsFill = NewRect("Fill", starsRow);
            _starsFill.anchorMin = new Vector2(0, 0); _starsFill.anchorMax = new Vector2(0, 1);
            _starsFill.pivot = new Vector2(0, 0.5f);
            _starsFill.sizeDelta = Vector2.zero;
            _starsFill.anchoredPosition = Vector2.zero;
            _starsFill.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                var star = NewRect($"F{i}", _starsFill);
                star.anchorMin = star.anchorMax = new Vector2(0, 0.5f);   // fixed x: the mask slides over it
                star.pivot = new Vector2(0.5f, 0.5f);
                star.sizeDelta = new Vector2(TopStarW, TopStarH);
                star.anchoredPosition = new Vector2(i * TopStarPitch + TopStarPitch * 0.5f, 0);
                var fi = star.gameObject.AddComponent<Image>();
                fi.sprite = ItemArt.Star(true, 16f);
                fi.raycastTarget = false;
                _ratingStars[i] = fi;
            }

            _rateHouse = NewRect("House", _rateWell);
            Place(_rateHouse, new Vector2(1, 0.5f), new Vector2(HouseStripW, TopWellH), Vector2.zero);
            _rateHouse.pivot = new Vector2(1, 0.5f);
            _serviceFill = IconStrip(_rateHouse, "Service", ItemArt.Heart(false, 16f), ItemArt.Heart(true, 16f), -9f);
            _comfortFill = IconStrip(_rateHouse, "Comfort", ItemArt.Medal(false, 16f), ItemArt.Medal(true, 16f), 9f);
            StripCaption(_rateHouse, "SERVICE", UIText.T("build.house.service"), -9f);
            StripCaption(_rateHouse, "COMFORT", UIText.T("build.house.comfort"), 9f);
            _rateCapService = _rateHouse.Find("Cap_SERVICE")?.GetComponent<Text>();
            _rateCapComfort = _rateHouse.Find("Cap_COMFORT")?.GetComponent<Text>();
            HoverTip(_rateHouse.Find("Service") as RectTransform, ItemArt.Heart(true, 16f),
                UIText.T("build.house.service.tip_title"), () =>
                {
                    var r = Run;
                    return r == null ? UIText.T("build.house.service.tip_idle")
                        : UIText.T("build.house.service.tip", ("rating", r.ServiceTonight.ToString("0.0")));
                });
            HoverTip(_rateHouse.Find("Comfort") as RectTransform, ItemArt.Medal(true, 16f),
                UIText.T("build.house.comfort.tip_title"), () =>
                {
                    var r = Run;
                    return r == null ? UIText.T("build.house.comfort.tip_idle")
                        // Two decimals (2026-09-26): a piece adds +0.02 to +0.19 of a house that sums to five.
                        : UIText.T("build.house.comfort.tip", ("rating", r.ComfortNow.ToString("0.00")));
                });
            string[] standingTip = UIText.T("build.standing.tip").Split(new[] { '\n' }, 2);
            HoverTip(starsRow, ItemArt.Star(true, 16f), standingTip[0], () =>
            {
                var r = Run;
                if (r == null) return standingTip.Length > 1 ? standingTip[1] : "";
                double lower = System.Math.Min(r.ServiceTonight, r.ComfortNow);
                return UIText.T("build.standing.tip_reading",
                    ("service", r.ServiceTonight.ToString("0.0")),
                    ("comfort", r.ComfortNow.ToString("0.00")),
                    ("tonight", lower.ToString("0.0")));
            });
        }

        // ── the week's job, as a tab under the beam (TycoonHud.Chrome builds and refreshes it) ────────────────────────

        private const float JobTabH = 38f, JobFace = 26f, JobTextX = 44f, JobPipW = 12f, JobPipH = 6f, JobPipGap = 3f;
        private Image _jobFace, _jobLip;
        private RectTransform _jobPips;
        private int _jobPipsFor = -1;

        /// <summary>A pip for each of what the job asks, lit for each done - lime once it is paid, the beam's magenta
        /// until then - redrawn only when the count changes.</summary>
        private void LayJobPips(WeeklyJob job)
        {
            if (_jobPips == null || job == null) return;
            int sig = job.Target * 1000 + job.Served * 10 + (job.IsDone ? 1 : 0);
            if (sig == _jobPipsFor) return;
            _jobPipsFor = sig;
            foreach (Transform old in _jobPips) Destroy(old.gameObject);
            for (int i = 0; i < job.Target; i++)
            {
                var pip = NewRect("P" + i, _jobPips);
                Place(pip, new Vector2(0, 0.5f), new Vector2(JobPipW, JobPipH), new Vector2(i * (JobPipW + JobPipGap), 0f));
                pip.pivot = new Vector2(0, 0.5f);
                var pi = pip.gameObject.AddComponent<Image>();
                bool lit = i < job.Served;
                pi.color = !lit ? new Color(UITheme.Cream[1].r, UITheme.Cream[1].g, UITheme.Cream[1].b, 0.35f)
                         : job.IsDone ? UITheme.Lime[3] : UITheme.Magenta[3];
                pi.raycastTarget = false;
            }
        }

        /// <summary>How far a lit share of the five stars reaches, for the fill's mask.</summary>
        private float TopStarsReach(double stars) => (float)(stars / 5.0) * _ratingStars.Length * TopStarPitch;

        // ── the pass ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Stands the beam's wells edge to edge at their words' own widths. Cheap, and asked each refresh: it does
        /// nothing unless something it measures has changed - the night's name, its crowd, the language.
        /// </summary>
        private void LayTopBar()
        {
            if (_hourWell == null) return;
            string sig = (_nightLabel != null ? _nightLabel.text : "") + "|" + (_crowdText != null ? _crowdText.text : "")
                       + "|" + (_hourDayCap != null ? _hourDayCap.text : "") + "|" + Localization.Current.Code;
            if (sig == _topBarSig) return;
            _topBarSig = sig;

            // THE HOUR: digits, a rule, the night's number, a rule, the night's name - each as wide as it reads
            float x = TopWellPad + TopHourDigitsW + 10f;
            _hourRuleA.anchoredPosition = new Vector2(x, 0f);
            x += 11f;
            float dayW = Mathf.Max(TopHourDayW, _hourDayCap != null ? _hourDayCap.preferredWidth + 4f : 0f);
            _hourDayBlock.sizeDelta = new Vector2(dayW, TopWellH);
            _hourDayBlock.anchoredPosition = new Vector2(x, 0f);
            x += dayW + 10f;
            _hourRuleB.anchoredPosition = new Vector2(x, 0f);
            x += 11f;
            float textW = Mathf.Max(_nightLabel != null ? _nightLabel.preferredWidth : 0f,
                                    _crowdText != null ? _crowdText.preferredWidth : 0f);
            _nightLabel.rectTransform.anchoredPosition = new Vector2(x, 6f);
            _crowdText.rectTransform.anchoredPosition = new Vector2(x, -8f);
            float hourW = x + Mathf.Max(40f, textW) + TopWellPad;
            _hourWell.sizeDelta = new Vector2(hourW, TopWellH);

            // THE STANDING: the captions as wide as they read, the strips, the stars
            float capW = Mathf.Max(_rateCapService != null ? _rateCapService.preferredWidth : 0f,
                                   _rateCapComfort != null ? _rateCapComfort.preferredWidth : 0f);
            float starsW = _ratingStars.Length * TopStarPitch;
            float houseRight = -(TopWellPad + starsW + 14f);
            _rateHouse.anchoredPosition = new Vector2(houseRight, 0f);
            float rateW = TopWellPad + capW + 8f + HouseStripW + 14f + starsW + TopWellPad;
            _rateWell.sizeDelta = new Vector2(rateW, TopWellH);

            // right to left from the key
            float right = -TopEdge - TopWellH - TopGap;
            _rateWell.anchoredPosition = new Vector2(right, 0f);
            right -= rateW + TopGap;
            _tillWell.anchoredPosition = new Vector2(right, 0f);
            right -= _tillWell.sizeDelta.x + TopGap;

            // THE PLAYER takes what is left in the middle, centred in it and never wider than it
            if (_playerWell != null)
            {
                var bar = (RectTransform)_hourWell.parent;
                float barW = bar.rect.width > 1f ? bar.rect.width : 1280f;
                float from = TopEdge + hourW + TopGap;           // from the bar's left
                float to = barW + right;                          // the till's left, from the bar's left
                float room = Mathf.Max(200f, to - from);
                float w = Mathf.Min(PlayerW, room);
                FitMusicPlayer(w);
                _playerWell.anchoredPosition = new Vector2((from + to) * 0.5f - barW * 0.5f, 0f);
            }
        }
    }
}
