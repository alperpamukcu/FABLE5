using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    /// <summary>
    /// Pure scoring function (GDD 02 + 13): no rendering, no state mutation outside the
    /// patron instances passed in, no randomness. Order of operations:
    ///   1. Base Flavor and Mult from the recipe at its current level.
    ///   2. Each scored card left to right: its Flavor value, its per-card effects
    ///      (quality tier), then patron OnCardScored triggers in slot order. Retriggers
    ///      repeat the whole card scoring (retrigger ops themselves fire only once).
    ///   3. Patron OnHandScored effects in slot order.
    ///   4. FinalScore = Flavor × Mult.
    /// A mix that matched no recipe scores 0 and fires no patron effects.
    /// </summary>
    public static class ScoringEngine
    {
        public static ScoreBreakdown Score(RecipeMatch match, int recipeLevel = 1) =>
            Score(match, recipeLevel, null, EffectContext.Empty);

        public static ScoreBreakdown Score(RecipeMatch match, int recipeLevel,
            IReadOnlyList<PatronInstance> patrons, EffectContext context)
        {
            if (match == null) return ScoreBreakdown.NoRecipe;
            patrons = patrons ?? System.Array.Empty<PatronInstance>();
            context = context ?? EffectContext.Empty;

            var recipe = match.Recipe;
            var steps = new List<ScoreStep>();
            double flavor = recipe.FlavorAtLevel(recipeLevel);
            double mult = recipe.MultAtLevel(recipeLevel);
            steps.Add(new ScoreStep($"{recipe.Name} (Lv{recipeLevel})", EffectOp.AddFlavor, flavor, flavor, mult));

            for (int index = 0; index < match.ScoredCards.Count; index++)
            {
                var card = match.ScoredCards[index];
                int retriggers = ScoreCardOnce(card, index, patrons, context, steps,
                    allowRetriggerOps: true, ref flavor, ref mult);
                for (int r = 0; r < retriggers; r++)
                {
                    ScoreCardOnce(card, index, patrons, context, steps,
                        allowRetriggerOps: false, ref flavor, ref mult);
                }
            }

            ApplyHandEffects(patrons, context, steps, ref flavor, ref mult);

            return new ScoreBreakdown(recipe, recipeLevel, match.ScoredCards, steps, flavor, mult);
        }

        /// <summary>Scores one pass of a card; returns how many retriggers were requested.</summary>
        private static int ScoreCardOnce(IngredientCard card, int index,
            IReadOnlyList<PatronInstance> patrons, EffectContext context, List<ScoreStep> steps,
            bool allowRetriggerOps, ref double flavor, ref double mult)
        {
            // VIP debuff (GDD 6): the card is played but scores nothing and triggers
            // nothing — no flavor, no quality/enhancement, no patron card effects.
            if (context.DebuffedTypes.Contains(card.Type))
            {
                steps.Add(new ScoreStep($"{card.Name} (debuffed)", EffectOp.AddFlavor, 0, flavor, mult));
                return 0;
            }

            if (card.Quality != QualityTier.Bootleg)
            {
                flavor += card.Flavor;
                steps.Add(new ScoreStep(card.Name, EffectOp.AddFlavor, card.Flavor, flavor, mult));
            }
            else
            {
                steps.Add(new ScoreStep($"{card.Name} (Bootleg)", EffectOp.AddFlavor, 0, flavor, mult));
            }

            switch (card.Quality)
            {
                case QualityTier.TopShelf:
                    flavor += 30;
                    steps.Add(new ScoreStep($"{card.Name} (Top Shelf)", EffectOp.AddFlavor, 30, flavor, mult));
                    break;
                case QualityTier.BarrelAged:
                    mult += 8;
                    steps.Add(new ScoreStep($"{card.Name} (Barrel-Aged)", EffectOp.AddMult, 8, flavor, mult));
                    break;
                case QualityTier.Signature:
                    mult *= 1.5;
                    steps.Add(new ScoreStep($"{card.Name} (Signature)", EffectOp.MultMult, 1.5, flavor, mult));
                    break;
            }

            switch (card.Enhancement)
            {
                case Enhancement.Infused:
                    flavor += 40;
                    steps.Add(new ScoreStep($"{card.Name} (Infused)", EffectOp.AddFlavor, 40, flavor, mult));
                    break;
                case Enhancement.Overproof:
                    mult += 4;
                    steps.Add(new ScoreStep($"{card.Name} (Overproof)", EffectOp.AddMult, 4, flavor, mult));
                    break;
                case Enhancement.Frozen:
                    mult *= 2;
                    steps.Add(new ScoreStep($"{card.Name} (Frozen)", EffectOp.MultMult, 2, flavor, mult));
                    break;
                // Premium is a matcher concern; the Frozen shatter roll and the Doubled
                // copy happen in the round layer (they mutate the deck); Golden pays out
                // in the run layer at customer end.
            }

            int retriggers = 0;
            foreach (var patron in patrons)
            {
                foreach (var effect in patron.Definition.Effects)
                {
                    if (effect.Trigger != EffectTrigger.OnCardScored) continue;
                    if (!effect.Condition.Evaluate(context, card, index)) continue;

                    if (effect.Op == EffectOp.Retrigger)
                    {
                        if (!allowRetriggerOps) continue;
                        int times = (int)patron.ResolveValue(effect);
                        retriggers += times;
                        steps.Add(new ScoreStep($"{patron.Definition.Name}: retrigger {card.Name}",
                            EffectOp.Retrigger, times, flavor, mult));
                    }
                    else
                    {
                        ApplyNumericOp(patron, effect, steps, ref flavor, ref mult);
                    }
                }
            }
            return retriggers;
        }

        private static void ApplyHandEffects(IReadOnlyList<PatronInstance> patrons,
            EffectContext context, List<ScoreStep> steps, ref double flavor, ref double mult)
        {
            foreach (var patron in patrons)
            {
                foreach (var effect in patron.Definition.Effects)
                {
                    if (effect.Trigger != EffectTrigger.OnHandScored) continue;
                    if (!effect.Condition.Evaluate(context)) continue;

                    if (effect.Op == EffectOp.Accumulate)
                    {
                        patron.Accumulate(effect.Value);
                        steps.Add(new ScoreStep($"{patron.Definition.Name}: builds up ({patron.Accumulated:0.#})",
                            EffectOp.Accumulate, effect.Value, flavor, mult));
                    }
                    else
                    {
                        ApplyNumericOp(patron, effect, steps, ref flavor, ref mult);
                    }
                }
            }
        }

        private static void ApplyNumericOp(PatronInstance patron, PatronEffect effect,
            List<ScoreStep> steps, ref double flavor, ref double mult)
        {
            double value = patron.ResolveValue(effect);
            // Skip no-ops (e.g. a scaling patron that hasn't grown yet) to keep the replayable
            // breakdown free of "+0" steps.
            if (value == 0 && effect.Op != EffectOp.MultMult) return;
            if (value == 1 && effect.Op == EffectOp.MultMult) return;
            switch (effect.Op)
            {
                case EffectOp.AddFlavor:
                    flavor += value;
                    break;
                case EffectOp.AddMult:
                    mult += value;
                    break;
                case EffectOp.MultMult:
                    mult *= value;
                    break;
                default:
                    return; // money/card ops are resolved by the round/run layer, not scoring
            }
            steps.Add(new ScoreStep(patron.Definition.Name, effect.Op, value, flavor, mult));
        }
    }
}
