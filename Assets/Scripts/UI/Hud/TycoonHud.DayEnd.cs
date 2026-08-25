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
    // TycoonHud, part DayEnd: the night's end: the slip, the stars that fall on it, the stamp, and the two.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    public sealed partial class TycoonHud
    {
        private void StepDayEndDue()
        {
            if (!_dayEndDue) return;
            if (!FloorIsClear() && Time.unscaledTime - _dayEndDueAt < DayEndPatience) return;
            _dayEndDue = false;
            ShowDayEnd();
        }

        /// <summary>
        /// A STAR REQUIREMENT, DRAWN (2026-08-25, the author: "yıldız gereksinimleri her
        /// zaman görsel olarak belirtilsin"). Five sockets and a gold row filled to
        /// <paramref name="stars"/> — per-star <see cref="Image.Type.Filled"/>, so a 3.5
        /// gate is three stars and a half rather than a rounded lie. Every surface that
        /// names a number in stars draws this beside it: the number says how many, the
        /// row says how far, and neither is asked to carry the meaning alone.
        /// </summary>
        private RectTransform StarRow(RectTransform parent, Vector2 anchor, Vector2 pos,
            float px, double stars, Color lit, Color socket)
        {
            float pitch = px + 2f;
            var row = NewRect("StarRow", parent);
            Place(row, anchor, new Vector2(BarRating.MaxStars * pitch, px), pos);
            var art = ItemArt.Load("star");
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var cell = NewRect("S" + i, row);
                Place(cell, new Vector2(0, 0.5f), new Vector2(px, px), new Vector2(i * pitch, 0));
                cell.pivot = new Vector2(0, 0.5f);
                var back = cell.gameObject.AddComponent<Image>();
                back.sprite = art;
                back.color = socket;
                back.preserveAspect = true;
                back.raycastTarget = false;
                float fill = Mathf.Clamp01((float)stars - i);
                if (fill <= 0.001f) continue;
                var over = NewRect("F", cell);
                Stretch(over, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var oi = over.gameObject.AddComponent<Image>();
                oi.sprite = art;
                oi.color = lit;
                oi.preserveAspect = true;
                oi.raycastTarget = false;
                oi.type = Image.Type.Filled;
                oi.fillMethod = Image.FillMethod.Horizontal;
                oi.fillOrigin = (int)Image.OriginHorizontal.Left;
                oi.fillAmount = fill;
            }
            return row;
        }

        private void StepDayEndBeats()
        {
            if (_endBeat == 0) return;
            _endT += Time.unscaledDeltaTime;

            if (_endBeat == 1)
            {
                if (Motion.Reduced) { _endT = CallIn + CallHold + CallOut; }
                float a = _endT < CallIn ? _endT / CallIn
                        : _endT < CallIn + CallHold ? 1f
                        : 1f - Mathf.Clamp01((_endT - CallIn - CallHold) / CallOut);
                if (_lastCallGroup != null) _lastCallGroup.alpha = a;
                if (_lastCallCard != null)
                {
                    // It settles as it arrives — a line that lands rather than appears.
                    float k = Mathf.Clamp01(_endT / CallIn);
                    _lastCallCard.rectTransform.anchoredPosition =
                        new Vector2(0, 10f + (1f - k) * 14f);
                }
                if (_endT < CallIn + CallHold + CallOut) return;
                if (_lastCallRt != null) _lastCallRt.gameObject.SetActive(false);
                _endBeat = 2; _endT = 0f;
                // HOME FIRST, THEN FEED. PlayPanel reads the rect's CURRENT position as the
                // place to land, and the slip has been parked off the top since the call —
                // so handing it the parked rect made it feed from 1520 down to 760 and then
                // jump the last 760 when the beat ended. Measured exactly that; put back
                // where it belongs first and the feed is one unbroken movement.
                _dayEndBill.anchoredPosition = _billHome;
                PlayPanel(_dayEndBill, new Vector2(0, SlipFeedFrom), SlipFeed,
                          fade: false, steady: true);
                return;
            }

            if (_endBeat == 2)
            {
                // The two instruments come in WITH the paper, from their own sides — the
                // desk being laid out while the till prints, which is one movement rather
                // than three screens arriving one after another.
                SetBoardsIn(Motion.Reduced ? 1f : Mathf.Clamp01(_endT / BoardsIn));
                // The slide owns the paper until it settles; the stars wait for that.
                if (_slideRt != null && !Motion.Reduced) return;
                SetBoardsIn(1f);
                _dayEndBill.anchoredPosition = _billHome;
                _endBeat = 3;
                StartStarDrop(_endStarFrac);
                return;
            }

            // Beat 4: the night's stars are in, so now the BAR moves. It is the last thing
            // that happens and it happens alone — the standing climbing into its stars with
            // the step it took printed beside it (2026-08-25).
            if (_endBeat == 4)
            {
                StepStandingClimb();
                if (_standT >= 0f) return;
                _endBeat = 0;
                if (_billNext != null && _dayEndStep == 0) _billNext.gameObject.SetActive(true);
                return;
            }

            // Beat 3: the shake lives here, so the paper is only ever moved by one thing.
            if (_billShake > 0f)
            {
                _billShake = Mathf.Max(0f, _billShake - Time.unscaledDeltaTime * 4.5f);
                float amp = _billShake * _billShake * 7f;   // dies away fast, like a strike
                _dayEndBill.anchoredPosition = _billHome + new Vector2(
                    Mathf.Sin(Time.unscaledTime * 62f) * amp * 0.5f,
                    Mathf.Sin(Time.unscaledTime * 47f) * amp);
                if (_billShake <= 0f) _dayEndBill.anchoredPosition = _billHome;
            }
            if (_starT < 0f && _stampT < 0f && _billShake <= 0f)
            {
                // The night has finished counting itself. What it did to the BAR is the
                // last beat — and the way out waits for that too, for the reason the
                // CONTINUE key waited for the stars.
                _endBeat = 4; _endT = 0f;
                StartStandingClimb();
                if (_standT < 0f)
                {
                    _endBeat = 0;
                    if (_billNext != null && _dayEndStep == 0) _billNext.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>Empties the row and starts the run. Reduced motion places them.</summary>
        private void StartStarDrop(float frac)
        {
            _starCount = Mathf.CeilToInt(Mathf.Clamp01(frac) * 5f - 0.001f);
            _landed = 0;
            // WHEN THE STAMP LANDS DEPENDS ON WHAT IT SAYS. A night that earned nothing has
            // no stars to wait for, so the stamp takes the beat they would have had. A
            // RECORD has to wait for them: the whole point is that the fifth star lands and
            // then the paper is stamped for it, which is a different sentence from stamping
            // over an empty row.
            _stampArmed = false;
            SetStampFace(_stampKind);
            // AND IT IS NOT ON THE PAPER UNTIL IT IS STRUCK (2026-08-19, the author: NEW
            // RECORD was sitting over the stars before its own animation). Showing it here
            // and only ARMING it when the last star landed are two different things, and
            // this line did the first: the stamp spent the whole star run parked at its
            // rest pose — full size, printed, crooked — over the row it was waiting for,
            // and then struck itself down over its own ink. It is shown by ArmStamp now,
            // on the frame it is driven at the paper and not before.
            if (_billStamp != null) _billStamp.gameObject.SetActive(false);
            _stampT = -1f;
            // A night that earned nothing has no stars to wait for, and reduced motion has
            // no run to wait for either — both take the stamp now.
            if (_stampKind != StampKind.None && (_starCount <= 0 || Motion.Reduced)) ArmStamp();
            if (Motion.Reduced || _billStars.Count == 0) { _starT = -1f; return; }
            _starT = 0f;
            foreach (var s in _billStars)
            {
                s.localScale = Vector3.one;
                var g = s.GetComponent<Image>();
                if (g != null) g.color = new Color(g.color.r, g.color.g, g.color.b, 0f);
            }
        }

        private void StepStarDrop()
        {
            if (_starT < 0f || _billStars.Count == 0) return;
            _starT += Time.unscaledDeltaTime;
            bool running = false;
            for (int i = 0; i < _billStars.Count; i++)
            {
                var star = _billStars[i];
                if (star == null) continue;
                var img = star.GetComponent<Image>();
                if (i >= _starCount)
                {
                    // Past the night's count: nothing to land, and the mask hides it anyway.
                    if (img != null) img.color = Opaque(img.color);
                    continue;
                }
                float t = _starT - i * StarStagger;
                if (t < 0f)
                {
                    if (img != null) img.color = Clear(img.color);
                    star.anchoredPosition = new Vector2(star.anchoredPosition.x, StarFallH);
                    running = true;
                    continue;
                }
                float k = Mathf.Clamp01(t / StarDrop);
                // Out-back: it falls past its place and rocks back into it.
                const float Over = 1.7f;
                float u = k - 1f;
                float e = u * u * ((Over + 1f) * u + Over) + 1f;
                star.anchoredPosition = new Vector2(star.anchoredPosition.x,
                    Mathf.Lerp(StarFallH, 0f, e));
                // ...and rolls as it lands, the wobble dying with the fall.
                star.localRotation = Quaternion.Euler(0, 0,
                    Mathf.Sin(k * Mathf.PI * 3f) * 14f * (1f - k));
                if (img != null)
                    img.color = new Color(img.color.r, img.color.g, img.color.b,
                                          Mathf.Clamp01(k * 4f));
                if (k < 1f) running = true;

                // THE SHAKE FIRES ON CONTACT, NOT ON REST (2026-08-11, the author: the
                // tremor and the star landing are not in step). They were not, and the
                // easing says why: an out-back curve reaches its target EARLY, punches
                // past it and rocks back. Solving e(k) = 1 for this overshoot gives
                // k = Over / (Over + 1) subtracted from 1 — 0.370 at Over 1.7 — and the
                // star is visibly on the paper from that moment, while the tween does not
                // finish until 1.0. Firing at the end put the tremor two thirds of a beat
                // after the impact it was meant to be.
                if (i >= _landed && k >= Contact) { _landed = i + 1; _billShake = 1f;
                    Sfx.Play("click", 0.5f); }
            }
            if (!running)
            {
                foreach (var s in _billStars)
                    if (s != null) { s.anchoredPosition = new Vector2(s.anchoredPosition.x, 0f);
                                     s.localRotation = Quaternion.identity; }
                _starT = -1f;
                // The stars are in; if the night beat every night before it, say so.
                if (_stampKind == StampKind.Record) ArmStamp();
            }
        }

        private float BillStars(float y, float frac)
        {
            const float StarPx = 32f, Gap = 6f;   // the 16px star at a whole 2x
            float rowW = 5f * StarPx + 4f * Gap;
            var host = NewRect("Stars", _invoiceRows);
            host.anchorMin = new Vector2(0.5f, 1); host.anchorMax = new Vector2(0.5f, 1);
            host.pivot = new Vector2(0.5f, 1);
            host.sizeDelta = new Vector2(rowW, StarPx);
            host.anchoredPosition = new Vector2(0, -y);
            var art = ChromeArt.Mark("star");
            for (int i = 0; i < 5; i++)
            {
                var dim = NewRect("D" + i, host);
                Place(dim, new Vector2(0, 0.5f), new Vector2(StarPx, StarPx),
                    new Vector2(i * (StarPx + Gap) + StarPx * 0.5f, 0));
                dim.pivot = new Vector2(0.5f, 0.5f);
                var di = dim.gameObject.AddComponent<Image>();
                di.sprite = art; di.preserveAspect = true; di.raycastTarget = false;
                di.color = new Color(0.72f, 0.68f, 0.60f, 0.5f);
            }
            var lit = NewRect("Lit", host);
            lit.anchorMin = new Vector2(0, 0); lit.anchorMax = new Vector2(0, 1);
            lit.pivot = new Vector2(0, 0.5f);
            // TALLER THAN THE ROW, on purpose (2026-08-11). The mask is what cuts a half
            // star in half, and it has to keep doing that — but the stars now FALL into
            // place, and a mask the height of the row would clip the fall to nothing. Extra
            // height changes no horizontal clipping at all, which is the only clipping this
            // mask was ever for.
            lit.sizeDelta = new Vector2(rowW * Mathf.Clamp01(frac), StarFallH * 2f);
            lit.anchoredPosition = Vector2.zero;
            lit.gameObject.AddComponent<RectMask2D>();
            _billStars.Clear();
            for (int i = 0; i < 5; i++)
            {
                var on = NewRect("L" + i, lit);
                Place(on, new Vector2(0, 0.5f), new Vector2(StarPx, StarPx),
                    new Vector2(i * (StarPx + Gap) + StarPx * 0.5f, 0));
                _billStars.Add(on);
                on.pivot = new Vector2(0.5f, 0.5f);
                var oi = on.gameObject.AddComponent<Image>();
                oi.sprite = art; oi.preserveAspect = true; oi.raycastTarget = false;
                oi.color = UITheme.Amber[3];
            }

            // THE STAMP, for a night that earned nothing (2026-08-11, the author: if you
            // take zero stars something aggressive should come down over them, like a stamp
            // being struck). It only exists because zero is reachable now — under the old
            // 1 + 4x scale the worst room in the world still filed one star, so there was
            // never a night for this to land on.
            //
            // It hangs on the ROWS rather than on the star host, because it has to be wider
            // than the five stars it is being struck across, and it is parked here rather
            // than built on demand: the run is driven from Update, and a thing that has to
            // be animated has to exist before the frame it animates in.
            _billStamp = NewRect("Stamp", _invoiceRows);
            _billStamp.anchorMin = new Vector2(0.5f, 1); _billStamp.anchorMax = new Vector2(0.5f, 1);
            _billStamp.pivot = new Vector2(0.5f, 0.5f);
            _billStamp.sizeDelta = new Vector2(236f, 42f);
            _billStamp.anchoredPosition = new Vector2(0, -(y + StarPx * 0.5f));
            var stampPlate = _billStamp.gameObject.AddComponent<Image>();
            stampPlate.color = new Color(BillRed.r, BillRed.g, BillRed.b, 0.10f);
            stampPlate.raycastTarget = false;
            Frame(_billStamp, 3f, new Color(BillRed.r, BillRed.g, BillRed.b, 0.85f));
            _billStampInk = NewText("W", _billStamp, _display, 24, TextAnchor.MiddleCenter,
                new Color(BillRed.r, BillRed.g, BillRed.b, 0.92f));
            Stretch(_billStampInk.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, 0), new Vector2(-4, 0));
            _billStampInk.horizontalOverflow = HorizontalWrapMode.Overflow;
            _billStampInk.verticalOverflow = VerticalWrapMode.Overflow;
            _billStampInk.raycastTarget = false;
            _billStampInk.text = "DISGRACE";
            _billStamp.gameObject.SetActive(false);

            return y + StarPx + 6f;
        }

        /// <summary>Dresses the stamp for what it is about to say.</summary>
        private void SetStampFace(StampKind kind)
        {
            _stampKind = kind;
            if (_billStamp == null || kind == StampKind.None) return;
            bool good = kind == StampKind.Record;
            var ink = good ? new Color(0.16f, 0.44f, 0.20f) : BillRed;
            _billStamp.GetComponent<Image>().color = new Color(ink.r, ink.g, ink.b, 0.10f);
            foreach (var edge in _billStamp.GetComponentsInChildren<Image>(true))
                if (edge.transform != _billStamp)
                    edge.color = new Color(ink.r, ink.g, ink.b, 0.85f);
            _billStampInk.color = new Color(ink.r, ink.g, ink.b, 0.92f);
            _billStampInk.text = good ? "NEW RECORD" : "DISGRACE";
            _billStamp.sizeDelta = new Vector2(good ? 268f : 236f, 42f);
        }

        /// <summary>
        /// A rubber stamp is a thing DRIVEN at the paper: it arrives huge, out of focus and
        /// crooked, and it stops dead. So it scales down hard rather than easing, and the
        /// only softness in it is after the strike — it rocks a few degrees and settles,
        /// and the paper takes the blow on the same frame the ink lands.
        /// </summary>
        private void ArmStamp()
        {
            if (_stampArmed || _billStamp == null || _stampKind == StampKind.None) return;
            _stampArmed = true;
            if (Motion.Reduced)
            {
                _billStamp.localScale = Vector3.one;
                _billStamp.localRotation = Quaternion.Euler(0, 0, -9f);
                _billStamp.gameObject.SetActive(true);
                return;
            }
            _stampT = 0f;
            // THE FIRST FRAME OF THE STRIKE IS SET HERE, not left to the step that runs
            // next frame. Arming can happen after StepStamp has already run for this frame
            // (the zero-star night arms from the beats, which are stepped last), and a stamp
            // shown at whatever pose it was left in flashes at rest for one frame before it
            // starts falling. Shown huge, crooked and unprinted, it can only fall.
            _billStamp.localScale = new Vector3(3.4f, 3.4f, 1f);
            _billStamp.localRotation = Quaternion.Euler(0, 0, -26f);
            var ink0 = _billStampInk.color;
            _billStampInk.color = new Color(ink0.r, ink0.g, ink0.b, 0f);
            _billStamp.gameObject.SetActive(true);
        }

        private void StepStamp()
        {
            if (_stampT < 0f || _billStamp == null) return;
            _stampT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_stampT / StampFall);
            float e = k * k * k;                            // gathers pace all the way down
            float scale = Mathf.Lerp(3.4f, 1f, e);
            _billStamp.localScale = new Vector3(scale, scale, 1f);
            _billStamp.localRotation = Quaternion.Euler(0, 0,
                Mathf.Lerp(-26f, -9f, e) + Mathf.Sin(k * Mathf.PI * 4f) * 3f * (1f - k));
            var c = _billStampInk.color;
            _billStampInk.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(k * 2.2f) * 0.92f);
            if (k < 1f) return;
            _billStamp.localScale = Vector3.one;
            _billStamp.localRotation = Quaternion.Euler(0, 0, -9f);
            _stampT = -1f;
            _billShake = 1f;                                 // the paper takes it
            Sfx.Play("click", 0.9f);
        }

        /// <summary>
        /// One critic: their licence photo, their stars, their name, and one short line of
        /// WHY — derived from what the visit still knows at day end. The face is the point:
        /// reading customers is the game, so the night's verdicts wear the faces that gave
        /// them.
        /// </summary>
        private float BillCritic(float y, CustomerVisit v, Color ink)
        {
            // ONE LINE, IN COLUMNS (2026-08-11, the author: not two stacked lines, a bit
            // more table-like without being a table; the name smaller and lighter; no star
            // pictogram for the score; and the DRINK shown by its own icon).
            //
            // The columns are the receipt's own: the picture, the drink, the name and what
            // happened, and the score in the same right-hand column the money lands in. That
            // last alignment is what makes it read as a book rather than as a caption —
            // without a single rule being drawn.
            const float Photo = 34f, Frame = 3f, Chin = 9f, Glyph = 20f;
            float cardW = Photo + Frame * 2f, cardH = Photo + Frame + Chin;
            float rowH = Mathf.Max(cardH, 26f) + 6f;

            var row = NewRect("Critic", _invoiceRows);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.sizeDelta = new Vector2(0, rowH);
            row.anchoredPosition = new Vector2(0, -y);

            // A POLAROID: the white border a print has, thicker under the picture than
            // around it, dropped at a slight angle. A night's two witnesses are stapled to
            // the takings, and a print says that; a bare square crop said "here is a face".
            var look = LookFor(v);
            if (look != null && look.Face != null)
            {
                var card = NewRect("Polaroid", row);
                Place(card, new Vector2(0, 0.5f), new Vector2(cardW, cardH), Vector2.zero);
                card.pivot = new Vector2(0, 0.5f);
                card.localRotation = Quaternion.Euler(0, 0, ink == BillRed ? 2.5f : -2.5f);
                var ci = card.gameObject.AddComponent<Image>();
                ci.color = new Color(0.99f, 0.98f, 0.94f);
                ci.raycastTarget = false;
                var lift = card.gameObject.AddComponent<Shadow>();
                lift.effectColor = new Color(0.24f, 0.15f, 0.06f, 0.32f);
                lift.effectDistance = new Vector2(2, -2);

                var photo = NewRect("P", card);
                Place(photo, new Vector2(0, 1), new Vector2(Photo, Photo), new Vector2(Frame, -Frame));
                photo.pivot = new Vector2(0, 1);
                var pi = photo.gameObject.AddComponent<Image>();
                pi.sprite = look.Face; pi.raycastTarget = false;
            }

            // What they were poured, drawn rather than named — the same icon the ticket and
            // the book use, so one glance ties the three together.
            var served = v.Served ?? (v.IdInspected ? v.Order.Wanted : null);
            if (served != null)
            {
                var glyph = NewRect("D", row);
                Place(glyph, new Vector2(0, 0.5f), new Vector2(Glyph, Glyph),
                    new Vector2(cardW + 8f, 0));
                glyph.pivot = new Vector2(0, 0.5f);
                var gi = glyph.gameObject.AddComponent<Image>();
                gi.sprite = DrinkIcon.For(served, _bootstrap.Glassware);
                gi.preserveAspect = true; gi.raycastTarget = false;
                gi.enabled = gi.sprite != null;
            }

            var papers = PapersFor(look);
            string full = papers != null ? papers.Name
                : v.Regular != null ? v.Regular.Name : "a drinker";
            // THE FIRST NAME ONLY (2026-08-11). "MEREDITH NOLAN  walked out" is 26
            // characters and the column holds 23, so the row wrapped and became the two
            // lines this was built to stop being. A receipt says a first name anyway.
            int space = full.IndexOf(' ');
            string name = (space > 0 ? full.Substring(0, space) : full).ToUpperInvariant();

            // Smaller and lighter: the regular face at 16, where it used to be the heavy one
            // at 24. A name on a receipt is a line item, not a headline.
            float textX = cardW + 8f + Glyph + 8f;
            float textW = BillW - BillInset * 2f - textX - 72f;   // the star and score keep the right
            var line = NewText("L", row, _body, 16, TextAnchor.MiddleLeft, ink);
            Place(line.rectTransform, new Vector2(0, 0.5f), new Vector2(textW, rowH),
                new Vector2(textX, 0));
            line.rectTransform.pivot = new Vector2(0, 0.5f);
            // Truncate is refused here as everywhere on this slip: at these sizes it drops
            // the WHOLE line the moment the face's line height clears the rect, and both
            // critics once rendered as a star, a reason and no name at all.
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Overflow;
            line.supportRichText = true;
            line.text = name + "  <color=#" + ColorUtility.ToHtmlStringRGB(BillQuiet) + ">"
                        + CriticReason(v) + "</color>";

            // A STAR BESIDE THE FIGURE (2026-08-11, the author: so it is understood that
            // the number with a point in it is a star rating). Not five of them — the row
            // above already draws the night as five — but ONE, as a unit mark, the way a
            // price carries a currency sign. It is the smallest thing that turns "1.0" from
            // a number into a score.
            var unit = NewRect("U", row);
            Place(unit, new Vector2(1, 0.5f), new Vector2(14, 14), new Vector2(-54f, 0));
            unit.pivot = new Vector2(1, 0.5f);
            var ui = unit.gameObject.AddComponent<Image>();
            ui.sprite = ChromeArt.Mark("star");
            ui.preserveAspect = true; ui.raycastTarget = false; ui.color = ink;

            var score = NewText("N", row, _body, 24, TextAnchor.MiddleRight, ink);
            Place(score.rectTransform, new Vector2(1, 0.5f), new Vector2(52f, rowH),
                new Vector2(0, 0));
            score.rectTransform.pivot = new Vector2(1, 0.5f);
            score.horizontalOverflow = HorizontalWrapMode.Overflow;
            score.verticalOverflow = VerticalWrapMode.Overflow;
            score.text = BarRating.ExactStarsFor(v.Satisfaction).ToString("0.0");

            return y + rowH;
        }

        /// <summary>One short honest line, from what a finished visit still carries. The
        /// judge's full verdict is transient — said in the service log, never stored — so
        /// this reads the STATE: how they left, what they were made, how it landed.</summary>
        private string CriticReason(CustomerVisit v)
        {
            // SHORT, because the row is one line now (2026-08-11). The drink is drawn beside
            // the name, so the reason no longer has to name it — it only has to say what
            // went right or wrong, in the fewest words that still sound like a person.
            if (v.State == VisitState.StormedOff) return "walked out";
            if (v.IdInspected && v.Served != null && v.Order.Wanted.Id != v.Served.Id)
                return "wrong drink";
            if (v.Satisfaction >= 0.85) return "exactly right";
            if (v.Satisfaction >= 0.55) return "a fair pour";
            return "a rough pour";
        }

        private float BillRow(float y, string label, string value, Color ink, bool heavy) =>
            BillRow(y, label, value, ink, heavy, null);

        /// <summary>A block's subtotal: a short rule over the figures it adds up, and the
        /// figure alone on the right. No label — the block above it is the label.</summary>
        private float BillSub(float y, string value, Color ink)
        {
            var row = NewRect("Sub", _invoiceRows);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.sizeDelta = new Vector2(0, BillRowH);
            row.anchoredPosition = new Vector2(0, -y);

            var rule = NewRect("R", row);
            rule.anchorMin = new Vector2(0.62f, 1); rule.anchorMax = new Vector2(1, 1);
            rule.pivot = new Vector2(0.5f, 1);
            rule.sizeDelta = new Vector2(0, 1);
            rule.anchoredPosition = Vector2.zero;
            var ri = rule.gameObject.AddComponent<Image>();
            ri.color = new Color(ink.r, ink.g, ink.b, 0.45f);
            ri.raycastTarget = false;

            var v = NewText("V", row, _body, 24, TextAnchor.MiddleRight, ink);
            v.rectTransform.anchorMin = new Vector2(0.62f, 0); v.rectTransform.anchorMax = Vector2.one;
            v.rectTransform.offsetMin = Vector2.zero; v.rectTransform.offsetMax = Vector2.zero;
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            v.verticalOverflow = VerticalWrapMode.Overflow;
            v.text = value;
            return y + BillRowH;
        }

        private float BillRow(float y, string label, string value, Color ink, bool heavy, string mark)
        {
            var row = NewRect("R", _invoiceRows);
            Place(row, new Vector2(0, 1), new Vector2(0, BillRowH), new Vector2(0, -y));
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.sizeDelta = new Vector2(0, BillRowH);
            row.anchoredPosition = new Vector2(0, -y);

            // THE MARK (2026-08-10, the author asked for one per line). White silhouettes
            // tinted by the row's own ink, so the colour says whether it cost you and the
            // shape says what it was — neither has to carry both, which is the rule the
            // inspector's buff icons already follow.
            float gutter = 0f;
            if (!string.IsNullOrEmpty(mark))
            {
                // Hand-drawn at the size it prints (see ChromeArt). The generated set was
                // seven little illustrations shrunk to 16 px, which is mud with a shadow on
                // it — the author asked for simpler and more useful, and a mark that has one
                // silhouette is both.
                var art = ChromeArt.Mark(mark);
                if (art != null)
                {
                    var icon = NewRect("M", row);
                    Place(icon, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(0, 0));
                    var iimg = icon.gameObject.AddComponent<Image>();
                    iimg.sprite = art; iimg.color = ink; iimg.raycastTarget = false;
                    gutter = 24f;
                }
            }

            // BOLD IS FOR HEADINGS (2026-08-11, the author: "çok fazla kalın yazı kullanma,
            // sadece başlıklarda"). Only the two summary lines — NET and TILL — carry the
            // heavy face now; every itemised line is set in the regular one, which is what a
            // receipt does anyway: the total is the thing you are meant to see first.
            // AND THE HEAVY FACE IS NO LONGER SILKSCREEN BOLD (2026-08-11, the author:
            // "4 gibi sayilar cok kalin oldugundan sayi arasindaki bosluklar birlesiyor").
            // That is exactly what it is. The face is drawn on an 8px grid with no side
            // bearing, so at a whole 3x its digits touch and -$14 reads as one shape; the
            // pixel size was never the problem, the metrics were. PressStart2P carries its
            // gap INSIDE the cell, which is why it can be set solid at any size, and it is
            // already the game's display type. It is wider, so the heavy rows drop to 16 —
            // still the biggest thing on the slip, because nothing else is set in it.
            var l = NewText("L", row, heavy ? _display : _body, heavy ? 16 : 24,
                            TextAnchor.MiddleLeft, ink);
            l.rectTransform.anchorMin = new Vector2(0, 0); l.rectTransform.anchorMax = new Vector2(0.62f, 1);
            l.rectTransform.offsetMin = new Vector2(gutter, 0); l.rectTransform.offsetMax = Vector2.zero;
            // Overflow on both axes: the labels are one short word each, and Truncate at
            // this size drops the WHOLE line the moment the face's line height clears the
            // row — which is exactly how the critics' names went missing.
            l.horizontalOverflow = HorizontalWrapMode.Overflow;
            l.verticalOverflow = VerticalWrapMode.Overflow;
            l.text = label;

            // The figure follows the label's weight, and for a reason beyond the rule above:
            // SilkscreenBold's digits do not survive this size — the author's screenshot has
            // a SALES of $4 whose 4 is a smear, and RENT's -$14 with it. PressStart2P is not
            // the escape either, at a full 24 units a character "-$1240" would be 144 of the
            // 146 this column has. The regular face is narrow, legible and correct.
            var v = NewText("V", row, heavy ? _display : _body, heavy ? 16 : 24,
                            TextAnchor.MiddleRight, ink);
            v.rectTransform.anchorMin = new Vector2(0.62f, 0); v.rectTransform.anchorMax = Vector2.one;
            v.rectTransform.offsetMin = Vector2.zero; v.rectTransform.offsetMax = Vector2.zero;
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            v.verticalOverflow = VerticalWrapMode.Overflow;
            v.text = value;
            return y + BillRowH;
        }

        private float BillRule(float y)
        {
            var rule = NewRect("Rule", _invoiceRows);
            rule.anchorMin = new Vector2(0, 1); rule.anchorMax = new Vector2(1, 1);
            rule.pivot = new Vector2(0.5f, 1);
            rule.sizeDelta = new Vector2(0, 1);
            rule.anchoredPosition = new Vector2(0, -(y + 5f));
            rule.gameObject.AddComponent<Image>().color = BillEdge;
            return y + 12f;
        }

        private float BillNote(float y, string text) => BillNote(y, text, BillQuiet);

        private float BillNote(float y, string text, Color ink, bool centred = false)
        {
            var note = NewText("N", _invoiceRows, _body, 16,
                centred ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, ink);
            note.rectTransform.anchorMin = new Vector2(0, 1);
            note.rectTransform.anchorMax = new Vector2(1, 1);
            note.rectTransform.pivot = new Vector2(0.5f, 1);
            note.rectTransform.sizeDelta = new Vector2(0, 19f);
            note.rectTransform.anchoredPosition = new Vector2(0, -y);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.verticalOverflow = VerticalWrapMode.Overflow;
            note.text = text;
            return y + 21f;
        }

        /// <summary>
        /// The print can never run off the paper (2026-08-11, the author: "sayfanın
        /// taşmaması için altındakileri de ona göre ayarlaman gerekiyor").
        ///
        /// The slip is a fixed roll and its content is the night's, which varies: two
        /// critics or none, a debt strike or not, a warning line under it. Tuning the row
        /// heights until one measured night fits is how a layout breaks on the night after,
        /// so the block is MEASURED after it is built and, if it is longer than the paper,
        /// scaled to it. Almost every night leaves this at 1 — it is a floor under the
        /// design, not the design.
        /// </summary>
        private void FitBillToPaper(float printed)
        {
            const float FootRoom = 22f;
            float room = BillH - BillRowsTop - FootRoom;
            float k = printed > room && printed > 0f ? room / printed : 1f;
            _invoiceRows.localScale = new Vector3(k, k, 1f);
        }

        private void BuildNightBoards(RectTransform panel)
        {
            _weekBoard = NightBoardPlate(panel, "WeekBoard", -BoardX, "THE WEEK");
            // IT SAYS WHICH READING IT IS. The top bar is still lit above the scrim with the
            // standing the bar WALKED IN with — the books have not closed, so it is telling
            // the truth — and two different star counts on one screen with nothing to tell
            // them apart is exactly the drift this project refuses everywhere else. The head
            // names the one this instrument shows.
            _standBoard = NightBoardPlate(panel, "StandBoard", BoardX, "AFTER TONIGHT");
        }

        private NightBoard NightBoardPlate(RectTransform panel, string name, float x, string caption)
        {
            var board = new NightBoard();
            board.Root = NewRect(name, panel);
            Place(board.Root, new Vector2(0.5f, 0.5f), new Vector2(BoardW, BoardH),
                new Vector2(x, BoardY));
            var plate = board.Root.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.Card();
            plate.type = Image.Type.Sliced;
            plate.color = BoardPlate;
            plate.raycastTarget = false;
            board.Group = board.Root.gameObject.AddComponent<CanvasGroup>();
            board.Group.blocksRaycasts = false;

            // The head names the instrument and gives its one reading, which is the same
            // grammar the top bar's wells use: a small caption, a big figure.
            var cap = NewText("Cap", board.Root, _body, 16, TextAnchor.MiddleLeft, UITheme.Cyan[3]);
            Place(cap.rectTransform, new Vector2(0, 1), new Vector2(BoardW - BoardPad * 2f, 20),
                new Vector2(BoardPad, -22f));
            cap.rectTransform.pivot = new Vector2(0, 0.5f);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            cap.text = caption;

            board.Reading = NewText("Reading", board.Root, _display, 16, TextAnchor.MiddleRight,
                UITheme.Amber[4]);
            Place(board.Reading.rectTransform, new Vector2(1, 1), new Vector2(150, 20),
                new Vector2(-BoardPad, -22f));
            board.Reading.rectTransform.pivot = new Vector2(1, 0.5f);
            board.Reading.horizontalOverflow = HorizontalWrapMode.Overflow;
            board.Reading.verticalOverflow = VerticalWrapMode.Overflow;

            var rule = NewRect("Rule", board.Root);
            Place(rule, new Vector2(0, 1), new Vector2(BoardW - BoardPad * 2f, 1),
                new Vector2(BoardPad, -38f));
            rule.pivot = new Vector2(0, 0.5f);
            var ri = rule.gameObject.AddComponent<Image>();
            ri.color = new Color(UITheme.Cyan[3].r, UITheme.Cyan[3].g, UITheme.Cyan[3].b, 0.32f);
            ri.raycastTarget = false;

            board.Body = NewRect("Body", board.Root);
            board.Body.anchorMin = new Vector2(0, 1); board.Body.anchorMax = new Vector2(1, 1);
            board.Body.pivot = new Vector2(0.5f, 1);
            board.Body.sizeDelta = new Vector2(-BoardPad * 2f, 0);
            board.Body.anchoredPosition = new Vector2(0, -48f);
            return board;
        }

        /// <summary>Five stars whose lit halves can be re-scored every frame — the star gate's
        /// row (2026-08-25) with the fills kept, so the standing can CLIMB into them instead
        /// of appearing already climbed.</summary>
        private Image[] LiveStarRow(RectTransform parent, Vector2 anchor, Vector2 pos, float px,
            float gap, Color lit, Color socket)
        {
            float pitch = px + gap;
            var row = NewRect("LiveStars", parent);
            Place(row, anchor, new Vector2(BarRating.MaxStars * pitch - gap, px), pos);
            var art = ItemArt.Load("star");
            var fills = new Image[BarRating.MaxStars];
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var cell = NewRect("S" + i, row);
                Place(cell, new Vector2(0, 0.5f), new Vector2(px, px), new Vector2(i * pitch, 0));
                cell.pivot = new Vector2(0, 0.5f);
                var back = cell.gameObject.AddComponent<Image>();
                back.sprite = art; back.color = socket;
                back.preserveAspect = true; back.raycastTarget = false;
                var over = NewRect("F", cell);
                Stretch(over, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var oi = over.gameObject.AddComponent<Image>();
                oi.sprite = art; oi.color = lit;
                oi.preserveAspect = true; oi.raycastTarget = false;
                oi.type = Image.Type.Filled;
                oi.fillMethod = Image.FillMethod.Horizontal;
                oi.fillOrigin = (int)Image.OriginHorizontal.Left;
                oi.fillAmount = 0f;
                fills[i] = oi;
            }
            return fills;
        }

        private static void SetStars(Image[] fills, double stars)
        {
            if (fills == null) return;
            for (int i = 0; i < fills.Length; i++)
                if (fills[i] != null) fills[i].fillAmount = Mathf.Clamp01((float)stars - i);
        }

        private void FillWeekBoard(TycoonRun run)
        {
            if (_weekBoard == null) return;
            var body = _weekBoard.Body;
            foreach (Transform old in body) Destroy(old.gameObject);

            int week = BarCalendar.WeekOf(run.Day);
            _weekBoard.Reading.text = week.ToString("00");

            var names = BarCalendar.WeekColumns;
            float y = 0f;
            int weekNet = 0;
            for (int i = 0; i < names.Length; i++)
            {
                bool closed = i >= BarCalendar.OpenNights;
                int day = closed ? 0 : BarCalendar.DayOf(week, (BarNight)i);
                bool tonight = !closed && day == run.Day;
                bool past = !closed && day < run.Day;
                var book = past ? BookFor(run, day) : null;
                bool scored = tonight || book != null;
                double stars = tonight ? run.TonightStars : book != null ? book.NightStars : 0;
                int net = tonight ? run.DayIncome - run.DayExpenses
                        : book != null ? book.Net : 0;
                if (scored) weekNet += net;

                var row = NewRect("N" + i, body);
                row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
                row.pivot = new Vector2(0.5f, 1);
                row.sizeDelta = new Vector2(0, WeekRowH);
                row.anchoredPosition = new Vector2(0, -y);
                y += WeekRowH;

                // TONIGHT IS THE LIT ROW. The marquee lights the night being played with a
                // tube under its name; the record lights it by standing it on its own warm
                // plate, which is the same idea at the size a row can carry.
                if (tonight)
                {
                    var lit = NewRect("Lit", row);
                    Stretch(lit, Vector2.zero, Vector2.one, new Vector2(-8, 2), new Vector2(8, -2));
                    var li = lit.gameObject.AddComponent<Image>();
                    li.sprite = ChromeArt.Card();
                    li.type = Image.Type.Sliced;
                    li.color = new Color(UITheme.Amber[1].r, UITheme.Amber[1].g,
                                         UITheme.Amber[1].b, 0.55f);
                    li.raycastTarget = false;
                }

                var name = NewText("D", row, _display, 16, TextAnchor.MiddleLeft,
                    closed ? UITheme.Night[4]
                    : tonight ? UITheme.Amber[4]
                    : past ? UITheme.Cream[3] : UITheme.Cream[1]);
                Place(name.rectTransform, new Vector2(0, 0.5f), new Vector2(58, 20),
                    new Vector2(4, 0));
                name.rectTransform.pivot = new Vector2(0, 0.5f);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                name.text = names[i];

                // SATURDAY IS PROMISED BEFORE IT ARRIVES (BarCalendar.VipNight): the night a
                // name comes wears the star every week, whether or not a beat is booked —
                // exactly as the top bar's marquee has since 2026-08-14.
                if (!closed && (BarNight)i == BarCalendar.VipNight)
                {
                    var vip = NewRect("Vip", row);
                    Place(vip, new Vector2(0, 0.5f), new Vector2(13, 13), new Vector2(62, 0));
                    vip.pivot = new Vector2(0, 0.5f);
                    var vi = vip.gameObject.AddComponent<Image>();
                    vi.sprite = ChromeArt.Mark("star");
                    vi.preserveAspect = true; vi.raycastTarget = false;
                    var m = UITheme.Magenta[4];
                    vi.color = tonight ? m : new Color(m.r, m.g, m.b, 0.55f);
                }

                if (closed)
                {
                    // The shutter, the marquee's own sign for a night the bar does not open.
                    for (int sl = 0; sl < 2; sl++)
                    {
                        var slat = NewRect("Shut" + sl, row);
                        Place(slat, new Vector2(0, 0.5f), new Vector2(44, 3),
                            new Vector2(WeekStarsX, 5f - sl * 10f));
                        slat.pivot = new Vector2(0, 0.5f);
                        var si = slat.gameObject.AddComponent<Image>();
                        si.color = UITheme.Night[3]; si.raycastTarget = false;
                    }
                    var shut = NewText("Off", row, _body, 16, TextAnchor.MiddleRight,
                        UITheme.Night[4]);
                    Place(shut.rectTransform, new Vector2(1, 0.5f), new Vector2(120, 20),
                        new Vector2(-4, 0));
                    shut.rectTransform.pivot = new Vector2(1, 0.5f);
                    shut.horizontalOverflow = HorizontalWrapMode.Overflow;
                    shut.text = "CLOSED";
                    continue;
                }

                // The night's stars, on the ruler every star in this game is drawn on. A
                // night not yet worked shows the five EMPTY sockets — the row is the same
                // length whatever happens, so the week reads as a ladder being filled in.
                var stars5 = StarRow(row, new Vector2(0, 0.5f), new Vector2(WeekStarsX, 0), 14f,
                    scored ? stars : 0,
                    tonight ? UITheme.Amber[4] : UITheme.Amber[3],
                    new Color(1f, 1f, 1f, scored ? 0.16f : 0.09f));
                stars5.pivot = new Vector2(0, 0.5f);

                if (!scored) continue;
                var money = NewText("M", row, _display, 16, TextAnchor.MiddleRight,
                    net >= 0 ? UITheme.Lime[4] : UITheme.ViceRed[4]);
                Place(money.rectTransform, new Vector2(1, 0.5f), new Vector2(120, 20),
                    new Vector2(-4, 0));
                money.rectTransform.pivot = new Vector2(1, 0.5f);
                money.horizontalOverflow = HorizontalWrapMode.Overflow;
                money.text = (net >= 0 ? "+$" : "-$") + Mathf.Abs(net);
            }

            // The week's own subtotal, which is the one number a week of receipts is for.
            y += 8f;
            var foot = NewRect("Foot", body);
            foot.anchorMin = new Vector2(0, 1); foot.anchorMax = new Vector2(1, 1);
            foot.pivot = new Vector2(0.5f, 1);
            foot.sizeDelta = new Vector2(0, 1);
            foot.anchoredPosition = new Vector2(0, -y);
            var fi = foot.gameObject.AddComponent<Image>();
            fi.color = new Color(UITheme.Cream[1].r, UITheme.Cream[1].g, UITheme.Cream[1].b, 0.28f);
            fi.raycastTarget = false;
            y += 10f;

            var label = NewText("WeekLabel", body, _body, 16, TextAnchor.MiddleLeft,
                UITheme.Cream[2]);
            Place(label.rectTransform, new Vector2(0, 1), new Vector2(200, 22), new Vector2(4, -y));
            label.rectTransform.pivot = new Vector2(0, 1);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = "THE WEEK SO FAR";

            var total = NewText("WeekNet", body, _display, 16, TextAnchor.MiddleRight,
                weekNet >= 0 ? UITheme.Lime[4] : UITheme.ViceRed[4]);
            Place(total.rectTransform, new Vector2(1, 1), new Vector2(160, 22), new Vector2(-4, -y));
            total.rectTransform.pivot = new Vector2(1, 1);
            total.horizontalOverflow = HorizontalWrapMode.Overflow;
            total.verticalOverflow = VerticalWrapMode.Overflow;
            total.text = (weekNet >= 0 ? "+$" : "-$") + Mathf.Abs(weekNet);
        }

        // ── the bar's own ladder ────────────────────────────────────────────────

        private void FillStandBoard(TycoonRun run)
        {
            if (_standBoard == null) return;
            var body = _standBoard.Body;
            foreach (Transform old in body) Destroy(old.gameObject);
            _standStars = null; _standFill = _standFillGhost = null;
            _standNumber = _standDelta = null;
            _standDeltaChip = _standWasTick = null; _standDeltaArrow = null;

            double was = run.Rating.Average;
            double now = run.StandingAfterTonight;
            _standFrom = was; _standTo = now;
            _standBoard.Reading.text = "NIGHT " + run.Day;

            float y = 4f;
            // THE STANDING, AS BIG AS IT IS IMPORTANT. Five 40px stars is the largest star
            // row in the game, which is correct: this is the number the whole loop is about.
            _standStars = LiveStarRow(body, new Vector2(0.5f, 1), new Vector2(0, -y), 40f, 8f,
                UITheme.Amber[4], new Color(1f, 1f, 1f, 0.13f));
            y += 48f;

            _standNumber = NewText("Now", body, _display, 24, TextAnchor.MiddleCenter,
                UITheme.Amber[4]);
            Place(_standNumber.rectTransform, new Vector2(0.5f, 1), new Vector2(200, 30),
                new Vector2(0, -y));
            _standNumber.rectTransform.pivot = new Vector2(0.5f, 1);
            _standNumber.horizontalOverflow = HorizontalWrapMode.Overflow;
            _standNumber.verticalOverflow = VerticalWrapMode.Overflow;
            _standNumber.text = was.ToString("0.00");
            y += 38f;

            // THE STEP, DRAWN. A tenth of a star is nothing to read as a number and
            // everything to see as a distance: the gauge fills to where the bar stands, a
            // dimmer band shows where it is going (or where it came from, on a bad night),
            // and a tick keeps the old mark so the movement has something to be measured
            // against. This is the author's "bugün ne kadar ilerlediğini göster".
            const float TrackW = 300f, TrackH = 18f;
            var track = NewRect("Track", body);
            Place(track, new Vector2(0.5f, 1), new Vector2(TrackW, TrackH), new Vector2(0, -y));
            track.pivot = new Vector2(0.5f, 1);
            var tube = track.gameObject.AddComponent<Image>();
            tube.sprite = ChromeArt.GaugeTube((int)TrackW, (int)TrackH);
            tube.color = UITheme.Night[2];
            tube.raycastTarget = false;

            var inner = NewRect("Inner", track);
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));

            bool rising = now >= was - 1e-9;
            _standFillGhost = FillBar(inner, rising
                ? new Color(UITheme.Amber[2].r, UITheme.Amber[2].g, UITheme.Amber[2].b, 0.55f)
                : new Color(UITheme.ViceRed[3].r, UITheme.ViceRed[3].g, UITheme.ViceRed[3].b, 0.55f));
            _standFillGhost.fillAmount = (float)(Math.Max(was, now) / BarRating.MaxStars);
            _standFill = FillBar(inner, UITheme.Amber[4]);
            _standFill.fillAmount = (float)(was / BarRating.MaxStars);

            var glass = NewRect("Glass", track);
            Stretch(glass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gi = glass.gameObject.AddComponent<Image>();
            gi.sprite = ChromeArt.GaugeGlass((int)TrackW, (int)TrackH, BarRating.MaxStars);
            gi.raycastTarget = false;

            // Where it stood when the doors opened tonight.
            _standWasTick = NewRect("Was", track);
            Place(_standWasTick, new Vector2(0, 1), new Vector2(2, TrackH + 8f),
                new Vector2((float)(was / BarRating.MaxStars) * (TrackW - 4f) + 2f, 4f));
            _standWasTick.pivot = new Vector2(0.5f, 1);
            var wi = _standWasTick.gameObject.AddComponent<Image>();
            wi.color = UITheme.Cream[4]; wi.raycastTarget = false;

            // And the next rung on the ladder, if there is one left to climb.
            int opens;
            double rung = NextRung(run, now, out opens);
            if (!double.IsNaN(rung) && rung <= BarRating.MaxStars)
            {
                var notch = NewRect("Rung", track);
                Place(notch, new Vector2(0, 1), new Vector2(2, TrackH + 6f),
                    new Vector2((float)(rung / BarRating.MaxStars) * (TrackW - 4f) + 2f, 3f));
                notch.pivot = new Vector2(0.5f, 1);
                var ni = notch.gameObject.AddComponent<Image>();
                ni.color = UITheme.Cyan[3]; ni.raycastTarget = false;
            }
            y += TrackH + 14f;

            // WAS ... and the movement, in a chip of its own colour.
            var wasLine = NewText("WasLine", body, _body, 16, TextAnchor.MiddleLeft,
                UITheme.Cream[2]);
            Place(wasLine.rectTransform, new Vector2(0, 1), new Vector2(160, 22),
                new Vector2(4, -y));
            wasLine.rectTransform.pivot = new Vector2(0, 1);
            wasLine.horizontalOverflow = HorizontalWrapMode.Overflow;
            wasLine.text = "WAS " + was.ToString("0.00");

            double step = now - was;
            var chipInk = Math.Abs(step) < 0.005 ? UITheme.Cream[2]
                : step > 0 ? UITheme.Lime[4] : UITheme.ViceRed[4];
            _standDeltaChip = NewRect("Step", body);
            Place(_standDeltaChip, new Vector2(1, 1), new Vector2(132, 26), new Vector2(-2, -y + 2f));
            _standDeltaChip.pivot = new Vector2(1, 1);
            var chip = _standDeltaChip.gameObject.AddComponent<Image>();
            chip.sprite = ChromeArt.Card();
            chip.type = Image.Type.Sliced;
            chip.color = new Color(chipInk.r * 0.32f, chipInk.g * 0.32f, chipInk.b * 0.32f, 0.9f);
            chip.raycastTarget = false;

            if (Math.Abs(step) >= 0.005)
            {
                var arrow = NewRect("Arrow", _standDeltaChip);
                Place(arrow, new Vector2(0, 0.5f), new Vector2(14, 14), new Vector2(10, 0));
                arrow.pivot = new Vector2(0, 0.5f);
                arrow.localRotation = Quaternion.Euler(0, 0, step > 0 ? 0f : 180f);
                _standDeltaArrow = arrow.gameObject.AddComponent<Image>();
                _standDeltaArrow.sprite = ChromeArt.Mark("rise");
                _standDeltaArrow.preserveAspect = true;
                _standDeltaArrow.raycastTarget = false;
                _standDeltaArrow.color = chipInk;
            }
            _standDelta = NewText("StepText", _standDeltaChip, _display, 16, TextAnchor.MiddleRight,
                chipInk);
            Place(_standDelta.rectTransform, new Vector2(1, 0.5f), new Vector2(104, 20),
                new Vector2(-8, 0));
            _standDelta.rectTransform.pivot = new Vector2(1, 0.5f);
            _standDelta.horizontalOverflow = HorizontalWrapMode.Overflow;
            _standDelta.verticalOverflow = VerticalWrapMode.Overflow;
            _standDelta.text = Math.Abs(step) < 0.005 ? "HELD"
                : (step > 0 ? "+" : "-") + Math.Abs(step).ToString("0.00");
            _standDeltaChip.gameObject.SetActive(false);   // it lands when the climb does
            y += 34f;

            var rule = NewRect("Rule2", body);
            rule.anchorMin = new Vector2(0, 1); rule.anchorMax = new Vector2(1, 1);
            rule.pivot = new Vector2(0.5f, 1);
            rule.sizeDelta = new Vector2(0, 1);
            rule.anchoredPosition = new Vector2(0, -y);
            var rui = rule.gameObject.AddComponent<Image>();
            rui.color = new Color(UITheme.Cream[1].r, UITheme.Cream[1].g, UITheme.Cream[1].b, 0.28f);
            rui.raycastTarget = false;
            y += 12f;

            // The three readings that explain the step: what tonight was worth, what the
            // bar is allowed to be worth, and who that has drawn for tomorrow.
            y = StandRow(y, "TONIGHT", run.TonightStars.ToString("0.0"), UITheme.Amber[4], true);
            double ceiling = run.StarCeiling;
            bool capped = run.TonightStars >= ceiling - 1e-9
                          && BarRating.ExactStarsFor(run.Floor.AverageSatisfaction) > ceiling + 1e-9;
            y = StandRow(y, "CEILING", ceiling.ToString("0.0"),
                capped ? UITheme.ViceRed[4] : UITheme.Cream[3], true);
            y = StandRow(y, "TOMORROW", CrowdName(run.CrowdTomorrow), UITheme.Cyan[3], false);

            y += 6f;
            string note = capped
                ? "THE ROOM WENT HIGHER THAN THE BAR IS FITTED FOR — BUY THE FITTINGS"
                : !double.IsNaN(rung) && rung <= BarRating.MaxStars
                    ? "NEXT RUNG AT " + rung.ToString("0.0") + " STARS — "
                      + opens + (opens == 1 ? " THING OPENS" : " THINGS OPEN")
                    : "EVERY RUNG ON THE LADDER IS OPEN";
            var foot = NewText("Note", body, _body, 16, TextAnchor.UpperLeft,
                capped ? UITheme.ViceRed[3] : UITheme.Cream[2]);
            foot.rectTransform.anchorMin = new Vector2(0, 1);
            foot.rectTransform.anchorMax = new Vector2(1, 1);
            foot.rectTransform.pivot = new Vector2(0.5f, 1);
            foot.rectTransform.sizeDelta = new Vector2(-8, 44);
            foot.rectTransform.anchoredPosition = new Vector2(0, -y);
            foot.horizontalOverflow = HorizontalWrapMode.Wrap;
            foot.verticalOverflow = VerticalWrapMode.Overflow;
            foot.text = note;
        }

        private Image FillBar(RectTransform inner, Color colour)
        {
            var rt = NewRect("Fill", inner);
            Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            // WITH A SPRITE, or Type.Filled is ignored and the gauge reads full at nought
            // (measured; see ChromeArt.Solid).
            img.sprite = ChromeArt.Solid();
            img.color = colour;
            img.raycastTarget = false;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            return img;
        }

        /// <summary>One reading on the standing board: a caption left, a figure right, and
        /// the star mark beside the figure when the figure IS stars — the same unit mark the
        /// slip's critics wear, for the same reason.</summary>
        private float StandRow(float y, string label, string value, Color ink, bool inStars)
        {
            var body = _standBoard.Body;
            var row = NewRect("R" + label, body);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.sizeDelta = new Vector2(0, 26);
            row.anchoredPosition = new Vector2(0, -y);

            var cap = NewText("C", row, _body, 16, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(cap.rectTransform, new Vector2(0, 0.5f), new Vector2(160, 22), new Vector2(4, 0));
            cap.rectTransform.pivot = new Vector2(0, 0.5f);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            cap.text = label;

            if (inStars)
            {
                var unit = NewRect("U", row);
                Place(unit, new Vector2(1, 0.5f), new Vector2(13, 13), new Vector2(-60f, 0));
                unit.pivot = new Vector2(1, 0.5f);
                var ui = unit.gameObject.AddComponent<Image>();
                ui.sprite = ChromeArt.Mark("star");
                ui.preserveAspect = true; ui.raycastTarget = false; ui.color = ink;
            }

            var val = NewText("V", row, _display, 16, TextAnchor.MiddleRight, ink);
            Place(val.rectTransform, new Vector2(1, 0.5f), new Vector2(inStars ? 56 : 200, 22),
                new Vector2(-4, 0));
            val.rectTransform.pivot = new Vector2(1, 0.5f);
            val.horizontalOverflow = HorizontalWrapMode.Overflow;
            val.verticalOverflow = VerticalWrapMode.Overflow;
            val.text = value;
            return y + 28f;
        }

        /// <summary>The lowest star gate still shut, and how many things it holds. Read off
        /// the same two questions the dev bench's table asks — a recipe's gate is the run's
        /// own answer, a bottle's is its lock's — so the board cannot promise a rung the
        /// shop then refuses to open.</summary>
        private double NextRung(TycoonRun run, double from, out int opens)
        {
            double best = double.NaN;
            opens = 0;
            foreach (var r in run.AllRecipes)
            {
                bool owned = false;
                foreach (var m in run.MenuRecipes) if (m.Id == r.Id) { owned = true; break; }
                if (owned) continue;
                double gate = run.RecipeStarGate(r);
                if (double.IsNaN(gate) || gate <= from + 1e-9) continue;
                if (double.IsNaN(best) || gate < best) best = gate;
            }
            foreach (var card in run.CatalogueBottles)
            {
                if (card.Info == null || run.Shelf.Find(card.Id) != null) continue;
                double rung = card.Info.Unlock != null
                    ? card.Info.Unlock.StarsWanted
                    : Market.RequiredStars(card.Info.Tier, card.Info.Price);
                if (double.IsNaN(rung) || rung <= from + 1e-9) continue;
                if (double.IsNaN(best) || rung < best) best = rung;
            }
            if (double.IsNaN(best)) return double.NaN;

            foreach (var r in run.AllRecipes)
            {
                bool owned = false;
                foreach (var m in run.MenuRecipes) if (m.Id == r.Id) { owned = true; break; }
                if (!owned && Math.Abs(run.RecipeStarGate(r) - best) < 1e-9) opens++;
            }
            foreach (var card in run.CatalogueBottles)
            {
                if (card.Info == null || run.Shelf.Find(card.Id) != null) continue;
                double rung = card.Info.Unlock != null
                    ? card.Info.Unlock.StarsWanted
                    : Market.RequiredStars(card.Info.Tier, card.Info.Price);
                if (Math.Abs(rung - best) < 1e-9) opens++;
            }
            return best;
        }

        // ── the climb, and the boards arriving ──────────────────────────────────

        /// <summary>Parks both boards off their own edge, ready to be brought in with the
        /// paper. Called before the beats, so nothing is on the screen when the night is
        /// called.</summary>
        private void SetBoardsIn(float k)
        {
            float e = 1f - (1f - k) * (1f - k) * (1f - k);   // out-cubic
            SetBoardIn(_weekBoard, -BoardX, e);
            SetBoardIn(_standBoard, BoardX, e);
        }

        private static void SetBoardIn(NightBoard board, float home, float e)
        {
            if (board == null) return;
            board.Group.alpha = e;
            board.Root.anchoredPosition = new Vector2(
                home + Mathf.Sign(home) * 54f * (1f - e), BoardY);
        }

        private void StartStandingClimb()
        {
            if (_standStars == null) { _standT = -1f; return; }
            if (Motion.Reduced)
            {
                ApplyStanding((float)_standTo);
                if (_standDeltaChip != null) _standDeltaChip.gameObject.SetActive(true);
                _standT = -1f;
                return;
            }
            _standT = 0f;
            ApplyStanding((float)_standFrom);
        }

        private void StepStandingClimb()
        {
            if (_standT < 0f) return;
            _standT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_standT / StandClimb);
            float e = k * k * (3f - 2f * k);
            ApplyStanding(Mathf.Lerp((float)_standFrom, (float)_standTo, e));
            if (k < 1f) return;
            _standT = -1f;
            if (_standDeltaChip != null)
            {
                _standDeltaChip.gameObject.SetActive(true);
                _chipPop = 1f;
            }
            Sfx.Play("click", 0.5f);
        }

        private void ApplyStanding(float stars)
        {
            SetStars(_standStars, stars);
            if (_standFill != null)
                _standFill.fillAmount = stars / BarRating.MaxStars;
            if (_standNumber != null) _standNumber.text = stars.ToString("0.00");
        }

        /// <summary>The chip lands rather than appears — one punch, gone in a fifth of a
        /// second, the same beat the stamp uses at a size a chip can carry.</summary>
        private void StepChipPop()
        {
            if (_chipPop <= 0f || _standDeltaChip == null) return;
            _chipPop = Mathf.Max(0f, _chipPop - Time.unscaledDeltaTime * 5f);
            float s = 1f + 0.35f * _chipPop * _chipPop;
            _standDeltaChip.localScale = new Vector3(s, s, 1f);
        }

        private void ShowDayEnd()
        {
            // Nothing of the shift survives into the books.
            CloseEverySheet();
            var run = Run;
            _dayEndStep = 0;   // the bill first; the market only after CONTINUE

            // THE BEAT IS CLAIMED BEFORE THE REBUILD (2026-08-11, the author: the way out
            // must not be pressable until the slip has landed and the last star with it).
            // It was not: RebuildDayEnd shows CONTINUE while _endBeat is 0, and the beats
            // were only started AFTERWARDS — so the key came up on the first frame of the
            // night's own arrival and simply stayed there through the call, the feed and
            // the whole star run. Claiming beat 1 first is the whole fix; the rebuild then
            // sees a sequence in progress and leaves the key alone.
            _endBeat = 1;
            _endT = 0f;

            _dayEndPanel.gameObject.SetActive(true);
            RebuildDayEnd();

            // THREE BEATS, IN ORDER (2026-08-11). The night is called and the room darkens
            // behind the words; the words go; only then does the paper feed, and slowly.
            // The stars wait for it to LAND — starting them with the slide meant the night's
            // score was already being counted while the slip was still in the air.
            _endStarFrac = (float)(BarRating.ExactStarsFor(run.Floor.AverageSatisfaction)
                                   / BarRating.MaxStars);

            // WHAT TONIGHT HAS TO SAY FOR ITSELF (2026-08-11, the author: the stamp should
            // come down for a personal best too, saying so). Decided once, here, so nothing
            // downstream can change its mind halfway through the drop.
            //
            // The record is measured against the number that will actually ENTER the books:
            // Rating.CloseNight stores the night CAPPED by the fittings and the menu, so
            // comparing tonight's raw stars to a history of capped ones would claim records
            // the ledger then refuses to keep. And it needs a night to beat — the first
            // night of a run is not a personal best, it is the only entry.
            double capped = run.TonightStars;
            _stampKind = _endStarFrac <= 0f ? StampKind.Disgrace
                : run.Rating.NightsClosed > 0 && capped > run.Rating.BestNight + 1e-9
                    ? StampKind.Record
                    : StampKind.None;
            _billHome = _dayEndBill.anchoredPosition;
            if (_lastCallRt != null)
            {
                _lastCallRt.gameObject.SetActive(true);
                _lastCallGroup.alpha = 0f;
            }
            // Parked off the top, opaque: paper out of a till is opaque from its first
            // millimetre, so it is hidden by being ELSEWHERE rather than by being faint.
            _dayEndBill.anchoredPosition = _billHome + new Vector2(0, SlipFeedFrom);
            var billGroup = _dayEndBill.GetComponent<CanvasGroup>();
            if (billGroup != null) billGroup.alpha = 1f;
            EmptyStarRow();
            // The instruments are off their own edges until the paper starts feeding: the
            // night is CALLED on an empty screen, and a board standing there through the
            // call would be the same mistake the CONTINUE key made.
            SetBoardsIn(0f);
            _standT = -1f;
            _chipPop = 0f;
        }

        private void OnDayEndAdvance()
        {
            if (_dayEndStep == 0)
            {
                _dayEndStep = 1;
                Sfx.Play("click", 0.6f);
                RebuildDayEnd();
                // THE SLIP GOES AND THE VAN ARRIVES: the bill leaves to the left, the
                // market comes in from the right, so the two read as one movement through
                // the evening rather than as two screens that happened to follow.
                PlayPanel(_dayEndTablet, new Vector2(180f, 0f), 0.34f);
            }
            else
            {
                // ASK BEFORE THE DOOR SHUTS (2026-08-14, the author: "markette eğer bir şey
                // satın almadan devam ediyorsan veya sepetinde ürün varken devam et diyorsa
                // oyuncu ekranda emin misin diye bir buton çıkmalı").
                //
                // Two ways to lose something here and no way back from either: picks sitting
                // in the basket are thrown away unbought, and a night nobody shopped on is a
                // night of rent for nothing. Both are silent today. The question is asked
                // only when there is something to lose — a bar that bought its stock and
                // emptied its basket is waved straight through, because a confirm on every
                // night is a key you learn to press without reading.
                string worry = ClosingWorry();
                if (worry != null) { ShowClosingAsk(worry); return; }
                // Closing the shop IS the screen going dark: the tablet pulls away and the
                // curtain takes over, so the market never simply vanishes.
                PlayTabletOut();
            }
        }

        /// <summary>What the player is about to lose by closing, or null when nothing is.</summary>
        private string ClosingWorry()
        {
            if (_cart.Count > 0)
                return _cart.Count == 1
                    ? "1 THING IS STILL IN THE BASKET."
                    : _cart.Count + " THINGS ARE STILL IN THE BASKET.";
            var run = Run;
            if (run != null && run.TodaysPurchases.Count == 0)
                return "THE VAN LEAVES EMPTY TONIGHT.";
            return null;
        }

        /// <summary>Brings a panel in from an offset. Reduced motion places it.</summary>
        /// <param name="steady">Feed it at a near-even rate instead of the usual soft
        /// landing. A panel arrives — fast, then settling — but paper is EXTRUDED, at the
        /// speed of the motor pushing it; the ease-out that suits the tablet spends most of
        /// a long duration crawling the last inch, which at 2.6 seconds reads as a fault.</param>
        private void PlayPanel(RectTransform rt, Vector2 from, float dur, bool fade = true,
            bool steady = false)
        {
            SettleSlide();
            if (rt == null) return;
            if (Motion.Reduced) return;
            _slideFade = fade;
            _slideSteady = steady;
            _slideRt = rt;
            _slideGroup = rt.GetComponent<CanvasGroup>();
            if (_slideGroup == null) _slideGroup = rt.gameObject.AddComponent<CanvasGroup>();
            _slideHome = rt.anchoredPosition;
            _slideFrom = from;
            _slideDur = dur;
            _slideT = 0f;
            _slideOut = false;
            rt.anchoredPosition = _slideHome + from;
            _slideGroup.alpha = fade ? 0f : 1f;
        }

        /// <summary>The market pulls away, and the night begins from black behind it.</summary>
        private void PlayTabletOut()
        {
            SettleSlide();
            if (_dayEndTablet == null || Motion.Reduced) { OnOpenTomorrow(); return; }
            _slideRt = _dayEndTablet;
            _slideGroup = _dayEndTablet.GetComponent<CanvasGroup>();
            if (_slideGroup == null) _slideGroup = _dayEndTablet.gameObject.AddComponent<CanvasGroup>();
            _slideHome = _dayEndTablet.anchoredPosition;
            _slideFrom = new Vector2(0f, -220f);
            _slideDur = 0.3f;
            _slideT = 0f;
            _slideOut = true;
        }

        /// <summary>Puts whatever was moving back where it belongs. Any new movement starts
        /// from rest, so an interrupted slide can never become the panel's new home.</summary>
        private void SettleSlide()
        {
            if (_slideRt == null) return;
            _slideRt.anchoredPosition = _slideHome;
            if (_slideGroup != null) _slideGroup.alpha = 1f;
            _slideRt = null; _slideGroup = null;
        }

        /// <summary>
        /// How paper arrives: fed at a near-even rate, then landing on its stop with a
        /// bounce (2026-08-11, the author: at the very bottom it should bounce a little and
        /// settle where it belongs).
        ///
        /// The last seventh of the run is the landing, and it is a rebound UPWARD — a thing
        /// dropping onto a surface comes back off it, it does not sink past it. Two hops,
        /// the second a quarter of the first, both returning exactly to the rest position,
        /// so the settle is a consequence of the curve rather than a correction after it.
        /// </summary>
        private static float PaperLand(float k)
        {
            const float Feed = 0.86f, Hop = 0.035f;
            if (k < Feed) return 1f - Mathf.Pow(1f - k / Feed, 1.35f);
            float u = (k - Feed) / (1f - Feed);
            return 1f - Hop * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * (1f - u);
        }

        private void StepSlide()
        {
            if (_slideRt == null) return;
            _slideT += Time.unscaledDeltaTime;
            float k = _slideDur <= 0f ? 1f : Mathf.Clamp01(_slideT / _slideDur);
            if (_slideOut)
            {
                float e = k * k;                                   // gathers pace away
                _slideRt.anchoredPosition = Vector2.Lerp(_slideHome, _slideHome + _slideFrom, e);
                if (_slideGroup != null) _slideGroup.alpha = 1f - e;
                if (k >= 1f) { SettleSlide(); OnOpenTomorrow(); }
                return;
            }
            // Paper feeds near-even and BOUNCES onto its stop. Everything else keeps the
            // old soft landing.
            float o = _slideSteady ? PaperLand(k)
                : 1f - (1f - k) * (1f - k) * (1f - k);              // lands soft
            _slideRt.anchoredPosition = Vector2.Lerp(_slideHome + _slideFrom, _slideHome, o);
            if (_slideGroup != null)
                _slideGroup.alpha = _slideFade ? Mathf.Clamp01(k * 1.8f) : 1f;
            if (k >= 1f) SettleSlide();
        }

        private void RebuildDayEnd()
        {
            var run = Run;
            _dayEndBill.gameObject.SetActive(_dayEndStep == 0);
            _dayEndTablet.gameObject.SetActive(_dayEndStep == 1);
            // The books have three objects on them; the market has one, and it covers the
            // room. The instruments belong to the night's own page.
            if (_weekBoard != null) _weekBoard.Root.gameObject.SetActive(_dayEndStep == 0);
            if (_standBoard != null) _standBoard.Root.gameObject.SetActive(_dayEndStep == 0);
            if (_dayEndStep == 0) { FillWeekBoard(run); FillStandBoard(run); }
            // NOT UNTIL THE LAST STAR HAS LANDED (2026-08-11, the author). A way out
            // offered while the night is still being counted is a way out taken: the whole
            // point of the drop is that the player watches it, and a button under it is the
            // one thing that can make them look away. StepDayEndBeats shows it.
            // ...but a rebuild AFTER the counting has finished must not take it away again:
            // the beats are over and nothing would ever put it back.
            if (_billNext != null)
                _billNext.gameObject.SetActive(_dayEndStep == 0 && _endBeat == 0);
            _dayEndTitle.text = "LAST CALL — TIME TO ORDER";
            if (_billNextLabel != null) _billNextLabel.text = "GO TO THE ORDER";
            // NO TITLE OVER THE SLIP (2026-08-11, the author: take the yellow LAST CALL —
            // THE BOOKS off the top). The slip already says LAST CALL across its own head in
            // its own ink; a second one in the scrim above it was the same words twice, in a
            // colour belonging to neither. The market keeps its line, because the tablet does
            // not name itself.
            _dayEndTitle.gameObject.SetActive(_dayEndStep == 1);
            // A 136-wide button holds 2 lines of 8 CAPS; the arrow and the parenthetical
            // wrapped to three and pushed themselves out of it.
            _openTomorrowLabel.text = run.Day % 6 == 0 ? "START\nTUESDAY" : "OPEN\nTOMORROW";
            var floor = run.Floor;
            int served = 0, stormed = 0;
            foreach (var visit in floor.Finished)
                if (visit.State == VisitState.StormedOff) stormed++; else served++;
            var cfg = run.Config;

            // The bill: income over expenses, net in bold, then the debt stamp. All the
            // day's line items come straight off the run's itemised book (GDD 24 §7).
            int net = run.DayIncome - run.DayExpenses;
            // (the strike stamp is a ROW now — see the bottom of the slip. What stood here
            // was the last of the one-Text receipt: a rich-text block nobody printed.)

            // RECEIPT v3 (2026-08-10, the author: "tüm satırların uyacağı arka plan ve
            // metin düzeni"). It was one Text with hand-typed dot leaders holding the
            // columns together, which only lines up while every name is short enough —
            // a long drink name pushed its price off the grid and the whole slip leaned.
            // Every line is a ROW now: a rect with the label pinned left and the figure
            // pinned right, so the columns are structural and no string can bend them.
            // The lines are taken from what was POURED (`visit.Served`) and priced at
            // `PaidBase`, so a night where the player misread somebody still adds up.
            foreach (Transform old in _invoiceRows) Destroy(old.gameObject);
            if (_billWhen != null)
                _billWhen.text = CalendarFor(run.Day) + "  ·  " + CrowdName(run.CrowdToday);
            float y = 0f;

            // THE NIGHT IN ONE LOOK (2026-08-10, the author: "az ama öz" — less type, only
            // what has to be known and SEEN). The itemised drink list is gone: it was the
            // noisiest block on the slip and its answer lives in SALES anyway. What earns
            // its place instead: the night's stars drawn AS stars, and the two people who
            // decided them — the best and the worst of the room, face, score and reason.
            double tonight = BarRating.ExactStarsFor(floor.AverageSatisfaction);
            y = BillStars(y, (float)(tonight / BarRating.MaxStars));
            // (BAR left this line on 2026-08-25: the bar's standing is a whole instrument of
            // its own now, on the right, where it can show the STEP as well as the number.
            // The slip says what tonight was and who was in the room — a receipt's business.)
            y = BillNote(y, "TONIGHT " + tonight.ToString("0.0") + "  ·  "
                            + served + " SERVED  ·  " + stormed + " WALKED", BillQuiet, centred: true);
            y += 8f;

            // The critics: the highest and the lowest word the night produced. One visit
            // gets one row; an empty room gets nothing, not a block of placeholders.
            CustomerVisit high = null, low = null;
            foreach (var v in floor.Finished)
            {
                if (v.State == VisitState.Served && (high == null || v.Satisfaction > high.Satisfaction))
                    high = v;
                if (low == null || v.Satisfaction < low.Satisfaction) low = v;
            }
            if (low == high) low = null;
            if (high != null || low != null)
            {
                y = BillRule(y);
                if (high != null)
                    y = BillCritic(y, high, BillInk);
                if (low != null)
                    y = BillCritic(y, low, BillRed);
            }

            // WHAT CAME IN, WHAT WENT OUT, WHAT IS LEFT (2026-08-11, the author: "gider ve
            // kalan daha açık belli edilsin"). The five figures used to run as one ladder
            // with a rule under it, so the reader had to notice for themselves which of them
            // were takings and which were bills. They are two named blocks now, each with its
            // own subtotal — the shape of a receipt — and only the last two lines are heavy.
            int tookIn = run.DaySales + run.DayTips;
            int paidOut = run.DayRent + run.DayStock + run.DayUpgrades;

            y = BillRule(y);
            y = BillNote(y, "TOOK IN", BillQuiet);
            y = BillRow(y, "SALES", "$" + run.DaySales, BillInk, false, "sales");
            y = BillRow(y, "TIPS", "$" + run.DayTips, BillInk, false, "tips");
            y = BillSub(y, "$" + tookIn, BillInk);

            y += 4f;
            y = BillNote(y, "PAID OUT", BillQuiet);
            y = BillRow(y, "RENT", "-$" + run.DayRent, BillRed, false, "rent");
            y = BillRow(y, "STOCK", "-$" + run.DayStock, BillRed, false, "stock");
            y = BillRow(y, "SHOP", "-$" + run.DayUpgrades, BillRed, false, "shop");
            y = BillSub(y, "-$" + paidOut, BillRed);

            y += 4f;
            y = BillRule(y);
            y = BillRow(y, "NET", (net >= 0 ? "+$" : "-$") + Math.Abs(net),
                        net >= 0 ? BillInk : BillRed, true, "net");
            y = BillRow(y, "TILL", (run.Money < 0 ? "-$" + (-run.Money) : "$" + run.Money),
                        run.Money < 0 ? BillRed : BillInk, true, "till");
            if (run.Ledger.DebtStrikes > 0)
            {
                y += 6f;
                y = BillNote(y, "IN THE RED — STRIKE " + run.Ledger.DebtStrikes
                                + "/" + DayLedger.StrikesToClose, BillRed);
                if (run.Ledger.DebtStrikes == DayLedger.StrikesToClose - 1)
                    y = BillNote(y, "one more red day closes the bar", BillRed);
            }

            FitBillToPaper(y);

            // The sheet is the ROLL's size; the print is the night's. What varies is how
            // much blank stock is left above the foot tear — which is how receipts work.
            if (_billNext != null)
                _billNext.anchoredPosition = new Vector2(0, -(BillH * 0.5f + 34f));

            // The tablet.
            foreach (Transform child in _offerRow) Destroy(child.gameObject);
            // (the account line counts too — see RunTheTill)
            // Tonight's fitting, said ONCE. It used to appear in five places — a band, a
            // rail note, the stool's tip, the glassware tip and a toast — and the author
            // still met it as a surprise, because none of the five was beside the control
            // it governed. It sits at the end of the department bar now, with a lamp.
            bool room = run.CanFitTonight && !CartHasFitting();
            if (_fittingNote != null)
            {
                _fittingNote.text = room ? "1 UPGRADE TONIGHT" : "UPGRADE USED";
                _fittingNote.color = room ? ShopViceDeep : ShopCost;
            }
            if (_fittingLamp != null) _fittingLamp.color = room ? ShopViceLit : ShopCost;
            for (int i = 0; i < _shopTabKeys.Length; i++)
            {
                bool on = i == _shopTab;
                _shopTabKeys[i].sprite = null;
                _shopTabKeys[i].color = on ? ShopViceDeep : ShopPaper;
                var key = (RectTransform)_shopTabKeys[i].transform;
                key.sizeDelta = new Vector2(TabKeyW, on ? TabLiveH : TabRestH);
                if (_shopTabLits[i] != null) _shopTabLits[i].enabled = on;
            }

            // The basket SHOWS what is in it (2026-08-11, the author: "sepetteki font
            // okunmuyor ... ürünlerin ikonu da gözükmeli ... üstüne basınca çıkarılabilmeli").
            // It used to be four names set at 8 in a 312-wide box with "+2 more" under them,
            // which is the whole failure in one line: too small to read, and everything past
            // the fourth thing simply gone. The foot is the basket now, and a picked line is
            // a chip you can see and press.
            if (_cartHeadLabel != null)
                _cartHeadLabel.text = _cart.Count == 0 ? "BASKET" : $"BASKET ({_cart.Count})";
            RebuildBasket();
            if (_cartTotal != null)
                _cartTotal.text = _cart.Count == 0 ? "" : "$" + CartTotal();
            // "TOTAL" with nothing after it is a label for a number that is not there.
            if (_cartTotalLabel != null) _cartTotalLabel.enabled = _cart.Count > 0;
            if (_checkoutLabel != null)
                if (_checkoutUntil < 0f)
                    _checkoutLabel.text = _cart.Count == 0 ? "NOTHING PICKED" : "PLACE ORDER";
            if (_osClock != null) _osClock.text = $"DAY {run.Day}";

            if (_dayEndStep == 0) return;   // the bill step shows no shop at all
            if (_shopTab == 0)
            {
                // RESTOCK. One band, not two: "everything at once" and "bottle by bottle"
                // were one errand split down the middle for no reason.
                _cardTarget = ShopSection("THE WELL");
                int restock = run.Shelf.RefillCost(cfg.RefillPricePerCapacity);
                var all = new TileSpec
                {
                    Name = "Restock the Whole Well",
                    Meta = "Every bottle to the brim",
                    // A CRATE, not the department icon it was borrowing — the errand
                    // and the tab it lives under were drawing the same thing.
                    Art = ItemArt.Load("sh_p_crate") ?? ItemArt.Load("sh_i_restock"),
                    Identity = "RESTOCK THE WHOLE WELL",
                    MetaLine = "Delivered before you open",
                    Body = "Fills every bottle behind the bar. $"
                           + cfg.RefillPricePerCapacity + " per measure.",
                };
                if (restock > 0)
                {
                    all.BuffA = new Buff(BuffKind.Cost, "$" + cfg.RefillPricePerCapacity
                        + " a measure · " + restock + " to fill the shelf");
                    all.BuffB = new Buff(BuffKind.Gain,
                        "Covers every bottle below — you cannot need both");
                    DressBuyable(all, restock, "restock:all", false, () => run.RefillShelf());
                }
                else
                {
                    all.State = TileState.Held;
                    all.Word = "FULL";
                    all.BuffA = new Buff(BuffKind.Gain, "Nothing to pour away — every bottle is at the brim.");
                }
                AddTile(all);

                // WHAT NEEDS POURING COMES FIRST (the author). A restock page whose top row
                // is six full bottles makes the player scroll to find the errand they came
                // for; sorting by what is missing puts the emptiest bottle where the eye
                // already is. Ties keep the shelf's own order, so the page does not
                // reshuffle under the pointer as levels change.
                var shelf = new List<ShelfBottle>(run.Shelf.Bottles);
                shelf.Sort((x, y) =>
                {
                    double mx = x.Capacity - x.Remaining, my = y.Capacity - y.Remaining;
                    return my.CompareTo(mx);
                });
                // THE WHOLE WELL COVERS EVERY BOTTLE IN IT (the author). Both could sit in
                // the same order, and the player paid twice for the same measure: the
                // restock-all tops up every bottle, so a per-bottle refill picked beside it
                // buys nothing. The tile says so instead of taking the money — and picking
                // the whole well throws the singles back out of the order, because the
                // basket is the place where "you already have this" has to be true.
                bool wellOrdered = InCart("restock:all") || _justOrdered.Contains("restock:all");
                if (InCart("restock:all"))
                    for (int i = _cart.Count - 1; i >= 0; i--)
                        if (_cart[i].Key != null && _cart[i].Key.StartsWith("refill:"))
                            _cart.RemoveAt(i);

                foreach (var b in shelf)
                {
                    var bottle = b;
                    int cost = (int)Math.Ceiling((bottle.Capacity - bottle.Remaining)
                        * cfg.RefillPricePerCapacity);
                    var spec = new TileSpec
                    {
                        Name = bottle.Ingredient.Name,
                        Art = ItemArt.Bottle(bottle.Ingredient),
                        // The one fact this department exists to show, and it was line 5 or 6
                        // of a 3-line box — i.e. never once rendered. It is a bar now.
                        StockFrac = bottle.Capacity > 0
                            ? (float)(bottle.Remaining / bottle.Capacity) : 0f,
                    };
                    DescribeBottle(spec, bottle.Ingredient, bottle);
                    if (cost > 0 && !wellOrdered)
                        DressBuyable(spec, cost, "refill:" + bottle.Ingredient.Id, false,
                            () => run.RefillBottle(bottle.Ingredient.Id));
                    else if (cost > 0)
                    {
                        spec.State = TileState.Held;
                        spec.Word = "IN";                       // 2 CAPS, 26.5 in a 66 slot
                        spec.BuffA = new Buff(BuffKind.Gain,
                            "Covered by the whole-well order — no need to buy it twice");
                    }
                    else { spec.State = TileState.Held; spec.Word = "FULL"; }
                    AddTile(spec);
                }
            }
            else if (_shopTab == 1 || _shopTab == 2)
            {
                // ONE LOOP, TWO AISLES. The board is rolled whole by Core; which half of it
                // a bottle belongs to is a question about the bottle, not about the roll.
                bool booze = _shopTab == 1;
                _cardTarget = ShopSection(booze ? "TONIGHT'S BOARD" : "THE MIXER BOARD");
                _liquorHead = _cardTarget; _kegHead = null; _garnishHead = null;
                bool anyKeg = false, anyGarnish = false;
                for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < run.MarketOffers.Count; i++)
                {
                    int index = i;
                    var offer = run.MarketOffers[i];
                    var card = offer.Bottle;
                    if (IngredientCategories.IsAlcoholic(card.Info?.Category, card.Type) != booze)
                        continue;
                    // A keg is not a bottle — 24 measures against 6, and the only beer drink
                    // on the book takes no ratio bands at all — so it gets its own aisle sign
                    // rather than standing unlabelled in a row of spirits. Same for the two
                    // garnishes, which are not mixers.
                    bool second = booze ? card.Type == IngredientType.Beer
                                        : card.Type == IngredientType.Garnish;
                    if ((pass == 1) != second) continue;
                    if (pass == 1 && booze && !anyKeg)
                    { anyKeg = true; _cardTarget = ShopSection("ON TAP — THE KEGS"); _kegHead = _cardTarget; }
                    if (pass == 1 && !booze && !anyGarnish)
                    { anyGarnish = true; _cardTarget = ShopSection("THE GARNISH TRAY"); _garnishHead = _cardTarget; }
                    var spec = new TileSpec
                    {
                        Name = offer.Bottle.Name,
                        Art = ItemArt.Bottle(offer.Bottle),
                    };
                    DescribeBottle(spec, offer.Bottle, null);
                    // "New stock" is a fact about the offer, not a prefix on the name —
                    // the old "+ " and "↑ " spent two cells drawing literally nothing,
                    // because neither glyph is in any of the three installed faces.
                    if (offer.IsNewStock)
                        spec.MetaLine = "New on the board tonight · " + spec.MetaLine;
                    if (offer.Sold) { spec.State = TileState.Ordered; spec.Word = "SOLD"; }
                    else DressBuyable(spec, offer.Price, "brand:" + offer.Bottle.Id, false,
                        () => run.BuyBrand(index));
                    AddTile(spec);
                }

                // THE LOCK BELONGS TO THE AISLE, NOT TO THE DEPARTMENT (2026-08-10, the
                // author). One crate at the foot of the tab said "more is coming" without
                // saying WHERE, so a player looking at a finished keg aisle had to guess
                // whether the news was about kegs or about spirits. Each aisle answers for
                // its own shelf now, and an aisle with nothing behind a star says nothing.
                SectionGate(run, booze
                    ? (System.Func<IngredientCard, bool>)(c => c.Type != IngredientType.Beer)
                    : (c => c.Type != IngredientType.Garnish), booze,
                    booze ? "bottle" : "mixer", _liquorHead);
                // AN AISLE THAT IS ALL LOCK STILL NEEDS ITS SIGN (2026-08-14). These two
                // sections were only ever created while drawing an OFFER, so an aisle whose
                // whole shelf is still behind a star had no header for its crate to stand
                // under — and SectionGate returns on a null grid. Harmless while every
                // garnish was for sale on night one; live the moment mint (3.0 stars) and
                // the olives (4.0) moved onto the ladder, because below three stars no
                // garnish is for sale, the mixer crate excludes garnishes by design, and
                // the two of them would have been counted by nothing at all. That is the
                // exact silence the ladder was built to end, one aisle further down.
                var kegHead = booze
                    ? AisleSign(run, _kegHead, c => c.Type == IngredientType.Beer, true,
                        "ON TAP — THE KEGS")
                    : null;
                if (kegHead != null)
                    SectionGate(run, c => c.Type == IngredientType.Beer, true, "keg", kegHead);
                var garnishHead = !booze
                    ? AisleSign(run, _garnishHead, c => c.Type == IngredientType.Garnish, false,
                        "THE GARNISH TRAY")
                    : null;
                if (garnishHead != null)
                    SectionGate(run, c => c.Type == IngredientType.Garnish, false, "garnish", garnishHead);
            }
            else if (_shopTab == 3)
            {
                _cardTarget = ShopSection("THE RECIPE BOOK");
                // LOWEST GATE FIRST (the author). The book is a ladder — what opens next
                // is the only thing on it the player can act on — and it was listing in
                // catalogue order, so the drink three stars away sat above the one that
                // unseals tonight. Ties keep the catalogue's order.
                var book = new List<RecipeDefinition>(run.LockedRecipes);
                // OrderBy, not Sort (audit 2026-08-11): List.Sort is introsort and NOT
                // stable, so the big tie groups reshuffled between rebuilds while the
                // comment above promised catalogue order. LINQ OrderBy is stable.
                book = book.OrderBy(run.RecipeStarGate).ToList();
                foreach (var recipe in book)
                {
                    var r = recipe;
                    // ASK THE LOCK; DO NOT RE-DERIVE IT (GDD 26 §12.2 step 4). This compared
                    // the rating to a rank table itself and wrote its own two sentences —
                    // which meant a page locked behind anything else, a person for instance,
                    // would have been drawn as though it were waiting for stars. The lock
                    // says what it wants and the crate prints that.
                    var lockedBy = run.RecipeUnlock(r);
                    if (!lockedBy.MetBy(run))
                    {
                        // SEALED, and the name never reaches the tile — that is the whole
                        // mechanic. No art either: the empty well is the tell.
                        double gate = run.RecipeStarGate(r);
                        AddTile(new TileSpec
                        {
                            Name = "Sealed Crate",
                            Meta = "Sealed",
                            Money = gate.ToString("0.0"),
                            GateStars = lockedBy.StarsWanted,
                            State = TileState.Sealed,
                            Identity = "A SEALED CRATE",
                            MetaLine = "The house will not open this one for you yet",
                            Body = lockedBy.Sentence,
                            BuffA = new Buff(BuffKind.Bad, lockedBy.Sentence),
                        });
                        continue;
                    }
                    // WHAT THE SHELF CANNOT POUR, said in the description as well as drawn
                    // on the card (2026-08-10, the author). A recipe you cannot make is still
                    // worth buying — the stock comes later — but that has to be a decision,
                    // not a surprise on the first night it is ordered.
                    var lacking = MissingStyles(r);
                    var spec = new TileSpec
                    {
                        Name = r.Name,
                        Meta = PrepWord(r) + " · " + GlassNameFor(r),
                        Art = DrinkIcon.For(r, _bootstrap.Glassware),
                        ArtH = IconH,
                        Recipe = r,
                        Identity = r.Name.ToUpperInvariant(),
                        MetaLine = PrepWord(r) + " · served in a " + GlassNameFor(r),
                        Body = BandLine(r),
                        BuffA = new Buff(BuffKind.Gain, "On the menu tomorrow — one more drink to sell"),
                        BuffB = lacking.Count == 0
                            ? new Buff(BuffKind.Gain, "Your shelf can pour it tonight")
                            : new Buff(BuffKind.Bad, "Nothing on the shelf pours "
                                + string.Join(" or ", lacking)),
                    };
                    DressBuyable(spec, run.RecipePrice(r), "recipe:" + r.Id, false,
                        () => run.UnlockRecipe(r.Id));
                    AddTile(spec);
                }
            }
            else
            {
                _cardTarget = ShopSection("THE ROOM");
                var stool = new TileSpec
                {
                    Name = "One More Stool",
                    Meta = "Seat " + Math.Min(run.Seats + 1, cfg.MaxSeats) + " of " + cfg.MaxSeats,
                    Art = ItemArt.Load("sh_i_upgrades"),
                    ArtH = IconH,
                    Identity = "ONE MORE STOOL",
                    MetaLine = "The floor · seat " + Math.Min(run.Seats + 1, cfg.MaxSeats)
                               + " of " + cfg.MaxSeats,
                    Body = "One more customer can sit at the bar.",
                    BuffA = new Buff(BuffKind.Gain, "+1 seat · +0.25 stars"),
                    BuffB = new Buff(BuffKind.Bad, "Uses tonight's one upgrade"),
                };
                if (run.Seats >= cfg.MaxSeats) { stool.State = TileState.Held; stool.Word = "MAX"; }
                else DressBuyable(stool, cfg.SeatPrice(run.Seats), "seat", true, () => run.BuySeat());
                AddTile(stool);

                // THE COUNTER. It has been a real, priced, guarded fitting in Core the whole
                // time — BuyCounter, CounterPrice, two steps at $40 and $80, worth up to 0.06
                // satisfaction on EVERY served visit — and it had no tile in any department,
                // so CounterTier was permanently 1 in every run that was not a dev preset and
                // a third of the ambience ceiling was dead weight. Found by counting what the
                // data offers against what the shop can show (2026-08-09).
                var bar = new TileSpec
                {
                    Name = "Resurface the Bar",
                    Meta = "Rung " + run.CounterTier + " of " + cfg.MaxAmbienceTier,
                    Art = ItemArt.Load("sh_p_bar") ?? ItemArt.Load("sh_i2_upgrades"),
                    ArtH = IconH,
                    Identity = "RESURFACE THE BAR",
                    MetaLine = "The room · rung " + run.CounterTier + " of " + cfg.MaxAmbienceTier,
                    Body = "A better bar top makes every customer happier. Even the "
                           + "ones whose drink you get wrong.",
                    BuffA = new Buff(BuffKind.Gain, "+0.03 on every served visit, up to +0.06"),
                    BuffB = new Buff(BuffKind.Bad, "Uses tonight's one upgrade"),
                };
                if (run.CounterTier >= cfg.MaxAmbienceTier)
                { bar.State = TileState.Held; bar.Word = "MAX"; }
                else DressBuyable(bar, cfg.CounterPrice(run.CounterTier), "counter", true,
                    () => run.BuyCounter());
                AddTile(bar);

                foreach (var g in run.Glassware)
                {
                    var glass = g;
                    int tier = run.GlassTier(glass.Id);
                    bool maxed = tier >= TycoonRun.MaxGlassTier;
                    int stepPrice = maxed ? 0 : glass.TierPrices[tier - 1];
                    var spec = new TileSpec
                    {
                        Name = glass.Name,
                        // "{tier-1}★ → {tier}★" spent 4 of its 16 characters drawing nothing.
                        Meta = "Rung " + tier + " of " + TycoonRun.MaxGlassTier,
                        Art = GlassArt.For(glass, Mathf.Min(tier + 1, TycoonRun.MaxGlassTier)).Sprite,
                        ArtH = VesselH,
                        Identity = glass.Name.ToUpperInvariant() + " GLASSWARE",
                        MetaLine = "Rung " + tier + " of " + TycoonRun.MaxGlassTier
                                   + " · " + DrinksServedIn(glass.Id),
                        Body = "Better glasses make every customer happier, and win you stars.",
                        BuffA = new Buff(BuffKind.Gain, "+1 rung on the " + glass.Name.ToLowerInvariant()
                                         + " line · every drink served in one"),
                        BuffB = new Buff(BuffKind.Bad, "Uses tonight's one upgrade"),
                    };
                    if (maxed) { spec.State = TileState.Held; spec.Word = "MAX"; }
                    else DressBuyable(spec, stepPrice, "glass:" + glass.Id, true,
                        () => run.BuyGlassTier(glass.Id));
                    AddTile(spec);
                }

                // THE DRESSING (2026-08-10): the modular room pieces. Cosmetic, so no
                // fitting is spent — a fern changes what the room looks like, not what
                // the bar can do — and each piece names its own slot in the picture.
                // Unlike the sealed recipe crates, a gated piece SHOWS itself: hiding
                // names is the recipe book's mechanic, not the furniture catalogue's.
                if (run.FixtureCatalogue.Count > 0)
                {
                    _cardTarget = ShopSection("THE DRESSING");
                    foreach (var fx in run.FixtureCatalogue)
                    {
                        var f = fx;
                        var spec = new TileSpec
                        {
                            Name = f.Name,
                            Meta = f.IsTap ? f.TapLevel + "-line tower"
                                 : f.Level > 0
                                 ? (f.HasLight ? "House light · mark " : "Fitting · mark ")
                                   + f.Level
                                 : f.HasLight ? "Dressing · lit" : "Dressing",
                            Art = FixtureArt(f.Sprite),
                            ArtH = IconH,
                            Identity = f.Name.ToUpperInvariant(),
                            MetaLine = f.IsTap
                                ? "The counter · " + f.TapLevel
                                  + (f.TapLevel == 1 ? " keg on tap" : " kegs on tap")
                                : f.Level > 0
                                ? RungPlace(f)
                                : f.HasLight
                                ? "The room · carries its own light"
                                : "The room · dressing",
                            Body = f.Flavor,
                            // A TOWER IS NOT DRESSING and its card must not say it is: it
                            // is the only fixture that changes what the bar can sell, and
                            // the whole reason the player is looking at it is the keg they
                            // cannot buy yet.
                            BuffA = new Buff(BuffKind.Gain, f.IsTap
                                ? "Pours " + f.TapLevel + (f.TapLevel == 1 ? " keg" : " kegs")
                                  + " · the market opens the rest"
                                : f.HasLight
                                ? "Stands in the room and lights it"
                                : "Stands in the room from tonight"),
                            BuffB = new Buff(BuffKind.Gain, "Never spends the night's fitting"),
                        };
                        if (run.OwnsFixture(f.Id))
                        {
                            spec.State = TileState.Held;
                            // A rung that has been fitted over is not what is standing in
                            // the room, and saying OURS about the whole ladder would leave
                            // the player unable to tell which one the bar actually runs.
                            spec.Word = f.Level > 0 && f.Level < run.LadderLevel(f.Slot)
                                ? "FITTED" : "OURS";
                        }
                        else if (run.Rating.Average < f.Stars)
                        {
                            spec.State = TileState.Sealed;
                            spec.Money = f.Stars.ToString("0.0");
                            spec.GateStars = f.Stars;
                            spec.BuffA = new Buff(BuffKind.Bad, "Needs a " + f.Stars.ToString("0.0")
                                + "-star room · you are at " + run.Rating.Average.ToString("0.0"));
                        }
                        else if (f.Level > 0 && !run.CanBuyRung(f))
                        {
                            // One rung at a time, and the tile says which rung is missing —
                            // a greyed rung with no reason on it reads as a bug.
                            int next = run.LadderLevel(f.Slot) + 1;
                            spec.State = TileState.Sealed;
                            spec.Money = next.ToString();
                            spec.GateNote = f.IsTap ? "LINE TOWER FIRST" : "LOWER MARK FIRST";
                            spec.BuffA = new Buff(BuffKind.Bad, f.IsTap
                                ? "Fit the " + next + "-line tower first · this bar runs "
                                  + run.TapLevel
                                : "Fit mark " + next + " first · these are mark "
                                  + run.LadderLevel(f.Slot));
                        }
                        else DressBuyable(spec, f.Price, "fx:" + f.Id, false,
                            () => run.BuyFixture(f.Id));
                        AddTile(spec);
                    }
                }
            }

            // WHAT THE NEXT STAR OPENS, IN EVERY DEPARTMENT (the author, 2026-08-10).
            // The board only shows what the room's standing already allows, so anything
            // waiting behind the next rung was invisible and the player could not tell an
            // empty aisle from a FINISHED one. Two aisles carried this tile; now every
            // department that still has something locked carries it, and it always names
            // the NEXT gate — at two stars it is the three-star crate, not the two.
            {
                int locked = 0;
                double next = double.MaxValue;
                string noun = "line", plural = "lines", verb = "the van will not bring you yet";
                // Liquor and mixers answer per AISLE now (SectionGate), because "more is
                // coming" without saying which shelf is a question, not an answer.
                if (_shopTab == 3)
                {
                    noun = "drink"; plural = "drinks"; verb = "the house will not open for you yet";
                    foreach (var r in run.LockedRecipes)
                    {
                        // Locked-ness is the LOCK's answer; the "next at" hint is still a
                        // star, because a page waiting on a person has no number to count
                        // towards and must not pull the hint down to zero.
                        if (run.RecipeUnlock(r).MetBy(run)) continue;
                        locked++;
                        double gate = run.RecipeStarGate(r);
                        if (r.Unlock == null && gate < next) next = gate;
                    }
                }
                else if (_shopTab == 4)
                {
                    noun = "piece"; plural = "pieces"; verb = "the room has not earned yet";
                    foreach (var f in run.FixtureCatalogue)
                    {
                        if (run.OwnsFixture(f.Id) || run.Rating.Average >= f.Stars) continue;
                        // A rung waiting on the rung below it is not waiting on a star, and
                        // counting it here would promise that the next star opens something
                        // no star opens (the same trap StarsWanted answers NaN to).
                        if (f.Level > 0 && !run.CanBuyRung(f)) continue;
                        locked++;
                        if (f.Stars < next) next = f.Stars;
                    }
                }
                if (locked > 0)
                    AddTile(new TileSpec
                    {
                        Name = locked + " more waiting",
                        Money = next.ToString("0.0"),
                        GateStars = next,
                        State = TileState.Sealed,
                        Identity = "MORE AT " + next.ToString("0.0") + " STARS",
                        MetaLine = locked + " " + (locked == 1 ? noun : plural) + " " + verb,
                        Body = "Get " + next.ToString("0.0") + " stars and more of these "
                               + "show up here.",
                        BuffA = new Buff(BuffKind.Bad, "Needs " + next.ToString("0.0")
                                         + " stars · you have " + run.Rating.Average.ToString("0.0")),
                    });
            }

            // A DEPARTMENT WITH NOTHING IN IT SAYS SO. Splitting the board in two means
            // either half can legitimately be empty on a given night — the van simply did
            // not bring any mixers — and a bare aisle sign with nothing under it reads as
            // a bug rather than as an answer.
            if (_cardTarget != null && _cardTarget.childCount == 0)
                AddTile(new TileSpec
                {
                    Name = "Nothing tonight",
                    Meta = "Try again tomorrow",
                    State = TileState.Held,
                    Identity = "NOTHING ON THIS BOARD TONIGHT",
                    MetaLine = "The van brings a different list every night",
                    Body = "What it brings depends on what you already have, and on "
                           + "how many stars you have.",
                });

            // NO REFUNDS (2026-08-11, the author: "iadeyi kaldıralım"). A shelf that
            // could be un-bought at the same close made every purchase provisional: the
            // cheapest way to play the market was to buy the lot, look at the night, and
            // send back whatever the room did not want. An order is an order now, and the
            // basket — which is still free to empty before it is placed — is where the
            // deciding belongs.

            // The reading card is put away with every rebuild: the tiles it was describing
            // have just been destroyed, so a card left up is a description of nothing that
            // the pointer never asked for.
            ShowShopCard(null);

            // The aisle stays where it was left (the author: picking something must not
            // throw you back to the top). Switching department is what resets it.
            Canvas.ForceUpdateCanvases();
            if (_shopScroll != null) _shopScroll.verticalNormalizedPosition = _shopScrollAt;
        }

        /// <summary>The stamp, on the CHIP. It used to be a 160-wide rotated word laid
        /// across a 190 card, printing straight through the name underneath it; the tile
        /// says "sold" with a strip, a plate and a van, so the stamp only has to land.
        /// </summary>
        private System.Collections.IEnumerator StampDrop(RectTransform rt)
        {
            const float dur = 0.16f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                float s = Mathf.Lerp(2.6f, 0.94f, k * k);      // slams down
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            for (float t = 0; t < 0.06f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.06f;
                float s = Mathf.Lerp(0.94f, 1f, k);            // and settles
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// The tablet's own dialog, built once and shown over the device rather than over
        /// the screen: what is at stake is on that device, so the question belongs on it.
        /// Two keys and no third — going back is the safe one and it sits where the eye
        /// lands first.
        /// </summary>
        private void BuildClosingAsk(RectTransform tablet)
        {
            _closingAsk = NewRect("ClosingAsk", tablet);
            Stretch(_closingAsk, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _closingAsk.gameObject.AddComponent<Image>();
            scrim.color = new Color(UITheme.ClubBlue[0].r, UITheme.ClubBlue[0].g, UITheme.ClubBlue[0].b, 0.78f);
            scrim.raycastTarget = true;   // a wall: nothing behind it may be clicked

            // THE 98 MESSAGE BOX (2026-08-19, the author: '"Close the Order?" kısmını da
            // windows 98 tarzına getir'). The question arrives as a little window OF the
            // site: raised panel, the vice fade for a title bar with the question ON it the
            // way that decade titled its dialogs, and two 98 keys. The head sits IN the bar
            // now rather than floating on the paper — a dialog names itself on its chrome.
            var card = NewRect("Card", _closingAsk);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(620, 220), Vector2.zero);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.sprite = ChromeArt.Win98Key();
            cardImg.type = Image.Type.Sliced;
            cardImg.color = ShopPaper;

            var askBar = NewRect("Bar", card);
            Place(askBar, new Vector2(0.5f, 1f), new Vector2(612, 28), new Vector2(0, -4));
            var askBarImg = askBar.gameObject.AddComponent<Image>();
            askBarImg.sprite = ChromeArt.FadeStrip();
            askBarImg.raycastTarget = false;

            var head = NewText("H", askBar, _shop, 16, TextAnchor.MiddleLeft, Color.white);
            Stretch(head.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10, 0), new Vector2(-10, 0));
            head.text = "CLOSE THE ORDER?";

            // THE WARNING IS THE POINT OF THIS BOX, so it is set like one (2026-08-19, the
            // author: kalin ve buyuk yazsin). It was the body face at 12 — a size the pixel
            // faces do not have at all (16 SS0: 8, 16 or 24, nothing else), so the one
            // sentence the dialog exists to make you read was also the softest thing in it.
            // The shop's bold face at 16 is 1x its design size and lands on the grid.
            _closingAskLine = NewText("L", card, _shop, 16, TextAnchor.UpperCenter, ShopInk);
            Place(_closingAskLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(540, 64),
                new Vector2(0, -56));
            _closingAskLine.horizontalOverflow = HorizontalWrapMode.Wrap;
            _closingAskLine.verticalOverflow = VerticalWrapMode.Overflow;

            // Two 98 keys and no third. GO BACK is the safe one and wears the vice blue —
            // on this site the coloured key is the one the house recommends; OPEN ANYWAY
            // stands on the plain face, a step quieter, exactly as able.
            var back = NewRect("Back", card);
            Place(back, new Vector2(0.5f, 0f), new Vector2(240, 44), new Vector2(-132, 34));
            var backImg = back.gameObject.AddComponent<Image>();
            backImg.sprite = ChromeArt.Win98Key();
            backImg.type = Image.Type.Sliced;
            backImg.color = ShopVice;
            var backBtn = back.gameObject.AddComponent<Button>();
            backBtn.targetGraphic = backImg;
            backBtn.onClick.AddListener(() =>
            {
                Sfx.Play("click", 0.6f);
                if (_closingAsk != null) _closingAsk.gameObject.SetActive(false);
            });
            var backLabel = NewText("L", back, _shop, 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backLabel.text = "GO BACK";
            MarkHoverable(back, backImg);
            var backPress = back.gameObject.AddComponent<Win98Press>();
            backPress.Face = backImg;
            backPress.Caption = backLabel.rectTransform;

            var anyway = NewRect("Anyway", card);
            Place(anyway, new Vector2(0.5f, 0f), new Vector2(240, 44), new Vector2(132, 34));
            var anywayImg = anyway.gameObject.AddComponent<Image>();
            anywayImg.sprite = ChromeArt.Win98Key();
            anywayImg.type = Image.Type.Sliced;
            anywayImg.color = ShopPaper;
            var anywayBtn = anyway.gameObject.AddComponent<Button>();
            anywayBtn.targetGraphic = anywayImg;
            anywayBtn.onClick.AddListener(() =>
            {
                if (_closingAsk != null) _closingAsk.gameObject.SetActive(false);
                PlayTabletOut();
            });
            var anywayLabel = NewText("L", anyway, _shop, 16, TextAnchor.MiddleCenter, ShopInk);
            Stretch(anywayLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            anywayLabel.text = "OPEN ANYWAY";
            MarkHoverable(anyway, anywayImg);
            var anywayPress = anyway.gameObject.AddComponent<Win98Press>();
            anywayPress.Face = anywayImg;
            anywayPress.Caption = anywayLabel.rectTransform;

            _closingAsk.SetAsLastSibling();
            _closingAsk.gameObject.SetActive(false);
        }

        private void ShowClosingAsk(string worry)
        {
            if (_closingAsk == null) { PlayTabletOut(); return; }   // never trap the player
            _closingAskLine.text = worry;
            _closingAsk.gameObject.SetActive(true);
            _closingAsk.SetAsLastSibling();
            Sfx.Play("click", 0.5f);
        }
    }
}
