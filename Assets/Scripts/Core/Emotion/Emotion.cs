namespace LastCall.Core
{
    /// <summary>
    /// The six things a customer walks in carrying (GDD 19 §1). Declaration order is
    /// gameplay-relevant: it is the tie-break order when two stats share the top value,
    /// so the dominant emotion is always deterministic. Never reorder.
    /// </summary>
    public enum Emotion
    {
        Anger,
        Sadness,
        Fatigue,
        Excitement,
        Heartbreak,
        Anxiety
    }

    /// <summary>Which way the customer wants the needle moved (GDD 19 §1). Always visible.</summary>
    public enum IntentDirection
    {
        /// <summary>Toward 0 — they want it put out.</summary>
        Extinguish,

        /// <summary>Toward 100 — they want it fed.</summary>
        Fuel
    }

    public static class Emotions
    {
        /// <summary>All six, in tie-break order. Cached so hot paths don't re-allocate.</summary>
        public static readonly Emotion[] All =
        {
            Emotion.Anger, Emotion.Sadness, Emotion.Fatigue,
            Emotion.Excitement, Emotion.Heartbreak, Emotion.Anxiety
        };

        public const int Count = 6;

        /// <summary>The value an Extinguish/Fuel intent is aiming at.</summary>
        public static int TargetValue(IntentDirection direction) =>
            direction == IntentDirection.Extinguish ? 0 : 100;
    }
}
