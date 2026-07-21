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

        [SerializeField] private string seed = "LASTCALL-DEV";

        /// <summary>The v4 loop (GDD 23) — what the scene plays.</summary>
        public TycoonRun Tycoon { get; private set; }

        public string CurrentSeed { get; private set; }

        /// <summary>Raised after a new run is dealt (including the initial one).</summary>
        public event System.Action RunStarted;

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
            var startingBottles = new List<ShelfBottle>();
            var brandCatalogue = new List<IngredientCard>();
            foreach (var card in bar.Cards)
            {
                if (card.Info != null && card.Info.Tier > 1) brandCatalogue.Add(card);
                else startingBottles.Add(new ShelfBottle(card.Clone()));
            }
            var recipes = DataLoader.ParseRecipes(recipesJson.text);
            var archetypes = archetypesJson != null ? DataLoader.ParseArchetypes(archetypesJson.text) : null;

            Tycoon = new TycoonRun(new Shelf(startingBottles), recipes, new RunRng(CurrentSeed),
                regulars: archetypes != null ? new RegularsRegistry(archetypes) : null,
                brandCatalogue: brandCatalogue);

            Debug.Log($"[LastCall] Tycoon run started — seed '{CurrentSeed}', " +
                      $"{startingBottles.Count} bottles, wallet ${Tycoon.Money}, " +
                      $"{(archetypes != null ? $"{archetypes.Count} archetypes" : "no emotion layer")}.");
            RunStarted?.Invoke();
        }
    }
}
