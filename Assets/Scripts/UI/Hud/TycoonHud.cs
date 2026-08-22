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
        private Text _crowdText;
        /// <summary>The bar's standing (v5 P12): the average, then five filled/empty stars.
        /// Replaces the TONIGHT satisfaction bar (D3) — reputation is what the player steers
        /// by now, and it carries between nights instead of resetting every morning.</summary>
        private RectTransform _starsFill;
        private SegmentClock _clock;      // the hour, drawn as a readout and not as a word
        private Image _neonTube, _neonBloom;       // the beam's own light, and the state light
        private bool _clockWasLast;       // the readout only re-tints when the state flips
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

        /// <summary>How far the whole cast stands BELOW the counter's rest line (2026-08-20,
        /// the author: "tüm müşteriler 16 pixel aşağı kaysın"). It moves the stool's rect, not
        /// the body alone, so the figure, the ticket over their head, the patience bar and the
        /// click target all travel together and nothing has to be re-measured against anything
        /// else. 16 screen units is 8 of the room's own pixels and 8 of the character's — a
        /// whole number in both grids, which a pixel drawing has to be moved by or it lands
        /// between its own pixels. The counter's props (the dirty glass) do NOT take it: they
        /// stand on the bar, and the bar has not moved.</summary>
        private const float SeatDrop = 16f;
        /// <summary>Where a stool's rect sits with the cellar shut.</summary>
        private const float SeatLineBaseY = CounterLineY - SeatDrop;
        /// <summary>
        /// Where a stool's rect actually sits. NOT a constant since the counter grew a cellar
        /// (2026-08-22): opening it lifts the whole room, and the author's mock-ups lift the
        /// drinkers with it — their heads sit 121 art px higher in the open frame, the same
        /// travel as the room. This is the ONE place that has to know, because the tag rides
        /// the seat rect as a child and the BODY is derived from the same anchoredPosition
        /// every frame, so both follow from this number and cannot drift apart.
        /// </summary>
        private float SeatLineY =>
            SeatLineBaseY + (stage != null
                ? stage.DrawerPhase * DiegeticStage.DrawerTravel * StageToHud : 0f);
        private const float BustW = 108f;

        /// <summary>How far apart the stools stand along the counter. It was a local const
        /// inside the builder; it is up here because the order ticket's width cap is DERIVED
        /// from it, and the two numbers agreeing by hand is exactly how the tickets came to
        /// overlap.</summary>
        private const float SeatGap = 180f;

        /// <summary>How wide an order ticket may grow before its order line wraps instead
        /// (2026-08-02). No longer a taste (2026-08-19, the author: "yan yana olan
        /// müşterilerin baloncukları üst üste binmemeli"): every ticket is centred on its own
        /// stool, so two full-width neighbours touch the moment this passes SeatGap. It was
        /// 236 against a gap of 180 — a card and a half of overlap — and the 16pt display
        /// face made every named customer hit the cap. SeatGap less twelve leaves a clear
        /// dozen units of room between two of the widest tickets the bar can draw.</summary>
        private const float TagMaxW = SeatGap - 12f;

        /// <summary>How SMALL a ticket may get (2026-08-19, the author: "gerekmedikçe de
        /// büyük olmamalı"). It used to have a floor of BustW + 48 = 156, which was fine
        /// while every ticket carried a name and an order — and absurd the moment a customer
        /// thinking about it says nothing but "...". The floor is now the plate's own two
        /// borders plus a character of air, so a thinking bubble is the size of a thought.</summary>
        private const float TagMinW = 34f;
        /// <summary>How far the ticket floats over its own owner's head. It was 15, which
        /// was the right number while the ticket ended in a flat edge — the spout added
        /// 2026-08-19 hangs six units BELOW that edge and landed straight on the patience
        /// gauge, which floats at head + 1 and is eight tall. 22 puts the point of the spout
        /// three units clear of the gauge's top and still reads as pointing at them.</summary>
        private const float TagLift = 22f;
        /// <summary>The air inside the plate, past its 5-unit border. Both axes.</summary>
        private const float TagPad = 7f;
        /// <summary>The plate's foot — the one dark row across the bottom of the white field
        /// (ChromeArt.Bubble). The content box owes the BOTTOM this much extra, or the type
        /// sits a row low in the white it is centred in (2026-08-19, the author: "yazılar +
        /// görseller baloncuğun tam ortasında gözükmeli").</summary>
        private const float TagFoot = 1f;
        /// <summary>The icon row's height. (The rows of TYPE no longer have a constant: a
        /// guessed row height is exactly what stopped the ticket being centred — 22 was set
        /// against a 20-unit line and left every plate two units tall at the bottom. Each row
        /// is now as tall as the font says it is; see SeatView.NameLineH.)</summary>
        private const float IconRowH = 24f;
        /// <summary>The thinking beat: how long each of ".", "..", "..." holds. Slow enough
        /// to read as thinking rather than as a fault in the screen.</summary>
        private const float DotBeat = 0.42f;
        /// <summary>How fast an order is spoken, in characters a second (the author: "yazılar
        /// konuşma metni gibi harf harf gelecek"). 20 puts SEX ON THE BEACH on the ticket in
        /// eight tenths of a second — long enough to read as speech, short enough that a busy
        /// bar never waits on it.</summary>
        private const float SpeakCps = 20f;
        /// <summary>Walk-in speed, in HUD units a second, and the number the whole gait
        /// hangs off. MEASURED off the drawing, twice — and the first measurement was wrong
        /// in a way worth keeping written down, because it is the reason the author said
        /// arriving took too long.
        ///
        /// I estimated a "small step" at 22 art px and set 90. The frames say otherwise: at
        /// full spread the feet stand 77 px apart (clubgirl) and 88 (heavyset), which is a
        /// heel-to-heel stride near 60 once a foot's own length is taken off — so a cycle
        /// carries the figure about 120 art px, not 44. The floor was moving at less than
        /// half the feet: the walk was not slow, it was SLIPPING, and every stool was a
        /// twelve-second journey.
        ///
        /// 120 art px a cycle at PatronFps 12 (nine frames, 0.75s) is 160 art px a second,
        /// which is 320 HUD units. 310 sits a hair under, where the feet grip.</summary>
        private const float WalkSpeed = 310f;
        /// <summary>How far out from the stool the arrival ease begins, and how slow it
        /// gets there. Both the floor and the cycle are scaled by it — see AdvanceWalkIn.</summary>
        private const float ArrivalEase = 260f, ArrivalPace = 0.45f;
        /// <summary>Frames a second for the walk: nine frames at nine is one cycle, two
        /// strides, a second — the pace WalkSpeed is measured against. Re-read this
        /// whenever the walk is re-cut; it is the drawing's frame count, not a taste.</summary>
        private const float PatronFps = 12f;   // ONE RATE FOR EVERY CLIP (2026-08-19,
        // the author: "FPS tum animasyonlarda ayni olmali"). The walk sets it, because
        // the walk is the clip measured against the world: nine frames, two strides, one
        // cycle a second, and WalkSpeed is derived from that. Everything else follows, so
        // nobody speaks in fast-forward beside somebody walking at nine.
        // (ExitSpeed is gone, 2026-08-19 — the author: "çıkış animasyonu giriş animasyonu ile
        //  aynı hızda aynı şekilde". The exit is the entrance mirrored and shares WalkSpeed,
        //  ArrivalEase and ArrivalPace; see AdvanceExit.)
        private const float OffscreenMargin = 150f; // how far past the right edge they start/finish
        /// <summary>The "placing the order" beat. The clip is two halves joined,
        /// about seventeen frames, which at PatronFps runs 1.42s - the beat is a hair
        /// longer, because a beat shorter than its clip cuts the sentence in half.</summary>
        private const float OrderAnimSeconds = 1.5f;
        /// <summary>ONE SIP CYCLE, END TO END: the glass goes up, they drink, it comes back
        /// down, and they stand with it at their side until the cycle is up. FIXED, and the
        /// rest at the end is whatever is left over — so a character whose clip shipped with
        /// fewer frames simply stands a moment longer, and three cycles is the same 13.2
        /// seconds for everybody. <c>TycoonConfig.Default</c>'s savour is exactly 3 × this,
        /// which is what makes a customer get up at the END of a sip instead of halfway
        /// through raising the glass (they were, at 4.1 cycles — see the note there).</summary>
        private const float DrinkCycleSeconds = 4.4f;
        /// <summary>How long each of the three frames AT THE LIPS is held, in ticks of
        /// 1/PatronFps (2026-08-20, the author: "yudum aldığı 3 frame çok daha yavaş olmalı
        /// ki yudum alıyor hissi uyandırsın"). Five ticks is 0.42s a frame and 1.25s across
        /// the three, which is how long a mouthful actually takes; at one tick each the
        /// whole swallow went by in a quarter of a second and read as a flinch.</summary>
        private const int DrinkSipTicks = 5;

        // The animated customer (2026-07-23): a full-body pixel sprite shown from about the waist
        // up, with the counter clipping the legs. Frames load from Resources/Patron/<clip>.
        // Re-aligned off the ART's own bbox (2026-07-31, the author's note): the figure spans
        // y 13–171 of the 180px canvas, so at 350 the head tops out at -FootDrop+324.7 and the
        // feet reach -FootDrop+17.5. FootDrop 150 crops ~43% of the figure below the counter
        // (the same waist as before), and the window ends 5px over the head — the gauge and
        // the tag now HUG the head instead of floating 28px above it.
        /// <summary>The rig's canvas (2026-08-19). The cast is stood on it by
        /// Tools/patron_ship.py, and the game draws it at ONE ART PIXEL PER STAGE UNIT —
        /// which is not a preference but the condition for belonging to the room: the room
        /// itself is drawn at one art pixel per stage unit, and a character drawn at any
        /// other rate has pixels of a different size from the wall behind them.</summary>
        private const float CharCanvas = 220f;
        private const float CharSize = CharCanvas * StageToHud;
        /// <summary>1, and it has to be. The old rig stretched every body 18% wider because
        /// that cast was drawn lanky; the 2026-08-19 cast is drawn in proportion, and 1.18
        /// on it is simply a squash — a non-uniform scale on pixel art, which is the one
        /// thing the art bible will not have.</summary>
        private const float CharWiden = 1f;
        private const float CharWinH = CharCanvas;
        /// <summary>How far the canvas's bottom hangs below the counter's far edge, in HUD
        /// units. MEASURED, not tuned: the bar's front face is 83 art px tall in counter.png
        /// (rows 67–149; rows 0–66 are its top surface seen at 30°), so a customer standing
        /// at it has their feet 83 units down, and the rig leaves 10 px of air under the
        /// shoes — 93 stage units, doubled into HUD.</summary>
        private const float CharFootDrop = 93f * StageToHud;
        /// <summary>Idle is a STILL frame here, not a loop (2026-08-19, the author: "nefes
        /// alış veriş istemiyorum … sabit durmalı, biraz biraz sağa sola bakınmalı gibi").
        /// The two glances carry the looking-around, and the same pair does double duty:
        /// an early frame is the small idle glance, the measured hold frame is the full turn
        /// at whoever just sat down beside them.</summary>
        // ONE WAY TO DRINK, with the glass drawn in the hand (2026-08-19, the author:
        // "sadece 1 tarz drinking olsun, ayri ayri uretme, bardak elinde olsun"). Three
        // grips keyed off the served glass were built and taken out again; the trade is
        // written down where it belongs, in Tools/patron_trial_gen.py - every customer now
        // drinks from the same plain glass whatever the recipe called for.
        private enum PatronClip { Idle, Order, Drink, Walk, Cheer, Upset, LookRight, LookLeft }
        /// <summary>The reaction beat before they leave: the same joined length as
        /// the order, so the clip lands back on the idle pose exactly as the beat ends
        /// and they walk off from the pose they were standing in.</summary>
        private const float ReactSeconds = 1.5f;

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
            /// <summary>Which frame of each glance is the far end of the turn. MEASURED off
            /// the shipped frames (Tools/patron_ship.py prints them), because only half of
            /// these clips end on the turn: some hold the look to the last frame and some
            /// swing back to the front, and a clip that swings back cannot be held on its
            /// end. So the game holds the frame furthest from the first one instead.</summary>
            public int HoldRight, HoldLeft;
            /// <summary>Where that head lands in HUD units, from the rig's own constants:
            /// the sprite is drawn CharSize tall off the rig canvas and pushed down by
            /// CharFootDrop so the counter takes the legs.</summary>
            public float HeadTop => (CharCanvas - HeadY) * (CharSize / CharCanvas) - CharFootDrop;
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
        private static readonly (string Slug, float HeadY, float Stars, int HoldRight, int HoldLeft)[]
            PatronCast =
        {
            // ── the 2026-08-19 rig, and for now the whole cast ───────────────────
            // Two people, on purpose (the author: "sadece şimdilik bu karakterler gözüksün
            // detaylı inceleyebilmem için"). Everyone drawn before them stands on the OLD
            // rig — a 180 canvas, drawn lanky and stretched 18% wider to compensate — and
            // loading them beside these would draw them at this rig's scale, which is not a
            // comparison, just a mistake. They come back one at a time as each is redrawn
            // to this skeleton and this line language, which is the author's own plan.
            //
            // Awaiting that redraw, with the star tier each already earned:
            //   0.0  nightnurse courier undone dockman bearded barber bouncer ember
            //   1.5  coder inked studentm nerd bikeryoung lumber cueball violet teal
            //        gothpunk
            //   2.5  wanderer studentf gothgirl bikerold profess chrome platina gothqueen
            //        linen
            //   3.5  glam execwoman execman
            //
            // Head rows and hold frames are MEASURED by Tools/patron_ship.py off the
            // shipped frames — never typed by hand, and re-measured whenever the art moves.
            ("clubgirl", 7f, 0f, 4, 6),
            ("heavyset", 13f, 0f, 5, 5),
            // The second pair, drawn to the same rig and MEASURED into the same bands
            // rather than eyeballed into them (Tools/patron_trial_gen.judge prints all
            // three): head-to-body inside the cast's own 0.112–0.166, silhouette under
            // 72% dark, and 47 and 51 colours against a cast that runs 37–57.
            ("silkwoman", 6f, 0f, 8, 5),
            ("pastelman", 2f, 0f, 4, 5),
            ("shaved", 7f, 0f, 5, 5),
            ("silverbob", 11f, 0f, 6, 6),
            ("afrowoman", 0f, 0f, 7, 6),
            ("eastasianman", 7f, 0f, 5, 6),
            // The last two before the casting pauses, both the author's own descriptions,
            // and both DRAWN AGAIN on 2026-08-20 rather than filtered. For one day their
            // keyline was stripped off the finished frames; the author threw that out
            // ("hicbir karakterde siyah kontur olmamali ... dogal kontur olacak"), so the
            // brief was changed where the ink was actually coming from - the waistcoat lost
            // its tailoring words, the leopard print lost its black - and each was rolled
            // several times with the best of the batch adopted on measurement.
            ("spanishsuit", 7f, 0f, 7, 6),
            ("leopard", 4f, 0f, 4, 6),
        };
        /// <summary>
        /// The papers for a face — name, age, country, flag — read from the cast file.
        ///
        /// WHO A DRINKER IS on paper used to be written here: thirty people in a Dictionary
        /// in the middle of a UI class, chosen against the drawings (the age matches the face
        /// the artist drew; the eight non-American passports are the ones the picture itself
        /// argued for). That is content, and content is data — so it moved to
        /// Assets/Data/customers/papers.json on 2026-08-12, where a writer can add a person
        /// without opening C# and where the story's characters can share the same table
        /// (PLAN_last_call S0).
        /// </summary>
        private Papers PapersFor(PatronLook look) =>
            _bootstrap != null && _bootstrap.Cast != null && look != null
                ? _bootstrap.Cast.For(look.Slug ?? "")
                : null;


        /// <summary>This drinker's papers, or null for a look nobody has written up.</summary>
        /// <summary>
        /// What this drinker is called — the ONE name the bar says about them, wherever it
        /// says it: the licence prints it, the ticket over their head repeats it, the receipt
        /// shortens it to the first word.
        ///
        /// It belongs to the FACE and not to the archetype (2026-08-10, ShowId). The ticket
        /// went on reading the archetype's name for another day, so the card said MARILOU
        /// CABRERA over a photograph while the stool beside it said MARGUERITE — the same
        /// disagreement that fix was written for, one screen further out (the author,
        /// 2026-08-11: "kimlikteki isimlerle kafa üstündeki isimler eşleşmiyor"). A look with
        /// no papers on file falls back to the archetype's name, and the card falls back the
        /// same way, so the two agree even when there is nothing to agree about.
        /// </summary>
        private string NameOn(CustomerVisit visit, PatronLook look)
        {
            // The story's guest carries their OWN name, not the borrowed face's (GDD 26 §1b).
            // Until Ece's portrait is drawn her plate wears somebody else's picture, and a
            // name read off that picture would introduce her as Serena Fontana.
            if (visit != null && visit.OnTheHouse && visit.Regular != null
                && !string.IsNullOrEmpty(visit.Regular.Name)) return visit.Regular.Name;
            var papers = PapersFor(look);
            if (papers != null && !string.IsNullOrEmpty(papers.Name)) return papers.Name;
            return visit?.Regular != null && !string.IsNullOrEmpty(visit.Regular.Name)
                ? visit.Regular.Name : "Customer";
        }

        private readonly List<PatronLook> _looks = new List<PatronLook>();
        private RectTransform _hudRoot;            // the canvas rect — the screen's right edge for entrances

        private sealed class SeatView
        {
            public RectTransform Root;       // the customer + tag, positioned at the counter (click target)
            public CanvasGroup Group;        // fades them in as they walk up
            public SpriteRenderer Body;      // the animated character, a WORLD sprite so the room's lights reach it
            public RectTransform Tag;        // the floating order ticket above the head
            public Image TagBg;
            public Text Name;
            public Text Wants;
            public Text Order;
            public Image Icon;               // the ordered drink, drawn by DrinkIcon (v5 P13)
            public Image PatienceFill;
            public int Index;                // which stool along the bar, left to right
            public float NeighbourT;         // how long somebody has been sitting beside them
            public float SeatX;              // this stool's resting x
            public DirtyGlass Dirty;         // the empty glass left on this stool (D2)
            public RectTransform DirtyProp;  // its clickable prop on the counter
            public float WalkT;              // 0..1 walk-in progress
            public float WalkPace;           // 1 at the door, ArrivalPace at the stool
            public bool SawRight, SawLeft;   // was a neighbour there last frame
            public bool Greeting;            // playing the one-shot glance at a newcomer
            public bool GreetRight;          // which way that glance goes
            public float GreetT;             // how far into it
            public bool Exiting;             // playing the leave animation
            public float ExitT;              // 0..1 leave progress
            public bool ExitStorm;           // stormed off (angry exit) vs served (calm)
            public CustomerVisit Visit;      // who is assigned to this stool (stable until they leave)
            public float AnimClock;          // running time for the looping clips (idle, walk)
            public bool WasOrdered;          // edge-detect the deciding→ordered moment
            public Image Tail;               // the bubble's spout, its own sprite (never sliced)
            public RectTransform IconRow;    // the drink, a rule, and the serving spec
            public Image IconRule;           // the drawn bar between what and how
            public Image[] Garnish;          // one mark per thing on the spec
            public bool WasKnown;            // edge-detect the licence being read
            public float SpeakFrom;          // when the order started being spoken
            public bool Spoken;              // the order has finished arriving
            public float OrderAnimLeft;      // remaining "placing the order" one-shot time
            public float DrinkT;             // time since they started drinking
            public float ReactLeft;          // remaining departure-reaction one-shot time
            public PatronClip ReactClip;     // Cheer or Upset, chosen from their satisfaction
            public PatronLook Look;          // who is sitting here, and how tall they are
            public RectTransform Gauge;      // the patience bar, re-hung off their own head
            // How tall ONE line of each row really is, asked of the font at build time (the
            // faces and sizes never change after that). The plate's height is the sum of the
            // rows it is showing, which is what makes the type land in the middle of it.
            public float NameLineH, WantsLineH, OrderLineH;
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
        // 166x190 is the drawing's own size. It was standing at 184x210 — 1.105x, chosen by
        // eye — so the well's hoops came back at uneven thicknesses (GDD 16 §3, found by
        // `LastCall → Audit UI`). Size the container to the art, never the art to the container.
        private const float BinW = 166f, BinH = 190f;

        private RectTransform _drinkGlass;
        private Image _drinkGlassLiquid;
        private Image _drinkGlassSurface;   // the drink's top face, as the ellipse it is
        private Image _drinkGlassArt;
        private GlasswareDefinition _drinkGlassware;
        private int _drinkGlassTier = 1;
        private Image _drinkGlassBack;
        private RectTransform _glassRack;
        /// <summary>The room, asked where its shelf compartments are.</summary>
        private DiegeticStage _stage;
        private const float CarriedGlassHeight = 116f;
        // CLICK-TO-SERVE (2026-08-11, the author's loop rework — experimental, replacing
        // the drag): the ready glass stands at its home; clicking a customer whose order
        // was TAKEN sends it sliding down the counter to them, and the serve happens on
        // ARRIVAL — never before, because ServeTo empties the vessel and the ready gate
        // would hide a glass still in flight.
        private bool _glassGrabbed;          // in hand, following the cursor
        private Vector2 _glassVel;           // the carry spring's velocity
        // STIFF (2026-08-11, the author). Critical damping for k=340 is 36.9, so 31 keeps
        // just enough overshoot for the drink to feel like it has weight in it without the
        // glass trailing the hand. The old carry was a third of this and read as sloshing
        // the glass through the air rather than carrying it.
        private const float GlassCarryStiffness = 340f, GlassCarryDamping = 31f;
        private bool _glassServing;          // sliding down the counter to a seat
        private bool _glassReturning;        // refused on arrival: sliding home
        private int _glassServeSeat = -1;
        private float _glassServeT, _glassServeDur;
        private Vector2 _glassServeFrom, _glassServeTo;
        private float _glassAngle;
        private Vector2 _glassHome;
        private bool _glassShown;
        private const float GlassSlideMax = 0.22f;   // a full-counter slide, distance-scaled below

        // day end — two steps now (the author, 2026-08-01): first the bill alone, then
        // the market, each with its own verb on the same button.
        private RectTransform _dayEndPanel;
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
        private Image _idFlag;
        private Text _idRelLabel, _idIntentLabel;
        private Text _idCitizen, _idNumber, _idVisitCount;
        private Image[] _idStars;       // the grey five, always drawn
        private Image[] _idStarFills;   // the amber over them, filled to the fraction
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
            public readonly int Box;         // the lit 20-point box, -1 = no bar on this row
            public readonly double Best;     // the run's best-make share, -1 = no tick

            public SpecRow(string style, string label, string amount = "", int minTier = 1,
                bool hint = false, int box = -1, double best = -1)
            { Style = style; Label = label; Amount = amount; MinTier = minTier; Hint = hint;
              Box = box; Best = best; }
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

        /// <summary>The five boxes' colours, box 0 to box 4 — the author's ladder (red,
        /// orange, yellow, green, dark green) mapped onto the palette's own ramps. Amber[3]
        /// is skipped on purpose: it is the Money colour, and a signal must never wear a
        /// sacred number's coat (GDD 16).</summary>
        private static readonly Color[] BandBoxColors =
        {
            UITheme.ViceRed[3], UITheme.Amber[2], UITheme.Amber[4],
            UITheme.Lime[3], UITheme.Lime[1],
        };

        /// <summary>The sight glass's own size. 72 wide so the five measures land on whole
        /// pixels (70 of interior, 14 to a measure) — a gauge whose scratches sit on
        /// fractions is the smooth-where-the-game-is-pixel tell (GDD 16 §6.10).</summary>
        private const float GaugeW = 72f, GaugeH = 12f;

        /// <summary>How tall one line of a spec card is — the bottle icons are square to it.</summary>
        private const float SpecRowH = 20f;

        /// <summary>A footnote row — the line that spells out a word like SPIRIT.</summary>
        private const float SpecHintH = 13f;

        /// <summary>The share column. Wide enough for "100%" in the display face, which is
        /// four whole 16px cells — the old 52 clipped it and pushed COFFEE LIQUEUR into it.</summary>
        private const float SpecAmountW = 70f;

        /// <summary>How wide the hover spec is, beside the card.</summary>
        private const float TipW = 252f;

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
        private readonly Image[] _shopTabLits = new Image[ShopTabs.Length];
        private int _shopTab;
        private Text _tabletTill;

        /// <summary>
        /// PALM CARGO — the trade the bar orders from. Renamed for the Miami turn
        /// (2026-08-19, the author: "logosu palmiye ve ada şeklinde olmalı, ismi miami
        /// temasına uygun"): a supply company off some key down the coast, whose mark is a
        /// palm on its own island (<see cref="ChromeArt.Isle"/>) — the name says what the
        /// van does and the mark says where it comes from. A parody storefront in the same
        /// spirit as the shelf's parody brands; the wordmark is set in the display face
        /// rather than drawn, because the generator cannot spell and a shop that misprints
        /// its own name is not a shop. Renaming is this constant and nothing else — every
        /// position is measured. "PALM CARGO" = 10 x 16 = 160 in the 200 box.
        /// </summary>
        private const string ShopBrand = "PALM CARGO";

        // THE STOREFRONT'S PALETTE, v2 (the author, 2026-08-19: miami/vice, eski bir Windows
        // 98 sitesi hissi, mavi→pembe fade). The house green is GONE — every job it did
        // (the frame, the tabs, the wordmark, the account readout) is now the vice fade or
        // one of the two ramps it runs between.
        //
        // What did NOT change is the one thing that was ever load-bearing: the aisle is a
        // PAGE and the page is light. The dark storefront failed the only test that matters
        // — whether the products can be read — and a vice-blue field under white type would
        // fail it exactly the same way. The fade dresses the WINDOW; the page inside it
        // stays paper. The two 98 face greys below carry a blue cast so the chrome and the
        // fade read as one machine rather than as a grey app with a pink hat.
        //
        // The state colours further down (StripStock, StripDeny, StripPicked, …) are NOT
        // part of this and did not move: those are signals — gain is Lime, refusal is
        // ViceRed, money is Amber (16 §5, sacred) — and a theme does not get to repaint a
        // signal. That the "stock" strip stays green is the rule working, not a leftover.
        /// <summary>The house colour: the blue end of the fade, and the frame's own body.</summary>
        private static readonly Color ShopVice = UITheme.ClubBlue[2];
        /// <summary>A resting tab is a leaf BEHIND the page, and a 98 window's face is grey
        /// — this is that grey with the house blue mixed into it, so five resting tabs read
        /// as files behind the open one rather than as five buttons.</summary>
        private static readonly Color ShopPaper = new Color(0.753f, 0.769f, 0.812f, 1f);
        /// <summary>The lit edge along the open tab's top: the pink end of the fade, which
        /// is the one bright line in the row, so the eye finds the open file without
        /// reading a word.</summary>
        private static readonly Color ShopTabLit = UITheme.Magenta[4];
        /// <summary>How long a tab's title takes to come up to white. Short enough to feel
        /// like a response, long enough to read as a light coming on rather than a swap.</summary>
        private const float TabFade = 0.18f;
        /// <summary>The pink end: the accent that answers the blue, used where the old green
        /// was "lit" rather than structural.</summary>
        private static readonly Color ShopViceLit = UITheme.Magenta[3];
        /// <summary>The outline everything on this page is cut with — the darkest step of
        /// the blue end, so the frame belongs to the fade rather than being a black box
        /// drawn round it.</summary>
        private static readonly Color ShopViceDeep = UITheme.ClubBlue[0];
        private static readonly Color ShopPage = new Color(0.969f, 0.973f, 0.984f, 1f);    // paper, cooled
        private static readonly Color ShopAisle = new Color(0.863f, 0.871f, 0.906f, 1f);   // rail
        /// <summary>Type. ClubBlue's darkest step rather than an off-palette near-black:
        /// 15.6:1 on the page, and the ink belongs to the same ramp as the frame.</summary>
        private static readonly Color ShopInk = UITheme.ClubBlue[0];
        private static readonly Color ShopInkSoft = new Color(0.353f, 0.376f, 0.451f, 1f);

        // ── the 98 window's own bevel ───────────────────────────────────────────────
        // Not a new idiom: 16 §1 already says a bevel is four hairlines, lit top and left,
        // shadowed right and bottom. This is that, with the two colours a 1998 control
        // surface used — and it is the reason the chrome reads as a MACHINE rather than as
        // rectangles, without a single sprite being stretched.
        /// <summary>The lit face of a raised edge — the light is up and to the left.</summary>
        private static readonly Color BevelLit = new Color(0.937f, 0.945f, 0.973f, 1f);
        /// <summary>The shadowed face. ClubBlue[1] and not a grey, so the shadow under a
        /// control is the same night the frame is cut from.</summary>
        private static readonly Color BevelShade = UITheme.ClubBlue[1];
        /// <summary>Secondary type ON a tile. ShopInkSoft was the obvious pick and it fails
        /// AA against four of the seven plate tints (3.4:1 on the sealed crate) — a grey
        /// chosen for white paper does not survive being reused on coloured paper.</summary>
        private static readonly Color TileMetaInk = new Color(0.278f, 0.298f, 0.361f, 1f);

        // THE STATE LANGUAGE (2026-08-09). Four independent channels — strip hue, chip
        // glyph, plate tint, and whether a control exists at all — so no two states lean on
        // colour alone and the colour-blind path still reads.
        private static readonly Color StripStock = new Color(0.290f, 0.706f, 0.400f, 1f);
        private static readonly Color StripDeny = new Color(0.706f, 0.243f, 0.259f, 1f);
        private static readonly Color StripPicked = new Color(0.937f, 0.678f, 0.180f, 1f);
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
        // that reads as a box rather than as text lying on the page. Recut on ClubBlue for
        // the vice storefront (2026-08-19) — it was the one large dark field on the page and
        // a green one left the old theme's fingerprint on it.
        private static readonly Color InspectorBack = UITheme.ClubBlue[0];
        private static readonly Color InspectorInk = new Color(0.886f, 0.898f, 0.937f, 1f);
        private static readonly Color InspectorDim = new Color(0.596f, 0.620f, 0.706f, 1f);
        private static readonly Color BuffGood = new Color(0.427f, 0.847f, 0.518f, 1f);
        private static readonly Color BuffCost = new Color(0.965f, 0.741f, 0.310f, 1f);
        private static readonly Color BuffBad = new Color(0.878f, 0.353f, 0.376f, 1f);
        /// <summary>What a thing costs you. ViceRed's own step, not a hand-picked red —
        /// refusal and cost are the same voice (16 §5).</summary>
        private static readonly Color ShopCost = UITheme.ViceRed[2];

        // The page, top to bottom: 20 + 40 + 32 + 8 + 400 + 8 + 128 + 8 = 644, which is the
        // screen a 1096x700 device leaves inside a 28 bezel. Every one of these is load
        // bearing; the old set carried a hardcoded 436 that overshot its own rail by 34.
        private const float OsBarH = 20f, AppBarH = 40f, TabBarH = 38f, TabKeyW = 160f;
        /// <summary>A file's tabs: the open one stands taller than the ones behind it.</summary>
        private const float TabRestH = 30f, TabLiveH = 38f;
        // The foot, re-balanced (the author: bring the basket forward). It reads as a
        // sum and has to: 8 + 560 + 8 + 312 + 8 + 136 + 8 = 1040, the screen's width.
        // The inspector gives up 80 units and the order takes them: the order is the
        // control the whole market exists to reach and it was the quietest thing on
        // the page, against a 640-wide dark slab shouting beside it.
        // THE FOOT: 8 + 880 (the basket) + 8 + 136 (the way out) + 8 = 1040.
        private const float FootH = 128f, BasketW = 880f, ExitW = 136f, CheckoutW = 212f;
        /// <summary>The reading card that rides the pointer — narrow, because it stands over
        /// the aisle it is describing and must not cover the neighbours.</summary>
        private const float ShopCardW = 320f;

        /// <summary>How far the foot stands off the device's own bottom edge.</summary>
        private const float FootY = 8f;

        /// <summary>The air between the aisle's sheet and the foot under it — a margin, and
        /// the only thing between the two panels. Measured from the foot's TOP, not from the
        /// screen's bottom, or the foot's own stand-off is quietly counted as part of it.</summary>
        private const float AisleGutter = 24f;

        /// <summary>How far inside its own sheet the aisle is cut. The mask, the scroll track
        /// and the page all read it, so the frame and the cut cannot part company.</summary>
        private const float PageInset = 12f;
        private const float BarW = 10f;
        // The tile, and the three product classes that share one shelf line.
        // 230, not 208 (2026-08-19). The card took on a name PLATE and a price TAG, and
        // the rows it already had would have had to pay for them — the art was the only
        // row with anything to give and it had already given 16 units this morning. A card
        // that gained two objects is a taller card; the aisle scrolls.
        private const float TileW = 160f, TileH = 236f, ContentW = 140f;
        // A bottle is the tallest thing the aisle draws and it may not exceed TileArtH:
        // PlaceProduct stands every product on ProductFootY and grows it UPWARD, so
        // ProductFootY + BottleH is the top of the drawing and TileH - TileCapH is the roof.
        // 118, not 134 (2026-08-19) — it is still by some way the biggest thing on the card.
        private const float BottleH = 118f, VesselH = 100f, IconH = 96f;

        // ── THE CARD'S GRID (2026-08-19) ────────────────────────────────────────
        //
        // Every row on this card used to be placed by its own hand-tuned y — the name at
        // -144, the meta at -168, the money at 6 up from the foot, the gauge at a third
        // number derived from a fourth — and the card LOOKED like that: things landed near
        // each other rather than on anything. (16 §6.4: a caption floating half a line
        // above the thing it captions reads as carelessness even when nobody can name it.)
        //
        // So there is one column of numbers now and every element on the card is placed
        // from it. They are measured DOWN FROM THE TOP, they sit on a 2-unit rhythm, and
        // they add up to TileH exactly:
        //
        //     0   – 4     the colophon (the storefront's fade — grey when locked)
        //     10  – 128   the product, with SIX units of air above it and six below
        //     134 – 174   the name, on its own plate, two lines of 16
        //     176 – 188   the meta, one line of 8
        //     190 – 204   the state: one mark and one word
        //     206 – 226   the foot: the price tag, and the key
        //     226 – 230   air, so nothing sits ON the plate's own edge
        //
        // If a row changes height, the rows under it move by the same amount and the sum
        // must still be 208. That is the whole discipline; it is what the old card had
        // none of.
        //
        // THREE RULES THIS COLUMN HAS TO KEEP, all of them learned the hard way from a
        // screenshot (2026-08-19):
        //
        //   The art is FRAMED BY AIR, and the two gaps match. It used to start at 4, hard
        //   against the colophon, so every bottle grew out of the fade bar while sitting six
        //   units clear of its own name — the card looked hung from the top rather than laid
        //   out. Six above and six below, and the product floats in the middle of its band.
        //
        //   TileArtH >= the tallest art. The first cut of this grid gave the art 124 and
        //   moved the product's foot to 80 — and BottleH is 134, so every can, carton and
        //   bottle on the mixer board grew straight out of the top of its own card. The art
        //   band and the art heights are one number apart and have to be checked together;
        //   BottleH came down to fit rather than the band going up, because the rows under
        //   it had nothing left to give.
        //
        //   The last row STOPS SHORT of TileH. The foot ran to 208 exactly, which is the
        //   plate's own edge, so the ADD key and the price were drawn sitting on the border
        //   — the "butonlar çerçeveyle kesişiyor" in the author's shot. Six units of air is
        //   the difference between a key on a card and a key through one.
        private const float TilePad = 10f;      // the left and right margin, both sides
        private const float TileCapH = 4f;
        /// <summary>The air over the product AND under it. One number, because the author's
        /// note is that the two must be equal — "yukarıdaki bar ile olacakları mesafe
        /// aşağıdaki isimleriyle olacakları mesafeyle aynı olmalı".</summary>
        private const float TileArtAir = 6f;
        private const float TileArtTop = TileCapH + TileArtAir, TileArtH = 118f;
        /// <summary>Two lines of 16 and the air round them. 46, not 40 (2026-08-19, measured
        /// in play): the legacy Text renderer sets 16 on a 19.2 line, so two lines need 39 —
        /// and a 40 box minus a 2-unit inset each side left 36, which TRUNCATED the second
        /// line. "Resurface the Bar" shipped to the screenshot reading RESURFACE.</summary>
        private const float TileNameTop = TileArtTop + TileArtH + TileArtAir, TileNameH = 46f;
        private const float TileMetaTop = 182f, TileMetaH = 12f;
        private const float TileStateTop = 196f, TileStateH = 14f;
        private const float TileFootTop = 212f, TileFootH = 20f;
        private const string ShopIdleTip =
            "Point at anything to read it. You only pay when you place the order.";

        private Text _fittingNote, _checkoutLabel, _cartHeadLabel, _osClock;
        private Text _cartTotal, _cartTotalLabel;
        /// <summary>The basket's row of picked things: one chip per line, icon and price,
        /// click to take it back out (2026-08-11, the author).</summary>
        private RectTransform _cartChips;
        private Text _cartEmpty;
        // The reading panel, on the POINTER (2026-08-11, the author: "ürünlerin açıklamasını
        // pop-up gibi mouse üzerine taşırız ve alt barı tamamen sepet olarak kullanırız").
        // It used to be a 560-wide slab bolted into the foot, which is where the basket
        // needed to be: a description is read for a second and a basket is watched all night.
        private RectTransform _shopCard, _shopCardRule;
        private Text _cardIdentity, _cardMeta, _cardBody, _cardBuffA, _cardBuffB;
        private Image _cardBuffAIcon, _cardBuffBIcon, _fittingLamp, _cardMarkImg;
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
            public Sprite Art;          // what it looks like, for its chip in the basket
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
            /// <summary>What a SEALED tile's tag says under its number. Null = "STARS TO
            /// OPEN", which is what every lock in the shop was until the draught tower
            /// (2026-08-19): a tower is held back by the rung below it, and a padlock
            /// promising a star that opens nothing is worse than no label at all.</summary>
            public string GateNote;
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
            /// <summary>Set on a tile that sells a DRINK. The pointer then gets the whole
            /// spec card — prep, shares, glass, and what the shelf is missing — instead of
            /// a sentence about it (2026-08-10).</summary>
            public RecipeDefinition Recipe;
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
            // The beer font on the counter is the only door onto the draught station now
            // (2026-08-15): the kegs left the back-bar wall, and a pint is poured by walking
            // to the tap. The flow's own guard turns the click down between days.
            if (stage != null) stage.SetTapHandler(OnTapClicked);
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
                SyncPatronBody(v);
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
            VesselArt.ClearCache();     // measurements are of sprites, so they go with them
            _lastPhase = TycoonPhase.DayOpen;
            _lastStormedCount = 0;
            ResetSeats();
            _dayEndPanel.gameObject.SetActive(false);
            _bannerText.gameObject.SetActive(false);
            _flow?.CloseFlow();
            CloseId();
            if (_ledgerPanel != null) _ledgerPanel.gameObject.SetActive(false);
            if (_drinkGlass != null)
            {
                _drinkGlass.gameObject.SetActive(false);
                _glassShown = false;
                _glassServing = false; _glassReturning = false; _glassServeSeat = -1;
                _glassGrabbed = false;
            }
            _lastFixtureCount = -1;   // force the dressing to re-sync against the new run
            ApplyBarLook();
        }

        private void Update()
        {
            // THE CURTAIN STEPS FIRST, AND UNCONDITIONALLY. Everything below is gated on
            // there being a run, and a full-screen black that is gated on game state is a
            // black screen waiting to happen: any frame where Run is null — between runs,
            // or on the first frames of one — would leave it up with nothing to lift it.
            StepCurtain();
            StepCheckoutLamp();

            var run = Run;
            if (run == null) return;
            WatchGlassRack();
            WatchFixtures();
            FadeShopTabs();
            StepSlide();
            RunTheTill(run);
            FollowPointerWithRecipeTip();
            FollowPointerWithShopCard();
            FollowPointerWithShopSpec();

            if (run.Phase == TycoonPhase.DayOpen)
            {
                // Menus slow the world (GDD 24 §10): mixing or reading a licence must not
                // cost a storm-off by itself, but the clock never fully stops.
                bool menuOpen = (_flow != null && _flow.IsOpen) ||
                                (_idRoot != null && _idRoot.gameObject.activeSelf);
                // The night does not start until the room is up (2026-08-10): the curtain
                // holds the clock, so nobody walks in through a black screen and no patience
                // is spent on a night the player cannot see yet.
                if (!DoorsClosed)
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
            SyncLastCall(run);    // after the seats: the guest is one of them
            UpdateOrderTip();     // after the seats: it reads the tickets they just placed
            UpdateDrinkGlass();
            UpdateEscape();
            StepStarDrop();
            StepStamp();
            StepMoneyDrops();
            StepCheckoutKey();
            StepDayEndBeats();
        }

        /// <summary>
        /// Escape shuts whatever sheet is open, topmost first (2026-08-11, the author).
        ///
        /// Order matters and it is the drawing order backwards: the thing lying over
        /// everything else is the thing the key belongs to. Without that, Escape over the
        /// book would close the licence underneath it and leave the board sitting there.
        /// </summary>
        private void UpdateEscape()
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys == null || !keys.escapeKey.wasPressedThisFrame) return;
            if (_bookOpen) { ToggleRecipeBook(); return; }
            if (Showing(_settingsPanel)) { ToggleSettings(); return; }
            // The bench is above the guide, so Escape must reach it first — a panel that
            // covers another and cannot be closed over it is a trap.
            if (Showing(_devPanel)) { _devPanel.gameObject.SetActive(false); return; }
            if (Showing(_guidePanel)) { _guidePanel.gameObject.SetActive(false); return; }
            if (Showing(_ledgerPanel)) { _ledgerPanel.gameObject.SetActive(false); return; }
            if (Showing(_idRoot)) { _idRoot.gameObject.SetActive(false); _idVisit = null; return; }
            // The market (2026-08-19): now that the title bar's close box is gone the foot
            // key is the one exit, and a fullscreen panel with one small exit and no Escape
            // is a trap. Escape walks the SAME door — the ask first if it is up (Escape on
            // a question is "go back", never "do it"), else the guarded advance, so the
            // basket warning can never be skipped past with a key.
            if (Showing(_dayEndPanel) && _dayEndStep == 1)
            {
                if (Showing(_closingAsk)) { _closingAsk.gameObject.SetActive(false); return; }
                OnDayEndAdvance();
                return;
            }
            if (_flow != null && _flow.IsOpen) _flow.CloseFlow();
        }

        private bool _flowWasOpen;

        // ── the floor ───────────────────────────────────────────────────────────

        private void OnMenuClicked() => _flow?.Open();

        /// <summary>The font on the counter, clicked: straight to the draught station. Nothing
        /// is checked here that the flow does not check itself — but a panel already open
        /// keeps the room, exactly as a seat does, because a click through an open sheet is
        /// how the bench lost its tin twice.</summary>
        private void OnTapClicked()
        {
            if (_flow != null && _flow.IsOpen) return;
            _flow?.OpenTap();
        }

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

            // Clicking a customer reads their licence (GDD 24 §5), and that is ALL it does
            // again (2026-08-11): serving is dragging the glass onto them. One click, one
            // meaning — the click-to-serve road had a drink on the counter turning the
            // licence into a second-click affair, which is how you end up serving somebody
            // you meant to read.
            ShowId(visit);
        }

        /// <summary>
        /// THE TICKET'S BOTTOM ROW: what they want, a rule, then how they want it served.
        /// Returns how wide it came out — the plate is sized to its widest line and this row
        /// is very often it.
        ///
        /// Every mark is asked for by the PREPARATION'S OWN ID, so a preparation added to the
        /// garnish pool tomorrow needs a mask in ChromeArt and nothing else here. A mark that
        /// does not exist yet comes back null and leaves a gap rather than throwing — the
        /// same contract every other mark in this game is drawn under.
        ///
        /// Placed by hand, not by a layout group: this row is centred on a plate whose width
        /// is decided in the same frame, and a layout group would settle a frame late — which
        /// on a ticket that grows one character at a time is a row that visibly lags its own
        /// balloon. (16 §0: positions here are absolute, deliberately.)
        /// </summary>
        private float LayOutOrderIcons(SeatView view, CustomerVisit visit, bool show)
        {
            var row = view.IconRow;
            if (row == null) return 0f;
            if (!show || visit == null)
            {
                if (row.gameObject.activeSelf) row.gameObject.SetActive(false);
                return 0f;
            }
            if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);

            var drink = DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
            var spec = visit.Order.Garnishes;
            int marks = 0;
            for (int i = 0; i < view.Garnish.Length; i++)
            {
                var mark = spec != null && i < spec.Count ? ChromeArt.Mark(spec[i].Id) : null;
                view.Garnish[i].sprite = mark;
                view.Garnish[i].enabled = mark != null;
                if (mark != null) marks++;
            }
            if (view.Icon != null)
            {
                view.Icon.sprite = drink;
                view.Icon.enabled = drink != null;
            }

            // 24 for the drink because that is the size DrinkIcon draws a glass at, and 16 for
            // the marks because that is the size they are authored at. Neither is scaled: a
            // pixel drawing squeezed to fit a row it does not belong in is the fault this
            // whole ticket was rebuilt to stop making.
            const float DrinkW = 24f, MarkW = 16f, RuleW = 1f, Gap = 5f, MarkGap = 3f;
            float w = drink != null ? DrinkW : 0f;
            if (marks > 0)
            {
                if (w > 0f) w += Gap + RuleW + Gap;
                w += marks * MarkW + (marks - 1) * MarkGap;
            }
            row.sizeDelta = new Vector2(w, IconRowH);

            float x = 0f;
            if (drink != null)
            {
                view.Icon.rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += DrinkW;
            }
            if (view.IconRule != null)
            {
                bool ruled = marks > 0 && drink != null;
                view.IconRule.enabled = ruled;
                if (ruled)
                {
                    view.IconRule.rectTransform.anchoredPosition = new Vector2(x + Gap, 0f);
                    x += Gap + RuleW + Gap;
                }
            }
            for (int i = 0; i < view.Garnish.Length; i++)
            {
                if (!view.Garnish[i].enabled) continue;
                view.Garnish[i].rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += MarkW + MarkGap;
            }
            return w;
        }

        /// <summary>Which seat the pointer is over, or −1. A rect test on the stool's own
        /// hit plate, so the drop lands wherever the customer is standing rather than on a
        /// guessed column — and it costs nothing to ask five stools.</summary>
        private int SeatUnderPointer(Mouse mouse)
        {
            if (mouse == null) return -1;
            var p = mouse.position.ReadValue();
            for (int i = 0; i < _seats.Count; i++)
            {
                var root = _seats[i].Root;
                if (root == null || !root.gameObject.activeInHierarchy) continue;
                if (_seats[i].Visit == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(root, p, null)) return i;
            }
            return -1;
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
            if (!visit.IdInspected) return false;  // only TAKEN orders are servable (HUD rule, 2026-08-11)
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
                    // If the line is unknown the glass stays UNDRAWN rather than borrowing
                    // a stock one — that is the same rule, held at its edge. The old
                    // `ItemArt.Glass` fallback did exactly what the rule forbids, and its
                    // art was a pre-v3 leftover deleted with the fridge; the colour set
                    // below is what a sprite-less prop is already dressed in.
                    if (dirtyDef != null)
                        img.sprite = GlassArt.For(dirtyDef, run.GlassTier(dirtyDef.Id)).Sprite;
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
                        Toast("GLASS CLEARED — SEAT IS FREE");
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
            // Placed by the author's own hand against the new counter (2026-08-19, live:
            // "Bin X -2 Y -124") - tuned in play, written down here.
            _binProp.anchoredPosition = new Vector2(-2f, -124f);
            _binImage = _binProp.gameObject.AddComponent<Image>();
            _binImage.sprite = ItemArt.Load("bin_well");
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

            // THE GLASS IS PICKED UP AGAIN (2026-08-11, the author: back to dragging
            // instead of clicking). Clicking a customer to serve them was the wrong verb for
            // the one moment in the loop that is physical: you have made a drink, and what
            // you do with a drink is carry it to somebody. The whole rect takes the press —
            // a glass is a narrow silhouette, and asking for the glass itself would be a
            // precision test nobody signed up for.
            var body = _drinkGlass.gameObject.AddComponent<Image>();
            body.color = new Color(0f, 0f, 0f, 0.004f);
            body.raycastTarget = true;
            var grab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            grab.callback.AddListener(_ =>
            {
                var run = Run;
                if (run == null || run.Phase != TycoonPhase.DayOpen) return;
                if (_flow != null && _flow.IsOpen) return;
                if (!_glassShown || _glassServing || _glassReturning || !run.DrinkReady) return;
                _glassGrabbed = true;
                _glassVel = Vector2.zero;
                Sfx.Play("click", 0.5f);
            });
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

            // THE TOP OF THE DRINK IS AN ELLIPSE (2026-08-11, the author: the glass is 3D, so
            // what is in it has to be). A vertical fillAmount cuts the interior with a straight
            // edge — right for the body, wrong for the surface, which is the one place the
            // drink shows the player it is a cylinder and not a picture of one. It goes over
            // the liquid and under the front face, so the glass's own wall still crosses it.
            var surf = NewRect("Surface", _drinkGlass);
            surf.anchorMin = surf.anchorMax = surf.pivot = new Vector2(0.5f, 0.5f);
            _drinkGlassSurface = surf.gameObject.AddComponent<Image>();
            _drinkGlassSurface.sprite = GlassArt.SurfaceDisc();
            _drinkGlassSurface.raycastTarget = false;
            _drinkGlassSurface.enabled = false;

            var art = NewRect("Art", _drinkGlass);
            Stretch(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassArt = art.gameObject.AddComponent<Image>();
            _drinkGlassArt.raycastTarget = false;
            _drinkGlassArt.preserveAspect = true;

            var hint = NewText("Hint", _drinkGlass, _body, 10, TextAnchor.UpperCenter, UITheme.Cyan[4]);
            Place(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(190, 18), new Vector2(0, 24));
            hint.text = "CLICK A CUSTOMER TO SERVE";
            hint.raycastTarget = false;

            // With the drag gone, the bin answers a CLICK: it was drag-and-drop's landing
            // pad, and the discard verb still needs a door on the counter.
            _binImage.raycastTarget = true;
            var binBtn = _binProp.gameObject.AddComponent<Button>();
            binBtn.targetGraphic = _binImage;
            binBtn.transition = Selectable.Transition.None;
            binBtn.onClick.AddListener(OnBinClicked);

            _drinkGlass.gameObject.SetActive(false);
        }

        /// <summary>
        /// Puts the drink's top face where the drink's top is, at the width the glass has
        /// there.
        ///
        /// The art is letterboxed inside its rect by preserveAspect, so the sprite's own box
        /// is worked out first: everything the level and the profile say is in SPRITE
        /// fractions, and placing them against the rect instead would float the surface off
        /// the liquid on any glass whose drawing is not exactly the rect's shape.
        /// </summary>
        private void PlaceDrinkSurface(GlassArt.Piece piece, float fraction)
        {
            if (_drinkGlassSurface == null) return;
            if (piece.Fill == null || fraction <= 0f || piece.Aspect <= 0f)
            {
                _drinkGlassSurface.enabled = false;
                return;
            }

            Vector2 rect = _drinkGlass.rect.size;
            float drawnH = Mathf.Min(rect.y, rect.x / piece.Aspect);
            float drawnW = drawnH * piece.Aspect;

            float level = piece.FillAmount(fraction);          // 0..1 up the sprite
            float width = piece.InteriorWidthAt(level) * drawnW;
            if (width <= 1f) { _drinkGlassSurface.enabled = false; return; }

            var rt = _drinkGlassSurface.rectTransform;
            rt.sizeDelta = new Vector2(width, width * GlassArt.SurfaceSquash);
            rt.anchoredPosition = new Vector2(0f, (level - 0.5f) * drawnH);
            // A shade lighter than the body: the top face catches the room, and without the
            // lift it reads as a hole in the drink rather than the top of it.
            var body = DrinkColor();
            _drinkGlassSurface.color = new Color(
                Mathf.Lerp(body.r, 1f, 0.24f), Mathf.Lerp(body.g, 1f, 0.24f),
                Mathf.Lerp(body.b, 1f, 0.24f), body.a);
            _drinkGlassSurface.enabled = true;
        }

        /// <summary>The bin's click: throws the ready drink away, fee and all. Inert with
        /// nothing to throw — an empty counter never nags.</summary>
        private void OnBinClicked()
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;
            if (!_glassShown || _glassServing || _glassReturning || !run.DrinkReady) return;
            int fee = run.DiscardGlass();
            Toast(fee > 0 ? $"BINNED · -${fee}" : "BINNED");
            if (fee > 0)
                LogService($"<color=#F27D8A>BINNED</color> a built drink · -${fee}");
            _drinkGlass.gameObject.SetActive(false);
            _glassShown = false;
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
                if (_glassShown)
                {
                    _drinkGlass.gameObject.SetActive(false);
                    _glassShown = false;
                    _glassServing = false; _glassReturning = false; _glassServeSeat = -1;
                _glassGrabbed = false;
                }
                return;
            }

            if (!_glassShown)
            {
                _glassShown = true;
                _drinkGlass.gameObject.SetActive(true);
                _drinkGlass.anchoredPosition = _glassHome;
                _glassAngle = 0f;
                _glassServing = false; _glassReturning = false; _glassServeSeat = -1;
                _glassGrabbed = false;
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
            PlaceDrinkSurface(piece, (float)run.ServingGlass.FillFraction);
            // The finishing touches ride the carried glass too (P14): the customer is handed
            // the drink that was actually finished, salt and wedge and all.
            GlassDecor.Sync(_drinkGlass, piece, run.ServingGlass, run);

            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            var mouse = Mouse.current;

            // THE CARRY (2026-08-11). While it is held the glass springs after the cursor,
            // stiff and slightly under-damped, and leans into whichever way it is travelling
            // — the weight is the whole reason this is a drag and not a click. Letting go
            // over a customer hands it to them; letting go anywhere else sends it home,
            // which is the same slide the refusal already used.
            if (_glassGrabbed)
            {
                if (mouse == null || !mouse.leftButton.isPressed)
                {
                    _glassGrabbed = false;
                    int seat = SeatUnderPointer(mouse);
                    bool served = false, saidWhy = false;
                    if (seat >= 0)
                    {
                        try { served = ServeSeat(seat); }
                        catch (InvalidOperationException e3)
                        { Toast(e3.Message.ToUpperInvariant()); saidWhy = true; }
                    }
                    if (served)
                    {
                        _drinkGlass.gameObject.SetActive(false);
                        _glassShown = false;
                        return;
                    }
                    if (seat >= 0 && !saidWhy) Toast("READ THEIR ID FIRST");
                    // Home it goes, along the counter, by the road it already knows.
                    _glassServeFrom = _glassHome;
                    _glassServeTo = _drinkGlass.anchoredPosition;
                    _glassServeDur = Mathf.Min(GlassSlideMax,
                        0.08f + (_glassServeTo - _glassServeFrom).magnitude / 4200f);
                    _glassServeT = 0f;
                    _glassReturning = true;
                    return;
                }

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)_drinkGlass.parent, mouse.position.ReadValue(), null,
                        out Vector2 want))
                {
                    var before = _drinkGlass.anchoredPosition;
                    _glassVel += (want - before) * (GlassCarryStiffness * dt);
                    _glassVel *= Mathf.Exp(-GlassCarryDamping * dt);
                    _drinkGlass.anchoredPosition = before + _glassVel * dt;
                    float carry = Mathf.Clamp(-_glassVel.x * 0.012f, -16f, 16f);
                    _glassAngle = Mathf.Lerp(_glassAngle, carry, 1f - Mathf.Exp(-18f * dt));
                    _drinkGlass.localRotation = Quaternion.Euler(0, 0, _glassAngle);
                }
                return;
            }

            // THE SLIDE (2026-08-11): the glass travels the counter on its own timer; the
            // serve fires on arrival, after a re-validation, because the seat can empty
            // and the patience can run out while the glass is in flight.
            if (_glassServing || _glassReturning)
            {
                _glassServeT += dt;
                float k = _glassServeDur <= 0f ? 1f : Mathf.Clamp01(_glassServeT / _glassServeDur);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);   // lands soft
                var from = _glassServing ? _glassServeFrom : _glassServeTo;
                var to = _glassServing ? _glassServeTo : _glassServeFrom;
                var before = _drinkGlass.anchoredPosition;
                _drinkGlass.anchoredPosition = Vector2.Lerp(from, to, e);
                // lean into the travel, upright at both ends
                float lean = Mathf.Clamp((_drinkGlass.anchoredPosition.x - before.x) / dt * -0.012f, -18f, 18f);
                _glassAngle = Mathf.Lerp(_glassAngle, lean * Mathf.Sin(k * Mathf.PI), 0.5f);
                _drinkGlass.localRotation = Quaternion.Euler(0, 0, _glassAngle);
                if (k < 1f) return;

                if (_glassReturning)
                {
                    _glassReturning = false;
                    _drinkGlass.anchoredPosition = _glassHome;
                    _drinkGlass.localRotation = Quaternion.identity;
                    return;
                }
                _glassServing = false;
                int seat = _glassServeSeat;
                _glassServeSeat = -1;
                bool served = false, saidWhy = false;
                try { served = seat >= 0 && ServeSeat(seat); }
                catch (InvalidOperationException e2)
                { Toast(e2.Message.ToUpperInvariant()); saidWhy = true; }
                if (served)
                {
                    _drinkGlass.gameObject.SetActive(false);   // handed over; a new drink re-shows it
                    _glassShown = false;
                }
                else
                {
                    // Refused at the stool: the drink comes back. The player keeps it.
                    if (!saidWhy) Toast("THEY LEFT — YOU GET THE DRINK BACK");
                    _glassServeT = 0f;
                    _glassReturning = true;
                }
                return;
            }

            // At rest: home, upright. The bin lifts its lid — brightens — under a hover
            // while there is something to throw, so it never nags at an empty counter.
            _drinkGlass.anchoredPosition = _glassHome;
            _drinkGlass.localRotation = Quaternion.identity;
            if (_binImage != null)
                _binImage.color = IsOverBin(mouse) ? Color.white : new Color(0.72f, 0.72f, 0.74f, 1f);
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
            string line = verdict.OrdersAgain ? "ANOTHER ROUND!"
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
        /// <summary>
        /// What a customer paid, rising off their stool.
        ///
        /// The author, 2026-08-11: the figures are hard to read and there are black and
        /// white frames around them. Both were one bug wearing two faces. The star line was
        /// built out of U+2605 and U+2606 — and PressStart2P carries neither, so Unity drew
        /// the missing-glyph box five times over. The "frames" were tofu. This project's own
        /// notes already record the trap ("Silkscreen cannot draw U+2605, and the label
        /// carries one"), which is exactly how it got in.
        ///
        /// So the stars are DRAWN now, with the mark the slip and the ticket use, at a size
        /// chosen rather than inherited from a font that could not render them. And the
        /// money was set at 14, which is not a whole multiple of the face's 8px design size
        /// — the one sizing rule this project has — so it was being resampled between the
        /// pixel grid, which is the rest of the softness. 16 is the legal size next to it.
        /// </summary>
        private System.Collections.IEnumerator TabFloat(int seatIndex, CustomerVisit visit)
        {
            var seat = _seats[seatIndex].Root;
            int tip = visit.Paid - visit.PaidBase;

            var host = NewRect("Tab", seat.parent);
            host.anchorMin = host.anchorMax = host.pivot = new Vector2(0, 0);
            host.sizeDelta = new Vector2(178, 46);
            var group = host.gameObject.AddComponent<CanvasGroup>();

            var paid = NewText("Paid", host, _display, 16, TextAnchor.LowerCenter,
                UITheme.Amber[4]);
            Place(paid.rectTransform, new Vector2(0.5f, 1), new Vector2(178, 20), new Vector2(0, 0));
            paid.rectTransform.pivot = new Vector2(0.5f, 1);
            paid.horizontalOverflow = HorizontalWrapMode.Overflow;
            paid.text = "+$" + visit.Paid;

            // The tip is the part worth its own colour, and it is short enough to sit under
            // the total without a word explaining itself.
            if (tip > 0)
            {
                var tipText = NewText("Tip", host, _display, 8, TextAnchor.UpperCenter,
                    UITheme.Lime[3]);
                Place(tipText.rectTransform, new Vector2(0.5f, 1), new Vector2(178, 10),
                    new Vector2(0, -20f));
                tipText.rectTransform.pivot = new Vector2(0.5f, 1);
                tipText.horizontalOverflow = HorizontalWrapMode.Overflow;
                tipText.text = "+$" + tip + " TIP";
            }

            // Five small drawn stars, lit to the visit's own score.
            const float StarPx = 10f, StarGap = 2f;
            int lit = Mathf.Clamp(Mathf.RoundToInt((float)visit.Satisfaction * 5f), 0, 5);
            float rowW = 5f * StarPx + 4f * StarGap;
            var stars = NewRect("Stars", host);
            Place(stars, new Vector2(0.5f, 1), new Vector2(rowW, StarPx),
                new Vector2(0, tip > 0 ? -32f : -22f));
            stars.pivot = new Vector2(0.5f, 1);
            var mark = ChromeArt.Mark("star");
            for (int i = 0; i < 5; i++)
            {
                var one = NewRect("S" + i, stars);
                Place(one, new Vector2(0, 0.5f), new Vector2(StarPx, StarPx),
                    new Vector2(i * (StarPx + StarGap) + StarPx * 0.5f, 0));
                one.pivot = new Vector2(0.5f, 0.5f);
                var img = one.gameObject.AddComponent<Image>();
                img.sprite = mark; img.preserveAspect = true; img.raycastTarget = false;
                img.color = i < lit ? UITheme.Amber[3] : new Color(1f, 1f, 1f, 0.22f);
            }

            var start = seat.anchoredPosition + new Vector2(-89f, 96f);
            const float duration = 1.6f;
            float tt = 0f;
            while (tt < duration && host != null)
            {
                tt += Time.deltaTime;
                float k = Mathf.Clamp01(tt / duration);
                host.anchoredPosition = start + new Vector2(0, 64f * k);
                group.alpha = 1f - k * k;
                yield return null;
            }
            if (host != null) Destroy(host.gameObject);
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

        // The dressing is synced by COUNT, the cheapest honest change signal Core
        // offers: a buy and a refund both move it, and the stage rebuild is seven
        // sprites at most. -1 forces the first sync of a run, so a fresh bar starts
        // bare even when the last one was furnished.
        private int _lastFixtureCount = -1;

        /// <summary>
        /// The open tab's title comes UP to white rather than being swapped to it (the
        /// author, 2026-08-10). One frame of green and the next of white reads as a redraw;
        /// a fifth of a second of travel reads as the tab lighting up. The icons ride the
        /// same curve, so the whole key brightens as one object.
        /// </summary>
        private void FadeShopTabs()
        {
            if (_shopTabLabels == null) return;
            float step = Time.unscaledDeltaTime / TabFade;
            for (int i = 0; i < _shopTabLabels.Length; i++)
            {
                if (_shopTabLabels[i] == null) continue;
                bool on = i == _shopTab;
                var want = on ? Color.white : ShopViceDeep;
                _shopTabLabels[i].color = Color.Lerp(_shopTabLabels[i].color, want, step);
                if (_shopTabIcons[i] != null)
                    _shopTabIcons[i].color = Color.Lerp(_shopTabIcons[i].color,
                        on ? Color.white : new Color(0.494f, 0.529f, 0.635f, 1f), step);
            }
        }

        // ── the till that counts (2026-08-10) ───────────────────────────────────
        // Buying used to be a number swapping for another number, which is not something
        // you can watch happen. The till RUNS DOWN to what you spent — fast enough not to
        // be a wait, slow enough that the money leaving is an event with a direction — and
        // both readouts, the diegetic one on the register and the market's account line,
        // read from the same running figure so they can never disagree mid-count.

        private float _tillShown = float.NaN;

        // ── what a line cost, said out loud (2026-08-11) ────────────────────────
        //
        // The author: each amount that leaves or arrives should show under the money, one
        // by one, coloured and signed. The till already ROLLS to its new number, which
        // says that something happened and never what — buy two lines at $34 and $20 and
        // the counter slides $54 in one movement, and the player is left doing the
        // arithmetic backwards to find out what they just agreed to.
        //
        // So every purchase drops its own figure out of the account box: red and negative
        // for money leaving, green and positive for money arriving, staggered so two lines
        // read as two events rather than one clump.
        private RectTransform _tillFloats;
        private readonly List<(RectTransform Rt, Text Label, float Born)> _moneyDrops =
            new List<(RectTransform, Text, float)>();
        private const float DropLife = 1.5f, DropRise = 26f, DropStagger = 0.22f;

        /// <summary>One figure, falling out from under the money it changed.</summary>
        private void DropMoney(int amount, int slot)
        {
            if (_tillFloats == null || amount == 0 || Motion.Reduced) return;
            var rt = NewRect("Drop", _tillFloats);
            Place(rt, new Vector2(1, 0), new Vector2(126, 16), new Vector2(-10, -14f));
            rt.pivot = new Vector2(1, 1);
            var t = NewText("L", rt, _display, 8, TextAnchor.MiddleRight,
                amount >= 0 ? ShopViceLit : ShopCost);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = (amount >= 0 ? "+$" : "-$") + Mathf.Abs(amount);
            _moneyDrops.Add((rt, t, Time.unscaledTime + slot * DropStagger));
        }

        private void StepMoneyDrops()
        {
            for (int i = _moneyDrops.Count - 1; i >= 0; i--)
            {
                var (rt, label, born) = _moneyDrops[i];
                if (rt == null) { _moneyDrops.RemoveAt(i); continue; }
                float t = Time.unscaledTime - born;
                if (t < 0f) { label.color = Clear(label.color); continue; }
                float k = t / DropLife;
                if (k >= 1f) { Destroy(rt.gameObject); _moneyDrops.RemoveAt(i); continue; }
                // Out fast, then drifting down and away — a figure that is read and gone.
                rt.anchoredPosition = new Vector2(-10f, -14f - DropRise * (1f - (1f - k) * (1f - k)));
                label.color = Opaque(label.color);
                var c = label.color;
                label.color = new Color(c.r, c.g, c.b,
                    k < 0.15f ? k / 0.15f : 1f - Mathf.Clamp01((k - 0.55f) / 0.45f));
            }
        }

        private int _tillLast = int.MinValue;

        private void RunTheTill(TycoonRun run)
        {
            float want = run.Money;
            if (float.IsNaN(_tillShown) || Motion.Reduced) { _tillShown = want; }
            else if (!Mathf.Approximately(_tillShown, want))
            {
                // Proportional, with a floor: a $2 refill still moves, a $200 fitting does
                // not take ten times as long as a $20 one.
                float speed = Mathf.Max(28f, Mathf.Abs(want - _tillShown) * 4.5f);
                _tillShown = Mathf.MoveTowards(_tillShown, want, speed * Time.unscaledDeltaTime);
            }
            // The CHANGE is announced off the real figure, not off the animated one: the
            // counter takes its time getting there, and a float per counted step would be a
            // stream of ones.
            if (_tillLast != int.MinValue && run.Money != _tillLast)
            {
                var st = _stage != null ? _stage : FindFirstObjectByType<DiegeticStage>();
                _stage = st;
                if (st != null) st.FloatMoney(run.Money - _tillLast);
            }
            _tillLast = run.Money;

            int shown = Mathf.RoundToInt(_tillShown);
            if (_tabletTill != null) _tabletTill.text = "$" + shown;
            if (stage != null) stage.SetMoney("$" + shown);
        }

        private void WatchFixtures()
        {
            var run = Run;
            if (run == null) return;
            var stage = _stage != null ? _stage : FindFirstObjectByType<DiegeticStage>();
            _stage = stage;
            if (stage == null) return;
            if (run.OwnedFixtureCount == _lastFixtureCount) return;
            _lastFixtureCount = run.OwnedFixtureCount;
            // ONE TOWER, NOT THE WHOLE LADDER (2026-08-19). A bar that upgraded still OWNS
            // the single — it was fitted over, not sold back — and every level stands in the
            // same slot, so handing the room all of them draws three towers one inside the
            // other. The run says which one is standing; everything else the bar owns is
            // dressing and goes in as it always did.
            var standing = run.StandingTap();
            var owned = new List<FixtureDefinition>();
            foreach (var f in run.FixtureCatalogue)
            {
                if (!run.OwnsFixture(f.Id)) continue;
                if (f.IsTap && !ReferenceEquals(f, standing)) continue;
                owned.Add(f);
            }
            // The room is handed its hooks before anything is stood in them. Cheap enough
            // to repeat: seven entries into a dictionary, only on the frames the dressing
            // actually changed.
            stage.SetSlots(_bootstrap != null ? _bootstrap.StageSlots : null);
            stage.SyncFixtures(owned);
            RefreshCellar(run);
        }

        /// <summary>
        /// Stands the bar's own stock in the counter's cellar (2026-08-22). The SAME rule the
        /// back-bar wall keeps: garnish is not stock you pour from and beer comes off the
        /// font on the counter, so neither stands here. The stage is TOLD the pictures and
        /// never reads the run, which is why this lives on the HUD side of the line.
        /// </summary>
        private void RefreshCellar(TycoonRun run)
        {
            if (stage == null) return;
            var art = new List<Sprite>(DiegeticStage.CellarSlots);
            if (run != null)
                foreach (var b in run.Shelf.Bottles)
                {
                    var card = b.Ingredient;
                    if (card == null) continue;
                    if (card.Type == IngredientType.Garnish || card.Type == IngredientType.Beer)
                        continue;
                    var sprite = ItemArt.Bottle(card);
                    if (sprite != null) art.Add(sprite);
                    if (art.Count >= DiegeticStage.CellarSlots) break;
                }
            stage.SetCellar(art);
        }

        /// <summary>A fixture's sprite, from its own Resources shelf (PPU 1 — world art).</summary>
        private static Sprite FixtureArt(string name) =>
            string.IsNullOrEmpty(name) ? null : Resources.Load<Sprite>("Fixtures/" + name);

        // "Ekrandaki bardaklari simdilik kaldir, gerek yok" (2026-08-19): the rack is
        // parked, not demolished - flip this back on when the new counter art gets its
        // compartments and the rack has somewhere honest to stand.
        private const bool GlassRackShown = false;

        private void WatchGlassRack()
        {
            if (!GlassRackShown) { if (_glassRack != null) _glassRack.gameObject.SetActive(false); return; }
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
                UiAuditExempt.Mark(_glassRack,
                    "glasses on a shelf, sized and dimmed by which row they stand in — " +
                    "perspective, not chrome; rounding them to whole units moves the shelf");
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
                    // A CONTACT SHADOW UNDER EACH ONE. They are standing IN a shelf now,
                    // not on a lit counter, and nothing sells that like the dark pooling
                    // where the glass meets the wood. Laid before the glass so it reads as
                    // underneath it, and narrower than the foot so it stays a contact
                    // rather than a halo.
                    var foot = NewRect($"S_{g.Id}_{k}", _glassRack);
                    foot.anchorMin = foot.anchorMax = new Vector2(0.5f, 0);
                    foot.pivot = new Vector2(0.5f, 0.5f);
                    foot.sizeDelta = new Vector2(kh * piece.Aspect * 0.86f, 7f);
                    foot.anchoredPosition = new Vector2(x + dx,
                        floorY + (back ? rise : 0f) + 2f);
                    var footImg = foot.gameObject.AddComponent<Image>();
                    footImg.sprite = BackBarArt.BottleShadow();
                    footImg.raycastTarget = false;
                    footImg.color = new Color(0f, 0f, 0f, back ? 0.42f : 0.62f);
                    foot.SetSiblingIndex(rt.GetSiblingIndex());

                    var img = rt.gameObject.AddComponent<Image>();
                    img.sprite = piece.Sprite;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    // AND THE GLASS ITSELF IS IN SHADOW. A bay is a hole in the bar front:
                    // the light that reaches it comes from in front and above and falls off
                    // fast, so even the near row sits well under full brightness and the far
                    // row further still. Drawing them at 1.0 lit them as if they were on the
                    // counter, which is the one place they are not.
                    float lit = (back ? 0.58f : 0.78f);
                    img.color = new Color(lit * 0.96f, lit * 1.0f, lit * 1.08f, 1f);
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
            // The sky outside runs on this same clock (2026-08-19): the window holds an
            // evening's worth of frames — a low sun at 18:00 through the pink band to a lit
            // city by 02:00 — and the shift's fraction is simply which frame is up. Driven
            // from here, beside the hour it belongs to, so the plaque and the glass can never
            // disagree about what time it is.
            if (stage != null) stage.SetSkyFraction((float)run.Floor.NightFraction);
            // The plaque's rule is the state light: cyan through the shift, magenta once the
            // room is being called — visible from across the screen without reading a word.
            bool last = run.Floor.IsClosingTime;
            // The colon keeps the second, which is the one thing on this board that moves on
            // its own. A display whose colon is painted on is a picture of a clock.
            if (_clock != null)
            {
                if (last != _clockWasLast)
                {
                    _clockWasLast = last;
                    _clock.SetHue(last ? UITheme.Magenta[4] : UITheme.Cyan[4]);
                    // The tube under the whole beam goes with it. This is the state light
                    // now: a 2px rule under one plaque was never going to be seen, and the
                    // board itself changing colour is read before anything is read.
                    if (_neonTube != null)
                        _neonTube.color = last ? UITheme.Magenta[4] : UITheme.Amber[4];
                    if (_neonBloom != null)
                    {
                        var b = last ? UITheme.Magenta[2] : UITheme.Amber[2];
                        _neonBloom.color = new Color(b.r, b.g, b.b, last ? 0.42f : 0.30f);
                    }
                }
                _clock.Show(hh, mm / 5 * 5, ((int)(Time.unscaledTime * 2f) & 1) == 0);
            }
            // The night names itself on the marquee — tonight's bulb is lit and its letters
            // are amber — so nothing up here prints the day in words as well. Printing it
            // twice across one board is what made the old one read as assembled.
            RefreshWeekStrip(run);

            // DEBT IS SHOWN ON THE MACHINE THAT HOLDS THE MONEY (2026-08-14). The fascia's
            // copy of the till is gone, so the register's own window goes red instead — and
            // the line that used to colour the fascia's number went with it. It was left
            // behind for one build and threw every frame, which took the standing and the
            // crowd down with it: everything after a NullReference in Update simply does not
            // run, and the plaque above went quietly blank.
            var tillStage = _stage != null ? _stage : FindFirstObjectByType<DiegeticStage>();
            _stage = tillStage;
            if (tillStage != null) tillStage.SetMoneyInDebt(run.Money < 0);

            // The caption line over the standing carries the crowd — and gives way to LAST
            // CALL when the room is being called, because at that point what is in front of
            // the bar matters more than who it is.
            _crowdText.text = last ? "LAST CALL"
                : run.CrowdToday == WealthTier.HighRoller ? "TONIGHT · HIGH ROLLERS"
                : run.CrowdToday == WealthTier.Broke ? "TONIGHT · BROKE CROWD" : "TONIGHT · REGULARS";
            _crowdText.color = last ? UITheme.Magenta[4]
                : run.CrowdToday == WealthTier.HighRoller ? UITheme.Magenta[4]
                : run.CrowdToday == WealthTier.Broke ? UITheme.ViceRed[3] : UITheme.Cream[3];

            // The standing, as a row of stars and NOTHING ELSE (2026-08-19, the author:
            // "0.0 neden gösteriliyor, daha çok görsel bir şerit olmalı"). The number that
            // read beside them is gone: the fill IS the reading. A half-lit star is a real
            // half — the average is continuous, and the mask's width carries it exactly,
            // so nothing legible was lost; the decimal lives on in the ledger and the shop,
            // where a number is being compared to another number.
            double stars = run.Rating.Average;
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
                    // THE STORY'S GUEST LEAVES A LINE, NOT A SCENE (GDD 26 §3). Their clock
                    // running out is a beat that did not land, not a customer storming out of
                    // a bad bar — they walk, they do not slam, and the night's log does not
                    // book them as a walk-out because they were never on its books at all.
                    v.ExitStorm = !v.Visit.OnTheHouse && v.Visit.State == VisitState.StormedOff;
                    if (v.Visit.OnTheHouse) { }
                    else if (v.ExitStorm)
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
            // 1b) THE GUEST WEARS THE FACE THE BEAT NAMES, whatever order the frame ran in.
            //
            // Measured, 2026-08-13: the story's guest kept turning up in a stranger's body
            // while the plate showed the right person — the stool had been given a rolled
            // look, and a stool KEEPS its look by design (a face that changes under the
            // player is worse than a wrong one). Rather than chase which frame won the race,
            // the written face is simply reasserted here, once a frame, idempotently: for
            // this one visit the beat is the authority, not the seat.
            var houseGuest = run.LastCustomer;
            if (houseGuest != null)
            {
                var written = LookForStory(run.LastCallBeat?.Who);
                if (written != null)
                    foreach (var v in _seats)
                        if (v.Visit == houseGuest && v.Look != written)
                        {
                            v.Look = written;
                            v.Tag.anchoredPosition = new Vector2(0, written.HeadTop + TagLift);
                            if (v.Gauge != null)
                                v.Gauge.anchoredPosition = new Vector2(0, written.HeadTop + 6f);
                        }
            }

            // 2) Arrivals — a seated customer with no stool takes the first free one and walks in.
            foreach (var visit in seated)
            {
                bool assigned = false;
                for (int i = 0; i < _seats.Count; i++) if (_seats[i].Visit == visit) { assigned = true; break; }
                if (assigned) continue;
                // THE GUEST SITS WHERE THEY CAN BE TALKED TO (GDD 26 §3): the stool nearest
                // the till, which is the end of the row the bar is worked from. Everyone else
                // takes the first free stool, as they always have.
                bool nearTheTill = visit.OnTheHouse;
                int from = nearTheTill ? Math.Min(run.Seats, _seats.Count) - 1 : 0;
                int step = nearTheTill ? -1 : 1;
                for (int n = 0, i = from; n < run.Seats && i >= 0 && i < _seats.Count; n++, i += step)
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
                            v.Tag.anchoredPosition = new Vector2(0, v.Look.HeadTop + TagLift);
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

                if (view.Exiting)
                {
                    // The ticket comes down the moment they get up (2026-08-19, the author:
                    // "içtikten sonra baloncuk kalkabilir") — a leaving customer is done
                    // talking, and a balloon walking out with them reads as unfinished
                    // business. The patience bar goes with it (2026-08-20): their clock
                    // stopped when they left the stool, and a gauge crossing the room is a
                    // countdown on somebody who is no longer waiting for anything.
                    if (view.Tag.gameObject.activeSelf) view.Tag.gameObject.SetActive(false);
                    if (view.Gauge != null && view.Gauge.gameObject.activeSelf)
                        view.Gauge.gameObject.SetActive(false);
                    AdvanceExit(view);
                    continue;
                }

                if (view.Visit == null)
                {
                    if (view.Root.gameObject.activeSelf) view.Root.gameObject.SetActive(false);
                    SyncPatronBody(view);
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
                // THE BUBBLE IS UP THE WHOLE TIME THEY ARE ON THE STOOL (2026-08-19). It used
                // to be hidden while they read the menu, so a customer deciding and a customer
                // who had not arrived yet looked exactly alike — the player had nothing to
                // wait ON. It says "..." instead, which is a customer visibly thinking.
                bool showBubble = atTheStool;
                if (view.Tag.gameObject.activeSelf != showBubble)
                    view.Tag.gameObject.SetActive(showBubble);

                if (showBubble)
                {
                    // A regular ordering again after a perfect serve gets a gold star and the
                    // round count (GDD 24 §4) — the reward for reading them right, made
                    // visible. The name is part of what the card teaches: it waits for the read.
                    // "x3", not a star from the font: no pixel face here carries one, so it
                    // arrived as a fallback glyph at the wrong weight beside a name set in ours.
                    string star = visit.ExtraOrdersTaken > 0
                        ? $"<color=#8F5A1E>x{visit.ExtraOrdersTaken + 1} </color>" : "";
                    view.Name.supportRichText = true;
                    // The name off their PAPERS, which is the name their licence prints —
                    // see NameOn. The ticket is where the card is remembered once it is shut.
                    view.Name.text = known && !deciding
                        ? star + NameOn(visit, view.Look).ToUpperInvariant() : "";

                    // THE ORDER ARRIVES AS SPEECH (2026-08-19, the author: "yazılar konuşma
                    // metni gibi harf harf gelecek"). The clock starts on the EDGE of the
                    // licence being read, not on the frame it is read in, so the ticket cannot
                    // restart its sentence every time the pointer moves. Reduced motion is
                    // handed the whole line at once — a typewriter is exactly the sort of
                    // thing that setting exists to switch off.
                    if (known && !view.WasKnown)
                    {
                        view.WasKnown = true;
                        view.SpeakFrom = Time.unscaledTime;
                    }
                    string wanted = known ? visit.Order.Wanted.Name.ToUpperInvariant() : "";
                    int said = Motion.Reduced ? wanted.Length
                        : Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - view.SpeakFrom) * SpeakCps),
                                      0, wanted.Length);
                    view.Spoken = said >= wanted.Length;

                    if (drinking)
                    {
                        // Served, mid-animation, off-limits: the ticket turns into a loading
                        // sign (2026-08-19, the author: "bir nevi yüklenme işareti") — the
                        // thinking beat's own dots, in the club's blue to match the plate's
                        // edge. The order line is gone: the drink is in their hand now.
                        view.Wants.text = "DRINKING" + (Motion.Reduced ? "..."
                            : new string('.', 1 + Mathf.FloorToInt(Time.unscaledTime / DotBeat) % 3));
                        view.Wants.color = UITheme.ClubBlue[1];
                        view.Order.text = "";
                        view.Spoken = true;
                    }
                    else if (deciding)
                    {
                        // Reading the menu. One, two, three dots and round again — the beat is
                        // the only thing on the ticket, so the ticket is the size of it.
                        view.Wants.text = Motion.Reduced ? "..."
                            : new string('.', 1 + Mathf.FloorToInt(Time.unscaledTime / DotBeat) % 3);
                        view.Wants.color = UITheme.Magenta[1];
                        view.Order.text = "";
                        view.Spoken = false;
                    }
                    else if (!known)
                    {
                        // Ready, unread: the one line the author asked for, and nothing else.
                        view.Wants.text = "READY TO ORDER";
                        view.Wants.color = UITheme.Magenta[1];
                        view.Order.text = "";
                    }
                    else if (visit.OnTheHouse)
                    {
                        // THE STORY'S GUEST NAMES ONE DRINK AT A TIME, and the post-it is
                        // where it is named (GDD 26 §4). Their licence is open from the
                        // moment they sit — they introduced themselves — so the ticket would
                        // otherwise print the ask over their head and hand the player the
                        // whole trial in advance, which is the one thing the reveal is for.
                        view.Wants.text = "TALK TO THEM";
                        view.Wants.color = UITheme.Magenta[1];
                        view.Order.text = "";
                        view.Spoken = false;
                    }
                    else
                    {
                        // Read: the name above, the order below — the card said the rest.
                        view.Wants.text = "";
                        view.Order.text = wanted.Substring(0, said);
                    }

                    // THE ICON ROW comes up only once the order has finished being SAID. The
                    // pictures are the fastest thing on the ticket to read, so showing them
                    // while the letters are still arriving would answer the question before
                    // the sentence asks it — and the typing would be decoration.
                    float iconW = LayOutOrderIcons(view, visit,
                        known && view.Spoken && !deciding && !drinking);

                    // The ticket FITS its lines and its WIDEST line (the author, 2026-08-02:
                    // "yazı hiçbir zaman taşmamalı"). SEX ON THE BEACH ran off both ends of
                    // a fixed card. The card takes the width of the longest thing it says,
                    // up to a cap; past the cap the order wraps to a second row and the card
                    // grows downward instead. Nothing is ever clipped, and nothing floats in
                    // an empty box.
                    //
                    // BOTH AXES ANSWER THE CONTENT NOW (2026-08-19). The height used to be
                    // the rows of TYPE only, so the icon row hung off the bottom of the plate;
                    // and the width had a 156 floor, which drew a poster round three dots.
                    float widest = Mathf.Max(view.Name.preferredWidth,
                        Mathf.Max(view.Wants.preferredWidth,
                            Mathf.Max(view.Order.preferredWidth, iconW)));
                    float cardW = Mathf.Clamp(widest + TagPad * 2f, TagMinW, TagMaxW);
                    float textW = cardW - TagPad * 2f;

                    // The order is the line that runs long, so it is the one allowed to wrap.
                    int orderLines = view.Order.text.Length == 0 ? 0
                        : Mathf.Max(1, Mathf.CeilToInt(view.Order.preferredWidth / Mathf.Max(1f, textW)));
                    view.Order.horizontalOverflow = orderLines > 1
                        ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;

                    // EACH ROW IS AS TALL AS ITS OWN FONT SAYS (2026-08-19). One constant used
                    // to stand in for three different line boxes, so the plate was always a
                    // few units taller than what it held and the type rode high in it.
                    float rowTop = -TagPad;
                    view.Name.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Name.text.Length > 0) rowTop -= view.NameLineH;
                    view.Wants.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Wants.text.Length > 0) rowTop -= view.WantsLineH;
                    view.Order.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    rowTop -= view.OrderLineH * orderLines;
                    if (view.IconRow != null && view.IconRow.gameObject.activeSelf)
                    {
                        view.IconRow.anchoredPosition = new Vector2(0, rowTop);
                        rowTop -= IconRowH;
                    }
                    // The bottom pays the foot row as well as the padding, so what the type is
                    // centred in is the WHITE FIELD and not the sprite: the plate's top edge
                    // is two units of colour and its bottom is two plus the foot's one.
                    view.Tag.sizeDelta = new Vector2(cardW, -rowTop + TagPad + TagFoot);
                }

                // (The drink icon used to dock against the order text's measured width, on
                // whatever row the order landed on. It lives on its own row now, beside the
                // serving spec, and LayOutOrderIcons places the whole row.)

                // TWO clocks now (the author, 2026-08-02): the wait to be ASKED, then a fresh
                // wait for the drink. The gauge draws whichever is live — Core says which —
                // and the asking wait draws in magenta so a bar that is emptying is visibly
                // a different failure from a bar that is slow.
                bool beingIgnored = visit.AwaitingOrderTaking;
                float patience = (deciding || drinking) ? 1f : (float)visit.PatienceFraction;
                float gaugeW = BustW * 0.72f - 2f;
                // THE BAR IS ONLY UP WHILE IT IS EMPTYING (2026-08-20, the author: "herhangi
                // bir sabır barı azalmıyorken kafasının üstünde bar gözükmesin ... içki
                // içerken odadan çıkarken vs"). It used to stand over every seated customer
                // and simply hold FULL through the beats where no clock runs — thinking,
                // drinking, walking out — which is a gauge that means nothing three times a
                // visit, and a room of them says the night is under pressure when it is not.
                //
                // The condition is Core's own, not a list of screens: patience ticks in
                // CustomerVisit.Tick only while the visit is WAITING, is not held, and has
                // finished deciding. Anything else — a mind being made up, a drink being
                // nursed, a guest who is being talked to (GDD 26 §4 keeps their clock on the
                // POST-IT anyway), somebody already off the stool — has no clock to draw.
                bool clockRunning = !visit.OnTheHouse && !visit.ClockHeld
                    && !deciding && !drinking && visit.State == VisitState.Waiting;
                if (view.Gauge != null && view.Gauge.gameObject.activeSelf != clockRunning)
                    view.Gauge.gameObject.SetActive(clockRunning);
                view.PatienceFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(gaugeW * patience), -2);
                view.PatienceFill.color = beingIgnored
                        ? (patience > 0.35f ? UITheme.Magenta[3] : UITheme.ViceRed[3])
                    : patience > 0.5f ? UITheme.Lime[3]
                    : patience > 0.25f ? UITheme.Amber[3] : UITheme.ViceRed[3];

                // Drive the animated customer (2026-07-23): walk-in, the sit-and-breathe idle,
                // a one-shot "placing the order" beat, then nursing the drink. Facing and frame
                // are chosen from the visit state; the body below the waist is clipped by the bar.
                UpdateSeatAnimation(view, visit);

                // The tag lights when a drink is built and this customer can actually take
                // it — and "can take" includes the READ: only taken orders are click-servable,
                // so the lit set and the clickable set are one set.
                //
                // It used to be a TINT over a flat rectangle, and a tint is no use to a
                // drawing: multiplying a white plate by cyan drags the magenta edge to a
                // muddy teal and the whole balloon changes colour to say one thing. The lit
                // ticket is a second SPRITE instead — the same 11x11 geometry with its edge
                // walked onto the information ramp, so only the edge moves and the plate is
                // still recognisably the same object (16 §5: light says state).
                bool canTake = drinkReady && !deciding && !drinking && visit.IdInspected;
                // A THIRD tone joined the two (2026-08-19, the author: "içecek içiyorsa pembe
                // rengi vice mavisi olsun"): while they drink, the edge walks onto the club's
                // blue — the customer is mid-animation and cannot be interacted with, and the
                // plate says so the same way the dots on it do.
                var tone = drinking ? ChromeArt.BubbleTone.Drink
                    : canTake ? ChromeArt.BubbleTone.Take
                    : ChromeArt.BubbleTone.Order;
                view.TagBg.sprite = ChromeArt.Bubble(tone);
                if (view.Tail != null) view.Tail.sprite = ChromeArt.BubbleTail(tone);
                if (view.IconRule != null)
                    view.IconRule.color = canTake ? UITheme.Cyan[0] : UITheme.Magenta[1];
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
                // ARRIVING SLOWS DOWN (2026-08-19, the author: "karakterler koltuğuna
                // yaklaşınca yürüme hızı biraz yavaşlamalı"). An ease-out lived here once and
                // was removed for a good reason, written down at the time: the ground slid
                // fast under slow feet, because only the FLOOR was easing. So the ease is
                // back with the missing half — WalkPace scales the walk cycle by exactly the
                // same factor it scales the speed, and the feet stay on the floor at every
                // pace. Nothing about the cycle is retimed; it is simply played slower.
                float left = (1f - view.WalkT) * dist;
                view.WalkPace = Mathf.Lerp(ArrivalPace, 1f, Mathf.Clamp01(left / ArrivalEase));
                view.WalkT = Mathf.Min(1f,
                    view.WalkT + Time.deltaTime * WalkSpeed * view.WalkPace / dist);
                view.Root.anchoredPosition =
                    new Vector2(Mathf.Lerp(entryX, view.SeatX, view.WalkT), SeatLineY);
                view.Group.alpha = Mathf.Clamp01(view.WalkT * 4f);
            }
            else
            {
                view.Root.anchoredPosition = new Vector2(view.SeatX, SeatLineY);
                view.Group.alpha = 1f;
            }
        }

        /// <summary>Plays a customer leaving (2026-07-23): they get up and walk back out to
        /// the right the way they came — and since 2026-08-19 it IS the way they came (the
        /// author: "çıkış animasyonu giriş animasyonu ile aynı hızda aynı şekilde"): the
        /// entrance mirrored, same WalkSpeed, same near-stool ease, same fade. One pace for
        /// everybody — the storm-off's shake and its 1.5× hurry are gone; anger is carried by
        /// the Upset reaction beat and the toast, not by the walk.</summary>
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

            // The entrance run backwards: slow at the stool, full pace by ArrivalEase out —
            // and the cycle is scaled by the same factor as the floor, so the feet grip at
            // every step exactly as they do on the way in (see AdvanceWalkIn).
            float exitX = _hudRoot.rect.width + OffscreenMargin;
            float dist = Mathf.Max(1f, exitX - view.SeatX);
            float gone = view.ExitT * dist;
            float pace = Mathf.Lerp(ArrivalPace, 1f, Mathf.Clamp01(gone / ArrivalEase));
            view.ExitT = Mathf.Min(1f,
                view.ExitT + Time.deltaTime * WalkSpeed * pace / dist);
            view.Root.anchoredPosition = new Vector2(
                Mathf.Lerp(view.SeatX, exitX, view.ExitT), SeatLineY);
            // The entrance fades up over its first quarter; leaving fades down over the last.
            view.Group.alpha = Mathf.Clamp01((1f - view.ExitT) * 4f);

            // Mirror the walk so they face the way they are leaving (to the right).
            UpdatePatronFrame(view, PatronClip.Walk, view.AnimClock, facing: -1);
            view.AnimClock += Time.deltaTime * Mathf.Max(0.05f, pace);

            if (view.ExitT >= 1f)
            {
                view.Exiting = false;
                // They are through the door: book the visit and the stars against the FACE
                // that walked out, which is the last moment both are still in hand.
                RecordDeparture(view.Look, view.Visit);
                view.Visit = null;
                // The next customer on this stool speaks their own order from the beginning.
                view.WasKnown = false;
                view.Spoken = false;
                view.Group.alpha = 1f;
                if (view.Body != null) view.Body.flipX = false;   // reset the mirror
                view.Root.gameObject.SetActive(false);
            }
        }

        // ── the animated customer (2026-07-23) ───────────────────────────────────

        /// <summary>Chooses the clip and frame for a seated customer from their state and drives
        /// the character image: the sit-and-breathe idle while they wait, a one-shot "placing the
        /// order" beat the moment they decide, and the drink once served. (An impatience flush
        /// used to tint the body here; removed 2026-08-19, the author: "kızınca kızarmasın
        /// kararmasın" — running out of patience is the gauge's job, not the skin's.)</summary>
        private void UpdateSeatAnimation(SeatView view, CustomerVisit visit)
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
            // The clock the walk is played on runs at the pace the figure is moving, so an
            // arriving customer's feet slow with the floor instead of skating on it.
            view.AnimClock += Time.deltaTime * (seated ? 1f : Mathf.Max(0.05f, view.WalkPace));

            // A seated customer's idle is a STILL frame, so the life in it comes from where
            // they are looking. This runs only in the idle branch — somebody speaking their
            // order or lifting a glass has better things to do with their head.
            int exact = -1;
            if (clip == PatronClip.Idle) SeatedGlance(view, ref clip, ref exact);
            UpdatePatronFrame(view, clip, t, facing: 1, exactFrame: exact);
        }

        // ── where a seated customer is looking (2026-08-19) ──────────────────────
        /// <summary>How long the head takes to swing to the far end of a glance.</summary>
        // (The full turn has no constant of its own any more: it takes as long as its
        //  frames take at PatronFps. Only the idle's small glance is timed below.)
        /// <summary>How often an unattended customer looks around, and for how long.</summary>
        private const float GlanceEvery = 7f, GlanceLookSeconds = 1.5f;
        /// <summary>How long they hold the look at somebody who just sat down before
        /// facing front again. Long enough to read as having looked, short enough that
        /// a bar of six is not a room of people staring at each other.</summary>
        private const float GreetHoldSeconds = 1.3f;
        /// <summary>The frame the SMALL idle glance stops at. The same clip's measured hold
        /// frame is the full turn; this is a fraction of the way there, which is what makes
        /// looking around read as different from looking AT somebody.</summary>
        private const int GlanceSmallFrame = 2;

        /// <summary>
        /// Turns the idle into a look. Nobody beside them: they hold still and glance a
        /// little to one side every few seconds, alternating. Somebody on one side: they
        /// turn to that person and HOLD it while they are there. Somebody on both: they
        /// look between them, on the same slow clock.
        ///
        /// The clock is the seat's own animation time plus a phase off its index — six
        /// customers glancing in unison would read as a machine, and a phase is free where
        /// a random number would have to be plumbed through RunRng to stay deterministic.
        /// </summary>
        private void SeatedGlance(SeatView view, ref PatronClip clip, ref int frame)
        {
            var look = view.Look;
            if (look == null) return;
            bool right = Occupied(view.Index + 1), left = Occupied(view.Index - 1);

            // Somebody SITTING DOWN is the event, not somebody being there: the glance is a
            // one-shot on the rising edge, and it ends by coming back (2026-08-19, the
            // author: "bakma animasyonu bittikten sonra normal pozisyona geri dönmeli").
            // Held forever, it stopped being a glance and became a pose.
            if (right && !view.SawRight) { view.Greeting = true; view.GreetRight = true;  view.GreetT = 0f; }
            else if (left && !view.SawLeft) { view.Greeting = true; view.GreetRight = false; view.GreetT = 0f; }
            view.SawRight = right; view.SawLeft = left;

            bool useRight = view.Greeting ? view.GreetRight
                          : Mathf.FloorToInt((view.AnimClock + view.Index * 1.7f) / GlanceEvery) % 2 == 0;
            var want = useRight ? PatronClip.LookRight : PatronClip.LookLeft;
            if (!look.Clips.TryGetValue(want, out var frames) || frames.Length == 0)
            {
                view.Greeting = false;
                return;
            }
            int hold = Mathf.Clamp(useRight ? look.HoldRight : look.HoldLeft, 1, frames.Length - 1);

            if (view.Greeting)
            {
                view.GreetT += Time.deltaTime;
                // The head turns at the house rate too, not at a rate of its own: the turn
                // takes as long as its own frames take, which is what keeps a glance the
                // same speed as the walk beside it.
                float outT = hold / PatronFps, backAt = outT + GreetHoldSeconds;
                if (view.GreetT >= backAt + outT) { view.Greeting = false; return; }
                float u = view.GreetT < outT ? view.GreetT / outT
                        : view.GreetT < backAt ? 1f
                        : 1f - (view.GreetT - backAt) / outT;
                // Held at the MEASURED far end rather than the clip's last frame: some of
                // these clips swing back to the front on their own, and holding their end
                // would hold a face looking straight ahead.
                frame = Mathf.Clamp(Mathf.RoundToInt(u * hold), 0, hold);
                clip = want;
                return;
            }

            // Nobody just arrived: still for most of a slow cycle, then a small look out and
            // back. The clock carries a phase off the seat index — six customers glancing in
            // unison would read as a machine, and a phase is free where a random number
            // would have to be plumbed through RunRng to stay deterministic.
            float t = Mathf.Repeat(view.AnimClock + view.Index * 1.7f, GlanceEvery);
            if (t >= GlanceLookSeconds) return;                 // the still frame, which is idle
            int small = Mathf.Min(GlanceSmallFrame, frames.Length - 1);
            float g = t / GlanceLookSeconds;
            int f = g < 0.28f ? Mathf.FloorToInt(g / 0.28f * (small + 1))
                  : g > 0.72f ? Mathf.FloorToInt((1f - g) / 0.28f * (small + 1))
                  : small;
            frame = Mathf.Clamp(f, 0, small);
            clip = want;
        }

        /// <summary>Is there somebody sitting on that stool? Off the end of the bar counts
        /// as nobody, which is why the two end seats only ever glance one way.</summary>
        private bool Occupied(int index) =>
            index >= 0 && index < _seats.Count && _seats[index].Visit != null
            && _seats[index].WalkT >= 1f && !_seats[index].Exiting;

        /// <summary>Sets the character image to the right frame of <paramref name="clip"/> at time
        /// <paramref name="t"/>, mirrored when <paramref name="facing"/> is -1 (leaving right).
        /// <paramref name="exactFrame"/> overrides the clip's own timing when the caller has
        /// already decided which frame it wants (the glances, which are driven by who is
        /// sitting where rather than by a clock).</summary>
        private void UpdatePatronFrame(SeatView view, PatronClip clip, float t, int facing,
            int exactFrame = -1)
        {
            var look = view.Look ?? (_looks.Count > 0 ? _looks[0] : null);
            if (look == null || !look.Clips.TryGetValue(clip, out var frames) || frames.Length == 0) return;
            if (view.Body == null) return;
            view.Body.sprite = frames[exactFrame >= 0
                ? Mathf.Clamp(exactFrame, 0, frames.Length - 1)
                : PatronFrameIndex(clip, t, frames.Length)];
            // A touch wider than tall (CharWiden). The mirror is flipX rather than a negative
            // scale: a negative scale on a lit sprite inverts its winding and the 2D renderer
            // drops it, so a leaving customer would simply vanish.
            view.Body.flipX = facing < 0;
            SyncPatronBody(view);
        }

        /// <summary>The frame index for a clip at time t. Most clips loop at a fixed rate; the
        /// drink raises and lowers the glass over a sip window then holds it at rest, so it reads
        /// as a real sip every few seconds instead of a gulp every frame (2026-07-23).</summary>
        private static int PatronFrameIndex(PatronClip clip, float t, int n)
        {
            if (n <= 1) return 0;
            // STRAIGHT THROUGH, and hold the last frame. Every one-shot is now drawn in two
            // halves - out to the middle of the action, then INTERPOLATED back to the idle
            // pose - so its last frame is the idle pose and the return is drawn rather than
            // reversed. That is what the halves bought: a clip that ends where the idle
            // stands, at twice the frames, with nothing mirrored.
            if (clip == PatronClip.Walk) return Mathf.FloorToInt(t * PatronFps) % n;
            if (clip == PatronClip.Drink)
            {
                // A sip, then a pause standing as they were, then another sip - the
                // "1. yudum, 2. yudum" the author asked for, out of one clip that ends
                // where it began. The clip is not played flat: see DrinkTicks.
                float u = Mathf.Repeat(t, DrinkCycleSeconds) * PatronFps;   // in ticks
                int acc = 0;
                for (int i = 0; i < n; i++)
                {
                    acc += DrinkTicks(i, n);
                    if (u < acc) return i;
                }
                return n - 1;   // the rest: standing with the glass down, as the clip left them
            }
            return Mathf.Min(n - 1, Mathf.FloorToInt(t * PatronFps));
        }

        /// <summary>
        /// THE DRINK'S TIMING CHART — how many ticks of 1/PatronFps each frame is held for.
        /// One rate still (the walk's), with HOLDS on it, which is how an animator slows a
        /// beat without slowing the film: "FPS tüm animasyonlarda aynı olmalı" is untouched.
        ///
        /// Everything hangs off ONE fact about the art: the clip is two halves joined —
        /// out to the glass at the mouth, then interpolated back to the idle pose — so the
        /// SIP IS THE MIDDLE FRAME. That is measured, not assumed, and it holds across the
        /// live cast's two clip lengths: 17 frames puts the glass at the lips on 7-8-9
        /// (afrowoman, clubgirl, silverbob) and 16 puts it on 6-7-8 (heavyset). The
        /// remaining cast still stands on the old rig and does not load; when they are
        /// redrawn they arrive at 17 like everyone else.
        ///
        ///   middle ±1   the swallow            5 ticks   0.42s each
        ///   ±2 … ±4     the arm's travel       2 ticks   the lift takes ~0.5s, and so does
        ///                                                the lower, where both were 0.25s
        ///   further     standing, glass down   1 tick    dead frames at either end
        ///
        /// A clip long enough for the chart to outrun DrinkCycleSeconds would be cut at the
        /// rest rather than dropping frames; at 35 ticks (2.9s) against a 4.4s cycle the
        /// longest clip in the cast has a second and a half of room.
        /// </summary>
        private static int DrinkTicks(int frame, int n)
        {
            int off = Mathf.Abs(frame - (n - 1) / 2);
            if (off <= 1) return DrinkSipTicks;
            return off <= 4 ? 2 : 1;
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
                    [PatronClip.LookRight] = LoadPatronClip(entry.Slug, "look_right"),
                    [PatronClip.LookLeft]  = LoadPatronClip(entry.Slug, "look_left"),
                };
                // A look with no idle has no art on disk. Skip it instead of seating a
                // customer who renders as nothing.
                if (clips[PatronClip.Idle].Length == 0) continue;
                var face = Resources.Load<Sprite>($"Patron/{entry.Slug}/face");
                _looks.Add(new PatronLook
                { Slug = entry.Slug, Clips = clips, HeadY = entry.HeadY, Face = face,
                  Stars = entry.Stars,
                  HoldRight = entry.HoldRight, HoldLeft = entry.HoldLeft });
            }
        }

        /// <summary>All frames of one clip, ordered by name. Everyone in the cast lives
        /// under their own slug. The very first patron used to sit loose at Patron/&lt;clip&gt;
        /// with no slug of their own, and this read that too; that art was deleted in the
        /// 2026-08-20 sweep along with the rest of the old rig, so the branch is gone.</summary>
        private static Sprite[] LoadPatronClip(string slug, string clip)
        {
            var sprites = Resources.LoadAll<Sprite>($"Patron/{slug}/{clip}");
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

            // THE STORY'S GUEST IS NOT ROLLED (GDD 26 §8): the beat names the face, and it
            // is the same face every night of the run. Hashing their name instead would put
            // the rent collector in a different body each time he came back — which is the
            // one thing a recurring character cannot survive.
            var run = Run;
            if (visit != null && run != null && ReferenceEquals(visit, run.LastCustomer))
            {
                var written = LookForStory(run.LastCallBeat?.Who);
                if (written != null) return written;
            }

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

        // ── the slip's own grid (2026-08-10) ────────────────────────────────────
        // One rect per line, the label pinned left and the figure pinned right. Dot
        // leaders lined a receipt up only while every name stayed short; a long drink
        // pushed its price off the grid and the whole slip leaned.

        // 152x200 art at 3x (grown on the author's note). The stock is generated; the
        // SILHOUETTE is authored — straight roll sides, a fine tear carved only at the
        // top and the foot — because three takes in a row drew perforated stamp edges on
        // all four sides: shape is a specification, and the generator does not hit
        // specifications. Solid stock spans y 6..591, full width, measured off the cream.
        // SET BIGGER (2026-08-11, the author: "faturadaki yazıların puntosunu arttır"). The
        // slip was set at 8 for everything that was not a figure, which is the size the HUD
        // uses for a hint you glance at — and this is the document the whole day is read
        // off. Every line moves up one legal step: the pixel faces rasterise cleanly only at
        // whole multiples of 8, so 8 goes to 16 and 16 to 24, and the rows and the marks
        // grow with them rather than the type growing inside its old gutter.
        private const float BillW = 456f, BillH = 600f, BillHeadH = 62f, BillRowH = 26f;
        private const float BillInset = 36f;   // type margin inside the sheet
        private static readonly Color BillPaper = new Color(0.965f, 0.945f, 0.886f, 1f);
        private static readonly Color BillEdge = new Color(0.62f, 0.58f, 0.50f, 1f);
        private static readonly Color BillBand = new Color(0.102f, 0.165f, 0.290f, 1f);
        private static readonly Color BillInk = new Color(0.13f, 0.11f, 0.09f, 1f);
        private static readonly Color BillRed = new Color(0.65f, 0.17f, 0.27f, 1f);
        private static readonly Color BillQuiet = new Color(0.51f, 0.47f, 0.41f, 1f);
        private RectTransform _invoiceRows;
        private Text _billWhen;

        /// <summary>
        /// The night's stars, drawn as stars — five of them, 24px on the pixel grid, with
        /// the lit row revealed through a mask the way the top bar's standing is. A number
        /// says how the night went; a row of stars is SEEN going that way.
        /// </summary>
        // ── the stars fall in (2026-08-11) ──────────────────────────────────────
        //
        // The author: the paper comes up empty and the stars drop onto it one at a time,
        // the last one a half if the night earned a half — "yıldızlar böyle sırayla inince
        // heyecan yaratacağını düşündüm".
        //
        // Each star falls from above its place, overshoots and settles, and the next starts
        // before the one before it has finished — a stagger shorter than the drop, so the
        // row reads as a run rather than as five separate events. The HALF star needs no
        // special case: the mask that always cut the row to the night's fraction cuts the
        // last star down the middle, and now it does it to a star that is falling.
        private readonly List<RectTransform> _billStars = new List<RectTransform>();
        private float _starT = -1f;      // < 0 = not running
        private int _starCount;          // how many are due to land
        private int _landed;             // how many have; the shake fires on the change
        private float _billShake;        // 1 at the impact, decaying to 0
        private Vector2 _billHome;

        // The night's end, in beats: 1 the call, 2 the paper feeding, 3 the stars.
        private RectTransform _lastCallRt;
        private CanvasGroup _lastCallGroup;
        private Text _lastCallCard;
        private int _endBeat;
        private float _endT, _endStarFrac;
        private const float CallIn = 0.4f, CallHold = 1.5f, CallOut = 0.5f;
        /// <summary>How slowly the paper feeds, and from how far up.
        ///
        /// 1.05 → 2.6 (2026-08-11, the author: much slower still). A till does not throw
        /// paper at you; it grinds it out, and the grinding is what the whole beat is for —
        /// the player has nothing to do but watch the night arrive.</summary>
        private const float SlipFeed = 2.6f, SlipFeedFrom = 760f;

        private void StepDayEndBeats()
        {
            if (_endBeat == 0) return;
            _endT += Time.unscaledDeltaTime;

            if (_endBeat == 1)
            {
                if (Motion.Reduced) { _endT = CallIn + CallHold + CallOut; }
                float a = _endT < CallIn ? _endT / CallIn
                        : _endT < CallIn + CallHold ? 1f
                        : 1f - Mathf.Clamp01((_endT - CallIn - CallHold) / CallOut);
                if (_lastCallGroup != null) _lastCallGroup.alpha = a;
                if (_lastCallCard != null)
                {
                    // It settles as it arrives — a line that lands rather than appears.
                    float k = Mathf.Clamp01(_endT / CallIn);
                    _lastCallCard.rectTransform.anchoredPosition =
                        new Vector2(0, 10f + (1f - k) * 14f);
                }
                if (_endT < CallIn + CallHold + CallOut) return;
                if (_lastCallRt != null) _lastCallRt.gameObject.SetActive(false);
                _endBeat = 2; _endT = 0f;
                // HOME FIRST, THEN FEED. PlayPanel reads the rect's CURRENT position as the
                // place to land, and the slip has been parked off the top since the call —
                // so handing it the parked rect made it feed from 1520 down to 760 and then
                // jump the last 760 when the beat ended. Measured exactly that; put back
                // where it belongs first and the feed is one unbroken movement.
                _dayEndBill.anchoredPosition = _billHome;
                PlayPanel(_dayEndBill, new Vector2(0, SlipFeedFrom), SlipFeed,
                          fade: false, steady: true);
                return;
            }

            if (_endBeat == 2)
            {
                // The slide owns the paper until it settles; the stars wait for that.
                if (_slideRt != null && !Motion.Reduced) return;
                _dayEndBill.anchoredPosition = _billHome;
                _endBeat = 3;
                StartStarDrop(_endStarFrac);
                return;
            }

            // Beat 3: the shake lives here, so the paper is only ever moved by one thing.
            if (_billShake > 0f)
            {
                _billShake = Mathf.Max(0f, _billShake - Time.unscaledDeltaTime * 4.5f);
                float amp = _billShake * _billShake * 7f;   // dies away fast, like a strike
                _dayEndBill.anchoredPosition = _billHome + new Vector2(
                    Mathf.Sin(Time.unscaledTime * 62f) * amp * 0.5f,
                    Mathf.Sin(Time.unscaledTime * 47f) * amp);
                if (_billShake <= 0f) _dayEndBill.anchoredPosition = _billHome;
            }
            if (_starT < 0f && _stampT < 0f && _billShake <= 0f)
            {
                _endBeat = 0;
                // The night has finished counting itself; now there is somewhere to go.
                if (_billNext != null && _dayEndStep == 0) _billNext.gameObject.SetActive(true);
            }
        }
        private const float StarFallH = 70f;     // how far above its place a star starts
        /// <summary>Where in the drop the star first touches its place — the root of the
        /// out-back curve, not a number picked to look right. See the shake below.</summary>
        private const float Contact = 1f - 1.7f / (1.7f + 1f);
        private const float StarDrop = 0.5f;     // one star's fall and settle
        private const float StarStagger = 0.42f; // the gap between two starting

        /// <summary>Takes every star off the paper, so the slip lands with an empty row on
        /// it (the author: "yıldızlar ilk 0'dır"). Called before the feed, not after.</summary>
        private void EmptyStarRow()
        {
            if (Motion.Reduced) return;
            foreach (var s in _billStars)
            {
                if (s == null) continue;
                s.anchoredPosition = new Vector2(s.anchoredPosition.x, StarFallH);
                var g = s.GetComponent<Image>();
                if (g != null) g.color = Clear(g.color);
            }
        }

        /// <summary>Empties the row and starts the run. Reduced motion places them.</summary>
        private void StartStarDrop(float frac)
        {
            _starCount = Mathf.CeilToInt(Mathf.Clamp01(frac) * 5f - 0.001f);
            _landed = 0;
            // WHEN THE STAMP LANDS DEPENDS ON WHAT IT SAYS. A night that earned nothing has
            // no stars to wait for, so the stamp takes the beat they would have had. A
            // RECORD has to wait for them: the whole point is that the fifth star lands and
            // then the paper is stamped for it, which is a different sentence from stamping
            // over an empty row.
            _stampArmed = false;
            SetStampFace(_stampKind);
            // AND IT IS NOT ON THE PAPER UNTIL IT IS STRUCK (2026-08-19, the author: NEW
            // RECORD was sitting over the stars before its own animation). Showing it here
            // and only ARMING it when the last star landed are two different things, and
            // this line did the first: the stamp spent the whole star run parked at its
            // rest pose — full size, printed, crooked — over the row it was waiting for,
            // and then struck itself down over its own ink. It is shown by ArmStamp now,
            // on the frame it is driven at the paper and not before.
            if (_billStamp != null) _billStamp.gameObject.SetActive(false);
            _stampT = -1f;
            // A night that earned nothing has no stars to wait for, and reduced motion has
            // no run to wait for either — both take the stamp now.
            if (_stampKind != StampKind.None && (_starCount <= 0 || Motion.Reduced)) ArmStamp();
            if (Motion.Reduced || _billStars.Count == 0) { _starT = -1f; return; }
            _starT = 0f;
            foreach (var s in _billStars)
            {
                s.localScale = Vector3.one;
                var g = s.GetComponent<Image>();
                if (g != null) g.color = new Color(g.color.r, g.color.g, g.color.b, 0f);
            }
        }

        private void StepStarDrop()
        {
            if (_starT < 0f || _billStars.Count == 0) return;
            _starT += Time.unscaledDeltaTime;
            bool running = false;
            for (int i = 0; i < _billStars.Count; i++)
            {
                var star = _billStars[i];
                if (star == null) continue;
                var img = star.GetComponent<Image>();
                if (i >= _starCount)
                {
                    // Past the night's count: nothing to land, and the mask hides it anyway.
                    if (img != null) img.color = Opaque(img.color);
                    continue;
                }
                float t = _starT - i * StarStagger;
                if (t < 0f)
                {
                    if (img != null) img.color = Clear(img.color);
                    star.anchoredPosition = new Vector2(star.anchoredPosition.x, StarFallH);
                    running = true;
                    continue;
                }
                float k = Mathf.Clamp01(t / StarDrop);
                // Out-back: it falls past its place and rocks back into it.
                const float Over = 1.7f;
                float u = k - 1f;
                float e = u * u * ((Over + 1f) * u + Over) + 1f;
                star.anchoredPosition = new Vector2(star.anchoredPosition.x,
                    Mathf.Lerp(StarFallH, 0f, e));
                // ...and rolls as it lands, the wobble dying with the fall.
                star.localRotation = Quaternion.Euler(0, 0,
                    Mathf.Sin(k * Mathf.PI * 3f) * 14f * (1f - k));
                if (img != null)
                    img.color = new Color(img.color.r, img.color.g, img.color.b,
                                          Mathf.Clamp01(k * 4f));
                if (k < 1f) running = true;

                // THE SHAKE FIRES ON CONTACT, NOT ON REST (2026-08-11, the author: the
                // tremor and the star landing are not in step). They were not, and the
                // easing says why: an out-back curve reaches its target EARLY, punches
                // past it and rocks back. Solving e(k) = 1 for this overshoot gives
                // k = Over / (Over + 1) subtracted from 1 — 0.370 at Over 1.7 — and the
                // star is visibly on the paper from that moment, while the tween does not
                // finish until 1.0. Firing at the end put the tremor two thirds of a beat
                // after the impact it was meant to be.
                if (i >= _landed && k >= Contact) { _landed = i + 1; _billShake = 1f;
                    Sfx.Play("click", 0.5f); }
            }
            if (!running)
            {
                foreach (var s in _billStars)
                    if (s != null) { s.anchoredPosition = new Vector2(s.anchoredPosition.x, 0f);
                                     s.localRotation = Quaternion.identity; }
                _starT = -1f;
                // The stars are in; if the night beat every night before it, say so.
                if (_stampKind == StampKind.Record) ArmStamp();
            }
        }

        private static Color Clear(Color c) => new Color(c.r, c.g, c.b, 0f);
        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        private float BillStars(float y, float frac)
        {
            const float StarPx = 32f, Gap = 6f;   // the 16px star at a whole 2x
            float rowW = 5f * StarPx + 4f * Gap;
            var host = NewRect("Stars", _invoiceRows);
            host.anchorMin = new Vector2(0.5f, 1); host.anchorMax = new Vector2(0.5f, 1);
            host.pivot = new Vector2(0.5f, 1);
            host.sizeDelta = new Vector2(rowW, StarPx);
            host.anchoredPosition = new Vector2(0, -y);
            var art = ChromeArt.Mark("star");
            for (int i = 0; i < 5; i++)
            {
                var dim = NewRect("D" + i, host);
                Place(dim, new Vector2(0, 0.5f), new Vector2(StarPx, StarPx),
                    new Vector2(i * (StarPx + Gap) + StarPx * 0.5f, 0));
                dim.pivot = new Vector2(0.5f, 0.5f);
                var di = dim.gameObject.AddComponent<Image>();
                di.sprite = art; di.preserveAspect = true; di.raycastTarget = false;
                di.color = new Color(0.72f, 0.68f, 0.60f, 0.5f);
            }
            var lit = NewRect("Lit", host);
            lit.anchorMin = new Vector2(0, 0); lit.anchorMax = new Vector2(0, 1);
            lit.pivot = new Vector2(0, 0.5f);
            // TALLER THAN THE ROW, on purpose (2026-08-11). The mask is what cuts a half
            // star in half, and it has to keep doing that — but the stars now FALL into
            // place, and a mask the height of the row would clip the fall to nothing. Extra
            // height changes no horizontal clipping at all, which is the only clipping this
            // mask was ever for.
            lit.sizeDelta = new Vector2(rowW * Mathf.Clamp01(frac), StarFallH * 2f);
            lit.anchoredPosition = Vector2.zero;
            lit.gameObject.AddComponent<RectMask2D>();
            _billStars.Clear();
            for (int i = 0; i < 5; i++)
            {
                var on = NewRect("L" + i, lit);
                Place(on, new Vector2(0, 0.5f), new Vector2(StarPx, StarPx),
                    new Vector2(i * (StarPx + Gap) + StarPx * 0.5f, 0));
                _billStars.Add(on);
                on.pivot = new Vector2(0.5f, 0.5f);
                var oi = on.gameObject.AddComponent<Image>();
                oi.sprite = art; oi.preserveAspect = true; oi.raycastTarget = false;
                oi.color = UITheme.Amber[3];
            }

            // THE STAMP, for a night that earned nothing (2026-08-11, the author: if you
            // take zero stars something aggressive should come down over them, like a stamp
            // being struck). It only exists because zero is reachable now — under the old
            // 1 + 4x scale the worst room in the world still filed one star, so there was
            // never a night for this to land on.
            //
            // It hangs on the ROWS rather than on the star host, because it has to be wider
            // than the five stars it is being struck across, and it is parked here rather
            // than built on demand: the run is driven from Update, and a thing that has to
            // be animated has to exist before the frame it animates in.
            _billStamp = NewRect("Stamp", _invoiceRows);
            _billStamp.anchorMin = new Vector2(0.5f, 1); _billStamp.anchorMax = new Vector2(0.5f, 1);
            _billStamp.pivot = new Vector2(0.5f, 0.5f);
            _billStamp.sizeDelta = new Vector2(236f, 42f);
            _billStamp.anchoredPosition = new Vector2(0, -(y + StarPx * 0.5f));
            var stampPlate = _billStamp.gameObject.AddComponent<Image>();
            stampPlate.color = new Color(BillRed.r, BillRed.g, BillRed.b, 0.10f);
            stampPlate.raycastTarget = false;
            Frame(_billStamp, 3f, new Color(BillRed.r, BillRed.g, BillRed.b, 0.85f));
            _billStampInk = NewText("W", _billStamp, _display, 24, TextAnchor.MiddleCenter,
                new Color(BillRed.r, BillRed.g, BillRed.b, 0.92f));
            Stretch(_billStampInk.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, 0), new Vector2(-4, 0));
            _billStampInk.horizontalOverflow = HorizontalWrapMode.Overflow;
            _billStampInk.verticalOverflow = VerticalWrapMode.Overflow;
            _billStampInk.raycastTarget = false;
            _billStampInk.text = "DISGRACE";
            _billStamp.gameObject.SetActive(false);

            return y + StarPx + 6f;
        }

        // ── the stamp comes down (2026-08-11) ───────────────────────────────────
        private RectTransform _billStamp;
        private Text _billStampInk;
        private float _stampT = -1f;
        private const float StampFall = 0.42f;

        /// <summary>What tonight has to say for itself, if anything: the disgrace, the
        /// record, or neither. Decided once when the slip is shown, so the stamp cannot
        /// change its mind halfway down.</summary>
        private enum StampKind { None, Disgrace, Record }
        private StampKind _stampKind;
        private bool _stampArmed;

        /// <summary>Dresses the stamp for what it is about to say.</summary>
        private void SetStampFace(StampKind kind)
        {
            _stampKind = kind;
            if (_billStamp == null || kind == StampKind.None) return;
            bool good = kind == StampKind.Record;
            var ink = good ? new Color(0.16f, 0.44f, 0.20f) : BillRed;
            _billStamp.GetComponent<Image>().color = new Color(ink.r, ink.g, ink.b, 0.10f);
            foreach (var edge in _billStamp.GetComponentsInChildren<Image>(true))
                if (edge.transform != _billStamp)
                    edge.color = new Color(ink.r, ink.g, ink.b, 0.85f);
            _billStampInk.color = new Color(ink.r, ink.g, ink.b, 0.92f);
            _billStampInk.text = good ? "NEW RECORD" : "DISGRACE";
            _billStamp.sizeDelta = new Vector2(good ? 268f : 236f, 42f);
        }

        /// <summary>
        /// A rubber stamp is a thing DRIVEN at the paper: it arrives huge, out of focus and
        /// crooked, and it stops dead. So it scales down hard rather than easing, and the
        /// only softness in it is after the strike — it rocks a few degrees and settles,
        /// and the paper takes the blow on the same frame the ink lands.
        /// </summary>
        private void ArmStamp()
        {
            if (_stampArmed || _billStamp == null || _stampKind == StampKind.None) return;
            _stampArmed = true;
            if (Motion.Reduced)
            {
                _billStamp.localScale = Vector3.one;
                _billStamp.localRotation = Quaternion.Euler(0, 0, -9f);
                _billStamp.gameObject.SetActive(true);
                return;
            }
            _stampT = 0f;
            // THE FIRST FRAME OF THE STRIKE IS SET HERE, not left to the step that runs
            // next frame. Arming can happen after StepStamp has already run for this frame
            // (the zero-star night arms from the beats, which are stepped last), and a stamp
            // shown at whatever pose it was left in flashes at rest for one frame before it
            // starts falling. Shown huge, crooked and unprinted, it can only fall.
            _billStamp.localScale = new Vector3(3.4f, 3.4f, 1f);
            _billStamp.localRotation = Quaternion.Euler(0, 0, -26f);
            var ink0 = _billStampInk.color;
            _billStampInk.color = new Color(ink0.r, ink0.g, ink0.b, 0f);
            _billStamp.gameObject.SetActive(true);
        }

        private void StepStamp()
        {
            if (_stampT < 0f || _billStamp == null) return;
            _stampT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_stampT / StampFall);
            float e = k * k * k;                            // gathers pace all the way down
            float scale = Mathf.Lerp(3.4f, 1f, e);
            _billStamp.localScale = new Vector3(scale, scale, 1f);
            _billStamp.localRotation = Quaternion.Euler(0, 0,
                Mathf.Lerp(-26f, -9f, e) + Mathf.Sin(k * Mathf.PI * 4f) * 3f * (1f - k));
            var c = _billStampInk.color;
            _billStampInk.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(k * 2.2f) * 0.92f);
            if (k < 1f) return;
            _billStamp.localScale = Vector3.one;
            _billStamp.localRotation = Quaternion.Euler(0, 0, -9f);
            _stampT = -1f;
            _billShake = 1f;                                 // the paper takes it
            Sfx.Play("click", 0.9f);
        }

        /// <summary>
        /// One critic: their licence photo, their stars, their name, and one short line of
        /// WHY — derived from what the visit still knows at day end. The face is the point:
        /// reading customers is the game, so the night's verdicts wear the faces that gave
        /// them.
        /// </summary>
        private float BillCritic(float y, CustomerVisit v, Color ink)
        {
            // ONE LINE, IN COLUMNS (2026-08-11, the author: not two stacked lines, a bit
            // more table-like without being a table; the name smaller and lighter; no star
            // pictogram for the score; and the DRINK shown by its own icon).
            //
            // The columns are the receipt's own: the picture, the drink, the name and what
            // happened, and the score in the same right-hand column the money lands in. That
            // last alignment is what makes it read as a book rather than as a caption —
            // without a single rule being drawn.
            const float Photo = 34f, Frame = 3f, Chin = 9f, Glyph = 20f;
            float cardW = Photo + Frame * 2f, cardH = Photo + Frame + Chin;
            float rowH = Mathf.Max(cardH, 26f) + 6f;

            var row = NewRect("Critic", _invoiceRows);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.sizeDelta = new Vector2(0, rowH);
            row.anchoredPosition = new Vector2(0, -y);

            // A POLAROID: the white border a print has, thicker under the picture than
            // around it, dropped at a slight angle. A night's two witnesses are stapled to
            // the takings, and a print says that; a bare square crop said "here is a face".
            var look = LookFor(v);
            if (look != null && look.Face != null)
            {
                var card = NewRect("Polaroid", row);
                Place(card, new Vector2(0, 0.5f), new Vector2(cardW, cardH), Vector2.zero);
                card.pivot = new Vector2(0, 0.5f);
                card.localRotation = Quaternion.Euler(0, 0, ink == BillRed ? 2.5f : -2.5f);
                var ci = card.gameObject.AddComponent<Image>();
                ci.color = new Color(0.99f, 0.98f, 0.94f);
                ci.raycastTarget = false;
                var lift = card.gameObject.AddComponent<Shadow>();
                lift.effectColor = new Color(0.24f, 0.15f, 0.06f, 0.32f);
                lift.effectDistance = new Vector2(2, -2);

                var photo = NewRect("P", card);
                Place(photo, new Vector2(0, 1), new Vector2(Photo, Photo), new Vector2(Frame, -Frame));
                photo.pivot = new Vector2(0, 1);
                var pi = photo.gameObject.AddComponent<Image>();
                pi.sprite = look.Face; pi.raycastTarget = false;
            }

            // What they were poured, drawn rather than named — the same icon the ticket and
            // the book use, so one glance ties the three together.
            var served = v.Served ?? (v.IdInspected ? v.Order.Wanted : null);
            if (served != null)
            {
                var glyph = NewRect("D", row);
                Place(glyph, new Vector2(0, 0.5f), new Vector2(Glyph, Glyph),
                    new Vector2(cardW + 8f, 0));
                glyph.pivot = new Vector2(0, 0.5f);
                var gi = glyph.gameObject.AddComponent<Image>();
                gi.sprite = DrinkIcon.For(served, _bootstrap.Glassware);
                gi.preserveAspect = true; gi.raycastTarget = false;
                gi.enabled = gi.sprite != null;
            }

            var papers = PapersFor(look);
            string full = papers != null ? papers.Name
                : v.Regular != null ? v.Regular.Name : "a drinker";
            // THE FIRST NAME ONLY (2026-08-11). "MEREDITH NOLAN  walked out" is 26
            // characters and the column holds 23, so the row wrapped and became the two
            // lines this was built to stop being. A receipt says a first name anyway.
            int space = full.IndexOf(' ');
            string name = (space > 0 ? full.Substring(0, space) : full).ToUpperInvariant();

            // Smaller and lighter: the regular face at 16, where it used to be the heavy one
            // at 24. A name on a receipt is a line item, not a headline.
            float textX = cardW + 8f + Glyph + 8f;
            float textW = BillW - BillInset * 2f - textX - 72f;   // the star and score keep the right
            var line = NewText("L", row, _body, 16, TextAnchor.MiddleLeft, ink);
            Place(line.rectTransform, new Vector2(0, 0.5f), new Vector2(textW, rowH),
                new Vector2(textX, 0));
            line.rectTransform.pivot = new Vector2(0, 0.5f);
            // Truncate is refused here as everywhere on this slip: at these sizes it drops
            // the WHOLE line the moment the face's line height clears the rect, and both
            // critics once rendered as a star, a reason and no name at all.
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Overflow;
            line.supportRichText = true;
            line.text = name + "  <color=#" + ColorUtility.ToHtmlStringRGB(BillQuiet) + ">"
                        + CriticReason(v) + "</color>";

            // A STAR BESIDE THE FIGURE (2026-08-11, the author: so it is understood that
            // the number with a point in it is a star rating). Not five of them — the row
            // above already draws the night as five — but ONE, as a unit mark, the way a
            // price carries a currency sign. It is the smallest thing that turns "1.0" from
            // a number into a score.
            var unit = NewRect("U", row);
            Place(unit, new Vector2(1, 0.5f), new Vector2(14, 14), new Vector2(-54f, 0));
            unit.pivot = new Vector2(1, 0.5f);
            var ui = unit.gameObject.AddComponent<Image>();
            ui.sprite = ChromeArt.Mark("star");
            ui.preserveAspect = true; ui.raycastTarget = false; ui.color = ink;

            var score = NewText("N", row, _body, 24, TextAnchor.MiddleRight, ink);
            Place(score.rectTransform, new Vector2(1, 0.5f), new Vector2(52f, rowH),
                new Vector2(0, 0));
            score.rectTransform.pivot = new Vector2(1, 0.5f);
            score.horizontalOverflow = HorizontalWrapMode.Overflow;
            score.verticalOverflow = VerticalWrapMode.Overflow;
            score.text = BarRating.ExactStarsFor(v.Satisfaction).ToString("0.0");

            return y + rowH;
        }

        /// <summary>One short honest line, from what a finished visit still carries. The
        /// judge's full verdict is transient — said in the service log, never stored — so
        /// this reads the STATE: how they left, what they were made, how it landed.</summary>
        private string CriticReason(CustomerVisit v)
        {
            // SHORT, because the row is one line now (2026-08-11). The drink is drawn beside
            // the name, so the reason no longer has to name it — it only has to say what
            // went right or wrong, in the fewest words that still sound like a person.
            if (v.State == VisitState.StormedOff) return "walked out";
            if (v.IdInspected && v.Served != null && v.Order.Wanted.Id != v.Served.Id)
                return "wrong drink";
            if (v.Satisfaction >= 0.85) return "exactly right";
            if (v.Satisfaction >= 0.55) return "a fair pour";
            return "a rough pour";
        }

        private float BillRow(float y, string label, string value, Color ink, bool heavy) =>
            BillRow(y, label, value, ink, heavy, null);

        /// <summary>A block's subtotal: a short rule over the figures it adds up, and the
        /// figure alone on the right. No label — the block above it is the label.</summary>
        private float BillSub(float y, string value, Color ink)
        {
            var row = NewRect("Sub", _invoiceRows);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.sizeDelta = new Vector2(0, BillRowH);
            row.anchoredPosition = new Vector2(0, -y);

            var rule = NewRect("R", row);
            rule.anchorMin = new Vector2(0.62f, 1); rule.anchorMax = new Vector2(1, 1);
            rule.pivot = new Vector2(0.5f, 1);
            rule.sizeDelta = new Vector2(0, 1);
            rule.anchoredPosition = Vector2.zero;
            var ri = rule.gameObject.AddComponent<Image>();
            ri.color = new Color(ink.r, ink.g, ink.b, 0.45f);
            ri.raycastTarget = false;

            var v = NewText("V", row, _body, 24, TextAnchor.MiddleRight, ink);
            v.rectTransform.anchorMin = new Vector2(0.62f, 0); v.rectTransform.anchorMax = Vector2.one;
            v.rectTransform.offsetMin = Vector2.zero; v.rectTransform.offsetMax = Vector2.zero;
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            v.verticalOverflow = VerticalWrapMode.Overflow;
            v.text = value;
            return y + BillRowH;
        }

        private float BillRow(float y, string label, string value, Color ink, bool heavy, string mark)
        {
            var row = NewRect("R", _invoiceRows);
            Place(row, new Vector2(0, 1), new Vector2(0, BillRowH), new Vector2(0, -y));
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.sizeDelta = new Vector2(0, BillRowH);
            row.anchoredPosition = new Vector2(0, -y);

            // THE MARK (2026-08-10, the author asked for one per line). White silhouettes
            // tinted by the row's own ink, so the colour says whether it cost you and the
            // shape says what it was — neither has to carry both, which is the rule the
            // inspector's buff icons already follow.
            float gutter = 0f;
            if (!string.IsNullOrEmpty(mark))
            {
                // Hand-drawn at the size it prints (see ChromeArt). The generated set was
                // seven little illustrations shrunk to 16 px, which is mud with a shadow on
                // it — the author asked for simpler and more useful, and a mark that has one
                // silhouette is both.
                var art = ChromeArt.Mark(mark);
                if (art != null)
                {
                    var icon = NewRect("M", row);
                    Place(icon, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(0, 0));
                    var iimg = icon.gameObject.AddComponent<Image>();
                    iimg.sprite = art; iimg.color = ink; iimg.raycastTarget = false;
                    gutter = 24f;
                }
            }

            // BOLD IS FOR HEADINGS (2026-08-11, the author: "çok fazla kalın yazı kullanma,
            // sadece başlıklarda"). Only the two summary lines — NET and TILL — carry the
            // heavy face now; every itemised line is set in the regular one, which is what a
            // receipt does anyway: the total is the thing you are meant to see first.
            // AND THE HEAVY FACE IS NO LONGER SILKSCREEN BOLD (2026-08-11, the author:
            // "4 gibi sayilar cok kalin oldugundan sayi arasindaki bosluklar birlesiyor").
            // That is exactly what it is. The face is drawn on an 8px grid with no side
            // bearing, so at a whole 3x its digits touch and -$14 reads as one shape; the
            // pixel size was never the problem, the metrics were. PressStart2P carries its
            // gap INSIDE the cell, which is why it can be set solid at any size, and it is
            // already the game's display type. It is wider, so the heavy rows drop to 16 —
            // still the biggest thing on the slip, because nothing else is set in it.
            var l = NewText("L", row, heavy ? _display : _body, heavy ? 16 : 24,
                            TextAnchor.MiddleLeft, ink);
            l.rectTransform.anchorMin = new Vector2(0, 0); l.rectTransform.anchorMax = new Vector2(0.62f, 1);
            l.rectTransform.offsetMin = new Vector2(gutter, 0); l.rectTransform.offsetMax = Vector2.zero;
            // Overflow on both axes: the labels are one short word each, and Truncate at
            // this size drops the WHOLE line the moment the face's line height clears the
            // row — which is exactly how the critics' names went missing.
            l.horizontalOverflow = HorizontalWrapMode.Overflow;
            l.verticalOverflow = VerticalWrapMode.Overflow;
            l.text = label;

            // The figure follows the label's weight, and for a reason beyond the rule above:
            // SilkscreenBold's digits do not survive this size — the author's screenshot has
            // a SALES of $4 whose 4 is a smear, and RENT's -$14 with it. PressStart2P is not
            // the escape either, at a full 24 units a character "-$1240" would be 144 of the
            // 146 this column has. The regular face is narrow, legible and correct.
            var v = NewText("V", row, heavy ? _display : _body, heavy ? 16 : 24,
                            TextAnchor.MiddleRight, ink);
            v.rectTransform.anchorMin = new Vector2(0.62f, 0); v.rectTransform.anchorMax = Vector2.one;
            v.rectTransform.offsetMin = Vector2.zero; v.rectTransform.offsetMax = Vector2.zero;
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            v.verticalOverflow = VerticalWrapMode.Overflow;
            v.text = value;
            return y + BillRowH;
        }

        private float BillRule(float y)
        {
            var rule = NewRect("Rule", _invoiceRows);
            rule.anchorMin = new Vector2(0, 1); rule.anchorMax = new Vector2(1, 1);
            rule.pivot = new Vector2(0.5f, 1);
            rule.sizeDelta = new Vector2(0, 1);
            rule.anchoredPosition = new Vector2(0, -(y + 5f));
            rule.gameObject.AddComponent<Image>().color = BillEdge;
            return y + 12f;
        }

        private float BillNote(float y, string text) => BillNote(y, text, BillQuiet);

        private float BillNote(float y, string text, Color ink, bool centred = false)
        {
            var note = NewText("N", _invoiceRows, _body, 16,
                centred ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, ink);
            note.rectTransform.anchorMin = new Vector2(0, 1);
            note.rectTransform.anchorMax = new Vector2(1, 1);
            note.rectTransform.pivot = new Vector2(0.5f, 1);
            note.rectTransform.sizeDelta = new Vector2(0, 19f);
            note.rectTransform.anchoredPosition = new Vector2(0, -y);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.verticalOverflow = VerticalWrapMode.Overflow;
            note.text = text;
            return y + 21f;
        }

        /// <summary>
        /// The print can never run off the paper (2026-08-11, the author: "sayfanın
        /// taşmaması için altındakileri de ona göre ayarlaman gerekiyor").
        ///
        /// The slip is a fixed roll and its content is the night's, which varies: two
        /// critics or none, a debt strike or not, a warning line under it. Tuning the row
        /// heights until one measured night fits is how a layout breaks on the night after,
        /// so the block is MEASURED after it is built and, if it is longer than the paper,
        /// scaled to it. Almost every night leaves this at 1 — it is a floor under the
        /// design, not the design.
        /// </summary>
        private void FitBillToPaper(float printed)
        {
            const float FootRoom = 22f;
            float room = BillH - (BillHeadH + 12f) - FootRoom;
            float k = printed > room && printed > 0f ? room / printed : 1f;
            _invoiceRows.localScale = new Vector3(k, k, 1f);
        }

        private void ShowDayEnd()
        {
            var run = Run;
            _dayEndStep = 0;   // the bill first; the market only after CONTINUE

            // THE BEAT IS CLAIMED BEFORE THE REBUILD (2026-08-11, the author: the way out
            // must not be pressable until the slip has landed and the last star with it).
            // It was not: RebuildDayEnd shows CONTINUE while _endBeat is 0, and the beats
            // were only started AFTERWARDS — so the key came up on the first frame of the
            // night's own arrival and simply stayed there through the call, the feed and
            // the whole star run. Claiming beat 1 first is the whole fix; the rebuild then
            // sees a sequence in progress and leaves the key alone.
            _endBeat = 1;
            _endT = 0f;

            _dayEndPanel.gameObject.SetActive(true);
            RebuildDayEnd();

            // THREE BEATS, IN ORDER (2026-08-11). The night is called and the room darkens
            // behind the words; the words go; only then does the paper feed, and slowly.
            // The stars wait for it to LAND — starting them with the slide meant the night's
            // score was already being counted while the slip was still in the air.
            _endStarFrac = (float)(BarRating.ExactStarsFor(run.Floor.AverageSatisfaction)
                                   / BarRating.MaxStars);

            // WHAT TONIGHT HAS TO SAY FOR ITSELF (2026-08-11, the author: the stamp should
            // come down for a personal best too, saying so). Decided once, here, so nothing
            // downstream can change its mind halfway through the drop.
            //
            // The record is measured against the number that will actually ENTER the books:
            // Rating.CloseNight stores the night CAPPED by the fittings and the menu, so
            // comparing tonight's raw stars to a history of capped ones would claim records
            // the ledger then refuses to keep. And it needs a night to beat — the first
            // night of a run is not a personal best, it is the only entry.
            double capped = System.Math.Min(
                BarRating.ExactStarsFor(run.Floor.AverageSatisfaction),
                System.Math.Min(run.UpgradeStarCap, run.MenuStarCap));
            _stampKind = _endStarFrac <= 0f ? StampKind.Disgrace
                : run.Rating.NightsClosed > 0 && capped > run.Rating.BestNight + 1e-9
                    ? StampKind.Record
                    : StampKind.None;
            _billHome = _dayEndBill.anchoredPosition;
            if (_lastCallRt != null)
            {
                _lastCallRt.gameObject.SetActive(true);
                _lastCallGroup.alpha = 0f;
            }
            // Parked off the top, opaque: paper out of a till is opaque from its first
            // millimetre, so it is hidden by being ELSEWHERE rather than by being faint.
            _dayEndBill.anchoredPosition = _billHome + new Vector2(0, SlipFeedFrom);
            var billGroup = _dayEndBill.GetComponent<CanvasGroup>();
            if (billGroup != null) billGroup.alpha = 1f;
            EmptyStarRow();
        }

        private void OnDayEndAdvance()
        {
            if (_dayEndStep == 0)
            {
                _dayEndStep = 1;
                Sfx.Play("click", 0.6f);
                RebuildDayEnd();
                // THE SLIP GOES AND THE VAN ARRIVES: the bill leaves to the left, the
                // market comes in from the right, so the two read as one movement through
                // the evening rather than as two screens that happened to follow.
                PlayPanel(_dayEndTablet, new Vector2(180f, 0f), 0.34f);
            }
            else
            {
                // ASK BEFORE THE DOOR SHUTS (2026-08-14, the author: "markette eğer bir şey
                // satın almadan devam ediyorsan veya sepetinde ürün varken devam et diyorsa
                // oyuncu ekranda emin misin diye bir buton çıkmalı").
                //
                // Two ways to lose something here and no way back from either: picks sitting
                // in the basket are thrown away unbought, and a night nobody shopped on is a
                // night of rent for nothing. Both are silent today. The question is asked
                // only when there is something to lose — a bar that bought its stock and
                // emptied its basket is waved straight through, because a confirm on every
                // night is a key you learn to press without reading.
                string worry = ClosingWorry();
                if (worry != null) { ShowClosingAsk(worry); return; }
                // Closing the shop IS the screen going dark: the tablet pulls away and the
                // curtain takes over, so the market never simply vanishes.
                PlayTabletOut();
            }
        }

        /// <summary>What the player is about to lose by closing, or null when nothing is.</summary>
        private string ClosingWorry()
        {
            if (_cart.Count > 0)
                return _cart.Count == 1
                    ? "1 THING IS STILL IN THE BASKET."
                    : _cart.Count + " THINGS ARE STILL IN THE BASKET.";
            var run = Run;
            if (run != null && run.TodaysPurchases.Count == 0)
                return "THE VAN LEAVES EMPTY TONIGHT.";
            return null;
        }

        // ── panel movement (2026-08-10) ─────────────────────────────────────────
        // Driven from Update by a timer rather than by a coroutine, for the same reason the
        // curtain is: it is the pattern this HUD is known to run correctly, it survives a
        // panel being rebuilt underneath it, and its whole state is two floats anybody can
        // read back. A coroutine here left the panels parked at their start offset when
        // anything interrupted them, which is worse than no animation at all.

        private RectTransform _slideRt;
        private Vector2 _slideHome, _slideFrom;
        private float _slideT, _slideDur;
        private bool _slideOut;                 // out: away and gone, then tomorrow
        private bool _slideFade = true;         // paper does not fade in; a tablet does
        private bool _slideSteady;              // paper is extruded, not thrown
        private CanvasGroup _slideGroup;

        /// <summary>Brings a panel in from an offset. Reduced motion places it.</summary>
        /// <param name="steady">Feed it at a near-even rate instead of the usual soft
        /// landing. A panel arrives — fast, then settling — but paper is EXTRUDED, at the
        /// speed of the motor pushing it; the ease-out that suits the tablet spends most of
        /// a long duration crawling the last inch, which at 2.6 seconds reads as a fault.</param>
        private void PlayPanel(RectTransform rt, Vector2 from, float dur, bool fade = true,
            bool steady = false)
        {
            SettleSlide();
            if (rt == null) return;
            if (Motion.Reduced) return;
            _slideFade = fade;
            _slideSteady = steady;
            _slideRt = rt;
            _slideGroup = rt.GetComponent<CanvasGroup>();
            if (_slideGroup == null) _slideGroup = rt.gameObject.AddComponent<CanvasGroup>();
            _slideHome = rt.anchoredPosition;
            _slideFrom = from;
            _slideDur = dur;
            _slideT = 0f;
            _slideOut = false;
            rt.anchoredPosition = _slideHome + from;
            _slideGroup.alpha = fade ? 0f : 1f;
        }

        /// <summary>The market pulls away, and the night begins from black behind it.</summary>
        private void PlayTabletOut()
        {
            SettleSlide();
            if (_dayEndTablet == null || Motion.Reduced) { OnOpenTomorrow(); return; }
            _slideRt = _dayEndTablet;
            _slideGroup = _dayEndTablet.GetComponent<CanvasGroup>();
            if (_slideGroup == null) _slideGroup = _dayEndTablet.gameObject.AddComponent<CanvasGroup>();
            _slideHome = _dayEndTablet.anchoredPosition;
            _slideFrom = new Vector2(0f, -220f);
            _slideDur = 0.3f;
            _slideT = 0f;
            _slideOut = true;
        }

        /// <summary>Puts whatever was moving back where it belongs. Any new movement starts
        /// from rest, so an interrupted slide can never become the panel's new home.</summary>
        private void SettleSlide()
        {
            if (_slideRt == null) return;
            _slideRt.anchoredPosition = _slideHome;
            if (_slideGroup != null) _slideGroup.alpha = 1f;
            _slideRt = null; _slideGroup = null;
        }

        /// <summary>
        /// How paper arrives: fed at a near-even rate, then landing on its stop with a
        /// bounce (2026-08-11, the author: at the very bottom it should bounce a little and
        /// settle where it belongs).
        ///
        /// The last seventh of the run is the landing, and it is a rebound UPWARD — a thing
        /// dropping onto a surface comes back off it, it does not sink past it. Two hops,
        /// the second a quarter of the first, both returning exactly to the rest position,
        /// so the settle is a consequence of the curve rather than a correction after it.
        /// </summary>
        private static float PaperLand(float k)
        {
            const float Feed = 0.86f, Hop = 0.035f;
            if (k < Feed) return 1f - Mathf.Pow(1f - k / Feed, 1.35f);
            float u = (k - Feed) / (1f - Feed);
            return 1f - Hop * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * (1f - u);
        }

        private void StepSlide()
        {
            if (_slideRt == null) return;
            _slideT += Time.unscaledDeltaTime;
            float k = _slideDur <= 0f ? 1f : Mathf.Clamp01(_slideT / _slideDur);
            if (_slideOut)
            {
                float e = k * k;                                   // gathers pace away
                _slideRt.anchoredPosition = Vector2.Lerp(_slideHome, _slideHome + _slideFrom, e);
                if (_slideGroup != null) _slideGroup.alpha = 1f - e;
                if (k >= 1f) { SettleSlide(); OnOpenTomorrow(); }
                return;
            }
            // Paper feeds near-even and BOUNCES onto its stop. Everything else keeps the
            // old soft landing.
            float o = _slideSteady ? PaperLand(k)
                : 1f - (1f - k) * (1f - k) * (1f - k);              // lands soft
            _slideRt.anchoredPosition = Vector2.Lerp(_slideHome + _slideFrom, _slideHome, o);
            if (_slideGroup != null)
                _slideGroup.alpha = _slideFade ? Mathf.Clamp01(k * 1.8f) : 1f;
            if (k >= 1f) SettleSlide();
        }

        private void RebuildDayEnd()
        {
            var run = Run;
            _dayEndBill.gameObject.SetActive(_dayEndStep == 0);
            _dayEndTablet.gameObject.SetActive(_dayEndStep == 1);
            // NOT UNTIL THE LAST STAR HAS LANDED (2026-08-11, the author). A way out
            // offered while the night is still being counted is a way out taken: the whole
            // point of the drop is that the player watches it, and a button under it is the
            // one thing that can make them look away. StepDayEndBeats shows it.
            // ...but a rebuild AFTER the counting has finished must not take it away again:
            // the beats are over and nothing would ever put it back.
            if (_billNext != null)
                _billNext.gameObject.SetActive(_dayEndStep == 0 && _endBeat == 0);
            _dayEndTitle.text = "LAST CALL — TIME TO ORDER";
            if (_billNextLabel != null) _billNextLabel.text = "GO TO THE ORDER";
            // NO TITLE OVER THE SLIP (2026-08-11, the author: take the yellow LAST CALL —
            // THE BOOKS off the top). The slip already says LAST CALL across its own head in
            // its own ink; a second one in the scrim above it was the same words twice, in a
            // colour belonging to neither. The market keeps its line, because the tablet does
            // not name itself.
            _dayEndTitle.gameObject.SetActive(_dayEndStep == 1);
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
            // (the strike stamp is a ROW now — see the bottom of the slip. What stood here
            // was the last of the one-Text receipt: a rich-text block nobody printed.)

            // RECEIPT v3 (2026-08-10, the author: "tüm satırların uyacağı arka plan ve
            // metin düzeni"). It was one Text with hand-typed dot leaders holding the
            // columns together, which only lines up while every name is short enough —
            // a long drink name pushed its price off the grid and the whole slip leaned.
            // Every line is a ROW now: a rect with the label pinned left and the figure
            // pinned right, so the columns are structural and no string can bend them.
            // The lines are taken from what was POURED (`visit.Served`) and priced at
            // `PaidBase`, so a night where the player misread somebody still adds up.
            foreach (Transform old in _invoiceRows) Destroy(old.gameObject);
            if (_billWhen != null)
                _billWhen.text = CalendarFor(run.Day) + "  ·  " + CrowdName(run.CrowdToday);
            float y = 0f;

            // THE NIGHT IN ONE LOOK (2026-08-10, the author: "az ama öz" — less type, only
            // what has to be known and SEEN). The itemised drink list is gone: it was the
            // noisiest block on the slip and its answer lives in SALES anyway. What earns
            // its place instead: the night's stars drawn AS stars, and the two people who
            // decided them — the best and the worst of the room, face, score and reason.
            double tonight = BarRating.ExactStarsFor(floor.AverageSatisfaction);
            y = BillStars(y, (float)(tonight / BarRating.MaxStars));
            y = BillNote(y, "TONIGHT " + tonight.ToString("0.0") + "  ·  BAR "
                            + run.Rating.Average.ToString("0.0") + "  ·  "
                            + served + " SERVED  ·  " + stormed + " WALKED", BillQuiet, centred: true);
            y += 8f;

            // The critics: the highest and the lowest word the night produced. One visit
            // gets one row; an empty room gets nothing, not a block of placeholders.
            CustomerVisit high = null, low = null;
            foreach (var v in floor.Finished)
            {
                if (v.State == VisitState.Served && (high == null || v.Satisfaction > high.Satisfaction))
                    high = v;
                if (low == null || v.Satisfaction < low.Satisfaction) low = v;
            }
            if (low == high) low = null;
            if (high != null || low != null)
            {
                y = BillRule(y);
                if (high != null)
                    y = BillCritic(y, high, BillInk);
                if (low != null)
                    y = BillCritic(y, low, BillRed);
            }

            // WHAT CAME IN, WHAT WENT OUT, WHAT IS LEFT (2026-08-11, the author: "gider ve
            // kalan daha açık belli edilsin"). The five figures used to run as one ladder
            // with a rule under it, so the reader had to notice for themselves which of them
            // were takings and which were bills. They are two named blocks now, each with its
            // own subtotal — the shape of a receipt — and only the last two lines are heavy.
            int tookIn = run.DaySales + run.DayTips;
            int paidOut = run.DayRent + run.DayStock + run.DayUpgrades;

            y = BillRule(y);
            y = BillNote(y, "TOOK IN", BillQuiet);
            y = BillRow(y, "SALES", "$" + run.DaySales, BillInk, false, "sales");
            y = BillRow(y, "TIPS", "$" + run.DayTips, BillInk, false, "tips");
            y = BillSub(y, "$" + tookIn, BillInk);

            y += 4f;
            y = BillNote(y, "PAID OUT", BillQuiet);
            y = BillRow(y, "RENT", "-$" + run.DayRent, BillRed, false, "rent");
            y = BillRow(y, "STOCK", "-$" + run.DayStock, BillRed, false, "stock");
            y = BillRow(y, "SHOP", "-$" + run.DayUpgrades, BillRed, false, "shop");
            y = BillSub(y, "-$" + paidOut, BillRed);

            y += 4f;
            y = BillRule(y);
            y = BillRow(y, "NET", (net >= 0 ? "+$" : "-$") + Math.Abs(net),
                        net >= 0 ? BillInk : BillRed, true, "net");
            y = BillRow(y, "TILL", (run.Money < 0 ? "-$" + (-run.Money) : "$" + run.Money),
                        run.Money < 0 ? BillRed : BillInk, true, "till");
            if (run.Ledger.DebtStrikes > 0)
            {
                y += 6f;
                y = BillNote(y, "IN THE RED — STRIKE " + run.Ledger.DebtStrikes
                                + "/" + DayLedger.StrikesToClose, BillRed);
                if (run.Ledger.DebtStrikes == DayLedger.StrikesToClose - 1)
                    y = BillNote(y, "one more red day closes the bar", BillRed);
            }

            FitBillToPaper(y);

            // The sheet is the ROLL's size; the print is the night's. What varies is how
            // much blank stock is left above the foot tear — which is how receipts work.
            if (_billNext != null)
                _billNext.anchoredPosition = new Vector2(0, -(BillH * 0.5f + 34f));

            // The tablet.
            foreach (Transform child in _offerRow) Destroy(child.gameObject);
            // (the account line counts too — see RunTheTill)
            // Tonight's fitting, said ONCE. It used to appear in five places — a band, a
            // rail note, the stool's tip, the glassware tip and a toast — and the author
            // still met it as a surprise, because none of the five was beside the control
            // it governed. It sits at the end of the department bar now, with a lamp.
            bool room = run.CanFitTonight && !CartHasFitting();
            if (_fittingNote != null)
            {
                _fittingNote.text = room ? "1 UPGRADE TONIGHT" : "UPGRADE USED";
                _fittingNote.color = room ? ShopViceDeep : ShopCost;
            }
            if (_fittingLamp != null) _fittingLamp.color = room ? ShopViceLit : ShopCost;
            for (int i = 0; i < _shopTabKeys.Length; i++)
            {
                bool on = i == _shopTab;
                _shopTabKeys[i].sprite = null;
                _shopTabKeys[i].color = on ? ShopViceDeep : ShopPaper;
                var key = (RectTransform)_shopTabKeys[i].transform;
                key.sizeDelta = new Vector2(TabKeyW, on ? TabLiveH : TabRestH);
                if (_shopTabLits[i] != null) _shopTabLits[i].enabled = on;
            }

            // The basket SHOWS what is in it (2026-08-11, the author: "sepetteki font
            // okunmuyor ... ürünlerin ikonu da gözükmeli ... üstüne basınca çıkarılabilmeli").
            // It used to be four names set at 8 in a 312-wide box with "+2 more" under them,
            // which is the whole failure in one line: too small to read, and everything past
            // the fourth thing simply gone. The foot is the basket now, and a picked line is
            // a chip you can see and press.
            if (_cartHeadLabel != null)
                _cartHeadLabel.text = _cart.Count == 0 ? "BASKET" : $"BASKET ({_cart.Count})";
            RebuildBasket();
            if (_cartTotal != null)
                _cartTotal.text = _cart.Count == 0 ? "" : "$" + CartTotal();
            // "TOTAL" with nothing after it is a label for a number that is not there.
            if (_cartTotalLabel != null) _cartTotalLabel.enabled = _cart.Count > 0;
            if (_checkoutLabel != null)
                if (_checkoutUntil < 0f)
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
                    Body = "Fills every bottle behind the bar. $"
                           + cfg.RefillPricePerCapacity + " per measure.",
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
                _liquorHead = _cardTarget; _kegHead = null; _garnishHead = null;
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
                    { anyKeg = true; _cardTarget = ShopSection("ON TAP — THE KEGS"); _kegHead = _cardTarget; }
                    if (pass == 1 && !booze && !anyGarnish)
                    { anyGarnish = true; _cardTarget = ShopSection("THE GARNISH TRAY"); _garnishHead = _cardTarget; }
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

                // THE LOCK BELONGS TO THE AISLE, NOT TO THE DEPARTMENT (2026-08-10, the
                // author). One crate at the foot of the tab said "more is coming" without
                // saying WHERE, so a player looking at a finished keg aisle had to guess
                // whether the news was about kegs or about spirits. Each aisle answers for
                // its own shelf now, and an aisle with nothing behind a star says nothing.
                SectionGate(run, booze
                    ? (System.Func<IngredientCard, bool>)(c => c.Type != IngredientType.Beer)
                    : (c => c.Type != IngredientType.Garnish), booze,
                    booze ? "bottle" : "mixer", _liquorHead);
                // AN AISLE THAT IS ALL LOCK STILL NEEDS ITS SIGN (2026-08-14). These two
                // sections were only ever created while drawing an OFFER, so an aisle whose
                // whole shelf is still behind a star had no header for its crate to stand
                // under — and SectionGate returns on a null grid. Harmless while every
                // garnish was for sale on night one; live the moment mint (3.0 stars) and
                // the olives (4.0) moved onto the ladder, because below three stars no
                // garnish is for sale, the mixer crate excludes garnishes by design, and
                // the two of them would have been counted by nothing at all. That is the
                // exact silence the ladder was built to end, one aisle further down.
                var kegHead = booze
                    ? AisleSign(run, _kegHead, c => c.Type == IngredientType.Beer, true,
                        "ON TAP — THE KEGS")
                    : null;
                if (kegHead != null)
                    SectionGate(run, c => c.Type == IngredientType.Beer, true, "keg", kegHead);
                var garnishHead = !booze
                    ? AisleSign(run, _garnishHead, c => c.Type == IngredientType.Garnish, false,
                        "THE GARNISH TRAY")
                    : null;
                if (garnishHead != null)
                    SectionGate(run, c => c.Type == IngredientType.Garnish, false, "garnish", garnishHead);
            }
            else if (_shopTab == 3)
            {
                _cardTarget = ShopSection("THE RECIPE BOOK");
                // LOWEST GATE FIRST (the author). The book is a ladder — what opens next
                // is the only thing on it the player can act on — and it was listing in
                // catalogue order, so the drink three stars away sat above the one that
                // unseals tonight. Ties keep the catalogue's order.
                var book = new List<RecipeDefinition>(run.LockedRecipes);
                // OrderBy, not Sort (audit 2026-08-11): List.Sort is introsort and NOT
                // stable, so the big tie groups reshuffled between rebuilds while the
                // comment above promised catalogue order. LINQ OrderBy is stable.
                book = book.OrderBy(run.RecipeStarGate).ToList();
                foreach (var recipe in book)
                {
                    var r = recipe;
                    // ASK THE LOCK; DO NOT RE-DERIVE IT (GDD 26 §12.2 step 4). This compared
                    // the rating to a rank table itself and wrote its own two sentences —
                    // which meant a page locked behind anything else, a person for instance,
                    // would have been drawn as though it were waiting for stars. The lock
                    // says what it wants and the crate prints that.
                    var lockedBy = run.RecipeUnlock(r);
                    if (!lockedBy.MetBy(run))
                    {
                        // SEALED, and the name never reaches the tile — that is the whole
                        // mechanic. No art either: the empty well is the tell.
                        double gate = run.RecipeStarGate(r);
                        AddTile(new TileSpec
                        {
                            Name = "Sealed Crate",
                            Meta = "Sealed",
                            Money = gate.ToString("0.0"),
                            State = TileState.Sealed,
                            Identity = "A SEALED CRATE",
                            MetaLine = "The house will not open this one for you yet",
                            Body = lockedBy.Sentence,
                            BuffA = new Buff(BuffKind.Bad, lockedBy.Sentence),
                        });
                        continue;
                    }
                    // WHAT THE SHELF CANNOT POUR, said in the description as well as drawn
                    // on the card (2026-08-10, the author). A recipe you cannot make is still
                    // worth buying — the stock comes later — but that has to be a decision,
                    // not a surprise on the first night it is ordered.
                    var lacking = MissingStyles(r);
                    var spec = new TileSpec
                    {
                        Name = r.Name,
                        Meta = PrepWord(r) + " · " + GlassNameFor(r),
                        Art = DrinkIcon.For(r, _bootstrap.Glassware),
                        ArtH = IconH,
                        Recipe = r,
                        Identity = r.Name.ToUpperInvariant(),
                        MetaLine = PrepWord(r) + " · served in a " + GlassNameFor(r),
                        Body = BandLine(r),
                        BuffA = new Buff(BuffKind.Gain, "On the menu tomorrow — one more drink to sell"),
                        BuffB = lacking.Count == 0
                            ? new Buff(BuffKind.Gain, "Your shelf can pour it tonight")
                            : new Buff(BuffKind.Bad, "Nothing on the shelf pours "
                                + string.Join(" or ", lacking)),
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
                    Body = "One more customer can sit at the bar.",
                    BuffA = new Buff(BuffKind.Gain, "+1 seat · +0.25 stars"),
                    BuffB = new Buff(BuffKind.Bad, "Uses tonight's one upgrade"),
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
                    Body = "A better bar top makes every customer happier. Even the "
                           + "ones whose drink you get wrong.",
                    BuffA = new Buff(BuffKind.Gain, "+0.03 on every served visit, up to +0.06"),
                    BuffB = new Buff(BuffKind.Bad, "Uses tonight's one upgrade"),
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
                        Body = "Better glasses make every customer happier, and win you stars.",
                        BuffA = new Buff(BuffKind.Gain, "+1 rung on the " + glass.Name.ToLowerInvariant()
                                         + " line · every drink served in one"),
                        BuffB = new Buff(BuffKind.Bad, "Uses tonight's one upgrade"),
                    };
                    if (maxed) { spec.State = TileState.Held; spec.Word = "MAX"; }
                    else DressBuyable(spec, stepPrice, "glass:" + glass.Id, true,
                        () => run.BuyGlassTier(glass.Id));
                    AddTile(spec);
                }

                // THE DRESSING (2026-08-10): the modular room pieces. Cosmetic, so no
                // fitting is spent — a fern changes what the room looks like, not what
                // the bar can do — and each piece names its own slot in the picture.
                // Unlike the sealed recipe crates, a gated piece SHOWS itself: hiding
                // names is the recipe book's mechanic, not the furniture catalogue's.
                if (run.FixtureCatalogue.Count > 0)
                {
                    _cardTarget = ShopSection("THE DRESSING");
                    foreach (var fx in run.FixtureCatalogue)
                    {
                        var f = fx;
                        var spec = new TileSpec
                        {
                            Name = f.Name,
                            Meta = f.IsTap ? f.TapLevel + "-line tower"
                                 : f.HasLight ? "Dressing · lit" : "Dressing",
                            Art = FixtureArt(f.Sprite),
                            ArtH = IconH,
                            Identity = f.Name.ToUpperInvariant(),
                            MetaLine = f.IsTap
                                ? "The counter · " + f.TapLevel
                                  + (f.TapLevel == 1 ? " keg on tap" : " kegs on tap")
                                : f.HasLight
                                ? "The room · carries its own light"
                                : "The room · dressing",
                            Body = f.Flavor,
                            // A TOWER IS NOT DRESSING and its card must not say it is: it
                            // is the only fixture that changes what the bar can sell, and
                            // the whole reason the player is looking at it is the keg they
                            // cannot buy yet.
                            BuffA = new Buff(BuffKind.Gain, f.IsTap
                                ? "Pours " + f.TapLevel + (f.TapLevel == 1 ? " keg" : " kegs")
                                  + " · the market opens the rest"
                                : f.HasLight
                                ? "Stands in the room and lights it"
                                : "Stands in the room from tonight"),
                            BuffB = new Buff(BuffKind.Gain, "Never spends the night's fitting"),
                        };
                        if (run.OwnsFixture(f.Id))
                        {
                            spec.State = TileState.Held;
                            // A tower that has been fitted over is not what is standing on
                            // the counter, and saying OURS about all three would leave the
                            // player unable to tell which one the bar actually runs.
                            spec.Word = f.IsTap && f.TapLevel < run.TapLevel ? "FITTED" : "OURS";
                        }
                        else if (run.Rating.Average < f.Stars)
                        {
                            spec.State = TileState.Sealed;
                            spec.Money = f.Stars.ToString("0.0");
                            spec.BuffA = new Buff(BuffKind.Bad, "Needs a " + f.Stars.ToString("0.0")
                                + "-star room · you are at " + run.Rating.Average.ToString("0.0"));
                        }
                        else if (f.IsTap && !run.CanBuyTap(f))
                        {
                            // One rung at a time, and the tile says which rung is missing —
                            // a greyed tower with no reason on it reads as a bug.
                            spec.State = TileState.Sealed;
                            spec.Money = (run.TapLevel + 1).ToString();
                            spec.GateNote = "LINE TOWER FIRST";
                            spec.BuffA = new Buff(BuffKind.Bad, "Fit the " + (run.TapLevel + 1)
                                + "-line tower first · this bar runs " + run.TapLevel);
                        }
                        else DressBuyable(spec, f.Price, "fx:" + f.Id, false,
                            () => run.BuyFixture(f.Id));
                        AddTile(spec);
                    }
                }
            }

            // WHAT THE NEXT STAR OPENS, IN EVERY DEPARTMENT (the author, 2026-08-10).
            // The board only shows what the room's standing already allows, so anything
            // waiting behind the next rung was invisible and the player could not tell an
            // empty aisle from a FINISHED one. Two aisles carried this tile; now every
            // department that still has something locked carries it, and it always names
            // the NEXT gate — at two stars it is the three-star crate, not the two.
            {
                int locked = 0;
                double next = double.MaxValue;
                string noun = "line", plural = "lines", verb = "the van will not bring you yet";
                // Liquor and mixers answer per AISLE now (SectionGate), because "more is
                // coming" without saying which shelf is a question, not an answer.
                if (_shopTab == 3)
                {
                    noun = "drink"; plural = "drinks"; verb = "the house will not open for you yet";
                    foreach (var r in run.LockedRecipes)
                    {
                        // Locked-ness is the LOCK's answer; the "next at" hint is still a
                        // star, because a page waiting on a person has no number to count
                        // towards and must not pull the hint down to zero.
                        if (run.RecipeUnlock(r).MetBy(run)) continue;
                        locked++;
                        double gate = run.RecipeStarGate(r);
                        if (r.Unlock == null && gate < next) next = gate;
                    }
                }
                else if (_shopTab == 4)
                {
                    noun = "piece"; plural = "pieces"; verb = "the room has not earned yet";
                    foreach (var f in run.FixtureCatalogue)
                    {
                        if (run.OwnsFixture(f.Id) || run.Rating.Average >= f.Stars) continue;
                        // A tower waiting on the rung below it is not waiting on a star, and
                        // counting it here would promise that the next star opens something
                        // no star opens (the same trap StarsWanted answers NaN to).
                        if (f.IsTap && !run.CanBuyTap(f)) continue;
                        locked++;
                        if (f.Stars < next) next = f.Stars;
                    }
                }
                if (locked > 0)
                    AddTile(new TileSpec
                    {
                        Name = locked + " more waiting",
                        Money = next.ToString("0.0"),
                        State = TileState.Sealed,
                        Identity = "MORE AT " + next.ToString("0.0") + " STARS",
                        MetaLine = locked + " " + (locked == 1 ? noun : plural) + " " + verb,
                        Body = "Get " + next.ToString("0.0") + " stars and more of these "
                               + "show up here.",
                        BuffA = new Buff(BuffKind.Bad, "Needs " + next.ToString("0.0")
                                         + " stars · you have " + run.Rating.Average.ToString("0.0")),
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
                    Body = "What it brings depends on what you already have, and on "
                           + "how many stars you have.",
                });

            // NO REFUNDS (2026-08-11, the author: "iadeyi kaldıralım"). A shelf that
            // could be un-bought at the same close made every purchase provisional: the
            // cheapest way to play the market was to buy the lot, look at the night, and
            // send back whatever the room did not want. An order is an order now, and the
            // basket — which is still free to empty before it is placed — is where the
            // deciding belongs.

            // The reading card is put away with every rebuild: the tiles it was describing
            // have just been destroyed, so a card left up is a description of nothing that
            // the pointer never asked for.
            ShowShopCard(null);

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
            var art = spec.Art;                 // the basket draws what the tile drew
            spec.OnClick = () => ToggleCart(cartKey, label, price, isFitting, buy, art);
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
                case TycoonRun.DayPurchase.Kind.Fixture:
                    foreach (var f in Run.FixtureCatalogue)
                        if (f.Id == pch.Id) return FixtureArt(f.Sprite);
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

        // A basket chip: wide enough to show what the thing IS, narrow enough that a night's
        // shopping fits on one row. The row shrinks its chips toward the floor as it fills;
        // past the floor it says how many more it is holding rather than dropping them.
        private const float ChipMax = 84f, ChipMin = 46f, ChipGap = 6f;

        /// <summary>
        /// Draws the basket: one chip per picked line, each with the product's own art and
        /// its price, each a button that takes it back out (2026-08-11, the author).
        /// </summary>
        private void RebuildBasket()
        {
            if (_cartChips == null) return;
            foreach (Transform child in _cartChips) Destroy(child.gameObject);
            if (_cartEmpty != null) _cartEmpty.gameObject.SetActive(_cart.Count == 0);
            if (_cart.Count == 0) return;

            float rowW = _cartChips.rect.width, rowH = _cartChips.rect.height;
            int fits = Mathf.Max(1, Mathf.FloorToInt((rowW + ChipGap) / (ChipMin + ChipGap)));
            bool over = _cart.Count > fits;
            int shown = over ? fits - 1 : _cart.Count;
            int slots = over ? fits : _cart.Count;
            float box = Mathf.Clamp((rowW + ChipGap) / slots - ChipGap, ChipMin, ChipMax);
            float x = 0f;
            for (int i = 0; i < shown; i++)
            {
                AddCartChip(_cart[i], x, box, rowH);
                x += box + ChipGap;
            }
            if (over) AddMoreChip(_cart.Count - shown, x, box, rowH);
        }

        private void AddCartChip(CartEntry entry, float x, float box, float rowH)
        {
            var chip = ChipPlate("Chip_" + entry.Key, x, box, rowH, PlateOf(TileState.Picked));

            // The product, standing on the chip's own line — measured off the drawing, so a
            // carton saved with air around it is the same size as the bottle beside it. It
            // takes the whole chip above the price now (2026-08-11, the author: the X went,
            // the picture grew into the corner it was using).
            var art = NewRect("Art", chip);
            VesselArt.StandOn(art, new Vector2(0.5f, 0f), entry.Art, rowH - 28f, new Vector2(0, 22f));
            var ai = art.gameObject.AddComponent<Image>();
            ai.sprite = entry.Art;
            ai.preserveAspect = true;
            ai.raycastTarget = false;
            ai.enabled = entry.Art != null;

            // The price in the AISLE's own money face and size (MoneyFace at 16), so a thing
            // in the basket and the same thing on the shelf are priced in one voice.
            string token = "$" + entry.Price;
            var price = NewText("P", chip, MoneyFace(token), 16, TextAnchor.MiddleCenter,
                MoneyInk(TileState.Picked));
            Place(price.rectTransform, new Vector2(0.5f, 0), new Vector2(box - 4f, 20), new Vector2(0, 3));
            price.horizontalOverflow = HorizontalWrapMode.Overflow;
            price.text = token;

            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip.GetComponent<Image>();
            var e = entry;
            button.onClick.AddListener(() => ToggleCart(e.Key, e.Label, e.Price, e.IsFitting, e.Buy, e.Art));

            var sink = chip.gameObject.AddComponent<PressSink>();
            sink.Face = art; sink.Depth = 3f; sink.Lift = 3f; sink.Squash = 0.02f;

            // A chip is a picture and a price; WHICH bottle it is goes on the pointer, in the
            // same card the aisle uses, so the basket needs no small print at all.
            var hover = chip.gameObject.AddComponent<HoverRelay>();
            hover.Entered = () => ShowShopCard(new TileSpec
            {
                Identity = (e.Label ?? "").ToUpperInvariant(),
                MetaLine = "$" + e.Price + "  ·  in the basket",
                Body = "Click to take it back out. You only pay when you place the order.",
                Art = e.Art,
            });
            hover.Exited = () => ShowShopCard(null);
        }

        /// <summary>What the row could not fit, counted rather than dropped.</summary>
        private void AddMoreChip(int more, float x, float box, float rowH)
        {
            var chip = ChipPlate("Chip_more", x, box, rowH, ShopAisle);
            var label = NewText("L", chip, _display, 16, TextAnchor.MiddleCenter, ShopInk);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = "+" + more;
            var hover = chip.gameObject.AddComponent<HoverRelay>();
            int n = more;
            hover.Entered = () => ShowShopCard(new TileSpec
            {
                Identity = "AND " + n + " MORE",
                MetaLine = "the basket holds them all",
                Body = "The row is full, the basket is not. Everything you picked "
                     + "is in the total.",
            });
            hover.Exited = () => ShowShopCard(null);
        }

        private RectTransform ChipPlate(string name, float x, float box, float rowH, Color paper)
        {
            var chip = NewRect(name, _cartChips);
            chip.anchorMin = chip.anchorMax = new Vector2(0, 1);
            chip.pivot = new Vector2(0, 1);
            chip.sizeDelta = new Vector2(box, rowH);
            chip.anchoredPosition = new Vector2(x, 0);
            var plate = chip.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.Card();
            plate.type = Image.Type.Sliced;
            plate.color = paper;
            return chip;
        }

        /// <summary>Notes where the aisle is standing, so a rebuild can put it back.</summary>
        private void RememberScroll()
        {
            if (_shopScroll != null) _shopScrollAt = _shopScroll.verticalNormalizedPosition;
        }

        /// <summary>Picks a listing up, or puts it back. Refuses what the night cannot
        /// carry: a second fitting, or more money than the till holds.</summary>
        private void ToggleCart(string key, string label, int price, bool isFitting, Action buy,
            Sprite art = null)
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
                if (!Run.CanFitTonight) { Toast("ONE UPGRADE A NIGHT"); return; }
                foreach (var e in _cart)
                    if (e.IsFitting) { Toast("ONE UPGRADE A NIGHT"); return; }
            }
            if (CartTotal() + price > Run.Money) { Toast("NOT ENOUGH MONEY"); return; }

            _cart.Add(new CartEntry { Key = key, Label = label, Price = price,
                                      IsFitting = isFitting, Buy = buy, Art = art });
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
            var paid = new List<int>();
            foreach (var e in _cart)
            {
                try { e.Buy(); _justOrdered.Add(e.Key); paid.Add(e.Price); bought++; }
                catch (InvalidOperationException) { Toast("ORDER STOPPED — " + e.Label); break; }
            }
            _cart.Clear();
            Sfx.Play("cash", 0.9f);
            ApplyBarLook();
            RebuildDayEnd();

            // Each line says what it cost, one after the other, under the account.
            for (int i = 0; i < paid.Count; i++) DropMoney(-paid[i], i);

            // THE KEY ANSWERS (2026-08-11, the author: it should grey out and say it was
            // bought, for about three seconds, then come back). A toast said the same thing
            // in a corner of the room; the key is the thing that was pressed, so the key is
            // where the answer belongs — and a control that cannot be pressed again while
            // the order is landing cannot be pressed twice by accident either.
            _checkoutUntil = Time.unscaledTime + CheckoutHold;
            RefreshCheckoutKey();
        }

        /// <summary>How long the order key stays spent before it can be used again.</summary>
        private const float CheckoutHold = 3f;
        private float _checkoutUntil = -1f;

        private void RefreshCheckoutKey()
        {
            if (_checkout == null || _checkoutLabel == null) return;
            bool spent = Time.unscaledTime < _checkoutUntil;
            var img = _checkout.GetComponent<Image>();
            var btn = _checkout.GetComponent<Button>();
            if (btn != null) btn.interactable = !spent;
            if (img != null)
                img.color = spent ? new Color(0.612f, 0.635f, 0.706f, 1f) : Color.white;
            _checkoutLabel.color = spent ? new Color(0.898f, 0.910f, 0.949f, 0.85f) : Color.white;
            // Spent, the key says so; free, the BASKET says what it says — an empty one
            // reads NOTHING PICKED, and hard-coding PLACE ORDER here would have handed the
            // player a key inviting them to order nothing the moment the hold released.
            _checkoutLabel.text = spent ? "ORDERED"
                : _cart.Count == 0 ? "NOTHING PICKED" : "PLACE ORDER";
        }

        private void StepCheckoutKey()
        {
            if (_checkoutUntil < 0f) return;
            if (Time.unscaledTime < _checkoutUntil) return;
            _checkoutUntil = -1f;
            RefreshCheckoutKey();
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
        private const float ShopSpecW = 268f;
        private RectTransform _shopSpec, _shopSpecBody;

        /// <summary>
        /// The pour, on the pointer. Nothing in it may take a raycast: the panel sits under
        /// the cursor, and a graphic that answers the pointer would read as leaving the tile
        /// underneath — which hides the panel, which hands the pointer back, many times a
        /// second. The licence tip learned this the hard way (2026-08-10).
        /// </summary>
        private void ShowShopSpec(RecipeDefinition r)
        {
            if (_shopSpec == null || _shopSpecBody == null) return;
            if (r == null) { _shopSpec.gameObject.SetActive(false); return; }
            // A CRATE IN THE MARKET IS A PAGE YOU DO NOT OWN, so its gauges read empty too
            // (2026-08-20). This is the surface the old market rule meant when it said
            // "buyable recipes show their pour on hover" — it shows what goes IN the drink,
            // which is what the purchase decision needs, and keeps the proportions for the
            // page you have actually bought. A recipe already on the menu never reaches here.
            bool unowned = Run != null && r.Locked && !Run.MenuRecipes.Contains(r);
            float h = DrawRecipeSpec(_shopSpecBody, r, dark: true, width: ShopSpecW - 20f,
                locked: unowned);
            _shopSpec.sizeDelta = new Vector2(ShopSpecW, h + 16f);
            _shopSpec.gameObject.SetActive(true);
            _shopSpec.SetAsLastSibling();
            foreach (var g in _shopSpec.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            FollowPointerWithShopSpec();
        }

        /// <summary>Hangs the spec off the cursor, turning back at the edges of the market's
        /// own panel rather than running off it.</summary>
        private void FollowPointerWithShopSpec()
        {
            // The card's gate, for the same reason — see FollowPointerWithShopCard.
            if (_shopSpec == null) return;
            if (!MarketIsUp)
            {
                if (_shopSpec.gameObject.activeSelf) _shopSpec.gameObject.SetActive(false);
                return;
            }
            if (!_shopSpec.gameObject.activeSelf) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || _dayEndPanel == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dayEndPanel, mouse.position.ReadValue(), null, out local)) return;
            const float Gap = 20f;
            Vector2 size = _shopSpec.sizeDelta;
            float halfW = _dayEndPanel.rect.width * 0.5f, halfH = _dayEndPanel.rect.height * 0.5f;
            // Under the reading card when both are up: they describe the same tile, so they
            // stack into one column rather than fighting for the same corner of the cursor.
            float drop = _shopCard != null && _shopCard.gameObject.activeSelf
                ? _shopCard.sizeDelta.y + 6f : 0f;
            float x = local.x + Gap;
            if (x + size.x > halfW) x = local.x - Gap - size.x;
            float y = local.y - Gap - drop;
            // The drop belongs to the TURNED-BACK case too. Near the foot both panels flip
            // above the cursor, and without it here the table landed back on top of the card
            // it was supposed to be stacked under (measured, 2026-08-11).
            if (y - size.y < -halfH) y = local.y + Gap + drop + size.y;
            _shopSpec.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>
        /// What the pointer is on, said on the pointer. Hiding is the whole answer for "on
        /// nothing": the card is a thing the cursor carries, so an empty one would be a box
        /// following the mouse around the shop saying nothing.
        ///
        /// The card GROWS to its text (2026-08-11). The slab it replaced was a fixed 128
        /// units with every line set to Truncate, which is how a description longer than
        /// three lines simply stopped mid-sentence.
        /// </summary>
        private void ShowShopCard(TileSpec spec)
        {
            if (_cardIdentity == null) return;
            if (spec == null)
            {
                if (_shopCard != null) _shopCard.gameObject.SetActive(false);
                return;
            }
            if (_cardMarkImg != null)
            {
                _cardMarkImg.enabled = spec.Art != null;
                _cardMarkImg.sprite = spec.Art;
            }
            // The mark alone said "a bottle"; the NAME beside it says which. The identity
            // row already carried it, but a hundred units to the right of the picture it
            // belongs to — so the two read as separate facts about the same tile.
            // WHITE AND HEAVY (2026-08-10, the author: the bottle had a mark and a grey
            // line of specifications, and nowhere did it say WHAT IT WAS). The identity row
            // was already here and already carried the name; it was set in the body face at
            // the body's weight, so it read as one more line of small print.
            // NEVER BLANK. A tile that forgot to set Identity showed the bottle's mark, a
            // grey line of specifications and no name at all — which is exactly the one
            // thing the panel exists to say. The tile's own name is always there.
            _cardIdentity.text = !string.IsNullOrEmpty(spec.Identity)
                ? spec.Identity : (spec.Name ?? "").ToUpperInvariant();
            _cardMeta.text = spec.MetaLine ?? "";
            _cardBody.text = spec.Body ?? "";
            WriteBuff(_cardBuffA, _cardBuffAIcon, spec.BuffA);
            WriteBuff(_cardBuffB, _cardBuffBIcon, spec.BuffB);

            // Every row is stacked on the one above it, and the card is cut to the last of
            // them — measured off the text itself, so a long name pushes the description
            // down instead of printing through it.
            float y = 8f;
            y += Mathf.Max(20f, _cardIdentity.preferredHeight);
            if (_cardMeta.text.Length > 0) y += RowAt(_cardMeta, y, 4f);
            else y += 2f;
            _shopCardRule.anchoredPosition = new Vector2(10f, -(y + 4f));
            y += 10f;
            if (_cardBody.text.Length > 0) y += RowAt(_cardBody, y, 0f);
            if (_cardBuffA.text.Length > 0) y += BuffAt(_cardBuffA, _cardBuffAIcon, y);
            if (_cardBuffB.text.Length > 0) y += BuffAt(_cardBuffB, _cardBuffBIcon, y);
            _shopCard.sizeDelta = new Vector2(ShopCardW, y + 10f);
            _shopCard.gameObject.SetActive(true);
            _shopCard.SetAsLastSibling();
            foreach (var g in _shopCard.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            FollowPointerWithShopCard();
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

        private static float BuffAt(Text row, Image icon, float y)
        {
            float h = RowAt(row, y, 2f);
            var irt = (RectTransform)icon.transform;
            irt.anchoredPosition = new Vector2(irt.anchoredPosition.x, -(y + 3f));
            return h;
        }

        /// <summary>
        /// Whether the pointer's two reading panels are allowed up at all. They describe
        /// MARKET TILES and nothing else, so the market being on screen is the whole
        /// condition — the night's slip is the same panel one step earlier, and a bottle's
        /// specifications hanging off the cursor over the takings describe nothing that is
        /// on that screen.
        /// </summary>
        private bool MarketIsUp => Showing(_dayEndPanel) && _dayEndStep == 1;

        /// <summary>Hangs the reading card off the cursor, turning back at the edges of the
        /// market's own panel rather than running off it.</summary>
        private void FollowPointerWithShopCard()
        {
            // PUT AWAY WITH THE SCREEN IT BELONGS TO (2026-08-19, the author: the card was
            // still following the mouse around the invoice). Every hover puts it away on
            // exit, but an exit is not always REPORTED: leaving the market by Escape or by
            // the foot key takes the panel down UNDER the pointer, which moves nothing and
            // destroys nothing, so OnPointerExit never fires — and the card outlives the
            // aisle, then comes back up with the panel on the next night's slip. The gate
            // is here, in the one thing that runs every frame, rather than at each of the
            // several ways out.
            if (_shopCard == null) return;
            if (!MarketIsUp)
            {
                if (_shopCard.gameObject.activeSelf) _shopCard.gameObject.SetActive(false);
                return;
            }
            if (!_shopCard.gameObject.activeSelf) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || _dayEndPanel == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dayEndPanel, mouse.position.ReadValue(), null, out Vector2 local)) return;
            const float Gap = 18f;
            Vector2 size = _shopCard.sizeDelta;
            float halfW = _dayEndPanel.rect.width * 0.5f, halfH = _dayEndPanel.rect.height * 0.5f;
            float x = local.x + Gap;
            if (x + size.x > halfW) x = local.x - Gap - size.x;
            float y = local.y - Gap;
            if (y - size.y < -halfH) y = local.y + Gap + size.y;
            _shopCard.anchoredPosition = new Vector2(x, y);
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

        private RectTransform _liquorHead, _kegHead, _garnishHead;

        /// <summary>
        /// The header a crate stands under, made on demand: the one the offers already
        /// built, or a fresh one when this aisle has nothing for sale tonight but something
        /// waiting behind a star. Null when the aisle is genuinely finished — then no sign
        /// is drawn and none is wanted.
        /// </summary>
        private RectTransform AisleSign(TycoonRun run, RectTransform existing,
            System.Func<IngredientCard, bool> belongs, bool booze, string title)
        {
            if (existing != null) return existing;
            foreach (var g in run.GatedStock())
            {
                if (IngredientCategories.IsAlcoholic(g.Card.Info?.Category, g.Card.Type) != booze) continue;
                if (!belongs(g.Card)) continue;
                return ShopSection(title);
            }
            return null;
        }

        /// <summary>
        /// The sealed crate for ONE aisle: how many of its lines are still behind a star,
        /// and the nearest of those stars. Draws nothing when the aisle is finished, which
        /// is the whole signal — a shelf with no crate at its foot has nothing left to give.
        /// </summary>
        private void SectionGate(TycoonRun run, System.Func<IngredientCard, bool> belongs,
            bool booze, string noun, RectTransform grid)
        {
            if (grid == null) return;
            int locked = 0;
            double next = double.MaxValue;
            // What the starless half of the aisle is waiting for, in ITS OWN words. The
            // locks already write these — "SERVE ECE WHAT THEY ASK FOR", "NEEDS THE 2-LINE
            // TOWER" — and the crate has been throwing them away and printing a star
            // instead, which is the one thing they are guaranteed not to be waiting for.
            var asked = new List<string>();
            foreach (var g in run.GatedStock())
            {
                if (IngredientCategories.IsAlcoholic(g.Card.Info?.Category, g.Card.Type) != booze) continue;
                if (!belongs(g.Card)) continue;
                locked++;
                // NaN is a line waiting on a person or on the room, not on a rung — it counts
                // as held back but has no number to pull the aisle's hint towards.
                if (!double.IsNaN(g.Stars)) { if (g.Stars < next) next = g.Stars; }
                else if (!string.IsNullOrEmpty(g.Sentence) && !asked.Contains(g.Sentence))
                    asked.Add(g.Sentence);
            }
            if (locked == 0) return;
            // NOTHING HERE IS WAITING FOR A STAR (2026-08-19). This used to fall back to the
            // bar's CURRENT standing, so an aisle held entirely behind the draught tower
            // promised "get 5.0 stars and more of these show up here" to a bar that already
            // had five — a sealed crate telling the player to go and do the one thing that
            // would change nothing. It says what the locks say now.
            bool starless = next == double.MaxValue;
            string wanted = asked.Count > 0 ? string.Join(" · ", asked) : "";
            var was = _cardTarget;
            _cardTarget = grid;
            AddTile(new TileSpec
            {
                Name = locked + " more waiting",
                Money = starless ? locked.ToString() : next.ToString("0.0"),
                GateNote = starless ? "STILL LOCKED" : null,
                State = TileState.Sealed,
                Identity = starless
                    ? "MORE " + noun.ToUpperInvariant() + "S TO EARN"
                    : "MORE " + noun.ToUpperInvariant() + "S AT " + next.ToString("0.0") + " STARS",
                MetaLine = locked + " " + (locked == 1 ? noun : noun + "s")
                           + " the van will not bring you yet",
                Body = starless
                    ? (wanted.Length > 0 ? wanted : "These are earned, not bought.")
                    : "Get " + next.ToString("0.0") + " stars and more of these show up "
                      + "here.",
                BuffA = new Buff(BuffKind.Bad, starless
                    ? (wanted.Length > 0 ? wanted : "Earned, not bought")
                    : "Needs " + next.ToString("0.0")
                      + " stars · you have " + run.Rating.Average.ToString("0.0")),
            });
            _cardTarget = was;
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
            band.gameObject.AddComponent<Image>().color = ShopViceDeep;
            var pip = NewRect("Pip", band);
            Place(pip, new Vector2(0, 0.5f), new Vector2(6, 18), new Vector2(10, 0));
            pip.gameObject.AddComponent<Image>().color = ShopViceLit;
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
        // Top-left: one line per service event — what was ordered, what the judge said and
        // WHY it scored the way it did (match, spec, fill, craft), what it paid, and the
        // stars it left. The author's in-play instrument for the balance work: the sim
        // report says what 200 runs did; this says what THIS serve just did.
        //
        // Folded behind its own key since 2026-08-19 (the author: the sheet sat over the
        // room and hid the scene). Closed by default; the lines keep accumulating while it
        // is shut, so opening it shows the night so far, not a blank page.

        private Text _serviceLog;
        private RectTransform _serviceLogPanel;
        private bool _serviceLogOpen;   // closed by default — the sheet covers the room
        private readonly List<string> _serviceLogLines = new List<string>();
        private const int ServiceLogMax = 9;

        private void BuildServiceLog(RectTransform root)
        {
            // The key stays put under the fascia; only the sheet below it comes and goes.
            NewButton(root, "LOG", new Vector2(0, 1), new Vector2(44, 20),
                new Vector2(10, -66), UITheme.Night[2], ToggleServiceLog);

            var panel = _serviceLogPanel = NewRect("ServiceLog", root);
            Place(panel, new Vector2(0, 1), new Vector2(430, 150), new Vector2(10, -90));
            panel.pivot = new Vector2(0, 1);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.55f);
            bg.raycastTarget = false;
            _serviceLog = NewText("Lines", panel, _body, 8, TextAnchor.UpperLeft, UITheme.TextSecondary);
            _serviceLog.supportRichText = true;
            Stretch(_serviceLog.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 4), new Vector2(-6, -4));
            panel.gameObject.SetActive(_serviceLogOpen);
        }

        private void ToggleServiceLog()
        {
            _serviceLogOpen = !_serviceLogOpen;
            if (_serviceLogPanel != null) _serviceLogPanel.gameObject.SetActive(_serviceLogOpen);
        }

        private void LogService(string line)
        {
            if (_serviceLog == null) return;
            _serviceLogLines.Insert(0, line);
            while (_serviceLogLines.Count > ServiceLogMax)
                _serviceLogLines.RemoveAt(_serviceLogLines.Count - 1);
            _serviceLog.text = string.Join("\n", _serviceLogLines);
        }

        /// <summary>The visit's score for the log, in marks a pixel font can actually draw.
        /// It used to be U+2605 — the same glyph that was printing five empty boxes over the
        /// payment float, and the same one this project already replaces with an asterisk
        /// wherever a licence name carries it (2026-08-11).</summary>
        private static string LogStars(double satisfaction) =>
            new string('*', Mathf.Clamp(Mathf.RoundToInt((float)satisfaction * 5f), 0, 5));

        /// <summary>The judge's verdict, said as one log line with its reasons.</summary>
        private void LogVerdict(CustomerVisit visit, ServiceVerdict verdict)
        {
            string ordered = visit.IdInspected ? visit.Order.Wanted.Name.ToUpperInvariant() : "?";
            string made = visit.Served != null ? visit.Served.Name.ToUpperInvariant() : "NOTHING NAMED";
            string col = verdict.Match == OrderMatch.Exact ? "8CE28C"
                : verdict.Match == OrderMatch.Close ? "F5C97B" : "F27D8A";
            var why = new List<string>();
            if (verdict.Match == OrderMatch.Wrong) why.Add($"made {made}");
            // A Close line names its own reason or it is a mystery (2026-08-14). The grade is
            // "their drink, out of tolerance", and the glass usually matches no recipe at all,
            // so `made` would read NOTHING NAMED and the reasons list would come back empty —
            // a serve that paid less than the last one with nothing on the line to say why.
            if (verdict.Match == OrderMatch.Close) why.Add("measures off");
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
                float dur = open ? 0.42f : 0.32f;   // a board this size does not snap
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
            // THE DIM CLOSES IT (2026-08-11, the author, reversing the earlier ruling that
            // only the X may: "kimlikteki gibi assetin dışına arka plana tıklandığında
            // otomatik kapanmalı ya da esc"). It is the licence's own behaviour, so the two
            // sheets now shut the same way, and it is the reason the X could go. The board
            // itself still swallows its clicks — BoardCatch below — so reading the page
            // cannot close the page.
            var scrimBtn = _bookPanel.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(() => { if (_bookOpen) ToggleRecipeBook(); });
            // The book also outranks the back-bar flow (canvas 12):
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

            // No X (2026-08-11). The dim behind the board closes it and so does Escape, which
            // is what the licence has always done — and a corner button that duplicates a
            // gesture the player already has is a button that has to be found first.

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
        private const float BkPaperW = 0.655f;
        private const float BkPaperCX = -0.015f;

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

        /// <summary>How tall the board is, and THE TWO RULES EVERYTHING ON IT SITS ON.
        /// The old board had a reading wherever its box happened to leave room, which is
        /// what "yazılar hizalanmamış" was describing (2026-08-14): captions at three
        /// different heights, values at two more. There are two lines now — the small
        /// upper one for what a reading IS, the lower one for what it SAYS — and every
        /// item on the beam is placed against one of them, left to right.</summary>
        private const float TopBarH = 54f, CapY = 12f, ReadY = -9f;

        // The display glass — "not black: a display's dark is the panel's own colour
        // seen through a tint" — is baked into ChromeArt.Well now, the one place both
        // instruments get their floor from, so it cannot fork.
        // 32 — the 3D star's own native size, drawn at 1× (2026-08-19, the author:
        // "Yıldızlarda 3 boyutlu yıldız iconlarından olsun"): Items/star3d.png is a
        // PixelLab take quantized onto the Amber/Malt ladder, generated AT 32 because
        // that is the size the row draws at. The earlier 32 was the flat 16px star at
        // 2×; the size held, the drawing under it changed. Size history: cut to 16 once
        // ("row climbed into its caption"), restored ("yıldız barı ortalasın ve boyutu
        // büyütülsün") by giving the standing its own block instead.
        private const float StarSize = 32f, StarGap = 34f;

        /// <summary>A full-width band `h` units tall, `down` units below the parent's top
        /// edge. `Hairline` can only sit ON an edge and is one unit thick; a beam is built
        /// from a few bands stacked down its face, which is what gives it a top the room
        /// lights and a front that falls away from it.</summary>
        private Image Band(RectTransform parent, string name, float down, float h, Color c)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, h);
            rt.anchoredPosition = new Vector2(0, -down);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
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

            // EVERY CARD ITS OWN HEIGHT (2026-08-11, the author: "bu kutular sıkıştırılmalı,
            // çok geniş yer kaplıyorlar"). A GridLayoutGroup has ONE cell size, so the whole
            // section was cut to its longest spec: a Long Island is seven pours, and while it
            // sat in the section every Gin & Tonic beside it got a card three times the height
            // of its two lines, most of it blank. That is what the screenshot was of.
            //
            // So the cards are packed by hand into two columns, each card measured from its
            // own rows, and each new one goes to whichever column is currently SHORTER. That
            // is the standard masonry answer, and it keeps the two sides level as well as
            // tight — filling left-then-right in order would leave one column hanging.
            // TWO COLUMNS, and the row earns its width instead of the card being widened for
            // it (2026-08-20, the author twice: first "her alkole daha fazla yer", then
            // "ekranda yan yana 2 kart durabilir"). The one-column draft answered the first
            // ask the expensive way — it halved how many drinks the page could hold, which is
            // what a catalogue is FOR. What buys the room back is the gauge itself: 72 pixels
            // of sight glass says what a "45–65%" caption used to spend a text column saying.
            const float ColGap = 12f, RowGap = 10f, HeadH = 30f, Air = 14f;
            float fullW = BkW * BkPaperW - 44f;
            float cellW = fullW / 2f - ColGap * 0.5f;

            var sec = NewRect("Sec", _bookList);
            var secLayout = sec.gameObject.AddComponent<LayoutElement>();

            var colH = new float[2];
            foreach (var r in rs)
            {
                float spec = 0;
                foreach (var row in RecipeSpecRows(r, locked: lockedRows))
                    spec += row.Hint ? SpecHintH : SpecRowH;
                if (lockedRows) spec += SpecRowH;         // the star gate takes its own line
                float h = HeadH + spec + Air;

                int col = colH[0] <= colH[1] ? 0 : 1;
                var card = BookRow(sec, r, lockedRows, run, cellW);
                card.anchorMin = card.anchorMax = new Vector2(0, 1);
                card.pivot = new Vector2(0, 1);
                card.sizeDelta = new Vector2(cellW, h);
                card.anchoredPosition = new Vector2(col * (cellW + ColGap), -colH[col]);
                colH[col] += h + RowGap;
            }
            secLayout.preferredHeight = Mathf.Max(colH[0], colH[1]) - RowGap;
        }

        /// <summary>One recipe: the glass drawn from its own bands, the name, how it is
        /// worked, and the pour — or, for a locked one, what it is waiting behind.</summary>
        private RectTransform BookRow(RectTransform parent, RecipeDefinition r, bool lockedRow,
            TycoonRun run, float cellW)
        {
            var row = NewRect($"R_{r.Id}", parent);
            // EACH RECIPE IN ITS OWN BOX (2026-08-11, the author: "açıkta olunca karmaşıklık
            // oluşuyor"). They were printed lines separated by a hairline, which is the right
            // treatment for a form and the wrong one for a catalogue: a spec card is five
            // stacked pours, and five of those under thin rules read as one long column of
            // numbers. A card gives each drink an edge, and the eye can stop at it.
            var rowImg = row.gameObject.AddComponent<Image>();
            rowImg.sprite = ChromeArt.Card();
            rowImg.type = Image.Type.Sliced;
            rowImg.color = lockedRow ? new Color(0.93f, 0.90f, 0.82f) : new Color(0.99f, 0.97f, 0.90f);
            var lift = row.gameObject.AddComponent<Shadow>();
            lift.effectColor = new Color(0.24f, 0.15f, 0.06f, lockedRow ? 0.18f : 0.30f);
            lift.effectDistance = new Vector2(2, -2);

            var icon = NewRect("I", row);
            Place(icon, new Vector2(0, 0.5f), new Vector2(40, 40), new Vector2(10, 0));
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = DrinkIcon.For(r, _bootstrap.Glassware);
            img.preserveAspect = true; img.raycastTarget = false;
            img.enabled = img.sprite != null;
            if (lockedRow) img.color = new Color(1, 1, 1, 0.4f);

            var name = NewText("N", row, _display, 16, TextAnchor.UpperLeft,
                lockedRow ? new Color(0.45f, 0.36f, 0.28f) : new Color(0.13f, 0.08f, 0.05f));
            Stretch(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(58, -30), new Vector2(-8, -8));
            name.text = r.Name.ToUpperInvariant();

            // The same spec card the licence draws, in the book's own ink (2026-08-02):
            // exact shares, the bottles' own art, and the stocked ones lit.
            var body = NewRect("Spec", row);
            float bodyW = cellW - 70f;
            Place(body, new Vector2(0, 1), new Vector2(bodyW, 10), Vector2.zero);
            body.pivot = new Vector2(0, 1);
            body.anchoredPosition = new Vector2(58, -30);
            // The book prints the lock's own sentence too, so a page earned from a person
            // does not quietly claim to be waiting for stars.
            var rowLock = lockedRow ? run.RecipeUnlock(r) : null;
            string note = rowLock != null && !string.IsNullOrEmpty(rowLock.Sentence)
                ? "OPENS: " + rowLock.Sentence : null;
            // A PAGE THE BAR DOES NOT OWN KEEPS ITS POUR (2026-08-20, the author: "sahip
            // olmadığın tariflerin yapımı kilitli gözükmeli"). The bottles still show — the
            // shopping list is how the book works as a progression map, and the shop tile
            // already says whether the shelf could pour it — but every gauge reads empty,
            // because the proportions ARE the making and the making is what is locked.
            DrawRecipeSpec(body, r, dark: false, width: bodyW, note: note, locked: lockedRow);
            return row;
        }

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
            // 420, not 300: the dev rows say what they DO ("close now, open the market"),
            // and a button whose caption does not fit is a button with no caption.
            // 360, not 320: the last-call skip is a ninth row and a row that does not fit the
            // panel is a button nobody can press.
            Place(_settingsPanel, new Vector2(1, 1), new Vector2(420, 360), new Vector2(-16, -58));
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
            // NEW RUN LIVES HERE NOW (2026-08-14, the author: "new run yazısını ayarların
            // içine taşı"). It was a key on the board, one thumb from the things pressed all
            // night, and it throws the night away — this is where a thing like that belongs.
            // It is the same verb the fresh-start dev row already called, so it takes that
            // row's place rather than becoming a tenth button that does the same thing.
            SettingsRow(3, "NEW RUN — day 1, empty bar", () =>
            { _bootstrap.StartNewRun(null); ToggleSettings(); });
            // THE WORKBENCH IS NOT A SETTING (2026-08-14, the author: "dev tool'u daha
            // verimli bir panele dönüştür bu şekilde seçmek zor oluyor. Ayarlarla dev toolu
            // ayır"). Five dev rows and three settings shared one 420-wide stack, so the
            // volume lived a thumb away from "throw this run away and jump two weeks", and
            // every dev row had to spell its whole job into a caption because there was
            // nowhere else to say it. Settings keeps what a player changes; everything a
            // DEVELOPER does moved to its own bench, which has room to group and to explain.
            SettingsRow(4, "DEV TOOLS — the bench, and the lineup table", () =>
            {
                ToggleSettings();
                ToggleDevBench();
            });

            _settingsMotion = SettingsRow(2, "MOTION", () =>
            {
                Motion.Reduced = !Motion.Reduced;
                Sfx.Play("click");
                RefreshSettings();
            });

            _settingsPanel.gameObject.SetActive(false);
        }

        // ── THE DEV BENCH (2026-08-14) ──────────────────────────────────────────
        //
        // The author: "dev tool'u daha verimli bir panele dönüştür bu şekilde seçmek zor
        // oluyor. Ayarlarla dev toolu ayır." And, of the lineup: "çok detaylı bir tablo
        // yapıp dev tool'a oyun içinde koy."
        //
        // Two panes, because a bench and a reference are different jobs. On the left, the
        // VERBS — grouped under headings, each with room to say what it does under its own
        // name instead of inside it. On the right, the LINEUP: every rung of the star track
        // with the pages that open on it and the bottles that come with them, read live off
        // the run so it answers what a generated markdown cannot — what is owned tonight,
        // what tonight's standing has already opened, and what is still sealed.
        //
        // It is the character guide's sibling on purpose: same size, same frame, same scroll,
        // same close key. A second dev sheet that invented its own shape would be a fifth
        // dialect two days after the fourth was closed (GDD 16 §2).

        private RectTransform _devPanel, _devRows;
        private Text _devStanding;

        private void ToggleDevBench()
        {
            if (_devPanel == null) return;
            bool show = !_devPanel.gameObject.activeSelf;
            if (show) { CloseId(); RefreshDevBench(); }
            _devPanel.gameObject.SetActive(show);
        }

        private void BuildDevBench(RectTransform root)
        {
            _devPanel = NewRect("DevBench", root);
            Place(_devPanel, new Vector2(0.5f, 0.5f), new Vector2(1180, 640), new Vector2(0, 6));
            var canvas = _devPanel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 25;                 // above the guide (24) and the market (22)
            _devPanel.gameObject.AddComponent<GraphicRaycaster>();
            var bg = _devPanel.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.985f);
            bg.raycastTarget = true;
            Frame(_devPanel, 2f, UITheme.Cyan[3]);    // cyan, not amber: this is not the game

            var title = NewText("T", _devPanel, _display, 16, TextAnchor.MiddleLeft, UITheme.Cyan[3]);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(600, 22), new Vector2(20, -18));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.text = "DEV BENCH";

            _devStanding = NewText("S", _devPanel, _body, 8, TextAnchor.MiddleRight, UITheme.Cream[2]);
            Place(_devStanding.rectTransform, new Vector2(1, 1), new Vector2(560, 12), new Vector2(-20, -22));
            _devStanding.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── the left rail: the verbs ────────────────────────────────────────
            int slot = 0;
            DevHeading(ref slot, "THE RUN");
            DevKey(ref slot, "NEW RUN", "day 1, empty bar",
                () => { _bootstrap.StartNewRun(null); ToggleDevBench(); });
            DevKey(ref slot, "MIDGAME", "day 12, stocked",
                () => { _bootstrap.StartNewRun(null); Run.DevPreset(1); ApplyBarLook(); ToggleDevBench(); });
            DevKey(ref slot, "ENDGAME", "late run, full shelf",
                () => { _bootstrap.StartNewRun(null); Run.DevPreset(2); ApplyBarLook(); ToggleDevBench(); });

            DevHeading(ref slot, "THE CLOCK");
            DevKey(ref slot, "SKIP TO DAY END", "close now, open the market", () =>
            {
                if (Run == null || Run.Phase != TycoonPhase.DayOpen) { Toast("NOT MID-DAY"); return; }
                _flow?.CloseFlow();
                CloseId();
                Run.DevSkipToDayEnd();
                ToggleDevBench();
            });
            DevKey(ref slot, "SKIP TO THE LAST CALL", "jump to the night, then run it out",
                DevJumpToLastCall);

            DevHeading(ref slot, "THE PEOPLE");
            DevKey(ref slot, "THE ROOM", "every drinker, papers and star",
                () => { ToggleDevBench(); ToggleGuide(); });

            // ── the right pane: the lineup ──────────────────────────────────────
            var head = NewText("H", _devPanel, _shop, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
            Place(head.rectTransform, new Vector2(0, 1), new Vector2(820, 12), new Vector2(348, -46));
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            head.text = "PRICE  NAME                          HOW IT IS MADE        WHAT IT ASKS FOR";

            var view = NewRect("LineupView", _devPanel);
            Place(view, new Vector2(0, 1), new Vector2(820, 556), new Vector2(340, -58));
            view.pivot = new Vector2(0, 1);
            view.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.02f);
            view.gameObject.AddComponent<RectMask2D>();
            _devRows = NewRect("Rows", view);
            _devRows.anchorMin = new Vector2(0, 1); _devRows.anchorMax = Vector2.one;
            _devRows.pivot = new Vector2(0.5f, 1);
            _devRows.offsetMin = Vector2.zero; _devRows.offsetMax = Vector2.zero;
            var layout = _devRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            // TRUE, unlike the guide's: its rows carry a photo and size themselves, these are
            // single lines of 8px type that must be told their height. With it false the
            // LayoutElement is ignored and every row took a Text's default rect — measured at
            // a hundred pixels a line, four rows to a screen for a table meant to be read in
            // one pass.
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var fit = _devRows.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = _devRows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.inertia = false;

            NewButton(_devPanel, "CLOSE", new Vector2(0, 0), new Vector2(300, 32),
                new Vector2(20, 12), UITheme.Cyan[3], () => ToggleDevBench());
            _devPanel.gameObject.SetActive(false);
        }

        private const float DevRailX = 20f, DevRailW = 300f;

        private void DevHeading(ref int slot, string text)
        {
            var t = NewText("DH", _devPanel, _shop, 8, TextAnchor.LowerLeft, UITheme.Cyan[3]);
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(DevRailW, 16),
                new Vector2(DevRailX, -52f - slot * 26f));
            t.rectTransform.pivot = new Vector2(0, 1);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = text;
            slot++;
        }

        /// <summary>One verb: its NAME on the key, and what it does underneath it rather than
        /// crammed inside it. That is the whole reason this panel exists.</summary>
        private void DevKey(ref int slot, string name, string what, Action onClick)
        {
            var row = NewRect("DK_" + name, _devPanel);
            Place(row, new Vector2(0, 1), new Vector2(DevRailW, 22),
                new Vector2(DevRailX, -52f - slot * 26f));
            row.pivot = new Vector2(0, 1);
            var btn = row.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var face = NewRect("Face", row);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(row, UITheme.Night[3], btn, face);
            var label = NewText("L", face, _body, 8, TextAnchor.MiddleLeft, UITheme.TextPrimary);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(8, KeyPlate.Throw), new Vector2(-8, 0));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = name;
            slot++;

            var note = NewText("N_" + name, _devPanel, _body, 8, TextAnchor.UpperLeft, UITheme.Cream[2]);
            Place(note.rectTransform, new Vector2(0, 1), new Vector2(DevRailW - 8f, 14),
                new Vector2(DevRailX + 8f, -52f - slot * 26f + 8f));
            note.rectTransform.pivot = new Vector2(0, 1);
            note.horizontalOverflow = HorizontalWrapMode.Overflow;
            note.text = what;
            slot++;
        }

        /// <summary>
        /// THE LINEUP, RUNG BY RUNG, read off the live run.
        ///
        /// `LastCall → Write Balance Guide` already writes the numbers a spreadsheet wants;
        /// this is the half a document cannot hold — what THIS bar owns tonight, what its
        /// standing has already opened, and what the next rung is still holding. Both halves
        /// exist because they answer different questions: the file says how the game is
        /// priced, this says where this run has got to.
        /// </summary>
        private void RefreshDevBench()
        {
            if (_devRows == null) return;
            for (int i = _devRows.childCount - 1; i >= 0; i--)
                Destroy(_devRows.GetChild(i).gameObject);
            var run = Run;
            if (run == null) { _devStanding.text = "no run"; return; }

            double stars = run.Rating.Average;
            _devStanding.text = $"standing {stars:0.00}★ · day {run.Day} · ${run.Money} · "
                              + $"{run.MenuRecipes.Count} pages on the menu · "
                              + $"{run.Shelf.Bottles.Count} bottles on the wall";

            // Every page in the book and every bottle in the catalogue, filed under the rung
            // that opens it. The bottle's rung is its own lock's answer, so the table cannot
            // disagree with the shop — both ask the same object.
            var rungs = new SortedDictionary<double, List<(string line, Color ink)>>();
            void File(double rung, string line, Color ink)
            {
                if (!rungs.TryGetValue(rung, out var list))
                    rungs[rung] = list = new List<(string, Color)>();
                list.Add((line, ink));
            }

            foreach (var r in run.AllRecipes)
            {
                double gate = run.RecipeStarGate(r);
                bool owned = false;
                foreach (var m in run.MenuRecipes) if (m.Id == r.Id) { owned = true; break; }
                var bands = new StringBuilder();
                foreach (var b in r.RatioRequirements)
                {
                    if (bands.Length > 0) bands.Append(", ");
                    bands.Append(b.IsStyleBand ? b.Style : b.Type.ToString());
                    bands.Append($" {b.MinRatio:P0}-{b.MaxRatio:P0}");
                    if (b.MinTier > 1) bands.Append($" T{b.MinTier}+");
                }
                string how = r.Prep.ToString().ToUpperInvariant()
                           + (string.IsNullOrEmpty(r.GlassId) ? "" : " · " + r.GlassId);
                File(gate, $"${run.RecipePrice(r),-4} {r.Name,-28} {how,-20} {bands}",
                    owned ? UITheme.Lime[3] : run.Money >= run.RecipePrice(r) && stars + 1e-9 >= gate
                        ? UITheme.TextPrimary : UITheme.Cream[2]);
            }

            foreach (var card in run.CatalogueBottles)
            {
                if (card.Info == null) continue;
                double rung = card.Info.Unlock != null
                    ? card.Info.Unlock.StarsWanted
                    : Market.RequiredStars(card.Info.Tier, card.Info.Price);
                if (double.IsNaN(rung)) rung = 0.0;   // a bottle earned from a person: file it at the top
                bool owned = run.Shelf.Find(card.Id) != null;
                File(rung, $"${card.Info.Price,-4} {card.Name,-28} "
                         + $"{card.Info.Category + " · tier " + card.Info.Tier,-20} "
                         + (owned ? "ON THE WALL" : "stock"),
                    owned ? UITheme.Lime[3] : UITheme.Cream[2]);
            }

            foreach (var rung in rungs)
            {
                bool reached = stars + 1e-9 >= rung.Key;
                // A SEALED RUNG IS THE MOST INTERESTING ROW ON THE TABLE and it was drawn in
                // the beam's own shade on a black panel — invisible, so the reader saw a gap
                // between two blocks and no reason for it. Dimmer than an open rung, never
                // dimmer than the rows under it.
                var header = NewText("Rung", _devRows, _shop, 8, TextAnchor.MiddleLeft,
                    reached ? UITheme.PrimaryAction : UITheme.Cyan[3]);
                var hr = header.rectTransform;
                hr.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
                header.horizontalOverflow = HorizontalWrapMode.Overflow;
                header.text = $"  ★ {rung.Key:0.0}   {rung.Value.Count} LINES"
                            + (reached ? "   — OPEN" : "   — SEALED");

                foreach (var (line, ink) in rung.Value)
                {
                    var row = NewText("L", _devRows, _shop, 8, TextAnchor.MiddleLeft, ink);
                    row.rectTransform.gameObject.AddComponent<LayoutElement>().preferredHeight = 13f;
                    row.horizontalOverflow = HorizontalWrapMode.Overflow;
                    row.text = "    " + line;
                }
            }
        }

        /// <summary>The last-call jump, lifted out of the settings stack unchanged.</summary>
        private void DevJumpToLastCall()
        {
            if (Run == null || Run.Phase != TycoonPhase.DayOpen) { Toast("NOT MID-DAY"); return; }
            if (Run.Story == null) { Toast("THIS RUN HAS NO STORY"); return; }
            if (Run.LastCustomer != null) { Toast("THEY ARE ALREADY AT THE BAR"); return; }
            _flow?.CloseFlow();
            CloseId();

            // THE DAY JUMPS TOO (2026-08-14): looking at a beat two weeks out used to be two
            // weeks of pressing things. `DevJumpToNight` winds the calendar; what that skips,
            // and why it skips it rather than playing the nights for real, is written there.
            int skipped = Run.DevJumpToNight(Run.Story.DueDay);
            if (skipped > 0) ApplyBarLook();
            if (!Run.Story.IsDueOn(Run.Day))
            {
                ToggleDevBench();
                Toast("NOTHING WRITTEN AHEAD — LAST WAS "
                      + BarCalendar.Label(Run.Story.DueDay).ToUpperInvariant());
                return;
            }
            // The REAL clock and the REAL verb, the same bargain DevSkipToDayEnd strikes:
            // everyone still seated storms off exactly as they would have, the rent and the
            // rating land where they always do. What is skipped is the waiting, never the
            // rules — a shortcut that lied would measure a game nobody plays.
            for (int guard = 0; guard < 20000 && Run.LastCustomer == null
                 && Run.Phase == TycoonPhase.DayOpen; guard++)
                Run.Tick(0.25);
            ToggleDevBench();
            Toast(Run.LastCustomer != null
                ? "LAST CALL — " + Run.LastCallBeat.Who.Name.ToUpperInvariant() + " IS AT THE BAR"
                : "THE NIGHT ENDED WITHOUT THEM");
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
            note.text = "Stars = how many you need before they walk in";

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
            // IN THE ORDER THEY ARRIVE (the author, 2026-08-10). The roster's job is
            // "who drinks here and when", and read down the star gate it answers that in
            // one pass: everyone already in the room at the top, then the next person the
            // bar has to earn, and so on. In generation order it answered nothing — the
            // gates ran 0, 0, 1.5, 0, 2.5, 1.5 and the reader had to sort it by eye.
            // List.Sort is not stable, so the generation index is the tiebreaker rather
            // than a hope: a batch of characters gated alike still reads as the batch it
            // was drawn in.
            var roster = new List<PatronLook>(_looks);
            var drawnOrder = new Dictionary<PatronLook, int>();
            for (int i = 0; i < _looks.Count; i++) drawnOrder[_looks[i]] = i;
            roster.Sort((a, b) =>
            {
                int byGate = a.Stars.CompareTo(b.Stars);
                return byGate != 0 ? byGate : drawnOrder[a].CompareTo(drawnOrder[b]);
            });
            foreach (var look in roster)
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
            Place(row, new Vector2(0.5f, 1), new Vector2(396, 30), new Vector2(0, -24f - index * 34f));
            var btn = row.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            // THE ONE KEY (GDD 16 §2). These were bare rects that did not even press — the
            // fourth dialect, and the one the author named first: a menu of things you click
            // where nothing answers the click.
            var face = NewRect("Face", row);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(row, UITheme.Night[3], btn, face);
            // THE LABEL WAS TAKEN AND NEVER WRITTEN (2026-08-10). Only the three settings
            // rows had text, because RefreshSettings assigns theirs afterwards — every dev
            // button was a blank slab you had to have written to know what it did.
            var text = NewText("L", face, _body, 8, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(8, KeyPlate.Throw), new Vector2(-8, 0));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = label;
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
            _bannerText.text = "THE BAR IS CLOSED\nthree days losing money — NEW RUN to try again";
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

            // THE PAPERS BELONG TO THE FACE, NOT TO THE ARCHETYPE (the author, 2026-08-10:
            // the licence and the guide disagreed). A regular's name used to come out of the
            // archetype's pool while their PICTURE came from the look — so the card said
            // "Marguerite" over a portrait the guide calls Marilou Cabrera, and reading a
            // customer became impossible on purpose. The look is the person now: it carries
            // the photo, the name, the age and the citizenship, and the archetype keeps only
            // what it is actually about — how they came in, and how well you know them.
            var idPapers = PapersFor(idLook);
            string idFullName = NameOn(visit, idLook).ToUpperInvariant();
            // Bold: the name is the headline of the document, and it was printing at the
            // same weight as the age on the rule under it.
            _idName.text = "<b>" + idFullName + "</b>";
            _idAgeFrom.text = (idPapers != null ? idPapers.Age : reg.Age).ToString();
            _idCitizen.text = (idPapers != null ? idPapers.Country : reg.Hometown).ToUpperInvariant();
            _idNumber.text = LicenceNumber(idLook, idFullName);
            if (_idFlag != null)
            {
                _idFlag.sprite = idPapers != null ? ItemArt.Load("fl_" + idPapers.Iso) : null;
                // A citizenship with no flag drawn shows nothing rather than a white box.
                _idFlag.enabled = _idFlag.sprite != null;
            }
            // THE TWO DATA CELLS. The count is per FACE, not per archetype: this card says
            // Miles Corrigan over Miles Corrigan's photograph, so "how many times" has to
            // mean how many times HE came in, which is what the departure log books.
            var rec = LogFor(idLook);
            _idVisitCount.text = (rec.Visits + 1).ToString();     // this one counts as they sit
            _idRel.text = rec.Visits == 0
                ? "FIRST TIME"
                : reg.Relationship.ToString().ToUpperInvariant();

            // What THEY make of US, in the stars they have actually left. Somebody who has
            // not rated the bar yet KEEPS THE ROW — five grey stars and a question mark —
            // because a blank box reads as a field that does not exist, while an empty row
            // of stars reads as a verdict not yet given, which is the true state.
            bool rated = rec.Ratings > 0;
            double avg = rated ? rec.Stars / rec.Ratings : 0.0;
            _idRates.text = rated ? avg.ToString("0.0") : "?";
            _idRates.color = rated ? UITheme.Night[1] : UITheme.Night[3];
            for (int i = 0; i < _idStars.Length; i++)
            {
                // A HALF STAR IS DRAWN AS A HALF (the author, 2026-08-11: "kimlikte yarım
                // yıldız tam yıldız olarak gözüküyor"). Lighting the whole star from the
                // halfway mark printed 2.5 and 3.0 as the same row, which is the one thing
                // this row exists to tell apart. The top bar has always drawn the standing as
                // a continuous fill — the licence does now too, star by star.
                float fill = rated ? Mathf.Clamp01((float)avg - i) : 0f;
                _idStars[i].color = new Color(0.62f, 0.58f, 0.50f, rated ? 0.55f : 0.35f);
                _idStarFills[i].fillAmount = fill;
                _idStarFills[i].enabled = fill > 0.001f;
            }

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
            foreach (Transform old in _idPrefRow) Destroy(old.gameObject);
            int chips = 0;
            foreach (var g in visit.Order.Garnishes)
                chips += PrefChip(PrefArt.ForPreparation(g.Id), g.Name.ToUpperInvariant());
            // (The SHAKEN HARD chip retired 2026-08-11: the method is the recipe's demand
            // now, printed where the recipe is — the spec panel and the book.)
            // No fill chip (the author, 2026-08-02): nobody demands a fill any more — the
            // only fill rule is the house floor, and it lives in the judge, not the licence.
            // A licence says NONE in an empty endorsements field rather than leaving it
            // blank, because blank means "not filled in" and NONE means "there are none".
            _idIntent.text = chips == 0 ? "NONE  ·  SERVE IT CLEAN" : "";
        }

        /// <summary>
        /// One endorsement, drawn the way a licence draws its categories: a bordered cell
        /// with the pictogram and the word side by side, not a picture with a caption
        /// floating under it. Returns 1 so the caller can count.
        /// </summary>
        private int PrefChip(Sprite icon, string label, RectTransform host = null)
        {
            const float CellH = 38f, IconBox = 26f;
            var chip = NewRect("Pref", host ?? _idPrefRow);
            var plate = chip.gameObject.AddComponent<Image>();
            plate.color = new Color(0.98f, 0.97f, 0.93f, 1f);
            plate.raycastTarget = false;
            Frame(chip, 2f, new Color(0.42f, 0.39f, 0.34f, 1f));

            var iconRt = NewRect("I", chip);
            Place(iconRt, new Vector2(0, 0.5f), new Vector2(IconBox, IconBox), new Vector2(7, 0));
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
            img.enabled = icon != null;

            var t = NewText("L", chip, _body, 8, TextAnchor.MiddleLeft, UITheme.Night[1]);
            Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(200, 12),
                new Vector2(IconBox + 12f, 0));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = label;

            var le = chip.gameObject.AddComponent<LayoutElement>();
            // The measured cell: icon gutter, the word at 6.7 points a character, and the
            // border's own margin. The old chip guessed at 7 per character with no gutter
            // and the widest word ran out of its box.
            le.preferredWidth = IconBox + 12f + label.Length * 6.7f + 12f;
            le.preferredHeight = CellH;
            return 1;
        }

        // ── the order tip: hover a decided customer's ticket ─────────────────────
        //
        // The author, 2026-08-11: hovering the choice above a customer's head should say how
        // the drink is made and how they want it.
        //
        // Both halves already exist and are already shared — DrawRecipeSpec draws the pour
        // the licence and the book draw, PrefChip draws the endorsements the licence draws —
        // so this is a third window onto them rather than a third telling of them, and the
        // three cannot drift apart.
        //
        // WHAT IT MAY SAY IS GATED, and not by politeness: the order lives behind the ID
        // card, Core refuses to hand it over before InspectId, and a tip that answered
        // anyway would quietly kill the card. Unread, it says only that the licence is where
        // the answer is — which teaches the mechanic instead of skipping it.
        private RectTransform _orderTip, _orderTipBody, _orderTipPrefs;
        private Text _orderTipTitle, _orderTipPrefHead, _orderTipHint;
        private int _orderTipSeat = -1;

        /// <summary>How wide the hover tip is. A little over the licence's own, because it
        /// carries the endorsements under the pour rather than beside them.</summary>
        private const float OrderTipW = 268f;

        /// <summary>How wide it may grow for a long drink name before the name has to wrap.</summary>
        private const float OrderTipMaxW = 380f;

        private void BuildOrderTip(RectTransform root)
        {
            _orderTip = NewRect("OrderTip", root);
            Place(_orderTip, new Vector2(0.5f, 0.5f), new Vector2(OrderTipW, 160f), Vector2.zero);
            _orderTip.pivot = new Vector2(0, 1);          // the position IS the top-left corner
            // Its own sorting layer, above the seats and their tickets — a tip drawn under the
            // thing it explains is not a tip.
            var canvas = _orderTip.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 26;
            var bg = _orderTip.gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.05f, 0.09f, 0.96f);
            bg.raycastTarget = false;
            var edge = new Color(UITheme.Cyan[3].r, UITheme.Cyan[3].g, UITheme.Cyan[3].b, 0.45f);
            Hairline(_orderTip, new Vector2(0, 0), new Vector2(1, 0), edge);
            Hairline(_orderTip, new Vector2(0, 1), new Vector2(1, 1), edge);
            HairlineV(_orderTip, 0f, edge);
            HairlineV(_orderTip, 1f, edge);

            // The drink's name is the HEADING (the author, 2026-08-11): it is the one thing
            // being answered, so it is set in the display face at 16 — a whole multiple of
            // the 8px design size, which is the only size a pixel font rasterises cleanly.
            _orderTipTitle = TipLine("Title", 16, TextAnchor.UpperLeft, UITheme.Amber[4],
                                     display: true);
            _orderTipBody = NewRect("Body", _orderTip);
            Place(_orderTipBody, new Vector2(0, 1), new Vector2(OrderTipW - 20f, 10f), Vector2.zero);
            _orderTipBody.pivot = new Vector2(0, 1);

            _orderTipPrefHead = TipLine("PrefHead", 8, TextAnchor.UpperLeft,
                new Color(0.61f, 0.58f, 0.66f));
            _orderTipPrefs = NewRect("Prefs", _orderTip);
            Place(_orderTipPrefs, new Vector2(0, 1), new Vector2(OrderTipW - 20f, 38f), Vector2.zero);
            _orderTipPrefs.pivot = new Vector2(0, 1);
            var row = _orderTipPrefs.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 5f;
            row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.UpperLeft;

            _orderTipHint = TipLine("Hint", 9, TextAnchor.UpperLeft, UITheme.Cyan[3]);

            _orderTip.gameObject.SetActive(false);
        }

        private Text TipLine(string name, int size, TextAnchor anchor, Color colour,
            bool display = false)
        {
            var t = NewText(name, _orderTip, display ? _display : _body, size, anchor, colour);
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(OrderTipW - 20f, size + 4f),
                Vector2.zero);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Whether something is open that the tip must not print over.</summary>
        private bool AnySheetOpen()
        {
            if (_flow != null && _flow.IsOpen) return true;
            // The bench belongs on this list for the same reason the guide does: it is a
            // sheet over the room, and a customer's order tip printing through it would be
            // the floor talking over a thing that covers the floor.
            return Showing(_idRoot) || Showing(_bookPanel) || Showing(_settingsPanel)
                || Showing(_guidePanel) || Showing(_devPanel) || Showing(_ledgerPanel)
                || Showing(_dayEndPanel);
        }

        private static bool Showing(RectTransform rt) => rt != null && rt.gameObject.activeSelf;

        /// <summary>
        /// Which seat's ticket the pointer is over, or −1.
        ///
        /// A rect test rather than an EventTrigger, and deliberately: the ticket's background
        /// takes no raycast, the seat under it is a button that opens the licence, and giving
        /// the ticket the pointer to win a hover would have taken the click away from the
        /// customer. Nothing about the input graph changes here.
        /// </summary>
        private int HoveredTicket()
        {
            if (AnySheetOpen()) return -1;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return -1;
            var p = mouse.position.ReadValue();
            for (int i = 0; i < _seats.Count; i++)
            {
                var tag = _seats[i].Tag;
                if (tag == null || !tag.gameObject.activeInHierarchy) continue;
                if (_seats[i].Visit == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(tag, p, null)) return i;
            }
            return -1;
        }

        private void UpdateOrderTip()
        {
            if (_orderTip == null) return;
            int seat = HoveredTicket();
            if (seat != _orderTipSeat)
            {
                _orderTipSeat = seat;
                if (seat < 0) _orderTip.gameObject.SetActive(false);
                else FillOrderTip(_seats[seat].Visit);
            }
            if (_orderTip.gameObject.activeSelf) FollowPointerWithOrderTip();
        }

        private void FillOrderTip(CustomerVisit visit)
        {
            if (visit == null) { _orderTip.gameObject.SetActive(false); return; }

            const float Pad = 10f, Gap = 8f, TitleH = 20f;
            float y = Pad;

            _orderTipTitle.rectTransform.anchoredPosition = new Vector2(Pad, -y);
            if (!visit.IdInspected)
            {
                // Unread. The card is the only thing that may answer, so this says where the
                // answer is and stops — no name, no drink, no hint of either.
                _orderTipTitle.text = "READY TO ORDER";
                y += TitleH + Gap;
                foreach (Transform old in _orderTipBody) Destroy(old.gameObject);
                _orderTipBody.gameObject.SetActive(false);
                _orderTipPrefHead.gameObject.SetActive(false);
                _orderTipPrefs.gameObject.SetActive(false);
                _orderTipHint.gameObject.SetActive(true);
                _orderTipHint.rectTransform.anchoredPosition = new Vector2(Pad, -y);
                _orderTipHint.text = "CLICK THEM TO READ THEIR ID";
                y += 13f + Pad;
                SizeTip(OrderTipW, y);
                Show();
                return;
            }

            _orderTipTitle.text = visit.Order.Wanted.Name.ToUpperInvariant();
            y += TitleH + Gap;
            // The heading may be wider than the pours under it — SEX ON THE BEACH in the
            // display face is. The box takes the widest thing it holds rather than clipping
            // the one thing the player came to read.
            float w = Mathf.Clamp(_orderTipTitle.preferredWidth + Pad * 2f, OrderTipW, OrderTipMaxW);

            _orderTipBody.gameObject.SetActive(true);
            _orderTipBody.anchoredPosition = new Vector2(Pad, -y);
            // JUST THE POUR (the author, 2026-08-11). The prep word, the fill line and the
            // glass name left this card: the glass is not the player's to pick — the run
            // chooses it from the recipe — and the prep is not in the match at all, which
            // reads only ratios. What is left is what actually goes in the glass.
            float specH = DrawRecipeSpec(_orderTipBody, visit.Order.Wanted, dark: true,
                width: w - Pad * 2f, poursOnly: true);
            _orderTipBody.sizeDelta = new Vector2(w - Pad * 2f, specH);
            y += specH;

            foreach (Transform old in _orderTipPrefs) Destroy(old.gameObject);
            int chips = 0;
            foreach (var g in visit.Order.Garnishes)
                chips += PrefChip(PrefArt.ForPreparation(g.Id), g.Name.ToUpperInvariant(),
                                  _orderTipPrefs);

            // Asking for nothing is said by there being nothing there. A line announcing that
            // the customer wants nothing is a line to read for no news, which is exactly what
            // was asked to go.
            _orderTipHint.gameObject.SetActive(false);
            _orderTipPrefHead.gameObject.SetActive(chips > 0);
            _orderTipPrefs.gameObject.SetActive(chips > 0);
            if (chips > 0)
            {
                y += Gap;
                _orderTipPrefHead.rectTransform.anchoredPosition = new Vector2(Pad, -y);
                _orderTipPrefHead.text = "HOW THEY WANT IT";
                y += 12f + 2f;
                _orderTipPrefs.anchoredPosition = new Vector2(Pad, -y);
                _orderTipPrefs.sizeDelta = new Vector2(w - Pad * 2f, 38f);
                y += 38f;
            }
            y += Pad;

            SizeTip(w, y);
            Show();

            void SizeTip(float width, float height)
            {
                _orderTip.sizeDelta = new Vector2(width, height);
                var titleRt = _orderTipTitle.rectTransform;
                titleRt.sizeDelta = new Vector2(width - Pad * 2f, titleRt.sizeDelta.y);
            }

            void Show()
            {
                _orderTip.gameObject.SetActive(true);
                // Rebuilt on every hover, so enforced on every hover: nothing in here may
                // take the pointer, or the tip becomes the thing the cursor is on and the
                // hover it is answering ends (the licence tip learned this the hard way).
                foreach (var g in _orderTip.GetComponentsInChildren<Graphic>(true))
                    g.raycastTarget = false;
                FollowPointerWithOrderTip();   // placed before its first frame is drawn
            }
        }

        /// <summary>Hangs off the pointer, and turns back at the edges of the safe frame
        /// rather than running off it.</summary>
        private void FollowPointerWithOrderTip()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || _hudRoot == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _hudRoot, mouse.position.ReadValue(), null, out Vector2 local)) return;

            const float Gap = 16f;
            Vector2 size = _orderTip.sizeDelta;
            float halfW = _hudRoot.rect.width * 0.5f, halfH = _hudRoot.rect.height * 0.5f;
            float x = local.x + Gap;
            if (x + size.x > halfW) x = local.x - Gap - size.x;
            float yTop = local.y - Gap;
            if (yTop - size.y < -halfH) yTop = local.y + Gap + size.y;
            _orderTip.anchoredPosition = new Vector2(x, yTop);
        }

        /// <summary>
        /// A caption in one of the rail's printed boxes, and the big value under it. The
        /// box itself is on the stock (licence_gen.py); this fills it.
        /// </summary>
        private Text LicCell(RectTransform card, float top, string caption, out Text captionText,
            float valueDrop = 22f, int valueSize = 24)
        {
            captionText = NewText("C_" + caption, card, _body, 8, TextAnchor.UpperCenter,
                UITheme.ClubBlue[2]);
            Place(captionText.rectTransform, new Vector2(0, 1), new Vector2(LicCellW, 12),
                new Vector2(LicCellX, -top - 6f));
            captionText.horizontalOverflow = HorizontalWrapMode.Overflow;
            captionText.text = caption;
            // The drop is a parameter because the two cells do not hold the same thing: one
            // is a caption over a number, the other is a caption over a row of stars with
            // the number under THEM. Sharing a fixed drop printed the rating straight
            // through its own third star.
            var val = NewText("V_" + caption, card, _display, valueSize, TextAnchor.UpperCenter,
                UITheme.Night[1]);
            Place(val.rectTransform, new Vector2(0, 1), new Vector2(LicCellW, valueSize + 2f),
                new Vector2(LicCellX, -top - valueDrop));
            val.horizontalOverflow = HorizontalWrapMode.Overflow;
            return val;
        }

        // ── what the bar remembers about a face (2026-08-10) ────────────────────────
        // The licence prints how often this person has come and what they have made of the
        // place, so somebody has to count. It is kept per LOOK rather than per RegularState
        // because the look IS the person on this card — it carries the photograph, the name
        // and the papers — while the archetype behind it only says how they came in. The
        // tally decides nothing; it is what the card reads back, which is why it may live
        // up here with the rest of the identity rather than in Core.
        private sealed class PatronRecord
        {
            public int Visits;
            public double Stars;
            public int Ratings;
        }

        private readonly Dictionary<string, PatronRecord> _patronLog =
            new Dictionary<string, PatronRecord>();

        private PatronRecord LogFor(PatronLook look)
        {
            string key = look != null && !string.IsNullOrEmpty(look.Slug) ? look.Slug : "patron";
            PatronRecord rec;
            if (!_patronLog.TryGetValue(key, out rec))
            {
                rec = new PatronRecord();
                _patronLog[key] = rec;
            }
            return rec;
        }

        /// <summary>One person has walked back out. Books the visit and the stars they left.</summary>
        private void RecordDeparture(PatronLook look, CustomerVisit visit)
        {
            if (visit == null) return;
            var rec = LogFor(look);
            rec.Visits++;
            // THE SAME NUMBER THE BAR'S OWN STANDING IS BUILT FROM (BarRating.Record books
            // exactly this for every finished visit), so the licence and the top bar cannot
            // disagree about how a night went.
            rec.Stars += BarRating.ExactStarsFor(visit.Satisfaction);
            rec.Ratings++;
        }

        /// <summary>
        /// The document number. Deterministic in the person, so the same face carries the
        /// same licence every night of a run — a number that changed on re-entry would be
        /// the one field on the card that proves it is scenery.
        /// </summary>
        private static string LicenceNumber(PatronLook look, string name)
        {
            string key = (look != null && !string.IsNullOrEmpty(look.Slug) ? look.Slug : "patron")
                + "|" + (name ?? "");
            int h = 17;
            unchecked
            {
                for (int i = 0; i < key.Length; i++) h = h * 31 + key[i];
            }
            h &= 0x7FFFFFFF;      // not Mathf.Abs: int.MinValue has no positive counterpart
            return string.Format("NA {0:0000} {1:0000}", h % 10000, h / 10000 % 10000);
        }

        // The week (the author's calendar): six open days, Monday through Saturday —
        // SUNDAY the bar is dark (BarCalendar.OpenNights; this comment said the opposite
        // week for a while, which is exactly the drift the next line exists to prevent).
        // It lives in Core now
        // (2026-08-13): the story schedules its guests by the weekend, so the week became a
        // RULE, and a calendar the HUD kept to itself would be a second one, free to disagree
        // with the game about what day it is. The words are unchanged.
        private static string CalendarFor(int day) => BarCalendar.Label(day);

        /// <summary>
        /// Stands the world body where its seat says, at the size and the alpha the seat is
        /// wearing. The body is a PASSENGER of the stool: every line that already moved,
        /// faded or hid a seat keeps working untouched, and this reads the result once a
        /// frame rather than each of them having to learn about the room.
        ///
        /// The conversion is the stage's own contract — one world unit is one stage unit,
        /// the HUD is drawn at twice that (StageToHud), and the stage's origin is the middle
        /// of its 640x360. The character's FEET sit CharFootDrop below the stool's line,
        /// which is what the counter then covers.
        /// </summary>
        private void SyncPatronBody(SeatView view)
        {
            if (view.Body == null) return;
            bool on = view.Root != null && view.Root.gameObject.activeSelf && view.Body.sprite != null;
            if (view.Body.gameObject.activeSelf != on) view.Body.gameObject.SetActive(on);
            if (!on) return;

            // WHO IS IN FRONT (2026-08-10, the author: a walker crossed in front of the
            // people already at the bar). Every body sat at one sorting order, so their
            // relative depth was whatever the renderer felt like. Somebody still walking
            // in or out is BEHIND everyone seated — they are further into the room — and
            // among the seated, the nearer stool draws in front, which is just perspective.
            bool walking = view.Exiting || view.WalkT < 1f;
            view.Body.sortingOrder = walking ? 22 : 25;

            float drawnH = CharSize / StageToHud;                       // stage units tall
            float k = drawnH / Mathf.Max(0.0001f, view.Body.sprite.bounds.size.y);
            view.Body.transform.localScale = new Vector3(k * CharWiden, k, 1f);

            var p = view.Root.anchoredPosition;
            float footY = (p.y - CharFootDrop) / StageToHud;            // stage units
            view.Body.transform.position = new Vector3(
                p.x / StageToHud - StageRef.x * 0.5f,
                footY + drawnH * 0.5f - StageRef.y * 0.5f, 0f);

            var c = view.Body.color;
            c.a = view.Group != null ? view.Group.alpha : 1f;
            view.Body.color = c;
        }

        /// <summary>The stage's reference frame, the one both halves agree on.</summary>
        private static readonly Vector2 StageRef = new Vector2(640f, 360f);

        /// <summary>0–5 stars as glyphs, the empty ones kept so the width never jumps.</summary>

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
        // A CARD CUT TO OUR OWN ZONES (2026-08-10). The old shell was 510x315 with its
        // furniture wherever the drawing had put it, and the layout bent around it; worse,
        // its scale was the PHOTO's hostage, because pixel art magnifies only in whole
        // steps and a 96px face at 2x demanded a 192-wide window.
        //
        // licence_shell3 is authored the other way round: the zones came first and the art
        // was cut to them. 256x160 drawn at a WHOLE 3x — every art pixel lands on three
        // screen pixels, nothing resamples — which is a 768x480 card, larger than the 714
        // it replaces. PixelLab drew the paper (stock, wear, a guilloche tint); the band,
        // the portrait well and the five rules are printed onto it at exact coordinates,
        // because furniture that has to line up with a text field is a specification and
        // the generator has never hit one.
        private const float LicScale = 3f;
        private const float LicW = 256f * LicScale, LicH = 160f * LicScale;
        //
        // A DRIVING-LICENCE STRUCTURE (2026-08-10, the author: "sürücü belgelerine benzer
        // bir yapı"). What makes a document read as a licence is not its outline — it is
        // the numbered field grid, the boxed data cells under the photograph and the
        // pictogram endorsements at the foot. So the card is built that way now: the band
        // carries the jurisdiction and the licence number, the name is field 1 where a
        // licence puts it, and the rail under the photo carries two data cells.
        //
        // The well is CUT TO THE PHOTO. It used to be 222 units around a 144 photo, which
        // was both the author's "too much room for the portrait" and the reason a caption
        // printed across every face: 78 units of the well were letterbox.
        // EVERY ZONE IS MEASURED OFF THE CREAM, NOT OFF THE CANVAS (2026-08-10). The
        // generator did not draw the card on transparency, it drew it on an opaque
        // near-white ground, so the 256x160 sprite carries a 228x138 card at x 14..241,
        // y 11..148. Laying the fields out to the canvas put the rules off the right edge
        // of the paper and dropped the endorsement cells clean off the bottom of it —
        // which every rect measurement passed, because they were all still inside the
        // CARD RECT. Only a screenshot could show it, and did.
        private static readonly Rect LicPortrait = new Rect(19f * LicScale, -32f * LicScale,
            48f * LicScale, 48f * LicScale);
        private const float LicHeaderH = 14f * LicScale;
        private const float LicHeaderY = -13f * LicScale;
        private const float LicFieldsX = 74f * LicScale;
        private const float LicFieldsW = 161f * LicScale;
        private static readonly float[] LicLines =   // the art's four printed rules
            { 46f * LicScale, 71f * LicScale, 99f * LicScale, 124f * LicScale };
        // The rail's two data cells and the rule the licence number is printed on, at the
        // art's own coordinates — the boxes are drawn on the stock, not by the UI.
        private const float LicCellX = 18f * LicScale, LicCellW = 50f * LicScale;
        private const float LicCellH = 24f * LicScale;
        private static readonly float[] LicCells = { 85f * LicScale, 112f * LicScale };
        private const float LicNumRule = 141f * LicScale;

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

        // ── the week, as an instrument (2026-08-19, the author: "Haftalık takvim
        // göstergesi daha profesyonelce olmalı") ────────────────────────────────
        //
        // Third cut. The first was seven filled cells — a table, thrown out as "kutu kutu".
        // The second was signage: bulbs hanging off a wire strung across the open beam, and
        // the wire is what the author is calling unprofessional now — it reads as bunting,
        // and its parts (a floating rail, stems, letters with nothing under them) sit ON
        // nothing. What a bar's wall actually mounts is a PANEL: this is the clock's own
        // case and glass at calendar width, the week counter reading at its head where the
        // instrument names its count, and the seven nights as lamps in a slotted row — the
        // same indicator-lamp grammar the seven-segment hour already speaks. Slot rules are
        // joinery, not cells: nothing is boxed, the glass is one surface.
        //
        // What each slot says is unchanged from the marquee (the grammar survived the
        // furniture): tonight's lamp burns and its glow is on, worked nights are dull glass,
        // nights ahead wait dark, SATURDAY's fitting is the star (shape says what the night
        // is, light says when), and SUNDAY carries shutter slats where the others carry a
        // lamp, because a bar that does not open has its shutter down, not a dimmer bulb.
        //
        // It is the same `BarCalendar` the rules count in: the panel cannot say Friday
        // while the arc thinks it is Thursday, because neither is doing its own arithmetic.

        private readonly List<(Image bulb, Image glow, Text name)> _weekCells =
            new List<(Image, Image, Text)>();
        private Text _weekLabel;
        private int _weekShown = -1;
        private int _vipCell = -1;    // which fitting in the row is the star

        // The instrument's own grid, in well-local units off its left edge. 52 of
        // pitch is what three Silkscreen letters at 16 actually need ("hafta göstergesi
        // ufak ve sönük kalıyor" bought the size; the pitch keeps it). The head column
        // holds the counter; a display rule divides it from the nights.
        private const float WeekStep = 52f;
        private const float WeekHeadCx = 40f;    // the counter column's centre
        private const float WeekRuleX = 76f;     // the display rule after it
        private const float WeekDaysX = 80f;     // where the first slot begins
        private const float WeekNameY = 5f;      // the word row, upper half of the glass
        private const float WeekSignY = -9f;     // the sign under it: tube, star, shutter

        private void BuildWeekStrip(RectTransform top)
        {
            var names = BarCalendar.WeekColumns;
            // The generated plate lasted one build ("Oluşturulan takvim görseli bozuk
            // duruyor, elinden geldiğince kendin tasarımını yap") — the exception to
            // chrome-is-never-generated was tried on the author's sentence and withdrawn
            // on the author's next one. The calendar sits in the same drawn WELL the hour
            // does; two instruments, one language, and nothing on the beam is a picture.
            float wellW = WeekDaysX + names.Length * WeekStep + 10f;
            var glass = NewRect("WeekWell", top);
            glass.anchorMin = glass.anchorMax = glass.pivot = new Vector2(0.5f, 0.5f);
            glass.sizeDelta = new Vector2(wellW, 40f);
            glass.anchoredPosition = Vector2.zero;
            var glassImg = glass.gameObject.AddComponent<Image>();
            glassImg.sprite = ChromeArt.Well();
            glassImg.type = Image.Type.Sliced;
            glassImg.raycastTarget = false;

            // The head: what the instrument counts, then the count. The caption is the
            // small line, the number is the reading — CapY/ReadY's own logic, folded to
            // the well's glass.
            var cap = NewText("WeekCap", glass, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
            Place(cap.rectTransform, new Vector2(0, 0.5f), new Vector2(52, 12),
                new Vector2(WeekHeadCx, 7f));
            cap.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            cap.text = "WEEK";

            _weekLabel = NewText("Week", glass, _display, 16, TextAnchor.MiddleCenter, UITheme.Cyan[3]);
            Place(_weekLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(52, 18),
                new Vector2(WeekHeadCx, -7f));
            _weekLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _weekLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            // The display rule between the count and the nights — on the glass, not the
            // case, because it separates what the display says (the clock's old divider,
            // moved to the instrument that still has two readings).
            var rule = NewRect("Divide", glass);
            Place(rule, new Vector2(0, 0.5f), new Vector2(1, 22), new Vector2(WeekRuleX, 0));
            var ruleImg = rule.gameObject.AddComponent<Image>();
            ruleImg.color = new Color(UITheme.Cyan[4].r, UITheme.Cyan[4].g, UITheme.Cyan[4].b, 0.22f);
            ruleImg.raycastTarget = false;

            for (int i = 0; i < names.Length; i++)
            {
                float cx = WeekDaysX + i * WeekStep + WeekStep * 0.5f;
                bool open = i < BarCalendar.OpenNights;

                // THE WORD IS THE LAMP (the fourth cut's one idea). The lamp row is gone:
                // the seven names sit on the same glass the hour's digits sit on, and
                // tonight's name is LIT the way a digit is lit — amber, with a miniature
                // neon tube burning under it, the beam's own foot light one slot wide.
                // Spent nights go dim glass, nights ahead read cream; the states live in
                // the letters, which is where the eye already was.
                var name = NewText("N" + i, glass, _body, 16, TextAnchor.MiddleCenter, UITheme.TextSecondary);
                Place(name.rectTransform, new Vector2(0, 0.5f), new Vector2(WeekStep, 18),
                    new Vector2(cx, WeekNameY));
                name.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                name.text = names[i];

                Image sign = null, bloom = null;
                if (!open)
                {
                    // THE SHUTTER (the author: "pazar gününün tatil olduğu anlaşılsın"):
                    // two slats under the name where the open nights carry their light.
                    // A closed bar has its shutter down; nothing here is a greyed cell.
                    for (int sl = 0; sl < 2; sl++)
                    {
                        var slat = NewRect("Shut" + sl, glass);
                        Place(slat, new Vector2(0, 0.5f), new Vector2(21, 2),
                            new Vector2(cx, WeekSignY + 2f - sl * 4f));
                        slat.pivot = new Vector2(0.5f, 0.5f);
                        var slatImg = slat.gameObject.AddComponent<Image>();
                        slatImg.color = UITheme.Night[3]; slatImg.raycastTarget = false;
                    }
                }
                else if ((BarNight)i == BarCalendar.VipNight)
                {
                    // THE NIGHT A NAME COMES IS THE STAR FITTING (the author: "cumartesi
                    // günleri vip hikaye müşterisi geleceği belirtilsin"): Saturday's sign
                    // is the star, every week, whether or not a beat is booked. Shape says
                    // what the night is; how hard it burns says when.
                    var starRt = NewRect("Star" + i, glass);
                    Place(starRt, new Vector2(0, 0.5f), new Vector2(16, 16),
                        new Vector2(cx, WeekSignY - 1f));
                    starRt.pivot = new Vector2(0.5f, 0.5f);
                    sign = starRt.gameObject.AddComponent<Image>();
                    sign.sprite = ChromeArt.Mark("star");
                    sign.raycastTarget = false;
                    _vipCell = i;
                }
                else
                {
                    // The night-tube: a 2-unit core over a 1-unit bloom, unlit until the
                    // night is being played — the same anatomy as the beam's foot.
                    var tube = NewRect("Tube" + i, glass);
                    Place(tube, new Vector2(0, 0.5f), new Vector2(24, 2),
                        new Vector2(cx, WeekSignY));
                    tube.pivot = new Vector2(0.5f, 0.5f);
                    sign = tube.gameObject.AddComponent<Image>();
                    sign.raycastTarget = false;
                    var bloomRt = NewRect("Bloom" + i, glass);
                    Place(bloomRt, new Vector2(0, 0.5f), new Vector2(24, 1),
                        new Vector2(cx, WeekSignY - 2f));
                    bloomRt.pivot = new Vector2(0.5f, 0.5f);
                    bloom = bloomRt.gameObject.AddComponent<Image>();
                    bloom.raycastTarget = false;
                }

                _weekCells.Add((sign, bloom, name));
            }
        }

        /// <summary>
        /// Lights the marquee from the run: which week it is, which night is being played,
        /// which nights the arc is due on, and the one the bar does not open at all.
        /// </summary>
        private void RefreshWeekStrip(TycoonRun run)
        {
            if (_weekCells.Count == 0) return;
            int week = BarCalendar.WeekOf(run.Day);
            var tonight = BarCalendar.NightOf(run.Day);
            if (week != _weekShown)
            {
                _weekShown = week;
                // Just the count: the word WEEK is the instrument's own printed caption now.
                _weekLabel.text = $"{week:00}";
            }

            // Which nights the story is coming on, THIS week. A beat due in a later week
            // leaves the marquee clean: the calendar shows the week it is showing.
            var due = run.Story?.Current;
            int dueDay = run.Story != null ? run.Story.DueDay : 0;
            bool dueThisWeek = due != null && BarCalendar.WeekOf(dueDay) == week;

            // THE WORD SAYS THE STATE, THE SIGN UNDER IT SAYS WHAT THE NIGHT IS. A
            // tube burns only under the night being played; the star fitting is always
            // Saturday's and only how hard it burns changes; the shutter is Sunday's and
            // never changes at all.
            for (int i = 0; i < _weekCells.Count; i++)
            {
                var (sign, bloom, name) = _weekCells[i];
                bool closed = i >= BarCalendar.OpenNights;          // the seventh night
                bool isTonight = !closed && (int)tonight == i;
                bool worked = !closed && i < (int)tonight;
                bool storyNight = dueThisWeek && !closed && (int)BarCalendar.NightOf(dueDay) == i;

                bool star = i == _vipCell;
                if (sign != null)
                {
                    if (star)
                    {
                        // Legible from across the week even four days out; full magenta
                        // only on the night itself.
                        var m = UITheme.Magenta[4];
                        sign.color = isTonight ? m : new Color(m.r, m.g, m.b, 0.62f);
                    }
                    else
                    {
                        // The tube burns the WORD's own hue: a story night that is also
                        // tonight reads magenta up top, and an amber tube under magenta
                        // letters would be the strip disagreeing with itself.
                        sign.enabled = isTonight;
                        sign.color = storyNight ? UITheme.Magenta[4] : UITheme.Amber[4];
                    }
                }
                if (bloom != null)
                {
                    bloom.enabled = isTonight;
                    var b = storyNight ? UITheme.Magenta[2] : UITheme.Amber[2];
                    bloom.color = new Color(b.r, b.g, b.b, 0.5f);
                }

                // BRIGHT ENOUGH TO BE A CALENDAR (the author: "ufak ve sönük kalıyor",
                // then "yazılar okumuyor"): nights ahead are cream, one step up from the
                // first cut; the nights already worked are the dim ones, because a night
                // that is spent is the only one on the glass with nothing left to say.
                name.color = closed ? UITheme.Night[4]
                    : storyNight ? UITheme.Magenta[4]
                    : isTonight ? UITheme.Amber[4]
                    : worked ? UITheme.Night[4]
                    : UITheme.Cream[3];
            }
        }

        // ── the last customer (GDD 26 §7, PLAN_last_call S3) ────────────────────
        //
        // Two surfaces and nothing else. THE PLATE is the conversation: a face, a name, one
        // line, and the two things the player can do about it — listen, or say no tonight. It
        // lives at the bottom edge, on the player's side of the counter, because the room
        // behind the bar is the one thing this game never covers up. THE POST-IT is the job:
        // the drink being asked for right now, how many are left, and the clock. It hangs
        // above EVERYTHING — over the bench, over the tap — because the whole point of a
        // trial is that you are working while it runs, and a note you cannot read while
        // pouring is a note nobody wrote.

        private RectTransform _plate;            // the dialogue plate
        private Image _plateFace;
        private Text _plateName, _plateLine, _plateKeyLabel;
        private RectTransform _plateKey, _plateNoKey;
        private RectTransform _gateRow, _gateFill;   // the rung a guest came too early for
        private Text _gateText;
        private RectTransform _postIt;           // the ask, the count, the clock
        private Text _postWho, _postAsk, _postCount, _postMissing;
        private Image _postClock;

        /// <summary>The lines the plate is working through, and where it has got to.</summary>
        private readonly List<(string who, string look, string line)> _plateScript =
            new List<(string, string, string)>();
        private int _plateAt;
        private string _plateStage = "";         // which part of the night the script is for

        private void BuildLastCall(RectTransform root)
        {
            // Its own layer at 7: above the drinkers and the counter props, below the service
            // flow (12) — opening the bench must cover the conversation, not fight it.
            _plate = NewRect("LastCallPlate", root);
            var plateCanvas = _plate.gameObject.AddComponent<Canvas>();
            plateCanvas.overrideSorting = true;
            plateCanvas.sortingOrder = 7;
            _plate.gameObject.AddComponent<GraphicRaycaster>();
            Place(_plate, new Vector2(0.5f, 0f), new Vector2(820, 140), new Vector2(0, 14));
            var paper = _plate.gameObject.AddComponent<Image>();
            paper.sprite = ChromeArt.Card();
            paper.type = Image.Type.Sliced;
            paper.color = UITheme.Cream[4];
            _plate.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            // The face, in a well cut to it — the same 2x whole-step rule the licence photo
            // obeys, because it is the same drawing.
            var well = NewRect("Well", _plate);
            Place(well, new Vector2(0, 0.5f), new Vector2(104, 104), new Vector2(70, 0));
            well.gameObject.AddComponent<Image>().color = UITheme.Night[2];
            var photo = NewRect("Photo", well);
            Place(photo, new Vector2(0.5f, 0.5f), new Vector2(96, 96), Vector2.zero);
            _plateFace = photo.gameObject.AddComponent<Image>();
            _plateFace.preserveAspect = true;
            _plateFace.raycastTarget = false;

            // The words live BETWEEN the face and the keys, and the column is measured from
            // both: the well's pivot is its left edge, so it ends at 174, and the key column
            // starts at 646. 200..640 clears each by a margin. A line that runs under a
            // button is a line the player reads half of — which is how the first cut of this
            // plate shipped, and it was obvious the moment it was looked at.
            _plateName = NewText("Who", _plate, _display, 16, TextAnchor.UpperLeft, UITheme.Night[1]);
            Place(_plateName.rectTransform, new Vector2(0, 1), new Vector2(440, 22),
                new Vector2(200, -24));
            _plateName.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 16, not 12 — the size the face actually has (GDD 16 §0), and 56 tall so the
            // gate strip below it has somewhere to live.
            _plateLine = NewText("Line", _plate, _body, 16, TextAnchor.UpperLeft, UITheme.Night[1]);
            Place(_plateLine.rectTransform, new Vector2(0, 1), new Vector2(440, 56),
                new Vector2(200, -52));

            // ── THE RUNG, WHEN THEY CAME TOO EARLY (GDD 26 §12) ───────────────
            // The dialogue says WHY there is no order; this says HOW FAR. It is drawn in the
            // standing's own stars rather than written out, because the number the player is
            // being asked for is the number they watch on the board all night — the same five
            // shapes, filled to the rung this guest came for, with where the bar actually
            // stands printed beside it. A sentence would have been a caption in the place a
            // drawing belongs (GDD 16 §6.7).
            _gateRow = NewRect("Gate", _plate);
            Place(_gateRow, new Vector2(0, 1), new Vector2(440, 20), new Vector2(200, -108));
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var socket = NewRect($"GS{i}", _gateRow);
                Place(socket, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(i * 18f + 8f, 0));
                socket.pivot = new Vector2(0.5f, 0.5f);
                var si = socket.gameObject.AddComponent<Image>();
                si.sprite = ItemArt.Load("star");
                si.preserveAspect = true; si.raycastTarget = false;
                si.color = new Color(UITheme.Night[2].r, UITheme.Night[2].g, UITheme.Night[2].b, 0.45f);
            }
            _gateFill = NewRect("GateFill", _gateRow);
            _gateFill.anchorMin = new Vector2(0, 0); _gateFill.anchorMax = new Vector2(0, 1);
            _gateFill.pivot = new Vector2(0, 0.5f);
            _gateFill.sizeDelta = Vector2.zero;
            _gateFill.anchoredPosition = Vector2.zero;
            _gateFill.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var star = NewRect($"GF{i}", _gateFill);
                Place(star, new Vector2(0, 0.5f), new Vector2(16, 16), new Vector2(i * 18f + 8f, 0));
                star.pivot = new Vector2(0.5f, 0.5f);
                var si = star.gameObject.AddComponent<Image>();
                si.sprite = ItemArt.Load("star");
                si.preserveAspect = true; si.raycastTarget = false;
                si.color = UITheme.Magenta[2];      // the story's colour, not the board's amber
            }
            _gateText = NewText("GateText", _gateRow, _body, 8, TextAnchor.MiddleLeft, UITheme.Night[2]);
            Place(_gateText.rectTransform, new Vector2(0, 0.5f), new Vector2(300, 16),
                new Vector2(104, 0));
            _gateText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _gateRow.gameObject.SetActive(false);

            // Two keys, and the second one is the honest exit. A player who cannot pour what
            // is being asked for must always be able to say so (GDD 26 §5) — the beat comes
            // back, and nothing about the night is lost by admitting it.
            _plateKey = NewRect("Listen", _plate);
            Place(_plateKey, new Vector2(1, 0.5f), new Vector2(150, 40), new Vector2(-24, 22));
            var keyBtn = _plateKey.gameObject.AddComponent<Button>();
            keyBtn.onClick.AddListener(OnPlateKey);
            var keyFace = NewRect("Face", _plateKey);
            Stretch(keyFace, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(_plateKey, UITheme.PrimaryAction, keyBtn, keyFace);   // GDD 16 §2
            _plateKeyLabel = NewText("Label", keyFace, _body, 16, TextAnchor.MiddleCenter,
                UITheme.TextOnAmber);
            Stretch(_plateKeyLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, KeyPlate.Throw), new Vector2(-4, 0));
            _plateKeyLabel.text = "GO ON";

            _plateNoKey = NewRect("SayNo", _plate);
            Place(_plateNoKey, new Vector2(1, 0.5f), new Vector2(150, 32), new Vector2(-24, -26));
            var noBtn = _plateNoKey.gameObject.AddComponent<Button>();
            noBtn.onClick.AddListener(OnSayNoTonight);
            var noFace = NewRect("Face", _plateNoKey);
            Stretch(noFace, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(_plateNoKey, UITheme.Night[3], noBtn, noFace);
            var noLabel = NewText("Label", noFace, _body, 8, TextAnchor.MiddleCenter,
                UITheme.TextPrimary);
            Stretch(noLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, KeyPlate.Throw), new Vector2(-4, 0));
            noLabel.text = "SAY NO TONIGHT";
            _plate.gameObject.SetActive(false);

            // The post-it, top right, ABOVE THE BENCH (14): the note you work from.
            _postIt = NewRect("LastCallNote", root);
            var noteCanvas = _postIt.gameObject.AddComponent<Canvas>();
            noteCanvas.overrideSorting = true;
            noteCanvas.sortingOrder = 14;
            // Clear of the LAST CALL plaque, which lives at the top right of the room and is
            // the other thing this night is announced by. Two magenta words on top of each
            // other read as one broken thing.
            Place(_postIt, new Vector2(1, 1), new Vector2(232, 150), new Vector2(-18, -152));
            var note = _postIt.gameObject.AddComponent<Image>();
            note.sprite = ChromeArt.Card();
            note.type = Image.Type.Sliced;
            note.color = UITheme.Amber[4];
            note.raycastTarget = false;

            _postWho = NewText("Who", _postIt, _body, 8, TextAnchor.UpperLeft, UITheme.Night[2]);
            Place(_postWho.rectTransform, new Vector2(0, 1), new Vector2(200, 12), new Vector2(16, -12));
            _postAsk = NewText("Ask", _postIt, _display, 16, TextAnchor.UpperLeft, UITheme.Night[1]);
            Place(_postAsk.rectTransform, new Vector2(0, 1), new Vector2(206, 44), new Vector2(16, -30));
            _postCount = NewText("Count", _postIt, _body, 12, TextAnchor.UpperLeft, UITheme.Night[2]);
            Place(_postCount.rectTransform, new Vector2(0, 1), new Vector2(200, 16), new Vector2(16, -84));
            _postMissing = NewText("Missing", _postIt, _body, 8, TextAnchor.UpperLeft, UITheme.ViceRed[2]);
            Place(_postMissing.rectTransform, new Vector2(0, 1), new Vector2(200, 24), new Vector2(16, -104));

            var track = NewRect("Track", _postIt);
            Place(track, new Vector2(0, 0), new Vector2(200, 8), new Vector2(16, 14));
            track.gameObject.AddComponent<Image>().color = UITheme.Night[2];
            var fill = NewRect("Fill", track);
            fill.anchorMin = new Vector2(0, 0);
            fill.anchorMax = new Vector2(1, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.pivot = new Vector2(0, 0.5f);
            _postClock = fill.gameObject.AddComponent<Image>();
            _postClock.color = UITheme.Magenta[4];
            _postClock.type = Image.Type.Filled;
            _postClock.fillMethod = Image.FillMethod.Horizontal;
            _postIt.gameObject.SetActive(false);
        }

        private PatronLook LookNamed(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return null;
            foreach (var look in _looks) if (look.Slug == slug) return look;
            return null;
        }

        /// <summary>The face this beat's person wears — their own if it has been drawn, the
        /// one they borrow until then (GDD 26 §1b). Null once neither exists, which is a
        /// content error the loader already refuses.</summary>
        private PatronLook LookForStory(StoryCharacter who) =>
            who == null ? null : LookNamed(who.Look) ?? LookNamed(who.PlaceholderLook);

        /// <summary>
        /// Drives the two surfaces off the run's own state (GDD 26 §4). It owns no timing and
        /// no rules: Core decides whether a trial is talking, pouring or over, and this reads
        /// that once a frame. The one thing it DOES own is the script — which line of the
        /// beat is being said, and by whom.
        /// </summary>
        private void SyncLastCall(TycoonRun run)
        {
            var beat = run.LastCallBeat;
            var trial = run.Trial;
            // Nothing of this survives the night's end: the slip is the next thing the player
            // reads, and a plate at layer 7 would sit on top of it.
            if (run.Phase != TycoonPhase.DayOpen) { beat = null; trial = null; }

            // THE ROOM SAYS IT TOO (GDD 26 §7, S4). The ceiling comes down, the neon over the
            // door burns harder and one lamp finds whoever is at the bar — driven off the
            // guest's own stool, so the light lands on the person and not on a guessed spot.
            if (stage != null)
            {
                var lit = run.LastCustomer;
                float x = 0f;
                if (lit != null)
                    foreach (var s in _seats) if (s.Visit == lit) { x = s.Root.anchoredPosition.x; break; }
                stage.SetClosingBeat(lit != null, x);
            }
            // A WITHHELD NIGHT HAS NO TRIAL AND IS STILL A SCENE (GDD 26 §12). The guest came,
            // the bar has not reached their rung, and they are on the stool saying so — which
            // is the one thing this panel exists for. Keying off `trial` alone hid the plate
            // and left somebody sitting in a dimmed room in silence.
            bool withheld = beat != null && run.LastCallWithheld && run.LastCustomer != null;
            if (beat == null || (trial == null && !withheld))
            {
                if (_plate != null && _plate.gameObject.activeSelf) _plate.gameObject.SetActive(false);
                if (_postIt != null && _postIt.gameObject.activeSelf) _postIt.gameObject.SetActive(false);
                if (_gateRow != null && _gateRow.gameObject.activeSelf) _gateRow.gameObject.SetActive(false);
                _plateStage = "";
                return;
            }

            // The script is rebuilt when the night moves to a new part of itself, and only
            // then: a plate that re-cued every frame would never get past its first line.
            string part = withheld ? "short"
                : trial.State == TrialState.Talking ? "ask"
                : trial.State == TrialState.Pouring ? "pour"
                : trial.State == TrialState.Passed ? "kept" : "missed";
            if (part != _plateStage)
            {
                _plateStage = part;
                _plateAt = 0;
                _plateScript.Clear();
                var host = _bootstrap?.Story?.Cast?.FirstOrDefault(c => c.IsHost);
                if (part == "ask")
                {
                    foreach (var line in beat.Lines.HostBefore) Add(host, line);
                    foreach (var line in beat.Lines.Ask) Add(beat.Who, line);
                }
                else if (part == "short")
                {
                    // The guest explains, and the house has the last word — which is where the
                    // system gets taught, because the host is the one who can say what a star
                    // is and how you get another one.
                    foreach (var line in beat.Lines.HostBefore) Add(host, line);
                    foreach (var line in beat.Lines.ShortOfGate) Add(beat.Who, line);
                    foreach (var line in beat.Lines.HostAfter) Add(host, line);
                }
                else if (part == "kept" || part == "missed")
                {
                    // THREE WAYS TO MISS, THREE THINGS TO SAY (GDD 26 §5): a wrong drink is
                    // answered by the wrong-drink line, an honest no by the declined line,
                    // and a clock that simply ran out by the nudge — because the beat never
                    // wrote a line for being ignored, and the nudge is what it has.
                    var said = part == "kept" ? beat.Lines.ServedRight
                        : trial.ToldNo ? beat.Lines.Declined
                        : trial.Mistakes > 0 ? beat.Lines.ServedWrong
                        : beat.Lines.Nudge;
                    foreach (var line in said) Add(beat.Who, line);
                    foreach (var line in beat.Lines.HostAfter) Add(host, line);
                }
            }

            bool talking = _plateScript.Count > 0 && _plateAt < _plateScript.Count;
            if (talking != _plate.gameObject.activeSelf) _plate.gameObject.SetActive(talking);
            if (talking)
            {
                var (who, look, line) = _plateScript[_plateAt];
                _plateName.text = who.ToUpperInvariant();
                _plateLine.text = line;
                var face = LookNamed(look);
                _plateFace.sprite = face?.Face;
                _plateFace.enabled = _plateFace.sprite != null;
                bool last = _plateAt == _plateScript.Count - 1;
                _plateKeyLabel.text = part == "ask" && last ? "POUR IT"
                    : part == "short" && last ? "GOOD NIGHT" : "GO ON";
                // Nothing to decline on a night nothing was asked for.
                _plateNoKey.gameObject.SetActive(part == "ask");
            }

            // THE RUNG STAYS UP FOR THE WHOLE SCENE, under whoever is speaking: the guest
            // saying they will be back and the host explaining why are both about one number,
            // and it is drawn once rather than repeated in two lines of prose.
            bool showGate = withheld && talking;
            if (showGate != _gateRow.gameObject.activeSelf) _gateRow.gameObject.SetActive(showGate);
            if (showGate)
            {
                double need = beat.RequiresStars, now = run.Rating.Average;
                _gateFill.sizeDelta = new Vector2((float)(need / BarRating.MaxStars)
                                                  * BarRating.MaxStars * 18f, 0);
                _gateText.text = $"COMES BACK AT {need:0.0} STARS  ·  YOU HAVE {now:0.0}";
            }

            bool working = trial != null && trial.State == TrialState.Pouring;
            if (working != _postIt.gameObject.activeSelf) _postIt.gameObject.SetActive(working);
            if (working)
            {
                var ask = trial.Current;
                _postWho.text = beat.Who.Name.ToUpperInvariant();
                _postAsk.text = ask != null ? ask.Name.ToUpperInvariant() : "";
                _postCount.text = $"{trial.Done + 1} OF {trial.Total}"
                                  + (trial.Trial.AllowedMistakes > 0
                                      ? $"  ·  {Math.Max(0, trial.Trial.AllowedMistakes - trial.Mistakes)} SPARE"
                                      : "  ·  NO MISTAKES");
                var guest = run.LastCustomer;
                _postClock.fillAmount = guest == null || guest.PatienceMax <= 0
                    ? 0f : Mathf.Clamp01((float)(guest.PatienceLeft / guest.PatienceMax));
                _postClock.color = _postClock.fillAmount < 0.25f ? UITheme.ViceRed[3] : UITheme.Magenta[4];
                var lacking = ask != null ? MissingStyles(ask) : null;
                _postMissing.text = lacking != null && lacking.Count > 0
                    ? "NO " + string.Join(", ", lacking).ToUpperInvariant() + " ON THE SHELF" : "";
            }

            void Add(StoryCharacter speaker, string line)
            {
                if (speaker == null || string.IsNullOrEmpty(line)) return;
                _plateScript.Add((speaker.Name, LookForStory(speaker)?.Slug, line));
            }
        }

        /// <summary>The listen key: one line at a time, and the last one starts the clock.</summary>
        private void OnPlateKey()
        {
            var run = Run;
            // A WITHHELD NIGHT HAS NO TRIAL and the key must still turn the page (GDD 26 §12);
            // guarding on `Trial` alone left the guest's own scene unadvanceable, with the
            // only way out being the clock.
            if (run == null || (run.Trial == null && !run.LastCallWithheld)) return;
            if (_plateAt < _plateScript.Count - 1) { _plateAt++; return; }
            _plateAt = _plateScript.Count;      // the script is spoken
            if (run.Trial != null && run.Trial.State == TrialState.Talking) run.BeginLastCallTrial();
        }

        /// <summary>The honest no. It costs the night and never the arc (GDD 26 §5).</summary>
        private void OnSayNoTonight()
        {
            var run = Run;
            if (run == null || run.LastCustomer == null) return;
            run.DeclineLastCall();
            Toast("YOU TOLD THEM NO — THEY WILL BE BACK");
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
            shell.sprite = ItemArt.Load("licence_shell3");
            if (shell.sprite == null) shell.sprite = ItemArt.Load("licence_shell2");
            if (shell.sprite == null) shell.color = UITheme.Cream[4];   // no art: a plain card
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallow clicks

            // THE BAND IS THE HEADER OF A LICENCE: the issuing authority on the left, the
            // document number on the right. It carried the NAME for a week, which read well
            // but is not where a licence puts a name — and the header it replaced was 320
            // units of ink identical on every card. The number fixes that: it is the one
            // header field that is different on all thirty-one.
            float bandMid = LicHeaderY - LicHeaderH * 0.5f;
            var authority = NewText("Authority", card, _display, 16, TextAnchor.MiddleLeft,
                UITheme.Cream[4]);
            Place(authority.rectTransform, new Vector2(0, 1), new Vector2(200, 18),
                new Vector2(56, bandMid + 6f));
            authority.horizontalOverflow = HorizontalWrapMode.Overflow;
            authority.text = "NEW ARDEN";
            var docType = NewText("DocType", card, _body, 8, TextAnchor.MiddleLeft,
                new Color(0.62f, 0.72f, 0.88f, 1f));
            Place(docType.rectTransform, new Vector2(0, 1), new Vector2(260, 12),
                new Vector2(220, bandMid + 5f));
            docType.horizontalOverflow = HorizontalWrapMode.Overflow;
            docType.text = "PATRON LICENCE  ·  CLASS B";

            // The flag rides the header, where a licence puts its emblem. It is the one
            // thing up here that changes from card to card besides the number below.
            var idFlag = NewRect("Flag", card);
            Place(idFlag, new Vector2(1, 1), new Vector2(24, 16),
                new Vector2(-59, bandMid + 8f));
            _idFlag = idFlag.gameObject.AddComponent<Image>();
            _idFlag.preserveAspect = true;
            _idFlag.raycastTarget = false;
            _idFlag.enabled = false;

            // A WHOLE 2x OF THE 72px FACE, centred in a well cut to fit it. Pixel art
            // magnifies only in whole steps, so 144 is not a taste — it is the only size
            // the photo can be drawn at on this card without resampling. The source is cut
            // at 1:1 too (patron_faces.py): the faces used to be measured per character and
            // pulled to 72, which magnified 26 of the 31 by a fraction and duplicated some
            // pixel rows and not others. That unevenness is what the author saw as the
            // photo being stretched, and it was.
            const float LicPhoto = 144f;
            var photo = NewRect("Photo", card);
            Place(photo, new Vector2(0, 1), new Vector2(LicPhoto, LicPhoto), new Vector2(
                LicPortrait.x + (LicPortrait.width - LicPhoto) * 0.5f,
                LicPortrait.y - (LicPortrait.height - LicPhoto) * 0.5f));
            _idPhoto = photo.gameObject.AddComponent<Image>();
            // The window and the sprite are both square, so this can never squash a face —
            // and a face that would not fit is cropped by the frame, never stretched to it.
            _idPhoto.preserveAspect = true;

            // ── the rail's two data cells, under the photograph ────────────────────
            // A licence keeps its counts in boxes beside the picture, and these are facts
            // about the person rather than about the drink: how often they have walked in,
            // and what they have made of the place. The boxes themselves are printed on the
            // stock; what goes in them is lettered here.
            _idVisitCount = LicCell(card, LicCells[0], "VISITS", out _idRelLabel);
            _idRel = NewText("Standing", card, _body, 8, TextAnchor.UpperCenter, UITheme.Night[3]);
            Place(_idRel.rectTransform, new Vector2(0, 1), new Vector2(LicCellW, 12),
                new Vector2(LicCellX, -LicCells[0] - LicCellH + 16f));
            _idRel.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Caption, then the stars, then the number under them — so the drop clears the
            // star row rather than landing in the middle of it.
            _idRates = LicCell(card, LicCells[1], "RATES THIS BAR", out _idRatesLabel,
                valueDrop: 50f, valueSize: 16);
            // FIVE STARS, ALWAYS DRAWN. Somebody who has not rated the bar yet still gets
            // the row — greyed, with a question mark where the number goes — because a
            // blank box says "no such field" while five empty stars say "not yet".
            _idStars = new Image[5];
            _idStarFills = new Image[5];
            const float StarBox = 24f, StarGap = 2f;
            float starRun = 5f * StarBox + 4f * StarGap;
            for (int i = 0; i < 5; i++)
            {
                var s = NewRect("Star" + i, card);
                Place(s, new Vector2(0, 1), new Vector2(StarBox, StarBox), new Vector2(
                    LicCellX + (LicCellW - starRun) * 0.5f + i * (StarBox + StarGap),
                    -LicCells[1] - 24f));
                _idStars[i] = s.gameObject.AddComponent<Image>();
                _idStars[i].sprite = ItemArt.Load("star");
                _idStars[i].preserveAspect = true;
                _idStars[i].raycastTarget = false;

                // The lit half, over the grey whole: a star fills from its left edge, so a
                // 2.5 leaves the third star half amber instead of rounding a customer's
                // opinion up to a verdict they did not give (see ShowId).
                var f = NewRect("Lit", s);
                Stretch(f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _idStarFills[i] = f.gameObject.AddComponent<Image>();
                _idStarFills[i].sprite = _idStars[i].sprite;
                _idStarFills[i].type = Image.Type.Filled;
                _idStarFills[i].fillMethod = Image.FillMethod.Horizontal;
                _idStarFills[i].fillOrigin = (int)Image.OriginHorizontal.Left;
                _idStarFills[i].preserveAspect = true;
                _idStarFills[i].raycastTarget = false;
                _idStarFills[i].color = UITheme.Amber[3];
                _idStarFills[i].enabled = false;
            }

            // The licence number, on the rule that closes the rail.
            _idNumber = NewText("Num", card, _body, 8, TextAnchor.LowerCenter, UITheme.Night[3]);
            Place(_idNumber.rectTransform, new Vector2(0, 1), new Vector2(LicCellW, 12),
                new Vector2(LicCellX, -LicNumRule + 3f));
            _idNumber.rectTransform.pivot = new Vector2(0, 0);
            _idNumber.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── the numbered field grid ───────────────────────────────────────────
            // The numbers are what make a form read as a licence rather than as a label
            // printed on card stock, and they cost one character each.
            _idName = LicenceField(card, "1   NAME", LicFieldsX, LicLines[0], LicFieldsW, out _);
            _idAgeFrom = LicenceField(card, "2   AGE", LicFieldsX, LicLines[1], 100f, out _);
            _idCitizen = LicenceField(card, "3   CITIZEN OF", LicFieldsX + 130f, LicLines[1],
                LicFieldsW - 130f, out _);

            // The order, seated on its own rule with the glass drawn beside it.
            var idIcon = NewRect("OrderIcon", card);
            Place(idIcon, new Vector2(0, 1), new Vector2(30, 30), Vector2.zero);
            idIcon.pivot = new Vector2(0, 0);
            idIcon.anchoredPosition = new Vector2(LicFieldsX, -LicLines[2] + 2f);
            _idOrderIcon = idIcon.gameObject.AddComponent<Image>();
            _idOrderIcon.preserveAspect = true;
            _idOrderIcon.raycastTarget = false;
            _idOrder = LicenceField(card, "4   ORDER", LicFieldsX + 40f, LicLines[2],
                LicFieldsW - 40f, out _, 16);
            // What is IN it, under the name (v5 P16): the menu speaks styles now, so the
            // licence has to say gin-and-tonic, not just "Gin & Tonic" — this line is the
            // player's recipe knowledge since the band rows left with v2.
            // UNDER the order's own rule, which is where a sub-field belongs. It used to
            // share the serving-preferences caption row two rules down — the only place it
            // fitted on the old five-rule card — so what a drink was made of was printed
            // nowhere near the drink. The four-rule grid leaves 84 units under this rule
            // and the line needs twelve.
            _idOrderParts = NewText("OrderParts", card, _body, 8, TextAnchor.UpperLeft, UITheme.Night[3]);
            Place(_idOrderParts.rectTransform, new Vector2(0, 1), new Vector2(LicFieldsW, 14),
                new Vector2(LicFieldsX, -LicLines[2] - 6f));
            _idOrderParts.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Hovering the order shows the RECIPE (2026-07-31): the drink they asked for,
            // said the way the book says it — prep, pour shares, glass — without leaving
            // the card. The hit rect covers the order line, icon included.
            var orderHit = NewRect("OrderHit", card);
            Place(orderHit, new Vector2(0, 1), new Vector2(LicFieldsW, 52), Vector2.zero);
            orderHit.pivot = new Vector2(0, 0);
            orderHit.anchoredPosition = new Vector2(LicFieldsX, -LicLines[2] - 6f);
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
            _idIntent = LicenceField(card, "5   ENDORSEMENTS", LicFieldsX, LicLines[3],
                LicFieldsW, out _idIntentLabel, 12);
            _idPrefRow = NewRect("PrefRow", card);
            Place(_idPrefRow, new Vector2(0, 1), new Vector2(LicFieldsW, 44), Vector2.zero);
            _idPrefRow.pivot = new Vector2(0, 1);
            _idPrefRow.anchoredPosition = new Vector2(LicFieldsX, -LicLines[3] - 6f);
            var prefLayout = _idPrefRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            prefLayout.spacing = 8;
            prefLayout.childControlWidth = true; prefLayout.childForceExpandWidth = false;
            prefLayout.childControlHeight = true; prefLayout.childForceExpandHeight = false;
            prefLayout.childAlignment = TextAnchor.UpperLeft;

            var hint = NewText("Hint", _idRoot, _body, 12, TextAnchor.MiddleCenter, UITheme.TextSecondary);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400, 20),
                new Vector2(0, -(LicH * 0.5f) - 16f));
            hint.text = "CLICK OUTSIDE TO GIVE IT BACK";

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
            // ...but the HUD is built in a FIXED field inside it (2026-08-11, the author:
            // "boyutu değiştirdikçe arkaplana göre oturttuğumuz nesneler kayıyor —
            // bardaklar, butonlar, çöp kutusu, kasa"). Match-height pins the vertical and
            // lets the width run with the window, and everything here that stands ON the
            // room — the glass shelf, the bin, the till, the buttons — was placed against
            // a width that moved. The field is 1280x720 forever and the camera is
            // windowboxed to the same 16:9, so the props and the room they stand in are
            // measured in the same units at every window shape. See DesignFrame.
            var root = DesignFrame.Wrap((RectTransform)canvasGo.transform,
                                        new Vector2(1280f, 720f));
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
            // The BOARD bleeds to the window; the instruments on it do not (2026-08-11).
            // A fascia that stops at the safe frame reads as a strip floating in the middle
            // of a wide monitor, and one that carries its plaques out to the window edges
            // is the drift the frame was built to stop. So the dark board, its hairline and
            // its neon run edge to edge, and the clock, the till and the standing stay
            // anchored where they were placed inside the frame.
            var top = NewRect("TopBar", root);
            Stretch(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -TopBarH), Vector2.zero);
            // THE BOARD IS A BEAM CARRYING INSTRUMENTS (2026-08-19, the author: "bu
            // şeriti tekrardan tasarla ... hepsinin yeri profesyonelce konumlandırılmalı.
            // Haftalık takvim göstergesi daha profesyonelce olmalı"). The 2026-08-14 cut
            // ("kutu kutu" refused) put ONE case on the beam and let the week float as
            // bulbs on a wire — and the wire is what the author is looking at now: signage
            // reads as decoration, not as a panel. So the beam still runs edge to edge with
            // its lit face and its neon foot, and what stands on it is now TWO matched
            // instruments and one jewel:
            //   · the HOUR — a case and a dark glass, the digits hand-drawn pixel masks
            //     (SegmentClock, 2026-08-19: "gerçekten kodlar pixel pixel yapsak")
            //   · the WEEK — a GENERATED plate ("pixellabden arkaplan oluştur", the one
            //     written exception to chrome-is-never-generated): teal-capped navy metal,
            //     the counter at its head, the seven nights as lamps in a slotted row
            //     (BuildWeekStrip); calendar data lives in the calendar instrument
            //   · the STANDING — five 3D gold stars straight on the beam (their box was
            //     refused 2026-08-14 and stays refused; the vice-fade tint lasted one
            //     build and was refused too), no number
            //   · one key at the far end; NEW RUN lives inside it
            // Everything else stands directly on the beam, on one centre line.
            var fascia = Panel(top, "Fascia", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, UITheme.Night[1]);
            BleedWidth.Apply(fascia);
            Band(fascia, "Face", 0f, 3f, UITheme.Night[3]);            // the top the room lights
            Band(fascia, "Turn", 3f, 1f, new Color(0f, 0f, 0f, 0.45f)); // where it turns down
            Band(fascia, "Foot", TopBarH - 3f, 3f, UITheme.Night[0]);   // shadow gathering at the tube

            // The tube: a bright core over a wider glow, bleeding below the panel. It is also
            // THE STATE LIGHT now — the whole board burns magenta at last call, which says it
            // from across the room without a word on it (it used to be a 2px rule under one
            // plaque, which nobody was ever going to notice).
            var tube = NewRect("Neon", fascia);
            tube.anchorMin = new Vector2(0, 0); tube.anchorMax = new Vector2(1, 0);
            tube.pivot = new Vector2(0.5f, 0);
            tube.sizeDelta = new Vector2(0, 2);
            tube.anchoredPosition = Vector2.zero;
            _neonTube = tube.gameObject.AddComponent<Image>();
            _neonTube.color = UITheme.Amber[4]; _neonTube.raycastTarget = false;
            var bloom = NewRect("NeonBloom", fascia);
            bloom.anchorMin = new Vector2(0, 0); bloom.anchorMax = new Vector2(1, 0);
            bloom.pivot = new Vector2(0.5f, 1);
            bloom.sizeDelta = new Vector2(0, 5);
            bloom.anchoredPosition = Vector2.zero;
            _neonBloom = bloom.gameObject.AddComponent<Image>();
            _neonBloom.color = new Color(UITheme.Amber[2].r, UITheme.Amber[2].g, UITheme.Amber[2].b, 0.30f);
            _neonBloom.raycastTarget = false;

            // ── the hour, left: what the night is measured in ──────────────────
            //
            // A DIGITAL CLOCK IS ITS SEGMENTS, NOT ITS TYPEFACE (2026-08-14, the author:
            // "dijital saat olmamış hiç dijital saate benzememiş"). The first pass set the
            // hour in the UI's pixel face and laid a dim copy of it behind — a caption in
            // costume, and it read as one. What a readout actually is: four seven-bar digits,
            // the unlit bars still faintly on the glass, a colon keeping its own half-second.
            // Then it was too small to read ("saat okunaklı değil"), so the case is the full
            // height of the beam and the digits are 28 units tall — the biggest thing on the
            // board, which is right, because the night is measured in it. See SegmentClock.
            // THE WEEK MOVED OUT (2026-08-19). It shared this glass for a build ("saat ve
            // kaçıncı hafta olduğu düzgün bir sayfa düzeninde olsun", 2026-08-14) and came
            // out as a two-line caption squeezed against the digits — the cramped corner the
            // author called unprofessional. The week is calendar data and reads at the
            // calendar's own head now; this instrument holds the hour and nothing else.
            //
            // A WELL, NOT A CASE (the fourth cut, same evening: "profesyonel bir UI/UX
            // designer gibi düşün"). The case was a raised box with a glass inside — two
            // nested rectangles, and the author called it boxing. A well is ONE object cut
            // INTO the beam: its floor is the display glass, its top edge is the dark one
            // (light comes from above — a recess shades at the top, a box shines there),
            // and its bottom lip catches the room. See ChromeArt.Well.
            var clockWell = NewRect("Clock", top);
            Place(clockWell, new Vector2(0, 0.5f), new Vector2(134, 40), new Vector2(16, 0));
            var clockImg = clockWell.gameObject.AddComponent<Image>();
            clockImg.sprite = ChromeArt.Well();
            clockImg.type = Image.Type.Sliced;
            clockImg.raycastTarget = false;

            var digits = NewRect("Digits", clockWell);
            Place(digits, new Vector2(0, 0.5f), new Vector2(110, 28), new Vector2(12, 0));
            // The metrics stopped being arguments (2026-08-19): the digits are hand-drawn
            // 11×14 masks at exactly 2× now — see SegmentClock's own header — so the one
            // size that exists is the drawing's, and passing another would be a lie.
            _clock = new SegmentClock(digits, UITheme.Cyan[4]);

            BuildWeekStrip(top);

            // ── the till is not up here any more (2026-08-14, the author: "para ise
            // kasada olsun") ───────────────────────────────────────────────────
            // The money reads off the REGISTER in the room, where the drawer is, and every
            // rise and fall floats off it. A copy of the same number in the fascia was the
            // thing that made the till in the room decorative.

            // ── the standing, right: the stars and who they brought in ─────────
            // NO PLATE UNDER THEM (2026-08-14, the author: "yıldızlar hala üst barda kutu
            // içerisinde gösteriliyor" — still standing). Stars are already a shape; boxing
            // them made them a widget. They stand on the beam with the crowd named on the
            // caption line above.
            // AND NO NUMBER BESIDE THEM (2026-08-19, the author: "0.0 neden gösteriliyor,
            // daha çok görsel bir şerit olmalı"). The display-size decimal that shared the
            // row is gone — the fill fraction says the same thing visually, and the exact
            // figure still prints where figures belong, in the ledger and the market. The
            // block is just the five-star run now, right-aligned into the same 40-unit gap
            // off the key the old block kept.
            const float RightEdge = -16f;                     // the grid's outer margin
            const float BlockRight = RightEdge - 40f;
            float starsW = _ratingStars.Length * StarGap;

            var standing = NewRect("Standing", top);
            Place(standing, new Vector2(1, 0.5f), new Vector2(starsW, TopBarH), new Vector2(BlockRight, 0));
            standing.pivot = new Vector2(1, 0.5f);

            // Inside the block: the row sits low enough to clear its own caption and high
            // enough to clear the neon. 32 tall centred at -5 spans -21..+11 in a 54 beam.
            const float RowY = -5f, CapRowY = 18f;

            var starsRow = NewRect("Stars", standing);
            Place(starsRow, new Vector2(0, 0.5f), new Vector2(starsW, StarSize),
                new Vector2(0, RowY));
            starsRow.pivot = new Vector2(0, 0.5f);
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                var star = NewRect($"B{i}", starsRow);
                star.anchorMin = star.anchorMax = new Vector2(0, 0.5f);
                star.pivot = new Vector2(0.5f, 0.5f);
                star.sizeDelta = new Vector2(StarSize, StarSize);
                star.anchoredPosition = new Vector2(i * StarGap + StarGap * 0.5f, 0);
                var img = star.gameObject.AddComponent<Image>();
                // The socket is the same 3D drawing in dark glass — its own sprite, not a
                // tint: multiplying the gold star dark turns its facets to mud, and the
                // socket has to read as the same OBJECT unlit, shading intact.
                img.sprite = ItemArt.Load("star3d_socket");
                img.preserveAspect = true; img.raycastTarget = false;
                img.color = Color.white;
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
                // 3D GOLD, BAKED (2026-08-19, the author: "Yıldızlarda 3 boyutlu yıldız
                // iconlarından olsun fade geçiş rengini beğenmedim"). The vice-fade tint
                // lasted one build; the fill is the generated star's own gold now, so the
                // Image tints nothing — colour is IN the sprite, white multiplies clean.
                img.sprite = ItemArt.Load("star3d");
                img.preserveAspect = true; img.raycastTarget = false;
                img.color = Color.white;
                _ratingStars[i] = img;
            }

            // Centred over the block it belongs to, not right-aligned to one edge of it.
            _crowdText = NewText("Crowd", standing, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
            Place(_crowdText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(starsW + 90f, 12),
                new Vector2(0, CapRowY));
            _crowdText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── the quiet end: one key, and nothing else that is not the night ──
            // NEW RUN went inside it (2026-08-14, the author). A button that throws away the
            // night you are playing does not belong one thumb away from the thing you press
            // all night, and it was the last word up here competing with the readings.
            // 26, NOT 30. A key inlays its mark 5 units a side, and the cog is drawn at 16;
            // at 30 the mark comes out 20 wide, which is 1.25x of a 16-pixel drawing — the
            // cog arrived with its teeth at two different widths. Pixel art scales at whole
            // multiples or it does not scale (see the house rule about the 8px faces).
            var cogKey = NewButton(top, "SETTINGS", new Vector2(1, 0.5f), new Vector2(26, 26),
                new Vector2(RightEdge, 0), UITheme.Night[2], ToggleSettings, ChromeArt.Mark("cog"));
            Hairline(cogKey, new Vector2(0, 1), new Vector2(1, 1), UITheme.Night[3]);
            Hairline(cogKey, new Vector2(0, 0), new Vector2(1, 0), new Color(0f, 0f, 0f, 0.55f));
            HairlineV(cogKey, 0f, UITheme.Night[3]);
            HairlineV(cogKey, 1f, new Color(0f, 0f, 0f, 0.55f));
            BuildSettings(root);
            BuildOrderTip(root);

            // THE CURTAIN, above everything the HUD owns. Its own canvas at 30 so it also
            // covers the market (22), the licence (20) and the guide (24) — a night that
            // starts from black starts from black whatever was left open.
            _curtain = NewRect("Curtain", root);
            Stretch(_curtain, Vector2.zero, Vector2.one, new Vector2(-64, -64), new Vector2(64, 64));
            var curtainCanvas = _curtain.gameObject.AddComponent<Canvas>();
            curtainCanvas.overrideSorting = true;
            curtainCanvas.sortingOrder = 30;
            _curtainImg = _curtain.gameObject.AddComponent<Image>();
            _curtainImg.color = new Color(0f, 0f, 0f, 0f);
            _curtainImg.raycastTarget = false;   // black, not a wall: it never eats a click
            BuildCurtainCard(_curtain);
            _curtain.gameObject.SetActive(false);

            // BIN GLASS retired (v5 P13 / C7): a drink is thrown away by carrying it to the bin
            // on the counter, which is the same verb that serves it.

            // Refusal notices ("NOT ENOUGH MONEY") drop in just under the top bar. On their
            // OWN canvas above every override (2026-08-19): the market sorts at 22 and half
            // of what the toast says — "ONE UPGRADE A NIGHT", "BASKET IS EMPTY" — is said
            // IN the market, where a toast without its own order drew under the scrim and
            // the refusal was never seen at all.
            _toast = NewText("Toast", root, _display, 14, TextAnchor.MiddleCenter, UITheme.ViceRed[3]);
            Place(_toast.rectTransform, new Vector2(0.5f, 1), new Vector2(500, 30), new Vector2(0, -66));
            var toastCanvas = _toast.gameObject.AddComponent<Canvas>();
            toastCanvas.overrideSorting = true;
            toastCanvas.sortingOrder = 30;   // above the market (22), guide (24), bench (25)
            // …and it must never EAT one. A notice that sits above everything is a notice
            // that can swallow the click under it; it says a thing and takes nothing.
            _toast.raycastTarget = false;
            _toast.gameObject.SetActive(false);

            // Six stools along the counter: each customer is a bust sitting at the bar with a
            // floating order tag above their head; click anywhere on them to read or serve.
            const float seatStartX = 118f;
            for (int i = 0; i < SeatSlots; i++)
            {
                int index = i;
                var seat = new SeatView();
                seat.Index = i;
                seat.SeatX = seatStartX + i * SeatGap;

                // The click zone spans the bust and its tag; a clear image catches the ray.
                seat.Root = NewRect($"Seat{i}", root);
                seat.Root.anchorMin = seat.Root.anchorMax = new Vector2(0, 0);
                seat.Root.pivot = new Vector2(0.5f, 0);
                seat.Root.sizeDelta = new Vector2(150f, CharWinH + 110f);
                seat.Root.anchoredPosition = new Vector2(seat.SeatX, SeatLineY);
                var hit = seat.Root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);   // invisible, but catches clicks
                var button = seat.Root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OnSeatClicked(index));
                seat.Group = seat.Root.gameObject.AddComponent<CanvasGroup>();

                // THE CUSTOMER STANDS IN THE ROOM, NOT ON THE HUD (2026-08-10). They were an
                // Image inside a RectMask2D window on this canvas — and an overlay canvas is
                // composited after the camera, so no Light2D could ever reach them: the room
                // was lit and the people in it were not. The body is a world sprite now, and
                // the mask is gone with it, because the BAR takes their legs. Sorting 25 puts
                // them over the room and under the counter, which is the same crop the window
                // was faking, done by the object that would really do it.
                //
                // Everything else about a stool stays here: the click target, the order tag,
                // the patience gauge and the fade all belong to the interface, not the room.
                var stageForBody = stage != null ? stage : FindFirstObjectByType<DiegeticStage>();
                if (stageForBody != null)
                {
                    seat.Body = stageForBody.NewStageSprite($"Patron{i}", 25);
                    seat.Body.gameObject.SetActive(false);
                }

                // The order tag, floating above the head.
                // THE ORDER BUBBLE (2026-08-19). It was a flat tinted rectangle with one
                // neon rule under it; it is a drawn balloon now — the palette's white, a hot
                // magenta edge, and a spout pointing down at the head it belongs to
                // (ChromeArt.Bubble / BubbleTail, and see that file for why it is drawn
                // rather than generated).
                //
                // 9-SLICED, which is the whole reason it can carry a drawing at all. This
                // rect changes size on nearly every refresh — its width is its longest line,
                // its height is how many lines there are — so a picture hung on it plainly
                // would be stretched every time somebody ordered something with a long name.
                // Sliced, Unity draws the four real corners 1:1 at any size and repeats the
                // one-unit runs between them, and nothing distorts at any width or height.
                //
                // THE SPOUT IS NOT IN THAT SPRITE. A tail inside a stretched band smears
                // along it, so it is its own Image under the middle — the same split
                // BackBarArt's bottle card makes, for the same reason.
                seat.Tag = NewRect("Tag", seat.Root);
                seat.Tag.anchorMin = seat.Tag.anchorMax = new Vector2(0.5f, 0);
                seat.Tag.pivot = new Vector2(0.5f, 0);
                seat.Tag.sizeDelta = new Vector2(TagMinW, 40f);
                seat.Tag.anchoredPosition = new Vector2(0, CharWinH + TagLift);
                seat.TagBg = seat.Tag.gameObject.AddComponent<Image>();
                seat.TagBg.sprite = ChromeArt.Bubble();
                seat.TagBg.type = Image.Type.Sliced;
                seat.TagBg.raycastTarget = false;

                // The spout hangs BELOW the plate and overlaps it by its own three skirt
                // rows, which are plain fill and erase the plate's bottom band (two rows of
                // edge and the foot) exactly where the balloon should be open. Drawn at
                // 11x9, the size it was authored at, and never scaled — the one thing in
                // this ticket that must not be.
                var tailRt = NewRect("Tail", seat.Tag);
                tailRt.anchorMin = tailRt.anchorMax = new Vector2(0.5f, 0);
                tailRt.pivot = new Vector2(0.5f, 1);
                tailRt.sizeDelta = new Vector2(11f, 9f);
                tailRt.anchoredPosition = new Vector2(0, 3f);
                seat.Tail = tailRt.gameObject.AddComponent<Image>();
                seat.Tail.sprite = ChromeArt.BubbleTail();
                seat.Tail.raycastTarget = false;

                // THE THREE ROWS ARE ONE FACE AT ONE SIZE (2026-08-20, the author: "alkolün
                // yazdığı fontu değiştir diğerleriyle aynı yap") — the display face at 8, with
                // the NAME double-struck bold (PixelBold) so the heading is a weight rather
                // than a second typeface. The order row was the last thing set in the body
                // face; a ticket in two faces read as two objects stacked, and the name could
                // only out-rank a 16pt body row by being 16pt itself, which is what put a
                // 224-unit name on a 180-unit stool in the first place.
                //
                // The measurements that decided it are kept, because they are what rules the
                // alternatives out (8 or 16, never between — GDD 16 §0):
                //
                //   Silkscreen 8   caps ~5u   "SEX ON THE BEACH" 88u    ← the old ticket
                //   Press Start 8  caps  8u   "SEX ON THE BEACH" 128u
                //   Silkscreen 16  caps ~11u  "SEX ON THE BEACH" 176u
                //   Press Start 16 caps  16u  "SERENA FONTANA"   224u   ← the 236 cap, alone
                //
                // The last line is the whole overlap: ONE name in the display face at 16 is
                // wider than the gap between two stools, so every named customer pushed their
                // ticket over their neighbour's head. It cannot be fixed by moving the plate
                // — it has to not be that wide.
                //
                // So: every row takes the display face at 8 — 8-unit caps against the old
                // ticket's 5, bigger than what was called unreadable and half of what was
                // called too big — and "SEX ON THE BEACH" comes out 128 units, which leaves a
                // 142-unit ticket inside a 168 cap. Only the two longest drinks in the book
                // wrap, and the plate has always grown downward to take a second row.
                //
                // THE NAME IS THE TITLE, and now it is a WEIGHT (2026-08-20, the author:
                // "isimler kalın yazsın"): the same face and size as the rows under it,
                // double-struck one pixel wide by PixelBold — which is the only bold an 8px
                // face has, and is why FontStyle.Bold is not used (see that file).
                //
                // THE INKS TURNED OVER with the plate (2026-08-19). Cream, pale cyan and pale
                // magenta were chosen to sit on a near-black card; on a white one they are
                // invisible. Each row keeps its ROLE and takes the dark step of the same ramp:
                // the name is the room's own near-black, the status line is the plate's own
                // magenta, and the order is read blackest of all because it is the thing the
                // player is actually being asked to make.
                seat.Name = NewText("Name", seat.Tag, _display, 8, TextAnchor.UpperCenter,
                    UITheme.Night[1]);
                Stretch(seat.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -8));
                seat.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
                seat.Name.gameObject.AddComponent<PixelBold>().Distance = 1f;

                seat.Wants = NewText("Wants", seat.Tag, _display, 8, TextAnchor.UpperCenter,
                    UITheme.Magenta[1]);
                Stretch(seat.Wants.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -18));
                seat.Wants.horizontalOverflow = HorizontalWrapMode.Overflow;

                seat.Order = NewText("Order", seat.Tag, _display, 8, TextAnchor.UpperCenter,
                    UITheme.Night[0]);
                Stretch(seat.Order.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -28));
                seat.Order.horizontalOverflow = HorizontalWrapMode.Overflow;

                // ASK THE FONT how tall its line is, once, here — the faces and the sizes are
                // fixed from this point on. Every frame's plate is the sum of the rows it is
                // showing, so a row height guessed even two units wrong is a plate with the
                // type sitting off-centre in it, which is precisely what a constant did.
                seat.NameLineH = MeasuredLineHeight(seat.Name);
                seat.WantsLineH = MeasuredLineHeight(seat.Wants);
                seat.OrderLineH = MeasuredLineHeight(seat.Order);

                // WHAT THEY WANT, BESIDE HOW THEY WANT IT (2026-08-19, the author: "istenilen
                // alkol iconun yanında nasıl servis edilmesi isteniyorsa onlarında iconu |
                // ayrılık gösterilecek ... ikonografi ile anlatmak önemli"). One row under the
                // order: the drink's own glass, a rule, then one mark per thing on the serving
                // spec — ice, a twist, a salted rim, a sugared rim.
                //
                // The rule between them is DRAWN, a one-unit Image, not a typed "|". Nothing
                // in this project asks a font for a picture (ChromeArt's own header) and a
                // pipe character in the pixel face is a different height on every row it sits
                // beside. The whole row is laid out in RefreshSeats, because how wide it is
                // depends on how many things they asked for.
                var icons = NewRect("Icons", seat.Tag);
                icons.anchorMin = icons.anchorMax = new Vector2(0.5f, 1);
                icons.pivot = new Vector2(0.5f, 1);
                icons.sizeDelta = new Vector2(IconRowH, IconRowH);
                seat.IconRow = icons;

                var iconRt = NewRect("OrderIcon", icons);
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0, 0.5f);
                iconRt.pivot = new Vector2(0, 0.5f);
                iconRt.sizeDelta = new Vector2(24, 24);
                seat.Icon = iconRt.gameObject.AddComponent<Image>();
                seat.Icon.preserveAspect = true;
                seat.Icon.raycastTarget = false;

                var ruleRt = NewRect("Rule", icons);
                ruleRt.anchorMin = ruleRt.anchorMax = new Vector2(0, 0.5f);
                ruleRt.pivot = new Vector2(0, 0.5f);
                ruleRt.sizeDelta = new Vector2(1f, 14f);
                seat.IconRule = ruleRt.gameObject.AddComponent<Image>();
                seat.IconRule.color = UITheme.Magenta[1];
                seat.IconRule.raycastTarget = false;

                // TWO marks and no more. ServingSpec.Roll never asks for a third (65% one,
                // else two), and a row that can grow without a bound is a row that will one
                // day run off the plate it is centred on.
                seat.Garnish = new Image[2];
                for (int g = 0; g < seat.Garnish.Length; g++)
                {
                    var gr = NewRect("G" + g, icons);
                    gr.anchorMin = gr.anchorMax = new Vector2(0, 0.5f);
                    gr.pivot = new Vector2(0, 0.5f);
                    gr.sizeDelta = new Vector2(16, 16);
                    var gi = gr.gameObject.AddComponent<Image>();
                    gi.color = UITheme.Night[1];
                    gi.raycastTarget = false;
                    seat.Garnish[g] = gi;
                }
                // The patience gauge rides the BODY, not the ticket (P15, absorbs the P8
                // gauge item): a slim bar floating just over the head, so reading who is
                // about to walk means looking at the people, not at their paperwork.
                var clockBg = NewRect("ClockBg", seat.Root);
                clockBg.anchorMin = clockBg.anchorMax = new Vector2(0.5f, 0);
                clockBg.pivot = new Vector2(0.5f, 0);
                clockBg.sizeDelta = new Vector2(BustW * 0.72f, 8f);
                clockBg.anchoredPosition = new Vector2(0, CharWinH + 1f);
                clockBg.gameObject.AddComponent<Image>().color = UITheme.Night[0];
                // The gauge's length IS the value, and it is re-hung off each look's own head
                // every frame. Snapping either to whole units would make patience tick down in
                // visible steps and park the bar off the head it belongs to. See UiAuditExempt.
                UiAuditExempt.Mark(clockBg, "a patience gauge whose width is the value itself, "
                    + "re-hung on the customer's own head each frame — snapping it would make "
                    + "the clock tick in steps");
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
            NewButton(root, "MENU — MAKE A DRINK", new Vector2(0.5f, 0),
                new Vector2(300, 40), new Vector2(0, 180), UITheme.PrimaryAction, OnMenuClicked);

            // The recipe book, beside the making verb (v5 P16): the menu speaks styles now,
            // so how a drink is MADE has to live somewhere the player can read mid-shift.
            NewButton(root, "BOOK", new Vector2(0.5f, 0),
                new Vector2(84, 40), new Vector2(-196, 180), UITheme.Night[3], ToggleRecipeBook);
            BuildRecipeBook(root);

            BuildDrinkGlass(root);
            BuildSnackRow(root);
            BuildServiceLog(root);
            BuildIdCard(root);
            BuildLastCall(root);

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

            // THE NIGHT IS CALLED BEFORE IT IS COUNTED (2026-08-11, the author: at two in
            // the morning a line says the day is over, the room goes dark behind it, and
            // only once the words have gone does the till start printing). It is the beat
            // the sequence was missing — the slip used to arrive while the bar was still
            // lit, so the night ended and was totalled in the same movement.
            _lastCallCard = NewText("Called", _dayEndPanel, _display, 24, TextAnchor.MiddleCenter,
                UITheme.Amber[4]);
            Place(_lastCallCard.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 40),
                new Vector2(0, 10f));
            _lastCallCard.horizontalOverflow = HorizontalWrapMode.Overflow;
            _lastCallCard.text = "THAT'S LAST CALL";
            _lastCallCard.raycastTarget = false;
            var calledUnder = NewText("CalledSub", _dayEndPanel, _body, 16, TextAnchor.MiddleCenter,
                new Color(0.72f, 0.68f, 0.62f));
            Place(calledUnder.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 20),
                new Vector2(0, -22f));
            calledUnder.horizontalOverflow = HorizontalWrapMode.Overflow;
            calledUnder.text = "doors shut · counting the money";
            calledUnder.raycastTarget = false;
            _lastCallRt = NewRect("Call", _dayEndPanel);   // one group carries both lines
            Stretch(_lastCallRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _lastCallCard.transform.SetParent(_lastCallRt, false);
            calledUnder.transform.SetParent(_lastCallRt, false);
            _lastCallGroup = _lastCallRt.gameObject.AddComponent<CanvasGroup>();
            _lastCallGroup.blocksRaycasts = false;
            _lastCallRt.gameObject.SetActive(false);

            // Left column: the till slip (v5 P13). Cream stock, and pinned to 16pt — a whole
            // multiple of the face's 8px design size, so the monospace columns the receipt is
            // set in actually land on the pixel grid instead of blurring between it.
            // THE SLIP, CENTRED (2026-08-10). It hung from the top of the screen at a
            // fixed 470, so a quiet night left a hand's width of blank paper under the
            // total and the bill sat high on a screen that had room for it in the middle.
            // THE PAPER IS GENERATED NOW (2026-08-10, the author: the drawn sheet and its
            // marks read as filler). PixelLab painted a receipt torn off a roll — jagged at
            // both ends, warm, slightly handled — at 140x192, drawn here at a WHOLE 3x like
            // the licence, because pixel art magnifies only in whole steps. FIXED size, and
            // that is a receipt being honest: the paper is the roll's, the print is the
            // night's, and a quiet night leaves blank stock above the foot tear.
            var bill = _dayEndBill = NewRect("Bill", _dayEndPanel);
            Place(bill, new Vector2(0.5f, 0.5f), new Vector2(BillW, BillH), Vector2.zero);
            var sheet = bill.gameObject.AddComponent<Image>();
            sheet.sprite = ItemArt.Load("bill_sheet");
            if (sheet.sprite == null) { sheet.color = BillPaper; Frame(bill, 2f, BillEdge); }

            // The head is PRINTED, not banded: a receipt is one ink on one paper, and the
            // navy band belonged to the licence, not to a till roll.
            var headTitle = NewText("T", bill, _display, 24, TextAnchor.MiddleCenter, BillInk);
            Place(headTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(BillW - 60f, 26),
                new Vector2(0, -30f));
            headTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            headTitle.text = "LAST CALL";
            _billWhen = NewText("W", bill, _body, 16, TextAnchor.MiddleCenter, BillQuiet);
            Place(_billWhen.rectTransform, new Vector2(0.5f, 1), new Vector2(BillW - 60f, 20),
                new Vector2(0, -54f));
            _billWhen.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Every line lands in here, one rect to a row, so the columns cannot bend.
            _invoiceRows = NewRect("Rows", bill);
            _invoiceRows.anchorMin = new Vector2(0, 1); _invoiceRows.anchorMax = new Vector2(1, 1);
            _invoiceRows.pivot = new Vector2(0.5f, 1);
            _invoiceRows.sizeDelta = new Vector2(-BillInset * 2f, 0);
            _invoiceRows.anchoredPosition = new Vector2(0, -(BillHeadH + 12f));

            // The bill's OWN way forward (2026-08-07). The day-end button moved inside the
            // tablet, and the tablet is only up on the market step — which left the books
            // with no door out of them at all. The slip carries its own now.
            // ON THE SLIP'S FOOT, not at a fixed 530 down the panel: the bill is centred
            // and its length is the night's, so a fixed key sat on top of a short slip and
            // under a long one. RebuildDayEnd puts it where the paper actually ends.
            _billNext = NewRect("BillNext", _dayEndPanel);
            Place(_billNext, new Vector2(0.5f, 0.5f), new Vector2(BillW, 44), Vector2.zero);
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
            _billNextLabel.text = "CONTINUE";

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
            osBar.gameObject.AddComponent<Image>().color = new Color(0.867f, 0.878f, 0.918f, 1f);
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

            // THE TITLE BAR (2026-08-19). This is the storefront's whole identity in one
            // 40-unit run: the vice fade, the mark and wordmark on it, and the money. It
            // replaced a white app bar with a green wordmark on it.
            //
            // The fade is `ChromeArt.FadeStrip` — one texel per band, drawn point-filtered,
            // so however wide the bar gets it comes back as FLAT runs with a hard edge
            // between them: twenty-six of 40 across this 1040 (the author asked the first
            // take's eight for "daha smooth", and more bands is how a band set smooths —
            // never interpolation, 16 §6.10). No sprite is stretched into a smear either:
            // the failure the old sh_bar had was that its middle carried MARKS, and a flat
            // run has no detail to smear.
            var strip = NewRect("Strip", screen);
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1);
            strip.pivot = new Vector2(0.5f, 1);
            strip.sizeDelta = new Vector2(0, AppBarH);
            strip.anchoredPosition = new Vector2(0, -OsBarH);
            var stripImg = strip.gameObject.AddComponent<Image>();
            stripImg.sprite = ChromeArt.FadeStrip();
            stripImg.color = Color.white;
            // The bar stands ON the window, so it closes with the frame's own dark edge.
            Hairline(strip, new Vector2(0, 0), new Vector2(1, 0), ShopViceDeep);

            // The mark: PALM CARGO's palm on its island, drawn in ChromeArt like every mark
            // (chrome is never generated) and authored at exactly this 28x24 — 1x, no
            // preserveAspect needed. White on the fade, the same footing as the wordmark, so
            // mark and name read as one printing.
            var mark = NewRect("Mark", strip);
            Place(mark, new Vector2(0, 0.5f), new Vector2(28, 24), new Vector2(10, 0));
            var markImg = mark.gameObject.AddComponent<Image>();
            markImg.sprite = ChromeArt.Isle();
            markImg.raycastTarget = false;

            // The wordmark IS the logo, and it is the one place the display face earns its
            // width. "BOOZE CRUISE" = 12 x 16 = 192 in a 200 box. It sits WHITE on the fade
            // now: on a title bar the name is the brightest thing, and the fade is dark
            // enough at both ends (ClubBlue[2] 4.9:1, Magenta[3] 4.6:1) to carry it.
            const float BrandX = 46f;
            var brand = NewText("Brand", strip, _display, 16, TextAnchor.MiddleLeft, Color.white);
            Place(brand.rectTransform, new Vector2(0, 0.5f), new Vector2(200, 20),
                new Vector2(BrandX, 0));
            brand.horizontalOverflow = HorizontalWrapMode.Overflow;
            brand.text = ShopBrand;

            // NO WINDOW BOXES (2026-08-19). The bar had the era's three — min, max, close —
            // and the author sent them away: they were a misreading of "buttons like Windows
            // 98", which asked for the era's BUTTON STYLE, not its window furniture. Their
            // going is also honest UI: two of the three were drawn dead, and the live one
            // was a second door out of a market that already has its own key — the foot's
            // OPEN TOMORROW, which keeps the ClosingAsk guard. One exit, one key (16 §2).

            // The account, two rows so the word and the number cannot print through each
            // other — and cut as a SUNKEN well rather than wearing sh_k_account, which was
            // drawn for the green storefront and reads as a green plate on a pink bar.
            // A readout is a hole in the face, and the bevel is what says so (16 §1, GLASS).
            // Flush right at the boxes' old margin, since it is the bar's right-most thing.
            var balance = NewRect("Balance", strip);
            Place(balance, new Vector2(1, 0.5f), new Vector2(150, 32), new Vector2(-10, 0));
            var balanceImg = balance.gameObject.AddComponent<Image>();
            balanceImg.color = ShopViceDeep;
            Bevel(balance, 2f, raised: false);
            var balanceLabel = NewText("BalanceL", balance, _body, 8, TextAnchor.MiddleLeft,
                new Color(0.667f, 0.702f, 0.847f, 1f));   // 6.2:1 on the well
            Place(balanceLabel.rectTransform, new Vector2(0, 1), new Vector2(80, 10), new Vector2(10, -6));
            balanceLabel.text = "ACCOUNT";
            _tabletTill = NewText("Till", balance, _display, 16, TextAnchor.MiddleRight, Color.white);
            Place(_tabletTill.rectTransform, new Vector2(1, 0), new Vector2(126, 18), new Vector2(-10, 6));
            _tabletTill.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tabletTill.verticalOverflow = VerticalWrapMode.Truncate;
            _tillFloats = balance;   // the deltas fall out of the account box, below it
            // THE SHEET THE DEPARTMENT OPENS (the author: give it a filing feel, and let
            // the frame go round the products too). The aisle sits on a framed page whose
            // top edge runs UNDER the tabs: the lit tab is drawn with no bottom rim and
            // overlaps that edge by two units, so it does not sit beside the page, it is
            // attached to it. Four flat Images make the frame, which cannot distort at any
            // size — the thing the stretched sprites kept getting wrong.
            // THE SHEET IS THE AISLE'S OWN RECT (2026-08-11, the author: the box the
            // products are listed in and the area the listing is CUT at are not the same,
            // there is white between them). They were not: the page was a FIXED 416 tall
            // hanging from the tabs while the mask stretched to the foot, so the two only
            // ever agreed at one screen height — measured at 26 units of white below the
            // last row and 12 above the first, and the page running 2 units INTO the foot.
            //
            // It also means last night's gutter went to the wrong rect. The air belongs
            // between the PAGE and the foot, where it reads as a margin; putting it under
            // the mask just pushed the cut further up its own sheet.
            //
            // So there is one set of numbers now: the page hangs from the tabs and stops a
            // gutter clear of the foot, and the mask and the scroll track are inset inside
            // it by the same amount on every side.
            var page = NewRect("Page", screen);
            Stretch(page, Vector2.zero, Vector2.one,
                new Vector2(4f, FootH + FootY + AisleGutter),
                new Vector2(-4f, -(OsBarH + AppBarH + TabBarH - 4f)));
            page.gameObject.AddComponent<Image>().color = ShopPage;
            Frame(page, 2f, ShopViceDeep);
            // THE WALLPAPER (2026-08-19). A 90s trade site had one, and this one is the
            // room's own weather: a banded sun, a grid running back to it, two palms.
            // Drawn in code (`ChromeArt.PalmWall`) because chrome is never generated here.
            //
            // It is hung at 13% of the house blue and it has to stay there. The page's one
            // job is that forty products can be read on it, and the wallpaper is allowed to
            // be the thing you notice SECOND — the moment it competes with a bottle
            // silhouette it has stopped being a wall and started being noise. Measured on
            // the palest tile (PlateSealed): the type still clears 11:1 over it.
            HangWall(page, 1032, 390, new Color(ShopVice.r, ShopVice.g, ShopVice.b, 0.13f));

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
                // DRAWN, NOT DRAWN-ON (2026-08-10, the author's file brief). A sprite
                // cannot do the one thing a file tab has to do — carry a seam under the
                // tabs you are not reading and NO seam under the one you are — so the tabs
                // are rects with the page's own 2px outline, and the COLOUR does the work:
                //
                //   resting  face fill + the frame's own dark outline on all four sides,
                //            so its bottom edge is a visible seam lying on the page's border
                //   open     that dark colour as the FILL, so its outline vanishes into it
                //            and its bottom edge merges with the page's border — no seam
                //
                // The colour changed to the vice frame's ClubBlue[0] (2026-08-19) and the
                // trick did not: it never depended on the hue, only on the open tab's fill
                // and the page's border being the SAME colour. If those two ever drift
                // apart the seam comes back and the file stops being a file.
                //
                // Both draw at 2 units, the page's own thickness, on the page's own plane,
                // and the open one stands taller. Nothing is stretched, so nothing smears.
                var key = NewRect($"Tab{i}", tabBar);
                key.anchorMin = key.anchorMax = key.pivot = new Vector2(0, 0);
                key.sizeDelta = new Vector2(TabKeyW, TabRestH);
                key.anchoredPosition = new Vector2(8f + i * (TabKeyW + 8f), 0);
                var bg = key.gameObject.AddComponent<Image>();
                // RAISED, not framed (2026-08-19): a 98 site's tab is a button that stands
                // off the page, and the whole storefront speaks that button now. The lit
                // magenta edge stays — the bevel says "pressable", the edge says "open".
                Bevel(key, 2f, raised: true);
                // The lit top edge, shown only on the open tab (RefreshDayEnd switches it).
                var lit = NewRect("Lit", key);
                lit.anchorMin = new Vector2(0, 1); lit.anchorMax = new Vector2(1, 1);
                lit.pivot = new Vector2(0.5f, 1);
                lit.sizeDelta = new Vector2(-6f, 3f);
                lit.anchoredPosition = new Vector2(0, -2f);
                var litImg = lit.gameObject.AddComponent<Image>();
                litImg.color = ShopTabLit; litImg.raycastTarget = false;
                _shopTabLits[i] = litImg;
                var btn = key.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() =>
                {
                    if (_shopTab != tab) { _justOrdered.Clear(); _shopScrollAt = 1f; }
                    _shopTab = tab;
                    RebuildDayEnd();
                });
                // AT ITS OWN SIZE. The icons are cut on a 24 canvas and were drawn into a
                // 20 box: 0.833x, a fractional shrink of pixel art, which rounds some rows
                // away and keeps others. 24 in a 30-tall key is 1:1 and has room to breathe.
                var icon = NewRect("I", key);
                Place(icon, new Vector2(0, 0.5f), new Vector2(24, 24), new Vector2(8, 0));
                var iconImg = icon.gameObject.AddComponent<Image>();
                iconImg.sprite = ItemArt.Load(ShopTabIcons[i]);
                iconImg.preserveAspect = true; iconImg.raycastTarget = false;
                if (iconImg.sprite == null) iconImg.color = new Color(1, 1, 1, 0);
                _shopTabIcons[i] = iconImg;
                var label = NewText("L", key, _shop, 16, TextAnchor.MiddleLeft, ShopInk);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(40, 0), new Vector2(-6, 0));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.text = ShopTabs[i];
                _shopTabKeys[i] = bg;
                _shopTabLabels[i] = label;
                MarkHoverable(key, bg);
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
            // A GUTTER UNDER THE AISLE (2026-08-11, the author: there is no gap at all
            // between the shelf and the boxes under it, and it does not look professional).
            // There were eight units — sixteen screen pixels — between the mask's foot and
            // the inspector's head, which at a glance is the two panels touching. Three
            // times that is the difference between a page with a foot and a page that ran
            // out of room.
            var offerView = NewRect("OfferView", screen);
            Stretch(offerView, Vector2.zero, Vector2.one,
                new Vector2(8f, FootH + FootY + AisleGutter + PageInset),
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
            barTrack.sizeDelta = new Vector2(BarW,
                -(OsBarH + AppBarH + TabBarH + 8f + FootH + FootY + AisleGutter + PageInset));
            barTrack.anchoredPosition = new Vector2(-8f, -(OsBarH + AppBarH + TabBarH + 8f));
            barTrack.gameObject.AddComponent<Image>().color = ShopAisle;
            Bevel(barTrack, 1f, raised: false);   // a trough is cut INTO the face
            var scrollbar = barTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = NewRect("Handle", barTrack);
            Stretch(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = ShopVice;
            Bevel(handle, 2f, raised: true);      // and the thumb stands out of it
            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handle;
            shopScroll.verticalScrollbar = scrollbar;
            shopScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            // THE FOOT: 8 + 640 + 8 + 232 + 8 + 136 + 8 = 1040.
            var foot = NewRect("Foot", screen);
            foot.anchorMin = new Vector2(0, 0); foot.anchorMax = new Vector2(1, 0);
            foot.pivot = new Vector2(0.5f, 0);
            foot.sizeDelta = new Vector2(0, FootH);
            foot.anchoredPosition = new Vector2(0, FootY);

            // THE READING CARD, ON THE POINTER (2026-08-11, the author). The description
            // used to be a 560-wide dark slab bolted into the foot — two thirds of the bar
            // spent on a sentence you read once, while the basket it sat beside had 312
            // units to list a night's shopping in. A description belongs to the thing the
            // pointer is on, so it goes to the pointer, and the foot goes to the basket.
            //
            // Built on the day-end PANEL rather than the foot: it has to be able to sit
            // over the aisle it is describing, and a child of the foot would be clipped to
            // it. Nothing in it may take a raycast — the panel sits under the cursor, and a
            // graphic that answers the pointer reads as leaving the tile underneath, which
            // hides the panel, which hands the pointer back, many times a second.
            _shopCard = NewRect("ShopCard", _dayEndPanel);
            Place(_shopCard, new Vector2(0.5f, 0.5f), new Vector2(ShopCardW, 132), Vector2.zero);
            _shopCard.pivot = new Vector2(0, 1);
            var cardBg = _shopCard.gameObject.AddComponent<Image>();
            cardBg.color = InspectorBack;
            var cardEdge = new Color(ShopViceLit.r, ShopViceLit.g, ShopViceLit.b, 0.85f);
            Hairline(_shopCard, new Vector2(0, 0), new Vector2(1, 0), cardEdge);
            Hairline(_shopCard, new Vector2(0, 1), new Vector2(1, 1), cardEdge);
            HairlineV(_shopCard, 0f, cardEdge);
            HairlineV(_shopCard, 1f, cardEdge);

            // ONE TEXT COLUMN AND ONE ICON GUTTER (the author: align it, and use the
            // space). The icons keep a gutter of their own and EVERY text starts beside it.
            const float CardGutter = 10f, CardText = 34f;
            const float CardCol = ShopCardW - CardText - 10f;

            // The heaviest ink on the card: white, in the shop's bold face. Everything
            // else here is specification; this is the product.
            _cardIdentity = NewText("Identity", _shopCard, _shop, 16, TextAnchor.UpperLeft, Color.white);
            Place(_cardIdentity.rectTransform, new Vector2(0, 1), new Vector2(CardCol, 20),
                new Vector2(CardText, -8));
            _cardIdentity.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cardIdentity.verticalOverflow = VerticalWrapMode.Overflow;

            // Sizes UP from 8 (the author: the market's small print was unreadable). The
            // card is narrower than the slab was, so every line wraps sooner — and the card
            // grows down to fit rather than truncating, which is what the fixed slab did.
            _cardMeta = NewText("CardMeta", _shopCard, _body, 10, TextAnchor.UpperLeft, InspectorDim);
            Place(_cardMeta.rectTransform, new Vector2(0, 1), new Vector2(CardCol, 14),
                new Vector2(CardText, -30));
            _cardMeta.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cardMeta.verticalOverflow = VerticalWrapMode.Overflow;

            // A rule under the head, so the identity block and the description read as two
            // things rather than five loose lines on a slate.
            _shopCardRule = NewRect("Rule", _shopCard);
            Place(_shopCardRule, new Vector2(0, 1), new Vector2(ShopCardW - 20f, 1),
                new Vector2(CardGutter, -48));
            var ruleImg = _shopCardRule.gameObject.AddComponent<Image>();
            ruleImg.color = UITheme.ClubBlue[1];
            ruleImg.raycastTarget = false;

            // The product's own mark, in the gutter beside the identity.
            var cardMark = NewRect("Mark", _shopCard);
            Place(cardMark, new Vector2(0, 1), new Vector2(20, 20), new Vector2(CardGutter, -8));
            _cardMarkImg = cardMark.gameObject.AddComponent<Image>();
            _cardMarkImg.preserveAspect = true;
            _cardMarkImg.raycastTarget = false;

            _cardBody = NewText("CardBody", _shopCard, _body, 10, TextAnchor.UpperLeft, InspectorInk);
            Place(_cardBody.rectTransform, new Vector2(0, 1), new Vector2(CardCol, 40),
                new Vector2(CardText, -54));
            _cardBody.supportRichText = true;
            _cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cardBody.verticalOverflow = VerticalWrapMode.Overflow;

            for (int i = 0; i < 2; i++)
            {
                var icon = NewRect("BuffI" + i, _shopCard);
                Place(icon, new Vector2(0, 1), new Vector2(14, 14), new Vector2(CardGutter, -100f));
                var ii = icon.gameObject.AddComponent<Image>();
                ii.preserveAspect = true; ii.raycastTarget = false;
                var line = NewText("Buff" + i, _shopCard, _body, 10, TextAnchor.UpperLeft, InspectorInk);
                Place(line.rectTransform, new Vector2(0, 1), new Vector2(CardCol, 14),
                    new Vector2(CardText, -100f));
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                line.verticalOverflow = VerticalWrapMode.Overflow;
                if (i == 0) { _cardBuffAIcon = ii; _cardBuffA = line; }
                else { _cardBuffBIcon = ii; _cardBuffB = line; }
            }
            _shopCard.gameObject.SetActive(false);

            // THE POINTER GETS THE RECIPE (2026-08-10, the author). The inspector says what
            // a drink IS in a sentence; buying one is a question about the POUR, and a pour
            // is a table. The panel rides the cursor the way the licence's does, and for the
            // same reason: parked anywhere fixed it either covers the tile you are reading
            // or sits too far from it to belong to it.
            _shopSpec = NewRect("ShopSpec", _dayEndPanel);
            Place(_shopSpec, new Vector2(0.5f, 0.5f), new Vector2(ShopSpecW, 120), Vector2.zero);
            _shopSpec.pivot = new Vector2(0, 1);
            var specBg = _shopSpec.gameObject.AddComponent<Image>();
            specBg.color = new Color(InspectorBack.r, InspectorBack.g, InspectorBack.b, 0.97f);
            specBg.raycastTarget = false;
            var specEdge = new Color(ShopViceLit.r, ShopViceLit.g, ShopViceLit.b, 0.85f);
            Hairline(_shopSpec, new Vector2(0, 0), new Vector2(1, 0), specEdge);
            Hairline(_shopSpec, new Vector2(0, 1), new Vector2(1, 1), specEdge);
            HairlineV(_shopSpec, 0f, specEdge);
            HairlineV(_shopSpec, 1f, specEdge);
            _shopSpecBody = NewRect("Body", _shopSpec);
            Stretch(_shopSpecBody, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
            _shopSpec.gameObject.SetActive(false);

            // THE BASKET IS THE FOOT (2026-08-11, the author: "alt barı tamamen sepet olarak
            // kullanırız ... böylece çok ürün sepete eklenince gözükmeme problemini
            // kaldırırız"). 880 units instead of 312, and what is in it is DRAWN rather than
            // listed: the four names at 8 with "+2 more" under them were both unreadable and
            // a lie about how much the basket holds.
            var order = NewRect("Basket", foot);
            Place(order, new Vector2(0, 0.5f), new Vector2(BasketW, FootH), new Vector2(8, 0));
            order.gameObject.AddComponent<Image>().color = ShopPage;
            // The status panel of a 98 window, and it stands OUT of the frame — the basket
            // is the one surface on this page you commit from, so it is the one that is
            // raised rather than cut in.
            Bevel(order, 2f, raised: true);

            // THE HEAD BAND carries the whole control: what is in the basket, what it comes
            // to, and the key that buys it — one row, all at a size that reads.
            var orderHead = NewRect("BasketHead", order);
            Place(orderHead, new Vector2(0, 1), new Vector2(BasketW, 30), Vector2.zero);
            orderHead.gameObject.AddComponent<Image>().color = ShopViceDeep;
            var orderIcon = NewRect("BasketI", orderHead);
            Place(orderIcon, new Vector2(0, 0.5f), new Vector2(20, 20), new Vector2(10, 0));
            var orderIconImg = orderIcon.gameObject.AddComponent<Image>();
            orderIconImg.sprite = ItemArt.Load("sh_i_cart");
            orderIconImg.preserveAspect = true; orderIconImg.raycastTarget = false;
            _cartHeadLabel = NewText("BasketHL", orderHead, _shop, 16, TextAnchor.MiddleLeft, Color.white);
            Place(_cartHeadLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(220, 20),
                new Vector2(38, 0));
            _cartHeadLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _cartHeadLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _cartHeadLabel.text = "BASKET";

            // The number the player decides on, beside the key that spends it.
            _cartTotalLabel = NewText("TotalL", orderHead, _shop, 8, TextAnchor.MiddleRight, ShopPaper);
            Place(_cartTotalLabel.rectTransform, new Vector2(1, 0.5f), new Vector2(60, 12),
                new Vector2(-(CheckoutW + 130f), 0));
            _cartTotalLabel.text = "TOTAL";
            _cartTotal = NewText("BasketTotal", orderHead, _display, 16, TextAnchor.MiddleRight,
                Color.white);
            Place(_cartTotal.rectTransform, new Vector2(1, 0.5f), new Vector2(120, 20),
                new Vector2(-(CheckoutW + 8f), 0));
            _cartTotal.horizontalOverflow = HorizontalWrapMode.Overflow;
            _cartTotal.verticalOverflow = VerticalWrapMode.Overflow;

            _checkout = NewRect("Checkout", orderHead);
            Place(_checkout, new Vector2(1, 0.5f), new Vector2(CheckoutW, 26), new Vector2(-4, 0));
            var checkoutImg = _checkoutImg = _checkout.gameObject.AddComponent<Image>();
            // The 98 key, not sh_k_order (2026-08-19): the baked plate carried its own baked
            // bevel, and one baked bevel in a storefront of drawn ones is the odd man out.
            // Vice blue face — the money key keeps its colour, the face style is the change.
            checkoutImg.sprite = ChromeArt.Win98Key();
            checkoutImg.type = Image.Type.Sliced;
            checkoutImg.color = ShopVice;
            var checkoutBtn = _checkout.gameObject.AddComponent<Button>();
            checkoutBtn.targetGraphic = checkoutImg;
            checkoutBtn.onClick.AddListener(Checkout);
            // THE ONE KEY ON THIS DEVICE THAT SPENDS MONEY, and it looked like the rest of
            // the fascia (2026-08-14, the author: "satın alma butonu biraz daha dikkat edici
            // olmalı"). It does not get a new colour — the shop's green is the shop's green
            // — it gets a LAMP behind it that only burns when there is something to buy.
            // Nothing pulses on an empty basket, so the eye is pulled exactly when acting
            // is the right thing to do and never as decoration (GDD 16 §5, §6).
            _checkoutLamp = NewRect("Lamp", orderHead);
            Place(_checkoutLamp, new Vector2(1, 0.5f), new Vector2(CheckoutW + 14f, 40f),
                new Vector2(-4, 0));
            _checkoutLamp.SetAsFirstSibling();
            _checkoutLampImg = _checkoutLamp.gameObject.AddComponent<Image>();
            _checkoutLampImg.sprite = ChromeArt.LampGlow();
            _checkoutLampImg.raycastTarget = false;
            _checkoutLampImg.color = new Color(1f, 1f, 1f, 0f);
            MarkHoverable(_checkout, checkoutImg);
            _checkoutLabel = NewText("L", _checkout, _shop, 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(_checkoutLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(6, 0), new Vector2(-6, 0));
            _checkoutLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _checkoutLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _checkoutLabel.text = "PLACE ORDER";
            var checkoutPress = _checkout.gameObject.AddComponent<Win98Press>();
            checkoutPress.Face = checkoutImg;
            checkoutPress.Caption = _checkoutLabel.rectTransform;

            // THE ROW OF WHAT IS PICKED. Chips are built into it on every rebuild.
            _cartChips = NewRect("BasketChips", order);
            Place(_cartChips, new Vector2(0, 1), new Vector2(BasketW - 20f, FootH - 38f),
                new Vector2(10, -34));
            _cartEmpty = NewText("BasketEmpty", order, _body, 12, TextAnchor.MiddleLeft, ShopInkSoft);
            Place(_cartEmpty.rectTransform, new Vector2(0, 1), new Vector2(BasketW - 20f, 40),
                new Vector2(12, -46));
            _cartEmpty.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cartEmpty.verticalOverflow = VerticalWrapMode.Overflow;
            _cartEmpty.text = ShopIdleTip;

            // The way out, bottom right of the device — and since the title bar lost its
            // close box (2026-08-19) the ONLY way out, which is why it is the biggest key
            // on the device and the one amber thing on it. The 98 face replaced sh_k_exit's
            // baked bevel for the same reason the checkout dropped sh_k_order: one button
            // language per site.
            _openTomorrow = NewRect("OpenTomorrow", foot);
            Place(_openTomorrow, new Vector2(0, 0.5f), new Vector2(ExitW, FootH), new Vector2(896, 0));
            var otImg = _openTomorrow.gameObject.AddComponent<Image>();
            otImg.sprite = ChromeArt.Win98Key();
            otImg.type = Image.Type.Sliced;
            otImg.color = UITheme.PrimaryAction;
            var otBtn2 = _openTomorrow.gameObject.AddComponent<Button>();
            otBtn2.targetGraphic = otImg;
            otBtn2.onClick.AddListener(OnDayEndAdvance);
            MarkHoverable(_openTomorrow, otImg);
            _openTomorrowLabel = NewText("Label", _openTomorrow, _shop, 16, TextAnchor.MiddleCenter,
                UITheme.TextOnAmber);
            Stretch(_openTomorrowLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(6, 0), new Vector2(-6, 0));
            _openTomorrowLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _openTomorrowLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _openTomorrowLabel.text = "OPEN\nTOMORROW";
            var otPress = _openTomorrow.gameObject.AddComponent<Win98Press>();
            otPress.Face = otImg;
            otPress.Caption = _openTomorrowLabel.rectTransform;

            BuildClosingAsk(_dayEndTablet);

            _dayEndPanel.gameObject.SetActive(false);

            _bannerText = NewText("Closed", root, _display, 22, TextAnchor.MiddleCenter, UITheme.ViceRed[3]);
            Place(_bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 120), new Vector2(0, 60));
            _bannerText.gameObject.SetActive(false);

            BuildLedgerPanel(root);
            BuildGuide(root);
            BuildDevBench(root);
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
            title.text = "THE REGISTER — DAYS SO FAR";

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
                empty.text = "No days yet. Close a night first.";
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
                room.text = $"        {d.Served} served{walked} · {d.NightStars:0.0} stars on the night"
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
            int leaving = run.Day;             // read BEFORE the roll: the curtain names both
            run.ContinueToNextDay();
            _dayEndPanel.gameObject.SetActive(false);
            if (run.Phase == TycoonPhase.DayOpen)
            {
                _lastPhase = TycoonPhase.DayOpen;
                ApplyBarLook();
                OpenTheDoors(leaving, run.Day);
            }
        }

        // ── the curtain (2026-08-10) ────────────────────────────────────────────
        // The market shut and the next night was simply THERE, mid-tick, with the clock
        // already running and drinkers already walking in — the shop closing and the doors
        // opening were the same frame. A night should start from black: the room comes up,
        // and only when it is up does the clock move. The hold is what makes it a beat
        // rather than a flourish, and it is the one thing a fade alone cannot do.

        // THE DARK CARRIES THE DATE NOW (2026-08-14, the author: "gün başlarında ekran
        // kararıyordu, bu kararmaya hafta ve gün takvimi eklensin… hangi günden hangi güne
        // geçtiğimizi animasyonla belirtsin yazı ile").
        //
        // The blackout was two and a quarter seconds of nothing, which is a beat with no
        // content in it — long enough to feel, too short to say anything, and the one place
        // in the game where the player is guaranteed to be looking at the screen and NOT
        // doing anything. It carries the week and the hand-off between two nights: the name
        // of the night that just closed slides out under the night arriving, and the marquee
        // the top bar wears is drawn again here, one bulb going dark and the next lighting.
        //
        // Same wire, same bulbs, same BarCalendar the rules count in — the dark cannot say
        // Friday while the beam says Thursday, because neither is doing its own arithmetic.

        // ── the pointer, answered (2026-08-14) ──────────────────────────────────
        //
        // The author: "markette mouse bir butonun üstüne gelince belli olsun o buton." Only
        // ONE control on the whole tablet answered the pointer before today — the basket
        // chip, which carries a PressSink. Everything else took Unity's default 4% tint,
        // which on a green fascia is invisible.
        //
        // It brightens rather than LIFTS, and that is deliberate: the tiles live inside a
        // ScrollRect and the tabs are re-laid on every rebuild (their height and colour are
        // written per frame), so a component that moves a rect would spend its life arguing
        // with the code that places it. Warmth costs nothing and cannot fight a layout.
        // HoverRelay, not EventTrigger — EventTrigger implements IScrollHandler too, and
        // the aisle froze the day that was learned.

        private RectTransform _checkoutLamp;
        private Image _checkoutLampImg, _checkoutImg;

        /// <summary>
        /// The lamp behind PLACE ORDER, which only burns when there is an order to place.
        /// A slow breath rather than a blink: the key is asking to be pressed, not warning
        /// about anything, and the chrome language keeps the fast pulses for refusals.
        /// It also goes dark for the three seconds after a checkout, while the key itself
        /// reads ORDERED — the one moment pressing it again would do nothing.
        /// </summary>
        private void StepCheckoutLamp()
        {
            if (_checkoutLampImg == null) return;
            bool wants = _cart.Count > 0 && Time.unscaledTime >= _checkoutUntil;
            float breath = wants
                ? 0.35f + 0.30f * Mathf.Sin(Time.unscaledTime * 3.4f)
                : 0f;
            var c = UITheme.Lime[3];
            var now = _checkoutLampImg.color;
            float a = Mathf.Lerp(now.a, breath, 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime));
            _checkoutLampImg.color = new Color(c.r, c.g, c.b, a);
        }

        // ── the question at the door (2026-08-14) ───────────────────────────────

        private RectTransform _closingAsk;
        private Text _closingAskLine;

        /// <summary>
        /// The tablet's own dialog, built once and shown over the device rather than over
        /// the screen: what is at stake is on that device, so the question belongs on it.
        /// Two keys and no third — going back is the safe one and it sits where the eye
        /// lands first.
        /// </summary>
        private void BuildClosingAsk(RectTransform tablet)
        {
            _closingAsk = NewRect("ClosingAsk", tablet);
            Stretch(_closingAsk, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _closingAsk.gameObject.AddComponent<Image>();
            scrim.color = new Color(UITheme.ClubBlue[0].r, UITheme.ClubBlue[0].g, UITheme.ClubBlue[0].b, 0.78f);
            scrim.raycastTarget = true;   // a wall: nothing behind it may be clicked

            // THE 98 MESSAGE BOX (2026-08-19, the author: '"Close the Order?" kısmını da
            // windows 98 tarzına getir'). The question arrives as a little window OF the
            // site: raised panel, the vice fade for a title bar with the question ON it the
            // way that decade titled its dialogs, and two 98 keys. The head sits IN the bar
            // now rather than floating on the paper — a dialog names itself on its chrome.
            var card = NewRect("Card", _closingAsk);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(620, 220), Vector2.zero);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.sprite = ChromeArt.Win98Key();
            cardImg.type = Image.Type.Sliced;
            cardImg.color = ShopPaper;

            var askBar = NewRect("Bar", card);
            Place(askBar, new Vector2(0.5f, 1f), new Vector2(612, 28), new Vector2(0, -4));
            var askBarImg = askBar.gameObject.AddComponent<Image>();
            askBarImg.sprite = ChromeArt.FadeStrip();
            askBarImg.raycastTarget = false;

            var head = NewText("H", askBar, _shop, 16, TextAnchor.MiddleLeft, Color.white);
            Stretch(head.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10, 0), new Vector2(-10, 0));
            head.text = "CLOSE THE ORDER?";

            // THE WARNING IS THE POINT OF THIS BOX, so it is set like one (2026-08-19, the
            // author: kalin ve buyuk yazsin). It was the body face at 12 — a size the pixel
            // faces do not have at all (16 SS0: 8, 16 or 24, nothing else), so the one
            // sentence the dialog exists to make you read was also the softest thing in it.
            // The shop's bold face at 16 is 1x its design size and lands on the grid.
            _closingAskLine = NewText("L", card, _shop, 16, TextAnchor.UpperCenter, ShopInk);
            Place(_closingAskLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(540, 64),
                new Vector2(0, -56));
            _closingAskLine.horizontalOverflow = HorizontalWrapMode.Wrap;
            _closingAskLine.verticalOverflow = VerticalWrapMode.Overflow;

            // Two 98 keys and no third. GO BACK is the safe one and wears the vice blue —
            // on this site the coloured key is the one the house recommends; OPEN ANYWAY
            // stands on the plain face, a step quieter, exactly as able.
            var back = NewRect("Back", card);
            Place(back, new Vector2(0.5f, 0f), new Vector2(240, 44), new Vector2(-132, 34));
            var backImg = back.gameObject.AddComponent<Image>();
            backImg.sprite = ChromeArt.Win98Key();
            backImg.type = Image.Type.Sliced;
            backImg.color = ShopVice;
            var backBtn = back.gameObject.AddComponent<Button>();
            backBtn.targetGraphic = backImg;
            backBtn.onClick.AddListener(() =>
            {
                Sfx.Play("click", 0.6f);
                if (_closingAsk != null) _closingAsk.gameObject.SetActive(false);
            });
            var backLabel = NewText("L", back, _shop, 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backLabel.text = "GO BACK";
            MarkHoverable(back, backImg);
            var backPress = back.gameObject.AddComponent<Win98Press>();
            backPress.Face = backImg;
            backPress.Caption = backLabel.rectTransform;

            var anyway = NewRect("Anyway", card);
            Place(anyway, new Vector2(0.5f, 0f), new Vector2(240, 44), new Vector2(132, 34));
            var anywayImg = anyway.gameObject.AddComponent<Image>();
            anywayImg.sprite = ChromeArt.Win98Key();
            anywayImg.type = Image.Type.Sliced;
            anywayImg.color = ShopPaper;
            var anywayBtn = anyway.gameObject.AddComponent<Button>();
            anywayBtn.targetGraphic = anywayImg;
            anywayBtn.onClick.AddListener(() =>
            {
                if (_closingAsk != null) _closingAsk.gameObject.SetActive(false);
                PlayTabletOut();
            });
            var anywayLabel = NewText("L", anyway, _shop, 16, TextAnchor.MiddleCenter, ShopInk);
            Stretch(anywayLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            anywayLabel.text = "OPEN ANYWAY";
            MarkHoverable(anyway, anywayImg);
            var anywayPress = anyway.gameObject.AddComponent<Win98Press>();
            anywayPress.Face = anywayImg;
            anywayPress.Caption = anywayLabel.rectTransform;

            _closingAsk.SetAsLastSibling();
            _closingAsk.gameObject.SetActive(false);
        }

        private void ShowClosingAsk(string worry)
        {
            if (_closingAsk == null) { PlayTabletOut(); return; }   // never trap the player
            _closingAskLine.text = worry;
            _closingAsk.gameObject.SetActive(true);
            _closingAsk.SetAsLastSibling();
            Sfx.Play("click", 0.5f);
        }

        /// <summary>Make a control answer the pointer. One component per control, living on
        /// the control, so a rebuilt tile takes its highlight to the grave with it — a list
        /// held by the HUD would grow by one entry per tile per rebuild and never shrink.</summary>
        private static void MarkHoverable(RectTransform hit, Graphic face)
        {
            if (hit == null || face == null) return;
            if (hit.GetComponent<HoverWarm>() != null) return;
            var warm = hit.gameObject.AddComponent<HoverWarm>();
            warm.Face = face;
        }

        /// <summary>
        /// What is written in the dark: the week, the night handing over to the night, and
        /// the same marquee the beam wears. Built once and driven by StepCurtain — nothing
        /// here is created per day, because a blackout that allocates is a blackout that
        /// hitches on the one frame the player is only looking at it.
        /// </summary>
        private void BuildCurtainCard(RectTransform curtain)
        {
            _curtainCard = NewRect("DateCard", curtain);
            Place(_curtainCard, new Vector2(0.5f, 0.5f), new Vector2(560, 220), Vector2.zero);
            _curtainCardGroup = _curtainCard.gameObject.AddComponent<CanvasGroup>();
            _curtainCardGroup.alpha = 0f;
            _curtainCardGroup.blocksRaycasts = false;

            _curtainWeek = NewText("Week", _curtainCard, _body, 16, TextAnchor.UpperCenter,
                UITheme.TextSecondary);
            Place(_curtainWeek.rectTransform, new Vector2(0.5f, 1f), new Vector2(400, 20),
                new Vector2(0, -6));

            // The two names share one seat: the one leaving rides up out of it while the one
            // arriving comes up into it, so the eye follows a single word changing rather
            // than reading two.
            var seat = NewRect("Seat", _curtainCard);
            Place(seat, new Vector2(0.5f, 1f), new Vector2(560, 56), new Vector2(0, -40));

            var leaving = NewRect("Leaving", seat);
            Stretch(leaving, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _curtainLeavingGroup = leaving.gameObject.AddComponent<CanvasGroup>();
            _curtainLeaving = NewText("L", leaving, _display, 32, TextAnchor.UpperCenter,
                UITheme.TextSecondary);
            Stretch(_curtainLeaving.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var arriving = NewRect("Arriving", seat);
            Stretch(arriving, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _curtainArrivingGroup = arriving.gameObject.AddComponent<CanvasGroup>();
            _curtainArriving = NewText("A", arriving, _display, 32, TextAnchor.UpperCenter,
                UITheme.PrimaryAction);
            Stretch(_curtainArriving.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // The marquee, drawn the way the beam draws it: a wire that stops where the work
            // stops, a bulb under every open night, a shutter under the day off.
            var names = BarCalendar.WeekColumns;
            const float step = 60f;
            float left = -names.Length * step * 0.5f;
            float railY = -150f;

            var rail = NewRect("Rail", _curtainCard);
            Place(rail, new Vector2(0.5f, 1f), new Vector2(BarCalendar.OpenNights * step, 1f),
                new Vector2(left + BarCalendar.OpenNights * step * 0.5f, railY + 13f));
            var railImg = rail.gameObject.AddComponent<Image>();
            railImg.color = UITheme.Night[3]; railImg.raycastTarget = false;

            for (int i = 0; i < names.Length; i++)
            {
                float cx = left + i * step + step * 0.5f;
                bool open = i < BarCalendar.OpenNights;

                var stem = NewRect("S" + i, _curtainCard);
                Place(stem, new Vector2(0.5f, 1f), new Vector2(1, 8), new Vector2(cx, railY + 9f));
                var simg = stem.gameObject.AddComponent<Image>();
                simg.color = UITheme.Night[3]; simg.raycastTarget = false;
                simg.enabled = open;

                if (!open)
                    for (int s = 0; s < 4; s++)
                    {
                        var slat = NewRect("Shut" + s + "_" + i, _curtainCard);
                        Place(slat, new Vector2(0.5f, 1f), new Vector2(24, 2),
                            new Vector2(cx, railY - s * 5f));
                        var slatImg = slat.gameObject.AddComponent<Image>();
                        slatImg.color = UITheme.Night[3]; slatImg.raycastTarget = false;
                    }

                var glow = NewRect("G" + i, _curtainCard);
                Place(glow, new Vector2(0.5f, 1f), new Vector2(32, 32), new Vector2(cx, railY - 4f));
                var gimg = glow.gameObject.AddComponent<Image>();
                gimg.sprite = ChromeArt.LampGlow();
                gimg.raycastTarget = false; gimg.enabled = false;

                var bulb = NewRect("B" + i, _curtainCard);
                Place(bulb, new Vector2(0.5f, 1f), new Vector2(16, 16), new Vector2(cx, railY - 4f));
                var bimg = bulb.gameObject.AddComponent<Image>();
                bimg.sprite = ChromeArt.Lamp();
                bimg.color = UITheme.Night[2]; bimg.raycastTarget = false;

                var name = NewText("N" + i, _curtainCard, _body, 8, TextAnchor.UpperCenter,
                    UITheme.TextSecondary);
                Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(step, 12),
                    new Vector2(cx, railY - 26f));
                name.text = names[i];

                _curtainCells.Add((bimg, gimg));
            }
        }

        private RectTransform _curtain, _curtainCard;
        private Image _curtainImg;
        private Text _curtainWeek, _curtainLeaving, _curtainArriving;
        private CanvasGroup _curtainCardGroup, _curtainLeavingGroup, _curtainArrivingGroup;
        private readonly List<(Image bulb, Image glow)> _curtainCells =
            new List<(Image, Image)>();
        private int _curtainFrom = 1, _curtainTo = 1;

        private float _curtainT;          // seconds elapsed, 0 → CurtainTotal
        // Four movements on one clock, SIX SECONDS (2026-08-15, the author: "gün geçişinde
        // takvim gözüktüğü sahne daha yavaş aksın, şu an 3 saniye ise 6 saniye olsun").
        //
        // The first cut ran 3.4 — the length of a transition, which is what it was before it
        // had anything in it. With a date on it, it is a SCENE, and the two want opposite
        // things: a transition is over before you notice it, a scene waits for you. The extra
        // time is not spread evenly. The hand-off gets the most, because it is the only thing
        // moving and the only thing that says what changed; the hold nearly doubles, because
        // the whole point of putting a week and a night on the screen is that they be read.
        private const float CurtainFadeIn = 0.50f;   // black is instant; the card arrives
        private const float CurtainSwap = 1.60f;     // the hand-off between the two nights
        private const float CurtainHold = 1.90f;     // let it sit, so it is read
        private const float CurtainLift = 2.00f;     // card out, room up
        private const float CurtainTotal = CurtainFadeIn + CurtainSwap + CurtainHold + CurtainLift;

        /// <summary>True while the room is still coming up: the clock must not run.</summary>
        private bool DoorsClosed => _curtainT < CurtainTotal;

        private void OpenTheDoors(int leaving, int arriving)
        {
            if (_curtain == null) return;
            _curtainFrom = leaving;
            _curtainTo = arriving;
            _curtain.gameObject.SetActive(true);
            _curtain.SetAsLastSibling();
            _curtainT = 0f;
            _curtainImg.color = new Color(0f, 0f, 0f, 1f);
            if (_curtainWeek != null)
                _curtainWeek.text = "WEEK " + BarCalendar.WeekOf(arriving);
            if (_curtainLeaving != null)
                _curtainLeaving.text = BarCalendar.Name(BarCalendar.NightOf(leaving));
            if (_curtainArriving != null)
                _curtainArriving.text = BarCalendar.Name(BarCalendar.NightOf(arriving));
            StepCurtain();   // place everything before the first frame is drawn
        }

        private void StepCurtain()
        {
            if (_curtain == null || _curtainT >= CurtainTotal) return;
            _curtainT += Time.unscaledDeltaTime;
            float t = _curtainT;

            // The black itself: full until the lift, then eased away.
            float liftAt = CurtainFadeIn + CurtainSwap + CurtainHold;
            float lift = t <= liftAt ? 0f : Mathf.Clamp01((t - liftAt) / CurtainLift);
            _curtainImg.color = new Color(0f, 0f, 0f, (1f - lift) * (1f - lift) + (1f - lift) * 0.0f);

            // The card: in, held, and out ahead of the room so the last thing to go is black.
            float inK = Mathf.Clamp01(t / CurtainFadeIn);
            float outK = Mathf.Clamp01((t - liftAt) / (CurtainLift * 0.55f));
            if (_curtainCardGroup != null) _curtainCardGroup.alpha = inK * (1f - outK);

            // THE HAND-OFF. The night that closed slides up and out; the night arriving
            // comes from under it. Smoothstep both ways — a linear slide reads as a scroll.
            float swap = Mathf.Clamp01((t - CurtainFadeIn) / CurtainSwap);
            float e = swap * swap * (3f - 2f * swap);
            // A BATON PASS, NOT A DISSOLVE. Both names crossfading on the same curve put
            // WEDNESDAY and THURSDAY at half alpha on top of each other for a third of a
            // second, and two 32pt words in one seat read as damage, not as a change. The
            // one leaving is gone before the one arriving is legible, and the travel is
            // bigger than the type so they clear each other rather than pass through.
            float goes = Mathf.Clamp01(swap / 0.55f);
            float comes = Mathf.Clamp01((swap - 0.45f) / 0.55f);
            goes = goes * goes * (3f - 2f * goes);
            comes = comes * comes * (3f - 2f * comes);
            if (_curtainLeavingGroup != null)
            {
                _curtainLeavingGroup.alpha = 1f - goes;
                ((RectTransform)_curtainLeavingGroup.transform).anchoredPosition =
                    new Vector2(0f, 4f + 46f * goes);
            }
            if (_curtainArrivingGroup != null)
            {
                _curtainArrivingGroup.alpha = comes;
                ((RectTransform)_curtainArrivingGroup.transform).anchoredPosition =
                    new Vector2(0f, 4f - 46f * (1f - comes));
            }

            // The marquee under it: last night's bulb goes out as tonight's comes up.
            int from = (int)BarCalendar.NightOf(_curtainFrom);
            int to = (int)BarCalendar.NightOf(_curtainTo);
            for (int i = 0; i < _curtainCells.Count; i++)
            {
                var (bulb, glow) = _curtainCells[i];
                if (bulb == null) continue;
                bool closed = i >= BarCalendar.OpenNights;
                bulb.enabled = !closed;
                if (closed) { if (glow != null) glow.enabled = false; continue; }
                float lit = i == to ? e : i == from ? 1f - e : 0f;
                // Worked nights keep the dull glass they wear on the beam; the two in the
                // hand-off ride the curve between dull and burning.
                bool worked = i < to;
                var cold = worked ? UITheme.Night[3] : UITheme.Night[2];
                bulb.color = Color.Lerp(cold, UITheme.Amber[4], lit);
                if (glow != null)
                {
                    glow.enabled = lit > 0.01f;
                    var g = UITheme.Amber[3];
                    glow.color = new Color(g.r, g.g, g.b, g.a * lit);
                }
            }

            if (_curtainT >= CurtainTotal)
            {
                _curtainT = CurtainTotal;
                // All the way to clear before it goes. The step that crosses the finish
                // returns early on the NEXT frame, so whatever alpha the last computed frame
                // happened to land on — six percent, measured — was the last thing drawn.
                _curtainImg.color = new Color(0f, 0f, 0f, 0f);
                _curtain.gameObject.SetActive(false);
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
            // THE 98 PANEL (2026-08-19, the author: "ürün kartlarını kötü buluyorum" — the
            // chamfered Card read as a washed grey lozenge on a grey page). A listing is a
            // small raised panel of the site now, square-cornered with the era's two-step
            // bevel, tinted with the state's paper — so its edges are shades of the
            // listing's own colour, same rule the Card obeyed, sharper object wearing it.
            img.sprite = ChromeArt.Win98Key();
            img.type = Image.Type.Sliced;
            img.color = PlateOf(state);

            // The click. A tile that cannot be acted on gets no Button at all, so the
            // pointer itself says whether there is anything here to do.
            if (spec.OnClick != null)
            {
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                var act = spec.OnClick;
                button.onClick.AddListener(() => act());
                // …and only THEN does it warm under the pointer. A sealed crate or a bottle
                // with no cash behind it lights up for nobody: hover says "you are pointing
                // at this", and a listing that cannot be acted on must not answer as though
                // it can (2026-08-14).
                MarkHoverable(rt, img);
            }

            // Hovering fills the inspector — the one place long text is allowed to live.
            // NOT an EventTrigger: it implements IScrollHandler too, so it ate the mouse
            // wheel and froze the aisle over every tile that had something to read.
            var shown = spec;
            var hover = rt.gameObject.AddComponent<HoverRelay>();
            hover.Entered = () => { ShowShopCard(shown); ShowShopSpec(shown.Recipe); };
            hover.Exited = () => { ShowShopCard(null); ShowShopSpec(null); };

            // 0 — THE BRAND CAP: four units of the vice fade across the panel's head,
            // inside the bevel. Every listing carries the storefront's one signature, the
            // way every window of a 98 site carried its bar — and at four units it is a
            // COLOPHON, not a title bar: it names the shop, it says nothing about the
            // listing, so the fade's chrome-only law holds.
            var cap = NewRect("Cap", rt);
            Place(cap, new Vector2(0, 1), new Vector2(TileW - 4f, TileCapH), new Vector2(2f, -2f));
            var capImg = cap.gameObject.AddComponent<Image>();
            capImg.raycastTarget = false;
            if (state == TileState.Sealed)
            {
                // A SEALED CRATE LOSES THE STOREFRONT'S COLOURS (2026-08-19, the author:
                // "kilitli olanların tepesindeki fade şerit yerini gri bir şerit alsın").
                // The fade is the shop saying "this is ours to sell"; on a crate the shop
                // will not open, it was the one cheerful thing on an otherwise chained card.
                //
                // Cream[1] and not a Graphite step: Graphite and Brick are ARCHITECTURE ONLY
                // (14 v3 §3) and may never carry a signal, and "you cannot buy this" is a
                // signal. Cream is under no such rule and its second step is a true grey.
                capImg.color = UITheme.Cream[1];
            }
            else capImg.sprite = ChromeArt.FadeStrip();

            // 1 — (THE STRIP IS GONE, 2026-08-19, the author: "kartin solunda yesil,
            // kirmizi, kahverengi dik serit tasarimi cok AI duruyor" — and the small box on
            // its top-left corner with it.)
            //
            // They were the same mistake twice. A coloured bar welded to an edge and a
            // coloured square welded to a corner are not things in a bar's world; they are
            // the house style of a dashboard, and 16 §6.8 has the name for it — a dot
            // standing in for an object. Worse, they were the ONLY two channels that said
            // the state, so between them they took a fact that deserved a sentence and gave
            // it two pieces of decoration.
            //
            // What the state says is a ROW now, on the grid with the name and the meta:
            // a mark and a word (row 4a below). Shape and language carry it, colour comes
            // along, and the plate's own tint is still underneath — three channels, none of
            // them bolted to an edge.

            // 2 — THE PRODUCT, on the shelf line every class shares.
            // Glassware is mostly transparent, and transparent on a white page is nothing
            // at all (the author: the glasses disappear). A vessel gets a recess to stand
            // in — a shaded back with a lit lip at the foot line — the way the bar's own
            // back shelf gives them something to be seen against. Bottles are opaque and
            // need none of it.
            if (spec.Art != null && spec.ArtH == VesselH)
            {
                var niche = NewRect("Niche", rt);
                Place(niche, new Vector2(0.5f, 0), new Vector2(116, 112),
                    new Vector2(0, ProductFootY - 4f));
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
                    ni.color = new Color(0.714f, 0.729f, 0.784f, 1f);
            }
            if (spec.Art != null)
            {
                var thumb = NewRect("Art", rt);
                PlaceProduct(thumb, spec.Art, spec.ArtH);
                var ti = thumb.gameObject.AddComponent<Image>();
                ti.sprite = spec.Art;
                ti.raycastTarget = false;
                ti.color = state == TileState.Unaffordable ? new Color(0.78f, 0.80f, 0.80f, 0.55f)
                    : state == TileState.Held ? new Color(0.855f, 0.871f, 0.918f, 0.85f)
                    : Color.white;
            }
            else if (state == TileState.Sealed)
            {
                // A crate the house will not open, and it is the WHOLE tile that is shut:
                // the chains run corner to corner (the author — a 78px X in the middle read
                // as an ornament, not as something chained), drawn at the tile's own
                // 160x208 so no link is stretched into an oval, with the padlock where they
                // cross. No product and no name: the empty well is the tell.
                // AT ITS OWN SIZE, centred (2026-08-19). sh_chain_x is drawn at 160x208 and
                // the card is 230 now, so stretching it to fill would pull every link into
                // an oval — the exact fault the art was cut at the tile's size to avoid.
                var chain = NewRect("Chain", rt);
                Place(chain, new Vector2(0.5f, 0.5f), new Vector2(160f, 208f), Vector2.zero);
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
                    new Color(0.624f, 0.647f, 0.729f, 1f));
                Place(what.rectTransform, new Vector2(0.5f, 1), new Vector2(96, 12),
                    new Vector2(0, -27));
                what.horizontalOverflow = HorizontalWrapMode.Wrap;
                what.verticalOverflow = VerticalWrapMode.Truncate;
                what.text = string.IsNullOrEmpty(spec.GateNote) ? "STARS TO OPEN" : spec.GateNote;
            }
            else
            {
                // NOT THE BOLD FACE (2026-08-11, the author: change whatever font the
                // product names are set in). It is the same complaint the receipt's figures
                // had: Silkscreen Bold is drawn on an 8px grid with NO side bearing, so its
                // letters touch at every size and a name reads as one long shape. The two
                // ways out are a lighter weight or a face that carries its own gap;
                // PressStart2P carries its gap inside the cell, which is why it is the one
                // of the three that can be set solid — and at 8 it is exactly 1x its design
                // size, so it lands on the pixel grid perfectly.
                //
                // It is wider — a flat 8 units a character against the bold's 6.25 mixed —
                // so the column holds 17 characters a line instead of 22. The box is two
                // lines, and the longest name on the shelf is 24 characters, which is what
                // the measurement below had to settle before this shipped.
                // THE NAME PLATE (2026-08-19, the author: "isimlerini kutu içerisine al
                // veya ön plana çıkması için bir şey yap ... daha okunaklı ve kalın bir
                // font"). The name was 8px PressStart2P lying loose on the card, one grey
                // among four other greys, and on a shelf of twelve listings the thing you
                // are actually shopping BY was the quietest reading on the plate.
                //
                // It is a NAME PLATE now, and that is not a box invented to hold it: the
                // back bar of this game has name plates on its rails, so the aisle borrows
                // the object the room already owns (16 §6, the positive form). Dark field,
                // light type — the one inversion on the card, which is why the eye lands on
                // it first.
                var namePlate = NewRect("NamePlate", rt);
                Place(namePlate, new Vector2(0, 1), new Vector2(TileW - 8f, TileNameH),
                    new Vector2(4f, -TileNameTop));
                var nameBg = namePlate.gameObject.AddComponent<Image>();
                // ONE PLATE FOR EVERY STATE. The first cut dimmed it for the listings you
                // cannot buy — Cream[0] under Cream[2] type, which measures 2.6:1 and shipped
                // a name you had to lean in to read. A name is not a state: what a bottle is
                // called does not change because the till is empty. The card's own tint and
                // the red NO CASH key carry that, and the plate stays legible.
                nameBg.color = ShopViceDeep;
                nameBg.raycastTarget = false;

                // SILKSCREEN BOLD AT 16, not PressStart2P at 8. Both halves of that matter:
                // twice the size, and the bold face. The standing objection to this face for
                // product names (2026-08-11) was that its letters touch — true, and it is a
                // complaint about 8px, where the gap it does not have is a whole pixel of a
                // six-pixel glyph. At 16 the same face is 2x its design size, every letter
                // lands on the grid, and light-on-dark parts them by contrast anyway.
                var name = NewText("Name", namePlate, _shop, 16, TextAnchor.UpperLeft,
                    Color.white);
                Stretch(name.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(6, 3), new Vector2(-6, -3));
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                name.text = spec.Name;
            }

            // 4 — ONE contextual token, or the stock meter where stock IS the fact.
            if (spec.StockFrac >= 0f)
            {
                // A METER YOU CAN ACTUALLY READ (2026-08-10, the author). It was a 6-unit
                // hairline with an 8pt percentage floating to its right: at a glance you
                // could tell "some" from "none" and nothing else. Now it is 12 deep with a
                // dark surround, so the bar has an edge to be read against, and the number
                // rides ON it in the shop's bold face — one object, one reading.
                // A GAUGE DOWN THE SIDE (2026-08-11, the author: stand the meter up beside
                // the bottles, and the bottle stays centred in its box).
                //
                // It was a 136-wide bar lying at -170, and the ADD pill sits 6..30 up from
                // the tile's foot — the bar's own bottom edge is 24 up from it, so the two
                // shared six units and the percentage printed into the key. Standing it up
                // does not just move the collision, it removes the row they were fighting
                // over: the strip runs the height of the ART, where there is nothing else,
                // and reads like the level in the bottle it is standing next to. The art is
                // an overlay on the left, so nothing about the bottle's placement changes.
                // ONE GAUGE, ONE SIZE, ON THE RIGHT EDGE (2026-08-11, the author's second
                // ruling, and the better one). Pinning it to each bottle's own drawing put
                // it where the product was — but that means it MOVES: a page of tiles then
                // has six gauges at six different x's and six different heights, and an
                // instrument you have to find on every card is not an instrument. Fixed and
                // flush right, it is the same stripe in the same place on every plate, and
                // the eye can run down a column of them and compare.
                //
                // It still clears the ADD key by construction: the strip's foot is at the
                // product's own foot line, 68 up from the plate, and the key lives in the
                // bottom 30.
                float frac = Mathf.Clamp01(spec.StockFrac);
                const float StripW = 12f;
                const float StripH = TileArtH - 8f;
                const float StripX = TileW - TilePad - StripW;
                const float StripTop = -(TileArtTop + 4f);
                var surround = NewRect("Track", rt);
                Place(surround, new Vector2(0, 1), new Vector2(StripW, StripH),
                    new Vector2(StripX, StripTop));
                var surroundImg = surround.gameObject.AddComponent<Image>();
                surroundImg.color = UITheme.ClubBlue[0];
                surroundImg.raycastTarget = false;

                var well = NewRect("Well", rt);
                Place(well, new Vector2(0, 1), new Vector2(StripW - 4f, StripH - 4f),
                    new Vector2(StripX + 2f, StripTop - 2f));
                var wellImg = well.gameObject.AddComponent<Image>();
                wellImg.color = new Color(0.792f, 0.812f, 0.871f, 1f);
                wellImg.raycastTarget = false;

                // THE LEVEL WEARS THE FADE (2026-08-19, the author: "restock doluluk barı da
                // yeşil yerine bu renk olsun"). The fill is the vertical FadeStrip CROPPED
                // by a Filled image, never squeezed: the rect spans the whole well and
                // fillAmount reveals the bottom `frac` of it, so a half bottle shows the
                // fade's blue half and a full one the whole blue-into-pink run — the level
                // climbs INTO the pink the way the evening climbs into the neon. Geometry
                // still carries the reading (height is the fraction); the fade only dresses
                // it, which is what keeps the chrome-only law honest here.
                //
                // The one signal stays a signal: under a quarter the level drops the fade
                // for flat ShopCost red, because "nearly out" is a warning and a warning is
                // never worn as decoration.
                var fill = NewRect("Fill", well);
                Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var fillImg = fill.gameObject.AddComponent<Image>();
                if (frac < 0.25f)
                {
                    fill.anchorMax = new Vector2(1, 0);
                    fill.pivot = new Vector2(0.5f, 0);
                    fill.sizeDelta = new Vector2(0, (StripH - 4f) * frac);
                    fill.anchoredPosition = Vector2.zero;
                    fillImg.color = ShopCost;
                }
                else
                {
                    fillImg.sprite = ChromeArt.FadeStrip(horizontal: false);
                    fillImg.type = Image.Type.Filled;
                    fillImg.fillMethod = Image.FillMethod.Vertical;
                    fillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
                    fillImg.fillAmount = frac;
                }
                fillImg.raycastTarget = false;

                // The reading moved onto the STATE ROW (2026-08-19), right-aligned. It used
                // to float at the card's top-right corner, which put a number in the one
                // band that carries no words — and left the state row half empty. Words
                // about the listing all live on one line now: what it is on the left, how
                // much of it there is on the right.
                var pct = NewText("Pct", rt, _shop, 8, TextAnchor.MiddleRight, ShopInkSoft);
                Place(pct.rectTransform, new Vector2(1, 1), new Vector2(60, TileStateH),
                    new Vector2(-TilePad, -TileStateTop));
                pct.raycastTarget = false;
                pct.text = Mathf.RoundToInt(frac * 100f) + "%";
            }
            else if (!string.IsNullOrEmpty(spec.Meta) && state != TileState.Sealed)
            {
                var meta = NewText("Meta", rt, _body, 8, TextAnchor.MiddleLeft, TileMetaInk);
                Place(meta.rectTransform, new Vector2(0, 1), new Vector2(ContentW, TileMetaH),
                    new Vector2(TilePad, -TileMetaTop));
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
                //
                // ON A TAG (2026-08-19, the author: "fiyatını gösteren yazıyı bir fiyat
                // etiketi içerisine al"). A number set loose in the bottom-left corner was
                // the last thing on this card still being a caption rather than an object;
                // ChromeArt.PriceTag is the card of stock a shop hangs on a bottle's neck,
                // 9-sliced so "$8" and "+$105" are one drawing at two widths.
                //
                // AMBER, which is not decoration: money is Amber and only money is (16 §5),
                // so the tag is the sacred colour doing exactly its job. Out of reach, it
                // drops to the ramp's dark step — still plainly a price tag, plainly one you
                // cannot pay, and the key beside it says NO CASH in red.
                var tag = NewRect("PriceTag", rt);
                Place(tag, new Vector2(0, 1), new Vector2(62, 16f),
                    new Vector2(TilePad, -(TileFootTop + 2f)));
                var tagImg = tag.gameObject.AddComponent<Image>();
                tagImg.sprite = ChromeArt.PriceTag();
                tagImg.type = Image.Type.Sliced;
                tagImg.color = state == TileState.Unaffordable || state == TileState.Held
                    ? UITheme.Amber[0] : UITheme.Money;
                tagImg.raycastTarget = false;

                // The type sits in the tag's BODY, clear of the nib and its punch hole —
                // the 11 units the 9-slice keeps for the point are 11 units of no man's land.
                var money = NewText("Money", tag, MoneyFace(spec.Money), 16, TextAnchor.MiddleLeft,
                    state == TileState.Unaffordable || state == TileState.Held
                        ? UITheme.Amber[4] : UITheme.TextOnAmber);
                Stretch(money.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(13, 0), new Vector2(-4, 0));
                money.horizontalOverflow = HorizontalWrapMode.Overflow;
                money.verticalOverflow = VerticalWrapMode.Truncate;
                money.text = spec.Money;
            }
            else if (!string.IsNullOrEmpty(spec.Word) && state != TileState.Held)
            {
                // Held says it on the sash across the product, so the action row stays
                // empty — printing FULL twice on one tile is the habit this rewrite was
                // supposed to break.
                var word = NewText("Word", rt, _shop, 16, TextAnchor.MiddleLeft, MoneyInk(state));
                Place(word.rectTransform, new Vector2(0, 1), new Vector2(62, TileFootH),
                    new Vector2(TilePad, -TileFootTop));
                word.horizontalOverflow = HorizontalWrapMode.Wrap;
                word.verticalOverflow = VerticalWrapMode.Truncate;
                word.text = spec.Word;
            }

            if (hasPill && !string.IsNullOrEmpty(spec.PillVerb))
            {
                // A KEY YOU COULD PRESS (2026-08-11, the author: "ADD butonu çok yapay
                // duruyor"). The generated pill was a flat lozenge with a word on it — a
                // picture of a button. This one is drawn with an edge and a throw: two dark
                // rows under the face, so it stands above the card instead of being printed
                // on it. The label rides one pixel up, off the throw.
                var pill = NewRect("Pill", rt);
                Place(pill, new Vector2(1, 1), new Vector2(70, TileFootH),
                    new Vector2(-TilePad, -TileFootTop));
                var pillImg = pill.gameObject.AddComponent<Image>();
                // The 98 key face (2026-08-19), same drawing as every button on this site.
                pillImg.sprite = ChromeArt.Win98Key();
                pillImg.type = Image.Type.Sliced;
                pillImg.color = PillOf(state);
                pillImg.raycastTarget = false;
                var label = NewText("L", pill, _shop, 8, TextAnchor.MiddleCenter, PillInk(state));
                Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(6, 0), new Vector2(-6, 0));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.text = spec.PillVerb;
                // The TILE is the click, so the tile's press turns ITS key inside out —
                // the pointer lands anywhere on the listing and the ADD key answers.
                if (spec.OnClick != null)
                {
                    var press = rt.gameObject.AddComponent<Win98Press>();
                    press.Face = pillImg;
                    press.Caption = label.rectTransform;
                }
            }

            // 6 — the picked tile is the only one wearing a frame on all four sides.
            if (state == TileState.Picked) Frame(rt, 2f, StripPicked);

            // (THE SASH IS GONE, 2026-08-19. A dark band printed FULL across the bottle
            // while the gauge beside it read 100% and the corner chip wore an "=" — the
            // same fact three times on one card, which is §6.5 exactly. The state row says
            // it once, in the place every card says everything.)

            // 7 — THE STATE, IN A ROW (2026-08-19). This is what replaced the edge strip
            // and the corner chip, and like the chip before it, it is built LAST so nothing
            // on the card can draw over the one line that says what the listing is doing.
            //
            // The card had four channels for saying what a listing was doing — a strip hue,
            // a chip glyph, a plate tint, and whether a control existed — and two of them
            // were decoration welded to the plate's edges. They are one LINE now, sitting on
            // the grid between the meta and the foot, built the way every other line on this
            // page is built: a drawn mark, then a word, then the reading right-aligned.
            //
            // It costs nothing in what the player can tell apart. Shape says it (each state
            // has its own mark), language says it (each has its own word), the plate's tint
            // still says it underneath, and the ADD key's presence still says it — so the
            // colour-blind path reads on three channels instead of on a hue down an edge.
            // And it gains the thing the strip could never give: a listing you can READ.
            string stateWord = StateWordOf(state);
            if (!string.IsNullOrEmpty(stateWord))
            {
                float markX = TilePad;
                var ink = StateInk(state);
                var markArt = StateMark(state);
                RectTransform markRt = null;
                if (markArt != null)
                {
                    markRt = NewRect("StateMark", rt);
                    Place(markRt, new Vector2(0, 1), new Vector2(TileStateH, TileStateH),
                        new Vector2(markX, -TileStateTop));
                    var mi = markRt.gameObject.AddComponent<Image>();
                    mi.sprite = markArt;
                    mi.preserveAspect = true;
                    mi.raycastTarget = false;
                    mi.color = ink;
                    markX += TileStateH + 4f;
                }
                var stateText = NewText("State", rt, _shop, 8, TextAnchor.MiddleLeft, ink);
                // IT STOPS SHORT OF THE READING. The stock percentage shares this row and
                // is right-aligned to the same margin, so a word allowed to overflow would
                // print straight through it on the restock tab — where a full shelf and an
                // empty wallet are exactly the two states that have most to say. 44 units
                // is what "100%" takes in the shop face plus a space to breathe.
                const float ReadingCol = 44f;
                Place(stateText.rectTransform, new Vector2(0, 1),
                    new Vector2(TileW - markX - TilePad - ReadingCol, TileStateH),
                    new Vector2(markX, -TileStateTop));
                stateText.horizontalOverflow = HorizontalWrapMode.Wrap;
                stateText.verticalOverflow = VerticalWrapMode.Truncate;
                stateText.text = stateWord;
                // The van still lands. It lands on the ROW rather than on a corner square:
                // the stamp belongs to the thing that says "ordered", and that is this line.
                if (state == TileState.Ordered && !Motion.Reduced)
                    StartCoroutine(StampDrop(markRt != null ? markRt : stateText.rectTransform));
            }
            return rt;
        }

        // ── the state language, in one place ─────────────────────────────────────
        // Seven answers, seven rows. Keeping them as switches beside each other is what
        // makes "no two states may look alike" checkable rather than hopeful.

        private static Color PlateOf(TileState s) =>
            s == TileState.Unaffordable ? PlateDeny
            : s == TileState.Picked ? PlatePicked
            : s == TileState.Ordered ? PlateOrdered
            : s == TileState.Sealed ? PlateSealed
            : s == TileState.Refundable ? PlateReturn
            : s == TileState.Held ? ShopAisle
            : s == TileState.NoFitting ? PlateDeny
            : ShopPage;

        /// <summary>
        /// WHAT THE STATE SAYS, in words, on the card's own state row. Seven answers, seven
        /// rows, beside StateInk and StateMark so "no two states may look alike" stays
        /// checkable by reading three lists side by side.
        ///
        /// Orderable is the empty one and that is deliberate: a listing you can simply buy
        /// has an ADD key on it saying so, and a row underneath repeating "IN STOCK" is the
        /// same fact twice (16 §6.5). The row appears when there is something to say.
        /// </summary>
        private static string StateWordOf(TileState s) =>
            s == TileState.Picked ? "IN BASKET"
            : s == TileState.Ordered ? "ON THE VAN"
            : s == TileState.Refundable ? "SEND BACK"
            : s == TileState.NoFitting ? "NO ROOM"
            : s == TileState.Held ? "SHELF FULL"
            // Sealed is the THIRD state that already says it (2026-08-19, seen in play): a
            // sealed crate is laid out on its own — chains corner to corner, a padlock where
            // they cross, and a tag hung under it reading the star gate — so a state row put
            // a fourth "LOCKED" on the card, in the bottom-left, printed over the chains.
            // A tile with its own layout does not also get the standard one.
            //
            // Unaffordable is the second one the key already says (2026-08-19, the author:
            // "No Cash buton üzerinde kırmızı şekilde yazsa yeterli olur"). Its key is right
            // there in the foot reading NO CASH, so a state row saying NO CASH again put the
            // same two words on one card twice — §6.5, the habit this card keeps relapsing
            // into. The key says it, in red, and that is the whole answer.
            : null;               // Orderable, Unaffordable, Sealed — all said elsewhere

        /// <summary>
        /// THE STATE ROW'S INK. Not the Strip* set — those were drawn to be a SOLID BAR, and
        /// this row is type on a near-white plate. Measured: the picked amber lands at 1.9:1
        /// and the held grey at 2.5:1, so half the states would have shipped as a word nobody
        /// could read. Each state keeps its own hue and takes the dark step of it.
        /// </summary>
        private static Color StateInk(TileState s) =>
            s == TileState.Picked ? UITheme.Amber[1]
            : s == TileState.Ordered ? ShopViceDeep
            : s == TileState.Sealed ? StripSealed
            : s == TileState.Refundable ? UITheme.ClubBlue[2]
            : s == TileState.NoFitting || s == TileState.Unaffordable ? ShopCost
            : TileMetaInk;                     // Held, and anything unlisted

        /// <summary>
        /// The drawing beside the word. Two drawers answer here and that is fine: the market
        /// has its own glyph set for the things only it has (the van, the crate's padlock,
        /// the return arrow), and ChromeArt carries the marks the whole game shares. What
        /// matters is that every state on the row HAS one — the two that used to letter their
        /// chip, "!" for no cash and "=" for a full shelf, were letters only because a 20x20
        /// square had room for nothing else.
        /// </summary>
        private static Sprite StateMark(TileState s)
        {
            string art = GlyphSpriteOf(s);
            if (art != null) return ItemArt.Load(art);
            return s == TileState.Unaffordable ? ChromeArt.Mark("tips")
                : s == TileState.Held ? ChromeArt.Mark("stock")
                : null;
        }

        private static string GlyphSpriteOf(TileState s) =>
            s == TileState.Picked ? "sh_g_tick"
            : s == TileState.Ordered ? "sh_van"
            : s == TileState.Sealed ? "sh_lock"
            : s == TileState.Refundable ? "sh_g_back"
            : s == TileState.NoFitting ? "sh_b_lock"
            : null;                            // the rest are ChromeArt marks (StateMark)

        private static Color PillOf(TileState s) =>
            s == TileState.Orderable ? StripStock
            : s == TileState.Picked ? StripPicked
            : s == TileState.Refundable ? StripReturn
            : new Color(0.720f, 0.720f, 0.700f, 1f);

        private static Color PillInk(TileState s) =>
            s == TileState.Picked ? PickedInk
            : s == TileState.Orderable || s == TileState.Refundable ? Color.white
            // NO CASH is now the only place the card says it, so it says it in the refusal
            // colour rather than in the dead grey a disabled key wears. 4.6:1 on the key's
            // own face — a word the player is meant to read, not a greyed-out label.
            : s == TileState.Unaffordable ? ShopCost
            : new Color(0.24f, 0.24f, 0.22f, 1f);

        private static Color MoneyInk(TileState s) =>
            s == TileState.Unaffordable ? StripDeny
            : s == TileState.Picked ? PickedInk
            : s == TileState.Ordered ? ShopVice
            : s == TileState.Refundable ? StripReturn
            : s == TileState.Held || s == TileState.Sealed ? ShopInkSoft
            : ShopViceDeep;

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

        /// <summary>A key with a word on it — or, when <paramref name="icon"/> is handed one,
        /// with a DRAWN mark on it instead. A button too small for its word used to borrow a
        /// glyph from the font (the cog was a "⚙"), which no pixel face carries: it arrived
        /// from whatever fallback the system had, at a weight belonging to no other control
        /// on the screen.</summary>
        private RectTransform NewButton(RectTransform parent, string label, Vector2 anchor,
            Vector2 size, Vector2 pos, Color fill, Action onClick, Sprite icon = null)
        {
            var rt = NewRect(label, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var button = rt.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            // A face of its own, so the hover lift moves the label with the plate rather than
            // sliding the plate out from under it (PressSink moves one transform).
            var face = NewRect("Face", rt);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // THE ONE KEY (GDD 16 §2). This used to be a flat coloured rect — the third of
            // four button dialects the game was speaking at once.
            KeyPlate.Dress(rt, fill, button, face);

            Color ink = fill == UITheme.PrimaryAction ? UITheme.TextOnAmber : UITheme.TextPrimary;
            if (icon != null)
            {
                // A MARK IS SQUARE OR IT IS NOTHING (GDD 16 §3). Insetting the bottom by the
                // throw made the box 16x13, and `preserveAspect` then fitted the 16px drawing
                // into 13 — 0.813x, the exact defect this key was made to fix one commit ago.
                // The mark keeps its square and is LIFTED off the throw instead.
                var mark = NewRect("Mark", face);
                Stretch(mark, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
                mark.anchoredPosition += new Vector2(0, KeyPlate.Throw * 0.5f);
                var mi = mark.gameObject.AddComponent<Image>();
                mi.sprite = icon; mi.color = ink; mi.preserveAspect = true; mi.raycastTarget = false;
                return rt;
            }

            // 8 OR 16, NEVER 12. The pixel faces rasterise at whole multiples of their 8px
            // design size and nowhere else (CLAUDE.md), and every worded key in the game was
            // drawn at 12 — a size that face does not have — so it arrived softened on the
            // buttons the player presses all night. Found by `LastCall → Audit UI`.
            //
            // The size comes from the KEY, not from one global guess: a tall key is a primary
            // action and carries the bigger face, a short one would be crowded by it. 8 alone
            // left MENU — MAKE A DRINK as a whisper inside a 300-unit amber slab.
            int labelSize = rt.sizeDelta.y >= 32f ? 16 : 8;
            var text = NewText("Label", face, _body, labelSize, TextAnchor.MiddleCenter, ink);
            // Riding up off the throw, the way the market's key has always set its caption.
            Stretch(text.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, KeyPlate.Throw), new Vector2(-4, 0));
            text.text = label;
            return rt;
        }

        /// <summary>How tall ONE line of this Text is, asked of the font itself. Measured
        /// with a capital and an descender in it and with wrapping off, so the answer is the
        /// line box and never a wrapped paragraph's height.</summary>
        private static float MeasuredLineHeight(Text t)
        {
            var hadText = t.text;
            var hadWrap = t.horizontalOverflow;
            t.text = "Xg";
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            float h = Mathf.Ceil(t.preferredHeight);
            t.text = hadText;
            t.horizontalOverflow = hadWrap;
            return h;
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

        /// <summary>
        /// A 98 BEVEL: four edges, lit top and left, shadowed right and bottom — or the
        /// other way round, which is the whole difference between a control that stands up
        /// and a well that is cut into the page (16 §1: "a bevel is four of them").
        ///
        /// It is built from rects and not from a sliced sprite on purpose. A bevel is the
        /// one piece of chrome whose thickness must NOT scale with the thing it is on: two
        /// units on a 1040-wide page and two units on a 60-wide key, or the window stops
        /// reading as one machine. Four flat Images cannot distort at any size.
        ///
        /// The corners overlap rather than mitre. At two units nobody can see a mitre, and
        /// every 16-colour toolkit this is quoting overlapped them too.
        /// </summary>
        private void Bevel(RectTransform parent, float t, bool raised)
            => Bevel(parent, t, raised ? BevelLit : BevelShade, raised ? BevelShade : BevelLit);

        private void Bevel(RectTransform parent, float t, Color topLeft, Color bottomRight)
        {
            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i < 2;
                // i: 0 bottom, 1 top, 2 left, 3 right. Top and left take one colour.
                bool lit = i == 1 || i == 2;
                var rt = NewRect("Bev", parent);
                rt.anchorMin = horizontal ? new Vector2(0, i) : new Vector2(i - 2, 0);
                rt.anchorMax = horizontal ? new Vector2(1, i) : new Vector2(i - 2, 1);
                rt.pivot = new Vector2(horizontal ? 0.5f : i - 2, horizontal ? i : 0.5f);
                rt.sizeDelta = horizontal ? new Vector2(0, t) : new Vector2(t, 0);
                rt.anchoredPosition = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.color = lit ? topLeft : bottomRight;
                img.raycastTarget = false;
            }
        }

        /// <summary>
        /// The wallpaper, hung under whatever is handed in. Faint on purpose: it is the
        /// thing you notice on the second look, and the aisle has to win the first one.
        /// Drawn at half size and stretched x2 (`ChromeArt.PalmWall`), so it stays pixel art.
        /// </summary>
        private void HangWall(RectTransform parent, int w, int h, Color tint)
        {
            var rt = NewRect("Wall", parent);
            Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.PalmWall(w, h);
            img.color = tint;
            img.raycastTarget = false;
            rt.SetAsFirstSibling();   // under the aisle, not over it
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
        /// <summary>Where a tile stands its product: the drawing's foot, up from the plate.
        /// 80, not 68 (2026-08-19): the card grew a state row, and 12 units had to come from
        /// somewhere. They come from the art, which had the most to give — the product still
        /// gets 124 of the card's 208 and is still the biggest thing on it.</summary>
        private const float ProductFootY = TileH - TileArtTop - TileArtH;

        private static void PlaceProduct(RectTransform rt, Sprite s, float boxH)
        {
            // Fitted and stood by the DRAWING, not by the sheet it was saved on (VesselArt,
            // 2026-08-11). A product saved with air around it — the juice cartons are, on a
            // 96x168 sheet — used to spend the box on that air: it drew a third small and then
            // hovered above the shelf line every other product on the page stands on. For art
            // that fills its sheet, which is most of it, this is the same arithmetic as before.
            var m = VesselArt.Of(s);
            float w = s.rect.width, h = s.rect.height;
            float k = Mathf.Min(ContentW / m.Drawing.width, boxH / m.Drawing.height);
            if (k >= 1f) k = Mathf.Floor(k);
            Place(rt, new Vector2(0.5f, 0f), new Vector2(w * k, h * k),
                new Vector2((w * 0.5f - m.Drawing.center.x) * k, ProductFootY - m.Drawing.y * k));
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
