using System;
using System.Collections;
using LastCall.Core;
using UnityEngine;

namespace LastCall.PlayTests.Trailer
{
    /// <summary>
    /// A NIGHT WORKED OFF CAMERA (2026-10-02, the author: "dolu ve pozitif bir fatura ekranı"). The slip only reads well
    /// when the night behind it was busy and good, and a night is twenty minutes of play - so before the close is
    /// filmed, the bar is worked through Core with the sim bot's own hands at their best (TycoonSimulator's
    /// BuildOrderedDrink at zero jitter): every card read, every liar shown the door, every drink poured to its perfect
    /// shares, mixed the way its page says, served, and the counter kept clear. Nothing here is drawn; the camera only
    /// rolls once the slip is up.
    /// </summary>
    public sealed partial class TrailerShots
    {
        private IEnumerator WorkTheNightOffCamera(double barSeconds, bool untilClosing = false)
        {
            var run = _boot.Tycoon;
            double build = 0;
            for (double t = 0; t < barSeconds && run.Phase == TycoonPhase.DayOpen; t += 1.0)
            {
                if (run.Talking || run.HostessVisit != null) yield return HearTheHostOut();
                if (run.Phase != TycoonPhase.DayOpen) break;
                if (untilClosing && run.Floor.IsClosingTime) break;
                run.Tick(1.0);
                if ((int)t % 4 == 0) yield return null;
                if (run.Phase != TycoonPhase.DayOpen) break;

                var messes = run.Floor.Messes;
                for (int m = messes.Count - 1; m >= 0; m--)
                {
                    if (messes[m].HasGlass) run.CollectGlass(messes[m]);
                    if (messes[m].Smudged) run.Wipe(messes[m]);
                }
                if (run.GlassesInHand > 0 && !run.SinkBusy) run.WashGlasses();

                foreach (var visit in run.Floor.Seated)
                {
                    if (visit.State != VisitState.Waiting || !visit.HasOrdered || visit.IdInspected) continue;
                    visit.InspectId();
                    if (run.Has(Feature.Door) && visit.Papers.ShouldBeKicked && visit.Paid == 0) run.Kick(visit);
                }

                build += 1.0;
                if (build < 6.0) continue;                         // a good hand, not an instant one
                foreach (var visit in run.Floor.Seated)
                {
                    if (visit.State != VisitState.Waiting || !visit.HasOrdered || !visit.IdInspected) continue;
                    if (!run.CanMake(visit.Order)) { run.DeclineOrder(visit); continue; }
                    if (!BuildPerfect(run, visit)) continue;
                    run.ServeTo(visit);
                    build = 0;
                    break;
                }
            }
            // the floor's last glasses, so the door can shut
            foreach (var mess in run.Floor.Messes)
            {
                if (mess.HasGlass) run.CollectGlass(mess);
                if (mess.Smudged) run.Wipe(mess);
            }
            if (run.GlassesInHand > 0 && !run.SinkBusy) run.WashGlasses();
            yield return null;
        }

        /// <summary>The sim bot's build with no slips in its hands: the drink exactly as the page wants it.</summary>
        private static bool BuildPerfect(TycoonRun run, CustomerVisit visit)
        {
            run.DiscardGlass();
            var recipe = visit.Order.Wanted;
            foreach (var band in recipe.RatioRequirements)
                if (!band.IsStyleBand && band.Type == IngredientType.Beer)
                {
                    ShelfBottle keg = null;
                    foreach (var b in run.Shelf.Bottles)
                        if (!b.IsEmpty && b.Ingredient.Type == IngredientType.Beer) { keg = b; break; }
                    if (keg == null) return false;
                    run.BeginPull(keg.Id);
                    for (int i = 0; i < 40 && run.ServingGlass.FillFraction < 0.78 && run.PullingId != null; i++)
                        run.PourTilted(0.05, TapPour.IdealTilt);
                    for (int i = 0; i < 20 && run.ServingGlass.FillFraction < 0.97 && run.PullingId != null; i++)
                        run.PourTilted(0.05, 6.0);
                    run.EndPull();
                    return !run.ServingGlass.IsEmpty;
                }

            double glassCap = run.Config.GlassCapacity;
            if (run.Glassware != null)
                foreach (var gw in run.Glassware)
                    if (gw.Id == recipe.GlassId) { glassCap = gw.Capacity; break; }
            double volume = Math.Max(0.05, Math.Min(1.0, Math.Max(recipe.MinFill, 0.85))) * Math.Min(glassCap, run.Glass.Capacity);
            var shares = RatioRecipeMatcher.PerfectPour(recipe);
            for (int i = 0; i < recipe.RatioRequirements.Count; i++)
            {
                ShelfBottle bottle = null;
                foreach (var b in run.Shelf.Bottles)
                    if (!b.IsEmpty && TycoonRun.CardAnswers(b.Ingredient, recipe.RatioRequirements[i])) { bottle = b; break; }
                if (bottle == null) return false;
                run.PourMeasure(bottle.Id, Math.Min(volume * shares[i], bottle.Remaining));
            }
            if (run.Glass.IsEmpty) return false;
            foreach (var garnish in visit.Order.Spec.Garnishes)
                if (!run.Glass.IsFull) run.AddPreparation(garnish);
            if (recipe.Prep == PrepMethod.Shaken) run.Shake(1.0);
            else if (recipe.Prep == PrepMethod.Stirred) { if (run.SpoonUnlocked) run.Stir(1.0); else run.Shake(1.0); }
            else if (run.MixRequired) { if (run.SpoonUnlocked) run.Stir(1.0); else run.Shake(1.0); }
            if (!run.Glass.IsEmpty) run.PourIntoServingGlass(run.Glass.TotalVolume, 1.0);
            return run.DrinkReady;
        }
    }
}
