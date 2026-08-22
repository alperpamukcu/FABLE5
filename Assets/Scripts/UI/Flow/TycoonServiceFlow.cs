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
        [SerializeField] private Font bodyFont;
        [SerializeField] private Font displayFont;

        private GameBootstrap _bootstrap;
        private TycoonRun Run => _bootstrap != null ? _bootstrap.Tycoon : null;

        private Font _body;
        private Font _display;

        private enum Stage { Closed, Menu, Shaker, Serve, Tap }
        private Stage _stage = Stage.Closed;

        private RectTransform _root;        // the whole modal (scrim + panels)
        private RectTransform _field;       // the fixed 1280x720 field the panels are built in
        private RectTransform _menuPanel;
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
        private const float SlideDist = 1280f;
        private bool InTransit => _slideOutRt != null || _slideInRt != null;
        private CanvasGroup _rootGroup;     // raycasts off while the field is moving
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
        private const float CapGrowth = 1.3f;
        private const float CapArtOffset = 0.245f;   // the lid art sits this far above its rect centre
        private const float TinW = 168f;
        private const float CavityFloor = 0.0913f, CavityRim = 0.6106f;
        private const float GridGap = 6f;
        private Text _menuTitle;
        private Vector2 _listHome;
        // The board draws one art pixel as ~5.8 screen pixels. Halving the key's pixels-per-unit
        // puts its grain at 4, so the keys read as the same piece of pixel art as the sheet they
        // sit on rather than a finer sticker laid over it (2026-07-27).
        private const float PlatePixelScale = 0.5f;
        // 64 units against 32px art puts one art pixel on 4 screen pixels — the same grain as the
        // keys, so the corner controls belong to the same drawing (2026-07-27).
        private const float CornerSize = 64f;


        private void Awake()
        {
            var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _body = bodyFont != null ? bodyFont : legacy;
            _display = displayFont != null ? displayFont : legacy;
            _bootstrap = GetComponent<GameBootstrap>();
            BuildUi();
        }

        public bool IsOpen => _stage != Stage.Closed;

        /// <summary>Opens the menu to build a drink. Ignored between days.</summary>
        public void Open()
        {
            if (Run == null || Run.Phase != TycoonPhase.DayOpen) return;
            GoTo(Stage.Menu);
        }

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

        public void CloseFlow() => GoTo(Stage.Closed);

        /// <summary>Every stage change kills the held-action sound: a loop belongs to the
        /// stage that started it, and a closed stage must not keep pouring in the dark.</summary>
        private void StopHeldSounds() => Sfx.HoldLoop(null);

        private void Update()
        {
            // The slide steps FIRST and unconditionally — the curtain's own law: a visual
            // that gates input must never be starved by an early return.
            StepStageSlide();

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
                StepShakerFluid(run);
                Sfx.HoldLoop(_shakerLoopWanted, _shakerLoopWanted == "shake_loop" ? 0.9f : 0.8f);
            }

            if (_stage == Stage.Serve)
            {
                _servePouringNow = false;
                UpdateServeTilt(run); UpdateServePrepDrag(run);
                UpdateServeStepCard(run); PushServeDone(run);
                // One loop source, driven once per frame from whatever poured (P17): the tin
                // and the hand bottle set the flag, and neither can stop the other's sound.
                Sfx.HoldLoop(_servePouringNow ? "pour_loop" : null, 0.7f);
            }

            if (_stage == Stage.Tap) UpdateTap(run);
        }

        // ── stage transitions ────────────────────────────────────────────────────

        /// <summary>The panel a stage lives on; null for Closed.</summary>
        private RectTransform PanelOf(Stage stage) =>
            stage == Stage.Menu ? _menuPanel
            : stage == Stage.Shaker ? _shakerPanel
            : stage == Stage.Serve ? _servePanel
            : stage == Stage.Tap ? _tapPanel : null;

        private void GoTo(Stage stage)
        {
            var previous = _stage;
            _stage = stage;
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
            _pouring = false;
            _serveGrabbed = false;
            _servePrep = null;
            _shaking = false;
            _shakeEnergy = 0;
            _spoonHeld = false;
            _stirEnergy = 0;
            _shakerFluid?.Clear();
            _serveFluid?.Clear();
            _shakerSolids?.Clear();
            if (Run != null && Run.PouringId != null) Run.EndPour();

            _root.gameObject.SetActive(stage != Stage.Closed);
            // A sliding stage keeps its OUTGOING panel alive for the transit; the slide's
            // settle turns it off. Everything else applies exactly as it always has.
            _menuPanel.gameObject.SetActive(stage == Stage.Menu || (slide && previous == Stage.Menu));
            _shakerPanel.gameObject.SetActive(stage == Stage.Shaker || (slide && previous == Stage.Shaker));
            _servePanel.gameObject.SetActive(stage == Stage.Serve || (slide && previous == Stage.Serve));
            _tapPanel.gameObject.SetActive(stage == Stage.Tap || (slide && previous == Stage.Tap));
            _glassHeld = false;
            _glassTilt = 0f;
            if (_tapGlass != null)
            {
                _tapGlass.anchoredPosition = _tapGlassRest;
                _tapGlass.localRotation = Quaternion.identity;
            }
            _tapFluid?.Clear();
            if (Run != null && Run.PullingId != null) Run.EndPull();

            if (stage == Stage.Menu) { ResetPageSlide(); RefreshMenu(); }
            if (stage == Stage.Shaker) RefreshShaker();
            if (stage == Stage.Serve) RefreshServe();
            if (stage == Stage.Tap) RefreshTap();

            // The visuals, last — the state above is already true whatever these draw.
            if (slide)
            {
                // Forward reads left-to-right: the hub (Menu) hands off to a station, and
                // the bench hands the capped tin ON to the glass. Everything else is the
                // way back.
                bool forward = previous == Stage.Menu
                            || (previous == Stage.Shaker && stage == Stage.Serve);
                PlayStageSlide(PanelOf(previous), PanelOf(stage), forward ? 1f : -1f);
            }
            else if (fade)
                PlayStageFade(PanelOf(stage));
        }

        // ── the two-slot stage slide ────────────────────────────────────────────

        private CanvasGroup GroupOn(RectTransform rt)
        {
            // Unity's GetComponent returns a fake-null, which ?? happily hands back — check it.
            var grp = rt.GetComponent<CanvasGroup>();
            if (grp == null) grp = rt.gameObject.AddComponent<CanvasGroup>();
            return grp;
        }

        private void PlayStageSlide(RectTransform outRt, RectTransform inRt, float dir)
        {
            _slideOutRt = outRt;
            _slideInRt = inRt;
            _slideInGroup = GroupOn(inRt);
            _slideInGroup.alpha = 1f;
            _slideDir = dir;
            _slideFade = false;
            _transT = 0f;
            _transDur = SlideDur;
            inRt.anchoredPosition = new Vector2(dir * SlideDist, 0f);
            if (_rootGroup != null) _rootGroup.blocksRaycasts = false;
        }

        /// <summary>Closed→Menu: the flow OPENS rather than arrives, so the first panel
        /// fades up in place instead of shoving in from a direction that means nothing.</summary>
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
            if (_rootGroup != null) _rootGroup.blocksRaycasts = false;
        }

        /// <summary>Everything home, the outgoing panel off, the pointer back on. Called
        /// before any new movement and at the end of every transit — an interrupted slide
        /// can never become a panel's new resting place.</summary>
        private void SettleStageSlide()
        {
            if (_slideOutRt != null)
            {
                _slideOutRt.anchoredPosition = Vector2.zero;
                if (_slideOutRt != PanelOf(_stage))
                    _slideOutRt.gameObject.SetActive(false);
            }
            if (_slideInRt != null)
            {
                _slideInRt.anchoredPosition = Vector2.zero;
                if (_slideInGroup != null) _slideInGroup.alpha = 1f;
            }
            _slideOutRt = null;
            _slideInRt = null;
            _slideInGroup = null;
            if (_rootGroup != null) _rootGroup.blocksRaycasts = true;
        }

        private void StepStageSlide()
        {
            if (!InTransit) return;
            _transT += Time.unscaledDeltaTime;
            float k = _transDur <= 0f ? 1f : Mathf.Clamp01(_transT / _transDur);
            if (k >= 1f) { SettleStageSlide(); return; }
            if (_slideFade)
            {
                if (_slideInGroup != null) _slideInGroup.alpha = k * k * (3f - 2f * k);
                return;
            }
            float e = Tweening.OutCubic(k);
            if (_slideOutRt != null)
                _slideOutRt.anchoredPosition = new Vector2(-_slideDir * SlideDist * e, 0f);
            if (_slideInRt != null)
                _slideInRt.anchoredPosition = new Vector2(_slideDir * SlideDist * (1f - e), 0f);
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
        private void AddEdgeBack(RectTransform panel, Stage back = Stage.Menu,
            string caption = "BACK\nTO\nBAR")
        {
            var rt = NewRect("EdgeBack", panel);
            Place(rt, new Vector2(0f, 0.5f), new Vector2(76, 150), new Vector2(14, 0));
            var btn = rt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => GoTo(back));
            var face = NewRect("Face", rt);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(rt, UITheme.Night[3], btn, face);      // THE ONE KEY (GDD 16 §2)
            // 8, not 12 — the size that face actually has (GDD 16 §0).
            var label = NewText("L", face, _body, 8, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, 4 + KeyPlate.Throw), new Vector2(-4, -4));
            label.text = caption;
        }

        private void OpenBottle(IngredientCard card)
        {
            _focusBottle = card;
            Sfx.Play("bottle_open", 0.8f);
            // Garnishes are a pinch, not a stream — no focus stage needed.
            if (card.Type == IngredientType.Garnish)
            {
                Run.PourGarnish(card.Id);
                RefreshMenu();
                return;
            }
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
            if (!Capped) { GoTo(Stage.Shaker); return; }
            if (BenchUnfinished(Run)) { DemandBench(BenchOwed(Run).ToUpperInvariant()); return; }
            GoTo(Stage.Serve);
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
            var scrim = _root.gameObject.AddComponent<Image>();
            scrim.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.86f);
            // Clicking the dim outside a panel backs out of the flow.
            var scrimBtn = _root.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(CloseFlow);

            // The scrim keeps the whole screen — a dimmed room with undimmed corners is
            // not dimmed. The STAGES go in a fixed field instead, so the back bar packs
            // its shelves to one width forever rather than to the window's (DesignFrame).
            _field = DesignFrame.Wrap(_root, new Vector2(1280f, 720f));
            // The slide pushes whole panels through the field, and DesignFrame does not
            // clip — without the mask a departing stage would draw on in the letterbox.
            _field.gameObject.AddComponent<RectMask2D>();
            // One switch for every pointer under the flow: off while the field is moving.
            _rootGroup = _root.gameObject.AddComponent<CanvasGroup>();

            BuildMenuPanel();
            BuildShakerPanel();
            BuildServePanel();
            BuildTapPanel();

            _root.gameObject.SetActive(false);
        }


        // ── tiny UI helpers ──────────────────────────────────────────────────────

        /// <summary>EMPTY, drawn as a waste bin rather than a word.</summary>
        private void AddBinButton(RectTransform parent)
        {
            // Just the bin — you click the object, not a button plate around it.
            var rt = NewRect("Bin", parent);
            // On the ledge, right of SERVE (2026-08-01) — it STANDS somewhere now.
            Place(rt, new Vector2(1, 0), new Vector2(CornerSize, CornerSize), new Vector2(-140f, 22f));
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.sprite = ItemArt.Load("btn_bin");
            img.color = img.sprite != null ? Color.white : UITheme.Night[3];
            img.alphaHitTestMinimumThreshold = img.sprite != null ? 0.35f : 0f;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                int fee = Run.DiscardGlass();
                RefreshMenu();
                if (fee > 0) _menuTitle.text = $"BINNED · -${fee}";
            });
            GiveKeyPress(rt, btn, img, "btn_bin_down");
            if (img.sprite == null)
            {
                var fallback = NewText("L", rt, _body, 12, TextAnchor.MiddleCenter, UITheme.TextPrimary);
                Stretch(fallback.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                fallback.text = "EMPTY";
            }
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

        /// <summary>Stops a panel's own clicks from falling through to the scrim's close.</summary>
        private static void Swallow(RectTransform panel)
        {
            var block = panel.gameObject.GetComponent<Image>();
            if (block != null) block.raycastTarget = true;
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
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
            text.font = font; text.fontSize = size; text.alignment = anchor; text.color = color;
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
