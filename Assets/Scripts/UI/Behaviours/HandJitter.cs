using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// LETTERS PUT DOWN BY A HAND (2026-09-22, the author's eighth list: the fake licence "her şeyi el ile kalemle
    /// çizmiş şekilde fontlar el yazısı gibi"). Every glyph of the text is turned a few degrees about its own middle
    /// and lifted or dropped a unit or two, each by its own amount - so a line does not sit on a rule the way type
    /// does, and a word written twice is not written the same twice.
    ///
    /// The amounts are hashed off <see cref="Seed"/> and the glyph's index, never rolled: the same card shows the same
    /// handwriting every night, and nothing here touches a game stream. It rides any <see cref="Graphic"/> that
    /// builds its mesh of quads - a UGUI Text is one quad a character.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class HandJitter : BaseMeshEffect
    {
        public uint Seed;
        /// <summary>The widest a glyph turns either way, in degrees.</summary>
        public float Angle = 6f;
        /// <summary>The furthest a glyph rises or falls off the line, in units.</summary>
        public float Lift = 1.6f;
        /// <summary>How far the whole line leans, in degrees (a hand writes uphill or down).</summary>
        public float Slope;

        private static readonly List<UIVertex> Verts = new List<UIVertex>();

        public void Set(uint seed, float angle, float lift, float slope)
        {
            Seed = seed; Angle = angle; Lift = lift; Slope = slope;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount < 4) return;
            Verts.Clear();
            vh.GetUIVertexStream(Verts);          // six a quad: two triangles
            float slope = Slope * Mathf.Deg2Rad;
            float originX = Verts.Count > 0 ? Verts[0].position.x : 0f;
            for (int q = 0; q + 5 < Verts.Count; q += 6)
            {
                Vector3 mid = Vector3.zero;
                for (int k = 0; k < 6; k++) mid += Verts[q + k].position;
                mid /= 6f;
                int glyph = q / 6;
                float ang = (IdArt.R(Seed, glyph * 2 + 1) - 0.5f) * 2f * Angle * Mathf.Deg2Rad;
                float lift = (IdArt.R(Seed, glyph * 2 + 2) - 0.5f) * 2f * Lift + (mid.x - originX) * Mathf.Tan(slope);
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                for (int k = 0; k < 6; k++)
                {
                    var v = Verts[q + k];
                    var p = v.position - mid;
                    v.position = mid + new Vector3(p.x * cos - p.y * sin, p.x * sin + p.y * cos + lift, p.z);
                    Verts[q + k] = v;
                }
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Verts);
        }
    }
}
