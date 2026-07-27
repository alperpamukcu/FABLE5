using System;

namespace LastCall.Core
{
    /// <summary>
    /// What one moment under an open tap produces (GDD 21 §10.2). Beer and its head share the
    /// glass, so froth genuinely serves less beer — the head is not decoration. Anything that
    /// ran past the rim is spilled: it left the keg and reached nobody.
    /// </summary>
    public readonly struct DraughtFlow
    {
        /// <summary>Volume of liquid beer, in glass-fractions.</summary>
        public double Beer { get; }
        /// <summary>Volume of foam, in glass-fractions.</summary>
        public double Head { get; }
        /// <summary>Volume that missed the glass, in glass-fractions.</summary>
        public double Spill { get; }

        /// <summary>Everything that left the keg this moment.</summary>
        public double Total => Beer + Head + Spill;
        /// <summary>What the glass actually caught.</summary>
        public double Caught => Beer + Head;

        public DraughtFlow(double beer, double head, double spill)
        {
            Beer = beer;
            Head = head;
            Spill = spill;
        }
    }

    /// <summary>
    /// The pour rules for a keg (GDD 21 §10). The tap runs at one rate; what the player controls
    /// is the angle they hold the glass at. Pure and stateless: the caller owns the glass, the
    /// hand and the clock, this only says what a given angle catches and what a finished pint is
    /// worth.
    /// </summary>
    public static class TapPour
    {
        /// <summary>Glass-fractions a second out of an open tap.</summary>
        public const double FlowPerSecond = 0.42;

        // ── the angle (GDD 21 §10.2) ────────────────────────────────────────────

        /// <summary>Degrees from upright. 0 = straight up, 90 = on its side.</summary>
        public const double IdealTilt = 45.0;
        /// <summary>Past this the stream starts running by the rim instead of into the glass.</summary>
        public const double SpillTilt = 60.0;
        /// <summary>Everything misses at this angle — the glass is essentially lying down.</summary>
        public const double LostTilt = 88.0;

        /// <summary>Foam share of what is caught, held dead upright and held at the ideal lean.
        /// Upright the stream drops the height of the glass and breaks; leaned over it runs down
        /// the wall and keeps its gas.</summary>
        public const double HeadShareUpright = 0.78;
        public const double HeadShareTilted = 0.04;

        /// <summary>Share of the head that collapses back into beer each second. Standing still
        /// rescues a botched pour — at the price of the customer's patience.</summary>
        // Froth above the keeper halves in about four seconds — brisk, because it is only ever
        // the excess that is falling now and the rescue should feel like it is working.
        public const double SettlePerSecond = 0.16;

        /// <summary>How much liquid a collapsing head leaves behind — the rest was air. This is
        /// why a settled pint needs topping up, and why froth cannot simply be waited out into
        /// a full glass (GDD 21 §10.2).</summary>
        public const double FoamLiquidShare = 0.35;

        /// <summary>The head a good pint carries, and the band around it that still reads as
        /// good (GDD 21 §10.3). Outside <see cref="HeadTolerance"/> the pint scores nothing.</summary>
        public const double IdealHead = 0.14;
        public const double GoodHeadMin = 0.08;
        public const double GoodHeadMax = 0.20;
        public const double HeadTolerance = 0.26;

        /// <summary>
        /// What lands while the glass is held at <paramref name="tiltDegrees"/> from upright for
        /// <paramref name="seconds"/> under an open tap.
        ///
        /// Foam falls off quickly as the glass leans — most of the useful control is in the first
        /// half of the lean, which is why the pour is a movement and not a setting. Past
        /// <see cref="SpillTilt"/> the stream begins to run by the rim, and what misses is gone.
        /// </summary>
        public static DraughtFlow Flow(double tiltDegrees, double seconds)
        {
            if (seconds <= 0) return default;

            double tilt = tiltDegrees < 0 ? 0 : tiltDegrees > 90 ? 90 : tiltDegrees;
            double total = FlowPerSecond * seconds;

            double spillShare = tilt <= SpillTilt
                ? 0.0
                : Clamp01((tilt - SpillTilt) / (LostTilt - SpillTilt));
            double spill = total * spillShare;
            double caught = total - spill;

            // The foam curve is steep near upright and flat around the ideal lean, so the fine
            // control sits where the player needs it: a wobble while filling costs nothing, and
            // standing the glass up at the end dials the head in. Squaring it the other way put
            // the precision in the wrong place — flat where the head is built and twitchy where
            // it is only being filled (caught by MostOfTheFoamControlIsNearUpright).
            double lean = Clamp01(tilt / IdealTilt);
            double shaped = 2.0 * lean - lean * lean;
            double headShare = HeadShareUpright + (HeadShareTilted - HeadShareUpright) * shaped;

            return new DraughtFlow(caught * (1.0 - headShare), caught * headShare, spill);
        }

        /// <summary>
        /// How much of <paramref name="head"/> has collapsed into beer after
        /// <paramref name="seconds"/> of standing.
        ///
        /// Only the froth ABOVE <paramref name="keep"/> falls: a head that belongs on the pint
        /// stays on it, the way a real one does, and it is the wild froth of a bad pour that
        /// drops away and gives some beer back. Settling everything to nothing made a good pint
        /// go flat while it was carried to the customer (2026-07-27).
        /// </summary>
        public static double Settled(double head, double seconds, double keep = 0)
        {
            if (head <= keep || seconds <= 0) return 0;
            return (head - keep) * (1.0 - Math.Exp(-SettlePerSecond * seconds));
        }

        /// <summary>
        /// What the pint is worth, 0…1, from the head it carries (GDD 21 §10.3). 1 across the
        /// good band, falling to 0 at the edge of tolerance either side — flat beer and a glass
        /// of froth are both bad pints, and the player can see which one they poured.
        /// </summary>
        public static double HeadScore(double head)
        {
            if (head >= GoodHeadMin && head <= GoodHeadMax) return 1.0;

            double miss = head < GoodHeadMin ? GoodHeadMin - head : head - GoodHeadMax;
            double room = head < GoodHeadMin ? GoodHeadMin : HeadTolerance - GoodHeadMax;
            if (room <= 0) return 0.0;
            return Clamp01(1.0 - miss / room);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
