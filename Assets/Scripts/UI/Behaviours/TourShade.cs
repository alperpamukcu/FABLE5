using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE TOUR'S SHADE (2026-09-29, the author: "göstergede eğer bir şey işaret ediliyorsa sadece onunla etkileşime
    /// geçilmeli"): one full-screen graphic that dims the room everywhere but its HOLES - the thing the hostess points at,
    /// and where a dragged thing goes - and, while it BLOCKS, eats every click outside them. A press inside a hole is not
    /// this graphic's (<see cref="IsRaycastLocationValid"/> says no), so it falls through to whatever is lit: the stool,
    /// the key, the bottle. Drawn as bands between the holes' edges, so any number of holes (two, in practice) cut clean.
    /// </summary>
    // Graphic itself no longer requires a CanvasRenderer in this uGUI (Image, Text and RawImage each ask for their own),
    // and in the editor GetComponent hands back a fake null for a missing one - so Graphic's lazy getter never adds it,
    // and the first raycast over the shade threw on every frame for the rest of the session (the PlayMode suite, 0/20).
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TourShade : MaskableGraphic, ICanvasRaycastFilter
    {
        private readonly List<Rect> _holes = new List<Rect>();

        /// <summary>Clicks outside the holes stop here.</summary>
        public bool Blocks { get; set; }

        /// <summary>The holes, in this rect's local space.</summary>
        public void SetHoles(IList<Rect> holes)
        {
            _holes.Clear();
            if (holes != null) _holes.AddRange(holes);
            raycastTarget = Blocks;
            SetVerticesDirty();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!Blocks) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var p))
                return true;
            foreach (var h in _holes) if (h.Contains(p)) return false;
            return true;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var full = rectTransform.rect;
            // Every horizontal edge a hole has, inside the frame, cuts the frame into bands; in each band the holes that
            // span it leave gaps, and the gaps are what is dimmed.
            var ys = new List<float> { full.yMin, full.yMax };
            foreach (var h in _holes)
            {
                ys.Add(Mathf.Clamp(h.yMin, full.yMin, full.yMax));
                ys.Add(Mathf.Clamp(h.yMax, full.yMin, full.yMax));
            }
            ys.Sort();
            var spans = new List<Vector2>();
            for (int i = 0; i + 1 < ys.Count; i++)
            {
                float y0 = ys[i], y1 = ys[i + 1];
                if (y1 - y0 < 0.01f) continue;
                float mid = (y0 + y1) * 0.5f;
                spans.Clear();
                foreach (var h in _holes)
                    if (mid > h.yMin && mid < h.yMax) spans.Add(new Vector2(Mathf.Max(full.xMin, h.xMin), Mathf.Min(full.xMax, h.xMax)));
                spans.Sort((a, b) => a.x.CompareTo(b.x));
                float x = full.xMin;
                foreach (var s in spans)
                {
                    if (s.x > x) Quad(vh, x, y0, s.x, y1);
                    x = Mathf.Max(x, s.y);
                }
                if (x < full.xMax) Quad(vh, x, y0, full.xMax, y1);
            }
        }

        private void Quad(VertexHelper vh, float x0, float y0, float x1, float y1)
        {
            int i = vh.currentVertCount;
            var c = (Color32)color;
            vh.AddVert(new Vector3(x0, y0), c, Vector2.zero);
            vh.AddVert(new Vector3(x0, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y0), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
