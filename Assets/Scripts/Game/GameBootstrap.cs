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

        /// <summary>The serving glasses and the snack bowls (v5 P10). Parsed and validated at
        /// boot so a bad file fails loudly today, even though the run consumes them in P14/P16.</summary>
        [SerializeField] private TextAsset glasswareJson;
        [SerializeField] private TextAsset snacksJson;

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

        /// <summary>The snack shelf (v5 P10); empty until the scene wires snacksJson.</summary>
        public IReadOnlyList<SnackDefinition> Snacks { get; private set; }

        /// <summary>Future stock (v5 P10): locked bottles the shop can sell later.</summary>
        public IReadOnlyList<IngredientCard> LockedStock { get; private set; }

        /// <summary>Who the bar can draw, by face (2026-08-12). Presentation data, like the
        /// stage slots: the licence prints it and Core never sees it.</summary>
        public PatronRoster Cast { get; private set; }

        /// <summary>The written nights, or null for a run with no story file (GDD 26). The
        /// run holds its own progress through it; this is the content it was built from, kept
        /// so the UI can read a character's lines and borrowed face without asking Core.</summary>
        public StoryArc Story { get; private set; }

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

        private void Start()
        {
            StartNewRun(seed);
        }

        /// <summary>Starts a fresh run. Null/empty seed keeps the inspector default.</summary>
        public void StartNewRun(string newSeed)
        {
            CurrentSeed = string.IsNullOrWhiteSpace(newSeed) ? seed : newSeed.Trim();

            var bar = DataLoader.ParseDeck(deckJson.text);
            // You open with a bare well — a couple of spirits and the essential mixers — and
            // grow the shelf by buying new stock at the end of each night (2026-07-23). Every
            // other bottle goes to the market catalogue.
            var startingStock = new HashSet<string>
            {
                "vodka_astra", "gin_boothby", "soda_klara", "lemon_fresh", "syrup_house",
                // One keg on from day one (GDD 21 §10): beer is the order you can always
                // answer, and a bar with no tap would make the simple customer unservable.
                // The other two kegs are stock to buy, like any other brand.
                "beer_kestrel",
            };
            var startingBottles = new List<ShelfBottle>();
            var brandCatalogue = new List<IngredientCard>();
            foreach (var card in bar.Cards)
            {
                if (startingStock.Contains(card.Id)) startingBottles.Add(new ShelfBottle(card.Clone()));
                else brandCatalogue.Add(card);
            }
            if (startingBottles.Count == 0)   // data drift safety: never open with an empty shelf
                foreach (var card in bar.Cards)
                    if (card.Info == null || card.Info.Tier <= 1)
                        startingBottles.Add(new ShelfBottle(card.Clone()));
            var recipes = DataLoader.ParseRecipes(recipesJson.text);
            var archetypes = archetypesJson != null ? DataLoader.ParseArchetypes(archetypesJson.text) : null;

            // v5 P10: parse the new content files loudly even though nothing consumes them
            // yet — a typo must fail at boot, not in P14. Recipe glass ids are checked
            // against the glass set here, the one place both files are in hand.
            Glassware = glasswareJson != null
                ? DataLoader.ParseGlassware(glasswareJson.text)
                : System.Array.Empty<GlasswareDefinition>();
            Snacks = snacksJson != null
                ? DataLoader.ParseSnacks(snacksJson.text)
                : System.Array.Empty<SnackDefinition>();
            var dressing = fixturesJson != null
                ? DataLoader.ParseFixtures(fixturesJson.text)
                : new LoadedFixtures(System.Array.Empty<FixtureDefinition>(),
                                     System.Array.Empty<StageSlot>());
            StageSlots = dressing.Slots;
            Cast = papersJson != null ? DataLoader.ParsePapers(papersJson.text) : null;
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

            Tycoon = new TycoonRun(new Shelf(startingBottles), recipes, new RunRng(CurrentSeed),
                regulars: archetypes != null ? new RegularsRegistry(archetypes) : null,
                brandCatalogue: brandCatalogue,
                glassware: Glassware,
                snacks: Snacks,
                lockedStock: LockedStock,
                fixtures: dressing.Fixtures,
                story: storyInPlay ? Story : null);

            Debug.Log($"[LastCall] Tycoon run started — seed '{CurrentSeed}', " +
                      $"{startingBottles.Count} bottles, wallet ${Tycoon.Money}, " +
                      $"{(archetypes != null ? $"{archetypes.Count} archetypes" : "no emotion layer")}.");
            RunStarted?.Invoke();
        }
    }
}
