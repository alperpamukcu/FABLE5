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
    /// <summary>
    /// The first playable of the tycoon loop (PLAN_tycoon_pivot P3): a functional, plain
    /// overlay that drives <see cref="TycoonRun"/> — a seat row with order bubbles and
    /// patience clocks, a top bar with the till and the day's satisfaction, and the
    /// day-end invoice with refills, brands, stools and the next day.
    ///
    /// Deliberately interim: pouring still happens by clicking shelf bottles (the shaker
    /// flow is P4), and the seat row is UI panels, not animated patrons (P8). The point is
    /// to make the loop the *played* loop so the sim and the hands can start tuning it.
    /// </summary>
    public sealed class TycoonHud : MonoBehaviour
    {
        [SerializeField] private Font bodyFont;
        [SerializeField] private Font displayFont;
        /// <summary>The shop's own face (the author, 2026-08-07: "yeni bir font bul
        /// kesinlikle"). Silkscreen BOLD — the same pixel grid the bar is set in, with the
        /// weight the white page needs. Falls back to the body face if unassigned.</summary>
        [SerializeField] private Font shopFont;
        [SerializeField] private DiegeticStage stage;

        private GameBootstrap _bootstrap;
        private TycoonRun Run => _bootstrap != null ? _bootstrap.Tycoon : null;

        private Font _body;
        private Font _display;
        private Font _shop;

        // top bar
        private Text _dayText;
        private Text _moneyText;
        private Text _crowdText;
        /// <summary>The bar's standing (v5 P12): the average, then five filled/empty stars.
        /// Replaces the TONIGHT satisfaction bar (D3) — reputation is what the player steers
        /// by now, and it carries between nights instead of resetting every morning.</summary>
        private Text _ratingText;
        private RectTransform _starsFill;
        private Text _clockText;          // the hour, the board's biggest reading
        private Image _clockRule, _tillRule, _standingRule;   // each plaque's lit base
        private readonly Image[] _ratingStars = new Image[BarRating.MaxStars];

        // Seats at the counter (GDD 24 §4, 2026-07-22): customers sit along the bar as
        // head-and-shoulders busts, not in a bottom strip. Each bust rises/slides into its
        // stool when its patron arrives, wears a floating order tag, and is the click target.
        private const int SeatSlots = 6;
        // HUD-space y (from bottom) of the bar top, where a customer's body is cut off. DERIVED
        // from the stage rather than copied: the HUD is 1280x720 against the stage's 640x360, so
        // it is exactly twice the stage's own line. It used to be a hand-written 279, which was
        // right for the old counter art and 19 units too high for the new one — every customer
        // floated that far above the bar (2026-07-29).
        private const float StageToHud = 720f / 360f;
        private const float CounterLineY = DiegeticStage.CounterTopY * StageToHud;
        private const float BustW = 108f;

        /// <summary>How wide an order ticket may grow before its order line wraps instead
        /// (2026-08-02). Wide enough for most drink names, narrow enough that five tickets
        /// across the counter do not overlap each other.</summary>
        private const float TagMaxW = 236f;
        private const float BustH = 128f;
        private const float WalkSpeed = 340f;       // walk-in speed (ref px/s) — slightly slower, per the notes (P15)
        private const float ExitSpeed = 560f;       // walk-out speed (ref px/s), back off the right edge
        private const float OffscreenMargin = 150f; // how far past the right edge they start/finish
        private const float OrderAnimSeconds = 2.4f;               // the one-shot "placing the order" beat
        private const float DrinkSipSeconds = 2.6f, DrinkHoldSeconds = 1.8f;   // one sip cycle (×3 = the savour)

        // The animated customer (2026-07-23): a full-body pixel sprite shown from about the waist
        // up, with the counter clipping the legs. Frames load from Resources/Patron/<clip>.
        // Re-aligned off the ART's own bbox (2026-07-31, the author's note): the figure spans
        // y 13–171 of the 180px canvas, so at 350 the head tops out at -FootDrop+324.7 and the
        // feet reach -FootDrop+17.5. FootDrop 150 crops ~43% of the figure below the counter
        // (the same waist as before), and the window ends 5px over the head — the gauge and
        // the tag now HUG the head instead of floating 28px above it.
        private const float CharSize = 350f;       // the character image, a touch bigger again
        private const float CharWiden = 1.18f;     // stretch a touch wider — the sprite is lanky for the bar
        private const float CharWinH = 180f;       // ends just over the head (art-measured)
        private const float CharFootDrop = 150f;   // same waist crop at 350 (art-measured)
        private enum PatronClip { Idle, Order, Drink, Walk, Cheer, Upset }
        private const float ReactSeconds = 1.15f;   // the one-shot reaction beat before they go

        /// <summary>
        /// One person who might sit down: six clips and the row their head occupies inside
        /// the 180px canvas. The head row is not decoration — the order ticket and the
        /// patience gauge hang off it, and ten people of ten different heights cannot share
        /// one hardcoded window. Measured off the shipped idle frames by
        /// scratchpad/driver/patrons10_ship.py, which is also what stood them on a common
        /// foot line in the first place.
        /// </summary>
        private sealed class PatronLook
        {
            public string Slug;
            public Dictionary<PatronClip, Sprite[]> Clips;
            /// <summary>This person's licence photo, cropped out of their own idle frame
            /// by scratchpad/driver/patron_faces.py. Derived, never drawn: a face cannot
            /// drift from the body it belongs to if it IS the body.</summary>
            public Sprite Face;
            public float HeadY;
            /// <summary>What the bar must be worth before this person comes in.</summary>
            public float Stars;
            /// <summary>Where that head lands in HUD units, from the rig's own constants:
            /// the sprite is drawn CharSize tall off a 180 canvas and pushed down by
            /// CharFootDrop so the counter takes the legs.</summary>
            public float HeadTop => (180f - HeadY) * (CharSize / 180f) - CharFootDrop;
        }

        /// <summary>
        /// The cast, and the head row measured for each. v7 is first and keeps its old
        /// folder layout (Patron/&lt;clip&gt;); the ten cast on 2026-08-09 live under
        /// Patron/&lt;slug&gt;/&lt;clip&gt;. A look that fails to load is dropped rather than
        /// drawn as a hole — a bench without the art still runs.
        /// </summary>
        /// <summary>
        /// WHO CAN WALK IN, and what the room has to be worth first.
        ///
        /// The star gate is the point (the author, 2026-08-10): a bar nobody talks about
        /// gets the people who will drink anywhere, and the room fills out as its standing
        /// climbs. It is a presentation rule, not a rules-layer one — which face is drawn
        /// cannot change what anybody orders or pays — so it lives here with the art it
        /// governs, and the cast can grow to a hundred without touching Core.
        ///
        /// Head rows are measured off each shipped idle frame; they place the order ticket.
        /// </summary>
        private static readonly (string Slug, float HeadY, float Stars)[] PatronCast =
        {
            (null, 13f, 0f),            // Bar Patron v7 — the rig's own reference
            // Nobody special: the people who drink in any bar that is open.
            ("nightnurse", 5f, 0f), ("courier", 36f, 0f), ("undone", 28f, 0f),
            ("dockman", 25f, 0f), ("bearded", 26f, 0f),
            // Word is getting round.
            ("coder", 31f, 1.5f), ("inked", 22f, 1.5f), ("studentm", 31f, 1.5f),
            ("nerd", 36f, 1.5f), ("bikeryoung", 25f, 1.5f),
            // A bar worth crossing town for.
            ("wanderer", 15f, 2.5f), ("studentf", 13f, 2.5f), ("gothgirl", 30f, 2.5f),
            ("bikerold", 26f, 2.5f),
            // The room people dress up for.
            ("glam", 9f, 3.5f), ("execwoman", 5f, 3.5f), ("execman", 23f, 3.5f),

            // ── the 2026-08-10 casting ──────────────────────────────────────────
            // Moustaches and beards, three bald men, four women in platinum, violet,
            // teal and copper, and the goths redrawn.
            ("walrus", 22f, 0f), ("barber", 19f, 0f), ("bouncer", 37f, 0f),
            ("ember", 8f, 0f),
            ("lumber", 16f, 1.5f), ("cueball", 31f, 1.5f), ("violet", 15f, 1.5f),
            ("teal", 22f, 1.5f), ("gothpunk", 0f, 1.5f),
            ("profess", 23f, 2.5f), ("chrome", 40f, 2.5f), ("platina", 26f, 2.5f),
            ("gothqueen", 5f, 2.5f),
        };
        /// <summary>
        /// WHO A DRINKER IS, on paper. A name, an age, a country and its flag — chosen
        /// against the DRAWING rather than at random, so the age matches the face the
        /// artist drew and the name matches how it reads (2026-08-10). Mostly American,
        /// because that is the bar's world, with eight passports from elsewhere where the
        /// picture itself argued for one: a blond beard and blue eyes is Swedish, a
        /// three-piece with a pocket square is a London suit, a waxed handlebar over a
        /// waistcoat is the Turkish barber's own uniform.
        ///
        /// Names are taken from the MIDDLE of each country's common-name lists rather than
        /// the top: the most famous name in a country belongs to a celebrity, the tenth
        /// belongs to a person.
        /// </summary>
        private sealed class Papers
        {
            public readonly string Name;
            public readonly int Age;
            public readonly string Country;
            public readonly string Iso;
            public Papers(string name, int age, string country, string iso)
            { Name = name; Age = age; Country = country; Iso = iso; }
        }

        private static readonly Dictionary<string, Papers> PatronPapers =
            new Dictionary<string, Papers>
        {
            { "", new Papers("Miles Corrigan", 26, "United States", "us") },
            { "nightnurse", new Papers("Marilou Cabrera", 37, "Philippines", "ph") },
            { "courier", new Papers("Danny Ferraro", 23, "United States", "us") },
            { "undone", new Papers("Craig Delaney", 46, "United States", "us") },
            { "dockman", new Papers("Dennis Wojcik", 63, "United States", "us") },
            { "bearded", new Papers("Fredrik Ohlsson", 34, "Sweden", "se") },
            { "walrus", new Papers("Kurt Ostrowski", 57, "United States", "us") },
            { "barber", new Papers("Serkan Aydemir", 33, "Turkey", "tr") },
            { "bouncer", new Papers("Marcus Boyd", 42, "United States", "us") },
            { "ember", new Papers("Meredith Nolan", 34, "United States", "us") },
            { "coder", new Papers("Elliot Brandt", 24, "United States", "us") },
            { "inked", new Papers("Rowan Pike", 33, "United States", "us") },
            { "studentm", new Papers("Trevor Hanley", 21, "United States", "us") },
            { "nerd", new Papers("Spencer Kaplan", 25, "United States", "us") },
            { "bikeryoung", new Papers("Shane Mercer", 26, "United States", "us") },
            { "lumber", new Papers("Dustin Kilgore", 43, "United States", "us") },
            { "cueball", new Papers("Neil Prentiss", 55, "United States", "us") },
            { "violet", new Papers("Sabrina Voss", 24, "United States", "us") },
            { "teal", new Papers("Piper Landry", 23, "United States", "us") },
            { "gothpunk", new Papers("Erika Vaughn", 24, "United States", "us") },
            { "wanderer", new Papers("Joost Kramer", 25, "Netherlands", "nl") },
            { "studentf", new Papers("Brooke Whitaker", 20, "United States", "us") },
            { "gothgirl", new Papers("Marissa Vogel", 24, "United States", "us") },
            { "bikerold", new Papers("Duane Halloran", 64, "United States", "us") },
            { "profess", new Papers("Ulrich Brenner", 66, "Germany", "de") },
            { "chrome", new Papers("Andre Whitlock", 36, "United States", "us") },
            { "platina", new Papers("Paulina Nowicka", 33, "Poland", "pl") },
            { "gothqueen", new Papers("Genevieve Marsh", 34, "United States", "us") },
            { "glam", new Papers("Serena Fontana", 35, "Italy", "it") },
            { "execwoman", new Papers("Vivian Marchetti", 44, "United States", "us") },
            { "execman", new Papers("Graham Sedgwick", 54, "United Kingdom", "gb") },
        };

        /// <summary>This drinker's papers, or null for a look nobody has written up.</summary>
        private static Papers PapersFor(PatronLook look) =>
            look != null && PatronPapers.TryGetValue(look.Slug ?? "", out var p) ? p : null;

        private readonly List<PatronLook> _looks = new List<PatronLook>();
        private RectTransform _hudRoot;            // the canvas rect — the screen's right edge for entrances

        private sealed class SeatView
        {
            public RectTransform Root;       // the customer + tag, positioned at the counter (click target)
            public CanvasGroup Group;        // fades them in as they walk up
            public Image Portrait;           // the animated character image (inside the counter mask)
            public RectTransform Tag;        // the floating order ticket above the head
            public Image TagBg;
            public Text Name;
            public Text Wants;
            public Text Order;
            public Image Icon;               // the ordered drink, drawn by DrinkIcon (v5 P13)
            public Image PatienceFill;
            public float SeatX;              // this stool's resting x
            public DirtyGlass Dirty;         // the empty glass left on this stool (D2)
            public RectTransform DirtyProp;  // its clickable prop on the counter
            public float WalkT;              // 0..1 walk-in progress
            public bool Exiting;             // playing the leave animation
            public float ExitT;              // 0..1 leave progress
            public bool ExitStorm;           // stormed off (angry exit) vs served (calm)
            public CustomerVisit Visit;      // who is assigned to this stool (stable until they leave)
            public float AnimClock;          // running time for the looping clips (idle, walk)
            public bool WasOrdered;          // edge-detect the deciding→ordered moment
            public float OrderAnimLeft;      // remaining "placing the order" one-shot time
            public float DrinkT;             // time since they started drinking
            public float ReactLeft;          // remaining departure-reaction one-shot time
            public PatronClip ReactClip;     // Cheer or Upset, chosen from their satisfaction
            public PatronLook Look;          // who is sitting here, and how tall they are
            public RectTransform Gauge;      // the patience bar, re-hung off their own head
        }
        private readonly List<SeatView> _seats = new List<SeatView>();

        // The finished drink on the counter (GDD 24 §3, 2026-07-22): a glass you drag onto a
        // customer to serve, carried with a heavy, springy AAA feel.
        /// <summary>The bin on the counter (v5 P13 / C7). A drink is thrown away by carrying it
        /// there, the same verb that serves it — the BIN GLASS button is gone.</summary>
        private RectTransform _binProp;
        private Image _binImage;

        /// <summary>How big the bin is drawn. The art is 166×190, so this is 1.3× on both axes
        /// — one scale, because a bin squeezed on one axis stops being a cylinder. It was 1.9×
        /// and read as furniture rather than as a bin standing in the corner (the author,
        /// 2026-08-04); the mouth is still wider than the carried glass, which is the only size
        /// it actually has to beat.</summary>
        private const float BinW = 184f, BinH = 210f;

        private RectTransform _drinkGlass;
        private Image _drinkGlassLiquid;
        private Image _drinkGlassArt;
        private GlasswareDefinition _drinkGlassware;
        private int _drinkGlassTier = 1;
        private Image _drinkGlassBack;
        private RectTransform _glassRack;
        /// <summary>The room, asked where its shelf compartments are.</summary>
        private DiegeticStage _stage;
        private const float CarriedGlassHeight = 116f;
        private bool _glassGrabbed;
        private Vector2 _glassGrabOffset;
        private Vector2 _glassPos, _glassVel;
        private float _glassAngle, _glassAngVel;
        private Vector2 _glassHome;
        private bool _glassShown;
        private const float GlassStiffness = 130f;   // spring to the cursor
        private const float GlassDamping = 12f;
        private const float GlassAngStiffness = 90f;  // spring the tilt back upright
        private const float GlassAngDamping = 9f;

        // day end — two steps now (the author, 2026-08-01): first the bill alone, then
        // the market, each with its own verb on the same button.
        private RectTransform _dayEndPanel;
        private Text _invoiceText;
        private RectTransform _offerRow;
        private RectTransform _openTomorrow;
        private Text _bannerText;
        private RectTransform _dayEndBill, _dayEndTablet;
        private Text _openTomorrowLabel, _dayEndTitle;
        private int _dayEndStep;
        private RectTransform _cardTarget;

        // ledger history (GDD 24 §7, 2026-07-22): the register opens the book of past days.
        private RectTransform _ledgerPanel;
        private RectTransform _ledgerRows;

        // ID card (GDD 24 §5): the licence you read a customer by. Emotion→recipe pivot
        // (2026-07-22): it now shows the drink's RECIPE and the garnishes they want, not moods.
        private RectTransform _idRoot;
        private Image _idPhoto;
        private Text _idName, _idAgeFrom, _idRel, _idIntent, _idOrder, _idOrderParts, _idRates, _idRatesLabel;
        private Text _idRelLabel, _idIntentLabel;
        private RectTransform _idRecipeTip;
        private RectTransform _idRecipeTipBody;
        private RectTransform _idPrefRow;

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

        /// <summary>One line of a recipe's spec card: an ingredient with its exact share,
        /// or a plain note (the prep, the fill, the glass).</summary>
        private readonly struct SpecRow
        {
            public readonly string Style;    // null on a note row
            public readonly string Label;
            public readonly string Amount;   // "" on a note row
            public readonly int MinTier;
            public readonly bool Hint;       // a footnote: half height, half size

            public SpecRow(string style, string label, string amount = "", int minTier = 1,
                bool hint = false)
            { Style = style; Label = label; Amount = amount; MinTier = minTier; Hint = hint; }
        }

        /// <summary>
        /// A recipe as a SPEC CARD: the prep, then one pour to a line, then the fill and the
        /// glass. The vertical form is the readable one (the author, 2026-08-02) — a run-on
        /// "GIN 45–65% · LEMON 20–40% · SYRUP 10–30%" wraps mid-number and has to be parsed;
        /// a column is read.
        ///
        /// Each pour shows the EXACT share to build at, not its tolerance band: the bands are
        /// how forgiving the matcher is, which is the game's business and not an instruction.
        /// A player told "45–65% gin" has to pick a number anyway, so the card picks it —
        /// <see cref="RatioRecipeMatcher.IdealPour"/>, which is inside every band and fills
        /// the glass. Built once and shared, so the licence and the book cannot drift.
        /// </summary>
        private static List<SpecRow> RecipeSpecRows(RecipeDefinition r)
        {
            var rows = new List<SpecRow> { new SpecRow(null, PrepWord(r)) };
            var bands = r.RatioRequirements;
            var shown = WholePercents(RatioRecipeMatcher.IdealPour(r));
            for (int i = 0; i < bands.Count; i++)
            {
                var b = bands[i];
                rows.Add(new SpecRow(
                    b.IsStyleBand ? b.Style : null,
                    b.IsStyleBand ? b.Style.Replace('_', ' ').ToUpperInvariant() : TypeWord(b.Type),
                    $"{shown[i]}%",
                    b.MinTier));
            }
            foreach (var b in bands)
            {
                if (b.IsStyleBand) continue;
                string hint = TypeHint(b.Type);
                if (hint != null) rows.Add(new SpecRow(null, hint, hint: true));
            }
            if (r.MinFill > 0) rows.Add(new SpecRow(null, "FILL", $"{r.MinFill * 100:0}%+"));
            if (!string.IsNullOrEmpty(r.GlassId))
                rows.Add(new SpecRow(null, r.GlassId.ToUpperInvariant()));
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

        /// <summary>Whether the bar can actually pour this band right now — the style, at the
        /// rung the recipe asks for, with something left in the bottle.</summary>
        private bool InStock(string style, int minTier)
        {
            var run = Run;
            if (run == null || string.IsNullOrEmpty(style)) return false;
            foreach (var b in run.Shelf.Bottles)
                if (!b.IsEmpty && b.Ingredient.Info != null && b.Ingredient.Info.Style == style
                    && b.Ingredient.Info.Tier >= minTier) return true;
            return false;
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
            float width, string note = null)
        {
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);

            Color ink = dark ? UITheme.Cream[4] : new Color(0.20f, 0.13f, 0.07f);
            Color quiet = dark ? new Color(0.61f, 0.58f, 0.66f) : new Color(0.52f, 0.44f, 0.36f);
            Color figure = dark ? UITheme.Cyan[3] : new Color(0.10f, 0.06f, 0.02f);
            Color prepInk = dark ? UITheme.Magenta[3] : new Color(0.11f, 0.37f, 0.40f);
            Color have = dark ? new Color(1f, 1f, 1f, 0.07f) : new Color(0.36f, 0.22f, 0.08f, 0.09f);
            Color miss = dark ? new Color(0.61f, 0.58f, 0.66f, 0.55f) : new Color(0.52f, 0.44f, 0.36f, 0.6f);

            var rows = RecipeSpecRows(r);
            float y = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                var spec = rows[i];
                bool ingredient = spec.Style != null;
                bool stocked = ingredient && InStock(spec.Style, spec.MinTier);

                float rowH = spec.Hint ? SpecHintH : SpecRowH;
                var line = NewRect($"S{i}", host);
                Place(line, new Vector2(0, 1), new Vector2(width, rowH), Vector2.zero);
                line.pivot = new Vector2(0, 1);
                line.anchoredPosition = new Vector2(0, -y);
                y += rowH;

                // The frame is the "you have this" tell: a lit slab behind the row.
                if (stocked)
                {
                    var slab = line.gameObject.AddComponent<Image>();
                    slab.color = have;
                    slab.raycastTarget = false;
                }

                float textX = 2f;
                if (ingredient)
                {
                    // Style-keyed art is the RETIRED shelf; the live bottle for this style
                    // hangs on the run's own shelf, and a v3 bottle's icon is its FRONT
                    // plate — the one with the label — never the interior back.
                    var live = Run != null
                        ? LastCall.Core.Market.FindByStyle(Run.Shelf, spec.Style) : null;
                    var art = live != null
                        ? ItemArt.Bottle(live.Ingredient)   // the composed flat, label and all
                        : ItemArt.Bottle(spec.Style);
                    if (art != null)
                    {
                        var icon = NewRect("B", line);
                        Place(icon, new Vector2(0, 0.5f), new Vector2(SpecRowH - 3f, SpecRowH - 3f),
                            new Vector2(3f, 0));
                        var img = icon.gameObject.AddComponent<Image>();
                        img.sprite = art;
                        img.preserveAspect = true;
                        img.raycastTarget = false;
                        img.color = stocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    }
                    textX = SpecRowH + 4f;
                }

                // The CONTENTS are the text face: lighter and narrower than the name above
                // them, so the card has a title and a body rather than one wall of capitals —
                // and so COFFEE LIQUEUR fits beside its share instead of running into it.
                var label = NewText("L", line, _body, spec.Hint ? 8 : 16, TextAnchor.MiddleLeft,
                    ingredient ? (stocked ? ink : miss) : (i == 0 ? prepInk : quiet));
                Place(label.rectTransform, new Vector2(0, 0.5f),
                    new Vector2(width - textX - SpecAmountW - 6f, rowH), Vector2.zero);
                label.rectTransform.pivot = new Vector2(0, 0.5f);
                label.rectTransform.anchoredPosition = new Vector2(textX, 0);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.raycastTarget = false;
                label.text = spec.Label + (spec.MinTier > 1 ? $"  T{spec.MinTier}+" : "");

                if (spec.Amount.Length > 0)
                {
                    var amount = NewText("A", line, _display, 16, TextAnchor.MiddleRight,
                        ingredient && !stocked ? miss : figure);
                    Place(amount.rectTransform, new Vector2(1, 0.5f), new Vector2(SpecAmountW, rowH),
                        new Vector2(-2, 0));
                    amount.horizontalOverflow = HorizontalWrapMode.Overflow;
                    amount.raycastTarget = false;
                    amount.text = spec.Amount;
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

        /// <summary>How tall one line of a spec card is — the bottle icons are square to it.</summary>
        private const float SpecRowH = 20f;

        /// <summary>A footnote row — the line that spells out a word like SPIRIT.</summary>
        private const float SpecHintH = 13f;

        /// <summary>The share column. Wide enough for "100%" in the display face, which is
        /// four whole 16px cells — the old 52 clipped it and pushed COFFEE LIQUEUR into it.</summary>
        private const float SpecAmountW = 70f;

        /// <summary>How wide the hover spec is, beside the card.</summary>
        private const float TipW = 252f;

        /// <summary>The spec for the ordered drink, shown BESIDE the card (hover).</summary>
        private void ShowOrderRecipeTip()
        {
            var visit = _idVisit;
            if (visit == null || _idRecipeTip == null || _idRecipeTipBody == null) return;
            float h = DrawRecipeSpec(_idRecipeTipBody, visit.Order.Wanted, dark: true, width: TipW - 20f);
            _idRecipeTip.sizeDelta = new Vector2(TipW, h + 16f);
            _idRecipeTip.gameObject.SetActive(true);
            _idRecipeTip.SetAsLastSibling();
        }
        private Image _idOrderIcon;

        // The shop tablet (v5 P13). Two errands, not one wall of cards: what goes behind the
        // bar, and what the room itself is made of.
        // FIVE (the author, 2026-08-09: booze and the mixer/garnish sort of thing must
        // not share a department). The split is keyed on IngredientCategories.IsAlcoholic,
        // which reads the CATEGORY rather than the ABV — strength is display-only and an
        // aisle must not make it load-bearing. Beer stays on the liquor side under its own
        // section: a keg is booze, and the split being asked for is booze against soft.
        private static readonly string[] ShopTabs =
            { "RESTOCK", "LIQUOR", "MIXERS", "RECIPES", "UPGRADES" };
        // Recut on ONE 24x24 canvas (2026-08-09). The old four were 16x26, 24x26,
        // 25x24 and 20x19, so in a single 20x20 preserveAspect rect they drew between
        // 12.3 and 20 units wide: the rects lined up and the pictures did not.
        private static readonly string[] ShopTabIcons =
            { "sh_i2_restock", "sh_i2_bottles", "sh_i2_mixers", "sh_i2_recipes", "sh_i2_upgrades" };
        private readonly Image[] _shopTabKeys = new Image[ShopTabs.Length];
        private readonly Text[] _shopTabLabels = new Text[ShopTabs.Length];
        private readonly Image[] _shopTabIcons = new Image[ShopTabs.Length];
        private int _shopTab;
        private Text _tabletTill;

        /// <summary>
        /// BOOZAAR — the trade the bar orders from (booze + bazaar). A parody storefront in
        /// the same spirit as the shelf's parody brands, and it carries a brand system of
        /// its own: the wordmark is set in the display face rather than drawn, because the
        /// generator cannot spell and a shop that misprints its own name is not a shop.
        /// Renaming is this constant and nothing else — every position is measured.
        /// </summary>
        private const string ShopBrand = "BOOZE CRUISE";

        // The storefront's palette (the author, 2026-08-07: white and green, and readable).
        // A distributor's ordering site is a white page with a house green — the dark shop
        // failed the only test that matters, which is whether you can read it.
        private static readonly Color ShopGreen = new Color(0.161f, 0.514f, 0.267f, 1f);   // house
        private static readonly Color ShopGreenLit = new Color(0.290f, 0.706f, 0.400f, 1f);
        private static readonly Color ShopGreenDark = new Color(0.075f, 0.290f, 0.157f, 1f);
        private static readonly Color ShopPage = new Color(0.949f, 0.961f, 0.945f, 1f);    // paper
        private static readonly Color ShopAisle = new Color(0.890f, 0.914f, 0.886f, 1f);   // rail
        private static readonly Color ShopInk = new Color(0.098f, 0.145f, 0.110f, 1f);     // type
        private static readonly Color ShopInkSoft = new Color(0.404f, 0.451f, 0.412f, 1f);
        /// <summary>Secondary type ON a tile. ShopInkSoft was the obvious pick and it fails
        /// AA against four of the seven plate tints (3.4:1 on the sealed crate) — a grey
        /// chosen for white paper does not survive being reused on coloured paper.</summary>
        private static readonly Color TileMetaInk = new Color(0.298f, 0.341f, 0.310f, 1f);

        // THE STATE LANGUAGE (2026-08-09). Four independent channels — strip hue, chip
        // glyph, plate tint, and whether a control exists at all — so no two states lean on
        // colour alone and the colour-blind path still reads.
        private static readonly Color StripStock = new Color(0.290f, 0.706f, 0.400f, 1f);
        private static readonly Color StripDeny = new Color(0.706f, 0.243f, 0.259f, 1f);
        private static readonly Color StripPicked = new Color(0.937f, 0.678f, 0.180f, 1f);
        private static readonly Color StripHeld = new Color(0.639f, 0.659f, 0.643f, 1f);
        private static readonly Color StripSealed = new Color(0.310f, 0.231f, 0.161f, 1f);
        private static readonly Color StripReturn = new Color(0.239f, 0.478f, 0.706f, 1f);
        private static readonly Color PlateDeny = new Color(0.965f, 0.949f, 0.941f, 1f);
        private static readonly Color PlatePicked = new Color(1.000f, 0.949f, 0.851f, 1f);
        private static readonly Color PlateOrdered = new Color(0.827f, 0.941f, 0.847f, 1f);
        private static readonly Color PlateSealed = new Color(0.847f, 0.839f, 0.812f, 1f);
        private static readonly Color PlateReturn = new Color(0.898f, 0.937f, 0.973f, 1f);
        private static readonly Color PickedInk = new Color(0.478f, 0.322f, 0.039f, 1f);

        // The inspector is a DARK plate inset into the white page: the author asked for a
        // box behind the descriptions, and a dark box under light type is the one treatment
        // that reads as a box rather than as text lying on the page.
        private static readonly Color InspectorBack = new Color(0.086f, 0.114f, 0.094f, 1f);
        private static readonly Color InspectorInk = new Color(0.878f, 0.906f, 0.878f, 1f);
        private static readonly Color InspectorDim = new Color(0.588f, 0.627f, 0.596f, 1f);
        private static readonly Color BuffGood = new Color(0.427f, 0.847f, 0.518f, 1f);
        private static readonly Color BuffCost = new Color(0.965f, 0.741f, 0.310f, 1f);
        private static readonly Color BuffBad = new Color(0.878f, 0.353f, 0.376f, 1f);
        private static readonly Color ShopCost = new Color(0.702f, 0.157f, 0.176f, 1f);

        // The page, top to bottom: 20 + 40 + 32 + 8 + 400 + 8 + 128 + 8 = 644, which is the
        // screen a 1096x700 device leaves inside a 28 bezel. Every one of these is load
        // bearing; the old set carried a hardcoded 436 that overshot its own rail by 34.
        private const float OsBarH = 20f, AppBarH = 40f, TabBarH = 32f, TabKeyW = 160f;
        // The foot, re-balanced (the author: bring the basket forward). It reads as a
        // sum and has to: 8 + 560 + 8 + 312 + 8 + 136 + 8 = 1040, the screen's width.
        // The inspector gives up 80 units and the order takes them: the order is the
        // control the whole market exists to reach and it was the quietest thing on
        // the page, against a 640-wide dark slab shouting beside it.
        private const float FootH = 128f, InspectorW = 560f, OrderW = 312f, ExitW = 136f;
        private const float BarW = 10f;
        // The tile, and the three product classes that share one shelf line.
        private const float TileW = 160f, TileH = 208f, ContentW = 140f;
        private const float BottleH = 134f, VesselH = 100f, IconH = 96f;
        private const string ShopIdleTip =
            "Point at a line to read it. Nothing is charged until you place the order.";

        private Text _fittingNote, _cartLine, _checkoutLabel, _cartHeadLabel, _osClock;
        private Text _cartTotal;
        private Text _inspIdentity, _inspMeta, _inspBody, _inspBuffA, _inspBuffB;
        private Image _inspBuffAIcon, _inspBuffBIcon, _fittingLamp, _inspMarkImg;
        private Text _billNextLabel;
        private RectTransform _checkout, _billNext;
        private ScrollRect _shopScroll;
        /// <summary>Where the aisle was left. Rebuilding after a pick must not throw the
        /// player back to the top of the shelf (the author, 2026-08-07).</summary>
        private float _shopScrollAt = 1f;

        /// <summary>
        /// The basket (the author, 2026-08-07: "önce ürünü seç, sepete ekle, sepeti öde").
        /// Nothing is bought on the click any more — a listing goes IN, and the order is
        /// placed as one act. Each entry keeps the verb that buys it, so checkout is just
        /// running them in the order they were picked.
        /// </summary>
        private sealed class CartEntry
        {
            public string Key;          // identity, so a second click takes it back out
            public string Label;
            public int Price;
            public bool IsFitting;      // stools/glassware/counter — one a night
            public Action Buy;
        }

        /// <summary>
        /// What a listing IS, right now. Eight answers, and the order they are TESTED in is
        /// part of the rule: the predicates overlap in real life (a refilled bottle is both
        /// "ordered tonight" and "full"), so a single if/else chain decides once and the
        /// tile can never render two states at the same time.
        /// </summary>
        private enum TileState
        {
            Orderable,      // in stock, affordable, click puts it in the order
            Unaffordable,   // the till cannot cover it — counting what is already picked
            Picked,         // sitting in the order, click takes it back out
            Ordered,        // bought by tonight's order
            Held,           // nothing to buy: bottle full, glass legendary, every stool in
            Sealed,         // behind a star gate, and its name stays hidden
            Refundable,     // bought at this close and still returnable
            NoFitting,      // a fitting, on a night whose one fitting is already spent
        }

        private enum BuffKind { Gain, Cost, Bad, Use }

        /// <summary>One effect line in the inspector: a colour AND its own icon, so the
        /// kind survives for a player who cannot tell the colours apart.</summary>
        private sealed class Buff
        {
            public string Text;
            public BuffKind Kind;
            public Buff(BuffKind kind, string text) { Kind = kind; Text = text; }
        }

        /// <summary>Everything one tile knows. Gathering it in a single object is what makes
        /// "say each fact once" enforceable: the tile shows Name/Meta/Money, and the
        /// inspector shows Identity/MetaLine/Body/Buffs — two disjoint sets, checked at the
        /// call site rather than hoped for across 200 lines of builder.</summary>
        private sealed class TileSpec
        {
            public string Name = "";
            public string Meta;              // one contextual token on the tile
            public string Money;             // "$38", "+$105", "3.0★"
            public string Word;              // "FULL" / "MAX" / "SOLD" — 4 CAPS, never 5
            public string PillVerb;          // "ADD" / "TAKE OUT" / "NO CASH" / "RETURN"
            public TileState State;
            public Sprite Art;
            public float ArtH = BottleH;
            public float StockFrac = -1f;    // >= 0 draws the meter instead of Meta
            public Action OnClick;
            public string Identity = "";     // the inspector's five rows
            public string MetaLine;
            public string Body;
            public Buff BuffA, BuffB;
        }

        private readonly List<CartEntry> _cart = new List<CartEntry>();
        /// <summary>What the last order bought, so the shop can stamp those rows SOLD.</summary>
        private readonly HashSet<string> _justOrdered = new HashSet<string>();
        /// <summary>The drawn device's bezel, in screen units.
        ///
        /// This number and the art are one decision. `sh_ipad2` is 274x175 — exactly the
        /// 1096x700 the device is drawn at — with a 28px border, and a sliced Image draws
        /// its border ring at 1:1 regardless of the rect. So the bezel renders at exactly
        /// the 28 the page insets by, and the frame is even on all four sides.
        ///
        /// The old pairing was the author's "bozuk": sh_ipad is 400x260 (1.538) worn on a
        /// 1120x706 rect (1.586), and its outer 62px ring is flat mid-grey with the actual
        /// charcoal body starting at x=70 — outside the border on the sides, inside it on
        /// the top. Sliced therefore drew a grey slab with a dark edge top and bottom and
        /// no edge at all left or right, which is exactly what the screenshot shows.
        /// </summary>
        private const float BezelX = 28f, BezelY = 28f;
        private const float TabletW = 1096f, TabletH = 700f;
        private static readonly Color TabletShell = new Color(0.13f, 0.12f, 0.15f, 1f);
        private static readonly Color TabletScreen = new Color(0.09f, 0.10f, 0.13f, 1f);
        private static readonly Color TabletLens = new Color(0.30f, 0.30f, 0.34f, 1f);
        private CustomerVisit _idVisit;

        private TycoonServiceFlow _flow;
        private TycoonPhase _lastPhase = TycoonPhase.DayOpen;
        private int _lastStormedCount;   // to catch a customer storming off (GDD 24 §4)
        private Text _toast;
        private float _toastUntil;

        private void Awake()
        {
            var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _body = bodyFont != null ? bodyFont : legacy;
            _display = displayFont != null ? displayFont : legacy;
            _shop = shopFont != null ? shopFont : _body;

            _bootstrap = GetComponent<GameBootstrap>();
            if (_bootstrap != null) _bootstrap.RunStarted += OnRunStarted;
            _flow = GetComponent<TycoonServiceFlow>();

            BuildUi();
            if (stage != null) stage.SetRegisterHandler(ToggleLedger);
        }

        private void OnDestroy()
        {
            if (_bootstrap != null) _bootstrap.RunStarted -= OnRunStarted;
        }

        private void ResetSeats()
        {
            foreach (var v in _seats)
            {
                v.Visit = null;
                v.Exiting = false;
                v.ExitT = 0f;
                v.WalkT = 0f;
                if (v.Group != null) v.Group.alpha = 1f;
                if (v.Root != null) v.Root.gameObject.SetActive(false);
            }
        }

        private void OnRunStarted()
        {
            // A new run re-resolves the ART, not just the state: sprite and piece
            // caches survive play sessions when domain reload is off, and plates
            // shipped mid-session were being ignored — worst case a front plate
            // remembered as missing left the drink floating OVER its bottle
            // (the author, 2026-08-05).
            ItemArt.ClearCache();
            BottleArt.ClearCache();
            _lastPhase = TycoonPhase.DayOpen;
            _lastStormedCount = 0;
            ResetSeats();
            _dayEndPanel.gameObject.SetActive(false);
            _bannerText.gameObject.SetActive(false);
            _flow?.CloseFlow();
            CloseId();
            if (_ledgerPanel != null) _ledgerPanel.gameObject.SetActive(false);
            if (_drinkGlass != null) { _drinkGlass.gameObject.SetActive(false); _glassShown = false; _glassGrabbed = false; }
            ApplyBarLook();
        }

        private void Update()
        {
            var run = Run;
            if (run == null) return;
            WatchGlassRack();

            if (run.Phase == TycoonPhase.DayOpen)
            {
                // Menus slow the world (GDD 24 §10): mixing or reading a licence must not
                // cost a storm-off by itself, but the clock never fully stops.
                bool menuOpen = (_flow != null && _flow.IsOpen) ||
                                (_idRoot != null && _idRoot.gameObject.activeSelf);
                run.Tick(Time.deltaTime * (menuOpen ? (float)TycoonConfig.MenuTimeScale : 1f));
            }

            if (run.Phase != _lastPhase)
            {
                _lastPhase = run.Phase;
                if (run.Phase == TycoonPhase.DayEnd) ShowDayEnd();
                if (run.Phase == TycoonPhase.Closed) ShowClosed();
            }

            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil)
                _toast.gameObject.SetActive(false);

            // Backing out of the flow with a drink still in the shaker leaves the counter empty,
            // because an unpoured drink is not a drink yet (2026-07-28). Say so, or the player
            // is left looking for a glass that was never filled.
            bool flowOpen = _flow != null && _flow.IsOpen;
            if (_flowWasOpen && !flowOpen && run.DrinkWaitingInShaker)
                Toast("STILL IN THE SHAKER — POUR IT INTO A GLASS");
            _flowWasOpen = flowOpen;

            RefreshTopBar();
            RefreshSeats();
            UpdateDrinkGlass();
        }

        private bool _flowWasOpen;

        // ── the floor ───────────────────────────────────────────────────────────

        private void OnMenuClicked() => _flow?.Open();

        private void OnSeatClicked(int index)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;   // finish the build first
            var visit = _seats[index].Visit;
            if (visit == null) return;

            // A bowl in hand goes down in front of them (v5 P16). Before the waiting check,
            // because a customer nursing a drink is exactly who takes a bowl of nuts — and
            // Core's own refusals do the talking when the snack cannot land (never alone,
            // bowl empty), so the toast is the rule speaking, not the menu's guess at it.
            if (_snackInHand != null)
            {
                var snack = _snackInHand;
                _snackInHand = null;
                try
                {
                    run.ServeSnack(snack.Id, visit);
                    Toast($"{snack.Name.ToUpperInvariant()} — ON THE TAB");
                }
                catch (InvalidOperationException e) { Toast(e.Message.ToUpperInvariant()); }
                RefreshSnackRow(run);
                return;
            }

            if (visit.State != VisitState.Waiting) return;
            if (!visit.HasOrdered) return;   // still deciding — no order to read yet (2026-07-23)

            // Clicking a customer reads their licence (GDD 24 §5). Serving is a separate act:
            // the finished drink is carried over and dropped on them (drag the glass).
            ShowId(visit);
        }

        /// <summary>Hands the ready drink to seat <paramref name="index"/> (the glass was dragged
        /// onto them). Returns true if it was served.</summary>
        private bool ServeSeat(int index)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return false;
            var visit = _seats[index].Visit;
            if (visit == null || visit.State != VisitState.Waiting) return false;
            if (!visit.HasOrdered) return false;   // can't hand a drink to someone still deciding
            if (!run.DrinkReady) return false;     // only what is in the glass goes out

            var verdict = run.ServeTo(visit);
            CloseId();
            Sfx.Play("serve_clink");                          // the glass lands in front of them
            LogVerdict(visit, verdict);
            StartCoroutine(ServeReaction(index, verdict));   // reaction + payment float up
            return true;
        }

        // ── the bussing beat (D2, v5 P14) ────────────────────────────────────────
        // A drinker leaves the empty glass on the stool, and the stool stays blocked until it
        // is cleared: the player's click does it now, the bar's slow clock does it in seven
        // seconds. The prop appears where the customer sat; clicking it is the bussing.

        private void RefreshDirtyGlasses(TycoonRun run)
        {
            foreach (var v in _seats)
            {
                bool show = v.Dirty != null && !v.Dirty.Cleared;
                if (show && v.DirtyProp == null)
                {
                    var prop = NewRect("DirtyGlass", _hudRoot);
                    prop.anchorMin = prop.anchorMax = new Vector2(0, 0);
                    prop.pivot = new Vector2(0.5f, 0);
                    prop.sizeDelta = new Vector2(34, 52);
                    // ON the counter's drawn surface, not floating at the waist-clip line
                    // (the author's report): the clip line is the counter's BACK edge; the
                    // top surface the glass stands on reads ~36px lower in the scene.
                    prop.anchoredPosition = new Vector2(v.SeatX, CounterLineY - 36f);
                    var img = prop.gameObject.AddComponent<Image>();
                    // The SAME glass everywhere (the author, 2026-08-02): the empty on the
                    // counter is the drawn vessel the drink was served in, at its line's
                    // tier — not a stock photo of some other glass.
                    GlasswareDefinition dirtyDef = null;
                    if (run != null && v.Dirty.GlasswareId != null)
                        foreach (var g in run.Glassware)
                            if (g.Id == v.Dirty.GlasswareId) { dirtyDef = g; break; }
                    if (dirtyDef != null)
                        img.sprite = GlassArt.For(dirtyDef, run.GlassTier(dirtyDef.Id)).Sprite;
                    else img.sprite = ItemArt.Glass;
                    img.preserveAspect = true;
                    img.color = new Color(1f, 1f, 1f, 0.85f);
                    if (img.sprite == null) img.color = new Color(0.8f, 0.9f, 0.95f, 0.5f);
                    var view = v;
                    var btn = prop.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(() =>
                    {
                        if (view.Dirty == null) return;
                        view.Dirty.Bus();
                        Sfx.Play("glass_down", 0.9f);
                        Toast("GLASS CLEARED — STOOL FREE");
                    });
                    var sink = prop.gameObject.AddComponent<PressSink>();
                    sink.Face = prop; sink.Depth = 3f; sink.Lift = 3f; sink.Tint = img;
                    v.DirtyProp = prop;
                }
                else if (!show && v.DirtyProp != null)
                {
                    Destroy(v.DirtyProp.gameObject);
                    v.DirtyProp = null;
                    v.Dirty = null;
                }
            }
        }

        // ── the snack bowls (v5 P16) ─────────────────────────────────────────────
        // On the counter, left end, opposite the bin: click a bowl to take it in hand, click
        // a customer to put it down. The plan said "from the menu"; the bowls stand on the
        // counter instead because a snack has no prep — sending the player through the drink
        // menu for a bowl of nuts would be a stage with nothing on it.

        private SnackDefinition _snackInHand;
        private readonly List<(SnackDefinition snack, Image art, Text stock)> _snackBowls =
            new List<(SnackDefinition, Image, Text)>();

        private void BuildSnackRow(RectTransform root)
        {
            var run = Run;
            if (run == null || run.Snacks.Count == 0) return;
            float x = 24f;
            foreach (var snack in run.Snacks)
            {
                var s = snack;
                var bowl = NewRect($"Snack_{s.Id}", root);
                bowl.anchorMin = bowl.anchorMax = bowl.pivot = new Vector2(0f, 0f);
                bowl.sizeDelta = new Vector2(76, 84);
                // ON the counter, which is what the comment above has always claimed:
                // at 96 they stood on the bar's FRONT panel, across the shelf bays,
                // and the glassware that belongs in those bays had nowhere to go.
                bowl.anchoredPosition = new Vector2(x, 190f);
                x += 82f;
                var hit = bowl.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0.001f);

                var art = NewRect("Art", bowl);
                Place(art, new Vector2(0.5f, 1), new Vector2(72, 54), new Vector2(0, 0));
                var img = art.gameObject.AddComponent<Image>();
                img.sprite = ItemArt.Load($"snack_{s.Id}");
                img.preserveAspect = true; img.raycastTarget = false;
                if (img.sprite == null) img.color = UITheme.Amber[2];   // no art yet: a warm chip

                var label = NewText("N", bowl, _body, 8, TextAnchor.LowerCenter, UITheme.TextSecondary);
                Place(label.rectTransform, new Vector2(0.5f, 0), new Vector2(96, 24), Vector2.zero);
                label.text = s.Name.ToUpperInvariant();

                var btn = bowl.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    var r = Run;
                    if (r == null || r.Phase != TycoonPhase.DayOpen) return;
                    if (r.SnackLeft(s.Id) <= 0) { Toast($"THE {s.Name.ToUpperInvariant()} BOWL IS EMPTY TODAY"); return; }
                    _snackInHand = _snackInHand == s ? null : s;   // click again to put it back
                    Sfx.Play(_snackInHand != null ? "garnish" : "glass_down", 0.8f);
                    Toast(_snackInHand != null
                        ? $"{s.Name.ToUpperInvariant()} IN HAND — CLICK A CUSTOMER"
                        : "PUT IT BACK");
                    RefreshSnackRow(r);
                });
                var sink = bowl.gameObject.AddComponent<PressSink>();
                sink.Face = art; sink.Depth = 4f; sink.Lift = 3f; sink.Tint = img;

                _snackBowls.Add((s, img, label));
            }
        }

        /// <summary>Stock counts and the in-hand highlight, redrawn after anything changes.</summary>
        private void RefreshSnackRow(TycoonRun run)
        {
            foreach (var (snack, art, stock) in _snackBowls)
            {
                int left = run.SnackLeft(snack.Id);
                stock.text = left > 0
                    ? $"{snack.Name.ToUpperInvariant()} · {left}"
                    : $"{snack.Name.ToUpperInvariant()} · OUT";
                var baseCol = art.sprite != null ? Color.white : UITheme.Amber[2];
                art.color = left <= 0 ? new Color(baseCol.r, baseCol.g, baseCol.b, 0.35f)
                    : _snackInHand == snack ? new Color(1f, 1f, 0.82f, 1f)
                    : baseCol;
            }
        }

        // ── the drink you carry (GDD 24 §3, 2026-07-22) ──────────────────────────

        private void BuildDrinkGlass(RectTransform root)
        {
            _glassHome = new Vector2(0, -200f);   // staged on the counter, above the MENU button
            // The bin, standing on the counter at the right-hand end, in front of the bar. Built
            // before the glass so the carried drink passes over it rather than under it.
            _binProp = NewRect("Bin", root);
            _binProp.anchorMin = _binProp.anchorMax = _binProp.pivot = new Vector2(1f, 0f);
            // An OPEN bin, cut in half by the bottom edge (the author, 2026-08-04).
            //
            // The bagged sack read as filth and was replaced 2026-08-02 by a chrome pedal bin;
            // the pedal bin's problem is what it shows once it is cropped. Standing half out of
            // frame, the half that survived was the domed LID — a closed shape, and the one part
            // of a bin a glass cannot be aimed at. The new one is an open stainless well, so the
            // half above the cut is its MOUTH: the dark oval you carry the glass to, which is
            // the whole verb of the prop.
            //
            // Half BELOW the screen, not half past the right edge: the bottom of the rect sits
            // one half-height under the frame, so the cut runs horizontally through the bin's
            // waist and its two banding hoops read as the last thing before the floor.
            _binProp.sizeDelta = new Vector2(BinW, BinH);
            _binProp.anchoredPosition = new Vector2(-8f, -BinH * 0.5f);
            _binImage = _binProp.gameObject.AddComponent<Image>();
            _binImage.sprite = ItemArt.Load("bin_well");
            if (_binImage.sprite == null) _binImage.sprite = ItemArt.Load("bin_clean");
            if (_binImage.sprite == null) _binImage.sprite = ItemArt.Load("bin_prop");
            _binImage.preserveAspect = true;
            _binImage.raycastTarget = false;
            _binImage.color = Color.white;
            if (_binImage.sprite == null) _binImage.enabled = false;

            // The drink you carry to a seat is the real glass now (v5 P14 / C9): the same
            // drawing the serve stage stands on the counter, with its interior filled to the
            // level the drink is actually at. It used to be a translucent box with a cyan bar
            // for a rim, which said "a drink" and nothing about WHICH drink.
            _drinkGlass = NewRect("DrinkGlass", root);
            _drinkGlass.anchorMin = _drinkGlass.anchorMax = _drinkGlass.pivot = new Vector2(0.5f, 0.5f);
            _drinkGlass.sizeDelta = new Vector2(78, CarriedGlassHeight);
            _drinkGlass.anchoredPosition = _glassHome;

            var body = _drinkGlass.gameObject.AddComponent<Image>();   // invisible, but the grab target
            body.color = new Color(0f, 0f, 0f, 0.004f);
            var grab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            grab.callback.AddListener(ev => OnGlassGrab((PointerEventData)ev));
            _drinkGlass.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);

            // The layer architecture (the author, 2026-08-02): BACK face and base first,
            // the liquid over it, the FRONT face — interior fully clear — on top.
            var backRt = NewRect("Back", _drinkGlass);
            Stretch(backRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassBack = backRt.gameObject.AddComponent<Image>();
            _drinkGlassBack.preserveAspect = true;
            _drinkGlassBack.raycastTarget = false;
            _drinkGlassBack.enabled = false;

            var liquid = NewRect("Liquid", _drinkGlass);
            Stretch(liquid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassLiquid = liquid.gameObject.AddComponent<Image>();
            _drinkGlassLiquid.raycastTarget = false;
            _drinkGlassLiquid.type = Image.Type.Filled;
            _drinkGlassLiquid.fillMethod = Image.FillMethod.Vertical;
            _drinkGlassLiquid.fillOrigin = (int)Image.OriginVertical.Bottom;
            _drinkGlassLiquid.preserveAspect = true;

            var art = NewRect("Art", _drinkGlass);
            Stretch(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassArt = art.gameObject.AddComponent<Image>();
            _drinkGlassArt.raycastTarget = false;
            _drinkGlassArt.preserveAspect = true;

            var hint = NewText("Hint", _drinkGlass, _body, 10, TextAnchor.UpperCenter, UITheme.Cyan[4]);
            Place(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(170, 18), new Vector2(0, 24));
            hint.text = "DRAG TO SERVE →";
            hint.raycastTarget = false;

            _drinkGlass.gameObject.SetActive(false);
        }

        private void OnGlassGrab(PointerEventData ev)
        {
            if (Run == null || Run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;
            _glassGrabbed = true;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_drinkGlass.parent, ev.position, null, out Vector2 cursor))
                _glassGrabOffset = _glassPos - cursor;   // keep the grab point under the cursor
            else
                _glassGrabOffset = Vector2.zero;
        }

        /// <summary>The finished drink sits on the counter and is dragged onto a customer to
        /// serve (GDD 24 §3). Heavy, springy carry with a lean into the motion (AAA feel).</summary>
        private void UpdateDrinkGlass()
        {
            var run = Run;
            // The glass on the counter is the SERVING glass and nothing else. A drink still in
            // the shaker is a half-finished build, not something you can pick up and carry — it
            // used to appear here, which is how an unpoured drink reached a customer (2026-07-28).
            bool ready = run != null && run.Phase == TycoonPhase.DayOpen
                && (_flow == null || !_flow.IsOpen)
                && run.DrinkReady;

            if (!ready)
            {
                if (_glassShown) { _drinkGlass.gameObject.SetActive(false); _glassShown = false; _glassGrabbed = false; }
                return;
            }

            if (!_glassShown)
            {
                _glassShown = true;
                _drinkGlass.gameObject.SetActive(true);
                _glassPos = _glassHome; _glassVel = Vector2.zero; _glassAngle = 0f; _glassAngVel = 0f;
            }
            // The glass shows the drink as it was actually built: the vessel it chose, its
            // blended colour and its real fill level — no fixed glass, colour or amount.
            int drinkTier = run.GlassTier(run.ServingGlassware?.Id);
            var piece = GlassArt.For(run.ServingGlassware, drinkTier);
            if (!ReferenceEquals(_drinkGlassware, run.ServingGlassware) || drinkTier != _drinkGlassTier
                || _drinkGlassArt.sprite == null)
            {
                _drinkGlassware = run.ServingGlassware;
                _drinkGlassTier = drinkTier;
                // Front face over the liquid when the set is modular; the composite
                // sprite carries a run without the generated art.
                _drinkGlassArt.sprite = piece.Front != null ? piece.Front : piece.Sprite;
                _drinkGlassBack.sprite = piece.Back;
                _drinkGlassBack.enabled = piece.Back != null;
                _drinkGlassLiquid.sprite = piece.Fill;
                _drinkGlass.sizeDelta = new Vector2(CarriedGlassHeight * piece.Aspect, CarriedGlassHeight);
            }
            _drinkGlassLiquid.color = DrinkColor();
            _drinkGlassLiquid.fillAmount = piece.FillAmount((float)run.ServingGlass.FillFraction);
            // The finishing touches ride the carried glass too (P14): the customer is handed
            // the drink that was actually finished, salt and wedge and all.
            GlassDecor.Sync(_drinkGlass, piece, run.ServingGlass, run);

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var mouse = Mouse.current;

            if (_glassGrabbed && (mouse == null || !mouse.leftButton.isPressed))
            {
                _glassGrabbed = false;
                // Dropped in the bin: the drink is thrown away (v5 P13 / C7). Checked before
                // the seats, because the bin sits on the counter among them and a drink let go
                // over it was plainly meant for it.
                if (IsOverBin(mouse))
                {
                    int fee = run.DiscardGlass();
                    Toast(fee > 0 ? $"BINNED · -${fee}" : "BINNED");
                    if (fee > 0)
                        LogService($"<color=#F27D8A>BINNED</color> a built drink · -${fee}");
                    _drinkGlass.gameObject.SetActive(false);
                    _glassShown = false;
                    return;
                }
                int seat = SeatUnderCursor(mouse);
                if (seat >= 0 && ServeSeat(seat))
                {
                    _drinkGlass.gameObject.SetActive(false);   // handed over; a new drink re-shows it
                    _glassShown = false;
                    return;
                }
            }

            // The bin only lifts its lid -- brightens -- while there is something to throw in it
            // and the hand is over it, so it never nags at an empty counter.
            if (_binImage != null)
                _binImage.color = _glassGrabbed && IsOverBin(mouse)
                    ? Color.white
                    : new Color(0.72f, 0.72f, 0.74f, 1f);

            Vector2 target = _glassHome;
            if (_glassGrabbed && mouse != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_drinkGlass.parent, mouse.position.ReadValue(), null, out Vector2 cursor))
                target = cursor + _glassGrabOffset;

            // Spring the glass to the target with a little overshoot (weight), and lean it into
            // the horizontal motion, springing back upright — the carry has heft.
            _glassVel += (target - _glassPos) * (GlassStiffness * dt);
            _glassVel *= Mathf.Exp(-GlassDamping * dt);
            _glassPos += _glassVel * dt;

            float targetAngle = Mathf.Clamp(-_glassVel.x * 0.035f, -26f, 26f);
            _glassAngVel += (targetAngle - _glassAngle) * (GlassAngStiffness * dt);
            _glassAngVel *= Mathf.Exp(-GlassAngDamping * dt);
            _glassAngle += _glassAngVel * dt;

            _drinkGlass.anchoredPosition = _glassPos;
            _drinkGlass.localRotation = Quaternion.Euler(0, 0, _glassAngle);
        }

        /// <summary>Which occupied stool the cursor is over, or -1.</summary>
        private int SeatUnderCursor(Mouse mouse)
        {
            if (mouse == null) return -1;
            var pos = mouse.position.ReadValue();
            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                if (s.Visit == null || s.Exiting || !s.Root.gameObject.activeSelf) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(s.Root, pos, null))
                    return i;
            }
            return -1;
        }

        /// <summary>The carried drink's colour: its ingredients' true liquid colours, blended by
        /// share in linear space (2026-07-23) — clear spirits read pale, and a mix stays clean.</summary>
        private Color DrinkColor() => UITheme.DrinkColor(Run?.Shelf, Run?.ServingGlass);

        /// <summary>How a served customer reacts (GDD 24 §4, §10): a word for the read/serve
        /// and the payment, rising from the seat with a little pop. Green when they're pleased,
        /// red when it's the wrong drink; a gold call when they order another round.</summary>
        private System.Collections.IEnumerator ServeReaction(int seatIndex, ServiceVerdict verdict)
        {
            var seat = _seats[seatIndex].Root;
            bool wrong = verdict.Match == OrderMatch.Wrong;
            Color tone = verdict.OrdersAgain ? UITheme.Amber[3]
                : wrong ? UITheme.ViceRed[3] : UITheme.Lime[3];

            // Only the FACE answers at the serve (2026-07-31): they can see the drink, so the
            // reaction line is honest — but the bill is not on the table yet. The money and
            // the stars float up when they finish and get up (TabFloat), which is when a
            // customer actually pays.
            string line = verdict.OrdersAgain ? "★ ANOTHER ROUND!"
                : verdict.Match == OrderMatch.Exact ? "PERFECT!"
                : verdict.Match == OrderMatch.Close ? "THANKS."
                : "NOT WHAT I ASKED";

            var text = NewText("React", seat.parent, _display, 14, TextAnchor.LowerCenter, tone);
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = line;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(178, 60);
            var start = seat.anchoredPosition + new Vector2(-89f, 118f);   // centred over the seat

            const float duration = 1.35f;
            float tt = 0f;
            while (tt < duration && text != null)
            {
                tt += Time.deltaTime;
                float k = Mathf.Clamp01(tt / duration);
                // A quick pop on the way in, then a slow rise and fade.
                float pop = 1f + 0.3f * Mathf.Clamp01(1f - k * 6f) - 0.05f * k;
                rt.localScale = new Vector3(pop, pop, 1f);
                rt.anchoredPosition = start + new Vector2(0, 58f * k);
                text.color = new Color(tone.r, tone.g, tone.b, 1f - k * k);
                yield return null;
            }
            if (text != null) Destroy(text.gameObject);
        }

        /// <summary>
        /// The bill, paid on the way out (2026-07-31): what the whole visit came to — every
        /// round of it — and the stars this customer leaves behind. Fired by the departure
        /// hook, which is the same moment Core settles the tab into the till.
        /// </summary>
        private System.Collections.IEnumerator TabFloat(int seatIndex, CustomerVisit visit)
        {
            var seat = _seats[seatIndex].Root;
            string amber = ColorUtility.ToHtmlStringRGB(UITheme.Amber[3]);
            string lime = ColorUtility.ToHtmlStringRGB(UITheme.Lime[3]);
            int tip = visit.Paid - visit.PaidBase;
            var body = new StringBuilder();
            body.Append($"<color=#{amber}>+${visit.PaidBase}</color>");
            if (tip > 0) body.Append($"  <color=#{lime}>+${tip} tip</color>");
            int stars = Mathf.Clamp(Mathf.RoundToInt((float)visit.Satisfaction * 5f), 1, 5);
            body.Append($"\n{Stars(stars)}");

            var text = NewText("Tab", seat.parent, _display, 14, TextAnchor.LowerCenter, UITheme.Amber[4]);
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = body.ToString();
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(178, 48);
            var start = seat.anchoredPosition + new Vector2(-89f, 96f);

            const float duration = 1.6f;
            float tt = 0f;
            var tone = UITheme.Amber[4];
            while (tt < duration && text != null)
            {
                tt += Time.deltaTime;
                float k = Mathf.Clamp01(tt / duration);
                rt.anchoredPosition = start + new Vector2(0, 64f * k);
                text.color = new Color(tone.r, tone.g, tone.b, 1f - k * k);
                yield return null;
            }
            if (text != null) Destroy(text.gameObject);
        }

        /// <summary>A short notice under the top bar — refusals, mostly (GDD 24 §7).</summary>
        private void Toast(string message)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toastUntil = Time.unscaledTime + 1.6f;
            _toast.gameObject.SetActive(true);
        }

        /// <summary>Whether the cursor is over the bin's mouth (v5 P13 / C7).</summary>
        private bool IsOverBin(UnityEngine.InputSystem.Mouse mouse)
        {
            if (_binProp == null || mouse == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(
                _binProp, mouse.position.ReadValue(), null);
        }

        // ── refresh ─────────────────────────────────────────────────────────────

        /// <summary>Pushes the bought glassware onto the bar — the under-counter rack is the
        /// one fitting the picture still shows (the stage's own tier tint retired with the
        /// pour-glass HUD in the 2026-08-07 sweep).</summary>
        private void ApplyBarLook()
        {
            var run = Run;
            if (run == null) return;
            RefreshGlassRack(run);
        }

        /// <summary>
        /// The rack is placed FROM the bar, and the bar re-fits itself whenever the window
        /// changes shape — including once on the first frames, before its parent's rect is
        /// trustworthy. Building the glasses against that first reading put them four times
        /// too far out and below the floor, which is the sort of bug that only ever shows up
        /// as "the glasses are somewhere else". So watch the geometry and rebuild when it
        /// moves: two float compares a frame against a rack that is silently wrong.
        /// </summary>
        private float _rackCellX = float.NaN, _rackCellY = float.NaN;

        private void WatchGlassRack()
        {
            var run = Run;
            if (run == null || _glassRack == null) return;
            var stage = _stage != null ? _stage : FindFirstObjectByType<DiegeticStage>();
            _stage = stage;
            if (stage == null) return;
            if (!stage.ShelfCell(0, out float cx, out float fy, out _)) return;
            if (Mathf.Approximately(cx, _rackCellX) && Mathf.Approximately(fy, _rackCellY)) return;
            _rackCellX = cx; _rackCellY = fy;
            RefreshGlassRack(run);
        }

        /// <summary>The under-counter glass rack (the author, 2026-08-02): every glass line
        /// the bar owns, standing on a walnut strip at its CURRENT tier — buy a step and
        /// the glass on the shelf is the finer one. Sits left of the bin, clear of MENU.</summary>
        // Where the glasses stand: the bar-front's own COMPARTMENTS (the author). It used to
        // be three cells left of the MENU key and two right of it — but the right-hand pair
        // stood exactly where the bin now sits (2026-08-04), and a rack you cannot see is not
        // a rack. All five moved to the run of cells left of the key, spaced 80 apart, which
        // is wider than the broadest glass the set has (the rocks tumbler at 69). The corner
        // belongs to the bin.
        /// <summary>
        /// WHICH of the bar front's eight compartments the glassware stands in.
        ///
        /// The slots used to be five hand-picked x values 80 apart, which put every glass
        /// on a divider rather than in a cell — the counter art carries the compartments
        /// and nothing was reading them. DiegeticStage measures them off the drawing now,
        /// so this is only the choice of WHICH five, and the choice is about what else is
        /// on the bar front: cells 7 and 8 (HUD x +400 and +560 at the reference aspect)
        /// stand behind the bin, which reaches up into the same band.
        /// </summary>
        private static readonly int[] GlassRackCells = { 0, 1, 2, 3, 4 };

        /// <summary>How tall a glass on the rack is drawn. Down from 92 with the move: five in
        /// a row need to be narrow enough that 80 apart is clear air, not a near-miss.</summary>
        private const float RackGlassH = 84f;

        private void RefreshGlassRack(TycoonRun run)
        {
            if (_hudRoot == null || run.Glassware.Count == 0) return;
            if (_glassRack == null)
            {
                _glassRack = NewRect("GlassRack", _hudRoot);
                _glassRack.anchorMin = _glassRack.anchorMax = new Vector2(0.5f, 0);
                _glassRack.pivot = new Vector2(0.5f, 0);
                // The whole lower half: the compartments sit at HUD y 76..154 at the
                // reference aspect, well above the 110-unit strip this used to be.
                _glassRack.sizeDelta = new Vector2(1280, 360);
                _glassRack.anchoredPosition = Vector2.zero;
                // BEHIND EVERYTHING. The rack is built lazily, on the first ApplyBarLook,
                // which is long after the HUD's own children exist — so it arrived as the
                // last sibling and drew over the menu keys, the bill and the whole market
                // (the author, 2026-08-09). It is scenery: it belongs at the back.
                _glassRack.SetAsFirstSibling();
            }
            _glassRack.SetAsFirstSibling();
            foreach (Transform c in _glassRack) Destroy(c.gameObject);

            // The compartments, asked of the bar itself. A missing stage (a bench, a test
            // scene) falls back to the old fixed spacing rather than stacking every glass
            // on top of the next.
            var stage = _stage != null ? _stage : FindFirstObjectByType<DiegeticStage>();
            _stage = stage;

            int i = 0;
            foreach (var g in run.Glassware)
            {
                int tier = run.GlassTier(g.Id);
                var piece = GlassArt.For(g, tier);
                int cell = GlassRackCells[Mathf.Min(i, GlassRackCells.Length - 1)];

                // Stage units, then doubled: the stage draws at 640x360 and the HUD at
                // 1280x720, so one is exactly two of the other.
                float x, floorY, cellH;
                if (stage != null && stage.ShelfCell(cell, out float sx, out float sy, out float sh))
                {
                    x = sx * StageToHud;
                    floorY = sy * StageToHud;
                    cellH = sh * StageToHud;
                }
                else
                {
                    x = -600f + i * 80f;
                    floorY = 8f;
                    cellH = RackGlassH;
                }
                // A SET OF FIVE, filling the bay with a little air at each end. Derived
                // rather than drawn — the line's OWN sprite at its own tier, five times —
                // so a bought rung changes all five at once and there is no second asset
                // to keep in step. Perspective is carried by DEPTH, not by scale alone:
                // the outermost stand furthest back, so they sit higher, smaller and
                // dimmer, which is what a shelf drawn from slightly above looks like.
                // The RUN is sized from the BAY, not the bay from the glass. Five glasses
                // as tall as the opening allows would each be 47 units wide in a 150-unit
                // interior, so they would land on top of one another; asking instead what
                // width lets five stand across the bay with a clean overlap, and taking the
                // height from THAT, is the only ordering that fills the shelf.
                // TWO ROWS IN DEPTH, NOT FIVE ACROSS (the author: the glasses should follow
                // the table's perspective, and cover more of it). Five in one line is a
                // frieze; a shelf holds a row at the back and a row in front of it, and
                // that is where a real front-to-back height difference comes from.
                //
                // THREE BACK, TWO FRONT. The back row stands on the far edge of the
                // surface, the front row on the near edge, and the depth between them is
                // MEASURED: the turquoise band is 13 art px of the cell's 53, so the two
                // rows are (13/53) of the opening apart on screen.
                const int BackRow = 3, FrontRow = 2;
                const float Overlap = 0.60f;                    // step within a row
                // The surface is 93..105 in the art and the cell opening is 53 tall, so the
                // far edge is nine art pixels behind the near one — NOT thirteen. Thirteen
                // was the whole band including its own front lip, and it stood the back row
                // clean off the shelf.
                const float SurfaceDepth = DiegeticStage.ShelfDepthPx / 53f;
                float bay = cellH * (75f / 53f);                // the interior, in HUD units
                // PROPORTION ACROSS THE WHOLE RACK, not within one bay. Sizing each line to
                // fill its own bay made a rocks tumbler and a highball the same height,
                // because the wide one had to shrink to fit five across — so the shelf said
                // they were the same glass. The run is measured from the WIDEST vessel in
                // the set instead, and every other line is drawn at the same units-per-
                // sprite-pixel, which is what makes a pint taller than a tumbler on screen
                // exactly as it is on the page.
                // ONE k FOR THE WHOLE SET — HUD units per sprite pixel — taken from the
                // widest and tallest vessels the bar owns. Dividing by each sprite's own
                // height instead gave every line the same drawn height, which is the very
                // thing that was wrong: a rocks tumbler is not as tall as a pint.
                float widestPx = 1f, tallestPx = 1f;
                foreach (var other in run.Glassware)
                {
                    var op = GlassArt.For(other, run.GlassTier(other.Id));
                    if (op.Sprite == null) continue;
                    widestPx = Mathf.Max(widestPx, op.Sprite.rect.width);
                    tallestPx = Mathf.Max(tallestPx, op.Sprite.rect.height);
                }
                // The back row is the wider one, so it sets the size; the front row then
                // has room to sit between its gaps.
                float wForBay = (bay - 4f) / (1f + Overlap * (BackRow - 1));
                // A shade smaller (the author): five vessels and two rows want a little air
                // between them and the shelf above.
                float unitsPerPixel = Mathf.Min(wForBay / widestPx, (cellH - 10f) / tallestPx) * 0.88f;
                float h = piece.Sprite.rect.height * unitsPerPixel;
                float gw = h * piece.Aspect;
                float step = gw * Overlap;
                float rise = cellH * SurfaceDepth;              // the far edge, in HUD units
                // The shadow is cast by the RUN, not by one glass, so it is laid down
                // once the run's width is known.
                var shadow = NewRect($"S_{g.Id}", _glassRack);
                shadow.anchorMin = shadow.anchorMax = new Vector2(0.5f, 0);
                shadow.pivot = new Vector2(0.5f, 0.5f);
                shadow.sizeDelta = new Vector2(gw + step * (BackRow - 1) + 12f, 10);
                shadow.anchoredPosition = new Vector2(x, floorY + 3f);
                var shImg = shadow.gameObject.AddComponent<Image>();
                shImg.sprite = BackBarArt.BottleShadow();
                shImg.raycastTarget = false;

                for (int k = 0; k < BackRow + FrontRow; k++)
                {
                    // 0..2 are the back row, 3..4 the front row standing in its gaps.
                    bool back = k < BackRow;
                    int inRow = back ? k : k - BackRow;
                    int rowCount = back ? BackRow : FrontRow;
                    int depth = back ? 1 : 0;
                    float dx = (inRow - (rowCount - 1) * 0.5f) * step * (back ? 1f : 1.6f);
                    var rt = NewRect($"G_{g.Id}_{k}", _glassRack);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                    rt.pivot = new Vector2(0.5f, 0);
                    // The far row is smaller AND higher by the surface's own depth — the
                    // two together are what perspective is. 3 units of rise, which is what
                    // this was, is a nudge; the drawn floor is thirteen art pixels deep.
                    float kh = h * (back ? 0.84f : 1f);
                    rt.sizeDelta = new Vector2(kh * piece.Aspect, kh);
                    rt.anchoredPosition = new Vector2(x + dx, floorY + (back ? rise : 0f));
                    var img = rt.gameObject.AddComponent<Image>();
                    img.sprite = piece.Sprite;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    // SHADED, front to back and warm to cool. The bay is lit from in front
                    // and above, so the further in a glass stands the less light reaches it
                    // AND the bluer what does reach it becomes — a flat brightness ramp
                    // read as five copies at five opacities rather than as depth.
                    float lit = 1f - 0.24f * depth;
                    img.color = new Color(lit * 0.98f, lit * 1.0f, lit * 1.06f, 1f);
                    for (int d = 0; d < depth; d++) rt.SetAsFirstSibling();
                }
                // No tier stars under the rack (the author, 2026-08-02): the glass's own
                // dress already says which rung it is, and a row of stars under every one
                // read as a scoreboard bolted to the counter.
                i++;
            }
        }

        private void RefreshTopBar()
        {
            var run = Run;
            // The clock, not a quota (v5 P12 / C5): a shift from 18:00 to 02:00. The day
            // number survives underneath — rent, the ledger and the strike count all still
            // count days — it simply stops being what the player reads the night by.
            double hour = run.Floor.ClockHour;
            int hh = (int)hour % 24, mm = (int)((hour - (int)hour) * 60);
            // Kept short on purpose: the clock string is longer than the old "DAY 1" and ran
            // straight into the till at the money's fixed x. Who is in is on the seat row.
            _clockText.text = $"{hh:00}:{mm / 5 * 5:00}";
            // The plaque's rule is the state light: cyan through the shift, magenta once the
            // room is being called — visible from across the screen without reading a word.
            bool last = run.Floor.IsClosingTime;
            _dayText.text = last ? "LAST CALL" : CalendarFor(run.Day);
            _dayText.color = last ? UITheme.Magenta[4] : UITheme.Cyan[3];
            _clockText.color = last ? UITheme.Magenta[4] : UITheme.TextPrimary;
            if (_clockRule != null) _clockRule.color = last ? UITheme.Magenta[3] : UITheme.Cyan[2];

            bool red = run.Money < 0;
            _moneyText.text = $"${run.Money}";
            _moneyText.color = red ? UITheme.ViceRed[3] : UITheme.Money;
            if (_tillRule != null) _tillRule.color = red ? UITheme.ViceRed[3] : UITheme.Amber[2];
            if (stage != null) stage.SetMoney($"${run.Money}");
            _crowdText.text = run.CrowdToday == WealthTier.HighRoller ? "TONIGHT · HIGH ROLLERS"
                : run.CrowdToday == WealthTier.Broke ? "TONIGHT · BROKE CROWD" : "TONIGHT · REGULARS";
            _crowdText.color = run.CrowdToday == WealthTier.HighRoller ? UITheme.Magenta[4]
                : run.CrowdToday == WealthTier.Broke ? UITheme.ViceRed[3] : UITheme.Cream[2];

            // The standing, as a number and as a row of stars. A half-lit star is a real
            // half: the average is continuous, and rounding it to whole stars would hide
            // exactly the movement the player is trying to cause.
            double stars = run.Rating.Average;
            _ratingText.text = stars.ToString("0.0");
            // A 1.3 is 1.3 stars of amber: the mask's width IS the rating (the author).
            _starsFill.sizeDelta = new Vector2((float)(stars / 5.0) * _ratingStars.Length * StarGap, 0);
        }

        private void RefreshSeats()
        {
            var run = Run;
            var seated = run.Floor.Seated;

            // A patron whose patience ran out storms off (GDD 24 §4) — a loud red notice, so a
            // walk-out never passes unnoticed.
            int stormed = 0;
            foreach (var v in run.Floor.Finished) if (v.State == VisitState.StormedOff) stormed++;
            if (stormed > _lastStormedCount) Toast("A CUSTOMER STORMED OFF");
            _lastStormedCount = stormed;

            // The licence is only good while its holder is at the bar.
            if (_idVisit != null && (_idVisit.State != VisitState.Waiting || !seated.Contains(_idVisit)))
                CloseId();

            bool drinkReady = run.Phase == TycoonPhase.DayOpen && run.DrinkReady &&
                (_flow == null || !_flow.IsOpen);

            // Stools are stable (2026-07-22): a customer keeps their seat until they leave, so
            // busts never shift or morph when the queue compacts. Reconcile the positional
            // Seated list against the fixed stools each frame.
            // 1) Departures — a stool whose patron is no longer seated starts a leave animation.
            for (int i = 0; i < _seats.Count; i++)
            {
                var v = _seats[i];
                if (v.Visit != null && !v.Exiting && !seated.Contains(v.Visit))
                {
                    v.Exiting = true;
                    v.ExitT = 0f;
                    v.ExitStorm = v.Visit.State == VisitState.StormedOff;
                    if (v.ExitStorm)
                        LogService($"<color=#F27D8A>STORM-OFF</color> " +
                            (v.Visit.IdInspected ? v.Visit.Order.Wanted.Name.ToUpperInvariant() : "?") +
                            " · patience ran out · $0 · " + LogStars(0));
                    else if (v.Visit.Paid > 0)
                        LogService($"<color=#F5C97B>TAB</color> settled ${v.Visit.Paid}" +
                            (v.Visit.SnacksTaken > 0 ? $" (+{v.Visit.SnacksTaken} snack)" : "") +
                            $" · leaves {LogStars(v.Visit.Satisfaction)}");
                    // The bussing beat (D2): a drinker leaves the empty glass on this stool.
                    // Core created the DirtyGlass in the same tick that freed the seat; this
                    // view claims the first one no other stool has claimed.
                    if (v.Visit.Served != null)
                        foreach (var g in run.Floor.Dirty)
                        {
                            bool claimed = false;
                            foreach (var other in _seats) if (other.Dirty == g) { claimed = true; break; }
                            if (!claimed) { v.Dirty = g; break; }
                        }
                    // The tab settles as they go: what they paid and the stars they leave
                    // behind float over the emptying stool. The serve only earned the face.
                    if (v.Visit.Paid > 0) StartCoroutine(TabFloat(i, v.Visit));
                    if (v.Visit.Paid > 0) Sfx.Play("cash");
                    Sfx.Play(!v.ExitStorm && v.Visit.Satisfaction >= 0.55 ? "cheer_sfx" : "upset_sfx", 0.6f);
                    // And the body answers before it leaves (P15/D5): a cheer or a slump on
                    // the stool. This is where the emotional tell lives now the stat rows
                    // left the card — skipped cleanly while the clips have no frames yet.
                    v.ReactClip = !v.ExitStorm && v.Visit.Satisfaction >= 0.55
                        ? PatronClip.Cheer : PatronClip.Upset;
                    var reactLook = v.Look ?? (_looks.Count > 0 ? _looks[0] : null);
                    v.ReactLeft = reactLook != null
                        && reactLook.Clips.TryGetValue(v.ReactClip, out var rf) && rf.Length > 0
                        ? ReactSeconds : 0f;
                }
            }
            // 2) Arrivals — a seated customer with no stool takes the first free one and walks in.
            foreach (var visit in seated)
            {
                bool assigned = false;
                for (int i = 0; i < _seats.Count; i++) if (_seats[i].Visit == visit) { assigned = true; break; }
                if (assigned) continue;
                for (int i = 0; i < run.Seats && i < _seats.Count; i++)
                {
                    var v = _seats[i];
                    if (v.Visit == null && !v.Exiting)
                    {
                        v.Visit = visit;
                        v.WalkT = 0f;
                        // Who walked in, and how tall they are. The ticket and the gauge
                        // hang off THEIR head: the cast runs from 135 to 166 pixels of
                        // figure, which is 60 HUD units of difference, and a fixed window
                        // would leave the short ones with their paperwork floating.
                        v.Look = LookFor(visit);
                        if (v.Look != null)
                        {
                            v.Tag.anchoredPosition = new Vector2(0, v.Look.HeadTop + 15f);
                            if (v.Gauge != null)
                                v.Gauge.anchoredPosition = new Vector2(0, v.Look.HeadTop + 6f);
                        }
                        v.Root.gameObject.SetActive(true);
                        Sfx.Play("door", 0.5f);   // someone through the door (P17)
                        break;
                    }
                }
            }

            RefreshSnackRow(run);
            RefreshDirtyGlasses(run);
            // The bar bed (P17): always on, muffled while a stage or the licence is open.
            Sfx.Ambience(ducked: (_flow != null && _flow.IsOpen) ||
                                 (_idRoot != null && _idRoot.gameObject.activeSelf));

            // 3) Render each stool from its assigned patron.
            for (int i = 0; i < _seats.Count; i++)
            {
                var view = _seats[i];

                if (view.Exiting) { AdvanceExit(view); continue; }

                if (view.Visit == null)
                {
                    if (view.Root.gameObject.activeSelf) view.Root.gameObject.SetActive(false);
                    continue;
                }

                AdvanceWalkIn(view);

                var visit = view.Visit;
                bool deciding = !visit.HasOrdered;                    // reading the menu (2026-07-23)
                bool drinking = visit.State == VisitState.Drinking;   // served, nursing the drink

                // The bubble only knows what the PLAYER knows (v5 C3): until the ID card has
                // been read, Core refuses to hand the order over at all. Stripped to three
                // beats (the author, 2026-08-02): it does not exist until they SIT and have
                // an order to give; unread it says only that they are ready — not who they
                // are, not what they want; read, it says only the name and the order.
                bool known = visit.IdInspected;
                bool atTheStool = view.WalkT >= 1f;
                bool showBubble = atTheStool && (!deciding || drinking);
                if (view.Tag.gameObject.activeSelf != showBubble)
                    view.Tag.gameObject.SetActive(showBubble);

                if (showBubble)
                {
                    // A regular ordering again after a perfect serve gets a gold star and the
                    // round count (GDD 24 §4) — the reward for reading them right, made
                    // visible. The name is part of what the card teaches: it waits for the read.
                    string star = visit.ExtraOrdersTaken > 0
                        ? $"<color=#F5C97B>★{visit.ExtraOrdersTaken + 1} </color>" : "";
                    view.Name.supportRichText = true;
                    view.Name.text = known
                        ? star + (visit.Regular?.Name ?? "Customer").ToUpperInvariant() : "";
                    view.Name.color = UITheme.TextPrimary;
                    view.Order.color = UITheme.Amber[4];

                    if (view.Icon != null)
                    {
                        view.Icon.sprite = !known ? null
                            : DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
                        view.Icon.enabled = view.Icon.sprite != null;
                        view.Icon.color = drinking ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                    }

                    if (drinking)
                    {
                        // Served and content; the drink is theirs to finish before they go.
                        view.Wants.text = "ENJOYING IT";
                        view.Order.text = known
                            ? $"{visit.Order.Wanted.Name.ToUpperInvariant()}  ·" : "·";
                        view.Order.color = UITheme.Lime[3];
                    }
                    else if (!known)
                    {
                        // Ready, unread: the one line the author asked for, and nothing else.
                        view.Wants.text = "READY TO ORDER";
                        view.Order.text = "";
                    }
                    else
                    {
                        // Read: the name above, the order below — the card said the rest.
                        view.Wants.text = "";
                        view.Order.text = visit.Order.Wanted.Name.ToUpperInvariant();
                    }

                    // The ticket FITS its lines and its WIDEST line (the author, 2026-08-02:
                    // "yazı hiçbir zaman taşmamalı"). SEX ON THE BEACH ran off both ends of
                    // a fixed card. The card takes the width of the longest thing it says,
                    // up to a cap; past the cap the order wraps to a second row and the card
                    // grows downward instead. Nothing is ever clipped, and nothing floats in
                    // an empty box.
                    float iconRoom = view.Icon != null && view.Icon.enabled ? 28f : 0f;
                    float widest = Mathf.Max(view.Name.preferredWidth,
                        Mathf.Max(view.Wants.preferredWidth, view.Order.preferredWidth + iconRoom));
                    float cardW = Mathf.Clamp(widest + 24f, BustW + 48f, TagMaxW);
                    float textW = cardW - 8f;

                    // The order is the line that runs long, so it is the one allowed to wrap.
                    int orderLines = view.Order.text.Length == 0 ? 0
                        : Mathf.Max(1, Mathf.CeilToInt(
                            (view.Order.preferredWidth + iconRoom) / Mathf.Max(1f, textW)));
                    view.Order.horizontalOverflow = orderLines > 1
                        ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;

                    float rowTop = -8f;
                    view.Name.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Name.text.Length > 0) rowTop -= 16f;
                    view.Wants.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Wants.text.Length > 0) rowTop -= 16f;
                    view.Order.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    rowTop -= 17f * orderLines;
                    view.Tag.sizeDelta = new Vector2(cardW, -rowTop + 8f);
                }

                // The icon docks against the text's measured width, so the centred line reads
                // as one piece: [glass] DRAUGHT, the pair centred together — on whatever row
                // the order landed on now that the ticket packs its lines.
                if (view.Icon != null && view.Icon.enabled)
                    view.Icon.rectTransform.anchoredPosition = new Vector2(
                        -view.Order.preferredWidth * 0.5f - 4f,
                        view.Order.rectTransform.offsetMax.y - 6f);

                // TWO clocks now (the author, 2026-08-02): the wait to be ASKED, then a fresh
                // wait for the drink. The gauge draws whichever is live — Core says which —
                // and the asking wait draws in magenta so a bar that is emptying is visibly
                // a different failure from a bar that is slow. Deciding holds it full; a
                // drinking customer is content.
                bool beingIgnored = visit.AwaitingOrderTaking;
                float patience = (deciding || drinking) ? 1f : (float)visit.PatienceFraction;
                float gaugeW = BustW * 0.72f - 2f;
                view.PatienceFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(gaugeW * patience), -2);
                view.PatienceFill.color = (deciding || drinking) ? UITheme.Cyan[3]
                    : beingIgnored
                        ? (patience > 0.35f ? UITheme.Magenta[3] : UITheme.ViceRed[3])
                    : patience > 0.5f ? UITheme.Lime[3]
                    : patience > 0.25f ? UITheme.Amber[3] : UITheme.ViceRed[3];

                // Drive the animated customer (2026-07-23): walk-in, the sit-and-breathe idle,
                // a one-shot "placing the order" beat, then nursing the drink. Facing and frame
                // are chosen from the visit state; the body below the waist is clipped by the bar.
                UpdateSeatAnimation(view, visit, patience);

                // The tag glows cyan when a drink is built and this customer can actually take it.
                bool canTake = drinkReady && !deciding && !drinking;
                view.TagBg.color = canTake
                    ? new Color(UITheme.Selection.r, UITheme.Selection.g, UITheme.Selection.b, 0.92f)
                    : new Color(0.07f, 0.07f, 0.11f, 0.90f);
            }
        }

        /// <summary>Walks a newly-seated customer in from the right along the counter and fades
        /// them up (2026-07-23): the west-facing walk cycle carries them left into their stool.</summary>
        private void AdvanceWalkIn(SeatView view)
        {
            // In from the screen's real right edge (not a few frames off the stool) at a steady
            // pace, so a far stool is a longer walk (2026-07-23).
            float entryX = _hudRoot.rect.width + OffscreenMargin;
            if (view.WalkT < 1f)
            {
                float dist = Mathf.Max(1f, entryX - view.SeatX);
                view.WalkT = Mathf.Min(1f, view.WalkT + Time.deltaTime * WalkSpeed / dist);
                // CONSTANT speed, no ease (P15, the notes): the old ease-out meant the ground
                // slid fast under slow feet at the door and slow under fast feet at the stool —
                // the walk cycle only reads as walking when the floor moves at one rate.
                view.Root.anchoredPosition =
                    new Vector2(Mathf.Lerp(entryX, view.SeatX, view.WalkT), CounterLineY);
                view.Group.alpha = Mathf.Clamp01(view.WalkT * 4f);
            }
            else
            {
                view.Root.anchoredPosition = new Vector2(view.SeatX, CounterLineY);
                view.Group.alpha = 1f;
            }
        }

        /// <summary>Plays a customer leaving (2026-07-23): they get up and walk back out to the
        /// right the way they came (the walk cycle, mirrored). A stormed-off patron shakes first,
        /// then storms out faster.</summary>
        private void AdvanceExit(SeatView view)
        {
            // The reaction beat first: they stay on the stool and the drink answers — a fist
            // up or a slow head-shake — before they get up. One shot, then the walk.
            if (view.ReactLeft > 0f)
            {
                view.ReactLeft -= Time.deltaTime;
                UpdatePatronFrame(view, view.ReactClip, ReactSeconds - view.ReactLeft, facing: 1);
                return;
            }

            // Get up and walk all the way back off the right edge the way they came.
            float exitX = _hudRoot.rect.width + OffscreenMargin;
            float dist = Mathf.Max(1f, exitX - view.SeatX);
            float speed = view.ExitStorm ? ExitSpeed * 1.5f : ExitSpeed;
            view.ExitT = Mathf.Min(1f, view.ExitT + Time.deltaTime * speed / dist);
            float k = view.ExitT;

            float shake = view.ExitStorm && k < 0.25f ? Mathf.Sin(k * 90f) * 8f : 0f;
            float e = k * k;   // ease-in: rises and steps away, gathering pace
            view.Root.anchoredPosition = new Vector2(
                view.SeatX + shake + (exitX - view.SeatX) * e, CounterLineY);
            view.Group.alpha = 1f - Mathf.Clamp01((k - 0.5f) / 0.5f);

            // Mirror the walk so they face the way they are leaving (to the right).
            UpdatePatronFrame(view, PatronClip.Walk, view.AnimClock, facing: -1);
            view.AnimClock += Time.deltaTime;

            if (view.ExitT >= 1f)
            {
                view.Exiting = false;
                view.Visit = null;
                view.Group.alpha = 1f;
                view.Portrait.rectTransform.localScale = new Vector3(CharWiden, 1f, 1f);   // reset the mirror
                view.Root.gameObject.SetActive(false);
            }
        }

        // ── the animated customer (2026-07-23) ───────────────────────────────────

        /// <summary>Chooses the clip and frame for a seated customer from their state and drives
        /// the character image: the sit-and-breathe idle while they wait, a one-shot "placing the
        /// order" beat the moment they decide, and the drink once served — plus a light impatience
        /// flush over the last of their patience.</summary>
        private void UpdateSeatAnimation(SeatView view, CustomerVisit visit, float patience)
        {
            bool ordered = visit.HasOrdered;
            bool seated = view.WalkT >= 1f;
            bool drinking = visit.State == VisitState.Drinking;

            if (ordered && !view.WasOrdered && seated) view.OrderAnimLeft = OrderAnimSeconds;
            view.WasOrdered = ordered;

            if (drinking) view.DrinkT += Time.deltaTime; else view.DrinkT = 0f;

            PatronClip clip; float t;
            if (!seated)                      { clip = PatronClip.Walk;  t = view.AnimClock; }   // faces left, walking in
            else if (drinking)                { clip = PatronClip.Drink; t = view.DrinkT; }
            else if (view.OrderAnimLeft > 0f) { clip = PatronClip.Order; t = OrderAnimSeconds - view.OrderAnimLeft;
                                                view.OrderAnimLeft -= Time.deltaTime; }
            else                              { clip = PatronClip.Idle;  t = view.AnimClock; }
            view.AnimClock += Time.deltaTime;

            UpdatePatronFrame(view, clip, t, facing: 1);

            float flush = (!ordered || drinking) ? 1f : Mathf.Clamp01(patience / 0.35f);
            view.Portrait.color = Color.Lerp(new Color(1f, 0.72f, 0.72f, 1f), Color.white, flush);
        }

        /// <summary>Sets the character image to the right frame of <paramref name="clip"/> at time
        /// <paramref name="t"/>, mirrored when <paramref name="facing"/> is -1 (leaving right).</summary>
        private void UpdatePatronFrame(SeatView view, PatronClip clip, float t, int facing)
        {
            var look = view.Look ?? (_looks.Count > 0 ? _looks[0] : null);
            if (look == null || !look.Clips.TryGetValue(clip, out var frames) || frames.Length == 0) return;
            view.Portrait.sprite = frames[PatronFrameIndex(clip, t, frames.Length)];
            // A touch wider than tall (CharWiden), mirrored to face right on the way out.
            view.Portrait.rectTransform.localScale = new Vector3(CharWiden * (facing < 0 ? -1f : 1f), 1f, 1f);
        }

        /// <summary>The frame index for a clip at time t. Most clips loop at a fixed rate; the
        /// drink raises and lowers the glass over a sip window then holds it at rest, so it reads
        /// as a real sip every few seconds instead of a gulp every frame (2026-07-23).</summary>
        private static int PatronFrameIndex(PatronClip clip, float t, int n)
        {
            if (n <= 1) return 0;
            // The reactions play once, spread over the beat, and hold their last frame.
            if (clip == PatronClip.Cheer || clip == PatronClip.Upset)
                return Mathf.Min(n - 1, Mathf.FloorToInt(t / ReactSeconds * n));
            if (clip == PatronClip.Drink)
            {
                float u = Mathf.Repeat(t, DrinkSipSeconds + DrinkHoldSeconds);
                if (u >= DrinkSipSeconds) return 0;                 // holding the glass at rest
                int span = 2 * (n - 1);                             // 0..n-1..1 (raise then lower)
                int p = Mathf.FloorToInt(u / DrinkSipSeconds * span) % span;
                return p < n ? p : span - p;
            }
            // Idle is a slow two-frame breath — a settled customer barely moves; the walk
            // strides (matched to the slower gait), the order talks.
            // Rates re-timed for the 2026-08-09 cast (the author: the talking runs too
            // slowly). The order clip is somebody speaking a sentence, not miming one:
            // its five frames at 7fps took most of a second per syllable. Idle is still a
            // breath rather than a fidget, just not a sigh.
            float fps = clip == PatronClip.Walk ? 10f : clip == PatronClip.Order ? 12f : 3.5f;
            return Mathf.FloorToInt(t * fps) % n;
        }

        private void LoadPatronFrames()
        {
            _looks.Clear();
            foreach (var entry in PatronCast)
            {
                var clips = new Dictionary<PatronClip, Sprite[]>
                {
                    [PatronClip.Idle]  = LoadPatronClip(entry.Slug, "idle"),
                    [PatronClip.Order] = LoadPatronClip(entry.Slug, "order"),
                    [PatronClip.Drink] = LoadPatronClip(entry.Slug, "drink"),
                    [PatronClip.Walk]  = LoadPatronClip(entry.Slug, "walk"),
                    [PatronClip.Cheer] = LoadPatronClip(entry.Slug, "cheer"),
                    [PatronClip.Upset] = LoadPatronClip(entry.Slug, "upset"),
                };
                // A look with no idle has no art on disk. Skip it instead of seating a
                // customer who renders as nothing.
                if (clips[PatronClip.Idle].Length == 0) continue;
                var face = Resources.Load<Sprite>(string.IsNullOrEmpty(entry.Slug)
                    ? "Patron/face" : $"Patron/{entry.Slug}/face");
                _looks.Add(new PatronLook
                { Slug = entry.Slug, Clips = clips, HeadY = entry.HeadY, Face = face,
                  Stars = entry.Stars });
            }
        }

        /// <summary>All frames of one clip, ordered by name. The original patron lives at
        /// Patron/&lt;clip&gt;; everyone cast since lives under their own slug.</summary>
        private static Sprite[] LoadPatronClip(string slug, string clip)
        {
            var sprites = Resources.LoadAll<Sprite>(
                string.IsNullOrEmpty(slug) ? $"Patron/{clip}" : $"Patron/{slug}/{clip}");
            System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }

        /// <summary>
        /// Which face sits down. Not rolled — a named regular is the SAME person every
        /// night, so hashing their name means Marguerite is always the nurse off the late
        /// shift, and recognising her across visits is the whole point of regulars. An
        /// anonymous drinker is keyed off the patience the run already rolled for them,
        /// which is deterministic under the seed and costs the RNG streams nothing (so the
        /// sim's arrivals stay byte-identical).
        /// </summary>
        private PatronLook LookFor(CustomerVisit visit)
        {
            if (_looks.Count == 0) return null;
            // A seat that already holds a look keeps it: this is asked again every time
            // the licence is opened, and a face that changed under the player would undo
            // the whole point of having twenty-two of them.
            foreach (var seat in _seats)
                if (seat.Visit == visit && seat.Look != null) return seat.Look;

            string key = visit != null && visit.Regular != null
                         && !string.IsNullOrEmpty(visit.Regular.Name)
                ? visit.Regular.Name
                : visit == null ? "" : visit.PatienceMax.ToString("R");
            // Only the people this bar has earned. Someone is always available — the
            // 0-star set never empties — so this cannot starve.
            var open = new List<PatronLook>();
            float standing = Run != null ? (float)Run.Rating.Average : 0f;
            foreach (var look in _looks)
                if (look.Stars <= standing + 0.001f) open.Add(look);
            if (open.Count == 0) open.Add(_looks[0]);

            int start;
            unchecked
            {
                int h = 17;
                for (int i = 0; i < key.Length; i++) h = h * 31 + key[i];
                start = Mathf.Abs(h) % open.Count;
            }
            // NO TWO OF THE SAME PERSON IN THE ROOM (the author, 2026-08-10). Each drawing
            // IS a character, so the same face on two stools reads as a bug rather than as
            // a coincidence. The hash still decides WHO — it stays deterministic under the
            // seed — and a collision simply walks to the next free face.
            for (int step = 0; step < open.Count; step++)
            {
                var candidate = open[(start + step) % open.Count];
                bool taken = false;
                foreach (var seat in _seats)
                    if (seat.Visit != null && seat.Visit != visit && seat.Look == candidate)
                    { taken = true; break; }
                if (!taken) return candidate;
            }
            return open[start];     // more stools than faces: somebody has to double up
        }

        // ── day end ─────────────────────────────────────────────────────────────

        private void ShowDayEnd()
        {
            var run = Run;
            _dayEndStep = 0;   // the bill first; the market only after CONTINUE
            _dayEndPanel.gameObject.SetActive(true);
            RebuildDayEnd();
        }

        private void OnDayEndAdvance()
        {
            if (_dayEndStep == 0) { _dayEndStep = 1; Sfx.Play("click", 0.6f); RebuildDayEnd(); }
            else OnOpenTomorrow();
        }

        private void RebuildDayEnd()
        {
            var run = Run;
            _dayEndBill.gameObject.SetActive(_dayEndStep == 0);
            _dayEndTablet.gameObject.SetActive(_dayEndStep == 1);
            if (_billNext != null) _billNext.gameObject.SetActive(_dayEndStep == 0);
            _dayEndTitle.text = _dayEndStep == 0
                ? "LAST CALL — THE BOOKS" : "LAST CALL — ORDERING IN";
            if (_billNextLabel != null) _billNextLabel.text = "CONTINUE  →  THE ORDER";
            // The title belongs to the BILL. Over the market it printed above the device,
            // in the scrim, saying what the device already says.
            _dayEndTitle.gameObject.SetActive(_dayEndStep == 0);
            // A 136-wide button holds 2 lines of 8 CAPS; the arrow and the parenthetical
            // wrapped to three and pushed themselves out of it.
            _openTomorrowLabel.text = run.Day % 6 == 0 ? "START\nTUESDAY" : "OPEN\nTOMORROW";
            var floor = run.Floor;
            int served = 0, stormed = 0;
            foreach (var visit in floor.Finished)
                if (visit.State == VisitState.StormedOff) stormed++; else served++;
            var cfg = run.Config;

            // The bill: income over expenses, net in bold, then the debt stamp. All the
            // day's line items come straight off the run's itemised book (GDD 24 §7).
            int net = run.DayIncome - run.DayExpenses;
            string netColour = net >= 0 ? "2A5926" : "A62B44";
            string stamp = run.Ledger.DebtStrikes == 0 ? ""
                : $"\n\n<color=#A62B44>◆ IN THE RED — STRIKE {run.Ledger.DebtStrikes}/{DayLedger.StrikesToClose} ◆</color>";
            if (run.Ledger.DebtStrikes == DayLedger.StrikesToClose - 1)
                stamp += "\n<color=#A62B44>one more red day closes the bar</color>";

            // Receipt v2 (v5 P13): a till slip, not a summary panel. Header, then the drinks
            // that actually crossed the bar as line items, then the totals block. The lines are
            // taken from what was POURED (`visit.Served`) and priced at `PaidBase`, so a night
            // where the player misread somebody still adds up — a wrong drink is paid at its
            // own price, and listing menu prices instead would leave the bill short.
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{Rule}</b>");
            sb.AppendLine($"<b>   LAST CALL   </b>");
            sb.AppendLine($"   {CalendarFor(run.Day)} · {CrowdName(run.CrowdToday)}");
            sb.AppendLine($"<b>{Rule}</b>");

            var lines = new List<string>();
            var counts = new Dictionary<string, int>();
            var totals = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var visit in floor.Finished)
            {
                if (visit.Served == null || visit.PaidBase <= 0) continue;
                string name = visit.Served.Name.ToUpperInvariant();
                if (!counts.ContainsKey(name)) { counts[name] = 0; totals[name] = 0; order.Add(name); }
                counts[name]++;
                totals[name] += visit.PaidBase;
            }
            foreach (var name in order)
                lines.Add(Line($"{counts[name]}x {name}", $"${totals[name]}", null));
            if (lines.Count == 0)
                sb.AppendLine("<color=#9C8F80>   nothing sold</color>");
            else
                foreach (var line in lines) sb.AppendLine(line);

            sb.AppendLine($"<color=#9C8F80>{Rule}</color>");
            sb.AppendLine(Line("SALES", $"${run.DaySales}", null));
            sb.AppendLine(Line("TIPS", $"${run.DayTips}", null));
            sb.AppendLine(Line("RENT", $"-${run.DayRent}", "A62B44"));
            sb.AppendLine(Line("STOCK", $"-${run.DayStock}", "A62B44"));
            sb.AppendLine(Line("SHOP", $"-${run.DayUpgrades}", "A62B44"));
            sb.AppendLine($"<color=#9C8F80>{Rule}</color>");
            sb.AppendLine("<b>" + Line("NET", $"{(net >= 0 ? "+" : "-")}${Math.Abs(net)}", netColour) + "</b>");
            sb.AppendLine("<b>" + Line("TILL", $"${run.Money}", null) + "</b>");
            sb.AppendLine($"<color=#9C8F80>{Rule}</color>");

            // The footer a real slip carries: who came, how they left, and the standing they
            // left behind — the number tomorrow's crowd is actually drawn from.
            sb.AppendLine($"<color=#9C8F80>   {served} served · {stormed} walked</color>");
            sb.AppendLine($"<color=#9C8F80>   tonight {BarRating.ExactStarsFor(floor.AverageSatisfaction):0.0}* " +
                          $"· bar {run.Rating.Average:0.0}*</color>");
            sb.Append(stamp);
            _invoiceText.text = sb.ToString();

            // The tablet.
            foreach (Transform child in _offerRow) Destroy(child.gameObject);
            _tabletTill.text = $"${run.Money}";
            // Tonight's fitting, said ONCE. It used to appear in five places — a band, a
            // rail note, the stool's tip, the glassware tip and a toast — and the author
            // still met it as a surprise, because none of the five was beside the control
            // it governed. It sits at the end of the department bar now, with a lamp.
            bool room = run.CanFitTonight && !CartHasFitting();
            if (_fittingNote != null)
            {
                _fittingNote.text = room ? "1 FITTING TONIGHT" : "FITTING SPENT";
                _fittingNote.color = room ? ShopGreenDark : ShopCost;
            }
            if (_fittingLamp != null) _fittingLamp.color = room ? ShopGreenLit : ShopCost;
            var tabOn = ItemArt.Load("sh_k_tab_on") ?? ItemArt.Load("sh_tab_on");
            var tabOff = ItemArt.Load("sh_k_tab_off") ?? ItemArt.Load("sh_tab_off");
            for (int i = 0; i < _shopTabKeys.Length; i++)
            {
                bool on = i == _shopTab;
                if (tabOn != null && tabOff != null)
                {
                    _shopTabKeys[i].sprite = on ? tabOn : tabOff;
                    _shopTabKeys[i].color = Color.white;
                }
                else _shopTabKeys[i].color = on ? ShopGreen : Color.white;
                // White type on the green key, ink on the resting one — the contrast is
                // the whole point (the author: the dark keys could not be read).
                _shopTabLabels[i].color = on ? Color.white : ShopInk;
                if (_shopTabIcons[i] != null && _shopTabIcons[i].sprite != null)
                    _shopTabIcons[i].color = on ? Color.white : Color.white;
            }

            // The order LISTS what is in it, at full length. The 18-character cut existed
            // because the panel was competing with a per-line price; the total moved to its
            // own row, which bought the names their whole width back.
            if (_cartHeadLabel != null)
                _cartHeadLabel.text = _cart.Count == 0 ? "ORDER" : $"ORDER ({_cart.Count})";
            if (_cartLine != null)
            {
                if (_cart.Count == 0) _cartLine.text = "Nothing picked yet.";
                else
                {
                    var basket = new StringBuilder();
                    int shown = Mathf.Min(4, _cart.Count);
                    for (int i = 0; i < shown; i++) basket.Append(_cart[i].Label).Append('\n');
                    if (_cart.Count > shown)
                        basket.Append('+').Append(_cart.Count - shown).Append(" more");
                    _cartLine.text = basket.ToString();
                }
            }
            if (_cartTotal != null)
                _cartTotal.text = _cart.Count == 0 ? "" : "$" + CartTotal();
            if (_checkoutLabel != null)
                _checkoutLabel.text = _cart.Count == 0 ? "NOTHING PICKED" : "PLACE ORDER";
            if (_osClock != null) _osClock.text = $"DAY {run.Day}";

            if (_dayEndStep == 0) return;   // the bill step shows no shop at all
            if (_shopTab == 0)
            {
                // RESTOCK. One band, not two: "everything at once" and "bottle by bottle"
                // were one errand split down the middle for no reason.
                _cardTarget = ShopSection("THE WELL");
                int restock = run.Shelf.RefillCost(cfg.RefillPricePerCapacity);
                var all = new TileSpec
                {
                    Name = "Restock the Whole Well",
                    Meta = "Every bottle to the brim",
                    // A CRATE, not the department icon it was borrowing — the errand
                    // and the tab it lives under were drawing the same thing.
                    Art = ItemArt.Load("sh_p_crate") ?? ItemArt.Load("sh_i_restock"),
                    Identity = "RESTOCK THE WHOLE WELL",
                    MetaLine = "Delivered before you open",
                    Body = "Tops up every bottle behind the bar in one line, at $"
                           + cfg.RefillPricePerCapacity + " a measure.",
                };
                if (restock > 0)
                {
                    all.BuffA = new Buff(BuffKind.Cost, "$" + cfg.RefillPricePerCapacity
                        + " a measure · " + restock + " to fill the shelf");
                    all.BuffB = new Buff(BuffKind.Gain,
                        "Covers every bottle below — you cannot need both");
                    DressBuyable(all, restock, "restock:all", false, () => run.RefillShelf());
                }
                else
                {
                    all.State = TileState.Held;
                    all.Word = "FULL";
                    all.BuffA = new Buff(BuffKind.Gain, "Nothing to pour away — every bottle is at the brim.");
                }
                AddTile(all);

                // WHAT NEEDS POURING COMES FIRST (the author). A restock page whose top row
                // is six full bottles makes the player scroll to find the errand they came
                // for; sorting by what is missing puts the emptiest bottle where the eye
                // already is. Ties keep the shelf's own order, so the page does not
                // reshuffle under the pointer as levels change.
                var shelf = new List<ShelfBottle>(run.Shelf.Bottles);
                shelf.Sort((x, y) =>
                {
                    double mx = x.Capacity - x.Remaining, my = y.Capacity - y.Remaining;
                    return my.CompareTo(mx);
                });
                // THE WHOLE WELL COVERS EVERY BOTTLE IN IT (the author). Both could sit in
                // the same order, and the player paid twice for the same measure: the
                // restock-all tops up every bottle, so a per-bottle refill picked beside it
                // buys nothing. The tile says so instead of taking the money — and picking
                // the whole well throws the singles back out of the order, because the
                // basket is the place where "you already have this" has to be true.
                bool wellOrdered = InCart("restock:all") || _justOrdered.Contains("restock:all");
                if (InCart("restock:all"))
                    for (int i = _cart.Count - 1; i >= 0; i--)
                        if (_cart[i].Key != null && _cart[i].Key.StartsWith("refill:"))
                            _cart.RemoveAt(i);

                foreach (var b in shelf)
                {
                    var bottle = b;
                    int cost = (int)Math.Ceiling((bottle.Capacity - bottle.Remaining)
                        * cfg.RefillPricePerCapacity);
                    var spec = new TileSpec
                    {
                        Name = bottle.Ingredient.Name,
                        Art = ItemArt.Bottle(bottle.Ingredient),
                        // The one fact this department exists to show, and it was line 5 or 6
                        // of a 3-line box — i.e. never once rendered. It is a bar now.
                        StockFrac = bottle.Capacity > 0
                            ? (float)(bottle.Remaining / bottle.Capacity) : 0f,
                    };
                    DescribeBottle(spec, bottle.Ingredient, bottle);
                    if (cost > 0 && !wellOrdered)
                        DressBuyable(spec, cost, "refill:" + bottle.Ingredient.Id, false,
                            () => run.RefillBottle(bottle.Ingredient.Id));
                    else if (cost > 0)
                    {
                        spec.State = TileState.Held;
                        spec.Word = "IN";                       // 2 CAPS, 26.5 in a 66 slot
                        spec.BuffA = new Buff(BuffKind.Gain,
                            "Covered by the whole-well order — no need to buy it twice");
                    }
                    else { spec.State = TileState.Held; spec.Word = "FULL"; }
                    AddTile(spec);
                }
            }
            else if (_shopTab == 1 || _shopTab == 2)
            {
                // ONE LOOP, TWO AISLES. The board is rolled whole by Core; which half of it
                // a bottle belongs to is a question about the bottle, not about the roll.
                bool booze = _shopTab == 1;
                _cardTarget = ShopSection(booze ? "TONIGHT'S BOARD" : "THE MIXER BOARD");
                bool anyKeg = false, anyGarnish = false;
                for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < run.MarketOffers.Count; i++)
                {
                    int index = i;
                    var offer = run.MarketOffers[i];
                    var card = offer.Bottle;
                    if (IngredientCategories.IsAlcoholic(card.Info?.Category, card.Type) != booze)
                        continue;
                    // A keg is not a bottle — 24 measures against 6, and the only beer drink
                    // on the book takes no ratio bands at all — so it gets its own aisle sign
                    // rather than standing unlabelled in a row of spirits. Same for the two
                    // garnishes, which are not mixers.
                    bool second = booze ? card.Type == IngredientType.Beer
                                        : card.Type == IngredientType.Garnish;
                    if ((pass == 1) != second) continue;
                    if (pass == 1 && booze && !anyKeg)
                    { anyKeg = true; _cardTarget = ShopSection("ON TAP — THE KEGS"); }
                    if (pass == 1 && !booze && !anyGarnish)
                    { anyGarnish = true; _cardTarget = ShopSection("THE GARNISH TRAY"); }
                    var spec = new TileSpec
                    {
                        Name = offer.Bottle.Name,
                        Art = ItemArt.Bottle(offer.Bottle),
                    };
                    DescribeBottle(spec, offer.Bottle, null);
                    // "New stock" is a fact about the offer, not a prefix on the name —
                    // the old "+ " and "↑ " spent two cells drawing literally nothing,
                    // because neither glyph is in any of the three installed faces.
                    if (offer.IsNewStock)
                        spec.MetaLine = "New on the board tonight · " + spec.MetaLine;
                    if (offer.Sold) { spec.State = TileState.Ordered; spec.Word = "SOLD"; }
                    else DressBuyable(spec, offer.Price, "brand:" + offer.Bottle.Id, false,
                        () => run.BuyBrand(index));
                    AddTile(spec);
                }
            }
            else if (_shopTab == 3)
            {
                _cardTarget = ShopSection("THE RECIPE BOOK");
                // LOWEST GATE FIRST (the author). The book is a ladder — what opens next
                // is the only thing on it the player can act on — and it was listing in
                // catalogue order, so the drink three stars away sat above the one that
                // unseals tonight. Ties keep the catalogue's order.
                var book = new List<RecipeDefinition>(run.LockedRecipes);
                book.Sort((a, b) => run.RecipeStarGate(a).CompareTo(run.RecipeStarGate(b)));
                foreach (var recipe in book)
                {
                    var r = recipe;
                    double gate = run.RecipeStarGate(r);
                    if (run.Rating.Average < gate)
                    {
                        // SEALED, and the name never reaches the tile — that is the whole
                        // mechanic. No art either: the empty well is the tell.
                        AddTile(new TileSpec
                        {
                            Name = "Sealed Crate",
                            Meta = "Sealed",
                            Money = gate.ToString("0.0") + "★",
                            State = TileState.Sealed,
                            Identity = "A SEALED CRATE",
                            MetaLine = "The house will not open this one for you yet",
                            Body = "It unseals at " + gate.ToString("0.0")
                                   + " stars. Keep the room happy and it opens itself.",
                            BuffA = new Buff(BuffKind.Bad, "Locked until the bar is worth "
                                             + gate.ToString("0.0") + " stars"),
                        });
                        continue;
                    }
                    var spec = new TileSpec
                    {
                        Name = r.Name,
                        Meta = PrepWord(r) + " · " + GlassNameFor(r),
                        Art = DrinkIcon.For(r, _bootstrap.Glassware),
                        ArtH = IconH,
                        Identity = r.Name.ToUpperInvariant(),
                        MetaLine = PrepWord(r) + " · served in a " + GlassNameFor(r),
                        Body = BandLine(r),
                        BuffA = new Buff(BuffKind.Gain, "On the menu tomorrow — one more drink to sell"),
                    };
                    DressBuyable(spec, run.RecipePrice(r), "recipe:" + r.Id, false,
                        () => run.UnlockRecipe(r.Id));
                    AddTile(spec);
                }
            }
            else
            {
                _cardTarget = ShopSection("THE ROOM");
                var stool = new TileSpec
                {
                    Name = "One More Stool",
                    Meta = "Seat " + Math.Min(run.Seats + 1, cfg.MaxSeats) + " of " + cfg.MaxSeats,
                    Art = ItemArt.Load("sh_i_upgrades"),
                    ArtH = IconH,
                    Identity = "ONE MORE STOOL",
                    MetaLine = "The floor · seat " + Math.Min(run.Seats + 1, cfg.MaxSeats)
                               + " of " + cfg.MaxSeats,
                    Body = "One more drinker at the bar at a time.",
                    BuffA = new Buff(BuffKind.Gain, "+1 seat · +0.25 stars on what the bar can be worth"),
                    BuffB = new Buff(BuffKind.Bad, "Counts as tonight's one fitting"),
                };
                if (run.Seats >= cfg.MaxSeats) { stool.State = TileState.Held; stool.Word = "MAX"; }
                else DressBuyable(stool, cfg.SeatPrice(run.Seats), "seat", true, () => run.BuySeat());
                AddTile(stool);

                // THE COUNTER. It has been a real, priced, guarded fitting in Core the whole
                // time — BuyCounter, CounterPrice, two steps at $40 and $80, worth up to 0.06
                // satisfaction on EVERY served visit — and it had no tile in any department,
                // so CounterTier was permanently 1 in every run that was not a dev preset and
                // a third of the ambience ceiling was dead weight. Found by counting what the
                // data offers against what the shop can show (2026-08-09).
                var bar = new TileSpec
                {
                    Name = "Resurface the Bar",
                    Meta = "Rung " + run.CounterTier + " of " + cfg.MaxAmbienceTier,
                    Art = ItemArt.Load("sh_p_bar") ?? ItemArt.Load("sh_i2_upgrades"),
                    ArtH = IconH,
                    Identity = "RESURFACE THE BAR",
                    MetaLine = "The room · rung " + run.CounterTier + " of " + cfg.MaxAmbienceTier,
                    Body = "A better bar top is the first thing anyone leans on. "
                           + "It lifts the mood of every visit, not just the ones you get right.",
                    BuffA = new Buff(BuffKind.Gain, "+0.03 on every served visit, up to +0.06"),
                    BuffB = new Buff(BuffKind.Bad, "Counts as tonight's one fitting"),
                };
                if (run.CounterTier >= cfg.MaxAmbienceTier)
                { bar.State = TileState.Held; bar.Word = "MAX"; }
                else DressBuyable(bar, cfg.CounterPrice(run.CounterTier), "counter", true,
                    () => run.BuyCounter());
                AddTile(bar);

                foreach (var g in run.Glassware)
                {
                    var glass = g;
                    int tier = run.GlassTier(glass.Id);
                    bool maxed = tier >= TycoonRun.MaxGlassTier;
                    int stepPrice = maxed ? 0 : glass.TierPrices[tier - 1];
                    var spec = new TileSpec
                    {
                        Name = glass.Name,
                        // "{tier-1}★ → {tier}★" spent 4 of its 16 characters drawing nothing.
                        Meta = "Rung " + tier + " of " + TycoonRun.MaxGlassTier,
                        Art = GlassArt.For(glass, Mathf.Min(tier + 1, TycoonRun.MaxGlassTier)).Sprite,
                        ArtH = VesselH,
                        Identity = glass.Name.ToUpperInvariant() + " GLASSWARE",
                        MetaLine = "Rung " + tier + " of " + TycoonRun.MaxGlassTier
                                   + " · " + DrinksServedIn(glass.Id),
                        Body = "A finer set lifts every visit's mood and raises what the bar can be worth.",
                        BuffA = new Buff(BuffKind.Gain, "+1 rung on the " + glass.Name.ToLowerInvariant()
                                         + " line · every drink served in one"),
                        BuffB = new Buff(BuffKind.Bad, "Counts as tonight's one fitting"),
                    };
                    if (maxed) { spec.State = TileState.Held; spec.Word = "MAX"; }
                    else DressBuyable(spec, stepPrice, "glass:" + glass.Id, true,
                        () => run.BuyGlassTier(glass.Id));
                    AddTile(spec);
                }
            }

            // WHAT THE NEXT STAR OPENS (the author). The board only shows what the room's
            // standing already allows, so the stock waiting behind the next rung was
            // invisible — the player could not tell an empty aisle from a finished one.
            // Core answers both halves: how many cards are gated, and at what.
            if (_shopTab == 1 || _shopTab == 2)
            {
                bool boozeTab = _shopTab == 1;
                int locked = 0;
                double next = double.MaxValue;
                foreach (var g in run.GatedStock())
                {
                    if (IngredientCategories.IsAlcoholic(g.Card.Info?.Category, g.Card.Type) != boozeTab)
                        continue;
                    locked++;
                    if (g.Stars < next) next = g.Stars;
                }
                if (locked > 0)
                    AddTile(new TileSpec
                    {
                        Name = locked + (locked == 1 ? " more waiting" : " more waiting"),
                        Money = next.ToString("0.0") + "★",
                        State = TileState.Sealed,
                        Identity = "MORE STOCK AT " + next.ToString("0.0") + " STARS",
                        MetaLine = locked + (locked == 1 ? " line" : " lines")
                                   + " the van will not bring you yet",
                        Body = "The board is rolled against what the room thinks of this bar. "
                               + "Reach " + next.ToString("0.0")
                               + " stars and the next of them start appearing here.",
                        BuffA = new Buff(BuffKind.Bad, "Needs " + next.ToString("0.0")
                                         + " stars · you are at " + run.Rating.Average.ToString("0.0")),
                    });
            }

            // A DEPARTMENT WITH NOTHING IN IT SAYS SO. Splitting the board in two means
            // either half can legitimately be empty on a given night — the van simply did
            // not bring any mixers — and a bare aisle sign with nothing under it reads as
            // a bug rather than as an answer.
            if (_cardTarget != null && _cardTarget.childCount == 0)
                AddTile(new TileSpec
                {
                    Name = "Nothing tonight",
                    Meta = "Try again tomorrow",
                    State = TileState.Held,
                    Identity = "NOTHING ON THIS BOARD TONIGHT",
                    MetaLine = "The van brings a different list every night",
                    Body = "What the market offers is rolled at each close, against what is "
                           + "already behind your bar and what the room thinks of it.",
                });

            // The refund slip rides every tab: anything bought at THIS close can go back.
            // It buys immediately — a return is not something you put in a basket.
            if (run.TodaysPurchases.Count > 0)
            {
                _cardTarget = ShopSection("ORDERED TONIGHT — CLICK TO RETURN");
                for (int i = 0; i < run.TodaysPurchases.Count; i++)
                {
                    int idx = i;
                    var pch = run.TodaysPurchases[i];
                    AddTile(new TileSpec
                    {
                        // Silkscreen cannot draw U+2605, and the label carries one.
                        Name = pch.Name.Replace('★', '*'),
                        Meta = "Ordered tonight",
                        Money = "+$" + pch.Price,
                        State = TileState.Refundable,
                        PillVerb = "RETURN",
                        Art = RefundArt(pch),
                        ArtH = pch.What == TycoonRun.DayPurchase.Kind.Glassware ? VesselH
                             : pch.What == TycoonRun.DayPurchase.Kind.Recipe ? IconH : BottleH,
                        OnClick = () => { run.RefundToday(idx); Toast("RETURNED");
                                          ApplyBarLook(); RebuildDayEnd(); },
                        Identity = pch.Name.Replace('★', '*').ToUpperInvariant(),
                        MetaLine = "On tonight's van · $" + pch.Price,
                        Body = "Send it back and the till is made whole.",
                        BuffA = new Buff(BuffKind.Gain, "Refunds $" + pch.Price
                            + (pch.What == TycoonRun.DayPurchase.Kind.Seat
                               || pch.What == TycoonRun.DayPurchase.Kind.Glassware
                               ? " and gives the night its fitting back" : "")),
                    });
                }
            }
            // The inspector starts on its idle line rather than as an empty black slab —
            // and this is also what switches the two buff icons off, which otherwise sit
            // there as white squares with no sprite in them.
            ShowInspector(null);

            // The aisle stays where it was left (the author: picking something must not
            // throw you back to the top). Switching department is what resets it.
            Canvas.ForceUpdateCanvases();
            if (_shopScroll != null) _shopScroll.verticalNormalizedPosition = _shopScrollAt;
        }

        // ── what a listing IS, in words (the author, 2026-08-07) ─────────────────

        /// <summary>
        /// A bottle, said in the inspector's five rows instead of one shouted blob: what it
        /// is, how good it is, what the bar pours it into, and — for a bottle already behind
        /// the bar — how much is left. Sentence case throughout; the whole market used to be
        /// upper-cased, which is a third of the author's complaint about the descriptions.
        /// </summary>
        private void DescribeBottle(TileSpec spec, IngredientCard card, ShelfBottle onShelf)
        {
            if (card == null) return;
            string style = card.Info?.Style;
            string styleWord = string.IsNullOrEmpty(style) ? "" : style.Replace('_', ' ');
            int tier = card.Info?.Tier ?? 1;

            // THE NAME, ALONE, IN BOLD BESIDE ITS PICTURE (the author). The style used to
            // ride the same row — "SMIRKOFF VODKA · VODKA" — which made the heading a
            // sentence and left the mark next to a fact rather than next to a title. The
            // style is a property of the bottle, so it goes on the property row.
            spec.Identity = card.Name.ToUpperInvariant();
            // The ABV lives HERE, not on the identity line: "GRAND MARINER TRIPLE SEC ·
            // TRIPLE SEC" is already 37 of the 46 characters that fit.
            var meta = new StringBuilder();
            if (styleWord.Length > 0)
                meta.Append(char.ToUpperInvariant(styleWord[0])).Append(styleWord.Substring(1))
                    .Append(" · ");
            meta.Append("Rung ").Append(tier).Append(" of 4");
            if (card.Info != null && card.Info.Abv > 0)
                meta.Append(" · ").Append(card.Info.Abv).Append("% ABV");
            if (onShelf != null && onShelf.Capacity > 0)
                meta.Append(" · ")
                    .Append((int)Math.Round(onShelf.Remaining / onShelf.Capacity * 100))
                    .Append("% left behind the bar");
            spec.MetaLine = meta.ToString();
            // The tile shows the STOCK BAR, so the tile's own meta stays empty here and the
            // two never say the same thing twice.
            if (styleWord.Length > 0 && spec.StockFrac < 0f)
                spec.Meta = char.ToUpperInvariant(styleWord[0]) + styleWord.Substring(1)
                    + (card.Info != null && card.Info.Abv > 0 ? " · " + card.Info.Abv + "%" : "");

            // What it is FOR: the drinks whose bands name this style — and only the drinks
            // the bar can actually pour (Core filters it; see MenuDrinksUsingStyle).
            var uses = new List<string>();
            if (Run != null)
                foreach (var r in Run.MenuDrinksUsingStyle(style))
                {
                    if (!uses.Contains(r.Name)) uses.Add(r.Name);
                    if (uses.Count >= 5) break;
                }
            spec.Body = uses.Count > 0 ? "Poured into: " + string.Join(", ", uses) + "."
                : card.Type == IngredientType.Beer ? "Pulled at the tap."
                : "No drink on the book calls for it yet.";
            spec.BuffA = uses.Count > 0
                ? new Buff(BuffKind.Use, uses.Count + (uses.Count == 1 ? " drink" : " drinks")
                           + " on the menu call for it")
                : new Buff(BuffKind.Bad, "Nothing on tonight's menu calls for it");
            if (tier > 1)
                spec.BuffB = new Buff(BuffKind.Gain,
                    "Joins the shelf; the well bottle stays");
        }

        /// <summary>
        /// Which state a buyable listing is in, and the control that goes with it. One
        /// if/else chain, so the predicates cannot both fire: they genuinely overlap (a
        /// picked fitting is also a fitting the night has no room for), and without an
        /// order the same tile could render two ways.
        /// </summary>
        private void DressBuyable(TileSpec spec, int price, string cartKey, bool isFitting,
            Action buy)
        {
            bool sold = cartKey != null && _justOrdered.Contains(cartKey);
            bool picked = cartKey != null && InCart(cartKey);
            // What the till has left AFTER the order already in the basket — checkout charges
            // the whole basket at once, so this is the only honest reading of "afford".
            bool afford = Run.Money - CartTotal() >= price;
            bool noRoom = isFitting && (!Run.CanFitTonight || CartHasFitting());

            spec.Money = "$" + price;
            if (sold)
            {
                spec.State = TileState.Ordered;
                spec.Money = null; spec.Word = "SOLD";
                return;
            }
            if (picked) { spec.State = TileState.Picked; spec.PillVerb = "TAKE OUT"; }
            else if (noRoom) { spec.State = TileState.NoFitting; spec.PillVerb = "NO SLOT"; }
            else if (!afford) { spec.State = TileState.Unaffordable; spec.PillVerb = "NO CASH"; }
            else { spec.State = TileState.Orderable; spec.PillVerb = "ADD"; }
            string label = spec.Name;
            spec.OnClick = () => ToggleCart(cartKey, label, price, isFitting, buy);
        }

        private bool CartHasFitting()
        {
            foreach (var e in _cart) if (e.IsFitting) return true;
            return false;
        }

        /// <summary>The glass a recipe is served in, by name.</summary>
        private string GlassNameFor(RecipeDefinition r)
        {
            if (Run != null)
                foreach (var g in Run.Glassware)
                    if (g.Id == r.GlassId) return g.Name;
            return "glass";
        }

        /// <summary>The picture for a refund row, resolved from what was bought. The purchase
        /// record carries a kind and an id, which is enough — no Core change needed.</summary>
        private Sprite RefundArt(TycoonRun.DayPurchase pch)
        {
            if (Run == null) return null;
            switch (pch.What)
            {
                case TycoonRun.DayPurchase.Kind.Brand:
                    var bottle = Run.Shelf.Find(pch.Id);
                    return bottle != null ? ItemArt.Bottle(bottle.Ingredient) : null;
                case TycoonRun.DayPurchase.Kind.Recipe:
                    foreach (var r in Run.AllRecipes)
                        if (r.Id == pch.Id) return DrinkIcon.For(r, _bootstrap.Glassware);
                    return null;
                case TycoonRun.DayPurchase.Kind.Glassware:
                    foreach (var g in Run.Glassware)
                        if (g.Id == pch.Id) return GlassArt.For(g, Run.GlassTier(g.Id)).Sprite;
                    return null;
                default:
                    return ItemArt.Load("sh_i_upgrades");
            }
        }

        /// <summary>The drinks a glass line actually serves, for its upgrade card. On the
        /// MENU only — a glassware card that listed sealed drinks leaked them exactly as the
        /// bottle card did (2026-08-09).</summary>
        private string DrinksServedIn(string glassId)
        {
            var names = new List<string>();
            if (Run != null)
                foreach (var r in Run.MenuDrinksInGlass(glassId))
                    if (!names.Contains(r.Name))
                    {
                        names.Add(r.Name);
                        if (names.Count >= 4) break;
                    }
            return names.Count == 0 ? "NOTHING ON THE BOOK YET."
                : string.Join(", ", names).ToUpperInvariant() + ".";
        }

        // ── the basket ───────────────────────────────────────────────────────────

        private bool InCart(string key)
        {
            foreach (var e in _cart) if (e.Key == key) return true;
            return false;
        }

        private int CartTotal()
        {
            int n = 0;
            foreach (var e in _cart) n += e.Price;
            return n;
        }

        /// <summary>Notes where the aisle is standing, so a rebuild can put it back.</summary>
        private void RememberScroll()
        {
            if (_shopScroll != null) _shopScrollAt = _shopScroll.verticalNormalizedPosition;
        }

        /// <summary>Picks a listing up, or puts it back. Refuses what the night cannot
        /// carry: a second fitting, or more money than the till holds.</summary>
        private void ToggleCart(string key, string label, int price, bool isFitting, Action buy)
        {
            RememberScroll();
            for (int i = 0; i < _cart.Count; i++)
                if (_cart[i].Key == key)
                {
                    _cart.RemoveAt(i);
                    Sfx.Play("click", 0.5f);
                    RebuildDayEnd();
                    return;
                }

            if (isFitting)
            {
                if (!Run.CanFitTonight) { Toast("ONE FITTING A NIGHT"); return; }
                foreach (var e in _cart)
                    if (e.IsFitting) { Toast("ONE FITTING A NIGHT"); return; }
            }
            if (CartTotal() + price > Run.Money) { Toast("NOT ENOUGH MONEY"); return; }

            _cart.Add(new CartEntry { Key = key, Label = label, Price = price,
                                      IsFitting = isFitting, Buy = buy });
            Sfx.Play("click", 0.7f);
            RebuildDayEnd();
        }

        /// <summary>Places the order: every picked line bought in the order it was picked.
        /// A refusal stops the rest — the till is the shop's word, not the basket's.</summary>
        private void Checkout()
        {
            if (_cart.Count == 0) { Toast("BASKET IS EMPTY"); return; }
            RememberScroll();
            _justOrdered.Clear();
            int bought = 0;
            foreach (var e in _cart)
            {
                try { e.Buy(); _justOrdered.Add(e.Key); bought++; }
                catch (InvalidOperationException) { Toast("ORDER STOPPED — " + e.Label); break; }
            }
            _cart.Clear();
            Sfx.Play("cash", 0.9f);
            Toast(bought == 1 ? "1 LINE ORDERED" : $"{bought} LINES ORDERED");
            ApplyBarLook();
            RebuildDayEnd();
        }

        /// <summary>The stamp, on the CHIP. It used to be a 160-wide rotated word laid
        /// across a 190 card, printing straight through the name underneath it; the tile
        /// says "sold" with a strip, a plate and a van, so the stamp only has to land.
        /// </summary>
        private System.Collections.IEnumerator StampDrop(RectTransform rt)
        {
            const float dur = 0.16f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                float s = Mathf.Lerp(2.6f, 0.94f, k * k);      // slams down
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            for (float t = 0; t < 0.06f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.06f;
                float s = Mathf.Lerp(0.94f, 1f, k);            // and settles
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// Fills the inspector from whatever the pointer is over, or empties it back to the
        /// idle line. This is the ONLY place long text is written in the market: the tile
        /// carries what identifies a thing, the inspector carries what explains it, and the
        /// two sets are disjoint by construction because they come from one TileSpec.
        /// </summary>
        private void ShowInspector(TileSpec spec)
        {
            if (_inspIdentity == null) return;
            if (_inspMarkImg != null)
            {
                _inspMarkImg.enabled = spec != null && spec.Art != null;
                if (spec != null) _inspMarkImg.sprite = spec.Art;
            }
            if (spec == null)
            {
                _inspIdentity.text = "";
                _inspMeta.text = "";
                _inspBody.text = ShopIdleTip;
                _inspBuffA.text = ""; _inspBuffB.text = "";
                _inspBuffAIcon.enabled = false; _inspBuffBIcon.enabled = false;
                return;
            }
            // The mark alone said "a bottle"; the NAME beside it says which. The identity
            // row already carried it, but a hundred units to the right of the picture it
            // belongs to — so the two read as separate facts about the same tile.
            _inspIdentity.text = spec.Identity ?? "";
            _inspMeta.text = spec.MetaLine ?? "";
            _inspBody.text = spec.Body ?? "";
            WriteBuff(_inspBuffA, _inspBuffAIcon, spec.BuffA);
            WriteBuff(_inspBuffB, _inspBuffBIcon, spec.BuffB);
        }

        /// <summary>One effect line: a colour AND its own icon. Colour alone would leave the
        /// three kinds indistinguishable for anyone who cannot separate them, and the icons
        /// are one-to-one with the kinds for exactly that reason.</summary>
        private void WriteBuff(Text line, Image icon, Buff buff)
        {
            if (buff == null || string.IsNullOrEmpty(buff.Text))
            {
                line.text = "";
                icon.enabled = false;
                return;
            }
            line.text = buff.Text;
            line.color = buff.Kind == BuffKind.Gain ? BuffGood
                : buff.Kind == BuffKind.Cost ? BuffCost
                : buff.Kind == BuffKind.Bad ? BuffBad : InspectorInk;
            icon.enabled = true;
            icon.color = line.color;
            icon.sprite = ItemArt.Load(
                buff.Kind == BuffKind.Gain ? "sh_b_star"
                : buff.Kind == BuffKind.Cost ? "sh_b_coin"
                : buff.Kind == BuffKind.Bad ? "sh_b_lock" : "sh_b_pour");
        }

        /// <summary>A titled section of the market: its header row, then its own grid.</summary>
        private RectTransform ShopSection(string title)
        {
            // An aisle sign: a coloured tick, the name in the signage colour, and a rule
            // running out to the edge — how a storefront heads a shelf.
            // An aisle sign that actually signs the aisle (the author: "başlıkların daha ön
            // plana çıkması lazım") — a solid green band with the name in white, not a
            // grey line of small caps lost between two rows of cards.
            var h = NewRect("SH", _offerRow);
            h.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            var band = NewRect("Band", h);
            Stretch(band, Vector2.zero, Vector2.one, new Vector2(0, 2), new Vector2(0, -2));
            band.gameObject.AddComponent<Image>().color = ShopGreenDark;
            var pip = NewRect("Pip", band);
            Place(pip, new Vector2(0, 0.5f), new Vector2(6, 18), new Vector2(10, 0));
            pip.gameObject.AddComponent<Image>().color = ShopGreenLit;
            var t = NewText("T", band, _shop, 16, TextAnchor.MiddleLeft, Color.white);
            Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(700, 18), new Vector2(26, 0));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = title;

            var sec = NewRect("Sec", _offerRow);
            var g = sec.gameObject.AddComponent<GridLayoutGroup>();
            // SIX across, and the arithmetic is the point: 6*160 + 5*8 = 1000 in a 1004
            // viewport. The grid runs from screen x 8 to 1008, the mask cuts at 1012 and the
            // scroll track begins at 1022 — 4 units inside the mask, 14 units of air before
            // the bar. The old line claimed "leaving 790" against a viewport that has never
            // been 790: it was 730, so a third of every fourth card — its whole pill and
            // pick-mark — was masked away, which is what the author saw run under the bar.
            // No padding and no centring: the 4 units of slack must stay on the right.
            g.cellSize = new Vector2(TileW, TileH);
            g.spacing = new Vector2(8, 8);
            g.padding = new RectOffset(0, 0, 0, 0);
            g.childAlignment = TextAnchor.UpperLeft;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 6;
            return sec;
        }

        // ── the service log (dev tooling, 2026-07-31) ────────────────────────────
        // Top-left, always on for now: one line per service event — what was ordered, what
        // the judge said and WHY it scored the way it did (match, spec, fill, craft), what
        // it paid, and the stars it left. The author's in-play instrument for the balance
        // work: the sim report says what 200 runs did; this says what THIS serve just did.

        private Text _serviceLog;
        private readonly List<string> _serviceLogLines = new List<string>();
        private const int ServiceLogMax = 9;

        private void BuildServiceLog(RectTransform root)
        {
            var panel = NewRect("ServiceLog", root);
            Place(panel, new Vector2(0, 1), new Vector2(430, 150), new Vector2(10, -66));
            panel.pivot = new Vector2(0, 1);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.55f);
            bg.raycastTarget = false;
            _serviceLog = NewText("Lines", panel, _body, 8, TextAnchor.UpperLeft, UITheme.TextSecondary);
            _serviceLog.supportRichText = true;
            Stretch(_serviceLog.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 4), new Vector2(-6, -4));
        }

        private void LogService(string line)
        {
            if (_serviceLog == null) return;
            _serviceLogLines.Insert(0, line);
            while (_serviceLogLines.Count > ServiceLogMax)
                _serviceLogLines.RemoveAt(_serviceLogLines.Count - 1);
            _serviceLog.text = string.Join("\n", _serviceLogLines);
        }

        private static string LogStars(double satisfaction) =>
            new string('★', Mathf.Clamp(Mathf.RoundToInt((float)satisfaction * 5f), 0, 5));

        /// <summary>The judge's verdict, said as one log line with its reasons.</summary>
        private void LogVerdict(CustomerVisit visit, ServiceVerdict verdict)
        {
            string ordered = visit.IdInspected ? visit.Order.Wanted.Name.ToUpperInvariant() : "?";
            string made = visit.Served != null ? visit.Served.Name.ToUpperInvariant() : "NOTHING NAMED";
            string col = verdict.Match == OrderMatch.Exact ? "8CE28C"
                : verdict.Match == OrderMatch.Close ? "F5C97B" : "F27D8A";
            var why = new List<string>();
            if (verdict.Match == OrderMatch.Wrong) why.Add($"made {made}");
            if (verdict.SpecScore < 0.999) why.Add($"spec {verdict.SpecScore:P0}");
            if (verdict.FillScore < 0.999) why.Add($"fill {verdict.FillScore:P0}");
            string reasons = why.Count > 0 ? "  <color=#9C8F80>(" + string.Join(", ", why) + ")</color>" : "";
            LogService($"<color=#{col}>{verdict.Match.ToString().ToUpperInvariant()}</color> {ordered}" +
                       $" · ${verdict.BasePaid}+${verdict.Tip} · {LogStars(verdict.Satisfaction)}{reasons}");
        }

        // ── the recipe book (v5 P16) ─────────────────────────────────────────────
        // The menu speaks styles now, so what is IN a drink has to be readable mid-shift:
        // an order for a Gimlet is unanswerable by a player who cannot look a Gimlet up.
        // Unlocked recipes print their full bands; locked ones show what tier and stars
        // they are waiting behind, so the book doubles as the progression map.

        private RectTransform _bookPanel, _bookList;
        // The filters (the author's spec): tier, prep and bottle, each a cycling chip.
        private int _bookTier = -1;              // -1 all, else 0..3
        private InputField _bookSearch;          // name substring, the author's "arama"
        private int _bookPrep = -1;              // -1 all, else (int)PrepMethod
        private string _bookStyle;               // null = all
        private Text _bookTierChip, _bookPrepChip, _bookStyleChip;
        private static readonly string[] BookTiers = { "STARTER", "MID SHELF", "TOP SHELF", "HOUSE PRIDE" };

        private static int TierIndex(int rank) => rank <= 8 ? 0 : rank <= 14 ? 1 : rank <= 21 ? 2 : 3;

        private List<string> BookStyles()
        {
            var run = Run;
            var seen = new List<string>();
            if (run == null) return seen;
            // The book LISTS MenuRecipes (BookAdmits, below), so its filter is cut from the
            // same cloth. Core decides which styles are sayable; this only sorts them.
            seen.AddRange(run.MenuStyles());
            seen.Sort(System.StringComparer.Ordinal);
            return seen;
        }

        private bool _bookOpen;
        private Coroutine _bookAnim;

        internal void ToggleRecipeBook()
        {
            if (_bookPanel == null) return;
            bool open = !_bookOpen;
            _bookOpen = open;
            Sfx.Play("click", 0.6f);
            var sheet = _bookPanel.Find("Sheet") as RectTransform;
            if (open)
            {
                if (!_bookPanel.gameObject.activeSelf)
                {
                    // First frame of the drop: board parked above the screen, scrim clear.
                    sheet.anchoredPosition = new Vector2(0, BkH);
                    var c = UITheme.Scrim;
                    _bookPanel.GetComponent<Image>().color = new Color(c.r, c.g, c.b, 0);
                }
                _bookPanel.gameObject.SetActive(true);
                _bookPanel.SetAsLastSibling();   // over the service log and everything else
                RebuildRecipeBook();
            }
            else CloseBookPopup();
            if (_bookAnim != null) StopCoroutine(_bookAnim);
            _bookAnim = StartCoroutine(BookSlide(open));
        }

        /// <summary>The board drops down from above and lifts back away (the author asked
        /// for a smooth open and close). Reduced motion snaps, as everywhere.</summary>
        private System.Collections.IEnumerator BookSlide(bool open)
        {
            var sheet = _bookPanel.Find("Sheet") as RectTransform;
            var scrim = _bookPanel.GetComponent<Image>();
            var c = UITheme.Scrim;
            float fromY = sheet.anchoredPosition.y, toY = open ? 0f : BkH;
            float fromA = scrim.color.a, toA = open ? c.a : 0f;
            if (!Motion.Reduced)
            {
                float dur = open ? 0.28f : 0.2f;
                for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
                {
                    float k = t / dur;
                    k = open ? 1f - (1f - k) * (1f - k) * (1f - k) : k * k * k;
                    sheet.anchoredPosition = new Vector2(0, Mathf.Lerp(fromY, toY, k));
                    scrim.color = new Color(c.r, c.g, c.b, Mathf.Lerp(fromA, toA, k));
                    yield return null;
                }
            }
            sheet.anchoredPosition = new Vector2(0, toY);
            scrim.color = new Color(c.r, c.g, c.b, toA);
            if (!open) _bookPanel.gameObject.SetActive(false);
            _bookAnim = null;
        }

        private void BuildRecipeBook(RectTransform root)
        {
            _bookPanel = NewRect("RecipeBook", root);
            Stretch(_bookPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _bookPanel.gameObject.AddComponent<Image>();
            scrim.color = UITheme.Scrim;
            // Only the X closes now (the author's ruling): the scrim blocks the bar behind
            // it but closes nothing. The book also outranks the back-bar flow (canvas 12):
            // its own canvas at 15 lets the BOOK key on the flow's ledge show the thing.
            var bookCanvas = _bookPanel.gameObject.AddComponent<Canvas>();
            bookCanvas.overrideSorting = true;
            bookCanvas.sortingOrder = 15;
            _bookPanel.gameObject.AddComponent<GraphicRaycaster>();

            // Promoted to THE menu (2026-08-01): recipes, search and filters live here now
            // that the clipboard is gone — so it takes the room a menu deserves.
            // The clipboard lives on in the book (2026-08-01, the author's ask): the wooden
            // board art, the paper region measured off it, the plate keys and the drawn X --
            // the old menu's whole visual language, now carrying the recipes instead of the
            // bottles. Paper constants are the flow's own measurements of menu_board.
            var sheet = NewRect("Sheet", _bookPanel);
            Place(sheet, new Vector2(0.5f, 0.5f), new Vector2(BkW, BkH), Vector2.zero);
            var boardImg = sheet.gameObject.AddComponent<Image>();
            var boardSprite = ItemArt.Load("menu_board");
            if (boardSprite != null) { boardImg.sprite = boardSprite; boardImg.preserveAspect = true; }
            else boardImg.color = UITheme.Cream[4];
            // The whole VISIBLE board swallows its clicks (the author, third ruling): the
            // sprite carries wide transparent margins, so a full-rect swallow ate the very
            // "outside" clicks meant to close the book. The catcher is sized to the board's
            // opaque pixels (measured off menu_board.png: 823x632 centred at -6,+6).
            boardImg.raycastTarget = false;
            var boardCatch = NewRect("BoardCatch", sheet);
            Place(boardCatch, new Vector2(0.5f, 0.5f), new Vector2(824, 634), new Vector2(-6, 6));
            var bcImg = boardCatch.gameObject.AddComponent<Image>();
            bcImg.color = new Color(0, 0, 0, 0.001f);
            boardCatch.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            // The X returns (the author): top-right of the board, and it is the ONLY way
            // out — outside clicks no longer close, so a stray click cannot eat the page.
            var closeRt = NewRect("Close", sheet);
            Place(closeRt, new Vector2(0.5f, 0.5f), new Vector2(56, 56), new Vector2(378f, 288f));
            var closeImg = closeRt.gameObject.AddComponent<Image>();
            var closeSprite = ItemArt.Load("btn_close");
            if (closeSprite != null) { closeImg.sprite = closeSprite; closeImg.preserveAspect = true; }
            else closeImg.color = new Color(0.62f, 0.15f, 0.17f);
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            var closeDown = ItemArt.Load("btn_close_down");
            if (closeSprite != null && closeDown != null)
            {
                closeBtn.transition = Selectable.Transition.SpriteSwap;
                var st = closeBtn.spriteState; st.pressedSprite = closeDown; st.selectedSprite = closeSprite;
                closeBtn.spriteState = st;
            }
            closeBtn.onClick.AddListener(ToggleRecipeBook);

            // The filter chips: click to cycle. Three axes the author named — the star tier,
            // how it is worked, and what bottle it contains.
            float chipY = 142f;   // just under the board's metal clip
            // One slim toolbar (the author: the filters should look professional and take
            // less room): search at the left, three compact value-pills at the right, all
            // inside the paper. A pill drops its option window under itself.
            float paperR = BkW * BkPaperCX + BkW * BkPaperW * 0.5f;
            float tierX = paperR - 282f, prepX = paperR - 184f, styleX = paperR - 76f;
            _bookTierChip = BookChip(sheet, tierX, chipY, 92f, () =>
            {
                var opts = new List<string> { "ALL", BookTiers[0], BookTiers[1], BookTiers[2], BookTiers[3] };
                OpenBookPopup(tierX, chipY, opts, pick => { _bookTier = pick - 1; RebuildRecipeBook(); });
            });
            _bookPrepChip = BookChip(sheet, prepX, chipY, 92f, () =>
            {
                var opts = new List<string> { "ALL", "BUILT", "SHAKEN", "STIRRED" };
                OpenBookPopup(prepX, chipY, opts, pick =>
                {
                    _bookPrep = pick == 0 ? -1
                        : pick == 1 ? (int)PrepMethod.Built
                        : pick == 2 ? (int)PrepMethod.Shaken : (int)PrepMethod.Stirred;
                    RebuildRecipeBook();
                });
            });
            _bookStyleChip = BookChip(sheet, styleX, chipY, 112f, () =>
            {
                var styles = BookStyles();
                var opts = new List<string> { "ALL" };
                foreach (var st in styles) opts.Add(st.Replace('_', ' ').ToUpperInvariant());
                OpenBookPopup(styleX, chipY, opts, pick =>
                {
                    _bookStyle = pick == 0 ? null : styles[pick - 1];
                    RebuildRecipeBook();
                });
            });

            // The search box: type a name, the list narrows as you do.
            var searchRt = NewRect("Search", sheet);
            Place(searchRt, new Vector2(0.5f, 0.5f), new Vector2(220, 26),
                new Vector2(BkW * BkPaperCX - BkW * BkPaperW * 0.5f + 130f, chipY));
            var searchBg = searchRt.gameObject.AddComponent<Image>();
            searchBg.color = new Color(0.94f, 0.90f, 0.80f);
            var searchText = NewText("T", searchRt, _body, 16, TextAnchor.MiddleLeft, new Color(0.16f, 0.10f, 0.06f));
            Stretch(searchText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
            searchText.supportRichText = false;
            var placeholder = NewText("P", searchRt, _body, 16, TextAnchor.MiddleLeft, new Color(0.5f, 0.42f, 0.32f));
            Stretch(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
            placeholder.text = "SEARCH…";
            _bookSearch = searchRt.gameObject.AddComponent<InputField>();
            _bookSearch.targetGraphic = searchBg;
            _bookSearch.textComponent = searchText;
            _bookSearch.placeholder = placeholder;
            _bookSearch.onValueChanged.AddListener(_ => RebuildRecipeBook());

            var viewport = NewRect("View", sheet);
            Place(viewport, new Vector2(0.5f, 0.5f),
                new Vector2(BkW * BkPaperW - 44f, 358f),
                new Vector2(BkW * BkPaperCX, -56f));
            viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.004f);
            viewport.gameObject.AddComponent<RectMask2D>();

            _bookList = NewRect("List", viewport);
            _bookList.anchorMin = new Vector2(0, 1); _bookList.anchorMax = new Vector2(1, 1);
            _bookList.pivot = new Vector2(0.5f, 1);
            _bookList.offsetMin = Vector2.zero; _bookList.offsetMax = Vector2.zero;
            // Sections (2026-08-01, the author: the groupings must read clearly): the list
            // is a stack of tier sections, each a full-width header over its own grid, so a
            // header can never land inside a column and the columns always align.
            var layout = _bookList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var fitter = _bookList.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = _bookList;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            _bookPanel.gameObject.SetActive(false);
        }

        // The board and its paper, as the flow measured them off menu_board.
        private const float BkW = 1148f, BkH = 719f;
        private const float BkPaperW = 0.655f, BkPaperH = 0.660f;
        private const float BkPaperCX = -0.015f, BkPaperCY = -0.008f;

        private Text BookChip(RectTransform sheet, float x, float y, float w, Action onClick)
        {
            // A compact value-pill: cream face, hairline ink frame, the value in small ink.
            var chip = NewRect("Chip", sheet);
            Place(chip, new Vector2(0.5f, 0.5f), new Vector2(w, 24), new Vector2(x, y));
            var img = chip.gameObject.AddComponent<Image>();
            img.color = new Color(0.95f, 0.92f, 0.82f, 0.95f);
            var btn = chip.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => { Sfx.Play("click", 0.5f); onClick(); });
            var frame = new Color(0.30f, 0.20f, 0.10f, 0.5f);
            Hairline(chip, new Vector2(0, 0), new Vector2(1, 0), frame);
            Hairline(chip, new Vector2(0, 1), new Vector2(1, 1), frame);
            HairlineV(chip, 0f, frame);
            HairlineV(chip, 1f, frame);
            var t = NewText("T", chip, _body, 8, TextAnchor.MiddleCenter, new Color(0.30f, 0.20f, 0.10f));
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return t;
        }

        /// <summary>How tall the board is, and where a plaque sits on it.</summary>
        private const float TopBarH = 54f, PlaqueH = 40f, PlaqueY = 3f;
        private const float StarSize = 14f, StarGap = 17f;

        /// <summary>
        /// One reading on the board: a recessed slab, lit along its top edge and seated on a
        /// rule in its own accent — the licence card's field, in neon rather than ink. The
        /// rule is handed back because it is also the state light: the clock's turns magenta
        /// at last call, the till's turns red in the red.
        /// </summary>
        private RectTransform TopPlaque(RectTransform parent, string name, Vector2 anchor,
            Vector2 size, Vector2 pos, Color accent, out Image rule)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var slab = rt.gameObject.AddComponent<Image>();
            slab.color = UITheme.Night[0];
            slab.raycastTarget = false;
            Hairline(rt, new Vector2(0, 1), new Vector2(1, 1), UITheme.Night[2]);

            var r = NewRect("Rule", rt);
            r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0);
            r.pivot = new Vector2(0.5f, 0);
            r.sizeDelta = new Vector2(0, 2);
            r.anchoredPosition = Vector2.zero;
            rule = r.gameObject.AddComponent<Image>();
            rule.color = accent;
            rule.raycastTarget = false;
            return rt;
        }

        private void Hairline(RectTransform parent, Vector2 aMin, Vector2 aMax, Color c)
        {
            var r = NewRect("HL", parent);
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.pivot = new Vector2(0.5f, aMin.y);
            r.sizeDelta = new Vector2(0, 1);
            r.anchoredPosition = Vector2.zero;
            var i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        }

        private void HairlineV(RectTransform parent, float ax, Color c)
        {
            var r = NewRect("VL", parent);
            r.anchorMin = new Vector2(ax, 0); r.anchorMax = new Vector2(ax, 1);
            r.pivot = new Vector2(ax, 0.5f);
            r.sizeDelta = new Vector2(1, 0);
            r.anchoredPosition = Vector2.zero;
            var i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        }

        private RectTransform _bookPopup;

        /// <summary>A little paper window of options under a chip. A full-screen invisible
        /// catcher behind it closes it on any other click.</summary>
        private void OpenBookPopup(float anchorX, float chipY, List<string> options, Action<int> onPick)
        {
            CloseBookPopup();
            var sheet = _bookPanel.Find("Sheet") as RectTransform;
            _bookPopup = NewRect("Popup", sheet);
            var catcher = _bookPopup.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0.001f);
            Stretch(_bookPopup, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var catchBtn = _bookPopup.gameObject.AddComponent<Button>();
            catchBtn.transition = Selectable.Transition.None;
            catchBtn.onClick.AddListener(CloseBookPopup);

            int cols = options.Count > 8 ? 2 : 1;
            int rows = Mathf.CeilToInt(options.Count / (float)cols);
            float w = cols * 190f + 8f, h = rows * 28f + 10f;
            var win = NewRect("Win", _bookPopup);
            Place(win, new Vector2(0.5f, 0.5f), new Vector2(w, h),
                new Vector2(anchorX, chipY - 16f - h * 0.5f));
            var winImg = win.gameObject.AddComponent<Image>();
            winImg.color = new Color(0.97f, 0.94f, 0.84f);
            var winShadow = win.gameObject.AddComponent<Shadow>();
            winShadow.effectColor = new Color(0.2f, 0.12f, 0.06f, 0.5f);
            winShadow.effectDistance = new Vector2(3, -3);

            for (int i = 0; i < options.Count; i++)
            {
                int pick = i;
                var opt = NewRect($"O{i}", win);
                Place(opt, new Vector2(0, 1), new Vector2(190, 28),
                    new Vector2(5 + (i / rows) * 190f, -5f - (i % rows) * 28f));
                opt.pivot = new Vector2(0, 1);
                var oImg = opt.gameObject.AddComponent<Image>();
                oImg.color = new Color(0, 0, 0, 0.001f);
                var ot = NewText("T", opt, _body, 16, TextAnchor.MiddleLeft, new Color(0.2f, 0.12f, 0.06f));
                Stretch(ot.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-4, 0));
                ot.text = options[i];
                var ob = opt.gameObject.AddComponent<Button>();
                ob.targetGraphic = oImg;
                ob.onClick.AddListener(() =>
                {
                    Sfx.Play("click", 0.5f);
                    CloseBookPopup();
                    onPick(pick);
                });
                var sink = opt.gameObject.AddComponent<PressSink>();
                sink.Face = opt; sink.Depth = 2f; sink.Lift = 1f; sink.Tint = ot;
            }
        }

        private void CloseBookPopup()
        {
            if (_bookPopup != null) { Destroy(_bookPopup.gameObject); _bookPopup = null; }
        }

        private bool BookAdmits(RecipeDefinition r)
        {
            if (_bookTier >= 0 && TierIndex(r.Rank) != _bookTier) return false;
            if (_bookPrep >= 0 && (int)r.Prep != _bookPrep) return false;
            if (r.Id == "draught" || r.Id == "neat_pour")
                if (_bookPrep >= 0 || _bookStyle != null) return false;   // the two specials filter out
            if (_bookStyle != null)
            {
                bool has = false;
                foreach (var b in r.RatioRequirements)
                    if (b.IsStyleBand && b.Style == _bookStyle) { has = true; break; }
                if (!has) return false;
            }
            string q = _bookSearch != null ? _bookSearch.text : null;
            if (!string.IsNullOrWhiteSpace(q) &&
                r.Name.IndexOf(q.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return true;
        }

        private static string TierName(int rank) =>
            rank <= 8 ? "STARTER" : rank <= 14 ? "MID SHELF" : rank <= 21 ? "TOP SHELF" : "HOUSE PRIDE";

        private void RebuildRecipeBook()
        {
            var run = Run;
            if (run == null || _bookList == null) return;
            for (int i = _bookList.childCount - 1; i >= 0; i--)
                Destroy(_bookList.GetChild(i).gameObject);

            // A set filter reads as its value alone: at 16px the prefixed form no longer
            // fits the chip, and "SHAKEN" says more than "PREP: SHAKEN" anyway.
            _bookTierChip.text = _bookTier < 0 ? "TIER: ALL" : BookTiers[_bookTier];
            _bookPrepChip.text = _bookPrep < 0 ? "PREP: ALL" : ((PrepMethod)_bookPrep).ToString().ToUpperInvariant();
            _bookStyleChip.text = _bookStyle == null ? "BOTTLE: ALL" : _bookStyle.Replace('_', ' ').ToUpperInvariant();

            var known = new List<RecipeDefinition>();
            foreach (var r in run.MenuRecipes) if (BookAdmits(r)) known.Add(r);
            var locked = new List<RecipeDefinition>();
            foreach (var r in run.LockedRecipes) if (BookAdmits(r)) locked.Add(r);
            known.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            locked.Sort((a, b) => a.Rank.CompareTo(b.Rank));

            var groups = new List<List<RecipeDefinition>>();
            foreach (var r in known)
            {
                if (groups.Count == 0 || TierName(groups[groups.Count - 1][0].Rank) != TierName(r.Rank))
                    groups.Add(new List<RecipeDefinition>());
                groups[groups.Count - 1].Add(r);
            }
            foreach (var g in groups) BookSection(TierName(g[0].Rank), g, lockedRows: false, run);
            if (locked.Count > 0) BookSection("STILL IN THE BOOK", locked, lockedRows: true, run);
            _bookList.anchoredPosition = Vector2.zero;   // a fresh filter reads from the top
        }

        private void BookHeader(string text)
        {
            var h = NewRect("H", _bookList);
            h.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            // The heavy face, not a faked weight (the author, 2026-08-02: the fake bold
            // read broken). PressStart2P is the game's display type and carries the
            // heading on its own.
            var t = NewText("T", h, _display, 16, TextAnchor.MiddleLeft,
                new Color(0.30f, 0.16f, 0.05f));
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 4), Vector2.zero);
            t.text = text;
            var rule = NewRect("Rule", h);
            rule.anchorMin = Vector2.zero; rule.anchorMax = new Vector2(1, 0);
            rule.pivot = new Vector2(0.5f, 0);
            rule.sizeDelta = new Vector2(0, 2);
            rule.anchoredPosition = Vector2.zero;
            var ruleImg = rule.gameObject.AddComponent<Image>();
            ruleImg.color = new Color(0.36f, 0.20f, 0.08f, 0.5f);
            ruleImg.raycastTarget = false;
        }

        /// <summary>A tier's worth of the book: its header, then its own grid — two columns
        /// while the pours fit a half page, one full-width column when they run long.</summary>
        private void BookSection(string title, List<RecipeDefinition> rs, bool lockedRows, TycoonRun run)
        {
            BookHeader(title);
            // The rows are SPEC CARDS now, so a cell is as tall as its longest spec: the
            // prep line, a line per pour, then the fill and the glass. Two columns hold
            // throughout — stacking the pours is exactly what buys the width back.
            float tallest = 0;
            foreach (var r in rs)
            {
                float h = 0;
                foreach (var row in RecipeSpecRows(r)) h += row.Hint ? SpecHintH : SpecRowH;
                if (h > tallest) tallest = h;
            }
            if (lockedRows) tallest += SpecRowH;   // the star gate takes its own line
            int cols = 2;
            var sec = NewRect("Sec", _bookList);
            var g = sec.gameObject.AddComponent<GridLayoutGroup>();
            float fullW = BkW * BkPaperW - 44f;
            g.cellSize = new Vector2(fullW / 2f - 6f, 30f + tallest);
            g.spacing = new Vector2(12, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = cols;
            foreach (var r in rs) BookRow(sec, r, lockedRows, run);
        }

        /// <summary>One recipe: the glass drawn from its own bands, the name, how it is
        /// worked, and the pour — or, for a locked one, what it is waiting behind.</summary>
        private void BookRow(RectTransform parent, RecipeDefinition r, bool lockedRow, TycoonRun run)
        {
            var row = NewRect($"R_{r.Id}", parent);
            // A printed line, not a key (2026-08-01): transparent row, thin ink rule under
            // it, the way the licence's own fields sit on their rules.
            var rowImg = row.gameObject.AddComponent<Image>();
            rowImg.color = new Color(0, 0, 0, 0.001f);
            var rowRule = NewRect("Rule", row);
            rowRule.anchorMin = new Vector2(0.01f, 0); rowRule.anchorMax = new Vector2(0.99f, 0);
            rowRule.pivot = new Vector2(0.5f, 0);
            rowRule.sizeDelta = new Vector2(0, 1);
            rowRule.anchoredPosition = new Vector2(0, 1);
            var rowRuleImg = rowRule.gameObject.AddComponent<Image>();
            rowRuleImg.color = new Color(0.30f, 0.20f, 0.10f, lockedRow ? 0.20f : 0.35f);
            rowRuleImg.raycastTarget = false;

            var icon = NewRect("I", row);
            Place(icon, new Vector2(0, 0.5f), new Vector2(40, 40), new Vector2(6, 0));
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = DrinkIcon.For(r, _bootstrap.Glassware);
            img.preserveAspect = true; img.raycastTarget = false;
            img.enabled = img.sprite != null;
            if (lockedRow) img.color = new Color(1, 1, 1, 0.4f);

            var name = NewText("N", row, _display, 16, TextAnchor.UpperLeft,
                lockedRow ? new Color(0.45f, 0.36f, 0.28f) : new Color(0.13f, 0.08f, 0.05f));
            Stretch(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(52, -24), new Vector2(-4, -4));
            name.text = r.Name.ToUpperInvariant();

            // The same spec card the licence draws, in the book's own ink (2026-08-02):
            // exact shares, the bottles' own art, and the stocked ones lit.
            var body = NewRect("Spec", row);
            float bodyW = parent.GetComponent<GridLayoutGroup>().cellSize.x - 56f;
            Place(body, new Vector2(0, 1), new Vector2(bodyW, 10), Vector2.zero);
            body.pivot = new Vector2(0, 1);
            body.anchoredPosition = new Vector2(52, -24);
            double gate = lockedRow ? run.RecipeStarGate(r) : 0;
            DrawRecipeSpec(body, r, dark: false, width: bodyW,
                note: gate > 0 ? $"OPENS AT {gate:0.0}★" : null);
        }

        /// <summary>"GIN 45–65 · LEMON 20–40 · SYRUP 10–30" — the pour, said in shares.</summary>
        private static string BandLine(RecipeDefinition r)
        {
            // The numbers carry the craft, so they print in heavier ink than the names.
            var parts = new List<string>();
            foreach (var b in r.RatioRequirements)
                parts.Add(string.Format("{0} <color=#1A0E06>{1:0}–{2:0}%</color>",
                    b.IsStyleBand ? b.Style.Replace('_', ' ').ToUpperInvariant() : TypeWord(b.Type),
                    b.MinRatio * 100, b.MaxRatio * 100));
            if (r.MinFill > 0)
                parts.Add(string.Format("<color=#1A0E06>FILL {0:0}%+</color>", r.MinFill * 100));
            return string.Join(" · ", parts);
        }

        // ── settings (P17): the smallest sheet that holds sound and motion ───────

        private RectTransform _settingsPanel;
        private Text _settingsVolume, _settingsMute, _settingsMotion;

        private void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            _settingsPanel.gameObject.SetActive(!_settingsPanel.gameObject.activeSelf);
            RefreshSettings();
        }

        private void BuildSettings(RectTransform root)
        {
            _settingsPanel = NewRect("Settings", root);
            Place(_settingsPanel, new Vector2(1, 1), new Vector2(300, 300), new Vector2(-16, -58));
            _settingsPanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];

            var title = NewText("T", _settingsPanel, _body, 10, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -18), Vector2.zero);
            title.text = "— SETTINGS —";

            _settingsVolume = SettingsRow(0, "VOLUME", () =>
            {
                Sound.Volume = Sound.Volume <= 0.05f ? 0.2f : Sound.Volume >= 0.95f ? 0.2f
                    : Sound.Volume + 0.2f;      // cycles 0.2 → 1.0 and wraps
                Sfx.Play("click");
                RefreshSettings();
            });
            _settingsMute = SettingsRow(1, "SOUND", () =>
            {
                Sound.Muted = !Sound.Muted;
                Sfx.Play("click");              // audible iff it just came back on — itself the test
                RefreshSettings();
            });
            // The three game modes (the author's dev tool): a fresh bar, a mid-run bar,
            // and the endgame sandbox — playtest any act without earning your way to it.
            SettingsRow(3, "DEV · FRESH START — day 1, empty bar", () =>
            { _bootstrap.StartNewRun(null); ToggleSettings(); });
            SettingsRow(4, "DEV · MIDGAME — day 12, stocked", () =>
            { _bootstrap.StartNewRun(null); Run.DevPreset(1); ApplyBarLook(); ToggleSettings(); });
            SettingsRow(5, "DEV · ENDGAME — late run, full shelf", () =>
            { _bootstrap.StartNewRun(null); Run.DevPreset(2); ApplyBarLook(); ToggleSettings(); });
            // Straight to the books and the shop (the author, 2026-08-07). It runs the real
            // clock rather than forcing the phase, so the night closes honestly — anyone
            // still sitting drinks up or walks, and the rent lands as it always would.
            SettingsRow(7, "DEV · THE ROOM — every drinker, their papers and their star", () =>
            {
                ToggleSettings();
                ToggleGuide();
            });
            SettingsRow(6, "DEV · SKIP TO DAY END — close now, open the market", () =>
            {
                if (Run == null || Run.Phase != TycoonPhase.DayOpen) { Toast("NOT MID-DAY"); return; }
                _flow?.CloseFlow();
                CloseId();
                Run.DevSkipToDayEnd();
                ToggleSettings();
            });

            _settingsMotion = SettingsRow(2, "MOTION", () =>
            {
                Motion.Reduced = !Motion.Reduced;
                Sfx.Play("click");
                RefreshSettings();
            });

            _settingsPanel.gameObject.SetActive(false);
        }

        // ── the character guide (dev tool, 2026-08-10) ──────────────────────────
        // Every drinker on one scrollable sheet: the licence photo, the name, the age, the
        // citizenship and its flag, the archetype, and the standing the bar has to reach
        // before they walk in. The author's reason is practical — deciding what to change
        // about a character means seeing all of them at once — and it is written as a panel
        // rather than as a printed page because it can become an in-game almanac later:
        // the same rows, unlocked one at a time as each person is met.

        private RectTransform _guidePanel;
        private RectTransform _guideRows;

        private void ToggleGuide()
        {
            if (_guidePanel == null) return;
            bool show = !_guidePanel.gameObject.activeSelf;
            if (show) { CloseId(); RefreshGuide(); }
            _guidePanel.gameObject.SetActive(show);
        }

        private void BuildGuide(RectTransform root)
        {
            _guidePanel = NewRect("Guide", root);
            Place(_guidePanel, new Vector2(0.5f, 0.5f), new Vector2(940, 620), new Vector2(0, 10));
            var canvas = _guidePanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 24;                 // above the market, which is 22
            _guidePanel.gameObject.AddComponent<GraphicRaycaster>();
            var bg = _guidePanel.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.985f);
            bg.raycastTarget = true;
            Frame(_guidePanel, 2f, UITheme.PrimaryAction);

            var title = NewText("T", _guidePanel, _display, 16, TextAnchor.MiddleLeft,
                UITheme.PrimaryAction);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(600, 22), new Vector2(20, -18));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = "THE ROOM — WHO DRINKS HERE";

            var note = NewText("N", _guidePanel, _body, 8, TextAnchor.MiddleRight, UITheme.Cream[2]);
            Place(note.rectTransform, new Vector2(1, 1), new Vector2(420, 12), new Vector2(-20, -22));
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.verticalOverflow = VerticalWrapMode.Truncate;
            note.text = "Star gate = what the bar must be worth before they come in";

            // Column heads, so the rows underneath do not need repeating labels.
            var head = NewText("H", _guidePanel, _shop, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(head.rectTransform, new Vector2(0, 1), new Vector2(880, 12), new Vector2(96, -46));
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.text = "NAME                          AGE   CITIZEN OF              ARCHETYPE            FROM";

            var view = NewRect("GuideView", _guidePanel);
            Stretch(view, Vector2.zero, Vector2.one, new Vector2(14, 52), new Vector2(-14, -58));
            view.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.02f);
            view.gameObject.AddComponent<RectMask2D>();
            _guideRows = NewRect("Rows", view);
            _guideRows.anchorMin = new Vector2(0, 1); _guideRows.anchorMax = Vector2.one;
            _guideRows.pivot = new Vector2(0.5f, 1);
            _guideRows.offsetMin = Vector2.zero; _guideRows.offsetMax = Vector2.zero;
            var layout = _guideRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childControlHeight = false; layout.childForceExpandHeight = false;
            var fit = _guideRows.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = _guideRows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.inertia = false;

            NewButton(_guidePanel, "CLOSE", new Vector2(0.5f, 0), new Vector2(180, 32),
                new Vector2(0, 12), UITheme.PrimaryAction, () => ToggleGuide());
            _guidePanel.gameObject.SetActive(false);
        }

        private void RefreshGuide()
        {
            if (_guideRows == null) return;
            for (int i = _guideRows.childCount - 1; i >= 0; i--)
                Destroy(_guideRows.GetChild(i).gameObject);

            float standing = Run != null ? (float)Run.Rating.Average : 0f;
            foreach (var look in _looks)
            {
                var papers = PapersFor(look);
                var row = NewRect("R", _guideRows);
                row.sizeDelta = new Vector2(0, 62);
                var rowBg = row.gameObject.AddComponent<Image>();
                bool here = look.Stars <= standing + 0.001f;
                rowBg.color = here ? new Color(1f, 1f, 1f, 0.045f) : new Color(1f, 1f, 1f, 0.015f);

                if (look.Face != null)
                {
                    var photo = NewRect("P", row);
                    Place(photo, new Vector2(0, 0.5f), new Vector2(54, 54), new Vector2(8, 0));
                    var pi = photo.gameObject.AddComponent<Image>();
                    pi.sprite = look.Face; pi.preserveAspect = true; pi.raycastTarget = false;
                    // Somebody who will not come in yet is shown, but dimmed — the guide is
                    // a roster, not a spoiler, and the point is seeing WHO is still to come.
                    pi.color = here ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
                }

                var line = NewText("L", row, _body, 8, TextAnchor.MiddleLeft,
                    here ? UITheme.Cream[4] : UITheme.Cream[2]);
                Place(line.rectTransform, new Vector2(0, 1), new Vector2(700, 14), new Vector2(72, -10));
                line.horizontalOverflow = HorizontalWrapMode.Overflow;
                line.text = papers != null
                    ? papers.Name.PadRight(30) + (papers.Age + "").PadRight(6) + papers.Country
                    : (look.Slug ?? "patron");

                var sub = NewText("S", row, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
                Place(sub.rectTransform, new Vector2(0, 1), new Vector2(700, 12), new Vector2(72, -28));
                sub.horizontalOverflow = HorizontalWrapMode.Overflow;
                sub.text = (look.Slug ?? "patron") + "   ·   6 clips   ·   head row " + (int)look.HeadY;

                var gate = NewText("G", row, _shop, 8, TextAnchor.MiddleRight,
                    here ? new Color(0.42f, 0.84f, 0.51f, 1f) : UITheme.PrimaryAction);
                Place(gate.rectTransform, new Vector2(1, 1), new Vector2(220, 14), new Vector2(-10, -10));
                gate.horizontalOverflow = HorizontalWrapMode.Overflow;
                gate.text = look.Stars <= 0f ? "OPENS THE DOORS"
                    : (here ? "IN THE ROOM · " : "WAITS FOR ") + look.Stars.ToString("0.0") + "*";

                if (papers != null)
                {
                    var flag = NewRect("F", row);
                    Place(flag, new Vector2(1, 1), new Vector2(16, 11), new Vector2(-10, -30));
                    var fi = flag.gameObject.AddComponent<Image>();
                    fi.sprite = ItemArt.Load("fl_" + papers.Iso);
                    fi.raycastTarget = false;
                    // A citizenship with no flag drawn shows nothing rather than a white box.
                    fi.enabled = fi.sprite != null;
                }
            }
        }

        private Text SettingsRow(int index, string label, Action onClick)
        {
            var row = NewRect($"Row{index}", _settingsPanel);
            Place(row, new Vector2(0.5f, 1), new Vector2(280, 28), new Vector2(0, -24f - index * 32f));
            var img = row.gameObject.AddComponent<Image>();
            img.color = UITheme.Night[3];
            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var text = NewText("L", row, _body, 12, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return text;
        }

        private void RefreshSettings()
        {
            if (_settingsVolume == null) return;
            _settingsVolume.text = $"VOLUME  {Mathf.RoundToInt(Sound.Volume * 100)}%";
            _settingsMute.text = Sound.Muted ? "SOUND  OFF" : "SOUND  ON";
            _settingsMotion.text = Motion.Reduced ? "MOTION  REDUCED" : "MOTION  FULL";
        }

        private static string CrowdName(WealthTier tier) =>
            tier == WealthTier.HighRoller ? "HIGH ROLLERS" : tier == WealthTier.Broke ? "BROKE" : "REGULARS";

        /// <summary>Leader dots so the bill columns line up in the monospace pixel font.</summary>
        private static string Dots(int n) => "<color=#9C8F80>" + new string('.', n) + "</color>";

        /// <summary>The slip is <see cref="Columns"/> characters wide. The pixel font is
        /// monospace, so a receipt line can be set the way a till sets one: label at the left,
        /// amount flush right, leader dots filling whatever is between them.</summary>
        private const int Columns = 26;

        private static readonly string Rule = new string('=', Columns);

        /// <summary>One receipt line, right-aligned to the slip's width.</summary>
        private static string Line(string label, string amount, string hex)
        {
            int gap = Math.Max(1, Columns - label.Length - amount.Length);
            string body = label + Dots(gap) + amount;
            return hex == null ? body : $"<color=#{hex}>{body}</color>";
        }

        private void ShowClosed()
        {
            _dayEndPanel.gameObject.SetActive(false);
            CloseId();
            _bannerText.gameObject.SetActive(true);
            _bannerText.text = "✖ THE BAR IS CLOSED\nthree days in the red — NEW RUN to try again";
        }

        // ── the licence: read the customer (GDD 24 §5) ───────────────────────────

        private void ShowId(CustomerVisit visit)
        {
            if (visit?.Regular == null) return;
            _idVisit = visit;
            var reg = visit.Regular;

            // Opening the card IS the inspection (v5 C3): this is the one gate Core opens the
            // order through, so everything below may read it — and the bubble may from now on.
            visit.InspectId();

            if (_ledgerPanel != null) _ledgerPanel.gameObject.SetActive(false);
            _idRoot.gameObject.SetActive(true);
            // THE PHOTO IS THIS DRINKER (the author, 2026-08-09). It used to be the
            // ARCHETYPE's portrait — one picture for everyone off the late shift — while
            // eleven different people sit on the stool. Reading a customer is the game;
            // a licence that does not match the face in front of you is a licence that
            // teaches the player to stop looking. The archetype portrait stays as the
            // fallback for a look with no photo on disk.
            var idLook = LookFor(visit);
            _idPhoto.sprite = idLook != null && idLook.Face != null
                ? idLook.Face
                : (stage != null ? stage.PortraitSpriteFor(reg.ArchetypeId) : null);
            _idPhoto.color = _idPhoto.sprite != null ? Color.white : UITheme.Night[3];

            _idName.text = reg.Name.ToUpperInvariant();
            _idAgeFrom.text = $"{reg.Age}  ·  {reg.Hometown.ToUpperInvariant()}";
            // The count rides on the LABEL, not the value (the author, 2026-08-03: the
            // stars were printing over it). The display face is a fixed-width 16px cell, so
            // "FAMILIAR · 12 VISITS" runs 300 points through a 188-point column and straight
            // into the rating beside it; the relationship alone fits, and the small navy
            // label has room to spare.
            _idRel.text = reg.Visits > 0
                ? reg.Relationship.ToString().ToUpperInvariant()
                : "NEW FACE";
            _idRelLabel.text = reg.Visits > 0
                ? $"STANDING  ·  {reg.Visits} VISIT{(reg.Visits == 1 ? "" : "S")}"
                : "STANDING";

            // What THEY make of US — their own nights here, said in stars. A stranger has no
            // row at all (the author's note: empty fields were noise, not a licence).
            bool stranger = reg.Visits == 0;
            _idRatesLabel.text = stranger ? "" : "RATES THIS BAR";
            _idRates.text = stranger ? ""
                : Stars(Mathf.RoundToInt(5f * reg.SatisfiedCount / reg.Visits));

            // No price, anywhere on the card (C3): the licence says who they are and what they
            // want, and what a drink costs is the menu's business.
            _idOrder.text = $"<b>{visit.Order.Wanted.Name.ToUpperInvariant()}</b>";
            var parts = new List<string>();
            foreach (var band in visit.Order.Wanted.RatioRequirements)
                parts.Add((band.IsStyleBand ? band.Style.Replace('_', ' ') : band.Type.ToString())
                    .ToUpperInvariant());
            _idOrderParts.text = string.Join("  ·  ", parts);
            _idOrderIcon.sprite = DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
            _idOrderIcon.enabled = _idOrderIcon.sprite != null;

            // The endorsements, drawn rather than listed (the author): each ask is a
            // pictogram with its word under it, and the read's fill preference joins them
            // as a glass marked with the band it wants — the empty space counted in the
            // numbers, which is the honest way to say how full a glass should be.
            var spec = visit.Order.Spec;
            foreach (Transform old in _idPrefRow) Destroy(old.gameObject);
            int chips = 0;
            foreach (var g in visit.Order.Garnishes)
                chips += PrefChip(PrefArt.ForPreparation(g.Id), g.Name.ToUpperInvariant());
            if (spec.ExtraShaken) chips += PrefChip(PrefArt.Shaker(), "SHAKEN HARD");
            // No fill chip (the author, 2026-08-02): nobody demands a fill any more — the
            // only fill rule is the house floor, and it lives in the judge, not the licence.
            _idIntent.text = chips == 0 ? "SERVE IT CLEAN" : "";
        }

        /// <summary>One pictogram with its word under it. Returns 1 so the caller can count.</summary>
        private int PrefChip(Sprite icon, string label)
        {
            var chip = NewRect("Pref", _idPrefRow);
            var iconRt = NewRect("I", chip);
            Place(iconRt, new Vector2(0.5f, 1), new Vector2(26, 26), Vector2.zero);
            iconRt.pivot = new Vector2(0.5f, 1);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
            img.enabled = icon != null;
            var t = NewText("L", chip, _body, 8, TextAnchor.UpperCenter, UITheme.Night[1]);
            Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(96, 10), new Vector2(0, -28));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = label;
            var le = chip.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = Mathf.Max(44f, label.Length * 7f);
            le.preferredHeight = 40f;
            return 1;
        }

        // The week (the author's calendar): six open days, Tuesday through Sunday —
        // Mondays the bar is dark and the calendar simply skips them.
        private static readonly string[] OpenDayNames =
            { "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" };

        private static string CalendarFor(int day) =>
            $"WEEK {(day - 1) / 6 + 1} · {OpenDayNames[(day - 1) % 6]}";

        /// <summary>0–5 stars as glyphs, the empty ones kept so the width never jumps.</summary>
        private static string Stars(int n) =>
            new string('★', Mathf.Clamp(n, 0, 5)) + new string('☆', 5 - Mathf.Clamp(n, 0, 5));

        private void CloseId()
        {
            _idVisit = null;
            if (_idRoot != null) _idRoot.gameObject.SetActive(false);
        }

        // ── the licence, v3 (P15 / C3) ──────────────────────────────────────────
        // A landscape US-licence, not a dossier: the v2 portrait card was explicitly disliked.
        // The shell is generated art (the first UI piece since the author lifted the no-AI-UI
        // rule, 2026-07-31) drawn at exactly 2× its 400×250 pixels — an integer scale, because
        // the prep table taught what a fractional upscale next to native-size text looks like.
        // Every field is lettered in engine over the blank shell: the generator cannot spell.
        // Sized down to 2.5× on the author's note ("kimlik boyutunu biraz küçült"), and the
        // lettering SITS ON THE SHELL'S OWN RULE LINES now — the art carries six faint field
        // rules at y 58/76/94/113/132/150 (measured), and every value's baseline lands on one,
        // so the text belongs to the printed card instead of floating over it.
        // The v4 shell (2026-08-02): a denser generated card — five rules, tight bottom —
        // measured off licence_shell2.png (510×315: navy band rows 23–51, portrait x 26–168
        // y 81–286, rules y 96/127/159/190/219) and drawn at 1.4×. The old shell ran long
        // and its lettering was hard to read; the values sit on the display face now.
        private const float LicScale = 1.4f;
        private const float LicW = 510f * LicScale, LicH = 315f * LicScale;
        private static readonly Rect LicPortrait = new Rect(26f * LicScale, -81f * LicScale,
            143f * LicScale, 206f * LicScale);
        private const float LicHeaderH = 29f * LicScale;
        private const float LicHeaderY = -23f * LicScale;
        private const float LicFieldsX = 200f * LicScale;
        private const float LicFieldsW = 280f * LicScale;
        private static readonly float[] LicLines =   // the art's five rules, ×1.4
            { 96f * LicScale, 127f * LicScale, 159f * LicScale, 190f * LicScale, 219f * LicScale };

        /// <summary>
        /// One licence line, SEATED on a rule: the value's bottom edge lands on the shell's own
        /// printed line (the way a form is filled in), with the small navy label just above it.
        /// Returns the value; the label comes back through <paramref name="labelText"/> so a
        /// row that is sometimes empty (a stranger has no rating yet) can hide whole.
        /// </summary>
        private Text LicenceField(RectTransform card, string label, float x, float lineY,
            float w, out Text labelText, int valueSize = 16)
        {
            float vh = valueSize + 6f;
            labelText = NewText("L_" + label, card, _body, 8, TextAnchor.LowerLeft, UITheme.ClubBlue[2]);
            Place(labelText.rectTransform, new Vector2(0, 1), new Vector2(w, 12), Vector2.zero);
            labelText.rectTransform.pivot = new Vector2(0, 0);
            labelText.rectTransform.anchoredPosition = new Vector2(x, -lineY + vh + 2f);
            labelText.text = label;
            var val = NewText("V_" + label, card, _display, valueSize, TextAnchor.LowerLeft, UITheme.Night[1]);
            val.supportRichText = true;
            val.horizontalOverflow = HorizontalWrapMode.Overflow;   // a licence never wraps; it runs
            Place(val.rectTransform, new Vector2(0, 1), new Vector2(w, vh), Vector2.zero);
            val.rectTransform.pivot = new Vector2(0, 0);
            val.rectTransform.anchoredPosition = new Vector2(x, -lineY + 2f);
            return val;
        }

        private void BuildIdCard(RectTransform root)
        {
            // ITS OWN LAYER, ABOVE THE BAR. The till was lifted to a canvas at 6 so it
            // would stand in front of the drinkers — and then it stood in front of the
            // licence and the market too, because both of those are ordinary children of
            // the HUD canvas at 5. Anything that is a WINDOW over the room needs to say so
            // rather than rely on being built late: stage -10, HUD 5, till 6, service flow
            // 12, recipe book 15, licence 20, the market 22.
            _idRoot = NewRect("IdCard", root);
            var idCanvas = _idRoot.gameObject.AddComponent<Canvas>();
            idCanvas.overrideSorting = true;
            idCanvas.sortingOrder = 20;
            _idRoot.gameObject.AddComponent<GraphicRaycaster>();
            Stretch(_idRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _idRoot.gameObject.AddComponent<Image>();
            scrim.color = UITheme.Scrim;
            var scrimBtn = _idRoot.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(CloseId);

            var card = NewRect("Card", _idRoot);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(LicW, LicH), new Vector2(0, 10));
            var shell = card.gameObject.AddComponent<Image>();
            shell.sprite = ItemArt.Load("licence_shell2");
            if (shell.sprite == null) shell.sprite = ItemArt.Load("licence_shell");
            if (shell.sprite == null) shell.color = UITheme.Cream[4];   // no art: a plain card
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallow clicks

            var htext = NewText("H", card, _body, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Place(htext.rectTransform, new Vector2(0.5f, 1), new Vector2(LicW - 40, LicHeaderH),
                new Vector2(0, LicHeaderY));
            htext.text = "NEW ARDEN  ·  PATRON LICENCE";

            var photo = NewRect("Photo", card);
            Place(photo, new Vector2(0, 1), new Vector2(LicPortrait.width, LicPortrait.height),
                new Vector2(LicPortrait.x, LicPortrait.y));
            _idPhoto = photo.gameObject.AddComponent<Image>();
            _idPhoto.preserveAspect = true;

            // The data column, one field to a printed rule (the author's note: the text and
            // the art disagreed — now the art's own lines decide where the text sits). The
            // reserved-slots row is GONE: a row of blanks was noise, not a licence.
            float colW = LicFieldsW * 0.5f - 8f;
            _idName = LicenceField(card, "NAME", LicFieldsX, LicLines[0], LicFieldsW, out _, 24);
            _idAgeFrom = LicenceField(card, "AGE  ·  CITY", LicFieldsX, LicLines[1], LicFieldsW, out _);
            _idRel = LicenceField(card, "STANDING", LicFieldsX, LicLines[2], colW, out _idRelLabel);
            _idRates = LicenceField(card, "RATES THIS BAR", LicFieldsX + colW + 16f, LicLines[2],
                colW, out _idRatesLabel);

            // The order, seated on its own rule with the glass drawn beside it.
            var idIcon = NewRect("OrderIcon", card);
            Place(idIcon, new Vector2(0, 1), new Vector2(30, 30), Vector2.zero);
            idIcon.pivot = new Vector2(0, 0);
            idIcon.anchoredPosition = new Vector2(LicFieldsX, -LicLines[3] + 2f);
            _idOrderIcon = idIcon.gameObject.AddComponent<Image>();
            _idOrderIcon.preserveAspect = true;
            _idOrderIcon.raycastTarget = false;
            _idOrder = LicenceField(card, "ORDER", LicFieldsX + 40f, LicLines[3],
                LicFieldsW - 40f, out _, 16);
            // What is IN it, under the name (v5 P16): the menu speaks styles now, so the
            // licence has to say gin-and-tonic, not just "Gin & Tonic" — this line is the
            // player's recipe knowledge since the band rows left with v2.
            // It SHARES the serving-preferences caption row (the author, 2026-08-03). Under
            // the order's own rule there are eight points before that caption starts and this
            // line needs twelve, so it printed straight through it; beside the drink's name
            // there is no room either — measured in play, the longest name and the longest
            // list of parts come to 429 points in a 352-point field. The caption row is 392
            // wide and its own text is barely a quarter of that, so the two sit on it
            // together, the parts to the left and the caption pushed to the right.
            _idOrderParts = NewText("OrderParts", card, _body, 8, TextAnchor.LowerLeft, UITheme.Night[3]);
            Place(_idOrderParts.rectTransform, new Vector2(0, 1), new Vector2(LicFieldsW, 12),
                Vector2.zero);
            _idOrderParts.rectTransform.pivot = new Vector2(0, 0);
            _idOrderParts.rectTransform.anchoredPosition = new Vector2(LicFieldsX, -LicLines[4] + 20f);

            // Hovering the order shows the RECIPE (2026-07-31): the drink they asked for,
            // said the way the book says it — prep, pour shares, glass — without leaving
            // the card. The hit rect covers the order line, icon included.
            var orderHit = NewRect("OrderHit", card);
            Place(orderHit, new Vector2(0, 1), new Vector2(LicFieldsW, 52), Vector2.zero);
            orderHit.pivot = new Vector2(0, 0);
            orderHit.anchoredPosition = new Vector2(LicFieldsX, -LicLines[3] - 6f);
            var orderHitImg = orderHit.gameObject.AddComponent<Image>();
            orderHitImg.color = new Color(0, 0, 0, 0.001f);
            // VERTICAL and vice (the author, 2026-08-02): the cream chip vanished into the
            // cream card. A dark glass panel, cyan-edged, one pour to a line, the numbers
            // bright — parked over the seal corner where nothing else lives.
            // BESIDE the card, in the scrim's own margin (the author, 2026-08-02). Parked
            // over the fields it flickered: the panel took the pointer, which fired the
            // order line's PointerExit, which hid the panel, which handed the pointer back
            // — many times a second. A tip that covers the line you hovered cannot help you
            // read it anyway. The card is 714 wide on a 1280 canvas, so 252 clears it.
            _idRecipeTip = NewRect("RecipeTip", _idRoot);
            Place(_idRecipeTip, new Vector2(0.5f, 0.5f), new Vector2(TipW, 120), Vector2.zero);
            _idRecipeTip.pivot = new Vector2(0, 1);
            _idRecipeTip.anchoredPosition = new Vector2(LicW * 0.5f + 12f, LicH * 0.5f - LicLines[2] + 16f);
            var tipBg = _idRecipeTip.gameObject.AddComponent<Image>();
            tipBg.color = new Color(0.07f, 0.07f, 0.11f, 0.96f);
            // Nothing in the panel may take a raycast, or hovering it reads as leaving the
            // order line and the whole thing blinks.
            tipBg.raycastTarget = false;
            var tipEdge = new Color(UITheme.Cyan[3].r, UITheme.Cyan[3].g, UITheme.Cyan[3].b, 0.8f);
            Hairline(_idRecipeTip, new Vector2(0, 0), new Vector2(1, 0), tipEdge);
            Hairline(_idRecipeTip, new Vector2(0, 1), new Vector2(1, 1), tipEdge);
            HairlineV(_idRecipeTip, 0f, tipEdge);
            HairlineV(_idRecipeTip, 1f, tipEdge);
            _idRecipeTipBody = NewRect("Body", _idRecipeTip);
            Stretch(_idRecipeTipBody, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
            _idRecipeTip.gameObject.SetActive(false);
            var trig = orderHit.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowOrderRecipeTip());
            trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => _idRecipeTip.gameObject.SetActive(false));
            trig.triggers.Add(exit);

            // Serving preferences — the endorsements, drawn as pictograms (the author,
            // 2026-08-01) in the free band under the rule; the field text only survives to
            // say SERVE IT CLEAN when there is nothing to draw.
            _idIntent = LicenceField(card, "SERVING PREFERENCES", LicFieldsX, LicLines[4],
                LicFieldsW, out _idIntentLabel, 12);
            _idIntentLabel.alignment = TextAnchor.LowerRight;   // the parts share this row
            _idPrefRow = NewRect("PrefRow", card);
            Place(_idPrefRow, new Vector2(0, 1), new Vector2(LicFieldsW, 42), Vector2.zero);
            _idPrefRow.pivot = new Vector2(0, 1);
            _idPrefRow.anchoredPosition = new Vector2(LicFieldsX, -LicLines[4] - 2f);
            var prefLayout = _idPrefRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            prefLayout.spacing = 8;
            prefLayout.childControlWidth = true; prefLayout.childForceExpandWidth = false;
            prefLayout.childControlHeight = true; prefLayout.childForceExpandHeight = false;
            prefLayout.childAlignment = TextAnchor.UpperLeft;

            var hint = NewText("Hint", _idRoot, _body, 12, TextAnchor.MiddleCenter, UITheme.TextSecondary);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400, 20),
                new Vector2(0, -(LicH * 0.5f) - 16f));
            hint.text = "TAP OUTSIDE THE CARD TO HAND IT BACK";

            _idRoot.gameObject.SetActive(false);
        }

        // ── construction ────────────────────────────────────────────────────────

        private void BuildUi()
        {
            LoadPatronFrames();   // the seated customer's animation clips (Resources/Patron/*)

            var canvasGo = new GameObject("TycoonHud", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            // Match height (like the stage, 2026-07-22): every canvas scales off the same axis
            // so nothing drifts out of alignment when the window's aspect changes.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasGo.transform;
            _hudRoot = root;

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            // The board over the back counter (the author, 2026-08-02: a top bar that
            // belongs to this bar). A dark fascia, lit along its top edge where the room
            // catches it and burning along its bottom in neon; the three readings sit on
            // it as PLAQUES, each seated on its own coloured rule — the licence card's
            // language, so the game's two instrument surfaces read as one hand.
            var top = Panel(root, "TopBar", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -TopBarH), Vector2.zero, UITheme.Night[1]);
            Hairline(top, new Vector2(0, 1), new Vector2(1, 1), UITheme.Night[3]);

            // The tube: a bright core over a wider glow, bleeding below the panel. One flat
            // line was the only themed thing up here and it read as a divider; neon reads
            // as a room.
            var tube = NewRect("Neon", top);
            tube.anchorMin = new Vector2(0, 0); tube.anchorMax = new Vector2(1, 0);
            tube.pivot = new Vector2(0.5f, 0);
            tube.sizeDelta = new Vector2(0, 2);
            tube.anchoredPosition = Vector2.zero;
            var tubeImg = tube.gameObject.AddComponent<Image>();
            tubeImg.color = UITheme.Amber[4]; tubeImg.raycastTarget = false;
            var bloom = NewRect("NeonBloom", top);
            bloom.anchorMin = new Vector2(0, 0); bloom.anchorMax = new Vector2(1, 0);
            bloom.pivot = new Vector2(0.5f, 1);
            bloom.sizeDelta = new Vector2(0, 5);
            bloom.anchoredPosition = Vector2.zero;
            var bloomImg = bloom.gameObject.AddComponent<Image>();
            bloomImg.color = new Color(UITheme.Amber[2].r, UITheme.Amber[2].g, UITheme.Amber[2].b, 0.30f);
            bloomImg.raycastTarget = false;

            // ── the hour, left: what the night is measured in ──────────────────
            var clock = TopPlaque(top, "Clock", new Vector2(0, 0.5f), new Vector2(214, PlaqueH),
                new Vector2(14, PlaqueY), UITheme.Cyan[2], out _clockRule);
            _dayText = NewText("Day", clock, _body, 8, TextAnchor.UpperLeft, UITheme.Cyan[3]);
            Place(_dayText.rectTransform, new Vector2(0, 1), new Vector2(196, 12), new Vector2(9, -5));
            _dayText.rectTransform.pivot = new Vector2(0, 1);
            _dayText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _clockText = NewText("Clock", clock, _display, 20, TextAnchor.LowerLeft, UITheme.TextPrimary);
            Place(_clockText.rectTransform, new Vector2(0, 0), new Vector2(196, 24), new Vector2(9, 4));
            _clockText.rectTransform.pivot = new Vector2(0, 0);

            // ── the till, centre: the number the whole loop is about ───────────
            var till = TopPlaque(top, "Till", new Vector2(0.5f, 0.5f), new Vector2(214, PlaqueH),
                new Vector2(0, PlaqueY), UITheme.Amber[2], out _tillRule);
            var tillLabel = NewText("L", till, _body, 8, TextAnchor.UpperLeft, UITheme.Amber[2]);
            Place(tillLabel.rectTransform, new Vector2(0, 1), new Vector2(196, 12), new Vector2(9, -5));
            tillLabel.rectTransform.pivot = new Vector2(0, 1);
            tillLabel.text = "TILL";
            _moneyText = NewText("Money", till, _display, 20, TextAnchor.LowerRight, UITheme.Money);
            Place(_moneyText.rectTransform, new Vector2(1, 0), new Vector2(196, 24), new Vector2(-9, 4));
            _moneyText.rectTransform.pivot = new Vector2(1, 0);

            // ── the standing, right: the stars and who they brought in ─────────
            var standing = TopPlaque(top, "Standing", new Vector2(1, 0.5f), new Vector2(236, PlaqueH),
                new Vector2(-158, PlaqueY), UITheme.Amber[3], out _standingRule);
            _crowdText = NewText("Crowd", standing, _body, 8, TextAnchor.UpperLeft, UITheme.Cream[2]);
            Place(_crowdText.rectTransform, new Vector2(0, 1), new Vector2(218, 12), new Vector2(9, -5));
            _crowdText.rectTransform.pivot = new Vector2(0, 1);
            _crowdText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // A 1.3 is 1.3 stars of amber: the mask's width IS the rating. Whole stars would
            // hide exactly the movement the player is trying to cause.
            float starsW = _ratingStars.Length * StarGap;
            var starsRow = NewRect("Stars", standing);
            Place(starsRow, new Vector2(0, 0), new Vector2(starsW, StarSize), new Vector2(8, 5));
            starsRow.pivot = new Vector2(0, 0);
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                var star = NewRect($"B{i}", starsRow);
                star.anchorMin = star.anchorMax = new Vector2(0, 0.5f);
                star.pivot = new Vector2(0.5f, 0.5f);
                star.sizeDelta = new Vector2(StarSize, StarSize);
                star.anchoredPosition = new Vector2(i * StarGap + StarGap * 0.5f, 0);
                var img = star.gameObject.AddComponent<Image>();
                img.sprite = ItemArt.Load("star");
                img.preserveAspect = true; img.raycastTarget = false;
                img.color = UITheme.Night[3];
            }
            _starsFill = NewRect("Fill", starsRow);
            _starsFill.anchorMin = new Vector2(0, 0); _starsFill.anchorMax = new Vector2(0, 1);
            _starsFill.pivot = new Vector2(0, 0.5f);
            _starsFill.sizeDelta = Vector2.zero;
            _starsFill.anchoredPosition = Vector2.zero;
            _starsFill.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                var star = NewRect($"F{i}", _starsFill);
                star.anchorMin = star.anchorMax = new Vector2(0, 0.5f);   // fixed x: the mask slides over it
                star.pivot = new Vector2(0.5f, 0.5f);
                star.sizeDelta = new Vector2(StarSize, StarSize);
                star.anchoredPosition = new Vector2(i * StarGap + StarGap * 0.5f, 0);
                var img = star.gameObject.AddComponent<Image>();
                img.sprite = ItemArt.Load("star");
                img.preserveAspect = true; img.raycastTarget = false;
                img.color = UITheme.Amber[3];
                _ratingStars[i] = img;
            }
            _ratingText = NewText("Rating", standing, _display, 18, TextAnchor.LowerRight, UITheme.Amber[3]);
            Place(_ratingText.rectTransform, new Vector2(1, 0), new Vector2(60, 22), new Vector2(-9, 3));
            _ratingText.rectTransform.pivot = new Vector2(1, 0);

            // ── the quiet end: nothing here is part of the night ───────────────
            NewButton(top, "NEW RUN", new Vector2(1, 0.5f), new Vector2(86, 26),
                new Vector2(-46, PlaqueY), UITheme.Night[3], () => _bootstrap.StartNewRun(null));
            NewButton(top, "⚙", new Vector2(1, 0.5f), new Vector2(26, 26),
                new Vector2(-14, PlaqueY), UITheme.Night[2], ToggleSettings);
            BuildSettings(root);

            // BIN GLASS retired (v5 P13 / C7): a drink is thrown away by carrying it to the bin
            // on the counter, which is the same verb that serves it.

            // Refusal notices ("NOT ENOUGH MONEY") drop in just under the top bar.
            _toast = NewText("Toast", root, _display, 14, TextAnchor.MiddleCenter, UITheme.ViceRed[3]);
            Place(_toast.rectTransform, new Vector2(0.5f, 1), new Vector2(500, 30), new Vector2(0, -66));
            _toast.gameObject.SetActive(false);

            // Six stools along the counter: each customer is a bust sitting at the bar with a
            // floating order tag above their head; click anywhere on them to read or serve.
            const float seatGap = 180f;
            const float seatStartX = 118f;
            for (int i = 0; i < SeatSlots; i++)
            {
                int index = i;
                var seat = new SeatView();
                seat.SeatX = seatStartX + i * seatGap;

                // The click zone spans the bust and its tag; a clear image catches the ray.
                seat.Root = NewRect($"Seat{i}", root);
                seat.Root.anchorMin = seat.Root.anchorMax = new Vector2(0, 0);
                seat.Root.pivot = new Vector2(0.5f, 0);
                seat.Root.sizeDelta = new Vector2(150f, CharWinH + 110f);
                seat.Root.anchoredPosition = new Vector2(seat.SeatX, CounterLineY);
                var hit = seat.Root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);   // invisible, but catches clicks
                var button = seat.Root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OnSeatClicked(index));
                seat.Group = seat.Root.gameObject.AddComponent<CanvasGroup>();

                // The customer stands behind the bar. A masked window shows them from about the
                // waist up (the bar clips the legs); the animated character sprite lives inside it.
                var win = NewRect("CharWin", seat.Root);
                win.anchorMin = win.anchorMax = new Vector2(0.5f, 0);
                win.pivot = new Vector2(0.5f, 0);
                win.sizeDelta = new Vector2(CharSize, CharWinH);
                win.anchoredPosition = new Vector2(0, 0);
                win.gameObject.AddComponent<RectMask2D>();

                var charRt = NewRect("Char", win);
                charRt.anchorMin = charRt.anchorMax = new Vector2(0.5f, 0);
                charRt.pivot = new Vector2(0.5f, 0);
                charRt.sizeDelta = new Vector2(CharSize, CharSize);
                charRt.anchoredPosition = new Vector2(0, -CharFootDrop);   // drop the legs below the window
                seat.Portrait = charRt.gameObject.AddComponent<Image>();
                seat.Portrait.preserveAspect = true;
                seat.Portrait.raycastTarget = false;

                // The order tag, floating above the head.
                seat.Tag = NewRect("Tag", seat.Root);
                seat.Tag.anchorMin = seat.Tag.anchorMax = new Vector2(0.5f, 0);
                seat.Tag.pivot = new Vector2(0.5f, 0);
                seat.Tag.sizeDelta = new Vector2(BustW + 48f, 84f);
                seat.Tag.anchoredPosition = new Vector2(0, CharWinH + 10f);
                seat.TagBg = seat.Tag.gameObject.AddComponent<Image>();
                seat.TagBg.raycastTarget = false;
                // Vice, not parchment (the author, 2026-08-02: too much yellow): a dark
                // glassy card with one neon rule under it — the room's own palette.
                var tagRule = NewRect("Rule", seat.Tag);
                tagRule.anchorMin = new Vector2(0.06f, 0); tagRule.anchorMax = new Vector2(0.94f, 0);
                tagRule.pivot = new Vector2(0.5f, 0);
                tagRule.sizeDelta = new Vector2(0, 2);
                tagRule.anchoredPosition = new Vector2(0, 2);
                var tagRuleImg = tagRule.gameObject.AddComponent<Image>();
                tagRuleImg.color = UITheme.Cyan[3];
                tagRuleImg.raycastTarget = false;

                seat.Name = NewText("Name", seat.Tag, _body, 12, TextAnchor.UpperCenter,
                    UITheme.TextPrimary);
                Stretch(seat.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -10));
                seat.Name.horizontalOverflow = HorizontalWrapMode.Overflow;

                seat.Wants = NewText("Wants", seat.Tag, _body, 10, TextAnchor.UpperCenter,
                    UITheme.Cyan[4]);
                Stretch(seat.Wants.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -26));
                seat.Wants.horizontalOverflow = HorizontalWrapMode.Overflow;

                // Centred for real (2026-07-31): the row used to keep a 32px left inset so the
                // corner-pinned icon had room, which centred the text in a right-shifted box —
                // visibly off-centre on every seat. Now the TEXT owns the middle and the icon
                // rides just left of its measured width, per refresh, like a bullet point.
                seat.Order = NewText("Order", seat.Tag, _body, 11, TextAnchor.UpperCenter,
                    UITheme.Magenta[4]);
                Stretch(seat.Order.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -42));
                seat.Order.horizontalOverflow = HorizontalWrapMode.Overflow;

                // The drink itself, on the order row (v5 P13). The shape and colour of the
                // glass is what a busy player actually reads across five stools. 24px, so it
                // clears the patience bar; its position follows the text every refresh.
                var iconRt = NewRect("OrderIcon", seat.Tag);
                Place(iconRt, new Vector2(0.5f, 1), new Vector2(24, 24), new Vector2(0, -42));
                iconRt.pivot = new Vector2(1f, 0.5f);   // placed by its right edge, beside the text
                seat.Icon = iconRt.gameObject.AddComponent<Image>();
                seat.Icon.preserveAspect = true;
                seat.Icon.raycastTarget = false;

                // The patience gauge rides the BODY, not the ticket (P15, absorbs the P8
                // gauge item): a slim bar floating just over the head, so reading who is
                // about to walk means looking at the people, not at their paperwork.
                var clockBg = NewRect("ClockBg", seat.Root);
                clockBg.anchorMin = clockBg.anchorMax = new Vector2(0.5f, 0);
                clockBg.pivot = new Vector2(0.5f, 0);
                clockBg.sizeDelta = new Vector2(BustW * 0.72f, 8f);
                clockBg.anchoredPosition = new Vector2(0, CharWinH + 1f);
                clockBg.gameObject.AddComponent<Image>().color = UITheme.Night[0];
                seat.Gauge = clockBg;   // re-hung off each look's own head, below
                var clockFill = NewRect("ClockFill", clockBg);
                clockFill.anchorMin = new Vector2(0, 0); clockFill.anchorMax = new Vector2(0, 1);
                clockFill.pivot = new Vector2(0, 0.5f);
                clockFill.offsetMin = new Vector2(1, 1); clockFill.offsetMax = new Vector2(1, -1);
                clockFill.anchoredPosition = new Vector2(1, 0);
                seat.PatienceFill = clockFill.gameObject.AddComponent<Image>();
                seat.PatienceFill.raycastTarget = false;

                seat.Root.gameObject.SetActive(false);
                _seats.Add(seat);
            }

            // The primary action: open the menu to build a drink (GDD 24 §1), bottom-centred.
            // THE KEYS SIT ON THE BAR'S FACE, not across its shelves. At y 40 they lay
            // over the compartments the glassware stands in — the bar front is the shelf,
            // and the two most-pressed controls in the game were parked on it. The face
            // band (art rows 9..45) is empty drawn panelling and puts them nearer the
            // counter, which is where the hand already is.
            NewButton(root, "▸  MENU — MAKE A DRINK", new Vector2(0.5f, 0),
                new Vector2(300, 40), new Vector2(0, 180), UITheme.PrimaryAction, OnMenuClicked);

            // The recipe book, beside the making verb (v5 P16): the menu speaks styles now,
            // so how a drink is MADE has to live somewhere the player can read mid-shift.
            NewButton(root, "❧ BOOK", new Vector2(0.5f, 0),
                new Vector2(84, 40), new Vector2(-196, 180), UITheme.Night[3], ToggleRecipeBook);
            BuildRecipeBook(root);

            BuildDrinkGlass(root);
            BuildSnackRow(root);
            BuildServiceLog(root);
            BuildIdCard(root);

            // Day end. The panel is a SCRIM over the whole room, not a slab (the author,
            // 2026-08-07: "hala mor bir çerçeve var"). The old 940x600 plate showed a
            // purple margin all round the tablet, which read as a window frame nobody
            // asked for — now the night dims and the device is the only lit thing.
            _dayEndPanel = NewRect("DayEnd", root);
            var dayEndCanvas = _dayEndPanel.gameObject.AddComponent<Canvas>();
            dayEndCanvas.overrideSorting = true;
            dayEndCanvas.sortingOrder = 22;   // the market covers the whole room, till included
            _dayEndPanel.gameObject.AddComponent<GraphicRaycaster>();
            Stretch(_dayEndPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var panelImg = _dayEndPanel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.88f);

            var title = _dayEndTitle = NewText("Title", _dayEndPanel, _display, 16, TextAnchor.MiddleCenter, UITheme.PrimaryAction);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(900, 24), new Vector2(0, -22));
            title.text = "LAST CALL — THE BOOKS";

            // Left column: the till slip (v5 P13). Cream stock, and pinned to 16pt — a whole
            // multiple of the face's 8px design size, so the monospace columns the receipt is
            // set in actually land on the pixel grid instead of blurring between it.
            var bill = _dayEndBill = NewRect("Bill", _dayEndPanel);
            Place(bill, new Vector2(0.5f, 1), new Vector2(400, 470), new Vector2(0, -50));
            bill.gameObject.AddComponent<Image>().color = UITheme.Cream[4];
            _invoiceText = NewText("Invoice", bill, _body, 16, TextAnchor.UpperLeft, UITheme.Night[1]);
            Stretch(_invoiceText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 12), new Vector2(-14, -12));
            _invoiceText.supportRichText = true;

            // The bill's OWN way forward (2026-08-07). The day-end button moved inside the
            // tablet, and the tablet is only up on the market step — which left the books
            // with no door out of them at all. The slip carries its own now.
            _billNext = NewRect("BillNext", _dayEndPanel);
            Place(_billNext, new Vector2(0.5f, 1), new Vector2(400, 44), new Vector2(0, -530));
            var billNextImg = _billNext.gameObject.AddComponent<Image>();
            billNextImg.color = UITheme.PrimaryAction;
            var billNextBtn = _billNext.gameObject.AddComponent<Button>();
            billNextBtn.targetGraphic = billNextImg;
            billNextBtn.onClick.AddListener(OnDayEndAdvance);
            _billNextLabel = NewText("Label", _billNext, _display, 16, TextAnchor.MiddleCenter,
                UITheme.TextOnAmber);
            Stretch(_billNextLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10, 0), new Vector2(-10, 0));
            _billNextLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _billNextLabel.text = "CONTINUE  →";

            // THE DEVICE. 1096 x 700 wearing sh_ipad2, which is drawn at 274 x 175 — the
            // same ratio to five decimals — with a 28px border. A sliced Image draws its
            // border ring 1:1 whatever the rect, so the bezel lands at exactly the 28 the
            // page insets by and is even on all four sides. The old pairing put a 62px ring
            // measured off a grey mat onto a rect of a different aspect, which is the grey
            // slab with a dark edge on only two sides in the author's screenshot.
            var tablet = _dayEndTablet = NewRect("Tablet", _dayEndPanel);
            Place(tablet, new Vector2(0.5f, 0.5f), new Vector2(TabletW, TabletH), Vector2.zero);
            var tabletImg = tablet.gameObject.AddComponent<Image>();
            var shellArt = ItemArt.Load("sh_ipad2");
            if (shellArt != null)
            {
                tabletImg.sprite = shellArt;
                tabletImg.type = Image.Type.Sliced;
            }
            else tabletImg.color = TabletShell;

            // The two pieces of jewellery that make it a device, placed in CODE. In the art
            // they would sit in a stretched band and smear along it.
            var lens = NewRect("Lens", tablet);
            Place(lens, new Vector2(0.5f, 1), new Vector2(8, 8), new Vector2(0, -10));
            var lensImg = lens.gameObject.AddComponent<Image>();
            lensImg.color = TabletLens; lensImg.raycastTarget = false;
            var home = NewRect("Home", tablet);
            Place(home, new Vector2(0.5f, 0), new Vector2(120, 4), new Vector2(0, 10));
            var homeImg = home.gameObject.AddComponent<Image>();
            homeImg.color = new Color(0.42f, 0.42f, 0.46f, 1f); homeImg.raycastTarget = false;

            // THE PAGE — 1040 x 644 inside the bezel. Every band below adds up to it:
            //   20 + 40 + 32 + 8 + 400 + 8 + 128 + 8 = 644
            var screen = NewRect("Screen", tablet);
            Stretch(screen, Vector2.zero, Vector2.one,
                new Vector2(BezelX, BezelY), new Vector2(-BezelX, -BezelY));
            screen.gameObject.AddComponent<Image>().color = ShopPage;

            // THE DEVICE'S OWN STATUS BAR. It belongs to the tablet, not to the shop.
            var osBar = NewRect("OsBar", screen);
            osBar.anchorMin = new Vector2(0, 1); osBar.anchorMax = new Vector2(1, 1);
            osBar.pivot = new Vector2(0.5f, 1);
            osBar.sizeDelta = new Vector2(0, OsBarH);
            osBar.anchoredPosition = Vector2.zero;
            osBar.gameObject.AddComponent<Image>().color = new Color(0.87f, 0.89f, 0.87f, 1f);
            // The derived "02:{Day*7%60}" was never a clock — it was noise dressed as one.
            _osClock = NewText("OsClock", osBar, _body, 8, TextAnchor.MiddleLeft, ShopInk);
            Place(_osClock.rectTransform, new Vector2(0, 0.5f), new Vector2(120, 12), new Vector2(12, 0));
            var osCarrier = NewText("OsCarrier", osBar, _body, 8, TextAnchor.MiddleCenter, ShopInkSoft);
            Place(osCarrier.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200, 12), Vector2.zero);
            osCarrier.text = "TRADE NET";
            var osWifi = NewRect("OsWifi", osBar);
            Place(osWifi, new Vector2(1, 0.5f), new Vector2(20, 14), new Vector2(-52, 0));
            var wifiImg = osWifi.gameObject.AddComponent<Image>();
            wifiImg.sprite = ItemArt.Load("sh_wifi"); wifiImg.preserveAspect = true;
            wifiImg.raycastTarget = false;
            if (wifiImg.sprite == null) wifiImg.color = ShopInkSoft;
            var osBatt = NewRect("OsBatt", osBar);
            Place(osBatt, new Vector2(1, 0.5f), new Vector2(23, 9), new Vector2(-14, 0));
            var battImg = osBatt.gameObject.AddComponent<Image>();
            battImg.sprite = ItemArt.Load("sh_batt"); battImg.preserveAspect = true;
            battImg.raycastTarget = false;
            if (battImg.sprite == null) battImg.color = ShopInkSoft;

            // THE APP BAR: the house mark, the wordmark, and the money.
            var strip = NewRect("Strip", screen);
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1);
            strip.pivot = new Vector2(0.5f, 1);
            strip.sizeDelta = new Vector2(0, AppBarH);
            strip.anchoredPosition = new Vector2(0, -OsBarH);
            // FLAT, not sh_bar. That sprite is 64x32 with a 44x16 centre, so a 1040-wide bar
            // stretches its middle 23x across against 1.5x down and every mark in it becomes
            // a streak the width of the page. A bar with nothing to smear cannot smear.
            var stripImg = strip.gameObject.AddComponent<Image>();
            stripImg.color = Color.white;
            Hairline(strip, new Vector2(0, 0), new Vector2(1, 0), ShopAisle);

            var mark = NewRect("Mark", strip);
            Place(mark, new Vector2(0, 0.5f), new Vector2(32, 28), new Vector2(10, 0));
            var markImg = mark.gameObject.AddComponent<Image>();
            markImg.sprite = ItemArt.Load("sh_van") ?? ItemArt.Load("sh_mark");
            markImg.preserveAspect = true; markImg.raycastTarget = false;
            if (markImg.sprite == null) markImg.color = ShopGreen;

            // The wordmark IS the logo, and it is the one place the display face earns its
            // width. "BOOZE CRUISE" = 12 x 16 = 192 in a 200 box.
            const float BrandX = 50f;
            var brand = NewText("Brand", strip, _display, 16, TextAnchor.MiddleLeft, ShopGreenDark);
            Place(brand.rectTransform, new Vector2(0, 0.5f), new Vector2(200, 20),
                new Vector2(BrandX, 3));
            brand.horizontalOverflow = HorizontalWrapMode.Overflow;
            brand.text = ShopBrand;
            var swash = NewRect("Swash", strip);
            Place(swash, new Vector2(0, 0.5f), new Vector2(ShopBrand.Length * 16f, 3f),
                new Vector2(BrandX, -10f));
            swash.gameObject.AddComponent<Image>().color = ShopGreen;

            // The account, two rows so the word and the number cannot print through each other.
            var balance = NewRect("Balance", strip);
            Place(balance, new Vector2(1, 0.5f), new Vector2(150, 32), new Vector2(-12, 0));
            var balanceImg = balance.gameObject.AddComponent<Image>();
            var accArt = ItemArt.Load("sh_k_account");   // 150x32, a recessed readout
            if (accArt != null) balanceImg.sprite = accArt;
            else balanceImg.color = ShopGreenDark;
            var balanceLabel = NewText("BalanceL", balance, _body, 8, TextAnchor.MiddleLeft,
                new Color(0.63f, 0.85f, 0.70f, 1f));   // lit green on the dark field was 2.4:1
            Place(balanceLabel.rectTransform, new Vector2(0, 1), new Vector2(80, 10), new Vector2(10, -4));
            balanceLabel.text = "ACCOUNT";
            _tabletTill = NewText("Till", balance, _display, 16, TextAnchor.MiddleRight, Color.white);
            Place(_tabletTill.rectTransform, new Vector2(1, 0), new Vector2(126, 18), new Vector2(-10, 4));
            _tabletTill.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tabletTill.verticalOverflow = VerticalWrapMode.Truncate;

            // THE SHEET THE DEPARTMENT OPENS (the author: give it a filing feel, and let
            // the frame go round the products too). The aisle sits on a framed page whose
            // top edge runs UNDER the tabs: the lit tab is drawn with no bottom rim and
            // overlaps that edge by two units, so it does not sit beside the page, it is
            // attached to it. Four flat Images make the frame, which cannot distort at any
            // size — the thing the stretched sprites kept getting wrong.
            var page = NewRect("Page", screen);
            page.anchorMin = new Vector2(0, 1); page.anchorMax = new Vector2(1, 1);
            page.pivot = new Vector2(0.5f, 1);
            page.sizeDelta = new Vector2(-8f, 416f);        // 1040 - 8 = 1032 wide
            page.anchoredPosition = new Vector2(0, -(OsBarH + AppBarH + TabBarH - 4f));
            page.gameObject.AddComponent<Image>().color = Color.white;
            Frame(page, 2f, ShopGreenDark);

            // THE DEPARTMENT BAR, across the top. It used to be a 230-wide rail down the
            // left; demolishing it hands 274 units back to the aisle, which is the
            // difference between eight products on screen and twelve.
            var tabBar = NewRect("TabBar", screen);
            tabBar.anchorMin = new Vector2(0, 1); tabBar.anchorMax = new Vector2(1, 1);
            tabBar.pivot = new Vector2(0.5f, 1);
            tabBar.sizeDelta = new Vector2(0, TabBarH);
            tabBar.anchoredPosition = new Vector2(0, -(OsBarH + AppBarH));
            // No fill: the tabs stand on the page's own shoulder, and a band behind them
            // would draw a second horizontal line right where the seam should be.
            var tabBarImg = tabBar.gameObject.AddComponent<Image>();
            tabBarImg.color = new Color(1f, 1f, 1f, 0f);
            tabBarImg.raycastTarget = false;

            for (int i = 0; i < ShopTabs.Length; i++)
            {
                int tab = i;
                var key = NewRect($"Tab{i}", tabBar);
                Place(key, new Vector2(0, 0.5f), new Vector2(TabKeyW, 28f),
                    new Vector2(8f + i * (TabKeyW + 8f), 0));
                var bg = key.gameObject.AddComponent<Image>();
                // Drawn AT 160x28, so it goes in 1:1 — no slicing, no middle band to
                // smear. Every control below follows the same rule.
                bg.type = Image.Type.Simple;
                var btn = key.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() =>
                {
                    if (_shopTab != tab) { _justOrdered.Clear(); _shopScrollAt = 1f; }
                    _shopTab = tab;
                    RebuildDayEnd();
                });
                var icon = NewRect("I", key);
                Place(icon, new Vector2(0, 0.5f), new Vector2(20, 20), new Vector2(10, 0));
                var iconImg = icon.gameObject.AddComponent<Image>();
                iconImg.sprite = ItemArt.Load(ShopTabIcons[i]);
                iconImg.preserveAspect = true; iconImg.raycastTarget = false;
                if (iconImg.sprite == null) iconImg.color = new Color(1, 1, 1, 0);
                _shopTabIcons[i] = iconImg;
                var label = NewText("L", key, _shop, 16, TextAnchor.MiddleLeft, ShopInk);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(36, 0), new Vector2(-6, 0));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.text = ShopTabs[i];
                _shopTabKeys[i] = bg;
                _shopTabLabels[i] = label;
            }

            // Tonight's fitting, said ONCE, at the right of the department bar — it used to
            // be repeated in five places and still be missed.
            var lamp = NewRect("Lamp", tabBar);
            Place(lamp, new Vector2(1, 0.5f), new Vector2(16, 16), new Vector2(-152, 0));
            var lampImg = lamp.gameObject.AddComponent<Image>();
            lampImg.sprite = ItemArt.Load("sh_b_lock");
            lampImg.preserveAspect = true; lampImg.raycastTarget = false;
            _fittingLamp = lampImg;
            _fittingNote = NewText("Fitting", tabBar, _body, 8, TextAnchor.MiddleRight, ShopInk);
            Place(_fittingNote.rectTransform, new Vector2(1, 0.5f), new Vector2(130, 12),
                new Vector2(-12, 0));
            _fittingNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            _fittingNote.verticalOverflow = VerticalWrapMode.Truncate;

            // THE AISLE. 1004 wide at x 8..1012; the mask cuts at 1012 and the scroll track
            // starts at 1022, so a 6-column grid of 1000 has 4 units of slack inside the
            // mask and 14 units of air before the bar. The old grid asked for 790 in a 730
            // viewport and lost a third of every fourth card behind the scrollbar.
            var offerView = NewRect("OfferView", screen);
            Stretch(offerView, Vector2.zero, Vector2.one,
                new Vector2(8f, FootH + 16f),
                new Vector2(-(BarW + 18f), -(OsBarH + AppBarH + TabBarH + 8f)));
            offerView.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.003f);
            offerView.gameObject.AddComponent<RectMask2D>();
            _offerRow = NewRect("Offers", offerView);
            _offerRow.anchorMin = new Vector2(0, 1); _offerRow.anchorMax = Vector2.one;
            _offerRow.pivot = new Vector2(0.5f, 1);
            _offerRow.offsetMin = Vector2.zero; _offerRow.offsetMax = Vector2.zero;
            var shopLayout = _offerRow.gameObject.AddComponent<VerticalLayoutGroup>();
            shopLayout.spacing = 8;
            shopLayout.childControlWidth = true; shopLayout.childForceExpandWidth = true;
            shopLayout.childControlHeight = true; shopLayout.childForceExpandHeight = false;
            var shopFit = _offerRow.gameObject.AddComponent<ContentSizeFitter>();
            shopFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var shopScroll = _shopScroll = offerView.gameObject.AddComponent<ScrollRect>();
            shopScroll.viewport = offerView; shopScroll.content = _offerRow;
            shopScroll.horizontal = false;
            shopScroll.movementType = ScrollRect.MovementType.Clamped;
            // One notch of the wheel used to throw the aisle most of a screen (110 was set
            // when the author asked for a faster page and it overshot by twenty). A notch
            // should move the shelf, not the department.
            shopScroll.scrollSensitivity = 5.5f;
            shopScroll.inertia = false;

            var barTrack = NewRect("ScrollTrack", screen);
            barTrack.anchorMin = new Vector2(1, 0); barTrack.anchorMax = new Vector2(1, 1);
            barTrack.pivot = new Vector2(1, 1);
            barTrack.sizeDelta = new Vector2(BarW, -(OsBarH + AppBarH + TabBarH + FootH + 24f));
            barTrack.anchoredPosition = new Vector2(-8f, -(OsBarH + AppBarH + TabBarH + 8f));
            barTrack.gameObject.AddComponent<Image>().color = new Color(0.84f, 0.87f, 0.84f, 1f);
            var scrollbar = barTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = NewRect("Handle", barTrack);
            Stretch(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = ShopGreen;
            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handle;
            shopScroll.verticalScrollbar = scrollbar;
            shopScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            // THE FOOT: 8 + 640 + 8 + 232 + 8 + 136 + 8 = 1040.
            var foot = NewRect("Foot", screen);
            foot.anchorMin = new Vector2(0, 0); foot.anchorMax = new Vector2(1, 0);
            foot.pivot = new Vector2(0.5f, 0);
            foot.sizeDelta = new Vector2(0, FootH);
            foot.anchoredPosition = new Vector2(0, 8f);

            // THE INSPECTOR (the author: the descriptions need a box behind them). A flat
            // DARK plate inset into the white page — the one treatment that reads as a box
            // instead of as text lying on the page. Not sh_panel: a 64x48 sprite on a 640
            // rect stretches 15.4x across against 3.67x down, which is why the two panels
            // beside each other used to look like different materials.
            var inspector = NewRect("Inspector", foot);
            Place(inspector, new Vector2(0, 0.5f), new Vector2(InspectorW, FootH), new Vector2(8, 0));
            inspector.gameObject.AddComponent<Image>().color = InspectorBack;
            Hairline(inspector, new Vector2(0, 0), new Vector2(1, 0), ShopGreenDark);
            Hairline(inspector, new Vector2(0, 1), new Vector2(1, 1), ShopGreenDark);

            // ONE TEXT COLUMN AND ONE ICON GUTTER (the author: align it, and use the
            // space). Everything used to start at x 12 except the two buff lines, which
            // started at 30 because their icons sat where the other rows' text did — four
            // rows on one edge and two on another. The icons keep a 14-unit gutter at
            // x 12..26 of their own now, and EVERY text starts at x 36.
            const float InspGutter = 12f, InspText = 36f;
            const float InspCol = InspectorW - InspText - 12f;   // 560 - 36 - 12 = 512

            _inspIdentity = NewText("Identity", inspector, _shop, 16, TextAnchor.UpperLeft, InspectorInk);
            Place(_inspIdentity.rectTransform, new Vector2(0, 1), new Vector2(InspCol, 20),
                new Vector2(InspText, -6));
            _inspIdentity.horizontalOverflow = HorizontalWrapMode.Wrap;
            _inspIdentity.verticalOverflow = VerticalWrapMode.Truncate;

            _inspMeta = NewText("InspMeta", inspector, _body, 8, TextAnchor.UpperLeft, InspectorDim);
            Place(_inspMeta.rectTransform, new Vector2(0, 1), new Vector2(InspCol, 12),
                new Vector2(InspText, -28));
            _inspMeta.horizontalOverflow = HorizontalWrapMode.Wrap;
            _inspMeta.verticalOverflow = VerticalWrapMode.Truncate;

            // A rule under the head, so the identity block and the description read as two
            // things rather than five loose lines on a slate.
            var inspRule = NewRect("Rule", inspector);
            Place(inspRule, new Vector2(0, 1), new Vector2(InspectorW - 24f, 1),
                new Vector2(InspGutter, -44));
            var ruleImg = inspRule.gameObject.AddComponent<Image>();
            ruleImg.color = new Color(0.24f, 0.31f, 0.26f, 1f);
            ruleImg.raycastTarget = false;

            // The product's own mark, in the gutter beside the identity — the gutter is
            // there for the buff icons and would otherwise be a 24-unit empty strip down
            // the whole panel.
            var inspMark = NewRect("Mark", inspector);
            Place(inspMark, new Vector2(0, 1), new Vector2(16, 16), new Vector2(InspGutter, -8));
            _inspMarkImg = inspMark.gameObject.AddComponent<Image>();
            _inspMarkImg.preserveAspect = true;
            _inspMarkImg.raycastTarget = false;

            // The body: the lightest face at the smallest legal size, in SENTENCE CASE.
            // 115 characters a line against the old 43, and no shouting.
            _inspBody = NewText("InspBody", inspector, _body, 8, TextAnchor.UpperLeft, InspectorInk);
            Place(_inspBody.rectTransform, new Vector2(0, 1), new Vector2(InspCol, 30),
                new Vector2(InspText, -48));
            _inspBody.supportRichText = true;
            _inspBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _inspBody.verticalOverflow = VerticalWrapMode.Truncate;

            for (int i = 0; i < 2; i++)
            {
                float y = i == 0 ? -83f : -101f;
                var icon = NewRect("BuffI" + i, inspector);
                Place(icon, new Vector2(0, 1), new Vector2(14, 14), new Vector2(InspGutter, y - 1f));
                var ii = icon.gameObject.AddComponent<Image>();
                ii.preserveAspect = true; ii.raycastTarget = false;
                var line = NewText("Buff" + i, inspector, _body, 8, TextAnchor.MiddleLeft, InspectorInk);
                Place(line.rectTransform, new Vector2(0, 1), new Vector2(InspCol, 14),
                    new Vector2(InspText, y));
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                line.verticalOverflow = VerticalWrapMode.Truncate;
                if (i == 0) { _inspBuffAIcon = ii; _inspBuffA = line; }
                else { _inspBuffBIcon = ii; _inspBuffB = line; }
            }

            // THE ORDER, and it lists what is in it.
            var order = NewRect("Order", foot);
            Place(order, new Vector2(0, 0.5f), new Vector2(OrderW, FootH), new Vector2(576, 0));
            order.gameObject.AddComponent<Image>().color = Color.white;
            Hairline(order, new Vector2(0, 0), new Vector2(1, 0), ShopAisle);
            // A HEAVIER HEAD. 26 units and the bold face at 16: the order is the control
            // the market exists to reach, and it was whispering beside a 640-wide slab.
            var orderHead = NewRect("OrderHead", order);
            Place(orderHead, new Vector2(0, 1), new Vector2(OrderW, 26), Vector2.zero);
            orderHead.gameObject.AddComponent<Image>().color = ShopGreenDark;
            var orderIcon = NewRect("OrderI", orderHead);
            Place(orderIcon, new Vector2(0, 0.5f), new Vector2(18, 18), new Vector2(10, 0));
            var orderIconImg = orderIcon.gameObject.AddComponent<Image>();
            orderIconImg.sprite = ItemArt.Load("sh_i_cart");
            orderIconImg.preserveAspect = true; orderIconImg.raycastTarget = false;
            _cartHeadLabel = NewText("OrderHL", orderHead, _shop, 16, TextAnchor.MiddleLeft, Color.white);
            Place(_cartHeadLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(OrderW - 46, 20),
                new Vector2(36, 0));
            _cartHeadLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cartHeadLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _cartHeadLabel.text = "ORDER";

            _cartLine = NewText("OrderLines", order, _body, 8, TextAnchor.UpperLeft, ShopInk);
            Place(_cartLine.rectTransform, new Vector2(0, 1), new Vector2(OrderW - 20, 40),
                new Vector2(10, -32));
            _cartLine.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cartLine.verticalOverflow = VerticalWrapMode.Truncate;

            // THE TOTAL, on its own banded row and at a size worth reading — it is the
            // number the player decides on, and it was set at 8 in the corner.
            var totalRow = NewRect("TotalRow", order);
            Place(totalRow, new Vector2(0, 1), new Vector2(OrderW - 20, 22), new Vector2(10, -76));
            var totalBg = totalRow.gameObject.AddComponent<Image>();
            totalBg.color = new Color(0.898f, 0.937f, 0.902f, 1f);
            totalBg.raycastTarget = false;
            var totalLabel = NewText("TotalL", totalRow, _shop, 8, TextAnchor.MiddleLeft, ShopInkSoft);
            Place(totalLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(80, 12), new Vector2(8, 0));
            totalLabel.text = "TOTAL";
            _cartTotal = NewText("OrderTotal", totalRow, _display, 16, TextAnchor.MiddleRight,
                ShopGreenDark);
            Place(_cartTotal.rectTransform, new Vector2(1, 0.5f), new Vector2(180, 18),
                new Vector2(-8, 0));
            _cartTotal.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cartTotal.verticalOverflow = VerticalWrapMode.Truncate;

            _checkout = NewRect("Checkout", order);
            Place(_checkout, new Vector2(0, 0), new Vector2(OrderW - 20, 26), new Vector2(10, 6));
            var checkoutImg = _checkout.gameObject.AddComponent<Image>();
            var orderArt = ItemArt.Load("sh_k_order");
            if (orderArt != null) checkoutImg.sprite = orderArt;   // 212x26, drawn to fit
            else checkoutImg.color = ShopGreen;
            var checkoutBtn = _checkout.gameObject.AddComponent<Button>();
            checkoutBtn.targetGraphic = checkoutImg;
            checkoutBtn.onClick.AddListener(Checkout);
            _checkoutLabel = NewText("L", _checkout, _shop, 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(_checkoutLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(6, 0), new Vector2(-6, 0));
            _checkoutLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _checkoutLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _checkoutLabel.text = "PLACE ORDER";

            // The way out, bottom right of the device.
            _openTomorrow = NewRect("OpenTomorrow", foot);
            Place(_openTomorrow, new Vector2(0, 0.5f), new Vector2(ExitW, FootH), new Vector2(896, 0));
            var otImg = _openTomorrow.gameObject.AddComponent<Image>();
            var exitArt = ItemArt.Load("sh_k_exit");   // 136x128, its own heavy bevel
            if (exitArt != null) otImg.sprite = exitArt;
            else otImg.color = UITheme.PrimaryAction;
            var otBtn2 = _openTomorrow.gameObject.AddComponent<Button>();
            otBtn2.targetGraphic = otImg;
            otBtn2.onClick.AddListener(OnDayEndAdvance);
            _openTomorrowLabel = NewText("Label", _openTomorrow, _shop, 16, TextAnchor.MiddleCenter,
                UITheme.TextOnAmber);
            Stretch(_openTomorrowLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(6, 0), new Vector2(-6, 0));
            _openTomorrowLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _openTomorrowLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _openTomorrowLabel.text = "OPEN\nTOMORROW";

            _dayEndPanel.gameObject.SetActive(false);

            _bannerText = NewText("Closed", root, _display, 22, TextAnchor.MiddleCenter, UITheme.ViceRed[3]);
            Place(_bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 120), new Vector2(0, 60));
            _bannerText.gameObject.SetActive(false);

            BuildLedgerPanel(root);
            BuildGuide(root);
        }

        /// <summary>The register's book of past days (GDD 24 §7, 2026-07-22): a scrollable
        /// list of every closed day — income, expenses, net, and the room's mood.</summary>
        private void BuildLedgerPanel(RectTransform root)
        {
            _ledgerPanel = NewRect("Ledger", root);
            Place(_ledgerPanel, new Vector2(0.5f, 0.5f), new Vector2(560, 560), new Vector2(0, 10));
            var panelImg = _ledgerPanel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(UITheme.Night[1].r, UITheme.Night[1].g, UITheme.Night[1].b, 0.98f);
            // Catch clicks so the world behind the book stays untouched.
            panelImg.raycastTarget = true;

            var title = NewText("Title", _ledgerPanel, _display, 16, TextAnchor.MiddleCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -44), new Vector2(0, -10));
            title.text = "THE REGISTER — DAYS PAST";

            // Column header, then the rows on cream stock beneath it. The header names the
            // TOP line of each entry; every entry now carries a second and third line under
            // it, which no fixed column head could describe.
            var header = NewText("Header", _ledgerPanel, _body, 8, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(header.rectTransform, new Vector2(0, 1), new Vector2(504, 20), new Vector2(28, -52));
            header.text = "DAY        TOOK        PAID OUT         NET        TILL";

            var sheet = NewRect("Sheet", _ledgerPanel);
            Place(sheet, new Vector2(0.5f, 1), new Vector2(508, 424), new Vector2(0, -76));
            sheet.gameObject.AddComponent<Image>().color = UITheme.Cream[4];

            _ledgerRows = NewRect("Rows", sheet);
            Stretch(_ledgerRows, Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12));
            var layout = _ledgerRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperLeft;

            NewButton(_ledgerPanel, "CLOSE", new Vector2(0.5f, 0),
                new Vector2(200, 38), new Vector2(0, 18), UITheme.PrimaryAction, () => ToggleLedger());

            _ledgerPanel.gameObject.SetActive(false);
        }

        /// <summary>Opens or closes the register's ledger; refreshes the rows on open.
        /// The book and the licence never share the screen — opening one closes the other.</summary>
        private void ToggleLedger()
        {
            if (_ledgerPanel == null || Run == null) return;
            bool show = !_ledgerPanel.gameObject.activeSelf;
            if (show) { CloseId(); RefreshLedger(); }
            _ledgerPanel.gameObject.SetActive(show);
        }

        private void RefreshLedger()
        {
            for (int i = _ledgerRows.childCount - 1; i >= 0; i--)
                Destroy(_ledgerRows.GetChild(i).gameObject);

            var history = Run.Ledger.History;
            if (history.Count == 0)
            {
                var empty = NewText("Empty", _ledgerRows, _body, 14, TextAnchor.UpperLeft, UITheme.Night[1]);
                empty.rectTransform.sizeDelta = new Vector2(0, 28);
                empty.text = "No days on the books yet — close a night first.";
                return;
            }

            // Newest day on top: the last thing you did is the first thing you read.
            //
            // Three lines a night, not one. The top line is the money as it was — what came in,
            // what went out, the net and the till it left behind. Under it the income is split
            // into what was CHARGED and what was TIPPED, and the outgoings into rent, stock and
            // fittings, because "you lost $180" and "the rent was fine, you spent $210 stocking
            // the shelf" are different nights and only the second one can be played differently.
            // The last line is the room: who drank, who left without a drink, and what the night
            // itself was worth in stars before the standing averaged it away.
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var d = history[i];
                bool red = d.Net < 0;
                var head = NewText($"Day{d.Day}", _ledgerRows, _body, 12, TextAnchor.UpperLeft,
                    red ? UITheme.ViceRed[3] : UITheme.Night[1]);
                head.rectTransform.sizeDelta = new Vector2(0, 20);
                head.supportRichText = true;
                string net = red ? $"-${-d.Net}" : $"+${d.Net}";
                string till = d.HasDetail
                    ? (d.TillAfter < 0 ? $"-${-d.TillAfter}" : $"${d.TillAfter}") : "";
                head.text = $"DAY {d.Day,-3}   ${d.Income,-7} ${d.Expenses,-8} {net,-8} {till}";

                if (!d.HasDetail)
                {
                    // A day booked before the book kept its detail. Say so rather than
                    // printing zeroes that would read as a night where nothing happened.
                    var bare = NewText($"Day{d.Day}Bare", _ledgerRows, _body, 8, TextAnchor.UpperLeft,
                        UITheme.Cream[1]);
                    bare.rectTransform.sizeDelta = new Vector2(0, 16);
                    bare.text = $"        {MoodLabel(d.AverageSatisfaction)} night · no breakdown kept";
                    Spacer(12);
                    continue;
                }

                var money = NewText($"Day{d.Day}Money", _ledgerRows, _body, 8, TextAnchor.UpperLeft,
                    UITheme.Cream[1]);
                money.rectTransform.sizeDelta = new Vector2(0, 16);
                money.supportRichText = true;
                var outgoings = new List<string>();
                if (d.Rent > 0) outgoings.Add($"rent ${d.Rent}");
                if (d.Stock > 0) outgoings.Add($"stock ${d.Stock}");
                if (d.Upgrades > 0) outgoings.Add($"fittings ${d.Upgrades}");
                money.text = $"        drinks ${d.Sales} · tips ${d.Tips}"
                           + (outgoings.Count > 0 ? "   —   " + string.Join(" · ", outgoings) : "");

                var room = NewText($"Day{d.Day}Room", _ledgerRows, _body, 8, TextAnchor.UpperLeft,
                    d.WalkedOut > 0 ? UITheme.ViceRed[2] : UITheme.Cream[1]);
                room.rectTransform.sizeDelta = new Vector2(0, 16);
                room.supportRichText = true;
                string walked = d.WalkedOut > 0
                    ? $" · {d.WalkedOut} left without one" : " · nobody left thirsty";
                room.text = $"        {d.Served} served{walked} · {d.NightStars:0.0}★ on the night"
                          + $" · {MoodLabel(d.AverageSatisfaction)}";

                Spacer(12);
            }
        }

        /// <summary>A blank row between ledger entries — three lines a night need the air, or
        /// the book reads as one wall of numbers.</summary>
        private void Spacer(float height)
        {
            var gap = NewRect("Gap", _ledgerRows);
            gap.sizeDelta = new Vector2(0, height);
        }

        private static string MoodLabel(double satisfaction) =>
            satisfaction >= DayLedger.HighRollerBar ? "GREAT"
            : satisfaction >= DayLedger.BrokeBar ? "OK"
            : "SOUR";

        private void OnOpenTomorrow()
        {
            var run = Run;
            run.ContinueToNextDay();
            _dayEndPanel.gameObject.SetActive(false);
            if (run.Phase == TycoonPhase.DayOpen)
            {
                _lastPhase = TycoonPhase.DayOpen;
                ApplyBarLook();
            }
        }

        /// <summary>
        /// ONE LISTING, 160x208 portrait. The old card was 190x104 landscape carrying up to
        /// five texts, a rotated stamp and a tick — as much as 800 units of type on a 190
        /// plate, saying the same thing four different ways and contradicting itself twice
        /// (a just-ordered stool printed "SOLD", a SOLD stamp AND "+ ADD").
        ///
        /// A tile now carries at most five objects: a state strip, a corner chip, the
        /// product, two short texts and one control. Everything a listing has to EXPLAIN
        /// moved to the inspector, which is where the author asked for it.
        /// </summary>
        private RectTransform AddTile(TileSpec spec)
        {
            var state = spec.State;
            bool hasPill = state == TileState.Orderable || state == TileState.Unaffordable
                        || state == TileState.Picked || state == TileState.Refundable
                        || state == TileState.NoFitting;

            var rt = NewRect("Tile", _cardTarget != null ? _cardTarget : _offerRow);
            var img = rt.gameObject.AddComponent<Image>();
            var plateArt = ItemArt.Load("sh_tile");
            if (plateArt != null) { img.sprite = plateArt; img.type = Image.Type.Sliced; }
            img.color = PlateOf(state);

            // The click. A tile that cannot be acted on gets no Button at all, so the
            // pointer itself says whether there is anything here to do.
            if (spec.OnClick != null)
            {
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                var act = spec.OnClick;
                button.onClick.AddListener(() => act());
            }

            // Hovering fills the inspector — the one place long text is allowed to live.
            // NOT an EventTrigger: it implements IScrollHandler too, so it ate the mouse
            // wheel and froze the aisle over every tile that had something to read.
            var shown = spec;
            var hover = rt.gameObject.AddComponent<HoverRelay>();
            hover.Entered = () => ShowInspector(shown);
            hover.Exited = () => ShowInspector(null);

            // 1 — THE STRIP: 8 x 208 of solid state colour down the left edge. 1664 units,
            // which is 1.7x the area of the entire old thumbnail, and it reads from across
            // the room without a single character being set.
            var strip = NewRect("Strip", rt);
            Place(strip, new Vector2(0, 0), new Vector2(8f, TileH), Vector2.zero);
            var stripImg = strip.gameObject.AddComponent<Image>();
            stripImg.color = StripOf(state);
            stripImg.raycastTarget = false;
            if (state == TileState.Sealed)
            {
                var hatch = ItemArt.Load("sh_strip_seal");
                // Drawn 1:1, so no tiling — and WHITE, because an Image tints its sprite
                // and the hatch is already crate-brown. Tinting brown art brown twice is
                // how the one textured strip in the market came out as a flat dark bar.
                if (hatch != null) { stripImg.sprite = hatch; stripImg.color = Color.white; }
            }

            // 2 — THE PRODUCT, on the shelf line every class shares.
            // Glassware is mostly transparent, and transparent on a white page is nothing
            // at all (the author: the glasses disappear). A vessel gets a recess to stand
            // in — a shaded back with a lit lip at the foot line — the way the bar's own
            // back shelf gives them something to be seen against. Bottles are opaque and
            // need none of it.
            if (spec.Art != null && spec.ArtH == VesselH)
            {
                var niche = NewRect("Niche", rt);
                Place(niche, new Vector2(0.5f, 0), new Vector2(116, 112), new Vector2(0, 64));
                var ni = niche.gameObject.AddComponent<Image>();
                // sh_niche2 is DRAWN AT 116x112 — the exact rect — so it goes in 1:1: no
                // slicing, no stretch, no smear. A back-bar alcove is a thing in the bar
                // rather than a piece of chrome, which is why this one is generated.
                // sh_niche3: a cool DISPLAY CASE in the page's own family. The generated warm
                // wooden alcove was a lovely picture of somewhere else — a brown lamp-lit
                // cupboard pasted into a white-and-green catalogue.
                var nicheArt = ItemArt.Load("sh_niche3") ?? ItemArt.Load("sh_niche2");
                if (nicheArt != null) ni.sprite = nicheArt;
                else ni.color = ShopAisle;
                ni.raycastTarget = false;
                // A dead listing dims the recess with the glass in it rather than going
                // pale — a pale recess is the white page again, and the vessel vanishes
                // exactly where it was supposed to become visible.
                if (state == TileState.Unaffordable || state == TileState.Held)
                    ni.color = new Color(0.72f, 0.74f, 0.72f, 1f);
            }
            if (spec.Art != null)
            {
                var thumb = NewRect("Art", rt);
                PlaceProduct(thumb, spec.Art, spec.ArtH);
                var ti = thumb.gameObject.AddComponent<Image>();
                ti.sprite = spec.Art;
                ti.raycastTarget = false;
                ti.color = state == TileState.Unaffordable ? new Color(0.78f, 0.80f, 0.80f, 0.55f)
                    : state == TileState.Held ? new Color(0.86f, 0.88f, 0.85f, 0.85f)
                    : Color.white;
            }
            else if (state == TileState.Sealed)
            {
                // A crate the house will not open, and it is the WHOLE tile that is shut:
                // the chains run corner to corner (the author — a 78px X in the middle read
                // as an ornament, not as something chained), drawn at the tile's own
                // 160x208 so no link is stretched into an oval, with the padlock where they
                // cross. No product and no name: the empty well is the tell.
                var chain = NewRect("Chain", rt);
                Stretch(chain, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var chainImg = chain.gameObject.AddComponent<Image>();
                chainImg.sprite = ItemArt.Load("sh_chain_x") ?? ItemArt.Load("sh_chain");
                chainImg.raycastTarget = false;
                chainImg.color = new Color(1f, 1f, 1f, 0.95f);
                var padlock = NewRect("Lock", rt);
                Place(padlock, new Vector2(0.5f, 0.5f), new Vector2(42, 63), new Vector2(0, 6));
                var lockImg = padlock.gameObject.AddComponent<Image>();
                lockImg.sprite = ItemArt.Load("sh_lock");
                lockImg.preserveAspect = true;
                lockImg.raycastTarget = false;
            }

            // 3 — THE NAME, title case straight from the JSON. Two lines of 26 characters;
            // the longest string in the game is 24 and lands on one.
            //
            // A SEALED crate is laid out differently, and it has to be: the chains cross
            // the whole tile now, so a star gate sitting in the bottom-left action row
            // printed straight through them. The gate belongs directly under the padlock,
            // centred, where the eye already is — and with the chains and the lock saying
            // "sealed" three ways over, the crate needs no left-aligned name beside them.
            if (state == TileState.Sealed)
            {
                // A TAG HUNG ON THE LOCK. The chains cross the whole tile, so a star gate
                // set straight onto the plate lands on the links whatever row it sits in —
                // it needs its own ground, not a better y. A dark tag under the padlock is
                // that ground, and it is the thing a chained crate would actually carry.
                var tag = NewRect("Tag", rt);
                Place(tag, new Vector2(0.5f, 1), new Vector2(104, 44), new Vector2(0, -132));
                var tagImg = tag.gameObject.AddComponent<Image>();
                tagImg.color = new Color(ShopInk.r, ShopInk.g, ShopInk.b, 0.92f);
                tagImg.raycastTarget = false;
                var gate = NewText("Gate", tag, _display, 16, TextAnchor.MiddleCenter, Color.white);
                Place(gate.rectTransform, new Vector2(0.5f, 1), new Vector2(96, 20),
                    new Vector2(0, -5));
                gate.horizontalOverflow = HorizontalWrapMode.Wrap;
                gate.verticalOverflow = VerticalWrapMode.Truncate;
                gate.text = spec.Money;
                var what = NewText("Sealed", tag, _body, 8, TextAnchor.MiddleCenter,
                    new Color(0.63f, 0.68f, 0.64f, 1f));
                Place(what.rectTransform, new Vector2(0.5f, 1), new Vector2(96, 12),
                    new Vector2(0, -27));
                what.horizontalOverflow = HorizontalWrapMode.Wrap;
                what.verticalOverflow = VerticalWrapMode.Truncate;
                what.text = "TO UNSEAL";
            }
            else
            {
                // The BOLD face at 8 (the author: a little bigger, a little heavier).
                // 16 is the next legal size — the pixel faces only rasterise cleanly at
                // whole multiples of 8 — and "Grand Mariner Triple Sec" at 16 is 300
                // units against a 140 column, which is three lines in a two-line box.
                // Silkscreen Bold at 8 is the whole of the available move: 6.25/char
                // mixed, so the same 24 characters take 150 and wrap to two lines of 22.
                var name = NewText("Name", rt, _shop, 8, TextAnchor.UpperLeft,
                    state == TileState.Unaffordable || state == TileState.Held
                        ? ShopInkSoft : ShopInk);
                Place(name.rectTransform, new Vector2(0, 1), new Vector2(ContentW, 22),
                    new Vector2(12, -144));
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                name.text = spec.Name;
            }

            // 4 — ONE contextual token, or the stock meter where stock IS the fact.
            if (spec.StockFrac >= 0f)
            {
                float frac = Mathf.Clamp01(spec.StockFrac);
                var track = NewRect("Track", rt);
                Place(track, new Vector2(0, 1), new Vector2(100, 6), new Vector2(12, -171));
                var trackImg = track.gameObject.AddComponent<Image>();
                trackImg.color = ShopInkSoft;      // dark: a pale track on white paper was
                trackImg.raycastTarget = false;    // 1.2:1 and could not be seen at all
                var fill = NewRect("Fill", rt);
                // Width IS the fraction, so the bar cannot overflow its track by construction.
                Place(fill, new Vector2(0, 1), new Vector2(100f * frac, 6), new Vector2(12, -171));
                var fillImg = fill.gameObject.AddComponent<Image>();
                fillImg.color = frac < 0.25f ? ShopCost : StripStock;
                fillImg.raycastTarget = false;
                var pct = NewText("Pct", rt, _body, 8, TextAnchor.MiddleRight, TileMetaInk);
                Place(pct.rectTransform, new Vector2(1, 1), new Vector2(34, 12), new Vector2(-8, -168));
                pct.text = Mathf.RoundToInt(frac * 100f) + "%";
            }
            else if (!string.IsNullOrEmpty(spec.Meta) && state != TileState.Sealed)
            {
                var meta = NewText("Meta", rt, _body, 8, TextAnchor.MiddleLeft, TileMetaInk);
                Place(meta.rectTransform, new Vector2(0, 1), new Vector2(ContentW, 12),
                    new Vector2(12, -168));
                meta.horizontalOverflow = HorizontalWrapMode.Wrap;
                meta.verticalOverflow = VerticalWrapMode.Truncate;
                meta.text = spec.Meta;
            }

            // 5 — THE ACTION ROW: one money token, and at most one control. Both texts
            // TRUNCATE rather than overflow — Overflow is exactly how the old badge walked
            // 165 units onto its neighbour.
            if (!string.IsNullOrEmpty(spec.Money) && state != TileState.Sealed)
            {
                // Sealed puts its price — the star gate — under the padlock instead.
                var money = NewText("Money", rt, MoneyFace(spec.Money), 16, TextAnchor.LowerLeft,
                    MoneyInk(state));
                Place(money.rectTransform, new Vector2(0, 0), new Vector2(66, 20), new Vector2(12, 6));
                money.horizontalOverflow = HorizontalWrapMode.Wrap;
                money.verticalOverflow = VerticalWrapMode.Truncate;
                money.text = spec.Money;
            }
            else if (!string.IsNullOrEmpty(spec.Word) && state != TileState.Held)
            {
                // Held says it on the sash across the product, so the action row stays
                // empty — printing FULL twice on one tile is the habit this rewrite was
                // supposed to break.
                var word = NewText("Word", rt, _shop, 16, TextAnchor.LowerLeft, MoneyInk(state));
                Place(word.rectTransform, new Vector2(0, 0), new Vector2(66, 20), new Vector2(12, 6));
                word.horizontalOverflow = HorizontalWrapMode.Wrap;
                word.verticalOverflow = VerticalWrapMode.Truncate;
                word.text = spec.Word;
            }

            if (hasPill && !string.IsNullOrEmpty(spec.PillVerb))
            {
                var pill = NewRect("Pill", rt);
                Place(pill, new Vector2(1, 0), new Vector2(70, 20), new Vector2(-8, 6));
                var pillImg = pill.gameObject.AddComponent<Image>();
                var pillArt = ItemArt.Load("sh_k_add") ?? ItemArt.Load("sh_pill");
                if (pillArt != null) pillImg.sprite = pillArt;   // 70x20, drawn to fit
                pillImg.color = PillOf(state);
                pillImg.raycastTarget = false;
                var label = NewText("L", pill, _shop, 8, TextAnchor.MiddleCenter, PillInk(state));
                Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(6, 0), new Vector2(-6, 0));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.text = spec.PillVerb;
            }

            // 6 — the picked tile is the only one wearing a frame on all four sides.
            if (state == TileState.Picked) Frame(rt, 2f, StripPicked);

            // A BAND ACROSS THE PRODUCT for anything there is nothing to buy on (the
            // author: besides going grey it should say FULL right on the box). Grey art
            // alone asks the player to notice an absence; a word across the bottle states
            // it. It sits over the product and under the name, so it can never collide
            // with a text — there is no text in the art band by construction.
            if (state == TileState.Held && !string.IsNullOrEmpty(spec.Word))
            {
                var band = NewRect("Sash", rt);
                Place(band, new Vector2(0, 1), new Vector2(TileW, 30), new Vector2(0, -62));
                var bandImg = band.gameObject.AddComponent<Image>();
                bandImg.color = new Color(ShopInk.r, ShopInk.g, ShopInk.b, 0.86f);
                bandImg.raycastTarget = false;
                var bandText = NewText("L", band, _shop, 16, TextAnchor.MiddleCenter, Color.white);
                Stretch(bandText.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(6, 0), new Vector2(-6, 0));
                bandText.horizontalOverflow = HorizontalWrapMode.Wrap;
                bandText.verticalOverflow = VerticalWrapMode.Truncate;
                bandText.text = spec.Word;
            }

            // 7 — THE CHIP, added last so nothing can draw over the state glyph.
            // THE CHIP, square on the corner (the author: the exclamation sits crooked).
            // It was 18x18 hung on the tile's corner while the state strip runs 8 wide down
            // the same edge, so the chip overhung the strip by 10 and every glyph inside it
            // read as off-centre against the two edges the eye actually measures from. It
            // is 20x20 now, inset 2 from both, so the square is symmetric on the corner and
            // whatever sits in it is centred on the square.
            var chip = NewRect("Chip", rt);
            Place(chip, new Vector2(0, 1), new Vector2(20, 20), new Vector2(2, -2));
            var chipImg = chip.gameObject.AddComponent<Image>();
            chipImg.color = StripOf(state);
            chipImg.raycastTarget = false;
            string glyphArt = GlyphSpriteOf(state);
            if (glyphArt != null)
            {
                var g = NewRect("G", chip);
                Stretch(g, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
                var gi = g.gameObject.AddComponent<Image>();
                gi.sprite = ItemArt.Load(glyphArt);
                gi.preserveAspect = true;
                gi.raycastTarget = false;
                gi.color = ChipInk(state);
            }
            else
            {
                // _shop, not _display: PressStart2P sets "!" with its own side bearing
                // inside a 16-wide cell, so a MiddleCenter box centres the CELL and leaves
                // the mark visibly left of true. Silkscreen Bold's marks are drawn on
                // their own centre.
                var g = NewText("G", chip, _shop, 16, TextAnchor.MiddleCenter, ChipInk(state));
                Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                g.horizontalOverflow = HorizontalWrapMode.Overflow;
                g.verticalOverflow = VerticalWrapMode.Overflow;
                g.text = state == TileState.Orderable ? "+"
                    : state == TileState.Unaffordable ? "!" : "=";
            }
            // The stamp lands on the CHIP now, not across the whole plate: the old one was
            // a 160-wide rotated word over a 190 card, printing through the name under it.
            if (state == TileState.Ordered && !Motion.Reduced)
                StartCoroutine(StampDrop(chip));
            return rt;
        }

        // ── the state language, in one place ─────────────────────────────────────
        // Seven answers, seven rows. Keeping them as switches beside each other is what
        // makes "no two states may look alike" checkable rather than hopeful.

        private static Color StripOf(TileState s) =>
            s == TileState.Orderable ? StripStock
            : s == TileState.Unaffordable ? StripDeny
            : s == TileState.Picked ? StripPicked
            : s == TileState.Ordered ? ShopGreenDark
            : s == TileState.Sealed ? StripSealed
            : s == TileState.Refundable ? StripReturn
            : StripHeld;                       // Held and NoFitting

        private static Color PlateOf(TileState s) =>
            s == TileState.Unaffordable ? PlateDeny
            : s == TileState.Picked ? PlatePicked
            : s == TileState.Ordered ? PlateOrdered
            : s == TileState.Sealed ? PlateSealed
            : s == TileState.Refundable ? PlateReturn
            : s == TileState.Held ? ShopAisle
            : s == TileState.NoFitting ? PlateDeny
            : ShopPage;

        /// <summary>Ink ON the strip. White reads on the four dark strips and fails on the
        /// three light ones — a glyph nobody can see is not a colour-blind fallback.</summary>
        private static Color ChipInk(TileState s) =>
            s == TileState.Orderable || s == TileState.Picked
            || s == TileState.Held || s == TileState.NoFitting
                ? ShopInk : Color.white;

        private static string GlyphSpriteOf(TileState s) =>
            s == TileState.Picked ? "sh_g_tick"
            : s == TileState.Ordered ? "sh_van"
            : s == TileState.Sealed ? "sh_lock"
            : s == TileState.Refundable ? "sh_g_back"
            : s == TileState.NoFitting ? "sh_b_lock"
            : null;                            // Orderable / Unaffordable / Held set a letter

        private static Color PillOf(TileState s) =>
            s == TileState.Orderable ? StripStock
            : s == TileState.Picked ? StripPicked
            : s == TileState.Refundable ? StripReturn
            : new Color(0.720f, 0.720f, 0.700f, 1f);

        private static Color PillInk(TileState s) =>
            s == TileState.Picked ? PickedInk
            : s == TileState.Orderable || s == TileState.Refundable ? Color.white
            : new Color(0.24f, 0.24f, 0.22f, 1f);

        private static Color MoneyInk(TileState s) =>
            s == TileState.Unaffordable ? StripDeny
            : s == TileState.Picked ? PickedInk
            : s == TileState.Ordered ? ShopGreen
            : s == TileState.Refundable ? StripReturn
            : s == TileState.Held || s == TileState.Sealed ? ShopInkSoft
            : ShopGreenDark;

        // ── tiny UI helpers (mirroring the house style) ─────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private RectTransform Panel(RectTransform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rt = NewRect(name, parent);
            Stretch(rt, anchorMin, anchorMax, offsetMin, offsetMax);
            rt.gameObject.AddComponent<Image>().color = color;
            return rt;
        }

        private void NewButton(RectTransform parent, string label, Vector2 anchor,
            Vector2 size, Vector2 pos, Color fill, Action onClick)
        {
            var rt = NewRect(label, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = fill;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            // A face of its own, so the hover lift moves the label with the plate rather than
            // sliding the plate out from under it (PressSink moves one transform).
            var face = NewRect("Face", rt);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var sink = rt.gameObject.AddComponent<PressSink>();
            sink.Face = face; sink.Depth = 3f; sink.Squash = 0.015f; sink.Lift = 2f; sink.Tint = img;

            var text = NewText("Label", face, _body, 12, TextAnchor.MiddleCenter,
                fill == UITheme.PrimaryAction ? UITheme.TextOnAmber : UITheme.TextPrimary);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.text = label;
        }

        private Text NewText(string name, Transform parent, Font font, int size,
            TextAnchor anchor, Color color)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        /// <summary>A border on all four sides, t units thick. `Hairline` cannot do it —
        /// it hardcodes a 1-unit height — and a picked tile needs to be visible from six
        /// columns away, which one thin rule at the top was not.</summary>
        private void Frame(RectTransform parent, float t, Color c)
        {
            for (int i = 0; i < 4; i++)
            {
                var rt = NewRect("Edge", parent);
                bool horizontal = i < 2;
                rt.anchorMin = horizontal ? new Vector2(0, i) : new Vector2(i - 2, 0);
                rt.anchorMax = horizontal ? new Vector2(1, i) : new Vector2(i - 2, 1);
                rt.pivot = new Vector2(horizontal ? 0.5f : i - 2, horizontal ? i : 0.5f);
                rt.sizeDelta = horizontal ? new Vector2(0, t) : new Vector2(t, 0);
                rt.anchoredPosition = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.color = c;
                img.raycastTarget = false;
            }
        }

        /// <summary>
        /// The product, drawn from the SPRITE's own shape rather than squeezed into a fixed
        /// box. `preserveAspect` in a fixed rect was the old way and it is why a 44x142
        /// bottle drew 17 units wide in a 46x56 slot — 5% of the card.
        ///
        /// The rect IS the aspect, so nothing is letterboxed, and every class shares one
        /// foot line (from-top 140) so a shelf of forty silhouettes reads as one shelf.
        /// An upscale is floored to a whole step: pixel art magnified 1.79x is a blur,
        /// magnified 1x or 2x it is pixel art. A downscale is free to be fractional —
        /// that only drops rows.
        /// </summary>
        private static void PlaceProduct(RectTransform rt, Sprite s, float boxH)
        {
            float w = s.rect.width, h = s.rect.height;
            float k = Mathf.Min(ContentW / w, boxH / h);
            if (k >= 1f) k = Mathf.Floor(k);
            Place(rt, new Vector2(0.5f, 0f), new Vector2(w * k, h * k), new Vector2(0f, 68f));
        }

        /// <summary>
        /// Which face a money token is set in. The display face is the one a shopper reads
        /// first, but it is exactly `fontSize` wide per character, so it only fits while the
        /// string is short: the price slot is 66 units, which is four display characters and
        /// no more. A refunded legendary glass is "+$105" — five — and would have walked 14
        /// units onto the pill. Anything that long drops to the body face, where the same
        /// string measures 58.
        /// </summary>
        private Font MoneyFace(string token) => token.Length <= 4 ? _display : _body;

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max,
            Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }
    }
}
