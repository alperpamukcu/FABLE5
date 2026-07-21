using System;
using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// Owns the game state in play mode: loads the data files and (re)starts full runs
    /// (8 Nights, GDD 5.1). The M1/M2 debug HUD renders and drives <see cref="Run"/>.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private TextAsset deckJson;
        [SerializeField] private TextAsset recipesJson;
        [SerializeField] private TextAsset patronsJson;
        [SerializeField] private TextAsset toolsJson;
        [SerializeField] private TextAsset vipsJson;
        [SerializeField] private TextAsset vouchersJson;

        /// <summary>
        /// Customer archetypes (GDD 19). Leave unassigned to run the pre-pivot loop: without
        /// it there is no emotional layer and no weekly quota.
        /// </summary>
        [SerializeField] private TextAsset archetypesJson;

        [SerializeField] private string seed = "LASTCALL-DEV";
        [SerializeField, Range(StakeTable.Min, StakeTable.Max)] private int stake = 1;
        [SerializeField] private string barId = "classic";

        public RunController Run { get; private set; }

        /// <summary>The v4 loop (GDD 23, PLAN P3) — what the scene actually plays now.</summary>
        public TycoonRun Tycoon { get; private set; }

        public string CurrentSeed { get; private set; }

        /// <summary>Raised after a new run is dealt (including the initial one).</summary>
        public event Action RunStarted;

        private void Start()
        {
            StartNewRun(seed);
        }

        /// <summary>Starts a fresh run. Null/empty seed keeps the inspector default.</summary>
        public void StartNewRun(string newSeed)
        {
            CurrentSeed = string.IsNullOrWhiteSpace(newSeed) ? seed : newSeed.Trim();

            var bar = DataLoader.ParseDeck(deckJson.text);
            // Tier 1 is the well you open with; higher tiers go to the end-of-night market.
            var startingBottles = new List<IngredientCard>();
            var brandCatalogue = new List<IngredientCard>();
            foreach (var card in bar.Cards)
            {
                if (card.Info != null && card.Info.Tier > 1) brandCatalogue.Add(card);
                else startingBottles.Add(card);
            }
            var recipes = DataLoader.ParseRecipes(recipesJson.text);
            var patronPool = DataLoader.ParsePatrons(patronsJson.text);
            var toolPool = DataLoader.ParseTools(toolsJson.text);
            var vipPool = DataLoader.ParseVips(vipsJson.text);
            var voucherPool = DataLoader.ParseVouchers(vouchersJson.text);
            var archetypes = archetypesJson != null
                ? DataLoader.ParseArchetypes(archetypesJson.text)
                : null;
            var barTheme = BarCatalog.Find(BarCatalog.CreateDefault(), barId);
            var config = StakeTable.Apply(RunConfig.Default, stake);

            Run = new RunController(startingBottles, recipes, new RunRng(CurrentSeed), config: config,
                patronPool: patronPool, toolPool: toolPool, vipPool: vipPool,
                voucherPool: voucherPool, bar: barTheme, archetypes: archetypes,
                brandCatalogue: brandCatalogue);

            // The tycoon run gets its own cloned cards and its own RunRng: the two loops
            // must never share mutable state or a random stream. The old run stays alive
            // for tests and the sim until PLAN P7 demolition.
            var tycoonBottles = new List<ShelfBottle>();
            foreach (var card in startingBottles) tycoonBottles.Add(new ShelfBottle(card.Clone()));
            Tycoon = new TycoonRun(new Shelf(tycoonBottles), recipes, new RunRng(CurrentSeed),
                regulars: archetypes != null ? new RegularsRegistry(archetypes) : null,
                brandCatalogue: brandCatalogue);

            var customer = Run.CurrentRound.Customer;
            Debug.Log($"[LastCall] Run started — seed '{CurrentSeed}', {barTheme.Name}, " +
                      $"Stake {stake} ({StakeTable.NameOf(stake)}), " +
                      $"{customer.Name} wants {customer.TargetScore}, wallet ${Run.Money}." +
                      (customer.HasEmotion
                          ? $" Reading: {customer.Read.Direction} {customer.Read.Intent}, " +
                            $"week quota {Run.Quota.Required}."
                          : " (no emotion layer)"));
            RunStarted?.Invoke();
        }
    }
}
