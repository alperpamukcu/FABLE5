using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The diegetic gameplay stage: the painted night-club room authored at 640×360 that
    /// sits BEHIND the UI overlay. What lives here today is exactly what the player sees —
    /// the room art with its lettered neon sign, the bar counter, and the clickable till
    /// with the wallet in its display window. Everything interactive above it (seats,
    /// patrons, glasses, menus) belongs to <see cref="TycoonHud"/> and the service flow.
    ///
    /// IN THE WORLD NOW, NOT ON A CANVAS (2026-08-10). The room and the counter draw as
    /// world-space SpriteRenderers under the URP 2D Renderer, because that is the one
    /// place a Light2D can reach them: an overlay canvas is composited after the camera
    /// and no light or bloom can ever touch it. The room carries real light — a global
    /// wash, a warm pool under each of the four painted lamps, and the sign's own spill —
    /// and the modular fixtures (bar upgrades) will be world sprites lit by the same
    /// system. What must draw OVER the HUD's patrons stays canvas: the till (order 6),
    /// its shadow and the wallet plaque (−10), and the lettered sign (−9), whose pixel
    /// text needs the canvas rasterizer.
    ///
    /// One world unit is one stage unit, and the PixelPerfectCamera draws it at a WHOLE
    /// number of screen pixels — two at a 1052px window, three at 1440p. How much room
    /// that shows follows from the window: 526 units of height at 1052px, not 360, with
    /// the backdrop, the picture and the counter all bleeding out to fill it. Spare window
    /// is spare bar; there are no black bars, and the camera is never cropped.
    ///
    /// The overlay canvases are pinned to that same whole number (see
    /// <see cref="DesignFrame"/>) rather than scaling smoothly off the height, which is
    /// what they used to do and is what made the props slide on the counter: the world
    /// drew at 2× while the UI drew at 2.92×. All public coordinates remain in the
    /// 640×360 reference space with a bottom-left origin.
    /// </summary>
    public sealed class DiegeticStage : MonoBehaviour
    {
        // ── layout (stage units, bottom-left origin) ────────────────────────────
        private static readonly Vector2 Reference = new Vector2(640, 360);

        /// <summary>
        /// The bar's FRONT EDGE in stage units — the brass line a customer leans on, and the
        /// line their body is cut off at. Public because TycoonHud draws the seats and has to
        /// agree with it exactly; it used to keep its own copy of the number and the two drifted
        /// apart when the art changed.
        ///
        /// This is the rest line, NOT the sprite's top: the counter art carries two transparent
        /// rows above its brass edge, so cutting the bodies at the sprite's top left a two-unit
        /// sliver of backdrop showing under every customer — measured at 8 screen pixels on a
        /// 1440p frame (2026-07-29).
        /// </summary>
        public const float CounterTopY = CounterRestY;

        // ── the bar front's shelf compartments ───────────────────────────────────
        // The counter art is not decoration: it carries EIGHT compartments across its
        // front, and those are where the bought glassware belongs. RE-MEASURED off the
        // 2026-08-18 marble-and-graphite counter (14 v3 §11.F: new background, new
        // measurement) by sampling the cabinet band's column profile.
        //
        // THE CELLS ARE NO LONGER EVENLY SPACED, and that is the whole reason this is a
        // table now. The old turquoise bar front was eight identical 80-wide bays, so a
        // pitch was enough. The new front is joinery — two cabinet doors, three glass
        // fridges, three open bays — and a pitch would stand a glass squarely on a stile.
        //
        // RE-MEASURED AGAIN for the 2026-08-19 brutalist counter, and THIS TABLE IS ART-BOUND:
        // it is measured off whatever counter.png currently is, and a new counter invalidates
        // it silently — a stale table stands a bought glass on a steel stile. Re-measure by
        // running the column-edge scan and then LOOKING at the eight ticks drawn on the plate.
        //
        // MEASURED ON THE BAR AS DRAWN, not on the sprite: the counter is 9-sliced and tiled
        // out to 807 art px (see CounterMiddleTiles), so the sprite's own x is not where a
        // thing ends up. Working in drawn space is also what keeps every cell ON SCREEN —
        // the first pass mapped the sprite's table through the slice and put cells 1 and 8
        // outside the frame, where a bought glass is drawn and never seen.
        //
        // What is visible of the drawn bar, inside the screen's 83..722: one cabinet door
        // (91..139) and three glazed bays (168..330, 336..497, 503..681). The right-hand door
        // lands almost entirely off-frame — 20 px of it survive — so it carries nothing. The
        // eight cells are spread over the rest in proportion to width: the door takes one and
        // the three bays take two, two and three.
        private static readonly float[] ShelfCentrePx =
            { 115f, 208f, 290f, 376f, 457f, 533f, 592f, 651f };
        //
        // The numbers below are in the ART's own pixels. They only equal stage units at
        // the reference aspect: the counter is scaled by visibleWidth/640 and hangs from
        // the rest line, so at 16:10 the cells sit narrower AND higher. ShelfCell resolves
        // that from the live fit rather than assuming 16:9.
        //
        // The band a glass is drawn in. 124 is where it stands and 84 is the headroom above,
        // measured to sit inside the bays' own openings on the 2026-08-19 front — this
        // counter draws no shelf boards, so the band is a display line rather than a plank,
        // and it is checked the same way the columns are: by drawing it on the plate. The
        // band is 40 art px against the older front's 52, so the glassware run draws a little
        // smaller; that follows the art and is not a number to tune back up on its own.
        private const float ShelfFloorPx = 124f;    // the near edge, art px from the art's top
        /// <summary>How deep the drawn shelf surface is, front edge to back, in art px.</summary>
        public const float ShelfDepthPx = 9f;
        private const float ShelfCeilPx = 84f;      // the shelf board above it

        private Transform _counterTr;
        private Vector2 _counterNative;
        private float _counterScale;                // stage units per counter-art pixel

        // ── the cellar drawer (2026-08-22) ──────────────────────────────────────
        // The back bar is not a room you travel to any more; it is the counter's own body,
        // shut behind a roller. MEASURED off the two pieces rather than chosen: the shelf
        // opening is rows 65..241 of the cropped counter — 176 tall, which is the shutter's
        // height to the pixel, because the author drew the two to each other.
        private const float ShutterOpeningTopPx = 65f;   // art px below the counter's top edge
        /// <summary>How far the room rises to bring the cellar into frame. Read off the
        /// author's own mock-ups: the slab's dark band sits at screen row 240 shut and 119
        /// open, and nothing had to be invented to fill the gap because the counter hangs
        /// exactly this far below the screen.</summary>
        // 121 → 131 (2026-09-04, the author: "backbarın açık halini 10 pixel yukarı çek").
        // Ten of the ROOM's own pixels, which is the grid every other number on this stage
        // is measured in — twenty on a 720p screen.
        public const float DrawerTravel = 131f;

        /// <summary>
        /// WHERE THE BAR TOP IS WITH THE CELLAR OPEN, as a fraction up the screen — the line a
        /// bench has to close onto so it lands ON the counter instead of leaving a stripe of
        /// room showing above it (2026-08-22, the author: "açılan arkaplan ui'si backbar
        /// açıkken tam olarak tezgahın üstüne kapansın").
        ///
        /// DERIVED, not typed: the counter hangs from its own rest line, the drawer lifts the
        /// whole room by its own travel, and this is those two added.
        ///
        /// IT FOLLOWS THE DRAWER rather than assuming it open (2026-08-22). The shaker and the
        /// glass are only reachable through the cellar, so for them the drawer always is — but
        /// the DRAUGHT station's door is the font standing in the room, and the cellar behind
        /// it may well be shut. A fixed line would have landed that bench 121 px above its own
        /// counter, which is the same stripe of room the other two just stopped showing.
        /// </summary>
        public float BenchSurfaceFraction =>
            (CounterRestY + CounterSurfaceInset + DrawerTravel * _drawerT) / Reference.y;
        /// <summary>
        /// HOW MUCH OF THE ROLLER IS LEFT STANDING AT THE SILL once the cellar is open
        /// (2026-08-25, the author: "back bar kapağı açıkken aşağıdan çok az kapak gözüküyor
        /// bu gözükmeyi arttırıp gözüken kısıma üst ok görseli koyalım").
        ///
        /// It used to be six units — a hairline at the bottom edge of the screen, which is
        /// not a rail, it is a seam. Sixteen is what a chevron needs to sit ON, and the
        /// number is bounded from above by the SHELF: with the drawer up, the lower board's
        /// own top row lands 15 units off the screen's foot and its bottles stand at 10, so
        /// a rail any taller than this stops reading as a bottom rail parked in front of the
        /// shelf and starts reading as a shutter that failed to open.
        /// </summary>
        private const float ShutterRail = 16f;
        /// <summary>How far the roller drops to clear the opening. The author's mock-ups
        /// put its top at screen row 305 shut and 356 open while the room rose 121, so against
        /// the room it travels its own height, near enough, less whatever the open frame is
        /// asked to leave showing at the sill. DERIVED from that rail rather than typed, so
        /// the two cannot drift: the roller's top ends up exactly <see cref="ShutterRail"/>
        /// units above the screen's foot. It goes DOWN, which is what the pink arrow drawn
        /// on it has been pointing at all along.</summary>
        private const float ShutterTravel =
            CounterRestY + CounterSurfaceInset - ShutterOpeningTopPx + DrawerTravel - ShutterRail;
        // WHERE THE WRITING SITS ON THE ROLLER, measured off the roller's own top edge and
        // not off the screen, because it rides. The shut roller hangs from the shelf
        // opening's top (57 stage units up) and the screen cuts it off at the sill, so its
        // readable face is exactly those 57 units: the word takes 21..55 of them and the
        // chevron 6..17, which leaves the writing clear of both the cut and the sill.
        private const float SignWordDrop = 19f;    // the word's centre, below the roller's top
        /// <summary>...and where the OTHER chevron sits: the one on the rail, pointing back
        /// up, which is the only mark left showing once the cellar is open. Half the rail
        /// down from the roller's top edge, so it is centred in the strip the player can
        /// actually see and aim at.</summary>
        private const float ShutMarkDrop = ShutterRail * 0.5f;
        // THE ROLLER IS HELD OPEN A CRACK UNDER THE POINTER (2026-08-25, the author: "kapağın
        // üstüne mouse ile gelindiğinde sadece kapak biraz yukarıdan aralanır ve aralanan
        // yerden ışık çıkar böylece bunun basılabilir etkileşime girilebilir bir nesne olduğu
        // belli olur"). Seven units, which is the smallest travel that opens a slit wide
        // enough to hold light at this scale and still reads as a shutter breathing rather
        // than as the drawer starting to open on its own.
        //
        // It travels the way the roller travels: DOWN. The cellar is revealed from the top
        // edge of the shelf opening, so a crack at the top is exactly what a real one lets
        // go of first, and the light behind it is the cellar's own.
        //
        // ...AND THE OPEN ROLLER BREATHES THE OTHER WAY (2026-08-25, the author: "Mouse ile
        // üstüne gelindiğinde biraz daha kapansın basılabilir olduğu anlaşılsın böylece").
        // The same seven units, spent UP: what the rail at the sill is for is shutting the
        // cellar again, so the hint it gives has to be the start of that movement and not
        // the start of the opposite one. One number, two directions, because it is one
        // gesture — the roller always leans the way the click would take it.
        private const float ShutterPeek = 7f;
        private const float PeekSeconds = 0.14f;
        private const float DrawerSeconds = 0.42f;
        private Transform _shutterTr;
        private SpriteRenderer _counterSr, _shutterSr;   // to repaint (SetCounterFinish)

        /// <summary>The counter as drawn — what the market's finish swatches are cut from.</summary>
        public Sprite CounterArt => counterSprite;

        /// <summary>THE COUNTER REPAINTED (2026-09-16, CounterFinish): the counter and the shutter swap their sprites
        /// for recoloured copies of the same drawing; the tiling and every measurement taken off it stay.</summary>
        public void SetCounterFinish(string id)
        {
            if (_counterSr != null && counterSprite != null) _counterSr.sprite = CounterFinish.Recolour(counterSprite, id);
            if (_shutterSr != null && shutterSprite != null) _shutterSr.sprite = CounterFinish.Recolour(shutterSprite, id);
        }
        private Vector2 _shutterNative;
        private float _shutterRestLocalY;

        // ── where a bottle stands in the cellar ─────────────────────────────────
        // MEASURED on the installed counter (638x241), not chosen. The blue posts scan at
        // x 7-32, 209-226, 412-429 and 605-630, which leaves three bays; the two shelf
        // boards are 12 px thick at rows 138..149 and 228..239.
        private static readonly float[] CellarBayCentrePx = { 120f, 319f, 517f };
        private const float CellarBayWidthPx = 175f;      // the narrowest of the three
        /// <summary>
        /// The line a bottle's foot stands on, in the counter art's own rows.
        ///
        /// NOT the board's TOP row (2026-08-25, the author: "rafın en üstündeki pixele temas
        /// edecek şekilde konumlandırılmışlar fakat rafın yüzeyine oturtulmaları gerekiyor …
        /// smirkoff -170'de rafta duruyor hissini veriyor"). The boards are drawn with DEPTH
        /// — twelve rows of blue apiece, which is a plank seen from slightly above and not a
        /// hairline — so a foot on row 138 is a bottle balanced on the plank's front EDGE.
        /// The surface it should stand on is the middle of that plank: rows 138..149 and
        /// 228..239, so 143 and 233. The author read the first one off the inspector as
        /// -170, and 143 is exactly what puts Smirkoff's transform there.
        /// </summary>
        private static readonly float[] CellarShelfFootPx = { 143f, 233f };
        /// <summary>The row each compartment's CEILING is drawn at — the shelf opening's own
        /// top edge, then the underside of the board above the lower run. What the cellar's
        /// lights hang from, so a re-cut counter moves them with the boards.</summary>
        private static readonly float[] CellarShelfCeilPx = { ShutterOpeningTopPx, 150f };
        /// <summary>Three bays across, on each of two boards.</summary>
        private const int CellarBays = 3;
        /// <summary>
        /// How thinly and how densely one bay may be packed.
        ///
        /// It used to be a flat three, which was what the author's open mock-up showed and
        /// what an OPENING bar looks like — and 3 × 3 × 2 is eighteen slots against a
        /// branded catalogue of thirty-six pourable bottles, every one of which JOINS the
        /// shelf (TycoonRun.BuyBrand). So a bar that kept shopping bought stock the cellar
        /// simply did not draw, which is half of "satın alınan alkoller eklenmiyor".
        ///
        /// The bay takes as many slots as tonight's stock needs and no more: six bottles
        /// stand spread out, thirty stand shoulder to shoulder (2026-08-25, the author:
        /// "birbirlerine yakın olabilirler"), and nothing is ever dropped.
        /// </summary>
        private const int CellarMaxPerBay = 8;
        /// <summary>How many bottles the cellar can show at once. An upper bound, not a
        /// promise: what a bay actually holds is decided by the WIDTH of tonight's stock
        /// (see <see cref="PackCellar"/>), and the narrowest bottle in the catalogue is
        /// half the width of the broadest. Eight a bay clears the thirty-six pourable
        /// brands the shop sells with room to spare.</summary>
        public const int CellarSlots = CellarBays * 2 * CellarMaxPerBay;
        /// <summary>Drawn height of a bottle in the cellar. The shallower compartment runs
        /// from the opening's top row (65) to the near board's surface (143), so 62 leaves
        /// the stock a clear sixteen rows of air under the board above it.
        ///
        /// IT IS A CONSTANT AGAIN, and that is the point (2026-08-25, the author: "raftaki
        /// alkolleri sığdırmak için boyutları değişmemeli gerekirse aralarında 1 pixel
        /// kalıcak kadar yakınlaşsınlar ama boyutları değişmesin"). It used to be a field
        /// the shelf shrank until the widest bottle fitted its slot — which meant buying a
        /// broad-shouldered rum made every OTHER bottle in the bar quietly smaller. A shelf
        /// is not a thumbnail grid: the bottles are the size they are, and what gives when
        /// the stock grows is the AIR BETWEEN THEM, down to a single pixel and no further.
        /// </summary>
        // 64 (2026-09-04, PLAN_bottle_art_v4 §3): the v4 cellar copy is 32x64 art drawn at one
        // art pixel per stage unit, so 64 tall is the drawing at 1:1 and 2x on a 720p screen.
        // Was 62; the compartment's air above the bottle goes from 16 rows to 14.
        private const float CellarBottleH = 64f;
        /// <summary>The least air allowed between two shoulders, in art px. One, because
        /// that is what the author asked for; not zero, because touching bottles read as
        /// one smear and the hit plates behind them would share an edge.</summary>
        private const float CellarMinGapPx = 1f;
        /// <summary>Where each drawn bottle stands and how wide it is, in the counter art's
        /// own pixels — filled by <see cref="PackCellar"/> and read by everything else, so
        /// the bottle and the plate that catches its click cannot disagree. Index is the
        /// slot's, and the list is SHORTER than the stock handed in when the shelves run
        /// out of room.</summary>
        private readonly List<float> _cellarSlotX = new List<float>();
        private readonly List<float> _cellarSlotW = new List<float>();
        private readonly List<float> _cellarSlotFoot = new List<float>();
        private readonly List<SpriteRenderer> _cellarStock = new List<SpriteRenderer>();
        // THE v4 SANDWICH IN THE CELLAR (2026-09-04, PLAN_bottle_art_v4 §4c). Per slot: the
        // interior plate behind the front, and between them a flat-colour quad clipped by a
        // SpriteMask carrying the drink's cavity — so the level is a real level, cut by the
        // glass, lit by the room like everything else on the counter. The quad is a 1x1 white
        // sprite scaled to the cavity: flat colour, so scaling distorts nothing. A mask only
        // reaches sprites that overlap it, so thirty-six masks on one order never cross.
        private readonly List<SpriteRenderer> _cellarBack = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _cellarDrink = new List<SpriteRenderer>();
        private readonly List<SpriteMask> _cellarMask = new List<SpriteMask>();
        // THE DRINK IS ROUND (2026-09-14): the oval face on top of each bottle's drink and the arched foot under it, both
        // inside the bottle's mask. The face shares the drink's sorting order and stands a hair nearer the camera, which
        // is what draws it over the drink (Renderer2D sorts a tie by depth); the order above is the bottle's front.
        private readonly List<SpriteRenderer> _cellarFace = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _cellarFoot = new List<SpriteRenderer>();
        // THE CELLAR'S SHADOWS (2026-09-16, the author: "Mahzen sahnesine şişelere göre gölgelendirme ve rafların
        // tepesinde loş spot ışığı etkisi verelim"): behind every bottle its own silhouette, black and faint, a few
        // pixels right and down — the spot at the bay's ceiling throws it — and under its foot a soft ellipse.
        private readonly List<SpriteRenderer> _cellarShadow = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _cellarFootShadow = new List<SpriteRenderer>();
        private readonly List<Rect> _cellarCavity = new List<Rect>();   // opaque bbox of the mask, in art px
        private Sprite _whitePx;
        private RectTransform _cellarDoorRoot;
        private CanvasGroup _cellarDoorGroup;
        private readonly List<RectTransform> _cellarDoors = new List<RectTransform>();
        private System.Action<int> _onCellarPick;
        private System.Action<int, RectTransform> _onCellarHover;   // (-1, plate) on leaving
        private RectTransform _shutterDoor;
        private RectTransform _cellarOpenSign;
        private CanvasGroup _cellarOpenGroup;
        private RectTransform _cellarShutSign;   // the rail's own chevron, pointing back up
        private CanvasGroup _cellarShutGroup;
        private RectTransform _shutterRailDoor;  // ...and the strip of roller it is drawn on
        private RectTransform _cellarCatcher;    // click anywhere off the shelves to shut it
        private RectTransform _shelfGuard;       // ...except here: the shelves keep their clicks
        private bool _shutterHovered;
        private float _shutterPeek;              // 0 shut tight, 1 held open a crack
        private Image _shutterLight;             // what spills out of that crack
        private IReadOnlyList<string> _cellarIds;

        /// <summary>
        /// Each door carries its bottle's id in its NAME. It costs nothing and it buys two
        /// things: a PlayMode test can ask for the vodka rather than for "slot 0", and a
        /// hierarchy full of CellarDoor0..17 stops being a puzzle the moment something is in
        /// the wrong bay. The same trick the fixtures use ("Fx_" + def.Id).
        /// </summary>
        private void NameCellarDoors(int n)
        {
            for (int i = 0; i < n && i < _cellarDoors.Count; i++)
            {
                string id = _cellarIds != null && i < _cellarIds.Count ? _cellarIds[i] : null;
                string want = string.IsNullOrEmpty(id) ? "CellarDoor" + i : "CellarDoor_" + id;
                if (_cellarDoors[i].name != want) _cellarDoors[i].name = want;
            }
        }

        /// <summary>Who to tell when a bottle in the cellar is picked, by its index in the
        /// list <see cref="SetCellar"/> was given.</summary>
        public void SetCellarHandler(System.Action<int> onPick) => _onCellarPick = onPick;

        /// <summary>The pointer arrived on (index, plate) or left it (-1, plate): the HUD
        /// raises the bottle's card (2026-09-06).</summary>
        public void SetCellarHoverHandler(System.Action<int, RectTransform> onHover) => _onCellarHover = onHover;

        // ── the snack mat's span (2026-09-06) ─────────────────────────────────────
        // THE MAT GROWS WITH THE RAIL (the author: "çerez matı mevcut garnishlerin tamamını
        // ortalamış bir şekilde kapsamıyor. Yeni garnish geldiğinde de ona göre boyutu sağ
        // ve sola doğru uzamalı"). The mat is a fixture of the room, but the dishes are the
        // HUD's, so the HUD tells the room where the rail stands — in its own units — and
        // the mat is tiled to that span and re-centred on it. The fixture's slot keeps only
        // the height it stands at.
        private float _prepMatCentreX = float.NaN, _prepMatWidth = 0f;

        /// <summary>The rail's centre and width in HUD units (the 1280-wide canvas, centred),
        /// and how many dishes are standing on it.</summary>
        public void SetPrepMatSpan(float centreHudX, float widthHud, int dishes = 0)
        {
            _prepMatCentreX = Reference.x * 0.5f + centreHudX * 0.5f;
            _prepMatWidth = Mathf.Max(8f, widthHud * 0.5f);
            // THE AUTHOR DREW ONE PER COUNT (2026-09-09: "garnishlerin altına uygun 4, 5, 6
            // garnishe göre olan paspas görsellerini verdim") — 143, 180 and 217 art px, the
            // exact widths the rail asks for at four, five and six dishes. A drawn mat is
            // used at its OWN size and never tiled; anything else still stretches the one
            // the room was given.
            _prepMatArt = dishes >= 4 && dishes <= 6
                ? Resources.Load<Sprite>("Fixtures/fx_prep_mat_" + dishes) : null;
            foreach (var placed in _placedFixtures)
            {
                if (placed.Def == null || placed.Def.Id != "prep_mat" || placed.Body == null) continue;
                var sr = placed.Body.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                if (_prepMatArt != null)
                {
                    sr.sprite = _prepMatArt;
                    sr.drawMode = SpriteDrawMode.Simple;
                }
                else if (sr.sprite != null)
                {
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.tileMode = SpriteTileMode.Continuous;
                    sr.size = new Vector2(_prepMatWidth, sr.sprite.rect.height / sr.sprite.pixelsPerUnit);
                }
            }
            PlaceFixtures();
        }

        private Sprite _prepMatArt;    // the drawn mat for this count, or null for the tiled one
        private float _drawerT;                     // 0 shut, 1 open
        private float _drawerTarget;

        /// <summary>Is the cellar open, or on its way there?</summary>
        public bool DrawerOpen => _drawerTarget > 0.5f;
        /// <summary>0 shut, 1 open — for anything that has to fade with the drawer.</summary>
        public float DrawerPhase => _drawerT;

        /// <summary>
        /// Opens or shuts the counter's cellar. The whole room rides up: the author's two
        /// mock-ups differ by exactly <see cref="DrawerTravel"/> in every landmark, patrons
        /// included, so this moves the world root rather than the counter alone.
        /// </summary>
        /// <param name="amount">How far open, 0..1 of <see cref="DrawerTravel"/> (2026-09-14): a bench
        /// stands the room a little short of the top so the heads have air over them.</param>
        public void SetDrawerOpen(bool open, bool instant = false, float amount = 1f)
        {
            float wanted = open ? Mathf.Clamp01(amount) : 0f;
            // Only when it actually OPENS OR SHUTS. Callers set the same state freely (SERVE IT
            // shuts a cellar that may already be shut), and a roller that speaks every time it is
            // asked to stay put — or to settle a little lower behind a bench — is a roller that rattles.
            if ((wanted > 0.5f) != (_drawerTarget > 0.5f) && !instant)
                Sfx.Play(open ? "cellar_open" : "cellar_close", 0.75f);
            _drawerTarget = wanted;
            if (instant || Motion.Reduced) { _drawerT = _drawerTarget; ApplyDrawer(); }
        }

        /// <summary>
        /// What is standing in the counter's cellar. The stage is TOLD, the same way the till
        /// is told the money — it never reads the run. Anything past <see cref="CellarSlots"/>
        /// is not drawn, because there is no shelf for it to stand on.
        /// </summary>
        public void SetCellar(IReadOnlyList<Sprite> bottles, IReadOnlyList<string> ids = null)
        {
            int n = bottles == null ? 0 : Mathf.Min(bottles.Count, CellarSlots);
            _cellarIds = ids;
            // The pack decides how many the shelves can actually hold at full size, which
            // is never MORE than what was handed in and can be less.
            PackCellar(bottles, n);
            n = _cellarSlotX.Count;
            while (_cellarStock.Count < n)
                // 32, not 31 (2026-09-04): the v4 sandwich puts the interior at 30 and the drink
                // at 31 UNDER this front; sharing 31 let the drink draw over the label.
                _cellarStock.Add(WorldSprite("Stock" + _cellarStock.Count, null, order: 32));
            for (int i = 0; i < _cellarStock.Count; i++)
            {
                var sr = _cellarStock[i];
                bool on = i < n && bottles[i] != null;
                if (sr.gameObject.activeSelf != on) sr.gameObject.SetActive(on);
                // A slot that goes dark takes its whole sandwich with it; a slot that lights
                // up gets its sandwich from SetCellarPlates, never a previous card's
                // (2026-09-04 audit: the mask stayed as it was, so a re-lit slot could show
                // the last card's interior until the plates were set).
                if (i < _cellarBack.Count && !on)
                {
                    _cellarBack[i].gameObject.SetActive(false);
                    _cellarDrink[i].gameObject.SetActive(false);
                    if (i < _cellarShadow.Count) { _cellarShadow[i].gameObject.SetActive(false); _cellarFootShadow[i].gameObject.SetActive(false); }
                    _cellarFace[i].gameObject.SetActive(false);
                    _cellarFoot[i].gameObject.SetActive(false);
                    _cellarMask[i].gameObject.SetActive(false);
                }
                if (!on) continue;
                sr.sprite = bottles[i];
                PlaceCellarSlot(sr, i);
            }
            BuildCellarDoors(n);
            NameCellarDoors(n);
            LayOutCellarDoors();
        }

        /// <summary>
        /// Where tonight's stock stands, decided ONCE before anything is placed.
        ///
        /// THE BOTTLES DO NOT CHANGE SIZE (2026-08-25, the author). Every one of them is
        /// drawn at <see cref="CellarBottleH"/>, always, whatever else is on the shelf.
        /// What absorbs a growing bar is the SPACING: six bottles stand spread across the
        /// six compartments, thirty stand shoulder to shoulder with a single pixel of air
        /// between them, and nothing in between is scaled to make it fit.
        ///
        /// So a slot is no longer a fixed share of a bay — it is a bottle's own drawn
        /// width, and the bay is packed with the real widths. That is not a detail: the
        /// catalogue's broadest bottle is nearly twice the width of its narrowest, so
        /// equal slots spend a fat bottle's room on a thin one and then shrink the whole
        /// shelf to pay for it.
        ///
        /// The compartments are filled the way a bar restocks — shelf by shelf, bay by bay
        /// — and each takes an even share of what is LEFT, so the stock spreads instead of
        /// piling into the first bay. A compartment that would overrun its bay at the
        /// minimum gap stops early and hands the rest forward; when the last one is full,
        /// what remains has nowhere to stand and is not drawn, which is the same rule
        /// <see cref="CellarSlots"/> has always kept.
        /// </summary>
        private void PackCellar(IReadOnlyList<Sprite> bottles, int n)
        {
            _cellarSlotX.Clear();
            _cellarSlotW.Clear();
            _cellarSlotFoot.Clear();
            if (bottles == null || n <= 0) return;

            int compartments = CellarShelfFootPx.Length * CellarBays;
            int taken = 0;
            for (int c = 0; c < compartments && taken < n; c++)
            {
                int left = compartments - c;
                int want = Mathf.Min(Mathf.CeilToInt((n - taken) / (float)left), CellarMaxPerBay);

                // How many of the next `want` actually fit this bay at the tightest legal
                // packing. Measured off the sprites themselves — the first bottle costs its
                // own width, every one after it costs a gap as well.
                int take = 0;
                float sumW = 0f;
                while (take < want && taken + take < n)
                {
                    float w = CellarDrawnWidth(bottles[taken + take]);
                    float needed = sumW + w + take * CellarMinGapPx;
                    if (take > 0 && needed > CellarBayWidthPx) break;
                    sumW += w;
                    take++;
                }
                if (take == 0) continue;

                // The air left over, shared out evenly — a full bay collapses to the
                // one-pixel minimum and a sparse one stands its bottles apart. The RUN is
                // then centred in the bay rather than laid out from its left edge, so the
                // packing is symmetric at every density and cannot creep past a post.
                float gap = take > 1
                    ? Mathf.Max(CellarMinGapPx, (CellarBayWidthPx - sumW) / (take + 1))
                    : 0f;
                float run = sumW + (take - 1) * gap;
                float x = CellarBayCentrePx[c % CellarBays] - run * 0.5f;
                float foot = CellarShelfFootPx[Mathf.Min(c / CellarBays,
                    CellarShelfFootPx.Length - 1)];
                for (int k = 0; k < take; k++)
                {
                    float w = CellarDrawnWidth(bottles[taken + k]);
                    _cellarSlotX.Add(x + w * 0.5f);
                    _cellarSlotW.Add(w);
                    _cellarSlotFoot.Add(foot);
                    x += w + gap;
                }
                taken += take;
            }
        }

        /// <summary>How wide a bottle is DRAWN, in the counter art's own pixels: its own
        /// aspect at the one shelf height. A missing sprite is given the catalogue's broadest
        /// shoulder so a hole in the stock cannot pack the shelf tighter than it will be.
        /// </summary>
        /// <summary>
        /// The v4 plates and the drink levels for the cellar, in the SAME order as the sprites
        /// given to <see cref="SetCellar"/>. Slots without plates (pre-v4 cards) draw the flat
        /// front only. Told by the HUD; the stage never reads the run.
        /// </summary>
        public void SetCellarPlates(IReadOnlyList<ItemArt.BottlePlates> plates, IReadOnlyList<float> fills)
        {
            while (_cellarBack.Count < _cellarStock.Count)
            {
                int i = _cellarBack.Count;
                var back = WorldSprite("StockBack" + i, null, order: 30);
                var maskGo = new GameObject("StockMask" + i);
                maskGo.transform.SetParent(_world, false);
                var mask = maskGo.AddComponent<SpriteMask>();
                mask.isCustomRangeActive = true;
                mask.frontSortingLayerID = mask.backSortingLayerID = back.sortingLayerID;
                mask.frontSortingOrder = 31; mask.backSortingOrder = 31;
                var drink = WorldSprite("StockDrink" + i, ChromeArt.LiquidBody(), order: 31);   // shaded like a cylinder (2026-09-16)
                // ...and NO CHECKER on the shelf (2026-09-17, the author: "Mahzendeki sıvıların içerisinde sıvılar
                // kare kare gözüküyor png gibi bunu kaldır"): at 20 px wide the pouring liquid's two-unit cells read
                // as a tiled picture rather than a texture. The hand bottle and the gauge keep theirs.
                drink.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                var face = WorldSprite("StockFace" + i, GlassArt.SurfaceDisc(), order: 31);
                face.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                var foot = WorldSprite("StockFoot" + i, GlassArt.SurfaceDisc(), order: 31);
                foot.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                _cellarBack.Add(back); _cellarMask.Add(mask); _cellarDrink.Add(drink);
                _cellarFace.Add(face); _cellarFoot.Add(foot);
                _cellarCavity.Add(Rect.zero);
            }
            for (int i = 0; i < _cellarStock.Count; i++)
            {
                var p = plates != null && i < plates.Count ? plates[i] : null;
                bool on = p != null && p.Mask != null && _cellarStock[i].gameObject.activeSelf;
                _cellarBack[i].gameObject.SetActive(on);
                _cellarDrink[i].gameObject.SetActive(on);
                _cellarFace[i].gameObject.SetActive(on);
                _cellarFoot[i].gameObject.SetActive(on);
                _cellarMask[i].gameObject.SetActive(on);
                if (!on) continue;
                _cellarBack[i].sprite = p.Back;
                _cellarMask[i].sprite = p.Mask;
                _cellarCavity[i] = ItemArt.OpaqueBounds(p.Mask);
                PlaceCellarSlot(_cellarStock[i], i);
            }
            RefreshCellarMovers();
            SetCellarFills(fills);
        }

        /// <summary>The glow's followers, wired AFTER the plates exist (2026-09-06, the author:
        /// "şişelerin üstüne gelindiğinde sağa sola sallanması gerekiyken içerisindeki doluluğu
        /// gösteren sıvılar sabit kalıyor"). The doors are built before the back, mask and drink
        /// renderers, so a door built then followed only the bottle — and the drink stood still
        /// in the air while the bottle rocked. Rewired every time the plates are set.</summary>
        /// <summary>Switches one cellar bottle's renderers off or on (2026-09-08): while
        /// the bottle's card stands, the HUD draws the bottle itself — unlit, bright, on
        /// top of the card — and the shelf's one must not show through beside it. The
        /// transforms keep moving (HoverGlow's sway), so the copy can follow them.</summary>
        public void ShowCellarBottle(int index, bool shown)
        {
            if (index < 0 || index >= _cellarStock.Count) return;
            var glow = CellarGlow(index);
            if (glow != null) glow.HaloHidden = !shown;   // the card draws the halo with the copy
            _cellarStock[index].enabled = shown;
            if (index < _cellarShadow.Count) { _cellarShadow[index].enabled = shown; _cellarFootShadow[index].enabled = shown; }
            if (index < _cellarBack.Count) _cellarBack[index].enabled = shown;
            if (index < _cellarDrink.Count) _cellarDrink[index].enabled = shown && _cellarDrinkOn(index);
            if (index < _cellarFace.Count)
            {
                // The face and the foot follow the drink, and only where the fills last drew them.
                _cellarFace[index].enabled = shown && _cellarDrink[index].enabled && _cellarFaceWidth(index) > 0f;
                _cellarFoot[index].enabled = shown && _cellarDrink[index].enabled && _cellarFoot[index].transform.localScale.y > 0f;
            }
        }

        private bool _cellarDrinkOn(int index) =>
            index < _cellarDrink.Count && _cellarDrink[index].gameObject.activeSelf
            && index < _cellarMask.Count && _cellarMask[index].sprite != null;

        /// <summary>The hover glow on one cellar door — the sway, the rise, the halo — for
        /// the card to draw the same light behind its copy of the bottle.</summary>
        public HoverGlow CellarGlow(int index) =>
            index >= 0 && index < _cellarDoors.Count ? _cellarDoors[index].GetComponent<HoverGlow>() : null;

        /// <summary>How one cellar bottle STANDS this frame: the world centre of its front
        /// plate, that plate's size WITHOUT the rock (world units) and the angle it is rocked
        /// by. The card copies the pose rather than the bounding box (2026-09-09): a box
        /// around a rocking bottle swells and shrinks with the angle, so a copy laid on it
        /// pumped, and the drink measured inside that copy could never keep up. False when
        /// there is no such bottle.</summary>
        public bool CellarBottlePose(int index, out Vector3 centre, out Vector2 size, out float angleDeg)
        {
            centre = Vector3.zero; size = Vector2.zero; angleDeg = 0f;
            if (index < 0 || index >= _cellarStock.Count) return false;
            var sr = _cellarStock[index];
            if (sr == null || sr.sprite == null) return false;
            var t = sr.transform;
            var b = sr.sprite.bounds;
            centre = t.TransformPoint(b.center);
            var ls = t.lossyScale;
            size = new Vector2(b.size.x * Mathf.Abs(ls.x), b.size.y * Mathf.Abs(ls.y));
            angleDeg = t.eulerAngles.z;
            return true;
        }

        private void RefreshCellarMovers()
        {
            for (int i = 0; i < _cellarDoors.Count && i < _cellarStock.Count; i++)
            {
                var glow = _cellarDoors[i].GetComponent<HoverGlow>();
                if (glow == null) continue;
                var movers = new List<Transform> { _cellarStock[i].transform };
                if (i < _cellarBack.Count) movers.Add(_cellarBack[i].transform);
                if (i < _cellarDrink.Count) movers.Add(_cellarDrink[i].transform);
                if (i < _cellarFace.Count) { movers.Add(_cellarFace[i].transform); movers.Add(_cellarFoot[i].transform); }
                if (i < _cellarShadow.Count) movers.Add(_cellarShadow[i].transform);   // the cast shadow rocks with it; the foot's stays
                if (i < _cellarMask.Count) movers.Add(_cellarMask[i].transform);
                glow.Movers = movers.ToArray();
            }
        }

        /// <summary>The drink levels only — cheap enough to call whenever stock moves.</summary>
        public void SetCellarFills(IReadOnlyList<float> fills)
        {
            for (int i = 0; i < _cellarDrink.Count && i < _cellarStock.Count; i++)
            {
                var d = _cellarDrink[i];
                if (!d.gameObject.activeSelf) continue;
                float f = fills != null && i < fills.Count ? Mathf.Clamp01(fills[i]) : 0f;
                var cav = _cellarCavity[i];
                var sp = _cellarMask[i].sprite;
                if (sp == null || cav.width <= 0f || f <= 0f) { d.enabled = false; RoundOff(i); continue; }
                d.enabled = true;
                d.color = i < _cellarTones.Count ? _cellarTones[i] : Color.white;
                // Art pixel -> world: the mask sprite's PPU, times the slot's scale. The cavity
                // rect is in art pixels from the sprite's bottom-left; the sprite is drawn
                // centred (pivot 0.5, 0.5), so offsets are from its centre.
                float ppu = sp.pixelsPerUnit;
                float k = _cellarMask[i].transform.localScale.x;
                float unit = k / ppu;
                // FULL IS THE SHOULDER, AS A VOLUME — the same table the hand reads
                // (BottleArt.Upright), so the cellar and the bench show one level for one
                // bottle. A plain height fraction over the whole mask drew a full bottle with
                // a neck full of drink, 5–11 rows above the hand's level (2026-09-04 audit).
                float rows = BottleArt.Upright(sp).RowsFor(f);    // whole art rows, from the cavity's foot
                if (rows < 1f) { d.enabled = false; RoundOff(i); continue; }
                float w = cav.width * unit, hgt = rows * unit;
                float cx = (cav.x + cav.width * 0.5f - sp.rect.width * 0.5f) * unit;
                float footY = (cav.y - sp.rect.height * 0.5f) * unit;   // the cavity's lowest row, from the sprite's centre
                // ROUND, NOT FLAT (2026-09-14, the author: "şişelerin içerisindeki sıvı da 3 boyutlu olmalı altı ve üstü ...
                // dairesel hissini vermeli"): the face is an oval as wide as the cavity is on the level's row, squashed as
                // the glasses' are; the foot is the near half of an oval as wide as the cavity just over its lowest row —
                // so the drink's quad starts that half-height up, and the foot's disc rounds it down to the middle.
                var table = BottleArt.Upright(sp);
                float faceW = 0f, footW = 0f;
                if (table.Width != null && table.Width.Length > 0)
                {
                    faceW = table.Width[Mathf.Clamp(table.MinY + Mathf.RoundToInt(rows) - 1, 0, table.Width.Length - 1)] * unit;
                    footW = table.Width[Mathf.Clamp(table.MinY + 2, 0, table.Width.Length - 1)] * unit;
                }
                float rise = footW * GlassArt.SurfaceSquash * 0.5f;
                if (rise * 2f >= hgt) rise = 0f;
                // The drink reaches the foot; the arc is a shade over it (2026-09-16, with BottleArt.SetLevel:
                // the base's corners stood empty under a full bottle while the quad began `rise` up).
                // the body at its own size in pixels, so its two-pixel checker stays two pixels (2026-09-17)
                d.sprite = ChromeArt.LiquidBody(Mathf.Max(2, Mathf.RoundToInt(w)), Mathf.Max(2, Mathf.RoundToInt(hgt)), checker: false);
                var body = d.sprite != null ? d.sprite.bounds.size : Vector3.one;
                d.transform.localScale = new Vector3(w / Mathf.Max(0.001f, body.x), hgt / Mathf.Max(0.001f, body.y), 1f);
                // Both hang under _world: place in the parent's frame, so a scaled stage (a
                // wide monitor, DesignFrame.SceneScale > 1) cannot push the level up.
                var at = _cellarMask[i].transform.localPosition;
                d.transform.localPosition = at + new Vector3(cx, footY + hgt * 0.5f, 0f);
                PlaceRoundDrink(i, at, cx, footY, hgt, faceW, footW, rise, d.color);
            }
        }

        /// <summary>The drink colours per slot, same order as the plates.</summary>
        public void SetCellarTones(IReadOnlyList<Color> tones)
        {
            _cellarTones.Clear();
            if (tones != null) _cellarTones.AddRange(tones);
        }

        private readonly List<Color> _cellarTones = new List<Color>();

        /// <summary>The oval face and the arched foot of cellar bottle <paramref name="i"/>'s drink (see SetCellarFills).</summary>
        private void PlaceRoundDrink(int i, Vector3 at, float cx, float footY, float hgt, float faceW, float footW, float rise, Color tone)
        {
            if (i >= _cellarFace.Count) return;
            var face = _cellarFace[i];
            var low = _cellarFoot[i];
            var size = face.sprite != null ? face.sprite.bounds.size : Vector3.one;
            float faceH = Mathf.Max(0.001f, faceW * GlassArt.SurfaceSquash);
            face.color = new Color(Mathf.Min(1f, tone.r * 1.18f + 0.07f), Mathf.Min(1f, tone.g * 1.18f + 0.07f),
                                   Mathf.Min(1f, tone.b * 1.18f + 0.07f), tone.a);   // the hand bottle's face tone
            face.transform.localScale = new Vector3(faceW / size.x, faceH / size.y, 1f);
            face.transform.localPosition = at + new Vector3(cx, footY + hgt, -0.01f);   // a hair nearer: over the drink
            face.enabled = faceW > 0f;
            low.color = new Color(tone.r * 0.88f, tone.g * 0.88f, tone.b * 0.88f, tone.a * 0.55f);   // the base seen through the drink, faint
            low.transform.localScale = new Vector3(rise > 0f ? footW / size.x : 0f, rise * 2f / size.y, 1f);
            low.transform.localPosition = at + new Vector3(cx, footY + rise, -0.01f);      // a hair nearer: over the drink
            low.enabled = rise > 0f && footW > 0f;
        }

        private void RoundOff(int i)
        {
            if (i >= _cellarFace.Count) return;
            _cellarFace[i].enabled = false;
            _cellarFoot[i].enabled = false;
        }

        private float _cellarFaceWidth(int i) => i < _cellarFace.Count ? _cellarFace[i].transform.localScale.x : 0f;

        private Sprite WhitePixel()
        {
            if (_whitePx != null) return _whitePx;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white); tex.Apply();
            // PPU 1: one unit per pixel, so localScale IS the size in stage units.
            _whitePx = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _whitePx;
        }

        // (The opaque-box reading moved to ItemArt.OpaqueBounds on 2026-09-04, when the
        //  counter's garnish rail needed the same answer to stand its dishes on one line.
        //  It was measured and cached here; it is measured and cached there, once, for
        //  everybody — two readings of one texture is how two props on one bar end up on
        //  two lines.)

        // ── the sink's water (GDD 27 §4.3, 2026-09-05) ──────────────────────────
        //
        // The tap runs for as long as Core says the sink is busy (WashSecondsFor: a second
        // and a half plus half a second a glass), and the HUD tells the stage each frame.
        // The stream is a frame sheet cut by the fixture's own cell (fixtures.json), laid
        // over the basin as a child sprite with the basin's pivot, so it lands on the sink
        // whichever sink is standing in the slot. Nothing is drawn while the tap is off.

        private SpriteRenderer _waterSr;
        private Sprite[] _waterFrames;
        private bool _waterOn;
        private int _waterFrame;
        /// <summary>THE ROOM STOPS WITH THE BOOK (2026-09-08, the author: "menü açıldığında
        /// oyun durduğu için arka plan animasyonları da durmalı"). The window's wind, the
        /// tap's water and the lights' flicker ran on unscaled time, so they carried on
        /// while the night stood still. The HUD hands the night's clock scale here every
        /// frame; the ambient timers accumulate through it instead of reading the wall.</summary>
        public float AmbientScale = 1f;
        private float _ambientClock;

        private float _waterClock;
        private const float WaterFrameStep = 1f / 10f;

        private void BuildWater(LastCall.Core.FixtureDefinition def, SpriteRenderer basin)
        {
            _waterSr = null;
            _waterFrames = null;
            if (def == null || basin == null || basin.sprite == null) return;
            if (string.IsNullOrEmpty(def.Water) || def.CellW <= 0 || def.CellH <= 0) return;
            var sheet = Resources.Load<Texture2D>("Fixtures/" + def.Water);
            if (sheet == null)
            {
                Debug.LogWarning($"DiegeticStage: {def.Id} names water '{def.Water}' and there is no such sheet.");
                return;
            }
            int n = sheet.width / def.CellW;
            if (n < 1 || sheet.height < def.CellH) return;
            // The basin's own pivot, so frame (0,0) of the water is the basin's (0,0).
            var pivot = new Vector2(basin.sprite.pivot.x / basin.sprite.rect.width,
                                    basin.sprite.pivot.y / basin.sprite.rect.height);
            _waterFrames = new Sprite[n];
            for (int i = 0; i < n; i++)
                _waterFrames[i] = Sprite.Create(sheet,
                    new Rect(i * def.CellW, sheet.height - def.CellH, def.CellW, def.CellH),
                    pivot, basin.sprite.pixelsPerUnit);
            var go = new GameObject("Fx_" + def.Id + "_water");
            go.transform.SetParent(basin.transform, false);
            _waterSr = go.AddComponent<SpriteRenderer>();
            _waterSr.sortingLayerName = basin.sortingLayerName;
            _waterSr.sortingOrder = basin.sortingOrder + 1;
            if (_litMaterial != null) _waterSr.sharedMaterial = _litMaterial;
            _waterSr.enabled = false;
        }

        /// <summary>Told each frame whether the tap is running. Reduced motion holds the
        /// first frame as a still rather than flickering.</summary>
        public void SetTapRunning(bool on) => _waterOn = on;

        private void StepWater()
        {
            if (_waterSr == null || _waterFrames == null || _waterFrames.Length == 0) return;
            if (!_waterOn)
            {
                if (_waterSr.enabled) _waterSr.enabled = false;
                return;
            }
            if (!_waterSr.enabled)
            {
                _waterSr.enabled = true;
                _waterFrame = 0;
                _waterClock = 0f;
                _waterSr.sprite = _waterFrames[0];
            }
            if (Motion.Reduced) return;
            _waterClock += Time.unscaledDeltaTime * AmbientScale;
            while (_waterClock >= WaterFrameStep)
            {
                _waterClock -= WaterFrameStep;
                _waterFrame = (_waterFrame + 1) % _waterFrames.Length;
            }
            _waterSr.sprite = _waterFrames[_waterFrame];
        }

        private static float CellarDrawnWidth(Sprite s)
        {
            if (s == null || s.rect.height <= 0.0001f) return CellarBottleH * 0.5f;
            // THE BOTTLE, NOT ITS CANVAS (2026-09-04 audit): every v4 cellar copy is a 32×64
            // canvas around a 16–32 px bottle, so measuring the canvas packed five a bay and
            // left six of the thirty-six pourable brands undrawn. What stands on the shelf,
            // and what the door should cover, is the opaque width.
            var ob = ItemArt.OpaqueBounds(s);
            float artW = ob.width > 0f ? ob.width : s.rect.width;
            return CellarBottleH * (artW / s.rect.height);
        }

        /// <summary>How far the drawing's centre sits from its canvas centre, in art px —
        /// added when the sprite is placed so the BOTTLE, not the canvas, stands on the slot.</summary>
        private static float CellarCentreShift(Sprite s)
        {
            if (s == null) return 0f;
            var ob = ItemArt.OpaqueBounds(s);
            if (ob.width <= 0f) return 0f;
            return s.rect.width * 0.5f - (ob.x + ob.width * 0.5f);
        }

        /// <summary>
        /// One invisible hit plate per bottle, over the room — the SAME door the draught font
        /// keeps (BuildTapDoor), for the same reason: a world sprite cannot take a click, and
        /// a plate that swallows at the edges is a door the player learns not to trust.
        /// </summary>
        private void BuildCellarDoors(int n)
        {
            if (n > 0 && _cellarDoorRoot == null)
            {
                _cellarDoorRoot = OverlayCanvas("CellarDoors", 7, raycasts: true);
                _cellarDoorGroup = _cellarDoorRoot.gameObject.AddComponent<CanvasGroup>();
                UiAuditExempt.Mark(_cellarDoorRoot, "the cellar doors are hit plates over the "
                    + "stock standing in the counter, sized to each bottle's own slot");
            }
            BuildCellarCatcher();
            while (_cellarDoors.Count < n)
            {
                int index = _cellarDoors.Count;
                var plate = NewRect("CellarDoor" + index, _cellarDoorRoot);   // renamed once fed
                plate.anchorMin = plate.anchorMax = new Vector2(0, 0);
                plate.pivot = new Vector2(0.5f, 0);
                var hit = plate.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var btn = plate.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                // THE CELLAR STAYS OPEN BEHIND YOU (2026-08-22, the author: "alkol yapma
                // sahnesinde arkada zaten backbar açık olacak"). It shut for one afternoon,
                // which meant every second bottle cost a full open-and-close of the room —
                // and a bar with the cellar shut behind the tin is a bar you left. The bench
                // slides in over it instead, and slides off it again.
                btn.onClick.AddListener(() => _onCellarPick?.Invoke(index));
                // The bottle behind the plate is the affordance (2026-08-25). The plate is a
                // transparent rectangle: there is nothing on it to light, and drawing one
                // would put a box on the shelf. The stock renderer at this index is stable
                // for the life of the stage, so it can be wired once here.
                if (index < _cellarStock.Count)
                {
                    var glow = plate.gameObject.AddComponent<HoverGlow>();
                    glow.Sprites = new[] { _cellarStock[index] };
                    // THE WHOLE SANDWICH MOVES AS ONE (2026-09-06, the author: "bunların
                    // aynısı mahzendeki alkoller için de olmalı"). A bottle down here is
                    // four objects standing in one place — the front plate, the back, the
                    // drink inside it and the mask that cuts the drink to the glass — each
                    // placed by PlaceCellarSlot at the same spot. Handing all four to the
                    // glow is what lets it rise, rock and grow without coming apart, and
                    // the sorting lift brings them in front of the shelf they stand on.
                    var movers = new System.Collections.Generic.List<Transform> { _cellarStock[index].transform };
                    if (index < _cellarBack.Count) movers.Add(_cellarBack[index].transform);
                    if (index < _cellarDrink.Count) movers.Add(_cellarDrink[index].transform);
                    if (index < _cellarMask.Count) movers.Add(_cellarMask[index].transform);
                    glow.Movers = movers.ToArray();
                    glow.Riser = _cellarStock[index].transform;
                    // The room's own units: one is two of the HUD's, so these are half the
                    // counter's numbers and read the same on the screen.
                    glow.Rise = 2f; glow.Sway = 2.2f; glow.Grow = 1.06f; glow.Halo = 1.1f;
                }
                var relay = plate.gameObject.AddComponent<HoverRelay>();
                relay.Entered = () => _onCellarHover?.Invoke(index, plate);
                relay.Exited = () => _onCellarHover?.Invoke(-1, plate);
                _cellarDoors.Add(plate);
            }
            for (int i = 0; i < _cellarDoors.Count; i++)
                if (_cellarDoors[i].gameObject.activeSelf != (i < n))
                    _cellarDoors[i].gameObject.SetActive(i < n);
            BuildShutterRail();   // ...and it goes back to the front of the glass
        }

        /// <summary>
        /// Puts the doors where the bottles are. They ride the drawer the long way round: the
        /// stock is in the world and moves with it, the plates are on a fixed overlay, so the
        /// lift has to be added here by hand. THE DOORS ARE SHUT WHILE THE ROLLER IS —
        /// anything else lets a click reach through a closed shutter and take a bottle.
        /// </summary>
        private void LayOutCellarDoors()
        {
            if (_cellarDoorGroup != null)
                _cellarDoorGroup.blocksRaycasts = _drawerT > 0.99f;
            // THE ROOM IS THE WAY OUT (2026-08-25, the author: "açık olan backbarı kapatmak
            // için ekranda raflar hariç bir yere basmak yeterli olmalı, Shut it butonu
            // kaldırılsın"). There was a pink SHUT IT plate floating over the shelves; it is
            // gone. What shuts the cellar now is everywhere that is not the cellar — the
            // ordinary click-outside-to-dismiss the book and the licence have always used,
            // which is why it needed no teaching and no key.
            //
            // The shelves keep their own clicks: the catcher is a full screen of nothing,
            // and the shelf face sits ON it swallowing what lands there, so a miss between
            // two bottles is a miss and not an exit.
            LayOutCellarCatcher();
            // The word and the chevron hand over as the drawer runs: the roller says how to
            // get IN while it is shut, and the rail says how to get back out once it is not.
            if (_cellarOpenGroup != null) _cellarOpenGroup.alpha = 1f - _drawerT;
            if (_cellarShutGroup != null) _cellarShutGroup.alpha = _drawerT;
            if (_cellarDoors.Count == 0) return;
            float left = (Reference.x - _counterNative.x) * 0.5f;
            for (int i = 0; i < _cellarDoors.Count; i++)
            {
                if (!_cellarDoors[i].gameObject.activeSelf) continue;
                CellarSlotArt(i, out float artX, out float artFoot);
                // THE PLATE IS THE BOTTLE'S OWN WIDTH now that the slots are not equal.
                // It used to be a share of the bay less four, which was fine while every
                // slot was the same size and would be a lie here: a thin vermouth would
                // carry a plate wider than itself and swallow its fat neighbour's clicks.
                // The full width, not width-less-four — the gap between two bottles is
                // one pixel at the tightest packing, so there is nothing left to inset.
                _cellarDoors[i].sizeDelta = new Vector2(
                    i < _cellarSlotW.Count ? _cellarSlotW[i] : CellarBottleH * 0.5f,
                    CellarBottleH);
                _cellarDoors[i].anchoredPosition = new Vector2(
                    left + artX,
                    CounterRestY + CounterSurfaceInset - artFoot + DrawerTravel * _drawerT);
            }
        }

        /// <summary>
        /// The roller's own door. THE AFFORDANCE WAS ALREADY DRAWN: the author put a pink
        /// arrow at the shutter's top centre pointing down, which is the way it travels, so
        /// the plate only has to make the picture answer. It rides the roller, which means
        /// the same plate shuts the cellar again from the sliver the open frame leaves at
        /// the sill — there is nothing else to click down there.
        /// </summary>
        private void BuildShutterDoor()
        {
            var root = OverlayCanvas("ShutterDoor", 6, raycasts: true);
            UiAuditExempt.Mark(root, "the shutter door is a hit plate over the roller in the "
                + "room, sized to the roller's own art");
            _shutterDoor = NewRect("ShutterDoor", root);
            _shutterDoor.anchorMin = _shutterDoor.anchorMax = new Vector2(0, 0);
            _shutterDoor.pivot = new Vector2(0.5f, 1f);        // hung by its top, like the art
            _shutterDoor.sizeDelta = _shutterNative;
            var hit = _shutterDoor.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var btn = _shutterDoor.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => SetDrawerOpen(!DrawerOpen));
            // The roller breathes under the pointer. HoverRelay rather than HoverGlow: what
            // answers here is not a colour but the shutter's own travel, and the travel is
            // the room's to run (ApplyDrawer), not a component's.
            var relay = _shutterDoor.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () => _shutterHovered = true;
            relay.Exited = () => _shutterHovered = false;
            BuildShutterLight();
            BuildOpenSign();
        }

        /// <summary>
        /// WHAT COMES OUT OF THE CRACK. The roller slides down a few units under the pointer
        /// and the cellar behind it is lit; this is that light, landing on the sill the roller
        /// just left. Warm, because the light in this room is tungsten and the cellar is part
        /// of the room.
        ///
        /// It hangs off the OPENING's top edge rather than off the roller, because the
        /// opening is where a slit can appear at all — the roller travels, the hole does not.
        /// It rides the drawer for the same reason every other cellar fitting does.
        ///
        /// No art on disk means no light and a roller that still peeks: the movement alone
        /// already says "this is a thing", and half an affordance beats a pink rectangle.
        /// </summary>
        private void BuildShutterLight()
        {
            if (_shutterLight != null || _shutterDoor == null) return;
            var art = ItemArt.Load("light_spill");
            if (art == null) return;
            var rt = NewRect("ShutterLight", _shutterDoor.parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 1f);      // hung by its top, at the opening's lip
            rt.sizeDelta = new Vector2(_shutterNative.x, art.rect.height);
            rt.SetAsFirstSibling();                // under the door plate; it takes no clicks
            _shutterLight = rt.gameObject.AddComponent<Image>();
            _shutterLight.sprite = art;
            _shutterLight.raycastTarget = false;
            _shutterLight.gameObject.SetActive(false);
            UiAuditExempt.Mark(rt, "the light out of the cellar's crack is a lit sliver of "
                + "room, drawn at the shutter's own width");
            LayOutShutterLight();
        }

        private void LayOutShutterLight()
        {
            if (_shutterLight == null) return;
            float alpha = _shutterPeek * (1f - _drawerT);
            bool on = alpha > 0.002f;
            if (_shutterLight.gameObject.activeSelf != on) _shutterLight.gameObject.SetActive(on);
            if (!on) return;
            var rt = (RectTransform)_shutterLight.transform;
            rt.anchoredPosition = new Vector2(
                Reference.x * 0.5f,
                CounterRestY + CounterSurfaceInset - ShutterOpeningTopPx + DrawerTravel * _drawerT);
            _shutterLight.color = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>
        /// THE WORD PAINTED ON THE ROLLER (2026-08-25, the author: "Menu butonu kaldirilsin
        /// onun yerine ekrandaki raflarin onundeki kapaga, text olarak Open ve asagi ok
        /// koyulsun"). Open, and the way it travels, in the italic three-coat lettering
        /// Tools/open_sign_gen.py strikes - magenta keyline, white-into-dark-pink lining,
        /// pink body - capped at the 34 px the author set.
        ///
        /// IT IS A LABEL AND NOT A BUTTON, on purpose. The roller already IS the door: a hit
        /// plate the size of the whole drawing sits under this (BuildShutterDoor), which is
        /// why the plate this replaces never took a click either - the cellar's canvas has
        /// blocksRaycasts off while the drawer is shut, so everything drawn up here falls
        /// through to the roller. A second button over the same pixels would be a second door.
        ///
        /// IT RIDES. The writing is ON the shutter, so it goes down with the shutter and fades
        /// as it goes; hanging it off the screen instead would leave the word floating while
        /// the slats slid away behind it, which reads as a decal on glass.
        /// </summary>
        private void BuildOpenSign()
        {
            if (_shutterDoor == null || _cellarOpenSign != null) return;
            // THE GUARD IS THE ARROW, not the word (2026-09-04). The roller stopped drawing
            // the word on 2026-08-26 and sign_open.png left the disk with it; this still
            // loaded the word as its no-art guard, so the sign — and the OpenSignArrow the
            // PlayMode suite presses — silently stopped being built once the editor's
            // Resources cache no longer had the deleted file. What is drawn is what gates.
            // THE ARROW IS GONE (2026-09-21, the author: "Ne olursa olsun üstündeki pembe aşağı oku kaldır"):
            // the roller opens as it did, without the chevron struck on it.
            var arrow = (Sprite)null;
            if (arrow == null) return;   // no art on disk: a bare roller still opens

            _cellarOpenSign = NewRect("OpenSign", _shutterDoor);
            _cellarOpenSign.anchorMin = _cellarOpenSign.anchorMax = new Vector2(0.5f, 1f);
            _cellarOpenSign.pivot = new Vector2(0.5f, 1f);
            _cellarOpenSign.sizeDelta = Vector2.zero;
            _cellarOpenSign.anchoredPosition = Vector2.zero;
            _cellarOpenGroup = _cellarOpenSign.gameObject.AddComponent<CanvasGroup>();
            _cellarOpenGroup.blocksRaycasts = false;
            _cellarOpenGroup.interactable = false;
            UiAuditExempt.Mark(_cellarOpenSign, "the roller's OPEN is struck lettering, not "
                + "type: one sprite, drawn at the size it was struck");

            // NO WORD ON THE ROLLER ANY MORE (2026-08-26, the author: "stock yazısı
            // kaldırılacak, sadece büyük bir ok koyulacak"). OPEN BAR became STOCK the same
            // morning and STOCK lasted one build: the roller does not need naming, it needs
            // POINTING AT, and the chevron was already doing that. So the chevron alone,
            // grown to a whole 3× — big enough to be the door's whole voice — hung where the
            // word used to sit. `word` is still loaded above as the no-art guard: the sign
            // tool ships the word and the arrows together, so its absence still means "no
            // art on disk", and the suites now aim at the ARROW.
            SignMark(_cellarOpenSign, "OpenSignArrow", arrow,
                SignWordDrop + 8f, scale: 3f);
            BuildShutSign();
        }

        /// <summary>
        /// THE MARK ON THE RAIL (2026-08-25, the author: "gözüken kısıma üst ok görseli
        /// koyalım open yazısında koyulduğu gibi aynı şekilde"). The cellar's way back out,
        /// said the same way its way in is said: the same chevron in the same three coats,
        /// struck mirrored, sitting on the strip of roller left standing at the sill.
        ///
        /// It is the OPEN sign's opposite number in every way, which is why it is built here
        /// rather than somewhere of its own: same parent, same 1:1 grain, same rule that the
        /// mark takes no clicks. What differs is one number — this one fades IN as the drawer
        /// opens, because the word and the chevron are never both true at once.
        /// </summary>
        private void BuildShutSign()
        {
            // THE ARROW IS GONE (2026-09-21, the author: "Ne olursa olsun üstündeki pembe aşağı oku kaldır"). The
            // rail still shuts the cellar; it just no longer wears a chevron saying so.
            if (_shutterDoor == null || _cellarShutSign != null) return;
            var mark = (Sprite)null;
            if (mark == null) return;

            _cellarShutSign = NewRect("ShutSign", _shutterDoor);
            _cellarShutSign.anchorMin = _cellarShutSign.anchorMax = new Vector2(0.5f, 1f);
            _cellarShutSign.pivot = new Vector2(0.5f, 1f);
            _cellarShutSign.sizeDelta = Vector2.zero;
            _cellarShutSign.anchoredPosition = Vector2.zero;
            _cellarShutGroup = _cellarShutSign.gameObject.AddComponent<CanvasGroup>();
            _cellarShutGroup.blocksRaycasts = false;
            _cellarShutGroup.interactable = false;
            _cellarShutGroup.alpha = 0f;
            UiAuditExempt.Mark(_cellarShutSign, "the rail's chevron is struck lettering, not "
                + "type: one sprite, drawn at the size it was struck");
            SignMark(_cellarShutSign, "ShutSignArrow", mark, ShutMarkDrop);
        }

        /// <summary>One piece of a sign, hung by its own centre this far below the roller's
        /// top edge — drawn at the size it was struck, or at a whole multiple of it, which
        /// is the only kind of "bigger" pixel art has.</summary>
        private void SignMark(RectTransform host, string name, Sprite art, float drop,
                              float scale = 1f)
        {
            if (art == null || host == null) return;
            // NAMED, not called after its sprite. The roller is the only way into the cellar
            // now, and the PlayMode suites drive it by aiming at this mark — a hook named
            // after an asset moves the day somebody renames the asset.
            var rt = NewRect(name, host);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = art.rect.size * scale;
            rt.anchoredPosition = new Vector2(0f, -drop);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = art;
            img.raycastTarget = false;
        }

        /// <summary>
        /// Puts the roller's hit plate — and the writing riding on it — where the roller is.
        ///
        /// THE PLATE DOES NOT TAKE THE PEEK; THE WRITING DOES (2026-08-25, the author: "Mouse
        /// raf kapağının üstüne geldiğinde biraz aşağı iniyor aynı şekilde open tuşu da sanki
        /// kapağın üstündeymiş gibi aşağı inmeli"). The word is painted ON the slats, so when
        /// the roller leans under the pointer the word has to lean with it or it is a decal
        /// on glass — but the PLATE that senses the pointer must not move, or its own top
        /// seven units would slide out from under the very pointer holding them open and the
        /// roller would sit there flickering at 7 units every 0.14s. So the plate is nailed
        /// to the roller's rest position and only the two signs ride the crack.
        /// </summary>
        private void LayOutShutterDoor()
        {
            if (_shutterDoor == null) return;
            _shutterDoor.anchoredPosition = new Vector2(
                Reference.x * 0.5f,
                CounterRestY + CounterSurfaceInset - ShutterOpeningTopPx
                    + (DrawerTravel - ShutterTravel) * _drawerT);
            // The same two numbers ApplyDrawer moves the slats by, read back off the same
            // fields: down while the cellar is shut, up while it is open.
            if (_cellarOpenSign != null)
                _cellarOpenSign.anchoredPosition =
                    new Vector2(0f, -ShutterPeek * _shutterPeek * (1f - _drawerT));
            if (_cellarShutSign != null)
                _cellarShutSign.anchoredPosition =
                    new Vector2(0f, ShutterPeek * _shutterPeek * _drawerT);
            LayOutShutterRail();
        }

        /// <summary>
        /// THE RAIL IS A DOOR (2026-08-25). The strip of roller left standing at the sill is
        /// what shuts the cellar again, so it needs to answer a pointer — and it cannot do
        /// that from the ShutterDoor canvas, which sorts UNDER the cellar's own full-screen
        /// catcher: with the drawer open, every click and every hover on that strip is eaten
        /// before it arrives. So the rail's plate lives IN the cellar's canvas as its last
        /// child, which is the one place Unity asks before the catcher and before the
        /// shelves' guard, and it blocks nothing while the drawer is shut because that whole
        /// canvas stops taking rays.
        ///
        /// It is a hair narrower than the roller so the two ends of the sill still belong to
        /// the room, and it is sized to the strip rather than to the roller: a plate the
        /// height of the whole shutter would hang off the bottom of the screen and take the
        /// clicks meant for the lower shelf's stock on its way past.
        /// </summary>
        private void BuildShutterRail()
        {
            if (_cellarDoorRoot == null) return;
            // LAST, every time it is asked. The stock's own doors are added to this canvas
            // as the shelf grows, and each one lands after the rail — so "build once" is not
            // enough here: the rail has to be sent to the back of the queue again whenever
            // the cellar is re-dealt, or a bottle's plate ends up over the sill.
            if (_shutterRailDoor != null) { _shutterRailDoor.SetAsLastSibling(); return; }
            _shutterRailDoor = NewRect("ShutterRail", _cellarDoorRoot);
            _shutterRailDoor.anchorMin = _shutterRailDoor.anchorMax = new Vector2(0, 0);
            _shutterRailDoor.pivot = new Vector2(0.5f, 1f);   // hung by its top, like the art
            var hit = _shutterRailDoor.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var btn = _shutterRailDoor.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => SetDrawerOpen(false));
            var relay = _shutterRailDoor.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () => _shutterHovered = true;
            relay.Exited = () => _shutterHovered = false;
            _shutterRailDoor.SetAsLastSibling();
            UiAuditExempt.Mark(_shutterRailDoor, "the rail is a hit plate over the strip of "
                + "roller the open frame leaves at the sill, sized to that strip");
            LayOutShutterRail();
        }

        /// <summary>Puts the rail's plate over the strip of roller that is actually showing.
        /// FIXED against the peek, for the reason LayOutShutterDoor gives: the plate the
        /// pointer is being felt through may not move under the pointer.</summary>
        private void LayOutShutterRail()
        {
            if (_shutterRailDoor == null) return;
            bool open = _drawerT > 0.5f;
            if (_shutterRailDoor.gameObject.activeSelf != open)
            {
                _shutterRailDoor.gameObject.SetActive(open);
                // A plate switched off under the pointer never gets its OnPointerExit, and a
                // hover left standing would hold the shut roller leaning for the rest of the
                // night. The pointer is re-read every frame anyway, so dropping it here costs
                // at most one frame of a hover that is about to be re-announced.
                if (!open) _shutterHovered = false;
            }
            if (!open) return;
            float top = CounterRestY + CounterSurfaceInset - ShutterOpeningTopPx
                        + (DrawerTravel - ShutterTravel) * _drawerT;
            _shutterRailDoor.anchoredPosition = new Vector2(Reference.x * 0.5f, top);
            // Off the roller's own art, which may not be installed yet the first time the
            // cellar is dealt — the rail is re-laid every time the drawer moves, so a frame
            // at zero width corrects itself rather than throwing a negative size.
            _shutterRailDoor.sizeDelta =
                new Vector2(Mathf.Max(0f, _shutterNative.x - 16f), ShutterRail);
        }

        /// <summary>
        /// THE WAY BACK OUT (2026-08-22, the author: "backbar açıldıktan sonra kapatılmıyor
        /// onun için başka buton ekle"). The roller shuts the cellar and its arrow is the
        /// diegetic way to do it — but once the drawer is open the roller has travelled to
        /// the sill and only a few pixels of it are left to aim at, which is not a door, it
        /// is a keyhole. This key sits on the counter's own slab, over the shelves, and is
        /// only there while the cellar is.
        ///
        /// It was drawn with the author's PINK KEY, nine-sliced, and it is GONE (2026-08-25).
        /// A modal state does not need a key to leave it; it needs somewhere to click that is
        /// not the modal. Two plates and a floating caption have been replaced by two
        /// invisible rectangles.
        /// </summary>
        private void BuildCellarCatcher()
        {
            if (_cellarCatcher != null || _cellarDoorRoot == null) return;

            // THE CATCHER IS A FULL SCREEN OF NOTHING, and it is the FIRST child of the
            // cellar's canvas on purpose: Unity hit-tests a canvas back to front by sibling
            // order, so everything added after this one — the shelf face, then every bottle's
            // own door — is asked before it is. It only ever sees the clicks nobody else
            // wanted, which is exactly the definition of "off the shelves".
            _cellarCatcher = NewRect("CellarCatcher", _cellarDoorRoot);
            Stretch(_cellarCatcher, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var back = _cellarCatcher.gameObject.AddComponent<Image>();
            back.color = new Color(0, 0, 0, 0);
            var backBtn = _cellarCatcher.gameObject.AddComponent<Button>();
            backBtn.targetGraphic = back;
            backBtn.transition = Selectable.Transition.None;
            backBtn.onClick.AddListener(() => SetDrawerOpen(false));
            _cellarCatcher.SetAsFirstSibling();

            // ...AND THE SHELVES ARE THE EXCEPTION. Sized to the counter's own shelf opening
            // (LayOutCellarCatcher measures it), it takes the clicks that land on the cellar
            // and does nothing with them, so missing a bottle by four pixels costs a bottle
            // and not the whole room.
            _shelfGuard = NewRect("ShelfGuard", _cellarDoorRoot);
            _shelfGuard.anchorMin = _shelfGuard.anchorMax = new Vector2(0, 0);
            _shelfGuard.pivot = new Vector2(0, 0);
            var guard = _shelfGuard.gameObject.AddComponent<Image>();
            guard.color = new Color(0, 0, 0, 0);
            var guardBtn = _shelfGuard.gameObject.AddComponent<Button>();
            guardBtn.targetGraphic = guard;
            guardBtn.transition = Selectable.Transition.None;   // swallows, and says nothing
            _shelfGuard.SetSiblingIndex(1);

            UiAuditExempt.Mark(_cellarCatcher, "the cellar's way out is the room around it: "
                + "an invisible full-screen catcher under the shelves' own guard");
        }

        /// <summary>
        /// Puts the shelf guard over the counter's shelf opening. MEASURED off the same two
        /// numbers the bottles' own doors are placed from — the opening's top row and the
        /// counter's height — so the exception and the shelves cannot drift apart.
        /// </summary>
        private void LayOutCellarCatcher()
        {
            if (_shelfGuard == null) return;
            float left = (Reference.x - _counterNative.x) * 0.5f;
            float top = CounterRestY + CounterSurfaceInset - ShutterOpeningTopPx
                        + DrawerTravel * _drawerT;
            float foot = CounterRestY + CounterSurfaceInset - _counterNative.y
                         + DrawerTravel * _drawerT;
            // The blue posts scan at art x 7 and 630 (see CellarBayCentrePx); between them is
            // shelf, outside them is cabinet, and the cabinet is not the cellar.
            _shelfGuard.anchoredPosition = new Vector2(left + 7f, foot);
            _shelfGuard.sizeDelta = new Vector2(623f, top - foot);
        }

        /// <summary>Slot i's foot, in the counter art's own pixels. One reading, so the
        /// drawn bottle and the plate that catches its click cannot disagree.</summary>
        private void CellarSlotArt(int i, out float artX, out float artFoot)
        {
            if (i < 0 || i >= _cellarSlotX.Count)
            {
                artX = CellarBayCentrePx[0];
                artFoot = CellarShelfFootPx[0];
                return;
            }
            artX = _cellarSlotX[i];
            artFoot = _cellarSlotFoot[i];
        }

        /// <summary>Slot i, filled shelf by shelf and bay by bay, the way a bar restocks.</summary>
        /// <summary>The bottle's two shadows, made on first use and moved with it: its silhouette (order 30, a hair
        /// nearer than the counter, a hair farther than the back plate) and the ellipse under its foot.</summary>
        private void PlaceCellarShadow(SpriteRenderer sr, int i, float x, float footY)
        {
            while (_cellarShadow.Count <= i)
            {
                int k = _cellarShadow.Count;
                var cast = WorldSprite("StockShadow" + k, null, order: 30);
                cast.color = new Color(0f, 0f, 0f, 0.22f);
                var foot = WorldSprite("StockFootShadow" + k, GlassArt.SurfaceDisc(), order: 30);
                foot.color = new Color(0f, 0f, 0f, 0.32f);
                _cellarShadow.Add(cast); _cellarFootShadow.Add(foot);
            }
            var lift = _world != null ? _world.position : Vector3.zero;
            var cast2 = _cellarShadow[i];
            cast2.sprite = sr.sprite;
            cast2.transform.localScale = sr.transform.localScale;
            cast2.transform.position = sr.transform.position + new Vector3(3f, -2f, -0.001f);
            cast2.enabled = sr.enabled; cast2.gameObject.SetActive(sr.gameObject.activeSelf);
            var foot2 = _cellarFootShadow[i];
            var disc = foot2.sprite != null ? foot2.sprite.bounds.size : Vector3.one;
            float w = i < _cellarSlotW.Count ? _cellarSlotW[i] : CellarBottleH * 0.5f;
            foot2.transform.localScale = new Vector3(w * 1.25f / Mathf.Max(0.001f, disc.x), 5f / Mathf.Max(0.001f, disc.y), 1f);
            foot2.transform.position = new Vector3(x, footY + 1f, -0.001f) + lift;
            foot2.enabled = sr.enabled; foot2.gameObject.SetActive(sr.gameObject.activeSelf);
        }

        private void PlaceCellarSlot(SpriteRenderer sr, int i)
        {
            CellarSlotArt(i, out float artX, out float artFoot);

            float h = sr.sprite.bounds.size.y;
            float k = h > 0.0001f ? CellarBottleH / h : 1f;
            sr.transform.localScale = new Vector3(k, k, 1f);

            // The counter hangs from its own rest line; the cellar is measured off its TOP
            // edge, so both live on the same number and a moved bar takes its stock with it.
            //
            // ...AND THE ROOM'S OWN OFFSET IS ADDED BACK. These are stage units, but the
            // DRAWER moves the world root the stock hangs under, so a bottle restocked while
            // the cellar is open would be placed at the shelf's SHUT height and then ride
            // 121 units further up with everything else. It never showed while the cellar
            // was only stocked between nights; it would the moment a bought bottle appears
            // on the shelf you are standing at.
            float counterTop = CounterRestY + CounterSurfaceInset - Reference.y * 0.5f;
            // The slot is where the DRAWING stands; the sprite's pivot is its canvas centre,
            // so the canvas is shifted by the drawing's own offset (art px → stage units).
            float shift = CellarCentreShift(sr.sprite) * k / Mathf.Max(1f, sr.sprite.pixelsPerUnit);
            sr.transform.position = new Vector3(
                artX + shift - _counterNative.x * 0.5f,
                counterTop - artFoot + CellarBottleH * 0.5f, 0f)
                + (_world != null ? _world.position : Vector3.zero);
            PlaceCellarShadow(sr, i, artX + shift - _counterNative.x * 0.5f, counterTop - artFoot);
            // The sandwich rides the same transform as the front: same plate canvas, same
            // scale, same position — 1:1 by construction.
            if (i < _cellarBack.Count)
            {
                _cellarBack[i].transform.localScale = sr.transform.localScale;
                _cellarBack[i].transform.position = sr.transform.position + new Vector3(0f, 0f, -0.002f);   // a hair nearer than the shadow
                _cellarMask[i].transform.localScale = sr.transform.localScale;
                _cellarMask[i].transform.position = sr.transform.position;
            }
        }

        // ── the cellar's own light (2026-08-25) ─────────────────────────────────
        //
        // The author: "açılan yeni alkol rafımızın ışıklandırmasını yap, o sahne çok karanlık
        // kalıyor". It was, and the numbers say why: the bar's downlights hang at stage y 276
        // with a 300 reach and their falloff starts at 0.58 of it, while the cellar's upper
        // shelf sits about 260 units under them — so what arrived on the stock was the very
        // tail of a pool aimed at the slab and the drinkers leaning on it. Opening the drawer
        // brings the lights up WITH the room, so the distance never closes.
        //
        // A back bar is not lit from the ceiling; it is lit from UNDER ITS OWN BOARDS, and
        // that is what these are — one strip a bay, hung just inside each compartment's
        // ceiling, aimed at the counter layer alone so the wall and the drinkers keep the
        // light plan they already have.
        /// <summary>How far under the compartment's ceiling the strip hangs, in art px.</summary>
        private const float CellarLightDropPx = 2f;   // 11 until 2026-09-17: the lamp hangs ON the board it is under
        /// <summary>Its reach. A bay is 175 wide and a compartment ~78 deep, so this carries
        /// the length of one bay and dies before the next one's post.</summary>
        private const float CellarLightRadius = 150f;   // 128 until 2026-09-16: reaches the board's far corners
        /// <summary>Where the falloff starts, and how hard it burns. Held wide, because a
        /// shelf strip is a diffuse line and not a bulb: at the house default the whole pool
        /// is spent on the bottle necks and the feet stay in the dark they were in.</summary>
        private const float CellarLightInner = 0.30f;
        private const float CellarLightIntensity = 1.55f;  // 0.80 was "loş" and too dark (the author, second look: "şişeler çok karanlık kalıyor")
        private const float CellarSpotInnerAngle = 100f, CellarSpotOuterAngle = 160f, CellarSpotRoll = 180f;   // a wide cone, pointing down
        /// <summary>Warm, because the light in this room is tungsten and the cellar is part
        /// of the room — one step brighter and cleaner than the ceiling's, the way a lit
        /// shelf actually reads against the lamps over it.</summary>
        private static readonly Color CellarLightTint = LightLanguage.Key;   // one tungsten for the house (2026-09-21)
        private readonly List<Light2D> _cellarLights = new List<Light2D>();
        // THE LAMP THE LIGHT COMES OUT OF (2026-09-17, the author: "ışığın çıkış köşesi biraz kırpılmış olmalı"): a
        // dark housing on the board's underside over each cone's apex, so the light reads as coming from behind the
        // shelf rather than from a bright point hanging in the bay.
        private readonly List<SpriteRenderer> _cellarLamps = new List<SpriteRenderer>();

        /// <summary>One strip per compartment, born dark: the drawer is what turns them on.</summary>
        private void BuildCellarLights()
        {
            if (_cellarLights.Count > 0) return;
            for (int shelf = 0; shelf < CellarShelfCeilPx.Length; shelf++)
                for (int bay = 0; bay < CellarBayCentrePx.Length; bay++)
                {
                    var l = PointLight($"CellarLight{shelf}_{bay}",
                        CellarLightTint, 0f, CellarLightRadius);
                    l.pointLightInnerRadius = CellarLightRadius * CellarLightInner;
                    // A SPOT, NOT A BULB (2026-09-16): a cone from the bay's ceiling down onto the board — dim, so the
                    // bottles' own shadows read (PlaceCellarShadow). Reset from the 2026 point light on the author's word.
                    l.pointLightInnerAngle = CellarSpotInnerAngle;
                    l.pointLightOuterAngle = CellarSpotOuterAngle;
                    l.transform.localRotation = Quaternion.Euler(0f, 0f, CellarSpotRoll);
                    LightLayers(l, LayerCounter);
                    _cellarLights.Add(l);
                    var lamp = WorldSprite($"CellarLamp{shelf}_{bay}", WhitePixel(), order: 31);
                    lamp.color = new Color(0.05f, 0.04f, 0.06f, 0.92f);
                    _cellarLamps.Add(lamp);
                }
            PlaceCellarLights();
        }

        /// <summary>Hangs them off the counter art's own rows, so a re-cut counter takes its
        /// shelf lighting with it. Placed in the same frame the stock is (see
        /// <see cref="PlaceCellarSlot"/>): stage units, plus whatever the drawer is doing.</summary>
        private void PlaceCellarLights()
        {
            if (_cellarLights.Count == 0 || _counterNative.x <= 0f) return;
            float counterTop = CounterRestY + CounterSurfaceInset - Reference.y * 0.5f;
            var lift = _world != null ? _world.position : Vector3.zero;
            int i = 0;
            for (int shelf = 0; shelf < CellarShelfCeilPx.Length; shelf++)
                for (int bay = 0; bay < CellarBayCentrePx.Length; bay++, i++)
                {
                    if (i >= _cellarLights.Count || _cellarLights[i] == null) continue;
                    var at = new Vector3(
                        CellarBayCentrePx[bay] - _counterNative.x * 0.5f,
                        counterTop - CellarShelfCeilPx[shelf] - CellarLightDropPx, 0f) + lift;
                    _cellarLights[i].transform.position = at;
                    if (i < _cellarLamps.Count && _cellarLamps[i] != null)
                    {
                        var lamp = _cellarLamps[i];
                        var size = lamp.sprite != null ? lamp.sprite.bounds.size : Vector3.one;
                        lamp.transform.localScale = new Vector3(30f / Mathf.Max(0.001f, size.x), 5f / Mathf.Max(0.001f, size.y), 1f);
                        lamp.transform.position = at + new Vector3(0f, 2f, 0f);
                    }
                }
        }

        /// <summary>They burn with the DRAWER. A cellar lit behind a shut roller would print
        /// a bar of light across the slats; the peek has its own spill for that moment
        /// (<see cref="BuildShutterLight"/>) and this stays out of its way.</summary>
        private void ApplyCellarLight()
        {
            for (int i = 0; i < _cellarLights.Count; i++)
                if (_cellarLights[i] != null)
                    _cellarLights[i].intensity = CellarLightIntensity * _drawerT;
            for (int i = 0; i < _cellarLamps.Count; i++)
                if (_cellarLamps[i] != null)
                    _cellarLamps[i].enabled = _drawerT > 0.05f;
        }

        private void ApplyDrawer()
        {
            if (_world != null)
                _world.position = new Vector3(
                    0f, DrawerTravel * _drawerT * Mathf.Max(0.0001f, _worldScale), 0f);
            if (_shutterTr != null)
            {
                var lp = _shutterTr.localPosition;
                // ONE PEEK, TWO DIRECTIONS, and the drawer's own progress is what picks
                // between them: down while the cellar is shut (opening a crack at the top,
                // which is where the light comes out), up while it is open (the start of
                // the movement that would shut it again). Halfway through the travel both
                // are half spent and cancel, which is right — a roller already moving has
                // nothing to hint at.
                float peek = ShutterPeek * _shutterPeek * (1f - _drawerT);
                float lean = ShutterPeek * _shutterPeek * _drawerT;
                _shutterTr.localPosition = new Vector3(
                    lp.x, _shutterRestLocalY - ShutterTravel * _drawerT - peek + lean, lp.z);
            }
            LayOutShutterLight();
            ApplyCellarLight();
            LayOutCellarDoors();
            LayOutShutterDoor();
            // The counter's own doors go out with the drawer — see BuildPropDoor's note.
            if (_propDoorGate != null) _propDoorGate.blocksRaycasts = _drawerT < 0.01f;
        }

        private void StepDrawer()
        {
            // The crack eases open and shut on its own clock, which runs whether or not the
            // drawer is moving — hovering a roller that is already still is the whole point.
            // It no longer asks WHICH way the drawer is: both states lean now (ApplyDrawer
            // resolves the direction), so the pointer gets an answer over the shut roller
            // and over the rail alike.
            float wantPeek = _shutterHovered ? 1f : 0f;
            if (!Mathf.Approximately(_shutterPeek, wantPeek))
            {
                _shutterPeek = Motion.Reduced ? wantPeek : Mathf.MoveTowards(
                    _shutterPeek, wantPeek, Time.unscaledDeltaTime / PeekSeconds);
                ApplyDrawer();
            }
            if (Mathf.Approximately(_drawerT, _drawerTarget)) return;
            // Unscaled: the cellar opens while the night's clock is running, and a paused or
            // slowed run must not leave the roller half down.
            float step = Time.unscaledDeltaTime / DrawerSeconds;
            _drawerT = Mathf.MoveTowards(_drawerT, _drawerTarget, step);
            ApplyDrawer();
        }

        // ── the bar runs past both edges (2026-08-19) ───────────────────────────
        // The art ends inside the frame: its slab is drawn with depth, so the back edge
        // tapers in over the last fifty pixels at each end and the bar visibly STOPS, with
        // floor showing beside its cut corners. The fix is not a wider drawing and NOT a
        // stretched one — the sprite is 9-sliced (border 168 / 305, FullRect mesh, already
        // set on the importer) and drawn TILED, so the middle bay repeats at its own size
        // and the two ends keep their joinery. Everything past the frame is off-screen: what
        // the player sees is a bar that carries on past both edges.
        //
        // How many times the middle bay repeats. TWO is the smallest number that both pushes
        // the tapered ends out of frame (the counter draws 26% wider than the room, so ~13%
        // hangs off each side) and lands on a WHOLE tile — Continuous tiling clips a partial
        // last tile, and a bay sliced down the middle beside the end cabinet is the one
        // artefact this could produce.
        private const int CounterMiddleTiles = 2;
        private float _counterDrawWidth;            // art px actually drawn, native if untiled

        /// <summary>
        /// Puts the bar on the renderer's 9-slice, and answers how many art pixels wide it
        /// will actually draw.
        ///
        /// Everything is read off the SPRITE rather than written down twice: the border it
        /// was imported with says where the caps end, so re-cutting the counter art moves
        /// this with it. A sprite that arrives without a border (or on a Tight mesh, which
        /// cannot 9-slice at all) is drawn plain at its native width — a bar that stops
        /// inside the frame is a blemish, and a silent fallback beats a torn one.
        /// </summary>
        private float SetUpCounterTiling(SpriteRenderer sr)
        {
            var border = sr.sprite.border;          // x left, y bottom, z right, w top
            float middle = _counterNative.x - border.x - border.z;
            if (border.x <= 0f || border.z <= 0f || middle <= 0f)
            {
                Debug.LogWarning("DiegeticStage: the counter sprite has no left/right border, " +
                                 "so it cannot be widened without stretching — drawing it at " +
                                 "its native width. Set the border on the importer.");
                return _counterNative.x;
            }
            float drawn = border.x + middle * CounterMiddleTiles + border.z;
            sr.drawMode = SpriteDrawMode.Tiled;
            // Continuous, not Adaptive: Adaptive stretches the tile to make it fit, which is
            // the one thing this is here to avoid. With a whole number of tiles there is
            // nothing left to fit.
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(drawn, _counterNative.y);
            return drawn;
        }

        /// <summary>
        /// Where shelf compartment <paramref name="index"/> is standing right now, in STAGE
        /// units: the centre of its opening, the floor a glass stands on, and how much
        /// headroom there is under the shelf board. False when the bar was never drawn.
        ///
        /// ShelfCentrePx is measured on the bar AS DRAWN — 9-sliced out to _counterDrawWidth
        /// — so a cell is simply its offset from that bar's left edge. Should the sprite ever
        /// arrive without a border, the bar falls back to its native width and these cells
        /// are the wrong ones; SetUpCounterTiling logs that case rather than letting it pass.
        /// </summary>
        /// <summary>
        /// Where a point of the COUNTER DRAWING stands, in stage units with the room at rest
        /// (bottom-left origin; the drawer's lift is the caller's to add). The counter is
        /// 9-sliced and tiled to the window, so an art pixel in the right cap is measured
        /// from the drawn bar's right edge, not from the sprite's left. Written for the towel
        /// rail the author drew into the counter's right cap (2026-09-07); anything else that
        /// has to sit ON a feature of the drawing should ask this rather than type a number.
        /// </summary>
        public bool CounterArtPoint(float artX, float artYFromTop, out Vector2 stage)
        {
            stage = default;
            if (_counterTr == null) return false;
            var sr = _counterTr.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return false;
            float drawnW = sr.drawMode == SpriteDrawMode.Tiled ? sr.size.x : _counterNative.x;
            var border = sr.sprite.border;
            float drawnX = border.z > 0f && artX > _counterNative.x - border.z
                ? drawnW - (_counterNative.x - artX)
                : artX;
            float artTopStage = CounterRestY + CounterSurfaceInset * _counterScale;
            stage = new Vector2(
                Reference.x * 0.5f + (drawnX - drawnW * 0.5f) * _counterScale,
                artTopStage - artYFromTop * _counterScale);
            return true;
        }

        /// <summary>Where cellar slot <paramref name="i"/> stands — the bottle's foot centre in
        /// stage units with the room at rest (2026-09-07, for the shelf's group captions).</summary>
        public bool CellarSlotStage(int i, out Vector2 stage)
        {
            stage = default;
            if (i < 0 || i >= _cellarSlotX.Count) return false;
            float counterTop = CounterRestY + CounterSurfaceInset;
            stage = new Vector2(Reference.x * 0.5f + _cellarSlotX[i] - _counterNative.x * 0.5f,
                                counterTop - _cellarSlotFoot[i]);
            return true;
        }

        public int CellarSlotCount => _cellarSlotX.Count;

        public bool ShelfCell(int index, out float centerX, out float floorY, out float height)
        {
            centerX = 0f; floorY = 0f; height = 0f;
            if (_counterTr == null || _counterNative.x <= 0f) return false;
            if (index < 0 || index >= ShelfCentrePx.Length) return false;
            float scale = _counterScale;
            // The art's own top edge, in stage units: the rest line is CounterSurfaceInset
            // art-pixels below it, and that line is pinned to CounterRestY.
            float artTopY = CounterRestY + CounterSurfaceInset * scale;
            float drawn = _counterDrawWidth > 0f ? _counterDrawWidth : _counterNative.x;
            centerX = (ShelfCentrePx[index] - drawn * 0.5f) * scale;
            floorY = artTopY - ShelfFloorPx * scale;
            height = (ShelfFloorPx - ShelfCeilPx) * scale;
            return true;
        }

        // (RegisterBaseY and the display-window fractions went out with the till on
        //  2026-08-26 — see BuildRegister's headstone below. Anything else standing on the
        //  bar still writes itself as an offset from CounterRestY, which is the rule the
        //  till's own note left behind and the only part of it worth keeping.)

        // 128 -> 116 (2026-08-19, the author, in play: "masayi biraz asagi cek"), then
        // 116 -> 131 the same evening ("tezgahi Y ekseninde -122'ye al"). The author reads
        // this one in the INSPECTOR, not here: the counter is a child of a world root whose
        // scale is 1 at 16:9, so the transform's y is CounterRestY + CounterSurfaceInset −
        // the art's own half height (75) − half the stage (180) = CounterRestY − 253. −122
        // is 131. The whole counter layer rides this number, which is why it is the dial —
        // and everything standing ON the counter is now written as an offset from it rather
        // than as its own constant, because the last two moves left the till and the beer
        // fonts behind.
        // 131 -> 120 (2026-08-21, the drawer counter). READ OFF THE AUTHOR'S OWN MOCK-UP
        // rather than tuned: in mockup_drawer_closed.png the slab's near-black band starts
        // at screen row 240 of 360, which is 120 above the bottom edge. The same slab sits
        // at row 119 in the OPEN frame - a difference of exactly 121, which is the drawer's
        // whole travel and the number Phase 2 will slide the scene by.
        private const float CounterRestY = 120f;           // counter-top rest line (till, glassware)
        // Measured off the art: the bar's far edge — where a glass is set down — is this far
        // below the sprite's top (2026-07-29).
        private const float CounterSurfaceInset = 2f;
        private const float CounterFrontY = 96f;           // surface line: the bottom 96px band
        private const float Overscan = 48f;         // bleed past screen edges (aspect safety)

        // ── the light plan (2026-08-10) ─────────────────────────────────────────
        // The room's ceiling lights, measured off club_room.png by clustering its
        // warm-bright pixels (Tools would guess; the art knows). RE-MEASURED TWICE: v2's
        // room hung FOUR pendant lamps low over the floor at y 84; the 2026-08-18 painted
        // shell put THREE recessed downlights at x 222/331/455, y 51; the PixelLab room
        // that replaced it that evening draws the same three a little higher and a little
        // wider apart. The clustering is in Tools/scene_variants_gen.py (`measure`), so the
        // next room does not need this done by hand — and it tests warm-over-cool, not just
        // brightness, because this room's cornice carries a CYAN rim light that is every bit
        // as bright as a downlight and is not one.
        // Each gets a warm pool; the global wash is slightly cool and slightly below 1
        // so the pools read as light and not as paint.
        // THE CEILING LIGHTS ARE GONE (2026-08-23). Three of them hung at art y 57 and had
        // been re-measured against three different rooms since 2026-08-10. The room that
        // ships now has NO CEILING IN FRAME - the picture stops at a cream band across the
        // top of the back wall - so what they lit was blank plaster, and what the player saw
        // was three white blobs arriving from nothing. The author: "su an tavan aydinlatmasi
        // yok tavan aydinlatmasi yerine duvar aydinlatmasini arka plandaki krem duvara 2
        // tane koy". A light with no fixture is a mistake, not a mood.
        //
        // TWO WALL LIGHTS INSTEAD — and one day later they stopped being the stage's own
        // hand-drawn stand-ins (2026-08-24): the author drew the lamps themselves, three
        // levels of them, and they sell as a fixture LADDER standing in the paired
        // "wall_lamps" slot (fixtures.json). The stage no longer knows what a wall lamp
        // looks like; it knows the slot is marked houseLight, and it runs whatever shines
        // there on the evening's clock:
        /// <summary>The house lights against the sky: dim while the window owns the room
        /// (the json intensity is the NIGHT value), up as the sky dies.</summary>
        private const float HouseDay = 0.30f, HouseNight = 1.0f;
        private static readonly Color GlobalTint = new Color(0.86f, 0.85f, 0.95f);
        private const float GlobalIntensity = 0.45f;
        /// <summary>The house tungsten: the closing beat's lamp, and the colour a plate with
        /// no sky left in it falls back to.</summary>
        private static readonly Color LampTint = LightLanguage.Key;   // the language's one tungsten (2026-09-21)
        private const float LampIntensity = 0.55f;
        private const float LampRadius = 92f;

        // ── THE ROOM IS LIT BY THE HOUR (2026-09-17) ─────────────────────────────
        //
        // From 2026-08-19 to today the room read its light OFF THE PICTURE in the glass:
        // every frame of the window's sheet was sampled for a key, a wash, a pink band and
        // a "how much day is left", and eight knobs (SkyPunch, AmbientPull, MidShare,
        // WashPunch, NightBounce, the luma range, the glow's area, the alpha gate) turned
        // those samples back into something a room could be lit by. It worked, and it could
        // only ever be as smooth as thirty-one frames, and the sun could only do what the
        // frames did — fade where it stood.
        //
        // The evening is a MODEL now (SkyClock, Resources/Data/sky_cycle.json): one number,
        // the shift's fraction, and everything the window draws and everything the room is
        // lit by comes out of the same evaluation. The author's asks, in order: "günün
        // rengi", "gün batımı içerisinin rengini ve tonunu değiştirmeli", "hava renk
        // değişimi daha smooth olmalı", "güneş aşağı doğru batmalı", "şehir ışıkları ona
        // göre yanmalı", "nesnelerin gölgeleri olmalı", "müşterilerin arkaplandan
        // sıyrılması". Each is one term of the model, named below where it lands.
        //
        // What the room does with it (the numbers are in the json; these are the lights):
        //   THE SUN'S KEY (WindowLight) — the cone through the glass, in the sun's own
        //     colour, full while the disc stands clear of the towers and dying with it.
        //     At night it is the city's glow instead: faint and blue (NightGlassTint).
        //   THE SHAFT (SunShaft) — the patch of sun on the plaster, four panes of it,
        //     sliding right and climbing as the sun sinks: the one thing in the room that
        //     says WHERE the sun is.
        //   THE SKY'S FILL (SkyGlow) — the pink air at the glass, up while the sky burns.
        //   THE AMBIENT (GlobalLight) — the room's base tint by the hour: cream at opening,
        //     coral at the set, pink through the band, blue in the blue hour, then the
        //     house's own tungsten once the lamps are the room's light.
        //   THE HOUSE (wall lamps, downlights, neon) — up as the sky goes, on one Dusk.
        //   THE PEOPLE (PatronFill) — a stop over the wall, always.
        //   THE SHADOWS (CastShadow) — off the sun while it is in the window, off the lamps
        //     after, blended by how much light each is throwing.
        /// <summary>The sun's cone through the glass, at its loudest and as the city glow.</summary>
        // 1.75 for the first probe (2026-09-17) blew the wall by the glass to white with the shaft
        // on top of it; the shaft carries the sun on the wall now and the cone is the fill behind
        // it — and since the glow took the colour (third pass, below), the cone is quieter still.
        private const float SunKeyDay = 0.7f, SunKeyNight = 0.14f;
        /// <summary>What comes through the glass once the sun is gone: the city's own light,
        /// cool and faint — the one cold source in a room of tungsten.</summary>
        private static Color NightGlassTint => UITheme.ClubBlue[3];
        /// <summary>The patch of sun on the wall, at full strength; where it lands while the
        /// sun is high and where it has climbed to by the set (room art px); how much it
        /// stretches sideways as the light comes in flatter.</summary>
        private const float ShaftDay = 1.5f, ShaftStretch = 1.5f;
        // (200, 122), not (215, 150) (2026-09-22): the patch STARTS ON THE FLOOR by the glass - the boards run to
        // about 135 in the room's art, and 150 was already the wall's foot, so the floor's shape never showed.
        private static readonly Vector2 ShaftNear = new Vector2(200f, 122f);
        private static readonly Vector2 ShaftFar = new Vector2(400f, 235f);
        /// <summary>The lift on the drinkers alone, in the sun and under the lamps.</summary>
        private const float PatronFillDay = 0.22f, PatronFillNight = 0.32f;
        private static readonly Color PatronFillTint = LightLanguage.PatronLift;   // the key, most of the way to white

        // ── A WINDOW IS AN AREA, NOT A BULB (2026-08-19) ────────────────────────
        //
        // The author: "camdan vuran ışığın tamamı müşterilerin üzerinde olması mantıksal
        // olarak doğru mu? camla aynı hizadalar — bir arka plana vuran ışığın bir kısmı
        // müşterilerin üstüne vursa daha mantıklı olmaz mı?" It would, and the numbers said
        // so: the light sat at the glass (art x 54.5) and the first stool stands at x 59 —
        // four and a half units, well inside the light's own inner core — so the person
        // beside the window took the full throw while the back wall, 265 units off, took a
        // fifth of it. Measured over the six stools: 0.97 at the first against 0.19 on the
        // wall. A window lit its neighbour and barely lit the room.
        //
        // Setting the source back and giving it a cone was the first fix and it was not
        // enough: pushed to 1200 units with the radius to match, the ratio only fell from
        // 5.5 to 2.6 and the whole room went dim with it. The reason is structural — a POINT
        // light always favours what is nearest, and a window does not. A window is an AREA
        // source: a small room lit through one is lit ALL OVER by it, and what stands beside
        // the glass catches a graze on top of that, not the whole of it.
        //
        // So the window's light is TWO things now: the sun's own cone at the glass, and
        // the sky's broad pink glow behind it (SkyGlow, 2026-08-24) — the split that used to
        // be done here with a share knob is done by two real lights with the sky's own two
        // colours, so the knob went with its job.

        // (The ambient's own knobs — AmbientPull, MidShare, WashPunch, WashDay/Night,
        //  NightBounce, BounceTint — went with the sampling on 2026-09-17: the room's tint
        //  by the hour is the model's "room" keys, palette tokens pulled toward white by a
        //  keep, which is the same arithmetic with the numbers where the author can read
        //  them. The lesson they carried stands: a light MULTIPLIES, so a saturated ambient
        //  does not warm a room, it replaces it — hence the keep.)

        // ── THE LIGHT OVER THE BAR, AND THE BAR'S OWN NEON (2026-08-24, the author:
        //    "BarLight'ı kaldır ve bar masasının üstüne vuran bir ışık yap bu ışık bar
        //    tezgahını ve müşterileri aydınlatmalı, aynı zamanda bar tezgahındaki neon
        //    şeriti de ışıklandır") ────────────────────────────────────
        //
        // ONE point light called BarLight used to be this, and its whole history is three
        // wrong hangings in a single day (2026-08-23): over the room, where it lit the back
        // wall; up the wall, where it painted a round blob on the plaster; and finally at
        // art (320,90) with a 90 reach — which is BELOW the counter's top edge, so what it
        // really lit was the cabinet front. The bar top and the people leaning on it were
        // never in it. A point light also favours whatever is nearest, so one lamp in the
        // middle of a 640-wide bar lights stools 3 and 4 and leaves both ends dark.
        //
        // A bar is lit by TWO things and counter.png draws both of them already, so what
        // hangs here now is two ROWS of lights and not one lamp:
        //
        //   THE DOWNLIGHTS come down from above the drinkers' heads onto the slab, and catch
        //   heads and shoulders on the way — the "üstüne vuran" the author asked for. They
        //   can hang high in a way none of the 2026-08-23 attempts could, and the sorting
        //   layers are why (2026-08-24): pointed at the counter and the patrons only, a lamp
        //   over the bar cannot reach the back wall however far it throws, which is the fight
        //   every earlier hanging lost. Three of them, over the STOOLS rather than over the
        //   middle of the screen, so the pool runs the length of the bar.
        //
        //   THE NEON STRIP is the magenta tube drawn along the front of the slab. Until now
        //   it was PAINT — a bright pink line with no light coming off it, in a room whose
        //   whole look is neon. It gets its own row of small lights sitting ON it, and since
        //   they sit below the drinkers they throw UP: the bar's edge light on a customer's
        //   front, under the tungsten coming down. That pair is what makes a counter read as
        //   a bar counter instead of as a table with bottles behind it.
        //
        // Both rows hang through StageToWorld and not StageArtPointToWorld, which is the
        // frame the two things they are FOR already live in: the counter is pinned at scale 1
        // off CounterRestY, and TycoonHud.SyncPatronBody stands the drinkers in plain stage
        // units. The room art's own space is a cover fit about the picture's centre — the
        // same units only while VisibleWidth() stays pinned to the reference, which is a
        // promise about the camera rig and not about the bar.

        /// <summary>The lamps over the bar, and the neon segments along its edge: enough of
        /// each that the pools overlap into a band instead of reading as separate spots.</summary>
        private const int BarLightCount = 3, BarNeonCount = 7;
        /// <summary>Where the run of lamps starts and how far apart they hang, in stage units.
        /// DERIVED from the stools, not chosen: TycoonHud stands six of them 180 HUD units
        /// apart from x 118, which is 90 stage units apart from stage x 59. One lamp per PAIR,
        /// over each pair's own midpoint — 104, 284, 464 — so three pools cover six seats and
        /// nobody drinks in a gap.</summary>
        private const float BarLightFirstX = 104f, BarLightPitchX = 180f;
        /// <summary>How high they hang, in stage units from the BOTTOM — the direction
        /// StageToWorld reads, and the one that cost 2026-08-23 two wrong hangings. A seated
        /// drinker is drawn 220 units tall with their feet at stage y 19, so heads top out at
        /// 239: this clears them and still reaches the slab (see the radius).</summary>
        private const float BarLightY = 276f;
        /// <summary>Their reach, MEASURED rather than chosen: the slab's own surface band is
        /// stage y 94..122, so the light has ~168 units to fall before it lands on the bar
        /// top, and it has to still be alive when it gets there.</summary>
        private const float BarLightRadius = 300f;
        /// <summary>Where the falloff STARTS, as a fraction of the reach. A downlight is not a
        /// candle: at the house default of 0.15 the whole pool is spent 45 units under the
        /// bulb and the bar top takes the tail of it. Held out past the slab's own distance,
        /// the drinkers stand in the core and the counter still gets a real pool.</summary>
        private const float BarLightInner = 0.58f;
        /// <summary>The lamps against the sky, like every other light the house owns: dim
        /// while the window carries the room, up as the evening dies. Lower per lamp than the
        /// single BarLight's 0.55/1.30 was, because there are three and they overlap.</summary>
        private const float BarLightDay = 0.18f, BarLightNight = 0.52f;
        private readonly List<Light2D> _barLights = new List<Light2D>();

        /// <summary>The tube's line, in stage units from the bottom. ART-BOUND and MEASURED
        /// off counter.png by scanning its top rows: the strip is rows 29..34 below the art's
        /// top edge (row 29 reads #D77BBA and it ramps back into the slab's own #171119 by
        /// row 35), and that top edge is pinned at CounterRestY + CounterSurfaceInset. A
        /// re-cut counter moves this silently — re-scan the rows, do not tune the number.</summary>
        private const float BarNeonRowPx = 31.5f;
        private const float BarNeonY = CounterRestY + CounterSurfaceInset - BarNeonRowPx;
        /// <summary>Small, and it has to be: a tube six art px tall lights its own edge, the
        /// slab lip over it and the hands that come down onto it. Anything wider stops being
        /// a strip and turns into a second lamp under the bar.</summary>
        private const float BarNeonRadius = 70f;
        /// <summary>The tube's colour, taken off the ramp (GDD 16: shading moves ALONG a ramp,
        /// never off it). Magenta[4] is #FF7DC6 and the strip's brightest drawn row is
        /// #D77BBA — the same step, one notch down for having been paint until now.</summary>
        private static Color BarNeonTint => UITheme.Magenta[4];
        /// <summary>Neon does not care what hour it is — a tube is on or it is off — but the
        /// room around it does, so it lifts a little once the window stops out-shouting it.
        /// Bright enough to read as a SOURCE at every hour, which the paint never did.</summary>
        private const float BarNeonDay = 0.34f, BarNeonNight = 0.58f;
        private readonly List<Light2D> _barNeons = new List<Light2D>();
        /// <summary>How far the window's light reaches. The glass is a wall, not a lamp —
        /// this is wide enough to carry across the room and die on the far side.</summary>
        private const float WindowRadius = 640f;
        /// <summary>Where the sun's beam STARTS falling off, as a fraction of its reach. A
        /// point light fades from its inner radius outwards, and at the house default of
        /// 0.15 the gold was spent 80 units into a room 640 wide - it lit the two people by
        /// the glass and nothing else. Sunlight does not fall off across a room; it crosses
        /// it and dies on the far wall, which is what this fraction buys.</summary>
        private const float WindowInner = 0.24f;

        // ── THE WINDOW THROWS, IT DOES NOT RADIATE (2026-08-19) ─────────────────
        //
        // The author: "camdan vuran ışığın tamamı müşterilerin üzerinde olması mantıksal
        // olarak doğru mu? camla aynı hizadalar." It was not, and the numbers say why: the
        // light sat AT the glass (art x 54.5) and the first stool stands at x 59 — four and
        // a half units away, well inside the light's own 84-unit inner core. So the person
        // beside the window was the closest object to the bulb and took the full 2.35, while
        // the back wall — the surface a window actually faces — sat 265 units off and took
        // a fraction of it. A window lit its neighbour and barely lit the room.
        //
        // Two changes put it right, and both are what the real thing does. The source moves
        // BEHIND THE GLASS: light through a window comes from outside, so its origin belongs
        // out there, and then nobody in the room is standing on it. And it becomes a CONE
        // aimed into the room, because a window throws one way — the beam lands on the far
        // wall and the floor, which is where the light in the reference photograph is, and
        // the people at the counter catch its EDGE. Part of it, as asked, rather than all.
        /// <summary>How far outside the glass the sun's origin sits, in art px.</summary>
        private const float WindowSetback = 40f;
        /// <summary>The cone: wide enough to fill the room, tight enough to have a direction.</summary>
        private const float WindowConeOuter = 168f, WindowConeInner = 78f;
        /// <summary>Where the cone points, in degrees about Z. A URP 2D spot opens along its
        /// own UP axis, so −100 aims it right and a little down: across the room and onto the
        /// floor, which is the way light falls through a window standing above head height.
        /// THE ONE NUMBER TO CHECK IN THE ENGINE — if the cone comes out pointing up, the
        /// convention is the transform's right and this wants −10 instead.</summary>
        private const float WindowAimDegrees = -100f;
        /// <summary>The glow's reach: from the glass it crosses most of the room and dies
        /// before the far corner, so the room runs gold, then pink, then violet.</summary>
        private const float SkyGlowRadius = 560f;
        /// <summary>The pink's own arc. It is atmosphere, not a lamp: broad and soft, up
        /// while the sky burns, down to the city's faint air after.</summary>
        // THE SUNSET IS LIGHT, NOT PAINT (third pass, 2026-09-17, the author: "Gün ışığı ortam
        // ışığını çok değiştirmeli, o sarı pembe turuncu renk oyunumuzun miami temasının
        // unsurlarından biri"). The first probe's flat pink was a saturated glow OVER a warm
        // ambient: the whole wall took the same colour and read as paint. The Miami answer is
        // the oldest one in lighting — a WARM KEY and a COOL FILL: the glow through the glass
        // is now loud and coloured (the room's "glow" keys: amber, coral, hot pink, plum) and
        // the ambient under it is cool (blue, violet, plum, blue), so the wall goes orange
        // where the sky lands on it and violet where it does not, and the colour has a
        // direction. Both come from the json now; the constants went with the flat look.
        /// <summary>The glow's second light, on the counter alone, at this share of it — the
        /// bar top catches the sunset too, a little less than the wall it faces away from.</summary>
        private const float CounterGlowShare = 0.55f;

        /// <summary>The evening's model and the view drawn from it. Null when the json or
        /// the city plates are missing — the still plate hangs and the room lights itself
        /// from DefaultDaylight, which is opening light on a build with no sky.</summary>
        private SkyClock _skyClock;
        private WindowSky _sky;
        private WindowSky.Flock _flock;
        private Light2D _windowLight;
        private Light2D _skyGlow, _counterGlow;
        private Light2D _sunShaft;
        /// <summary>The same patch while it lies on the FLOOR (2026-09-21): a second sprite light with the floor's
        /// cookie, crossed over with the wall's as the patch climbs past the skirting (<see cref="FloorTopPx"/>).</summary>
        private Light2D _sunFloor;
        /// <summary>Where the floor meets the wall, in room art px: the patch below it is on the floor and lies
        /// flat, above it on the wall and stands up. Measured off the room's slots (tables at 126, the signs at 178).</summary>
        private const float FloorTopPx = 136f;
        private Light2D _patronFill;
        /// <summary>The unlit material the outside wears: the view, the palms, the pane.</summary>
        private Material _viewMaterial;
        /// <summary>The hour, as the HUD last told it, and everything the model made of it.</summary>
        private float _tau;
        private SkyClock.Daylight _now = DefaultDaylight();
        /// <summary>The sun's key before the closing beat touches it, so the beat falls FROM
        /// the hour rather than from a constant.</summary>
        private float _sunKeyBase = SunKeyDay;
        // The sky-driven bases the closing beat dims FROM. A beat that lerped from a
        // constant would have snapped the room back to noon-of-nowhere the moment the last
        // call began.
        private float _washBase = GlobalIntensity, _houseBase = HouseDay;

        /// <summary>What the room is lit by before the first hour arrives: opening light.</summary>
        private static SkyClock.Daylight DefaultDaylight() => new SkyClock.Daylight
        {
            Tau = 0f, SunVisible = 1f, SunStrength = 1f, Day = 1f, Dusk = 0f, LateNight = 0f,
            SunCore = UITheme.Cream[4], SunRim = UITheme.Amber[4], SunHalo = UITheme.Amber[4],
            SkyTop = UITheme.ClubBlue[2], SkyUpper = UITheme.Magenta[1], SkyMid = UITheme.Magenta[2],
            SkyLow = UITheme.ViceRed[3], SkyHorizon = UITheme.Amber[3],
            Ambient = GlobalTint, AmbientIntensity = GlobalIntensity,
            ShadowOffset = new Vector2(6f, -5f), ShadowAlpha = 0.3f,
        };

        // ── the fixture slots (2026-08-10): where bought dressing stands ────────
        // Named hooks in the ROOM ART's own space (art px, bottom-left origin — identical
        // to stage units at the native 640×360), each marking the BOTTOM CENTRE of whatever
        // stands there. They were seven constants in this file, which made a new place to
        // put a plant a CODE change in a project whose first rule about content is that
        // content is data. They come out of fixtures.json now, beside the fixtures that
        // name them, and the parser refuses a fixture whose slot the room does not have.
        private readonly Dictionary<string, LastCall.Game.StageSlot> _slots =
            new Dictionary<string, LastCall.Game.StageSlot>();

        /// <summary>Hands the room its hooks. Told, not read — the same way the till is told
        /// the money — so the stage never reaches into the run or the loader.</summary>
        public void SetSlots(IReadOnlyList<LastCall.Game.StageSlot> slots)
        {
            _slots.Clear();
            if (slots == null) return;
            foreach (var s in slots) _slots[s.Id] = s;
        }

        private readonly List<(LastCall.Core.FixtureDefinition Def, Transform Body,
                Light2D Glow, float OffsetX)>
            _placedFixtures = new List<(LastCall.Core.FixtureDefinition, Transform,
                Light2D, float)>();

        private Font _display;
        [SerializeField] private Font displayFont;         // Press Start 2P (headings/numbers)

        /// <summary>Installed environment art. When set, the full-screen club background and
        /// the bar counter replace their flat procedural placeholders.</summary>
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite counterSprite;
        /// <summary>The roller shutter that shuts the counter's cellar (2026-08-22). Optional:
        /// with no shutter the cabinet simply stands open, which is what the stage did before
        /// the drawer existed.</summary>
        [SerializeField] private Sprite shutterSprite;


        // ── the world ───────────────────────────────────────────────────────────
        private Transform _world;                   // root of every world-space stage object
        private Material _litMaterial;              // Sprite-Lit-Default, shared by the stage
        private SpriteRenderer _backdropSr, _backgroundSr, _windowSr, _glassSr;

        // ── the palms, and the wind in them (2026-08-23) ────────────────────────
        // The author: "ağaçları görselden ayıralım, ağaçlara animasyonu ayrı vereceğiz çünkü
        // ağaçlar çok daha fazla sallanması gerekiyor". They are their own drawing on their
        // own transparency, standing between the sky and the room - so the room's mullions
        // cut them exactly as they cut the view behind.
        //
        // THE WIND IS CODE, NOT FRAMES, and that is the point of separating them. A drawn
        // sway is one amplitude at one speed for ever; this one BENDS - each horizontal
        // band is pushed by an amount that grows with its height, so the trunk leans and
        // the fronds at the top travel furthest, which is what a palm does. It costs
        // nothing to make the wind stronger, and the two trees can be out of step.
        //
        // ONE PIECE, LEANING FROM THE ROOT (2026-08-23, the author: "palmiyelerin gövdesi
        // beraber hareket etmeli dalgalanma olmamalı"). The first take sliced the layer into
        // 22 bands and pushed each by its own height — a real bend, and wrong: every band
        // rounds its offset to a whole pixel, the bands cross their rounding thresholds at
        // different moments, and the trunk crawls like a snake instead of leaning. A trunk
        // is stiff. It goes over as ONE THING.
        //
        // So each layer is one sprite and the wind is a small ROTATION about its foot. The
        // trunk keeps its shape by construction — there is no seam left to ripple — and the
        // crown still travels furthest simply because it is furthest from the pivot.
        //
        // SIX LAYERS, SIX RHYTHMS (2026-08-23, the author again: "iki ağacın yaprakları da
        // bağımsız olarak hareket etmeli"). One sprite fixed the ripple and bought a second
        // wrong: both trees leaned in perfect lockstep, which is the one thing a pair of
        // trees never does. So the plate is cut — ONCE, offline, by
        // Tools/window_palms_split.py — into a pole and a crown for each tree and the ground
        // plants at each foot, and every piece gets its own speed and its own phase.
        //
        // A crown hangs off ITS OWN TRUNK in the hierarchy, so it inherits the trunk's lean
        // and adds its own turn about the junction on top. That is the whole trick: the
        // fronds have a life of their own without ever coming off the tree, and the trunk
        // below the junction never feels it.
        //
        // The pivots are art px on the WINDOW'S OWN 141×274 opening, y from the TOP, printed
        // by the split script — the ROOTS are off the opening on purpose, because the sill
        // cuts each trunk long before the ground does, and a tree that turns about its
        // visible foot swings like a hanged sign.
        //
        // THE PLATES ARE BIGGER THAN THE OPENING (2026-08-23, the author, having drawn the
        // crowns out whole by hand: "png de ekran dışında kaldığından kesiliyor yapraklar"). A
        // finished crown reaches past the opening on the far side of each tree, and a canvas
        // cut to the opening chops those fronds off in the FILE, where no amount of care in
        // the game can bring them back. So the split pads every layer evenly — the plate
        // stays centred on the opening, the art has room, and the room's own wall (order 10,
        // over all of these) hides whatever falls outside. Because the pad is even, an offset
        // measured in opening px is still measured from the plate's centre, and no number
        // here has to know how wide the pad is.
        private const float PalmLeanDegrees = 2.2f;    // the trunk, each way at full gust
        private const float PalmCrownDegrees = 3.0f;   // the fronds, on top of that
        private const float PlantLeanDegrees = 2.6f;   // the plants on the sill

        private sealed class WindLayer
        {
            public SpriteRenderer Sr;
            public Transform Pivot;      // the point this piece turns about
            public float Amplitude, Speed, Phase;
            public Vector2 PivotArt;     // that point, in plate px, y from the top
            public WindLayer Parent;     // the trunk a crown hangs off, null for a trunk
        }

        private readonly List<WindLayer> _wind = new List<WindLayer>();
        private Transform _windRoot;
        private Vector2 _palmPlate;      // the plate's own size, read off the art

        // ── the view out of the window, played on the shift's clock (2026-08-19) ────
        // A Miami skyline that runs from a golden sun through the pink band into deep
        // night, sliced from ONE sheet: Assets/Resources/Scene/window_cycle.png, built by
        // Tools/window_cycle.py out of six PixelLab animation sheets (one of them
        // generated brightening and played reversed — the chain is pinned there).
        //
        // Each cell is the room's window hole — its bounding box, carrying its alpha — so the
        // view is cut to the glass at build time and cannot slide off it at any aspect, and
        // the painted glazing bars split it into panes for free.
        //
        // The picture is stood in the opening's own PLANE rather than cropped to it: this
        // window is seen at an angle (183 art rows tall at its near edge, 120 at its far
        // one) and each frame is fitted to that trapezoid whole, by nearest sampling, so it
        // tilts with the wall without going soft. That work is all done at build time; at
        // runtime this is a plain sprite swap.
        //
        // The cell size is the hole's size, MEASURED (window_cycle.py build prints it beside
        // the centre below). The frame COUNT is deliberately not a constant — it is read off
        // the sheet, so re-generating with more frames needs no edit here.
        // RE-MEASURED for the 2026-08-22 room (the author: "mevcut ana sahne
        // arkaplanının camlarına gün batım animasyonumuzu ekleyelim"). The old room's hole
        // was 109x182; this one is 141x274 and leans harder — 274 rows at its near edge
        // against 141 at its far one. Every number here was PRINTED by the tool that cut the
        // sheet (Tools/window_cycle.py build), never typed: the cell IS the hole, so if these
        // two ever disagree the view slides off the glass.
        private const int WindowCellW = 141, WindowCellH = 274;
        /// <summary>The hole's centre in the room art's own bottom-left space.</summary>
        private static readonly Vector2 WindowCentreArtPx = new Vector2(70.5f, 212.0f);
        private Vector2 _backgroundNative;
        private float _backgroundScale = 1f;        // stage units per background-art pixel
        private Light2D _globalLight;
        private float _lastVisibleW = -1f;

        private System.Action _onTapClicked;
        private System.Action _onSinkClicked;

        /// <summary>The sink's click (2026-09-05): the HUD washes what is in the hand.</summary>
        public void SetSinkHandler(System.Action onClick) => _onSinkClicked = onClick;

        /// <summary>Wires the beer font on the counter to the draught station (2026-08-15).
        /// The stage owns WHERE the font stands, so it owns the hit plate; the HUD owns what
        /// clicking it means — the same split the till has had since the ledger landed.</summary>
        public void SetTapHandler(System.Action onClick) => _onTapClicked = onClick;

        /// <summary>The drain's own hit plate, for the HUD to test a dropped glass against
        /// (2026-08-26: a drink is poured away by CARRYING it to the sink). Null until the
        /// room owns a drain, which a run without fixtures never does.</summary>
        private RectTransform _drainDoor;

        /// <summary>The drain's own glow, kept so the room can call it forward.</summary>
        private HoverGlow _drainGlow;

        /// <summary>
        /// THE BASIN CALLS THE HAND (2026-09-06, the author: "bardak tutulduğunda lavaboya
        /// oyuncuyu yönlendirmeli ... bardak sürüklenirken lavabo ön plana çıkmalı"). While
        /// something is being carried that belongs in the sink, the sink lights and lifts
        /// exactly as it would under the pointer — same rise, same light, same coming to the
        /// front — so the room is telling the player where to let go rather than teaching
        /// them a second visual language for it.
        /// </summary>
        public void CallTheDrain(bool on) => _drainGlow?.Beckon(on);

        /// <summary>
        /// Whether the drain answers the pointer at all (2026-09-06). A running tap takes
        /// nothing — no glass, no tin, no click — so it stops lighting up under the hand as
        /// well: an affordance that is offered and then refused is worse than none.
        /// </summary>
        public void SetDrainAnswers(bool on)
        {
            if (_drainGlow != null && _drainGlow.enabled != on) _drainGlow.enabled = on;
            if (_drainDoor != null)
            {
                var hit = _drainDoor.GetComponent<UnityEngine.UI.Image>();
                if (hit != null && hit.raycastTarget != on) hit.raycastTarget = on;
            }
        }

        /// <summary>Is this screen point on the sink? Asked by the carry, once, on release.</summary>
        public bool PointerOverDrain(Vector2 screenPoint) =>
            _drainDoor != null && _drainDoor.gameObject.activeInHierarchy
            && (_propDoorGate == null || _propDoorGate.blocksRaycasts)
            && RectTransformUtility.RectangleContainsScreenPoint(_drainDoor, screenPoint, null);

        private System.Action<RectTransform, string> _onPropHover;

        /// <summary>Wires the room's props to the HUD's hover caption (2026-08-26). The stage
        /// knows WHERE a prop is and what it is called; the HUD owns the plate that says so.
        /// Told, not read, like the money and the slots before it.</summary>
        public void SetPropHoverHandler(System.Action<RectTransform, string> onHover) =>
            _onPropHover = onHover;

        private void Awake()
        {
            Application.runInBackground = true; // keep animations advancing unfocused
            FillTheWindow();
            _display = LanguageFonts.Display(displayFont != null                         // L3
                ? displayFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            BuildScene();
        }

        private void Update()
        {

            _ambientClock += Time.unscaledDeltaTime * AmbientScale;
            StepWater();
            StepClosing();
            StepDrawer();
            StepPalms();
            StepSky();
            SyncPatronFill();   // after the closing beat and the flicker have had their say
            float w = VisibleWidth();
            if (!Mathf.Approximately(w, _lastVisibleW)) Refit(w);
            // The room is built once at the reference and MAGNIFIED to cover the window.
            // One transform carries it, so the picture, the counter, the lamps, the fixtures
            // and their shadows all take the same scale by construction — there is no second
            // number for any of them to disagree with.
            float k = DesignFrame.SceneScale;
            if (_world != null && !Mathf.Approximately(k, _worldScale))
            {
                _worldScale = k;
                _world.localScale = new Vector3(k, k, 1f);
            }
        }

        private float _worldScale = -1f;

        /// <summary>
        /// Makes sure the camera fills the window rather than boxing itself inside it.
        ///
        /// Windowboxing it was the wrong half of the right idea (2026-08-11): it did stop
        /// the room from stretching, and it did it by drawing black bars, which the author
        /// rejected on sight — a wide monitor should get more bar, not a smaller one in a
        /// frame. So the camera keeps showing 360 × aspect and the ROOM fills whatever that
        /// is; what stopped moving instead is everything the player reads or touches, which
        /// now lives in a fixed safe frame (see <see cref="DesignFrame"/>).
        /// </summary>
        private static void FillTheWindow()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // The PixelPerfectCamera has to go, and it is worth saying why: it snaps the
            // camera to a WHOLE zoom, which means the height it shows jumps in steps as the
            // window is dragged — 360 stage units at one size, 526 at the next. Nothing that
            // scales smoothly can stay glued to something that jumps, and the author's
            // requirement is that nothing move at all. So the camera holds the reference
            // height exactly and the scene takes one continuous scale instead
            // (DesignFrame.SceneScale). Pixel art pays for that in the sizes where the scale
            // is not a whole number; a scene that comes apart when you drag a corner costs
            // more.
            var ppc = cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            if (ppc != null && ppc.enabled) ppc.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = Reference.y * 0.5f;
            // NO POST, NO HDR (2026-09-11, the author: "oyun çok düşük sistemlerde de
            // çalışmalı"). Both were switched on for the bloom LastCallVolume wires into the
            // pipeline — "only HDR light blooms", above 1.1. None ever does: 2D light
            // MULTIPLIES a sprite's own colour, and the room never lands a pixel past 1.1.
            // Measured by rendering one frame three ways inside one call — at the start of a
            // night, twice later in it, and at the closing beat at full strength (the guest's
            // lamp at 2.1x): 0 pixels differ with post-processing off, 0 with HDR off too. What
            // they did cost was GPU: the bloom chain and the HDR resolve are full-screen passes
            // at double-width pixels, a quarter to a third of the room's GPU frame on an
            // RTX 4070 — and memory bandwidth is what a cheap laptop's graphics lack most.
            // Bloom comes back with RoomBloom, and only means anything once a light is pushed
            // past 1.1.
            // THE GRADE (2026-09-21, the author: "belli tonlar belli filtrelerimiz olmalı"): post-processing
            // is back on for the one filter the room wears - LightLanguage's grade on LastCallVolume: lift toward
            // the club's blue, gain toward the amber, a little contrast, a vignette - and NOTHING else: HDR stays
            // off and the volume's bloom is inactive, so the cost is the uber pass alone, one LDR full-screen
            // blit at 1280x720 (measured, r79). RoomGrade is the one switch if a machine cannot pay it.
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = RoomGrade;
            cam.allowHDR = false;
        }

        /// <summary>Post-processing on the room's camera, for the grade alone — see FillTheWindow.</summary>
        private const bool RoomGrade = true;

        /// <summary>The width the room is BUILT at — the reference, always. The window is not
        /// the room's business any more: everything under the stage root is laid out at
        /// 640×360 and the root itself is magnified by <see cref="DesignFrame.SceneScale"/>,
        /// so the picture and everything standing in it grow by exactly the same amount and
        /// cannot come apart.</summary>
        private static float VisibleWidth() => Reference.x;

        /// <summary>Stage units (bottom-left origin) → world position. One world unit is one
        /// stage unit; the camera sits over the stage's centre.</summary>
        private static Vector3 StageToWorld(float x, float y) =>
            new Vector3(x - Reference.x * 0.5f, y - Reference.y * 0.5f, 0f);

        // ── scene construction ──────────────────────────────────────────────────

        private void BuildScene()
        {
            _world = new GameObject("StageWorld").transform;
            _world.position = Vector3.zero;

            // The one material the whole stage shares. Sprites-Default is unlit — under the
            // 2D renderer an unlit sprite ignores every Light2D, which would make the whole
            // migration a very quiet no-op.
            var litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (litShader != null) _litMaterial = new Material(litShader);
            else Debug.LogWarning("DiegeticStage: Sprite-Lit-Default not found — the stage will draw unlit.");
            // THE OUTSIDE IS NOT LIT BY THE INSIDE (2026-09-17). The view through the glass,
            // the palms in front of it and the pane's own sheen used to share the lit
            // material, so the room's lamps multiplied the sky — a sunset at 45% of itself
            // under the ambient, then blown back up by the very lights it was supposed to be
            // driving. The sky is the light source; it draws at the colour it was made in.
            var viewShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                             ?? Shader.Find("Sprites/Default");
            if (viewShader != null) _viewMaterial = new Material(viewShader);

            // Opaque backdrop behind everything, overscanned past the screen edges so no
            // aspect-ratio border ever exposes the clear colour / editor checker.
            _backdropSr = WorldSprite("Backdrop",
                Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f),
                order: 0);
            _backdropSr.color = UITheme.Night[0];


            // The room. Real club background when installed, else the flat procedural
            // placeholders on their own canvas, so a broken reference is still a visible bar.
            if (backgroundSprite != null)
            {
                // THE VIEW, BEHIND THE ROOM (14 v3 §5's layer order, §7's plate). The shell's
                // window panes are keyed to transparent holes, so without this the backdrop
                // shows through them and the bar looks out on Night[0] — black rectangles
                // where daylight belongs (measured in play, 2026-08-18).
                //
                // Loaded by NAME rather than serialized, because what hangs here changes with
                // the shift and one inspector slot cannot hold a whole evening. It is an
                // ANIMATION now (2026-08-19): a Miami skyline that goes from a low sun to lit
                // windows across the night, sliced from one sheet and stepped by the clock in
                // SetSkyFraction. The single day plate stays as the fallback, so a missing or
                // half-built sheet still leaves daylight in the glass rather than black holes.
                // ...AND SINCE 2026-09-17 IT IS NOT A SHEET AT ALL: the view is DRAWN, by
                // WindowSky, from the hour — a sky of palette bands, a sun that sinks behind
                // the author's own skyline, windows that come on one by one (see SkyClock).
                // The still day plate stays as the fallback for a build with no model.
                _skyClock = SkyClock.Load();
                _sky = WindowSky.Load(_skyClock);
                var windowPlate = _sky != null ? _sky.Sprite : Resources.Load<Sprite>("Scene/window_day");
                if (windowPlate != null)
                {
                    _windowSr = WorldSprite("WindowPlate", windowPlate, order: 5);
                    if (_viewMaterial != null) _windowSr.sharedMaterial = _viewMaterial;
                    if (_sky != null)
                    {
                        var outside = _viewMaterial != null ? _viewMaterial : _litMaterial;
                        _flock = new WindowSky.Flock(_skyClock.Spec.birds, _windowSr.transform,
                                                     outside, LayerBackground, 5);
                        _sky.HangMoon(_windowSr.transform, outside, LayerBackground, 5);
                    }
                }

                _backgroundSr = WorldSprite("Background", backgroundSprite, order: 10);
                _backgroundNative = backgroundSprite.rect.size;

                // THE PANE ITSELF, over the room and under everything standing in it (the
                // counter is 30). A window is TWO things: the view stands flat behind the
                // wall and shows through the keyed panes, and this is the sheet of glass in
                // front of it. Without it the opening reads as a hole in a wall rather than
                // something glazed (the author, 2026-08-22). Cut to the room's own mask by
                // the same tool that cuts the animation, so the sheen cannot sit a pixel off
                // the glass — Tools/window_cycle.py glass.
                var glass = Resources.Load<Sprite>("Scene/window_glass");
                if (glass != null)
                {
                    _glassSr = WorldSprite("WindowGlass", glass, order: 11);
                    if (_viewMaterial != null) _glassSr.sharedMaterial = _viewMaterial;
                }
                BuildPalms();

                // (The wall lamps are FIXTURES now — SyncFixtures stands them, because
                // they have levels and levels are the market's business, not the stage's.)

                // THE SUN, standing where the window is. It is one light and not a shaped
                // one: what sells daylight through glass is the DIRECTION it falls from and
                // the colour it carries, and both of those are already true of a point hung
                // in the window's own opening. Its colour and its strength are the glass's
                // to decide, every frame — see ApplyDaylight.
                _windowLight = PointLight("WindowLight", LampTint, 0f, WindowRadius);
                // THE SKY'S OWN GLOW (2026-08-24, the author: "gök yüzü daha çok ortama renk
                // vermeli ... o miami vice hissini ışıklandırmayla vermeliyiz"). The sun's cone
                // is a shaft; this is the AIR. A broad soft pool hung at the glass, coloured
                // by the sky's pink band, so the room is pink where it faces the evening and
                // falls to violet where it does not.
                _skyGlow = PointLight("SkyGlow", GlobalTint, 0f, SkyGlowRadius);
                _skyGlow.falloffIntensity = 0.86f;
                LightLayers(_skyGlow, LayerBackground, LayerPatrons);
                // ...and the same pool on the bar top, quieter (CounterGlowShare): one light
                // cannot give two layers two strengths, so the counter has its own.
                _counterGlow = PointLight("SkyGlowCounter", GlobalTint, 0f, SkyGlowRadius);
                _counterGlow.falloffIntensity = 0.86f;
                LightLayers(_counterGlow, LayerCounter);
                // A throw, not a bulb: see WindowSetback / WindowAimDegrees above.
                LightLayers(_windowLight, LayerBackground, LayerPatrons);
                _windowLight.pointLightInnerRadius = WindowRadius * WindowInner;
                _windowLight.pointLightOuterAngle = WindowConeOuter;
                _windowLight.pointLightInnerAngle = WindowConeInner;
                _windowLight.transform.rotation = Quaternion.Euler(0f, 0f, WindowAimDegrees);
                // THE SHAFT ON THE WALL (2026-09-17, the author: "gün batımı içerisinin
                // rengini ve tonunu değiştirmeli"). A cone from the glass lights the room
                // evenly and says nothing about the hour. What says it is the PATCH of sun
                // on the plaster — four panes of it, lying low and near the window at
                // opening, sliding right and climbing the wall as the sun drops, going from
                // cream to amber to red, and gone when the disc is. A sprite light wearing
                // that shape; ApplyDaylight moves and colours it.
                _sunShaft = new GameObject("SunShaft").AddComponent<Light2D>();
                _sunShaft.transform.SetParent(_world, false);
                _sunShaft.lightType = Light2D.LightType.Sprite;
                _sunShaft.lightCookieSprite = SunShaftCookie();
                _sunShaft.color = LampTint;
                _sunShaft.intensity = 0f;
                // NOT THE DRINKERS (2026-09-21, the author: "müşterilerin üstünde olmamalı"): the patch lands on the
                // room and the counter; the people standing in it take the window's cone and the fill, not the panes.
                LightLayers(_sunShaft, LayerBackground, LayerCounter);
                _sunFloor = new GameObject("SunFloor").AddComponent<Light2D>();
                _sunFloor.transform.SetParent(_world, false);
                _sunFloor.lightType = Light2D.LightType.Sprite;
                _sunFloor.lightCookieSprite = SunFloorCookie();
                _sunFloor.color = LampTint;
                _sunFloor.intensity = 0f;
                LightLayers(_sunFloor, LayerBackground, LayerCounter);
            }
            else
            {
                BuildFallbackRoom();
            }

            // The global wash: what "the room is dim" means to the lighting system. Slightly
            // cool and slightly below 1, so the warm pools have something to be warmer than.
            // ONE GLOBAL LIGHT PER SORTING LAYER is URP 2D's law ("More than one global
            // light on layer ..." is an error it logs, and the PlayMode suite fails a test
            // on any error log — all thirteen went red on the first run, 2026-09-17). So
            // the room's wash reaches every layer EXCEPT the drinkers', and the drinkers'
            // own global (below) carries the wash for them plus their lift. Both are
            // configured before they are enabled: a Light2D registers itself on enable
            // with whatever layers it has at that moment, and a component born on the
            // Default layer next to one that already covers it logs the error right then.
            _globalLight = GlobalLight("GlobalLight", GlobalTint, GlobalIntensity,
                                       AllLayersExcept(LayerPatrons));
            // THE PEOPLE GET THEIR OWN STOP (2026-09-17, the author: "müşterilerin arkaplandan
            // ayrılması, sıyrılması"). Every light in the room lands on the drinkers and on
            // the wall behind them alike, so at any hour a figure was exactly as bright as the
            // plaster it stood against. A global light that reaches ONLY the Patrons layer
            // lifts them a stop above the wall whatever the sky is doing — the oldest rule of
            // lighting a subject: the key on the person, the background a stop under.
            // It CARRIES the wash for that layer too (see SyncPatronFill): what the wall
            // gets from GlobalLight, the drinkers get from this one, and the lift on top.
            _patronFill = GlobalLight("PatronFill", GlobalTint, GlobalIntensity + PatronFillDay,
                                      new[] { LayerPatrons });

            // The bar. A drawn asset: it carries EMPTY shelves, which is not decoration but
            // structure — glassware is a buyable upgrade and those shelves are where the
            // bought glasses get drawn. There is no procedural counter to fall back on, so a
            // lost reference is an invisible bar and no other symptom (2026-07-29).
            if (counterSprite == null)
                Debug.LogWarning("DiegeticStage: no counterSprite — the bar will not be drawn. " +
                                 "Check the reference in the scene, or re-run LastCall > Create Debug Scene.");
            if (counterSprite != null)
            {
                var sr = WorldSprite("Counter", counterSprite, order: 30);
                _counterTr = sr.transform;
                _counterSr = sr;
                _counterNative = counterSprite.rect.size;
                _counterDrawWidth = SetUpCounterTiling(sr);
                // Built HERE and not up with the room's lights, because they are the BAR's:
                // the slab, the stock standing on it and the drinkers leaning over it. The
                // one they replace was hung with the window and inherited its branch, so a
                // room with no sky sheet had no light over its counter either.
                BuildBarLights();
                // ...and the cellar's, off the same art: the shelves are a room of their own
                // once the roller is up, and nothing over the bar reaches into them.
                BuildCellarLights();
            }
            // Order 33: over the counter's cabinet (30) AND over the stock standing in it
            // (31), and under anything on the bar top (35). The roller has to hide the
            // bottles or it is not shutting anything.
            if (shutterSprite != null)
            {
                var sh = WorldSprite("Shutter", shutterSprite, order: 33);
                _shutterTr = sh.transform;
                _shutterSr = sh;
                _shutterNative = shutterSprite.rect.size;
                BuildShutterDoor();
            }

            Refit(VisibleWidth());

            // The bar opens at the hour the window says it is. Without this the room stood
            // at the old constants until the clock happened to step a frame — which is a
            // whole second and a half of the wrong evening, right at the moment the player
            // is looking hardest.
            SetSkyFraction(_tau);

            if (!Motion.Reduced) StartCoroutine(Ambient());
        }

        /// <summary>
        /// A world-space sprite on the stage, for something the HUD owns but the ROOM has to
        /// light — the seated drinkers (2026-08-10). They were UI Images on an overlay canvas,
        /// which no Light2D can reach, so a lamp bought for the corner lit the wall behind a
        /// customer and not the customer. The caller keeps driving it; all this hands out is a
        /// renderer already parented to the stage and already on the lit material.
        ///
        /// Sorting orders are the stage's own ledger: room 10, wall dressing 20, DRINKERS 25,
        /// the bar 30, whatever stands on the bar 35. A drinker at 25 is drawn over by the
        /// counter, which is how the bar takes their legs — the honest version of the mask the
        /// canvas needed.
        /// </summary>
        public SpriteRenderer NewStageSprite(string name, int order, bool castsShadow = false)
        {
            var sr = WorldSprite(name, null, order);
            // A figure throws a shadow on the wall behind it (2026-09-17): a dark copy one
            // sorting step under the body, off the hour's light — see CastShadow.
            if (castsShadow) CastShadow.Attach(sr);
            return sr;
        }

        // THE STAGE IS THREE LAYERS, AND THE LIGHT KNOWS IT (2026-08-24, the author:
        // "Arkaplan, müşteriler ve tezgah 3 ayrı katmanda ... bunu düşünmek mantıklı olur
        // mu?"). It is, and URP 2D can act on it: every stage sprite lands on one of three
        // sorting layers by the order band it already lives in, and every light targets
        // the layers it is FOR. One sorting layer meant the bar's lamp painted the wall
        // and the window's cone spent itself on a counter it should barely graze.
        public const string LayerBackground = "Stage Background";   // orders 0..21
        public const string LayerPatrons = "Patrons";               // orders 22..29
        public const string LayerCounter = "Bar Counter";           // orders 30+

        private static string LayerForOrder(int order) =>
            order < 22 ? LayerBackground : order < 30 ? LayerPatrons : LayerCounter;

        /// <summary>One world-space stage sprite on the shared lit material, standing on
        /// the sorting layer its order band belongs to.</summary>
        /// <summary>Where an overlay backdrop sorts: over the plate (10) and the window's
        /// glass (11), under the hangers (15). See SyncFixtures.</summary>
        private const int OverlayOrder = 12;

        private SpriteRenderer WorldSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = LayerForOrder(order);
            sr.sortingOrder = order;
            if (_litMaterial != null) sr.sharedMaterial = _litMaterial;
            return sr;
        }

        private Light2D PointLight(string name, Color tint, float intensity, float radius)
        {
            var l = new GameObject(name).AddComponent<Light2D>();
            l.transform.SetParent(_world, false);
            l.lightType = Light2D.LightType.Point;
            l.color = tint;
            l.intensity = intensity;
            l.pointLightInnerRadius = radius * 0.15f;
            l.pointLightOuterRadius = radius;
            l.falloffIntensity = 0.62f;
            LightAllLayers(l);
            return l;
        }

        /// <summary>
        /// Points a light at the layers it is FOR. The sky lights the wall and the people
        /// and dies before the counter; the bar's own lamp pools on the counter and the
        /// hands over it without painting the wall behind; only the global wash and the
        /// closing beat speak to the whole room.
        /// </summary>
        private static void LightLayers(Light2D light, params string[] names)
        {
            var f = typeof(Light2D).GetField("m_ApplyToSortingLayers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null) return;
            var ids = new int[names.Length];
            for (int i = 0; i < names.Length; i++) ids[i] = SortingLayer.NameToID(names[i]);
            f.SetValue(light, ids);
        }

        /// <summary>
        /// A CONTACT SHADOW under something standing on the floor — a soft dark ellipse at
        /// its feet, the trick that already sells the till as resting on the bar and the
        /// rack glasses as standing in their bays.
        ///
        /// This replaces a real ShadowCaster2D, and the reason is measured rather than
        /// assumed (2026-08-10): with the room frozen, switching every point light's
        /// shadows on changed ZERO pixels. URP's 2D shadows cast radially from the light,
        /// and in a head-on composition where every caster is flat at the same depth and
        /// every lamp hangs above, the shadows all fall below the counter — behind the HUD,
        /// where nothing can see them. The technique that reads at this camera is the one
        /// the game already uses, so this is it, applied to the dressing.
        /// </summary>
        private void ContactShadow(Transform under, float width)
        {
            var art = ItemArt.Load("shadow_soft");
            var go = new GameObject(under.name + "_Shadow");
            go.transform.SetParent(_world, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art;
            // OVER THE COUNTER (31), not under the dressing. The floor slots sit at the bar's
            // own rest line, so a blob at 19 was drawn and then covered by the counter at 30 —
            // a shadow you cannot see is not a shadow. 31 puts it on the bar top where the
            // thing casting it is standing, still under anything that stands ON the bar (35).
            sr.sortingLayerName = LayerForOrder(31);
            sr.sortingOrder = 31;
            sr.color = new Color(0f, 0f, 0f, art != null ? 0.38f : 0f);
            if (_litMaterial != null) sr.sharedMaterial = _litMaterial;
            _shadows.Add((under, sr, width));
        }

        private readonly List<(Transform Under, SpriteRenderer Blob, float Width)> _shadows =
            new List<(Transform, SpriteRenderer, float)>();

        /// <summary>
        /// Light2D ships targeting whatever sorting layers its serialized default says, and
        /// that default is not a public API — so the target list is set outright to every
        /// layer the project has. The UI never suffers for it: overlay canvases are composited
        /// after the camera and no 2D light can reach them at all.
        /// </summary>
        private static void LightAllLayers(Light2D light)
        {
            var f = typeof(Light2D).GetField("m_ApplyToSortingLayers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null) return;
            var layers = SortingLayer.layers;
            var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
            f.SetValue(light, ids);
        }

        /// <summary>Every sorting layer the project has but one, by name.</summary>
        private static string[] AllLayersExcept(string name)
        {
            var layers = SortingLayer.layers;
            var names = new List<string>();
            foreach (var l in layers) if (l.name != name) names.Add(l.name);
            return names.ToArray();
        }

        /// <summary>
        /// A global light aimed at exactly these layers, configured BEFORE it is enabled:
        /// URP 2D allows one global light per sorting layer per blend style, checks it the
        /// moment a light registers, and a fresh Light2D registers on the Default layer.
        /// </summary>
        private Light2D GlobalLight(string name, Color tint, float intensity, string[] layers)
        {
            // Born ACTIVE and as the point light a fresh Light2D is, then aimed, then made
            // global — in that order. The duplicate check runs the moment the type becomes
            // Global and reads the layers the light has right then; and it runs on an
            // inactive light too, where it dereferences state OnEnable has not built yet
            // (NullReferenceException in Light2DManager.ErrorIfDuplicateGlobalLight, every
            // PlayMode test, 2026-09-17 — the inactive-then-enable order was tried first).
            var l = new GameObject(name).AddComponent<Light2D>();
            l.transform.SetParent(_world, false);
            LightLayers(l, layers);
            l.lightType = Light2D.LightType.Global;
            l.color = tint;
            l.intensity = intensity;
            return l;
        }

        /// <summary>The lift on the drinkers, kept in step with the wall's wash every frame
        /// (the closing beat and the ambient flicker both write the wash), so the people
        /// are always exactly one lift over the room they stand in — never under it.</summary>
        private void SyncPatronFill()
        {
            if (_patronFill == null || _globalLight == null) return;
            float lift = Mathf.Lerp(PatronFillDay, PatronFillNight, _now.Dusk);
            _patronFill.intensity = _globalLight.intensity + lift;
            _patronFill.color = Color.Lerp(_globalLight.color, PatronFillTint, 0.5f);
        }

        /// <summary>
        /// Everything whose size depends on the window: the backdrop, the room's cover fit,
        /// the counter's width fit, and the lights that hang off the room's own pixels.
        /// Scaled by ONE factor each, so every art pixel stays square (the StageArtFit rule,
        /// now in world units).
        /// </summary>
        private void Refit(float visibleW)
        {
            _lastVisibleW = visibleW;
            float visibleH = Reference.y;

            if (_backdropSr != null)
            {
                var b = _backdropSr.sprite.bounds.size;
                _backdropSr.transform.localScale = new Vector3(
                    (visibleW + Overscan * 2f) / b.x, (visibleH + Overscan * 2f) / b.y, 1f);
            }

            if (_windowSr != null && _sky == null)
            {
                // THE SAME COVER FIT AS THE ROOM, deliberately: the still plate is authored at
                // the room's own 640x360 with the sky sitting in the room's own hole, so any
                // fit that is not identical slides the daylight off the window. The animated
                // frames are not plates and are fitted after the room instead — see below.
                var wb = _windowSr.sprite.bounds.size;
                float wk = Mathf.Max(visibleW / wb.x, visibleH / wb.y);
                _windowSr.transform.localScale = new Vector3(wk, wk, 1f);
            }

            if (_backgroundSr != null)
            {
                // Cover: the larger ratio fills both axes, overflow is cropped by the frame.
                var b = _backgroundSr.sprite.bounds.size;
                float k = Mathf.Max(visibleW / b.x, visibleH / b.y);
                _backgroundSr.transform.localScale = new Vector3(k, k, 1f);
                _backgroundScale = k * (b.x / _backgroundNative.x);   // stage units per art px

                // The animated view is a CUT-OUT of the room's own window hole, not a plate:
                // it is the hole's bounding box and carries the hole's alpha. So it rides the
                // room's own art scale and stands at the hole's own centre — which is why it
                // is fitted here, after _backgroundScale exists, and not in the block above.
                if (_windowSr != null && _sky != null)
                {
                    _windowSr.transform.localScale =
                        new Vector3(_backgroundScale, _backgroundScale, 1f);
                    _windowSr.transform.position = StageArtPointToWorld(WindowCentreArtPx);
                    if (_glassSr != null)
                    {
                        _glassSr.transform.position = _windowSr.transform.position;
                        _glassSr.transform.localScale = _windowSr.transform.localScale;
                    }
                    // The sun stands OUTSIDE the opening and is re-hung with it: the window
                    // moves with the room's fit, so a light left at a build-time position
                    // would slide off the glass the moment the window is not 16:9. The
                    // setback is in ART px and goes through the same fit, so the source keeps
                    // its distance behind the glass at every aspect rather than drifting into
                    // the room as the picture grows.
                    if (_windowLight != null)
                        _windowLight.transform.position = StageArtPointToWorld(
                            new Vector2(WindowCentreArtPx.x - WindowSetback, WindowCentreArtPx.y));
                    if (_skyGlow != null)
                        _skyGlow.transform.position = _windowSr.transform.position
                            + new Vector3(30f * _backgroundScale, 0f, 0f);
                    if (_counterGlow != null && _skyGlow != null)
                        _counterGlow.transform.position = _skyGlow.transform.position;
                }

                // (The wall lamps ride the fixture path — PlaceFixtures re-hangs them
                // with everything else the bar owns.)
            }

            if (_counterTr != null)
            {
                // THE BAR GROWS BY REPEATING, NOT BY STRETCHING (2026-08-19, the author:
                // "ekrandaki tezgahı unity'nin özelliğiyle sağa ve sola doğru genişlet ...
                // kenarlara uzattıkça sündüren değil görüntüyü üreten metodla").
                //
                // It used to span the window with a UNIFORM SCALE - k = visibleW / artW -
                // which on a 16:9 window is exactly 1 and invisible, and on anything wider
                // is the smear the author is looking at: the counter grows sideways AND
                // upward, its pixels stop being square with the room's, and everything the
                // stage stands on it (the till, the taps, the rest line) drifts with it.
                //
                // Unity's own answer is SpriteDrawMode.Tiled: the sprite's 9-slice border
                // draws at 1:1 and the CENTRE repeats to fill whatever width is asked for.
                // The border is set on import (PatronArtPostprocessor) at the drawing's own
                // cabinet dividers, so a wider window buys more cabinet run. Scale stays 1,
                // which means one art pixel is one stage unit for good - the counter can no
                // longer drift against the room whatever shape the window is.
                var sr = _counterTr.GetComponent<SpriteRenderer>();
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                // Rounded UP to a whole art pixel, and never shorter than the drawing:
                // at exactly 16:9 that is 640 and the caps land pixel-for-pixel where they
                // were drawn, while a wider window is covered without a hairline gap.
                sr.size = new Vector2(
                    Mathf.Max(_counterNative.x, Mathf.Ceil(visibleW)), _counterNative.y);
                _counterTr.localScale = Vector3.one;
                _counterScale = 1f;                            // stage units per art px
                // Hung from the rest line: the art's top is CounterSurfaceInset above it.
                float artTopStage = CounterRestY + CounterSurfaceInset * _counterScale;
                float artHStage = _counterNative.y * _counterScale;
                _counterTr.position = new Vector3(0f, artTopStage - artHStage * 0.5f - Reference.y * 0.5f, 0f);
                if (_shutterTr != null)
                {
                    // Laid out SHUT, then the drawer's own offset is re-applied on top, so a
                    // window resize mid-open does not slam the roller back into the sill.
                    // NOT tiled, unlike the counter: the roller carries one pink arrow at its
                    // top centre and a repeat would draw a row of them. It stays at its drawn
                    // 592 against the counter's 638, which leaves the blue side posts showing
                    // when shut — the author drew it that way and it reads as deliberate.
                    float counterTop = artTopStage - Reference.y * 0.5f;
                    _shutterTr.position = new Vector3(
                        0f, counterTop - ShutterOpeningTopPx - _shutterNative.y * 0.5f, 0f);
                    _shutterRestLocalY = _shutterTr.localPosition.y;
                    ApplyDrawer();
                }
            }

            PositionBarLights(visibleW);
            PlaceCellarLights();
            PlaceFixtures();
        }

        /// <summary>
        /// THE ROW OVER THE BAR AND THE ROW ALONG ITS EDGE.
        ///
        /// Both are pointed at the counter and the patrons and at nothing else. That is not
        /// tidiness: a lamp hung this close to the back wall in a head-on 2D room IS a lamp
        /// on the back wall, and the three failed hangings this replaces were all the same
        /// fight against that. Aimed at two sorting layers it cannot happen, so the lamps are
        /// free to hang where a bar's lamps actually hang.
        ///
        /// They are born at their NIGHT level rather than at zero. ApplyDaylight owns them
        /// from the first frame the window has a sheet to read, and a room without one used
        /// to leave the old lamp dark for ever, which reads as a bug in the art.
        /// </summary>
        private void BuildBarLights()
        {
            for (int i = 0; i < BarLightCount; i++)
            {
                var l = PointLight($"BarDownlight{i}", LampTint, BarLightNight, BarLightRadius);
                l.pointLightInnerRadius = BarLightRadius * BarLightInner;
                LightLayers(l, LayerCounter, LayerPatrons);
                _barLights.Add(l);
            }
            for (int i = 0; i < BarNeonCount; i++)
            {
                var l = PointLight($"BarNeon{i}", BarNeonTint, BarNeonNight, BarNeonRadius);
                LightLayers(l, LayerCounter, LayerPatrons);
                _barNeons.Add(l);
            }
        }

        /// <summary>
        /// Hangs both rows, in stage units, so neither can drift from the counter it is
        /// lighting or from the drinkers standing at it.
        ///
        /// The lamps go over the STOOLS, at the stools' own pitch. The neon spreads over the
        /// width the bar is actually DRAWN at instead: the slab is 9-sliced out to fill the
        /// frame, its tube goes out with it, and a run measured against the reference would
        /// stop short of the ends of its own strip on a wider window.
        /// </summary>
        private void PositionBarLights(float visibleW)
        {
            for (int i = 0; i < _barLights.Count; i++)
                if (_barLights[i] != null)
                    _barLights[i].transform.position =
                        StageToWorld(BarLightFirstX + i * BarLightPitchX, BarLightY);

            float step = visibleW / Mathf.Max(1, _barNeons.Count);
            for (int i = 0; i < _barNeons.Count; i++)
                if (_barNeons[i] != null)
                    // Half a step in from each end, which is what puts the outer segments ON
                    // the tube rather than hanging off the ends of it.
                    _barNeons[i].transform.position = new Vector3(
                        (i + 0.5f) * step - visibleW * 0.5f,
                        BarNeonY - Reference.y * 0.5f, 0f);
        }

        /// <summary>
        /// The palms, sliced into horizontal bands so the wind can BEND them rather than
        /// swing them rigidly. One texture, many renderers: each band shows its own strip of
        /// it and is pushed sideways on its own, and the push grows with height.
        /// </summary>
        private void BuildPalms()
        {
            _windRoot = new GameObject("WindRoot").transform;
            _windRoot.SetParent(_world, false);

            // The far tree is drawn first, then the near one, then the plants on the sill —
            // which is the order the split already assumes, since it is the LEFT crown that
            // overlaps the right one. Orders 6–8 all sit over the view (5) and under the room
            // (10), so the room's own frame and glazing bars cut every layer with one drawing
            // and not one of them needs a mask.
            var rTrunk = WindPiece("window_palm_r", new Vector2(153.3f, 274f), 6,
                                   PalmLeanDegrees, 0.74f, 1.9f, null);
            WindPiece("window_palm_r_crown", new Vector2(113f, 94f), 6,
                      PalmCrownDegrees, 1.31f, 0.6f, rTrunk);
            var lTrunk = WindPiece("window_palm_l", new Vector2(-5.4f, 274f), 7,
                                   PalmLeanDegrees, 0.90f, 0f, null);
            WindPiece("window_palm_l_crown", new Vector2(35f, 69f), 7,
                      PalmCrownDegrees, 1.53f, 2.4f, lTrunk);
            WindPiece("window_plants_l", new Vector2(22f, 274f), 8,
                      PlantLeanDegrees, 1.10f, 1.2f, null);
            WindPiece("window_plants_r", new Vector2(124f, 274f), 8,
                      PlantLeanDegrees, 1.22f, 3.1f, null);
        }

        /// <summary>
        /// One piece of the window's greenery, hung on a pivot of its own. <paramref
        /// name="parent"/> is the trunk a crown belongs to — passing it is what keeps the
        /// fronds on the tree while they move by themselves.
        /// </summary>
        private WindLayer WindPiece(string res, Vector2 pivotArt, int order,
                                    float amplitude, float speed, float phase,
                                    WindLayer parent)
        {
            var sprite = Resources.Load<Sprite>("Scene/" + res);
            if (sprite == null) return null;
            var plate = new Vector2(sprite.texture.width, sprite.texture.height);
            if (_palmPlate != Vector2.zero && plate != _palmPlate)
                Debug.LogWarning($"[Stage] {res} is {plate} but the other window layers are "
                                 + $"{_palmPlate}. They all hang at the opening's centre, so a "
                                 + "layer on a different canvas lands somewhere else.");
            _palmPlate = plate;

            var pivot = new GameObject(res).transform;
            pivot.SetParent(parent != null ? parent.Pivot : _windRoot, false);
            var sr = WorldSprite(res + "Art", sprite, order);
            if (_viewMaterial != null) sr.sharedMaterial = _viewMaterial;   // outside the glass: unlit
            sr.transform.SetParent(pivot, false);

            var layer = new WindLayer
            {
                Sr = sr,
                Pivot = pivot,
                Amplitude = amplitude,
                Speed = speed,
                Phase = phase,
                PivotArt = pivotArt,
                Parent = parent,
            };
            _wind.Add(layer);
            return layer;
        }

        /// <summary>
        /// One frame of wind. Every layer leans a couple of degrees about its own pivot on
        /// its own clock, and a crown leans about ITS TRUNK'S junction after that trunk has
        /// already leaned — so the fronds travel further than the pole carrying them without
        /// ever leaving it.
        /// </summary>
        private void StepPalms()
        {
            if (_windRoot == null || _windowSr == null || _wind.Count == 0) return;

            // Every layer keeps the plate's own canvas, so the whole set is hung at the
            // view's centre off the view's own transform rather than measured twice — they
            // cannot come apart, whatever the drawer does to the stage under them.
            _windRoot.position = _windowSr.transform.position;
            float s = _windowSr.transform.localScale.x;
            float t = _ambientClock;
            float k = Motion.Reduced ? 0f : 1f;

            for (int i = 0; i < _wind.Count; i++)
            {
                var L = _wind[i];
                if (L == null || L.Sr == null) continue;

                // The pivot stands at its art point; the sprite is pushed back by the same
                // offset, which lands the plate exactly over the view again. Turning the
                // pivot then turns the plate about that art point and nothing else.
                //
                // A crown is measured from ITS TRUNK'S pivot, not from the plate's centre —
                // the difference of two offsets, never the offset of a difference. Handing
                // ArtOffset a delta re-applies the centring and throws the crown half a
                // plate off its own tree, which is exactly what it did once.
                L.Pivot.localPosition = L.Parent == null
                    ? ArtOffset(L.PivotArt, s)
                    : ArtOffset(L.PivotArt, s) - ArtOffset(L.Parent.PivotArt, s);
                L.Sr.transform.localPosition = -ArtOffset(L.PivotArt, s);
                L.Sr.transform.localScale = new Vector3(s, s, 1f);

                // Two waves at rates that do not divide, so no layer ever repeats a beat and
                // no two layers fall into step with one another.
                float w = L.Speed * t + L.Phase;
                float wave = Mathf.Sin(w) * 0.7f + Mathf.Sin(w * 1.7f + 1.3f) * 0.3f;
                L.Pivot.localRotation = Quaternion.Euler(0f, 0f, L.Amplitude * wave * k);
            }
        }

        /// <summary>
        /// Window-opening px (y from the TOP) → an offset from the plate's centre, in the
        /// stage's own units at the view's scale. The two centres are the same point because
        /// the plates are padded evenly, which is why the pad appears in no number here.
        /// </summary>
        private Vector3 ArtOffset(Vector2 openingPx, float scale) => new Vector3(
            (openingPx.x - WindowCellW * 0.5f) * scale,
            (WindowCellH * 0.5f - openingPx.y) * scale, 0f);

        // ── the bought dressing ─────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the room's dressing to match what the run says the bar owns. Called by
        /// the HUD whenever the owned set changes (a buy, a refund, a new run) — the stage
        /// itself never reads the run, it is told, the same way the till is told the money.
        /// Each piece is a world sprite in the fixture's own slot; a piece whose definition
        /// shines gets a Light2D of its own, hung at the sprite's glow line.
        /// </summary>
        public void SyncFixtures(IReadOnlyList<LastCall.Core.FixtureDefinition> owned)
        {
            foreach (var placed in _placedFixtures)
            {
                if (placed.Body != null) Destroy(placed.Body.gameObject);
                if (placed.Glow != null) Destroy(placed.Glow.gameObject);
            }
            _placedFixtures.Clear();
            _houseLights.Clear();
            foreach (var door in _tapDoors) if (door != null) Destroy(door.gameObject);
            _tapDoors.Clear();
            _drainDoor = null;
            _waterSr = null;
            _waterFrames = null;
            // Re-dressing the room destroys the set along with everything else, so the
            // player must be stopped and its handles dropped — a coroutine left running
            // against a destroyed renderer is the shape of bug this file already carries a
            // warning about for the cellar (see PlaceFixtures below).
            if (_tvPlayer != null) { StopCoroutine(_tvPlayer); _tvPlayer = null; }
            _tvSr = null; _tvGlow = null; _tvFrames = null; _tvCols = 0;
            foreach (var sh in _shadows) if (sh.Blob != null) Destroy(sh.Blob.gameObject);
            _shadows.Clear();
            if (owned == null || _world == null) return;

            foreach (var def in owned)
            {
                if (!_slots.ContainsKey(def.Slot))
                {
                    Debug.LogWarning($"DiegeticStage: fixture '{def.Id}' wants unknown slot " +
                                     $"'{def.Slot}' — the stage was never handed it.");
                    continue;
                }
                // A carried piece's drawing lives with the tools in Items, not on the room's
                // own shelf — and it is not stood here anyway, so a missing plate is no
                // reason to skip the rest of the loop's bookkeeping.
                var sprite = Resources.Load<Sprite>("Fixtures/" + def.Sprite);
                if (sprite == null && _slots.TryGetValue(def.Slot, out var carriedSlot)
                    && carriedSlot.Carried) continue;
                if (sprite == null)
                {
                    Debug.LogWarning($"DiegeticStage: fixture '{def.Id}' has no sprite " +
                                     $"'Fixtures/{def.Sprite}' — sold but not drawn.");
                    continue;
                }
                // Room dressing draws between the picture (10) and the bar (30); a piece
                // standing ON the counter must draw over the counter that holds it — the
                // candle was sorting behind the bar top and simply vanished (measured on
                // the first proof shot, 2026-08-10). A piece that HANGS draws behind the
                // floor dressing (15): the triptych and the middle table overlap by a few
                // rows, and two sprites sharing order 20 leave which one wins to chance —
                // a picture ON the wall is behind a table in FRONT of the wall, always
                // (2026-08-24). The slot says which it is, so a new counter-top or wall
                // place needs no code either.
                // A MAT DRAWS UNDER WHAT STANDS ON IT (2026-08-25, the rug and the drip
                // mat). Both share a surface with dressing that is already there — the
                // tables stand on the boards, the beer font stands on the bar — and two
                // sprites on one order leave which one wins to chance, which is the exact
                // trap the triptych's own band was carved out of. So a flat piece drops one
                // band below its surface's props: 34 under the bar top's 35, 16 under the
                // floor's 20 and still over the wall's hangers at 15.
                var slot = _slots[def.Slot];
                // THE WALL IS A LADDER TOO (2026-09-06, the author's four plates: the
                // cracked room the bar opens in, then plaster, then panelling, then the
                // harlequin paper). A rung of it is not stood at a hook — it IS the
                // picture, so it goes where the picture goes: onto the background's own
                // renderer, at the same size, under everything that stands in the room.
                // A stage built without a plate has nowhere to put it and skips it.
                if (slot.Backdrop)
                {
                    if (_backgroundSr == null) continue;
                    if (!slot.Overlay) { _backgroundSr.sprite = sprite; continue; }
                    // AN OVERLAY IS A LAYER OF THE PICTURE (2026-09-12, the right wall
                    // ladder): the same 640x360 canvas as the plate, on the plate's own
                    // transform — so the cover fit that sizes the plate sizes it too, and it
                    // can never drift off the wall it was cut from — over the plate (10) and
                    // the window's glass (11), which is at the far side and never meets it,
                    // and under every hook (15).
                    // Kept with the placed pieces so a re-dress clears it like the rest, and
                    // skipped by PlaceFixtures, which stands things on their feet.
                    var layer = new GameObject("Fx_" + def.Id).AddComponent<SpriteRenderer>();
                    layer.transform.SetParent(_backgroundSr.transform, false);
                    layer.sprite = sprite;
                    layer.sortingLayerID = _backgroundSr.sortingLayerID;
                    // ...at its slot's own order when it names one (2026-09-13: the ceiling
                    // and the floor lie over the right wall at 13).
                    layer.sortingOrder = slot.Order != 0 ? slot.Order : OverlayOrder;
                    if (_litMaterial != null) layer.sharedMaterial = _litMaterial;
                    _placedFixtures.Add((def, layer.transform, null, 0f));
                    continue;
                }
                // A CARRIED piece is not room dressing (2026-09-06, the shaker): the bar
                // owns it and the market sold it, but where it stands is the HUD's — on the
                // coaster while a drink waits in it, in the hand on the bench, nowhere at
                // all the rest of the night. The room would stand it at a hook forever.
                if (slot.Carried) continue;
                bool onCounter = slot.OnCounter;
                bool hangs = slot.Hangs;
                bool flat = slot.Flat;
                // A PAIRED slot mounts the same piece twice, symmetric about the hook
                // (2026-08-24, the wall lamps: "simetrik bir şekilde 2 adet"). One fixture,
                // one purchase, two mountings — the spread is the slot's, not the piece's,
                // because the two brackets are on the wall whichever lamp is screwed to them.
                int copies = slot.PairSpreadPx > 0f ? 2 : 1;
                for (int m = 0; m < copies; m++)
                {
                    float off = copies == 2
                        ? (m == 0 ? -0.5f : 0.5f) * slot.PairSpreadPx : 0f;
                    string suffix = copies == 2 ? (m == 0 ? "_L" : "_R") : "";
                    // A PIECE MAY WANT ITS OWN ORDER (2026-09-09, the author: the agave is
                    // counter dressing — "tezgahın önünde bira musluklarının arkasında" —
                    // which is neither its slot's hook nor its slot's band).
                    var sr = WorldSprite("Fx_" + def.Id + suffix, sprite,
                                         order: def.Order != 0 ? def.Order
                                              : onCounter ? (flat ? 34 : 35)
                                              : hangs ? 15 : flat ? 16 : 20);
                    if (def.Id == "prep_mat")
                    {
                        // Tiled along the rail (see SetPrepMatSpan): the art repeats its ribs
                        // rather than stretching them, the counter's own law.
                        sr.drawMode = SpriteDrawMode.Tiled;
                        sr.tileMode = SpriteTileMode.Continuous;
                        float h = sprite.rect.height / sprite.pixelsPerUnit;
                        sr.size = new Vector2(_prepMatWidth > 0f ? _prepMatWidth : sprite.rect.width / sprite.pixelsPerUnit, h);
                    }
                    // A SCREEN's sprite is a sheet, and the loader handed the whole sheet
                    // in as one picture. Cut it and start the set playing; if the cut fails
                    // the fixture simply stays the still it already is.
                    if (def.IsScreen)
                    {
                        int cols;
                        // The cell is the DATA's (2026-09-05); the constants are the
                        // fallback for a sheet whose row forgot to say.
                        var frames = LoadScreenFrames(def.Sprite,
                            def.CellW > 0 ? def.CellW : TvCellW, def.CellH > 0 ? def.CellH : TvCellH, out cols);
                        if (frames != null)
                        {
                            _tvSr = sr;
                            _tvFrames = frames;
                            _tvCols = cols;
                            SetTvFrame(0, 0);
                        }
                    }
                    // A matched pair FACES each other: the art is one drawing, and two
                    // copies leaning the same way read as a print error, not a pair.
                    if (copies == 2 && m == 1) sr.flipX = true;

                    Light2D glow = null;
                    if (def.HasLight)
                    {
                        // House lights are found by an INDEXED name — the closing beat's
                        // look test reads them without seeing this assembly — and run on
                        // the evening's clock rather than at their json intensity.
                        bool house = slot.HouseLight;
                        string glowName = house
                            ? "HouseLight" + _houseLights.Count
                            : "FxGlow_" + def.Id + suffix;
                        // IN THE LANGUAGE (2026-09-21): the data's colour is a wish; the room snaps it to
                        // the house's tungsten, one of the two tubes, or the screen (LightLanguage.Snap).
                        glow = PointLight(glowName,
                            LightLanguage.Snap(new Color(def.LightR, def.LightG, def.LightB), def.IsScreen),
                            def.LightIntensity, def.LightRadius);
                        if (onCounter) LightLayers(glow, LayerCounter, LayerPatrons);
                        else LightLayers(glow, LayerBackground, LayerPatrons);
                        if (house)
                        {
                            _houseLights.Add((glow, def.LightIntensity));
                            glow.intensity = def.LightIntensity * _houseBase;
                        }
                        // The set's own spill is driven by the picture, not by the hour:
                        // it dies when the tube dies and comes back with the warm-up.
                        if (def.IsScreen)
                        {
                            _tvGlow = glow;
                            _tvGlowBase = def.LightIntensity;
                        }
                    }
                    _placedFixtures.Add((def, sr.transform, glow, off));

                    // EVERYTHING STANDING OR HANGING CASTS ONE (2026-09-17, the author:
                    // "nesnelerin gölgeleri olmalı"): a dark copy of the piece one step
                    // behind it, off the hour's light, so a plant on the boards and a
                    // picture on the wall both have air between them and the plaster. A
                    // mat lies flat and cannot; a piece on the bar top throws a shorter one,
                    // since the lamps over the bar stand almost straight above it. Destroyed
                    // with its source: the copy watches for it and takes itself down.
                    if (!flat) CastShadow.Attach(sr, onCounter ? 0.7f : 1f);

                    // WHAT SELLS "STANDING ON" RATHER THAN "FLOATING NEAR": only pieces that
                    // touch a surface get one. A sconce on the wall and a lantern on a cord
                    // touch nothing, and a blob under them would read as a stain. Every
                    // hanger shipped lit until the triptych, so !HasLight passed for
                    // "touches the floor" — the slot says it outright now (2026-08-24).
                    // A MAT is the third case: it touches the floor along its whole face,
                    // so a blob under it is a stain rather than a contact (2026-08-25).
                    if (!onCounter && !hangs && !flat && !def.HasLight)
                        ContactShadow(sr.transform, sprite.rect.width * 0.9f);

                    // A beer font is the door onto the draught station, and the sink is the
                    // door onto the drain, so both answer the pointer.
                    if (def.IsTap)
                        BuildPropDoor(def, sr, () => _onTapClicked?.Invoke(), UIText.T("stage.door.tap"));
                    // THE DRAIN TAKES NO CLICK (2026-08-26). Its plate is here for two
                    // other jobs: the carried glass tests its release against it, and the
                    // pointer is told what the basin is for. A click as well would be a
                    // second, cheaper way to do the one thing in this game that costs money
                    // - which is exactly the shape the author sent back.
                    else if (def.IsDrain)
                    {
                        // ...and since 2026-09-05 the sink DOES take a click, for the one
                        // free thing it does: washing the glasses in the hand (GDD 27 §4.2).
                        // Pouring a drink away is still only by carry. The water it runs
                        // is a frame sheet of its own, over the basin (BuildWater).
                        _drainDoor = BuildPropDoor(def, sr, () => _onSinkClicked?.Invoke(),
                                                   UIText.T("stage.door.sink"));
                        BuildWater(def, sr);
                    }
                }
            }
            // The set starts once the whole room is dressed, not inside the loop: its own
            // glow is hung a few lines above and the player drives both together.
            // Motion.Reduced silences it the way it silences the ambient flicker — a screen
            // cutting itself off every few seconds is exactly what that setting is for; the
            // first advert stays on the wall as a still.
            if (_tvSr != null && _tvFrames != null && !Motion.Reduced)
                _tvPlayer = StartCoroutine(PlayTelevision());
            else if (_tvSr != null) SetTvGlow(1f);
            PlaceFixtures();
        }

        // ── props that are doors (2026-08-15) ───────────────────────────────────
        // The author: "bira musluğuna tıklanması gereksin ... musluğa tıklanınca direkt bira
        // koyma sahnesi gelecek". The kegs left the back-bar wall, so the only way to a pint
        // is walking to the tap — which means the prop has to be clickable. The SINK joined
        // it on 2026-08-26 for the same reason from the other end: the pedal bin went, and a
        // drink you decide against goes down the basin that is already standing there.

        private RectTransform _tapDoorRoot;
        private CanvasGroup _propDoorGate;
        private readonly List<RectTransform> _tapDoors = new List<RectTransform>();

        /// <summary>
        /// An invisible plate over a beer font, sized and stood exactly where the font is.
        ///
        /// The plate is CANVAS, not world: it inherits the flow's scrim for free (a panel open
        /// over the room blocks it, the way the till is blocked), which a physics raycast into
        /// world space would have had to re-implement. Its coordinates are the slot's own —
        /// the room art is authored at the design frame's own 640×360, so an art pixel IS a
        /// design unit and the cover fit is the identity. The till's fixed X/Y have always
        /// assumed the same thing; the ratio is written out anyway so the assumption is
        /// visible if a wider room is ever painted.
        ///
        /// Order 7: over the patrons (5) and the till (6), under the service flow (12). Over
        /// the patrons on purpose — a seat's hit rect is half again as wide as the bust in it,
        /// so the far edges of two of them reach across the fonts, and a door that the room
        /// swallows at the edges is a door the player learns not to trust. What it costs is a
        /// sliver of empty seat-rect beside each font, nowhere near anybody's body.
        /// </summary>
        private RectTransform BuildPropDoor(LastCall.Core.FixtureDefinition def,
                                            SpriteRenderer body, System.Action onClick,
                                            string word)
        {
            LastCall.Game.StageSlot slot;
            if (!_slots.TryGetValue(def.Slot, out slot)) return null;
            if (_tapDoorRoot == null)
            {
                _tapDoorRoot = OverlayCanvas("TapDoors", 7, raycasts: true);
                UiAuditExempt.Mark(_tapDoorRoot, "the tap door is a hit plate over a prop in "
                    + "the room, sized to the font's own art and stood in the font's own slot");
                // A PROP DOOR IS SHUT WHILE THE DRAWER IS (2026-08-26). These plates are
                // CANVAS rects standing at their slot's fixed coordinates, and the slot is on
                // the counter — which RISES when the cellar opens. The prop goes up with the
                // room and the plate does not, so an open drawer leaves every one of them
                // hanging over the shelves, catching clicks meant for the stock behind them.
                // It went unseen for as long as the only prop door was the beer font at stage
                // 540, clear of the bottle row; the sink is at 140 and sits straight over it,
                // and the smoke suite caught it on the first run ("under the pointer:
                // PropDoor_counter_sink"). The till answered this exact question the same way
                // and for the same reason — while the cellar is open you are behind the bar,
                // not at the counter's furniture.
                _propDoorGate = _tapDoorRoot.gameObject.AddComponent<CanvasGroup>();
                _propDoorGate.blocksRaycasts = _drawerT < 0.01f;
            }

            var art = body.sprite.rect.size;
            float sx = Reference.x / Mathf.Max(1f, _backgroundNative.x);
            float sy = Reference.y / Mathf.Max(1f, _backgroundNative.y);

            var plate = NewRect("PropDoor_" + def.Id, _tapDoorRoot);
            plate.anchorMin = plate.anchorMax = new Vector2(0, 0);
            plate.pivot = new Vector2(0.5f, 0);
            plate.sizeDelta = new Vector2(art.x * sx, art.y * sy);
            plate.anchoredPosition = new Vector2(
                (float.IsNaN(def.X) ? slot.X : def.X) * sx,
                (float.IsNaN(def.Y) ? slot.Y : def.Y) * sy);   // the piece's own spot, if it has one
            var hit = plate.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);   // invisible, but catches the pointer
            if (onClick != null)
            {
                var btn = plate.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => onClick());
            }

            // THE AFFORDANCE IS THE PROP, not the plate: an invisible plate cannot light up
            // without drawing a rectangle over the counter, so the pointer lights the FONT's
            // own brass instead. This is where that rule was first written, by hand, with an
            // EventTrigger and a colour snapped on and off; on 2026-08-25 it became HoverGlow
            // and every other prop in the room got the same answer, easing rather than
            // snapping and reading its rest colour at the moment the pointer arrives.
            var glow = plate.gameObject.AddComponent<HoverGlow>();
            glow.Sprites = new[] { body };
            // WHAT MOVES IS THE PROP, NOT THE PLATE (2026-09-06). The glow lives on the hit
            // plate, so with no riser named it was raising, growing and rocking the invisible
            // rectangle while the font and the basin stood perfectly still — the rise has
            // never once been seen, and a plate that drifts two units off its prop is a drop
            // target that moves out from under the hand (the tin's carry test caught it by
            // failing to reach a basin it was standing on). The prop is the affordance.
            glow.Riser = body.transform;
            // The font is one drawing standing on the counter, so it takes the whole
            // answer — in the world's own units, where one is two of the HUD's.
            glow.Rise = 2f; glow.Sway = 0.8f; glow.Grow = 1.05f; glow.Halo = 1.4f;
            if (def.IsDrain) _drainGlow = glow;   // ...and the drain can be called (below)

            // ...and it SAYS what it does, before it is pressed (2026-08-26). The glow was
            // already the affordance; the word is what turns "this can be clicked" into
            // "this is the drain". The HUD owns the plate the word is written on.
            var relay = plate.gameObject.AddComponent<HoverRelay>();
            var thePlate = plate;
            var theWord = word;
            relay.Entered = () => _onPropHover?.Invoke(thePlate, theWord);
            relay.Exited = () => _onPropHover?.Invoke(thePlate, null);

            _tapDoors.Add(plate);
            return plate;
        }

        /// <summary>Stands every placed piece in its slot for the CURRENT fit — the slots
        /// ride the room art the way the painted lamps do, so a re-fitted window moves the
        /// dressing with the picture rather than away from it.</summary>
        private void PlaceFixtures()
        {
            foreach (var placed in _placedFixtures)
            {
                LastCall.Game.StageSlot slot;
                if (!_slots.TryGetValue(placed.Def.Slot, out slot)) continue;
                if (slot.Backdrop) continue;   // a layer of the picture rides the plate itself
                // THE ROOM'S OWN OFFSET, like the cellar's stock (see PlaceCellarSlot).
                // These are art/stage points, but the DRAWER moves the world root every
                // fixture hangs under, so re-dressing the room while the cellar is open —
                // which is exactly what a dev preset or a mid-night restart does — dropped
                // the whole set back to its shut height, and the sink came down into the
                // shelves. It has never fired in a normal night (the market shuts the
                // cellar), and it is one term.
                // The snack mat stands where the HUD's rail is, not at its slot's x.
                // AND A PIECE MAY STAND WHERE IT LIKES (2026-09-09): the author put the agave
                // on the counter and lifted the pothos without moving the rungs they share a
                // slot with, so an x/y written on the FIXTURE wins over the slot's hook.
                float atX = placed.Def.Id == "prep_mat" && !float.IsNaN(_prepMatCentreX)
                    ? _prepMatCentreX
                    : (float.IsNaN(placed.Def.X) ? slot.X : placed.Def.X) + placed.OffsetX;
                float atY = float.IsNaN(placed.Def.Y) ? slot.Y : placed.Def.Y;
                var basePos = (_backgroundSr != null
                    ? StageArtPointToWorld(new Vector2(atX, atY))
                    : StageToWorld(atX, atY))
                    + (_world != null ? _world.position : Vector3.zero);
                float k = _backgroundSr != null ? _backgroundScale : 1f;
                placed.Body.localScale = new Vector3(k, k, 1f);
                float h = placed.Body.GetComponent<SpriteRenderer>().sprite.bounds.size.y * k;
                // FURTHER BACK DRAWS BEHIND (2026-08-26). Two pieces of floor dressing share
                // sorting order 20, and two sprites on one order leave which of them wins to
                // chance — the trap this file already names twice, for the triptych and for
                // the mats, each time solved by carving out another band. There are no bands
                // left to carve between a plant and a table: they stand on the same floor.
                // So the tie is broken by the ROOM instead — the slot's own depth, as a
                // hair of Z. The project sorts transparents by distance (Default, and the
                // camera is orthographic), so a piece standing further up the picture is
                // further from the lens and loses to the one in front of it, every frame, on
                // every machine. It surfaced the day the author moved the palm to x 150,
                // where it shares a footprint with the left-hand table.
                //
                // The scale is deliberately tiny: 360 art rows come to 0.72 world units,
                // nowhere near the camera's clip planes, and Z never outranks a sorting
                // order — it only decides who goes first when two pieces are already equal.
                // GREENERY STANDS BEHIND THE FURNITURE (2026-09-21, the author: "bitkiler sandalyelerin
                // arkasında olmalı hiyerarşide"): the pothos at x 150 shares its footprint with the left table,
                // and the depth rule alone put it in front. A plant is pushed a hair further back than any
                // piece of floor dressing can stand, so the tie always falls the furniture's way.
                float back = placed.Def.Group == "greenery" ? 0.3f : 0f;
                placed.Body.position = basePos + new Vector3(0f, h * 0.5f, atY * 0.002f + back);
                // The glow hangs at the piece's own light line — the flame, the belly of
                // the shade — which for every launch fixture is about ⅔ up the sprite.
                if (placed.Glow != null)
                    placed.Glow.transform.position = basePos + new Vector3(0f, h * 0.66f + placed.Def.LightDy * k, 0f);
            }

            // The blobs ride their own pieces, so a re-fitted window moves the shadow with
            // the thing casting it rather than leaving it on the old floor.
            foreach (var sh in _shadows)
            {
                if (sh.Blob == null || sh.Under == null || sh.Blob.sprite == null) continue;
                float k = _backgroundSr != null ? _backgroundScale : 1f;
                var b = sh.Under.GetComponent<SpriteRenderer>();
                float footY = b != null ? b.bounds.min.y : sh.Under.position.y;
                var art = sh.Blob.sprite.bounds.size;
                sh.Blob.transform.localScale = new Vector3(
                    sh.Width * k / art.x, sh.Width * k * 0.28f / art.y, 1f);
                sh.Blob.transform.position = new Vector3(sh.Under.position.x, footY + 1f * k, 0f);
            }
        }

        /// <summary>
        /// The sky outside, on the shift's own clock: 0 at opening (18:00, the sun clear of
        /// the towers) and 1 at closing (02:00, the city deep in night). Straight proportion,
        /// the way the author asked for it.
        ///
        /// TOLD, NOT READ, like the money and the slots: the stage never reaches into the
        /// run. Cheap to call every frame — the model is a few lerps, and the picture only
        /// redraws when the hour has moved enough to show (WindowSky.Step).
        /// </summary>
        public void SetSkyFraction(float fraction)
        {
            _tau = Mathf.Clamp01(fraction);
            if (_skyClock == null) return;
            // How much sun is left is MEASURED on the drawing — only the skyline knows where
            // the towers are — and a build without the view falls back to a straight set.
            float visible = _sky != null ? _sky.SunVisible(_tau)
                : 1f - SkyClock.SmoothStep(0.10f, 0.25f, _tau);
            _now = _skyClock.Evaluate(_tau, visible);
            ApplyDaylight(_now);
        }

        /// <summary>One frame of the view: the plate redraws when the hour or the ambient
        /// clock has moved enough to show; the birds move every frame they are up.</summary>
        private void StepSky()
        {
            if (_sky == null) return;
            bool motion = !Motion.Reduced;
            _sky.Step(_tau, _ambientClock, motion);
            _flock?.Step(_tau, _ambientClock, Time.unscaledDeltaTime * AmbientScale, motion);
        }

        /// <summary>
        /// LIGHTS THE ROOM FROM THE HOUR. Every number here is continuous in tau; nothing
        /// steps. The closing beat owns the ceiling, the bar's lamps and the wash while it
        /// runs (two writers with no execution order between them would leave the last
        /// call's dimming to a coin toss — the race the sconces paid for, 2026-08-24), so
        /// those are only written while it is not.
        /// </summary>
        private void ApplyDaylight(SkyClock.Daylight d)
        {
            float closing = Mathf.SmoothStep(0f, 1f, _closingT);
            _sunKeyBase = Mathf.Lerp(SunKeyNight, SunKeyDay, d.SunStrength);
            if (_windowLight != null)
            {
                _windowLight.color = Color.Lerp(NightGlassTint, d.SunCore, d.SunStrength);
                if (_closingT <= 0f) _windowLight.intensity = _sunKeyBase;
            }
            if (_skyGlow != null)
            {
                _skyGlow.color = d.Glow;
                _skyGlow.intensity = d.GlowIntensity;
            }
            if (_counterGlow != null)
            {
                _counterGlow.color = d.Glow;
                _counterGlow.intensity = d.GlowIntensity * CounterGlowShare;
            }
            if (_sunShaft != null)
            {
                _sunShaft.color = d.Shaft;
                float shaftBase = ShaftDay * d.SunStrength * Mathf.Lerp(1f, ClosingWash, closing);
                // Where the patch lands: low and near the glass while the sun is high, far and high on the wall as
                // it sinks - the sun's own line, in the room's art px, under the world root so the drawer's lift
                // carries it with the room. THE SURFACE PICKS THE SHAPE (2026-09-21): below the skirting the patch
                // is the floor's - long, flat, leaning hard the way light through a side window lies on boards -
                // and above it the wall's, upright; the two cross over across a hand's width of the skirting.
                float sink = _skyClock != null ? SkyClock.SmoothStep(0f, _skyClock.Spec.sun.setBy, d.Tau) : 0f;
                var at = Vector2.Lerp(ShaftNear, ShaftFar, sink);
                float k = _backgroundSr != null ? _backgroundScale : 1f;
                float onWall = SkyClock.SmoothStep(FloorTopPx - 10f, FloorTopPx + 10f, at.y);
                _sunShaft.intensity = shaftBase * onWall;
                _sunShaft.transform.localPosition = StageArtPointToWorld(at);
                _sunShaft.transform.localScale = new Vector3(k * Mathf.Lerp(1f, ShaftStretch, sink), k, 1f);
                bool shaftOn = d.SunStrength > 0.01f;
                if (_sunShaft.enabled != shaftOn) _sunShaft.enabled = shaftOn;
                if (_sunFloor != null)
                {
                    _sunFloor.color = d.Shaft;
                    _sunFloor.intensity = shaftBase * (1f - onWall);
                    _sunFloor.transform.localPosition = StageArtPointToWorld(at);
                    _sunFloor.transform.localScale = new Vector3(k, k, 1f);
                    if (_sunFloor.enabled != shaftOn) _sunFloor.enabled = shaftOn;
                }
            }
            _washBase = d.AmbientIntensity;
            _houseBase = Mathf.Lerp(HouseDay, HouseNight, d.Dusk);
            if (_globalLight != null)
            {
                _globalLight.color = d.Ambient;
                if (_closingT <= 0f) _globalLight.intensity = _washBase;
            }
            SyncPatronFill();
            // THE BAR'S TWO ROWS, on the same clock as everything else the house owns. The
            // NEON is set unconditionally: a tube is on or it is off, the closing beat has
            // no business dimming one, and it is the only light left on the bar once that
            // beat takes the lamps down.
            _barDownBase = Mathf.Lerp(BarLightDay, BarLightNight, d.Dusk);
            float neonNow = Mathf.Lerp(BarNeonDay, BarNeonNight, d.Dusk);
            for (int i = 0; i < _barNeons.Count; i++)
                if (_barNeons[i] != null) _barNeons[i].intensity = neonNow;
            if (_closingT <= 0f)
            {
                for (int i = 0; i < _barLights.Count; i++)
                    if (_barLights[i] != null) _barLights[i].intensity = _barDownBase;
                for (int i = 0; i < _houseLights.Count; i++)
                    if (_houseLights[i].Light != null)
                        _houseLights[i].Light.intensity = _houseLights[i].Base * _houseBase;
            }
            // Every shadow in the room swings on the same light.
            CastShadow.Offset = d.ShadowOffset;
            CastShadow.Alpha = d.ShadowAlpha;
        }

        /// <summary>
        /// The window's four panes as a LIGHT: bars leaning the way sun through a side window
        /// lands on a wall, the head and sill dithered off in two steps rather than faded,
        /// so the patch is drawn and not blurred (16 §6.10). Built once, white; the light
        /// wears it in the sun's colour at the room's own scale.
        /// </summary>
        private static Sprite SunShaftCookie()
        {
            // 212 by 160, up from 132 by 100 (2026-09-21, the author: "boyutunu büyütmeliyiz")
            const int W = 212, H = 160;
            const float Shear = 0.5f;               // x per row: the lean of the panes on the wall
            const int Bar = 32, Gap = 13, Bars = 4;
            int run = Bars * Bar + (Bars - 1) * Gap;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SunShaftCookie",
            };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float lean = (y - H * 0.5f) * Shear;
                int edge = Mathf.Min(y, H - 1 - y);
                for (int x = 0; x < W; x++)
                {
                    float u = x - lean - (W - run) * 0.5f;
                    bool inBar = u >= 0f && u < run && (u % (Bar + Gap)) < Bar;
                    byte a = 0;
                    if (inBar)
                        a = edge < 3 ? (byte)(((x + y) & 1) == 0 ? 255 : 0)
                          : edge < 7 ? (byte)(((x + y) & 1) == 0 ? 255 : 128)
                          : (byte)255;
                    px[y * W + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>
        /// The same four panes lying on the FLOOR (2026-09-21): foreshortened to less than half the height and
        /// leaning almost three times as hard, because light through a side window lies along the boards in a long
        /// low sweep. Dithered off at the head and the sill the same way, so the two patches are one family.
        /// </summary>
        private static Sprite SunFloorCookie()
        {
            const int W = 300, H = 72;
            const float Shear = 1.4f;
            const int Bar = 34, Gap = 12, Bars = 4;
            int run = Bars * Bar + (Bars - 1) * Gap;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SunFloorCookie",
            };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float lean = (y - H * 0.5f) * Shear;
                int edge = Mathf.Min(y, H - 1 - y);
                for (int x = 0; x < W; x++)
                {
                    float u = x - lean - (W - run) * 0.5f;
                    bool inBar = u >= 0f && u < run && (u % (Bar + Gap)) < Bar;
                    byte a = 0;
                    if (inBar)
                        a = edge < 2 ? (byte)(((x + y) & 1) == 0 ? 255 : 0)
                          : edge < 5 ? (byte)(((x + y) & 1) == 0 ? 255 : 128)
                          : (byte)255;
                    px[y * W + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f);
        }


        /// <summary>
        /// WHAT THE ROOM IS LIT BY RIGHT NOW, for the surfaces that a Light2D cannot reach.
        ///
        /// The book on the bar and the bench's mat are canvas — no light in the world
        /// touches them — so they read this to wear the same hour by hand (the author,
        /// 2026-08-19: "backbar sahnesi çok aydınlık, ortamın ışığına uygun değil"). It is
        /// the model's own ambient tint, the very colour on the GlobalLight, so a prop and
        /// the plaster behind it are one room; told rather than computed twice.
        /// </summary>
        public Color RoomWashLight => Color.Lerp(_now.Ambient, _now.Glow, 0.5f * Mathf.Clamp01(_now.GlowIntensity));


        // ── the wall television (2026-09-04) ────────────────────────────────────
        // The author: "Televizyon içinde gözükecek animasyonlar oluştur sürekli olarak
        // animasyon dönecek her reklamdan sonra televizyon kapanacak kapanma animasyonu ile
        // sonra açılacak açılma animasyonu ile kapalı kaldığı süre 5 saniye kadar olucak."
        //
        // So the set runs a LOOP with four beats: an advert holds, the tube collapses, the
        // screen stays dark for five seconds, the tube warms back up on the NEXT advert.
        // Nothing here is a rule — a night does not change because a picture moved — so the
        // whole clock lives in the stage and Core only carries the flag that says "sheet".

        // The cell is the AUTHOR'S OWN cabinet drawing, so its size is the art's and not a
        // number chosen here (2026-09-04: "Televizyon görseli bu olacak" — a 45×45 set seen
        // at an angle). Tools/tv_build.py seats the adverts in its face and lays the sheet
        // out; a redraw at another size changes these two numbers and nothing else.
        private const int TvCellW = 49, TvCellH = 49;
        // ONE ROW PER ADVERT, AND EACH ROW IS A LOOP (2026-09-09, the author:
        // "televizyon animasyonları daha detaylandırılsın, daha profesyonel animasyonlar
        // üretilsin, reklam kafasında olsun yine"). The four adverts were four STILLS held
        // for six seconds each, which is a poster on a wall rather than a set that is on.
        // Tools/tv_build.py derives six frames from each plate — a push in, a shine crossing
        // it, the tube's own scan stepping down — so the sheet is rows 0..3 of advert and
        // then the two states the tube has.
        private const int TvAdCount = 4, TvAdFrames = 6;
        private const int TvOffRow = TvAdCount, TvOnRow = TvAdCount + 1;
        /// <summary>Seconds a frame of an advert gets — slower than the tube's own frames,
        /// which are a machine doing something, where this is a picture moving.</summary>
        private const float TvAdStep = 0.19f;

        /// <summary>How long one advert holds before the set shuts itself off.</summary>
        private const float TvAdHold = 6f;
        /// <summary>The author's five seconds of dark, named rather than typed twice.</summary>
        private const float TvDarkHold = 5f;
        /// <summary>Seconds a frame of the collapse and the warm-up each get.</summary>
        private const float TvFrameStep = 0.055f;

        private SpriteRenderer _tvSr;
        private Coroutine _tvPlayer;
        private Light2D _tvGlow;
        private float _tvGlowBase;
        private Sprite[,] _tvFrames;      // [row, col]
        private int _tvCols;

        /// <summary>
        /// Plays the set for as long as the room is standing.
        ///
        /// The dark beat carries the light with it: a television that goes black while its
        /// spill keeps washing the wall reads as a bug, and the glow is the only part of
        /// this the sprite cannot say. It is the same split the sky already makes — the
        /// PICTURE steps between whole frames and the LIGHT is a number.
        ///
        /// WaitForSecondsRealtime, not WaitForSeconds: this is ambience and it must keep
        /// running while a panel has the game paused, which is the rule the PlayMode suite
        /// learned the hard way (a test frame is ~1ms, so a frame count is not a wait).
        /// </summary>
        private System.Collections.IEnumerator PlayTelevision()
        {
            int ad = 0;
            while (true)
            {
                if (_tvSr == null || _tvFrames == null) yield break;

                // the advert, PLAYING: its own six frames, round and round for as long as
                // the spot runs (2026-09-09)
                SetTvGlow(1f);
                float until = Time.realtimeSinceStartup + TvAdHold;
                int frame = 0;
                while (Time.realtimeSinceStartup < until)
                {
                    if (_tvSr == null || _tvFrames == null) yield break;
                    SetTvFrame(ad, frame % TvAdFrames);
                    frame++;
                    yield return new WaitForSecondsRealtime(TvAdStep);
                }

                // it switches itself off
                for (int i = 0; i < _tvCols; i++)
                {
                    if (_tvFrames[TvOffRow, i] == null) break;
                    SetTvFrame(TvOffRow, i);
                    // The spill dies with the picture rather than after it.
                    SetTvGlow(1f - (i + 1) / (float)_tvCols);
                    yield return new WaitForSecondsRealtime(TvFrameStep);
                }
                SetTvGlow(0f);
                yield return new WaitForSecondsRealtime(TvDarkHold);

                // ...and comes back on the NEXT advert, which is why the warm-up plays
                // before the still rather than after it.
                ad = (ad + 1) % TvAdCount;
                for (int i = 0; i < _tvCols; i++)
                {
                    if (_tvFrames[TvOnRow, i] == null) break;
                    SetTvFrame(TvOnRow, i);
                    SetTvGlow((i + 1) / (float)_tvCols);
                    yield return new WaitForSecondsRealtime(TvFrameStep);
                }
            }
        }

        private void SetTvFrame(int row, int col)
        {
            if (_tvSr == null || _tvFrames == null) return;
            if (row < 0 || row >= _tvFrames.GetLength(0)) return;
            if (col < 0 || col >= _tvFrames.GetLength(1)) return;
            var sp = _tvFrames[row, col];
            if (sp != null) _tvSr.sprite = sp;
        }

        /// <summary>The CRT's spill, as a fraction of the fixture's own json intensity.</summary>
        private void SetTvGlow(float t)
        {
            if (_tvGlow == null) return;
            _tvGlow.intensity = _tvGlowBase * Mathf.Clamp01(t);
        }

        /// <summary>
        /// Cuts a screen's sheet into [row, col] frames.
        ///
        /// Rows are the STATES (advert stills, the collapse, the warm-up) and the row widths
        /// differ — there are four adverts and six frames of tube — so the short row's tail
        /// is left null and the player stops at the first one, rather than a count constant
        /// here going stale the next time the sheet is rebuilt. Same reasoning, and same
        /// bottom-up rect arithmetic, as the window's own cutter below.
        /// </summary>
        private static Sprite[,] LoadScreenFrames(string spriteName, int cellW, int cellH, out int cols)
        {
            cols = 0;
            var sheet = Resources.Load<Texture2D>("Fixtures/" + spriteName);
            if (sheet == null) return null;
            int c = sheet.width / cellW, r = sheet.height / cellH;
            if (c < 1 || r < 1)
            {
                Debug.LogWarning($"DiegeticStage: screen sheet '{spriteName}' is " +
                                 $"{sheet.width}×{sheet.height}, too small for a " +
                                 $"{cellW}×{cellH} cell — it will draw as a still.");
                return null;
            }
            cols = c;
            var frames = new Sprite[r, c];
            for (int row = 0; row < r; row++)
            {
                // The ADVERTS row is shorter than the two tube rows — four pictures against
                // six frames of collapse — so its tail cells are transparent. They are
                // skipped by COUNT and not by reading the sheet's alpha: the fixtures are
                // imported without Read/Write enabled (LastCallImporter sets everything
                // else and deliberately not that), so GetPixels on them throws, and a cutter
                // that needs a readable texture would work in the editor and fail in a
                // build. The window's cutter can scan because Scene textures are readable;
                // this one cannot, so the sheet's shape is stated instead.
                // Every row is full now: an advert's row is its six frames, and the tube's
                // two rows are six each (2026-09-09).
                int wide = c;
                for (int col = 0; col < wide; col++)
                {
                    var rect = new Rect(col * cellW,
                                        sheet.height - (row + 1) * cellH,
                                        cellW, cellH);
                    frames[row, col] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), 1f);
                }
            }
            return frames;
        }

        // THE SHEET'S CUTTER IS GONE (2026-09-17): LoadWindowFrames and IsCellEmpty sliced
        // Scene/window_cycle.png into thirty-one plates for the window. The sheet is the
        // SOURCE Tools/window_sky.py derives the city's plates from now, and the view is
        // drawn from the hour — see WindowSky.


        /// <summary>A point in the background art's own bottom-left space → world, through the
        /// cover fit (scaled about the centre, so the mapping is scale-about-centre too).</summary>
        private Vector3 StageArtPointToWorld(Vector2 artPoint) => new Vector3(
            (artPoint.x - _backgroundNative.x * 0.5f) * _backgroundScale,
            (artPoint.y - _backgroundNative.y * 0.5f) * _backgroundScale, 0f);

        // THE LETTERED SIGN IS GONE (2026-08-19, the author: "sahnedeki Last Call
        // neonunu ve ışığını kaldır"). It hung at art (470,300) on its own overlay canvas
        // at -9, blinked on NeonBlink, and threw a magenta spill into the room; all of it
        // went together, because a sign's light without its sign is a magenta stain on a
        // wall with nothing making it. The room's light now comes from the window and its
        // own lamps, and nothing else.

        // ── the till is gone (2026-08-26) ────────────────────────────────────────
        // The author: "kasa ve parayı ana sahneden kaldır". The register stood on the bar's
        // right-hand end on two overlay canvases of its own, wore the bar's balance over it
        // in gold, floated every rise and fall off the drawer, and opened the ledger when it
        // was clicked. All four went together: a machine with no number on it is a prop, and
        // a number with no machine under it is the fascia readout this one replaced.
        //
        // WHAT THE ROOM KEEPS: nothing counts money at you while you are serving. The night's
        // takings are read where a night's takings are read — on the slip, when the books
        // open — and the market tablet carries the running balance while you are spending it.
        // The ledger's door moved to the standing block on the fascia (TycoonHud's own note).

        private static RectTransform OverlayCanvas(string name, int order, bool raycasts)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            if (raycasts) go.AddComponent<ForgivingRaycaster>();
            // Fixed field, like the HUD's: what draws over the room has to be measured in
            // the same units the room is, and the room is now windowboxed to 640x360.
            return DesignFrame.Wrap((RectTransform)go.transform, Reference);
        }

        // ── the procedural fallback room (no environment art wired) ──────────────

        /// <summary>The flat stand-in room, canvas-drawn as it always was: a dev safety net,
        /// not a lit scene. A broken art reference should look wrong, not invisible.</summary>
        private void BuildFallbackRoom()
        {
            var root = OverlayCanvas("FallbackRoom", -10, raycasts: false);

            var sky = FullLayer(root, "SkyCity", UITheme.Night[0]);
            Window(sky, new Vector2(60, 40), new Vector2(70, 300));
            Window(sky, new Vector2(80, 44), new Vector2(510, 300));

            var far = NewRect("ClubFar", root);
            Stretch(far, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var wall = NewRect("BackWall", far);
            Stretch(wall, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, CounterFrontY), Vector2.zero);
            var wallImg = wall.gameObject.AddComponent<Image>();
            wallImg.color = UITheme.Night[1]; wallImg.raycastTarget = false;
            AddCrowd(far);

            var mid = FullLayer(root, "ClubMid", new Color(0, 0, 0, 0));
            AddNeonSigns(mid);
        }

        /// <summary>
        /// Ambient life, deliberately sparse: the room flickers for a frame every few
        /// seconds — the GLOBAL LIGHT dips now, which is what a mains stutter is. Purely
        /// cosmetic; this jitter never touches RunRng, so run determinism is unaffected.
        /// </summary>
        // ── the closing beat (GDD 26 §7, PLAN_last_call S4) ─────────────────────
        //
        // When the last customer is on their stool the room says so, in the language it
        // already has: the ceiling comes down, the wash thins, the neon over the door burns
        // harder, and ONE lamp finds the person at the bar. Nothing is drawn for this — every
        // number below is an intensity on a light that was already hanging there, which is
        // what keeps it from reading as a different game for thirty seconds.

        /// <summary>How far the ceiling and the wash drop for the last call. The sign used
        /// to burn HARDER here (ClosingSign 1.9) — it went with the sign itself, 2026-08-19.</summary>
        private const float ClosingCeiling = 0.22f, ClosingWash = 0.55f;

        /// <summary>The house lights, kept by reference: the evening dims them and the
        /// closing beat takes them down every frame, and finding them by name would rebuild
        /// strings a frame to do it. Base is the fixture's own (night) intensity; the list
        /// is rebuilt whenever SyncFixtures stands a new set.</summary>
        private readonly List<(Light2D Light, float Base)> _houseLights =
            new List<(Light2D, float)>();

        /// <summary>Where the bar's downlights stand before the closing beat touches them,
        /// so the beat falls FROM the hour the sky left them at. Seeded at the night value:
        /// ApplyDaylight overwrites it from the first frame it has a model to read, and a
        /// room without one still dims from somewhere real.</summary>
        private float _barDownBase = BarLightNight;

        /// <summary>The lamp over the guest, built dark and only ever lit for them.</summary>
        private Light2D _guestLight;
        private bool _closing;
        private float _closingT;            // 0 = the ordinary room, 1 = the last call
        private float _guestWorldX;

        /// <summary>
        /// Turns the closing beat on or off and says WHERE the person is, in HUD units (the
        /// stool's own x). Called every frame by the HUD — it is idempotent, and the fade is
        /// this class's business, not the caller's.
        /// </summary>
        /// <summary>Whether the closing beat has already spoken. SetClosingBeat is called
        /// every frame with the current state, so the swell needs the rising edge.</summary>
        private bool _closingBeatSpoke;

        public void SetClosingBeat(bool on, float hudX)
        {
            if (on && !_closingBeatSpoke) Sfx.Play("synth_swell", 0.85f);
            _closingBeatSpoke = on;
            _closing = on;
            _guestWorldX = hudX / (720f / 360f);
        }

        /// <summary>Drives the fade. Separate from <see cref="Ambient"/> because that one is a
        /// coroutine that Motion.Reduced switches off, and a player who has asked for less
        /// movement still gets the light — it simply arrives without the ramp.</summary>
        private void StepClosing()
        {
            float target = _closing ? 1f : 0f;
            if (Motion.Reduced) _closingT = target;
            else if (!Mathf.Approximately(_closingT, target))
                _closingT = Mathf.MoveTowards(_closingT, target, Time.unscaledDeltaTime / 1.1f);
            else if (_guestLight == null || !_closing) { if (_closingT <= 0f) return; }

            float t = Mathf.SmoothStep(0f, 1f, _closingT);

            // FROM the hour the sky left the room, DOWN TO A LEVEL — and the level is
            // absolute, not a fraction of wherever it started. "The ceiling comes down"
            // means the room actually goes dark for the beat, whatever time it is; taking
            // 22% off a night that is already at 1.85 leaves 0.41, which is brighter than
            // the room this beat was written against and is not a room going dark at all.
            // So the sky decides where the fall STARTS and the beat decides where it ENDS.
            if (_globalLight != null)
                _globalLight.intensity = Mathf.Lerp(_washBase, GlobalIntensity * ClosingWash, t);
            // Seeded at the wall lamps' own line rather than zero: with no lit house
            // fixture standing (possible — a fixture may carry no light), a zero here would
            // hang the guest's lamp in the middle of the stage (found by review, 2026-08-24).
            float ceilingY = StageArtPointToWorld(new Vector2(0f, 260f)).y;
            for (int i = 0; i < _houseLights.Count; i++)
            {
                if (_houseLights[i].Light == null) continue;
                _houseLights[i].Light.intensity = _houseLights[i].Base
                    * Mathf.Lerp(_houseBase, HouseDay * ClosingCeiling, t);
                ceilingY = _houseLights[i].Light.transform.position.y;
            }
            // The lamps over the bar come down with the ceiling — they ARE the house, over
            // the one place the house works — and the tube along the counter's edge does
            // not, because neon has no dimmer. What the beat leaves is the last drinker in
            // a dark room with a magenta line under their elbows, which is the picture.
            float barClosed = Mathf.Lerp(_barDownBase, BarLightDay * ClosingCeiling, t);
            for (int i = 0; i < _barLights.Count; i++)
                if (_barLights[i] != null) _barLights[i].intensity = barClosed;
            // The window dies with the room: at the last call the light outside is not what
            // the beat is about, and leaving it burning kept a bright hole in a dark room.
            if (_windowLight != null)
                _windowLight.intensity = Mathf.Lerp(_windowLight.intensity,
                    _sunKeyBase * Mathf.Lerp(1f, ClosingWash, t),
                    1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));

            if (_guestLight == null && _world != null && t > 0.001f)
            {
                _guestLight = PointLight("LastCallLamp", LampTint, 0f, LampRadius * 1.15f);
                LightLayers(_guestLight, LayerBackground, LayerPatrons);
            }
            if (_guestLight != null)
            {
                // It hangs where the other lamps hang — the room has one ceiling, and a pool
                // that floated at mid-wall would read as a spotlight from nowhere.
                // Against the beat's OWN ceiling, which the block above lands on an absolute
                // level — so this is the constant it always was, and the contrast it buys
                // (2.1 against 0.22 of the same base) is nine to one at every hour.
                _guestLight.intensity = Mathf.Lerp(0f, LampIntensity * 2.1f, t);
                _guestLight.transform.position = new Vector3(_guestWorldX, ceilingY, 0f);
                if (_guestLight.gameObject.activeSelf != (t > 0.002f))
                    _guestLight.gameObject.SetActive(t > 0.002f);
            }
        }

        private System.Collections.IEnumerator Ambient()
        {
            float nextFlicker = Random.Range(3f, 7f);
            while (true)
            {
                nextFlicker -= Time.unscaledDeltaTime * AmbientScale;
                if (nextFlicker <= 0f && _globalLight != null)
                {
                    // OFF THE HOUR'S OWN LEVEL, not off a constant (2026-08-19). The flicker
                    // dipped to GlobalIntensity*0.72 and then RESTORED to GlobalIntensity —
                    // which was fine while the wash was a constant and is a bug now that the
                    // sky sets it: the first flicker of the night pinned the room at 0.85
                    // for good, and every hour after it lit the same. It dips from where the
                    // evening left the room and puts it back there.
                    _globalLight.intensity = _washBase * 0.72f;
                    yield return new WaitForSecondsRealtime(0.05f);
                    if (_globalLight != null) _globalLight.intensity = _washBase;
                    nextFlicker = Random.Range(3f, 7f);
                }
                yield return null;
            }
        }

        private RectTransform FullLayer(RectTransform root, string name, Color fill)
        {
            var layer = NewRect(name, root);
            Stretch(layer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = layer.gameObject.AddComponent<Image>();
            img.color = fill;
            img.raycastTarget = false;
            return layer;
        }

        private void Window(RectTransform layer, Vector2 size, Vector2 pos)
        {
            var w = NewRect("Window", layer);
            Place(w, new Vector2(0, 0), size, pos);
            var img = w.gameObject.AddComponent<Image>();
            img.color = new Color(UITheme.ClubBlue[1].r, UITheme.ClubBlue[1].g, UITheme.ClubBlue[1].b, 0.55f);
            img.raycastTarget = false;
        }

        private void AddCrowd(RectTransform layer)
        {
            // Dim head + shoulder silhouettes across the dance floor, well above the bar
            // surface so they read as patrons in the club behind the counter.
            for (int i = 0; i < 9; i++)
            {
                var head = NewRect($"Head{i}", layer);
                Place(head, new Vector2(0, 0), new Vector2(22, 34), new Vector2(40 + i * 70, 196));
                var img = head.gameObject.AddComponent<Image>();
                img.color = i % 2 == 0 ? UITheme.Night[2] : UITheme.Night[3];
                img.raycastTarget = false;
            }
        }

        private void AddNeonSigns(RectTransform layer)
        {
            // Two small accents high on the back wall — the procedural stand-ins for a
            // missing room picture. The "LAST CALL" sign that hung between them went with
            // the real one (2026-08-19): a fallback that shows a sign the room no longer
            // has is a fallback that lies about the room.
            NeonSign(layer, UITheme.Cyan[3], new Vector2(56, 10), new Vector2(120, 322), null);
            NeonSign(layer, UITheme.Magenta[3], new Vector2(48, 10), new Vector2(548, 316), null);
        }

        private void NeonSign(RectTransform layer, Color c, Vector2 size, Vector2 center, string label)
        {
            // Glow halo (dim, larger) + bright core — glow = hand-placed halo, no shader.
            var halo = NewRect("Halo", layer);
            Place(halo, new Vector2(0.5f, 0.5f), size + new Vector2(12, 12), center);
            var haloImg = halo.gameObject.AddComponent<Image>();
            haloImg.color = new Color(c.r, c.g, c.b, 0.25f); haloImg.raycastTarget = false;
            var core = NewRect("Sign", layer);
            Place(core, new Vector2(0.5f, 0.5f), size, center);
            var coreImg = core.gameObject.AddComponent<Image>();
            coreImg.color = c; coreImg.raycastTarget = false;
            if (!string.IsNullOrEmpty(label))
            {
                var t = NewText("Label", core, _display, 12, TextAnchor.MiddleCenter, UITheme.Night[0]);
                Stretch((RectTransform)t.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        private Text NewText(string name, Transform parent, Font font, int size, TextAnchor anchor, Color color)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = LanguageFonts.Size(font, size);   // L3
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
