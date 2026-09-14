using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// WHAT ONE DRINKER WOULD SAY ABOUT THE DRINK IN THEIR HAND (2026-09-04, the author:
    /// "her müşteri içerken içecekte mükemmel oranda neyin yanlış olduğunu küçük bir cümle
    /// ile ipucu versin ... örnek: Gin oranı biraz daha az olabilir").
    ///
    /// ONE INGREDIENT, ONE DIRECTION, ONE SENTENCE. A drink that missed its perfect misses
    /// it in every band at once — the pour is a set of shares and they sum to a glass, so
    /// pouring too much gin makes everything else too little by construction. A note that
    /// listed all of them would be a table, and a table is the recipe book's job; what a
    /// drinker says over their glass is the ONE thing that would have changed it most.
    ///
    /// THE NUMBER NEVER LEAVES CORE. <see cref="RecipeDefinition.Perfect"/> is internal for
    /// the reason CLAUDE.md keeps repeating — the exact pour may only be shown once the
    /// drink has been made perfectly, and that door is <see cref="TycoonRun.ExactPourFor"/>.
    /// This class lives inside that wall on purpose: it reads the secret and hands back a
    /// WORD ("a little less gin"), never a value. A player who listens learns the pour by
    /// making it, which is the mechanic; a player handed 0.58 has been given the answer key.
    /// </summary>
    public readonly struct PourNote
    {
        /// <summary>Nothing to say — no authored bands, or nothing was delivered.</summary>
        public bool Silent => string.IsNullOrEmpty(Sentence);

        /// <summary>The pour landed inside the perfect window on every band.</summary>
        public bool Flawless { get; }

        /// <summary>The band the note is about: its style ("gin"), or the type's name for a
        /// band with no style. Empty when <see cref="Flawless"/> or silent.</summary>
        public string Ingredient { get; }

        /// <summary>−1 to pour less of it, +1 to pour more, 0 when there is nothing to fix.</summary>
        public int Direction { get; }

        /// <summary>How far that band sat from its perfect, as a share of the glass. Unsigned;
        /// <see cref="Direction"/> carries the sign. Reported so the UI can size a reaction to
        /// it — never so it can print it.</summary>
        public double Miss { get; }

        /// <summary>What the drinker says. Null when there is nothing to say.</summary>
        public string Sentence { get; }

        /// <summary><see cref="Sentence"/> as a string-table line (localization L1). Default (no
        /// key) exactly when <see cref="Silent"/>.</summary>
        public Line SentenceLine { get; }

        public PourNote(bool flawless, string ingredient, int direction, double miss, string sentence)
            : this(flawless, ingredient, direction, miss, sentence, default)
        {
        }

        public PourNote(bool flawless, string ingredient, int direction, double miss, string sentence,
            Line sentenceLine)
        {
            Flawless = flawless;
            Ingredient = ingredient ?? string.Empty;
            Direction = direction;
            Miss = miss;
            Sentence = sentence;
            SentenceLine = sentenceLine;
        }
    }

    /// <summary>
    /// Turns a delivered glass into the one sentence a drinker would offer about it.
    /// Pure, deterministic and rng-free: the same glass always draws the same note.
    /// </summary>
    public static class PourAdvice
    {
        /// <summary>
        /// HOW BADLY, IN WORDS. The steps are read off the box the whole pour system is
        /// built on (<see cref="RatioBox.Width"/> = 20 points of the glass), so the language
        /// and the scoring are measuring with one ruler:
        ///
        ///   ≤ 2.5 pts   the perfect window itself — nothing to say (ServiceJudge.PerfectWindow)
        ///   ≤ 6 pts     "a touch"    — inside a third of a box; the drink is right, barely off
        ///   ≤ 12 pts    "a little"   — over half a box; a taster would notice
        ///   more        "a lot"      — a box or more adrift; a different drink is arriving
        /// </summary>
        public const double TouchMiss = 0.06, LittleMiss = 0.12;

        /// <summary>What a drink with nothing wrong with it gets said about it.</summary>
        public const string FlawlessLine = "Perfect pour. Not a thing I would change.";

        /// <summary>The same praise, cut short, for a glass that was poured perfectly and
        /// then sent out missing something they asked for. The long line ends "not a thing I
        /// would change", which cannot be followed by a complaint.</summary>
        public const string FlawlessStem = "Poured just right.";

        /// <summary>
        /// HOW CLOSE COUNTS AS THE SAME MISS. A two-part drink's shares sum to a glass, so
        /// too much gin IS too little tonic and both bands are out by the identical amount —
        /// an exact tie that float arithmetic then breaks by a millionth of nothing, which
        /// had the same fault reading "less tonic" at one deviation and "more vodka" at the
        /// next. Ties are settled by the BIGGER band instead: with one fault and two names,
        /// name the ingredient that is more of the drink.
        /// </summary>
        private const double TieWindow = 1e-9;

        /// <summary>
        /// The note for one delivered glass against the recipe it was meant to be.
        ///
        /// <paramref name="recipe"/> is the drink the note is ABOUT — the ordered one for a
        /// serve that matched, so the coaching is about the drink the player was aiming at.
        /// A recipe whose bands were derived rather than authored (a pint, a neat pour) has
        /// no pour to learn and gets a silent note, not a cheerful one: a drinker praising a
        /// perfect pint for its ratios is the game saying something it does not mean.
        /// </summary>
        /// <param name="spec">What they asked for ON the drink. A garnish that was ordered
        /// and did not arrive is said AFTER the pour (the author, 2026-09-04: "hata yapılan
        /// alkolü söyledikten sonra ... bu garnishleri de sipariş etmiştim eksik kalmış") —
        /// and it is said by EVERY customer, including the ones whose drink has no ratios to
        /// coach at all. A pint poured without its ice is still a pint without its ice.</param>
        /// <summary>
        /// WHAT THEY SAY OVER THREE SIPS (2026-09-06, the author: "müşteriler toplam 3 yudum
        /// alıyor, her yudumda yeni bir cümle ekleyecekler ... 1. yudumdan sonra içkideki en
        /// büyük problemi söyleyecekler ... daha az problem olan unsuru en sonunda").
        ///
        /// The same reading as <see cref="For"/> — one ingredient, one direction, no numbers —
        /// but the whole ORDER of it, worst miss first, down to as many as
        /// <paramref name="max"/> sips. A drink with one thing wrong says one thing and then
        /// nothing: a drinker who keeps finding new faults in a glass that only had one is a
        /// drinker nobody believes. The missing-garnish line always goes LAST, because it is
        /// about what never arrived rather than about the pour, and a flawless glass says the
        /// one flawless line.
        /// </summary>
        public static IReadOnlyList<string> Lines(RecipeDefinition recipe, GlassContents glass,
            Func<string, IngredientCard> lookup, ServingSpec spec = null, int max = 3)
        {
            var said = new List<string>();
            Collect(recipe, glass, lookup, spec, max, said, new List<Line>());
            return said;
        }

        /// <summary><see cref="Lines"/> as string-table lines (localization L1): the same sips, in
        /// the same order, one line for each sentence Lines returns.</summary>
        public static IReadOnlyList<Line> SaidLines(RecipeDefinition recipe, GlassContents glass,
            Func<string, IngredientCard> lookup, ServingSpec spec = null, int max = 3)
        {
            var lines = new List<Line>();
            Collect(recipe, glass, lookup, spec, max, new List<string>(), lines);
            return lines;
        }

        /// <summary>The one reading behind <see cref="Lines"/> and <see cref="SaidLines"/>: every
        /// sentence is added to <paramref name="said"/> and its line to <paramref name="lines"/>
        /// at the same moment, so the two lists cannot disagree.</summary>
        private static void Collect(RecipeDefinition recipe, GlassContents glass,
            Func<string, IngredientCard> lookup, ServingSpec spec, int max,
            List<string> said, List<Line> lines)
        {
            if (glass == null || max <= 0) return;
            string missing = MissingLine(spec, glass, out Line missingLine);

            // Beer has one band and it is the head; it says its piece and stops.
            if (glass.HasPreparation(Preparations.Draught.Id))
            {
                var head = Head(glass, missing, missingLine);
                if (!head.Silent) { said.Add(head.Sentence); lines.Add(head.SentenceLine); }
                return;
            }

            if (recipe != null && lookup != null && recipe.HasAuthoredRatios)
            {
                var bands = recipe.RatioRequirements;
                var perfect = recipe.Perfect;
                var shares = RatioRecipeMatcher.SharesFor(recipe, glass, lookup);
                if (perfect.Length > 0 && shares.Length == perfect.Length
                    && bands.Count == perfect.Length)
                {
                    // Every band that missed its window, ordered by how far it moved the
                    // glass — the same "biggest correction in the hand" reading For() takes,
                    // laid out rather than reduced to one.
                    var order = new List<int>();
                    for (int i = 0; i < perfect.Length; i++)
                    {
                        double miss = Math.Abs(shares[i] - perfect[i]);
                        if (miss <= ServiceJudge.PerfectWindow) continue;
                        order.Add(i);
                    }
                    order.Sort((a, b) =>
                    {
                        double ma = Math.Abs(shares[a] - perfect[a]);
                        double mb = Math.Abs(shares[b] - perfect[b]);
                        if (Math.Abs(ma - mb) > TieWindow) return mb.CompareTo(ma);
                        return perfect[b].CompareTo(perfect[a]);   // ties: the bigger band first
                    });
                    foreach (int i in order)
                    {
                        if (said.Count >= max - (missing != null ? 1 : 0)) break;
                        string name = NameOf(bands[i]);
                        if (string.IsNullOrEmpty(name)) continue;
                        double d = shares[i] - perfect[i];
                        double miss = Math.Abs(d);
                        string degree = miss <= TouchMiss ? "A touch"
                            : miss <= LittleMiss ? "A little" : "A lot";
                        said.Add(degree + " " + (d > 0 ? "less" : "more") + " " + name + " next time.");
                        lines.Add(PourLine(d > 0, DegreeLine(miss, TouchMiss, LittleMiss), NameLineOf(bands[i])));
                    }
                    if (said.Count == 0 && missing == null)
                    {
                        said.Add(FlawlessLine);
                        lines.Add(Line.Of("advice.flawless"));
                    }
                }
            }
            if (said.Count == 0 && missing == null)
            {
                // Nothing to coach — no authored bands. Silence is the honest answer, and
                // the caller shows nothing rather than inventing praise.
                return;
            }
            if (missing != null && said.Count < max) { said.Add(missing); lines.Add(missingLine); }
        }

        public static PourNote For(RecipeDefinition recipe, GlassContents glass,
            Func<string, IngredientCard> lookup, ServingSpec spec = null)
        {
            if (glass == null) return default;

            // THE MISSING HALF IS COMPUTED FIRST and does not depend on the pour: it is the
            // one thing here a derived recipe can still get wrong.
            string missing = MissingLine(spec, glass, out Line missingLine);
            var silent = missing == null ? default
                : new PourNote(false, string.Empty, 0, 0, missing, missingLine);

            // A PINT HAS A CRAFT TOO, AND IT IS THE HEAD (GDD 21 §10; 2026-09-04, the
            // author: "Drinking... yerine içerken teslim edilen alkol ile ilgili bilgi
            // versin"). Beer takes no ratio bands at all, so the pour half above has nothing
            // to say about it — and on an early bar the draught and the neat pour are a real
            // share of the orders, which is exactly how a ticket ends up reading DRINKING…
            // over a drinker who was in fact handed a badly pulled pint. The head is a
            // measured band (TapPour), so it coaches the same way an ingredient does: one
            // thing, one direction, no number.
            if (glass.HasPreparation(Preparations.Draught.Id))
                return Head(glass, missing, missingLine);

            if (recipe == null || lookup == null) return silent;
            if (!recipe.HasAuthoredRatios) return silent;

            var bands = recipe.RatioRequirements;
            var perfect = recipe.Perfect;
            var shares = RatioRecipeMatcher.SharesFor(recipe, glass, lookup);
            if (perfect.Length == 0 || shares.Length != perfect.Length
                || bands.Count != perfect.Length)
                return silent;

            // THE BAND THAT MOVED THE GLASS MOST, which is the largest ABSOLUTE miss and not
            // the largest proportional one: what the player pours is volume, so the fix
            // worth naming is the biggest correction in the glass. A syrup 3 points out and
            // a gin 3 points out are the same size of mistake to the hand holding the
            // bottle. Ties go to the bigger band — with two equal corrections available, the
            // one carrying more of the drink is the one a drinker would taste first.
            int worst = -1;
            double worstMiss = 0, worstPerfect = 0, signed = 0;
            for (int i = 0; i < perfect.Length; i++)
            {
                double d = shares[i] - perfect[i];
                double miss = Math.Abs(d);
                bool better = worst < 0
                    || miss > worstMiss + TieWindow
                    || (miss >= worstMiss - TieWindow && perfect[i] > worstPerfect);
                if (!better) continue;
                worst = i; worstMiss = miss; worstPerfect = perfect[i]; signed = d;
            }
            if (worst < 0) return silent;

            // FLAWLESS MEANS NOTHING TO COMPLAIN ABOUT, not merely a perfect pour: a glass
            // poured exactly right and sent out without the ice they asked for is not a
            // drink anybody celebrates, and the burst that fires off this flag would be
            // congratulating the player for half a job.
            if (worstMiss <= ServiceJudge.PerfectWindow)
                return new PourNote(missing == null, string.Empty, 0, worstMiss,
                    missing == null ? FlawlessLine : FlawlessStem + " " + missing,
                    missing == null ? Line.Of("advice.flawless")
                        : Then(Line.Of("advice.flawless_stem"), missingLine));

            string name = NameOf(bands[worst]);
            if (string.IsNullOrEmpty(name)) return silent;

            int direction = signed > 0 ? -1 : 1;      // poured too much → pour less
            string degree = worstMiss <= TouchMiss ? "A touch"
                : worstMiss <= LittleMiss ? "A little"
                : "A lot";
            string way = direction < 0 ? "less" : "more";
            string line = degree + " " + way + " " + name + " next time.";
            if (missing != null) line += " " + missing;
            var said = PourLine(direction < 0, DegreeLine(worstMiss, TouchMiss, LittleMiss), NameLineOf(bands[worst]));
            if (missing != null) said = Then(said, missingLine);
            return new PourNote(false, name, direction, worstMiss, line, said);
        }

        // ── the same sentences as string-table lines (localization L1) ──────────
        // Each is built beside the English it mirrors, from the same branch, so a
        // language reads exactly the note English reads.

        /// <summary>"A touch" / "A little" / "A lot", by the same two steps the English uses.</summary>
        private static Line DegreeLine(double miss, double touch, double little) =>
            Line.Of(miss <= touch ? "advice.degree.touch"
                : miss <= little ? "advice.degree.little" : "advice.degree.lot");

        /// <summary>"{degree} less {ingredient} next time." or its "more" twin.</summary>
        private static Line PourLine(bool less, Line degree, Line ingredient) =>
            Line.Of(less ? "advice.pour.less" : "advice.pour.more")
                .With("degree", degree).With("ingredient", ingredient);

        /// <summary>Two sentences said one after the other: "{first} {second}".</summary>
        private static Line Then(Line first, Line second) =>
            Line.Of("advice.then").With("first", first).With("second", second);

        /// <summary>
        /// THE PINT'S OWN NOTE. Its band is <see cref="TapPour.GoodHeadMin"/>..
        /// <see cref="TapPour.GoodHeadMax"/> of the glass — a window rather than a point, so
        /// "how far off" is the distance OUTSIDE it and anything inside is simply right. The
        /// degrees are half the ratio ladder's because the band itself is: twelve points of
        /// glass against a ratio box's twenty.
        /// </summary>
        public const double HeadTouch = 0.03, HeadLittle = 0.06;

        private static PourNote Head(GlassContents glass, string missing, Line missingLine)
        {
            double head = glass.Capacity > 0 ? glass.Head / glass.Capacity : 0;
            double over = head - TapPour.GoodHeadMax, under = TapPour.GoodHeadMin - head;
            if (over <= 0 && under <= 0)
                return new PourNote(missing == null, string.Empty, 0, 0,
                    missing == null ? PulledWellLine : PulledWellStem + " " + missing,
                    missing == null ? Line.Of("advice.pulled_well")
                        : Then(Line.Of("advice.pulled_well_stem"), missingLine));

            bool tooMuch = over > 0;
            double miss = tooMuch ? over : under;
            string degree = miss <= HeadTouch ? "A touch" : miss <= HeadLittle ? "A little" : "A lot";
            string line = degree + (tooMuch ? " less" : " more") + " head next time.";
            if (missing != null) line += " " + missing;
            var said = Line.Of(tooMuch ? "advice.head.less" : "advice.head.more")
                .With("degree", DegreeLine(miss, HeadTouch, HeadLittle));
            if (missing != null) said = Then(said, missingLine);
            return new PourNote(false, "head", tooMuch ? -1 : 1, miss, line, said);
        }

        /// <summary>What a well-pulled pint gets said about it, and the short form for one
        /// that still came out missing something.</summary>
        public const string PulledWellLine = "Pulled just right. Perfect head on it.";
        public const string PulledWellStem = "Pulled just right.";

        /// <summary>
        /// WHAT THEY ASKED FOR AND DID NOT GET, in one clause, or null when the glass came
        /// out complete. The order is the SPEC's own — they list them the way they asked for
        /// them — and the names are spoken rather than titled: the spec calls a lemon twist
        /// "Lemon Twist" because that is a heading on a ticket, and nobody says that out loud.
        /// </summary>
        private static string MissingLine(ServingSpec spec, GlassContents glass, out Line missingLine)
        {
            missingLine = default;
            if (spec == null || spec.IsPlain || glass == null) return null;
            string list = null;
            var items = new List<Line>();
            int n = 0;
            foreach (var want in spec.Garnishes)
            {
                if (want == null || glass.HasPreparation(want.Id)) continue;
                string said = SpokenName(want);
                n++;
                list = list == null ? said : list + "|" + said;
                items.Add(SpokenLine(want));
            }
            if (n == 0) return null;
            // "the ice", "the ice and a lemon twist", "the ice, a lemon twist and a salted
            // rim" — a spoken list, not a comma-joined dump of ids.
            var parts = list.Split('|');
            string joined = parts[0];
            for (int i = 1; i < parts.Length; i++)
                joined += (i == parts.Length - 1 ? " and " : ", ") + parts[i];
            // The same list as lines, folded the same way: "{list}, {item}" until the last,
            // "{list} and {item}" for it.
            Line joinedLine = items[0];
            for (int i = 1; i < items.Count; i++)
                joinedLine = Line.Of(i == items.Count - 1 ? "advice.list.and" : "advice.list.comma")
                    .With("list", joinedLine).With("item", items[i]);
            missingLine = Line.Of("advice.missing").With("garnishes", joinedLine);
            return "I asked for " + joined + " as well.";
        }

        /// <summary><see cref="SpokenName"/> as a line: <c>advice.garnish.&lt;id&gt;</c>, whose
        /// English is exactly what SpokenName says for that preparation.</summary>
        private static Line SpokenLine(PreparationDefinition prep) =>
            Line.Of("advice.garnish." + prep.Id);

        /// <summary><see cref="NameOf"/> as a line: <c>advice.ingredient.&lt;style&gt;</c> for a
        /// style band, <c>advice.type.&lt;type&gt;</c> for a band with no style.</summary>
        private static Line NameLineOf(RatioRequirement band) =>
            band.IsStyleBand ? Line.Of("advice.ingredient." + band.Style)
                : Line.Of("advice.type." + band.Type.ToString().ToLowerInvariant());

        /// <summary>How a drinker names a preparation out loud.</summary>
        private static string SpokenName(PreparationDefinition prep)
        {
            switch (prep.Id)
            {
                case "ice": return "the ice";
                case "lemon_twist": return "a lemon twist";
                case "salt_rim": return "a salted rim";
                case "sugar_rim": return "a sugared rim";
                default: return prep.Name.ToLowerInvariant();
            }
        }

        /// <summary>
        /// What a band is CALLED to somebody drinking it. The style, with its underscores
        /// opened out — "triple_sec" is a key, "triple sec" is a word — and the type's own
        /// name where a band has no style, which is only the derived ones this class already
        /// refuses. Kept here rather than on the band because it is a phrasing decision, and
        /// the band is a rule.
        /// </summary>
        private static string NameOf(RatioRequirement band) =>
            band.IsStyleBand ? band.Style.Replace('_', ' ')
                : band.Type.ToString().ToLowerInvariant();
    }
}
