using System.IO;
using System.Text;
using UnityEngine;

namespace LastCall.PlayTests
{
    /// <summary>
    /// WHERE THE SUITE'S TIME GOES (2026-09-14, the author: "playmode testlerini daha optimize hızlı
    /// yapamaz mısın?"). The run took 272 s and the test runner only says when a test started, so
    /// every test writes one line to Temp/PlayTestTimes.txt: its total, how many frames the editor
    /// gave it, and the laps between the marks the shared doors set (the scene loaded, the run
    /// dealt, the doors open, the slip done, the market open). Measure before cutting a wait.
    /// </summary>
    internal static class SuiteClock
    {
        private static float _start, _last;
        private static int _frames;
        private static readonly StringBuilder Laps = new StringBuilder();

        public static string FilePath => Path.Combine(Application.dataPath, "..", "Temp", "PlayTestTimes.txt");

        public static void Start()
        {
            _start = _last = Time.realtimeSinceStartup;
            _frames = Time.frameCount;
            Laps.Clear();
        }

        public static void Mark(string what)
        {
            float now = Time.realtimeSinceStartup;
            Laps.Append(" | ").Append(what).Append(' ').Append((now - _last).ToString("0.00")).Append('s');
            _last = now;
        }

        public static void End(string test)
        {
            float now = Time.realtimeSinceStartup, total = now - _start;
            int frames = Time.frameCount - _frames;
            string line = $"{System.DateTime.Now:HH:mm:ss} {test} {total:0.0}s, {frames} frames "
                        + $"({(total > 0f ? frames / total : 0f):0} fps){Laps} | rest {now - _last:0.00}s";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.AppendAllText(FilePath, line + "\n");
            }
            catch (IOException) { }
            Debug.Log("[suite-time] " + line);
        }
    }
}
