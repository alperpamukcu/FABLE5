using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LastCall.Tests
{
    /// <summary>
    /// The v4 bottle plates import the way the sandwich reads them. BottleArt.Upright and
    /// DiegeticStage.OpaqueBounds read the mask's texels, so a plate that landed before the
    /// Items import rule compiled (Unity's default: not readable, bilinear, compressed) drew
    /// an EMPTY bottle with no error (memory: urp-2d-lighting-stage). This makes that loud.
    /// The canvases are the plan's: 96×192 in the hand, 32×64 in the cellar
    /// (Docs/PLAN_bottle_art_v4.md §3), and every card ships its whole set.
    /// </summary>
    public class V4PlateImportTests
    {
        private static List<Sprite> Plates() =>
            Resources.LoadAll<Sprite>("Items").Where(s => s.name.StartsWith("v4_")).ToList();

        private static bool IsCellar(string name) => name.EndsWith("_c");

        [Test]
        public void EveryPlate_IsReadablePointFilteredAtPpu100()
        {
            var plates = Plates();
            Assert.That(plates.Count, Is.GreaterThan(0), "no v4 plates under Resources/Items");
            foreach (var s in plates)
            {
                var t = s.texture;
                Assert.IsTrue(t.isReadable, s.name + ": not readable — the sandwich cannot see its mask");
                Assert.AreEqual(FilterMode.Point, t.filterMode, s.name + ": filter mode");
                Assert.AreEqual(100f, s.pixelsPerUnit, 0.001f, s.name + ": pixels per unit");
                Assert.AreEqual(1, t.mipmapCount, s.name + ": mipmaps");
            }
        }

        [Test]
        public void EveryPlate_HasThePlansCanvas()
        {
            foreach (var s in Plates())
            {
                int w = Mathf.RoundToInt(s.rect.width), h = Mathf.RoundToInt(s.rect.height);
                if (IsCellar(s.name)) Assert.AreEqual((32, 64), (w, h), s.name);
                else Assert.AreEqual((96, 192), (w, h), s.name);
            }
        }

        [Test]
        public void EveryCard_ShipsItsWholeSet()
        {
            var names = new HashSet<string>(Plates().Select(s => s.name));
            Assert.That(names.Count, Is.GreaterThan(0));
            foreach (var n in names)
            {
                if (n.EndsWith("_front"))
                {
                    // a glass bottle: three hand plates and three cellar plates
                    var id = n.Substring(3, n.Length - 3 - "_front".Length);
                    foreach (var suf in new[] { "_back", "_mask", "_front", "_back_c", "_mask_c", "_front_c" })
                        Assert.IsTrue(names.Contains("v4_" + id + suf), "v4_" + id + suf + " is missing");
                }
                else if (!n.Contains("_back") && !n.Contains("_mask") && !n.Contains("_front"))
                {
                    // a sealed vessel (can, carton, beer): the sprite and its cellar copy
                    var id = IsCellar(n) ? n.Substring(3, n.Length - 5) : n.Substring(3);
                    Assert.IsTrue(names.Contains("v4_" + id), "v4_" + id + " is missing");
                    Assert.IsTrue(names.Contains("v4_" + id + "_c"), "v4_" + id + "_c is missing");
                }
            }
        }
    }
}
