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
    ///   Sky/City → Club Far (crowd) → Club Mid (neon "LAST CALL") → Customer →
    ///   Counter (amber-lit bottom 96px) → Bottle rail (8 slots ON the counter).
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
        private const int Slots = 8;                       // GDD: 8-card rail
        private static readonly Vector2 Reference = new Vector2(640, 360);
        private const float SlotPitch = 48f;               // rail sits between the register and the VIP
        private const float FirstSlotX = 100f;             // clears the bottom-left cash register
        private const float CustomerX = 556f;              // VIP centre, bottom-right on the bar
        private const float CustomerBaseY = 126f;          // hands rest on the bar-top surface
        private const float CustomerTilt = 0f;           // right end leans right (radial, arc)
        private const float RegisterX = 44f;               // cash register centre, bottom-left
        private const float RegisterTilt = 0f;            // left end leans left (radial, arc)
        private const float BottleBaseY = 128f;            // 18 §2: y=232 top-down → 360−232
        private const float CounterFrontY = 96f;           // 18 §2: surface line y=264 → 360−264 (bottom 96px)
        private const float BottleW = 40f;                 // placeholder fallback size
        private const float BottleH = 60f;
        private const float SelectRise = 4f;               // 18 §3: select rises 4px
        private const float ArcHeight = 0f;               // curved bar: centre slots rise this much
        private const float BottleTilt = 0f;               // max radial lean at the arc ends
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

        /// <summary>The per-type bottle sprite, so UI (e.g. the recipe book) can show the
        /// same art the rail uses instead of abstract colour dots.</summary>
        public Sprite BottleIcon(IngredientType type) =>
            _bottleSprites.TryGetValue(type, out var s) ? s : null;

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

            // Layer 4 — Counter. Real bar art when installed (positioned so its top surface
            // meets the bottle rest line); else the flat procedural amber band.
            if (counterSprite != null)
            {
                float h = counterSprite.rect.height;
                // The bar-top surface front (the rest line) sits CounterSurfaceInset px below
                // the sprite's top, so align that line to BottleBaseY; the deep surface then
                // recedes up behind the bottles.
                float cy = BottleBaseY + CounterSurfaceInset - h;
                var c = NewRect("Counter", root);
                c.anchorMin = new Vector2(0, 0); c.anchorMax = new Vector2(1, 0);
                c.offsetMin = new Vector2(-Overscan, cy - Overscan); c.offsetMax = new Vector2(Overscan, cy + h);
                var cImg = c.gameObject.AddComponent<Image>();
                cImg.sprite = counterSprite; cImg.raycastTarget = false;
            }
            else
            {
                var face = NewRect("CounterFace", root);
                Stretch(face, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, CounterFrontY));
                face.gameObject.AddComponent<Image>().color = UITheme.Amber[0];      // dark wood
                var surface = NewRect("CounterSurface", root);
                Stretch(surface, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, CounterFrontY), new Vector2(0, BottleBaseY));
                surface.gameObject.AddComponent<Image>().color = UITheme.Amber[1];   // lit wood surface
                var lip = NewRect("CounterLip", root);                               // chrome front edge
                Stretch(lip, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, CounterFrontY - 2), new Vector2(0, CounterFrontY));
                lip.gameObject.AddComponent<Image>().color = UITheme.Cream[2];
                var keyLine = NewRect("CounterKey", root);                           // amber key highlight (rest line)
                Stretch(keyLine, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, BottleBaseY - 2), new Vector2(0, BottleBaseY));
                keyLine.gameObject.AddComponent<Image>().color = UITheme.Amber[3];
            }

            // Cash register on the bar top, bottom-left, with the wallet on a plaque above it
            // (18 §2 — the player reads their money diegetically from the till).
            if (registerSprite != null)
            {
                var reg = NewRect("Register", root);
                reg.anchorMin = reg.anchorMax = new Vector2(0, 0);
                reg.pivot = new Vector2(0.5f, 0);
                reg.sizeDelta = new Vector2(registerSprite.rect.width, registerSprite.rect.height);
                reg.anchoredPosition = new Vector2(RegisterX, BottleBaseY);
                reg.localRotation = Quaternion.Euler(0, 0, RegisterTilt);   // POV angle
                var regImg = reg.gameObject.AddComponent<Image>();
                regImg.sprite = registerSprite; regImg.preserveAspect = true; regImg.raycastTarget = false;

                float plaqueY = BottleBaseY + registerSprite.rect.height - 18f;  // on the till's display
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
                cust.sizeDelta = new Vector2(customerSprite.rect.width, customerSprite.rect.height);
                cust.anchoredPosition = new Vector2(CustomerX, CustomerBaseY);
                cust.localRotation = Quaternion.Euler(0, 0, CustomerTilt);   // POV angle
                var img = cust.gameObject.AddComponent<Image>();
                img.sprite = customerSprite; img.preserveAspect = true; img.raycastTarget = false;
                _customerRect = cust;
            }

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
        public void SetBottles(IReadOnlyList<IngredientCard> rail, ICollection<IngredientCard> selected,
            IEnumerable<IngredientType> debuffedTypes, UnityAction<IngredientCard> onClick, Exit exitStyle)
        {
            var debuffed = debuffedTypes != null
                ? new HashSet<IngredientType>(debuffedTypes)
                : new HashSet<IngredientType>();
            string signature = Signature(rail);
            bool composition = signature != _railSignature;
            _railSignature = signature;

            if (!composition)
            {
                foreach (var kv in _bottles) StyleBottle(kv.Value, selected, debuffed, animateRise: true);
                return;
            }

            var present = new HashSet<int>();
            for (int i = 0; i < rail.Count && i < Slots; i++) present.Add(rail[i].InstanceId);

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
                float arcY = SlotArcY(i);
                Vector2 target = new Vector2(SlotX(i), arcY + (sel ? SelectRise : 0f));
                if (_bottles.TryGetValue(card.InstanceId, out var view))
                {
                    view.RestBaseY = arcY;
                    view.Root.localRotation = Quaternion.Euler(0, 0, SlotTilt(i));
                    if (view.Move != null) StopCoroutine(view.Move);
                    view.Move = StartCoroutine(Tweening.MoveAnchored(view.Root, target, DrawDur, Tweening.OutQuad));
                    StyleBottle(view, selected, debuffed, animateRise: false);
                }
                else
                {
                    view = CreateBottle(card, onClick);
                    view.RestBaseY = arcY;
                    view.Root.localRotation = Quaternion.Euler(0, 0, SlotTilt(i));
                    _bottles[card.InstanceId] = view;
                    StyleBottle(view, selected, debuffed, animateRise: false);
                    view.Root.anchoredPosition = new Vector2(OffscreenRight, target.y);
                    float delay = enterOrder++ * EnterStagger;
                    view.Move = StartCoroutine(EnterAfter(view.Root, target, delay));
                }
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
                Vector2 up = new Vector2(320f, BottleBaseY + 70f); // center mixing area
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
            yield return Tweening.MoveAnchored(rt, new Vector2(OffscreenLeft, BottleBaseY), MixExitDur, Tweening.InQuad, done);
        }

        private BottleView CreateBottle(IngredientCard card, UnityAction<IngredientCard> onClick)
        {
            var root = NewRect($"Bottle_{card.InstanceId}", _railRoot);
            root.anchorMin = root.anchorMax = new Vector2(0, 0);
            root.pivot = new Vector2(0.5f, 0);

            bool hasSprite = _bottleById.TryGetValue(card.Id, out var sprite)
                             || _bottleSprites.TryGetValue(card.Type, out sprite);
            Vector2 size = hasSprite
                ? new Vector2(sprite.rect.width, sprite.rect.height)
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

            var button = root.gameObject.AddComponent<Image>();  // full-slot click target
            button.color = new Color(0, 0, 0, 0);
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = button;
            var captured = card;
            btn.onClick.AddListener(() => onClick(captured));

            // Value chip (flavor number, Flavor=Cyan per 16 §2) floats just above the bottle.
            var chip = NewRect("ValueChip", root);
            Place(chip, new Vector2(0.5f, 0), new Vector2(BottleW + 6, 12), new Vector2(0, size.y + 4));
            var chipImg = chip.gameObject.AddComponent<Image>();
            chipImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.75f);
            chipImg.raycastTarget = false;
            view.Value = NewText("Value", chip, _display, 8, TextAnchor.MiddleCenter, UITheme.Flavor);
            Stretch((RectTransform)view.Value.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.Value.text = card.Flavor.ToString();

            // Name caption — shown only when selected (too small to keep on every bottle).
            view.Name = NewText("Name", root, _body, 7, TextAnchor.LowerCenter, UITheme.TextPrimary);
            Place((RectTransform)view.Name.transform, new Vector2(0.5f, 0), new Vector2(56, 10), new Vector2(0, size.y + 18));
            view.Name.gameObject.SetActive(false);

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

            view.Name.gameObject.SetActive(isSelected);
            if (isSelected)
            {
                view.Name.supportRichText = true;
                view.Name.text = isDebuffed
                    ? $"{view.Card.Name} <color=#E84DA6>DEBUFF</color>"
                    : view.Card.Name;
            }

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

        // ── helpers ──────────────────────────────────────────────────────────────

        private static float SlotX(int i) => FirstSlotX + i * SlotPitch;
        // Curved bar: t ∈ [-1,1] across the rail; centre slots rise (dome), ends sit low and
        // lean outward so the row reads as an arc wrapping around the bartender.
        private static float SlotT(int i) => Slots > 1 ? (2f * i / (Slots - 1) - 1f) : 0f;
        private static float SlotArcY(int i) { float t = SlotT(i); return BottleBaseY + ArcHeight * (1f - t * t); }
        private static float SlotTilt(int i) => -SlotT(i) * BottleTilt;

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
