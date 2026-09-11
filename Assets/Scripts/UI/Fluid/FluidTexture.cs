using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// Owns what a <see cref="MetaballFluid"/> holds on the GPU — the small texture the drink is
    /// drawn into and its own clone of the liquid material — and gives both back when the
    /// drink's image is destroyed. The fluid is a plain object with no lifetime of its own; the
    /// image's GameObject is the thing that comes and goes (a bench rebuilt, a scene reloaded by
    /// the PlayMode suite), and without this every one of them leaked a texture.
    /// </summary>
    public sealed class FluidTexture : MonoBehaviour
    {
        public RenderTexture Tex;
        public Material Material;

        private void OnDestroy()
        {
            if (Tex != null) { Tex.Release(); Destroy(Tex); Tex = null; }
            if (Material != null) { Destroy(Material); Material = null; }
        }
    }
}
