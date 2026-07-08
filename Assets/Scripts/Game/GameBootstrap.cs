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
        [SerializeField] private string seed = "LASTCALL-DEV";

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

            Run = new RunController(deck.Cards, recipes, new RunRng(CurrentSeed),
                patronPool: patronPool, toolPool: toolPool);

            Debug.Log($"[LastCall] Run started — seed '{CurrentSeed}', " +
                      $"{Run.CurrentRound.Customer.Name} wants {Run.CurrentRound.Customer.TargetScore}, wallet ${Run.Money}.");
            RunStarted?.Invoke();
        }
    }
}
