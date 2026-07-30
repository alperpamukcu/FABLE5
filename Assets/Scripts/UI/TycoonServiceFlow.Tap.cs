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
            hint.text = "HOLD THE GLASS AND POINT WHERE ITS BASE GOES · LEANED FILLS, UPRIGHT BUILDS THE HEAD";

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
            }
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
