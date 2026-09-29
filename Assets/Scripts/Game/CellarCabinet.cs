using System;
using System.Collections.Generic;
using LastCall.Core;

namespace LastCall.Game
{
    /// <summary>
    /// THE CELLAR IS A DISPLAY CABINET (2026-09-28, the author: "Mahzen tasarımı = C · Art Deco vitrin"). Ten fixed
    /// niches under the counter, five across and two high, ONE family a niche - the spirits on the upper row, what they
    /// are mixed with on the lower - mirrored about a wider stepped centre column that holds the whisky and the juices:
    ///
    ///     VODKA     GIN      WHISKY    RUM            TEQUILA
    ///     LIQUEURS  SYRUPS   JUICES    SODA &amp; TONIC   MIXERS
    ///
    /// A niche never moves and a family never splits: the old planogram cut three bays per board where a run fitted
    /// best (DiegeticStage.PlanCellar, 2026-09-28 morning), so the gin and the tequila stood half in one bay and half in
    /// the next and a bought bottle could shift a whole family over a post. Here the only thing a new bottle changes is
    /// the air between its neighbours.
    ///
    /// Pure arithmetic over the drawing's own numbers (Tools/cellar_cabinet/cabinet.py draws the body to exactly these),
    /// so the stage, the HUD and the EditMode suite read one plan. Every number is in the COUNTER ART's pixels - one art
    /// pixel is one stage unit and two screen pixels at 720p - with art x counted from the drawing's left edge as the
    /// stage places it natively centred, and art rows counted down from the counter's top edge.
    /// </summary>
    public static class CellarCabinet
    {
        public const int Columns = 5, Rows = 2, Niches = Columns * Rows;

        /// <summary>The counter drawing's width, and the rows the cabinet's body spans under the author's slab (the slab is
        /// rows 0..64 of counter.png; the body stops at its last row, 249 - "Tezgah boyutunu değiştirme").</summary>
        public const int ArtWidth = 638, BodyTop = 65, BodyBottom = 249;
        public const int BodyHeight = BodyBottom - BodyTop + 1;

        /// <summary>The carcass's outer columns: the body is opaque from here to <see cref="CarcassRight"/>, which covers
        /// the whole of the old drawing's body where the stage stands the two.</summary>
        public const int CarcassLeft = 4, CarcassRight = 633;

        /// <summary>Each column's opening, left edge and width. The door (592 wide, centred) covers 23..614 exactly;
        /// the centre column is the wider feature.</summary>
        public static readonly int[] NicheX = { 23, 139, 255, 393, 509 };
        public static readonly int[] NicheW = { 106, 106, 128, 106, 106 };

        /// <summary>Per row: the opening's first row; the name plate's rows; the lamp's row under the plate (a bottle's top
        /// is the row after it); the row a bottle's foot stands on (unchanged from the three-bay counter, so the drawer's
        /// travel and every bench line keep); the board's front edge's last row.</summary>
        public static readonly int[] OpenTop = { 66, 156 };
        public static readonly int[] PlateTop = { 67, 157 };
        public static readonly int[] PlateBottom = { 77, 167 };
        public static readonly int[] LampRow = { 78, 168 };
        public static readonly int[] FootRow = { 143, 233 };
        public static readonly int[] FloorTop = { 138, 228 };
        public static readonly int[] LipBottom = { 148, 238 };

        /// <summary>The name plate's width per column - holds a 16 px word of about twelve characters.</summary>
        public static readonly int[] PlateW = { 72, 72, 88, 72, 72 };

        /// <summary>Clear air between a niche's side and its outermost bottle, and the air between two bottles: never
        /// under one pixel (the author, 2026-08-25: "aralarında 1 pixel kalıcak kadar yakınlaşsınlar ama boyutları
        /// değişmesin") and never over six, so a family of two still reads as a family.</summary>
        public const int Margin = 4, GapMin = 1, GapMax = 6;

        /// <summary>The family each niche holds, board-major: index = board * <see cref="Columns"/> + column.</summary>
        public static readonly string[] Families =
            { "vodka", "gin", "whiskey", "rum", "tequila", "liqueur", "syrup", "juice", "soda", "mixer" };

        public static int Board(int niche) => niche / Columns;
        public static int Column(int niche) => niche % Columns;

        /// <summary>Inside a family, the order a bartender lines them up: the juices citrus first, the fizz by how often
        /// it is reached for, the liqueurs aperitif before sweet. A spirit has no rank here and goes by its tier.</summary>
        private static readonly string[] StyleRank =
            { "lemon", "lime", "orange", "pineapple", "cranberry",
              "soda", "tonic", "ginger", "cola", "energy",
              "syrup", "grenadine",
              "vermouth", "amaro", "triple_sec", "coffee_liqueur" };

        private static readonly string[] Spirits =
            { IngredientCategories.Vodka, IngredientCategories.Gin, IngredientCategories.Whiskey,
              IngredientCategories.Rum, IngredientCategories.Tequila };

        /// <summary>
        /// The family a bottle stands with: the category for the spirits, LIQUEURS for any other category the data
        /// names, and the type's plain word for the rest. SODA &amp; TONIC is its own niche now - soda water and tonic,
        /// the two a highball is built on - and the cola, the ginger beer and the energy drink stand under MIXERS.
        /// </summary>
        public static string Family(IngredientCard card)
        {
            string cat = card?.Info?.Category;
            if (!string.IsNullOrEmpty(cat) && cat != IngredientCategories.Mixer && cat != IngredientCategories.Juice)
                return Array.IndexOf(Spirits, cat) >= 0 ? cat : "liqueur";
            if (cat == IngredientCategories.Juice) return "juice";
            switch (card?.Type ?? IngredientType.Spirit)
            {
                case IngredientType.Sweet: case IngredientType.Bitter: return "syrup";
                case IngredientType.Sour: return "juice";
            }
            string style = card?.Info?.Style;
            return style == "soda" || style == "tonic" ? "soda" : "mixer";
        }

        /// <summary>The niche a family stands in; anything the table does not know stands with the liqueurs.</summary>
        public static int NicheOf(string family)
        {
            int n = Array.IndexOf(Families, family);
            return n >= 0 ? n : Array.IndexOf(Families, "liqueur");
        }

        public static int NicheOf(IngredientCard card) => NicheOf(Family(card));

        /// <summary>
        /// The shelf order: niche, then style, then tier - so a family stands from the well bottle to the top shelf,
        /// left to right, the order the 2026-09-28 planogram already taught (tier 1 to 4). Sort by this, then by name.
        /// </summary>
        public static int ShelfKey(IngredientCard card)
        {
            int style = Array.IndexOf(StyleRank, card?.Info?.Style ?? "");
            int tier = Math.Max(0, Math.Min(9, card?.Info?.Tier ?? 1));
            return NicheOf(card) * 1000 + (style < 0 ? 99 : style) * 10 + tier;
        }

        /// <summary>Where one bottle stands: the left edge of its drawing, its drawn width and the centre between them
        /// (art px), its niche and the row its foot stands on. <see cref="Overflow"/> when its family is wider than the
        /// niche - the EditMode suite fails on that, because the answer is a data decision and never a smaller bottle.</summary>
        public struct Slot
        {
            public float Left, Width;
            public int Niche, Foot;
            public bool Overflow;
            public float Centre => Left + Width * 0.5f;
        }

        /// <summary>
        /// Stands a shelved run of bottles (sorted by <see cref="ShelfKey"/>, which is what the HUD hands the stage) in
        /// their niches. Each family is centred under its own lamp, the air between its bottles shared out evenly and
        /// kept to whole pixels - a lone bottle stands in the middle of its niche like a featured one, a full niche
        /// closes to a single pixel. Nothing is ever dropped: a family wider than its niche still stands, at one pixel,
        /// running into the niche's margin, and says so. A niche's bottles stand left to right in the order they were
        /// handed in, together even if the list did not keep them together - two runs printed over each other in one
        /// niche is the one outcome worse than a wrong order.
        /// </summary>
        public static Slot[] Plan(IReadOnlyList<float> widths, IReadOnlyList<int> niches)
        {
            int n = widths?.Count ?? 0;
            var slots = new Slot[n];
            var members = new List<int>[Niches];
            for (int i = 0; i < n; i++)
            {
                int niche = Clamp(niches != null && i < niches.Count ? niches[i] : 0, 0, Niches - 1);
                (members[niche] ??= new List<int>()).Add(i);
            }
            for (int niche = 0; niche < Niches; niche++)
            {
                var run = members[niche];
                if (run == null) continue;
                int col = Column(niche), board = Board(niche);
                float usable = NicheW[col] - 2 * Margin;
                float sum = 0f;
                foreach (int k in run) sum += widths[k];
                int count = run.Count;
                float gap = count < 2 ? 0f : Math.Max(GapMin, Math.Min(GapMax, (float)Math.Floor((usable - sum) / (count - 1))));
                float span = sum + gap * (count - 1);
                bool over = span > usable + 0.001f;
                float left = NicheX[col] + Margin + (float)Math.Floor((usable - span) * 0.5f);
                foreach (int k in run)
                {
                    slots[k] = new Slot { Left = left, Width = widths[k], Niche = niche, Foot = FootRow[board], Overflow = over };
                    left += widths[k] + gap;
                }
            }
            return slots;
        }

        /// <summary>A niche's rectangle, art px inclusive: its opening from the first row to its board's lip. The lit and
        /// the pilot bodies differ only inside these ten, so a niche lights by swapping this one rectangle.</summary>
        public static void NicheRect(int niche, out int x0, out int y0, out int x1, out int y1)
        {
            int col = Column(niche), board = Board(niche);
            x0 = NicheX[col];
            x1 = NicheX[col] + NicheW[col] - 1;
            y0 = OpenTop[board];
            y1 = LipBottom[board];
        }

        /// <summary>A niche's name plate: left column, top row, width and height (art px).</summary>
        public static void PlateRect(int niche, out int x0, out int y0, out int w, out int h)
        {
            int col = Column(niche), board = Board(niche);
            w = PlateW[col];
            x0 = NicheX[col] + (NicheW[col] - w) / 2;
            y0 = PlateTop[board];
            h = PlateBottom[board] - PlateTop[board] + 1;
        }

        /// <summary>The centre of a niche, art x.</summary>
        public static float NicheCentre(int niche) => NicheX[Column(niche)] + NicheW[Column(niche)] * 0.5f;

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
