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
            // You open with a bare well — a couple of spirits and the essential mixers — and
            // grow the shelf by buying new stock at the end of each night (2026-07-23). Every
            // other bottle goes to the market catalogue.
            var startingStock = new HashSet<string>
            {
                "vodka_astra", "gin_boothby", "soda_klara", "lemon_fresh", "syrup_house",
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
