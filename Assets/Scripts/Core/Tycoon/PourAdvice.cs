using System;

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

        public PourNote(bool flawless, string ingredient, int direction, double miss, string sentence)
        {
            Flawless = flawless;
            Ingredient = ingredient ?? string.Empty;
            Direction = direction;
            Miss = miss;
            Sentence = sentence;
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
        public static PourNote For(RecipeDefinition recipe, GlassContents glass,
            Func<string, IngredientCard> lookup, ServingSpec spec = null)
        {
            if (glass == null) return default;

            // THE MISSING HALF IS COMPUTED FIRST and does not depend on the pour: it is the
            // one thing here a derived recipe can still get wrong.
            string missing = MissingLine(spec, glass);
            var silent = missing == null ? default
                : new PourNote(false, string.Empty, 0, 0, missing);

            // A PINT HAS A CRAFT TOO, AND IT IS THE HEAD (GDD 21 §10; 2026-09-04, the
            // author: "Drinking... yerine içerken teslim edilen alkol ile ilgili bilgi
            // versin"). Beer takes no ratio bands at all, so the pour half above has nothing
            // to say about it — and on an early bar the draught and the neat pour are a real
            // share of the orders, which is exactly how a ticket ends up reading DRINKING…
            // over a drinker who was in fact handed a badly pulled pint. The head is a
            // measured band (TapPour), so it coaches the same way an ingredient does: one
            // thing, one direction, no number.
            if (glass.HasPreparation(Preparations.Draught.Id))
                return Head(glass, missing);

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
                    missing == null ? FlawlessLine : FlawlessStem + " " + missing);

            string name = NameOf(bands[worst]);
            if (string.IsNullOrEmpty(name)) return silent;

            int direction = signed > 0 ? -1 : 1;      // poured too much → pour less
            string degree = worstMiss <= TouchMiss ? "A touch"
                : worstMiss <= LittleMiss ? "A little"
                : "A lot";
            string way = direction < 0 ? "less" : "more";
            string line = degree + " " + way + " " + name + " next time.";
            if (missing != null) line += " " + missing;
            return new PourNote(false, name, direction, worstMiss, line);
        }

        /// <summary>
        /// THE PINT'S OWN NOTE. Its band is <see cref="TapPour.GoodHeadMin"/>..
        /// <see cref="TapPour.GoodHeadMax"/> of the glass — a window rather than a point, so
        /// "how far off" is the distance OUTSIDE it and anything inside is simply right. The
        /// degrees are half the ratio ladder's because the band itself is: twelve points of
        /// glass against a ratio box's twenty.
        /// </summary>
        public const double HeadTouch = 0.03, HeadLittle = 0.06;

        private static PourNote Head(GlassContents glass, string missing)
        {
            double head = glass.Capacity > 0 ? glass.Head / glass.Capacity : 0;
            double over = head - TapPour.GoodHeadMax, under = TapPour.GoodHeadMin - head;
            if (over <= 0 && under <= 0)
                return new PourNote(missing == null, string.Empty, 0, 0,
                    missing == null ? PulledWellLine : PulledWellStem + " " + missing);

            bool tooMuch = over > 0;
            double miss = tooMuch ? over : under;
            string degree = miss <= HeadTouch ? "A touch" : miss <= HeadLittle ? "A little" : "A lot";
            string line = degree + (tooMuch ? " less" : " more") + " head next time.";
            if (missing != null) line += " " + missing;
            return new PourNote(false, "head", tooMuch ? -1 : 1, miss, line);
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
        private static string MissingLine(ServingSpec spec, GlassContents glass)
        {
            if (spec == null || spec.IsPlain || glass == null) return null;
            string list = null;
            int n = 0;
            foreach (var want in spec.Garnishes)
            {
                if (want == null || glass.HasPreparation(want.Id)) continue;
                string said = SpokenName(want);
                n++;
                list = list == null ? said : list + "|" + said;
            }
            if (n == 0) return null;
            // "the ice", "the ice and a lemon twist", "the ice, a lemon twist and a salted
            // rim" — a spoken list, not a comma-joined dump of ids.
            var parts = list.Split('|');
            string joined = parts[0];
            for (int i = 1; i < parts.Length; i++)
                joined += (i == parts.Length - 1 ? " and " : ", ") + parts[i];
            return "I asked for " + joined + " as well.";
        }

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
