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
    /// THE CELLAR CABINET HOLDS THE CATALOGUE (2026-09-28, the author picked direction C: "Mahzen tasarımı = C · Art Deco
    /// vitrin"). Ten fixed niches, one family each, and the bottles never change size - so what can go wrong is a family
    /// outgrowing its niche (a fifth gin), a family with no niche, the opening shelf losing the bottle the PlayMode suite
    /// reaches for, or the drawing drifting off the numbers the stage cuts it by. Each of those is a line here, and the
    /// answer to a red one is a data or art decision - never a smaller bottle.
    /// </summary>
    public class CellarCabinetTests
    {
        private static LoadedDeck Deck() => DataLoader.ParseDeck(
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", "bottles", "base_bar.json")));

        /// <summary>What the cellar stands: every card but the garnishes and the beers, shelved as the HUD shelves it.</summary>
        private static List<IngredientCard> Shelved(IEnumerable<IngredientCard> cards) =>
            cards.Where(c => c.Type != IngredientType.Garnish && c.Type != IngredientType.Beer)
                 .OrderBy(CellarCabinet.ShelfKey).ThenBy(c => c.Name, System.StringComparer.Ordinal).ToList();

        /// <summary>The width the stage draws a bottle at: its cellar plate's opaque width (alpha over half, as
        /// ItemArt.OpaqueBounds reads it) at the cellar's one height of 64 (DiegeticStage.CellarDrawnWidth).</summary>
        private static float DrawnWidth(IngredientCard card)
        {
            var s = Resources.Load<Sprite>("Items/v4_" + card.Id + "_front_c") ?? Resources.Load<Sprite>("Items/v4_" + card.Id + "_c");
            Assert.IsNotNull(s, card.Id + ": no cellar plate");
            var t = s.texture;
            Assert.IsTrue(t.isReadable, card.Id + ": the cellar plate is not readable");
            var r = s.rect;
            var px = t.GetPixels32();
            int min = int.MaxValue, max = -1;
            for (int y = (int)r.y; y < (int)(r.y + r.height); y++)
                for (int x = (int)r.x; x < (int)(r.x + r.width); x++)
                    if (px[y * t.width + x].a > 127) { if (x < min) min = x; if (x > max) max = x; }
            Assert.That(max, Is.GreaterThanOrEqualTo(min), card.Id + ": an empty plate");
            return 64f * (max - min + 1) / r.height;
        }

        private static CellarCabinet.Slot[] Plan(List<IngredientCard> shelved) =>
            CellarCabinet.Plan(shelved.Select(DrawnWidth).ToList(), shelved.Select(CellarCabinet.NicheOf).ToList());

        [Test]
        public void TheWholeCatalogue_StandsInItsNiches_AtOnePixelOrMore()
        {
            var shelved = Shelved(Deck().Cards);
            Assert.That(shelved.Count, Is.EqualTo(36), "the pourable catalogue this cabinet was drawn for");
            var plan = Plan(shelved);
            Assert.That(plan.Length, Is.EqualTo(shelved.Count), "every bottle stands - nothing is dropped");
            for (int i = 0; i < plan.Length; i++)
            {
                var s = plan[i];
                int col = CellarCabinet.Column(s.Niche);
                Assert.IsFalse(s.Overflow, $"{shelved[i].Id}: its family is wider than niche {s.Niche} - a data decision, never a smaller bottle");
                Assert.That(s.Left, Is.GreaterThanOrEqualTo(CellarCabinet.NicheX[col] + CellarCabinet.Margin),
                    shelved[i].Id + ": runs into its niche's left margin");
                Assert.That(s.Left + s.Width, Is.LessThanOrEqualTo(CellarCabinet.NicheX[col] + CellarCabinet.NicheW[col] - CellarCabinet.Margin),
                    shelved[i].Id + ": runs into its niche's right margin");
                Assert.That(s.Foot, Is.EqualTo(CellarCabinet.FootRow[CellarCabinet.Board(s.Niche)]));
                if (i > 0 && plan[i - 1].Niche == s.Niche)
                    Assert.That(s.Left - (plan[i - 1].Left + plan[i - 1].Width), Is.GreaterThanOrEqualTo(CellarCabinet.GapMin),
                        $"{shelved[i - 1].Id} and {shelved[i].Id} closer than one pixel");
            }
        }

        [Test]
        public void EveryFamily_HasOneNiche_AndNoNicheTwoFamilies()
        {
            var byNiche = new Dictionary<int, HashSet<string>>();
            foreach (var card in Shelved(Deck().Cards))
            {
                string family = CellarCabinet.Family(card);
                Assert.That(CellarCabinet.Families, Does.Contain(family), card.Id + ": a family the cabinet has no niche for");
                int n = CellarCabinet.NicheOf(card);
                if (!byNiche.TryGetValue(n, out var set)) byNiche[n] = set = new HashSet<string>();
                set.Add(family);
            }
            foreach (var kv in byNiche)
                Assert.That(kv.Value.Count, Is.EqualTo(1), $"niche {kv.Key} holds {string.Join(", ", kv.Value)}");
            // the one regrouping the cabinet made: soda and tonic have their own niche, the cola stands with the mixers
            var all = Deck().Cards.ToDictionary(c => c.Id);
            Assert.That(CellarCabinet.Family(all["soda_klara"]), Is.EqualTo("soda"));
            Assert.That(CellarCabinet.Family(all["tonic_quinbury"]), Is.EqualTo("soda"));
            Assert.That(CellarCabinet.Family(all["cola_marlow"]), Is.EqualTo("mixer"));
        }

        /// <summary>The PlayMode suite opens the cellar and clicks CellarDoor_vodka_astra: it has to be on the opening
        /// shelf, first in the upper-left niche, and - standing alone there - in the niche's middle.</summary>
        [Test]
        public void TheOpeningShelf_StandsVodkaAstra_AloneInTheFirstNiche()
        {
            var deck = Deck();
            var shelved = Shelved(deck.StartingCards);
            int at = shelved.FindIndex(c => c.Id == "vodka_astra");
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "vodka_astra is not on the opening shelf");
            var plan = Plan(shelved);
            Assert.That(plan[at].Niche, Is.EqualTo(0));
            Assert.That(plan.Count(s => s.Niche == 0), Is.EqualTo(1));
            float mid = CellarCabinet.NicheCentre(0);
            Assert.That(Mathf.Abs(plan[at].Centre - mid), Is.LessThanOrEqualTo(1f), "a lone bottle stands under its lamp");
        }

        /// <summary>A family stands from the well bottle to the top shelf, left to right - the order the 2026-09-28
        /// planogram taught - and the plan is the same whatever else is on the shelf around it.</summary>
        [Test]
        public void AFamily_RunsFromTheWellToTheTopShelf_AndKeepsItsPlaceWhateverElseIsBought()
        {
            var shelved = Shelved(Deck().Cards);
            var vodkas = shelved.Where(c => CellarCabinet.Family(c) == "vodka").ToList();
            for (int i = 1; i < vodkas.Count; i++)
                Assert.That(vodkas[i].Info.Tier, Is.GreaterThanOrEqualTo(vodkas[i - 1].Info.Tier));
            // the gins alone and the gins among everything stand in the same place
            var gins = shelved.Where(c => CellarCabinet.Family(c) == "gin").ToList();
            var alone = Plan(gins);
            var among = Plan(shelved);
            for (int i = 0; i < gins.Count; i++)
                Assert.That(among[shelved.IndexOf(gins[i])].Left, Is.EqualTo(alone[i].Left));
        }

        /// <summary>
        /// THE DRAWING AGREES WITH THE NUMBERS. Each level ships a pilot body and a lit overlay (Tools/cellar_cabinet),
        /// 638 by the body's rows, readable. The overlay is opaque ONLY inside the ten niche rectangles - the stage lights
        /// a niche by laying that rectangle of it, unlit, over the pilot body, so an opaque pixel anywhere else would be
        /// light the stage never draws - and it is opaque at every niche's heart, under its lamp, or the niche would
        /// never light. The pilot's carcass is opaque from end post to end post, which is what hides the counter
        /// drawing's old body.
        /// </summary>
        [Test]
        public void EveryLevel_IsDrawnToTheNiches()
        {
            for (int level = 1; level <= 3; level++)
            {
                var lit = Resources.Load<Texture2D>($"Scene/cabinet_L{level}_lit");
                var pilot = Resources.Load<Texture2D>($"Scene/cabinet_L{level}_pilot");
                Assert.IsNotNull(lit, $"level {level}: no lit overlay");
                Assert.IsNotNull(pilot, $"level {level}: no pilot body");
                foreach (var t in new[] { lit, pilot })
                {
                    Assert.That(t.width, Is.EqualTo(CellarCabinet.ArtWidth), t.name);
                    Assert.That(t.height, Is.EqualTo(CellarCabinet.BodyHeight), t.name);
                    Assert.IsTrue(t.isReadable, t.name + ": not readable - the finish cannot repaint it");
                    Assert.AreEqual(FilterMode.Point, t.filterMode, t.name);
                }
                var a = lit.GetPixels32();
                var b = pilot.GetPixels32();
                var inside = new bool[a.Length];
                for (int n = 0; n < CellarCabinet.Niches; n++)
                {
                    CellarCabinet.NicheRect(n, out int x0, out int y0, out int x1, out int y1);
                    for (int row = y0; row <= y1; row++)
                        for (int x = x0; x <= x1; x++)
                            inside[(CellarCabinet.BodyBottom - row) * lit.width + x] = true;
                    // the heart of the niche: its middle column, ten rows under the lamp
                    int heart = (CellarCabinet.BodyBottom - (CellarCabinet.LampRow[CellarCabinet.Board(n)] + 10)) * lit.width
                                + (int)CellarCabinet.NicheCentre(n);
                    Assert.That(a[heart].a, Is.EqualTo(255), $"level {level}: niche {n}'s overlay is empty under its lamp");
                }
                int stray = 0;
                for (int i = 0; i < a.Length; i++)
                    if (!inside[i] && a[i].a != 0) stray++;
                Assert.That(stray, Is.EqualTo(0), $"level {level}: the lit overlay draws outside the niches");
                for (int y = 0; y < pilot.height; y++)
                    for (int x = CellarCabinet.CarcassLeft; x <= CellarCabinet.CarcassRight; x++)
                        Assert.That(b[y * pilot.width + x].a, Is.EqualTo(255), $"level {level}: a hole in the carcass at {x},{y}");
            }
        }
    }
}
