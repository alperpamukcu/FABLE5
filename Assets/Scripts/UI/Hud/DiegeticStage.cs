using System.Collections.Generic;
using UnityEngine;
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
        private const float RegisterX = 604f;              // till pushed to the counter's right edge

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
        // The counter art is not decoration: it carries EIGHT empty cells across its
        // front, and those cells are where the bought glassware belongs. Measured off
        // Assets/Art/Backgrounds/counter.png (640x150) by sampling the luminance profile,
        // not estimated — the dividers land at x 77-82, 157-162, 237-242, 317-322,
        // 397-402, 477-482 and 557-562, so the cells are 80 wide on 80 centres; the upper
        // cell's interior runs y 53..92 with its floor at y 93.
        //
        // The numbers below are in the ART's own pixels. They only equal stage units at
        // the reference aspect: the counter is scaled by visibleWidth/640 and hangs from
        // the rest line, so at 16:10 the cells sit narrower AND higher. ShelfCell resolves
        // that from the live fit rather than assuming 16:9.
        private const float ShelfCellPx = 80f;      // cell pitch, art px
        // THE FLOOR IS THE TURQUOISE, AND IT IS DRAWN IN PERSPECTIVE (the author,
        // 2026-08-10). Rows 93..105 are (13,30,40) — thirteen rows of it — and rows
        // 106..112 are the wooden board (141,82,30 lipping to 161,107,57). The wood is not
        // the floor: it is the shelf's FRONT EDGE, seen from slightly above, and the
        // turquoise band behind it is the surface things stand on.
        private const float ShelfFloorPx = 104f;    // the near edge, art px from the art's top
        /// <summary>How deep the drawn shelf surface is, front edge to back, in art px.</summary>
        public const float ShelfDepthPx = 9f;
        private const float ShelfCeilPx = 53f;      // the shelf board above it

        private Transform _counterTr;
        private Vector2 _counterNative;
        private float _counterScale;                // stage units per counter-art pixel

        /// <summary>
        /// Where shelf compartment <paramref name="index"/> is standing right now, in STAGE
        /// units: the centre of its opening, the floor a glass stands on, and how much
        /// headroom there is under the shelf board. False when the bar was never drawn.
        /// </summary>
        public bool ShelfCell(int index, out float centerX, out float floorY, out float height)
        {
            centerX = 0f; floorY = 0f; height = 0f;
            if (_counterTr == null || _counterNative.x <= 0f) return false;
            float scale = _counterScale;
            // The art's own top edge, in stage units: the rest line is CounterSurfaceInset
            // art-pixels below it, and that line is pinned to CounterRestY.
            float artTopY = CounterRestY + CounterSurfaceInset * scale;
            centerX = ((index + 0.5f) * ShelfCellPx - _counterNative.x * 0.5f) * scale;
            floorY = artTopY - ShelfFloorPx * scale;
            height = (ShelfFloorPx - ShelfCeilPx) * scale;
            return true;
        }

        /// <summary>Where the till's base sits. The bar top runs from CounterFrontY (96) up to
        /// CounterRestY (128), and this sits well forward inside that — near the front of the
        /// surface, where something on the bartender's side of the bar actually stands.</summary>
        private const float RegisterBaseY = 104f;

        // The till's display window, as fractions of the sprite — measured off the register
        // art (x 8..42 of 49, y 5..12 of 43, y from the TOP). Read them again if the till is
        // redrawn; the money is placed from these so it lands in the window rather than near it.
        private const float DisplayLeft = 8f / 49f, DisplayRight = 43f / 49f;
        private const float DisplayTop = 5f / 43f, DisplayBottom = 12f / 43f;

        private const float CounterRestY = 128f;           // counter-top rest line (till, glassware)
        // Measured off the art: the bar's far edge — where a glass is set down — is this far
        // below the sprite's top (2026-07-29).
        private const float CounterSurfaceInset = 2f;
        private const float CounterFrontY = 96f;           // surface line: the bottom 96px band
        private const float Overscan = 48f;         // bleed past screen edges (aspect safety)

        // ── the light plan (2026-08-10) ─────────────────────────────────────────
        // The room's four hanging lamps, measured off club_room.png by clustering its
        // warm-bright pixels (Tools would guess; the art knows): bulb centres at art
        // x 65 / 237 / 406 / 579, y 84 from the top. Each gets a warm pool; the global
        // wash is slightly cool and slightly below 1 so the pools read as light and not
        // as paint. The sign's spill rides NeonBlink with its lettering.
        private static readonly Vector2[] LampArtPx =
            { new Vector2(65, 84), new Vector2(237, 84), new Vector2(406, 84), new Vector2(579, 84) };
        private static readonly Color GlobalTint = new Color(0.86f, 0.85f, 0.95f);
        private const float GlobalIntensity = 0.85f;
        private static readonly Color LampTint = new Color(1f, 0.80f, 0.52f);
        private const float LampIntensity = 0.55f;
        private const float LampRadius = 92f;

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

        private readonly List<(LastCall.Core.FixtureDefinition Def, Transform Body, Light2D Glow)>
            _placedFixtures = new List<(LastCall.Core.FixtureDefinition, Transform, Light2D)>();

        private Font _display;
        [SerializeField] private Font displayFont;         // Press Start 2P (headings/numbers)

        /// <summary>Installed environment art. When set, the full-screen club background and
        /// the bar counter replace their flat procedural placeholders.</summary>
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite counterSprite;
        [SerializeField] private Sprite registerSprite;   // cash register, shows the wallet
        private Text _moneyText;

        /// <summary>Per-archetype ID photos for the licence card. Falls back to a flat silhouette.</summary>
        [System.Serializable]
        public struct PortraitSprite { public string archetypeId; public Sprite sprite; }
        [SerializeField] private PortraitSprite[] portraits;
        private readonly Dictionary<string, Sprite> _portraits = new Dictionary<string, Sprite>();

        private NeonBlink _neon;

        // ── the world ───────────────────────────────────────────────────────────
        private Transform _world;                   // root of every world-space stage object
        private Material _litMaterial;              // Sprite-Lit-Default, shared by the stage
        private SpriteRenderer _backdropSr, _backgroundSr;
        private Vector2 _backgroundNative;
        private float _backgroundScale = 1f;        // stage units per background-art pixel
        private Light2D _globalLight;
        private Light2D _signLight;
        private RectTransform _signHost;            // the sign's canvas host, Cover-fitted
        private float _lastVisibleW = -1f;

        /// <summary>Update the diegetic wallet shown on the cash register plaque.</summary>
        public void SetMoney(string text)
        {
            if (_moneyText != null) _moneyText.text = text;
        }

        private System.Action _onRegisterClicked;

        /// <summary>Wires the till click to the ledger-history popup (GDD 24 §7).</summary>
        public void SetRegisterHandler(System.Action onClick) => _onRegisterClicked = onClick;

        /// <summary>The ID photo for an archetype, for the tycoon floor's licence card.</summary>
        public Sprite PortraitSpriteFor(string archetypeId) =>
            !string.IsNullOrEmpty(archetypeId) && _portraits.TryGetValue(archetypeId, out var s) ? s : null;

        private void Awake()
        {
            Application.runInBackground = true; // keep animations advancing unfocused
            FillTheWindow();
            _display = displayFont != null
                ? displayFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (portraits != null)
                foreach (var p in portraits)
                    if (p.sprite != null && !string.IsNullOrEmpty(p.archetypeId)) _portraits[p.archetypeId] = p.sprite;
            BuildScene();
        }

        private void Update()
        {
            _neon?.Step(Time.deltaTime);
            StepClosing();
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
        }

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

            // Opaque backdrop behind everything, overscanned past the screen edges so no
            // aspect-ratio border ever exposes the clear colour / editor checker.
            _backdropSr = WorldSprite("Backdrop",
                Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f),
                order: 0);
            _backdropSr.color = UITheme.Night[0];

            _neon = new NeonBlink();

            // The room. Real club background when installed, else the flat procedural
            // placeholders on their own canvas, so a broken reference is still a visible bar.
            if (backgroundSprite != null)
            {
                _backgroundSr = WorldSprite("Background", backgroundSprite, order: 10);
                _backgroundNative = backgroundSprite.rect.size;

                // The lamps the picture already painted, made real: a warm pool under each
                // bulb. Positions are measured art pixels, converted per-fit in Refit.
                for (int i = 0; i < LampArtPx.Length; i++)
                    _lamps.Add(PointLight("Lamp" + i, LampTint, LampIntensity, LampRadius));
                _signLight = PointLight("SignSpill", UITheme.Magenta[4], 0.9f, 60f);

                BuildSign();
            }
            else
            {
                BuildFallbackRoom();
            }

            // The global wash: what "the room is dim" means to the lighting system. Slightly
            // cool and slightly below 1, so the warm pools have something to be warmer than.
            _globalLight = new GameObject("GlobalLight").AddComponent<Light2D>();
            _globalLight.transform.SetParent(_world, false);
            _globalLight.lightType = Light2D.LightType.Global;
            _globalLight.color = GlobalTint;
            _globalLight.intensity = GlobalIntensity;
            LightAllLayers(_globalLight);

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
                _counterNative = counterSprite.rect.size;
            }

            BuildRegister();

            Refit(VisibleWidth());

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
        public SpriteRenderer NewStageSprite(string name, int order) =>
            WorldSprite(name, null, order);

        /// <summary>One world-space stage sprite on the shared lit material.</summary>
        private SpriteRenderer WorldSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
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

            if (_backgroundSr != null)
            {
                // Cover: the larger ratio fills both axes, overflow is cropped by the frame.
                var b = _backgroundSr.sprite.bounds.size;
                float k = Mathf.Max(visibleW / b.x, visibleH / b.y);
                _backgroundSr.transform.localScale = new Vector3(k, k, 1f);
                _backgroundScale = k * (b.x / _backgroundNative.x);   // stage units per art px

                // The lamps hang where the picture drew them: art px from the TOP, through
                // the same cover fit the picture itself got.
                for (int i = 0; i < LampArtPx.Length; i++)
                {
                    var lamp = _world.Find("Lamp" + i);
                    if (lamp == null) continue;
                    lamp.position = ArtPxToWorld(LampArtPx[i]);
                }
                if (_signHost != null)
                {
                    // The sign's spill sits at the lettering's centre. The sign hangs at art
                    // (470,300) bottom-left origin, and its rect is 93×20.
                    var c = StageArtPointToWorld(new Vector2(470f + 46.5f, 300f + 10f));
                    if (_signLight != null) _signLight.transform.position = c;
                }
            }

            if (_counterTr != null)
            {
                var sr = _counterTr.GetComponent<SpriteRenderer>();
                var b = sr.sprite.bounds.size;
                float k = visibleW / b.x;                      // WidthAligned: span the window
                _counterTr.localScale = new Vector3(k, k, 1f);
                _counterScale = k * (b.x / _counterNative.x);  // stage units per art px
                // Hung from the rest line: the art's top is CounterSurfaceInset above it.
                float artTopStage = CounterRestY + CounterSurfaceInset * _counterScale;
                float artHStage = _counterNative.y * _counterScale;
                _counterTr.position = new Vector3(0f, artTopStage - artHStage * 0.5f - Reference.y * 0.5f, 0f);
            }

            PlaceFixtures();
        }

        /// <summary>Background-art pixel (y from the TOP, the way art is measured) → world.</summary>
        private Vector3 ArtPxToWorld(Vector2 artPx) =>
            StageArtPointToWorld(new Vector2(artPx.x, _backgroundNative.y - artPx.y));

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
                var sprite = Resources.Load<Sprite>("Fixtures/" + def.Sprite);
                if (sprite == null)
                {
                    Debug.LogWarning($"DiegeticStage: fixture '{def.Id}' has no sprite " +
                                     $"'Fixtures/{def.Sprite}' — sold but not drawn.");
                    continue;
                }
                // Room dressing draws between the picture (10) and the bar (30); a piece
                // standing ON the counter must draw over the counter that holds it — the
                // candle was sorting behind the bar top and simply vanished (measured on
                // the first proof shot, 2026-08-10). The slot says which it is, so a new
                // counter-top place needs no code either.
                bool onCounter = _slots[def.Slot].OnCounter;
                var sr = WorldSprite("Fx_" + def.Id, sprite, order: onCounter ? 35 : 20);

                Light2D glow = null;
                if (def.HasLight)
                {
                    glow = PointLight("FxGlow_" + def.Id,
                        new Color(def.LightR, def.LightG, def.LightB),
                        def.LightIntensity, def.LightRadius);
                }
                _placedFixtures.Add((def, sr.transform, glow));

                // WHAT SELLS "STANDING ON" RATHER THAN "FLOATING NEAR": only pieces that
                // touch a surface get one. A sconce on the wall and a lantern on a cord
                // touch nothing, and a blob under them would read as a stain.
                if (!onCounter && !def.HasLight)
                    ContactShadow(sr.transform, sprite.rect.width * 0.9f);
            }
            PlaceFixtures();
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
                var basePos = _backgroundSr != null
                    ? StageArtPointToWorld(new Vector2(slot.X, slot.Y))
                    : StageToWorld(slot.X, slot.Y);
                float k = _backgroundSr != null ? _backgroundScale : 1f;
                placed.Body.localScale = new Vector3(k, k, 1f);
                float h = placed.Body.GetComponent<SpriteRenderer>().sprite.bounds.size.y * k;
                placed.Body.position = basePos + new Vector3(0f, h * 0.5f, 0f);
                // The glow hangs at the piece's own light line — the flame, the belly of
                // the shade — which for every launch fixture is about ⅔ up the sprite.
                if (placed.Glow != null)
                    placed.Glow.transform.position = basePos + new Vector3(0f, h * 0.66f, 0f);
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

        /// <summary>A point in the background art's own bottom-left space → world, through the
        /// cover fit (scaled about the centre, so the mapping is scale-about-centre too).</summary>
        private Vector3 StageArtPointToWorld(Vector2 artPoint) => new Vector3(
            (artPoint.x - _backgroundNative.x * 0.5f) * _backgroundScale,
            (artPoint.y - _backgroundNative.y * 0.5f) * _backgroundScale, 0f);

        // ── the lettered sign (canvas: pixel text needs the canvas rasterizer) ─────

        /// <summary>
        /// What the painted room cannot do for itself: blink (GDD 24 §8). The word is real
        /// text in the pixel font — the art generator cannot spell — so it keeps a small
        /// overlay canvas of its own at −9: above the world stage, under the dressing (−5)
        /// and everything else, exactly where the old scene canvas held it. Its LIGHT lives
        /// in the world and stutters on the same schedule.
        /// </summary>
        private void BuildSign()
        {
            var canvasGo = new GameObject("SignCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -9;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var root = DesignFrame.Wrap((RectTransform)canvasGo.transform, Reference);

            // Sized and scaled exactly like the room art, so the sign hangs on the wall the
            // picture actually draws rather than on the screen edge.
            _signHost = NewRect("SignHost", root);
            var hostFit = _signHost.gameObject.AddComponent<StageArtFit>();
            hostFit.Native = _backgroundNative;

            BuildLetteredNeon(_signHost, new Vector2(470f, 300f), "LAST CALL", UITheme.Magenta[4]);
        }

        /// <summary>
        /// A neon sign whose word is real text: the word set in the display face, and a soft
        /// copy behind it for the glow. Both blink together, so the sign reads as one object —
        /// and the world-side spill light blinks with the word.
        /// </summary>
        private void BuildLetteredNeon(RectTransform host, Vector2 centre, string word, Color tint)
        {
            var sign = NewRect("NeonSign", host);
            sign.anchorMin = sign.anchorMax = sign.pivot = new Vector2(0f, 0f);
            // Sized to the word: a lettered sign IS its lettering, so there is no frame art to
            // match. The pixel face only stays crisp at whole multiples of 8.
            sign.sizeDelta = new Vector2(word.Length * 9f + 12f, 20f);
            sign.anchoredPosition = centre;

            // The glow first, larger and dim, then the word itself over it.
            var glow = NewText("Glow", sign, _display, 8, TextAnchor.MiddleCenter,
                new Color(tint.r, tint.g, tint.b, 0.35f));
            Stretch(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-2, -2), new Vector2(2, 2));
            glow.text = word;

            var label = NewText("Word", sign, _display, 8, TextAnchor.MiddleCenter, tint);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.text = word;

            _neon.Register(glow, 3f, 9f, 0.10f);
            _neon.Register(label, 3f, 9f, 0.25f, _signLight);
        }

        // ── the till (canvas: it must draw OVER the HUD's seated patrons) ─────────

        private void BuildRegister()
        {
            if (registerSprite == null) return;

            // THE TILL STANDS ON THE BAR, SO IT STANDS IN FRONT OF THE DRINKERS.
            // The customers are HUD objects at sorting 5 — so the register gets its own
            // canvas at 6: over the seats, under the service flow (12) and the licence (20).
            // Its shadow and the wallet plaque draw at −7: under the patrons and the
            // dressing (−5), visible through the till's display window — and ABOVE the
            // fallback room, which also sits at −10. On the old single canvas the plaque
            // outdrew the fallback by sibling order; two canvases on the same order have
            // no defined order at all, and a lost art reference would have taken the
            // wallet with it.
            var backRoot = OverlayCanvas("RegisterBack", -7, raycasts: false);
            var frontRoot = OverlayCanvas("RegisterLayer", 6, raycasts: true);

            var reg = NewRect("Register", frontRoot);
            reg.anchorMin = reg.anchorMax = new Vector2(0, 0);
            reg.pivot = new Vector2(0.5f, 0);
            // Fixed footprint (hi-bit): a 2x-density sprite renders finer pixels
            // into the same 57px slot instead of doubling on screen.
            const float regW = 57f;
            reg.sizeDelta = new Vector2(regW, regW * registerSprite.rect.height / registerSprite.rect.width);
            reg.anchoredPosition = new Vector2(RegisterX, RegisterBaseY);
            var regImg = reg.gameObject.AddComponent<Image>();
            regImg.sprite = registerSprite; regImg.preserveAspect = true;
            // The till is clickable: it opens the ledger of days gone by (GDD 24 §7).
            regImg.raycastTarget = true;
            var regBtn = reg.gameObject.AddComponent<Button>();
            regBtn.targetGraphic = regImg;
            regBtn.transition = Selectable.Transition.None;
            regBtn.onClick.AddListener(() => _onRegisterClicked?.Invoke());

            // A soft contact shadow under it — the thing that actually sells "resting on"
            // rather than "floating near".
            var shadow = NewRect("TillShadow", backRoot);
            shadow.anchorMin = shadow.anchorMax = new Vector2(0, 0);
            shadow.pivot = new Vector2(0.5f, 0.5f);
            shadow.sizeDelta = new Vector2(regW * 0.92f, 5f);
            shadow.anchoredPosition = new Vector2(RegisterX, RegisterBaseY + 1f);
            var shImg = shadow.gameObject.AddComponent<Image>();
            shImg.color = new Color(0f, 0f, 0f, 0.42f); shImg.raycastTarget = false;

            // The money sits IN the till's display window, and the window's place is read
            // off the art rather than guessed at (2026-07-29).
            float regH = reg.sizeDelta.y;
            var plaque = NewRect("MoneyPlaque", backRoot);
            plaque.anchorMin = plaque.anchorMax = new Vector2(0, 0);   // absolute, on the till
            plaque.pivot = new Vector2(0.5f, 0);
            plaque.sizeDelta = new Vector2(regW * (DisplayRight - DisplayLeft),
                                           regH * (DisplayBottom - DisplayTop));
            // The window's fractions run from the sprite's TOP; the rect measures from its
            // base, so the bottom of the window is 1 - DisplayBottom up from it.
            plaque.anchoredPosition = new Vector2(
                RegisterX + regW * ((DisplayLeft + DisplayRight) * 0.5f - 0.5f),
                RegisterBaseY + regH * (1f - DisplayBottom));
            var pImg = plaque.gameObject.AddComponent<Image>();
            pImg.color = UITheme.Night[0]; pImg.raycastTarget = false;
            // 8, not 10: the window is about eight units deep, and the pixel face only
            // rasterises cleanly at whole multiples of its 8px design size anyway.
            _moneyText = NewText("Money", plaque, _display, 8, TextAnchor.MiddleCenter, UITheme.Money);
            Stretch((RectTransform)_moneyText.transform, Vector2.zero, Vector2.one, new Vector2(2, 0), new Vector2(-2, 0));
            _moneyText.text = "$0";
        }

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
            if (raycasts) go.AddComponent<GraphicRaycaster>();
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

        /// <summary>How far the ceiling and the wash drop for the last call.</summary>
        private const float ClosingCeiling = 0.22f, ClosingWash = 0.55f, ClosingSign = 1.9f;

        /// <summary>The ceiling, kept by reference: the closing beat dims every one of them
        /// each frame, and finding them by name would rebuild four strings a frame to do it.</summary>
        private readonly List<Light2D> _lamps = new List<Light2D>();

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
        public void SetClosingBeat(bool on, float hudX)
        {
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

            if (_globalLight != null)
                _globalLight.intensity = Mathf.Lerp(GlobalIntensity, GlobalIntensity * ClosingWash, t);
            float ceilingY = 0f;
            for (int i = 0; i < _lamps.Count; i++)
            {
                if (_lamps[i] == null) continue;
                _lamps[i].intensity = Mathf.Lerp(LampIntensity, LampIntensity * ClosingCeiling, t);
                ceilingY = _lamps[i].transform.position.y;
            }
            if (_signLight != null)
                _signLight.intensity = Mathf.Lerp(0.9f, 0.9f * ClosingSign, t);

            if (_guestLight == null && _world != null && t > 0.001f)
                _guestLight = PointLight("LastCallLamp", LampTint, 0f, LampRadius * 1.15f);
            if (_guestLight != null)
            {
                // It hangs where the other lamps hang — the room has one ceiling, and a pool
                // that floated at mid-wall would read as a spotlight from nowhere.
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
                nextFlicker -= Time.unscaledDeltaTime;
                if (nextFlicker <= 0f && _globalLight != null)
                {
                    _globalLight.intensity = GlobalIntensity * 0.72f;
                    yield return new WaitForSecondsRealtime(0.05f);
                    if (_globalLight != null) _globalLight.intensity = GlobalIntensity;
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
            // The wall sign "LAST CALL" + a couple of small accents high on the back wall —
            // the procedural stand-ins for a missing room picture.
            NeonSign(layer, UITheme.Magenta[3], new Vector2(200, 22), new Vector2(320, 300), "LAST CALL");
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
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
