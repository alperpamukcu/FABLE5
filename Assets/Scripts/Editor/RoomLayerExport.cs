using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using LastCall.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastCall.EditorTools
{
    /// <summary>
    /// THE ROOM, TAKEN APART (2026-09-12, the author: "tüm geliştirmeleri ağaç şeklinde html
    /// olarak görmeliyim, aynı zamanda kendim tıklayarak istediğim kombinasyonu yapıp
    /// görebilmeliyim ... bu sistemi ilerideki eklenecek upgradelerde de kullanırız").
    ///
    /// In play mode, with the room on screen, this stands EVERY standing piece of the fixture
    /// catalogue in the room at once and renders each on its own — plus the room's own layers
    /// under and over them — into transparent 640x360 PNGs at the art's own pixel, under a plain
    /// white light so each is the art as drawn. Where each piece stands and what it sorts over
    /// comes from the stage itself, so nothing about placement is re-derived. The backdrops
    /// (the wall plates, the right wall) are whole-canvas art already and are not rendered.
    ///
    /// Out: Tools/upgrade_tree/layers/*.png + layers.json. Tools/upgrade_tree/build.py stacks
    /// them into the upgrade tree page. The room is put back as the run owns it afterwards.
    /// </summary>
    public static class RoomLayerExport
    {
        private const string Menu = "LastCall/Export Room Layers (play mode, room on screen)";
        private const int W = 640, H = 360;
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [MenuItem(Menu, true)]
        private static bool CanExport() => Application.isPlaying;

        [MenuItem(Menu)]
        public static void Export()
        {
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            var stage = Object.FindFirstObjectByType<DiegeticStage>();
            var hud = Object.FindFirstObjectByType<TycoonHud>();
            var cam = Camera.main;
            var run = boot != null ? boot.Tycoon : null;
            if (run == null || stage == null || cam == null)
            {
                Debug.LogError("RoomLayerExport: needs the game running with a run dealt and the room on screen.");
                return;
            }

            var slots = new Dictionary<string, StageSlot>();
            foreach (var s in boot.StageSlots) slots[s.Id] = s;
            var standing = run.FixtureCatalogue
                .Where(f => slots.TryGetValue(f.Slot, out var sl) && !sl.Backdrop && !sl.Carried)
                .ToList();

            string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tools", "upgrade_tree", "layers");
            Directory.CreateDirectory(outDir);
            foreach (var old in Directory.GetFiles(outDir, "*.png")) File.Delete(old);

            // Every standing piece at once: they are rendered one at a time below, so two
            // rungs of one ladder standing inside each other here is harmless.
            stage.SyncFixtures(standing);

            // ── who is who ───────────────────────────────────────────────────────────
            var ids = standing.Select(f => f.Id).OrderByDescending(id => id.Length).ToList();
            string FixtureOf(string name)
            {
                if (!name.StartsWith("Fx_")) return null;
                string rest = name.Substring(3);
                foreach (var id in ids)
                    if (rest == id || rest.StartsWith(id + "_")) return id;
                return null;
            }

            var blobs = new Dictionary<SpriteRenderer, string>();
            var shadowField = typeof(DiegeticStage).GetField("_shadows", Any);
            if (shadowField != null && shadowField.GetValue(stage) is System.Collections.IList shadows)
                foreach (var entry in shadows)
                {
                    var t = entry.GetType();
                    var under = t.GetField("Item1").GetValue(entry) as Transform;
                    var blob = t.GetField("Item2").GetValue(entry) as SpriteRenderer;
                    string owner = under != null ? FixtureOf(under.name) : null;
                    if (blob != null && owner != null) blobs[blob] = owner;
                }

            var all = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sprite != null).ToList();
            var wasOn = all.ToDictionary(r => r, r => r.enabled);
            var pieces = new Dictionary<string, List<SpriteRenderer>>();
            var baseParts = new List<SpriteRenderer>();
            foreach (var r in all)
            {
                if (r.sortingLayerName == "Patrons") continue;          // the crowd is not the room
                if (r.name == "Background") continue;                    // the plates are their own files
                string owner = FixtureOf(r.name);
                if (owner == null && blobs.TryGetValue(r, out var b)) owner = b;
                if (owner != null)
                {
                    if (!pieces.TryGetValue(owner, out var list)) pieces[owner] = list = new List<SpriteRenderer>();
                    list.Add(r);
                }
                else if (!r.name.StartsWith("Fx_")) baseParts.Add(r);   // a backdrop layer is its own file too
            }

            // ── a plain white light: the art as drawn ────────────────────────────────
            var lights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            var lightWas = lights.Select(l => (l, l.enabled, l.intensity, l.color)).ToList();
            foreach (var l in lights)
            {
                if (l.lightType == Light2D.LightType.Global) { l.enabled = true; l.intensity = 1f; l.color = Color.white; }
                else l.enabled = false;
            }

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            rt.Create();
            var prevTarget = cam.targetTexture;
            var prevClear = cam.clearFlags;
            var prevBg = cam.backgroundColor;
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            var read = new Texture2D(W, H, TextureFormat.RGBA32, false);

            var json = new StringBuilder();
            json.Append("{\n  \"size\": [").Append(W).Append(", ").Append(H).Append("],\n  \"layers\": [\n");
            bool first = true;

            void Shoot(string name, string kind, IEnumerable<SpriteRenderer> show)
            {
                var set = new HashSet<SpriteRenderer>(show);
                if (set.Count == 0) return;
                foreach (var r in all) r.enabled = set.Contains(r);
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                read.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), read.EncodeToPNG());
                int order = set.Max(r => r.sortingOrder);
                int orderMin = set.Min(r => r.sortingOrder);
                json.Append(first ? "" : ",\n").Append("    {\"id\": \"").Append(name)
                    .Append("\", \"kind\": \"").Append(kind)
                    .Append("\", \"order\": ").Append(order).Append(", \"orderMin\": ").Append(orderMin)
                    .Append(", \"layer\": \"").Append(set.First().sortingLayerName).Append("\"}");
                first = false;
            }

            try
            {
                // The room's own picture around the pieces, cut at the bands the pieces sort in
                // (DiegeticStage: plate 10, hangers 15, mats 16, dressing 20, bar 30, on it 34/35).
                Shoot("base_under", "base", baseParts.Where(r => r.sortingOrder < 10));
                Shoot("base_glass", "base", baseParts.Where(r => r.sortingOrder > 10 && r.sortingOrder < 15));
                Shoot("base_mid", "base", baseParts.Where(r => r.sortingOrder >= 15 && r.sortingOrder < 30));
                Shoot("base_front", "base", baseParts.Where(r => r.sortingOrder >= 30));
                // A piece can straddle the bar: a table's body sorts at 20, behind the counter,
                // while its contact shadow sorts at 31, over it. One picture of both could only
                // be stacked on one side of the counter, so each piece is cut at the bar the way
                // the room's own bands are: its part behind (under 30) and its part over.
                foreach (var kv in pieces)
                {
                    var lo = kv.Value.Where(r => r.sortingOrder < 30).ToList();
                    var hi = kv.Value.Where(r => r.sortingOrder >= 30).ToList();
                    if (lo.Count > 0 && hi.Count > 0)
                    {
                        Shoot("fx_" + kv.Key, "fixture", lo);
                        Shoot("fx_" + kv.Key + "__hi", "fixture", hi);
                    }
                    else Shoot("fx_" + kv.Key, "fixture", kv.Value);
                }
            }
            finally
            {
                json.Append("\n  ]\n}\n");
                File.WriteAllText(Path.Combine(outDir, "layers.json"), json.ToString());
                foreach (var kv in wasOn) if (kv.Key != null) kv.Key.enabled = kv.Value;
                foreach (var (l, on, intensity, color) in lightWas)
                    if (l != null) { l.enabled = on; l.intensity = intensity; l.color = color; }
                cam.targetTexture = prevTarget;
                cam.clearFlags = prevClear;
                cam.backgroundColor = prevBg;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(read);
                // The room goes back to what the run owns: the HUD re-dresses it on its next
                // frame when its change counter says so.
                var counter = typeof(TycoonHud).GetField("_lastFixtureCount", Any);
                if (hud != null && counter != null) counter.SetValue(hud, -1);
            }
            Debug.Log($"[LastCall] Room layers: {pieces.Count} pieces + the room's own bands -> {outDir}");
        }
    }
}
