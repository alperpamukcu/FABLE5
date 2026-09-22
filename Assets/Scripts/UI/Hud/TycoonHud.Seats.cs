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
    // TycoonHud, part Seats: the floor: who is on which stool, how they walk in and out, what they order,.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    public sealed partial class TycoonHud
    {
        private void ResetSeats()
        {
            foreach (var v in _seats)
            {
                v.Visit = null;
                v.Note = default;
                HushSeat(v);
                v.Look = null;          // see AdvanceExit: a stool with nobody on it has no face
                v.Exiting = false;
                v.ExitT = 0f;
                v.WalkT = 0f;
                if (v.Group != null) v.Group.alpha = 1f;
                if (v.Root != null) v.Root.gameObject.SetActive(false);
                SyncPatronBody(v);
            }
        }

        /// <summary>Is there anybody still on the screen? A stool's view is hidden by
        /// <see cref="AdvanceExit"/> on the frame it reaches the door, and a settled tab
        /// keeps counting for a beat after that — both have to be finished, or the books
        /// land on top of the thing they are counting.</summary>
        private bool FloorIsClear()
        {
            if (_tabFloats > 0) return false;
            foreach (var v in _seats)
                if (v.Root != null && v.Root.gameObject.activeSelf) return false;
            return true;
        }

        // ── the floor ───────────────────────────────────────────────────────────


        /// <summary>The font on the counter, clicked: straight to the draught station. Nothing
        /// is checked here that the flow does not check itself — but a panel already open
        /// keeps the room, exactly as a seat does, because a click through an open sheet is
        /// how the bench lost its tin twice.</summary>
        private void OnTapClicked()
        {
            if (_flow != null && _flow.IsOpen) return;
            // ...and not with a drink on the go (2026-09-04, the author: "kokteyl yapim
            // esnasindayken bira yapma sahnesine girilmemeli"). The rule is Core's — the
            // flow asks it too, and BeginPull has always refused the keg — but the ROOM is
            // where the click happens, so the room is where it can be answered with words.
            // A door that opens onto a bench you cannot work is worse than a locked one.
            var run = Run;
            if (run != null && run.BuildingACocktail)
            {
                Toast(run.DrinkReady ? UIText.T("seats.tap.serve_first")
                                     : UIText.T("seats.tap.finish_first"));
                return;
            }
            _flow?.OpenTap();
        }

        private void OnSeatClicked(int index)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;   // finish the build first
            // NOT WITH THE ROOM LIFTED (2026-08-22, the author: "Ekran aşağı kayıkken yani
            // backbar açıkken servis yapılmamalı"). With the cellar open you are turned round
            // to the bar's own body, the drinkers have ridden up out of the way and their
            // tickets are down — a stool that still answered a click there would be serving
            // somebody you cannot see, from a room you are not facing.
            if (CellarOpen) return;
            var visit = _seats[index].Visit;
            if (visit == null) return;

            if (visit.State != VisitState.Waiting) return;
            if (!visit.HasOrdered) return;   // still deciding — no order to read yet (2026-07-23)

            // Clicking a customer reads their licence (GDD 24 §5), and that is ALL it does
            // again (2026-08-11): serving is dragging the glass onto them. One click, one
            // meaning — the click-to-serve road had a drink on the counter turning the
            // licence into a second-click affair, which is how you end up serving somebody
            // you meant to read.
            ShowId(visit);
        }

        /// <summary>
        /// THE TICKET'S BOTTOM ROW: what they want, a rule, then how they want it served.
        /// Returns how wide it came out — the plate is sized to its widest line and this row
        /// is very often it.
        ///
        /// Every mark is asked for by the PREPARATION'S OWN ID, so a preparation added to the
        /// garnish pool tomorrow needs a mask in ChromeArt and nothing else here. A mark that
        /// does not exist yet comes back null and leaves a gap rather than throwing — the
        /// same contract every other mark in this game is drawn under.
        ///
        /// Placed by hand, not by a layout group: this row is centred on a plate whose width
        /// is decided in the same frame, and a layout group would settle a frame late — which
        /// on a ticket that grows one character at a time is a row that visibly lags its own
        /// balloon. (16 §0: positions here are absolute, deliberately.)
        /// </summary>
        private float LayOutOrderIcons(SeatView view, CustomerVisit visit, bool show)
        {
            var row = view.IconRow;
            if (row == null) return 0f;
            if (!show || visit == null)
            {
                if (row.gameObject.activeSelf) row.gameObject.SetActive(false);
                return 0f;
            }
            if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);

            var drink = DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
            var spec = visit.Order.Garnishes;
            int marks = 0;
            for (int i = 0; i < view.Garnish.Length; i++)
            {
                // ONE DRAWING A GARNISH (2026-09-07, the author: "musterilerin kafasinin
                // ustundeki siparisteki sugar rim gorseli degistirilsin ... kimlikte
                // sugarrim lemon vs gibi garnish gorselleri esas alinsin"). The ticket drew
                // ChromeArt's 16px punched mask while the licence and the counter's dishes
                // drew PrefArt's pictogram, so the same ask was two pictures depending on
                // where it was read. PrefArt is the one that carries colour and reads at a
                // glance; ChromeArt's mark stays as the fallback for an id it has no drawing
                // for, which is what a new garnish arrives as.
                var mark = spec != null && i < spec.Count
                    ? (PrefArt.ForPreparation(spec[i].Id) ?? ChromeArt.Mark(spec[i].Id))
                    : null;
                view.Garnish[i].sprite = mark;
                view.Garnish[i].enabled = mark != null;
                if (mark != null) marks++;
            }
            if (view.Icon != null)
            {
                view.Icon.sprite = drink;
                view.Icon.enabled = drink != null;
            }

            // 24 for the drink because that is the size DrinkIcon draws a glass at, and 16 for
            // the marks because that is the size they are authored at. Neither is scaled: a
            // pixel drawing squeezed to fit a row it does not belong in is the fault this
            // whole ticket was rebuilt to stop making.
            const float DrinkW = 24f, MarkW = 16f, RuleW = 1f, Gap = 5f, MarkGap = 3f;
            float w = drink != null ? DrinkW : 0f;
            if (marks > 0)
            {
                if (w > 0f) w += Gap + RuleW + Gap;
                w += marks * MarkW + (marks - 1) * MarkGap;
            }
            row.sizeDelta = new Vector2(w, IconRowH);

            float x = 0f;
            if (drink != null)
            {
                view.Icon.rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += DrinkW;
            }
            if (view.IconRule != null)
            {
                bool ruled = marks > 0 && drink != null;
                view.IconRule.enabled = ruled;
                if (ruled)
                {
                    view.IconRule.rectTransform.anchoredPosition = new Vector2(x + Gap, 0f);
                    x += Gap + RuleW + Gap;
                }
            }
            for (int i = 0; i < view.Garnish.Length; i++)
            {
                if (!view.Garnish[i].enabled) continue;
                view.Garnish[i].rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += MarkW + MarkGap;
            }
            return w;
        }

        /// <summary>Which seat the pointer is over, or −1. A rect test on the stool's own
        /// hit plate, so the drop lands wherever the customer is standing rather than on a
        /// guessed column — and it costs nothing to ask five stools.</summary>
        private int SeatUnderPointer(Mouse mouse)
        {
            if (mouse == null) return -1;
            var p = mouse.position.ReadValue();
            for (int i = 0; i < _seats.Count; i++)
            {
                var root = _seats[i].Root;
                if (root == null || !root.gameObject.activeInHierarchy) continue;
                if (_seats[i].Visit == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(root, p, null)) return i;
            }
            return -1;
        }

        /// <summary>
        /// HOW LONG A LINE STAYS UP (2026-09-04, the author: "birkaç saniye görünüp sonra yok
        /// olmalı"). Long enough to read a clause of the game's own 8px face twice over at a
        /// comfortable pace, short enough that a room of six is never a wall of paper.
        /// </summary>
        private const float SaySeconds = 4.5f;

        /// <summary>How wide a spoken line is allowed to get before it wraps. Wider than the
        /// ticket's cap because a balloon is up for four seconds and a ticket is up all
        /// night: a line that overhangs a neighbour's head briefly is readable, one that
        /// parks there is a layout fault. Still inside the gap between two stools.</summary>
        // WIDER SINCE THE TYPE GREW (2026-09-06): the balloon is set at 16 now, and a
        // sentence that wrapped to two rows at 8 wants four at 16 inside a stool's gap.
        // A balloon is up for a few seconds and it is the thing being read, so it is
        // allowed to overhang a neighbour's head by half a stool while it is.
        private const float SayMaxW = SeatGap * 1.5f;
        /// <summary>The balloon's width cap while a bench is open over a lifted room (2026-09-13).</summary>
        private const float SayNarrowW = SeatGap * 0.7f;

        /// <summary>
        /// Puts one line in a drinker's balloon and starts its clock. An empty line says
        /// nothing at all — a pint with a good head and every garnish on it has no business
        /// making a speech, and silence there is the note working rather than failing.
        /// </summary>
        private void SayIt(SeatView view, string line)
        {
            if (view == null || view.Say == null || view.SayText == null) return;
            if (string.IsNullOrEmpty(line)) { HushSeat(view); return; }
            // AS WRITTEN (2026-09-07, the author: "müşteri konuşmalarında büyük küçük
            // kullanımına dikkat edelim"). The balloon shouted every line in capitals; a
            // spoken line is sentence case, and the pop-up over a serve is the one thing
            // up here that is allowed to shout.
            view.SayText.text = line;
            view.SayUntil = Time.unscaledTime + SaySeconds;
            RaiseBubble(view.Say);
            LayOutSay(view);
        }

        /// <summary>
        /// WHAT THIS DRINKER SAYS FOR THE MOMENT (2026-09-07, the author: "belli başlı
        /// aksanlarda konuşma şekilleri, konuşmalarına karakter katalım … her müşteri aynı
        /// cümleleri kurmaz"). The voice is the LOOK's — the person's own if the book names
        /// them, else their flag's, else the plain one — and the line is picked on the run's
        /// "voice" stream. Without a book (a scene with no voices file) the old fixed lines
        /// stand, so nothing up here ever comes up empty.
        /// </summary>
        private string VoiceLine(CustomerVisit visit, VoiceCue cue, string drink = null, string advice = null)
        {
            var book = _bootstrap != null ? _bootstrap.Voices : null;
            var run = Run;
            if (book == null || run == null || visit == null) return PlainLine(cue, advice);
            var look = LookFor(visit);
            var papers = PapersFor(look);
            var voice = book.For(look?.Slug, papers?.Iso) ?? book.Default;
            // THE SAME DRAW Say MAKES (VoiceBook.Pick, localization L1), looked up in the
            // player's language BEFORE the placeholders go in: a voice is rewritten per
            // language rather than translated, so {drink} and {advice} land wherever that
            // language's line puts them. The index is the line's place in its pool.
            var (index, english) = book.Pick(voice, cue, run.VoiceStream);
            string raw = index < 0 ? english
                : UIText.Data("voice", voice.Id + "." + cue.ToString().ToLowerInvariant(),
                              index.ToString(System.Globalization.CultureInfo.InvariantCulture), english);
            string said = VoiceBook.Fill(raw, drink, advice);
            return Sentence(string.IsNullOrEmpty(said) ? PlainLine(cue, advice) : said);
        }

        /// <summary>
        /// A SPOKEN LINE IS A SENTENCE (2026-09-08, the author: "konuşma balonlarında cümleler
        /// büyük harfle başlar küçük harfle biter"). Opens on a capital, closes on punctuation.
        ///
        /// It is applied at the END, on the way into the balloon, because the lines that come
        /// out wrong are the COMPOSED ones — a voice's praise glued to the coaching's "I asked
        /// for…" tail, a Title Case recipe name dropped in by {drink}, a whole sentence dropped
        /// into the middle of another by {advice} where {advice_l} was meant. Fixing those one
        /// at a time fixes the lines that exist today; fixing the ends here fixes every line
        /// the game will ever speak, including the ones the next voice brings.
        /// </summary>
        private static string Sentence(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            var t = line.Trim();
            if (t.Length == 0) return t;
            if (char.IsLower(t[0])) t = UIText.Caps(t.Substring(0, 1)) + t.Substring(1);
            char end = t[t.Length - 1];
            // A line that already ends in punctuation keeps it — including the '!' and '?'
            // that carry most of this bar's character.
            if (end != '.' && end != '!' && end != '?' && end != '…' && end != ':') t += ".";
            return t;
        }

        /// <summary>The lines the bar said before it had voices — the fallback, never the norm.</summary>
        private static string PlainLine(VoiceCue cue, string advice)
        {
            switch (cue)
            {
                case VoiceCue.Perfect: return UIText.T("seats.plain.perfect");
                case VoiceCue.Another: return UIText.T("seats.plain.another");
                case VoiceCue.Close: return UIText.T("seats.plain.close");
                case VoiceCue.Wrong: return UIText.T("seats.plain.wrong");
                case VoiceCue.Praise: return UIText.T("seats.plain.praise");
                case VoiceCue.Sip: return advice ?? string.Empty;
                default: return string.Empty;
            }
        }

        /// <summary>How many sips a drinker takes, and so how many things they can say.</summary>
        private const int SipsPerDrink = 3;

        /// <summary>How long after the glass lands before the first sip.</summary>
        private const float FirstSipAfter = 1.6f;

        /// <summary>And the gap between them. Three sips and a beat to read each: a savour is
        /// 13 seconds, so the last line lands with a third of the drink still to go.</summary>
        private const float SipEvery = 3.4f;

        /// <summary>
        /// THE DRINK, ONE SIP AT A TIME (2026-09-06, the author: "her yudumda yeni bir cümle
        /// ekleyecekler ... 2. cümleye geçerken 1. cümle silinmeyecek"). Each sip appends the
        /// next line to the balloon and types it out; the lines already said stay above it, so
        /// what builds up is a short verdict rather than three flashes of text. The balloon
        /// stays up while there is anything left to say and goes down when they do.
        /// </summary>
        private void StepSips(SeatView view, CustomerVisit visit)
        {
            if (view.SayLines == null || view.SayLines.Count == 0) return;
            if (visit == null || visit.State != VisitState.Drinking)
            {
                // Gone, or back to waiting with another order in hand: either way the glass
                // they were talking about is finished with, so the balloon comes down.
                view.SayLines = null;
                view.SaidLines = 0;
                if (view.SayUntil > 0f) HushSeat(view);
                return;
            }
            float now = Time.unscaledTime;
            if (view.SaidLines < view.SayLines.Count && now >= view.SayNextAt)
            {
                view.SaidLines++;
                view.SayTypeFrom = now;
                view.SayNextAt = now + SipEvery;
                Sfx.Play("hover", 0.12f);          // the glass going down again
                RaiseBubble(view.Say);
            }
            if (view.SaidLines == 0) return;

            // Everything said so far, and the line in progress typed out letter by letter —
            // the room's own speech rate (SpeakCps), and handed over whole under Reduced.
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < view.SaidLines - 1; i++)
            {
                sb.Append(view.SayLines[i]);
                sb.Append('\n');
            }
            string last = view.SayLines[view.SaidLines - 1];
            int shown = Motion.Reduced ? last.Length
                : Mathf.Clamp(Mathf.FloorToInt((now - view.SayTypeFrom) * SpeakCps), 0, last.Length);
            sb.Append(last.Substring(0, shown));
            string line = sb.ToString();
            if (view.SayText.text != line)
            {
                view.SayText.text = line;
                LayOutSay(view);
            }
            view.SayUntil = now + SaySeconds;      // never times out mid-drink
        }

        /// <summary>Puts a balloon up, and OPENS it if it was down (2026-09-22): a bubble that is already
        /// standing while its line grows a word does not pop again on every letter.</summary>
        private static void RaiseBubble(RectTransform rt)
        {
            if (rt == null) return;
            bool wasUp = rt.gameObject.activeSelf;
            rt.gameObject.SetActive(true);
            if (!wasUp)
            {
                var pop = rt.GetComponent<PopIn>();
                if (pop != null) pop.Play();
            }
        }

        /// <summary>Takes a balloon down and forgets what was in it.</summary>
        private static void HushSeat(SeatView view)
        {
            if (view == null || view.Say == null) return;
            view.SayUntil = 0f;
            if (view.SayText != null) view.SayText.text = "";
            if (view.Say.gameObject.activeSelf) view.Say.gameObject.SetActive(false);
        }

        /// <summary>
        /// THE BALLOON IS THE SIZE OF WHAT IS IN IT (the author: "baloncuk metnin boyutuna
        /// göre boyutlandırılacak"). One measurement, both ways: the line's own preferred
        /// width up to the cap, then however many rows that width forces, then the plate is
        /// the sum of them plus the padding and the balloon's own foot. The same arithmetic
        /// the ticket does — it is the same drawn balloon, and neither may ever clip type.
        /// </summary>
        private void LayOutSay(SeatView view)
        {
            var text = view.SayText;
            float widest = text.preferredWidth;
            // TALL AND THIN WHILE THE CELLAR IS OPEN (2026-09-13, the author: "daha ufak ve dikey
            // baloncukları olabilir"): the line wraps into a narrow column instead of a wide strip.
            float cardW = Mathf.Clamp(widest + TagPad * 2f, TagMinW, CellarOpen ? SayNarrowW : SayMaxW);
            float textW = cardW - TagPad * 2f;
            // MEASURED, NOT ESTIMATED (2026-09-06). Dividing the widest line by the card's
            // width guesses the row count, and a balloon that now holds THREE sentences —
            // each of which wraps on its own — was guessing two rows for six and hanging the
            // last of them over the drinker's head. The generator wraps exactly the way the
            // label will, so it is asked instead.
            float tall = view.SayLineH;
            if (text.text.Length > 0)
            {
                var settings = text.GetGenerationSettings(new Vector2(textW, 0f));
                tall = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings)
                     / Mathf.Max(0.0001f, text.pixelsPerUnit);
            }
            view.Say.sizeDelta = new Vector2(cardW, tall + TagPad * 2f + TagFoot);
        }

        /// <summary>
        /// NO TWO BALLOONS OVER EACH OTHER (2026-09-06, the author: "konuşma balonları üst
        /// üste binmemeli üst üste binecek şekilde sıkışırsa düzgün bir biçimde ayrılmalı.
        /// Balonun oku hep karakteri hedef almalı"). Every frame the balloons are stood back
        /// over their own heads, then walked left to right and any two that touch are pushed
        /// apart by half the overlap each, and the whole row is kept inside the picture.
        /// The TAIL is then set back over the head — the balloon may be shoved aside, the
        /// pointer is not — and clamped to the balloon's own rim so it never hangs in air.
        /// </summary>
        private void SeparateSays()
        {
            if (_seats == null) return;
            const float Gap = 8f, TailInset = 14f, MaxSlide = 48f;
            float halfRoom = _hudRoot != null ? _hudRoot.rect.width * 0.5f : 640f;
            var live = new System.Collections.Generic.List<SeatView>();
            var sig = new System.Text.StringBuilder();
            foreach (var v in _seats)
            {
                if (v?.Say == null || !v.Say.gameObject.activeInHierarchy) continue;
                // A LEAVER'S BALLOON IS NOT IN THE ROW (2026-09-08, the author: "konuşma
                // balonları ekranın sağ tarafına geçemiyor ortada takılıyor, oysa ekrandan
                // çıkarkende müşterileri takip etmeliydi"). The solver's job is to keep the
                // balloons of people AT THE BAR off each other and inside the picture; a
                // walk-out is neither. Their seat travels to halfRoom + OffscreenMargin, so
                // the inside-the-picture clamp below pinned the balloon at the frame's edge
                // while its owner walked out from under it — the tail stretching, the balloon
                // apparently stuck in mid-air. It is a child of the seat, so left alone it
                // simply travels with them, which is the whole ask.
                if (v.Exiting)
                {
                    v.Say.anchoredPosition = new Vector2(HeadX(v), SayHomeY(v));
                    if (v.SayTail != null)
                    {
                        v.SayTail.rectTransform.anchoredPosition = new Vector2(0f, 2f);
                        v.SayTail.rectTransform.sizeDelta = new Vector2(20f, 12f);
                    }
                    continue;
                }
                live.Add(v);
                sig.Append(v.Exiting ? 'x' : '.')
                   .Append(Mathf.RoundToInt(v.Root.anchoredPosition.x)).Append(':')
                   .Append(Mathf.RoundToInt(v.Say.sizeDelta.x)).Append('x')
                   .Append(Mathf.RoundToInt(v.Say.sizeDelta.y)).Append('@')
                   .Append(Mathf.RoundToInt(HeadX(v))).Append('/').Append(Mathf.RoundToInt(SayHomeY(v))).Append(';');
            }
            // SETTLED ROWS STAY SETTLED (2026-09-07, the author: "konuşma balonları gereksiz
            // kayabiliyor"). The row was re-solved every frame from scratch, so a balloon
            // slid whenever a neighbour's line grew by a letter. It is solved once for a
            // given set of balloons and sizes, and left alone until that set changes.
            // ÇEKMECE FAZI DA İMZANIN PARÇASI (2026-09-08, oyunda ölçülerek). Bu erken dönüş
            // balonların gereksiz kaymasını durdurmak için doğru; ama mahzen açılırken balon
            // kümesi de boyutları da değişmediğinden imza aynı kalıyordu, çözücü hiç
            // çalışmıyordu ve balonlar ne küçülüyor ne de banda taşınıyordu. Faz onda birlik
            // adımlarla imzaya giriyor: açılış boyunca birkaç kez çözülür, açıldıktan sonra
            // yine tek imzada durur.
            // Narrow or wide is decided by the drawer, so a change of drawer re-measures every
            // live balloon once before the row is solved (2026-09-13).
            if (CellarOpen != _saysNarrow)
            {
                _saysNarrow = CellarOpen;
                foreach (var v in live) LayOutSay(v);
                sig.Append('n');
            }
            sig.Append('|').Append(Mathf.RoundToInt((stage != null ? stage.DrawerPhase : 0f) * 10f));
            string now = sig.ToString();
            if (now == _saysSig) return;
            _saysSig = now;

            live.Sort((a, b) => a.Root.anchoredPosition.x.CompareTo(b.Root.anchoredPosition.x));
            // Each balloon's home: over its own head, on its own row. It may SLIDE a little
            // along the row to clear a neighbour, and if that is not enough it goes UP a
            // storey above the balloon it would have covered — never sideways over the
            // next drinker's head (the author: "karakter görsellerinin üstüne gelmeyecek
            // şekilde, dikey ve yatay olarak esnek").
            // WITH THE DRAWER OPEN THEY ARE SMALL, AND THEY SIT IN THE BAND (2026-09-08, the
            // author: "mahzen açıkken konuşma balonları üst bara taşıyor; küçülecek ve tezgah
            // ile üst bar arasında konumlandırılacak"). The room rides up when the cellar
            // opens and the balloons ride with it, straight through the top bar — which is why
            // they used to be taken down outright. Kept and shrunk instead: scaled to
            // CellarSayScale, and their row pinned into the gap between the counter's top and
            // the bar's underside so they cannot reach it however tall the speech is.
            float drawer = stage != null ? stage.DrawerPhase : 0f;
            float sayScale = Mathf.Lerp(1f, CellarSayScale, drawer);
            var placed = new System.Collections.Generic.List<Rect>();
            foreach (var v in live)
            {
                v.Say.localScale = Vector3.one * sayScale;
                float w = v.Say.sizeDelta.x * sayScale, h = v.Say.sizeDelta.y * sayScale;
                float rootX = v.Root.anchoredPosition.x + HeadX(v);
                float homeY = v.Say.anchoredPosition.y;   // the row it stands on
                float baseY = SayHomeY(v);
                if (homeY < baseY - 0.5f || homeY > baseY + 400f) homeY = baseY;
                homeY = baseY;
                // BANT, BALONUN KENDİ ORİJİNİNDE (2026-09-08, ölçülerek düzeltildi). İlk
                // hâli `barFloor - seatTop - h` idi: seatTop koltuğun HUD merkezine göre y'si,
                // balonun anchoredPosition'ı ise koltuğun KENDİ tabanına göre — iki ayrı
                // orijini çıkarınca balon ekranın ortasına düştü (ölçüldü: tepe -64).
                //
                // Doğrusu: balonun TEPESİ üst barın altında kalsın. Balonun tepesi koltuk
                // uzayında (homeY + dy + h), koltuğun HUD'daki y'si v.Root.anchoredPosition.y,
                // ve barın tabanı ekranın yarısı eksi bar yüksekliği. Hepsi HUD birimine
                // çevrilip tek eşitlikten çözülüyor.
                // MAHZEN AÇIKKEN BALON KAFANIN ALTINA İNER (2026-09-08, yazar: "konuşma
                // balonu kafalarının altında vücutlarının üstünde de olabilir").
                //
                // Bu izin, tavan hesabını tamamen gereksiz kıldı — ve iyi ki: koltuk uzayı
                // HUD'un TABANINA çapalı, balon ise koltuğun tabanına, aradaki 360 birimlik
                // ofsetle iki kez yanlış hesapladım (balon bir kez ekranın ortasına düştü,
                // bir kez barın 32 birim içine girdi, ikisi de oyunda ölçüldü).
                //
                // Başın ALTI zaten boş: gövdenin üstü, tezgahın üzerinde kalan bölge. Oraya
                // yerleşince üst bardan uzaklık kendiliğinden geliyor, çünkü konum yukarıdan
                // değil BAŞTAN aşağı ölçülüyor — mahzen ne kadar açılırsa açılsın balon başla
                // birlikte hareket eder ve arasındaki mesafe sabit kalır. Yüzü de kapatmaz:
                // kafa balonun üstünde durur.
                // OVER THE HEAD AGAIN, DRAWER OR NOT (2026-09-14, the author: "konuşma metinleri
                // karakterlerin kafasının üstünde çıkmıyor"). With the cellar open the balloon was
                // lerped to HeadTop − h − 10, which puts its TOP ten units under the crown: over
                // the face. It stays small in the band (CellarSayScale) and stands on the head.
                float ceiling = float.MaxValue;
                float dx = 0f, dy = 0f;
                // Inside the picture first.
                float left = rootX - w * 0.5f, right = rootX + w * 0.5f;
                // THE SEATS' OWN SPACE (2026-09-14): a stool's x runs 0..width from the HUD's left
                // edge, and this clamp was written for a centred origin — every stool right of the
                // middle failed it and had its balloon shoved toward the centre of the screen.
                float roomW = halfRoom * 2f;
                if (left < 4f) dx = 4f - left;
                else if (right > roomW - 4f) dx = (roomW - 4f) - right;
                for (int storey = 0; storey < 4; storey++)
                {
                    var mine = new Rect(rootX + dx - w * 0.5f, homeY + dy, w, h);
                    float push = 0f;
                    bool clear = true;
                    foreach (var p in placed)
                    {
                        if (mine.yMax + Gap <= p.yMin || mine.yMin >= p.yMax + Gap) continue;
                        float overlap = (p.xMax + Gap) - mine.xMin;
                        if (overlap <= 0f) continue;
                        clear = false;
                        push = Mathf.Max(push, overlap);
                    }
                    if (clear) break;
                    // A small slide along the row is allowed; a big one is a balloon over
                    // somebody else's head, so it climbs instead.
                    if (dx + push <= MaxSlide && rootX + dx + push + w * 0.5f <= halfRoom * 2f - 4f)
                    {
                        dx += push;
                        var again = new Rect(rootX + dx - w * 0.5f, homeY + dy, w, h);
                        bool ok = true;
                        foreach (var p in placed)
                            if (!(again.yMax + Gap <= p.yMin || again.yMin >= p.yMax + Gap)
                                && !(again.xMax + Gap <= p.xMin || again.xMin >= p.xMax + Gap)) ok = false;
                        if (ok) break;
                        dx -= push;
                    }
                    float top = float.MinValue;
                    foreach (var p in placed)
                        if (!(mine.xMax + Gap <= p.xMin || mine.xMin >= p.xMax + Gap))
                            top = Mathf.Max(top, p.yMax);
                    if (top == float.MinValue) break;
                    dy = top + Gap - homeY;
                    // ...ama mahzen açıkken yukarı tırmanmasın: balonu başın altına indiren
                    // şeyin tamamı bu, tırmanan bir balon onu geri bozar.
                    if (homeY + dy > ceiling) { dy = ceiling - homeY; break; }
                }
                // NEVER OVER OR UNDER THE TOP BAR (2026-09-13, the author: "o sahnede yazı balonları
                // üstbarın üstüne veya altına geçmemeli"). The balloon's top is held under the bar's
                // underside, measured through the transforms themselves — the origins of the seat
                // and the HUD have been got wrong by arithmetic twice (see the band note above).
                if (_hudRoot != null)
                {
                    var rootRect = v.Root.rect;
                    var topLocal = new Vector3(rootRect.center.x + dx, rootRect.yMin + homeY + dy + h, 0f);
                    var inHud = _hudRoot.InverseTransformPoint(v.Root.TransformPoint(topLocal));
                    float roof = _hudRoot.rect.yMax - TopBarH - 4f;
                    if (inHud.y > roof)
                    {
                        float unit = Mathf.Max(0.0001f, v.Root.lossyScale.y / Mathf.Max(0.0001f, _hudRoot.lossyScale.y));
                        dy -= (inHud.y - roof) / unit;
                    }
                }
                v.Say.anchoredPosition = new Vector2(HeadX(v) + dx, homeY + dy);
                placed.Add(new Rect(rootX + dx - w * 0.5f, homeY + dy, w, h));
                // The tail: over the head, on the balloon's underside. It used to grow with
                // the climb so a raised balloon still pointed at its drinker; with the
                // author's drawn tail (2026-09-08) that stretch read as a white spike a
                // hundred pixels tall, so the tail keeps its drawn size and a raised
                // balloon simply floats above its owner, the tail still over the head.
                var tail = v.SayTail != null ? v.SayTail.rectTransform : null;
                if (tail == null) continue;
                float half = w * 0.5f;
                float tailX = Mathf.Clamp(-dx, -half + TailInset, half - TailInset);
                // The tail is a child of the balloon, so it is already scaled — its numbers
                // are in the balloon's own units, and the climb has to be divided back out.
                float k = Mathf.Max(0.01f, sayScale);
                // The author's tail is drawn 10x6 and shown at 2x, its top row being the
                // body's own outline; it overlaps the body by that one row (2026-09-08).
                tail.anchoredPosition = new Vector2(tailX / k, 2f);
                tail.sizeDelta = new Vector2(20f, 12f);
            }
        }

        /// <summary>The "I asked for … as well." sentence riding on a sip's line, in the player's
        /// language, or null when it carries none: the whole line when it IS that sentence
        /// (<c>advice.missing</c>), its second half when it is a sentence followed by it
        /// (<c>advice.then</c>) — the same tail the English used to find by searching for
        /// "I asked for".</summary>
        private static string MissingTail(Line line)
        {
            if (line.Key == "advice.missing") return UIText.T(line);
            if (line.Key != "advice.then") return null;
            foreach (var arg in line.Args)
                if (arg.Key == "second" && arg.Value is Line second && second.Key == "advice.missing")
                    return UIText.T(second);
            return null;
        }

        /// <summary>Hands the ready drink to seat <paramref name="index"/> (the glass was dragged
        /// onto them). Returns true if it was served.</summary>
        private bool ServeSeat(int index)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return false;
            var visit = _seats[index].Visit;
            if (visit == null || visit.State != VisitState.Waiting) return false;
            if (!visit.HasOrdered) return false;   // can't hand a drink to someone still deciding
            if (!visit.IdInspected) return false;  // only TAKEN orders are servable (HUD rule, 2026-08-11)
            if (!run.DrinkReady) return false;     // only what is in the glass goes out

            // WAS IT ALREADY PERFECT BEFORE THIS ONE? Core keeps a set, not an event, so
            // the first time is a thing only the caller can see: ask before, ask after
            // (2026-08-25, the author: a perfect pour must announce itself). The key is
            // the ORDERED recipe — the same one TycoonRun files the perfect under.
            var asked = visit.Order.Wanted;
            bool knewItAlready = run.IsPerfected(asked.Id);

            // WHAT THEY WILL SAY ABOUT IT, TAKEN NOW (2026-09-04). ServeTo empties the
            // serving glass into the visit, so the pour can only be read on this side of the
            // call — and it is read ONCE and kept, because the note must not change while
            // they are drinking it. Against the ORDERED drink: that is the pour the player
            // was aiming at, so that is the pour worth coaching.
            // …and what they asked for ON it goes in with it: a garnish that was ordered
            // and never arrived is the second half of what they have to say. visit.Order is
            // legal here because ServeSeat has already refused an unread card.
            _seats[index].Note = PourAdvice.For(asked, run.ServingGlass,
                id => run.Shelf.Find(id)?.Ingredient, visit.Order.Spec);
            // ...AND THE WHOLE ORDER OF IT, one line a sip (2026-09-06, the author: "müşteriler
            // toplam 3 yudum alıyor, her yudumda yeni bir cümle ekleyecekler"). Read here for
            // the same reason the note is: ServeTo empties the serving glass, so this is the
            // last frame the pour can be looked at.
            // In the player's language (localization L1): SaidLines is Lines as string-table
            // lines — the same sips, in the same order — rendered here.
            var sipLines = PourAdvice.SaidLines(asked, run.ServingGlass,
                id => run.Shelf.Find(id)?.Ingredient, visit.Order.Spec, SipsPerDrink);
            _seats[index].SayLines = new System.Collections.Generic.List<string>(sipLines.Count);
            for (int L = 0; L < sipLines.Count; L++)
                _seats[index].SayLines.Add(Sentence(UIText.T(sipLines[L])));
            // IN THEIR OWN VOICE (2026-09-07): the first sip is the one with the character in
            // it — a flawless pour earns the voice's own praise (keeping any "I asked for…"
            // the coaching appended), and a pour with something to fix is told the way this
            // drinker tells things. The coaching sentence itself is PourAdvice's, untouched.
            var sips = _seats[index].SayLines;
            if (sips.Count > 0)
            {
                string first = sips[0];
                if (_seats[index].Note.Flawless)
                {
                    // The voice's praise, then any "I asked for…" the coaching carried, joined
                    // the way the coaching joins its own two sentences (advice.then).
                    string praise = VoiceLine(visit, VoiceCue.Praise);
                    string missing = MissingTail(sipLines[0]);
                    sips[0] = Sentence(missing != null
                        ? UIText.T("advice.then", ("first", praise), ("second", missing))
                        : praise);
                }
                else sips[0] = Sentence(VoiceLine(visit, VoiceCue.Sip, advice: first));
            }
            _seats[index].SaidLines = 0;
            _seats[index].SayNextAt = Time.unscaledTime + FirstSipAfter;
            // …and they SAY it, now, over the glass they were just handed — not when they
            // start drinking. An extra round never enters Drinking at all (CustomerVisit
            // .Resolve refreshes the order and stays Waiting), and the drink they are
            // commenting on is the one that just landed either way.
            HushSeat(_seats[index]);   // the first line comes with the first sip, not now

            var verdict = run.ServeTo(visit);
            CloseId();
            Sfx.Play("serve_clink");                          // the glass lands in front of them
            // The rarer faces (2026-09-08), held for the reaction beat so one face shows:
            // mind-blown for a flawless first make, in love for a regular's perfect drink.
            if (verdict.PerfectMake && !knewItAlready && run.IsPerfected(asked.Id))
                _seats[index].HeldEmote = EmoteBeat.Flawless;
            else if (verdict.PerfectMake && visit.Regular != null && visit.Regular.Relationship >= Relationship.Regular)
                _seats[index].HeldEmote = EmoteBeat.Bond;
            if (verdict.PerfectMake && !knewItAlready && run.IsPerfected(asked.Id))
                NotePerfect(asked);
            LogVerdict(visit, verdict);
            StartCoroutine(ServeReaction(index, verdict));   // reaction + payment float up
            return true;
        }

        // ── the bussing beat (D2, v5 P14) ────────────────────────────────────────
        // A drinker leaves the empty glass on the stool, and the stool stays blocked until it
        // is cleared: the player's click does it now, the bar's slow clock does it in seven
        // seconds. The prop appears where the customer sat; clicking it is the bussing.

        /// <summary>
        /// Whether Housekeeping still owns this mess. Core drops one the instant it is clean and CLEARS the whole
        /// list at closing (<c>SweepForClosing</c>), and a view holding the dropped object has a glass that cannot
        /// be collected, wiped or carried: every verb goes through <c>Own</c>, which refuses a mess off the list.
        /// </summary>
        private static bool OnTheCounter(TycoonRun run, CounterMess mess)
        {
            var messes = run?.Floor?.Messes;
            if (mess == null || messes == null) return false;
            for (int i = 0; i < messes.Count; i++) if (ReferenceEquals(messes[i], mess)) return true;
            return false;
        }

        private void RefreshDirtyGlasses(TycoonRun run)
        {
            foreach (var v in _seats)
            {
                // THE GLASS NOBODY COULD MOVE (2026-09-22, the author: "sahnenin ortasinda surukleyip yok
                // edemedigim bardak var oluyor bir sekilde"). The night's sweep empties the counter in Core, and
                // this view went on drawing the empty it had claimed, because the test for "is it still there"
                // was the mess's OWN state - and a swept mess still says it has a glass. So the picture stood on
                // the counter into the next night, refused the cloth, refused the carry, and answered every click
                // with "That is not on this counter." The view asks the counter now, not the mess.
                if (v.Dirty != null && !OnTheCounter(run, v.Dirty))
                {
                    v.Dirty = null;
                    if (_carriedEmpty == v)
                    {
                        _carriedEmpty = null;
                        _glassCarrying = false;
                        if (_glassCarry != null) _glassCarry.gameObject.SetActive(false);
                    }
                }
                // IN THE HAND MEANS OFF THE COUNTER (2026-09-08, the author: "bardaklar
                // lavaboya sürüklenirken bir silüet tezgahta kalmaya devam ediyor"). The
                // prop is drawn from the mess every frame, and Core keeps the glass IN the
                // mess until the sink takes it — so while it was being carried the counter
                // copy stood exactly where it had been, under the one in the hand. The seat
                // being carried draws no counter glass until the carry ends.
                bool show = v.Dirty != null && v.Dirty.HasGlass
                            && !(_glassCarrying && _carriedEmpty == v);
                if (show && v.DirtyProp == null)
                {
                    var prop = NewRect("DirtyGlass", _hudRoot);
                    prop.anchorMin = prop.anchorMax = new Vector2(0, 0);
                    prop.pivot = new Vector2(0.5f, 0);
                    prop.sizeDelta = new Vector2(34, 52);
                    var img = prop.gameObject.AddComponent<Image>();
                    // The SAME glass everywhere (the author, 2026-08-02): the empty on the
                    // counter is the drawn vessel the drink was served in, at its line's
                    // tier — not a stock photo of some other glass.
                    GlasswareDefinition dirtyDef = null;
                    if (run != null && v.Dirty.GlasswareId != null)
                        foreach (var g in run.Glassware)
                            if (g.Id == v.Dirty.GlasswareId) { dirtyDef = g; break; }
                    // If the line is unknown the glass stays UNDRAWN rather than borrowing
                    // a stock one — that is the same rule, held at its edge. The old
                    // `ItemArt.Glass` fallback did exactly what the rule forbids, and its
                    // art was a pre-v3 leftover deleted with the fridge; the colour set
                    // below is what a sprite-less prop is already dressed in.
                    if (dirtyDef != null)
                    {
                        var dirtyPiece = GlassArt.For(dirtyDef, run.GlassTier(dirtyDef.Id));
                        img.sprite = dirtyPiece.Sprite;
                        // ONE SCALE FOR THE SET (2026-09-06, GlassArt.TallestSheet): the
                        // sheets are trimmed per line, so fitting each one to the same box
                        // drew a tumbler as tall as a pint.
                        if (dirtyPiece.Sprite != null)
                            prop.sizeDelta = GlassArt.BoxFor(dirtyDef, dirtyPiece.Sprite,
                                                             EmptyGlassHeight);
                        GlassArt.Lip(prop, dirtyPiece);   // the front is on every glass (2026-09-09)
                    }
                    img.preserveAspect = true;
                    img.color = Color.white;   // solid (2026-09-21): the counter never shows through a glass
                    if (img.sprite == null) img.color = new Color(0.8f, 0.9f, 0.95f, 0.5f);
                    var view = v;
                    // PICKED UP, NOT PRESSED (GDD 27 §4.2, H4 2026-09-05): pointer-down
                    // collects the glass — Core frees the stool this instant — and the glass
                    // follows the hand until it is let go. Over the sink it is washed; anywhere
                    // else it stays in the hand for the sink's own click. The mark under it
                    // stays for the cloth.
                    // A PRESS IS NOT A COLLECTION (2026-09-06, the author: "sadece tıklamak
                    // bardağı toplamak için yetmemeli"). The press only takes hold of it; the
                    // hand has to MOVE before the glass leaves the counter, which is the same
                    // rule the tin on the coaster keeps and the same one that stops a stray
                    // click from quietly starting the sink's errand.
                    var grab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                    grab.callback.AddListener(_ =>
                    {
                        if (view.Dirty == null || !view.Dirty.HasGlass) return;
                        var runNow = Run;
                        if (runNow == null || (_flow != null && _flow.IsOpen) || CellarOpen) return;
                        var m = Mouse.current;
                        if (m == null) return;
                        _emptyPressed = view;
                        _emptyPressAt = m.position.ReadValue();
                    });
                    prop.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);
                    // ...and it says so before it is pressed (2026-08-26): the author's
                    // rule is about this KIND of interaction, not about the menu alone.
                    // THE EMPTY LIGHTS, AND SO DOES WHERE IT IS GOING (2026-09-06, the
                    // author: "toplanması gereken bardaklar mouse ile gelince hem kendisinin
                    // hem de sinkin parlaması gerekiyor"). Two lit things and a line between
                    // them: what to pick up, and where to carry it.
                    var dirtyGlow = prop.gameObject.AddComponent<HoverGlow>();
                    dirtyGlow.Graphics = new Graphic[] { img };
                    dirtyGlow.Rise = 4f; dirtyGlow.Sway = 1.4f; dirtyGlow.Grow = 1.05f;
                    var dirtyRelay = prop.gameObject.AddComponent<HoverRelay>();
                    var dirtyRt = prop;
                    // WHICH EMPTY, NOT HOW MANY (2026-09-09, the author: "ilk bardağı
                    // yıkadıktan sonra sink hover modda takılı kalıyor, mouse onun üstüne
                    // gelmese bile; hatta sonraki güne geçildiğinde de aynı kalıyor"). It was
                    // a COUNTER, and a glass picked up under the pointer is destroyed before
                    // its Exit can fire — so the count never came back down and the basin
                    // beckoned for the rest of the night, and the next one. A reference heals
                    // itself: the prop it names is checked for life every frame.
                    dirtyRelay.Entered = () => { ShowPropTip(dirtyRt, UIText.T("seats.tip.carry_to_sink")); _emptyOver = dirtyRt; };
                    dirtyRelay.Exited = () => { HidePropTip(dirtyRt); if (_emptyOver == dirtyRt) _emptyOver = null; };
                    var sink = prop.gameObject.AddComponent<PressSink>();
                    sink.Face = prop; sink.Depth = 3f; sink.Lift = 3f; sink.Tint = img;
                    v.DirtyProp = prop;
                }
                else if (!show && v.DirtyProp != null)
                {
                    Destroy(v.DirtyProp.gameObject);
                    v.DirtyProp = null;
                }
                // THE MARKS AROUND IT (GDD 27 §4.1, 2026-09-06): none, one or three of
                // them, each its own mess and each wiped on its own. They outlast the glass
                // — collecting the empty leaves them behind for the cloth.
                for (int m = v.Marks.Count - 1; m >= 0; m--)
                {
                    var mk = v.Marks[m];
                    if (mk.Mess != null && !mk.Mess.IsClean && OnTheCounter(run, mk.Mess))
                    {
                        if (mk.Prop == null) BuildMark(v, mk);
                        continue;
                    }
                    if (mk.Prop != null)
                    {
                        var img0 = mk.Prop.GetComponent<Image>();
                        if (img0 != null && img0.sprite != null) Destroy(img0.sprite);
                        if (mk.Tex != null) Destroy(mk.Tex);
                        Destroy(mk.Prop.gameObject);
                    }
                    v.Marks.RemoveAt(m);
                }
                if (v.Dirty != null && v.Dirty.IsClean && v.DirtyProp == null) v.Dirty = null;
                // ON the counter's drawn surface, not floating at the waist-clip line (the
                // author's report): the clip line is the counter's BACK edge; the top surface
                // the glass stands on reads ~36px lower in the scene.
                //
                // AND IT IS PLACED EVERY FRAME, not once when it is made (2026-08-25, the
                // author: "içilip tezgahta kalan bardaklar tezgahla beraber hareket etmiyorlar
                // ekranda sabit kalıyorlar"). The empties are the last thing standing on the
                // bar that was still written as a one-off position, so when the cellar lifted
                // the counter they stayed exactly where the drinker had left them — on the
                // screen rather than on the wood. The book beside them takes the same lift off
                // the same dial (PlaceBookProp); this is that, for the glasses.
                // NOT UNDER THE MENU BOARD (2026-09-06). The board stands at the counter's
                // left end and the leftmost stool stands under it — measured in play, the
                // board covers 67..129 of the counter and stool 0's leavings want 101..170 —
                // so an empty glass and its mark were half hidden behind a picture frame,
                // which is no way to ask somebody to clear them. Everything a drinker leaves
                // is pushed clear of the board and no further; every other stool is untouched.
                if (v.DirtyProp != null)
                    v.DirtyProp.anchoredPosition =
                        new Vector2(ClearOfTheBoard(v.SeatX + v.DirtyX, v.DirtyProp.rect.width),
                                    CounterLineY - 36f + CounterLift);
                foreach (var mk in v.Marks)
                {
                    if (mk.Prop == null) continue;
                    mk.Prop.anchoredPosition =
                        new Vector2(ClearOfTheBoard(v.SeatX + mk.Dx, mk.Prop.rect.width),
                                    CounterLineY - 40f + CounterLift + mk.Dy);
                }
            }
        }

        /// <summary>The nearest place to <paramref name="x"/> where something this wide can
        /// stand without disappearing behind the menu board. Bottom-left units, like the
        /// stools' own.</summary>
        private float ClearOfTheBoard(float x, float width)
        {
            float boardHalf = _bookProp != null ? _bookProp.rect.width * 0.5f : 31f;
            float edge = _hudRoot.rect.width * 0.5f + BookPropX + boardHalf;
            return Mathf.Max(x, edge + width * 0.5f + 4f);
        }

        // ── ON THE HEAD, EXACTLY (2026-09-13) ───────────────────────────────────
        // The author: "bardak da müşterilerin kafa hizasında olsun tam olarak". Everything a
        // drinker owns on the counter and over it — the empty, its mess, the ticket, the
        // balloon — hung off the MIDDLE of their canvas, and a figure is stood on that canvas by
        // its feet: a head that leans, or a body drawn a little to one side, is not in the
        // middle. These hang off the head itself, measured off the art (MeasureHeadX).

        /// <summary>The drinker's head across, in the seat's own units (0 is the stool's middle).
        /// A mirrored body mirrors it.</summary>
        private static float HeadX(SeatView v)
        {
            if (v == null || v.Look == null) return 0f;
            return v.Body != null && v.Body.flipX ? -v.Look.HeadX : v.Look.HeadX;
        }

        /// <summary>Where the balloon rests over this drinker: on their own head, where the ticket
        /// rests — not at one height for every figure in the cast.</summary>
        private float SayHomeY(SeatView v) => (v.Look != null ? v.Look.HeadTop : CharWinH) + TagLift;

        /// <summary>How small a carried empty gets by the time it is over the basin, and where the
        /// basin is (the sink's foot at stage 68.5 plus half its 35-pixel art).</summary>
        private const float CarryShrink = 0.72f, CarrySinkStageY = 86f;
        private float _glassCarryFromY;

        // ── the counter's prep RAIL (2026-08-26) ──────────────────────────────
        //
        // The author: "tezgah sahnesinde buz limon tuz şeker gibi nesneler için tezgah
        // boyuna oranlı görseller üretilecek, eğer oyuncu servis et dedikten sonra buz
        // limon şeker koymayı unutursa diye." A drink that leaves the bench unfinished
        // used to mean a walk back through the whole flow; the four stations stand on the
        // ROOM's counter now, at the counter's own scale (32px art at a whole 2×,
        // Tools/bench_props_gen.py minis), beside where the made drink rests. They only
        // offered themselves while a served drink was standing there, and they were the
        // FORGIVING door: a plain press, no lap and no aim.
        //
        // IT IS THE WHOLE VERB NOW (2026-08-26, the author: "buz, zeytin, tuz, şeker,
        // nane gibi bardağa koyulan şeyler ana sahnede tezgahta dursun ... bardağa ana
        // sahnedeki nesnelerden sürükleyerek koyulabilecek"). What goes IN a glass
        // stopped being part of building the mix: you pour the drink out of the tin,
        // come back to the room, and finish it on the bar with your hands. So the props
        // STAND on the counter all night, whether or not there is a glass in front of
        // them, and they are DRAGGED — the same verb, and the same weight, as carrying
        // the drink to a stool. Six, not four: the olive and the mint used to live on
        // the glass bench's garnish rail and came here with the rest of them.
        private RectTransform _prepRail;
        private readonly List<PrepProp> _prepProps = new List<PrepProp>();

        /// <summary>What a garnish is FOR, in the words the bar would use — the fallback when
        /// a preparation carries no description of its own (2026-09-07).</summary>
        private static string GarnishPurpose(string id)
        {
            switch (id)
            {
                case "ice": return UIText.T("seats.garnish.purpose.ice");
                case "lemon_twist": return UIText.T("seats.garnish.purpose.lemon_twist");
                case "salt_rim": return UIText.T("seats.garnish.purpose.salt_rim");
                case "sugar_rim": return UIText.T("seats.garnish.purpose.sugar_rim");
                case "olive": return UIText.T("seats.garnish.purpose.olive");
                case "mint": return UIText.T("seats.garnish.purpose.mint");
                default: return UIText.T("seats.garnish.purpose.other");
            }
        }

        /// <summary>The dish this garnish is taken from, as it stands on the counter — the
        /// picture a hover shows when a word is not enough (2026-09-09). One table, because
        /// the rail's own table is built inside a method and this is asked from the licence.</summary>
        private static Sprite GarnishCounterArt(string id)
        {
            switch (id)
            {
                case "ice": return ItemArt.Load("counter_ice");
                case "lemon_twist": return ItemArt.Load("counter_lemon");
                case "salt_rim": return ItemArt.Load("counter_salt");
                case "sugar_rim": return ItemArt.Load("counter_sugar");
                case "olive": return ItemArt.Load("counter_olive");
                case "mint": return ItemArt.Load("counter_mint");
                default: return null;
            }
        }

        private sealed class PrepProp
        {
            public string Id;
            public RectTransform Rt;
            public Image Img;
            /// <summary>What leaves the dish when it is picked up: a CUBE off the bucket,
            /// a WEDGE off the bowl, a spear off the jar (2026-08-26, the author: "buz
            /// kovasindan buz alirsin buz kovasi degil"). Null on the two rims, which are
            /// the one case where the dish itself is carried — you turn the glass IN it.
            /// The sprite is the same one that ends up floating in the drink, so what you
            /// pick up and what you see in the glass are one object.</summary>
            public string Carry;
            public Vector2 CarrySize;
            /// <summary>The volumeless mark this prop puts on the glass, or null when the
            /// prop is stock rather than a mark.</summary>
            public PreparationDefinition Prep;
            /// <summary>The ingredient STYLE this prop pours, or null when it is a mark.
            /// Resolved to a shelf bottle at drop time, because the bar's stock changes.</summary>
            public string Style;
            public bool IsRim;           // turned in the dish, not dropped in the glass
            public string Word;          // what the pointer is told it does
            /// <summary>The rect's y, worked out from THIS drawing, that puts its lowest
            /// drawn pixel on the counter's foot line. See <see cref="DishRestY"/>.</summary>
            public float Rest;
        }

        // ── the rim, turned on the counter (2026-08-26) ──────────────────────────
        //
        // The lap arithmetic is the glass bench's own, moved with the dishes: hold the dish
        // over the drink and run a full circle round its MOUTH with the cursor. It came here
        // because the dishes did, and it came WHOLE - taking the dishes off that bench and
        // applying salt on a single drop would have deleted a skill the author asked for
        // eight days earlier ("tuz artik bardagin etrafinda cevirerek ... ufak bir skill
        // oyunu") without anybody asking for it back.
        //
        // The numbers are the bench's, to the unit, so a player who learned the lap there
        // does not have to learn it again: a band round the mouth where the sweep counts, a
        // single-frame jump bigger than a third of a lap thrown away as the cursor crossing
        // the glass rather than a hand moving, and a part-run lap KEPT against its dish.
        private const int RimSegments = 14;
        private const float RimLap = 2f * Mathf.PI;
        private const float RimNear = 28f, RimFar = 150f;
        private readonly Dictionary<string, float> _rimSwept = new Dictionary<string, float>();
        private RectTransform _rimRing;
        private readonly List<Image> _rimTicks = new List<Image>();
        private float _rimAngle;
        private bool _rimAngleKnown;

        /// <summary>Standing on the counter, right of where the made drink rests. The rail
        /// rides CounterLift like everything else on the bar, so an open cellar takes it up
        /// with the room rather than leaving six dishes hanging in the air.</summary>
        // BETWEEN THE SINK AND THE DRIP MAT (2026-08-26, the author: "garnishler ekranda
        // sola kaymalı, bira matı ile sink arasında olmalı"). At X0 100 the rail ran to
        // stage 555 and its last two dishes stood ON the beer font; at -250 the six span
        // stage 195..380 — clear of the basin's right edge (181) and well short of the drip
        // mat (480). The finished drink moved right with it (see GlassHome), so the counter
        // reads left to right the way the night runs: sink, the makings, the drink, the tap.
        // -250 -> -310: thirty of the room's own pixels left (2026-09-06, the author), which
        // is sixty of the HUD's. The mat under them is measured off this, so it follows.
        private const float PrepRailX0 = -310f, PrepRailGap = 74f;

        /// <summary>How big a square each dish is drawn inside. The drawings are all
        /// different shapes, so preserveAspect fits each one in here and leaves the rest of
        /// the square as air — which is why a dish's rect says nothing about where its foot
        /// is, and why <see cref="DishRestY"/> exists.</summary>
        private const float PrepDishBox = 64f;

        /// <summary>
        /// WHERE A DISH'S RECT HAS TO SIT for the drawing inside it to STAND on the
        /// counter's foot line (2026-09-04, the author: "garnishlerin en alt pixeli ayni
        /// yukseklikte olmali").
        ///
        /// Two things push a drawing up off the bottom of its square and both are measured
        /// here rather than guessed: preserveAspect letterboxes the sprite inside the box,
        /// and the drawing itself may not reach the bottom of its own canvas (the ice
        /// bucket carries two transparent rows under its base, the lemon bowl five). The
        /// six dishes came from three different rounds of art at three different aspects,
        /// so no single offset could have levelled them.
        /// </summary>
        private static float DishRestY(Sprite art, float box)
        {
            if (art == null || art.rect.height <= 0.0001f) return CounterFootY + box * 0.5f;
            float scale = Mathf.Min(box / art.rect.width, box / art.rect.height);
            float drawn = art.rect.height * scale;                 // what preserveAspect gives it
            float pad = ItemArt.FootPadding(art) * scale;          // empty canvas under the art
            return CounterFootY + box * 0.5f - (box - drawn) * 0.5f - pad;
        }

        // The piece in the hand: a copy of the prop's own drawing, following the cursor.
        private RectTransform _prepCarry;
        private Image _prepCarryImg;
        private PrepProp _prepHeld;

        // ── the grains a carried pinch sheds (2026-08-26) ────────────────────────
        //
        // The author: "surukleyen kucuk taneler dokuluyor gibi gozukebilir." A clump of
        // salt in a hand LEAKS, and the leak is what makes it read as loose crystals
        // rather than as a small white object. Two units square, falling under their own
        // gravity, fading as they go — and shed by DISTANCE TRAVELLED rather than by time,
        // so a pinch held still does not bleed onto the counter and one swept across the
        // bar leaves a trail behind the hand.
        private readonly List<(RectTransform Rt, Image Img, Vector2 Vel, float Born)> _grains
            = new List<(RectTransform, Image, Vector2, float)>();
        private Vector2 _grainLastAt;

        /// <summary>Set by <see cref="StepRimLap"/> on a frame the lap actually turned;
        /// read and cleared once a frame by the rail's step. One loop source, one
        /// decider — the tin bench's rule, applied to the counter.</summary>
        private bool _rimLoopWanted;

        // NOBODY IN THIS BAR HAS A VOICE ANY MORE (2026-09-04, the author: "konuşma sesi
        // olmayacak"). SpeakSeat lived here: four murmur clips, pitched by the stool so the
        // six seats sounded like six people, fired on the greeting, the order and the way
        // out. What they say is WRITTEN now — the bubble over the head carries the order, the
        // thinking beat and the note on the drink — and a murmur under a written line is the
        // same information twice, in the one channel that cannot be read at a glance or
        // turned off separately. The stool, the till and the room keep every sound they had;
        // only the mouths are quiet.
        private float _grainCarried;

        // ── the ice pour and the shake in the hand (2026-09-18) ──────────────────
        //
        // The author: "bardağın içine buzu aynı sıvı döker gibi bardağın içine atmamız
        // gereksin. Ve sallanmalı hem taşınırken hem de sıvı içerisinde." Held over the
        // mouth, the hand tips a cube in at this rate; in the drink they are GlassDecor's,
        // riding its bob. In the hand they are neither still nor falling, so the cube
        // swings on the wrist — a slow sine, wider the faster the hand is travelling,
        // because a cube carried at speed swings and one held still barely does.
        private float _icePoured = -1f;           // when the last cube went in, or -1 for none
        private int _icePourCount;                // cubes this pour has dropped
        private const float IceEvery = 0.40f;     // seconds between cubes while it is held
        private const float CarrySwing = 11f;     // degrees at a standstill
        private const float CarrySwingRate = 5.2f;
        private const float CarrySwingDrift = 0.22f;   // extra degrees per unit of hand speed
        private const float CarrySwingMax = 26f;
        private float _carrySwing;
        private float _clothSprayed;              // travel banked toward the next wipe drop
        private const float ClothSprayEvery = 16f;
        private const float GrainEvery = 26f;     // units of travel between crystals
        private const float GrainLife = 0.55f;
        private const float GrainFall = 520f;

        private void BuildMiniPreps(RectTransform root)
        {
            _prepRail = NewRect("CounterPreps", root);
            _prepRail.anchorMin = _prepRail.anchorMax = _prepRail.pivot = new Vector2(0.5f, 0.5f);
            _prepRail.sizeDelta = Vector2.zero;
            // Art first, fallback second: the 2026-08-26 counter set is drawn at the bar's
            // own eye line (the author: "masanin acisiyla ayni aciya sahip"), and the
            // 2026-08-25 minis stay behind it so a missing drawing never costs a verb.
            // SIX, AND TWO OF THEM ARE NOT PREPARATIONS (2026-08-26, the author listed
            // "buz, zeytin, tuz, seker, nane"). Ice and the two rims and the twist are
            // PreparationDefinitions - volumeless marks on the glass. The olive and the
            // mint are INGREDIENTS: they are stock, they come off the shelf, they run out,
            // and they are what recipes.json's "olive" and "mint" style bands are graded
            // against. So the rail carries two kinds of prop and each drops through its own
            // Core verb - AddPreparationAtGlass for a mark, PourAtGlass for a pinch of
            // stock. A garnish whose bottle the bar does not stock, or has emptied, is
            // simply not built: an empty jar on the counter is a promise the bar cannot keep.
            (string id, string art, PreparationDefinition prep,
             string style, string word, string carry, float carryH)[] rail =
            {
                ("ice", "counter_ice", Preparations.Ice, null, UIText.T("seats.rail.ice"),
                 "ice-1", 34f),
                // The hand carries one of the author's three cubes (2026-09-18); which one
                // FALLS is the run's own roll for that cube, dressed in DropCube.
                ("lemon_twist", "counter_lemon", Preparations.LemonTwist,
                 null, UIText.T("seats.rail.lemon"), "glass_lemon", 40f),
                // A PINCH, not the dish (2026-08-26, the author: "surukledigimiz tuz ve
                // seker daha cok tuz ve seker yumagi gibi olmali"). Carrying the whole
                // cellar was the same mistake the bucket made, and the answer is the same:
                // what leaves a dish of salt is salt. The lap still turns the GLASS in it —
                // the pinch in the hand is what you are turning it through.
                ("salt_rim", "counter_salt", Preparations.SaltRim,
                 null, UIText.T("seats.rail.salt"), "carry_salt", 32f),
                ("sugar_rim", "counter_sugar", Preparations.SugarRim,
                 null, UIText.T("seats.rail.sugar"), "carry_sugar", 30f),
                // WHAT IS BOUGHT GOES ON THE END (2026-09-09, the author: "yeni eklenen
                // garnishler sağa doğru eklenmeli"). The olive and the mint are the only two
                // the bar does not start with, and they sat in the middle of the table — so
                // buying a jar of olives opened a gap between the lemon and the salt and
                // shoved half the rail sideways. The four the house always has keep their
                // places; a jar arrives at the right-hand end, where a new thing belongs.
                // THE JARS ARE EXTRAS (2026-09-21): a spear and a sprig dropped on the glass like ice, never a
                // pinch poured into it — the dish stands on the rail once the rung and the market's jar allow.
                ("olive", "counter_olive", Preparations.Olive, null, UIText.T("seats.rail.olive"),
                 "glass_olive", 52f),
                ("mint", "counter_mint", Preparations.Mint, null, UIText.T("seats.rail.mint"),
                 "glass_mint", 40f),
            };
            // THE MAT IS THE ROOM'S (2026-09-06, the author: "çerez paspası tezgah, oda,
            // musluk, bira paspası, bira musluğunun olduğu sahneye koyulsun ve 2 pixel
            // yukarı alınsın"). It was a canvas picture under the dishes here, tinted by hand
            // to look lit; it is a FIXTURE now (`prep_mat`, given with the room like the drip
            // mat), stood on the counter by DiegeticStage at its own size and lit by the
            // room's lights like everything else on the wood. The dishes still stand on it —
            // the HUD is over the stage — at the same rail they always had.

            for (int i = 0; i < rail.Length; i++)
            {
                var (id, art, prep, style, word, carry, carryH) = rail[i];
                var rt = NewRect("MP_" + id, _prepRail);
                var dish = ItemArt.Load(art);
                float rest = DishRestY(dish, PrepDishBox);
                Place(rt, new Vector2(0.5f, 0.5f), new Vector2(PrepDishBox, PrepDishBox),
                    new Vector2(PrepRailX0 + i * PrepRailGap, rest));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = dish;
                img.preserveAspect = true;
                if (img.sprite == null) img.color = UITheme.Cyan[3];
                var glow = rt.gameObject.AddComponent<HoverGlow>();
                glow.Graphics = new Graphic[] { img };
                glow.Rise = 4f; glow.Sway = 1.4f; glow.Grow = 1.07f;
                // THE DISH RISES OVER ITS CARD (2026-09-08, the author: "üstüne gelinen asset
                // hiyerarşide en üste, bilgi kartının üstüne çıksın, odağa"). A canvas of
                // its own, dormant (inheriting) until the card stands, when ShowGarnishCard
                // sorts it at 27 — over the card's 26 — and the dish itself, glow and all,
                // is the one thing in front. Its raycaster keeps the pointer on it.
                var lift = rt.gameObject.AddComponent<Canvas>();
                lift.overrideSorting = false;
                rt.gameObject.AddComponent<ForgivingRaycaster>();
                var carryArt = carry != null ? ItemArt.Load(carry) : null;
                var prop = new PrepProp
                {
                    Id = id, Rt = rt, Img = img, Prep = prep, Style = style, Word = word,
                    Rest = rest,
                    Carry = carryArt != null ? carry : null,
                    CarrySize = carryArt != null
                        ? new Vector2(carryH * (carryArt.rect.width / carryArt.rect.height),
                                      carryH)
                        : new Vector2(64f, 64f),
                    // A rim is TURNED, not dropped (2026-08-25's skill, re-homed here on
                    // 2026-08-26 when the dishes left the glass bench). See StepPrepCarry.
                    IsRim = id == "salt_rim" || id == "sugar_rim",
                };

                // PICKED UP, NOT PRESSED. A whole-rect PointerDown rather than a Button:
                // what follows is a carry, and a Button fires on the way back UP, after the
                // drop has already been decided.
                var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                down.callback.AddListener(_ => GrabPrep(prop));
                rt.gameObject.AddComponent<EventTrigger>().triggers.Add(down);
                var relay = rt.gameObject.AddComponent<HoverRelay>();
                var theRt = rt;
                // A CARD, NOT A CAPTION (2026-09-07, the author: "garnish kaselerinin ustune
                // mouse getirildiginde onlarin iconu ve ne oldugu ne icin oldugu aciklayan bir
                // kart cisin"). The pictogram is the one the licence prints and the ticket
                // over a head draws, so a garnish is one picture wherever it is read; the
                // line under it is the preparation's own description, which Core has been
                // carrying unused.
                var theIcon = PrefArt.ForPreparation(id);
                var theWord = word;
                var theWhy = prep != null && !string.IsNullOrEmpty(prep.Description)
                    ? UIText.Caps(UIText.T(prep.DescriptionLine))
                    : GarnishPurpose(id);
                // The dish's caption is the bottle's card now (2026-09-08).
                relay.Entered = () => ShowGarnishCard(theRt, prop, theWord, theIcon, theWhy);
                relay.Exited = () => HideGarnishCard(theRt);
                // ...AND THE DISH ITSELF STANDS IN THE ROOM (2026-09-22, the author: "Garnishler ve menü ve bez
                // stage world'ün içinde olmadığından sahnede kullandığımız ışıklandırmalardan etkilenmiyor"): the
                // rect keeps the grab, the hover and the card; the picture is a stage sprite the lamps can reach.
                IntoTheRoom("Prep_" + id, rt, img);
                _prepProps.Add(prop);
            }

            BuildCloth();

            // The piece in the hand. Built once, hidden, and dressed at every pick-up: a
            // sprite spawned per drag would be a new GameObject on every touch of the bar.
            _prepCarry = NewRect("PrepInHand", root);
            _prepCarry.anchorMin = _prepCarry.anchorMax = _prepCarry.pivot = new Vector2(0.5f, 0.5f);
            _prepCarry.sizeDelta = new Vector2(64, 64);
            _prepCarryImg = _prepCarry.gameObject.AddComponent<Image>();
            _prepCarryImg.preserveAspect = true;
            _prepCarryImg.raycastTarget = false;
            _prepCarry.gameObject.SetActive(false);

            _prepRail.gameObject.SetActive(false);
        }

        /// <summary>Takes a piece off the rail. Refused between days and behind a panel —
        /// the bar is not yours to reach across while the books are open.</summary>
        private void GrabPrep(PrepProp prop)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;
            if (CellarOpen || _prepCarry == null) return;
            _prepHeld = prop;
            // WHAT COMES OUT OF THE DISH, not the dish (2026-08-26). The hand used to
            // lift the whole bucket; you take a cube out of a bucket, and the cube you
            // take is the cube that ends up in the drink — the same sprite, so the pick,
            // the carry and the float are one object all the way through.
            var inHand = prop.Carry != null ? ItemArt.Load(prop.Carry) : null;
            _prepCarryImg.sprite = inHand ?? prop.Img.sprite;
            _prepCarryImg.color = _prepCarryImg.sprite != null ? Color.white : UITheme.Cyan[3];
            _prepCarry.sizeDelta = inHand != null ? prop.CarrySize : new Vector2(64f, 64f);
            _prepCarry.anchoredPosition = prop.Rt.anchoredPosition + _prepRail.anchoredPosition;
            // The finger lands on the CUBE where it landed on the dish: the grip is kept in
            // proportion, since what comes out is smaller than what it came out of.
            _prepGrabOffset = Vector2.zero;
            var m = Mouse.current;
            if (m != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudRoot, m.position.ReadValue(), null, out Vector2 held))
            {
                var dish = prop.Rt.rect.size;
                var scale = new Vector2(dish.x > 1f ? _prepCarry.sizeDelta.x / dish.x : 1f,
                                        dish.y > 1f ? _prepCarry.sizeDelta.y / dish.y : 1f);
                _prepGrabOffset = Vector2.Scale(_prepCarry.anchoredPosition - held, scale);
                _prepCarry.anchoredPosition = held + _prepGrabOffset;
            }
            _prepCarry.gameObject.SetActive(true);
            _prepCarry.SetAsLastSibling();
            _grainCarried = 0f;
            _grainLastAt = _prepCarry.anchoredPosition;
            _icePoured = -1f;
            _icePourCount = 0;
            _carrySwing = 0f;
            _prepCarry.localRotation = Quaternion.identity;
            if (prop.IsRim) Sfx.Play("grain_pinch", 0.5f);
            HidePropTip(prop.Rt);
            Sfx.Play("click", 0.4f);
        }

        /// <summary>The carry itself: the piece follows the cursor, and letting go over the
        /// glass puts it in the drink. Anywhere else it simply goes back — a garnish dropped
        /// on the floor is not a mechanic anybody asked for.</summary>
        private void StepPrepCarry(TycoonRun run)
        {
            if (_prepHeld == null) return;
            var mouse = Mouse.current;
            if (mouse == null) { DropPrep(false); return; }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_prepCarry.parent, mouse.position.ReadValue(), null,
                    out Vector2 at))
            {
                at += _prepGrabOffset;
                if (_prepHeld.IsRim)
                {
                    _grainCarried += (at - _grainLastAt).magnitude;
                    while (_grainCarried >= GrainEvery) { _grainCarried -= GrainEvery; ShedGrain(at); }
                }
                // IT SWINGS IN THE HAND (2026-09-18). The travel is already measured for the
                // grains; a cube leans by how fast it is moving and rocks on top of that, so a
                // piece crossing the bar reads as carried rather than as dragged.
                if (!_prepHeld.IsRim)
                {
                    float speed = (at - _grainLastAt).magnitude / Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
                    float want = Mathf.Min(CarrySwing + speed * CarrySwingDrift, CarrySwingMax);
                    _carrySwing = Mathf.Lerp(_carrySwing, want, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
                    _prepCarry.localRotation = Quaternion.Euler(0, 0,
                        Mathf.Sin(Time.unscaledTime * CarrySwingRate) * _carrySwing);
                }
                _grainLastAt = at;
                _prepCarry.anchoredPosition = at;
            }
            // A RIM DISH WORKS WHILE IT IS HELD. Everything else waits for the release.
            bool glassOut = _glassShown && !_glassServing && !_glassReturning
                            && _drinkGlass != null && run != null && run.DrinkReady;
            if (_prepHeld.IsRim && glassOut)
            {
                bool done = _prepHeld.Prep != null
                            && !run.ServingGlass.HasPreparation(_prepHeld.Id)
                            && StepRimLap(run, _prepHeld, mouse.position.ReadValue());
                if (done) { DropPrep(false); return; }
            }
            else ShowRimRing(false);

            bool overTheGlass = glassOut
                && RectTransformUtility.RectangleContainsScreenPoint(
                       _drinkGlass, mouse.position.ReadValue(), null);

            // ICE IS POURED, NOT PLACED (2026-09-18, the author: "bardağın içine buzu aynı
            // sıvı döker gibi bardağın içine atmamız gereksin"). A cube used to be one drag
            // for one cube: four cubes meant four round trips to the bucket, and the bar's
            // other way of putting something in a glass — hold it over and let it run — was
            // nowhere in it. Held over the glass, the hand now TIPS: the first cube goes in
            // the moment it is over the mouth and another follows every IceEvery for as long
            // as the button is down, each one falling into the drink on its own.
            if (_prepHeld.Id == "ice" && _prepHeld.Prep != null)
            {
                // No ice in a pint (2026-09-22): Core refuses it, so the tipping hand simply never starts.
                if (!overTheGlass || !run.PreparationSuitsGlass(_prepHeld.Prep)) _icePoured = -1f;
                else
                {
                    float now = Time.unscaledTime;
                    if (_icePoured < 0f || now - _icePoured >= IceEvery)
                    {
                        _icePoured = now;
                        run.AddPreparationAtGlass(_prepHeld.Prep);
                        DropCube(_prepCarry.anchoredPosition);
                        Sfx.Play("ice_drop", 0.7f);
                        _icePourCount++;
                    }
                }
            }

            if (mouse.leftButton.isPressed) return;

            // The ice already went in while it was held, so the release only puts the tongs
            // down — and it says how many cubes the pour turned out to be.
            if (_icePourCount > 0)
            {
                Toast(UIText.T("seats.garnish.ice_in", ("cubes", run.ServingGlass.IceCubes)),
                      UITheme.Cyan[3]);
                DropPrep(false);
                return;
            }
            if (!overTheGlass && _prepHeld != null) Sfx.Play("dish_down", 0.55f);
            DropPrep(overTheGlass);
        }

        /// <summary>One crystal off the pinch, thrown a little sideways and then falling.
        /// Its sideways kick comes from the grain COUNT, not from a roll: the same hand
        /// движение sheds the same trail, and the determinism rule never comes up.</summary>
        private void ShedGrain(Vector2 at)
        {
            if (_prepHeld == null || _prepCarry == null) return;
            var rt = NewRect("Grain", (RectTransform)_prepCarry.parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(3f, 3f);
            rt.anchoredPosition = at + new Vector2(0f, -12f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = _prepHeld.Id == "salt_rim"
                ? new Color(0.95f, 0.96f, 0.97f) : new Color(0.94f, 0.89f, 0.77f);
            img.raycastTarget = false;
            float kick = ((_grains.Count * 37) % 41) / 20f - 1f;   // -1..1, walked, not rolled
            _grains.Add((rt, img, new Vector2(kick * 34f, -20f), Time.unscaledTime));
        }

        /// <summary>The shed crystals, falling. Cheap: a handful of 3-unit rects with a
        /// half-second life, and the list is empty the moment the hand is empty.</summary>
        // ── the counter's own verbs on the screen (GDD 27 §4, H4, 2026-09-05) ───
        //
        // Three things the player does with what a drinker leaves: TAKE the glass (pointer-
        // down on it; it follows the hand and is washed if let go over the sink, kept in the
        // hand otherwise), WIPE the mark (the cloth, picked up off the counter's left end and
        // passed over it — never under a glass, which Core refuses in words), and WASH what
        // is in the hand (the sink's click; the tap runs for Core's WashSecondsFor). Every
        // one of them is a Core verb the bot already calls; this only draws them.

        // THE CLOTH MOVED TO THE OTHER END (2026-09-06, the author: "peçeteyi tezgahın sağına
        // çek"). It sat at the counter's left end, which is where the menu now stands; the
        // right end is clear past the beer font, and a cloth kept at the far end of the bar
        // from the glassware is where a bar actually keeps one.
        private const float ClothX = 1200f;   // the counter's right end, past the font (the fallback)
        // THE TOWEL RAIL (2026-09-07, the author: "bar tezgahına bir bar demiri çizdim,
        // towel_on_bar tam ona oturacak şekilde düzenlendi, onu onun üstüne oturt"). The rail
        // is drawn into the counter's right cap at art columns 566..632, rows 59..62; the
        // folded towel hangs from two rows above its top so the fold wraps the bar.
        private const float RailArtX = 599f, RailArtTopY = 59f;
        /// <summary>The author's two towels, drawn at the counter's own two units a pixel:
        /// folded over the rail (45x51) and in the hand (30x54). The rect is the drawing at
        /// 2x and nothing more — see the note in BuildCloth about the alpha hit test, which
        /// samples across the rect it is handed rather than across the drawn picture.</summary>
        private static readonly Vector2 TowelRestSize = new Vector2(90f, 102f), TowelHeldSize = new Vector2(60f, 108f);

        /// <summary>Sizes the cloth's rect to the sprite it is wearing, at two units an art
        /// pixel, so the picture fills the rect exactly. Anything else and the hit test
        /// answers off the drawing (2026-09-07).</summary>
        private void FitCloth(Sprite art, Vector2 fallback)
        {
            if (_clothRt == null) return;
            _clothRt.sizeDelta = art != null
                ? new Vector2(art.rect.width * 2f, art.rect.height * 2f)
                : fallback;
        }
        private RectTransform _clothRt;
        private Image _clothImg;
        private bool _clothHeld;
        private float _clothLastX;        // where the hand was, for which way the tail points
        // THE SWING (2026-09-07): a cloth nailed at its top lags behind the hand. These are
        // the angle it hangs at and the speed that angle is changing, in degrees.
        private float _clothSwing, _clothSwingVel;
        /// <summary>How far the hem trails the hand, in degrees per unit of pointer speed,
        /// and the spring that brings it back to hanging straight.</summary>
        private const float ClothSwingPerSpeed = 0.055f, ClothSwingMax = 34f,
                            ClothSwingStiffness = 90f, ClothSwingDamping = 11f;
        private float _rubT;              // the wipe sound's own gap, so a rub is not a rattle
        private SeatView _clothRefused;
        private RectTransform _glassCarry;
        private Image _glassCarryImg;
        private bool _glassCarrying;
        // THE TIN GOES TO THE BASIN TOO (2026-09-06, the author: "shaker da lavaboya
        // dokulup cope atilabilir"). Carried, not clicked: the sink is the one verb on
        // this counter that costs money, and the room already asks for the walk before it
        // will take a finished drink. A press that never travels is still the door back
        // to the bench, so the tin keeps both meanings and the hand decides which.
        private Vector2 _glassPressAt;    // where the press on the finished drink landed
        // THE HAND KEEPS ITS GRIP (2026-09-06, the author: "nesneler tutulurken veya
        // sürüklenirken hep mouseun ortasına hizalanıyor bunun olmamasını istiyorum").
        // Every carried thing used to snap its pivot to the cursor the moment it was taken,
        // so a glass grabbed by the rim jumped to sit centred on the finger. Each grab now
        // records where on the thing the finger landed, and the carry keeps that offset —
        // the rim you took it by is the rim it hangs from.
        private Vector2 _glassGrabOffset, _emptyGrabOffset, _tinGrabOffset, _clothGrabOffset, _prepGrabOffset;
        private bool _glassTravelled;     // ...and whether the hand has moved since
        private SeatView _emptyPressed;   // an empty under a finger that has not travelled yet
        private SeatView _carriedEmpty;   // ...and the one in the air, not yet Core's business
        private Vector2 _emptyPressAt;
        private RectTransform _emptyOver;   // the empty the pointer is on, or null (2026-09-09)
        private RectTransform _tinCarry;
        private Image _tinCarryImg;
        private bool _tinCarrying, _tinPressed;
        private Vector2 _tinPressAt;
        /// <summary>How far the pointer has to travel before a press on the tin stops
        /// being the door back to the bench and becomes a carry, in screen pixels.</summary>
        private const float TinDragSlop = 10f;
        private Text _handStrip;

        private void BuildCloth()
        {
            if (_clothRt != null) return;
            // ITS OWN LAYER, ABOVE THE ROLLER (2026-09-07, the author: "temizlik bezi hitboxu
            // kepenk hitboxunun onunde olmali"). The cloth used to be an ordinary child of the
            // HUD canvas, which sorts at 5; the shutter's hit plate is a canvas of its own at
            // 6 and runs the width of the room, so it swallowed every click meant for the
            // towel along the whole rail. This is the same trick the licence and the market
            // use to sit over the bar: a canvas that says where it belongs rather than
            // relying on the order it happened to be built in.
            var clothLayer = NewRect("ClothLayer", _hudRoot);
            Stretch(clothLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var clothCanvas = clothLayer.gameObject.AddComponent<Canvas>();
            clothCanvas.overrideSorting = true;
            clothCanvas.sortingOrder = 8;
            clothLayer.gameObject.AddComponent<ForgivingRaycaster>();
            UiAuditExempt.Mark(clothLayer, "the cloth hangs over the room's own doors: the "
                + "shutter's hit plate is a canvas at 6 and would otherwise take its clicks");

            _clothRt = NewRect("Cloth", clothLayer);
            _clothRt.anchorMin = _clothRt.anchorMax = new Vector2(0.5f, 0.5f);
            // Hung from the middle of its top edge - on the rail by the fold, in the hand
            // by the nail. One pivot serves both, and it is what the swing turns about.
            _clothRt.pivot = new Vector2(0.5f, 1f);
            _clothRt.sizeDelta = TowelRestSize;
            _clothImg = _clothRt.gameObject.AddComponent<Image>();
            _clothImg.sprite = ItemArt.Load("towel_on_bar") ?? ChromeArt.Cloth();
            _clothImg.preserveAspect = true;
            FitCloth(_clothImg.sprite, TowelRestSize);
            // IN FRONT OF THE COUNTER, IN THE ROOM (2026-09-22): order 36, over the bar (30) and its shutter (33),
            // so the towel hangs on the rail and is lit by whatever is lighting the rail.
            IntoTheRoom("Cloth", _clothRt, _clothImg, 36);
            // HIT BY ITS OWN PICTURE (2026-09-07, the author: "cloth çok büyük bir bölgeye
            // sahip, hitbox'ını görselin boyutuyla orantıla").
            //
            // THE RECT IS THE DRAWING, and that is the half that was missing. An alpha hit
            // test samples the sprite across the rect it is GIVEN — it knows nothing about
            // preserveAspect's letterboxing — so a rect wider than the drawing maps the
            // towel's alpha onto a box the towel does not fill, and the pointer answers in
            // the air beside it. TowelSize is the art at exactly 2x (45x51 and 30x54 art
            // px), so the rect and the picture are the same rectangle and the threshold
            // then trims the transparent corners honestly.
            _clothImg.alphaHitTestMinimumThreshold = 0.5f;
            var glow = _clothRt.gameObject.AddComponent<HoverGlow>();
            glow.Graphics = new Graphic[] { _clothImg };
            glow.Rise = 4f; glow.Sway = 1.4f; glow.Grow = 1.07f;
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => GrabCloth());
            _clothRt.gameObject.AddComponent<EventTrigger>().triggers.Add(down);
            var relay = _clothRt.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () => ShowPropTip(_clothRt, UIText.T("seats.tip.cloth"));
            relay.Exited = () => HidePropTip(_clothRt);

            // What the hand holds, said over the sink: glasses waiting, or the wash running.
            _handStrip = NewText("InHand", _hudRoot, _body, 8, TextAnchor.LowerCenter, UITheme.Cream[3]);
            _handStrip.rectTransform.anchorMin = _handStrip.rectTransform.anchorMax = new Vector2(0, 0);
            _handStrip.rectTransform.pivot = new Vector2(0.5f, 0f);
            _handStrip.rectTransform.sizeDelta = new Vector2(240, 14);
            _handStrip.horizontalOverflow = HorizontalWrapMode.Overflow;
            _handStrip.raycastTarget = false;
            _handStrip.text = "";
        }

        /// <summary>How big the mark is drawn: ONE unit to a pixel, because the mark is
        /// grit rather than a picture (ChromeArt.SmudgePixels). The mark keeps the size it
        /// had on the counter; what changed is how finely it is made.</summary>
        private const float SmudgeScale = 1f;

        /// <summary>The tallest empty on the counter, in HUD units; every other line is drawn
        /// at the same units-per-pixel as this one (GlassArt.TallestSheet).</summary>
        // IN PROPORTION WITH THE REST (2026-09-09, the author: "müşterilerin bıraktığı
        // bardağın boyutunu diğer bardak boyutlarıyla orantıla"). It is the same glass that
        // was carried to them at 116 and it stood at 52 — less than half — so a bar with
        // empties on it looked like a bar with toys on it. It stands a little smaller than
        // the working glass because it is on the far side of the counter, not because it is
        // a different object.
        // A LITTLE SMALLER (2026-09-13, the author: "masada müşterilerin bıraktığı bardakların
        // boyutunu biraz küçültelim"): 96 stood the empties nearly as tall as the working glass.
        private const float EmptyGlassHeight = 80f;

        /// <summary>What is left of a mark, as a share of the ink it started with, below which
        /// the counter calls it clean. Not zero: chasing the last few translucent pixels of a
        /// splash with a cloth is not a game, it is an eye test.</summary>
        private const float SmudgeGone = 0.07f;

        /// <summary>Passes of the cloth a texel of a mark takes before it is gone, how fast what a
        /// pass took off fades out, and how far inside the cloth a texel must be to count as under
        /// it (and outside, to count as left) so a swinging hem cannot rub a pixel on its own.</summary>
        private const int RubsToClean = 3;
        private const float RubFadePerSecond = 4f, RubHysteresis = 2f;

        /// <summary>
        /// A stool's mark — and its OWN copy of one (2026-09-06, the author: "bezle silinirken
        /// kir tek seferde silinmemeli, piksele göre boyama mantığında her yeri silmeli"). The
        /// art is shared and cached, so a mess that rubbed holes in it would rub them in every
        /// other mess on the counter; this takes the pixels, makes a texture nobody else owns,
        /// and hands the cloth that.
        /// </summary>
        private void BuildMark(SeatView v, Mark mk)
        {
            var rt = NewRect("Smudge", _hudRoot);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(ChromeArt.SmudgeW * SmudgeScale, ChromeArt.SmudgeH * SmudgeScale);
            var img = rt.gameObject.AddComponent<Image>();
            mk.Px = ChromeArt.SmudgePixels(mk.Seed, out int sw, out int sh);
            mk.Ink = 0;
            foreach (var c in mk.Px) mk.Ink += c.a;
            int texels = mk.Px.Length;
            mk.Orig = new byte[texels]; mk.Rubs = new byte[texels];
            mk.Covered = new bool[texels]; mk.Level = new float[texels];
            for (int i = 0; i < texels; i++) { mk.Orig[i] = mk.Px[i].a; mk.Level[i] = 1f; }
            mk.Tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            mk.Tex.SetPixels32(mk.Px);
            mk.Tex.Apply();
            img.sprite = Sprite.Create(mk.Tex, new Rect(0, 0, sw, sh), new Vector2(0.5f, 0.5f), 100f);
            img.sprite.hideFlags = HideFlags.DontSave;
            img.preserveAspect = true;
            rt.SetAsFirstSibling();
            var relay = rt.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () => ShowPropTip(rt, UIText.T("seats.tip.mark"));
            relay.Exited = () => HidePropTip(rt);
            mk.Prop = rt;
        }

        /// <summary>Moves <paramref name="t"/> to the end of its parent only if it is not there already.</summary>
        private static void KeepLast(Transform t)
        {
            if (t == null || t.parent == null) return;
            if (t.GetSiblingIndex() != t.parent.childCount - 1) t.SetAsLastSibling();
        }

        /// <summary>Bottom-left HUD units to the centre-anchored space the carried props live in.</summary>
        private Vector2 ToCentre(Vector2 bottomLeft) =>
            bottomLeft - _hudRoot.rect.size * 0.5f;

        private void GrabCloth()
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;
            if (CellarOpen || _clothRt == null) return;
            _clothHeld = true;
            _clothRefused = null;
            _clothRt.SetAsLastSibling();
            // In the hand it is the loose towel (the author's towel.png), and its BODY is
            // the rag the marks are rubbed with — Rub walks the rect's own corners.
            var towelHeld = ItemArt.Load("towel");
            if (towelHeld != null) { _clothImg.sprite = towelHeld; FitCloth(towelHeld, TowelHeldSize); }
            _clothRt.localScale = Vector3.one;
            // NAILED, NOT CARRIED (2026-09-07, the author: "bez gorselinin orta ust kismina
            // sabitlenmeli mouse"). Everything else in the hand keeps the offset it was
            // grabbed by, because a glass taken by the rim should hang from the rim. A cloth
            // is the other thing: it hangs from the pointer at the middle of its top edge
            // however it was picked up, which is why the offset is zero rather than measured.
            _clothGrabOffset = Vector2.zero;
            _clothSwing = _clothSwingVel = 0f;
            _clothLastX = float.NaN;
            _clothFlyT = -1f;          // caught mid-throw: the hand wins

            HidePropTip(_clothRt);
            Sfx.Play("click", 0.4f);
        }

        /// <summary>The cloth's flight home: where it was let go, and how far through the
        /// throw it is. -1 when it is not flying (2026-09-08).</summary>
        private Vector2 _clothFlyFrom;
        private float _clothFlyT = -1f;
        /// <summary>How long the throw takes. Long enough to read as a throw, short enough
        /// that the next mark can be wiped without waiting for it.</summary>
        private const float ClothFlySeconds = 0.28f;

        private void DropCloth()
        {
            if (!_clothHeld) return;
            _clothHeld = false;
            _clothSwing = _clothSwingVel = 0f;
            _clothLastX = float.NaN;
            if (_clothRt != null) _clothRt.localRotation = Quaternion.identity;
            // Back on the rail, folded.
            var rest = ItemArt.Load("towel_on_bar");
            if (rest != null) { _clothImg.sprite = rest; FitCloth(rest, TowelRestSize); }
            _clothRt.localScale = Vector3.one;
            // IT FLIES BACK (2026-09-08, the author: "havlu bırakıldığında direkt doğru yere
            // ışınlanmasın, bırakıldığı noktadan başlangıç noktasına animasyonla yavaşça
            // gitsin"). Letting go used to write `home` into the rect on the next frame, so a
            // cloth dropped at the far end of the bar was simply on the rail with nothing in
            // between — a teleport, because that is what it was. StepCloth carries it now.
            _clothFlyFrom = _clothRt != null ? _clothRt.anchoredPosition : Vector2.zero;
            _clothFlyT = Motion.Reduced ? -1f : 0f;
            Sfx.Play("dish_down", 0.45f);
        }

        private void StepCloth(TycoonRun run)
        {
            if (_rubT > 0f) _rubT -= Time.unscaledDeltaTime;
            if (_clothRt == null) return;
            bool on = run != null && run.Phase == TycoonPhase.DayOpen && (_flow == null || !_flow.IsOpen);
            if (_clothRt.gameObject.activeSelf != on) _clothRt.gameObject.SetActive(on);
            if (!on) { _clothHeld = false; return; }
            // ON THE RAIL: the fold two art rows above the bar's top, at the bar's middle,
            // wherever the drawn counter puts that in this window; the old right-end spot
            // only if the room has no counter to ask.
            Vector2 home = stage != null && stage.CounterArtPoint(RailArtX, RailArtTopY - 2f, out var rail)
                ? ToCentre(new Vector2(rail.x * StageToHud, rail.y * StageToHud + CounterLift))
                : ToCentre(new Vector2(ClothX, CounterLineY - 36f + CounterLift));
            if (!_clothHeld)
            {
                // The throw, if one is in the air: from where the hand let go to the rail,
                // eased out, on the ROOM's clock — a towel sailing across a room the book has
                // stopped would be the pause bug in miniature.
                if (_clothFlyT >= 0f)
                {
                    _clothFlyT += Mathf.Max(0f, AnimDelta) / ClothFlySeconds;
                    if (_clothFlyT >= 1f) _clothFlyT = -1f;
                    else home = Vector2.Lerp(_clothFlyFrom, home, Tweening.OutCubic(_clothFlyT));
                }
                _clothRt.anchoredPosition = home;
                _clothImg.raycastTarget = !CellarOpen;
                // SEE-THROUGH WHILE THE CELLAR IS OPEN (2026-09-07, the author: "mahzen
                // açıldığında towel şeffaf olmalı"): the rail rides up with the room and
                // the towel would hang over the shelves you are reading.
                float phase = stage != null ? stage.DrawerPhase : 0f;
                _clothImg.color = new Color(1f, 1f, 1f, 1f - 0.8f * phase);
                StepMarks(run, false, default, 0f);   // what the last passes took keeps fading
                return;
            }
            _clothImg.color = Color.white;
            float dxTravel = 0f;               // how far the hand moved this frame, for the spray
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed || CellarOpen) { DropCloth(); return; }
            var screen = mouse.position.ReadValue();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudRoot, screen, null, out Vector2 at))
            {
                // The nail is the pointer itself: the rect's pivot is its top middle, so
                // putting the rect there hangs the drawing from the hand exactly.
                _clothRt.anchoredPosition = at;

                // AND IT SWINGS (the author: "civiyle cakilmis gibi mouse saga sola
                // oynatildiginda bezde sallanmali"). The hand's own speed drives a spring;
                // the cloth is drawn rotated about the nail, so the hem trails the hand and
                // settles when the hand stops. Unscaled, like every other feel in this file,
                // and the first frame of a grab has no previous position to measure against.
                float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
                float dx = float.IsNaN(_clothLastX) ? 0f : at.x - _clothLastX;
                _clothLastX = at.x;
                float want = Mathf.Clamp(-dx / dt * ClothSwingPerSpeed, -ClothSwingMax, ClothSwingMax);
                if (Motion.Reduced) { _clothSwing = 0f; _clothSwingVel = 0f; }
                else
                {
                    _clothSwingVel += (want - _clothSwing) * ClothSwingStiffness * dt;
                    _clothSwingVel -= _clothSwingVel * Mathf.Min(1f, ClothSwingDamping * dt);
                    _clothSwing = Mathf.Clamp(_clothSwing + _clothSwingVel * dt,
                                              -ClothSwingMax, ClothSwingMax);
                }
                _clothRt.localRotation = Quaternion.Euler(0f, 0f, _clothSwing);
                dxTravel = dx;
            }
            // WIPING IS RUBBING (GDD 27 §4.2, corrected 2026-09-06). The cloth used to erase
            // a whole mark the instant it touched any part of it, so a mess was a click with
            // extra steps. It takes the ink out of the pixels it actually passes over now, and
            // the mark is only wiped — Core's own verb, with Core's own refusals — once there
            // is next to nothing left of it. Under a glass Core refuses, and the refusal is
            // said once per mark per grab.
            StepMarks(run, true, at, dxTravel);
        }

        /// <summary>
        /// The counter's marks, every frame: rubbed where the held cloth passes, eased toward what
        /// the passes left of them, and handed to Core once they are gone. Runs with the cloth on
        /// the rail too, so the last pass keeps fading after the hand lets go.
        /// </summary>
        private void StepMarks(TycoonRun run, bool held, Vector2 at, float dxTravel)
        {
            foreach (var v in _seats)
                foreach (var mk in v.Marks)
                {
                    if (mk.Prop == null || mk.Mess == null || mk.Tex == null || mk.Px == null || mk.Orig == null) continue;
                    bool took = held && Rub(mk);
                    EaseMark(mk);
                    if (took)
                    {
                        // IT THROWS OFF WHAT IT TAKES UP (2026-09-09): a drop every ClothSprayEvery
                        // units of travel, off the cloth's own hem, only on a frame that rubbed.
                        _clothSprayed += Mathf.Abs(dxTravel);
                        while (_clothSprayed >= ClothSprayEvery)
                        {
                            _clothSprayed -= ClothSprayEvery;
                            ShedDrop(at + new Vector2(((_grains.Count * 29) % 23) - 11f, -20f));
                        }
                    }
                    if (mk.Mess.IsClean || InkLeft(mk) > SmudgeGone) continue;
                    try
                    {
                        run.Wipe(mk.Mess);
                        Sfx.Play("cloth_wipe", 0.6f);   // the rag's own sound, three takes in turn (2026-09-15)
                        if (held)
                            for (int i = 0; i < 4; i++) ShedDrop(at + new Vector2((i - 1.5f) * 7f, -16f));
                        // ...AND THE SPOT SAYS WHEN IT IS DONE (2026-09-22, the author: "tezgahtaki o koltugun onu
                        // silinince 4 koseli yildiz parlama iconlari belirmeli bu oyuncu tamamen silindigini
                        // anlamasi icin"). Not per mark - per STOOL: the twinkle is the answer to "is this bit
                        // finished?", and a stool with a second drop still on it is not finished.
                        if (SpotIsClean(run, v)) Sparkle(v);
                    }
                    catch (System.InvalidOperationException e)
                    {
                        if (held && _clothRefused != v) { _clothRefused = v; Toast(UIText.Refusal(e)); }
                    }
                }
        }

        /// <summary>
        /// THE CLOTH WIPES WHAT THE CLOTH COVERS (2026-09-06, the author: "silerken peçetenin
        /// boyutunda bir silme işlemi uygulayacak, tezgahın üstündeki pixellere denk
        /// getirererek silecek, bez pixelle temas edince pixel silinecek, boyama oyunu gibi
        /// tamamını silmek gerekecek"). The first cut rubbed a soft disc under the POINTER,
        /// which is neither where the rag is drawn nor the size of it: the player watched a
        /// hand-sized cloth take out a coin-sized hole somewhere near its top edge.
        ///
        /// This is the rag's own rectangle, walked in the mark's own pixels: every pixel the
        /// cloth's box covers is taken out completely, so wiping is colouring-in — cover it
        /// all and it is gone, miss a corner and the corner stays. Returns whether anything
        /// came off, so a cloth resting on a clean patch neither sounds nor asks Core.
        /// </summary>
        private bool Rub(Mark mk)
        {
            var rt = mk.Prop;
            if (rt == null || _clothRt == null || mk.Orig == null) return false;
            // The rag's four corners, in the mark's own local space, and the box around them —
            // padded, so a texel the cloth has just left is still looked at and can be let go.
            var corners = new Vector3[4];
            _clothRt.GetWorldCorners(corners);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                var local = rt.InverseTransformPoint(corners[c]);
                minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, local.y); maxY = Mathf.Max(maxY, local.y);
            }
            float pad = RubHysteresis;
            int W = ChromeArt.SmudgeW, H = ChromeArt.SmudgeH;
            int x0 = Mathf.FloorToInt(((minX - pad) / rt.rect.width + 0.5f) * W);
            int x1 = Mathf.CeilToInt(((maxX + pad) / rt.rect.width + 0.5f) * W);
            int y0 = Mathf.FloorToInt(((minY - pad) / rt.rect.height) * H);
            int y1 = Mathf.CeilToInt(((maxY + pad) / rt.rect.height) * H);

            // THREE PASSES, NOT ONE (2026-09-14, the author: "tezgah silme özelliği tek seferde
            // temizlemesin 3 kere üstünde ovalaması gereksin fade şeklinde silinen bölge gitsin").
            // A texel counts a pass when the cloth COMES ONTO it — covered now, not covered last
            // frame — and each pass takes a third of its ink, eased out by EaseMark. A cloth resting
            // on a mark rubs nothing; a cloth worked back and forth across it takes it in three.
            // The rag's own rectangle still decides what is under it (2026-09-09), with a couple of
            // units of hysteresis so a swinging hem does not count a pass by itself.
            // AND THE ROW IS THE ONE ON SCREEN (2026-09-14): the texture reads row 0 at the FOOT,
            // and this indexed from the top, so a hem reaching halfway down a mark rubbed out the
            // mirrored strip.
            float halfW = _clothRt.rect.width * 0.5f, cloth = _clothRt.rect.height;
            bool took = false;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (mk.Orig[i] == 0) continue;
                    if (x < x0 || x > x1 || y < y0 || y > y1) { mk.Covered[i] = false; continue; }
                    var here = new Vector3(((x + 0.5f) / W - 0.5f) * rt.rect.width,
                                           ((y + 0.5f) / H) * rt.rect.height, 0f);
                    var inCloth = _clothRt.InverseTransformPoint(rt.TransformPoint(here));
                    float ax = Mathf.Abs(inCloth.x);
                    // the rag hangs from its top edge: pivot (0.5, 1), so y runs 0 .. -height
                    bool inside = ax <= halfW - pad && inCloth.y <= -pad && inCloth.y >= -cloth + pad;
                    bool outside = ax > halfW + pad || inCloth.y > pad || inCloth.y < -cloth - pad;
                    if (inside)
                    {
                        if (mk.Covered[i]) continue;
                        mk.Covered[i] = true;
                        if (mk.Rubs[i] < RubsToClean) { mk.Rubs[i]++; took = true; }
                    }
                    else if (outside) mk.Covered[i] = false;
                }
            if (took && _rubT <= 0f)
            {
                _rubT = 0.16f;                       // the cloth on wet slate, not a machine gun
                Sfx.Play("rim_turn", 0.22f);
            }
            return took;
        }

        /// <summary>Eases every texel of a mark toward what its passes have left of it, so a pass
        /// fades the ink out rather than punching it out. Writes the texture only if a texel moved.</summary>
        private void EaseMark(Mark mk)
        {
            if (mk.Orig == null) return;
            float step = Motion.Reduced ? 1f : RubFadePerSecond * Time.unscaledDeltaTime;
            bool changed = false;
            for (int i = 0; i < mk.Px.Length; i++)
            {
                if (mk.Orig[i] == 0) continue;
                float target = 1f - Mathf.Min(mk.Rubs[i], RubsToClean) / (float)RubsToClean;
                float lv = mk.Level[i];
                if (lv == target) continue;
                lv = Mathf.MoveTowards(lv, target, step);
                mk.Level[i] = lv;
                byte a = (byte)Mathf.RoundToInt(mk.Orig[i] * lv);
                var c = mk.Px[i];
                if (c.a == a) continue;
                mk.Px[i] = new Color32(c.r, c.g, c.b, a);
                changed = true;
            }
            if (!changed) return;
            mk.Tex.SetPixels32(mk.Px);
            mk.Tex.Apply();
        }

        /// <summary>Do these two rects overlap on screen? Corner-box against corner-box, which
        /// is all the cloth and a mark on a flat counter ever need.</summary>
        private static bool Overlaps(RectTransform a, RectTransform b)
        {
            if (a == null || b == null) return false;
            var ca = new Vector3[4]; var cb = new Vector3[4];
            a.GetWorldCorners(ca); b.GetWorldCorners(cb);
            return ca[0].x <= cb[2].x && ca[2].x >= cb[0].x
                && ca[0].y <= cb[2].y && ca[2].y >= cb[0].y;
        }

        /// <summary>What is left of a mark, as a share of what it started with.</summary>
        private float InkLeft(Mark mk)
        {
            if (mk.Ink <= 0) return 0f;
            int ink = 0;
            foreach (var c in mk.Px) ink += c.a;
            return ink / (float)mk.Ink;
        }

        /// <summary>A droplet off the cloth — the grain's own rig, in water's colour.</summary>
        private void ShedDrop(Vector2 at)
        {
            var rt = NewRect("Drop", _hudRoot);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(3f, 3f);
            rt.anchoredPosition = at;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UITheme.Cyan[4];
            img.raycastTarget = false;
            float kick = ((_grains.Count * 37) % 41) / 20f - 1f;
            _grains.Add((rt, img, new Vector2(kick * 26f, -16f), Time.unscaledTime));
        }

        /// <summary>
        /// The press on an empty, watched until it travels. Past the slop the glass comes off
        /// the counter into the hand — Core frees the stool at that moment, not at the press —
        /// and the carry takes over. A press that never travels leaves everything as it was.
        /// </summary>
        private void StepEmptyPress(TycoonRun run)
        {
            if (_emptyPressed == null) return;
            var mouse = Mouse.current;
            if (mouse == null || run == null || run.Phase != TycoonPhase.DayOpen
                || (_flow != null && _flow.IsOpen) || CellarOpen)
            { _emptyPressed = null; return; }
            if (!mouse.leftButton.isPressed) { _emptyPressed = null; return; }
            if ((mouse.position.ReadValue() - _emptyPressAt).magnitude <= TinDragSlop) return;

            var view = _emptyPressed;
            _emptyPressed = null;
            if (view.Dirty == null || !view.Dirty.HasGlass) return;
            // NOTHING HAS HAPPENED TO CORE YET (2026-09-06, the author: "sahnede müşterilerin
            // bıraktığı bardaklar tıklanarak hala yok olabiliyor, sadece lavaboya sürüklenerek
            // yok olmaları lazım"). The lift is a PICTURE until it is let go: an empty that
            // came off the counter and went nowhere used to stay in the hand, which is a
            // glass that vanished from the bar without ever reaching the basin. Core is told
            // at the drop, and only if the drop was over the sink.
            _carriedEmpty = view;
            Sfx.Play("glass_pickup", 0.8f);
            var art = view.DirtyProp != null ? view.DirtyProp.GetComponent<Image>() : null;
            BeginGlassCarry(art != null ? art.sprite : null, view.DirtyProp);
        }

        private void BeginGlassCarry(Sprite art, RectTransform from)
        {
            if (_glassCarry == null)
            {
                _glassCarry = NewRect("GlassInHand", _hudRoot);
                _glassCarry.anchorMin = _glassCarry.anchorMax = new Vector2(0.5f, 0.5f);
                _glassCarry.pivot = new Vector2(0.5f, 0.35f);
                _glassCarry.sizeDelta = new Vector2(34, 52);
                _glassCarryImg = _glassCarry.gameObject.AddComponent<Image>();
                _glassCarryImg.preserveAspect = true;
                _glassCarryImg.raycastTarget = false;
            }
            _glassCarryImg.sprite = art;
            _glassCarryImg.color = art != null ? Color.white : new Color(0.8f, 0.9f, 0.95f, 0.5f);   // solid (2026-09-21)
            if (_sinkFadeRt == _glassCarry) { _sinkFadeRt = null; _sinkFadeImg = null; }   // picked up mid-sink
            // THE SAME GLASS IN THE HAND (2026-09-13): the size it stood at on the counter, not a
            // 34x52 stand-in that visibly shrank the moment it was lifted.
            _glassCarry.sizeDelta = from != null ? from.sizeDelta : new Vector2(34, 52);
            _glassCarry.localScale = Vector3.one;
            _glassCarrying = true;
            _glassCarry.gameObject.SetActive(true);
            _glassCarry.SetAsLastSibling();
            // Lifted from where it stood, by the part of it the finger is on.
            _emptyGrabOffset = Vector2.zero;
            var m = Mouse.current;
            if (m != null && from != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudRoot, m.position.ReadValue(), null, out Vector2 held)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudRoot,
                    RectTransformUtility.WorldToScreenPoint(null, from.TransformPoint(from.rect.center)), null, out Vector2 centre))
            {
                var start = centre - new Vector2(0f, (0.5f - _glassCarry.pivot.y) * _glassCarry.rect.height);
                _glassCarry.anchoredPosition = start;
                _emptyGrabOffset = start - held;
            }
            _glassCarryFromY = _glassCarry.anchoredPosition.y;
        }

        private void StepGlassCarry(TycoonRun run)
        {
            if (!_glassCarrying || _glassCarry == null) return;
            var mouse = Mouse.current;
            if (mouse == null || run == null || run.Phase != TycoonPhase.DayOpen) { EndGlassCarry(false); return; }
            var screen = mouse.position.ReadValue();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudRoot, screen, null, out Vector2 at))
                _glassCarry.anchoredPosition = at + _emptyGrabOffset;
            // SMALLER AS IT GOES DOWN TO THE COUNTER (2026-09-13, the author: "grablerken boyu
            // tezgaha yaklaştıkça küçülsün"): full size where it was lifted, CarryShrink over the
            // basin — the scale the sink's own swallow then starts from.
            float sinkY = (CarrySinkStageY - StageRef.y * 0.5f) * StageToHud;
            float down = Mathf.InverseLerp(_glassCarryFromY, sinkY, _glassCarry.anchoredPosition.y);
            float shrink = Mathf.Lerp(1f, CarryShrink, down);
            _glassCarry.localScale = new Vector3(shrink, shrink, 1f);
            if (mouse.leftButton.isPressed) return;
            // BY THE GLASS, NOT ONLY THE FINGER (2026-09-06, the author: "müşterilerin
            // içtiği bardak lavaboya sürüklenmiyor"). Since the hand keeps its grip, the
            // picture can be over the basin while the pointer is a rim's width off it —
            // and the player is watching the picture. Either counts.
            EndGlassCarry(stage != null && (stage.PointerOverDrain(screen)
                                            || stage.PointerOverDrain(ScreenOf(_glassCarry))));
        }

        private void EndGlassCarry(bool intoTheSink)
        {
            _glassCarrying = false;
            var view = _carriedEmpty;
            _carriedEmpty = null;
            var run = Run;
            if (!intoTheSink || view == null || view.Dirty == null || !view.Dirty.HasGlass
                || run == null || run.Phase != TycoonPhase.DayOpen)
            {
                // Back on the counter it goes, exactly as it was. The prop is drawn from the
                // mess every frame, so there is nothing to put back by hand.
                if (_glassCarry != null) _glassCarry.gameObject.SetActive(false);
                if (view != null) Toast(UIText.T("seats.sink.put_back"), UITheme.Cream[3]);
                return;
            }
            if (run.SinkBusy)
            {
                if (_glassCarry != null) _glassCarry.gameObject.SetActive(false);
                Toast(UIText.T("seats.sink.tap_running"));
                return;
            }
            try
            {
                run.CollectGlass(view.Dirty);
                run.WashGlasses();
            }
            catch (System.InvalidOperationException e)
            {
                if (_glassCarry != null) _glassCarry.gameObject.SetActive(false);
                Toast(UIText.Refusal(e));
                return;
            }
            // INTO THE BASIN, not into thin air (the author: "bardağın lavaboya girdiği bir
            // fade animasyonu gösterilsin bardak direkt yok olmasın").
            SinkFade(_glassCarry, _glassCarryImg);
            Sfx.Play("drain", 0.5f);
            Toast(UIText.T("seats.sink.washing_up"), UITheme.Cyan[4]);
        }

        /// <summary>The screen point under a carried thing's centre: where the PICTURE is,
        /// which is what the player is aiming with.</summary>
        private static Vector2 ScreenOf(RectTransform rt) =>
            rt == null ? new Vector2(-1000f, -1000f)
                : (Vector2)RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));

        // ── the basin swallows what is dropped in it ─────────────────────────────
        private RectTransform _sinkFadeRt;
        private Image _sinkFadeImg;
        private float _sinkFadeT;
        // Longer and deeper than the old fade (2026-09-09): it is a fall into a basin now,
        // and a fall you can see takes about a third of a second.
        private const float SinkFadeSeconds = 0.42f, SinkFadeDrop = 96f;

        /// <summary>Starts a carried picture sinking: down into the basin and gone, over a
        /// quarter of a second. Under reduced motion it is simply put away.</summary>
        private void SinkFade(RectTransform rt, Image img)
        {
            if (rt == null || img == null) return;
            if (Motion.Reduced) { rt.gameObject.SetActive(false); return; }
            _sinkFadeRt = rt; _sinkFadeImg = img; _sinkFadeT = 0f;
            _sinkFadeFrom = rt.anchoredPosition;
            _sinkFadeScale = rt.localScale;
            // A GLASS GOES INTO A SINK, IT DOES NOT EVAPORATE OVER ONE (2026-09-09, the
            // author: "bardak sink'e sürüklenip bırakıldığındaki durum için bir animasyon
            // ekle bardağa"). Three droplets leave the rim as it tips in — the same drops
            // the cloth sheds, so the bar has one kind of splash.
            for (int i = 0; i < 3; i++)
                ShedDrop(_sinkFadeFrom + new Vector2((i - 1) * 9f, -10f));
        }

        private void StepSinkFade()
        {
            if (_sinkFadeRt == null) return;
            float dt = Time.unscaledDeltaTime;
            _sinkFadeT += dt / SinkFadeSeconds;
            // TIPPED, DROPPED AND SWALLOWED. It leans into the basin, falls the basin's own
            // depth on an eased curve rather than at a constant rate, shrinks as it goes
            // down into the bowl, and only then fades — so the eye follows it in instead of
            // watching it dissolve where it was let go.
            float t = Mathf.Clamp01(_sinkFadeT);
            float fall = t * t * SinkFadeDrop;      // in-quad: it accelerates as it drops
            _sinkFadeRt.anchoredPosition = _sinkFadeFrom + new Vector2(0f, -fall);
            _sinkFadeRt.localRotation = Quaternion.Euler(0f, 0f, -34f * Tweening.OutCubic(t));
            float k = Mathf.Lerp(1f, 0.62f, t);
            _sinkFadeRt.localScale = new Vector3(_sinkFadeScale.x * k, _sinkFadeScale.y * k, 1f);
            var c = _sinkFadeImg.color;
            c.a = Mathf.Clamp01(1f - Mathf.Max(0f, t - 0.45f) / 0.55f);
            _sinkFadeImg.color = c;
            if (_sinkFadeT < 1f) return;
            _sinkFadeRt.gameObject.SetActive(false);
            _sinkFadeRt.localRotation = Quaternion.identity;
            _sinkFadeRt.localScale = _sinkFadeScale;
            c.a = 1f; _sinkFadeImg.color = c;
            _sinkFadeRt = null; _sinkFadeImg = null;
        }

        private Vector2 _sinkFadeFrom;
        private Vector3 _sinkFadeScale = Vector3.one;

        /// <summary>The sink's click: wash what the hand holds. The one free verb on the
        /// drain — pouring a drink away is still only by carrying it there.</summary>
        private void OnSinkClicked()
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;
            if (run.GlassesInHand == 0) { Toast(UIText.T("seats.sink.nothing_to_wash")); return; }
            if (run.SinkBusy) { Toast(UIText.T("seats.sink.tap_running")); return; }
            try { run.WashGlasses(); }
            // As the rule wrote it, not in capitals: the one refusal here that never shouted.
            catch (System.InvalidOperationException e) { Toast(Said.TryGet(e, out var l) ? UIText.T(l) : e.Message); return; }
            Sfx.Play("drain", 0.5f);
            Toast(UIText.T("seats.sink.washing_up"), UITheme.Cyan[4]);
        }

        /// <summary>The clock that stands over the basin while its tap runs.</summary>
        private RectTransform _sinkClock, _sinkClockHand;
        private Image _sinkPie;             // the wedge that fills as the tap runs

        /// <summary>
        /// The wait, as an object rather than a number (2026-09-06, the author: "eski tip bir
        /// saat ile 5 saniye bekleme süresine girer"). Built on the first wash and then simply
        /// shown and hidden; the hand sweeps once through the whole wait, so how much is left
        /// is read the way it is read off a clock.
        /// </summary>
        private void BuildSinkClock()
        {
            // A PIE, NOT A CLOCK (2026-09-06, the author: "dairesel peynir gibi saat yönünde
            // dönerek dolacak bir bar"). The ring is the dial; the disc over it is cut by a
            // clockwise radial fill from the top, so the lit wedge is exactly the share of
            // the wait that has gone.
            _sinkClock = NewRect("SinkClock", _hudRoot);
            _sinkClock.anchorMin = _sinkClock.anchorMax = _sinkClock.pivot = new Vector2(0.5f, 0.5f);
            _sinkClock.sizeDelta = new Vector2(24f * StageToHud, 24f * StageToHud);
            var face = _sinkClock.gameObject.AddComponent<Image>();
            face.sprite = ChromeArt.PieRing();
            face.raycastTarget = false;
            _sinkClockHand = NewRect("Wedge", _sinkClock);
            Stretch(_sinkClockHand, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _sinkPie = _sinkClockHand.gameObject.AddComponent<Image>();
            _sinkPie.sprite = ChromeArt.PieDisc();
            _sinkPie.type = Image.Type.Filled;
            _sinkPie.fillMethod = Image.FillMethod.Radial360;
            _sinkPie.fillOrigin = (int)Image.Origin360.Top;
            _sinkPie.fillClockwise = true;
            _sinkPie.fillAmount = 0f;
            _sinkPie.color = UITheme.Cyan[4];
            _sinkPie.raycastTarget = false;

            // THE SECONDS, AT THE DIAL'S TOP RIGHT (2026-09-08, the author: "musluk bekleme
            // süresi musluğun üstünde sağ üstte gözüksün"). The count lived in the hand strip
            // under the basin, a whole readout away from the dial that was showing the same
            // wait without a number. It sits on the dial now, in the badge corner, on its own
            // dark plate — the basin behind it is lit and moving, and cyan type straight onto
            // running water is not type.
            var badge = NewRect("Secs", _sinkClock);
            badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.sizeDelta = new Vector2(30f, 20f);
            badge.anchoredPosition = new Vector2(4f, 2f);
            var plate = badge.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.Card();
            plate.type = Image.Type.Sliced;
            plate.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.92f);
            plate.raycastTarget = false;
            _sinkSecs = NewText("N", badge, _display, 16, TextAnchor.MiddleCenter, UITheme.Cyan[4]);
            Stretch(_sinkSecs.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _sinkSecs.horizontalOverflow = HorizontalWrapMode.Overflow;
            _sinkSecs.verticalOverflow = VerticalWrapMode.Overflow;
            _sinkSecs.raycastTarget = false;

            _sinkClock.gameObject.SetActive(false);
        }

        /// <summary>The seconds left, on the dial over the basin (2026-09-08).</summary>
        private Text _sinkSecs;

        /// <summary>Each frame: the tap runs while Core says so, and the strip over the sink
        /// says what the hand holds.</summary>
        private void StepSink(TycoonRun run)
        {
            bool busy = run != null && run.Phase == TycoonPhase.DayOpen && run.SinkBusy;
            if (stage != null) stage.SetTapRunning(busy);
            // AND THE BASIN CALLS WHILE A HAND IS FULL (2026-09-06): a carried glass or a
            // carried tin both end at the same place, and the room says so by lighting it.
            // NOT WHILE IT IS RUNNING, though (the author: "lavabo animasyona girildiğinde
            // kullanılamaz olacağından parlayıp ön plana çıkmasın"): a basin that cannot take
            // anything must not offer to, so it goes quiet under the pointer as well.
            if (stage != null)
            {
                if (_emptyOver != null && !_emptyOver.gameObject.activeInHierarchy) _emptyOver = null;
                stage.CallTheDrain(!busy && (_glassCarrying || _tinCarrying || _emptyOver != null));
                stage.SetDrainAnswers(!busy);
            }
            // The clock over the basin, while it is busy.
            // NO CLOCK OVER THE BASIN (2026-09-22, the author's seventh list: "ekrandan sink bekleme süresi kalkmalı").
            // The running water says the sink is busy; a pie and a seconds count under it said it twice. It is no
            // longer built; BuildSinkClock stays for a screen that wants it back.
            if (_sinkClock != null)
            {
                // Only over the ROOM: a bench over the counter draws its own scene and the
                // pie, placed for the basin's spot on the counter, was floating on the wall
                // behind the tin (photographed 2026-09-06).
                bool showPie = busy && (_flow == null || !_flow.IsOpen);
                if (_sinkClock.gameObject.activeSelf != showPie) _sinkClock.gameObject.SetActive(showPie);
                if (busy)
                {
                    // UNDER THE BASIN (2026-09-09, the author: "sink bekleme süresi sinkin
                    // hemen altına alınsın"). The dial stood at HUD (280, 237) — the middle
                    // of the counter, a screen away from the sink it was timing. The basin's
                    // fixture slot is stage (110, 68.5) and a slot is the art's FOOT, so the
                    // dial hangs a clock's height under that foot, on the sink's own column.
                    const float SinkStageX = 110f, SinkStageY = 68.5f;
                    _sinkClock.anchoredPosition = new Vector2(
                        (SinkStageX - 320f) * StageToHud,
                        (SinkStageY - 180f) * StageToHud - 30f + CounterLift);
                    float whole = (float)Mathf.Max(0.01f, (float)run.SinkSeconds);
                    float gone = Mathf.Clamp01(1f - (float)run.WashLeft / whole);
                    if (_sinkPie != null) _sinkPie.fillAmount = gone;
                    if (_sinkSecs != null)
                    {
                        // Ceiling, so it reads "1s" for the whole last second and never
                        // shows a 0 over a tap that is still running.
                        string secs = UIText.T("seats.sink.secs", ("secs", Mathf.CeilToInt((float)run.WashLeft)));
                        if (_sinkSecs.text != secs) _sinkSecs.text = secs;
                    }
                }
            }
            if (_handStrip == null) return;
            // WHAT THE GLASSES ARE COSTING, not just where they are (2026-09-06): a glass
            // out of service holds the stool it came off until the sink hands it back, so
            // the strip counts stools rather than crockery.
            int held = run != null ? run.GlassesInHand + run.GlassesWashing : 0;
            // The stools held ride on the end of whatever the strip says, as one counted line.
            string Held(string what) => held > 0 ? UIText.N("seats.strip.held", held, ("line", what)) : what;
            // THE NIGHT WAITS ON THE COUNTER, AND SAYS SO (2026-09-08): with the last
            // drinker gone the doors used to shut at once; they wait for the glasses and the
            // marks now (BarDay.IsComplete), and a shift standing in an empty room needs to
            // be told what it is waiting for.
            if (_emptyOver != null && !_emptyOver.gameObject.activeInHierarchy) _emptyOver = null;
            bool waitingOnCounter = run != null && run.Phase == TycoonPhase.DayOpen
                && run.Floor != null && run.Floor.FloorEmpty && !run.Floor.House.CounterClear;
            string line = run == null || run.Phase != TycoonPhase.DayOpen ? ""
                // The strip no longer counts (2026-09-08): the dial does, at the tap. What
                // is left here is the thing only this line says — what the held glasses cost.
                : waitingOnCounter && !busy ? UIText.T("seats.strip.last_call")
                : busy ? Held(UIText.T("seats.strip.washing"))
                : run.GlassesInHand > 0 ? Held(UIText.N("seats.strip.in_hand", run.GlassesInHand)) : "";
            if (_handStrip.text != line) _handStrip.text = line;
            // Over the sink's own slot (stage 140, 68.5 — the basin is 35 art px tall).
            _handStrip.rectTransform.anchoredPosition = new Vector2(280f, 137f + 70f + 6f + CounterLift);
        }

        /// <summary>Whether this stool's stretch of counter has nothing left on it: no empty, no mark.</summary>
        private bool SpotIsClean(TycoonRun run, SeatView v)
        {
            if (v.Dirty != null && OnTheCounter(run, v.Dirty)) return false;
            foreach (var mk in v.Marks)
                if (mk.Mess != null && !mk.Mess.IsClean && OnTheCounter(run, mk.Mess)) return false;
            return true;
        }

        /// <summary>Five twinkles over the stretch of counter that has just come clean, popping in turn.</summary>
        private void Sparkle(SeatView v)
        {
            // REDUCED MOTION KEEPS THE ANSWER (2026-09-22): this is the game telling the player the spot is
            // finished, not a flourish, so it still appears - it simply stands still and fades instead of popping.
            float x = v.SeatX + HeadX(v);
            for (int i = 0; i < 5; i++)
            {
                float size = 15f + ((i * 5) % 3) * 7f;
                var rt = NewRect("Sparkle", _hudRoot);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(ClearOfTheBoard(x + ((i * 29) % 53) - 26f, size),
                                                  CounterLineY - 30f + CounterLift + ((i * 17) % 19) - 9f);
                rt.localScale = Motion.Reduced ? Vector3.one : Vector3.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = ChromeArt.Sparkle4(17);
                img.color = i % 2 == 0 ? UITheme.Cream[4] : UITheme.Cyan[4];
                img.raycastTarget = false;
                _sparks.Add((rt, img, Time.unscaledTime + (Motion.Reduced ? 0f : i * 0.06f)));
            }
        }

        private readonly List<(RectTransform Rt, Image Img, float Born)> _sparks
            = new List<(RectTransform, Image, float)>();
        /// <summary>How long one twinkle lasts, and how much of that it spends growing. A FIELD, not a const,
        /// so a probe can stretch it: one execute_code round trip is about a second, which is longer than the
        /// whole animation, and a twinkle nobody can photograph is a twinkle nobody can check (2026-09-22).</summary>
        private static float SparkLife = 0.62f;
        private const float SparkRise = 0.32f;

        private void StepSparks()
        {
            if (_sparks.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var (rt, img, born) = _sparks[i];
                float k = (now - born) / SparkLife;
                if (rt == null || k >= 1f)
                {
                    if (rt != null) Destroy(rt.gameObject);
                    _sparks.RemoveAt(i);
                    continue;
                }
                if (k < 0f) continue;                       // its turn has not come round yet
                if (Motion.Reduced)
                {
                    var flat = img.color;
                    img.color = new Color(flat.r, flat.g, flat.b, 1f - k);
                    continue;
                }
                // A pop and a fade: out fast to full size, then away, turning a little as it goes.
                float s = k < SparkRise ? Mathf.SmoothStep(0f, 1f, k / SparkRise)
                                        : Mathf.Lerp(1f, 0.34f, (k - SparkRise) / (1f - SparkRise));
                rt.localScale = new Vector3(s, s, 1f);
                rt.localRotation = Quaternion.Euler(0f, 0f, k * 34f);
                var c = img.color;
                img.color = new Color(c.r, c.g, c.b,
                    k < SparkRise ? 1f : 1f - (k - SparkRise) / (1f - SparkRise));
            }
        }

        private void StepGrains()
        {
            if (_grains.Count == 0) return;
            float now = Time.unscaledTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = _grains.Count - 1; i >= 0; i--)
            {
                var (rt, img, vel, born) = _grains[i];
                float k = (now - born) / GrainLife;
                if (rt == null || k >= 1f)
                {
                    if (rt != null) Destroy(rt.gameObject);
                    _grains.RemoveAt(i);
                    continue;
                }
                vel = new Vector2(vel.x * 0.94f, vel.y - GrainFall * dt);
                rt.anchoredPosition += vel * dt;
                var c = img.color;
                img.color = new Color(c.r, c.g, c.b, 1f - k * k);
                _grains[i] = (rt, img, vel, born);
            }
        }

        /// <summary>One cube out of the hand and into the drink: it leaves with the hand's
        /// own sideways drift, falls, and is gone by the time it reaches the level — the pile
        /// GlassDecor draws is what it turns into. The sideways kick comes from the COUNT, the
        /// same walked arithmetic the shed grains use, so a pour repeats exactly.</summary>
        private void DropCube(Vector2 at)
        {
            if (_prepCarry == null) return;
            var rt = NewRect("Cube", (RectTransform)_prepCarry.parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = _prepCarry.sizeDelta;
            rt.anchoredPosition = at + new Vector2(0f, -10f);
            var img = rt.gameObject.AddComponent<Image>();
            // WHAT FALLS IS WHAT LANDS: the cube just added has a roll of its own, and the
            // pile GlassDecor is about to draw will show that same drawing at that index.
            var run = Run;
            var rolls = run != null ? run.IceCubeRolls() : null;
            img.sprite = rolls != null && rolls.Count > 0
                ? GlassDecor.IceSprite(rolls[rolls.Count - 1])
                : (_prepCarryImg != null ? _prepCarryImg.sprite : null);
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (img.sprite == null) img.color = new Color(0.75f, 0.9f, 1f, 0.9f);
            float kick = ((_grains.Count * 37) % 41) / 20f - 1f;   // -1..1, walked, not rolled
            _grains.Add((rt, img, new Vector2(kick * 22f, -30f), Time.unscaledTime));
        }

        private void DropPrep(bool intoTheGlass)
        {
            var prop = _prepHeld;
            _prepHeld = null;
            _icePoured = -1f;
            _icePourCount = 0;
            if (_prepCarry != null)
            {
                _prepCarry.localRotation = Quaternion.identity;
                _prepCarry.gameObject.SetActive(false);
            }
            ShowRimRing(false);
            _rimAngleKnown = false;
            _rimLoopWanted = false;
            Sfx.HoldLoop(null);
            if (!intoTheGlass || prop == null) return;
            // A RIM IS NEVER APPLIED BY A DROP. Putting the dish down over the glass is
            // putting the dish down; what puts salt on a rim is the lap, and a half-run one
            // waits on the counter until the dish is picked up again.
            if (prop.IsRim) return;
            var run = Run;
            if (run == null) return;

            // Stock, not a mark: the olive and the mint are poured out of a bottle the bar
            // owns, through the one Core verb that puts an ingredient straight into the
            // serving glass. A pinch, measured against the GLASS rather than the tin -
            // PourGarnish's own fraction, in the vessel this drop is actually aimed at.
            if (prop.Prep == null)
            {
                var bottle = GarnishOnTheShelf(run, prop.Style);
                if (bottle == null) { Toast(UIText.T("seats.garnish.none_left")); return; }
                double pinch = run.ServingGlass.Capacity * GarnishPinch;
                if (run.PourAtGlass(bottle.Id, pinch) <= 0)
                { Toast(UIText.T("seats.garnish.glass_full")); return; }
                Sfx.Play("garnish");
                Toast(UIText.T("seats.garnish.in_drink",
                          ("bottle", UIText.Caps(UIText.Data("bottle", bottle.Id, "name", bottle.Name)))),
                      UITheme.Lime[3]);
                return;
            }

            if (prop.Id != "ice" && run.ServingGlass.HasPreparation(prop.Id))
            {
                Toast(UIText.T("seats.garnish.already_on"));
                return;
            }
            // A PINT TAKES A LEMON AND A RIM OF SALT (2026-09-22): Core refuses the rest, so the rail says why
            // rather than letting the drop throw.
            if (!run.PreparationSuitsGlass(prop.Prep))
            {
                Toast(UIText.T("seats.garnish.not_in_beer"));
                Sfx.Play("deny", 0.8f);
                return;
            }
            run.AddPreparationAtGlass(prop.Prep);
            Sfx.Play(prop.Id == "ice" ? "ice_drop" : "garnish");
            Toast(prop.Id == "ice"
                ? UIText.T("seats.garnish.ice_in", ("cubes", run.ServingGlass.IceCubes))
                : UIText.T("seats.garnish.on_drink", ("garnish", UIText.Caps(UIText.T(prop.Prep.NameLine)))),
                UITheme.Cyan[3]);
        }

        /// <summary>How much of the glass one tap of a garnish is worth. The tin's own
        /// GarnishClickFraction, so a pinch is a pinch wherever it is taken.</summary>
        private const double GarnishPinch = 0.05;

        /// <summary>The bar's bottle of this garnish style, or null if it stocks none or has
        /// emptied the one it had. Asked at DROP time and never cached: a jar the bar ran out
        /// of halfway through a night must stop pouring halfway through that night.</summary>
        private static IngredientCard GarnishOnTheShelf(TycoonRun run, string style)
        {
            if (run == null || string.IsNullOrEmpty(style)) return null;
            foreach (var b in run.Shelf.Bottles)
                if (!b.IsEmpty && b.Ingredient.Type == IngredientType.Garnish
                    && b.Ingredient.Info != null && b.Ingredient.Info.Style == style)
                    return b.Ingredient;
            return null;
        }

        /// <summary>
        /// The lap, run against the drink standing on the counter. Called from StepPrepCarry
        /// while a rim dish is in the hand; returns true when the crust went on, which is the
        /// signal to put the dish back.
        /// </summary>
        private bool StepRimLap(TycoonRun run, PrepProp prop, Vector2 screen)
        {
            var mouth = GlassMouth();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_prepCarry.parent, screen, null, out Vector2 local))
                return false;
            ShowRimRing(true);
            PlaceRimRing(mouth, prop);

            _rimLoopWanted = true;      // consumed once a frame by StepPreps

            var arm = local - mouth;
            float dist = arm.magnitude;
            if (dist < RimNear || dist > RimFar) { _rimAngleKnown = false; return false; }

            float angle = Mathf.Atan2(arm.y, arm.x);
            if (_rimAngleKnown)
            {
                float step = Mathf.Abs(Mathf.DeltaAngle(_rimAngle * Mathf.Rad2Deg,
                                                        angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (step < RimLap / 3f)
                {
                    _rimSwept.TryGetValue(prop.Id, out float swept);
                    swept += step;
                    _rimSwept[prop.Id] = swept;
                    if (swept >= RimLap)
                    {
                        // Salt on a pint, never sugar (2026-09-22): the lap is done, the rim is refused.
                        if (!run.PreparationSuitsGlass(prop.Prep))
                        {
                            _rimSwept.Remove(prop.Id);
                            _rimAngleKnown = false;
                            Toast(UIText.T("seats.garnish.not_in_beer"));
                            Sfx.Play("deny", 0.8f);
                            return true;
                        }
                        run.AddPreparationAtGlass(prop.Prep);
                        Sfx.Play("rim_done", 0.9f);
                        _rimSwept.Remove(prop.Id);
                        Toast(UIText.T(prop.Id == "salt_rim" ? "seats.rim.salt_done" : "seats.rim.sugar_done"),
                              UITheme.Lime[3]);
                        _rimAngleKnown = false;
                        return true;
                    }
                }
            }
            _rimAngle = angle;
            _rimAngleKnown = true;
            return false;
        }

        /// <summary>Where the drink's mouth is, in the rail's own space.</summary>
        private Vector2 GlassMouth() =>
            _drinkGlass == null ? Vector2.zero
            : _drinkGlass.anchoredPosition + new Vector2(0f, _drinkGlass.rect.height * 0.34f);

        /// <summary>
        /// The lap's instrument (2026-08-26, the author: "tuz ve sekeri bardagin etrafina
        /// surdugumuz mini oyun gelistirilsin, gorsel olarak hic estetik ve iyi degil").
        ///
        /// It was fourteen 5×13 rectangles on a circle, half-lit, and nothing else: no
        /// centre, no reading, no sense of a lap being RUN — the author's word for it was
        /// "boxes", and that is what it was. Four things now, and each earns its place:
        ///
        ///   the SEAT   a dim ring of the same fourteen marks, so the circle you are being
        ///              asked to run is visible before you start running it
        ///   the CRUST  the marks behind the sweep, in the dish's own colour and TALLER —
        ///              a crust builds up, so the mark grows as it takes
        ///   the HEAD   the mark under the cursor burns brighter and stands proudest, which
        ///              is the one thing that says "this is where you are"
        ///   the COUNT  the lap's own percentage in the middle of the glass's mouth, in the
        ///              house's display face, so a half-run rim is a number and not a guess
        /// </summary>
        private void ShowRimRing(bool on)
        {
            if (_rimRing == null)
            {
                if (!on) return;
                _rimRing = NewRect("RimRing", (RectTransform)_prepCarry.parent);
                _rimRing.anchorMin = _rimRing.anchorMax = _rimRing.pivot = new Vector2(0.5f, 0.5f);
                _rimRing.sizeDelta = Vector2.zero;
                for (int i = 0; i < RimSegments; i++)
                {
                    var tick = NewRect("T" + i, _rimRing);
                    tick.anchorMin = tick.anchorMax = tick.pivot = new Vector2(0.5f, 0.5f);
                    tick.sizeDelta = new Vector2(6f, 12f);
                    var img = tick.gameObject.AddComponent<Image>();
                    img.sprite = ChromeArt.Card();
                    img.type = Image.Type.Sliced;
                    img.raycastTarget = false;
                    _rimTicks.Add(img);
                }
                _rimCount = NewText("Lap", _rimRing, _display, 8, TextAnchor.MiddleCenter,
                                    UITheme.Cream[4]);
                Place(_rimCount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(120, 16),
                      Vector2.zero);
                _rimCount.raycastTarget = false;
                var edge = _rimCount.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0f, 0f, 0f, 0.9f);
                edge.effectDistance = new Vector2(1f, -1f);
            }
            if (_rimRing.gameObject.activeSelf != on) _rimRing.gameObject.SetActive(on);
            if (on) _rimRing.SetAsLastSibling();
        }

        private Text _rimCount;

        /// <summary>Stands the ring on the drink's mouth and colours it by how far the lap
        /// has run — tick colour, no arc: the bench's own reading, at the bench's own
        /// fourteen segments, so the two are one gesture with one picture.</summary>
        private void PlaceRimRing(Vector2 mouth, PrepProp prop)
        {
            if (_rimRing == null) return;
            _rimRing.anchoredPosition = mouth;
            _rimSwept.TryGetValue(prop.Id, out float swept);
            float ran = Mathf.Clamp01(swept / RimLap);
            var lit = prop.Id == "sugar_rim" ? UITheme.Amber[4] : UITheme.Cream[4];
            var seat = new Color(1f, 1f, 1f, 0.15f);
            // The head is where the HAND is, not where the fill ends: the lap counts a
            // swept ANGLE, so the cursor may be anywhere on the circle while the crust
            // fills from the start. The mark under the cursor is the one that burns.
            int head = _rimAngleKnown
                ? Mathf.RoundToInt(Mathf.Repeat(_rimAngle / RimLap, 1f) * RimSegments) % RimSegments
                : -1;
            for (int i = 0; i < _rimTicks.Count; i++)
                {
                float a = (i / (float)RimSegments) * RimLap;
                bool crusted = (i / (float)RimSegments) < ran;
                bool burning = i == head;
                // A crust BUILDS: the mark grows as it takes, and the one being laid now
                // stands proudest of all.
                float len = burning ? 20f : crusted ? 15f : 10f;
                float wide = burning ? 8f : crusted ? 7f : 5f;
                var rt = _rimTicks[i].rectTransform;
                rt.sizeDelta = new Vector2(wide, len);
                rt.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RimRingRadius;
                rt.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90f);
                _rimTicks[i].color = burning ? Color.white : crusted ? lit : seat;
            }
            if (_rimCount != null)
            {
                _rimCount.text = UIText.T("seats.rim.percent", ("pct", Mathf.RoundToInt(ran * 100f)));
                _rimCount.color = ran >= 1f ? UITheme.Lime[3] : lit;
            }
        }

        /// <summary>How far out the ring stands from the mouth. Inside RimFar and outside
        /// RimNear, so the marks sit in the band the sweep is actually counted in — a ring
        /// drawn where the hand is not counted is a ring that lies.</summary>
        private const float RimRingRadius = 62f;

        /// <summary>
        /// The rail, every frame: out whenever the bar is open, dimmed where a piece has
        /// already been used, and carrying whatever is in the hand.
        ///
        /// ALWAYS OUT (2026-08-26). It used to appear only beside a finished drink, which
        /// made it read as a prompt; a bar's garnish tray does not come and go, and the
        /// player is meant to know where these things live before they need them. What
        /// changes with the drink is whether a piece can be USED, and that is said by the
        /// dimming and by the drop refusing — not by the tray vanishing.
        /// </summary>
        private void StepMiniPreps(TycoonRun run)
        {
            if (_prepRail == null) return;
            // THE RAIL DOES NOT VANISH WHEN THE CELLAR OPENS (2026-08-26, the author:
            // "garnishler kapak acmak icin bastigimizda yok oluyorlar"). It used to be
            // switched off with the drawer; the dishes are standing on the bar, the bar
            // rises with the room, and a tray that disappears the moment you reach behind
            // it reads as a bug. It rides CounterLift up instead — and its props stop
            // ANSWERING the pointer while the drawer is open, because the cellar's own
            // doors are under them and a click meant for a bottle must reach the bottle.
            bool on = run != null && run.Phase == TycoonPhase.DayOpen
                      && (_flow == null || !_flow.IsOpen);
            if (_prepRail.gameObject.activeSelf != on)
                _prepRail.gameObject.SetActive(on);
            if (!on)
            {
                if (_prepHeld != null) DropPrep(false);
                return;
            }
            _prepRail.anchoredPosition = new Vector2(0, CounterLift);
            if (_coasterRt != null)
            {
                if (!_coasterRt.gameObject.activeSelf) _coasterRt.gameObject.SetActive(true);
                // On the foot line the dishes and the glass stand on, riding the bar with
                // them — plus the mat's own few pixels back into the counter, because it
                // LIES on the bar rather than standing on it (see CoasterLift).
                _coasterRt.anchoredPosition =
                    new Vector2(GlassHomeX, CounterFootY + CoasterLift + CounterLift);
            }
            bool reachable = !CellarOpen;
            if (!reachable && _prepHeld != null) DropPrep(false);

            bool glass = run.DrinkReady && _glassShown && !_glassServing && !_glassReturning;
            // WHAT THE BAR OWNS, AND NOTHING ELSE (2026-08-26, the author: "bazilari
            // ileriki seviyelerde acilacakti"). Ice, the twist and the two rims are house
            // basics and always out. The olive and the mint are STOCK, and base_bar.json
            // has always priced them behind three and four stars — the gate existed in the
            // economy and the rail was not reading it. A jar the bar has not bought, or has
            // emptied tonight, is not on the counter; buying one puts it there.
            int slot = 0;
            foreach (var prop in _prepProps)
            {
                // ...AND WHAT THE LADDER HAS OPENED (2026-09-21): ice and the twist at half a star, the rims at
                // one — a dish the rank has not brought is not on the counter, and Core would refuse the drop anyway.
                bool stocked = prop.Prep != null ? run.PreparationOpen(prop.Prep)
                             : prop.Style == null || GarnishOnTheShelf(run, prop.Style) != null;
                if (prop.Rt.gameObject.activeSelf != stocked)
                    prop.Rt.gameObject.SetActive(stocked);
                if (!stocked) continue;
                // Laid out by VISIBLE index, so an unbought garnish leaves no hole in the
                // row — the rail closes up and still ends where the coaster begins.
                // The x closes up as a slot goes unstocked; the y is the DISH'S OWN and
                // never moves, because it is where that drawing touches the counter.
                prop.Rt.anchoredPosition = new Vector2(PrepRailX0 + slot * PrepRailGap, prop.Rest);
                slot++;

                // Spent, or nothing to spend it on. The bucket is never spent — ice is
                // counted, not applied — which is the one exception the glass already makes.
                bool done = glass && prop.Prep != null && prop.Id != "ice"
                            && run.ServingGlass.HasPreparation(prop.Id);
                // ...and a dish a PINT will not take darkens the same way (2026-09-22), so the player sees what
                // beer is finished with rather than learning it from a refusal.
                bool wrong = glass && prop.Prep != null && !run.PreparationSuitsGlass(prop.Prep);
                // DIMMED, NOT SEE-THROUGH (2026-09-21, the author: "garnishlerde şeffaflık var ... katı olması
                // gerekiyor"): a dish that cannot be used yet, or has been, darkens instead of fading, so the
                // counter never shows through it.
                var baseCol = prop.Img.sprite != null ? Color.white : UITheme.Cyan[3];
                float dim = !glass ? 0.6f : done || wrong ? 0.45f : 1f;
                // NO HAND-MADE LIGHT ANY MORE (2026-09-22): the dish is a stage sprite now (IntoTheRoom), so the
                // room's own lamps fall on it. What is left here is what the RAIL is saying - a dish that cannot
                // be used, or has been, darkens - and the room does the rest.
                prop.Img.color = new Color(baseCol.r * dim, baseCol.g * dim, baseCol.b * dim, 1f);
                prop.Img.raycastTarget = reachable;
            }
            // THE MAT IS AS LONG AS THE RAIL IS (2026-09-06, the author: "yeni garnish
            // eklendiginde onlarin da altina gelecek sekilde ortalansin"). The row closes up
            // as jars go unstocked and opens out as they are bought, so the mat is measured
            // from what is actually STANDING there this frame — not from the six slots the
            // rail could hold — and re-centred on that span.
            // THE MAT GROWS WITH THE RAIL (2026-09-06, the author: "çerez matı mevcut
            // garnishlerin tamamını ortalamış bir şekilde kapsamıyor ... yeni garnish
            // geldiğinde de ona göre boyutu sağ ve sola doğru uzamalı"). The fixture in the
            // room is told the standing dishes' span, in the HUD's own units, whenever the
            // count changes — the dishes sit at PrepRailX0 + i*PrepRailGap, so the mat is
            // centred on that run and reaches half a box past the first and last of them.
            if (stage != null && slot != _prepMatSlots)
            {
                _prepMatSlots = slot;
                float span = Mathf.Max(0, slot - 1) * PrepRailGap;
                stage.SetPrepMatSpan(PrepRailX0 + span * 0.5f, span + PrepDishBox, slot);
            }
            StepSinkFade();
            StepPrepCarry(run);
            StepGrains();
            StepSparks();
            StepCloth(run);
            StepEmptyPress(run);
            StepGlassCarry(run);
            StepSink(run);
            // THE RIM'S GRIND, decided once (2026-08-27). The flag is set by StepRimLap
            // while the lap is actually turning and cleared here after it is read, so a
            // cursor that leaves the band, a dish that is put down, or a stage that opens
            // over the room all stop the sound by simply not asking for it again.
            // The TAP shares the channel (H4): it runs while Core says the sink is busy, and
            // the rim's grind, being the one in the hand, takes precedence.
            string loop = _rimLoopWanted ? "rim_turn" : run.SinkBusy ? "tap_water" : null;
            Sfx.HoldLoop(loop, loop == "tap_water" ? 0.45f : 0.7f);
            _rimLoopWanted = false;
        }

        // ── the drink you carry (GDD 24 §3, 2026-07-22) ──────────────────────────

        private void BuildDrinkGlass(RectTransform root)
        {
            // BETWEEN THE LAST DISH AND THE DRIP MAT (2026-08-26, the author: "son
            // garnish ile bar mati arasinda tezgahin ustunde durmali"). The rail runs to
            // stage 380 at its longest and the mat starts at 480; the drink stands at 430,
            // in the gap, and every drag off a dish travels right into it.

            // THE COASTER IS ALWAYS THERE (same note: "tam bardagin koyulacagi yere bir
            // bardak altligi olmali sahnede her zaman"). It is the drink's PLACE, so it is
            // drawn whether or not there is a drink on it — an empty coaster is what tells
            // the player where the next one will land, and it is why the glass no longer
            // looks like it is floating on a strip of counter. Built before the glass, so
            // the drink stands ON it.
            // DRAWN, at the proportion the counter needs (2026-08-26), and REDRAWN with a
            // body on 2026-09-04 (the author: "sahnedeki bardak altligi yeniden uretilmeli
            // ve masanin yuzeyine tam otursun") — the flat ellipse read as a stain on the
            // bar, and centred on the old foot line its front arc hung off the counter into
            // the shelf bays. The mat has an edge now and the line moved up to hold it.
            // See BackBarArt.Coaster and CounterFootY.
            var coaster = NewRect("Coaster", root);
            coaster.anchorMin = coaster.anchorMax = coaster.pivot = new Vector2(0.5f, 0.5f);
            coaster.sizeDelta = new Vector2(112f, 36f);
            _coasterRt = coaster;
            var coasterImg = coaster.gameObject.AddComponent<Image>();
            coasterImg.sprite = BackBarArt.Coaster();
            coasterImg.raycastTarget = false;
            coaster.gameObject.SetActive(false);
            BuildShakerProp(root);

            // (The bin used to be built here, before the glass, so the carried drink passed
            //  over it. It went on 2026-08-26 and the sink took the verb — see TycoonHud's
            //  own headstone for it, and OnDrainClicked below.)

            // The drink you carry to a seat is the real glass now (v5 P14 / C9): the same
            // drawing the serve stage stands on the counter, with its interior filled to the
            // level the drink is actually at. It used to be a translucent box with a cyan bar
            // for a rim, which said "a drink" and nothing about WHICH drink.
            _drinkGlass = NewRect("DrinkGlass", root);
            _drinkGlass.anchorMin = _drinkGlass.anchorMax = _drinkGlass.pivot = new Vector2(0.5f, 0.5f);
            _drinkGlass.sizeDelta = new Vector2(78, CarriedGlassHeight);
            _drinkGlass.anchoredPosition = GlassHome;

            // THE GLASS IS PICKED UP AGAIN (2026-08-11, the author: back to dragging
            // instead of clicking). Clicking a customer to serve them was the wrong verb for
            // the one moment in the loop that is physical: you have made a drink, and what
            // you do with a drink is carry it to somebody. The whole rect takes the press —
            // a glass is a narrow silhouette, and asking for the glass itself would be a
            // precision test nobody signed up for.
            var body = _drinkGlass.gameObject.AddComponent<Image>();
            body.color = new Color(0f, 0f, 0f, 0.004f);
            body.raycastTarget = true;
            var grab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            grab.callback.AddListener(_ =>
            {
                var run = Run;
                if (run == null || run.Phase != TycoonPhase.DayOpen) return;
                if (_flow != null && _flow.IsOpen) return;
                if (!_glassShown || _glassServing || _glassReturning || !run.DrinkReady) return;
                _glassGrabbed = true;
                _glassVel = Vector2.zero;
                // WHERE THE PRESS STARTED, so a press that never travels can be told from a
                // carry (2026-09-06, the author: "bardak altlığının üstünde gözüken bardak
                // veya shaker'a sol tık yaptığımızda, bardak varsa bardak sahnesine shaker
                // varsa shaker sahnesine gitmeli").
                var m = Mouse.current;
                _glassPressAt = m != null ? m.position.ReadValue() : Vector2.zero;
                _glassTravelled = false;
                _glassGrabOffset = Vector2.zero;
                if (m != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)_drinkGlass.parent, _glassPressAt, null, out Vector2 held))
                    _glassGrabOffset = _drinkGlass.anchoredPosition - held;
                Sfx.Play("click", 0.5f);
            });
            _drinkGlass.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);
            // IT ANSWERS THE POINTER LIKE EVERYTHING ELSE YOU CAN PICK UP (2026-09-06, the
            // author: "bardağı servis ederken ana sahnede bardak parlamıyor"). The light is
            // cut from the glass's FRONT face, which is the drawing you see; the hit plate
            // it rides is a transparent rectangle and would have lit as a rectangle.
            _drinkGlassGlow = _drinkGlass.gameObject.AddComponent<HoverGlow>();
            _drinkGlassGlow.Rise = 4f; _drinkGlassGlow.Sway = 1.4f; _drinkGlassGlow.Grow = 1.05f;
            // ...AND IT SAYS WHAT THE TWO PRESSES DO (2026-09-06, the author: "sağ tık sol tık
            // etkileşiminin farkını göstermek için bardağın üstüne geldiğimizde bilgi kutusu
            // çıkmalı"). One line, both verbs, in the room's own tip plate.
            var glassRelay = _drinkGlass.gameObject.AddComponent<HoverRelay>();
            var glassRt = _drinkGlass;
            glassRelay.Entered = () => ShowPropTip(glassRt, UIText.T("seats.tip.glass"));
            glassRelay.Exited = () => HidePropTip(glassRt);

            // The layer architecture (the author, 2026-08-02): BACK face and base first,
            // the liquid over it, the FRONT face — interior fully clear — on top.
            var backRt = NewRect("Back", _drinkGlass);
            Stretch(backRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassBack = backRt.gameObject.AddComponent<Image>();
            _drinkGlassBack.preserveAspect = true;
            _drinkGlassBack.raycastTarget = false;
            _drinkGlassBack.enabled = false;

            var liquid = NewRect("Liquid", _drinkGlass);
            Stretch(liquid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassLiquid = liquid.gameObject.AddComponent<Image>();
            _drinkGlassLiquid.raycastTarget = false;
            _drinkGlassLiquid.type = Image.Type.Filled;
            _drinkGlassLiquid.fillMethod = Image.FillMethod.Vertical;
            _drinkGlassLiquid.fillOrigin = (int)Image.OriginVertical.Bottom;
            _drinkGlassLiquid.preserveAspect = true;

            // THE TOP OF THE DRINK IS AN ELLIPSE (2026-08-11, the author: the glass is 3D, so
            // what is in it has to be). A vertical fillAmount cuts the interior with a straight
            // edge — right for the body, wrong for the surface, which is the one place the
            // drink shows the player it is a cylinder and not a picture of one. It goes over
            // the liquid and under the front face, so the glass's own wall still crosses it.
            var surf = NewRect("Surface", _drinkGlass);
            surf.anchorMin = surf.anchorMax = surf.pivot = new Vector2(0.5f, 0.5f);
            _drinkGlassSurface = surf.gameObject.AddComponent<Image>();
            _drinkGlassSurface.sprite = GlassArt.SurfaceDisc();
            _drinkGlassSurface.raycastTarget = false;
            _drinkGlassSurface.enabled = false;

            var art = NewRect("Art", _drinkGlass);
            Stretch(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassArt = art.gameObject.AddComponent<Image>();
            _drinkGlassArt.raycastTarget = false;
            _drinkGlassArt.preserveAspect = true;
            // BEHIND THE DRINK (2026-09-14, the author: "görsel->sıvı->(garnishler)->görsel_front"):
            // the whole sheet stands under the liquid, and the author's _Front crop (the Lip) is the
            // one thing drawn in front of it. It stays the glow's drawing.
            art.SetSiblingIndex(liquid.GetSiblingIndex());
            // THE RIM'S FRONT EDGE, over everything (2026-09-08, the author's `_Front`
            // strips): the liquid's surface disc used to draw over the front of the rim,
            // which is what made a full glass look like a glass with a plate of liquid on
            // it. The lip is the last layer, placed on the rim by GlassArt.Piece.LipPlacement.
            var lipRt = NewRect("Lip", _drinkGlass);
            lipRt.anchorMin = lipRt.anchorMax = new Vector2(0.5f, 1f);
            lipRt.pivot = new Vector2(0.5f, 1f);
            _drinkGlassLip = lipRt.gameObject.AddComponent<Image>();
            _drinkGlassLip.raycastTarget = false;
            _drinkGlassLip.enabled = false;
            // The rim's crust rides over the front crop (2026-09-14): GlassDecor hangs it here.
            _drinkGlassRimOver = NewRect("RimOver", _drinkGlass);
            Stretch(_drinkGlassRimOver, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // ...and the lemon wheel rides UNDER the drink (2026-09-18, the author: "limon katman
            // olarak sıvı katmanında arkasında olacak"). Slotted in at the liquid's own index, which
            // pushes the liquid one on: sheet, front face, wheel, drink.
            _drinkGlassUnder = NewRect("Under", _drinkGlass);
            Stretch(_drinkGlassUnder, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassUnder.SetSiblingIndex(liquid.GetSiblingIndex());
            // The glow's drawing, now that there is one: the front face is the glass you see.
            if (_drinkGlassGlow != null) _drinkGlassGlow.Graphics = new Graphic[] { _drinkGlassArt };

            var hint = NewText("Hint", _drinkGlass, _body, 10, TextAnchor.UpperCenter, UITheme.Cyan[4]);
            Place(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(190, 18), new Vector2(0, 24));
            hint.text = UIText.T("seats.glass.hint");
            hint.raycastTarget = false;

            _drinkGlass.gameObject.SetActive(false);
        }

        /// <summary>
        /// Puts the drink's top face where the drink's top is, at the width the glass has
        /// there.
        ///
        /// The art is letterboxed inside its rect by preserveAspect, so the sprite's own box
        /// is worked out first: everything the level and the profile say is in SPRITE
        /// fractions, and placing them against the rect instead would float the surface off
        /// the liquid on any glass whose drawing is not exactly the rect's shape.
        /// </summary>
        private void PlaceDrinkSurface(GlassArt.Piece piece, float fraction)
        {
            if (_drinkGlassSurface == null) return;
            if (piece.Fill == null || fraction <= 0f || piece.Aspect <= 0f)
            {
                _drinkGlassSurface.enabled = false;
                return;
            }

            Vector2 rect = _drinkGlass.rect.size;
            float drawnH = Mathf.Min(rect.y, rect.x / piece.Aspect);
            float drawnW = drawnH * piece.Aspect;

            float level = piece.FillAmount(fraction);          // 0..1 up the sprite
            float width = piece.InteriorWidthAt(level) * drawnW;
            if (width <= 1f) { _drinkGlassSurface.enabled = false; return; }

            var rt = _drinkGlassSurface.rectTransform;
            rt.sizeDelta = new Vector2(width, width * GlassArt.SurfaceSquash);
            rt.anchoredPosition = new Vector2(0f, (level - 0.5f) * drawnH);
            // A shade lighter than the body: the top face catches the room, and without the
            // lift it reads as a hole in the drink rather than the top of it.
            var body = DrinkColor();
            _drinkGlassSurface.color = new Color(
                Mathf.Lerp(body.r, 1f, 0.24f), Mathf.Lerp(body.g, 1f, 0.24f),
                Mathf.Lerp(body.b, 1f, 0.24f), body.a);
            _drinkGlassSurface.enabled = true;
        }

        /// <summary>
        /// The SINK's click: pours the ready drink away, and pays for it (2026-08-26). Inert
        /// with nothing to pour — an empty counter never nags, and the basin is scenery the
        /// rest of the night.
        ///
        /// What it COSTS is Core's answer, not this one's: the steel basin the bar opens with
        /// writes the goods off, the brass one it can fit later does not
        /// (TycoonRun.WasteIsFree), and the toast reports whichever came back. That is the
        /// whole of the upgrade, and the first piece of dressing that changes what the bar
        /// can afford to do.
        /// </summary>
        private void OnDrainClicked()
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;
            if (!_glassShown || _glassServing || _glassReturning || !run.DrinkReady) return;
            // EVERY USE OF THE SINK STARTS THE WAIT (2026-09-06). Tipping a drink away
            // busies the basin exactly as a stack of glasses does, so whatever is queued
            // behind it waits — and the stools those glasses came off wait with them.
            int fee;
            try { fee = run.PourAwayAtSink(); }
            catch (InvalidOperationException) { Toast(UIText.T("seats.sink.tap_running")); return; }
            Sfx.Play("drain", 0.9f);
            Toast(fee > 0 ? UIText.T("seats.drain.poured_fee", ("fee", "-$" + fee)) : UIText.T("seats.drain.poured"));
            if (fee > 0)
                LogService(UIText.T("seats.log.poured_away", ("fee", "-$" + fee)));
            _drinkGlass.gameObject.SetActive(false);
            _glassShown = false;
        }

        /// <summary>The finished drink sits on the counter and is dragged onto a customer to
        /// serve (GDD 24 §3). Heavy, springy carry with a lean into the motion (AAA feel).</summary>
        /// <summary>
        /// THE TIN ON THE COUNTER (2026-09-06). Built beside the coaster it stands on, and
        /// before the drink glass, so a glass being carried draws over it rather than under.
        /// Everything about WHEN it is there is <see cref="UpdateShakerProp"/>'s.
        /// </summary>
        private void BuildShakerProp(RectTransform root)
        {
            _shakerProp = NewRect("ShakerProp", root);
            _shakerProp.anchorMin = _shakerProp.anchorMax = _shakerProp.pivot = new Vector2(0.5f, 0.5f);
            _shakerProp.sizeDelta = new Vector2(ShakerPropBox, ShakerPropBox);
            _shakerPropImg = _shakerProp.gameObject.AddComponent<Image>();
            _shakerPropImg.preserveAspect = true;
            // The drawing takes the click, not its box: the tin is a narrow silhouette on a
            // square sheet, and a box would catch presses meant for the counter beside it.
            // (The sheet is readable — the postprocessor sets it — so alphaHitTest works.)
            _shakerPropImg.raycastTarget = true;
            _shakerPropImg.alphaHitTestMinimumThreshold = 0.4f;
            var btn = _shakerProp.gameObject.AddComponent<Button>();
            btn.targetGraphic = _shakerPropImg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                var run = Run;
                if (run == null || run.Phase != TycoonPhase.DayOpen) return;
                if (_flow == null || _flow.IsOpen) return;
                Sfx.Play("tin_tip", 0.6f);
                _flow.OpenShaker();
            });
            var glow = _shakerProp.gameObject.AddComponent<HoverGlow>();
            glow.Graphics = new UnityEngine.UI.Graphic[] { _shakerPropImg };
            glow.Rise = 5f; glow.Sway = 1.6f; glow.Grow = 1.06f;
            var relay = _shakerProp.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () => _shakerPropHovered = true;
            relay.Exited = () => _shakerPropHovered = false;

            // A press is watched rather than acted on: travel past the slop and the tin comes
            // off the mat into the hand (StepTinCarry), and the button's own click never
            // fires because the prop is gone from under the pointer by the time it is let go.
            var press = _shakerProp.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                var m = Mouse.current;
                if (m == null) return;
                _tinPressed = true;
                _tinPressAt = m.position.ReadValue();
            });
            press.triggers.Add(down);

            // The hint, in the room's one hint language (the book's plate, the roller's own
            // fade): a prop that opens a whole screen says so before it is clicked.
            _shakerPropLabel = NewRect("ShakerPropLabel", root);
            _shakerPropLabel.anchorMin = _shakerPropLabel.anchorMax = new Vector2(0.5f, 0.5f);
            _shakerPropLabel.pivot = new Vector2(0.5f, 0f);
            _shakerPropLabel.sizeDelta = new Vector2(160f, 22f);
            var plate = _shakerPropLabel.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.Card();
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.Night[1];
            plate.raycastTarget = false;
            var line = NewText("Line", _shakerPropLabel, _display, 8, TextAnchor.MiddleCenter,
                               UITheme.Amber[4]);
            Stretch((RectTransform)line.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            line.text = UIText.T("seats.tin.label");
            line.raycastTarget = false;
            _shakerPropLabelGroup = _shakerPropLabel.gameObject.AddComponent<CanvasGroup>();
            _shakerPropLabelGroup.alpha = 0f;
            _shakerPropLabelGroup.blocksRaycasts = false;
            _shakerPropLabelGroup.interactable = false;

            _shakerProp.gameObject.SetActive(false);
            _shakerPropLabel.gameObject.SetActive(false);
        }

        /// <summary>
        /// The tin stands on the counter while a drink waits in it, and it is the door back
        /// to the bench. Driven off Core: <c>DrinkWaitingInShaker</c> is "something is in the
        /// shaker and nothing has been poured out yet", which is exactly the state the author
        /// described — in the shaker stage, not yet at the glass.
        ///
        /// It rides the counter (the cellar lifts the room), it wears whichever tin the bar
        /// owns, and it is not there while the bench itself is open — you are holding it.
        /// </summary>
        private void UpdateShakerProp()
        {
            if (_shakerProp == null) return;
            var run = Run;
            // IT DOES NOT GO AWAY WHEN THE CELLAR OPENS (2026-09-06, the author: "ana
            // sahnedeki shaker mahzen acilinca yok oluyor"). The drawer lifts the whole
            // counter and everything standing on it rides up together — the book, the
            // dishes, the coaster — so the tin standing on that coaster rides too. It was
            // hidden out of caution and the caution was wrong: a drink in progress does
            // not stop existing because you turned round to the shelves.
            bool show = run != null && run.Phase == TycoonPhase.DayOpen
                && (_flow == null || !_flow.IsOpen)
                && run.DrinkWaitingInShaker;
            // ...AND IT IS NOT ON THE MAT WHILE IT IS IN THE HAND. The carry hides the prop
            // and draws a copy under the cursor, so without this the next frame would find
            // the drink still in the shaker, decide the tin belongs on the counter and set
            // it back down there — twice on screen, and the second one clinking as it lands.
            bool have = show;
            show &= !_tinCarrying;
            if (show)
            {
                string tier = run.LadderLevel("shaker") >= 2 ? "_t2" : "";
                if (tier != _shakerPropTier)
                {
                    _shakerPropTier = tier;
                    _shakerPropImg.sprite = ItemArt.Load("shaker_prop" + tier)
                                            ?? ItemArt.Load("shaker_prop");
                }
                // Standing on the coaster, by its own lowest drawn pixel — the same reading
                // every dish on this counter is placed by (DishRestY), plus the mat's lift
                // and whatever the room is doing with the counter this frame.
                float y = DishRestY(_shakerPropImg.sprite, ShakerPropBox) + CoasterLift + CounterLift;
                _shakerProp.anchoredPosition = new Vector2(GlassHomeX, y);
                _shakerPropLabel.anchoredPosition =
                    new Vector2(GlassHomeX, y + ShakerPropBox * 0.5f + 6f);
            }
            if (show != _shakerPropShown)
            {
                _shakerPropShown = show;
                _shakerProp.gameObject.SetActive(show);
                _shakerPropLabel.gameObject.SetActive(show);
                if (show) Sfx.Play("glass_down", 0.45f);   // it is set down on the mat
                else _shakerPropHovered = false;
            }
            if (!have) { _tinPressed = false; if (_tinCarrying) EndTinCarry(false); return; }
            if (show)
            {
                float want = _shakerPropHovered ? 1f : 0f;
                _shakerPropLabelGroup.alpha = Motion.Reduced ? want : Mathf.MoveTowards(
                    _shakerPropLabelGroup.alpha, want, Time.unscaledDeltaTime / BookLabelFade);
            }
            StepTinCarry(run);
        }

        /// <summary>
        /// THE TIN, CARRIED (2026-09-06). A press that travels lifts it off the coaster; while
        /// it is up the tin itself is hidden and a copy follows the cursor, which is both how
        /// the room already carries a dirty glass and what keeps the prop's own click from
        /// firing on the way down. Let go over the basin and the build goes down the drain
        /// with the tap running after it; let go anywhere else and it is simply back on its
        /// mat, because that is where the prop is drawn from every frame anyway.
        /// </summary>
        private void StepTinCarry(TycoonRun run)
        {
            var mouse = Mouse.current;
            if (mouse == null || run == null || run.Phase != TycoonPhase.DayOpen
                || (_flow != null && _flow.IsOpen))
            {
                _tinPressed = false;
                if (_tinCarrying) EndTinCarry(false);
                return;
            }
            if (_tinPressed && !mouse.leftButton.isPressed) _tinPressed = false;
            if (_tinPressed && !_tinCarrying
                && (mouse.position.ReadValue() - _tinPressAt).magnitude > TinDragSlop)
                BeginTinCarry();
            if (!_tinCarrying) return;

            var screen = mouse.position.ReadValue();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudRoot, screen, null, out Vector2 at))
                _tinCarry.anchoredPosition = at + _tinGrabOffset;
            if (mouse.leftButton.isPressed) return;
            EndTinCarry(stage != null && (stage.PointerOverDrain(screen)
                                          || stage.PointerOverDrain(ScreenOf(_tinCarry))));
        }

        private void BeginTinCarry()
        {
            if (_tinCarry == null)
            {
                _tinCarry = NewRect("TinInHand", _hudRoot);
                _tinCarry.anchorMin = _tinCarry.anchorMax = new Vector2(0.5f, 0.5f);
                _tinCarry.pivot = new Vector2(0.5f, 0.35f);
                _tinCarry.sizeDelta = new Vector2(ShakerPropBox, ShakerPropBox);
                _tinCarryImg = _tinCarry.gameObject.AddComponent<Image>();
                _tinCarryImg.preserveAspect = true;
                _tinCarryImg.raycastTarget = false;
            }
            _tinCarryImg.sprite = _shakerPropImg.sprite;
            _tinCarryImg.color = Color.white;   // solid (2026-09-21)
            if (_sinkFadeRt == _tinCarry) { _sinkFadeRt = null; _sinkFadeImg = null; }   // picked up mid-sink
            _tinCarrying = true;
            _tinPressed = false;
            _tinCarry.gameObject.SetActive(true);
            _tinCarry.SetAsLastSibling();
            // Lifted from where it stood, by the part of it the finger pressed.
            _tinGrabOffset = Vector2.zero;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudRoot, _tinPressAt, null, out Vector2 held)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudRoot,
                    RectTransformUtility.WorldToScreenPoint(null, _shakerProp.TransformPoint(_shakerProp.rect.center)), null, out Vector2 centre))
            {
                var start = centre - new Vector2(0f, (0.5f - _tinCarry.pivot.y) * _tinCarry.rect.height);
                _tinCarry.anchoredPosition = start;
                _tinGrabOffset = start - held;
            }
            _shakerProp.gameObject.SetActive(false);
            _shakerPropLabel.gameObject.SetActive(false);
            _shakerPropHovered = false;
            Sfx.Play("tin_tip", 0.5f);
        }

        private void EndTinCarry(bool intoTheSink)
        {
            _tinCarrying = false;
            var run = Run;
            if (!intoTheSink || run == null || run.Phase != TycoonPhase.DayOpen)
            {
                if (_tinCarry != null) _tinCarry.gameObject.SetActive(false);
                return;
            }
            int fee;
            try { fee = run.PourAwayAtSink(); }
            catch (InvalidOperationException)
            {
                if (_tinCarry != null) _tinCarry.gameObject.SetActive(false);
                Toast(UIText.T("seats.sink.tap_running"));
                return;
            }
            SinkFade(_tinCarry, _tinCarryImg);
            Sfx.Play("drain", 0.9f);
            Toast(fee > 0 ? UIText.T("seats.tin.tipped_fee", ("fee", "-$" + fee)) : UIText.T("seats.tin.tipped"));
            if (fee > 0) LogService(UIText.T("seats.log.tipped_out", ("fee", "-$" + fee)));
        }

        private void UpdateDrinkGlass()
        {
            var run = Run;
            // The glass on the counter is the SERVING glass and nothing else. A drink still in
            // the shaker is a half-finished build, not something you can pick up and carry — it
            // used to appear here, which is how an unpoured drink reached a customer (2026-07-28).
            bool ready = run != null && run.Phase == TycoonPhase.DayOpen
                && (_flow == null || !_flow.IsOpen)
                && run.DrinkReady;

            if (!ready)
            {
                if (_glassShown)
                {
                    _drinkGlass.gameObject.SetActive(false);
                    _glassShown = false;
                    _glassServing = false; _glassReturning = false; _glassServeSeat = -1;
                _glassGrabbed = false;
                }
                return;
            }

            if (!_glassShown)
            {
                _glassShown = true;
                Sfx.Play("glass_down", 0.7f);
                _drinkGlass.gameObject.SetActive(true);
                _drinkGlass.anchoredPosition = GlassHome;
                _glassAngle = 0f;
                _glassServing = false; _glassReturning = false; _glassServeSeat = -1;
                _glassGrabbed = false;
            }
            // The glass shows the drink as it was actually built: the vessel it chose, its
            // blended colour and its real fill level — no fixed glass, colour or amount.
            int drinkTier = run.GlassTier(run.ServingGlassware?.Id);
            var piece = GlassArt.For(run.ServingGlassware, drinkTier);
            if (!ReferenceEquals(_drinkGlassware, run.ServingGlassware) || drinkTier != _drinkGlassTier
                || _drinkGlassArt.sprite == null)
            {
                _drinkGlassware = run.ServingGlassware;
                _drinkGlassTier = drinkTier;
                // Front face over the liquid when the set is modular; the composite
                // sprite carries a run without the generated art.
                _drinkGlassArt.sprite = piece.Front != null ? piece.Front : piece.Sprite;
                _drinkGlassBack.sprite = piece.Back;
                _drinkGlassBack.enabled = piece.Back != null;
                _drinkGlassLiquid.sprite = piece.Fill;
                // ONE SCALE FOR THE SET, so the hand carries a tumbler as a tumbler
                // (2026-09-06). The sheets are trimmed per line; scaling each to a fixed
                // height is what made every glass the same glass.
                _drinkGlass.sizeDelta = GlassArt.BoxFor(run.ServingGlassware, piece.Sprite,
                                                        CarriedGlassHeight);
                // ON THE COASTER'S FACE, BY ITS DRAWN FOOT (2026-09-14, the author: "bardak altlığının
                // üstündeki bardak görselini tam bardak altlığının ortasına oturt"): the sheet's empty
                // rows under the foot come off, and the drawing's own middle goes over the mat's.
                var footBounds = ItemArt.OpaqueBounds(piece.Sprite);
                float sheetScale = piece.Sprite != null && piece.Sprite.rect.height > 0f
                    ? _drinkGlass.sizeDelta.y / piece.Sprite.rect.height : 1f;
                _drinkGlassFootPad = footBounds.width > 0f ? footBounds.y * sheetScale : 0f;
                _drinkGlassFootDx = footBounds.width > 0f && piece.Sprite != null
                    ? (piece.Sprite.rect.width * 0.5f - (footBounds.x + footBounds.width * 0.5f)) * sheetScale : 0f;
                if (_drinkGlassLip != null)
                {
                    bool hasLip = piece.LipPlacement(_drinkGlass.sizeDelta, out var lipSize, out var lipAt);
                    _drinkGlassLip.enabled = hasLip;
                    if (hasLip)
                    {
                        _drinkGlassLip.sprite = piece.Lip;
                        _drinkGlassLip.rectTransform.sizeDelta = lipSize;
                        _drinkGlassLip.rectTransform.anchoredPosition = lipAt;
                        _drinkGlassLip.transform.SetAsLastSibling();
                    }
                }
            }
            _drinkGlassLiquid.color = DrinkColor();
            _drinkGlassLiquid.fillAmount = piece.FillAmount((float)run.ServingGlass.FillFraction);
            PlaceDrinkSurface(piece, (float)run.ServingGlass.FillFraction);
            // The finishing touches ride the carried glass too (P14): the customer is handed
            // the drink that was actually finished, salt and wedge and all.
            // The lap in progress rides in with it, so the crust on the glass grows while
            // the hand turns it (2026-09-09).
            _rimSwept.TryGetValue("salt_rim", out float sweptSalt);
            _rimSwept.TryGetValue("sugar_rim", out float sweptSugar);
            GlassDecor.Sync(_drinkGlass, piece, run.ServingGlass, run,
                            sweptSalt / RimLap, sweptSugar / RimLap, _drinkGlassRimOver,
                            _drinkGlassUnder);
            // The decor puts itself last every sync; the front crop goes back over it, and the crust
            // over that — only when the order is actually wrong, so the canvas is not re-sorted per frame.
            if (_drinkGlassLip != null && _drinkGlassLip.enabled)
                KeepLast(_drinkGlassLip.transform);
            if (_drinkGlassRimOver != null) KeepLast(_drinkGlassRimOver);

            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            var mouse = Mouse.current;

            // THE CARRY (2026-08-11). While it is held the glass springs after the cursor,
            // stiff and slightly under-damped, and leans into whichever way it is travelling
            // — the weight is the whole reason this is a drag and not a click. Letting go
            // over a customer hands it to them; letting go anywhere else sends it home,
            // which is the same slide the refusal already used.
            if (_glassGrabbed)
            {
                if (mouse == null || !mouse.leftButton.isPressed)
                {
                    _glassGrabbed = false;
                    // A PRESS THAT NEVER TRAVELLED IS A DOOR (2026-09-06): the drink standing
                    // on the coaster is the one you are building, and clicking it goes back to
                    // the bench it was poured at. Carrying it is still how it is served.
                    if (!_glassTravelled && _flow != null && !_flow.IsOpen)
                    {
                        _glassServeFrom = GlassHome;
                        _glassServeTo = GlassHome;
                        _drinkGlass.anchoredPosition = GlassHome;
                        // ...AND A PINT GOES BACK TO THE TAP (2026-09-22, the author: "Bira koyduktan sonra ana
                        // sahnede gozuken bira bardagina tiklandiginda bira koyma sahnesine tekrardan atmali,
                        // built sahnesine degil bira da istisna var"). Beer is not a cocktail and its glass was
                        // never filled at the glass bench, so the door it opens is the one it came out of.
                        if (Run != null && Run.ServingIsBeer) _flow.OpenTap();
                        else _flow.OpenServe();
                        return;
                    }
                    // THE SINK IS A PLACE YOU CARRY IT TO (2026-08-26, the author: "bardağı
                    // çöpe atmak için ana sahnede bardağı lavaboya sürüklemek gerekir").
                    // It answered a click for one round, which made throwing a drink away
                    // cheaper and easier than serving it — the wrong shape for the one verb
                    // that costs money. It is the same carry as the serve now, and it is
                    // asked FIRST: the sink is at the far end of the bar from every stool,
                    // so a drop that is over the basin was never also over a drinker.
                    if (stage != null && mouse != null
                        && (stage.PointerOverDrain(mouse.position.ReadValue())
                            || stage.PointerOverDrain(ScreenOf(_drinkGlass))))
                    {
                        OnDrainClicked();
                        if (!_glassShown) return;
                    }
                    int seat = SeatUnderPointer(mouse);
                    bool served = false, saidWhy = false;
                    if (seat >= 0)
                    {
                        try { served = ServeSeat(seat); }
                        catch (InvalidOperationException e3)
                        { Toast(UIText.Refusal(e3)); saidWhy = true; }
                    }
                    if (served)
                    {
                        _drinkGlass.gameObject.SetActive(false);
                        _glassShown = false;
                        return;
                    }
                    if (seat >= 0 && !saidWhy) Toast(UIText.T("seats.serve.read_id_first"));
                    // Home it goes, along the counter, by the road it already knows.
                    _glassServeFrom = GlassHome;
                    _glassServeTo = _drinkGlass.anchoredPosition;
                    _glassServeDur = Mathf.Min(GlassSlideMax,
                        0.08f + (_glassServeTo - _glassServeFrom).magnitude / 4200f);
                    _glassServeT = 0f;
                    _glassReturning = true;
                    return;
                }

                if (!_glassTravelled && mouse != null
                    && (mouse.position.ReadValue() - _glassPressAt).magnitude > TinDragSlop)
                    _glassTravelled = true;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)_drinkGlass.parent, mouse.position.ReadValue(), null,
                        out Vector2 want))
                {
                    want += _glassGrabOffset;
                    var before = _drinkGlass.anchoredPosition;
                    _glassVel += (want - before) * (GlassCarryStiffness * dt);
                    _glassVel *= Mathf.Exp(-GlassCarryDamping * dt);
                    _drinkGlass.anchoredPosition = before + _glassVel * dt;
                    float carry = Mathf.Clamp(-_glassVel.x * 0.012f, -16f, 16f);
                    _glassAngle = Mathf.Lerp(_glassAngle, carry, 1f - Mathf.Exp(-18f * dt));
                    _drinkGlass.localRotation = Quaternion.Euler(0, 0, _glassAngle);
                }
                return;
            }

            // THE SLIDE (2026-08-11): the glass travels the counter on its own timer; the
            // serve fires on arrival, after a re-validation, because the seat can empty
            // and the patience can run out while the glass is in flight.
            if (_glassServing || _glassReturning)
            {
                _glassServeT += dt;
                float k = _glassServeDur <= 0f ? 1f : Mathf.Clamp01(_glassServeT / _glassServeDur);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);   // lands soft
                var from = _glassServing ? _glassServeFrom : _glassServeTo;
                var to = _glassServing ? _glassServeTo : _glassServeFrom;
                var before = _drinkGlass.anchoredPosition;
                _drinkGlass.anchoredPosition = Vector2.Lerp(from, to, e);
                // lean into the travel, upright at both ends
                float lean = Mathf.Clamp((_drinkGlass.anchoredPosition.x - before.x) / dt * -0.012f, -18f, 18f);
                _glassAngle = Mathf.Lerp(_glassAngle, lean * Mathf.Sin(k * Mathf.PI), 0.5f);
                _drinkGlass.localRotation = Quaternion.Euler(0, 0, _glassAngle);
                if (k < 1f) return;

                if (_glassReturning)
                {
                    _glassReturning = false;
                    _drinkGlass.anchoredPosition = GlassHome;
                    _drinkGlass.localRotation = Quaternion.identity;
                    return;
                }
                _glassServing = false;
                int seat = _glassServeSeat;
                _glassServeSeat = -1;
                bool served = false, saidWhy = false;
                try { served = seat >= 0 && ServeSeat(seat); }
                catch (InvalidOperationException e2)
                { Toast(UIText.Refusal(e2)); saidWhy = true; }
                if (served)
                {
                    _drinkGlass.gameObject.SetActive(false);   // handed over; a new drink re-shows it
                    _glassShown = false;
                }
                else
                {
                    // Refused at the stool: the drink comes back. The player keeps it.
                    if (!saidWhy) Toast(UIText.T("seats.serve.they_left"));
                    _glassServeT = 0f;
                    _glassReturning = true;
                }
                return;
            }

            // At rest: home, upright. (The bin's hover tint lived here; the sink answers the
            // pointer with HoverGlow, like every other prop standing in the room.)
            _drinkGlass.anchoredPosition = GlassHome;
            _drinkGlass.localRotation = Quaternion.identity;
        }

        /// <summary>The carried drink's colour: its ingredients' true liquid colours, blended by
        /// share in linear space (2026-07-23) — clear spirits read pale, and a mix stays clean.</summary>
        private Color DrinkColor() => UITheme.DrinkColor(Run?.Shelf, Run?.ServingGlass);

        /// <summary>How a served customer reacts (GDD 24 §4, §10): a word for the read/serve
        /// and the payment, rising from the seat with a little pop. Green when they're pleased,
        /// red when it's the wrong drink; a gold call when they order another round.</summary>
        private System.Collections.IEnumerator ServeReaction(int seatIndex, ServiceVerdict verdict)
        {
            var seat = _seats[seatIndex].Root;
            bool wrong = verdict.Match == OrderMatch.Wrong;
            Color tone = verdict.OrdersAgain ? UITheme.Amber[3]
                : wrong ? UITheme.ViceRed[3] : UITheme.Lime[3];

            // Only the FACE answers at the serve (2026-07-31): they can see the drink, so the
            // reaction line is honest — but the bill is not on the table yet. The money and
            // the stars float up when they finish and get up (TabFloat), which is when a
            // customer actually pays.
            // The shout is the drinker's own (2026-09-07): the pop-up is the one line that
            // is allowed capitals, and it takes them here, not in the book.
            var cue = verdict.OrdersAgain ? VoiceCue.Another
                : verdict.Match == OrderMatch.Exact ? VoiceCue.Perfect
                : verdict.Match == OrderMatch.Close ? VoiceCue.Close
                : VoiceCue.Wrong;
            string line = Sentence(VoiceLine(_seats[seatIndex].Visit, cue));   // lowercase face now (2026-09-08)
            // The face for the verdict (2026-09-08): a wrong drink they HATED gets the sick
            // face rather than the raised brow, read off the same satisfaction the motes use.
            // The serve may already have chosen a rarer face (flawless first make, a
            // regular's bond); one face a verdict, so that one wins.
            var seatView = _seats[seatIndex];
            var beat = seatView.HeldEmote ?? (verdict.OrdersAgain ? EmoteBeat.Another
                : verdict.Match == OrderMatch.Exact ? EmoteBeat.Perfect
                : verdict.Match == OrderMatch.Close ? EmoteBeat.Close
                : seatView.Visit != null && seatView.Visit.Satisfaction < ReactionSour ? EmoteBeat.Awful
                : EmoteBeat.Wrong);
            seatView.HeldEmote = null;
            Emote(seatView, beat);
            if (verdict.OrdersAgain) Sfx.Play("another_round", 0.85f);

            var text = NewText("React", seat.parent, _display, 14, TextAnchor.LowerCenter, tone);
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = line;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(178, 60);
            var start = seat.anchoredPosition + new Vector2(-89f, 118f);   // centred over the seat
            // ...and it rides the counter, like everything else over the bar. Only the LIFT
            // is followed and not the seat itself: the word belongs to the stool it was said
            // at, so it must not walk out of the room with a customer who is leaving.
            float liftAtStart = CounterLift;

            const float duration = 1.35f;
            float tt = 0f;
            while (tt < duration && text != null)
            {
                tt += Time.deltaTime;
                float k = Mathf.Clamp01(tt / duration);
                // A quick pop on the way in, then a slow rise and fade.
                float pop = 1f + 0.3f * Mathf.Clamp01(1f - k * 6f) - 0.05f * k;
                rt.localScale = new Vector3(pop, pop, 1f);
                rt.anchoredPosition = start
                    + new Vector2(0, 58f * k + CounterLift - liftAtStart);
                text.color = new Color(tone.r, tone.g, tone.b, 1f - k * k);
                yield return null;
            }
            if (text != null) Destroy(text.gameObject);
        }

        // ── what they thought of it (2026-09-04) ────────────────────────────────
        //
        // The author: "müşteriler içkilerini içtikten sonra tepkilerini emoji efektleriyle
        // verecek … partikül sayısı kötüden/mükemmele göre artacak … mükemmelde 20 adet".
        // The face is the mood, the COUNT is the grade: four motes for a drink that missed,
        // twenty for one that landed. ReactionMotes does the flying.

        /// <summary>Where the mouth turns down, and where it turns up.</summary>
        private const double ReactionSour = 0.35, ReactionSweet = 0.7;

        /// <summary>How far under the crown the motes leave: they come up from BEHIND the
        /// drinker, so they start at the SHOULDERS and clear the head on the way — measured
        /// in play, where a chin-line start read as a puff off the top of the hair.</summary>
        private const float MotesBelowCrown = 74f;

        /// <summary>
        /// A PERFECT POUR IS ITS OWN RUNG (2026-09-04, the author: "eğer perfect ise kusursuz
        /// olduğunu belirtsin ... partiküller abartılsın"). The three bands below grade
        /// SATISFACTION, which a perfect pour shares with any merely good drink served
        /// promptly — so the rarest thing in the game arrived looking exactly like the
        /// common one. It gets a count no other serve can reach, gold instead of green, and
        /// an answer thrown from the player's own side of the counter.
        /// </summary>
        private const int PerfectMotes = 32, PerfectBackMotes = 20;

        /// <summary>The face, its ink and how many of them one serve is worth.</summary>
        // ── THE CROWD'S EMOJIS (2026-09-08, narrowed to ONE FAMILY 2026-09-22) ───────────────────────
        //
        // The author's thirty faces, read one by one and sorted by what each says; the game's beats get the
        // faces that fit them. Numbers are the author's file numbers (Resources/Emotes/em_<n>).
        //
        // ONE FAMILY, AND THE RULE IS THE DRAWING (2026-09-22, the author: "musterilerin arkasindan cikan
        // emojileri duzenle farkli tipte emojiler kullaniliyor kullanilabilecek emojileri sen sec"). The table
        // used to reach outside the set for the loud beats and it showed: a PILE OF POO for a bad drink, a RED
        // HORNED DEVIL for a customer storming off, a GREEN sick face, a white burst on top of a yellow one for
        // a perfect pour. Four different things flying out from behind one drinker, and the crowd stopped having
        // a single voice. What counts as usable here is one shape and one colour:
        //
        //     A YELLOW SQUARE FACE with black features. Marks ON the face are fine - a tear, a heart, spirals
        //     for eyes, a blush - because they are still that face doing something. Another body colour, another
        //     object, or anything that changes the silhouette is not.
        //
        // Nothing new had to be drawn: twenty-six of the thirty pass, which is more than the ten beats can use.
        // The four that do not (19 white burst, 65 green sick, 71 poo, 73 red devil) stay on disk - they are the
        // author's drawings and another screen may want them - they are simply not what the crowd throws. The
        // three that had no beat before (12 a single tear, 18 drooling, 37 dizzy) are on the table now.
        private enum EmoteBeat { Perfect, Flawless, Another, Close, Wrong, Awful, Patience, Storm, Kicked, Bond }

        // SIGNS, NOT FACES (2026-09-22, the author's seventh list: "gülen ağlamaklı emoji iyi bir emoji kötü
        // sonuçlarda çıkmamalı, müşteri kicklendiğinde kızgın surat çıkmalı ... emojileri sen oluştur ve sen ona göre
        // kullan ... emoji yerine kalp gibi ünlem gibi işaretler de kullanılabilir"). The yesterday's one-family table
        // still threw the laughing-until-crying face at a customer being thrown out. The crowd throws the game's own
        // SIGNS now (Tools/reaction_signs.py -> Resources/Emotes/sign_<id>): drawn by one rule on the palette, a heart
        // and a star when it went right, a question mark and a bead of sweat when it did not, a broken heart and a
        // storm cloud when it was awful, and the red face and the anger mark when they are shown the door. The two
        // faces in the set are the only faces: glad, and angry.
        private static readonly Dictionary<EmoteBeat, string[]> EmoteTable = new Dictionary<EmoteBeat, string[]>
        {
            [EmoteBeat.Perfect]  = new[] { "star", "heart", "glad" },
            [EmoteBeat.Flawless] = new[] { "star", "heart", "note" },
            [EmoteBeat.Another]  = new[] { "heart", "note", "glad" },
            [EmoteBeat.Close]    = new[] { "glad", "note" },
            [EmoteBeat.Wrong]    = new[] { "question", "drop" },
            [EmoteBeat.Awful]    = new[] { "broken", "storm" },
            [EmoteBeat.Patience] = new[] { "drop", "exclaim" },
            [EmoteBeat.Storm]    = new[] { "anger", "storm", "angry" },
            [EmoteBeat.Kicked]   = new[] { "angry", "anger" },
            [EmoteBeat.Bond]     = new[] { "heart", "star" },
        };

        /// <summary>Up to <paramref name="want"/> DIFFERENT faces for the beat, the first
        /// rolled on the run's voice stream (the seed's pick, not the frame's), the rest
        /// following it round the pool — so a burst never shows the same face twice while
        /// the pool has another.</summary>
        private Sprite[] EmotesFor(EmoteBeat beat, int want)
        {
            if (!EmoteTable.TryGetValue(beat, out var pool) || pool.Length == 0) return null;
            var run = Run;
            int first = run != null ? run.VoiceStream.NextInt(pool.Length) : 0;
            var faces = new List<Sprite>();
            for (int k = 0; k < pool.Length && faces.Count < want; k++)
            {
                var s = Resources.Load<Sprite>("Emotes/sign_" + pool[(first + k) % pool.Length]);
                if (s != null) faces.Add(s);
            }
            return faces.Count > 0 ? faces.ToArray() : null;
        }

        /// <summary>The 16px face at 1:1 on the stage (a stage unit is an art pixel; 32 drew a
        /// blurred face wider than the head — measured 2026-09-08).</summary>
        private const float EmoteUnits = 16f;

        /// <summary>How many faces a beat throws: the big moments more, the quiet ones fewer.</summary>
        private static int EmoteCount(EmoteBeat beat)
        {
            switch (beat)
            {
                case EmoteBeat.Perfect: case EmoteBeat.Flawless: case EmoteBeat.Bond: return 4;
                case EmoteBeat.Patience: case EmoteBeat.Close: return 2;
                default: return 3;
            }
        }

        /// <summary>
        /// Throws the beat's faces out from behind the drinker — the same stage motes the
        /// reactions have always used (ReactionMotes: they leave the shoulders behind the
        /// body, rise, sway and fade), only wearing the author's emojis, several different
        /// ones, at 2x (2026-09-08, the author: "emojiler eskisi gibi müşterilerin
        /// arkasından birkaç tane olsun"). The first draft stood ONE face over the head on
        /// the HUD, which read as a badge landing on them rather than them reacting.
        /// </summary>
        private void Emote(SeatView view, EmoteBeat beat)
        {
            if (stage == null || view == null || view.Body == null) return;
            if (!view.Body.gameObject.activeSelf) return;
            int count = EmoteCount(beat);
            var faces = EmotesFor(beat, count);
            if (faces == null) return;
            float headHud = view.Look != null ? view.Look.HeadTop : CharSize * 0.5f;
            float upStage = (headHud - (CharSize * 0.5f - CharFootDrop) - MotesBelowCrown) / StageToHud;
            var at = view.Body.transform.position + new Vector3(0f, upStage, 0f);
            ReactionMotes.Burst(stage, at, view.Body, false, faces, Color.white, count, EmoteUnits);
        }

        private static (string Face, Color Tint, int Count) ReactionFor(double satisfaction, bool perfect)
        {
            if (perfect) return ("good", UITheme.Amber[4], PerfectMotes);
            double s = System.Math.Max(0.0, System.Math.Min(1.0, satisfaction));
            if (s < ReactionSour)
                return ("bad", UITheme.ViceRed[3], 4 + (int)System.Math.Round(s / ReactionSour * 3));
            if (s < ReactionSweet)
                return ("fair", UITheme.Amber[3],
                    8 + (int)System.Math.Round((s - ReactionSour) / (ReactionSweet - ReactionSour) * 5));
            return ("good", UITheme.Lime[3],
                14 + (int)System.Math.Round((s - ReactionSweet) / (1.0 - ReactionSweet) * 6));
        }

        /// <summary>Throws the motes from behind one drinker.</summary>
        private void ReactionBurst(SeatView view, double satisfaction, bool follow,
            bool perfect = false)
        {
            if (stage == null || view == null || view.Body == null) return;
            if (!view.Body.gameObject.activeSelf) return;
            var (faceName, tint, count) = ReactionFor(satisfaction, perfect);
            // THE SAME SIGNS AS THE BEATS (2026-09-22): the tinted procedural faces were a second family of
            // "emoji" flying out of the same drinker. The grade is still the COUNT; the sign is the mood - a star
            // for a perfect pour, a heart for a good one, a note for a fair one, a broken heart for a bad one - in
            // its own colours, untinted.
            string sign = perfect ? "star" : faceName == "good" ? "heart" : faceName == "fair" ? "note" : "broken";
            var face = Resources.Load<Sprite>("Emotes/sign_" + sign) ?? ChromeArt.Face(faceName);
            if (face == null) return;
            tint = Color.white;
            // The body's own position is the middle of the rig canvas; the crown sits
            // HeadTop above the stool, which is CharSize/2 - CharFootDrop above that middle.
            float headHud = view.Look != null ? view.Look.HeadTop : CharSize * 0.5f;
            float upStage = (headHud - (CharSize * 0.5f - CharFootDrop) - MotesBelowCrown) / StageToHud;
            var at = view.Body.transform.position + new Vector3(0f, upStage, 0f);
            ReactionMotes.Burst(stage, at, view.Body, follow, face, tint, count);
            if (!perfect) return;
            // AND THE BAR ANSWERS. A second burst from below the counter's line, in the
            // room's magenta, so a perfect pour is a thing the two sides of the bar do
            // TOGETHER rather than a thing that happens to a customer. It is pinned to the
            // stool and never follows anybody: the player does not walk out.
            ReactionMotes.Burst(stage, at + new Vector3(0f, -MotesBelowCrown / StageToHud, 0f),
                view.Body, false, Resources.Load<Sprite>("Emotes/sign_heart") ?? face, Color.white, PerfectBackMotes);   // the bar answers in hearts
        }

        /// <summary>
        /// The bill, paid on the way out (2026-07-31): what the whole visit came to — every
        /// round of it — and the stars this customer leaves behind. Fired by the departure
        /// hook, which is the same moment Core settles the tab into the till.
        ///
        /// The stars are DRAWN, never typed (2026-08-11): the first cut set them in U+2605
        /// and U+2606, which PressStart2P does not carry, so Unity drew the missing-glyph
        /// box five times over and the author read the tofu as "black and white frames
        /// around the figures". They ride the `StarRow` ruler now, like every other star in
        /// the game — and the money sits on a whole multiple of the face's 8px design size,
        /// which is the rest of what made it soft.
        ///
        /// THREE MARKS, NOT ONE SLIP (2026-08-25). It used to be one host carrying the stars,
        /// the total and the tip stacked on each other, so they arrived together, drifted
        /// together and left together — a receipt floating off a stool. They are three now,
        /// counted out a third of a second apart, each on its own host with its own phase,
        /// its own climb and its own lean. Nothing about the movement is shared.
        ///
        /// The START is shared, on purpose. The stool is walking out from under them — a
        /// leaving drinker's rect is being lerped across the room — so all three are fired
        /// from where the stool stood when the tab settled, not from wherever it has got to
        /// by the time a mark's turn comes round.
        /// </summary>
        private void TabFloat(int seatIndex, CustomerVisit visit)
        {
            var seat = _seats[seatIndex].Root;
            var start = seat.anchoredPosition + new Vector2(0f, 96f);
            int tip = visit.Paid - visit.PaidBase;

            // THE SCORE CAME OFF THE STOOL (2026-09-04, the author: "müşterilerin verdikleri
            // ücretle beraber gözüken puanları gizlensin"). A leaving drinker threw three
            // marks — a five-star row, the money, the tip — and the first of them was a
            // GRADE: a number about a drink already drunk, printed at the one moment the
            // player can do nothing about it.
            //
            // What it said is not lost, it moved to where it is useful. The motes off their
            // shoulders carry how it went, and the note in the bubble carries what to change
            // — both while the glass is still in their hand. The bar's own standing still
            // counts every one of these stars; it is read on the night's slip, where the
            // night is what is being judged rather than the customer walking out.
            //
            // The remaining two marks keep their lanes and their stagger. Lane 0 is simply
            // empty now, which is what leaves the money climbing highest (TabLaneClimb).

            // THE MONEY, AND THE FIGURE IS THE EVENT (2026-08-25, the author: "daha
            // belirgin ve dikkat çekici"). 24 — the next legal step up, a whole 3x of the
            // face's 8px grid — and ringed the way the till's change is, so it holds its
            // shape over a lit wall or a dark one. Amber[3] and not the ramp's palest step:
            // the figure crosses a sunset window on its way up, and 0xF5C97B against that is
            // cream on cream (measured).
            StartCoroutine(TabMark(seat.parent, start, seatIndex, 1, TabStagger, host =>
            {
                var paid = NewText("Paid", host, _display, 24, TextAnchor.LowerCenter,
                    UITheme.Amber[3]);
                Place(paid.rectTransform, new Vector2(0.5f, 1), new Vector2(200, 28),
                    Vector2.zero);
                paid.rectTransform.pivot = new Vector2(0.5f, 1);
                paid.horizontalOverflow = HorizontalWrapMode.Overflow;
                paid.verticalOverflow = VerticalWrapMode.Overflow;
                paid.text = "+$" + visit.Paid;
                Ring(paid);
            }));

            // AND LAST THE TIP, which is the part worth its own colour and is short enough
            // to say what it is without a line explaining itself. It comes last because it
            // is the part that is not owed — a bar earns it after the bill is already paid.
            if (tip > 0)
                StartCoroutine(TabMark(seat.parent, start, seatIndex, 2, TabStagger * 2f,
                    host =>
                    {
                        var tipText = NewText("Tip", host, _display, 16, TextAnchor.UpperCenter,
                            UITheme.Lime[4]);
                        Place(tipText.rectTransform, new Vector2(0.5f, 1), new Vector2(200, 18),
                            Vector2.zero);
                        tipText.rectTransform.pivot = new Vector2(0.5f, 1);
                        tipText.horizontalOverflow = HorizontalWrapMode.Overflow;
                        tipText.verticalOverflow = VerticalWrapMode.Overflow;
                        tipText.text = UIText.T("seats.tab.tip", ("tip", "+$" + tip));
                        Ring(tipText);
                    }));
        }

        /// <summary>
        /// One of the three: built, held back its share of a second, then carried up off the
        /// stool on its own air.
        ///
        /// PIVOTED IN THE MIDDLE OF ITS OWN FOOT, so the lean turns it about the point it is
        /// rising from rather than swinging it about a corner off to the left.
        ///
        /// EVERY NUMBER IN THE MOVEMENT COMES FROM (seat, lane) and none of it is rolled:
        /// nothing in this game is random by accident (the determinism rule), so a wander
        /// that reproduces is one less thing that can differ between two runs of the same
        /// seed — and three marks off one stool still take three different paths, because
        /// the lane is in the phase.
        /// </summary>
        private System.Collections.IEnumerator TabMark(Transform parent, Vector2 start,
            int seatIndex, int lane, float delay, Action<RectTransform> dress)
        {
            // Counted the moment it is PROMISED and not the moment it appears: the night's
            // books wait on this (FloorIsClear), and a tip still holding its breath is money
            // the day has not finished paying.
            _tabFloats++;

            var host = NewRect(TabLaneName[lane], parent);
            host.anchorMin = host.anchorMax = new Vector2(0, 0);
            host.pivot = new Vector2(0.5f, 0f);
            host.sizeDelta = new Vector2(200, 40);
            host.anchoredPosition = start + new Vector2(TabLaneX[lane], 0f);
            var group = host.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;              // not here yet; the wait below is its cue
            dress(host);

            for (float wait = delay; wait > 0f && host != null; wait -= Time.deltaTime)
                yield return null;

            float phase = seatIndex * 1.7f + lane * 2.3f;
            float climb = TabClimb + TabLaneClimb[lane];
            float tt = 0f;
            while (tt < TabLife && host != null)
            {
                tt += Time.deltaTime;
                float k = Mathf.Clamp01(tt / TabLife);
                float rise = 1f - (1f - k) * (1f - k);        // fast off the stool, then easing
                float wander = Mathf.Sin(phase + k * Mathf.PI * 1.6f);
                host.anchoredPosition = start + new Vector2(
                    TabLaneX[lane] + TabSway * wander * k, climb * rise);
                // It leans the way it is being carried: the lean is the drift's own slope,
                // which is why it reads as one movement rather than as a spin.
                host.localRotation = Quaternion.Euler(0, 0,
                    -TabLean * Mathf.Cos(phase + k * Mathf.PI * 1.6f) * k);
                // A pop out of the stool, then a slow settle — the punch is what makes it
                // arrive rather than appear.
                float pop = 1f + 0.35f * Mathf.Clamp01(1f - k * 9f) - 0.06f * k;
                host.localScale = new Vector3(pop, pop, 1f);
                // It holds its ink for two thirds of the life and only then goes. The old
                // curve (1 - k²) was already fading on the first frame, which is most of
                // why it read as "gone at once".
                group.alpha = k < 0.62f ? 1f : 1f - (k - 0.62f) / 0.38f;
                yield return null;
            }
            _tabFloats--;
            if (host != null) Destroy(host.gameObject);
        }

        /// <summary>
        /// A dark halo round a label, so a figure thrown over the room keeps its shape on
        /// any wall it crosses — and the till's own sunset window is the wall it crosses.
        ///
        /// TWO BLACKS, NOT THE TILL'S WHITE-THEN-BLACK (measured, 2026-08-25). The white
        /// ring works on the register, which stands in shadow. Over the window it does not:
        /// `Outline` draws offset copies BEHIND the glyph, and a pixel face at 24 is a
        /// three-unit stroke with a unit of anti-aliasing down each side — so the white
        /// shows straight through the soft edges and a gold "+$4" comes out cream. Two dark
        /// rings at different distances give the same read on a dark wall and leave the
        /// gold gold.
        /// </summary>
        private static void Ring(Text label)
        {
            var near = label.gameObject.AddComponent<Outline>();
            near.effectColor = new Color(0f, 0f, 0f, 0.92f);
            near.effectDistance = new Vector2(2f, -2f);
            var far = label.gameObject.AddComponent<Outline>();
            far.effectColor = new Color(0f, 0f, 0f, 0.62f);
            far.effectDistance = new Vector2(3.5f, -3.5f);
        }

        /// <summary>
        /// WHAT THE BAR IS PLAYING (2026-09-15, the author: "arkaplanda biraz daha 80ler elektronik jazz olmalı rahatlatıcı
        /// bir oyun olmalı"): the night while it is open, its late mood over the last stretch of the shift and past closing,
        /// the story's own while the last customer is on their stool, the books after, and a closed bar's. Sfx.Music turns
        /// a mood into tracks.
        /// </summary>
        private static string MusicMood(TycoonRun run) =>
            run.Phase == TycoonPhase.Closed ? "closed"
            : run.Phase == TycoonPhase.DayEnd ? "dayend"
            : run.LastCustomer != null ? "story"
            : run.Floor != null && run.Floor.NightFraction >= LateNight ? "lastcall"
            : "night";

        /// <summary>The share of the shift after which the music turns late.</summary>
        private const double LateNight = 0.85;

        private void RefreshSeats()
        {
            var run = Run;
            var seated = run.Floor.Seated;

            // A patron whose patience ran out storms off (GDD 24 §4). IT IS SAID ON THE
            // STOOL, NOT IN A BANNER (2026-09-04, the author: "'A customer stormed off'
            // yazısı kalkacak"). A red line across the top of the screen was the bar
            // telling you about its own room; the walk-out is now what everyone else
            // gives back — a handful of sour motes over the stool they got up from, thrown
            // by the departure branch below, where the seat that did it is known.

            // The licence is only good while its holder is at the bar.
            if (_idVisit != null && (_idVisit.State != VisitState.Waiting || !seated.Contains(_idVisit)))
                CloseId();

            bool drinkReady = run.Phase == TycoonPhase.DayOpen && run.DrinkReady &&
                (_flow == null || !_flow.IsOpen);

            // Stools are stable (2026-07-22): a customer keeps their seat until they leave, so
            // busts never shift or morph when the queue compacts. Reconcile the positional
            // Seated list against the fixed stools each frame.
            // 1) Departures — a stool whose patron is no longer seated starts a leave animation.
            for (int i = 0; i < _seats.Count; i++)
            {
                var v = _seats[i];
                if (v.Visit != null && !v.Exiting && !seated.Contains(v.Visit))
                {
                    v.Exiting = true;
                    v.ExitT = 0f;
                    // THE STORY'S GUEST LEAVES A LINE, NOT A SCENE (GDD 26 §3). Their clock
                    // running out is a beat that did not land, not a customer storming out of
                    // a bad bar — they walk, they do not slam, and the night's log does not
                    // book them as a walk-out because they were never on its books at all.
                    bool kicked = v.Visit.State == VisitState.Kicked;
                    v.ExitStorm = !v.Visit.OnTheHouse
                        && (v.Visit.State == VisitState.StormedOff || kicked);
                    // WHAT THEY THOUGHT, AT THE BOTTOM OF THE GLASS (2026-09-04, the author:
                    // "verilen emoji tepkileri içkiyi bitirdikten sonra verilmeli"). It used to
                    // be thrown a sip after the serve, which is a verdict on a drink they had
                    // barely tasted; it belongs here, where they set the empty down and get up.
                    // A walk-out gets the same sentence in the same language — the worst face
                    // there is, as few as they come. Both are pinned to the stool rather than
                    // following them out: a cloud chasing a leaver reads as a comet. The
                    // guest of the house is left out, as they are left out of every other
                    // ledger (GDD 26 §3).
                    // A PERFECT POUR IS REMEMBERED PAST THE SIP that earned it: the note
                    // taken at the serve is still on the seat when they set the glass down,
                    // and it is the only thing here that knows the pour was exact. A
                    // storm-off never reaches it — there was no glass.
                    // Shown the door (GDD 28 §8): no verdict on a drink they never had, no
                    // cheer, no slump — the log says why, and the walk out is the storm-off's.
                    if (!v.Visit.OnTheHouse && !kicked)
                        ReactionBurst(v, v.ExitStorm ? 0.0 : v.Visit.Satisfaction, follow: false,
                            perfect: !v.ExitStorm && v.Note.Flawless);
                    // The face on the way out (2026-09-08): rage for a storm-off, tears for
                    // the door. A served drinker leaving calm already said their piece.
                    if (!v.Visit.OnTheHouse && (kicked || v.ExitStorm))
                        Emote(v, kicked ? EmoteBeat.Kicked : EmoteBeat.Storm);
                    // ...AND A WORD ON THE WAY OUT (2026-09-07): a walk-out and a kick each get
                    // one line in the drinker's voice, said from the stool as they get up. The
                    // guest of the house leaves in silence here — her lines are the story's.
                    if (v.ExitStorm && !v.Visit.OnTheHouse)
                    {
                        v.SayLines = null;
                        v.SaidLines = 0;
                        SayIt(v, VoiceLine(v.Visit, kicked ? VoiceCue.Kicked : VoiceCue.Leaving));
                    }
                    if (v.Visit.OnTheHouse) { }
                    else if (kicked)
                        LogService(v.Visit.OffTheBooks
                            ? UIText.T("seats.log.shown_door", ("reason", KickReason(v.Visit)))
                            : UIText.T("seats.log.shown_door_stars", ("reason", KickReason(v.Visit)),
                                       ("stars", LogStars(0))));
                    else if (v.ExitStorm)
                        LogService(UIText.T("seats.log.storm_off",
                            ("drink", v.Visit.IdInspected
                                ? UIText.Caps(UIText.Data("recipe", v.Visit.Order.Wanted.Id, "name", v.Visit.Order.Wanted.Name))
                                : "?"),
                            ("paid", "$0"), ("stars", LogStars(0))));
                    else if (v.Visit.Paid > 0)
                        LogService(UIText.T("seats.log.tab",
                            ("paid", "$" + v.Visit.Paid), ("stars", LogStars(v.Visit.Satisfaction))));
                    // The bussing beat (D2): a drinker leaves the empty glass on this stool.
                    // Core left the mess in the same tick that freed the seat (GDD 27 §4.1 —
                    // the SERVE is the signal, so an unmatched pour's glass is claimed too);
                    // this view claims the first one no other stool has claimed.
                    if (v.Visit.DrinkServed)
                        foreach (var g in run.Floor.Messes)
                        {
                            bool claimed = false;
                            foreach (var other in _seats)
                            {
                                if (other.Dirty == g) { claimed = true; break; }
                                foreach (var mk in other.Marks) if (mk.Mess == g) { claimed = true; break; }
                                if (claimed) break;
                            }
                            if (claimed) continue;
                            // The glass is this stool's; so is every mark the same drinker
                            // left, scattered around where they were sitting. The scatter is
                            // hashed off the stool and the mark's number, so a mark does not
                            // jump about between frames or between one visit and the next.
                            if (g.HasGlass) { if (v.Dirty == null) { v.Dirty = g; v.DirtyX = HeadX(v); } }
                            else if (v.Marks.Count < 3)
                            {
                                int n = v.Marks.Count;
                                int seed = v.Index * 7 + n * 13;
                                v.Marks.Add(new Mark
                                {
                                    Mess = g,
                                    Seed = seed,
                                    Dx = ((seed * 37 % 61) - 30) * 1.8f + 4f + HeadX(v),
                                    Dy = ((seed * 53 % 17) - 8) * 1.1f,
                                });
                            }
                        }
                    // The tab settles as they go: what they paid and the stars they leave
                    // behind float over the emptying stool. The serve only earned the face.
                    if (v.Visit.Paid > 0) TabFloat(i, v.Visit);
                    if (v.Visit.Paid > 0) Sfx.Play("cash");
                    if (!kicked)
                        Sfx.Play(!v.ExitStorm && v.Visit.Satisfaction >= 0.55 ? "cheer_sfx" : "upset_sfx", 0.6f);
                    // And the body answers before it leaves (P15/D5): a cheer or a slump on
                    // the stool. This is where the emotional tell lives now the stat rows
                    // left the card — skipped cleanly while the clips have no frames yet.
                    v.ReactClip = !v.ExitStorm && v.Visit.Satisfaction >= 0.55
                        ? PatronClip.Cheer : PatronClip.Upset;
                    var reactLook = v.Look ?? (_looks.Count > 0 ? _looks[0] : null);
                    v.ReactLeft = !kicked && reactLook != null
                        && reactLook.Clips.TryGetValue(v.ReactClip, out var rf) && rf.Length > 0
                        ? ReactSeconds : 0f;
                    // …then up off the stool and turned to the door (LEAVE, 2026-09-15), kicked
                    // or not: a kicked customer still has to stand up before walking out.
                    v.LeaveSeconds = v.LeaveLeft = OneShotSeconds(v, PatronClip.Leave);
                }
            }
            // 1b) THE GUEST WEARS THE FACE THE BEAT NAMES, whatever order the frame ran in.
            //
            // Measured, 2026-08-13: the story's guest kept turning up in a stranger's body
            // while the plate showed the right person — the stool had been given a rolled
            // look, and a stool KEEPS its look by design (a face that changes under the
            // player is worse than a wrong one). Rather than chase which frame won the race,
            // the written face is simply reasserted here, once a frame, idempotently: for
            // this one visit the beat is the authority, not the seat.
            var houseGuest = run.LastCustomer;
            if (houseGuest != null)
            {
                var written = LookForStory(run.LastCallBeat?.Who);
                if (written != null)
                    foreach (var v in _seats)
                        if (v.Visit == houseGuest && v.Look != written)
                        {
                            v.Look = written;
                            v.Tag.anchoredPosition = new Vector2(written.HeadX, written.HeadTop + TagLift);
                            if (v.Gauge != null)
                                v.Gauge.anchoredPosition = new Vector2(written.HeadX, written.HeadTop + 6f);
                        }
            }

            // 2) Arrivals — a seated customer with no stool takes the first free one and walks in.
            foreach (var visit in seated)
            {
                bool assigned = false;
                for (int i = 0; i < _seats.Count; i++) if (_seats[i].Visit == visit) { assigned = true; break; }
                if (assigned) continue;
                // THE GUEST SITS WHERE THEY CAN BE TALKED TO (GDD 26 §3): the stool nearest
                // the till, which is the end of the row the bar is worked from. Everyone else
                // takes the first free stool, as they always have — "first" meaning first in
                // the order the ROOM fills (SeatFillOrder), which is no longer the same thing
                // as first along the counter.
                var order = SeatOrderFor(run);
                int owned = Math.Min(run.Seats, order.Length);
                bool nearTheTill = visit.OnTheHouse;
                for (int n = 0; n < owned; n++)
                {
                    int i = nearTheTill ? TillEndward(order, owned, n) : order[n];
                    // A run can own more stools than the HUD has built (seen 2026-09-08 with
                    // a run started against a bar whose seats were not up): the room's
                    // order is the run's, the views are the HUD's, and the two are bounded
                    // separately.
                    if (i < 0 || i >= _seats.Count) break;
                    var v = _seats[i];
                    if (v.Visit == null && !v.Exiting)
                    {
                        v.Visit = visit;
                        v.WalkT = 0f;
                        v.ArriveLeft = v.LeaveLeft = 0f;
                        v.AnimClock = SeatWalkClock(v);
                        v.Nagged = false;
                        v.Note = default;      // the last drinker's line is not this one's
                        HushSeat(v);           // …and neither is what they said about it
                        // Who walked in, and how tall they are. The ticket and the gauge
                        // hang off THEIR head: the cast runs from 135 to 166 pixels of
                        // figure, which is 60 HUD units of difference, and a fixed window
                        // would leave the short ones with their paperwork floating.
                        v.Look = LookFor(visit);
                        if (v.Look != null)
                        {
                            v.Tag.anchoredPosition = new Vector2(v.Look.HeadX, v.Look.HeadTop + TagLift);
                            if (v.Gauge != null)
                                v.Gauge.anchoredPosition = new Vector2(v.Look.HeadX, v.Look.HeadTop + 6f);
                        }
                        v.Root.gameObject.SetActive(true);
                        Sfx.Play("door", 0.5f);   // someone through the door (P17)
                        break;
                    }
                }
            }

            RefreshDirtyGlasses(run);
            // The bar bed (P17): always on, muffled while a stage or the licence is open — and, since 2026-09-15, the
            // music over it, which follows the night (MusicMood). The mood goes first: the bed is chosen by it.
            bool attention = (_flow != null && _flow.IsOpen) || (_idRoot != null && _idRoot.gameObject.activeSelf);
            Sfx.Music(MusicMood(run), attention);
            Sfx.Ambience(ducked: attention);

            // 3) Render each stool from its assigned patron.
            for (int i = 0; i < _seats.Count; i++)
            {
                var view = _seats[i];

                if (view.Exiting)
                {
                    // The ticket comes down the moment they get up (2026-08-19, the author:
                    // "içtikten sonra baloncuk kalkabilir") — a leaving customer is done
                    // talking, and a balloon walking out with them reads as unfinished
                    // business. The patience bar goes with it (2026-08-20): their clock
                    // stopped when they left the stool, and a gauge crossing the room is a
                    // countdown on somebody who is no longer waiting for anything.
                    // The parting line rides them all the way out (2026-09-07, the author:
                    // "musteriler giderken soyledikleri kafalarinin ustunde devam etmeli
                    // sahnenin sonuna kadar"). It used to come down on the balloon's own
                    // four-second clock while its drinker was still crossing the room, so the
                    // last thing they said vanished over a walking figure. The balloon is a
                    // child of the seat, so it travels with them; AdvanceExit takes it down
                    // when they are off the screen.
                    if (view.Tag.gameObject.activeSelf) view.Tag.gameObject.SetActive(false);
                    if (view.Gauge != null && view.Gauge.gameObject.activeSelf)
                        view.Gauge.gameObject.SetActive(false);
                    AdvanceExit(view);
                    continue;
                }

                if (view.Visit == null)
                {
                    if (view.Root.gameObject.activeSelf) view.Root.gameObject.SetActive(false);
                    SyncPatronBody(view);
                    continue;
                }

                AdvanceWalkIn(view);

                var visit = view.Visit;
                bool deciding = !visit.HasOrdered;                    // reading the menu (2026-07-23)
                bool drinking = visit.State == VisitState.Drinking;   // served, nursing the drink

                // The bubble only knows what the PLAYER knows (v5 C3): until the ID card has
                // been read, Core refuses to hand the order over at all. Stripped to three
                // beats (the author, 2026-08-02): it does not exist until they SIT and have
                // an order to give; unread it says only that they are ready — not who they
                // are, not what they want; read, it says only the name and the order.
                bool known = visit.IdInspected;
                bool atTheStool = view.WalkT >= 1f;
                // THE BUBBLE IS UP THE WHOLE TIME THEY ARE ON THE STOOL (2026-08-19). It used
                // to be hidden while they read the menu, so a customer deciding and a customer
                // who had not arrived yet looked exactly alike — the player had nothing to
                // wait ON. It says "..." instead, which is a customer visibly thinking.
                // …AND DOWN WHILE THE CELLAR IS (2026-08-22, the author: "Backbar
                // açıldığında müşterilerin kafasının üstündeki barlar gitmeli"). The drinkers
                // ride the room up with the drawer, and their tickets and clocks ride with
                // them — straight into the shelves you are trying to read. Nothing is lost by
                // taking them down: the cellar is a place you are looking AWAY from the room
                // to work in, and the clocks are still running underneath.
                // THE BALLOON'S OWN CLOCK, read before the ticket's, because the ticket
                // stands down while somebody is talking.
                bool saying = view.Say != null && view.Say.gameObject.activeSelf;
                StepSips(view, visit);
                bool timed = view.SayLines == null || view.SayLines.Count == 0;
                if (saying && timed && Time.unscaledTime >= view.SayUntil) { HushSeat(view); saying = false; }
                // THE CELLAR NO LONGER HUSHES THEM (2026-09-08, the author: "mahzen açıkken
                // konuşma balonları ... küçülecek ve tezgah ile üst bar arasında
                // konumlandırılacak"). They were taken down outright from 2026-08-22 because
                // they rode the room up into the shelves; SeparateSays shrinks them into the
                // band under the top bar instead, so the line can be finished while you are
                // turned round to the stock.
                if (saying && !atTheStool) { HushSeat(view); saying = false; }

                // ONE THING OVER ONE HEAD (2026-09-04, the author: "kafalarının üstündeki
                // kutucuk yerine konuşma baloncukları gözükmeli"). The ticket is a standing
                // readout of an OPEN order; speech is what happens once the drink is in
                // their hand. So the ticket goes down while a line is being said — and stays
                // down for the rest of the savour, because a customer who has been served
                // has no order left to read and a plate over them saying so was the loading
                // sign this beat replaced.
                //
                // An extra round brings it straight back: Core never puts those visits into
                // Drinking (CustomerVisit.Resolve refreshes the order and stays Waiting), so
                // the moment the balloon retires the ticket is up again with the new drink
                // on it — which is the author's "ikinci siparişte tekrardan görüntüleyebilmeliyiz".
                bool showBubble = atTheStool && !CellarOpen && !saying && !drinking;
                if (view.Tag.gameObject.activeSelf != showBubble)
                {
                    if (showBubble) RaiseBubble(view.Tag);
                    else view.Tag.gameObject.SetActive(false);
                }

                if (showBubble)
                {
                    // A regular ordering again after a perfect serve gets a gold star and the
                    // round count (GDD 24 §4) — the reward for reading them right, made
                    // visible. The name is part of what the card teaches: it waits for the read.
                    // "x3", not a star from the font: no pixel face here carries one, so it
                    // arrived as a fallback glyph at the wrong weight beside a name set in ours.
                    string star = visit.ExtraOrdersTaken > 0
                        ? $"<color=#8F5A1E>x{visit.ExtraOrdersTaken + 1} </color>" : "";
                    view.Name.supportRichText = true;
                    // NO NAME OVER THE HEAD (2026-09-13, the author: "müşterilerin ismi kafa
                    // hizasında olmasın, talepleri biraz daha ön plana çıksın"). The name is on
                    // the licence and in the book; over the head the ticket says the drink, and
                    // the round count rides in front of it.
                    view.Name.text = "";
                    view.Order.supportRichText = true;

                    // THE ORDER ARRIVES AS SPEECH (2026-08-19, the author: "yazılar konuşma
                    // metni gibi harf harf gelecek"). The clock starts on the EDGE of the
                    // licence being read, not on the frame it is read in, so the ticket cannot
                    // restart its sentence every time the pointer moves. Reduced motion is
                    // handed the whole line at once — a typewriter is exactly the sort of
                    // thing that setting exists to switch off.
                    if (known && !view.WasKnown)
                    {
                        view.WasKnown = true;
                        view.SpeakFrom = Time.unscaledTime;
                    }
                    string wanted = known
                        ? UIText.Data("recipe", visit.Order.Wanted.Id, "name", visit.Order.Wanted.Name) : "";
                    int said = Motion.Reduced ? wanted.Length
                        : Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - view.SpeakFrom) * SpeakCps),
                                      0, wanted.Length);
                    view.Spoken = said >= wanted.Length;
                    // (THE ORDER'S BALLOON IS GONE, 2026-09-07, the author: "müşteriler
                    // içkiyi içmeden önce sipariş verirken konuşma balonları çıkıyor,
                    // çıkmamalı; kokteyli teslim aldıktan sonra her yudumlarında çıkmalı".
                    // It said the drink a second time — the ticket over their head is
                    // already typing it out letter by letter — and it put a balloon up
                    // during the one stretch the player is reading tickets and pouring.
                    // The balloon belongs to the DRINK now and to nothing else: one line a
                    // sip, over a glass already handed across. The voice's Order lines stay
                    // in voices.json; VoiceCue.Order is simply not asked for on the floor.)

                    if (drinking)
                    {
                        // Served, mid-animation, off-limits. The ticket barely gets to say
                        // this any more — a drinker's plate stands down for the whole savour
                        // (see showTag) — but the branch stays honest for the frames between
                        // a serve and the balloon coming up.
                        view.Wants.text = UIText.T("seats.ticket.drinking") + (Motion.Reduced ? "..."
                            : new string('.', 1 + Mathf.FloorToInt(Time.unscaledTime / DotBeat) % 3));
                        view.Wants.color = UITheme.ClubBlue[1];
                        view.Order.text = "";
                        view.Spoken = true;
                    }
                    else if (deciding)
                    {
                        // Reading the menu. One, two, three dots and round again — the beat is
                        // the only thing on the ticket, so the ticket is the size of it.
                        view.Wants.text = Motion.Reduced ? "..."
                            : new string('.', 1 + Mathf.FloorToInt(Time.unscaledTime / DotBeat) % 3);
                        view.Wants.color = UITheme.Magenta[1];
                        view.Order.text = "";
                        view.Spoken = false;
                    }
                    else if (!known)
                    {
                        // Ready, unread: the one line the author asked for, and nothing else.
                        view.Wants.text = UIText.T("seats.ticket.ready");
                        view.Wants.color = UITheme.Magenta[1];
                        view.Order.text = "";
                    }
                    else if (visit.OnTheHouse)
                    {
                        // THE STORY'S GUEST NAMES ONE DRINK AT A TIME, and the post-it is
                        // where it is named (GDD 26 §4). Their licence is open from the
                        // moment they sit — they introduced themselves — so the ticket would
                        // otherwise print the ask over their head and hand the player the
                        // whole trial in advance, which is the one thing the reveal is for.
                        view.Wants.text = UIText.T("seats.ticket.talk");
                        view.Wants.color = UITheme.Magenta[1];
                        view.Order.text = "";
                        view.Spoken = false;
                    }
                    else
                    {
                        // Read: the name above, the order below — the card said the rest.
                        view.Wants.text = "";
                        view.Order.text = star + wanted.Substring(0, said);
                    }

                    // THE ICON ROW comes up only once the order has finished being SAID. The
                    // pictures are the fastest thing on the ticket to read, so showing them
                    // while the letters are still arriving would answer the question before
                    // the sentence asks it — and the typing would be decoration.
                    float iconW = LayOutOrderIcons(view, visit,
                        known && view.Spoken && !deciding && !drinking);

                    // The ticket FITS its lines and its WIDEST line (the author, 2026-08-02:
                    // "yazı hiçbir zaman taşmamalı"). SEX ON THE BEACH ran off both ends of
                    // a fixed card. The card takes the width of the longest thing it says,
                    // up to a cap; past the cap the order wraps to a second row and the card
                    // grows downward instead. Nothing is ever clipped, and nothing floats in
                    // an empty box.
                    //
                    // BOTH AXES ANSWER THE CONTENT NOW (2026-08-19). The height used to be
                    // the rows of TYPE only, so the icon row hung off the bottom of the plate;
                    // and the width had a 156 floor, which drew a poster round three dots.
                    float widest = Mathf.Max(view.Name.preferredWidth,
                        Mathf.Max(view.Wants.preferredWidth,
                            Mathf.Max(view.Order.preferredWidth, iconW)));
                    float cardW = Mathf.Clamp(widest + TagPad * 2f, TagMinW, TagMaxW);
                    float textW = cardW - TagPad * 2f;

                    // The order is the line that runs long, so it is the one allowed to wrap.
                    // A unit of slack (2026-09-13): the card is sized to the order's own width, so a
                    // one-line order measured a hair over textW and counted as two — an empty row
                    // between the drink and its icons (measured in play with the order at 16 bold).
                    int orderLines = view.Order.text.Length == 0 ? 0
                        : Mathf.Max(1, Mathf.CeilToInt((view.Order.preferredWidth - 1f) / Mathf.Max(1f, textW)));
                    view.Order.horizontalOverflow = orderLines > 1
                        ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;


                    // EACH ROW IS AS TALL AS ITS OWN FONT SAYS (2026-08-19). One constant used
                    // to stand in for three different line boxes, so the plate was always a
                    // few units taller than what it held and the type rode high in it.
                    float rowTop = -TagPad;
                    view.Name.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Name.text.Length > 0) rowTop -= view.NameLineH;
                    view.Wants.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Wants.text.Length > 0) rowTop -= view.WantsLineH;
                    view.Order.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    rowTop -= view.OrderLineH * orderLines;
                    if (view.IconRow != null && view.IconRow.gameObject.activeSelf)
                    {
                        view.IconRow.anchoredPosition = new Vector2(0, rowTop);
                        rowTop -= IconRowH;
                    }
                    // The bottom pays the foot row as well as the padding, so what the type is
                    // centred in is the WHITE FIELD and not the sprite: the plate's top edge
                    // is two units of colour and its bottom is two plus the foot's one.
                    view.Tag.sizeDelta = new Vector2(cardW, -rowTop + TagPad + TagFoot);
                }

                // (The drink icon used to dock against the order text's measured width, on
                // whatever row the order landed on. It lives on its own row now, beside the
                // serving spec, and LayOutOrderIcons places the whole row.)

                // ONE clock again (the author, 2026-09-04: "sipariş almak barı 0lamaz +1 kutu
                // daha ekler"). It was split in two on 2026-08-02 and taking the order started
                // the second from full, so the gauge visibly refilled to the brim and said the
                // wait had not begun. The bar runs from the moment they have decided to the
                // moment the drink lands; walking over to ask pays one of its three boxes back
                // into it, capped at full. Core says which THIRD they are in, so the colour
                // over the head is the same reading the till pays by, and the bubble — not a
                // hue — says which wait it is.
                float patience = (deciding || drinking) ? 1f : (float)visit.PatienceFraction;
                // ONE warning face a visit (2026-09-08): when a third of the patience is left,
                // the sweat drop comes up over the head. Once — a face every frame is noise.
                if (!view.Nagged && !deciding && !drinking && patience < 0.34f)
                {
                    view.Nagged = true;
                    Emote(view, EmoteBeat.Patience);
                }
                // THE BAR IS ONLY UP WHILE IT IS EMPTYING (2026-08-20, the author: "herhangi
                // bir sabır barı azalmıyorken kafasının üstünde bar gözükmesin ... içki
                // içerken odadan çıkarken vs"). It used to stand over every seated customer
                // and simply hold FULL through the beats where no clock runs — thinking,
                // drinking, walking out — which is a gauge that means nothing three times a
                // visit, and a room of them says the night is under pressure when it is not.
                //
                // The condition is Core's own, not a list of screens: patience ticks in
                // CustomerVisit.Tick only while the visit is WAITING, is not held, and has
                // finished deciding. Anything else — a mind being made up, a drink being
                // nursed, a guest who is being talked to (GDD 26 §4 keeps their clock on the
                // POST-IT anyway), somebody already off the stool — has no clock to draw.
                bool clockRunning = !visit.OnTheHouse && !visit.ClockHeld
                    && !deciding && !drinking && visit.State == VisitState.Waiting
                    && !CellarOpen;          // see the bubble, above
                if (view.Gauge != null && view.Gauge.gameObject.activeSelf != clockRunning)
                    view.Gauge.gameObject.SetActive(clockRunning);
                var band = visit.Band;
                var lit = band == ServiceBand.Green ? UITheme.Lime[3]
                    : band == ServiceBand.Amber ? UITheme.Amber[3] : UITheme.ViceRed[3];
                // The last third breathes. Nothing else on the head moves, so a bar that is
                // about to lose somebody is visible from across the screen without a word.
                float pulse = band == ServiceBand.Red && !Motion.Reduced
                    ? 0.74f + 0.26f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4.2f))
                    : 1f;
                view.PatienceFill.fillAmount = patience;
                view.PatienceFill.color = new Color(lit.r, lit.g, lit.b, pulse);
                if (view.PatienceNeon != null)
                    view.PatienceNeon.color = new Color(lit.r, lit.g, lit.b, 0.42f * pulse);

                // Drive the animated customer (2026-07-23): walk-in, the sit-and-breathe idle,
                // a one-shot "placing the order" beat, then nursing the drink. Facing and frame
                // are chosen from the visit state; the body below the waist is clipped by the bar.
                UpdateSeatAnimation(view, visit);

                // The tag lights when a drink is built and this customer can actually take
                // it — and "can take" includes the READ: only taken orders are click-servable,
                // so the lit set and the clickable set are one set.
                //
                // It used to be a TINT over a flat rectangle, and a tint is no use to a
                // drawing: multiplying a white plate by cyan drags the magenta edge to a
                // muddy teal and the whole balloon changes colour to say one thing. The lit
                // ticket is a second SPRITE instead — the same 11x11 geometry with its edge
                // walked onto the information ramp, so only the edge moves and the plate is
                // still recognisably the same object (16 §5: light says state).
                bool canTake = drinkReady && !deciding && !drinking && visit.IdInspected;
                // A THIRD tone joined the two (2026-08-19, the author: "içecek içiyorsa pembe
                // rengi vice mavisi olsun"): while they drink, the edge walks onto the club's
                // blue — the customer is mid-animation and cannot be interacted with, and the
                // plate says so the same way the dots on it do.
                var tone = drinking ? ChromeArt.BubbleTone.Drink
                    : canTake ? ChromeArt.BubbleTone.Take
                    : ChromeArt.BubbleTone.Order;
                // The ticket keeps the author's one balloon whatever its state (2026-09-08);
                // the tone survives in the rule's colour below.
                view.TagBg.sprite = ChromeArt.SpeechBox(tone);
                if (view.Tail != null) view.Tail.sprite = ChromeArt.SpeechTail(tone);
                if (view.IconRule != null)
                    view.IconRule.color = canTake ? UITheme.Cyan[0] : UITheme.Magenta[1];
            }
        }

        /// <summary>Walks a newly-seated customer in from the right along the counter and fades
        /// them up (2026-07-23): the west-facing walk cycle carries them left into their stool.</summary>
        private void AdvanceWalkIn(SeatView view)
        {
            // In from the screen's real right edge (not a few frames off the stool) at a steady
            // pace, so a far stool is a longer walk (2026-07-23).
            float entryX = _hudRoot.rect.width + OffscreenMargin;
            if (view.WalkT < 1f)
            {
                float dist = Mathf.Max(1f, entryX - view.SeatX);
                // A STEADY WALK, THEN THE ARRIVAL (2026-09-15). The slow-down that lived here
                // (2026-08-19) played the last steps at a third of the pace, which read as slow
                // motion; the ARRIVE clip does the stopping now, so the floor and the cycle run
                // at one pace all the way to the stool. The walk's clock was phased when the
                // stool was given (SeatWalkClock), so the step that lands here is the cycle's
                // first frame - the pose ARRIVE was drawn from.
                bool stillWalking = view.WalkT < 1f;
                view.WalkT = Mathf.Min(1f, view.WalkT + AnimDelta * WalkSpeed / dist);
                if (stillWalking && view.WalkT >= 1f)
                {
                    Sfx.Play("stool_take", 0.7f);
                    view.ArriveSeconds = view.ArriveLeft = OneShotSeconds(view, PatronClip.Arrive);
                }
                view.Root.anchoredPosition =
                    new Vector2(Mathf.Lerp(entryX, view.SeatX, view.WalkT), SeatLineY);
                // NO FADE (2026-09-07, the author: "musterilerin sahneye girisi ve cikisinda
                // fade olmamali, tam olarak sahnenin sonuna normal hizlarinda yuruyup
                // ekrandan disari cikmalilar"). They used to fade up over the first quarter
                // of the walk, which reads as a person materialising in the middle of the
                // room rather than coming in through a door. They walk in solid; entryX is
                // already off the frame, so there is nothing to hide.
                view.Group.alpha = 1f;
            }
            else
            {
                view.Root.anchoredPosition = new Vector2(view.SeatX, SeatLineY);
                view.Group.alpha = 1f;
            }
        }

        /// <summary>Plays a customer leaving (2026-07-23): they get up and walk back out to
        /// the right the way they came — and since 2026-08-19 it IS the way they came (the
        /// author: "çıkış animasyonu giriş animasyonu ile aynı hızda aynı şekilde"): the
        /// entrance mirrored, same WalkSpeed, same steady cycle (the near-stool ease is gone,
        /// 2026-09-15: the reaction beat, then LEAVE, then the walk). One pace for
        /// everybody — the storm-off's shake and its 1.5× hurry are gone; anger is carried by
        /// the Upset reaction beat and the toast, not by the walk.</summary>
        private void AdvanceExit(SeatView view)
        {
            // The reaction beat first: they stay on the stool and the drink answers — a fist
            // up or a slow head-shake — before they get up. One shot, then the walk.
            if (view.ReactLeft > 0f)
            {
                view.ReactLeft -= RoomDelta;
                // ON THE STOOL means on the stool AS IT IS RIGHT NOW (2026-08-25, the author:
                // "müşteriler tepki animasyonu verirlerse tezgah açılıp kapandığında havada
                // asılı kalıyorlar"). This branch used to return without touching the rect,
                // which is fine for a room that is standing still and is a person left
                // hanging in mid-air the moment the cellar lifts the one they are leaning on.
                // Every other beat — walking in, walking out, sitting — re-reads SeatLineY
                // each frame; this is simply the one that did not.
                view.Root.anchoredPosition = new Vector2(view.SeatX, SeatLineY);
                UpdatePatronFrame(view, view.ReactClip, ReactSeconds - view.ReactLeft, facing: 1);
                return;
            }

            // Then up off the stool and turned to the door (LEAVE, 2026-09-15) - still on the
            // stool as it is right now, for the reason the reaction beat above re-reads
            // SeatLineY. It ends on the walk's first pose, mirrored, so the walk out starts on
            // frame 0.
            if (view.LeaveLeft > 0f)
            {
                view.LeaveLeft -= RoomDelta;
                view.Root.anchoredPosition = new Vector2(view.SeatX, SeatLineY);
                UpdatePatronFrame(view, PatronClip.Leave, view.LeaveSeconds - view.LeaveLeft, facing: 1);
                if (view.LeaveLeft <= 0f) view.AnimClock = 0f;
                return;
            }

            // The entrance run backwards, at the entrance's one steady pace (see AdvanceWalkIn).
            float exitX = _hudRoot.rect.width + OffscreenMargin;
            float dist = Mathf.Max(1f, exitX - view.SeatX);
            view.ExitT = Mathf.Min(1f, view.ExitT + AnimDelta * WalkSpeed / dist);
            view.Root.anchoredPosition = new Vector2(
                Mathf.Lerp(view.SeatX, exitX, view.ExitT), SeatLineY);
            // Solid the whole way out, for the same reason they walk in solid: exitX is past
            // the frame's edge by OffscreenMargin, so they leave by leaving.
            view.Group.alpha = 1f;

            // Mirror the walk so they face the way they are leaving (to the right).
            UpdatePatronFrame(view, PatronClip.Walk, view.AnimClock, facing: -1);
            view.AnimClock += AnimDelta;

            if (view.ExitT >= 1f)
            {
                view.Exiting = false;
                HushSeat(view);        // whatever they said on the way out went out with them
                // They are through the door: book the visit and the stars against the FACE
                // that walked out, which is the last moment both are still in hand.
                RecordDeparture(view.Look, view.Visit);
                view.Visit = null;
                // AN EMPTY STOOL HAS NO FACE (2026-08-25). This line is the whole reason the
                // bar looked like four people on a loop. The arrival that reuses this stool
                // sets `v.Visit` FIRST and asks LookFor SECOND, and LookFor's first act is to
                // honour a stool that already holds a look - which this stool still did,
                // belonging to whoever just walked out. So every customer after the first on
                // any stool inherited the last one's face: four stools, four faces, all
                // night, every night, and a licence that read "3rd visit" with a full row of
                // stars on the bar's opening hour. Measured on 2026-08-25: seven different
                // people through the door, four faces drawn.
                view.Look = null;
                // The next customer on this stool speaks their own order from the beginning.
                view.WasKnown = false;
                view.Spoken = false;
                view.Group.alpha = 1f;
                if (view.Body != null) view.Body.flipX = false;   // reset the mirror
                view.Root.gameObject.SetActive(false);
            }
        }

        // ── the animated customer (2026-07-23) ───────────────────────────────────

        /// <summary>Chooses the clip and frame for a seated customer from their state and drives
        /// the character image: the sit-and-breathe idle while they wait, a one-shot "placing the
        /// order" beat the moment they decide, and the drink once served. (An impatience flush
        /// used to tint the body here; removed 2026-08-19, the author: "kızınca kızarmasın
        /// kararmasın" — running out of patience is the gauge's job, not the skin's.)</summary>
        private void UpdateSeatAnimation(SeatView view, CustomerVisit visit)
        {
            bool ordered = visit.HasOrdered;
            bool seated = view.WalkT >= 1f;
            bool drinking = visit.State == VisitState.Drinking;

            if (ordered && !view.WasOrdered && seated)
            {
                view.OrderAnimLeft = OrderAnimSeconds;
                Sfx.Play("order_ready", 0.6f);
            }
            view.WasOrdered = ordered;

            if (drinking) view.DrinkT += AnimDelta; else view.DrinkT = 0f;

            PatronClip clip; float t;
            if (!seated)                      { clip = PatronClip.Walk;  t = view.AnimClock; }   // faces left, walking in
            else if (view.ArriveLeft > 0f)    { clip = PatronClip.Arrive; t = view.ArriveSeconds - view.ArriveLeft;
                                                view.ArriveLeft -= RoomDelta; }
            else if (drinking)                { clip = PatronClip.Drink; t = view.DrinkT; }
            else if (view.OrderAnimLeft > 0f) { clip = PatronClip.Order; t = OrderAnimSeconds - view.OrderAnimLeft;
                                                view.OrderAnimLeft -= Time.deltaTime; }
            else                              { clip = PatronClip.Idle;  t = view.AnimClock; }
            // The walk's clock runs on the room's time, the time the floor is moved by, so the
            // step that reaches the stool is the frame the walk-in was phased for (SeatWalkClock).
            view.AnimClock += AnimDelta;   // seated or walking, the frames keep their own time (2026-09-21)

            // A seated customer's idle is a STILL frame, so the life in it comes from where
            // they are looking. This runs only in the idle branch — somebody speaking their
            // order or lifting a glass has better things to do with their head.
            int exact = -1;
            if (clip == PatronClip.Idle) SeatedGlance(view, ref clip, ref exact);
            UpdatePatronFrame(view, clip, t, facing: 1, exactFrame: exact);
        }

        /// <summary>
        /// Turns the idle into a look. Nobody beside them: they hold still and glance a
        /// little to one side every few seconds, alternating. Somebody on one side: they
        /// turn to that person and HOLD it while they are there. Somebody on both: they
        /// look between them, on the same slow clock.
        ///
        /// The clock is the seat's own animation time plus a phase off its index — six
        /// customers glancing in unison would read as a machine, and a phase is free where
        /// a random number would have to be plumbed through RunRng to stay deterministic.
        /// </summary>
        private void SeatedGlance(SeatView view, ref PatronClip clip, ref int frame)
        {
            var look = view.Look;
            if (look == null) return;
            bool right = Occupied(view.Index + 1), left = Occupied(view.Index - 1);

            // Somebody SITTING DOWN is the event, not somebody being there: the glance is a
            // one-shot on the rising edge, and it ends by coming back (2026-08-19, the
            // author: "bakma animasyonu bittikten sonra normal pozisyona geri dönmeli").
            // Held forever, it stopped being a glance and became a pose.
            if (right && !view.SawRight) { view.Greeting = true; view.GreetRight = true;  view.GreetT = 0f; }
            else if (left && !view.SawLeft) { view.Greeting = true; view.GreetRight = false; view.GreetT = 0f; }
            view.SawRight = right; view.SawLeft = left;

            bool useRight = view.Greeting ? view.GreetRight
                          : Mathf.FloorToInt((view.AnimClock + view.Index * 1.7f) / GlanceEvery) % 2 == 0;
            var want = useRight ? PatronClip.LookRight : PatronClip.LookLeft;
            if (!look.Clips.TryGetValue(want, out var frames) || frames.Length == 0)
            {
                view.Greeting = false;
                return;
            }
            int hold = Mathf.Clamp(useRight ? look.HoldRight : look.HoldLeft, 1, frames.Length - 1);

            if (view.Greeting)
            {
                view.GreetT += Time.deltaTime;
                // The head turns at the house rate too, not at a rate of its own: the turn
                // takes as long as its own frames take, which is what keeps a glance the
                // same speed as the walk beside it.
                float outT = hold / PatronFps, backAt = outT + GreetHoldSeconds;
                if (view.GreetT >= backAt + outT) { view.Greeting = false; return; }
                float u = view.GreetT < outT ? view.GreetT / outT
                        : view.GreetT < backAt ? 1f
                        : 1f - (view.GreetT - backAt) / outT;
                // Held at the MEASURED far end rather than the clip's last frame: some of
                // these clips swing back to the front on their own, and holding their end
                // would hold a face looking straight ahead.
                frame = Mathf.Clamp(Mathf.RoundToInt(u * hold), 0, hold);
                clip = want;
                return;
            }

            // Nobody just arrived: still for most of a slow cycle, then a small look out and
            // back. The clock carries a phase off the seat index — six customers glancing in
            // unison would read as a machine, and a phase is free where a random number
            // would have to be plumbed through RunRng to stay deterministic.
            float t = Mathf.Repeat(view.AnimClock + view.Index * 1.7f, GlanceEvery);
            if (t >= GlanceLookSeconds) return;                 // the still frame, which is idle
            int small = Mathf.Min(GlanceSmallFrame, frames.Length - 1);
            float g = t / GlanceLookSeconds;
            int f = g < 0.28f ? Mathf.FloorToInt(g / 0.28f * (small + 1))
                  : g > 0.72f ? Mathf.FloorToInt((1f - g) / 0.28f * (small + 1))
                  : small;
            frame = Mathf.Clamp(f, 0, small);
            clip = want;
        }

        /// <summary>Is there somebody sitting on that stool? Off the end of the bar counts
        /// as nobody, which is why the two end seats only ever glance one way.</summary>
        private bool Occupied(int index) =>
            index >= 0 && index < _seats.Count && _seats[index].Visit != null
            && _seats[index].WalkT >= 1f && !_seats[index].Exiting;

        /// <summary>Sets the character image to the right frame of <paramref name="clip"/> at time
        /// <paramref name="t"/>, mirrored when <paramref name="facing"/> is -1 (leaving right).
        /// <paramref name="exactFrame"/> overrides the clip's own timing when the caller has
        /// already decided which frame it wants (the glances, which are driven by who is
        /// sitting where rather than by a clock).</summary>
        private void UpdatePatronFrame(SeatView view, PatronClip clip, float t, int facing,
            int exactFrame = -1)
        {
            var look = view.Look ?? (_looks.Count > 0 ? _looks[0] : null);
            if (look == null || !look.Clips.TryGetValue(clip, out var frames) || frames.Length == 0) return;
            if (view.Body == null) return;
            view.Body.sprite = frames[exactFrame >= 0
                ? Mathf.Clamp(exactFrame, 0, frames.Length - 1)
                : PatronFrameIndex(clip, t, frames.Length)];
            // A touch wider than tall (CharWiden). The mirror is flipX rather than a negative
            // scale: a negative scale on a lit sprite inverts its winding and the 2D renderer
            // drops it, so a leaving customer would simply vanish.
            view.Body.flipX = facing < 0;
            SyncPatronBody(view);
        }

        /// <summary>How long a one-shot plays at PatronFps, or 0 when this face has no frames for
        /// it - a face drawn before ARRIVE and LEAVE existed simply snaps, as every face used to.</summary>
        private float OneShotSeconds(SeatView view, PatronClip clip)
        {
            var look = view.Look ?? (_looks.Count > 0 ? _looks[0] : null);
            return look != null && look.Clips.TryGetValue(clip, out var frames) && frames.Length > 0
                ? frames.Length / PatronFps : 0f;
        }

        /// <summary>
        /// Where a walk-in's clock starts, so that the step which reaches the stool is the cycle's
        /// FIRST frame - the pose the ARRIVE clip was drawn from (2026-09-15). The walk runs at
        /// one pace, so the time to the stool is known the moment the stool is given.
        /// </summary>
        private float SeatWalkClock(SeatView view)
        {
            if (_hudRoot == null) return 0f;
            float entryX = _hudRoot.rect.width + OffscreenMargin;
            float travel = Mathf.Max(1f, entryX - view.SeatX) / WalkSpeed;
            return WalkCycleSeconds - Mathf.Repeat(travel, WalkCycleSeconds);
        }

        /// <summary>The frame index for a clip at time t. Most clips loop at a fixed rate; the
        /// drink raises and lowers the glass over a sip window then holds it at rest, so it reads
        /// as a real sip every few seconds instead of a gulp every frame (2026-07-23).</summary>
        private static int PatronFrameIndex(PatronClip clip, float t, int n)
        {
            if (n <= 1) return 0;
            // STRAIGHT THROUGH, and hold the last frame. Every one-shot is now drawn in two
            // halves - out to the middle of the action, then INTERPOLATED back to the idle
            // pose - so its last frame is the idle pose and the return is drawn rather than
            // reversed. That is what the halves bought: a clip that ends where the idle
            // stands, at twice the frames, with nothing mirrored.
            // The walk is timed by its cycle, not by its frame count (see WalkCycleSeconds).
            if (clip == PatronClip.Walk)
                return ((Mathf.FloorToInt(t / WalkCycleSeconds * n) % n) + n) % n;
            if (clip == PatronClip.Drink)
            {
                // A sip, then a pause standing as they were, then another sip - the
                // "1. yudum, 2. yudum" the author asked for, out of one clip that ends
                // where it began. The clip is not played flat: see DrinkTicks.
                float u = Mathf.Repeat(t, DrinkCycleSeconds) * PatronFps;   // in ticks
                int acc = 0;
                for (int i = 0; i < n; i++)
                {
                    acc += DrinkTicks(i, n);
                    if (u < acc) return i;
                }
                return n - 1;   // the rest: standing with the glass down, as the clip left them
            }
            return Mathf.Min(n - 1, Mathf.FloorToInt(t * PatronFps));
        }

        /// <summary>
        /// THE DRINK'S TIMING CHART — how many ticks of 1/PatronFps each frame is held for.
        /// One rate still (the walk's), with HOLDS on it, which is how an animator slows a
        /// beat without slowing the film: "FPS tüm animasyonlarda aynı olmalı" is untouched.
        ///
        /// Everything hangs off ONE fact about the art: the clip is two halves joined —
        /// out to the glass at the mouth, then interpolated back to the idle pose — so the
        /// SIP IS THE MIDDLE FRAME. That is measured, not assumed, and it holds across the
        /// live cast's two clip lengths: 17 frames puts the glass at the lips on 7-8-9
        /// (afrowoman, clubgirl, silverbob) and 16 puts it on 6-7-8 (heavyset). The
        /// remaining cast still stands on the old rig and does not load; when they are
        /// redrawn they arrive at 17 like everyone else.
        ///
        ///   middle ±1   the swallow            5 ticks   0.42s each
        ///   ±2 … ±4     the arm's travel       2 ticks   the lift takes ~0.5s, and so does
        ///                                                the lower, where both were 0.25s
        ///   further     standing, glass down   1 tick    dead frames at either end
        ///
        /// A clip long enough for the chart to outrun DrinkCycleSeconds would be cut at the
        /// rest rather than dropping frames; at 35 ticks (2.9s) against a 4.4s cycle the
        /// longest clip in the cast has a second and a half of room.
        /// </summary>
        private static int DrinkTicks(int frame, int n)
        {
            int off = Mathf.Abs(frame - (n - 1) / 2);
            if (off <= 1) return DrinkSipTicks;
            return off <= 4 ? 2 : 1;
        }

        private void LoadPatronFrames()
        {
            _looks.Clear();
            foreach (var entry in PatronCast)
            {
                var clips = new Dictionary<PatronClip, Sprite[]>
                {
                    [PatronClip.Idle]  = LoadPatronClip(entry.Slug, "idle"),
                    [PatronClip.Order] = LoadPatronClip(entry.Slug, "order"),
                    [PatronClip.Drink] = LoadPatronClip(entry.Slug, "drink"),
                    [PatronClip.Walk]  = LoadPatronClip(entry.Slug, "walk"),
                    [PatronClip.Cheer] = LoadPatronClip(entry.Slug, "cheer"),
                    [PatronClip.Upset] = LoadPatronClip(entry.Slug, "upset"),
                    [PatronClip.LookRight] = LoadPatronClip(entry.Slug, "look_right"),
                    [PatronClip.LookLeft]  = LoadPatronClip(entry.Slug, "look_left"),
                    [PatronClip.Arrive]    = LoadPatronClip(entry.Slug, "arrive"),
                    [PatronClip.Leave]     = LoadPatronClip(entry.Slug, "leave"),
                };
                // A look with no idle has no art on disk. Skip it instead of seating a
                // customer who renders as nothing.
                if (clips[PatronClip.Idle].Length == 0) continue;
                var face = Resources.Load<Sprite>($"Patron/{entry.Slug}/face");
                _looks.Add(new PatronLook
                { Slug = entry.Slug, Clips = clips, HeadY = entry.HeadY, Face = face,
                  Stars = entry.Stars,
                  HoldRight = entry.HoldRight, HoldLeft = entry.HoldLeft,
                  HeadX = MeasureHeadX(clips[PatronClip.Idle][0], entry.HeadY) });
            }
        }

        /// <summary>
        /// Where a person's head is ACROSS, in HUD units from their sprite's pivot — the same
        /// measurement HeadY is, taken the other way (2026-09-13): the opaque pixels in the
        /// head's top rows of the idle frame, averaged. Zero when the frame cannot be read.
        /// </summary>
        private static float MeasureHeadX(Sprite frame, float headY)
        {
            if (frame == null || frame.texture == null || !frame.texture.isReadable) return 0f;
            var rect = frame.rect;
            int w = Mathf.RoundToInt(rect.width), h = Mathf.RoundToInt(rect.height);
            if (w <= 0 || h <= 0) return 0f;
            float perCanvas = h / CharCanvas;                 // texels per rig-canvas pixel
            int top = Mathf.RoundToInt(headY * perCanvas);
            int rows = Mathf.Max(1, Mathf.RoundToInt(24f * perCanvas));
            var px = frame.texture.GetPixels32();
            int tw = frame.texture.width;
            double sum = 0; int n = 0;
            for (int row = top; row < top + rows && row < h; row++)
            {
                int ty = Mathf.RoundToInt(rect.y) + h - 1 - row;
                for (int x = 0; x < w; x++)
                    if (px[ty * tw + Mathf.RoundToInt(rect.x) + x].a >= 128) { sum += x; n++; }
            }
            if (n == 0) return 0f;
            float headTexel = (float)(sum / n) + 0.5f;
            // Drawn about its pivot, CharSize HUD units for the frame's height (SyncPatronBody).
            return (headTexel - frame.pivot.x) * (CharSize / h) * CharWiden;
        }

        /// <summary>All frames of one clip, ordered by name. Everyone in the cast lives
        /// under their own slug. The very first patron used to sit loose at Patron/&lt;clip&gt;
        /// with no slug of their own, and this read that too; that art was deleted in the
        /// 2026-08-20 sweep along with the rest of the old rig, so the branch is gone.</summary>
        private static Sprite[] LoadPatronClip(string slug, string clip)
        {
            var sprites = Resources.LoadAll<Sprite>($"Patron/{slug}/{clip}");
            System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }

        // A DIFFERENT DOOR ORDER EVERY SESSION (2026-09-07, the author: "karakterlerin mekana
        // geliş sırası rastgele olmalı"). The faces stream was cut from the run's seed alone,
        // so every run played from the scene's fixed seed sat the same faces down in the same
        // order. Which DRAWING a person wears is cosmetic — the sim never asks — so this one
        // stream is salted with the clock; who they are, what they order and when they walk
        // in still come from the run's own seeded streams.
        private SeededRng FaceRng => _faceRng ?? (_faceRng =
            new RunRng(((_bootstrap != null ? _bootstrap.CurrentSeed : null) ?? "")
                       + "|faces|" + System.DateTime.UtcNow.Ticks).GetStream("faces"));

        /// <summary>
        /// Which face sits down. The same person is the same face every time they come back —
        /// recognising them across visits is the whole point of remembering anybody — and a
        /// person nobody has met yet is given whichever face has been off the floor longest.
        /// </summary>
        private PatronLook LookFor(CustomerVisit visit)
        {
            if (_looks.Count == 0) return null;
            // A seat that already holds a look keeps it: this is asked again every time
            // the licence is opened, and a face that changed under the player would undo
            // the whole point of having twenty-two of them.
            foreach (var seat in _seats)
                if (seat.Visit == visit && seat.Look != null) return seat.Look;

            // THE STORY'S GUEST IS NOT ROLLED (GDD 26 §8): the beat names the face, and it
            // is the same face every night of the run. Rolling one instead would put the
            // rent collector in a different body each time he came back — which is the
            // one thing a recurring character cannot survive.
            var run = Run;
            if (visit != null && run != null && ReferenceEquals(visit, run.LastCustomer))
            {
                var written = LookForStory(run.LastCallBeat?.Who);
                if (written != null) return written;
            }
            if (visit == null) return _looks[0];

            // Asked again after they have walked out — the night's invoice staples a
            // polaroid of its two witnesses to the takings. A second answer here would put
            // a stranger's photograph on the receipt.
            if (_faceOfVisit.TryGetValue(visit, out var booked)) return booked;

            // Only the people this bar has earned. Someone is always available — the
            // 0-star set never empties — so this cannot starve.
            float standing = run != null ? (float)run.Rating.Average : 0f;
            var open = new List<PatronLook>();
            foreach (var look in _looks)
                if (look.Stars <= standing + 0.001f) open.Add(look);
            if (open.Count == 0) open.Add(_looks[0]);

            // A FACE THAT COULD PASS FOR NINETEEN (GDD 28 §3.1, 2026-09-05): a visit the room
            // may read as young draws from the young pool — every minor does, and so do the
            // adults who look it, which is what keeps the face from being the verdict.
            if (visit.Regular != null && visit.Regular.LooksYoung)
            {
                var young = new List<PatronLook>();
                foreach (var look in open) if (IsYoung(look)) young.Add(look);
                if (young.Count > 0) open = young;
            }

            // NO TWO OF THE SAME DRAWING IN THE ROOM (the author, 2026-08-10). Each drawing
            // IS a character, so the same face on two stools reads as a bug rather than as
            // a coincidence — and the registry can hand back somebody who is still sitting
            // there, which is the one case that produces it.
            var free = new List<PatronLook>();
            foreach (var look in open)
            {
                bool taken = false;
                foreach (var seat in _seats)
                    if (seat.Visit != null && seat.Visit != visit && seat.Look == look)
                    { taken = true; break; }
                if (!taken) free.Add(look);
            }
            bool doubling = free.Count == 0;
            if (doubling) free = open;      // more stools than faces: somebody has to double up

            string person = visit.Regular != null ? visit.Regular.Id : null;
            PatronLook theirs = null;
            bool met = person != null && _faceOfPerson.TryGetValue(person, out theirs);
            if (met && (doubling || free.Contains(theirs)))
                return BookFace(visit, person, theirs);

            // The longest-unseen face, and a coin toss between those tied at the back of the
            // queue — which on the opening night is every face in the building.
            int oldest = int.MaxValue;
            foreach (var look in free)
                oldest = Math.Min(oldest, _faceLastSeen.TryGetValue(look, out var t) ? t : 0);
            var queue = new List<PatronLook>();
            foreach (var look in free)
                if ((_faceLastSeen.TryGetValue(look, out var seen) ? seen : 0) == oldest)
                    queue.Add(look);

            // A face they are only BORROWING: somebody the bar has already met, whose own
            // face is on another stool this minute, is drawn as a free one for this visit
            // alone and keeps their real face for the next time they come in.
            return BookFace(visit, met ? null : person, queue[FaceRng.NextInt(queue.Count)]);
        }

        private static string CrowdName(WealthTier tier) =>
            tier == WealthTier.HighRoller ? UIText.T("seats.crowd.high_rollers")
                : tier == WealthTier.Broke ? UIText.T("seats.crowd.broke") : UIText.T("seats.crowd.regulars");

        private PatronRecord LogFor(PatronLook look)
        {
            string key = look != null && !string.IsNullOrEmpty(look.Slug) ? look.Slug : "patron";
            PatronRecord rec;
            if (!_patronLog.TryGetValue(key, out rec))
            {
                rec = new PatronRecord();
                _patronLog[key] = rec;
            }
            return rec;
        }

        /// <summary>One person has walked back out. Books the visit and the stars they left.</summary>
        private void RecordDeparture(PatronLook look, CustomerVisit visit)
        {
            if (visit == null) return;
            var rec = LogFor(look);
            rec.Visits++;
            // THE SAME NUMBER THE BAR'S OWN STANDING IS BUILT FROM (BarRating.Record books
            // exactly this for every finished visit), so the licence and the top bar cannot
            // disagree about how a night went.
            rec.Stars += BarRating.ExactStarsFor(visit.Satisfaction);
            rec.Ratings++;
        }

        /// <summary>
        /// Stands the world body where its seat says, at the size and the alpha the seat is
        /// wearing. The body is a PASSENGER of the stool: every line that already moved,
        /// faded or hid a seat keeps working untouched, and this reads the result once a
        /// frame rather than each of them having to learn about the room.
        ///
        /// The conversion is the stage's own contract — one world unit is one stage unit,
        /// the HUD is drawn at twice that (StageToHud), and the stage's origin is the middle
        /// of its 640x360. The character's FEET sit CharFootDrop below the stool's line,
        /// which is what the counter then covers.
        /// </summary>
        private void SyncPatronBody(SeatView view)
        {
            if (view.Body == null) return;
            bool on = view.Root != null && view.Root.gameObject.activeSelf && view.Body.sprite != null;
            if (view.Body.gameObject.activeSelf != on) view.Body.gameObject.SetActive(on);
            if (!on) return;

            // WHO IS IN FRONT (2026-08-10, the author: a walker crossed in front of the
            // people already at the bar). Every body sat at one sorting order, so their
            // relative depth was whatever the renderer felt like. Somebody still walking
            // in or out is BEHIND everyone seated — they are further into the room — and
            // among the seated, the nearer stool draws in front, which is just perspective.
            bool walking = view.Exiting || view.WalkT < 1f;
            view.Body.sortingOrder = walking ? 22 : 25;

            float drawnH = CharSize / StageToHud;                       // stage units tall
            float k = drawnH / Mathf.Max(0.0001f, view.Body.sprite.bounds.size.y);
            view.Body.transform.localScale = new Vector3(k * CharWiden, k, 1f);

            var p = view.Root.anchoredPosition;
            float footY = (p.y - CharFootDrop) / StageToHud;            // stage units
            view.Body.transform.position = new Vector3(
                p.x / StageToHud - StageRef.x * 0.5f,
                footY + drawnH * 0.5f - StageRef.y * 0.5f, 0f);

            // SOLID OR GONE (2026-09-21, the author: "karakterlerde şeffaflık var ve arka planı gösteriyor, katı
            // olması gerekiyor"): a body never rides the group's fade at a middle alpha - the room showed through
            // it. It is drawn whole while the group shows at all, and not at all once the group is out.
            var c = view.Body.color;
            c.a = view.Group != null && view.Group.alpha <= 0.001f ? 0f : 1f;
            view.Body.color = c;
        }

        private PatronLook LookNamed(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return null;
            foreach (var look in _looks) if (look.Slug == slug) return look;
            return null;
        }

        /// <summary>The face this beat's person wears — their own if it has been drawn, the
        /// one they borrow until then (GDD 26 §1b). Null once neither exists, which is a
        /// content error the loader already refuses.</summary>
        private PatronLook LookForStory(StoryCharacter who) =>
            who == null ? null : LookNamed(who.Look) ?? LookNamed(who.PlaceholderLook);
    }
}
