using System;

namespace LastCall.Core
{
    /// <summary>
    /// Green Stake satisfaction targets (GDD 5.1). Kept in code for now; moves to data
    /// alongside the Stake modifiers in M3 so stakes can rescale it.
    /// </summary>
    public static class TargetTable
    {
        private static readonly double[,] Green =
        {
            //  A       B        VIP
            {    300,    450,     600 }, // Night 1
            {    800,   1200,    1600 }, // Night 2
            {   2000,   3000,    4000 }, // Night 3
            {   5000,   7500,   10000 }, // Night 4
            {  11000,  16500,   22000 }, // Night 5
            {  20000,  30000,   40000 }, // Night 6
            {  35000,  52500,   70000 }, // Night 7
            {  50000,  75000,  100000 }  // Night 8
        };

        public static double GreenStake(int night, CustomerSlot slot)
        {
            if (night < 1 || night > Green.GetLength(0))
                throw new ArgumentOutOfRangeException(nameof(night));
            return Green[night - 1, (int)slot];
        }
    }
}
