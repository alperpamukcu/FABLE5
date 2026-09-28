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
            public QuestBook Quests;
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
            // The hostess's book (2026-09-27), from where the game loads it: the gold walk carries the chain too.
            content.Quests = DataLoader.ParseQuests(
                File.ReadAllText(Path.Combine(Application.dataPath, "Resources", "Data", "quests.json")),
                content.Dressing.Fixtures);
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
                story: content.Story,
                quests: content.Quests);
        }

        private static TycoonRun Reborn(Content content, RunSnapshot snap) =>
            TycoonRun.Restore(snap, content.Recipes, content.AllCards,
                config: TycoonConfig.ForTheScene,
                regulars: new RegularsRegistry(content.Archetypes),
                glassware: content.Glassware,
                fixtures: content.Dressing.Fixtures,
                story: content.Story,
                quests: content.Quests);

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

            var noBook = JsonUtility.FromJson<RunSnapshot>(good);
            Assert.Throws<ArgumentException>(() => TycoonRun.Restore(noBook, content.Recipes,
                content.AllCards, config: TycoonConfig.ForTheScene,
                regulars: new RegularsRegistry(content.Archetypes),
                glassware: content.Glassware, fixtures: content.Dressing.Fixtures, story: content.Story,
                quests: null),
                "a save written with the hostess's book cannot land in a scene without one");

            var goneJob = JsonUtility.FromJson<RunSnapshot>(good);
            Assert.IsTrue(goneJob.quest.has, "she handed a job over at the first close");
            goneJob.quest.id = "a_job_the_book_never_had";
            Assert.Throws<ArgumentException>(() => Reborn(content, goneJob), "a job the book no longer has is refused");
        }

        /// <summary>
        /// THE JOB ON THE BAR RIDES THE SAVE WHOLE (2026-09-27): its row, the drink she picked, how far along it is,
        /// what it pays, the night it was handed over, where the book stands and when she comes next — and the
        /// unread "done" flash a state goal raises at the dawn itself.
        /// </summary>
        [Test]
        public void TheChainRidesTheSaveWhole()
        {
            var content = Load();
            var run = NewRun(content, "SAVE-CHAIN");
            PlayNight(run, worked: false);
            RunSnapshot dawn = null;
            run.ContinueToNextDay(s => dawn = s);
            Assert.AreEqual("first_wage", dawn.quest.id, "handed over at the close of night one");
            Assert.AreEqual(1, dawn.questFormat);

            // Mid-job: two of three poured, and the flash still unread.
            dawn.quest.progress = 2;
            dawn.questJustDone = true;
            string json = JsonUtility.ToJson(dawn);
            var back = Reborn(content, JsonUtility.FromJson<RunSnapshot>(json));
            Assert.AreEqual("first_wage", back.Quest.Id);
            Assert.AreEqual(dawn.quest.recipeId, back.Quest.RecipeId);
            Assert.AreEqual(2, back.Quest.Progress);
            Assert.AreEqual(dawn.quest.target, back.Quest.Target);
            Assert.AreEqual(dawn.quest.reward, back.Quest.Reward);
            Assert.AreEqual(1, back.Quest.GivenDay);
            Assert.AreEqual(dawn.questNext, back.QuestNextUp.Index, "the book stands where it stood");
            Assert.AreEqual(dawn.questVisitFrom, back.HostessComesOn);
            Assert.AreSame(back.Quest, back.TakeQuestJustDone(), "and the flash is still owed");

            // And it goes on being the same run: the reborn dawn writes the dawn it was reborn from.
            RunSnapshot again = null;
            PlayNight(back, worked: false);
            back.ContinueToNextDay(s => again = s);
            Assert.AreEqual(2, again.quest.progress);
            Assert.AreEqual(dawn.quest.recipeId, again.quest.recipeId);
            Assert.AreEqual(dawn.questNext, again.questNext);
        }

        /// <summary>
        /// A SAVE FROM BEFORE THE BOOK (2026-09-27). Its file has no quest fields at all — JsonUtility reads each as
        /// its default — and may carry a weekly job. The job's keys have had no field since it was deleted
        /// (2026-09-28), so they are skipped (it was paid the moment it finished; nothing owed is lost), and the chain
        /// starts at the save's own dawn: she comes at the close of the night it resumes.
        /// </summary>
        [Test]
        public void AnOldSaveStartsTheChain()
        {
            var content = Load();
            var run = NewRun(content, "SAVE-OLD");
            for (int night = 0; night < 3; night++)
            {
                PlayNight(run, worked: night % 2 == 1);
                if (night < 2) run.ContinueToNextDay();
            }
            RunSnapshot snap = null;
            run.ContinueToNextDay(s => snap = s);

            // Written down the way the game wrote it before 2026-09-27.
            snap.questFormat = 0;
            snap.hasQuests = false;
            snap.questNext = snap.questVisitFrom = snap.questsSkipped = 0;
            snap.questDoneUnsaid = snap.questJustDone = false;
            snap.quest = new RunSnapshot.QuestState();
            // ...with the weekly job it carried, in the keys and the shape the old writer gave it (its signer's name
            // included). The snapshot has no field for any of them now, so they go in as text.
            var page = content.Recipes.First(r => r.HasAuthoredRatios && !r.Locked);
            string Quoted(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            string json = JsonUtility.ToJson(snap);
            json = json.Substring(0, json.LastIndexOf('}'))
                   + ",\"jobGiver\":\"ECE\""
                   + ",\"job\":{\"has\":true,\"kind\":0,\"recipeId\":" + Quoted(page.Id)
                   + ",\"recipeName\":" + Quoted(page.Name) + ",\"target\":3,\"served\":1,\"week\":"
                   + BarCalendar.WeekOf(snap.day) + ",\"who\":\"ECE\",\"reward\":12}"
                   + ",\"jobDone\":{\"has\":false},\"jobJustDone\":1}";
            StringAssert.Contains("\"job\":{\"has\":true", json, "the old file's weekly job is in the text");

            var back = Reborn(content, JsonUtility.FromJson<RunSnapshot>(json));
            Assert.IsNull(back.Quest, "nothing is on the bar yet");
            Assert.AreEqual(snap.day, back.HostessComesOn, "she comes at the close of the night it resumes");
            Assert.IsNull(back.TakeQuestJustDone(), "the weekly job's unread flash is not hers to show");
            Assert.AreEqual("first_wage", back.QuestNextUp?.Id, "and the book starts at its first row");

            int guard = 0;
            while (back.HostessVisit == null)
            {
                Assert.Less(guard++, 20000);
                Assert.AreEqual(TycoonPhase.DayOpen, back.Phase, "she comes before the night closes");
                back.Tick(0.25);
            }
            Assert.AreEqual(snap.day, back.HostessVisit.Day);
            Assert.AreEqual("first_wage", back.HostessVisit.Offered?.Id);
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
