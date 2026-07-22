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
        private Image _pourStream;
        private Vector2 _bottleRest;
        private bool _bottleGrabbed;
        private bool _pouring;
        private const float LiftRange = 200f;  // px of lift for a full tilt
        private const float MaxTilt = 118f;    // degrees the bottle leans at full lift
        private const float BottleH = 150f;

        private Text _serveShakerText;
        private Text _serveGlassText;
        private RectTransform _servePourZone;
        private Image _aimFill;
        private Text _aimText;
        private bool _servePourHeld;
        private const float ServePourRate = 0.6f;   // glass-fractions per second

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

            if (_stage == Stage.Shaker) UpdateTiltPour(run);

            if (_stage == Stage.Serve && _servePourHeld)
            {
                double before = run.ServingGlass.TotalVolume;
                run.PourIntoServingGlass(ServePourRate * Time.deltaTime, CurrentAim());
                if (run.Glass.IsEmpty || run.ServingGlass.FillFraction >= 1.0) _servePourHeld = false;
                if (run.ServingGlass.TotalVolume != before || !_servePourHeld) RefreshServe();
            }
        }

        // ── stage transitions ────────────────────────────────────────────────────

        private void GoTo(Stage stage)
        {
            _stage = stage;
            _bottleGrabbed = false;
            _pouring = false;
            _servePourHeld = false;
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
            _pourStream.gameObject.SetActive(false);
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

                DrawStream(mouth, opening, pourNow);
            }
            else
            {
                DrawStream(default, default, false);
            }

            if (pourNow)
            {
                if (run.PouringId == null) run.BeginPour(_focusBottle.Id);
                run.PourTick(Time.deltaTime);
                _shakerReadout.text = ShakerLine(run);
            }
            else if (run.PouringId != null)
            {
                run.EndPour();
            }
            _pouring = pourNow;
        }

        /// <summary>The falling stream from mouth to opening while pouring; hidden otherwise.</summary>
        private void DrawStream(Vector2 mouth, Vector2 opening, bool on)
        {
            if (!on) { _pourStream.gameObject.SetActive(false); return; }
            _pourStream.gameObject.SetActive(true);
            var rt = _pourStream.rectTransform;
            float top = mouth.y;
            float bottom = opening.y;
            rt.anchoredPosition = new Vector2(mouth.x, (top + bottom) * 0.5f);
            rt.sizeDelta = new Vector2(5f, Mathf.Max(8f, top - bottom));
            _pourStream.color = new Color(_pourBottleBody.color.r, _pourBottleBody.color.g,
                _pourBottleBody.color.b, 0.85f);
        }

        // ── the serve stage ──────────────────────────────────────────────────────

        private void RefreshServe()
        {
            var run = Run;
            _serveShakerText.text = run.Glass.IsEmpty
                ? "shaker empty"
                : $"shaker {run.Glass.FillFraction:P0} left";
            _serveGlassText.text = run.ServingGlass.IsEmpty
                ? "glass empty — hold to pour"
                : $"glass {run.ServingGlass.FillFraction:P0} full";
        }

        /// <summary>
        /// Aim accuracy from the cursor's horizontal offset inside the pour zone: dead centre
        /// over the glass is a clean pour, drifting to the edges spills (GDD 24 §3).
        /// </summary>
        private double CurrentAim()
        {
            if (Mouse.current == null) return 1.0;
            Vector2 screen = Mouse.current.position.ReadValue();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _servePourZone, screen, null, out Vector2 local))
                return 0.0;
            float half = _servePourZone.rect.width * 0.5f;
            if (half <= 0) return 1.0;
            double aim = 1.0 - Mathf.Abs(local.x) / half;
            aim = Mathf.Clamp01((float)aim);
            _aimFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(200f * (float)aim), -4);
            _aimFill.color = Color.Lerp(UITheme.ViceRed[3], UITheme.Lime[3], (float)aim);
            _aimText.text = _servePourHeld ? $"AIM {aim:P0}" : "hold & aim over the glass";
            return aim;
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
            var lip = NewRect("Lip", _shakerVessel);   // the open mouth
            Place(lip, new Vector2(0.5f, 1), new Vector2(128, 16), new Vector2(0, 0));
            lip.gameObject.AddComponent<Image>().color = UITheme.Cream[3];

            // The falling liquid stream (hidden until pouring).
            var stream = NewRect("Stream", _pourSurface);
            stream.pivot = new Vector2(0.5f, 0.5f);
            _pourStream = stream.gameObject.AddComponent<Image>();
            _pourStream.raycastTarget = false;
            stream.gameObject.SetActive(false);

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

            _shakerReadout = NewText("Readout", _shakerPanel, _body, 13, TextAnchor.LowerCenter, UITheme.TextSecondary);
            Stretch(_shakerReadout.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(16, 52), new Vector2(-16, 84));

            var back = NewRect("Back", _shakerPanel);
            Place(back, new Vector2(0.5f, 0), new Vector2(220, 34), new Vector2(0, 12));
            back.gameObject.AddComponent<Image>().color = UITheme.PrimaryAction;
            back.gameObject.AddComponent<Button>().onClick.AddListener(() => GoTo(Stage.Menu));
            var backLabel = NewText("Label", back, _body, 13, TextAnchor.MiddleCenter, UITheme.TextOnAmber);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backLabel.text = "← DONE — BACK TO MENU";
        }

        private void BuildServePanel()
        {
            _servePanel = NewRect("ServePanel", _root);
            Place(_servePanel, new Vector2(0.5f, 0.5f), new Vector2(600, 460), Vector2.zero);
            _servePanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_servePanel);

            var title = NewText("Title", _servePanel, _display, 16, TextAnchor.UpperCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -40), new Vector2(0, -10));
            title.text = "POUR THE GLASS";

            _serveShakerText = NewText("Shaker", _servePanel, _body, 13, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(_serveShakerText.rectTransform, new Vector2(0, 1), new Vector2(260, 24), new Vector2(20, -56));
            _serveGlassText = NewText("Glass", _servePanel, _body, 13, TextAnchor.UpperRight, UITheme.TextPrimary);
            Place(_serveGlassText.rectTransform, new Vector2(1, 1), new Vector2(260, 24), new Vector2(-20, -56));

            // The pour zone: hold and keep the cursor centred over the glass. Off-centre spills.
            _servePourZone = NewRect("PourZone", _servePanel);
            Place(_servePourZone, new Vector2(0.5f, 0.5f), new Vector2(420, 200), new Vector2(0, 20));
            var zoneImg = _servePourZone.gameObject.AddComponent<Image>();
            zoneImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.6f);
            HoldTrigger(_servePourZone, () => _servePourHeld = Run != null && !Run.Glass.IsEmpty,
                () => _servePourHeld = false);
            // A target band down the middle marks the glass mouth.
            var target = NewRect("Target", _servePourZone);
            Place(target, new Vector2(0.5f, 0.5f), new Vector2(60, 200), Vector2.zero);
            var targetImg = target.gameObject.AddComponent<Image>();
            targetImg.color = new Color(UITheme.Cyan[3].r, UITheme.Cyan[3].g, UITheme.Cyan[3].b, 0.28f);
            targetImg.raycastTarget = false;
            var zoneLabel = NewText("Label", _servePourZone, _display, 13, TextAnchor.MiddleCenter, UITheme.TextSecondary);
            Stretch(zoneLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            zoneLabel.text = "HOLD & AIM";
            zoneLabel.raycastTarget = false;

            var aimBg = NewRect("AimBg", _servePanel);
            Place(aimBg, new Vector2(0.5f, 0), new Vector2(200, 12), new Vector2(0, 96));
            aimBg.gameObject.AddComponent<Image>().color = UITheme.Night[0];
            var aimFill = NewRect("AimFill", aimBg);
            aimFill.anchorMin = new Vector2(0, 0); aimFill.anchorMax = new Vector2(0, 1);
            aimFill.pivot = new Vector2(0, 0.5f); aimFill.offsetMin = new Vector2(2, 2);
            aimFill.offsetMax = new Vector2(2, -2); aimFill.anchoredPosition = new Vector2(2, 0);
            _aimFill = aimFill.gameObject.AddComponent<Image>();
            _aimFill.raycastTarget = false;
            _aimText = NewText("AimText", _servePanel, _body, 12, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Place(_aimText.rectTransform, new Vector2(0.5f, 0), new Vector2(300, 20), new Vector2(0, 112));

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

        private static void HoldTrigger(RectTransform rt, Action onDown, Action onUp)
        {
            var trigger = rt.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => onDown());
            trigger.triggers.Add(down);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => onUp());
            trigger.triggers.Add(up);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => onUp());
            trigger.triggers.Add(exit);
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
