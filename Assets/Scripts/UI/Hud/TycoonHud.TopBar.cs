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
        // ── THE REGISTER UNDER THE NEON (2026-09-28, first pass) ─────────────────────────────────────────────────────
        //
        // The beam was rebuilt the same morning around the one question that can end a run - will tonight's till cover
        // tonight's bill, and how many red nights are already on the books? - as a REGISTER in the middle of the bar:
        // the till in seven segment cells, the BILL the close would take and three red LAMPS counting the nights closed
        // under $0; the licence (the house's two strips and the stars) on the right, a JUKEBOX key and the neon cog.
        //
        // ── THE BEAM, SIMPLER (2026-09-28, second pass) ──────────────────────────────────────────────────────────────
        //
        // The author saw it and said: "Üst bar hala istediğim gibi değil, sadeleştirelim çok şey var şu an üst barda.
        // butonları kaldır sadece yıldız gözüksün konfor ve servis yıldızın üstünde hover ile gözüksün. para göstergesi
        // saatle aynı olmasın." - and asked "red nights ne?". Three wells are left on the beam and nothing in its middle:
        //   - the CLOCK on the left, exactly as it was (the segment digits, the night's number, its name and its crowd);
        //   - on the right, the STARS alone - the only rating on the bar; the house's two readings, COMFORT and SERVICE,
        //     hang under them on a card while the pointer is on them, and a press still opens the ladder;
        //   - left of them the TILL, in the house's figures face at 24 beside the drawn stack of bills - money, not a
        //     second clock. What the register said in lamps and a bill its card says in words: the money, tonight's
        //     bill, what a red night is and how many are already on the books.
        // The keys went: the jukebox (the music keeps its N and M keys and Settings → AUDIO's own player) and the cog
        // (Escape opens the pause menu, whose SETTINGS key is the way in, and the front door has its own). The tube at
        // the foot stays the ONE state light - amber, magenta at last call, red in debt, out at the game over.

        private const float TopEdge = 16f, TopGap = 8f, TopWellH = 42f;
        /// <summary>The standing's star: the small drawing (14x12) at exactly 2x. The big one at 36 was a headline.</summary>
        private const float TopStarW = 28f, TopStarH = 24f, TopStarPitch = 30f;
        private const float TopHourDigitsW = 110f, TopHourDayW = 44f, TopWellPad = 12f;
        /// <summary>The beam's two lines inside a 42 well (GDD 16 §1): what a reading IS centred 20 down the bar, 7 over
        /// the well's middle, and what it SAYS centred 34 down, 7 under it. The night's block has always sat on them.</summary>
        private const float TopCapY = 7f, TopReadY = -7f;
        /// <summary>The standing's well, one width in every language: a pad, the five stars, a pad (174).</summary>
        private const float StarsWellW = TopWellPad + BarRating.MaxStars * TopStarPitch + TopWellPad;
        /// <summary>The till: the house figures face at 24, and the money mark drawn at 24 shown at 1x beside it.</summary>
        private const int MoneyFigurePx = 24;
        private const float MoneyMarkPx = 24f;
        /// <summary>How many figures the till's glass is cut for: six - $999999, or -$99999 in the red. A seventh widens
        /// it once (ShowTillFigure) rather than running out of it.</summary>
        private const int MoneyGlyphsMin = 6;

        private RectTransform _hourWell, _hourRuleA, _hourDayBlock, _hourRuleB, _moneyWell, _starsWell;
        private Text _hourDayCap, _moneyFigure;
        /// <summary>The five unlit stars under the fill: the wave lifts a socket with the lit star over it.</summary>
        private readonly RectTransform[] _starSockets = new RectTransform[BarRating.MaxStars];
        private int _tillFigureShown = int.MinValue;
        private int _moneyGlyphs = MoneyGlyphsMin;

        // The words' key (RefreshTopBar): the night, its crowd, last call and the language, as one int - so the name
        // and the crowd are written, and the beam re-laid, only when one of them changes, and not built as a string
        // every frame to find that out.
        private int _topBarKey;
        private bool _topBarKeyed;

        private void BuildTopBarWells(RectTransform top)
        {
            BuildHourWell(top);
            BuildStarsWell(top);
            BuildMoneyWell(top);
            BuildBeamCard(top.parent as RectTransform);
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

            _hourRuleA = HourRule(_hourWell, "RuleA");
            _hourDayBlock = NewRect("Night", _hourWell);
            Place(_hourDayBlock, new Vector2(0, 0.5f), new Vector2(TopHourDayW, TopWellH), Vector2.zero);
            _hourDayBlock.pivot = new Vector2(0, 0.5f);
            _hourDayCap = NewText("Cap", _hourDayBlock, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
            Place(_hourDayCap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(TopHourDayW + 12f, 12), new Vector2(0, TopCapY));
            _hourDayCap.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hourDayCap.text = UIText.T("hud.day_well.caption");
            _dayLabel = NewText("Day", _hourDayBlock, _figures, 16, TextAnchor.MiddleCenter, UITheme.Cyan[3]);
            Place(_dayLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(TopHourDayW + 12f, 18), new Vector2(0, TopReadY));
            _dayLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hourRuleB = HourRule(_hourWell, "RuleB");

            _nightLabel = NewText("NightName", _hourWell, _body, 16, TextAnchor.MiddleLeft, UITheme.Amber[4]);
            Place(_nightLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(160, 18), new Vector2(0, 6f));
            _nightLabel.rectTransform.pivot = new Vector2(0, 0.5f);
            _nightLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            // Tonight's crowd rides under the night's name: it is a fact about the night.
            _crowdText = NewText("Crowd", _hourWell, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            Place(_crowdText.rectTransform, new Vector2(0, 0.5f), new Vector2(160, 12), new Vector2(0, -8f));
            _crowdText.rectTransform.pivot = new Vector2(0, 0.5f);
            _crowdText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // The clock's own mark on its tip (2026-09-28): it wore the standing's star, which is the licence's symbol.
            HoverTip(_hourWell, ChromeArt.Mark("clock"), UIText.T("hud.day_well.tip_title"), () =>
            {
                var r = Run;
                if (r == null) return "";
                return UIText.T("hud.day_well.tip", ("day", r.Day), ("week", BarCalendar.WeekOf(r.Day)),
                    ("night", UIText.Caps(UIText.T(BarCalendar.NameLine(BarCalendar.NightOf(r.Day))))));
            });
        }

        /// <summary>A display rule between two readings of one well: 1x22, the clock's cyan at a quarter.</summary>
        private RectTransform HourRule(RectTransform well, string name)
        {
            var rule = NewRect(name, well);
            Place(rule, new Vector2(0, 0.5f), new Vector2(1, 22), Vector2.zero);
            rule.pivot = new Vector2(0, 0.5f);
            var ri = rule.gameObject.AddComponent<Image>();
            ri.color = new Color(UITheme.Cyan[4].r, UITheme.Cyan[4].g, UITheme.Cyan[4].b, 0.22f);
            ri.raycastTarget = false;
            return rule;
        }

        // ── the standing ──────────────────────────────────────────────────────────────────────────────────────────

        private void BuildStarsWell(RectTransform top)
        {
            // THE STARS ALONE (2026-09-28, second pass: "sadece yıldız gözüksün konfor ve servis yıldızın üstünde hover
            // ile gözüksün"). The licence's two strips left the beam for the card that hangs under the stars while the
            // pointer is on them (BeamCard below); the well is the five stars and their pads, at the bar's right edge
            // where the cog stood, one width in every language.
            _starsWell = NewRect("Standing", top);
            Place(_starsWell, new Vector2(1, 0.5f), new Vector2(StarsWellW, TopWellH), new Vector2(-TopEdge, 0));
            var wellImg = _starsWell.gameObject.AddComponent<Image>();
            wellImg.sprite = ChromeArt.Well();
            wellImg.type = Image.Type.Sliced;
            wellImg.color = Color.white;
            wellImg.raycastTarget = true;
            // The whole well is the ladder's door now that the stars are all it holds - the 24-unit row was a thin target
            // on a 42 well.
            var starsBtn = _starsWell.gameObject.AddComponent<Button>();
            starsBtn.transition = Selectable.Transition.None;
            starsBtn.onClick.AddListener(OpenLadderFromTheBeam);

            // THE FIVE STARS, A SIZE DOWN: the small star at exactly 2x, a pitch of 30 - where the big one stood 36 high
            // at 38 apart and the standing was the loudest thing on the beam.
            float starsW = _ratingStars.Length * TopStarPitch;
            var starsRow = NewRect("Stars", _starsWell);
            Place(starsRow, new Vector2(0, 0.5f), new Vector2(starsW, TopStarH), new Vector2(TopWellPad, 0));
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
                _starSockets[i] = star;
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

            HoverBeamCard(_starsWell, BeamFace.House);
        }

        /// <summary>How far a lit share of the five stars reaches, for the fill's mask.</summary>
        private float TopStarsReach(double stars) => (float)(stars / 5.0) * _ratingStars.Length * TopStarPitch;

        // ── the till ──────────────────────────────────────────────────────────────────────────────────────────────

        private void BuildMoneyWell(RectTransform top)
        {
            // NOT THE CLOCK'S HAND (2026-09-28, second pass: "para göstergesi saatle aynı olmasın"). The till wore the
            // clock's seven bars in the green of money, so the beam carried two segment readouts that read as one
            // instrument twice. It is the house's figures face now - the arcade digits the market's tablet prints its
            // account in - at 24, beside the drawn stack of bills at its own 24: a sum of money, not a machine's
            // display. Green, red under zero, as it always was. The glass is cut for the widest till it will show
            // (LayMoneyWell), so the figure never jumps while it counts and never runs out of its well.
            _moneyWell = NewRect("Till", top);
            Place(_moneyWell, new Vector2(1, 0.5f), new Vector2(200f, TopWellH),
                new Vector2(-(TopEdge + StarsWellW + TopGap), 0));
            var img = _moneyWell.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.Well();
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;

            // The figure is the money flights' target and what their punch swells, so it is pivoted on its own middle;
            // right-aligned, because CoinFigure stands the mark off the digits' left edge by measuring them.
            _moneyFigure = NewText("Figure", _moneyWell, _figures, MoneyFigurePx, TextAnchor.MiddleRight, UITheme.Lime[4]);
            var fig = _moneyFigure.rectTransform;
            fig.anchorMin = fig.anchorMax = new Vector2(1f, 0.5f);
            fig.pivot = new Vector2(0.5f, 0.5f);
            _moneyFigure.horizontalOverflow = HorizontalWrapMode.Overflow;
            _moneyFigure.verticalOverflow = VerticalWrapMode.Overflow;
            _moneyFigure.text = "";
            _beamTillCard = fig;
            LayMoneyWell();

            HoverBeamCard(_moneyWell, BeamFace.Till);
        }

        /// <summary>Cuts the till's glass: the mark, its gap, and as many figures as the till is reserved for, measured
        /// in the face itself (a figure face keeps one advance for every digit and the minus) - on the 4 grid.</summary>
        private void LayMoneyWell()
        {
            if (_moneyWell == null || _moneyFigure == null) return;
            var settings = _moneyFigure.GetGenerationSettings(Vector2.zero);
            float digitsW = _moneyFigure.cachedTextGeneratorForLayout.GetPreferredWidth(
                new string('0', _moneyGlyphs), settings) / _moneyFigure.pixelsPerUnit;
            float figW = Ceil4(MoneyMarkPx + CoinGap + digitsW);   // 24 + 6 + 144 = 174 -> 176 in the house face
            var fig = _moneyFigure.rectTransform;
            fig.sizeDelta = new Vector2(figW, MoneyFigurePx);
            fig.anchoredPosition = new Vector2(-(TopWellPad + figW * 0.5f), 0f);
            _moneyWell.sizeDelta = new Vector2(TopWellPad + figW + TopWellPad, TopWellH);
        }

        /// <summary>The till's readout, once a frame from RunTheTill: repainted only when the figure moves.</summary>
        private void ShowTillFigure(int shown)
        {
            if (_moneyFigure == null || shown == _tillFigureShown) return;
            _tillFigureShown = shown;
            _moneyFigure.color = shown < 0 ? UITheme.ViceRed[3] : UITheme.Lime[4];
            // What the bar HAS wears the stack of bills (CoinFigure's account mark), drawn at 24 beside a figure of 24.
            CoinFigure(_moneyFigure, shown, shown < 0 ? "-" : "", account: true);
            // A till past what the glass was cut for (a seventh figure) widens it once, rather than running out of it.
            if (_moneyFigure.text.Length > _moneyGlyphs)
            {
                _moneyGlyphs = _moneyFigure.text.Length;
                LayMoneyWell();
            }
        }

        // ── the card under the beam ─────────────────────────────────────────────────────────────────────────────────
        //
        // WHAT THE BEAM NO LONGER SAYS, UNDER THE POINTER (2026-09-28, second pass). The house's two readings and the
        // register's warnings left the bar; they hang under it on one card when the pointer asks. The stars' card
        // carries the standing's own sentence with tonight's reading under it, then the house's two strips, each
        // captioned in its word: COMFORT (the medallions, the room right now) over SERVICE (the hearts, at their own
        // 12x12, tonight's drinks). The till's card says in words what the register said in lamps: the money, the
        // bill the close will take, what a red night IS - the author's "red nights ne?" - and how many are already on
        // the books, with a red line while closing now would add one. Every figure is Core's; the card writes words.
        //
        // ONE CARD, built once and filled for whichever well raised it, on the plate the room's tips stand on. It is a
        // card and not the one-line tip because it has rows - strips, a sentence and its reading, four lines of money
        // - and every row of it sits on a whole unit. Its own canvas at 29 puts it over the pinned recipe note (28),
        // which hangs in the very corner the stars live in, and over the bench (25), whose veil stops at the bar; the
        // pause, the settings, the ladder, the recipe book, the night's books, the game over and the front door take it
        // down while they are up (BeamCardBlocked), so it never shows through them. It writes its words only when what
        // they say moves.

        private enum BeamFace { House, Till }

        private const int BeamCardSortingOrder = 29, BeamCardLines = 5;
        private const float CardIconX = 10f, CardTextX = 32f, CardTitleTop = 6f, CardRowH = 16f, CardLinesTop = 24f,
            CardLineH = 12f, CardPadR = 10f, CardPadB = 8f, CardStripsGap = 6f, CardCapGap = 8f, CardHang = 6f,
            CardMinW = 120f, CardEdge = 8f;
        /// <summary>The strips' block: two 16 rows on the 18 pitch, centred 9 over and 9 under its middle.</summary>
        private const float CardStripsH = HouseGap + HouseIcon;

        private RectTransform _beamCard, _beamCardOver, _beamCardHouse;
        private CanvasGroup _beamCardGroup;
        private Image _beamCardIcon;
        private Text _beamCardTitle, _beamCardComfortWord, _beamCardServiceWord;
        private readonly Text[] _beamCardLines = new Text[BeamCardLines];
        private BeamFace _beamCardFace;
        // What the card last wrote from: it writes its words when one of these moves, never every frame.
        private bool _beamCardKeyed;
        private int _beamCardK0, _beamCardK1, _beamCardK2, _beamCardK3;
        private static readonly Vector3[] BeamCardCorners = new Vector3[4];

        private void BuildBeamCard(RectTransform root)
        {
            if (root == null) return;
            _beamCard = NewRect("BeamCard", root);
            _beamCard.anchorMin = _beamCard.anchorMax = new Vector2(0.5f, 1f);
            _beamCard.pivot = new Vector2(0.5f, 1f);      // hung by its head: it grows out of the well above it
            _beamCard.sizeDelta = new Vector2(CardMinW, 84f);
            var canvas = _beamCard.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = BeamCardSortingOrder;
            // THE ROOM'S TIP PLATE (ShowPropTip's): ui_blue as the author drew it, at its own size, with the scanlines.
            var plate = _beamCard.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.BluePlate() ?? NightArt.TipPlate();
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 1f;
            plate.color = Color.white;
            plate.raycastTarget = false;
            Scanlines(_beamCard, 0.18f);
            _beamCardGroup = _beamCard.gameObject.AddComponent<CanvasGroup>();
            _beamCardGroup.alpha = 0f;
            _beamCardGroup.blocksRaycasts = false;
            _beamCardGroup.interactable = false;

            var icon = NewRect("Icon", _beamCard);
            CardRect(icon, CardIconX, CardTitleTop, 16f, 16f);
            _beamCardIcon = icon.gameObject.AddComponent<Image>();
            _beamCardIcon.preserveAspect = true;
            _beamCardIcon.raycastTarget = false;
            _beamCardTitle = CardText("Title", _display, UITheme.Cream[4], CardTitleTop, CardRowH);
            for (int i = 0; i < BeamCardLines; i++)
                _beamCardLines[i] = CardText("Line" + i, _body, UITheme.Cream[3], CardLinesTop + i * CardLineH, CardLineH);

            // COMFORT over SERVICE, as the licence stacked them: the medallion at 16, the heart at its own 12 (1x) on
            // the same centres - the strips the beam carried, one door further in.
            _beamCardHouse = NewRect("House", _beamCard);
            CardRect(_beamCardHouse, CardTextX, CardLinesTop + CardLineH + CardStripsGap, HouseStripW, CardStripsH);
            var heartLit = ItemArt.Heart(true, 16f);
            float heartCell = heartLit != null ? heartLit.rect.width : HouseIcon;
            _comfortFill = IconStrip(_beamCardHouse, "Comfort", ItemArt.Medal(false, 16f), ItemArt.Medal(true, 16f),
                HouseGap * 0.5f);
            _serviceFill = IconStrip(_beamCardHouse, "Service", ItemArt.Heart(false, 16f), heartLit,
                -HouseGap * 0.5f, heartCell);
            _beamCardComfortWord = StripWord("ComfortWord", HouseGap * 0.5f);
            _beamCardServiceWord = StripWord("ServiceWord", -HouseGap * 0.5f);

            _beamCard.gameObject.SetActive(false);
        }

        /// <summary>A card row's words: one line, left on the text column, as wide as LayBeamCard measures them.</summary>
        private Text CardText(string name, Font font, Color ink, float top, float h)
        {
            var t = NewText(name, _beamCard, font, 8, TextAnchor.MiddleLeft, ink);
            CardRect(t.rectTransform, CardTextX, top, CardMinW, h);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// <summary>A strip's word, beside its strip on the strip's own centre line.</summary>
        private Text StripWord(string name, float y)
        {
            var t = NewText(name, _beamCardHouse, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(64f, CardLineH), new Vector2(HouseStripW + CardCapGap, y));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// <summary>A rect hung from the card's top-left corner, <paramref name="top"/> down and <paramref name="x"/> in.</summary>
        private static void CardRect(RectTransform rt, float x, float top, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -top);
        }

        /// <summary>The pointer on a well raises the card with that well's face; leaving it lets the card go - only the
        /// well that raised it may, so two wells side by side cannot trade it away.</summary>
        private void HoverBeamCard(RectTransform over, BeamFace face)
        {
            var relay = over.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () => ShowBeamCard(over, face);
            relay.Exited = () => { if (_beamCardOver == over) _beamCardOver = null; };
        }

        private void ShowBeamCard(RectTransform over, BeamFace face)
        {
            if (_beamCard == null || over == null) return;
            _beamCardOver = over;
            _beamCardFace = face;
            _beamCardKeyed = false;          // this face's words are written on the step that raises it
            DressBeamCard();
        }

        /// <summary>What a face wears that does not move while it is up: its mark, its title, its strips' words.</summary>
        private void DressBeamCard()
        {
            bool house = _beamCardFace == BeamFace.House;
            var icon = house ? ItemArt.Star(true, 16f) : ItemArt.Money(16f);
            _beamCardIcon.sprite = icon;
            _beamCardIcon.enabled = icon != null;
            if (icon != null)
            {
                // at the size it is drawn (the small star is 14x12), centred in the title row's 16 on whole units
                float w = Mathf.Min(16f, icon.rect.width), h = Mathf.Min(16f, icon.rect.height);
                CardRect(_beamCardIcon.rectTransform, CardIconX + Mathf.Floor((16f - w) * 0.5f),
                    CardTitleTop + Mathf.Floor((16f - h) * 0.5f), w, h);
            }
            if (house)
            {
                // ONE WORD FOR A TITLE (2026-09-28, measured in play): the one-line tip's sentence "STANDING - A STEP A
                // NIGHT TOWARD THE LOWER OF ..." ran the card past the frame's right edge, where the stars stand. The card
                // says the rest in its own rows - tonight's reading and the two strips.
                _beamCardTitle.text = UIText.T("build.standing.card_title");
                _beamCardComfortWord.text = UIText.T("build.house.comfort");
                _beamCardServiceWord.text = UIText.T("build.house.service");
            }
            else _beamCardTitle.text = UIText.T("build.till.tip_title");
            if (_beamCardHouse.gameObject.activeSelf != house) _beamCardHouse.gameObject.SetActive(house);
        }

        /// <summary>
        /// Once a frame after the room's tip (Update): raises the card over the well the pointer is on, fades it out of
        /// the one it left, and while it is up keeps its strips and its words on Core's figures - growing out of the
        /// well and shrinking back into it the way every hover in the game does (2026-09-22).
        /// </summary>
        private void StepBeamCard()
        {
            if (_beamCard == null) return;
            var run = Run;
            var over = _beamCardOver;
            bool up = over != null && run != null && over.gameObject.activeInHierarchy && !BeamCardBlocked();
            bool shown = _beamCard.gameObject.activeSelf;
            if (!up && !shown) return;
            if (up && !shown)
            {
                _beamCard.gameObject.SetActive(true);
                _beamCardGroup.alpha = 0f;
                _beamCardKeyed = false;
            }
            float want = up ? 1f : 0f;
            _beamCardGroup.alpha = Motion.Reduced ? want
                : Mathf.MoveTowards(_beamCardGroup.alpha, want, Time.unscaledDeltaTime / PropTipFade);
            if (!up && _beamCardGroup.alpha <= 0f)
            {
                _beamCard.gameObject.SetActive(false);
                return;
            }
            float pop = Motion.Reduced ? 1f : Mathf.Lerp(0.74f, 1f, Mathf.SmoothStep(0f, 1f, _beamCardGroup.alpha));
            _beamCard.localScale = new Vector3(pop, pop, 1f);
            if (!up) return;                 // it fades where it stood
            FillBeamCard(run);
            PlaceBeamCard(over);
        }

        /// <summary>Whatever lies over the whole bar takes the card down with it while it is up.</summary>
        private bool BeamCardBlocked() =>
            MenuUp || GameOverUp || Showing(_pausePanel) || Showing(_settingsPanel) || Showing(_ladderPanel)
            || Showing(_dayEndPanel) || _bookOpen;

        /// <summary>
        /// The card's live half: the strips' fills every frame (a width, no words), and the words only when a figure
        /// they print has moved. Core decides every figure - the two ratings, the bill (<see cref="TycoonRun.BillAtClose"/>),
        /// the red nights on the books and whether closing now would add one (<see cref="TycoonRun.StrikeTonight"/>).
        /// </summary>
        private void FillBeamCard(TycoonRun run)
        {
            if (_beamCardFace == BeamFace.House)
            {
                double service = run.ServiceTonight, now = run.ComfortNow, filed = run.ComfortTonight;
                double lower = System.Math.Min(service, filed);
                // ...on whole units, like the stars: the mask's edge is a pixel's edge.
                if (_comfortFill != null)
                    _comfortFill.sizeDelta = new Vector2(Mathf.Round((float)(now / BarRating.MaxStars) * HouseStripW), 0f);
                if (_serviceFill != null)
                    _serviceFill.sizeDelta = new Vector2(Mathf.Round((float)(service / BarRating.MaxStars) * HouseStripW), 0f);
                if (!BeamCardKeyMoved(Hundredths(service), Hundredths(filed), Hundredths(lower), 1)) return;
                // THE NUMBER THE NIGHT FILES (2026-09-28): {comfort} is the room over the night (ComfortTonight), which
                // is what the close files - the strip above it is the room right now, which is what the mess drains.
                SetCardLine(0, UIText.T("build.standing.tip_reading",
                    ("service", service.ToString("0.0")),
                    ("comfort", filed.ToString("0.00")),
                    ("tonight", lower.ToString("0.0"))), UITheme.Cream[3]);
                for (int i = 1; i < BeamCardLines; i++) SetCardLine(i, null, UITheme.Cream[3]);
            }
            else
            {
                bool open = run.Phase == TycoonPhase.DayOpen;
                bool strike = run.StrikeTonight;
                int strikes = run.Ledger.DebtStrikes, bill = run.BillAtClose;
                if (!BeamCardKeyMoved(run.Money, bill, strikes, 2 + (open ? 4 : 0) + (strike ? 8 : 0))) return;
                int limit = DayLedger.StrikesToClose;
                int n = 0;
                SetCardLine(n++, UIText.T("build.till.tip", ("money", MoneyWords(run.Money))), UITheme.Cream[3]);
                // Tonight's bill while there is a shift to take it from; the slip prints it after the close.
                if (open) SetCardLine(n++, UIText.T("hud.till.tip_bill", ("bill", Mathf.Max(0, bill))), UITheme.Cream[3]);
                SetCardLine(n++, UIText.T("hud.till.tip_red", ("strikes", strikes), ("limit", limit)), UITheme.Cream[3]);
                SetCardLine(n++, UIText.T("hud.till.tip_red_rule", ("limit", limit)), UITheme.Cream[3]);
                // The warning the pending lamp gave, as a sentence: the bill is bigger than the till.
                if (strike)
                    SetCardLine(n++, UIText.T("hud.till.tip_red_warning", ("next", Mathf.Min(strikes + 1, limit)),
                        ("limit", limit)), UITheme.ViceRed[3]);
                while (n < BeamCardLines) SetCardLine(n++, null, UITheme.Cream[3]);
            }
            LayBeamCard();
        }

        private bool BeamCardKeyMoved(int a, int b, int c, int d)
        {
            if (_beamCardKeyed && a == _beamCardK0 && b == _beamCardK1 && c == _beamCardK2 && d == _beamCardK3) return false;
            _beamCardKeyed = true;
            _beamCardK0 = a; _beamCardK1 = b; _beamCardK2 = c; _beamCardK3 = d;
            return true;
        }

        private static int Hundredths(double v) => (int)System.Math.Round(v * 100.0);

        /// <summary>Money in words' figures: "$2520", "-$40".</summary>
        private static string MoneyWords(int money) => (money < 0 ? "-$" : "$") + Mathf.Abs(money);

        private void SetCardLine(int i, string words, Color ink)
        {
            var t = _beamCardLines[i];
            bool on = !string.IsNullOrEmpty(words);
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            if (!on) return;
            t.text = words;
            t.color = ink;
        }

        /// <summary>The card as wide as its longest row and as tall as its rows, on the 4 grid; each row's rect as wide
        /// as its words, so none of them runs past its own box. Called when the words change, never every frame.</summary>
        private void LayBeamCard()
        {
            float w = CardTextX + FitCardWidth(_beamCardTitle);
            int lines = 0;
            for (int i = 0; i < BeamCardLines; i++)
            {
                var t = _beamCardLines[i];
                if (!t.gameObject.activeSelf) continue;
                lines = i + 1;
                w = Mathf.Max(w, CardTextX + FitCardWidth(t));
            }
            float h = CardLinesTop + lines * CardLineH + CardPadB;
            if (_beamCardFace == BeamFace.House)
            {
                float wordW = Mathf.Max(FitCardWidth(_beamCardComfortWord), FitCardWidth(_beamCardServiceWord));
                float stripsW = HouseStripW + CardCapGap + wordW;
                _beamCardHouse.sizeDelta = new Vector2(stripsW, CardStripsH);
                _beamCardHouse.anchoredPosition = new Vector2(CardTextX, -(CardLinesTop + lines * CardLineH + CardStripsGap));
                w = Mathf.Max(w, CardTextX + stripsW);
                h += CardStripsGap + CardStripsH;
            }
            _beamCard.sizeDelta = new Vector2(Mathf.Max(CardMinW, Ceil4(w + CardPadR)), Ceil4(h));
        }

        /// <summary>A row's rect to its words' width, on the 4 grid; returns that width.</summary>
        private static float FitCardWidth(Text t)
        {
            float tw = Ceil4(t.preferredWidth);
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(tw, rt.sizeDelta.y);
            return tw;
        }

        /// <summary>Centred under the well that raised it, hung just below the bar's tube, and slid back inside the frame
        /// if it would cross an edge ("Hiçbir hover ekran dışına taşmamalı") - the stars' well is at the right edge.</summary>
        private void PlaceBeamCard(RectTransform over)
        {
            var parent = _beamCard.parent as RectTransform;
            if (parent == null) return;
            over.GetWorldCorners(BeamCardCorners);
            Vector2 local = parent.InverseTransformPoint((BeamCardCorners[0] + BeamCardCorners[2]) * 0.5f);
            var r = parent.rect;
            float half = _beamCard.sizeDelta.x * 0.5f;
            float x = Mathf.Clamp(local.x - r.center.x, -r.width * 0.5f + CardEdge + half, r.width * 0.5f - CardEdge - half);
            _beamCard.anchoredPosition = new Vector2(Mathf.Round(x), -(TopBarH + CardHang));
        }

        // ── the pass ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Stands the hour's well at its words' own width in the language being spoken. The right-hand wells need no
        /// words measured - the stars are one width everywhere, the till's glass is cut by its face (LayMoneyWell) -
        /// and the middle of the beam is empty (2026-09-28, second pass). Called at build and whenever RefreshTopBar's
        /// key says a word on the beam has changed - never every frame.
        /// </summary>
        private void LayTopBar()
        {
            if (_hourWell == null) return;

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
            float hourW = Mathf.Ceil(x + Mathf.Max(40f, textW) + TopWellPad);
            _hourWell.sizeDelta = new Vector2(hourW, TopWellH);
        }

        /// <summary>Up to the 4 grid: a column is never narrower than its word, and never off the grid.</summary>
        private static float Ceil4(float w) => Mathf.Ceil(w / UITheme.Grid - 0.001f) * UITheme.Grid;
    }
}
