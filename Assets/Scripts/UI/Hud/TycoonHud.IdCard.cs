using System;
using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part IdCard: the licence itself, from scratch (2026-09-22, the author's eighth list: "Kimlikleri en
    // baştan tasarlayalım"). What the card prints, how each of the four lies looks, how it turns over on its way up
    // from the stool, and the hovers on it that hold still once they have been read for a moment. The door's rules
    // behind it (who is lying, and how) are Core's IdPapers; this only draws what the card would show.
    //
    //   ┌──────────────────────────────────────────────┬───────┐
    //   │ MALIBU CLUB                        [ KICK ]  │ FLAG  │   the band: the house, the class, the number
    //   │ MIAMI · PATRON LICENCE · NA 1234 5678        │       │   and the flag in a frame cut to its shape
    //   ├──────────┬───────────────────┬───────────────┴───────┤
    //   │          │ NAME              │ SURNAME               │
    //   │  PHOTO   │ AGE               │ NATIONALITY           │
    //   │          │ [glass] ORDER                             │
    //   ├──────────┤ SERVE WITH  [ice] [twist] [rim]           │
    //   │ 3  │ 4.2 │                                           │   under the photo: visits and the stars they give
    //   └──────────┴───────────────────────────────────────────┘
    public sealed partial class TycoonHud
    {
        // ── the grid ────────────────────────────────────────────────────────────────────────────────────────────
        // 188 x 96 art pixels at the stock's own three units - two to one, a licence's shape, which is what the
        // author asked of the card on 2026-09-06 and asks of this one. Every box below is placed off these numbers
        // and nothing else.
        private const float IdW = 188f * IdArt.Grid, IdH = 96f * IdArt.Grid;     // 564 x 288
        private const float IdPad = 18f;
        private const float IdBandH = 45f;                                        // 15 art px: the flag's frame fills it
        private const float IdBodyTop = IdPad + IdBandH + 12f;                    // 75
        private const float IdWell = 134f, IdPhoto = 128f;                        // a 64 face at exactly 2x, in a frame
        private const float IdColX = IdPad + IdWell + 18f;                        // 170
        private const float IdColW = IdW - IdColX - IdPad;                        // 376
        private const float IdRowH = 42f, IdServeH = 51f, IdRowGap = 6f;
        private const float IdSplitW = 172f, IdSplitGap = 12f;                    // name | surname, age | nationality
        private const float IdStatsTop = IdBodyTop + IdWell + IdRowGap;           // 215
        private const float IdStatsH = IdBodyTop + 3f * (IdRowH + IdRowGap) + IdServeH - IdStatsTop;   // to the serve row's foot
        private const float IdFlagW = 60f, IdFlagH = 45f;                         // a 48x33 flag at 1:1 in six units of frame
        private const float IdKickW = 84f, IdKickH = 33f;
        private const float IdChip = 28f, IdChipGap = 6f;

        // ── the turn: up from the stool, face down, and over ─────────────────────────────────────────────────────
        // "Kimlik tasarımı ve açılışı çevirerek olmalı kimlik açılırken tıklanan müşterinin üzerinden büyüyerek
        // açılmalı, kapanırken de tam tersi." The card leaves the drinker it was taken from at a seventh of its size,
        // back up, and turns over as it comes to the middle; giving it back runs the same path the other way.
        private const float IdOpenSeconds = 0.46f, IdCloseSeconds = 0.36f, IdFromScale = 0.14f;
        private float _idFlipT;          // 0 at the stool, face down; 1 open in the middle, face up
        private int _idFlipDir;          // +1 opening, -1 giving it back, 0 still
        private Vector2 _idFrom;         // the stool's point, in the licence layer's own units
        private RectTransform _idCardRt, _idFront, _idBack;
        private Image _idScrim, _idBackImg;
        private Text _idBackWord, _idHint;

        private enum CardLook { Print, Copied, Drawn }

        private enum IdTipKind { None, Recipe, Garnish, Stats, Flag }

        /// <summary>A thing on the card that raises a hover, found by a rect test each frame (never by an
        /// EventTrigger: the tip may cover it once it is pinned, and a pointer event would call that a leave).</summary>
        private sealed class IdHover
        {
            public RectTransform Rt;
            public IdTipKind Kind;
            public PreparationDefinition Garnish;
        }

        /// <summary>Everything the card PRINTS in words, so each look can re-ink it: the role says which face and
        /// size it takes.</summary>
        private sealed class IdInk
        {
            public Text Text;
            public int Role;         // 0 caption, 1 value, 2 the house's name, 3 the band's small line, 4 a figure
            public HandJitter Hand;
            public Shadow Ghost;     // the copy's second plate, out of register
            public Vector2 Home;
            public string Base;      // a caption's own words, put back before each look re-inks them
        }

        /// <summary>A box the card's print draws round a field; the drawn card rules it by hand instead.</summary>
        private sealed class IdBox
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Home;
            public int Salt;
        }

        private readonly List<IdInk> _idInks = new List<IdInk>();
        private readonly List<IdBox> _idBoxes = new List<IdBox>();
        private readonly List<IdHover> _idHovers = new List<IdHover>();

        private Image _idPaper, _idBand, _idBandScrim, _idFlag, _idFlagFrame, _idPhoto, _idPhotoWell;
        private Image _idOrderIcon, _idDoor, _idStar;
        private Text _idAuthority, _idClass, _idNumber, _idFirst, _idSurname, _idAge, _idNation, _idOrderName;
        private Text _idVisitsN, _idRateN, _idServePlain;
        private RectTransform _idServeRow, _idOrderBox, _idStatsBox, _idFlagBox, _idKick;
        private CardLook _idLook;
        private uint _idSeed;
        private string _idFlagIso;
        private int _idVisitsShown;
        private double _idAverage = -1;

        // ── building it ─────────────────────────────────────────────────────────────────────────────────────────

        private RectTransform IdRect(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        private Text IdText(string name, RectTransform parent, float x, float y, float w, float h, int role,
                            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var t = NewText(name, parent, _body, 16, anchor, UITheme.Night[1]);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;   // a licence never wraps; the limits keep it inside
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            var hand = t.gameObject.AddComponent<HandJitter>();
            hand.enabled = false;
            var ghost = t.gameObject.AddComponent<Shadow>();
            ghost.enabled = false;
            _idInks.Add(new IdInk { Text = t, Role = role, Hand = hand, Ghost = ghost, Home = rt.anchoredPosition });
            return t;
        }

        private void IdCaption(Text t, string key)
        {
            t.text = UIText.T(key);
            foreach (var ink in _idInks) if (ink.Text == t) ink.Base = t.text;
        }

        private IdBox IdFieldBox(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var rt = IdRect(name, parent, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var box = new IdBox { Rt = rt, Img = img, Home = rt.anchoredPosition, Salt = _idBoxes.Count * 31 + 7 };
            _idBoxes.Add(box);
            return box;
        }

        private void BuildIdCard(RectTransform root)
        {
            // ITS OWN LAYER, ABOVE THE BAR: stage -10, HUD 5, till 6, service flow 12, recipe book 15, licence 20,
            // the market 22. Anything that is a WINDOW over the room says so rather than relying on being built late.
            _idRoot = NewRect("IdCard", root);
            var idCanvas = _idRoot.gameObject.AddComponent<Canvas>();
            idCanvas.overrideSorting = true;
            idCanvas.sortingOrder = 20;
            _idRoot.gameObject.AddComponent<ForgivingRaycaster>();
            Stretch(_idRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _idScrim = _idRoot.gameObject.AddComponent<Image>();
            _idScrim.color = UITheme.Scrim;
            var scrimBtn = _idRoot.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(CloseId);

            _idCardRt = NewRect("Card", _idRoot);
            Place(_idCardRt, new Vector2(0.5f, 0.5f), new Vector2(IdW, IdH), new Vector2(0, 10));
            _idCardRt.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // swallow clicks

            // THE BACK, seen only while it turns over
            _idBack = NewRect("Back", _idCardRt);
            Stretch(_idBack, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _idBackImg = _idBack.gameObject.AddComponent<Image>();
            _idBackImg.raycastTarget = true;
            _idBackWord = NewText("House", _idBack, _display, 16, TextAnchor.MiddleCenter, UITheme.Night[1]);
            Place(_idBackWord.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(IdW, 22), new Vector2(0, -IdH * 0.22f));
            _idBackWord.horizontalOverflow = HorizontalWrapMode.Overflow;
            _idBackWord.text = "MALIBU CLUB";   // the house's own name, a brand: never translated
            _idBack.gameObject.SetActive(false);

            // THE FACE. The paper is the mask, so the band's picture is cut to the card's own corners - rounded on
            // the house's stock, square on the copy, wherever the scissors went on the drawing.
            _idFront = NewRect("Front", _idCardRt);
            Stretch(_idFront, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _idPaper = _idFront.gameObject.AddComponent<Image>();
            _idPaper.raycastTarget = true;
            _idFront.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            BuildIdBand();
            BuildIdPhoto();
            BuildIdFields();
            BuildIdTip();

            _idHint = NewText("Hint", _idRoot, _body, 12, TextAnchor.MiddleCenter, UITheme.TextSecondary);
            Place(_idHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400, 20), new Vector2(0, -(IdH * 0.5f) - 6f));
            _idHint.text = UIText.T("id.card.give_back_hint");

            _idRoot.gameObject.SetActive(false);
        }

        private void BuildIdBand()
        {
            float bandW = IdW - IdPad * 2f;
            var band = IdRect("Band", _idFront, IdPad, IdPad, bandW, IdBandH);
            _idBand = band.gameObject.AddComponent<Image>();
            _idBand.raycastTarget = false;
            // ...and a scrim under the type at the left, fading to the sea: a sunset is the value of cream type.
            var scrim = NewRect("Scrim", band);
            // ...over the left half only (2026-09-22): the beach's sun sits at three fifths of the picture, and a
            // scrim reaching to 58% put it out. The class line ends well before the half on every language measured.
            Stretch(scrim, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-bandW * 0.50f, 0f));
            _idBandScrim = scrim.gameObject.AddComponent<Image>();
            _idBandScrim.sprite = ChromeArt.FadeRight();
            _idBandScrim.raycastTarget = false;

            _idAuthority = IdText("Authority", _idFront, IdPad + 9f, IdPad + 5f, 260f, 20f, 2);
            _idClass = IdText("Class", _idFront, IdPad + 9f, IdPad + 28f, 200f, 12f, 3);
            _idNumber = IdText("Num", _idFront, IdPad + 9f, IdPad + 28f, 160f, 12f, 3);

            // THE FLAG, IN ITS OWN SHAPE (the author: "sağ üstte normal ülkesinin bayrağı, ülkesinin bayrağının
            // etrafında bayrağın şekline göre çerçeve olacak"). The 48x33 drawing at 1:1 - a flag at 1.3x is a flag
            // with some stripes twice as thick as the others - in a frame one pixel of ink over one of cream, at
            // the band's own height, where a card puts its emblem.
            _idFlagBox = IdRect("FlagBox", _idFront, IdW - IdPad - IdFlagW, IdPad, IdFlagW, IdFlagH);
            var flag = NewRect("Flag", _idFlagBox);
            Place(flag, new Vector2(0.5f, 0.5f), new Vector2(48, 33), Vector2.zero);
            _idFlag = flag.gameObject.AddComponent<Image>();
            _idFlag.raycastTarget = false;
            // the frame over the flag, a child drawn after it
            var frame = NewRect("Frame", _idFlagBox);
            Stretch(frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _idFlagFrame = frame.gameObject.AddComponent<Image>();
            _idFlagFrame.raycastTarget = false;
            _idFlagFrame.type = Image.Type.Sliced;
            _idHovers.Add(new IdHover { Rt = _idFlagBox, Kind = IdTipKind.Flag });

            // THE KICK KEY (GDD 28 §4): the house's own red key, in the band left of the flag - never on the scrim,
            // whose one meaning is close. It is the game's chrome, not the card's print, so every lie shows the same key.
            _idKick = IdRect("Kick", _idFront, IdW - IdPad - IdFlagW - 12f - IdKickW, IdPad + (IdBandH - IdKickH) * 0.5f, IdKickW, IdKickH);
            var kickBtn = _idKick.gameObject.AddComponent<Button>();
            var kickFace = NewRect("Face", _idKick);
            Stretch(kickFace, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(_idKick, UITheme.ViceRed[2], kickBtn, kickFace);
            var kickWord = NewText("L", kickFace, _body, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Stretch(kickWord.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, KeyPlate.Throw), new Vector2(-4, 0));
            kickWord.text = UIText.T("id.card.kick");
            kickWord.raycastTarget = false;
            kickBtn.onClick.AddListener(KickTheOneOnTheCard);
        }

        private void BuildIdPhoto()
        {
            // THE PHOTO IS THIS DRINKER'S OWN FACE (the author: "Karakter vesikalıkları ... her karakterin face.png'si
            // kendi dosyalarının içerisinde bulunuyor onları kullan") - the 64x64 cut from their own idle frame, at
            // exactly 2x, on the pale ground an ID photo is taken against, in the flag's frame.
            var well = IdRect("PhotoWell", _idFront, IdPad, IdBodyTop, IdWell, IdWell);
            _idPhotoWell = well.gameObject.AddComponent<Image>();
            _idPhotoWell.raycastTarget = false;
            var photo = NewRect("Photo", well);
            Place(photo, new Vector2(0.5f, 0.5f), new Vector2(IdPhoto, IdPhoto), Vector2.zero);
            _idPhoto = photo.gameObject.AddComponent<Image>();
            _idPhoto.preserveAspect = true;
            _idPhoto.raycastTarget = false;
            var frame = IdFieldBox("PhotoFrame", _idFront, IdPad, IdBodyTop, IdWell, IdWell);
            frame.Salt = 991;

            // VISITS AND STARS, THE PHOTO'S WIDTH, RIGHT UNDER IT (the author: "Vesikalığın tam altında vesikalığın
            // genişliğinde ziyaret sayısı ve ortalama verdiği puanı gösteren panel olacak"). Two halves: a door and
            // the times they have walked through it, a star and the average they leave; the words under each.
            _idStatsBox = IdRect("Stats", _idFront, IdPad, IdStatsTop, IdWell, IdStatsH);
            var statsHit = _idStatsBox.gameObject.AddComponent<Image>();
            statsHit.color = new Color(0, 0, 0, 0.001f);
            statsHit.raycastTarget = false;
            IdFieldBox("StatsBox", _idFront, IdPad, IdStatsTop, IdWell, IdStatsH);
            float half = IdWell * 0.5f;
            var rule = IdFieldBox("StatsRule", _idFront, IdPad + half - 1f, IdStatsTop + 8f, 2f, IdStatsH - 16f);
            rule.Salt = -1;                            // a rule, not a box: drawn as one line in every look
            float rowY = IdStatsTop + 10f;
            var door = IdRect("Door", _idFront, IdPad + 12f, rowY + 1f, 16f, 16f);
            _idDoor = door.gameObject.AddComponent<Image>();
            _idDoor.sprite = IdArt.DoorMark();
            _idDoor.raycastTarget = false;
            _idVisitsN = IdText("Visits", _idFront, IdPad + 32f, rowY, half - 36f, 18f, 4);
            var star = IdRect("Star", _idFront, IdPad + half + 10f, rowY + 1f, 16f, 16f);
            _idStar = star.gameObject.AddComponent<Image>();
            _idStar.preserveAspect = true;
            _idStar.raycastTarget = false;
            _idRateN = IdText("Rate", _idFront, IdPad + half + 30f, rowY, half - 34f, 18f, 4);
            IdCaption(IdText("C_Visits", _idFront, IdPad + 6f, rowY + 24f, half - 10f, 12f, 0, TextAnchor.UpperCenter), "id.stamps.visits");
            IdCaption(IdText("C_Rate", _idFront, IdPad + half + 4f, rowY + 24f, half - 10f, 12f, 0, TextAnchor.UpperCenter), "id.stamps.rated");
            _idHovers.Add(new IdHover { Rt = _idStatsBox, Kind = IdTipKind.Stats });
        }

        private void BuildIdFields()
        {
            float x1 = IdColX, x2 = IdColX + IdSplitW + IdSplitGap, w2 = IdColW - IdSplitW - IdSplitGap;
            float y = IdBodyTop;

            // NAME | SURNAME, and under them AGE | NATIONALITY (the author: "sağda isim yanında ayrı bir bölümde soy
            // isim, altında yaşı yanında yarı şekilde milliyeti"): one two-column grid, so the four boxes line up.
            void Field(string id, string capKey, float x, float w, out Text value)
            {
                IdFieldBox("Box_" + id, _idFront, x, y, w, IdRowH);
                IdCaption(IdText("C_" + id, _idFront, x + 6f, y + 4f, w - 12f, 12f, 0), capKey);
                value = IdText("V_" + id, _idFront, x + 6f, y + 17f, w - 12f, 22f, 1);
            }
            Field("First", "id.card.first", x1, IdSplitW, out _idFirst);
            Field("Surname", "id.card.surname", x2, w2, out _idSurname);
            y += IdRowH + IdRowGap;
            Field("Age", "id.card.age", x1, IdSplitW, out _idAge);
            Field("Nation", "id.card.nationality", x2, w2, out _idNation);
            y += IdRowH + IdRowGap;

            // THE ORDER: its picture and, beside it, its name (the author: "Altında siparişin görseli ve yanında
            // siparişin ismi"). The glass is DrinkIcon's 32 at 1:1. Hovering it opens the recipe, the book's own page.
            _idOrderBox = IdRect("OrderHit", _idFront, x1, y, IdColW, IdRowH);
            IdFieldBox("Box_Order", _idFront, x1, y, IdColW, IdRowH);
            var icon = IdRect("OrderIcon", _idFront, x1 + 5f, y + 5f, 32f, 32f);
            _idOrderIcon = icon.gameObject.AddComponent<Image>();
            _idOrderIcon.preserveAspect = true;
            _idOrderIcon.raycastTarget = false;
            IdCaption(IdText("C_Order", _idFront, x1 + 44f, y + 4f, IdColW - 52f, 12f, 0), "id.card.order");
            _idOrderName = IdText("V_Order", _idFront, x1 + 44f, y + 17f, IdColW - 52f, 22f, 1);
            _idHovers.Add(new IdHover { Rt = _idOrderBox, Kind = IdTipKind.Recipe });
            y += IdRowH + IdRowGap;

            // WHAT GOES ON THE GLASS (the author: "Onun altında ise siparişin sunumunda istediği şeyler, örneğin
            // limon/tuz/birkaç adet buz vs."): the pictograms in a row under their caption, each with its hover.
            IdFieldBox("Box_Serve", _idFront, x1, y, IdColW, IdServeH);
            IdCaption(IdText("C_Serve", _idFront, x1 + 6f, y + 4f, IdColW - 12f, 12f, 0), "id.card.serve");
            _idServeRow = IdRect("ServeRow", _idFront, x1 + 6f, y + 18f, IdColW - 12f, IdChip);
            _idServePlain = IdText("V_Plain", _idFront, x1 + 6f, y + 22f, IdColW - 12f, 22f, 1);
        }

        // ── filling it ──────────────────────────────────────────────────────────────────────────────────────────

        private void ShowId(CustomerVisit visit)
        {
            if (visit?.Regular == null) return;
            _idVisit = visit;
            var reg = visit.Regular;

            // Opening the card IS the inspection (v5 C3): this is the one gate Core opens the order through, so
            // everything below may read it - and the ticket may from now on.
            visit.InspectId();
            Sfx.Play("id_card", 0.85f);          // a card drawn out of a wallet (the eighth list: "kart çekme sesi")

            if (_ledgerPanel != null) _ledgerPanel.gameObject.SetActive(false);
            _idRoot.gameObject.SetActive(true);
            HideIdTip(true);

            var ownLook = LookFor(visit);
            IdPapers truth = visit.Papers;
            var lie = truth != null ? truth.Forgery : Forgery.None;
            _idLook = lie == Forgery.Copied ? CardLook.Copied : lie == Forgery.Drawn ? CardLook.Drawn : CardLook.Print;
            _idSeed = IdArt.Seed(reg.Id ?? ownLook?.Slug ?? "patron");

            // WHOSE CARD IT IS. A borrowed one is a stranger's, photo and all (GDD 28 §3.1; since the eighth list a
            // man the bar never draws); every other lie is their own card, lying in its own way.
            Sprite photo = ownLook?.Face;
            Papers papers = PapersFor(ownLook);
            string numberSlug = ownLook?.Slug;
            if (lie == Forgery.Borrowed)
            {
                if (StrangerFor(visit, out var stranger, out var face)) { photo = face; papers = stranger; numberSlug = stranger.Slug; }
                else
                {
                    var lender = LenderFor(visit, ownLook);
                    if (lender != null) { photo = lender.Face; papers = PapersFor(lender); numberSlug = lender.Slug; }
                }
            }

            string name = NameOn(visit, ownLook);
            int space = name.IndexOf(' ');
            _idFirst.text = space < 0 ? name : name.Substring(0, space);
            _idSurname.text = space < 0 ? "" : name.Substring(space + 1);
            // An honest minor's card says how old they are (GDD 28 §2.1), and an altered, copied or drawn one prints
            // the year they bumped it to: the truth's PrintedAge. Only a borrowed card wears the lender's own age.
            int age = truth != null && truth.IsMinor && lie != Forgery.Borrowed
                ? truth.PrintedAge : (papers != null ? papers.Age : reg.Age);
            _idAge.text = age.ToString();
            string iso = papers != null ? papers.Iso : null;
            string country = papers == null ? UIText.T(reg.HometownLine)
                : string.IsNullOrEmpty(iso) ? papers.Country : UIText.Data("country", iso, "name", papers.Country);
            _idNation.text = string.IsNullOrEmpty(iso) ? country : UIText.TOr("id.nationality." + iso, country);

            _idAuthority.text = "MALIBU CLUB";   // the house's own name, a brand: never translated
            _idClass.text = UIText.T("id.card.class");
            _idNumber.text = LicenceNumber(numberSlug, name.ToUpperInvariant());   // the same person, the same number

            // THE ALTERED CARD'S TELL (GDD 28 §2.1, H6): their own face and name, the year bumped - and a flag that
            // is not their country's, chosen off the person's id so the same card lies the same way every time.
            string flagIso = iso;
            if (lie == Forgery.Altered) flagIso = WrongFlagFor(visit, iso);
            _idFlagIso = flagIso;

            // THE COUNTS ARE THE PERSON'S, whoever's card it is: how many times they have walked in (this one counts
            // as they sit) and the stars they have left the bar, averaged.
            var rec = LogFor(ownLook);
            _idVisitsShown = rec.Visits + 1;
            _idAverage = rec.Ratings > 0 ? rec.Stars / rec.Ratings : -1;
            _idVisitsN.text = _idVisitsShown.ToString();
            _idRateN.text = _idAverage >= 0 ? _idAverage.ToString("0.0") : "?";

            // No price, anywhere on the card (C3): the licence says who they are and what they want.
            _idOrderName.text = RecipeTitle(visit.Order.Wanted);

            // ...and not before the ladder's second rung (2026-09-21): the door is not the bar's yet, and run.Kick
            // would refuse it - the key is not drawn rather than drawn dead.
            if (_idKick != null) _idKick.gameObject.SetActive(!visit.OnTheHouse && Run != null && Run.Has(Feature.Door));

            DressIdCard(photo, flagIso, visit);
            FillIdServe(visit);
            FitIdValues();
            // the number follows the class line, wherever this language's words end
            foreach (var ink in _idInks)
                if (ink.Text == _idNumber)
                {
                    ink.Home = new Vector2(IdPad + 9f + Mathf.Ceil(_idClass.preferredWidth) + 8f, ink.Home.y);
                    _idNumber.rectTransform.anchoredPosition = ink.Home + (_idLook == CardLook.Drawn ? new Vector2(0f, 3f) : Vector2.zero);
                }

            // UP FROM THE STOOL
            _idFrom = IdPointOfSeat(visit);
            _idFlipT = Motion.Reduced ? 1f : 0f;
            _idFlipDir = Motion.Reduced ? 0 : 1;
            ApplyIdFlip(_idFlipT);
        }

        /// <summary>Where the drinker is, in the licence layer's units: two thirds up the stool's rect, which is their
        /// chest - the card leaves the person, not the counter.</summary>
        private Vector2 IdPointOfSeat(CustomerVisit visit)
        {
            foreach (var seat in _seats)
            {
                if (seat.Visit != visit || seat.Root == null) continue;
                var c = new Vector3[4];
                seat.Root.GetWorldCorners(c);
                var p = Vector3.Lerp((c[0] + c[3]) * 0.5f, (c[1] + c[2]) * 0.5f, 0.62f);
                var screen = RectTransformUtility.WorldToScreenPoint(null, p);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_idRoot, screen, null, out var local))
                    return local;
            }
            return new Vector2(0f, -220f);
        }

        /// <summary>Everything the look changes: the stock, the band, the photo and the flag, the boxes and the ink.</summary>
        private void DressIdCard(Sprite photo, string flagIso, CustomerVisit visit)
        {
            var look = _idLook;
            uint seed = _idSeed;
            int artW = (int)(IdW / IdArt.Grid), artH = (int)(IdH / IdArt.Grid);

            // THE STOCK
            _idPaper.color = Color.white;
            _idBackImg.color = Color.white;
            if (look == CardLook.Print)
            {
                // The house's cream, TILED so its stipple is the grain the game is drawn at (a sliced centre
                // stretches its three pixels into a wall of blocks), its corners rounded a little.
                _idPaper.sprite = ChromeArt.LicencePaper();
                _idPaper.type = Image.Type.Tiled;
                _idPaper.pixelsPerUnitMultiplier = 1f / IdArt.Grid;
                _idBackImg.sprite = IdArt.Back(seed, artW, artH, false);
            }
            else if (look == CardLook.Copied)
            {
                // "Kimliğin arkaplanı yıpranmış ve köşeli": worn, and square at every corner.
                _idPaper.sprite = IdArt.WornPaper(seed, artW, artH);
                _idPaper.type = Image.Type.Simple;
                _idBackImg.sprite = IdArt.Back(seed, artW, artH, true);
            }
            else
            {
                _idPaper.sprite = IdArt.ScrapPaper(seed, (int)IdW, (int)IdH);
                _idPaper.type = Image.Type.Simple;
                _idBackImg.sprite = IdArt.ScrapBack(seed, (int)IdW, (int)IdH);
            }
            _idBackImg.type = Image.Type.Simple;

            // THE BAND: the house's beach at its own three (the middle 176 of its 200 columns, 15 of its 16 rows,
            // so nothing is stretched); the copy's run through a tired copier and printed a pixel off; the drawing's
            // is a box ruled in pen with nothing in it.
            var beach = BandPicture();
            _idBand.enabled = look != CardLook.Drawn && beach != null;
            _idBandScrim.enabled = _idBand.enabled;
            _idBand.sprite = look == CardLook.Copied ? IdArt.Reprint(beach) : beach;
            _idBand.color = Color.white;
            _idBand.rectTransform.anchoredPosition = new Vector2(IdPad, -IdPad) + (look == CardLook.Copied ? new Vector2(2f, -1f) : Vector2.zero);
            _idBandScrim.color = look == CardLook.Copied ? new Color(0.20f, 0.18f, 0.22f, 0.55f) : new Color(0.05f, 0.03f, 0.08f, 0.82f);

            // THE PHOTO
            _idPhoto.sprite = look == CardLook.Drawn ? IdArt.StickFigure(seed)
                            : look == CardLook.Copied ? (IdArt.Reprint(photo) ?? photo)
                            : photo;
            _idPhoto.color = _idPhoto.sprite != null ? Color.white : UITheme.Night[3];
            _idPhotoWell.color = look == CardLook.Drawn ? new Color(0, 0, 0, 0)
                               : look == CardLook.Copied ? new Color(0.78f, 0.79f, 0.78f, 1f)
                               : new Color(0.80f, 0.85f, 0.89f, 1f);     // the pale ground an ID photo is taken on

            // THE FLAG
            var flag = flagIso != null ? ItemArt.Load("fl_" + flagIso) : null;
            _idFlag.sprite = flag == null ? null
                           : look == CardLook.Drawn ? IdArt.HandFlag(flag, seed)
                           : look == CardLook.Copied ? IdArt.Reprint(flag)
                           : flag;
            _idFlag.enabled = _idFlag.sprite != null;       // a citizenship with no flag drawn shows nothing, not a box
            _idFlagFrame.sprite = look == CardLook.Drawn ? IdArt.PenBox((int)IdFlagW, (int)IdFlagH, seed + 77u) : IdArt.FlagFrame();
            _idFlagFrame.type = look == CardLook.Drawn ? Image.Type.Simple : Image.Type.Sliced;
            _idFlagFrame.pixelsPerUnitMultiplier = look == CardLook.Drawn ? 1f : 1f / IdArt.Grid;
            _idFlagFrame.color = Color.white;

            // THE BOXES: the print's hairline boxes, a pixel out of register on the copy, ruled by hand on the drawing.
            foreach (var b in _idBoxes)
            {
                var size = b.Rt.sizeDelta;
                if (look == CardLook.Drawn)
                {
                    b.Img.sprite = b.Salt < 0 ? null : IdArt.PenBox(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), seed + (uint)b.Salt);
                    b.Img.type = Image.Type.Simple;
                    b.Img.color = b.Salt < 0 ? new Color(IdArt.Pen.r, IdArt.Pen.g, IdArt.Pen.b, 0.55f) : Color.white;
                    b.Rt.anchoredPosition = b.Home;
                }
                else
                {
                    bool photoFrame = b.Salt == 991;
                    b.Img.sprite = b.Salt < 0 ? null : photoFrame ? IdArt.PhotoFrame() : IdArt.FieldBox();
                    b.Img.type = Image.Type.Sliced;
                    b.Img.pixelsPerUnitMultiplier = photoFrame ? 1f / IdArt.Grid : 1f;
                    b.Img.color = b.Salt < 0 ? new Color(0.30f, 0.16f, 0.05f, 0.35f) : Color.white;
                    b.Rt.anchoredPosition = b.Home + (look == CardLook.Copied ? new Vector2(2f, -1f) : Vector2.zero);
                }
            }

            // THE MARKS
            _idDoor.color = look == CardLook.Drawn ? IdArt.Pen : look == CardLook.Copied ? new Color(0.34f, 0.36f, 0.46f, 1f) : UITheme.ClubBlue[2];
            _idStar.sprite = look == CardLook.Drawn ? IdArt.PenStar() : ItemArt.Star(true, 16f);
            _idStar.color = look == CardLook.Copied ? new Color(0.86f, 0.84f, 0.80f, 1f) : Color.white;
            var icon = DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
            _idOrderIcon.sprite = look == CardLook.Drawn ? IdArt.Sketch(icon, seed) : look == CardLook.Copied ? IdArt.Reprint(icon) : icon;
            _idOrderIcon.enabled = _idOrderIcon.sprite != null;

            // THE BACK'S WORD
            _idBackWord.font = look == CardLook.Drawn ? LanguageFonts.Hand(_body) : _display;
            _idBackWord.fontSize = look == CardLook.Drawn ? 24 : 16;
            _idBackWord.color = look == CardLook.Drawn ? IdArt.Pen : look == CardLook.Copied ? new Color(0.36f, 0.33f, 0.38f, 1f) : UITheme.Night[1];
            _idBackWord.text = look == CardLook.Drawn ? "Malibu Club" : "MALIBU CLUB";
            var backHand = _idBackWord.GetComponent<HandJitter>();
            if (backHand == null) backHand = _idBackWord.gameObject.AddComponent<HandJitter>();   // not ??: the editor's fake null
            backHand.enabled = look == CardLook.Drawn;
            backHand.Set(seed + 5u, 7f, 2f, -3f);

            InkIdCard();
        }

        /// <summary>The words, in the look's hand: the house faces in their inks on the print; the same, washed and
        /// doubled a pixel off on the copy; a ballpoint's lettering on the drawing, every glyph put down by hand.</summary>
        private void InkIdCard()
        {
            var look = _idLook;
            var small = LanguageFonts.SmallLine(_body);
            var hand = LanguageFonts.Hand(_body);
            bool realHand = LanguageFonts.IsHand(hand);
            int i = 0;
            foreach (var ink in _idInks)
            {
                var t = ink.Text;
                i++;
                if (ink.Base != null) t.text = ink.Base;
                if (look == CardLook.Drawn)
                {
                    t.font = hand;
                    t.fontSize = realHand ? (ink.Role == 0 || ink.Role == 3 ? 13 : ink.Role == 2 ? 22 : 20)
                                          : LanguageFonts.Size(hand, ink.Role == 0 || ink.Role == 3 ? 8 : 16);
                    t.color = ink.Role == 0 || ink.Role == 3 ? new Color(IdArt.Pen.r, IdArt.Pen.g, IdArt.Pen.b, 0.78f) : IdArt.Pen;
                    ink.Hand.enabled = true;
                    ink.Hand.Set(_idSeed + (uint)(i * 97), realHand ? 4f : 7f, realHand ? 1.2f : 1.8f,
                                 (IdArt.R(_idSeed, 300 + i) - 0.5f) * 5f);
                    ink.Ghost.enabled = false;
                    t.rectTransform.anchoredPosition = ink.Home + new Vector2(0f, realHand ? 3f : 0f);
                    // lower case, as a hand writes it: the captions and the house's name in the language's own
                    // sentence case, the values as they are
                    if (ink.Role == 0 || ink.Role == 3) t.text = UIText.Sentence(t.text);
                    continue;
                }
                ink.Hand.enabled = false;
                t.rectTransform.anchoredPosition = ink.Home;
                switch (ink.Role)
                {
                    case 0: t.font = small.font; t.fontSize = small.size; t.color = UITheme.ClubBlue[2]; break;
                    case 1: t.font = _display; t.fontSize = LanguageFonts.Size(_display, 16); t.color = UITheme.Night[1]; break;
                    case 2: t.font = _display; t.fontSize = LanguageFonts.Size(_display, 16); t.color = UITheme.Cream[4]; break;
                    case 3: t.font = small.font; t.fontSize = small.size; t.color = new Color(0.80f, 0.86f, 0.96f, 1f); break;
                    default: t.font = _body; t.fontSize = LanguageFonts.Size(_body, 16); t.color = UITheme.Night[1]; break;
                }
                bool copied = look == CardLook.Copied;
                ink.Ghost.enabled = copied && ink.Role != 3;
                if (copied)
                {
                    // the copier's second plate, a pixel and a half off, and the black gone to a tired grey
                    ink.Ghost.effectColor = new Color(0.86f, 0.20f, 0.55f, 0.42f);
                    ink.Ghost.effectDistance = new Vector2(2f, -1f);
                    ink.Ghost.useGraphicAlpha = true;
                    var c = t.color;
                    t.color = ink.Role == 2 ? new Color(0.92f, 0.90f, 0.86f, 1f)
                            : new Color(Mathf.Lerp(c.r, 0.42f, 0.35f), Mathf.Lerp(c.g, 0.40f, 0.35f), Mathf.Lerp(c.b, 0.44f, 0.35f), 1f);
                }
            }
        }

        /// <summary>A value too wide for its box in the heading face is set in the body face, which is narrower; the
        /// limits (Papers.FirstNameMax and friends) are what keep the heading face the usual answer.</summary>
        private void FitIdValues()
        {
            if (_idLook == CardLook.Drawn) return;
            foreach (var ink in _idInks)
            {
                if (ink.Role != 1) continue;
                var t = ink.Text;
                float room = t.rectTransform.sizeDelta.x;
                if (t.preferredWidth > room)
                {
                    t.font = _body;
                    t.fontSize = LanguageFonts.Size(_body, 16);
                    t.rectTransform.anchoredPosition = ink.Home + new Vector2(0f, -2f);
                }
            }
        }

        private static Sprite s_bandCut;

        /// <summary>The beach band at its own three units a pixel: the middle 176 of its 200 columns and the top 15 of
        /// its 16 rows, cut once, so the band is the picture and not a stretch of it.</summary>
        private static Sprite BandPicture()
        {
            if (s_bandCut != null) return s_bandCut;
            var src = ItemArt.Load("licence_band");
            if (src == null || src.texture == null) return null;
            var r = src.rect;
            float w = Mathf.Min(r.width, (IdW - IdPad * 2f) / IdArt.Grid), h = Mathf.Min(r.height, IdBandH / IdArt.Grid);
            var cut = new Rect(r.x + Mathf.Floor((r.width - w) * 0.5f), r.y + (r.height - h), w, h);
            s_bandCut = Sprite.Create(src.texture, cut, new Vector2(0.5f, 0.5f), src.pixelsPerUnit);
            s_bandCut.name = "licence_band(card)";
            return s_bandCut;
        }

        /// <summary>The garnishes they want, as pictograms in a row; nothing asked for says so in words.</summary>
        private void FillIdServe(CustomerVisit visit)
        {
            for (int i = _idServeRow.childCount - 1; i >= 0; i--) Destroy(_idServeRow.GetChild(i).gameObject);
            _idHovers.RemoveAll(h => h.Kind == IdTipKind.Garnish);
            float x = 0f;
            int n = 0;
            foreach (var g in visit.Order.Garnishes)
            {
                var chip = NewRect("Pref_" + g.Id, _idServeRow);
                chip.anchorMin = chip.anchorMax = new Vector2(0, 1);
                chip.pivot = new Vector2(0, 1);
                chip.sizeDelta = new Vector2(IdChip, IdChip);
                chip.anchoredPosition = new Vector2(x, 0f);
                var plate = chip.gameObject.AddComponent<Image>();
                plate.raycastTarget = false;
                if (_idLook == CardLook.Drawn)
                {
                    plate.sprite = IdArt.PenBox((int)IdChip, (int)IdChip, _idSeed + 400u + (uint)n);
                    plate.color = Color.white;
                }
                else
                {
                    plate.sprite = IdArt.FieldBox();
                    plate.type = Image.Type.Sliced;
                    plate.color = new Color(1f, 1f, 1f, 1f);
                }
                var iconRt = NewRect("I", chip);
                Place(iconRt, new Vector2(0.5f, 0.5f), new Vector2(24, 24), Vector2.zero);
                var img = iconRt.gameObject.AddComponent<Image>();
                var art = PrefArt.ForPreparation(g.Id) ?? GarnishCounterArt(g.Id);
                img.sprite = _idLook == CardLook.Drawn ? IdArt.Sketch(art, _idSeed) : _idLook == CardLook.Copied ? IdArt.Reprint(art) : art;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.enabled = img.sprite != null;
                if (_idLook == CardLook.Copied) chip.anchoredPosition += new Vector2(2f, -1f);
                _idHovers.Add(new IdHover { Rt = chip, Kind = IdTipKind.Garnish, Garnish = g });
                x += IdChip + IdChipGap;
                n++;
            }
            // Asking for nothing is said in words: blank means "not filled in", this means "there are none".
            _idServePlain.gameObject.SetActive(n == 0);
            _idServePlain.text = n == 0 ? UIText.T("id.card.serve_plain") : "";
        }

        // ── the turn ────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Steps the card's turn and its hovers. Called every frame the HUD is stepped; costs a compare when
        /// nothing is moving.</summary>
        private void StepIdOpen()
        {
            if (_idRoot == null || !_idRoot.gameObject.activeSelf) return;
            if (_idFlipDir != 0)
            {
                float dur = _idFlipDir > 0 ? IdOpenSeconds : IdCloseSeconds;
                _idFlipT = Mathf.Clamp01(_idFlipT + _idFlipDir * Time.unscaledDeltaTime / dur);
                ApplyIdFlip(_idFlipT);
                if (_idFlipDir > 0 && _idFlipT >= 1f) _idFlipDir = 0;
                else if (_idFlipDir < 0 && _idFlipT <= 0f)
                {
                    _idFlipDir = 0;
                    HideIdTip(true);
                    _idRoot.gameObject.SetActive(false);
                    return;
                }
            }
            StepIdTip();
        }

        /// <summary>
        /// The card at <paramref name="t"/> of its way up: from the stool to the middle on an ease that arrives
        /// gently (and so leaves briskly on the way back), a seventh of its size to all of it, and turned over about
        /// its upright - face down for the first part of the way, edge on at the middle of the turn, face up for the
        /// rest. A touch of spin comes off as it lands. The dim behind it comes up with it.
        /// </summary>
        private void ApplyIdFlip(float t)
        {
            if (_idCardRt == null) return;
            float ease = Tweening.OutCubic(t);
            var home = new Vector2(0f, 10f);
            _idCardRt.anchoredPosition = Vector2.LerpUnclamped(_idFrom, home, ease);
            float s = Mathf.Lerp(IdFromScale, 1f, ease);
            float turn = Mathf.Clamp01((t - 0.12f) / 0.62f);
            turn = turn * turn * (3f - 2f * turn);
            float cos = Mathf.Cos(Mathf.PI * (1f - turn));          // -1 face down .. 1 face up
            bool faceUp = cos >= 0f;
            _idCardRt.localScale = new Vector3(s * Mathf.Max(0.02f, Mathf.Abs(cos)), s, 1f);
            _idCardRt.localEulerAngles = new Vector3(0f, 0f, (1f - ease) * -9f);
            if (_idFront.gameObject.activeSelf != faceUp) _idFront.gameObject.SetActive(faceUp);
            if (_idBack.gameObject.activeSelf == faceUp) _idBack.gameObject.SetActive(!faceUp);
            if (_idScrim != null)
            {
                var c = UITheme.Scrim;
                _idScrim.color = new Color(c.r, c.g, c.b, c.a * ease);
            }
            if (_idHint != null)
            {
                var h = _idHint.color;
                _idHint.color = new Color(h.r, h.g, h.b, Mathf.Clamp01((t - 0.7f) / 0.3f));
            }
        }

        /// <summary>
        /// GIVING IT BACK: the card turns face down and goes home to the stool it came from - the reverse of the way
        /// it came up - and the layer switches off when it lands. A card already on its way back is left to finish.
        /// </summary>
        private void CloseId()
        {
            if (_idRoot == null || !_idRoot.gameObject.activeSelf)
            {
                _idVisit = null;
                return;
            }
            if (_idFlipDir >= 0) Sfx.Play("id_card_away", 0.7f);   // slid back into the wallet
            _idVisit = null;
            HideIdTip(false);
            if (Motion.Reduced)
            {
                _idFlipDir = 0;
                _idFlipT = 0f;
                HideIdTip(true);
                _idRoot.gameObject.SetActive(false);
                return;
            }
            _idFlipDir = -1;
        }

        // ── the card's hovers: open, hold, pin (the author: "Kimlik kartının içerisindeki hoverin üzerinde birkaç
        // saniye durunca hover sabit kalmalı mouse ile hoverin üstünde gezinebilmeli hoverden mouseu kaldırınca hover
        // yok olmalı, hoverin açılma ve kapanma animasyonu olmalı") ─────────────────────────────────────────────────
        //
        // A tip opens under the pointer and follows it while the pointer moves over its source. Rest on the source
        // for IdPinAfter and it stops following and STAYS - a bar along its foot fills to say it is about to - so the
        // pointer can be carried onto it and read it; it closes once the pointer is off both it and its source for a
        // breath (the gap between the two has to be crossable). It grows out of the pointer and shrinks back into it.
        private const float IdPinAfter = 1.5f, IdTipGrace = 0.28f, IdTipOpen = 0.14f, IdTipClose = 0.10f;
        private IdHover _idTipFor;
        private float _idTipRest, _idTipAway, _idTipShown;
        private bool _idTipPinned, _idTipClosing;
        private CanvasGroup _idTipGroup;
        private Image _idTipBar, _idTipPin, _idTipPinHit;
        private RectTransform _idTipBarRt;

        private void BuildIdTip()
        {
            _idRecipeTip = NewRect("CardTip", _idRoot);
            Place(_idRecipeTip, new Vector2(0.5f, 0.5f), new Vector2(TipW, 120), Vector2.zero);
            _idRecipeTip.pivot = new Vector2(0, 1);
            var tipBg = _idRecipeTip.gameObject.AddComponent<Image>();
            // THE BOOK'S OWN PAPER (2026-09-09), opaque, in the page frame's gold: a tip prints a page of the menu.
            tipBg.color = new Color(0.949f, 0.910f, 0.835f, 1f);
            tipBg.raycastTarget = true;                 // once pinned the pointer lives on it: a click must stop here
            _idRecipeTip.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // ...and not reach the scrim
            Frame(_idRecipeTip, 3f, new Color(0.788f, 0.510f, 0.169f, 0.98f));
            var grain = NewRect("Grain", _idRecipeTip);
            Stretch(grain, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            grain.SetAsFirstSibling();
            var grainImg = grain.gameObject.AddComponent<Image>();
            grainImg.sprite = ChromeArt.PaperGrain();
            grainImg.type = Image.Type.Tiled;
            grainImg.raycastTarget = false;
            _idRecipeTipBody = NewRect("Body", _idRecipeTip);
            Stretch(_idRecipeTipBody, Vector2.zero, Vector2.one, new Vector2(10, 9), new Vector2(-10, -6));
            // the hold bar along the foot, and the pin it turns into
            _idTipBarRt = NewRect("Hold", _idRecipeTip);
            _idTipBarRt.anchorMin = new Vector2(0, 0); _idTipBarRt.anchorMax = new Vector2(0, 0);
            _idTipBarRt.pivot = new Vector2(0, 0);
            _idTipBarRt.anchoredPosition = new Vector2(3f, 3f);
            _idTipBarRt.sizeDelta = new Vector2(0f, 3f);
            _idTipBar = _idTipBarRt.gameObject.AddComponent<Image>();
            _idTipBar.color = new Color(0.788f, 0.510f, 0.169f, 0.9f);
            _idTipBar.raycastTarget = false;
            // THE PIN IS A KEY (2026-09-25, the author: "hover sağ üstteki bir sabitleme butonuna basarak ekranın
            // sağına küçük postit gibi ... bilgi kutusu gelecek"). It was only the mark that said the tip had stopped
            // following the pointer; on a recipe it is now the key that pins the drink to the note on the right of
            // the screen (TycoonHud.Note). Shown faint while the tip still follows - it cannot be reached then - and
            // whole once the tip holds still, which is when the pointer can be carried onto it. A 22-unit key round
            // the 12-unit pin, so it is a thing to press rather than a pixel to hunt.
            var pinKey = NewRect("PinKey", _idRecipeTip);
            pinKey.anchorMin = pinKey.anchorMax = pinKey.pivot = new Vector2(1, 1);
            pinKey.sizeDelta = new Vector2(22f, 22f);
            pinKey.anchoredPosition = new Vector2(-1f, -1f);
            _idTipPinHit = pinKey.gameObject.AddComponent<Image>();
            _idTipPinHit.color = new Color(1f, 1f, 1f, 0f);
            _idTipPinHit.raycastTarget = true;
            var pinButton = pinKey.gameObject.AddComponent<Button>();
            pinButton.transition = Selectable.Transition.None;
            pinButton.onClick.AddListener(PinRecipeFromCard);
            var pin = NewRect("Pin", pinKey);
            pin.anchorMin = pin.anchorMax = pin.pivot = new Vector2(0.5f, 0.5f);
            pin.sizeDelta = new Vector2(12, 12);
            pin.anchoredPosition = Vector2.zero;
            _idTipPin = pin.gameObject.AddComponent<Image>();
            _idTipPin.sprite = IdArt.PinMark();
            _idTipPin.color = UITheme.ViceRed[3];
            _idTipPin.raycastTarget = false;
            _idTipGroup = _idRecipeTip.gameObject.AddComponent<CanvasGroup>();
            _idTipGroup.alpha = 0f;
            _idRecipeTip.gameObject.SetActive(false);
        }

        private void HideIdTip(bool instant)
        {
            if (_idRecipeTip == null) return;
            _idTipPinned = false;
            _idTipRest = 0f;
            _idTipAway = 0f;
            if (instant || !_idRecipeTip.gameObject.activeSelf)
            {
                _idTipFor = null;
                _idTipClosing = false;
                _idTipShown = 0f;
                _idRecipeTip.gameObject.SetActive(false);
                return;
            }
            _idTipClosing = true;
        }

        /// <summary>Which of the card's hovers the pointer is over, or null. Only while the card is face up and home.</summary>
        private IdHover IdHoverUnder(Vector2 screen)
        {
            if (_idFlipDir != 0 || _idFlipT < 1f || !_idFront.gameObject.activeInHierarchy) return null;
            foreach (var h in _idHovers)
                if (h.Rt != null && h.Rt.gameObject.activeInHierarchy
                    && RectTransformUtility.RectangleContainsScreenPoint(h.Rt, screen, null))
                    return h;
            return null;
        }

        private void StepIdTip()
        {
            if (_idRecipeTip == null) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            var screen = mouse.position.ReadValue();
            float dt = Time.unscaledDeltaTime;
            var under = IdHoverUnder(screen);
            bool open = _idRecipeTip.gameObject.activeSelf;
            bool overTip = open && RectTransformUtility.RectangleContainsScreenPoint(_idRecipeTip, screen, null);

            if (_idTipPinned)
            {
                // PINNED: it stays while the pointer is on it or on its source; another source takes over only if the
                // pointer went there without passing over the tip.
                if (overTip || under == _idTipFor) _idTipAway = 0f;
                else if (under != null && !overTip) { OpenIdTip(under); return; }
                else
                {
                    _idTipAway += dt;
                    if (_idTipAway >= IdTipGrace) HideIdTip(false);
                }
            }
            else if (under != null && !_idTipClosing && under == _idTipFor)
            {
                _idTipRest += dt;
                FollowPointerWithRecipeTip();
                if (_idTipRest >= IdPinAfter) { _idTipPinned = true; }
            }
            else if (under != null)
            {
                OpenIdTip(under);
            }
            else if (open && !_idTipClosing)
            {
                HideIdTip(false);
            }

            // the bar fills while the pointer rests; the pin shows once it has stopped following
            if (_idTipBarRt != null)
            {
                float full = _idRecipeTip.sizeDelta.x - 6f;
                float k = _idTipPinned ? 1f : Mathf.Clamp01(_idTipRest / IdPinAfter);
                _idTipBarRt.sizeDelta = new Vector2(full * k, 3f);
                _idTipBar.enabled = k > 0.02f;
                // On a recipe the pin is always there - faint while the tip follows, whole once it holds - because it
                // is the key to the note; on the card's other tips it keeps its old meaning, the tip holding still.
                bool recipe = _idTipFor != null && _idTipFor.Kind == IdTipKind.Recipe;
                _idTipPin.enabled = recipe || _idTipPinned;
                var pinInk = UITheme.ViceRed[3];
                _idTipPin.color = new Color(pinInk.r, pinInk.g, pinInk.b, _idTipPinned ? 1f : 0.4f);
                if (_idTipPinHit != null) _idTipPinHit.raycastTarget = recipe && _idTipPinned;
            }

            // IT GROWS OUT OF THE POINTER AND SHRINKS BACK INTO IT: the pivot is the corner nearest the pointer, so
            // scaling about it is scaling out of the pointer.
            if (!open) return;
            float want = _idTipClosing ? 0f : 1f;
            float speed = _idTipClosing ? 1f / IdTipClose : 1f / IdTipOpen;
            _idTipShown = Motion.Reduced ? want : Mathf.MoveTowards(_idTipShown, want, dt * speed);
            _idTipGroup.alpha = _idTipShown;
            float pop = Motion.Reduced ? 1f : Mathf.Lerp(0.72f, 1f, Tweening.OutCubic(_idTipShown));
            _idRecipeTip.localScale = new Vector3(pop, pop, 1f);
            if (_idTipClosing && _idTipShown <= 0f)
            {
                _idTipClosing = false;
                _idTipFor = null;
                _idRecipeTip.gameObject.SetActive(false);
            }
        }

        /// <summary>Builds the tip for a hover and opens it from nothing at the pointer.</summary>
        private void OpenIdTip(IdHover h)
        {
            _idTipFor = h;
            _idTipPinned = false;
            _idTipClosing = false;
            _idTipRest = 0f;
            _idTipAway = 0f;
            // A RECIPE IS A PAGE OF THE BOOK, AT THE BOOK'S OWN MEASURE (2026-09-23, the author: "bu menü
            // görüntüsüne ve tasarımına göre kimlikte alkol hoverini güncelle"). The recipe tip is the page's
            // column (BkColW) plus the tip's own 10-unit margins, so every block in it is the page's block at the
            // width it was measured at; the three small tips keep TipW.
            float tipW = h.Kind == IdTipKind.Recipe ? BkColW + 20f : TipW;
            float inner = tipW - 20f, bodyH;
            switch (h.Kind)
            {
                case IdTipKind.Recipe: bodyH = DrawRecipeCard(_idRecipeTipBody, OrderOnTheCard(), inner); break;
                case IdTipKind.Garnish: bodyH = DrawGarnishTip(_idRecipeTipBody, h.Garnish, inner); break;
                case IdTipKind.Stats: bodyH = DrawStatsTip(_idRecipeTipBody, inner); break;
                default: bodyH = DrawFlagTip(_idRecipeTipBody, inner); break;
            }
            _idRecipeTip.sizeDelta = new Vector2(tipW, bodyH + 18f);
            _idRecipeTip.gameObject.SetActive(true);
            _idRecipeTip.SetAsLastSibling();
            foreach (var g in _idRecipeTip.GetComponentsInChildren<Graphic>(true))
                if (g.gameObject != _idRecipeTip.gameObject) g.raycastTarget = false;
            // ...except the pin's key on a recipe, which StepIdTip arms once the tip holds still.
            if (_idTipPinHit != null) _idTipPinHit.gameObject.SetActive(h.Kind == IdTipKind.Recipe);
            _idTipShown = Motion.Reduced ? 1f : 0f;
            _idTipGroup.alpha = _idTipShown;
            FollowPointerWithRecipeTip();          // placed before its first frame is drawn
        }

        /// <summary>
        /// THE PIN, PRESSED: the drink on the card goes to the note on the right of the screen with this customer's
        /// asks and the name the card prints (a borrowed card's lender, as the card itself says), and the tip closes
        /// - the note is where it lives now.
        /// </summary>
        private void PinRecipeFromCard()
        {
            var r = OrderOnTheCard();
            if (r == null) return;
            IReadOnlyList<PreparationDefinition> asks = null;
            try { asks = _idVisit?.Order?.Garnishes; }
            catch (InvalidOperationException) { asks = null; }
            string whose = ((_idFirst != null ? _idFirst.text : "") + " " + (_idSurname != null ? _idSurname.text : "")).Trim();
            PinNote(r, asks, whose);
            HideIdTip(true);
        }

        private RecipeDefinition OrderOnTheCard()
        {
            try { return _idVisit?.Order?.Wanted; }
            catch (InvalidOperationException) { return null; }
        }

        private Text TipText(RectTransform host, string name, Font font, int size, Color colour, float y, float w, float h,
                             TextAnchor anchor = TextAnchor.UpperLeft, float x = 0f)
        {
            var t = NewText(name, host, font, size, anchor, colour);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            t.raycastTarget = false;
            return t;
        }

        private void ClearTip(RectTransform host)
        {
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);
        }

        /// <summary>A garnish: the dish it comes from as it stands on the counter, its name and what it is for.</summary>
        private float DrawGarnishTip(RectTransform host, PreparationDefinition g, float width)
        {
            ClearTip(host);
            if (g == null) return 0f;
            var pic = GarnishCounterArt(g.Id) ?? PrefArt.ForPreparation(g.Id);
            const float Box = 48f;
            if (pic != null)
            {
                var rt = NewRect("Pic", host);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(Box, Box);
                rt.anchoredPosition = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = pic; img.preserveAspect = true; img.raycastTarget = false;
            }
            float x = pic != null ? Box + 10f : 0f;
            var ink = new Color(0.30f, 0.16f, 0.05f);
            var title = TipText(host, "Title", _display, 16, ink, 4f, width - x, 20f, TextAnchor.UpperLeft, x);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = GarnishWord(g);
            var line = TipText(host, "Line", _body, 8, new Color(0.52f, 0.44f, 0.36f), 28f, width - x, 30f, TextAnchor.UpperLeft, x);
            line.text = GarnishPurpose(g.Id);
            return Mathf.Max(Box, 28f + Mathf.Max(12f, line.preferredHeight));
        }

        /// <summary>The visits and the stars, said in words.</summary>
        private float DrawStatsTip(RectTransform host, float width)
        {
            ClearTip(host);
            var ink = new Color(0.30f, 0.16f, 0.05f);
            var title = TipText(host, "Title", _display, 16, ink, 0f, width, 20f);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = _idVisitsShown <= 1 ? UIText.T("id.tip.first_visit") : UIText.N("id.tip.visits", _idVisitsShown);
            var line = TipText(host, "Line", _body, 8, new Color(0.52f, 0.44f, 0.36f), 26f, width, 14f);
            line.text = _idAverage >= 0 ? UIText.T("id.tip.rates", ("stars", _idAverage.ToString("0.0"))) : UIText.T("id.tip.rates_none");
            return 26f + 14f;
        }

        /// <summary>The flag: the country it is (for an altered card, the country it is NOT) and what it is.</summary>
        private float DrawFlagTip(RectTransform host, float width)
        {
            ClearTip(host);
            var flag = _idFlagIso != null ? ItemArt.Load("fl_" + _idFlagIso) : null;
            float x = 0f;
            if (flag != null)
            {
                var rt = NewRect("Flag", host);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(48f, 33f);
                rt.anchoredPosition = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = flag; img.raycastTarget = false;
                x = 58f;
            }
            string country = CountryNameOf(_idFlagIso);
            var ink = new Color(0.30f, 0.16f, 0.05f);
            var title = TipText(host, "Title", _display, 16, ink, 2f, width - x, 20f, TextAnchor.UpperLeft, x);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = country;
            var line = TipText(host, "Line", _body, 8, new Color(0.52f, 0.44f, 0.36f), 24f, width - x, 14f, TextAnchor.UpperLeft, x);
            line.text = UIText.T("id.tip.flag");
            return 36f;
        }

        /// <summary>A country's name from its flag code, in this language where the tables have it: the cast's own
        /// papers first, the strangers' after them.</summary>
        private string CountryNameOf(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "";
            string english = null;
            var cast = _bootstrap != null ? _bootstrap.Cast : null;
            if (cast != null) foreach (var p in cast.All) if (p.Iso == iso) { english = p.Country; break; }
            var strangers = _bootstrap != null ? _bootstrap.Strangers : null;
            if (english == null && strangers != null) foreach (var p in strangers.All) if (p.Iso == iso) { english = p.Country; break; }
            return UIText.Data("country", iso, "name", english ?? iso.ToUpperInvariant());
        }

        // ── the strangers ───────────────────────────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, Papers> _strangerOfPerson = new Dictionary<string, Papers>();
        private static readonly Dictionary<string, Sprite> s_strangerFaces = new Dictionary<string, Sprite>();

        /// <summary>
        /// WHOSE CARD A BORROWED ONE IS, since the eighth list (the author: "başka oyunumuzda dahil olmayan adamların
        /// görselleri"): a stranger from strangers.json, booked per PERSON once from a stable hash of their id - no
        /// stream is touched, and a returning minor shows the same stranger's card. False when there are no strangers
        /// to lend one (the file is missing, or none of their photographs is), and the caller falls back to a drinker's.
        /// </summary>
        private bool StrangerFor(CustomerVisit visit, out Papers papers, out Sprite face)
        {
            papers = null; face = null;
            string person = visit?.Regular?.Id;
            var strangers = _bootstrap != null ? _bootstrap.Strangers : null;
            if (person == null || strangers == null || strangers.All.Count == 0) return false;
            if (!_strangerOfPerson.TryGetValue(person, out papers))
            {
                int h = 29;
                foreach (char c in person) h = unchecked(h * 31 + c);
                papers = strangers.All[(h & 0x7FFFFFFF) % strangers.All.Count];
                _strangerOfPerson[person] = papers;
            }
            face = StrangerFace(papers.Slug);
            return face != null;
        }

        private static Sprite StrangerFace(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return null;
            if (s_strangerFaces.TryGetValue(slug, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("Strangers/" + slug);
            s_strangerFaces[slug] = s;
            return s;
        }
    }
}
