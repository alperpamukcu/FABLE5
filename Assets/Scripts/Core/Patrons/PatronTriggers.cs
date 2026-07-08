using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// Resolves non-scoring patron triggers (money payouts now; card ops in M3).
    /// Kept separate from ScoringEngine so the round/run layer can fire
    /// OnCustomerEnd / OnNightEnd / OnShopEnter without faking a hand.
    /// </summary>
    public static class PatronTriggers
    {
        public static double ResolveMoney(EffectTrigger trigger,
            IReadOnlyList<PatronInstance> patrons, EffectContext context)
        {
            if (patrons == null) return 0;
            context = context ?? EffectContext.Empty;

            double total = 0;
            foreach (var patron in patrons)
            {
                foreach (var effect in patron.Definition.Effects)
                {
                    if (effect.Trigger != trigger) continue;
                    if (effect.Op != EffectOp.AddMoney) continue;
                    if (!effect.Condition.Evaluate(context)) continue;
                    total += patron.ResolveValue(effect);
                }
            }
            return total;
        }
    }
}
