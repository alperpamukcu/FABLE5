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
            r.Id == "draught" ? "ON TAP" : r.Id == "neat_pour" ? "NEAT"
            : r.Prep == PrepMethod.Shaken ? "SHAKEN"
            : r.Prep == PrepMethod.Stirred ? "STIRRED" : "BUILT";

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
                case IngredientType.Spirit: return "ANY SPIRIT";
                case IngredientType.Beer: return "ANY BEER";
                case IngredientType.Sweet: return "ANY SWEET";
                case IngredientType.Sour: return "ANY SOUR";
                case IngredientType.Bitter: return "ANY BITTER";
                case IngredientType.Bubbly: return "ANY FIZZ";
                default: return "ANY GARNISH";
            }
        }

        /// <summary>The line that spells the word out, or null where it needs no help.</summary>
        private static string TypeHint(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.Spirit:
                    return "SPIRIT = VODKA·GIN·RUM·WHISKEY·TEQUILA";
                case IngredientType.Beer: return "BEER = LAGER·STOUT·PALE ALE";
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
                    b.IsStyleBand ? b.Style.Replace('_', ' ').ToUpperInvariant() : TypeWord(b.Type),
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
                    revealed ? "PERFECTED" : $"YOUR BEST · {bestMake.Accuracy * 100:0}%",
                    hint: true));
            if (r.MinFill > 0) rows.Add(new SpecRow(null, "FILL", $"{r.MinFill * 100:0}%+"));
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
        private float DrawRecipeSpec(RectTransform host, RecipeDefinition r, bool dark,
            float width, string note = null, bool poursOnly = false, bool locked = false)
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
                        var fallback = ItemArt.Bottle(spec.Style);
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
                label.text = spec.Label + (spec.MinTier > 1 ? $"  T{spec.MinTier}+" : "");

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
                    none.text = "NONE";
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

        /// <summary>The spec for the ordered drink, shown AT THE POINTER (hover).</summary>
        private void ShowOrderRecipeTip()
        {
            var visit = _idVisit;
            if (visit == null || _idRecipeTip == null || _idRecipeTipBody == null) return;
            float h = DrawRecipeSpec(_idRecipeTipBody, visit.Order.Wanted, dark: true, width: TipW - 20f);
            _idRecipeTip.sizeDelta = new Vector2(TipW, h + 16f);
            _idRecipeTip.gameObject.SetActive(true);
            _idRecipeTip.SetAsLastSibling();
            // NOTHING IN IT MAY TAKE THE POINTER. Only the background used to say so, which
            // was survivable while the panel was parked out in the margin; under the cursor
            // a single raycasting hairline or line of spec text brings the flicker back, and
            // the contents are rebuilt on every hover, so it is enforced on every hover.
            foreach (var g in _idRecipeTip.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;
            FollowPointerWithRecipeTip();     // place it before its first frame is drawn
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

        private static string TierName(int rank) =>
            rank <= 8 ? "STARTER" : rank <= 14 ? "MID SHELF" : rank <= 21 ? "TOP SHELF" : "HOUSE PRIDE";

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
                string word = band.Style.Replace('_', ' ').ToUpperInvariant();
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
                    ? b.Style.Replace('_', ' ').ToUpperInvariant()
                    : TypeWord(b.Type));
            if (r.MinFill > 0)
                parts.Add(string.Format("<color=#1A0E06>FILL {0:0}%+</color>", r.MinFill * 100));
            return string.Join(" · ", parts);
        }
    }
}
