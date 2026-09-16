using System;
using System.Collections.Generic;
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
    /// The service flow (PLAN_tycoon_pivot P4, GDD 24 §1–3): the bottles leave the stage
    /// into a menu, the drink is built in a focused shaker stage, and it is poured into a
    /// glass by aim before being handed to a seat. A dimmed modal over the floor; the floor
    /// keeps running underneath (patience does not pause while you mix).
    ///
    /// Interim visuals — panels, bars and readouts, not the animated shaker of P8. The point
    /// of this phase is the *flow* and the *spill-by-aim*, both of which are real here.
    /// </summary>
    public sealed partial class TycoonServiceFlow : MonoBehaviour
    {
        /// <summary>Something is in the hand on the bench: a bottle being poured, the bar
        /// spoon, or the pint under the tap (2026-09-08, for the cursor's grab frame).</summary>
        public bool IsHolding => IsOpen && (_bottleGrabbed || _spoonHeld || _glassHeld);

        /// <summary>The serve glass's rim strip and its rect (2026-09-08).</summary>
        private Image _serveGlassLip;
        private RectTransform _serveGlassLipRt;

        /// <summary>Keeps the rim strip on the glass wherever the glass is: called from the
        /// serve stage's step. The strip's top-centre is the glass rect's top-centre plus
        /// the placement the piece measured.</summary>
        private void FollowServeGlassLip()
        {
            if (_serveRimOver != null && _serveGlass != null)
            {
                _serveRimOver.anchoredPosition = _serveGlass.anchoredPosition;
                _serveRimOver.sizeDelta = _serveGlass.sizeDelta;
                _serveRimOver.localRotation = _serveGlass.localRotation;
                _serveRimOver.localScale = _serveGlass.localScale;
            }
            if (_serveGlassLip == null || !_serveGlassLip.enabled || _serveGlass == null) return;
            // The SIZE too, every frame (2026-09-14): ShowServingGlassware sized the crop off the
            // PREVIOUS glass's box, before the new glass's size was set, so a change of glass left the
            // front at the last glass's scale (measured: a highball's front drawn 0.64 of its size).
            if (!_serveGlassPiece.LipPlacement(_serveGlass.sizeDelta, out var lipSize, out var lipAt)) return;
            if ((_serveGlassLipRt.sizeDelta - lipSize).sqrMagnitude > 0.01f) _serveGlassLipRt.sizeDelta = lipSize;   // Piece is a struct; LipPlacement answers false with no sprite
            var g = _serveGlass;
            // Turned with the glass (2026-09-14): the glass rocks while it catches, and the strip's offset from
            // the glass's centre turns with it, or the front would slide off the rim as it leans.
            _serveGlassLipRt.anchoredPosition = g.anchoredPosition
                + (Vector2)(g.localRotation * new Vector3(lipAt.x, g.sizeDelta.y * (1f - g.pivot.y) + lipAt.y, 0f));
            _serveGlassLipRt.localRotation = g.localRotation;
            _serveGlassLipRt.localScale = g.localScale;
        }

        [SerializeField] private Font bodyFont;
        [SerializeField] private Font displayFont;

        private GameBootstrap _bootstrap;
        private TycoonRun Run => _bootstrap != null ? _bootstrap.Tycoon : null;

        private Font _body;
        private Font _display;

        // THE BACK-BAR PAGE IS GONE (2026-08-22, the author: "o sahne artık olmayacak
        // silinecek"). It was the hub every bench hung off; the counter's own cellar took
        // that job, standing open in the room behind whichever bench is out. What went with
        // it: TycoonServiceFlow.Menu.cs whole, the wall of bottles it drew, Open(), and the
        // forward/back reading of the slide that only made sense with a hub in the middle.
        private enum Stage { Closed, Shaker, Serve, Tap }
        private Stage _stage = Stage.Closed;

        private RectTransform _root;        // the whole modal (scrim + panels)
        private RectTransform _field;       // the fixed 1280x720 field the panels are built in
        // THE STAGES SLIDE (2026-08-11, the author's loop rework: "keskin geçiş olmamalı").
        // One timer drives BOTH panels — the outgoing pushed off one way, the incoming
        // arriving from the other — so a stage change reads as the bar moving past the
        // camera, not as a screen swap. Update-timer, not a coroutine: the HUD's PlayPanel
        // family already paid for that lesson (an interrupted coroutine parks panels at
        // their start offsets), and this is its two-slot sibling.
        private RectTransform _slideOutRt, _slideInRt;
        private CanvasGroup _slideInGroup;
        private float _transT, _transDur;
        private float _slideDir;            // +1: forward (in from the right), -1: back
        private bool _slideFade;            // Closed→Menu opens with a fade, not a push
        private const float SlideDur = 0.16f;

        /// <summary>
        /// THE ONE SLOW MOVE (2026-09-06, the author: "shaker->bardak sahne gecis
        /// animasyonunda gecis daha yavas olmali ... bu animasyonda hizli olmasin"). Every
        /// other stage change is a cut with a push behind it; this one is the drink being
        /// carried from the tin to the glass, and it is the only move in the game where
        /// the player is meant to watch the thing travel.
        /// </summary>
        /// <remarks>A FIELD, not a const, so a probe in play can stretch it and watch what
        /// the slide actually does frame by frame — which is how the chrome pinning below
        /// was measured rather than believed (2026-09-06).</remarks>
        private static float BenchSlideDur = 0.42f;

        /// <summary>How much of the slide runs at full speed before the brake bites.</summary>
        private const float BrakePoint = 0.86f;

        /// <summary>What the props do when it stops: how far they carry on, how fast they
        /// rock it off and how quickly that dies (2026-09-06, "tam durdugu sirada ani fren
        /// etkisi yasasin"). The bench travels at one speed and then simply STOPS, and
        /// everything standing on it keeps going for a moment — which is the whole read.</summary>
        private const float LurchUnits = 22f, LurchHz = 4.2f;
        private static float LurchLife = 0.42f;   // a field for the same reason as BenchSlideDur

        private bool _benchSlide;           // this transit is bench-to-bench: slow, braked
        private RectTransform _lurchRt;     // the surface that is still catching up
        private float _lurchT, _lurchDir;

        /// <summary>
        /// The chrome both benches wear in the same place: the slab, the way back and the
        /// bin. It is REGISTERED rather than reparented — each panel keeps building its
        /// own, and during a bench-to-bench slide every copy is pushed the other way by
        /// exactly what the panel is doing, so what the player sees is one set of controls
        /// standing still while the work slides past behind it (2026-09-06, the author:
        /// "UI ve butonlar degismiyorsa sabit kalmali").
        /// </summary>
        private readonly List<(RectTransform Panel, RectTransform Child, Vector2 Home)> _fixedChrome
            = new List<(RectTransform, RectTransform, Vector2)>();

        private void RegisterFixed(RectTransform panel, RectTransform child)
        {
            if (panel == null || child == null) return;
            _fixedChrome.Add((panel, child, child.anchoredPosition));
        }

        /// <summary>Constant speed, then a short hard stop: the curve of a thing that was
        /// being pushed and is not any more. OutCubic decelerates from the first frame,
        /// which is the opposite of what braking looks like.</summary>
        private static float Brake(float k)
        {
            if (k <= BrakePoint) return k / BrakePoint * (1f - (1f - BrakePoint) * 0.5f);
            float t = (k - BrakePoint) / (1f - BrakePoint);
            float head = 1f - (1f - BrakePoint) * 0.5f;
            return head + (1f - head) * Tweening.OutCubic(t);
        }
        private const float SlideDist = 1280f;
        private bool InTransit => _slideOutRt != null || _slideInRt != null || _closing;
        private CanvasGroup _rootGroup;     // raycasts off while the field is moving

        /// <summary>
        /// THE ROOM DIMS WHILE A DRINK IS MADE (2026-09-13, the author: "alkol yapma esnasında ana
        /// sahne biraz karartılabilir"). Overturns 2026-08-22's "karartma olmasın" by the author's
        /// own word, and only by a little: a veil over the room below the top bar, under the
        /// bench, so the counter you are working at is the bright thing on the screen and the
        /// drinkers are still there to be seen.
        /// </summary>
        private Image _roomDim;
        private const float RoomDimAlpha = 0.5f;   // 0.34 measured as a 14% drop on the wall — too faint to read
        // THE ROOM BEHIND THE BENCH, DIMMED A LITTLE (2026-09-17, the author: "müşterilerin olduğu arkaplan biraz
        // karartılır"): the veil that was switched off on 2026-08-22 ("karartma olmasın") comes back at a third.
        private const float RoomDimBench = 0.62f;   // 0.30 measured lighter than the old veil (wall 176 vs 151 of 216); 0.62 lands near 135
        /// <summary>The HUD's top bar, which the veil stops under (TycoonHud.TopBarH).</summary>
        private const float HudTopBarH = 54f;

        // THE BENCH CLOSES, IT DOES NOT VANISH (2026-09-13, the author: "her şeyin kapanma
        // animasyonu atlanmamalı"). Closing used to switch the whole flow off in one frame; it
        // fades and sinks now, and only then goes off. The STATE is closed at once — the room is
        // live under the fade — only the picture takes its time.
        private bool _closing, _fadeRoot;
        private float _closeT;
        private RectTransform _closePanel;
        private float _benchCounterTop = -1f;     // the drawer height the bench bands were last lined up to
        private static float CloseDur = 0.24f;   // a field for the same reason as BenchSlideDur
        private const float CloseDrop = 36f;
        private RectTransform _shakerPanel;
        private RectTransform _servePanel;

        private RectTransform _bottleList;

        private IngredientCard _focusBottle;
        private Text _shakerTitle;
        private Text _shakerReadout;

        // The tilt-pour (GDD 24 §2): grab the bottle, lift it, and it leans left toward the
        // shaker; liquid streams from the mouth only while the mouth is tilted over the
        // shaker's opening. Purely procedural placeholder art — P8 re-skins it.
        private RectTransform _pourSurface;   // the interaction area inside the shaker panel
        private HoverGlow _tinGlow;           // the tin's own answer, off until the lid is on
        private RectTransform _shakerVessel;  // the target, opening at its top
        private RectTransform _shakerTop;     // the cap: drag it onto the tin to close it
        // Capping the tin (2026-07-24): the shaker is open while you build the drink, so the
        // liquid can go in. Drop the cap on its mouth and the bench clears — the props fade
        // out and the tin slides to the middle and grows — so the focus moves to shaking it.
        private bool _capped, _capGrabbed;

        /// <summary>
        /// IS THE TIN CLOSED RIGHT NOW? An empty tin is never capped, whatever the lid was
        /// doing when the drink left it (2026-08-14, the author: binning after a cap sent
        /// the next bottle straight to the glass stage). `_capped` is bench state and the
        /// bench only clears it on the way in; the drink can end anywhere — the bin on the
        /// wall, a customer's hand, a tin that burst — so every decision made OFF the bench
        /// asks this instead, and it cannot be stale because it reads the tin itself.
        /// </summary>
        private bool Capped => _capped && Run != null && !Run.Glass.IsEmpty;
        private float _capT;                  // 0 = open on the bench, 1 = capped and centred
        private Vector2 _capRest, _capPos;
        private Vector2 _shakerOpenSize;
        private readonly List<CanvasGroup> _benchProps = new List<CanvasGroup>();
        private const float CapCentreX = 0f;
        // ONE SHAKER, ONE SIZE (2026-09-04, the author: "tüm shakerlar aynı boyutta
        // gözükmeli"). This was 1.3: capping the tin grew it by a third, and the serve
        // bench — which is where that same capped tin arrives — draws it at 1.0. So the
        // object you had just closed shrank by a quarter on its way to the glass, and it
        // was the SAME DRAWING at two sizes, which is the one thing a prop must never be.
        // The focus the growth was buying is still bought twice over: the props fade out
        // and the tin slides to the middle of the bench (UpdateCap), which says "this is
        // the thing you are about to shake" without telling the player it changed size.
        private const float CapGrowth = 1.0f;
        private const float CapArtOffset = 0.245f;   // the lid art sits this far above its rect centre
        /// <summary>
        /// THE TIN'S ONE SIZE, ON BOTH BENCHES (2026-09-06, the author: "shaker ve bardak
        /// sahnelerinde bardak boyutuyla shaker boyutu orantili degil bunlari orantila.
        /// Kucuk olmasinlar"). The tin was 200x358 and the glass 190x260, which drew a
        /// 300-unit shaker beside a 244-unit highball — 1.23 of it, where a real 23cm
        /// shaker stands 1.5 times a 15cm glass. The ratio is fixed by GROWING the tin
        /// rather than shrinking the glass, which is the other half of what was asked.
        /// The size itself is the sheet at a WHOLE multiple — 2x of 116x208 — because a
        /// pixel drawing at 1.72 puts some of its pixels on two screen pixels and some on
        /// three; the capped tin lands at 348 against a highball's 244, which is 1.43 of
        /// it against a real bar's 1.53. Every other number on these benches is a fraction
        /// of this rect (the cavity, the cap's seat, the cap's own plate), so they follow.
        /// </summary>
        private const float TinW = 232f, TinH = 416f;   // EXACTLY 2x the 116x208 sheet
        private const float CavityFloor = 0.0913f, CavityRim = 0.6106f;
        private const float GridGap = 6f;
        private Vector2 _listHome;
        // The board draws one art pixel as ~5.8 screen pixels. Halving the key's pixels-per-unit
        // puts its grain at 4, so the keys read as the same piece of pixel art as the sheet they
        // sit on rather than a finer sticker laid over it (2026-07-27).
        private const float PlatePixelScale = 0.5f;
        // 64 units against 32px art puts one art pixel on 4 screen pixels — the same grain as the
        // keys, so the corner controls belong to the same drawing (2026-07-27).
        private const float CornerSize = 64f;

        /// <summary>The bin key. Wide enough to carry a mark AND the word, which is what
        /// the round button could not do, and tall enough that the cap's six pixels of
        /// throw read as travel rather than as a flicker.</summary>
        private const float BinKeyW = 150f, BinKeyH = 52f;


        private void Awake()
        {
            var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _body = LanguageFonts.Body(bodyFont != null ? bodyFont : legacy);          // L3
            _display = LanguageFonts.Display(displayFont != null ? displayFont : legacy);
            _bootstrap = GetComponent<GameBootstrap>();
            BuildUi();
        }

        public bool IsOpen => _stage != Stage.Closed;

        /// <summary>
        /// Straight to the draught station, because the tap is its own door (2026-08-15, the
        /// author: "musluğa tıklanınca direkt bira koyma sahnesi gelecek"). The kegs used to
        /// stand on the back-bar floor and opening one came here; beer has left the wall, and
        /// the font standing on the counter is what the player walks to instead. The station
        /// couples whatever keg the cellar has on hand, so this needs no argument — see
        /// <see cref="RefreshTap"/>.
        /// </summary>
        public void OpenTap()
        {
            if (Run == null || Run.Phase != TycoonPhase.DayOpen) return;
            // NOT WHILE A COCKTAIL IS BEING BUILT (2026-09-04, the author: "kokteyl yapim
            // esnasindayken bira yapma sahnesine girilmemeli"). Core has always refused the
            // KEG over a half-built drink; what it could not refuse was the walk over to the
            // font, so the player arrived at a station whose handle did nothing. The room
            // says why (TycoonHud.OnTapClicked toasts it) and this is the second lock: the
            // flow asks the rules rather than trusting the door it was opened by.
            if (Run.BuildingACocktail) return;
            // AND THE ROOM LIFTS, as it does for the other two benches (2026-09-16, measured): the plaque under the
            // rail hangs from the room's counter line, and with the drawer shut that line runs UNDER the draught
            // bench's own counter — the beer's name and the verdict were hanging behind the keg rack.
            GetComponent<TycoonHud>()?.Room?.SetDrawerOpen(true);
            GoTo(Stage.Tap);
        }

        /// <summary>
        /// A bottle taken out of the counter's own cellar (2026-08-22). The cellar is the back
        /// bar now — it is the same pick, so it takes the same road: <see cref="OpenBottle"/>
        /// decides where the bottle is carried to, and the lid still decides whether that is
        /// the tin or the glass. What is NOT here is a stop at the wall: the player already
        /// has the bottle in hand, so opening the old menu page on the way would be a room
        /// they walk through without touching anything.
        /// </summary>
        public void PickFromCellar(IngredientCard card)
        {
            if (card == null || Run == null || Run.Phase != TycoonPhase.DayOpen) return;
            OpenBottle(card);
        }

        /// <summary>
        /// Back to the tin (2026-09-06, the author: "ana sahnedeki shakera basip o sahneye
        /// geri donebilir"). The bench is not a place you can only be sent to any more: a
        /// drink left standing in the tin puts the shaker on the counter, and the shaker is
        /// the way back to the work. Guarded like every other door — the room can offer it
        /// when the rules would not, so the rules are asked here rather than trusted there.
        /// </summary>
        public void OpenShaker()
        {
            if (Run == null || Run.Phase != TycoonPhase.DayOpen) return;
            // AND THE ROOM MOVES, whichever door you came through (2026-09-06, the author:
            // "mahzen kapaliyken shaker sahnesine gecildiginde arkaplan kaymiyor"). Until
            // the tin stood on the counter the bench could only be reached FROM the cellar,
            // so the room was always already lifted when it opened; coming straight off the
            // counter left the bar sitting where it was and the bench slid up over a room
            // that had not moved. The drawer is what lifts it, and it stays open behind the
            // bench exactly as it does on the old road — BACK TO THE BAR keeps it open,
            // SERVE IT shuts it.
            GetComponent<TycoonHud>()?.Room?.SetDrawerOpen(true);
            GoTo(Stage.Shaker);
        }

        /// <summary>
        /// The door onto the GLASS bench, for the drink standing on the counter (2026-09-06,
        /// the author: "bardak varsa bardak sahnesine shaker varsa shaker sahnesine gitmeli").
        /// The same room lift the tin's door takes: the bench slides in over a raised bar.
        /// </summary>
        public void OpenServe()
        {
            if (Run == null || Run.Phase != TycoonPhase.DayOpen) return;
            GetComponent<TycoonHud>()?.Room?.SetDrawerOpen(true);
            GoTo(Stage.Serve);
        }

        public void CloseFlow() => GoTo(Stage.Closed);

        /// <summary>
        /// THE BENCHES, BUILT AGAIN IN THE COUNTER'S FINISH (2026-09-16, the author: "Tezgah rengi değişince Built
        /// sahnesindeki tezgah arkaplanı da ona göre değişmeli. Ona göre de o sahnedeki UI renkleri de değişmeli").
        /// Every recess, rim, needle and counter tile reads CounterFinish.Current as it is built, so a repaint is a
        /// rebuild: the whole flow canvas goes and BuildUi lays it again. Only with the flow closed — a bench in use
        /// is not torn down under the hand; the HUD asks again next frame.
        /// </summary>
        public void RebuildBenches()
        {
            if (_root == null || IsOpen) return;
            var canvasGo = _root.parent != null ? _root.parent.gameObject : _root.gameObject;
            _railHung.Clear(); _benchCounters.Clear(); _benchProps.Clear(); _fixedChrome.Clear();
            _stepRows.Clear(); _serveStepRows.Clear(); _gaugeBands.Clear();
            _benchCounterTop = -1f;
            Destroy(canvasGo);
            BuildUi();
        }

        /// <summary>Every stage change kills the held-action sound: a loop belongs to the
        /// stage that started it, and a closed stage must not keep pouring in the dark.</summary>
        private void StopHeldSounds() => Sfx.HoldLoop(null);

        private void Update()
        {
            // The slide steps FIRST and unconditionally — the curtain's own law: a visual
            // that gates input must never be starved by an early return.
            StepStageSlide();
            StepBenchLurch();
            StepBenchEntrance();
            // The band rides the drawer while a bench is up (2026-09-13): the tin's door on the
            // counter opens the drawer and the bench in the same frame, and a band lined up once
            // stayed at the shut room's height behind a room that then rose.
            if (_stage != Stage.Closed && !_closing) AlignBenchCounters();

            var run = Run;
            if (run == null) return;

            if (_stage != Stage.Closed && run.Phase != TycoonPhase.DayOpen)
            {
                GoTo(Stage.Closed);
                return;
            }

            // A half-slid stage takes no input and runs no verbs: the panels are moving
            // scenery until the slide settles (raycasts are off at the root for the same
            // reason — two independent locks).
            if (InTransit) return;

            // The shake moves the tin, so the lid is placed AFTER it — placing it first left the
            // cap a frame behind the body, which is why they did not read as one object. The
            // drink is placed last for the same reason: it belongs inside the tin, so it can
            // only be positioned once the tin has finished moving (2026-07-28).
            if (_stage == Stage.Shaker)
            {
                _shakerLoopWanted = null;
                _saidThisFrame = false;   // the readout is unclaimed until something claims it
                StepBenchDemand();        // …unless the player was sent here to be told something
                UpdateShake(run); UpdateTiltPour(run); UpdateCap(run);
                UpdateStir(run); UpdateToGlass(run); UpdateStepCard(run);
                // LAST, and after UpdateCap: a tin that has just gone off owns where the lid
                // and the body are, and UpdateCap would otherwise walk them both home in the
                // same frame the bang was drawn.
                StepBlowout();
                FollowTinFront();                // after the blowout: the plate rides wherever the tin ended up (2026-09-08)
                // LAST of the bench's own steps: the meter withdraws on the first frame no
                // hand claimed it, so whichever verb wrote to it above has already had its say.
                StepWorkMeter();
                StepShakerFluid(run);
                // THE WORK IS AUDIBLE (2026-08-27). This passed one fixed level for every
                // loop, so the two verbs the bench is ABOUT — shaking and stirring — sounded
                // identical however hard you worked them, even though both energies are
                // measured from real cursor travel every frame. Each verb now hands its own
                // energy to the loop, which turns it into level AND pitch (see Sfx.HoldLoop);
                // a pour has none to give, because a pour runs at the rate a bottle pours.
                Sfx.HoldLoop(_shakerLoopWanted,
                             _shakerLoopWanted == "shake_loop" ? 0.9f : 0.8f,
                             _shakerLoopWanted == "shake_loop" ? (float)_shakeEnergy
                           : _shakerLoopWanted == "stir_loop" ? (float)_stirEnergy
                           // The tin rises as it fills, exactly as the glass does.
                           : _shakerLoopWanted == "pour_tin" ? (float)run.Glass.FillFraction
                           : -1f,
                             // ...and a pour drops near or far, left or right of the bench (2026-09-15).
                             _shakerLoopWanted == "pour_tin" ? _pourFall01 : -1f,
                             _shakerLoopWanted == "pour_tin" ? _pourPan : 0f);
                // THE TIN MOVING (2026-09-15, the author: "Bardağın hareketine ... göre değişen sese ihtiyacımız var"): the
                // drink swashing in the open tin as it slides under a stream and rocks; nothing while it is capped, when the
                // shake has the sound.
                Sfx.HoldMotion(!_capped && _capT <= 0f ? "slosh_tin" : null,
                               CatchMotion(_tinCatchV, _tinSwayV, run.Glass.FillFraction), PanOf(_tinCatchX, _pourSurface));
            }

            if (_stage == Stage.Serve)
            {
                _servePouringNow = false;
                UpdateServeTilt(run);
                FollowServeGlassLip();           // the rim strip rides the tilted glass (2026-09-08)
                UpdateServeStepCard(run); PushServeDone(run);
                // One loop source, driven once per frame from whatever poured (P17): the tin
                // and the hand bottle set the flag, and neither can stop the other's sound.
                Sfx.HoldLoop(_servePouringNow ? "pour_glass" : null, 0.7f,
                             _servePouringNow ? (float)run.ServingGlass.FillFraction : -1f,
                             _serveFall01, _servePan);   // near or far, left or right of the bench (2026-09-15)
                // THE GLASS MOVING (2026-09-15): the same swash in the glass that follows the stream.
                Sfx.HoldMotion("slosh_glass", CatchMotion(_glassCatchV, _glassSwayV, run.ServingGlass.FillFraction),
                               PanOf(_glassCatchX, _serveSurface));
            }

            if (_stage == Stage.Tap) UpdateTap(run);
        }

        // ── stage transitions ────────────────────────────────────────────────────

        /// <summary>The panel a stage lives on; null for Closed.</summary>
        private RectTransform PanelOf(Stage stage) =>
            stage == Stage.Shaker ? _shakerPanel
            : stage == Stage.Serve ? _servePanel
            : stage == Stage.Tap ? _tapPanel : null;

        private void GoTo(Stage stage)
        {
            var previous = _stage;
            _stage = stage;
            // Leaving the glass bench empties the hand (2026-08-25): a dish mid-lap or a
            // piece mid-drag must not survive into another stage — or into the room.
            if (previous == Stage.Serve && stage != Stage.Serve) ResetServeHand();
            // Any slide still in flight settles before the panels are touched — the house
            // "settle before movement" law. State stays SYNCHRONOUS end to end: only the
            // visuals animate, so call sites that act right after GoTo keep their contract.
            SettleStageSlide();
            bool slide = stage != Stage.Closed && previous != Stage.Closed
                      && previous != stage && !Motion.Reduced;
            bool fade = stage != Stage.Closed && previous == Stage.Closed && !Motion.Reduced;
            StopHeldSounds();
            Sfx.Play(slide ? "whoosh" : "click", 0.6f);
            _bottleGrabbed = false;
            _bottleHand.Release();
            _pouring = false;
            _serveGrabbed = false;
            _serveHand.Release();
            _shaking = false;
            _shakeEnergy = 0;
            _spoonHeld = false;
            _stirEnergy = 0;
            _shakerFluid?.Clear();
            _serveFluid?.Clear();
            if (Run != null && Run.PouringId != null) Run.EndPour();

            // A closing bench keeps its panel up for the fade (2026-09-13); the settle takes it down.
            bool closing = stage == Stage.Closed && previous != Stage.Closed && !Motion.Reduced
                           && _root != null && _root.gameObject.activeSelf;
            _root.gameObject.SetActive(stage != Stage.Closed || closing);
            // A sliding stage keeps its OUTGOING panel alive for the transit; the slide's
            // settle turns it off. Everything else applies exactly as it always has.
            bool keepOut = slide || closing;
            _shakerPanel.gameObject.SetActive(stage == Stage.Shaker || (keepOut && previous == Stage.Shaker));
            _servePanel.gameObject.SetActive(stage == Stage.Serve || (keepOut && previous == Stage.Serve));
            _tapPanel.gameObject.SetActive(stage == Stage.Tap || (keepOut && previous == Stage.Tap));
            _glassHeld = false;
            _glassTilt = 0f;
            if (_tapGlass != null)
            {
                _tapGlass.anchoredPosition = _tapGlassRest;
                _tapGlass.localRotation = Quaternion.identity;
            }
            _tapFluid?.Clear();
            if (Run != null && Run.PullingId != null) Run.EndPull();

            // Not while closing: the band follows the drawer, and the drawer shuts on the same
            // click, so it snapped up over the room on the fade's first frame (measured 2026-09-13).
            if (!closing) AlignBenchCounters();
            // THE TIN THE BAR OWNS, not the one it opened with (2026-09-06): the gold shaker
            // is a rung, and a rung can be bought between two visits to this bench.
            if (stage == Stage.Shaker || stage == Stage.Serve) DressShakerArt();
            if (stage == Stage.Shaker) RefreshShaker();
            if (stage == Stage.Serve) RefreshServe();
            if (stage == Stage.Tap) RefreshTap();

            // The visuals, last — the state above is already true whatever these draw.
            if (slide)
            {
                // Forward reads left-to-right. With the hub gone there is one forward
                // move left in the game: the bench hands the capped tin ON to the glass.
                // Everything else — including arriving from the room — is the way back.
                bool forward = previous == Stage.Shaker && stage == Stage.Serve;
                bool bench = (previous == Stage.Shaker || previous == Stage.Serve)
                          && (stage == Stage.Shaker || stage == Stage.Serve);
                // THE TIN STAYS, THE WORDS FADE (2026-09-17, the author: "Kapanan shaker ekranda kalacak ... ekrandan
                // shaker gitmeyecek, tezgah arkaplanıda sabit kalacak. Bilgi ve açıklama paneli sabit kalacak ve
                // üstündeki yazılar fade olup değişecek"): between the two benches nothing slides any more — one
                // panel fades out over the other fading in, on the same counter, the plaque in the same place.
                if (bench) PlayStageCross(PanelOf(previous), PanelOf(stage));
                else PlayStageSlide(PanelOf(previous), PanelOf(stage), forward ? 1f : -1f, false);
            }
            else if (fade)
                PlayStageFade(PanelOf(stage));
            else if (closing)
                PlayStageClose(PanelOf(previous));
            // THE ENTRANCE (2026-09-17, the author: "şişe hariç tüm her şey aşağıdan gelip oturma ... şişe ise ekranın
            // üstünden ... sallanarak düşecek"): opening the shaker bench, every prop rises from under the counter and
            // settles past its rest; the bottle swings down from the top. The glass bench's glass drops the same way.
            if (stage == Stage.Shaker && previous == Stage.Closed)
                PlayBenchEntrance(_shakerPanel, _pourSurface, _pourBottle, _bottleRest);
            if (stage == Stage.Serve && !Motion.Reduced)
            {
                _serveDropStart = Time.unscaledTime;
                _serveDropDur = 0.6f / Mathf.Max(1f, LastCall.Game.Ceremony.Pace);
            }

            // THE SCENE A LITTLE LOWER BEHIND A BENCH (2026-09-14, the author: "sahneyi biraz daha
            // aşağı alıp şişe ve shakera diklemesine daha çok alan tanıyabilirsin", and "konuşma
            // metinleri karakterlerin kafasının üstünde çıkmıyor"). With the drawer all the way up the
            // heads stood twenty units under the top bar and every balloon was pressed down over a
            // face (measured). A bench holds the room at BenchDrawer; back to the cellar, it rises
            // the rest of the way so the shelves are whole.
            var room = GetComponent<TycoonHud>()?.Room;
            if (room != null && room.DrawerOpen)
                room.SetDrawerOpen(true, false, stage == Stage.Closed ? 1f : BenchDrawer);
        }

        /// <summary>How far the cellar's drawer stands open while a bench is up (2026-09-14).</summary>
        // 0.72, not 0.85: at 0.85 the balloons over the heads cleared them by 9, −1 and 9 units (measured).
        private const float BenchDrawer = 0.72f;

        // ── the two-slot stage slide ────────────────────────────────────────────

        private CanvasGroup GroupOn(RectTransform rt)
        {
            // Unity's GetComponent returns a fake-null, which ?? happily hands back — check it.
            var grp = rt.GetComponent<CanvasGroup>();
            if (grp == null) grp = rt.gameObject.AddComponent<CanvasGroup>();
            return grp;
        }

        private void PlayStageSlide(RectTransform outRt, RectTransform inRt, float dir,
                                   bool bench = false)
        {
            _slideOutRt = outRt;
            _slideInRt = inRt;
            _slideInGroup = GroupOn(inRt);
            _slideInGroup.alpha = 1f;
            _slideDir = dir;
            _slideFade = false;
            _benchSlide = bench;
            _transT = 0f;
            _transDur = bench ? BenchSlideDur : SlideDur;
            inRt.anchoredPosition = new Vector2(dir * SlideDist, 0f);
            if (_rootGroup != null) _rootGroup.blocksRaycasts = false;
        }

        /// <summary>Closed→Menu: the flow OPENS rather than arrives, so the first panel
        /// fades up in place instead of shoving in from a direction that means nothing.</summary>
        /// <summary>One bench over the other, in place (2026-09-17): the incoming panel fades in at zero while the
        /// outgoing fades out where it is; nothing slides, the fixed chrome never moves, no lurch after.</summary>
        private void PlayStageCross(RectTransform outRt, RectTransform inRt)
        {
            _slideOutRt = outRt;
            _slideInRt = inRt;
            _slideInGroup = GroupOn(inRt);
            _slideInGroup.alpha = 0f;
            _crossOutGroup = GroupOn(outRt);
            _crossOutGroup.alpha = 1f;
            _slideFade = true;
            _fadeRoot = false;
            _benchSlide = false;
            _transT = 0f;
            _transDur = CrossDur / Mathf.Max(1f, LastCall.Game.Ceremony.Pace);
            inRt.anchoredPosition = Vector2.zero;
            outRt.anchoredPosition = Vector2.zero;
            if (_rootGroup != null) _rootGroup.blocksRaycasts = false;
        }
        private CanvasGroup _crossOutGroup;
        private const float CrossDur = 0.4f;

        // ── THE ENTRANCE ─────────────────────────────────────────────────────────────────────────────────────
        private float _entranceT = -1f, _entranceDur;
        private readonly List<(RectTransform rt, Vector2 rest)> _entranceMovers = new List<(RectTransform, Vector2)>();
        private RectTransform _entranceSurface, _entranceBottle;
        private Vector2 _entranceSurfaceRest, _entranceBottleRest;
        private const float EntranceRise = 380f, EntranceDrop = 720f;
        private float _serveDropStart = -1f, _serveDropDur = 0.6f;

        private void PlayBenchEntrance(RectTransform panel, RectTransform surface, RectTransform bottle, Vector2 bottleRest)
        {
            if (Motion.Reduced || panel == null) return;
            _entranceMovers.Clear();
            foreach (Transform t in panel)
            {
                var rt = t as RectTransform;
                if (rt == null || !rt.gameObject.activeSelf) continue;
                if (_benchCounters.Contains(rt)) continue;                 // the slab is the room's; it stays
                bool hung = false;                                          // the rail-hung ride the alignment instead
                foreach (var (h, _) in _railHung) if (h == rt) { hung = true; break; }
                if (hung) continue;
                _entranceMovers.Add((rt, rt.anchoredPosition));
            }
            _entranceSurface = surface; _entranceSurfaceRest = surface != null ? surface.anchoredPosition : Vector2.zero;
            _entranceBottle = bottle; _entranceBottleRest = bottleRest;
            _entranceT = 0f;
            _entranceDur = 0.55f / Mathf.Max(1f, LastCall.Game.Ceremony.Pace);
            StepBenchEntrance();
        }

        /// <summary>Every frame of the entrance: each mover rises on an ease that overshoots its rest and comes back,
        /// a little later than the one before; the bottle, inside the surface, is held against the surface's own
        /// motion and dropped from the top with a damped swing.</summary>
        private void StepBenchEntrance()
        {
            if (_entranceT < 0f) return;
            _entranceT += Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // a hitch must not throw the props home in one frame
            bool done = true;
            float dur = Mathf.Max(0.001f, _entranceDur);
            {
                // the rail-hung (the plaque, the mat, the lemons) take the same rise through AlignBenchCounters, which
                // keeps setting their places while the room lifts under the bench
                float k0 = Mathf.Clamp01(_entranceT / dur);
                float u0 = k0 - 1f;
                float e0 = u0 * u0 * ((1.70158f + 1f) * u0 + 1.70158f) + 1f;
                _entranceRailOffset = -EntranceRise * (1f - e0);
                if (k0 < 1f) done = false;
            }
            for (int i = 0; i < _entranceMovers.Count; i++)
            {
                var (rt, rest) = _entranceMovers[i];
                if (rt == null) continue;
                float k = Mathf.Clamp01((_entranceT - i * 0.03f / Mathf.Max(1f, LastCall.Game.Ceremony.Pace)) / dur);
                if (k < 1f) done = false;
                const float s = 1.70158f;            // ease-out-back: past the rest, then home
                float u = k - 1f;
                float e = u * u * ((s + 1f) * u + s) + 1f;
                rt.anchoredPosition = rest + new Vector2(0f, -EntranceRise * (1f - e));
            }
            if (_entranceBottle != null && _entranceBottle.gameObject.activeSelf)
            {
                float kb = Mathf.Clamp01(_entranceT / (dur * 1.25f));
                if (kb < 1f) done = false;
                float fall = (1f - kb) * (1f - kb);
                Vector2 surfaceShift = _entranceSurface != null ? _entranceSurface.anchoredPosition - _entranceSurfaceRest : Vector2.zero;
                _entranceBottle.anchoredPosition = _entranceBottleRest - surfaceShift + new Vector2(0f, EntranceDrop * fall);
                _entranceBottle.localRotation = Quaternion.Euler(0f, 0f, 14f * Mathf.Sin(kb * 9.42f) * (1f - kb));
            }
            if (done)
            {
                foreach (var (rt, rest) in _entranceMovers) if (rt != null) rt.anchoredPosition = rest;
                if (_entranceBottle != null) { _entranceBottle.anchoredPosition = _entranceBottleRest; _entranceBottle.localRotation = Quaternion.identity; }
                _entranceMovers.Clear();
                _entranceT = -1f;
                _entranceRailOffset = 0f;
                _benchCounterTop = -1f;   // one more alignment, so the rail-hung land exactly where the rail is now
            }
        }
        private float _entranceRailOffset;

        /// <summary>The glass bench's glass, on its way down (2026-09-17): how far above its rest it still is, and its swing.</summary>
        private float ServeDropOffset()
        {
            if (_serveDropStart < 0f) return 0f;
            float kb = Mathf.Clamp01((Time.unscaledTime - _serveDropStart) / Mathf.Max(0.001f, _serveDropDur));
            if (kb >= 1f) { _serveDropStart = -1f; return 0f; }
            return EntranceDrop * (1f - kb) * (1f - kb);
        }
        private float ServeDropSwing()
        {
            if (_serveDropStart < 0f) return 0f;
            float kb = Mathf.Clamp01((Time.unscaledTime - _serveDropStart) / Mathf.Max(0.001f, _serveDropDur));
            return 14f * Mathf.Sin(kb * 9.42f) * (1f - kb);
        }

        private void PlayStageFade(RectTransform inRt)
        {
            _slideOutRt = null;
            _slideInRt = inRt;
            _slideInGroup = GroupOn(inRt);
            _slideInGroup.alpha = 0f;
            _slideFade = true;
            _transT = 0f;
            _transDur = SlideDur;
            inRt.anchoredPosition = Vector2.zero;
            if (_rootGroup != null) { _rootGroup.blocksRaycasts = false; _rootGroup.alpha = 0f; }
            _fadeRoot = true;   // the veil and the counter come up with the bench (2026-09-13)
        }

        /// <summary>Stage→Closed: the whole flow fades out while the bench sinks a little, then
        /// switches off. The pointer is the room's from the first frame of it.</summary>
        private void PlayStageClose(RectTransform outRt)
        {
            _closing = true;
            _closeT = 0f;
            _closePanel = outRt;
            if (_rootGroup != null) _rootGroup.blocksRaycasts = false;
        }

        private void StepStageClose()
        {
            _closeT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_closeT / CloseDur);
            float e = k * k;
            if (_rootGroup != null) _rootGroup.alpha = 1f - e;
            if (_closePanel != null) _closePanel.anchoredPosition = new Vector2(0f, -CloseDrop * e);
            if (k >= 1f) SettleStageSlide();
        }

        /// <summary>Everything home, the outgoing panel off, the pointer back on. Called
        /// before any new movement and at the end of every transit — an interrupted slide
        /// can never become a panel's new resting place.</summary>
        private void SettleStageSlide()
        {
            if (_closing)
            {
                _closing = false;
                if (_closePanel != null)
                {
                    _closePanel.anchoredPosition = Vector2.zero;
                    if (_closePanel != PanelOf(_stage)) _closePanel.gameObject.SetActive(false);
                }
                _closePanel = null;
                if (_stage == Stage.Closed && _root != null) _root.gameObject.SetActive(false);
            }
            if (_rootGroup != null) _rootGroup.alpha = 1f;
            _fadeRoot = false;
            if (_slideOutRt != null)
            {
                _slideOutRt.anchoredPosition = Vector2.zero;
                if (_slideOutRt != PanelOf(_stage))
                    _slideOutRt.gameObject.SetActive(false);
            }
            if (_crossOutGroup != null) { _crossOutGroup.alpha = 1f; _crossOutGroup = null; }
            if (_slideInRt != null)
            {
                _slideInRt.anchoredPosition = Vector2.zero;
                if (_slideInGroup != null) _slideInGroup.alpha = 1f;
            }
            // Whatever was held still goes back to where its panel thinks it is.
            foreach (var (panel, child, rest) in _fixedChrome)
                if (child != null) child.anchoredPosition = rest;
            // AND THE WORK CATCHES UP. The bench has stopped dead; the tin, the glass and
            // the shadows standing on it carry on for a beat and rock back.
            if (_benchSlide && _slideInRt != null)
            {
                _lurchRt = SurfaceOf(_stage);
                _lurchDir = _slideDir;
                _lurchT = 0f;
            }
            _benchSlide = false;
            _slideOutRt = null;
            _slideInRt = null;
            _slideInGroup = null;
            if (_rootGroup != null) _rootGroup.blocksRaycasts = true;
        }

        /// <summary>The coordinate space each bench does its work in — what lurches.</summary>
        private RectTransform SurfaceOf(Stage stage) =>
            stage == Stage.Shaker ? _pourSurface : stage == Stage.Serve ? _serveSurface : null;

        /// <summary>
        /// The brake, after the fact: a damped rock along the direction of travel, started
        /// the frame the slide stops. It moves the whole work surface, so the glass, the
        /// tin, their shadows and the drink in them all lurch together — one bench with
        /// things standing on it, rather than a handful of props each doing its own trick.
        /// </summary>
        private void StepBenchLurch()
        {
            if (_lurchRt == null) return;
            if (Motion.Reduced) { _lurchRt.anchoredPosition = Vector2.zero; _lurchRt = null; return; }
            _lurchT += Time.unscaledDeltaTime;
            float k = _lurchT / LurchLife;
            if (k >= 1f)
            {
                _lurchRt.anchoredPosition = Vector2.zero;
                _lurchRt = null;
                return;
            }
            float amp = LurchUnits * (1f - k) * (1f - k);
            float x = -_lurchDir * amp * Mathf.Cos(_lurchT * LurchHz * Mathf.PI * 2f);
            _lurchRt.anchoredPosition = new Vector2(x, 0f);
        }

        private void StepStageSlide()
        {
            if (!InTransit) return;
            if (_closing) { StepStageClose(); return; }
            StepFixedChrome();
            _transT += Time.unscaledDeltaTime;
            float k = _transDur <= 0f ? 1f : Mathf.Clamp01(_transT / _transDur);
            if (k >= 1f) { SettleStageSlide(); return; }
            if (_slideFade)
            {
                float s = k * k * (3f - 2f * k);
                if (_slideInGroup != null) _slideInGroup.alpha = s;
                if (_crossOutGroup != null) _crossOutGroup.alpha = 1f - s;
                if (_fadeRoot && _rootGroup != null) _rootGroup.alpha = s;
                return;
            }
            float e = _benchSlide ? Brake(k) : Tweening.OutCubic(k);
            if (_slideOutRt != null)
                _slideOutRt.anchoredPosition = new Vector2(-_slideDir * SlideDist * e, 0f);
            if (_slideInRt != null)
                _slideInRt.anchoredPosition = new Vector2(_slideDir * SlideDist * (1f - e), 0f);
            StepFixedChrome();
        }

        /// <summary>Holds the shared controls where they were while their panel slides out
        /// from under them. Only on a bench-to-bench move: everywhere else the whole screen
        /// IS the change, and pinning the keys would read as them being left behind.</summary>
        private void StepFixedChrome()
        {
            if (!_benchSlide) return;
            foreach (var (panel, child, rest) in _fixedChrome)
            {
                if (panel == null || child == null) continue;
                if (panel != _slideInRt && panel != _slideOutRt) continue;
                child.anchoredPosition = rest - new Vector2(panel.anchoredPosition.x, 0f);
            }
        }

        /// <summary>
        /// The way back, worn on the left edge centre of every station (the author's loop
        /// rework): one key, one place, every stage — the mirror of the shaker's TO THE
        /// GLASS. Returns to the back bar with the reverse slide.
        /// </summary>
        /// <summary>The one back key, on the left edge. Where it leads is the door the stage was
        /// entered by: the three bench stages hang off the back-bar wall and return to it, while
        /// the draught station's only door is the font standing in the room (2026-08-19, the
        /// author: "bira koyma ekranı açıldıktan sonra geri dönmeye çalışıldığında backbara
        /// dönüyor, ana sahneye dönmeli"), so its key walks back out to the room rather than onto
        /// a wall the player never passed through.</summary>
        /// <summary>Gives a corner control the same press as the section keys: it swaps to
        /// its pressed art and dips as it goes down. (Moved here 2026-08-22 with the back-bar
        /// page it used to live on — the bin and the tap still want it.)</summary>
        private static void GiveKeyPress(RectTransform rt, Button btn, Image img, string pressedName)
        {
            var down = ItemArt.Load(pressedName);
            if (down != null && img.sprite != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var st = btn.spriteState;
                st.pressedSprite = down; st.selectedSprite = img.sprite;
                btn.spriteState = st;
            }
            var sink = rt.gameObject.AddComponent<PressSink>();
            sink.Face = rt; sink.Depth = 6f; sink.Squash = 0.02f;
        }

        /// <summary>Rings a label in black so it stays legible on any coloured key. The ring is
        /// one font-pixel wide and closes on all eight sides — see <see cref="PixelOutline"/>.
        /// (Moved here 2026-08-22; the draught station is what still asks for it.)</summary>
        private static Text Outlined(Text t, float thickness = 2f)
        {
            var o = t.gameObject.AddComponent<PixelOutline>();
            o.EffectColor = new Color(0f, 0f, 0f, 1f);
            o.Distance = thickness;
            return t;
        }

        // EVERY STAGE WALKS BACK OUT TO THE ROOM NOW (2026-08-22, the author: "back to
        // bar dendiginde eski bar sahnesine gidiyor o sahne artik olmayacak"). The bench
        // stages used to hang off a full-screen back-bar wall and return to it; the back
        // bar is the counter's own cellar now, standing open BEHIND the bench, so the way
        // back is to leave the bench rather than to open a page the player never passed
        // through. The draught station already worked this way (2026-08-19); the rest
        // have caught up, and Stage.Menu now has no door left into it.
        /// <summary>Where a bench's own controls live: a strip along the FRONT EDGE of the
        /// bar, at the height a hand rests at. They stood at the vertical middle of the two
        /// side edges until 2026-08-26 (the author: "butonların konumlarını ... tamamen
        /// tekrardan tasarla, çok amatörce duruyor") — 76 wide, 150 tall, three words stacked
        /// one per line, floating halfway up a wall with nothing under them. Nothing in a bar
        /// is operated at shoulder height on a wall; the controls are on the bar.</summary>
        private const float KeyStripY = 26f, KeyStripH = 46f;

        // ── the bench's composition (2026-08-26, the author: "sahnede butonlar nesneler
        // esyalar ust uste biniyor, bir duzen yok, bu sahneye duzen getir") ────────
        //
        // One rule split the knot: PROPS ARE DIEGETIC, CHROME IS NOT. The tin, the bottle,
        // the spoon and the glass are things STANDING ON THE COUNTER, and a thing on the
        // counter is nearer the camera than the wall behind it — so a tall tin drawing over
        // the counter rail into the room is perspective, not a collision. Everything the
        // player READS, though — the card, the gauges, the keys, the text — is an
        // instrument, and instruments stay inside the band below the rail.
        //
        // The band is three columns: instruments left (the step card, the spoon standing
        // against the wall), the work centre (tin, then tin-and-glass), the measures right
        // (mix gauge over the bin). Every prop's FOOT is on one line, because a counter
        // where each object stands at its own height is not a counter; and both step
        // cards' TOP edges land on one line, so the eye finds the checklist in the same
        // place on both phases of the build.

        /// <summary>Where a prop's foot touches the bench, in surface-centre coordinates
        /// (screen y 585 — low in the band, clear of the bottom text stack).</summary>
        // -245, not -225 (2026-09-14, the author: "sahnedeki shaker şişe kaşık vs. gibi nesneleri biraz daha
        // aşağı alabiliriz"); the strip, the readout and the bottle's and lid's rests came down with it.
        private const float BenchFootY = -245f;
        /// <summary>How far over the surface's top the hand may lift a vessel, and how far over a target's
        /// drawn top its mouth must be before it leans (PourHand.Press, 2026-09-14).</summary>
        private const float HandAbove = 20f, ClearOverRim = 6f;

        // ── THE CATCH (2026-09-14, the author: "Artık şişe her yerde dökülebilecek. Bardak/shaker ekranın neredeyse en
        // aşağısında dökülen sıvıyı otomatik yakalayacak asla kaçırmayacak ve hareket esnasında sallanacak. şişeyi kaldırma
        // aralığını genişletip büyütebiliriz böylece") ────────────────────────────────────────────────────────────────
        /// <summary>The foot of the vessel that catches — the open tin, the serving glass — at the bottom of the
        /// screen (surface-local; the rect's bottom). The lift room over it is what grew.</summary>
        private const float CatchFootY = -340f;
        /// <summary>The glass stands a little higher than the tin: its sheet has fewer empty rows under its foot.</summary>
        private const float GlassFootLift = 16f;
        /// <summary>A vessel is pouring from this lean on (level, less the lean's own spring).</summary>
        private const float PourFromTilt = 85f;
        // 18, not 9 (2026-09-14, second pass): at 9 the tin was a quarter of a second behind a pour that moved, and the
        // drops already falling came down beside it — the author's screenshot of a stream spilling past the tin.
        private const float CatchOmega = 18f, CatchZeta = 0.9f;                 // follows the liquid, no overshoot
        /// <summary>The fastest the liquid's own speed may lead the catcher (units a second): a target that jumps — the
        /// pour starting, a stream ending — is a spring's job, not a lead's.</summary>
        private const float CatchLeadMax = 1600f;
        private const float SwayOmega = 11f, SwayZeta = 0.25f;                   // rocks, and wobbles when it stops
        // Measured in play at 0.0005 per px/s² alone: the tin following a pour rocked 1.3 degrees at most, which does not
        // read as a sway. The lean now takes the speed as well as the push, and the push counts for more.
        // Smaller since the follow got stiffer (2026-09-14, second pass): the same gains on a spring twice as quick rocked the
        // tin past fifteen degrees in the author's screenshot, and a tin leaning that far is a mouth moved off the stream.
        private const float SwayPerAccel = 0.0003f, SwayPerSpeed = 0.0015f, MaxSway = 6f;

        /// <summary>One frame of a catcher: its x follows <paramref name="target"/> on a spring, and it rocks on
        /// its foot with the push — an underdamped lean off its low-passed sideways acceleration, so it wobbles
        /// as it stops. Positive sway leans the top left (Unity's counter-clockwise).</summary>
        private static void StepCatch(ref float x, ref float v, ref float ax, ref float sway, ref float swayV,
            ref float lastTarget, float target, float dt)
        {
            float was = v;
            // LED BY THE LIQUID'S OWN SPEED (2026-09-14, second pass): a plain spring trails a target moving at a steady
            // pace by 2ζ/ω of it — at 18 a tenth of a second, a hundred units behind a pour swept at a thousand a second,
            // wider than the tin's mouth. Damping toward the target's velocity instead of toward rest takes that lag out.
            float targetV = Mathf.Clamp((target - lastTarget) / dt, -CatchLeadMax, CatchLeadMax);
            lastTarget = target;
            v += (CatchOmega * CatchOmega * (target - x) + 2f * CatchZeta * CatchOmega * (targetV - v)) * dt;
            x += v * dt;
            ax = Mathf.Lerp(ax, (v - was) / dt, 1f - Mathf.Exp(-14f * dt));
            float want = Mathf.Clamp(ax * SwayPerAccel + v * SwayPerSpeed, -MaxSway, MaxSway);
            swayV += (SwayOmega * SwayOmega * (want - sway) - 2f * SwayZeta * SwayOmega * swayV) * dt;
            sway += swayV * dt;
            if (Motion.Reduced) { sway = 0f; swayV = 0f; }
        }

        // ── THE CATCH, HEARD (2026-09-15, the author: "Bardağın hareketine ve suyun yakından ya da uzaktan düşmesine göre
        // değişen sese ihtiyacımız var") ─────────────────────────────────────────────────────────────────────────────
        /// <summary>A stream this far above what it lands on (surface units) sounds like one poured right into the drink,
        /// and <see cref="FallFar"/> like one dropped from the top of the lift. Measured in play (2026-09-15): a pouring
        /// bottle's mouth stands at 202 to 208 over the bench whatever the lift, because it leans further as it rises, so
        /// the tin's drop runs from 510 empty to 290 full; the tin's mouth over a glass climbs from 155 to 247 with the
        /// lift, and a full highball takes a drop of 160, an empty rocks glass 500.</summary>
        private const float FallNear = 150f, FallFar = 520f;
        /// <summary>The catcher's speed (units a second) and its rocking (degrees a second) that sound flat out.</summary>
        private const float MotionFullSpeed = 900f, MotionFullRock = 45f;
        /// <summary>How far to one side a sound at the bench's edge sits: the bench is in front of the player, not beside them.</summary>
        private const float PanWidth = 0.55f;

        /// <summary>How far a stream drops for its sound: 0 its mouth at the drink .. 1 the top of the lift.</summary>
        private static float FallOf(float mouthY, float landY) =>
            Mathf.Clamp01((mouthY - landY - FallNear) / (FallFar - FallNear));

        /// <summary>How much a catching vessel is moving for its held sound, 0..1: its speed along the bench and its
        /// rocking, and the drink in it — an empty glass slid is only its foot on the wood.</summary>
        private static float CatchMotion(float v, float swayV, double fill) =>
            Mathf.Clamp01(Mathf.Abs(v) / MotionFullSpeed + Mathf.Abs(swayV) / MotionFullRock)
            * (0.3f + 0.7f * Mathf.Clamp01((float)fill));

        /// <summary>Where x sits across a stage surface for a sound, left edge -1 .. right edge 1, narrowed by
        /// <see cref="PanWidth"/>.</summary>
        private static float PanOf(float x, RectTransform surface) =>
            surface == null || surface.rect.width < 1f ? 0f
            : Mathf.Clamp(x / (surface.rect.width * 0.5f), -1f, 1f) * PanWidth;

        /// <summary>The step cards' shared TOP line: bottom-anchored y for a card of the
        /// given height, so a four-row card and a two-row card start at the same edge.</summary>
        private static float CardSeat(float height) => 422f - height;

        private void AddEdgeBack(RectTransform panel, Stage back = Stage.Closed,
            string caption = null)   // null: bench.back_to_bar
        {
            // BIGGER, AND A PLATE OF THE BENCH'S OWN (2026-09-16, the author: "bara dön butonu çok daha büyük ve
            // farklı bir tasarımda olmalı"): a 216x64 recess cut into the counter's front at the bench's left foot,
            // brass-rimmed like the instruments (the plaque, the dial, the column), a chevron at 2x and the word
            // in the 16 px face. The spoon's towel starts at 240, the plate ends at 232.
            var rt = NewRect("EdgeBack", panel);
            Place(rt, new Vector2(0f, 0f), new Vector2(BackKeyW, BackKeyH), new Vector2(16f, 18f));
            RegisterFixed(panel, rt);    // ...and so is the way out
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = CounterFinish.Recess();
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => GoTo(back));
            BrassRim(rt);
            var face = NewRect("Face", rt);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var sink = rt.gameObject.AddComponent<PressSink>();
            sink.Face = face; sink.Depth = 3f; sink.Lift = 2f; sink.Squash = 0f; sink.Bloom = 0f;
            var mark = NewRect("Mark", face);
            Place(mark, new Vector2(0f, 0.5f), new Vector2(32, 32), new Vector2(14f, 0f));
            mark.pivot = new Vector2(0f, 0.5f);
            var mi = mark.gameObject.AddComponent<Image>();
            mi.sprite = ChromeArt.Mark("chevron_left");
            mi.color = CounterFinish.Current.Accent;
            mi.raycastTarget = false;
            var label = NewText("L", face, _body, 16, TextAnchor.MiddleLeft, UITheme.TextPrimary);
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(BackKeyW - 62f, 20f), new Vector2(54f, 0f));
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            // the chevron is drawn, so the line's own arrow (kept in the string for the old key) comes off
            label.text = (caption ?? UIText.T("bench.back_to_bar")).TrimStart('◀', ' ');
            Engraved(label);
        }

        private const float BackKeyW = 216f, BackKeyH = 64f;

        /// <summary>The brass line the bench's instruments wear two units inside their edge.</summary>
        private void BrassRim(RectTransform plate)
        {
            var tone = CounterFinish.Current.Rim;   // brass in walnut, teal in pine, pink in neon (2026-09-16)
            var rim = new Color(tone.r, tone.g, tone.b, 0.85f);
            foreach (var (name, min, max, offMin, offMax) in new[] {
                ("RimT", new Vector2(0, 1), new Vector2(1, 1), new Vector2(2, -4), new Vector2(-2, -2)),
                ("RimB", new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 2), new Vector2(-2, 4)),
                ("RimL", new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 2), new Vector2(4, -2)),
                ("RimR", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-4, 2), new Vector2(-2, -2)) })
            {
                var r = NewRect(name, plate);
                Stretch(r, min, max, offMin, offMax);
                var ri = r.gameObject.AddComponent<Image>();
                ri.color = rim; ri.raycastTarget = false;
            }
        }

        private void OpenBottle(IngredientCard card)
        {
            _focusBottle = card;
            Sfx.Play("bottle_open", 0.8f);
            // GARNISH CANNOT REACH HERE ANY MORE (2026-08-22), the way beer already could
            // not. The one door into this method is the cellar, and the cellar is stocked by
            // the same filter the wall used — no garnish, no beer. A pinch of mint is taken
            // at the GLASS, where it is dropped in, and that is the only place it is offered.
            // The old branch poured it and redrew the wall; both the wall and the branch are
            // gone rather than left as a route nobody can walk.
            // Beer cannot reach here at all any more (2026-08-15): it left the wall with the
            // kegs, and the only door onto the draught station is the font in the room
            // (OpenTap). OnTheBackBar keeps it off the shelves, so there is no beer branch to
            // take — a route that cannot be reached is worse than no route, because the next
            // reader believes it.
            // EVERYTHING ELSE GOES IN THE TIN (2026-08-14, the author: "tüm içecekler
            // shakera koyulacak"). The fizz used to have its own door onto the counter,
            // which gave the bar two places to build a drink and left the tin holding half
            // of one; Core lets carbonated into the tin now, so the wall has one answer for
            // every bottle. The tin is where a drink is built, whatever the recipe's method
            // turns out to be.
            //
            // THE LID DECIDES WHERE THE WALL SENDS YOU ("shaker açıksa shakera koyma
            // menüsüne yönlendirecek, shaker kapağı kapandıktan sonra bardağa koyma
            // menüsüne"). An open tin always takes the bottle — that is what an open tin
            // is for. A closed one is a finished drink, so the wall reads the pick as "on
            // with it" and moves to the counter — unless the bench is still owed the
            // method the recipe asks for, in which case it turns you around and says so.
            // The lid comes off again on the bench, which is the way back from a cap
            // closed too early.
            // A BOTTLE GOES WHERE IT CAN BE POURED (2026-09-06, the author: "mesrubatlar
            // icki dokme sahnesinde gozukmuyorlar ve bundan dolayi shakera koyulmuyorlar").
            //
            // The lid rule above sent a bottle picked over a CLOSED tin to the counter —
            // written when that bench had a cabinet of mixers to pour from. It has not had
            // one since (a shelf on a bench has been built and cut twice, CLAUDE.md), so
            // the bottle arrived in a room with nowhere to stand: invisible, unpourable,
            // and silently dropped. A bottle is carried to the TIN — that is the one place
            // a drink is built — and when the lid is on, the bench says so, because the
            // lid comes off there and nowhere else.
            if (!Capped) { GoTo(Stage.Shaker); return; }
            if (BenchUnfinished(Run)) { DemandBench(BenchOwed(Run)); return; }
            DemandBench(UIText.T("bench.take_lid_off_to_add"));
        }


        // ── colour helper ─────────────────────────────────────────────────────────

        /// <summary>The drink's colour: its ingredients' true liquid colours, blended by share
        /// in linear space (2026-07-23) — clear spirits read pale, and a mix stays clean.</summary>
        private Color DrinkColor(GlassContents glass) => UITheme.DrinkColor(Run?.Shelf, glass);

        // ── construction ─────────────────────────────────────────────────────────

        private void BuildUi()
        {
            var canvasGo = new GameObject("ServiceFlow", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;   // above the HUD floor (5), below the ID (20)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;   // match height, like the stage (2026-07-22)

            _root = NewRect("FlowRoot", (RectTransform)canvasGo.transform);
            Stretch(_root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // NO DIM (2026-08-22, the author again: "Karıştırma ve dökme sahnelerinde arka
            // plan karartılıyor bu olmayacak"). THIS is what was doing it — Night[0] at 0.86
            // across the whole screen — and it was never on the panels, which is why taking
            // the panels' own backdrops off did not stop it. It dimmed the room because the
            // flow used to be a modal over a room you had left; the benches open onto the bar
            // now and the bar keeps its light.
            //
            // The plate stays and still catches the pointer: it is the floor under a stage
            // that has stopped sliding, and without it a click between panels reaches the room.
            var scrim = _root.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0f);
            // CLICKING OFF THE BENCH GOES BACK TO THE BAR (2026-09-13, the author: "ekran dışına
            // tıklandığında direkt ana sahneye dönülmeli"). The room above the counter is the
            // outside: a click there that lands on nothing of the bench's own closes the flow —
            // with its fade — and shuts the cellar, the way SERVE IT leaves the room. The
            // letterbox counts as outside too. See OnBackgroundClick.
            var scrimBtn = _root.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(OnBackgroundClick);

            // The veil over the room, under everything the bench draws (see _roomDim).
            var dimRt = NewRect("RoomDim", _root);
            Stretch(dimRt, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -HudTopBarH));
            _roomDim = dimRt.gameObject.AddComponent<Image>();
            _roomDim.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, RoomDimBench);
            _roomDim.raycastTarget = false;

            // The scrim keeps the whole screen — a dimmed room with undimmed corners is
            // not dimmed. The STAGES go in a fixed field instead, so the back bar packs
            // its shelves to one width forever rather than to the window's (DesignFrame).
            _field = DesignFrame.Wrap(_root, new Vector2(1280f, 720f));
            // The slide pushes whole panels through the field, and DesignFrame does not
            // clip — without the mask a departing stage would draw on in the letterbox.
            _field.gameObject.AddComponent<RectMask2D>();
            // One switch for every pointer under the flow: off while the field is moving.
            _rootGroup = _root.gameObject.AddComponent<CanvasGroup>();

            BuildBenchStage();
            BuildShakerPanel();
            BuildServePanel();
            BuildTapPanel();

            _root.gameObject.SetActive(false);
        }


        // ── the bench's own room (2026-08-26) ────────────────────────────────────
        //
        // The author, in one note: "bardağa koyma sahnesiyle shakera koyma sahnesi aynı
        // sahne olacak, arkaplan değişmeyecek tezgahın üstündekiler değişecek ... shaker
        // sahnesinden bardak sahnesine geçerken arkaplan sabit kalacak sadece nesneler
        // kayacak ... tezgahı doldurmamız lazım çok boş duruyor, tezgah için bir arkaplan
        // üret."
        //
        // Three faults, one cause. The benches had no BACKGROUND: each panel drew a band of
        // counter across its own bottom third and left the top two thirds transparent, so
        // the ROOM showed through — which is why the screen read empty, and why moving from
        // the tin to the glass slid the whole world sideways instead of just the props.
        //
        // The room is one object and it belongs to the FIELD, not to a panel. The panels
        // keep only what a bench actually differs by — what is standing on the counter —
        // and those are the only things that slide.
        //
        // THE WALL LASTED ONE BUILD (2026-08-26, the author: "arkadaki bu planı kaldıralım
        // müşteriler gözüksün"). A generated back wall was hung over the room here for one
        // round, and it answered "the bench looks empty" by boarding the bar up: the room —
        // the window, the lamps, the DRINKERS the night is about — sat fully drawn behind a
        // painting of panelling. What the stage owns now is only the bar top; above the
        // counter line the live room shows through, exactly as it always has on the draught
        // bench, and the crowd you are pouring for stays in view while you pour.
        private RectTransform _benchStage;

        private void BuildBenchStage()
        {
            _benchStage = NewRect("BenchStage", _field);
            Stretch(_benchStage, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _benchStage.SetAsFirstSibling();

            // The bar top, ONCE. It used to be built three times, one per bench, which is
            // what made a stage change move it.
            AddBenchCounter(_benchStage, 0.675f);
            BuildBenchDressing();
        }

        // ── tiny UI helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// THE BIN KEY: a bin mark and the word ÇÖP on a cap that sinks into its socket.
        ///
        /// This has now been three things. It was a light grey button PLATE with a
        /// clipart wastebasket on it — the one piece of clipart in the game, and a plate
        /// that contradicted this method's own comment ("just the bin, no plate"). Then a
        /// round arcade dome, which the author sent back for being too small and having
        /// nowhere to put a word: "boyut olarak daha büyük ve dikdörtgen bir buton olsun
        /// '(çöp ikonu) Çöp' yazsın üstünde."
        ///
        /// What survived all three is the PRESS. The cap stands proud of a dark socket
        /// and drops onto its floor, its cast shadow going as it lands — a key that only
        /// changed colour would read as a light coming on, not as something moving. The
        /// mark and the word ride ON the cap and travel with it, so the whole face moves
        /// as one object.
        /// </summary>
        private void AddBinButton(RectTransform parent)
        {
            // A REAL BIN (2026-09-16, the author: "çöp için ise arkaplanda gerçek bir çöp kutusu ile
            // tasarlanabilir"): the pedal bin ChromeArt draws, standing at the counter's right end at 2x, its lid
            // lifting under the pointer; pressing it throws the drink away. The word is engraved over it, in the
            // small face. The red key it replaces (2026-09-04's cap) is gone with its face and travel.
            var rt = NewRect("Bin", parent);
            Place(rt, new Vector2(1, 0), new Vector2(64, 96), new Vector2(-16f, 18f));
            RegisterFixed(parent, rt);   // the bin is the same object in the same place on both benches (registered AFTER it is placed: Home is read then)
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.Bin(false);
            img.color = Color.white;
            img.raycastTarget = true;
            img.alphaHitTestMinimumThreshold = 0.1f;      // the drawing takes the press, not its box
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            var relay = rt.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () => img.sprite = ChromeArt.Bin(true);
            relay.Exited = () => img.sprite = ChromeArt.Bin(false);
            var sink = rt.gameObject.AddComponent<PressSink>();
            sink.Face = rt; sink.Depth = 3f; sink.Lift = 2f; sink.Squash = 0f; sink.Bloom = 0f;
            var word = NewText("BinWord", parent, _body, 8, TextAnchor.MiddleCenter, UITheme.TextSecondary);
            Place(word.rectTransform, new Vector2(1, 0), new Vector2(96, 12), new Vector2(0f, 120f));
            RegisterFixed(parent, word.rectTransform);
            word.text = UIText.T("bench.bin");
            word.raycastTarget = false;
            Engraved(word);

            btn.onClick.AddListener(() =>
            {
                int fee = Run.DiscardGlass();
                Sfx.Play("bin_drop", 0.85f);   // the drink goes in the bin, and is heard going (2026-09-15)
                // Redraw whatever bench it is standing on: the tin and the glass both have
                // to come back empty, and which one is on screen is the stage's business.
                if (_stage == Stage.Shaker) RefreshShaker();
                else if (_stage == Stage.Serve) RefreshServe();
                GetComponent<TycoonHud>()?.Toast(fee > 0
                    ? UIText.T("bench.binned_fee", ("fee", "$" + fee))
                    : UIText.T("bench.binned"));
            });

        }

        /// <summary>
        /// Makes a thing answer the pointer before it is clicked: it lifts and warms on hover and
        /// sinks on press (<see cref="PressSink"/>). Every clickable object goes through here, so
        /// "can I click this?" is answered the same way by a button, a bottle and a tub of ice —
        /// the notes' complaint was that the screen only responded once it was too late to ask.
        /// <paramref name="hit"/> catches the pointer; <paramref name="face"/> is what moves, and
        /// defaults to the same object when the whole thing should move.
        /// </summary>
        private static PressSink Pressable(RectTransform hit, RectTransform face = null,
            Graphic tint = null, float lift = 3f, float depth = 4f)
        {
            var sink = hit.gameObject.GetComponent<PressSink>() ?? hit.gameObject.AddComponent<PressSink>();
            sink.Face = face != null ? face : hit;
            sink.Lift = lift;
            sink.Depth = depth;
            sink.Squash = 0.015f;
            sink.Tint = tint;
            return sink;
        }

        private Button AddFlexButton(RectTransform parent, string label, Color fill, Action onClick)
        {
            var rt = NewRect(label, parent);
            var button = rt.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());

            // THE ONE KEY (GDD 16 §2). This loaded a `plate` sprite out of Resources with a
            // pressed twin — the SECOND of four button dialects, and the reason the bench and
            // the market never looked like the same game. The drawn key replaces it: it is
            // grey by construction and takes its colour here, so one drawing serves every
            // state instead of two sprites serving one.
            var face = NewRect("Content", rt);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(rt, fill, button, face);

            var text = NewText("Label", face, _body, 16, TextAnchor.MiddleCenter, Color.black);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(0, KeyPlate.Throw), new Vector2(0, -4));
            text.text = label;
            return button;
        }

        /// <summary>The panel's own floor: it catches every press nothing on the bench took, and
        /// a click on it above the counter is a click off the bench (2026-09-13).</summary>
        private void Swallow(RectTransform panel)
        {
            var block = panel.gameObject.GetComponent<Image>();
            if (block != null) block.raycastTarget = true;
            var btn = panel.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OnBackgroundClick);
        }

        /// <summary>
        /// A click that reached the floor under the bench. Above the counter it is a click on
        /// the ROOM, and the room is where it takes you: the flow closes (and fades), the cellar
        /// shuts. On the counter itself it does nothing — the counter is where the work lies,
        /// and a stray click beside the tin must not throw the drink's bench away. Nothing held
        /// counts: a hand carrying a bottle over the room is pouring, not leaving.
        /// </summary>
        private void OnBackgroundClick()
        {
            if (_stage == Stage.Closed || InTransit) return;
            if (_bottleGrabbed || _serveGrabbed || _capGrabbed || _spoonHeld || _glassHeld || _shaking) return;
            var mouse = Mouse.current;
            if (mouse == null || !AboveTheCounter(mouse.position.ReadValue())) return;
            LeaveForTheRoom();
        }

        private bool AboveTheCounter(Vector2 screen)
        {
            RectTransform band = null;
            foreach (var b in _benchCounters) if (b != null) { band = b; break; }
            if (band == null) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(band, screen, null, out Vector2 local))
                return true;
            return local.y > band.rect.yMax;
        }

        /// <summary>Straight back to the bar: the bench closes with its fade and the cellar
        /// rolls shut, so the room is the room again. The tin or the glass left standing on the
        /// counter is the door back in, as it always was.</summary>
        private void LeaveForTheRoom()
        {
            GoTo(Stage.Closed);
            GetComponent<TycoonHud>()?.Room?.SetDrawerOpen(false);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private Text NewText(string name, Transform parent, Font font, int size,
            TextAnchor anchor, Color color)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = LanguageFonts.Size(font, size); text.alignment = anchor; text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// Carries a point round a pivot. Every vessel here holds its liquid in a cavity whose
        /// centre is NOT the vessel's pivot, and the two rotate as one object — so wherever a
        /// leaning vessel's interior is measured, it has to be swung about the pivot the sprite
        /// actually turns on, or the drink slides out of the glass as it tips (2026-07-28).
        /// </summary>
        private static Vector2 RotateAbout(Vector2 point, Vector2 pivot, float rad)
        {
            var d = point - pivot;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return pivot + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }
    }
}
