using System;

namespace LastCall.Core
{
    /// <summary>
    /// Per-customer rules (GDD 00, core loop). All values are "modifiable" by design —
    /// Patrons/Tools will hand out altered configs in M2+.
    /// </summary>
    public sealed class RoundConfig
    {
        public int RailSize { get; }
        public int MaxMixSelection { get; }
        public int MixesPerCustomer { get; }
        public int RestocksPerCustomer { get; }

        /// <summary>How many times you can ask instead of pour (GDD 19 §8).</summary>
        public int ChatsPerCustomer { get; }

        public RoundConfig(int railSize = 8, int maxMixSelection = 5,
            int mixesPerCustomer = 4, int restocksPerCustomer = 3,
            int chatsPerCustomer = 2)
        {
            if (railSize <= 0) throw new ArgumentOutOfRangeException(nameof(railSize));
            if (maxMixSelection <= 0) throw new ArgumentOutOfRangeException(nameof(maxMixSelection));
            if (mixesPerCustomer <= 0) throw new ArgumentOutOfRangeException(nameof(mixesPerCustomer));
            if (restocksPerCustomer < 0) throw new ArgumentOutOfRangeException(nameof(restocksPerCustomer));
            if (chatsPerCustomer < 0) throw new ArgumentOutOfRangeException(nameof(chatsPerCustomer));
            RailSize = railSize;
            MaxMixSelection = maxMixSelection;
            MixesPerCustomer = mixesPerCustomer;
            RestocksPerCustomer = restocksPerCustomer;
            ChatsPerCustomer = chatsPerCustomer;
        }

        public static RoundConfig Default { get; } = new RoundConfig();
    }
}
