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

        private void LateUpdate()
        {
            if (Source == null || Air == null) return;
            Air.color = Source.color;
            Air.intensity = Source.enabled && Source.gameObject.activeInHierarchy ? Source.intensity * Share : 0f;
        }
    }
}
