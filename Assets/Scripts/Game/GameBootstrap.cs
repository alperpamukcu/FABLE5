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

        [SerializeField] private string seed = "LASTCALL-DEV";

        /// <summary>The v4 loop (GDD 23) — what the scene plays.</summary>
        public TycoonRun Tycoon { get; private set; }

        /// <summary>The glass set (v5 P10); empty until the scene wires glasswareJson.</summary>
        public IReadOnlyList<GlasswareDefinition> Glassware { get; private set; }

        /// <summary>The snack shelf (v5 P10); empty until the scene wires snacksJson.</summary>
        public IReadOnlyList<SnackDefinition> Snacks { get; private set; }

        /// <summary>Future stock (v5 P10): locked bottles the shop can sell later.</summary>
        public IReadOnlyList<IngredientCard> LockedStock { get; private set; }

        public string CurrentSeed { get; private set; }

        /// <summary>Raised after a new run is dealt (including the initial one).</summary>
        public event System.Action RunStarted;

        /// <summary>
        /// Pins the whole game to one culture, before anything formats or parses a number.
        ///
        /// The rating printed as "3,0" is the visible half of this. The dangerous half is that
        /// the same setting decides how "0.75" is READ, so a glass profile or a ratio band
        /// could come out of a data file meaning seventy-five instead of three quarters on
        /// someone else's desktop. The UI is English throughout, so there is nothing to
        /// localise and one culture everywhere is the whole fix.
        ///
        /// Invariant with one amendment: it writes a percent as "75 %", and Turkish writes it
        /// "%75", so `:P0` was being patched at four call sites with a string replace that
        /// only ever worked under one of them. Setting the pattern here means every `:P0` in
        /// the project reads "75%" and no call site has to know about it.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PinCulture()
        {
            var culture = (System.Globalization.CultureInfo)
                System.Globalization.CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.PercentPositivePattern = 1;   // "n%"
            culture.NumberFormat.PercentNegativePattern = 1;   // "-n%"

            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
        }

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
                brandCatalogue: brandCatalogue);

            Debug.Log($"[LastCall] Tycoon run started — seed '{CurrentSeed}', " +
                      $"{startingBottles.Count} bottles, wallet ${Tycoon.Money}, " +
                      $"{(archetypes != null ? $"{archetypes.Count} archetypes" : "no emotion layer")}.");
            RunStarted?.Invoke();
        }
    }
}
