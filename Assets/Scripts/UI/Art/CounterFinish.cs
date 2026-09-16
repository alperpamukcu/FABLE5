using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// THE COUNTER'S FINISHES (2026-09-16, the author: "Mevcut olan hariç hepsi eklenecek. Başlangıçta C olucak
    /// marketten 1 geliştirme satın alınarak masa rengi değiştirme özelliği gelecek ... Tezgah rengi değişince Built
    /// sahnesindeki tezgah arkaplanı da ona göre değişmeli. Ona göre de o sahnedeki UI renkleri de değişmeli").
    ///
    /// Nothing is drawn twice: the counter, the shutter and the bench's own counter tile are the author's drawings
    /// RECOLOURED by family — the pink frames, the purple back wall and the dark slab of counter.png, each mapped
    /// onto a finish's ramp by luminance rank, so every highlight and shadow lands where the author put it. The
    /// same spec carries the bench's chrome: the accent its rims, needles, marks and fills take, and the slab tone
    /// its recesses and counter are cut from. Core holds the finish (TycoonRun.CounterFinish); this only paints it.
    /// </summary>
    public static class CounterFinish
    {
        public sealed class Spec
        {
            public string Id;
            public Color32[] Frame, Back, Slab;   // ramps, dark to light
            public Color Accent, Rim;             // the bench's chrome
            public Color32 Counter;               // the bench counter's slab tone
        }

        private static Color32 H(int v) => new Color32((byte)(v >> 16), (byte)((v >> 8) & 255), (byte)(v & 255), 255);
        private static Color C(int v) { var c = H(v); return new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f); }

        // The three families as counter.png draws them (measured 2026-09-16, Tools: scratchpad/backbar_preview.py).
        private static readonly Color32[] SrcFrame = { H(0xF76FCC), H(0xE168BB), H(0xD77BBA), H(0xB7699F), H(0xB65497), H(0xA8488A), H(0x975885), H(0x77476B), H(0x750050), H(0x573650), H(0x372536) };
        private static readonly Color32[] SrcBack = { H(0x4A3160), H(0x362447) };
        private static readonly Color32[] SrcSlab = { H(0x1F1924), H(0x17141C), H(0x292630), H(0x312E3A), H(0x111119), H(0x14111A), H(0x1D1721), H(0x0D0D14), H(0x110E14), H(0x39343E), H(0x474747) };

        private static readonly Dictionary<string, Spec> Specs = new Dictionary<string, Spec>
        {
            // C on the author's page: the frames as drawn, the back wall darker so the bottles stand out.
            ["neon"] = new Spec { Id = "neon",
                Frame = new[] { H(0x372536), H(0x573650), H(0x750050), H(0x77476B), H(0x975885), H(0xA8488A), H(0xB65497), H(0xB7699F), H(0xD77BBA), H(0xE168BB), H(0xF76FCC) },
                Back = new[] { H(0x16101D), H(0x241830) }, Slab = new[] { H(0x0D0D14), H(0x1F1924), H(0x39343E) },
                Accent = C(0xE168BB), Rim = C(0xB65497), Counter = H(0x1F1924) },
            ["walnut"] = new Spec { Id = "walnut",
                Frame = new[] { H(0x4A2A12), H(0x7A4A1E), H(0xB07A2E), H(0xD9A94A), H(0xF2D27A) },
                Back = new[] { H(0x2A1A14), H(0x3B2519) }, Slab = new[] { H(0x0D0B0C), H(0x1B1618), H(0x2E2629) },
                Accent = C(0xD9A94A), Rim = C(0xB07A2E), Counter = H(0x1B1618) },
            ["pine"] = new Spec { Id = "pine",
                Frame = new[] { H(0x0F3A34), H(0x1B5C52), H(0x2E8C7A), H(0x58BFA6), H(0x9FE3CD) },
                Back = new[] { H(0x141C2E), H(0x1F2B44) }, Slab = new[] { H(0x0B0D12), H(0x171A22), H(0x262B36) },
                Accent = C(0x58BFA6), Rim = C(0x2E8C7A), Counter = H(0x171A22) },
            ["cream"] = new Spec { Id = "cream",
                Frame = new[] { H(0x5A3A2A), H(0x9C6B4A), H(0xD9B48C), H(0xF0DCBE), H(0xFFF3E0) },
                Back = new[] { H(0x3A1420), H(0x55202F) }, Slab = new[] { H(0x120C10), H(0x221820), H(0x35262F) },
                Accent = C(0xD9B48C), Rim = C(0x9C6B4A), Counter = H(0x221820) },
            ["copper"] = new Spec { Id = "copper",
                Frame = new[] { H(0x4A2418), H(0x8A4A2C), H(0xC4763F), H(0xE8A063), H(0xFFCF9A) },
                Back = new[] { H(0x101A33), H(0x19264A) }, Slab = new[] { H(0x090B14), H(0x141827), H(0x232A40) },
                Accent = C(0xE8A063), Rim = C(0xC4763F), Counter = H(0x141827) },
        };

        /// <summary>The finish the bench chrome is drawn in — set by the HUD off the run before the benches build.</summary>
        public static Spec Current { get; private set; } = Specs[TycoonRun.CounterFinishes[0]];

        public static Spec Of(string id) => Specs.TryGetValue(id ?? "", out var s) ? s : Specs[TycoonRun.CounterFinishes[0]];

        public static void Set(string id) => Current = Of(id);

        private static float Lum(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        private static Color32 Lerp(Color32 a, Color32 b, float t) => new Color32(
            (byte)Mathf.RoundToInt(a.r + (b.r - a.r) * t), (byte)Mathf.RoundToInt(a.g + (b.g - a.g) * t),
            (byte)Mathf.RoundToInt(a.b + (b.b - a.b) * t), 255);

        /// <summary>A colour on a ramp, dark to light, at t 0..1.</summary>
        public static Color32 Ramp(Color32[] stops, float t)
        {
            int n = stops.Length - 1;
            if (n <= 0) return stops[0];
            t = Mathf.Clamp01(t);
            int k = Mathf.Min(n - 1, (int)(t * n));
            return Lerp(stops[k], stops[k + 1], t * n - k);
        }

        private static int Key(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        private static void MapFamily(Dictionary<int, Color32> map, Color32[] src, Color32[] ramp)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var c in src) { float l = Lum(c); if (l < lo) lo = l; if (l > hi) hi = l; }
            foreach (var c in src)
            {
                float t = hi <= lo ? 0.5f : (Lum(c) - lo) / (hi - lo);
                map[Key(c)] = Ramp(ramp, t);
            }
        }

        private static readonly Dictionary<string, Dictionary<int, Color32>> Maps = new Dictionary<string, Dictionary<int, Color32>>();

        private static Dictionary<int, Color32> MapFor(Spec s, bool slabOnly)
        {
            string key = s.Id + (slabOnly ? ":slab" : ":all");
            if (Maps.TryGetValue(key, out var m)) return m;
            m = new Dictionary<int, Color32>();
            if (!slabOnly) { MapFamily(m, SrcFrame, s.Frame); MapFamily(m, SrcBack, s.Back); }
            MapFamily(m, SrcSlab, s.Slab);
            return Maps[key] = m;
        }

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>The drawing in this finish: the counter, the shutter — every family remapped. Same rect, pivot
        /// and pixels per unit, so nothing measured off the original moves.</summary>
        public static Sprite Recolour(Sprite src, string id) => Repaint(src, id, false);

        /// <summary>Only the slab family remapped: the bench's own counter drawing, which carries no frames.</summary>
        public static Sprite RecolourSlab(Sprite src, string id) => Repaint(src, id, true);

        private static Sprite Repaint(Sprite src, string id, bool slabOnly)
        {
            if (src == null) return null;
            var spec = Of(id);
            string key = src.GetInstanceID() + ":" + spec.Id + (slabOnly ? ":slab" : "");
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            var map = MapFor(spec, slabOnly);
            var tex = src.texture;
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a != 0 && map.TryGetValue(Key(c), out var to)) px[i] = new Color32(to.r, to.g, to.b, c.a);
            }
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false)
            {
                filterMode = tex.filterMode, wrapMode = tex.wrapMode, name = tex.name + "_" + spec.Id,
            };
            copy.SetPixels32(px);
            copy.Apply(false, false);
            var sprite = Sprite.Create(copy, src.rect, new Vector2(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height),
                src.pixelsPerUnit, 0, SpriteMeshType.FullRect, src.border);
            sprite.name = src.name + "_" + spec.Id;
            return Cache[key] = sprite;
        }

        /// <summary>A swatch for the market: a piece of the counter in this finish — the slab's edge, the lip, a bay's
        /// frame and back wall — 140 wide by 70, cut from the recoloured drawing.</summary>
        public static Sprite Swatch(Sprite counter, string id)
        {
            var whole = Recolour(counter, id);
            if (whole == null) return null;
            string key = "swatch:" + whole.name;
            if (Cache.TryGetValue(key, out var got) && got != null) return got;
            float w = Mathf.Min(140f, whole.rect.width), h = Mathf.Min(70f, whole.rect.height);
            // 24 rows down from the top: the slab's edge, the lip, and the first bay's frame and back wall — the
            // three families in one cut (the top rows alone were slab).
            var r = new Rect(whole.rect.x + Mathf.Min(200f, whole.rect.width - w), whole.rect.y + Mathf.Max(0f, whole.rect.height - h - 24f), w, h);
            var s = Sprite.Create(whole.texture, r, new Vector2(0.5f, 0.5f), whole.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            s.name = key;
            return Cache[key] = s;
        }

        /// <summary>The bench's recess in the current finish: cut from the slab ramp.</summary>
        public static Sprite Recess() =>
            ChromeArt.CounterRecess(Ramp(Current.Slab, 0.35f), Ramp(Current.Slab, 0.02f), Ramp(Current.Slab, 0.9f), Current.Id);
    }
}
