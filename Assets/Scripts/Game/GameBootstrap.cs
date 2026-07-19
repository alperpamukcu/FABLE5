using System;
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

            var deck = DataLoader.ParseDeck(deckJson.text);
            var recipes = DataLoader.ParseRecipes(recipesJson.text);
            var patronPool = DataLoader.ParsePatrons(patronsJson.text);
            var toolPool = DataLoader.ParseTools(toolsJson.text);
            var vipPool = DataLoader.ParseVips(vipsJson.text);
            var voucherPool = DataLoader.ParseVouchers(vouchersJson.text);
            var archetypes = archetypesJson != null
                ? DataLoader.ParseArchetypes(archetypesJson.text)
                : null;
            var bar = BarCatalog.Find(BarCatalog.CreateDefault(), barId);
            var config = StakeTable.Apply(RunConfig.Default, stake);

            Run = new RunController(deck.Cards, recipes, new RunRng(CurrentSeed), config: config,
                patronPool: patronPool, toolPool: toolPool, vipPool: vipPool,
                voucherPool: voucherPool, bar: bar, archetypes: archetypes);

            var customer = Run.CurrentRound.Customer;
            Debug.Log($"[LastCall] Run started — seed '{CurrentSeed}', {bar.Name}, " +
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
