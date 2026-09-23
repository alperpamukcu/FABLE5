using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// THE LIGHT LANGUAGE (2026-09-21, the author: "Oyundaki ışıklandırma için profesyonel bir oyun sanat tasarımı
    /// dokunuşu lazım, oyunda belli tonlar belli filtrelerimiz olmalı tüm ışıklandırmalar yansımalar buna göre
    /// olmalı"). Every light in the room is one of five things, and each thing has one colour:
    ///
    ///   KEY      tungsten - every lamp the house owns: the bar's downlights, the wall lamps, the cellar, the
    ///            lamps over the counter, the guest's lamp at the last call. Amber pulled a third to white.
    ///   FILL     the hour's - the sky json's ambient token, cool while the sky is warm and tungsten late; it is
    ///            not set here, it is read from Resources/Data/sky_cycle.json by the hour (SkyClock).
    ///   NEON     a tube is Magenta[4] or Cyan[4] and nothing between: the counter's strip, the signs.
    ///   SCREEN   the set's spill, ClubBlue[4].
    ///   SUN      the window's, from the same json: Cream/Amber cores, ViceRed rims, the glow keys.
    ///
    /// The fixture data still names a colour per piece, as it always did; the room SNAPS it to the language at
    /// load (<see cref="Snap"/>), so a lamp bought in the market cannot bring a sixth tint into the room, and a
    /// new fixture needs no new rule. Measured before this (r78, 00:30): six warm sources on five different
    /// near-ambers, a coral lamp, a pink lamp, a purple sign.
    ///
    /// And the GRADE: the one filter the room wears, on the game's own volume (LastCallVolume, written by
    /// LastCall → Setup Light Grade from the numbers below): shadows a step toward the club's blue, highlights a
    /// step toward the amber, a little more contrast, a vignette in Night[0]. Nothing tonemaps - the palette is
    /// tokened (GDD 14 §5) and a tonemapper is a regrade. The HUD is a screen-space overlay and takes none of it.
    /// </summary>
    public static class LightLanguage
    {
        /// <summary>
        /// Tungsten: the house's every lamp. It was the amber a third of the way to WHITE until 2026-09-23,
        /// when the author looked at the room and said the ceiling was giving white light ("tavan
        /// aydınlatmaları çok beyaz ışık veriyor biraz renksizleştir", "aydınlatmanın beyazlığını kaldıralım")
        /// - measured, the wall under a globe read (255,253,210), which is not a colour, it is a clipped
        /// channel. It is the palette's own amber now, with no white mixed into it at all, and the lamps
        /// carry a fifth less punch (<see cref="DiegeticStage.HouseLampStop"/>), which is the pair of changes
        /// the author picked out of four in the room (L3, 2026-09-23).
        /// </summary>
        public static readonly Color Key = UITheme.Amber[4];
        /// <summary>The lift the drinkers take over the room: the key most of the way to white, so a face reads
        /// as lit rather than tinted.</summary>
        public static readonly Color PatronLift = Color.Lerp(Key, Color.white, 0.60f);
        public static readonly Color NeonPink = UITheme.Magenta[4];
        public static readonly Color NeonCyan = UITheme.Cyan[4];
        public static readonly Color Screen = UITheme.ClubBlue[4];

        /// <summary>How saturated a fixture's asked colour must be to count as a tube rather than a lamp.</summary>
        private const float NeonSaturation = 0.40f;
        /// <summary>The two tubes' hues (0..1) and how far from one a tube may sit and still be it.</summary>
        private const float PinkHue = 0.88f, CyanHue = 0.50f, HueReach = 0.13f;

        /// <summary>
        /// A fixture's light, in the language: a screen is <see cref="Screen"/>; a saturated colour near a tube's
        /// hue is that tube; everything else - white, cream, straw, coral, the reds and yellows a lamp's paint
        /// comes in - is a lamp and is <see cref="Key"/>.
        /// </summary>
        public static Color Snap(Color asked, bool screen)
        {
            if (screen) return Screen;
            Color.RGBToHSV(asked, out float h, out float s, out _);
            if (s < NeonSaturation) return Key;
            if (HueDistance(h, PinkHue) <= HueReach) return NeonPink;
            if (HueDistance(h, CyanHue) <= HueReach) return NeonCyan;
            return Key;
        }

        private static float HueDistance(float a, float b)
        {
            float d = Mathf.Abs(a - b);
            return Mathf.Min(d, 1f - d);
        }

        // ── the grade ─────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Contrast and saturation, in URP's -100..100.</summary>
        public const float GradeContrast = 12f, GradeSaturation = 8f;
        /// <summary>Lift, gamma, gain (xyz about 1, w the offset): shadows toward the club's blue, highlights
        /// toward the amber - the warm key and cool fill the room already lights by, said once more in the grade.</summary>
        public static readonly Vector4 GradeLift = new Vector4(0.97f, 0.98f, 1.04f, 0f);
        public static readonly Vector4 GradeGamma = new Vector4(1f, 1f, 1f, 0f);
        public static readonly Vector4 GradeGain = new Vector4(1.04f, 1.02f, 0.97f, 0f);
        /// <summary>The vignette: a quarter, soft, in the palette's darkest night.</summary>
        public const float VignetteIntensity = 0.22f, VignetteSmoothness = 0.40f;
        public static Color VignetteColor => UITheme.Night[0];
    }
}
