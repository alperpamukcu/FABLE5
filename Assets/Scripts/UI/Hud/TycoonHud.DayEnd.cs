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
    // TycoonHud, part DayEnd: the night's end - its door in, the stars that fall, the stand's climb, the way out and the market.
    // (The show itself lives in TycoonHud.NightShow; its tape, hook and board in Tape, Week and Tomorrow.)
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
        /// THE SLIP COUNTS ITSELF UP (2026-09-22, the author's seventh list: "sayılar 0dan x'e doğru artarak
        /// yükselsin sırayla yukarıdan aşağı ... en sonunda yıldızlar gelecek"). Every figure the slip prints is
        /// registered here as it is built - top to bottom, which is print order - as a closure that draws it at a
        /// share of its value. The beats run them one after another once the paper has landed, and the stars drop
        /// after the last. A click finishes whatever is counting; nothing skips the paper coming in.
        /// </summary>
        private readonly List<System.Action<float>> _billCounts = new List<System.Action<float>>();
        private int _billCountTicked;
        /// <summary>How long one figure takes to count up, and how soon the next one starts after it.</summary>
        private const float CountEach = 0.34f, CountStep = 0.22f;
        /// <summary>A figure's colour: what came in is green, what went out is the deep red (the money only).</summary>
        private static readonly Color BillGain = UITheme.Lime[1], BillLoss = UITheme.ViceRed[1];

        /// <summary>The player asked to see the end of whatever is animating: one press of the mouse.</summary>
        private static bool ClickedToSkip()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
        }

        private void StepDayEndDue()
        {
            if (!_dayEndDue) return;
            // AND THE LAST GLASS IS PUT AWAY FIRST (2026-09-09, the author: "gün sonu son
            // bardak yıkandıktan 1 saniye sonra triggerlanmalı"). The floor being clear is
            // about PEOPLE; the sink is still running when the last of them goes, and the
            // books used to land over a tap the player was watching. One beat after the wash
            // finishes, so the last thing the night does is finish, and then the books come.
            var run = Run;
            bool washing = run != null && (run.SinkBusy || run.GlassesWashing > 0
                                           || run.GlassesInHand > 0)
                           || (stage != null && stage.WaterPlaying);   // and the basin's last loop has played out
            bool clear = FloorIsClear() && !washing;
            if (!clear)
            {
                _dayEndClearAt = -1f;
                // The old escape hatch stands: a night that cannot clear itself still closes.
                if (Time.unscaledTime - _dayEndDueAt < DayEndPatience) return;
            }
            else if (_dayEndClearAt < 0f) { _dayEndClearAt = Time.unscaledTime; return; }
            else if (Time.unscaledTime - _dayEndClearAt < DayEndBeat / LastCall.Game.Ceremony.Pace) return;
            _dayEndDue = false;
            _dayEndClearAt = -1f;
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
            // ONE STAR, ITS OWN COLOUR (2026-09-04). The two colours passed in are read for
            // their ALPHA only — a caller may dim a row, none may repaint the star.
            var socketArt = ItemArt.Star(false, px);
            var litArt = ItemArt.Star(true, px);
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var cell = NewRect("S" + i, row);
                Place(cell, new Vector2(0, 0.5f), new Vector2(px, px), new Vector2(i * pitch, 0));
                cell.pivot = new Vector2(0, 0.5f);
                var back = cell.gameObject.AddComponent<Image>();
                back.sprite = socketArt;
                back.color = new Color(1f, 1f, 1f, socket.a);
                back.preserveAspect = true;
                back.raycastTarget = false;
                float fill = Mathf.Clamp01((float)stars - i);
                if (fill <= 0.001f) continue;
                var over = NewRect("F", cell);
                Stretch(over, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var oi = over.gameObject.AddComponent<Image>();
                oi.sprite = litArt;
                oi.color = new Color(1f, 1f, 1f, lit.a);
                oi.preserveAspect = true;
                oi.raycastTarget = false;
                oi.type = Image.Type.Filled;
                oi.fillMethod = Image.FillMethod.Horizontal;
                oi.fillOrigin = (int)Image.OriginHorizontal.Left;
                oi.fillAmount = fill;
            }
            return row;
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
            _starT += Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace;
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
                    Sfx.Play("star_earn", 0.85f); }
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

        /// <summary>Five stars whose lit halves can be re-scored every frame — the star gate's
        /// row (2026-08-25) with the fills kept, so the standing can CLIMB into them instead
        /// of appearing already climbed.</summary>
        private Image[] LiveStarRow(RectTransform parent, Vector2 anchor, Vector2 pos, float px,
            float gap, Color lit, Color socket)
        {
            float pitch = px + gap;
            var row = NewRect("LiveStars", parent);
            Place(row, anchor, new Vector2(BarRating.MaxStars * pitch - gap, px), pos);
            var socketArt = ItemArt.Star(false, px);
            var litArt = ItemArt.Star(true, px);
            var fills = new Image[BarRating.MaxStars];
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var cell = NewRect("S" + i, row);
                Place(cell, new Vector2(0, 0.5f), new Vector2(px, px), new Vector2(i * pitch, 0));
                cell.pivot = new Vector2(0, 0.5f);
                var back = cell.gameObject.AddComponent<Image>();
                back.sprite = socketArt; back.color = new Color(1f, 1f, 1f, socket.a);
                back.preserveAspect = true; back.raycastTarget = false;
                var over = NewRect("F", cell);
                Stretch(over, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var oi = over.gameObject.AddComponent<Image>();
                oi.sprite = litArt; oi.color = new Color(1f, 1f, 1f, lit.a);
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

        // ── the climb ───────────────────────────────────────────────────────────

        /// <summary>The standing's climb from where it stood (WAS) to where tonight leaves it (NOW). ZERO LONG WHEN IT
        /// HELD (2026-09-28): a climb of nothing is not a beat, so the chip lands at once. Reduced motion places it.</summary>
        private void StartStandingClimb()
        {
            if (_standStars == null) { _standT = -1f; return; }
            if (Motion.Reduced)
            {
                ApplyStanding((float)_standTo);
                if (_standDeltaChip != null) _standDeltaChip.gameObject.SetActive(true);
                _chipPop = 0f;
                _standT = -1f;
                return;
            }
            if (Math.Abs(_standTo - _standFrom) < 0.005)
            {
                ApplyStanding((float)_standTo);
                LandChip();
                _standT = -1f;
                return;
            }
            _standT = 0f;
            ApplyStanding((float)_standFrom);
        }

        private void StepStandingClimb()
        {
            if (_standT < 0f) return;
            _standT += Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace;
            float k = Mathf.Clamp01(_standT / StandClimb);
            float e = k * k * (3f - 2f * k);
            ApplyStanding(Mathf.Lerp((float)_standFrom, (float)_standTo, e));
            if (k < 1f) return;
            _standT = -1f;
            // (the certificate is offered by the show once the chip and the rung's swap have landed - StepClimb)
            LandChip();
        }

        /// <summary>The climb lands: the step's chip pops and the key sounds.</summary>
        private void LandChip()
        {
            if (_standDeltaChip != null)
            {
                _standDeltaChip.gameObject.SetActive(true);
                _chipPop = 1f;
            }
            Sfx.Play("key_press", 0.5f);
        }

        private void ApplyStanding(float stars)
        {
            SetStars(_standStars, stars);
            if (_standFill != null)
                _standFill.fillAmount = stars / BarRating.MaxStars;
            if (_standNumber != null) _standNumber.text = stars.ToString("0.00");
        }

        /// <summary>The chip lands rather than appears — one punch over <see cref="ChipPop"/>, the same beat the stamp
        /// uses at a size a chip can carry. On the ceremonies' pace since 2026-09-28 (it ran raw before).</summary>
        private void StepChipPop()
        {
            if (_chipPop <= 0f || _standDeltaChip == null) return;
            _chipPop = Mathf.Max(0f, _chipPop - Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace / ChipPop);
            float s = 1f + 0.35f * _chipPop * _chipPop;
            _standDeltaChip.localScale = new Vector3(s, s, 1f);
        }

        private void OnDayEndAdvance()
        {
            // THE MARKET IS ALREADY LEAVING (review, 2026-09-27). The pull-away drops the pointer, but Escape still
            // walked in here through it: with nothing to lose it ran PlayTabletOut again, whose SettleSlide put the
            // tablet back home and whole before it left a second time; after OPEN ANYWAY it raised the question again
            // over a market that was closing. The door has been shut - every way to it waits for tomorrow.
            if (_slideOut && _slideRt != null && _slideRt == _dayEndTablet) return;
            if (_dayEndStep == 0)
            {
                _dayEndStep = 1;
                Sfx.Play("key_press", 0.6f);
                // THE SLIP GOES AND THE VAN ARRIVES: the bill leaves to the left, the
                // market comes in from the right, so the two read as one movement through
                // the evening rather than as two screens that happened to follow.
                // ...which, until 2026-09-27, was half true: the rebuild switched the slip and
                // the boards off in the frame of the press and only the tablet moved. The exit
                // is claimed BEFORE the rebuild, so the rebuild keeps them up while they go —
                // and the tablet is still switched on in this same frame, under the press,
                // because the suite reads "CONTINUE gone, basket up" as the press landing.
                StartBillOut();
                RebuildDayEnd();
                PlayPanel(_dayEndTablet, new Vector2(MarketCrossBy, 0f), MarketCross);
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
                //
                // Since the two foot keys became one (2026-09-04) the FOOT can no longer
                // reach here with a full basket — that press buys instead — so the basket
                // branch below now belongs to Escape alone. It stays: Escape is still a way
                // out of the market, and it must not be the cheap way to bin the picks.
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
                return UIText.N("dayend.closing.basket", _cart.Count);
            var run = Run;
            if (run != null && run.TodaysPurchases.Count == 0)
                return UIText.T("dayend.closing.van_empty");
            return null;
        }

        /// <summary>Brings a panel in from an offset, landing soft. Reduced motion places it. (The paper that fed at a
        /// near-even rate is the night's tape now, which unrolls on its own clock - TycoonHud.Tape.)</summary>
        private void PlayPanel(RectTransform rt, Vector2 from, float dur, bool fade = true)
        {
            SettleSlide();
            if (rt == null) return;
            if (Motion.Reduced) return;
            _slideFade = fade;
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
            // The market's own movements end first (2026-09-27): a ghost aisle, a fading rail or
            // a slip still leaving would otherwise be carried off under the curtain half-done.
            SettleMarketMotion();
            SettleSlide();
            if (_dayEndTablet == null || Motion.Reduced) { OnOpenTomorrow(); return; }
            _slideRt = _dayEndTablet;
            _slideGroup = _dayEndTablet.GetComponent<CanvasGroup>();
            if (_slideGroup == null) _slideGroup = _dayEndTablet.gameObject.AddComponent<CanvasGroup>();
            // A TABLET ON ITS WAY OUT TAKES NO PRESSES (2026-09-27). It used to: a tile, OPEN
            // TOMORROW or the question could all still be pressed through the pull-away, on a
            // market that had already been closed. What is LEAVING may drop the pointer; the
            // panel is switched off at the end, so no glow is left holding it. SettleSlide
            // gives it back. (The keyboard's way in, Escape, is turned away in OnDayEndAdvance.)
            _slideGroup.blocksRaycasts = false;
            _slideHome = _dayEndTablet.anchoredPosition;
            _slideFrom = new Vector2(0f, -220f);
            _slideDur = 0.3f;
            _slideT = 0f;
            _slideOut = true;
            Sfx.Play("screen_off", 0.75f);
        }

        /// <summary>Puts whatever was moving back where it belongs. Any new movement starts
        /// from rest, so an interrupted slide can never become the panel's new home.</summary>
        private void SettleSlide()
        {
            if (_slideRt == null) return;
            _slideRt.anchoredPosition = _slideHome;
            if (_slideGroup != null) { _slideGroup.alpha = 1f; _slideGroup.blocksRaycasts = true; }
            _slideRt = null; _slideGroup = null;
        }

        /// <summary>
        /// The night's way out (2026-09-27; the whole night sheet since 2026-09-28), claimed in the frame of the press and
        /// before the rebuild, which reads <see cref="_billOutT"/> to keep the sheet switched on while it goes. The sheet
        /// never takes the pointer - the market is live over it from its first frame. Reduced motion claims nothing, and
        /// the rebuild switches it off exactly as it always did.
        /// </summary>
        private void StartBillOut()
        {
            SettleBillOut();
            if (Motion.Reduced || _nightSheet == null || !_nightSheet.gameObject.activeSelf) return;
            _billOutGroup = _nightGroup;
            _nightSheet.anchoredPosition = Vector2.zero;   // at rest already - the show is over when CONTINUE is up
            _billOutT = 0f;
        }

        /// <summary>The second slot's frame, on the first slot's clock: the night pushed left by the distance the tablet
        /// comes in from the right, on the same out-cubic, fading as the tablet brightens.</summary>
        private void StepBillOut()
        {
            if (_billOutT < 0f) return;
            _billOutT += Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace;
            float k = Motion.Reduced ? 1f : Mathf.Clamp01(_billOutT / MarketCross);
            if (k >= 1f) { SettleBillOut(); return; }
            float e = Tweening.OutCubic(k);
            if (_nightSheet != null)   // on whole units: the night is all pixel face
                _nightSheet.anchoredPosition = new Vector2(-Mathf.Round(MarketCrossBy * e), 0f);
            if (_billOutGroup != null) _billOutGroup.alpha = 1f - Mathf.Clamp01(k * 1.8f);   // the tablet's k*1.8, mirrored
        }

        /// <summary>
        /// The night back AT HOME and whole, and only then switched off - at the end and on every interruption (a quick
        /// close, tomorrow, the next night's books). It never takes the pointer, going or staying.
        /// </summary>
        private void SettleBillOut()
        {
            if (_billOutT < 0f) return;
            _billOutT = -1f;
            bool slipStep = _dayEndStep == 0;
            if (_nightSheet != null)
            {
                _nightSheet.anchoredPosition = Vector2.zero;
                if (_billOutGroup != null) { _billOutGroup.alpha = 1f; _billOutGroup.blocksRaycasts = false; }
                if (!slipStep) _nightSheet.gameObject.SetActive(false);
            }
            _billOutGroup = null;
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
            StepBillOut();   // the second slot, on this same clock (2026-09-27)
            if (_slideRt == null) return;
            _slideT += Time.unscaledDeltaTime * LastCall.Game.Ceremony.Pace;
            float k = _slideDur <= 0f ? 1f : Mathf.Clamp01(_slideT / _slideDur);
            if (_slideOut)
            {
                float e = k * k;                                   // gathers pace away
                _slideRt.anchoredPosition = Vector2.Lerp(_slideHome, _slideHome + _slideFrom, e);
                if (_slideGroup != null) _slideGroup.alpha = 1f - e;
                if (k >= 1f) { SettleSlide(); OnOpenTomorrow(); }
                return;
            }
            float o = 1f - (1f - k) * (1f - k) * (1f - k);          // lands soft
            _slideRt.anchoredPosition = Vector2.Lerp(_slideHome + _slideFrom, _slideHome, o);
            if (_slideGroup != null)
                _slideGroup.alpha = _slideFade ? Mathf.Clamp01(k * 1.8f) : 1f;
            if (k >= 1f) SettleSlide();
        }

        /// <summary>Which aisle of the restock page a shelf line belongs to, and in what
        /// order the aisles run (2026-09-09). Coarser than the cellar's own families — a
        /// shopping list wants four headings, not eleven — but derived from the same fact,
        /// the card's category and type, so the page and the shelf never disagree.</summary>
        private static int RestockAisleOrder(IngredientCard card)
        {
            if (card == null) return 9;
            if (card.Type == IngredientType.Garnish) return 4;
            if (card.Type == IngredientType.Beer) return 1;
            string cat = card.Info?.Category;
            if (cat == IngredientCategories.Juice || cat == IngredientCategories.Mixer
                || card.Type == IngredientType.Bubbly) return 2;
            if (card.Type == IngredientType.Sweet || card.Type == IngredientType.Bitter) return 3;
            return IngredientCategories.IsAlcoholic(card.Info?.Category, card.Type) ? 0 : 2;
        }

        private static string RestockAisleWord(IngredientCard card)
        {
            switch (RestockAisleOrder(card))
            {
                case 0: return UIText.T("dayend.aisle.spirits");
                case 1: return UIText.T("dayend.aisle.beer");
                case 2: return UIText.T("dayend.aisle.mixers");
                case 3: return UIText.T("dayend.aisle.syrups");
                case 4: return UIText.T("dayend.aisle.garnishes");
                default: return UIText.T("dayend.aisle.rest");
            }
        }

        private void RebuildDayEnd()
        {
            var run = Run;
            // ...and while it is LEAVING (2026-09-27) the night is still on the screen: it goes out under the arriving
            // market and is switched off by SettleBillOut.
            bool slipUp = _dayEndStep == 0 || _billOutT >= 0f;
            if (_nightSheet != null && _nightSheet.gameObject.activeSelf != slipUp) _nightSheet.gameObject.SetActive(slipUp);
            _dayEndTablet.gameObject.SetActive(_dayEndStep == 1);
            // NOT UNTIL THE SHOW IS OVER (2026-08-11, the author: a way out offered while the night is still being
            // counted is a way out taken) - and not under the certificate. ShowWayOn is asked every frame after the
            // show and by every rebuild, so a rebuild can neither offer the key early nor take it away again.
            ShowWayOn();
            _dayEndTitle.text = UIText.T("dayend.market.title");
            if (_billNextLabel != null) _billNextLabel.text = UIText.T("dayend.bill.next");
            // NO TITLE OVER THE NIGHT (2026-08-11, the author: take the yellow LAST CALL — THE BOOKS off the top). The
            // tape says MALIBU CLUB across its own head in its own ink; the market keeps its line, because the tablet
            // does not name itself.
            _dayEndTitle.gameObject.SetActive(_dayEndStep == 1);
            var cfg = run.Config;

            // The tablet. A department change lifts the old shelf out into a ghost that slides away
            // (MarketFx, 2026-09-27) — here, while the viewport still has the old department's width.
            if (!LiftAisleIntoGhost())
                foreach (Transform child in _offerRow) Destroy(child.gameObject);
            // (the account line counts too — see RunTheTill)
            // Tonight's fitting, said ONCE. It used to appear in five places — a band, a
            // rail note, the stool's tip, the glassware tip and a toast — and the author
            // still met it as a surprise, because none of the five was beside the control
            // it governed. It sits at the end of the department bar now, with a lamp.
            bool room = run.CanFitTonight && !CartHasFitting();
            if (_fittingNote != null)
            {
                _fittingNote.text = UIText.T(room ? "dayend.fitting.free" : "dayend.fitting.used");
                _fittingNote.color = room ? ShopViceDeep : ShopCost;
            }
            if (_fittingLamp != null) _fittingLamp.color = room ? ShopViceLit : ShopCost;
            // The keys are AIMED here, not drawn (2026-09-27): this runs on every pick, and a height
            // or a colour written here would snap a key that is still rising. StepShopTabKeys draws
            // them — through HoverWarm, so the one under the pointer keeps its open colour.
            for (int i = 0; i < _shopTabKeys.Length; i++)
                if (_shopTabKeys[i] != null) _shopTabKeys[i].sprite = null;
            AimShopTabKeys();

            // The basket SHOWS what is in it (2026-08-11, the author: "sepetteki font
            // okunmuyor ... ürünlerin ikonu da gözükmeli ... üstüne basınca çıkarılabilmeli").
            // It used to be four names set at 8 in a 312-wide box with "+2 more" under them,
            // which is the whole failure in one line: too small to read, and everything past
            // the fourth thing simply gone. The foot is the basket now, and a picked line is
            // a chip you can see and press.
            //
            // BEFORE ANY OF IT IS DRAWN, the whole-well line is re-priced against what the
            // basket now holds (2026-09-04) — the chips, the total and the key are all read
            // off _cart, so a stale price here is a stale price everywhere.
            RepriceWholeWell();
            if (_cartHeadLabel != null)
                _cartHeadLabel.text = _cart.Count == 0 ? UIText.T("dayend.basket.head")
                    : UIText.N("dayend.basket.head_count", _cart.Count);
            RebuildBasket();
            if (_cartTotal != null)
                // ClearCoin, not text="": a figure emptied by hand keeps its coin, and a
                // cleared basket would sit there showing a mark with no number under it.
                if (_cart.Count == 0) ClearCoin(_cartTotal); else CoinFigure(_cartTotal, CartTotal());
            // "TOTAL" with nothing after it is a label for a number that is not there.
            if (_cartTotalLabel != null) _cartTotalLabel.enabled = _cart.Count > 0;
            // WHAT THE NIGHT LEAVES YOU WITH (2026-09-04, the author: "sepete ürün
            // eklendiğinde kalan bakiyeyi göstermeli"). The top bar says what the till HOLDS
            // and the basket said what the order COSTS, and the player was doing the
            // subtraction — on the one screen where getting it wrong is how a bar goes
            // under. It goes red at nothing left: the shop refuses to overspend, so zero is
            // the wall, and a wall you can see is not a refusal you have to discover.
            int leftInTill = run.Money - CartTotal();
            if (_cartLeft != null)
            {
                if (_cart.Count == 0) ClearCoin(_cartLeft); else CoinFigure(_cartLeft, leftInTill, account: true);
                _cartLeft.color = leftInTill > 0 ? Color.white : ShopCost;
            }
            if (_cartLeftLabel != null) _cartLeftLabel.enabled = _cart.Count > 0;
            // The foot key reads the basket for which errand it is on — buy, or open
            // tomorrow — so every rebuild that can have changed the basket re-dresses it.
            RefreshMarketKey();
            if (_osClock != null) _osClock.text = UIText.T("dayend.os_clock", ("day", run.Day));

            if (_dayEndStep == 0) return;   // the bill step shows no shop at all
            // The upgrade screen stands its own rail of shelves left of the aisle (2026-09-13).
            LayOutDecorRail(_shopTab == 4);
            if (_shopTab == 0)
            {
                // RESTOCK. One band, not two: "everything at once" and "bottle by bottle"
                // were one errand split down the middle for no reason.
                _cardTarget = ShopSection(UIText.T("dayend.restock.section"));
                // WHAT THE SHELF IS SHORT, AND WHAT THIS LINE WOULD STILL ADD — two numbers
                // now, and the difference between them is what the basket is already
                // covering bottle by bottle (2026-09-04; see WholeWellPrice).
                int shelfShort = run.Shelf.RefillCost(cfg.RefillPricePerCapacity);
                int restock = WholeWellPrice();
                var all = new TileSpec
                {
                    Name = UIText.T("dayend.restock.well.name"),
                    Meta = UIText.T("dayend.restock.well.meta"),
                    // A CRATE, not the department icon it was borrowing — the errand
                    // and the tab it lives under were drawing the same thing.
                    // IN THE RAIL'S OWN HAND (2026-09-27, the author, pointing at the upgrade rail: "Restock
                    // görseli, paylaştığım görseldeki iconlar. Tekrardan tasarlansın anlattığım tarza göre"):
                    // up_restock is the crate as an up_* pictogram with the house's green badge, 48 at a whole
                    // 2x in the tile like every upgrade tile (Tools/market_icons.py, D).
                    Art = ItemArt.Load("up_restock") ?? ItemArt.Load("sh_p_crate"),
                    Identity = UIText.T("dayend.restock.well.identity"),
                    MetaLine = UIText.T("dayend.restock.well.meta_line"),
                    Body = UIText.T("dayend.restock.well.body",
                           ("price", "$" + cfg.RefillPricePerCapacity(1) + "-"
                                     + cfg.RefillPricePerCapacity(TycoonConfig.MaxStockTier))),
                };
                if (restock > 0)
                {
                    all.BuffA = new Buff(BuffKind.Cost, UIText.T(restock < shelfShort
                            ? "dayend.restock.well.cost_rest"
                            : "dayend.restock.well.cost_all",
                        ("price", "$" + cfg.RefillPricePerCapacity(1) + "-"
                                  + cfg.RefillPricePerCapacity(TycoonConfig.MaxStockTier)),
                        ("total", restock)));
                    all.BuffB = new Buff(BuffKind.Gain,
                        UIText.T("dayend.restock.well.tops_up"));
                    DressBuyable(all, restock, WholeWellKey, false, () => run.RefillShelf());
                }
                else
                {
                    // TWO WAYS TO HAVE NOTHING TO DO, and they are not the same news: a shelf
                    // with nothing missing, and a shelf whose every short bottle is already
                    // in the basket. Either way the crate is not for sale — the author:
                    // "eğer restock edilebilecek ürün yoksa restock alınamamalı".
                    all.State = TileState.Held;
                    all.Word = UIText.T(shelfShort > 0 ? "dayend.restock.word_in" : "dayend.restock.word_full");
                    // 6 CAPS: the state row keeps 44 units for the reading beside it, and
                    // ALL IN BASKET wrapped and lost its second line (measured in play).
                    all.StateWord = shelfShort > 0 ? UIText.T("dayend.restock.state_all_in") : null;
                    all.BuffA = new Buff(BuffKind.Gain, UIText.T(shelfShort > 0
                        ? "dayend.restock.well.all_in_basket"
                        : "dayend.restock.well.at_brim"));
                }
                AddTile(all);

                // WHAT NEEDS POURING COMES FIRST (the author). A restock page whose top row
                // is six full bottles makes the player scroll to find the errand they came
                // for; sorting by what is missing puts the emptiest bottle where the eye
                // already is. Ties keep the shelf's own order, so the page does not
                // reshuffle under the pointer as levels change.
                var shelf = new List<ShelfBottle>(run.Shelf.Bottles);
                // GROUPED, THEN EMPTIEST FIRST (2026-09-09, the author: "markette restock
                // kısmını guruplandır alkoller, meşrubatlar, garnishler vs. gibi"). Thirty-six
                // lines in one run is a wall you have to read to shop; the cellar behind the
                // bar is already sorted into families, so the page that refills it is sorted
                // the same way and in the same order — spirits, beer, mixers, garnishes —
                // with what is emptiest at the head of each family, which is the errand.
                shelf.Sort((x, y) =>
                {
                    int gx = RestockAisleOrder(x.Ingredient), gy = RestockAisleOrder(y.Ingredient);
                    if (gx != gy) return gx.CompareTo(gy);
                    double mx = x.Capacity - x.Remaining, my = y.Capacity - y.Remaining;
                    return my.CompareTo(mx);
                });
                int aisleNow = -1;
                // THE WHOLE WELL COVERS WHAT YOU HAVE NOT PICKED YOURSELF (2026-09-04). It
                // used to cover EVERYTHING and throw the singles back out of the basket to
                // prove it — a line that silently edited the order after the player had made
                // it, and which left the crate quoting a price for measures the basket was
                // paying for twice. The crate is a remainder now, so both can stand in one
                // order and the two numbers add up to exactly one shelf.
                //
                // What still cannot happen is picking a bottle the crate is ALREADY paying
                // for: those tiles say IN. A bottle picked BEFORE the crate keeps its own
                // line and stays takeable — pulling it out simply puts its measures back on
                // the crate, which re-prices on the same rebuild.
                bool wellOrdered = InCart(WholeWellKey) || _justOrdered.Contains(WholeWellKey);

                foreach (var b in shelf)
                {
                    var bottle = b;
                    // Aisles are told apart by their ORDER, never by their word: two languages
                    // may give two aisles one word, and the page must not merge them.
                    int aisle = RestockAisleOrder(bottle.Ingredient);
                    if (aisle != aisleNow) { aisleNow = aisle; ShopSection(RestockAisleWord(bottle.Ingredient)); }
                    int cost = (int)Math.Ceiling((bottle.Capacity - bottle.Remaining)
                        * cfg.RefillPricePerCapacity(bottle.Tier));
                    string key = RefillKey + bottle.Ingredient.Id;
                    var spec = new TileSpec
                    {
                        Name = UIText.Data("bottle", bottle.Ingredient.Id, "name", bottle.Ingredient.Name),
                        Art = ItemArt.Bottle(bottle.Ingredient),
                        Card = bottle.Ingredient,
                        // The one fact this department exists to show, and it was line 5 or 6
                        // of a 3-line box — i.e. never once rendered. It is a bar now.
                        StockFrac = bottle.Capacity > 0
                            ? (float)(bottle.Remaining / bottle.Capacity) : 0f,
                    };
                    DescribeBottle(spec, bottle.Ingredient, bottle);
                    if (cost > 0 && (!wellOrdered || InCart(key)))
                        DressBuyable(spec, cost, key, false,
                            () => run.RefillBottle(bottle.Ingredient.Id));
                    else if (cost > 0)
                    {
                        spec.State = TileState.Held;
                        spec.Word = UIText.T("dayend.restock.word_in");   // 2 CAPS, 26.5 in a 66 slot
                        spec.BuffA = new Buff(BuffKind.Gain,
                            UIText.T("dayend.restock.covered"));
                    }
                    else { spec.State = TileState.Held; spec.Word = UIText.T("dayend.restock.word_full"); }
                    AddTile(spec);
                }
            }
            else if (_shopTab == 1 || _shopTab == 2)
            {
                // ONE LOOP, TWO AISLES. The board is rolled whole by Core; which half of it
                // a bottle belongs to is a question about the bottle, not about the roll.
                bool booze = _shopTab == 1;
                _cardTarget = ShopSection(UIText.T(booze ? "dayend.board.spirits" : "dayend.board.mixers"));
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
                    { anyKeg = true; _cardTarget = ShopSection(UIText.T("dayend.board.kegs")); _kegHead = _cardTarget; }
                    if (pass == 1 && !booze && !anyGarnish)
                    { anyGarnish = true; _cardTarget = ShopSection(UIText.T("dayend.board.garnish")); _garnishHead = _cardTarget; }
                    var spec = new TileSpec
                    {
                        Name = UIText.Data("bottle", offer.Bottle.Id, "name", offer.Bottle.Name),
                        Art = ItemArt.Bottle(offer.Bottle),
                        Card = offer.Bottle,
                        // The rung it stands on, drawn up its left margin (2026-09-04). Only
                        // on the BOARD: the restock aisle sells measures of what the bar
                        // already owns, and a gate on a bottle you are holding is history.
                        RungStars = RungOf(offer.Bottle),
                    };
                    DescribeBottle(spec, offer.Bottle, null);
                    // "New stock" is a fact about the offer, not a prefix on the name —
                    // the old "+ " and "↑ " spent two cells drawing literally nothing,
                    // because neither glyph is in any of the three installed faces.
                    if (offer.IsNewStock)
                        spec.MetaLine = UIText.T("dayend.board.new_stock", ("meta", spec.MetaLine));
                    if (offer.Sold) { spec.State = TileState.Ordered; spec.Word = UIText.T("dayend.board.sold"); }
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
                        UIText.T("dayend.board.kegs"))
                    : null;
                if (kegHead != null)
                    SectionGate(run, c => c.Type == IngredientType.Beer, true, "keg", kegHead);
                var garnishHead = !booze
                    ? AisleSign(run, _garnishHead, c => c.Type == IngredientType.Garnish, false,
                        UIText.T("dayend.board.garnish"))
                    : null;
                if (garnishHead != null)
                    SectionGate(run, c => c.Type == IngredientType.Garnish, false, "garnish", garnishHead);
            }
            else if (_shopTab == 3)
            {
                _cardTarget = ShopSection(UIText.T("dayend.recipes.section"));
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
                            Name = UIText.T("dayend.recipes.sealed.name"),
                            Meta = UIText.T("dayend.recipes.sealed.meta"),
                            Money = gate.ToString("0.0"),
                            GateStars = lockedBy.StarsWanted,
                            State = TileState.Sealed,
                            Identity = UIText.T("dayend.recipes.sealed.identity"),
                            MetaLine = UIText.T("dayend.recipes.sealed.meta_line"),
                            Body = UIText.T(lockedBy.SentenceLine),
                            BuffA = new Buff(BuffKind.Bad, UIText.T(lockedBy.SentenceLine)),
                        });
                        continue;
                    }
                    // WHAT THE SHELF CANNOT POUR, said in the description as well as drawn
                    // on the card (2026-08-10, the author). A recipe you cannot make is still
                    // worth buying — the stock comes later — but that has to be a decision,
                    // not a surprise on the first night it is ordered.
                    // WHAT GOES IN IT STAYS SEALED UNTIL IT IS BOUGHT (2026-09-13, the author:
                    // "satın alınmayan tariflerin içeriği gözükmemeli ... market hoverında da
                    // zorluğu gözüksün"). The card used to print the shopping list and which
                    // of it the shelf was missing; it says how hard the drink is instead.
                    var hard = RecipeDifficulty.Of(r);
                    var spec = new TileSpec
                    {
                        Name = UIText.Data("recipe", r.Id, "name", r.Name),
                        Meta = DifficultyWord(hard) + " · " + PrepWord(r),
                        Art = DrinkIcon.For(r, _bootstrap.Glassware),
                        ArtH = IconH,
                        Recipe = r,
                        // The page's own rung, on the same ladder its sealed neighbours
                        // print — the book is sorted by this number, so it is the one fact
                        // that explains the order of the aisle.
                        RungStars = run.RecipeStarGate(r),
                        Identity = UIText.Caps(UIText.Data("recipe", r.Id, "name", r.Name)),
                        MetaLine = UIText.T("dayend.recipes.meta_line", ("difficulty", DifficultyWord(hard)),
                                            ("prep", PrepWord(r)), ("glass", GlassNameFor(r))),
                        // THE PAGE THAT BRINGS THE SPOON SAYS SO (2026-09-16): while the bar has
                        // no bar spoon, a stirred page's card carries one more sentence — the
                        // buy is a tool as much as a drink, and that has to be a decision too.
                        Body = r.Prep == PrepMethod.Stirred && !run.SpoonUnlocked
                            ? DifficultySentence(hard) + " " + UIText.T("dayend.recipes.brings_spoon")
                            : DifficultySentence(hard),
                        BuffA = new Buff(BuffKind.Gain, UIText.T("dayend.recipes.on_menu")),
                        BuffB = new Buff(hard == DrinkDifficulty.Hard ? BuffKind.Bad
                                         : hard == DrinkDifficulty.Medium ? BuffKind.Cost : BuffKind.Gain,
                                         UIText.T("dayend.recipes.difficulty", ("difficulty", DifficultyWord(hard)))),
                    };
                    DressBuyable(spec, run.RecipePrice(r), "recipe:" + r.Id, false,
                        () => run.UnlockRecipe(r.Id));
                    AddTile(spec);
                }
            }
            // THE UPGRADE SCREEN IS A CATALOGUE (2026-09-13): a rail of shelves and every rung of
            // every ladder on them — TycoonHud.Decor.cs.
            else BuildUpgradeAisle(run, cfg);

            // WHAT THE NEXT STAR OPENS, IN EVERY DEPARTMENT (the author, 2026-08-10).
            // The board only shows what the room's standing already allows, so anything
            // waiting behind the next rung was invisible and the player could not tell an
            // empty aisle from a FINISHED one. Two aisles carried this tile; now every
            // department that still has something locked carries it, and it always names
            // the NEXT gate — at two stars it is the three-star crate, not the two.
            {
                int locked = 0;
                double next = double.MaxValue;
                // One counted sentence per department, "{n} drinks the house will not open for
                // you yet": the noun and its verb are one line, never glued from pieces.
                string waitingKey = "dayend.more.lines";
                // Liquor and mixers answer per AISLE now (SectionGate), because "more is
                // coming" without saying which shelf is a question, not an answer.
                if (_shopTab == 3)
                {
                    waitingKey = "dayend.more.drinks";
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
                // (The upgrade screen stopped carrying this crate on 2026-09-13: every rung of
                // every ladder stands on its shelf now, a locked one with its own gate on it.)
                if (locked > 0)
                    AddTile(new TileSpec
                    {
                        Name = UIText.N("dayend.more.name", locked),
                        Money = next.ToString("0.0"),
                        GateStars = next,
                        State = TileState.Sealed,
                        Identity = UIText.T("dayend.more.identity", ("stars", next.ToString("0.0"))),
                        MetaLine = UIText.N(waitingKey, locked),
                        Body = UIText.T("dayend.more.body", ("stars", next.ToString("0.0"))),
                        BuffA = new Buff(BuffKind.Bad, UIText.T("dayend.more.needs",
                                         ("stars", next.ToString("0.0")),
                                         ("have", run.Rating.Average.ToString("0.0")))),
                    });
            }

            // A DEPARTMENT WITH NOTHING IN IT SAYS SO. Splitting the board in two means
            // either half can legitimately be empty on a given night — the van simply did
            // not bring any mixers — and a bare aisle sign with nothing under it reads as
            // a bug rather than as an answer.
            if (_cardTarget != null && _cardTarget.childCount == 0)
                AddTile(new TileSpec
                {
                    Name = UIText.T("dayend.nothing.name"),
                    Meta = UIText.T("dayend.nothing.meta"),
                    State = TileState.Held,
                    Identity = UIText.T("dayend.nothing.identity"),
                    MetaLine = UIText.T("dayend.nothing.meta_line"),
                    Body = UIText.T("dayend.nothing.body"),
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
            // IN PIXELS, AFTER A REAL LAYOUT PASS (2026-09-07, the author: "market sepetine
            // her ürün eklendiğinde market ürün ekranı aşağı kayıyor"). The normalised
            // figure was restored against a content height the nested grids had not
            // measured yet, so every basket click landed the aisle somewhere else. The
            // content's own offset is what the eye was looking at; it is put back exactly.
            if (_shopScroll != null && _shopScroll.content != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_shopScroll.content);
                Canvas.ForceUpdateCanvases();
                if (_shopScrollPx >= 0f)
                {
                    float viewH = _shopScroll.viewport != null ? _shopScroll.viewport.rect.height : 0f;
                    float max = Mathf.Max(0f, _shopScroll.content.rect.height - viewH);
                    var p = _shopScroll.content.anchoredPosition;
                    _shopScroll.content.anchoredPosition = new Vector2(p.x, Mathf.Clamp(_shopScrollPx, 0f, max));
                }
                else _shopScroll.verticalNormalizedPosition = _shopScrollAt;
            }
        }

        /// <summary>The stamp, on the CHIP. It used to be a 160-wide rotated word laid
        /// across a 190 card, printing straight through the name underneath it; the tile
        /// says "sold" with a strip, a plate and a van, so the stamp only has to land.
        /// </summary>
        /// <remarks>ITS TILE CAN GO FIRST (2026-09-27). This runs on the HUD, not on the tile, and the
        /// tile is the market's to tear down: a pick or an order inside the 0.22 s rebuilds the aisle,
        /// and a department change carries it off in the ghost and destroys it there. Writing to it
        /// after that is a MissingReferenceException, which the test runner fails a test on — so every
        /// frame asks first.</remarks>
        private System.Collections.IEnumerator StampDrop(RectTransform rt)
        {
            const float dur = 0.16f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                if (rt == null) yield break;
                float k = t / dur;
                float s = Mathf.Lerp(2.6f, 0.94f, k * k);      // slams down
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            for (float t = 0; t < 0.06f; t += Time.unscaledDeltaTime)
            {
                if (rt == null) yield break;
                float k = t / 0.06f;
                float s = Mathf.Lerp(0.94f, 1f, k);            // and settles
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
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
            scrim.color = new Color(UITheme.ClubBlue[0].r, UITheme.ClubBlue[0].g, UITheme.ClubBlue[0].b, AskScrimA);
            scrim.raycastTarget = true;   // a wall: nothing behind it may be clicked

            // THE 98 MESSAGE BOX (2026-08-19, the author: '"Close the Order?" kısmını da
            // windows 98 tarzına getir'). The question arrives as a little window OF the
            // site: raised panel, the vice fade for a title bar with the question ON it the
            // way that decade titled its dialogs, and two 98 keys. The head sits IN the bar
            // now rather than floating on the paper — a dialog names itself on its chrome.
            var card = NewRect("Card", _closingAsk);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(620, 220), Vector2.zero);
            AddDialogPop(card);
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
            head.text = UIText.T("dayend.closing.head");

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
                Sfx.Play("key_press", 0.6f);
                if (_closingAsk != null) _closingAsk.gameObject.SetActive(false);
            });
            var backLabel = NewText("L", back, _shop, 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backLabel.text = UIText.T("dayend.closing.go_back");
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
            anywayLabel.text = UIText.T("dayend.closing.open_anyway");
            MarkHoverable(anyway, anywayImg);
            var anywayPress = anyway.gameObject.AddComponent<Win98Press>();
            anywayPress.Face = anywayImg;
            anywayPress.Caption = anywayLabel.rectTransform;

            _closingAsk.SetAsLastSibling();
            _closingAsk.gameObject.SetActive(false);
        }

        /// <summary>
        /// The host's message box on the market (GDD 26 §1b): her face in a well, her name on
        /// the title bar the way that decade titled its dialogs, one line, one key. Built the
        /// way the closing question is built, because on this site a word from the house IS
        /// a little window of the site.
        /// </summary>
        private void BuildHostNote(RectTransform tablet)
        {
            _hostNote = NewRect("HostNote", tablet);
            Stretch(_hostNote, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _hostNote.gameObject.AddComponent<Image>();
            scrim.color = new Color(UITheme.ClubBlue[0].r, UITheme.ClubBlue[0].g, UITheme.ClubBlue[0].b, NoteScrimA);
            scrim.raycastTarget = true;   // a wall, like the question's: read it, then shop

            var card = NewRect("Card", _hostNote);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(620, 200), Vector2.zero);
            AddDialogPop(card);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.sprite = ChromeArt.Win98Key();
            cardImg.type = Image.Type.Sliced;
            cardImg.color = ShopPaper;

            var bar = NewRect("Bar", card);
            Place(bar, new Vector2(0.5f, 1f), new Vector2(612, 28), new Vector2(0, -4));
            var barImg = bar.gameObject.AddComponent<Image>();
            barImg.sprite = ChromeArt.FadeStrip();
            barImg.raycastTarget = false;
            _hostNoteWho = NewText("H", bar, _shop, 16, TextAnchor.MiddleLeft, Color.white);
            Stretch(_hostNoteWho.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10, 0), new Vector2(-10, 0));
            _hostNoteWho.text = UIText.T("dayend.host.fallback");

            // The face, in a well cut to it — the plate's own rule, on the site's paper.
            var well = NewRect("Well", card);
            Place(well, new Vector2(0, 0.5f), new Vector2(80, 80), new Vector2(24, -8));
            well.gameObject.AddComponent<Image>().color = UITheme.Night[2];
            var photo = NewRect("Photo", well);
            Place(photo, new Vector2(0.5f, 0.5f), new Vector2(72, 72), Vector2.zero);
            _hostNoteFace = photo.gameObject.AddComponent<Image>();
            _hostNoteFace.preserveAspect = true;
            _hostNoteFace.raycastTarget = false;

            _hostNoteLine = NewText("L", card, _shop, 16, TextAnchor.MiddleLeft, ShopInk);
            Place(_hostNoteLine.rectTransform, new Vector2(0, 0.5f), new Vector2(470, 80),
                new Vector2(120, -8));
            _hostNoteLine.rectTransform.pivot = new Vector2(0, 0.5f);
            _hostNoteLine.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hostNoteLine.verticalOverflow = VerticalWrapMode.Overflow;

            var key = NewRect("Key", card);
            Place(key, new Vector2(0.5f, 0f), new Vector2(240, 44), new Vector2(0, 22));
            var keyImg = key.gameObject.AddComponent<Image>();
            keyImg.sprite = ChromeArt.Win98Key();
            keyImg.type = Image.Type.Sliced;
            keyImg.color = ShopVice;
            var keyBtn = key.gameObject.AddComponent<Button>();
            keyBtn.targetGraphic = keyImg;
            keyBtn.onClick.AddListener(OnHostNoteKey);
            _hostNoteKeyLabel = NewText("L", key, _shop, 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(_hostNoteKeyLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _hostNoteKeyLabel.text = UIText.T("dayend.host.go_on");
            MarkHoverable(key, keyImg);
            var press = key.gameObject.AddComponent<Win98Press>();
            press.Face = keyImg;
            press.Caption = _hostNoteKeyLabel.rectTransform;

            _hostNote.SetAsLastSibling();
            _hostNote.gameObject.SetActive(false);
        }

        /// <summary>Drives the note off Core once a frame: up while the market is up and
        /// the host has a lesson due, the lines one key at a time, GOT IT on the last.</summary>
        private void SyncHostNote(TycoonRun run)
        {
            if (_hostNote == null) return;
            var lesson = run != null && run.Phase == TycoonPhase.DayEnd && MarketIsUp
                         && !Showing(_closingAsk) ? run.LessonDue : null;
            bool show = lesson != null;
            bool wasUp = _hostNote.gameObject.activeSelf, fresh = false;
            if (show && lesson.Id != _hostNoteLesson)
            {
                fresh = true;
                _hostNoteLesson = lesson.Id;
                _hostNoteAt = 0;
                var host = _bootstrap?.Story?.Cast?.FirstOrDefault(c => c.IsHost);
                _hostNoteWho.text = host != null ? UIText.Caps(host.Name) : UIText.T("dayend.host.fallback");
                var face = host != null ? LookForStory(host) : null;
                _hostNoteFace.sprite = face?.Face;
                _hostNoteFace.enabled = _hostNoteFace.sprite != null;
                _hostNote.SetAsLastSibling();
                Sfx.Play("screen_on", 0.5f);
            }
            if (show)
            {
                int at = Math.Min(_hostNoteAt, lesson.Say.Count - 1);
                _hostNoteLine.text = UIText.Data("lesson", lesson.Id, "say." + at, lesson.Say[at]);
                _hostNoteKeyLabel.text = UIText.T(at >= lesson.Say.Count - 1 ? "dayend.host.got_it" : "dayend.host.go_on");
            }
            else _hostNoteLesson = "";
            if (_hostNote.gameObject.activeSelf != show) _hostNote.gameObject.SetActive(show);
            // A NEW LESSON OPENS (2026-09-27): the box pops; the scrim dims in only when the note was
            // not already up — a lesson following a lesson must not flash the room back to bright.
            if (fresh) OpenDialog(_hostNote, NoteScrimA, ref _noteScrimT, fadeScrim: !wasUp);
        }

        private void OnHostNoteKey()
        {
            var run = Run;
            var lesson = run?.LessonDue;
            Sfx.Play("key_press", 0.6f);
            if (lesson == null) { if (_hostNote != null) _hostNote.gameObject.SetActive(false); return; }
            if (_hostNoteAt < lesson.Say.Count - 1) { _hostNoteAt++; return; }
            run.HeardLesson();
            _hostNoteLesson = "";
        }

        private void ShowClosingAsk(string worry)
        {
            if (_closingAsk == null) { PlayTabletOut(); return; }   // never trap the player
            _closingAskLine.text = worry;
            Sfx.Play("screen_on", 0.7f);
            _closingAsk.gameObject.SetActive(true);
            _closingAsk.SetAsLastSibling();
            OpenDialog(_closingAsk, AskScrimA, ref _askScrimT, fadeScrim: true);
            Sfx.Play("key_press", 0.5f);
        }

        // ── the message boxes open (2026-09-27) ─────────────────────────────────
        //
        // The author's eighth list asked for "pop-up açılmaları" to move, and the tablet's two
        // boxes were the last pop-ups on it that simply appeared. They open the way the hover card
        // does — a quick scale up out of their own middle — over a scrim that dims in. Both stay
        // SWITCHED ON from the first frame (Showing() and the suite read activeSelf, and the scrim
        // is a wall from that frame whatever its alpha), and both still close in one frame — the
        // house's rule for its screens (GDD_MEVCUT): "açılış fade, kapanış anlık".

        /// <summary>The box's opening, on its card: the hover card's own pop, from a little nearer
        /// full size — a message box is a window arriving, not a label unrolling.</summary>
        private static void AddDialogPop(RectTransform card)
        {
            var pop = card.gameObject.AddComponent<PopIn>();
            pop.Seconds = DialogPop;
            pop.From = DialogPopFrom;
            pop.Overshoot = 0.05f;
        }

        /// <summary>Opens a box that has just been switched on: its card pops, and its scrim — the Image
        /// on the box's root — dims in from nothing to <paramref name="scrimA"/>. Reduced motion gives
        /// the scrim at once (PopIn snaps itself).</summary>
        private static void OpenDialog(RectTransform box, float scrimA, ref float scrimT, bool fadeScrim)
        {
            if (box == null) return;
            var card = box.Find("Card");
            var pop = card != null ? card.GetComponent<PopIn>() : null;
            if (pop != null) pop.Play();
            var scrim = box.GetComponent<Image>();
            if (scrim == null) return;
            if (!fadeScrim || Motion.Reduced)
            {
                if (scrimT >= 0f) { scrimT = -1f; SetScrim(scrim, scrimA); }
                return;
            }
            scrimT = 0f;
            SetScrim(scrim, ScrimFloor);
        }

        private static void SetScrim(Graphic scrim, float a)
        {
            var c = scrim.color;
            if (c.a != a) scrim.color = new Color(c.r, c.g, c.b, a);
        }

        private void StepDialogScrims()
        {
            StepScrim(_closingAsk, AskScrimA, ref _askScrimT);
            StepScrim(_hostNote, NoteScrimA, ref _noteScrimT);
        }

        /// <summary>One frame of a scrim dimming in. It lands on exactly its resting alpha, and a box
        /// closed halfway (or reduced motion switched on) puts it there at once, so a box that is not
        /// fading always carries its full scrim.</summary>
        private static void StepScrim(RectTransform box, float scrimA, ref float t)
        {
            if (t < 0f) return;
            var scrim = box != null ? box.GetComponent<Image>() : null;
            if (scrim == null) { t = -1f; return; }
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / DialogFade);
            if (k >= 1f || Motion.Reduced || !box.gameObject.activeSelf)
            {
                t = -1f;
                SetScrim(scrim, scrimA);
                return;
            }
            SetScrim(scrim, Mathf.Max(ScrimFloor, scrimA * Tweening.OutCubic(k)));
        }
    }
}
