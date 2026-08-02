using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The first playable of the tycoon loop (PLAN_tycoon_pivot P3): a functional, plain
    /// overlay that drives <see cref="TycoonRun"/> — a seat row with order bubbles and
    /// patience clocks, a top bar with the till and the day's satisfaction, and the
    /// day-end invoice with refills, brands, stools and the next day.
    ///
    /// Deliberately interim: pouring still happens by clicking shelf bottles (the shaker
    /// flow is P4), and the seat row is UI panels, not animated patrons (P8). The point is
    /// to make the loop the *played* loop so the sim and the hands can start tuning it.
    /// </summary>
    public sealed class TycoonHud : MonoBehaviour
    {
        [SerializeField] private Font bodyFont;
        [SerializeField] private Font displayFont;
        [SerializeField] private DiegeticStage stage;

        private GameBootstrap _bootstrap;
        private TycoonRun Run => _bootstrap != null ? _bootstrap.Tycoon : null;

        private Font _body;
        private Font _display;

        // top bar
        private Text _dayText;
        private Text _moneyText;
        private Text _crowdText;
        /// <summary>The bar's standing (v5 P12): the average, then five filled/empty stars.
        /// Replaces the TONIGHT satisfaction bar (D3) — reputation is what the player steers
        /// by now, and it carries between nights instead of resetting every morning.</summary>
        private Text _ratingText;
        private RectTransform _starsFill;
        private readonly Image[] _ratingStars = new Image[BarRating.MaxStars];

        // Seats at the counter (GDD 24 §4, 2026-07-22): customers sit along the bar as
        // head-and-shoulders busts, not in a bottom strip. Each bust rises/slides into its
        // stool when its patron arrives, wears a floating order tag, and is the click target.
        private const int SeatSlots = 6;
        // HUD-space y (from bottom) of the bar top, where a customer's body is cut off. DERIVED
        // from the stage rather than copied: the HUD is 1280x720 against the stage's 640x360, so
        // it is exactly twice the stage's own line. It used to be a hand-written 279, which was
        // right for the old counter art and 19 units too high for the new one — every customer
        // floated that far above the bar (2026-07-29).
        private const float StageToHud = 720f / 360f;
        private const float CounterLineY = DiegeticStage.CounterTopY * StageToHud;
        private const float BustW = 108f;
        private const float BustH = 128f;
        private const float WalkSpeed = 340f;       // walk-in speed (ref px/s) — slightly slower, per the notes (P15)
        private const float ExitSpeed = 560f;       // walk-out speed (ref px/s), back off the right edge
        private const float OffscreenMargin = 150f; // how far past the right edge they start/finish
        private const float OrderAnimSeconds = 2.4f;               // the one-shot "placing the order" beat
        private const float DrinkSipSeconds = 2.6f, DrinkHoldSeconds = 1.8f;   // one sip cycle (×3 = the savour)

        // The animated customer (2026-07-23): a full-body pixel sprite shown from about the waist
        // up, with the counter clipping the legs. Frames load from Resources/Patron/<clip>.
        // Re-aligned off the ART's own bbox (2026-07-31, the author's note): the figure spans
        // y 13–171 of the 180px canvas, so at 350 the head tops out at -FootDrop+324.7 and the
        // feet reach -FootDrop+17.5. FootDrop 150 crops ~43% of the figure below the counter
        // (the same waist as before), and the window ends 5px over the head — the gauge and
        // the tag now HUG the head instead of floating 28px above it.
        private const float CharSize = 350f;       // the character image, a touch bigger again
        private const float CharWiden = 1.18f;     // stretch a touch wider — the sprite is lanky for the bar
        private const float CharWinH = 180f;       // ends just over the head (art-measured)
        private const float CharFootDrop = 150f;   // same waist crop at 350 (art-measured)
        private enum PatronClip { Idle, Order, Drink, Walk, Cheer, Upset }
        private const float ReactSeconds = 1.15f;   // the one-shot reaction beat before they go
        private Dictionary<PatronClip, Sprite[]> _patron;
        private RectTransform _hudRoot;            // the canvas rect — the screen's right edge for entrances

        private sealed class SeatView
        {
            public RectTransform Root;       // the customer + tag, positioned at the counter (click target)
            public CanvasGroup Group;        // fades them in as they walk up
            public Image Portrait;           // the animated character image (inside the counter mask)
            public RectTransform Tag;        // the floating order ticket above the head
            public Image TagBg;
            public Text Name;
            public Text Wants;
            public Text Order;
            public Image Icon;               // the ordered drink, drawn by DrinkIcon (v5 P13)
            public Image PatienceFill;
            public float SeatX;              // this stool's resting x
            public DirtyGlass Dirty;         // the empty glass left on this stool (D2)
            public RectTransform DirtyProp;  // its clickable prop on the counter
            public float WalkT;              // 0..1 walk-in progress
            public bool Exiting;             // playing the leave animation
            public float ExitT;              // 0..1 leave progress
            public bool ExitStorm;           // stormed off (angry exit) vs served (calm)
            public CustomerVisit Visit;      // who is assigned to this stool (stable until they leave)
            public float AnimClock;          // running time for the looping clips (idle, walk)
            public bool WasOrdered;          // edge-detect the deciding→ordered moment
            public float OrderAnimLeft;      // remaining "placing the order" one-shot time
            public float DrinkT;             // time since they started drinking
            public float ReactLeft;          // remaining departure-reaction one-shot time
            public PatronClip ReactClip;     // Cheer or Upset, chosen from their satisfaction
        }
        private readonly List<SeatView> _seats = new List<SeatView>();

        // The finished drink on the counter (GDD 24 §3, 2026-07-22): a glass you drag onto a
        // customer to serve, carried with a heavy, springy AAA feel.
        /// <summary>The bin on the counter (v5 P13 / C7). A drink is thrown away by carrying it
        /// there, the same verb that serves it — the BIN GLASS button is gone.</summary>
        private RectTransform _binProp;
        private Image _binImage;

        private RectTransform _drinkGlass;
        private Image _drinkGlassLiquid;
        private Image _drinkGlassArt;
        private GlasswareDefinition _drinkGlassware;
        private int _drinkGlassTier = 1;
        private Image _drinkGlassBack;
        private RectTransform _glassRack;
        private const float CarriedGlassHeight = 116f;
        private bool _glassGrabbed;
        private Vector2 _glassGrabOffset;
        private Vector2 _glassPos, _glassVel;
        private float _glassAngle, _glassAngVel;
        private Vector2 _glassHome;
        private bool _glassShown;
        private const float GlassStiffness = 130f;   // spring to the cursor
        private const float GlassDamping = 12f;
        private const float GlassAngStiffness = 90f;  // spring the tilt back upright
        private const float GlassAngDamping = 9f;

        // day end — two steps now (the author, 2026-08-01): first the bill alone, then
        // the market, each with its own verb on the same button.
        private RectTransform _dayEndPanel;
        private Text _invoiceText;
        private RectTransform _offerRow;
        private RectTransform _openTomorrow;
        private Text _bannerText;
        private RectTransform _dayEndBill, _dayEndTablet;
        private Text _openTomorrowLabel, _dayEndTitle;
        private int _dayEndStep;
        private RectTransform _cardTarget;
        private Text _marketTip;

        // ledger history (GDD 24 §7, 2026-07-22): the register opens the book of past days.
        private RectTransform _ledgerPanel;
        private RectTransform _ledgerRows;

        // ID card (GDD 24 §5): the licence you read a customer by. Emotion→recipe pivot
        // (2026-07-22): it now shows the drink's RECIPE and the garnishes they want, not moods.
        private RectTransform _idRoot;
        private Image _idPhoto;
        private Text _idName, _idAgeFrom, _idRel, _idIntent, _idOrder, _idOrderParts, _idRates, _idRatesLabel;
        private RectTransform _idRecipeTip;
        private Text _idRecipeTipText;
        private RectTransform _idPrefRow;

        /// <summary>The book's line for the ordered drink, shown at the card (hover).</summary>
        private void ShowOrderRecipeTip()
        {
            var visit = _idVisit;
            if (visit == null || _idRecipeTip == null) return;
            var r = visit.Order.Wanted;
            string prep = r.Id == "draught" ? "ON TAP" : r.Id == "neat_pour" ? "NEAT"
                : r.Prep == PrepMethod.Shaken ? "SHAKEN"
                : r.Prep == PrepMethod.Stirred ? "STIRRED" : "BUILT";
            // One pour to a line, the numbers in bright cyan — vertical, like a spec card.
            var lines = new List<string> { $"<color=#FF6EC7>{prep}</color>" };
            foreach (var band in r.RatioRequirements)
                lines.Add((band.IsStyleBand ? band.Style.Replace('_', ' ') : band.Type.ToString())
                    .ToUpperInvariant()
                    + $"  <color=#8FE8DC>{band.MinRatio * 100:0}–{band.MaxRatio * 100:0}%</color>");
            if (r.MinFill > 0) lines.Add($"FILL  <color=#8FE8DC>{r.MinFill * 100:0}%+</color>");
            if (!string.IsNullOrEmpty(r.GlassId))
                lines.Add($"<color=#9C93A8>{r.GlassId.ToUpperInvariant()}</color>");
            _idRecipeTipText.text = string.Join("\n", lines);
            _idRecipeTip.sizeDelta = new Vector2(252, 18 + lines.Count * 20);
            _idRecipeTip.gameObject.SetActive(true);
            _idRecipeTip.SetAsLastSibling();
        }
        private Image _idOrderIcon;

        // The shop tablet (v5 P13). Two errands, not one wall of cards: what goes behind the
        // bar, and what the room itself is made of.
        private static readonly string[] ShopTabs = { "RESTOCK", "BOTTLES", "RECIPES", "UPGRADES" };
        private readonly Image[] _shopTabKeys = new Image[ShopTabs.Length];
        private readonly Text[] _shopTabLabels = new Text[ShopTabs.Length];
        private int _shopTab;
        private Text _tabletTill;
        private static readonly Color TabletShell = new Color(0.13f, 0.12f, 0.15f, 1f);
        private static readonly Color TabletScreen = new Color(0.09f, 0.10f, 0.13f, 1f);
        private static readonly Color TabletLens = new Color(0.30f, 0.30f, 0.34f, 1f);
        private CustomerVisit _idVisit;
        private const float IdTrackW = 176f;

        private TycoonServiceFlow _flow;
        private TycoonPhase _lastPhase = TycoonPhase.DayOpen;
        private int _lastStormedCount;   // to catch a customer storming off (GDD 24 §4)
        private Text _toast;
        private float _toastUntil;

        private void Awake()
        {
            var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _body = bodyFont != null ? bodyFont : legacy;
            _display = displayFont != null ? displayFont : legacy;

            _bootstrap = GetComponent<GameBootstrap>();
            if (_bootstrap != null) _bootstrap.RunStarted += OnRunStarted;
            _flow = GetComponent<TycoonServiceFlow>();

            BuildUi();
            if (stage != null) stage.SetRegisterHandler(ToggleLedger);
        }

        private void OnDestroy()
        {
            if (_bootstrap != null) _bootstrap.RunStarted -= OnRunStarted;
        }

        private void ResetSeats()
        {
            foreach (var v in _seats)
            {
                v.Visit = null;
                v.Exiting = false;
                v.ExitT = 0f;
                v.WalkT = 0f;
                if (v.Group != null) v.Group.alpha = 1f;
                if (v.Root != null) v.Root.gameObject.SetActive(false);
            }
        }

        private void OnRunStarted()
        {
            _lastPhase = TycoonPhase.DayOpen;
            _lastStormedCount = 0;
            ResetSeats();
            _dayEndPanel.gameObject.SetActive(false);
            _bannerText.gameObject.SetActive(false);
            _flow?.CloseFlow();
            CloseId();
            if (_ledgerPanel != null) _ledgerPanel.gameObject.SetActive(false);
            if (_drinkGlass != null) { _drinkGlass.gameObject.SetActive(false); _glassShown = false; _glassGrabbed = false; }
            if (stage != null)
            {
                stage.SetSoloCustomerVisible(false);
                stage.HideBuildDressing();   // bottles live in the menu now (2026-07-22)
            }
            ApplyBarLook();
        }

        private void Update()
        {
            var run = Run;
            if (run == null) return;

            if (run.Phase == TycoonPhase.DayOpen)
            {
                // Menus slow the world (GDD 24 §10): mixing or reading a licence must not
                // cost a storm-off by itself, but the clock never fully stops.
                bool menuOpen = (_flow != null && _flow.IsOpen) ||
                                (_idRoot != null && _idRoot.gameObject.activeSelf);
                run.Tick(Time.deltaTime * (menuOpen ? (float)TycoonConfig.MenuTimeScale : 1f));
            }

            if (run.Phase != _lastPhase)
            {
                _lastPhase = run.Phase;
                if (run.Phase == TycoonPhase.DayEnd) ShowDayEnd();
                if (run.Phase == TycoonPhase.Closed) ShowClosed();
            }

            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil)
                _toast.gameObject.SetActive(false);

            // Backing out of the flow with a drink still in the shaker leaves the counter empty,
            // because an unpoured drink is not a drink yet (2026-07-28). Say so, or the player
            // is left looking for a glass that was never filled.
            bool flowOpen = _flow != null && _flow.IsOpen;
            if (_flowWasOpen && !flowOpen && run.DrinkWaitingInShaker)
                Toast("STILL IN THE SHAKER — POUR IT INTO A GLASS");
            _flowWasOpen = flowOpen;

            RefreshTopBar();
            RefreshSeats();
            UpdateDrinkGlass();
        }

        private bool _flowWasOpen;

        // ── the floor ───────────────────────────────────────────────────────────

        private void OnMenuClicked() => _flow?.Open();

        private void OnSeatClicked(int index)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;   // finish the build first
            var visit = _seats[index].Visit;
            if (visit == null) return;

            // A bowl in hand goes down in front of them (v5 P16). Before the waiting check,
            // because a customer nursing a drink is exactly who takes a bowl of nuts — and
            // Core's own refusals do the talking when the snack cannot land (never alone,
            // bowl empty), so the toast is the rule speaking, not the menu's guess at it.
            if (_snackInHand != null)
            {
                var snack = _snackInHand;
                _snackInHand = null;
                try
                {
                    run.ServeSnack(snack.Id, visit);
                    Toast($"{snack.Name.ToUpperInvariant()} — ON THE TAB");
                }
                catch (InvalidOperationException e) { Toast(e.Message.ToUpperInvariant()); }
                RefreshSnackRow(run);
                return;
            }

            if (visit.State != VisitState.Waiting) return;
            if (!visit.HasOrdered) return;   // still deciding — no order to read yet (2026-07-23)

            // Clicking a customer reads their licence (GDD 24 §5). Serving is a separate act:
            // the finished drink is carried over and dropped on them (drag the glass).
            ShowId(visit);
        }

        /// <summary>Hands the ready drink to seat <paramref name="index"/> (the glass was dragged
        /// onto them). Returns true if it was served.</summary>
        private bool ServeSeat(int index)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return false;
            var visit = _seats[index].Visit;
            if (visit == null || visit.State != VisitState.Waiting) return false;
            if (!visit.HasOrdered) return false;   // can't hand a drink to someone still deciding
            if (!run.DrinkReady) return false;     // only what is in the glass goes out

            var verdict = run.ServeTo(visit);
            CloseId();
            Sfx.Play("serve_clink");                          // the glass lands in front of them
            LogVerdict(visit, verdict);
            StartCoroutine(ServeReaction(index, verdict));   // reaction + payment float up
            return true;
        }

        // ── the bussing beat (D2, v5 P14) ────────────────────────────────────────
        // A drinker leaves the empty glass on the stool, and the stool stays blocked until it
        // is cleared: the player's click does it now, the bar's slow clock does it in seven
        // seconds. The prop appears where the customer sat; clicking it is the bussing.

        private void RefreshDirtyGlasses(TycoonRun run)
        {
            foreach (var v in _seats)
            {
                bool show = v.Dirty != null && !v.Dirty.Cleared;
                if (show && v.DirtyProp == null)
                {
                    var prop = NewRect("DirtyGlass", _hudRoot);
                    prop.anchorMin = prop.anchorMax = new Vector2(0, 0);
                    prop.pivot = new Vector2(0.5f, 0);
                    prop.sizeDelta = new Vector2(34, 52);
                    // ON the counter's drawn surface, not floating at the waist-clip line
                    // (the author's report): the clip line is the counter's BACK edge; the
                    // top surface the glass stands on reads ~36px lower in the scene.
                    prop.anchoredPosition = new Vector2(v.SeatX, CounterLineY - 36f);
                    var img = prop.gameObject.AddComponent<Image>();
                    // The SAME glass everywhere (the author, 2026-08-02): the empty on the
                    // counter is the drawn vessel the drink was served in, at its line's
                    // tier — not a stock photo of some other glass.
                    GlasswareDefinition dirtyDef = null;
                    if (run != null && v.Dirty.GlasswareId != null)
                        foreach (var g in run.Glassware)
                            if (g.Id == v.Dirty.GlasswareId) { dirtyDef = g; break; }
                    if (dirtyDef != null)
                        img.sprite = GlassArt.For(dirtyDef, run.GlassTier(dirtyDef.Id)).Sprite;
                    else img.sprite = ItemArt.Glass;
                    img.preserveAspect = true;
                    img.color = new Color(1f, 1f, 1f, 0.85f);
                    if (img.sprite == null) img.color = new Color(0.8f, 0.9f, 0.95f, 0.5f);
                    var view = v;
                    var btn = prop.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(() =>
                    {
                        if (view.Dirty == null) return;
                        view.Dirty.Bus();
                        Sfx.Play("glass_down", 0.9f);
                        Toast("GLASS CLEARED — STOOL FREE");
                    });
                    var sink = prop.gameObject.AddComponent<PressSink>();
                    sink.Face = prop; sink.Depth = 3f; sink.Lift = 3f; sink.Tint = img;
                    v.DirtyProp = prop;
                }
                else if (!show && v.DirtyProp != null)
                {
                    Destroy(v.DirtyProp.gameObject);
                    v.DirtyProp = null;
                    v.Dirty = null;
                }
            }
        }

        // ── the snack bowls (v5 P16) ─────────────────────────────────────────────
        // On the counter, left end, opposite the bin: click a bowl to take it in hand, click
        // a customer to put it down. The plan said "from the menu"; the bowls stand on the
        // counter instead because a snack has no prep — sending the player through the drink
        // menu for a bowl of nuts would be a stage with nothing on it.

        private SnackDefinition _snackInHand;
        private readonly List<(SnackDefinition snack, Image art, Text stock)> _snackBowls =
            new List<(SnackDefinition, Image, Text)>();

        private void BuildSnackRow(RectTransform root)
        {
            var run = Run;
            if (run == null || run.Snacks.Count == 0) return;
            float x = 24f;
            foreach (var snack in run.Snacks)
            {
                var s = snack;
                var bowl = NewRect($"Snack_{s.Id}", root);
                bowl.anchorMin = bowl.anchorMax = bowl.pivot = new Vector2(0f, 0f);
                bowl.sizeDelta = new Vector2(76, 84);
                bowl.anchoredPosition = new Vector2(x, 96);
                x += 82f;
                var hit = bowl.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0.001f);

                var art = NewRect("Art", bowl);
                Place(art, new Vector2(0.5f, 1), new Vector2(72, 54), new Vector2(0, 0));
                var img = art.gameObject.AddComponent<Image>();
                img.sprite = ItemArt.Load($"snack_{s.Id}");
                img.preserveAspect = true; img.raycastTarget = false;
                if (img.sprite == null) img.color = UITheme.Amber[2];   // no art yet: a warm chip

                var label = NewText("N", bowl, _body, 8, TextAnchor.LowerCenter, UITheme.TextSecondary);
                Place(label.rectTransform, new Vector2(0.5f, 0), new Vector2(96, 24), Vector2.zero);
                label.text = s.Name.ToUpperInvariant();

                var btn = bowl.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    var r = Run;
                    if (r == null || r.Phase != TycoonPhase.DayOpen) return;
                    if (r.SnackLeft(s.Id) <= 0) { Toast($"THE {s.Name.ToUpperInvariant()} BOWL IS EMPTY TODAY"); return; }
                    _snackInHand = _snackInHand == s ? null : s;   // click again to put it back
                    Sfx.Play(_snackInHand != null ? "garnish" : "glass_down", 0.8f);
                    Toast(_snackInHand != null
                        ? $"{s.Name.ToUpperInvariant()} IN HAND — CLICK A CUSTOMER"
                        : "PUT IT BACK");
                    RefreshSnackRow(r);
                });
                var sink = bowl.gameObject.AddComponent<PressSink>();
                sink.Face = art; sink.Depth = 4f; sink.Lift = 3f; sink.Tint = img;

                _snackBowls.Add((s, img, label));
            }
        }

        /// <summary>Stock counts and the in-hand highlight, redrawn after anything changes.</summary>
        private void RefreshSnackRow(TycoonRun run)
        {
            foreach (var (snack, art, stock) in _snackBowls)
            {
                int left = run.SnackLeft(snack.Id);
                stock.text = left > 0
                    ? $"{snack.Name.ToUpperInvariant()} · {left}"
                    : $"{snack.Name.ToUpperInvariant()} · OUT";
                var baseCol = art.sprite != null ? Color.white : UITheme.Amber[2];
                art.color = left <= 0 ? new Color(baseCol.r, baseCol.g, baseCol.b, 0.35f)
                    : _snackInHand == snack ? new Color(1f, 1f, 0.82f, 1f)
                    : baseCol;
            }
        }

        // ── the drink you carry (GDD 24 §3, 2026-07-22) ──────────────────────────

        private void BuildDrinkGlass(RectTransform root)
        {
            _glassHome = new Vector2(0, -200f);   // staged on the counter, above the MENU button
            // The bin, standing on the counter at the right-hand end, in front of the bar. Built
            // before the glass so the carried drink passes over it rather than under it.
            _binProp = NewRect("Bin", root);
            _binProp.anchorMin = _binProp.anchorMax = _binProp.pivot = new Vector2(1f, 0f);
            // The clean can (the author, 2026-08-02: the bagged one read as filth): a
            // chrome pedal bin with a magenta neon edge, standing HALF out of frame.
            _binProp.sizeDelta = new Vector2(150, 270);
            _binProp.anchoredPosition = new Vector2(72, 2);
            _binImage = _binProp.gameObject.AddComponent<Image>();
            _binImage.sprite = ItemArt.Load("bin_clean");
            if (_binImage.sprite == null) _binImage.sprite = ItemArt.Load("bin_prop");
            _binImage.preserveAspect = true;
            _binImage.raycastTarget = false;
            _binImage.color = Color.white;
            if (_binImage.sprite == null) _binImage.enabled = false;

            // The drink you carry to a seat is the real glass now (v5 P14 / C9): the same
            // drawing the serve stage stands on the counter, with its interior filled to the
            // level the drink is actually at. It used to be a translucent box with a cyan bar
            // for a rim, which said "a drink" and nothing about WHICH drink.
            _drinkGlass = NewRect("DrinkGlass", root);
            _drinkGlass.anchorMin = _drinkGlass.anchorMax = _drinkGlass.pivot = new Vector2(0.5f, 0.5f);
            _drinkGlass.sizeDelta = new Vector2(78, CarriedGlassHeight);
            _drinkGlass.anchoredPosition = _glassHome;

            var body = _drinkGlass.gameObject.AddComponent<Image>();   // invisible, but the grab target
            body.color = new Color(0f, 0f, 0f, 0.004f);
            var grab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            grab.callback.AddListener(ev => OnGlassGrab((PointerEventData)ev));
            _drinkGlass.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);

            // The layer architecture (the author, 2026-08-02): BACK face and base first,
            // the liquid over it, the FRONT face — interior fully clear — on top.
            var backRt = NewRect("Back", _drinkGlass);
            Stretch(backRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassBack = backRt.gameObject.AddComponent<Image>();
            _drinkGlassBack.preserveAspect = true;
            _drinkGlassBack.raycastTarget = false;
            _drinkGlassBack.enabled = false;

            var liquid = NewRect("Liquid", _drinkGlass);
            Stretch(liquid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassLiquid = liquid.gameObject.AddComponent<Image>();
            _drinkGlassLiquid.raycastTarget = false;
            _drinkGlassLiquid.type = Image.Type.Filled;
            _drinkGlassLiquid.fillMethod = Image.FillMethod.Vertical;
            _drinkGlassLiquid.fillOrigin = (int)Image.OriginVertical.Bottom;
            _drinkGlassLiquid.preserveAspect = true;

            var art = NewRect("Art", _drinkGlass);
            Stretch(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _drinkGlassArt = art.gameObject.AddComponent<Image>();
            _drinkGlassArt.raycastTarget = false;
            _drinkGlassArt.preserveAspect = true;

            var hint = NewText("Hint", _drinkGlass, _body, 10, TextAnchor.UpperCenter, UITheme.Cyan[4]);
            Place(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(170, 18), new Vector2(0, 24));
            hint.text = "DRAG TO SERVE →";
            hint.raycastTarget = false;

            _drinkGlass.gameObject.SetActive(false);
        }

        private void OnGlassGrab(PointerEventData ev)
        {
            if (Run == null || Run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;
            _glassGrabbed = true;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_drinkGlass.parent, ev.position, null, out Vector2 cursor))
                _glassGrabOffset = _glassPos - cursor;   // keep the grab point under the cursor
            else
                _glassGrabOffset = Vector2.zero;
        }

        /// <summary>The finished drink sits on the counter and is dragged onto a customer to
        /// serve (GDD 24 §3). Heavy, springy carry with a lean into the motion (AAA feel).</summary>
        private void UpdateDrinkGlass()
        {
            var run = Run;
            // The glass on the counter is the SERVING glass and nothing else. A drink still in
            // the shaker is a half-finished build, not something you can pick up and carry — it
            // used to appear here, which is how an unpoured drink reached a customer (2026-07-28).
            bool ready = run != null && run.Phase == TycoonPhase.DayOpen
                && (_flow == null || !_flow.IsOpen)
                && run.DrinkReady;

            if (!ready)
            {
                if (_glassShown) { _drinkGlass.gameObject.SetActive(false); _glassShown = false; _glassGrabbed = false; }
                return;
            }

            if (!_glassShown)
            {
                _glassShown = true;
                _drinkGlass.gameObject.SetActive(true);
                _glassPos = _glassHome; _glassVel = Vector2.zero; _glassAngle = 0f; _glassAngVel = 0f;
            }
            // The glass shows the drink as it was actually built: the vessel it chose, its
            // blended colour and its real fill level — no fixed glass, colour or amount.
            int drinkTier = run.GlassTier(run.ServingGlassware?.Id);
            var piece = GlassArt.For(run.ServingGlassware, drinkTier);
            if (!ReferenceEquals(_drinkGlassware, run.ServingGlassware) || drinkTier != _drinkGlassTier
                || _drinkGlassArt.sprite == null)
            {
                _drinkGlassware = run.ServingGlassware;
                _drinkGlassTier = drinkTier;
                // Front face over the liquid when the set is modular; the composite
                // sprite carries a run without the generated art.
                _drinkGlassArt.sprite = piece.Front != null ? piece.Front : piece.Sprite;
                _drinkGlassBack.sprite = piece.Back;
                _drinkGlassBack.enabled = piece.Back != null;
                _drinkGlassLiquid.sprite = piece.Fill;
                _drinkGlass.sizeDelta = new Vector2(CarriedGlassHeight * piece.Aspect, CarriedGlassHeight);
            }
            _drinkGlassLiquid.color = DrinkColor();
            _drinkGlassLiquid.fillAmount = piece.FillAmount((float)run.ServingGlass.FillFraction);
            // The finishing touches ride the carried glass too (P14): the customer is handed
            // the drink that was actually finished, salt and wedge and all.
            GlassDecor.Sync(_drinkGlass, piece, run.ServingGlass);

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var mouse = Mouse.current;

            if (_glassGrabbed && (mouse == null || !mouse.leftButton.isPressed))
            {
                _glassGrabbed = false;
                // Dropped in the bin: the drink is thrown away (v5 P13 / C7). Checked before
                // the seats, because the bin sits on the counter among them and a drink let go
                // over it was plainly meant for it.
                if (IsOverBin(mouse))
                {
                    int fee = run.DiscardGlass();
                    Toast(fee > 0 ? $"BINNED · -${fee}" : "BINNED");
                    if (fee > 0)
                        LogService($"<color=#F27D8A>BINNED</color> a built drink · -${fee}");
                    _drinkGlass.gameObject.SetActive(false);
                    _glassShown = false;
                    return;
                }
                int seat = SeatUnderCursor(mouse);
                if (seat >= 0 && ServeSeat(seat))
                {
                    _drinkGlass.gameObject.SetActive(false);   // handed over; a new drink re-shows it
                    _glassShown = false;
                    return;
                }
            }

            // The bin only lifts its lid -- brightens -- while there is something to throw in it
            // and the hand is over it, so it never nags at an empty counter.
            if (_binImage != null)
                _binImage.color = _glassGrabbed && IsOverBin(mouse)
                    ? Color.white
                    : new Color(0.72f, 0.72f, 0.74f, 1f);

            Vector2 target = _glassHome;
            if (_glassGrabbed && mouse != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_drinkGlass.parent, mouse.position.ReadValue(), null, out Vector2 cursor))
                target = cursor + _glassGrabOffset;

            // Spring the glass to the target with a little overshoot (weight), and lean it into
            // the horizontal motion, springing back upright — the carry has heft.
            _glassVel += (target - _glassPos) * (GlassStiffness * dt);
            _glassVel *= Mathf.Exp(-GlassDamping * dt);
            _glassPos += _glassVel * dt;

            float targetAngle = Mathf.Clamp(-_glassVel.x * 0.035f, -26f, 26f);
            _glassAngVel += (targetAngle - _glassAngle) * (GlassAngStiffness * dt);
            _glassAngVel *= Mathf.Exp(-GlassAngDamping * dt);
            _glassAngle += _glassAngVel * dt;

            _drinkGlass.anchoredPosition = _glassPos;
            _drinkGlass.localRotation = Quaternion.Euler(0, 0, _glassAngle);
        }

        /// <summary>Which occupied stool the cursor is over, or -1.</summary>
        private int SeatUnderCursor(Mouse mouse)
        {
            if (mouse == null) return -1;
            var pos = mouse.position.ReadValue();
            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                if (s.Visit == null || s.Exiting || !s.Root.gameObject.activeSelf) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(s.Root, pos, null))
                    return i;
            }
            return -1;
        }

        /// <summary>The carried drink's colour: its ingredients' true liquid colours, blended by
        /// share in linear space (2026-07-23) — clear spirits read pale, and a mix stays clean.</summary>
        private Color DrinkColor() => UITheme.DrinkColor(Run?.Shelf, Run?.ServingGlass);

        /// <summary>How a served customer reacts (GDD 24 §4, §10): a word for the read/serve
        /// and the payment, rising from the seat with a little pop. Green when they're pleased,
        /// red when it's the wrong drink; a gold call when they order another round.</summary>
        private System.Collections.IEnumerator ServeReaction(int seatIndex, ServiceVerdict verdict)
        {
            var seat = _seats[seatIndex].Root;
            bool wrong = verdict.Match == OrderMatch.Wrong;
            Color tone = verdict.OrdersAgain ? UITheme.Amber[3]
                : wrong ? UITheme.ViceRed[3] : UITheme.Lime[3];

            // Only the FACE answers at the serve (2026-07-31): they can see the drink, so the
            // reaction line is honest — but the bill is not on the table yet. The money and
            // the stars float up when they finish and get up (TabFloat), which is when a
            // customer actually pays.
            string line = verdict.OrdersAgain ? "★ ANOTHER ROUND!"
                : verdict.Match == OrderMatch.Exact ? "PERFECT!"
                : verdict.Match == OrderMatch.Close ? "THANKS."
                : "NOT WHAT I ASKED";

            var text = NewText("React", seat.parent, _display, 14, TextAnchor.LowerCenter, tone);
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = line;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(178, 60);
            var start = seat.anchoredPosition + new Vector2(-89f, 118f);   // centred over the seat

            const float duration = 1.35f;
            float tt = 0f;
            while (tt < duration && text != null)
            {
                tt += Time.deltaTime;
                float k = Mathf.Clamp01(tt / duration);
                // A quick pop on the way in, then a slow rise and fade.
                float pop = 1f + 0.3f * Mathf.Clamp01(1f - k * 6f) - 0.05f * k;
                rt.localScale = new Vector3(pop, pop, 1f);
                rt.anchoredPosition = start + new Vector2(0, 58f * k);
                text.color = new Color(tone.r, tone.g, tone.b, 1f - k * k);
                yield return null;
            }
            if (text != null) Destroy(text.gameObject);
        }

        /// <summary>
        /// The bill, paid on the way out (2026-07-31): what the whole visit came to — every
        /// round of it — and the stars this customer leaves behind. Fired by the departure
        /// hook, which is the same moment Core settles the tab into the till.
        /// </summary>
        private System.Collections.IEnumerator TabFloat(int seatIndex, CustomerVisit visit)
        {
            var seat = _seats[seatIndex].Root;
            string amber = ColorUtility.ToHtmlStringRGB(UITheme.Amber[3]);
            string lime = ColorUtility.ToHtmlStringRGB(UITheme.Lime[3]);
            int tip = visit.Paid - visit.PaidBase;
            var body = new StringBuilder();
            body.Append($"<color=#{amber}>+${visit.PaidBase}</color>");
            if (tip > 0) body.Append($"  <color=#{lime}>+${tip} tip</color>");
            int stars = Mathf.Clamp(Mathf.RoundToInt((float)visit.Satisfaction * 5f), 1, 5);
            body.Append($"\n{Stars(stars)}");

            var text = NewText("Tab", seat.parent, _display, 14, TextAnchor.LowerCenter, UITheme.Amber[4]);
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = body.ToString();
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(178, 48);
            var start = seat.anchoredPosition + new Vector2(-89f, 96f);

            const float duration = 1.6f;
            float tt = 0f;
            var tone = UITheme.Amber[4];
            while (tt < duration && text != null)
            {
                tt += Time.deltaTime;
                float k = Mathf.Clamp01(tt / duration);
                rt.anchoredPosition = start + new Vector2(0, 64f * k);
                text.color = new Color(tone.r, tone.g, tone.b, 1f - k * k);
                yield return null;
            }
            if (text != null) Destroy(text.gameObject);
        }

        /// <summary>A short notice under the top bar — refusals, mostly (GDD 24 §7).</summary>
        private void Toast(string message)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toastUntil = Time.unscaledTime + 1.6f;
            _toast.gameObject.SetActive(true);
        }

        /// <summary>Whether the cursor is over the bin's mouth (v5 P13 / C7).</summary>
        private bool IsOverBin(UnityEngine.InputSystem.Mouse mouse)
        {
            if (_binProp == null || mouse == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(
                _binProp, mouse.position.ReadValue(), null);
        }

        // ── refresh ─────────────────────────────────────────────────────────────

        /// <summary>Pushes the bought ambience upgrades onto the scene (GDD 24 §6).</summary>
        private void ApplyBarLook()
        {
            var run = Run;
            if (run == null) return;
            if (stage != null)
                stage.ApplyBarLook(1 + Mathf.Min(2, run.GlassUpgradeSteps / 4),
                    run.CounterTier, run.WallTier, run.HasMusician);
            RefreshGlassRack(run);
        }

        /// <summary>The under-counter glass rack (the author, 2026-08-02): every glass line
        /// the bar owns, standing on a walnut strip at its CURRENT tier — buy a step and
        /// the glass on the shelf is the finer one. Sits left of the bin, clear of MENU.</summary>
        // Where the glasses stand: the bar-front's own COMPARTMENTS (the author): five
        // cells across the painted front, three left of the MENU key, two right of it,
        // clear of the bin in the corner.
        private static readonly float[] GlassRackSlots = { -600f, -480f, -360f, 360f, 480f };

        private void RefreshGlassRack(TycoonRun run)
        {
            if (_hudRoot == null || run.Glassware.Count == 0) return;
            if (_glassRack == null)
            {
                _glassRack = NewRect("GlassRack", _hudRoot);
                _glassRack.anchorMin = _glassRack.anchorMax = new Vector2(0.5f, 0);
                _glassRack.pivot = new Vector2(0.5f, 0);
                _glassRack.sizeDelta = new Vector2(1280, 110);
                _glassRack.anchoredPosition = new Vector2(0, 4);
            }
            foreach (Transform c in _glassRack) Destroy(c.gameObject);

            int i = 0;
            foreach (var g in run.Glassware)
            {
                int tier = run.GlassTier(g.Id);
                var piece = GlassArt.For(g, tier);
                float x = GlassRackSlots[Mathf.Min(i, GlassRackSlots.Length - 1)];

                var shadow = NewRect($"S_{g.Id}", _glassRack);
                shadow.anchorMin = shadow.anchorMax = new Vector2(0.5f, 0);
                shadow.pivot = new Vector2(0.5f, 0.5f);
                shadow.sizeDelta = new Vector2(64, 12);
                shadow.anchoredPosition = new Vector2(x, 8);
                var shImg = shadow.gameObject.AddComponent<Image>();
                shImg.sprite = BackBarArt.BottleShadow();
                shImg.raycastTarget = false;

                var rt = NewRect($"G_{g.Id}", _glassRack);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                rt.pivot = new Vector2(0.5f, 0);
                float h = 92f;
                rt.sizeDelta = new Vector2(h * piece.Aspect, h);
                rt.anchoredPosition = new Vector2(x, 8);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = piece.Sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                if (tier > 1)
                {
                    var stars = NewText("T", rt, _body, 8, TextAnchor.LowerCenter, UITheme.Cyan[4]);
                    Place(stars.rectTransform, new Vector2(0.5f, 0), new Vector2(40, 10), new Vector2(0, -2));
                    stars.text = new string('★', tier - 1);
                }
                i++;
            }
        }

        private void RefreshTopBar()
        {
            var run = Run;
            // The clock, not a quota (v5 P12 / C5): a shift from 18:00 to 02:00. The day
            // number survives underneath — rent, the ledger and the strike count all still
            // count days — it simply stops being what the player reads the night by.
            double hour = run.Floor.ClockHour;
            int hh = (int)hour % 24, mm = (int)((hour - (int)hour) * 60);
            // Kept short on purpose: the clock string is longer than the old "DAY 1" and ran
            // straight into the till at the money's fixed x. Who is in is on the seat row.
            _dayText.text = run.Floor.IsClosingTime
                ? $"{hh:00}:{mm / 5 * 5:00}  ·  LAST CALL"
                : $"{hh:00}:{mm / 5 * 5:00}  ·  {CalendarFor(run.Day)}";
            _moneyText.text = $"${run.Money}";
            _moneyText.color = run.Money < 0 ? UITheme.ViceRed[3] : UITheme.Money;
            if (stage != null) stage.SetMoney($"${run.Money}");
            _crowdText.text = run.CrowdToday == WealthTier.HighRoller ? "HIGH ROLLERS"
                : run.CrowdToday == WealthTier.Broke ? "BROKE CROWD" : "REGULARS";

            // The standing, as a number and as a row of stars. A half-lit star is a real
            // half: the average is continuous, and rounding it to whole stars would hide
            // exactly the movement the player is trying to cause.
            double stars = run.Rating.Average;
            _ratingText.text = stars.ToString("0.0");
            // A 1.3 is 1.3 stars of amber: the mask's width IS the rating (the author).
            _starsFill.sizeDelta = new Vector2((float)(stars / 5.0) * _ratingStars.Length * 20f, 0);
        }

        private void RefreshSeats()
        {
            var run = Run;
            var seated = run.Floor.Seated;

            // A patron whose patience ran out storms off (GDD 24 §4) — a loud red notice, so a
            // walk-out never passes unnoticed.
            int stormed = 0;
            foreach (var v in run.Floor.Finished) if (v.State == VisitState.StormedOff) stormed++;
            if (stormed > _lastStormedCount) Toast("A CUSTOMER STORMED OFF");
            _lastStormedCount = stormed;

            // The licence is only good while its holder is at the bar.
            if (_idVisit != null && (_idVisit.State != VisitState.Waiting || !seated.Contains(_idVisit)))
                CloseId();

            bool drinkReady = run.Phase == TycoonPhase.DayOpen && run.DrinkReady &&
                (_flow == null || !_flow.IsOpen);

            // Stools are stable (2026-07-22): a customer keeps their seat until they leave, so
            // busts never shift or morph when the queue compacts. Reconcile the positional
            // Seated list against the fixed stools each frame.
            // 1) Departures — a stool whose patron is no longer seated starts a leave animation.
            for (int i = 0; i < _seats.Count; i++)
            {
                var v = _seats[i];
                if (v.Visit != null && !v.Exiting && !seated.Contains(v.Visit))
                {
                    v.Exiting = true;
                    v.ExitT = 0f;
                    v.ExitStorm = v.Visit.State == VisitState.StormedOff;
                    if (v.ExitStorm)
                        LogService($"<color=#F27D8A>STORM-OFF</color> " +
                            (v.Visit.IdInspected ? v.Visit.Order.Wanted.Name.ToUpperInvariant() : "?") +
                            " · patience ran out · $0 · " + LogStars(0));
                    else if (v.Visit.Paid > 0)
                        LogService($"<color=#F5C97B>TAB</color> settled ${v.Visit.Paid}" +
                            (v.Visit.SnacksTaken > 0 ? $" (+{v.Visit.SnacksTaken} snack)" : "") +
                            $" · leaves {LogStars(v.Visit.Satisfaction)}");
                    // The bussing beat (D2): a drinker leaves the empty glass on this stool.
                    // Core created the DirtyGlass in the same tick that freed the seat; this
                    // view claims the first one no other stool has claimed.
                    if (v.Visit.Served != null)
                        foreach (var g in run.Floor.Dirty)
                        {
                            bool claimed = false;
                            foreach (var other in _seats) if (other.Dirty == g) { claimed = true; break; }
                            if (!claimed) { v.Dirty = g; break; }
                        }
                    // The tab settles as they go: what they paid and the stars they leave
                    // behind float over the emptying stool. The serve only earned the face.
                    if (v.Visit.Paid > 0) StartCoroutine(TabFloat(i, v.Visit));
                    if (v.Visit.Paid > 0) Sfx.Play("cash");
                    Sfx.Play(!v.ExitStorm && v.Visit.Satisfaction >= 0.55 ? "cheer_sfx" : "upset_sfx", 0.6f);
                    // And the body answers before it leaves (P15/D5): a cheer or a slump on
                    // the stool. This is where the emotional tell lives now the stat rows
                    // left the card — skipped cleanly while the clips have no frames yet.
                    v.ReactClip = !v.ExitStorm && v.Visit.Satisfaction >= 0.55
                        ? PatronClip.Cheer : PatronClip.Upset;
                    v.ReactLeft = _patron.TryGetValue(v.ReactClip, out var rf) && rf.Length > 0
                        ? ReactSeconds : 0f;
                }
            }
            // 2) Arrivals — a seated customer with no stool takes the first free one and walks in.
            foreach (var visit in seated)
            {
                bool assigned = false;
                for (int i = 0; i < _seats.Count; i++) if (_seats[i].Visit == visit) { assigned = true; break; }
                if (assigned) continue;
                for (int i = 0; i < run.Seats && i < _seats.Count; i++)
                {
                    var v = _seats[i];
                    if (v.Visit == null && !v.Exiting)
                    {
                        v.Visit = visit;
                        v.WalkT = 0f;
                        v.Root.gameObject.SetActive(true);
                        Sfx.Play("door", 0.5f);   // someone through the door (P17)
                        break;
                    }
                }
            }

            RefreshSnackRow(run);
            RefreshDirtyGlasses(run);
            // The bar bed (P17): always on, muffled while a stage or the licence is open.
            Sfx.Ambience(ducked: (_flow != null && _flow.IsOpen) ||
                                 (_idRoot != null && _idRoot.gameObject.activeSelf));

            // 3) Render each stool from its assigned patron.
            for (int i = 0; i < _seats.Count; i++)
            {
                var view = _seats[i];

                if (view.Exiting) { AdvanceExit(view); continue; }

                if (view.Visit == null)
                {
                    if (view.Root.gameObject.activeSelf) view.Root.gameObject.SetActive(false);
                    continue;
                }

                AdvanceWalkIn(view);

                var visit = view.Visit;
                bool deciding = !visit.HasOrdered;                    // reading the menu (2026-07-23)
                bool drinking = visit.State == VisitState.Drinking;   // served, nursing the drink

                // The bubble only knows what the PLAYER knows (v5 C3): until the ID card has
                // been read, Core refuses to hand the order over at all. Stripped to three
                // beats (the author, 2026-08-02): it does not exist until they SIT and have
                // an order to give; unread it says only that they are ready — not who they
                // are, not what they want; read, it says only the name and the order.
                bool known = visit.IdInspected;
                bool atTheStool = view.WalkT >= 1f;
                bool showBubble = atTheStool && (!deciding || drinking);
                if (view.Tag.gameObject.activeSelf != showBubble)
                    view.Tag.gameObject.SetActive(showBubble);

                if (showBubble)
                {
                    // A regular ordering again after a perfect serve gets a gold star and the
                    // round count (GDD 24 §4) — the reward for reading them right, made
                    // visible. The name is part of what the card teaches: it waits for the read.
                    string star = visit.ExtraOrdersTaken > 0
                        ? $"<color=#F5C97B>★{visit.ExtraOrdersTaken + 1} </color>" : "";
                    view.Name.supportRichText = true;
                    view.Name.text = known
                        ? star + (visit.Regular?.Name ?? "Customer").ToUpperInvariant() : "";
                    view.Name.color = UITheme.TextPrimary;
                    view.Order.color = UITheme.Amber[4];

                    if (view.Icon != null)
                    {
                        view.Icon.sprite = !known ? null
                            : DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
                        view.Icon.enabled = view.Icon.sprite != null;
                        view.Icon.color = drinking ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                    }

                    if (drinking)
                    {
                        // Served and content; the drink is theirs to finish before they go.
                        view.Wants.text = "ENJOYING IT";
                        view.Order.text = known
                            ? $"{visit.Order.Wanted.Name.ToUpperInvariant()}  ·" : "·";
                        view.Order.color = UITheme.Lime[3];
                    }
                    else if (!known)
                    {
                        // Ready, unread: the one line the author asked for, and nothing else.
                        view.Wants.text = "READY TO ORDER";
                        view.Order.text = "";
                    }
                    else
                    {
                        // Read: the name above, the order below — the card said the rest.
                        view.Wants.text = "";
                        view.Order.text = visit.Order.Wanted.Name.ToUpperInvariant();
                    }

                    // The ticket FITS its lines (the author, 2026-08-02: one line in the old
                    // three-line card floated in a big empty box). Empty rows give their
                    // space back, the card takes the height of what it actually says.
                    float rowTop = -8f;
                    view.Name.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Name.text.Length > 0) rowTop -= 16f;
                    view.Wants.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Wants.text.Length > 0) rowTop -= 16f;
                    view.Order.rectTransform.offsetMax = new Vector2(-4, rowTop);
                    if (view.Order.text.Length > 0) rowTop -= 17f;
                    view.Tag.sizeDelta = new Vector2(BustW + 48f, -rowTop + 8f);
                }

                // The icon docks against the text's measured width, so the centred line reads
                // as one piece: [glass] DRAUGHT, the pair centred together — on whatever row
                // the order landed on now that the ticket packs its lines.
                if (view.Icon != null && view.Icon.enabled)
                    view.Icon.rectTransform.anchoredPosition = new Vector2(
                        -view.Order.preferredWidth * 0.5f - 4f,
                        view.Order.rectTransform.offsetMax.y - 6f);

                // The patience clock only bites while they wait on an order. Deciding holds it
                // full; a drinking customer is content — both show a calm, full cyan bar.
                float patience = (deciding || drinking) ? 1f
                    : (float)(visit.PatienceLeft / visit.PatienceMax);
                float gaugeW = BustW * 0.72f - 2f;
                view.PatienceFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(gaugeW * patience), -2);
                view.PatienceFill.color = (deciding || drinking) ? UITheme.Cyan[3]
                    : patience > 0.5f ? UITheme.Lime[3]
                    : patience > 0.25f ? UITheme.Amber[3] : UITheme.ViceRed[3];

                // Drive the animated customer (2026-07-23): walk-in, the sit-and-breathe idle,
                // a one-shot "placing the order" beat, then nursing the drink. Facing and frame
                // are chosen from the visit state; the body below the waist is clipped by the bar.
                UpdateSeatAnimation(view, visit, patience);

                // The tag glows cyan when a drink is built and this customer can actually take it.
                bool canTake = drinkReady && !deciding && !drinking;
                view.TagBg.color = canTake
                    ? new Color(UITheme.Selection.r, UITheme.Selection.g, UITheme.Selection.b, 0.92f)
                    : new Color(0.07f, 0.07f, 0.11f, 0.90f);
            }
        }

        /// <summary>Walks a newly-seated customer in from the right along the counter and fades
        /// them up (2026-07-23): the west-facing walk cycle carries them left into their stool.</summary>
        private void AdvanceWalkIn(SeatView view)
        {
            // In from the screen's real right edge (not a few frames off the stool) at a steady
            // pace, so a far stool is a longer walk (2026-07-23).
            float entryX = _hudRoot.rect.width + OffscreenMargin;
            if (view.WalkT < 1f)
            {
                float dist = Mathf.Max(1f, entryX - view.SeatX);
                view.WalkT = Mathf.Min(1f, view.WalkT + Time.deltaTime * WalkSpeed / dist);
                // CONSTANT speed, no ease (P15, the notes): the old ease-out meant the ground
                // slid fast under slow feet at the door and slow under fast feet at the stool —
                // the walk cycle only reads as walking when the floor moves at one rate.
                view.Root.anchoredPosition =
                    new Vector2(Mathf.Lerp(entryX, view.SeatX, view.WalkT), CounterLineY);
                view.Group.alpha = Mathf.Clamp01(view.WalkT * 4f);
            }
            else
            {
                view.Root.anchoredPosition = new Vector2(view.SeatX, CounterLineY);
                view.Group.alpha = 1f;
            }
        }

        /// <summary>Plays a customer leaving (2026-07-23): they get up and walk back out to the
        /// right the way they came (the walk cycle, mirrored). A stormed-off patron shakes first,
        /// then storms out faster.</summary>
        private void AdvanceExit(SeatView view)
        {
            // The reaction beat first: they stay on the stool and the drink answers — a fist
            // up or a slow head-shake — before they get up. One shot, then the walk.
            if (view.ReactLeft > 0f)
            {
                view.ReactLeft -= Time.deltaTime;
                UpdatePatronFrame(view, view.ReactClip, ReactSeconds - view.ReactLeft, facing: 1);
                return;
            }

            // Get up and walk all the way back off the right edge the way they came.
            float exitX = _hudRoot.rect.width + OffscreenMargin;
            float dist = Mathf.Max(1f, exitX - view.SeatX);
            float speed = view.ExitStorm ? ExitSpeed * 1.5f : ExitSpeed;
            view.ExitT = Mathf.Min(1f, view.ExitT + Time.deltaTime * speed / dist);
            float k = view.ExitT;

            float shake = view.ExitStorm && k < 0.25f ? Mathf.Sin(k * 90f) * 8f : 0f;
            float e = k * k;   // ease-in: rises and steps away, gathering pace
            view.Root.anchoredPosition = new Vector2(
                view.SeatX + shake + (exitX - view.SeatX) * e, CounterLineY);
            view.Group.alpha = 1f - Mathf.Clamp01((k - 0.5f) / 0.5f);

            // Mirror the walk so they face the way they are leaving (to the right).
            UpdatePatronFrame(view, PatronClip.Walk, view.AnimClock, facing: -1);
            view.AnimClock += Time.deltaTime;

            if (view.ExitT >= 1f)
            {
                view.Exiting = false;
                view.Visit = null;
                view.Group.alpha = 1f;
                view.Portrait.rectTransform.localScale = new Vector3(CharWiden, 1f, 1f);   // reset the mirror
                view.Root.gameObject.SetActive(false);
            }
        }

        // ── the animated customer (2026-07-23) ───────────────────────────────────

        /// <summary>Chooses the clip and frame for a seated customer from their state and drives
        /// the character image: the sit-and-breathe idle while they wait, a one-shot "placing the
        /// order" beat the moment they decide, and the drink once served — plus a light impatience
        /// flush over the last of their patience.</summary>
        private void UpdateSeatAnimation(SeatView view, CustomerVisit visit, float patience)
        {
            bool ordered = visit.HasOrdered;
            bool seated = view.WalkT >= 1f;
            bool drinking = visit.State == VisitState.Drinking;

            if (ordered && !view.WasOrdered && seated) view.OrderAnimLeft = OrderAnimSeconds;
            view.WasOrdered = ordered;

            if (drinking) view.DrinkT += Time.deltaTime; else view.DrinkT = 0f;

            PatronClip clip; float t;
            if (!seated)                      { clip = PatronClip.Walk;  t = view.AnimClock; }   // faces left, walking in
            else if (drinking)                { clip = PatronClip.Drink; t = view.DrinkT; }
            else if (view.OrderAnimLeft > 0f) { clip = PatronClip.Order; t = OrderAnimSeconds - view.OrderAnimLeft;
                                                view.OrderAnimLeft -= Time.deltaTime; }
            else                              { clip = PatronClip.Idle;  t = view.AnimClock; }
            view.AnimClock += Time.deltaTime;

            UpdatePatronFrame(view, clip, t, facing: 1);

            float flush = (!ordered || drinking) ? 1f : Mathf.Clamp01(patience / 0.35f);
            view.Portrait.color = Color.Lerp(new Color(1f, 0.72f, 0.72f, 1f), Color.white, flush);
        }

        /// <summary>Sets the character image to the right frame of <paramref name="clip"/> at time
        /// <paramref name="t"/>, mirrored when <paramref name="facing"/> is -1 (leaving right).</summary>
        private void UpdatePatronFrame(SeatView view, PatronClip clip, float t, int facing)
        {
            if (_patron == null || !_patron.TryGetValue(clip, out var frames) || frames.Length == 0) return;
            view.Portrait.sprite = frames[PatronFrameIndex(clip, t, frames.Length)];
            // A touch wider than tall (CharWiden), mirrored to face right on the way out.
            view.Portrait.rectTransform.localScale = new Vector3(CharWiden * (facing < 0 ? -1f : 1f), 1f, 1f);
        }

        /// <summary>The frame index for a clip at time t. Most clips loop at a fixed rate; the
        /// drink raises and lowers the glass over a sip window then holds it at rest, so it reads
        /// as a real sip every few seconds instead of a gulp every frame (2026-07-23).</summary>
        private static int PatronFrameIndex(PatronClip clip, float t, int n)
        {
            if (n <= 1) return 0;
            // The reactions play once, spread over the beat, and hold their last frame.
            if (clip == PatronClip.Cheer || clip == PatronClip.Upset)
                return Mathf.Min(n - 1, Mathf.FloorToInt(t / ReactSeconds * n));
            if (clip == PatronClip.Drink)
            {
                float u = Mathf.Repeat(t, DrinkSipSeconds + DrinkHoldSeconds);
                if (u >= DrinkSipSeconds) return 0;                 // holding the glass at rest
                int span = 2 * (n - 1);                             // 0..n-1..1 (raise then lower)
                int p = Mathf.FloorToInt(u / DrinkSipSeconds * span) % span;
                return p < n ? p : span - p;
            }
            // Idle is a slow two-frame breath — a settled customer barely moves; the walk
            // strides (matched to the slower gait), the order talks.
            float fps = clip == PatronClip.Walk ? 9f : clip == PatronClip.Order ? 7f : 2.5f;
            return Mathf.FloorToInt(t * fps) % n;
        }

        private void LoadPatronFrames()
        {
            _patron = new Dictionary<PatronClip, Sprite[]>
            {
                [PatronClip.Idle]  = LoadPatronClip("idle"),
                [PatronClip.Order] = LoadPatronClip("order"),
                [PatronClip.Drink] = LoadPatronClip("drink"),
                [PatronClip.Walk]  = LoadPatronClip("walk"),
                [PatronClip.Cheer] = LoadPatronClip("cheer"),
                [PatronClip.Upset] = LoadPatronClip("upset"),
            };
        }

        /// <summary>All frames of one clip from Resources/Patron/&lt;clip&gt;, ordered by name.</summary>
        private static Sprite[] LoadPatronClip(string clip)
        {
            var sprites = Resources.LoadAll<Sprite>($"Patron/{clip}");
            System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }

        // ── day end ─────────────────────────────────────────────────────────────

        private void ShowDayEnd()
        {
            var run = Run;
            _dayEndStep = 0;   // the bill first; the market only after CONTINUE
            _dayEndPanel.gameObject.SetActive(true);
            RebuildDayEnd();
        }

        private void OnDayEndAdvance()
        {
            if (_dayEndStep == 0) { _dayEndStep = 1; Sfx.Play("click", 0.6f); RebuildDayEnd(); }
            else OnOpenTomorrow();
        }

        private void RebuildDayEnd()
        {
            var run = Run;
            _dayEndBill.gameObject.SetActive(_dayEndStep == 0);
            _dayEndTablet.gameObject.SetActive(_dayEndStep == 1);
            _dayEndTitle.text = _dayEndStep == 0 ? "LAST CALL — THE BOOKS" : "LAST CALL — THE MARKET";
            _openTomorrowLabel.text = _dayEndStep == 0 ? "CONTINUE  →  THE MARKET"
                : run.Day % 6 == 0 ? "START TUESDAY  →   (MONDAY IS DARK)"
                : "START THE DAY  →";
            var floor = run.Floor;
            int served = 0, stormed = 0;
            foreach (var visit in floor.Finished)
                if (visit.State == VisitState.StormedOff) stormed++; else served++;
            var cfg = run.Config;

            // The bill: income over expenses, net in bold, then the debt stamp. All the
            // day's line items come straight off the run's itemised book (GDD 24 §7).
            int net = run.DayIncome - run.DayExpenses;
            string netColour = net >= 0 ? "2A5926" : "A62B44";
            string stamp = run.Ledger.DebtStrikes == 0 ? ""
                : $"\n\n<color=#A62B44>◆ IN THE RED — STRIKE {run.Ledger.DebtStrikes}/{DayLedger.StrikesToClose} ◆</color>";
            if (run.Ledger.DebtStrikes == DayLedger.StrikesToClose - 1)
                stamp += "\n<color=#A62B44>one more red day closes the bar</color>";

            // Receipt v2 (v5 P13): a till slip, not a summary panel. Header, then the drinks
            // that actually crossed the bar as line items, then the totals block. The lines are
            // taken from what was POURED (`visit.Served`) and priced at `PaidBase`, so a night
            // where the player misread somebody still adds up — a wrong drink is paid at its
            // own price, and listing menu prices instead would leave the bill short.
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{Rule}</b>");
            sb.AppendLine($"<b>   LAST CALL   </b>");
            sb.AppendLine($"   {CalendarFor(run.Day)} · {CrowdName(run.CrowdToday)}");
            sb.AppendLine($"<b>{Rule}</b>");

            var lines = new List<string>();
            var counts = new Dictionary<string, int>();
            var totals = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var visit in floor.Finished)
            {
                if (visit.Served == null || visit.PaidBase <= 0) continue;
                string name = visit.Served.Name.ToUpperInvariant();
                if (!counts.ContainsKey(name)) { counts[name] = 0; totals[name] = 0; order.Add(name); }
                counts[name]++;
                totals[name] += visit.PaidBase;
            }
            foreach (var name in order)
                lines.Add(Line($"{counts[name]}x {name}", $"${totals[name]}", null));
            if (lines.Count == 0)
                sb.AppendLine("<color=#9C8F80>   nothing sold</color>");
            else
                foreach (var line in lines) sb.AppendLine(line);

            sb.AppendLine($"<color=#9C8F80>{Rule}</color>");
            sb.AppendLine(Line("SALES", $"${run.DaySales}", null));
            sb.AppendLine(Line("TIPS", $"${run.DayTips}", null));
            sb.AppendLine(Line("RENT", $"-${run.DayRent}", "A62B44"));
            sb.AppendLine(Line("STOCK", $"-${run.DayStock}", "A62B44"));
            sb.AppendLine(Line("SHOP", $"-${run.DayUpgrades}", "A62B44"));
            sb.AppendLine($"<color=#9C8F80>{Rule}</color>");
            sb.AppendLine("<b>" + Line("NET", $"{(net >= 0 ? "+" : "-")}${Math.Abs(net)}", netColour) + "</b>");
            sb.AppendLine("<b>" + Line("TILL", $"${run.Money}", null) + "</b>");
            sb.AppendLine($"<color=#9C8F80>{Rule}</color>");

            // The footer a real slip carries: who came, how they left, and the standing they
            // left behind — the number tomorrow's crowd is actually drawn from.
            sb.AppendLine($"<color=#9C8F80>   {served} served · {stormed} walked</color>");
            sb.AppendLine($"<color=#9C8F80>   tonight {BarRating.ExactStarsFor(floor.AverageSatisfaction):0.0}* " +
                          $"· bar {run.Rating.Average:0.0}*</color>");
            sb.Append(stamp);
            _invoiceText.text = sb.ToString();

            // The tablet.
            foreach (Transform child in _offerRow) Destroy(child.gameObject);
            _tabletTill.text = $"${run.Money}";
            for (int i = 0; i < _shopTabKeys.Length; i++)
            {
                bool on = i == _shopTab;
                _shopTabKeys[i].color = on ? UITheme.PrimaryAction : UITheme.Night[2];
                _shopTabLabels[i].color = on ? UITheme.TextOnAmber : UITheme.TextSecondary;
            }

            if (_dayEndStep == 0) return;   // the bill step shows no shop at all
            if (_marketTip != null) _marketTip.text = "";
            if (_shopTab == 0)
            {
                // RESTOCK (the author): the whole well at once, or bottle by bottle.
                _cardTarget = ShopSection("EVERYTHING AT ONCE");
                int restock = run.Shelf.RefillCost(cfg.RefillPricePerCapacity);
                AddCard("RESTOCK THE WELL", "well is full", restock, restock > 0, () =>
                {
                    run.RefillShelf(); RebuildDayEnd();
                });
                _cardTarget = ShopSection("BOTTLE BY BOTTLE");
                foreach (var b in run.Shelf.Bottles)
                {
                    var bottle = b;
                    int cost = (int)Math.Ceiling((bottle.Capacity - bottle.Remaining)
                        * cfg.RefillPricePerCapacity);
                    AddCard(bottle.Ingredient.Name.ToUpperInvariant(), "full", cost, cost > 0,
                        () => { run.RefillBottle(bottle.Ingredient.Id); RebuildDayEnd(); },
                        ItemArt.Bottle(bottle.Ingredient.Info?.Style));
                }
            }
            else if (_shopTab == 1)
            {
                _cardTarget = ShopSection("TONIGHT'S MARKET");
                for (int i = 0; i < run.MarketOffers.Count; i++)
                {
                    int index = i;
                    var offer = run.MarketOffers[i];
                    string title = (offer.IsNewStock ? "+ " : "↑ ") + offer.Bottle.Name.ToUpperInvariant();
                    AddCard(title, "bought", offer.Price, !offer.Sold, () =>
                    {
                        run.BuyBrand(index);
                        RebuildDayEnd();
                    }, ItemArt.Bottle(offer.Bottle.Info?.Style));
                }
            }
            else if (_shopTab == 2)
            {
                // RECIPES on their own tab (the author). A recipe the stars cannot buy yet
                // is SEALED — the player is not told what waits behind the wax — and an
                // affordable one answers a hover with how it is actually made.
                _cardTarget = ShopSection("THE RECIPE BOOK");
                foreach (var recipe in run.LockedRecipes)
                {
                    var r = recipe;
                    double gate = run.RecipeStarGate(r);
                    if (run.Rating.Average < gate)
                    {
                        AddCard($"✦ SEALED · NEEDS {gate:0.0}★", "sealed", 0, false, null);
                        continue;
                    }
                    var card = AddCard($"✦ {r.Name.ToUpperInvariant()}", "gated",
                        run.RecipePrice(r), true, () =>
                    {
                        try { run.UnlockRecipe(r.Id); Toast($"{r.Name.ToUpperInvariant()} — ON THE MENU TOMORROW"); }
                        catch (InvalidOperationException e) { Toast(e.Message.ToUpperInvariant()); }
                        RebuildDayEnd();
                    }, DrinkIcon.For(r, _bootstrap.Glassware));
                    HoverRecipe(card, r);
                }
            }
            else
            {
                // UPGRADES, pruned to the two that provably work (the author, 2026-08-02):
                // stools and the glassware line. Musician, counter and back bar retired.
                _cardTarget = ShopSection("THE FLOOR");
                AddCard($"STOOL #{run.Seats + 1}", "bar is full", cfg.SeatPrice(run.Seats),
                    run.Seats < cfg.MaxSeats, () => { run.BuySeat(); ApplyBarLook(); RebuildDayEnd(); });
                _cardTarget = ShopSection("GLASSWARE — EVERY LINE ITS OWN, TO LEGENDARY");
                foreach (var g in run.Glassware)
                {
                    var glass = g;
                    int tier = run.GlassTier(glass.Id);
                    bool maxed = tier >= TycoonRun.MaxGlassTier;
                    int stepPrice = maxed ? 0 : glass.TierPrices[tier - 1];
                    // Tier is 1-based; the label speaks STARS (0★ base … 5★ legendary).
                    string label = maxed
                        ? $"{glass.Name.ToUpperInvariant()} — LEGENDARY"
                        : $"{glass.Name.ToUpperInvariant()} {tier - 1}★ → {tier}★";
                    // The card previews the glass being SOLD — the next tier's dress —
                    // not the one already on the rack.
                    AddCard(label, "legendary", stepPrice, !maxed,
                        () => { run.BuyGlassTier(glass.Id); ApplyBarLook(); RebuildDayEnd(); },
                        GlassArt.For(glass, Mathf.Min(tier + 1, TycoonRun.MaxGlassTier)).Sprite);
                }
            }

            // The refund slip rides every tab: anything bought at THIS close can go back.
            if (run.TodaysPurchases.Count > 0)
            {
                _cardTarget = ShopSection("BOUGHT TONIGHT — CLICK TO REFUND");
                for (int i = 0; i < run.TodaysPurchases.Count; i++)
                {
                    int idx = i;
                    var pch = run.TodaysPurchases[i];
                    AddCard($"⟲ {pch.Name.ToUpperInvariant()}", "", pch.Price, true,
                        () => { run.RefundToday(idx); Toast("REFUNDED"); ApplyBarLook(); RebuildDayEnd(); });
                }
            }
            _offerRow.anchoredPosition = Vector2.zero;   // a rebuilt shop reads from the top
        }

        /// <summary>Points the market's hover line at a recipe: prep and pour, the book's way.</summary>
        private void HoverRecipe(RectTransform card, RecipeDefinition r)
        {
            if (card == null || _marketTip == null) return;
            string prep = r.Id == "draught" ? "ON TAP" : r.Id == "neat_pour" ? "NEAT"
                : r.Prep == PrepMethod.Shaken ? "SHAKEN"
                : r.Prep == PrepMethod.Stirred ? "STIRRED" : "BUILT";
            var trig = card.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
                _marketTip.text = $"<color=#7FD4C9>{prep}</color>  ·  {BandLine(r)}");
            trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => _marketTip.text = "");
            trig.triggers.Add(exit);
        }

        /// <summary>A titled section of the market: its header row, then its own grid.</summary>
        private RectTransform ShopSection(string title)
        {
            var h = NewRect("SH", _offerRow);
            h.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            var t = NewText("T", h, _body, 12, TextAnchor.LowerLeft, UITheme.TextSecondary);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 2), Vector2.zero);
            t.text = $"— {title} —";
            var sec = NewRect("Sec", _offerRow);
            var g = sec.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(162, 74);
            g.spacing = new Vector2(8, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 5;
            return sec;
        }

        // ── the service log (dev tooling, 2026-07-31) ────────────────────────────
        // Top-left, always on for now: one line per service event — what was ordered, what
        // the judge said and WHY it scored the way it did (match, spec, fill, craft), what
        // it paid, and the stars it left. The author's in-play instrument for the balance
        // work: the sim report says what 200 runs did; this says what THIS serve just did.

        private Text _serviceLog;
        private readonly List<string> _serviceLogLines = new List<string>();
        private const int ServiceLogMax = 9;

        private void BuildServiceLog(RectTransform root)
        {
            var panel = NewRect("ServiceLog", root);
            Place(panel, new Vector2(0, 1), new Vector2(430, 150), new Vector2(10, -66));
            panel.pivot = new Vector2(0, 1);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.55f);
            bg.raycastTarget = false;
            _serviceLog = NewText("Lines", panel, _body, 8, TextAnchor.UpperLeft, UITheme.TextSecondary);
            _serviceLog.supportRichText = true;
            Stretch(_serviceLog.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 4), new Vector2(-6, -4));
        }

        private void LogService(string line)
        {
            if (_serviceLog == null) return;
            _serviceLogLines.Insert(0, line);
            while (_serviceLogLines.Count > ServiceLogMax)
                _serviceLogLines.RemoveAt(_serviceLogLines.Count - 1);
            _serviceLog.text = string.Join("\n", _serviceLogLines);
        }

        private static string LogStars(double satisfaction) =>
            new string('★', Mathf.Clamp(Mathf.RoundToInt((float)satisfaction * 5f), 0, 5));

        /// <summary>The judge's verdict, said as one log line with its reasons.</summary>
        private void LogVerdict(CustomerVisit visit, ServiceVerdict verdict)
        {
            string ordered = visit.IdInspected ? visit.Order.Wanted.Name.ToUpperInvariant() : "?";
            string made = visit.Served != null ? visit.Served.Name.ToUpperInvariant() : "NOTHING NAMED";
            string col = verdict.Match == OrderMatch.Exact ? "8CE28C"
                : verdict.Match == OrderMatch.Close ? "F5C97B" : "F27D8A";
            var why = new List<string>();
            if (verdict.Match == OrderMatch.Wrong) why.Add($"made {made}");
            if (verdict.SpecScore < 0.999) why.Add($"spec {verdict.SpecScore:P0}");
            if (verdict.FillScore < 0.999) why.Add($"fill {verdict.FillScore:P0}");
            if (!verdict.CraftLanded && visit.Order != null) { }
            string reasons = why.Count > 0 ? "  <color=#9C8F80>(" + string.Join(", ", why) + ")</color>" : "";
            LogService($"<color=#{col}>{verdict.Match.ToString().ToUpperInvariant()}</color> {ordered}" +
                       $" · ${verdict.BasePaid}+${verdict.Tip} · {LogStars(verdict.Satisfaction)}{reasons}");
        }

        // ── the recipe book (v5 P16) ─────────────────────────────────────────────
        // The menu speaks styles now, so what is IN a drink has to be readable mid-shift:
        // an order for a Gimlet is unanswerable by a player who cannot look a Gimlet up.
        // Unlocked recipes print their full bands; locked ones show what tier and stars
        // they are waiting behind, so the book doubles as the progression map.

        private RectTransform _bookPanel, _bookList;
        // The filters (the author's spec): tier, prep and bottle, each a cycling chip.
        private int _bookTier = -1;              // -1 all, else 0..3
        private InputField _bookSearch;          // name substring, the author's "arama"
        private int _bookPrep = -1;              // -1 all, else (int)PrepMethod
        private string _bookStyle;               // null = all
        private Text _bookTierChip, _bookPrepChip, _bookStyleChip;
        private static readonly string[] BookTiers = { "STARTER", "MID SHELF", "TOP SHELF", "HOUSE PRIDE" };

        private static int TierIndex(int rank) => rank <= 8 ? 0 : rank <= 14 ? 1 : rank <= 21 ? 2 : 3;

        private List<string> BookStyles()
        {
            var run = Run;
            var seen = new List<string>();
            if (run == null) return seen;
            foreach (var r in run.AllRecipes)
                foreach (var b in r.RatioRequirements)
                    if (b.IsStyleBand && !seen.Contains(b.Style)) seen.Add(b.Style);
            seen.Sort(System.StringComparer.Ordinal);
            return seen;
        }

        private bool _bookOpen;
        private Coroutine _bookAnim;

        internal void ToggleRecipeBook()
        {
            if (_bookPanel == null) return;
            bool open = !_bookOpen;
            _bookOpen = open;
            Sfx.Play("click", 0.6f);
            var sheet = _bookPanel.Find("Sheet") as RectTransform;
            if (open)
            {
                if (!_bookPanel.gameObject.activeSelf)
                {
                    // First frame of the drop: board parked above the screen, scrim clear.
                    sheet.anchoredPosition = new Vector2(0, BkH);
                    var c = UITheme.Scrim;
                    _bookPanel.GetComponent<Image>().color = new Color(c.r, c.g, c.b, 0);
                }
                _bookPanel.gameObject.SetActive(true);
                _bookPanel.SetAsLastSibling();   // over the service log and everything else
                RebuildRecipeBook();
            }
            else CloseBookPopup();
            if (_bookAnim != null) StopCoroutine(_bookAnim);
            _bookAnim = StartCoroutine(BookSlide(open));
        }

        /// <summary>The board drops down from above and lifts back away (the author asked
        /// for a smooth open and close). Reduced motion snaps, as everywhere.</summary>
        private System.Collections.IEnumerator BookSlide(bool open)
        {
            var sheet = _bookPanel.Find("Sheet") as RectTransform;
            var scrim = _bookPanel.GetComponent<Image>();
            var c = UITheme.Scrim;
            float fromY = sheet.anchoredPosition.y, toY = open ? 0f : BkH;
            float fromA = scrim.color.a, toA = open ? c.a : 0f;
            if (!Motion.Reduced)
            {
                float dur = open ? 0.28f : 0.2f;
                for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
                {
                    float k = t / dur;
                    k = open ? 1f - (1f - k) * (1f - k) * (1f - k) : k * k * k;
                    sheet.anchoredPosition = new Vector2(0, Mathf.Lerp(fromY, toY, k));
                    scrim.color = new Color(c.r, c.g, c.b, Mathf.Lerp(fromA, toA, k));
                    yield return null;
                }
            }
            sheet.anchoredPosition = new Vector2(0, toY);
            scrim.color = new Color(c.r, c.g, c.b, toA);
            if (!open) _bookPanel.gameObject.SetActive(false);
            _bookAnim = null;
        }

        private void BuildRecipeBook(RectTransform root)
        {
            _bookPanel = NewRect("RecipeBook", root);
            Stretch(_bookPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _bookPanel.gameObject.AddComponent<Image>();
            scrim.color = UITheme.Scrim;
            // Only the X closes now (the author's ruling): the scrim blocks the bar behind
            // it but closes nothing. The book also outranks the back-bar flow (canvas 12):
            // its own canvas at 15 lets the BOOK key on the flow's ledge show the thing.
            var bookCanvas = _bookPanel.gameObject.AddComponent<Canvas>();
            bookCanvas.overrideSorting = true;
            bookCanvas.sortingOrder = 15;
            _bookPanel.gameObject.AddComponent<GraphicRaycaster>();

            // Promoted to THE menu (2026-08-01): recipes, search and filters live here now
            // that the clipboard is gone — so it takes the room a menu deserves.
            // The clipboard lives on in the book (2026-08-01, the author's ask): the wooden
            // board art, the paper region measured off it, the plate keys and the drawn X --
            // the old menu's whole visual language, now carrying the recipes instead of the
            // bottles. Paper constants are the flow's own measurements of menu_board.
            var sheet = NewRect("Sheet", _bookPanel);
            Place(sheet, new Vector2(0.5f, 0.5f), new Vector2(BkW, BkH), Vector2.zero);
            var boardImg = sheet.gameObject.AddComponent<Image>();
            var boardSprite = ItemArt.Load("menu_board");
            if (boardSprite != null) { boardImg.sprite = boardSprite; boardImg.preserveAspect = true; }
            else boardImg.color = UITheme.Cream[4];
            // The whole VISIBLE board swallows its clicks (the author, third ruling): the
            // sprite carries wide transparent margins, so a full-rect swallow ate the very
            // "outside" clicks meant to close the book. The catcher is sized to the board's
            // opaque pixels (measured off menu_board.png: 823x632 centred at -6,+6).
            boardImg.raycastTarget = false;
            var boardCatch = NewRect("BoardCatch", sheet);
            Place(boardCatch, new Vector2(0.5f, 0.5f), new Vector2(824, 634), new Vector2(-6, 6));
            var bcImg = boardCatch.gameObject.AddComponent<Image>();
            bcImg.color = new Color(0, 0, 0, 0.001f);
            boardCatch.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            // The X returns (the author): top-right of the board, and it is the ONLY way
            // out — outside clicks no longer close, so a stray click cannot eat the page.
            var closeRt = NewRect("Close", sheet);
            Place(closeRt, new Vector2(0.5f, 0.5f), new Vector2(56, 56), new Vector2(378f, 288f));
            var closeImg = closeRt.gameObject.AddComponent<Image>();
            var closeSprite = ItemArt.Load("btn_close");
            if (closeSprite != null) { closeImg.sprite = closeSprite; closeImg.preserveAspect = true; }
            else closeImg.color = new Color(0.62f, 0.15f, 0.17f);
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            var closeDown = ItemArt.Load("btn_close_down");
            if (closeSprite != null && closeDown != null)
            {
                closeBtn.transition = Selectable.Transition.SpriteSwap;
                var st = closeBtn.spriteState; st.pressedSprite = closeDown; st.selectedSprite = closeSprite;
                closeBtn.spriteState = st;
            }
            closeBtn.onClick.AddListener(ToggleRecipeBook);

            // The filter chips: click to cycle. Three axes the author named — the star tier,
            // how it is worked, and what bottle it contains.
            float chipY = 142f;   // just under the board's metal clip
            // One slim toolbar (the author: the filters should look professional and take
            // less room): search at the left, three compact value-pills at the right, all
            // inside the paper. A pill drops its option window under itself.
            float paperR = BkW * BkPaperCX + BkW * BkPaperW * 0.5f;
            float tierX = paperR - 282f, prepX = paperR - 184f, styleX = paperR - 76f;
            _bookTierChip = BookChip(sheet, tierX, chipY, 92f, () =>
            {
                var opts = new List<string> { "ALL", BookTiers[0], BookTiers[1], BookTiers[2], BookTiers[3] };
                OpenBookPopup(tierX, chipY, opts, pick => { _bookTier = pick - 1; RebuildRecipeBook(); });
            });
            _bookPrepChip = BookChip(sheet, prepX, chipY, 92f, () =>
            {
                var opts = new List<string> { "ALL", "BUILT", "SHAKEN", "STIRRED" };
                OpenBookPopup(prepX, chipY, opts, pick =>
                {
                    _bookPrep = pick == 0 ? -1
                        : pick == 1 ? (int)PrepMethod.Built
                        : pick == 2 ? (int)PrepMethod.Shaken : (int)PrepMethod.Stirred;
                    RebuildRecipeBook();
                });
            });
            _bookStyleChip = BookChip(sheet, styleX, chipY, 112f, () =>
            {
                var styles = BookStyles();
                var opts = new List<string> { "ALL" };
                foreach (var st in styles) opts.Add(st.Replace('_', ' ').ToUpperInvariant());
                OpenBookPopup(styleX, chipY, opts, pick =>
                {
                    _bookStyle = pick == 0 ? null : styles[pick - 1];
                    RebuildRecipeBook();
                });
            });

            // The search box: type a name, the list narrows as you do.
            var searchRt = NewRect("Search", sheet);
            Place(searchRt, new Vector2(0.5f, 0.5f), new Vector2(220, 26),
                new Vector2(BkW * BkPaperCX - BkW * BkPaperW * 0.5f + 130f, chipY));
            var searchBg = searchRt.gameObject.AddComponent<Image>();
            searchBg.color = new Color(0.94f, 0.90f, 0.80f);
            var searchText = NewText("T", searchRt, _body, 16, TextAnchor.MiddleLeft, new Color(0.16f, 0.10f, 0.06f));
            Stretch(searchText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
            searchText.supportRichText = false;
            var placeholder = NewText("P", searchRt, _body, 16, TextAnchor.MiddleLeft, new Color(0.5f, 0.42f, 0.32f));
            Stretch(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
            placeholder.text = "SEARCH…";
            _bookSearch = searchRt.gameObject.AddComponent<InputField>();
            _bookSearch.targetGraphic = searchBg;
            _bookSearch.textComponent = searchText;
            _bookSearch.placeholder = placeholder;
            _bookSearch.onValueChanged.AddListener(_ => RebuildRecipeBook());

            var viewport = NewRect("View", sheet);
            Place(viewport, new Vector2(0.5f, 0.5f),
                new Vector2(BkW * BkPaperW - 44f, 358f),
                new Vector2(BkW * BkPaperCX, -56f));
            viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.004f);
            viewport.gameObject.AddComponent<RectMask2D>();

            _bookList = NewRect("List", viewport);
            _bookList.anchorMin = new Vector2(0, 1); _bookList.anchorMax = new Vector2(1, 1);
            _bookList.pivot = new Vector2(0.5f, 1);
            _bookList.offsetMin = Vector2.zero; _bookList.offsetMax = Vector2.zero;
            // Sections (2026-08-01, the author: the groupings must read clearly): the list
            // is a stack of tier sections, each a full-width header over its own grid, so a
            // header can never land inside a column and the columns always align.
            var layout = _bookList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var fitter = _bookList.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = _bookList;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            _bookPanel.gameObject.SetActive(false);
        }

        // The board and its paper, as the flow measured them off menu_board.
        private const float BkW = 1148f, BkH = 719f;
        private const float BkPaperW = 0.655f, BkPaperH = 0.660f;
        private const float BkPaperCX = -0.015f, BkPaperCY = -0.008f;

        private Text BookChip(RectTransform sheet, float x, float y, float w, Action onClick)
        {
            // A compact value-pill: cream face, hairline ink frame, the value in small ink.
            var chip = NewRect("Chip", sheet);
            Place(chip, new Vector2(0.5f, 0.5f), new Vector2(w, 24), new Vector2(x, y));
            var img = chip.gameObject.AddComponent<Image>();
            img.color = new Color(0.95f, 0.92f, 0.82f, 0.95f);
            var btn = chip.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => { Sfx.Play("click", 0.5f); onClick(); });
            var frame = new Color(0.30f, 0.20f, 0.10f, 0.5f);
            Hairline(chip, new Vector2(0, 0), new Vector2(1, 0), frame);
            Hairline(chip, new Vector2(0, 1), new Vector2(1, 1), frame);
            HairlineV(chip, 0f, frame);
            HairlineV(chip, 1f, frame);
            var t = NewText("T", chip, _body, 8, TextAnchor.MiddleCenter, new Color(0.30f, 0.20f, 0.10f));
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return t;
        }

        private void Hairline(RectTransform parent, Vector2 aMin, Vector2 aMax, Color c)
        {
            var r = NewRect("HL", parent);
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.pivot = new Vector2(0.5f, aMin.y);
            r.sizeDelta = new Vector2(0, 1);
            r.anchoredPosition = Vector2.zero;
            var i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        }

        private void HairlineV(RectTransform parent, float ax, Color c)
        {
            var r = NewRect("VL", parent);
            r.anchorMin = new Vector2(ax, 0); r.anchorMax = new Vector2(ax, 1);
            r.pivot = new Vector2(ax, 0.5f);
            r.sizeDelta = new Vector2(1, 0);
            r.anchoredPosition = Vector2.zero;
            var i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        }

        private RectTransform _bookPopup;

        /// <summary>A little paper window of options under a chip. A full-screen invisible
        /// catcher behind it closes it on any other click.</summary>
        private void OpenBookPopup(float anchorX, float chipY, List<string> options, Action<int> onPick)
        {
            CloseBookPopup();
            var sheet = _bookPanel.Find("Sheet") as RectTransform;
            _bookPopup = NewRect("Popup", sheet);
            var catcher = _bookPopup.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0.001f);
            Stretch(_bookPopup, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var catchBtn = _bookPopup.gameObject.AddComponent<Button>();
            catchBtn.transition = Selectable.Transition.None;
            catchBtn.onClick.AddListener(CloseBookPopup);

            int cols = options.Count > 8 ? 2 : 1;
            int rows = Mathf.CeilToInt(options.Count / (float)cols);
            float w = cols * 190f + 8f, h = rows * 28f + 10f;
            var win = NewRect("Win", _bookPopup);
            Place(win, new Vector2(0.5f, 0.5f), new Vector2(w, h),
                new Vector2(anchorX, chipY - 16f - h * 0.5f));
            var winImg = win.gameObject.AddComponent<Image>();
            winImg.color = new Color(0.97f, 0.94f, 0.84f);
            var winShadow = win.gameObject.AddComponent<Shadow>();
            winShadow.effectColor = new Color(0.2f, 0.12f, 0.06f, 0.5f);
            winShadow.effectDistance = new Vector2(3, -3);

            for (int i = 0; i < options.Count; i++)
            {
                int pick = i;
                var opt = NewRect($"O{i}", win);
                Place(opt, new Vector2(0, 1), new Vector2(190, 28),
                    new Vector2(5 + (i / rows) * 190f, -5f - (i % rows) * 28f));
                opt.pivot = new Vector2(0, 1);
                var oImg = opt.gameObject.AddComponent<Image>();
                oImg.color = new Color(0, 0, 0, 0.001f);
                var ot = NewText("T", opt, _body, 16, TextAnchor.MiddleLeft, new Color(0.2f, 0.12f, 0.06f));
                Stretch(ot.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-4, 0));
                ot.text = options[i];
                var ob = opt.gameObject.AddComponent<Button>();
                ob.targetGraphic = oImg;
                ob.onClick.AddListener(() =>
                {
                    Sfx.Play("click", 0.5f);
                    CloseBookPopup();
                    onPick(pick);
                });
                var sink = opt.gameObject.AddComponent<PressSink>();
                sink.Face = opt; sink.Depth = 2f; sink.Lift = 1f; sink.Tint = ot;
            }
        }

        private void CloseBookPopup()
        {
            if (_bookPopup != null) { Destroy(_bookPopup.gameObject); _bookPopup = null; }
        }

        private bool BookAdmits(RecipeDefinition r)
        {
            if (_bookTier >= 0 && TierIndex(r.Rank) != _bookTier) return false;
            if (_bookPrep >= 0 && (int)r.Prep != _bookPrep) return false;
            if (r.Id == "draught" || r.Id == "neat_pour")
                if (_bookPrep >= 0 || _bookStyle != null) return false;   // the two specials filter out
            if (_bookStyle != null)
            {
                bool has = false;
                foreach (var b in r.RatioRequirements)
                    if (b.IsStyleBand && b.Style == _bookStyle) { has = true; break; }
                if (!has) return false;
            }
            string q = _bookSearch != null ? _bookSearch.text : null;
            if (!string.IsNullOrWhiteSpace(q) &&
                r.Name.IndexOf(q.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return true;
        }

        private static string TierName(int rank) =>
            rank <= 8 ? "STARTER" : rank <= 14 ? "MID SHELF" : rank <= 21 ? "TOP SHELF" : "HOUSE PRIDE";

        private void RebuildRecipeBook()
        {
            var run = Run;
            if (run == null || _bookList == null) return;
            for (int i = _bookList.childCount - 1; i >= 0; i--)
                Destroy(_bookList.GetChild(i).gameObject);

            // A set filter reads as its value alone: at 16px the prefixed form no longer
            // fits the chip, and "SHAKEN" says more than "PREP: SHAKEN" anyway.
            _bookTierChip.text = _bookTier < 0 ? "TIER: ALL" : BookTiers[_bookTier];
            _bookPrepChip.text = _bookPrep < 0 ? "PREP: ALL" : ((PrepMethod)_bookPrep).ToString().ToUpperInvariant();
            _bookStyleChip.text = _bookStyle == null ? "BOTTLE: ALL" : _bookStyle.Replace('_', ' ').ToUpperInvariant();

            var known = new List<RecipeDefinition>();
            foreach (var r in run.MenuRecipes) if (BookAdmits(r)) known.Add(r);
            var locked = new List<RecipeDefinition>();
            foreach (var r in run.LockedRecipes) if (BookAdmits(r)) locked.Add(r);
            known.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            locked.Sort((a, b) => a.Rank.CompareTo(b.Rank));

            var groups = new List<List<RecipeDefinition>>();
            foreach (var r in known)
            {
                if (groups.Count == 0 || TierName(groups[groups.Count - 1][0].Rank) != TierName(r.Rank))
                    groups.Add(new List<RecipeDefinition>());
                groups[groups.Count - 1].Add(r);
            }
            foreach (var g in groups) BookSection(TierName(g[0].Rank), g, lockedRows: false, run);
            if (locked.Count > 0) BookSection("STILL IN THE BOOK", locked, lockedRows: true, run);
            _bookList.anchoredPosition = Vector2.zero;   // a fresh filter reads from the top
        }

        private void BookHeader(string text)
        {
            var h = NewRect("H", _bookList);
            h.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            var t = NewText("T", h, _body, 16, TextAnchor.MiddleLeft, new Color(0.36f, 0.20f, 0.08f));
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 4), Vector2.zero);
            t.text = text;
            var rule = NewRect("Rule", h);
            rule.anchorMin = Vector2.zero; rule.anchorMax = new Vector2(1, 0);
            rule.pivot = new Vector2(0.5f, 0);
            rule.sizeDelta = new Vector2(0, 2);
            rule.anchoredPosition = Vector2.zero;
            var ruleImg = rule.gameObject.AddComponent<Image>();
            ruleImg.color = new Color(0.36f, 0.20f, 0.08f, 0.5f);
            ruleImg.raycastTarget = false;
        }

        /// <summary>A tier's worth of the book: its header, then its own grid — two columns
        /// while the pours fit a half page, one full-width column when they run long.</summary>
        private void BookSection(string title, List<RecipeDefinition> rs, bool lockedRows, TycoonRun run)
        {
            BookHeader(title);
            int maxBands = 0;
            foreach (var r in rs) if (r.RatioRequirements.Count > maxBands) maxBands = r.RatioRequirements.Count;
            int cols = maxBands >= 4 ? 1 : 2;
            var sec = NewRect("Sec", _bookList);
            var g = sec.gameObject.AddComponent<GridLayoutGroup>();
            float fullW = BkW * BkPaperW - 44f;
            g.cellSize = new Vector2(cols == 1 ? fullW : fullW / 2f - 6f, cols == 1 ? 72f : 88f);
            g.spacing = new Vector2(12, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = cols;
            foreach (var r in rs) BookRow(sec, r, lockedRows, run);
        }

        /// <summary>One recipe: the glass drawn from its own bands, the name, how it is
        /// worked, and the pour — or, for a locked one, what it is waiting behind.</summary>
        private void BookRow(RectTransform parent, RecipeDefinition r, bool lockedRow, TycoonRun run)
        {
            var row = NewRect($"R_{r.Id}", parent);
            // A printed line, not a key (2026-08-01): transparent row, thin ink rule under
            // it, the way the licence's own fields sit on their rules.
            var rowImg = row.gameObject.AddComponent<Image>();
            rowImg.color = new Color(0, 0, 0, 0.001f);
            var rowRule = NewRect("Rule", row);
            rowRule.anchorMin = new Vector2(0.01f, 0); rowRule.anchorMax = new Vector2(0.99f, 0);
            rowRule.pivot = new Vector2(0.5f, 0);
            rowRule.sizeDelta = new Vector2(0, 1);
            rowRule.anchoredPosition = new Vector2(0, 1);
            var rowRuleImg = rowRule.gameObject.AddComponent<Image>();
            rowRuleImg.color = new Color(0.30f, 0.20f, 0.10f, lockedRow ? 0.20f : 0.35f);
            rowRuleImg.raycastTarget = false;

            var icon = NewRect("I", row);
            Place(icon, new Vector2(0, 0.5f), new Vector2(40, 40), new Vector2(6, 0));
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = DrinkIcon.For(r, _bootstrap.Glassware);
            img.preserveAspect = true; img.raycastTarget = false;
            img.enabled = img.sprite != null;
            if (lockedRow) img.color = new Color(1, 1, 1, 0.4f);

            var name = NewText("N", row, _body, 16, TextAnchor.UpperLeft,
                lockedRow ? new Color(0.45f, 0.36f, 0.28f) : new Color(0.13f, 0.08f, 0.05f));
            Stretch(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(52, -24), new Vector2(-4, -4));
            // The two brand-agnostic specials never touch the tin; everything else says how
            // it is worked. (Prep defaults to Shaken in the ctor, which fits neither a pint
            // nor a neat pour.)
            string prep = r.Id == "draught" ? "ON TAP"
                : r.Id == "neat_pour" ? "NEAT"
                : r.Prep == PrepMethod.Shaken ? "SHAKEN"
                : r.Prep == PrepMethod.Stirred ? "STIRRED" : "BUILT";
            name.text = r.Name.ToUpperInvariant();

            var line = NewText("L", row, _body, 16, TextAnchor.UpperLeft, new Color(0.45f, 0.35f, 0.24f));
            Stretch(line.rectTransform, Vector2.zero, Vector2.one, new Vector2(52, 4), new Vector2(-4, -26));
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Truncate;   // never lap the row beneath
            line.supportRichText = true;
            // The prep leads the detail line — at 16px it no longer fits beside the name —
            // and the price is gone (the author: the shop can do its own selling).
            string lead = $"<color=#1B5F66>{prep}</color>";
            if (lockedRow)
            {
                double gate = run.RecipeStarGate(r);
                line.text = lead + (gate > 0 ? $" · {gate:0.0}★" : "") + " · " + BandLine(r);
            }
            else line.text = lead + " · " + BandLine(r);
        }

        /// <summary>"GIN 45–65 · LEMON 20–40 · SYRUP 10–30" — the pour, said in shares.</summary>
        private static string BandLine(RecipeDefinition r)
        {
            // The numbers carry the craft, so they print in heavier ink than the names.
            var parts = new List<string>();
            foreach (var b in r.RatioRequirements)
                parts.Add(string.Format("{0} <color=#1A0E06>{1:0}–{2:0}%</color>",
                    (b.IsStyleBand ? b.Style.Replace('_', ' ') : b.Type.ToString()).ToUpperInvariant(),
                    b.MinRatio * 100, b.MaxRatio * 100));
            if (r.MinFill > 0)
                parts.Add(string.Format("<color=#1A0E06>FILL {0:0}%+</color>", r.MinFill * 100));
            return string.Join(" · ", parts);
        }

        // ── settings (P17): the smallest sheet that holds sound and motion ───────

        private RectTransform _settingsPanel;
        private Text _settingsVolume, _settingsMute, _settingsMotion;

        private void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            _settingsPanel.gameObject.SetActive(!_settingsPanel.gameObject.activeSelf);
            RefreshSettings();
        }

        private void BuildSettings(RectTransform root)
        {
            _settingsPanel = NewRect("Settings", root);
            Place(_settingsPanel, new Vector2(1, 1), new Vector2(240, 214), new Vector2(-16, -48));
            _settingsPanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];

            var title = NewText("T", _settingsPanel, _body, 10, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -18), Vector2.zero);
            title.text = "— SETTINGS —";

            _settingsVolume = SettingsRow(0, "VOLUME", () =>
            {
                Sound.Volume = Sound.Volume <= 0.05f ? 0.2f : Sound.Volume >= 0.95f ? 0.2f
                    : Sound.Volume + 0.2f;      // cycles 0.2 → 1.0 and wraps
                Sfx.Play("click");
                RefreshSettings();
            });
            _settingsMute = SettingsRow(1, "SOUND", () =>
            {
                Sound.Muted = !Sound.Muted;
                Sfx.Play("click");              // audible iff it just came back on — itself the test
                RefreshSettings();
            });
            // The three game modes (the author's dev tool): a fresh bar, a mid-run bar,
            // and the endgame sandbox — playtest any act without earning your way to it.
            SettingsRow(3, "DEV · FRESH START", () =>
            { _bootstrap.StartNewRun(null); ToggleSettings(); });
            SettingsRow(4, "DEV · MIDGAME", () =>
            { _bootstrap.StartNewRun(null); Run.DevPreset(1); ApplyBarLook(); ToggleSettings(); });
            SettingsRow(5, "DEV · ENDGAME", () =>
            { _bootstrap.StartNewRun(null); Run.DevPreset(2); ApplyBarLook(); ToggleSettings(); });

            _settingsMotion = SettingsRow(2, "MOTION", () =>
            {
                Motion.Reduced = !Motion.Reduced;
                Sfx.Play("click");
                RefreshSettings();
            });

            _settingsPanel.gameObject.SetActive(false);
        }

        private Text SettingsRow(int index, string label, Action onClick)
        {
            var row = NewRect($"Row{index}", _settingsPanel);
            Place(row, new Vector2(0.5f, 1), new Vector2(220, 28), new Vector2(0, -24f - index * 32f));
            var img = row.gameObject.AddComponent<Image>();
            img.color = UITheme.Night[3];
            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var text = NewText("L", row, _body, 12, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return text;
        }

        private void RefreshSettings()
        {
            if (_settingsVolume == null) return;
            _settingsVolume.text = $"VOLUME  {Mathf.RoundToInt(Sound.Volume * 100)}%";
            _settingsMute.text = Sound.Muted ? "SOUND  OFF" : "SOUND  ON";
            _settingsMotion.text = Motion.Reduced ? "MOTION  REDUCED" : "MOTION  FULL";
        }

        private static string CrowdName(WealthTier tier) =>
            tier == WealthTier.HighRoller ? "HIGH ROLLERS" : tier == WealthTier.Broke ? "BROKE" : "REGULARS";

        /// <summary>Leader dots so the bill columns line up in the monospace pixel font.</summary>
        private static string Dots(int n) => "<color=#9C8F80>" + new string('.', n) + "</color>";

        /// <summary>The slip is <see cref="Columns"/> characters wide. The pixel font is
        /// monospace, so a receipt line can be set the way a till sets one: label at the left,
        /// amount flush right, leader dots filling whatever is between them.</summary>
        private const int Columns = 26;

        private static readonly string Rule = new string('=', Columns);

        /// <summary>One receipt line, right-aligned to the slip's width.</summary>
        private static string Line(string label, string amount, string hex)
        {
            int gap = Math.Max(1, Columns - label.Length - amount.Length);
            string body = label + Dots(gap) + amount;
            return hex == null ? body : $"<color=#{hex}>{body}</color>";
        }

        private void ShowClosed()
        {
            _dayEndPanel.gameObject.SetActive(false);
            CloseId();
            _bannerText.gameObject.SetActive(true);
            _bannerText.text = "✖ THE BAR IS CLOSED\nthree days in the red — NEW RUN to try again";
        }

        // ── the licence: read the customer (GDD 24 §5) ───────────────────────────

        private void ShowId(CustomerVisit visit)
        {
            if (visit?.Regular == null) return;
            _idVisit = visit;
            var reg = visit.Regular;

            // Opening the card IS the inspection (v5 C3): this is the one gate Core opens the
            // order through, so everything below may read it — and the bubble may from now on.
            visit.InspectId();

            if (_ledgerPanel != null) _ledgerPanel.gameObject.SetActive(false);
            _idRoot.gameObject.SetActive(true);
            _idPhoto.sprite = stage != null ? stage.PortraitSpriteFor(reg.ArchetypeId) : null;
            _idPhoto.color = _idPhoto.sprite != null ? Color.white : UITheme.Night[3];

            _idName.text = reg.Name.ToUpperInvariant();
            _idAgeFrom.text = $"{reg.Age}  ·  {reg.Hometown.ToUpperInvariant()}";
            _idRel.text = reg.Visits > 0
                ? $"{reg.Relationship.ToString().ToUpperInvariant()} · {reg.Visits} VISITS"
                : "NEW FACE";

            // What THEY make of US — their own nights here, said in stars. A stranger has no
            // row at all (the author's note: empty fields were noise, not a licence).
            bool stranger = reg.Visits == 0;
            _idRatesLabel.text = stranger ? "" : "RATES THIS BAR";
            _idRates.text = stranger ? ""
                : Stars(Mathf.RoundToInt(5f * reg.SatisfiedCount / reg.Visits));

            // No price, anywhere on the card (C3): the licence says who they are and what they
            // want, and what a drink costs is the menu's business.
            _idOrder.text = $"<b>{visit.Order.Wanted.Name.ToUpperInvariant()}</b>";
            var parts = new List<string>();
            foreach (var band in visit.Order.Wanted.RatioRequirements)
                parts.Add((band.IsStyleBand ? band.Style.Replace('_', ' ') : band.Type.ToString())
                    .ToUpperInvariant());
            _idOrderParts.text = string.Join("  ·  ", parts);
            _idOrderIcon.sprite = DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
            _idOrderIcon.enabled = _idOrderIcon.sprite != null;

            // The endorsements, drawn rather than listed (the author): each ask is a
            // pictogram with its word under it, and the read's fill preference joins them
            // as a glass marked with the band it wants — the empty space counted in the
            // numbers, which is the honest way to say how full a glass should be.
            var spec = visit.Order.Spec;
            foreach (Transform old in _idPrefRow) Destroy(old.gameObject);
            int chips = 0;
            foreach (var g in visit.Order.Garnishes)
                chips += PrefChip(PrefArt.ForPreparation(g.Id), g.Name.ToUpperInvariant());
            if (spec.ExtraShaken) chips += PrefChip(PrefArt.Shaker(), "SHAKEN HARD");
            if (spec.FilledToTheTop) chips += PrefChip(PrefArt.FilledGlass(), "TO THE TOP");
            // No fill chip (the author, 2026-08-02): nobody demands a fill any more — the
            // only fill rule is the house floor, and it lives in the judge, not the licence.
            _idIntent.text = chips == 0 ? "SERVE IT CLEAN" : "";
        }

        /// <summary>One pictogram with its word under it. Returns 1 so the caller can count.</summary>
        private int PrefChip(Sprite icon, string label)
        {
            var chip = NewRect("Pref", _idPrefRow);
            var iconRt = NewRect("I", chip);
            Place(iconRt, new Vector2(0.5f, 1), new Vector2(26, 26), Vector2.zero);
            iconRt.pivot = new Vector2(0.5f, 1);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
            img.enabled = icon != null;
            var t = NewText("L", chip, _body, 8, TextAnchor.UpperCenter, UITheme.Night[1]);
            Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(96, 10), new Vector2(0, -28));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = label;
            var le = chip.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = Mathf.Max(44f, label.Length * 7f);
            le.preferredHeight = 40f;
            return 1;
        }

        // The week (the author's calendar): six open days, Tuesday through Sunday —
        // Mondays the bar is dark and the calendar simply skips them.
        private static readonly string[] OpenDayNames =
            { "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" };

        private static string CalendarFor(int day) =>
            $"WEEK {(day - 1) / 6 + 1} · {OpenDayNames[(day - 1) % 6]}";

        /// <summary>0–5 stars as glyphs, the empty ones kept so the width never jumps.</summary>
        private static string Stars(int n) =>
            new string('★', Mathf.Clamp(n, 0, 5)) + new string('☆', 5 - Mathf.Clamp(n, 0, 5));

        private void CloseId()
        {
            _idVisit = null;
            if (_idRoot != null) _idRoot.gameObject.SetActive(false);
        }

        private static float TrackAt(int value) => IdTrackW * Mathf.Clamp01(value / 100f);

        // ── the licence, v3 (P15 / C3) ──────────────────────────────────────────
        // A landscape US-licence, not a dossier: the v2 portrait card was explicitly disliked.
        // The shell is generated art (the first UI piece since the author lifted the no-AI-UI
        // rule, 2026-07-31) drawn at exactly 2× its 400×250 pixels — an integer scale, because
        // the prep table taught what a fractional upscale next to native-size text looks like.
        // Every field is lettered in engine over the blank shell: the generator cannot spell.
        // Sized down to 2.5× on the author's note ("kimlik boyutunu biraz küçült"), and the
        // lettering SITS ON THE SHELL'S OWN RULE LINES now — the art carries six faint field
        // rules at y 58/76/94/113/132/150 (measured), and every value's baseline lands on one,
        // so the text belongs to the printed card instead of floating over it.
        // The v4 shell (2026-08-02): a denser generated card — five rules, tight bottom —
        // measured off licence_shell2.png (510×315: navy band rows 23–51, portrait x 26–168
        // y 81–286, rules y 96/127/159/190/219) and drawn at 1.4×. The old shell ran long
        // and its lettering was hard to read; the values sit on the display face now.
        private const float LicScale = 1.4f;
        private const float LicW = 510f * LicScale, LicH = 315f * LicScale;
        private static readonly Rect LicPortrait = new Rect(26f * LicScale, -81f * LicScale,
            143f * LicScale, 206f * LicScale);
        private const float LicHeaderH = 29f * LicScale;
        private const float LicHeaderY = -23f * LicScale;
        private const float LicFieldsX = 200f * LicScale;
        private const float LicFieldsW = 280f * LicScale;
        private static readonly float[] LicLines =   // the art's five rules, ×1.4
            { 96f * LicScale, 127f * LicScale, 159f * LicScale, 190f * LicScale, 219f * LicScale };

        /// <summary>
        /// One licence line, SEATED on a rule: the value's bottom edge lands on the shell's own
        /// printed line (the way a form is filled in), with the small navy label just above it.
        /// Returns the value; the label comes back through <paramref name="labelText"/> so a
        /// row that is sometimes empty (a stranger has no rating yet) can hide whole.
        /// </summary>
        private Text LicenceField(RectTransform card, string label, float x, float lineY,
            float w, out Text labelText, int valueSize = 16)
        {
            float vh = valueSize + 6f;
            labelText = NewText("L_" + label, card, _body, 8, TextAnchor.LowerLeft, UITheme.ClubBlue[2]);
            Place(labelText.rectTransform, new Vector2(0, 1), new Vector2(w, 12), Vector2.zero);
            labelText.rectTransform.pivot = new Vector2(0, 0);
            labelText.rectTransform.anchoredPosition = new Vector2(x, -lineY + vh + 2f);
            labelText.text = label;
            var val = NewText("V_" + label, card, _display, valueSize, TextAnchor.LowerLeft, UITheme.Night[1]);
            val.supportRichText = true;
            val.horizontalOverflow = HorizontalWrapMode.Overflow;   // a licence never wraps; it runs
            Place(val.rectTransform, new Vector2(0, 1), new Vector2(w, vh), Vector2.zero);
            val.rectTransform.pivot = new Vector2(0, 0);
            val.rectTransform.anchoredPosition = new Vector2(x, -lineY + 2f);
            return val;
        }

        private void BuildIdCard(RectTransform root)
        {
            _idRoot = NewRect("IdCard", root);
            Stretch(_idRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _idRoot.gameObject.AddComponent<Image>();
            scrim.color = UITheme.Scrim;
            var scrimBtn = _idRoot.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(CloseId);

            var card = NewRect("Card", _idRoot);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(LicW, LicH), new Vector2(0, 10));
            var shell = card.gameObject.AddComponent<Image>();
            shell.sprite = ItemArt.Load("licence_shell2");
            if (shell.sprite == null) shell.sprite = ItemArt.Load("licence_shell");
            if (shell.sprite == null) shell.color = UITheme.Cream[4];   // no art: a plain card
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallow clicks

            var htext = NewText("H", card, _body, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Place(htext.rectTransform, new Vector2(0.5f, 1), new Vector2(LicW - 40, LicHeaderH),
                new Vector2(0, LicHeaderY));
            htext.text = "NEW ARDEN  ·  PATRON LICENCE";

            var photo = NewRect("Photo", card);
            Place(photo, new Vector2(0, 1), new Vector2(LicPortrait.width, LicPortrait.height),
                new Vector2(LicPortrait.x, LicPortrait.y));
            _idPhoto = photo.gameObject.AddComponent<Image>();
            _idPhoto.preserveAspect = true;

            // The data column, one field to a printed rule (the author's note: the text and
            // the art disagreed — now the art's own lines decide where the text sits). The
            // reserved-slots row is GONE: a row of blanks was noise, not a licence.
            float colW = LicFieldsW * 0.5f - 8f;
            _idName = LicenceField(card, "NAME", LicFieldsX, LicLines[0], LicFieldsW, out _, 24);
            _idAgeFrom = LicenceField(card, "AGE  ·  CITY", LicFieldsX, LicLines[1], LicFieldsW, out _);
            _idRel = LicenceField(card, "STANDING", LicFieldsX, LicLines[2], colW, out _);
            _idRates = LicenceField(card, "RATES THIS BAR", LicFieldsX + colW + 16f, LicLines[2],
                colW, out _idRatesLabel);

            // The order, seated on its own rule with the glass drawn beside it.
            var idIcon = NewRect("OrderIcon", card);
            Place(idIcon, new Vector2(0, 1), new Vector2(30, 30), Vector2.zero);
            idIcon.pivot = new Vector2(0, 0);
            idIcon.anchoredPosition = new Vector2(LicFieldsX, -LicLines[3] + 2f);
            _idOrderIcon = idIcon.gameObject.AddComponent<Image>();
            _idOrderIcon.preserveAspect = true;
            _idOrderIcon.raycastTarget = false;
            _idOrder = LicenceField(card, "ORDER", LicFieldsX + 40f, LicLines[3],
                LicFieldsW - 40f, out _, 16);
            // What is IN it, under the name (v5 P16): the menu speaks styles now, so the
            // licence has to say gin-and-tonic, not just "Gin & Tonic" — this line is the
            // player's recipe knowledge since the band rows left with v2.
            _idOrderParts = NewText("OrderParts", card, _body, 8, TextAnchor.UpperLeft, UITheme.Night[3]);
            Place(_idOrderParts.rectTransform, new Vector2(0, 1), new Vector2(LicFieldsW - 54f, 12),
                Vector2.zero);
            _idOrderParts.rectTransform.pivot = new Vector2(0, 1);
            _idOrderParts.rectTransform.anchoredPosition = new Vector2(LicFieldsX + 54f, -LicLines[3] - 4f);

            // Hovering the order shows the RECIPE (2026-07-31): the drink they asked for,
            // said the way the book says it — prep, pour shares, glass — without leaving
            // the card. The hit rect covers the order line, icon included.
            var orderHit = NewRect("OrderHit", card);
            Place(orderHit, new Vector2(0, 1), new Vector2(LicFieldsW, 52), Vector2.zero);
            orderHit.pivot = new Vector2(0, 0);
            orderHit.anchoredPosition = new Vector2(LicFieldsX, -LicLines[3] - 6f);
            var orderHitImg = orderHit.gameObject.AddComponent<Image>();
            orderHitImg.color = new Color(0, 0, 0, 0.001f);
            // VERTICAL and vice (the author, 2026-08-02): the cream chip vanished into the
            // cream card. A dark glass panel, cyan-edged, one pour to a line, the numbers
            // bright — parked over the seal corner where nothing else lives.
            _idRecipeTip = NewRect("RecipeTip", card);
            Place(_idRecipeTip, new Vector2(1, 1), new Vector2(252, 120), Vector2.zero);
            _idRecipeTip.pivot = new Vector2(1, 1);
            _idRecipeTip.anchoredPosition = new Vector2(-18f, -LicLines[2] + 6f);
            var tipBg = _idRecipeTip.gameObject.AddComponent<Image>();
            tipBg.color = new Color(0.07f, 0.07f, 0.11f, 0.96f);
            var tipEdge = new Color(UITheme.Cyan[3].r, UITheme.Cyan[3].g, UITheme.Cyan[3].b, 0.8f);
            Hairline(_idRecipeTip, new Vector2(0, 0), new Vector2(1, 0), tipEdge);
            Hairline(_idRecipeTip, new Vector2(0, 1), new Vector2(1, 1), tipEdge);
            HairlineV(_idRecipeTip, 0f, tipEdge);
            HairlineV(_idRecipeTip, 1f, tipEdge);
            _idRecipeTipText = NewText("T", _idRecipeTip, _body, 16, TextAnchor.UpperLeft,
                UITheme.TextSecondary);
            Stretch(_idRecipeTipText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
            _idRecipeTipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _idRecipeTipText.verticalOverflow = VerticalWrapMode.Overflow;
            _idRecipeTip.gameObject.SetActive(false);
            var trig = orderHit.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowOrderRecipeTip());
            trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => _idRecipeTip.gameObject.SetActive(false));
            trig.triggers.Add(exit);

            // Serving preferences — the endorsements, drawn as pictograms (the author,
            // 2026-08-01) in the free band under the rule; the field text only survives to
            // say SERVE IT CLEAN when there is nothing to draw.
            _idIntent = LicenceField(card, "SERVING PREFERENCES", LicFieldsX, LicLines[4],
                LicFieldsW, out _, 12);
            _idPrefRow = NewRect("PrefRow", card);
            Place(_idPrefRow, new Vector2(0, 1), new Vector2(LicFieldsW, 42), Vector2.zero);
            _idPrefRow.pivot = new Vector2(0, 1);
            _idPrefRow.anchoredPosition = new Vector2(LicFieldsX, -LicLines[4] - 2f);
            var prefLayout = _idPrefRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            prefLayout.spacing = 8;
            prefLayout.childControlWidth = true; prefLayout.childForceExpandWidth = false;
            prefLayout.childControlHeight = true; prefLayout.childForceExpandHeight = false;
            prefLayout.childAlignment = TextAnchor.UpperLeft;

            var hint = NewText("Hint", _idRoot, _body, 12, TextAnchor.MiddleCenter, UITheme.TextSecondary);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400, 20),
                new Vector2(0, -(LicH * 0.5f) - 16f));
            hint.text = "TAP OUTSIDE THE CARD TO HAND IT BACK";

            _idRoot.gameObject.SetActive(false);
        }

        // ── construction ────────────────────────────────────────────────────────

        private void BuildUi()
        {
            LoadPatronFrames();   // the seated customer's animation clips (Resources/Patron/*)

            var canvasGo = new GameObject("TycoonHud", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            // Match height (like the stage, 2026-07-22): every canvas scales off the same axis
            // so nothing drifts out of alignment when the window's aspect changes.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasGo.transform;
            _hudRoot = root;

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            // Top bar v2 (v5 P13). Three groups, each anchored to its own edge instead of the
            // hand-tuned offsets from the centre that the first pass used: the clock at the
            // left, the till in the middle, the standing at the right. It is also OPAQUE now,
            // with a lit bottom rule — at 0.82 the neon sign behind it showed straight through
            // the rating, which is the one number the whole loop is about.
            var top = Panel(root, "TopBar", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -44), Vector2.zero, UITheme.Night[0]);
            var rule = NewRect("Rule", top);
            rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0);
            rule.pivot = new Vector2(0.5f, 0);
            rule.sizeDelta = new Vector2(0, 2);
            rule.anchoredPosition = Vector2.zero;
            rule.gameObject.AddComponent<Image>().color = UITheme.Amber[2];

            _dayText = NewText("Day", top, _display, 14, TextAnchor.MiddleLeft, UITheme.TextPrimary);
            Place(_dayText.rectTransform, new Vector2(0, 0.5f), new Vector2(300, 30), new Vector2(16, 0));

            _moneyText = NewText("Money", top, _display, 16, TextAnchor.MiddleCenter, UITheme.Money);
            Place(_moneyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200, 30), Vector2.zero);

            // The standing: five stars, the number, and who is in tonight — read right to left
            // from the NEW RUN button, so the group keeps its shape at any window width.
            NewButton(top, "NEW RUN", new Vector2(1, 0.5f), new Vector2(110, 30),
                new Vector2(-68, 0), UITheme.PrimaryAction, () => _bootstrap.StartNewRun(null));

            // Settings (P17): sound and reduced motion in one small sheet under a gear. The
            // audio items the phase asks for live BESIDE the motion toggle — this is the
            // settings surface module 16 deferred, at its minimum useful size.
            NewButton(top, "⚙", new Vector2(1, 0.5f), new Vector2(30, 30),
                new Vector2(-32, 0), UITheme.Night[3], ToggleSettings);
            BuildSettings(root);

            // The standing, front and centre (the author: the stars are becoming the
            // game's spine) — a row of five just right of the till, PARTIALLY filled:
            // 1.3 shows 1.3 stars' worth of amber under a mask, not a dimmed second star.
            const float StarSize = 16f, StarGap = 20f;
            float starsW = _ratingStars.Length * StarGap;
            var starsRow = NewRect("Stars", top);
            Place(starsRow, new Vector2(0.5f, 0.5f), new Vector2(starsW, StarSize), new Vector2(196f, 0));
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                var star = NewRect($"B{i}", starsRow);
                star.anchorMin = star.anchorMax = new Vector2(0, 0.5f);
                star.pivot = new Vector2(0.5f, 0.5f);
                star.sizeDelta = new Vector2(StarSize, StarSize);
                star.anchoredPosition = new Vector2(i * StarGap + StarGap * 0.5f, 0);
                var img = star.gameObject.AddComponent<Image>();
                img.sprite = ItemArt.Load("star");
                img.preserveAspect = true; img.raycastTarget = false;
                img.color = UITheme.Night[3];
            }
            _starsFill = NewRect("Fill", starsRow);
            _starsFill.anchorMin = new Vector2(0, 0); _starsFill.anchorMax = new Vector2(0, 1);
            _starsFill.pivot = new Vector2(0, 0.5f);
            _starsFill.sizeDelta = Vector2.zero;
            _starsFill.anchoredPosition = Vector2.zero;
            _starsFill.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                var star = NewRect($"F{i}", _starsFill);
                star.anchorMin = star.anchorMax = new Vector2(0, 0.5f);   // fixed x: the mask slides over it
                star.pivot = new Vector2(0.5f, 0.5f);
                star.sizeDelta = new Vector2(StarSize, StarSize);
                star.anchoredPosition = new Vector2(i * StarGap + StarGap * 0.5f, 0);
                var img = star.gameObject.AddComponent<Image>();
                img.sprite = ItemArt.Load("star");
                img.preserveAspect = true; img.raycastTarget = false;
                img.color = UITheme.Amber[3];
                _ratingStars[i] = img;
            }
            _ratingText = NewText("Rating", top, _display, 14, TextAnchor.MiddleLeft, UITheme.Amber[3]);
            Place(_ratingText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60, 30),
                new Vector2(196f + starsW * 0.5f + 36f, 0));

            _crowdText = NewText("Crowd", top, _body, 13, TextAnchor.MiddleRight, UITheme.TextSecondary);
            Place(_crowdText.rectTransform, new Vector2(1, 0.5f), new Vector2(200, 30),
                new Vector2(-196f, 0));

            // BIN GLASS retired (v5 P13 / C7): a drink is thrown away by carrying it to the bin
            // on the counter, which is the same verb that serves it.

            // Refusal notices ("NOT ENOUGH MONEY") drop in just under the top bar.
            _toast = NewText("Toast", root, _display, 14, TextAnchor.MiddleCenter, UITheme.ViceRed[3]);
            Place(_toast.rectTransform, new Vector2(0.5f, 1), new Vector2(500, 30), new Vector2(0, -56));
            _toast.gameObject.SetActive(false);

            // Six stools along the counter: each customer is a bust sitting at the bar with a
            // floating order tag above their head; click anywhere on them to read or serve.
            const float seatGap = 180f;
            const float seatStartX = 118f;
            for (int i = 0; i < SeatSlots; i++)
            {
                int index = i;
                var seat = new SeatView();
                seat.SeatX = seatStartX + i * seatGap;

                // The click zone spans the bust and its tag; a clear image catches the ray.
                seat.Root = NewRect($"Seat{i}", root);
                seat.Root.anchorMin = seat.Root.anchorMax = new Vector2(0, 0);
                seat.Root.pivot = new Vector2(0.5f, 0);
                seat.Root.sizeDelta = new Vector2(150f, CharWinH + 110f);
                seat.Root.anchoredPosition = new Vector2(seat.SeatX, CounterLineY);
                var hit = seat.Root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);   // invisible, but catches clicks
                var button = seat.Root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OnSeatClicked(index));
                seat.Group = seat.Root.gameObject.AddComponent<CanvasGroup>();

                // The customer stands behind the bar. A masked window shows them from about the
                // waist up (the bar clips the legs); the animated character sprite lives inside it.
                var win = NewRect("CharWin", seat.Root);
                win.anchorMin = win.anchorMax = new Vector2(0.5f, 0);
                win.pivot = new Vector2(0.5f, 0);
                win.sizeDelta = new Vector2(CharSize, CharWinH);
                win.anchoredPosition = new Vector2(0, 0);
                win.gameObject.AddComponent<RectMask2D>();

                var charRt = NewRect("Char", win);
                charRt.anchorMin = charRt.anchorMax = new Vector2(0.5f, 0);
                charRt.pivot = new Vector2(0.5f, 0);
                charRt.sizeDelta = new Vector2(CharSize, CharSize);
                charRt.anchoredPosition = new Vector2(0, -CharFootDrop);   // drop the legs below the window
                seat.Portrait = charRt.gameObject.AddComponent<Image>();
                seat.Portrait.preserveAspect = true;
                seat.Portrait.raycastTarget = false;

                // The order tag, floating above the head.
                seat.Tag = NewRect("Tag", seat.Root);
                seat.Tag.anchorMin = seat.Tag.anchorMax = new Vector2(0.5f, 0);
                seat.Tag.pivot = new Vector2(0.5f, 0);
                seat.Tag.sizeDelta = new Vector2(BustW + 48f, 84f);
                seat.Tag.anchoredPosition = new Vector2(0, CharWinH + 10f);
                seat.TagBg = seat.Tag.gameObject.AddComponent<Image>();
                seat.TagBg.raycastTarget = false;
                // Vice, not parchment (the author, 2026-08-02: too much yellow): a dark
                // glassy card with one neon rule under it — the room's own palette.
                var tagRule = NewRect("Rule", seat.Tag);
                tagRule.anchorMin = new Vector2(0.06f, 0); tagRule.anchorMax = new Vector2(0.94f, 0);
                tagRule.pivot = new Vector2(0.5f, 0);
                tagRule.sizeDelta = new Vector2(0, 2);
                tagRule.anchoredPosition = new Vector2(0, 2);
                var tagRuleImg = tagRule.gameObject.AddComponent<Image>();
                tagRuleImg.color = UITheme.Cyan[3];
                tagRuleImg.raycastTarget = false;

                seat.Name = NewText("Name", seat.Tag, _body, 12, TextAnchor.UpperCenter,
                    UITheme.TextPrimary);
                Stretch(seat.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -10));
                seat.Name.horizontalOverflow = HorizontalWrapMode.Overflow;

                seat.Wants = NewText("Wants", seat.Tag, _body, 10, TextAnchor.UpperCenter,
                    UITheme.Cyan[4]);
                Stretch(seat.Wants.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -26));
                seat.Wants.horizontalOverflow = HorizontalWrapMode.Overflow;

                // Centred for real (2026-07-31): the row used to keep a 32px left inset so the
                // corner-pinned icon had room, which centred the text in a right-shifted box —
                // visibly off-centre on every seat. Now the TEXT owns the middle and the icon
                // rides just left of its measured width, per refresh, like a bullet point.
                seat.Order = NewText("Order", seat.Tag, _body, 11, TextAnchor.UpperCenter,
                    UITheme.Magenta[4]);
                Stretch(seat.Order.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -42));
                seat.Order.horizontalOverflow = HorizontalWrapMode.Overflow;

                // The drink itself, on the order row (v5 P13). The shape and colour of the
                // glass is what a busy player actually reads across five stools. 24px, so it
                // clears the patience bar; its position follows the text every refresh.
                var iconRt = NewRect("OrderIcon", seat.Tag);
                Place(iconRt, new Vector2(0.5f, 1), new Vector2(24, 24), new Vector2(0, -42));
                iconRt.pivot = new Vector2(1f, 0.5f);   // placed by its right edge, beside the text
                seat.Icon = iconRt.gameObject.AddComponent<Image>();
                seat.Icon.preserveAspect = true;
                seat.Icon.raycastTarget = false;

                // The patience gauge rides the BODY, not the ticket (P15, absorbs the P8
                // gauge item): a slim bar floating just over the head, so reading who is
                // about to walk means looking at the people, not at their paperwork.
                var clockBg = NewRect("ClockBg", seat.Root);
                clockBg.anchorMin = clockBg.anchorMax = new Vector2(0.5f, 0);
                clockBg.pivot = new Vector2(0.5f, 0);
                clockBg.sizeDelta = new Vector2(BustW * 0.72f, 8f);
                clockBg.anchoredPosition = new Vector2(0, CharWinH + 1f);
                clockBg.gameObject.AddComponent<Image>().color = UITheme.Night[0];
                var clockFill = NewRect("ClockFill", clockBg);
                clockFill.anchorMin = new Vector2(0, 0); clockFill.anchorMax = new Vector2(0, 1);
                clockFill.pivot = new Vector2(0, 0.5f);
                clockFill.offsetMin = new Vector2(1, 1); clockFill.offsetMax = new Vector2(1, -1);
                clockFill.anchoredPosition = new Vector2(1, 0);
                seat.PatienceFill = clockFill.gameObject.AddComponent<Image>();
                seat.PatienceFill.raycastTarget = false;

                seat.Root.gameObject.SetActive(false);
                _seats.Add(seat);
            }

            // The primary action: open the menu to build a drink (GDD 24 §1), bottom-centred.
            NewButton(root, "▸  MENU — MAKE A DRINK", new Vector2(0.5f, 0),
                new Vector2(300, 40), new Vector2(0, 40), UITheme.PrimaryAction, OnMenuClicked);

            // The recipe book, beside the making verb (v5 P16): the menu speaks styles now,
            // so how a drink is MADE has to live somewhere the player can read mid-shift.
            NewButton(root, "❧ BOOK", new Vector2(0.5f, 0),
                new Vector2(84, 40), new Vector2(-196, 40), UITheme.Night[3], ToggleRecipeBook);
            BuildRecipeBook(root);

            BuildDrinkGlass(root);
            BuildSnackRow(root);
            BuildServiceLog(root);
            BuildIdCard(root);

            // Day end: a plain invoice panel with the night's business under it.
            _dayEndPanel = NewRect("DayEnd", root);
            Place(_dayEndPanel, new Vector2(0.5f, 0.5f), new Vector2(940, 600), new Vector2(0, 10));
            var panelImg = _dayEndPanel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(UITheme.Night[1].r, UITheme.Night[1].g, UITheme.Night[1].b, 0.97f);

            var title = _dayEndTitle = NewText("Title", _dayEndPanel, _display, 16, TextAnchor.MiddleCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -46), new Vector2(0, -8));
            title.text = "LAST CALL — THE BOOKS";

            // Left column: the till slip (v5 P13). Cream stock, and pinned to 16pt — a whole
            // multiple of the face's 8px design size, so the monospace columns the receipt is
            // set in actually land on the pixel grid instead of blurring between it.
            var bill = _dayEndBill = NewRect("Bill", _dayEndPanel);
            Place(bill, new Vector2(0.5f, 1), new Vector2(400, 470), new Vector2(0, -56));
            bill.gameObject.AddComponent<Image>().color = UITheme.Cream[4];
            _invoiceText = NewText("Invoice", bill, _body, 16, TextAnchor.UpperLeft, UITheme.Night[1]);
            Stretch(_invoiceText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 12), new Vector2(-14, -12));
            _invoiceText.supportRichText = true;

            // Right column: the shop, as the tablet the bar orders from (v5 P13). It was a flat
            // grid of thirteen identical cards — a bottle, a stool and a musician all reading the
            // same. The shell is the cheap half of the fix; the tabs are the real one, because
            // restocking the well and buying a musician are different errands and the player is
            // only ever doing one of them.
            var tablet = _dayEndTablet = NewRect("Tablet", _dayEndPanel);
            Place(tablet, new Vector2(0.5f, 1), new Vector2(892, 468), new Vector2(0, -56));
            tablet.gameObject.AddComponent<Image>().color = TabletShell;

            var lens = NewRect("Lens", tablet);
            Place(lens, new Vector2(0.5f, 1), new Vector2(5, 5), new Vector2(0, -6));
            lens.gameObject.AddComponent<Image>().color = TabletLens;

            var homeBar = NewRect("Home", tablet);
            Place(homeBar, new Vector2(0.5f, 0), new Vector2(84, 4), new Vector2(0, 6));
            homeBar.gameObject.AddComponent<Image>().color = TabletLens;

            var screen = NewRect("Screen", tablet);
            Stretch(screen, Vector2.zero, Vector2.one, new Vector2(14, 16), new Vector2(-14, -16));
            screen.gameObject.AddComponent<Image>().color = TabletScreen;

            // The status strip: what this is, and what there is to spend.
            var strip = NewRect("Strip", screen);
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1);
            strip.pivot = new Vector2(0.5f, 1);
            strip.sizeDelta = new Vector2(0, 22);
            strip.anchoredPosition = Vector2.zero;
            strip.gameObject.AddComponent<Image>().color = UITheme.Night[0];
            var stripName = NewText("N", strip, _body, 12, TextAnchor.MiddleLeft, UITheme.TextSecondary);
            Stretch(stripName.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-120, 0));
            stripName.text = "SUPPLY — ORDER IN";
            _tabletTill = NewText("Till", strip, _body, 12, TextAnchor.MiddleRight, UITheme.Money);
            Stretch(_tabletTill.rectTransform, Vector2.zero, Vector2.one, new Vector2(120, 0), new Vector2(-10, 0));

            for (int i = 0; i < ShopTabs.Length; i++)
            {
                int tab = i;
                var key = NewRect($"Tab{i}", screen);
                Place(key, new Vector2(0, 1), new Vector2(120, 24), new Vector2(10 + i * 126, -28));
                var bg = key.gameObject.AddComponent<Image>();
                var btn = key.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => { _shopTab = tab; RebuildDayEnd(); });
                var label = NewText("L", key, _body, 12, TextAnchor.MiddleCenter, UITheme.TextPrimary);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                label.text = ShopTabs[i];
                _shopTabKeys[i] = bg;
                _shopTabLabels[i] = label;
            }

            // The market is grouped now (the author): a stack of titled sections, each its
            // own grid, the whole thing scrolling — the shop outgrew a single flat grid.
            var offerView = NewRect("OfferView", screen);
            Stretch(offerView, Vector2.zero, Vector2.one, new Vector2(10, 30), new Vector2(-10, -58));
            offerView.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.003f);
            offerView.gameObject.AddComponent<RectMask2D>();
            _offerRow = NewRect("Offers", offerView);
            _offerRow.anchorMin = new Vector2(0, 1); _offerRow.anchorMax = Vector2.one;
            _offerRow.pivot = new Vector2(0.5f, 1);
            _offerRow.offsetMin = Vector2.zero; _offerRow.offsetMax = Vector2.zero;
            var shopLayout = _offerRow.gameObject.AddComponent<VerticalLayoutGroup>();
            shopLayout.spacing = 8;
            shopLayout.childControlWidth = true; shopLayout.childForceExpandWidth = true;
            shopLayout.childControlHeight = true; shopLayout.childForceExpandHeight = false;
            var shopFit = _offerRow.gameObject.AddComponent<ContentSizeFitter>();
            shopFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var shopScroll = offerView.gameObject.AddComponent<ScrollRect>();
            shopScroll.viewport = offerView; shopScroll.content = _offerRow;
            shopScroll.horizontal = false;
            shopScroll.movementType = ScrollRect.MovementType.Clamped;
            shopScroll.scrollSensitivity = 30f;

            // The hover line: an unbought recipe says how it is made when pointed at.
            _marketTip = NewText("MarketTip", screen, _body, 12, TextAnchor.MiddleLeft,
                new Color(0.85f, 0.78f, 0.62f));
            _marketTip.rectTransform.anchorMin = new Vector2(0, 0);
            _marketTip.rectTransform.anchorMax = new Vector2(1, 0);
            _marketTip.rectTransform.pivot = new Vector2(0.5f, 0);
            _marketTip.rectTransform.sizeDelta = new Vector2(-24, 24);
            _marketTip.rectTransform.anchoredPosition = new Vector2(0, 4);
            _marketTip.supportRichText = true;
            _marketTip.horizontalOverflow = HorizontalWrapMode.Overflow;

            _openTomorrow = NewRect("OpenTomorrow", _dayEndPanel);
            Place(_openTomorrow, new Vector2(0.5f, 0), new Vector2(892, 40), new Vector2(0, 16));
            _openTomorrow.gameObject.AddComponent<Image>().color = UITheme.PrimaryAction;
            var otBtn = _openTomorrow.gameObject.AddComponent<Button>();
            otBtn.onClick.AddListener(OnDayEndAdvance);
            var otLabel = _openTomorrowLabel = NewText("Label", _openTomorrow, _display, 14, TextAnchor.MiddleCenter, UITheme.TextOnAmber);
            Stretch(otLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            otLabel.text = "OPEN TOMORROW →";
            _dayEndPanel.gameObject.SetActive(false);

            _bannerText = NewText("Closed", root, _display, 22, TextAnchor.MiddleCenter, UITheme.ViceRed[3]);
            Place(_bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 120), new Vector2(0, 60));
            _bannerText.gameObject.SetActive(false);

            BuildLedgerPanel(root);
        }

        /// <summary>The register's book of past days (GDD 24 §7, 2026-07-22): a scrollable
        /// list of every closed day — income, expenses, net, and the room's mood.</summary>
        private void BuildLedgerPanel(RectTransform root)
        {
            _ledgerPanel = NewRect("Ledger", root);
            Place(_ledgerPanel, new Vector2(0.5f, 0.5f), new Vector2(560, 560), new Vector2(0, 10));
            var panelImg = _ledgerPanel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(UITheme.Night[1].r, UITheme.Night[1].g, UITheme.Night[1].b, 0.98f);
            // Catch clicks so the world behind the book stays untouched.
            panelImg.raycastTarget = true;

            var title = NewText("Title", _ledgerPanel, _display, 15, TextAnchor.MiddleCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -44), new Vector2(0, -10));
            title.text = "THE REGISTER — DAYS PAST";

            // Column header, then the rows on cream stock beneath it.
            var header = NewText("Header", _ledgerPanel, _body, 12, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(header.rectTransform, new Vector2(0, 1), new Vector2(504, 20), new Vector2(28, -52));
            header.text = "DAY      INCOME     EXPENSES     NET      MOOD";

            var sheet = NewRect("Sheet", _ledgerPanel);
            Place(sheet, new Vector2(0.5f, 1), new Vector2(508, 424), new Vector2(0, -76));
            sheet.gameObject.AddComponent<Image>().color = UITheme.Cream[4];

            _ledgerRows = NewRect("Rows", sheet);
            Stretch(_ledgerRows, Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12));
            var layout = _ledgerRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperLeft;

            NewButton(_ledgerPanel, "CLOSE", new Vector2(0.5f, 0),
                new Vector2(200, 38), new Vector2(0, 18), UITheme.PrimaryAction, () => ToggleLedger());

            _ledgerPanel.gameObject.SetActive(false);
        }

        /// <summary>Opens or closes the register's ledger; refreshes the rows on open.
        /// The book and the licence never share the screen — opening one closes the other.</summary>
        private void ToggleLedger()
        {
            if (_ledgerPanel == null || Run == null) return;
            bool show = !_ledgerPanel.gameObject.activeSelf;
            if (show) { CloseId(); RefreshLedger(); }
            _ledgerPanel.gameObject.SetActive(show);
        }

        private void RefreshLedger()
        {
            for (int i = _ledgerRows.childCount - 1; i >= 0; i--)
                Destroy(_ledgerRows.GetChild(i).gameObject);

            var history = Run.Ledger.History;
            if (history.Count == 0)
            {
                var empty = NewText("Empty", _ledgerRows, _body, 14, TextAnchor.UpperLeft, UITheme.Night[1]);
                empty.rectTransform.sizeDelta = new Vector2(0, 28);
                empty.text = "No days on the books yet — close a night first.";
                return;
            }

            // Newest day on top: the last thing you did is the first thing you read.
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var d = history[i];
                var row = NewText($"Day{d.Day}", _ledgerRows, _body, 14, TextAnchor.UpperLeft,
                    d.Net < 0 ? UITheme.ViceRed[3] : UITheme.Night[1]);
                row.rectTransform.sizeDelta = new Vector2(0, 24);
                row.supportRichText = true;
                string net = d.Net < 0 ? $"-${-d.Net}" : $"+${d.Net}";
                row.text = $"Day {d.Day,-3}   ${d.Income,-6}   ${d.Expenses,-6}   {net,-6}   {MoodLabel(d.AverageSatisfaction)}";
            }
        }

        private static string MoodLabel(double satisfaction) =>
            satisfaction >= DayLedger.HighRollerBar ? "GREAT"
            : satisfaction >= DayLedger.BrokeBar ? "OK"
            : "SOUR";

        private void OnOpenTomorrow()
        {
            var run = Run;
            run.ContinueToNextDay();
            _dayEndPanel.gameObject.SetActive(false);
            if (run.Phase == TycoonPhase.DayOpen)
            {
                _lastPhase = TycoonPhase.DayOpen;
                ApplyBarLook();
            }
        }

        /// <summary>One shop listing: art, title, price, and a bought/maxed/can't-afford state.
        /// Nothing sells on credit (GDD 23 §6): an unaffordable card refuses with a notice.</summary>
        private RectTransform AddCard(string title, string sub, int price, bool available, Action onBuy,
            Sprite art = null)
        {
            var rt = NewRect("Card", _cardTarget != null ? _cardTarget : _offerRow);
            var img = rt.gameObject.AddComponent<Image>();
            bool afford = Run.Money >= price;
            img.color = !available ? UITheme.Night[0]
                : afford ? UITheme.Night[3]
                : new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.9f);
            if (available)
            {
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                button.onClick.AddListener(() =>
                {
                    if (!afford) { Toast("NOT ENOUGH MONEY"); return; }
                    try { onBuy(); }
                    catch (InvalidOperationException) { Toast("NOT ENOUGH MONEY"); }
                });
            }

            // The thing itself, at the right-hand end of the listing — a shop where every row
            // is the same block of text is a spreadsheet, and the bottles are already drawn.
            float textRight = -8f;
            if (art != null)
            {
                var thumb = NewRect("Art", rt);
                Place(thumb, new Vector2(1, 0.5f), new Vector2(44, 58), new Vector2(-6, 0));
                var ti = thumb.gameObject.AddComponent<Image>();
                ti.sprite = art;
                ti.preserveAspect = true;
                ti.raycastTarget = false;
                ti.color = available && afford ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                textRight = -54f;
            }

            var name = NewText("Name", rt, _body, 12, TextAnchor.UpperLeft,
                available ? (afford ? UITheme.TextPrimary : UITheme.Cream[1]) : UITheme.Cream[1]);
            Stretch(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 22), new Vector2(textRight, -6));
            name.text = title;
            var priceText = NewText("Price", rt, _body, 12, TextAnchor.LowerLeft,
                !available ? UITheme.Cream[1] : afford ? UITheme.Money : UITheme.ViceRed[3]);
            Stretch(priceText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 6), new Vector2(textRight, -50));
            priceText.text = available ? $"${price}" : sub;
            return rt;
        }

        // ── tiny UI helpers (mirroring the house style) ─────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private RectTransform Panel(RectTransform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rt = NewRect(name, parent);
            Stretch(rt, anchorMin, anchorMax, offsetMin, offsetMax);
            rt.gameObject.AddComponent<Image>().color = color;
            return rt;
        }

        private void NewButton(RectTransform parent, string label, Vector2 anchor,
            Vector2 size, Vector2 pos, Color fill, Action onClick)
        {
            var rt = NewRect(label, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = fill;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            // A face of its own, so the hover lift moves the label with the plate rather than
            // sliding the plate out from under it (PressSink moves one transform).
            var face = NewRect("Face", rt);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var sink = rt.gameObject.AddComponent<PressSink>();
            sink.Face = face; sink.Depth = 3f; sink.Squash = 0.015f; sink.Lift = 2f; sink.Tint = img;

            var text = NewText("Label", face, _body, 12, TextAnchor.MiddleCenter,
                fill == UITheme.PrimaryAction ? UITheme.TextOnAmber : UITheme.TextPrimary);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.text = label;
        }

        private Text NewText(string name, Transform parent, Font font, int size,
            TextAnchor anchor, Color color)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max,
            Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }
    }
}
