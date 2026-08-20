using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// BOLD, THE WAY A PIXEL FACE CAN BE BOLD (2026-08-20, the author: "baloncuğun içindeki
    /// isimler kalın yazsın"). The glyphs are stamped a second time one pixel to the right, so
    /// every 1-unit stem becomes 2 — which is the double-strike a dot-matrix printer made bold
    /// with, and the only emboldening that leaves a pixel drawing on its grid.
    ///
    /// NOT <c>FontStyle.Bold</c>, and the reason is the house rule about the 8px faces: Unity
    /// has no bold cut of either face, so it synthesises one by emboldening the outline and
    /// re-rasterising — which lands the stems off the pixel grid and hands back grey, softened
    /// type at the one size the face is drawn to be sharp at.
    ///
    /// NOT <see cref="PixelOutline"/> with the ink's own colour either: that stamps all eight
    /// neighbours, and a glyph grown one unit in every direction has its counters filled — an
    /// 8px 'O' comes back as a solid block.
    ///
    /// The stamp keeps each vertex's OWN colour rather than taking one from the component, so
    /// a line carrying a rich-text colour tag (the regular's "x3" over the name) stays two
    /// colours when it is bolded.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PixelBold : BaseMeshEffect
    {
        /// <summary>How far the second strike lands, in UI units. ONE FONT PIXEL: 1 on an 8px
        /// face set at 8, 2 at 16. Anything else smears rather than thickens.</summary>
        public float Distance = 1f;

        private readonly List<UIVertex> _face = new List<UIVertex>();
        private readonly List<UIVertex> _all = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            _face.Clear();
            vh.GetUIVertexStream(_face);
            if (_face.Count == 0) return;

            _all.Clear();
            for (int i = 0; i < _face.Count; i++)
            {
                var v = _face[i];
                var p = v.position;
                p.x += Distance;
                v.position = p;
                _all.Add(v);
            }
            _all.AddRange(_face);   // the true glyphs sit on top of their own second strike

            vh.Clear();
            vh.AddUIVertexTriangleStream(_all);
        }
    }
}
