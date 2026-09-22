using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastCall.UI
{
    /// <summary>
    /// THE BEAM YOU CAN SEE (2026-09-22, the author's seventh list: "yeni tavan aydınlatmasından tezgaha ve
    /// müşterilere net bir ışık hüzmesi vurmalı"). A 2D light only brightens what it lands on; the pendants' cones
    /// raised the dark counter from 12 to 37 and nobody could tell a lamp was on. A bar's pendant is also the shaft
    /// of air it lights, so each cone carries its own drawing: the same cone sprite, unlit, in the lamp's colour,
    /// over the drinkers and the counter, as bright as its light is right now - dim in the afternoon, full at night,
    /// down with the house at closing.
    /// </summary>
    public sealed class LightBeam : MonoBehaviour
    {
        public Light2D Light;
        public SpriteRenderer Beam;
        /// <summary>The beam's alpha per unit of the light's intensity.</summary>
        public float AlphaPerIntensity = 0.06f;
        public float MaxAlpha = 0.2f;

        private void LateUpdate()
        {
            if (Light == null || Beam == null) return;
            var c = Light.color;
            float a = Light.enabled ? Mathf.Min(MaxAlpha, Light.intensity * AlphaPerIntensity) : 0f;
            Beam.color = new Color(c.r, c.g, c.b, a);
        }
    }
}
