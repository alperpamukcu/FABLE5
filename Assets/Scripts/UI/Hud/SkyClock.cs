using System;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// THE EVENING AS A FUNCTION OF THE HOUR (2026-09-17).
    ///
    /// The author: "gün batımı içerisinin rengini ve tonunu değiştirmeli ... güneş aşağı
    /// doğru batmalı, şehir ışıkları ona göre yanmalı, hava renk değişimi daha smooth
    /// olmalı." Until now the room read its light OFF THE PICTURE in the window - thirty-one
    /// PixelLab frames sampled for a key, a wash and a "how much day is left" - so the light
    /// could only be as smooth as the frames and the sun could only do what the frames did,
    /// which was fade out where it stood. This turns the evening into a MODEL: one number,
    /// tau, 0 at 18:00 and 1 at 02:00, and everything the sky and the room need is a
    /// continuous function of it. The window draws the model (<see cref="WindowSky"/>); the
    /// room lights itself from the same evaluation, so the glass and the walls can never
    /// disagree about what hour it is.
    ///
    /// The constants are DATA - Resources/Data/sky_cycle.json, the same file
    /// Tools/window_sky.py renders its preview from - and the colours in it are palette
    /// tokens, so nothing here can put up a colour the game does not own.
    /// </summary>
    public sealed class SkyClock
    {
        /// <summary>Everything the hour decides, evaluated once a frame.</summary>
        public struct Daylight
        {
            public float Tau;
            /// <summary>How much of the sun's disc still stands above the skyline, 0..1.</summary>
            public float SunVisible;
            /// <summary>The sun as a LIGHT: its visible share, eased, so the key through the
            /// glass dies with the disc and not a moment before.</summary>
            public float SunStrength;
            /// <summary>How much daylight the sky still throws: 1 at opening, 0 by full dark.</summary>
            public float Day;
            /// <summary>How far the house has switched itself on: 0 in the sun, 1 once the
            /// lamps are the room's light.</summary>
            public float Dusk;
            /// <summary>The small hours: 0 until late, 1 at closing. The city goes to bed on it.</summary>
            public float LateNight;
            public Color SunCore, SunRim, SunHalo;
            /// <summary>The sky's ramp, top to horizon, as the model's five stops.</summary>
            public Color SkyTop, SkyUpper, SkyMid, SkyLow, SkyHorizon;
            /// <summary>The room's own ambient for this hour - a tint, already pulled toward
            /// white by its "keep" - and how strong it is.</summary>
            public Color Ambient;
            public float AmbientIntensity;
            /// <summary>The glow through the glass — the sunset as LIGHT on the near wall — and
            /// its strength; the shaft's own colour.</summary>
            public Color Glow;
            public float GlowIntensity;
            public Color Shaft;
            /// <summary>Where a cast shadow falls, in stage units, and how dark it is.</summary>
            public Vector2 ShadowOffset;
            public float ShadowAlpha;
        }

        // ── the json's shape (JsonUtility: public fields, no nullables) ────────
        [Serializable] public class Key { public float t; public string hour; public string[] stops; }
        [Serializable] public class SunSpec
        {
            public float col, radius, rowStart, rowEnd, setBy, horizonRow;
            public string coreHigh, coreLow, rimHigh, rimLow, halo;
            /// <summary>The colour of the patch of sun on the wall, high and at the set —
            /// hotter than the disc's own, because a shaft of sunset light really is.</summary>
            public string shaftHigh, shaftLow;
            public float haloRadius, haloHigh, haloLow;
        }
        [Serializable] public class StarSpec { public int count, rowMax; public float from, to, lumaMax; }
        [Serializable] public class CitySpec
        {
            /// <summary>How far behind the hour the city's frames run, as a fraction of the
            /// night, so the windows start as the disc touches the towers.</summary>
            public float frameLag;
            public float cloudDrift, ditherSpread;
        }
        [Serializable] public class BirdSpec { public float until, everyMin, everyMax, speed; public int flockMin, flockMax; }
        /// <summary>The moon: up from `from`, faded in over `fade`, crossing from (xStart,yStart)
        /// to (xEnd,yEnd) in source px by closing time, with `arc` rows of lift at mid-crossing.</summary>
        [Serializable] public class MoonSpec { public float from, fade, xStart, xEnd, yStart, yEnd, arc; }
        [Serializable] public class RoomKey
        {
            public float t;
            public string ambient; public float keep, intensity;
            /// <summary>The sky's colour arriving through the glass: a pool at the window.</summary>
            public string glow; public float glowKeep, glowIntensity;
        }
        [Serializable] public class RoomSpec
        {
            public RoomKey[] keys;
            public float sunShadowNearX, sunShadowNearY, sunShadowFarX, sunShadowFarY, sunShadowAlpha;
            /// <summary>The sky's own shadow once the sun is gone: the glass is still the
            /// brightest thing in the room, so shadows keep the sun's far line, fainter.</summary>
            public float skyShadowAlpha;
            public float lampShadowX, lampShadowY, lampShadowAlpha, shadowFloor;
            public float dayFrom, dayTo, duskFrom, duskTo, lateFrom;
        }
        [Serializable] public class Model
        {
            public float[] stopsV;
            public string[] skyPalette;
            public Key[] keys;
            public SunSpec sun;
            public StarSpec stars;
            public CitySpec city;
            public BirdSpec birds;
            public MoonSpec moon;
            public RoomSpec room;
        }

        public readonly Model Spec;
        private readonly Color[][] _keyStops;
        private readonly Color[] _roomAmbient, _roomGlow;

        public SkyClock(Model model)
        {
            Spec = model;
            _keyStops = new Color[model.keys.Length][];
            for (int i = 0; i < model.keys.Length; i++)
            {
                _keyStops[i] = new Color[model.keys[i].stops.Length];
                for (int s = 0; s < model.keys[i].stops.Length; s++)
                    _keyStops[i][s] = Token(model.keys[i].stops[s]);
            }
            _roomAmbient = new Color[model.room.keys.Length];
            _roomGlow = new Color[model.room.keys.Length];
            for (int i = 0; i < model.room.keys.Length; i++)
            {
                _roomAmbient[i] = Token(model.room.keys[i].ambient);
                _roomGlow[i] = string.IsNullOrEmpty(model.room.keys[i].glow)
                    ? Color.white : Token(model.room.keys[i].glow);
            }
        }

        /// <summary>Reads Resources/Data/sky_cycle.json. Null when it is missing, and the
        /// stage then keeps the still plate and its old constants.</summary>
        public static SkyClock Load()
        {
            var text = Resources.Load<TextAsset>("Data/sky_cycle");
            if (text == null) return null;
            Model m;
            try { m = JsonUtility.FromJson<Model>(text.text); }
            catch (Exception e) { Debug.LogWarning("SkyClock: sky_cycle.json did not parse: " + e.Message); return null; }
            if (m == null || m.keys == null || m.keys.Length < 2 || m.stopsV == null || m.sun == null
                || m.city == null || m.stars == null || m.birds == null || m.room == null || m.room.keys == null
                || m.room.keys.Length < 2)
            {
                Debug.LogWarning("SkyClock: sky_cycle.json is missing a section.");
                return null;
            }
            foreach (var k in m.keys)
                if (k.stops == null || k.stops.Length != m.stopsV.Length)
                {
                    Debug.LogWarning($"SkyClock: key '{k.hour}' has {k.stops?.Length ?? 0} stops, the model has {m.stopsV.Length}.");
                    return null;
                }
            return new SkyClock(m);
        }

        /// <summary>'Magenta3' → UITheme.Magenta[3]. A token the palette does not have is
        /// loud, because a sky colour off the palette is exactly the thing the model exists
        /// to prevent.</summary>
        public static Color Token(string name)
        {
            int cut = name.Length;
            while (cut > 0 && char.IsDigit(name[cut - 1])) cut--;
            string ramp = name.Substring(0, cut);
            int idx = cut < name.Length ? int.Parse(name.Substring(cut)) : 0;
            Color[] r;
            switch (ramp)
            {
                case "Night": r = UITheme.Night; break;
                case "Magenta": r = UITheme.Magenta; break;
                case "Cyan": r = UITheme.Cyan; break;
                case "Amber": r = UITheme.Amber; break;
                case "ViceRed": r = UITheme.ViceRed; break;
                case "ClubBlue": r = UITheme.ClubBlue; break;
                case "Lime": r = UITheme.Lime; break;
                case "Cream": r = UITheme.Cream; break;
                case "Malt": r = UITheme.Malt; break;
                case "Graphite": r = UITheme.Graphite; break;
                case "Brick": r = UITheme.Brick; break;
                default:
                    Debug.LogWarning("SkyClock: unknown palette token '" + name + "'");
                    return Color.magenta;
            }
            return r[Mathf.Clamp(idx, 0, r.Length - 1)];
        }

        /// <summary>Walks a colour toward white by 1 − keep: a light MULTIPLIES what it lands
        /// on, so one that is only part of the way to a hue tints, and one that goes all the
        /// way paints over.</summary>
        public static Color Pull(Color c, float keep) =>
            new Color(1f + (c.r - 1f) * keep, 1f + (c.g - 1f) * keep, 1f + (c.b - 1f) * keep, 1f);

        public static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The sky's five stops at this hour, top to horizon, continuous.</summary>
        public void Bands(float tau, Color[] into)
        {
            var keys = Spec.keys;
            int n = into.Length;
            if (tau <= keys[0].t) { Array.Copy(_keyStops[0], into, n); return; }
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (tau > keys[i + 1].t) continue;
                float f = SmoothStep(keys[i].t, keys[i + 1].t, tau);
                for (int s = 0; s < n; s++) into[s] = Color.Lerp(_keyStops[i][s], _keyStops[i + 1][s], f);
                return;
            }
            Array.Copy(_keyStops[keys.Length - 1], into, n);
        }

        /// <summary>The sun's centre row in source px, sinking on a straight line.</summary>
        public float SunRow(float tau) =>
            Spec.sun.rowStart + (Spec.sun.rowEnd - Spec.sun.rowStart) * Mathf.Clamp01(tau / Spec.sun.setBy);

        /// <summary>
        /// Evaluates the hour. <paramref name="sunVisible"/> is measured by the window - how
        /// much of the disc the skyline has not yet taken - because only the drawing knows
        /// where the towers are.
        /// </summary>
        public Daylight Evaluate(float tau, float sunVisible)
        {
            tau = Mathf.Clamp01(tau);
            var d = new Daylight { Tau = tau, SunVisible = sunVisible };
            var sun = Spec.sun;
            var room = Spec.room;
            float sink = SmoothStep(0f, sun.setBy, tau);
            d.SunCore = Color.Lerp(Token(sun.coreHigh), Token(sun.coreLow), sink);
            d.SunRim = Color.Lerp(Token(sun.rimHigh), Token(sun.rimLow), sink);
            d.SunHalo = string.IsNullOrEmpty(sun.halo) ? d.SunCore : Token(sun.halo);
            d.Shaft = string.IsNullOrEmpty(sun.shaftHigh) ? d.SunCore
                : Color.Lerp(Token(sun.shaftHigh), Token(string.IsNullOrEmpty(sun.shaftLow) ? sun.shaftHigh : sun.shaftLow), sink);
            // The key through the glass follows the DISC: eased, so it dies as the last of
            // the sun goes behind the towers rather than snapping off at the horizon row.
            d.SunStrength = SmoothStep(0f, 1f, sunVisible);
            d.Day = 1f - SmoothStep(room.dayFrom, room.dayTo, tau);
            d.Dusk = SmoothStep(room.duskFrom, room.duskTo, tau);
            d.LateNight = SmoothStep(room.lateFrom, 1f, tau);

            var stops = new Color[Spec.stopsV.Length];
            Bands(tau, stops);
            d.SkyTop = stops[0];
            d.SkyUpper = stops[Mathf.Min(1, stops.Length - 1)];
            d.SkyMid = stops[Mathf.Min(2, stops.Length - 1)];
            d.SkyLow = stops[Mathf.Min(3, stops.Length - 1)];
            d.SkyHorizon = stops[stops.Length - 1];

            // The room's ambient, keyed like the sky and pulled toward white by its keep: a
            // light MULTIPLIES what it lands on, so one that is only part of the way to a hue
            // tints the plaster, and one that goes all the way paints over it.
            var rk = room.keys;
            int last = rk.Length - 1;
            int ka = last, kb = last; float f = 0f;
            if (tau <= rk[0].t) { ka = kb = 0; }
            else
                for (int i = 0; i < last; i++)
                {
                    if (tau > rk[i + 1].t) continue;
                    ka = i; kb = i + 1; f = SmoothStep(rk[i].t, rk[i + 1].t, tau);
                    break;
                }
            var amb = Color.Lerp(_roomAmbient[ka], _roomAmbient[kb], f);
            float keep = Mathf.Lerp(rk[ka].keep, rk[kb].keep, f);
            d.Ambient = Pull(amb, keep);
            d.AmbientIntensity = Mathf.Lerp(rk[ka].intensity, rk[kb].intensity, f);
            var glow = Color.Lerp(_roomGlow[ka], _roomGlow[kb], f);
            d.Glow = Pull(glow, Mathf.Lerp(rk[ka].glowKeep, rk[kb].glowKeep, f));
            d.GlowIntensity = Mathf.Lerp(rk[ka].glowIntensity, rk[kb].glowIntensity, f);

            // WHERE SHADOWS FALL. The sun stands outside the left window: high at opening,
            // so shadows are short and fall down and to the right; low at the set, so they
            // stretch across the wall. Once the lamps own the room, light comes from over the
            // bar and shadows sit close under whatever casts them. The two are blended by how
            // much light each is actually throwing, so at the changeover the shadow swings
            // from the sun's line to the lamps' without ever jumping.
            float sunW = d.SunStrength * room.sunShadowAlpha;
            // ...and the SKY between the two: with the sun gone and the lamps not yet up,
            // the glass is still the room's brightest source, and a shadow that vanished
            // for the pink band read as the figures losing their edge for half a minute.
            float skyW = (1f - d.SunStrength) * d.Day * room.skyShadowAlpha;
            float lampW = d.Dusk * room.lampShadowAlpha;
            var sunFar = new Vector2(room.sunShadowFarX, room.sunShadowFarY);
            var sunOff = Vector2.Lerp(new Vector2(room.sunShadowNearX, room.sunShadowNearY), sunFar, sink);
            var lampOff = new Vector2(room.lampShadowX, room.lampShadowY);
            float wsum = sunW + skyW + lampW;
            d.ShadowOffset = wsum > 0.0001f
                ? (sunOff * sunW + sunFar * skyW + lampOff * lampW) / wsum : lampOff;
            d.ShadowAlpha = Mathf.Clamp(wsum, room.shadowFloor, 1f);
            return d;
        }
    }
}
