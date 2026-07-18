namespace LastCall.Core
{
    /// <summary>Why a serve missed (GDD 19 §6). <see cref="None"/> means it didn't.</summary>
    public enum BustKind
    {
        None,

        /// <summary>Pushed straight past what they were asking for. Too much, too fast.</summary>
        Overshoot,

        /// <summary>Moved them the wrong way, and by more than a rounding error.</summary>
        WrongWay
    }

    /// <summary>
    /// The verdict on one serve: how well the bartender read the person in front of them
    /// (GDD 19 §6). Feeds the Mult side of scoring and the week's satisfaction quota.
    /// A bust is never committed to the customer's stats — see <see cref="CommittedDelta"/>.
    /// </summary>
    public sealed class ResonanceResult
    {
        /// <summary>Added to Mult: how much closer the intent stat got, over 10.</summary>
        public double ResonanceMult { get; }

        /// <summary>Flat Mult bonus for hitting a stat you could not see.</summary>
        public double LuckyReadMult { get; }

        /// <summary>Multiplies Mult when the serve landed exactly on target (×2, or ×3 blind).</summary>
        public double ServeBurst { get; }

        /// <summary>Subtracted from Mult on a bust; Mult never falls below 1.</summary>
        public double BustPenalty { get; }

        public BustKind Bust { get; }
        public bool IsBust => Bust != BustKind.None;

        /// <summary>Landed the intent stat exactly on 0 or exactly on 100.</summary>
        public bool CleanServe { get; }

        /// <summary>The intent stat was Unknown on the ID card — this was a read, not a readout.</summary>
        public bool BlindRead { get; }

        /// <summary>Units of movement toward what they asked for; 0 on a bust.</summary>
        public int Progress { get; }

        /// <summary>What this serve contributes to the week's quota (GDD 19 §10).</summary>
        public int Satisfaction { get; }

        /// <summary>What should actually be written to the customer's stats — empty on a bust.</summary>
        public EmotionDelta CommittedDelta { get; }

        public ResonanceResult(double resonanceMult, double luckyReadMult, double serveBurst,
            double bustPenalty, BustKind bust, bool cleanServe, bool blindRead,
            int progress, int satisfaction, EmotionDelta committedDelta)
        {
            ResonanceMult = resonanceMult;
            LuckyReadMult = luckyReadMult;
            ServeBurst = serveBurst;
            BustPenalty = bustPenalty;
            Bust = bust;
            CleanServe = cleanServe;
            BlindRead = blindRead;
            Progress = progress;
            Satisfaction = satisfaction;
            CommittedDelta = committedDelta ?? EmotionDelta.Empty;
        }

        /// <summary>Nothing was served, or the mix was cancelled before it reached them.</summary>
        public static ResonanceResult None { get; } = new ResonanceResult(
            0, 0, 1, 0, BustKind.None, false, false, 0, 0, EmotionDelta.Empty);

        public override string ToString() =>
            IsBust ? $"BUST ({Bust}) -{BustPenalty} Mult"
            : CleanServe ? $"CLEAN SERVE ×{ServeBurst} (+{ResonanceMult} Mult, {Satisfaction} sat)"
            : $"+{ResonanceMult} Mult, progress {Progress}, {Satisfaction} sat";
    }
}
