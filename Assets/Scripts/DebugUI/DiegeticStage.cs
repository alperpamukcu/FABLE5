using System.Collections.Generic;
using System.Text;
using LastCall.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LastCall.DebugUI
{
    /// <summary>
    /// The diegetic gameplay stage (v2, module 18): a layered night-club scene that sits
    /// BEHIND the UI overlay. Layers, back to front:
    ///   BackgroundLayers (far club/crowd, mid neon, front counter face) → parallax-ready
    ///   BarCounter (amber-lit band across the bottom)
    ///   BottleRail (the round's rail cards as bottle slots standing ON the counter)
    /// The UI overlay (HUD/panels) draws on its own higher-order canvas.
    ///
    /// Everything here is placeholder: flat silhouettes drawn straight from the locked v2
    /// palette (14_art_bible v2). Final pixel sprites drop in without touching the layout
    /// or the slide system. Bottle motion is DOTween-shaped (see <see cref="Tweening"/>)
    /// and collapses to instant snaps under <see cref="Motion.Reduced"/>.
    /// </summary>
    public sealed class DiegeticStage : MonoBehaviour
    {
        // ── locked v2 palette (14_art_bible v2 §3) ──────────────────────────────
        private static readonly Color NightDeep = Hex(0x0D0813);
        private static readonly Color Night2 = Hex(0x1A1023);
        private static readonly Color Night3 = Hex(0x241830);
        private static readonly Color Night4 = Hex(0x362447);
        private static readonly Color MagentaNeon = Hex(0xE84DA6);
        private static readonly Color CyanNeon = Hex(0x3BC8BE);
        private static readonly Color AmberBar = Hex(0x8F5A1E);
        private static readonly Color AmberEdge = Hex(0xC9822B);
        private static readonly Color Cream = Hex(0xF2E8D5);
        private static readonly Color Night1Text = Hex(0x241830);

        // Ingredient ramp anchors, v2 §5 (Spirit=Amber, Sour=Lime, Sweet=Magenta,
        // Bitter=Vice Red, Bubbly=Cyan, Garnish=Cream).
        private static readonly Dictionary<IngredientType, Color> RampColor = new Dictionary<IngredientType, Color>
        {
            [IngredientType.Spirit] = Hex(0xE8A33D),
            [IngredientType.Sour] = Hex(0x6FCC4B),
            [IngredientType.Sweet] = Hex(0xE84DA6),
            [IngredientType.Bitter] = Hex(0xD9455C),
            [IngredientType.Bubbly] = Hex(0x3BC8BE),
            [IngredientType.Garnish] = Hex(0xC9BCA8),
        };

        private const int Slots = 8;                // GDD: 8-card rail
        private static readonly Vector2 Reference = new Vector2(1280, 720);
        private const float Spacing = 150f;         // between slot centres
        private const float SlotY = 196f;           // bottle base sits on the counter top
        private const float BottleW = 96f;
        private const float BottleH = 168f;
        private const float OffscreenRight = 760f;
        private const float OffscreenLeft = -760f;
        private const float EnterStagger = 0.06f;   // 60 ms per slot
        private const float SlideDur = 0.26f;       // within the 180–300 ms window

        private Font _font;
        private RectTransform _railRoot;
        private readonly Dictionary<int, BottleView> _bottles = new Dictionary<int, BottleView>();
        private string _railSignature = "";

        private sealed class BottleView
        {
            public IngredientCard Card;
            public RectTransform Root;
            public Image Rim;
            public Image Body;
            public Text Value;
            public Text Name;
            public Coroutine Move;
        }

        /// <summary>How exiting bottles leave, so the wave reads as the right action.</summary>
        public enum Exit { None, Mix, Restock, Refresh }

        /// <summary>Show/hide the bottle rail (hidden while the Back Room modal is up).</summary>
        public void SetRailVisible(bool visible)
        {
            if (_railRoot != null) _railRoot.gameObject.SetActive(visible);
        }

        private void Awake()
        {
            Application.runInBackground = true; // keep the slide animations advancing unfocused
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            scaler.referenceResolution = Reference;
            var root = (RectTransform)canvasGo.transform;

            // BackgroundLayers (named for future parallax offsetting), back to front.
            // BgFar: the dark club room.
            var far = FullLayer(root, "BgFar", NightDeep);
            // Back wall behind the bar (everything above the counter surface).
            var wall = NewRect("BackWall", far);
            Stretch(wall, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 168), new Vector2(0, 0));
            var wallImg = wall.gameObject.AddComponent<Image>();
            wallImg.color = Night2; wallImg.raycastTarget = false;
            AddCrowd(far);                                   // club patrons along the back wall

            // BgMid: neon signage high on the back wall.
            var mid = FullLayer(root, "BgMid", new Color(0, 0, 0, 0));
            AddNeonSigns(mid);

            // Bar: dark front face + a bright amber lit surface the bottles stand on.
            var face = NewRect("CounterFace", root);
            Stretch(face, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 150));
            var faceImg = face.gameObject.AddComponent<Image>();
            faceImg.color = Hex(0x4A2E14); faceImg.raycastTarget = false; // dark wood
            var top = NewRect("CounterTop", root);
            Stretch(top, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 150), new Vector2(0, 176));
            top.gameObject.AddComponent<Image>().color = AmberBar;
            var edge = NewRect("CounterEdge", root);
            Stretch(edge, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 170), new Vector2(0, 178));
            edge.gameObject.AddComponent<Image>().color = AmberEdge; // amber key highlight line

            // BottleRail root: bottles anchor to bottom-centre and position by slot.
            _railRoot = NewRect("BottleRail", root);
            Stretch(_railRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
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

        private void AddCrowd(RectTransform layer)
        {
            // A row of dim head + shoulder silhouettes along the back wall, well above the
            // bar surface (bottles top out ~340) so they read as patrons behind the bar.
            for (int i = 0; i < 11; i++)
            {
                var head = NewRect($"Head{i}", layer);
                Place(head, new Vector2(0, 0), new Vector2(64, 110), new Vector2(80 + i * 116, 360));
                var img = head.gameObject.AddComponent<Image>();
                img.color = i % 2 == 0 ? Night3 : Night4;
                img.raycastTarget = false;
            }
        }

        private void AddNeonSigns(RectTransform layer)
        {
            // A few small neon signs high on the back wall (not full-width lasers).
            NeonSign(layer, MagentaNeon, new Vector2(150, 34), new Vector2(200, 560));
            NeonSign(layer, CyanNeon, new Vector2(110, 26), new Vector2(560, 600));
            NeonSign(layer, MagentaNeon, new Vector2(90, 22), new Vector2(1040, 580));
        }

        private void NeonSign(RectTransform layer, Color c, Vector2 size, Vector2 pos)
        {
            // Glow halo (dim, larger) + bright core — v2 glow = hand-placed halo.
            var halo = NewRect("Halo", layer);
            Place(halo, new Vector2(0, 0), size + new Vector2(16, 16), pos);
            var haloImg = halo.gameObject.AddComponent<Image>();
            haloImg.color = new Color(c.r, c.g, c.b, 0.28f); haloImg.raycastTarget = false;
            var core = NewRect("Sign", layer);
            Place(core, new Vector2(0, 0), size, pos);
            var coreImg = core.gameObject.AddComponent<Image>();
            coreImg.color = c; coreImg.raycastTarget = false;
        }

        // ── bottle rail binding + slide reconcile ───────────────────────────────

        /// <summary>
        /// Reconciles the 8 bottle slots to the new rail. Staying bottles tween to their
        /// slot; new bottles enter from the right with a 60 ms stagger; removed bottles
        /// leave per <paramref name="exitStyle"/>. A selection-only change (same rail)
        /// just refreshes tints — no wave.
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
                foreach (var kv in _bottles) StyleBottle(kv.Value, selected, debuffed);
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
                Vector2 target = new Vector2(SlotX(i), SlotY);
                if (_bottles.TryGetValue(card.InstanceId, out var view))
                {
                    if (view.Move != null) StopCoroutine(view.Move);
                    view.Move = StartCoroutine(Tweening.MoveAnchored(view.Root, target, SlideDur, Tweening.OutBack));
                    StyleBottle(view, selected, debuffed);
                }
                else
                {
                    view = CreateBottle(card, onClick);
                    _bottles[card.InstanceId] = view;
                    StyleBottle(view, selected, debuffed);
                    view.Root.anchoredPosition = new Vector2(OffscreenRight, SlotY);
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
            yield return Tweening.MoveAnchored(rt, target, SlideDur, Tweening.OutBack);
        }

        private void ExitBottle(BottleView view, Exit style, int order)
        {
            if (view.Move != null) StopCoroutine(view.Move);
            void Destroy() { if (view.Root != null) UnityEngine.Object.Destroy(view.Root.gameObject); }

            if (style == Exit.Mix)
            {
                // Pop forward-up off the counter, then slide out to the left.
                Vector2 up = view.Root.anchoredPosition + new Vector2(0, 90);
                StartCoroutine(Tweening.MoveAnchored(view.Root, up, 0.14f, Tweening.OutBack, () =>
                    StartCoroutine(Tweening.MoveAnchored(view.Root,
                        new Vector2(OffscreenLeft, SlotY + 90), SlideDur, Tweening.OutCubic, Destroy))));
            }
            else
            {
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
            yield return Tweening.MoveAnchored(rt, new Vector2(OffscreenLeft, SlotY), SlideDur, Tweening.OutCubic, done);
        }

        private BottleView CreateBottle(IngredientCard card, UnityAction<IngredientCard> onClick)
        {
            var root = NewRect($"Bottle_{card.InstanceId}", _railRoot);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0);
            root.sizeDelta = new Vector2(BottleW, BottleH);

            // Bottle body silhouette (type ramp colour) — placeholder slab, ~60 wide.
            var body = NewRect("Body", root);
            Stretch(body, Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-18, -40));
            var bodyImg = body.gameObject.AddComponent<Image>();

            // Neon rim: a thin (3 px) back-light hugging the body silhouette (v2 §4).
            var rim = NewRect("Rim", root);
            Stretch(rim, Vector2.zero, Vector2.one, new Vector2(15, -3), new Vector2(-15, -37));
            var rimImg = rim.gameObject.AddComponent<Image>();
            rim.SetAsFirstSibling(); // behind the body

            // Neck.
            var neck = NewRect("Neck", root);
            Place(neck, new Vector2(0.5f, 1), new Vector2(26, 44), new Vector2(0, -2));
            neck.gameObject.AddComponent<Image>().color = bodyImg.color;

            var button = root.gameObject.AddComponent<Image>();  // full-slot click target
            button.color = new Color(0, 0, 0, 0);
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = button;
            var captured = card;
            btn.onClick.AddListener(() => onClick(captured));

            // Value chip (top) and name (base).
            var value = NewText("Value", root, 22, TextAnchor.MiddleCenter, Night1Text);
            Place((RectTransform)value.transform, new Vector2(0.5f, 1), new Vector2(34, 30), new Vector2(0, -6));
            value.text = card.Flavor.ToString();
            var name = NewText("Name", root, 12, TextAnchor.UpperCenter, Cream);
            Stretch((RectTransform)name.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-8, -28), new Vector2(8, -2));
            name.text = card.Name;

            return new BottleView { Card = card, Root = root, Rim = rimImg, Body = bodyImg, Value = value, Name = name };
        }

        private void StyleBottle(BottleView view, ICollection<IngredientCard> selected, HashSet<IngredientType> debuffed)
        {
            bool isSelected = selected != null && selected.Contains(view.Card);
            bool isDebuffed = debuffed != null && debuffed.Contains(view.Card.Type);
            var ramp = RampColor[view.Card.Type];
            if (isDebuffed) ramp = Color.Lerp(ramp, NightDeep, 0.6f);

            view.Body.color = ramp;
            foreach (Transform child in view.Root)
                if (child.name == "Neck") child.GetComponent<Image>().color = ramp;

            // Selected → bright cyan rim + brighter body (v2 selection colour); else the
            // magenta club rim. Selection never moves the bottle (that would fight the
            // slide tweens); the mix "pop forward-up" is the exit flourish instead.
            view.Rim.color = isSelected ? CyanNeon : new Color(MagentaNeon.r, MagentaNeon.g, MagentaNeon.b, 0.9f);
            if (isSelected) view.Body.color = Color.Lerp(ramp, Cream, 0.25f);
            view.Name.supportRichText = true;
            view.Name.text = isDebuffed ? $"{view.Card.Name}\n<color=#E84DA6>DEBUFF</color>" : view.Card.Name;
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static float SlotX(int i) => (i - (Slots - 1) * 0.5f) * Spacing;

        private static string Signature(IReadOnlyList<IngredientCard> rail)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < rail.Count && i < Slots; i++) sb.Append(rail[i].InstanceId).Append(',');
            return sb.ToString();
        }

        private static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

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

        private Text NewText(string name, Transform parent, int size, TextAnchor anchor, Color color)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
