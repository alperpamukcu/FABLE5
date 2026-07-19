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
        private const string ArchetypesPath = "Assets/Data/customers/archetypes.json";
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
            so.FindProperty("archetypesJson").objectReferenceValue = LoadRequired<TextAsset>(ArchetypesPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            var stage = game.AddComponent<LastCall.DebugUI.DiegeticStage>();
            var stageSo = new SerializedObject(stage);
            stageSo.FindProperty("displayFont").objectReferenceValue = LoadRequired<Font>(PixelDisplayFontPath);
            stageSo.FindProperty("bodyFont").objectReferenceValue = LoadRequired<Font>(PixelBodyFontPath);
            // Installed v2 pixel bottle sprites (18 §5 first batch); types without one fall
            // back to the flat placeholder silhouette.
            var bottles = new (LastCall.Core.IngredientType type, string path)[]
            {
                (LastCall.Core.IngredientType.Spirit,  "Assets/Art/Bottles/bottle_spirit.png"),
                (LastCall.Core.IngredientType.Bubbly,  "Assets/Art/Bottles/bottle_bubbly.png"),
                (LastCall.Core.IngredientType.Sweet,   "Assets/Art/Bottles/bottle_sweet.png"),
                (LastCall.Core.IngredientType.Sour,    "Assets/Art/Bottles/bottle_sour.png"),
                (LastCall.Core.IngredientType.Bitter,  "Assets/Art/Bottles/bottle_bitter.png"),
                (LastCall.Core.IngredientType.Garnish, "Assets/Art/Bottles/bottle_garnish.png"),
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
            // Per-ingredient bottles: every Assets/Art/Bottles/<id>.png that is not a
            // per-type fallback (bottle_*) is wired by its ingredient id.
            var byIdProp = stageSo.FindProperty("bottleById");
            var idPaths = new System.Collections.Generic.List<(string id, string path)>();
            foreach (var g in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/Bottles" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var fn = Path.GetFileNameWithoutExtension(path);
                if (fn.StartsWith("bottle_")) continue;   // skip the per-type fallbacks
                idPaths.Add((fn, path));
            }
            byIdProp.arraySize = idPaths.Count;
            for (int i = 0; i < idPaths.Count; i++)
            {
                var el = byIdProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("id").stringValue = idPaths[i].id;
                el.FindPropertyRelative("sprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(idPaths[i].path);
            }

            // ID photos, keyed by archetype id — the file name is the key (GDD 19 §9).
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
            // Environment art (18 §5): club background + bar counter. Optional — the stage
            // falls back to flat procedural layers when either is missing.
            stageSo.FindProperty("backgroundSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backgrounds/club_bg.png");
            stageSo.FindProperty("counterSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backgrounds/counter.png");
            stageSo.FindProperty("customerSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/vip_patron.png");
            stageSo.FindProperty("registerSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Props/register.png");
            stageSo.ApplyModifiedPropertiesWithoutUndo();

            var hud = game.AddComponent<DebugHud>();
            var hudSo = new SerializedObject(hud);
            // v2 HUD pixel pass: the whole overlay uses the pixel fonts now (Silkscreen for
            // body/headers, Press Start 2P for buttons/numbers via pixelFont).
            hudSo.FindProperty("displayFont").objectReferenceValue = LoadRequired<Font>(PixelBodyFontPath);
            hudSo.FindProperty("bodyFont").objectReferenceValue = LoadRequired<Font>(PixelBodyFontPath);
            hudSo.FindProperty("pixelFont").objectReferenceValue = LoadRequired<Font>(PixelBodyFontPath);
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
