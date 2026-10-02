#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LastCall.PlayTests.Trailer
{
    /// <summary>
    /// LastCall -> Trailer: films the trailer's shots (TrailerShots) through the test runner, which is what knows how
    /// to enter play mode, hand the scene a virtual mouse and take it away again. The editor must be out of play mode;
    /// the Game view must be able to show 1920x1080 (the shots pin it there and put it back after).
    /// </summary>
    public static class TrailerMenu
    {
        private const string Fixture = "LastCall.PlayTests.Trailer.TrailerShots";

        [MenuItem("LastCall/Trailer/Record All Shots")]
        public static void RecordAll() => Record(null);

        [MenuItem("LastCall/Trailer/Record One Shot...")]
        public static void RecordOne()
        {
            var shots = new GenericMenu();
            foreach (var m in typeof(TrailerShots).GetMethods())
            {
                if (m.GetCustomAttributes(typeof(UnityEngine.TestTools.UnityTestAttribute), false).Length == 0) continue;
                string name = m.Name;
                shots.AddItem(new GUIContent(name), false, () => Record(Fixture + "." + name));
            }
            shots.ShowAsContext();
        }

        [MenuItem("LastCall/Trailer/Record All Shots (Turkish)")]
        public static void RecordAllTurkish()
        {
            TrailerSwitch.Language = "tr";
            Record(null);
        }

        [MenuItem("LastCall/Trailer/Open Recordings Folder")]
        public static void Reveal()
        {
            Directory.CreateDirectory(TrailerCamera.Folder);
            EditorUtility.RevealInFinder(TrailerCamera.Folder);
        }

        private static void Record(string oneTest)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Trailer", "Leave play mode first - the shots enter it themselves.", "OK");
                return;
            }
            TrailerSwitch.On = true;
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Done());
            var filter = new Filter { testMode = TestMode.PlayMode };
            if (oneTest != null) filter.testNames = new[] { oneTest };
            else filter.groupNames = new[] { "^" + System.Text.RegularExpressions.Regex.Escape(Fixture) + "\\." };
            Debug.Log("[trailer] filming " + (oneTest ?? "every shot") + " into " + TrailerCamera.Folder);
            api.Execute(new ExecutionSettings(filter));
        }

        private sealed class Done : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite) return;
                Debug.Log($"[trailer] {result.Test.Name}: {result.TestStatus}"
                          + (string.IsNullOrEmpty(result.Message) ? "" : " - " + result.Message));
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                TrailerSwitch.On = false;
                TrailerSwitch.Language = LastCall.Core.Languages.Source;
                Debug.Log($"[trailer] done: {result.PassCount} filmed, {result.FailCount} failed. Films in {TrailerCamera.Folder}");
                EditorUtility.RevealInFinder(TrailerCamera.Folder);
            }
        }
    }
}
#endif
