using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastCall.UI
{
    /// <summary>
    /// THE LIT AIR UNDER A PENDANT (2026-09-22, the author's eighth list: "Unity üzerinden profesyonel
    /// ışıklandırma"). A second light on the back wall's layer that exists for its VOLUME - the shaft URP draws
    /// behind the drinkers - and follows the spot it hangs from: its colour, and a share of its brightness, so it
    /// warms with the evening and goes down with the house at closing without being told.
    /// </summary>
    public sealed class PendantAir : MonoBehaviour
    {
        public Light2D Source;
        public Light2D Air;
        public float Share = 0.04f;

        /// <summary>
        /// HOW FAR BACK DOWN THE SHAFT HANGS FROM ITS SPOT (2026-09-23, the author: "tavan ışıkları
        /// abajurun üstünden başlıyor, abajurun üstünde kalan kısmı kes").
        ///
        /// The SPOT's apex is lifted above the shade on purpose — that is what makes its cone the
        /// bulb's width where it leaves the glass instead of a point (DiegeticStage.ApexLift) — but
        /// the shaft of lit air is the part you can SEE, and lifting it put a wedge of light over
        /// the lamp, which a shade is precisely the thing that stops. The light keeps its lifted
        /// apex; the air is hung back down at the shade's mouth and starts at the bulb's own width
        /// instead (Air.pointLightInnerRadius), which is the same look with nothing above the shade.
        ///
        /// In the spot's own space, because the spot is turned half a circle to face the floor:
        /// local +Y is world DOWN. Set in stage units by whoever lays the fixture out, since the
        /// lift is scaled with the room.
        /// </summary>
        public float DropBelowSpot;

        private void LateUpdate()
        {
            if (Source == null || Air == null) return;
            Air.color = Source.color;
            Air.intensity = Source.enabled && Source.gameObject.activeInHierarchy ? Source.intensity * Share : 0f;
            var want = new Vector3(0f, DropBelowSpot, 0f);
            if (Air.transform.localPosition != want) Air.transform.localPosition = want;
        }
    }
}
