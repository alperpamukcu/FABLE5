using System.IO;
using LastCall.DebugUI;
using LastCall.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Main.unity from scratch: camera + a "Game" object carrying
    /// GameBootstrap (data assets wired) and DebugHud. Rerunnable — overwrites the scene.
    /// </summary>
    public static class DebugSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string DeckPath = "Assets/Data/decks/classic_bar.json";
        private const string RecipesPath = "Assets/Data/recipes/recipes.json";
        private const string PatronsPath = "Assets/Data/patrons/patrons.json";
        private const string ToolsPath = "Assets/Data/tools/tools.json";
        private const string VipsPath = "Assets/Data/vips/vips.json";
        private const string VouchersPath = "Assets/Data/vouchers/vouchers.json";

        [MenuItem("LastCall/Create Debug Scene")]
        public static void CreateDebugScene()
        {
            // Keep whatever the user had open instead of dropping unsaved edits.
            EditorSceneManager.SaveOpenScenes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.07f, 0.10f);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            var game = new GameObject("Game");
            var bootstrap = game.AddComponent<GameBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("deckJson").objectReferenceValue = LoadRequired<TextAsset>(DeckPath);
            so.FindProperty("recipesJson").objectReferenceValue = LoadRequired<TextAsset>(RecipesPath);
            so.FindProperty("patronsJson").objectReferenceValue = LoadRequired<TextAsset>(PatronsPath);
            so.FindProperty("toolsJson").objectReferenceValue = LoadRequired<TextAsset>(ToolsPath);
            so.FindProperty("vipsJson").objectReferenceValue = LoadRequired<TextAsset>(VipsPath);
            so.FindProperty("vouchersJson").objectReferenceValue = LoadRequired<TextAsset>(VouchersPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            game.AddComponent<DebugHud>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[LastCall] Debug scene created at {ScenePath}");
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new FileNotFoundException($"Required asset missing: {path}");
            return asset;
        }
    }
}
