using System;
using System.Linq;
using LastCall.Core;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// Owns the game state in play mode: loads the data files and (re)starts customer
    /// rounds. The M1 debug HUD renders and drives <see cref="Round"/> through this.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private TextAsset deckJson;
        [SerializeField] private TextAsset recipesJson;
        [SerializeField] private string seed = "LASTCALL-DEV";
        [SerializeField] private string customerName = "First Regular";
        [SerializeField] private double targetScore = 300;

        public RoundController Round { get; private set; }
        public RunRng Rng { get; private set; }
        public string CurrentSeed { get; private set; }

        /// <summary>Raised after a new round is dealt (including the initial one).</summary>
        public event Action RoundStarted;

        private void Start()
        {
            StartNewRound(seed, targetScore);
        }

        /// <summary>Deals a fresh round. Null/empty seed keeps the inspector default.</summary>
        public void StartNewRound(string newSeed, double newTarget)
        {
            CurrentSeed = string.IsNullOrWhiteSpace(newSeed) ? seed : newSeed.Trim();
            if (newTarget <= 0) newTarget = targetScore;

            var loadedDeck = DataLoader.ParseDeck(deckJson.text);
            var recipes = DataLoader.ParseRecipes(recipesJson.text);

            Rng = new RunRng(CurrentSeed);
            var deck = new Deck(loadedDeck.Cards);
            deck.Shuffle(Rng.GetStream("deck"));

            Round = new RoundController(deck, recipes, new CustomerOrder(customerName, newTarget));

            string rail = string.Join(", ", Round.Rail.Select(c => c.ToString()));
            Debug.Log($"[LastCall] Seed '{CurrentSeed}' — {Round.Customer.Name} wants {Round.Customer.TargetScore}. Rail: {rail}");
            RoundStarted?.Invoke();
        }
    }
}
