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
        private RectTransform _tapPanel, _tapSurface, _tapGlass;
        /// <summary>A lever at every faucet (2026-09-22): as many live as the ladder has opened, the rest dark; the
        /// one on the coupled keg's faucet swings while the pint pulls.</summary>
        private RectTransform[] _tapLevers;
        private Image[] _tapLeverImgs;
        private int _tapFaucet, _tapKegBay;
        private readonly List<RectTransform> _tapLineSegs = new List<RectTransform>();
        private Image _tapPintImage;
        private GlassArt.Piece _tapPiece;
        private bool _pouringNow;
        private MetaballFluid _tapFluid;
        private Image _tapKeg;
        private Text _tapTitle, _tapReadout, _tapVerdict;
        // THE DRAUGHT BENCH'S INSTRUMENTS (2026-09-16, the author: "Bira koyma ekranını da tamamen baştan tasarla ...
        // çok fazla detay ve işlemeden kaçın"): the same family as the shaker's — a tilt dial cut into the counter
        // left of the glass, a PINT and a HEAD column right of the font, the verdict engraved on the plaque, a light
        // that follows the glass and a mirror of the pint under its rest. The bottom status strip is gone.
        private RectTransform _tapLight, _pintMirror, _tapFontShadow, _tapPintShadow;
        /// <summary>The tilt ladder in the column (2026-09-21): nine rungs of ten degrees, lit as far as the glass leans.</summary>
        private Image[] _tiltRungs;
        private const int TiltRungs = 9;
        private readonly List<(Image icon, Text label, Image tick)> _tapStepRows = new List<(Image, Text, Image)>();
        private RectTransform _tapDone;
        private PackKey _tapDonePack;
        private bool _tapDoneReady = true;
        private Image _pintMirrorImg, _pintColFill, _headColFill;
        private const float TapColW = 24f, TapColH = 200f;
        /// <summary>The plate the tower is bolted to (2026-09-22): a recess-finished slab 24 tall under the tower, so
        /// the spouts stand a pint's height and a hand over the counter; the pint waits on it, under the faucet.</summary>
        private const float TowerPlinth = 24f, TowerPlinthW = 340f;
        private const double HeadColumnFull = 0.40;   // the HEAD column's top: twice the good band's ceiling
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
        // The pint at its own size (49 by 96, 2026-09-22): under a tower drawn at 296 by 196 a glass half the tower's
        // height is the true proportion, and the old 124 by 200 stood taller than the faucets.
        private const float PintW = 49f, PintH = 96f;
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
        /// <summary>
        /// THE TOWER (2026-09-22, the author's pick T4): the room's own tap redrawn at four times by PixelLab, 296 by
        /// 196, with THREE faucets. The bench draws a lever at each valve and lights as many as the ladder has opened
        /// (TycoonRun.TapLevel, BarRank's DraughtLines); the coupled keg's bay picks which faucet pours. Every measure
        /// is from the drawn rect's centre, y up, taken off the art by Tools (scratchpad/ship_tower.py): the spouts'
        /// lips eight over the centre, the valves on the bar's top at sixty-seven. The three fonts of the old ladder
        /// (single, arch, tee) retired with it.
        /// </summary>
        private readonly struct FontRig
        {
            public readonly string Art;         // Resources/Items
            public readonly Vector2 Size;       // the drawn rect: the art at 1x
            public readonly Vector2[] Spouts;   // the faucets' lips, left to right, from the rect's centre
            public readonly Vector2[] Valves;   // where each lever bolts on, from the same centre
            public readonly Vector2 Lever;      // how big a lever draws on this tower
            public readonly int Lines;          // how many faucets are live

            public FontRig(string art, Vector2 size, Vector2[] spouts, Vector2[] valves, Vector2 lever, int lines)
            {
                Art = art; Size = size; Spouts = spouts; Valves = valves; Lever = lever;
                Lines = Mathf.Clamp(lines, 1, spouts.Length);
            }
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
        private static FontRig RigFor(int tapLevel) => new FontRig("bench_tap_tower", new Vector2(296f, 196f),
            new[] { new Vector2(-43.5f, 8f), new Vector2(-0.5f, 8f), new Vector2(42.5f, 8f) },
            new[] { new Vector2(-43.5f, 67f), new Vector2(-0.5f, 67f), new Vector2(42.5f, 67f) },
            new Vector2(12f, 38f), tapLevel);

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
        // -250, not -170 (2026-09-21): the props stand on the bench stage's slab now, near the foot of the screen
        // like the tin and the bottle, and the plaque and SERVE IT hang under the rail above them.
        private const float CounterY = -250f;
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
        private const float TowerX = 260f;   // 150 until 2026-09-21: the kegs stand on the left now, the tower right of centre   // -50 until 2026-09-17: the pint at rest stood on the plaque's words
        /// <summary>The open recess under the bar, where the kegs live in a real one. Putting the
        /// keg BEHIND the counter hid its label under the counter line, and putting it beside the
        /// counter left it standing in the room; under the bar it is both in its right place and
        /// fully readable (2026-07-30).</summary>
        /// <summary>A keg stands under the bar and runs off the bottom of the frame — this is a
        /// close-up of the bar top, not a view of the whole room, so its foot is simply not in
        /// shot. Its base is set so the label band lands inside the recess and stays readable.</summary>
        private const float KegW = 96f, KegH = 165f;
        /// <summary>The plumbed bay: the keg standing here is the one on tap, and the beer
        /// line runs from ITS coupler. A swap moves the keg to the line, not the line to
        /// the keg — one line, as in a one-font bar.</summary>
        private const float KegBaseY = CounterY;   // on the slab, at the bench's left (2026-09-21)   // -315 until the counter dropped
        /// <summary>Where the spare kegs park, one bay each, nearest first. The recess holds
        /// four kegs in all (one on the line, three parked); the live cellar carries three
        /// beers, so the bays have never had to turn one away. A fourth beer would still be
        /// reachable from the wall's own keg row, which opens the tap on whatever it is
        /// clicked with — the bays are a shortcut, not the only door.</summary>
        /// <summary>The three bays the kegs stand in, left to right, clear of the column and the tower's left leg
        /// (2026-09-22): a keg keeps its bay whether it is coupled or parked, and its bay picks its faucet.</summary>
        private static readonly float[] BayX = { -350f, -255f, -160f };
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
        private const float MouthBelowSpout = 20f;   // 34 until 2026-09-22: a shorter pint sits closer under the lip
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
            _tapFaucet = Mathf.Clamp(_tapKegBay, 0, _rig.Lines - 1);
            if (_tapTower == null) return;
            _tapTowerPos = new Vector2(TowerX, CounterY + TowerPlinth + _rig.Size.y * 0.5f);
            _tapTower.sizeDelta = _rig.Size;
            _tapTower.anchoredPosition = _tapTowerPos;
            if (_tapTowerImg != null)
            {
                _tapTowerImg.sprite = ItemArt.Load(_rig.Art) ?? ItemArt.Load("tap");
                _tapTowerImg.color = _tapTowerImg.sprite == null ? UITheme.Amber[2] : Color.white;
            }
            if (_tapTray != null)
                _tapTray.anchoredPosition = new Vector2(TowerX + _rig.Spouts[_tapFaucet].x, CounterY + TowerPlinth + 17f);
            LayLevers();
            _tapGlassRest = new Vector2(TowerX + _rig.Spouts[_tapFaucet].x, CounterY + TowerPlinth + PintH * GlassPivotY);
            // +6 with the tin bench's: a shadow falls to one side of the thing casting it, a reflection does not
            if (_pintMirror != null) _pintMirror.anchoredPosition = new Vector2(_tapGlassRest.x + 6f, CounterY + 2f);
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

            // ON A PLAQUE UNDER THE RAIL (2026-09-16), like the other two benches' words. The title and the hint
            // used to stand at the top of the panel — under the HUD's beam, where the music player now is.
            // Three lines on it (2026-09-16): the beer's name, the VERDICT (it stood on a strip at the foot of the
            // screen), and a small line that is the hint until the glass is taken and the numbers after.
            // BESIDE THE COLUMN (2026-09-21), where the other two benches hang theirs: the column owns the far left.
            var tapPlaque = AddCounterPlaque(_tapPanel, 560f, 70f, PlaqueUnderRail, ColX + ColW + 16f);   // 480 until 2026-09-17: the hint line ran past it
            _tapTitle = NewText("Title", tapPlaque, _display, 16, TextAnchor.MiddleLeft, UITheme.TextPrimary);
            Place(_tapTitle.rectTransform, new Vector2(0f, 0f), new Vector2(560f - PlaquePad * 2f, 20f),
                  new Vector2(PlaquePad, 46f));
            _tapTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            Engraved(_tapTitle);
            _tapVerdict = NewText("Verdict", tapPlaque, _display, 16, TextAnchor.MiddleLeft, UITheme.TextPrimary);
            Place(_tapVerdict.rectTransform, new Vector2(0f, 0f), new Vector2(560f - PlaquePad * 2f, 20f),
                  new Vector2(PlaquePad, 24f));
            _tapVerdict.horizontalOverflow = HorizontalWrapMode.Overflow;
            Engraved(_tapVerdict);
            _tapReadout = NewText("Readout", tapPlaque, _body, 8, TextAnchor.MiddleLeft, UITheme.TextSecondary);
            Place(_tapReadout.rectTransform, new Vector2(0f, 0f), new Vector2(560f - PlaquePad * 2f, 14f),
                  new Vector2(PlaquePad, 4f));
            _tapReadout.horizontalOverflow = HorizontalWrapMode.Overflow;
            _tapReadout.text = UIText.T("bench.tap.hint");
            Engraved(_tapReadout);

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
            // THE SLAB IS THE ROOM'S (2026-09-21, the author's direction A: "sahne yine içki yapma sahnesine
            // benzeyecek - arkaplan, tuşlar, butonlar"). No timber front and no brass lip any more: the bench stage's
            // counter under every bench is this bench's counter too, and the tower, the pint and the kegs stand on it
            // the way the tin and the bottle do. The hatch cut into the timber went with the timber - the kegs stand in
            // a row at the bench's left, in the open, the coupled one bright and the spares parked in the shade.
            AddNeonWash(_tapSurface, over: 0);
            _tapFontShadow = AddContactShadow(_tapSurface, 320f, new Vector2(TowerX, CounterY + 4f));
            _tapFontShadow.SetSiblingIndex(1);
            _tapPintShadow = AddContactShadow(_tapSurface, PintW * 0.8f, new Vector2(TowerX + _rig.Spouts[_tapFaucet].x, CounterY + TowerPlinth + 4f));
            _tapPintShadow.SetSiblingIndex(2);
            _pintMirror = NewRect("PintMirror", _tapSurface);
            _pintMirror.anchorMin = _pintMirror.anchorMax = new Vector2(0.5f, 0.5f);
            _pintMirror.pivot = new Vector2(0.5f, 0f);       // the foot: flipped, it hangs under the line
            _pintMirror.sizeDelta = new Vector2(PintW, PintH * 0.16f);
            _pintMirror.localScale = new Vector3(1f, -0.86f, 1f);
            _pintMirror.localRotation = Quaternion.Euler(0f, 0f, -75f);   // laid almost flat, the tin bench's light
            _pintMirrorImg = _pintMirror.gameObject.AddComponent<Image>();
            _pintMirrorImg.color = new Color(0f, 0f, 0f, 0.45f);   // the glass's own shadow, not a reflection (2026-09-17)
            _pintMirrorImg.raycastTarget = false;
            _pintMirrorImg.preserveAspect = false;

            _tapKegRow = NewRect("KegRow", _tapSurface);
            Stretch(_tapKegRow, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // The line from the keg's coupler to the foot of the font, so the two read as one
            // plumbed-in rig instead of two props that happen to share a screen. It dives
            // behind the counter on its way. The line is FIXED: it serves the plumbed bay,
            // and a swap stands a different keg under it.

            // The drip tray, on the counter directly under the faucet. Re-placed with the
            // font (StandTheFont): a taller tower puts its lip somewhere else along the bar.
            // THE PLINTH (2026-09-22): the plate the tower is bolted to, in the counter's own recess finish with a brass
            // lip, standing on the slab under the tower; the tray and the pint stand on it too.
            var plinth = NewRect("TowerPlinth", _tapSurface);
            Place(plinth, new Vector2(0.5f, 0.5f), new Vector2(TowerPlinthW, TowerPlinth), new Vector2(TowerX, CounterY + TowerPlinth * 0.5f));
            var plImg = plinth.gameObject.AddComponent<Image>();
            plImg.sprite = CounterFinish.Recess(); plImg.type = Image.Type.Sliced; plImg.color = Color.white; plImg.raycastTarget = false;
            var plLip = NewRect("Lip", plinth);
            Stretch(plLip, new Vector2(0f, 1f), Vector2.one, new Vector2(3f, -5f), new Vector2(-3f, -3f));
            var plLipImg = plLip.gameObject.AddComponent<Image>();
            plLipImg.color = UITheme.Amber[2]; plLipImg.raycastTarget = false;

            var tray = _tapTray = NewRect("DripTray", _tapSurface);
            var trayPos = new Vector2(TowerX + _rig.Spouts[_tapFaucet].x, CounterY + TowerPlinth + 17f);
            Place(tray, new Vector2(0.5f, 0.5f), new Vector2(132, 33), trayPos);
            var trayImg = tray.gameObject.AddComponent<Image>();
            trayImg.sprite = ItemArt.Load("drip_tray");
            trayImg.preserveAspect = true; trayImg.raycastTarget = false;
            if (trayImg.sprite == null) trayImg.enabled = false;

            // The font, and the pint under its spout. Everything here hangs off the tower, so
            // moving the tower moves the whole rig and the spout stays over the glass. It is
            // seated ON the counter — its base sits on the surface, it does not hover over it.
            var tower = _tapTower = NewRect("Tower", _tapSurface);
            var towerPos = _tapTowerPos = new Vector2(TowerX, CounterY + TowerPlinth + _rig.Size.y * 0.5f);
            Place(tower, new Vector2(0.5f, 0.5f), _rig.Size, towerPos);
            var towerImg = _tapTowerImg = tower.gameObject.AddComponent<Image>();
            towerImg.preserveAspect = true; towerImg.raycastTarget = false;

            // The glass is the thing you hold, so it stands on the counter until you pick it up.
            // Its base rests on the surface: the rect is pivoted low, so the pivot sits a
            // fraction of the glass above the counter.
            _tapGlassRest = new Vector2(TowerX + _rig.Spouts[_tapFaucet].x, CounterY + TowerPlinth + PintH * GlassPivotY);
            _tapGlass = NewRect("Pint", _tapSurface);
            Place(_tapGlass, new Vector2(0.5f, 0.5f), new Vector2(PintW, PintH), _tapGlassRest);
            // Pivoted low, near where a hand holds it: a glass leans off its base, it does not
            // swing about its middle (2026-07-27).
            _tapGlass.pivot = new Vector2(0.5f, GlassPivotY);
            var pint = _tapPintImage = _tapGlass.gameObject.AddComponent<Image>();
            // The SAME glass everywhere (the author, 2026-08-02): the pint under the tap is
            // the drawn glassware pint at its line's tier, refreshed on stage entry. Until
            // then it wears the line's first tier, so a run built without glassware still
            // has a glass here (the generated pint.png it used to fall back on was swept
            // on 2026-09-05).
            pint.sprite = ItemArt.Load("glass3d_pint");
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
            // A LEVER AT EVERY FAUCET (2026-09-22): the same drawn lever three times, hung from its foot at each
            // valve; LayLevers lights the open ones and darkens the rest, UpdateTap swings the one that pours.
            _tapLevers = new RectTransform[3];
            _tapLeverImgs = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var lever = NewRect("Lever" + i, _tapSurface);
                lever.pivot = new Vector2(0.5f, 0.06f);
                lever.anchorMin = lever.anchorMax = new Vector2(0.5f, 0.5f);
                var li = lever.gameObject.AddComponent<Image>();
                li.sprite = ItemArt.Load("tap_handle");
                li.preserveAspect = true; li.raycastTarget = false;
                if (li.sprite == null) li.color = UITheme.Amber[1];
                _tapLevers[i] = lever; _tapLeverImgs[i] = li;
            }
            StandTheFont(Run != null ? Run.TapLevel : 1);

            // A plate under the verdict and the readout. They used to sit straight on top of the
            // shelving, which read as text spilled over the art rather than as a status strip.
            BuildTapInstruments();

            // The way back is the left-edge key now (the loop rework's one back, one place), and
            // from the draught station it leads to the ROOM: the font on the counter is this
            // stage's only door, so the back-bar wall is not behind it and landing there is a
            // room the player never walked into.
            AddEdgeBack(_tapPanel, Stage.Closed, UIText.T("bench.tap.back_to_room"));
            // THE BIN, AS ON THE OTHER TWO BENCHES (2026-09-21): a pulled pint can be thrown away here too.
            AddBinButton(_tapPanel);

            // SERVE IT ON THE PLATE FAMILY AT THE PLAQUE'S RIGHT END (2026-09-21): the glass bench's key in the same
            // place - the one accent on the bench, hung from the rail beside the plaque, the word alone. It was a
            // 240x64 slab of its own in the corner.
            // The glass bench's own word on it: what to do after is the room's to say over the standing pint.
            _tapDone = PackKeyFlow(_tapPanel, "Done", UIText.T("bench.serve.serve_key"), null, KeyGo,
                new Vector2(0f, 0f), new Vector2(ServeKeyW, PlaqueH), new Vector2(ColX + ColW + 16f + PlaqueW + 16f, 0f),
                () => { if (Run != null && !Run.ServingGlass.IsEmpty) GoTo(Stage.Closed); },
                out _tapDoneBtn, out _tapDonePack, 16);
            _railHung.Add((_tapDone, RailBandH + PlaqueUnderRail + PlaqueH));
            _benchCounterTop = -1f;
            _tapDoneGroup = _tapDone.gameObject.AddComponent<CanvasGroup>();
            var doneLabel = _tapDone.Find("Face/Label").GetComponent<Text>();
            doneLabel.font = _display;
            Stretch(doneLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 4f), new Vector2(-20f, 0f));
            FitWord(doneLabel, new Vector2(ServeKeyW - 40f, PlaqueH - 8f), 16, 8);
        }

        /// <summary>
        /// Restocks the bays: the keg on tap standing under the line's coupler, lettered and
        /// lit, and every OTHER stocked keg parked in a spare bay — in the shade, but real,
        /// lettered small and answering the pointer. Clicking a spare couples it (the swap
        /// goes through Core's own <see cref="TycoonRun.CanPull"/>: a glass holding a
        /// different beer refuses the change, and the verdict says why).
        /// </summary>
        /// <summary>The tilt dial left of the glass's rest and the two columns right of the font — the shaker bench's
        /// pour dial and MIX column, cut into this counter.</summary>
        private void BuildTapInstruments()
        {
            // THE INSTRUMENT COLUMN, AS ON THE TIN BENCH (2026-09-21): the steps listed one under another at the top,
            // the TILT LADDER under them where the tin bench keeps its lift ladder - nine rungs of ten degrees, lit as
            // far as the glass leans: amber while it stands too straight (that pours foam), lime through the good
            // band about 45°, the vice red past the spill line - and the way out at the foot. The half-round dial the
            // author sent back on the tin bench ("daha profesyonel olsun") is gone from here too.
            var stepPanel = ColumnPanel(_tapPanel, "StepPanel", ColStepsTop - StepPanelH(4), StepPanelH(4));
            _tapStepRows.Clear();
            BuildStepList(stepPanel,
                new[] { UIText.T("bench.tap.step.glass"), UIText.T("bench.tap.step.pull"),
                        UIText.T("bench.tap.step.head"), UIText.T("bench.tap.step.serve") },
                new[] { "step_glass", "step_fill", "step_cap", "step_glass" }, _tapStepRows);

            var ladder = ColumnPanel(_tapPanel, "TiltLadder", ColDialY, ColDialH);
            float ladderW = TiltRungs * RungW + (TiltRungs - 1) * RungGap;
            float x0 = ColPad + (ColW - ColPad * 2f - ladderW) * 0.5f;
            _tiltRungs = new Image[TiltRungs];
            for (int i = 0; i < TiltRungs; i++)
            {
                float h = RungBase + i * RungRise;
                var rung = NewRect("Rung" + i, ladder);
                Place(rung, new Vector2(0f, 0f), new Vector2(RungW, h), new Vector2(x0 + i * (RungW + RungGap), RungFootY));
                var tube = rung.gameObject.AddComponent<Image>();
                tube.sprite = ChromeArt.GaugeTube((int)(RungW * 0.5f), (int)(h * 0.5f));
                tube.color = CounterFinish.Ramp(CounterFinish.Current.Slab, -0.35f);
                tube.raycastTarget = false;
                var lit = NewRect("Lit", rung);
                Stretch(lit, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
                var li = lit.gameObject.AddComponent<Image>();
                li.sprite = ChromeArt.Solid();
                li.raycastTarget = false;
                li.enabled = false;
                _tiltRungs[i] = li;
            }
            var word = NewText("Hint", ladder, _body, 8, TextAnchor.LowerCenter, UITheme.TextSecondary);
            Stretch(word.rectTransform, Vector2.zero, new Vector2(1f, 0f),
                    new Vector2(ColPad, ColPad + 8f), new Vector2(-ColPad, ColPad + 32f));
            word.horizontalOverflow = HorizontalWrapMode.Wrap;
            word.text = UIText.T("bench.tap.dial_hint");
            Engraved(word);
            LightTiltLadder(0f);

            // right of the tower, clear of its drawing at 296 wide (2026-09-21)
            _pintColFill = BuildTapColumn("PintColumn", TowerX + 190f, UIText.T("bench.tap.col.pint"), new[] { 0.75f });
            _headColFill = BuildTapColumn("HeadColumn", TowerX + 190f + TapColW + 14f, UIText.T("bench.tap.col.head"),
                new[] { (float)(TapPour.GoodHeadMin / HeadColumnFull), (float)(TapPour.GoodHeadMax / HeadColumnFull) });
            StepTapInstruments(Run);
        }

        private Image BuildTapColumn(string name, float x, string head, float[] marks)
        {
            var rig = NewRect(name, _tapSurface);
            Place(rig, new Vector2(0.5f, 0.5f), new Vector2(TapColW, TapColH), new Vector2(x, CounterY + 12f + TapColH * 0.5f));
            var tube = rig.gameObject.AddComponent<Image>();
            tube.sprite = ChromeArt.GaugeTube((int)TapColW, (int)TapColH);
            tube.color = UITheme.Night[2];
            tube.raycastTarget = false;
            var inner = NewRect("Inner", rig);
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            var fillRt = NewRect("Fill", inner);
            Stretch(fillRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = ChromeArt.Solid();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0f;
            fill.color = CounterFinish.Current.Accent;
            fill.raycastTarget = false;
            var glass = NewRect("Glass", rig);
            Stretch(glass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gi = glass.gameObject.AddComponent<Image>();
            gi.sprite = ChromeArt.GaugeGlass((int)TapColW, (int)TapColH, 5);
            gi.raycastTarget = false;
            BrassRim(rig);   // the rim the tin bench's columns wear (2026-09-21)
            foreach (float m in marks)
            {
                var mark = NewRect("Mark", rig);
                Place(mark, new Vector2(0.5f, 0f), new Vector2(TapColW + 8f, 2f), new Vector2(0f, 2f + m * (TapColH - 4f)));
                mark.pivot = new Vector2(0.5f, 0.5f);
                var mi = mark.gameObject.AddComponent<Image>();
                mi.color = UITheme.Cream[4];
                mi.raycastTarget = false;
            }
            var word = NewText("Head", rig, _body, 8, TextAnchor.UpperCenter, UITheme.TextSecondary);
            word.rectTransform.anchorMin = new Vector2(0, 0); word.rectTransform.anchorMax = new Vector2(1, 0);
            word.rectTransform.pivot = new Vector2(0.5f, 1);
            word.rectTransform.offsetMin = new Vector2(-20, -16); word.rectTransform.offsetMax = new Vector2(20, -4);
            word.text = head;
            word.raycastTarget = false;
            Engraved(word);
            return fill;
        }

        /// <summary>Every frame: the needle on the glass's angle, the columns on the pint, the light on the glass in
        /// hand, the mirror under the glass at rest.</summary>
        private void StepTapInstruments(TycoonRun run)
        {
            LightTiltLadder(_glassTilt);
            PaintTapSteps(run);
            if (run != null && _pintColFill != null && _headColFill != null)
            {
                var g = run.ServingGlass;
                float fill = Mathf.Clamp01((float)g.FillFraction);
                _pintColFill.fillAmount = fill;
                _pintColFill.color = fill >= 0.75f ? UITheme.Lime[3] : CounterFinish.Current.Accent;
                double head = g.Capacity > 0 ? g.Head / g.Capacity : 0.0;
                _headColFill.fillAmount = Mathf.Clamp01((float)(head / HeadColumnFull));
                _headColFill.color = head >= TapPour.GoodHeadMin && head <= TapPour.GoodHeadMax ? UITheme.Lime[3] : UITheme.Cream[3];
            }
            if (_tapPintShadow != null && _tapGlass != null)
                PushPropShadow(_tapPintShadow, _tapGlass, _tapGlassRest.y, CounterY + 4f, PintW * 0.8f, 1f);
            if (_pintMirror != null)
            {
                bool show = !_glassHeld && _pintMirrorImg != null && _pintMirrorImg.sprite != null;
                if (_pintMirror.gameObject.activeSelf != show) _pintMirror.gameObject.SetActive(show);
            }
            if (_tapLight != null)
            {
                Vector2 want = _glassHeld && _tapGlass != null ? _tapGlass.anchoredPosition + new Vector2(0f, 60f)
                    : new Vector2(TowerX, CounterY + 120f);
                _tapLight.anchoredPosition = Vector2.Lerp(_tapLight.anchoredPosition, want, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            }
        }

        /// <summary>The ladder lit as far as the glass leans, each rung in the colour of what that angle pours.</summary>
        private void LightTiltLadder(float tilt)
        {
            if (_tiltRungs == null) return;
            float step = 90f / TiltRungs;
            int lit = Mathf.Clamp(Mathf.CeilToInt(tilt / step - 0.05f), 0, TiltRungs);
            for (int i = 0; i < _tiltRungs.Length; i++)
            {
                var img = _tiltRungs[i];
                if (img == null) continue;
                bool on = i < lit;
                if (img.enabled != on) img.enabled = on;
                if (!on) continue;
                float top = (i + 1) * step;   // the rung's own angle
                img.color = top < (float)TapPour.IdealTilt - 10f ? UITheme.Amber[3]
                          : top <= (float)TapPour.SpillTilt ? UITheme.Lime[3] : UITheme.ViceRed[3];
            }
        }

        /// <summary>Which step the pint is on, read off the glass the way the tin bench reads its card.</summary>
        private void PaintTapSteps(TycoonRun run)
        {
            if (_tapStepRows.Count == 0 || run == null) return;
            var g = run.ServingGlass;
            double head = g.Capacity > 0 ? g.Head / g.Capacity : 0.0;
            bool full = g.FillFraction >= 0.75;
            bool headGood = head >= TapPour.GoodHeadMin && head <= TapPour.GoodHeadMax;
            int at = g.IsEmpty && !_glassHeld ? 0 : !full ? 1 : !headGood ? 2 : 3;
            PaintSteps(_tapStepRows, at, -1, false);
        }

        private void BuildTapKegs(TycoonRun run)
        {
            if (_tapKegRow == null) return;
            foreach (Transform ch in _tapKegRow) Destroy(ch.gameObject);
            var kegSprite = ItemArt.Load("keg");
            _tapKegBay = 0;
            int bay = 0;
            // EVERY KEG KEEPS ITS BAY (2026-09-22): the stocked kegs stand left to right in shelf order, the coupled
            // one bright and the spares parked in the shade, clickable; the coupled keg's bay picks the faucet.
            foreach (var b in run.Shelf.Bottles)
            {
                var card = b.Ingredient;
                if (card.Type != IngredientType.Beer || b.IsEmpty) continue;
                if (bay >= BayX.Length) break;
                bool coupled = _tapKegCard != null && card.Id == _tapKegCard.Id;
                float scale = coupled ? 1f : 0.88f;
                var keg = NewRect(coupled ? "Keg" : $"SpareKeg_{card.Id}", _tapKegRow);
                keg.anchorMin = keg.anchorMax = new Vector2(0.5f, 0.5f);
                keg.pivot = new Vector2(0.5f, 0.5f);
                keg.sizeDelta = new Vector2(KegW * scale, KegH * scale);
                keg.anchoredPosition = new Vector2(BayX[bay], KegBaseY + KegH * scale * 0.5f);   // its foot on the slab
                var img = keg.gameObject.AddComponent<Image>();
                img.sprite = kegSprite; img.preserveAspect = true;
                img.raycastTarget = !coupled;   // a spare answers the pointer: click = couple it
                img.color = kegSprite == null ? UITheme.StyleColor(card.Info?.Style, IngredientType.Beer)
                          : coupled ? Color.white : new Color(0.42f, 0.38f, 0.36f, 1f);
                var brand = NewText("Brand", keg, _body, 8, TextAnchor.MiddleCenter, UITheme.Night[1]);
                var bl = brand.rectTransform;
                bl.anchorMin = new Vector2(0.08f, KegLabelCentreY - KegLabelH * 0.5f);
                bl.anchorMax = new Vector2(0.92f, KegLabelCentreY + KegLabelH * 0.5f);
                bl.offsetMin = Vector2.zero; bl.offsetMax = Vector2.zero;
                brand.text = UIText.Caps(UIText.Data("bottle", card.Id, "name", card.Name));
                var ink = UITheme.StyleColor(card.Info?.Style, IngredientType.Beer);
                float dim = coupled ? 0.35f : 0.22f;
                brand.color = new Color(ink.r * dim, ink.g * dim, ink.b * dim, 1f);
                if (coupled) { _tapKeg = img; _kegLabel = brand; _tapKegBay = bay; }
                else
                {
                    Pressable(keg, keg, img, lift: 4f, depth: 4f);
                    var c = card;
                    var trig = keg.gameObject.AddComponent<EventTrigger>();
                    var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                    down.callback.AddListener(_ => SwapKeg(c));
                    trig.triggers.Add(down);
                }
                bay++;
            }
            StandTheFont(run.TapLevel);   // the faucet, the tray, the levers and the pint's rest follow the bay
            BuildBeerLine();
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
                    ? UIText.T("bench.tap.swap.cocktail")
                    : UIText.T("bench.tap.swap.pint");
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
                        if (_pintMirrorImg != null) _pintMirrorImg.sprite = tapPiece.Sprite;
                        _tapPintImage.color = Color.white;
                        // AND ITS FRONT, over the beer (2026-09-09): the pint is the one
                        // glass in the game that is always full of something, and it was the
                        // one glass drawn without its rim strip.
                        GlassArt.Lip((RectTransform)_tapPintImage.transform, tapPiece);
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

        /// <summary>The coupled keg's name as the title and the label print it: the bottle's
        /// data line in capitals, or DRAUGHT when no keg (or no name) is on the line.</summary>
        private string KegTitle() =>
            _tapKegCard?.Name != null
                ? UIText.Caps(UIText.Data("bottle", _tapKegCard.Id, "name", _tapKegCard.Name))
                : UIText.T("bench.tap.draught");

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
            _tapTitle.text = KegTitle();

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
            StepTapInstruments(run);

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
            if (_tapLevers != null)
                for (int i = 0; i < _tapLevers.Length; i++)
                    if (_tapLevers[i] != null)
                        _tapLevers[i].localRotation = Quaternion.Euler(0, 0, pouring && i == _tapFaucet ? HandleTilt : 0f);
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
                // A steady column, a little under the hand pour's rest girth (2026-09-11): the
                // stream became a rope of radius-10 nodes, and a tap's flow is fixed and modest
                // beside a tin tipped right over — the head is what this bench is read by.
                _tapFluid.EmitStream(spout, new Vector2(toMouth.x * 2.2f, -300f), dt, 0.8f);
                if (run.ServingGlass.TotalVolume + run.ServingGlass.Head != before) RefreshTapText(run);
            }

            run.SettleHead(dt);
            PushTapPool(run);
            _tapFluid.Step(dt);
            if (!pouring) RefreshTapText(run);

            // The SERVE key answers only a glass with beer in it — dim until then.
            bool ready = !run.ServingGlass.IsEmpty;
            if (_tapDoneGroup != null) _tapDoneGroup.alpha = ready ? 1f : 0.8f;
            if (_tapDoneBtn != null) _tapDoneBtn.interactable = ready;
            if (ready != _tapDoneReady)   // the glass bench's rule: the key goes to chrome while there is nothing to serve
            {
                _tapDoneReady = ready;
                RetonePackKey(_tapDone, ready ? KeyGo : KeyWay);
            }
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
            // The beer is drawn in the pint's own pixels, counted from the drawing's corner
            // (the aspect-fit picture, not the letterboxed rect, which turns about its foot).
            var pint = _tapPiece.Sprite;
            if (pint != null && pint.rect.height >= 1f)
            {
                float tw = _tapGlass.rect.width, th = _tapGlass.rect.height;
                float ak = Mathf.Min(tw / pint.rect.width, th / pint.rect.height);
                var tc = _tapGlass.anchoredPosition;
                _tapFluid.SetPixelGrid(ak, new Vector2(
                    tc.x - tw * _tapGlass.pivot.x + (tw - pint.rect.width * ak) * 0.5f,
                    tc.y - th * _tapGlass.pivot.y + (th - pint.rect.height * ak) * 0.5f));
            }

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
                // The pint's floor is an arc too (2026-09-07, GlassArt.Piece.FloorArc).
                _tapFluid.SetFloorArc(_tapPiece.Sprite != null ? _tapPiece.FloorArc * _tapGlass.rect.height : 0f);
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
        // (BuildTapCounter - the dark timber front and the brass lip - came down on 2026-09-21: the bench stage's slab
        //  is the counter on this bench as on the other two.)

        /// <summary>
        /// The beer line from the plumbed bay's coupler to the foot of the font. Three straight
        /// segments rather than a curve: at this scale a hose is a few pixels wide and the joints
        /// read as bends. It passes behind the counter, which is drawn after it.
        /// </summary>
        private void BuildBeerLine()
        {
            // Up out of the coupler, along under the bar top, and into the foot of the font.
            // From the coupled keg's top, up a hand, across the counter behind the pint, down into the tower's
            // left foot (2026-09-21: the kegs stand on the slab, so the line runs over it rather than under it).
            // REBUILT WHENEVER A KEG IS COUPLED (2026-09-22): from the coupled keg's bay, up a hand, across the counter
            // behind the pint, down into the plinth under the faucet that pours.
            foreach (var seg in _tapLineSegs) if (seg != null) Destroy(seg.gameObject);
            _tapLineSegs.Clear();
            if (_tapKegRow == null) return;
            float kegX = BayX[Mathf.Clamp(_tapKegBay, 0, BayX.Length - 1)];
            // ...into the tower's LEFT LEG rather than the faucet's own x (r87): the leg hides the drop, and a hose
            // dropping through the arch's open middle read as a cable over the pint.
            float tapX = TowerX - 105.5f;   // the left leg's centre in the drawing (art x 17..68 of 296)
            var coupler = new Vector2(kegX, KegBaseY + KegH - 6f);
            var rise = new Vector2(kegX, KegBaseY + KegH + 14f);
            var run = new Vector2(tapX, KegBaseY + KegH + 14f);
            LineSegment(coupler, rise);
            LineSegment(rise, run);
            LineSegment(run, new Vector2(tapX, CounterY + TowerPlinth * 0.5f));
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
            // GRAPHITE, NOT BLACK (2026-09-22): the hose lies on the slab now, and black on Night was invisible (r86).
            img.color = UITheme.Graphite[3];
            img.raycastTarget = false;
            seg.SetSiblingIndex(_tapKegRow.GetSiblingIndex() + 1);   // over the kegs, under the plinth and the tower
            _tapLineSegs.Add(seg);
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
        private Vector2 SpoutPoint() => _tapTowerPos + _rig.Spouts[_tapFaucet];

        /// <summary>The levers on their valves: sized to the rig, the open ones bright, the closed ones dark - the
        /// second and third handles wait for the ladder (BarRank: SecondLine, ThirdLine).</summary>
        private void LayLevers()
        {
            if (_tapLevers == null) return;
            for (int i = 0; i < _tapLevers.Length; i++)
            {
                var lever = _tapLevers[i];
                if (lever == null) continue;
                bool open = i < _rig.Lines && i < _rig.Valves.Length;
                lever.sizeDelta = _rig.Lever;
                lever.anchoredPosition = _tapTowerPos + (i < _rig.Valves.Length ? _rig.Valves[i] : Vector2.zero);
                if (_tapLeverImgs[i] != null && _tapLeverImgs[i].sprite != null)
                    _tapLeverImgs[i].color = open ? Color.white : new Color(0.32f, 0.30f, 0.34f, 1f);
            }
        }

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
            // Numbers go in formatted exactly as the interpolation did (current culture); the
            // spilled clause is its own whole line so a translator can move it.
            string tiltS = ((int)_glassTilt).ToString(), fillS = fillPct.ToString(),
                   headS = headPct.ToString(), leftS = left.ToString("0.#");
            // The small line is the hint until the glass is taken, the numbers from then on (2026-09-16).
            _tapReadout.text = glass.IsEmpty && !_glassHeld ? UIText.T("bench.tap.hint")
                : run.SpilledBeer > 0.02
                ? UIText.T("bench.tap.readout_spilled", ("tilt", tiltS), ("fill", fillS), ("head", headS),
                    ("spilled", run.SpilledBeer.ToString("0.0")), ("left", leftS))
                : UIText.T("bench.tap.readout", ("tilt", tiltS), ("fill", fillS), ("head", headS), ("left", leftS));

            // While it is running, the glass's angle is the live thing to say; once it is down,
            // the pint is what there is to judge.
            if (_glassHeld && !_pouringNow && Run != null && !Run.ServingGlass.IsEmpty)
            { _tapVerdict.text = UIText.T("bench.tap.verdict.hold_under"); _tapVerdict.color = UITheme.Amber[3]; }
            else if (_glassHeld && _glassTilt > TapPour.SpillTilt)
            { _tapVerdict.text = UIText.T("bench.tap.verdict.spilling"); _tapVerdict.color = UITheme.ViceRed[3]; }
            // The glass holds SOMEONE ELSE'S beer, so this keg's tap is shut. Said only for
            // that one case: "the tap is closed" is also true of a full glass and of a tin
            // with a cocktail in it, and testing the whole of CanPull here put this line
            // over the player's own finished pint and told them to bin it.
            else if (run.PullingId == null && !glass.IsEmpty && _tapKegCard != null
                     && glass.VolumeOf(_tapKegCard.Id) <= 0)
            { _tapVerdict.text = UIText.T("bench.tap.verdict.other_beer"); _tapVerdict.color = UITheme.ViceRed[3]; }
            else if (glass.IsEmpty) { _tapVerdict.text = UIText.T("bench.tap.verdict.take_glass"); _tapVerdict.color = UITheme.TextSecondary; }
            // Beer and foam share the same room, so a glass at the brim takes neither — say it,
            // because otherwise holding it under a running tap looks like the tap has died.
            else if (glass.IsFull && score < 1.0)
            { _tapVerdict.text = UIText.T("bench.tap.verdict.settle"); _tapVerdict.color = UITheme.Amber[3]; }
            else if (glass.FillFraction < 0.75) { _tapVerdict.text = UIText.T("bench.tap.verdict.short"); _tapVerdict.color = UITheme.Amber[3]; }
            else if (score >= 1.0) { _tapVerdict.text = UIText.T("bench.tap.verdict.good"); _tapVerdict.color = UITheme.Lime[3]; }
            else if (head > TapPour.GoodHeadMax) { _tapVerdict.text = UIText.T("bench.tap.verdict.too_much_head"); _tapVerdict.color = UITheme.ViceRed[3]; }
            else { _tapVerdict.text = UIText.T("bench.tap.verdict.flat"); _tapVerdict.color = UITheme.ViceRed[3]; }

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
            // Compared against the same keys the verdict was set from, so the chime survives a language.
            if (line == UIText.T("bench.tap.verdict.good")) Sfx.Play("verdict_good", 0.85f);
            else if (line == UIText.T("bench.tap.verdict.too_much_head")) Sfx.Play("verdict_bad", 0.7f);
            else if (line == UIText.T("bench.tap.verdict.flat")) Sfx.Play("verdict_flat", 0.7f);
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
