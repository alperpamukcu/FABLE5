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
    /// that fills it, and the finishing table. The one bottle this stage can hold is the
    /// one the wall hands over: fizz, whose only door is the serving glass because Core
    /// refuses it in the tin (GDD 21 §12).
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
        private RectTransform _serveGarnishRow; // mint/olive garnishes are added here (2026-07-23)

        /// <summary>One press of a mixer key, as a share of whatever glass is on the counter —
        /// so a splash into a coupe is a splash, not most of the drink.</summary>
        private const double MixerMeasure = 0.15;

        // ── the first-person staging (v5 P14, the author's diagram of 2026-07-31) ──
        //
        // One-point perspective, the room seen from where the bartender stands: the wall
        // across the back, and THE COUNTER — the panel itself — running under everything
        // else. There is no furniture drawn on it (2026-08-13): a table sprite was a
        // second surface inside the first, so the props had to stand on a picture of a
        // counter that sat on top of the counter. They stand on the counter now.

        /// <summary>
        /// The counter's stand line for the finishing props: a shallow diagonal from the
        /// near-left corner to the far right, in PANEL px from the bottom-left. It is the
        /// room's own perspective — the far end of a counter seen from behind it rides up
        /// and away — and props placed along it shrink toward that end, which is what makes
        /// the viewpoint read as depth rather than as a row of icons. The near end starts
        /// clear of the BACK TO BAR key (at x 51 the ice bucket, the most-reached-for tub,
        /// spent its whole life buried under that plate).
        /// </summary>
        private static readonly Vector2 StandNear = new Vector2(150f, 286f);
        private static readonly Vector2 StandFar = new Vector2(482f, 352f);
        private const float StandFarScale = 0.80f;

        /// <summary>
        /// How big each thing on the finishing shelf actually is. An ice bucket is not a salt
        /// cellar, and forcing all four into one row height made them read as four of the same
        /// button wearing different pictures — which is the opposite of a shelf of real things.
        /// Width and height in stage px, before the label.
        /// </summary>
        private static readonly Dictionary<string, Vector2> FinishProps = new Dictionary<string, Vector2>
        {
            ["ice"] = new Vector2(112, 84),           // a bucket you reach into with both hands
            ["lemon_twist"] = new Vector2(100, 74),   // a tub of wedges
            ["salt_rim"] = new Vector2(76, 54),       // a cellar
            ["sugar_rim"] = new Vector2(76, 54),
        };

        private static Vector2 FinishPropSize(string prepId) =>
            FinishProps.TryGetValue(prepId, out var s) ? s : new Vector2(100, 74);

        /// <summary>Room kept clear at the top for the title and the aim line, and at the bottom
        /// for the two buttons.</summary>
        private const float StageTop = 86f, StageBottom = 62f;

        /// <summary>How tall the shaker and the bottle in hand are drawn on THIS stage. The shaker
        /// bench's 180 left the tin looking like a thimble beside a 260-tall glass.</summary>
        private const float ServeVesselH = 250f;

        /// <summary>The piece being carried from the finishing shelf to the glass.</summary>
        private PreparationDefinition _servePrep;
        private string _servePrepLabel;
        private RectTransform _serveDragPiece;

        // The carried-piece spring (GDD 24 §2.4's weight, kept when the shaker bench lost
        // its own prep drag in the 2026-08-13 rebuild): the grip springs after the cursor
        // with a hint of overshoot and the piece hangs and swings from it as a pendulum.
        // 300/28 sits just under critical damping — it still leads and settles, it just
        // stops arguing with the hand (2026-08-11).
        private readonly Pendulum _dragSwing = new Pendulum();
        private Vector2 _dragPos;    // the grip's current position (lags the cursor)
        private Vector2 _dragVel;    // the grip's velocity (drives the spring and the swing)
        private const float DragStiffness = 300f;
        private const float DragDamping = 28f;

        private RectTransform _serveBottle;        // the bottle in hand
        private RectTransform _serveVessel;        // the bottle itself inside it, sized to its art
        private Image _serveBottleImage;
        private BottleFill _serveFill;             // what is left in it, behind the glass
        /// <summary>The cap of the bottle in hand, as an offset from the grip (VesselArt).</summary>
        private Vector2 _serveMouth;
        private IngredientCard _serveFocusBottle;
        private bool _serveBottleGrabbed;
        private Vector2 _serveBottleRest;
        /// <summary>Glass-fractions a second while a bottle is held over the glass.</summary>
        private const float GlassPourRate = 0.30f;
        private RectTransform _serveShaker;     // the grabbable shaker
        private Image _serveShakerBody;
        private MetaballFluid _serveFluid;      // the metaball liquid in the serving glass

        /// <summary>How much has gone into the glass since this bottle was picked up.</summary>
        private double _servePourTotal;

        /// <summary>
        /// A poured volume said the way a bar says it. A measure is <see cref="MixerMeasure"/> of
        /// the glass, which is the same unit the mixer keys used to pour in one press — so the
        /// number on screen and the recipe the judge grades against are counted in the same thing.
        /// </summary>
        private static string Measures(double volume) => $"{volume / MixerMeasure:0.0}";

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

            // The prep table finishes the drink: ice and the rims go on at the GLASS, so a
            // built drink can have them at all, then the stocked garnishes (mint, olive).
            // Everything stands ALONG THE TABLETOP, nearest thing biggest — the ice bucket
            // takes the near end because it is the thing you reach for most.
            foreach (Transform ch in _serveGarnishRow) Destroy(ch.gameObject);
            int standCount = 4;
            foreach (var bottle in run.Shelf.Bottles)
                if (bottle.Ingredient.Type == IngredientType.Garnish && !bottle.IsEmpty)
                    standCount++;
            int stand = 0;
            AddFinishTub("ice", "ICE", Preparations.Ice, TableStand(stand++, standCount));
            AddFinishTub("lemon_twist", "LEMON", Preparations.LemonTwist, TableStand(stand++, standCount));
            AddFinishTub("salt_rim", "SALT", Preparations.SaltRim, TableStand(stand++, standCount));
            AddFinishTub("sugar_rim", "SUGAR", Preparations.SugarRim, TableStand(stand++, standCount));
            foreach (var bottle in run.Shelf.Bottles)
                if (bottle.Ingredient.Type == IngredientType.Garnish && !bottle.IsEmpty)
                    AddGarnishChip(bottle.Ingredient, TableStand(stand++, standCount));

            // NO BOTTLES STAND ON THIS COUNTER. The hand bottle is whatever the player
            // carried in from the back bar, and it is put down by letting go of it.
            _serveBottleGrabbed = false;
            _serveFocusBottle = null;
            _serveBottle.gameObject.SetActive(false);
            _servePourTotal = 0;
        }

        /// <summary>Where the n-th finishing prop stands on the counter, and how big it is
        /// drawn: a position along the counter's stand line (panel space, from the
        /// bottom-left) and a depth scale that shrinks toward the far end.</summary>
        private (Vector2 pos, float depth) TableStand(int index, int count)
        {
            float t = count <= 1 ? 0f : index / (float)(count - 1);
            return (Vector2.Lerp(StandNear, StandFar, t), Mathf.Lerp(1f, StandFarScale, t));
        }

        /// <summary>
        /// One container on the finishing shelf. NOT a button (the author's brief, 2026-07-31):
        /// you reach into an open tub and drag a piece out, and it only goes in if you drop it
        /// in the glass. The drop only counts over the glass's mouth, so finishing a drink is
        /// an act of aiming rather than a click that could not miss.
        /// </summary>
        private void AddFinishTub(string prepId, string label, PreparationDefinition prep,
                                  (Vector2 pos, float depth) stand)
        {
            var run = Run;
            bool already = run != null && run.ServingGlass.HasPreparation(prep.Id);

            var size = FinishPropSize(prepId) * stand.depth;
            var tub = NewRect($"F_{prepId}", _serveGarnishRow);
            tub.anchorMin = tub.anchorMax = Vector2.zero;      // panel space, from the bottom-left
            tub.pivot = new Vector2(0.5f, 0f);                 // standing on the tabletop line
            tub.sizeDelta = size + new Vector2(24f, 20f);      // grab margin round the prop
            tub.anchoredPosition = stand.pos;
            var hit = tub.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);   // the whole tub is the grab target

            // It STANDS on the counter; the shadow is what says so now that no table is
            // drawn under it. Placed at the icon's own foot, inside the grab margin.
            AddContactShadow(tub, size.x * 0.86f, new Vector2(0, 16f - tub.sizeDelta.y * 0.5f));

            var icon = NewRect("Tub", tub);
            Place(icon, new Vector2(0.5f, 0), size, new Vector2(0, 14f));
            var iimg = icon.gameObject.AddComponent<Image>();
            iimg.sprite = ItemArt.Bucket(prepId) ?? ItemArt.Prep(prepId);
            iimg.preserveAspect = true; iimg.raycastTarget = false;
            if (iimg.sprite == null) iimg.color = UITheme.Cyan[3];
            else if (already) iimg.color = new Color(1f, 1f, 1f, 0.55f);

            var name = NewText("N", tub, _body, 8, TextAnchor.LowerCenter,
                already ? UITheme.Lime[4] : UITheme.TextPrimary);
            Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(96, 14), new Vector2(0, 0));
            name.text = label;
            if (already)
            {
                // A DRAWN tick, not one borrowed from the typeface (2026-08-11): the pixel
                // faces carry no such glyph, so it was set from a fallback font and read as
                // a stray mark rather than as a thing this bench had ticked off.
                var done = NewRect("Done", tub);
                Place(done, new Vector2(0.5f, 0), new Vector2(16, 16), new Vector2(-52f, 2f));
                var di = done.gameObject.AddComponent<Image>();
                di.sprite = ChromeArt.Mark("tick");
                di.color = UITheme.Lime[4]; di.raycastTarget = false;
                return;   // it is on the drink; the tub stops offering it
            }

            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                var r = Run;
                if (r == null) return;
                if (!r.CanFinishAtGlass)
                {
                    // The predicate is a phase check now (2026-08-10: preparations are
                    // volumeless) — the old "glass is full" line could only ever lie.
                    _aimText.text = "THE NIGHT IS OVER";
                    _aimText.color = UITheme.Amber[3];
                    return;
                }
                _servePrep = prep;
                _servePrepLabel = label;
                var dpImg = _serveDragPiece.GetComponent<Image>();
                dpImg.sprite = ItemArt.Prep(prepId);
                dpImg.color = dpImg.sprite != null ? Color.white : UITheme.Cyan[3];
                _dragSwing.Reset();
                Vector2 start = _serveDragPiece.anchoredPosition;
                if (Mouse.current != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _serveSurface, Mouse.current.position.ReadValue(), null, out Vector2 l0))
                    start = l0;
                _dragPos = start;
                _dragVel = Vector2.zero;
                _serveDragPiece.anchoredPosition = _dragPos;
                _serveDragPiece.localRotation = Quaternion.identity;
                _serveDragPiece.gameObject.SetActive(true);
            });
            tub.gameObject.AddComponent<EventTrigger>().triggers.Add(down);
            Pressable(tub, icon, iimg, lift: 5f, depth: 5f);   // the tub tips toward you before you reach in
        }

        /// <summary>
        /// Carries a piece from the shelf to the glass. The grip springs after the cursor with
        /// overshoot and the piece swings from it — and the drop only counts over the glass's
        /// mouth, so finishing a drink is an act of aiming rather than a click that could not
        /// miss.
        /// </summary>
        private void UpdateServePrepDrag(TycoonRun run)
        {
            if (_servePrep == null || Mouse.current == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _serveSurface, Mouse.current.position.ReadValue(), null, out Vector2 cursor);

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            _dragVel += (cursor - _dragPos) * (DragStiffness * dt);
            _dragVel *= Mathf.Exp(-DragDamping * dt);
            _dragPos += _dragVel * dt;
            _dragSwing.Step(dt, _dragVel);
            _serveDragPiece.anchoredPosition = _dragPos;
            _serveDragPiece.localRotation = Quaternion.Euler(0, 0, _dragSwing.Angle);

            if (Mouse.current.leftButton.isPressed) return;

            var opening = _serveGlass.anchoredPosition
                        + new Vector2(0, _serveGlass.rect.height * (_serveGlassPiece.RimY - 0.5f));
            bool inMouth = Mathf.Abs(_dragPos.x - opening.x) < 80f
                        && Mathf.Abs(_dragPos.y - opening.y) < 80f;
            if (inMouth && !run.CanFinishAtGlass)
            {
                _aimText.text = "THE NIGHT IS OVER";
                _aimText.color = UITheme.Amber[3];
            }
            else if (inMouth)
            {
                run.AddPreparationAtGlass(_servePrep);
                Sfx.Play(_servePrep != null && _servePrep.Id == "ice" ? "ice_drop" : "garnish");
                // The drink takes the hit, and the touch appears ON the glass (GlassDecor):
                // the crust on the rim, the wedge on the edge, the ice at the liquid line.
                _serveFluid.Ripple(opening.x, 0.03f);
                GlassDecor.Sync(_serveGlass, _serveGlassPiece, run.ServingGlass, run);
                string label = _servePrepLabel;
                _servePrep = null;
                _serveDragPiece.gameObject.SetActive(false);
                RefreshServe();   // the tub becomes a tick, so the shelf does have to rebuild
                _aimText.text = $"{label} IN THE GLASS";
                _aimText.color = UITheme.Cyan[3];
                return;
            }
            _servePrep = null;
            _serveDragPiece.gameObject.SetActive(false);
        }

        private void AddGarnishChip(IngredientCard card, (Vector2 pos, float depth) stand)
        {
            var size = new Vector2(72f, 84f) * stand.depth;    // a jar on the table, not a key
            var chip = NewRect($"G_{card.Id}", _serveGarnishRow);
            chip.anchorMin = chip.anchorMax = Vector2.zero;
            chip.pivot = new Vector2(0.5f, 0f);
            chip.sizeDelta = size + new Vector2(20f, 18f);
            chip.anchoredPosition = stand.pos;
            var bg = chip.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.001f);          // the jar is the button; no plate
            AddContactShadow(chip, size.x * 0.86f, new Vector2(0, 16f - chip.sizeDelta.y * 0.5f));
            var icon = NewRect("Icon", chip);
            Place(icon, new Vector2(0.5f, 0), size, new Vector2(0, 14f));
            var iimg = icon.gameObject.AddComponent<Image>();
            iimg.sprite = ItemArt.Bottle(card); iimg.preserveAspect = true; iimg.raycastTarget = false;
            if (iimg.sprite == null) iimg.color = UITheme.StyleColor(card.Info?.Style, card.Type);
            var name = NewText("N", chip, _body, 8, TextAnchor.LowerCenter, UITheme.TextPrimary);
            Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(92, 14), new Vector2(0, 2));
            name.text = RailLabel(card);
            var btn = chip.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var c = card;
            // The garnish goes into the shaker, before the pour — so a shaker filled to the brim
            // has nowhere to put it, and says so rather than swallowing the click (2026-07-28).
            btn.onClick.AddListener(() =>
            {
                if (Run == null) return;
                if (Run.Glass.IsEmpty)
                {
                    // Said, not swallowed (audit 2026-08-11): the jars stand on the SERVE
                    // bench but pour into the TIN, which is usually already empty here —
                    // a silent click read as "mint is broken" instead of "wrong order".
                    if (_aimText != null)
                    {
                        _aimText.text = "THE TIN IS EMPTY — GARNISH GOES IN BEFORE THE POUR-OUT";
                        _aimText.color = UITheme.Amber[3];
                    }
                    return;
                }
                if (Run.PourGarnish(c.Id) <= 0)
                {
                    _aimText.text = "THE SHAKER IS FULL — NO ROOM FOR A GARNISH";
                    _aimText.color = UITheme.Amber[3];
                    return;
                }
                RefreshServe();
            });
            Pressable(chip, icon, iimg, lift: 4f, depth: 4f);
        }

        /// <summary>
        /// What a jar is called at prop scale: the STYLE, not the brand. You reach for mint,
        /// not for Fresh Mint — and the brand would not fit under an 84px jar anyway. Falls
        /// back to the brand for anything with no style to speak of.
        /// </summary>
        private static string RailLabel(IngredientCard card)
        {
            string style = card.Info?.Style;
            if (string.IsNullOrEmpty(style)) return card.Name.ToUpperInvariant();
            return style.Replace('_', ' ').ToUpperInvariant();
        }

        /// <summary>
        /// Puts a bottle in the hand at the counter. Only the back bar sends one — no
        /// bottle stands on this stage to be picked up (2026-08-13, the author: nothing to
        /// pour is chosen here). It arrives with NO BUTTON HELD, which is the whole reason
        /// this is not a grab: a hand that is already open would drop it on the first frame
        /// (the route was dead code until the wall began carrying fizz, so it had never
        /// been played). It stands in the hand, and pressing it closes the hand.
        /// </summary>
        private void TakeCabinetBottle(TycoonRun run, IngredientCard c)
        {
            _serveFocusBottle = c;
            _serveBottleGrabbed = false;
            _serveBottleImage.sprite = ItemArt.Bottle(c);
            _serveBottleImage.color = _serveBottleImage.sprite != null
                ? Color.white : UITheme.StyleColor(c.Info?.Style, c.Type);
            // Sized and stood by its own drawing, which is also where its cap is found.
            _serveMouth = VesselArt.StandOn(_serveVessel, new Vector2(0.5f, 0f),
                _serveBottleImage.sprite, ServeVesselH, Vector2.zero);
            PushServeFill(run);
            _servePourTotal = 0;
            _serveBottle.anchoredPosition = _serveBottleRest;
            _serveBottle.localRotation = Quaternion.identity;
            _serveBottle.gameObject.SetActive(true);
            Sfx.Play("bottle_open", 0.8f);
            _aimText.text = $"{c.Name.ToUpperInvariant()} IN HAND — GRAB IT AND TIP IT OVER THE GLASS";
            _aimText.color = UITheme.TextSecondary;
        }

        /// <summary>How the back bar passes a carbonated bottle to this stage: fizz never
        /// enters the tin (GDD 21 §12) and the serving glass is its only door, so the wall
        /// sends it here already in hand. A bottle the bar does not stock is a no-op.</summary>
        private void TakeFromCabinet(string ingredientId)
        {
            var run = Run;
            if (run == null) return;
            var card = run.Shelf.Find(ingredientId)?.Ingredient;
            if (card == null) return;
            TakeCabinetBottle(run, card);
        }

        /// <summary>
        /// Puts the bottle down — it goes back to the wall it came from, which is where it
        /// is picked up again. Nothing else may clear <see cref="_serveFocusBottle"/>: the
        /// hand and the pour both read it, and a bottle cleared from under a live pour
        /// leaves a stream coming out of nothing.
        /// </summary>
        private void PutTheBottleBack(TycoonRun run)
        {
            _serveBottleGrabbed = false;
            _serveFocusBottle = null;
            // Drops still falling are the glass's drink now — the next bottle the wall
            // sends over must not reach back and recolour them.
            _serveFluid.ClearStreamColor();
            _serveBottle.gameObject.SetActive(false);
            _serveBottle.localRotation = Quaternion.identity;
            _serveBottle.anchoredPosition = _serveBottleRest;
            if (run != null) RefreshServeText(run, 1.0);
        }

        /// <summary>Pours whatever bottle is in hand. The tilt and the aim are the shaker's,
        /// to the letter — one bar, one way of pouring.</summary>
        private void UpdateServeCabinet(TycoonRun run)
        {
            if (_serveFocusBottle == null || Mouse.current == null)
            {
                PushServeDone(run);
                return;
            }
            // Letting go puts the bottle back on the shelf. It used to only drop the grab, which
            // left the bottle floating where the cursor happened to be while its twin stood in
            // the case — you could never put one down, only abandon it mid-air.
            if (_serveBottleGrabbed && !Mouse.current.leftButton.isPressed)
            {
                PutTheBottleBack(run);
                PushServeDone(run);
                return;
            }

            bool pourNow = false;
            if (_serveBottleGrabbed &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _serveSurface, Mouse.current.position.ReadValue(), null, out Vector2 local))
            {
                float halfW = _serveSurface.rect.width * 0.5f;
                float halfH = _serveSurface.rect.height * 0.5f;
                local.x = Mathf.Clamp(local.x, -halfW + 30f, halfW - 30f);
                local.y = Mathf.Clamp(local.y, -halfH + 20f, halfH - 20f);
                _serveBottle.anchoredPosition = local;

                float lift = Mathf.Clamp01((local.y - _serveBottleRest.y) / LiftRange);
                float tilt = lift * MaxTilt;
                _serveBottle.localRotation = Quaternion.Euler(0, 0, tilt);

                // The cap, swung around the grip — measured off this bottle's own art, the
                // shaker bench's line to the letter (VesselArt, 2026-08-11).
                Vector2 mouth = local + VesselArt.Swing(_serveMouth, tilt);
                var opening = _serveGlass.anchoredPosition
                            + new Vector2(0, _serveGlass.rect.height * (_serveGlassPiece.RimY - 0.5f));
                bool over = Mathf.Abs(mouth.x - opening.x) < 78f && mouth.y > opening.y - 30f;
                bool full = run.ServingGlass.IsFull;
                pourNow = tilt > 42f && over && !full;
                if (full && tilt > 42f && over)
                {
                    _aimText.text = "THE GLASS IS FULL";
                    _aimText.color = UITheme.Amber[3];
                }

                if (pourNow)
                {
                    // The LIQUID's colour, not the shelf tag's. StyleColor is the vivid identity
                    // hue the shelf is read by — amaro's is navy — and pouring with it painted the
                    // drink a colour it never is, then snapped to the real one the next time the
                    // stage refreshed (the author, 2026-08-03: navy while pouring, pale blue after
                    // a trip through the menu). It goes on the STREAM, so the falling drink keeps
                    // its own colour until it lands.
                    _serveFluid.SetStreamColor(
                        UITheme.LiquidColor(_serveFocusBottle.Info?.Style, _serveFocusBottle.Type));
                    var streamVel = new Vector2((opening.x - mouth.x) * 1.8f, -225f);
                    _serveFluid.EmitStream(mouth, streamVel, Time.deltaTime);
                }
            }

            if (pourNow) _servePouringNow = true;
            if (pourNow)
            {
                double landed = run.PourAtGlass(_serveFocusBottle.Id, GlassPourRate * Time.deltaTime);
                _servePourTotal += landed;
                _serveFluid.SetColor(DrinkColor(run.ServingGlass));
                RefreshServeText(run, 1.0);
                // How much has gone in. Without it the pour was a held button with no number on
                // it, and the only way to learn a measure was to serve the drink and be told.
                _aimText.text = $"{_serveFocusBottle.Name.ToUpperInvariant()}  ·  " +
                                $"POURED {Measures(_servePourTotal)}  ·  GLASS {run.ServingGlass.FillFraction:P0}";
                _aimText.color = UITheme.Cyan[3];
                // It drains as it pours, so the level is pushed on every frame that moved
                // any — a level set once on pick-up would be a lie by the second measure.
                // Only on those frames: nothing about a bottle merely held over the glass
                // can change what is left in it.
                PushServeFill(run);
                if (landed <= 0)
                {
                    // The bottle ran dry mid-pour: put it down and rebuild the shelf without it.
                    PutTheBottleBack(run);
                    RefreshServe();
                    return;
                }
            }

            PushServeDone(run);
        }

        /// <summary>The SERVE key answers only a glass with a drink in it — dim until then.
        /// Driven every frame from the cabinet update, which always runs on this stage.</summary>
        private void PushServeDone(TycoonRun run)
        {
            if (_serveDoneGroup == null || run == null) return;
            bool ready = !run.ServingGlass.IsEmpty;
            _serveDoneGroup.alpha = ready ? 1f : 0.45f;
            if (_serveDoneBtn != null) _serveDoneBtn.interactable = ready;
        }

        /// <summary>How full the bottle in hand is, read off the shelf it came from.</summary>
        private void PushServeFill(TycoonRun run)
        {
            if (_serveFill == null) return;
            if (_serveFocusBottle == null) { _serveFill.Hide(); return; }
            var stock = run?.Shelf?.Find(_serveFocusBottle.Id);
            _serveFill.Show(_serveBottleImage.sprite,
                UITheme.LiquidColor(_serveFocusBottle.Info?.Style, _serveFocusBottle.Type),
                stock != null && stock.Capacity > 0 ? stock.Remaining / stock.Capacity : 0.0);
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
            bool dressed = run.ServingGlass.PreparationSteps.Count > 0;
            PaintSteps(_serveStepRows, poured ? 2 : 0, 1, poured && dressed);
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
                        _aimText.text = "IT WANTS A MIX — BACK TO THE SHAKER";
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
            _servePanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_servePanel);

            var title = NewText("Title", _servePanel, _display, 16, TextAnchor.UpperCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -40), new Vector2(0, -10));
            title.text = "POUR THE GLASS";

            // The two corner readouts wear the top corners at 8px; the aim line gets its own
            // full-width band UNDER them at 16 — they used to share one band and long aim
            // strings ran under the corner numbers.
            // THE SAME CARD THE BENCH WEARS (2026-08-14, the author: "aynı öğreticiyi
            // bardağa koyma sahnesi içinde oluştur"). Top-left, ahead of the tin's line,
            // which moves down under it — the left column now reads what to do, then what
            // is in the tin, in that order.
            BuildStepCard(_servePanel, "THE COUNTER",
                new[] { "toglass", "garnish", "serve" },
                // No ampersand: the pixel face draws & as $ (measured in play, 2026-08-14).
                new[] { "TIP THE TIN", "ICE AND GARNISH", "CARRY IT OVER" },
                _serveStepRows, new Vector2(20, -18));

            _serveShakerText = NewText("Shaker", _servePanel, _body, 8, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(_serveShakerText.rectTransform, new Vector2(0, 1), new Vector2(280, 12), new Vector2(20, -132));
            _serveGlassText = NewText("Glass", _servePanel, _body, 8, TextAnchor.UpperRight, UITheme.TextPrimary);
            Place(_serveGlassText.rectTransform, new Vector2(1, 1), new Vector2(280, 12), new Vector2(-20, -44));

            // VERTICAL and engine-drawn (2026-08-02): the GLASS's contents as shares of
            // the vessel, magenta-edged where the shaker's column is cyan. Against the
            // RIGHT WALL now: at 348 it stood where the fridge used to hide it, and with
            // the fridge gone it hung in the middle of an empty room instead — with the
            // bottle in hand resting a hair to its left, which is exactly where a gauge
            // must not be.
            var serveTrack = NewRect("MixTrack", _servePanel);
            Place(serveTrack, new Vector2(0.5f, 0.5f), new Vector2(44, 300), new Vector2(575, -8));
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
            AddBenchWall(_servePanel, 0.68f);

            // The props' container spans the whole panel: everything in it is placed in
            // panel space, on the counter's own stand line. No layout groups — a room is
            // not a list.
            _serveGarnishRow = NewRect("TableTop", _servePanel);
            Stretch(_serveGarnishRow, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _aimText = NewText("AimText", _servePanel, _body, 16, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(_aimText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -78), new Vector2(0, -56));

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
            var glassRest = new Vector2(-110, -88);
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

            // The piece in hand between the shelf and the glass.
            _serveDragPiece = NewRect("DragPiece", _serveSurface);
            _serveDragPiece.pivot = new Vector2(0.5f, 1f);
            _serveDragPiece.sizeDelta = new Vector2(76, 84);   // in scale with the tub it came out of
            var sdp = _serveDragPiece.gameObject.AddComponent<Image>();
            sdp.preserveAspect = true; sdp.raycastTarget = false;
            _serveDragPiece.gameObject.SetActive(false);

            // The bottle in hand, once one is taken off the shelf.
            _serveBottleRest = new Vector2(226, -96);
            _serveBottle = NewRect("HandBottle", _serveSurface);
            _serveBottle.pivot = new Vector2(0.5f, 0.22f);
            _serveBottle.sizeDelta = new Vector2(118, ServeVesselH);
            _serveBottle.anchoredPosition = _serveBottleRest;
            // An invisible plate over the whole slot, so a bottle STANDING in the hand can
            // be grabbed again — the wall and the rail hand fizz over with no button held,
            // and a bottle you cannot take hold of is a bottle you cannot pour.
            var handHit = _serveBottle.gameObject.AddComponent<Image>();
            handHit.color = new Color(0, 0, 0, 0.001f);
            var handGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            handGrab.callback.AddListener(_ =>
            {
                if (_serveFocusBottle != null && Run != null && Run.Phase == TycoonPhase.DayOpen)
                    _serveBottleGrabbed = true;
            });
            _serveBottle.gameObject.AddComponent<EventTrigger>().triggers.Add(handGrab);
            // The drink first, then the art in a CHILD of its own. It cannot stay on the
            // hand rect itself: a parent Graphic draws before its children, so the bottle
            // would have gone behind its own contents however the fill was ordered.
            _serveVessel = NewRect("Vessel", _serveBottle);
            _serveFill = BottleFill.Under(_serveVessel);
            var serveArt = NewRect("Art", _serveVessel);
            Stretch(serveArt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _serveBottleImage = serveArt.gameObject.AddComponent<Image>();
            _serveBottleImage.preserveAspect = true;
            _serveBottleImage.raycastTarget = false;
            _serveBottle.gameObject.SetActive(false);
            _serveGlass.SetAsLastSibling();   // the hollow glass draws over the fluid

            // The grabbable steel shaker you pour from, resting lower-right.
            _serveShakerRest = new Vector2(96, -96);
            _serveShaker = NewRect("Shaker", _serveSurface);
            _serveShaker.pivot = new Vector2(0.5f, 0.22f);
            _serveShaker.sizeDelta = new Vector2(146, ServeVesselH);
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
                if (Run == null || Run.Phase != TycoonPhase.DayOpen || Run.Glass.IsEmpty) return;
                // WHAT WENT THROUGH THE TIN WAS WORKED IN THE TIN (2026-08-14, the author: an
                // uncapped shaker was reaching the glass). The bench's own key already refuses
                // to let an open tin leave — but the glass has other doors into it (the wall
                // routes a fizz bottle straight here), and the tin rode along through them
                // with its lid still on the counter. One law, both doors.
                if (!_capped)
                {
                    _aimText.text = "THAT TIN IS STILL OPEN — CAP IT AND WORK IT AT THE BENCH";
                    return;
                }
                _serveGrabbed = true;
            });
            _serveShaker.gameObject.AddComponent<EventTrigger>().triggers.Add(sgrab);

            // The way back is the left-edge key now (the loop rework's one back, one place).
            AddEdgeBack(_servePanel);

            var done = NewRect("Done", _servePanel);
            Place(done, new Vector2(0.5f, 0), new Vector2(240, 34), new Vector2(130, 12));
            _serveDoneBtn = done.gameObject.AddComponent<Button>();
            _serveDoneBtn.onClick.AddListener(() =>
            {
                // Ready to hand over: close the flow, then click a seat to deliver.
                if (!Run.ServingGlass.IsEmpty) GoTo(Stage.Closed);
            });
            _serveDoneGroup = done.gameObject.AddComponent<CanvasGroup>();
            var doneFace = NewRect("Face", done);
            Stretch(doneFace, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(done, UITheme.PrimaryAction, _serveDoneBtn, doneFace);   // GDD 16 §2
            var doneLabel = NewText("Label", doneFace, _body, 8, TextAnchor.MiddleCenter, UITheme.TextOnAmber);
            Stretch(doneLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, KeyPlate.Throw), new Vector2(-4, 0));
            doneLabel.text = "SERVE IT · CLICK A CUSTOMER";
        }

    }
}
