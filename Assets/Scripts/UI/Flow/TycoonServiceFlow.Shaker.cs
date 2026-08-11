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
        private BottleFill _pourFill;         // what is left in it, behind the glass
        private MetaballFluid _shakerFluid;   // the metaball liquid: pour stream + pooled body
        private ShakerSolids _shakerSolids;   // ice / lemon afloat inside the shaker
        private float _slosh;                 // running slosh phase for the shaker surface
        private Vector2 _bottleRest;
        private bool _bottleGrabbed;
        private bool _pouring;
        private const float LiftRange = 200f;  // px of lift for a full tilt
        private const float MaxTilt = 118f;    // degrees the bottle leans at full lift
        // 230 → 300 (the author, 2026-08-05: "shakera dökme sahnesinde tüm alkol
        // şişelerinin boyutunu büyüt") — the v3 masters are slimmer than the old art,
        // and at 230 a 3.7:1 bottle read as a wand. The mouth offset and the tilt
        // maths all derive from this, so the pour arc scales with it.
        private const float BottleH = 300f;
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
        /// <summary>Which held-action sound this frame wants; the stage frame plays it once,
        /// so the pour and the shake cannot silence each other (P17).</summary>
        private string _shakerLoopWanted;
        private double _shakeEnergy;
        private Vector2 _lastShakeMouse;
        private Vector2 _shakerVel;      // the shaker's spring velocity while thrown about
        private Vector2 _shakerHome;     // its rest position

        // The STIR (GDD 21 §14, 2026-08-11): the mandatory mix made Preparations.Stirred
        // load-bearing, so the bench grew a bar spoon. Stir and shake are told apart by the
        // CAP — the spoon only works an OPEN tin, the shake only a capped one — so the two
        // mixing verbs can never fight over one gesture.
        private RectTransform _spoonRt;
        private Vector2 _spoonRest;
        private bool _spoonHeld;
        private double _stirEnergy;
        private float _stirPrevAngle;
        private bool _stirHasPrev;
        /// <summary>Radians of circling over the tin for a 100% stir — about five laps.</summary>
        private const float StirFullRadians = 5f * 2f * Mathf.PI;

        // The way OUT of the bench (the author's loop rework): once the tin is capped and
        // the mix rule is satisfied, the drink moves ON to the glass instead of back
        // through the menu. Gated on Core's own CanPourOut, so the key can never walk
        // the player into the refusal.
        private Button _toGlassBtn;
        private CanvasGroup _toGlassGroup;
        private Text _toGlassLabel;
        private bool _toGlassWasOn;
        private float _toGlassPulse;

        /// <summary>The shake meter's track width; the fill derives from it, so the bar can
        /// actually reach its own end at 100%.</summary>
        private const float ShakeMeterW = 220f;
        private Image _shakeMeterFill;
        private Text _shakeMeterText;
        private const float ShakeFullTravel = 4000f;   // px of cursor travel for a full shake
        private const float ShakeStiffness = 105f;      // loose follow -> it whips around
        private const float ShakeDamping = 6f;

        // The pour gauge (2026-07-31, the author's note): WHILE pouring, a bar shows each
        // ingredient's share in its own liquid colour with the percentage inked on it — the
        // number the recipe bands grade, live, where the pouring happens.
        private RectTransform _shakerMixBar;
        private string _mixBarSig = "";

        private void RefreshShakerMixBar(TycoonRun run)
        {
            if (_shakerMixBar == null) return;
            var glass = run.Glass;
            var sig = new StringBuilder();
            foreach (var id in glass.Ingredients)
                sig.Append(id).Append((int)(glass.RatioOf(id) * 100)).Append(';');
            sig.Append((int)(glass.FillFraction * 100));
            string signature = sig.ToString();
            if (signature == _mixBarSig) return;
            _mixBarSig = signature;

            foreach (Transform child in _shakerMixBar) Destroy(child.gameObject);
            float h = _shakerMixBar.rect.height, y = 0f;
            foreach (var id in glass.Ingredients)
            {
                var card = run.Shelf.Find(id)?.Ingredient;
                float share = (float)(glass.RatioOf(id) * glass.FillFraction);   // of the VESSEL
                float segH = share * h;
                var seg = NewRect($"S_{id}", _shakerMixBar);
                seg.anchorMin = new Vector2(0, 0); seg.anchorMax = new Vector2(1, 0);
                seg.pivot = new Vector2(0.5f, 0);
                seg.sizeDelta = new Vector2(-2, segH);
                seg.anchoredPosition = new Vector2(0, y);
                var img = seg.gameObject.AddComponent<Image>();
                img.color = UITheme.LiquidColor(card?.Info?.Style, card?.Type ?? IngredientType.Spirit);
                img.raycastTarget = false;
                if (segH > 13f)
                {
                    var label = NewText("P", seg, _body, 8, TextAnchor.MiddleCenter, UITheme.InkOn(img.color));
                    Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.text = $"{share:P0} {(card?.Name ?? id).ToUpperInvariant().Split(' ')[0]}";
                }
                y += segH;
            }
            float free = Mathf.Max(0f, 1f - (float)glass.FillFraction);
            if (free > 0.001f)
            {
                var seg = NewRect("S_empty", _shakerMixBar);
                seg.anchorMin = new Vector2(0, 0); seg.anchorMax = new Vector2(1, 0);
                seg.pivot = new Vector2(0.5f, 0);
                seg.sizeDelta = new Vector2(-2, free * h);
                seg.anchoredPosition = new Vector2(0, y);
                var img = seg.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.05f);
                img.raycastTarget = false;
                if (free * h > 15f)
                {
                    var label = NewText("P", seg, _body, 8, TextAnchor.MiddleCenter, UITheme.TextSecondary);
                    Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.text = $"{free:P0} EMPTY";
                }
            }
        }

        /// <summary>The 1px neon frame both pour gauges wear.</summary>
        private void GaugeEdge(RectTransform host, Color c)
        {
            for (int i = 0; i < 4; i++)
            {
                var r = NewRect("E" + i, host);
                if (i < 2)
                {
                    r.anchorMin = new Vector2(0, i); r.anchorMax = new Vector2(1, i);
                    r.pivot = new Vector2(0.5f, i);
                    r.sizeDelta = new Vector2(0, 1);
                }
                else
                {
                    float ax = i == 2 ? 0f : 1f;
                    r.anchorMin = new Vector2(ax, 0); r.anchorMax = new Vector2(ax, 1);
                    r.pivot = new Vector2(ax, 0.5f);
                    r.sizeDelta = new Vector2(1, 0);
                }
                r.anchoredPosition = Vector2.zero;
                var img = r.gameObject.AddComponent<Image>();
                img.color = c; img.raycastTarget = false;
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

        /// <summary>The readout's ordinary voice — and it clears any warning colour left on it.</summary>
        private void SayShaker(string line)
        {
            _shakerReadout.text = line;
            _shakerReadout.color = UITheme.TextSecondary;
            _saidThisFrame = true;
        }

        /// <summary>The tin is at the brim and is refusing things. Said in red, because it is the
        /// reason nothing is happening (2026-07-28).</summary>
        private void ShowShakerFull()
        {
            _shakerReadout.text = "THE TIN IS FULL — CAP IT AND SHAKE, OR EMPTY IT";
            _shakerReadout.color = UITheme.ViceRed[3];
            _saidThisFrame = true;
        }

        /// <summary>
        /// Whether the readout has already been given something to say this frame.
        ///
        /// The stage methods run in one order every frame and the LAST one wins the readout —
        /// which was UpdateCap, whose closing nudge fires whenever the tin holds anything and
        /// is not yet capped. That is most of the stage's life, so it silently stomped the
        /// live mix line, the red "THE TIN IS FULL" refusal and the fizz refusal, every frame,
        /// before any of them could be seen. A nudge is the thing you say when there is
        /// nothing else to say; this is what lets it be that.
        /// </summary>
        private bool _saidThisFrame;

        /// <summary>Said only if nothing louder was said this frame.</summary>
        private void NudgeShaker(string line)
        {
            if (_saidThisFrame) return;
            _shakerReadout.text = line;
            _shakerReadout.color = UITheme.TextSecondary;
        }

        // ── the shaker focus stage: the tilt-pour ────────────────────────────────

        private void RefreshShaker()
        {
            var run = Run;
            if (_focusBottle == null) return;
            var colour = UITheme.StyleColor(_focusBottle.Info?.Style, _focusBottle.Type);
            _shakerTitle.text = _focusBottle.Name.ToUpperInvariant();
            SayShaker(ShakerLine(run));
            // In the hand it stands OPEN (the author, 2026-08-01): the pour scene uses the
            // capless variant when one exists. Same canvas as the closed art, so the liquid
            // mask and the mouth line all stay put; styles missing an open shot fall back.
            var bottleSprite = ItemArt.BottleOpen(_focusBottle);
            _pourBottleBody.sprite = bottleSprite;
            _pourBottleBody.color = bottleSprite != null ? Color.white : colour;   // real art, else the style tint
            PushPourFill(run);
            _pourBottle.anchoredPosition = _bottleRest;
            _pourBottle.localRotation = Quaternion.identity;
            _shakerFluid.Clear();
            _shakerFluid.ClearStreamColor();      // a new visit pours nothing yet
            _shakerFluid.SetColor(DrinkColor(run.Glass));
            _shakerVessel.anchoredPosition = _shakerHome;
            _shakerVessel.localRotation = Quaternion.identity;
            _capped = false; _capGrabbed = false; _capT = 0f;
            _spoonHeld = false; _stirEnergy = 0; _stirHasPrev = false;
            _toGlassWasOn = false; _toGlassPulse = 0f;
            if (_spoonRt != null)
            {
                _spoonRt.anchoredPosition = _spoonRest;
                _spoonRt.localRotation = Quaternion.identity;
            }
            if (_shakerOpenSize != Vector2.zero) _shakerVessel.sizeDelta = _shakerOpenSize;
            _capPos = _capRest;
            if (_shakerTop != null) { _shakerTop.anchoredPosition = _capRest; _shakerTop.localRotation = Quaternion.identity; }
            foreach (var g in _benchProps) if (g != null) g.alpha = 1f;
            PushShakerPool(run, 0f);
            _mixBarSig = "!";                 // force a redraw on stage entry
            RefreshShakerMixBar(run);
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

            // A grab already in flight must not survive the lid going on either.
            if (_capped) { _bottleGrabbed = false; return; }

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
                // Core refuses fizz in the tin (GDD 21 §12). The refusal must SPEAK here:
                // routed wrong, a carbonated bottle used to tip mutely while BeginPour threw
                // every frame — the pour looked simply broken (the author, 2026-08-02).
                bool fizzy = _focusBottle.Info != null && _focusBottle.Info.Carbonated;
                pourNow = tilt > 42f && over && !full && !fizzy;
                if (full && tilt > 42f && over) ShowShakerFull();
                else if (fizzy && tilt > 42f && over)
                    SayShaker("FIZZ DIES IN THE TIN — BUILD IT AT THE GLASS");

                if (pourNow)
                {
                    // A stream of merging droplets falls from the mouth toward the opening; the
                    // metaball field fuses them into one liquid column and melts them into the
                    // pool where they land (GDD 24 §3.5).
                    // The LIQUID's colour, on the STREAM: StyleColor is the shelf tag's identity
                    // hue (amaro navy, gin green) and pouring with it drew a drink no bottle
                    // contains, which then snapped to the true colour on the next refresh.
                    _shakerFluid.SetStreamColor(
                        UITheme.LiquidColor(_focusBottle.Info?.Style, _focusBottle.Type));
                    var streamVel = new Vector2((opening.x - mouth.x) * 1.8f, -225f);
                    _shakerFluid.EmitStream(mouth, streamVel, Time.deltaTime);
                }
            }

            if (pourNow)
            {
                if (run.PouringId == null) run.BeginPour(_focusBottle.Id);
                run.PourTick(Time.deltaTime * PourTimeScale);   // slower, deliberate pour
                // The tin's own colour, every frame it changes. RefreshShaker sets this once on
                // the way in, and on the way in the tin is EMPTY — so without this the body kept
                // DrinkColor's empty-glass cream while the stream poured pink into it (the
                // author's screenshot, 2026-08-03: a tin the gauge called 80% House Syrup drawn
                // the colour of nothing). The serve stage has always had the twin of this line.
                _shakerFluid.SetColor(DrinkColor(run.Glass));
                SayShaker(ShakerLine(run));
            }
            else if (run.PouringId != null)
            {
                run.EndPour();
                // Whatever is still falling belongs to the tin now, not to the next bottle
                // the player picks up.
                _shakerFluid.ClearStreamColor();
            }

            if (pourNow) _shakerLoopWanted = "pour_loop";   // the stage frame drives the source
            if (pourNow) RefreshShakerMixBar(run);          // the gauge follows the stream
            _pouring = pourNow;

            // Every frame, not only the pouring ones: the bottle in hand is the same bottle
            // that stands on the wall, and it drains while you hold it over the tin. Setting
            // it once on the way in would show the level it had when you picked it up.
            PushPourFill(run);
        }

        /// <summary>How full the bottle in hand is, read off the shelf it came from.</summary>
        private void PushPourFill(TycoonRun run)
        {
            if (_pourFill == null) return;
            if (_focusBottle == null) { _pourFill.Hide(); return; }
            var stock = run?.Shelf?.Find(_focusBottle.Id);
            _pourFill.Show(_pourBottleBody.sprite,
                UITheme.LiquidColor(_focusBottle.Info?.Style, _focusBottle.Type),
                stock != null && stock.Capacity > 0 ? stock.Remaining / stock.Capacity : 0.0);
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

        // (AdvanceStageOpen retired 2026-08-11: the fade-and-scale pop on the incoming
        // panel gave way to the two-slot stage SLIDE in TycoonServiceFlow — which also
        // fixes its two old defects: it ran on scaled time, and it ignored Motion.Reduced.)

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
                    if (onTin && !run.Glass.IsEmpty) { _capped = true; Sfx.Play("glass_down"); }
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

            if (!_capped && !run.Glass.IsEmpty && !_capGrabbed && !_spoonHeld)
            {
                // The mix rule speaks BEFORE the lid does: a two-spirit tin has a decision
                // to make (spoon or lid), and "close it" alone steers past the spoon.
                if (run.MixRequired && !run.IsMixed)
                    NudgeShaker("two spirits — stir it with the spoon, or cap it and shake");
                else
                    NudgeShaker("drag the lid onto the tin to close it");
            }
        }

        /// <summary>
        /// The stir (GDD 21 §14): pick the spoon up while the tin is OPEN and work circles
        /// over its mouth. Energy is the swept ANGLE around the tin's centre — a straight
        /// rattle sweeps nothing, so the shake's gesture cannot fake a stir. Release with
        /// anything behind it and the stir commits at that thoroughness.
        /// </summary>
        private void UpdateStir(TycoonRun run)
        {
            if (_spoonRt == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);

            if (!_spoonHeld)
            {
                _spoonRt.anchoredPosition = Vector2.Lerp(
                    _spoonRt.anchoredPosition, _spoonRest, 1f - Mathf.Exp(-12f * dt));
                _spoonRt.localRotation = Quaternion.Lerp(
                    _spoonRt.localRotation, Quaternion.identity, 1f - Mathf.Exp(-12f * dt));
                return;
            }

            // Capping mid-stir puts the spoon down: the two verbs never share a tin state.
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed || _capped)
            {
                if (!_capped && !run.Glass.IsEmpty && _stirEnergy > 0.05)
                {
                    run.Stir(_stirEnergy);
                    SayShaker($"STIRRED · {_stirEnergy:P0} · {ShakerLine(run)}");
                }
                _spoonHeld = false;
                _stirEnergy = 0;
                _stirHasPrev = false;
                if (_shakeMeterText != null) _shakeMeterText.text = "";
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _pourSurface, mouse.position.ReadValue(), null, out Vector2 local))
                return;

            _spoonRt.anchoredPosition = Vector2.Lerp(
                _spoonRt.anchoredPosition, local, 1f - Mathf.Exp(-30f * dt));

            // The swept angle, taken about the tin's centre and only while the spoon is
            // actually over the tin — circling the bench does not stir the drink.
            var tin = _shakerVessel;
            Vector2 arm = local - tin.anchoredPosition;
            bool overTin = Mathf.Abs(arm.x) < tin.rect.width * 0.9f
                        && Mathf.Abs(arm.y) < tin.rect.height * 0.9f
                        && arm.magnitude > 8f;
            if (overTin)
            {
                float angle = Mathf.Atan2(arm.y, arm.x);
                if (_stirHasPrev)
                {
                    float swept = Mathf.Abs(Mathf.DeltaAngle(
                        _stirPrevAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    _stirEnergy = Mathf.Clamp01((float)_stirEnergy + swept / StirFullRadians);
                    if (swept > 0.01f) _shakerLoopWanted = "stir_loop";
                }
                _stirPrevAngle = angle;
                _stirHasPrev = true;
                // the spoon leans into the work, the way the drag pieces swing
                _spoonRt.localRotation = Quaternion.Euler(0, 0,
                    Mathf.Sin(Time.unscaledTime * 9f) * 9f);
            }
            else _stirHasPrev = false;

            _shakeMeterFill.rectTransform.sizeDelta =
                new Vector2(Mathf.Round((ShakeMeterW - 4f) * (float)_stirEnergy), -4);
            _shakeMeterFill.color = Color.Lerp(UITheme.Cyan[3], UITheme.Lime[3], (float)_stirEnergy);
            if (_shakeMeterText != null) _shakeMeterText.text = $"STIR! {_stirEnergy:P0}";
            NudgeShaker(overTin ? "work circles over the tin" : "bring the spoon over the tin");
        }

        /// <summary>
        /// The right-edge key out of the bench: lit only when the tin is capped and Core
        /// itself would let the drink leave (<see cref="TycoonRun.CanPourOut"/>). It pulses
        /// once the moment it first comes alive, so the way forward announces itself.
        /// </summary>
        private void UpdateToGlass(TycoonRun run)
        {
            if (_toGlassBtn == null) return;
            bool on = _capped && !run.Glass.IsEmpty && run.CanPourOut;
            _toGlassBtn.interactable = on;
            if (_toGlassGroup != null)
                _toGlassGroup.alpha = on ? 1f : 0.4f;
            if (on && !_toGlassWasOn) _toGlassPulse = 1f;
            _toGlassWasOn = on;
            if (_toGlassPulse > 0f)
            {
                _toGlassPulse = Mathf.MoveTowards(_toGlassPulse, 0f, Time.unscaledDeltaTime / 0.35f);
                float k = 1f + 0.10f * Mathf.Sin(_toGlassPulse * Mathf.PI);
                ((RectTransform)_toGlassBtn.transform).localScale = new Vector3(k, k, 1f);
            }
            if (_capped && !run.Glass.IsEmpty && !run.CanPourOut)
                NudgeShaker("it wants a mix — shake it, or bin it and start again");
        }

        /// <summary>
        /// The mouse-energy shake (GDD 24 §2.5): while the pad is held, cursor travel builds
        /// the shake energy and the shaker jitters; releasing applies the shake at whatever
        /// energy was reached.
        /// </summary>
        private void UpdateShake(TycoonRun run)
        {
            if (_shaking) _shakerLoopWanted = "shake_loop";
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

            _shakeMeterFill.rectTransform.sizeDelta =
                new Vector2(Mathf.Round((ShakeMeterW - 4f) * (float)_shakeEnergy), -4);
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
            if (inMouth && !run.Glass.IsEmpty)
            {
                run.AddPreparation(_draggingPrep);
                SayShaker(ShakerLine(run));
                // The piece keeps its own face on the way down. It used to fall as a bare
                // colour, and the drag layer tints itself WHITE whenever the real art loaded —
                // which it always does — so ice, lemon, salt and sugar all landed in the tin as
                // identical white squares that differed only in size.
                var dragImg = _dragPiece.GetComponent<Image>();
                var c = dragImg.color;
                var face = dragImg.sprite;
                bool granular = _draggingPrep.Id == "salt_rim" || _draggingPrep.Id == "sugar_rim";
                if (granular)
                {
                    // Salt / sugar: a scatter of fine grains that fall and dissolve on the drink.
                    // Grains are too small to read a sprite, so they stay as tinted specks — and
                    // the tint has to come from the STYLE, since the drag image is white.
                    var grain = _draggingPrep.Id == "salt_rim" ? UITheme.Cream[4] : UITheme.Cream[3];
                    for (int i = 0; i < 8; i++)
                        _shakerSolids.Add(new Vector2(opening.x + UnityEngine.Random.Range(-16f, 16f), opening.y),
                            grain, UnityEngine.Random.Range(6f, 9f));
                }
                else
                {
                    // Ice / lemon: a single piece that falls and dissolves the moment it hits.
                    _shakerSolids.Add(new Vector2(opening.x + UnityEngine.Random.Range(-16f, 16f), opening.y),
                        c, _draggingPrep.Id == "ice" ? 30f : 26f, face);
                }
                _shakerFluid.Ripple(opening.x, 0.02f);   // the piece ripples the surface as it lands
            }
            _draggingPrep = null;
            _dragPiece.gameObject.SetActive(false);
        }

        private void BuildShakerPanel()
        {
            // The whole screen (P14 v2, the serve stage's recipe): the stage is the counter
            // you are standing at, not a dialog floating on it.
            _shakerPanel = NewRect("ShakerPanel", _field);
            Stretch(_shakerPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _shakerPanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_shakerPanel);

            // 16, not 18: the pixel faces only rasterise cleanly at whole multiples of their
            // 8px design size (CLAUDE.md), and the serve stage's twin title is 16 in
            // PrimaryAction. Two adjacent stages of one class had two different display sizes.
            _shakerTitle = NewText("Title", _shakerPanel, _display, 16, TextAnchor.UpperCenter, UITheme.PrimaryAction);
            Stretch(_shakerTitle.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -44), new Vector2(0, -10));

            var hint = NewText("Hint", _shakerPanel, _body, 12, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(hint.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -64), new Vector2(0, -46));
            hint.text = "GRAB THE BOTTLE TO POUR  ·  GRAB THE SHAKER TO SHAKE IT";

            // The room, the same room the serve stage stands in (2026-08-04). The two halves of
            // building one drink were being shot on different sets: serve had one-point
            // perspective with a prep table receding to the upper right and a fridge on the
            // right wall, and the shaker had a black box with a mat on the floor. Same table,
            // same wall, same horizon — so the bench and the counter read as one bar seen
            // twice rather than as two screens.
            var table = NewRect("PrepTable", _shakerPanel);
            table.anchorMin = table.anchorMax = Vector2.zero;
            table.pivot = Vector2.zero;
            table.sizeDelta = new Vector2(298f, 356f) * TableScale;
            table.anchoredPosition = TableFoot;
            var tImg = table.gameObject.AddComponent<Image>();
            tImg.sprite = ItemArt.Load("prep_table");
            tImg.preserveAspect = true;
            tImg.raycastTarget = false;
            if (tImg.sprite == null) tImg.color = new Color(0.35f, 0.36f, 0.40f, 1f);

            // The play surface: bottle and shaker live in here, mouse-local. Barely tinted —
            // the furniture carries the room, and the old half-strength slab read as a dialog
            // floating on top of it (the serve stage's lesson, applied here).
            _pourSurface = NewRect("PourSurface", _shakerPanel);
            Stretch(_pourSurface, Vector2.zero, Vector2.one, new Vector2(16, StageBottom), new Vector2(-16, -StageTop));
            var surfImg = _pourSurface.gameObject.AddComponent<Image>();
            surfImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.18f);
            surfImg.raycastTarget = false;

            // The bar mat the tin stands on (the author's note, generated with transparent
            // drainage channels so the counter shows through the ribs). Drawn at exactly 2×
            // its 400×195 pixels — integer, the prep table's lesson.
            // Centred on the two things that stand on it. It used to sit at x=-120, which put
            // its right edge at 280 while the bottle rests at 330 — so the bottle stood on
            // nothing and the mat read as a floor off to one side rather than as the counter
            // under the work.
            var matRt = NewRect("Mat", _pourSurface);
            Place(matRt, new Vector2(0.5f, 0), new Vector2(800, 390), new Vector2(60, -8));
            var matImg = matRt.gameObject.AddComponent<Image>();
            matImg.sprite = ItemArt.Load("bar_mat");
            matImg.raycastTarget = false;
            if (matImg.sprite == null) matImg.enabled = false;

            // The shaker vessel: a tapered tin, opening at the top, left of centre. Grab it to
            // shake — it becomes the toy you throw around.
            _shakerHome = new Vector2(-210, -44);
            _shakerVessel = NewRect("Shaker", _pourSurface);
            Place(_shakerVessel, new Vector2(0.5f, 0.5f), new Vector2(200, 358), _shakerHome);
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
            topImg.sprite = ItemArt.Load("shaker_cap");
            topImg.preserveAspect = true; topImg.raycastTarget = true;
            _benchProps.Clear();

            var capGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            capGrab.callback.AddListener(_ => { if (!_capped) _capGrabbed = true; });
            _shakerTop.gameObject.AddComponent<EventTrigger>().triggers.Add(capGrab);
            _shakerTop.gameObject.SetActive(topImg.sprite != null);

            _shakerSolids = new ShakerSolids(_pourSurface);
            // The metal shaker is opaque, so the fluid draws OVER it (2026-07-24): you see the
            // drink inside the tin as a cutaway, which is the point — a metal shaker you can
            // still read the level in. (A clear vessel would sit in front instead.)
            _shakerVessel.SetAsFirstSibling();
            matRt.SetAsFirstSibling();   // the mat lies UNDER the tin, not over it

            // The grabbable bottle, resting lower-right. Procedural body + neck; the grip
            // pivot sits low so lifting swings the mouth in a big arc.
            _bottleRest = new Vector2(330, -70);
            _pourBottle = NewRect("Bottle", _pourSurface);
            _pourBottle.pivot = new Vector2(0.5f, 0.22f);
            _pourBottle.sizeDelta = new Vector2(180, BottleH);
            _pourBottle.anchoredPosition = _bottleRest;
            // The art is a CHILD of the grab rect, which is itself an invisible hit plate:
            // a bottle is a narrow silhouette and the grab has to be the whole slot.
            //
            // The drink rides BEHIND the art, cut out by it (2026-08-11, the author:
            // "hepsinde ne kadar miktarı kaldıysa o kadar doluluk olmalı"). The bar of
            // colour that used to run down either side of this bottle is exactly what the
            // stencil removes: the grab rect is a fixed 180 wide and the art is
            // letterboxed inside it, so a plain rectangle of drink had nothing to stop it
            // at the glass. See BottleFill.
            var hitBottle = _pourBottle.gameObject.AddComponent<Image>();
            hitBottle.color = new Color(0, 0, 0, 0.001f);   // invisible, still grabbable

            _pourFill = BottleFill.Under(_pourBottle);

            var pourArt = NewRect("Body", _pourBottle);
            Stretch(pourArt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _pourBottleBody = pourArt.gameObject.AddComponent<Image>();
            _pourBottleBody.preserveAspect = true;    // the real bottle art, set per focus in RefreshShaker
            _pourBottleBody.color = UITheme.Cyan[3];
            _pourBottleBody.raycastTarget = false;
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
                // Capping puts the bench away by fading the props — but a CanvasGroup's alpha
                // does not stop raycasts, so the faded bottle stayed fully clickable and a
                // sealed, shaken tin could be topped up from a bottle nobody could see. The
                // prep chips have always guarded this; the bottle never did.
                if (_capped) return;
                if (_focusBottle != null && Run != null && Run.Phase == TycoonPhase.DayOpen)
                    _bottleGrabbed = true;
            });
            _pourBottle.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);
            _benchProps.Add(_pourBottle.gameObject.AddComponent<CanvasGroup>());

            // The prep sources STAND ON the table, on the line measured off its art, nearest
            // first and shrinking as they recede — the serve stage's own placement, called
            // through the same two helpers, so the ice bucket on this bench is the same ice
            // bucket in the same place as the one on that counter.
            // THE FOUR LEFT THE BENCH (2026-08-10, the author). Ice, a twist, a salt rim
            // and a sugar rim are finished AT THE GLASS — that is where a bartender puts
            // them and where AddPreparationAtGlass has always applied them. On the tin they
            // were a second door to the same act, and the one that could refuse you.

            // The single piece that follows the mouse while a prep is held. Its pivot is at the
            // top (the grip), so it hangs below the cursor and swings about that point.
            _dragPiece = NewRect("DragPiece", _pourSurface);
            _dragPiece.pivot = new Vector2(0.5f, 1f);
            _dragPiece.sizeDelta = new Vector2(64, 72);   // in scale with the buckets it leaves
            var dragImg = _dragPiece.gameObject.AddComponent<Image>();
            dragImg.raycastTarget = false; dragImg.preserveAspect = true;   // the real prep piece
            _dragPieceLabel = NewText("L", _dragPiece, _body, 10, TextAnchor.LowerCenter, UITheme.Night[0]);
            Stretch(_dragPieceLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 2), new Vector2(0, -2));
            _dragPiece.gameObject.SetActive(false);

            _shakerReadout = NewText("Readout", _shakerPanel, _body, 13, TextAnchor.LowerCenter, UITheme.TextSecondary);
            Stretch(_shakerReadout.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(16, 92), new Vector2(-16, 118));

            // The pour gauge: a recessed track under the readout, filled live per ingredient.
            // VERTICAL and engine-drawn (the author, 2026-08-02: the generated tube read
            // as the wrong decade): a slim standing column, cyan-edged, filled bottom-up
            // with the TIN's contents as shares of the whole vessel — 5% of vodka reads
            // 5% VODKA and the room above it reads EMPTY.
            // Off the bench and against the right wall, the way the serve stage's twin stands.
            // At x=-340 it hung in the middle of the room, over the prep table's far end and
            // through the shaker's cap — a gauge drawn across the props it is reporting on.
            var mixTrack = NewRect("MixTrack", _shakerPanel);
            Place(mixTrack, new Vector2(0.5f, 0.5f), new Vector2(44, 330), new Vector2(520, -10));
            var trackBg = mixTrack.gameObject.AddComponent<Image>();
            trackBg.color = new Color(0.05f, 0.05f, 0.09f, 0.88f);
            trackBg.raycastTarget = false;
            GaugeEdge(mixTrack, new Color(UITheme.Cyan[3].r, UITheme.Cyan[3].g, UITheme.Cyan[3].b, 0.7f));
            _shakerMixBar = NewRect("MixSegs", mixTrack);
            Stretch(_shakerMixBar, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));

            // The shake meter, above the bottom bar. Its usable width is derived, not typed:
            // the fill was hardcoded to 200px inside a 216px track, so a shake the caption
            // called 100% left the bar visibly short of its own end.
            var meterBg = NewRect("ShakeMeterBg", _shakerPanel);
            Place(meterBg, new Vector2(0.5f, 0), new Vector2(ShakeMeterW, 14), new Vector2(0, 70));
            meterBg.gameObject.AddComponent<Image>().color = UITheme.Night[0];
            var meterFill = NewRect("ShakeMeterFill", meterBg);
            meterFill.anchorMin = new Vector2(0, 0); meterFill.anchorMax = new Vector2(0, 1);
            meterFill.pivot = new Vector2(0, 0.5f); meterFill.offsetMin = new Vector2(2, 2);
            meterFill.offsetMax = new Vector2(2, -2); meterFill.anchoredPosition = new Vector2(2, 0);
            _shakeMeterFill = meterFill.gameObject.AddComponent<Image>();
            _shakeMeterFill.raycastTarget = false;
            _shakeMeterText = NewText("ShakeText", _shakerPanel, _body, 11, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Place(_shakeMeterText.rectTransform, new Vector2(0.5f, 0), new Vector2(240, 16), new Vector2(0, 86));

            // (The bottom-left plate that read "↔ GRAB THE SHAKER · SHAKE IT" is gone. It was
            // a caption wearing a button's clothes — same dark plate, same size, sitting beside
            // the real DONE key with no handler on it — and it was the third surface telling
            // the player the same thing the header hint already says. The tin says "grab me" by
            // answering the pointer now.)

            // THE BAR SPOON (GDD 21 §14, 2026-08-11): the stir's instrument, resting by the
            // tin. Drawn, not generated — it is an instrument the pointer works, and at this
            // size a rod and a bowl in the bench's own steel read truer than any take.
            _spoonRest = new Vector2(-195f, -110f);   // leaning by the table's near end
            _spoonRt = NewRect("BarSpoon", _pourSurface);
            _spoonRt.pivot = new Vector2(0.5f, 1f);        // held by the grip, bowl hangs down
            _spoonRt.sizeDelta = new Vector2(26, 118);
            _spoonRt.anchoredPosition = _spoonRest;
            var spoonHit = _spoonRt.gameObject.AddComponent<Image>();
            spoonHit.color = new Color(0, 0, 0, 0.001f);   // the whole slot answers the hand
            var rod = NewRect("Rod", _spoonRt);
            rod.anchorMin = rod.anchorMax = new Vector2(0.5f, 1f);
            rod.pivot = new Vector2(0.5f, 1f);
            rod.sizeDelta = new Vector2(5, 96);
            rod.anchoredPosition = Vector2.zero;
            var rodImg = rod.gameObject.AddComponent<Image>();
            rodImg.color = new Color(0.72f, 0.75f, 0.80f, 1f);
            rodImg.raycastTarget = false;
            var twist = NewRect("Twist", _spoonRt);        // the twisted shaft's glint
            twist.anchorMin = twist.anchorMax = new Vector2(0.5f, 1f);
            twist.pivot = new Vector2(0.5f, 1f);
            twist.sizeDelta = new Vector2(2, 84);
            twist.anchoredPosition = new Vector2(-1, -6);
            var twistImg = twist.gameObject.AddComponent<Image>();
            twistImg.color = new Color(0.92f, 0.94f, 0.97f, 0.85f);
            twistImg.raycastTarget = false;
            var bowl = NewRect("Bowl", _spoonRt);
            bowl.anchorMin = bowl.anchorMax = new Vector2(0.5f, 0f);
            bowl.pivot = new Vector2(0.5f, 0f);
            bowl.sizeDelta = new Vector2(16, 24);
            bowl.anchoredPosition = new Vector2(0, 0);
            var bowlImg = bowl.gameObject.AddComponent<Image>();
            bowlImg.color = new Color(0.62f, 0.66f, 0.72f, 1f);
            bowlImg.raycastTarget = false;
            var spoonGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            spoonGrab.callback.AddListener(_ =>
            {
                // The spoon works an OPEN tin only — the cap hands the stage to the shake.
                if (!_capped && Run != null && Run.Phase == TycoonPhase.DayOpen)
                { _spoonHeld = true; _stirHasPrev = false; }
            });
            _spoonRt.gameObject.AddComponent<EventTrigger>().triggers.Add(spoonGrab);
            _benchProps.Add(_spoonRt.gameObject.AddComponent<CanvasGroup>());

            // THE WAY FORWARD (the author's loop rework): the drink moves ON to the glass
            // from here. Right edge centre — the mirror of where the back key will stand —
            // and lit only when Core itself would let the drink leave.
            var toGlass = NewRect("ToGlass", _shakerPanel);
            Place(toGlass, new Vector2(1f, 0.5f), new Vector2(76, 150), new Vector2(-14, 0));
            var tgImg = toGlass.gameObject.AddComponent<Image>();
            tgImg.color = UITheme.PrimaryAction;
            _toGlassBtn = toGlass.gameObject.AddComponent<Button>();
            _toGlassBtn.targetGraphic = tgImg;
            _toGlassBtn.onClick.AddListener(() => GoTo(Stage.Serve));
            _toGlassGroup = toGlass.gameObject.AddComponent<CanvasGroup>();
            _toGlassLabel = NewText("L", toGlass, _body, 12, TextAnchor.MiddleCenter, Color.black);
            Stretch(_toGlassLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, 4), new Vector2(-4, -4));
            _toGlassLabel.text = "TO\nTHE\nGLASS";
            var tgSink = toGlass.gameObject.AddComponent<PressSink>();
            tgSink.Face = toGlass; tgSink.Depth = 3f; tgSink.Lift = 2f;

            // The way back wears the LEFT edge now (the loop rework): one key, one place,
            // every station — the bottom DONE plate retired with the hub-and-spoke cuts.
            AddEdgeBack(_shakerPanel);
        }

    }
}
