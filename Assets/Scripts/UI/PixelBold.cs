using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// Emboldens a pixel face by stamping the glyphs one FONT-pixel to the right, the way a
    /// pixel artist thickens a stem (the author, 2026-08-02: the menu's names and headings
    /// should carry more weight).
    ///
    /// Unity's own <see cref="FontStyle.Bold"/> is no use here: with no bold face to switch
    /// to it fakes one by smearing the glyph at a fractional offset, which on an 8px face
    /// lands between pixels and reads as blur — the same reason
    /// <c>TycoonServiceFlow.Handwritten</c> refuses Unity's fake italic. One clean stamp at a
    /// whole font-pixel thickens the letter and keeps every edge on the grid.
    ///
    /// <see cref="Distance"/> is in UI units, so it is one font-pixel: at font size 16 on an
    /// 8px face that is 2, which is the default.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PixelBold : BaseMeshEffect
    {
        /// <summary>One font-pixel, in UI units (size 16 on an 8px face = 2).</summary>
        public float Distance = 2f;

        private readonly List<UIVertex> _face = new List<UIVertex>();
        private readonly List<UIVertex> _all = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            _face.Clear();
            vh.GetUIVertexStream(_face);
            if (_face.Count == 0) return;

            // The stamp goes UNDER the face, so anti-aliased edges (there are none on a pixel
            // face, but a fallback font may have them) never darken the letter's own colour.
            _all.Clear();
            for (int i = 0; i < _face.Count; i++)
            {
                var v = _face[i];
                v.position.x += Distance;
                _all.Add(v);
            }
            _all.AddRange(_face);

            vh.Clear();
            vh.AddUIVertexTriangleStream(_all);
        }
    }
}
