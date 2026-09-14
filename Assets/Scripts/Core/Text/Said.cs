using System;

namespace LastCall.Core
{
    /// <summary>
    /// The sentence a refusal says on screen, riding on the exception it already throws (2026-09-13,
    /// localization L1). The rules keep their exception types and English messages — tests assert both
    /// — and attach a <see cref="Line"/> in <see cref="Exception.Data"/>, which the UI renders in the
    /// player's language:
    /// <code>throw Said.With(new InvalidOperationException("The till is short."), Line.Of("rule.till_short"));</code>
    /// </summary>
    public static class Said
    {
        public const string DataKey = "LastCall.Line";

        public static T With<T>(T exception, Line line) where T : Exception
        {
            exception.Data[DataKey] = line;
            return exception;
        }

        public static bool TryGet(Exception exception, out Line line)
        {
            if (exception != null && exception.Data.Contains(DataKey) && exception.Data[DataKey] is Line said)
            {
                line = said;
                return true;
            }
            line = default;
            return false;
        }
    }
}
