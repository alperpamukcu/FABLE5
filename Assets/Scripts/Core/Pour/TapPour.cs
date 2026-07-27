using System;

namespace LastCall.Core
{
    /// <summary>
    /// What one moment of holding a tap open produces (GDD 21 §10.2). Beer and its head share
    /// the glass, so a frothy pull genuinely serves less beer — the head is not decoration.
    /// </summary>
    public readonly struct DraughtFlow
    {
        /// <summary>Volume of liquid beer, in glass-fractions.</summary>
        public double Beer { get; }
        /// <summary>Volume of foam, in glass-fractions.</summary>
        public double Head { get; }

        public double Total => Beer + Head;

        public DraughtFlow(double beer, double head)
        {
            Beer = beer;
            Head = head;
        }
    }

    /// <summary>
    /// The pull-to-pour rules for a keg (GDD 21 §10). Pure and stateless: the caller owns the
    /// handle, the glass and the clock, this only says what a given pull produces and what a
    /// finished pint is worth.
    /// </summary>
    public static class TapPour
    {
        /// <summary>Glass-fractions a second with the handle wide open.</summary>
        public const double FlowPerSecond = 0.62;

        /// <summary>Foam share of the stream at a hair's opening, and at wide open. A tap eased
        /// open pours almost clean; thrown open it froths (GDD 21 §10.2).</summary>
        public const double HeadShareAtCrack = 0.06;
        public const double HeadShareAtFull = 0.72;

        /// <summary>Share of the head that collapses back into beer each second. Standing still
        /// rescues a botched pull — at the price of the customer's patience.</summary>
        // Half the head is gone in about nine seconds. Measured at 0.18 first, which cleared
        // a good pint before it could reach the customer — the rescue has to be slow enough
        // to be a decision, not so fast that standing still is the only outcome.
        public const double SettlePerSecond = 0.08;

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
        /// What flows while the handle is held at <paramref name="pull"/> (0 = shut,
        /// 1 = wide open) for <paramref name="seconds"/>. Foam rises with the square of the
        /// pull, so the bottom half of the handle's travel is the controllable part — which is
        /// what makes easing it open a skill rather than a setting.
        /// </summary>
        public static DraughtFlow Flow(double pull, double seconds)
        {
            if (seconds <= 0) return new DraughtFlow(0, 0);
            pull = Clamp01(pull);
            if (pull <= 0) return new DraughtFlow(0, 0);

            double total = FlowPerSecond * pull * seconds;
            double headShare = HeadShareAtCrack + (HeadShareAtFull - HeadShareAtCrack) * pull * pull;
            return new DraughtFlow(total * (1.0 - headShare), total * headShare);
        }

        /// <summary>
        /// How much of <paramref name="head"/> has collapsed into beer after
        /// <paramref name="seconds"/> of standing. Exponential, so foam falls away quickly at
        /// first and the last skim lingers — which is what a real pint does.
        /// </summary>
        public static double Settled(double head, double seconds)
        {
            if (head <= 0 || seconds <= 0) return 0;
            return head * (1.0 - Math.Exp(-SettlePerSecond * seconds));
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
