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
    public sealed partial class TycoonHud : MonoBehaviour
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
        /// <summary>What the fascia's neon is currently saying: 0 the shift, 1 last call,
        /// 2 in the red. -1 until the first frame paints it. One writer, one cache — see
        /// RefreshChrome's own note about the two that used to fight over it.</summary>
        private int _beamState = -1;
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
        /// How far the room is riding right now, in HUD units — the counter's cellar lifting
        /// the whole world (DiegeticStage.SetDrawerOpen).
        ///
        /// ONE READING, because everything that stands on the bar has to take the same one:
        /// the stools, the recipe book, the empties left on the counter, and the takings
        /// floating over a stool. Each of those used to add the same three terms by hand and
        /// the ones that FORGOT are exactly the bugs this is here to end — a glass that stays
        /// put while the counter under it climbs is a glass hanging in the air.
        /// </summary>
        private float CounterLift => stage != null
            ? stage.DrawerPhase * DiegeticStage.DrawerTravel * StageToHud : 0f;

        /// <summary>
        /// How much of that lift the DRINKERS give back, so the beam does not take their
        /// heads (2026-08-25, the author: "tezgah açılınca müşterilerin kafası üst bara
        /// deymeyecek şekilde çok az müşterilerde aşağı insin").
        ///
        /// DERIVED from the cast, not tuned: the tallest head in the room is the one drawn
        /// highest on the rig (the smallest HeadY), the field is 720 units tall and the top
        /// bar owns the last TopBarH of it. This is what the lift has to give back for that
        /// head to clear the board with a little air, and it is 0 the moment the cast, the
        /// beam or the drawer's travel changes enough not to need it.
        /// </summary>
        private const float HeadAir = 6f;

        private static float _cellarSeatDrop = float.NaN;

        private static float CellarSeatDrop
        {
            get
            {
                // Read once and kept: the cast is a static table and the answer cannot move
                // inside a session. It cannot be a field initialiser — PatronCast is declared
                // below this and static initialisers run in the order they are written, so
                // that reading would be of a null array.
                if (float.IsNaN(_cellarSeatDrop))
                {
                    float highestHead = float.MaxValue;      // the SMALLEST HeadY is the tallest
                    foreach (var entry in PatronCast)
                        highestHead = Mathf.Min(highestHead, entry.HeadY);
                    if (highestHead > CharCanvas) highestHead = 0f;
                    // The same reading PatronLook.HeadTop makes, off the cast table instead
                    // of off one look, so the two cannot drift.
                    float tallest = (CharCanvas - highestHead) * (CharSize / CharCanvas)
                                    - CharFootDrop;
                    _cellarSeatDrop = Mathf.Max(0f,
                        SeatLineBaseY + DiegeticStage.DrawerTravel * StageToHud + tallest
                        - (DesignFrame.StageHeight * StageToHud - TopBarH - HeadAir));
                }
                return _cellarSeatDrop;
            }
        }

        /// <summary>
        /// Where a stool's rect actually sits. NOT a constant since the counter grew a cellar
        /// (2026-08-22): opening it lifts the whole room, and the author's mock-ups lift the
        /// drinkers with it — their heads sit 121 art px higher in the open frame, the same
        /// travel as the room. This is the ONE place that has to know, because the tag rides
        /// the seat rect as a child and the BODY is derived from the same anchoredPosition
        /// every frame, so both follow from this number and cannot drift apart.
        ///
        /// They ride it a little SHORT of the room, though — see CellarSeatDrop.
        /// </summary>
        private float SeatLineY
        {
            get
            {
                float lift = CounterLift;
                float phase = stage != null ? stage.DrawerPhase : 0f;
                return SeatLineBaseY + lift - CellarSeatDrop * phase;
            }
        }

        private const float BustW = 108f;

        /// <summary>How far apart the stools stand along the counter. It was a local const
        /// inside the builder; it is up here because the order ticket's width cap is DERIVED
        /// from it, and the two numbers agreeing by hand is exactly how the tickets came to
        /// overlap.</summary>
        private const float SeatGap = 180f;

        /// <summary>
        /// THE ORDER THE ROOM FILLS ITS STOOLS IN (2026-08-25, the author: "Oyundaki
        /// başlangıç koltukları 4 ise … başlangıçtaki koltuklar 2-3-4-5 sırası olacak
        /// geliştirme ile alınan koltuklar 1 ve 6 olmalı").
        ///
        /// Six stools are drawn along the counter and a new bar owns four of them. It used
        /// to own the FIRST four, which put every opening night's whole crowd against the
        /// left-hand wall with two stools' worth of empty bar between them and the till — a
        /// room that reads half-abandoned on the night it opens, and an upgrade that adds a
        /// stool to the far end of a line nobody is sitting at. The four it owns now are the
        /// MIDDLE four and the two an upgrade buys are the two ENDS: the bar fills from its
        /// centre outward, which is how a bar fills.
        ///
        /// DERIVED from the config rather than typed, so a bar that opens with some other
        /// number of stools still centres them: the opening block sits in the middle of the
        /// row, and what is left over is added from the till end (the high index, the end
        /// the bar is worked from) back towards the far wall.
        /// </summary>
        private static int[] SeatFillOrder(int slots, int opening)
        {
            var order = new int[slots];
            int first = Mathf.Clamp((slots - opening) / 2, 0, Mathf.Max(0, slots - 1));
            int n = 0;
            for (int i = first; i < slots && n < opening; i++) order[n++] = i;
            for (int i = first + opening; i < slots && n < slots; i++) order[n++] = i;
            for (int i = first - 1; i >= 0 && n < slots; i--) order[n++] = i;
            return order;
        }

        private int[] _seatOrder;
        private int _seatOrderFor = -1;

        /// <summary>Tonight's fill order, cached against the config that made it.</summary>
        private int[] SeatOrderFor(TycoonRun run)
        {
            int opening = run.Config.StartingSeats;
            if (_seatOrder == null || _seatOrderFor != opening)
            {
                _seatOrder = SeatFillOrder(_seats.Count, opening);
                _seatOrderFor = opening;
            }
            return _seatOrder;
        }

        /// <summary>
        /// The n-th OWNED stool counting down from the till end of the counter.
        ///
        /// NOT the fill order reversed, which is the trap: the last stool an upgrade buys is
        /// the one against the FAR wall, so walking the fill order backwards would seat the
        /// house's guest at the wrong end of a full bar. This asks the counter rather than
        /// the shopping list.
        /// </summary>
        private static int TillEndward(int[] order, int owned, int n)
        {
            for (int slot = order.Length - 1; slot >= 0; slot--)
            {
                bool mine = false;
                for (int k = 0; k < owned; k++) if (order[k] == slot) { mine = true; break; }
                if (!mine) continue;
                if (n == 0) return slot;
                n--;
            }
            return -1;
        }

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
        /// gets there. Both the floor and the cycle are scaled by it — see AdvanceWalkIn.
        ///
        /// DEEPER AND EARLIER (2026-08-26, the author: "yürüme animasyonunun sonunda
        /// yavaşlarken animasyonun yavaşlaması gerekmez mi"). The cycle HAS been riding the
        /// pace since the ease was rebuilt, so what was missing was not the wiring but the
        /// reading: at 0.45 over 260 units the last steps run at five and a half frames a
        /// second for about a third of a second, which is a slow-down you can measure and
        /// not one you can see. 0.30 over 300 lands the last steps at three and a half
        /// frames a second and gives the slow-down two thirds of a second more to happen in
        /// — the difference between a figure that stops and a figure that is stopping.
        /// Measured before it was picked, because the shift is 95 seconds long and a walk
        /// is spent out of it: the approach costs 0.45s more than it did, and a curved ease
        /// (u² rather than u) was tried first and cost 1.7s, which is a customer.</summary>
        private const float ArrivalEase = 300f, ArrivalPace = 0.30f;

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
            // 0 -> 7 (2026-08-26): her crown was CLIPPED by the rig canvas and a head row
            // of zero is what that reads as. Tools/afro_crown_fix.py slid the whole set
            // down seven rows and rebuilt the dome on the curve her own hair was already
            // drawing, so the measurement moved with the art.
            ("afrowoman", 7f, 0f, 7, 6),
            ("eastasianman", 7f, 0f, 5, 6),
            // The last one before the casting pauses, the author's own description, and
            // DRAWN AGAIN on 2026-08-20 rather than filtered. For one day her keyline was
            // stripped off the finished frames; the author threw that out ("hicbir
            // karakterde siyah kontur olmamali ... dogal kontur olacak"), so the brief was
            // changed where the ink was actually coming from - the leopard print lost its
            // black - and she was rolled several times with the best of the batch adopted
            // on measurement.
            //
            // CUT 2026-08-25: "spanishsuit" — the waistcoated drinker who used to sit here —
            // left the game on the author's call ("İspanyol müşteriyi oyundan kaldır görseli
            // ve animasyonları bozuk"). His 200 frames were deleted with him. His generation
            // record is still in Tools/patron_trial_state.json; anyone tempted to re-adopt it
            // should re-roll the character rather than re-ship those frames, because the
            // frames are what was wrong with him.
            ("leopard", 4f, 0f, 4, 6),
        };

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
            public Image PatienceNeon;      // the strip under the glass, lit in the band's colour
            public int Index;                // which stool along the bar, left to right
            public float NeighbourT;         // how long somebody has been sitting beside them
            public float SeatX;              // this stool's resting x
            public CounterMess Dirty;        // the mess left on this stool: the glass, then the mark (GDD 27 §4)
            public RectTransform DirtyProp;  // its clickable prop on the counter
            public RectTransform SmudgeProp; // the mark under it, until the cloth (GDD 27 §4)
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
            // ── what they SAY, which is not what the ticket shows (2026-09-04) ──────
            // The plate above a drinker's head is a TICKET: a name, an order, a spec, a
            // clock. Speech is a different object with a different life — it arrives, it is
            // read, it goes — and putting a sentence in a row of the ticket made the ticket
            // grow a paragraph and stay that size for as long as they nursed the glass.
            public RectTransform Say;        // the speech balloon, its own object
            public Image SayBg, SayTail;
            public Text SayText;
            public float SayLineH;           // asked of the font once, like the ticket's rows
            public float SayUntil;           // unscaled time it goes away
            public RectTransform IconRow;    // the drink, a rule, and the serving spec
            public Image IconRule;           // the drawn bar between what and how
            public Image[] Garnish;          // one mark per thing on the spec
            public bool WasKnown;            // edge-detect the licence being read
            public float SpeakFrom;          // when the order started being spoken
            public bool Spoken;              // the order has finished arriving
            public float OrderAnimLeft;      // remaining "placing the order" one-shot time
            public float DrinkT;             // time since they started drinking
            /// <summary>What this drinker will say about the glass they were handed —
            /// read off the pour at the moment of the serve and kept, so the line cannot
            /// change under them while they drink it. Default (silent) until served.</summary>
            public PourNote Note;
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
        // THE BIN IS GONE (2026-08-26, the author: "cop kutusunu da kaldir, cop kutusu
        // yerine lavabo kullanilacak"). A stainless well stood half out of frame at the
        // counter's right-hand end and answered a click; it was an invented object doing a
        // job the room already had a fixture for, and one the market was already selling
        // two marks of. The verb did not change — a built drink is clicked away — only what
        // it is clicked ON: the SINK, which is a piece of dressing in fixtures.json with its
        // own hit plate (DiegeticStage.BuildPropDoor) and its own upgrade. See OnDrainClicked.

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

        /// <summary>How tall the finished drink stands on the counter. 116 -> 92
        /// (2026-08-26, the author: "boyutu kucultulmeli"): at 116 it was the tallest thing
        /// on the bar and read as a prop in the foreground rather than a glass waiting on a
        /// coaster.</summary>
        private const float CarriedGlassHeight = 92f;

        /// <summary>Where the finished drink stands: stage 430, in the gap between the
        /// garnish rail's far end (380) and the drip mat (480). The coaster is drawn at
        /// this x too — they are the same place, and one constant keeps them there.</summary>
        private const float GlassHomeX = 220f;

        /// <summary>
        /// THE BAR'S ONE FOOT LINE (2026-08-26; re-cut 2026-09-04). Everything standing on
        /// this counter is placed by where it TOUCHES it, not by where its middle is — the
        /// coaster lies on this line with the glass standing in it, and every dish on the
        /// garnish rail is lifted until its own lowest drawn pixel lands here.
        ///
        /// IT WAS A DERIVATION AND THAT IS WHAT WAS WRONG WITH IT. It read the dishes'
        /// 64-tall rects — "their feet are 32 below their centres" — which is where the
        /// RECTS end and not where the DRAWINGS do: preserveAspect letterboxes a 48x25 salt
        /// bowl inside that square, so the bowl's foot floated 15 units over the ice
        /// bucket's, and the author could see it (2026-09-04: "garnishlerin en alt pixeli
        /// ayni yukseklikte olmali, buz kovasi biraz yukari tuz kasesi biraz asagi"). A rect
        /// is not a foot. The line is a NUMBER now and the drawings are lifted onto it one
        /// by one — see DishRestY.
        ///
        /// -228 → -220, and the eight units are the coaster's: at -228 a 36-deep mat centred
        /// on the line hung its front arc off the counter's drawn band into the shelf bays
        /// ("masanin yuzeyine tam otursun"). At -220 the whole mat lies on the bar with a
        /// strip of counter still showing in front of it, and the rail's two extremes move
        /// the way the author asked — the bucket up, the salt bowl down.
        /// </summary>
        private const float CounterFootY = -220f;

        /// <summary>
        /// THE MAT SITS A LITTLE BEHIND THE FOOT LINE (2026-09-04, the author: "bardak
        /// altligi birkac pixel yukari cekilsin", then "3 pixel daha yukari cekilsin", then
        /// one more — three of the room's pixels, then three, then one). Everything that
        /// STANDS on this bar has
        /// its lowest pixel on <see cref="CounterFootY"/>; a mat does not stand, it LIES —
        /// and a thing lying flat reads as further into the counter than the dishes beside
        /// it, which is what these three pixels buy. It also puts a wider strip of bar in
        /// front of the mat's near edge, so nothing about it reads as hanging off.
        ///
        /// SEVEN OF THE ROOM'S OWN PIXELS, not seven screen units: the room is drawn at
        /// 640x360 in a 1280x720 field, so a prop on this counter can only be moved by whole
        /// multiples of that grain or it lands between its own pixels (the house rule the
        /// cast's own 16-unit drop is written to).
        ///
        /// THE DRINK COMES WITH IT (2026-09-04, the author: "bardak altligini ne kadar
        /// yukari cektiysek bardagida ayni miktarda yukari cek"). It stood on the foot line
        /// through the first three lifts and ended up on the mat's front arc, which is a
        /// drink standing off the edge of its own mat. The glass and the mat are ONE place
        /// on this bar, so they take one offset — see GlassHome, which adds this.
        /// </summary>
        private const float CoasterLift = 7f * StageToHud;

        /// <summary>The drink's place on the bar, drawn whether or not there is a drink.</summary>
        private RectTransform _coasterRt;

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

        /// <summary>
        /// Where the finished drink rests, RIDING THE COUNTER (2026-08-26). It was a plain
        /// field set once at build, so opening the cellar lifted the bar, the coaster and
        /// every dish on the rail and left the drink standing in mid-air over the shelves —
        /// which nobody saw until the coaster arrived and gave it something to be separated
        /// FROM. The same lift the book, the dirty glasses and the rail already read.
        ///
        /// AND IT RIDES THE MAT'S OWN OFFSET TOO (2026-09-04). The drink does not stand on
        /// the counter, it stands on the COASTER, so wherever the mat is drawn is where the
        /// glass's foot goes — one term, added in both places, so the two cannot be moved
        /// apart by hand again.
        /// </summary>
        private Vector2 GlassHome =>
            new Vector2(GlassHomeX,
                CounterFootY + CoasterLift + CarriedGlassHeight * 0.5f + CounterLift);

        private bool _glassShown;

        private const float GlassSlideMax = 0.22f;   // a full-counter slide, distance-scaled below

        // day end — two steps now (the author, 2026-08-01): first the bill alone, then
        // the market, each with its own verb on the same button.
        private RectTransform _dayEndPanel;

        private RectTransform _offerRow;

        private Text _bannerText;

        private RectTransform _dayEndBill, _dayEndTablet;

        private Text _dayEndTitle;

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
        private Image[] _idBond;        // how well they know you, in hearts
        private Image[] _idStarFills;   // the amber over them, filled to the fraction
        private RectTransform _idRecipeTip;

        private RectTransform _idRecipeTipBody;

        private RectTransform _idPrefRow;

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
        // THE FOOT: 8 + 808 (the basket) + 8 + 208 (the one key) + 8 = 1040. ExitW is the
        // key that buys AND the key that opens tomorrow since the two were merged
        // (2026-09-04); it took 72 units off the basket the same day, because the author
        // asked for it to be harder to miss ("daha dikkat çekici olmalı") and the honest way
        // to make a control louder is to make it BIGGER — a pulse on a key with nothing to
        // pulse about is decoration, which 16 §5 does not allow. At 208 the caption sets at
        // 24 instead of 16, which is where the noise actually went.
        // The basket keeps room for fifteen chips at 808, so nothing was taken from it that
        // it was using.
        private const float FootH = 128f, BasketW = 800f, ExitW = 216f;

        // THE ONE KEY'S TWO FACES (2026-09-04, the author: "satın alma seçeneğinde rengi
        // yeşil olmalı ... sonraki güne geçme butonunda renk değişmeli").
        //
        // GREEN TO SPEND. Lime carries no sacred role (16 §2 spends Amber on money, Cyan on
        // flavour, Magenta on the multiplier), so it was free to take one — and it is the
        // colour the key's own lamp has always burned, so the lit key and its glow are now
        // one object rather than a blue key inside a green halo. Dark ink from its own ramp:
        // Lime 4 is bright enough that black-green reads better on it than white.
        //
        // AND THE NIGHT'S END COMES OFF AMBER. It had to move for two reasons, and the
        // second is the load-bearing one: amber is MONEY (16 §5) and this face is the one
        // that spends none. Magenta 4 is the storefront's own lit accent — the colour the
        // market's fade runs into and its open tab is edged with — so the exit is loud
        // without borrowing a meaning that belongs to a number.
        private static readonly Color MarketKeyBuy = UITheme.Lime[3];
        private static readonly Color MarketKeyBuyInk = UITheme.Lime[0];
        private static readonly Color MarketKeyNight = UITheme.Magenta[3];
        private static readonly Color MarketKeyNightInk = Color.white;
        private static readonly Color MarketKeySpent = new Color(0.612f, 0.635f, 0.706f, 1f);
        private static readonly Color MarketKeySpentInk = new Color(0.898f, 0.910f, 0.949f, 0.85f);

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

        private Text _fittingNote, _marketKeyLabel, _cartHeadLabel, _osClock;

        private Text _cartTotal, _cartTotalLabel, _cartLeft, _cartLeftLabel;

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

        private RectTransform _marketKey, _billNext;

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
            /// <summary>The star gate this tile is waiting on, or NaN where the lock is
            /// not about stars (a tower's rung, a person's beat). A real number here is
            /// drawn as stars under the figure — the number alone made every gate in the
            /// shop a thing to be read rather than seen (2026-08-25).</summary>
            public double GateStars = double.NaN;
            /// <summary>The rung an OPEN listing stands on, or NaN to draw no ladder
            /// (2026-09-04, the author: "markette açık olan her ürünün kutusunun bir
            /// tarafında kaç yıldız gerekiyorsa yıldız iconu ile gösterilsin"). A sealed
            /// crate says its gate on its tag instead — see <see cref="GateStars"/> — so the
            /// two are never both drawn and the shop says the number once.</summary>
            public double RungStars = double.NaN;
            public string Word;              // "FULL" / "MAX" / "SOLD" — 4 CAPS, never 5
            /// <summary>Overrides the state row's word for a listing whose state is right
            /// and whose stock phrase is not (2026-09-04). The whole-well crate is HELD when
            /// the basket already covers every short bottle, and the shared reading for held
            /// is SHELF FULL — which on that one tile is a lie about the shelf.</summary>
            public string StateWord;
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


        private static readonly Color TabletLens = new Color(0.30f, 0.30f, 0.34f, 1f);

        private CustomerVisit _idVisit;

        private TycoonServiceFlow _flow;

        private TycoonPhase _lastPhase = TycoonPhase.DayOpen;

        /// <summary>Whether the closing screen has already announced itself; the phase
        /// test that raises it runs every frame.</summary>
        private bool _closedSpoke;

        /// <summary>The week's job, one line beside the LOG key (2026-09-04).</summary>
        private Text _jobStrip;

        private Text _toast;

        private float _toastUntil;

        /// <summary>The notice line's own ink — the refusal red it was built in. A tinted
        /// notice borrows the channel for one message and this hands it back.</summary>
        private Color _toastInk = UITheme.ViceRed[3];

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
            // Every prop in the room says what it does before it is pressed (2026-08-26).
            if (stage != null)
                stage.SetPropHoverHandler((rt, word) =>
                { if (word == null) HidePropTip(rt); else ShowPropTip(rt, word); });
            // The beer font on the counter is the only door onto the draught station now
            // (2026-08-15): the kegs left the back-bar wall, and a pint is poured by walking
            // to the tap. The flow's own guard turns the click down between days.
            if (stage != null) stage.SetTapHandler(OnTapClicked);
            if (stage != null) stage.SetSinkHandler(OnSinkClicked);
            if (stage != null) stage.SetCellarHandler(OnCellarPick);
        }

        private void OnDestroy()
        {
            if (_bootstrap != null) _bootstrap.RunStarted -= OnRunStarted;
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
            _dayEndDue = false;   // a new bar is not owed last night's books
            _tabFloats = 0;       // and nothing of last night's is still in the air
            // A NEW BAR REMEMBERS NOBODY (2026-08-25). The guest log and the casting both
            // outlived the run they were built in: the HUD is made once and StartNewRun only
            // replaces the run under it, so NEW RUN opened on a bar whose faces already
            // carried the last run's visit counts and star ratings, and day one printed
            // "3rd visit" for a room that had been open for ninety seconds. Everything below
            // is keyed off a run that no longer exists.
            _patronLog.Clear();
            _faceOfPerson.Clear();
            _lenderOfPerson.Clear();
            _faceOfVisit.Clear();
            _faceLastSeen.Clear();
            _faceClock = 0;
            _faceRng = null;          // re-seeded off the new run's own seed
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
            _lastShelfMark = 0;       // ...and the stock, which keeps its own signal now
            ApplyBarLook();
        }

        private void Update()
        {
            // THE CURTAIN STEPS FIRST, AND UNCONDITIONALLY. Everything below is gated on
            // there being a run, and a full-screen black that is gated on game state is a
            // black screen waiting to happen: any frame where Run is null — between runs,
            // or on the first frames of one — would leave it up with nothing to lift it.
            StepCurtain();
            StepMarketKeyLamp();

            var run = Run;
            if (run == null) return;
            WatchGlassRack();
            WatchFixtures();
            WatchCellar();
            FadeShopTabs();
            StepSlide();
            RunTheTill(run);
            PushCellarFills(run);       // the cellar levels follow the night's pours (PLAN §4c)
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
                // The BOOK is homework, not service (2026-08-24, the author: "Menü
                // açıkken zaman çok yavaş geçmeli"): it slows the night nearly to a
                // hold while the working menus keep their 0.3 — and the clock still
                // moves, because the night keeps its one-way arrow.
                float clock = _bookOpen ? (float)TycoonConfig.BookTimeScale
                    : menuOpen ? (float)TycoonConfig.MenuTimeScale : 1f;
                if (!DoorsClosed)
                    run.Tick(Time.deltaTime * clock);
            }

            if (run.Phase != _lastPhase)
            {
                _lastPhase = run.Phase;
                if (run.Phase == TycoonPhase.DayEnd)
                {
                    _dayEndDue = true;
                    _dayEndDueAt = Time.unscaledTime;
                    Sfx.Play("day_close", 0.8f);
                    // AND THE ROOM COMES BACK NOW, not when the books land (2026-08-25, the
                    // author: "önce açık olan tüm pencereler kapanır, ana sahneye dönülür ve
                    // oyun sonu ekranı öyle gelir"). Shutting the sheets at ShowDayEnd was
                    // half the sentence: the last customer's walk to the door then played
                    // out behind whatever bench or book was still up, and the player never
                    // saw the thing the books are now waiting for.
                    CloseEverySheet();
                }
                if (run.Phase == TycoonPhase.Closed)
                {
                    if (!_closedSpoke) { _closedSpoke = true; Sfx.Play("bar_closed", 0.9f); }
                    ShowClosed();
                }
                else _closedSpoke = false;
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
            PlaceBookProp();      // it stands on the counter, so it rides with the counter
            SyncLastCall(run);    // after the seats: the guest is one of them
            SyncHostNote(run);    // the closing's lessons, on the market
            UpdateOrderTip();     // after the seats: it reads the tickets they just placed
            UpdateDrinkGlass();
            StepMiniPreps(run);
            StepPropTip();
            UpdateEscape();
            UpdateBookKeys();
            StepStarDrop();
            StepStamp();
            StepMoneyDrops();
            StepMarketKey();
            StepDayEndBeats();
            StepChipPop();
            StepDayEndDue();
        }

        // ── the night waits for the room to empty (2026-08-25) ──────────────────
        //
        // The author: "gün müşteri içkisini bitirip ekrandan çıkmadan bitmemeli". It did.
        // Core has always been right about this — a stool stays taken until the drink is
        // finished, and BarDay.IsComplete waits for the last one — but the WALK OUT is the
        // HUD's, and it starts on the same tick that empties the stool. So the scrim came
        // down over the last customer's cheer and their walk to the door, and the last
        // thing the player saw of the night they had just worked was it being covered up.
        //
        // The phase flip only ARMS the books now; they arrive when the floor is actually
        // clear — nobody left on screen, and no tab still counting itself over a stool.
        private bool _dayEndDue;

        private float _dayEndDueAt;

        /// <summary>The longest the books will wait for a stubborn walker. The far stool is
        /// about 1000 units from the door at WalkSpeed, plus the reaction beat — call it
        /// four and a half seconds — so this is a backstop against a view that never
        /// finishes, never a timer the night is expected to hit.</summary>
        private const float DayEndPatience = 9f;

        private bool _flowWasOpen;

        /// <summary>The room, for the service flow — which has to put its bar top on the same
        /// line the room's counter is on, and cannot ask the scene for it twice.</summary>
        public DiegeticStage Room => stage;

        // ── the snack bowls (v5 P16) ─────────────────────────────────────────────
        // On the counter, left end, opposite the bin: click a bowl to take it in hand, click
        // a customer to put it down. The plan said "from the menu"; the bowls stand on the
        // counter instead because a snack has no prep — sending the player through the drink
        // menu for a bowl of nuts would be a stage with nothing on it.

        private SnackDefinition _snackInHand;

        private readonly List<(SnackDefinition snack, Image art, Text stock)> _snackBowls =
            new List<(SnackDefinition, Image, Text)>();

        /// <summary>How many tabs are still counting themselves over a stool. The night's
        /// books wait for these (see <see cref="FloorIsClear"/>): the money and the stars a
        /// customer left are the last thing that happens in a day, and a scrim over them is
        /// the day ending before it finished paying.</summary>
        private int _tabFloats;

        /// <summary>How long a mark hangs over the stool, and how far it climbs. 3.2s
        /// against the 1.6 it was (2026-08-25, the author: "hemen yok oluyor") — long enough
        /// to be read twice, which is what a reward has to be.</summary>
        private const float TabLife = 3.2f, TabClimb = 104f;

        /// <summary>How far it wanders off the vertical and how far it leans doing it. The
        /// author again: "düz bir şekilde yukarı çıkmak zorunda değil, metin biraz sağa sola
        /// eğimli olabilir" — so it drifts on a slow sine and leans INTO the drift, the way
        /// a thing carried upward on air does.</summary>
        private const float TabSway = 26f, TabLean = 7f;

        /// <summary>
        /// HOW FAR APART THE THREE LEAVE THE STOOL (2026-08-25, the author: "Müşterilerin
        /// içkiyi içtikten sonra verdiği yıldız, para ve tip, arka arkaya çıksın ve
        /// birbirinden bağımsız hareket etsinler").
        ///
        /// A third of a second. Long enough that the eye has finished one before the next
        /// arrives — which is the whole point of counting them out instead of handing over
        /// a block — and short enough that all three are in the air together, so the stool
        /// reads as paying up rather than as three unrelated events.
        /// </summary>
        private const float TabStagger = 0.5f;

        /// <summary>
        /// Where each of the three leaves from and how high it carries.
        /// 
        /// THE ONE OUT FIRST CARRIES FURTHEST, and that is not decoration — it is what keeps
        /// them off each other. The first cut let the money climb FOURTEEN FURTHER than the
        /// stars it was chasing, so it caught them up about a second in and the row of stars
        /// came down on the "+$17" (measured in play, 2026-08-25). Ordered the other way they
        /// fan instead of converge and the air between them only grows.
        ///
        /// THE SPACING IS SET BY A RULE AND NOT BY EYE. Every mark hangs its content from its
        /// host's top edge, so two of them clear each other only while the gap in their
        /// heights EXCEEDS THE UPPER ONE'S OWN HEIGHT — 14 for the star row, 28 for the
        /// figure. These leave 30 and 34 at full climb, which is that rule plus a few units
        /// of air, and more than that at every point on the way up. The second cut missed it
        /// by six units and the tip line sat on the total's descenders.
        ///
        /// The sideways numbers stay small: they are three marks off ONE stool, and three
        /// columns would read as a table, which is the receipt this stopped being.
        /// </summary>
        private static readonly float[] TabLaneX = { 0f, -12f, 14f };
        private static readonly float[] TabLaneClimb = { 30f, 0f, -34f };

        /// <summary>What each mark is called in the hierarchy. NOT "Tab0..2" (measured,
        /// 2026-08-25): the book's own tab strip already stands five children called exactly
        /// that, so a search for a tab in flight came back holding the book. Named after what
        /// the mark says instead, which is what anybody reading the hierarchy wanted.</summary>
        private static readonly string[] TabLaneName = { "TabStars", "TabPaid", "TabTip" };

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


        /// <summary>What the cellar was last built against. 0 is not a shelf any run can
        /// have, so a fresh run always stocks once.</summary>
        private int _lastShelfMark;

        private readonly List<IngredientCard> _cellarCards = new List<IngredientCard>();

        // "Ekrandaki bardaklari simdilik kaldir, gerek yok" (2026-08-19): the rack is
        // parked, not demolished - flip this back on when the new counter art gets its
        // compartments and the rack has somewhere honest to stand.
        private const bool GlassRackShown = false;

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

        // ── who wears which face (rewritten 2026-08-25) ───────────────────────
        // The old rule was A HASH OF THE NAME, and the name came out of an archetype's
        // five-name pool: forty strings collapsed onto ten drawings, the same string always
        // landed on the same drawing, and two unrelated people both called Marguerite were
        // one person as far as the licence, the guest log and the player's eye could tell.
        // The room read as four or five faces on a loop, night after night (the author,
        // 2026-08-25: "musteriler rastgele gelmeli hergun").
        //
        // A face belongs to a PERSON now, for as long as the run remembers them, and a
        // stranger takes the face nobody has worn for the longest. So a night draws across
        // the whole cast before it repeats anybody, and the opening night — about eight
        // drinkers against nine faces — is eight people the bar has never seen, which is
        // what an opening night is.
        //
        // It decides nothing: which drawing sits down cannot change what anybody orders,
        // pays or waits. That is why it may live up here in the HUD, and why it draws on its
        // OWN generator rather than on the run's streams, which the floor's arrivals are
        // counted out of. Seeded off the run's seed all the same, so a shared seed still
        // shows two players the same crowd.
        private readonly Dictionary<string, PatronLook> _faceOfPerson =
            new Dictionary<string, PatronLook>();

        private readonly Dictionary<CustomerVisit, PatronLook> _faceOfVisit =
            new Dictionary<CustomerVisit, PatronLook>();

        private readonly Dictionary<PatronLook, int> _faceLastSeen =
            new Dictionary<PatronLook, int>();

        private int _faceClock;

        private SeededRng _faceRng;

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
        /// <summary>Where the print starts under the head. 12 → 20 on 2026-08-25: the first
        /// row is the star row, the stamp is struck ACROSS the star row, and a stamp is
        /// crooked and taller than what it strikes — so on a night that earned nothing its
        /// raised corner clipped the date line above it. Eight units of paper is the whole
        /// fix, and the roll has blank stock to spare at the foot.</summary>
        private const float BillRowsTop = BillHeadH + 20f;

        private static readonly Color BillPaper = new Color(0.965f, 0.945f, 0.886f, 1f);

        private static readonly Color BillEdge = new Color(0.62f, 0.58f, 0.50f, 1f);


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

        /// <summary>How long the boards take to arrive. Under the paper's own feed, so they
        /// are standing there before the slip lands rather than racing it.</summary>
        private const float BoardsIn = 0.85f;

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

        // ── the night's two instruments (2026-08-25) ────────────────────────────
        //
        // The author: "gün sonu ekranını baştan sona tekrardan tasarla, mevcut haftalık
        // takvimin ilerlemesini daha profesyonelce göster, gün sonunda restoranın yıldız
        // ilerlemesini göster, bugün görselle ne kadar ilerlediğini göster."
        //
        // The books were ONE object on an empty scrim: a till slip, centred, with four
        // hundred units of nothing either side of it. And a receipt can only ever say what
        // tonight COST and TOOK — so neither of the two questions a tycoon player actually
        // has at two in the morning was on the screen at all: where am I in the week, and
        // did tonight move my bar.
        //
        // They are the two instruments flanking the slip now. THE WEEK is the record: every
        // night of this week with the stars it filed and the money it made, tonight lit, the
        // nights ahead still empty sockets, Sunday shuttered. THE BAR is the ladder: the
        // standing, the step tonight moved it (drawn, on a gauge, against where it stood
        // before), the ceiling the fittings hold it under, and the next rung up. Both are
        // drawn in the room's own chrome — the market's card, the bottle gauge's tube — and
        // both read the RULES for their numbers (BarRating.StandingAfter, TycoonRun's
        // ceilings and crowd) rather than working the climb out for themselves.
        /// <summary>The night's two instruments. The width is the plate's own drawing at a
        /// whole 2× (178 art px); the HEIGHT is the content's, because the plate is 9-sliced
        /// now (ItemArt.BoardPlate) and only its plain navy field stretches — a hard 350 was
        /// the first build's mistake, and it cut the week's subtotal and the standing's
        /// next-rung line off the bottom of their own instruments.</summary>
        // 420 → 460 on 2026-09-05 (H5): the standing board took SERVICE and COMFORT above
        // TONIGHT, two rows of 28, and the week board rides the same plate.
        private const float BoardW = 356f, BoardH = 460f, BoardX = 430f, BoardY = 48f;

        private const float BoardPad = 18f;

        private static readonly Color BoardPlate = new Color(0.102f, 0.063f, 0.137f, 0.96f);

        /// <summary>One of the two boards: the plate, its head, and the body its rows are
        /// rebuilt into every night.</summary>
        private sealed class NightBoard
        {
            public RectTransform Root;
            public RectTransform Body;
            public CanvasGroup Group;
            public Text Reading;      // the head's right-hand figure
        }

        private NightBoard _weekBoard, _standBoard;

        // The standing's moving parts, re-taken every rebuild (the body is destroyed and
        // built again, so a reference kept across nights would point at a corpse).
        private Image[] _standStars;

        private Image _standFill, _standFillGhost;

        private Text _standNumber, _standDelta;

        private RectTransform _standDeltaChip, _standWasTick;

        private Image _standDeltaArrow;

        private float _standT = -1f;          // < 0 = not running
        private double _standFrom, _standTo;

        private const float StandClimb = 1.1f;

        // ── the week, as a record rather than a row of names ────────────────────

        /// <summary>40 → 38 on 2026-09-04, when a night's row took a second figure (the
        /// TAKE over the NET). Seven of these, a rule and a two-line foot have to finish
        /// clear of the plate's own drawn foot — its bottom border is 18 art px at 2×, and
        /// the rivets live inside it — so the rows give the foot the two units it needs.</summary>
        private const float WeekRowH = 38f;

        /// <summary>Where a night's score starts. Far enough past the VIP mark that
        /// Saturday's promise is not read as Saturday's first star.</summary>
        private const float WeekStarsX = 92f;

        private float _chipPop;

        /// <summary>
        /// EVERY SHEET SHUTS, AND THE ROOM COMES BACK (2026-08-25, the author: "Oyun sonu
        /// ekranı gelmeden önce açık olan tüm pencereler kapanır ana sahneye dönülür ve oyun
        /// sonu ekranı öyle gelir, aynı şekilde gün başlarken de ekran ana ekran haline gelir
        /// ve temizlenir").
        ///
        /// The night used to be counted UNDER whatever the player had left open: the books
        /// arrived over a half-read recipe, a licence, an open cellar, a bench with a drink
        /// still in the tin. The invoice is a scrim over the room, so all of it stayed there,
        /// lit, behind the one thing that was supposed to be read.
        ///
        /// The book is shut HARD rather than toggled: its close is a slide, and a page still
        /// travelling while the scrim comes down is exactly the mess this exists to prevent.
        /// Everything else is a panel and simply goes.
        /// </summary>
        private void CloseEverySheet()
        {
            if (_bookOpen)
            {
                _bookOpen = false;
                if (_bookAnim != null) { StopCoroutine(_bookAnim); _bookAnim = null; }
                if (_bookTurnAnim != null) { StopCoroutine(_bookTurnAnim); _bookTurnAnim = null; }
                _bookTurning = false;
                if (_bookPanel != null) _bookPanel.gameObject.SetActive(false);
            }
            if (Showing(_settingsPanel)) _settingsPanel.gameObject.SetActive(false);
            if (Showing(_devPanel)) _devPanel.gameObject.SetActive(false);
            if (Showing(_guidePanel)) _guidePanel.gameObject.SetActive(false);
            if (Showing(_ledgerPanel)) _ledgerPanel.gameObject.SetActive(false);
            CloseId();
            _flow?.CloseFlow();
            // ...and the room itself goes back to how it opens: the counter's cellar shut,
            // instantly, because this is a cut and not a beat.
            if (stage != null) stage.SetDrawerOpen(false, instant: true);
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

        // A basket chip: wide enough to show what the thing IS, narrow enough that a night's
        // shopping fits on one row. The row shrinks its chips toward the floor as it fills;
        // past the floor it says how many more it is holding rather than dropping them.
        private const float ChipMax = 84f, ChipMin = 46f, ChipGap = 6f;

        /// <summary>How long the order key stays spent before it can be used again.</summary>
        private const float CheckoutHold = 3f;

        private float _checkoutUntil = -1f;

        /// <summary>
        /// Fills the inspector from whatever the pointer is over, or empties it back to the
        /// idle line. This is the ONLY place long text is written in the market: the tile
        /// carries what identifies a thing, the inspector carries what explains it, and the
        /// two sets are disjoint by construction because they come from one TileSpec.
        /// </summary>
        private const float ShopSpecW = 268f;

        private RectTransform _shopSpec, _shopSpecBody;

        private RectTransform _liquorHead, _kegHead, _garnishHead;

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

        // ── the recipe book (v5 P16) ─────────────────────────────────────────────
        // The menu speaks styles now, so what is IN a drink has to be readable mid-shift:
        // an order for a Gimlet is unanswerable by a player who cannot look a Gimlet up.
        // Unlocked recipes print their full bands; locked ones show what tier and stars
        // they are waiting behind, so the book doubles as the progression map.

        // THE BOOK IS A BOOKLET (2026-08-24) AND A COOKBOOK SINCE THE SAME EVENING
        // (the author: "bir sayfa tamamen bir tarif olabilir, yemek kitabı gibi"): a
        // title-and-contents spread opens it, and after the first turn EVERY PAGE IS ONE
        // RECIPE — name, prep and glass, a legend saying what the gauge measures and
        // which colour owns which fifth, the pours at full width, and the drink's own
        // history at the foot. The contents jump straight to a chapter; the tiers ARE
        // the chapters, with the unowned pages locked in place among their own.
        private RectTransform _bookPanel;

        private readonly List<BookPage> _bookPages = new List<BookPage>();

        private readonly List<BookChapter> _bookChapters = new List<BookChapter>();

        private int _bookSpread;                 // the open spread — the ribbon's bookmark
        private RectTransform _bookWinRestL, _bookWinRestR, _bookWinInL, _bookWinInR;

        private RectTransform _bookPageRestL, _bookPageRestR, _bookPageInL, _bookPageInR;

        private Image _bookLeaf;

        private Sprite[] _bookLeafFrames;

        private bool _bookTurning;

        private Coroutine _bookTurnAnim;

        private RectTransform _bookPrevKey, _bookNextKey, _bookHomeKey;

        private RectTransform _bookBadge;        // the mark on the book's own corner
        private RectTransform _bookProp;         // the shut book standing on the bar
        private RectTransform _bookShadow;       // what pins it to the counter
        /// <summary>Where the book stands along the bar, from the middle. Left of centre,
        /// clear of the sink at one end and the beer font and till at the other — the same
        /// stretch of counter the old BOOK key floated over, so the hand already knows.</summary>
        // OFF THE MIDDLE OF THE SCREEN (2026-08-25, the author: "menu en ekranin
        // ortasinda kalmis biraz daha tezgaha dahil hissi verdirilmeli"). At -196 the book
        // stood at stage x 222 — left of centre by the numbers and dead centre to the eye,
        // with clear counter either side of it, which is what makes a prop read as placed
        // ON a surface rather than BELONGING to it. The bar's other working objects are all
        // pushed to its ends: the sink at 140, the font at 540, the till at 604. This puts
        // the book in the working left end beside the sink, where the things you actually
        // pick up live, and leaves the middle of the bar clear for the drinkers.
        //
        // ON THE SINK'S OTHER SIDE (2026-08-26, the author: "menüyü lavabonun sol yanına
        // getir"). It stood at stage 152, which is INSIDE the basin's own footprint — the
        // sink is 82 art px wide about x 140, so it runs 99…181 and the book was standing in
        // it rather than beside it. The left shoulder is the free counter: the basin's left
        // edge is 99, the book is 28 wide, and six units of air between them puts its middle
        // at stage 79. It is also further from every stool than it has ever been, which the
        // suite's stool click is grateful for (see BuildBookProp's raycast note).
        private const float BookPropX = -482f;   // stage x 79, on the sink's left shoulder

        private Text _bookBadgeText;

        /// <summary>Recipes perfected but not yet looked up — the news the title page
        /// carries. A page drops off the list the moment it is opened from there.</summary>
        private readonly List<string> _perfectNews = new List<string>();

        private string _bookTocChapter;          // the chapter open in the contents
        private string _bookTocQuery = "";       // the search line, kept like the ribbon

        private enum BookPageKind { Title, Contents, Recipe }

        /// <summary>One page of the book: the title plate, the contents, or one recipe.</summary>
        private sealed class BookPage
        {
            public BookPageKind Kind;
            public string Chapter;
            public RecipeDefinition Recipe;
            public bool Locked;
        }

        /// <summary>A line of the contents: a chapter, where it starts, what it holds.</summary>
        private sealed class BookChapter
        {
            public string Title;
            public int FirstPage;
            public int Count;
            public int LockedCount;
        }

        private bool _bookOpen;

        private Coroutine _bookAnim;

        // The booklet, placed against menu_booklet.py's printed ruler at exactly 2× —
        // one art pixel is two HUD units, the room's own rate.
        private const float BkSheetW = 740f, BkSheetH = 708f;   // the PNG: 370×354 art px
        private const float BkBoardH = 692f;                    // the leather, sans ribbon tail
        private const float BkLiftY = 8f;                       // paper centre above sprite centre
        private const float BkPageW = 334f, BkPageH = 652f;     // one page: 167×326 art px
        private const float BkLeafW = 700f;                     // the turn frames: 350 art px
        private const float BkPageDX = 183f;                    // a page's centre off the spine
        private const float BkReach = 175f;                     // spine → leaf outer edge, art px
        private const float BkColW = 296f;                      // print column inside the gold frame
        private const float BkContentTop = 80f;                 // under the heading rules
        private const float BkParkY = 748f;                     // the drop's overhead park
        private const float BkGaugeW = 102f, BkGaugeH = 14f;    // the page's sight glass:
                                                                // 100 px interior, 20 to a
                                                                // fifth — whole pixels only
        private static readonly Color BkPlatinum = new Color(0.83f, 0.86f, 0.92f);

        private const int BkTurnFrames = 16;

        private const float BkFrameSec = 0.040f;                // the script: "16 is smooth at 40ms"

        /// <summary>How tall the board is, and THE TWO RULES EVERYTHING ON IT SITS ON.
        /// The old board had a reading wherever its box happened to leave room, which is
        /// what "yazılar hizalanmamış" was describing (2026-08-14): captions at three
        /// different heights, values at two more. There are two lines now — the small
        /// upper one for what a reading IS, the lower one for what it SAYS — and every
        /// item on the beam is placed against one of them, left to right.</summary>
        private const float TopBarH = 54f;

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

        /// <summary>The house's two strips in the top bar (GDD 27 §4.4): 16 px icons on an
        /// 18-unit pitch, five wide, one over the other.</summary>
        private const float HouseIcon = 16f, HouseGap = 18f, HouseStripW = 5 * 18f;
        private RectTransform _serviceFill, _comfortFill;

        // ── settings (P17): the smallest sheet that holds sound and motion ───────

        private RectTransform _settingsPanel;

        private Text _settingsVolume, _settingsMute, _settingsMotion;

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

        private const float DevRailX = 20f, DevRailW = 300f;

        // ── the character guide (dev tool, 2026-08-10) ──────────────────────────
        // Every drinker on one scrollable sheet: the licence photo, the name, the age, the
        // citizenship and its flag, the archetype, and the standing the bar has to reach
        // before they walk in. The author's reason is practical — deciding what to change
        // about a character means seeing all of them at once — and it is written as a panel
        // rather than as a printed page because it can become an in-game almanac later:
        // the same rows, unlocked one at a time as each person is met.

        private RectTransform _guidePanel;

        private RectTransform _guideRows;

        /// <summary>The slip is <see cref="Columns"/> characters wide. The pixel font is
        /// monospace, so a receipt line can be set the way a till sets one: label at the left,
        /// amount flush right, leader dots filling whatever is between them.</summary>
        private const int Columns = 26;

        private static readonly string Rule = new string('=', Columns);

        private void ShowClosed()
        {
            _dayEndPanel.gameObject.SetActive(false);
            CloseId();
            _bannerText.gameObject.SetActive(true);
            _bannerText.text = "THE BAR IS CLOSED\nthree days losing money — NEW RUN to try again";
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

        /// <summary>The stage's reference frame, the one both halves agree on.</summary>
        private static readonly Vector2 StageRef = new Vector2(640f, 360f);

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

        private readonly List<(Image sign, Image bloom, Text name)> _weekCells =
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
            var glass = BuildWeekGlass(top, _weekCells, out _weekLabel, out _vipCell);
            glass.anchoredPosition = Vector2.zero;      // centred on the beam, as it was
        }

        /// <summary>
        /// THE WEEK INSTRUMENT, BUILT ONCE AND MOUNTED TWICE (2026-08-25, the author, of the
        /// day card: "Gun baslangic ekranindaki takvim gostergesini begenmiyorum bunu
        /// gelistir, ana sahnedeki ust bardaki takvim gostergesine benzer yapabilirsin").
        ///
        /// The beam and the day card used to draw two different pictures of the same week: a
        /// panel of lit names up here, and down there a wire strung with bulbs. That wire is
        /// the OLDER of the two ideas and was already thrown out once up here, for reading as
        /// bunting - so rather than draw a third picture, the instrument moved. This builds
        /// the glass, the head and the seven slots; both surfaces mount it, and the card just
        /// hangs it bigger. That is the whole difference between them.
        ///
        /// The caller keeps the cells and lights them with <see cref="LightWeekCells"/>.
        /// </summary>
        private RectTransform BuildWeekGlass(RectTransform parent,
            List<(Image sign, Image bloom, Text name)> cells, out Text weekLabel, out int vipCell)
        {
            vipCell = -1;
            var names = BarCalendar.WeekColumns;
            // The generated plate lasted one build ("Oluşturulan takvim görseli bozuk
            // duruyor, elinden geldiğince kendin tasarımını yap") — the exception to
            // chrome-is-never-generated was tried on the author's sentence and withdrawn
            // on the author's next one. The calendar sits in the same drawn WELL the hour
            // does; two instruments, one language, and nothing on the beam is a picture.
            float wellW = WeekDaysX + names.Length * WeekStep + 10f;
            var glass = NewRect("WeekWell", parent);
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

            weekLabel = NewText("Week", glass, _display, 16, TextAnchor.MiddleCenter, UITheme.Cyan[3]);
            Place(weekLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(52, 18),
                new Vector2(WeekHeadCx, -7f));
            weekLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            weekLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

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
                    sign.sprite = ItemArt.Star(true, 16f);
                    sign.raycastTarget = false;
                    vipCell = i;
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

                cells.Add((sign, bloom, name));
            }
            return glass;
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
                si.sprite = ItemArt.Star(false, 16f);
                si.preserveAspect = true; si.raycastTarget = false;
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
                si.sprite = ItemArt.Star(true, 16f);
                si.preserveAspect = true; si.raycastTarget = false;
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

        private void OnOpenTomorrow()
        {
            var run = Run;
            int leaving = run.Day;             // read BEFORE the roll: the curtain names both
            run.ContinueToNextDay();
            _dayEndPanel.gameObject.SetActive(false);
            // The market is a sheet like any other and the next night must not open behind
            // one — nor behind a cellar somebody left open while they were shopping.
            CloseEverySheet();
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

        private RectTransform _marketKeyLamp;

        private Image _marketKeyLampImg, _marketKeyImg;

        // ── the question at the door (2026-08-14) ───────────────────────────────

        private RectTransform _closingAsk;

        // THE HOST'S WORD ON THE MARKET (GDD 26 §1b/§10, PLAN_last_call S5): the plate is a
        // thing of the open night, so the two lessons that come at the CLOSE — the market
        // opening, a night under the rent — are said in a 98 message box of the site's own,
        // the same window the closing question uses.
        private RectTransform _hostNote;
        private Image _hostNoteFace;
        private Text _hostNoteWho, _hostNoteLine, _hostNoteKeyLabel;
        private string _hostNoteLesson = "";   // which lesson the note is reading
        private int _hostNoteAt;

        private Text _closingAskLine;

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

        // ── the day goes past (2026-08-25) ──────────────────────────────────────
        //
        // The author: "gün başı ekranı olmalı; güneşin doğudan çıkıp battığını ve şu anki
        // saate geldiğini gösteren bir gün geçme animasyonu, aynı zamanda saati tam 18:00'a
        // saran — Kingdom Come Deliverance 2'deki uyuduğunda gösterilen ekran gibi."
        //
        // What was here carried a week and two day-names on black, which is a CAPTION for a
        // transition rather than a scene. The bar shuts at two in the morning and opens at
        // six in the evening, and those sixteen hours were nothing at all — so they are the
        // scene now: the moon finishes its fall into the west, the sky walks from deep night
        // through first light, morning, noon and afternoon into the room's own golden hour,
        // the sun climbs out of the east and comes back down, and the readout winds from
        // 02:00 round to exactly 18:00, where the shift starts.
        //
        // ALL OF IT IS DRAWN HERE, in the palette's own tokens — no picture is generated for
        // it (14 §3: chrome is procedural). The sky is BANDED, twenty flat rows and not a
        // smooth ramp, which is the same law the room's own light is banded under; the sun
        // and the moon are the marquee's bulb and its glow, at the size a sun wants; the
        // moon's crescent is bitten out by a second disc wearing the sky's own colour behind
        // it. The hour is the game's own SegmentClock, hung at twice the size it wears on
        // the beam — a whole multiple, because pixel art magnifies in whole steps or not at
        // all.
        private const float SkyW = 640f, SkyH = 220f;

        private const int SkyBands = 20;

        /// <summary>Where the horizon runs: the generated city's own base band — 10 art
        /// px of bay at its foot, 20 units at the 2x it stands at. The sun's arc is rooted
        /// here; the city in front hides everything below its own rooftops.</summary>
        private const float SkyGround = 20f;

        /// <summary>The shift's own hours: the doors shut at two, the next one opens at six
        /// in the evening. The animation is exactly that gap and nothing else.</summary>
        private const float DayFrom = 2f, DayTo = 18f;

        /// <summary>When the sun is up, and when the moon is. A Miami summer: first light
        /// before six, the sun down at eight — so 18:00 is late in its fall, which is why
        /// the room opens in gold.</summary>
        private const float SunUp = 6f, SunDown = 20f, MoonUp = 18f, MoonDown = 6f;

        /// <summary>
        /// The sky at an hour, top and bottom, keyed off the palette. The horizon carries
        /// the warm end and the zenith the cold one, which is what a sky does; the last key
        /// is the evening the room's own window opens on, so the curtain lifts on the colour
        /// that is already outside it.
        /// </summary>
        private static readonly (float Hour, Color Zenith, Color Horizon)[] SkyKeys =
        {
            (2.0f,  UITheme.Night[0],    UITheme.Night[2]),
            (5.0f,  UITheme.Night[1],    UITheme.ClubBlue[1]),
            (6.5f,  UITheme.ClubBlue[2], UITheme.Amber[3]),
            (8.0f,  UITheme.ClubBlue[3], UITheme.Cyan[4]),
            (13.0f, UITheme.ClubBlue[4], UITheme.Cyan[4]),
            (16.0f, UITheme.ClubBlue[3], UITheme.Amber[4]),
            (18.0f, UITheme.Magenta[2],  UITheme.Amber[3]),
        };

        private RectTransform _skyPanel, _sun, _sunGlow, _moon, _moonGlow;

        private Image _sunImg, _sunGlowImg, _moonImg, _moonGlowImg, _cityImg;

        private Image[] _skyRows;

        private Image[] _stars;

        private SegmentClock _curtainClock;

        private RectTransform _curtainClockHost;

        private RectTransform _curtain, _curtainCard;

        private Image _curtainImg;

        private Text _curtainWeek, _curtainLeaving, _curtainArriving;

        private CanvasGroup _curtainCardGroup, _curtainLeavingGroup, _curtainArrivingGroup;

        private readonly List<(Image sign, Image bloom, Text name)> _curtainCells =
            new List<(Image, Image, Text)>();

        private int _curtainVip = -1;          // Saturday's fitting, on the card's own copy
        private int _curtainStoryNight = -1;   // ...and which night the arc is due on

        /// <summary>Where the week instrument hangs on the day card, and how much bigger.
        /// 1.4 draws its 454 units of glass at 636 - wide enough to be the card's foot
        /// without touching the 700 the card itself is.</summary>
        private const float CurtainWeekY = -452f, CurtainWeekScale = 1.4f;

        private int _curtainFrom = 1, _curtainTo = 1;

        private float _curtainT;          // seconds elapsed, 0 → CurtainTotal
        // Four movements on one clock, SIX SECONDS (2026-08-15, the author: "gün geçişinde
        // takvim gözüktüğü sahne daha yavaş aksın, şu an 3 saniye ise 6 saniye olsun").
        //
        // The first cut ran 3.4 — the length of a transition, which is what it was before it
        // had anything in it. With a date on it, it is a SCENE, and the two want opposite
        // things: a transition is over before you notice it, a scene waits for you.
        //
        // The middle movement is THE DAY now (2026-08-25) rather than a hand-off between two
        // words: sixteen hours of sky in three and a half seconds, with the names changing
        // over inside its first half. The hold sits on 18:00 — the same beat as before, now
        // with an hour to land on — and the total is a little over seven seconds, which is
        // as long as a time-skip may take before it stops being a rest and starts being a
        // wait.
        private const float CurtainFadeIn = 0.45f;   // black is instant; the card arrives
        private const float CurtainDay = 3.60f;      // 02:00 → 18:00, sun, sky and readout
        private const float CurtainHold = 1.25f;     // the hour stands where it landed
        private const float CurtainLift = 1.70f;     // card out, room up
        private const float CurtainTotal = CurtainFadeIn + CurtainDay + CurtainHold + CurtainLift;

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
    }
}
