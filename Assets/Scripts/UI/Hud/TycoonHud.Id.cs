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
            int shownAge = truth != null && truth.IsMinor && truth.Forgery == Forgery.None
                ? truth.PrintedAge : (idPapers != null ? idPapers.Age : reg.Age);
            _idAgeFrom.text = shownAge.ToString();
            _idCitizen.text = (idPapers != null ? idPapers.Country : reg.Hometown).ToUpperInvariant();
            _idNumber.text = LicenceNumber(idLook, idFullName);
            if (_idFlag != null)
            {
                _idFlag.sprite = idPapers != null ? ItemArt.Load("fl_" + idPapers.Iso) : null;
                // A citizenship with no flag drawn shows nothing rather than a white box.
                _idFlag.enabled = _idFlag.sprite != null;
            }
            // THE TWO DATA CELLS. The count is per FACE, not per archetype: this card says
            // Miles Corrigan over Miles Corrigan's photograph, so "how many times" has to
            // mean how many times HE came in, which is what the departure log books.
            var rec = LogFor(ownLook);      // the record is the PERSON's, whoever's card it is
            _idVisitCount.text = (rec.Visits + 1).ToString();     // this one counts as they sit
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
            var parts = new List<string>();
            foreach (var band in visit.Order.Wanted.RatioRequirements)
                parts.Add((band.IsStyleBand ? band.Style.Replace('_', ' ') : band.Type.ToString())
                    .ToUpperInvariant());
            _idOrderParts.text = string.Join("  ·  ", parts);
            _idOrderIcon.sprite = DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
            _idOrderIcon.enabled = _idOrderIcon.sprite != null;

            // The endorsements, drawn rather than listed (the author): each ask is a
            // pictogram with its word under it, and the read's fill preference joins them
            // as a glass marked with the band it wants — the empty space counted in the
            // numbers, which is the honest way to say how full a glass should be.
            foreach (Transform old in _idPrefRow) Destroy(old.gameObject);
            int chips = 0;
            foreach (var g in visit.Order.Garnishes)
                chips += PrefChip(PrefArt.ForPreparation(g.Id), g.Name.ToUpperInvariant());
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
        private const float KickW = 128f, KickH = 34f;
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
            return truth.IsForged ? "borrowed card" : "under age";
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
            _idRoot.gameObject.AddComponent<GraphicRaycaster>();
            Stretch(_idRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _idRoot.gameObject.AddComponent<Image>();
            scrim.color = UITheme.Scrim;
            var scrimBtn = _idRoot.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(CloseId);

            var card = NewRect("Card", _idRoot);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(LicW, LicH), new Vector2(0, 10));
            var shell = card.gameObject.AddComponent<Image>();
            shell.sprite = ItemArt.Load("licence_shell3");
            if (shell.sprite == null) shell.sprite = ItemArt.Load("licence_shell2");
            if (shell.sprite == null) shell.color = UITheme.Cream[4];   // no art: a plain card
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallow clicks

            // THE BAND IS THE HEADER OF A LICENCE: the issuing authority on the left, the
            // document number on the right. It carried the NAME for a week, which read well
            // but is not where a licence puts a name — and the header it replaced was 320
            // units of ink identical on every card. The number fixes that: it is the one
            // header field that is different on all thirty-one.
            float bandMid = LicHeaderY - LicHeaderH * 0.5f;
            var authority = NewText("Authority", card, _display, 16, TextAnchor.MiddleLeft,
                UITheme.Cream[4]);
            Place(authority.rectTransform, new Vector2(0, 1), new Vector2(200, 18),
                new Vector2(56, bandMid + 6f));
            authority.horizontalOverflow = HorizontalWrapMode.Overflow;
            authority.text = "NEW ARDEN";
            var docType = NewText("DocType", card, _body, 8, TextAnchor.MiddleLeft,
                new Color(0.62f, 0.72f, 0.88f, 1f));
            Place(docType.rectTransform, new Vector2(0, 1), new Vector2(260, 12),
                new Vector2(220, bandMid + 5f));
            docType.horizontalOverflow = HorizontalWrapMode.Overflow;
            docType.text = "PATRON LICENCE  ·  CLASS B";

            // The flag rides the header, where a licence puts its emblem. It is the one
            // thing up here that changes from card to card besides the number below.
            var idFlag = NewRect("Flag", card);
            Place(idFlag, new Vector2(1, 1), new Vector2(24, 16),
                new Vector2(-59, bandMid + 8f));
            _idFlag = idFlag.gameObject.AddComponent<Image>();
            _idFlag.preserveAspect = true;
            _idFlag.raycastTarget = false;
            _idFlag.enabled = false;

            // THE KICK KEY (GDD 28 §4, 2026-09-05, the author: "kimliğin üstündeki butondan
            // 'kick'leyebileceksin"). ON the card, in its header band, left of the flag —
            // never on the scrim, whose one meaning is close. The bench's red key
            // (ChromeArt.KeyCap): a cap that drops into its socket on press, the word riding
            // it. Hidden, not merely disabled, for the guest of the house.
            var kick = NewRect("Kick", card);
            Place(kick, new Vector2(1, 1), new Vector2(KickW, KickH),
                new Vector2(-59 - 24 - 14, bandMid + KickH * 0.5f));
            var kickImg = kick.gameObject.AddComponent<Image>();
            kickImg.sprite = ChromeArt.KeyCap(UITheme.ViceRed, false, "kick");
            kickImg.type = Image.Type.Sliced;
            var kickBtn = kick.gameObject.AddComponent<Button>();
            kickBtn.targetGraphic = kickImg;
            kickBtn.transition = Selectable.Transition.SpriteSwap;
            var ks = kickBtn.spriteState;
            ks.pressedSprite = ChromeArt.KeyCap(UITheme.ViceRed, true, "kick");
            ks.selectedSprite = kickImg.sprite;
            kickBtn.spriteState = ks;
            var kickFace = NewRect("Face", kick);
            Stretch(kickFace, Vector2.zero, Vector2.one, Vector2.zero,
                new Vector2(0, -ChromeArt.KeyCapFaceUp));
            // Cream on the red cap, not the ramp's deepest step: at this key's height the
            // bench's dark-on-red word read as a smear in play (2026-09-05, photographed).
            var kickWord = NewText("L", kickFace, _body, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Stretch(kickWord.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            kickWord.text = "KICK";
            kickWord.raycastTarget = false;
            var kickTravel = kickFace.gameObject.AddComponent<KeyFaceTravel>();
            kickTravel.Button = kickBtn;
            kickTravel.Up = -ChromeArt.KeyCapFaceUp;
            kickTravel.Down = -ChromeArt.KeyCapFaceDown;
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

            // ── the rail's two data cells, under the photograph ────────────────────
            // A licence keeps its counts in boxes beside the picture, and these are facts
            // about the person rather than about the drink: how often they have walked in,
            // and what they have made of the place. The boxes themselves are printed on the
            // stock; what goes in them is lettered here.
            _idVisitCount = LicCell(card, LicCells[0], "VISITS", out _idRelLabel);
            _idRel = NewText("Standing", card, _body, 8, TextAnchor.UpperCenter, UITheme.Night[3]);
            Place(_idRel.rectTransform, new Vector2(0, 1), new Vector2(LicCellW, 12),
                new Vector2(LicCellX, -LicCells[0] - LicCellH + 16f));
            _idRel.horizontalOverflow = HorizontalWrapMode.Overflow;

            // HOW WELL THEY KNOW YOU, IN HEARTS (2026-09-04, the author: "bundan sonra
            // oyunda kalp ve yıldız iconu olarak her yerde bunları kullanacaksın"). The rank
            // is already a count — Stranger 0, Familiar 1, Regular 2, Confidant 3
            // (Relationships.ForSatisfiedVisits) — so it is a row of three, drawn the way
            // the star rows are: the sockets always there, the earned ones lit. The word
            // above it stays; the hearts are what you read across the bar, the word is what
            // it is called.
            const float BondPx = 10f, BondGap = 2f;
            float bondRun = 3f * BondPx + 2f * BondGap;
            _idBond = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var b = NewRect("Bond" + i, card);
                Place(b, new Vector2(0, 1), new Vector2(BondPx, BondPx), new Vector2(
                    LicCellX + (LicCellW - bondRun) * 0.5f + i * (BondPx + BondGap),
                    -LicCells[0] - LicCellH + 27f));
                var bs = b.gameObject.AddComponent<Image>();
                bs.sprite = ItemArt.Heart(false, BondPx);
                bs.preserveAspect = true; bs.raycastTarget = false;
                var f = NewRect("Lit", b);
                Stretch(f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _idBond[i] = f.gameObject.AddComponent<Image>();
                _idBond[i].sprite = ItemArt.Heart(true, BondPx);
                _idBond[i].preserveAspect = true; _idBond[i].raycastTarget = false;
            }

            // Caption, then the stars, then the number under them — so the drop clears the
            // star row rather than landing in the middle of it.
            _idRates = LicCell(card, LicCells[1], "RATES THIS BAR", out _idRatesLabel,
                valueDrop: 50f, valueSize: 16);
            // FIVE STARS, ALWAYS DRAWN. Somebody who has not rated the bar yet still gets
            // the row — greyed, with a question mark where the number goes — because a
            // blank box says "no such field" while five empty stars say "not yet".
            _idStars = new Image[5];
            _idStarFills = new Image[5];
            const float StarBox = 24f, StarGap = 2f;
            float starRun = 5f * StarBox + 4f * StarGap;
            for (int i = 0; i < 5; i++)
            {
                var s = NewRect("Star" + i, card);
                Place(s, new Vector2(0, 1), new Vector2(StarBox, StarBox), new Vector2(
                    LicCellX + (LicCellW - starRun) * 0.5f + i * (StarBox + StarGap),
                    -LicCells[1] - 24f));
                _idStars[i] = s.gameObject.AddComponent<Image>();
                _idStars[i].sprite = ItemArt.Star(false, StarBox);
                _idStars[i].preserveAspect = true;
                _idStars[i].raycastTarget = false;

                // The lit half, over the grey whole: a star fills from its left edge, so a
                // 2.5 leaves the third star half amber instead of rounding a customer's
                // opinion up to a verdict they did not give (see ShowId).
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

            // The licence number, on the rule that closes the rail.
            _idNumber = NewText("Num", card, _body, 8, TextAnchor.LowerCenter, UITheme.Night[3]);
            Place(_idNumber.rectTransform, new Vector2(0, 1), new Vector2(LicCellW, 12),
                new Vector2(LicCellX, -LicNumRule + 3f));
            _idNumber.rectTransform.pivot = new Vector2(0, 0);
            _idNumber.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── the numbered field grid ───────────────────────────────────────────
            // The numbers are what make a form read as a licence rather than as a label
            // printed on card stock, and they cost one character each.
            _idName = LicenceField(card, "1   NAME", LicFieldsX, LicLines[0], LicFieldsW, out _);
            _idAgeFrom = LicenceField(card, "2   AGE", LicFieldsX, LicLines[1], 100f, out _);
            _idCitizen = LicenceField(card, "3   CITIZEN OF", LicFieldsX + 130f, LicLines[1],
                LicFieldsW - 130f, out _);

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
            _idRecipeTip.pivot = new Vector2(0, 1);
            _idRecipeTip.anchoredPosition = new Vector2(LicW * 0.5f + 12f, LicH * 0.5f - LicLines[2] + 16f);
            var tipBg = _idRecipeTip.gameObject.AddComponent<Image>();
            tipBg.color = new Color(0.07f, 0.07f, 0.11f, 0.96f);
            // Nothing in the panel may take a raycast, or hovering it reads as leaving the
            // order line and the whole thing blinks.
            tipBg.raycastTarget = false;
            var tipEdge = new Color(UITheme.Cyan[3].r, UITheme.Cyan[3].g, UITheme.Cyan[3].b, 0.8f);
            Hairline(_idRecipeTip, new Vector2(0, 0), new Vector2(1, 0), tipEdge);
            Hairline(_idRecipeTip, new Vector2(0, 1), new Vector2(1, 1), tipEdge);
            HairlineV(_idRecipeTip, 0f, tipEdge);
            HairlineV(_idRecipeTip, 1f, tipEdge);
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
                LicFieldsW, out _idIntentLabel, 12);
            _idPrefRow = NewRect("PrefRow", card);
            Place(_idPrefRow, new Vector2(0, 1), new Vector2(LicFieldsW, 44), Vector2.zero);
            _idPrefRow.pivot = new Vector2(0, 1);
            _idPrefRow.anchoredPosition = new Vector2(LicFieldsX, -LicLines[3] - 6f);
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
