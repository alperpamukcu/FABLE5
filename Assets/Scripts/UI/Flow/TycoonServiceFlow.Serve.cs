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
    /// The serve stage (GDD 24 §3), rebuilt 2026-08-13: grab the shaker and tip it over
    /// the glass — how well the mouth lines up is the aim, off-centre spills, and what
    /// spills is gone.
    ///
    /// THE FRIDGE IS RETIRED AND NOTHING REPLACED IT. No drink is chosen on this stage
    /// (the author: "bardağa dökme aşamasında herhangi bir sıvı koyulmayacak, ondan
    /// dolayı o sahnede içeceklerin olmasına gerek yok") — the bar has one place where a
    /// bottle is picked up and it is the back bar. What stands here is the glass, the tin
    /// that fills it, and the finishing table. NO BOTTLE AT ALL, since 2026-08-14: the one
    /// exception was the fizz the wall handed over, because Core refused it in the tin, and
    /// GDD 21 §12 was overturned that day — every drink is built in the tin now, so the
    /// bottle-in-hand this stage kept for carbonated has gone with the rule that needed it.
    /// </summary>
    public sealed partial class TycoonServiceFlow
    {

        // The serve pour uses the same tilt model (GDD 24 §3): grab the shaker, tip it over
        // the glass. How well the mouth lines up over the glass is the aim — off-centre spills.
        private Text _serveShakerText;
        private readonly List<(Image icon, Text label, Image tick)> _serveStepRows =
            new List<(Image, Text, Image)>();
        private Text _serveGlassText;
        private RectTransform _serveSurface;
        private RectTransform _serveGlass;      // the target

        /// <summary>How tall the serving glass is drawn; the width follows the drawing, so the
        /// five vessels differ by silhouette rather than all being stretched into one box.</summary>
        private const float ServeGlassHeight = 260f;
        private Image _serveGlassImage;
        private LastCall.Core.GlasswareDefinition _serveGlassware;
        private int _serveGlassTier = 1;
        private RectTransform _serveGlassBackRt;
        private RectTransform _serveGlassShadow;
        private Image _serveGlassBack;
        private RectTransform _serveMixBar;
        private string _serveMixSig = "";
        private GlassArt.Piece _serveGlassPiece;

        // ── the first-person staging (v5 P14, the author's diagram of 2026-07-31) ──
        //
        // One-point perspective, the room seen from where the bartender stands: the wall
        // across the back, and THE COUNTER — the panel itself — running under everything
        // else. There is no furniture drawn on it (2026-08-13): a table sprite was a
        // second surface inside the first, so the props had to stand on a picture of a
        // counter that sat on top of the counter. They stand on the counter now.

        /// <summary>Room kept clear at the top for the title and the aim line, and at the bottom
        /// for the two buttons.</summary>
        private const float StageTop = 86f, StageBottom = 62f;

        /// <summary>How tall the shaker is drawn on THIS stage. The shaker bench's 180 left
        /// the tin looking like a thimble beside a 260-tall glass.</summary>
        /// <summary>The tin bench's own 358 since the two benches agreed on one tin
        /// (2026-08-26); the pour maths reads the mouth off this, so it moves with it.</summary>
        private const float ServeVesselH = 358f;

        private RectTransform _serveShaker;     // the grabbable shaker
        private Image _serveShakerBody;
        private MetaballFluid _serveFluid;      // the metaball liquid in the serving glass

        /// <summary>Set by whichever pour path ran this frame; the stage frame drives the
        /// one loop source from it, so the tin and the bottle cannot stop each other's sound.</summary>
        private bool _servePouringNow;

        private Text _aimText;
        private Vector2 _serveShakerRest;
        private bool _serveGrabbed;
        private const float ServePourRate = 0.34f;   // glass-fractions per second (slower, 2026-07-22)

        // The way out: SERVE only means something once a drink stands in the glass, so
        // the key dims until one does — the ToGlass key's own law, applied here.
        private Button _serveDoneBtn;
        private CanvasGroup _serveDoneGroup;

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
            _serveFluid.Clear();
            _serveFluid.ClearStreamColor();       // nothing is in the air on the way in
            // The pool is the drink IN THE GLASS, not the one in the shaker. Those are the same
            // thing while you are tipping one into the other, which is why nobody noticed — but
            // a built drink leaves the shaker empty, and the pool was taking the colour of
            // nothing and drawing a soda as pale tan.
            _serveFluid.SetColor(DrinkColor(run.ServingGlass.IsEmpty ? run.Glass : run.ServingGlass));
            ShowServingGlassware(run);
            RefreshServeMixBar(run);
            PushServePool(run);
            GlassDecor.Sync(_serveGlass, _serveGlassPiece, run.ServingGlass, run);
            // Steel is steel whatever is in it. This used to multiply the tin sprite by the
            // drink's colour AND by its alpha — harmless while that alpha was a fixed 0.9, and
            // not harmless at all once it became the fill-derived 0.52-0.86: the serve stage's
            // shaker turned into a see-through, drink-tinted tin. The hand bottle three methods
            // away and the tap's keg both guard this the same way.
            _serveShakerBody.color = _serveShakerBody.sprite != null
                ? Color.white : DrinkColor(run.Glass);
            _serveShaker.gameObject.SetActive(!run.Glass.IsEmpty);
            _aimText.text = run.Glass.IsEmpty
                ? "NOTHING IN THE TIN · PICK A BOTTLE AT THE BACK BAR"
                : "GRAB THE SHAKER · TIP IT OVER THE GLASS";
            _aimText.color = UITheme.TextSecondary;


            // NO BOTTLES STAND ON THIS COUNTER, and none is carried in either (2026-08-14):
            // the tin arrives with the whole drink in it.
        }

        /// <summary>
        /// THE BENCH GOES BACK TO NORMAL when the drink leaves it (2026-08-25, the author:
        /// "servis et dedikten sonra tezgah normal haline gelmeli"): the tin may not stay
        /// in the hand. It reset three other hands too — a dish mid-lap, a piece mid-drag,
        /// the progress ring — until the finishing table left this bench for the room's
        /// counter (2026-08-26) and took all three with it.
        /// </summary>
        private void ResetServeHand()
        {
            _serveGrabbed = false;
        }

        /// <summary>The SERVE key answers only a glass with a drink in it — dim until then.
        /// Driven every frame from the stage's own update, now that the cabinet it used to
        /// ride on is gone.</summary>
        private void PushServeDone(TycoonRun run)
        {
            if (_serveDoneGroup == null || run == null) return;
            bool ready = !run.ServingGlass.IsEmpty;
            _serveDoneGroup.alpha = ready ? 1f : 0.45f;
            if (_serveDoneBtn != null) _serveDoneBtn.interactable = ready;
        }

        /// <summary>
        /// One frame of the serve pour (GDD 24 §3): the shaker tips the same way the bottle
        /// did. How well the mouth lines up over the glass is the aim — dead over the glass
        /// pours clean, drifting off spills, and a full pour still drains the shaker.
        /// </summary>
        /// <summary>
        /// The counter's card, read off the same state the counter itself obeys: the glass
        /// is empty until the tin is tipped, the dressing is optional, and the last step is
        /// a walk. It has no cursor of its own to disagree with — it asks the run.
        /// </summary>
        private void UpdateServeStepCard(TycoonRun run)
        {
            if (_serveStepRows.Count == 0) return;
            bool poured = !run.ServingGlass.IsEmpty;
            // Two rows since the dressing moved to the room (2026-08-26): no optional
            // middle step any more, so no row is exempt from the ladder.
            PaintSteps(_serveStepRows, poured ? 1 : 0, -1, false);
        }

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
                Vector2 mouth = local + new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (ServeVesselH * 0.78f);
                var opening = _serveGlass.anchoredPosition + new Vector2(0, _serveGlass.rect.height * 0.5f);

                // The glass is full: the pour stops there rather than running a stream into a
                // vessel that cannot take it (GDD 21 §3, 2026-07-28).
                if (run.ServingGlass.IsFull && tilt > 42f && mouth.y > opening.y - 30f)
                {
                    _serveGrabbed = false;
                    ShowGlassFull();
                }
                else if (tilt > 42f && mouth.y > opening.y - 30f && !run.CanPourOut)
                {
                    // THE MANDATORY MIX (GDD 21 §14): two spirits may not leave the tin
                    // unmixed. Core refuses in PourIntoServingGlass — which this stage
                    // calls per frame — so the UI reads the predicate and stops the
                    // stream the way CanPull greys the keg key, instead of catching an
                    // exception forty times a second.
                    _serveGrabbed = false;
                    _serveShaker.localRotation = Quaternion.identity;
                    if (_aimText != null)
                        _aimText.text = "THIS ONE NEEDS MIXING — BACK TO THE SHAKER";
                }
                else if (tilt > 42f && mouth.y > opening.y - 30f)
                {
                    // Aim: how well the mouth is centred over the glass. Within ~half the
                    // glass width is a clean pour; beyond that it spills more the further off.
                    accuracy = Mathf.Clamp01(1f - Mathf.Abs(mouth.x - opening.x) / 90f);
                    pourNow = true;

                    // The stream falls toward where the aim sends it: dead-on it drops into the
                    // glass and melts into the drink; off-aim it drifts wide and misses the rim,
                    // falling past onto the counter — the spill you can see (GDD 24 §3).
                    // What comes out of the TIN is the shaker's own drink, which is a different
                    // liquid from what is already standing in the glass; it goes on the stream.
                    _serveFluid.SetStreamColor(DrinkColor(run.Glass));
                    float landX = Mathf.Lerp(mouth.x + (mouth.x - opening.x) * 1.5f, opening.x, (float)accuracy);
                    var streamVel = new Vector2((landX - mouth.x) * 1.8f, -225f);
                    _serveFluid.EmitStream(mouth, streamVel, Time.deltaTime);
                }
            }

            if (pourNow)
            {
                _servePouringNow = true;
                double before = run.ServingGlass.TotalVolume;
                run.PourIntoServingGlass(ServePourRate * Time.deltaTime, accuracy);
                // The GLASS's colour as the tin goes into it. Only the refresh set this, and the
                // refresh reads the tin when the glass is empty — so tipping a shaken drink into
                // a glass that already held something left the pool at the old drink's colour
                // for the whole pour. The twin of the shaker bench's line.
                _serveFluid.SetColor(DrinkColor(run.ServingGlass));
                if (run.ServingGlass.IsFull) { PutTheShakerDown(run); ShowGlassFull(); }
                else if (run.Glass.IsEmpty) PutTheShakerDown(run);
                else if (run.ServingGlass.TotalVolume != before) RefreshServeText(run, accuracy);
            }

            // The vessel is chosen by the first drop out of the shaker, so the glass on the
            // counter can change in the middle of this stage. Checked every frame; it costs a
            // reference compare until the day it actually changes.
            ShowServingGlassware(run);
            PushServePool(run);
            _serveFluid.Step(Time.deltaTime);
        }

        /// <summary>
        /// Puts the glass the drink actually chose on the counter (v5 P14 / C9). Rebuilt only
        /// when the vessel changes, because the sprite is drawn once and kept.
        /// </summary>
        private void ShowServingGlassware(TycoonRun run)
        {
            var glassware = run.ServingGlassware;
            int tier = run.GlassTier(glassware?.Id);
            if (ReferenceEquals(glassware, _serveGlassware) && tier == _serveGlassTier
                && _serveGlassImage.sprite != null) return;
            _serveGlassware = glassware;
            _serveGlassTier = tier;

            var piece = GlassArt.For(glassware, tier);
            _serveGlassPiece = piece;
            // Modular when the art is: the glass image is the clear FRONT face, and the
            // back face mirrors it under the pooled drink.
            _serveGlassImage.sprite = piece.Front != null ? piece.Front : piece.Sprite;
            _serveGlassImage.preserveAspect = true;
            _serveGlassImage.color = Color.white;
            if (_serveGlassBack != null)
            {
                _serveGlassBack.sprite = piece.Back;
                _serveGlassBack.enabled = piece.Back != null;
                _serveGlassBack.preserveAspect = true;
            }
            // Height is fixed and width follows the drawing, so a coupe is wide and a highball
            // narrow at the same place on the counter instead of all five being stretched into
            // one box.
            _serveGlass.sizeDelta = new Vector2(ServeGlassHeight * piece.Aspect, ServeGlassHeight);
            if (_serveGlassBackRt != null)
            {
                _serveGlassBackRt.sizeDelta = _serveGlass.sizeDelta;
                _serveGlassBackRt.anchoredPosition = _serveGlass.anchoredPosition;
            }
            if (_serveGlassShadow != null)
            {
                // A shadow the width of the FOOT, not of the widest point: a coupe's bowl
                // hangs over its base, and a shadow drawn to the bowl reads as a puddle.
                float foot = _serveGlass.sizeDelta.x * (piece.Profile != null && piece.Profile.Length > 0
                    ? Mathf.Max(0.35f, piece.Profile[0]) : 0.8f);
                _serveGlassShadow.sizeDelta = new Vector2(foot, Mathf.Max(10f, foot * 0.22f));
                _serveGlassShadow.anchoredPosition = new Vector2(_serveGlass.anchoredPosition.x,
                    _serveGlass.anchoredPosition.y - ServeGlassHeight * 0.5f + 8f);
            }
            _serveFluid.SetProfile(piece.Profile);
            _serveFluid.SetDensity(piece.Density);   // measured per vessel, not one number for all
        }

        /// <summary>Places the serving glass's pooled liquid from its interior and live fill.
        /// The interior is <b>reported by the drawing</b> (v5 P14) rather than tuned by hand:
        /// three magic fractions used to say where the drink went in one particular tumbler,
        /// and five glasses would have been fifteen of them.</summary>
        private void PushServePool(TycoonRun run)
        {
            if (run.ServingGlass.IsEmpty) { _serveFluid.ClearPool(); return; }
            var piece = _serveGlassPiece;
            var c = _serveGlass.anchoredPosition;
            float w = _serveGlass.rect.width, h = _serveGlass.rect.height;
            // The box is FLUSH with the measured cavity — the metaball surface is built
            // to touch its box, so contact needs no overshoot (the seam of 2026-08-02
            // came from an INSET box; the spills came from overshooting). The ceiling
            // sits 3 art px BELOW the cavity top: the surface is a bumpy band, not a
            // line, and those bumps must crest inside the mouth, not over the lip.
            // ...less HALF an art pixel a side: the field's edge smoothing bleeds that
            // far past the box (the author, 2026-08-02: "çok çok az taşma kaldı").
            float artPx = piece.Sprite != null ? w / piece.Sprite.rect.width : 1.5f;
            float iw = w * 0.5f * piece.InteriorHalf - 0.5f * artPx;
            float floor = c.y - h * 0.5f + h * piece.FloorY;
            float rim = c.y - h * 0.5f + h * piece.RimY - GlassArt.PoolCeilingArtPx * artPx;
            _serveFluid.SetPool(c.x - iw, c.x + iw, floor, rim, (float)run.ServingGlass.FillFraction);
        }

        /// <summary>
        /// Ends a pour by PUTTING THE SHAKER DOWN, rather than letting go of it wherever the
        /// cursor happened to be (2026-07-31 bug report: "the shaker freezes and cannot be
        /// moved when the drink is poured, or when what is inside runs out").
        ///
        /// Dropping the grab was all this used to do, which left the tin hanging in mid-air at
        /// whatever angle it was tipped to — and if it had just run dry, the next refresh
        /// deactivated it exactly where it hung. What the player saw was a shaker frozen at an
        /// angle that no longer answered the pointer. A tin you have finished with goes back on
        /// the bench, upright, and is only taken off the stage once it is standing there.
        /// </summary>
        private void PutTheShakerDown(TycoonRun run)
        {
            _serveGrabbed = false;
            _serveFluid.ClearStreamColor();   // the tin has stopped; the air belongs to the glass
            _serveShaker.anchoredPosition = _serveShakerRest;
            _serveShaker.localRotation = Quaternion.identity;
            _serveShaker.gameObject.SetActive(!run.Glass.IsEmpty);
            RefreshServeText(run, 1.0);
            if (run.Glass.IsEmpty)
            {
                _aimText.text = "SHAKER EMPTY — FINISH IT AND SERVE";
                _aimText.color = UITheme.TextSecondary;
            }
        }

        /// <summary>The glass is at the brim and is refusing what comes next — the drink stops
        /// there, and so does the garnish (2026-07-28).</summary>
        private void ShowGlassFull()
        {
            var run = Run;
            if (run != null)
            {
                _serveShakerText.text = run.Glass.IsEmpty
                    ? "shaker empty" : $"shaker {run.Glass.FillFraction:P0} left";
                _serveGlassText.text = "glass FULL";
            }
            _aimText.text = "THE GLASS IS FULL — SERVE IT";
            _aimText.color = UITheme.Amber[3];
        }

        /// <summary>The glass's shares in their liquid colours, the headroom as a pale
        /// tail — the serve-side twin of the shaker's tube.</summary>
        private void RefreshServeMixBar(TycoonRun run)
        {
            if (_serveMixBar == null) return;
            var glass = run.ServingGlass;
            var sig = new StringBuilder();
            foreach (var id in glass.Ingredients)
                sig.Append(id).Append((int)(glass.RatioOf(id) * 100)).Append(';');
            sig.Append((int)(glass.FillFraction * 100));
            string signature = sig.ToString();
            if (signature == _serveMixSig) return;
            _serveMixSig = signature;

            // The glass's column, captioned beside it — the same drawing the tin's gauge
            // uses, and for the same reason: 44 units of width cannot hold a word.
            FillGauge(_serveMixBar, glass, run, labelsLeft: true);
        }

        private void RefreshServeText(TycoonRun run, double accuracy)
        {
            _serveShakerText.text = $"shaker {run.Glass.FillFraction:P0} left";
            _serveGlassText.text = $"glass {run.ServingGlass.FillFraction:P0} full";
            GlassDecor.Sync(_serveGlass, _serveGlassPiece, run.ServingGlass, run);
            RefreshServeMixBar(run);
            _aimText.text = accuracy > 0.8 ? "CLEAN POUR" : accuracy > 0.4 ? "SOME SPILL" : "SPILLING!";
            _aimText.color = Color.Lerp(UITheme.ViceRed[3], UITheme.Lime[3], (float)accuracy);
        }

        private void BuildServePanel()
        {
            // The whole screen, not a panel floating on it: the stage is the counter you are
            // standing at, which is what lets the props be props instead of icons on keys.
            _servePanel = NewRect("ServePanel", _field);
            Stretch(_servePanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // NO DARKENING (2026-08-22, the author: "Shaker ve pour sahnelerinde karartma
            // olmasın"). This was an opaque page, then a scrim over the room; it is neither
            // now. The room behind the bench is the bar you are standing in and it keeps its
            // own light. The plate is still HERE and still raycasts — that is the whole job
            // it has left: you can see past the bench, you cannot reach past it.
            var block = _servePanel.gameObject.AddComponent<Image>();
            block.color = new Color(0f, 0f, 0f, 0f);
            Swallow(_servePanel);

            // (No title and no name plate on this bench since 2026-08-26: the step
            //  card's first row says TIP THE TIN, the aim line coaches the pour, and a
            //  plate that only ever said POUR THE GLASS was saying what both already say
            //  — from the middle of the mix gauge's column.)

            // The two corner readouts wear the top corners at 8px; the aim line gets its own
            // full-width band UNDER them at 16 — they used to share one band and long aim
            // strings ran under the corner numbers.
            // THE SAME CARD THE BENCH WEARS (2026-08-14, the author: "aynı öğreticiyi
            // bardağa koyma sahnesi içinde oluştur"). Top-left, ahead of the tin's line,
            // which moves down under it — the left column now reads what to do, then what
            // is in the tin, in that order.
            // TWO steps, because that is what this bench does now (2026-08-26): the ice
            // and the garnish moved to the room's own counter with the rail, and a card
            // that still listed them here would be directions to a station that left.
            BuildStepCard(_servePanel, "THE COUNTER",
                new[] { "toglass", "serve" },
                new[] { "TIP THE TIN", "SERVE IT" },
                _serveStepRows, new Vector2(130, CardSeat(110f)));

            // ON THE BAND (2026-08-26): what is left in the tin reads under the step
            // card in the left column, what is in the glass reads over the name plate on
            // the right — each beside the object it is a number for.
            _serveShakerText = NewText("Shaker", _servePanel, _body, 8, TextAnchor.LowerLeft, UITheme.TextSecondary);
            Place(_serveShakerText.rectTransform, new Vector2(0, 0), new Vector2(280, 12),
                  new Vector2(70, 228));
            _serveShakerText.rectTransform.pivot = new Vector2(0, 0);
            _serveGlassText = NewText("Glass", _servePanel, _body, 8, TextAnchor.LowerRight, UITheme.TextPrimary);
            // Under its own gauge's foot, so the number and the column read as one meter.
            Place(_serveGlassText.rectTransform, new Vector2(1, 0), new Vector2(280, 12),
                  new Vector2(-66, 130));
            _serveGlassText.rectTransform.pivot = new Vector2(1, 0);

            // VERTICAL and engine-drawn (2026-08-02): the GLASS's contents as shares of
            // the vessel, magenta-edged where the shaker's column is cyan. Against the
            // RIGHT WALL now: at 348 it stood where the fridge used to hide it, and with
            // the fridge gone it hung in the middle of an empty room instead — with the
            // bottle in hand resting a hair to its left, which is exactly where a gauge
            // must not be.
            var serveTrack = NewRect("MixTrack", _servePanel);
            // 550, -60: at 575 its right edge stood past the author's 1149-wide working
            // area, and at -8 its head poked over the counter rail (2026-08-26).
            Place(serveTrack, new Vector2(0.5f, 0.5f), new Vector2(44, 300), new Vector2(550, -60));
            var serveBg = serveTrack.gameObject.AddComponent<Image>();
            serveBg.color = new Color(0.05f, 0.05f, 0.09f, 0.88f);
            serveBg.raycastTarget = false;
            GaugeEdge(serveTrack,
                new Color(UITheme.Magenta[3].r, UITheme.Magenta[3].g, UITheme.Magenta[3].b, 0.7f));
            _serveMixBar = NewRect("MixSegs", serveTrack);
            Stretch(_serveMixBar, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));

            // NO FURNITURE ON THIS STAGE AT ALL (2026-08-13, the author: "bardak
            // sahnesindeki masa assetini kaldır, zaten mor alan tezgahmış gibi olmalı").
            // The prep table went the way of the fridge: the PANEL is the counter, the
            // wall stands behind it, and everything the player touches stands directly on
            // that surface. A drawn table was a second surface inside the first, and the
            // props had to be walked up its measured top face to clear the BACK TO BAR key.
            // The painted wall went with the shaker's (2026-08-22): the room itself is
            // behind the scrim now, so a drawn one would be a second bar inside the first.
            // The bar top stays — it is the surface the glass and its shadow stand on.

            _aimText = NewText("AimText", _servePanel, _body, 16, TextAnchor.UpperCenter, UITheme.TextSecondary);
            // On the band (2026-08-26): the same shelf the tin bench's readout sits on,
            // so the eye finds the bench's one sentence in one place on both screens.
            Stretch(_aimText.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(16, 78), new Vector2(-16, 104));

            // The play surface — a COORDINATE SPACE, not a thing you can see. It is where
            // the glass, the tin and the hand bottle are placed and where the pointer is
            // read; it draws nothing. The faint slab it used to wear was a second surface
            // laid over the counter, and with the counter now BEING the panel that slab
            // was a lit rectangle in the middle of it.
            _serveSurface = NewRect("ServeSurface", _servePanel);
            Stretch(_serveSurface, new Vector2(0.26f, 0f), new Vector2(0.95f, 1f),
                new Vector2(0, StageBottom), new Vector2(0, -StageTop));

            // The serving glass: real clear-glass art (2026-07-23), transparent interior so the
            // poured drink pools behind it and shows through; the outline+rim draw in front.
            // The layer architecture (the author, 2026-08-02): the BACK face sits under
            // the pooled drink; the glass image itself becomes the clear FRONT face.
            // Centre-left of the work column, foot on the bench's own line: the glass is
            // 260 tall about its centre, so half of that stands it on BenchFootY.
            var glassRest = new Vector2(-110, BenchFootY + ServeGlassHeight * 0.5f);
            // Under the glass, on the counter. Re-sized with the vessel in
            // ShowServingGlassware — a coupe casts a wider shadow than a highball.
            _serveGlassShadow = AddContactShadow(_serveSurface, 150f,
                new Vector2(glassRest.x, glassRest.y - ServeGlassHeight * 0.5f + 8f));
            _serveGlassBackRt = NewRect("GlassBack", _serveSurface);
            Place(_serveGlassBackRt, new Vector2(0.5f, 0.5f), new Vector2(190, ServeGlassHeight),
                glassRest);
            _serveGlassBack = _serveGlassBackRt.gameObject.AddComponent<Image>();
            _serveGlassBack.raycastTarget = false;
            _serveGlassBack.enabled = false;

            _serveGlass = NewRect("Glass", _serveSurface);
            Place(_serveGlass, new Vector2(0.5f, 0.5f), new Vector2(190, ServeGlassHeight),
                glassRest);
            _serveGlassImage = _serveGlass.gameObject.AddComponent<Image>();
            _serveGlassImage.raycastTarget = false;

            _serveFluid = new MetaballFluid(_serveSurface);
            // The vessel's silhouette and its interior now come from GlassArt, which draws the
            // glass from the same profile the solver fills — set on first refresh, because the
            // glass that stands here depends on what the drink turns out to be.
            // The cavity is the shortest of the three vessels, so the floor and surface insets
            // are a bigger share of it and the estimate runs generous — measured at four fills,
            // it wants a tenth fewer particles to draw the level it was actually given.
            _serveFluid.SetDensity(0.90f);

            _serveGlass.SetAsLastSibling();   // the hollow glass draws over the fluid

            // THE SAME TIN THE OTHER BENCH WORKS (2026-08-26, the author: "bardağa
            // koyduğumuz sahnedeki shaker ile shakera koyduğumuz sahnedeki shaker aynı
            // olmalı"). It was ItemArt.Shaker — a different drawing at two-thirds the size
            // — so the object you had just capped came through the slide as somebody
            // else's shaker. It is the tin bench's own body and cap now, at the tin
            // bench's own 200×358, with the cap SEATED: this bench only ever meets the
            // tin closed.
            _serveShakerRest = new Vector2(190, BenchFootY + 0.22f * ServeVesselH);
            _serveShaker = NewRect("Shaker", _serveSurface);
            _serveShaker.pivot = new Vector2(0.5f, 0.22f);
            _serveShaker.sizeDelta = new Vector2(200, ServeVesselH);
            _serveShaker.anchoredPosition = _serveShakerRest;
            _serveShakerBody = _serveShaker.gameObject.AddComponent<Image>();
            var serveTin = ItemArt.Load("tin_open") ?? ItemArt.Shaker;
            if (serveTin != null)
            {
                _serveShakerBody.sprite = serveTin;
                _serveShakerBody.preserveAspect = true;
                _serveShakerBody.color = Color.white;
                var capArt = ItemArt.Load("shaker_cap");
                if (capArt != null)
                {
                    var cap = NewRect("Cap", _serveShaker);
                    Stretch(cap, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    var capImg = cap.gameObject.AddComponent<Image>();
                    capImg.sprite = capArt;
                    capImg.preserveAspect = true;
                    capImg.raycastTarget = false;
                }
            }
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
                if (Run == null || Run.Phase != TycoonPhase.DayOpen || Run.Glass.IsEmpty) return;
                // WHAT WENT THROUGH THE TIN WAS WORKED IN THE TIN (2026-08-14, the author: an
                // uncapped shaker was reaching the glass). The bench's own key already refuses
                // to let an open tin leave — but the glass has other doors into it (the wall
                // routes a fizz bottle straight here), and the tin rode along through them
                // with its lid still on the counter. One law, both doors.
                if (!_capped)
                {
                    _aimText.text = "THE TIN IS OPEN — PUT THE LID ON AND MIX IT";
                    return;
                }
                _serveGrabbed = true;
            Sfx.Play("tin_tip", 0.6f);
            });
            _serveShaker.gameObject.AddComponent<EventTrigger>().triggers.Add(sgrab);

            // The way back is the left-edge key now (the loop rework's one back, one place).
            AddEdgeBack(_servePanel);
            AddBinButton(_servePanel);      // see the shaker's, above


            var done = NewRect("Done", _servePanel);
            // On the key strip with the others, at the key strip's own height — and 250
            // wide, because the one key that finishes the job earns the widest plate.
            Place(done, new Vector2(0.5f, 0), new Vector2(250, KeyStripH), new Vector2(120, KeyStripY));
            _serveDoneBtn = done.gameObject.AddComponent<Button>();
            _serveDoneBtn.onClick.AddListener(() =>
            {
                // Ready to hand over: close the flow, then click a seat to deliver — and
                // the bench is put back the way it stood (2026-08-25).
                if (Run.ServingGlass.IsEmpty) return;
                ResetServeHand();
                GoTo(Stage.Closed);
                // ...AND THE CELLAR SHUTS BEHIND YOU (2026-08-26, the author: "serve it'e
                // basıldığında kapak kapalı bir şekilde oyuna dönmeli"). The drawer was
                // opened to reach the bottle and stayed open through the build, so SERVE IT
                // dropped you into a room still standing on its shelves — with a drink in
                // hand and a drinker waiting, which is exactly the moment the room should be
                // a bar again. Only THIS door closes it: BACK TO THE BAR keeps the cellar
                // open, because the way back is for reaching another bottle.
                GetComponent<TycoonHud>()?.Room?.SetDrawerOpen(false);
            });
            _serveDoneGroup = done.gameObject.AddComponent<CanvasGroup>();
            var doneFace = NewRect("Face", done);
            Stretch(doneFace, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(done, UITheme.PrimaryAction, _serveDoneBtn, doneFace);   // GDD 16 §2
            // ONE LOUD LINE (2026-08-26, the author's screenshot: the old caption was
            // twenty-six 8px characters on a 240 plate — a whisper on the one key that
            // matters). What to do AFTER pressing it is the room's job to say, and the
            // room already says it over the standing drink.
            var doneLabel = NewText("Label", doneFace, _display, 16, TextAnchor.MiddleCenter, UITheme.TextOnAmber);
            Stretch(doneLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, KeyPlate.Throw), new Vector2(-4, 0));
            doneLabel.text = "SERVE IT ▶";
        }

    }
}
