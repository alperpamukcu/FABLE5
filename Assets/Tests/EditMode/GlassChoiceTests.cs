using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// A drink declares itself AT THE GLASS (`TycoonRun.PourAtGlass`, 2026-08-11) — and that
    /// rule has a sharp edge nothing was holding until now.
    ///
    /// The tin can never hold the fizz, so a highball's spirit half arrives on its own and
    /// reads as a neat pour: a rocks glass comes down, the mixer hits the brim, and the ratio
    /// lands outside its band. Core re-houses the drink the moment it becomes recognisable —
    /// but only if that clamped, mid-build ratio is legal, and whether it is depends entirely
    /// on how big the pour was. Nothing here is broken; it is a real bar's real problem
    /// (pour a double into a rocks glass and there is no room for the soda), and the player
    /// meets it exactly as the sim bot did: 1600 identical wrong drinks in one 200-run report.
    ///
    /// These two tests pin BOTH halves so neither can move silently: the naive big pour comes
    /// out wrong, and the same drink poured in small rounds comes out right AND full.
    /// </summary>
    public sealed class GlassChoiceTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relative));

        private static TycoonRun NewBar()
        {
            var deck = DataLoader.ParseDeck(Read("bottles/base_bar.json"));
            var recipes = DataLoader.ParseRecipes(Read("recipes/recipes.json"));
            var glassware = DataLoader.ParseGlassware(Read("glassware/glassware.json"));
            var starting = deck.Cards
                .Where(c => c.Info == null || c.Info.Tier <= 1)
                .Select(c => c.Clone()).ToList();
            return new TycoonRun(new Shelf(starting.Select(c => new ShelfBottle(c))),
                recipes, new RunRng("glass-choice"), glassware: glassware,
                lockedStock: deck.LockedCards);
        }

        private static RecipeDefinition VodkaSoda(TycoonRun run)
        {
            foreach (var r in run.AllRecipes) if (r.Id == "vodka_soda") return r;
            throw new InvalidOperationException("the book has lost its vodka soda");
        }

        private static string WhatIsInTheGlass(TycoonRun run) =>
            $"{run.ServingGlassware?.Id} {run.ServingGlass.TotalVolume:0.00}/" +
            $"{run.ServingGlass.Capacity:0.00} [" +
            string.Join(" ", run.ServingGlass.Ingredients.Select(
                i => i + "=" + run.ServingGlass.RatioOf(i).ToString("0.00"))) + "]";

        private static RecipeDefinition Delivered(TycoonRun run)
        {
            var match = RatioRecipeMatcher.Match(run.ServingGlass, run.MenuRecipes,
                id => run.Shelf.Find(id)?.Ingredient);
            return match?.Recipe;
        }

        [Test]
        public void One_confident_measure_of_a_fizzy_drink_comes_out_wrong()
        {
            // A TRIPWIRE, not an endorsement. If the pour system ever learns to hold the
            // right glass through a half-built drink, THIS test is the one that should fail —
            // delete it then, and keep the one below.
            var run = NewBar();
            var recipe = VodkaSoda(run);
            var ideal = RatioRecipeMatcher.PerfectPour(recipe);   // the box rule, 2026-08-20
            double volume = 0.98 * 1.0;   // a full highball, which is the glass this drink names

            run.PourMeasure("vodka_astra", volume * ideal[0]);
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
            run.PourAtGlass("soda_klara", volume * ideal[1]);

            Assert.AreNotEqual("vodka_soda", Delivered(run)?.Id,
                "the trap is gone — good news, and this test should go with it: "
                + WhatIsInTheGlass(run));
            Assert.AreEqual("rocks", run.ServingGlassware?.Id,
                "the spirit arrived alone and the bar reached for a neat pour's glass: "
                + WhatIsInTheGlass(run));
        }

        [Test]
        public void The_same_drink_poured_in_small_rounds_comes_out_right_and_full()
        {
            // The way a bartender actually does it: pour, watch the glass get swapped under
            // you, top it up. Small rounds keep the ratio legal at every step, so the drink
            // is recognised, re-housed into its own glass, and finished there.
            var run = NewBar();
            var recipe = VodkaSoda(run);
            var ideal = RatioRecipeMatcher.PerfectPour(recipe);   // the box rule, 2026-08-20
            var ids = new[] { "vodka_astra", "soda_klara" };

            for (int round = 0; round < 12; round++)
            {
                double capacity = run.ServingGlass.Capacity;
                double gap = 0.98 * capacity - run.ServingGlass.TotalVolume;
                if (gap <= 1e-6) break;
                double step = Math.Min(gap, capacity * 0.15);
                for (int i = 0; i < ids.Length; i++) run.PourAtGlass(ids[i], step * ideal[i]);
            }

            Assert.AreEqual("vodka_soda", Delivered(run)?.Id, WhatIsInTheGlass(run));
            Assert.AreEqual("highball", run.ServingGlassware?.Id,
                "and it ended in the glass the recipe names: " + WhatIsInTheGlass(run));
            Assert.GreaterOrEqual(run.ServingGlass.FillFraction, StoryTrial.DefaultMinFill,
                "an inspector's standard is reachable this way: " + WhatIsInTheGlass(run));
        }
    }
}
