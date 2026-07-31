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
        private GlassArt.Piece _serveGlassPiece;
        private RectTransform _serveGarnishRow; // mint/olive garnishes are added here (2026-07-23)
        private RectTransform _serveMixerRow;   // mixers and juices go in AT THE GLASS (v5 P14)

        /// <summary>One press of a mixer key, as a share of whatever glass is on the counter —
        /// so a splash into a coupe is a splash, not most of the drink.</summary>
        private const double MixerMeasure = 0.15;

        // The two columns run the height of the stage and are wide enough to hold a real tub and
        // a real bottle (2026-07-31). They used to be 96 and 118 wide, which forced an ice bucket
        // down to 88x74 — at that size it reads as a button with a picture on it rather than
        // something you reach into, which was the whole complaint. They scroll, so the finishing
        // touches and the mixers still coming have somewhere to go.
        private const float ShelfW = 244f, CabinetW = 244f;
        private const float RailGap = 8f, RailKeyHeight = 130f, CabinetSlotHeight = 152f;

        /// <summary>Margin from the screen edge to a column, and from a column to the play surface.</summary>
        private const float StageInset = 16f;

        /// <summary>Room kept clear at the top for the title and the aim line, and at the bottom
        /// for the two buttons.</summary>
        private const float StageTop = 86f, StageBottom = 62f;

        /// <summary>How tall the shaker and the bottle in hand are drawn on THIS stage. The shaker
        /// bench's 180 left the tin looking like a thimble beside a 260-tall glass.</summary>
        private const float ServeVesselH = 250f;
        private static readonly Color CabinetFrame = new Color(0.20f, 0.17f, 0.22f, 1f);
        private static readonly Color CabinetInside = new Color(0.11f, 0.14f, 0.17f, 1f);

        /// <summary>The piece being carried from the finishing shelf to the glass.</summary>
        private PreparationDefinition _servePrep;
        private string _servePrepLabel;
        private RectTransform _serveDragPiece;

        // The fizzy-drinks cabinet (the author's sketch, 2026-07-31): bottles standing at their
        // own proportions behind a glass door. The door opens, a bottle comes out in your hand,
        // and you tip it over the glass — the same verb the shaker's bottles use, so a mixer is
        // POURED rather than clicked, and the measure is how long you hold it.
        private RectTransform _serveCabinet, _serveCabinetDoor, _serveCabinetShelf;
        private Image _serveCabinetDoorGlass;
        private bool _serveCabinetOpen;
        private float _serveDoorT;                 // 0 = shut, 1 = wide open
        private const float DoorSpeed = 5.5f;
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
            PushServePool(run);
            _serveShakerBody.color = DrinkColor(run.Glass);
            _serveShaker.gameObject.SetActive(!run.Glass.IsEmpty);
            _aimText.text = run.Glass.IsEmpty
                ? "BUILD IT IN THE GLASS · MIXERS ON THE RIGHT"
                : "GRAB THE SHAKER · TIP IT OVER THE GLASS";
            _aimText.color = UITheme.TextSecondary;

            // The left rail finishes the drink: ice and the rims go on at the GLASS, so a built
            // drink can have them at all, then the stocked garnishes (mint, olive) which still
            // go into the shaker before the pour.
            foreach (Transform ch in _serveGarnishRow) Destroy(ch.gameObject);
            AddFinishTub("ice", "ICE", Preparations.Ice);
            AddFinishTub("salt_rim", "SALT", Preparations.SaltRim);
            AddFinishTub("sugar_rim", "SUGAR", Preparations.SugarRim);
            AddFinishTub("lemon_twist", "LEMON", Preparations.LemonTwist);
            foreach (var bottle in run.Shelf.Bottles)
                if (bottle.Ingredient.Type == IngredientType.Garnish && !bottle.IsEmpty)
                    AddGarnishChip(bottle.Ingredient);

            // The cabinet: the mixers and juices, standing behind its door.
            _serveBottleGrabbed = false;
            _serveFocusBottle = null;
            _serveBottle.gameObject.SetActive(false);
            foreach (Transform ch in _serveCabinetShelf) Destroy(ch.gameObject);
            foreach (var bottle in run.Shelf.Bottles)
                if (IsGlassSide(bottle.Ingredient) && !bottle.IsEmpty)
                    AddCabinetBottle(bottle.Ingredient);
        }

        /// <summary>
        /// One container on the finishing shelf. NOT a button (the author's brief, 2026-07-31):
        /// you reach into an open tub and drag a piece out, and it only goes in if you drop it
        /// in the glass. The shaker bench has worked this way since 2026-07-23; this is the same
        /// verb aimed at the serving glass, so the two stages read as one bar.
        /// </summary>
        private void AddFinishTub(string prepId, string label, PreparationDefinition prep)
        {
            var run = Run;
            bool already = run != null && run.ServingGlass.HasPreparation(prep.Id);

            var tub = NewRect($"F_{prepId}", _serveGarnishRow);
            tub.gameObject.AddComponent<LayoutElement>().preferredHeight = RailKeyHeight;
            var hit = tub.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);   // the whole tub is the grab target

            var icon = NewRect("Tub", tub);
            Place(icon, new Vector2(0.5f, 1), new Vector2(ShelfW - 24f, RailKeyHeight - 26f),
                new Vector2(0, -4));
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
                // The drink takes the hit. The shaker floats the piece afterwards with its own
                // solids layer; this stage has no such layer yet, so the ripple is the whole
                // acknowledgement until the P14 item that draws decorations ON the glass lands.
                _serveFluid.Ripple(opening.x, 0.03f);
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

        private void AddGarnishChip(IngredientCard card)
        {
            var chip = NewRect($"G_{card.Id}", _serveGarnishRow);
            chip.gameObject.AddComponent<LayoutElement>().preferredHeight = RailKeyHeight;
            var bg = chip.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.85f);
            var icon = NewRect("Icon", chip);
            Place(icon, new Vector2(0.5f, 1), new Vector2(ShelfW - 40f, RailKeyHeight - 26f),
                new Vector2(0, -4));
            var iimg = icon.gameObject.AddComponent<Image>();
            iimg.sprite = ItemArt.Bottle(card.Info?.Style); iimg.preserveAspect = true; iimg.raycastTarget = false;
            if (iimg.sprite == null) iimg.color = UITheme.StyleColor(card.Info?.Style, card.Type);
            var name = NewText("N", chip, _body, 8, TextAnchor.LowerCenter, UITheme.TextPrimary);
            Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(92, 14), new Vector2(0, 2));
            name.text = card.Name.ToUpperInvariant();
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

        /// <summary>One mixer key on the right rail: a measure straight into the serving glass
        /// (v5 P14 / the P10 `PourAtGlass` verb).</summary>
        /// <summary>
        /// One bottle standing in the cabinet. Not a key: press it and the bottle comes out in
        /// your hand, and it is poured by being tipped over the glass, so the measure is how
        /// long you hold it there. Sized off its own art, so a squat cola bottle and a tall
        /// siphon stand at their true proportions behind the door.
        /// </summary>
        private void AddCabinetBottle(IngredientCard card)
        {
            var slot = NewRect($"M_{card.Id}", _serveCabinetShelf);
            slot.gameObject.AddComponent<LayoutElement>().preferredHeight = CabinetSlotHeight;
            var hit = slot.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);   // the whole slot takes the press

            var art = NewRect("Bottle", slot);
            Place(art, new Vector2(0.5f, 1), new Vector2(CabinetW - 60f, CabinetSlotHeight - 22f),
                new Vector2(0, -4));
            var img = art.gameObject.AddComponent<Image>();
            img.sprite = ItemArt.Bottle(card.Info?.Style);
            img.preserveAspect = true; img.raycastTarget = false;
            if (img.sprite == null) img.color = UITheme.StyleColor(card.Info?.Style, card.Type);

            // What is left in it, so a mixer that is running out says so behind the glass door.
            var shelfBottle = Run?.Shelf.Find(card.Id);
            var liquid = BottleArt.AddLiquid(art, card.Info?.Style, card.Type);
            if (liquid != null && shelfBottle != null && shelfBottle.Capacity > 0)
                liquid.fillAmount = BottleArt.For(card.Info?.Style)
                    .FillAmount((float)(shelfBottle.Remaining / shelfBottle.Capacity));

            var name = NewText("N", slot, _body, 8, TextAnchor.LowerCenter, UITheme.TextPrimary);
            Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(96, 12), new Vector2(0, 0));
            name.text = card.Name.ToUpperInvariant();

            Pressable(slot, art, img, lift: 5f, depth: 5f);

            var c = card;
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                // A shut cabinet is a shut cabinet: open the door first, the way you would.
                if (!_serveCabinetOpen)
                {
                    _serveCabinetOpen = true;
                    _aimText.text = "CABINET OPEN — TAKE A BOTTLE";
                    _aimText.color = UITheme.Cyan[3];
                    return;
                }
                var run = Run;
                if (run == null) return;
                _serveFocusBottle = c;
                _serveBottleGrabbed = true;
                _serveBottleImage.sprite = ItemArt.Bottle(c.Info?.Style);
                _serveBottleImage.color = _serveBottleImage.sprite != null
                    ? Color.white : UITheme.StyleColor(c.Info?.Style, c.Type);
                _serveBottle.anchoredPosition = _serveBottleRest;
                _serveBottle.localRotation = Quaternion.identity;
                _serveBottle.gameObject.SetActive(true);
                _aimText.text = $"{c.Name.ToUpperInvariant()} — TIP IT OVER THE GLASS";
                _aimText.color = UITheme.TextSecondary;
            });
            slot.gameObject.AddComponent<EventTrigger>().triggers.Add(down);
        }

        /// <summary>Swings the cabinet door, and pours whatever bottle is in hand. The tilt and
        /// the aim are the shaker's, to the letter — one bar, one way of pouring.</summary>
        private void UpdateServeCabinet(TycoonRun run)
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            _serveDoorT = Mathf.MoveTowards(_serveDoorT, _serveCabinetOpen ? 1f : 0f, DoorSpeed * dt);
            // A hinged pane: it swings out on its left edge, so it narrows as it opens and the
            // shelf behind it comes into the light.
            _serveCabinetDoor.localScale = new Vector3(Mathf.Lerp(1f, 0.12f, _serveDoorT), 1f, 1f);
            _serveCabinetDoorGlass.color = new Color(0.62f, 0.80f, 0.86f,
                Mathf.Lerp(0.22f, 0.06f, _serveDoorT));

            if (_serveFocusBottle == null || Mouse.current == null) return;
            if (_serveBottleGrabbed && !Mouse.current.leftButton.isPressed) _serveBottleGrabbed = false;

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

            if (pourNow)
            {
                double landed = run.PourAtGlass(_serveFocusBottle.Id, GlassPourRate * Time.deltaTime);
                _serveFluid.SetColor(DrinkColor(run.ServingGlass));
                RefreshServeText(run, 1.0);
                _aimText.text = $"{_serveFocusBottle.Name.ToUpperInvariant()} IN THE GLASS";
                _aimText.color = UITheme.Cyan[3];
                if (landed <= 0)
                {
                    // The bottle ran dry mid-pour: put it down and rebuild the shelf without it.
                    _serveBottleGrabbed = false;
                    _serveFocusBottle = null;
                    _serveBottle.gameObject.SetActive(false);
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
            if (ReferenceEquals(glassware, _serveGlassware) && _serveGlassImage.sprite != null) return;
            _serveGlassware = glassware;

            var piece = GlassArt.For(glassware);
            _serveGlassPiece = piece;
            _serveGlassImage.sprite = piece.Sprite;
            _serveGlassImage.preserveAspect = true;
            _serveGlassImage.color = Color.white;
            // Height is fixed and width follows the drawing, so a coupe is wide and a highball
            // narrow at the same place on the counter instead of all five being stretched into
            // one box.
            _serveGlass.sizeDelta = new Vector2(ServeGlassHeight * piece.Aspect, ServeGlassHeight);
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
            float iw = w * 0.5f * piece.InteriorHalf;
            float floor = c.y - h * 0.5f + h * piece.FloorY;
            float rim = c.y - h * 0.5f + h * piece.RimY;
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

        private void RefreshServeText(TycoonRun run, double accuracy)
        {
            _serveShakerText.text = $"shaker {run.Glass.FillFraction:P0} left";
            _serveGlassText.text = $"glass {run.ServingGlass.FillFraction:P0} full";
            _aimText.text = accuracy > 0.8 ? "CLEAN POUR" : accuracy > 0.4 ? "SOME SPILL" : "SPILLING!";
            _aimText.color = Color.Lerp(UITheme.ViceRed[3], UITheme.Lime[3], (float)accuracy);
        }

        /// <summary>
        /// A column of props that scrolls when there are more of them than there is room for.
        /// Returns the content the props go into — callers add children and forget about it.
        /// The viewport is masked, so a tub half off the bottom is clipped by the shelf edge
        /// instead of drawing over the buttons under it.
        /// </summary>
        private RectTransform ScrollShelf(RectTransform column, string name)
        {
            var viewport = NewRect(name + "View", column);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -20));
            var mask = viewport.gameObject.AddComponent<Image>();
            mask.color = new Color(1f, 1f, 1f, 0.004f);   // a mask needs something to cut
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect(name, viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(0, 0);
            content.offsetMax = new Vector2(0, 0);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RailGap;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            return content;
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
            _serveGlassText = NewText("Glass", _servePanel, _body, 13, TextAnchor.UpperRight, UITheme.TextPrimary);
            Place(_serveGlassText.rectTransform, new Vector2(1, 1), new Vector2(280, 24), new Vector2(-20, -46));

            // The finishing shelf: a full-height column down the left edge, holding the tubs at a
            // size you would actually reach into. It scrolls, so the finishing touches still to
            // come do not silently run off the bottom the way eight keys used to run off the rail.
            var shelfCol = NewRect("FinishShelf", _servePanel);
            Stretch(shelfCol, new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(StageInset, StageBottom), new Vector2(StageInset + ShelfW, -StageTop));
            var glabel = NewText("GLabel", shelfCol, _body, 10, TextAnchor.UpperCenter,
                UITheme.TypeRamp[IngredientType.Garnish][3]);
            Stretch(glabel.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -18), Vector2.zero);
            glabel.text = "— FINISH —";
            _serveGarnishRow = ScrollShelf(shelfCol, "Garnishes");

            // The right column: what goes in AT THE GLASS (v5 P14, the notes' second rail). P10
            // put the rule in Core — carbonated never enters the shaker, it is added to the
            // serving glass — and then there was no door in the UI to do it through, so the six
            // built cocktails could not be made by playing at all. This is that door.
            // The cabinet: a lit case with a glass door, the bottles standing on its shelf.
            var cabinetCol = NewRect("MixerColumn", _servePanel);
            Stretch(cabinetCol, new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(-(StageInset + CabinetW), StageBottom), new Vector2(-StageInset, -StageTop));
            var mlabel = NewText("MLabel", cabinetCol, _body, 10, TextAnchor.UpperCenter,
                UITheme.TypeRamp[IngredientType.Bubbly][3]);
            Stretch(mlabel.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -18), Vector2.zero);
            mlabel.text = "— MIXERS —";

            _serveCabinet = NewRect("Cabinet", cabinetCol);
            Stretch(_serveCabinet, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -20));
            _serveCabinet.gameObject.AddComponent<Image>().color = CabinetFrame;

            var inside = NewRect("Inside", _serveCabinet);
            Stretch(inside, Vector2.zero, Vector2.one, new Vector2(7, 7), new Vector2(-7, -7));
            inside.gameObject.AddComponent<Image>().color = CabinetInside;

            _serveCabinetShelf = ScrollShelf(inside, "Shelf");
            _serveMixerRow = _serveCabinetShelf;

            // The door, hinged on its left edge: it narrows as it swings out, and the pane over
            // the bottles clears with it.
            _serveCabinetDoor = NewRect("Door", _serveCabinet);
            Stretch(_serveCabinetDoor, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
            _serveCabinetDoor.pivot = new Vector2(0f, 0.5f);
            _serveCabinetDoorGlass = _serveCabinetDoor.gameObject.AddComponent<Image>();
            _serveCabinetDoorGlass.color = new Color(0.62f, 0.80f, 0.86f, 0.22f);
            _serveCabinetDoorGlass.raycastTarget = false;
            var handle = NewRect("Handle", _serveCabinetDoor);
            Place(handle, new Vector2(1, 0.5f), new Vector2(4, 46), new Vector2(-6, 0));
            handle.gameObject.AddComponent<Image>().color = CabinetFrame;

            _aimText = NewText("AimText", _servePanel, _body, 13, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(_aimText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -70), new Vector2(0, -46));

            // The play surface: the counter between the two columns. The author's sketch reads
            // left to right — finishing shelf, the glass being filled, the shaker, the cabinet —
            // so the surface is what is left after the two columns take their edges, and the
            // glass and the shaker split it.
            _serveSurface = NewRect("ServeSurface", _servePanel);
            Stretch(_serveSurface, Vector2.zero, Vector2.one,
                new Vector2(StageInset * 2 + ShelfW, StageBottom),
                new Vector2(-(StageInset * 2 + CabinetW), -StageTop));
            var surfImg = _serveSurface.gameObject.AddComponent<Image>();
            surfImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.5f);
            surfImg.raycastTarget = false;

            // The serving glass: real clear-glass art (2026-07-23), transparent interior so the
            // poured drink pools behind it and shows through; the outline+rim draw in front.
            _serveGlass = NewRect("Glass", _serveSurface);
            Place(_serveGlass, new Vector2(0.5f, 0.5f), new Vector2(190, ServeGlassHeight),
                new Vector2(-150, -30));
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
            _serveBottleRest = new Vector2(252, -50);
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
            _serveShakerRest = new Vector2(170, -56);
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
