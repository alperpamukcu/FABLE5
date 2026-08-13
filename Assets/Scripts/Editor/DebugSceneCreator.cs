using System.IO;
using LastCall.UI;
using LastCall.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        private const string FixturesPath = "Assets/Data/fixtures/fixtures.json";
        private const string PapersPath = "Assets/Data/customers/papers.json";
        private const string StoryPath = "Assets/Data/story/story.json";
        // v2 pixel fonts (16_ui_style_guide v2 §1: Press Start 2P headings/numbers,
        // Silkscreen body/caption — spec-sanctioned fallbacks for m6x11/m5x7). The legacy
        // Limelight/Barlow pair is gone: every slot wires a pixel font now.
        private const string PixelDisplayFontPath = "Assets/Fonts/PressStart2P-Regular.ttf";
        private const string PixelBodyFontPath = "Assets/Fonts/Silkscreen-Regular.ttf";
        /// <summary>The shop's heavier face (2026-08-07) — Silkscreen BOLD, same grid.</summary>
        private const string ShopFontPath = "Assets/Fonts/SilkscreenBold.ttf";

        [MenuItem("LastCall/Create Debug Scene")]
        public static void CreateDebugScene()
        {
            // Keep whatever the user had open instead of dropping unsaved edits.
            EditorSceneManager.SaveOpenScenes();

            // The tool owns exactly two roots — "Main Camera" and "Game" — and rebuilds
            // only those. Everything else in the scene is the AUTHOR'S (2026-08-07, the
            // level-design request): the StageDressing canvas and anything they placed by
            // hand survive every rebuild. A missing scene is still built from scratch.
            UnityEngine.SceneManagement.Scene scene;
            if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == "Main Camera" || root.name == "Game")
                        Object.DestroyImmediate(root);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            EnsureDressingRoot();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.03f, 0.075f); // Night 1 (#0D0813)
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // Pixel Perfect Camera (15 v2 §4, PATCH_15 §C): 640×360 reference, PPU 1 —
            // URP's OWN component now (2026-08-10), because the 2D Renderer is what draws
            // the world and the legacy package version is a stranger to it. PixelSnapping
            // rather than UpscaleRenderTexture: snapping keeps the world's visible width
            // equal to 360×aspect, which is EXACTLY the width the CanvasScaler gives the
            // overlay UI at match-height — the stage sprites and the HUD keep agreeing
            // about where things are at every window shape, the way the two canvases used
            // to. An upscaled RT would letterbox the world while the UI kept filling.
            var ppc = camGo.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            ppc.assetsPPU = 1;
            ppc.refResolutionX = 640;
            ppc.refResolutionY = 360;
            ppc.gridSnapping = UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.PixelSnapping;
            // NO CROP (2026-08-11): the camera fills the window and a wider one simply
            // shows more room. Windowboxing was tried for one commit and drew black bars,
            // which is not what a 2D game does with a monitor it did not choose. What holds
            // still instead is the SAFE FRAME the UI is built in (DesignFrame) — the room
            // grows, the game does not move.
            ppc.cropFrame = UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.None;

            // The bloom volume finally has somewhere to render: HDR light in the world.
            // The UI never blooms — overlay canvases draw after the camera entirely.
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var game = new GameObject("Game");
            var bootstrap = game.AddComponent<GameBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("deckJson").objectReferenceValue = LoadRequired<TextAsset>(DeckPath);
            so.FindProperty("recipesJson").objectReferenceValue = LoadRequired<TextAsset>(RecipesPath);
            so.FindProperty("archetypesJson").objectReferenceValue = LoadRequired<TextAsset>(ArchetypesPath);
            so.FindProperty("glasswareJson").objectReferenceValue = LoadRequired<TextAsset>(GlasswarePath);
            so.FindProperty("snacksJson").objectReferenceValue = LoadRequired<TextAsset>(SnacksPath);
            so.FindProperty("fixturesJson").objectReferenceValue = LoadRequired<TextAsset>(FixturesPath);
            so.FindProperty("papersJson").objectReferenceValue = LoadRequired<TextAsset>(PapersPath);
            so.FindProperty("storyJson").objectReferenceValue = LoadRequired<TextAsset>(StoryPath);
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
            hudSo.FindProperty("shopFont").objectReferenceValue = LoadRequired<Font>(ShopFontPath);
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

        // ── the author's own layer ──────────────────────────────────────────────

        /// <summary>
        /// The hand-editable scene layer (2026-08-07, the author: "sahne tasarımını
        /// sürükle bırak tarzı level design ile yapamaz mıyım"). A canvas the RUNTIME
        /// NEVER TOUCHES: it draws between the painted room (order −10) and the HUD
        /// (order 5), in the room's own 640×360 units, and the scene tool rebuilds
        /// around it. Whatever the author drags, moves or scales here is theirs.
        /// </summary>
        private static GameObject EnsureDressingRoot()
        {
            var existing = GameObject.Find("StageDressing");
            if (existing != null) return existing;

            var go = new GameObject("StageDressing",
                typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -5;   // over the room art, under every HUD element
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640, 360);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            return go;
        }

        /// <summary>
        /// Select PNG/sprite assets in the Project window, run this, and each lands on
        /// the dressing layer as an Image at its own native pixel size — then drag it
        /// where it belongs in the Scene view. The closest thing UGUI has to dropping
        /// a sprite straight into the world.
        /// </summary>
        [MenuItem("LastCall/Add Selected Sprites To Dressing")]
        public static void AddSelectedSpritesToDressing()
        {
            var root = EnsureDressingRoot();
            int added = 0;
            foreach (var obj in Selection.objects)
            {
                Sprite sprite = obj as Sprite;
                if (sprite == null && obj is Texture2D tex)
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(tex));
                if (sprite == null) continue;

                var go = new GameObject(sprite.name, typeof(RectTransform),
                    typeof(UnityEngine.UI.Image));
                go.transform.SetParent(root.transform, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = sprite.rect.size;                 // native pixels, no stretch
                rt.anchoredPosition = new Vector2(added * 12f, added * -12f); // stagger drops
                var img = go.GetComponent<UnityEngine.UI.Image>();
                img.sprite = sprite;
                img.raycastTarget = false;   // decor never eats a click meant for the game
                Undo.RegisterCreatedObjectUndo(go, "Add Dressing Sprite");
                added++;
            }
            if (added > 0) EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[LastCall] {added} sprite(s) added to StageDressing — drag them into place.");
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
