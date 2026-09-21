using System;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// THE LADDER (PLAN_rank_ladder, 2026-09-21): six rungs on the star scale, each opening something the bar did
    /// not have. Pure — no run, no room — so a rung is exactly where the table says and nothing else can move it.
    /// </summary>
    public sealed class BarRankTests
    {
        [Test]
        public void TheRungs_StandAtTheAuthorsThresholds()
        {
            Assert.AreEqual(7, BarRank.Rungs.Count, "the foot and six rungs: a half star, then one to five");
            double[] stars = { 0.0, 0.5, 1.0, 2.0, 3.0, 4.0, 5.0 };
            for (int i = 0; i < stars.Length; i++)
            {
                Assert.AreEqual(i, BarRank.Rungs[i].Index);
                Assert.AreEqual(stars[i], BarRank.Rungs[i].Stars, 1e-9);
            }
        }

        [Test]
        public void Of_ReadsTheHighestRungReached_OnTheRungExactly()
        {
            Assert.AreEqual(0, BarRank.Of(0.0).Index);
            Assert.AreEqual(0, BarRank.Of(0.49).Index, "just under the first rung is still the foot");
            Assert.AreEqual(1, BarRank.Of(0.5).Index, "sitting exactly on a rung has reached it");
            Assert.AreEqual(1, BarRank.Of(0.5 - 1e-12).Index, "and floating point may not say otherwise");
            Assert.AreEqual(2, BarRank.Of(1.0).Index);
            Assert.AreEqual(2, BarRank.Of(1.99).Index);
            Assert.AreEqual(3, BarRank.Of(2.0).Index);
            Assert.AreEqual(6, BarRank.Of(5.0).Index);
            Assert.AreEqual(6, BarRank.Of(9.0).Index, "nothing above the top");
        }

        [Test]
        public void EachFeature_IsOnExactlyOneRung_WhereTheAuthorPutIt()
        {
            Assert.AreEqual(1, BarRank.Granting(Feature.IceAndLemon).Index, "ice and lemon at half a star");
            Assert.AreEqual(2, BarRank.Granting(Feature.Door).Index, "the door after the first star");
            Assert.AreEqual(2, BarRank.Granting(Feature.Rims).Index, "salt and sugar at one star");
            Assert.AreEqual(3, BarRank.Granting(Feature.Spoon).Index, "the spoon at two stars");
            Assert.AreEqual(3, BarRank.Granting(Feature.Jars).Index, "olives and mint at two stars");
            foreach (Feature f in Enum.GetValues(typeof(Feature)))
            {
                int on = 0;
                foreach (var r in BarRank.Rungs) if (r.Grants(f)) on++;
                Assert.AreEqual(1, on, f + " must be on exactly one rung");
            }
        }

        [Test]
        public void Has_IsMonotonic_UpTheLadder()
        {
            Assert.IsFalse(BarRank.Has(0.0, Feature.IceAndLemon));
            Assert.IsTrue(BarRank.Has(0.5, Feature.IceAndLemon));
            Assert.IsFalse(BarRank.Has(0.5, Feature.Door));
            Assert.IsTrue(BarRank.Has(1.0, Feature.Door));
            Assert.IsTrue(BarRank.Has(1.0, Feature.Rims));
            Assert.IsFalse(BarRank.Has(1.9, Feature.Spoon));
            Assert.IsTrue(BarRank.Has(2.0, Feature.Spoon));
            Assert.IsTrue(BarRank.Has(5.0, Feature.IceAndLemon), "what a rung opened stays open above it");
        }

        [Test]
        public void ThePreparationsOpen_GrowWithTheRungs_InTheRailsOrder()
        {
            Assert.AreEqual(0, BarRank.PreparationsOpen(0.0).Count, "a bar nobody has heard of has nothing on its rail");
            var half = BarRank.PreparationsOpen(0.5);
            Assert.AreEqual(2, half.Count);
            Assert.AreEqual("ice", half[0].Id);
            Assert.AreEqual("lemon_twist", half[1].Id);
            var one = BarRank.PreparationsOpen(1.0);
            Assert.AreEqual(4, one.Count);
            Assert.AreEqual("salt_rim", one[2].Id);
            Assert.AreEqual("sugar_rim", one[3].Id);
            CollectionAssert.AreEqual(ServingSpec.GarnishPool, one, "at one star the rail carries the whole pool, in its order");
        }

        [Test]
        public void Gating_NamesTheRungForTheRailsFour_AndNothingElse()
        {
            Assert.AreEqual(Feature.IceAndLemon, BarRank.Gating(Preparations.Ice));
            Assert.AreEqual(Feature.IceAndLemon, BarRank.Gating(Preparations.LemonTwist));
            Assert.AreEqual(Feature.Rims, BarRank.Gating(Preparations.SaltRim));
            Assert.AreEqual(Feature.Rims, BarRank.Gating(Preparations.SugarRim));
            Assert.IsNull(BarRank.Gating(Preparations.Stirred), "a stir has its own law (the spoon), not this one");
            Assert.IsNull(BarRank.Gating(null));
        }

        [Test]
        public void Above_ClimbsToTheTop_AndStopsThere()
        {
            var r = BarRank.Bottom;
            int steps = 0;
            while (BarRank.Above(r) != null) { r = BarRank.Above(r); steps++; }
            Assert.AreEqual(6, steps);
            Assert.AreEqual(5.0, r.Stars, 1e-9);
            Assert.IsNull(BarRank.Above(r));
        }

        [Test]
        public void TheTitles_AreStringTableLines_OnePerRung()
        {
            foreach (var r in BarRank.Rungs)
                Assert.AreEqual("rank.title.r" + r.Index, r.Title.Key);
        }
    }
}
