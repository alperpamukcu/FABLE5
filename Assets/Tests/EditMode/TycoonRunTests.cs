using System;
using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using NUnit.Framework;

namespace LastCall.Tests
{
    /// <summary>
    /// The P2 gate (PLAN_tycoon_pivot): a full seeded day plays headless through
    /// <see cref="TycoonRun"/> — arrivals, pours, serves, the invoice, the strike logic —
    /// with nothing but Core.
    /// </summary>
    public class TycoonRunTests
    {
        private static RecipeDefinition Spritz() => new RecipeDefinition(
            "spritz", "Spritz", rank: 2, baseFlavor: 10, baseMult: 2,
            flavorPerLevel: 0, multPerLevel: 0,
            requirements: Array.Empty<PatternRequirement>(),
            ratioRequirements: new[]
            {
                new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
            },
            minFill: 0.5);

        private static readonly IReadOnlyList<RecipeDefinition> Book = new[] { Spritz() };

        private static Shelf NewShelf() => new Shelf(new[]
        {
            new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 20),
            new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 20),
        });

        // The economy math here is written against an instant serve (order the moment they
        // sit, gone the moment they are served), so these runs switch the decision beat and
        // the savour off. The pacing itself is covered by TycoonCoreTests.
        private static TycoonRun NewRun(string seed = "day-one", int startingMoney = 20) =>
            new TycoonRun(NewShelf(), Book, new RunRng(seed),
                config: new TycoonConfig(startingMoney, orderDecisionSeconds: 0, savorSeconds: 0));

        /// <summary>Tips the whole shaker into the serving glass, dead on the rim. Nothing is
        /// served straight out of the shaker any more (2026-07-28), so every test that hands a
        /// drink over pours it first — the same two verbs the player uses.</summary>
        private static void PourOut(TycoonRun run) =>
            run.PourIntoServingGlass(run.Glass.TotalVolume, accuracy: 1.0);

        /// <summary>Serves every seated customer an exact Spritz until the day closes.</summary>
        private static void PlayDayServingEveryone(TycoonRun run)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                Assert.Less(guard++, 600, "the day must terminate");
                run.Tick(5);
                foreach (var visit in run.Floor.Seated.ToList())
                {
                    if (visit.State != VisitState.Waiting) continue;
                    run.PourMeasure("gin", 0.35);
                    run.PourMeasure("soda", 0.35);
                    PourOut(run);
                    run.ServeTo(visit);
                }
            }
        }

        [Test]
        public void AFullDay_PlaysHeadless_AndPaysTheBills()
        {
            var run = NewRun();

            PlayDayServingEveryone(run);

            Assert.AreEqual(TycoonPhase.DayEnd, run.Phase);
            // v5 P12 / C4: no quota any more. How many come through the door is set by the
            // clock and by how fast the stools turn over, so the count is stated as a shape,
            // not a magic number -- this helper serves everyone the instant they order.
            int served = run.Floor.Finished.Count;
            Assert.Greater(served, 0, "an open night still sends people in");
            Assert.IsTrue(run.Floor.IsClosingTime, "and it ends at closing time");
            // v5 P11: 8 exact spritzes at the new $4 base, plus a tip that is a share of that
            // base rather than a flat $4. Stated as the day's own book so the shape is pinned
            // without re-deriving the tip formula here -- OrderSpecTests owns that.
            run.Floor.Finished[0].InspectId();   // the books are read off the card (C3)
            Assert.AreEqual(served * DrinkOrder.MenuPrice(run.Floor.Finished[0].Order.Wanted),
                run.DaySales, "every serve was exact, at the menu price");
            Assert.AreEqual(20 + run.DaySales + run.DayTips - run.Config.Rent(1), run.Money);

            var result = run.ContinueToNextDay();

            Assert.AreEqual(run.Ledger.History[0].Income, result.Income);
            Assert.AreEqual(run.Config.Rent(1), result.Expenses, "rent is the only expense today");
            Assert.AreEqual(0, run.Ledger.DebtStrikes, "a green day");
            Assert.AreEqual(2, run.Day);
            Assert.AreEqual(TycoonPhase.DayOpen, run.Phase);
        }

        [Test]
        public void AGoodDay_DrawsAWealthierCrowd_WhoPayMore()
        {
            // Rewritten for the zero-start standing (2026-08-02): one good night no
            // longer vaults a no-name bar to the rich crowd — the standing is inertial
            // and capped by the fittings and the menu tier.
            var run = NewRun();
            PlayDayServingEveryone(run);
            run.ContinueToNextDay();
            Assert.AreEqual(WealthTier.Regular, run.CrowdToday,
                "a good night draws a normal crowd — the rollers need fame, not one story");
            Assert.Greater(run.Rating.Average, 0.0, "and the standing moved a step");
            Assert.Less(run.Rating.Average, 1.0, "but only a step — the climb is the game");

            // A bar whose standing is already made draws the high rollers, who pay
            // the same premium as before. (4.9, not 4.2: a night of rank-2 pours is
            // CAPPED low and drags a made bar's standing down — by design.)
            var made = NewRun();
            made.Rating.DevSet(4.9);
            PlayDayServingEveryone(made);
            made.ContinueToNextDay();
            Assert.AreEqual(WealthTier.HighRoller, made.CrowdToday,
                "a made bar's crowd follows its standing");

            int guard = 0;
            while (made.Floor.Seated.Count == 0)
            {
                Assert.Less(guard++, 100);
                made.Tick(5);
            }

            // A rank-2 drink is $4 on the menu.
            made.Floor.Seated[0].InspectId();     // the price is read off the card (C3)
            Assert.AreEqual(5, made.Floor.Seated[0].Order.Price,
                "the $4 rank-2 drink sells for $5 to high rollers (×1.25)");
        }

        [Test]
        public void PremiumSpirits_RaiseTheMenuPrice()
        {
            // A tier-3 gin on the shelf lifts a spirit drink's price; a basic (tier-1) bar
            // adds nothing, so the sim bot's floor is untouched (2026-07-23).
            var premiumShelf = new Shelf(new[]
            {
                new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6,
                    info: new IngredientInfo("gin", tier: 3)), capacity: 20),
                new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 20),
            });
            var run = new TycoonRun(premiumShelf, Book, new RunRng("premium"),
                config: new TycoonConfig(20, orderDecisionSeconds: 0, savorSeconds: 0));

            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(5); }

            // base $6 (rank 2) + premium (tier 3 spirit → +2×$2) = $10 to a regular crowd.
            run.Floor.Seated[0].InspectId();
            Assert.AreEqual(DrinkOrder.MenuPrice(Spritz()) + (3 - 1) * 2,
                run.Floor.Seated[0].Order.Price);
        }

        [Test]
        public void ServingNobody_UntilTheTillRunsDry_ClosesTheBar()
        {
            // $20 starting money vs $20 day-1 rent: the till goes under on day 2 with no
            // income, and three underwater closes later the doors are shut.
            var run = NewRun("bankrupt");

            for (int day = 0; day < 4; day++)
            {
                int guard = 0;
                while (run.Phase == TycoonPhase.DayOpen)
                {
                    Assert.Less(guard++, 500, "storm-offs must clear the floor");
                    run.Tick(50);
                }
                run.ContinueToNextDay();
                if (run.Phase == TycoonPhase.Closed) break;
            }

            Assert.AreEqual(TycoonPhase.Closed, run.Phase);
            Assert.IsTrue(run.Ledger.IsBankrupt);
            Assert.AreEqual(3, run.Ledger.DebtStrikes);
        }

        [Test]
        public void AGlassOfNothing_PaysNothing_AndSoursTheRoom()
        {
            var run = NewRun();
            int guard = 0;
            while (run.Floor.Seated.Count == 0)
            {
                Assert.Less(guard++, 100);
                run.Tick(5);
            }
            var visit = run.Floor.Seated[0];

            run.PourMeasure("soda", 0.7);   // pure soda against a spritz order
            PourOut(run);
            var verdict = run.ServeTo(visit);

            // v5 P11 / C1: pure soda is no recipe at all, so there is nothing to pay for --
            // but a wrong drink that IS a drink now pays its own base price (see
            // TycoonCoreTests.AWrongDrink_PaysForWhatIsActuallyInTheGlass).
            Assert.AreEqual(OrderMatch.Wrong, verdict.Match);
            Assert.AreEqual(0, verdict.BasePaid, "a glass of soda is not a drink anyone sells");
            Assert.AreEqual(0, verdict.Tip);
            Assert.LessOrEqual(verdict.Satisfaction, 0.2);
        }

        [Test]
        public void BinningADrink_CostsALittle_AndAnEmptyBinIsFree()
        {
            // 2026-07-31, the author: a mistake must cost. The fee scales with what was
            // actually poured away, clamps to the till, and an empty discard — the UI's
            // routine reset — stays free, or every menu-close would be a fine.
            var run = NewRun(startingMoney: 50);
            int before = run.Money;

            Assert.AreEqual(0, run.DiscardGlass(), "nothing binned, nothing owed");
            Assert.AreEqual(before, run.Money);

            run.PourMeasure("gin", 0.5);
            run.PourMeasure("soda", 0.5);
            int fee = run.DiscardGlass();
            Assert.AreEqual((int)Math.Ceiling(1.0 * TycoonRun.BinFeePerVolume), fee,
                "a full shaker binned costs its volume's fee");
            Assert.AreEqual(before - fee, run.Money);
            Assert.IsTrue(run.Glass.IsEmpty, "and the drink is gone either way");
        }

        [Test]
        public void AnEmptyGlass_BlocksTheStool_UntilItIsBussed()
        {
            // D2, the bussing beat: a drinker leaves the glass on the stool, and the stool is
            // not sat on again until it is cleared — the click does it now, the bar's own
            // clock does it in BarDay.BusSeconds. A storm-off leaves nothing.
            var run = new TycoonRun(NewShelf(), Book, new RunRng("bussing"),
                config: new TycoonConfig(20, orderDecisionSeconds: 0, savorSeconds: 1));
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(5); }
            var visit = run.Floor.Seated[0];

            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            PourOut(run);
            run.ServeTo(visit);
            run.Tick(1.5);   // the savour ends; they get up and leave the glass

            Assert.AreEqual(1, run.Floor.Dirty.Count, "the empty glass stands on the stool");
            var glass = run.Floor.Dirty[0];
            Assert.IsFalse(glass.Cleared);

            glass.Bus();
            run.Tick(0.1);
            Assert.AreEqual(0, run.Floor.Dirty.Count, "bussed — the stool is free now");
        }

        [Test]
        public void ARecipe_IsBoughtOntoTheMenu_AndTheGateHolds()
        {
            // v5 P16: P10's locked cocktails were dead content — nothing ever unlocked them.
            // They are bought at day end now, and the better ones are gated on the standing.
            var recipes = RecipeCatalog.CreateDefault();
            var tonic = new IngredientCard("tonic_q", "Quinbury Tonic", IngredientType.Bubbly, 2,
                info: new IngredientInfo("tonic", tier: 1, price: 6));
            var run = new TycoonRun(NewShelf(), recipes, new RunRng("book"),
                config: new TycoonConfig(200, orderDecisionSeconds: 0, savorSeconds: 0),
                lockedStock: new[] { tonic });

            Assert.IsFalse(run.MenuRecipes.Any(r => r.Id == "gin_tonic"), "locked = off the menu");
            Assert.Throws<InvalidOperationException>(() => run.UnlockRecipe("gin_tonic"),
                "a recipe is bought at day end, not mid-shift");

            int guard = 0;
            while (run.Phase != TycoonPhase.DayEnd) { Assert.Less(guard++, 3000); run.Tick(5); }

            int before = run.Money;
            var bought = run.UnlockRecipe("gin_tonic");
            Assert.AreEqual(before - run.RecipePrice(bought), run.Money);
            Assert.IsTrue(run.MenuRecipes.Any(r => r.Id == "gin_tonic"), "on the menu");
            Assert.Throws<InvalidOperationException>(() => run.UnlockRecipe("gin_tonic"),
                "no buying it twice");

            // The recipe brings its bottles: the quarantined tonic is on TONIGHT'S market,
            // so the drink just bought is never a drink the bar cannot learn to stock.
            Assert.IsTrue(run.MarketOffers.Any(o => o.Bottle.Info?.Style == "tonic"),
                "buying the recipe releases its stock to the shop");

            // The house pride wants stars a fresh bar does not have (neutral is 3.0 < 4.0).
            Assert.Throws<InvalidOperationException>(() => run.UnlockRecipe("dirty_martini"),
                "the star gate holds until the room talks");
        }

        [Test]
        public void ASnack_RidesTheTab_AndTheBowlRunsDown()
        {
            // v5 P16: a bowl is a line on the bill, not its own transaction — it settles on
            // the way out with everything else, and the bowl empties as it is served.
            var run = new TycoonRun(NewShelf(), Book, new RunRng("snack"),
                config: new TycoonConfig(20, orderDecisionSeconds: 0, savorSeconds: 0),
                snacks: new[] { new SnackDefinition("peanuts", "Peanuts", 2, 1) });
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(5); }
            var visit = run.Floor.Seated[0];

            int before = run.Money;
            run.ServeSnack("peanuts", visit);
            Assert.AreEqual(1, visit.SnacksTaken);
            Assert.AreEqual(2, visit.Paid, "the bowl went on the tab");
            Assert.AreEqual(before, run.Money, "and the tab is not paid at the counter");
            Assert.AreEqual(0, run.SnackLeft("peanuts"), "the bowl ran down");
            Assert.Throws<InvalidOperationException>(() => run.ServeSnack("peanuts", visit),
                "an empty bowl refuses");
        }

        [Test]
        public void ASnackAlone_IsRefused()
        {
            // Never alone (the pairing rule): a customer still reading the menu has no drink
            // order, so the bowl waits. Core refuses — no menu wiring can create a solo snack.
            var run = new TycoonRun(NewShelf(), Book, new RunRng("solo"),
                config: new TycoonConfig(20, orderDecisionSeconds: 30, savorSeconds: 0),
                snacks: new[] { new SnackDefinition("peanuts", "Peanuts", 2, 5) });
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(1); }
            var visit = run.Floor.Seated[0];

            Assert.IsFalse(visit.HasOrdered, "still deciding — no drink order open");
            Assert.Throws<InvalidOperationException>(() => run.ServeSnack("peanuts", visit));
            Assert.AreEqual(5, run.SnackLeft("peanuts"), "and the bowl is untouched");
        }

        [Test]
        public void TheTab_IsPaidOnTheWayOut_NotAtTheServe()
        {
            // 2026-07-31: a customer pays and rates when they finish the drink and get up.
            // The till moving at the serve was a spoiler — it announced the verdict before
            // the reaction did — so Core holds the money on the visit until they leave.
            var run = new TycoonRun(NewShelf(), Book, new RunRng("tab"),
                config: new TycoonConfig(20, orderDecisionSeconds: 0, savorSeconds: 6));
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(5); }
            var visit = run.Floor.Seated[0];

            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            PourOut(run);
            int before = run.Money;
            var verdict = run.ServeTo(visit);

            Assert.Greater(verdict.Total, 0, "a real drink went out; something is owed");
            Assert.AreEqual(before, run.Money, "the drink is on the table — the money is not in the till yet");
            Assert.AreEqual(0, run.DaySales, "and the day's books show no sale yet");
            Assert.IsFalse(visit.TabSettled);

            guard = 0;
            while (!visit.TabSettled) { Assert.Less(guard++, 100); run.Tick(2); }

            Assert.AreEqual(before + visit.Paid, run.Money, "paid in full as they got up");
            Assert.AreEqual(visit.PaidBase, run.DaySales, "the sale books when the tab settles");
        }

        [Test]
        public void AStillDecidingCustomer_CannotBeServedYet()
        {
            // A real decision beat: the drink is built and correct, but until they have
            // actually ordered it cannot be handed over (2026-07-23).
            var run = new TycoonRun(NewShelf(), Book, new RunRng("decide"),
                config: new TycoonConfig(20, orderDecisionSeconds: 5, savorSeconds: 6));
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(1); }
            var visit = run.Floor.Seated[0];

            Assert.IsFalse(visit.HasOrdered, "they just sat — still deciding");
            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            PourOut(run);
            Assert.Throws<InvalidOperationException>(() => run.ServeTo(visit),
                "no serving a customer who has not ordered");

            guard = 0;
            while (!visit.HasOrdered) { Assert.Less(guard++, 100); run.Tick(1); }
            var verdict = run.ServeTo(visit);   // the same built drink now goes out fine
            Assert.AreEqual(OrderMatch.Exact, verdict.Match);
            Assert.AreEqual(VisitState.Drinking, visit.State, "and then they nurse it");
        }

        [Test]
        public void Refills_LandOnTheInvoice()
        {
            var run = NewRun();
            PlayDayServingEveryone(run);

            // Every drink is 0.7 of capacity out of two bottles, refilled at $3 a capacity.
            // Stated from the night that actually happened: an open night's customer count is
            // no longer a constant (v5 P12).
            double poured = 0.7 * run.Floor.Finished.Count;
            int cost = run.RefillShelf();
            Assert.AreEqual((int)System.Math.Ceiling(poured * run.Config.RefillPricePerCapacity),
                cost, 1);

            var result = run.ContinueToNextDay();
            Assert.AreEqual(run.Config.Rent(1) + cost, result.Expenses, "rent + the refill");
        }

        [Test]
        public void AmbienceUpgrades_BookAsExpenses_AndLiftTheAmbience()
        {
            // Musician, counter and wall retired (2026-08-02): glassware is the ambience
            // line now, and the two retired verbs are off the shop entirely.
            var run = NewRun(startingMoney: 200);   // purchases need cash now
            PlayDayServingEveryone(run);   // reaches DayEnd
            Assert.AreEqual(0.0, run.Ambience, 1e-9, "a plain bar pleases no one extra");

            int glassware = run.BuyGlassware();

            Assert.Greater(run.Ambience, 0.0, "finer glasses please the room");
            Assert.AreEqual(2, run.GlasswareTier);
            Assert.AreEqual(glassware, run.DayUpgrades, "the invoice itemises it");

            var result = run.ContinueToNextDay();
            Assert.AreEqual(run.Config.Rent(1) + glassware, result.Expenses,
                "rent + the upgrade");
        }

        [Test]
        public void TheStanding_StartsAtZero_AndOneNightMovesItAStep()
        {
            var run = NewRun();
            Assert.AreEqual(0.0, run.Rating.Average, 1e-9,
                "a new bar has no reputation at all (2026-08-02)");
            PlayDayServingEveryone(run);
            run.ContinueToNextDay();
            Assert.Greater(run.Rating.Average, 0.0, "a good night moves the standing");
            Assert.LessOrEqual(run.Rating.Average, BarRating.MaxNightlyGain + 1e-9,
                "but only a step, never a leap");
        }

        [Test]
        public void SameDayPurchases_CanBeRefunded_AtDawnTheyAreFinal()
        {
            var run = NewRun(startingMoney: 200);
            PlayDayServingEveryone(run);

            int before = run.Money;
            int seatsBefore = run.Seats;
            run.BuySeat();
            Assert.AreEqual(1, run.TodaysPurchases.Count, "the slip lists tonight's buy");
            run.RefundToday(0);
            Assert.AreEqual(before, run.Money, "the till is made whole");
            Assert.AreEqual(seatsBefore, run.Seats, "and the stool goes back");
            Assert.AreEqual(0, run.TodaysPurchases.Count);

            run.BuySeat();
            run.ContinueToNextDay();
            Assert.AreEqual(0, run.TodaysPurchases.Count, "at dawn the slip is torn up");
            Assert.AreEqual(seatsBefore + 1, run.Seats, "yesterday's buy is final");
        }

        [Test]
        public void OneBottle_CanBeRefilledAlone()
        {
            var run = NewRun(startingMoney: 200);
            PlayDayServingEveryone(run);   // the night drank from the well

            ShelfBottle drained = null;
            foreach (var b in run.Shelf.Bottles)
                if (b.Remaining < b.Capacity) { drained = b; break; }
            Assert.IsNotNull(drained, "sanity: the night emptied something");

            int stockBefore = run.DayStock;
            int cost = run.RefillBottle(drained.Ingredient.Id);
            Assert.Greater(cost, 0);
            Assert.AreEqual(drained.Capacity, drained.Remaining, 1e-9, "that bottle is full");
            Assert.AreEqual(stockBefore + cost, run.DayStock, "and only that bottle was billed");
        }

        [Test]
        public void NothingSellsOnCredit()
        {
            // GDD 23 §6 (2026-07-22): if the till cannot cover it, the buy is refused.
            // Only rent may push the till below zero.
            var run = NewRun();   // $20 start; the day leaves ~$80 in the till, under the $90 musician
            PlayDayServingEveryone(run);

            Assert.Less(run.Money, run.Config.MusicianPrice, "sanity: the musician is out of reach");
            Assert.Throws<InvalidOperationException>(() => run.BuyMusician());
            Assert.AreEqual(0, run.DayUpgrades, "a refused buy books nothing");
        }

        [Test]
        public void Glassware_CapsAtTheTopTier()
        {
            var run = NewRun(startingMoney: 300);
            PlayDayServingEveryone(run);

            run.BuyGlassware();   // 1 → 2
            run.BuyGlassware();   // 2 → 3

            Assert.AreEqual(3, run.GlasswareTier);
            Assert.Throws<InvalidOperationException>(() => run.BuyGlassware(), "tier 3 is the top");
        }

        [Test]
        public void TheInvoice_ItemisesSalesTipsRentAndStock()
        {
            var run = NewRun();
            PlayDayServingEveryone(run);

            // v5 P11: exact spritzes at the new $4 base. The tip is no longer a flat speed
            // bonus but a share of the base scaled by speed/spec/fill, so it tracks the
            // drink's price instead of standing beside it. v5 P12: how many spritzes is the
            // night's business, not a constant.
            run.Floor.Finished[0].InspectId();
            Assert.AreEqual(
                run.Floor.Finished.Count * DrinkOrder.MenuPrice(run.Floor.Finished[0].Order.Wanted),
                run.DaySales);
            Assert.Greater(run.DayTips, 0, "served fast and exact, so they tip");
            Assert.LessOrEqual(run.DayTips, run.DaySales,
                "and the tip ceiling is the base price itself");
            Assert.AreEqual(run.Config.Rent(1), run.DayRent, "day 1 rent");

            int stock = run.RefillShelf();
            Assert.AreEqual(stock, run.DayStock);
            Assert.AreEqual(run.DaySales + run.DayTips, run.DayIncome);
            Assert.AreEqual(run.Config.Rent(1) + stock, run.DayExpenses);
        }

        [Test]
        public void EveryVisit_RemembersWhatItWasPouredAndWhatThatEarned()
        {
            // The night's receipt itemises the drinks off the visits themselves (v5 P13), so
            // the base halves have to add up to the day's sales exactly. They would not if the
            // slip listed menu prices instead: a wrong drink is paid at the price of the thing
            // in the glass, which is the case this pins.
            var run = NewRun();
            PlayDayServingEveryone(run);

            int itemised = 0;
            foreach (var visit in run.Floor.Finished)
            {
                Assert.NotNull(visit.Served, "a served customer knows what they were poured");
                Assert.AreEqual("spritz", visit.Served.Id);
                Assert.Greater(visit.PaidBase, 0);
                Assert.LessOrEqual(visit.PaidBase, visit.Paid, "the base cannot exceed base+tip");
                itemised += visit.PaidBase;
            }
            Assert.AreEqual(run.DaySales, itemised, "the itemised lines are the day's sales");
        }

        [Test]
        public void AStormedOffCustomer_LeavesNoLineOnTheReceipt()
        {
            var run = NewRun();
            run.Tick(20);
            var visit = run.Floor.Seated[0];
            run.Tick(visit.PatienceMax + 1);   // nobody pours; they give up and walk

            Assert.AreEqual(VisitState.StormedOff, visit.State);
            Assert.IsNull(visit.Served, "nothing was sold, so there is nothing to itemise");
            Assert.AreEqual(0, visit.PaidBase);
        }

        // ── glassware (v5 P14 / C9) ─────────────────────────────────────────────

        private static GlasswareDefinition Glass(string id, double capacity) =>
            new GlasswareDefinition(id, id, id, new[] { 1.0, 1.0 }, new[] { 10, 20 }, capacity);

        private static readonly IReadOnlyList<GlasswareDefinition> GlassSet = new[]
        {
            Glass("highball", 1.0), Glass("coupe", 0.55), Glass("pint", 1.6),
        };

        /// <summary>A run whose book is one coupe-served Spritz, so the glass the bar reaches
        /// for is unambiguous.</summary>
        private static TycoonRun RunWithGlassware(string glassId)
        {
            var recipe = new RecipeDefinition(
                "spritz", "Spritz", rank: 2, baseFlavor: 10, baseMult: 2,
                flavorPerLevel: 0, multPerLevel: 0,
                requirements: Array.Empty<PatternRequirement>(),
                ratioRequirements: new[]
                {
                    new RatioRequirement(IngredientType.Spirit, 0.3, 0.7),
                    new RatioRequirement(IngredientType.Bubbly, 0.3, 0.7),
                },
                minFill: 0.5, glassId: glassId);
            return new TycoonRun(NewShelf(), new[] { recipe }, new RunRng("glassware"),
                config: new TycoonConfig(20, orderDecisionSeconds: 0, savorSeconds: 0),
                glassware: GlassSet);
        }

        [Test]
        public void ThePourOut_ReachesForTheDrinksOwnGlass()
        {
            var run = RunWithGlassware("coupe");
            Assert.AreEqual("highball", run.ServingGlassware.Id, "an empty counter holds the default");

            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            PourOut(run);

            Assert.AreEqual("coupe", run.ServingGlassware.Id, "the shaker said Spritz, so a coupe");
            Assert.AreEqual(0.55, run.ServingGlass.Capacity, 1e-9, "and a coupe is a small drink");
        }

        [Test]
        public void AMixTheBarCannotName_LandsInTheDefaultGlass()
        {
            var run = RunWithGlassware("coupe");
            run.PourMeasure("gin", 0.7);   // all spirit: no recipe matches this
            PourOut(run);

            Assert.AreEqual("highball", run.ServingGlassware.Id);
        }

        [Test]
        public void TheGlassIsNeverSwappedUnderLiquid()
        {
            var run = RunWithGlassware("coupe");
            run.PourMeasure("gin", 0.2);
            run.PourMeasure("soda", 0.2);
            PourOut(run);
            var chosen = run.ServingGlassware;

            // A second build tipped into the same glass must not change the vessel: the drink
            // standing in it would either spill or be silently topped up.
            run.PourMeasure("gin", 0.6);
            PourOut(run);

            Assert.AreSame(chosen, run.ServingGlassware);
        }

        [Test]
        public void ABarWithNoGlassSet_KeepsTheSingleGlass()
        {
            var run = NewRun();   // built without glassware, like the bench runs and the sim
            Assert.IsNull(run.ServingGlassware);
            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            PourOut(run);

            Assert.IsNull(run.ServingGlassware);
            Assert.AreEqual(run.Config.GlassCapacity, run.ServingGlass.Capacity, 1e-9);
        }

        [Test]
        public void ABuiltDrink_CanBeFinishedAtTheGlass()
        {
            // A built drink never sees the shaker, so the shaker's AddPreparation cannot reach
            // it (v5 P14). Without the glass-side verb every serving spec asking for ice on one
            // was unmeetable.
            var run = NewRun();
            run.PourAtGlass("soda", 0.4);
            run.AddPreparationAtGlass(Preparations.Ice);

            Assert.IsTrue(run.ServingGlass.HasPreparation(Preparations.Ice.Id));
            Assert.IsFalse(run.Glass.HasPreparation(Preparations.Ice.Id),
                "the shaker was never involved");
        }

        [Test]
        public void ABrimfulGlass_TakesNoFinishingTouch()
        {
            // The shaker's rule, against the glass it is actually going in: ice needs somewhere
            // to go, and a full glass that takes it anyway is a garnished drink for free.
            var run = NewRun();
            run.PourAtGlass("soda", run.ServingGlass.Capacity);

            Assert.IsTrue(run.ServingGlass.IsFull);
            Assert.IsFalse(run.CanFinishAtGlass);
            Assert.Throws<InvalidOperationException>(
                () => run.AddPreparationAtGlass(Preparations.Ice));
        }

        [Test]
        public void Shaking_RecordsThePreparation()
        {
            var run = NewRun();
            run.PourMeasure("gin", 0.4);

            run.Shake();

            Assert.IsTrue(run.IsShaken);
            Assert.IsTrue(run.Glass.HasPreparation("shaken"));
        }

        [Test]
        public void ASloppyServePour_CostsTheRecipe()
        {
            var run = NewRun();
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(5); }
            var visit = run.Floor.Seated[0];   // day 1 orders a Spritz

            // An exact spritz built in the shaker, then poured badly: 60% misses the rim,
            // so the serving glass lands under the recipe's MinFill and the drink no longer
            // reads as a Spritz. The aim is the skill; spilling has a price.
            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);
            run.PourIntoServingGlass(0.7, accuracy: 0.4);

            Assert.Less(run.ServingGlass.FillFraction, 0.5, "a spilled pour under-fills the glass");
            Assert.IsTrue(run.Glass.IsEmpty, "the shaker is spent either way");

            var verdict = run.ServeTo(visit);
            Assert.AreNotEqual(OrderMatch.Exact, verdict.Match, "the spill lost the recipe");
        }

        [Test]
        public void ADrinkStillInTheShaker_CannotBeServed()
        {
            // Backing out of the build (closing the flow) used to hand the shaker over whole,
            // which skipped the aim-and-spill pour entirely — the drink was served without ever
            // being in a glass. The glass is the drink now (ruling 2026-07-28).
            var run = NewRun();
            int guard = 0;
            while (run.Floor.Seated.Count == 0) { Assert.Less(guard++, 100); run.Tick(5); }
            var visit = run.Floor.Seated[0];

            run.PourMeasure("gin", 0.35);
            run.PourMeasure("soda", 0.35);

            Assert.IsFalse(run.DrinkReady, "nothing has reached a glass yet");
            Assert.IsTrue(run.DrinkWaitingInShaker);
            Assert.Throws<InvalidOperationException>(() => run.ServeTo(visit),
                "a full shaker is not a served drink");

            PourOut(run);

            Assert.IsTrue(run.DrinkReady);
            Assert.AreEqual(OrderMatch.Exact, run.ServeTo(visit).Match,
                "poured into the glass, the same drink goes out fine");
        }

        [Test]
        public void APintIsNotPulledOnTopOfSomeoneElsesDrink()
        {
            // Once the serve pour became mandatory the finished cocktail waits in the SERVING
            // glass, so the old "is the shaker busy" guard no longer caught this: opening a keg
            // would have run beer straight into the drink standing there (2026-07-28).
            var shelf = new Shelf(new[]
            {
                new ShelfBottle(new IngredientCard("gin", "Gin", IngredientType.Spirit, 6), capacity: 20),
                new ShelfBottle(new IngredientCard("soda", "Soda", IngredientType.Bubbly, 1), capacity: 20),
                new ShelfBottle(new IngredientCard("stout", "Stout", IngredientType.Beer, 3), capacity: 20),
            });
            var run = new TycoonRun(shelf, Book, new RunRng("pint"),
                config: new TycoonConfig(20, orderDecisionSeconds: 0, savorSeconds: 0));

            run.PourMeasure("gin", 0.35);
            PourOut(run);

            Assert.IsFalse(run.CanPull("stout"));
            Assert.Throws<InvalidOperationException>(() => run.BeginPull("stout"),
                "there is already a drink in that glass");

            run.DiscardGlass();

            Assert.IsTrue(run.CanPull("stout"));
            run.BeginPull("stout");
            run.PourTilted(1.0, TapPour.IdealTilt);
            Assert.IsTrue(run.CanPull("stout"), "topping up the same pint is what a second pull is");
        }

        [Test]
        public void AFullGlassTakesNothingMore_NotEvenIce()
        {
            // Pouring past the brim stops at it (GDD 21 §3) — and once it is there, the ice and
            // the twist are refused too, because they need room the glass no longer has.
            var run = NewRun();
            run.PourMeasure("gin", 0.6);
            double taken = run.PourMeasure("soda", 0.9);   // only 0.4 of it can fit

            Assert.AreEqual(0.4, taken, 1e-9, "the pour stops at the brim, it does not overflow");
            Assert.IsTrue(run.Glass.IsFull);
            Assert.AreEqual(1.0, run.Glass.FillFraction, 1e-9);
            Assert.IsFalse(run.CanAddPreparation);

            Assert.AreEqual(0, run.PourMeasure("gin", 0.3), "a full glass takes no more liquid");
            Assert.AreEqual(0, run.PourGarnish("gin"), "nor a pinch of garnish");
            Assert.Throws<InvalidOperationException>(() => run.AddPreparation(Preparations.Ice),
                "nor a cube of ice");
            Assert.AreEqual(1.0, run.Glass.FillFraction, 1e-9, "and it is still exactly full");
        }

        [Test]
        public void TheDayClock_AndThePourClock_AreIndependent()
        {
            // Holding a bottle while the floor runs must not double-charge time anywhere:
            // the floor ticks patience, the pour ticks volume, and serving one seat leaves
            // the others waiting untouched.
            var run = NewRun();
            int guard = 0;
            while (run.Floor.Seated.Count < 2)
            {
                Assert.Less(guard++, 300);
                run.Tick(5);
            }

            var first = run.Floor.Seated[0];
            var second = run.Floor.Seated[1];
            double secondPatienceBefore = second.PatienceLeft;

            run.BeginPour("gin");
            run.PourTick(1.0);
            run.EndPour();
            run.PourMeasure("soda", run.Glass.VolumeOf("gin"));
            PourOut(run);
            run.ServeTo(first);

            Assert.AreEqual(VisitState.Waiting, second.State);
            Assert.AreEqual(secondPatienceBefore, second.PatienceLeft, 1e-9,
                "pouring costs the pourer time, not the waiter patience — only Tick does that");
        }
    }
}
