using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// Owns the game state in play mode: loads the data files and (re)starts full runs.
    /// The tycoon HUD renders and drives <see cref="Tycoon"/> (GDD 23/24).
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private TextAsset deckJson;
        [SerializeField] private TextAsset recipesJson;

        /// <summary>Customer archetypes (GDD 19). Leave unassigned for an anonymous crowd.</summary>
        [SerializeField] private TextAsset archetypesJson;

        /// <summary>The serving glasses (v5 P10). Parsed and validated at boot so a bad file
        /// fails loudly today. (The snack file that stood beside it left with the snacks, 2026-09-15.)</summary>
        [SerializeField] private TextAsset glasswareJson;

        /// <summary>The bar-dressing catalogue (2026-08-10). Leave unassigned for a bare room.</summary>
        [SerializeField] private TextAsset fixturesJson;

        /// <summary>The cast's papers — what the licence prints per face (2026-08-12).
        /// Leave unassigned and every drinker is called by their archetype name instead.</summary>
        [SerializeField] private TextAsset papersJson;

        /// <summary>The written nights (GDD 26). Leave unassigned and the run has no last
        /// customer at all — the story is opt-in exactly like the regulars, which is what
        /// keeps a bench scene and an older test valid.</summary>
        [SerializeField] private TextAsset storyJson;

        /// <summary>
        /// Whether the parsed arc is handed to the RUN, i.e. whether a last customer actually
        /// walks in (GDD 26). Off until the dialogue plate exists (PLAN_last_call S3): the
        /// guest arrives into a conversation and holds their clock until it ends, so a scene
        /// that cannot talk would sit a silent stranger on a stool and stall the night for the
        /// length of the talking grace. The file is still PARSED at boot either way — a typo
        /// in the story has to fail here, not in six weeks — and the simulator plays the arc
        /// regardless, because balance cannot wait for a plate. Delete this field with S3.
        /// </summary>
        [SerializeField] private bool storyInPlay;

        [SerializeField] private string seed = "LASTCALL-DEV";

        /// <summary>The v4 loop (GDD 23) — what the scene plays.</summary>
        public TycoonRun Tycoon { get; private set; }

        /// <summary>The glass set (v5 P10); empty until the scene wires glasswareJson.</summary>
        public IReadOnlyList<GlasswareDefinition> Glassware { get; private set; }

        /// <summary>Future stock (v5 P10): locked bottles the shop can sell later.</summary>
        public IReadOnlyList<IngredientCard> LockedStock { get; private set; }

        /// <summary>Who the bar can draw, by face (2026-08-12). Presentation data, like the
        /// stage slots: the licence prints it and Core never sees it.</summary>
        public PatronRoster Cast { get; private set; }

        /// <summary>The strangers (2026-09-22): men the bar never draws, whose licences a minor borrows —
        /// <c>Resources/Data/strangers.json</c>, read like the voices, so the scene needs no new wire. Null when
        /// the file is missing: a borrowed card then wears another drinker's face, as it did before.</summary>
        public PatronRoster Strangers { get; private set; }

        /// <summary>The crowd's voices (2026-09-06) — <c>Resources/Data/voices.json</c>, read
        /// the way the menu's lore is, so the scene needs no new wire for it. Null when the
        /// file is missing: the balloons then say the plain lines they always said.</summary>
        public VoiceBook Voices { get; private set; }

        /// <summary>The written nights, or null for a run with no story file (GDD 26). The
        /// run holds its own progress through it; this is the content it was built from, kept
        /// so the UI can read a character's lines and borrowed face without asking Core.</summary>
        public StoryArc Story { get; private set; }

        /// <summary>The hostess's book (2026-09-27) — <c>Resources/Data/quests.json</c>, read like the voices so the
        /// scene needs no new wire. Unlike the voices it is NOT optional: a missing book is a boot error, never a
        /// quietly chain-less run. The run holds its own place in it; the UI reads the lines from here.</summary>
        public QuestBook Quests { get; private set; }

        /// <summary>The house tour (2026-09-28, TycoonRun.Tour) — <c>Resources/Data/tour.json</c>, read loudly like the
        /// book. Null when the file is missing: the first night then opens the way it did before there was one.</summary>
        public TourScript Tour { get; private set; }

        /// <summary>
        /// Whether a NEW RUN opens on the tour. On in the game; the PlayMode fixtures switch it off for their session
        /// (they play the first night by hand, with their own mouse, and a held door would stop them) and it comes back
        /// on at every play, as <see cref="Ceremony.Pace"/> does. A resumed bar never has one: the tour lives inside a
        /// first night, and a save is written at a dawn.
        /// </summary>
        public static bool TourForNewRuns { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTourSwitch() => TourForNewRuns = true;

        /// <summary>Where the room stands its bought dressing (2026-08-10). Presentation
        /// data: it comes out of the same file as the fixtures but never enters Core.</summary>
        public IReadOnlyList<StageSlot> StageSlots { get; private set; }
            = System.Array.Empty<StageSlot>();

        public string CurrentSeed { get; private set; }

        /// <summary>Raised after a new run is dealt (including the initial one).</summary>
        public event System.Action RunStarted;

        /// <summary>Pins the game's number culture before the first scene loads
        /// (see <see cref="RunCulture"/> for why this is not a display preference).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PinCulture() => RunCulture.Pin();

        private void Awake()
        {
            // A STEADY FRAME, NOT THE MOST FRAMES (2026-09-11, the author: "Oyunda FPS sorunu
            // yaşanmamalı ... oyun çok düşük sistemlerde de çalışmalı"). Both quality levels
            // ship with vSync off and nothing capped the frame rate, so a build ran as fast as
            // the machine would go — 160-260 fps measured in the editor, the CPU and the GPU
            // flat out for a picture that moves at the speed of a bar. On a cheap laptop that
            // is heat, then throttling, then exactly the dropped frames this is meant to
            // prevent. At the display's own rate the pacing is smooth and the machine idles.
            // Player builds only: the editor and the PlayMode suite keep their own pacing
            // (the suite counts on fast frames — CLAUDE.md, "Verifying changes").
            // THE PLAYER'S FRAME RATE (2026-09-26, the settings' DISPLAY page): MATCH SCREEN, the
            // default, is exactly the two lines that stood here (vsync 1, no target); a cap of 60,
            // 120 or 144 turns vsync off and targets it. DisplayOptions keeps the player-only guard.
            DisplayOptions.ApplyPacing();
            // THE ACHIEVEMENTS LISTEN TO WHICHEVER RUN IS BEING PLAYED (2026-09-28): a fresh bar, a saved one
            // taken up again, or one carried across a language reload — every one of them raises this.
            RunStarted += () => Achievements.Attach(Tycoon);
        }

        private void Update() => Achievements.Step();

        private void OnApplicationQuit() => Achievements.Flush();

        /// <summary>The scene came up around a run that was already going (a language
        /// reload) — the front door must not reappear over a night in progress.</summary>
        public bool ResumedAcrossReload { get; private set; }

        /// <summary>The reload was asked for FROM the front door (its settings' LANGUAGE → APPLY, 2026-09-29): the door
        /// comes back over the run handed across, the way it stood before the reload, instead of the player landing in a
        /// bar they never chose (read_menus finding 2).</summary>
        public bool DoorAcrossReload { get; private set; }

        private void Start()
        {
            ResumedAcrossReload = s_handoff != null;
            DoorAcrossReload = s_handoff != null && s_handoffDoor;
            s_handoffDoor = false;
            StartNewRun(seed);
            if (s_handoff != null)
            {
                // The scene was reloaded around a run that was already going (ReloadKeepingRun): the fresh run
                // StartNewRun just made is dropped and the one handed across takes its place, content and all.
                Tycoon = s_handoff;
                CurrentSeed = s_handoffSeed ?? CurrentSeed;
                s_handoff = null; s_handoffSeed = null;
                RunStarted?.Invoke();
            }
        }

        // THE SCENE, RELOADED AROUND THE SAME RUN (2026-09-16, the author: "ESC menüsünde dil seçildikten sonra
        // uygula dendiğinde oyunun dili direkt değişmeli"). Every word of the HUD is baked as it is built, so a
        // language is applied by building the scene again — and the run is handed across the reload in a static,
        // so the night goes on exactly where it was, in the new words.
        private static TycoonRun s_handoff;
        private static string s_handoffSeed;
        private static bool s_handoffDoor;

        /// <param name="backToTheDoor">The front door was up (its settings asked for the reload): it is offered again
        /// over the same run (<see cref="DoorAcrossReload"/>).</param>
        public void ReloadKeepingRun(bool backToTheDoor = false)
        {
            s_handoff = Tycoon;
            s_handoffSeed = CurrentSeed;
            s_handoffDoor = backToTheDoor;
            Time.timeScale = 1f;   // the menu that asked for this had stopped the clock
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
        }

        /// <summary>Everything a run is built FROM, loaded in one place so a fresh run and a
        /// restored one read the very same files (the save carries ids and numbers only —
        /// TycoonRun.Save.cs).</summary>
        private sealed class Content
        {
            public LoadedDeck Bar;
            public IReadOnlyList<RecipeDefinition> Recipes;
            public IReadOnlyList<ArchetypeDefinition> Archetypes;
            public LoadedFixtures Dressing;
        }

        private Content LoadContent()
        {
            var bar = DataLoader.ParseDeck(deckJson.text);
            var recipes = DataLoader.ParseRecipes(recipesJson.text);
            var archetypes = archetypesJson != null ? DataLoader.ParseArchetypes(archetypesJson.text) : null;

            // v5 P10: parse the new content files loudly even though nothing consumes them
            // yet — a typo must fail at boot, not in P14. Recipe glass ids are checked
            // against the glass set here, the one place both files are in hand.
            Glassware = glasswareJson != null
                ? DataLoader.ParseGlassware(glasswareJson.text)
                : System.Array.Empty<GlasswareDefinition>();
            var dressing = fixturesJson != null
                ? DataLoader.ParseFixtures(fixturesJson.text)
                : new LoadedFixtures(System.Array.Empty<FixtureDefinition>(),
                                     System.Array.Empty<StageSlot>());
            StageSlots = dressing.Slots;
            // The book names rungs of the room's ladders, so it is read against the catalogue just parsed.
            var questsJson = Resources.Load<TextAsset>("Data/quests");
            if (questsJson == null)
                throw new System.FormatException(
                    "Resources/Data/quests.json is missing: the hostess has no book, and a run without one is not the game.");
            Quests = DataLoader.ParseQuests(questsJson.text, dressing.Fixtures);
            var tourJson = Resources.Load<TextAsset>("Data/tour");
            Tour = tourJson != null ? DataLoader.ParseTour(tourJson.text) : null;
            Cast = papersJson != null ? DataLoader.ParsePapers(papersJson.text) : null;
            var strangersJson = Resources.Load<TextAsset>("Data/strangers");
            Strangers = strangersJson != null ? DataLoader.ParsePapers(strangersJson.text) : null;
            var voicesJson = Resources.Load<TextAsset>("Data/voices");
            Voices = voicesJson != null ? DataLoader.ParseVoices(voicesJson.text) : null;
            // The arc needs the cast and the book in hand — a story character IS a face plus
            // its papers, and every ask is graded against a real recipe (GDD 26 §8/§10).
            Story = storyJson != null && Cast != null
                ? DataLoader.ParseStory(storyJson.text, Cast, recipes)
                : null;
            LockedStock = bar.LockedCards;
            if (Glassware.Count > 0)
            {
                var glassIds = new HashSet<string>();
                foreach (var glass in Glassware) glassIds.Add(glass.Id);
                foreach (var recipe in recipes)
                    if (!string.IsNullOrEmpty(recipe.GlassId) && !glassIds.Contains(recipe.GlassId))
                        throw new System.FormatException(
                            $"Recipe '{recipe.Id}' names unknown glass '{recipe.GlassId}'.");
            }
            return new Content { Bar = bar, Recipes = recipes, Archetypes = archetypes, Dressing = dressing };
        }

        /// <summary>
        /// The player's own fresh start — the menu's NEW RUN and the settings' START OVER.
        /// Clears the save FIRST: a bar on its opening night has nothing worth continuing,
        /// and CONTINUE must never bring back the run the player just walked away from.
        /// The cold-boot deal in <see cref="Start"/> calls <see cref="StartNewRun"/> directly
        /// and clears nothing — booting the game must never eat the save.
        /// </summary>
        public void StartFreshRun(string newSeed)
        {
            SaveStore.Clear();
            StartNewRun(newSeed);
        }

        /// <summary>
        /// The menu's CONTINUE: the run stood back up from its dawn snapshot, over freshly
        /// loaded content. False — with the reason logged, and the save left in place — when
        /// the snapshot cannot be honoured exactly (drifted data, wrong version): a save is
        /// restored whole or not at all.
        /// </summary>
        public bool TryStartSavedRun(RunSnapshot snap)
        {
            if (snap == null) return false;
            try
            {
                var content = LoadContent();
                var allCards = new List<IngredientCard>(content.Bar.Cards);
                allCards.AddRange(content.Bar.LockedCards);
                var run = TycoonRun.Restore(snap, content.Recipes, allCards,
                    config: TycoonConfig.ForTheScene,
                    regulars: content.Archetypes != null ? new RegularsRegistry(content.Archetypes) : null,
                    glassware: Glassware,
                    fixtures: content.Dressing.Fixtures,
                    story: storyInPlay ? Story : null,
                    quests: Quests);
                Tycoon = run;
                CurrentSeed = snap.seed;
                Debug.Log($"[LastCall] Run resumed — seed '{CurrentSeed}', night {run.Day}, " +
                          $"wallet ${run.Money}, standing {run.Rating.Average:0.00}.");
                RunStarted?.Invoke();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LastCall] The save could not be honoured: {e.Message}");
                return false;
            }
        }

        /// <summary>Starts a fresh run. Null/empty seed keeps the inspector default.</summary>
        public void StartNewRun(string newSeed)
        {
            CurrentSeed = string.IsNullOrWhiteSpace(newSeed) ? seed : newSeed.Trim();

            var content = LoadContent();
            var bar = content.Bar;
            // You open with a bare well and grow the shelf by buying new stock at the end of each
            // night (2026-07-23); every other bottle goes to the market catalogue. WHICH WELL IS DATA
            // since 2026-09-26 — the cards base_bar.json marks "starting": three spirits, three
            // mixers, lemon, syrup and one keg (GDD 21 §10: beer is the order you can always answer).
            // It used to be six ids written here, which could not pour two of the six pages the bar
            // opens with, so the first night asked for drinks the bar could not make.
            var startingBottles = new List<ShelfBottle>();
            var brandCatalogue = new List<IngredientCard>();
            foreach (var card in bar.Cards)
            {
                if (bar.IsStarting(card)) startingBottles.Add(new ShelfBottle(card.Clone()));
                else brandCatalogue.Add(card);
            }
            if (startingBottles.Count == 0)   // data drift safety: never open with an empty shelf
                foreach (var card in bar.Cards)
                    if (card.Info == null || card.Info.Tier <= 1)
                        startingBottles.Add(new ShelfBottle(card.Clone()));
            Tycoon = new TycoonRun(new Shelf(startingBottles), content.Recipes, new RunRng(CurrentSeed),
                config: TycoonConfig.ForTheScene,
                regulars: content.Archetypes != null ? new RegularsRegistry(content.Archetypes) : null,
                brandCatalogue: brandCatalogue,
                glassware: Glassware,
                lockedStock: LockedStock,
                fixtures: content.Dressing.Fixtures,
                story: storyInPlay ? Story : null,
                quests: Quests,
                tour: TourForNewRuns ? Tour : null);

            Debug.Log($"[LastCall] Tycoon run started — seed '{CurrentSeed}', " +
                      $"{startingBottles.Count} bottles, wallet ${Tycoon.Money}, " +
                      $"{(content.Archetypes != null ? $"{content.Archetypes.Count} archetypes" : "no emotion layer")}.");
            RunStarted?.Invoke();
        }
    }
}
