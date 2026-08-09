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
    /// The tap (GDD 21 §10): beer never sees the shaker. The tap runs at one rate and
    /// what the player holds is the glass — lean it to fill, straighten it at the end
    /// to raise the head, tip it too far and it runs past the rim.
    /// </summary>
    public sealed partial class TycoonServiceFlow
    {

        // The tap (GDD 21 §10): a font you pull the handle on, over the pint it fills. There is
        // no shaker in this stage and no aiming — the whole skill is how far the handle is held.
        private RectTransform _tapPanel, _tapSurface, _tapHandle, _tapGlass;
        private Image _tapPintImage;
        private GlassArt.Piece _tapPiece;
        private bool _pouringNow;
        private MetaballFluid _tapFluid;
        private Image _tapKeg;
        private Text _tapTitle, _tapReadout, _tapVerdict;
        private IngredientCard _tapKegCard;
        private bool _glassHeld;
        private float _glassTilt;        // degrees from upright
        private Vector2 _tapGlassRest, _tapGlassPour;
        /// <summary>
        /// How fast the glass follows the hand. High enough to feel direct, low enough that a
        /// pixel of pointer jitter is not a degree of lean — the head is decided in the last few
        /// degrees of the pour, so the last few degrees have to be holdable (2026-07-30).
        /// </summary>
        private const float TiltFollow = 22f;
        private const float HandleTilt = 62f;   // degrees the handle swings while it runs
        /// <summary>How far left of the font's centre the spout hangs — the glass goes under it.</summary>
        private const float SpoutReach = 118f;
        /// <summary>Where the glass turns: low, where the hand is.</summary>
        private const float GlassPivotY = 0.16f;
        /// <summary>
        /// The pint, sized to what the liquid solver can actually fill. At 148x240 its cavity
        /// wanted ~2170 particles and the pool is capped below that, so asking for a full glass
        /// drew a 58% one — measured, not guessed. This size needs ~1500 and fills.
        /// </summary>
        private const float PintW = 124f, PintH = 200f;
        /// <summary>
        /// The faucet's lip, MEASURED off the font art rather than guessed: the spout tip is the
        /// leftmost opaque pixel of tap.png, at (-39, +34.5) from the sprite's centre in its own
        /// 82×175 pixels. Scaled by TowerH/175 to the size the tower is drawn at (2026-07-30).
        /// </summary>
        private static Vector2 SpoutOffset => new Vector2(-39f, 34.5f) * (TowerH / 175f);

        // ── the bar station (2026-07-30) ─────────────────────────────────────────
        // Everything used to float in an empty box: a tower, a glass and a keg side by side on
        // nothing, with no surface under them and no connection between them. The stage is now a
        // station — a counter the tower is bolted to and the glass stands on, a drip tray under
        // the faucet, and the keg behind the bar with its line running to the font.
        /// <summary>The counter's top surface, in the pour surface's local space.</summary>
        private const float CounterY = -140f;
        private const float CounterLip = 6f;      // the brass edge along its front, as in the bar
        /// <summary>
        /// The font stands well over the glass, because it does: a bar tower is around 450 mm
        /// against a pint glass's 160. Drawn the same height as the glass it read as a toy, so the
        /// counter dropped and the panel grew to make room for a tower that dominates the station
        /// the way the real thing does — 1.55× the glass, with its handle standing above that
        /// again (2026-07-30). The glass itself cannot be shrunk to buy the ratio: its size is
        /// calibrated to what the fluid solver fills, so the tower and the panel had to grow
        /// instead. What caps it is the keg: the under-bar recess has to stay deep enough to
        /// show the keg's label, and every millimetre the counter drops for the tower is a
        /// millimetre off the recess.
        /// </summary>
        private const float TowerW = 145f, TowerH = 310f;
        private const float TowerX = -50f;
        /// <summary>The open recess under the bar, where the kegs live in a real one. Putting the
        /// keg BEHIND the counter hid its label under the counter line, and putting it beside the
        /// counter left it standing in the room; under the bar it is both in its right place and
        /// fully readable (2026-07-30).</summary>
        private const float RecessLeft = -545f, RecessRight = 545f;
        /// <summary>The keg stands under the bar and runs off the bottom of the frame — this is a
        /// close-up of the bar top, not a view of the whole room, so its foot is simply not in
        /// shot. Its base is set so the label band lands inside the recess and stays readable.</summary>
        private const float KegW = 96f, KegH = 165f;
        private const float KegX = 300f, KegBaseY = -315f;
        /// <summary>Where the blank label sits on keg.png, as fractions of the sprite: the pale
        /// band runs from 0.516 to 0.676 of its height, 95% of its width. Measured, so the brand
        /// lands on the label instead of near it.</summary>
        private const float KegLabelCentreY = 0.404f, KegLabelH = 0.165f;
        private Text _kegLabel;
        /// <summary>How far under the faucet the rim is carried — close enough to catch, far
        /// enough that the stream is visibly falling into the glass.</summary>
        private const float MouthBelowSpout = 34f;
        private Vector2 _tapTowerPos;

        // ── the tap (GDD 21 §10) ─────────────────────────────────────────────────

        private void BuildTapPanel()
        {
            _tapPanel = NewRect("TapPanel", _root);
            // Near the full canvas. The station needs the height: a font drawn at its real
            // proportion to the glass simply does not fit a 640-tall box (2026-07-30).
            Place(_tapPanel, new Vector2(0.5f, 0.5f), new Vector2(1210, 700), Vector2.zero);
            _tapPanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_tapPanel);

            _tapTitle = NewText("Title", _tapPanel, _display, 16, TextAnchor.UpperCenter, UITheme.TextPrimary);
            Stretch(_tapTitle.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -46), new Vector2(0, -18));

            var hint = NewText("Hint", _tapPanel, _body, 8, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(hint.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -68), new Vector2(0, -44));
            hint.text = "HOLD THE GLASS AND POINT WHERE ITS BASE GOES · LEANED FILLS, UPRIGHT BUILDS THE HEAD";

            _tapSurface = NewRect("TapSurface", _tapPanel);
            Stretch(_tapSurface, Vector2.zero, Vector2.one, new Vector2(20, 84), new Vector2(-20, -82));
            var surf = _tapSurface.gameObject.AddComponent<Image>();
            // The back wall behind the station, a shade off the panel so the room has depth
            // rather than being one flat field.
            surf.color = UITheme.Night[0];
            surf.raycastTarget = false;
            // NOT masked. A Mask here clips the keg beautifully and empties the glass: Unity gives
            // a masked Graphic a stencil-modified COPY of its material, while MetaballFluid goes
            // on writing its particle array to the original, so the drink never reaches the screen.
            // The mask belongs on the under-bar alone (2026-07-30).

            // Order matters here, back to front: the counter's timber, then the recess cut into
            // it, then the keg standing in the recess, then its line, then everything on the bar
            // top. Building the counter after the keg simply painted over both of them.
            BuildTapCounter();

            // The recess under the bar the kegs stand in, cut into the counter front — and the
            // viewport for everything under there. A keg is taller than the hatch it stands in, so
            // it has to be cropped by the hatch; masking HERE and not on the whole surface is what
            // lets it be, without the fluid's material being swapped out from under it.
            var recess = NewRect("Recess", _tapSurface);
            recess.anchorMin = new Vector2(0.5f, 0f);
            recess.anchorMax = new Vector2(0.5f, 0.5f);
            recess.pivot = new Vector2(0.5f, 0.5f);
            recess.offsetMin = new Vector2(RecessLeft, 0f);
            recess.offsetMax = new Vector2(RecessRight, CounterY - 10f);
            var recessImg = recess.gameObject.AddComponent<Image>();
            // Warm shadow, not a void. At near-black the bays read as holes cut through the
            // cabinet rather than as shelving standing in its own shade.
            recessImg.color = new Color(0.115f, 0.075f, 0.065f, 1f);
            recessImg.raycastTarget = false;
            recess.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            // The lit underside of the bar top, right along the back of the counter. One line, and
            // the recess stops looking painted on and starts looking like a space under something.
            var under = NewRect("CounterUnderside", _tapSurface);
            under.anchorMin = new Vector2(0.5f, 0.5f);
            under.anchorMax = new Vector2(0.5f, 0.5f);
            under.pivot = new Vector2(0.5f, 1f);
            under.sizeDelta = new Vector2(RecessRight - RecessLeft, 4f);
            under.anchoredPosition = new Vector2((RecessLeft + RecessRight) * 0.5f, CounterY - 10f);
            var uimg = under.gameObject.AddComponent<Image>();
            uimg.color = new Color(0.26f, 0.17f, 0.12f, 1f);
            uimg.raycastTarget = false;

            // Timber posts dividing the under-bar into bays. Without them the whole lower half is
            // one flat slab; a working bar is shelving, and the bays give the eye something true
            // to read there instead of dead space.
            // Everything under the bar is parented to the recess, so the hatch crops it. Each is
            // hung from the recess's TOP edge, which is a known line (CounterY − 10) — measuring
            // from there needs no rect height and so is right on the frame it is built.
            foreach (float x in new[] { -300f, -110f, 130f })
            {
                var post = NewRect("Bay", recess);
                post.anchorMin = new Vector2(0.5f, 0f);
                post.anchorMax = new Vector2(0.5f, 1f);
                post.pivot = new Vector2(0.5f, 0.5f);
                post.offsetMin = new Vector2(x - 7f, 0f);
                post.offsetMax = new Vector2(x + 7f, 0f);
                var pimg = post.gameObject.AddComponent<Image>();
                pimg.color = new Color(0.13f, 0.08f, 0.07f, 1f);
                pimg.raycastTarget = false;
            }

            var keg = NewRect("Keg", recess);
            keg.anchorMin = keg.anchorMax = new Vector2(0.5f, 1f);
            keg.pivot = new Vector2(0.5f, 0.5f);
            keg.sizeDelta = new Vector2(KegW, KegH);
            keg.anchoredPosition = new Vector2(KegX, -KegH * 0.5f + 26f);   // cropped by the hatch lintel
            _tapKeg = keg.gameObject.AddComponent<Image>();
            _tapKeg.preserveAspect = true; _tapKeg.raycastTarget = false;

            // The bar's spare stock, standing in another bay and knocked well back so it never
            // competes with the keg actually on tap. Same sprite: it is the same keg.
            var spare = NewRect("SpareKeg", recess);
            spare.anchorMin = spare.anchorMax = new Vector2(0.5f, 1f);
            spare.pivot = new Vector2(0.5f, 0.5f);
            spare.sizeDelta = new Vector2(KegW * 0.88f, KegH * 0.88f);
            spare.anchoredPosition = new Vector2(-215f, -KegH * 0.88f * 0.5f + 16f);
            var spareImg = spare.gameObject.AddComponent<Image>();
            spareImg.sprite = ItemArt.Load("keg");
            spareImg.preserveAspect = true; spareImg.raycastTarget = false;
            spareImg.color = new Color(0.30f, 0.26f, 0.25f, 1f);   // deep in the shade
            if (spareImg.sprite == null) spareImg.enabled = false;

            // The brand, set on the keg's blank label. The art generator cannot spell, so every
            // word in this game is drawn in engine — the same rule the neon sign follows.
            _kegLabel = NewText("Brand", keg, _body, 8, TextAnchor.MiddleCenter, UITheme.Night[1]);
            var kl = _kegLabel.rectTransform;
            kl.anchorMin = new Vector2(0.08f, KegLabelCentreY - KegLabelH * 0.5f);
            kl.anchorMax = new Vector2(0.92f, KegLabelCentreY + KegLabelH * 0.5f);
            kl.offsetMin = Vector2.zero; kl.offsetMax = Vector2.zero;

            // The line from the keg to the font, so the two read as one plumbed-in rig instead of
            // two props that happen to share a screen. It dives behind the counter on its way.
            BuildBeerLine();

            // The drip tray, on the counter directly under the faucet.
            var tray = NewRect("DripTray", _tapSurface);
            var trayPos = new Vector2(TowerX + SpoutOffset.x, CounterY + 17f);
            Place(tray, new Vector2(0.5f, 0.5f), new Vector2(132, 33), trayPos);
            var trayImg = tray.gameObject.AddComponent<Image>();
            trayImg.sprite = ItemArt.Load("drip_tray");
            trayImg.preserveAspect = true; trayImg.raycastTarget = false;
            if (trayImg.sprite == null) trayImg.enabled = false;

            // The font, and the pint under its spout. Everything here hangs off the tower, so
            // moving the tower moves the whole rig and the spout stays over the glass. It is
            // seated ON the counter — its base sits on the surface, it does not hover over it.
            var tower = NewRect("Tower", _tapSurface);
            var towerPos = _tapTowerPos = new Vector2(TowerX, CounterY + TowerH * 0.5f);
            var towerSize = new Vector2(TowerW, TowerH);
            Place(tower, new Vector2(0.5f, 0.5f), towerSize, towerPos);
            var towerImg = tower.gameObject.AddComponent<Image>();
            towerImg.sprite = ItemArt.Load("tap");
            towerImg.preserveAspect = true; towerImg.raycastTarget = false;
            if (towerImg.sprite == null) towerImg.color = UITheme.Amber[2];

            // The glass is the thing you hold, so it stands on the counter until you pick it up
            // and it is the only grab target on this stage. Its base rests on the surface: the
            // rect is pivoted low, so the pivot sits a fraction of the glass above the counter.
            _tapGlassPour = towerPos + new Vector2(-SpoutReach, -46);
            _tapGlassRest = new Vector2(TowerX - SpoutReach - 96f, CounterY + PintH * GlassPivotY);
            _tapGlass = NewRect("Pint", _tapSurface);
            Place(_tapGlass, new Vector2(0.5f, 0.5f), new Vector2(PintW, PintH), _tapGlassRest);
            // Pivoted low, near where a hand holds it: a glass leans off its base, it does not
            // swing about its middle (2026-07-27).
            _tapGlass.pivot = new Vector2(0.5f, GlassPivotY);
            var pint = _tapPintImage = _tapGlass.gameObject.AddComponent<Image>();
            // The SAME glass everywhere (the author, 2026-08-02): the pint under the tap is
            // the drawn glassware pint at its line's tier, refreshed on stage entry. The
            // generated pint.png stays as the fallback for a run built without glassware.
            pint.sprite = ItemArt.Load("pint");
            pint.preserveAspect = true;
            if (pint.sprite == null) pint.color = UITheme.Cream[2];
            // No alpha hit-test here: the sprite was hollowed out so the beer shows through it,
            // which left only the thin walls clickable and the grab feeling crooked. The whole
            // rect is the glass as far as the hand is concerned (2026-07-27).
            var glassGrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            glassGrab.callback.AddListener(_ =>
            {
                if (Run == null || Run.Phase != TycoonPhase.DayOpen || Mouse.current == null) return;
                _glassHeld = true;
            });
            _tapGlass.gameObject.AddComponent<EventTrigger>().triggers.Add(glassGrab);

            _tapFluid = new MetaballFluid(_tapSurface);
            // A pint glass: narrow foot opening steadily out to the mouth.
            _tapFluid.SetProfile(new[] { 0.82f, 0.88f, 0.93f, 0.97f, 1.00f, 1.00f });
            // A pint is the one vessel that is filled and then STANDS, its beer creeping up a
            // sliver at a time as the head collapses into it. A body topped up that gently never
            // packs the way a poured one does and drew ~10 points high — measured on the settled
            // pint, not on the bench, where a straight pour into this same cavity needs no
            // correction at all. It matters more here than anywhere else: the foam band starts
            // at the beer's surface, so beer drawn high is foam drawn thin, and the head is the
            // whole skill of a pint (GDD 21 §10).
            _tapFluid.SetDensity(0.90f);

            // The head is not drawn here at all any more (2026-07-30). It used to be a tiled
            // Image laid over the beer, which is exactly why it read as a rectangle: straight
            // sides, square corners, and it could not rotate with the glass. Foam is now part of
            // the fluid body itself — see MetaballFluid's foam particles — so the head is a
            // wobbling, bubbled crown that leans when the glass leans.

            // Nothing is drawn on or beside the glass to mark a target (2026-07-30). The good-head
            // ticks and the lean-guide arc both came out: they turned a drink you look at into a
            // gauge you line up against, and the pint already says what it is — the head is right
            // there, on top, against the glass it fills. The readout still names the numbers.

            if (pint.sprite != null) _tapGlass.SetAsLastSibling();   // the glass draws over its contents

            // The handle: pivots at its brass collar, so pulling swings it toward you.
            _tapHandle = NewRect("Handle", _tapSurface);
            _tapHandle.pivot = new Vector2(0.5f, 0.06f);
            // Near its own native size. Blown up to 60×140 it read as a separate wooden object
            // parked beside the tap rather than the handle bolted to it.
            _tapHandle.sizeDelta = new Vector2(36, 112);
            _tapHandle.anchorMin = _tapHandle.anchorMax = new Vector2(0.5f, 0.5f);
            // Onto the brass fitting on top of the faucet — measured off the art at (-29, +87.5)
            // in the sprite's own pixels, scaled to the size the tower is drawn at.
            _tapHandle.anchoredPosition =
                towerPos + new Vector2(-29f, 87.5f) * (TowerH / 175f);
            var handleImg = _tapHandle.gameObject.AddComponent<Image>();
            handleImg.sprite = ItemArt.Load("tap_handle");
            handleImg.preserveAspect = true; handleImg.raycastTarget = false;
            if (handleImg.sprite == null) handleImg.color = UITheme.Amber[1];

            // A plate under the verdict and the readout. They used to sit straight on top of the
            // shelving, which read as text spilled over the art rather than as a status strip.
            var statusPlate = NewRect("StatusPlate", _tapPanel);
            statusPlate.anchorMin = new Vector2(0f, 0f);
            statusPlate.anchorMax = new Vector2(1f, 0f);
            statusPlate.pivot = new Vector2(0.5f, 0f);
            statusPlate.offsetMin = new Vector2(20f, 44f);
            statusPlate.offsetMax = new Vector2(-20f, 0f);
            statusPlate.sizeDelta = new Vector2(-40f, 42f);
            var plateImg = statusPlate.gameObject.AddComponent<Image>();
            plateImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.94f);
            plateImg.raycastTarget = false;

            _tapReadout = NewText("Readout", _tapPanel, _body, 8, TextAnchor.LowerCenter, UITheme.TextSecondary);
            Stretch(_tapReadout.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(0, 46), new Vector2(0, 64));

            _tapVerdict = Outlined(NewText("Verdict", _tapPanel, _display, 16, TextAnchor.LowerCenter, UITheme.TextPrimary));
            Stretch(_tapVerdict.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(0, 64), new Vector2(0, 86));

            var back = NewRect("Back", _tapPanel);
            Place(back, new Vector2(0.5f, 0), new Vector2(240, 34), new Vector2(-130, 12));
            back.gameObject.AddComponent<Image>().color = UITheme.Night[3];
            back.gameObject.AddComponent<Button>().onClick.AddListener(() => GoTo(Stage.Menu));
            var backLabel = NewText("Label", back, _body, 8, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backLabel.text = "← BACK TO THE MENU";

            var done = NewRect("Done", _tapPanel);
            Place(done, new Vector2(0.5f, 0), new Vector2(240, 34), new Vector2(130, 12));
            done.gameObject.AddComponent<Image>().color = UITheme.PrimaryAction;
            done.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                if (!Run.ServingGlass.IsEmpty) GoTo(Stage.Closed);
            });
            var doneLabel = NewText("Label", done, _body, 8, TextAnchor.MiddleCenter, UITheme.TextOnAmber);
            Stretch(doneLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            doneLabel.text = "SERVE IT → PICK A SEAT";
        }

        private void RefreshTap()
        {
            // The pint wears its line's tier (per-glass upgrades, 2026-08-02).
            var runNow = Run;
            if (runNow != null && _tapPintImage != null)
                foreach (var g in runNow.Glassware)
                    if (g.Id == "pint")
                    {
                        // BOTH faces here (the author: the pint's top pixels went missing —
                        // the far lip lives on the back face): the tap draws the composite
                        // glass over the beer, and the pool takes the PIECE's own numbers
                        // instead of constants measured off the retired sprite.
                        var tapPiece = GlassArt.For(g, runNow.GlassTier(g.Id));
                        _tapPiece = tapPiece;
                        _tapPintImage.sprite = tapPiece.Sprite;
                        _tapPintImage.color = Color.white;
                        _tapFluid.SetProfile(tapPiece.Profile);
                        _tapFluid.SetDensity(tapPiece.Density);
                        break;
                    }
            var run = Run;
            if (run == null) return;

            _tapKegCard = _focusBottle;
            _tapTitle.text = (_tapKegCard?.Name ?? "DRAUGHT").ToUpperInvariant();
            // A keg is a keg — steel, whatever is in it. What changes with the beer is the label,
            // so the brand goes on the blank band and the style tints its ink. The bottle sprite
            // used to stand in for the keg here, which is why the stage showed a menu icon blown
            // up to prop size (2026-07-30).
            var kegSprite = ItemArt.Load("keg");
            _tapKeg.sprite = kegSprite;
            _tapKeg.color = kegSprite != null ? Color.white
                : UITheme.StyleColor(_tapKegCard?.Info?.Style, IngredientType.Beer);
            if (_kegLabel != null)
            {
                _kegLabel.text = (_tapKegCard?.Name ?? "DRAUGHT").ToUpperInvariant();
                var ink = UITheme.StyleColor(_tapKegCard?.Info?.Style, IngredientType.Beer);
                // Printed ink on a cream label: the style's hue, taken well down so it reads as
                // print rather than as a glow.
                _kegLabel.color = new Color(ink.r * 0.35f, ink.g * 0.35f, ink.b * 0.35f, 1f);
            }

            // Beer obeys the same depth law as every other drink. It used to come straight out
            // of the colour table, whose entries carry alpha 1 — so a pint was clamped to 0.97
            // and was the most opaque liquid in the game, on the one drink whose craft the
            // player is being graded on.
            var beer = UITheme.LiquidColor(_tapKegCard?.Info?.Style, IngredientType.Beer);
            beer.a = UITheme.DrinkAlpha(run.ServingGlass.FillFraction, beer);
            _tapFluid.SetColor(beer);
            // Beer falling from the faucet is the same beer; the stream colour is set anyway so
            // the tap never inherits whichever drink the material was last handed.
            _tapFluid.SetStreamColor(beer);
            _tapFluid.SetFoamColor(UITheme.HeadColor(_tapKegCard?.Info?.Style));

            if (_tapKegCard != null && run.PullingId == null && run.CanPull(_tapKegCard.Id))
                run.BeginPull(_tapKegCard.Id);

            PushTapPool(run);
            RefreshTapText(run);
        }

        /// <summary>
        /// One frame at the tap. The tap runs at one rate; what the player holds is the glass,
        /// and lifting it lays it over — the same grip the bottle uses in the shaker stage. Held
        /// on its side the beer runs down the wall and stays flat; stood up it breaks into froth;
        /// tipped too far it runs past the rim and is lost (GDD 21 §10.2). So the pint is two
        /// movements: lean it to fill, straighten it at the end to raise the head.
        ///
        /// The head settles the whole time, pouring or not, which is what makes standing still a
        /// real and costly way to fix a bad pour.
        /// </summary>
        private void UpdateTap(TycoonRun run)
        {
            float dt = Time.deltaTime;
            var mouse = Mouse.current;

            if (_glassHeld && (mouse == null || !mouse.leftButton.isPressed)) _glassHeld = false;

            if (_glassHeld && mouse != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _tapSurface, mouse.position.ReadValue(), null, out Vector2 local))
            {
                // The glass POINTS AT THE HAND (2026-07-30). The mouth stays under the spout, so
                // what is left to steer is where the base swings to — and that is what the pointer
                // now is: aim below the tap and the glass stands up, swing out to the left and it
                // lays over. The angle is measured about the SPOUT, which never moves. Measuring
                // it about the glass's own grip ran away instead: leaning slides the grip left to
                // keep the mouth under the tap, which increases the angle, which leans it further.
                //
                // The old control was a vertical drag from wherever the glass was clicked. It
                // never felt attached to anything, because it was not — the glass flew off to its
                // own docking position while the hand dragged in empty space.
                var fromPivot = local - TiltPivot();
                if (fromPivot.sqrMagnitude > 64f)
                {
                    float aim = Mathf.Clamp(Mathf.Atan2(-fromPivot.x, -fromPivot.y) * Mathf.Rad2Deg, 0f, 90f);
                    _glassTilt = Mathf.Lerp(_glassTilt, aim, 1f - Mathf.Exp(-TiltFollow * dt));
                }
            }
            else _glassTilt = Mathf.MoveTowards(_glassTilt, 0f, dt * 220f);

            // In hand the glass is held so its MOUTH stays under the faucet, whatever the lean —
            // the hand slides to keep it there, which is what a bartender's does. Docking the
            // base instead swung the mouth a hundred units clear of the tap at the very angle
            // the pour is supposed to happen at (2026-07-27).
            var want = _glassHeld ? GripHoldingMouthUnderSpout() : _tapGlassRest;
            _tapGlass.anchoredPosition =
                Vector2.Lerp(_tapGlass.anchoredPosition, want, 1f - Mathf.Exp(-14f * dt));
            _tapGlass.localRotation = Quaternion.Euler(0, 0, -_glassTilt);

            // Beer only goes in through the mouth: the glass has to be held so its rim is under
            // the faucet. Leaning it swings the mouth toward the tap, which is what makes the
            // lean a place you have to find rather than a slider (2026-07-27).
            var spout = SpoutPoint();
            var mouth = MouthPoint();
            bool underSpout = Mathf.Abs(mouth.x - spout.x) < 78f
                              && mouth.y < spout.y + 24f && mouth.y > spout.y - 190f;
            // A brim-full glass stops the pour, so the handle springs back and the stream cuts out
            // rather than running into a glass that cannot take it (2026-07-30). Core refuses it
            // too — this is what makes the refusal visible.
            bool pouring = _glassHeld && underSpout && run.PullingId != null
                           && !run.ServingGlass.IsFull;
            _tapHandle.localRotation = Quaternion.Euler(0, 0, pouring ? HandleTilt : 0f);

            _pouringNow = pouring;
            if (pouring)
            {
                double before = run.ServingGlass.TotalVolume + run.ServingGlass.Head;
                run.PourTilted(dt, _glassTilt);

                // A stream from the faucet's lip, falling into the mouth wherever it now is.
                var toMouth = mouth - spout;
                _tapFluid.EmitStream(spout, new Vector2(toMouth.x * 2.2f, -300f), dt);
                if (run.ServingGlass.TotalVolume + run.ServingGlass.Head != before) RefreshTapText(run);
            }

            run.SettleHead(dt);
            PushTapPool(run);
            _tapFluid.Step(dt);
            if (!pouring) RefreshTapText(run);
        }

        /// <summary>Beer pools in the pint's interior; the head sits on it as its own band.</summary>
        private void PushTapPool(TycoonRun run)
        {
            var glass = run.ServingGlass;
            var (minX, maxX, bottomY, innerH) = PintInterior();

            // The glass turns about its base but the pool turns about its own middle, so the
            // cavity's centre has to be carried round the pivot by hand — otherwise the beer
            // stays where an upright glass would have been and the glass leans out of it.
            float rad = -_glassTilt * Mathf.Deg2Rad;
            var centre = RotateAboutGrip(new Vector2((minX + maxX) * 0.5f, bottomY + innerH * 0.5f), rad);
            float iw = (maxX - minX) * 0.5f;

            if (glass.IsEmpty) _tapFluid.ClearPool();
            else
            {
                // Beer and its head are one body of fluid in one solver (GDD 21 §10 — they share
                // the glass). The beer fills to its own line and the foam rides on top of it as
                // lighter particles, so the head takes the top of the glass rather than being
                // painted over a full pint, and it leans, wobbles and settles because it is
                // liquid rather than a band drawn across the rect (2026-07-30).
                // The pool leans with the glass — the solver rotates gravity into the vessel's
                // frame, so the drink stays level in the world while the glass goes over.
                float beerFrac = (float)(glass.TotalVolume / glass.Capacity);
                float headFrac = (float)(glass.Head / glass.Capacity);
                _tapFluid.SetPool(centre.x - iw, centre.x + iw,
                    centre.y - innerH * 0.5f, centre.y + innerH * 0.5f, beerFrac, rad, headFrac);
                // The beer deepens as the pint fills, like every other drink. RefreshTap sets
                // the colour once on the way in, when the glass is empty and the depth law
                // reads its thinnest — so without this a pulled pint would stay as pale as an
                // empty one all the way to the brim.
                var beer = UITheme.LiquidColor(_tapKegCard?.Info?.Style, IngredientType.Beer);
                beer.a = UITheme.DrinkAlpha(glass.FillFraction, beer);
                _tapFluid.SetColor(beer);
            }
        }

        /// <summary>
        /// The counter the station stands on: a dark wooden bar top with the same brass edge the
        /// room's own counter carries (module 18), so the tap stage reads as a corner of THIS bar
        /// rather than a separate diagram. Drawn procedurally — bar chrome is never generated art.
        /// </summary>
        private void BuildTapCounter()
        {
            // Anchored from the surface's bottom edge up to its vertical CENTRE, then pulled back
            // down to the counter line by the offset. Stated that way it needs no rect height, so
            // it is correct on the frame it is built — before any layout pass has run.
            var front = NewRect("CounterFront", _tapSurface);
            front.anchorMin = new Vector2(0f, 0f);
            front.anchorMax = new Vector2(1f, 0.5f);
            front.pivot = new Vector2(0.5f, 0.5f);
            front.offsetMin = Vector2.zero;
            front.offsetMax = new Vector2(0f, CounterY);
            var frontImg = front.gameObject.AddComponent<Image>();
            frontImg.color = new Color(0.16f, 0.10f, 0.08f, 1f);   // the bar's dark timber
            frontImg.raycastTarget = false;

            var lip = NewRect("CounterLip", _tapSurface);
            lip.anchorMin = new Vector2(0f, 0.5f);
            lip.anchorMax = new Vector2(1f, 0.5f);
            lip.pivot = new Vector2(0.5f, 0f);     // its underside sits ON the counter line
            lip.sizeDelta = new Vector2(0f, CounterLip);
            lip.anchoredPosition = new Vector2(0f, CounterY);
            var lipImg = lip.gameObject.AddComponent<Image>();
            lipImg.color = UITheme.Amber[2];      // the brass line, as on the room's own counter
            lipImg.raycastTarget = false;
        }

        /// <summary>
        /// The beer line from the keg's coupler to the foot of the font. Three straight segments
        /// rather than a curve: at this scale a hose is a few pixels wide and the joints read as
        /// bends. It passes behind the counter, which is drawn after it.
        /// </summary>
        private void BuildBeerLine()
        {
            // Up out of the coupler, along under the bar top, and into the foot of the font.
            var coupler = new Vector2(KegX, KegBaseY + KegH - 6f);
            var rise = new Vector2(KegX, CounterY - 22f);
            var run = new Vector2(TowerX + 10f, CounterY - 22f);
            LineSegment(coupler, rise);
            LineSegment(rise, run);
            LineSegment(run, new Vector2(TowerX + 10f, CounterY - 2f));
        }

        private void LineSegment(Vector2 a, Vector2 b)
        {
            var seg = NewRect("BeerLine", _tapSurface);
            var d = b - a;
            seg.anchorMin = seg.anchorMax = new Vector2(0.5f, 0.5f);
            seg.pivot = new Vector2(0.5f, 0.5f);
            seg.sizeDelta = new Vector2(d.magnitude, 7f);
            seg.anchoredPosition = (a + b) * 0.5f;
            seg.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = seg.gameObject.AddComponent<Image>();
            img.color = new Color(0.10f, 0.09f, 0.11f, 1f);   // black rubber hose
            img.raycastTarget = false;
        }

        /// <summary>The pint's drinkable interior, measured off the glass art.</summary>
        private (float minX, float maxX, float bottomY, float innerH) PintInterior()
        {
            var c = _tapGlass.anchoredPosition;
            float w = _tapGlass.rect.width, h = _tapGlass.rect.height;
            // The rect turns about its low pivot, so the base is measured from there.
            float baseY = c.y - h * _tapGlass.pivot.y;
            if (_tapPiece.Sprite != null)
            {
                // The generated pint: heights are REPORTED by the piece, and widths
                // measure against the aspect-fit drawn glass, not the letterboxed rect.
                // Same law as the serve pool (2026-08-02): the box is FLUSH with the
                // cavity, and the ceiling drops 3 art px so the bumpy surface — and the
                // head riding it — crests inside the mouth instead of over the lip.
                float drawnW = Mathf.Min(w, h * _tapPiece.Aspect);
                float artPx = drawnW / _tapPiece.Sprite.rect.width;
                // Half an art pixel in, matching the serve pool: the field's edge
                // smoothing bleeds about that far past the box.
                float iwp = drawnW * 0.5f * _tapPiece.InteriorHalf - 0.5f * artPx;
                return (c.x - iwp, c.x + iwp,
                    baseY + h * _tapPiece.FloorY,
                    h * (_tapPiece.RimY - _tapPiece.FloorY) - GlassArt.PoolCeilingArtPx * artPx);
            }
            // The retired sprite's hand-measured cavity, kept for a run without glassware.
            float iw = w * 0.5f * 0.58f;
            return (c.x - iw, c.x + iw, baseY + h * 0.07f, h * 0.82f);
        }

        /// <summary>The faucet's lip, where the beer leaves the font.</summary>
        private Vector2 SpoutPoint() => _tapTowerPos + SpoutOffset;

        /// <summary>
        /// The point the glass turns about while it is being held: its mouth, parked under the
        /// faucet. Both the steering and the guide arc measure from HERE rather than from the
        /// spout itself, so pointing at a guide dot asks for exactly the lean that dot marks —
        /// they are the same object, and a 34 px disagreement between them is not something the
        /// player should have to feel their way around (2026-07-30).
        /// </summary>
        private Vector2 TiltPivot() => SpoutPoint() + new Vector2(0f, -MouthBelowSpout);

        /// <summary>How far the rim stands above the grip on an upright glass.</summary>
        private float RimAboveGrip() => _tapGlass.rect.height * (0.07f + 0.82f - GlassPivotY);

        /// <summary>
        /// Where the hand has to be for the glass's mouth to sit under the faucet at the lean it
        /// is currently held at. The rim swings a long way round the grip on a glass this tall,
        /// so the hand moves with it.
        /// </summary>
        private Vector2 GripHoldingMouthUnderSpout()
        {
            float rad = -_glassTilt * Mathf.Deg2Rad;
            float up = RimAboveGrip();
            var rim = new Vector2(-Mathf.Sin(rad) * up, Mathf.Cos(rad) * up);
            return SpoutPoint() + new Vector2(0f, -MouthBelowSpout) - rim;
        }

        /// <summary>The centre of the glass's rim, carried round by however far it is leaning.</summary>
        private Vector2 MouthPoint()
        {
            var (minX, maxX, bottomY, innerH) = PintInterior();
            var rim = new Vector2((minX + maxX) * 0.5f, bottomY + innerH);
            return RotateAboutGrip(rim, -_glassTilt * Mathf.Deg2Rad);
        }

        /// <summary>Carries a point round the glass's grip, which is where it actually turns.</summary>
        private Vector2 RotateAboutGrip(Vector2 point, float rad) =>
            RotateAbout(point, _tapGlass.anchoredPosition, rad);

        private void RefreshTapText(TycoonRun run)
        {
            var glass = run.ServingGlass;
            double head = glass.Head / glass.Capacity;
            double score = TapPour.HeadScore(head);

            int fillPct = (int)System.Math.Round(glass.FillFraction * 100);
            int headPct = (int)System.Math.Round(head * 100);
            double left = run.Shelf.Find(_tapKegCard?.Id ?? "")?.Remaining ?? 0;
            string spilt = run.SpilledBeer > 0.02 ? $" · {run.SpilledBeer:0.0} spilled" : "";
            _tapReadout.text =
                $"tilt {(int)_glassTilt}° · pint {fillPct}% full · head {headPct}%{spilt} · {left:0.#} left in the keg";

            // While it is running, the glass's angle is the live thing to say; once it is down,
            // the pint is what there is to judge.
            if (_glassHeld && !_pouringNow && Run != null && !Run.ServingGlass.IsEmpty)
            { _tapVerdict.text = "HOLD IT UNDER THE TAP"; _tapVerdict.color = UITheme.Amber[3]; }
            else if (_glassHeld && _glassTilt > TapPour.SpillTilt)
            { _tapVerdict.text = "SPILLING — STAND IT UP"; _tapVerdict.color = UITheme.ViceRed[3]; }
            else if (glass.IsEmpty) { _tapVerdict.text = "TAKE THE GLASS TO THE TAP"; _tapVerdict.color = UITheme.TextSecondary; }
            // Beer and foam share the same room, so a glass at the brim takes neither — say it,
            // because otherwise holding it under a running tap looks like the tap has died.
            else if (glass.IsFull && score < 1.0)
            { _tapVerdict.text = "THE GLASS IS FULL — LET IT SETTLE"; _tapVerdict.color = UITheme.Amber[3]; }
            else if (glass.FillFraction < 0.75) { _tapVerdict.text = "SHORT POUR"; _tapVerdict.color = UITheme.Amber[3]; }
            else if (score >= 1.0) { _tapVerdict.text = "GOOD PINT"; _tapVerdict.color = UITheme.Lime[3]; }
            else if (head > TapPour.GoodHeadMax) { _tapVerdict.text = "TOO MUCH HEAD"; _tapVerdict.color = UITheme.ViceRed[3]; }
            else { _tapVerdict.text = "FLAT — NEEDS A HEAD"; _tapVerdict.color = UITheme.ViceRed[3]; }
        }
    }
}
