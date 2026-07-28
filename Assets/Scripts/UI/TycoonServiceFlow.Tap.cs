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
        private RectTransform _tapPanel, _tapSurface, _tapHandle, _tapGlass, _tapHeadBand, _tapBandMark, _tapBandMarkHigh;
        private RectTransform _tapHeadCrown;
        /// <summary>Foam drawn at the keys' grain, so it belongs to the same picture.</summary>
        private const float FoamPixelScale = 0.5f;
        private const float FoamCrownHeight = 20f;
        private bool _pouringNow;
        private MetaballFluid _tapFluid;
        private Image _tapKeg;
        private Text _tapTitle, _tapReadout, _tapVerdict;
        private IngredientCard _tapKegCard;
        private bool _glassHeld;
        private float _glassTilt;        // degrees from upright
        private float _glassGrabY;       // pointer y when the glass was taken
        private Vector2 _tapGlassRest, _tapGlassPour;
        /// <summary>Screen units of upward drag that lays the glass right over. The same grip the
        /// bottle uses in the shaker stage: lift and it leans (GDD 21 §10.2).</summary>
        private const float TiltDrag = 190f;
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
        /// <summary>The faucet's lip, measured off the font art, relative to the tower's centre.</summary>
        private static readonly Vector2 SpoutOffset = new Vector2(-30f, 44f);
        /// <summary>How far under the faucet the rim is carried — close enough to catch, far
        /// enough that the stream is visibly falling into the glass.</summary>
        private const float MouthBelowSpout = 34f;
        private Vector2 _tapTowerPos;

        // ── the tap (GDD 21 §10) ─────────────────────────────────────────────────

        private void BuildTapPanel()
        {
            _tapPanel = NewRect("TapPanel", _root);
            Place(_tapPanel, new Vector2(0.5f, 0.5f), new Vector2(1120, 640), Vector2.zero);
            _tapPanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_tapPanel);

            _tapTitle = NewText("Title", _tapPanel, _display, 16, TextAnchor.UpperCenter, UITheme.TextPrimary);
            Stretch(_tapTitle.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -46), new Vector2(0, -18));

            var hint = NewText("Hint", _tapPanel, _body, 8, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(hint.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -68), new Vector2(0, -44));
            hint.text = "HOLD THE GLASS AND LIFT TO LEAN IT · TILTED FILLS, UPRIGHT BUILDS THE HEAD";

            _tapSurface = NewRect("TapSurface", _tapPanel);
            Stretch(_tapSurface, Vector2.zero, Vector2.one, new Vector2(20, 84), new Vector2(-20, -82));
            var surf = _tapSurface.gameObject.AddComponent<Image>();
            surf.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.5f);
            surf.raycastTarget = false;

            // The keg stands off to the right, plainly the thing the beer is coming out of.
            var keg = NewRect("Keg", _tapSurface);
            Place(keg, new Vector2(0.5f, 0.5f), new Vector2(224, 296), new Vector2(300, -40));
            _tapKeg = keg.gameObject.AddComponent<Image>();
            _tapKeg.preserveAspect = true; _tapKeg.raycastTarget = false;

            // The font, and the pint under its spout. Everything here hangs off the tower, so
            // moving the tower moves the whole rig and the spout stays over the glass.
            var tower = NewRect("Tower", _tapSurface);
            var towerPos = _tapTowerPos = new Vector2(20, -30);
            var towerSize = new Vector2(150, 262);
            Place(tower, new Vector2(0.5f, 0.5f), towerSize, towerPos);
            var towerImg = tower.gameObject.AddComponent<Image>();
            towerImg.sprite = ItemArt.Load("tap");
            towerImg.preserveAspect = true; towerImg.raycastTarget = false;
            if (towerImg.sprite == null) towerImg.color = UITheme.Amber[2];

            // The glass is the thing you hold, so it rests on the counter until you pick it up
            // and it is the only grab target on this stage.
            _tapGlassPour = towerPos + new Vector2(-SpoutReach, -46);
            _tapGlassRest = towerPos + new Vector2(-SpoutReach - 40f, -150);
            _tapGlass = NewRect("Pint", _tapSurface);
            Place(_tapGlass, new Vector2(0.5f, 0.5f), new Vector2(PintW, PintH), _tapGlassRest);
            // Pivoted low, near where a hand holds it: a glass leans off its base, it does not
            // swing about its middle (2026-07-27).
            _tapGlass.pivot = new Vector2(0.5f, GlassPivotY);
            var pint = _tapGlass.gameObject.AddComponent<Image>();
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
                _glassGrabY = Mouse.current.position.ReadValue().y;
            });
            _tapGlass.gameObject.AddComponent<EventTrigger>().triggers.Add(glassGrab);

            _tapFluid = new MetaballFluid(_tapSurface);
            // A pint glass: narrow foot opening steadily out to the mouth.
            _tapFluid.SetProfile(new[] { 0.82f, 0.88f, 0.93f, 0.97f, 1.00f, 1.00f });
            // Measured: this cavity packs tighter than the solver's estimate and stopped at 73%
            // of the line it was given, so it is told to ask for the difference.
            _tapFluid.SetDensity(1f / 0.73f);

            // The head, drawn as its own band on top of the beer rather than as more fluid —
            // foam is a different material and reading it has to be instant.
            // The head is foam, not a lid: a body that tiles in both directions with a bubbled
            // crest tiled along its top. Tiled rather than stretched so the bubbles keep their
            // size however deep the head is.
            _tapHeadBand = NewRect("Head", _tapSurface);
            var headImg0 = _tapHeadBand.gameObject.AddComponent<Image>();
            headImg0.raycastTarget = false;
            headImg0.sprite = ItemArt.Load("foam_body");
            if (headImg0.sprite != null)
            {
                headImg0.type = Image.Type.Tiled;
                headImg0.pixelsPerUnitMultiplier = FoamPixelScale;
            }
            _tapHeadCrown = NewRect("Crown", _tapHeadBand);
            var crownImg = _tapHeadCrown.gameObject.AddComponent<Image>();
            crownImg.raycastTarget = false;
            crownImg.sprite = ItemArt.Load("foam_crown");
            if (crownImg.sprite != null)
            {
                crownImg.type = Image.Type.Tiled;
                crownImg.pixelsPerUnitMultiplier = FoamPixelScale;
            }
            else crownImg.enabled = false;
            _tapHeadBand.gameObject.SetActive(false);

            // Where a good head sits, marked on the glass so the target is visible while pouring
            // and not a number to be memorised (GDD 21 §10.3).
            _tapBandMark = NewRect("GoodBand", _tapSurface);
            _tapBandMark.gameObject.AddComponent<Image>().raycastTarget = false;

            if (pint.sprite != null) _tapGlass.SetAsLastSibling();   // the glass draws over its contents

            // The handle: pivots at its brass collar, so pulling swings it toward you.
            _tapHandle = NewRect("Handle", _tapSurface);
            _tapHandle.pivot = new Vector2(0.5f, 0.06f);
            _tapHandle.sizeDelta = new Vector2(60, 140);
            _tapHandle.anchorMin = _tapHandle.anchorMax = new Vector2(0.5f, 0.5f);
            // Seated on the font's cap: the handle is the thing you grab, so it has to look
            // bolted to the tower rather than hovering over it.
            _tapHandle.anchoredPosition = towerPos + new Vector2(0, towerSize.y * 0.5f - 42f);
            var handleImg = _tapHandle.gameObject.AddComponent<Image>();
            handleImg.sprite = ItemArt.Load("tap_handle");
            handleImg.preserveAspect = true; handleImg.raycastTarget = false;
            if (handleImg.sprite == null) handleImg.color = UITheme.Amber[1];

            _tapReadout = NewText("Readout", _tapPanel, _body, 8, TextAnchor.LowerCenter, UITheme.TextSecondary);
            Stretch(_tapReadout.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(0, 52), new Vector2(0, 74));

            _tapVerdict = Outlined(NewText("Verdict", _tapPanel, _display, 16, TextAnchor.LowerCenter, UITheme.TextPrimary));
            Stretch(_tapVerdict.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(0, 74), new Vector2(0, 100));

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
            var run = Run;
            if (run == null) return;

            _tapKegCard = _focusBottle;
            _tapTitle.text = (_tapKegCard?.Name ?? "DRAUGHT").ToUpperInvariant();
            var kegSprite = ItemArt.Bottle(_tapKegCard?.Info?.Style);
            _tapKeg.sprite = kegSprite;
            _tapKeg.color = kegSprite != null ? Color.white
                : UITheme.StyleColor(_tapKegCard?.Info?.Style, IngredientType.Beer);

            _tapFluid.SetColor(UITheme.LiquidColor(_tapKegCard?.Info?.Style, IngredientType.Beer));
            var headImg = _tapHeadBand.GetComponent<Image>();
            headImg.color = UITheme.HeadColor(_tapKegCard?.Info?.Style);

            if (_tapKegCard != null && run.PullingId == null && run.Glass.IsEmpty)
                run.BeginPull(_tapKegCard.Id);

            PlaceGoodBandMark();
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

            if (_glassHeld && mouse != null)
            {
                // Lift to lean it over; bring it back down to stand it up again.
                float lifted = mouse.position.ReadValue().y - _glassGrabY;
                _glassTilt = Mathf.Clamp01(lifted / TiltDrag) * 90f;
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
            bool pouring = _glassHeld && underSpout && run.PullingId != null;
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
            PlaceGoodBandMark();
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
                // The beer fills only up to where the foam starts, so the head really does take
                // the top of the glass rather than being painted over the top of a full pint.
                // The pool leans with the glass — the solver rotates gravity into the vessel's
                // frame, so the beer stays level in the world while the glass goes over.
                float beerFrac = (float)(glass.TotalVolume / glass.Capacity);
                _tapFluid.SetPool(centre.x - iw, centre.x + iw,
                    centre.y - innerH * 0.5f, centre.y + innerH * 0.5f, beerFrac, rad);
            }
            minX = centre.x - iw; maxX = centre.x + iw;
            bottomY = centre.y - innerH * 0.5f;

            float head = (float)(glass.Head / glass.Capacity);
            _tapHeadBand.gameObject.SetActive(head > 1e-4f);
            if (head > 1e-4f)
            {
                // On the surface the player can see, not the fill line the pool was aimed at —
                // the two differ enough to leave the foam floating over a gap.
                float nominal = bottomY + innerH * (float)(glass.TotalVolume / glass.Capacity);
                float beerTop = glass.TotalVolume > 0 ? _tapFluid.SurfaceY(nominal) : bottomY;
                float bandH = innerH * head;
                float top = Mathf.Min(beerTop + bandH, bottomY + innerH);
                bandH = Mathf.Max(top - beerTop, 2f);
                // Foam floats level however the glass is held, so the band never rotates — it
                // only narrows, to stay inside the leaning glass's silhouette.
                float lean = Mathf.Cos(_glassTilt * Mathf.Deg2Rad);
                _tapHeadBand.anchorMin = _tapHeadBand.anchorMax = _tapHeadBand.pivot = new Vector2(0.5f, 0.5f);
                _tapHeadBand.sizeDelta = new Vector2((maxX - minX) * Mathf.Max(lean, 0.35f), bandH);
                _tapHeadBand.anchoredPosition = new Vector2((minX + maxX) * 0.5f, beerTop + bandH * 0.5f);
                if (_tapHeadCrown != null)
                {
                    // Sits on the head's top edge and never inside the beer, so a thin head is
                    // all crest and a deep one grows underneath it.
                    float crownH = Mathf.Min(FoamCrownHeight, bandH);
                    _tapHeadCrown.anchorMin = new Vector2(0, 1);
                    _tapHeadCrown.anchorMax = new Vector2(1, 1);
                    _tapHeadCrown.pivot = new Vector2(0.5f, 1f);
                    _tapHeadCrown.sizeDelta = new Vector2(0, crownH);
                    _tapHeadCrown.anchoredPosition = Vector2.zero;
                }
            }
        }

        /// <summary>
        /// Where the beer should stop and the head begin, marked on the glass (GDD 21 §10.3).
        /// Two thin ticks against the inside wall rather than a filled band: a slab of colour
        /// across the glass read as another layer of the drink, which is the one thing this
        /// marker must never look like.
        /// </summary>
        private void PlaceGoodBandMark()
        {
            var (minX, maxX, bottomY, innerH) = PintInterior();
            float span = (maxX - minX) * 0.34f;
            var tint = new Color(UITheme.Lime[4].r, UITheme.Lime[4].g, UITheme.Lime[4].b, 0.85f);

            // Only worth showing on a glass that is standing: they mark where a finished pint's
            // head belongs, and rotating them with a leaning glass would say nothing.
            bool standing = _glassTilt < 12f;
            _tapBandMark.gameObject.SetActive(standing);

            PlaceTick(_tapBandMark, minX, span, bottomY + innerH * (float)(1.0 - TapPour.GoodHeadMax), tint);
            if (_tapBandMarkHigh == null)
            {
                _tapBandMarkHigh = NewRect("GoodBandHigh", _tapSurface);
                var img = _tapBandMarkHigh.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                _tapBandMarkHigh.SetSiblingIndex(_tapBandMark.GetSiblingIndex() + 1);
            }
            _tapBandMarkHigh.gameObject.SetActive(standing);
            PlaceTick(_tapBandMarkHigh, minX, span, bottomY + innerH * (float)(1.0 - TapPour.GoodHeadMin), tint);
        }

        private static void PlaceTick(RectTransform rt, float leftX, float span, float y, Color tint)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(span, 3f);
            rt.anchoredPosition = new Vector2(leftX + span * 0.5f + 4f, y);
            rt.GetComponent<Image>().color = tint;
        }

        /// <summary>The pint's drinkable interior, measured off the glass art.</summary>
        private (float minX, float maxX, float bottomY, float innerH) PintInterior()
        {
            // Measured off the hollowed sprite rather than guessed: the drinkable cavity is
            // 64 of its 108 px across and runs from 0.07 to 0.94 of its height. Guessing 0.72
            // put the beer through the walls (2026-07-27).
            var c = _tapGlass.anchoredPosition;
            float halfW = _tapGlass.rect.width * 0.5f;
            float h = _tapGlass.rect.height;
            float iw = halfW * 0.58f;
            // The rect turns about its low pivot, so the base is measured from there, not from
            // a centre the glass no longer rotates around.
            float bottomY = c.y - h * _tapGlass.pivot.y + h * 0.07f;
            return (c.x - iw, c.x + iw, bottomY, h * 0.82f);
        }

        /// <summary>The faucet's lip, where the beer leaves the font.</summary>
        private Vector2 SpoutPoint() => _tapTowerPos + SpoutOffset;

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
        private Vector2 RotateAboutGrip(Vector2 point, float rad)
        {
            var grip = _tapGlass.anchoredPosition;
            var d = point - grip;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return grip + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
        }

        /// <summary>The pint's rim, in the surface's own space.</summary>
        private float PintRimY()
        {
            var (_, _, bottomY, innerH) = PintInterior();
            return bottomY + innerH;
        }

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
            else if (glass.FillFraction < 0.75) { _tapVerdict.text = "SHORT POUR"; _tapVerdict.color = UITheme.Amber[3]; }
            else if (score >= 1.0) { _tapVerdict.text = "GOOD PINT"; _tapVerdict.color = UITheme.Lime[3]; }
            else if (head > TapPour.GoodHeadMax) { _tapVerdict.text = "TOO MUCH HEAD"; _tapVerdict.color = UITheme.ViceRed[3]; }
            else { _tapVerdict.text = "FLAT — NEEDS A HEAD"; _tapVerdict.color = UITheme.ViceRed[3]; }
        }
    }
}
