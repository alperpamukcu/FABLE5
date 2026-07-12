using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// Generates the procedural cozy-noir UI kit (art bible 14, §2 palette + §3 light
    /// rule) into Assets/Art/UI. Every sprite is white and carries BAKED lighting —
    /// a bright rim toward the upper-left, shade toward the lower-right and a soft
    /// vertical falloff — so any runtime tint stays inside one lighting language.
    /// UI chrome is authored here by design; AI generation is reserved for
    /// illustrative content (GDD 14 §8.1). Rerunnable.
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
            WriteSprite("toast.png", Shape(128, 48, PanelSdf(12), topGlow: true), border: 16);
            WriteSprite("bubble.png", Shape(128, 96, BubbleSdf()), border: 0);
            WriteSprite("tooltip.png", Shape(128, 80, TooltipSdf()), border: 0);
            WriteSprite("tag.png", Shape(96, 56, TagSdf()), border: 0);
            WriteSprite("frame.png", Shape(96, 112, FrameSdf()), border: 0);
            WriteSprite("vignette.png", Vignette(256), border: 0);
            WriteSprite("glow.png", RadialGlow(128), border: 0);
            CreateBackgroundMaterial();

            AssetDatabase.SaveAssets();
            Debug.Log($"[LastCall] UI kit generated in {Folder}");
        }

        // ── signed distance shapes (negative = inside) ─────────────────────────

        private delegate float Sdf(float x, float y, int w, int h);

        private static Sdf PanelSdf(float radius) => (x, y, w, h) =>
            RoundedRect(x - w * 0.5f, y - h * 0.5f, w * 0.5f, h * 0.5f, radius);

        /// <summary>Speech bubble: rounded body with a tail toward the lower-left.</summary>
        private static Sdf BubbleSdf() => (x, y, w, h) =>
        {
            float body = RoundedRect(x - w * 0.5f, y - (h - 26) * 0.5f, w * 0.5f, (h - 26) * 0.5f, 16);
            float tail = Triangle(x, y, new Vector2(30, h - 30), new Vector2(58, h - 30), new Vector2(24, h - 4));
            return Mathf.Min(body, tail);
        };

        /// <summary>Tooltip: rounded box with a pointer notch on the left edge.</summary>
        private static Sdf TooltipSdf() => (x, y, w, h) =>
        {
            float body = RoundedRect(x - (w + 14) * 0.5f, y - h * 0.5f, (w - 14) * 0.5f, h * 0.5f, 12);
            float notch = Triangle(x, y, new Vector2(16, h * 0.5f - 12), new Vector2(16, h * 0.5f + 12), new Vector2(2, h * 0.5f));
            return Mathf.Min(body, notch);
        };

        /// <summary>Shop tag: rectangle with a pointed left end and a punched hole.</summary>
        private static Sdf TagSdf() => (x, y, w, h) =>
        {
            float box = RoundedRect(x - (w + 22) * 0.5f, y - h * 0.5f, (w - 22) * 0.5f, h * 0.5f, 8);
            float point = Triangle(x, y, new Vector2(23, 4), new Vector2(23, h - 4), new Vector2(2, h * 0.5f));
            float shape = Mathf.Min(box, point);
            float hole = Vector2.Distance(new Vector2(x, y), new Vector2(30, h * 0.5f)) - 5f;
            return Mathf.Max(shape, -hole); // subtract the hole
        };

        /// <summary>Photo frame: ring only, with the classic wider bottom border.</summary>
        private static Sdf FrameSdf() => (x, y, w, h) =>
        {
            float outer = RoundedRect(x - w * 0.5f, y - h * 0.5f, w * 0.5f, h * 0.5f, 6);
            // window: 10 px sides/top, 30 px bottom -> its center sits 10 px above middle
            float window = RoundedRect(x - w * 0.5f, y - (h * 0.5f - 10f),
                w * 0.5f - 10f, (h - 40f) * 0.5f, 4);
            return Mathf.Max(outer, -window); // ring = outer minus window
        };

        private static float RoundedRect(float px, float py, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(px) - (halfW - radius - 1f);
            float qy = Mathf.Abs(py) - (halfH - radius - 1f);
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude
                   + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
        }

        private static float Triangle(float px, float py, Vector2 a, Vector2 b, Vector2 c)
        {
            var p = new Vector2(px, py);
            Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
            Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / e0.sqrMagnitude);
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / e1.sqrMagnitude);
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / e2.sqrMagnitude);
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            var d = Vector2.Min(Vector2.Min(
                    new Vector2(pq0.sqrMagnitude, s * (v0.x * e0.y - v0.y * e0.x)),
                    new Vector2(pq1.sqrMagnitude, s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(pq2.sqrMagnitude, s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }

        // ── rasterizer with the baked light rule ───────────────────────────────

        private static Color[] Shape(int w, int h, Sdf sdf, bool topGlow = false)
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
                if (topGlow && sy <= 3.5f) body = 1f;
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
            int area = pixels.Length;
            // dimensions are threaded through Shape callers; recover from the callers'
            // conventions: width is encoded by the generator call, so infer via border
            // metadata below instead of the pixel count when non-square.
            (int w, int h) = SizeOf(fileName, area);
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

        private static (int, int) SizeOf(string fileName, int area)
        {
            switch (fileName)
            {
                case "toast.png": return (128, 48);
                case "bubble.png": return (128, 96);
                case "tooltip.png": return (128, 80);
                case "tag.png": return (96, 56);
                case "frame.png": return (96, 112);
                default:
                    int s = (int)Mathf.Sqrt(area);
                    return (s, s);
            }
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
