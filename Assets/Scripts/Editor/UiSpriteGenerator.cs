using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// Generates the procedural "cozy noir" UI kit (GDD 12.1) into Assets/Art/UI:
    /// anti-aliased rounded panels/buttons (white, tinted at runtime), a screen
    /// vignette, a soft glow, and the SmokeSwirl background material. Rerunnable.
    /// </summary>
    public static class UiSpriteGenerator
    {
        private const string Folder = "Assets/Art/UI";

        [MenuItem("LastCall/Generate UI Sprites")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);

            WriteSprite("panel.png", RoundedRect(64, 64, 14, rimBoost: true), border: 20);
            WriteSprite("button.png", RoundedRect(48, 48, 10, rimBoost: false, bottomShade: true), border: 14);
            WriteSprite("vignette.png", Vignette(256), border: 0);
            WriteSprite("glow.png", RadialGlow(128), border: 0);
            CreateBackgroundMaterial();

            AssetDatabase.SaveAssets();
            Debug.Log($"[LastCall] UI kit generated in {Folder}");
        }

        /// <summary>White rounded rect via a signed-distance alpha with 1px AA.</summary>
        private static Color[] RoundedRect(int w, int h, float radius,
            bool rimBoost, bool bottomShade = false)
        {
            var pixels = new Color[w * h];
            var half = new Vector2(w * 0.5f, h * 0.5f);
            var inner = half - new Vector2(radius + 1f, radius + 1f);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f - half.x, y + 0.5f - half.y);
                var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - inner;
                float dist = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude
                             + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
                float alpha = Mathf.Clamp01(0.5f - dist);

                // A faintly translucent body makes the 2px rim read as a brighter
                // accent once the sprite is tinted.
                float body = rimBoost && dist < -2.5f ? 0.90f : 1f;
                if (bottomShade) body *= Mathf.Lerp(0.82f, 1f, (float)y / h);
                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha * body);
            }
            return pixels;
        }

        /// <summary>Transparent center falling off to soft darkness at the screen edges.</summary>
        private static Color[] Vignette(int size)
        {
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - half) / half;
                float ny = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.65f)) * 0.5f;
                pixels[y * size + x] = new Color(0.02f, 0.01f, 0.05f, a);
            }
            return pixels;
        }

        private static Color[] RadialGlow(int size)
        {
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - half) / half;
                float ny = (y + 0.5f - half) / half;
                float d = Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny));
                float a = Mathf.Pow(1f - d, 2.2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return pixels;
        }

        private static void WriteSprite(string fileName, Color[] pixels, int border)
        {
            int size = (int)Mathf.Sqrt(pixels.Length);
            int w = size, h = pixels.Length / size;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            string path = $"{Folder}/{fileName}";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border > 0 ? new Vector4(border, border, border, border) : Vector4.zero;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void CreateBackgroundMaterial()
        {
            var shader = Shader.Find("LastCall/UI/SmokeSwirl");
            if (shader == null)
            {
                Debug.LogWarning("[LastCall] SmokeSwirl shader not found; background material skipped.");
                return;
            }
            string path = $"{Folder}/SmokeSwirl.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
        }
    }
}
