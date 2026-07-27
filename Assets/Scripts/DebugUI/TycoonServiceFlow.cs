using System;
using System.Collections.Generic;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.DebugUI
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
    public sealed class TycoonServiceFlow : MonoBehaviour
    {
        [SerializeField] private Font bodyFont;
        [SerializeField] private Font displayFont;

        private GameBootstrap _bootstrap;
        private TycoonRun Run => _bootstrap != null ? _bootstrap.Tycoon : null;

        private Font _body;
        private Font _display;

        private enum Stage { Closed, Menu, Shaker, Serve }
        private Stage _stage = Stage.Closed;

        private RectTransform _root;        // the whole modal (scrim + panels)
        private RectTransform _menuPanel;
        private RectTransform _menuSide;   // readouts + buttons, off the board
        // Windows open rather than snap (2026-07-24): tapping a bottle on the clipboard plays
        // the menu out and the pour window in, so the two stages are visibly linked.
        private CanvasGroup _stageGroup;   // the window currently easing in
        private RectTransform _stageRect;
        private float _stageT;
        private const float StageOpen = 0.22f;
        private RectTransform _shakerPanel;
        private RectTransform _servePanel;

        private RectTransform _bottleList;
        private Text _menuShaker;           // "what's in the shaker" readout
        private Text _menuPreps;

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
        // The clipboard, and the share of it its paper covers (measured off the art).
        private const float BoardW = 1148f, BoardH = 719f;
        private const float BoardX = 0f;   // the board fills the screen, centred
        private const float TinW = 168f, TinAspect = 116f / 208f;
        private const float CavityFloor = 0.0913f, CavityRim = 0.6106f;
        // Measured off the dark-walnut board art: the sheet's share of the canvas and where
        // its centre sits, so the list lands on paper and never on the wood or the clip.
        private const float PaperW = 0.655f, PaperH = 0.660f;
        private const float PaperCX = -0.015f, PaperCY = -0.008f;
        private const int MenuColumns = 3;
        private const float GridGap = 6f, HeadingH = 18f;
        private IngredientType? _menuTab;   // null = the section index page
        private Text _menuTitle;
        private RectTransform _menuBack;
        // Turning a page (2026-07-24): the sheet is taken by its bottom-right and folded over
        // going in, and lifted back the other way coming out.
        private IngredientType? _flipTo;
        private float _flipT = 1f;
        private int _flipDir;
        // The board draws one art pixel as ~5.8 screen pixels. Halving the key's pixels-per-unit
        // puts its grain at 4, so the keys read as the same piece of pixel art as the sheet they
        // sit on rather than a finer sticker laid over it (2026-07-27).
        private const float PlatePixelScale = 0.5f;
        private const float FlipTime = 0.34f;
        private Vector2 _menuHome;
        private const float CornerSize = 52f, CornerInset = 30f;   // identical for every corner
        private RectTransform _mixBar;

        private RectTransform _pourBottle;    // the grabbable bottle
        private Image _pourBottleBody;
        private MetaballFluid _shakerFluid;   // the metaball liquid: pour stream + pooled body
        private Splasher _shakerSplash;       // brief splashes (dissolving salt / sugar)
        private ShakerSolids _shakerSolids;   // ice / lemon afloat inside the shaker
        private float _shakerLiquidFloorY;    // pool bottom y (the empty liquid line)
        private float _slosh;                 // running slosh phase for the shaker surface
        private Vector2 _bottleRest;
        private bool _bottleGrabbed;
        private bool _pouring;
        private const float LiftRange = 200f;  // px of lift for a full tilt
        private const float MaxTilt = 118f;    // degrees the bottle leans at full lift
        private const float BottleH = 180f;
        // The pour fills slower than the raw bottle rate so the stream reads as a real pour
        // (GDD 24 §2, 2026-07-22 — "doluş hızı çok hızlı"). Only the drawn volume slows; the
        // floor's patience clock runs on its own tick, untouched.
        private const float PourTimeScale = 0.45f;

        // Drag-drop preparations (GDD 24 §2.4): pick a piece off its tray and drop it into
        // the shaker's mouth. The grip springs after the cursor with overshoot (weighty, lively
        // lag) and the piece hangs and swings from that grip as a pendulum.
        private PreparationDefinition _draggingPrep;
        private RectTransform _dragPiece;
        private Text _dragPieceLabel;
        private readonly Pendulum _dragSwing = new Pendulum();
        private Vector2 _dragPos;    // the grip's current position (lags the cursor)
        private Vector2 _dragVel;    // the grip's velocity (drives the spring and the swing)
        private const float DragStiffness = 150f;  // how hard the grip is pulled to the cursor
        private const float DragDamping = 9f;       // < critical -> it overshoots and jiggles

        // The shake (GDD 24 §2.5, 2026-07-22): grab the shaker itself and throw it around —
        // it springs after the cursor with overshoot (loose and lively), the liquid sloshes,
        // and how far the cursor travels builds the shake energy.
        private bool _shaking;
        private double _shakeEnergy;
        private Vector2 _lastShakeMouse;
        private Vector2 _shakerVel;      // the shaker's spring velocity while thrown about
        private Vector2 _shakerHome;     // its rest position
        private Image _shakeMeterFill;
        private Text _shakeMeterText;
        private const float ShakeFullTravel = 4000f;   // px of cursor travel for a full shake
        private const float ShakeStiffness = 105f;      // loose follow -> it whips around
        private const float ShakeDamping = 6f;

        // The serve pour uses the same tilt model (GDD 24 §3): grab the shaker, tip it over
        // the glass. How well the mouth lines up over the glass is the aim — off-centre spills.
        private Text _serveShakerText;
        private Text _serveGlassText;
        private RectTransform _serveSurface;
        private RectTransform _serveGlass;      // the target
        private RectTransform _serveGarnishRow; // mint/olive garnishes are added here (2026-07-23)
        private RectTransform _serveShaker;     // the grabbable shaker
        private Image _serveShakerBody;
        private MetaballFluid _serveFluid;      // the metaball liquid in the serving glass
        private Splasher _serveSplash;
        private Text _aimText;
        private Vector2 _serveShakerRest;
        private bool _serveGrabbed;
        private const float ServePourRate = 0.34f;   // glass-fractions per second (slower, 2026-07-22)

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

        private void Update()
        {
            AdvanceStageOpen();
            AdvancePageTurn();

            var run = Run;
            if (run == null) return;

            if (_stage != Stage.Closed && run.Phase != TycoonPhase.DayOpen)
            {
                GoTo(Stage.Closed);
                return;
            }

            // The shake moves the tin, so the lid is placed AFTER it — placing it first left the
            // cap a frame behind the body, which is why they did not read as one object.
            if (_stage == Stage.Shaker) { UpdateShake(run); UpdatePrepDrag(run); UpdateTiltPour(run); UpdateCap(run); }

            if (_stage == Stage.Serve) UpdateServeTilt(run);
        }

        // ── stage transitions ────────────────────────────────────────────────────

        private void GoTo(Stage stage)
        {
            _stage = stage;
            _bottleGrabbed = false;
            _pouring = false;
            _serveGrabbed = false;
            _draggingPrep = null;
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
            if (_menuSide != null) _menuSide.gameObject.SetActive(stage == Stage.Menu);

            _shakerPanel.gameObject.SetActive(stage == Stage.Shaker);
            _servePanel.gameObject.SetActive(stage == Stage.Serve);

            if (stage == Stage.Menu) { _menuTab = null; _flipT = 1f; RefreshMenu(); }
            if (stage == Stage.Shaker) RefreshShaker();
            if (stage == Stage.Serve) RefreshServe();

            // Play the window that just opened.
            _stageRect = stage == Stage.Menu ? _menuPanel
                       : stage == Stage.Shaker ? _shakerPanel
                       : stage == Stage.Serve ? _servePanel : null;
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
            // Garnishes are a pinch, not a stream — no focus stage needed.
            if (card.Type == IngredientType.Garnish)
            {
                Run.PourGarnish(card.Id);
                RefreshMenu();
                return;
            }
            GoTo(Stage.Shaker);
        }

        // ── the menu ─────────────────────────────────────────────────────────────

        /// <summary>Starts a page turn to <paramref name="tab"/> (null = back to the index).</summary>
        private void OpenTab(IngredientType? tab)
        {
            if (_flipT < 1f) return;                 // already turning
            _flipTo = tab;
            _flipDir = tab != null ? 1 : -1;         // in = folded away, out = lifted back
            _flipT = 0f;
        }

        /// <summary>Drives the turn. The SHEET is what moves — the board lifts, tips and settles
        /// back — while the tabs keep their layout untouched; animating them was what threw their
        /// positions off. The page is swapped under the lift.</summary>
        private void AdvancePageTurn()
        {
            if (_flipT >= 1f || _menuPanel == null) return;
            bool wasFirstHalf = _flipT < 0.5f;
            _flipT = Mathf.MoveTowards(_flipT, 1f, Mathf.Max(Time.deltaTime, 1e-4f) / FlipTime);

            if (wasFirstHalf && _flipT >= 0.5f)
            {
                _menuTab = _flipTo;                  // the page changes while the sheet is up
                RefreshMenu();
            }

            // 1 → 0 → 1: the sheet lifts away and the next one settles in.
            float half = _flipT < 0.5f ? _flipT / 0.5f : (1f - _flipT) / 0.5f;
            float lift = Mathf.Max(0f, 1f - (1f - half) * (1f - half));
            float k = Mathf.Lerp(1f, 0.90f, lift);
            _menuPanel.localScale = new Vector3(k, k, 1f);
            _menuPanel.localRotation = Quaternion.Euler(0, 0, lift * -3.5f * _flipDir);
            _menuPanel.anchoredPosition = _menuHome + new Vector2(26f * _flipDir, 34f) * lift;

            if (_flipT >= 1f)
            {
                _menuPanel.localScale = Vector3.one;
                _menuPanel.localRotation = Quaternion.identity;
                _menuPanel.anchoredPosition = _menuHome;
            }
        }

        private static int CountStockedGroups(TycoonRun run)
        {
            int n = 0;
            foreach (var type in MenuOrder)
            {
                if (type == IngredientType.Garnish) continue;
                foreach (var b in run.Shelf.Bottles)
                    if (b.Ingredient.Type == type) { n++; break; }
            }
            return n;
        }

        /// <summary>Everything written on the sheet is slanted — the closest a pixel face gets
        /// to a hand that scrawled the list out behind the bar.</summary>
        private static Text Handwritten(Text t)
        {
            // The pixel faces have no true italic, so Unity fakes it by shearing the glyphs —
            // which read as broken rather than hand-written. Upright it is.
            t.fontStyle = FontStyle.Normal;
            return t;
        }

        /// <summary>Gives a corner control the same press as the section keys: it swaps to its
        /// pressed art and dips as it goes down.</summary>
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
            sink.Face = rt; sink.Depth = 3f; sink.Squash = 0.02f;
        }

        /// <summary>Rings a label in black so it stays legible on any coloured key. The ring is one
        /// font-pixel wide and closes on all eight sides — see <see cref="PixelOutline"/>.</summary>
        private static Text Outlined(Text t, float thickness = 2f)
        {
            var o = t.gameObject.AddComponent<PixelOutline>();
            o.EffectColor = new Color(0f, 0f, 0f, 1f);
            o.Distance = thickness;
            return t;
        }

        /// <summary>A point inset from one of the paper's corners — every corner control uses
        /// this, so they are geometrically symmetric rather than eyeballed.</summary>
        private static Vector2 PaperCorner(int sx, int sy) => new Vector2(
            BoardW * (PaperCX + sx * PaperW * 0.5f) - sx * CornerInset,
            BoardH * (PaperCY + sy * PaperH * 0.5f) - sy * CornerInset);

        /// <summary>
        /// What is in the tin, as a single bar across the top of the sheet: one segment per
        /// poured ingredient, in its own colour, carrying its share. No vessel, just the mix.
        /// </summary>
        private void BuildMixBar()
        {
            _mixBar = NewRect("MixBar", _menuPanel);
            var barHost = NewRect("MixBarFrame", _menuPanel);
            Place(barHost, new Vector2(0.5f, 0.5f), new Vector2(BoardW * PaperW - 30f, 74f),
                new Vector2(BoardW * PaperCX, BoardH * (PaperCY + PaperH * 0.5f) - 96f));
            var frame = barHost.gameObject.AddComponent<Image>();
            var barSprite = ItemArt.Load("plate");
            if (barSprite != null)
            {
                // The same plate, tinted dark and sunk — a track the mix sits in.
                frame.sprite = barSprite; frame.type = Image.Type.Sliced;
                frame.color = new Color(0.26f, 0.22f, 0.19f);
            }
            else frame.color = new Color(0.30f, 0.24f, 0.16f, 0.16f);
            frame.raycastTarget = false;

            // Say what the gauge is, so a row of coloured chips is not a mystery.
            var caption = Handwritten(NewText("Caption", barHost, _body, 11, TextAnchor.MiddleLeft,
                new Color(0.30f, 0.21f, 0.12f)));
            Place(caption.rectTransform, new Vector2(0, 0.5f), new Vector2(180, 18), new Vector2(6, 46));
            caption.text = "WHAT'S IN THE TIN";

            // The segments live in the frame's recessed channel.
            _mixBar = NewRect("MixBar", barHost);
            Stretch(_mixBar, Vector2.zero, Vector2.one, new Vector2(14, 10), new Vector2(-14, -10));


        }

        private void RefreshMixBar()
        {
            if (_mixBar == null) return;   // the gauge is shelved for now
            foreach (Transform child in _mixBar) Destroy(child.gameObject);

            var run = Run;
            var glass = run.Glass;
            float w = _mixBar.rect.width;
            float x = 0f;

            // Each poured ingredient in ITS GROUP'S colour, then what is still empty — the bar
            // is the whole tin, so you read how much room is left, not just the ratios.
            foreach (var id in glass.Ingredients)
            {
                var card = run.Shelf.Find(id)?.Ingredient;
                float share = (float)glass.RatioOf(id) * (float)glass.FillFraction;
                var col = UITheme.TypeRamp[card?.Type ?? IngredientType.Spirit][3];
                x += AddMixSegment(id, share * w, new Color(col.r, col.g, col.b, 0.92f),
                    $"{share:P0}".Replace(" %", "%"), new Color(0.12f, 0.10f, 0.08f), x);
            }

            float free = Mathf.Max(0f, 1f - (float)glass.FillFraction);
            if (free > 0.001f)
                AddMixSegment("empty", free * w, new Color(0.42f, 0.35f, 0.26f, 0.16f),
                    glass.IsEmpty ? "EMPTY" : $"{free:P0} EMPTY".Replace(" %", "%"),
                    new Color(0.44f, 0.37f, 0.28f), x);
        }

        private float AddMixSegment(string id, float width, Color fill, string label, Color ink, float x)
        {
            var seg = NewRect($"Seg_{id}", _mixBar);
            seg.anchorMin = new Vector2(0, 0); seg.anchorMax = new Vector2(0, 1);
            seg.pivot = new Vector2(0, 0.5f);
            seg.offsetMin = new Vector2(0, 2); seg.offsetMax = new Vector2(0, -2);
            seg.sizeDelta = new Vector2(width, -4);
            seg.anchoredPosition = new Vector2(x, 0);
            var img = seg.gameObject.AddComponent<Image>();
            var chip = ItemArt.Load("plate");
            if (chip != null) { img.sprite = chip; img.type = Image.Type.Sliced; }
            img.color = fill; img.raycastTarget = false;

            var text = NewText("Pct", seg, _body, 11, TextAnchor.MiddleCenter, ink);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = width > 26f ? label : "";
            return width;
        }

        /// <summary>
        /// The menu is paged (2026-07-24). The first page is the shelf's sections — SPIRITS,
        /// BITTERS and so on — and choosing one opens that section's page, where the bottles
        /// are listed with what they cost. That keeps the sheet readable however big the bar
        /// gets, and gives prices somewhere to live.
        /// </summary>
        private void RefreshMenu()
        {
            var run = Run;
            foreach (Transform child in _bottleList) Destroy(child.gameObject);
            if (_menuBack != null) _menuBack.gameObject.SetActive(_menuTab != null);
            if (_menuTab == null) BuildGroupPage(run); else BuildTabPage(run, _menuTab.Value);

        }

        /// <summary>Page one: one card per stocked section.</summary>
        private void BuildGroupPage(TycoonRun run)
        {
            _menuTitle.text = "DRINKS";
            var row = NewRect("Groups", _bottleList);
            var grid = row.gameObject.AddComponent<GridLayoutGroup>();
            float areaW = _bottleList.rect.width, areaH = _bottleList.rect.height;
            grid.spacing = new Vector2(14, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            // Flows for however many groups the bar ends up carrying: three across, and the
            // rows follow from the count.
            int cols = 3;
            // A 3x3 board: five groups today (spirits, bitters, sweet, sour, mixers) with room
            // for whatever the bar grows into, and the cells stay the same size either way.
            const int gRows = 3;
            grid.constraintCount = cols;
            grid.cellSize = new Vector2((areaW - (cols - 1) * 14f) / cols,
                Mathf.Min(150f, (areaH - (gRows - 1) * 14f) / gRows));
            grid.childAlignment = TextAnchor.MiddleCenter;

            foreach (var type in MenuOrder)
            {
                if (type == IngredientType.Garnish) continue;
                int have = 0, empty = 0;
                foreach (var b in run.Shelf.Bottles)
                    if (b.Ingredient.Type == type) { have++; if (b.IsEmpty) empty++; }
                if (have == 0) continue;

                var card = NewRect($"Grp_{type}", row);
                var bg = card.gameObject.AddComponent<Image>();
                var col = UITheme.TypeRamp[type][3];
                var plate = ItemArt.Load("plate");
                var plateDown = ItemArt.Load("plate_down");
                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                if (plate != null)
                {
                    // One white 3D plate, tinted with the group's colour — so a new group is
                    // just a new colour, never a new sprite.
                    bg.sprite = plate; bg.type = Image.Type.Sliced;
                    bg.pixelsPerUnitMultiplier = PlatePixelScale;
                    bg.color = col;
                    if (plateDown != null)
                    {
                        btn.transition = Selectable.Transition.SpriteSwap;
                        var st = btn.spriteState;
                        st.pressedSprite = plateDown; st.selectedSprite = plate;
                        btn.spriteState = st;
                    }
                    else
                    {
                        btn.transition = Selectable.Transition.ColorTint;
                        var cb = btn.colors;
                        cb.normalColor = Color.white;
                        cb.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
                        cb.fadeDuration = 0.05f;
                        btn.colors = cb;
                    }
                }
                else bg.color = new Color(col.r, col.g, col.b, 0.20f);
                var t = type;
                btn.onClick.AddListener(() => OpenTab(t));

                // Everything printed on the plate lives here, so it sinks WITH the press —
                // swapping only the background left the label and bottles floating.
                var content = NewRect("Content", card);
                Stretch(content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var sink = card.gameObject.AddComponent<PressSink>();
                sink.Face = content;
                sink.Depth = 4f; sink.Squash = 0.015f;

                var name = Handwritten(NewText("N", content, _display, 16, TextAnchor.MiddleCenter, Color.black));
                Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(grid.cellSize.x - 24, 22), new Vector2(0, -10));
                name.text = GroupKeyName(t);

                var count = Handwritten(NewText("C", content, _body, 8, TextAnchor.UpperCenter, new Color(0.12f, 0.12f, 0.12f)));
                Place(count.rectTransform, new Vector2(0.5f, 1), new Vector2(grid.cellSize.x - 24, 14),
                    new Vector2(0, -34));
                string unit = have == 1 ? "bottle" : "bottles";
                count.text = empty > 0 ? $"{have} {unit} · {empty} out" : $"{have} {unit}";

                // The bottles themselves, just their art, under the heading.
                var icons = NewRect("Icons", content);
                Place(icons, new Vector2(0.5f, 0), new Vector2(grid.cellSize.x - 20, grid.cellSize.y - 76),
                    new Vector2(0, 26));
                var ig = icons.gameObject.AddComponent<GridLayoutGroup>();
                int iconCols = Mathf.Clamp(have, 1, 4);
                float cell = Mathf.Min(grid.cellSize.y - 78f, (grid.cellSize.x - 28f) / iconCols);
                ig.cellSize = new Vector2(cell, cell);
                ig.spacing = new Vector2(4, 4);
                ig.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                ig.constraintCount = iconCols;
                ig.childAlignment = TextAnchor.MiddleCenter;
                foreach (var b in run.Shelf.Bottles)
                {
                    if (b.Ingredient.Type != t) continue;
                    var slot = NewRect($"I_{b.Ingredient.Id}", icons);
                    var si2 = slot.gameObject.AddComponent<Image>();
                    si2.sprite = ItemArt.Bottle(b.Ingredient.Info?.Style);
                    si2.preserveAspect = true; si2.raycastTarget = false;
                    si2.color = si2.sprite == null
                        ? UITheme.StyleColor(b.Ingredient.Info?.Style, b.Ingredient.Type)
                        : (b.IsEmpty ? new Color(1f, 1f, 1f, 0.35f) : Color.white);
                }
            }
        }

        /// <summary>A section's page: its bottles, with prices, and a way back.</summary>
        private void BuildTabPage(TycoonRun run, IngredientType type)
        {
            _menuTitle.text = GroupName(type);

            var items = new List<ShelfBottle>();
            foreach (var b in run.Shelf.Bottles) if (b.Ingredient.Type == type) items.Add(b);
            float areaW = _bottleList.rect.width, areaH = _bottleList.rect.height;

            // Once the bottles need a second row the page scrolls. Kept deliberately damped:
            // the shelf tracks the wheel and stops with it, rather than sliding on afterwards.
            var scroller = NewRect("Scroll", _bottleList);
            // The list lays its children out vertically and a ScrollRect reports no preferred
            // size, so without this it collapses to 100x100 and the shelf vanishes.
            var scrollFill = scroller.gameObject.AddComponent<LayoutElement>();
            scrollFill.preferredWidth = areaW; scrollFill.preferredHeight = areaH;
            scrollFill.flexibleHeight = 1f;
            var scroll = scroller.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 12f; scroll.inertia = true;
            scroll.decelerationRate = 0.02f;   // barely coasts — the shelf follows the wheel, no glide
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.02f;
            var viewport = NewRect("Viewport", scroller);
            Stretch(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpHit = viewport.gameObject.AddComponent<Image>();
            vpHit.color = new Color(0, 0, 0, 0.001f);
            scroll.viewport = viewport;

            var grid = NewRect("Grid", viewport);
            grid.anchorMin = new Vector2(0, 1); grid.anchorMax = new Vector2(1, 1);
            grid.pivot = new Vector2(0.5f, 1); grid.anchoredPosition = Vector2.zero;
            grid.sizeDelta = Vector2.zero;
            scroll.content = grid;
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            int rows = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)MenuColumns));
            g.spacing = new Vector2(GridGap, GridGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = MenuColumns;
            g.cellSize = new Vector2((areaW - (MenuColumns - 1) * GridGap) / MenuColumns,
                Mathf.Clamp((areaH - (rows - 1) * GridGap) / rows, 140f, 300f));
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var bottle in items) AddItemBox(grid, bottle, run);
        }

        private static readonly IngredientType[] MenuOrder =
        {
            IngredientType.Spirit, IngredientType.Bitter, IngredientType.Sweet,
            IngredientType.Sour, IngredientType.Bubbly, IngredientType.Garnish,
        };

        private static string GroupName(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.Spirit: return "SPIRITS";
                case IngredientType.Bitter: return "BITTERS";
                case IngredientType.Sweet: return "SWEET";
                case IngredientType.Sour: return "SOUR / CITRUS";
                case IngredientType.Bubbly: return "MIXERS";
                default: return "GARNISHES";
            }
        }

        /// <summary>The name as it fits on a key — one word, so the heading never wraps onto the
        /// bottle count underneath it. The section page still uses the full name.</summary>
        private static string GroupKeyName(IngredientType type)
            => type == IngredientType.Sour ? "CITRUS" : GroupName(type);

        private void AddGroupHeader(RectTransform parent, string title, Color colour)
        {
            var rt = NewRect("Header", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = HeadingH;
            var text = NewText("L", rt, _body, 12, TextAnchor.LowerLeft,
                Color.Lerp(colour, new Color(0.24f, 0.16f, 0.09f), 0.62f));
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 0), new Vector2(-2, 0));
            text.text = $"— {title} —";
            var line = NewRect("Rule", rt);
            line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0);
            line.offsetMin = new Vector2(0, 0); line.offsetMax = new Vector2(0, 2);
            var img = line.gameObject.AddComponent<Image>();
            img.color = new Color(colour.r, colour.g, colour.b, 0.4f);
            img.raycastTarget = false;
        }

        /// <summary>
        /// One bottle on a section page — the same key as the section tabs, tinted by its group
        /// and only as big as its name, how full it is and what it costs.
        /// </summary>
        private void AddItemBox(RectTransform parent, ShelfBottle bottle, TycoonRun run)
        {
            var card = bottle.Ingredient;
            bool empty = bottle.IsEmpty;
            var col = UITheme.TypeRamp[card.Type][3];

            var box = NewRect($"Box_{card.Id}", parent);
            var bg = box.gameObject.AddComponent<Image>();
            var plate = ItemArt.Load("plate");
            var plateDown = ItemArt.Load("plate_down");
            if (plate != null)
            {
                bg.sprite = plate; bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = PlatePixelScale;
                bg.color = empty ? Color.Lerp(col, new Color(0.45f, 0.43f, 0.42f), 0.7f) : col;
            }
            else bg.color = new Color(col.r, col.g, col.b, empty ? 0.25f : 0.5f);

            var content = NewRect("Content", box);
            Stretch(content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            if (!empty)
            {
                var btn = box.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                if (plateDown != null)
                {
                    btn.transition = Selectable.Transition.SpriteSwap;
                    var st = btn.spriteState;
                    st.pressedSprite = plateDown; st.selectedSprite = plate;
                    btn.spriteState = st;
                }
                var sink = box.gameObject.AddComponent<PressSink>();
                sink.Face = content; sink.Depth = 4f; sink.Squash = 0.015f;
                var c = card;
                btn.onClick.AddListener(() => OpenBottle(c));
            }

            // The key's contents follow the grid cell, so changing the column count moves the
            // bottle, the name and the price together instead of leaving them at an old width.
            var g = parent.GetComponent<GridLayoutGroup>();
            float cw = g != null ? g.cellSize.x : 172f;
            float chh = g != null ? g.cellSize.y : 226f;

            // The bottle is the thing you are choosing, so it gets most of the key.
            var icon = NewRect("Icon", content);
            Place(icon, new Vector2(0.5f, 1), new Vector2(cw - 42f, chh - 100f), new Vector2(0, -4));
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.raycastTarget = false; iconImg.preserveAspect = true;
            iconImg.sprite = ItemArt.Bottle(card.Info?.Style);
            iconImg.color = iconImg.sprite == null ? UITheme.StyleColor(card.Info?.Style, card.Type)
                : (empty ? new Color(1f, 1f, 1f, 0.4f) : Color.white);

            // Name, then how full it is and what it costs — the three things the key is sized for.
            // Pixel faces only rasterise cleanly at whole multiples of their 8px design size, so
            // the labels are pinned to 16 and best-fit is off — it used to pick sizes like 11,
            // which lands the stems on half pixels and makes the letters look chewed (2026-07-27).
            var name = Outlined(Handwritten(NewText("Name", content, _body, 16, TextAnchor.LowerCenter, Color.white)));
            Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(cw + 6f, 36), new Vector2(0, 54));
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.text = card.Name.ToUpperInvariant();

            // How full it is, and what it costs — each in its own colour, both ringed in black.
            double fill = bottle.Capacity > 0 ? bottle.Remaining / bottle.Capacity : 0;
            var pct = Outlined(Handwritten(NewText("Fill", content, _body, 16, TextAnchor.UpperLeft,
                empty ? new Color(1f, 0.42f, 0.42f) : new Color(1f, 0.80f, 0.32f))));
            Place(pct.rectTransform, new Vector2(0, 0), new Vector2(cw * 0.55f, 20), new Vector2(16, 30));
            pct.text = empty ? "OUT" : $"{(int)System.Math.Round(fill * 100)}%";

            var price = Outlined(Handwritten(NewText("Price", content, _body, 16, TextAnchor.UpperRight,
                new Color(0.45f, 0.95f, 0.45f))));
            Place(price.rectTransform, new Vector2(1, 0), new Vector2(cw * 0.55f, 20), new Vector2(-16, 30));
            price.text = $"${Market.StockPrice(card)}";

            if (!empty && run.IsNewStock(card.Id))
            {
                var badge = Handwritten(NewText("New", content, _body, 8, TextAnchor.UpperRight,
                    new Color(0.62f, 0.36f, 0.04f)));
                Place(badge.rectTransform, new Vector2(1, 1), new Vector2(46, 14), new Vector2(-8, -6));
                badge.text = "NEW";
            }
        }

        private string ShakerLine(TycoonRun run)
        {
            if (run.Glass.IsEmpty) return "shaker empty — tap a bottle";
            var sb = new StringBuilder();
            sb.Append($"SHAKER {run.Glass.FillFraction:P0} — ");
            var parts = new List<string>();
            foreach (var id in run.Glass.Ingredients)
            {
                var card = run.Shelf.Find(id)?.Ingredient;
                parts.Add($"{(card?.Name ?? id).ToUpperInvariant()} {run.Glass.RatioOf(id):P0}");
            }
            sb.Append(string.Join(", ", parts));
            return sb.ToString();
        }

        // ── the shaker focus stage: the tilt-pour ────────────────────────────────

        private void RefreshShaker()
        {
            var run = Run;
            if (_focusBottle == null) return;
            var colour = UITheme.StyleColor(_focusBottle.Info?.Style, _focusBottle.Type);
            _shakerTitle.text = _focusBottle.Name.ToUpperInvariant();
            _shakerReadout.text = ShakerLine(run);
            var bottleSprite = ItemArt.Bottle(_focusBottle.Info?.Style);
            _pourBottleBody.sprite = bottleSprite;
            _pourBottleBody.color = bottleSprite != null ? Color.white : colour;   // real art, else the style tint
            _pourBottle.anchoredPosition = _bottleRest;
            _pourBottle.localRotation = Quaternion.identity;
            _shakerSplash.Clear();
            _shakerFluid.Clear();
            _shakerFluid.SetColor(DrinkColor(run.Glass));
            _shakerVessel.anchoredPosition = _shakerHome;
            _shakerVessel.localRotation = Quaternion.identity;
            _capped = false; _capGrabbed = false; _capT = 0f;
            if (_shakerOpenSize != Vector2.zero) _shakerVessel.sizeDelta = _shakerOpenSize;
            _capPos = _capRest;
            if (_shakerTop != null) { _shakerTop.anchoredPosition = _capRest; _shakerTop.localRotation = Quaternion.identity; }
            foreach (var g in _benchProps) if (g != null) g.alpha = 1f;
            PushShakerPool(run, 0f);
            _shakeMeterFill.rectTransform.sizeDelta = new Vector2(0, -4);
            _shakeMeterText.text = run.Glass.HasPreparation("shaken")
                ? $"SHAKEN · {run.ShakeEnergy:P0}" : "";
        }

        /// <summary>
        /// One frame of the tilt-pour. The bottle follows the mouse while grabbed; the
        /// higher it is lifted the further it leans toward the shaker (GDD 24 §2). Liquid
        /// runs from the mouth only when it is tilted over the shaker's opening.
        /// </summary>
        private void UpdateTiltPour(TycoonRun run)
        {
            if (Mouse.current == null || _focusBottle == null) return;

            // Release when the button comes up, wherever the cursor is.
            if (_bottleGrabbed && !Mouse.current.leftButton.isPressed)
                _bottleGrabbed = false;

            bool pourNow = false;
            if (_bottleGrabbed &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _pourSurface, Mouse.current.position.ReadValue(), null, out Vector2 local))
            {
                // Keep the bottle on the surface.
                float halfW = _pourSurface.rect.width * 0.5f;
                float halfH = _pourSurface.rect.height * 0.5f;
                local.x = Mathf.Clamp(local.x, -halfW + 30f, halfW - 30f);
                local.y = Mathf.Clamp(local.y, -halfH + 20f, halfH - 20f);
                _pourBottle.anchoredPosition = local;

                float lift = Mathf.Clamp01((local.y - _bottleRest.y) / LiftRange);
                float tilt = lift * MaxTilt;                       // degrees, counter-clockwise = leans left
                _pourBottle.localRotation = Quaternion.Euler(0, 0, tilt);

                // Where the mouth ends up: the bottle's top, swung around its grip.
                float rad = tilt * Mathf.Deg2Rad;
                Vector2 mouth = local + new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (BottleH * 0.78f);

                var opening = _shakerVessel.anchoredPosition + new Vector2(0, _shakerVessel.rect.height * 0.5f);
                bool over = Mathf.Abs(mouth.x - opening.x) < 78f && mouth.y > opening.y - 30f;
                pourNow = tilt > 42f && over;

                if (pourNow)
                {
                    // A stream of merging droplets falls from the mouth toward the opening; the
                    // metaball field fuses them into one liquid column and melts them into the
                    // pool where they land (GDD 24 §3.5).
                    var colour = UITheme.StyleColor(_focusBottle.Info?.Style, _focusBottle.Type);
                    _shakerFluid.SetColor(colour);
                    var streamVel = new Vector2((opening.x - mouth.x) * 1.8f, -225f);
                    _shakerFluid.EmitStream(mouth, streamVel, Time.deltaTime);
                }
            }

            if (pourNow)
            {
                if (run.PouringId == null) run.BeginPour(_focusBottle.Id);
                run.PourTick(Time.deltaTime * PourTimeScale);   // slower, deliberate pour
                _shakerReadout.text = ShakerLine(run);
            }
            else if (run.PouringId != null)
            {
                run.EndPour();
            }

            // A gentle vertical heave on the pool top; the height-field carries the real waves.
            float energy = _shaking ? 1f + 3f * (float)_shakeEnergy : (pourNow ? 1.2f : 0.3f);
            _slosh += Time.deltaTime * (4f + 6f * energy);
            float bob = Mathf.Sin(_slosh) * 1.0f * energy;
            PushShakerPool(run, bob);

            _shakerFluid.Step(Time.deltaTime);
            _shakerSolids.Step(Time.deltaTime);
            _shakerSplash.Step(Time.deltaTime);
            _pouring = pourNow;
        }

        /// <summary>Places the shaker's pooled liquid from the glass interior and its live fill,
        /// plus a vertical slosh <paramref name="bob"/> on the surface (all surface-local px).</summary>
        private void PushShakerPool(TycoonRun run, float bob)
        {
            if (run.Glass.IsEmpty) { _shakerFluid.ClearPool(); return; }
            // Read the vessel live so the pool travels with the shaker when it is thrown about.
            // Fill the glass INTERIOR (inset from the walls) so the liquid pools inside the
            // clear shaker instead of a box around it (2026-07-23).
            var c = _shakerVessel.anchoredPosition;
            float halfW = _shakerVessel.rect.width * 0.5f;
            float iw = halfW * 0.50f;   // measured: the tin's cavity is 50% of the sprite width
            float minX = c.x - iw;
            float maxX = c.x + iw;
            float h = _shakerVessel.rect.height;
            float bottomY = c.y - h * 0.5f + h * 0.0913f;  // measured: above the rounded base
            float innerH = h * 0.5192f;                     // measured: that floor → rim
            float fill = (float)run.Glass.FillFraction * (8f / 9f);   // shows a ninth less
            float rimY = bottomY + innerH;
            float topY = bottomY + innerH * fill + bob;
            // The particle fluid collides with the tin's rotated interior, so it sloshes with it.
            float deg = _shakerVessel.localEulerAngles.z;
            if (deg > 180f) deg -= 360f;
            _shakerFluid.SetPool(minX, maxX, bottomY, rimY, fill, deg * Mathf.Deg2Rad);
            // The cap's placement belongs to UpdateCap now — it rests on the bench until
            // you drop it on the tin, so it must not be glued to the vessel here.
            // The solids float on the liquid line and bounce off these same walls.
            _shakerSolids.SetBounds(minX, maxX, bottomY, topY);
        }

        /// <summary>Eases the window that just opened up to full size and opacity — the menu
        /// hands over to the pour window instead of cutting to it.</summary>
        private void AdvanceStageOpen()
        {
            if (_stageGroup == null || _stageRect == null) return;
            _stageT = Mathf.MoveTowards(_stageT, 1f, Mathf.Max(Time.deltaTime, 1e-4f) / StageOpen);
            float e = 1f - (1f - _stageT) * (1f - _stageT);          // ease-out
            _stageGroup.alpha = e;
            float k = Mathf.Lerp(0.94f, 1f, e);
            _stageRect.localScale = new Vector3(k, k, 1f);
            if (_stageT >= 1f)
            {
                _stageRect.localScale = Vector3.one;
                _stageGroup.alpha = 1f;
                _stageGroup = null;
            }
        }

        /// <summary>
        /// The cap (2026-07-24). While the tin is open you build the drink in it; drag the lid
        /// over its mouth and it snaps on. Capping hands the stage over to shaking: the bottle
        /// and the buckets fade away and the tin eases into the middle and grows, so nothing is
        /// left on the bench but the thing you are about to shake.
        /// </summary>
        private void UpdateCap(TycoonRun run)
        {
            if (_shakerTop == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var mouse = Mouse.current;

            if (_capGrabbed)
            {
                // The cap's art lives in the top of its canvas, so centre THAT on the cursor —
                // grabbing it used to pin the mouse to the empty space beneath the lid.
                float lift = _shakerTop.rect.height * CapArtOffset;
                if (mouse != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _pourSurface, mouse.position.ReadValue(), null, out Vector2 local))
                    _capPos = Vector2.Lerp(_capPos, local - new Vector2(0, lift), 1f - Mathf.Exp(-30f * dt));
                if (mouse == null || !mouse.leftButton.isPressed)
                {
                    _capGrabbed = false;
                    // Anywhere over the tin will do — you should not have to thread the mouth.
                    var tin = _shakerVessel;
                    var d = _capPos + new Vector2(0, lift) - tin.anchoredPosition;
                    bool onTin = Mathf.Abs(d.x) < tin.rect.width * 0.75f
                              && Mathf.Abs(d.y) < tin.rect.height * 0.75f;
                    if (onTin && !run.Glass.IsEmpty) _capped = true;
                    else _capPos = _capRest;
                }
            }

            _capT = Mathf.MoveTowards(_capT, _capped ? 1f : 0f, dt / 0.45f);
            float e = _capT * _capT * (3f - 2f * _capT);   // smoothstep

            if (!_shaking)
                _shakerVessel.anchoredPosition = Vector2.Lerp(
                    _shakerVessel.anchoredPosition,
                    Vector2.Lerp(_shakerHome, new Vector2(CapCentreX, _shakerHome.y), e),
                    1f - Mathf.Exp(-9f * dt));
            _shakerVessel.sizeDelta = Vector2.Lerp(_shakerOpenSize, _shakerOpenSize * CapGrowth, e);

            foreach (var g in _benchProps) if (g != null) g.alpha = 1f - e;

            if (_capT > 0f)
            {
                _shakerTop.sizeDelta = _shakerVessel.sizeDelta;
                _shakerTop.anchoredPosition = Vector2.Lerp(_capPos, _shakerVessel.anchoredPosition, e);
                _shakerTop.localRotation = _shakerVessel.localRotation;
            }
            else
            {
                _shakerTop.sizeDelta = _shakerOpenSize;
                _shakerTop.anchoredPosition = _capPos;
            }
            _shakerTop.SetAsLastSibling();
            var capImg = _shakerTop.GetComponent<Image>();
            if (capImg != null) capImg.raycastTarget = !_capped;   // capped: grab the tin, not the lid

            if (!_capped && !run.Glass.IsEmpty && !_capGrabbed)
                _shakerReadout.text = "drag the lid onto the tin to close it";
        }

        /// <summary>
        /// The mouse-energy shake (GDD 24 §2.5): while the pad is held, cursor travel builds
        /// the shake energy and the shaker jitters; releasing applies the shake at whatever
        /// energy was reached.
        /// </summary>
        private void UpdateShake(TycoonRun run)
        {
            if (!_shaking) return;
            var mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
            {
                // Released: commit the shake if there's a drink and any energy behind it.
                if (!run.Glass.IsEmpty && _shakeEnergy > 0.05)
                {
                    run.Shake(_shakeEnergy);
                    _shakerReadout.text = $"SHAKEN · {_shakeEnergy:P0} · {ShakerLine(run)}";
                }
                _shaking = false;
                _shakeEnergy = 0;
                _shakerVessel.localRotation = Quaternion.identity;
                // Leave the shaker wherever it was set down — no teleport home (2026-07-22).
                _shakerVel = Vector2.zero;
                if (_shakeMeterText != null) _shakeMeterText.text = "";
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            // Cursor travel builds the shake energy.
            float travel = (mouse - _lastShakeMouse).magnitude;
            _lastShakeMouse = mouse;
            _shakeEnergy = Mathf.Clamp01((float)_shakeEnergy + travel / ShakeFullTravel);

            // The shaker springs loosely after the cursor and overshoots — throw it around.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _pourSurface, mouse, null, out Vector2 local))
            {
                _shakerVel += (local - _shakerVessel.anchoredPosition) * (ShakeStiffness * dt);
                _shakerVel *= Mathf.Exp(-ShakeDamping * dt);
                _shakerVessel.anchoredPosition += _shakerVel * dt;
                _shakerVessel.localRotation =
                    Quaternion.Euler(0, 0, Mathf.Clamp(-_shakerVel.x * 0.02f, -24f, 24f));

                // The slosh comes from the fluid feeling the tin's acceleration (MetaballFluid
                // reads the vessel's motion itself). The old Disturb/Ripple pokes that used to
                // fake it are gone: they injected a one-way velocity into every particle on
                // every frame, on top of the real inertia, and that compounded — the drink was
                // driven into the wall and packed tighter and tighter until a full tin read as
                // a puddle (measured: 100% -> 35% of its area over 16s of shaking). Ripple was
                // also being handed a surface-space x while it now expects the tin's own frame.
            }

            _shakeMeterFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(200f * (float)_shakeEnergy), -4);
            _shakeMeterFill.color = Color.Lerp(UITheme.Amber[3], UITheme.Lime[3], (float)_shakeEnergy);
            if (_shakeMeterText != null) _shakeMeterText.text = $"SHAKE! {_shakeEnergy:P0}";
        }

        /// <summary>
        /// The prep drag (GDD 24 §2.4): while a piece is held it follows the mouse; dropping
        /// it over the shaker's mouth adds the preparation, a miss just falls away.
        /// </summary>
        private void UpdatePrepDrag(TycoonRun run)
        {
            if (_draggingPrep == null || Mouse.current == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _pourSurface, Mouse.current.position.ReadValue(), null, out Vector2 cursor);

            // The grip springs after the cursor with overshoot — it has weight and jiggle — and
            // the piece hangs from that grip and swings; grab a lemon by one end and the other
            // end lags and sways (GDD 24 §2.4, 2026-07-22).
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            _dragVel += (cursor - _dragPos) * (DragStiffness * dt);
            _dragVel *= Mathf.Exp(-DragDamping * dt);   // frame-rate-independent damping
            _dragPos += _dragVel * dt;
            _dragSwing.Step(dt, _dragVel);
            _dragPiece.anchoredPosition = _dragPos;
            _dragPiece.localRotation = Quaternion.Euler(0, 0, _dragSwing.Angle);

            if (Mouse.current.leftButton.isPressed) return;

            // Dropped. Over the shaker's mouth → it goes in.
            var local = _dragPos;
            var opening = _shakerVessel.anchoredPosition + new Vector2(0, _shakerVessel.rect.height * 0.5f);
            bool inMouth = Mathf.Abs(local.x - opening.x) < 90f && Mathf.Abs(local.y - opening.y) < 90f;
            if (inMouth && !run.Glass.IsEmpty)
            {
                run.AddPreparation(_draggingPrep);
                _shakerReadout.text = ShakerLine(run);
                var c = _dragPiece.GetComponent<Image>().color;
                bool granular = _draggingPrep.Id == "salt_rim" || _draggingPrep.Id == "sugar_rim";
                if (granular)
                {
                    // Salt / sugar: a scatter of fine grains that fall and dissolve on the drink.
                    for (int i = 0; i < 8; i++)
                        _shakerSolids.Add(new Vector2(opening.x + UnityEngine.Random.Range(-16f, 16f), opening.y),
                            c, UnityEngine.Random.Range(6f, 9f));
                }
                else
                {
                    // Ice / lemon: a single piece that falls and dissolves the moment it hits.
                    _shakerSolids.Add(new Vector2(opening.x + UnityEngine.Random.Range(-16f, 16f), opening.y),
                        c, _draggingPrep.Id == "ice" ? 30f : 26f);
                }
                _shakerFluid.Ripple(opening.x, 0.02f);   // the piece ripples the surface as it lands
            }
            _draggingPrep = null;
            _dragPiece.gameObject.SetActive(false);
        }

        // ── the serve stage ──────────────────────────────────────────────────────

        private void RefreshServe()
        {
            var run = Run;
            _serveShakerText.text = run.Glass.IsEmpty
                ? "shaker empty"
                : $"shaker {run.Glass.FillFraction:P0} left";
            _serveGlassText.text = run.ServingGlass.IsEmpty
                ? "glass empty"
                : $"glass {run.ServingGlass.FillFraction:P0} full";
            _serveShaker.anchoredPosition = _serveShakerRest;
            _serveShaker.localRotation = Quaternion.identity;
            _serveSplash.Clear();
            _serveFluid.Clear();
            _serveFluid.SetColor(DrinkColor(run.Glass));
            PushServePool(run);
            _serveShakerBody.color = DrinkColor(run.Glass);
            _aimText.text = "GRAB THE SHAKER · TIP IT OVER THE GLASS";

            // The stocked garnishes (mint, olive), added into the drink before it is poured.
            foreach (Transform ch in _serveGarnishRow) Destroy(ch.gameObject);
            foreach (var bottle in run.Shelf.Bottles)
                if (bottle.Ingredient.Type == IngredientType.Garnish && !bottle.IsEmpty)
                    AddGarnishChip(bottle.Ingredient);
        }

        private void AddGarnishChip(IngredientCard card)
        {
            var chip = NewRect($"G_{card.Id}", _serveGarnishRow);
            chip.gameObject.AddComponent<LayoutElement>().preferredHeight = 66;
            var bg = chip.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.85f);
            var icon = NewRect("Icon", chip);
            Place(icon, new Vector2(0.5f, 1), new Vector2(46, 46), new Vector2(0, -3));
            var iimg = icon.gameObject.AddComponent<Image>();
            iimg.sprite = ItemArt.Bottle(card.Info?.Style); iimg.preserveAspect = true; iimg.raycastTarget = false;
            if (iimg.sprite == null) iimg.color = UITheme.StyleColor(card.Info?.Style, card.Type);
            var name = NewText("N", chip, _body, 8, TextAnchor.LowerCenter, UITheme.TextPrimary);
            Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(92, 14), new Vector2(0, 2));
            name.text = card.Name.ToUpperInvariant();
            var btn = chip.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var c = card;
            btn.onClick.AddListener(() => { if (Run != null && !Run.Glass.IsEmpty) { Run.PourGarnish(c.Id); RefreshServe(); } });
        }

        /// <summary>
        /// One frame of the serve pour (GDD 24 §3): the shaker tips the same way the bottle
        /// did. How well the mouth lines up over the glass is the aim — dead over the glass
        /// pours clean, drifting off spills, and a full pour still drains the shaker.
        /// </summary>
        private void UpdateServeTilt(TycoonRun run)
        {
            if (Mouse.current == null) return;
            if (_serveGrabbed && !Mouse.current.leftButton.isPressed) _serveGrabbed = false;

            bool pourNow = false;
            double accuracy = 0;
            if (_serveGrabbed && !run.Glass.IsEmpty &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _serveSurface, Mouse.current.position.ReadValue(), null, out Vector2 local))
            {
                float halfW = _serveSurface.rect.width * 0.5f;
                float halfH = _serveSurface.rect.height * 0.5f;
                local.x = Mathf.Clamp(local.x, -halfW + 30f, halfW - 30f);
                local.y = Mathf.Clamp(local.y, -halfH + 20f, halfH - 20f);
                _serveShaker.anchoredPosition = local;

                float lift = Mathf.Clamp01((local.y - _serveShakerRest.y) / LiftRange);
                float tilt = lift * MaxTilt;
                _serveShaker.localRotation = Quaternion.Euler(0, 0, tilt);

                float rad = tilt * Mathf.Deg2Rad;
                Vector2 mouth = local + new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (BottleH * 0.78f);
                var opening = _serveGlass.anchoredPosition + new Vector2(0, _serveGlass.rect.height * 0.5f);

                if (tilt > 42f && mouth.y > opening.y - 30f)
                {
                    // Aim: how well the mouth is centred over the glass. Within ~half the
                    // glass width is a clean pour; beyond that it spills more the further off.
                    accuracy = Mathf.Clamp01(1f - Mathf.Abs(mouth.x - opening.x) / 90f);
                    pourNow = true;

                    // The stream falls toward where the aim sends it: dead-on it drops into the
                    // glass and melts into the drink; off-aim it drifts wide and misses the rim,
                    // falling past onto the counter — the spill you can see (GDD 24 §3).
                    var colour = DrinkColor(run.Glass);
                    _serveFluid.SetColor(colour);
                    float landX = Mathf.Lerp(mouth.x + (mouth.x - opening.x) * 1.5f, opening.x, (float)accuracy);
                    var streamVel = new Vector2((landX - mouth.x) * 1.8f, -225f);
                    _serveFluid.EmitStream(mouth, streamVel, Time.deltaTime);
                }
            }

            if (pourNow)
            {
                double before = run.ServingGlass.TotalVolume;
                run.PourIntoServingGlass(ServePourRate * Time.deltaTime, accuracy);
                if (run.Glass.IsEmpty || run.ServingGlass.FillFraction >= 1.0) _serveGrabbed = false;
                if (run.ServingGlass.TotalVolume != before) RefreshServeText(run, accuracy);
            }

            PushServePool(run);
            _serveFluid.Step(Time.deltaTime);
            _serveSplash.Step(Time.deltaTime);
        }

        /// <summary>Places the serving glass's pooled liquid from its interior and live fill.</summary>
        private void PushServePool(TycoonRun run)
        {
            if (run.ServingGlass.IsEmpty) { _serveFluid.ClearPool(); return; }
            // Fill the tumbler's INTERIOR (inset from the crystal walls) so the drink pools
            // inside the glass, not as a box behind it (2026-07-23).
            var c = _serveGlass.anchoredPosition;
            float halfW = _serveGlass.rect.width * 0.5f;
            float iw = halfW * 0.66f;
            float minX = c.x - iw;
            float maxX = c.x + iw;
            float h = _serveGlass.rect.height;
            float bottomY = c.y - h * 0.5f + h * 0.14f;
            float innerH = h * 0.6f;
            float rimY = bottomY + innerH;
            _serveFluid.SetPool(minX, maxX, bottomY, rimY, (float)run.ServingGlass.FillFraction);
        }

        private void RefreshServeText(TycoonRun run, double accuracy)
        {
            _serveShakerText.text = $"shaker {run.Glass.FillFraction:P0} left";
            _serveGlassText.text = $"glass {run.ServingGlass.FillFraction:P0} full";
            _aimText.text = accuracy > 0.8 ? "CLEAN POUR" : accuracy > 0.4 ? "SOME SPILL" : "SPILLING!";
            _aimText.color = Color.Lerp(UITheme.ViceRed[3], UITheme.Lime[3], (float)accuracy);
        }

        // ── colour helper ─────────────────────────────────────────────────────────

        /// <summary>The drink's colour: its ingredients' true liquid colours, blended by share
        /// in linear space (2026-07-23) — clear spirits read pale, and a mix stays clean.</summary>
        private Color DrinkColor(GlassContents glass)
        {
            if (glass == null || glass.IsEmpty) return UITheme.Cream[3];
            var shelf = Run?.Shelf;
            var parts = new List<(string, IngredientType, float)>();
            foreach (var id in glass.Ingredients)
            {
                var card = shelf?.Find(id)?.Ingredient;
                parts.Add((card?.Info?.Style, card?.Type ?? IngredientType.Spirit, (float)glass.RatioOf(id)));
            }
            return UITheme.BlendLiquid(parts, UITheme.Cream[3], 0.9f);
        }

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

            _root.gameObject.SetActive(false);
        }

        private void BuildMenuPanel()
        {
            // The menu is a wooden clipboard with the drink list written on its paper.
            _menuPanel = NewRect("MenuPanel", _root);
            Place(_menuPanel, new Vector2(0.5f, 0.5f), new Vector2(BoardW, BoardH), new Vector2(BoardX, 0));
            _menuHome = _menuPanel.anchoredPosition;
            var boardImg = _menuPanel.gameObject.AddComponent<Image>();
            var board = ItemArt.Load("menu_board");
            if (board != null) { boardImg.sprite = board; boardImg.preserveAspect = true; boardImg.color = Color.white; }
            else boardImg.color = UITheme.Night[1];
            Swallow(_menuPanel);

            // A red X in the board's top-right corner closes the whole flow.
            var close = NewRect("Close", _menuPanel);
            Place(close, new Vector2(0.5f, 0.5f), new Vector2(CornerSize, CornerSize),
                PaperCorner(1, 1) + new Vector2(-22f, 0f));
            var closeImg = close.gameObject.AddComponent<Image>();
            var closeSprite = ItemArt.Load("btn_close");
            if (closeSprite != null) { closeImg.sprite = closeSprite; closeImg.preserveAspect = true; closeImg.color = Color.white; }
            else closeImg.color = new Color(0.62f, 0.15f, 0.17f);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(CloseFlow);
            GiveKeyPress(close, closeBtn, closeImg, "btn_close_down");
            if (closeSprite == null)
            {
                var closeX = NewText("X", close, _display, 18, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.90f));
                Stretch(closeX.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                closeX.text = "X";
            }

            // Its mirror on the paper's top-left: step back out of a section.
            _menuBack = NewRect("Back", _menuPanel);
            Place(_menuBack, new Vector2(0.5f, 0.5f), new Vector2(CornerSize, CornerSize),
                PaperCorner(-1, 1) + new Vector2(22f, 0f));
            var backImg = _menuBack.gameObject.AddComponent<Image>();
            var backSprite = ItemArt.Load("btn_back");
            if (backSprite != null) { backImg.sprite = backSprite; backImg.preserveAspect = true; backImg.color = Color.white; }
            else backImg.color = new Color(0.62f, 0.15f, 0.17f);
            var backBtn = _menuBack.gameObject.AddComponent<Button>();
            backBtn.targetGraphic = backImg;
            backBtn.onClick.AddListener(() => OpenTab(null));
            GiveKeyPress(_menuBack, backBtn, backImg, "btn_back_down");
            if (backSprite == null)
            {
                var backArrow = NewText("A", _menuBack, _display, 20, TextAnchor.MiddleCenter, new Color(0.97f, 0.93f, 0.86f));
                Stretch(backArrow.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                backArrow.text = "←";
            }

            var title = _menuTitle = Handwritten(NewText("Title", _menuPanel, _display, 19, TextAnchor.MiddleCenter, Color.white));
            var outline = title.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.16f, 0.09f, 0.04f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);
            // Kept inside the clip: it wraps and shrinks to fit rather than running past the metal.
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(232, 44),
                new Vector2(BoardW * PaperCX, BoardH * (PaperCY + PaperH * 0.5f) + 2f));
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 9; title.resizeTextMaxSize = 19;
            title.text = "MAKE A DRINK";

            // Left: a SCROLLABLE back-shelf of grouped item boxes — it grows as you buy more
            // stock without overflowing the panel (2026-07-23 fix).
            // One grid on the paper, never a scrollbar: the cell size is recomputed from the
            // stock count in RefreshMenu, so a growing bar packs tighter instead of scrolling.
            _bottleList = NewRect("Bottles", _menuPanel);
            Place(_bottleList, new Vector2(0.5f, 0.5f),
                new Vector2(BoardW * PaperW - 44f, BoardH * PaperH - 112f),
                new Vector2(BoardW * PaperCX, BoardH * PaperCY - 20f));
            var listLayout = _bottleList.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = GridGap; listLayout.childControlHeight = true;
            listLayout.childControlWidth = true; listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childAlignment = TextAnchor.UpperLeft;

            // Right: a side column beside the menu — what's in the shaker, then the actions.
            // The mix/serve buttons live here, out of the item grid, per the redesign.
            // Nothing but the drink list belongs on the paper — the readouts and the buttons
            // sit off the board, under it.
            var side = _menuSide = NewRect("Side", _root);
            Place(side, new Vector2(0.5f, 0.5f), new Vector2(BoardW * PaperW, 54),
                new Vector2(BoardW * PaperCX, BoardH * (PaperCY - PaperH * 0.5f) + 34f));

            // On the sheet itself and centred, so the page animation carries it too.
            var actions = NewRect("Actions", _menuPanel);
            Place(actions, new Vector2(0.5f, 0.5f), new Vector2(212, 40),
                new Vector2(BoardW * PaperCX, BoardH * (PaperCY - PaperH * 0.5f) + 30f));
            var actLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actLayout.childControlWidth = true; actLayout.childForceExpandWidth = true;
            actLayout.childControlHeight = true; actLayout.childForceExpandHeight = true;
            AddFlexButton(actions, "SERVE  →", UITheme.PrimaryAction, () =>
            {
                if (!Run.Glass.IsEmpty) GoTo(Stage.Serve);
            });

            AddBinButton(_menuPanel);
        }

        /// <summary>The side "mix / shake" action: open the shaker stage to shake what's poured;
        /// focuses a stocked spirit so the stage renders even when nothing new is being added.</summary>
        private void OpenShakeStage()
        {
            if (Run == null) return;
            if (_focusBottle == null)
                foreach (var b in Run.Shelf.Bottles)
                    if (!b.IsEmpty && b.Ingredient.Type != IngredientType.Garnish)
                    { _focusBottle = b.Ingredient; break; }
            if (_focusBottle != null) GoTo(Stage.Shaker);
        }

        private void BuildShakerPanel()
        {
            _shakerPanel = NewRect("ShakerPanel", _root);
            Place(_shakerPanel, new Vector2(0.5f, 0.5f), new Vector2(1120, 640), Vector2.zero);
            _shakerPanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_shakerPanel);

            _shakerTitle = NewText("Title", _shakerPanel, _display, 18, TextAnchor.UpperCenter, UITheme.TextPrimary);
            Stretch(_shakerTitle.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -44), new Vector2(0, -10));

            var hint = NewText("Hint", _shakerPanel, _body, 12, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(hint.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -64), new Vector2(0, -46));
            hint.text = "GRAB THE BOTTLE TO POUR  ·  GRAB THE SHAKER TO SHAKE IT";

            // The play surface: bottle and shaker live in here, mouse-local.
            _pourSurface = NewRect("PourSurface", _shakerPanel);
            Stretch(_pourSurface, Vector2.zero, Vector2.one, new Vector2(20, 84), new Vector2(-20, -76));
            var surfImg = _pourSurface.gameObject.AddComponent<Image>();
            surfImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.5f);
            surfImg.raycastTarget = false;

            // The shaker vessel: a tapered tin, opening at the top, left of centre. Grab it to
            // shake — it becomes the toy you throw around.
            _shakerHome = new Vector2(-210, -34);
            _shakerVessel = NewRect("Shaker", _pourSurface);
            Place(_shakerVessel, new Vector2(0.5f, 0.5f), new Vector2(168, 301), _shakerHome);
            var shakerImg = _shakerVessel.gameObject.AddComponent<Image>();
            // The real steel shaker (2026-07-23). It sits in front of the fluid so the metal
            // reads solid — the falling stream shows above the mouth then vanishes into the tin.
            var tinSprite = ItemArt.Load("tin_open") ?? ItemArt.Shaker;
            if (tinSprite != null) { shakerImg.sprite = tinSprite; shakerImg.preserveAspect = true; shakerImg.color = Color.white; }
            else
            {
                shakerImg.color = UITheme.Cream[2];
                var tin = NewRect("Tin", _shakerVessel);
                Stretch(tin, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -22));
                tin.gameObject.AddComponent<Image>().color = UITheme.Night[3];
                var lip = NewRect("Lip", _shakerVessel);
                Place(lip, new Vector2(0.5f, 1), new Vector2(128, 16), new Vector2(0, 0));
                lip.gameObject.AddComponent<Image>().color = UITheme.Cream[3];
            }

            // Grabbing the shaker (once it holds a drink) starts a free, loose shake.
            var shakeGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            shakeGrab.callback.AddListener(_ =>
            {
                if (Run == null || Run.Glass.IsEmpty) { _shakerReadout.text = "pour something to shake"; return; }
                if (!_capped) { _shakerReadout.text = "cap it first — drag the lid onto the tin"; return; }
                _shaking = true;
                _shakeEnergy = Run.ShakeEnergy;   // continue from what's been shaken, don't reset
                _shakerVel = Vector2.zero;
                _lastShakeMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            });
            _shakerVessel.gameObject.AddComponent<EventTrigger>().triggers.Add(shakeGrab);

            // The metaball fluid draws over the vessel (pool); the solids float on top of it;
            // the bottle and prep pieces are created after, so they sit in front of the liquid.
            _shakerFluid = new MetaballFluid(_pourSurface);
            // The tin's silhouette (bottom → rim): a full body that draws in to the neck, so the
            // drink takes the shaker's shape instead of filling an invisible box (2026-07-24).
            _shakerFluid.SetProfile(new[] {
                // The tin's cavity from just above its rounded base up to the rim. The pinched
                // base rows are deliberately left out of the simulated interior — they are a
                // slot barely wider than a particle, which only squeezed the drink and fired it
                // back out; the floor sits above them instead.
                0.690f, 0.707f, 0.724f, 0.741f, 0.759f, 0.776f, 0.793f, 0.810f, 0.828f, 0.828f,
                0.828f, 0.862f, 0.862f, 0.879f, 0.897f, 0.914f, 0.914f, 0.931f, 0.931f, 0.948f,
                0.966f, 0.966f, 0.966f, 0.983f, 0.983f, 1.000f, 1.000f, 1.000f });
            // The tin's rim, dome and cap ride ABOVE the liquid (2026-07-24): the fluid draws
            // over the open body to show the level, but it must never cover the cap.
            _shakerOpenSize = _shakerVessel.sizeDelta;
            _capRest = new Vector2(-350, -150);   // bottom-left of the tin
            _shakerTop = NewRect("ShakerCap", _pourSurface);
            _shakerTop.anchorMin = _shakerTop.anchorMax = _shakerTop.pivot = new Vector2(0.5f, 0.5f);
            _shakerTop.sizeDelta = _shakerOpenSize;
            _capPos = _capRest;
            _shakerTop.anchoredPosition = _capRest;
            var topImg = _shakerTop.gameObject.AddComponent<Image>();
            topImg.sprite = ItemArt.Load("shaker_cap") ?? ItemArt.Load("shaker_top");
            topImg.preserveAspect = true; topImg.raycastTarget = true;
            _benchProps.Clear();

            var capGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            capGrab.callback.AddListener(_ => { if (!_capped) _capGrabbed = true; });
            _shakerTop.gameObject.AddComponent<EventTrigger>().triggers.Add(capGrab);
            _shakerTop.gameObject.SetActive(topImg.sprite != null);

            _shakerSolids = new ShakerSolids(_pourSurface);
            _shakerSplash = new Splasher(_pourSurface);
            // The metal shaker is opaque, so the fluid draws OVER it (2026-07-24): you see the
            // drink inside the tin as a cutaway, which is the point — a metal shaker you can
            // still read the level in. (A clear vessel would sit in front instead.)
            _shakerVessel.SetAsFirstSibling();
            _shakerLiquidFloorY = _shakerVessel.anchoredPosition.y - _shakerVessel.rect.height * 0.5f + 12f;

            // The grabbable bottle, resting lower-right. Procedural body + neck; the grip
            // pivot sits low so lifting swings the mouth in a big arc.
            _bottleRest = new Vector2(300, -70);
            _pourBottle = NewRect("Bottle", _pourSurface);
            _pourBottle.pivot = new Vector2(0.5f, 0.22f);
            _pourBottle.sizeDelta = new Vector2(110, BottleH);
            _pourBottle.anchoredPosition = _bottleRest;
            _pourBottleBody = _pourBottle.gameObject.AddComponent<Image>();
            _pourBottleBody.preserveAspect = true;    // the real bottle art, set per focus in RefreshShaker
            _pourBottleBody.color = UITheme.Cyan[3];
            if (ItemArt.Bottle("vodka") == null)      // no art available → keep a procedural neck
            {
                var neck = NewRect("Neck", _pourBottle);
                Place(neck, new Vector2(0.5f, 1), new Vector2(20, 34), new Vector2(0, 0));
                neck.gameObject.AddComponent<Image>().color = UITheme.Cream[3];
            }
            // Pointer-down anywhere on the bottle grabs it.
            var grab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            grab.callback.AddListener(_ =>
            {
                if (_focusBottle != null && Run != null && Run.Phase == TycoonPhase.DayOpen)
                    _bottleGrabbed = true;
            });
            _pourBottle.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);
            _benchProps.Add(_pourBottle.gameObject.AddComponent<CanvasGroup>());

            // The prep tray, down the left edge: pick a piece up and drag it into the shaker.
            AddPrepSource(0, "ICE", Preparations.Ice, UITheme.Cyan[4]);
            AddPrepSource(1, "LEMON", Preparations.LemonTwist, UITheme.Amber[4]);
            AddPrepSource(2, "SALT", Preparations.SaltRim, UITheme.Cream[4]);
            AddPrepSource(3, "SUGAR", Preparations.SugarRim, UITheme.Magenta[4]);

            // The single piece that follows the mouse while a prep is held. Its pivot is at the
            // top (the grip), so it hangs below the cursor and swings about that point.
            _dragPiece = NewRect("DragPiece", _pourSurface);
            _dragPiece.pivot = new Vector2(0.5f, 1f);
            _dragPiece.sizeDelta = new Vector2(46, 52);
            var dragImg = _dragPiece.gameObject.AddComponent<Image>();
            dragImg.raycastTarget = false; dragImg.preserveAspect = true;   // the real prep piece
            _dragPieceLabel = NewText("L", _dragPiece, _body, 10, TextAnchor.LowerCenter, UITheme.Night[0]);
            Stretch(_dragPieceLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 2), new Vector2(0, -2));
            _dragPiece.gameObject.SetActive(false);

            _shakerReadout = NewText("Readout", _shakerPanel, _body, 13, TextAnchor.LowerCenter, UITheme.TextSecondary);
            Stretch(_shakerReadout.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(16, 92), new Vector2(-16, 118));

            // The shake meter, above the bottom bar.
            var meterBg = NewRect("ShakeMeterBg", _shakerPanel);
            Place(meterBg, new Vector2(0.5f, 0), new Vector2(220, 14), new Vector2(0, 70));
            meterBg.gameObject.AddComponent<Image>().color = UITheme.Night[0];
            var meterFill = NewRect("ShakeMeterFill", meterBg);
            meterFill.anchorMin = new Vector2(0, 0); meterFill.anchorMax = new Vector2(0, 1);
            meterFill.pivot = new Vector2(0, 0.5f); meterFill.offsetMin = new Vector2(2, 2);
            meterFill.offsetMax = new Vector2(2, -2); meterFill.anchoredPosition = new Vector2(2, 0);
            _shakeMeterFill = meterFill.gameObject.AddComponent<Image>();
            _shakeMeterFill.raycastTarget = false;
            _shakeMeterText = NewText("ShakeText", _shakerPanel, _body, 11, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Place(_shakeMeterText.rectTransform, new Vector2(0.5f, 0), new Vector2(240, 16), new Vector2(0, 86));

            // Bottom bar: a hint (the shaker itself is the toy now) and DONE.
            var pad = NewRect("ShakeHint", _shakerPanel);
            Place(pad, new Vector2(0.5f, 0), new Vector2(300, 40), new Vector2(-160, 12));
            pad.gameObject.AddComponent<Image>().color = UITheme.Night[3];
            var padLabel = NewText("Label", pad, _body, 12, TextAnchor.MiddleCenter, UITheme.Cyan[4]);
            Stretch(padLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            padLabel.text = "↔  GRAB THE SHAKER · SHAKE IT";

            var back = NewRect("Back", _shakerPanel);
            Place(back, new Vector2(0.5f, 0), new Vector2(300, 40), new Vector2(160, 12));
            back.gameObject.AddComponent<Image>().color = UITheme.PrimaryAction;
            back.gameObject.AddComponent<Button>().onClick.AddListener(() => GoTo(Stage.Menu));
            var backLabel = NewText("Label", back, _body, 13, TextAnchor.MiddleCenter, UITheme.TextOnAmber);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backLabel.text = "← DONE — BACK TO MENU";
        }

        private void BuildServePanel()
        {
            _servePanel = NewRect("ServePanel", _root);
            Place(_servePanel, new Vector2(0.5f, 0.5f), new Vector2(1120, 640), Vector2.zero);
            _servePanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_servePanel);

            var title = NewText("Title", _servePanel, _display, 16, TextAnchor.UpperCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -40), new Vector2(0, -10));
            title.text = "POUR THE GLASS";

            _serveShakerText = NewText("Shaker", _servePanel, _body, 13, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(_serveShakerText.rectTransform, new Vector2(0, 1), new Vector2(280, 24), new Vector2(20, -46));
            _serveGlassText = NewText("Glass", _servePanel, _body, 13, TextAnchor.UpperRight, UITheme.TextPrimary);
            Place(_serveGlassText.rectTransform, new Vector2(1, 1), new Vector2(280, 24), new Vector2(-20, -46));

            // Garnishes (mint, olive) live at the serve stage now — a small row down the left.
            // Add one before you pour and it goes into the drink.
            var glabel = NewText("GLabel", _servePanel, _body, 10, TextAnchor.LowerLeft, UITheme.TypeRamp[IngredientType.Garnish][3]);
            Place(glabel.rectTransform, new Vector2(0, 0.5f), new Vector2(96, 16), new Vector2(14, 118));
            glabel.text = "— GARNISH —";
            _serveGarnishRow = NewRect("Garnishes", _servePanel);
            Place(_serveGarnishRow, new Vector2(0, 0.5f), new Vector2(96, 224), new Vector2(14, 0));
            var grow = _serveGarnishRow.gameObject.AddComponent<VerticalLayoutGroup>();
            grow.spacing = 8f; grow.childControlWidth = true; grow.childForceExpandWidth = true;
            grow.childControlHeight = false; grow.childAlignment = TextAnchor.UpperCenter;

            _aimText = NewText("AimText", _servePanel, _body, 13, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(_aimText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -70), new Vector2(0, -46));

            // The play surface: the glass sits centre-left, the shaker rests lower-right.
            _serveSurface = NewRect("ServeSurface", _servePanel);
            Stretch(_serveSurface, Vector2.zero, Vector2.one, new Vector2(20, 84), new Vector2(-20, -82));
            var surfImg = _serveSurface.gameObject.AddComponent<Image>();
            surfImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.5f);
            surfImg.raycastTarget = false;

            // The serving glass: real clear-glass art (2026-07-23), transparent interior so the
            // poured drink pools behind it and shows through; the outline+rim draw in front.
            _serveGlass = NewRect("Glass", _serveSurface);
            Place(_serveGlass, new Vector2(0.5f, 0.5f), new Vector2(150, 186), new Vector2(-210, -34));
            var glassImg = _serveGlass.gameObject.AddComponent<Image>();
            glassImg.raycastTarget = false;
            if (ItemArt.Glass != null) { glassImg.sprite = ItemArt.Glass; glassImg.preserveAspect = true; glassImg.color = Color.white; }
            else
            {
                glassImg.color = UITheme.Cream[2];
                var bowl = NewRect("Bowl", _serveGlass);
                Stretch(bowl, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -14));
                bowl.gameObject.AddComponent<Image>().color = UITheme.Night[3];
                var rim = NewRect("Rim", _serveGlass);
                Place(rim, new Vector2(0.5f, 1), new Vector2(104, 12), new Vector2(0, 0));
                rim.gameObject.AddComponent<Image>().color = UITheme.Cyan[3];
            }

            _serveFluid = new MetaballFluid(_serveSurface);
            // The tumbler: a slightly narrower base opening out to the mouth.
            _serveFluid.SetProfile(new[] { 0.88f, 0.93f, 0.96f, 0.98f, 1.00f, 1.00f });
            _serveSplash = new Splasher(_serveSurface);
            if (ItemArt.Glass != null) _serveGlass.SetAsLastSibling();   // clear glass over the fluid

            // The grabbable steel shaker you pour from, resting lower-right.
            _serveShakerRest = new Vector2(280, -70);
            _serveShaker = NewRect("Shaker", _serveSurface);
            _serveShaker.pivot = new Vector2(0.5f, 0.22f);
            _serveShaker.sizeDelta = new Vector2(104, BottleH);
            _serveShaker.anchoredPosition = _serveShakerRest;
            _serveShakerBody = _serveShaker.gameObject.AddComponent<Image>();
            if (ItemArt.Shaker != null) { _serveShakerBody.sprite = ItemArt.Shaker; _serveShakerBody.preserveAspect = true; _serveShakerBody.color = Color.white; }
            else
            {
                _serveShakerBody.color = UITheme.Cream[3];
                var cap = NewRect("Cap", _serveShaker);
                Place(cap, new Vector2(0.5f, 1), new Vector2(40, 22), new Vector2(0, 0));
                cap.gameObject.AddComponent<Image>().color = UITheme.Cream[4];
            }
            var sgrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            sgrab.callback.AddListener(_ =>
            {
                if (Run != null && Run.Phase == TycoonPhase.DayOpen && !Run.Glass.IsEmpty)
                    _serveGrabbed = true;
            });
            _serveShaker.gameObject.AddComponent<EventTrigger>().triggers.Add(sgrab);

            var back = NewRect("Back", _servePanel);
            Place(back, new Vector2(0.5f, 0), new Vector2(240, 34), new Vector2(-130, 12));
            back.gameObject.AddComponent<Image>().color = UITheme.Night[3];
            back.gameObject.AddComponent<Button>().onClick.AddListener(() => GoTo(Stage.Menu));
            var backLabel = NewText("Label", back, _body, 13, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backLabel.text = "← ADD MORE";

            var done = NewRect("Done", _servePanel);
            Place(done, new Vector2(0.5f, 0), new Vector2(240, 34), new Vector2(130, 12));
            done.gameObject.AddComponent<Image>().color = UITheme.PrimaryAction;
            done.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                // Ready to hand over: close the flow, then click a seat to deliver.
                if (!Run.ServingGlass.IsEmpty) GoTo(Stage.Closed);
            });
            var doneLabel = NewText("Label", done, _body, 13, TextAnchor.MiddleCenter, UITheme.TextOnAmber);
            Stretch(doneLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            doneLabel.text = "SERVE IT → PICK A SEAT";
        }

        /// <summary>One source chip on the prep tray: pointer-down picks its piece up.</summary>
        private void AddPrepSource(int index, string label, PreparationDefinition prep, Color colour)
        {
            var prepSprite = ItemArt.Prep(prep.Id);
            var bucketSprite = ItemArt.Bucket(prep.Id);
            var chip = NewRect($"Prep_{label}", _pourSurface);
            Place(chip, new Vector2(0.5f, 0), new Vector2(92, 88), new Vector2(-70 + index * 112, 58));
            var img = chip.gameObject.AddComponent<Image>();
            if (bucketSprite != null)
            {
                // A real bucket you grab a piece out of (2026-07-23): drag the ice / lemon /
                // salt / sugar from the bucket into the shaker.
                img.color = new Color(1f, 1f, 1f, 0.001f);   // clear grab target over the whole cell
                var icon = NewRect("Bucket", chip);
                Place(icon, new Vector2(0.5f, 1), new Vector2(80, 64), new Vector2(0, -2));
                var iconImg = icon.gameObject.AddComponent<Image>();
                iconImg.sprite = bucketSprite; iconImg.preserveAspect = true; iconImg.raycastTarget = false;
                var text = NewText("L", chip, _body, 9, TextAnchor.LowerCenter, UITheme.TextPrimary);
                Place(text.rectTransform, new Vector2(0.5f, 0), new Vector2(84, 14), new Vector2(0, 0));
                text.text = label;
            }
            else
            {
                img.color = new Color(colour.r, colour.g, colour.b, 0.85f);
                var text = NewText("L", chip, _body, 11, TextAnchor.MiddleCenter, UITheme.Night[0]);
                Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                text.text = label;
            }
            _benchProps.Add(chip.gameObject.AddComponent<CanvasGroup>());
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                if (_capped) return;   // the tin is closed — the bench is put away
                if (Run == null || Run.Glass.IsEmpty) { _shakerReadout.text = "pour something first"; return; }
                _draggingPrep = prep;
                var dpImg = _dragPiece.GetComponent<Image>();
                dpImg.sprite = prepSprite;
                dpImg.color = prepSprite != null ? Color.white : new Color(colour.r, colour.g, colour.b, 0.85f);
                _dragPieceLabel.text = prepSprite != null ? "" : label;
                _dragSwing.Reset();
                Vector2 start = chip.anchoredPosition;   // spring in from the tray chip
                if (Mouse.current != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _pourSurface, Mouse.current.position.ReadValue(), null, out Vector2 l0))
                    start = l0;
                _dragPos = start;
                _dragVel = Vector2.zero;
                _dragPiece.anchoredPosition = _dragPos;
                _dragPiece.localRotation = Quaternion.identity;
                _dragPiece.gameObject.SetActive(true);
            });
            chip.gameObject.AddComponent<EventTrigger>().triggers.Add(down);
        }

        // ── tiny UI helpers ──────────────────────────────────────────────────────

        /// <summary>EMPTY, drawn as a waste bin rather than a word.</summary>
        private void AddBinButton(RectTransform parent)
        {
            // Just the bin — you click the object, not a button plate around it.
            var rt = NewRect("Bin", parent);
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(52, 60), PaperCorner(1, -1) + new Vector2(-22f, 10f));
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.sprite = ItemArt.Load("btn_bin");
            img.color = img.sprite != null ? Color.white : UITheme.Night[3];
            img.alphaHitTestMinimumThreshold = img.sprite != null ? 0.35f : 0f;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => { Run.DiscardGlass(); RefreshMenu(); });
            GiveKeyPress(rt, btn, img, "btn_bin_down");
            if (img.sprite == null)
            {
                var fallback = NewText("L", rt, _body, 12, TextAnchor.MiddleCenter, UITheme.TextPrimary);
                Stretch(fallback.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                fallback.text = "EMPTY";
            }
        }

        private void AddFlexButton(RectTransform parent, string label, Color fill, Action onClick)
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
