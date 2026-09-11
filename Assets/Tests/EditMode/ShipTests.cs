using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// Things that only break in a PLAYER BUILD, pinned where the editor can see them.
    ///
    /// The editor finds every shader in the project by name, so a shader nothing references
    /// works perfectly here and silently vanishes from a build — which is exactly how the
    /// pour, the game's main mechanic, would have shipped: MetaballFluid found its shader only
    /// by Shader.Find, no asset referenced it, and the build strips unreferenced shaders, so
    /// outside the editor no liquid would have drawn at all (found 2026-09-11). A material in
    /// Resources is what carries it into the build, so the material is what is pinned.
    /// </summary>
    public class ShipTests
    {
        /// <summary>The same path MetaballFluid.MaterialPath names — this assembly does not
        /// reference the UI, so it is written out, and a rename that misses one side fails here.</summary>
        private const string LiquidMaterial = "Fluid/MetaballLiquid";

        [Test]
        public void TheLiquidShaderShipsWithTheGame()
        {
            var mat = Resources.Load<Material>(LiquidMaterial);
            Assert.That(mat, Is.Not.Null,
                "Resources/" + LiquidMaterial + ".mat is missing — a player build would strip the "
                + "liquid shader and no drink would draw anywhere");
            Assert.That(mat.shader, Is.Not.Null);
            Assert.That(mat.shader.name, Is.EqualTo("LastCall/MetaballLiquid"));
            Assert.That(mat.shader.isSupported, Is.True,
                "the liquid shader does not compile for this platform");
        }
    }
}
