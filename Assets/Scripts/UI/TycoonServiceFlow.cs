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
        private RectTransform _menuPanel;
        // Windows open rather than snap (2026-07-24): tapping a bottle on the clipboard plays
        // the menu out and the pour window in, so the two stages are visibly linked.
        private CanvasGroup _stageGroup;   // the window currently easing in
        private RectTransform _stageRect;
        private float _stageT;
        private const float StageOpen = 0.22f;
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
        private float _capT;                  // 0 = open on the bench, 1 = capped and centred
        private Vector2 _capRest, _capPos;
        private Vector2 _shakerOpenSize;
        private readonly List<CanvasGroup> _benchProps = new List<CanvasGroup>();
        private const float CapCentreX = 0f;
        private const float CapGrowth = 1.3f;
        private const float CapArtOffset = 0.245f;   // the lid art sits this far above its rect centre
        private const float TinW = 168f, TinAspect = 116f / 208f;
        private const float CavityFloor = 0.0913f, CavityRim = 0.6106f;
        private const float GridGap = 6f;
        private Text _menuTitle;
        private Vector2 _listHome;
        // The board draws one art pixel as ~5.8 screen pixels. Halving the key's pixels-per-unit
        // puts its grain at 4, so the keys read as the same piece of pixel art as the sheet they
        // sit on rather than a finer sticker laid over it (2026-07-27).
        private const float PlatePixelScale = 0.5f;
        private Vector2 _menuHome;
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

        public void CloseFlow() => GoTo(Stage.Closed);

        /// <summary>Every stage change kills the held-action sound: a loop belongs to the
        /// stage that started it, and a closed stage must not keep pouring in the dark.</summary>
        private void StopHeldSounds() => Sfx.HoldLoop(null);

        private void Update()
        {
            AdvanceStageOpen();

            var run = Run;
            if (run == null) return;

            if (_stage != Stage.Closed && run.Phase != TycoonPhase.DayOpen)
            {
                GoTo(Stage.Closed);
                return;
            }

            // The shake moves the tin, so the lid is placed AFTER it — placing it first left the
            // cap a frame behind the body, which is why they did not read as one object. The
            // drink is placed last for the same reason: it belongs inside the tin, so it can
            // only be positioned once the tin has finished moving (2026-07-28).
            if (_stage == Stage.Shaker)
            {
                _shakerLoopWanted = null;
                _saidThisFrame = false;   // the readout is unclaimed until something claims it
                UpdateShake(run); UpdatePrepDrag(run); UpdateTiltPour(run); UpdateCap(run);
                StepShakerFluid(run);
                Sfx.HoldLoop(_shakerLoopWanted, _shakerLoopWanted == "shake_loop" ? 0.9f : 0.8f);
            }

            if (_stage == Stage.Serve)
            {
                _servePouringNow = false;
                UpdateServeTilt(run); UpdateServePrepDrag(run); UpdateServeCabinet(run);
                // One loop source, driven once per frame from whatever poured (P17): the tin
                // and the hand bottle set the flag, and neither can stop the other's sound.
                Sfx.HoldLoop(_servePouringNow ? "pour_loop" : null, 0.7f);
            }

            if (_stage == Stage.Tap) UpdateTap(run);
        }

        // ── stage transitions ────────────────────────────────────────────────────

        private void GoTo(Stage stage)
        {
            _stage = stage;
            StopHeldSounds();
            Sfx.Play("click", 0.6f);
            _bottleGrabbed = false;
            _pouring = false;
            _serveGrabbed = false;
            _draggingPrep = null;
            _servePrep = null;
            _serveBottleGrabbed = false;
            _serveFocusBottle = null;
            _shaking = false;
            _shakeEnergy = 0;
            if (_dragPiece != null) _dragPiece.gameObject.SetActive(false);
            _shakerSplash?.Clear();
            _serveSplash?.Clear();
            _shakerFluid?.Clear();
            _serveFluid?.Clear();
            _shakerSolids?.Clear();
            if (Run != null && Run.PouringId != null) Run.EndPour();

            _root.gameObject.SetActive(stage != Stage.Closed);
            _menuPanel.gameObject.SetActive(stage == Stage.Menu);

            _shakerPanel.gameObject.SetActive(stage == Stage.Shaker);
            _servePanel.gameObject.SetActive(stage == Stage.Serve);
            _tapPanel.gameObject.SetActive(stage == Stage.Tap);
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

            // Play the window that just opened.
            _stageRect = stage == Stage.Menu ? _menuPanel
                       : stage == Stage.Shaker ? _shakerPanel
                       : stage == Stage.Serve ? _servePanel
                       : stage == Stage.Tap ? _tapPanel : null;
            if (_stageRect != null)
            {
                // Unity's GetComponent returns a fake-null, which ?? happily hands back — check it.
                var grp = _stageRect.GetComponent<CanvasGroup>();
                if (grp == null) grp = _stageRect.gameObject.AddComponent<CanvasGroup>();
                _stageGroup = grp;
                _stageT = 0f;
                _stageGroup.alpha = 0f;
            }
            else _stageGroup = null;
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
            // Beer never enters the shaker (GDD 21 §10): the keg opens the tap instead, and the
            // glass it fills is the one that goes out.
            if (card.Type == IngredientType.Beer) { GoTo(Stage.Tap); return; }
            // Fizz never enters it either (GDD 21 §12): its only door is the serving glass.
            // Routing it to the shaker anyway put a bottle in the hand that Core refuses
            // every frame — the author's bug report: "cola, volt enerji dökülmüyor". It goes
            // to the counter instead, already in hand, ready to tip over the glass.
            if (card.Info != null && card.Info.Carbonated)
            {
                GoTo(Stage.Serve);
                TakeFromCabinet(card.Id);
                return;
            }
            GoTo(Stage.Shaker);
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
            var img = rt.gameObject.AddComponent<Image>();
            img.color = fill;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());

            // Same key as the shelf, so the sheet carries one set of buttons rather than two.
            var plate = ItemArt.Load("plate");
            var plateDown = ItemArt.Load("plate_down");
            if (plate != null)
            {
                img.sprite = plate; img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = PlatePixelScale;
                if (plateDown != null)
                {
                    button.transition = Selectable.Transition.SpriteSwap;
                    var st = button.spriteState;
                    st.pressedSprite = plateDown; st.selectedSprite = plate;
                    button.spriteState = st;
                }
            }
            var face = NewRect("Content", rt);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var sink = rt.gameObject.AddComponent<PressSink>();
            sink.Face = face; sink.Depth = 4f; sink.Squash = 0.015f;

            var text = NewText("Label", face, _body, 16, TextAnchor.MiddleCenter, Color.black);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 10), new Vector2(0, -4));
            text.text = label;
            return button;
        }

        private RectTransform AddListButton(RectTransform parent, string label, Color colour, Action onClick)
        {
            var rt = NewRect("Row", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = onClick == null ? UITheme.Night[0] : UITheme.Night[3];
            if (onClick != null)
            {
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                button.onClick.AddListener(() => onClick());
                Pressable(rt, tint: img, lift: 2f, depth: 3f);   // a row is thin: a small lift
            }
            var swatch = NewRect("Swatch", rt);
            Place(swatch, new Vector2(0, 0.5f), new Vector2(10, 20), new Vector2(10, 0));
            swatch.gameObject.AddComponent<Image>().color = colour;
            var text = NewText("Label", rt, _body, 13, TextAnchor.MiddleLeft,
                onClick == null ? UITheme.Cream[1] : colour);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(28, 0), new Vector2(-8, 0));
            text.text = label;
            return rt;
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
