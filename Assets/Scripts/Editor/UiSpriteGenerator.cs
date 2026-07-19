using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// Generates the procedural UI kit (art bible 14, §2 palette + §3 light rule) into
    /// Assets/Art/UI. Every sprite is white and carries BAKED lighting — a bright rim toward
    /// the upper-left, shade toward the lower-right and a soft vertical falloff — so any
    /// runtime tint stays inside one lighting language. UI chrome is authored here by design;
    /// AI generation is reserved for illustrative content (GDD 14 §8.1). Rerunnable.
    ///
    /// Trimmed to the three sprites the HUD actually uses. The kit once also produced
    /// toast/bubble/tooltip/tag/frame/glow and a SmokeSwirl backdrop material, none of which
    /// anything referenced — the v2 diegetic stage replaced the backdrop, and the rest were
    /// speculative chrome for screens that were never built.
    /// </summary>
    public static class UiSpriteGenerator
    {
        private const string Folder = "Assets/Art/UI";

        [MenuItem("LastCall/Generate UI Sprites")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);

            WriteSprite("panel.png", Shape(64, 64, PanelSdf(14)), border: 20);
            WriteSprite("button.png", Shape(48, 48, PanelSdf(10)), border: 14);
            WriteSprite("vignette.png", Vignette(256), border: 0);

            AssetDatabase.SaveAssets();
            Debug.Log($"[LastCall] UI kit generated in {Folder}");
        }

        // ── signed distance shapes (negative = inside) ─────────────────────────

        private delegate float Sdf(float x, float y, int w, int h);

        private static Sdf PanelSdf(float radius) => (x, y, w, h) =>
            RoundedRect(x - w * 0.5f, y - h * 0.5f, w * 0.5f, h * 0.5f, radius);

        private static float RoundedRect(float px, float py, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(px) - (halfW - radius - 1f);
            float qy = Mathf.Abs(py) - (halfH - radius - 1f);
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude
                   + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
        }

        // ── rasterizer with the baked light rule ───────────────────────────────

        private static Color[] Shape(int w, int h, Sdf sdf)
        {
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // PNG rows are bottom-up in SetPixels; flip so "top" means visual top.
                float sy = h - 1 - y + 0.5f;
                float d = sdf(x + 0.5f, sy, w, h);
                float coverage = Mathf.Clamp01(0.5f - d);
                if (coverage <= 0f) { pixels[y * w + x] = Color.clear; continue; }

                // Art bible §3 on UI: bright rim toward the upper-left, shaded rim
                // toward the lower-right, candle-glow falloff down the body.
                float t = sy / h;
                float body = Mathf.Lerp(0.96f, 0.86f, t);
                float inside = -d;
                if (inside <= 2.0f)
                {
                    bool litSide = (x + 0.5f) + sy < (w + h) * 0.5f;
                    body = litSide ? 1f : 0.76f;
                }
                pixels[y * w + x] = new Color(1f, 1f, 1f, coverage * body);
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

        private static void WriteSprite(string fileName, Color[] pixels, int border)
        {
            // Every sprite the kit still produces is square, so the side is the root of the
            // pixel count. Non-square shapes went with the unused toast/bubble/tooltip/tag/
            // frame sprites and their per-file size table.
            int side = (int)Mathf.Sqrt(pixels.Length);
            var tex = new Texture2D(side, side, TextureFormat.RGBA32, false);
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

    }
}
