using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LastCall.PlayTests.Trailer
{
    /// <summary>
    /// The v4 shots and the per-frame helpers they ride on (2026-10-02, the author's revision: a stool-by-stool run of
    /// reactions with the balloons gone, a finished rocks glass with a salt rim and a lemon, the menu book leafed
    /// through, the light following Roxy while she talks, the ceiling lamps kept to their first two rungs).
    /// </summary>
    public sealed partial class TrailerShots
    {
        private static string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        /// <summary>Where Roxy stands, written as a mark every few frames so the edit can light her and follow her:
        /// "focus roxy x y", fractions of the frame from its top left.</summary>
        private static Action TrackRoxy(int every = 6)
        {
            int n = 0;
            return () =>
            {
                if (n++ % every != 0) return;
                var go = GameObject.Find("Hostess");
                var cam = Camera.main;
                var sr = go != null ? go.GetComponent<SpriteRenderer>() : null;
                if (sr == null || cam == null || !sr.enabled) return;
                var p = cam.WorldToScreenPoint(sr.bounds.center);
                TrailerCamera.Mark("focus roxy " + F(p.x / Screen.width) + " " + F(1f - p.y / Screen.height));
            };
        }

        /// <summary>A seated person's place on the frame, as a focus mark (the body, a quarter of the stool up).</summary>
        private static void MarkFocus(string who, RectTransform seat)
        {
            var p = BodyOf(seat) + new Vector2(0f, seat.rect.height * 0.25f * seat.lossyScale.y);
            TrailerCamera.Mark("focus " + who + " " + F(p.x / Screen.width) + " " + F(1f - p.y / Screen.height));
        }

        /// <summary>Folds the drinkers' speech balloons and order tickets away every frame (the author: "sadece konuşma
        /// balonları gözükmesin") - the reactions, their shouts and their effects stay.</summary>
        private static Action FoldBalloons()
        {
            var parts = new List<RectTransform>();
            for (int i = 0; i < 12; i++)
            {
                var seat = Find("Seat" + i);
                if (seat == null) continue;
                foreach (var name in new[] { "Say", "Tag" })
                {
                    var rt = Find(name, seat);
                    if (rt != null) parts.Add(rt);
                }
            }
            return () =>
            {
                foreach (var rt in parts)
                    if (rt != null) rt.localScale = Vector3.zero;
            };
        }

        /// <summary>Keeps the ceiling lamps on their first two rungs (the author: "sadece 1 ve 2. seviye tavan
        /// aydınlatması"): from 1.5 stars the bell shades are worn whatever else the preset bought.</summary>
        private void KeepTheLampsLow()
        {
            var run = _boot.Tycoon;
            try
            {
                var worn = run.WornRung("counter_lamps");
                if (worn != null && worn.Level > 2) run.DevWear("counter_lamps_bell");
            }
            catch (Exception e) { Debug.LogWarning("[trailer] lamps: " + e.Message); }
        }

        // ── a drink made to a chosen standard, through Core, for the reactions ──────────────────────────────

        private enum Make { Perfect, Good, Nice, Meh, Wrong }

        /// <summary>The sim bot's build (BuildPerfect) with its hand deliberately off by a known amount, so the reaction
        /// run walks the whole scale from a delighted face to a turned-up nose.</summary>
        private static bool BuildAs(TycoonRun run, CustomerVisit visit, Make how)
        {
            if (how == Make.Perfect) return BuildPerfect(run, visit);
            run.DiscardGlass();
            var recipe = visit.Order.Wanted;
            if (how == Make.Wrong)
            {
                // something else entirely: the first spirit the recipe does NOT ask for
                foreach (var b in run.Shelf.Bottles)
                {
                    if (b.IsEmpty || b.Ingredient.Type != IngredientType.Spirit) continue;
                    bool asked = recipe.RatioRequirements.Any(band => TycoonRun.CardAnswers(b.Ingredient, band));
                    if (asked) continue;
                    run.PourMeasure(b.Id, 0.6);
                    if (run.MixRequired) run.Shake(1.0);
                    if (!run.Glass.IsEmpty) run.PourIntoServingGlass(run.Glass.TotalVolume, 1.0);
                    return run.DrinkReady;
                }
                return false;
            }
            foreach (var band in recipe.RatioRequirements)
                if (!band.IsStyleBand && band.Type == IngredientType.Beer) return BuildPerfect(run, visit);
            double skew = how == Make.Good ? 1.12 : how == Make.Nice ? 1.25 : 1.9;
            var shares = RatioRecipeMatcher.PerfectPour(recipe);
            for (int i = 0; i < recipe.RatioRequirements.Count; i++)
            {
                var bottle = run.Shelf.Bottles.FirstOrDefault(b => !b.IsEmpty && TycoonRun.CardAnswers(b.Ingredient, recipe.RatioRequirements[i]));
                if (bottle == null) return false;
                run.PourMeasure(bottle.Id, Math.Min(0.8 * shares[i] * (i == 0 ? skew : 1.0), bottle.Remaining));
            }
            if (how != Make.Meh)
                foreach (var g in visit.Order.Spec.Garnishes)
                    if (!run.Glass.IsFull) run.AddPreparation(g);
            if (recipe.Prep == PrepMethod.Shaken) run.Shake(1.0);
            else if (recipe.Prep == PrepMethod.Stirred) { if (run.SpoonUnlocked) run.Stir(1.0); else run.Shake(1.0); }
            else if (run.MixRequired) { if (run.SpoonUnlocked) run.Stir(1.0); else run.Shake(1.0); }
            if (!run.Glass.IsEmpty) run.PourIntoServingGlass(run.Glass.TotalVolume, how == Make.Meh ? 0.7 : 1.0);
            return run.DrinkReady;
        }

        // ── S09: every stool taken, every face answering in turn ─────────────────────────────────────────────

        /// <summary>
        /// The stools fill, and one after another each drinker gets a glass and answers it: good, nice, perfect, meh,
        /// no (the author's order). The glasses are made through Core to a chosen standard and handed over by the hand,
        /// which is the only road the room's reactions come down (TycoonHud.ServeSeat). Each hand-over marks "react k"
        /// with the drinker's place, so the edit can cut close on every face with a whip between them. Balloons and
        /// tickets are folded away for the whole take.
        /// </summary>
        [UnityTest, Timeout(1800000)]
        public IEnumerator S09_reactions()
        {
            yield return SetTheBar(2.0);
            var run = _boot.Tycoon;
            int want = Mathf.Min(run.Seats, 5);
            Func<int> ready = () => _stoolOf.Keys.Count(v => v.State == VisitState.Waiting && v.HasOrdered);
            for (int i = 0; i < 3000 && ready() < want; i++)
            {
                if (run.Talking || run.HostessVisit != null) yield return HearTheHostOut();
                if (run.Phase != TycoonPhase.DayOpen) break;
                run.Tick(0.5);
                yield return null;
                WatchStools();
                // the order is read the moment it is made, so nobody's patience is the shot's clock
                foreach (var v in run.Floor.Seated)
                    if (v.State == VisitState.Waiting && v.HasOrdered && !v.IdInspected) v.InspectId();
            }
            var line = _stoolOf.Where(kv => kv.Key.State == VisitState.Waiting && kv.Key.HasOrdered)
                               .OrderBy(kv => ScreenPointOf(kv.Value).x).Take(5).ToList();
            Assert.That(line.Count, Is.GreaterThanOrEqualTo(3), "the stools never filled for the reactions");
            var how = new[] { Make.Good, Make.Nice, Make.Perfect, Make.Meh, Make.Wrong };

            Set(_mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.12f));
            TrailerCamera.Roll("S09_reactions");
            TrailerCamera.EachFrame = FoldBalloons();
            TrailerCamera.Mark("full counter");
            yield return Hold(1.2f);
            for (int k = 0; k < line.Count; k++)
            {
                var visit = line[k].Key;
                var seat = line[k].Value;
                if (visit.State != VisitState.Waiting) continue;
                if (!BuildAs(run, visit, how[k % how.Length])) { Debug.LogWarning("[trailer] could not build for stool " + k); continue; }
                bool glass = false;
                yield return Until(() => Shown(Find("DrinkGlass")), 2f, ok => glass = ok);
                if (!glass) continue;
                yield return Drag(FaceOf(Find("DrinkGlass")), new[] { BodyOf(seat) }, 0.32f, 0.08f);
                TrailerCamera.Mark("react " + (k + 1));
                MarkFocus("react" + (k + 1), seat);
                yield return Until(() => visit.Paid > 0 || visit.State != VisitState.Waiting, 1.5f);
                yield return Hold(1.7f);                                   // the face, the shout, the sparks
            }
            yield return Hold(1f);
            TrailerCamera.Cut();
        }

        // ── S10: the craft, close - a rocks glass, a salt rim, a lemon ───────────────────────────────────────

        /// <summary>One spirit into the tin, capped, poured into the rocks glass, then at the rail: the glass's rim
        /// turned through the salt and a lemon twist dropped in. Nobody is waiting for it; it is the craft alone.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator S10_craft()
        {
            yield return SetTheBar(1.0);
            var run = _boot.Tycoon;
            var spirit = run.Shelf.Bottles.FirstOrDefault(b => !b.IsEmpty && b.Ingredient.Type == IngredientType.Spirit);
            Assert.That(spirit, Is.Not.Null, "there is no spirit on the shelf");
            Set(_mouse.position, new Vector2(Screen.width * 0.7f, Screen.height * 0.2f));
            TrailerCamera.Roll("S10_craft");
            yield return PourBottle(spirit.Id, 0.55);
            yield return CapTheTin();
            yield return PourIntoTheGlass();
            TrailerCamera.Mark("glass " + (run.ServingGlassware != null ? run.ServingGlassware.Id : "?"));
            yield return GarnishWith(new[] { "salt_rim", "lemon_twist" });
            TrailerCamera.Mark("finished");
            var glass = Find("DrinkGlass");
            if (Shown(glass)) MarkFocus("glass", glass);
            yield return Glide(new Vector2(Screen.width * 0.85f, Screen.height * 0.15f), 0.5f);
            yield return Hold(2.5f);
            TrailerCamera.Cut();
        }

        /// <summary>The rail's dishes on the glass on the counter, by id: dishes dragged in, ice held over, rims circled.</summary>
        private IEnumerator GarnishWith(IEnumerable<string> ids)
        {
            var run = _boot.Tycoon;
            foreach (var id in ids)
            {
                var glass = Find("DrinkGlass");
                var dish = Find("MP_" + id);
                if (!Shown(glass) || !Shown(dish)) { Debug.LogWarning("[trailer] no " + id + " on the rail"); continue; }
                var mouth = FaceOf(glass) + new Vector2(0f, glass.rect.height * 0.34f * glass.lossyScale.y);
                yield return Glide(FaceOf(dish));
                yield return Hold(0.1f);
                Press(_mouse.leftButton);
                yield return Hold(0.08f);
                TrailerCamera.Mark("garnish " + id);
                if (id == "ice")
                {
                    yield return Glide(FaceOf(glass) + new Vector2(0f, 30f * U), 0.4f);
                    yield return Until(() => run.ServingGlass.IceCubes >= 3, 2.2f);
                }
                else if (id.EndsWith("_rim"))
                {
                    float r = 62f * U;
                    yield return Glide(mouth + new Vector2(r, 0f), 0.4f);
                    float t0 = Time.unscaledTime;
                    while (Time.unscaledTime - t0 < 1.6f && !run.ServingGlass.HasPreparation(id))
                    {
                        float a = (Time.unscaledTime - t0) / 1.2f * 2f * Mathf.PI;
                        Set(_mouse.position, mouth + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                        yield return null;
                    }
                }
                else
                {
                    yield return Glide(FaceOf(glass), 0.45f);
                    yield return Hold(0.12f);
                }
                Release(_mouse.leftButton);
                yield return Hold(0.6f);
            }
        }

        // ── S11: the menu book, leafed through ───────────────────────────────────────────────────────────────

        [UnityTest, Timeout(600000)]
        public IEnumerator S11_book()
        {
            yield return SetTheBar(3.0);
            Set(_mouse.position, new Vector2(Screen.width * 0.9f, Screen.height * 0.1f));
            TrailerCamera.Roll("S11_book");
            yield return Hold(0.6f);
            yield return Tap(_keys.bKey);
            TrailerCamera.Mark("book open");
            yield return Hold(1.2f);
            for (int p = 1; p <= 7; p++)
            {
                yield return Tap(_keys.rightArrowKey);
                TrailerCamera.Mark("page " + p);
                yield return Hold(0.45f);
            }
            yield return Hold(0.6f);
            yield return Tap(_keys.bKey);
            TrailerCamera.Mark("book shut");
            yield return Hold(1f);
            TrailerCamera.Cut();
        }
    }
}
