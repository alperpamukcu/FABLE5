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
                // OLIVES AND MINT HAVE HAD LOCALISED NAMES ALL ALONG (2026-09-22): the
                // preparation set carries prep.<id>.name in all 29 tables, and this branch was
                // printing Core's raw English past them. The key already exists, so nothing has
                // to be translated for the two signature pages to speak the player's language.
                default: return UIText.Caps(UIText.TOr("prep." + g.Id + ".name", g.Name));
            }
        }

        // ── the page's CHARACTER, said the way every buff is said (2026-09-22, DrinkTraits; redrawn 2026-09-23) ──
        // The mark, the figure, the word and the plate come from TycoonHud.Buffs.cs (BuffIcon / Pct / BuffWord /
        // BuffFactRow / ChromeArt.BuffPlate), shared with the market's tiles and the fittings' cards; this part
        // lays them out on a page and on the licence's tip.

        private static string TraitName(DrinkTrait t) => UIText.T("book.trait." + t.Id + ".name");
        private static string TraitLine(DrinkTrait t) => UIText.T("book.trait." + t.Id + ".line");

        /// <summary>The mark for the LEVER a character pulls - the family its stat belongs to (clock, round,
        /// room, counter, craft, till). No longer drawn on the page, where every stat has a mark of its own
        /// (BuffIcon); kept as the family's fallback for a caller that has a lever and no stat.</summary>
        private static Sprite TraitMark(TraitChannel channel)
        {
            switch (channel)
            {
                case TraitChannel.Clock: return ChromeArt.Mark("clock");
                case TraitChannel.Round: return ChromeArt.Mark("round");
                case TraitChannel.Room: return ChromeArt.Mark("room");
                case TraitChannel.Counter: return ChromeArt.Mark("counter");
                case TraitChannel.Craft: return ChromeArt.Mark("mix");
                // The till's mark is a COIN, not the flat dollar glyph (2026-09-23, the author:
                // "$ gorseli de eski kullanilmayan bir gorsel o").
                case TraitChannel.Coin: return ItemArt.Load("coin3d_16") ?? ItemArt.Coin(16f);
                default: return null;
            }
        }

        /// <summary>A coin is the author's own drawing and carries its colour; every other lever
        /// mark is a white mask to tint.</summary>
        private static bool TraitMarkIsTinted(TraitChannel channel) => channel != TraitChannel.Coin;

        /// <summary>The fact, the groove and the character's name: 29 to the name, 12 of it, 3 of air, 4 of foot.</summary>
        private const float BuffNamedPlateH = 48f;

        /// <summary>
        /// THE CHARACTER STRIP (redrawn 2026-09-23, the author: "okunması zor üst üste binen yazılar aynı fontta
        /// aynı renkte yazılar, hangi yazının en önemli içeriği barındırdığını anlayamıyorsun").
        ///
        /// What it was: a Lime[2] wash with four texts on it in one colour. The name and the stat shared the top
        /// row from fixed guesses - the stat took its measured width, the name got what was left with a floor of
        /// 40, stepped to 8 and still ran 50-80 units under the stat on eleven of the nineteen English pages. The
        /// lever word and the host's line were both hung at y -22, the line across the whole column, so the word
        /// printed over the line's first row on eighteen of nineteen. Cream on Lime[2] is 2.9:1, and the two quiet
        /// texts were at 72% of that.
        ///
        /// What it is: a framed plate with rounded corners in the sign's ramp (<see cref="ChromeArt.BuffPlate"/>) and
        /// three rows that never share a line - the FACT (mark, the figure as the loudest thing, the word), a
        /// groove, the character's NAME in the heavy face at 8, and the host's LINE in the body face at 8. Each
        /// is placed at the measured end of the one above it.
        ///
        /// Returns the height it took, or zero for a page with no character. It spends no more than
        /// <paramref name="maxH"/>: the host's line goes first, then the name and the groove; the fact always stays.
        /// </summary>
        private float TraitStrip(RectTransform host, RecipeDefinition r, float width, float y,
            float maxH = float.PositiveInfinity)
        {
            var t = DrinkTraits.Of(r);
            if (ReferenceEquals(t, DrinkTrait.None) || !t.HasStat) return 0f;
            bool nerf = t.Sign == TraitSign.Nerf;
            var ramp = nerf ? UITheme.ViceRed : UITheme.Lime;
            const float Pad = 8f;
            float inner = width - Pad * 2f;

            var plate = NewRect("Character", host);
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 1f);
            plate.pivot = new Vector2(0.5f, 1f);
            plate.anchoredPosition = new Vector2(0f, -y);
            var bg = plate.gameObject.AddComponent<Image>();
            bg.sprite = ChromeArt.BuffPlate(ramp);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 0.5f;      // a 2-unit frame, 4-unit corners, a 4-unit foot
            bg.raycastTarget = false;

            BuffFactRow(plate, Pad, 2f, width - Pad, t.StatKey, Pct(t.StatPercent), ramp);

            float h = BuffFactPlateH;
            if (maxH >= BuffNamedPlateH)
            {
                var groove = NewRect("Groove", plate);
                Place(groove, new Vector2(0.5f, 1f), new Vector2(inner, 2f), new Vector2(0f, -24f));
                var gi = groove.gameObject.AddComponent<Image>();
                gi.color = ramp[0];
                gi.raycastTarget = false;

                var name = NewText("TraitName", plate, _shop, 8, TextAnchor.UpperLeft, UITheme.Cream[4]);   // never "Name": the PlayMode suite finds market tiles by a child Text of that name
                name.raycastTarget = false;
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                Place(name.rectTransform, new Vector2(0, 1), new Vector2(inner, 400f), new Vector2(Pad, -29f));
                name.text = UIText.Caps(TraitName(t));
                // A language whose heavy face IS its body face (Turkish, Polish, Czech, Hungarian, Romanian and
                // every non-Latin one) double-strikes the name, or it is the line under it in another colour.
                if (_shop == _body) name.gameObject.AddComponent<PixelBold>().Distance = 1f;
                float nameH = Mathf.Max(12f, Mathf.Ceil(name.preferredHeight));
                name.rectTransform.sizeDelta = new Vector2(inner, nameH);
                h = 29f + nameH + 3f + 4f;

                var line = NewText("Line", plate, _body, 8, TextAnchor.UpperLeft, UITheme.Cream[3]);
                line.raycastTarget = false;
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                Place(line.rectTransform, new Vector2(0, 1), new Vector2(inner, 400f),
                    new Vector2(Pad, -(29f + nameH + 1f)));
                line.text = TraitLine(t);
                float lineH = Mathf.Max(12f, Mathf.Ceil(line.preferredHeight));
                line.rectTransform.sizeDelta = new Vector2(inner, lineH);
                float full = 29f + nameH + 1f + lineH + 3f + 4f;
                if (full <= maxH) h = full;
                else line.enabled = false;
                // A name that wrapped past the budget goes with its groove: the fact alone, never a cut plate.
                if (h > maxH)
                {
                    gi.enabled = false;
                    name.enabled = false;
                    h = BuffFactPlateH;
                }
            }
            plate.sizeDelta = new Vector2(width, h);
            return h;
        }

        /// <summary>
        /// ONE CHIP: a drawn mark, a small caption over it and the fact under it, on its own
        /// recessed plate. Three of them stand in a row where the page used to print three
        /// separate sentences — how it is worked, what it goes in, and how hard it is.
        /// </summary>
        /// <param name="pips">0..3. Where a chip's fact is already spoken in the book's own
        /// three-lamp language, it shows the lamps instead of a mark — a second shaker beside
        /// the shaker would be two drawings of the same idea.</param>
        private void MenuChip(RectTransform row, float x, float w, string cap, string value,
            Sprite mark, Color ink, Color capInk, bool dark, int pips = 0)
        {
            var chip = NewRect("Chip", row);
            // The plate is the row's own height, never taller: a chip standing two units proud of
            // its row is the first pixel of the next collision.
            Place(chip, new Vector2(0, 0.5f), new Vector2(w, Mathf.Max(24f, row.sizeDelta.y)),
                new Vector2(x, 0f));
            var plate = chip.gameObject.AddComponent<Image>();
            plate.color = dark ? new Color(1f, 1f, 1f, 0.05f) : new Color(0.36f, 0.22f, 0.08f, 0.07f);
            plate.raycastTarget = false;

            var capT = NewText("C", chip, _body, 8, TextAnchor.UpperCenter, capInk);
            Place(capT.rectTransform, new Vector2(0.5f, 1f), new Vector2(w - 4f, 12f), new Vector2(0f, -3f));
            capT.rectTransform.pivot = new Vector2(0.5f, 1f);
            capT.horizontalOverflow = HorizontalWrapMode.Overflow;
            capT.raycastTarget = false;
            capT.text = cap;

            float textX = 0f;
            if (pips > 0)
            {
                var bulb = ChromeArt.Bulb(8);
                var socket = dark ? new Color(1f, 1f, 1f, 0.18f) : new Color(0.36f, 0.22f, 0.08f, 0.22f);
                for (int i = 0; i < 3; i++)
                {
                    var p = NewRect("P" + i, chip);
                    Place(p, new Vector2(0, 0), new Vector2(8f, 8f), new Vector2(5f + i * 11f, 8f));
                    p.pivot = new Vector2(0, 0);
                    var pi = p.gameObject.AddComponent<Image>();
                    pi.sprite = bulb;
                    pi.preserveAspect = true;
                    pi.color = i < pips ? ink : socket;
                    pi.raycastTarget = false;
                }
                textX = 38f;
            }
            else if (mark != null)
            {
                var m = NewRect("M", chip);
                Place(m, new Vector2(0, 0), new Vector2(16f, 16f), new Vector2(5f, 4f));
                m.pivot = new Vector2(0, 0);
                var mi = m.gameObject.AddComponent<Image>();
                mi.sprite = mark;
                mi.preserveAspect = true;
                mi.raycastTarget = false;
                mi.color = ink;
                textX = 22f;
            }
            var val = NewText("V", chip, _body, 16, TextAnchor.LowerLeft, ink);
            Place(val.rectTransform, new Vector2(0, 0), new Vector2(w - textX - 4f, 20f),
                new Vector2(textX + 3f, 2f));
            val.horizontalOverflow = HorizontalWrapMode.Overflow;
            val.raycastTarget = false;
            val.text = value;
            // MEASURED, AND STEPPED BACK WHEN IT MUST BE (2026-09-23). A chip is a third of the
            // column and some of the words in it are not short — HIGHBALL at 16, and whatever the
            // twenty-nine languages make of THE WAY's three prep words. The overflow mode keeps a
            // long one on one line; this keeps it inside its own plate.
            if (val.preferredWidth > val.rectTransform.sizeDelta.x)
                val.fontSize = LanguageFonts.Size(_body, 8);
        }

        /// <summary>
        /// HOW THE PAGE IS USUALLY TAKEN (2026-09-23): one line of the drink's own dressing, each
        /// with its own drawn mark — the rail's four already have one, and the two jars borrow the
        /// garnish mark. It is not a list of instructions: the order still says what THIS customer
        /// wants, and this is what they will nearly always want. Returns the height it took, or
        /// zero for a page that is taken as it comes.
        /// </summary>
        private float HabitRow(RectTransform host, RecipeDefinition r, float width, float y, bool dark)
        {
            if (r.Likes == null || r.Likes.Count == 0) return 0f;
            var ink = dark ? new Color(0.61f, 0.58f, 0.66f) : new Color(0.42f, 0.34f, 0.24f);
            const float RowH = 18f;

            var row = NewRect("Usually", host);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(width, RowH);
            row.anchoredPosition = new Vector2(0f, -y);

            var cap = NewText("C", row, _body, 8, TextAnchor.MiddleLeft, ink);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            cap.raycastTarget = false;
            cap.text = UIText.T("book.page.usually");

            // Laid out from a measured width so the run sits centred rather than starting at a
            // guess — the one thing every dated overflow in this file has in common.
            var words = new List<string>(r.Likes.Count);
            var marks = new List<Sprite>(r.Likes.Count);
            var tinted = new List<bool>(r.Likes.Count);
            foreach (var id in r.Likes)
            {
                var p = Preparations.Find(id);
                if (p == null) continue;
                words.Add(GarnishWord(p));
                // THE DISHES THE COUNTER ACTUALLY CARRIES (2026-09-23, the author: "menude
                // kullanilan garnish iconlarini guncelle, artik onlari kullanmiyoruz"). The page was
                // drawing ChromeArt's old 16-px masks while the rail, the licence and the
                // certificate all show the author's drawn dishes; one set of pictures for one set
                // of things. GarnishCounterArt is the table those three already read.
                //
                // ...AT HALF SIZE, IN ITS OWN COLOURS (2026-09-23, the author: "Garnishler hem menü
                // görsellerinde hem de ana sahnede çok karanlık kalıyorlar"). The darkness was this
                // row: it inked the author's COLOURED dish with the page's brown - a multiply by
                // (0.42, 0.34, 0.24) that took the ice bucket's mean value from 179 to about 60 - and
                // point-sampled the 35x33 drawing into 12, keeping one pixel in three. GarnishArt.Mini
                // is the dish at half size the cellar's way (ring peeled, averaged, snapped back onto
                // its own palette, one ring put back), 18x18, drawn at exactly 1x and never tinted.
                // Only the white fallback mask takes the ink.
                var dish = GarnishCounterArt(id);
                marks.Add(dish != null ? GarnishArt.Mini(dish) : ChromeArt.Mark("garnish"));
                tinted.Add(dish == null);
            }
            if (words.Count == 0) { Destroy(row.gameObject); return 0f; }

            // The mark's column is the mini's 18 whatever the dish, so the words line up.
            const float MarkBox = 18f, MarkGap = 3f;
            var widths = new float[words.Count];
            float capW = cap.preferredWidth + 6f;
            float total = capW;
            for (int i = 0; i < words.Count; i++)
            {
                var probe = NewText("W" + i, row, _body, 8, TextAnchor.MiddleLeft, ink);
                probe.horizontalOverflow = HorizontalWrapMode.Overflow;
                probe.raycastTarget = false;
                probe.text = words[i];
                widths[i] = probe.preferredWidth;
                total += MarkBox + MarkGap + widths[i] + (i + 1 < words.Count ? 8f : 0f);
                probe.rectTransform.anchorMin = probe.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                probe.rectTransform.pivot = new Vector2(0f, 0.5f);
                probe.rectTransform.sizeDelta = new Vector2(widths[i] + 2f, RowH);
            }
            // A run wider than the column gives up its caption before it gives up a dish: the dishes
            // and their words are the fact, "USUALLY WITH" is the preposition (measured 2026-09-23: the
            // widest English habit is ~251 of the 296, so only a longer language ever gets here).
            bool withCap = total <= width;
            if (!withCap) { total -= capW; cap.enabled = false; }

            // Whole units, so the 1x mini lands on pixels.
            float x = Mathf.Round(-total * 0.5f);
            if (withCap)
            {
                Place(cap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(cap.preferredWidth + 2f, RowH),
                    new Vector2(x, 0f));
                cap.rectTransform.pivot = new Vector2(0f, 0.5f);
                x = Mathf.Round(x + capW);
            }
            for (int i = 0; i < words.Count; i++)
            {
                if (marks[i] != null)
                {
                    // The mini at its own size (18x18 for the six dishes), the 16 mask at its own 16:
                    // each centred in the column, neither drawn between sizes.
                    float mw = tinted[i] ? 16f : Mathf.Min(MarkBox, marks[i].rect.width);
                    float mh = tinted[i] ? 16f : Mathf.Min(MarkBox, marks[i].rect.height);
                    var m = NewRect("M" + i, row);
                    Place(m, new Vector2(0.5f, 0.5f), new Vector2(mw, mh),
                        new Vector2(x + Mathf.Floor((MarkBox - mw) * 0.5f), 0f));
                    m.pivot = new Vector2(0f, 0.5f);
                    var mi = m.gameObject.AddComponent<Image>();
                    mi.sprite = marks[i];
                    mi.preserveAspect = true;
                    mi.raycastTarget = false;
                    mi.color = tinted[i] ? ink : Color.white;
                }
                x += MarkBox + MarkGap;
                row.Find("W" + i).GetComponent<RectTransform>().anchoredPosition = new Vector2(x, 0f);
                x = Mathf.Round(x + widths[i] + 8f);
            }
            return RowH;
        }

        /// <summary>The mark for how a drink is worked: the tin, the spoon, or nothing at all
        /// for a built one — which is the instruction, and a mark for "do nothing" would be a
        /// drawing of an absence.</summary>
        private static Sprite PrepMark(RecipeDefinition r) =>
            r.Id == "draught" ? ChromeArt.Mark("pour")
            : r.Prep == PrepMethod.Shaken ? ChromeArt.Mark("step_shake")
            : r.Prep == PrepMethod.Stirred ? ChromeArt.Mark("step_stir")
            : ChromeArt.Mark("toglass");

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
            // BRACED (2026-09-22). The signature row was indented INSIDE a braceless if and so
            // was never the draught's to skip — harmless only for as long as no pint has a
            // signature, which is a thing a future list could ask for in one line of data.
            if (r.Id != "draught")
            {
                rows.Add(new SpecRow(null, PrepWord(r)));
                // THE SIGNATURE EXTRA (2026-09-21): the sprig or the spear the page is served with, as its own row.
                if (r.Garnish != null && Preparations.Find(r.Garnish) != null)
                    rows.Add(new SpecRow(null, GarnishWord(Preparations.Find(r.Garnish))));
            }
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
        /// THE PAGE'S TITLE FACE, AND ITS SIZE (2026-09-23, the author: "Menüde alkollerin adının yazdığı üst
        /// başlığın fontunu değiş okunaklı değil"). Jersey 15 at its own 27 where it draws the language
        /// (<see cref="LanguageFonts.Title"/>), the heading face at 16 where it does not. Asked each time rather
        /// than kept in a field: the face follows the language, and the lookup is a dictionary hit.
        /// </summary>
        private (Font face, int px) TitleFace()
        {
            var face = LanguageFonts.Title(null);
            return face != null ? (face, LanguageFonts.TitlePx) : (_display, 16);
        }

        /// <summary>
        /// THE PLATE: the drink on a drawn plinth at exactly twice its own 32, and the price beside it as the
        /// biggest figure on the page, the tab under it and the list price when the page's character has moved
        /// it (2026-09-22, moved out of FillRecipePage 2026-09-23 so the licence's tip sets the same block).
        /// Hung from its own top at <paramref name="y"/>; returns its height.
        /// </summary>
        private float RecipePlinth(RectTransform host, RecipeDefinition r, float width, float y, bool locked,
            bool perfected)
        {
            Color quiet = new Color(0.52f, 0.44f, 0.36f);
            Color figure = new Color(0.10f, 0.06f, 0.02f);
            const float PlinthH = 72f, IconPx = 64f;
            var plinth = NewRect("Plate", host);
            plinth.anchorMin = plinth.anchorMax = new Vector2(0.5f, 1f);
            plinth.pivot = new Vector2(0.5f, 1f);
            plinth.sizeDelta = new Vector2(width, PlinthH);
            plinth.anchoredPosition = new Vector2(0f, -y);
            var plinthImg = plinth.gameObject.AddComponent<Image>();
            plinthImg.color = perfected ? new Color(0.84f, 0.86f, 0.93f, 0.55f)
                                        : new Color(0.36f, 0.22f, 0.08f, 0.06f);
            plinthImg.raycastTarget = false;

            // A WHOLE MULTIPLE, MEASURED (2026-09-22): DrinkIcon is struck at 32; 64 is 2x.
            var icon = NewRect("I", plinth);
            Place(icon, new Vector2(0, 0.5f), new Vector2(IconPx, IconPx), new Vector2(10f, 0f));
            icon.pivot = new Vector2(0, 0.5f);
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = _bootstrap != null ? DrinkIcon.For(r, _bootstrap.Glassware) : null;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = img.sprite != null;
            if (locked) img.color = new Color(1, 1, 1, 0.4f);

            // The list price, not tonight's crowd-adjusted one, beside the drawn coin (2026-09-22/23) - and the
            // room's PRICE in it (2026-09-23): the till charges the neon's share, so a page that printed the sheet
            // under it would be lying again. TycoonRun.PagePrice is that one number, for the book and the licence.
            int price = Run != null ? Run.PagePrice(r) : DrinkOrder.MenuPrice(r, 0.0);
            var coin = NewRect("Coin", plinth);
            Place(coin, new Vector2(0, 0.5f), new Vector2(24f, 24f), new Vector2(IconPx + 22f, 6f));
            coin.pivot = new Vector2(0, 0.5f);
            var coinImg = coin.gameObject.AddComponent<Image>();
            coinImg.sprite = ItemArt.Load("coin3d_24") ?? ItemArt.Coin(24f);
            coinImg.preserveAspect = true;
            coinImg.raycastTarget = false;
            coinImg.enabled = coinImg.sprite != null;

            var priceT = NewText("Price", plinth, _display, 24, TextAnchor.MiddleLeft, figure);
            Place(priceT.rectTransform, new Vector2(0, 0.5f), new Vector2(120f, 30f),
                new Vector2(IconPx + (coinImg.sprite != null ? 50f : 22f), 6f));
            priceT.rectTransform.pivot = new Vector2(0, 0.5f);
            priceT.horizontalOverflow = HorizontalWrapMode.Overflow;
            priceT.raycastTarget = false;
            priceT.text = price.ToString();

            var priceCap = NewText("PriceCap", plinth, _body, 8, TextAnchor.MiddleLeft, quiet);
            Place(priceCap.rectTransform, new Vector2(0, 0.5f), new Vector2(150f, 12f),
                new Vector2(IconPx + 22f, -13f));
            priceCap.rectTransform.pivot = new Vector2(0, 0.5f);
            priceCap.horizontalOverflow = HorizontalWrapMode.Overflow;
            priceCap.raycastTarget = false;
            priceCap.text = UIText.T("book.page.on_the_tab");

            // AND WHAT IT WOULD HAVE COST WITHOUT ITS CHARACTER (2026-09-22).
            var pageTrait = DrinkTraits.Of(r);
            if (pageTrait.PriceSign != 0)
            {
                int sheet = DrinkOrder.SheetPrice(r);
                var list = NewText("List", plinth, _body, 8, TextAnchor.MiddleLeft,
                    pageTrait.Sign == TraitSign.Nerf ? UITheme.ViceRed[2] : UITheme.Lime[1]);
                Place(list.rectTransform, new Vector2(0, 0.5f), new Vector2(150f, 12f),
                    new Vector2(IconPx + 22f, -25f));
                list.rectTransform.pivot = new Vector2(0, 0.5f);
                list.horizontalOverflow = HorizontalWrapMode.Overflow;
                list.raycastTarget = false;
                list.text = UIText.T("book.page.list_price", ("price", sheet.ToString()));
            }
            return PlinthH;
        }

        /// <summary>
        /// THREE CHIPS: the way, the glass, the work - one drawing against each fact (2026-09-22, moved out of
        /// FillRecipePage 2026-09-23). Hung from its own top at <paramref name="y"/>; returns its height.
        /// </summary>
        private float RecipeChips(RectTransform host, RecipeDefinition r, float width, float y)
        {
            Color ink = new Color(0.20f, 0.13f, 0.07f);
            Color quiet = new Color(0.52f, 0.44f, 0.36f);
            Color prepInk = new Color(0.11f, 0.37f, 0.40f);
            const float ChipRowH = 36f, ChipGap = 6f, ChipEdge = 4f;
            float chipW = (width - ChipEdge * 2f - ChipGap * 2f) / 3f;
            var chips = NewRect("Chips", host);
            chips.anchorMin = chips.anchorMax = new Vector2(0.5f, 1f);
            chips.pivot = new Vector2(0.5f, 1f);
            chips.sizeDelta = new Vector2(width, ChipRowH);
            chips.anchoredPosition = new Vector2(0f, -y);
            string glassId = string.IsNullOrEmpty(r.GlassId) ? "highball" : r.GlassId;
            var work = RecipeDifficulty.Of(r);
            MenuChip(chips, ChipEdge, chipW, UIText.T("book.page.way"),
                PrepWord(r), PrepMark(r), prepInk, quiet, dark: false);
            MenuChip(chips, ChipEdge + chipW + ChipGap, chipW, UIText.T("book.page.glass_cap"),
                UIText.Caps(UIText.Data("glass", glassId, "name", glassId.Replace('_', ' '))),
                ChromeArt.Mark("step_glass"), ink, quiet, dark: false);
            MenuChip(chips, ChipEdge + 2f * (chipW + ChipGap), chipW, UIText.T("book.page.work_cap"),
                DifficultyWord(work), null, DifficultyInk(work, false),
                quiet, dark: false, pips: (int)work);
            return ChipRowH;
        }

        /// <summary>The height of the pour legend's dots: RatioDots at 2x.</summary>
        private static float BookLegendDotsH =>
            ChromeArt.RatioDots(RatioBox.Count - 1, BandBoxColors, RatioBox.Count).rect.height * 2f;

        /// <summary>
        /// THE GAUGE'S LEGEND: the caption (on a page with room for it), the same five dots the rows use all
        /// lit, and the scale under them (moved out of FillRecipePage 2026-09-23). Returns its height.
        /// </summary>
        private float BookLegend(RectTransform host, float width, float y, bool caption)
        {
            Color quiet = new Color(0.52f, 0.44f, 0.36f);
            float y0 = y;
            if (caption)
            {
                var cap = NewText("Cap", host, _body, 8, TextAnchor.MiddleCenter, quiet);
                cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                cap.rectTransform.pivot = new Vector2(0.5f, 1f);
                cap.rectTransform.sizeDelta = new Vector2(width, 12f);
                cap.rectTransform.anchoredPosition = new Vector2(0, -y);
                cap.horizontalOverflow = HorizontalWrapMode.Overflow;
                cap.text = UIText.T("book.page.pour_legend");
                y += 13f;
            }
            var legend = NewRect("Legend", host);
            var legendArt = ChromeArt.RatioDots(RatioBox.Count - 1, BandBoxColors, RatioBox.Count);
            float legendW = legendArt.rect.width * 2f, legendH = legendArt.rect.height * 2f;
            legend.anchorMin = legend.anchorMax = new Vector2(0.5f, 1f);
            legend.pivot = new Vector2(0.5f, 1f);
            legend.sizeDelta = new Vector2(legendW, legendH);
            legend.anchoredPosition = new Vector2(0, -y);
            var legendImg = legend.gameObject.AddComponent<Image>();
            legendImg.sprite = legendArt;
            legendImg.raycastTarget = false;
            var scale = NewText("Scale", host, _body, 8, TextAnchor.MiddleCenter, quiet);
            scale.rectTransform.anchorMin = scale.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            scale.rectTransform.pivot = new Vector2(0.5f, 1f);
            scale.rectTransform.sizeDelta = new Vector2(width, 12f);
            scale.rectTransform.anchoredPosition = new Vector2(0, -(y + legendH + 1f));
            scale.horizontalOverflow = HorizontalWrapMode.Overflow;
            scale.text = UIText.T("book.page.pour_scale");
            y += legendH + 15f;
            return y - y0;
        }

        /// <summary>
        /// THE POURS AS THE BOOK SETS THEM: one slab a pour, the bottles that would do, the name on the row's
        /// first line and the five dots (or the perfected share) on its second, every row centred in its slab;
        /// type hints at 16 after them (moved out of FillRecipePage 2026-09-23 so the licence's tip prints the
        /// same rows). Rows stand <paramref name="pitch"/> apart. Returns the height used.
        /// </summary>
        private float BookPourRows(RectTransform host, RecipeDefinition r, TycoonRun run, float width, float y,
            float pitch, bool locked)
        {
            Color ink = new Color(0.20f, 0.13f, 0.07f);
            Color quiet = new Color(0.52f, 0.44f, 0.36f);
            Color figure = new Color(0.10f, 0.06f, 0.02f);
            Color prepInk = new Color(0.11f, 0.37f, 0.40f);
            Color goneInk = new Color(0.66f, 0.12f, 0.16f);
            Color miss = new Color(0.52f, 0.44f, 0.36f, 0.6f);
            Color have = new Color(0.36f, 0.22f, 0.08f, 0.09f);
            Color gone = new Color(0.74f, 0.16f, 0.20f, 0.13f);
            float y0 = y;
            var specRows = RecipeSpecRows(r, poursOnly: true, locked: locked);
            for (int i = 0; i < specRows.Count; i++)
            {
                var spec = specRows[i];
                // The prep word already stands over the icon; its row would say it twice.
                if (i == 0 && r.Id != "draught") continue;
                bool ingredient = spec.Style != null;
                bool stocked = !ingredient || InStock(spec.Style, spec.MinTier);

                if (spec.Hint)
                {
                    var hintT = NewText("H" + i, host, _body, 8, TextAnchor.MiddleLeft, quiet);
                    hintT.rectTransform.anchorMin = hintT.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    hintT.rectTransform.pivot = new Vector2(0.5f, 1f);
                    hintT.rectTransform.sizeDelta = new Vector2(width - 8f, 14f);
                    hintT.rectTransform.anchoredPosition = new Vector2(0, -y);
                    hintT.text = spec.Label;
                    y += 16f;
                    continue;
                }

                var line = NewRect("S" + i, host);
                line.anchorMin = line.anchorMax = new Vector2(0.5f, 1f);
                line.pivot = new Vector2(0.5f, 1f);
                line.sizeDelta = new Vector2(width, pitch - 2f);
                line.anchoredPosition = new Vector2(0, -y);
                y += pitch;

                const float RowLine1 = 20f, RowLine2 = 14f;
                float rowTop = Mathf.Max(0f, ((pitch - 2f) - (RowLine1 + RowLine2)) * 0.5f);

                if (ingredient)
                {
                    var slab = line.gameObject.AddComponent<Image>();
                    slab.color = stocked ? have : gone;
                    slab.raycastTarget = false;
                }

                float textX = 4f;
                if (ingredient)
                {
                    var pour = new List<Sprite>();
                    foreach (var b in run.Shelf.Bottles)
                    {
                        var info = b.Ingredient?.Info;
                        if (info == null || info.Style != spec.Style) continue;
                        if (info.Tier < spec.MinTier) continue;
                        var a = ItemArt.Bottle(b.Ingredient);
                        if (a != null) pour.Add(a);
                    }
                    if (pour.Count == 0)
                    {
                        var fallback = ItemArt.StyleBottle(run.CatalogueBottles, spec.Style);
                        if (fallback != null) pour.Add(fallback);
                    }
                    float box = Mathf.Min(40f, pitch - 6f);
                    float step = pour.Count > 1 ? Mathf.Min(box, 56f / pour.Count) : box;
                    for (int b = 0; b < pour.Count; b++)
                    {
                        var bi = NewRect("B" + b, line);
                        Place(bi, new Vector2(0, 0.5f), new Vector2(box, box), new Vector2(3f + b * step, 0));
                        var bimg = bi.gameObject.AddComponent<Image>();
                        bimg.sprite = pour[b];
                        bimg.preserveAspect = true;
                        bimg.raycastTarget = false;
                        bimg.color = stocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    }
                    textX = box + 6f + Mathf.Max(0, pour.Count - 1) * step;
                }

                var label = NewText("L", line, _body, 16, TextAnchor.UpperLeft,
                    ingredient ? (stocked ? ink : miss) : prepInk);
                Place(label.rectTransform, new Vector2(0, 1),
                    new Vector2(width - textX - 8f, RowLine1), new Vector2(textX, -rowTop));
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.raycastTarget = false;
                label.text = SpecLabel(spec);

                if (ingredient && !stocked)
                {
                    var lockT = NewText("X", line, _body, 8, TextAnchor.UpperLeft, goneInk);
                    Place(lockT.rectTransform, new Vector2(0, 1), new Vector2(200f, 12f),
                        new Vector2(textX, -(rowTop + RowLine1)));
                    lockT.horizontalOverflow = HorizontalWrapMode.Overflow;
                    lockT.verticalOverflow = VerticalWrapMode.Truncate;
                    lockT.raycastTarget = false;
                    lockT.text = UIText.T("book.page.bottle_locked");
                }

                if (spec.Amount.Length > 0)
                {
                    var amount = NewText("A", line, _display, 16, TextAnchor.UpperRight, figure);
                    Place(amount.rectTransform, new Vector2(1, 1), new Vector2(BkGaugeW, RowLine1),
                        new Vector2(-4f, -rowTop));
                    amount.raycastTarget = false;
                    amount.text = spec.Amount;
                    var tag = NewText("PT", line, _body, 8, TextAnchor.UpperRight,
                        new Color(0.42f, 0.46f, 0.55f));
                    Place(tag.rectTransform, new Vector2(1, 1), new Vector2(BkGaugeW, 12f),
                        new Vector2(-4f, -(rowTop + RowLine1)));
                    tag.raycastTarget = false;
                    tag.text = UIText.T("book.page.perfect_tag");
                }
                else if (spec.Box >= 0)
                {
                    var dotsArt = ChromeArt.RatioDots(locked ? -1 : spec.Box, BandBoxColors, RatioBox.Count);
                    float dw = dotsArt.rect.width * 2f, dh = dotsArt.rect.height * 2f;
                    var dots = NewRect("Dots", line);
                    Place(dots, new Vector2(1, 1), new Vector2(dw, dh),
                        new Vector2(-6f, -(rowTop + RowLine1 + (RowLine2 - dh) * 0.5f)));
                    var dimg = dots.gameObject.AddComponent<Image>();
                    dimg.sprite = dotsArt;
                    dimg.raycastTarget = false;
                    dimg.color = stocked || !ingredient ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                }
            }
            return y - y0;
        }

        /// <summary>
        /// A RECIPE AS THE BOOK SETS IT (2026-09-09; rebuilt on the page's own blocks 2026-09-23, the author:
        /// "Aynı zamanda bu menü görüntüsüne ve tasarımına göre kimlikte alkol hoverini güncelle"). The licence's
        /// tip IS a page of the book now, at the book's own column width: the chapter over the name in the title
        /// face, the plinth with the drink and its price, the three chips, the character strip, the legend and
        /// the pours - each drawn by the same method the page calls, so the two cannot drift apart.
        ///
        /// What it leaves out, and why: the page's USUALLY WITH row (the licence prints THIS customer's asks in
        /// its own SERVE row beside the tip, and a second garnish list twenty units away that can disagree with
        /// it is a trap), the player's best (read over a head, five times a night), and the story at the foot.
        /// Returns the height it used.
        /// </summary>
        private float DrawRecipeCard(RectTransform host, RecipeDefinition r, float width)
        {
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);
            if (r == null) return 0f;
            var run = Run;
            Color quiet = new Color(0.52f, 0.44f, 0.36f);
            Color figure = new Color(0.10f, 0.06f, 0.02f);
            bool perfected = run != null && r.HasAuthoredRatios && run.IsPerfected(r.Id);
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

            var chapter = Centred("Tier", perfected ? _shop : _body, 16, perfected ? BkPlatinumInk : quiet, 20f);
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

            var (titleFace, titlePx) = TitleFace();
            var head = Centred("Head", titleFace, titlePx,
                perfected ? new Color(0.22f, 0.20f, 0.34f) : new Color(0.30f, 0.16f, 0.05f), 30f);
            head.text = UIText.Caps(RecipeTitle(r));
            if (head.preferredWidth > width)
            {
                head.font = _display;
                head.fontSize = LanguageFonts.Size(_display, 16);
            }
            y += 32f;

            y += RecipePlinth(host, r, width, y, locked: false, perfected) + 4f;
            y += RecipeChips(host, r, width, y) + 4f;
            float strip = TraitStrip(host, r, width, y);
            if (strip > 0f) y += strip + 4f;
            if (run == null) return y;

            y += BookLegend(host, width, y, caption: true);
            int pours = 0;
            var peek = RecipeSpecRows(r, poursOnly: true, locked: false);
            for (int k = 0; k < peek.Count; k++)
                if (!(k == 0 && r.Id != "draught") && !peek[k].Hint) pours++;
            // Two lines to a row (20 + 14) is the floor; a short recipe gets the air the page gives it.
            y += BookPourRows(host, r, run, width, y, pours <= 3 ? 40f : 34f, locked: false);

            if (r.MinFill > 0)
            {
                var fillLine = Centred("Fill", _body, 16, figure, 20f);
                fillLine.text = UIText.T("book.page.fill", ("pct", (r.MinFill * 100).ToString("0")));
                y += 22f;
            }
            return y;
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
            // A DRAWN PIP, NOT A SQUARE OF COLOUR (2026-09-22, GDD 16 §6.8: "a dot standing in
            // for an object"). ChromeArt.Bulb is a struck disc with a rim and a lit side, white
            // for the caller to tint — and it is cached on its pixel size alone, so a lit one
            // and an empty one are the same drawing in two inks rather than two sprites.
            var bulb = ChromeArt.Bulb(Mathf.Max(6, Mathf.RoundToInt(pip)));
            for (int i = 0; i < 3; i++)
            {
                var p = NewRect("P" + i, row);
                p.anchorMin = p.anchorMax = new Vector2(0.5f, 0.5f);
                p.pivot = new Vector2(0f, 0.5f);
                p.sizeDelta = new Vector2(pip, pip);
                p.anchoredPosition = new Vector2(x + i * (pip + gap), 0f);
                var img = p.gameObject.AddComponent<Image>();
                img.sprite = bulb;
                img.preserveAspect = true;
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
