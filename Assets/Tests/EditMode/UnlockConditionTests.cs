using System.Collections.Generic;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// One lock type, asked the same way (GDD 26 §12.2 step 4). These pin the two halves that
    /// matter: whether it OPENS, and whether it can SAY what it wants — because a lock the
    /// player cannot read is indistinguishable from a thing the game forgot to give them.
    /// </summary>
    public class UnlockConditionTests
    {
        /// <summary>A bar, as a lock is allowed to see one.</summary>
        private sealed class Bar : IUnlockState
        {
            public double Stars { get; set; }
            public readonly HashSet<string> Kept = new HashSet<string>();
            public bool BeatWasKept(string beatId) => Kept.Contains(beatId);
        }

        [Test]
        public void An_open_lock_wants_nothing_and_says_nothing()
        {
            var bar = new Bar();
            Assert.IsTrue(UnlockCondition.Open.MetBy(bar));
            Assert.AreEqual("", UnlockCondition.Open.Sentence);
            // Zero stars is not a lock, it is the absence of one — so it collapses rather
            // than printing "NEEDS 0.0 STARS" on every unlocked line in the shop.
            Assert.AreSame(UnlockCondition.Open, UnlockCondition.Stars(0));
            Assert.AreSame(UnlockCondition.Open, UnlockCondition.Stars(-1));
        }

        [Test]
        public void A_star_gate_opens_on_the_rung_itself()
        {
            var gate = UnlockCondition.Stars(2.5);
            var bar = new Bar { Stars = 2.0 };
            Assert.IsFalse(gate.MetBy(bar));
            bar.Stars = 2.5;
            Assert.IsTrue(gate.MetBy(bar), "a bar sitting exactly on the rung has reached it");
            Assert.That(gate.Sentence, Does.Contain("2.5"));
        }

        [Test]
        public void A_kept_beat_lock_is_earned_from_a_person()
        {
            var lockedBy = UnlockCondition.Kept("ece_2", "Ece");
            var bar = new Bar { Stars = 5.0 };
            Assert.IsFalse(lockedBy.MetBy(bar), "all the stars in the world are not this");
            bar.Kept.Add("ece_2");
            Assert.IsTrue(lockedBy.MetBy(bar));
            // Named after the person: a player can go and do "SERVE ECE WHAT THEY ASK FOR".
            Assert.That(lockedBy.Sentence, Does.Contain("ECE"));
        }

        [Test]
        public void A_kept_beat_lock_without_a_name_still_says_something_useful()
        {
            var lockedBy = UnlockCondition.Kept("someone_1");
            Assert.That(lockedBy.Sentence, Does.Contain("LAST CALL"));
            Assert.That(lockedBy.Sentence, Does.Not.Contain("someone_1"),
                "an id is not a sentence a player can act on");
        }

        [Test]
        public void All_wants_every_part_and_names_every_part()
        {
            var both = UnlockCondition.All(UnlockCondition.Stars(3), UnlockCondition.Kept("ece_2", "Ece"));
            var bar = new Bar { Stars = 3.0 };
            Assert.IsFalse(both.MetBy(bar), "half of it is not it");
            bar.Kept.Add("ece_2");
            Assert.IsTrue(both.MetBy(bar));
            Assert.That(both.Sentence, Does.Contain("3.0"));
            Assert.That(both.Sentence, Does.Contain("ECE"),
                "a lock that admits to half of what it wants is worse than one that says nothing");
        }

        // ── a recipe carrying its own lock (GDD 26 §12.2 step 4) ─────────────

        [Test]
        public void A_recipe_with_no_lock_carries_none()
        {
            var plain = Recipe();
            Assert.IsNull(plain.Unlock, "an always-open condition is the absence of one");
            Assert.IsNull(plain.UnlockBeatId);
            // Built from an All() that collapses: the same thing every unlocked page in
            // recipes.json gets, so none of the 49 changes behaviour.
            var collapsed = Recipe(UnlockCondition.All(UnlockCondition.Stars(0), UnlockCondition.Open));
            Assert.IsNull(collapsed.Unlock);
        }

        [Test]
        public void A_recipe_can_be_locked_behind_a_rung_and_a_person_at_once()
        {
            var r = Recipe(UnlockCondition.All(
                UnlockCondition.Stars(3), UnlockCondition.Kept("ece_2", "Ece")), beatId: "ece_2");
            var bar = new Bar { Stars = 3.0 };
            Assert.IsFalse(r.Unlock.MetBy(bar), "the stars alone are not it");
            bar.Kept.Add("ece_2");
            Assert.IsTrue(r.Unlock.MetBy(bar));
            Assert.AreEqual("ece_2", r.UnlockBeatId,
                "the beat id rides alongside so the loader can check it against the arc");
            Assert.That(r.Unlock.Sentence, Does.Contain("3.0"));
            Assert.That(r.Unlock.Sentence, Does.Contain("ECE"));
        }

        private static RecipeDefinition Recipe(UnlockCondition unlock = null, string beatId = null) =>
            new RecipeDefinition("special", "Something Special", rank: 30,
                baseFlavor: 10, baseMult: 2, flavorPerLevel: 0, multPerLevel: 0,
                requirements: System.Array.Empty<PatternRequirement>(),
                ratioRequirements: new[] { new RatioRequirement(IngredientType.Spirit, 0.4, 0.6) },
                locked: true, unlock: unlock, unlockBeatId: beatId);

        [Test]
        public void All_collapses_when_there_is_nothing_to_earn()
        {
            Assert.AreSame(UnlockCondition.Open, UnlockCondition.All());
            Assert.AreSame(UnlockCondition.Open,
                UnlockCondition.All(UnlockCondition.Open, UnlockCondition.Stars(0), null));
            // One real part is that part, not a wrapper printing it with separators.
            var one = UnlockCondition.Stars(1.5);
            Assert.AreSame(one, UnlockCondition.All(UnlockCondition.Open, one));
        }
    }
}
