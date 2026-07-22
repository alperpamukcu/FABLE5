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
        private RectTransform _pourBottle;    // the grabbable bottle
        private Image _pourBottleBody;
        private Image _shakerLiquid;          // the liquid rising inside the shaker
        private Splasher _shakerSplash;       // falling droplets / solids in the shaker stage
        private float _shakerLiquidFloorY;    // surface y at empty, for droplet catch
        private Vector2 _bottleRest;
        private bool _bottleGrabbed;
        private bool _pouring;
        private const float LiftRange = 200f;  // px of lift for a full tilt
        private const float MaxTilt = 118f;    // degrees the bottle leans at full lift
        private const float BottleH = 150f;

        // Drag-drop preparations (GDD 24 §2.4): pick a piece off its tray and drop it into
        // the shaker's mouth.
        private PreparationDefinition _draggingPrep;
        private RectTransform _dragPiece;
        private Text _dragPieceLabel;

        // The mouse-energy shake (GDD 24 §2.5): hold the shake pad and shake the mouse; how
        // much the cursor travels builds the energy, and the shaker jitters as you go.
        private bool _shaking;
        private double _shakeEnergy;
        private Vector2 _lastShakeMouse;
        private Image _shakeMeterFill;
        private Text _shakeMeterText;
        private const float ShakeFullTravel = 4000f;   // px of cursor travel for a full shake

        // The serve pour uses the same tilt model (GDD 24 §3): grab the shaker, tip it over
        // the glass. How well the mouth lines up over the glass is the aim — off-centre spills.
        private Text _serveShakerText;
        private Text _serveGlassText;
        private RectTransform _serveSurface;
        private RectTransform _serveGlass;      // the target
        private RectTransform _serveShaker;     // the grabbable shaker
        private Image _serveShakerBody;
        private Image _serveLiquid;             // the liquid rising inside the serving glass
        private Splasher _serveSplash;
        private Text _aimText;
        private Vector2 _serveShakerRest;
        private bool _serveGrabbed;
        private const float ServePourRate = 0.7f;   // glass-fractions per second

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
            var run = Run;
            if (run == null) return;

            if (_stage != Stage.Closed && run.Phase != TycoonPhase.DayOpen)
            {
                GoTo(Stage.Closed);
                return;
            }

            if (_stage == Stage.Shaker) { UpdateShake(run); UpdatePrepDrag(run); UpdateTiltPour(run); }

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
            if (Run != null && Run.PouringId != null) Run.EndPour();

            _root.gameObject.SetActive(stage != Stage.Closed);
            _menuPanel.gameObject.SetActive(stage == Stage.Menu);
            _shakerPanel.gameObject.SetActive(stage == Stage.Shaker);
            _servePanel.gameObject.SetActive(stage == Stage.Serve);

            if (stage == Stage.Menu) RefreshMenu();
            if (stage == Stage.Shaker) RefreshShaker();
            if (stage == Stage.Serve) RefreshServe();
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

        private void RefreshMenu()
        {
            var run = Run;
            foreach (Transform child in _bottleList) Destroy(child.gameObject);

            foreach (var bottle in run.Shelf.Bottles)
            {
                var card = bottle.Ingredient;
                var colour = UITheme.StyleColor(card.Info?.Style, card.Type);
                double fill = bottle.Capacity > 0 ? bottle.Remaining / bottle.Capacity : 0;
                string label = $"{card.Name.ToUpperInvariant()}    {fill:P0} LEFT";
                var row = AddListButton(_bottleList, label, colour, bottle.IsEmpty
                    ? (Action)null : () => OpenBottle(card));
            }

            _menuShaker.text = ShakerLine(run);
            var preps = new List<string>();
            foreach (var prep in run.Glass.PreparationSteps) preps.Add(prep.Name);
            _menuPreps.text = preps.Count == 0 ? "no preparations" : "+ " + string.Join(", ", preps);
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
            _pourBottleBody.color = colour;
            _pourBottle.anchoredPosition = _bottleRest;
            _pourBottle.localRotation = Quaternion.identity;
            _shakerSplash.Clear();
            SetLiquid(_shakerLiquid, run.Glass);
            _shakerVessel.anchoredPosition = new Vector2(-120, -30);
            _shakerVessel.localRotation = Quaternion.identity;
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
                    // A ribbon of droplets falls from the mouth, aimed at the opening.
                    var colour = UITheme.StyleColor(_focusBottle.Info?.Style, _focusBottle.Type);
                    for (int i = 0; i < 2; i++)
                        _shakerSplash.EmitDroplet(mouth,
                            new Vector2((opening.x - mouth.x) * 1.1f, -140f), colour, _shakerLiquidFloorY);
                }
            }

            if (pourNow)
            {
                if (run.PouringId == null) run.BeginPour(_focusBottle.Id);
                run.PourTick(Time.deltaTime);
                SetLiquid(_shakerLiquid, run.Glass);
                _shakerReadout.text = ShakerLine(run);
            }
            else if (run.PouringId != null)
            {
                run.EndPour();
            }
            _shakerSplash.Step(Time.deltaTime);
            _pouring = pourNow;
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
                _shakerVessel.anchoredPosition = new Vector2(-120, -30);
                if (_shakeMeterText != null) _shakeMeterText.text = "";
                return;
            }

            float travel = (mouse - _lastShakeMouse).magnitude;
            _lastShakeMouse = mouse;
            _shakeEnergy = Mathf.Clamp01((float)_shakeEnergy + travel / ShakeFullTravel);

            // Jitter the shaker so it reads as being worked.
            float amp = 6f + 10f * (float)_shakeEnergy;
            _shakerVessel.anchoredPosition = new Vector2(-120, -30) +
                new Vector2(UnityEngine.Random.Range(-amp, amp), UnityEngine.Random.Range(-amp, amp));
            _shakerVessel.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-8f, 8f) * (float)_shakeEnergy);

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
                _pourSurface, Mouse.current.position.ReadValue(), null, out Vector2 local);
            _dragPiece.anchoredPosition = local;

            if (Mouse.current.leftButton.isPressed) return;

            // Dropped. Over the shaker's mouth → it goes in.
            var opening = _shakerVessel.anchoredPosition + new Vector2(0, _shakerVessel.rect.height * 0.5f);
            bool inMouth = Mathf.Abs(local.x - opening.x) < 90f && Mathf.Abs(local.y - opening.y) < 90f;
            if (inMouth && !run.Glass.IsEmpty)
            {
                run.AddPreparation(_draggingPrep);
                _shakerReadout.text = ShakerLine(run);
                // The piece drops in and settles on the drink (a splash of granules for a rim).
                var c = _dragPiece.GetComponent<Image>().color;
                bool granular = _draggingPrep.Id == "salt_rim" || _draggingPrep.Id == "sugar_rim";
                int n = granular ? 8 : 1;
                for (int i = 0; i < n; i++)
                    _shakerSplash.EmitSolid(
                        new Vector2(opening.x + UnityEngine.Random.Range(-14f, 14f), opening.y),
                        new Vector2(UnityEngine.Random.Range(-40f, 40f), -80f), c, _shakerLiquidFloorY,
                        granular ? 8f : 26f);
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
            SetLiquid(_serveLiquid, run.ServingGlass);
            _serveShakerBody.color = DrinkColor(run.Glass);
            _aimText.text = "GRAB THE SHAKER · TIP IT OVER THE GLASS";
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

                    // Droplets aim for the glass; a poor aim throws them wide, and they
                    // fall past the rim (the spill you can see).
                    var colour = DrinkColor(run.Glass);
                    float landX = Mathf.Lerp(mouth.x + (mouth.x - opening.x) * 1.5f, opening.x, (float)accuracy);
                    float floor = accuracy > 0.35 ? opening.y - _serveGlass.rect.height * 0.4f
                                                  : -_serveSurface.rect.height * 0.5f;
                    for (int i = 0; i < 2; i++)
                        _serveSplash.EmitDroplet(mouth, new Vector2((landX - mouth.x) * 1.2f, -140f), colour, floor);
                }
            }

            if (pourNow)
            {
                double before = run.ServingGlass.TotalVolume;
                run.PourIntoServingGlass(ServePourRate * Time.deltaTime, accuracy);
                SetLiquid(_serveLiquid, run.ServingGlass);
                if (run.Glass.IsEmpty || run.ServingGlass.FillFraction >= 1.0) _serveGrabbed = false;
                if (run.ServingGlass.TotalVolume != before) RefreshServeText(run, accuracy);
            }
            _serveSplash.Step(Time.deltaTime);
        }

        private void RefreshServeText(TycoonRun run, double accuracy)
        {
            _serveShakerText.text = $"shaker {run.Glass.FillFraction:P0} left";
            _serveGlassText.text = $"glass {run.ServingGlass.FillFraction:P0} full";
            _aimText.text = accuracy > 0.8 ? "CLEAN POUR" : accuracy > 0.4 ? "SOME SPILL" : "SPILLING!";
            _aimText.color = Color.Lerp(UITheme.ViceRed[3], UITheme.Lime[3], (float)accuracy);
        }

        // ── liquid & colour helpers ──────────────────────────────────────────────

        /// <summary>A liquid fill anchored to a vessel's bottom, resized by <see cref="SetLiquid"/>.</summary>
        private Image MakeLiquid(RectTransform vessel)
        {
            var rt = NewRect("Liquid", vessel);
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(2, 0); rt.offsetMax = new Vector2(-2, 0);
            rt.sizeDelta = new Vector2(-4, 0);
            rt.anchoredPosition = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            rt.gameObject.SetActive(false);
            return img;
        }

        /// <summary>Sets a vessel's liquid level from its contents (GDD 24 §2, §7).</summary>
        private void SetLiquid(Image liquid, GlassContents glass)
        {
            if (liquid == null) return;
            if (glass == null || glass.IsEmpty) { liquid.gameObject.SetActive(false); return; }
            var parent = (RectTransform)liquid.transform.parent;
            float h = parent.rect.height * (float)glass.FillFraction;
            liquid.rectTransform.sizeDelta = new Vector2(-4, h);
            liquid.color = DrinkColor(glass);
            liquid.gameObject.SetActive(true);
        }

        /// <summary>The drink's colour: its ingredients' style colours, blended by share.</summary>
        private Color DrinkColor(GlassContents glass)
        {
            if (glass == null || glass.IsEmpty) return UITheme.Cream[3];
            var shelf = Run?.Shelf;
            float r = 0, g = 0, b = 0;
            foreach (var id in glass.Ingredients)
            {
                var card = shelf?.Find(id)?.Ingredient;
                var c = card != null ? UITheme.StyleColor(card.Info?.Style, card.Type) : UITheme.Cream[3];
                float w = (float)glass.RatioOf(id);
                r += c.r * w; g += c.g * w; b += c.b * w;
            }
            return new Color(r, g, b, 0.9f);
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
            _menuPanel = NewRect("MenuPanel", _root);
            Place(_menuPanel, new Vector2(0.5f, 0.5f), new Vector2(560, 560), Vector2.zero);
            _menuPanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_menuPanel);

            var title = NewText("Title", _menuPanel, _display, 16, TextAnchor.UpperCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -40), new Vector2(0, -8));
            title.text = "MAKE A DRINK";

            _bottleList = NewRect("Bottles", _menuPanel);
            Stretch(_bottleList, new Vector2(0, 0), new Vector2(1, 1), new Vector2(16, 150), new Vector2(-16, -48));
            var layout = _bottleList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f; layout.childForceExpandHeight = false;
            layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            _menuShaker = NewText("Shaker", _menuPanel, _body, 13, TextAnchor.LowerLeft, UITheme.TextPrimary);
            Stretch(_menuShaker.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(16, 116), new Vector2(-16, 140));
            _menuPreps = NewText("Preps", _menuPanel, _body, 12, TextAnchor.LowerLeft, UITheme.Cyan[4]);
            Stretch(_menuPreps.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(16, 96), new Vector2(-16, 116));

            // Preparation toggles (GDD 24 §2.4) — plumbing, no craft effect yet.
            var prepRow = NewRect("PrepRow", _menuPanel);
            Stretch(prepRow, Vector2.zero, new Vector2(1, 0), new Vector2(16, 56), new Vector2(-16, 92));
            var prepLayout = prepRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            prepLayout.spacing = 4f; prepLayout.childControlWidth = true;
            prepLayout.childForceExpandWidth = true; prepLayout.childControlHeight = true;
            prepLayout.childForceExpandHeight = true;
            AddPrepButton(prepRow, "ICE", Preparations.Ice);
            AddPrepButton(prepRow, "LEMON", Preparations.LemonTwist);
            AddPrepButton(prepRow, "SALT", Preparations.SaltRim);
            AddPrepButton(prepRow, "SUGAR", Preparations.SugarRim);

            // Action row: shake, pour, empty, close.
            var actionRow = NewRect("Actions", _menuPanel);
            Stretch(actionRow, Vector2.zero, new Vector2(1, 0), new Vector2(16, 12), new Vector2(-16, 52));
            var actLayout = actionRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            actLayout.spacing = 4f; actLayout.childControlWidth = true;
            actLayout.childForceExpandWidth = true; actLayout.childControlHeight = true;
            actLayout.childForceExpandHeight = true;
            AddFlexButton(actionRow, "SHAKE", UITheme.ClubBlue[2], () =>
            {
                if (!Run.Glass.IsEmpty) { Run.Shake(); RefreshMenu(); }
            });
            AddFlexButton(actionRow, "POUR →", UITheme.PrimaryAction, () =>
            {
                if (!Run.Glass.IsEmpty) GoTo(Stage.Serve);
            });
            AddFlexButton(actionRow, "EMPTY", UITheme.Night[3], () =>
            {
                Run.DiscardGlass(); RefreshMenu();
            });
            AddFlexButton(actionRow, "CLOSE", UITheme.Night[3], CloseFlow);
        }

        private void BuildShakerPanel()
        {
            _shakerPanel = NewRect("ShakerPanel", _root);
            Place(_shakerPanel, new Vector2(0.5f, 0.5f), new Vector2(720, 520), Vector2.zero);
            _shakerPanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_shakerPanel);

            _shakerTitle = NewText("Title", _shakerPanel, _display, 18, TextAnchor.UpperCenter, UITheme.TextPrimary);
            Stretch(_shakerTitle.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -44), new Vector2(0, -10));

            var hint = NewText("Hint", _shakerPanel, _body, 12, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(hint.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -64), new Vector2(0, -46));
            hint.text = "GRAB THE BOTTLE · LIFT IT TO TIP · POUR INTO THE SHAKER";

            // The play surface: bottle and shaker live in here, mouse-local.
            _pourSurface = NewRect("PourSurface", _shakerPanel);
            Stretch(_pourSurface, Vector2.zero, Vector2.one, new Vector2(20, 84), new Vector2(-20, -76));
            var surfImg = _pourSurface.gameObject.AddComponent<Image>();
            surfImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.5f);
            surfImg.raycastTarget = false;

            // The shaker vessel: a tapered tin, opening at the top, left of centre.
            _shakerVessel = NewRect("Shaker", _pourSurface);
            Place(_shakerVessel, new Vector2(0.5f, 0.5f), new Vector2(120, 190), new Vector2(-120, -30));
            _shakerVessel.gameObject.AddComponent<Image>().color = UITheme.Cream[2];
            var tin = NewRect("Tin", _shakerVessel);
            Stretch(tin, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -22));
            tin.gameObject.AddComponent<Image>().color = UITheme.Night[3];
            _shakerLiquid = MakeLiquid(tin);   // the liquid that rises as you pour
            var lip = NewRect("Lip", _shakerVessel);   // the open mouth
            Place(lip, new Vector2(0.5f, 1), new Vector2(128, 16), new Vector2(0, 0));
            lip.gameObject.AddComponent<Image>().color = UITheme.Cream[3];

            // Droplets fall on this surface; they die (splash) at the shaker's liquid line.
            _shakerSplash = new Splasher(_pourSurface);
            _shakerLiquidFloorY = _shakerVessel.anchoredPosition.y - _shakerVessel.rect.height * 0.5f + 12f;

            // The grabbable bottle, resting lower-right. Procedural body + neck; the grip
            // pivot sits low so lifting swings the mouth in a big arc.
            _bottleRest = new Vector2(170, -70);
            _pourBottle = NewRect("Bottle", _pourSurface);
            _pourBottle.pivot = new Vector2(0.5f, 0.22f);
            _pourBottle.sizeDelta = new Vector2(56, BottleH);
            _pourBottle.anchoredPosition = _bottleRest;
            _pourBottleBody = _pourBottle.gameObject.AddComponent<Image>();
            _pourBottleBody.color = UITheme.Cyan[3];
            var neck = NewRect("Neck", _pourBottle);
            Place(neck, new Vector2(0.5f, 1), new Vector2(20, 34), new Vector2(0, 0));
            neck.gameObject.AddComponent<Image>().color = UITheme.Cream[3];
            var grip = NewText("Grip", _pourBottle, _body, 10, TextAnchor.MiddleCenter, UITheme.Night[0]);
            Stretch(grip.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            grip.text = "";
            // Pointer-down anywhere on the bottle grabs it.
            var grab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            grab.callback.AddListener(_ =>
            {
                if (_focusBottle != null && Run != null && Run.Phase == TycoonPhase.DayOpen)
                    _bottleGrabbed = true;
            });
            _pourBottle.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);

            // The prep tray, down the left edge: pick a piece up and drag it into the shaker.
            AddPrepSource(0, "ICE", Preparations.Ice, UITheme.Cyan[4]);
            AddPrepSource(1, "LEMON", Preparations.LemonTwist, UITheme.Amber[4]);
            AddPrepSource(2, "SALT", Preparations.SaltRim, UITheme.Cream[4]);
            AddPrepSource(3, "SUGAR", Preparations.SugarRim, UITheme.Magenta[4]);

            // The single piece that follows the mouse while a prep is held.
            _dragPiece = NewRect("DragPiece", _pourSurface);
            _dragPiece.pivot = new Vector2(0.5f, 0.5f);
            _dragPiece.sizeDelta = new Vector2(48, 48);
            _dragPiece.gameObject.AddComponent<Image>().raycastTarget = false;
            _dragPieceLabel = NewText("L", _dragPiece, _body, 10, TextAnchor.MiddleCenter, UITheme.Night[0]);
            Stretch(_dragPieceLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
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

            // Bottom bar: the shake pad (hold and shake the mouse) and DONE.
            var pad = NewRect("ShakePad", _shakerPanel);
            Place(pad, new Vector2(0.5f, 0), new Vector2(300, 40), new Vector2(-160, 12));
            pad.gameObject.AddComponent<Image>().color = UITheme.ClubBlue[2];
            var padDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            padDown.callback.AddListener(_ =>
            {
                if (Run == null || Run.Glass.IsEmpty) { _shakerReadout.text = "pour something to shake"; return; }
                _shaking = true;
                _shakeEnergy = 0;
                _lastShakeMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            });
            pad.gameObject.AddComponent<EventTrigger>().triggers.Add(padDown);
            var padLabel = NewText("Label", pad, _body, 12, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Stretch(padLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            padLabel.text = "HOLD · SHAKE THE MOUSE";

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
            Place(_servePanel, new Vector2(0.5f, 0.5f), new Vector2(720, 520), Vector2.zero);
            _servePanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_servePanel);

            var title = NewText("Title", _servePanel, _display, 16, TextAnchor.UpperCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -40), new Vector2(0, -10));
            title.text = "POUR THE GLASS";

            _serveShakerText = NewText("Shaker", _servePanel, _body, 13, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(_serveShakerText.rectTransform, new Vector2(0, 1), new Vector2(280, 24), new Vector2(20, -46));
            _serveGlassText = NewText("Glass", _servePanel, _body, 13, TextAnchor.UpperRight, UITheme.TextPrimary);
            Place(_serveGlassText.rectTransform, new Vector2(1, 1), new Vector2(280, 24), new Vector2(-20, -46));

            _aimText = NewText("AimText", _servePanel, _body, 13, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(_aimText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -70), new Vector2(0, -46));

            // The play surface: the glass sits centre-left, the shaker rests lower-right.
            _serveSurface = NewRect("ServeSurface", _servePanel);
            Stretch(_serveSurface, Vector2.zero, Vector2.one, new Vector2(20, 84), new Vector2(-20, -82));
            var surfImg = _serveSurface.gameObject.AddComponent<Image>();
            surfImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.5f);
            surfImg.raycastTarget = false;

            // The serving glass (a simple tumbler outline), opening at the top.
            _serveGlass = NewRect("Glass", _serveSurface);
            Place(_serveGlass, new Vector2(0.5f, 0.5f), new Vector2(96, 150), new Vector2(-120, -40));
            _serveGlass.gameObject.AddComponent<Image>().color = UITheme.Cream[2];
            var bowl = NewRect("Bowl", _serveGlass);
            Stretch(bowl, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -14));
            bowl.gameObject.AddComponent<Image>().color = UITheme.Night[3];
            _serveLiquid = MakeLiquid(bowl);
            var rim = NewRect("Rim", _serveGlass);
            Place(rim, new Vector2(0.5f, 1), new Vector2(104, 12), new Vector2(0, 0));
            rim.gameObject.AddComponent<Image>().color = UITheme.Cyan[3];

            _serveSplash = new Splasher(_serveSurface);

            // The grabbable shaker, resting lower-right.
            _serveShakerRest = new Vector2(160, -70);
            _serveShaker = NewRect("Shaker", _serveSurface);
            _serveShaker.pivot = new Vector2(0.5f, 0.22f);
            _serveShaker.sizeDelta = new Vector2(64, BottleH);
            _serveShaker.anchoredPosition = _serveShakerRest;
            _serveShakerBody = _serveShaker.gameObject.AddComponent<Image>();
            _serveShakerBody.color = UITheme.Cream[3];
            var cap = NewRect("Cap", _serveShaker);
            Place(cap, new Vector2(0.5f, 1), new Vector2(40, 22), new Vector2(0, 0));
            cap.gameObject.AddComponent<Image>().color = UITheme.Cream[4];
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
            var chip = NewRect($"Prep_{label}", _pourSurface);
            Place(chip, new Vector2(0, 1), new Vector2(72, 44), new Vector2(14, -14 - index * 52));
            var img = chip.gameObject.AddComponent<Image>();
            img.color = new Color(colour.r, colour.g, colour.b, 0.85f);
            var text = NewText("L", chip, _body, 11, TextAnchor.MiddleCenter, UITheme.Night[0]);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.text = label;
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                if (Run == null || Run.Glass.IsEmpty) { _shakerReadout.text = "pour something first"; return; }
                _draggingPrep = prep;
                _dragPiece.GetComponent<Image>().color = img.color;
                _dragPieceLabel.text = label;
                _dragPiece.gameObject.SetActive(true);
            });
            chip.gameObject.AddComponent<EventTrigger>().triggers.Add(down);
        }

        // ── tiny UI helpers ──────────────────────────────────────────────────────

        private void AddPrepButton(RectTransform parent, string label, PreparationDefinition prep)
        {
            AddFlexButton(parent, label, UITheme.Night[3], () =>
            {
                if (Run.Glass.IsEmpty) return;
                Run.AddPreparation(prep);
                RefreshMenu();
            });
        }

        private void AddFlexButton(RectTransform parent, string label, Color fill, Action onClick)
        {
            var rt = NewRect(label, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = fill;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            var text = NewText("Label", rt, _body, 12, TextAnchor.MiddleCenter,
                fill == UITheme.PrimaryAction ? UITheme.TextOnAmber : UITheme.TextPrimary);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
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
