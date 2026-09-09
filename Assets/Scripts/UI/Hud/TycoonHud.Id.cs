using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Id: the licence and the order tip: the one door the hidden order opens through.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    public sealed partial class TycoonHud
    {
        /// <summary>
        /// The papers for a face — name, age, country, flag — read from the cast file.
        ///
        /// WHO A DRINKER IS on paper used to be written here: thirty people in a Dictionary
        /// in the middle of a UI class, chosen against the drawings (the age matches the face
        /// the artist drew; the eight non-American passports are the ones the picture itself
        /// argued for). That is content, and content is data — so it moved to
        /// Assets/Data/customers/papers.json on 2026-08-12, where a writer can add a person
        /// without opening C# and where the story's characters can share the same table
        /// (PLAN_last_call S0).
        /// </summary>
        private Papers PapersFor(PatronLook look) =>
            _bootstrap != null && _bootstrap.Cast != null && look != null
                ? _bootstrap.Cast.For(look.Slug ?? "")
                : null;

        /// <summary>This drinker's papers, or null for a look nobody has written up.</summary>
        /// <summary>
        /// What this drinker is called — the ONE name the bar says about them, wherever it
        /// says it: the licence prints it, the ticket over their head repeats it, the receipt
        /// shortens it to the first word.
        ///
        /// It belongs to the FACE and not to the archetype (2026-08-10, ShowId). The ticket
        /// went on reading the archetype's name for another day, so the card said MARILOU
        /// CABRERA over a photograph while the stool beside it said MARGUERITE — the same
        /// disagreement that fix was written for, one screen further out (the author,
        /// 2026-08-11: "kimlikteki isimlerle kafa üstündeki isimler eşleşmiyor"). A look with
        /// no papers on file falls back to the archetype's name, and the card falls back the
        /// same way, so the two agree even when there is nothing to agree about.
        /// </summary>
        private string NameOn(CustomerVisit visit, PatronLook look)
        {
            // The story's guest carries their OWN name, not the borrowed face's (GDD 26 §1b).
            // Until Ece's portrait is drawn her plate wears somebody else's picture, and a
            // name read off that picture would introduce her as Serena Fontana.
            if (visit != null && visit.OnTheHouse && visit.Regular != null
                && !string.IsNullOrEmpty(visit.Regular.Name)) return visit.Regular.Name;
            // Once the card is read, a borrowed card's name is the LENDER's (GDD 28 §3.1): the
            // ticket over the head and the log print what the licence printed, so the name
            // is never a second, free tell.
            if (visit != null && visit.IdInspected && visit.Regular != null && !visit.OnTheHouse)
            {
                var truth = visit.Papers;
                if (truth != null && truth.Forgery == Forgery.Borrowed)
                {
                    var lent = PapersFor(LenderFor(visit, look));
                    if (lent != null && !string.IsNullOrEmpty(lent.Name)) return lent.Name;
                }
            }
            var papers = PapersFor(look);
            if (papers != null && !string.IsNullOrEmpty(papers.Name)) return papers.Name;
            return visit?.Regular != null && !string.IsNullOrEmpty(visit.Regular.Name)
                ? visit.Regular.Name : "Customer";
        }

        // ── the licence: read the customer (GDD 24 §5) ───────────────────────────

        /// <summary>The licence's open beat: the card comes up from this fraction of its size
        /// over this long, on the unscaled clock (2026-09-07).</summary>
        private const float IdOpenFrom = 0.86f, IdOpenSeconds = 0.14f;
        private RectTransform _idCardRt;
        private float _idOpenAt = -1f;

        /// <summary>Steps the card's arrival. Called every frame the HUD is stepped; costs a
        /// compare once the beat is over.</summary>
        private void StepIdOpen()
        {
            if (_idCardRt == null || _idOpenAt < 0f) return;
            float k = Mathf.Clamp01((Time.unscaledTime - _idOpenAt) / IdOpenSeconds);
            _idCardRt.localScale = Vector3.one * Mathf.Lerp(IdOpenFrom, 1f, Tweening.OutCubic(k));
            if (k >= 1f) _idOpenAt = -1f;
        }

        private void ShowId(CustomerVisit visit)
        {
            if (visit?.Regular == null) return;
            _idVisit = visit;
            var reg = visit.Regular;

            // Opening the card IS the inspection (v5 C3): this is the one gate Core opens the
            // order through, so everything below may read it — and the bubble may from now on.
            visit.InspectId();
            Sfx.Play("id_card", 0.85f);   // a card slid out and laid on the counter

            if (_ledgerPanel != null) _ledgerPanel.gameObject.SetActive(false);
            _idRoot.gameObject.SetActive(true);
            // IT ARRIVES (2026-09-07, the author: "kimligin acilma animasyonu olsun"). A card
            // slid across the bar should not simply be there; it comes up to size over
            // IdOpenSeconds on the unscaled clock, which is the same stepper every other
            // panel in this HUD uses, and snaps under Motion.Reduced.
            _idOpenAt = Time.unscaledTime;
            if (_idCardRt != null)
                _idCardRt.localScale = Motion.Reduced ? Vector3.one : Vector3.one * IdOpenFrom;
            // THE PHOTO IS THIS DRINKER (the author, 2026-08-09). It used to be the
            // ARCHETYPE's portrait — one picture for everyone off the late shift — while
            // eleven different people sit on the stool. Reading a customer is the game;
            // a licence that does not match the face in front of you is a licence that
            // teaches the player to stop looking. (The archetype portraits themselves were
            // swept on 2026-09-05: every look carries its own face, so the fallback to one
            // never fired.)
            var idLook = LookFor(visit);
            var ownLook = idLook;
            // THE CARD MAY NOT BE THEIRS (GDD 28 §3.1, 2026-09-05). A borrowed card prints
            // somebody else's papers — photo, name, age, country, flag — and the tell is that
            // the person on the stool is not the person on the card. The lender is booked
            // per person, once, so a returning minor shows the same stranger's card.
            IdPapers truth = visit.Papers;
            if (truth != null && truth.Forgery == Forgery.Borrowed)
                idLook = LenderFor(visit, ownLook) ?? idLook;
            if (_idKick != null) _idKick.gameObject.SetActive(!visit.OnTheHouse);
            _idPhoto.sprite = idLook?.Face;
            _idPhoto.color = _idPhoto.sprite != null ? Color.white : UITheme.Night[3];

            // THE PAPERS BELONG TO THE FACE, NOT TO THE ARCHETYPE (the author, 2026-08-10:
            // the licence and the guide disagreed). A regular's name used to come out of the
            // archetype's pool while their PICTURE came from the look — so the card said
            // "Marguerite" over a portrait the guide calls Marilou Cabrera, and reading a
            // customer became impossible on purpose. The look is the person now: it carries
            // the photo, the name, the age and the citizenship, and the archetype keeps only
            // what it is actually about — how they came in, and how well you know them.
            var idPapers = PapersFor(idLook);
            string idFullName = NameOn(visit, idLook).ToUpperInvariant();
            // Bold: the name is the headline of the document, and it was printing at the
            // same weight as the age on the rule under it.
            _idName.text = "<b>" + idFullName + "</b>";
            // An honest minor's card says how old they are (GDD 28 §2.1): the printed age is
            // the tell, and it is the truth's, not the face's.
            // ...and an ALTERED card prints the bumped year on their own face (H6): both are
            // the truth's PrintedAge; only a borrowed card wears the lender's paper age.
            int shownAge = truth != null && truth.IsMinor && truth.Forgery != Forgery.Borrowed
                ? truth.PrintedAge : (idPapers != null ? idPapers.Age : reg.Age);
            _idAgeFrom.text = shownAge.ToString();
            _idCitizen.text = (idPapers != null ? idPapers.Country : reg.Hometown).ToUpperInvariant();
            _idNumber.text = LicenceNumber(idLook, idFullName);
            if (_idFlag != null)
            {
                // THE ALTERED CARD'S TELL (GDD 28 §2.1, H6): their own face and name, the
                // year bumped — and a flag that is not their country's, chosen off the
                // person's id so the same card lies the same way every time it is shown.
                string iso = idPapers != null ? idPapers.Iso : null;
                if (truth != null && truth.Forgery == Forgery.Altered) iso = WrongFlagFor(visit, iso);
                _idFlag.sprite = iso != null ? ItemArt.Load("fl_" + iso) : null;
                // A citizenship with no flag drawn shows nothing rather than a white box.
                _idFlag.enabled = _idFlag.sprite != null;
            }
            // THE TWO DATA CELLS. The count is per FACE, not per archetype: this card says
            // Miles Corrigan over Miles Corrigan's photograph, so "how many times" has to
            // mean how many times HE came in, which is what the departure log books.
            var rec = LogFor(ownLook);      // the record is the PERSON's, whoever's card it is
            _idVisitCount.text = (rec.Visits + 1).ToString();     // this one counts as they sit
            // ...and as punches (v4): one a visit, five holes, "+N" past that.
            int visits = rec.Visits + 1;
            if (_idPunches != null)
                for (int i = 0; i < _idPunches.Length; i++)
                    if (_idPunches[i] != null)
                        _idPunches[i].color = i < visits ? UITheme.Magenta[3] : new Color(0.62f, 0.58f, 0.50f, 0.55f);
            if (_idVisitMore != null) _idVisitMore.text = visits > 5 ? "+" + (visits - 5) : "";
            _idRel.text = rec.Visits == 0
                ? "FIRST TIME"
                : reg.Relationship.ToString().ToUpperInvariant();
            // …and the same fact as a bond: a stranger lights none of the three.
            int bond = rec.Visits == 0 ? 0 : (int)reg.Relationship;
            if (_idBond != null)
                for (int i = 0; i < _idBond.Length; i++)
                    if (_idBond[i] != null) _idBond[i].enabled = i < bond;

            // What THEY make of US, in the stars they have actually left. Somebody who has
            // not rated the bar yet KEEPS THE ROW — five grey stars and a question mark —
            // because a blank box reads as a field that does not exist, while an empty row
            // of stars reads as a verdict not yet given, which is the true state.
            bool rated = rec.Ratings > 0;
            double avg = rated ? rec.Stars / rec.Ratings : 0.0;
            _idRates.text = rated ? avg.ToString("0.0") : "?";
            _idRates.color = rated ? UITheme.Night[1] : UITheme.Night[3];
            for (int i = 0; i < _idStars.Length; i++)
            {
                // A HALF STAR IS DRAWN AS A HALF (the author, 2026-08-11: "kimlikte yarım
                // yıldız tam yıldız olarak gözüküyor"). Lighting the whole star from the
                // halfway mark printed 2.5 and 3.0 as the same row, which is the one thing
                // this row exists to tell apart. The top bar has always drawn the standing as
                // a continuous fill — the licence does now too, star by star.
                float fill = rated ? Mathf.Clamp01((float)avg - i) : 0f;
                _idStars[i].color = new Color(0.62f, 0.58f, 0.50f, rated ? 0.55f : 0.35f);
                _idStarFills[i].fillAmount = fill;
                _idStarFills[i].enabled = fill > 0.001f;
            }

            // No price, anywhere on the card (C3): the licence says who they are and what they
            // want, and what a drink costs is the menu's business.
            _idOrder.text = $"<b>{visit.Order.Wanted.Name.ToUpperInvariant()}</b>";
            // WHAT GOES IN IT, IN THE MENU'S OWN LANGUAGE (2026-09-08, the author:
            // "kimliklerdeki tarif ön izlemesini de menüdeki yeni tarif göstergesine uygun
            // yap"). This was a string join set at 8 — "GIN · VERMOUTH · BITTERS" — while the
            // book and this card's OWN hover tip both draw the spec panel: bottle art, stock
            // colour, shares. Three places, two languages, and the odd one out was the one
            // printed on the card the player is holding.
            //
            // It cannot be the panel itself: the card leaves 19 units between the ORDER rule
            // and the ENDORSEMENTS caption and a spec row is 20, so a cocktail's four rows
            // have nowhere to go. It takes the panel's language at the size the line has —
            // the same bottle silhouettes, one per ingredient, each carrying the panel's own
            // stock reading, so a bottle the bar cannot pour is as plain here as it is there.
            // The words go with the join: at 8 they were barely type, the icons are what the
            // panel exists to teach, and the hover tip still spells all of it out.
            foreach (Transform oldPart in _idOrderParts.rectTransform) Destroy(oldPart.gameObject);
            _idOrderParts.text = "";
            float partX = 0f;
            foreach (var band in visit.Order.Wanted.RatioRequirements)
            {
                // Every band in recipes.json is a STYLE band — counted 2026-09-08: 164 of
                // 164 across 54 recipes — so every one of them has a bottle to draw. The
                // fallback is for a hand-built recipe that has not, and it draws nothing
                // rather than a white blob that says nothing.
                var art = band.IsStyleBand && Run != null
                    ? ItemArt.StyleBottle(Run.CatalogueBottles, band.Style)
                    : null;
                if (art == null) continue;
                var chip = NewRect("P", _idOrderParts.rectTransform);
                Place(chip, new Vector2(0, 0.5f), new Vector2(OrderPartPx, OrderPartPx),
                    new Vector2(partX, 0f));
                chip.pivot = new Vector2(0, 0.5f);
                var img = chip.gameObject.AddComponent<Image>();
                img.sprite = art;
                img.preserveAspect = true;
                img.raycastTarget = false;
                // The panel's own reading: full ink for a bottle on the shelf, dimmed for one
                // the bar has not got. Same question, same answer, both places.
                bool stocked = !band.IsStyleBand || InStock(band.Style, band.MinTier);
                img.color = stocked ? Color.white : new Color(0.74f, 0.16f, 0.20f, 0.55f);
                partX += OrderPartPx + OrderPartGap;
            }
            _idOrderIcon.sprite = DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
            _idOrderIcon.enabled = _idOrderIcon.sprite != null;

            // The endorsements, drawn rather than listed (the author): each ask is a
            // pictogram with its word under it, and the read's fill preference joins them
            // as a glass marked with the band it wants — the empty space counted in the
            // numbers, which is the honest way to say how full a glass should be.
            foreach (Transform old in _idPrefRow) Destroy(old.gameObject);
            int chips = 0;
            // AND THE THING ITSELF UNDER THE POINTER (2026-09-09, the author: "kimlikte
            // hangi garnish istenildiği iconun üstüne gelindiğinde görseliyle gözükmeli").
            // The chip is a pictogram, which says WHICH of the four this is; hovering it
            // shows the dish as it stands on the counter, which says where to go and get it.
            foreach (var g in visit.Order.Garnishes)
                chips += PrefChip(PrefArt.ForPreparation(g.Id), g.Name.ToUpperInvariant(),
                                  picture: GarnishCounterArt(g.Id), detail: GarnishPurpose(g.Id));
            // (The SHAKEN HARD chip retired 2026-08-11: the method is the recipe's demand
            // now, printed where the recipe is — the spec panel and the book.)
            // No fill chip (the author, 2026-08-02): nobody demands a fill any more — the
            // only fill rule is the house floor, and it lives in the judge, not the licence.
            // A licence says NONE in an empty endorsements field rather than leaving it
            // blank, because blank means "not filled in" and NONE means "there are none".
            _idIntent.text = chips == 0 ? "NONE  ·  SERVE IT CLEAN" : "";
        }

        private void BuildOrderTip(RectTransform root)
        {
            _orderTip = NewRect("OrderTip", root);
            Place(_orderTip, new Vector2(0.5f, 0.5f), new Vector2(OrderTipW, 160f), Vector2.zero);
            _orderTip.pivot = new Vector2(0, 1);          // the position IS the top-left corner
            // Its own sorting layer, above the seats and their tickets — a tip drawn under the
            // thing it explains is not a tip.
            var canvas = _orderTip.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 26;
            var bg = _orderTip.gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.05f, 0.09f, 0.96f);
            bg.raycastTarget = false;
            var edge = new Color(UITheme.Cyan[3].r, UITheme.Cyan[3].g, UITheme.Cyan[3].b, 0.45f);
            Hairline(_orderTip, new Vector2(0, 0), new Vector2(1, 0), edge);
            Hairline(_orderTip, new Vector2(0, 1), new Vector2(1, 1), edge);
            HairlineV(_orderTip, 0f, edge);
            HairlineV(_orderTip, 1f, edge);

            // The drink's name is the HEADING (the author, 2026-08-11): it is the one thing
            // being answered, so it is set in the display face at 16 — a whole multiple of
            // the 8px design size, which is the only size a pixel font rasterises cleanly.
            _orderTipTitle = TipLine("Title", 16, TextAnchor.UpperLeft, UITheme.Amber[4],
                                     display: true);
            _orderTipBody = NewRect("Body", _orderTip);
            Place(_orderTipBody, new Vector2(0, 1), new Vector2(OrderTipW - 20f, 10f), Vector2.zero);
            _orderTipBody.pivot = new Vector2(0, 1);

            _orderTipPrefHead = TipLine("PrefHead", 8, TextAnchor.UpperLeft,
                new Color(0.61f, 0.58f, 0.66f));
            _orderTipPrefs = NewRect("Prefs", _orderTip);
            Place(_orderTipPrefs, new Vector2(0, 1), new Vector2(OrderTipW - 20f, 38f), Vector2.zero);
            _orderTipPrefs.pivot = new Vector2(0, 1);
            var row = _orderTipPrefs.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 5f;
            row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.UpperLeft;

            _orderTipHint = TipLine("Hint", 9, TextAnchor.UpperLeft, UITheme.Cyan[3]);

            _orderTip.gameObject.SetActive(false);
        }

        private Text TipLine(string name, int size, TextAnchor anchor, Color colour,
            bool display = false)
        {
            var t = NewText(name, _orderTip, display ? _display : _body, size, anchor, colour);
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(OrderTipW - 20f, size + 4f),
                Vector2.zero);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Whether something is open that the tip must not print over.</summary>
        private bool AnySheetOpen()
        {
            if (_flow != null && _flow.IsOpen) return true;
            // The bench belongs on this list for the same reason the guide does: it is a
            // sheet over the room, and a customer's order tip printing through it would be
            // the floor talking over a thing that covers the floor.
            return Showing(_idRoot) || Showing(_bookPanel) || Showing(_settingsPanel)
                || Showing(_guidePanel) || Showing(_devPanel) || Showing(_ledgerPanel)
                || Showing(_dayEndPanel);
        }

        /// <summary>
        /// Which seat's ticket the pointer is over, or −1.
        ///
        /// A rect test rather than an EventTrigger, and deliberately: the ticket's background
        /// takes no raycast, the seat under it is a button that opens the licence, and giving
        /// the ticket the pointer to win a hover would have taken the click away from the
        /// customer. Nothing about the input graph changes here.
        /// </summary>
        private int HoveredTicket()
        {
            if (AnySheetOpen()) return -1;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return -1;
            var p = mouse.position.ReadValue();
            for (int i = 0; i < _seats.Count; i++)
            {
                var tag = _seats[i].Tag;
                if (tag == null || !tag.gameObject.activeInHierarchy) continue;
                if (_seats[i].Visit == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(tag, p, null)) return i;
            }
            return -1;
        }

        private void UpdateOrderTip()
        {
            if (_orderTip == null) return;
            int seat = HoveredTicket();
            if (seat != _orderTipSeat)
            {
                _orderTipSeat = seat;
                if (seat < 0) _orderTip.gameObject.SetActive(false);
                else FillOrderTip(_seats[seat].Visit);
            }
            if (_orderTip.gameObject.activeSelf) FollowPointerWithOrderTip();
        }

        private void FillOrderTip(CustomerVisit visit)
        {
            if (visit == null) { _orderTip.gameObject.SetActive(false); return; }

            const float Pad = 10f, Gap = 8f, TitleH = 20f;
            float y = Pad;

            _orderTipTitle.rectTransform.anchoredPosition = new Vector2(Pad, -y);
            if (!visit.IdInspected)
            {
                // Unread. The card is the only thing that may answer, so this says where the
                // answer is and stops — no name, no drink, no hint of either.
                _orderTipTitle.text = "READY TO ORDER";
                y += TitleH + Gap;
                foreach (Transform old in _orderTipBody) Destroy(old.gameObject);
                _orderTipBody.gameObject.SetActive(false);
                _orderTipPrefHead.gameObject.SetActive(false);
                _orderTipPrefs.gameObject.SetActive(false);
                _orderTipHint.gameObject.SetActive(true);
                _orderTipHint.rectTransform.anchoredPosition = new Vector2(Pad, -y);
                _orderTipHint.text = "CLICK THEM TO READ THEIR ID";
                y += 13f + Pad;
                SizeTip(OrderTipW, y);
                Show();
                return;
            }

            _orderTipTitle.text = visit.Order.Wanted.Name.ToUpperInvariant();
            y += TitleH + Gap;
            // The heading may be wider than the pours under it — SEX ON THE BEACH in the
            // display face is. The box takes the widest thing it holds rather than clipping
            // the one thing the player came to read.
            float w = Mathf.Clamp(_orderTipTitle.preferredWidth + Pad * 2f, OrderTipW, OrderTipMaxW);

            _orderTipBody.gameObject.SetActive(true);
            _orderTipBody.anchoredPosition = new Vector2(Pad, -y);
            // JUST THE POUR (the author, 2026-08-11). The prep word, the fill line and the
            // glass name left this card: the glass is not the player's to pick — the run
            // chooses it from the recipe — and the prep is not in the match at all, which
            // reads only ratios. What is left is what actually goes in the glass.
            float specH = DrawRecipeSpec(_orderTipBody, visit.Order.Wanted, dark: true,
                width: w - Pad * 2f, poursOnly: true);
            _orderTipBody.sizeDelta = new Vector2(w - Pad * 2f, specH);
            y += specH;

            foreach (Transform old in _orderTipPrefs) Destroy(old.gameObject);
            int chips = 0;
            foreach (var g in visit.Order.Garnishes)
                chips += PrefChip(PrefArt.ForPreparation(g.Id), g.Name.ToUpperInvariant(),
                                  _orderTipPrefs);

            // Asking for nothing is said by there being nothing there. A line announcing that
            // the customer wants nothing is a line to read for no news, which is exactly what
            // was asked to go.
            _orderTipHint.gameObject.SetActive(false);
            _orderTipPrefHead.gameObject.SetActive(chips > 0);
            _orderTipPrefs.gameObject.SetActive(chips > 0);
            if (chips > 0)
            {
                y += Gap;
                _orderTipPrefHead.rectTransform.anchoredPosition = new Vector2(Pad, -y);
                _orderTipPrefHead.text = "HOW THEY WANT IT";
                y += 12f + 2f;
                _orderTipPrefs.anchoredPosition = new Vector2(Pad, -y);
                _orderTipPrefs.sizeDelta = new Vector2(w - Pad * 2f, 38f);
                y += 38f;
            }
            y += Pad;

            SizeTip(w, y);
            Show();

            void SizeTip(float width, float height)
            {
                _orderTip.sizeDelta = new Vector2(width, height);
                var titleRt = _orderTipTitle.rectTransform;
                titleRt.sizeDelta = new Vector2(width - Pad * 2f, titleRt.sizeDelta.y);
            }

            void Show()
            {
                _orderTip.gameObject.SetActive(true);
                // Rebuilt on every hover, so enforced on every hover: nothing in here may
                // take the pointer, or the tip becomes the thing the cursor is on and the
                // hover it is answering ends (the licence tip learned this the hard way).
                foreach (var g in _orderTip.GetComponentsInChildren<Graphic>(true))
                    g.raycastTarget = false;
                FollowPointerWithOrderTip();   // placed before its first frame is drawn
            }
        }

        /// <summary>Hangs off the pointer, and turns back at the edges of the safe frame
        /// rather than running off it.</summary>
        private void FollowPointerWithOrderTip()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || _hudRoot == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _hudRoot, mouse.position.ReadValue(), null, out Vector2 local)) return;

            const float Gap = 16f;
            Vector2 size = _orderTip.sizeDelta;
            float halfW = _hudRoot.rect.width * 0.5f, halfH = _hudRoot.rect.height * 0.5f;
            float x = local.x + Gap;
            if (x + size.x > halfW) x = local.x - Gap - size.x;
            float yTop = local.y - Gap;
            if (yTop - size.y < -halfH) yTop = local.y + Gap + size.y;
            _orderTip.anchoredPosition = new Vector2(x, yTop);
        }

        /// <summary>
        /// A caption in one of the rail's printed boxes, and the big value under it. The
        /// box itself is on the stock (licence_gen.py); this fills it.
        /// </summary>
        private Text LicCell(RectTransform card, float top, string caption, out Text captionText,
            float valueDrop = 22f, int valueSize = 24)
        {
            captionText = NewText("C_" + caption, card, _body, 8, TextAnchor.UpperCenter,
                UITheme.ClubBlue[2]);
            Place(captionText.rectTransform, new Vector2(0, 1), new Vector2(LicCellW, 12),
                new Vector2(LicCellX, -top - 6f));
            captionText.horizontalOverflow = HorizontalWrapMode.Overflow;
            captionText.text = caption;
            // The drop is a parameter because the two cells do not hold the same thing: one
            // is a caption over a number, the other is a caption over a row of stars with
            // the number under THEM. Sharing a fixed drop printed the rating straight
            // through its own third star.
            var val = NewText("V_" + caption, card, _display, valueSize, TextAnchor.UpperCenter,
                UITheme.Night[1]);
            Place(val.rectTransform, new Vector2(0, 1), new Vector2(LicCellW, valueSize + 2f),
                new Vector2(LicCellX, -top - valueDrop));
            val.horizontalOverflow = HorizontalWrapMode.Overflow;
            return val;
        }

        /// <summary>
        /// The document number. Deterministic in the person, so the same face carries the
        /// same licence every night of a run — a number that changed on re-entry would be
        /// the one field on the card that proves it is scenery.
        /// </summary>
        private static string LicenceNumber(PatronLook look, string name)
        {
            string key = (look != null && !string.IsNullOrEmpty(look.Slug) ? look.Slug : "patron")
                + "|" + (name ?? "");
            int h = 17;
            unchecked
            {
                for (int i = 0; i < key.Length; i++) h = h * 31 + key[i];
            }
            h &= 0x7FFFFFFF;      // not Mathf.Abs: int.MinValue has no positive counterpart
            return string.Format("NA {0:0000} {1:0000}", h % 10000, h / 10000 % 10000);
        }

        /// <summary>0–5 stars as glyphs, the empty ones kept so the width never jumps.</summary>

        /// <summary>The key's size on the card: wide enough for the word on the bench's
        /// key, and inside the header band's 42 units.</summary>
        // THE CAP AT ITS OWN HEIGHT (v3, 2026-09-06, the author: "kick butonunu düzelt kick
        // yazısı butondan daha büyük buton tasarımı kötü"). The cap sprite is sliced with
        // fourteen units of top border and ten of bottom; at 30 tall that left SIX units of
        // face for a sixteen-pixel word, so the word stood taller than the key it was on.
        // 52 is the height the cap was drawn at, and the band is 72: it fits, with the word
        // on its face and the throw under it.
        // THE HOUSE KEY (v3.1, 2026-09-06, the author: "kick butonu değiştirilsin"). Two
        // arcade caps were sent back; this is the ONE key every other press in the game is
        // (GDD 16 §2, KeyPlate) in the door's red, the word on its face at the body face's
        // 16 — a key you already know how to press, in the colour that says what it does.
        private const float KickW = 84f, KickH = 30f;   // v4: inside the 42-unit band
        private RectTransform _idKick;

        /// <summary>
        /// THE KICK, FROM THE CARD (GDD 28 §4). The visit is read into a local first: the
        /// card closes itself the moment a visit stops waiting, and the local is what
        /// survives that. Core decides — right or wrong — and the toast says what the door
        /// said; a refused kick (unread card, already served) comes back as the rule's own
        /// words.
        /// </summary>
        private void KickTheOneOnTheCard()
        {
            var visit = _idVisit;
            var run = Run;
            if (visit == null || run == null) return;
            IdPapers truth = null;
            try { truth = visit.Papers; } catch (InvalidOperationException) { }
            try { run.Kick(visit); }
            catch (InvalidOperationException e) { Toast(e.Message.ToUpperInvariant()); return; }
            CloseId();
            Sfx.Play("deny", 0.8f);
            Toast("SHOWN THE DOOR · " + KickReason(truth).ToUpperInvariant(),
                visit.OffTheBooks ? (Color?)UITheme.Lime[3] : UITheme.ViceRed[3]);
        }

        /// <summary>Why somebody was shown the door, in the log's words — off the truth
        /// behind the card, which a kick has always read.</summary>
        private static string KickReason(IdPapers truth)
        {
            if (truth == null || !truth.ShouldBeKicked) return "they were of age";
            return truth.Forgery == Forgery.Altered ? "altered card"
                 : truth.IsForged ? "borrowed card" : "under age";
        }

        /// <summary>A flag that is NOT the country's, for an altered card: any other flag the
        /// roster draws, picked off the person's id so it never changes under the player.</summary>
        private string WrongFlagFor(CustomerVisit visit, string iso)
        {
            var pool = new List<string>();
            var cast = _bootstrap != null ? _bootstrap.Cast : null;
            if (cast != null)
                foreach (var p in cast.All)
                    if (!string.IsNullOrEmpty(p.Iso) && p.Iso != iso && !pool.Contains(p.Iso)
                        && ItemArt.Load("fl_" + p.Iso) != null)
                        pool.Add(p.Iso);
            if (pool.Count == 0) return iso;
            pool.Sort(string.CompareOrdinal);
            string person = visit?.Regular?.Id ?? "";
            int h = 23;
            foreach (char c in person) h = unchecked(h * 31 + c);
            return pool[(h & 0x7FFFFFFF) % pool.Count];
        }

        private static string KickReason(CustomerVisit visit)
        {
            IdPapers truth = null;
            try { truth = visit.Papers; } catch (InvalidOperationException) { }
            return KickReason(truth);
        }

        /// <summary>
        /// Whose card a borrowed one is (GDD 28 §3.1): booked per PERSON, once, from a stable
        /// hash of their id — no stream is touched, and a returning minor shows the same
        /// stranger's card. Never their own face, never a face with no papers, never a face
        /// on another stool this minute.
        /// </summary>
        private PatronLook LenderFor(CustomerVisit visit, PatronLook own)
        {
            string person = visit?.Regular?.Id;
            if (person == null || _looks.Count == 0) return null;
            if (_lenderOfPerson.TryGetValue(person, out var booked)) return booked;
            var pool = new List<PatronLook>();
            foreach (var look in _looks)
            {
                if (look == own || PapersFor(look) == null) continue;
                bool seated = false;
                foreach (var seat in _seats)
                    if (seat.Visit != null && seat.Visit != visit && seat.Look == look) { seated = true; break; }
                if (!seated) pool.Add(look);
            }
            if (pool.Count == 0) return null;
            int h = 17;
            foreach (char c in person) h = unchecked(h * 31 + c);
            var lender = pool[(h & 0x7FFFFFFF) % pool.Count];
            _lenderOfPerson[person] = lender;
            return lender;
        }

        private readonly Dictionary<string, PatronLook> _lenderOfPerson = new Dictionary<string, PatronLook>();

        /// <summary>A face that could pass for nineteen, by its papers.</summary>
        private bool IsYoung(PatronLook look) => PapersFor(look)?.Young == true;

        private void CloseId()
        {
            if (_idRoot != null && _idRoot.gameObject.activeSelf)
                Sfx.Play("id_card_away", 0.7f);
            _idVisit = null;
            if (_idRoot != null) _idRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// One licence line, SEATED on a rule: the value's bottom edge lands on the shell's own
        /// printed line (the way a form is filled in), with the small navy label just above it.
        /// Returns the value; the label comes back through <paramref name="labelText"/> so a
        /// row that is sometimes empty (a stranger has no rating yet) can hide whole.
        /// </summary>
        private Text LicenceField(RectTransform card, string label, float x, float lineY,
            float w, out Text labelText, int valueSize = 16)
        {
            float vh = valueSize + 6f;
            labelText = NewText("L_" + label, card, _body, 8, TextAnchor.LowerLeft, UITheme.ClubBlue[2]);
            Place(labelText.rectTransform, new Vector2(0, 1), new Vector2(w, 12), Vector2.zero);
            labelText.rectTransform.pivot = new Vector2(0, 0);
            labelText.rectTransform.anchoredPosition = new Vector2(x, -lineY + vh + 2f);
            labelText.text = label;
            var val = NewText("V_" + label, card, _display, valueSize, TextAnchor.LowerLeft, UITheme.Night[1]);
            val.supportRichText = true;
            val.horizontalOverflow = HorizontalWrapMode.Overflow;   // a licence never wraps; it runs
            Place(val.rectTransform, new Vector2(0, 1), new Vector2(w, vh), Vector2.zero);
            val.rectTransform.pivot = new Vector2(0, 0);
            val.rectTransform.anchoredPosition = new Vector2(x, -lineY + 2f);
            return val;
        }

        /// <summary>
        /// Everything PRINTED on the licence: the header band, the portrait well, the two data
        /// cells and the four rules of the field grid. Drawn from the same constants the text
        /// is placed with, so the two cannot drift apart (2026-09-06).
        /// </summary>
        private void LicenceFurniture(RectTransform card)
        {
            var band = NewRect("Band", card);
            // INSIDE THE PAPER (2026-09-07, the author: "kimlige koyulan serit resmi kimlik
            // gorselinden tasiyor"). It was the card's full width starting six units down,
            // so it ran out to the rounded corners AND left a strip of stock above itself.
            // The band keeps the margin every other element on this card keeps, and it starts
            // at the top: a header band with paper above it is not a header band.
            // ONE MARGIN FOR THE WHOLE CARD (2026-09-08, the author: "kutuların kenarlara
            // olan uzaklıkları da sabit olmalı"). The band was inset LicPad/2 = 9 while the
            // photo well and every field rule keep LicPad = 18, so the card had two left
            // edges nine units apart and read as off-centre however carefully anything else
            // was placed. It keeps the card's margin now, like everything else on it.
            Place(band, new Vector2(0, 1), new Vector2(LicW - LicPad * 2f, LicHeaderH),
                new Vector2(LicPad, -LicPad));
            band.pivot = new Vector2(0, 1);
            var bandImg = band.gameObject.AddComponent<Image>();
            // THE BEACH ON THE BAND (2026-09-07, the author: "kimliğin üst şeritine uygun
            // uzun bir görsel üret, plaj ve okyanus görseli olabilir"). A 200x16 golden-hour
            // panorama at the card's own three, edge to edge; the card's rounded corners
            // clip it (the card is a Mask). Without the picture the band is the old ink.
            var beach = ItemArt.Load("licence_band");
            bandImg.sprite = beach;
            bandImg.color = beach != null ? Color.white : UITheme.Night[1];
            bandImg.raycastTarget = false;
            // ...and a scrim under the type at the left, fading to the sea: the sunset is
            // the same value as cream type, and a licence has to be read.
            if (beach != null)
            {
                var scrim = NewRect("Scrim", band);
                Stretch(scrim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                scrim.offsetMax = new Vector2(-LicW * 0.42f, 0f);
                var scrimImg = scrim.gameObject.AddComponent<Image>();
                scrimImg.sprite = ChromeArt.FadeRight();
                scrimImg.color = new Color(0.05f, 0.03f, 0.08f, 0.82f);
                scrimImg.raycastTarget = false;
            }

            void Well(float x, float y, float w, float h, bool sunk)
            {
                var rt = NewRect("Well", card);
                Place(rt, new Vector2(0, 1), new Vector2(w, h), new Vector2(x, -y));
                rt.pivot = new Vector2(0, 1);
                var img = rt.gameObject.AddComponent<Image>();
                img.color = sunk ? new Color(0.84f, 0.79f, 0.70f, 1f)
                                 : new Color(0.30f, 0.16f, 0.05f, 0.10f);
                img.raycastTarget = false;
            }

            // The portrait well and the stamp strip under it: one column, one width.
            Well(LicPortrait.x, -LicPortrait.y, LicPortrait.width, LicPortrait.height, true);
            Well(LicPad, LicStampY, LicStripW, LicStampH, false);

            // The field grid's rules, and the one the licence number sits on.
            void Rule(float x, float y, float w, float alpha)
            {
                var rt = NewRect("Rule", card);
                Place(rt, new Vector2(0, 1), new Vector2(w, 1f), new Vector2(x, -y));
                rt.pivot = new Vector2(0, 1);
                var img = rt.gameObject.AddComponent<Image>();
                img.color = new Color(0.30f, 0.16f, 0.05f, alpha);
                img.raycastTarget = false;
            }
            foreach (float line in LicLines) Rule(LicGridX, line, LicGridW, 0.55f);
        }

        private void BuildIdCard(RectTransform root)
        {
            // ITS OWN LAYER, ABOVE THE BAR. The till was lifted to a canvas at 6 so it
            // would stand in front of the drinkers — and then it stood in front of the
            // licence and the market too, because both of those are ordinary children of
            // the HUD canvas at 5. Anything that is a WINDOW over the room needs to say so
            // rather than rely on being built late: stage -10, HUD 5, till 6, service flow
            // 12, recipe book 15, licence 20, the market 22.
            _idRoot = NewRect("IdCard", root);
            var idCanvas = _idRoot.gameObject.AddComponent<Canvas>();
            idCanvas.overrideSorting = true;
            idCanvas.sortingOrder = 20;
            _idRoot.gameObject.AddComponent<ForgivingRaycaster>();
            Stretch(_idRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _idRoot.gameObject.AddComponent<Image>();
            scrim.color = UITheme.Scrim;
            var scrimBtn = _idRoot.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(CloseId);

            var card = _idCardRt = NewRect("Card", _idRoot);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(LicW, LicH), new Vector2(0, 10));
            var shell = card.gameObject.AddComponent<Image>();
            // THE PAPER IS DRAWN (2026-09-06). The generated shell carried its band, wells and
            // rules baked in, so every field was placed by hand to land on furniture the code
            // could not see. The stock is a 9-slice now and everything printed on it is laid
            // out below, off the same numbers that place the type.
            shell.sprite = ChromeArt.LicencePaper();
            // TILED, NOT SLICED: the stock carries a security stipple, and a sliced centre
            // STRETCHES its three middle pixels into a wall of beige blocks (photographed).
            // Tiled repeats them, and the multiplier puts one art pixel on the card's own
            // three units so the stipple is the grain the rest of the game is drawn at.
            shell.type = Image.Type.Tiled;
            shell.pixelsPerUnitMultiplier = 1f / LicScale;
            shell.color = Color.white;
            // THE CARD CLIPS TO ITS OWN CORNERS (2026-09-07): the band's picture runs edge to
            // edge, and a rounded card with a square picture poking out of its corners is
            // not a rounded card. The paper is the mask; everything printed on it is cut to it.
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallow clicks

            LicenceFurniture(card);

            // THE BAND IS THE HEADER OF A LICENCE: the issuing authority on the left, the
            // document number on the right. It carried the NAME for a week, which read well
            // but is not where a licence puts a name — and the header it replaced was 320
            // units of ink identical on every card. The number fixes that: it is the one
            // header field that is different on all thirty-one.
            // STACKED at the band's left (v3): the authority over the document's class,
            // which leaves the band's right half to the key and the seal.
            // The band's own middle, measured from where it is actually placed above.
            float bandMid = -LicPad - LicHeaderH * 0.5f;
            // CLEAR OF THE EDGES (2026-09-07, the author: "şeritin üstündeki yazılar
            // çizgilere çok yakın, hizala"). The house name stood with its top on the band's
            // top rule and the class with its foot on the bottom one. The two lines are a
            // stack centred in the band now: 18 and 12 tall, four apart, eight from either
            // edge. THE HOUSE (the author: "mekan Miami, barın ismi Malibu Club"): the club
            // issues its patron licences, and the city is the class line's first word.
            var authority = NewText("Authority", card, _display, 16, TextAnchor.MiddleLeft,
                UITheme.Cream[4]);
            Place(authority.rectTransform, new Vector2(0, 1), new Vector2(240, 18),
                new Vector2(LicPad + 4f, bandMid + 8f));
            authority.horizontalOverflow = HorizontalWrapMode.Overflow;
            authority.text = "MALIBU CLUB";
            var docType = NewText("DocType", card, _body, 8, TextAnchor.MiddleLeft,
                new Color(0.80f, 0.86f, 0.96f, 1f));
            Place(docType.rectTransform, new Vector2(0, 1), new Vector2(170, 12),
                new Vector2(LicPad + 4f, bandMid - 9f));
            docType.horizontalOverflow = HorizontalWrapMode.Overflow;
            docType.text = "MIAMI  ·  PATRON LICENCE  ·";
            // The document number, on the band's second line (v4): the one header field
            // that differs on every card, beside the class.
            _idNumber = NewText("Num", card, _body, 8, TextAnchor.MiddleLeft,
                new Color(0.80f, 0.86f, 0.96f, 1f));
            Place(_idNumber.rectTransform, new Vector2(0, 1), new Vector2(160, 12),
                new Vector2(LicPad + 4f + 172f, bandMid - 9f));
            _idNumber.horizontalOverflow = HorizontalWrapMode.Overflow;

            // The flag rides the header, where a licence puts its emblem. It is the one
            // thing up here that changes from card to card besides the number below.
            // THE FLAG IN A ROUNDEL (v3, 2026-09-06, the author: "kimlikte bayrak görselleri
            // daha büyük olmalı ve dairesel çerçeve içerisinde bayrak olmalı"). A 30x20 flag
            // was a postage stamp in the band's corner. It is the licence's SEAL now: the
            // 16x11 flag at six times, cut to a disc 22 art pixels across — the flag's own
            // height, so it shows top to bottom and the disc trims only its ends — hung on
            // the band's lower edge in a ring of ink, the way a medallion hangs on a ribbon.
            // CENTRED IN THE BAND (v3.1, the author: "ülke bayrakları tam üstteki farklı
            // renk şeritin yükseklik bakımından orta konumuna getirilsin"): the band is 22
            // art pixels tall and so is the seal, so it fills the band's height exactly.
            // v4: the band is 14 art pixels, the seal 14, the flag at FOUR times (64x44) so it
            // covers the 42-unit disc top to bottom.
            // THE SEAL HOLDS ITS FLAG (2026-09-07, measured). The roundel was 14 * LicScale =
            // 42 units across and the flag inside it was 16 * 4 = 64 WIDE — a picture half as
            // wide again as the circle masking it, so the mask cut the flag into a shape and
            // the overspill read as a broken drawing lying across the KICK key. The flags are
            // drawn 48 x 33 since 2026-09-07, so the seal is cut to hold one at its drawn size
            // with a ring of air: 56 across, the flag 48 x 33 at 1:1. Whole multiples only —
            // this is a drawing of a flag, and a flag at 1.3x is a flag with some stripes
            // twice as thick as the others.
            const float Roundel = 56f, FlagW = 48f, FlagH = 33f;
            var sealAt = new Vector2(-(LicPad + Roundel * 0.5f), bandMid);
            var seal = NewRect("Seal", card);
            Place(seal, new Vector2(1, 1), new Vector2(Roundel, Roundel), sealAt);
            seal.pivot = new Vector2(0.5f, 0.5f);
            var sealImg = seal.gameObject.AddComponent<Image>();
            sealImg.sprite = ChromeArt.Roundel();
            sealImg.color = UITheme.Cream[3];       // the paper, where a country has no flag drawn
            sealImg.raycastTarget = false;
            seal.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var idFlag = NewRect("Flag", seal);
            Place(idFlag, new Vector2(0.5f, 0.5f), new Vector2(FlagW, FlagH), Vector2.zero);
            _idFlag = idFlag.gameObject.AddComponent<Image>();
            _idFlag.preserveAspect = false;         // six times exactly; the box is cut to it
            _idFlag.raycastTarget = false;
            _idFlag.enabled = false;
            var sealRing = NewRect("SealRing", card);
            Place(sealRing, new Vector2(1, 1), new Vector2(Roundel, Roundel), sealAt);
            sealRing.pivot = new Vector2(0.5f, 0.5f);
            var ringImg = sealRing.gameObject.AddComponent<Image>();
            ringImg.sprite = ChromeArt.RoundelRing();
            ringImg.color = UITheme.Night[1];
            ringImg.raycastTarget = false;

            // THE KICK KEY (GDD 28 §4, 2026-09-05, the author: "kimliğin üstündeki butondan
            // 'kick'leyebileceksin"). ON the card, in its header band, left of the flag —
            // never on the scrim, whose one meaning is close. The bench's red key
            // (ChromeArt.KeyCap): a cap that drops into its socket on press, the word riding
            // it. Hidden, not merely disabled, for the guest of the house.
            var kick = NewRect("Kick", card);
            // In the band, right of the type and left of the flag, with the same margin the
            // grid keeps (2026-09-06): the key is furniture on this card, not a floating button.
            Place(kick, new Vector2(1, 1), new Vector2(KickW, KickH),
                new Vector2(-(LicPad + Roundel + 12f), bandMid + KickH * 0.5f));
            var kickBtn = kick.gameObject.AddComponent<Button>();
            var kickFace = NewRect("Face", kick);
            Stretch(kickFace, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(kick, UITheme.ViceRed[2], kickBtn, kickFace);
            var kickWord = NewText("L", kickFace, _body, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Stretch(kickWord.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, KeyPlate.Throw), new Vector2(-4, 0));
            kickWord.text = "KICK";
            kickWord.raycastTarget = false;
            kickBtn.onClick.AddListener(KickTheOneOnTheCard);
            _idKick = kick;

            // A WHOLE 2x OF THE 72px FACE, centred in a well cut to fit it. Pixel art
            // magnifies only in whole steps, so 144 is not a taste — it is the only size
            // the photo can be drawn at on this card without resampling. The source is cut
            // at 1:1 too (patron_faces.py): the faces used to be measured per character and
            // pulled to 72, which magnified 26 of the 31 by a fraction and duplicated some
            // pixel rows and not others. That unevenness is what the author saw as the
            // photo being stretched, and it was.
            const float LicPhoto = 144f;
            var photo = NewRect("Photo", card);
            Place(photo, new Vector2(0, 1), new Vector2(LicPhoto, LicPhoto), new Vector2(
                LicPortrait.x + (LicPortrait.width - LicPhoto) * 0.5f,
                LicPortrait.y - (LicPortrait.height - LicPhoto) * 0.5f));
            _idPhoto = photo.gameObject.AddComponent<Image>();
            // The window and the sprite are both square, so this can never squash a face —
            // and a face that would not fit is cropped by the frame, never stretched to it.
            _idPhoto.preserveAspect = true;

            // ── the stamp strip, under the photograph (v4, 2026-09-06) ───────────
            // THE COUNTS AS STAMPS (the author: "verdiği yıldız ziyaret miktarı farklı bir
            // sunum ile gösterilmeli"). Two boxed figures under the photo read as a form;
            // this is a strip of punches and stars, the way a loyalty card is punched: the
            // first row is how often they have walked in — one punch a visit, five holes,
            // a "+N" past that — with the bond's three hearts at its end; the second row is
            // what they make of the place, five small stars filled to their average, with
            // the figure beside them. Two captions, one word each.
            float stripTop = -LicStampY;
            // CENTRED IN ITS WELL (2026-09-09, the author: "kimlikteki görselde paylaştığım
            // kutunun içindeki görsel ve yazıları tam ortala kutuya"). Both rows were pinned
            // to the well's top-left corner — caption on the margin, marks two units under
            // the rule — so a box drawn round them had all its air on the other two sides.
            // The block is a caption column plus five marks wide and two rows tall; what the
            // well has over that is split evenly, and every mark below is placed off these.
            const float StampMark = 12f, StampMarkGap = 2f;
            float stampBlockW = LicStampCol + 5f * (StampMark + StampMarkGap) - StampMarkGap;
            float stampBlockH = LicStampRow + 16f;
            float stampX = LicPad + Mathf.Max(0f, (LicStripW - stampBlockW) * 0.5f);
            float stampTop = stripTop - Mathf.Max(0f, (LicStampH - stampBlockH) * 0.5f);
            // AT SIXTEEN (2026-09-07, the author: "sol altta visit rate us fontlari hem
            // okunakli degil"). The body face is drawn on an 8px grid: at 8 it is half its
            // design size and every stroke lands between pixels, which is the hairline the
            // balloon was cured of on 2026-09-06 and this card still had.
            _idRelLabel = NewText("C_VISITS", card, _body, 16, TextAnchor.UpperLeft, UITheme.ClubBlue[2]);
            // ON THE CARD'S OWN MARGIN (2026-09-08): the photo well above this starts at
            // LicPad, and a caption four units adrift of the box it labels is the "tam
            // ortalanamamış" of the author's list, in miniature.
            // ROW ONE, and the caption heads it (2026-09-08). Each count gets a row with
            // its word at the head and its marks beside it — which is how a form is set, reads
            // left to right as a sentence, and is what a 54-unit well can actually hold.
            Place(_idRelLabel.rectTransform, new Vector2(0, 1), new Vector2(64, 16),
                new Vector2(stampX, stampTop));
            _idRelLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _idRelLabel.text = "VISITS";
            // ONE CAPTION PER ROW, because two do not fit (2026-09-07, measured in play).
            // The strip is LicRailW = 144 art px wide; at the readable 16 the two words are
            // 59 and 89 units of ink, which is 148 before either gets a gap, so side by side
            // they overlapped by 13 and printed "VISITSRATES US". They label two SEPARATE
            // rows of marks — the punches and the stars — so each caption goes above its own
            // row, which is what the rows were always for. The second one moves down with the
            // star row it names.
            // ...and RATED, not RATES US, at the head of the second column: the shorter word
            // is what fits the half-rail the stars need, and it says the same thing.
            _idRatesLabel = NewText("C_RATES", card, _body, 16, TextAnchor.UpperLeft, UITheme.ClubBlue[2]);
            Place(_idRatesLabel.rectTransform, new Vector2(0, 1), new Vector2(64, 16),
                new Vector2(stampX, stampTop - LicStampRow));
            _idRatesLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _idRatesLabel.text = "RATED";

            // ON THE GRID (2026-09-07, the author: "ikonografi hd durmuyor, yamuk pixelli
            // duruyor"). ChromeArt.Punch is drawn at 6x6, so it may be shown at 6, 12, 18 -
            // whole multiples, the same law the pixel faces keep. It was at 12 next to
            // 16-unit hearts and stars, which is a mark two thirds their size AND on a
            // different grid; 18 is 3x its drawing and stands with them.
            // TWELVE, not eighteen (2026-09-07): 18 is a clean 3x of the 6x6 drawing but five
            // of them plus their gaps is 105 units, and the left column is 72. 12 is a whole
            // 2x — still on the grid, which is the rule — and five fit in 68.
            const float PunchPx = 12f, PunchGap = 2f;
            _idPunches = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var p = NewRect("Punch" + i, card);
                Place(p, new Vector2(0, 1), new Vector2(PunchPx, PunchPx), new Vector2(
                    stampX + LicStampCol + i * (PunchPx + PunchGap), stampTop - 2f));
                _idPunches[i] = p.gameObject.AddComponent<Image>();
                _idPunches[i].sprite = ChromeArt.Punch();
                _idPunches[i].preserveAspect = true; _idPunches[i].raycastTarget = false;
            }
            _idVisitMore = NewText("More", card, _body, 16, TextAnchor.MiddleLeft, UITheme.Night[3]);
            // MEASURED, and it has nowhere to go (2026-09-07): the punches end at x92 and the
            // stars' column starts at x94, so a "+N" between them landed exactly on Star0.
            // It rides the VISITS caption instead — the caption is 58 units of ink in a
            // 90-unit box, so there is room after the word, and "VISITS  +3" is a better
            // sentence than a number floating over a star anyway.
            // Under the punches' own row is the only clear spot left, and only because the
            // punches stop at x92 while the stars begin at x94 — so the count rides the
            // LAST punch rather than sitting after it, drawn small and dark on the card.
            _idVisitMore.enabled = false;
            Place(_idVisitMore.rectTransform, new Vector2(0, 1), new Vector2(28, 16), new Vector2(
                stampX + stampBlockW - 28f, stampTop));
            _idVisitMore.horizontalOverflow = HorizontalWrapMode.Overflow;
            // The count itself is kept off the card (the punches are the count); it feeds
            // nothing now but is still written, so the old readers stay honest.
            _idVisitCount = NewText("V_VISITS", card, _body, 8, TextAnchor.MiddleLeft, UITheme.Night[3]);
            _idVisitCount.enabled = false;
            _idRel = NewText("Standing", card, _body, 8, TextAnchor.MiddleLeft, UITheme.Night[3]);
            _idRel.enabled = false;

            // THE BOND COMES OFF THE CARD (2026-09-08). It was three hearts at the end of a
            // row, and it has been moved four times in two days without ever fitting, so the
            // measurement was finally done properly: the well is 54 units tall (two rows —
            // the card gives it 18 art px) and 156 wide, and with each count's caption at the
            // head of its own row, row one spends 18..152 on VISITS and five punches and row
            // two 18..149 on RATED and five stars. Three 12px hearts want 38 units. Neither
            // row has them. There is no fifth place on the card either: the photo well is
            // flush to the strip, and the band is spent on the authority, the number, the
            // KICK key and the seal.
            //
            // So it goes, and it is the right one to lose: the bond is derived from the VISIT
            // COUNT (see the reader above — bond is Relationship, which counts visits), and
            // the punches on the row above ARE the visit count. It was the same fact twice,
            // and the card was overflowing to print it. The Image[] stays, built and disabled,
            // because the reader that lights it is honest and cheap and the day this card
            // grows a row is the day it comes back.
            const bool BondOnCard = false;
            const float BondPx = 12f, BondGap = 1f;
            _idBond = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var b = NewRect("Bond" + i, card);
                // CLOSING ROW TWO, inside the well (2026-09-08). They were measured off
                // LicRailW — the PHOTOGRAPH's width — so they sat half outside the strip's own
                // box and over the order field beside it, which is the "kalp iconlarinin
                // yerlestirmesinde de problem var" of the author's list. Measured off the
                // strip's own right edge they cannot leave it; and row two is where the spare
                // width is (row one leaves 28 units, row two 44, and three hearts want 38).
                // It belongs there as well as fitting: RATED and the bond are both what this
                // drinker makes of the bar, while the punches are how often they have come.
                Place(b, new Vector2(0, 1), new Vector2(BondPx, BondPx), new Vector2(
                    stampX + stampBlockW - (3 - i) * (BondPx + BondGap) + BondGap,
                    stampTop - 2f - LicStampRow));
                b.gameObject.SetActive(BondOnCard);
                var bs = b.gameObject.AddComponent<Image>();
                bs.sprite = ItemArt.Heart(false, BondPx);
                bs.preserveAspect = true; bs.raycastTarget = false;
                var f = NewRect("Lit", b);
                Stretch(f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _idBond[i] = f.gameObject.AddComponent<Image>();
                _idBond[i].sprite = ItemArt.Heart(true, BondPx);
                _idBond[i].preserveAspect = true; _idBond[i].raycastTarget = false;
            }

            // FIVE STARS, ALWAYS DRAWN, filled to the average — and the figure beside them.
            _idStars = new Image[5];
            _idStarFills = new Image[5];
            const float StarBox = 12f, StarGap = 1f;
            for (int i = 0; i < 5; i++)
            {
                var s = NewRect("Star" + i, card);
                Place(s, new Vector2(0, 1), new Vector2(StarBox, StarBox), new Vector2(
                    stampX + LicStampCol + i * (StarBox + StarGap),
                    stampTop - 2f - LicStampRow));
                _idStars[i] = s.gameObject.AddComponent<Image>();
                _idStars[i].sprite = ItemArt.Star(false, StarBox);
                _idStars[i].preserveAspect = true;
                _idStars[i].raycastTarget = false;
                var f = NewRect("Lit", s);
                Stretch(f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _idStarFills[i] = f.gameObject.AddComponent<Image>();
                _idStarFills[i].sprite = ItemArt.Star(true, StarBox);
                _idStarFills[i].type = Image.Type.Filled;
                _idStarFills[i].fillMethod = Image.FillMethod.Horizontal;
                _idStarFills[i].fillOrigin = (int)Image.OriginHorizontal.Left;
                _idStarFills[i].preserveAspect = true;
                _idStarFills[i].raycastTarget = false;
                _idStarFills[i].color = Color.white;
                _idStarFills[i].enabled = false;
            }
            _idRates = NewText("V_RATES", card, _display, 8, TextAnchor.MiddleRight, UITheme.Night[1]);
            Place(_idRates.rectTransform, new Vector2(0, 1), new Vector2(46, 16), new Vector2(
                stampX + LicStampCol, stampTop - 2f - LicStampRow));
            _idRates.enabled = false;
            _idRates.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── the numbered field grid ───────────────────────────────────────────
            // The numbers are what make a form read as a licence rather than as a label
            // printed on card stock, and they cost one character each.
            _idName = LicenceField(card, "1   NAME", LicFieldsX, LicLines[0], LicFieldsW, out _);
            _idAgeFrom = LicenceField(card, "2   AGE", LicFieldsX, LicLines[1], 100f, out _);
            _idCitizen = LicenceField(card, "3   CITIZEN OF", LicFieldsX + 100f, LicLines[1],
                LicFieldsW - 100f, out _);

            // The order, seated on its own rule with the glass drawn beside it.
            var idIcon = NewRect("OrderIcon", card);
            Place(idIcon, new Vector2(0, 1), new Vector2(30, 30), Vector2.zero);
            idIcon.pivot = new Vector2(0, 0);
            idIcon.anchoredPosition = new Vector2(LicFieldsX, -LicLines[2] + 2f);
            _idOrderIcon = idIcon.gameObject.AddComponent<Image>();
            _idOrderIcon.preserveAspect = true;
            _idOrderIcon.raycastTarget = false;
            _idOrder = LicenceField(card, "4   ORDER", LicFieldsX + 40f, LicLines[2],
                LicFieldsW - 40f, out _, 16);
            // What is IN it, under the name (v5 P16): the menu speaks styles now, so the
            // licence has to say gin-and-tonic, not just "Gin & Tonic" — this line is the
            // player's recipe knowledge since the band rows left with v2.
            // UNDER the order's own rule, which is where a sub-field belongs. It used to
            // share the serving-preferences caption row two rules down — the only place it
            // fitted on the old five-rule card — so what a drink was made of was printed
            // nowhere near the drink. The four-rule grid leaves 84 units under this rule
            // and the line needs twelve.
            _idOrderParts = NewText("OrderParts", card, _body, 8, TextAnchor.UpperLeft, UITheme.Night[3]);
            Place(_idOrderParts.rectTransform, new Vector2(0, 1), new Vector2(LicFieldsW, 14),
                new Vector2(LicFieldsX, -LicLines[2] - 6f));
            _idOrderParts.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Hovering the order shows the RECIPE (2026-07-31): the drink they asked for,
            // said the way the book says it — prep, pour shares, glass — without leaving
            // the card. The hit rect covers the order line, icon included.
            var orderHit = NewRect("OrderHit", card);
            Place(orderHit, new Vector2(0, 1), new Vector2(LicFieldsW, 52), Vector2.zero);
            orderHit.pivot = new Vector2(0, 0);
            orderHit.anchoredPosition = new Vector2(LicFieldsX, -LicLines[2] - 6f);
            var orderHitImg = orderHit.gameObject.AddComponent<Image>();
            orderHitImg.color = new Color(0, 0, 0, 0.001f);
            // VERTICAL and vice (the author, 2026-08-02): the cream chip vanished into the
            // cream card. A dark glass panel, cyan-edged, one pour to a line, the numbers
            // bright — parked over the seal corner where nothing else lives.
            // BESIDE the card, in the scrim's own margin (the author, 2026-08-02). Parked
            // over the fields it flickered: the panel took the pointer, which fired the
            // order line's PointerExit, which hid the panel, which handed the pointer back
            // — many times a second. A tip that covers the line you hovered cannot help you
            // read it anyway. The card is 714 wide on a 1280 canvas, so 252 clears it.
            _idRecipeTip = NewRect("RecipeTip", _idRoot);
            Place(_idRecipeTip, new Vector2(0.5f, 0.5f), new Vector2(TipW, 120), Vector2.zero);
            _idRecipeTip.sizeDelta = new Vector2(TipW, 120f);
            _idRecipeTip.pivot = new Vector2(0, 1);
            _idRecipeTip.anchoredPosition = new Vector2(LicW * 0.5f + 12f, LicH * 0.5f - LicLines[2] + 16f);
            var tipBg = _idRecipeTip.gameObject.AddComponent<Image>();
            // THE BOOK'S OWN PAPER (2026-09-09): the tip prints a page of the menu, so it is
            // set on the booklet's paper (menu_booklet.png, #F2E8D5) and bound in the page
            // frame's gold. OPAQUE — a card that lets the room through is a card the room
            // tints, and the author saw exactly that: "arkaplan renkleri uygun değil".
            tipBg.color = new Color(0.949f, 0.910f, 0.835f, 1f);
            // Nothing in the panel may take a raycast, or hovering it reads as leaving the
            // order line and the whole thing blinks.
            tipBg.raycastTarget = false;
            // A HOVER WEARS A THICK EDGE (2026-09-09, the author: "biraz daha hover gibi
            // olmalı, daha kalın kenarları olsun"). Four one-unit hairlines are what a FIELD
            // on a form is ruled with — this is a card that has come up over the room, and it
            // has to read as lifted. Three units of the page frame's own gold, with a dark
            // line outside them so the gold has something to sit against on any ground.
            var tipEdge = new Color(0.788f, 0.510f, 0.169f, 0.98f);   // menu_page_frame's gold
            Frame(_idRecipeTip, 3f, tipEdge);
            // AND A PATTERN THAT DOES NOT TAKE THE EYE (2026-09-09, the author: "arkaplana
            // göz almayacak bir desen uygula"): the house's paper grain, tiled at 1x under
            // everything the card prints. Four percent ink — it reads as paper, not as a
            // texture, and no line of type has to compete with it.
            //
            // (A drop shadow stood here for one build and was the fault the author saw: a
            // CHILD of the card draws OVER its background, so an 85%-opaque dark rect turned
            // the cream paper grey-brown. A shadow belongs behind the card, and the card has
            // no room behind it.)
            var grain = NewRect("Grain", _idRecipeTip);
            Stretch(grain, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            grain.SetAsFirstSibling();
            var grainImg = grain.gameObject.AddComponent<Image>();
            grainImg.sprite = ChromeArt.PaperGrain();
            grainImg.type = Image.Type.Tiled;
            grainImg.raycastTarget = false;
            _idRecipeTipBody = NewRect("Body", _idRecipeTip);
            Stretch(_idRecipeTipBody, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
            _idRecipeTip.gameObject.SetActive(false);
            var trig = orderHit.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowOrderRecipeTip());
            trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => _idRecipeTip.gameObject.SetActive(false));
            trig.triggers.Add(exit);

            // Serving preferences — the endorsements, drawn as pictograms (the author,
            // 2026-08-01) in the free band under the rule; the field text only survives to
            // say SERVE IT CLEAN when there is nothing to draw.
            _idIntent = LicenceField(card, "5   ENDORSEMENTS", LicFieldsX, LicLines[3],
                LicFieldsW, out _idIntentLabel, 8);
            // THE CHIPS SIT IN THE ROW, right of the caption (v3, 2026-09-06). They hung
            // under the fourth rule, which is under the card: 44 units of chip below a rule
            // 24 units from the paper's edge. The row is 81 tall and the caption is 12 of
            // it; a 38-unit chip stands in the rest, six units above the rule.
            _idPrefRow = NewRect("PrefRow", card);
            Place(_idPrefRow, new Vector2(0, 1), new Vector2(LicFieldsW - 150f, 38), Vector2.zero);
            _idPrefRow.pivot = new Vector2(0, 1);
            _idPrefRow.anchoredPosition = new Vector2(LicFieldsX + 150f, -LicLines[3] + 44f);
            var prefLayout = _idPrefRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            prefLayout.spacing = 8;
            prefLayout.childControlWidth = true; prefLayout.childForceExpandWidth = false;
            prefLayout.childControlHeight = true; prefLayout.childForceExpandHeight = false;
            prefLayout.childAlignment = TextAnchor.UpperLeft;

            var hint = NewText("Hint", _idRoot, _body, 12, TextAnchor.MiddleCenter, UITheme.TextSecondary);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400, 20),
                new Vector2(0, -(LicH * 0.5f) - 16f));
            hint.text = "CLICK OUTSIDE TO GIVE IT BACK";

            _idRoot.gameObject.SetActive(false);
        }
    }
}
