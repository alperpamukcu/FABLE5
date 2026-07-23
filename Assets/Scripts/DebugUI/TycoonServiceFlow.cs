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
            _shakerFluid?.Clear();
            _serveFluid?.Clear();
            _shakerSolids?.Clear();
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

            // Grouped like a real bar's back shelf: spirits, then bitters, sweet, sour,
            // bubbly, garnishes — each under its own coloured heading (GDD 24 §1).
            foreach (var type in MenuOrder)
            {
                // Garnishes are no longer mixed in with the alcohols — they're added at the
                // serve stage now (2026-07-23), so the menu is spirits and mixers only.
                if (type == IngredientType.Garnish) continue;
                var group = new List<ShelfBottle>();
                foreach (var bottle in run.Shelf.Bottles)
                    if (bottle.Ingredient.Type == type) group.Add(bottle);
                if (group.Count == 0) continue;

                // A section: a coloured heading, then a wrapping grid of item boxes.
                var section = NewRect($"Sec_{type}", _bottleList);
                var vl = section.gameObject.AddComponent<VerticalLayoutGroup>();
                vl.spacing = 3f; vl.childControlHeight = true; vl.childControlWidth = true;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
                vl.childAlignment = TextAnchor.UpperLeft;
                section.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;

                AddGroupHeader(section, GroupName(type), UITheme.TypeRamp[type][3]);

                var grid = NewRect("Grid", section);
                var g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(88, 96); g.spacing = new Vector2(6, 6);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 4;
                grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;
                foreach (var bottle in group)
                    AddItemBox(grid, bottle, run);
            }

            _menuShaker.text = ShakerLine(run);
            var preps = new List<string>();
            foreach (var prep in run.Glass.PreparationSteps) preps.Add(prep.Name);
            _menuPreps.text = preps.Count == 0 ? "no preparations" : "+ " + string.Join(", ", preps);
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

        private void AddGroupHeader(RectTransform parent, string title, Color colour)
        {
            var rt = NewRect("Header", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            var text = NewText("L", rt, _body, 12, TextAnchor.LowerLeft, colour);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 0), new Vector2(-2, 0));
            text.text = $"— {title} —";
            var line = NewRect("Rule", rt);
            line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0);
            line.offsetMin = new Vector2(0, 0); line.offsetMax = new Vector2(0, 2);
            var img = line.gameObject.AddComponent<Image>();
            img.color = new Color(colour.r, colour.g, colour.b, 0.4f);
            img.raycastTarget = false;
        }

        /// <summary>One item box in the menu grid: the bottle/ingredient art, its name and its
        /// remaining fill (OUT when empty, ★NEW when just bought). Tapping it opens the pour.</summary>
        private void AddItemBox(RectTransform parent, ShelfBottle bottle, TycoonRun run)
        {
            var card = bottle.Ingredient;
            bool empty = bottle.IsEmpty;
            var box = NewRect($"Box_{card.Id}", parent);
            var bg = box.gameObject.AddComponent<Image>();
            bg.color = empty
                ? new Color(0.32f, 0.30f, 0.34f, 0.45f)
                : new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.9f);

            var icon = NewRect("Icon", box);
            Place(icon, new Vector2(0.5f, 0.5f), new Vector2(66, 66), new Vector2(0, 8));   // centred
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.raycastTarget = false; iconImg.preserveAspect = true;
            iconImg.sprite = ItemArt.Bottle(card.Info?.Style);
            iconImg.color = iconImg.sprite == null ? UITheme.StyleColor(card.Info?.Style, card.Type)
                : (empty ? new Color(1f, 1f, 1f, 0.4f) : Color.white);

            var name = NewText("Name", box, _body, 9, TextAnchor.LowerCenter,
                empty ? UITheme.TextSecondary : UITheme.TextPrimary);
            Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(86, 16), new Vector2(0, 2));
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.text = card.Name.ToUpperInvariant();

            var badge = NewText("Badge", box, _body, 8, TextAnchor.UpperRight,
                empty ? UITheme.ViceRed[3] : run.IsNewStock(card.Id) ? UITheme.PrimaryAction : UITheme.Cyan[4]);
            Place(badge.rectTransform, new Vector2(1, 1), new Vector2(56, 12), new Vector2(-3, -3));
            double fill = bottle.Capacity > 0 ? bottle.Remaining / bottle.Capacity : 0;
            badge.text = empty ? "OUT"
                : run.IsNewStock(card.Id) ? "★NEW" : $"{(int)System.Math.Round(fill * 100)}%";

            if (!empty)
            {
                var btn = box.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg; btn.transition = Selectable.Transition.ColorTint;
                var c = card;
                btn.onClick.AddListener(() => OpenBottle(c));
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
            float iw = halfW * 0.62f;
            float minX = c.x - iw;
            float maxX = c.x + iw;
            float h = _shakerVessel.rect.height;
            float bottomY = c.y - h * 0.5f + h * 0.13f;
            float innerH = h * 0.64f;
            float topY = bottomY + innerH * (float)run.Glass.FillFraction + bob;
            // The liquid tilts with the tin (2026-07-23) so they read as one mass while shaking.
            float deg = _shakerVessel.localEulerAngles.z;
            if (deg > 180f) deg -= 360f;
            _shakerFluid.SetPool(minX, maxX, bottomY, topY, deg * Mathf.Deg2Rad);
            // The solids float on this same liquid line and bounce off these same walls.
            _shakerSolids.SetBounds(minX, maxX, bottomY, topY);
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

                // The liquid sloshes opposite to the throw and gets jostled — waves slap the walls.
                float lateral = -_shakerVel.x / Mathf.Max(_pourSurface.rect.width, 1f) * 0.22f;
                _shakerFluid.Disturb(lateral);
                float speed = _shakerVel.magnitude;
                if (speed > 200f)
                    _shakerFluid.Ripple(_shakerVessel.anchoredPosition.x + UnityEngine.Random.Range(-36f, 36f),
                        Mathf.Min(speed / 8000f, 0.05f));
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
            float topY = bottomY + innerH * (float)run.ServingGlass.FillFraction;
            _serveFluid.SetPool(minX, maxX, bottomY, topY);
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
            _menuPanel = NewRect("MenuPanel", _root);
            Place(_menuPanel, new Vector2(0.5f, 0.5f), new Vector2(560, 560), Vector2.zero);
            _menuPanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_menuPanel);

            var title = NewText("Title", _menuPanel, _display, 16, TextAnchor.UpperCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -40), new Vector2(0, -8));
            title.text = "MAKE A DRINK";

            // Left: a SCROLLABLE back-shelf of grouped item boxes — it grows as you buy more
            // stock without overflowing the panel (2026-07-23 fix).
            var scrollGo = NewRect("BottleScroll", _menuPanel);
            Place(scrollGo, new Vector2(0, 1), new Vector2(462, 500), new Vector2(14, -46));
            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 26f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = NewRect("Viewport", scrollGo);
            Stretch(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>();   // a near-clear raycast target
            vpImg.color = new Color(0, 0, 0, 0.001f);
            scroll.viewport = viewport;

            _bottleList = NewRect("Bottles", viewport);
            _bottleList.anchorMin = new Vector2(0, 1); _bottleList.anchorMax = new Vector2(1, 1);
            _bottleList.pivot = new Vector2(0.5f, 1); _bottleList.anchoredPosition = Vector2.zero;
            _bottleList.sizeDelta = Vector2.zero;
            var layout = _bottleList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f; layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childForceExpandHeight = false; layout.childControlHeight = true;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperLeft;
            _bottleList.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _bottleList;

            // Right: a side column beside the menu — what's in the shaker, then the actions.
            // The mix/serve buttons live here, out of the item grid, per the redesign.
            var side = NewRect("Side", _menuPanel);
            Place(side, new Vector2(1, 1), new Vector2(184, 500), new Vector2(-14, -46));
            side.gameObject.AddComponent<Image>().color =
                new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.55f);

            var sideTitle = NewText("SideTitle", side, _body, 11, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(sideTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(164, 16), new Vector2(0, -8));
            sideTitle.text = "IN THE SHAKER";
            _menuShaker = NewText("Shaker", side, _body, 12, TextAnchor.UpperLeft, UITheme.TextPrimary);
            Place(_menuShaker.rectTransform, new Vector2(0.5f, 1), new Vector2(164, 70), new Vector2(0, -26));
            _menuShaker.horizontalOverflow = HorizontalWrapMode.Wrap;
            _menuPreps = NewText("Preps", side, _body, 11, TextAnchor.UpperLeft, UITheme.Cyan[4]);
            Place(_menuPreps.rectTransform, new Vector2(0.5f, 1), new Vector2(164, 40), new Vector2(0, -100));
            _menuPreps.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Action buttons stacked at the bottom of the side column.
            var actions = NewRect("Actions", side);
            Place(actions, new Vector2(0.5f, 0), new Vector2(164, 208), new Vector2(0, 12));
            var actLayout = actions.gameObject.AddComponent<VerticalLayoutGroup>();
            actLayout.spacing = 8f; actLayout.childControlWidth = true;
            actLayout.childForceExpandWidth = true; actLayout.childControlHeight = true;
            actLayout.childForceExpandHeight = true;
            AddFlexButton(actions, "↔  MIX / SHAKE", UITheme.Cyan[3], OpenShakeStage);
            AddFlexButton(actions, "SERVE  →", UITheme.PrimaryAction, () =>
            {
                if (!Run.Glass.IsEmpty) GoTo(Stage.Serve);
            });
            AddFlexButton(actions, "EMPTY", UITheme.Night[3], () =>
            {
                Run.DiscardGlass(); RefreshMenu();
            });
            AddFlexButton(actions, "CLOSE", UITheme.Night[3], CloseFlow);
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
            Place(_shakerPanel, new Vector2(0.5f, 0.5f), new Vector2(720, 520), Vector2.zero);
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
            _shakerHome = new Vector2(-120, -30);
            _shakerVessel = NewRect("Shaker", _pourSurface);
            Place(_shakerVessel, new Vector2(0.5f, 0.5f), new Vector2(158, 244), _shakerHome);
            var shakerImg = _shakerVessel.gameObject.AddComponent<Image>();
            // The real steel shaker (2026-07-23). It sits in front of the fluid so the metal
            // reads solid — the falling stream shows above the mouth then vanishes into the tin.
            if (ItemArt.Shaker != null) { shakerImg.sprite = ItemArt.Shaker; shakerImg.preserveAspect = true; shakerImg.color = Color.white; }
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
                _shaking = true;
                _shakeEnergy = Run.ShakeEnergy;   // continue from what's been shaken, don't reset
                _shakerVel = Vector2.zero;
                _lastShakeMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            });
            _shakerVessel.gameObject.AddComponent<EventTrigger>().triggers.Add(shakeGrab);

            // The metaball fluid draws over the vessel (pool); the solids float on top of it;
            // the bottle and prep pieces are created after, so they sit in front of the liquid.
            _shakerFluid = new MetaballFluid(_pourSurface);
            _shakerSolids = new ShakerSolids(_pourSurface);
            _shakerSplash = new Splasher(_pourSurface);
            _shakerVessel.SetAsLastSibling();   // draw the steel shaker over the fluid (opaque tin)
            _shakerLiquidFloorY = _shakerVessel.anchoredPosition.y - _shakerVessel.rect.height * 0.5f + 12f;

            // The grabbable bottle, resting lower-right. Procedural body + neck; the grip
            // pivot sits low so lifting swings the mouth in a big arc.
            _bottleRest = new Vector2(182, -64);
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
            Place(_serveGlass, new Vector2(0.5f, 0.5f), new Vector2(150, 186), new Vector2(-120, -34));
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
            _serveSplash = new Splasher(_serveSurface);
            if (ItemArt.Glass != null) _serveGlass.SetAsLastSibling();   // clear glass over the fluid

            // The grabbable steel shaker you pour from, resting lower-right.
            _serveShakerRest = new Vector2(168, -64);
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
            Place(chip, new Vector2(0, 1), new Vector2(86, 84), new Vector2(12, -8 - index * 86));
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
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
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
