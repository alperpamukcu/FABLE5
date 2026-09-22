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
                    // a stranger's, since the eighth list; another drinker's only where there are no strangers
                    if (StrangerFor(visit, out var stranger, out _) && !string.IsNullOrEmpty(stranger.Name)) return stranger.Name;
                    var lent = PapersFor(LenderFor(visit, look));
                    if (lent != null && !string.IsNullOrEmpty(lent.Name)) return lent.Name;
                }
            }
            var papers = PapersFor(look);
            if (papers != null && !string.IsNullOrEmpty(papers.Name)) return papers.Name;
            return visit?.Regular != null && !string.IsNullOrEmpty(visit.Regular.Name)
                ? visit.Regular.Name : UIText.T("id.customer_fallback");
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
                || Showing(_dayEndPanel) || Showing(_pausePanel);
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
                _orderTipTitle.text = UIText.T("id.tip.ready");
                y += TitleH + Gap;
                foreach (Transform old in _orderTipBody) Destroy(old.gameObject);
                _orderTipBody.gameObject.SetActive(false);
                _orderTipPrefHead.gameObject.SetActive(false);
                _orderTipPrefs.gameObject.SetActive(false);
                _orderTipHint.gameObject.SetActive(true);
                _orderTipHint.rectTransform.anchoredPosition = new Vector2(Pad, -y);
                _orderTipHint.text = UIText.T("id.tip.click_to_read");
                y += 13f + Pad;
                SizeTip(OrderTipW, y);
                Show();
                return;
            }

            _orderTipTitle.text = UIText.Caps(RecipeTitle(visit.Order.Wanted));
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
                chips += PrefChip(PrefArt.ForPreparation(g.Id), GarnishWord(g),
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
                _orderTipPrefHead.text = UIText.T("id.tip.how_they_want_it");
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
        /// The document number. Deterministic in the person, so the same face carries the
        /// same licence every night of a run — a number that changed on re-entry would be
        /// the one field on the card that proves it is scenery.
        /// </summary>
        private static string LicenceNumber(string slug, string name)
        {
            string key = (!string.IsNullOrEmpty(slug) ? slug : "patron") + "|" + (name ?? "");
            int h = 17;
            unchecked
            {
                for (int i = 0; i < key.Length; i++) h = h * 31 + key[i];
            }
            h &= 0x7FFFFFFF;      // not Mathf.Abs: int.MinValue has no positive counterpart
            return string.Format("NA {0:0000} {1:0000}", h % 10000, h / 10000 % 10000);
        }

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
            catch (InvalidOperationException e) { Toast(UIText.Refusal(e)); return; }
            CloseId();
            Sfx.Play("kick_out", 0.8f);   // shown the door, and the door shuts behind them (2026-09-15)
            Toast(UIText.T("id.kick.shown_door", ("reason", UIText.Caps(KickReason(truth)))),
                visit.OffTheBooks ? (Color?)UITheme.Lime[3] : UITheme.ViceRed[3]);
        }

        /// <summary>Why somebody was shown the door, in the log's words — off the truth
        /// behind the card, which a kick has always read.</summary>
        private static string KickReason(IdPapers truth)
        {
            if (truth == null || !truth.ShouldBeKicked) return UIText.T("id.kick_reason.of_age");
            switch (truth.Forgery)
            {
                case Forgery.Altered: return UIText.T("id.kick_reason.altered");
                case Forgery.Borrowed: return UIText.T("id.kick_reason.borrowed");
                case Forgery.Copied: return UIText.T("id.kick_reason.copied");     // the cheap reprint (the eighth list)
                case Forgery.Drawn: return UIText.T("id.kick_reason.drawn");       // the card drawn by hand
                default: return UIText.T("id.kick_reason.under_age");
            }
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
        /// Whose card a borrowed one is WHEN THERE ARE NO STRANGERS TO LEND ONE (GDD 28 §3.1; since the eighth list
        /// a borrowed card is a stranger's, TycoonHud.IdCard.StrangerFor, and this is the fallback for a scene with no
        /// strangers file): booked per PERSON, once, from a stable hash of their id — no stream is touched, and a
        /// returning minor shows the same card. Never their own face, never a face with no papers, never a face on
        /// another stool this minute.
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
    }
}
