using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A GraphicRaycaster that forgives a few pixels (2026-09-08, the author: "mouse
    /// imlecinin ucu etki alanına dahil edilmeli — png'nin sol üst köşesinin 4 pixel çaplık
    /// bir daire daha etki alanına dahil edilmeli ... cursorun parmak görselinin etrafı
    /// tıklarken bir şeyleri seçebilmeli"). The pointer's hotspot is one point on the
    /// fingertip; a pixel-art hand at 2x is a fat finger, and a click a couple of pixels
    /// off a small key should still land. When the exact point finds nothing, the same
    /// cast is tried round it — eight points at three pixels, then eight at six — and
    /// the first ring that hits, counts. Every canvas the game raycasts through carries
    /// this instead of the stock one, so the forgiveness is the same everywhere.
    /// </summary>
    public sealed class ForgivingRaycaster : GraphicRaycaster
    {
        private static readonly Vector2[] Ring = BuildRing();

        private static Vector2[] BuildRing()
        {
            var list = new List<Vector2>();
            foreach (float r in new[] { 3f, 6f })
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    list.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                }
            return list.ToArray();
        }

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            int before = resultAppendList.Count;
            base.Raycast(eventData, resultAppendList);
            if (resultAppendList.Count > before) return;
            var at = eventData.position;
            foreach (var off in Ring)
            {
                eventData.position = at + off;
                base.Raycast(eventData, resultAppendList);
                if (resultAppendList.Count > before) break;
            }
            eventData.position = at;
        }
    }
}
