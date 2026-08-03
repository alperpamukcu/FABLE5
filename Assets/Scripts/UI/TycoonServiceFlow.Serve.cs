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
    /// The serve stage (GDD 24 §3): grab the shaker and tip it over the glass. How
    /// well the mouth lines up is the aim — off-centre spills, and what spills is
    /// gone.
    /// </summary>
    public sealed partial class TycoonServiceFlow
    {

        // The serve pour uses the same tilt model (GDD 24 §3): grab the shaker, tip it over
        // the glass. How well the mouth lines up over the glass is the aim — off-centre spills.
        private Text _serveShakerText;
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
        private Image _serveGlassBack;
        private RectTransform _serveMixBar;
        private string _serveMixSig = "";
        private GlassArt.Piece _serveGlassPiece;
        private RectTransform _serveGarnishRow; // mint/olive garnishes are added here (2026-07-23)
        private RectTransform _serveMixerRow;   // mixers and juices go in AT THE GLASS (v5 P14)

        /// <summary>One press of a mixer key, as a share of whatever glass is on the counter —
        /// so a splash into a coupe is a splash, not most of the drink.</summary>
        private const double MixerMeasure = 0.15;

        // ── the first-person staging (v5 P14, the author's diagram of 2026-07-31) ──
        //
        // One-point perspective, the room seen from where the bartender stands: the prep
        // table runs along the LEFT wall and recedes to the upper right, the drinks fridge
        // stands against the RIGHT wall angled front-down-left, and the counter with the
        // glass and the shaker opens toward the viewer between them. The horizon sits at
        // y ≈ 0.51 of the panel. Everything the player touches STANDS ON the furniture —
        // positions come from the art, measured off it the way every anchor here is.
        //
        // The furniture is shot EMPTY (bare top, bare shelves), because anything painted
        // into it would be a picture: the props standing on it are separate sprites whose
        // positions are hit targets and whose levels the game draws.

        /// <summary>The prep table's scale on screen, and where its bottom-left corner sits
        /// (from the panel's bottom-left). The art is 298×356.</summary>
        private const float TableScale = 1.5f;
        private static readonly Vector2 TableFoot = new Vector2(-16f, 26f);

        /// <summary>
        /// The stand line along the tabletop, measured off the art: the top face's midline
        /// runs from (45,160) at the near end to (250,45) at the far end (art px, y from the
        /// top). Props are placed along it and scale down as they recede — that shrink is
        /// what makes the viewpoint read as depth rather than as a tilted picture.
        /// </summary>
        private static readonly Vector2 TableNear = new Vector2(45f, 160f);
        private static readonly Vector2 TableFar = new Vector2(250f, 45f);
        private const float TableFarScale = 0.74f;

        /// <summary>The fridge's scale, and its bottom-RIGHT corner from the panel's
        /// bottom-right. At 1.9 the art's three shelves all stay on screen — the 07-31 note
        /// wanted bottles at hand size with the fridge overflowing the screen, but the
        /// 08-02 ruling on the same cabinet (every bottle in frame, one press away) is later
        /// and it won: a fridge you scroll blind is the thing that was just torn out.</summary>
        private const float FridgeScale = 1.9f;
        private static readonly Vector2 FridgeFoot = new Vector2(6f, 18f);

        /// <summary>The wire shelves' front edges, measured off the fridge art (y from the
        /// top of its 198×334 canvas), and the glass interior's x span. A shelf's edge falls
        /// toward the near (left) end because the case is angled — bottles follow it.</summary>
        private static readonly float[] FridgeShelfY = { 120f, 188f, 249f, 296f };
        private const float FridgeShelfDrop = 14f;   // how much lower a shelf sits at its left end
        private const float FridgeInteriorL = 72f, FridgeInteriorR = 184f;
        private const int FridgePerShelf = 3;

        /// <summary>How tall a bottle stands INSIDE the fridge. Taking one out grows it to
        /// <see cref="ServeVesselH"/> — the pick-up toward the camera.</summary>
        private const float FridgeBottleH = 112f;

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

        /// <summary>What stands in for the fridge if its art ever fails to load.</summary>
        private static readonly Color CabinetFrame = new Color(0.20f, 0.17f, 0.22f, 1f);

        /// <summary>The piece being carried from the finishing shelf to the glass.</summary>
        private PreparationDefinition _servePrep;
        private string _servePrepLabel;
        private RectTransform _serveDragPiece;

        // The fizzy-drinks cabinet (the author's sketch, 2026-07-31): bottles standing at their
        // own proportions behind a glass door. The door opens, a bottle comes out in your hand,
        // and you tip it over the glass — the same verb the shaker's bottles use, so a mixer is
        // POURED rather than clicked, and the measure is how long you hold it.
        private RectTransform _serveCabinet, _serveCabinetShelf;
        private RectTransform _serveBottle;        // the bottle in hand
        private Image _serveBottleImage;
        private IngredientCard _serveFocusBottle;
        private bool _serveBottleGrabbed;
        private Vector2 _serveBottleRest;
        /// <summary>Glass-fractions a second while a bottle is held over the glass.</summary>
        private const float GlassPourRate = 0.30f;
        private RectTransform _serveShaker;     // the grabbable shaker
        private Image _serveShakerBody;
        private MetaballFluid _serveFluid;      // the metaball liquid in the serving glass
        private Splasher _serveSplash;

        /// <summary>The shelf slot standing empty while its bottle is in your hand.</summary>
        private RectTransform _serveCabinetGap;
        private Image _serveHandLiquid;
        private string _serveHandStyle;

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
            // The pool is the drink IN THE GLASS, not the one in the shaker. Those are the same
            // thing while you are tipping one into the other, which is why nobody noticed — but
            // a built drink leaves the shaker empty, and the pool was taking the colour of
            // nothing and drawing a soda as pale tan.
            _serveFluid.SetColor(DrinkColor(run.ServingGlass.IsEmpty ? run.Glass : run.ServingGlass));
            ShowServingGlassware(run);
            RefreshServeMixBar(run);
            PushServePool(run);
            GlassDecor.Sync(_serveGlass, _serveGlassPiece, run.ServingGlass, run);
            _serveShakerBody.color = DrinkColor(run.Glass);
            _serveShaker.gameObject.SetActive(!run.Glass.IsEmpty);
            _aimText.text = run.Glass.IsEmpty
                ? "BUILD IT IN THE GLASS · MIXERS ON THE RIGHT"
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

            // The fridge: the mixers and juices, standing on its wire shelves.
            _serveBottleGrabbed = false;
            _serveFocusBottle = null;
            _serveBottle.gameObject.SetActive(false);
            _serveCabinetGap = null;      // the shelf is about to be rebuilt; the old slot is gone
            _servePourTotal = 0;
            foreach (Transform ch in _serveCabinetShelf) Destroy(ch.gameObject);
            int slot = 0;
            foreach (var bottle in run.Shelf.Bottles)
                if (IsGlassSide(bottle.Ingredient) && !bottle.IsEmpty)
                    AddCabinetBottle(bottle.Ingredient, slot++);
        }

        /// <summary>Where the n-th thing on the prep table stands, and how big it is drawn:
        /// a position along the measured tabletop line (panel space, from the bottom-left)
        /// and a depth scale that shrinks toward the far end.</summary>
        private (Vector2 pos, float depth) TableStand(int index, int count)
        {
            float t = count <= 1 ? 0f : index / (float)(count - 1);
            float x = TableFoot.x + Mathf.Lerp(TableNear.x, TableFar.x, t) * TableScale;
            float y = TableFoot.y + (356f - Mathf.Lerp(TableNear.y, TableFar.y, t)) * TableScale;
            return (new Vector2(x, y), Mathf.Lerp(1f, TableFarScale, t));
        }

        /// <summary>
        /// One container on the finishing shelf. NOT a button (the author's brief, 2026-07-31):
        /// you reach into an open tub and drag a piece out, and it only goes in if you drop it
        /// in the glass. The shaker bench has worked this way since 2026-07-23; this is the same
        /// verb aimed at the serving glass, so the two stages read as one bar.
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
            name.text = already ? "✓ " + label : label;
            if (already) return;   // it is on the drink; the tub stops offering it

            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                var r = Run;
                if (r == null) return;
                if (!r.CanFinishAtGlass)
                {
                    _aimText.text = "THE GLASS IS FULL — NO ROOM TO FINISH IT";
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
        /// overshoot and the piece swings from it, the same weight the shaker bench has — and
        /// the drop only counts over the glass's mouth, so finishing a drink is an act of
        /// aiming rather than a click that could not miss.
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
                _aimText.text = "THE GLASS IS FULL — NO ROOM TO FINISH IT";
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
                if (Run == null || Run.Glass.IsEmpty) return;
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
        /// Whether this belongs on the right rail — the things a drink is finished with at the
        /// glass rather than built with in the shaker. Carbonated ingredients are here because
        /// Core refuses them anywhere else; juices and the rest of the mixers are here because
        /// that is where a built drink is made, and they can still go in the shaker for a
        /// shaken one. Beer is never here: it comes off the tap.
        /// </summary>
        private static bool IsGlassSide(IngredientCard card)
        {
            if (card.Type == IngredientType.Beer) return false;
            if (card.Info != null && card.Info.Carbonated) return true;
            string category = card.Info?.Category;
            return category == IngredientCategories.Mixer || category == IngredientCategories.Juice;
        }

        /// <summary>
        /// What a rail bottle is called at rail scale: the STYLE, not the brand. You reach for
        /// tonic, not for Quinbury — and "Quinbury Tonic" does not fit under a 62px bottle
        /// anyway. Falls back to the brand for anything with no style to speak of.
        /// </summary>
        private static string RailLabel(IngredientCard card)
        {
            string style = card.Info?.Style;
            if (string.IsNullOrEmpty(style)) return card.Name.ToUpperInvariant();
            return style.Replace('_', ' ').ToUpperInvariant();
        }

        /// <summary>One mixer key on the right rail: a measure straight into the serving glass
        /// (v5 P14 / the P10 `PourAtGlass` verb).</summary>
        /// <summary>
        /// One bottle standing in the cabinet. Not a key: press it and the bottle comes out in
        /// your hand, and it is poured by being tipped over the glass, so the measure is how
        /// long you hold it there. Sized off its own art, so a squat cola bottle and a tall
        /// siphon stand at their true proportions behind the door.
        /// </summary>
        private void AddCabinetBottle(IngredientCard card, int index)
        {
            // Which wire shelf, which place on it, which rank. The front rank fills all
            // three shelves first; what will not fit stands BEHIND the front rank — a shelf
            // with two ranks reads as a stocked fridge, and the depth stays playable because
            // hovering a bottle pulls it in front of whatever it stands behind.
            int perRank = FridgePerShelf * (FridgeShelfY.Length - 1);
            int rank = index / perRank;
            int inRank = index % perRank;
            int shelfRow = inRank / FridgePerShelf;
            int col = inRank % FridgePerShelf;

            // The stand point, from the fridge art: x across the interior, y on the shelf's
            // front edge, which falls toward the near (left) end because the case is angled.
            float tx = (col + 0.5f) / FridgePerShelf;
            float artX = Mathf.Lerp(FridgeInteriorL + 8f, FridgeInteriorR - 8f, tx);
            float artY = FridgeShelfY[shelfRow] - FridgeShelfDrop * (1f - tx);
            if (rank > 0) { artX += 7f; artY -= 6f; }          // the back rank, up and right
            float x = -FridgeFoot.x - (198f - artX) * FridgeScale;   // from the panel's RIGHT
            float y = FridgeFoot.y + (334f - artY) * FridgeScale;
            float h = FridgeBottleH * (rank > 0 ? 0.88f : 1f);

            var slot = NewRect($"M_{card.Id}", _serveCabinetShelf);
            slot.anchorMin = slot.anchorMax = new Vector2(1f, 0f);   // panel space, bottom-right
            slot.pivot = new Vector2(0.5f, 0f);
            slot.sizeDelta = new Vector2(h * 0.62f, h);
            slot.anchoredPosition = new Vector2(x, y);
            if (rank > 0) slot.SetAsFirstSibling();            // behind the front rank
            var hit = slot.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);   // the whole slot takes the press

            var art = NewRect("Bottle", slot);
            Stretch(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = art.gameObject.AddComponent<Image>();
            img.sprite = ItemArt.Bottle(card);
            img.preserveAspect = true; img.raycastTarget = false;
            if (img.sprite == null) img.color = UITheme.StyleColor(card.Info?.Style, card.Type);

            // What is left in it, so a mixer that is running out says so behind the glass door.
            var shelfBottle = Run?.Shelf.Find(card.Id);
            var liquid = BottleArt.AddLiquid(art, card);
            if (liquid != null && shelfBottle != null && shelfBottle.Capacity > 0)
                liquid.fillAmount = BottleArt.For(card)
                    .FillAmount((float)(shelfBottle.Remaining / shelfBottle.Capacity));

            // The label is the STYLE, not the brand: at fridge scale "TONIC" is what you are
            // reaching for, and "Quinbury Tonic" would not fit anyway. Hover only — nine
            // names under nine bottles is a spreadsheet, not a fridge.
            var name = NewText("N", slot, _body, 8, TextAnchor.LowerCenter, UITheme.TextPrimary);
            Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(74, 11), new Vector2(0, -12));
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.text = RailLabel(card);
            name.gameObject.SetActive(false);

            Pressable(slot, art, img, lift: 5f, depth: 5f);

            var c = card;
            var trig = slot.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                var run = Run;
                if (run == null) return;
                TakeCabinetBottle(run, c, art);
            });
            trig.triggers.Add(down);
            // Hover pulls the bottle to the front of anything it stands behind (the rank-2
            // reach), and names it. It stays in front until the shelf next rebuilds, which
            // is the cheap version of putting it back where it stood — nothing else needs
            // the exact sibling order once the pointer has left.
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { slot.SetAsLastSibling(); name.gameObject.SetActive(true); });
            trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => name.gameObject.SetActive(false));
            trig.triggers.Add(exit);
        }

        /// <summary>Takes a cabinet bottle into the hand — the slot press and the menu's
        /// carbonated routing both come through here, so there is exactly one way a bottle
        /// leaves that shelf.</summary>
        private void TakeCabinetBottle(TycoonRun run, IngredientCard c, RectTransform art)
        {
            _serveFocusBottle = c;
            _serveBottleGrabbed = true;
            // The bottle leaves the shelf. It used to stay standing there while a copy of it
            // appeared in your hand, so the same bottle was in two places at once.
            _serveCabinetGap = art;
            art.gameObject.SetActive(false);
            _serveBottleImage.sprite = ItemArt.Bottle(c);
            _serveBottleImage.color = _serveBottleImage.sprite != null
                ? Color.white : UITheme.StyleColor(c.Info?.Style, c.Type);
            SetHandBottleLevel(run, c);
            _servePourTotal = 0;
            _serveBottle.anchoredPosition = _serveBottleRest;
            _serveBottle.localRotation = Quaternion.identity;
            _serveBottle.gameObject.SetActive(true);
            Sfx.Play("bottle_open", 0.8f);
            _aimText.text = $"{c.Name.ToUpperInvariant()} — TIP IT OVER THE GLASS";
            _aimText.color = UITheme.TextSecondary;
        }

        /// <summary>Finds a bottle's cabinet slot and takes it straight into the hand, door
        /// and all — how the menu hands a carbonated bottle over (the author, 2026-08-02:
        /// opened from the menu, cola landed on the SHAKER bench, where Core refuses fizz
        /// every frame — the bottle simply would not pour). Runs after GoTo(Serve) has
        /// rebuilt the shelf; a bottle not standing there (empty, missing) is a no-op.</summary>
        private void TakeFromCabinet(string ingredientId)
        {
            var run = Run;
            if (run == null || _serveCabinetShelf == null) return;
            var slot = _serveCabinetShelf.Find($"M_{ingredientId}") as RectTransform;
            var art = slot != null ? slot.Find("Bottle") as RectTransform : null;
            var card = run.Shelf.Find(ingredientId)?.Ingredient;
            if (art == null || card == null) return;
            TakeCabinetBottle(run, card, art);
        }

        /// <summary>
        /// Stands the bottle back where it came from. Nothing else may clear
        /// <see cref="_serveFocusBottle"/> — the shelf has a hole in it while a bottle is out, and
        /// forgetting to fill it is how you end up with a cabinet missing a bottle that is not in
        /// anyone's hand either.
        /// </summary>
        private void PutTheBottleBack(TycoonRun run)
        {
            _serveBottleGrabbed = false;
            _serveFocusBottle = null;
            _serveBottle.gameObject.SetActive(false);
            _serveBottle.localRotation = Quaternion.identity;
            _serveBottle.anchoredPosition = _serveBottleRest;
            if (_serveCabinetGap != null) _serveCabinetGap.gameObject.SetActive(true);
            _serveCabinetGap = null;
            if (run != null) RefreshServeText(run, 1.0);
        }

        /// <summary>Draws the level in the bottle you are holding, the same way the shelf does.</summary>
        private void SetHandBottleLevel(TycoonRun run, IngredientCard card)
        {
            // Keyed on the BRAND, not the style: two vodkas share a style and no longer
            // share a bottle, so a cached cavity cut for one would be the wrong shape in
            // the other's glass.
            string style = card?.Id;
            if (style != _serveHandStyle)
            {
                if (_serveHandLiquid != null) Destroy(_serveHandLiquid.gameObject);
                _serveHandLiquid = card == null ? null : BottleArt.AddLiquid(_serveBottle, card);
                _serveHandStyle = style;
            }
            if (_serveHandLiquid == null) return;
            var shelf = card == null ? null : run.Shelf.Find(card.Id);
            float level = shelf != null && shelf.Capacity > 0
                ? (float)(shelf.Remaining / shelf.Capacity) : 0f;
            _serveHandLiquid.fillAmount = BottleArt.For(card).FillAmount(level);
        }

        /// <summary>Pours whatever bottle is in hand. The tilt and the aim are the shaker's,
        /// to the letter — one bar, one way of pouring.</summary>
        private void UpdateServeCabinet(TycoonRun run)
        {
            if (_serveFocusBottle == null || Mouse.current == null) return;
            // Letting go puts the bottle back on the shelf. It used to only drop the grab, which
            // left the bottle floating where the cursor happened to be while its twin stood in
            // the cabinet — you could never put one down, only abandon it mid-air.
            if (_serveBottleGrabbed && !Mouse.current.leftButton.isPressed)
            {
                PutTheBottleBack(run);
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

                float rad = tilt * Mathf.Deg2Rad;
                Vector2 mouth = local + new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (ServeVesselH * 0.78f);
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
                    var colour = UITheme.StyleColor(_serveFocusBottle.Info?.Style, _serveFocusBottle.Type);
                    _serveFluid.SetColor(colour);
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
                SetHandBottleLevel(run, _serveFocusBottle);
                RefreshServeText(run, 1.0);
                // How much has gone in. Without it the pour was a held button with no number on
                // it, and the only way to learn a measure was to serve the drink and be told.
                _aimText.text = $"{_serveFocusBottle.Name.ToUpperInvariant()}  ·  " +
                                $"POURED {Measures(_servePourTotal)}  ·  GLASS {run.ServingGlass.FillFraction:P0}";
                _aimText.color = UITheme.Cyan[3];
                if (landed <= 0)
                {
                    // The bottle ran dry mid-pour: put it down and rebuild the shelf without it.
                    PutTheBottleBack(run);
                    RefreshServe();
                }
            }
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
                Vector2 mouth = local + new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (ServeVesselH * 0.78f);
                var opening = _serveGlass.anchoredPosition + new Vector2(0, _serveGlass.rect.height * 0.5f);

                // The glass is full: the pour stops there rather than running a stream into a
                // vessel that cannot take it (GDD 21 §3, 2026-07-28).
                if (run.ServingGlass.IsFull && tilt > 42f && mouth.y > opening.y - 30f)
                {
                    _serveGrabbed = false;
                    ShowGlassFull();
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
                    var colour = DrinkColor(run.Glass);
                    _serveFluid.SetColor(colour);
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
            _serveSplash.Step(Time.deltaTime);
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

            foreach (Transform child in _serveMixBar) Destroy(child.gameObject);
            float h = _serveMixBar.rect.height, y = 0f;
            foreach (var id in glass.Ingredients)
            {
                var card = Run.Shelf.Find(id)?.Ingredient;
                float share = (float)(glass.RatioOf(id) * glass.FillFraction);   // of the VESSEL
                float segH = share * h;
                var seg = NewRect($"S_{id}", _serveMixBar);
                seg.anchorMin = new Vector2(0, 0); seg.anchorMax = new Vector2(1, 0);
                seg.pivot = new Vector2(0.5f, 0);
                seg.sizeDelta = new Vector2(-2, segH);
                seg.anchoredPosition = new Vector2(0, y);
                var img = seg.gameObject.AddComponent<Image>();
                img.color = UITheme.LiquidColor(card?.Info?.Style, card?.Type ?? IngredientType.Spirit);
                img.raycastTarget = false;
                if (segH > 13f)
                {
                    var label = NewText("P", seg, _body, 8, TextAnchor.MiddleCenter, UITheme.Night[0]);
                    Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.text = $"{share:P0} {(card?.Name ?? id).ToUpperInvariant().Split(' ')[0]}";
                }
                y += segH;
            }
            float free = Mathf.Max(0f, 1f - (float)glass.FillFraction);
            if (free > 0.001f)
            {
                var seg = NewRect("S_empty", _serveMixBar);
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
            // The whole screen, not a panel floating on it. The gain in pixels is small — the old
            // 1120x640 already covered most of a 1280x720 canvas — but the framing is the point:
            // the stage stops being a dialog you opened and becomes the counter you are standing
            // at, which is what lets the props be props instead of icons on keys.
            _servePanel = NewRect("ServePanel", _root);
            Stretch(_servePanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _servePanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_servePanel);

            var title = NewText("Title", _servePanel, _display, 16, TextAnchor.UpperCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -40), new Vector2(0, -10));
            title.text = "POUR THE GLASS";

            _serveShakerText = NewText("Shaker", _servePanel, _body, 13, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(_serveShakerText.rectTransform, new Vector2(0, 1), new Vector2(280, 24), new Vector2(20, -46));
            // VERTICAL and engine-drawn (2026-08-02): the GLASS's contents as shares of
            // the vessel, magenta-edged where the shaker's column is cyan.
            var serveTrack = NewRect("MixTrack", _servePanel);
            Place(serveTrack, new Vector2(0.5f, 0.5f), new Vector2(44, 300), new Vector2(348, -8));
            var serveBg = serveTrack.gameObject.AddComponent<Image>();
            serveBg.color = new Color(0.05f, 0.05f, 0.09f, 0.88f);
            serveBg.raycastTarget = false;
            GaugeEdge(serveTrack,
                new Color(UITheme.Magenta[3].r, UITheme.Magenta[3].g, UITheme.Magenta[3].b, 0.7f));
            _serveMixBar = NewRect("MixSegs", serveTrack);
            Stretch(_serveMixBar, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));

            _serveGlassText = NewText("Glass", _servePanel, _body, 13, TextAnchor.UpperRight, UITheme.TextPrimary);
            Place(_serveGlassText.rectTransform, new Vector2(1, 1), new Vector2(280, 24), new Vector2(-20, -46));

            // The room (the author's diagram, 2026-07-31): the prep table along the left
            // wall receding to the upper right, the fridge against the right wall, and the
            // counter opening toward the viewer between them. The furniture is ART, and the
            // props standing on it are the interface.
            var table = NewRect("PrepTable", _servePanel);
            table.anchorMin = table.anchorMax = Vector2.zero;
            table.pivot = Vector2.zero;
            table.sizeDelta = new Vector2(298f, 356f) * TableScale;
            table.anchoredPosition = TableFoot;
            var tImg = table.gameObject.AddComponent<Image>();
            tImg.sprite = ItemArt.Load("prep_table");
            tImg.preserveAspect = true;
            tImg.raycastTarget = false;
            if (tImg.sprite == null) tImg.color = new Color(0.35f, 0.36f, 0.40f, 1f);

            var fridge = NewRect("MixerFridge", _servePanel);
            fridge.anchorMin = fridge.anchorMax = new Vector2(1f, 0f);
            fridge.pivot = new Vector2(1f, 0f);
            fridge.sizeDelta = new Vector2(198f, 334f) * FridgeScale;
            fridge.anchoredPosition = new Vector2(FridgeFoot.x, FridgeFoot.y);
            var fImg = fridge.gameObject.AddComponent<Image>();
            fImg.sprite = ItemArt.Load("mixer_fridge");
            fImg.preserveAspect = true;
            fImg.raycastTarget = false;
            if (fImg.sprite == null) fImg.color = CabinetFrame;
            _serveCabinet = fridge;

            // The props' containers span the whole panel: everything in them is placed in
            // panel space, on a stand point measured off the furniture art. No layout
            // groups — a room is not a list.
            _serveGarnishRow = NewRect("TableTop", _servePanel);
            Stretch(_serveGarnishRow, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _serveCabinetShelf = NewRect("FridgeShelves", _servePanel);
            Stretch(_serveCabinetShelf, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _serveMixerRow = _serveCabinetShelf;

            _aimText = NewText("AimText", _servePanel, _body, 13, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(_aimText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -70), new Vector2(0, -46));

            // The play surface: the counter between the table and the fridge (the red region
            // of the diagram — x 0.26..0.71 of the panel). The glass and the shaker split it.
            _serveSurface = NewRect("ServeSurface", _servePanel);
            Stretch(_serveSurface, new Vector2(0.26f, 0f), new Vector2(0.71f, 1f),
                new Vector2(0, StageBottom), new Vector2(0, -StageTop));
            var surfImg = _serveSurface.gameObject.AddComponent<Image>();
            // Barely there: the furniture carries the room now, and the old half-strength
            // slab read as a dialog floating between the table and the fridge.
            surfImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.18f);
            surfImg.raycastTarget = false;

            // The serving glass: real clear-glass art (2026-07-23), transparent interior so the
            // poured drink pools behind it and shows through; the outline+rim draw in front.
            // The layer architecture (the author, 2026-08-02): the BACK face sits under
            // the pooled drink; the glass image itself becomes the clear FRONT face.
            // Down where the counter is, not floating at mid-air: the room has a horizon
            // now, and a glass hanging above it read as pinned to the wall.
            var glassRest = new Vector2(-110, -88);
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
            _serveSplash = new Splasher(_serveSurface);

            // The piece in hand between the shelf and the glass.
            _serveDragPiece = NewRect("DragPiece", _serveSurface);
            _serveDragPiece.pivot = new Vector2(0.5f, 1f);
            _serveDragPiece.sizeDelta = new Vector2(76, 84);   // in scale with the tub it came out of
            var sdp = _serveDragPiece.gameObject.AddComponent<Image>();
            sdp.preserveAspect = true; sdp.raycastTarget = false;
            _serveDragPiece.gameObject.SetActive(false);

            // The bottle in hand, once one is taken out of the cabinet.
            _serveBottleRest = new Vector2(226, -96);
            _serveBottle = NewRect("HandBottle", _serveSurface);
            _serveBottle.pivot = new Vector2(0.5f, 0.22f);
            _serveBottle.sizeDelta = new Vector2(118, ServeVesselH);
            _serveBottle.anchoredPosition = _serveBottleRest;
            _serveBottleImage = _serveBottle.gameObject.AddComponent<Image>();
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
    }
}
