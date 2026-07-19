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
    ///   4. The resonance block (GDD 19 §6): how well the drink read the person.
    ///   5. FinalScore = Flavor × Mult.
    /// A mix that matched no recipe scores 0 and fires no patron effects (its emotional
    /// charges still pour, at half strength — that is the round layer's job, not scoring's).
    ///
    /// Resonance is applied last on purpose: the Clean Serve burst is the closing flourish
    /// of the score card, and it should not be compounded by patron MultMult effects.
    /// </summary>
    public static class ScoringEngine
    {
        /// <summary>Mult can be penalised but never driven below this (GDD 19 §6).</summary>
        public const double MinMult = 1.0;

        public static ScoreBreakdown Score(RecipeMatch match, int recipeLevel = 1) =>
            Score(match, recipeLevel, null, EffectContext.Empty);

        public static ScoreBreakdown Score(RecipeMatch match, int recipeLevel,
            IReadOnlyList<PatronInstance> patrons, EffectContext context) =>
            Score(match, recipeLevel, patrons, context, null);

        public static ScoreBreakdown Score(RecipeMatch match, int recipeLevel,
            IReadOnlyList<PatronInstance> patrons, EffectContext context,
            ResonanceResult resonance)
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
                double weight = match.WeightAt(index);
                int retriggers = ScoreCardOnce(card, index, weight, patrons, context, steps,
                    allowRetriggerOps: true, ref flavor, ref mult);
                for (int r = 0; r < retriggers; r++)
                {
                    ScoreCardOnce(card, index, weight, patrons, context, steps,
                        allowRetriggerOps: false, ref flavor, ref mult);
                }
            }

            ApplyHandEffects(patrons, context, steps, ref flavor, ref mult);
            ApplyResonance(resonance, steps, ref flavor, ref mult);

            return new ScoreBreakdown(recipe, recipeLevel, match.ScoredCards, steps, flavor, mult, resonance);
        }

        /// <summary>
        /// The emotional half of the score (GDD 19 §6): additive Mult for how much closer the
        /// customer got to what they asked for, a flat bonus for reading a stat you could not
        /// see, a burst for landing it exactly — and a penalty for pushing them too far.
        /// </summary>
        private static void ApplyResonance(ResonanceResult resonance, List<ScoreStep> steps,
            ref double flavor, ref double mult)
        {
            if (resonance == null) return;

            if (resonance.ResonanceMult != 0)
            {
                mult += resonance.ResonanceMult;
                steps.Add(new ScoreStep("Resonance", EffectOp.AddMult, resonance.ResonanceMult, flavor, mult));
            }

            if (resonance.LuckyReadMult != 0)
            {
                mult += resonance.LuckyReadMult;
                steps.Add(new ScoreStep("Lucky read", EffectOp.AddMult, resonance.LuckyReadMult, flavor, mult));
            }

            if (resonance.BustPenalty != 0)
            {
                mult = System.Math.Max(MinMult, mult - resonance.BustPenalty);
                steps.Add(new ScoreStep($"Bust ({resonance.Bust})", EffectOp.AddMult,
                    -resonance.BustPenalty, flavor, mult));
            }

            if (resonance.ServeBurst != 1)
            {
                mult *= resonance.ServeBurst;
                steps.Add(new ScoreStep(resonance.BlindRead ? "Clean Serve (blind)" : "Clean Serve",
                    EffectOp.MultMult, resonance.ServeBurst, flavor, mult));
            }
        }

        /// <summary>
        /// A Mult effect needs at least this much of the glass to count (GDD 21 §9).
        ///
        /// Flavor scales with volume, but Mult cannot — "×1.5" has no sensible fractional
        /// form. Left unguarded that hands the player a free combo: pour a drop of every
        /// Barrel-Aged and Signature bottle on the shelf and collect every multiplier for
        /// almost no volume. An ingredient has to actually be *in* the drink.
        /// </summary>
        public const double MinShareForMultEffects = 0.10;

        /// <summary>
        /// Scores one pass of a card; returns how many retriggers were requested.
        /// <paramref name="weight"/> is the ingredient's share of the glass under the pour
        /// system, or 1 for a card-era mix.
        /// </summary>
        private static int ScoreCardOnce(IngredientCard card, int index, double weight,
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

            // Flavor is a quantity — how much good stuff is in the glass — so it scales with
            // the pour. Mult is a property of the finished drink: the barrel-aged whisky is
            // either in it or it is not, gated by MinShareForMultEffects.
            bool countsForMult = weight >= MinShareForMultEffects;

            if (card.Quality != QualityTier.Bootleg)
            {
                double contribution = card.Flavor * weight;
                flavor += contribution;
                steps.Add(new ScoreStep(card.Name, EffectOp.AddFlavor, contribution, flavor, mult));
            }
            else
            {
                steps.Add(new ScoreStep($"{card.Name} (Bootleg)", EffectOp.AddFlavor, 0, flavor, mult));
            }

            switch (card.Quality)
            {
                case QualityTier.TopShelf:
                    flavor += 30 * weight;
                    steps.Add(new ScoreStep($"{card.Name} (Top Shelf)", EffectOp.AddFlavor, 30 * weight, flavor, mult));
                    break;
                case QualityTier.BarrelAged:
                    if (!countsForMult) break;
                    mult += 8;
                    steps.Add(new ScoreStep($"{card.Name} (Barrel-Aged)", EffectOp.AddMult, 8, flavor, mult));
                    break;
                case QualityTier.Signature:
                    if (!countsForMult) break;
                    mult *= 1.5;
                    steps.Add(new ScoreStep($"{card.Name} (Signature)", EffectOp.MultMult, 1.5, flavor, mult));
                    break;
            }

            switch (card.Enhancement)
            {
                case Enhancement.Infused:
                    flavor += 40 * weight;
                    steps.Add(new ScoreStep($"{card.Name} (Infused)", EffectOp.AddFlavor, 40 * weight, flavor, mult));
                    break;
                case Enhancement.Overproof:
                    if (!countsForMult) break;
                    mult += 4;
                    steps.Add(new ScoreStep($"{card.Name} (Overproof)", EffectOp.AddMult, 4, flavor, mult));
                    break;
                case Enhancement.Frozen:
                    if (!countsForMult) break;
                    mult *= 2;
                    steps.Add(new ScoreStep($"{card.Name} (Frozen)", EffectOp.MultMult, 2, flavor, mult));
                    break;
                // Premium is a matcher concern; Golden pays out in the run layer at customer
                // end. Both, along with Doubled and Frozen's shatter roll, are casualties of
                // the pour pivot — see Docs/PLAN_pour_pivot.md.
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
