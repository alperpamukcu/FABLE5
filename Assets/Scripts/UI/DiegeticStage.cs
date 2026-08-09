using System.Collections.Generic;
using UnityEngine;
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
    /// The 2026-08-07 sweep removed the retired pre-menu loop this class used to carry:
    /// the on-stage bottle rail and its slide choreography, the pour-glass HUD, the mood
    /// gauge, the solo customer and his old ID card. Bottles live in the menu, customers
    /// in the HUD's seat row, and the licence card in TycoonHud — none of it ever came
    /// back here, so the code did not either. All coordinates are in the 640×360 reference
    /// space with a bottom-left origin.
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
        // the reference aspect: StageArtFit scales the counter by parentWidth/640 and
        // hangs it from the rest line, so at 16:10 the cells sit narrower AND higher.
        // ShelfCell resolves that from the live transform rather than assuming 16:9,
        // which is what the old hardcoded rack slots did.
        private const float ShelfCellPx = 80f;      // cell pitch, art px
        private const float ShelfFloorPx = 93f;     // the cell floor, art px from the art's top
        private const float ShelfCeilPx = 53f;      // the shelf board above it
        public const int ShelfCells = 8;

        private RectTransform _counter;
        private Vector2 _counterNative;

        /// <summary>
        /// Where shelf compartment <paramref name="index"/> is standing right now, in STAGE
        /// units: the centre of its opening, the floor a glass stands on, and how much
        /// headroom there is under the shelf board. False when the bar was never drawn.
        /// </summary>
        public bool ShelfCell(int index, out float centerX, out float floorY, out float height)
        {
            centerX = 0f; floorY = 0f; height = 0f;
            if (_counter == null || _counterNative.x <= 0f) return false;
            float scale = _counter.rect.width / _counterNative.x;
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
        // below the sprite's top. The two candidates drawn for this put it 2px and 54px down,
        // so it is a property of the picture, never a constant to assume (2026-07-29).
        private const float CounterSurfaceInset = 2f;
        private const float CounterFrontY = 96f;           // surface line: the bottom 96px band
        private const float Overscan = 48f;         // bleed past screen edges (aspect safety)

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

        // Ambient life, deliberately sparse: a neon flicker and nothing else.
        private Image _backgroundImage;

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
        }

        /// <summary>
        /// What the painted room cannot do for itself: blink (GDD 24 §8). The room art is its
        /// own sky and its own skyline — the sign is the one live element on the wall.
        /// </summary>
        private void BuildBackdrop(RectTransform root, Vector2 native)
        {
            // Sized and scaled exactly like the room art, so the sign hangs on the wall the
            // picture actually draws rather than on the screen edge.
            var host = NewRect("Backdrop", root);
            var hostFit = host.gameObject.AddComponent<StageArtFit>();
            hostFit.Native = native;

            _neon = new NeonBlink();

            // The bar's own sign, high and off to one side — the one deliberate touch of colour
            // in a scene that is otherwise all shadow. Its word is real text drawn in the pixel
            // font: the art generator cannot spell, so a sign that has to say something is built
            // here rather than painted (2026-07-29).
            BuildLetteredNeon(host, new Vector2(470f, 300f), "LAST CALL", UITheme.Magenta[4]);
        }

        /// <summary>
        /// A neon sign whose word is real text: the word set in the display face, and a soft
        /// copy behind it for the glow. Both blink together, so the sign reads as one object.
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
            _neon.Register(label, 3f, 9f, 0.25f);
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

            // The room. Real club background when installed, else the flat procedural
            // sky / crowd / neon placeholders so a broken reference is still a visible bar.
            if (backgroundSprite != null)
            {
                // Scaled to cover by ONE factor rather than stretched to a fixed overscanned
                // rect: the old way pulled the art 1.24× across and 1.36× up, so no pixel of it
                // was square and the room stood 9% too tall (2026-07-29). See StageArtFit.
                var bg = NewRect("Background", root);
                var bgImg = bg.gameObject.AddComponent<Image>();
                bgImg.sprite = backgroundSprite; bgImg.raycastTarget = false;
                var fit = bg.gameObject.AddComponent<StageArtFit>();
                fit.Native = backgroundSprite.rect.size;
                _backgroundImage = bgImg;

                // The weather is a SIBLING of the room, not a child of it. Parented under the
                // room it drew on TOP of it — a UI child always does — which is how an extra sky
                // layer came to cover the very skyline it was meant to sit behind (2026-07-29).
                BuildBackdrop(root, backgroundSprite.rect.size);
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
            }

            // The bar. A drawn asset: it carries EMPTY shelves, which is not decoration but
            // structure — glassware is a buyable upgrade and those shelves are where the
            // bought glasses get drawn. The surface line inside the art is CounterSurfaceInset
            // below its top, and that line is pinned to CounterRestY, so everything on the
            // counter still stands on a constant. Scaled by one factor, not stretched.
            // There is no procedural counter to fall back on, so a lost reference is an
            // invisible bar and no other symptom — exactly what happened when this asset was
            // re-imported under a fresh GUID while the scene pointed at the old one (2026-07-29).
            if (counterSprite == null)
                Debug.LogWarning("DiegeticStage: no counterSprite — the bar will not be drawn. " +
                                 "Check the reference in the scene, or re-run LastCall > Create Debug Scene.");
            if (counterSprite != null)
            {
                var c = NewRect("Counter", root);
                var counterImage = c.gameObject.AddComponent<Image>();
                counterImage.sprite = counterSprite; counterImage.raycastTarget = false;
                var cfit = c.gameObject.AddComponent<StageArtFit>();
                cfit.Fit = StageArtFit.Mode.WidthAligned;
                cfit.Native = counterSprite.rect.size;
                cfit.RestLineY = CounterRestY;
                cfit.RestFromTop = CounterSurfaceInset;
                _counter = c;
                _counterNative = counterSprite.rect.size;
            }

            // Cash register on the bar top, with the wallet in its display window (the player
            // reads their money diegetically from the till).
            if (registerSprite != null)
            {
                var reg = NewRect("Register", root);
                reg.anchorMin = reg.anchorMax = new Vector2(0, 0);
                reg.pivot = new Vector2(0.5f, 0);
                // Fixed footprint (hi-bit): a 2x-density sprite renders finer pixels
                // into the same 57px slot instead of doubling on screen.
                const float regW = 57f;
                reg.sizeDelta = new Vector2(regW, regW * registerSprite.rect.height / registerSprite.rect.width);
                // Down onto the surface, not balanced on the far edge: the rest line IS that
                // edge, so an object placed exactly on it reads as standing behind the bar
                // rather than on it (2026-07-29).
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
                var shadow = NewRect("TillShadow", root);
                shadow.anchorMin = shadow.anchorMax = new Vector2(0, 0);
                shadow.pivot = new Vector2(0.5f, 0.5f);
                shadow.sizeDelta = new Vector2(regW * 0.92f, 5f);
                shadow.anchoredPosition = new Vector2(RegisterX, RegisterBaseY + 1f);
                var shImg = shadow.gameObject.AddComponent<Image>();
                shImg.color = new Color(0f, 0f, 0f, 0.42f); shImg.raycastTarget = false;
                shadow.SetSiblingIndex(reg.GetSiblingIndex());

                // The money sits IN the till's display window, and the window's place is read
                // off the art rather than guessed at. The old plaque was a hand-written 46x14
                // at a hand-written offset, so it overhung the till on both sides (2026-07-29).
                float regH = reg.sizeDelta.y;
                var plaque = NewRect("MoneyPlaque", root);
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

            if (!Motion.Reduced) StartCoroutine(Ambient());
        }

        /// <summary>
        /// Ambient life, deliberately sparse: the room flickers off for a frame every few
        /// seconds. Purely cosmetic — this jitter never touches RunRng, so run determinism
        /// is unaffected.
        /// </summary>
        private System.Collections.IEnumerator Ambient()
        {
            float nextFlicker = Random.Range(3f, 7f);
            while (true)
            {
                nextFlicker -= Time.unscaledDeltaTime;
                if (nextFlicker <= 0f && _backgroundImage != null)
                {
                    _backgroundImage.color = new Color(0.72f, 0.72f, 0.78f);
                    yield return new WaitForSecondsRealtime(0.05f);
                    if (_backgroundImage != null) _backgroundImage.color = Color.white;
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
