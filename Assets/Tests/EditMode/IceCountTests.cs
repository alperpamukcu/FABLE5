using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE ICE IS COUNTED NOW (2026-08-25, the author: "buz istediği sayıda atılabilecek
    /// ve bardağın içerisinde gözükecek"). The preparation STEP stays deduplicated — the
    /// judge and every recipe spec ask "is there ice", never "how much" — but the glass
    /// keeps a cube count for the drawing to stand its pile on. These pin the boundary:
    /// the count grows without the step list growing, travels with the drink, and dies
    /// with it.
    /// </summary>
    public class IceCountTests
    {
        [Test]
        public void EveryCall_DropsOneMoreCube_ButOnlyOneStep()
        {
            var glass = new GlassContents(1.0);
            glass.AddPreparation(Preparations.Ice);
            glass.AddPreparation(Preparations.Ice);
            glass.AddPreparation(Preparations.Ice);

            Assert.AreEqual(3, glass.IceCubes, "three cubes went in");
            int iceSteps = 0;
            foreach (var s in glass.PreparationSteps) if (s.Id == "ice") iceSteps++;
            Assert.AreEqual(1, iceSteps, "the STEP applies once — a drink is iced or it is not");
            Assert.IsTrue(glass.HasPreparation("ice"));
        }

        [Test]
        public void OtherPreparations_StayUncounted_AndDeduplicated()
        {
            var glass = new GlassContents(1.0);
            glass.AddPreparation(Preparations.SaltRim);
            glass.AddPreparation(Preparations.SaltRim);
            Assert.AreEqual(0, glass.IceCubes, "salt is not ice");
            Assert.AreEqual(1, glass.PreparationSteps.Count);
        }

        [Test]
        public void TheCubes_TravelWithTheDrink()
        {
            var tin = new GlassContents(1.0);
            tin.Add("gin", 0.5);
            tin.AddPreparation(Preparations.Ice);
            tin.AddPreparation(Preparations.Ice);

            var glass = new GlassContents(1.0);
            tin.TransferInto(glass, 0.5, accuracy: 1.0);

            Assert.AreEqual(2, glass.IceCubes, "both cubes crossed with the pour");
            Assert.IsTrue(glass.HasPreparation("ice"));
        }

        [Test]
        public void ATransfer_NeverTakesCubesAway()
        {
            // The glass may already hold cubes of its own; a tin with fewer must not
            // argue them back down.
            var tin = new GlassContents(1.0);
            tin.Add("gin", 0.5);
            tin.AddPreparation(Preparations.Ice);

            var glass = new GlassContents(1.0);
            glass.AddPreparation(Preparations.Ice);
            glass.AddPreparation(Preparations.Ice);
            glass.AddPreparation(Preparations.Ice);

            tin.TransferInto(glass, 0.5, accuracy: 1.0);
            Assert.AreEqual(3, glass.IceCubes);
        }

        [Test]
        public void RemovingTheIce_TakesTheCubesWithIt()
        {
            var glass = new GlassContents(1.0);
            glass.AddPreparation(Preparations.Ice);
            glass.AddPreparation(Preparations.Ice);
            glass.RemovePreparation("ice");
            Assert.AreEqual(0, glass.IceCubes, "no ice step, no cubes to see");
            Assert.IsFalse(glass.HasPreparation("ice"));
        }

        [Test]
        public void ClearingTheGlass_ClearsTheCount()
        {
            var glass = new GlassContents(1.0);
            glass.Add("gin", 0.3);
            glass.AddPreparation(Preparations.Ice);
            glass.AddPreparation(Preparations.Ice);
            glass.Clear();
            Assert.AreEqual(0, glass.IceCubes);
        }
    }
}
