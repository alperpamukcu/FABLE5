using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// The one control the pixel-exact look tests need from a human: permission to change what
    /// the game is supposed to look like.
    ///
    /// LookTests holds each screen against a blessed picture and fails on a single pixel. When
    /// a screen is MEANT to change, the blessed pictures are thrown away here and the next run
    /// of the suite writes them again — and that run fails on purpose, because a picture nobody
    /// has looked at is not a baseline, it is just the last thing that happened. Run it twice:
    /// once to draw, once to agree.
    /// </summary>
    public static class UiBaselines
    {
        private static string Dir =>
            Path.Combine(Application.dataPath, "Tests", "PlayMode", "Baselines~");

        private static string Failures =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "UiLooks"));

        [MenuItem("LastCall/Re-bless UI Baselines")]
        public static void Rebless()
        {
            if (!Directory.Exists(Dir))
            {
                Debug.Log($"[LastCall] No baselines to re-bless ({Dir} does not exist yet). " +
                          "Run the PlayMode suite and it will draw them.");
                return;
            }

            var pictures = Directory.GetFiles(Dir, "*.png");
            if (pictures.Length == 0)
            {
                Debug.Log("[LastCall] The baselines are already gone; the next PlayMode run draws them.");
                return;
            }

            // Named, not counted: the log line is the last chance to notice that a screen you
            // did not touch is about to be re-blessed along with the one you did.
            var names = new string[pictures.Length];
            for (int i = 0; i < pictures.Length; i++)
            {
                names[i] = Path.GetFileNameWithoutExtension(pictures[i]);
                File.Delete(pictures[i]);
            }

            Debug.Log($"[LastCall] Re-blessing {pictures.Length} UI baseline(s): {string.Join(", ", names)}.\n" +
                      "Run LastCall.PlayTests (PlayMode) TWICE: the first run draws the new pictures and " +
                      "fails on purpose, the second agrees with them. Look at them in between — " +
                      $"{Dir}");
        }

        [MenuItem("LastCall/Show Last UI Look Failures")]
        public static void ShowFailures()
        {
            if (!Directory.Exists(Failures))
            {
                Debug.Log("[LastCall] No look failures recorded — nothing has differed since the editor started.");
                return;
            }
            EditorUtility.RevealInFinder(Failures + Path.DirectorySeparatorChar);
        }
    }
}
