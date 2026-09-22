using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Recipes: how a recipe is DRAWN, shared by the card, the market and the book.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    public sealed partial class TycoonHud
    {
        /// <summary>How a drink is worked, in one word.</summary>
        private static string PrepWord(RecipeDefinition r) =>
            r.Id == "draught" ? UIText.T("recipes.prep.on_tap")
            : r.Id == "neat_pour" ? UIText.T("recipes.prep.neat")
            : r.Prep == PrepMethod.Shaken ? UIText.T("recipes.prep.shaken")
            : r.Prep == PrepMethod.Stirred ? UIText.T("recipes.prep.stirred")
            : UIText.T("recipes.prep.built");

        /// <summary>A garnish or preparation ask, in capitals, the way the licence and the order
        /// tip print it. The names live in Core's built-in set; an id this does not know keeps
        /// the name Core gave it.</summary>
        private static string GarnishWord(PreparationDefinition g)
        {
            switch (g.Id)
            {
                case "shaken": return UIText.T("recipes.garnish.shaken");
                case "stirred": return UIText.T("recipes.garnish.stirred");
                case "ice": return UIText.T("recipes.garnish.ice");
                case "lemon_twist": return UIText.T("recipes.garnish.lemon_twist");
                case "salt_rim": return UIText.T("recipes.garnish.salt_rim");
                case "sugar_rim": return UIText.T("recipes.garnish.sugar_rim");
                case "draught": return UIText.T("recipes.garnish.draught");
                default: return UIText.Caps(g.Name);
            }
        }

        /// <summary>A recipe's name as the player's language has it (the English is recipes.json's).</summary>
        private static string RecipeTitle(RecipeDefinition r) =>
            UIText.Data("recipe", r.Id, "name", r.Name);

        /// <summary>"SHAKEN · COUPE GLASS": how the drink is worked, and the glass it goes in.</summary>
        private static string WayLine(RecipeDefinition r)
        {
            string glassId = string.IsNullOrEmpty(r.GlassId) ? "highball" : r.GlassId;
            string glassWord = UIText.Caps(UIText.Data("glass", glassId, "name", glassId.Replace('_', ' ')));
            return UIText.T("recipes.way_line", ("prep", PrepWord(r)), ("glass", glassWord));
        }

        /// <summary>A style id as a word on a card: "coffee_liqueur" is COFFEE LIQUEUR.</summary>
        private static string StyleWord(string style) => UIText.Caps(style.Replace('_', ' '));

        /// <summary>A spec row's label with the tier it asks for, when it asks for more than the well.</summary>
        private static string SpecLabel(SpecRow spec) =>
            spec.MinTier > 1
                ? UIText.T("recipes.spec.label_tier", ("label", spec.Label), ("tier", spec.MinTier))
                : spec.Label;

        /// <summary>
        /// What a TYPE band asks for, said to somebody who has never worked a bar (the
        /// author, 2026-08-02: not everyone knows what a spirit is). The two brand-agnostic
        /// orders are the only recipes that speak in types — "pour me something" and
        /// "whatever is on tap" — so this is where the word has to teach itself.
        /// </summary>
        private static string TypeWord(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.Spirit: return UIText.T("recipes.type.spirit");
                case IngredientType.Beer: return UIText.T("recipes.type.beer");
                case IngredientType.Sweet: return UIText.T("recipes.type.sweet");
                case IngredientType.Sour: return UIText.T("recipes.type.sour");
                case IngredientType.Bitter: return UIText.T("recipes.type.bitter");
                case IngredientType.Bubbly: return UIText.T("recipes.type.bubbly");
                default: return UIText.T("recipes.type.garnish");
            }
        }

        /// <summary>The line that spells the word out, or null where it needs no help.</summary>
        private static string TypeHint(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.Spirit:
                    return UIText.T("recipes.type_hint.spirit");
                case IngredientType.Beer: return UIText.T("recipes.type_hint.beer");
                default: return null;
            }
        }

        /// <summary>
        /// A recipe as a SPEC CARD: the prep, then one pour to a line, then the fill and the
        /// glass. The vertical form is the readable one (the author, 2026-08-02) — a run-on
        /// list wraps mid-number and has to be parsed; a column is read.
        ///
        /// WHAT A POUR ROW SHOWS changed with the perfect-pour respec (2026-08-20, GDD 21
        /// §9a): until the drink has been made PERFECTLY once, the row carries the five-box
        /// bar with only the perfect's 20-point box lit — the box is the whole contract now —
        /// plus a tick where the run's best make landed, so the player can triangulate. After
        /// a perfect make the exact number appears. The exact value comes ONLY from
        /// <c>TycoonRun.ExactPourFor</c>, which throws until it is earned: this card must
        /// never compute the perfect itself, because a menu that can is a menu that leaks
        /// (the ID card paid for that twice). Built once and shared, so the licence, the
        /// book, the shop and the order tip cannot drift apart.
        /// </summary>
        /// <param name="poursOnly">Just what goes in the glass and in what share — no prep
        /// word, no fill line, no glass name. The hover tip asks for this (the author,
        /// 2026-08-11: "boşu boşuna fazla okunacak iş çıkartıyorlar"); the licence, the book
        /// and the shop still take the whole card, because those are read once and deliberately
        /// while this one is read at a glance, five times a night, over somebody's head.</param>
        private List<SpecRow> RecipeSpecRows(RecipeDefinition r, bool poursOnly = false,
            bool locked = false)
        {
            var rows = new List<SpecRow>();
            // THE PREP WORD WHEN IT CHANGES WHAT YOU DO (2026-08-11, narrowed to the graded
            // methods on the author's "kaldırılan gereksiz yazıları hepsini kaldır"; widened
            // again 2026-08-14 when GDD 21 §12 was overturned). ServiceJudge.MethodScore
            // scores a shaken recipe that was not shaken at zero, and the method is 40% of
            // craft which is 35% of the tip — so SHAKEN and STIRRED were always worth
            // printing. BUILT earns its place now that every drink comes through the tin:
            // it is the instruction NOT to work this one, which is a thing you can get
            // wrong. ON TAP stays out — the keg is its own stage and never reads a card.
            if (r.Id != "draught")
                rows.Add(new SpecRow(null, PrepWord(r)));
                // THE SIGNATURE EXTRA (2026-09-21): the sprig or the spear the page is served with, as its own row.
                if (r.Garnish != null && Preparations.Find(r.Garnish) != null)
                    rows.Add(new SpecRow(null, GarnishWord(Preparations.Find(r.Garnish))));
            var bands = r.RatioRequirements;
            var run = Run;
            // The reveal gate, asked rather than computed: only a perfected page has exact
            // numbers, and only Core may say so. A page the bar does not own reveals nothing
            // at all — you cannot have perfected a drink you cannot make.
            bool revealed = !locked && r.HasAuthoredRatios && run != null && run.IsPerfected(r.Id);
            int[] shown = null;
            if (revealed)
            {
                var exact = run.ExactPourFor(r);
                var copy = new double[exact.Count];
                for (int i = 0; i < exact.Count; i++) copy[i] = exact[i];
                shown = WholePercents(copy);
            }
            var bestMake = locked || !r.HasAuthoredRatios ? null : run?.BestMakeFor(r.Id);
            for (int i = 0; i < bands.Count; i++)
            {
                var b = bands[i];
                bool banded = r.HasAuthoredRatios;
                rows.Add(new SpecRow(
                    b.IsStyleBand ? b.Style : null,
                    b.IsStyleBand ? StyleWord(b.Style) : TypeWord(b.Type),
                    revealed ? $"{shown[i]}%" : "",
                    b.MinTier,
                    box: banded ? r.PerfectBoxes[i] : -1,
                    best: bestMake != null && i < bestMake.Shares.Count ? bestMake.Shares[i] : -1));
            }
            foreach (var b in bands)
            {
                if (b.IsStyleBand) continue;
                string hint = TypeHint(b.Type);
                if (hint != null) rows.Add(new SpecRow(null, hint, hint: true));
            }
            // THE GLASS IS GONE FROM EVERY CARD. The player never picks it — TycoonRun reads
            // it off the recipe and puts the right glass on the counter — so naming it was
            // never an instruction, only a word to read past. The drink's own icon still
            // shows its shape, which is the part that was ever worth knowing.
            if (poursOnly) return rows;
            // The run's own record, under the boxes (2026-08-20, the author: the player's
            // best make is on the menu). The tip skips it — that card is read over
            // somebody's head at a glance — the book, the licence and the shop keep it.
            if (bestMake != null)
                rows.Add(new SpecRow(null,
                    revealed ? UIText.T("recipes.spec.perfected")
                        : UIText.T("recipes.spec.your_best", ("pct", (bestMake.Accuracy * 100).ToString("0"))),
                    hint: true));
            if (r.MinFill > 0) rows.Add(new SpecRow(null, UIText.T("recipes.spec.fill"), $"{r.MinFill * 100:0}%+"));
            return rows;
        }

        /// <summary>
        /// Shares as whole percents that still add up to what they came from. Rounding each
        /// one on its own prints a Gin Sour as 53 + 28 + 18 = 99, and a card that shows exact
        /// numbers cannot show numbers that do not total (the author's whole point in asking
        /// for the perfect pour). Largest remainder: everyone floors, and the pennies go to
        /// whoever was cut closest to rounding up.
        /// </summary>
        private static int[] WholePercents(double[] shares)
        {
            int n = shares.Length;
            var whole = new int[n];
            double total = 0;
            for (int i = 0; i < n; i++) total += shares[i];
            int target = (int)System.Math.Round(total * 100);

            int given = 0;
            var remainder = new double[n];
            for (int i = 0; i < n; i++)
            {
                double exact = shares[i] * 100;
                whole[i] = (int)System.Math.Floor(exact);
                remainder[i] = exact - whole[i];
                given += whole[i];
            }
            for (int spare = target - given; spare > 0; spare--)
            {
                int best = -1;
                for (int i = 0; i < n; i++)
                    if (remainder[i] >= 0 && (best < 0 || remainder[i] > remainder[best])) best = i;
                if (best < 0) break;
                whole[best]++;
                remainder[best] = -1;   // one pip each, so the biggest share cannot take them all
            }
            return whole;
        }

        /// <summary>
        /// Draws a recipe's spec into <paramref name="host"/>, one row a line: the bottle's
        /// own art, its name, its exact share. A bottle the bar HAS is framed and printed in
        /// full ink; one it lacks is dimmed and unframed, so "can I make this" is answered by
        /// looking rather than by remembering (the author, 2026-08-02). The icons are the
        /// same silhouettes that stand on the back bar — seeing them here is how the shapes
        /// become readable there.
        /// </summary>
        /// <param name="skipPrep">Leaves the prep row out — for a surface that already says
        /// how the drink is worked in its own heading (the licence's page, 2026-09-09), where
        /// the row would print SHAKEN under a line reading SHAKEN · COUPE GLASS.</param>
        private float DrawRecipeSpec(RectTransform host, RecipeDefinition r, bool dark,
            float width, string note = null, bool poursOnly = false, bool locked = false,
            bool skipPrep = false)
        {
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);

            Color ink = dark ? UITheme.Cream[4] : new Color(0.20f, 0.13f, 0.07f);
            Color quiet = dark ? new Color(0.61f, 0.58f, 0.66f) : new Color(0.52f, 0.44f, 0.36f);
            Color figure = dark ? UITheme.Cyan[3] : new Color(0.10f, 0.06f, 0.02f);
            Color prepInk = dark ? UITheme.Magenta[3] : new Color(0.11f, 0.37f, 0.40f);
            Color have = dark ? new Color(1f, 1f, 1f, 0.07f) : new Color(0.36f, 0.22f, 0.08f, 0.09f);
            Color miss = dark ? new Color(0.61f, 0.58f, 0.66f, 0.55f) : new Color(0.52f, 0.44f, 0.36f, 0.6f);
            Color gone = dark ? new Color(0.86f, 0.24f, 0.32f, 0.16f) : new Color(0.74f, 0.16f, 0.20f, 0.13f);
            Color goneInk = dark ? new Color(0.94f, 0.40f, 0.46f) : new Color(0.66f, 0.12f, 0.16f);

            var rows = RecipeSpecRows(r, poursOnly, locked);
            float y = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                var spec = rows[i];
                if (skipPrep && i == 0 && spec.Style == null) continue;
                bool ingredient = spec.Style != null;
                // The stock reading stays HONEST on a locked page: whether the shelf holds
                // this bottle is true whether or not the bar owns the recipe, and dimming it
                // to "NONE" would print a lie next to a lock. Only the gauge goes dark.
                bool stocked = ingredient && InStock(spec.Style, spec.MinTier);

                float rowH = spec.Hint ? SpecHintH : SpecRowH;
                var line = NewRect($"S{i}", host);
                Place(line, new Vector2(0, 1), new Vector2(width, rowH), Vector2.zero);
                line.pivot = new Vector2(0, 1);
                line.anchoredPosition = new Vector2(0, -y);
                y += rowH;

                // THE SLAB SAYS WHICH WAY (2026-08-10, the author: "olmayan özellikle
                // belirtilsin"). A lit wash behind a row you can pour, a red one behind a
                // row you cannot — and a WORD on the red ones, because colour alone leaves
                // the two indistinguishable for anyone who cannot separate them. It is the
                // same rule the inspector's buff icons already follow.
                if (ingredient)
                {
                    var slab = line.gameObject.AddComponent<Image>();
                    slab.color = stocked ? have : gone;
                    slab.raycastTarget = false;
                }

                float textX = 2f;
                if (ingredient)
                {
                    // EVERY BOTTLE THAT WOULD DO, not one of them (2026-08-10, the author:
                    // "seviyesi yeten rumların hepsini göster"). A band asks for a STYLE at
                    // a minimum tier, and a well with three rums can answer it three ways —
                    // the card used to draw whichever one FindByStyle happened to return,
                    // so a shelf full of choices looked like a shelf with one bottle on it.
                    var pour = new List<Sprite>();
                    if (Run != null)
                    {
                        foreach (var b in Run.Shelf.Bottles)
                        {
                            var info = b.Ingredient?.Info;
                            if (info == null || info.Style != spec.Style) continue;
                            if (info.Tier < spec.MinTier) continue;   // too plain for this drink
                            var a = ItemArt.Bottle(b.Ingredient);
                            if (a != null) pour.Add(a);
                        }
                    }
                    if (pour.Count == 0)
                    {
                        // Nothing of that style on the shelf: the catalogue's own bottle
                        // of it stands in, so the card still shows what it wants.
                        var fallback = ItemArt.StyleBottle(Run.CatalogueBottles, spec.Style);
                        if (fallback != null) pour.Add(fallback);
                    }
                    float box = SpecRowH - 3f;
                    // They overlap as they multiply rather than growing the row: a spec card
                    // is a fixed grid and three bottles must cost the same height as one.
                    float step = pour.Count > 1 ? Mathf.Min(box, 40f / pour.Count) : box;
                    for (int b = 0; b < pour.Count; b++)
                    {
                        var icon = NewRect("B" + b, line);
                        Place(icon, new Vector2(0, 0.5f), new Vector2(box, box),
                            new Vector2(3f + b * step, 0));
                        var img = icon.gameObject.AddComponent<Image>();
                        img.sprite = pour[b];
                        img.preserveAspect = true;
                        img.raycastTarget = false;
                        img.color = stocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    }
                    textX = SpecRowH + 4f + Mathf.Max(0, pour.Count - 1) * step;
                }

                // The CONTENTS are the text face: lighter and narrower than the name above
                // them, so the card has a title and a body rather than one wall of capitals —
                // and so COFFEE LIQUEUR fits beside its share instead of running into it.
                var label = NewText("L", line, _body, spec.Hint ? 8 : 16, TextAnchor.MiddleLeft,
                    ingredient ? (stocked ? ink : miss) : (i == 0 ? prepInk : quiet));
                Place(label.rectTransform, new Vector2(0, 0.5f),
                    new Vector2(width - textX - SpecAmountW - (ingredient && !stocked ? 66f : 6f), rowH),
                    Vector2.zero);
                label.rectTransform.pivot = new Vector2(0, 0.5f);
                label.rectTransform.anchoredPosition = new Vector2(textX, 0);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.raycastTarget = false;
                label.text = SpecLabel(spec);

                // NONE: the shape half of the tell. Right-aligned into the gap the share
                // leaves, so it reads on the same sweep as the percentage rather than
                // hiding at the end of a long ingredient name.
                if (ingredient && !stocked)
                {
                    var none = NewText("X", line, _body, 8, TextAnchor.MiddleRight, goneInk);
                    Place(none.rectTransform, new Vector2(1, 0.5f), new Vector2(60f, rowH),
                        new Vector2(-SpecAmountW - 4f, 0));
                    none.horizontalOverflow = HorizontalWrapMode.Overflow;
                    none.raycastTarget = false;
                    none.text = UIText.T("recipes.spec.none");
                }

                if (spec.Amount.Length > 0)
                {
                    var amount = NewText("A", line, _display, 16, TextAnchor.MiddleRight,
                        ingredient && !stocked ? goneInk : figure);
                    Place(amount.rectTransform, new Vector2(1, 0.5f), new Vector2(SpecAmountW, rowH),
                        new Vector2(-2, 0));
                    amount.horizontalOverflow = HorizontalWrapMode.Overflow;
                    amount.raycastTarget = false;
                    amount.text = spec.Amount;
                }
                else if (spec.Box >= 0)
                {
                    // THE POUR GAUGE (2026-08-20, GDD 21 §9a): a sight glass, filled to the
                    // top of the measure this bottle belongs in. It FILLS rather than lighting
                    // one box (the author: "%60'ı gösteriyorsa kırmızı turuncu ve sarı kutucuk
                    // dolu olmalıdır") because a level is what the reading actually is — how
                    // much of the drink this is — and a liquid level fills from the bottom.
                    //
                    // A locked page draws the tube EMPTY: the shopping list is public (the
                    // bottles are drawn right there), the PROPORTIONS are the craft, and the
                    // craft is what a page you have not bought is still keeping from you.
                    var gauge = NewRect("Gauge", line);
                    Place(gauge, new Vector2(1, 0.5f), new Vector2(GaugeW, GaugeH),
                        new Vector2(-4 - GaugeW, 0));
                    gauge.pivot = new Vector2(0, 0.5f);

                    var tube = gauge.gameObject.AddComponent<Image>();
                    tube.sprite = ChromeArt.GaugeTube((int)GaugeW, (int)GaugeH);
                    tube.raycastTarget = false;
                    // The tube wears the SURFACE's ink, not its own: a channel cut into the
                    // book's paper on a light card, one cut into the panel on a dark one.
                    tube.color = dark ? new Color(0.30f, 0.24f, 0.38f, stocked ? 1f : 0.6f)
                                      : new Color(0.80f, 0.74f, 0.62f, stocked ? 1f : 0.6f);

                    if (!locked)
                    {
                        var fill = NewRect("Level", gauge);
                        Place(fill, new Vector2(0, 0.5f), new Vector2(GaugeW - 2f, GaugeH - 3f),
                            new Vector2(1f, -0.5f));
                        var lvl = fill.gameObject.AddComponent<Image>();
                        lvl.sprite = ChromeArt.GaugeLadder(BandBoxColors);
                        lvl.type = Image.Type.Filled;
                        lvl.fillMethod = Image.FillMethod.Horizontal;
                        lvl.fillOrigin = (int)Image.OriginHorizontal.Left;
                        // The level stands at the TOP of its measure, which is what makes
                        // "fill to 60%" mean "the yellow band is the one to land in".
                        lvl.fillAmount = (float)RatioBox.Upper(spec.Box);
                        lvl.raycastTarget = false;
                        lvl.color = stocked || !ingredient ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                    }

                    var glass = NewRect("Glass", gauge);
                    Stretch(glass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    var gimg = glass.gameObject.AddComponent<Image>();
                    gimg.sprite = ChromeArt.GaugeGlass((int)GaugeW, (int)GaugeH, RatioBox.Count);
                    gimg.raycastTarget = false;

                    // THE CHALK MARK: where this run's best pour actually landed. The only
                    // compass the player has toward a number the menu refuses to say, and a
                    // mark that encodes something true — §6.9's test for a tick's right to exist.
                    if (spec.Best >= 0 && !locked)
                    {
                        var mark = NewRect("Best", gauge);
                        Place(mark, new Vector2(0, 0.5f), new Vector2(1f, GaugeH + 5f),
                            new Vector2(1f + Mathf.Clamp01((float)spec.Best) * (GaugeW - 3f), 0));
                        var mimg = mark.gameObject.AddComponent<Image>();
                        mimg.raycastTarget = false;
                        mimg.color = dark ? UITheme.Cream[4] : new Color(0.20f, 0.13f, 0.07f, 0.85f);
                    }
                }
            }

            if (!string.IsNullOrEmpty(note))
            {
                var n = NewText("Note", host, _body, 16, TextAnchor.MiddleLeft, quiet);
                Place(n.rectTransform, new Vector2(0, 1), new Vector2(width, SpecRowH), Vector2.zero);
                n.rectTransform.pivot = new Vector2(0, 1);
                n.rectTransform.anchoredPosition = new Vector2(2f, -y);
                n.raycastTarget = false;
                n.text = note;
                y += SpecRowH;
            }
            return y;
        }

        /// <summary>
        /// A RECIPE AS THE BOOK SETS IT (2026-09-09, the author: "kimlikte kokteyl tarifi çıkan
        /// hoveri güncelleyelim ve menüdeki tarif tarzına benzetelim"). The same grammar as
        /// FillRecipePage, on whatever paper the caller hands it: the chapter over the name,
        /// the working and the glass, the drink beside its price, the legend that says what a
        /// dot is worth, and the pours full width under it. The rows themselves are
        /// <see cref="DrawRecipeSpec"/>'s — its light palette is this page's ink — so the two
        /// surfaces can never drift apart. Returns the height it used.
        /// </summary>
        private float DrawRecipeCard(RectTransform host, RecipeDefinition r, float width)
        {
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);
            if (r == null) return 0f;
            Color ink = new Color(0.30f, 0.16f, 0.05f);
            Color quiet = new Color(0.52f, 0.44f, 0.36f);
            Color figure = new Color(0.10f, 0.06f, 0.02f);
            Color prepInk = new Color(0.11f, 0.37f, 0.40f);
            bool perfected = Run != null && r.HasAuthoredRatios && Run.IsPerfected(r.Id);
            float y = 0f;

            Text Centred(string name, Font face, int size, Color colour, float h)
            {
                var t = NewText(name, host, face, size, TextAnchor.MiddleCenter, colour);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                t.rectTransform.pivot = new Vector2(0.5f, 1f);
                t.rectTransform.sizeDelta = new Vector2(width, h);
                t.rectTransform.anchoredPosition = new Vector2(0f, -y);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = false;
                return t;
            }

            var chapter = Centred("Tier", perfected ? _shop : _body, 16,
                                  perfected ? new Color(0.42f, 0.46f, 0.55f) : quiet, 20f);
            chapter.text = TierName(r.Rank);
            if (perfected)
            {
                var seal = NewRect("PerfectMark", host);
                seal.anchorMin = seal.anchorMax = new Vector2(0.5f, 1f);
                seal.pivot = new Vector2(1f, 1f);
                seal.sizeDelta = new Vector2(16f, 16f);
                seal.anchoredPosition = new Vector2(-(chapter.preferredWidth * 0.5f + 6f), -(y + 2f));
                var simg = seal.gameObject.AddComponent<Image>();
                simg.sprite = ItemArt.Perfect(16f);
                simg.preserveAspect = true;
                simg.raycastTarget = false;
            }
            y += 20f;

            var head = Centred("Head", perfected ? _shop : _display, 16, ink, 24f);
            head.text = UIText.Caps(RecipeTitle(r));
            y += 26f;

            var way = Centred("Way", _body, 16, prepInk, 20f);
            way.text = WayLine(r);
            y += 24f;
            y += DifficultyRow(host, r, width, y, dark: false) + 2f;   // how hard it is (2026-09-13)

            // the drink and what it sells for, the way the page pairs them
            var icon = NewRect("I", host);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
            icon.pivot = new Vector2(1f, 1f);
            icon.sizeDelta = new Vector2(48f, 48f);
            icon.anchoredPosition = new Vector2(-8f, -y);
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = _bootstrap != null ? DrinkIcon.For(r, _bootstrap.Glassware) : null;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = img.sprite != null;

            var priceT = NewText("Price", host, _display, 24, TextAnchor.MiddleLeft, figure);
            priceT.rectTransform.anchorMin = priceT.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            priceT.rectTransform.pivot = new Vector2(0f, 1f);
            priceT.rectTransform.sizeDelta = new Vector2(110f, 28f);
            priceT.rectTransform.anchoredPosition = new Vector2(8f, -(y + 8f));
            priceT.horizontalOverflow = HorizontalWrapMode.Overflow;
            priceT.raycastTarget = false;
            priceT.text = "$" + DrinkOrder.MenuPrice(r);
            var priceCap = NewText("PriceCap", host, _body, 8, TextAnchor.MiddleLeft, quiet);
            priceCap.rectTransform.anchorMin = priceCap.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            priceCap.rectTransform.pivot = new Vector2(0f, 1f);
            priceCap.rectTransform.sizeDelta = new Vector2(110f, 12f);
            priceCap.rectTransform.anchoredPosition = new Vector2(10f, -(y + 34f));
            priceCap.horizontalOverflow = HorizontalWrapMode.Overflow;
            priceCap.raycastTarget = false;
            priceCap.text = UIText.T("recipes.card.on_the_tab");
            y += 56f;

            // The book's caption is "THE POUR · ONE DOT IS A FIFTH" — 29 capitals, wider than
            // this card (measured in play 2026-09-09: it ran off both edges). Same sentence,
            // the half that carries the meaning.
            var cap = Centred("Cap", _body, 16, quiet, 22f);
            cap.text = UIText.T("recipes.card.one_dot");
            y += 20f;
            var legend = NewRect("Legend", host);
            var legendArt = ChromeArt.RatioDots(RatioBox.Count - 1, BandBoxColors, RatioBox.Count);
            float legendW = legendArt.rect.width * 2f, legendH = legendArt.rect.height * 2f;
            legend.anchorMin = legend.anchorMax = new Vector2(0.5f, 1f);
            legend.pivot = new Vector2(0.5f, 1f);
            legend.sizeDelta = new Vector2(legendW, legendH);
            legend.anchoredPosition = new Vector2(0f, -y);
            var legendImg = legend.gameObject.AddComponent<Image>();
            legendImg.sprite = legendArt;
            legendImg.raycastTarget = false;
            y += legendH + 6f;

            // the pours, in the book's own order and the paper palette
            var rows = NewRect("Rows", host);
            rows.anchorMin = rows.anchorMax = new Vector2(0.5f, 1f);
            rows.pivot = new Vector2(0.5f, 1f);
            rows.sizeDelta = new Vector2(width, 10f);
            rows.anchoredPosition = new Vector2(0f, -y);
            float rowsH = DrawRecipeSpec(rows, r, dark: false, width: width, poursOnly: true,
                                         skipPrep: true);
            rows.sizeDelta = new Vector2(width, rowsH);
            return y + rowsH;
        }

        /// <summary>
        /// The recipe panel rides the pointer (the author, 2026-08-10). It used to be
        /// parked in the scrim's margin beside the card, which was itself a retreat: over
        /// the fields it FLICKERED, because the panel took the pointer, which fired the
        /// order line's PointerExit, which hid the panel, which handed the pointer back,
        /// many times a second. Nothing in the panel takes a raycast any more, so it can
        /// sit under the cursor without ever being the thing the cursor is on.
        ///
        /// It hangs down and to the right, and TURNS BACK at the edges rather than running
        /// off the screen — a tip you cannot read is not a tip.
        /// </summary>
        private void FollowPointerWithRecipeTip()
        {
            if (_idRecipeTip == null || !_idRecipeTip.gameObject.activeSelf) return;
            // ...until it is PINNED (2026-09-22, the eighth list): a tip that has stopped following is where the
            // pointer is going, and one that ran after it could never be reached.
            if (_idTipPinned) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || _idRoot == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _idRoot, mouse.position.ReadValue(), null, out local)) return;

            const float Gap = 18f;
            Vector2 size = _idRecipeTip.sizeDelta;
            float halfW = _idRoot.rect.width * 0.5f, halfH = _idRoot.rect.height * 0.5f;
            // pivot is (0,1): the position IS the panel's top-left corner
            float x = local.x + Gap;
            if (x + size.x > halfW) x = local.x - Gap - size.x;
            float y = local.y - Gap;
            if (y - size.y < -halfH) y = local.y + Gap + size.y;
            // ...and turned back or not, never past the panel (2026-09-16, the author: "Hiçbir hover ekran dışına
            // taşmamalı"): a flip at one edge can land it over the other.
            x = Mathf.Clamp(x, -halfW + 4f, halfW - size.x - 4f);
            y = Mathf.Clamp(y, -halfH + size.y + 4f, halfH - 4f);
            _idRecipeTip.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>Parks a text row at <paramref name="y"/> from the card's top, cut to what
        /// it actually needs, and answers how much of the card it just spent.</summary>
        private static float RowAt(Text row, float y, float gap)
        {
            float h = Mathf.Max(row.fontSize + 2f, row.preferredHeight);
            var rt = row.rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(y + gap));
            return h + gap + 2f;
        }

        // ── HOW HARD A DRINK IS (2026-09-13) ─────────────────────────────────────
        // The author: "kokteyllere zorluk seviyesi ekleyelim, örneğin çok malzeme isteyen zaman
        // alan kokteyller 3. seviye kırmızı zorluk, daha azı turuncu, daha azı yeşil ... market
        // hoverında da zorluğu gözüksün." Core reads it off the recipe (RecipeDifficulty); this
        // draws it the same way everywhere a drink is shown: three pips lit up to the step in
        // the step's own colour, and the word — colour AND count AND word, so no one of the
        // three has to be read alone.

        private static Color DifficultyInk(DrinkDifficulty d, bool dark) =>
            d == DrinkDifficulty.Hard ? (dark ? UITheme.ViceRed[3] : UITheme.ViceRed[2])
            : d == DrinkDifficulty.Medium ? (dark ? UITheme.Amber[3] : UITheme.Amber[2])
            : (dark ? UITheme.Lime[3] : UITheme.Lime[1]);

        private static string DifficultyWord(DrinkDifficulty d) =>
            d == DrinkDifficulty.Hard ? UIText.T("recipes.difficulty.hard")
            : d == DrinkDifficulty.Medium ? UIText.T("recipes.difficulty.medium")
            : UIText.T("recipes.difficulty.easy");

        private static string DifficultySentence(DrinkDifficulty d) =>
            d == DrinkDifficulty.Hard ? UIText.T("recipes.difficulty_sentence.hard")
            : d == DrinkDifficulty.Medium ? UIText.T("recipes.difficulty_sentence.medium")
            : UIText.T("recipes.difficulty_sentence.easy");

        /// <summary>The difficulty row, centred across <paramref name="width"/> with its top at
        /// <paramref name="y"/> from the host's top. Returns the height it took.</summary>
        private float DifficultyRow(RectTransform host, RecipeDefinition r, float width, float y,
            bool dark, int size = 16)
        {
            var d = RecipeDifficulty.Of(r);
            var ink = DifficultyInk(d, dark);
            float rowH = size + 4f;
            var row = NewRect("Difficulty", host);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(width, rowH);
            row.anchoredPosition = new Vector2(0f, -y);

            var word = NewText("W", row, _body, size, TextAnchor.MiddleLeft, ink);
            word.horizontalOverflow = HorizontalWrapMode.Overflow;
            word.raycastTarget = false;
            word.text = DifficultyWord(d);
            float pip = size >= 16 ? 10f : 6f, gap = size >= 16 ? 4f : 3f;
            float total = 3f * pip + 2f * gap + 8f + word.preferredWidth;
            float x = -total * 0.5f;
            var socket = dark ? new Color(1f, 1f, 1f, 0.18f) : new Color(0.36f, 0.22f, 0.08f, 0.22f);
            for (int i = 0; i < 3; i++)
            {
                var p = NewRect("P" + i, row);
                p.anchorMin = p.anchorMax = new Vector2(0.5f, 0.5f);
                p.pivot = new Vector2(0f, 0.5f);
                p.sizeDelta = new Vector2(pip, pip);
                p.anchoredPosition = new Vector2(x + i * (pip + gap), 0f);
                var img = p.gameObject.AddComponent<Image>();
                img.color = i < (int)d ? ink : socket;
                img.raycastTarget = false;
            }
            var wrt = word.rectTransform;
            wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.pivot = new Vector2(0f, 0.5f);
            wrt.sizeDelta = new Vector2(word.preferredWidth + 4f, rowH);
            wrt.anchoredPosition = new Vector2(x + 3f * pip + 2f * gap + 8f, 0f);
            return rowH;
        }

        /// <summary>
        /// A DRINK THE BAR HAS NOT BOUGHT (2026-09-13, the author: "satın alınmayan tariflerin
        /// içeriği gözükmemeli menüde veya market hoverında da"). What goes in it is the thing
        /// being sold, so it is not printed; how hard it is to make is, because that is what the
        /// player is deciding on. Replaces the pour rows on the market's hover card.
        /// </summary>
        private float DrawRecipeSealed(RectTransform host, RecipeDefinition r, bool dark, float width)
        {
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);
            if (r == null) return 0f;
            float y = 2f;
            y += DifficultyRow(host, r, width, y, dark) + 4f;
            var note = NewText("Sealed", host, _body, 16, TextAnchor.UpperCenter,
                dark ? new Color(0.61f, 0.58f, 0.66f) : new Color(0.52f, 0.44f, 0.36f));
            note.rectTransform.anchorMin = note.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            note.rectTransform.pivot = new Vector2(0.5f, 1f);
            note.rectTransform.anchoredPosition = new Vector2(0f, -y);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.raycastTarget = false;
            note.text = UIText.T("recipes.sealed");
            // Sized to what it wraps to: the market's card is narrower than the book's page,
            // and a fixed 40 let four wrapped lines run out of the card (measured 2026-09-13).
            note.rectTransform.sizeDelta = new Vector2(width, 1000f);
            float noteH = Mathf.Max(40f, note.preferredHeight + 2f);
            note.rectTransform.sizeDelta = new Vector2(width, noteH);
            return y + noteH;
        }

        private static string TierName(int rank) =>
            rank <= 8 ? UIText.T("recipes.tier.starter")
            : rank <= 14 ? UIText.T("recipes.tier.mid_shelf")
            : rank <= 21 ? UIText.T("recipes.tier.top_shelf")
            : UIText.T("recipes.tier.house_pride");

        /// <summary>
        /// The styles this drink names that the shelf cannot pour, in the recipe's own
        /// order. Empty when the bar can make it tonight. Type bands (ANY SPIRIT) are not
        /// counted: they ask for a kind, not a bottle, and the well always has a kind.
        /// </summary>
        private List<string> MissingStyles(RecipeDefinition r)
        {
            var missing = new List<string>();
            foreach (var band in r.RatioRequirements)
            {
                if (!band.IsStyleBand) continue;
                if (InStock(band.Style, band.MinTier)) continue;
                string word = StyleWord(band.Style);
                if (!missing.Contains(word)) missing.Add(word);
            }
            return missing;
        }

        /// <summary>"GIN · LEMON · SYRUP" — what goes in it, and not in what share.
        ///
        /// It printed the authored bands until 2026-08-20, and the perfect-pour respec takes
        /// the numbers off it twice over: those bands stopped being the acceptance the day the
        /// measure became it, and this line is only ever drawn for a page the bar does NOT own
        /// (the shop's crate) — where the author's rule is that the MAKING stays locked. The
        /// shopping list is fair game and load-bearing: the tile beside it says whether the
        /// shelf could pour the thing, which is the decision being made here.</summary>
        private static string BandLine(RecipeDefinition r)
        {
            var parts = new List<string>();
            foreach (var b in r.RatioRequirements)
                parts.Add(b.IsStyleBand
                    ? StyleWord(b.Style)
                    : TypeWord(b.Type));
            if (r.MinFill > 0)
                parts.Add("<color=#1A0E06>"
                    + UIText.T("recipes.band_fill", ("pct", (r.MinFill * 100).ToString("0")))
                    + "</color>");
            return string.Join(" · ", parts);
        }
    }
}
