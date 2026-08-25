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
    // TycoonHud, part Curtain: the black between two nights, and the day that goes past in it.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    public sealed partial class TycoonHud
    {
        private static void SkyAt(float hour, out Color zenith, out Color horizon)
        {
            var keys = SkyKeys;
            if (hour <= keys[0].Hour) { zenith = keys[0].Zenith; horizon = keys[0].Horizon; return; }
            for (int i = 1; i < keys.Length; i++)
            {
                if (hour > keys[i].Hour) continue;
                float k = Mathf.InverseLerp(keys[i - 1].Hour, keys[i].Hour, hour);
                zenith = Color.Lerp(keys[i - 1].Zenith, keys[i].Zenith, k);
                horizon = Color.Lerp(keys[i - 1].Horizon, keys[i].Horizon, k);
                return;
            }
            zenith = keys[keys.Length - 1].Zenith;
            horizon = keys[keys.Length - 1].Horizon;
        }

        /// <summary>A deterministic dib for the skyline and the stars. Nothing in this game
        /// rolls dice by accident — and a city that re-shuffled itself every morning would
        /// say the player had gone to sleep somewhere else.</summary>
        private static float Dib(int seed)
        {
            unchecked
            {
                int h = seed * 374761393 + 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Builds the sky the day crosses. Everything in it is placed once and only
        /// ever re-coloured or re-positioned — a blackout that allocates is a blackout that
        /// hitches on the one frame the player is only looking at it.</summary>
        private void BuildSkyPanel(RectTransform card, float top)
        {
            _skyPanel = NewRect("Sky", card);
            Place(_skyPanel, new Vector2(0.5f, 1f), new Vector2(SkyW, SkyH), new Vector2(0, top));
            // The sun rises FROM BEHIND the horizon, so the panel has to cut it off — and
            // its glow is wider than the panel, which would otherwise wash the whole card.
            _skyPanel.gameObject.AddComponent<RectMask2D>();

            float band = SkyH / SkyBands;
            _skyRows = new Image[SkyBands];
            for (int i = 0; i < SkyBands; i++)
            {
                var row = NewRect("B" + i, _skyPanel);
                Place(row, new Vector2(0.5f, 1f), new Vector2(SkyW, Mathf.Ceil(band)),
                    new Vector2(0, -i * band));
                _skyRows[i] = row.gameObject.AddComponent<Image>();
                _skyRows[i].raycastTarget = false;
            }

            // The small hours' stars, over the bands and under everything else.
            _stars = new Image[30];
            for (int i = 0; i < _stars.Length; i++)
            {
                float sx = (Dib(i * 3 + 1) - 0.5f) * (SkyW - 24f);
                float sy = SkyH * 0.5f - 14f - Dib(i * 3 + 2) * (SkyH * 0.55f);
                float px = Dib(i * 3 + 3) > 0.72f ? 3f : 2f;
                var st = NewRect("St" + i, _skyPanel);
                Place(st, new Vector2(0.5f, 0.5f), new Vector2(px, px), new Vector2(sx, sy));
                _stars[i] = st.gameObject.AddComponent<Image>();
                _stars[i].color = UITheme.Cream[4];
                _stars[i].raycastTarget = false;
            }

            // The moon first, because the sun rises through where it has been. A DRAWN
            // crescent (Tools/day_sky_gen.py, quantized to the house colours) — the first
            // cut bit a disc out of a second disc wearing the sky's colour, which only
            // ever matched one band of a banded sky at a time.
            _moonGlow = NewRect("MoonGlow", _skyPanel);
            Place(_moonGlow, new Vector2(0.5f, 0.5f), new Vector2(120, 120), Vector2.zero);   // 5x of 24
            _moonGlowImg = _moonGlow.gameObject.AddComponent<Image>();
            _moonGlowImg.sprite = ChromeArt.LampGlow();
            _moonGlowImg.raycastTarget = false;

            _moon = NewRect("Moon", _skyPanel);
            Place(_moon, new Vector2(0.5f, 0.5f), new Vector2(48, 48), Vector2.zero);   // the 24px art at 2x
            _moonImg = _moon.gameObject.AddComponent<Image>();
            _moonImg.sprite = Resources.Load<Sprite>("Scene/curtain_moon");
            _moonImg.preserveAspect = true;
            _moonImg.raycastTarget = false;

            // THE CITY IT ALL GOES DOWN BEHIND — generated art, not procedural boxes
            // (2026-08-25, the author: "kullanilan mevcut gorsel profesyonelce durmuyor,
            // gerekirse gorsel ve animasyonu uret"). Tools/day_sky_gen.py made it at 320x96
            // — the Miami skyline across the bay, lit windows and the two palms baked in —
            // and it stands here at a whole 2x, over the sun, so the sun RISES FROM BEHIND
            // the towers and sets back behind them.
            var cityRt = NewRect("City", _skyPanel);
            Place(cityRt, new Vector2(0.5f, 0f), new Vector2(SkyW, 192f), Vector2.zero);
            cityRt.pivot = new Vector2(0.5f, 0f);
            _cityImg = cityRt.gameObject.AddComponent<Image>();
            _cityImg.sprite = Resources.Load<Sprite>("Scene/curtain_city");
            _cityImg.raycastTarget = false;

            // The sun LAST: it is the only thing in the sky that passes in front of the city.
            _sunGlow = NewRect("SunGlow", _skyPanel);
            Place(_sunGlow, new Vector2(0.5f, 0.5f), new Vector2(240, 240), Vector2.zero);    // 10x of 24
            _sunGlowImg = _sunGlow.gameObject.AddComponent<Image>();
            _sunGlowImg.sprite = ChromeArt.LampGlow();
            _sunGlowImg.raycastTarget = false;

            _sun = NewRect("Sun", _skyPanel);
            Place(_sun, new Vector2(0.5f, 0.5f), new Vector2(64, 64), Vector2.zero);   // the 32px art at 2x
            _sunImg = _sun.gameObject.AddComponent<Image>();
            _sunImg.sprite = Resources.Load<Sprite>("Scene/curtain_sun");
            _sunImg.preserveAspect = true;
            _sunImg.raycastTarget = false;

            // BEHIND THE SKYLINE, both bodies and both glows: a sun that rises in front
            // of a city is a sticker on a photograph.
            cityRt.SetAsLastSibling();
        }

        /// <summary>
        /// What is written in the dark: the day going past, the hour it lands on, the week,
        /// the night handing over to the night, and the same marquee the beam wears. Built
        /// once and driven by StepCurtain.
        /// </summary>
        private void BuildCurtainCard(RectTransform curtain)
        {
            _curtainCard = NewRect("DateCard", curtain);
            Place(_curtainCard, new Vector2(0.5f, 0.5f), new Vector2(700, 520), Vector2.zero);
            _curtainCardGroup = _curtainCard.gameObject.AddComponent<CanvasGroup>();
            _curtainCardGroup.alpha = 0f;
            _curtainCardGroup.blocksRaycasts = false;

            _curtainWeek = NewText("Week", _curtainCard, _body, 16, TextAnchor.UpperCenter,
                UITheme.TextSecondary);
            Place(_curtainWeek.rectTransform, new Vector2(0.5f, 1f), new Vector2(400, 20),
                new Vector2(0, -2));

            BuildSkyPanel(_curtainCard, -26f);

            // THE HOUR, WOUND. The beam's own readout, hung at twice the size — 4× the art,
            // which is still a whole multiple and still lands on the pixel grid.
            _curtainClockHost = NewRect("Hour", _curtainCard);
            // ITS OWN GEOMETRY, NOT ITS RECT'S. SegmentClock hangs every cell off the
            // host's LEFT-MIDDLE, so the digits sit half the host's height below its top —
            // and this host is drawn at twice the size, which doubles that offset too. The
            // row lands at -300; the rect's top has to be 28 above it. (Measured the hard
            // way: at a plain -286 the readout printed straight through THURSDAY.)
            Place(_curtainClockHost, new Vector2(0.5f, 1f), new Vector2(110, 28),
                new Vector2(0, -272f));
            _curtainClockHost.localScale = new Vector3(2f, 2f, 1f);
            _curtainClock = new SegmentClock(_curtainClockHost, UITheme.Cyan[4]);

            // The two names share one seat: the one leaving rides up out of it while the one
            // arriving comes up into it, so the eye follows a single word changing rather
            // than reading two.
            var seat = NewRect("Seat", _curtainCard);
            Place(seat, new Vector2(0.5f, 1f), new Vector2(560, 56), new Vector2(0, -352));

            var leaving = NewRect("Leaving", seat);
            Stretch(leaving, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _curtainLeavingGroup = leaving.gameObject.AddComponent<CanvasGroup>();
            _curtainLeaving = NewText("L", leaving, _display, 32, TextAnchor.UpperCenter,
                UITheme.TextSecondary);
            Stretch(_curtainLeaving.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var arriving = NewRect("Arriving", seat);
            Stretch(arriving, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _curtainArrivingGroup = arriving.gameObject.AddComponent<CanvasGroup>();
            _curtainArriving = NewText("A", arriving, _display, 32, TextAnchor.UpperCenter,
                UITheme.PrimaryAction);
            Stretch(_curtainArriving.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // The marquee, drawn the way the beam draws it: a wire that stops where the work
            // stops, a bulb under every open night, a shutter under the day off.
            var names = BarCalendar.WeekColumns;
            const float step = 60f;
            float left = -names.Length * step * 0.5f;
            float railY = -432f;

            var rail = NewRect("Rail", _curtainCard);
            Place(rail, new Vector2(0.5f, 1f), new Vector2(BarCalendar.OpenNights * step, 1f),
                new Vector2(left + BarCalendar.OpenNights * step * 0.5f, railY + 13f));
            var railImg = rail.gameObject.AddComponent<Image>();
            railImg.color = UITheme.Night[3]; railImg.raycastTarget = false;

            for (int i = 0; i < names.Length; i++)
            {
                float cx = left + i * step + step * 0.5f;
                bool open = i < BarCalendar.OpenNights;

                var stem = NewRect("S" + i, _curtainCard);
                Place(stem, new Vector2(0.5f, 1f), new Vector2(1, 8), new Vector2(cx, railY + 9f));
                var simg = stem.gameObject.AddComponent<Image>();
                simg.color = UITheme.Night[3]; simg.raycastTarget = false;
                simg.enabled = open;

                if (!open)
                    for (int s = 0; s < 4; s++)
                    {
                        var slat = NewRect("Shut" + s + "_" + i, _curtainCard);
                        Place(slat, new Vector2(0.5f, 1f), new Vector2(24, 2),
                            new Vector2(cx, railY - s * 5f));
                        var slatImg = slat.gameObject.AddComponent<Image>();
                        slatImg.color = UITheme.Night[3]; slatImg.raycastTarget = false;
                    }

                var glow = NewRect("G" + i, _curtainCard);
                Place(glow, new Vector2(0.5f, 1f), new Vector2(32, 32), new Vector2(cx, railY - 4f));
                var gimg = glow.gameObject.AddComponent<Image>();
                gimg.sprite = ChromeArt.LampGlow();
                gimg.raycastTarget = false; gimg.enabled = false;

                var bulb = NewRect("B" + i, _curtainCard);
                Place(bulb, new Vector2(0.5f, 1f), new Vector2(16, 16), new Vector2(cx, railY - 4f));
                var bimg = bulb.gameObject.AddComponent<Image>();
                bimg.sprite = ChromeArt.Lamp();
                bimg.color = UITheme.Night[2]; bimg.raycastTarget = false;

                var name = NewText("N" + i, _curtainCard, _body, 8, TextAnchor.UpperCenter,
                    UITheme.TextSecondary);
                Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(step, 12),
                    new Vector2(cx, railY - 26f));
                name.text = names[i];

                _curtainCells.Add((bimg, gimg));
            }
        }

        /// <summary>True while the room is still coming up: the clock must not run.</summary>
        private bool DoorsClosed => _curtainT < CurtainTotal;

        private void OpenTheDoors(int leaving, int arriving)
        {
            if (_curtain == null) return;
            _curtainFrom = leaving;
            _curtainTo = arriving;
            _curtain.gameObject.SetActive(true);
            _curtain.SetAsLastSibling();
            _curtainT = 0f;
            _curtainImg.color = new Color(0f, 0f, 0f, 1f);
            if (_curtainWeek != null)
                _curtainWeek.text = "WEEK " + BarCalendar.WeekOf(arriving);
            if (_curtainLeaving != null)
                _curtainLeaving.text = BarCalendar.Name(BarCalendar.NightOf(leaving));
            if (_curtainArriving != null)
                _curtainArriving.text = BarCalendar.Name(BarCalendar.NightOf(arriving));
            StepCurtain();   // place everything before the first frame is drawn
        }

        private void StepCurtain()
        {
            if (_curtain == null || _curtainT >= CurtainTotal) return;
            _curtainT += Time.unscaledDeltaTime;
            float t = _curtainT;

            // The black itself: full until the lift, then eased away.
            float liftAt = CurtainFadeIn + CurtainDay + CurtainHold;
            float lift = t <= liftAt ? 0f : Mathf.Clamp01((t - liftAt) / CurtainLift);
            _curtainImg.color = new Color(0f, 0f, 0f, (1f - lift) * (1f - lift) + (1f - lift) * 0.0f);

            // The card: in, held, and out ahead of the room so the last thing to go is black.
            float inK = Mathf.Clamp01(t / CurtainFadeIn);
            float outK = Mathf.Clamp01((t - liftAt) / (CurtainLift * 0.55f));
            if (_curtainCardGroup != null) _curtainCardGroup.alpha = inK * (1f - outK);

            // THE DAY ITSELF. One eased run from two in the morning to six in the evening —
            // slow off the mark, quick through the middle, settling onto the hour rather
            // than stopping dead at it, which is what a time-skip has to feel like.
            float dayK = Mathf.Clamp01((t - CurtainFadeIn) / CurtainDay);
            if (Motion.Reduced) dayK = 1f;
            float dayE = dayK * dayK * (3f - 2f * dayK);
            float hour = Mathf.Lerp(DayFrom, DayTo, dayE);
            StepSky(hour);

            // THE HAND-OFF, inside the day's first half: the night that closed slides up and
            // out, the night arriving comes from under it. Smoothstep both ways — a linear
            // slide reads as a scroll.
            float swap = Mathf.Clamp01(dayK / 0.5f);
            float e = swap * swap * (3f - 2f * swap);
            // A BATON PASS, NOT A DISSOLVE. Both names crossfading on the same curve put
            // WEDNESDAY and THURSDAY at half alpha on top of each other for a third of a
            // second, and two 32pt words in one seat read as damage, not as a change. The
            // one leaving is gone before the one arriving is legible, and the travel is
            // bigger than the type so they clear each other rather than pass through.
            float goes = Mathf.Clamp01(swap / 0.55f);
            float comes = Mathf.Clamp01((swap - 0.45f) / 0.55f);
            goes = goes * goes * (3f - 2f * goes);
            comes = comes * comes * (3f - 2f * comes);
            if (_curtainLeavingGroup != null)
            {
                _curtainLeavingGroup.alpha = 1f - goes;
                ((RectTransform)_curtainLeavingGroup.transform).anchoredPosition =
                    new Vector2(0f, 4f + 46f * goes);
            }
            if (_curtainArrivingGroup != null)
            {
                _curtainArrivingGroup.alpha = comes;
                ((RectTransform)_curtainArrivingGroup.transform).anchoredPosition =
                    new Vector2(0f, 4f - 46f * (1f - comes));
            }

            // The marquee under it: last night's bulb goes out as tonight's comes up.
            int from = (int)BarCalendar.NightOf(_curtainFrom);
            int to = (int)BarCalendar.NightOf(_curtainTo);
            for (int i = 0; i < _curtainCells.Count; i++)
            {
                var (bulb, glow) = _curtainCells[i];
                if (bulb == null) continue;
                bool closed = i >= BarCalendar.OpenNights;
                bulb.enabled = !closed;
                if (closed) { if (glow != null) glow.enabled = false; continue; }
                float lit = i == to ? e : i == from ? 1f - e : 0f;
                // Worked nights keep the dull glass they wear on the beam; the two in the
                // hand-off ride the curve between dull and burning.
                bool worked = i < to;
                var cold = worked ? UITheme.Night[3] : UITheme.Night[2];
                bulb.color = Color.Lerp(cold, UITheme.Amber[4], lit);
                if (glow != null)
                {
                    glow.enabled = lit > 0.01f;
                    var g = UITheme.Amber[3];
                    glow.color = new Color(g.r, g.g, g.b, g.a * lit);
                }
            }

            if (_curtainT >= CurtainTotal)
            {
                _curtainT = CurtainTotal;
                // All the way to clear before it goes. The step that crosses the finish
                // returns early on the NEXT frame, so whatever alpha the last computed frame
                // happened to land on — six percent, measured — was the last thing drawn.
                _curtainImg.color = new Color(0f, 0f, 0f, 0f);
                _curtain.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Puts the sky at an hour: the bands, the stars, the city's own windows, the moon
        /// falling west, the sun crossing, and the readout the whole thing is winding.
        ///
        /// Every one of them is driven from the SAME hour — that is the point of the scene.
        /// A sun that crossed on its own timer and a clock that wound on another would be
        /// two animations playing at once, which is exactly what a time-skip must not be.
        /// </summary>
        private void StepSky(float hour)
        {
            if (_skyRows == null) return;
            SkyAt(hour, out var zenith, out var horizon);

            // The warm end HUGS the horizon rather than spreading evenly up the panel: a
            // linear ramp reads as a paint chart, and a sky does not do that.
            for (int i = 0; i < _skyRows.Length; i++)
            {
                float k = _skyRows.Length == 1 ? 0f : i / (float)(_skyRows.Length - 1);
                _skyRows[i].color = Color.Lerp(zenith, horizon, Mathf.Pow(k, 1.6f));
            }

            // The stars go out at first light and are not seen again before the doors open.
            float night = 1f - Mathf.Clamp01((hour - 4.4f) / 1.8f);
            for (int i = 0; i < _stars.Length; i++)
            {
                // A slow twinkle, each on its own phase — a still field of dots reads as
                // dust on the screen.
                float tw = 0.62f + 0.38f * Mathf.Sin(Time.unscaledTime * 2.6f + Dib(i * 5 + 7) * 6.28f);
                var c = _stars[i].color;
                _stars[i].color = new Color(c.r, c.g, c.b, night * tw);
            }

            // The city LIGHTENS with its sky rather than staying pitch: the tint climbs
            // above white toward the horizon's own colour at the bright hours, so the
            // silhouette reads as a city under that sky and not as a hole cut in it. (An
            // Image tint can only multiply, so the art was generated dark on purpose and
            // the day is bought by how far past 1 the channels are pushed.)
            if (_cityImg != null)
            {
                float bright = Mathf.Clamp01((horizon.r + horizon.g + horizon.b) / 1.8f);
                _cityImg.color = Color.Lerp(Color.white,
                    new Color(1f + horizon.r * 0.5f, 1f + horizon.g * 0.5f,
                              1f + horizon.b * 0.5f), bright * 0.55f);
            }

            // THE MOON, finishing its fall into the west. It is already past its peak when
            // the bar shuts, which is why it only ever comes down on this screen.
            float moonSpan = 24f - MoonUp + MoonDown;                 // 18:00 → 06:00
            float moonK = (hour + 24f - MoonUp) / moonSpan;
            PlaceInSky(_moon, _moonGlow, moonK, out _);
            bool moonOut = moonK <= 1.02f;
            float moonFade = Mathf.Clamp01((1.02f - moonK) / 0.12f) * night;
            _moonImg.enabled = moonOut;
            _moonGlowImg.enabled = moonOut;
            if (moonOut)
            {
                _moonImg.color = new Color(1f, 1f, 1f, moonFade);
                var mg = UITheme.ClubBlue[4];
                _moonGlowImg.color = new Color(mg.r, mg.g, mg.b, 0.5f * moonFade);
            }

            // THE SUN, out of the east and back down the west. At 18:00 it is low and gold,
            // which is the light the room's own window is already carrying when the curtain
            // lifts off it.
            float sunK = (hour - SunUp) / (SunDown - SunUp);
            PlaceInSky(_sun, _sunGlow, sunK, out float sunY);
            bool sunOut = sunK >= -0.04f && sunK <= 1.04f;
            _sunImg.enabled = sunOut;
            _sunGlowImg.enabled = sunOut;
            if (sunOut)
            {
                // High and pale, low and orange — a sun reddens at the ends of its own
                // arc. The drawn disc is already warm, so the low tint only leans it.
                float high = Mathf.Sin(Mathf.Clamp01(sunK) * Mathf.PI);
                _sunImg.color = Color.Lerp(new Color(1f, 0.78f, 0.55f), Color.white, high);
                var halo = Color.Lerp(UITheme.Amber[3], UITheme.Amber[4], high);
                // Low sun, heavy haze — carried by the ALPHA and not by the size. A glow
                // that grows off its whole multiple stops landing on the pixel grid, and
                // its four steps come back with ragged edges (16 §6.10).
                _sunGlowImg.color = new Color(halo.r, halo.g, halo.b, 0.55f + 0.35f * (1f - high));
            }

            // And the readout the whole scene is winding. Rounded to the five the beam's own
            // clock reads in, so the two never disagree about what a minute looks like.
            if (_curtainClock != null)
            {
                int total = Mathf.RoundToInt(hour * 60f / 5f) * 5;
                _curtainClock.Show((total / 60) % 24, total % 60,
                    ((int)(Time.unscaledTime * 2f) & 1) == 0);
            }
        }

        /// <summary>Stands a body on its arc: east at 0, west at 1, highest at the middle.
        /// The glow rides with it, and the height comes back out so the moon can ask what
        /// colour the sky is behind it.</summary>
        private void PlaceInSky(RectTransform body, RectTransform glow, float k, out float y)
        {
            float x = Mathf.Lerp(-SkyW * 0.5f + 46f, SkyW * 0.5f - 46f, k);
            float horizonY = -SkyH * 0.5f + SkyGround;
            y = horizonY + Mathf.Sin(Mathf.Clamp(k, 0f, 1f) * Mathf.PI) * 142f;
            var at = new Vector2(x, y);
            if (body != null) body.anchoredPosition = at;
            if (glow != null) glow.anchoredPosition = at;
        }
    }
}
