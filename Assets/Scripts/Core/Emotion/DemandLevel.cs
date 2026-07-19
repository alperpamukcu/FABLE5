using System;

namespace LastCall.Core
{
    /// <summary>
    /// How hard a customer is to please (GDD 20 §2.1). Rises as the week gets later, so the
    /// same serve that delighted someone on Night 1 barely registers on Night 8.
    ///
    /// This is the difficulty axis that scales *the customer*, as opposed to the quota, which
    /// scales what the week demands of you. Values are used arithmetically — do not reorder.
    /// </summary>
    public enum DemandLevel
    {
        /// <summary>Glad you tried. A nudge in the right direction reads as care.</summary>
        Easygoing = 1,

        /// <summary>Wants to actually feel the difference.</summary>
        Particular = 2,

        /// <summary>A token gesture is worth nothing to them. Land it or don't bother.</summary>
        Demanding = 3
    }

    public static class Demands
    {
        /// <summary>
        /// What the night alone adds. The run opens forgiving and closes hard: whoever walks
        /// in on Night 7 is a tougher room than the same person on Night 1.
        ///
        /// Measured, not guessed. A first pass stepped at nights 3 and 6, which made every
        /// customer from Night 6 on Demanding, dropped average satisfaction 2.10 → 1.74 and
        /// crushed the bot win rate to 4%. Stepping later keeps the top of the scale as a
        /// late-run squeeze instead of the default state of the back half.
        /// </summary>
        public static int NightStep(int night) => night <= 3 ? 0 : night <= 6 ? 1 : 2;

        /// <summary>
        /// The demand for one visit: the archetype's disposition, pushed up by the night.
        /// Clamped to the enum's range, so a Demanding archetype on Night 8 is still
        /// Demanding rather than falling off the end of the scale.
        /// </summary>
        public static DemandLevel For(int night, DemandLevel archetypeBase)
        {
            int level = (int)archetypeBase + NightStep(night);
            if (level < (int)DemandLevel.Easygoing) level = (int)DemandLevel.Easygoing;
            if (level > (int)DemandLevel.Demanding) level = (int)DemandLevel.Demanding;
            return (DemandLevel)level;
        }

        /// <summary>Movement that reads as a real change to this customer (2 satisfaction).</summary>
        public static int StrongProgress(DemandLevel demand)
        {
            switch (demand)
            {
                case DemandLevel.Easygoing: return 15;
                case DemandLevel.Particular: return 22;
                default: return 30;
            }
        }

        /// <summary>
        /// Movement below which this customer feels nothing at all (0 satisfaction). Only the
        /// Demanding have a floor — everyone else takes any honest effort as something.
        /// </summary>
        public static int MinProgress(DemandLevel demand) =>
            demand == DemandLevel.Demanding ? 8 : 1;

        /// <summary>Short label for the ID card.</summary>
        public static string Label(DemandLevel demand)
        {
            switch (demand)
            {
                case DemandLevel.Easygoing: return "easy to please";
                case DemandLevel.Particular: return "particular";
                default: return "hard to please";
            }
        }
    }
}
