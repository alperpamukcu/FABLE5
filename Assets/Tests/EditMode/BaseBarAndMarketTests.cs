using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// The branded base bar (GDD 22): a small curated shelf where every bottle is knowable,
    /// plus the brand catalogue the end-of-night market sells from. Same philosophy as the
    /// other content suites — pin the shape the design guarantees, not tunable numbers.
    /// </summary>
    public class BaseBarContentTests
    {
        private static string ReadDataFile(string relativePath) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", relativePath));

        private static IReadOnlyList<IngredientCard> All() =>
            DataLoader.ParseDeck(ReadDataFile("bottles/base_bar.json")).Cards;

        private static IReadOnlyList<IngredientCard> Starting() =>
            All().Where(c => c.Info == null || c.Info.Tier <= 1).ToList();

        /// <summary>The quarantined half: stock that exists but waits for its recipe.</summary>
        private static IReadOnlyList<IngredientCard> Locked() =>
            DataLoader.ParseDeck(ReadDataFile("bottles/base_bar.json")).LockedCards;

        [Test]
        public void TheOpeningWall_IsSmallEnoughToKnowByHeart()
        {
            // The whole point of the base bar: the 46-bottle wall was unreadable.
            //
            // COUNTED AT THE FIRST RUNG, NOT OFF THE DECK'S PARTITION (2026-08-14). This
            // used to count "unlocked tier-1 cards", which stopped meaning anything the day
            // every tier-1 bottle became something a RECIPE releases: the partition now says
            // how much stock has no page yet, not how much wall a new player reads. What a
            // new player reads is the opening shelf plus whatever the zero-star pages bring
            // with them, and that is the number this holds.
            //
            // Counted per axis since draught arrived (GDD 21 §10, 2026-07-27). The rule is
            // about what you have to hold in your head at once, and the taps are not part of
            // the bottle wall — they are their own short row with their own menu section. A
            // twelfth bottle still costs readability; a third keg does not.
            var reachable = ReachableAtFirstRung();
            var bottles = reachable.Where(c => c.Type != IngredientType.Beer).ToList();
            var kegs = All().Concat(Locked()).Where(c => c.Type == IngredientType.Beer).ToList();

            Assert.LessOrEqual(bottles.Count, 12,
                "a thirteenth bottle on the opening wall: " +
                string.Join(", ", bottles.Select(c => c.Id)));
            Assert.GreaterOrEqual(bottles.Count, 8,
                "the opening wall has gone bare: " + string.Join(", ", bottles.Select(c => c.Id)));
            Assert.LessOrEqual(kegs.Count, 3, "more taps than that and beer stops being the simple order");
            Assert.GreaterOrEqual(kegs.Count, 1, "a bar with no tap cannot answer the simplest order there is");
        }

        /// <summary>
        /// Everything a bar at zero stars can end up holding: the six it opens with, plus
        /// every tier-1 bottle whose style is named by a page that also opens at zero stars.
        /// This is the wall the tutorial happens on.
        /// </summary>
        private static List<IngredientCard> ReachableAtFirstRung()
        {
            var run = new TycoonRun(new Shelf(new[] { new ShelfBottle(All().First()) }),
                RecipeCatalog.CreateDefault(), new RunRng("wall"));
            var styles = new HashSet<string>();
            foreach (var recipe in RecipeCatalog.CreateDefault())
            {
                if (run.RecipeStarGate(recipe) > 0.0) continue;
                foreach (var band in recipe.RatioRequirements)
                    if (!string.IsNullOrEmpty(band.Style)) styles.Add(band.Style);
            }
            var opening = new HashSet<string>
            {
                "vodka_astra", "gin_boothby", "soda_klara", "lemon_fresh", "syrup_house", "beer_kestrel",
            };
            return All().Concat(Locked())
                .Where(c => c.Info != null && c.Info.Tier <= 1)
                .Where(c => opening.Contains(c.Id) || styles.Contains(c.Info.Style))
                .ToList();
        }

        /// <summary>
        /// A BOTTLE OPENS ON THE SAME RUNG AS THE FIRST PAGE THAT WANTS IT (2026-08-14).
        ///
        /// Two authors' notes, one rule. First: "tariflerde içerisinde başlangıçta satın
        /// alamadığı alkollerin olduğu kokteyl tarifinde olmasının mantığı yok… şu an ginger
        /// beer var fakat 0 yıldız tariflerinde ginger beer kullanılmıyor" — no bottle for
        /// sale before something wants it. Then, looking at the shop: "bazı meşrubatlarda
        /// alkoller gibi sonra açılabilir, örneğin başka yıldız seviyelerinde" — and the
        /// player must be able to SEE it coming.
        ///
        /// The quarantine answered the first and broke the second: a held bottle was not
        /// merely unbuyable, it was absent from the catalogue, so the mixer board drew the
        /// words "Nothing tonight" on a night when six pages on the recipe board wanted six
        /// mixers. The star lock answers both — held back, and legible on the shelf as
        /// "NEEDS 2.0 STARS". So the two ladders are not merely compatible now, they are the
        /// SAME ladder, and this is the rung-for-rung equality that says so.
        ///
        /// Beer is the exception and says so: the tap answers an order with no page at all.
        /// </summary>
        [Test]
        public void EveryBottle_OpensOnTheRungOfTheFirstPageThatWantsIt()
        {
            var opening = new HashSet<string>
            {
                "vodka_astra", "gin_boothby", "soda_klara", "lemon_fresh", "syrup_house", "beer_kestrel",
            };
            var book = RecipeCatalog.CreateDefault();
            var run = new TycoonRun(new Shelf(new[] { new ShelfBottle(All().First()) }),
                book, new RunRng("lineup"));

            foreach (var card in All().Concat(Locked()))
            {
                if (card.Info == null || opening.Contains(card.Id)) continue;
                if (card.Type == IngredientType.Beer) continue;

                // What the shop makes this bottle wait for.
                double gate = card.Info.Unlock != null
                    ? card.Info.Unlock.StarsWanted
                    : Market.RequiredStars(card.Info.Tier, card.Info.Price);

                // The first page that could pour it — a band of its style that its tier fills.
                double earliest = double.MaxValue;
                foreach (var recipe in book)
                    foreach (var band in recipe.RatioRequirements)
                        if (band.Style == card.Info.Style && band.MinTier <= card.Info.Tier)
                            earliest = System.Math.Min(earliest, run.RecipeStarGate(recipe));

                Assert.AreNotEqual(double.MaxValue, earliest,
                    $"{card.Id} is a tier-{card.Info.Tier} {card.Info.Style} and no page in the " +
                    "book can pour it.");

                // AN UPGRADE IS NOT A NEW STYLE, and is held to the weaker half of the rule.
                // vodka_vor pours every drink the well vodka does, so "the first page that
                // can pour it" is night one — but nobody NEEDS it then, and a reserve bottle
                // arriving early is a bar getting better at what it already does. What must
                // hold is only that it is never later than the page that DEMANDS its tier;
                // EveryUpgradeBottle_IsNamedByAPage checks that such a page exists at all.
                if (card.Info.Tier > 1)
                {
                    double demandedAt = double.MaxValue;
                    foreach (var recipe in book)
                        foreach (var band in recipe.RatioRequirements)
                            if (band.Style == card.Info.Style && band.MinTier >= card.Info.Tier)
                                demandedAt = System.Math.Min(demandedAt, run.RecipeStarGate(recipe));
                    Assert.LessOrEqual(gate, demandedAt,
                        $"{card.Id} unseals at {gate:0.0} stars but a page demands its tier at " +
                        $"{demandedAt:0.0} — the page would open unmakeable.");
                    continue;
                }

                Assert.AreEqual(earliest, gate, 1e-9,
                    $"{card.Id} unseals at {gate:0.0} stars but the first page that wants it " +
                    $"opens at {earliest:0.0} — move one to meet the other.");
            }
        }

        /// <summary>The quarantine is empty and stays that way: a bottle held back must be
        /// HELD, not hidden, or the board goes silent about it (2026-08-14).</summary>
        [Test]
        public void NothingIsHiddenFromTheBoard()
        {
            CollectionAssert.IsEmpty(Locked().Select(c => c.Id).ToList(),
                "a quarantined bottle cannot be seen or counted towards; use unlockStars");
        }

        /// <summary>Every brand above the well — the reserve and top shelves — is named by
        /// at least one page. A $58 bourbon no recipe asks for is a bottle the player is
        /// invited to waste money on.</summary>
        [Test]
        public void EveryUpgradeBottle_IsNamedByAPage()
        {
            var book = RecipeCatalog.CreateDefault();
            foreach (var card in All().Concat(Locked()))
            {
                if (card.Info == null || card.Info.Tier <= 1) continue;
                if (card.Type == IngredientType.Beer) continue;
                bool wanted = book.Any(r => r.RatioRequirements.Any(
                    b => b.Style == card.Info.Style && b.MinTier >= card.Info.Tier));
                Assert.IsTrue(wanted,
                    $"{card.Id} is a tier-{card.Info.Tier} {card.Info.Style} and no recipe asks for that tier.");
            }
        }

        [Test]
        public void EveryBottle_CarriesItsIdentityPapers()
        {
            foreach (var card in All())
            {
                Assert.IsNotNull(card.Info, card.Id);
                Assert.IsNotEmpty(card.Info.Style, card.Id);
                Assert.IsNotEmpty(card.Info.Origin, $"{card.Id} has no origin");
                Assert.IsNotEmpty(card.Info.Blurb, $"{card.Id} has no blurb");
            }
        }

        [Test]
        public void MixersAndGarnishes_CarryNoAlcohol()
        {
            // Tone guardrail bookkeeping: the fill axis must be reachable with zero-ABV
            // volume, so the mixers must actually be zero-ABV.
            foreach (var card in All().Where(c =>
                         c.Type == IngredientType.Bubbly || c.Type == IngredientType.Sour ||
                         c.Type == IngredientType.Garnish ||
                         (c.Type == IngredientType.Sweet && c.Info.Style == "syrup")))
                Assert.AreEqual(0, card.Info.Abv, card.Id);
        }

        [Test]
        public void StartingStyles_AreUnique()
        {
            // One bottle per style on the opening shelf, or the market's "replace your vodka"
            // upgrade would be ambiguous about which vodka.
            var styles = Starting().Select(c => c.Info.Style).ToList();
            CollectionAssert.AllItemsAreUnique(styles);
        }

        [Test]
        public void EveryMarketBrand_UpgradesAStyleTheBarCanCarry()
        {
            // No orphan upgrades: a tier-2+ brand must upgrade a style the bar either opens
            // with or can BUY as tier-1 new stock (v5 P16 — tequila's tier 2 arrived while
            // its tier 1 is itself a market bottle; the market only offers upgrades for
            // carried styles, so the chain is sonora first, alta luna after).
            var deck = DataLoader.ParseDeck(ReadDataFile("bottles/base_bar.json"));
            var reachable = new HashSet<string>(deck.Cards.Concat(deck.LockedCards)
                .Where(c => c.Info != null && c.Info.Tier <= 1)
                .Select(c => c.Info.Style));
            foreach (var brand in All().Where(c => c.Info.Tier > 1))
            {
                Assert.IsTrue(reachable.Contains(brand.Info.Style),
                    $"{brand.Id} upgrades '{brand.Info.Style}', which no tier-1 bottle carries");
                Assert.Greater(brand.Info.Price, 0, brand.Id);
            }
        }

        [Test]
        public void TheOpeningWall_CoversWhatTheFirstRungAsksFor()
        {
            // It used to demand every IngredientType in the enum off the unlocked partition,
            // for the derived type bands — and the only two recipes that still use those are
            // Draught (Beer) and Neat Pour (Spirit). Since the lineup pass (2026-08-14) the
            // Bitter and Garnish bottles wait for the pages that name them, three and four
            // rungs up, so the old assertion was asking the first night to stock the last.
            // What must be true is that the first rung can be PLAYED: every type its own
            // pages call for is on the wall a zero-star bar can reach.
            var types = ReachableAtFirstRung().Select(c => c.Type).Distinct().ToList();
            CollectionAssert.Contains(types, IngredientType.Spirit);
            CollectionAssert.Contains(types, IngredientType.Beer);
            CollectionAssert.Contains(types, IngredientType.Bubbly);
            CollectionAssert.Contains(types, IngredientType.Sour);
            CollectionAssert.Contains(types, IngredientType.Sweet);
        }
    }

    /// <summary>
    /// The end-of-night brand market (GDD 22 §4), now driven by the tycoon day loop: offers
    /// roll when the day closes, and buying swaps a bottle in place, full, so the shelf keeps
    /// its muscle memory.
    /// </summary>
    public class MarketTests
    {
        private static IngredientCard Bottle(string id, string style, int tier, int price = 0) =>
            new IngredientCard(id, id, IngredientType.Spirit, 5, new IngredientInfo(style, tier, price, "somewhere", 40, "test"));

        private static readonly IReadOnlyList<RecipeDefinition> Recipes = RecipeCatalog.CreateDefault();

        private static TycoonRun NewRun(IReadOnlyList<IngredientCard> catalogue)
        {
            var shelf = new Shelf(new[]
            {
                new ShelfBottle(Bottle("vodka_a", "vodka", 1)),
                new ShelfBottle(Bottle("gin_a", "gin", 1)),
            });
            // Rich enough that no-income test days can still shop (purchases need cash).
            return new TycoonRun(shelf, Recipes, new RunRng("MKT"),
                config: new TycoonConfig(startingMoney: 100), brandCatalogue: catalogue);
        }

        /// <summary>Fast-forwards an unserved day: everyone storms off and the day closes.</summary>
        private static void RunDayToClose(TycoonRun run)
        {
            int guard = 0;
            while (run.Phase == TycoonPhase.DayOpen)
            {
                if (guard++ > 500) throw new System.Exception("the day never closed");
                run.Tick(60);
            }
        }

        [Test]
        public void TheMarket_FillsWhenTheDayCloses()
        {
            var run = NewRun(new[] { Bottle("vodka_b", "vodka", 2, 6) });
            run.Rating.DevSet(2.0);   // the tier-2 offer is star-gated (2026-08-02)

            Assert.IsEmpty(run.MarketOffers, "no deliveries mid-day");
            RunDayToClose(run);
            Assert.AreEqual(1, run.MarketOffers.Count, "deliveries come at closing time");
        }

        [Test]
        public void BuyingABrand_StandsBesideTheOldOne()
        {
            // The author's model (2026-08-02): a better bottle is a NEW bottle. The well
            // vodka stays for the cheap drinks; the reserve arrives full beside it for the
            // cocktails that name a rung. Different brands, never the same bottle upgraded.
            var run = NewRun(new[] { Bottle("vodka_b", "vodka", 2, 6) });
            run.Rating.DevSet(2.0);   // the mid rung asks for a bar worth talking about
            RunDayToClose(run);

            int money = run.Money;
            run.BuyBrand(0);

            Assert.AreEqual(money - 6, run.Money);
            Assert.IsNotNull(run.Shelf.Find("vodka_a"), "the well brand STAYS on the shelf");
            var reserve = run.Shelf.Find("vodka_b");
            Assert.IsNotNull(reserve, "the better brand stands beside it");
            Assert.AreEqual(reserve.Capacity, reserve.Remaining, 1e-9, "and it arrives full");
        }

        [Test]
        public void AnOwnedBrand_NoLongerAppearsOnTheMarket()
        {
            var run = NewRun(new[] { Bottle("vodka_b", "vodka", 2, 6) });
            run.Rating.DevSet(2.0);
            RunDayToClose(run);
            run.BuyBrand(0);
            run.ContinueToNextDay();
            RunDayToClose(run);

            Assert.IsEmpty(run.MarketOffers, "both vodkas are owned; the catalogue is spent");
        }

        [Test]
        public void TheBrandLadder_ClimbsTheStars()
        {
            // Tier 2 wants 2.0 stars, tier 3 wants 3.0 (Market.RequiredStars) — a young bar
            // sees neither, a mid bar sees the mid rung only.
            var run = NewRun(new[]
            {
                Bottle("vodka_b", "vodka", 2, 6),
                Bottle("vodka_c", "vodka", 3, 20),
            });
            RunDayToClose(run);
            Assert.IsEmpty(run.MarketOffers, "no standing, no reserve bottles");

            // DevSet AFTER the night settles: CloseNight drags the standing toward the
            // empty test-night's zero, and the market reads the standing at the close.
            run.ContinueToNextDay();
            run.Rating.DevSet(2.0);
            RunDayToClose(run);
            Assert.AreEqual(1, run.MarketOffers.Count, "the mid rung only");
            Assert.AreEqual("vodka_b", run.MarketOffers[0].Bottle.Id);

            run.ContinueToNextDay();
            run.Rating.DevSet(3.0);
            RunDayToClose(run);
            Assert.AreEqual(2, run.MarketOffers.Count, "three stars opens the good rung");
        }
    }

    public class PreparationTests
    {
        [Test]
        public void PreparationsRecord_AndDeduplicate()
        {
            var glass = new GlassContents(1.0);
            glass.AddPreparation(Preparations.Ice);
            glass.AddPreparation(Preparations.Ice);

            Assert.AreEqual(1, glass.PreparationSteps.Count);
            Assert.IsTrue(glass.HasPreparation("ice"));
        }

        [Test]
        public void ShakenAndStirred_ShareOneSlot()
        {
            // A drink is shaken or stirred, never both; the later choice wins.
            var glass = new GlassContents(1.0);
            glass.AddPreparation(Preparations.Shaken);
            glass.AddPreparation(Preparations.Stirred);

            Assert.IsFalse(glass.HasPreparation("shaken"));
            Assert.IsTrue(glass.HasPreparation("stirred"));
        }

        [Test]
        public void ClearingTheGlass_ClearsThePreparations()
        {
            var glass = new GlassContents(1.0);
            glass.Add("gin", 0.5);
            glass.AddPreparation(Preparations.SaltRim);

            glass.Clear();

            Assert.IsEmpty(glass.PreparationSteps);
        }
    }

    public class LicenceDataTests
    {
        private static ArchetypeDefinition Archetype(params string[] hometowns) =>
            new ArchetypeDefinition("test", "Test", new[] { "Sam" },
                hometowns: hometowns.Length > 0 ? hometowns : null);

        [Test]
        public void EveryRegular_GetsAnAdultAge_AndAHometownFromThePool()
        {
            var registry = new RegularsRegistry(new[] { Archetype("Eastport", "Milltown") }, 0);
            var rng = new RunRng("licence").GetStream("customer");

            for (int i = 0; i < 20; i++)
            {
                var regular = registry.RollNext(rng);
                Assert.GreaterOrEqual(regular.Age, 21, "nobody underage in the bar");
                Assert.Less(regular.Age, 68);
                CollectionAssert.Contains(new[] { "Eastport", "Milltown" }, regular.Hometown);
            }
        }

        [Test]
        public void LicenceDetails_AreSeedDeterministic()
        {
            RegularState Roll(string seed)
            {
                var registry = new RegularsRegistry(new[] { Archetype("Eastport", "Milltown") }, 0);
                return registry.RollNext(new RunRng(seed).GetStream("customer"));
            }

            var a = Roll("PAIR");
            var b = Roll("PAIR");
            Assert.AreEqual(a.Age, b.Age);
            Assert.AreEqual(a.Hometown, b.Hometown);
        }
    }
}
