using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastCall.PlayTests.Trailer
{
    /// <summary>
    /// The bar's verbs, played the way the trailer wants them seen: who is on which stool, the card, the cellar, the
    /// pour, the shake, the glass, the garnish, the hand-over, the tap and the door. Every verb watches CORE to know
    /// when it is done - the lean that pours is found by rising until the tin starts filling, not by a coordinate -
    /// so a bench that is redrawn tomorrow is still filmed correctly.
    ///
    /// Distances are in the HUD's 1280x720 units (the reference every canvas is laid out against) and scaled to the
    /// film by <see cref="U"/>, so the same hand works at any Game view size.
    /// </summary>
    public sealed partial class TrailerShots
    {
        private Keyboard _keys;

        /// <summary>Screen pixels per HUD unit.</summary>
        private static float U => Screen.height / 720f;

        // ── who is on which stool ────────────────────────────────────────────────────────────────────────────

        /// <summary>The HUD gives each drinker the first free stool in its own fill order and does not say which;
        /// the stool that lights up in the frames after a drinker sits down is theirs.</summary>
        private readonly Dictionary<CustomerVisit, RectTransform> _stoolOf = new Dictionary<CustomerVisit, RectTransform>();
        private readonly HashSet<RectTransform> _litLastLook = new HashSet<RectTransform>();

        private void WatchStools()
        {
            var run = _boot.Tycoon;
            var lit = new List<RectTransform>();
            for (int i = 0; i < 12; i++)
            {
                var seat = Find("Seat" + i);
                if (Shown(seat)) lit.Add(seat);
            }
            // forget the ones who left
            var gone = new List<CustomerVisit>();
            foreach (var kv in _stoolOf)
                if (!run.Floor.Seated.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var v in gone) _stoolOf.Remove(v);
            var newStools = lit.FindAll(s => !_litLastLook.Contains(s) && !_stoolOf.ContainsValue(s));
            var newVisits = new List<CustomerVisit>();
            foreach (var v in run.Floor.Seated) if (!_stoolOf.ContainsKey(v)) newVisits.Add(v);
            if (newStools.Count == 1 && newVisits.Count == 1) _stoolOf[newVisits[0]] = newStools[0];
            _litLastLook.Clear();
            foreach (var s in lit) _litLastLook.Add(s);
        }

        /// <summary>Runs the bar's clock OFF CAMERA until somebody who passes <paramref name="who"/> has walked in and
        /// been given a stool; the camera can roll the moment this returns and catch them crossing the room.</summary>
        private IEnumerator Admit(System.Func<CustomerVisit, bool> who, float barSeconds, System.Action<CustomerVisit> found)
        {
            var run = _boot.Tycoon;
            CustomerVisit hit = null;
            for (float t = 0f; t < barSeconds && hit == null; t += 0.5f)
            {
                if (run.Talking || run.HostessVisit != null) yield return HearTheHostOut();
                if (run.Phase != TycoonPhase.DayOpen) break;
                run.Tick(0.5);
                yield return null;
                WatchStools();
                foreach (var kv in _stoolOf)
                    if (kv.Key.State == VisitState.Waiting && !kv.Key.IdInspected && who(kv.Key)) { hit = kv.Key; break; }
            }
            found(hit);
        }

        private IEnumerator WaitUntilSettled(RectTransform seat)
        {
            var last = seat.anchoredPosition;
            int still = 0;
            float until = Time.unscaledTime + 20f;
            while (still < 8 && Time.unscaledTime < until)
            {
                yield return null;
                WatchStools();
                if ((seat.anchoredPosition - last).sqrMagnitude < 0.01f) still++;
                else { still = 0; last = seat.anchoredPosition; }
            }
        }

        private static Vector2 BodyOf(RectTransform seat) => ScreenPointOf(seat) + new Vector2(0f, seat.rect.height * 0.27f * seat.lossyScale.y);

        // ── the card ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Waits for the drinker to decide, clicks them, and lets the card turn over. Returns with it up.</summary>
        private IEnumerator ReadTheCard(CustomerVisit visit, RectTransform seat, bool lingerOnTheOrder)
        {
            yield return WaitUntilSettled(seat);
            TrailerCamera.Mark("seated");
            yield return Until(() => visit.HasOrdered, 25f);
            yield return Hold(0.5f);
            yield return Glide(BodyOf(seat));
            yield return Hold(0.25f);
            yield return Click(seat, BodyOf(seat) - ScreenPointOf(seat));
            yield return Until(() => visit.IdInspected, 2f);
            TrailerCamera.Mark("card open");
            yield return Hold(1.1f);
            if (!lingerOnTheOrder) yield break;
            var card = Find("IdCard");
            var row = card != null ? Find("OrderHit", card) : null;
            if (row == null) yield break;
            yield return Glide(ScreenPointOf(row));
            TrailerCamera.Mark("recipe");
            yield return Hold(2.2f);
        }

        /// <summary>The card goes away the way a hand puts it away: a click on the room beside it.</summary>
        private IEnumerator PutTheCardDown()
        {
            if (!Shown(Find("IdCard"))) yield break;
            var beside = new Vector2(Screen.width * 0.09f, Screen.height * 0.5f);
            yield return Glide(beside);
            Press(_mouse.leftButton);
            yield return Hold(0.06f);
            Release(_mouse.leftButton);
            yield return Hold(0.5f);
            if (Shown(Find("IdCard"))) yield return Tap(_keys.escapeKey);
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            Press(key);
            yield return Hold(0.05f);
            Release(key);
            yield return Hold(0.3f);
        }

        // ── the cellar and the bench ─────────────────────────────────────────────────────────────────────────

        private static RectTransform Roller()
        {
            var root = Find("ShutterDoor");
            if (root == null) return null;
            var key = root.GetComponentInChildren<UnityEngine.UI.Button>(true);
            return key != null ? key.GetComponent<RectTransform>() : root;
        }

        private static Vector2 RollerFace(RectTransform door)
        {
            var c = new Vector3[4];
            door.GetWorldCorners(c);
            return RectTransformUtility.WorldToScreenPoint(null, (c[1] + c[2]) * 0.5f) + new Vector2(0f, -60f * U);
        }

        private static bool DoorAnswers(string door)
        {
            var d = Find(door);
            return Shown(d) && WhatIsUnder(ScreenPointOf(d)).Contains(door);
        }

        /// <summary>The roller up, until the named cellar door takes the pointer.</summary>
        private IEnumerator OpenTheCellar(string door)
        {
            for (int attempt = 0; attempt < 5 && !DoorAnswers(door); attempt++)
            {
                yield return HearTheHostOut();
                var roller = Roller();
                Assert.That(roller, Is.Not.Null, "there is no roller to lift");
                var face = RollerFace(roller);
                yield return Click(roller, face - ScreenPointOf(roller));
                yield return Hold(0.7f);
            }
            TrailerCamera.Mark("cellar open");
        }

        private IEnumerator CloseTheCellar()
        {
            var any = Find("ShutterDoor");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                bool open = false;
                foreach (var b in _boot.Tycoon.Shelf.Bottles)
                    if (DoorAnswers("CellarDoor_" + b.Id)) { open = true; break; }
                if (!open || any == null) yield break;
                var roller = Roller();
                yield return Click(roller, RollerFace(roller) - ScreenPointOf(roller));
                yield return Hold(0.7f);
            }
        }

        /// <summary>The shelf bottle that answers a recipe's band (the first one with drink left in it).</summary>
        private ShelfBottle BottleFor(RatioRequirement band)
        {
            foreach (var b in _boot.Tycoon.Shelf.Bottles)
                if (!b.IsEmpty && TycoonRun.CardAnswers(b.Ingredient, band)) return b;
            return null;
        }

        /// <summary>
        /// One bottle into the tin, to <paramref name="target"/> tin units. The bottle is taken by its label and lifted
        /// up and over; its lean comes from how high the hand has risen (PourHand), so the hand RISES until the tin
        /// starts to fill, climbs a few steps more for a stream worth seeing, holds, and lets go on the measure.
        /// </summary>
        private IEnumerator PourBottle(string id, double target)
        {
            var run = _boot.Tycoon;
            yield return OpenTheCellar("CellarDoor_" + id);
            yield return Click(Find("CellarDoor_" + id));
            bool bench = false;
            yield return Until(() => Shown(Find("ShakerPanel")), 3f, ok => bench = ok);
            Assert.That(bench, Is.True, "the bottle never came to the bench");
            yield return Hold(0.35f);
            yield return HearTheHostOut();

            var panel = Find("ShakerPanel");
            var surface = Find("PourSurface", panel) ?? panel;
            var bottle = Find("Bottle", panel);
            Assert.That(bottle, Is.Not.Null, "there is no bottle on the bench");
            var grip = ScreenPointOf(bottle) + new Vector2(0f, 80f * U);
            yield return Glide(grip);
            yield return Hold(0.12f);
            Press(_mouse.leftButton);
            yield return Hold(0.1f);

            // across to where the stream falls on the tin's home, then up until it pours
            var gripLocal = (Vector2)surface.InverseTransformPoint(grip);
            float x = 100f, y = gripLocal.y;
            yield return Glide(ScreenPointIn(surface, new Vector2(x, y + 40f)), 0.35f);
            y += 40f;
            double before = run.Glass.VolumeOf(id);
            while (y < 320f && run.Glass.VolumeOf(id) <= before + 1e-6)
            {
                y += 260f * Time.unscaledDeltaTime;
                Set(_mouse.position, ScreenPointIn(surface, new Vector2(x, y)));
                yield return null;
            }
            TrailerCamera.Mark("first drop");
            // THE POUR BUILDS (2026-10-02, the author: "giderek dökme şiddetini arttırmalı"): from the first drop the
            // hand climbs steadily through every flow step (1,1,2,3,5,8,13,21,34) to the full stream - a drip, a
            // thread, a rope, a gush - in 1.4 s, quick enough that the drinker's patience holds (the first filmed take
            // lost its guest to a slow hand)
            yield return Glide(ScreenPointIn(surface, new Vector2(x, 316f)), 1.4f);
            TrailerCamera.Mark("full stream");
            float until = Time.unscaledTime + 8f;
            while (run.Glass.VolumeOf(id) < target - 0.01 && !run.Glass.IsFull && Time.unscaledTime < until)
                yield return null;
            Release(_mouse.leftButton);
            yield return Hold(0.55f);      // it walks home on its own
        }

        /// <summary>A short pour from the bottle already on the bench, for the bottle montage: lifted, tipped until it
        /// runs, then up through the flow steps for <paramref name="seconds"/>, and let go.</summary>
        private IEnumerator PourBurst(string id, float seconds)
        {
            var run = _boot.Tycoon;
            var panel = Find("ShakerPanel");
            var surface = Find("PourSurface", panel) ?? panel;
            var bottle = Find("Bottle", panel);
            if (bottle == null) yield break;
            var grip = ScreenPointOf(bottle) + new Vector2(0f, 80f * U);
            yield return Glide(grip, 0.22f);
            Press(_mouse.leftButton);
            yield return Hold(0.06f);
            var gl = (Vector2)surface.InverseTransformPoint(grip);
            float x = 100f, y = gl.y + 40f;
            yield return Glide(ScreenPointIn(surface, new Vector2(x, y)), 0.25f);
            double before = run.Glass.VolumeOf(id);
            while (y < 320f && run.Glass.VolumeOf(id) <= before + 1e-6)
            {
                y += 300f * Time.unscaledDeltaTime;
                Set(_mouse.position, ScreenPointIn(surface, new Vector2(x, y)));
                yield return null;
            }
            TrailerCamera.Mark("pouring " + id);
            yield return Glide(ScreenPointIn(surface, new Vector2(x, 316f)), seconds);
            Release(_mouse.leftButton);
            yield return Hold(0.45f);
        }

        /// <summary>Back off the bench to the cellar (the drawer stays open behind it).</summary>
        private IEnumerator StepBack()
        {
            var back = Find("EdgeBack");
            if (Shown(back)) yield return ClickFace(back);
            else yield return Tap(_keys.escapeKey);
            yield return Hold(0.4f);
        }

        /// <summary>The lid onto the tin, by hand. A pourable tin then walks itself to the glass.</summary>
        private IEnumerator CapTheTin()
        {
            var panel = Find("ShakerPanel");
            var cap = Find("ShakerCap", panel);
            var tin = Find("Shaker", panel);
            Assert.That(cap != null && tin != null, Is.True, "the bench has no lid or no tin");
            yield return Drag(ScreenPointOf(cap), new[] { ScreenPointOf(tin) }, 0.45f, 0.18f);
            TrailerCamera.Mark("capped");
            yield return Hold(0.4f);
        }

        /// <summary>A hard shake: the capped tin held and worked side to side until Core calls it shaken.</summary>
        private IEnumerator Shake()
        {
            var run = _boot.Tycoon;
            var tin = Find("Shaker", Find("ShakerPanel"));
            var at = FaceOf(tin);
            yield return Glide(at);
            yield return Hold(0.1f);
            Press(_mouse.leftButton);
            TrailerCamera.Mark("shake");
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 2.4f)
            {
                float t = Time.unscaledTime - t0;
                Set(_mouse.position, at + new Vector2(Mathf.Sin(t * 2f * Mathf.PI * 4f) * 150f * U,
                                                      Mathf.Sin(t * 2f * Mathf.PI * 8f) * 18f * U));
                yield return null;
            }
            Set(_mouse.position, at);
            yield return Hold(0.1f);
            Release(_mouse.leftButton);
            yield return Hold(0.6f);
            if (!run.IsShaken) Debug.LogWarning("[trailer] the shake did not take");
        }

        /// <summary>A stir: the bar spoon taken and circled round the open tin until Core calls it stirred (five laps
        /// is a full stir; four and a half on film).</summary>
        private IEnumerator Stir()
        {
            var run = _boot.Tycoon;
            var panel = Find("ShakerPanel");
            var spoon = Find("BarSpoon", panel);
            var tin = Find("Shaker", panel);
            if (!Shown(spoon) || tin == null) { Debug.LogWarning("[trailer] no spoon on the bench to stir with"); yield break; }
            var grip = FaceOf(spoon);
            yield return Glide(grip);
            yield return Hold(0.1f);
            Press(_mouse.leftButton);
            yield return Hold(0.1f);
            var centre = FaceOf(tin);
            float r = 90f * U;
            yield return Glide(centre + new Vector2(r, 0f), 0.4f);
            TrailerCamera.Mark("stir");
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 3.4f)
            {
                float a = (Time.unscaledTime - t0) / 0.7f * 2f * Mathf.PI;     // a lap every 0.7 s
                Set(_mouse.position, FaceOf(tin) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                yield return null;
            }
            Release(_mouse.leftButton);
            yield return Hold(0.6f);
            if (!run.IsStirred) Debug.LogWarning("[trailer] the stir did not take");
        }

        /// <summary>The tin over the glass: lifted by the neck and raised until the glass catches the stream.</summary>
        private IEnumerator PourIntoTheGlass()
        {
            var run = _boot.Tycoon;
            bool there = false;
            yield return Until(() => Shown(Find("ServePanel")), 5f, ok => there = ok);
            Assert.That(there, Is.True, "the tin never came to the glass");
            yield return Hold(0.8f);
            yield return HearTheHostOut();
            var surface = Find("ServeSurface");
            var tin = Find("Shaker", surface);
            yield return Until(() => WhatIsUnder(ScreenPointOf(tin)).StartsWith("[Shaker]"), 3f);
            var press = ScreenPointOf(tin);
            yield return Glide(press);
            yield return Hold(0.12f);
            Press(_mouse.leftButton);
            yield return Hold(0.1f);
            var local = (Vector2)surface.InverseTransformPoint(press);
            float x = -200f, y = local.y + 30f;
            yield return Glide(ScreenPointIn(surface, new Vector2(x, y)), 0.4f);
            while (y < 320f && run.ServingGlass.IsEmpty && !run.Glass.IsEmpty)
            {
                y += 240f * Time.unscaledDeltaTime;
                Set(_mouse.position, ScreenPointIn(surface, new Vector2(x, y)));
                yield return null;
            }
            TrailerCamera.Mark("pour out");
            yield return Glide(ScreenPointIn(surface, new Vector2(x, 316f)), 1.1f);
            yield return Until(() => run.Glass.IsEmpty || run.ServingGlass.IsFull, 8f);
            Release(_mouse.leftButton);
            yield return Hold(0.7f);
            var done = Find("Done", Find("ServePanel"));
            if (Shown(done)) yield return ClickFace(done);
            yield return Hold(0.6f);
        }

        /// <summary>The order's garnishes at the counter rail: dishes dragged, ice held over, rims circled.</summary>
        private IEnumerator Garnish(DrinkOrder order)
        {
            var run = _boot.Tycoon;
            var glass = Find("DrinkGlass");
            if (!Shown(glass) || order.Garnishes == null) yield break;
            foreach (var prep in order.Garnishes)
            {
                var dish = Find("MP_" + prep.Id);
                if (!Shown(dish)) continue;
                yield return HearTheHostOut();
                var mouth = FaceOf(glass) + new Vector2(0f, glass.rect.height * 0.34f * glass.lossyScale.y);
                yield return Glide(FaceOf(dish));
                yield return Hold(0.1f);
                Press(_mouse.leftButton);
                yield return Hold(0.08f);
                TrailerCamera.Mark("garnish " + prep.Id);
                if (prep.Id == "ice")
                {
                    yield return Glide(FaceOf(glass) + new Vector2(0f, 30f * U), 0.4f);
                    yield return Until(() => run.ServingGlass.IceCubes >= 3, 2.2f);
                }
                else if (prep.Id.EndsWith("_rim"))
                {
                    float r = 62f * U;
                    yield return Glide(mouth + new Vector2(r, 0f), 0.4f);
                    float t0 = Time.unscaledTime;
                    while (Time.unscaledTime - t0 < 1.3f && !run.ServingGlass.HasPreparation(prep.Id))
                    {
                        float a = (Time.unscaledTime - t0) / 1.1f * 2f * Mathf.PI;
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
                yield return Hold(0.5f);
            }
        }

        /// <summary>The finished glass carried to the drinker. Back once they have paid for it.</summary>
        private IEnumerator HandItOver(CustomerVisit visit, RectTransform seat)
        {
            yield return PutTheCardDown();
            var glass = Find("DrinkGlass");
            Assert.That(Shown(glass), Is.True, "there is no drink on the counter to hand over");
            yield return Drag(FaceOf(glass), new[] { BodyOf(seat) }, 0.5f, 0.15f);
            yield return Until(() => visit.Paid > 0, 2f);
            TrailerCamera.Mark("served");
            yield return Hold(2.6f);       // the face is the review
        }

        /// <summary>A pint from the tap: leaned at 45 degrees to fill, straightened for the head, served.</summary>
        private IEnumerator PullAPint(CustomerVisit visit, RectTransform seat)
        {
            var run = _boot.Tycoon;
            yield return PutTheCardDown();
            yield return CloseTheCellar();
            var tap = Find("PropDoor_taps_one");
            Assert.That(Shown(tap), Is.True, "the bar has no tap to pull");
            yield return ClickFace(tap);
            bool open = false;
            yield return Until(() => Shown(Find("TapPanel")), 3f, ok => open = ok);
            Assert.That(open, Is.True, "the tap never opened");
            yield return Hold(0.8f);
            yield return HearTheHostOut();
            var pint = Find("Pint", Find("TapPanel"));
            var press = ScreenPointOf(pint);
            var pivot = press + new Vector2(0f, 141f * U);
            float r = 150f * U;
            System.Func<float, Vector2> lean = deg =>
                pivot + r * new Vector2(-Mathf.Sin(deg * Mathf.Deg2Rad), -Mathf.Cos(deg * Mathf.Deg2Rad));
            yield return Glide(press);
            yield return Hold(0.12f);
            Press(_mouse.leftButton);
            yield return Hold(0.1f);
            TrailerCamera.Mark("pull");
            yield return Glide(lean(45f), 0.45f);
            yield return Until(() => run.ServingGlass.FillFraction >= 0.70, 3.5f);
            yield return Glide(lean(10f), 0.3f);
            yield return Hold(0.45f);
            Release(_mouse.leftButton);
            TrailerCamera.Mark("head");
            yield return Hold(0.8f);
            var done = Find("Done", Find("TapPanel"));
            if (Shown(done)) yield return ClickFace(done);
            yield return Hold(0.5f);
            yield return CloseTheCellar();
            yield return HandItOver(visit, seat);
        }

        // ── one whole drink ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>Whatever is on the card, made and handed over: built, shaken or pulled.</summary>
        private IEnumerator MakeTheOrder(CustomerVisit visit, RectTransform seat)
        {
            var order = visit.Order;
            var recipe = order.Wanted;
            if (recipe.Id == "draught" || recipe.GlassId == "pint")
            {
                yield return PullAPint(visit, seat);
                yield break;
            }
            yield return PutTheCardDown();
            var shares = RatioRecipeMatcher.PerfectPour(recipe);
            var bands = recipe.RatioRequirements;
            const double fill = 0.86;      // one tin is one portion; the order wants the glass 0.80 full
            bool first = true;
            for (int i = 0; i < bands.Count; i++)
            {
                var bottle = BottleFor(bands[i]);
                if (bottle == null) { Debug.LogWarning("[trailer] no bottle answers " + recipe.Id + " band " + i); continue; }
                if (!first) yield return StepBack();
                first = false;
                // VolumeOf is per bottle, so each pour stops on its own share of the tin
                yield return PourBottle(bottle.Id, _boot.Tycoon.Glass.VolumeOf(bottle.Id) + shares[i] * fill);
            }
            if (recipe.Prep == PrepMethod.Stirred) yield return Stir();
            yield return CapTheTin();
            if (recipe.Prep == PrepMethod.Shaken) yield return Shake();
            yield return PourIntoTheGlass();
            yield return Garnish(order);
            yield return HandItOver(visit, seat);
        }
    }
}
