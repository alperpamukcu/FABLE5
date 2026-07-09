using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>The five Booster Pack types (GDD 7.1). Contents roll when the pack is bought.</summary>
    public enum PackKind
    {
        Cellar,     // ingredient cards, pick 1, $4
        Distiller,  // Recipe Books, pick 1, $4
        BarKit,     // Tools, pick 1, $4
        Regulars,   // Patrons, pick 1 of 2, $6
        Speakeasy   // rare hybrid (rare+ Patron / Tool / Book), pick 1, $8
    }

    /// <summary>Fixed pack metadata straight from GDD 7.1.</summary>
    public static class PackCatalog
    {
        public static readonly IReadOnlyList<PackKind> AllKinds =
            (PackKind[])Enum.GetValues(typeof(PackKind));

        public static int PriceOf(PackKind kind)
        {
            switch (kind)
            {
                case PackKind.Regulars: return 6;
                case PackKind.Speakeasy: return 8;
                default: return 4;
            }
        }

        public static string NameOf(PackKind kind)
        {
            switch (kind)
            {
                case PackKind.Cellar: return "Cellar Pack";
                case PackKind.Distiller: return "Distiller Pack";
                case PackKind.BarKit: return "Bar Kit";
                case PackKind.Regulars: return "Regulars Pack";
                default: return "Speakeasy Pack";
            }
        }
    }

    public enum PackOptionKind
    {
        IngredientCard, // joins the deck permanently
        Patron,         // sits at the bar (needs a free slot)
        Tool,           // joins the tool inventory (needs a free slot)
        Book            // levels the recipe permanently
    }

    /// <summary>One pickable reward inside an opened pack.</summary>
    public sealed class PackOption
    {
        public PackOptionKind Kind { get; }
        public IngredientCard Card { get; }
        public PatronDefinition Patron { get; }
        public ToolDefinition Tool { get; }
        public RecipeDefinition Recipe { get; }

        public string DisplayName
        {
            get
            {
                switch (Kind)
                {
                    case PackOptionKind.IngredientCard:
                        return Card.Quality == QualityTier.HousePour
                            ? $"{Card.Name} [{Card.Type} {Card.Flavor}]"
                            : $"{Card.Name} [{Card.Type} {Card.Flavor}, {Card.Quality}]";
                    case PackOptionKind.Patron: return $"Patron: {Patron.Name}";
                    case PackOptionKind.Tool: return $"Tool: {Tool.Name}";
                    default: return $"Recipe Book: {Recipe.Name}";
                }
            }
        }

        private PackOption(PackOptionKind kind, IngredientCard card, PatronDefinition patron,
            ToolDefinition tool, RecipeDefinition recipe)
        {
            Kind = kind;
            Card = card;
            Patron = patron;
            Tool = tool;
            Recipe = recipe;
        }

        internal static PackOption ForCard(IngredientCard card) =>
            new PackOption(PackOptionKind.IngredientCard, card, null, null, null);

        internal static PackOption ForPatron(PatronDefinition patron) =>
            new PackOption(PackOptionKind.Patron, null, patron, null, null);

        internal static PackOption ForTool(ToolDefinition tool) =>
            new PackOption(PackOptionKind.Tool, null, null, tool, null);

        internal static PackOption ForBook(RecipeDefinition recipe) =>
            new PackOption(PackOptionKind.Book, null, null, null, recipe);
    }

    /// <summary>A bought, opened pack waiting for the player to pick one option or skip.</summary>
    public sealed class OpenPackState
    {
        public PackKind Kind { get; }
        public IReadOnlyList<PackOption> Options { get; }

        internal OpenPackState(PackKind kind, IReadOnlyList<PackOption> options)
        {
            Kind = kind;
            Options = options;
        }
    }
}
