using System.Globalization;
using System.Threading;

namespace LastCall.Core
{
    /// <summary>
    /// The one culture the game formats and parses numbers in.
    ///
    /// What forced it was display: on a Turkish desktop the bar's rating printed as "3,0" and
    /// a percent came out "%75", where the invariant culture writes "75 %" — which is why four
    /// call sites were patching `:P0` with a string replace that only ever worked under one
    /// culture, and none of them was the one running. Invariant with one amendment, the
    /// percent after the number with no space, so every `:P0` reads "75%" and no call site has
    /// to know about it.
    ///
    /// It is set globally rather than passed to each `ToString` because the same setting also
    /// decides how "0.75" is READ. Nothing in Core parses a number by hand today — the data
    /// files go through JsonUtility, which is invariant on its own — but the day something
    /// does, a glass profile could come out meaning seventy-five instead of three quarters,
    /// and it would do it silently.
    ///
    /// Called from both places this code runs: play mode, and the editor — the simulator
    /// writes a checked-in report, which must not change by whose desktop generated it.
    /// </summary>
    public static class RunCulture
    {
        private static CultureInfo _culture;

        public static CultureInfo Culture
        {
            get
            {
                if (_culture == null)
                {
                    var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                    c.NumberFormat.PercentPositivePattern = 1;   // "n%"
                    c.NumberFormat.PercentNegativePattern = 1;   // "-n%"
                    _culture = c;
                }
                return _culture;
            }
        }

        /// <summary>Pins this thread, and every thread started after it, to that culture.</summary>
        public static void Pin()
        {
            CultureInfo.DefaultThreadCurrentCulture = Culture;
            CultureInfo.DefaultThreadCurrentUICulture = Culture;
            Thread.CurrentThread.CurrentCulture = Culture;
            Thread.CurrentThread.CurrentUICulture = Culture;
        }
    }
}
