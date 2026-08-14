using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LastCall.UI;

namespace LastCall.EditorTools
{
    /// <summary>
    /// THE MECHANICAL HALF OF THE DELIVERY GATE (GDD 16 §7, 2026-08-14).
    ///
    /// Taste cannot be automated. Fit, scale, grid, type and palette can, and every one of
    /// them has already shipped broken while somebody was looking straight at it:
    ///
    ///   · the settings cog drawn at 20 units from a 16-pixel mask — 1.25x, teeth at two
    ///     different widths, and it survived a screenshot review
    ///   · "0.0" at display 24 given a 60-unit rect, overflowing left onto the fifth star
    ///   · the week marquee pivoted to its own left edges, hanging left of its own letters
    ///
    /// All three are ARITHMETIC. This walks the live screen and does the arithmetic, so the
    /// looking that follows is spent on the thing looking is actually for.
    ///
    /// It runs in PLAY MODE, because this UI does not exist until it is built in code.
    /// </summary>
    public static class UiAudit
    {
        private const float Eps = 0.01f;

        [MenuItem("LastCall/Audit UI %#u")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Audit UI",
                    "The UI is built in code and does not exist outside play mode.\n\n" +
                    "Enter play mode, put the screen you want on the screen, then run this.",
                    "OK");
                return;
            }

            var findings = new List<Finding>();
            Exemptions.Clear();
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.transform.parent != null) continue;      // roots only; walk finds the rest
                Walk(canvas.transform, findings);
            }

            Report(findings);
        }

        private readonly struct Finding
        {
            public readonly string Rule, Path, Detail;
            public Finding(string rule, string path, string detail) { Rule = rule; Path = path; Detail = detail; }
        }

        private static readonly List<string> Exemptions = new List<string>();

        private static void Walk(Transform t, List<Finding> found)
        {
            // A hidden branch is not on the screen, and auditing it reports the market's
            // furniture while the player is looking at the bench.
            if (!t.gameObject.activeInHierarchy) return;

            // A branch that says WHY it is not chrome is skipped whole, and its sentence is
            // printed with the report. See UiAuditExempt: the glasses on the rack and the
            // register are placed by where they stand in the room, not on a unit grid.
            var exempt = t.GetComponent<UiAuditExempt>();
            if (exempt != null)
            {
                Exemptions.Add($"{t.name} — {exempt.Reason}");
                return;
            }

            var rt = t as RectTransform;
            if (rt != null)
            {
                CheckGrid(rt, found);
                var img = t.GetComponent<Image>();
                if (img != null && img.enabled) { CheckScale(rt, img, found); CheckPalette(t, img.color, found); }
                var raw = t.GetComponent<RawImage>();
                if (raw != null && raw.enabled) CheckPalette(t, raw.color, found);
                var text = t.GetComponent<Text>();
                if (text != null && text.enabled)
                {
                    CheckType(rt, text, found);
                    CheckFit(rt, text, found);
                    CheckPalette(t, text.color, found);
                }
            }

            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), found);
        }

        // ── §3 the scaling law ──────────────────────────────────────────────────
        // A drawing is used at the size it was drawn, or at a whole multiple of it.
        private static void CheckScale(RectTransform rt, Image img, List<Finding> found)
        {
            var sprite = img.sprite;
            if (sprite == null) return;                          // a plain fill has no drawing to scale
            if (img.type != Image.Type.Simple) return;           // sliced/tiled art is MEANT to stretch
            if (sprite.border != Vector4.zero) return;

            float sw = sprite.rect.width, sh = sprite.rect.height;
            if (sw < 1f || sh < 1f) return;
            float rw = rt.rect.width, rh = rt.rect.height;
            if (rw < 1f || rh < 1f) return;

            // preserveAspect fits the drawing inside the rect, so ONE factor governs both axes.
            if (img.preserveAspect)
            {
                float k = Mathf.Min(rw / sw, rh / sh);
                if (!IsWholeFactor(k))
                    found.Add(new Finding("SCALE", Path(rt),
                        $"{sprite.name} is {sw:0}x{sh:0} drawn at {k:0.###}x " +
                        $"(rect {rw:0.#}x{rh:0.#}) — use {Mathf.Max(1f, Mathf.Round(k)) * sw:0}x{Mathf.Max(1f, Mathf.Round(k)) * sh:0}"));
                return;
            }

            float kx = rw / sw, ky = rh / sh;
            if (!IsWholeFactor(kx) || !IsWholeFactor(ky))
                found.Add(new Finding("SCALE", Path(rt),
                    $"{sprite.name} is {sw:0}x{sh:0} drawn at {kx:0.###}x by {ky:0.###}x (rect {rw:0.#}x{rh:0.#})"));
        }

        private static bool IsWholeFactor(float k)
            => k >= 1f - Eps && Mathf.Abs(k - Mathf.Round(k)) <= Eps;

        // ── §4 the fitting law ──────────────────────────────────────────────────
        // Measure the string; the rect must hold it. Overflow is for text allowed to run,
        // never a substitute for a box that fits.
        private static void CheckFit(RectTransform rt, Text text, List<Finding> found)
        {
            if (string.IsNullOrEmpty(text.text)) return;
            float w = rt.rect.width, h = rt.rect.height;

            if (text.horizontalOverflow == HorizontalWrapMode.Overflow)
            {
                float need = text.preferredWidth;
                if (need > w + 0.5f)
                    found.Add(new Finding("FIT", Path(rt),
                        $"\"{Clip(text.text)}\" needs {need:0} units, rect is {w:0} — it runs {need - w:0} outside its box"));
            }
            if (text.verticalOverflow == VerticalWrapMode.Truncate && text.preferredHeight > h + 0.5f)
                found.Add(new Finding("FIT", Path(rt),
                    $"\"{Clip(text.text)}\" needs {text.preferredHeight:0} units of height, rect is {h:0} — it is being cut off"));
        }

        // ── §0 type ─────────────────────────────────────────────────────────────
        private static void CheckType(RectTransform rt, Text text, List<Finding> found)
        {
            int s = text.fontSize;
            if (s != 8 && s != 16 && s != 24)
                found.Add(new Finding("TYPE", Path(rt),
                    $"font size {s} — the pixel faces only rasterise at 8, 16 or 24"));
            if (text.resizeTextForBestFit)
                found.Add(new Finding("TYPE", Path(rt), "resizeTextForBestFit is on — it scales the face off the grid"));
        }

        // ── §0 grid ─────────────────────────────────────────────────────────────
        // Halves are allowed: centring an odd width lands on one honestly. Anything else is
        // a number nobody chose.
        private static void CheckGrid(RectTransform rt, List<Finding> found)
        {
            if (!OnGrid(rt.sizeDelta.x) || !OnGrid(rt.sizeDelta.y))
                found.Add(new Finding("GRID", Path(rt), $"size {rt.sizeDelta} is not on whole units"));
            if (!OnGrid(rt.anchoredPosition.x) || !OnGrid(rt.anchoredPosition.y))
                found.Add(new Finding("GRID", Path(rt), $"position {rt.anchoredPosition} is not on whole units"));
        }

        private static bool OnGrid(float v)
        {
            float f = Mathf.Abs(v - Mathf.Floor(v));
            return f <= Eps || Mathf.Abs(f - 0.5f) <= Eps || f >= 1f - Eps;
        }

        // ── §0 palette ──────────────────────────────────────────────────────────
        // Every colour is a UITheme token, an alpha of one, or a uniform dim of one. A tint
        // that is none of those is not necessarily wrong — it needs a reason on the line
        // above it, which is what this asks for.
        private static Color[] _tokens;

        private static Color[] Tokens()
        {
            if (_tokens != null) return _tokens;
            var list = new List<Color> { Color.clear, Color.white, Color.black };
            foreach (var f in typeof(UITheme).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType == typeof(Color)) list.Add((Color)f.GetValue(null));
                else if (f.FieldType == typeof(Color[])) list.AddRange((Color[])f.GetValue(null));
            }
            return _tokens = list.ToArray();
        }

        private static void CheckPalette(Transform t, Color c, List<Finding> found)
        {
            if (c.a <= 0.004f) return;                     // invisible: a hit target, not a colour
            foreach (var token in Tokens())
            {
                if (Near(c, token)) return;
                // A uniform dim of a token reads as the same colour under less light.
                float k = token.r > 0.02f ? c.r / token.r : token.g > 0.02f ? c.g / token.g : -1f;
                if (k > 0.05f && k <= 1.5f
                    && Mathf.Abs(c.r - token.r * k) < 0.02f
                    && Mathf.Abs(c.g - token.g * k) < 0.02f
                    && Mathf.Abs(c.b - token.b * k) < 0.02f) return;
            }
            found.Add(new Finding("PALETTE", Path(t),
                $"#{ColorUtility.ToHtmlStringRGB(c)} is not a UITheme token, an alpha of one, or a dim of one"));
        }

        private static bool Near(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

        // ── the report ──────────────────────────────────────────────────────────

        /// <summary>
        /// The report also lands in `Temp/UiAudit.txt` — the same bargain `LookTests` strikes
        /// with `Temp/UiLooks`. A long report is unreadable in the console and the console is
        /// a shared room: one chatty warning a frame (this scene has no audio listener) buries
        /// it inside a second. A file is still there when you go looking.
        /// </summary>
        public const string ReportPath = "Temp/UiAudit.txt";

        private static void Write(string body)
        {
            try
            {
                System.IO.Directory.CreateDirectory("Temp");
                System.IO.File.WriteAllText(ReportPath, body);
            }
            catch (Exception e) { Debug.LogWarning("UI AUDIT could not write its report: " + e.Message); }
        }

        private static void Report(List<Finding> found)
        {
            if (found.Count == 0)
            {
                string clean = "UI AUDIT — clean. Scale, fit, grid, type and palette all pass on this screen."
                               + Skipped() + "\nThe rest of the gate is looking: GDD 16 §6, then §7.";
                Write(clean);
                Debug.Log(clean);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"UI AUDIT — {found.Count} findings on this screen (GDD 16 §7)");
            sb.Append(Skipped());
            foreach (var rule in new[] { "SCALE", "FIT", "TYPE", "GRID", "PALETTE" })
            {
                var group = found.Where(f => f.Rule == rule).ToList();
                if (group.Count == 0) continue;
                sb.AppendLine();
                sb.AppendLine($"── {rule} ({group.Count}) ──");
                // PALETTE is the noisy one: the same off-token colour is usually one decision
                // repeated across forty siblings, so it reports once per colour with a count.
                if (rule == "PALETTE")
                {
                    foreach (var g in group.GroupBy(f => f.Detail).OrderByDescending(g => g.Count()))
                        sb.AppendLine($"  {g.Count()}x  {g.Key}\n        e.g. {g.First().Path}");
                    continue;
                }
                foreach (var f in group.Take(40)) sb.AppendLine($"  {f.Path}\n        {f.Detail}");
                if (group.Count > 40) sb.AppendLine($"  ... and {group.Count - 40} more");
            }
            Write(sb.ToString());
            Debug.LogWarning(sb + "\n(also written to " + ReportPath + ")");
        }

        /// <summary>What was not looked at, and the sentence that says why. Printed with every
        /// report: an exemption nobody sees is the same as no rule.</summary>
        private static string Skipped()
        {
            if (Exemptions.Count == 0) return "";
            var sb = new StringBuilder("\nNot chrome, skipped:");
            foreach (var e in Exemptions.Distinct()) sb.Append("\n  · ").Append(e);
            return sb.ToString() + "\n";
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null && parts.Count < 8; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Clip(string s)
            => s.Replace("\n", "\\n") is var f && f.Length > 28 ? f.Substring(0, 28) + "…" : f;
    }
}
