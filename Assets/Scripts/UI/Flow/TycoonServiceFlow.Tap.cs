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
    /// The tap (GDD 21 §10), rebuilt 2026-08-13: beer never sees the shaker. The tap runs
    /// at one rate and what the player holds is the glass — lean it to fill, straighten it
    /// at the end to raise the head, tip it too far and it runs past the rim. The under-bar
    /// recess is a real cellar now: every keg the bar stocks stands in a bay, the one on
    /// tap coupled to the line, and clicking a spare couples THAT one instead — the whole
    /// cellar within reach, the way the bench's rail and the counter's shelf now are.
    /// </summary>
    public sealed partial class TycoonServiceFlow
    {

        // The tap (GDD 21 §10): a font you pull the handle on, over the pint it fills. There is
        // no shaker in this stage and no aiming — the whole skill is how far the glass is leaned.
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
        private Vector2 _tapGlassRest;
        /// <summary>
        /// How fast the glass follows the hand. High enough to feel direct, low enough that a
        /// pixel of pointer jitter is not a degree of lean — the head is decided in the last few
        /// degrees of the pour, so the last few degrees have to be holdable (2026-07-30).
        /// </summary>
        private const float TiltFollow = 22f;
        private const float HandleTilt = 62f;   // degrees the handle swings while it runs
        /// <summary>Where the glass turns: low, where the hand is.</summary>
        private const float GlassPivotY = 0.16f;
        /// <summary>
        /// The pint, sized to what the liquid solver can actually fill. At 148x240 its cavity
        /// wanted ~2170 particles and the pool is capped below that, so asking for a full glass
        /// drew a 58% one — measured, not guessed. This size needs ~1500 and fills.
        /// </summary>
        private const float PintW = 124f, PintH = 200f;
        /// <summary>
        /// ONE FONT PER RUNG (2026-08-26, the author: "bira koyma sahnesinde kullanılan
        /// büyük boy fıçı hem yanlış hem de bozuk gözüküyor, 3 seviyeye uygun büyütülmüş
        /// halini oluşturman gerekiyor").
        ///
        /// The bench used to stand ONE drawing whatever the bar owned — and that drawing
        /// matched none of the three towers the market sells: it wore two faucets facing
        /// opposite ways and carried a red smear where its baked-on handle had been rubbed
        /// out. The station stands the tower the bar actually bought now, at the bench's own
        /// grain, and every number that hangs off a font hangs off THIS row instead of off a
        /// constant: how big it draws, where its lip is, where its lever bolts on.
        ///
        /// EVERY FIGURE IS MEASURED, in the art's own pixels, off the shipped sprites
        /// (Tools/room_furniture_gen.py struck them; the pixel grids that were read are in
        /// that round's scratchpad). The offsets below are those readings doubled, because a
        /// font stands at a whole 2× of its drawing — the house rule for pixel art, and the
        /// reason the sizes are not round numbers.
        /// </summary>
        private readonly struct FontRig
        {
            public readonly string Art;      // Resources/Items
            public readonly Vector2 Size;    // the drawn rect: exactly 2× the art
            public readonly Vector2 Spout;   // the faucet's lip, from the rect's centre
            public readonly Vector2 Valve;   // where the lever bolts on, from the same centre
            public readonly Vector2 Lever;   // how big that lever draws on THIS font
            public readonly float Rest;      // where the pint waits, from the font's own x

            public FontRig(string art, Vector2 size, Vector2 spout, Vector2 valve, Vector2 lever,
                           float rest)
            { Art = art; Size = size; Spout = spout; Valve = valve; Lever = lever; Rest = rest; }
        }

        /// <summary>
        /// The three rungs, each checked in play (2026-08-26) and corrected there — the grid
        /// gave the lip and the valve, the screen gave the last two numbers.
        ///
        /// THE LEVER SHRINKS as the tower gets busier, for a different reason on each. The
        /// single column has open air over its faucet and takes a full lever. The ARCH's
        /// wheel hangs inside the arch's own opening, so a full lever runs straight through
        /// the brass above it — the drawn finial that used to fill that gap was rubbed out at
        /// ship time to make room for a lever that MOVES, and 40 is what fits the hole left
        /// behind. The TEE's crossbar has clear sky over its middle, but the tee is the
        /// tallest font in the game: at 112 its lever came up through the FASCIA, which is a
        /// scrim over the room and does not own the top of the screen.
        ///
        /// THE PINT RESTS CLEAR OF THE FONT, not a fixed distance from its lip. On the
        /// single that is the same thing — the column is 140 wide — but the arch and the tee
        /// are near enough 370, so a rest measured off the spout stood the glass INSIDE the
        /// tower: half behind a brass leg, which is a glass nobody would think to pick up.
        /// It is measured off the font's own half-width instead, plus a hand's width of bar.
        /// </summary>
        private static FontRig RigFor(int tapLevel) =>
            tapLevel >= 3 ? new FontRig("bench_tap_tee", new Vector2(368f, 462f),
                                        new Vector2(-2f, 35f), new Vector2(-2f, 159f),
                                        new Vector2(36f, 80f), -254f)
          : tapLevel == 2 ? new FontRig("bench_tap_arch", new Vector2(376f, 440f),
                                        new Vector2(-6f, 40f), new Vector2(-6f, 116f),
                                        new Vector2(36f, 40f), -258f)
          :                 new FontRig("bench_tap_single", new Vector2(140f, 324f),
                                        new Vector2(-58f, 70f), new Vector2(-50f, 156f),
                                        new Vector2(36f, 96f), -154f);

        /// <summary>The font standing on the bench this visit. Set by <see cref="StandTheFont"/>,
        /// which is the only writer; everything that needs a faucet reads it.</summary>
        private FontRig _rig = RigFor(1);

        // ── the bar station (2026-07-30) ─────────────────────────────────────────
        // Everything used to float in an empty box: a tower, a glass and a keg side by side on
        // nothing, with no surface under them and no connection between them. The stage is now a
        // station — a counter the tower is bolted to and the glass stands on, a drip tray under
        // the faucet, and the kegs behind the bar with the line running to the font.
        /// <summary>The counter's top surface, in the pour surface's local space.
        /// -170 since the big font (2026-08-25): its 480 needs the headroom.</summary>
        private const float CounterY = -170f;
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
        // THE ROOM'S OWN FONT, GROWN (2026-08-25) — and since 2026-08-26 it is the font the
        // bar OWNS, not one of them standing in for all three. Its size is the rig's now;
        // what is left here is the one number a font does not carry, which is where along
        // the counter it is bolted. The counter dropped 30 for the big drawing and the kegs
        // dropped with it (see KegBaseY) so none of them pokes up through the counter line.
        private const float TowerX = -50f;
        /// <summary>The open recess under the bar, where the kegs live in a real one. Putting the
        /// keg BEHIND the counter hid its label under the counter line, and putting it beside the
        /// counter left it standing in the room; under the bar it is both in its right place and
        /// fully readable (2026-07-30).</summary>
        private const float RecessLeft = -545f, RecessRight = 545f;
        /// <summary>A keg stands under the bar and runs off the bottom of the frame — this is a
        /// close-up of the bar top, not a view of the whole room, so its foot is simply not in
        /// shot. Its base is set so the label band lands inside the recess and stays readable.</summary>
        private const float KegW = 96f, KegH = 165f;
        /// <summary>The plumbed bay: the keg standing here is the one on tap, and the beer
        /// line runs from ITS coupler. A swap moves the keg to the line, not the line to
        /// the keg — one line, as in a one-font bar.</summary>
        private const float KegX = 300f, KegBaseY = -345f;   // -315 until the counter dropped
        /// <summary>Where the spare kegs park, one bay each, nearest first. The recess holds
        /// four kegs in all (one on the line, three parked); the live cellar carries three
        /// beers, so the bays have never had to turn one away. A fourth beer would still be
        /// reachable from the wall's own keg row, which opens the tap on whatever it is
        /// clicked with — the bays are a shortcut, not the only door.</summary>
        private static readonly float[] SpareKegX = { -215f, 10f, -420f };
        /// <summary>Where the blank label sits on keg.png, as fractions of the sprite: the pale
        /// band runs from 0.516 to 0.676 of its height, 95% of its width. Measured, so the brand
        /// lands on the label instead of near it.</summary>
        private const float KegLabelCentreY = 0.404f, KegLabelH = 0.165f;
        private Text _kegLabel;
        /// <summary>The bays' contents, rebuilt whenever the tap refreshes (a swap, a new
        /// keg bought, a keg run dry).</summary>
        private RectTransform _tapKegRow;
        /// <summary>How far under the faucet the rim is carried — close enough to catch, far
        /// enough that the stream is visibly falling into the glass.</summary>
        private const float MouthBelowSpout = 34f;
        private Vector2 _tapTowerPos;
        private RectTransform _tapTower, _tapTray;
        private Image _tapTowerImg;

        /// <summary>
        /// Bolts the bar's own font to the bench and moves everything that hangs off a
        /// faucet with it: the tower's picture and size, the drip tray under its lip, the
        /// lever at its valve, and where the pint stands waiting to be carried under it.
        ///
        /// Called once at build and again at every stage entry, because the ladder is
        /// climbed at DAY END and the bench is built once at start-up — without the second
        /// call a bar that fitted the arch on Tuesday would still be pouring out of Monday's
        /// column. Cheap: four rects, on a screen that has just been opened.
        /// </summary>
        private void StandTheFont(int tapLevel)
        {
            _rig = RigFor(tapLevel);
            if (_tapTower == null) return;
            _tapTowerPos = new Vector2(TowerX, CounterY + _rig.Size.y * 0.5f);
            _tapTower.sizeDelta = _rig.Size;
            _tapTower.anchoredPosition = _tapTowerPos;
            if (_tapTowerImg != null)
            {
                // The room's own single font is the fallback: a missing drawing must leave a
                // tap you can still pour out of, not a magenta hole (the house rule for art).
                _tapTowerImg.sprite = ItemArt.Load(_rig.Art) ?? ItemArt.Load("tap");
                _tapTowerImg.color = _tapTowerImg.sprite == null ? UITheme.Amber[2] : Color.white;
            }
            if (_tapTray != null)
                _tapTray.anchoredPosition = new Vector2(TowerX + _rig.Spout.x, CounterY + 17f);
            if (_tapHandle != null)
            {
                _tapHandle.sizeDelta = _rig.Lever;
                _tapHandle.anchoredPosition = _tapTowerPos + _rig.Valve;
            }
            _tapGlassRest = new Vector2(TowerX + _rig.Rest, CounterY + PintH * GlassPivotY);
            // The glass only moves home if it is not in the player's hand: re-standing the
            // font mid-pour would tear the pint out of it.
            if (_tapGlass != null && !_glassHeld)
                _tapGlass.anchoredPosition = _tapGlassRest;
        }

        // The way out: SERVE only means something once beer stands in the glass, so the
        // key dims until it does — the ToGlass key's own law, applied here.
        private Button _tapDoneBtn;
        private CanvasGroup _tapDoneGroup;

        // ── the tap (GDD 21 §10) ─────────────────────────────────────────────────

        private void BuildTapPanel()
        {
            _tapPanel = NewRect("TapPanel", _field);
            // Near the full canvas. The station needs the height: a font drawn at its real
            // proportion to the glass simply does not fit a 640-tall box (2026-07-30).
            // THE SAME BENCH AS THE OTHER TWO (2026-08-22, the author: "bira koyma
            // sahnesinin tasarımı da pour sahnesiyle aynı şekilde olacak"). It was a 1210x700
            // plate of flat Night over the room; it is the full field now, undimmed, with the
            // bar top drawn on the room's own counter line. Full field and not the old inset,
            // because the band has to reach both edges or the room shows past its ends.
            Stretch(_tapPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var block = _tapPanel.gameObject.AddComponent<Image>();
            block.color = new Color(0f, 0f, 0f, 0f);
            Swallow(_tapPanel);

            _tapTitle = NewText("Title", _tapPanel, _display, 16, TextAnchor.UpperCenter, UITheme.TextPrimary);
            Stretch(_tapTitle.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -46), new Vector2(0, -18));

            var hint = NewText("Hint", _tapPanel, _body, 8, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(hint.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -68), new Vector2(0, -44));
            hint.text = "HOLD THE GLASS AND AIM · TILT IT TO FILL, HOLD IT STRAIGHT FOR FOAM · CLICK A KEG TO SWAP";

            _tapSurface = NewRect("TapSurface", _tapPanel);
            Stretch(_tapSurface, Vector2.zero, Vector2.one, new Vector2(20, 84), new Vector2(-20, -82));
            var surf = _tapSurface.gameObject.AddComponent<Image>();
            // A COORDINATE SPACE, not a thing you can see — the same as the shaker's
            // PourSurface. It was a Night[0] back wall "so the room has depth rather than
            // being one flat field", which was true while the panel was an opaque plate over
            // nowhere. The room is behind the station now and has its own depth, so a painted
            // wall in front of it is just a wall in front of a wall (2026-08-22).
            surf.color = new Color(0f, 0f, 0f, 0f);
            surf.raycastTarget = false;
            // NOT masked. A Mask here clips the keg beautifully and empties the glass: Unity gives
            // a masked Graphic a stencil-modified COPY of its material, while MetaballFluid goes
            // on writing its particle array to the original, so the drink never reaches the screen.
            // The mask belongs on the under-bar alone (2026-07-30).

            // Order matters here, back to front: the counter's timber, then the recess cut into
            // it, then the kegs standing in the recess, then the line, then everything on the bar
            // top. Building the counter after the kegs simply painted over them.
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

            // The kegs live in here, rebuilt per refresh: the one on tap in the plumbed bay,
            // the spares parked in the shade beside it.
            _tapKegRow = NewRect("KegRow", recess);
            Stretch(_tapKegRow, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // The line from the keg's coupler to the foot of the font, so the two read as one
            // plumbed-in rig instead of two props that happen to share a screen. It dives
            // behind the counter on its way. The line is FIXED: it serves the plumbed bay,
            // and a swap stands a different keg under it.
            BuildBeerLine();

            // The drip tray, on the counter directly under the faucet. Re-placed with the
            // font (StandTheFont): a taller tower puts its lip somewhere else along the bar.
            var tray = _tapTray = NewRect("DripTray", _tapSurface);
            var trayPos = new Vector2(TowerX + _rig.Spout.x, CounterY + 17f);
            Place(tray, new Vector2(0.5f, 0.5f), new Vector2(132, 33), trayPos);
            var trayImg = tray.gameObject.AddComponent<Image>();
            trayImg.sprite = ItemArt.Load("drip_tray");
            trayImg.preserveAspect = true; trayImg.raycastTarget = false;
            if (trayImg.sprite == null) trayImg.enabled = false;

            // The font, and the pint under its spout. Everything here hangs off the tower, so
            // moving the tower moves the whole rig and the spout stays over the glass. It is
            // seated ON the counter — its base sits on the surface, it does not hover over it.
            var tower = _tapTower = NewRect("Tower", _tapSurface);
            var towerPos = _tapTowerPos = new Vector2(TowerX, CounterY + _rig.Size.y * 0.5f);
            Place(tower, new Vector2(0.5f, 0.5f), _rig.Size, towerPos);
            var towerImg = _tapTowerImg = tower.gameObject.AddComponent<Image>();
            towerImg.preserveAspect = true; towerImg.raycastTarget = false;

            // The glass is the thing you hold, so it stands on the counter until you pick it up.
            // Its base rests on the surface: the rect is pivoted low, so the pivot sits a
            // fraction of the glass above the counter.
            _tapGlassRest = new Vector2(TowerX + _rig.Rest, CounterY + PintH * GlassPivotY);
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
                Sfx.Play("glass_pickup", 0.8f);
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

            // The handle: pivots at its brass collar, so pulling swings it toward you. Near
            // its own native size — blown up to 60×140 it read as a separate wooden object
            // parked beside the tap rather than the handle bolted to it. Its size and its
            // seat are the RIG's (StandTheFont): every font's own drawn handle is rubbed out
            // at ship time, because one rig must not wear two handles and only this one moves.
            _tapHandle = NewRect("Handle", _tapSurface);
            _tapHandle.pivot = new Vector2(0.5f, 0.06f);
            _tapHandle.anchorMin = _tapHandle.anchorMax = new Vector2(0.5f, 0.5f);
            var handleImg = _tapHandle.gameObject.AddComponent<Image>();
            handleImg.sprite = ItemArt.Load("tap_handle");
            handleImg.preserveAspect = true; handleImg.raycastTarget = false;
            if (handleImg.sprite == null) handleImg.color = UITheme.Amber[1];
            StandTheFont(Run != null ? Run.TapLevel : 1);

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

            // The way back is the left-edge key now (the loop rework's one back, one place), and
            // from the draught station it leads to the ROOM: the font on the counter is this
            // stage's only door, so the back-bar wall is not behind it and landing there is a
            // room the player never walked into.
            AddEdgeBack(_tapPanel, Stage.Closed, "◀  BACK TO THE ROOM");

            var done = NewRect("Done", _tapPanel);
            Place(done, new Vector2(0.5f, 0), new Vector2(240, 34), new Vector2(130, 12));
            _tapDoneBtn = done.gameObject.AddComponent<Button>();
            _tapDoneBtn.onClick.AddListener(() =>
            {
                if (!Run.ServingGlass.IsEmpty) GoTo(Stage.Closed);
            });
            _tapDoneGroup = done.gameObject.AddComponent<CanvasGroup>();
            var doneFace = NewRect("Face", done);
            Stretch(doneFace, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            KeyPlate.Dress(done, UITheme.PrimaryAction, _tapDoneBtn, doneFace);   // GDD 16 §2
            var doneLabel = NewText("Label", doneFace, _body, 8, TextAnchor.MiddleCenter, UITheme.TextOnAmber);
            Stretch(doneLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, KeyPlate.Throw), new Vector2(-4, 0));
            doneLabel.text = "SERVE IT · CLICK A CUSTOMER";
        }

        /// <summary>
        /// Restocks the bays: the keg on tap standing under the line's coupler, lettered and
        /// lit, and every OTHER stocked keg parked in a spare bay — in the shade, but real,
        /// lettered small and answering the pointer. Clicking a spare couples it (the swap
        /// goes through Core's own <see cref="TycoonRun.CanPull"/>: a glass holding a
        /// different beer refuses the change, and the verdict says why).
        /// </summary>
        private void BuildTapKegs(TycoonRun run)
        {
            if (_tapKegRow == null) return;
            foreach (Transform ch in _tapKegRow) Destroy(ch.gameObject);
            var kegSprite = ItemArt.Load("keg");

            // The keg on tap, in the plumbed bay, cropped by the hatch lintel.
            var keg = NewRect("Keg", _tapKegRow);
            keg.anchorMin = keg.anchorMax = new Vector2(0.5f, 1f);
            keg.pivot = new Vector2(0.5f, 0.5f);
            keg.sizeDelta = new Vector2(KegW, KegH);
            keg.anchoredPosition = new Vector2(KegX, -KegH * 0.5f + 26f);
            _tapKeg = keg.gameObject.AddComponent<Image>();
            _tapKeg.preserveAspect = true; _tapKeg.raycastTarget = false;
            // A keg is a keg — steel, whatever is in it. What changes with the beer is the label,
            // so the brand goes on the blank band and the style tints its ink. The bottle sprite
            // used to stand in for the keg here, which is why the stage showed a menu icon blown
            // up to prop size (2026-07-30).
            _tapKeg.sprite = kegSprite;
            _tapKeg.color = kegSprite != null ? Color.white
                : UITheme.StyleColor(_tapKegCard?.Info?.Style, IngredientType.Beer);
            // The brand, set on the keg's blank label. The art generator cannot spell, so every
            // word in this game is drawn in engine — the same rule the neon sign follows.
            _kegLabel = NewText("Brand", keg, _body, 8, TextAnchor.MiddleCenter, UITheme.Night[1]);
            var kl = _kegLabel.rectTransform;
            kl.anchorMin = new Vector2(0.08f, KegLabelCentreY - KegLabelH * 0.5f);
            kl.anchorMax = new Vector2(0.92f, KegLabelCentreY + KegLabelH * 0.5f);
            kl.offsetMin = Vector2.zero; kl.offsetMax = Vector2.zero;
            _kegLabel.text = (_tapKegCard?.Name ?? "DRAUGHT").ToUpperInvariant();
            var ink = UITheme.StyleColor(_tapKegCard?.Info?.Style, IngredientType.Beer);
            // Printed ink on a cream label: the style's hue, taken well down so it reads as
            // print rather than as a glow.
            _kegLabel.color = new Color(ink.r * 0.35f, ink.g * 0.35f, ink.b * 0.35f, 1f);

            // The spares: every other stocked keg, one bay each. Knocked back so they never
            // compete with the keg on tap — but only into the shade, not out of reach.
            int bay = 0;
            foreach (var b in run.Shelf.Bottles)
            {
                var card = b.Ingredient;
                if (card.Type != IngredientType.Beer || b.IsEmpty) continue;
                if (_tapKegCard != null && card.Id == _tapKegCard.Id) continue;
                if (bay >= SpareKegX.Length) break;

                var spare = NewRect($"SpareKeg_{card.Id}", _tapKegRow);
                spare.anchorMin = spare.anchorMax = new Vector2(0.5f, 1f);
                spare.pivot = new Vector2(0.5f, 0.5f);
                spare.sizeDelta = new Vector2(KegW * 0.88f, KegH * 0.88f);
                spare.anchoredPosition = new Vector2(SpareKegX[bay], -KegH * 0.88f * 0.5f + 16f);
                var spareImg = spare.gameObject.AddComponent<Image>();
                spareImg.sprite = kegSprite;
                spareImg.preserveAspect = true;
                spareImg.raycastTarget = true;   // the bay answers the pointer: click = couple it
                spareImg.color = kegSprite != null
                    ? new Color(0.42f, 0.38f, 0.36f, 1f)   // parked in the shade
                    : UITheme.StyleColor(card.Info?.Style, IngredientType.Beer);

                var brand = NewText("Brand", spare, _body, 8, TextAnchor.MiddleCenter, UITheme.Night[1]);
                var bl = brand.rectTransform;
                bl.anchorMin = new Vector2(0.08f, KegLabelCentreY - KegLabelH * 0.5f);
                bl.anchorMax = new Vector2(0.92f, KegLabelCentreY + KegLabelH * 0.5f);
                bl.offsetMin = Vector2.zero; bl.offsetMax = Vector2.zero;
                brand.text = card.Name.ToUpperInvariant();
                // DARKER than the on-tap keg's ink, not lighter: the whole spare is
                // multiplied down into the shade, so its label is a mid grey — ink that
                // was merely dimmed with it disappeared into the label it is printed on.
                var sink = UITheme.StyleColor(card.Info?.Style, IngredientType.Beer);
                brand.color = new Color(sink.r * 0.22f, sink.g * 0.22f, sink.b * 0.22f, 1f);

                Pressable(spare, spare, spareImg, lift: 4f, depth: 4f);
                var c = card;
                var trig = spare.gameObject.AddComponent<EventTrigger>();
                var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                down.callback.AddListener(_ => SwapKeg(c));
                trig.triggers.Add(down);
                bay++;
            }
        }

        /// <summary>Couples a different keg to the line. The pull in progress ends, the clicked
        /// keg takes the plumbed bay, and the refresh re-opens the tap through Core's own gate —
        /// a glass already holding a different beer keeps the new keg closed, and the verdict
        /// explains instead of the handle lying.</summary>
        private void SwapKeg(IngredientCard card)
        {
            var run = Run;
            if (run == null || card == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_tapKegCard != null && card.Id == _tapKegCard.Id) return;
            // ASK BEFORE UNCOUPLING. Core would refuse the new keg over a glass that
            // already holds another beer — and closing the old line first would leave the
            // tap dead over a half-poured pint, with the way back a click the player has
            // to work out for themselves. A refused swap changes nothing and says why.
            if (!run.CanPull(card.Id))
            {
                _tapVerdict.text = !run.Glass.IsEmpty
                    ? "A COCKTAIL IS IN THE TIN — POUR IT OUT FIRST"
                    : "FINISH THE PINT IN THE GLASS BEFORE CHANGING KEGS";
                _tapVerdict.color = UITheme.Amber[3];
                Sfx.Play("deny", 0.8f);
                return;
            }
            if (run.PullingId != null) run.EndPull();
            _tapKegCard = card;
            Sfx.Play("cap_on", 0.9f);
            CoupleTheKeg(run);
        }

        /// <summary>
        /// Stage entry: the pint takes its line's tier, and the keg the player opened from
        /// the wall goes on the line. <see cref="_focusBottle"/> is read HERE and nowhere
        /// else in this stage — it is the shaker's hand, and the tap borrowing it as its
        /// own coupling would leave a keg standing in the bench's hand.
        /// </summary>
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
            var entryRun = Run;
            if (entryRun == null) return;
            // THE FONT THE BAR OWNS, every time the station opens (2026-08-26). The ladder
            // is climbed at day end and this bench is built once, so the rung is re-read on
            // entry rather than trusted from build time.
            StandTheFont(entryRun.TapLevel);
            if (_focusBottle != null && _focusBottle.Type == IngredientType.Beer)
                _tapKegCard = _focusBottle;
            // NOBODY CHOSE A KEG, SO THE CELLAR CHOOSES (2026-08-15). The station used to be
            // entered by clicking a keg on the back bar, which named the beer on the way in;
            // the door is the font in the room now, and it names nothing. Without this the
            // line stood uncoupled: the title read DRAUGHT, the keg wore a blank label, and
            // CanPull was never asked, so the handle did nothing and the station looked
            // broken rather than empty. The first stocked keg in shelf order — deterministic,
            // and a swap is one click away in the bays below.
            if (_tapKegCard == null || !StillOnTap(entryRun, _tapKegCard))
                _tapKegCard = FirstStockedKeg(entryRun);
            CoupleTheKeg(entryRun);
        }

        /// <summary>Is this keg still one the bar could pour — on the shelf and not run dry?
        /// A remembered keg that has since emptied would otherwise hold the line against the
        /// full one standing next to it.</summary>
        private static bool StillOnTap(TycoonRun run, IngredientCard card)
        {
            foreach (var b in run.Shelf.Bottles)
                if (b.Ingredient.Id == card.Id) return !b.IsEmpty;
            return false;
        }

        /// <summary>The first beer the cellar still has, in shelf order.</summary>
        private static IngredientCard FirstStockedKeg(TycoonRun run)
        {
            foreach (var b in run.Shelf.Bottles)
                if (b.Ingredient.Type == IngredientType.Beer && !b.IsEmpty) return b.Ingredient;
            return null;
        }

        /// <summary>Draws the station around whichever keg is on the line: the title, the
        /// cellar, the beer's colour, and the tap opened if Core allows it. Stage entry and
        /// a bay swap both land here; only entry re-reads the glassware.</summary>
        private void CoupleTheKeg(TycoonRun run)
        {
            if (run == null) return;
            _tapTitle.text = (_tapKegCard?.Name ?? "DRAUGHT").ToUpperInvariant();

            // The cellar: the keg on tap in the plumbed bay, the spares in theirs.
            BuildTapKegs(run);

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
            // The handle only speaks when it MOVES — comparing against last frame's
            // state, because this runs every frame the station is open.
            if (pouring != _pouringNow)
            {
                Sfx.Play("tap_handle", pouring ? 0.8f : 0.55f);
                if (!pouring)
                {
                    if (run.ServingGlass.IsFull) Sfx.Play("pour_cutoff", 0.6f);
                    else if (run.ServingGlass.Head > 0) Sfx.Play("head_settle", 0.5f);
                }
            }

            _pouringNow = pouring;
            // The tap has a voice now (2026-08-13): the pull runs the same held pour loop the
            // bench does, a shade quieter — a running tap you cannot hear reads as a broken one.
            // THE SPILL WINS THE CHANNEL (2026-08-27). There is one held-loop source in
            // the whole game, so a frame that is both pouring AND spilling has to pick,
            // and it picks the spill: beer going on the floor is the thing the player
            // most needs to hear, and it is the only one of the two they can still fix.
            // The pint's own pull rises as the glass fills, the same way the bench's two
            // pours do — the air column above the beer shortens as it goes in.
            Sfx.HoldLoop(_spillingNow ? "pour_floor" : pouring ? "tap_pull" : null,
                         _spillingNow ? 0.75f : 0.6f,
                         _spillingNow ? -1f
                       : pouring ? (float)run.ServingGlass.FillFraction : -1f);
            if (pouring)
            {
                double before = run.ServingGlass.TotalVolume + run.ServingGlass.Head;
                run.PourTilted(dt, _glassTilt);
                // Spilling is a STATE, not an event: the beer keeps running past the
                // rim for as long as the glass is tipped, so the sound is held for as
                // long as it is happening. The one-shot splash it replaced fired on a
                // threshold and said nothing about how long the loss went on.
                _spillingNow = run.SpilledBeer > _spilledLast + 0.0004;
                _spilledLast = run.SpilledBeer;

                // A stream from the faucet's lip, falling into the mouth wherever it now is.
                var toMouth = mouth - spout;
                _tapFluid.EmitStream(spout, new Vector2(toMouth.x * 2.2f, -300f), dt);
                if (run.ServingGlass.TotalVolume + run.ServingGlass.Head != before) RefreshTapText(run);
            }

            run.SettleHead(dt);
            PushTapPool(run);
            _tapFluid.Step(dt);
            if (!pouring) RefreshTapText(run);

            // The SERVE key answers only a glass with beer in it — dim until then.
            bool ready = !run.ServingGlass.IsEmpty;
            if (_tapDoneGroup != null) _tapDoneGroup.alpha = ready ? 1f : 0.45f;
            if (_tapDoneBtn != null) _tapDoneBtn.interactable = ready;
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
        /// The beer line from the plumbed bay's coupler to the foot of the font. Three straight
        /// segments rather than a curve: at this scale a hose is a few pixels wide and the joints
        /// read as bends. It passes behind the counter, which is drawn after it.
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
        private Vector2 SpoutPoint() => _tapTowerPos + _rig.Spout;

        /// <summary>
        /// The point the glass turns about while it is being held: its mouth, parked under the
        /// faucet. Both the steering and the pour gate measure from HERE rather than from the
        /// spout itself — they are the same object, and a 34 px disagreement between them is not
        /// something the player should have to feel their way around (2026-07-30).
        /// </summary>
        private Vector2 TiltPivot() => SpoutPoint() + new Vector2(0f, -MouthBelowSpout);

        /// <summary>How far the rim stands above the grip on an upright glass. Reported by the
        /// glassware piece when one is drawn; the retired sprite's hand-measured fractions stay
        /// as the fallback (they agree to within a pixel on the stock pint — this is why the
        /// hardcoded pair survived so long).</summary>
        private float RimAboveGrip()
        {
            float rim = _tapPiece.Sprite != null ? _tapPiece.RimY : 0.07f + 0.82f;
            return _tapGlass.rect.height * (rim - GlassPivotY);
        }

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
            // The glass holds SOMEONE ELSE'S beer, so this keg's tap is shut. Said only for
            // that one case: "the tap is closed" is also true of a full glass and of a tin
            // with a cocktail in it, and testing the whole of CanPull here put this line
            // over the player's own finished pint and told them to bin it.
            else if (run.PullingId == null && !glass.IsEmpty && _tapKegCard != null
                     && glass.VolumeOf(_tapKegCard.Id) <= 0)
            { _tapVerdict.text = "ANOTHER BEER IS IN THE GLASS — SERVE IT OR BIN IT"; _tapVerdict.color = UITheme.ViceRed[3]; }
            else if (glass.IsEmpty) { _tapVerdict.text = "TAKE THE GLASS TO THE TAP"; _tapVerdict.color = UITheme.TextSecondary; }
            // Beer and foam share the same room, so a glass at the brim takes neither — say it,
            // because otherwise holding it under a running tap looks like the tap has died.
            else if (glass.IsFull && score < 1.0)
            { _tapVerdict.text = "THE GLASS IS FULL — LET IT SETTLE"; _tapVerdict.color = UITheme.Amber[3]; }
            else if (glass.FillFraction < 0.75) { _tapVerdict.text = "SHORT POUR"; _tapVerdict.color = UITheme.Amber[3]; }
            else if (score >= 1.0) { _tapVerdict.text = "GOOD PINT"; _tapVerdict.color = UITheme.Lime[3]; }
            else if (head > TapPour.GoodHeadMax) { _tapVerdict.text = "TOO MUCH HEAD"; _tapVerdict.color = UITheme.ViceRed[3]; }
            else { _tapVerdict.text = "FLAT — NEEDS A HEAD"; _tapVerdict.color = UITheme.ViceRed[3]; }

            SpeakVerdict(_tapVerdict.text);
        }

        /// <summary>
        /// The pint's verdict, said aloud once per change — and only once the beer has
        /// STOPPED, which is the part that is easy to get wrong.
        ///
        /// RefreshTapText runs every frame the station is open, so a Play() in any of
        /// those branches would fire sixty times a second: the exact "bozuk ses" the
        /// brief forbids, arriving at the loudest possible moment. A text-change guard
        /// alone is still not enough, because `score` crosses 1.0 back and forth WHILE
        /// beer is going in and the head is climbing — the line genuinely changes several
        /// times during one pull, and a pint on its way to good passes through TOO MUCH
        /// HEAD. So the gate is both: the line must have changed, AND the tap must be
        /// shut. A verdict is a judgement on a finished pour, not a running commentary.
        ///
        /// Only the three JUDGEMENTS speak. "TAKE THE GLASS TO THE TAP" and its siblings
        /// are instructions, and a bar that chimes at you for reading the instructions is
        /// a bar nobody can think in.
        /// </summary>
        private void SpeakVerdict(string line)
        {
            if (_pouringNow) { _spokenVerdict = line; return; }   // mid-pull: watch, don't speak
            if (line == _spokenVerdict) return;
            _spokenVerdict = line;
            if (line == "GOOD PINT") Sfx.Play("verdict_good", 0.85f);
            else if (line == "TOO MUCH HEAD") Sfx.Play("verdict_bad", 0.7f);
            else if (line == "FLAT — NEEDS A HEAD") Sfx.Play("verdict_flat", 0.7f);
        }

        private string _spokenVerdict;

        /// <summary>How much had been spilled last time the splash was heard. SpilledBeer
        /// is monotonic, so this is the only edge available.</summary>
        private double _spilledLast;

        /// <summary>Set on any frame the spill total grew; read once per frame by the
        /// stage so the one loop source is decided in a single place.</summary>
        private bool _spillingNow;
    }
}
