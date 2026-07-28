using System;
using System.Collections.Generic;
using System.Text;
using LastCall.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The shaker stage (GDD 24 §2): tip a bottle into the open tin, drop ice and
    /// garnishes in by hand, cap it, then grab it and shake. The liquid is a real
    /// particle body, so it pours, pools and sloshes.
    /// </summary>
    public sealed partial class TycoonServiceFlow
    {

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

        /// <summary>The readout's ordinary voice — and it clears any warning colour left on it.</summary>
        private void SayShaker(string line)
        {
            _shakerReadout.text = line;
            _shakerReadout.color = UITheme.TextSecondary;
        }

        /// <summary>The tin is at the brim and is refusing things. Said in red, because it is the
        /// reason nothing is happening (2026-07-28).</summary>
        private void ShowShakerFull()
        {
            _shakerReadout.text = "THE TIN IS FULL — CAP IT AND SHAKE, OR EMPTY IT";
            _shakerReadout.color = UITheme.ViceRed[3];
        }

        // ── the shaker focus stage: the tilt-pour ────────────────────────────────

        private void RefreshShaker()
        {
            var run = Run;
            if (_focusBottle == null) return;
            var colour = UITheme.StyleColor(_focusBottle.Info?.Style, _focusBottle.Type);
            _shakerTitle.text = _focusBottle.Name.ToUpperInvariant();
            SayShaker(ShakerLine(run));
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
                // A full tin takes nothing more, so the stream stops with it: liquid pouring into
                // a glass that cannot accept it read as an overflow the rules do not have
                // (GDD 21 §3, 2026-07-28). The bottle stays in hand — only the pour ends.
                bool full = run.Glass.IsFull;
                pourNow = tilt > 42f && over && !full;
                if (full && tilt > 42f && over) ShowShakerFull();

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
                SayShaker(ShakerLine(run));
            }
            else if (run.PouringId != null)
            {
                run.EndPour();
            }

            _pouring = pourNow;
        }

        /// <summary>
        /// Places the drink and steps it, once every vessel has finished moving for the frame.
        /// It used to run inside the tilt-pour, which is BEFORE the cap animation eases the tin
        /// across the bench and grows it — so the liquid was placed against last frame's tin and
        /// trailed it visibly wherever it moved (2026-07-28). It also sat behind that method's
        /// early return, which meant a stage with no mouse present simply froze the drink.
        /// </summary>
        private void StepShakerFluid(TycoonRun run)
        {
            // A gentle vertical heave on the pool top; the height-field carries the real waves.
            float energy = _shaking ? 1f + 3f * (float)_shakeEnergy : (_pouring ? 1.2f : 0.3f);
            _slosh += Time.deltaTime * (4f + 6f * energy);
            PushShakerPool(run, Mathf.Sin(_slosh) * 1.0f * energy);

            _shakerFluid.Step(Time.deltaTime);
            _shakerSolids.Step(Time.deltaTime);
            _shakerSplash.Step(Time.deltaTime);
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
            float h = _shakerVessel.rect.height;
            float innerH = h * (CavityRim - CavityFloor);   // measured: that floor → rim

            // The cavity's centre sits well BELOW the tin's own pivot — the drinkable part runs
            // from 0.09 to 0.61 of the sprite, so its middle is about a seventh of the height
            // down from the middle of the art. The sprite turns about its pivot and the pool
            // turns about its own centre, so unless that centre is carried round the pivot by
            // hand the two swing apart the moment the tin leans: at the 24° a shake reaches,
            // by nearly twenty pixels — the liquid visibly leaving the steel (2026-07-28).
            // The tap already does this for the leaning pint; the shaker never did.
            float rad = _shakerVessel.localEulerAngles.z * Mathf.Deg2Rad;
            if (rad > Mathf.PI) rad -= 2f * Mathf.PI;
            var centre = RotateAbout(new Vector2(c.x, c.y - h * 0.5f + h * CavityFloor + innerH * 0.5f), c, rad);
            float minX = centre.x - iw;
            float maxX = centre.x + iw;
            float bottomY = centre.y - innerH * 0.5f;   // measured: above the rounded base
            // A full tin draws full. The ninth this used to shave off was a fudge for the
            // solver's particle-count estimate, and it made a glass the rules called 100% read
            // as nine-tenths — the one number the player checks against the vessel (2026-07-28).
            // The estimate is fixed where it belongs now, in the solver itself; measured after:
            // a tin the rules call 100% draws to 100% of its cavity.
            float fill = (float)run.Glass.FillFraction;
            float rimY = bottomY + innerH;
            float topY = bottomY + innerH * fill + bob;
            // The particle fluid collides with the tin's rotated interior, so it sloshes with it.
            _shakerFluid.SetPool(minX, maxX, bottomY, rimY, fill, rad);
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
                SayShaker("drag the lid onto the tin to close it");
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
                    SayShaker($"SHAKEN · {_shakeEnergy:P0} · {ShakerLine(run)}");
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
            // A tin filled to the brim has no room for a cube of ice or a twist of lemon, so the
            // piece falls away instead of going in — the rules refuse it either way, and dropping
            // it in silently would just look broken (2026-07-28).
            if (inMouth && !run.Glass.IsEmpty && run.Glass.IsFull) ShowShakerFull();
            else if (inMouth && !run.Glass.IsEmpty)
            {
                run.AddPreparation(_draggingPrep);
                SayShaker(ShakerLine(run));
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
                if (Run == null || Run.Glass.IsEmpty) { SayShaker("pour something to shake"); return; }
                if (!_capped) { SayShaker("cap it first — drag the lid onto the tin"); return; }
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
                if (Run == null || Run.Glass.IsEmpty) { SayShaker("pour something first"); return; }
                if (!Run.CanAddPreparation) { ShowShakerFull(); return; }   // no room for it
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
    }
}
