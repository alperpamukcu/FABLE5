using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A black ring that actually closes around the glyphs. Unity's own Outline only stamps the
    /// four diagonal copies, so a straight stem gets no cover on its flat sides and the frame
    /// reads as chewed rather than drawn (2026-07-27). This stamps all eight neighbours, which
    /// is what a pixel artist would do by hand.
    ///
    /// Distance is in UI units, so on a pixel face set it to one font-pixel — at font size 16 on
    /// an 8px face that is 2 — and the ring lands exactly one pixel out on every side.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PixelOutline : BaseMeshEffect
    {
        public Color EffectColor = Color.black;
        public float Distance = 2f;

        private static readonly Vector2[] Ring =
        {
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(-1, 1),
            new Vector2(-1, 0), new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),
        };

        private readonly List<UIVertex> _face = new List<UIVertex>();
        private readonly List<UIVertex> _all = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            _face.Clear();
            vh.GetUIVertexStream(_face);
            if (_face.Count == 0) return;

            _all.Clear();
            for (int r = 0; r < Ring.Length; r++)
            {
                float ox = Ring[r].x * Distance, oy = Ring[r].y * Distance;
                for (int i = 0; i < _face.Count; i++)
                {
                    var v = _face[i];
                    var p = v.position;
                    p.x += ox; p.y += oy;
                    v.position = p;
                    v.color = EffectColor;
                    _all.Add(v);
                }
            }
            _all.AddRange(_face);   // the glyphs themselves go on top of their own ring

            vh.Clear();
            vh.AddUIVertexTriangleStream(_all);
        }
    }
}
