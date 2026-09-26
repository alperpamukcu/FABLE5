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
    /// THE SAVE LAYER'S ONE PROMISE (2026-09-26, TycoonRun.Save.cs): a run written down at
    /// dawn and stood back up is the SAME run, bit for bit. The gold walk below is the whole
    /// test in one sentence — twin A plays straight through, twin B dies and is reborn from
    /// JSON at every single dawn, and the two must file identical books, hold identical
    /// money, and WRITE IDENTICAL SNAPSHOTS at every dawn after. Any state the snapshot
    /// forgets, any restore that re-rolls a die, any double that rode through the JSON
    /// writer rounded — the walk catches it as a diverging night.
    /// </summary>
    public sealed class SaveTests
    {
        private static string ReadDataFile(string relativePath) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relativePath));

        /// <summary>The scene's own content, loaded the way GameBootstrap loads it — the
        /// full game: regulars, papers, the written nights, the fixture catalogue.</summary>
        private sealed class Content
        {
            public LoadedDeck Deck;
            public IReadOnlyList<RecipeDefinition> Recipes;
            public IReadOnlyList<ArchetypeDefinition> Archetypes;
            public IReadOnlyList<GlasswareDefinition> Glassware;
            public LoadedFixtures Dressing;
            public StoryArc Story;
            public List<IngredientCard> AllCards;
        }

        private static Content Load()
        {
            var content = new Content();
            content.Deck = DataLoader.ParseDeck(ReadDataFile("bottles/base_bar.json"));
            content.Recipes = DataLoader.ParseRecipes(ReadDataFile("recipes/recipes.json"));
            content.Archetypes = DataLoader.ParseArchetypes(ReadDataFile("customers/archetypes.json"));
            content.Glassware = DataLoader.ParseGlassware(ReadDataFile("glassware/glassware.json"));
            content.Dressing = DataLoader.ParseFixtures(ReadDataFile("fixtures/fixtures.json"));
            var cast = DataLoader.ParsePapers(ReadDataFile("customers/papers.json"));
            content.Story = DataLoader.ParseStory(ReadDataFile("story/story.json"), cast, content.Recipes);
            content.AllCards = new List<IngredientCard>(content.Deck.Cards);
            content.AllCards.AddRange(content.Deck.LockedCards);
            return content;
        }

        private static TycoonRun NewRun(Content content, string seed)
        {
            var starting = new List<ShelfBottle>();
            var catalogue = new List<IngredientCard>();
            foreach (var card in content.Deck.Cards)
            {
                if (content.Deck.IsStarting(card)) starting.Add(new ShelfBottle(card.Clone()));
                else catalogue.Add(card);
            }
            return new TycoonRun(new Shelf(starting), content.Recipes, new RunRng(seed),
                config: TycoonConfig.ForTheScene,
                regulars: new RegularsRegistry(content.Archetypes),
                brandCatalogue: catalogue,
                glassware: content.Glassware,
                lockedStock: content.Deck.LockedCards,
                fixtures: content.Dressing.Fixtures,
                story: content.Story);
        }

        private static TycoonRun Reborn(Content content, RunSnapshot snap) =>
            TycoonRun.Restore(snap, content.Recipes, content.AllCards,
                config: TycoonConfig.ForTheScene,
                regulars: new RegularsRegistry(content.Archetypes),
                glassware: content.Glassware,
                fixtures: content.Dressing.Fixtures,
                story: content.Story);

        /// <summary>One night, played the same on both twins: even nights are worked (every
        /// waiting drinker gets a vodka-soda, right or wrong, off a clean counter), odd
        /// nights run out on their own clock — both roads are pure Core and deterministic.</summary>
        private static void PlayNight(TycoonRun run, bool worked)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 20000, "the night must terminate");
                if (run.Floor.IsComplete && !run.Floor.House.CounterClear) run.Floor.House.SweepForClosing();
                run.Tick(worked ? 5 : 0.25);
                if (!worked) continue;
                TestNight.Clean(run);
                foreach (var visit in run.Floor.Seated.ToList())
                {
                    // A drinker walks in and then DECIDES (CLAUDE.md): Waiting alone is not
                    // yet an order, and ServeTo refuses "they are still choosing".
                    if (visit.State != VisitState.Waiting || !visit.HasOrdered) continue;
                    if (!run.CanFinishAtGlass) continue;
                    run.PourMeasure("vodka_astra", 0.3);
                    run.PourMeasure("soda_klara", 0.4);
                    run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);
                    run.ServeTo(visit);
                }
            }
            // The market: one deterministic buy when the till allows, so the catalogue, the
            // shelf and the NEW flash all move through the snapshot.
            if (run.Phase == TycoonPhase.DayEnd && run.MarketOffers.Count > 0)
            {
                var offer = run.MarketOffers[0];
                if (run.Money >= offer.Price + 40) run.BuyBrand(0);
            }
        }

        [Test]
        public void TheGoldWalk_ARunRebornFromJsonAtEveryDawn_IsTheSameRun()
        {
            var content = Load();
            foreach (string seed in new[] { "SAVE-A", "SAVE-B", "SAVE-C", "SAVE-D" })
            {
                var straight = NewRun(content, seed);
                var reborn = NewRun(content, seed);
                for (int night = 1; night <= 8; night++)
                {
                    bool worked = night % 2 == 0;
                    PlayNight(straight, worked);
                    PlayNight(reborn, worked);

                    RunSnapshot snapStraight = null, snapReborn = null;
                    if (straight.Phase != TycoonPhase.DayEnd) break;   // bankruptcy ends both alike
                    straight.ContinueToNextDay(s => snapStraight = s);
                    reborn.ContinueToNextDay(s => snapReborn = s);
                    if (straight.Phase == TycoonPhase.Closed)
                    {
                        Assert.AreEqual(TycoonPhase.Closed, reborn.Phase, seed + " night " + night);
                        break;
                    }

                    // The whole state, written down, must match — this is the assertion that
                    // catches a forgotten field the moment it first diverges.
                    Assert.AreEqual(JsonUtility.ToJson(snapStraight), JsonUtility.ToJson(snapReborn),
                        seed + ": the twins' dawns differ after night " + night);

                    // And twin B is killed and reborn from that JSON, every single dawn.
                    string json = JsonUtility.ToJson(snapReborn);
                    reborn = Reborn(content, JsonUtility.FromJson<RunSnapshot>(json));

                    Assert.AreEqual(straight.Day, reborn.Day, seed);
                    Assert.AreEqual(straight.Money, reborn.Money, seed);
                    Assert.AreEqual(straight.Rating.Average, reborn.Rating.Average, 0.0, seed);
                    Assert.AreEqual(straight.Ledger.History.Count, reborn.Ledger.History.Count, seed);
                    Assert.AreEqual(straight.MenuRecipes.Count, reborn.MenuRecipes.Count, seed);
                    for (int b = 0; b < straight.Shelf.Bottles.Count; b++)
                    {
                        Assert.AreEqual(straight.Shelf.Bottles[b].Id, reborn.Shelf.Bottles[b].Id, seed);
                        Assert.AreEqual(straight.Shelf.Bottles[b].Remaining, reborn.Shelf.Bottles[b].Remaining, 0.0, seed);
                    }
                }
            }
        }

        [Test]
        public void ADressedBar_RidesTheSnapshotWhole()
        {
            // The preset stands a fitted two-star bar (fixtures, glass steps, stools, the
            // spoon); one dawn later the reborn twin must read the same room.
            var content = Load();
            var straight = NewRun(content, "SAVE-DRESSED");
            var reborn = NewRun(content, "SAVE-DRESSED");
            straight.DevPresetStars(2.0);
            reborn.DevPresetStars(2.0);
            PlayNight(straight, worked: true);
            PlayNight(reborn, worked: true);
            RunSnapshot snapStraight = null, snapReborn = null;
            straight.ContinueToNextDay(s => snapStraight = s);
            reborn.ContinueToNextDay(s => snapReborn = s);
            string json = JsonUtility.ToJson(snapReborn);
            Assert.AreEqual(JsonUtility.ToJson(snapStraight), json, "the dressed dawns differ");
            var back = Reborn(content, JsonUtility.FromJson<RunSnapshot>(json));
            Assert.AreEqual(straight.ComfortBase, back.ComfortBase, 0.0, "the room's comfort");
            Assert.AreEqual(straight.Seats, back.Seats);
            Assert.AreEqual(straight.CounterTier, back.CounterTier);
            Assert.AreEqual(straight.Rating.BestStanding, back.Rating.BestStanding, 0.0);

            // and the two runs' NEXT night agrees, floor deal and all
            PlayNight(straight, worked: false);
            PlayNight(back, worked: false);
            Assert.AreEqual(straight.Money, back.Money, "the night after the reborn dawn");
        }

        [Test]
        public void ASaveTheDataNoLongerHonours_IsRefusedWhole()
        {
            var content = Load();
            var run = NewRun(content, "SAVE-REFUSE");
            PlayNight(run, worked: false);
            RunSnapshot snap = null;
            run.ContinueToNextDay(s => snap = s);
            Assert.IsNotNull(snap);

            string good = JsonUtility.ToJson(snap);

            var wrongVersion = JsonUtility.FromJson<RunSnapshot>(good);
            wrongVersion.version = RunSnapshot.Version + 1;
            Assert.Throws<ArgumentException>(() => Reborn(content, wrongVersion),
                "a version this game does not read is refused");

            var wrongBottle = JsonUtility.FromJson<RunSnapshot>(good);
            wrongBottle.bottles[0].id = "bottle_the_data_never_had";
            Assert.Throws<ArgumentException>(() => Reborn(content, wrongBottle),
                "a bottle the data no longer has is refused");

            var wrongPage = JsonUtility.FromJson<RunSnapshot>(good);
            wrongPage.menu[0] = "page_the_data_never_had";
            Assert.Throws<ArgumentException>(() => Reborn(content, wrongPage),
                "a page the data no longer has is refused");

            var noStory = JsonUtility.FromJson<RunSnapshot>(good);
            Assert.Throws<ArgumentException>(() => TycoonRun.Restore(noStory, content.Recipes,
                content.AllCards, config: TycoonConfig.ForTheScene,
                regulars: new RegularsRegistry(content.Archetypes),
                glassware: content.Glassware, fixtures: content.Dressing.Fixtures, story: null),
                "a save with a story cannot land in a scene without one");
        }

        [Test]
        public void TheSnapshot_NeverInterruptsTheRunItReads()
        {
            // Writing the run down is a READ: a twin whose dawns carry the callback and a
            // twin whose dawns do not must play the same run.
            var content = Load();
            var with = NewRun(content, "SAVE-READONLY");
            var without = NewRun(content, "SAVE-READONLY");
            for (int night = 1; night <= 3; night++)
            {
                PlayNight(with, worked: night % 2 == 0);
                PlayNight(without, worked: night % 2 == 0);
                with.ContinueToNextDay(s => { });
                without.ContinueToNextDay();
                Assert.AreEqual(without.Money, with.Money, "night " + night);
                Assert.AreEqual(without.Rating.Average, with.Rating.Average, 0.0, "night " + night);
            }
        }
    }
}
