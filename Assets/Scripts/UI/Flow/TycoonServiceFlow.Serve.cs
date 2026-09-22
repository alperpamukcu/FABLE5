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
        private RectTransform _servePlaque;   // the recess under the rail the steps and the aim line are cut into
        private readonly List<(Image icon, Text label, Image tick)> _serveStepRows =
            new List<(Image, Text, Image)>();
        private Text _serveGlassText;
        private RectTransform _serveSurface;
        private RectTransform _serveGlass;      // the target

        /// <summary>How tall the serving glass is drawn; the width follows the drawing, so the
        /// five vessels differ by silhouette rather than all being stretched into one box.</summary>
        // 340, from 260 (2026-09-06, the author: "bardakların boyutu küçük kalmış ekranda daha
        // da büyümeli"): the glass is the thing this stage is about.
        // BIGGER (2026-09-09, the author: "bardağa alkol koyma sahnesindeki bardak
        // boyutlarını büyüt"): the glass is what this whole bench is about and it stood at
        // 340 on a 720 field with air all round it.
        private const float ServeGlassHeight = 420f;
        private Image _serveGlassImage;
        private LastCall.Core.GlasswareDefinition _serveGlassware;
        private int _serveGlassTier = 1;
        private RectTransform _serveGlassBackRt;
        private RectTransform _serveGlassShadow;
        private Image _serveGlassBack;
        private RectTransform _serveMixBar;

        /// <summary>The lid drawn over the tin on this bench — kept so the tier can dress
        /// it (2026-09-06, the gold shaker). See TycoonServiceFlow.Shaker's DressShakerArt.</summary>
        private Image _serveCapImg;
        private string _serveMixSig = "";
        private GlassArt.Piece _serveGlassPiece;
        private Image _serveGlassSurface;          // the pool's top face (2026-09-09)
        private RectTransform _serveGlassSurfaceRt;

        // ── the first-person staging (v5 P14, the author's diagram of 2026-07-31) ──
        //
        // One-point perspective, the room seen from where the bartender stands: the wall
        // across the back, and THE COUNTER — the panel itself — running under everything
        // else. There is no furniture drawn on it (2026-08-13): a table sprite was a
        // second surface inside the first, so the props had to stand on a picture of a
        // counter that sat on top of the counter. They stand on the counter now.

        /// <summary>Room kept clear at the top for the title and the aim line, and at the bottom
        /// for the two buttons.</summary>
        // 18, not 62 (2026-09-14, "sahneyi biraz daha aşağı al"): the surface's foot drops 44, so its
        // middle — which every prop stands off — is 22 lower, and its top, where the hand stops, stays.
        private const float StageTop = 86f, StageBottom = 18f;

        /// <summary>How tall the shaker is drawn on THIS stage. The shaker bench's 180 left
        /// the tin looking like a thimble beside a 260-tall glass.</summary>
        /// <summary>The tin bench's own 358 since the two benches agreed on one tin
        /// (2026-08-26); the pour maths reads the mouth off this, so it moves with it.</summary>
        private const float ServeVesselH = TinH;

        private RectTransform _serveShaker;     // the grabbable shaker
        private Image _serveShakerBody;
        private MetaballFluid _serveFluid;      // the metaball liquid in the serving glass

        /// <summary>Set by whichever pour path ran this frame; the stage frame drives the
        /// one loop source from it, so the tin and the bottle cannot stop each other's sound.</summary>
        private bool _servePouringNow;

        private Text _aimText;
        private Vector2 _serveShakerRest;
        private RectTransform _serveTinShadow;   // the tin casts one too (2026-09-17); the glass had its own
        private bool _serveGrabbed;
        /// <summary>The hand on the tin (PourHand, 2026-09-11): the neck grip that lets a tall
        /// glass be poured, the weight, and the walk home when it is let go.</summary>
        private readonly PourHand _serveHand = new PourHand();
        private RectTransform _serveRimOver;     // the rim crust's host, over the glass's front crop
        /// <summary>The serve glass draws its drink as the back sheet's own pixels (2026-09-14).</summary>
        private static readonly bool ServeBodyFill = true;
        private HoverGlow _serveGlow;            // the tin's glow, stilled while the hand holds it
        /// <summary>How far below the drawn spout the hand holds the tin, and how much grip lift
        /// tips it fully. Measured against the 420 highball: the tin's spout clears the drawn rim
        /// from the first pouring angle to full tilt, over ~116 units of hand travel (was 10).</summary>
        // 220, not 170 (2026-09-14, the author: "şişelerde sıvı dökerken daha ince ayar yapılabilmeli").
        private const float ServeGripDepth = 60f, ServeLiftRange = 300f;   // 300: the glass went to the bottom (2026-09-14)
        /// <summary>A tin that ran dry leaves the bench — but only once it is standing on it.</summary>
        private bool _serveTinLeaving;
        /// <summary>The share of full flow the tin is giving this frame — Core's BottlePour, read
        /// off the tin's own level (the rate itself is TycoonConfig.ServePourMax).</summary>
        private float _tinShare;

        // The way out: SERVE only means something once a drink stands in the glass, so
        // the key dims until one does — the ToGlass key's own law, applied here.
        private Button _serveDoneBtn;
        private CanvasGroup _serveDoneGroup;
        private RectTransform _serveDone;
        private PackKey _serveDonePack;
        private PressSink _serveDoneSink;
        private MarqueeBulbs _serveSign;   // null since 2026-09-21: the lamps came off the key
        /// <summary>The key at the bottom's centre: wider than tall, big enough to be the last thing pressed.</summary>
        private const float ServeKeyW = 288f, ServeKeyH = 64f;
        private bool _serveDoneReady = true;

        // ── the serve stage ──────────────────────────────────────────────────────

        private void RefreshServe()
        {
            var run = Run;
            _serveShakerText.text = run.Glass.IsEmpty
                ? UIText.T("bench.serve.shaker_empty")
                : UIText.T("bench.serve.shaker_left", ("pct", run.Glass.FillFraction.ToString("P0")));
            _serveGlassText.text = run.ServingGlass.IsEmpty
                ? UIText.T("bench.serve.glass_empty")
                : UIText.T("bench.serve.glass_pct_full", ("pct", run.ServingGlass.FillFraction.ToString("P0")));
            // The hand is re-measured on the way in (the tier dresses the cap, and the cap is the
            // spout), and a hand that is not holding anything is stood back on the rest.
            ConfigureServeHand();
            _serveFluid.Clear();
            _serveFluid.ClearStreamColor();       // nothing is in the air on the way in
            // The pool is the drink IN THE GLASS, not the one in the shaker. Those are the same
            // thing while you are tipping one into the other, which is why nobody noticed — but
            // a built drink leaves the shaker empty, and the pool was taking the colour of
            // nothing and drawing a soda as pale tan.
            _serveFluid.SetColor(DrinkColor(run.ServingGlass.IsEmpty ? run.Glass : run.ServingGlass));
            ShowServingGlassware(run);
            RefreshServeMixBar(run);
            _glassCatchX = ServeGlassRestX; _glassCatchV = 0f; _glassAx = 0f; _glassSway = 0f; _glassSwayV = 0f; _glassCatchLast = ServeGlassRestX;
            PushServePool(run);
            GlassDecor.Sync(_serveGlass, _serveGlassPiece, run.ServingGlass, run, 0f, 0f, _serveRimOver);
            // Steel is steel whatever is in it. This used to multiply the tin sprite by the
            // drink's colour AND by its alpha — harmless while that alpha was a fixed 0.9, and
            // not harmless at all once it became the fill-derived 0.52-0.86: the serve stage's
            // shaker turned into a see-through, drink-tinted tin. The hand bottle three methods
            // away and the tap's keg both guard this the same way.
            _serveShakerBody.color = _serveShakerBody.sprite != null
                ? Color.white : DrinkColor(run.Glass);
            _serveShaker.gameObject.SetActive(!run.Glass.IsEmpty);
            _aimText.text = run.Glass.IsEmpty
                ? UIText.T("bench.serve.aim.nothing")
                : UIText.T("bench.serve.aim.grab");
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
            _serveHand.Release();
        }

        /// <summary>
        /// Reads the tin's DRAWN spout off its cap and gives it to the hand. The cap is stretched
        /// over the tin's whole rect and its sheet is exactly the tin's, so the spout is the top of
        /// the cap's opaque pixels mapped into that rect, relative to the tin's pivot — the same
        /// measurement the shaker bench's TinMouth makes, which this bench never had: it aimed
        /// from the rect's top, 42 units of empty canvas above the drawn lid.
        /// </summary>
        private void ConfigureServeHand()
        {
            if (_serveShaker == null) return;
            float h = _serveShaker.rect.height;
            float top = h * (1f - _serveShaker.pivot.y);                 // the rect top, pivot-relative
            var sp = _serveCapImg != null ? _serveCapImg.sprite : null;
            if (sp != null && sp.rect.height >= 1f)
            {
                var ob = ItemArt.OpaqueBounds(sp);
                top -= (sp.rect.height - (ob.y + ob.height)) * (h / sp.rect.height);
            }
            _serveHand.Configure(_serveShakerRest, new Vector2(0f, top), ServeGripDepth, ServeLiftRange, MaxTilt);
            _serveHand.Apply(_serveShaker);
        }

        /// <summary>Where the tin's drawn spout really is, in surface space — through the live
        /// transform, because the hover glow grows the held tin by 4% about its pivot and that
        /// carries the spout 11 units further out than the hand's own arithmetic knows.</summary>
        private Vector2 ServeSpoutNow()
        {
            var world = _serveShaker.TransformPoint(_serveHand.Spout);
            return _serveSurface.InverseTransformPoint(world);
        }

        /// <summary>The glass's DRAWN rim: its cavity top and half-width, off the glass art.</summary>
        private (Vector2 Centre, float Half) ServeRim()
        {
            var c = _serveGlass.anchoredPosition;
            float w = _serveGlass.rect.width, h = _serveGlass.rect.height;
            var piece = _serveGlassPiece;
            if (piece.Sprite == null)
                return (c + new Vector2(0f, h * 0.5f), w * 0.4f);
            return (new Vector2(c.x, c.y - h * 0.5f + h * piece.RimY), w * 0.5f * piece.InteriorHalf);
        }

        private float _glassCatchX = ServeGlassRestX, _glassCatchV, _glassAx, _glassSway, _glassSwayV, _glassCatchLast = ServeGlassRestX;
        // THE PAIR IS CENTRED, NOT THE GLASS (2026-09-17, the author: "bardak tam ortada konumlanmasın,
        // bardak ve shaker ikisi sahnenin ortasında olacak ama iç içe olmayacak aynı şu anki mesafe olacak
        // aralarında"): the two keep the 260 units they have always had between them, and it is the PAIR's
        // middle that stands on the SCREEN's centre line now.
        //
        // The numbers look strange until you measure the surface they are in: ServeSurface is stretched 0.26..0.95
        // of the panel, so ITS middle is at screen 774, not 640. That is how -110 put the glass at screen 664 -
        // near enough dead centre for the author to say so - and the first fix here, -130, actually walked it ONTO
        // 644. Centred as a pair on the screen, the glass is at -264 (screen 510) and the tin at -4 (screen 770),
        // with the same 260 between them; the tin's right shoulder passes six units behind SERVE IT's plate, which
        // is a key drawn after it.
        private const float ServeGlassRestX = -264f;
        private float _serveFall01, _servePan;   // the tin's pour, heard: how far it drops, where on the bench (2026-09-15)

        /// <summary>Where a stream into the serving glass lands, surface-local: the top of the drink in it, its floor
        /// when it is empty (2026-09-15).</summary>
        private float ServeDrinkTopY(TycoonRun run)
        {
            var c = _serveGlass.anchoredPosition;
            float h = _serveGlass.rect.height;
            return c.y - h * 0.5f + h * _serveGlassPiece.FillAmount((float)run.ServingGlass.FillFraction);
        }
        /// <summary>How far either side of the surface's middle the glass's centre may go.</summary>
        private float GlassReach() =>
            Mathf.Max(0f, _serveSurface.rect.width * 0.5f - _serveGlass.rect.width * 0.5f - 10f);

        /// <summary>The serving glass catches (2026-09-14): it follows the tin's mouth along the bottom, between the
        /// bench's left edge and SERVE IT, rocked on its foot; the back sheet and the shadow go with it.</summary>
        private void StepGlassCatch(float targetX)
        {
            if (_serveGlass == null || _serveSurface == null) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f) return;
            // THE WHOLE BENCH (2026-09-14, second pass): it passes behind SERVE IT rather than leave a stream it cannot
            // reach; the hand keeps the tin's mouth inside this (UpdateServeTilt).
            float reach = GlassReach();
            StepCatch(ref _glassCatchX, ref _glassCatchV, ref _glassAx, ref _glassSway, ref _glassSwayV, ref _glassCatchLast,
                Mathf.Clamp(targetX, -reach, reach), dt);
            var rock = Quaternion.Euler(0f, 0f, _glassSway);
            float h = _serveGlass.rect.height;
            _serveGlass.anchoredPosition = new Vector2(_glassCatchX, CatchFootY + GlassFootLift)
                + (Vector2)(rock * new Vector3(0f, h * 0.5f, 0f))
                + new Vector2(0f, ServeDropOffset());                 // on its way down when the bench opens (2026-09-17)
            _serveGlass.localRotation = rock * Quaternion.Euler(0f, 0f, ServeDropSwing());
            if (_serveShaker != null)
                PushPropShadow(_serveTinShadow, _serveShaker, _serveShakerRest.y, BenchFootY + 4f, 158f * (TinW / 200f),
                    _serveShaker.gameObject.activeSelf ? 1f : 0f);
            if (_serveGlassBackRt != null)
            {
                _serveGlassBackRt.anchoredPosition = _serveGlass.anchoredPosition;
                _serveGlassBackRt.localRotation = rock;
                _serveGlassBackRt.sizeDelta = _serveGlass.sizeDelta;
            }
            if (_serveGlassShadow != null)
                _serveGlassShadow.anchoredPosition = new Vector2(_glassCatchX, _serveGlassShadow.anchoredPosition.y);
        }

        /// <summary>The top of the glass's DRAWING in surface space — what the tin's mouth has to clear
        /// before it leans (2026-09-14). The sheet fills the glass rect's height, as ServeRim reads it.</summary>
        private float ServeGlassTop()
        {
            var c = _serveGlass.anchoredPosition;
            float h = _serveGlass.rect.height;
            var sp = _serveGlassPiece.Sprite;
            if (sp == null || sp.rect.height < 1f) return c.y + h * 0.5f;
            var ob = ItemArt.OpaqueBounds(sp);
            return c.y - h * 0.5f + (ob.y + ob.height) * (h / sp.rect.height);
        }

        /// <summary>The SERVE key answers only a glass with a drink in it — dim until then.
        /// Driven every frame from the stage's own update, now that the cabinet it used to
        /// ride on is gone.</summary>
        private void PushServeDone(TycoonRun run)
        {
            if (_serveDoneGroup == null || run == null) return;
            bool ready = !run.ServingGlass.IsEmpty;
            _serveDoneGroup.alpha = ready ? 1f : 0.8f;
            if (_serveDoneBtn != null) _serveDoneBtn.interactable = ready;
            if (_serveDoneReady == ready) return;
            // THE THIRD DRAWING (2026-09-17): not the amber key at half opacity, which still looked pressable,
            // but the same plate cut from NIGHT with its lit and pressed states taken off it and its lamps put
            // out - so nothing under the pointer promises anything until there is a drink in the glass.
            _serveDoneReady = ready;
            RetonePackKey(_serveDone, ready ? KeyGo : KeyWay);
            if (_serveSign != null) _serveSign.On = ready;   // a key that cannot be pressed stops advertising
            if (_serveDonePack != null) _serveDonePack.enabled = ready;
            if (_serveDoneSink != null) _serveDoneSink.enabled = ready;
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
            if (_serveGrabbed && !Mouse.current.leftButton.isPressed) { _serveGrabbed = false; _serveHand.Release(); }
            // Held, the glow lets go — see the shaker bench's bottle (2026-09-13).
            if (_serveGlow != null)
            {
                _serveGlow.Rise = _serveGrabbed ? 0f : 4f;
                _serveGlow.Sway = _serveGrabbed ? 0f : 1.4f;
                _serveGlow.Grow = _serveGrabbed ? 1f : 1.04f;
            }

            // The hand moves every frame, held or not: let go and the tin walks itself home.
            Vector2? pointer = null;
            if (_serveGrabbed && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _serveSurface, Mouse.current.position.ReadValue(), null, out Vector2 ptr))
                pointer = ptr;
            float halfW = _serveSurface.rect.width * 0.5f;
            float halfH = _serveSurface.rect.height * 0.5f;
            // The tin's mouth stays where the glass can go (2026-09-14, second pass): tipped left it runs up to its grip
            // left of the hand, so the hand keeps inside the glass's reach by that much on the left.
            float glassReach = _serveGlass != null ? GlassReach() : halfW;
            _serveHand.Step(Time.deltaTime, pointer,
                Rect.MinMaxRect(Mathf.Max(-halfW + 30f, -glassReach + _serveHand.HoldBelowSpout),
                                -halfH + 20f, Mathf.Min(halfW - 30f, glassReach + 20f), halfH + HandAbove));
            _serveHand.Apply(_serveShaker);
            if (_serveTinLeaving && _serveHand.AtRest)
            {
                _serveTinLeaving = false;
                _serveShaker.gameObject.SetActive(!run.Glass.IsEmpty);
            }

            bool pourNow = false;
            double accuracy = 0;
            if (_serveGrabbed && _serveHand.Held && !run.Glass.IsEmpty)
            {
                float tilt = _serveHand.Tilt;
                // The DRAWN spout over the DRAWN rim — the pair the shaker bench has measured
                // since 2026-08-11 and this bench never did (it aimed rect top at rect top).
                Vector2 mouth = ServeSpoutNow();
                var (opening, rimHalf) = ServeRim();
                // THE GLASS CATCHES (2026-09-14): it slides under the stream wherever the tin is tipped
                // (StepGlassCatch), so there is no rim to clear and no aim to miss.
                bool clear = true;
                // BY THE LIFT, IN STEPS (BottlePour.LiftShare): 1, 1, 2, 3, 5, 8, 13, 21, 34 past level.
                _tinShare = tilt >= PourFromTilt ? (float)BottlePour.LiftShare(_serveHand.PourLift01, run.Glass.FillFraction) : 0f;
                bool running = _tinShare > 0f;

                // The glass is full: the pour stops there rather than running a stream into a
                // vessel that cannot take it (GDD 21 §3, 2026-07-28).
                if (run.ServingGlass.IsFull && running && clear)
                {
                    _serveGrabbed = false;
                    _serveHand.Release();
                    ShowGlassFull();
                }
                else if (running && clear && !run.CanPourOut)
                {
                    // THE MANDATORY MIX (GDD 21 §14): two spirits may not leave the tin
                    // unmixed. Core refuses in PourIntoServingGlass — which this stage
                    // calls per frame — so the UI reads the predicate and stops the
                    // stream the way CanPull greys the keg key, instead of catching an
                    // exception forty times a second.
                    _serveGrabbed = false;
                    _serveHand.Release();     // set back on the bench, not snapped upright in mid-air
                    if (_aimText != null)
                        _aimText.text = UIText.T("bench.serve.aim.needs_mixing");
                }
                else if (running && clear)
                {
                    // Aim: how well the mouth is centred over the glass. Within ~half the
                    // glass width is a clean pour; beyond that the stream drifts wide.
                    // NOTHING SPILLS ANY MORE (2026-09-07, the author: "1 shakerdan hangi
                    // bardak olursa olsun tam 1 porsiyon çıkmalı"): a stream that misses the
                    // glass is a stream the tin does not pour — the aim GATES the pour
                    // instead of taxing it, so a full tin is always a full glass.
                    accuracy = 1.0;     // the glass is under the stream wherever it falls (2026-09-14)
                    pourNow = true;

                    // The stream falls toward where the aim sends it: dead-on it drops into the
                    // glass and melts into the drink; off-aim it drifts wide and misses the rim,
                    // falling past onto the counter — the spill you can see (GDD 24 §3).
                    // What comes out of the TIN is the shaker's own drink, which is a different
                    // liquid from what is already standing in the glass; it goes on the stream.
                    // NO LIQUID IS DRAWN THAT DOES NOT POUR (2026-09-11). Off the aim nothing
                    // leaves the tin (GDD 21, 2026-09-07: the aim gates the pour), yet a thin
                    // "miss" stream was still drawn falling wide of the glass and onto the
                    // counter — liquid visibly running out of a tin that was pouring nothing.
                    if (pourNow)
                    {
                        _serveFluid.SetStreamColor(DrinkColor(run.Glass));
                        var streamVel = new Vector2(0f, -225f);   // straight down: the glass follows the liquid (2026-09-14)
                        // As thick as the pour is heavy: Core's share of full flow, not the angle.
                        // A thread when little is running (2026-09-13), a rope neck-down.
                        _serveFluid.EmitStream(mouth, streamVel, Time.deltaTime, 0.2f + 1.3f * _tinShare);
                        // What the pour sounds like (2026-09-15): how far it drops onto the drink, and where on the bench.
                        _serveFall01 = FallOf(mouth.y, ServeDrinkTopY(run));
                        _servePan = PanOf(mouth.x, _serveSurface);
                    }
                    else RefreshServeText(run, accuracy);
                }
            }

            if (pourNow)
            {
                _servePouringNow = true;
                double before = run.ServingGlass.TotalVolume;
                run.PourOutLift(Time.deltaTime, _serveHand.PourLift01);   // Core's steps, Core's rate
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
            // THE GLASS FOLLOWS THE LIQUID (2026-09-14, second pass): where the next drop comes down at its top, else the
            // mouth of a tin well on its way over, else home.
            float glassTo = ServeGlassRestX;
            if (_serveFluid.NextLandingX(ServeGlassTop(), out float falling)) glassTo = falling;
            else if (_serveGrabbed && _serveHand.Held && _serveHand.Tilt > 40f) glassTo = ServeSpoutNow().x;
            StepGlassCatch(glassTo);
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
            // BACK, DRINK, GARNISH, FRONT (2026-09-14, the author: "görsel->sıvı->(araya girmesi
            // gereken garnishler girecek (tuz ve şeker değil))->görsel_front"). The whole sheet stands
            // BEHIND the drink on GlassBack; the glass's own image keeps the sheet for whoever reads
            // it but draws nothing; the author's _Front crop (GlassLip) is what stands in front.
            _serveGlassImage.sprite = piece.Front != null ? piece.Front : piece.Sprite;
            _serveGlassImage.preserveAspect = true;
            _serveGlassImage.color = Color.white;
            _serveGlassImage.enabled = false;
            if (_serveGlassLip != null)
            {
                bool hasLip = piece.LipPlacement(_serveGlass.sizeDelta, out var lipSize, out var lipAt);
                _serveGlassLip.enabled = hasLip;
                if (hasLip)
                {
                    _serveGlassLip.sprite = piece.Lip;
                    _serveGlassLipRt.sizeDelta = lipSize;
                    _serveGlassLipRt.SetAsLastSibling();
                }
                if (_serveRimOver != null) _serveRimOver.SetAsLastSibling();   // the crust over the front
            }
            PlaceServeSurface(piece, (float)run.ServingGlass.FillFraction);
            if (_serveGlassBack != null)
            {
                _serveGlassBack.sprite = piece.Back != null ? piece.Back : piece.Sprite;
                _serveGlassBack.enabled = _serveGlassBack.sprite != null;
                _serveGlassBack.preserveAspect = true;
            }
            // Height is fixed and width follows the drawing, so a coupe is wide and a highball
            // narrow at the same place on the counter instead of all five being stretched into
            // one box.
            // ONE SCALE FOR THE SET (2026-09-06): the installed sheets are trimmed per
            // line, so a fixed height drew the rocks tumbler as tall as the highball. The
            // bench takes its units-per-pixel from the tallest glass the bar owns, and every
            // vessel keeps the proportion it was drawn with. The fluid follows: the profile,
            // floor and rim are fractions of this rect.
            _serveGlass.sizeDelta = GlassArt.BoxFor(glassware, piece.Sprite, ServeGlassHeight);
            // ON THE BENCH, WHATEVER ITS HEIGHT (2026-09-07): the rect's centre was fixed at
            // half the TALLEST glass above the bench line, so a rocks tumbler floated two
            // inches over its own shadow. The foot stands on the line; the centre follows.
            _serveGlass.anchoredPosition = new Vector2(_serveGlass.anchoredPosition.x,
                CatchFootY + GlassFootLift + _serveGlass.sizeDelta.y * 0.5f);
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
                    CatchFootY + GlassFootLift + 8f);
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
            // AN EMPTY GLASS STILL HAS A FLOOR (2026-09-11). It used to clear the pool, and a pool
            // that is not there is nothing to land on: the first drops of EVERY pour fell straight
            // through the empty glass and onto the counter behind it — the author's "bardağa
            // koyarken sıvılar yere damlıyor gibi gözüküyor". The box is set with no drink in it,
            // so the stream lands on the glass's own floor and the drink starts there.
            var piece = _serveGlassPiece;
            var c = _serveGlass.anchoredPosition;
            float w = _serveGlass.rect.width, h = _serveGlass.rect.height;
            // The box is FLUSH with the measured cavity — the metaball surface is built
            // to touch its box, so contact needs no overshoot (the seam of 2026-08-02
            // came from an INSET box; the spills came from overshooting). The ceiling
            // sits PoolCeilingArtPx BELOW the cavity top: the surface is a bumpy band, not a
            // line, and those bumps must crest inside the mouth, not over the lip.
            // FLUSH, NOT HALF A PIXEL IN (2026-09-13, the author: "dökme animasyonunda
            // bardaklar tam dolmuyor"): the half pixel a side left the outermost see-through
            // column of the rocks glass dry; the wall's own pixels take the edge's bleed.
            float artPx = piece.Sprite != null ? w / piece.Sprite.rect.width : 1.5f;
            float iw = w * 0.5f * piece.InteriorHalf;
            float floor = c.y - h * 0.5f + h * piece.FloorY;
            // The particles fill to the drink's FULL line, not the cavity's top (2026-09-14): they are
            // what the stream lands on, and the body below draws the drink at that same line.
            float rim = c.y - h * 0.5f + h * piece.FillAmount(1f);
            _serveFluid.SetFloorArc(piece.FloorArc * h);   // the floor's near arc, not a line
            _serveFluid.SetPool(c.x - iw, c.x + iw, floor, rim, (float)run.ServingGlass.FillFraction);
            // The drink is drawn in the glass's own pixels, counted from the drawing's corner.
            _serveFluid.SetPixelGrid(artPx, new Vector2(c.x - w * 0.5f, c.y - h * 0.5f));
            // THE DRINK IS THE GLASS'S OWN PIXELS (2026-09-14; MetaballFluid.SetBody): every pixel of
            // the back sheet between the floor and the drink's line — cut by the sheet's alpha, level
            // on top. The grid's origin is this rect's corner, so the rect is (0,0,w,h) from it.
            float bodyFrac = (float)run.ServingGlass.FillFraction;
            if (piece.Sprite != null && bodyFrac > 0.001f)
            {
                float level = piece.FillAmount(bodyFrac);
                _serveFluid.SetBody(piece.Sprite, new Rect(0f, 0f, w, h), h * piece.FloorY, h * level);
                // ROUND, NOT FLAT (2026-09-14, the author: "bardağın içerisindeki sıvı ve şişelerin içerisindeki sıvı da 3 boyutlu olmalı altı ve üstü bardağın yüzeylerine göre dairesel hissini vermeli"): the top face is the squashed oval the counter's
                // glass wears, as wide as this glass's own drink is on the level's row.
                float sheetW = piece.Sprite.rect.width, sheetH = piece.Sprite.rect.height, toGrid = w / sheetW;
                _serveFluid.SetBodyArcs(GlassArt.BodySpan(piece.Sprite, level * sheetH - 0.5f, out float topC, out float topHalf)
                    ? new Vector3(topC * toGrid, topHalf * toGrid, topHalf * toGrid * GlassArt.SurfaceSquash) : Vector3.zero);
                // THE FLOOR FOLLOWS THE GLASS'S PIXEL CURVE (2026-09-15, the author: "Bardakların tabanında da ovallik
                // gerekiyor bardağın pixel eğrisine göre"): the drawing's own bottom edge, scaled to the drink's width on
                // the floor's row, stands each column of the drink up from the floor (GlassArt.BodyFloorRise).
                _serveFluid.SetBodyFloor(GlassArt.BodyFloorRise(piece.Sprite, piece.FloorY * sheetH + 1.5f), toGrid);
            }
            else _serveFluid.ClearBody();
            _serveFluid.SetBodyTurn(_glassSway, new Vector2(w * 0.5f, h * 0.5f));   // the drink rocks with the glass
            // The top face rides the pour (2026-09-13): it was placed once, when the glass
            // appeared, and a full glass kept the face it had at the first drop.
            PlaceServeSurface(piece, (float)run.ServingGlass.FillFraction);
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
            _serveHand.Release();             // it walks home now — it used to teleport there
            _serveFluid.ClearStreamColor();   // the tin has stopped; the air belongs to the glass
            _serveTinLeaving = true;          // and leaves the bench once it is standing on it
            RefreshServeText(run, 1.0);
            if (run.Glass.IsEmpty)
            {
                _aimText.text = UIText.T("bench.serve.aim.shaker_empty");
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
                    ? UIText.T("bench.serve.shaker_empty")
                    : UIText.T("bench.serve.shaker_left", ("pct", run.Glass.FillFraction.ToString("P0")));
                _serveGlassText.text = UIText.T("bench.serve.glass_full");
            }
            _aimText.text = UIText.T("bench.serve.aim.glass_full");
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

        /// <summary>The aim under which the tin does not pour: the stream visibly misses the
        /// glass, and what misses the glass never leaves the tin (2026-09-07).</summary>
        private const double AimGate = 0.35;

        private void RefreshServeText(TycoonRun run, double accuracy)
        {
            _serveShakerText.text = UIText.T("bench.serve.shaker_left", ("pct", run.Glass.FillFraction.ToString("P0")));
            _serveGlassText.text = UIText.T("bench.serve.glass_pct_full", ("pct", run.ServingGlass.FillFraction.ToString("P0")));
            GlassDecor.Sync(_serveGlass, _serveGlassPiece, run.ServingGlass, run, 0f, 0f, _serveRimOver);
            RefreshServeMixBar(run);
            _aimText.text = accuracy > 0.8 ? UIText.T("bench.serve.aim.clean")
                : accuracy > AimGate ? UIText.T("bench.serve.aim.steady") : UIText.T("bench.serve.aim.missing");
            _aimText.color = Color.Lerp(UITheme.ViceRed[3], UITheme.Lime[3], (float)accuracy);
        }

        /// <summary>
        /// THE TOP OF THE POOL IS AN ELLIPSE HERE TOO (2026-09-09, the author: "bardakların
        /// içerisinde sıvının bulunduğu konumu kesinlikle doğru konumlandır, taban ve tavan
        /// eğimli olmalı 2.5d"). The fluid gives the bench a real surface with a wet band on
        /// it, but seen from the front that surface is still a LINE: the drink in a cylinder
        /// shows its top face, which is what tells the eye the glass is round. The counter's
        /// carried glass has drawn one since 2026-08-11; this is the same disc, at the same
        /// width the glass has at that level, riding the fluid's own measured surface.
        /// </summary>
        private void PlaceServeSurface(GlassArt.Piece piece, float fraction)
        {
            if (_serveGlassSurface == null) return;
            // THE BODY DRAWS ITS OWN TOP NOW (2026-09-14, PushServePool): a disc on its own grid over
            // a level line was a second surface the drink did not agree with.
            if (ServeBodyFill) { _serveGlassSurface.enabled = false; return; }
            if (piece.Sprite == null || fraction <= 0.001f || piece.Aspect <= 0f)
            {
                _serveGlassSurface.enabled = false;
                return;
            }
            var rect = _serveGlass.rect.size;
            float drawnH = Mathf.Min(rect.y, rect.x / piece.Aspect);
            float drawnW = drawnH * piece.Aspect;
            float level = piece.FillAmount(fraction);
            float width = piece.InteriorWidthAt(level) * drawnW;
            if (width <= 1f) { _serveGlassSurface.enabled = false; return; }
            var rt = _serveGlassSurface.rectTransform;
            rt.sizeDelta = new Vector2(width, width * GlassArt.SurfaceSquash);
            rt.anchoredPosition = _serveGlass.anchoredPosition
                                  + new Vector2(0f, (level - 0.5f) * drawnH);
            var body = DrinkColor(Run != null ? Run.ServingGlass : null);
            _serveGlassSurface.color = new Color(
                Mathf.Lerp(body.r, 1f, 0.24f), Mathf.Lerp(body.g, 1f, 0.24f),
                Mathf.Lerp(body.b, 1f, 0.24f), body.a);
            _serveGlassSurface.enabled = true;
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
            // Cut into the counter like the tin bench's (2026-09-13) — on the same plaque under the rail (2026-09-16),
            // with the aim line on its lower row.
            _servePlaque = AddCounterPlaque(_servePanel, PlaqueW, PlaqueH, PlaqueUnderRail, RightColX);   // the right column (seventh list)
            // the same column as the tin's bench, two rows deep (2026-09-17)
            var serveSteps = ColumnPanel(_servePanel, "StepPanel", ColStepsTop - StepPanelH(2), StepPanelH(2));
            BuildStepList(serveSteps,
                new[] { UIText.T("bench.serve.step.tip"), UIText.T("bench.serve.step.serve") },
                new[] { "step_fill", "step_glass" }, _serveStepRows);

            // ON THE BAND (2026-08-26): what is left in the tin reads under the step
            // card in the left column, what is in the glass reads over the name plate on
            // the right — each beside the object it is a number for.
            _serveShakerText = NewText("Shaker", _servePanel, _body, 8, TextAnchor.LowerLeft, UITheme.TextSecondary);
            // On the counter front at the left, level with the strip (2026-09-13).
            Place(_serveShakerText.rectTransform, new Vector2(0, 0), new Vector2(280, 12),
                  new Vector2(24, 62));
            _serveShakerText.rectTransform.pivot = new Vector2(0, 0);
            _serveGlassText = NewText("Glass", _servePanel, _body, 8, TextAnchor.LowerRight, UITheme.TextPrimary);
            Place(_serveGlassText.rectTransform, new Vector2(1, 0), new Vector2(280, 12),
                  new Vector2(-66, 130));
            _serveGlassText.rectTransform.pivot = new Vector2(1, 0);
            // The measuring glass carries the glass's reading on its own surface now.
            _serveGlassText.gameObject.SetActive(false);

            // VERTICAL and engine-drawn (2026-08-02): the GLASS's contents as shares of
            // the vessel, magenta-edged where the shaker's column is cyan. Against the
            // RIGHT WALL now: at 348 it stood where the fridge used to hide it, and with
            // the fridge gone it hung in the middle of an empty room instead — with the
            // bottle in hand resting a hair to its left, which is exactly where a gauge
            // must not be.
            // 550, -60: at 575 its right edge stood past the author's 1149-wide working
            // area, and at -8 its head poked over the counter rail (2026-08-26).
            // The tin bench's instrument, with the other word on its cap (2026-09-04).
            _serveMixBar = BuildStandingGauge(_servePanel, MeasureAt, MeasureSize, UIText.T("bench.serve.gauge_head"));

            // NO FURNITURE ON THIS STAGE AT ALL (2026-08-13, the author: "bardak
            // sahnesindeki masa assetini kaldır, zaten mor alan tezgahmış gibi olmalı").
            // The prep table went the way of the fridge: the PANEL is the counter, the
            // wall stands behind it, and everything the player touches stands directly on
            // that surface. A drawn table was a second surface inside the first, and the
            // props had to be walked up its measured top face to clear the BACK TO BAR key.
            // The painted wall went with the shaker's (2026-08-22): the room itself is
            // behind the scrim now, so a drawn one would be a second bar inside the first.
            // The bar top stays — it is the surface the glass and its shadow stand on.

            // On the plaque's lower row (2026-09-16): the same place the tin bench's readout sits, so the eye finds
            // the bench's one sentence in one place on both screens.
            _aimText = NewText("AimText", _servePlaque, _body, 16, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(_aimText.rectTransform, new Vector2(0f, 0f), new Vector2(PlaqueW - PlaquePad * 2f, PlaqueLineH),
                  new Vector2(PlaquePad, PlaqueLineY));
            _aimText.horizontalOverflow = HorizontalWrapMode.Wrap;   // a long line takes the plaque's second row

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
            var glassRest = new Vector2(ServeGlassRestX, CatchFootY + GlassFootLift + ServeGlassHeight * 0.5f);
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

            // shadows under the glass and the tin, first in the surface so every prop draws over them (2026-09-17)
            _serveTinShadow = AddContactShadow(_serveSurface, 158f * (TinW / 200f), new Vector2(150f, BenchFootY + 4f));
            _serveTinShadow.SetAsFirstSibling();

            _serveGlass = NewRect("Glass", _serveSurface);
            // the pool's top face, made before the glass so it draws under the front wall
            Place(_serveGlass, new Vector2(0.5f, 0.5f), new Vector2(190, ServeGlassHeight),
                glassRest);
            _serveGlassImage = _serveGlass.gameObject.AddComponent<Image>();
            _serveGlassImage.raycastTarget = false;

            _serveFluid = new MetaballFluid(_serveSurface);
            // The serving glass stands still and never carries foam, and its tall glasses are
            // the columns the stacked regime exists for (MetaballFluid, 2026-09-11): without it
            // the full 420 highball boiled at 13 ms a step and drew its level wherever the boil
            // put it. The tin and the tap keep the regime they were tuned in.
            _serveFluid.SetStacked(true);
            // The vessel's silhouette and its interior now come from GlassArt, which draws the
            // glass from the same profile the solver fills — set on first refresh, because the
            // glass that stands here depends on what the drink turns out to be.
            // The cavity is the shortest of the three vessels, so the floor and surface insets
            // are a bigger share of it and the estimate runs generous — measured at four fills,
            // it wants a tenth fewer particles to draw the level it was actually given.
            _serveFluid.SetDensity(0.90f);

            // The pool's top face: made BEFORE the glass is brought forward, so the glass's
            // own front wall crosses it and it reads as inside the vessel (2026-09-09).
            var srf = NewRect("PoolSurface", _serveSurface);
            srf.anchorMin = srf.anchorMax = srf.pivot = new Vector2(0.5f, 0.5f);
            srf.anchoredPosition = _serveGlass.anchoredPosition;
            _serveGlassSurface = srf.gameObject.AddComponent<Image>();
            _serveGlassSurface.sprite = GlassArt.SurfaceDisc();
            _serveGlassSurface.raycastTarget = false;
            _serveGlassSurface.enabled = false;
            _serveGlassSurfaceRt = srf;

            _serveGlass.SetAsLastSibling();   // the hollow glass draws over the fluid
            // ...and the rim's front edge over the glass (2026-09-08, the author's `_Front`
            // strips): the fluid's meniscus met the rim's front and read as sitting on it.
            _serveGlassLipRt = NewRect("GlassLip", _serveSurface);
            _serveGlassLipRt.anchorMin = _serveGlassLipRt.anchorMax = new Vector2(0.5f, 0.5f);
            _serveGlassLipRt.pivot = new Vector2(0.5f, 1f);
            _serveGlassLip = _serveGlassLipRt.gameObject.AddComponent<Image>();
            _serveGlassLip.raycastTarget = false;
            _serveGlassLip.enabled = false;
            // The rim's crust rides OVER the front (2026-09-14): salt and sugar are on the mouth, not
            // in the drink, so GlassDecor hangs its crust here — the glass rect's size and centre.
            _serveRimOver = NewRect("GlassRimOver", _serveSurface);
            _serveRimOver.anchorMin = _serveRimOver.anchorMax = _serveRimOver.pivot = new Vector2(0.5f, 0.5f);

            // THE SAME TIN THE OTHER BENCH WORKS (2026-08-26, the author: "bardağa
            // koyduğumuz sahnedeki shaker ile shakera koyduğumuz sahnedeki shaker aynı
            // olmalı"). It was ItemArt.Shaker — a different drawing at two-thirds the size
            // — so the object you had just capped came through the slide as somebody
            // else's shaker. It is the tin bench's own body and cap now, at the tin
            // bench's own 200×358, with the cap SEATED: this bench only ever meets the
            // tin closed.
            // 150, not 190 (2026-09-13): the measuring glass stands at the right of the bench now.
            // -4 since 2026-09-17: the same 260 from the glass, the pair centred on the screen (ServeGlassRestX).
            _serveShakerRest = new Vector2(-4, BenchFootY + 0.22f * ServeVesselH);
            _serveShaker = NewRect("Shaker", _serveSurface);
            _serveShaker.pivot = new Vector2(0.5f, 0.22f);
            _serveShaker.sizeDelta = new Vector2(TinW, ServeVesselH);
            _serveShaker.anchoredPosition = _serveShakerRest;
            _serveShakerBody = _serveShaker.gameObject.AddComponent<Image>();
            // IT IS THE VERB ON THIS BENCH (2026-09-06, the author: "bardağa koyma
            // sahnesinde shaker parlamıyor") — you grab it and tip it, so it answers the
            // pointer like everything else you can pick up.
            var serveGlow = _serveShaker.gameObject.AddComponent<HoverGlow>();
            Unlit(serveGlow);
            serveGlow.Graphics = new Graphic[] { _serveShakerBody };
            serveGlow.Rise = 4f; serveGlow.Sway = 1.4f; serveGlow.Grow = 1.04f; serveGlow.Halo = 1.3f;
            _serveGlow = serveGlow;
            var serveTin = ItemArt.Load("tin_open") ?? ItemArt.Shaker;
            if (serveTin != null)
            {
                _serveShakerBody.sprite = serveTin;
                _serveShakerBody.preserveAspect = true;
                _serveShakerBody.color = Color.white;
                // TAKEN ON ITS OWN STEEL (2026-09-13, the author: "nesnenin üzerinden tutmamız
                // gereksin"): the 232x416 rect is mostly air round a tin, and a press in that
                // air used to pick the tin up.
                if (serveTin.texture != null && serveTin.texture.isReadable)
                    _serveShakerBody.alphaHitTestMinimumThreshold = 0.1f;
                // ...AND ITS CAP IS OFF (2026-09-04, the author: "bardağa koyarken
                // ucundaki kapak açık olmalı"). A cobbler shaker does not pour through
                // its cap: you lift the little cap and the drink comes out of the
                // strainer under it. This bench was drawing the lid SEATED, so a closed
                // shaker was tipping into the glass. `shaker_cap_pour` is that same lid
                // with the cap cut off at its own seam and the strainer opened — derived
                // from the sprite beside it rather than drawn again, so it is the same
                // lid to the pixel (Tools/shaker_cap_open.py). The closed lid stays the
                // fallback, because a missing plate should cost the look and not the pour.
                var capArt = ItemArt.Load("shaker_cap_pour") ?? ItemArt.Load("shaker_cap");
                if (capArt != null)
                {
                    var cap = NewRect("Cap", _serveShaker);
                    Stretch(cap, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    var capImg = cap.gameObject.AddComponent<Image>();
                    capImg.sprite = capArt;
                    capImg.preserveAspect = true;
                    capImg.raycastTarget = false;
                    _serveCapImg = capImg;
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
                    _aimText.text = UIText.T("bench.serve.aim.tin_open");
                    return;
                }
                _serveGrabbed = true;
                // The out parameter is ZERO when the point cannot be mapped, so the fallback is
                // chosen after the call, not before it.
                if (Mouse.current == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _serveSurface, Mouse.current.position.ReadValue(), null, out Vector2 held))
                    held = _serveHand.GripPoint;
                _serveHand.Press(held, _serveSurface.rect.height * 0.5f + HandAbove, ServeGlassTop() + ClearOverRim);
            Sfx.Play("tin_tip", 0.6f);
            });
            _serveShaker.gameObject.AddComponent<EventTrigger>().triggers.Add(sgrab);

            // The way back is the left-edge key now (the loop rework's one back, one place).
            AddEdgeBack(_servePanel);
            AddBinButton(_servePanel);      // see the shaker's, above
            // ENGRAVED like the strip's words (2026-09-16): the plaque stands left of the glass, so nothing needs
            // to draw over the glass any more.
            Engraved(_aimText);


            // THE LOUDEST KEY ON THE BENCH, IN THREE DRAWINGS (2026-09-17, the author: "Serve It tasarımı en
            // göz alıcı en dikkat çekici buton olmalı ... eğer bardak boşsa basılamaz renksiz gri tarzı,
            // basılabilir çıkıntılı 2.5d tarzı, basıldığı an tarzı. 3 adet şekli olmalı"). All three are the
            // author's own key pack's plate - the one place in this game a drawn button may come from a picture
            // rather than the procedural kit (GDD 14, the written exception) - repainted onto AMBER, which is
            // GDD 16's PrimaryAction and allowed one to a screen: proud of its shadow at rest, a row lower under
            // the finger, and cut from Night with its lamps out while the glass is empty (PushServeDone).
            //
            // AND IT ADVERTISES (2026-09-17's seventh list: "spot ampuller gibi yanıp sönen panayır ışığı"): a
            // ring of fairground lamps just inside the amber face, chasing round it. Nothing else on the bench
            // moves when it is not being touched, so the sign is the only thing pulling the eye - which is what
            // "en dikkat çekici" asks for, said in light rather than in size.
            //
            // AT THE ROW'S RIGHT, BESIDE THE BIN (2026-09-14): centred on the strip at 26..72 it sat under the
            // aim line once the bench's text came down for the props (measured: the line 439..841 x 32..55 over
            // the key 635..885 x 26..72). 880..1168 now, sixteen units short of the bin at 1184.
            // AT THE BOTTOM, DEAD CENTRE (2026-09-22, the author: "Serve it butonları hep sahnenin en altında en
            // ortada olmalı"): the one accent on the bench stands in the middle of the bottom row - the way out at
            // the left foot, the bin at the right - where the eye already is when the drink is finished. It hung
            // from the rail at the plaque's end for a day; a key you press last belongs at the bottom, not up in
            // the words.
            _serveDone = PackKeyFlow(_servePanel, "Done", UIText.T("bench.serve.serve_key"), null,
                KeyGo, new Vector2(0f, 0f), new Vector2(ServeKeyW, ServeKeyH), new Vector2(KeyRowX(2), BuiltKeyRowY),   // third in the bottom-left row (eighth list)
                () =>
                {
                    // Ready to hand over: close the flow, then click a seat to deliver - and
                    // the bench is put back the way it stood (2026-08-25).
                    if (Run.ServingGlass.IsEmpty) return;
                    ResetServeHand();
                    GoTo(Stage.Closed);
                    // ...AND THE CELLAR SHUTS BEHIND YOU (2026-08-26, the author: "serve it'e
                    // basıldığında kapak kapalı bir şekilde oyuna dönmeli"). The drawer was
                    // opened to reach the bottle and stayed open through the build, so SERVE IT
                    // dropped you into a room still standing on its shelves - with a drink in
                    // hand and a drinker waiting, which is exactly the moment the room should be
                    // a bar again. Only THIS door closes it: BACK TO THE BAR keeps the cellar
                    // open, because the way back is for reaching another bottle.
                    GetComponent<TycoonHud>()?.Room?.SetDrawerOpen(false);
                    Sfx.Play("serve_it", 1f);
                }, out _serveDoneBtn, out _serveDonePack, 16);
            RegisterFixed(_servePanel, _serveDone);   // the bottom row is the same row on every bench
            _serveDoneSink = _serveDone.GetComponent<PressSink>();
            _serveDoneGroup = _serveDone.gameObject.AddComponent<CanvasGroup>();
            // ONE LOUD LINE (2026-08-26, the author's screenshot: the old caption was
            // twenty-six 8px characters on a 240 plate - a whisper on the one key that
            // matters). What to do AFTER pressing it is the room's job to say, and the
            // room already says it over the standing drink. Dead centre on the face: the
            // key carries no glyph, so nothing pulls the word off the middle.
            var doneLabel = _serveDone.Find("Face/Label").GetComponent<Text>();
            doneLabel.font = _display;   // the display face on the one key that ends the drink, as it always had
            Stretch(doneLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(26f, 4f), new Vector2(-26f, 0f));
            // The word at 24 where it fits and 16 where it does not (SERVE IT does; a long translation may not).
            FitWord(doneLabel, new Vector2(ServeKeyW - 52f, ServeKeyH - 8f), 24, 16);
            doneLabel.transform.SetAsLastSibling();
        }

    }
}
