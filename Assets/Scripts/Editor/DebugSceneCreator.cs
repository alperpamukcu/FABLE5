using System.IO;
using LastCall.DebugUI;
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
        private const string DeckPath = "Assets/Data/decks/classic_bar.json";
        private const string RecipesPath = "Assets/Data/recipes/recipes.json";
        private const string PatronsPath = "Assets/Data/patrons/patrons.json";
        private const string ToolsPath = "Assets/Data/tools/tools.json";
        private const string VipsPath = "Assets/Data/vips/vips.json";
        private const string VouchersPath = "Assets/Data/vouchers/vouchers.json";
        // Legacy HUD overlay fonts (temporary, cozy-noir — do not polish). The diegetic
        // pixel stage uses the v2 pixel fonts below.
        private const string LegacyDisplayFontPath = "Assets/Fonts/Limelight-Regular.ttf";
        private const string LegacyBodyFontPath = "Assets/Fonts/Barlow-Regular.ttf";
        // v2 pixel fonts (16_ui_style_guide v2 §1: Press Start 2P headings/numbers,
        // Silkscreen body/caption — spec-sanctioned fallbacks for m6x11/m5x7).
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
            so.FindProperty("patronsJson").objectReferenceValue = LoadRequired<TextAsset>(PatronsPath);
            so.FindProperty("toolsJson").objectReferenceValue = LoadRequired<TextAsset>(ToolsPath);
            so.FindProperty("vipsJson").objectReferenceValue = LoadRequired<TextAsset>(VipsPath);
            so.FindProperty("vouchersJson").objectReferenceValue = LoadRequired<TextAsset>(VouchersPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            var stage = game.AddComponent<LastCall.DebugUI.DiegeticStage>();
            var stageSo = new SerializedObject(stage);
            stageSo.FindProperty("displayFont").objectReferenceValue = LoadRequired<Font>(PixelDisplayFontPath);
            stageSo.FindProperty("bodyFont").objectReferenceValue = LoadRequired<Font>(PixelBodyFontPath);
            // Installed v2 pixel bottle sprites (18 §5 first batch); types without one fall
            // back to the flat placeholder silhouette.
            var bottles = new (LastCall.Core.IngredientType type, string path)[]
            {
                (LastCall.Core.IngredientType.Spirit, "Assets/Art/Bottles/bottle_spirit.png"),
                (LastCall.Core.IngredientType.Bubbly, "Assets/Art/Bottles/bottle_bubbly.png"),
                (LastCall.Core.IngredientType.Sweet,  "Assets/Art/Bottles/bottle_sweet.png"),
            };
            var spritesProp = stageSo.FindProperty("bottleSprites");
            spritesProp.arraySize = bottles.Length;
            for (int i = 0; i < bottles.Length; i++)
            {
                var el = spritesProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("type").intValue = (int)bottles[i].type;
                el.FindPropertyRelative("sprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(bottles[i].path);
            }
            stageSo.ApplyModifiedPropertiesWithoutUndo();

            var hud = game.AddComponent<DebugHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("displayFont").objectReferenceValue = LoadRequired<Font>(LegacyDisplayFontPath);
            hudSo.FindProperty("bodyFont").objectReferenceValue = LoadRequired<Font>(LegacyBodyFontPath);
            // UI kit is optional: the HUD falls back to flat colors when unwired.
            hudSo.FindProperty("panelSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/panel.png");
            hudSo.FindProperty("buttonSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/button.png");
            hudSo.FindProperty("vignetteSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/vignette.png");
            hudSo.FindProperty("backgroundMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/UI/SmokeSwirl.mat");
            hudSo.FindProperty("art").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<LastCall.Game.ArtLibrary>("Assets/Art/ArtLibrary.asset");
            hudSo.FindProperty("stage").objectReferenceValue = stage;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

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
