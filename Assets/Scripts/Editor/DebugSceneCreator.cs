using System.IO;
using LastCall.UI;
using LastCall.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;

namespace LastCall.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Main.unity from scratch: camera + a "Game" object carrying
    /// GameBootstrap (data assets wired) and DebugHud. Rerunnable — overwrites the scene.
    /// </summary>
    public static class DebugSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string DeckPath = "Assets/Data/bottles/base_bar.json";
        private const string RecipesPath = "Assets/Data/recipes/recipes.json";
        private const string ArchetypesPath = "Assets/Data/customers/archetypes.json";
        private const string GlasswarePath = "Assets/Data/glassware/glassware.json";
        private const string SnacksPath = "Assets/Data/snacks/snacks.json";
        // v2 pixel fonts (16_ui_style_guide v2 §1: Press Start 2P headings/numbers,
        // Silkscreen body/caption — spec-sanctioned fallbacks for m6x11/m5x7). The legacy
        // Limelight/Barlow pair is gone: every slot wires a pixel font now.
        private const string PixelDisplayFontPath = "Assets/Fonts/PressStart2P-Regular.ttf";
        private const string PixelBodyFontPath = "Assets/Fonts/Silkscreen-Regular.ttf";

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
            cam.backgroundColor = new Color(0.05f, 0.03f, 0.075f); // Night 1 (#0D0813)
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // Pixel Perfect Camera (15 v2 §4, PATCH_15 §C): 640×360 reference, integer
            // scaling only (×2, ×3 = 1080p), PPU 1 to match the /Art/ importer. Governs
            // future world-space sprites; the UI canvases integer-scale via CanvasScaler.
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 1;
            ppc.refResolutionX = 640;
            ppc.refResolutionY = 360;
            ppc.upscaleRT = true;      // render at 640×360, then integer-upscale
            ppc.pixelSnapping = true;  // snap sprites to the pixel grid

            var game = new GameObject("Game");
            var bootstrap = game.AddComponent<GameBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("deckJson").objectReferenceValue = LoadRequired<TextAsset>(DeckPath);
            so.FindProperty("recipesJson").objectReferenceValue = LoadRequired<TextAsset>(RecipesPath);
            so.FindProperty("archetypesJson").objectReferenceValue = LoadRequired<TextAsset>(ArchetypesPath);
            so.FindProperty("glasswareJson").objectReferenceValue = LoadRequired<TextAsset>(GlasswarePath);
            so.FindProperty("snacksJson").objectReferenceValue = LoadRequired<TextAsset>(SnacksPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            // The stage carries only what the player still sees (2026-08-07 sweep): the room,
            // the counter, the till and the licence portraits. The old rail bottles, the
            // pour-glass pair and the solo customer left with their stage code.
            var stage = game.AddComponent<LastCall.UI.DiegeticStage>();
            var stageSo = new SerializedObject(stage);
            stageSo.FindProperty("displayFont").objectReferenceValue = LoadRequired<Font>(PixelDisplayFontPath);

            // ID photos, keyed by archetype id — the file name is the key.
            var portraitProp = stageSo.FindProperty("portraits");
            var portraitPaths = new System.Collections.Generic.List<(string id, string path)>();
            foreach (var g in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/Portraits/Archetypes" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                portraitPaths.Add((Path.GetFileNameWithoutExtension(path), path));
            }
            portraitProp.arraySize = portraitPaths.Count;
            for (int i = 0; i < portraitPaths.Count; i++)
            {
                var el = portraitProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("archetypeId").stringValue = portraitPaths[i].id;
                el.FindPropertyRelative("sprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(portraitPaths[i].path);
            }
            // Environment art (18 §5): club background + the bar, whose empty shelves are where
            // bought glassware will stand. Optional — the stage falls back to flat procedural
            // layers when either is missing.
            stageSo.FindProperty("backgroundSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backgrounds/club_room.png");
            stageSo.FindProperty("counterSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backgrounds/counter.png");
            stageSo.FindProperty("registerSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Props/register2.png");
            stageSo.ApplyModifiedPropertiesWithoutUndo();

            // The tycoon loop is the only loop now (the card-era HUD and run were demolished
            // in P7). TycoonHud renders and drives the run.
            var hud = game.AddComponent<TycoonHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("bodyFont").objectReferenceValue = LoadRequired<Font>(PixelBodyFontPath);
            hudSo.FindProperty("displayFont").objectReferenceValue = LoadRequired<Font>(PixelDisplayFontPath);
            hudSo.FindProperty("stage").objectReferenceValue = stage;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // P4 (GDD 24 §1–3): the menu / shaker / serve flow.
            var flow = game.AddComponent<TycoonServiceFlow>();
            var flowSo = new SerializedObject(flow);
            flowSo.FindProperty("bodyFont").objectReferenceValue = LoadRequired<Font>(PixelBodyFontPath);
            flowSo.FindProperty("displayFont").objectReferenceValue = LoadRequired<Font>(PixelDisplayFontPath);
            flowSo.ApplyModifiedPropertiesWithoutUndo();

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
