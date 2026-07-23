using System.Collections.Generic;
using System.Text;
using LastCall.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LastCall.DebugUI
{
    /// <summary>
    /// The diegetic gameplay stage (v2, module 18): a layered night-club scene authored at
    /// 640×360 that sits BEHIND the UI overlay. Layers back → front (18 §1):
    ///   Sky/City → Club Far (crowd) → Club Mid (neon "LAST CALL") → Back bar (two
    ///   wall shelves of bottles, seen from the customer's side) → Counter (amber-lit
    ///   bottom 96px) → Customer.
    /// The UI overlay (HUD/panels) draws on its own higher-order canvas.
    ///
    /// Everything here is placeholder: flat silhouettes drawn straight from the locked v2
    /// palette (<see cref="UITheme"/>). Final pixel sprites drop in without touching the
    /// layout or the slide system. Bottle motion is DOTween-shaped (see <see cref="Tweening"/>)
    /// and collapses to instant snaps under <see cref="Motion.Reduced"/>. All coordinates
    /// are in the 640×360 reference space with a bottom-left origin (18 §2 y-values are
    /// top-down, so y_here = 360 − y_spec).
    /// </summary>
    public sealed class DiegeticStage : MonoBehaviour
    {
        // ── layout (18 §2, converted to bottom-left origin) ─────────────────────
        // Customer POV (GDD 22): the camera sits where the patron does, so the liquid
        // bottles live on two wall shelves on the back bar — spirits up top, mixers
        // below — with the counter running along the bottom of the frame.
        private const int Slots = 10;                      // liquid bottles on the back bar
        private static readonly Vector2 Reference = new Vector2(640, 360);
        private const int ShelfCols = 5;                   // bottles per shelf row
        private const float ShelfFirstX = 166f;            // clears the pour glass, top-left
        private const float ShelfPitch = 64f;
        // Row spacing leaves the upper shelf's tag band clear of the lower row's bottle
        // caps (bottles are 60px tall at the 40px display width; tags hang 21px deep).
        private const float ShelfRow0Y = 246f;             // upper shelf rest line
        private const float ShelfRow1Y = 158f;             // lower shelf rest line

        // The garnish rack (GDD 22): mint, olives and future rim garnishes live on a shelf
        // under the counter, where a bartender actually keeps them — and where the Patrons
        // and Tools tables used to crowd the band before they moved into the STAFF popup.
        // Tycoon floor (PLAN P3): the bottom band belongs to the seat row now, so the
        // garnish jars stand on the counter top, left margin, under the pour glass.
        private const int GarnishSlots = 4;
        private const float GarnishPitch = 56f;
        private const float GarnishFirstX = 30f;
        private const float GarnishBaseY = 100f;
        private const float GarnishDisplayW = 30f;
        private const float CustomerX = 556f;              // patron centre, standing at the bar
        private const float CustomerBaseY = 126f;          // hands rest on the bar-top surface
        private const float RegisterX = 604f;              // till pushed to the counter's right edge
        private const float CounterRestY = 128f;           // counter-top rest line (till, glassware)
        private const float CounterFrontY = 96f;           // 18 §2: surface line y=264 → 360−264 (bottom 96px)
        private const float BottleW = 30f;                 // placeholder fallback size
        private const float BottleH = 52f;
        private const float BottleDisplayW = 40f;          // fixed slot width for sprite bottles
        private const float SelectRise = 4f;               // 18 §3: select rises 4px
        private const float OffscreenRight = 680f;
        private const float OffscreenLeft = -40f;
        private const float Overscan = 48f;         // bleed past screen edges (aspect safety)
        private const float CounterSurfaceInset = 12f; // rest line = px below the counter sprite top (arc centre)

        // ── choreography timings (18 §3) ────────────────────────────────────────
        private const float EnterStagger = 0.06f;          // 60 ms per bottle
        private const float DrawDur = 0.24f;               // 240 ms OutQuad each
        private const float SettleDur = 0.06f;             // 2px overshoot settle
        private const float Overshoot = 2f;                // px past the slot on entry
        private const float MixExitDur = 0.18f;            // 180 ms InQuad off-screen
        private const float MixPopDur = 0.14f;             // forward+up flourish

        private Font _body;
        private Font _display;
        [SerializeField] private Font bodyFont;            // Silkscreen (16 v2 §1 BODY/CAPTION)
        [SerializeField] private Font displayFont;         // Press Start 2P (headings/numbers)

        /// <summary>Installed pixel bottle sprites (18 §5), keyed by ingredient type. Types
        /// without a sprite fall back to the flat-silhouette placeholder.</summary>
        [System.Serializable]
        public struct BottleSprite { public IngredientType type; public Sprite sprite; }
        [SerializeField] private BottleSprite[] bottleSprites;
        private readonly Dictionary<IngredientType, Sprite> _bottleSprites = new Dictionary<IngredientType, Sprite>();

        /// <summary>Per-ingredient bottle sprites (18 §5): same colour ramp per type, a
        /// distinct recognizable silhouette per ingredient. Keyed by ingredient id; falls
        /// back to the per-type sprite, then the flat placeholder.</summary>
        [System.Serializable]
        public struct IdSprite { public string id; public Sprite sprite; }
        [SerializeField] private IdSprite[] bottleById;
        private readonly Dictionary<string, Sprite> _bottleById = new Dictionary<string, Sprite>();

        /// <summary>Installed environment art (18 §5). When set, the full-screen club
        /// background and the bar counter replace their flat procedural placeholders.</summary>
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite counterSprite;
        [SerializeField] private Sprite customerSprite;   // VIP/patron leaning on the bar (18 §6)
        [SerializeField] private Sprite registerSprite;   // cash register, bottom-left, shows the wallet
        private Text _moneyText;

        // Ambience upgrades change the scene (GDD 24 §6): the counter, back bar and glass
        // gain a sheen per tier, and a bought musician takes the corner stage.
        private Image _counterImage;
        private Image _cabinetImage;
        private Image _glassImage;
        private RectTransform _musicianRoot;

        private RectTransform _railRoot;
        private readonly Dictionary<int, BottleView> _bottles = new Dictionary<int, BottleView>();
        private string _railSignature = "";

        // Ambient life (18 §4): at most a neon flicker + a customer idle, both subtle and
        // both silenced under reduced motion.
        private Image _backgroundImage;
        private RectTransform _customerRect;

        private sealed class BottleView
        {
            public IngredientCard Card;
            public RectTransform Root;
            public Image Rim;
            public Image Outline;
            public Image Body;
            public Image Neck;
            public Image SpriteImg;    // non-null when a real pixel sprite is installed
            public Text Value;
            public Text Name;
            public bool Selected;
            public float RestBaseY;    // arc rest y for this slot (select adds SelectRise)
            public Coroutine Move;
            public Coroutine Rise;
        }

        /// <summary>How exiting bottles leave, so the wave reads as the right action.</summary>
        public enum Exit { None, Mix, Restock, Refresh }

        /// <summary>Show/hide the bottle rail (hidden while the Back Room modal is up).</summary>
        public void SetRailVisible(bool visible)
        {
            if (_railRoot != null) _railRoot.gameObject.SetActive(visible);
        }

        /// <summary>Update the diegetic wallet shown on the cash register plaque.</summary>
        public void SetMoney(string text)
        {
            if (_moneyText != null) _moneyText.text = text;
        }

        private System.Action _onRegisterClicked;

        /// <summary>Wires the till click to the ledger-history popup (GDD 24 §7).</summary>
        public void SetRegisterHandler(System.Action onClick) => _onRegisterClicked = onClick;

        /// <summary>
        /// Retires the pre-menu stage dressing (2026-07-22): the pour glass HUD (top-left
        /// goblet) and the on-counter bottle rail + garnish jars belonged to the old
        /// shelf-click loop. Bottles live in the menu now, so the stage keeps only the bar
        /// itself. Real back-bar scenery comes in the P8 art pass.
        /// </summary>
        public void HideBuildDressing()
        {
            if (_glassRoot != null) _glassRoot.gameObject.SetActive(false);
            if (_glassRatios != null) _glassRatios.gameObject.SetActive(false);
            if (_railRoot != null) _railRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Hides the single-patron props for the tycoon floor (PLAN P3): the seat row owns
        /// the customers now; the per-seat licence returns in P6.
        /// </summary>
        public void SetSoloCustomerVisible(bool visible)
        {
            if (_customerRect != null) _customerRect.gameObject.SetActive(visible);
            if (visible) return;
            if (_idPrompt != null) _idPrompt.gameObject.SetActive(false);
            if (_moodRoot != null) _moodRoot.gameObject.SetActive(false);
            CloseId();
        }

        /// <summary>
        /// Applies the bought ambience upgrades to the scene (GDD 24 §6): a warmer counter,
        /// a richer back bar, a crystal sheen on the glass, and the musician on stage. Each
        /// buyable has a visible counterpart — the scene is the save file.
        /// </summary>
        public void ApplyBarLook(int glasswareTier, int counterTier, int wallTier, bool musician)
        {
            if (_counterImage != null)
                _counterImage.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.72f),
                    (counterTier - 1) * 0.45f);
            if (_cabinetImage != null)
            {
                var rich = Color.Lerp(UITheme.Night[1], UITheme.Magenta[0], (wallTier - 1) * 0.4f);
                _cabinetImage.color = new Color(rich.r, rich.g, rich.b, 0.62f + (wallTier - 1) * 0.08f);
            }
            if (_glassImage != null)
                _glassImage.color = Color.Lerp(Color.white, UITheme.Cyan[4], (glasswareTier - 1) * 0.28f);
            if (_musicianRoot != null) _musicianRoot.gameObject.SetActive(musician);
        }

        private void BuildMusician(RectTransform root)
        {
            // A small performer on a corner stage, high on the back wall so it never crowds
            // the seats or the bottles. Placeholder silhouette; P8 animates it.
            _musicianRoot = NewRect("Musician", root);
            Place(_musicianRoot, new Vector2(0, 0), new Vector2(46, 78), new Vector2(96, 250));

            var glow = NewRect("Glow", _musicianRoot);
            Place(glow, new Vector2(0.5f, 0.5f), new Vector2(58, 90), Vector2.zero);
            var glowImg = glow.gameObject.AddComponent<Image>();
            glowImg.color = new Color(UITheme.Magenta[3].r, UITheme.Magenta[3].g, UITheme.Magenta[3].b, 0.22f);
            glowImg.raycastTarget = false;

            var body = NewRect("Body", _musicianRoot);
            Place(body, new Vector2(0.5f, 0), new Vector2(30, 52), new Vector2(0, 0));
            body.gameObject.AddComponent<Image>().color = UITheme.Night[3];
            var head = NewRect("Head", _musicianRoot);
            Place(head, new Vector2(0.5f, 1), new Vector2(20, 20), new Vector2(0, -6));
            head.gameObject.AddComponent<Image>().color = UITheme.Cream[2];
            var instrument = NewRect("Sax", _musicianRoot);
            Place(instrument, new Vector2(0.5f, 0), new Vector2(12, 30), new Vector2(14, 10));
            instrument.gameObject.AddComponent<Image>().color = UITheme.Amber[3];
            var note = NewRect("Note", _musicianRoot);
            Place(note, new Vector2(0.5f, 1), new Vector2(10, 12), new Vector2(20, 4));
            note.gameObject.AddComponent<Image>().color = UITheme.Magenta[4];

            foreach (var img in _musicianRoot.GetComponentsInChildren<Image>()) img.raycastTarget = false;
            _musicianRoot.gameObject.SetActive(false);
        }

        private void Awake()
        {
            Application.runInBackground = true; // keep the slide animations advancing unfocused
            var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _body = bodyFont != null ? bodyFont : legacy;
            _display = displayFont != null ? displayFont : legacy;
            if (bottleSprites != null)
                foreach (var b in bottleSprites)
                    if (b.sprite != null) _bottleSprites[b.type] = b.sprite;
            if (bottleById != null)
                foreach (var b in bottleById)
                    if (b.sprite != null && !string.IsNullOrEmpty(b.id)) _bottleById[b.id] = b.sprite;
            if (portraits != null)
                foreach (var p in portraits)
                    if (p.sprite != null && !string.IsNullOrEmpty(p.archetypeId)) _portraits[p.archetypeId] = p.sprite;
            BuildScene();
        }

        // ── scene construction ──────────────────────────────────────────────────

        private void BuildScene()
        {
            var canvasGo = new GameObject("SceneCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10; // behind the HUD overlay (order 0)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;     // 640×360 → ×3 = 1080p, integer scaling
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;             // match height: keeps the 96px counter band exact
            var root = (RectTransform)canvasGo.transform;

            // Opaque backdrop behind everything, overscanned past the screen edges so no
            // aspect-ratio border ever exposes the clear colour / editor checker.
            var backdrop = NewRect("Backdrop", root);
            Stretch(backdrop, Vector2.zero, Vector2.one, new Vector2(-Overscan, -Overscan), new Vector2(Overscan, Overscan));
            var backdropImg = backdrop.gameObject.AddComponent<Image>();
            backdropImg.color = UITheme.Night[0]; backdropImg.raycastTarget = false;

            // Layers 0-2, 6 — environment. Real club background when installed, else the
            // flat procedural sky / crowd / neon / customer placeholders.
            if (backgroundSprite != null)
            {
                var bg = NewRect("Background", root);
                Stretch(bg, Vector2.zero, Vector2.one, new Vector2(-Overscan, -Overscan), new Vector2(Overscan, Overscan));
                var bgImg = bg.gameObject.AddComponent<Image>();
                bgImg.sprite = backgroundSprite; bgImg.raycastTarget = false;
                _backgroundImage = bgImg;
            }
            else
            {
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

                AddCustomer(root);
            }

            // The back bar (wall shelves behind the bartender) is gone (2026-07-23): we play
            // from behind the counter looking out, so bottles-on-a-back-wall faced the wrong way
            // and, with the bottles living in the menu now, the shelves stood empty. The wall-tier
            // ambience upgrade simply has no cabinet to tint until the P8 scenery pass.

            // Layer 4 — Counter. Real bar art when installed (positioned so its top surface
            // meets the counter rest line); else the flat procedural amber band.
            if (counterSprite != null)
            {
                float h = counterSprite.rect.height;
                // The bar-top surface front (the rest line) sits CounterSurfaceInset px below
                // the sprite's top, so align that line to CounterRestY; the deep surface then
                // recedes up behind the bottles.
                float cy = CounterRestY + CounterSurfaceInset - h;
                var c = NewRect("Counter", root);
                c.anchorMin = new Vector2(0, 0); c.anchorMax = new Vector2(1, 0);
                c.offsetMin = new Vector2(-Overscan, cy - Overscan); c.offsetMax = new Vector2(Overscan, cy + h);
                _counterImage = c.gameObject.AddComponent<Image>();
                _counterImage.sprite = counterSprite; _counterImage.raycastTarget = false;
            }
            else
            {
                var face = NewRect("CounterFace", root);
                Stretch(face, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, CounterFrontY));
                face.gameObject.AddComponent<Image>().color = UITheme.Amber[0];      // dark wood
                var surface = NewRect("CounterSurface", root);
                Stretch(surface, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, CounterFrontY), new Vector2(0, CounterRestY));
                surface.gameObject.AddComponent<Image>().color = UITheme.Amber[1];   // lit wood surface
                var lip = NewRect("CounterLip", root);                               // chrome front edge
                Stretch(lip, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, CounterFrontY - 2), new Vector2(0, CounterFrontY));
                lip.gameObject.AddComponent<Image>().color = UITheme.Cream[2];
                var keyLine = NewRect("CounterKey", root);                           // amber key highlight (rest line)
                Stretch(keyLine, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, CounterRestY - 2), new Vector2(0, CounterRestY));
                keyLine.gameObject.AddComponent<Image>().color = UITheme.Amber[3];
            }

            // Cash register on the bar top, bottom-left, with the wallet on a plaque above it
            // (18 §2 — the player reads their money diegetically from the till).
            if (registerSprite != null)
            {
                var reg = NewRect("Register", root);
                reg.anchorMin = reg.anchorMax = new Vector2(0, 0);
                reg.pivot = new Vector2(0.5f, 0);
                // Fixed footprint (v2.5 hi-bit): a 2x-density sprite renders finer pixels
                // into the same 57px slot instead of doubling on screen.
                const float regW = 57f;
                reg.sizeDelta = new Vector2(regW, regW * registerSprite.rect.height / registerSprite.rect.width);
                reg.anchoredPosition = new Vector2(RegisterX, CounterRestY);
                var regImg = reg.gameObject.AddComponent<Image>();
                regImg.sprite = registerSprite; regImg.preserveAspect = true;
                // The till is clickable: it opens the ledger of days gone by (GDD 24 §7).
                regImg.raycastTarget = true;
                var regBtn = reg.gameObject.AddComponent<Button>();
                regBtn.targetGraphic = regImg;
                regBtn.transition = Selectable.Transition.None;
                regBtn.onClick.AddListener(() => _onRegisterClicked?.Invoke());

                float plaqueY = CounterRestY + reg.sizeDelta.y - 18f;  // on the till's display
                var plaque = NewRect("MoneyPlaque", root);
                plaque.anchorMin = plaque.anchorMax = new Vector2(0, 0);   // absolute, on the till
                plaque.pivot = new Vector2(0.5f, 0);
                plaque.sizeDelta = new Vector2(46, 14);
                plaque.anchoredPosition = new Vector2(RegisterX, plaqueY);
                var pImg = plaque.gameObject.AddComponent<Image>();
                pImg.color = UITheme.Night[0]; pImg.raycastTarget = false;
                _moneyText = NewText("Money", plaque, _display, 10, TextAnchor.MiddleCenter, UITheme.Money);
                Stretch((RectTransform)_moneyText.transform, Vector2.zero, Vector2.one, new Vector2(2, 0), new Vector2(-2, 0));
                _moneyText.text = "$0";
            }

            // Layer 5 — Bottle rail: bottles anchor to the bottom-left and position by slot.
            _railRoot = NewRect("BottleRail", root);
            Stretch(_railRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Layer 6 — Customer: the VIP/patron pixel sprite, hands resting on the bar top,
            // bottom-right. Created LAST so it draws in front of the counter and the rail —
            // he's seated at our wooden bar, not floating above it (18 §6).
            if (customerSprite != null)
            {
                var cust = NewRect("Customer", root);
                cust.anchorMin = cust.anchorMax = new Vector2(0, 0);   // absolute from bottom-left
                cust.pivot = new Vector2(0.5f, 0);                      // centred on CustomerX
                const float custW = 144f;   // fixed footprint; hi-bit art just gets finer
                cust.sizeDelta = new Vector2(custW, custW * customerSprite.rect.height / customerSprite.rect.width);
                cust.anchoredPosition = new Vector2(CustomerX, CustomerBaseY);
                var img = cust.gameObject.AddComponent<Image>();
                img.sprite = customerSprite; img.preserveAspect = true;

                // You do not get to read someone by staring at them: the stats live on an ID
                // the customer hands over when asked, so the sprite itself is the ask.
                img.raycastTarget = true;
                var idButton = cust.gameObject.AddComponent<Button>();
                idButton.targetGraphic = img;
                idButton.transition = Selectable.Transition.None;
                idButton.onClick.AddListener(ToggleId);
                _customerRect = cust;
            }

            // Layer 7 — the "see ID" nudge, the pour glass, the mood gauge, and the licence
            // on its own canvas above everything.
            BuildMusician(root);
            BuildIdPrompt(root);
            BuildGlassHud(root);
            BuildMoodGauge(root);
            BuildIdCard();

            // The old shelf-click dressing (top-left goblet, on-counter rail) is retired —
            // building goes through the menu now. Hide it up front so it never flashes.
            HideBuildDressing();

            if (!Motion.Reduced) StartCoroutine(Ambient());
        }

        /// <summary>
        /// Ambient life (18 §4), deliberately sparse: the neon wall flickers off for a frame
        /// every few seconds and the patron breathes on a 2-frame whole-pixel idle. Nothing
        /// else moves, so the score moment still owns the screen. Purely cosmetic — this
        /// jitter never touches RunRng, so run determinism is unaffected.
        /// </summary>
        private System.Collections.IEnumerator Ambient()
        {
            float nextFlicker = Random.Range(3f, 7f);
            float bobTimer = 0f;
            bool bobUp = false;
            float customerY = _customerRect != null ? _customerRect.anchoredPosition.y : 0f;

            while (true)
            {
                float dt = Time.unscaledDeltaTime;

                // Neon flicker: a brief dip in the wall's brightness, then back.
                nextFlicker -= dt;
                if (nextFlicker <= 0f && _backgroundImage != null)
                {
                    _backgroundImage.color = new Color(0.72f, 0.72f, 0.78f);
                    yield return new WaitForSecondsRealtime(0.05f);
                    if (_backgroundImage != null) _backgroundImage.color = Color.white;
                    nextFlicker = Random.Range(3f, 7f);
                }

                // Patron idle: 1px whole-pixel bob so he reads as alive, never drifting.
                bobTimer += dt;
                if (bobTimer >= 1.1f && _customerRect != null)
                {
                    bobUp = !bobUp;
                    _customerRect.anchoredPosition = new Vector2(
                        _customerRect.anchoredPosition.x, customerY + (bobUp ? 1f : 0f));
                    bobTimer = 0f;
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
            // The wall sign "LAST CALL" (Magenta ramp) + a couple of small cyan accents high
            // on the back wall. Placeholder for the club_mid neon layer (18 §5).
            NeonSign(layer, UITheme.Magenta[3], new Vector2(200, 22), new Vector2(320, 300), "LAST CALL");
            NeonSign(layer, UITheme.Cyan[3], new Vector2(56, 10), new Vector2(120, 322), null);
            NeonSign(layer, UITheme.Magenta[3], new Vector2(48, 10), new Vector2(548, 316), null);
        }

        private void NeonSign(RectTransform layer, Color c, Vector2 size, Vector2 center, string label)
        {
            // Glow halo (dim, larger) + bright core — v2 glow = hand-placed halo, no shader.
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

        private void AddCustomer(RectTransform root)
        {
            // 96×128 patron placeholder — a quiet dark silhouette behind the counter
            // (18 §2 x=200-296). The real pixel customer sprite (18 §5) replaces this; kept
            // deliberately dim so it reads as "someone stands here" without stealing focus.
            var cust = NewRect("Customer", root);
            Place(cust, new Vector2(0, 0), new Vector2(96, 128), new Vector2(200, CounterFrontY - 12));
            var rim = NewRect("CustomerRim", cust);            // faint magenta club rim behind
            Stretch(rim, Vector2.zero, Vector2.one, new Vector2(-1, -1), new Vector2(1, 1));
            rim.SetAsFirstSibling();
            var rimImg = rim.gameObject.AddComponent<Image>();
            rimImg.color = new Color(UITheme.Magenta[2].r, UITheme.Magenta[2].g, UITheme.Magenta[2].b, 0.16f);
            rimImg.raycastTarget = false;
            var body = cust.gameObject.AddComponent<Image>();
            body.color = UITheme.Night[2]; body.raycastTarget = false; // dim shoulders
            var head = NewRect("Head", cust);                  // head cap
            Place(head, new Vector2(0.5f, 1), new Vector2(40, 40), new Vector2(0, -8));
            head.gameObject.AddComponent<Image>().color = UITheme.Night[3];
        }

        // ── bottle rail binding + slide reconcile ───────────────────────────────

        /// <summary>
        /// Reconciles the 8 bottle slots to the new rail. Staying bottles tween to their
        /// slot; new bottles enter from the right with a 60 ms stagger; removed bottles
        /// leave per <paramref name="exitStyle"/>. A selection-only change (same rail)
        /// refreshes tints and the 4px select-rise — no wave.
        /// </summary>
        /// <summary>
        /// Draws the shelf (GDD 21 §2). Replaces the card rail: the bottles no longer come and
        /// go, so the composition only changes when the run gains one. Selection is gone with
        /// the cards — clicking a bottle pours from it.
        /// </summary>
        private UnityAction<IngredientCard> _onPourStart;
        private UnityAction<IngredientCard> _onPourEnd;

        public void SetShelf(Shelf shelf, IEnumerable<IngredientType> debuffedTypes,
            UnityAction<IngredientCard> onPourStart, UnityAction<IngredientCard> onPourEnd,
            Exit exitStyle)
        {
            _onPourStart = onPourStart;
            _onPourEnd = onPourEnd;

            // Liquids stand on the back-bar wall shelves; garnishes rack on the counter (GDD 22).
            var rail = new List<IngredientCard>();
            var rack = new List<IngredientCard>();
            foreach (var bottle in shelf.Bottles)
            {
                if (bottle.Ingredient.Type == IngredientType.Garnish)
                {
                    if (rack.Count < GarnishSlots) rack.Add(bottle.Ingredient);
                }
                else if (rail.Count < Slots)
                {
                    rail.Add(bottle.Ingredient);
                }
            }
            var selected = new List<IngredientCard>();

            var debuffed = debuffedTypes != null
                ? new HashSet<IngredientType>(debuffedTypes)
                : new HashSet<IngredientType>();
            string signature = Signature(rail) + "|" + Signature(rack);
            bool composition = signature != _railSignature;
            _railSignature = signature;

            if (!composition)
            {
                foreach (var kv in _bottles) StyleBottle(kv.Value, selected, debuffed, animateRise: true);
                return;
            }

            var present = new HashSet<int>();
            for (int i = 0; i < rail.Count && i < Slots; i++) present.Add(rail[i].InstanceId);
            foreach (var garnish in rack) present.Add(garnish.InstanceId);

            // Exit: bottles no longer on the rail.
            var leaving = new List<int>();
            foreach (var id in _bottles.Keys) if (!present.Contains(id)) leaving.Add(id);
            int exitOrder = 0;
            foreach (var id in leaving)
            {
                var view = _bottles[id];
                _bottles.Remove(id);
                ExitBottle(view, exitStyle, exitOrder++);
            }

            // Enter / move.
            int enterOrder = 0;
            for (int i = 0; i < rail.Count && i < Slots; i++)
            {
                var card = rail[i];
                bool sel = selected != null && selected.Contains(card);
                float restY = SlotY(i);
                Vector2 target = new Vector2(SlotX(i), restY + (sel ? SelectRise : 0f));
                if (_bottles.TryGetValue(card.InstanceId, out var view))
                {
                    view.RestBaseY = restY;
                    if (view.Move != null) StopCoroutine(view.Move);
                    view.Move = StartCoroutine(Tweening.MoveAnchored(view.Root, target, DrawDur, Tweening.OutQuad));
                    StyleBottle(view, selected, debuffed, animateRise: false);
                }
                else
                {
                    view = CreateBottle(card);
                    view.RestBaseY = restY;
                    _bottles[card.InstanceId] = view;
                    StyleBottle(view, selected, debuffed, animateRise: false);
                    view.Root.anchoredPosition = new Vector2(OffscreenRight, target.y);
                    float delay = enterOrder++ * EnterStagger;
                    view.Move = StartCoroutine(EnterAfter(view.Root, target, delay));
                }
            }

            // The garnish rack, under the counter. No slide choreography — jars sit still.
            for (int i = 0; i < rack.Count; i++)
            {
                var card = rack[i];
                var target = new Vector2(GarnishFirstX + i * GarnishPitch, GarnishBaseY);
                if (!_bottles.TryGetValue(card.InstanceId, out var view))
                {
                    view = CreateBottle(card, GarnishDisplayW);
                    _bottles[card.InstanceId] = view;
                }
                view.RestBaseY = GarnishBaseY;
                view.Root.localRotation = Quaternion.identity;
                view.Root.anchoredPosition = target;
                StyleBottle(view, selected, debuffed, animateRise: false);
            }
        }

        private System.Collections.IEnumerator EnterAfter(RectTransform rt, Vector2 target, float delay)
        {
            if (!Motion.Reduced && delay > 0f)
            {
                float t = 0f;
                while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
            }
            // Slide in from the right, overshoot 2px past the slot, then settle (18 §3 Draw).
            Vector2 overshoot = new Vector2(target.x - Overshoot, target.y);
            yield return Tweening.MoveAnchored(rt, overshoot, DrawDur, Tweening.OutQuad);
            yield return Tweening.MoveAnchored(rt, target, SettleDur, Tweening.OutCubic);
        }

        private void ExitBottle(BottleView view, Exit style, int order)
        {
            if (view.Move != null) StopCoroutine(view.Move);
            if (view.Rise != null) StopCoroutine(view.Rise);
            void Destroy() { if (view.Root != null) UnityEngine.Object.Destroy(view.Root.gameObject); }

            if (style == Exit.Mix)
            {
                // Slide forward+up to the mixing area, then the empty slides LEFT off-screen
                // (180 ms InQuad, 18 §3 Mix).
                Vector2 up = new Vector2(320f, CounterRestY + 70f); // center mixing area
                StartCoroutine(Tweening.MoveAnchored(view.Root, up, MixPopDur, Tweening.OutBack, () =>
                    StartCoroutine(Tweening.MoveAnchored(view.Root,
                        new Vector2(OffscreenLeft, up.y), MixExitDur, Tweening.InQuad, Destroy))));
            }
            else
            {
                // Restock / Refresh: slide left off-screen; Refresh staggers as one wave.
                float delay = style == Exit.Refresh ? order * EnterStagger : 0f;
                StartCoroutine(ExitLeftAfter(view.Root, delay, Destroy));
            }
        }

        private System.Collections.IEnumerator ExitLeftAfter(RectTransform rt, float delay, System.Action done)
        {
            if (!Motion.Reduced && delay > 0f)
            {
                float t = 0f;
                while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
            }
            yield return Tweening.MoveAnchored(rt, new Vector2(OffscreenLeft, CounterRestY), MixExitDur, Tweening.InQuad, done);
        }

        private BottleView CreateBottle(IngredientCard card, float displayW = 0)
        {
            var root = NewRect($"Bottle_{card.InstanceId}", _railRoot);
            root.anchorMin = root.anchorMax = new Vector2(0, 0);
            root.pivot = new Vector2(0.5f, 0);

            bool hasSprite = _bottleById.TryGetValue(card.Id, out var sprite)
                             || _bottleSprites.TryGetValue(card.Type, out sprite);
            // The slot box is fixed; art authored at 2x texel density (v2.5 hi-bit) simply
            // renders finer pixels into the same slot instead of growing.
            float w = displayW > 0 ? displayW : BottleDisplayW;
            Vector2 size = hasSprite
                ? new Vector2(w, w * sprite.rect.height / sprite.rect.width)
                : new Vector2(BottleW, BottleH);
            root.sizeDelta = size;
            var view = new BottleView { Card = card, Root = root };

            if (hasSprite)
            {
                // Installed sprite carries its own baked neon rim, so no rim rectangle here
                // (a rect would show through the sprite's transparent margins as a box).
                var img = NewRect("Sprite", root);
                Place(img, new Vector2(0.5f, 0), size, Vector2.zero);
                view.SpriteImg = img.gameObject.AddComponent<Image>();
                view.SpriteImg.sprite = sprite;
                view.SpriteImg.preserveAspect = true;
                view.SpriteImg.raycastTarget = false;
            }
            else
            {
                // Neon rim (back-light glow) hugs the placeholder silhouette; the body rect
                // covers all but a thin edge of it.
                var rim = NewRect("Rim", root);
                Place(rim, new Vector2(0.5f, 0), size + new Vector2(4, 2), new Vector2(0, -1));
                view.Rim = rim.gameObject.AddComponent<Image>();
                view.Rim.raycastTarget = false;

                // Flat-silhouette placeholder: 1px dark outline behind a ramp-coloured body.
                var outline = NewRect("Outline", root);
                Place(outline, new Vector2(0.5f, 0), new Vector2(BottleW + 2, BottleH), Vector2.zero);
                view.Outline = outline.gameObject.AddComponent<Image>();
                view.Outline.raycastTarget = false;

                var body = NewRect("Body", root);
                Place(body, new Vector2(0.5f, 0), new Vector2(BottleW, BottleH - 8), Vector2.zero);
                view.Body = body.gameObject.AddComponent<Image>();
                view.Body.raycastTarget = false;

                var neck = NewRect("Neck", root);
                Place(neck, new Vector2(0.5f, 0), new Vector2(10, 12), new Vector2(0, BottleH - 12));
                view.Neck = neck.gameObject.AddComponent<Image>();
                view.Neck.raycastTarget = false;
            }

            // Hold-to-pour (GDD 21 §3): press starts the pour, holding keeps it flowing,
            // release stops it. A Button's click event fires on release only, which is
            // exactly wrong for pouring, so this is an EventTrigger pair instead.
            var button = root.gameObject.AddComponent<Image>();  // full-slot press target
            button.color = new Color(0, 0, 0, 0);
            var trigger = root.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var captured = card;
            var down = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            down.callback.AddListener(_ => _onPourStart?.Invoke(captured));
            trigger.triggers.Add(down);
            var up = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            up.callback.AddListener(_ => _onPourEnd?.Invoke(captured));
            trigger.triggers.Add(up);
            // Dragging off the bottle mid-pour must also stop it, or the pour runs forever.
            var exit = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => _onPourEnd?.Invoke(captured));
            trigger.triggers.Add(exit);

            // The old Flavor-number chip is gone: the number still feeds weighted scoring,
            // but what the player needs at a glance is *which bottle this is*. The brand name
            // sits under the bottle like a shelf tag; Flavor moves to the bottle-info popup
            // (GDD 22 §2, future).
            var tag = NewRect("NameTag", root);
            // Shelf tags are two lines — brand above, style below — because the style word
            // is the load-bearing one ("what IS this bottle"), and the whole tag wears the
            // style's signature colour (GDD 22 §1). Rack jars keep a one-line tag.
            bool rack = card.Type == IngredientType.Garnish;
            Place(tag, new Vector2(0.5f, 0), new Vector2(rack ? 52 : 60, rack ? 10 : 19),
                new Vector2(0, rack ? -12f : -21f));
            var tagImg = tag.gameObject.AddComponent<Image>();
            tagImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.72f);
            tagImg.raycastTarget = false;
            view.Name = NewText("Name", tag, _body, 7, TextAnchor.MiddleCenter,
                UITheme.StyleColor(card.Info?.Style, card.Type));
            Stretch((RectTransform)view.Name.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.Name.text = TagText(card, rack);

            return view;
        }

        private void StyleBottle(BottleView view, ICollection<IngredientCard> selected,
            HashSet<IngredientType> debuffed, bool animateRise)
        {
            bool isSelected = selected != null && selected.Contains(view.Card);
            bool isDebuffed = debuffed != null && debuffed.Contains(view.Card.Type);

            if (view.SpriteImg != null)
            {
                // Installed pixel sprite: keep its baked colours. Debuff dims it; selection
                // washes it cyan (+ the 4px rise) since it has no procedural rim.
                view.SpriteImg.color = isDebuffed
                    ? Color.Lerp(Color.white, UITheme.Night[1], 0.5f)
                    : (isSelected ? Color.Lerp(Color.white, UITheme.Cyan[4], 0.35f) : Color.white);
            }
            else
            {
                Color fill = UITheme.TypeFill(view.Card.Type);
                Color outline = UITheme.TypeOutline(view.Card.Type);
                if (isDebuffed)
                {
                    fill = Color.Lerp(fill, UITheme.Night[0], 0.55f);
                    outline = Color.Lerp(outline, UITheme.Night[0], 0.4f);
                }
                if (isSelected) fill = Color.Lerp(fill, UITheme.Cream[4], 0.22f);
                view.Body.color = fill;
                view.Neck.color = fill;
                view.Outline.color = outline;
            }

            // Selected → cyan back-light + a 4px rise; else the magenta club rim
            // (placeholder bottles only; sprite bottles carry a baked rim).
            if (view.Rim != null)
                view.Rim.color = isSelected
                    ? new Color(UITheme.Selection.r, UITheme.Selection.g, UITheme.Selection.b, 0.9f)
                    : new Color(UITheme.Magenta[3].r, UITheme.Magenta[3].g, UITheme.Magenta[3].b, 0.55f);

            view.Name.supportRichText = true;
            string tagText = TagText(view.Card, view.Card.Type == IngredientType.Garnish);
            view.Name.text = isDebuffed ? $"<color=#E84DA6>{tagText}</color>" : tagText;

            if (view.Selected != isSelected)
            {
                view.Selected = isSelected;
                if (animateRise)
                {
                    if (view.Rise != null) StopCoroutine(view.Rise);
                    var to = new Vector2(view.Root.anchoredPosition.x, view.RestBaseY + (isSelected ? SelectRise : 0f));
                    view.Rise = StartCoroutine(Tweening.MoveAnchored(view.Root, to, 0.10f, Tweening.OutCubic));
                }
            }
        }

        // ── customer ID (GDD 19 §3) ──────────────────────────────────────────────

        /// <summary>
        /// One cell of the 3×2 stat grid on the ID: a tag, a reading of whatever clarity the
        /// bartender has earned, and a 0–100 bar under it.
        /// </summary>
        private sealed class StatCell
        {
            public Text Tag;
            public Text Value;
            public Image Track;    // the 0–100 rail
            public Image Band;     // the lit portion: a point for Exact, a span for Range
            public Image Ghost;    // where the current selection would put them
        }

        private Canvas _idCanvas;
        private RectTransform _idRoot;      // scrim + card; the whole popup
        private RectTransform _idCard;
        private Text _idName;
        private Text _idIntent;
        private Text _idRelationship;
        private Text _idArchetype;
        private Text _idAgeFrom;
        private Image _idMoodFill;
        private Image _idPortrait;
        private RectTransform _idPrompt;    // "click for ID" nudge over the customer
        private readonly Dictionary<Emotion, StatCell> _statCells = new Dictionary<Emotion, StatCell>();
        private bool _idOpen;

        /// <summary>Per-archetype ID photos (18 §5). Falls back to a flat silhouette.</summary>
        [System.Serializable]
        public struct PortraitSprite { public string archetypeId; public Sprite sprite; }
        [SerializeField] private PortraitSprite[] portraits;
        private readonly Dictionary<string, Sprite> _portraits = new Dictionary<string, Sprite>();

        // The ID is a full state-licence prop (GDD 22 §3): light card stock, a coloured
        // header band, the photo left, licence fields right, and the six readings as a 3×2
        // grid of record fields below. Deliberately big — it is a modal you hold up.
        private const float CardW = 288f;
        private const float CardH = 186f;
        private const float HeaderH = 18f;
        private const float PhotoW = 56f;
        private const float PhotoH = 70f;
        private const float CellW = 88f;
        private const float CellH = 26f;
        private const float CellGapX = 5f;
        private const float CellGapY = 5f;
        private const float TrackW = 82f;

        private void BuildIdCard()
        {
            // Its own canvas above the HUD overlay: the ID must never be half-covered by a
            // button, and it has to stay pixel-crisp, so it keeps the 640×360 reference.
            var canvasGo = new GameObject("IdCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _idCanvas = canvasGo.GetComponent<Canvas>();
            _idCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _idCanvas.sortingOrder = 20;                  // above the HUD (0) and its modals
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            _idRoot = NewRect("CustomerId", (RectTransform)canvasGo.transform);
            Stretch(_idRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Dimmed room behind the licence; clicking anywhere off the card puts it away.
            var scrim = NewRect("IdScrim", _idRoot);
            Stretch(scrim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrimImg = scrim.gameObject.AddComponent<Image>();
            scrimImg.color = UITheme.Scrim;
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.targetGraphic = scrimImg;
            scrimButton.transition = Selectable.Transition.None;
            scrimButton.onClick.AddListener(CloseId);

            _idCard = NewRect("IdCard", _idRoot);
            _idCard.anchorMin = _idCard.anchorMax = _idCard.pivot = new Vector2(0.5f, 0.5f);
            _idCard.sizeDelta = new Vector2(CardW, CardH);
            _idCard.anchoredPosition = new Vector2(0, 24);
            var frame = _idCard.gameObject.AddComponent<Image>();
            frame.color = UITheme.Night[0];               // dark laminated edge

            // Light card stock — the one place the night palette inverts, because a licence
            // is a bright object in a dark room.
            var fill = NewRect("Fill", _idCard);
            Stretch(fill, Vector2.zero, Vector2.one, Vector2.one, -Vector2.one);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = UITheme.Cream[4];

            // Header band, state-licence style.
            var header = NewRect("Header", fill);
            Stretch(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -HeaderH), Vector2.zero);
            var headerImg = header.gameObject.AddComponent<Image>();
            headerImg.color = UITheme.ClubBlue[2];
            headerImg.raycastTarget = false;
            var headerText = NewText("Title", header, _display, 7, TextAnchor.MiddleLeft, UITheme.Cream[4]);
            Stretch((RectTransform)headerText.transform, Vector2.zero, Vector2.one, new Vector2(6, 0), new Vector2(-6, 0));
            headerText.text = "CITY OF NEW ARDEN — PATRON ID";
            _idRelationship = NewText("Relationship", header, _body, 7, TextAnchor.MiddleRight, UITheme.Cream[4]);
            Stretch((RectTransform)_idRelationship.transform, Vector2.zero, Vector2.one, new Vector2(6, 0), new Vector2(-6, 0));

            // Photo, left, with the classic double border.
            var photoFrame = NewRect("PhotoFrame", fill);
            Place(photoFrame, new Vector2(0, 1), new Vector2(PhotoW + 4, PhotoH + 4), new Vector2(7, -HeaderH - 6));
            var photoFrameImg = photoFrame.gameObject.AddComponent<Image>();
            photoFrameImg.color = UITheme.Night[1];
            photoFrameImg.raycastTarget = false;
            var photo = NewRect("Photo", photoFrame);
            Stretch(photo, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            _idPortrait = photo.gameObject.AddComponent<Image>();
            _idPortrait.preserveAspect = true;
            _idPortrait.raycastTarget = false;

            // Licence fields, right of the photo: NAME / AGE / FROM / DEMEANOR.
            float fieldsX = 7 + PhotoW + 4 + 8;
            _idName = NewText("Name", fill, _display, 10, TextAnchor.UpperLeft, UITheme.Night[1]);
            Place((RectTransform)_idName.transform, new Vector2(0, 1), new Vector2(CardW - fieldsX - 8, 12),
                new Vector2(fieldsX, -HeaderH - 8));
            _idAgeFrom = NewText("AgeFrom", fill, _body, 8, TextAnchor.UpperLeft, UITheme.Night[2]);
            Place((RectTransform)_idAgeFrom.transform, new Vector2(0, 1), new Vector2(CardW - fieldsX - 8, 22),
                new Vector2(fieldsX, -HeaderH - 26));
            _idAgeFrom.supportRichText = true;
            _idArchetype = NewText("Archetype", fill, _body, 8, TextAnchor.UpperLeft, UITheme.Night[2]);
            Place((RectTransform)_idArchetype.transform, new Vector2(0, 1), new Vector2(CardW - fieldsX - 8, 10),
                new Vector2(fieldsX, -HeaderH - 50));
            _idArchetype.supportRichText = true;

            // The six readings as a 3×2 grid of record fields, full width below the photo row.
            float gridTop = -(HeaderH + PhotoH + 14);
            for (int i = 0; i < Emotions.Count; i++)
            {
                int col = i % 3, rowIndex = i / 3;
                var pos = new Vector2(
                    7 + col * (CellW + CellGapX),
                    gridTop - rowIndex * (CellH + CellGapY));
                _statCells[Emotions.All[i]] = BuildStatCell(fill, Emotions.All[i], pos);
            }

            // The one thing never hidden, printed like a licence endorsement.
            var intentBand = NewRect("IntentBand", fill);
            Stretch(intentBand, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 14));
            var intentImg = intentBand.gameObject.AddComponent<Image>();
            intentImg.color = UITheme.Amber[3];
            intentImg.raycastTarget = false;
            _idIntent = NewText("Intent", intentBand, _display, 7, TextAnchor.MiddleLeft, UITheme.Night[1]);
            _idIntent.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch((RectTransform)_idIntent.transform, Vector2.zero, Vector2.one, new Vector2(6, 0), new Vector2(-110, 0));

            // Visit satisfaction on the licence too, mirroring the stage gauge.
            var moodLabel = NewText("MoodLabel", intentBand, _body, 7, TextAnchor.MiddleRight, UITheme.Night[1]);
            Place((RectTransform)moodLabel.transform, new Vector2(1, 0.5f), new Vector2(30, 10), new Vector2(-54, 0));
            moodLabel.text = "VISIT";
            var moodBg = NewRect("MoodBg", intentBand);
            moodBg.anchorMin = moodBg.anchorMax = new Vector2(1, 0.5f);
            moodBg.pivot = new Vector2(1, 0.5f);
            moodBg.sizeDelta = new Vector2(44, 6);
            moodBg.anchoredPosition = new Vector2(-6, 0);
            var moodBgImg = moodBg.gameObject.AddComponent<Image>();
            moodBgImg.color = UITheme.Night[1];
            moodBgImg.raycastTarget = false;
            var moodFill = NewRect("MoodFill", moodBg);
            moodFill.anchorMin = new Vector2(0, 0); moodFill.anchorMax = new Vector2(0, 1);
            moodFill.pivot = new Vector2(0, 0.5f);
            moodFill.sizeDelta = new Vector2(0, -2);
            moodFill.anchoredPosition = new Vector2(1, 0);
            _idMoodFill = moodFill.gameObject.AddComponent<Image>();
            _idMoodFill.raycastTarget = false;

            _idRoot.gameObject.SetActive(false);
        }

        private StatCell BuildStatCell(RectTransform parent, Emotion emotion, Vector2 pos)
        {
            var cell = new StatCell();
            var ramp = UITheme.EmotionRamp[emotion];

            var box = NewRect($"Cell{emotion}", parent);
            Place(box, new Vector2(0, 1), new Vector2(CellW, CellH), pos);
            // Printed record fields on light card stock: a pale slab, a ramp-coloured tag,
            // dark ink for the value.
            var boxBg = box.gameObject.AddComponent<Image>();
            boxBg.color = UITheme.Cream[3];
            boxBg.raycastTarget = false;

            cell.Tag = NewText("Tag", box, _body, 7, TextAnchor.UpperLeft, ramp[2]);
            Place((RectTransform)cell.Tag.transform, new Vector2(0, 1), new Vector2(30, 9), new Vector2(3, -3));

            cell.Value = NewText("Val", box, _body, 8, TextAnchor.UpperRight, UITheme.Night[1]);
            Place((RectTransform)cell.Value.transform, new Vector2(1, 1), new Vector2(52, 9), new Vector2(-3, -3));

            var track = NewRect("Track", box);
            Place(track, new Vector2(0, 1), new Vector2(TrackW, 5), new Vector2(3, -16));
            cell.Track = track.gameObject.AddComponent<Image>();
            cell.Track.color = UITheme.Cream[2];
            cell.Track.raycastTarget = false;

            // Drawn under the band, so a ghost that disappears behind it reads as
            // "this lands inside what you already know".
            var ghost = NewRect("Ghost", track);
            ghost.anchorMin = ghost.anchorMax = new Vector2(0, 0.5f);
            ghost.pivot = new Vector2(0.5f, 0.5f);
            ghost.sizeDelta = new Vector2(2, 7);
            cell.Ghost = ghost.gameObject.AddComponent<Image>();
            cell.Ghost.color = UITheme.Night[1];
            cell.Ghost.raycastTarget = false;
            ghost.gameObject.SetActive(false);

            var band = NewRect("Band", track);
            band.anchorMin = band.anchorMax = new Vector2(0, 0.5f);
            band.pivot = new Vector2(0, 0.5f);
            band.sizeDelta = new Vector2(2, 5);
            cell.Band = band.gameObject.AddComponent<Image>();
            cell.Band.color = ramp[2];
            cell.Band.raycastTarget = false;

            return cell;
        }

        // ── the pour glass (GDD 21 §3.1 / GDD 22) ────────────────────────────────

        [SerializeField] private Sprite glassSprite;      // hi-bit cocktail glass, bowl emptied by the pipeline
        [SerializeField] private Sprite glassMaskSprite;  // baked bowl-interior stencil the fill clips to

        private RectTransform _glassRoot;
        private RectTransform _glassFillArea;
        private Text _glassPercent;
        private Text _glassRatios;

        // Big, top-left, deliberately dominant: the glass is the primary feedback channel.
        private const float GlassX = 10f;
        private const float GlassY = 214f;
        private const float GlassW = 96f;    // 0.75 × the 128×176 sprite: exact aspect, no letterbox
        private const float GlassH = 132f;
        // The bowl interior inside the glass sprite, relative to its rect — measured off
        // the installed art by the fetch pipeline, so the fill area and its baked stencil
        // mask land exactly on the painted bowl instead of floating as a loose square.
        private const float GlassInnerL = 0.156f, GlassInnerR = 0.844f;
        private const float GlassInnerB = 0.483f, GlassInnerT = 0.852f;

        private void BuildGlassHud(RectTransform root)
        {
            _glassRoot = NewRect("GlassHud", root);
            _glassRoot.anchorMin = _glassRoot.anchorMax = new Vector2(0, 0);
            _glassRoot.pivot = new Vector2(0, 0);
            _glassRoot.sizeDelta = new Vector2(GlassW, GlassH);
            _glassRoot.anchoredPosition = new Vector2(GlassX, GlassY);

            if (glassSprite != null)
            {
                var glass = NewRect("Glass", _glassRoot);
                Stretch(glass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _glassImage = glass.gameObject.AddComponent<Image>();
                _glassImage.sprite = glassSprite; _glassImage.preserveAspect = true; _glassImage.raycastTarget = false;
            }
            else
            {
                // Placeholder tumbler: two walls and a base, straight from the palette.
                var wallL = NewRect("WallL", _glassRoot);
                Stretch(wallL, new Vector2(0.10f, 0.06f), new Vector2(0.16f, 0.92f), Vector2.zero, Vector2.zero);
                wallL.gameObject.AddComponent<Image>().color = UITheme.Cream[2];
                var wallR = NewRect("WallR", _glassRoot);
                Stretch(wallR, new Vector2(0.84f, 0.06f), new Vector2(0.90f, 0.92f), Vector2.zero, Vector2.zero);
                wallR.gameObject.AddComponent<Image>().color = UITheme.Cream[2];
                var baseR = NewRect("Base", _glassRoot);
                Stretch(baseR, new Vector2(0.10f, 0.02f), new Vector2(0.90f, 0.12f), Vector2.zero, Vector2.zero);
                baseR.gameObject.AddComponent<Image>().color = UITheme.Cream[3];
            }

            // Liquid layers draw OVER the glass sprite (the generated glass has an opaque
            // interior, so anything beneath it would be invisible) and are clipped by a
            // bowl-shaped stencil mask, so the fill hugs the glass instead of sitting on it
            // as a bare rectangle.
            var area = NewRect("FillArea", _glassRoot);
            Stretch(area, new Vector2(GlassInnerL, GlassInnerB), new Vector2(GlassInnerR, GlassInnerT),
                Vector2.zero, Vector2.zero);
            var maskImg = area.gameObject.AddComponent<Image>();
            maskImg.sprite = glassMaskSprite != null ? glassMaskSprite : BuildBowlMask();
            maskImg.raycastTarget = false;
            var mask = area.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            _glassFillArea = area;

            // The % lives inside the glass (GDD 22 §1) — the number you steer by.
            _glassPercent = NewText("Percent", _glassRoot, _display, 12, TextAnchor.MiddleCenter, UITheme.Night[1]);
            Stretch((RectTransform)_glassPercent.transform, new Vector2(0, GlassInnerB), new Vector2(1, GlassInnerT),
                Vector2.zero, Vector2.zero);
            _glassPercent.text = "0%";

            // Live ratios under the glass (the back-bar shelves now own the space beside
            // it) — fill and ratio are different numbers, and the Phase-1 finding was that
            // hiding the ratios makes players systematically wrong.
            _glassRatios = NewText("Ratios", root, _body, 8, TextAnchor.UpperLeft, UITheme.TextSecondary);
            var ratioRt = (RectTransform)_glassRatios.transform;
            ratioRt.anchorMin = ratioRt.anchorMax = new Vector2(0, 0);
            ratioRt.pivot = new Vector2(0, 1);
            ratioRt.sizeDelta = new Vector2(128, 60);
            ratioRt.anchoredPosition = new Vector2(GlassX, GlassY - 4);
            _glassRatios.supportRichText = true;
        }

        private readonly List<Image> _glassLayers = new List<Image>();

        /// <summary>
        /// Redraws the glass: one colour band per pour, bottom-up in pour order, the fill %
        /// inside the glass and the live ratio list beside it.
        /// </summary>
        public void SetGlass(GlassContents glass, System.Func<string, IngredientCard> lookup)
        {
            if (_glassRoot == null || glass == null) return;

            foreach (var layer in _glassLayers)
                if (layer != null) Destroy(layer.gameObject);
            _glassLayers.Clear();

            float areaH = _glassFillArea.rect.height;
            float y = 0;
            foreach (var pour in glass.Pours)
            {
                var card = lookup?.Invoke(pour.IngredientId);
                float h = (float)(pour.Volume / glass.Capacity) * areaH;
                var layer = NewRect($"Layer_{pour.IngredientId}", _glassFillArea);
                layer.anchorMin = new Vector2(0, 0); layer.anchorMax = new Vector2(1, 0);
                layer.pivot = new Vector2(0.5f, 0);
                layer.offsetMin = new Vector2(0, 0); layer.offsetMax = new Vector2(0, 0);
                layer.sizeDelta = new Vector2(0, h);
                layer.anchoredPosition = new Vector2(0, y);
                var img = layer.gameObject.AddComponent<Image>();
                // The liquid pours in the style's signature colour — the same one the shelf
                // tag and ratio list wear — so "vodka is ice-blue" holds all the way down.
                var c = card != null ? UITheme.StyleColor(card.Info?.Style, card.Type) : UITheme.Cream[3];
                img.color = new Color(c.r, c.g, c.b, 0.88f);
                img.raycastTarget = false;
                layer.SetSiblingIndex(0); // beneath earlier-created glass sprite? area holds only layers
                _glassLayers.Add(img);
                y += h;
            }

            _glassPercent.text = $"{glass.FillFraction:P0}".Replace(" ", "");

            if (glass.IsEmpty)
            {
                _glassRatios.text = "";
                return;
            }
            var sb = new StringBuilder();
            foreach (var id in glass.Ingredients)
            {
                var card = lookup?.Invoke(id);
                var c = card != null ? UITheme.StyleColor(card.Info?.Style, card.Type) : UITheme.Cream[4];
                string hex = ColorUtility.ToHtmlStringRGB(c);
                sb.AppendLine($"<color=#{hex}>{(card?.Name ?? id).ToUpperInvariant()} {glass.RatioOf(id):P0}</color>");
            }
            _glassRatios.text = sb.ToString().Replace(" %", "%");
        }

        // ── the visit-satisfaction gauge (GDD 22 §3) ─────────────────────────────

        private RectTransform _moodRoot;
        private Image _moodFill;
        private const int MoodCap = 8;   // a very good visit; the bar tops out here

        private void BuildMoodGauge(RectTransform root)
        {
            _moodRoot = NewRect("MoodGauge", root);
            _moodRoot.anchorMin = _moodRoot.anchorMax = new Vector2(0, 0);
            _moodRoot.pivot = new Vector2(0.5f, 0);
            _moodRoot.sizeDelta = new Vector2(48, 6);
            _moodRoot.anchoredPosition = new Vector2(CustomerX, CustomerBaseY + 118);
            var bg = _moodRoot.gameObject.AddComponent<Image>();
            bg.color = UITheme.Night[0];
            bg.raycastTarget = false;

            var fill = NewRect("Fill", _moodRoot);
            fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0, 1);
            fill.pivot = new Vector2(0, 0.5f);
            fill.offsetMin = new Vector2(1, 1); fill.offsetMax = new Vector2(1, -1);
            fill.sizeDelta = new Vector2(0, -2);
            fill.anchoredPosition = new Vector2(1, 0);
            _moodFill = fill.gameObject.AddComponent<Image>();
            _moodFill.raycastTarget = false;
            _moodRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// The happiness gauge: fills and greens as the visit's earned satisfaction grows.
        /// Driven by *earned* satisfaction, never by the hidden stats — a gauge computed from
        /// the truth would leak exactly what the tier system hides.
        /// </summary>
        public void SetSatisfaction(int earned, bool visible)
        {
            if (_moodRoot == null) return;
            _moodRoot.gameObject.SetActive(visible);

            float t = Mathf.Clamp01(earned / (float)MoodCap);
            var colour = Color.Lerp(UITheme.ViceRed[3], UITheme.Lime[3], t);

            if (visible)
            {
                _moodFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(46f * t), -2);
                _moodFill.color = colour;
            }
            if (_idMoodFill != null)
            {
                _idMoodFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(42f * t), -2);
                _idMoodFill.color = colour;
            }
        }

        /// <summary>The little nudge over the customer telling you the ID can be asked for.</summary>
        private void BuildIdPrompt(RectTransform root)
        {
            _idPrompt = NewRect("IdPrompt", root);
            _idPrompt.anchorMin = _idPrompt.anchorMax = new Vector2(0, 0);
            _idPrompt.pivot = new Vector2(0.5f, 0);
            _idPrompt.sizeDelta = new Vector2(44, 11);
            _idPrompt.anchoredPosition = new Vector2(CustomerX, CustomerBaseY + 104);
            var bg = _idPrompt.gameObject.AddComponent<Image>();
            bg.color = UITheme.Night[0];
            bg.raycastTarget = false;
            var label = NewText("Label", _idPrompt, _body, 7, TextAnchor.MiddleCenter, UITheme.PrimaryAction);
            Stretch((RectTransform)label.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.text = "SEE ID";
            _idPrompt.gameObject.SetActive(false);
        }

        public void OpenId()
        {
            if (_idRoot == null) return;
            _idOpen = true;
            _idRoot.gameObject.SetActive(true);
        }

        public void CloseId()
        {
            if (_idRoot == null) return;
            _idOpen = false;
            _idRoot.gameObject.SetActive(false);
        }

        public void ToggleId()
        {
            if (_idOpen) CloseId(); else OpenId();
        }

        private Sprite PortraitFor(string archetypeId) =>
            !string.IsNullOrEmpty(archetypeId) && _portraits.TryGetValue(archetypeId, out var s) ? s : null;

        /// <summary>The ID photo for an archetype, for the tycoon floor's licence card (P6).</summary>
        public Sprite PortraitSpriteFor(string archetypeId) => PortraitFor(archetypeId);

        /// <summary>x offset inside the 0–100 rail for a stat value.</summary>
        private static float TrackAt(int value) => TrackW * Mathf.Clamp01(value / 100f);

        // ── helpers ──────────────────────────────────────────────────────────────

        // The back-bar grid: slot i fills the upper shelf left→right, then the lower.
        private static float SlotX(int i) => ShelfFirstX + (i % ShelfCols) * ShelfPitch;
        private static float SlotY(int i) => i < ShelfCols ? ShelfRow0Y : ShelfRow1Y;

        /// <summary>
        /// A white U-shape used as a stencil for the glass liquid: full width at the rim,
        /// rounded at the base, so the fill bands follow the bowl instead of poking square
        /// corners out of it.
        /// </summary>
        private static Sprite BuildBowlMask()
        {
            const int w = 48, h = 64;
            const float r = 16f;   // bottom-corner radius
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inside = true;
                    if (y < r)
                    {
                        float dx = x < r ? r - x : (x > w - 1 - r ? x - (w - 1 - r) : 0f);
                        float dy = r - y;
                        inside = dx * dx + dy * dy <= r * r;
                    }
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Two-line shelf tag: brand on top, the style word below it.</summary>
        private static string TagText(IngredientCard card, bool singleLine)
        {
            string name = card.Name.ToUpperInvariant();
            if (singleLine) return name;
            int cut = name.LastIndexOf(' ');
            return cut <= 0 ? name : name.Substring(0, cut) + "\n" + name.Substring(cut + 1);
        }

        private static string Signature(IReadOnlyList<IngredientCard> rail)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < rail.Count && i < Slots; i++) sb.Append(rail[i].InstanceId).Append(',');
            return sb.ToString();
        }

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
