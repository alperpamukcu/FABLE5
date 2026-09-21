using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastCall.EditorTools
{
    /// <summary>
    /// Switches the project's URP renderer to the 2D Renderer, which is the ONE thing
    /// Light2D needs and the project did not have (measured 2026-08-10: both quality
    /// levels pointed at a UniversalRendererData — the 3D forward path — and there was
    /// no Renderer2DData asset anywhere, so 2D lights had nothing to render them).
    ///
    /// Also authors the game's own post volume. The RP assets used to point at URP's
    /// SampleSceneProfile — bloom, NEUTRAL TONEMAPPING and a vignette, none of it ours,
    /// all of it dormant only because no camera had post-processing on. Turning post on
    /// against that profile would have quietly regraded the whole stage; the palette is
    /// tokened (GDD 14 §5) and tonemapping is a regrade. The game's profile is bloom
    /// alone, thresholded above 1 so ONLY HDR light (lamps, neon) blooms and the painted
    /// art stays exactly the colour it was painted.
    ///
    /// Rerunnable: creates what is missing, rewires what is there.
    /// </summary>
    public static class Lighting2DSetup
    {
        private const string RendererPath = "Assets/Settings/Renderer2D.asset";
        private const string VolumePath = "Assets/Settings/LastCallVolume.asset";
        private static readonly string[] RpAssets =
        {
            "Assets/Settings/PC_RPAsset.asset",
            "Assets/Settings/Mobile_RPAsset.asset",
        };

        /// <summary>
        /// THE GRADE ON THE GAME'S OWN VOLUME (2026-09-21, LightLanguage): colour adjustments, lift/gamma/gain and a
        /// vignette from the language's numbers, written onto LastCallVolume - the profile both RP assets point at,
        /// which is what the room's camera reads once post-processing is on. The bloom stays in the profile but
        /// INACTIVE: with HDR off nothing could cross its threshold, and its passes are the expensive ones. Rerunnable;
        /// the numbers live in code, the asset only carries them.
        /// </summary>
        [MenuItem("LastCall/Setup Light Grade")]
        public static void SetupGrade()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null) { Run(); profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath); }
            if (profile == null) { Debug.LogWarning("[LastCall] no volume profile to grade"); return; }
            if (profile.TryGet<Bloom>(out var bloom)) bloom.active = false;
            if (!profile.TryGet<ColorAdjustments>(out var ca))
            {
                ca = profile.Add<ColorAdjustments>(overrides: false);
                AssetDatabase.AddObjectToAsset(ca, profile);
            }
            ca.active = true;
            ca.contrast.Override(LastCall.UI.LightLanguage.GradeContrast);
            ca.saturation.Override(LastCall.UI.LightLanguage.GradeSaturation);
            if (!profile.TryGet<LiftGammaGain>(out var lgg))
            {
                lgg = profile.Add<LiftGammaGain>(overrides: false);
                AssetDatabase.AddObjectToAsset(lgg, profile);
            }
            lgg.active = true;
            lgg.lift.Override(LastCall.UI.LightLanguage.GradeLift);
            lgg.gamma.Override(LastCall.UI.LightLanguage.GradeGamma);
            lgg.gain.Override(LastCall.UI.LightLanguage.GradeGain);
            if (!profile.TryGet<Vignette>(out var vig))
            {
                vig = profile.Add<Vignette>(overrides: false);
                AssetDatabase.AddObjectToAsset(vig, profile);
            }
            vig.active = true;
            vig.intensity.Override(LastCall.UI.LightLanguage.VignetteIntensity);
            vig.smoothness.Override(LastCall.UI.LightLanguage.VignetteSmoothness);
            vig.color.Override(LastCall.UI.LightLanguage.VignetteColor);
            EditorUtility.SetDirty(profile);
            foreach (var c in profile.components) if (c != null) EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            Debug.Log("[LastCall] the light grade is on LastCallVolume: contrast " + LastCall.UI.LightLanguage.GradeContrast
                + ", saturation " + LastCall.UI.LightLanguage.GradeSaturation + ", vignette " + LastCall.UI.LightLanguage.VignetteIntensity);
        }

        [MenuItem("LastCall/Setup 2D Lighting")]
        public static void Run()
        {
            // ── the 2D renderer ────────────────────────────────────────────────
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                // The create-menu callback does this reload; CreateInstance alone leaves
                // every shader reference null and the renderer draws nothing.
                ResourceReloader.ReloadAllNullIn(renderer, "Packages/com.unity.render-pipelines.universal");
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            // ── the game's own volume: bloom only ──────────────────────────────
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
                var bloom = profile.Add<Bloom>(overrides: true);
                bloom.intensity.Override(0.35f);
                bloom.threshold.Override(1.1f);   // above LDR: painted art can never bloom
                bloom.scatter.Override(0.6f);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }

            // ── point both RP assets at them ───────────────────────────────────
            foreach (var path in RpAssets)
            {
                var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (rp == null) { Debug.LogWarning($"[LastCall] RP asset missing: {path}"); continue; }
                var so = new SerializedObject(rp);
                var list = so.FindProperty("m_RendererDataList");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                so.FindProperty("m_DefaultRendererIndex").intValue = 0;
                so.FindProperty("m_VolumeProfile").objectReferenceValue = profile;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rp);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[LastCall] 2D lighting: Renderer2D + LastCallVolume wired into both RP assets.");
        }
    }
}
